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
        public sealed class RuntimeDomain : IDisposable
        {
            private readonly NativeRuntime Native;
            private readonly RuntimeGeneration Vm;
            private ulong Handle;
            internal ulong NativeHandle { get { return Handle; } }
            public readonly long VmGenerationId, DomainLifetimeId;
            public FacadeSession FacadeSession { get; private set; }
            internal StorageQueue.Binding StorageBinding { get; private set; }
            public bool Alive { get { return Handle != 0 && Vm.Alive; } }
            public VmInfo Info { get { return Vm.Info; } }
            public RuntimeDomain(NativeRuntime Native, RuntimeGeneration Vm, RuntimeConfig Config, ScriptSnapshot Snapshot, string PackageId=null)
            {
                this.Native = Native; this.Vm = Vm;
                VmGenerationId = checked((long)Native.GenerationInfo(Vm.Handle).VmGenerationId);
                Handle = Native.DomainCreate(Vm.Handle, Config, Snapshot);
                DomainLifetimeId = checked((long)Handle);
                if (Native.Storage!=null) {
                    try { StorageBinding=Native.Storage.Bind((ulong)VmGenerationId,(ulong)DomainLifetimeId,PackageId); }
                    catch (InvalidOperationException) { /* persistence admission unavailable; runtime remains independent */ }
                }
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
                if (StorageBinding!=null) { Native.Storage.Retire(StorageBinding); StorageBinding=null; }
                ulong Owned = Handle; Handle = 0;
                if (Vm.Alive) Native.DomainDestroy(Vm.Handle, Owned); else Native.ReleaseDomainFacade(Owned);
            }
        }
    }
}
