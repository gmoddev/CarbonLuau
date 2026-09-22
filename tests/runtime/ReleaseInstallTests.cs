using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class ReleaseInstallTests
{
    [DllImport("libc", SetLastError = true)] private static extern int chmod(string Path, int Mode);
    internal static void Run(string Bundle)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauInstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        try {
            ZipFile.ExtractToDirectory(Bundle, Root);
            string Data = Path.Combine(Root, "carbon", "data");
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            if (Rid == "linux-x64" && chmod(Path.Combine(Data, "CarbonLuau/native/linux-x64/carbonluau_compiler"), 493) != 0)
                throw new Exception("Cannot make installed compiler executable");
            if (!File.Exists(Path.Combine(Root, "carbon/plugins/CarbonLuau.cszip"))) throw new Exception("Missing production plugin");
            using (var Native = new Runtime.NativeRuntime(Data)) {
                int Count = 0;
                ulong Vm = Native.Create(new Runtime.RuntimeConfig());
                try {
                    foreach (string FileName in Directory.GetFiles(Path.Combine(Root, "examples"), "*.luau", SearchOption.AllDirectories)) {
                        // Compile every body without executing host-dependent top-level effects.
                        var Result = Native.Execute(Vm, "bundled-example", "return function(...)\n" + File.ReadAllText(FileName) + "\nend", 100);
                        if (Result.Status != Runtime.RuntimeStatus.OK) throw new Exception(FileName + ": " + Result.Error);
                        Count++;
                    }
                } finally { Native.Destroy(Vm); }
                if (Count == 0) throw new Exception("No bundled examples");
                GuiFoundation2FTests.RunNative(Native, Root);
                PlayerInteractionFoundation1FCTests.RunNative(Native, Root);
                using (var Host = new Runtime.RuntimeHost(Native, new Runtime.RuntimeConfig())) {
                    if (Host.Reload().Status != Runtime.RuntimeStatus.OK || Host.Execute("installed", "return 42").Number != 42)
                        throw new Exception("Installed runtime/bootstrap smoke failed");
                }
                if (Native.LiveVmCount != 0) throw new Exception("Installed runtime leaked VMs");
                Console.WriteLine("[CarbonLuau:ReleaseInstall] PASS clean bundle load, compile " + Count + " examples, GUI/Player execution and teardown");
            }
        } finally { Directory.Delete(Root, true); }
    }
}
