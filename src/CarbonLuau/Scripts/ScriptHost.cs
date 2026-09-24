using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class ScriptHost : IDisposable
        {
            private readonly NativeRuntime Native;
            private readonly RuntimeConfig Settings;
            private readonly Func<ScriptSnapshot> ReadSnapshot;
            private readonly FacadeWorld Facade;
            private RuntimeGeneration Vm;
            private RuntimeDomain Current;
            private bool Disposed, Draining, RecoveryAvailable;
            public bool Busy { get; private set; }
            private bool StopRequested;
            public void RequestStop() { StopRequested = true; }
            private long NextVmGeneration;
            private string Entry = "", Reason = "not initialized", LastReload = "none";
            public ulong Attempted, Completed, Failed, Cancelled, Invalidated, Rejected, BudgetOverruns, Recoveries, Timeouts;
            public long Generation { get { return Current == null ? 0 : Current.DomainLifetimeId; } }
            public long VmGenerationId { get { return Current == null ? 0 : Current.VmGenerationId; } }
            public bool Ready { get { return !Disposed && !StopRequested && Vm != null && Current != null && Current.Alive && Vm.Info.Ready != 0; } }
            private bool StorageWork { get { return Native.Storage!=null && Native.Storage.HasCompletions; } }
            private bool NeedsRecovery { get { return !Disposed && !StopRequested && Vm!=null && Current!=null && Vm.Alive && Vm.Info.Ready==0; } }
            public bool HasWork { get { return NeedsRecovery || (Ready && (StorageWork || Vm.Scheduler.Queued != 0 || (Facade != null && Facade.HasWork))); } }
            public bool HasReadyWork {
                get {
                    if (NeedsRecovery) return true;
                    if (!Ready) return false;
                    if (StorageWork) return true;
                    if (Facade != null && Facade.HasWork) return true;
                    SchedulerInfo Info = Vm.Scheduler;
                    return Info.Queued != 0 && Info.NextDueNs <= Info.NowNs;
                }
            }
            internal bool TryGetNextDue(out ulong DueNs, out double DelaySeconds)
            {
                DueNs = 0; DelaySeconds = 0;
                if (!Ready || StorageWork || (Facade != null && Facade.HasWork)) return false;
                SchedulerInfo Info = Vm.Scheduler;
                if (Info.Queued == 0 || Info.NextDueNs == 0 || Info.NextDueNs <= Info.NowNs) return false;
                DueNs = Info.NextDueNs;
                DelaySeconds = (Info.NextDueNs - Info.NowNs) / 1000000000.0;
                return true;
            }
            internal ulong VmMemoryBytes { get { return Ready ? Vm.Info.MemoryBytes : 0; } }
            internal ulong VmMemoryLimitBytes { get { return Ready ? Vm.Info.MemoryLimitBytes : 0; } }
            internal SchedulerInfo SchedulerSnapshot { get { return Ready ? Vm.Scheduler : new SchedulerInfo(); } }
            public ScriptHost(NativeRuntime Native, RuntimeConfig Config, Func<ScriptSnapshot> ReadSnapshot, FacadeWorld Facade = null)
            { this.Native = Native; Settings = Config.Validate(); this.ReadSnapshot = ReadSnapshot; this.Facade = Facade; }
            private void ReleaseDomain(bool Replaced, SchedulerInfo? Snapshot = null)
            {
                RuntimeDomain Old = Current; Current = null;
                if (Old == null) return;
                if (Old.FacadeSession != null) {
                    Cancelled += (ulong)Old.FacadeSession.PendingCount;
                    if (Replaced) Invalidated += (ulong)Old.FacadeSession.PendingCount;
                    Rejected += Old.FacadeSession.Rejected;
                    Facade.Retire(Old.FacadeSession);
                }
                var Info = Snapshot ?? (Vm != null && Vm.Alive ? Vm.Scheduler : new SchedulerInfo());
                Cancelled += Info.Queued + Info.Discarded;
                if (Replaced) Invalidated += Info.Queued + Info.Discarded;
                Rejected += Info.Rejected;
                Old.Dispose();
            }
            private void ReleaseVm(bool Replaced)
            {
                SchedulerInfo Info = Vm != null && Vm.Alive ? Vm.Scheduler : new SchedulerInfo();
                ReleaseAddonDomains();
                ReleaseDomain(Replaced, Info);
                RuntimeGeneration Old = Vm; Vm = null;
                if (Old != null) Old.Dispose();
            }
            public ExecutionResult Reload() { ExecutionResult Result = Replace(true, null); if (Result.Status != RuntimeStatus.OK && Current == null) ReleaseVm(false); return Result; }
            // Only used by isolated Phase 1 regression fixtures, never exposed to scripts.
            public ExecutionResult Reload(string Source) { ExecutionResult Result = Replace(true, Source); if (Result.Status != RuntimeStatus.OK && Current == null) ReleaseVm(false); return Result; }
            private ExecutionResult Replace(bool Operator, string Override)
            {
                Native.CheckOwner();
                if (Disposed || StopRequested || !Settings.Enabled || Busy) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT, Error = "disabled/unloaded/busy" };
                RuntimeDomain Candidate = null;
                Busy = true;
                try {
                    ScriptSnapshot Snapshot = ReadSnapshot();
                    if (Vm == null) Vm = new RuntimeGeneration(Native, checked(++NextVmGeneration), Settings);
                    Candidate = new RuntimeDomain(Native, Vm, Settings, Snapshot);
                    if (Facade != null) Candidate.Facade(new FacadeSession(Facade, Candidate.VmGenerationId,
                        Candidate.DomainLifetimeId, Settings.MaxQueuedCallbacks));
                    // Chunk identity is logical, not an absolute filesystem path.
                    ExecutionResult Result = Candidate.Execute("entry." + Snapshot.EntryName.Replace('/', '.'), Override ?? Snapshot.EntrySource, Settings.MaxCallbackMilliseconds);
                    Result.Generation = Candidate.DomainLifetimeId;
                    if (StopRequested) { Result.Status = RuntimeStatus.INVALID_ARGUMENT; Result.Error = "host stopping"; }
                    if (Result.Status != RuntimeStatus.OK) {
                        Result.Logs = ""; LastReload = "rejected: " + Result.Status;
                        if (Result.Retired) ReleaseVm(true);
                        if (Current == null) Reason = LastReload;
                        return Result;
                    }
                    SchedulerInfo PreviousInfo = Vm.Scheduler;
                    Candidate.Commit();
                    if (Facade != null) Facade.Commit(Candidate.FacadeSession);
                    ReleaseDomain(true, PreviousInfo);
                    Current = Candidate; Candidate = null; Entry = Snapshot.EntryName;
                    Reason = null; LastReload = Operator ? "operator OK" : "recovery OK";
                    if (Operator) RecoveryAvailable = true;
                    return Result;
                } catch (Exception Error) {
                    LastReload = "initialization rejected"; if (Current == null) Reason = LastReload;
                    // File exceptions can contain absolute paths. Snapshot validation
                    // emits curated InvalidOperationException diagnostics only.
                    return new ExecutionResult { Status = RuntimeStatus.INTERNAL_ERROR,
                        Error = Error is InvalidOperationException ? Error.Message.Substring(0, Math.Min(1024, Error.Message.Length)) : "source snapshot/native initialization failed" };
                } finally {
                    if (Candidate != null) {
                        if (Candidate.FacadeSession != null) Facade.Retire(Candidate.FacadeSession);
                        Candidate.Dispose();
                    }
                    Busy = false;
                }
            }
            private ExecutionResult Recover()
            {
                ReleaseVm(true);
                if (!RecoveryAvailable) { Reason = "automatic recovery exhausted; fix scripts and use carbonluau.reload"; return null; }
                RecoveryAvailable = false; Recoveries++;
                var Result = Replace(false, null);
                if (Result.Status != RuntimeStatus.OK) {
                    if (Current == null) ReleaseVm(true);
                    Reason = "automatic recovery failed; fix scripts and use carbonluau.reload";
                }
                return Result;
            }
            public ExecutionResult Execute(string Chunk, string Source)
            {
                Native.CheckOwner();
                if (!Ready || Busy) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT, Error = Reason ?? "runtime busy" };
                ExecutionResult Result;
                Busy = true;
                try { Result = Current.Execute(Chunk, Source, Settings.MaxCallbackMilliseconds); }
                finally { Busy = false; }
                Result.Generation = Generation; Result.VmGenerationId = VmGenerationId; Result.DomainLifetimeId = Generation;
                if (Result.Retired || Vm.Info.Ready == 0) {
                    if (Result.Status == RuntimeStatus.TIMEOUT) Timeouts++;
                    var Recovery = Recover(); Result.Error += "; recovery=" + (Recovery == null ? "exhausted" : Recovery.Status.ToString());
                }
                return Result;
            }
            public List<ExecutionResult> Drain()
            {
                Native.CheckOwner();
                var Results = new List<ExecutionResult>();
                if (Disposed || StopRequested || Busy || Draining || Vm==null || Current==null) return Results;
                Draining = true;
                try {
                    var Watch = Stopwatch.StartNew();
                    Native.PumpStorage();
                    if (Vm.Info.Ready == 0) { var Recovery = Recover(); if (Recovery != null) Results.Add(Recovery); return Results; }
                    if (!Ready) return Results;
                    FlushDomainFacades(Watch);
                    if (Vm.Info.Ready == 0) { var Recovery = Recover(); if (Recovery != null) Results.Add(Recovery); return Results; }
                    SchedulerInfo Cutoff = Vm.Scheduler;
                    for (int Count = 0; Count < 256 && !StopRequested && Watch.Elapsed.TotalMilliseconds < Settings.FrameDrainBudgetMilliseconds; ++Count) {
                        bool Ran; ExecutionResult Result;
                        Busy = true;
                        try { Result = Vm.Callback(Cutoff, Settings.MaxCallbackMilliseconds, out Ran); }
                        finally { Busy = false; }
                        if (!Ran) break;
                        Result.Generation = Generation; Result.VmGenerationId = VmGenerationId; Result.DomainLifetimeId = Generation; Attempted++;
                        if (Result.Status == RuntimeStatus.OK) Completed++; else Failed++;
                        // At most 256 fixed-size results, additionally bounded by time.
                        Results.Add(Result);
                        if (Result.Retired || Vm.Info.Ready == 0) {
                            if (Result.Status == RuntimeStatus.TIMEOUT) Timeouts++;
                            var Recovery = Recover(); if (Recovery != null) Results.Add(Recovery);
                            break; // Never use the retired drain's cutoff/handles for a new generation.
                        }
                    }
                    if (Facade != null && Vm != null && Vm.Info.Ready != 0 && !Busy && !StopRequested) {
                        var GuiWatch = Stopwatch.StartNew();
                        Facade.FlushGui(GuiWatch, Math.Max(1, (Facade.Gui.Limits.GuiFlushBudgetMicroseconds + 999) / 1000));
                    }
                    if (Watch.Elapsed.TotalMilliseconds > Settings.FrameDrainBudgetMilliseconds) BudgetOverruns++;
                    return Results;
                } finally { Draining = false; }
            }
            public string Status()
            {
                Native.CheckOwner(); bool Healthy = Ready;
                SchedulerInfo Info = Healthy ? Vm.Scheduler : new SchedulerInfo();
                return "CarbonLuau: " + (Healthy ? "ready" : "unavailable") + "\nGeneration: " + Generation
                    + "\nVM generation: " + VmGenerationId + "; root domain: " + Generation + "\nEntrypoint: " + Entry
                    + "\nNative ABI: " + (Native.AbiVersion >> 16) + "." + (Native.AbiVersion & 65535) + " OK\nLuau: " + Native.Revision + "\nReason: " + (Reason ?? "none")
                    + (Facade == null ? "" : "\nScripting API: " + FacadePolicy.ApiName + " " + FacadePolicy.ApiVersion)
                    + "\nVM bytes: " + (Healthy ? Vm.Info.MemoryBytes : 0) + " / " + ((long)Settings.MaxVmMemoryMiB * 1048576)
                    + "\nCallback deadline: " + Settings.MaxCallbackMilliseconds + " ms; frame budget: " + Settings.FrameDrainBudgetMilliseconds + " ms"
                    + "\nQueued: " + Info.Queued + "; modules: " + Info.Modules + "; last reload: " + LastReload
                    + "\nCallbacks attempted/completed/failed/cancelled/invalidated/rejected: " + Attempted + "/" + Completed + "/" + Failed + "/" + Cancelled + "/" + Invalidated + "/" + (Rejected + Info.Rejected + (Healthy && Current.FacadeSession != null ? Current.FacadeSession.Rejected : 0))
                    + (Healthy && Current.FacadeSession != null ? "\nFacade pending/listeners/commands: " + Current.FacadeSession.PendingCount + "/" + Current.FacadeSession.ListenerCount + "/" + Current.FacadeSession.Commands.Count : "")
                    + "\nTimeouts: " + Timeouts + "; recoveries: " + Recoveries + "; recovery available: " + RecoveryAvailable + "; budget overruns: " + BudgetOverruns;
            }
            public void Dispose() { if (Disposed) return; Native.CheckOwner(); if (Busy) throw new InvalidOperationException("runtime busy; defer teardown until execution returns"); Disposed = true; RecoveryAvailable = false; ReleaseVm(false); Reason = "unloaded"; }
        }
    }
}
