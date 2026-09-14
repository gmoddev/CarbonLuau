using System;
using System.IO;
using System.Runtime.InteropServices;
using Loader = Carbon.Plugins.CarbonLuau.NativeLibraryLoader;

internal static class Program
{
    private static void Check(bool Condition, string Message)
    {
        if (!Condition) throw new Exception(Message);
    }

    private static void Expect(Action Action, string Message)
    {
        try { Action(); }
        catch (Exception Error)
        {
            if (Error.Message.Contains(Message)) return;
            throw;
        }
        throw new Exception("Expected failure: " + Message);
    }

    private static int Main(string[] Args)
    {
        try
        {
            if (Args.Length != 3) throw new Exception("Expected good, wrong-value and missing-symbol library paths");
            string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            try
            {
                Expect(() => Loader.GetRid(PlatformID.Win32NT, Architecture.Arm64), "unsupported platform");
                Expect(() => Loader.GetRid(PlatformID.Win32NT, Architecture.X86), "unsupported platform");
                Expect(() => Loader.GetRid(PlatformID.MacOSX, Architecture.X64), "unsupported platform");
                Check(Loader.GetRid(PlatformID.Win32NT, Architecture.X64) == "win-x64", "Windows RID");
                string Rid = Loader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
                string Target = Loader.GetLibraryPath(Root, Rid);
                Check(Path.IsPathRooted(Target) && Target.StartsWith(Root + Path.DirectorySeparatorChar), "Path must stay under data root");
                Check(Path.GetFileName(Loader.GetLibraryPath(Root, "win-x64")) == "carbonluau_native.dll", "DLL filename");
                Check(Path.GetFileName(Loader.GetLibraryPath(Root, "linux-x64")) == "libcarbonluau_native.so", "SO filename");
                Expect(() => Loader.GetLibraryPath(Root, "../escape"), "unsupported platform");
                Directory.CreateDirectory(Path.GetDirectoryName(Target));
                using (var Missing = new Loader()) Expect(() => Missing.Load(Root), "native library missing");
                for (int Fixture = 1; Fixture <= 3; Fixture++)
                {
                    if (Fixture == 3) File.WriteAllText(Target, "not a native library");
                    else File.Copy(Args[Fixture], Target, true);
                    using (var Failed = new Loader())
                    {
                        Expect(() => Failed.Load(Root), Fixture == 1 ? "probe magic mismatch" : Fixture == 2 ? "symbol missing" : "native library load failed");
                        Check(!Failed.Available, "Failed loader must be unavailable");
                        Failed.Dispose();
                        Check(Failed.UnloadError == null, "Failed-load handle cleanup");
                    }
                    File.Delete(Target);
                }
                for (int Cycle = 0; Cycle < 100; Cycle++)
                {
                    File.Copy(Args[0], Target);
                    var Instance = new Loader();
                    Instance.Load(Root);
                    Check(Instance.Available, "Probe should succeed");
                    Expect(() => Instance.Load(Root), "already attempted");
                    Instance.Dispose();
                    Instance.Dispose();
                    Check(!Instance.Available && Instance.UnloadError == null, "Idempotent unload");
                    Expect(() => Instance.Load(Root), "disposed");
                    // Windows denies this deletion if a loader reference remains.
                    File.Delete(Target);
                    if (Rid == "linux-x64")
                        Check(!File.ReadAllText("/proc/self/maps").Contains(Target), "Library still mapped after unload");
                }
            }
            finally { Directory.Delete(Root, true); }
            Console.WriteLine("[CarbonLuau:Test] PASS: platform/path checks, missing/broken/symbol/value failures, 100 managed load/unload cycles");
            return 0;
        }
        catch (Exception Error)
        {
            Console.Error.WriteLine("[CarbonLuau:Test] FAIL: " + Error);
            return 1;
        }
    }
}
