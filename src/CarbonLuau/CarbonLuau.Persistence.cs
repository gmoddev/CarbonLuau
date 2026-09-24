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
        // Owner-thread data intake only; RequestDrain schedules later Luau entry.
        private void OnTick()
        {
            if (Stopping || Persistence==null || Native==null || (Host!=null && Host.Busy)) return;
            try { Native.CheckOwner(); Persistence.Tick(); Native.PumpStorage(); RequestDrain(); }
            catch (Exception) {
                // An unexpected owner-thread intake/scheduling failure can leave
                // accepted native callbacks alive. Use normal host teardown;
                // controlled worker failures remain ordinary completion results.
                ReleaseNative();
                PrintError("[CarbonLuau:Persistence] Scripting stopped after an internal intake failure; reload the CarbonLuau plugin.");
            }
        }
        private void StopPersistence()
        {
            if (Native!=null && Native.Storage!=null) Native.Storage.RetireAll();
            if (Persistence!=null) { Persistence.Stop(); Persistence=null; }
        }
    }
}
