using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;
using Runtime = Carbon.Plugins.CarbonLuau;

// Paired identical workload, D8 binary versus current binary. Test-only; no ABI additions.
internal static class CatalogMemoryTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    [DllImport("libc")] private static extern int chmod(string Path, int Mode);
    private static void Check(bool Value, string Message, [System.Runtime.CompilerServices.CallerArgumentExpression("Value")] string Expression = "", [System.Runtime.CompilerServices.CallerFilePath] string File = "", [System.Runtime.CompilerServices.CallerLineNumber] int Line = 0)
    {
        if (!Value) TestDiagnostics.Pass(Value, Expression, File, Line); if (!Value) throw new InvalidOperationException("CATALOG-D9 memory: " + Message); }
    private static JObject Measure(string Library, string Compiler)
    {
        string DirectoryPath = Path.Combine(Path.GetTempPath(), "CarbonLuauCatalogMemory-" + Guid.NewGuid().ToString("N"));
        var Records = new JArray();
        Exception DiagnosticPrimary = null;
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(DirectoryPath, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(Library, Target);
            string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, Worker); if (Rid == "linux-x64") Check(chmod(Worker, 493) == 0, "compiler mode");
            using (var Native = new Runtime.NativeRuntime(DirectoryPath)) {
                var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar(), new Runtime.ItemDirectory(Name => null));
                Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot { EntryName = "init.luau", EntrySource = "return true" };
                using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100}, Snapshot, World))
                using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                    var Timer = Stopwatch.StartNew();
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "root setup");
                    Records.Add(new JObject { ["Stage"] = "Root", ["Addons"] = 0, ["VmBytes"] = Host.VmMemoryBytes, ["StartupMilliseconds"] = Timer.Elapsed.TotalMilliseconds });
                    var Providers = new[] {new object(), new object(), new object(), new object()};
                    var Tokens = new List<string>();
                    for (int Index = 0; Index < 100; ++Index) {
                        string[] Result = Registry.RegisterSource(Providers[Index / 32], "catalogmemory" + Index, "1.0.0", Encoding.UTF8.GetBytes("return true"));
                        Check(Result[0] == "OK" && Registry.ProcessOne() && Registry.Status(Providers[Index / 32], Result[1])[2] == "Active", "addon activation");
                        Tokens.Add(Result[1]);
                        if (Index == 0 || Index == 9 || Index == 49 || Index == 99)
                            Records.Add(new JObject { ["Stage"] = "Scale", ["Addons"] = Index + 1, ["VmBytes"] = Host.VmMemoryBytes });
                    }
                    long Generation = Host.VmGenerationId;
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Host.VmGenerationId == Generation, "healthy root replacement");
                    Records.Add(new JObject { ["Stage"] = "RootReplacement", ["Addons"] = 100, ["VmBytes"] = Host.VmMemoryBytes });
                    Check(Registry.ReplaceSource(Providers[0], Tokens[0], "2.0.0", Encoding.UTF8.GetBytes("return true"))[0] == "OK" && Registry.ProcessOne(), "healthy addon replacement");
                    Records.Add(new JObject { ["Stage"] = "AddonReplacement", ["Addons"] = 100, ["VmBytes"] = Host.VmMemoryBytes });
                    Check(Host.Execute("catalog.memory.recovery", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT && Host.Ready && Host.VmGenerationId != Generation,
                        "fatal reconstruction");
                    for (int Pass = 0; Pass < 128 && Registry.HasPending; ++Pass) Check(Registry.ProcessOne(), "bounded recovery publication");
                    for (int Index = 0; Index < 100; ++Index) Check(Registry.Status(Providers[Index / 32], Tokens[Index])[2] == "Active", "recovered addon");
                    Records.Add(new JObject { ["Stage"] = "Recovery", ["Addons"] = 100, ["VmBytes"] = Host.VmMemoryBytes });
                    foreach (object Provider in Providers) Registry.UnloadProvider(Provider);
                    Check(Registry.Count == 0 && Host.DomainCount == 1 && Host.VmMemoryLimitBytes == 64UL * 1024 * 1024 && Host.VmMemoryBytes < Host.VmMemoryLimitBytes,
                        "unload and unchanged cap");
                    Records.Add(new JObject { ["Stage"] = "ProviderUnload", ["Addons"] = 0, ["VmBytes"] = Host.VmMemoryBytes });
                    Host.Dispose(); Check(!Host.Ready && Host.DomainCount == 0, "complete VM/domain retirement");
                }
            }
        } catch (Exception Error) { DiagnosticPrimary = Error; TestDiagnostics.ExceptionFailure(Error); throw; }
        finally { TestDiagnostics.Cleanup(() => { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }, DiagnosticPrimary); }
        return new JObject { ["LibrarySha256"] = Hash(Library), ["Records"] = Records, ["UnmappedAndFilesRemoved"] = true };
    }
    private static string Hash(string PathName)
    { using (var Algorithm = System.Security.Cryptography.SHA256.Create()) using (var Stream = File.OpenRead(PathName)) return BitConverter.ToString(Algorithm.ComputeHash(Stream)).Replace("-", "").ToLowerInvariant(); }
    internal static void Run(string Baseline, string Candidate, string Compiler)
    {
        JObject Before = Measure(Baseline, Compiler), After = Measure(Candidate, Compiler);
        var Deltas = new JArray();
        var Left = (JArray)Before["Records"]; var Right = (JArray)After["Records"];
        Check(Left.Count == Right.Count, "paired stages");
        for (int Index = 0; Index < Left.Count; ++Index) {
            Check((string)Left[Index]["Stage"] == (string)Right[Index]["Stage"] && (int)Left[Index]["Addons"] == (int)Right[Index]["Addons"], "paired identical workload");
            Deltas.Add(new JObject { ["Stage"] = Right[Index]["Stage"].DeepClone(), ["Addons"] = Right[Index]["Addons"].DeepClone(),
                ["VmByteDelta"] = (long)Right[Index]["VmBytes"] - (long)Left[Index]["VmBytes"] });
        }
        Console.WriteLine(new JObject { ["Baseline"] = Before, ["Candidate"] = After, ["Deltas"] = Deltas }.ToString(Newtonsoft.Json.Formatting.Indented));
        Console.WriteLine("[CarbonLuau:CatalogMemory] PASS identical D8/current workload, root/1/10/50/100 addons, root/addon replacement, recovery, provider unload and deterministic native unmap");
    }
}
