using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    private static void AttachedStorageRequiresAbi15(Runtime.NativeRuntime Native)
    {
        // This runs after real-worker coverage has cached the completion export.
        // Only the managed reported version is lowered; the loaded binary never
        // changes. The guard must also reject when that delegate is already bound.
        uint Original = Native.AbiVersion;
        var Setter = typeof(Runtime.NativeRuntime).GetProperty("AbiVersion").GetSetMethod(true);
        var Roots = (System.Collections.IDictionary)typeof(Runtime.NativeRuntime)
            .GetField("DomainFacadeRoots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Native);
        Check(Original == 0x00010005 && Native.LiveVmCount == 0 && Roots.Count == 0, "ABI guard starts without live domains");
        using (var F = new Fixture(Native, null, null, Initialize: false)) {
            try {
                Setter.Invoke(Native, new object[] { 0x00010004u });
                var Rejected = F.Host.Reload();
                Check(Rejected.Status == Runtime.RuntimeStatus.INTERNAL_ERROR && Rejected.Error.Contains("native ABI 1.5"),
                    "attached storage rejects ABI 1.4 with controlled diagnostic: " + Rejected.Error);
                Check(!F.Host.Ready && F.Host.Generation == 0 && Rejected.Logs == "" && F.Commands.Publications == 0 &&
                    Native.LiveVmCount == 0 && Roots.Count == 0 && F.Queue.PendingCount == 0,
                    "ABI rejection publishes no facade and leaks no VM, domain route or request");
                var Bindings = (System.Collections.ICollection)typeof(Runtime.StorageQueue)
                    .GetField("Bindings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(F.Queue);
                Check(Bindings.Count == 0, "failed ABI candidate releases storage binding");
            } finally { Setter.Invoke(Native, new object[] { Original }); }
            F.Reload();
            Check(F.Host.Ready && F.Commands.Publications == 1 && Native.LiveVmCount == 1 && Roots.Count == 1,
                "restored ABI 1.5 can publish a fresh facade");
            F.Execute("assert(game.ApiVersion=='0.5.0-experimental'); game:GetService('DataStoreService'):GetDataStore('Restored')");
        }
        Check(Native.AbiVersion == Original && Native.Storage == null && Native.LiveVmCount == 0 && Roots.Count == 0,
            "ABI guard fixture restores version and releases all resources");
        Console.WriteLine("[CarbonLuau:Persistence1B] attached-storage ABI 1.4 rejection/restored 1.5 publication PASS; version-property seam only");
    }

    private static byte[] Reply(Runtime.StorageQueue.Request Request, Runtime.StorageQueue.Error Error = Runtime.StorageQueue.Error.None, uint Flags = 0)
    {
        using (var Stream = new MemoryStream()) using (var Writer = new BinaryWriter(Stream)) {
            Writer.Write(Encoding.ASCII.GetBytes("CLPS")); Writer.Write(1u); Writer.Write((uint)Error); Writer.Write(Request.Id);
            Writer.Write(Request.Owner.Host); Writer.Write(Request.Owner.Vm); Writer.Write(Request.Owner.Domain); Writer.Write(Request.Route);
            Writer.Write(Flags); Writer.Write(0u); return Stream.ToArray();
        }
    }
    private static Runtime.StorageQueue.Request TakeRequest(Fixture F)
    {
        var Request = F.Queue.Dispatch();
        Check(Request != null, "public API admitted request reaches transport");
        return Request;
    }
    private static void Finish(Fixture F, Runtime.StorageQueue.Request Request, ulong Now,
        Runtime.StorageQueue.Error Error = Runtime.StorageQueue.Error.None, uint Flags = 0)
    {
        Runtime.StorageQueue.Complete(Request, Reply(Request, Error, Flags), Now);
        Check(F.Queue.Dispatch() == null, "single synthetic request settles");
        F.Tick();
        Check(F.Queue.PendingCount == 0, "callback releases reservation");
    }
    private static int HandedOff(Fixture F)
    {
        int Count = 0;
        foreach (var Request in Reservations(F.Queue)) if (Request != null && Request.HandedOff) ++Count;
        return Count;
    }
    private static void DeterministicAdmission(Runtime.NativeRuntime Native)
    {
        ulong Now = Runtime.StorageProcess.Now;
        using (var F = new Fixture(Native, null, null, () => Now)) {
            F.Queue.Ready = false;
            F.Execute(Store + @"
assert(not pcall(function() S:GetAsync('not-ready',function() error('rejected') end) end))
assert(not pcall(function() S:SetAsync('not-ready',true,function() error('rejected') end) end))
assert(not pcall(function() S:RemoveAsync('not-ready',function() error('rejected') end) end))");
            Check(F.Queue.PendingCount == 0, "service not ready rejects synchronously");
            F.Queue.Ready = true;
            F.Execute(Store + @"
local State=require('state'); State.Calls=0
assert(select('#',S:GetAsync('cutoff',function(...)
 assert(select('#',...)==2)
 local V,E=...; assert(V==nil and E==nil)
 State.Calls+=1; task.defer(function() State.Deferred=true end)
end))==0)
assert(State.Calls==0)");
            var Request = TakeRequest(F);
            Runtime.StorageQueue.Complete(Request, Reply(Request), Now);
            Check(F.Queue.Dispatch() == null, "completed transport not recursive");
            Native.PumpStorage();
            F.Execute("assert(require('state').Calls==0)");
            Check(F.Queue.PendingCount == 1, "native intake retains undelivered reservation");
            // The original execution's 100-ms budget has elapsed. A completion
            // starts a fresh callback budget instead of resuming that execution.
            Thread.Sleep(150);
            F.DrainChecked();
            F.Execute("assert(require('state').Calls==1 and require('state').Deferred==nil)");
            F.DrainChecked();
            F.Execute("assert(require('state').Deferred==true)");
            Check(F.Queue.PendingCount == 0, "completion and newly deferred work have separate cutoffs");
            Exception Affinity = null;
            var OtherThread = new Thread(() => { try { Native.PumpStorage(); } catch (Exception Error) { Affinity = Error; } });
            OtherThread.Start();
            Check(OtherThread.Join(5000) && Affinity is InvalidOperationException, "completion pump rejects non-owner thread");

            // Explicitly synthetic operational replies; validates public callback
            // shape/mapping, not a worker rollback or quota enforcement claim.
            foreach (var Code in new[] {
                Runtime.StorageQueue.Error.QuotaExceeded, Runtime.StorageQueue.Error.StorageUnavailable,
                Runtime.StorageQueue.Error.StorageBusy, Runtime.StorageQueue.Error.StorageFull,
                Runtime.StorageQueue.Error.StorageCorrupt, Runtime.StorageQueue.Error.FormatUnsupported,
                Runtime.StorageQueue.Error.DeadlineExceeded, Runtime.StorageQueue.Error.StorageError,
                Runtime.StorageQueue.Error.Indeterminate }) {
                Now += 2000;
                foreach (string Method in new[] { "GetAsync", "SetAsync", "RemoveAsync" }) {
                    F.Execute(Store + "S:" + Method + "('errors'," + (Method == "SetAsync" ? "true," : "") +
                        "function(...) assert(select('#',...)==2); local V,E=...; assert(V==nil and E=='" + Code + "'); end)");
                    Finish(F, TakeRequest(F), Now, Code);
                }
            }

            Now += 2000;
            F.Execute(Store + "S:SetAsync('expires',true,function(V,E) assert(V==nil and E=='DeadlineExceeded'); print('expired-before-dispatch') end)");
            Now += 5000;
            Check(F.Queue.Dispatch() == null, "expired mutation never handed to worker");
            F.Tick();
            Check(F.Logs.ToString().Contains("expired-before-dispatch") && F.Queue.PendingCount == 0 && F.Host.Recoveries == 0,
                "queue deadline is not VM timeout");

            Now += 2000;
            F.Execute(Store + "S:GetAsync('identity',function(V,E) assert(V==nil and E=='StorageUnavailable'); print('identity-done') end)");
            Request = TakeRequest(F);
            foreach (int Offset in new[] { 0, 4, 12, 20, 28, 36, 44, 52, 56 }) {
                byte[] Invalid = Reply(Request); Invalid[Offset] ^= 0x20;
                bool Rejected = false;
                try { Runtime.StorageQueue.Complete(Request, Invalid, Now); } catch (IOException) { Rejected = true; }
                Check(Rejected && Request.State == 1, "mismatched response cannot complete public callback at offset " + Offset);
            }
            bool Late = false;
            try { Runtime.StorageQueue.Complete(Request, Reply(Request), Request.End); } catch (TimeoutException) { Late = true; }
            Check(Late && Request.State == 1, "late response cannot extend acceptance deadline");
            Runtime.StorageQueue.Fail(Request);
            Check(F.Queue.Dispatch() == null, "failed transport settles");
            F.Tick();
            Check(F.Logs.ToString().Contains("identity-done"), "read transport failure is not absence");

            Now += 2000;
            F.Execute(Store + "S:GetAsync('duplicate',function() require('state').Duplicate=(require('state').Duplicate or 0)+1 end)");
            Request = TakeRequest(F);
            byte[] Completed = Reply(Request);
            Runtime.StorageQueue.Complete(Request, Completed, Now);
            bool Duplicate = false;
            try { Runtime.StorageQueue.Complete(Request, Completed, Now); } catch (IOException) { Duplicate = true; }
            Check(Duplicate, "duplicate reply rejected");
            Check(F.Queue.Dispatch() == null, "duplicate test settles");
            F.Tick(); F.Tick();
            F.Execute("assert(require('state').Duplicate==1)");

            Now += 2000;
            F.Execute(Store + "S:GetAsync('retire-before-dispatch',function() error('stale queued callback') end)");
            F.Reload();
            Check(F.Queue.Dispatch() == null && F.Queue.PendingCount == 0, "retirement cancels queued request before dispatch");
            F.Execute(Store + "S:GetAsync('retire-in-flight',function() error('stale in-flight callback') end)");
            Request = TakeRequest(F);
            F.Reload();
            Finish(F, Request, Now);
            F.Execute(Store + "S:GetAsync('retire-after-intake',function() error('stale handed-off callback') end)");
            Request = TakeRequest(F);
            Runtime.StorageQueue.Complete(Request, Reply(Request), Now);
            Check(F.Queue.Dispatch() == null, "retirement intake settles");
            F.Tick(false);
            Check(F.Queue.PendingCount == 1 && HandedOff(F) == 1, "completed reservation held until delivery or discard");
            F.Reload(); F.Tick();
            Check(F.Queue.PendingCount == 0, "root replacement drops native-ready completion");

            // Public SetAsync -> dispatched mutation -> fatal VM retirement ->
            // terminal reply. Transport is synthetic; CallbackFailures separately
            // proves durable readback with the production worker.
            Now += 2000;
            long RetiringVm = F.Host.VmGenerationId;
            ulong RecoveriesBefore = F.Host.Recoveries;
            long DiscardedBefore = F.Queue.Discarded;
            F.Execute(Store + "S:SetAsync('vm-retired-set','old',function() print('FORBIDDEN-LATE-VM-SET') end)");
            var LateWrite = TakeRequest(F);
            LateWrite.Sent = true; // Same dispatched marker set by the real supervisor.
            Check(LateWrite.Op == Runtime.StorageQueue.Operation.Set && LateWrite.State == 1 && !LateWrite.HandedOff,
                "public SetAsync in flight without a backend result before fatal retirement");
            var Fatal = F.Host.Execute("persistence.inflight-vm-timeout", "while true do end");
            F.Logs.Append(Fatal.Logs);
            Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && F.Host.Ready &&
                F.Host.VmGenerationId != RetiringVm && F.Host.Recoveries == RecoveriesBefore + 1,
                "in-flight public SetAsync survives actual fatal VM retirement and recovery");
            Check(!LateWrite.Owner.Alive && LateWrite.State == 1 && !LateWrite.Released &&
                LateWrite.Frame != null && F.Queue.PendingCount == 1,
                "retired mutation remains transport-owned until its terminal reply");
            F.Execute(Store + @"
local State=require('state'); State.LateVmCalls=0
S:SetAsync('vm-retired-set','new',function(V,E)
 assert(V==true and E==nil); State.LateVmCalls+=1
end)");
            Check(F.Queue.PendingCount == 2, "recovered generation accepts one successor without replaying old write");
            for (int Turn = 0; Turn < 3; ++Turn) {
                Check(F.Queue.Dispatch() == null, "recovered public mutation fenced behind old in-flight write");
                F.Tick();
            }
            F.Execute("assert(require('state').LateVmCalls==0)");
            Check(LateWrite.State == 1 && !LateWrite.Released && F.Queue.PendingCount == 2,
                "owner turns neither abandon nor settle the retired write prematurely");
            Runtime.StorageQueue.Complete(LateWrite, Reply(LateWrite, Runtime.StorageQueue.Error.None, 3), Now);
            var Successor = TakeRequest(F);
            Check(Successor.Id == LateWrite.Id + 1 && Successor.Op == Runtime.StorageQueue.Operation.Set &&
                Successor.Owner.Alive && Successor.Owner.Vm == (ulong)F.Host.VmGenerationId &&
                Successor.Owner.Namespace == LateWrite.Owner.Namespace,
                "only the explicit new-generation successor dispatches after the old terminal reply");
            F.Tick();
            Check(LateWrite.Released && !LateWrite.HandedOff && F.Queue.Discarded == DiscardedBefore + 1 &&
                F.Queue.PendingCount == 1 && !F.Logs.ToString().Contains("FORBIDDEN-LATE-VM-SET"),
                "late public SetAsync completion is discarded after VM retirement and its reservation released");
            Successor.Sent = true;
            Finish(F, Successor, Now, Runtime.StorageQueue.Error.None, 3);
            F.Tick();
            F.Execute("assert(require('state').LateVmCalls==1)");
            Check(Successor.Released && F.Queue.PendingCount == 0 && F.Queue.Dispatch() == null &&
                !F.Logs.ToString().Contains("FORBIDDEN-LATE-VM-SET"),
                "late VM completion never replays; successor delivered once; all reservations settled");
            Console.WriteLine("[CarbonLuau:Persistence1B] public SetAsync in flight across fatal VM retirement: synthetic late reply, successor fencing, discard/no replay and reservation release PASS");

            // A fixed clock proves rates cannot be reset by completion, failure,
            // or domain replacement. These calls still traverse the actual VM.
            Now += 2000;
            for (int Index = 0; Index < 32; ++Index) {
                F.Execute(Store + "S:GetAsync('rate',function() end)");
                Finish(F, TakeRequest(F), Now);
            }
            F.Reload();
            F.Execute(Store + "assert(not pcall(function() S:GetAsync('rate',function() error('rate bypass') end) end))");
            Check(F.Queue.PendingCount == 0 && F.Queue.RateRejected == 1, "request burst survives replacement");
            Now += 50;
            F.Execute(Store + "S:GetAsync('refilled',function() end)");
            Finish(F, TakeRequest(F), Now);
            Now += 2000;
            for (int Index = 0; Index < 8; ++Index) {
                F.Execute(Store + "S:RemoveAsync('mutation-rate',function(V,E) assert(V==false and E==nil) end)");
                Finish(F, TakeRequest(F), Now);
            }
            F.Reload();
            F.Execute(Store + "assert(not pcall(function() S:RemoveAsync('mutation-rate',function() error('mutation rate bypass') end) end))");
            Check(F.Queue.PendingCount == 0 && F.Queue.RateRejected == 2, "mutation burst survives replacement");
            Now += 200;
            F.Execute(Store + "S:RemoveAsync('refilled',function() end)");
            Finish(F, TakeRequest(F), Now);

            // Native acquired-name accounting is separate from durable stores.
            F.Reload();
            F.Execute(@"
local D=game:GetService('DataStoreService')
for I=1,64 do D:GetDataStore('N'..I) end
for I=1,64 do D:GetDataStore('N'..I) end
assert(not pcall(function() D:GetDataStore('N65') end))");
            Check(F.Queue.PendingCount == 0, "disk-free 64-name cap does not submit work");
        }
        Console.WriteLine("[CarbonLuau:Persistence1B] synthetic transport + real VM: errors, cutoff, deadlines, response identity, duplicate/stale suppression, replacement rates, names PASS");
    }

    private static void GlobalCapacity(Runtime.NativeRuntime Native)
    {
        ulong Now = Runtime.StorageProcess.Now;
        using (var F = new Fixture(Native, null, null, () => Now)) {
            object Provider = new object();
            for (int Index = 0; Index < 16; ++Index) {
                string Source = @"
local S=game:GetService('DataStoreService'):GetDataStore('Capacity'); local Calls=0
task.defer(function()
 for I=1,8 do S:GetAsync('K'..I,function(V,E)
  assert(V==nil and E==nil); Calls+=1
  if Calls==8 then print('capacity-'..addon.Id) end
 end) end
 assert(not pcall(function() S:GetAsync('ninth',function() error('namespace overflow') end) end))
end)";
                Check(F.Addons.RegisterSource(Provider, "capacity" + Index, "1.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "capacity addon activation");
            }
            WaitFor(() => F.Queue.PendingCount == 128, F.DrainChecked, "16 namespaces each reserve eight completions");
            F.Execute(Store + "assert(not pcall(function() S:GetAsync('global129',function() error('global overflow') end) end))");
            Check(F.Queue.PendingCount == 128, "global 129th public request rejected");
            var FirstRound = new List<string>();
            for (int Index = 0; Index < 128; ++Index) {
                var Request = TakeRequest(F);
                if (Index < 16) FirstRound.Add(Request.Owner.Package);
                Check(Request.Owner.Package == FirstRound[Index % 16], "fair namespace rotation before second request");
                Runtime.StorageQueue.Complete(Request, Reply(Request), Now);
            }
            Check(new HashSet<string>(FirstRound).Count == 16 && F.Queue.Dispatch() == null && F.Queue.PendingCount == 128,
                "all completed-undelivered requests retain reservations");
            F.Queue.BeginTick();
            Native.PumpStorage(); Native.PumpStorage();
            Check(HandedOff(F) == 8 && F.Queue.PendingCount == 128, "eight-completion owner-tick intake bound includes repeated pumps");
            F.DrainChecked();
            Check(F.Queue.PendingCount == 120, "first eight delivered slots released");
            WaitFor(() => F.Queue.PendingCount == 0, () => F.Tick(), "global reservations drain");
            for (int Index = 0; Index < 16; ++Index)
                Check(F.Logs.ToString().Contains("capacity-capacity" + Index + "\n"), "all namespace callbacks delivered");
            // Native may reject at its reserved callback ledger before reaching
            // the managed queue; no managed rejection-counter increment is owed.
            Check(F.Queue.Completed[0] == 128, "bounded global completion total");
        }
        Console.WriteLine("[CarbonLuau:Persistence1B] synthetic transport + real VM: namespace 8/global 128, retained reservations, fair dispatch and eight/tick intake PASS");
    }

    private static void WorkerFailure(Runtime.NativeRuntime Native, string Worker, string Directory, bool Committed)
    {
        using (var F = new Fixture(Native, Worker, Directory)) {
            F.Execute(Store + @"
local State=require('state'); State.Calls=0
S:SetAsync('fault','durable-if-committed',function(V,E)
 assert(V==nil and E=='Indeterminate'); State.Calls+=1; print('uncertain-done')
end)");
            F.Until("uncertain-done", 15000);
            WaitFor(() => F.Worker.WorkerStarts == 2 && F.Queue.Ready, () => F.Tick(), "fault restart ready", 35000);
            Check(F.Worker.RequestsSent == 1 && F.Queue.PendingCount == 0 && F.Host.Recoveries == 0, "no automatic mutation replay and no VM retirement");
            F.Execute("assert(require('state').Calls==1)");
            F.Execute(Store + "S:GetAsync('fault',function(V,E) assert(E==nil and " +
                (Committed ? "V=='durable-if-committed'" : "V==nil") + "); print('fault-read-done') end)");
            F.Until("fault-read-done");
            Check(F.Worker.RequestsSent == 2, "only explicit verification read after fault");
            // A second fault consumes no additional restart allowance.
            F.Execute(Store + "S:SetAsync('second-fault','value',function(V,E) assert(V==nil and E=='Indeterminate'); print('second-fault-done') end)");
            F.Until("second-fault-done", 15000);
            WaitFor(() => F.Worker.IsFinished, () => F.Tick(), "second failure stops supervisor");
            F.Tick();
            Check(!F.Queue.Ready && F.Worker.WorkerStarts == 2 && F.Worker.RequestsSent == 3 && F.Queue.PendingCount == 0, "one restart allowance, never replay");
            F.Execute(Store + "assert(not pcall(function() S:GetAsync('disabled',function() error('disabled callback') end) end))");
        }
        Console.WriteLine("[CarbonLuau:Persistence1B] existing " + (Committed ? "lost committed acknowledgement" : "hung worker deadline/reap") +
            " fixture + real public callbacks PASS; Indeterminate/no replay/one restart");
    }
}
