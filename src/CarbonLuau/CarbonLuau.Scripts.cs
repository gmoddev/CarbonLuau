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
        // Snapshot before entering Luau: no file access from native require or a
        // scheduler callback. Filesystem owner is trusted against concurrent edits.
        public sealed class ScriptSnapshot
        {
            public string EntryName, EntrySource;
            public readonly SortedDictionary<string, string> Modules = new SortedDictionary<string, string>(StringComparer.Ordinal);
            private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            public static void ValidatePath(string Value, bool File)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > 127 || Path.IsPathRooted(Value)) throw new InvalidOperationException("source path: expected bounded relative path");
                string Name = Value;
                if (File) {
                    if (!Name.EndsWith(".luau", StringComparison.Ordinal)) throw new InvalidOperationException("source path: expected .luau extension");
                    Name = Name.Substring(0, Name.Length - 5);
                }
                foreach (string Part in Name.Split('/')) {
                    if (Part.Length == 0) throw new InvalidOperationException("source path: empty segment");
                    foreach (char C in Part)
                        if (!(C >= 'a' && C <= 'z') && !(C >= '0' && C <= '9') && C != '_' && C != '-')
                            throw new InvalidOperationException("source path: use lowercase letters, digits, '_' or '-' and single '/' separators");
                    string Upper = Part.ToUpperInvariant();
                    if (Upper == "CON" || Upper == "PRN" || Upper == "AUX" || Upper == "NUL" ||
                        (Upper.Length == 4 && (Upper.StartsWith("COM") || Upper.StartsWith("LPT")) && Upper[3] >= '0' && Upper[3] <= '9'))
                        throw new InvalidOperationException("source path: reserved device name");
                }
            }
            private static void CheckNode(string PathValue)
            {
                FileAttributes Attributes = File.GetAttributes(PathValue);
                if ((Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("source path: symlinks/reparse points are unsupported");
            }
            private static string Resolve(string Root, string Relative, bool File)
            {
                ValidatePath(Relative, File);
                string Current = Root;
                foreach (string Part in Relative.Split('/')) {
                    // Ordinal segment equality prevents case-insensitive Windows aliases.
                    bool Exact = false;
                    int Count = 0;
                    foreach (string Child in Directory.EnumerateFileSystemEntries(Current)) {
                        if (++Count > 1024) throw new InvalidOperationException("source directory exceeds 1024 entries");
                        if (String.Equals(Path.GetFileName(Child), Part, StringComparison.Ordinal)) { Exact = true; break; }
                    }
                    if (!Exact) throw new InvalidOperationException("source not found: " + Relative);
                    Current = Path.Combine(Current, Part); CheckNode(Current);
                }
                return Current;
            }
            private static string ReadSource(string FileName, string Logical)
            {
                try {
                    using (var Stream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        if (Stream.Length > 65536) throw new InvalidOperationException("source exceeds 65536 bytes: " + Logical);
                        byte[] Bytes = new byte[65537]; int Count = 0, Read;
                        while (Count < Bytes.Length && (Read = Stream.Read(Bytes, Count, Bytes.Length - Count)) != 0) Count += Read;
                        if (Count > 65536) throw new InvalidOperationException("source exceeds 65536 bytes: " + Logical);
                        int Start = Count >= 3 && Bytes[0] == 239 && Bytes[1] == 187 && Bytes[2] == 191 ? 3 : 0;
                        string Text = Utf8.GetString(Bytes, Start, Count - Start);
                        if (Text.IndexOf('\0') >= 0) throw new InvalidOperationException("NUL source rejected: " + Logical);
                        return Text;
                    }
                } catch (DecoderFallbackException) { throw new InvalidOperationException("source must be valid UTF-8: " + Logical); }
                catch (IOException) { throw new InvalidOperationException("source could not be read: " + Logical); }
                catch (UnauthorizedAccessException) { throw new InvalidOperationException("source access denied: " + Logical); }
            }
            public static ScriptSnapshot Load(string DataDirectory, RuntimeConfig Config)
            {
                ValidatePath(Config.ScriptRoot, false); ValidatePath(Config.ModuleRoot, false); ValidatePath(Config.EntryScript, true);
                if (Config.EntryScript.Length > 121) throw new InvalidOperationException("entry path exceeds 121 characters including extension");
                string Root = Path.GetFullPath(Path.Combine(DataDirectory, "CarbonLuau"));
                // Fail closed for links anywhere in the configured ancestor chain.
                for (var Node = new DirectoryInfo(Root); Node != null; Node = Node.Parent) CheckNode(Node.FullName);
                Root = Resolve(Root, Config.ScriptRoot, false);
                var Snapshot = new ScriptSnapshot { EntryName = Config.EntryScript };
                Snapshot.EntrySource = ReadSource(Resolve(Root, Config.EntryScript, true), Config.EntryScript);
                string ModuleDirectory = Resolve(Root, Config.ModuleRoot, false);
                var Pending = new Stack<KeyValuePair<string, string>>();
                Pending.Push(new KeyValuePair<string, string>(ModuleDirectory, ""));
                int Nodes = 0, Total = Utf8.GetByteCount(Snapshot.EntrySource);
                while (Pending.Count != 0) {
                    var Directory = Pending.Pop();
                    foreach (string Child in System.IO.Directory.EnumerateFileSystemEntries(Directory.Key)) {
                        if (++Nodes > 1024) throw new InvalidOperationException("module tree exceeds 1024 entries");
                        CheckNode(Child);
                        string Relative = Directory.Value + Path.GetFileName(Child);
                        if ((File.GetAttributes(Child) & FileAttributes.Directory) != 0) {
                            ValidatePath(Relative, false);
                            Pending.Push(new KeyValuePair<string, string>(Child, Relative + "/"));
                        } else {
                            if (!Relative.EndsWith(".luau", StringComparison.Ordinal)) continue;
                            ValidatePath(Relative, true);
                            if (Snapshot.Modules.Count == 256) throw new InvalidOperationException("module count exceeds 256");
                            string Source = ReadSource(Child, Config.ModuleRoot + "/" + Relative);
                            Total = checked(Total + Utf8.GetByteCount(Source));
                            if (Total > 4 * 1024 * 1024) throw new InvalidOperationException("source snapshot exceeds 4 MiB");
                            Snapshot.Modules.Add(Relative.Substring(0, Relative.Length - 5), Source);
                        }
                    }
                }
                return Snapshot;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SchedulerInfo { public ulong NowNs, NextDueNs, Sequence, Queued, Modules, Rejected, Discarded; }
        public sealed partial class NativeRuntime
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus ScriptsDelegate(ulong Vm, uint MaxQueued);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus ModuleDelegate(ulong Vm,
                [MarshalAs(UnmanagedType.LPStr)] string Name, byte[] Source, uint Length);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus SchedulerDelegate(ulong Vm, out SchedulerInfo Info);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus CallbackDelegate(ulong Vm, ulong CutoffNs, ulong Sequence, ulong BudgetNs, out uint Ran, out NativeResult Result);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainCreateDelegate(ulong Vm, uint MaxQueued, out ulong Domain);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainOperationDelegate(ulong Vm, ulong Domain);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainModuleDelegate(ulong Vm, ulong Domain,
                [MarshalAs(UnmanagedType.LPStr)] string Name, byte[] Source, uint Length);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainLoadDelegate(ulong Vm, ulong Domain,
                [MarshalAs(UnmanagedType.LPStr)] string Chunk, byte[] Source, uint Length, out ulong ThreadHandle, out NativeResult Result);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainAddonDelegate(ulong Vm, ulong Domain,
                [MarshalAs(UnmanagedType.LPStr)] string Id, [MarshalAs(UnmanagedType.LPStr)] string Version,
                [MarshalAs(UnmanagedType.LPStr)] string Main);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainNameDelegate(ulong Vm, ulong Domain,
                [MarshalAs(UnmanagedType.LPStr)] string Name);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainDependencyDelegate(ulong Vm, ulong Domain,
                [MarshalAs(UnmanagedType.LPStr)] string Id, ulong TargetDomain);
            private ScriptsDelegate InstallScripts;
            private ModuleDelegate InstallModule;
            private SchedulerDelegate ReadScheduler;
            private CallbackDelegate RunCallback;
            private DomainCreateDelegate CreateDomain;
            private DomainOperationDelegate DestroyDomain, CommitDomain;
            private DomainModuleDelegate InstallDomainModule;
            private DomainLoadDelegate LoadDomainSource;
            private DomainAddonDelegate InstallDomainAddon;
            private DomainNameDelegate InstallDomainPublicModule;
            private DomainDependencyDelegate InstallDomainDependency;
            private void BindDomains()
            {
                if ((AbiVersion & 65535) < 3) throw new InvalidOperationException("Foundation A requires native ABI 1.3 or later");
                if (ReadScheduler == null) {
                    ReadScheduler = Loader.Bind<SchedulerDelegate>("cl_vm_scheduler");
                    RunCallback = Loader.Bind<CallbackDelegate>("cl_vm_callback");
                }
                if (CreateDomain != null) return;
                CreateDomain = Loader.Bind<DomainCreateDelegate>("cl_domain_create");
                DestroyDomain = Loader.Bind<DomainOperationDelegate>("cl_domain_destroy");
                CommitDomain = Loader.Bind<DomainOperationDelegate>("cl_domain_commit");
                InstallDomainModule = Loader.Bind<DomainModuleDelegate>("cl_domain_module");
                LoadDomainSource = Loader.Bind<DomainLoadDelegate>("cl_domain_load_source");
            }
            private void BindAddonDomains()
            {
                BindDomains();
                if ((AbiVersion & 65535) < 4) throw new InvalidOperationException("Foundation D requires native ABI 1.4 or later");
                if (InstallDomainAddon != null) return;
                InstallDomainAddon = Loader.Bind<DomainAddonDelegate>("cl_domain_addon");
                InstallDomainPublicModule = Loader.Bind<DomainNameDelegate>("cl_domain_public_module");
                InstallDomainDependency = Loader.Bind<DomainDependencyDelegate>("cl_domain_dependency");
            }
            public void Scripts(ulong Handle, RuntimeConfig Config, ScriptSnapshot Snapshot)
            {
                CheckOwner();
                if ((AbiVersion & 65535) < 1) throw new InvalidOperationException("Phase 2 requires native ABI 1.1 or later");
                if (InstallScripts == null) {
                    InstallScripts = Loader.Bind<ScriptsDelegate>("cl_vm_scripts");
                    InstallModule = Loader.Bind<ModuleDelegate>("cl_vm_module");
                    ReadScheduler = Loader.Bind<SchedulerDelegate>("cl_vm_scheduler");
                    RunCallback = Loader.Bind<CallbackDelegate>("cl_vm_callback");
                }
                Require(InstallScripts(Handle, (uint)Config.MaxQueuedCallbacks));
                foreach (var Module in Snapshot.Modules) {
                    byte[] Bytes = Encoding.UTF8.GetBytes(Module.Value);
                    Require(InstallModule(Handle, Module.Key, Bytes, (uint)Bytes.Length));
                }
            }
            public SchedulerInfo Scheduler(ulong Handle) { CheckOwner(); SchedulerInfo Info; Require(ReadScheduler(Handle, out Info)); return Info; }
            public ExecutionResult Callback(ulong Handle, SchedulerInfo Cutoff, int Milliseconds, out bool Attempted)
            {
                CheckOwner(); uint Ran; NativeResult Value;
                RuntimeStatus Status;
                InsideNative = true;
                try { Status = RunCallback(Handle, Cutoff.NowNs, Cutoff.Sequence, (ulong)Milliseconds * 1000000, out Ran, out Value); }
                finally { InsideNative = false; }
                Attempted = Ran != 0; return ExecutionResult.FromNative(Status, Value);
            }
            public ulong DomainCreate(ulong Vm, RuntimeConfig Config, ScriptSnapshot Snapshot)
            {
                CheckOwner(); BindDomains(); ulong Domain; Require(CreateDomain(Vm, (uint)Config.MaxQueuedCallbacks, out Domain), "domain create");
                try {
                    foreach (var Module in Snapshot.Modules) {
                        byte[] Bytes = Encoding.UTF8.GetBytes(Module.Value);
                        Require(InstallDomainModule(Vm, Domain, Module.Key, Bytes, (uint)Bytes.Length), "domain module " + Module.Key);
                    }
                    return Domain;
                } catch { DestroyDomain(Vm, Domain); throw; }
            }
            public void DomainCommit(ulong Vm, ulong Domain) { CheckOwner(); BindDomains(); Require(CommitDomain(Vm, Domain), "domain commit"); }
            internal void DomainAddon(ulong Vm, ulong Domain, AddonPackageSnapshot Package, AddonDomainBinding[] Bindings)
            {
                CheckOwner(); BindAddonDomains();
                Require(InstallDomainAddon(Vm, Domain, Package.Id, Package.Version, Package.Main ?? ""), "domain addon metadata");
                foreach (string Module in Package.PublicModules())
                    Require(InstallDomainPublicModule(Vm, Domain, Module), "domain public module " + Module);
                foreach (AddonDomainBinding Binding in Bindings)
                    Require(InstallDomainDependency(Vm, Domain, Binding.Id,
                        Binding.Target == null ? 0 : Binding.Target.NativeHandle), "domain dependency " + Binding.Id);
            }
            public void DomainDestroy(ulong Vm, ulong Domain)
            {
                CheckOwner(); if (Domain == 0) return; BindDomains(); Require(DestroyDomain(Vm, Domain), "domain destroy"); ReleaseDomainFacade(Domain);
            }
            public ExecutionResult DomainExecute(ulong Vm, ulong Domain, string Chunk, string Source, int Milliseconds)
            {
                CheckOwner(); BindDomains();
                if (!Vms.Contains(Vm) || Domain == 0 || Source == null || Source.Length > 65536 || Milliseconds < 1 || Milliseconds > 100)
                    return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                byte[] Bytes = Encoding.UTF8.GetBytes(Source);
                if (Bytes.Length > 65536) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                ulong ThreadHandle = 0; NativeResult Value = new NativeResult(); RuntimeStatus Status;
                InsideNative = true;
                try {
                    Status = LoadDomainSource(Vm, Domain, Chunk, Bytes, (uint)Bytes.Length, out ThreadHandle, out Value);
                    if (Status == RuntimeStatus.OK) Status = Resume(ThreadHandle, (ulong)Milliseconds * 1000000, out Value);
                    return ExecutionResult.FromNative(Status, Value);
                } finally {
                    try {
                        if (ThreadHandle != 0) {
                            RuntimeStatus Cleanup = DestroyThread(ThreadHandle);
                            if (Cleanup != RuntimeStatus.OK && !((Value.Flags & 1) != 0 && Cleanup == RuntimeStatus.INVALID_ARGUMENT)) Require(Cleanup);
                        }
                    } finally { InsideNative = false; }
                }
            }
        }
        public sealed partial class RuntimeGeneration
        {
            public void Scripts(RuntimeConfig Config, ScriptSnapshot Snapshot) { Native.Scripts(Handle, Config, Snapshot); }
            public SchedulerInfo Scheduler { get { return Native.Scheduler(Handle); } }
            public ExecutionResult Callback(SchedulerInfo Cutoff, int Milliseconds, out bool Attempted)
            { return Native.Callback(Handle, Cutoff, Milliseconds, out Attempted); }
        }

        public sealed class RuntimeDomain : IDisposable
        {
            private readonly NativeRuntime Native;
            private readonly RuntimeGeneration Vm;
            private ulong Handle;
            internal ulong NativeHandle { get { return Handle; } }
            public readonly long VmGenerationId, DomainLifetimeId;
            public FacadeSession FacadeSession { get; private set; }
            public bool Alive { get { return Handle != 0 && Vm.Alive; } }
            public VmInfo Info { get { return Vm.Info; } }
            public RuntimeDomain(NativeRuntime Native, RuntimeGeneration Vm, RuntimeConfig Config, ScriptSnapshot Snapshot)
            {
                this.Native = Native; this.Vm = Vm;
                VmGenerationId = checked((long)Native.GenerationInfo(Vm.Handle).VmGenerationId);
                Handle = Native.DomainCreate(Vm.Handle, Config, Snapshot);
                DomainLifetimeId = checked((long)Handle);
            }
            public void Facade(FacadeSession Session) { FacadeSession = Session; Native.DomainFacade(Vm.Handle, Handle, Session); }
            internal void Addon(AddonPackageSnapshot Package, AddonDomainBinding[] Bindings)
            { if (!Alive) throw new InvalidOperationException("stale domain"); Native.DomainAddon(Vm.Handle, Handle, Package, Bindings); }
            public ExecutionResult Execute(string Chunk, string Source, int Milliseconds)
            {
                if (!Alive) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                ExecutionResult Result = Native.DomainExecute(Vm.Handle, Handle, Chunk, Source, Milliseconds);
                Result.Generation = DomainLifetimeId; Result.VmGenerationId = VmGenerationId; Result.DomainLifetimeId = DomainLifetimeId;
                return Result;
            }
            public void Commit() { if (!Alive) throw new InvalidOperationException("stale domain"); Native.DomainCommit(Vm.Handle, Handle); }
            public RuntimeStatus Event(byte[] Payload) { return Alive ? Native.DomainEvent(Vm.Handle, Handle, Payload) : RuntimeStatus.INVALID_ARGUMENT; }
            public void Dispose()
            {
                if (Handle == 0) return;
                ulong Owned = Handle; Handle = 0;
                if (Vm.Alive) Native.DomainDestroy(Vm.Handle, Owned); else Native.ReleaseDomainFacade(Owned);
            }
        }

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
            public bool HasWork { get { return Ready && (Vm.Scheduler.Queued != 0 || (Facade != null && Facade.HasWork)); } }
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
                if (!Ready || Draining) return Results;
                Draining = true;
                try {
                    var Watch = Stopwatch.StartNew();
                    if (Current.FacadeSession != null) Current.FacadeSession.Flush(Current, Watch, Settings.FrameDrainBudgetMilliseconds);
                    FlushAddonFacades(Watch);
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
