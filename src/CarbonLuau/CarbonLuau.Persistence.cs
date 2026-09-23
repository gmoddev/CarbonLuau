using System;
using System.IO;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private StorageSupervisor Persistence;
        private void InitializePersistence()
        {
            try {
                var Queue=new StorageQueue(checked((ulong)Native.HostLifetimeId),()=>StorageProcess.Now);
                string Root=Path.Combine(Oxide.Core.Interface.Oxide.DataDirectory,"CarbonLuau");
                string Name=Native.Rid=="win-x64" ? "carbonluau_storage.exe" : "carbonluau_storage";
                Persistence=new StorageSupervisor(Queue,Path.GetFullPath(Path.Combine(Root,"native",Native.Rid,Name)),
                    Path.GetFullPath(Path.Combine(Root,"persistence")));
                Native.Storage=Queue; Persistence.Start();
            } catch (Exception) {
                Persistence=null;
                PrintWarning("[CarbonLuau:Persistence] Storage unavailable; scripting runtime remains independent.");
            }
        }
        // No VM entry or I/O. Foundation 1A accepts no public Luau requests.
        private void OnTick()
        {
            if (Stopping || Persistence==null) return;
            try { Persistence.Tick(); }
            catch (Exception) { StopPersistence(); PrintWarning("[CarbonLuau:Persistence] Storage intake stopped after an internal failure."); }
        }
        private void StopPersistence()
        {
            if (Native!=null && Native.Storage!=null) Native.Storage.RetireAll();
            if (Persistence!=null) { Persistence.Stop(); Persistence=null; }
        }
    }
}
