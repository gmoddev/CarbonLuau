using System.Runtime.InteropServices;
using System.Text;
using CarbonLuau.Core;
using Gui = Carbon.Plugins.CarbonLuau.PreviewGuiSession;

namespace CarbonLuau.Tooling;

// Private tooling bridge over the exact runtime VM/module/bootstrap implementation.
// The coordinator never constructs this type.
internal sealed class NativePreview : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct VmConfig { internal ulong MemoryLimitBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeResult {
        internal double Number; internal uint HasNumber, Flags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)] internal byte[] Error;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)] internal byte[] Logs;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Scheduler { internal ulong NowNs, NextDueNs, Sequence, Queued, Modules, Rejected, Discarded; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Version();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Revision([Out] byte[] Buffer, uint Capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Create(ref VmConfig Config, out ulong Vm);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Destroy(ulong Handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DomainCreate(ulong Vm, uint MaxQueued, out ulong Domain);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DomainOperation(ulong Vm, ulong Domain);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Module(ulong Vm, ulong Domain, [MarshalAs(UnmanagedType.LPUTF8Str)] string Name, byte[] Source, uint Length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Addon(ulong Vm, ulong Domain, [MarshalAs(UnmanagedType.LPUTF8Str)] string Id, [MarshalAs(UnmanagedType.LPUTF8Str)] string PackageVersion, [MarshalAs(UnmanagedType.LPUTF8Str)] string Main);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PublicModule(ulong Vm, ulong Domain, [MarshalAs(UnmanagedType.LPUTF8Str)] string Name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Dependency(ulong Vm, ulong Domain, [MarshalAs(UnmanagedType.LPUTF8Str)] string Id, ulong Target);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Host(ulong Generation, uint Operation, IntPtr Request, uint Length, IntPtr Response, uint Capacity, out uint Written);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Facade(ulong Vm, ulong Domain, Host Callback);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Load(ulong Vm, ulong Domain, [MarshalAs(UnmanagedType.LPUTF8Str)] string Chunk, byte[] Source, uint Length, out ulong Thread, out NativeResult Result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Resume(ulong Thread, ulong BudgetNs, out NativeResult Result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadScheduler(ulong Vm, out Scheduler Info);
    private readonly IntPtr Library;
    private readonly ulong Vm;
    private readonly List<Host> Callbacks = [];
    private int GlobalObjects;
    private bool Disposed;
    internal NativePreview(string LibraryPath, string ExpectedRevision)
    {
        Library = NativeLibrary.Load(LibraryPath);
        try {
            byte[] Bytes = new byte[41];
            if (Bind<Version>("cl_preview_bridge_version")() != 1 || Bind<Version>("cl_preview_containment")() != 1 ||
                Bind<Version>("carbonluau_abi_version")() != 0x10005 || Bind<Revision>("cl_luau_revision")(Bytes, 41) != 0 ||
                Encoding.ASCII.GetString(Bytes, 0, 40) != ExpectedRevision)
                throw new ProtocolError("IncompatiblePack", "Preview bridge identity or process containment mismatch.");
            var Config = new VmConfig { MemoryLimitBytes = 64 * 1024 * 1024 };
            Require(Bind<Create>("cl_vm_create")(ref Config, out Vm), "VM creation");
        } catch { NativeLibrary.Free(Library); throw; }
    }
    private T Bind<T>(string Name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(Library, Name));
    internal (ulong Domain, Gui Gui) CreateDomain(PreviewProject Project, Dictionary<string, ulong> Active)
    {
        Require(Bind<DomainCreate>("cl_domain_create")(Vm, 256, out ulong Domain), "domain creation");
        var Tree = new Gui(Domain, Change => {
            int Next = checked(GlobalObjects + Change);
            if (Next < 0 || Next > Gui.GlobalObjectLimit) throw new InvalidOperationException("global GUI object limit reached");
            GlobalObjects = Next;
        });
        foreach (var Source in Project.Sources.Modules) {
            byte[] Bytes = Protocol.Utf8.GetBytes(Source.Value);
            Require(Bind<Module>("cl_domain_module")(Vm, Domain, Source.Key, Bytes, (uint)Bytes.Length), "module admission");
        }
        if (Project.Package != null) {
            var Package = Project.Package;
            Require(Bind<Addon>("cl_domain_addon")(Vm, Domain, Package.Id, Package.Version, Package.Main ?? ""), "addon metadata");
            foreach (string Name in Package.PublicModules()) Require(Bind<PublicModule>("cl_domain_public_module")(Vm, Domain, Name), "public module admission");
            foreach (string Name in Package.Dependencies(false).Concat(Package.Dependencies(true))) {
                Active.TryGetValue(Name, out ulong Target);
                if (Target == 0 && Package.Dependencies(false).Contains(Name, StringComparer.Ordinal)) throw new ProtocolError("ModuleError", "Required preview dependency is not active: " + Name);
                Require(Bind<Dependency>("cl_domain_dependency")(Vm, Domain, Name, Target), "dependency binding");
            }
        }
        Host Callback = (ulong Generation, uint Operation, IntPtr Request, uint Length, IntPtr Response, uint Capacity, out uint Written) => {
            Written = 0;
            try {
                if (Generation != Domain || Length > 16384 || Capacity < 256 || Capacity > 262144) return 1;
                byte[] Input = new byte[Length]; Marshal.Copy(Request, Input, 0, Input.Length);
                if (Input.Length != 0 && Input[^1] != 0) throw new InvalidOperationException("invalid host payload");
                string[] Fields = Input.Length == 0 ? [] : Protocol.Utf8.GetString(Input, 0, Input.Length - 1).Split('\0');
                string[] Result;
                if (Operation == 0) { if (Fields.Length != 0) return 1; Result = []; }
                else if (Operation is 10 or 11 or 12 or 20 or 21) Result = Tree.Call(Operation, Fields);
                else throw new InvalidOperationException("[Preview:UnsupportedHost] " + OperationName(Operation) + " is unavailable in the static GUI preview environment.");
                byte[] Output = Protocol.Utf8.GetBytes(string.Join('\0', Result) + (Result.Length == 0 ? "" : "\0"));
                if (Output.Length > Capacity) throw new InvalidOperationException("GUI response exceeds native bridge capacity");
                Marshal.Copy(Output, 0, Response, Output.Length); Written = (uint)Output.Length; return 0;
            } catch (Exception Error) {
                string Prefix = Operation is 10 or 11 or 12 or 20 or 21 ? "[Preview:Gui] " : "";
                byte[] Output = Encoding.UTF8.GetBytes(AddonPolicy.Diagnostic(Prefix + Error.Message));
                int Count = Math.Min(Math.Min(Output.Length, 240), (int)Capacity);
                Marshal.Copy(Output, 0, Response, Count); Written = (uint)Count; return 1;
            }
        };
        Callbacks.Add(Callback);
        Require(Bind<Facade>("cl_domain_facade")(Vm, Domain, Callback), "preview facade installation");
        return (Domain, Tree);
    }
    internal void Execute(ulong Domain, string Chunk, string Source)
    {
        byte[] Bytes = Protocol.Utf8.GetBytes(Source);
        int Status = Bind<Load>("cl_domain_load_source")(Vm, Domain, Chunk, Bytes, (uint)Bytes.Length, out ulong Thread, out NativeResult Result);
        try {
            if (Status == 0) Status = Bind<Resume>("cl_thread_resume")(Thread, 100_000_000, out Result);
            if (Status != 0) {
                string Message = Decode(Result.Error);
                string Code = Status switch { 3 => "CompileError", 4 => "PreviewMemory", 5 => "PreviewDeadline", _ => "RuntimeError" };
                if (Status != 4 && Status != 5) {
                    if (Message.Contains("[Preview:UnsupportedHost]", StringComparison.Ordinal) ||
                        Message.Contains("unavailable in the static GUI preview environment", StringComparison.Ordinal)) Code = "UnsupportedPreviewApi";
                    else if (Message.Contains("module ", StringComparison.Ordinal) || Message.Contains("package ", StringComparison.Ordinal)) Code = "ModuleError";
                    else if (Message.Contains("[Preview:Gui]", StringComparison.Ordinal)) Code = "GuiError";
                }
                throw new ProtocolError(Code, AddonPolicy.Diagnostic(Message.Length == 0 ? "Preview execution failed with runtime status " + Status + "." : Message));
            }
        } finally { if (Thread != 0) Bind<Destroy>("cl_thread_destroy")(Thread); }
        Require(Bind<DomainOperation>("cl_domain_commit")(Vm, Domain), "domain commit");
        Require(Bind<ReadScheduler>("cl_vm_scheduler")(Vm, out Scheduler Info), "scheduler inspection");
        if (Info.Queued != 0) throw new ProtocolError("UnsupportedPreviewApi", "Queued task callbacks are unavailable in the static GUI preview environment; initialize GUI synchronously.");
    }
    private static string Decode(byte[]? Buffer) { if (Buffer == null) return ""; int End = Array.IndexOf(Buffer, (byte)0); return Encoding.UTF8.GetString(Buffer, 0, End < 0 ? Buffer.Length : End); }
    private static void Require(int Status, string Operation) {
        if (Status != 0) throw new ProtocolError(Status == 4 ? "PreviewMemory" : "PreviewNative", "Preview " + Operation + " failed with runtime status " + Status + ".");
    }
    private static string OperationName(uint Value) => Value switch {
        1 => "Players:GetPlayers", 2 => "Players:GetPlayerByUserId", 3 => "Player observation", 4 => "Player:SendMessage", 5 => "Player:HasPermission",
        6 or 7 => "Players Signal", 8 => "Commands:Register", 22 => "Player.Position", 23 or 24 => "Player health", 25 => "Items:Exists",
        26 or 27 => "Player inventory", 28 => "Player:Teleport", 29 => "Player:TakeItem", 30 => "Player:GiveItem", _ => "Host operation" };
    public void Dispose()
    {
        if (Disposed) return; Disposed = true;
        Bind<Destroy>("cl_vm_destroy")(Vm); Callbacks.Clear(); NativeLibrary.Free(Library);
    }
}
