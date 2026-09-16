using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed class RuntimeConfig
        {
            public bool Enabled = true;
            public int MaxVmMemoryMiB = 64;
            public int MaxCallbackMilliseconds = 3;
            public string ScriptRoot = "scripts", EntryScript = "init.luau", ModuleRoot = "modules";
            public int FrameDrainBudgetMilliseconds = 5, MaxQueuedCallbacks = 4096;

            public RuntimeConfig Validate()
            {
                return new RuntimeConfig {
                    Enabled = Enabled,
                    MaxVmMemoryMiB = Math.Max(16, Math.Min(256, MaxVmMemoryMiB)),
                    MaxCallbackMilliseconds = Math.Max(1, Math.Min(100, MaxCallbackMilliseconds)),
                    ScriptRoot = ScriptRoot, EntryScript = EntryScript, ModuleRoot = ModuleRoot,
                    FrameDrainBudgetMilliseconds = Math.Max(1, Math.Min(20, FrameDrainBudgetMilliseconds)),
                    MaxQueuedCallbacks = Math.Max(1, Math.Min(4096, MaxQueuedCallbacks))
                };
            }
        }

        public enum RuntimeStatus
        {
            OK = 0, YIELDED = 1, RUNTIME_ERROR = 2, COMPILE_ERROR = 3,
            MEMORY_LIMIT = 4, TIMEOUT = 5, INVALID_ARGUMENT = 6, INTERNAL_ERROR = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VmConfig { public ulong MemoryLimitBytes; }
        [StructLayout(LayoutKind.Sequential)]
        public struct VmInfo { public ulong MemoryBytes, MemoryLimitBytes, Ready; }
        [StructLayout(LayoutKind.Sequential)]
        public struct VmGenerationInfo { public ulong VmGenerationId, Domains; }
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeResult
        {
            public double Number;
            public uint HasNumber, Flags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)] public byte[] Error;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)] public byte[] Logs;
        }

        public sealed class ExecutionResult
        {
            public RuntimeStatus Status;
            public long Generation;
            public long VmGenerationId, DomainLifetimeId;
            public string Error = "", Logs = "";
            public bool Retired, LogTruncated, HasNumber;
            public double Number;
            public static ExecutionResult FromNative(RuntimeStatus Status, NativeResult Value)
            {
                if (!Enum.IsDefined(typeof(RuntimeStatus), Status)) Status = RuntimeStatus.INTERNAL_ERROR;
                return new ExecutionResult { Status = Status, Error = Decode(Value.Error), Logs = Decode(Value.Logs),
                    Retired = (Value.Flags & 1) != 0, LogTruncated = (Value.Flags & 2) != 0,
                    HasNumber = Value.HasNumber != 0, Number = Value.Number };
            }
            private static string Decode(byte[] Bytes)
            {
                if (Bytes == null) return "";
                int Length = Array.IndexOf(Bytes, (byte)0);
                return Encoding.UTF8.GetString(Bytes, 0, Length < 0 ? Bytes.Length : Length);
            }
        }

        // Owns the loaded library and all ABI bindings. No finalizer: destruction
        // must run on the owner thread, never on the managed GC thread.
        public sealed partial class NativeRuntime : IDisposable
        {
            private readonly NativeLibraryLoader Loader = new NativeLibraryLoader();
            private readonly int Owner = Thread.CurrentThread.ManagedThreadId;
            private readonly HashSet<ulong> Vms = new HashSet<ulong>();
            private static long NextHostLifetimeId;
            private bool Disposed;
            private bool InsideNative;
            public long HostLifetimeId { get; private set; }
            public uint AbiVersion { get; private set; }
            public string Rid { get { return Loader.Rid; } }
            public string Revision { get; private set; }
            public int LiveVmCount { get { return Vms.Count; } }
            public int DestroyedVmCount { get; private set; }
            public string UnloadError { get { return Loader.UnloadError; } }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint VersionDelegate();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus RevisionDelegate([Out] byte[] Buffer, uint Capacity);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus CreateDelegate(ref VmConfig Config, out ulong Vm);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DestroyDelegate(ulong Handle);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus InfoDelegate(ulong Vm, out VmInfo Info);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus GenerationInfoDelegate(ulong Vm, out VmGenerationInfo Info);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus LoadDelegate(ulong Vm,
                [MarshalAs(UnmanagedType.LPStr)] string Chunk, byte[] Source, uint Length, out ulong ThreadHandle, out NativeResult Result);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus ResumeDelegate(ulong ThreadHandle, ulong BudgetNs, out NativeResult Result);
            private CreateDelegate CreateVm;
            private DestroyDelegate DestroyVm, DestroyThread;
            private InfoDelegate ReadInfo;
            private GenerationInfoDelegate ReadGenerationInfo;
            private LoadDelegate LoadSource;
            private ResumeDelegate Resume;

            public NativeRuntime(string DataDirectory)
            {
                try
                {
                    Loader.Load(DataDirectory);
                    AbiVersion = Loader.Bind<VersionDelegate>("carbonluau_abi_version")();
                    if ((AbiVersion >> 16) != 1) throw new InvalidOperationException("native ABI major mismatch; expected 1");
                    byte[] RevisionBytes = new byte[41];
                    Require(Loader.Bind<RevisionDelegate>("cl_luau_revision")(RevisionBytes, 41));
                    Revision = Encoding.ASCII.GetString(RevisionBytes, 0, 40);
                    CreateVm = Loader.Bind<CreateDelegate>("cl_vm_create");
                    DestroyVm = Loader.Bind<DestroyDelegate>("cl_vm_destroy");
                    ReadInfo = Loader.Bind<InfoDelegate>("cl_vm_info");
                    if ((AbiVersion & 65535) >= 3) ReadGenerationInfo = Loader.Bind<GenerationInfoDelegate>("cl_vm_generation");
                    LoadSource = Loader.Bind<LoadDelegate>("cl_vm_load_source");
                    Resume = Loader.Bind<ResumeDelegate>("cl_thread_resume");
                    DestroyThread = Loader.Bind<DestroyDelegate>("cl_thread_destroy");
                    HostLifetimeId = Interlocked.Increment(ref NextHostLifetimeId);
                    if (HostLifetimeId <= 0) throw new InvalidOperationException("host lifetime identity exhausted");
                }
                catch { Loader.Dispose(); Disposed = true; throw; }
            }
            public void CheckOwner()
            {
                if (Thread.CurrentThread.ManagedThreadId != Owner) throw new InvalidOperationException("runtime owner-thread required");
                if (Disposed) throw new ObjectDisposedException("NativeRuntime");
                if (InsideNative) throw new InvalidOperationException("native callback reentry prohibited");
            }
            private static void Require(RuntimeStatus Status)
            {
                if (Status != RuntimeStatus.OK) throw new InvalidOperationException("native operation failed: " + Status);
            }
            private static void Require(RuntimeStatus Status, string Operation)
            {
                if (Status != RuntimeStatus.OK) throw new InvalidOperationException(Operation + " failed: " + Status);
            }
            public ulong Create(RuntimeConfig Config)
            {
                CheckOwner();
                VmConfig NativeConfig = new VmConfig { MemoryLimitBytes = (ulong)Config.Validate().MaxVmMemoryMiB * 1024 * 1024 };
                ulong Handle;
                Require(CreateVm(ref NativeConfig, out Handle));
                if (Handle == 0) throw new InvalidOperationException("native VM creation returned zero");
                try { Vms.Add(Handle); } catch { DestroyVm(Handle); throw; }
                return Handle;
            }
            public void Destroy(ulong Handle)
            {
                CheckOwner();
                if (!Vms.Contains(Handle)) return;
                Require(DestroyVm(Handle));
                FacadeRoots.Remove(Handle);
                Vms.Remove(Handle);
                DestroyedVmCount++;
            }
            public VmInfo Info(ulong Handle)
            {
                CheckOwner();
                VmInfo Value;
                Require(ReadInfo(Handle, out Value));
                return Value;
            }
            public VmGenerationInfo GenerationInfo(ulong Handle)
            {
                CheckOwner();
                if (ReadGenerationInfo == null) throw new InvalidOperationException("Foundation A requires native ABI 1.3 or later");
                VmGenerationInfo Value; Require(ReadGenerationInfo(Handle, out Value)); return Value;
            }
            public ExecutionResult Execute(ulong Handle, string Chunk, string Source, int Milliseconds)
            {
                CheckOwner();
                if (!Vms.Contains(Handle) || Source == null || Source.Length > 65536 || Milliseconds < 1 || Milliseconds > 100)
                    return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                byte[] Bytes = Encoding.UTF8.GetBytes(Source);
                if (Bytes.Length > 65536) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                ulong ThreadHandle = 0;
                NativeResult Value = new NativeResult();
                RuntimeStatus Status;
                InsideNative = true;
                try
                {
                    Status = LoadSource(Handle, Chunk, Bytes, (uint)Bytes.Length, out ThreadHandle, out Value);
                    if (Status == RuntimeStatus.OK) Status = Resume(ThreadHandle, (ulong)Milliseconds * 1000000, out Value);
                    return ExecutionResult.FromNative(Status, Value);
                }
                finally
                {
                    try {
                        if (ThreadHandle != 0) {
                            RuntimeStatus Cleanup = DestroyThread(ThreadHandle);
                            if (Cleanup != RuntimeStatus.OK && !((Value.Flags & 1) != 0 && Cleanup == RuntimeStatus.INVALID_ARGUMENT)) Require(Cleanup);
                        }
                    } finally { InsideNative = false; }
                }
            }
            public void Dispose()
            {
                if (Disposed) return;
                CheckOwner();
                foreach (ulong Handle in new List<ulong>(Vms)) Destroy(Handle);
                Disposed = true;
                CreateVm = null; DestroyVm = null; DestroyThread = null; ReadInfo = null; ReadGenerationInfo = null; LoadSource = null; Resume = null;
                Loader.Dispose();
            }
        }

        public sealed partial class RuntimeGeneration : IDisposable
        {
            private readonly NativeRuntime Native;
            internal ulong Handle;
            public readonly long Number;
            public bool Alive { get { return Handle != 0; } }
            public RuntimeGeneration(NativeRuntime Native, long Number, RuntimeConfig Config)
            {
                this.Native = Native; this.Number = Number;
                Handle = Native.Create(Config);
            }
            public VmInfo Info { get { return Native.Info(Handle); } }
            public ExecutionResult Execute(string Chunk, string Source, int Milliseconds)
            {
                if (!Alive) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                return Native.Execute(Handle, Chunk, Source, Milliseconds);
            }
            public void Dispose()
            {
                if (!Alive) return;
                Native.CheckOwner();
                ulong Owned = Handle;
                Handle = 0;
                Native.Destroy(Owned);
            }
        }

        public sealed class RuntimeHost : IDisposable
        {
            public const string Smoke = "print('hello from Luau'); assert(1 + 2 == 3); return 3";
            private readonly NativeRuntime Native;
            private readonly RuntimeConfig Settings;
            private bool Disposed;
            private RuntimeGeneration Current;
            public long Generation { get { return Current == null ? 0 : Current.Number; } }
            public string Reason { get; private set; }
            public RuntimeHost(NativeRuntime Native, RuntimeConfig Config)
            {
                this.Native = Native;
                Settings = Config.Validate();
                Reason = Settings.Enabled ? "not initialized" : "disabled by configuration";
            }
            public ExecutionResult Reload(string SmokeSource = Smoke)
            {
                Native.CheckOwner();
                if (Disposed || !Settings.Enabled) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT, Error = Reason };
                RuntimeGeneration Candidate = null;
                try
                {
                    Candidate = new RuntimeGeneration(Native, checked(Generation + 1), Settings);
                    ExecutionResult Result = Candidate.Execute("bootstrap", SmokeSource, Settings.MaxCallbackMilliseconds);
                    Result.Generation = Candidate.Number;
                    if (Result.Status != RuntimeStatus.OK || !Result.HasNumber || Result.Number != 3)
                    {
                        if (Result.Status == RuntimeStatus.OK) { Result.Status = RuntimeStatus.RUNTIME_ERROR; Result.Error = "bootstrap must return 3"; }
                        Result.Logs = ""; // Candidate output is staged until commit.
                        Reason = "replacement rejected: " + Result.Status;
                        return Result;
                    }
                    RuntimeGeneration Previous = Current;
                    Current = Candidate;
                    Candidate = null;
                    Reason = null;
                    if (Previous != null) Previous.Dispose();
                    return Result;
                }
                catch (Exception Error)
                {
                    Reason = "replacement initialization failed";
                    return new ExecutionResult { Status = RuntimeStatus.INTERNAL_ERROR, Error = Error.Message };
                }
                finally { if (Candidate != null) Candidate.Dispose(); }
            }
            public ExecutionResult Execute(string Chunk, string Source)
            {
                Native.CheckOwner();
                if (Disposed || Current == null) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT, Error = Reason };
                ExecutionResult Result = Current.Execute(Chunk, Source, Settings.MaxCallbackMilliseconds);
                Result.Generation = Current.Number;
                if (Result.Retired || Current.Info.Ready == 0)
                {
                    // The original failure is returned intact. No retry of user
                    // source. Recovery only executes our deterministic bootstrap.
                    ExecutionResult Recovery = Reload();
                    Result.Error += "; recovery=" + Recovery.Status + "; generation=" + Generation;
                }
                return Result;
            }
            public string Status()
            {
                Native.CheckOwner();
                bool Ready = !Disposed && Current != null && Current.Info.Ready != 0;
                VmInfo Info = Ready ? Current.Info : new VmInfo();
                return "CarbonLuau: " + (Ready ? "ready" : "unavailable")
                    + (Ready ? "" : "\nReason: " + (Reason ?? "runtime retired"))
                    + "\nGeneration: " + Generation + "\nNative ABI: 1.0 OK\nLuau: " + Native.Revision
                    + "\nVM memory: " + (Info.MemoryBytes / 1048576.0).ToString("F3", CultureInfo.InvariantCulture)
                    + " / " + Settings.MaxVmMemoryMiB + " MiB\nCallback deadline: " + Settings.MaxCallbackMilliseconds + " ms";
            }
            public void Dispose()
            {
                if (Disposed) return;
                Native.CheckOwner();
                Disposed = true;
                RuntimeGeneration Previous = Current;
                Current = null;
                Reason = "unloaded";
                if (Previous != null) Previous.Dispose();
            }
        }
    }
}
