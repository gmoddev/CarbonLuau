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
    }
}

