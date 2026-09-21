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
        if (Args.Length >= 1 && Args[0] == "--gui-only") {
            try { GuiFoundation1ATests.Run(Args.Length > 1 ? Args[1] : null); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui1b-only") {
            try { GuiFoundation1BTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui1c-only") {
            try { GuiFoundation1CTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui1d-only") {
            try { GuiFoundation1DTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui1e-only") {
            try { GuiFoundation1ETests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui1f-only") {
            try { GuiFoundation1FTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui2a-only") {
            try { GuiFoundation2ATests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui2b-only") {
            try { GuiFoundation2BTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui2c-only") {
            try { GuiFoundation2CTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui2e-only") {
            try { GuiFoundation2ETests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui2f-only") {
            try { GuiFoundation2FTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui3a-only") {
            try { GuiFoundation3ATests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui3b-only") {
            try { GuiFoundation3BTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui3c-only") {
            try { GuiFoundation3CTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        if (Args.Length >= 1 && Args[0] == "--gui3d-only") {
            try { GuiFoundation3DTests.RunModel(); return 0; }
            catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:ManagedTest] FAIL: " + Error); return 1; }
        }
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauRuntime-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(Marshal.SizeOf(typeof(Runtime.NativeResult)) == 6160 && Marshal.SizeOf(typeof(Runtime.VmInfo)) == 24, "ABI layout");
            var Default = new Runtime.RuntimeConfig();
            Check(Default.Enabled && Default.MaxVmMemoryMiB == 64 && Default.MaxCallbackMilliseconds == 3, "defaults");
            GuiFoundation1ATests.Run(Args.Length > 3 ? Args[3] : null);
            GuiFoundation1BTests.RunModel();
            GuiFoundation1CTests.RunModel();
            GuiFoundation1DTests.RunModel();
            GuiFoundation1ETests.RunModel();
            GuiFoundation1FTests.RunModel();
            GuiFoundation2ATests.RunModel();
            GuiFoundation2BTests.RunModel();
            GuiFoundation2CTests.RunModel();
            GuiFoundation2ETests.RunModel();
            GuiFoundation2FTests.RunModel();
            GuiFoundation3ATests.RunModel();
            GuiFoundation3BTests.RunModel();
            GuiFoundation3CTests.RunModel();
            GuiFoundation3DTests.RunModel();
            var Low = new Runtime.RuntimeConfig { MaxVmMemoryMiB = int.MinValue, MaxCallbackMilliseconds = int.MinValue }.Validate();
            var High = new Runtime.RuntimeConfig { MaxVmMemoryMiB = int.MaxValue, MaxCallbackMilliseconds = int.MaxValue }.Validate();
            Check(Low.MaxVmMemoryMiB == 16 && Low.MaxCallbackMilliseconds == 1 && High.MaxVmMemoryMiB == 256 && High.MaxCallbackMilliseconds == 100, "clamps");
            Check(Runtime.ExecutionResult.FromNative((Runtime.RuntimeStatus)99, new Runtime.NativeResult()).Status == Runtime.RuntimeStatus.INTERNAL_ERROR, "unknown native status");
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Library = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Library));
            File.Copy(Args[0], Library);
            string Compiler = Path.Combine(Path.GetDirectoryName(Library), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Args[4], Compiler);
            if (Rid == "linux-x64") Check(chmod(Compiler, 493) == 0, "compiler worker executable mode");
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
                GuiFoundation1BTests.RunNative(Native);
                GuiFoundation1CTests.RunNative(Native);
                GuiFoundation1DTests.RunNative(Native);
                GuiFoundation1ETests.RunNative(Native);
                GuiFoundation1FTests.RunNative(Native);
                GuiFoundation2ATests.RunNative(Native);
                GuiFoundation2BTests.RunNative(Native);
                GuiFoundation2CTests.RunNative(Native);
                GuiFoundation2ETests.RunNative(Native);
                GuiFoundation2FTests.RunNative(Native, Args.Length > 3 ? Args[3] : ".");
                GuiFoundation3ATests.RunNative(Native, Args.Length > 3 ? Args[3] : ".");
                GuiFoundation3BTests.RunNative(Native);
                GuiFoundation3CTests.RunNative(Native);
                GuiFoundation3DTests.RunNative(Native);
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

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string Path, uint Mode);
}
