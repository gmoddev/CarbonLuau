using System;
using System.Runtime.InteropServices;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class NativeRuntime
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate RuntimeStatus StorageCompletionDelegate(ulong Vm,ulong Domain,ulong VmGeneration,
                ulong Route,uint Code,uint Found,[In] byte[] Envelope,uint Length);
            private StorageCompletionDelegate AdmitStorageCompletion;
            private ulong StorageFaultedVm;

            private void BindStorage()
            {
                if (Storage==null) return;
                if ((AbiVersion&65535)<5) throw new FacadeException("Persistence requires native ABI 1.5 or later");
                if (AdmitStorageCompletion!=null) return;
                AdmitStorageCompletion=Loader.Bind<StorageCompletionDelegate>("cl_domain_storage_completion");
            }

            internal void PumpStorage()
            {
                CheckOwner();
                if (Storage==null || AdmitStorageCompletion==null) return;
                StorageQueue.Request Request;
                // TakeCompletion owns the per-tick eight-result bound, including
                // stale discards. At most 1 MiB is copied across both sides here.
                while ((Request=Storage.TakeCompletion())!=null) {
                    bool Accepted=false;
                    try {
                        FacadeSession Session;
                        if (Request.Owner.Host!=(ulong)HostLifetimeId || !Request.Owner.Alive ||
                            !DomainFacadeRoots.TryGetValue(Request.Owner.Domain,out Session) || Session.Disposed || !Session.Active ||
                            Session.Storage!=Storage || Session.StorageBinding!=Request.Owner ||
                            (ulong)Session.VmGenerationId!=Request.Owner.Vm || !Vms.Contains(Session.StorageVm)) continue;
                        if (Info(Session.StorageVm).Ready==0) { Storage.RetireVm(Request.Owner.Vm); return; }
                        uint Length=Request.Error==StorageQueue.Error.None && Request.Op==StorageQueue.Operation.Get && Request.Found
                            ? (uint)Request.Envelope.Length : 0;
                        RuntimeStatus Status;
                        InsideNative=true;
                        try {
                            Status=AdmitStorageCompletion(Session.StorageVm,Request.Owner.Domain,Request.Owner.Vm,
                                Request.Route,(uint)Request.Error,Request.Found ? 1u : 0u,Request.Envelope,Length);
                        } catch (Exception) {
                            // A marshalling/allocation failure cannot establish
                            // whether native retained the result. Retire, never retry.
                            Status=RuntimeStatus.INTERNAL_ERROR;
                        } finally { InsideNative=false; }
                        if (Status==RuntimeStatus.OK) { Storage.Handoff(Request); Accepted=true; }
                        else {
                            // There is no owner-thread interleaving between route
                            // validation and ingress. Rejection of this live route
                            // is a protocol/integrity failure, never a retry. Expose
                            // the VM as unavailable until normal teardown/recovery.
                            StorageFaultedVm=Session.StorageVm;
                            Storage.RetireVm(Request.Owner.Vm);
                            return;
                        }
                    } finally {
                        if (!Accepted) { Storage.Release(Request); StorageQueue.Count(ref Storage.Discarded); }
                    }
                }
            }
        }
    }
}
