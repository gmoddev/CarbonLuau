using System;

namespace Carbon.Plugins
{
    [Info("CarbonLuau", "gmoddev", "0.0.1")]
    [Description("Phase 0 native-load feasibility probe")]
    public partial class CarbonLuau : CarbonPlugin
    {
        private NativeLibraryLoader Loader;
        private bool Attempted;

        private void OnServerInitialized()
        {
            if (Attempted) return;
            Attempted = true;
            try
            {
                Loader = new NativeLibraryLoader();
                Loader.Load(Oxide.Core.Interface.Oxide.DataDirectory);
                Puts("[CarbonLuau:Native] Native probe loaded successfully. Platform: " + Loader.Rid
                    + "; ABI probe: 0x4C554155; Path: " + Loader.LibraryPath);
            }
            catch (Exception Error)
            {
                PrintError("[CarbonLuau:Native] Unavailable: " + Error.Message);
                ReleaseNative();
            }
        }

        private void Unload() { ReleaseNative(); }

        private void ReleaseNative()
        {
            try
            {
                if (Loader == null) return;
                bool WasAvailable = Loader.Available;
                Loader.Dispose();
                if (Loader.UnloadError != null)
                    PrintError("[CarbonLuau:Native] " + Loader.UnloadError);
                else if (WasAvailable)
                    Puts("[CarbonLuau:Native] Native library unloaded successfully.");
                Loader = null;
            }
            catch (Exception) { /* Never throw through Carbon's unload hook. */ }
        }
    }
}
