using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class FacadeTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        public Runtime.FacadeSession Active;
        public bool RejectNext;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        {
            if (RejectNext) { RejectNext = false; throw new InvalidOperationException("fixture command collision"); }
            Active = Next;
        }
    }
    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("Phase 3: " + Message); }
    private static string Drain(Runtime.ScriptHost Host)
    {
        string Logs = "";
        for (int Frame = 0; Frame < 200 && Host.HasWork; ++Frame)
            foreach (var Result in Host.Drain()) Logs += Result.Logs;
        return Logs;
    }
    public static void Run(Runtime.NativeRuntime Native, string Repository = null)
    {
        const string UserId = "76561198000000001";
        var Views = new Dictionary<string, Runtime.PlayerView>();
        var Messages = new List<string>(); bool Allowed = false;
        Func<Runtime.PlayerView> View = () => new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Fixture Player", Connected = true, Send = Message => Messages.Add(Message), Permission = Permission => Allowed };
        var Directory = new Runtime.PlayerDirectory(Id => Views.ContainsKey(Id) ? Views[Id] : null);
        var Registrar = new Registrar(); var World = new Runtime.FacadeWorld(Directory, Registrar);
        string Source = "", FailureModule = null;
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> SnapshotSource = () => {
            var Snapshot = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
            Snapshot.Modules.Add("state", "return {}");
            if (FailureModule != null) Snapshot.Modules.Add("caughtresource", FailureModule);
            return Snapshot;
        };
        const string ReadState = "local State=require('state'); local P,Old,Snapshot,C=State.P,State.Old,State.Snapshot,State.C; ";
        using (var Host = new Runtime.ScriptHost(Native, Config, SnapshotSource, World)) {
            Action<string> Load = Text => { Source = ReadState + Text + "; State.P=P; State.Old=Old; State.Snapshot=Snapshot; State.C=C"; var Result = Host.Reload(); Check(Result.Status == Runtime.RuntimeStatus.OK, "load: " + Result.Error); };
            Action<string> Execute = Text => { var Result = Host.Execute("facade.test", ReadState + Text); Check(Result.Status == Runtime.RuntimeStatus.OK, "execute: " + Result.Error); };
            Load("local P=game:GetService('Players'); assert(#P:GetPlayers()==0); assert(P==game:GetService('Players')); assert(game:GetService('Commands')==game:GetService('Commands')); assert(game.ApiVersion=='0.4.0-experimental'); assert(not pcall(function() game:GetService('X') end)); assert(not pcall(function() game:GetService(1) end)); assert(not pcall(function() P:GetPlayerByUserId(123) end)); assert(P:GetPlayerByUserId('123')==nil); assert(__hostcall==nil and debug==nil and getfenv==nil)");
            FailureModule = "print('module-attempt'); game:GetService('Commands'):Register('moduleleak',{},function() end); task.defer(function() error('module task leaked') end); error('caught resource failure')";
            Source = "assert(not pcall(require,'caughtresource')); assert(not pcall(require,'caughtresource'))";
            var FailedModule = Host.Reload();
            Check(FailedModule.Status == Runtime.RuntimeStatus.OK && FailedModule.Logs == "module-attempt\nmodule-attempt\n", "caught failed module retries inside successful candidate");
            Check(!Registrar.Active.Commands.ContainsKey("moduleleak") && !Host.HasWork && Host.Status().Contains("modules: 0"), "caught failed module publishes no cache, command or task");
            FailureModule = null;
            Views[UserId] = View(); var Lifetime = Directory.Connect(Views[UserId]);
            Load("P=game:GetService('Players'); Old=P:GetPlayers()[1]; Snapshot=P:GetPlayers(); assert(Old==P:GetPlayerByUserId('" + UserId + "')); assert(Old.UserId=='" + UserId + "' and type(Old.UserId)=='string'); assert(Old.Name=='Fixture Player' and Old.IsConnected); assert(not pcall(function() Old.Name='forged' end)); assert(not pcall(function() Old.SendMessage({},'forged') end)); assert(not pcall(function() Old:SendMessage('provisional') end)); task.defer(function() Old:SendMessage('committed') end)");
            Check(Messages.Count == 0, "D10 caught provisional message has no effect"); Drain(Host); Check(Messages.Count == 1 && Messages[0] == "committed", "D10 deferred delivery after commit");
            var Second = View(); Second.UserId = "76561198000000002"; Second.Name = "Second Player";
            Views[Second.UserId] = Second; Directory.Connect(Second);
            Execute("assert(#P:GetPlayers()==2 and #Snapshot==1); assert(P:GetPlayers()[2].Name=='Second Player')");
            Directory.Disconnect(Second.UserId,Second.Identity); Views.Remove(Second.UserId);
            var Previous = Registrar.Active;
            Source = "local P=game:GetService('Players'):GetPlayers()[1]; task.defer(function() P:SendMessage('leak') end); P:SendMessage('illegal')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Registrar.Active == Previous, "D10 unhandled send rejects candidate");
            Drain(Host); Check(Messages.Count == 1, "failed candidate never sends deferred message");
            Execute("assert(not pcall(function() Old:SendMessage(string.rep('x',1025)) end)); assert(not pcall(function() Old:HasPermission('INVALID') end)); assert(not Old:HasPermission('fixture.allowed'))");
            var NormalSend = Views[UserId].Send;
            Views[UserId].Send = Message => { Host.Status(); };
            Execute("local Ok,Error=pcall(function() Old:SendMessage('reentrant') end); assert(not Ok and string.find(Error,'host operation failed',1,true))");
            Views[UserId].Send = Message => { throw new InvalidOperationException("private host detail must not leak"); };
            Execute("local Ok,Error=pcall(function() Old:SendMessage('host failure') end); assert(not Ok and not string.find(Error,'private host detail',1,true))");
            Views[UserId].Send = NormalSend;
            Views[UserId].Connected = false;
            Execute("assert(not Old.IsConnected)");
            Views[UserId].Connected = true;
            Execute("assert(not Old.IsConnected)");
            Check(Directory.Resolve(Lifetime.Token,UserId)==null,"observed invalidation is permanent even if host object/connection is reused");
            var Removed = Directory.Disconnect(UserId, Views[UserId].Identity); Views.Remove(UserId);
            Execute("assert(not Old.IsConnected and Old.Name=='Fixture Player'); assert(#Snapshot==1); assert(not pcall(function() Old:SendMessage('stale') end)); assert(not pcall(function() Old:HasPermission('fixture.allowed') end))");
            Views[UserId] = View(); var Reconnected = Directory.Connect(Views[UserId]);
            Check(Reconnected.Token != Removed.Token && Directory.Resolve(Removed.Token, UserId) == null && Directory.Resolve("forged", UserId) == null, "reconnect/forged token never retargets");
            Execute("assert(not Old.IsConnected); assert(P:GetPlayers()[1]~=Old and P:GetPlayers()[1].IsConnected)");

            Load("local P=game:GetService('Players'); P.PlayerAdded:Connect(function() print('first') end); C=P.PlayerAdded:Connect(function() print('second') end); P.PlayerAdded:Connect(function() error('listener error') end); P.PlayerAdded:Connect(function() print('last') end); P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected); print(V.UserId) end)");
            Check(!Host.HasWork, "no synthetic joins for current players"); World.Event("added", Reconnected);
            Execute("C:Disconnect(); C:Disconnect()");
            Check(Drain(Host) == "first\nlast\n", "registration order, queued disconnect and listener error isolation");
            Source = "game:GetService('Players').PlayerAdded:Connect(function() print('leak') end); error('bad')";
            Previous = Registrar.Active; Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Registrar.Active == Previous, "failed candidate preserves listeners");
            World.Event("added", Reconnected); Check(Drain(Host) == "first\nlast\n", "preserved listeners actually execute");
            Removed = Directory.Disconnect(UserId, Views[UserId].Identity); Views.Remove(UserId); World.Event("removing", Removed);
            Check(Drain(Host) == UserId + "\n", "removing retains safe snapshot after host disappears");
            Views[UserId] = View(); Reconnected = Directory.Connect(Views[UserId]);

            string Command = "game:GetService('Commands'):Register('hello',{permission='carbonluau.example.hello',description='Hello'},function(C) assert(C.Name=='hello' and C.Player.UserId=='" + UserId + "'); print(C.Arguments[1]); C.Player:SendMessage('A') end)";
            Load(Command);
            Check(!Registrar.Active.Invoke("hello", UserId, new[] {"denied"}) && !Host.HasWork, "permission denial before native admission");
            Allowed = true; string[] Arguments = {"snapshot"}; Check(Registrar.Active.Invoke("hello", UserId, Arguments), "authorized command admitted"); Arguments[0] = "changed";
            Check(Drain(Host) == "snapshot\n" && Messages[Messages.Count - 1] == "A", "argument snapshot and messaging");
            Check(!Registrar.Active.Invoke("hello", UserId, new string[17]) && !Registrar.Active.Invoke("hello", UserId, new[] {new string('x',513)}), "argument bounds");
            var ExcessTotal=new string[9]; for(int Index=0; Index<ExcessTotal.Length; ++Index) ExcessTotal[Index]=new string('x',512);
            Check(!Registrar.Active.Invoke("hello", UserId, ExcessTotal) && !Registrar.Active.Invoke("hello",UserId,new[]{"embedded\0NUL"}),"total/NUL argument bounds");
            Check(Registrar.Active.Invoke("hello", UserId, new[] {"revoked"}), "queued authorization fixture"); Allowed = false;
            Check(Drain(Host) == "", "permission recheck immediately before Lua entry"); Allowed = true;
            Previous = Registrar.Active;
            Source = Command.Replace("'A'", "'B'") + "; error('candidate B')";
            Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Registrar.Active == Previous, "B never published");
            Check(Previous.Invoke("hello", UserId, new[] {"A retained"}) && Drain(Host) == "A retained\n", "A command survives B");
            Registrar.RejectNext = true; Source = Command; Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Registrar.Active == Previous, "host publication failure preserves A");
            Check(Previous.Invoke("hello", UserId, new[] {"old queued"}), "old work pending before C");
            Load(Command.Replace("'A'", "'C'"));
            Check(!Previous.Invoke("hello", UserId, new[] {"late selected A"}) && Drain(Host) == "", "selected late A and old queued work cannot enter C");
            Check(Registrar.Active.Invoke("hello", UserId, new[] {"C active"}) && Drain(Host) == "C active\n" && Messages[Messages.Count-1] == "C", "C atomically replaces A");
            Load("game:GetService('Commands'):Register('hello',{},function(C) assert(not pcall(function() C.Name='changed' end)); assert(not pcall(function() C.Arguments[1]='changed' end)); if C.Arguments[1]=='error' then error('command fixture error') end; print('command survived') end)");
            Check(Registrar.Active.Invoke("hello",UserId,new[]{"error"}) && Registrar.Active.Invoke("hello",UserId,new[]{"ok"}),"command error fixture admitted");
            Check(Drain(Host)=="command survived\n" && Host.Ready,"command error is isolated and context/arguments are immutable");
            Check(Registrar.Active.Invoke("hello",UserId,new[]{"disconnect queued"}),"command pending before disconnect");
            Directory.Disconnect(UserId,Views[UserId].Identity); Views.Remove(UserId);
            Check(!Registrar.Active.Invoke("hello",UserId,new[]{"disconnected"}) && Drain(Host)=="","disconnected caller and queued old connection never enter Lua");
            Views[UserId]=View(); Reconnected=Directory.Connect(Views[UserId]);
            bool WrongThreadRejected=false;
            var OtherThread=new System.Threading.Thread(()=>{ try { World.Event("added",Reconnected); } catch (InvalidOperationException) { WrongThreadRejected=true; } });
            OtherThread.Start(); OtherThread.Join(); Check(WrongThreadRejected,"facade rejects off-thread host intake");
            foreach (string Invalid in new[] {"", "Bad", "quit", "carbonluau", "a.b", new string('a',33)}) {
                Source = "game:GetService('Commands'):Register('" + Invalid + "',{},function() end)";
                Check(Host.Reload().Status != Runtime.RuntimeStatus.OK, "invalid/protected command name");
            }
            foreach (string Bad in new[] {
                "local C=game:GetService('Commands'); C:Register('hello',{},function() end); C:Register('hello',{},function() end)",
                "for I=1,65 do game:GetService('Commands'):Register('cmd'..I,{},function() end) end",
                "game:GetService('Commands'):Register('hello',{permission='bad'},function() end)",
                "game:GetService('Commands'):Register('hello',{permission=1},function() end)",
                "game:GetService('Commands'):Register('hello',{extra=true},function() end)",
                "for I=1,129 do game:GetService('Players').PlayerAdded:Connect(function() end) end"}) {
                Source = Bad; Check(Host.Reload().Status != Runtime.RuntimeStatus.OK, "registration/options bounds");
            }
            Load("local S=game:GetService('Players').PlayerAdded; for I=1,2000 do local C=S:Connect(function() end); C:Disconnect(); C:Disconnect() end");
            World.Event("added", Reconnected); Check(!Host.HasWork, "2000 disconnects release registrations");
            Load("game:GetService('Commands'):Register('hello',{},function() print('public') end)");
            Allowed=false; Check(Registrar.Active.Invoke("hello",UserId,new string[0]) && Drain(Host)=="public\n","no permission is explicitly public"); Allowed=true;
            Execute("assert(not pcall(function() game:GetService('Commands'):Register('later',{},function() end) end))");
            Load("game:GetService('Players').PlayerAdded:Connect(function() print('queued') end)");
            for(int Index=0; Index<1000; ++Index) World.Event("added",Reconnected);
            Check(Registrar.Active.PendingCount==256 && Registrar.Active.Rejected==744,"managed admission bound rejects excess events");
            Load(Command); Check(!Host.HasWork,"replacement cancels bounded intake");
            var Watch = Stopwatch.StartNew();
            for (int Cycle=0; Cycle<100; ++Cycle) {
                Previous = Registrar.Active; Load(Command);
                Check(!Previous.Invoke("hello",UserId,new[]{"late"}), "stress late generation");
                for (int Index=0; Index<20; ++Index) {
                    var Old = Reconnected;
                    Directory.Disconnect(UserId,Views[UserId].Identity); Views[UserId]=View(); Reconnected=Directory.Connect(Views[UserId]);
                    Check(Directory.Resolve(Old.Token,UserId)==null && Directory.Resolve(Reconnected.Token,UserId)!=null,"2000 reconnect checks");
                }
                Check(Registrar.Active.Invoke("hello",UserId,new[]{"stress"}),"stress command"); Drain(Host);
            }
            Console.WriteLine("[CarbonLuau:FacadeTest] 100 registration replacements + 2000 reconnects: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            foreach (int Count in new[]{1,10,100}) {
                Load("for I=1,"+Count+" do game:GetService('Players').PlayerAdded:Connect(function(P) assert(P.IsConnected) end) end");
                Watch.Restart(); World.Event("added",Reconnected); Drain(Host);
                Console.WriteLine("[CarbonLuau:FacadeTest] dispatch " + Count + " listeners: " + Watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
            }
            Watch.Restart(); Execute("local P=game:GetService('Players'); for I=1,2000 do assert(P:GetPlayerByUserId('"+UserId+"').IsConnected); assert(#P:GetPlayers()==1) end");
            Console.WriteLine("[CarbonLuau:FacadeTest] 2000 proxy lookup/snapshot pairs: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            Previous=Registrar.Active; Host.Dispose(); Check(Registrar.Active==null && !Previous.Invoke("hello",UserId,new[]{"unload"}) && !Host.HasWork,"unload removes all host registrations");
        }
        var Tight = new Runtime.RuntimeConfig {MaxCallbackMilliseconds=3}; string Runaway = "game:GetService('Players').PlayerAdded:Connect(function() while true do end end)";
        using (var Host = new Runtime.ScriptHost(Native,Tight,()=>new Runtime.ScriptSnapshot{EntryName="init.luau",EntrySource=Runaway},World)) {
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"runaway signal init");
            World.Event("added",Directory.Find(UserId)); Drain(Host);
            Check(Host.Ready && Host.Recoveries==1 && Host.Timeouts==1,"signal timeout reconstructs once");
            World.Event("added",Directory.Find(UserId)); Drain(Host);
            Check(!Host.Ready && Host.Timeouts==2 && World.Active==null,"second timeout retires signals and commands");
        }
        Check(Native.LiveVmCount==0,"all facade VMs destroyed");
        using (var Host = new Runtime.ScriptHost(Native,Config,()=>new Runtime.ScriptSnapshot{EntryName="init.luau",EntrySource="game:GetService('Players').PlayerAdded:Connect(function(P) P:SendMessage('stop'); print('returned') end); game:GetService('Players').PlayerAdded:Connect(function() print('must not run') end)"},World)) {
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"nested stop fixture init");
            var NormalSend=Views[UserId].Send;
            Views[UserId].Send=Message=>Host.RequestStop();
            try {
                World.Event("added",Directory.Find(UserId));
                Check(Drain(Host)=="returned\n" && !Host.Ready,"nested stop returns from current native call without admitting another callback");
                Host.Dispose(); Check(World.Active==null && Native.LiveVmCount==0,"outer teardown releases stopped generation");
            } finally { Views[UserId].Send=NormalSend; }
        }
        if (Repository != null) {
            foreach (string Example in new[]{"player-events", "hello-command"}) {
                string Text=File.ReadAllText(Path.Combine(Repository,"examples",Example,"init.luau"));
                using (var Host=new Runtime.ScriptHost(Native,Config,()=>new Runtime.ScriptSnapshot{EntryName="init.luau",EntrySource=Text},World))
                    Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"shipped example loads: "+Example);
            }
            Console.WriteLine("[CarbonLuau:FacadeTest] PASS both shipped public API examples loaded through real compiler/VM");
        }
        Console.WriteLine("[CarbonLuau:FacadeTest] PASS services, proxies, lifetime, D10, signals, transactional commands, permissions, bounds, stress, recovery");
    }
}
