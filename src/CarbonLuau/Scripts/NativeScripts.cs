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
    }
}

