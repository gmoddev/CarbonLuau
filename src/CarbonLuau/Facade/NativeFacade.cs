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
        public sealed partial class NativeRuntime
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate uint HostDelegate(ulong Generation, uint Operation, IntPtr Request, uint Length, IntPtr Response, uint Capacity, out uint Written);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus FacadeDelegate(ulong Vm, ulong Generation, HostDelegate Host);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus EventDelegate(ulong Vm, byte[] Payload, uint Length);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainFacadeDelegate(ulong Vm, ulong Domain, HostDelegate Host);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus DomainEventDelegate(ulong Vm, ulong Domain, byte[] Payload, uint Length);
            private FacadeDelegate InstallFacade;
            private EventDelegate AdmitEvent;
            private DomainFacadeDelegate InstallDomainFacade;
            private DomainEventDelegate AdmitDomainEvent;
            private readonly Dictionary<ulong, FacadeSession> FacadeRoots = new Dictionary<ulong, FacadeSession>();
            private readonly Dictionary<ulong, FacadeSession> DomainFacadeRoots = new Dictionary<ulong, FacadeSession>();
            public void Facade(ulong Handle, FacadeSession Session)
            {
                CheckOwner();
                if ((AbiVersion & 65535) < 2) throw new FacadeException("Phase 3 requires native ABI 1.2 or later");
                if (InstallFacade == null) { InstallFacade = Loader.Bind<FacadeDelegate>("cl_vm_facade"); AdmitEvent = Loader.Bind<EventDelegate>("cl_vm_event"); }
                FacadeRoots.Add(Handle, Session); // Root until native destruction succeeds, including failed installation.
                InsideNative = true;
                try { Require(InstallFacade(Handle, (ulong)Session.Generation, Session.Callback)); }
                finally { InsideNative = false; }
            }
            public RuntimeStatus Event(ulong Handle, byte[] Payload) { CheckOwner(); return AdmitEvent(Handle, Payload, (uint)Payload.Length); }
            public void DomainFacade(ulong Vm, ulong Domain, FacadeSession Session)
            {
                CheckOwner();
                if ((AbiVersion & 65535) < 3) throw new FacadeException("Foundation A requires native ABI 1.3 or later");
                if (InstallDomainFacade == null) {
                    InstallDomainFacade = Loader.Bind<DomainFacadeDelegate>("cl_domain_facade");
                    AdmitDomainEvent = Loader.Bind<DomainEventDelegate>("cl_domain_event");
                }
                if ((ulong)Session.DomainLifetimeId != Domain) throw new FacadeException("facade/domain lifetime mismatch");
                if (Session.StorageBinding!=null && (Session.Storage!=Storage || Session.StorageBinding.Host!=(ulong)HostLifetimeId ||
                    Session.StorageBinding.Vm!=(ulong)Session.VmGenerationId || Session.StorageBinding.Domain!=Domain))
                    throw new FacadeException("storage facade lifetime mismatch");
                BindStorage();
                Session.StorageVm=Vm;
                DomainFacadeRoots.Add(Domain, Session);
                InsideNative = true;
                try { Require(InstallDomainFacade(Vm, Domain, Session.Callback), "domain facade"); }
                catch { DomainFacadeRoots.Remove(Domain); throw; }
                finally { InsideNative = false; }
            }
            public RuntimeStatus DomainEvent(ulong Vm, ulong Domain, byte[] Payload)
            { CheckOwner(); return AdmitDomainEvent(Vm, Domain, Payload, (uint)Payload.Length); }
            public void ReleaseDomainFacade(ulong Domain) { DomainFacadeRoots.Remove(Domain); }
        }
        public sealed partial class RuntimeGeneration
        {
            public FacadeSession FacadeSession { get; private set; }
            public void Facade(FacadeSession Session) { FacadeSession = Session; Native.Facade(Handle, Session); }
            public RuntimeStatus Event(byte[] Payload) { return Native.Event(Handle, Payload); }
        }
    }
}
