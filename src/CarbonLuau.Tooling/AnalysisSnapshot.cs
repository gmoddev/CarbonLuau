using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

// Immutable per revision. Workspace names never become physical path components.
internal sealed class AnalysisSnapshot : IDisposable
{
    internal string Root { get; }
    internal Dictionary<string, string> Uris { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, string> Sources { get; } = new(StringComparer.Ordinal);
    internal JArray Withheld { get; } = [];
    private readonly Dictionary<string, string> Files = new(StringComparer.Ordinal);
    internal static string Key(string Folder, string Path) => Folder + "\0" + Path;
    internal AnalysisSnapshot(JObject Input, JObject Inspection)
    {
        Root = Path.Combine(Path.GetTempPath(), "carbonluau-analysis-" + Guid.NewGuid().ToString("N"));
        CheckAncestors(Path.GetDirectoryName(Root)!);
        Directory.CreateDirectory(Root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            var Excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject Folder in (JArray)Input["Folders"]!) foreach (JObject File in (JArray)Folder["Files"]!) {
                string Name = (string)File["Path"]!, Id = Key((string)Folder["Id"]!, Name);
                if (!Name.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) || File["Text"] == null) continue;
                string Text = (string)File["Text"]!;
                Sources.Add(Id, Text);
                bool Config = Name.Split('/').Any(Part => Part.Equals(".config.luau", StringComparison.OrdinalIgnoreCase) || Part.Equals(".vscode", StringComparison.OrdinalIgnoreCase));
                if (Config || Protocol.Utf8.GetByteCount(Text) > 65536) { Excluded.Add(Id); continue; }
                JObject Parsed = NativeAnalysis.Parse(Text);
                if ((bool?)Parsed["UnsafeRequire"] != false || ((JArray)Parsed["Errors"]!).Count != 0) { Excluded.Add(Id); continue; }
                var Mappings = ((JArray)Inspection["Imports"]!).Cast<JObject>().Where(Map => Key((string)Map["Folder"]!, (string)Map["Path"]!) == Id).ToArray();
                if (Mappings.Length != ((JArray)Parsed["Requires"]!).Count) Excluded.Add(Id);
            }
            // Invalid package/path admission withholds the affected project, even if diagnostics were capped.
            foreach (JObject Project in (JArray)Inspection["Projects"]!) {
                if ((string?)Project["Kind"] != "Addon" || (bool?)Project["ValidPackage"] == true) continue;
                string Prefix = Key((string)Project["Folder"]!, (string)Project["Prefix"]!);
                foreach (string Id in Sources.Keys.Where(Id => Id.StartsWith(Prefix, StringComparison.Ordinal))) Excluded.Add(Id);
            }
            bool Changed;
            do {
                Changed = false;
                foreach (JObject Map in (JArray)Inspection["Imports"]!) {
                    string Target = Key((string)Map["TargetFolder"]!, (string)Map["TargetPath"]!);
                    if (!Sources.ContainsKey(Target) || Excluded.Contains(Target))
                        Changed |= Excluded.Add(Key((string)Map["Folder"]!, (string)Map["Path"]!));
                }
            } while (Changed);
            foreach (var Source in Sources) {
                if (Excluded.Contains(Source.Key)) { Withheld.Add(Source.Key); continue; }
                string File = Path.Combine(Root, "m" + Convert.ToHexStringLower(SHA256.HashData(Protocol.Utf8.GetBytes(Source.Key))) + ".luau");
                Files.Add(Source.Key, File); Uris.Add(Source.Key, new Uri(File).AbsoluteUri);
                System.IO.File.WriteAllText(File, Source.Value, Protocol.Utf8);
            }
            System.IO.File.WriteAllText(Path.Combine(Root, "base.json"), "{\"languageMode\":\"strict\"}", Protocol.Utf8);
            System.IO.File.WriteAllText(Path.Combine(Root, "adapter.luau"), Transform(Inspection), Protocol.Utf8);
            System.IO.File.WriteAllText(Path.Combine(Root, "settings.json"), Program.Json(Settings()), Protocol.Utf8);
            Check();
        } catch { Dispose(); throw; }
    }
    internal JObject Settings() => JObject.FromObject(new {
        platform = new { type = "standard" }, sourcemap = new { enabled = false }, index = new { enabled = false },
        diagnostics = new { workspace = false, includeDependents = false }, completion = new { imports = new { enabled = false } },
        require = new { useOriginalRequireByStringSemantics = true },
        plugins = new { enabled = true, paths = new[] { Path.Combine(Root, "adapter.luau") }, timeoutMs = 1000, fileSystem = new { enabled = false } },
        types = new { roblox = false, definitionFiles = new { }, documentationFiles = Array.Empty<string>() },
        fflags = new { enableNewSolver = true, sync = false, @override = new Dictionary<string, string> { ["LuauSolverV2"] = "true", ["DebugLuauTypeFunctionRuntimeHeapLimit"] = "67108864" } }
    });
    private string Transform(JObject Inspection)
    {
        var Builder = new StringBuilder("-- CarbonLuau pack-owned analysis adapter revision 2\nlocal Files = {\n");
        foreach (var File in Files) {
            var Edits = new List<string>();
            foreach (JObject Map in (JArray)Inspection["Imports"]!) {
                if (Key((string)Map["Folder"]!, (string)Map["Path"]!) != File.Key) continue;
                string Target = Files[Key((string)Map["TargetFolder"]!, (string)Map["TargetPath"]!)];
                string Relative = "./" + Path.GetFileNameWithoutExtension(Target);
                Edits.Add($"{{startLine={(int)Map["Line"]! + 1},startColumn={(int)Map["Column"]! + 1},endLine={(int)Map["EndLine"]! + 1},endColumn={(int)Map["EndColumn"]! + 1},newText={Quote(Quote(Relative))}}}");
            }
            string Name = File.Value.Replace('\\', '/');
            if (OperatingSystem.IsWindows()) Name = char.ToLowerInvariant(Name[0]) + Name[1..];
            Builder.Append('[').Append(Quote(Name)).Append("]={Source=").Append(Quote(Sources[File.Key])).Append(",Edits={").AppendJoin(',', Edits).Append("}},\n");
            if (Builder.Length > 32 * 1024 * 1024) throw new ProtocolError("InputLimit", "Analysis transform exceeds 32 MiB.");
        }
        Builder.Append("}\nreturn {transformSource=function(Source, Context)\nlocal File = Files[string.gsub(Context.filePath, \"\\\\\", \"/\")]\nif File and File.Source == Source then return File.Edits end\nreturn nil\nend}\n");
        return Builder.ToString();
    }
    private static string Quote(string Text) => "\"" + string.Concat(Protocol.Utf8.GetBytes(Text).Select(Byte => "\\" + Byte.ToString("D3", System.Globalization.CultureInfo.InvariantCulture))) + "\"";
    internal void Check() => CheckAncestors(Root);
    private static void CheckAncestors(string DirectoryPath)
    {
        for (DirectoryInfo? Directory = new(DirectoryPath); Directory != null; Directory = Directory.Parent) {
            if ((Directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new ProtocolError("UnsafeEnvironment", "Analysis ancestry contains a link or reparse point.");
            foreach (string Name in new[] { ".config.luau", ".luaurc", ".robloxrc" })
                if (System.IO.File.Exists(Path.Combine(Directory.FullName, Name)) || System.IO.Directory.Exists(Path.Combine(Directory.FullName, Name)))
                    throw new ProtocolError("UnsafeEnvironment", "Unexpected Luau configuration in analysis ancestry; static tooling remains available.");
        }
    }
    public void Dispose()
    {
        // Root is generated here, never derived from a workspace path or protocol field.
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
}
