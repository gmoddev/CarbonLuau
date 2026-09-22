using CarbonLuau.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CarbonLuau.Tooling;

internal static class Program
{
    internal static int Main(string[] Args)
    {
        try {
            if (OperatingSystem.IsWindows()) SetErrorMode(0x8003);
            if (Args.Length == 1 && Args[0] == "--stdio") return new Host().Run(Console.OpenStandardInput(), Console.OpenStandardOutput());
            if (Args.Length == 1 && Args[0] == "--preview-worker") return PreviewWorker.Run(Console.OpenStandardInput(), Console.OpenStandardOutput());
            if (Args.Length == 1 && Args[0] == "--analysis-stdio") {
                using var Supervisor = new AnalysisSupervisor();
                return Supervisor.Run(Console.OpenStandardInput(), Console.OpenStandardOutput()).GetAwaiter().GetResult();
            }
            if (Args.Length == 2 && (Args[0] == "--generate" || Args[0] == "--check-generated")) {
                Generate(Path.GetFullPath(Args[1]), Args[0] == "--check-generated");
                return 0;
            }
            Console.Error.WriteLine("[CarbonLuau:Tooling] Expected --stdio, --generate <repository> or --check-generated <repository>.");
            return 2;
        } catch (Exception Error) {
            Console.Error.WriteLine("[CarbonLuau:Tooling] " + AddonPolicy.Diagnostic(Error.Message));
            return 1;
        }
    }
    internal static void Generate(string Root, bool Check)
    {
        var Release = JObject.Parse(File.ReadAllText(Path.Combine(Root, "release.json")));
        string Source = File.ReadAllText(Path.Combine(Root, "scripts", "bootstrap.luau"));
        JObject Catalog = ApiCatalog.Build(Source, Release, File.ReadAllText(Path.Combine(Root, "native", "src", "scripts", "ModuleLoader.cpp")));
        var Outputs = new SortedDictionary<string, string>(StringComparer.Ordinal) {
            ["api/carbonluau-api.json"] = Json(Catalog),
            ["generated/carbonluau.d.luau"] = ApiArtifacts.Definitions(Catalog),
            ["generated/carbonluau-docs.json"] = Json(ApiArtifacts.Documentation(Catalog)),
            ["generated/tooling-metadata.json"] = Json(new JObject { ["SchemaVersion"] = 1, ["Api"] = Catalog["Api"]!.DeepClone(),
                ["PackageSchema"] = Release["packageSchema"]!.DeepClone(), ["RuntimeLuauRevision"] = Release["luauRevision"]!.DeepClone(),
                ["Limits"] = ApiCatalog.Limits() })
        };
        foreach (var Output in Outputs) {
            string Target = Path.Combine(Root, Output.Key);
            if (Check) {
                if (!File.Exists(Target) || !File.ReadAllBytes(Target).SequenceEqual(new UTF8Encoding(false, true).GetBytes(Output.Value)))
                    throw new InvalidOperationException("Generated artifact differs: " + Output.Key);
            } else {
                Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
                File.WriteAllText(Target, Output.Value, new UTF8Encoding(false, true));
            }
        }
    }
    internal static string Json(JToken Value) => Value.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint Value);
}
