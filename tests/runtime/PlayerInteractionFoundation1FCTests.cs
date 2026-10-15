using System;
using System.IO;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;
using Fixture = PlayerInteractionFoundation1FBTests.Fixture;

internal static class PlayerInteractionFoundation1FCTests
{
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Player-1F-C: " + Message); }
    private static string Reject(Action Action)
    { try { Action(); } catch (InvalidOperationException Error) { return Error.Message; } throw new Exception("Player-1F-C: expected error"); }
    private static long Count(Fixture Value, object Definition)
    { return Runtime.PhysicalInventoryObservation.Count(Value.Source(), Definition); }

    internal static void RunModel()
    {
        var Take = new Runtime.PlayerTakeItemOperation();
        var Give = new Runtime.PlayerGiveItemOperation(Take.SharedGate);
        foreach (bool OuterGive in new[] {true, false}) foreach (bool InnerGive in new[] {true, false}) {
            var Value = new Fixture(); Value.Add(0, 0, 3);
            var Other = new Fixture(); Other.Add(0, 0, 3);
            Action Reenter = () => {
                string Error = Reject(() => {
                    if (InnerGive) Give.Execute("same", Value.View, Value.Definition, 1);
                    else Take.Execute("same", Value.View, Value.Definition, 1);
                });
                Check(Error.Contains("already in progress"), "all four recursive pairs rejected");
                Check(InnerGive ? Give.Execute("other", Other.View, Other.Definition, 1) :
                    Take.Execute("other", Other.View, Other.Definition, 1), "different exact Player independent");
            };
            if (OuterGive) Value.DuringTransfer = Reenter; else Value.DuringTake = Reenter;
            Check(OuterGive ? Give.Execute("same", Value.View, Value.Definition, 1) :
                Take.Execute("same", Value.View, Value.Definition, 1), "outer operation verifies");
            Check(Give.BusyCount == 0 && Give.TrackedResources == 0 && Take.BusyCount == 0, "nested operations release shared gate/resources");
        }

        // Separate definitions: a shop is deliberately two irreversible operations.
        object Wood = new object();
        foreach (bool Throw in new[] {false, true}) {
            var Shop = new Fixture(); Shop.Add(0, 0, 100);
            Check(Take.Execute("shop", Shop.View, Shop.Definition, 100), "shop payment removed");
            if (Throw) {
                Shop.Failure = "reject";
                Check(Reject(() => Give.Execute("shop", Shop.View, Wood, 1)).Contains("may have changed"), "shop grant error");
            } else Check(!Give.Execute("shop", Shop.View, Wood, 1281), "shop grant definite rejection");
            Check(Count(Shop, Shop.Definition) == 0 && Count(Shop, Wood) == 0, "no payment rollback or invented refund");
            Shop.Failure = "";
            Check(Give.Execute("shop", Shop.View, Wood, 1000) && Count(Shop, Wood) == 1000, "fresh explicit grant after shop failure");
        }
        var Observation = new Fixture(); Observation.Add(0, 0, 2);
        Check(Runtime.PhysicalInventoryObservation.Has(Observation.Source(), Observation.Definition, 2) &&
            Count(Observation, Observation.Definition) == 2, "observation before mutation");
        Observation.Take(Observation.Definition, 2); // Normal host change between separate calls.
        Check(!Take.Execute("observed", Observation.View, Observation.Definition, 2), "reads reserve nothing");

        // Reuse one exact token after every outcome; no operation history is necessary.
        for (int Index = 0; Index < 1000; ++Index) {
            var Value = new Fixture();
            Check(Give.Execute("stress", Value.View, Value.Definition, 11), "multi-chunk Give");
            Check(Take.Execute("stress", Value.View, Value.Definition, 10), "partial Take");
            Check(Give.Execute("stress", Value.View, Value.Definition, 9), "merge Give");
            Check(Take.Execute("stress", Value.View, Value.Definition, 10), "terminal Take");
            Check(!Take.Execute("stress", Value.View, Value.Definition, 1), "Take PREPARE false");
            Check(!Give.Execute("stress", Value.View, Value.Definition, Int32.MaxValue), "Give PREPARE false");
            Value.Failure = "reject";
            Check(Reject(() => Give.Execute("stress", Value.View, Value.Definition, 1)).Contains("may have changed"), "post-COMMIT error");
            Reject(() => Give.Execute("stress", Value.View, null, 1));
            Reject(() => Take.Execute("stress", () => null, Value.Definition, 1));
            Check(Count(Value, Value.Definition) == 0 && Give.BusyCount == 0 && Give.TrackedResources == 0, "stress physical and ownership baseline");
        }
        Take.Diagnostics.Attempts = UInt64.MaxValue; Give.Diagnostics.Attempts = UInt64.MaxValue;
        var Empty = new Fixture(); Take.Execute("saturate", Empty.View, null, 1);
        Give.Execute("saturate", Empty.View, Empty.Definition, Int32.MaxValue);
        Check(Take.Diagnostics.Attempts == UInt64.MaxValue && Give.Diagnostics.Attempts == UInt64.MaxValue, "diagnostics saturate");
        Console.WriteLine("[CarbonLuau:Player1FCModel] PASS four gate pairs, independent Players, nontransactional shop, non-reserving reads, 9000 combined stress calls");
    }

    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    internal static void RunNative(Runtime.NativeRuntime Native, string Repository)
    {
        var Value = new Fixture(); Value.Add(0, 0, 5);
        int Takes = 0, Teleports = 0;
        var View = Value.View();
        View.TakeInventory = (Definition, Amount) => { Takes++; return Value.Take(Definition, Amount); };
        View.Teleport = new Runtime.PlayerTeleportOperation(
            () => new Runtime.PlayerTeleportState {Current = true, Alive = true},
            (Destination, Before) => { Teleports++; }, (Destination, Before) => true);
        var Players = new Runtime.PlayerDirectory(Id => View); Players.Connect(View);
        var World = new Runtime.FacadeWorld(Players, new Registrar(),
            new Runtime.ItemDirectory(Name => Name == "scrap" ? Value.Definition : null));
        const string P = "local P=game:GetService('Players'):GetPlayers()[1]; ";
        const string Give = "assert(P:GetInventory():GiveItem('scrap',2)); ";
        const string Take = "assert(P:GetInventory():TakeItem('scrap',1)); ";
        const string Teleport = "P:Teleport(Vector3.new(1,2,3)); ";
        Func<Runtime.ScriptSnapshot> Snapshot = () => {
            var Result = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = P +
                "assert(not pcall(function() " + Give + "end)); assert(not pcall(function() " + Take +
                "end)); assert(not pcall(function() " + Teleport + "end))"};
            Result.Modules.Add("give", P + Give + "error('module fails after Give')");
            Result.Modules.Add("take", P + Take + "error('module fails after Take')");
            Result.Modules.Add("teleport", P + Teleport + "error('module fails after Teleport')");
            return Result;
        };
        using (var Host = new Runtime.ScriptHost(Native,
            new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "root candidate loads");
            Check(Value.Creates == 0 && Takes == 0 && Teleports == 0, "root candidate rejects all three mutations");
            // Cold require during a committed operation still creates a first-load publication scope.
            // A failed module must not perform any of these committed-only effects (D18).
            foreach (string Module in new[] {"give", "take", "teleport"}) {
                var Result = Host.Execute("player1fc.module." + Module,
                    "local Ok,Error=pcall(require,'" + Module + "'); assert(not Ok); print(Error)");
                Check(Result.Status == Runtime.RuntimeStatus.OK, "outer callback catches module failure");
                Console.WriteLine("[CarbonLuau:Player1FCModuleBoundary] " + Module +
                    " creates=" + Value.Creates + " takes=" + Takes + " teleports=" + Teleports +
                    " physical=" + Count(Value, Value.Definition) + " error=" + Result.Logs.Trim());
            }
            Check(World.GiveItems.BusyCount == 0 && World.GiveItems.TrackedResources == 0, "failure releases gate and references");
        }
        Check(World.Active == null && Native.LiveVmCount == 0, "probe tears down cleanly");
        Check(Value.Creates == 0 && Takes == 0 && Teleports == 0,
            "D18 STOP: failed first-load modules performed committed-only mutations: creates=" +
            Value.Creates + " takes=" + Takes + " teleports=" + Teleports);
        RunClosure(Native, Repository);
        RunC4(Native);
    }

    // Focused migration coverage lives in the existing 1F-C qualification harness.
    // Backend29/30 remain private reusable operations; their public Player aliases are absent.
    private static string Boundary(Runtime.FacadeSession Owner, uint Code, bool Success, params string[] Fields)
    {
        byte[] Bytes = Runtime.FacadePolicy.Pack(Fields);
        IntPtr Input = Marshal.AllocHGlobal(Math.Max(1,Bytes.Length)), Output = Marshal.AllocHGlobal(262144);
        try {
            if (Bytes.Length != 0) Marshal.Copy(Bytes,0,Input,Bytes.Length);
            uint Written;
            uint Status = Owner.Callback((ulong)Owner.DomainLifetimeId,Code,Input,(uint)Bytes.Length,Output,262144,out Written);
            Check(Status == (Success ? 0u : 1u) && Written <= 262144,"C4 bounded host response " + Code);
            var Response = new byte[Written]; if (Written != 0) Marshal.Copy(Output,Response,0,(int)Written);
            return Runtime.FacadePolicy.Utf8.GetString(Response).TrimEnd('\0');
        } finally { Marshal.FreeHGlobal(Input); Marshal.FreeHGlobal(Output); }
    }
    internal static void RunC4(Runtime.NativeRuntime Native)
    {
        var Value = new Fixture(); Value.Add(0,0,5);
        var View = Value.View(); var Players = new Runtime.PlayerDirectory(Id => View);
        var Lifetime = Players.Connect(View);
        var World = new Runtime.FacadeWorld(Players,new Registrar(),new Runtime.ItemDirectory(Name => Name == "scrap" ? Value.Definition : null));
        const string State = "local S=require('state'); ";
        const string Acquire = State + "S.P=game:GetService('Players'):GetPlayers()[1]; S.I=S.P:GetInventory(); ";
        const string Absent = "assert(game.ApiVersion=='0.7.5-experimental'); for _,K in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(S.P[K]==nil) end; ";
        const string Block = "for _,M in {'GiveItem','TakeItem'} do local Ok,Error=pcall(function() S.I[M](S.I,'scrap',1) end); assert(not Ok and string.find(Error,'requires a committed domain',1,true)) end; ";
        string Source = Acquire + Absent + Block;
        Func<Runtime.ScriptSnapshot> Snapshot = () => {
            var Result = new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource=Source};
            Result.Modules.Add("state","return {}");
            Result.Modules.Add("failed",State+"S.Leak=S.P:GetInventory(); "+Block+"error('failed C4 module')");
            Result.Modules.Add("nested","return require('failed')");
            Result.Modules.Add("child",State+"S.ChildInventory=S.P:GetInventory(); assert(S.ChildInventory:CountItem('scrap')>=0); return S.ChildInventory");
            Result.Modules.Add("parent","assert(require('child')~=nil); error('parent rollback after successful child')");
            return Result;
        };
        using (var Host = new Runtime.ScriptHost(Native,new Runtime.RuntimeConfig {MaxCallbackMilliseconds=100,FrameDrainBudgetMilliseconds=20},Snapshot,World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Value.Creates == 0,"C4 provisional Give/Take never COMMIT");
            var Owner = World.Active;
            string Token = Lifetime.Token, UserId = View.UserId;
            string Publication = Boundary(Owner,40,true,Token,UserId);
            Check(Boundary(Owner,26,true,Token,UserId,"scrap") == "5" &&
                Boundary(Owner,27,true,Token,UserId,"scrap","5") == "1","C4 backend26/27 physical parity");
            Check(Boundary(Owner,30,true,Token,UserId,"scrap","2","InventoryOnly") == "1" && Count(Value,Value.Definition) == 7,
                "C4 qualified private backend30 preserved");
            Check(Boundary(Owner,29,true,Token,UserId,"scrap","2") == "1" && Count(Value,Value.Definition) == 5,
                "C4 qualified private backend29 preserved");
            Execute(Host,State+Absent+"assert(S.I:GiveItem('scrap',2)); assert(S.I:CountItem('scrap')==7); assert(S.I:TakeItem('scrap',2)); assert(S.I:CountItem('scrap')==5); "+
                "assert(S.I:GiveItem('scrap',3,GiveItemBehavior.InventoryOnly)); assert(S.I:GiveItem('scrap',1,nil)); assert(S.I:TakeItem('scrap',4)); assert(S.I:CountItem('scrap')==5)");
            Check(Boundary(Owner,41,true,Token,UserId,Publication,"count","scrap") == "5" &&
                Boundary(Owner,41,true,Token,UserId,Publication,"has","scrap","5") == "1","C4 op41 shares same physical backend");
            int Created = Value.Creates, Transfers = Value.Transfers;
            Execute(Host,State+"assert(not S.I:GiveItem('scrap',2147483647)); assert(not S.I:TakeItem('scrap',6)); assert(not S.I:TakeItem('unknown',1)); "+
                "for _,M in {'GiveItem','TakeItem'} do for _,A in {0,-1,1.5,0/0,math.huge,2147483648,'1',true,{}} do assert(not pcall(function() S.I[M](S.I,'scrap',A) end)) end; "+
                "for _,N in {'','Scrap',' scrap','scrap ',string.rep('a',129),'scr\\0ap','é'} do assert(not pcall(function() S.I[M](S.I,N,1) end)) end; "+
                "assert(not pcall(function() S.I[M]({},'scrap',1) end)); assert(not pcall(function() S.I[M](S.I,'scrap') end)) end; "+
                "for _,B in {true,false,'InventoryOnly',Enum.DropMode.Replace,GuiFont.DroidSansMono,{},1} do assert(not pcall(function() S.I:GiveItem('scrap',1,B) end)) end; "+
                "assert(not pcall(function() S.I:GiveItem('unknown',1) end)); assert(S.I:CountItem('scrap')==5)");
            Check(Value.Creates == Created && Value.Transfers == Transfers,"C4 invalid/full/insufficient PREPARE has no physical COMMIT");
            Boundary(Owner,41,false,Token,UserId,Publication,"give","scrap","1");
            Boundary(Owner,41,false,Token,UserId,Publication,"take","scrap","1","InventoryOnly");
            Boundary(Owner,41,false,Token,UserId,"18446744073709551615","give","scrap","1","InventoryOnly");
            Check(Value.Creates == Created,"C4 malformed/stale private op41 cannot reach creation");
            Check(World.TakeItems.SharedGate.TryEnter(Token),"C4 existing shared gate acquired");
            try {
                Execute(Host,State+"for _,M in {'GiveItem','TakeItem'} do local Ok,Error=pcall(function() S.I[M](S.I,'scrap',1) end); assert(not Ok and string.find(Error,'already in progress',1,true)) end");
            } finally { World.TakeItems.SharedGate.Exit(Token); }
            Check(Value.Creates == Created && Value.Transfers == Transfers,"C4 captured facade respects original shared gate");
            Execute(Host,State+"assert(not pcall(require,'nested')); for _,M in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(not pcall(function() S.Leak[M](S.Leak,'scrap',1) end)) end; assert(S.I:CountItem('scrap')==5)");
            Execute(Host,State+"assert(not pcall(require,'parent')); for _,M in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(not pcall(function() S.ChildInventory[M](S.ChildInventory,'scrap',1) end)) end; assert(S.I:CountItem('scrap')==5)");
            Check(Value.Creates == Created && Value.Transfers == Transfers,"C4 successful child plus failed parent invalidates leaked facade with zero COMMITs");
            Source = Acquire+Block+"task.defer(function() S.I:GiveItem('scrap',1) end); error('failed C4 root')";
            Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && World.Active == Owner,"C4 failed replacement preserves defining owner");
            Drain(Host); Check(Value.Creates == Created,"C4 failed candidate publishes no mutation work");
            Execute(Host,State+"assert(S.I:GiveItem('scrap',1)); assert(S.I:TakeItem('scrap',1))");
            foreach (string Failure in new[] {"create","null","reject","transfer","world","insert-false","insert-throw"}) {
                Value.Failure = Failure;
                Execute(Host,State+"local Ok,Error=pcall(function() S.I:GiveItem('scrap',1) end); assert(not Ok and string.find(Error,'may have changed',1,true))");
                Value.Failure = "";
                Check(World.GiveItems.BusyCount == 0 && World.GiveItems.TrackedResources == 0,"C4 indeterminate Give releases gate/resources: "+Failure);
            }
            View.TakeInventory = (Definition,Amount) => Value.Take(Definition,Amount) - 1;
            Execute(Host,State+"local Ok,Error=pcall(function() S.I:TakeItem('scrap',1) end); assert(not Ok and string.find(Error,'may have changed',1,true))");
            View.TakeInventory = Value.Take;
            Players.Disconnect(View.UserId,View.Identity); View = Value.View(); View.Connection = new object(); Lifetime = Players.Connect(View);
            Execute(Host,State+Absent+"for _,M in {'CountItem','HasItem','GiveItem','TakeItem'} do local Ok,Error=pcall(function() S.I[M](S.I,'scrap',1) end); assert(not Ok and string.find(Error,'no longer connected',1,true)) end; "+
                "local P=game:GetService('Players'):GetPlayers()[1]; assert(P~=S.P); assert(P:GetInventory():GiveItem('scrap',1))");
            Source = Acquire+Absent+Block;
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Owner.Disposed,"C4 successful root replacement retires old inventory owner");
            Boundary(Owner,41,false,Token,UserId,Publication,"give","scrap","1","InventoryOnly");
            Created = Value.Creates; long Before = Count(Value,Value.Definition);
            var Result = Host.Execute("c4.recovery",State+"assert(S.I:GiveItem('scrap',1)); task.defer(function() S.I:GiveItem('scrap',1) end); while true do end");
            Check(Result.Status == Runtime.RuntimeStatus.TIMEOUT && Host.Ready,"C4 fatal recovery reconstructs facade authority");
            Drain(Host);
            Check(Value.Creates == Created+1 && Count(Value,Value.Definition) == Before+1 && World.TakeItems.BusyCount == 0 &&
                World.GiveItems.BusyCount == 0 && World.GiveItems.TrackedResources == 0,"C4 recovery has no rollback/replay or gate leak");
            Execute(Host,State+Absent+"assert(S.I:TakeItem('scrap',1))");
        }
        Check(World.Active == null && Native.LiveVmCount == 0,"C4 teardown converges VM/domain/resources");
        Console.WriteLine("[CarbonLuau:GameplayC4] PASS real VM, legacy absence, optional typed behavior, backend parity, publication, failure, gate, exact connection and recovery");
    }

    private static void Execute(Runtime.ScriptHost Host, string Source)
    { var Result = Host.Execute("player1fc", Source); Check(Result.Status == Runtime.RuntimeStatus.OK, Result.Error); }
    private static void Drain(Runtime.ScriptHost Host)
    {
        for (int Frame = 0; Frame < 100 && Host.HasWork; ++Frame)
            foreach (var Result in Host.Drain()) Check(Result.Status == Runtime.RuntimeStatus.OK, Result.Error);
        Check(!Host.HasWork, "bounded drain converged");
    }
    private static string ActionToken(Runtime.InMemoryGuiBackend Backend)
    {
        var Calls = Backend.Calls();
        for (int Index = Calls.Length - 1; Index >= 0; --Index) {
            if (Calls[Index].Plan == null) continue;
            foreach (var Element in Calls[Index].Plan.Elements) foreach (var Property in Element.Properties)
                if (Property.Id == Runtime.GuiRenderPropertyId.ActionCommand)
                    return Property.Value.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
        }
        throw new Exception("Player-1F-C: no GUI action published");
    }
    private static void RunClosure(Runtime.NativeRuntime Native, string Repository)
    {
        var Value = new Fixture {Limit = 1000}; object Wood = new object();
        int Takes = 0, Teleports = 0, Messages = 0;
        bool Allowed = true;
        var Position = new Runtime.PlayerPosition(1, 2, 3);
        Func<Runtime.PlayerView> ViewFactory = () => {
            var Result = Value.View(); Result.Connection = new object();
            Result.Position = () => Position; Result.Health = () => 75; Result.MaxHealth = () => 100;
            Result.Send = Text => { Messages++; }; Result.Permission = Name => Allowed;
            Result.TakeInventory = (Definition, Amount) => { Takes++; return Value.Take(Definition, Amount); };
            Result.Teleport = new Runtime.PlayerTeleportOperation(
                () => new Runtime.PlayerTeleportState {Current = true, Alive = true},
                (Destination, Before) => { Teleports++; Position = Destination; },
                (Destination, Before) => Position.X == Destination.X && Position.Y == Destination.Y && Position.Z == Destination.Z);
            return Result;
        };
        var View = ViewFactory(); var Players = new Runtime.PlayerDirectory(Id => View);
        var Lifetime = Players.Connect(View); var Backend = new Runtime.InMemoryGuiBackend();
        var World = new Runtime.FacadeWorld(Players, new Registrar(),
            new Runtime.ItemDirectory(Name => Name == "scrap" ? Value.Definition : Name == "wood" ? Wood : null), new Runtime.GuiConfig().Validate(), Backend);
        const string P = "local P=game:GetService('Players'):GetPlayers()[1]; ";
        const string Reads = "assert(P.Position==Vector3.new(1,2,3) and P.Health==75 and P.MaxHealth==100); assert(game:GetService('Items'):Exists('scrap')); P:GetInventory():CountItem('scrap'); P:GetInventory():HasItem('scrap'); assert(Vector3.new(1,2,3)*2==Vector3.new(2,4,6)); ";
        const string Mutate = "assert(P:GetInventory():GiveItem('scrap',2)); assert(P:GetInventory():TakeItem('scrap',1)); P:Teleport(Vector3.new(1,2,3)); ";
        const string Block = "for _,F in {function() P:GetInventory():GiveItem('scrap',1) end,function() P:GetInventory():TakeItem('scrap',1) end,function() P:Teleport(Vector3.new(1,2,3)) end,function() P:SendMessage('blocked') end} do local Ok,Err=pcall(F); assert(not Ok and string.find(Err,'requires a committed domain',1,true)) end; ";
        string Source = P + Reads + Block;
        Func<Runtime.ScriptSnapshot> Snapshot = () => {
            var Result = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
            Result.Modules.Add("state", "return {}");
            Result.Modules.Add("cached", P + Reads + "return function() " + Mutate + "end");
            foreach (string Name in new[] {"leaf", "commandcold", "guicold", "defercold"})
                Result.Modules.Add(Name, P + Reads + Block + "return true");
            Result.Modules.Add("nested", "assert(require('leaf')); local F=require('cached'); local Ok,Err=pcall(F); assert(not Ok and string.find(Err,'requires a committed domain',1,true)); return true");
            Result.Modules.Add("failed", P + "local S=require('state'); S.Attempts=(S.Attempts or 0)+1; " + Block +
                "game:GetService('Commands'):Register('leaked',{},function() " + Mutate + "end); task.defer(function() " + Mutate + "end); error('failed module')");
            Result.Modules.Add("fatal", "while true do end");
            return Result;
        };
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "candidate reads allowed, mutations blocked");
            Execute(Host, "require('state'); assert(require('nested')); for I=1,2 do assert(not pcall(require,'failed')) end; assert(require('state').Attempts==2)");
            Drain(Host);
            Check(Value.Creates == 0 && Takes == 0 && Teleports == 0 && Messages == 0 &&
                !World.Active.Invoke("leaked", View.UserId, new string[0]), "cold/nested/shared closures, retries and failed publication have zero effects");
            Execute(Host, "local F=require('cached'); assert(F==require('cached')); F()");
            Check(Value.Creates == 1 && Takes == 1 && Teleports == 1, "cached closure committed execution restored");

            Source = P + Reads + Block + "task.defer(function() " + Mutate + "end); error('candidate')";
            Check(Host.Reload().Status != Runtime.RuntimeStatus.OK, "failed replacement preserves old root"); Drain(Host);
            Check(Value.Creates == 1 && Takes == 1 && Teleports == 1, "failed candidate zero COMMITs");
            Execute(Host, "require('cached')()");
            for (int Index = 0; Index < 20; ++Index) {
                Execute(Host, P + "local S=require('state'); S.Old=P; S.OldInventory=P:GetInventory()");
                Players.Disconnect(View.UserId, View.Identity); View = ViewFactory(); Lifetime = Players.Connect(View);
                Execute(Host, "local P=require('state').Old; assert(P~=nil); " +
                    "for _,F in {function() return P.Position end,function() return P.Health end,function() return P.MaxHealth end," +
                    "function() return P:GetInventory():CountItem('scrap') end,function() return P:GetInventory():HasItem('scrap') end," +
                    "function() P:GetInventory():GiveItem('scrap',1) end,function() P:GetInventory():TakeItem('scrap',1) end,function() P:Teleport(Vector3.new(1,2,3)) end} do " +
                    "local Ok,Err=pcall(F); assert(not Ok and string.find(Err,'no longer connected',1,true)) end; " +
                    "local I=require('state').OldInventory; for _,M in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(P[M]==nil); local Ok,Err=pcall(function() I[M](I,'scrap',1) end); assert(not Ok and string.find(Err,'no longer connected',1,true)) end");
            }
            Check(Value.Creates == 2 && Takes == 2 && Teleports == 2, "stale proxies never retarget reconnect");
            Source = "game:GetService('Commands'):Register('reward',{permission='fixture.reward'},function(C) assert(require('commandcold')); require('cached')() end); " +
                P + "local S=game:GetService('Gui'):Create('ScreenGui'); local B=S:Create('TextButton'); B.Activated:Connect(function(Who) assert(require('guicold')); require('cached')() end); S:Show(P); " +
                "task.defer(function() assert(require('defercold')); require('cached')() end)";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "reward publication"); Drain(Host);
            Allowed = false; Check(!World.Active.Invoke("reward", View.UserId, new string[0]), "denied command no entry");
            Allowed = true; Check(World.Active.Invoke("reward", View.UserId, new string[0]), "command admitted");
            string Action = ActionToken(Backend); Check(World.AdmitGuiAction(Lifetime, Action), "GUI reward admitted");
            Check(Value.Creates == 3, "ingress no inline entry"); Drain(Host);
            Check(Value.Creates == 5 && Takes == 5 && Teleports == 5, "three cold callback paths blocked; cached effects allowed afterward");
            Check(World.AdmitGuiAction(Lifetime, Action), "old queued GUI work");
            Source = "-- clean recovery source";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "replace root"); Drain(Host);
            Check(Value.Creates == 5 && !World.AdmitGuiAction(Lifetime, Action), "old queued authority retired");
            for (int Index = 0; Index < 10; ++Index) {
                Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "explicit reload rearms recovery");
                int Created = Value.Creates, Removed = Takes, Moved = Teleports;
                var Result = Host.Execute("player1fc.recovery", P + Reads + Mutate + "task.defer(function() " + Mutate + "end); pcall(require,'fatal')");
                Check(Result.Status == Runtime.RuntimeStatus.TIMEOUT, "cold module inherits uncatchable admitted deadline"); Drain(Host);
                Check(Value.Creates == Created + 1 && Takes == Removed + 1 && Teleports == Moved + 1, "no operation or queued effect replay");
                Check(World.GiveItems.BusyCount == 0 && World.GiveItems.TrackedResources == 0, "recovery resource baseline");
                Execute(Host, P + Reads + "assert(require('nested')); require('cached')()");
            }
            if (File.Exists(Path.Combine(Repository, "examples", "player-status", "init.luau"))) {
                foreach (string Example in new[] {"player-status", "player-inventory", "player-give-item", "player-take-item", "player-shop", "gui/inventory-reward"}) {
                    Source = File.ReadAllText(Path.Combine(Repository, "examples", Example, "init.luau"));
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "example loads: " + Example); Drain(Host);
                    long Before = Count(Value, Value.Definition), BeforeWood = Count(Value, Wood);
                    if (Example == "player-inventory") continue;
                    if (Example == "gui/inventory-reward") {
                        Check(World.AdmitGuiAction(Lifetime, ActionToken(Backend)), "example GUI action admitted"); Drain(Host);
                        Check(Count(Value, Value.Definition) == Before + 10, "GUI example delivers reward"); continue;
                    }
                    string Command = Example == "player-status" ? "playerstatus" : Example == "player-give-item" ? "reward" : Example == "player-shop" ? "buywood" : "takescrap";
                    if (Example == "player-shop" || Example == "player-take-item") { Execute(Host, P + "assert(P:GetInventory():GiveItem('scrap',100))"); Before += 100; }
                    Check(World.Active.Invoke(Command, View.UserId, new string[0]), "example command admitted"); Drain(Host);
                    long Expected = Example == "player-status" ? Before : Example == "player-give-item" ? Before + 10 : Before - 100;
                    Check(Count(Value, Value.Definition) == Expected, "example physical result: " + Example);
                    if (Example == "player-shop") Check(Count(Value, Wood) == BeforeWood + 1000, "shop wood result");
                }
                Console.WriteLine("[CarbonLuau:Player1FCExamples] PASS status, observation, Take, command/GUI Give and nontransactional shop through real VM");
            }
        }
        Check(World.Active == null && World.GiveItems.BusyCount == 0 && World.GiveItems.TrackedResources == 0, "unload baseline");
        Console.WriteLine("[CarbonLuau:Player1FCNative] PASS cold/nested/cached/command/GUI/deferred, read-only, retries, restoration, 20 reconnects, 10 recoveries and unload");
    }
}
