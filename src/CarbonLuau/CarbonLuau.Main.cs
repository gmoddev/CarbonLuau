using System;

namespace Carbon.Plugins
{
    [Info("CarbonLuau", "gmoddev", "0.3.0")]
    [Description("Experimental bounded Luau gameplay facade")]
    public partial class CarbonLuau : CarbonPlugin
    {
        private NativeRuntime Native;
        private ScriptHost Host;
        private RuntimeConfig Settings;
        private bool Attempted, Initialized;
        private bool Stopping, DrainScheduled;
        private bool TeardownPending;
        private long ErrorWindow;
        private int ErrorCount;
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
                InitializeGameplay();
                Host = new ScriptHost(Native, Settings, () => ScriptSnapshot.Load(Oxide.Core.Interface.Oxide.DataDirectory, Settings), Gameplay);
                Puts("[CarbonLuau:Native] Native probe loaded successfully. Platform: " + Native.Rid + "; ABI: 1.2");
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
                SeedPlayers();
                ExecutionResult Result = Host.Reload();
                if (Result.Status == RuntimeStatus.OK) RegisterActivePermissions();
                Report("bootstrap", Result);
                if (Result.Status == RuntimeStatus.OK) Puts("[CarbonLuau:Runtime] Ready; generation=" + Host.Generation);
                RequestDrain();
            }
            catch (Exception Error) { PrintError("[CarbonLuau:Runtime] Initialization failed: " + Error.Message); }
            finally { if (TeardownPending) ReleaseNative(); }
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
            if (RegisteringPermissions) { Arg.ReplyWith("CarbonLuau: reload rejected during permission publication"); return; }
            if (Host == null) { Arg.ReplyWith("CarbonLuau: reload failed; " + UnavailableReason); return; }
            try
            {
                ExecutionResult Result = Host.Reload();
                if (Result.Status == RuntimeStatus.OK) RegisterActivePermissions();
                Report("bootstrap", Result);
                RequestDrain();
                Arg.ReplyWith("CarbonLuau: reload " + Result.Status + "; generation=" + Host.Generation);
            }
            catch (Exception Error)
            {
                PrintError("[CarbonLuau:Runtime] Reload failed: " + Error.Message);
                Arg.ReplyWith("CarbonLuau: reload failed; see server log");
            }
            finally { if (TeardownPending) ReleaseNative(); }
        }

        private void Unload() { ReleaseNative(); }

        private void ReleaseNative()
        {
            Stopping = true;
            if ((Host != null && Host.Busy) || RegisteringPermissions) {
                if (Host != null) Host.RequestStop();
                TeardownPending = true;
                return;
            }
            TeardownPending = false;
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

        private void RequestDrain()
        {
            if (Stopping || DrainScheduled || Host == null || Host.Busy || !Host.HasWork) return;
            DrainScheduled = true;
            ScriptHost ExpectedHost = Host;
            long ExpectedGeneration = Host.Generation;
            NextFrame(() => {
                DrainScheduled = false;
                if (Stopping || Host != ExpectedHost) return;
                try {
                    if (Host.Generation == ExpectedGeneration) {
                        int Logged = 0;
                        foreach (ExecutionResult Result in Host.Drain()) {
                            if (Result.Status != RuntimeStatus.OK) {
                                long Now = System.Diagnostics.Stopwatch.GetTimestamp();
                                if (Now - ErrorWindow >= System.Diagnostics.Stopwatch.Frequency * 60) { ErrorWindow = Now; ErrorCount = 0; }
                                if (ErrorCount >= 5) {
                                    if (ErrorCount == 5) { ErrorCount = 6; PrintWarning("[CarbonLuau:Scheduler] Further callback errors suppressed for this 60-second window; see status counters."); }
                                    continue;
                                }
                                ErrorCount++;
                            }
                            if (Logged++ < 8) Report("scheduler", Result);
                        }
                        RegisterActivePermissions();
                        if (Logged > 8) PrintWarning("[CarbonLuau:Scheduler] Drain output limited to eight records.");
                    }
                    RequestDrain();
                } catch (Exception) {
                    PrintError("[CarbonLuau:Scheduler] Drain stopped after host-context failure; reload the plugin.");
                    Stopping = true;
                } finally { if (TeardownPending) ReleaseNative(); }
            });
        }
    }
}
