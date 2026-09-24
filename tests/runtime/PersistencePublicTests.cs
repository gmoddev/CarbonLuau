using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

// Real pinned compiler/VM -> production facade -> supervised production SQLite
// worker. Synthetic completion races are separately identified, never counted
// as evidence of a durable transaction.
internal static partial class PersistencePublicTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        internal int Publications;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { if (Next != null) ++Publications; }
    }
    private static void Check(bool Value, string Message)
    { if (!Value) throw new Exception("Persistence-1B: " + Message); }
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.NativeRuntime Native;
        internal readonly Runtime.StorageQueue Queue;
        internal readonly Runtime.StorageSupervisor Worker;
        internal readonly Runtime.ScriptHost Host;
        internal readonly Runtime.AddonRegistry Addons;
        internal readonly Registrar Commands = new Registrar();
        internal readonly StringBuilder Logs = new StringBuilder();
        internal readonly Dictionary<string,string> Modules = new Dictionary<string,string>();
        internal string Source = "return true";
        internal double WorkerReadyObservedMilliseconds;
        private bool Disposed;
        internal Fixture(Runtime.NativeRuntime Native, string Executable, string Directory, Func<ulong> Clock = null, bool Initialize = true)
        {
            this.Native = Native;
            Check(Native.Storage == null, "isolated public fixture");
            Queue = new Runtime.StorageQueue((ulong)Native.HostLifetimeId, Clock ?? (() => Runtime.StorageProcess.Now));
            Native.Storage = Queue;
            try {
            var StartupObservation = Stopwatch.StartNew();
            if (Executable != null) {
                Worker = new Runtime.StorageSupervisor(Queue, Executable, Directory);
                Worker.Start();
            } else Queue.Ready = true;
            Modules.Add("state", "return {}");
            var Config = new Runtime.RuntimeConfig { MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20, MaxQueuedCallbacks = 64 };
            var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), Commands);
            Host = new Runtime.ScriptHost(Native, Config, () => {
                var Snapshot = new Runtime.ScriptSnapshot { EntryName = "init.luau", EntrySource = Source };
                foreach (var Entry in Modules) Snapshot.Modules.Add(Entry.Key, Entry.Value);
                return Snapshot;
            }, World);
            Addons = new Runtime.AddonRegistry(Host, Native.HostLifetimeId);
            var End = Stopwatch.StartNew();
            while (!Queue.Ready && End.ElapsedMilliseconds < 35000) { Worker.Tick(); Thread.Sleep(5); }
            Check(Queue.Ready, "production worker ready: " + Status);
            WorkerReadyObservedMilliseconds = StartupObservation.Elapsed.TotalMilliseconds;
            if (Initialize) Reload();
            } catch {
                try { Dispose(); }
                catch (Exception Cleanup) { Console.Error.WriteLine("[CarbonLuau:Persistence1B] partial fixture cleanup: " + Cleanup.Message); }
                throw;
            }
        }
        internal void Reload()
        {
            var Result = Host.Reload();
            Check(Result.Status == Runtime.RuntimeStatus.OK, "reload: " + Result.Error);
            Logs.Append(Result.Logs);
        }
        internal Runtime.ExecutionResult Execute(string Source)
        {
            var Result = Host.Execute("persistence.public", Source);
            Logs.Append(Result.Logs);
            Check(Result.Status == Runtime.RuntimeStatus.OK, "Luau: " + Result.Error);
            return Result;
        }
        internal void Tick(bool Drain = true)
        {
            if (Worker != null) Worker.Tick();
            else Queue.BeginTick();
            Native.PumpStorage();
            if (Drain) DrainChecked();
        }
        internal string Status { get { return Worker == null ? "synthetic transport; pending=" + Queue.PendingCount : Worker.Status; } }
        internal void DrainChecked()
        {
            foreach (var Result in Host.Drain()) {
                Logs.Append(Result.Logs);
                Check(Result.Status == Runtime.RuntimeStatus.OK, "callback: " + Result.Error);
            }
        }
        internal void Until(string Marker, int Milliseconds = 12000)
        {
            var Time = Stopwatch.StartNew();
            while (!Logs.ToString().Contains(Marker) && Time.ElapsedMilliseconds < Milliseconds) { Tick(); Thread.Sleep(5); }
            Check(Logs.ToString().Contains(Marker), "missing " + Marker + "; " + Status + "; " + Logs);
            for (int Index = 0; Index < 3; ++Index) { Tick(); Thread.Sleep(5); }
        }
        internal void Refill()
        {
            var Time = Stopwatch.StartNew();
            while (Time.ElapsedMilliseconds < 1700) { Tick(); Thread.Sleep(5); }
        }
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            try {
            if (Addons != null) Addons.Dispose();
            if (Host != null) Host.Dispose();
            Queue.RetireAll();
            if (Worker != null) Worker.Stop();
            var Time = Stopwatch.StartNew();
            while (Worker != null && !Worker.IsFinished && Time.ElapsedMilliseconds < 10000) Thread.Sleep(5);
            Check(Worker == null || Worker.IsFinished, "worker teardown");
            if (Worker == null) foreach (var Request in Reservations(Queue))
                if (Request != null && Request.State == 1) Runtime.StorageQueue.Fail(Request);
            Queue.Dispatch(); Native.PumpStorage();
            Check(Queue.PendingCount == 0 && Native.LiveVmCount == 0, "queue and VM teardown");
            } finally { Native.Storage = null; }
        }
    }
    private const string Store = "local S=game:GetService('DataStoreService'):GetDataStore('Players'); ";
    private static byte[] Package(string Id, string Init, string Api = null, string Dependencies = null)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                var Manifest = "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" +
                    (Api == null ? "" : ",\"main\":\"api\"") +
                    (Dependencies == null ? "" : ",\"dependencies\":" + Dependencies) + "}";
                var Files = new Dictionary<string,string> {{"addon.json",Manifest},{"init.luau",Init}};
                if (Api != null) Files.Add("api.luau", Api);
                foreach (var File in Files) using (var Writer = new StreamWriter(Zip.CreateEntry(File.Key).Open(), new UTF8Encoding(false))) Writer.Write(File.Value);
            }
            return Output.ToArray();
        }
    }
    private static void RoundTrips(Runtime.NativeRuntime Native, string Worker, string Directory)
    {
        var Watch = Stopwatch.StartNew();
        using (var F = new Fixture(Native, Worker, Directory)) {
            long Before = F.Worker.RequestsSent;
            F.Execute(@"
local D=game:GetService('DataStoreService')
assert(type(D.GetDataStore)=='function')
local S=D:GetDataStore('Players')
for _,Name in {'Path','SQL','NamespaceId','Query','UpdateAsync','Name','Close'} do
 local OK,V=pcall(function() return S[Name] end); assert(not OK or V==nil)
end
assert(game.ApiVersion=='0.5.0-experimental')
local function Bad(F) assert(not pcall(F)) end
Bad(function() D:GetDataStore('Players','spoofed-namespace') end)
Bad(function() D.NamespaceId='spoofed-namespace' end)
Bad(function() S.NamespaceId='spoofed-namespace' end)
local MetamethodCalls=0
local function Invoked() MetamethodCalls+=1; error('metamethod must not run') end
local WithMeta=setmetatable({Value=true},{__index=Invoked,__iter=Invoked,__len=Invoked,__tostring=Invoked})
Bad(function() S:SetAsync('metamethod',WithMeta,function() error('rejected callback') end) end)
Bad(function() D:GetDataStore(WithMeta) end)
Bad(function() S:GetAsync(WithMeta,function() error('rejected callback') end) end)
assert(MetamethodCalls==0)
for _,V in { '', '.', '..', 'a/b', 'a\\b', 'a:b', '\0', '\127', '\255', string.rep('x',65) } do Bad(function() D:GetDataStore(V) end) end
Bad(function() D:GetDataStore(1) end)
for _,V in { '', '.', '..', 'a/b', 'a\\b', 'a:b', '\0', '\127', '\255', string.rep('x',129) } do Bad(function() S:GetAsync(V,function() end) end) end
Bad(function() S:GetAsync(1,function() end) end)
Bad(function() S:GetAsync('x',nil) end)
Bad(function() S:SetAsync('x',true,false) end)
Bad(function() S:RemoveAsync('x',{}) end)
Bad(function() S:SetAsync('x',nil,function() end) end)
local Cycle={} Cycle.Self=Cycle
local Depth={} local P=Depth for I=1,17 do P.Next={} P=P.Next end
local Many={} for I=1,1025 do Many[I]=true end
local Mixed={[1]=1, X=2}
for _,V in { Cycle, Depth, Many, Mixed, {[2]=true}, {[1e100]=true}, {[true]=true},
  setmetatable({},{__index=function() error('must not invoke') end}), function() end,
  coroutine.create(function() end), S, D, Vector3.new(1,2,3), buffer.create(4),
  0/0, 1/0, -1/0, '\255', 'a\0b', string.rep('x',16385), {[string.rep('k',129)]=1} } do
  Bad(function() S:SetAsync('x',V,function() end) end)
end
assert(not pcall(function() S:GetAsync('x',function() end,'extra') end))
print('validation-done')
");
            Check(F.Queue.PendingCount == 0 && F.Worker.RequestsSent == Before, "invalid arguments enqueue zero");
            F.Execute(Store + @"
S:GetAsync('absent',function(V,E) assert(V==nil and E==nil); print('missing-done') end)
local V={Coins=10, Flags={true,false}, Empty={}, Text='é 雪 \\ \"" \t\n', Alias={}}
V.Copy=V.Alias
S:SetAsync('snapshot',V,function(Saved,E)
 assert(Saved==true and E==nil)
 S:GetAsync('snapshot',function(Read,E2)
  assert(E2==nil and Read.Coins==10 and Read.Flags[2]==false and Read.Text=='é 雪 \\ \"" \t\n')
  assert(Read.Alias~=Read.Copy)
  Read.Coins=55
  S:GetAsync('snapshot',function(Again,E3) assert(E3==nil and Again.Coins==10 and Again~=Read); print('snapshot-done') end)
 end)
end)
V.Coins=999; V.Flags[2]=true
");
            F.Until("snapshot-done"); F.Until("missing-done"); F.Refill();
            F.Execute(Store + @"
local Values={ false, true, '', 0, -0.0, 5e-324, 2.2250738585072014e-308, 1.7976931348623157e308, -1.7976931348623157e308 }
local I=0
local function Next()
 I+=1 if I>#Values then print('numbers-done'); return end
 local V=Values[I]
 S:SetAsync('number',V,function(Saved,E)
  assert(Saved==true and E==nil)
  S:GetAsync('number',function(R,E2)
   assert(E2==nil and R==V)
   if type(V)=='number' and V==0 then assert(1/R==1/V) end
   task.delay(0.22,Next)
  end)
 end)
end
Next()
");
            F.Until("numbers-done"); F.Refill();
            F.Execute(Store + @"
local Order={}
S:SetAsync('fifo',false,function(V,E) assert(V==true and E==nil); table.insert(Order,1) end)
S:GetAsync('fifo',function(V,E) assert(V==false and E==nil); table.insert(Order,2) end)
S:RemoveAsync('fifo',function(V,E) assert(V==true and E==nil); table.insert(Order,3) end)
S:GetAsync('fifo',function(V,E) assert(V==nil and E==nil); table.insert(Order,4) end)
S:RemoveAsync('fifo',function(V,E)
 assert(V==false and E==nil); table.insert(Order,5)
 for I=1,5 do assert(Order[I]==I) end
 S:SetAsync('after-remove','ok',function(V2,E2) assert(V2==true and E2==nil); print('fifo-done') end)
end)
");
            F.Until("fifo-done"); F.Refill();
            F.Execute(@"
local D=game:GetService('DataStoreService')
local A,B=D:GetDataStore('Case'),D:GetDataStore('case')
A:SetAsync('User','Upper',function(V,E) assert(V and not E) end)
A:SetAsync('user','Lower',function(V,E) assert(V and not E) end)
B:SetAsync('User','Other',function(V,E)
 assert(V and not E)
 A:GetAsync('User',function(V,E) assert(V=='Upper' and not E) end)
 A:GetAsync('user',function(V,E) assert(V=='Lower' and not E) end)
 B:GetAsync('User',function(V,E) assert(V=='Other' and not E); print('case-done') end)
end)
");
            F.Until("case-done"); F.Refill();
            // Provisional dispatch rejection, including caught cold module errors.
            F.Modules["cold"] = Store + "assert(not pcall(function() S:GetAsync('x',function() end) end)); return function() S:GetAsync('snapshot',function(V,E) assert(not E and V.Coins==10); print('cached-done') end) end";
            F.Modules["nested"] = "return require('cold')";
            F.Source = Store + "for _,M in {'GetAsync','SetAsync','RemoveAsync'} do assert(not pcall(function() if M=='SetAsync' then S[M](S,'x',true,function() end) else S[M](S,'x',function() end) end end)) end; require('nested'); task.defer(function() require('cold')() end)";
            Before = F.Worker.RequestsSent; F.Reload();
            Check(F.Queue.PendingCount == 0 && F.Worker.RequestsSent == Before, "candidate and cold modules enqueue zero");
            F.Until("cached-done"); F.Refill();
            F.Source = "return true"; F.Reload();
            // Bounded independent callback reservations survive an ordinary task flood.
            F.Execute(Store + @"
local State=require('state'); State.Calls=0
for I=1,8 do S:GetAsync('absent',function(V,E) assert(V==nil and E==nil); State.Calls+=1 end) end
assert(not pcall(function() S:GetAsync('ninth',function() error('rejected callback') end) end))
local Flood=0
for I=1,1024 do if not pcall(task.defer,function() end) then break end; Flood+=1 end
assert(Flood==64)
");
            for (int I=0; I<300 && F.Queue.PendingCount!=0; ++I) { F.Tick(); Thread.Sleep(5); }
            Check(F.Queue.PendingCount == 0, "accepted completions survive task saturation");
            F.Execute("assert(require('state').Calls==8)"); F.Refill();
            // Actual commit, then root replacement before owner-thread delivery.
            Before = F.Worker.RequestsSent;
            F.Execute(Store + "S:SetAsync('retired','durable',function() print('FORBIDDEN-OLD-CALLBACK') end)");
            F.Worker.Tick();
            var Retiring = OnlyRequest(F);
            WaitFor(() => F.Worker.RequestsSent == Before + 1 && Volatile.Read(ref Retiring.State) == 2,
                () => { }, "committed response before replacement");
            F.Reload();
            F.Execute(Store + "S:GetAsync('retired',function(V,E) assert(V=='durable' and not E); print('replacement-done') end)");
            F.Until("replacement-done");
            Check(!F.Logs.ToString().Contains("FORBIDDEN-OLD-CALLBACK"), "retired completion suppressed");
            F.Refill();
            ValueBounds(F);
            Publication(F);
            CallbackFailures(F);
            CrossDomain(F);
            var Provider = new object();
            foreach (string Id in new[] {"persist.a","persist.b", new string('a',32) + "." + new string('b',32)}) {
                string Init = "local S=game:GetService('DataStoreService'):GetDataStore('State'); task.defer(function() S:SetAsync('x',addon.Id,function(V,E) assert(V and not E); S:GetAsync('x',function(R,E2) assert(R==addon.Id and not E2); print('isolation-'..addon.Id) end) end) end)";
                var Result = F.Addons.RegisterSource(Provider, Id, "1.0.0", Encoding.UTF8.GetBytes(Init));
                Check(Result[0] == "OK" && F.Addons.ProcessOne(), "addon activation");
            }
            F.Until("isolation-persist.a"); F.Until("isolation-persist.b");
            F.Until("isolation-" + new string('a',32) + "." + new string('b',32));
            Check(F.Addons.UnloadProvider(Provider) == 3, "addon retirement including 65-byte package ID");
            Console.WriteLine("[CarbonLuau:Persistence1B] real compiler/VM + worker validation, snapshot/binary64/FIFO, publication, saturation, replacement and namespaces PASS; elapsed_ms=" + Watch.ElapsedMilliseconds);
        }
        // A new worker/VM host over the same private directory proves restart data,
        // not a reference surviving the old VM. No test database enters packaging.
        using (var F = new Fixture(Native, Worker, Directory)) {
            F.Execute(Store + "S:GetAsync('retired',function(V,E) assert(V=='durable' and not E); print('restart-root') end)");
            F.Until("restart-root");
            var Provider = new object();
            foreach (string Id in new[] {"persist.a","persist.b", new string('a',32) + "." + new string('b',32)}) {
                string Source = "local S=game:GetService('DataStoreService'):GetDataStore('State'); task.defer(function() S:GetAsync('x',function(V,E) assert(V==addon.Id and not E); print('restart-'..addon.Id) end) end)";
                Check(F.Addons.RegisterSource(Provider, Id, "2.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "restart addon");
            }
            F.Until("restart-persist.a"); F.Until("restart-persist.b");
            F.Until("restart-" + new string('a',32) + "." + new string('b',32));
        }
        Console.WriteLine("[CarbonLuau:Persistence1B] worker restart/root and distinct addon namespace retention PASS");
    }
}
