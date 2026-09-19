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
        public sealed class RuntimeHost : IDisposable
        {
            public const string Smoke = "print('hello from Luau'); assert(1 + 2 == 3); return 3";
            private readonly NativeRuntime Native;
            private readonly RuntimeConfig Settings;
            private bool Disposed;
            private RuntimeGeneration Current;
            public long Generation { get { return Current == null ? 0 : Current.Number; } }
            public string Reason { get; private set; }
            public RuntimeHost(NativeRuntime Native, RuntimeConfig Config)
            {
                this.Native = Native;
                Settings = Config.Validate();
                Reason = Settings.Enabled ? "not initialized" : "disabled by configuration";
            }
            public ExecutionResult Reload(string SmokeSource = Smoke)
            {
                Native.CheckOwner();
                if (Disposed || !Settings.Enabled) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT, Error = Reason };
                RuntimeGeneration Candidate = null;
                try
                {
                    Candidate = new RuntimeGeneration(Native, checked(Generation + 1), Settings);
                    ExecutionResult Result = Candidate.Execute("bootstrap", SmokeSource, Settings.MaxCallbackMilliseconds);
                    Result.Generation = Candidate.Number;
                    if (Result.Status != RuntimeStatus.OK || !Result.HasNumber || Result.Number != 3)
                    {
                        if (Result.Status == RuntimeStatus.OK) { Result.Status = RuntimeStatus.RUNTIME_ERROR; Result.Error = "bootstrap must return 3"; }
                        Result.Logs = ""; // Candidate output is staged until commit.
                        Reason = "replacement rejected: " + Result.Status;
                        return Result;
                    }
                    RuntimeGeneration Previous = Current;
                    Current = Candidate;
                    Candidate = null;
                    Reason = null;
                    if (Previous != null) Previous.Dispose();
                    return Result;
                }
                catch (Exception Error)
                {
                    Reason = "replacement initialization failed";
                    return new ExecutionResult { Status = RuntimeStatus.INTERNAL_ERROR, Error = Error.Message };
                }
                finally { if (Candidate != null) Candidate.Dispose(); }
            }
            public ExecutionResult Execute(string Chunk, string Source)
            {
                Native.CheckOwner();
                if (Disposed || Current == null) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT, Error = Reason };
                ExecutionResult Result = Current.Execute(Chunk, Source, Settings.MaxCallbackMilliseconds);
                Result.Generation = Current.Number;
                if (Result.Retired || Current.Info.Ready == 0)
                {
                    // The original failure is returned intact. No retry of user
                    // source. Recovery only executes our deterministic bootstrap.
                    ExecutionResult Recovery = Reload();
                    Result.Error += "; recovery=" + Recovery.Status + "; generation=" + Generation;
                }
                return Result;
            }
            public string Status()
            {
                Native.CheckOwner();
                bool Ready = !Disposed && Current != null && Current.Info.Ready != 0;
                VmInfo Info = Ready ? Current.Info : new VmInfo();
                return "CarbonLuau: " + (Ready ? "ready" : "unavailable")
                    + (Ready ? "" : "\nReason: " + (Reason ?? "runtime retired"))
                    + "\nGeneration: " + Generation + "\nNative ABI: 1.0 OK\nLuau: " + Native.Revision
                    + "\nVM memory: " + (Info.MemoryBytes / 1048576.0).ToString("F3", CultureInfo.InvariantCulture)
                    + " / " + Settings.MaxVmMemoryMiB + " MiB\nCallback deadline: " + Settings.MaxCallbackMilliseconds + " ms";
            }
            public void Dispose()
            {
                if (Disposed) return;
                Native.CheckOwner();
                Disposed = true;
                RuntimeGeneration Previous = Current;
                Current = null;
                Reason = "unloaded";
                if (Previous != null) Previous.Dispose();
            }
        }
    }
}

