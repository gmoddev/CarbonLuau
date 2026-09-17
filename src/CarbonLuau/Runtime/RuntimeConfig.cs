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
        public sealed class RuntimeConfig
        {
            public bool Enabled = true;
            public int MaxVmMemoryMiB = 64;
            public int MaxCallbackMilliseconds = 3;
            public string ScriptRoot = "scripts", EntryScript = "init.luau", ModuleRoot = "modules";
            public int FrameDrainBudgetMilliseconds = 5, MaxQueuedCallbacks = 4096;

            public RuntimeConfig Validate()
            {
                return new RuntimeConfig {
                    Enabled = Enabled,
                    MaxVmMemoryMiB = Math.Max(16, Math.Min(256, MaxVmMemoryMiB)),
                    MaxCallbackMilliseconds = Math.Max(1, Math.Min(100, MaxCallbackMilliseconds)),
                    ScriptRoot = ScriptRoot, EntryScript = EntryScript, ModuleRoot = ModuleRoot,
                    FrameDrainBudgetMilliseconds = Math.Max(1, Math.Min(20, FrameDrainBudgetMilliseconds)),
                    MaxQueuedCallbacks = Math.Max(1, Math.Min(4096, MaxQueuedCallbacks))
                };
            }
        }

        public enum RuntimeStatus
        {
            OK = 0, YIELDED = 1, RUNTIME_ERROR = 2, COMPILE_ERROR = 3,
            MEMORY_LIMIT = 4, TIMEOUT = 5, INVALID_ARGUMENT = 6, INTERNAL_ERROR = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VmConfig { public ulong MemoryLimitBytes; }
        [StructLayout(LayoutKind.Sequential)]
        public struct VmInfo { public ulong MemoryBytes, MemoryLimitBytes, Ready; }
        [StructLayout(LayoutKind.Sequential)]
        public struct VmGenerationInfo { public ulong VmGenerationId, Domains; }
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeResult
        {
            public double Number;
            public uint HasNumber, Flags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)] public byte[] Error;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)] public byte[] Logs;
        }

        public sealed class ExecutionResult
        {
            public RuntimeStatus Status;
            public long Generation;
            public long VmGenerationId, DomainLifetimeId;
            public string Error = "", Logs = "";
            public bool Retired, LogTruncated, HasNumber;
            public double Number;
            public static ExecutionResult FromNative(RuntimeStatus Status, NativeResult Value)
            {
                if (!Enum.IsDefined(typeof(RuntimeStatus), Status)) Status = RuntimeStatus.INTERNAL_ERROR;
                return new ExecutionResult { Status = Status, Error = Decode(Value.Error), Logs = Decode(Value.Logs),
                    Retired = (Value.Flags & 1) != 0, LogTruncated = (Value.Flags & 2) != 0,
                    HasNumber = Value.HasNumber != 0, Number = Value.Number };
            }
            private static string Decode(byte[] Bytes)
            {
                if (Bytes == null) return "";
                int Length = Array.IndexOf(Bytes, (byte)0);
                return Encoding.UTF8.GetString(Bytes, 0, Length < 0 ? Bytes.Length : Length);
            }
        }

        // Owns the loaded library and all ABI bindings. No finalizer: destruction
    }
}

