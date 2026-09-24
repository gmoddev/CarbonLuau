using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    internal static void RunClosure(Runtime.NativeRuntime Native, string Worker, string Root)
    {
        Check(Native.AbiVersion == 0x00010005, "closure requires ABI 1.5");
        ReplacementCompletionMatrix(Native);
        DependencyCompletionMatrix(Native);
        using (var F = new Fixture(Native, Worker, Path.Combine(Root, "dispatch"))) FairWorkerDispatch(F);
        using (var F = new Fixture(Native, Worker, Path.Combine(Root, "examples"))) PublicExamples(F);
        using (var F = new Fixture(Native, Worker, Path.Combine(Root, "combined"))) CombinedStress(F);
        using (var F = new Fixture(Native, Worker, Path.Combine(Root, "byte-quota"))) PublicByteQuota(F);
        string Durable = Path.Combine(Root, "lifecycle");
        for (int Cycle = 0; Cycle < 12; ++Cycle) {
            using (var F = new Fixture(Native, Worker, Durable)) ClosureLifecycle(F, Cycle);
            Check(Native.LiveVmCount == 0 && Native.Storage == null, "host cycle retires all native authority");
        }
        Console.WriteLine("[CarbonLuau:Persistence1C] PASS combined real-worker stress and 12 full host/worker reopen cycles; not live Carbon or power-loss evidence");
    }

    private static void PublicExamples(Fixture F)
    {
        Console.WriteLine("[CarbonLuau:Persistence1C] worker_start_to_ready_observed_ms=" +
            F.WorkerReadyObservedMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
            "; includes host construction and 5ms readiness polling; observation only");
        string Root = Path.Combine(Environment.CurrentDirectory, "examples", "persistence");
        foreach (var Example in new[] {
            new[] { "get", "[Persistence:Get] Key is absent" },
            new[] { "set", "[Persistence:Set] Durable save completed" },
            new[] { "get", "[Persistence:Get] Read completed" },
            new[] { "remove", "[Persistence:Remove] Key removed" },
            new[] { "remove", "[Persistence:Remove] Key was absent" },
            new[] { "snapshot", "[Persistence:Snapshot] Fresh stored theme\tBlue" },
            new[] { "errors", "[Persistence:Errors] Durable save completed" }
        }) {
            F.Source = File.ReadAllText(Path.Combine(Root, Example[0], "init.luau"));
            F.Reload(); F.Until(Example[1]);
        }
        Check(F.Logs.ToString().Contains("[Persistence:Errors] Rejected before acceptance") &&
            !F.Logs.ToString().Contains("Unexpected callback"), "example rejected submission has no callback");
        F.Source = File.ReadAllText(Path.Combine(Root, "player-key", "init.luau"));
        F.Reload(); F.Tick();
        Check(F.Queue.PendingCount == 0 && F.Worker.RequestsSent == 9,
            "six unchanged example files execute; player listener setup is disk-free");
        Console.WriteLine("[CarbonLuau:Persistence1C] six public example files through pinned VM PASS; real storage callbacks, player listener initialization only (no client event)");
    }

    private static void DependencyCompletionMatrix(Runtime.NativeRuntime Native)
    {
        foreach (bool Required in new[] { true, false })
        foreach (string Method in new[] { "GetAsync", "SetAsync", "RemoveAsync" }) {
            ulong Now = Runtime.StorageProcess.Now;
            using (var F = new Fixture(Native, null, null, () => Now)) {
                object Provider = new object(), Consumer = new object();
                Check(F.Addons.RegisterSource(Provider, "closure.dependency", "1.0.0", Encoding.UTF8.GetBytes("return true"))[0] == "OK" &&
                    F.Addons.ProcessOne(), "dependency active");
                string Init = Store + "task.defer(function() S:" + Method + "('pending'," +
                    (Method == "SetAsync" ? "true," : "") + "function() print('dependency-completed') end) end)";
                string Edges = Required ? "{\"required\":[\"closure.dependency\"],\"optional\":[]}" :
                    "{\"required\":[],\"optional\":[\"closure.dependency\"]}";
                Check(F.Addons.RegisterArchive(Consumer, Package("closure.consumer", Init, null, Edges))[0] == "OK" &&
                    F.Addons.ProcessOne(), "dependency consumer active");
                F.DrainChecked();
                var Request = TakeRequest(F);
                Request.Sent = true;
                uint Flags = Method == "SetAsync" ? 3u : 0u;
                Check(F.Addons.UnloadProvider(Provider) == 1 && Request.Owner.Alive != Required,
                    "required loss retires consumer while optional loss preserves it");
                Finish(F, Request, Now, Flags: Flags);
                Check(F.Logs.ToString().Contains("dependency-completed") != Required,
                    "dependency loss callback follows exact consumer lifetime");
                Provider = new object();
                Check(F.Addons.RegisterSource(Provider, "closure.dependency", "2.0.0", Encoding.UTF8.GetBytes("return true"))[0] == "OK" &&
                    F.Addons.ProcessOne(), "fresh provider restores dependency");
                if (Required) {
                    Check(F.Addons.ProcessOne(), "required consumer reconstruction queued");
                    F.DrainChecked();
                    var Restored = TakeRequest(F);
                    Check(Restored.Owner.Domain != Request.Owner.Domain && Restored.Owner.Namespace == Request.Owner.Namespace,
                        "restoration keeps durable namespace with fresh authority");
                    Finish(F, Restored, Now, Flags: Flags);
                    Check(F.Logs.ToString().Contains("dependency-completed"), "fresh consumer callback delivers");
                } else Check(!F.Addons.ProcessOne() && F.Queue.PendingCount == 0,
                    "optional restoration does not reinitialize consumer or replay request");
                Check(F.Addons.UnloadProvider(Consumer) == 1 && F.Addons.UnloadProvider(Provider) == 1,
                    "dependency fixture cleanup");
            }
        }
        Console.WriteLine("[CarbonLuau:Persistence1C] pending Get/Set/Remove with required/optional dependency loss, provider reload and reconstruction PASS cases=6; synthetic transport, real VM");
    }

    private static void ReplacementCompletionMatrix(Runtime.NativeRuntime Native)
    {
        // Real public bindings/domains; synthetic terminal transport deliberately
        // controls all three race boundaries. This is not durability evidence.
        foreach (string Method in new[] { "GetAsync", "SetAsync", "RemoveAsync" })
        for (int Boundary = 0; Boundary < 3; ++Boundary) {
            ulong Now = Runtime.StorageProcess.Now;
            using (var F = new Fixture(Native, null, null, () => Now)) {
                object Provider = new object();
                string Init = Store + "task.defer(function() S:" + Method + "('retiring'," +
                    (Method == "SetAsync" ? "true," : "") +
                    "function() print('FORBIDDEN-REPLACEMENT-CALLBACK') end) end)";
                var Registered = F.Addons.RegisterSource(Provider, "closure.race", "1.0.0", Encoding.UTF8.GetBytes(Init));
                Check(Registered[0] == "OK" && F.Addons.ProcessOne(), "race addon activation");
                F.DrainChecked();
                var Request = TakeRequest(F);
                Request.Sent = true;
                uint Flags = Method == "SetAsync" ? 3u : 0u;
                long Discarded = F.Queue.Discarded;
                Check(F.Addons.ReplaceSource(Provider, Registered[1], "2.0.0", Encoding.UTF8.GetBytes("error('failed candidate')"))[0] == "OK" &&
                    F.Addons.ProcessOne() && Request.Owner.Alive, "failed candidate preserves pending authority");
                if (Boundary > 0) {
                    Runtime.StorageQueue.Complete(Request, Reply(Request, Flags: Flags), Now);
                    Check(F.Queue.Dispatch() == null, "terminal result settles");
                    if (Boundary == 2) {
                        F.Tick(false);
                        Check(HandedOff(F) == 1, "native completion awaits admission");
                    }
                }
                Check(F.Addons.ReplaceSource(Provider, Registered[1], "2.0.0", Encoding.UTF8.GetBytes("return true"))[0] == "OK" &&
                    F.Addons.ProcessOne() && !Request.Owner.Alive, "replacement retires exact callback authority");
                if (Boundary == 0) Runtime.StorageQueue.Complete(Request, Reply(Request, Flags: Flags), Now);
                Check(F.Queue.Dispatch() == null, "replacement does not replay request");
                F.Tick(); F.Tick();
                Check(F.Queue.PendingCount == 0 && F.Queue.Discarded == Discarded + 1 &&
                    !F.Logs.ToString().Contains("FORBIDDEN-REPLACEMENT-CALLBACK"), "old callback never enters replacement");
                Check(Request.Frame == null && Request.Envelope == null, "terminal request releases encoded snapshots");
                Check(F.Addons.UnloadProvider(Provider) == 1, "race replacement cleanup");
            }
        }
        Console.WriteLine("[CarbonLuau:Persistence1C] Get/Set/Remove addon replacement at in-flight/terminal/native-ready boundaries PASS cases=9; synthetic transport, real VM");
    }

    private static void FairWorkerDispatch(Fixture F)
    {
        object Provider = new object();
        F.Execute(Store + @"
S:GetAsync('first',function(V,E) assert(V==nil and E==nil); print('root-first') end)
S:GetAsync('second',function(V,E) assert(V==nil and E==nil,'fair root second '..tostring(E)); print('root-second') end)");
        string Init = Store + @"task.defer(function()
S:GetAsync('third',function(V,E) assert(V==nil and E==nil); print('addon-third') end)
end)";
        Check(F.Addons.RegisterSource(Provider, "closure.fair", "1.0.0", Encoding.UTF8.GetBytes(Init))[0] == "OK" &&
            F.Addons.ProcessOne(), "fair worker addon activation");
        F.DrainChecked();
        Check(F.Queue.PendingCount == 3, "two root submissions precede one addon submission");
        try { F.Until("root-second"); }
        catch { Console.Error.WriteLine("[CarbonLuau:Persistence1C] fair dispatch failure " + F.Status); throw; }
        string Output = F.Logs.ToString();
        Check(Output.IndexOf("root-first", StringComparison.Ordinal) < Output.IndexOf("addon-third", StringComparison.Ordinal) &&
            Output.IndexOf("addon-third", StringComparison.Ordinal) < Output.IndexOf("root-second", StringComparison.Ordinal),
            "namespace fairness preserves root FIFO while changing global submission order");
        Check(F.Worker.WorkerStarts == 1 && F.Worker.RequestsSent == 3 && F.Queue.BackendFailures == 0 &&
            F.Queue.PendingCount == 0, "fair dispatch never restarts worker or replays requests");
        Check(F.Addons.UnloadProvider(Provider) == 1, "fair addon retires");
        Console.WriteLine("[CarbonLuau:Persistence1C] real-worker dispatch order root1/addon3/root2 PASS; no restart/replay");
    }

    private static void PublicByteQuota(Fixture F)
    {
        var Watch = Stopwatch.StartNew();
        // Exact public production quota, not a parameterized backend. The 1A
        // envelope for four strings is 69 bytes plus their UTF-8 payload. A
        // one-byte store and four-byte key add five logical charge bytes.
        // 255 * (65536 + 5) + (64256 + 5) = 16 MiB.
        F.Execute(@"
local S=game:GetService('DataStoreService'):GetDataStore('Q')
local Full={string.rep('a',16384),string.rep('b',16384),string.rep('c',16384),string.rep('d',16315)}
local Last={Full[1],Full[2],Full[3],string.rep('e',15035)}
local Index=0
local function AfterFill()
 local Rejected=0
 local function CheckRejected(V,E)
  assert(V==nil and E=='QuotaExceeded'); Rejected+=1
  if Rejected~=2 then return end
  task.delay(0.5,function()
   S:GetAsync('0001',function(V1,E1)
    assert(E1==nil and #V1[4]==16315)
    S:GetAsync('0256',function(V2,E2)
     assert(E2==nil and #V2[4]==15035)
     S:SetAsync('0256',false,function(V3,E3)
      assert(V3==true and E3==nil)
      task.delay(0.25,function()
       S:SetAsync('0256',Last,function(V4,E4)
        assert(V4==true and E4==nil)
        task.delay(0.25,function()
         S:RemoveAsync('0001',function(V5,E5)
          assert(V5==true and E5==nil)
          task.delay(0.25,function()
           S:SetAsync('new1',Full,function(V6,E6)
            assert(V6==true and E6==nil); print('[Closure:Quota] exact namespace restored')
           end)
          end)
         end)
        end)
       end)
      end)
     end)
    end)
   end)
  end)
 end
 -- Both accepted requests encounter the same full namespace transactionally.
 S:SetAsync('0256',{Last[1],Last[2],Last[3],Last[4]..'x'},CheckRejected)
 S:SetAsync('new1',false,CheckRejected)
end
local function Fill()
 Index+=1
 S:SetAsync(string.format('%04d',Index),Index==256 and Last or Full,function(V,E)
  assert(V==true and E==nil)
  task.delay(0.25,Index==256 and AfterFill or Fill)
 end)
end
Fill()");
        F.Until("[Closure:Quota] exact namespace restored", 150000);
        Check(F.Queue.Completed[1] == 259 && F.Queue.Completed[0] == 2 && F.Queue.Completed[2] == 1 &&
            F.Queue.QuotaRejected == 2 && F.Queue.RateRejected == 0 && F.Queue.PendingCount == 0 &&
            F.Worker.RequestsSent == 264 && F.Worker.WorkerStarts == 1,
            "exact public quota rejection/overwrite/remove accounting: " + F.Status);
        Console.WriteLine("[CarbonLuau:Persistence1C] public 16-MiB exact/+1/concurrent rejection, shrink/grow/remove/refill PASS; " +
            "accepted=264 quota_rejected=2 elapsed_ms=" + Watch.ElapsedMilliseconds);
    }

    private static void CombinedStress(Fixture F)
    {
        const int Domains = 11, Cycles = 200;
        var Watch = Stopwatch.StartNew();
        object Provider = new object();
        // One real root plus ten actual AddonRegistry domains. The same two
        // stores/key names intentionally collide logically across namespaces.
        // Each stream stays below 5 mutations/s and 20 requests/s. No fake clock,
        // backend bypass, hidden retry or synthetic completion is used here.
        string Source = @"
local D=game:GetService('DataStoreService')
local Stores={D:GetDataStore('State'),D:GetDataStore('state')}
local Identity=__IDENTITY__
local Sequence=0
local function Next()
 Sequence+=1
 local Current=Sequence
 local S=Stores[1+Current%2]
 local Snapshot={Owner=Identity,Sequence=Current,Payload=string.rep('x',256)}
 local SetCalled,GetCalled,RemoveCalled,AbsentCalled=false,false,false,false
 S:SetAsync('x',Snapshot,function(Saved,E)
  assert(not SetCalled and Saved==true and E==nil); SetCalled=true
  S:GetAsync('x',function(Value,E2)
   assert(not GetCalled,'duplicate Get callback')
   assert(E2==nil,'Get error '..tostring(E2))
   assert(type(Value)=='table','missing snapshot '..Identity..' '..Current)
   assert(Value.Owner==Identity,'owner mismatch expected '..Identity..' got '..tostring(Value.Owner))
   assert(Value.Sequence==Current,'sequence mismatch '..Identity..' expected '..Current..' got '..tostring(Value.Sequence))
   assert(Value.Payload==string.rep('x',256)); GetCalled=true; Value.Owner='not persisted'
   S:RemoveAsync('x',function(Removed,E3)
    assert(not RemoveCalled and Removed==true and E3==nil); RemoveCalled=true
    S:GetAsync('x',function(Missing,E4)
     assert(not AbsentCalled and Missing==nil and E4==nil); AbsentCalled=true
     if Current==200 then print('[Closure:Done]',Identity)
     else task.delay(0.5,Next) end
    end)
   end)
  end)
 end)
 Snapshot.Owner='mutated after submission'
end
task.defer(Next)";
        F.Execute(Source.Replace("__IDENTITY__", "'root'"));
        for (int Index = 0; Index < Domains - 1; ++Index) {
            string Id = "closure" + Index;
            Check(F.Addons.RegisterSource(Provider, Id, "1.0.0",
                Encoding.UTF8.GetBytes(Source.Replace("__IDENTITY__", "addon.Id")))[0] == "OK" &&
                F.Addons.ProcessOne(), "combined stress addon activation");
        }
        int PeakPending = 0;
        try { WaitFor(() => F.Queue.Completed[0] == Domains * Cycles * 2 && F.Queue.PendingCount == 0,
            () => { F.Tick(); PeakPending = Math.Max(PeakPending, F.Queue.PendingCount); },
            "combined real-worker streams finish", 240000); }
        catch { Console.Error.WriteLine("[CarbonLuau:Persistence1C] combined failure " + F.Status); throw; }
        Check(F.Queue.Completed[1] == Domains * Cycles && F.Queue.Completed[2] == Domains * Cycles &&
            F.Worker.RequestsSent == Domains * Cycles * 4 && F.Queue.RateRejected == 0 &&
            F.Queue.QueueRejected == 0 && F.Queue.QuotaRejected == 0,
            "exact accepted durable operations, no replay/rejection: " + F.Status);
        Check(F.Logs.ToString().Contains("[Closure:Done]\troot\n"), "root stream progressed");
        for (int Index = 0; Index < Domains - 1; ++Index)
            Check(F.Logs.ToString().Contains("[Closure:Done]\tclosure" + Index + "\n"), "each addon completed independently");
        Check(F.Addons.UnloadProvider(Provider) == Domains - 1 && F.Queue.PendingCount == 0,
            "combined provider teardown releases pending resources");
        Console.WriteLine("[CarbonLuau:Persistence1C] mixed real-worker stress PASS; root=1 addons=10 stores_per_domain=2 " +
            "set=2200 get=4400 remove=2200 duplicate=0 replay=0 peak_pending=" + PeakPending +
            " elapsed_ms=" + Watch.ElapsedMilliseconds);
    }

    private static void ClosureLifecycle(Fixture F, int Cycle)
    {
        var Watch = Stopwatch.StartNew();
        string Marker = "closure-cycle-" + Cycle;
        if (Cycle > 0) {
            F.Execute(Store + "S:GetAsync('retained',function(V,E) assert(E==nil and V==" + (Cycle - 1) +
                "); print('" + Marker + "-reopen') end)");
            F.Until(Marker + "-reopen");
        }
        F.Execute(Store + "S:SetAsync('retained'," + Cycle + ",function() error('retired root callback entered') end)");
        WaitFor(() => HandedOff(F) == 1, () => F.Tick(false), "durable result queued before root replacement");
        long Before = F.Worker.RequestsSent;
        long Generation = F.Host.Generation;
        var Failed = F.Host.Reload(Store + "S:RemoveAsync('retained',function() end)");
        Check(Failed.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && F.Host.Generation == Generation &&
            F.Worker.RequestsSent == Before && F.Queue.PendingCount == 1,
            "failed provisional candidate preserves old queued completion and sends no request");
        F.Reload();
        Check(F.Queue.PendingCount == 0, "root replacement discards ready callback");
        F.Execute(Store + "S:GetAsync('retained',function(V,E) assert(E==nil and V==" + Cycle +
            "); print('" + Marker + "-root') end)");
        F.Until(Marker + "-root");

        object Provider = new object();
        string Seed = Store + "task.defer(function() S:SetAsync('retained'," + (1000 + Cycle) +
            ",function() error('retired addon callback entered') end) end)";
        string[] Registered = F.Addons.RegisterSource(Provider, "closure.lifecycle", "1.0.0", Encoding.UTF8.GetBytes(Seed));
        Check(Registered[0] == "OK" && F.Addons.ProcessOne(), "lifecycle writer addon activation");
        F.DrainChecked();
        WaitFor(() => HandedOff(F) == 1, () => F.Tick(false), "addon write result queued before version replacement");
        Before = F.Worker.RequestsSent;
        Check(F.Addons.ReplaceSource(Provider, Registered[1], "2.0.0", Encoding.UTF8.GetBytes(
            Store + "S:RemoveAsync('retained',function() end)"))[0] == "OK" && F.Addons.ProcessOne(), "failed addon candidate evaluated");
        Check(F.Worker.RequestsSent == Before && F.Queue.PendingCount == 1, "failed addon candidate cannot submit");
        string Read = Store + "task.defer(function() S:GetAsync('retained',function(V,E) assert(E==nil and V==" +
            (1000 + Cycle) + "); print('" + Marker + "-addon') end) end)";
        Check(F.Addons.ReplaceSource(Provider, Registered[1], "2.0.0", Encoding.UTF8.GetBytes(Read))[0] == "OK" &&
            F.Addons.ProcessOne(), "successful version replacement");
        F.Until(Marker + "-addon");
        Check(F.Addons.UnloadProvider(Provider) == 1, "provider unload");

        // A real Luau deadline retires the VM while a real worker result is
        // already durable but not admitted. Reconstruction must not replay it.
        F.Refill();
        F.Execute(Store + "S:SetAsync('fatal'," + Cycle + ",function() error('old VM callback entered') end)");
        WaitFor(() => HandedOff(F) == 1, () => F.Tick(false), "durable result awaits admission before fatal VM retirement");
        Before = F.Worker.RequestsSent;
        long Vm = F.Host.VmGenerationId;
        var Fatal = F.Host.Execute("closure.timeout", "while true do end");
        Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && F.Host.Ready && F.Host.VmGenerationId != Vm &&
            F.Queue.PendingCount == 0 && F.Worker.RequestsSent == Before, "fatal recovery drops callback without replay");
        F.Execute(Store + "S:GetAsync('fatal',function(V,E) assert(E==nil and V==" + Cycle +
            "); print('" + Marker + "-fatal') end)");
        F.Until(Marker + "-fatal");
        Check(F.Queue.PendingCount == 0, "lifecycle returns to idle");
        Console.WriteLine("[CarbonLuau:Persistence1C] root/addon failed/successful replacement, provider retirement, " +
            "durable-before-fatal recovery/reopen PASS cycle=" + Cycle + " requests=" + F.Worker.RequestsSent +
            " elapsed_ms=" + Watch.ElapsedMilliseconds);
    }
}
