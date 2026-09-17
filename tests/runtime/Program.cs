using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class Program
{
    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception(Message); }
    private static int Main(string[] Args)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauRuntime-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(Marshal.SizeOf(typeof(Runtime.NativeResult)) == 6160 && Marshal.SizeOf(typeof(Runtime.VmInfo)) == 24, "ABI layout");
            var Default = new Runtime.RuntimeConfig();
            Check(Default.Enabled && Default.MaxVmMemoryMiB == 64 && Default.MaxCallbackMilliseconds == 3, "defaults");
            var Low = new Runtime.RuntimeConfig { MaxVmMemoryMiB = int.MinValue, MaxCallbackMilliseconds = int.MinValue }.Validate();
            var High = new Runtime.RuntimeConfig { MaxVmMemoryMiB = int.MaxValue, MaxCallbackMilliseconds = int.MaxValue }.Validate();
            Check(Low.MaxVmMemoryMiB == 16 && Low.MaxCallbackMilliseconds == 1 && High.MaxVmMemoryMiB == 256 && High.MaxCallbackMilliseconds == 100, "clamps");
            Check(Runtime.ExecutionResult.FromNative((Runtime.RuntimeStatus)99, new Runtime.NativeResult()).Status == Runtime.RuntimeStatus.INTERNAL_ERROR, "unknown native status");
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Library = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Library));
            File.Copy(Args[0], Library);
            using (var Native = new Runtime.NativeRuntime(Root))
            {
                Check(Native.Revision == "c6b830185af962c82003f86784e2fe036357c830", "native Luau pin");
                using (var OtherHostLifetime = new Runtime.NativeRuntime(Root))
                    Check(Native.HostLifetimeId > 0 && OtherHostLifetime.HostLifetimeId > Native.HostLifetimeId, "distinct CarbonLuau host lifetimes");
                using (var Disabled = new Runtime.RuntimeHost(Native, new Runtime.RuntimeConfig { Enabled = false }))
                    Check(Disabled.Reload().Status == Runtime.RuntimeStatus.INVALID_ARGUMENT && Native.LiveVmCount == 0, "disabled");
                using (var Partial = new Runtime.RuntimeHost(Native, Default)) { }
                Check(Native.LiveVmCount == 0, "unload before initialization");
                using (var Host = new Runtime.RuntimeHost(Native, new Runtime.RuntimeConfig { MaxVmMemoryMiB = 16 }))
                {
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Host.Generation == 1, "initial bootstrap");
                    Check(Host.Status().Contains("CarbonLuau: ready") && Host.Status().Contains("16 MiB"), "status");
                    for (int Cycle = 0; Cycle < 100; Cycle++)
                    {
                        long Previous = Host.Generation;
                        int Destroyed = Native.DestroyedVmCount;
                        Check(Host.Reload("print('discard me'); local =").Status == Runtime.RuntimeStatus.COMPILE_ERROR, "failed reload classification");
                        Check(Host.Generation == Previous && Native.LiveVmCount == 1 && Native.DestroyedVmCount == Destroyed + 1, "failed candidate destroyed; old retained");
                        var Staged = Host.Reload("print('discard me'); error('reject candidate')");
                        Check(Staged.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Staged.Logs == "" && Host.Generation == Previous, "runtime candidate rollback and log staging");
                        Check(Host.Execute("valid", "return 3").Number == 3, "healthy after rejection");
                        Destroyed = Native.DestroyedVmCount;
                        Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Host.Generation == Previous + 1 && Native.LiveVmCount == 1 && Native.DestroyedVmCount == Destroyed + 1, "atomic replacement exactly-once destruction");
                    }
                    Check(Host.Execute("runtime", "error('intentional runtime failure')").Status == Runtime.RuntimeStatus.RUNTIME_ERROR, "runtime map");
                    Check(Host.Execute("memory", "return buffer.create(16777217)").Status == Runtime.RuntimeStatus.MEMORY_LIMIT, "memory map");
                    Check(Host.Execute("valid", "return 3").Number == 3, "post-memory recovery");
                    long BeforeTimeout = Host.Generation;
                    Check(Host.Execute("timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT && Host.Generation == BeforeTimeout + 1, "timeout retirement and replacement");
                    Check(Host.Execute("valid", "return 3").Number == 3 && Native.LiveVmCount == 1, "post-timeout recovery");
                    bool Rejected = false;
                    var Other = new Thread(() => { try { Host.Execute("wrongthread", "return 3"); } catch (InvalidOperationException) { Rejected = true; } });
                    Other.Start(); Other.Join(); Check(Rejected, "managed affinity");
                    Host.Dispose(); Host.Dispose();
                    Check(Native.LiveVmCount == 0, "host idempotent teardown");
                }
                var Generation = new Runtime.RuntimeGeneration(Native, 999, Default);
                Generation.Dispose(); Generation.Dispose();
                Check(Generation.Execute("stale", "return 3", 3).Status == Runtime.RuntimeStatus.INVALID_ARGUMENT, "stale managed generation");
                ScriptTests.Run(Native, Root);
                FacadeTests.Run(Native, Args.Length > 3 ? Args[3] : null);
                AddonTests.Run(Native, Args.Length > 3 ? Args[3] : null);
                FoundationETests.Run(Native);
                Native.Dispose(); Native.Dispose();
            }
            File.Delete(Library);
            for (int Fixture = 1; Fixture <= 2; Fixture++)
            {
                File.Copy(Args[Fixture], Library);
                bool Rejected = false;
                try { using (var BadNative = new Runtime.NativeRuntime(Root)) { } }
                catch (InvalidOperationException Error)
                {
                    Rejected = Error.Message.Contains(Fixture == 1 ? "ABI major mismatch" : "symbol missing");
                }
                Check(Rejected, "incompatible/legacy native rejected before VM creation");
                File.Delete(Library);
            }
            Console.WriteLine("[CarbonLuau:ManagedTest] PASS: real native ABI; configuration; 100 atomic replacements and 200 failed candidates; timeout/memory recovery; ownership/affinity/partial teardown");
            return 0;
        }
        catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        finally { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
