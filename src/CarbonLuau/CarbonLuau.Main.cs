using System;

namespace Carbon.Plugins
{
    [Info("CarbonLuau", "gmoddev", "0.1.0")]
    [Description("Phase 1 bounded sandboxed Luau execution core")]
    public partial class CarbonLuau : CarbonPlugin
    {
        private NativeRuntime Native;
        private RuntimeHost Host;
        private RuntimeConfig Settings;
        private bool Attempted, Initialized;
        private string UnavailableReason = "not initialized";

        protected override void LoadDefaultConfig() { Config.WriteObject(new RuntimeConfig(), true); }

        private void Loaded()
        {
            if (Attempted) return;
            Attempted = true;
            try
            {
                Settings = Config.ReadObject<RuntimeConfig>();
                if (Settings == null) throw new InvalidOperationException("configuration is null");
                RuntimeConfig Validated = Settings.Validate();
                if (Validated.MaxVmMemoryMiB != Settings.MaxVmMemoryMiB || Validated.MaxCallbackMilliseconds != Settings.MaxCallbackMilliseconds)
                    PrintWarning("[CarbonLuau:Config] Limits clamped to memory 16..256 MiB and callback 1..100 ms.");
                Settings = Validated;
                if (!Settings.Enabled) { UnavailableReason = "disabled by configuration"; return; }
            }
            catch (Exception Error)
            {
                UnavailableReason = "invalid configuration (see server log)";
                PrintError("[CarbonLuau:Config] " + Error.Message);
                return;
            }
            try
            {
                Native = new NativeRuntime(Oxide.Core.Interface.Oxide.DataDirectory);
                Host = new RuntimeHost(Native, Settings);
                Puts("[CarbonLuau:Native] Native probe loaded successfully. Platform: " + Native.Rid + "; ABI: 1.0");
            }
            catch (Exception Error)
            {
                UnavailableReason = Error is System.IO.FileNotFoundException ? "native library missing" : "native setup failed (ABI/platform/library; see server log)";
                PrintError("[CarbonLuau:Native] Unavailable: " + Error.Message);
                ReleaseNative();
            }
        }

        private void OnServerInitialized()
        {
            if (Initialized) return;
            Initialized = true;
            Loaded();
            if (Host == null) return;
            try
            {
                ExecutionResult Result = Host.Reload();
                Report("bootstrap", Result);
                if (Result.Status == RuntimeStatus.OK) Puts("[CarbonLuau:Runtime] Ready; generation=" + Host.Generation);
            }
            catch (Exception Error) { PrintError("[CarbonLuau:Runtime] Initialization failed: " + Error.Message); }
        }

        private void Report(string Chunk, ExecutionResult Result)
        {
            string Prefix = "[CarbonLuau:Runtime] [gen=" + Result.Generation + "][" + Chunk + "] ";
            // One bounded Carbon log call per execution; no script-to-managed callback.
            if (Result.Logs.Length != 0) Puts(Prefix + Result.Logs.TrimEnd('\n'));
            if (Result.LogTruncated) PrintWarning(Prefix + "output truncated at native log limit");
            if (Result.Status != RuntimeStatus.OK) PrintError(Prefix + Result.Status + ": " + Result.Error);
        }

        [ConsoleCommand("carbonluau.status"), AuthLevel(2)]
        private void StatusCommand(ConsoleSystem.Arg Arg)
        {
            try { Arg.ReplyWith(Host == null ? "CarbonLuau: unavailable\nReason: " + UnavailableReason : Host.Status()); }
            catch (Exception) { Arg.ReplyWith("CarbonLuau: unavailable\nReason: runtime context failure; see server log"); }
        }

        [ConsoleCommand("carbonluau.reload"), AuthLevel(2)]
        private void ReloadCommand(ConsoleSystem.Arg Arg)
        {
            if (Host == null) { Arg.ReplyWith("CarbonLuau: reload failed; " + UnavailableReason); return; }
            try
            {
                ExecutionResult Result = Host.Reload();
                Report("bootstrap", Result);
                Arg.ReplyWith("CarbonLuau: reload " + Result.Status + "; generation=" + Host.Generation);
            }
            catch (Exception Error)
            {
                PrintError("[CarbonLuau:Runtime] Reload failed: " + Error.Message);
                Arg.ReplyWith("CarbonLuau: reload failed; see server log");
            }
        }

        private void Unload() { ReleaseNative(); }

        private void ReleaseNative()
        {
            try
            {
                if (Host != null) { Host.Dispose(); Host = null; }
                if (Native == null) return;
                Native.Dispose();
                if (Native.UnloadError != null) PrintError("[CarbonLuau:Native] " + Native.UnloadError);
                else Puts("[CarbonLuau:Native] Native library unloaded successfully.");
                Native = null;
            }
            catch (Exception Error) { PrintError("[CarbonLuau:Native] Teardown failed; library retained for safety: " + Error.Message); }
        }
    }
}
