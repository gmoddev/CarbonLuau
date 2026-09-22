using System.IO.Compression;
using CarbonLuau.Core;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal sealed class ProjectModel
{
    private sealed record Source(string Folder, string Path, string Text);
    private sealed record Project(string Id, string Folder, string Prefix, string Kind, List<Source> Sources, AddonPackageSnapshot? Package);
    private readonly List<Project> Projects = [];
    private readonly JArray Diagnostics = [], Imports = [];
    private const int MaxDiagnostics = 256;
    // Reuses the same bounded admission and package parser as static inspection.
    // Returned sources are in memory; the worker performs no workspace filesystem reads.
    internal (PreviewProject Selected, List<PreviewProject> Activation) Preview(JObject Input, string ProjectId, string? Entry)
    {
        Inspect(Input);
        Project Selected = Projects.SingleOrDefault(Value => Value.Id == ProjectId)
            ?? throw new ProtocolError("InvalidPreviewEntry", "Preview project is absent from the admitted snapshot.");
        var Available = Projects.Where(Value => Value.Package != null)
            .GroupBy(Value => Value.Package!.Id, StringComparer.Ordinal).ToDictionary(Group => Group.Key, Group => Group.ToArray(), StringComparer.Ordinal);
        var Closure = new Dictionary<string, Project>(StringComparer.Ordinal);
        void Visit(Project Value) {
            if (Value.Package == null) return;
            if (!Closure.TryAdd(Value.Package.Id, Value)) return;
            if (Closure.Count > AddonPolicy.MaxRegistrations) throw new ProtocolError("InputLimit", "Preview dependency count exceeds the canonical registration limit.");
            foreach (string Id in Value.Package.Dependencies(false).Concat(Value.Package.Dependencies(true))) {
                if (Available.TryGetValue(Id, out var Matches)) {
                    if (Matches.Length != 1) throw new ProtocolError("ModuleError", "Preview dependency is ambiguous: " + Id);
                    Visit(Matches[0]);
                } else if (Value.Package.Dependencies(false).Contains(Id, StringComparer.Ordinal))
                    throw new ProtocolError("ModuleError", "Required preview dependency has no supplied local package: " + Id);
            }
        }
        PreviewProject ConvertProject(Project Value, string? SelectedEntry) {
            if (Value.Kind == "Addon" && Value.Package == null) throw new ProtocolError("InvalidProject", "Preview addon failed canonical package admission.");
            if (Value.Package != null) {
                PackageSources Sources = Value.Package.GetSources();
                if (SelectedEntry != null && SelectedEntry != "init.luau") {
                    ModulePath.Validate(SelectedEntry, true);
                    if (!Sources.Modules.TryGetValue(SelectedEntry[..^5], out string? Source)) throw new ProtocolError("InvalidPreviewEntry", "Selected addon entry is absent.");
                    Sources = new PackageSources(Source, Sources.Modules);
                }
                return new(Value.Id, SelectedEntry ?? "init.luau", Sources, Value.Package);
            }
            string EntryName = SelectedEntry ?? "init.luau";
            ModulePath.Validate(EntryName, true);
            Source EntrySource = Value.Sources.SingleOrDefault(File => File.Path == EntryName)
                ?? throw new ProtocolError("InvalidPreviewEntry", "Select an existing Luau entry in this project.");
            var Modules = new SortedDictionary<string, string>(StringComparer.Ordinal);
            int Total = Protocol.Utf8.GetByteCount(EntrySource.Text);
            if (Total > AddonPolicy.MaxSourceBytes || EntrySource.Text.Contains('\0')) throw new ProtocolError("InputLimit", "Preview entry violates canonical source bounds.");
            foreach (Source File in Value.Sources.Where(File => File.Path.StartsWith("modules/", StringComparison.Ordinal))) {
                string Path = File.Path[8..]; ModulePath.Validate(Path, true);
                int Size = Protocol.Utf8.GetByteCount(File.Text); Total = checked(Total + Size);
                if (Size > AddonPolicy.MaxSourceBytes || File.Text.Contains('\0') || Total > AddonPolicy.MaxAggregateSourceBytes || Modules.Count >= AddonPolicy.MaxSourceModules)
                    throw new ProtocolError("InputLimit", "Preview module snapshot violates canonical source bounds.");
                Modules.Add(Path[..^5], File.Text);
            }
            return new(Value.Id, EntryName, new PackageSources(EntrySource.Text, Modules), null);
        }
        Visit(Selected);
        // Exactly the runtime's ordinal activation selection: required targets must
        // already be active; optional targets bind only when already active.
        var Ordered = new List<PreviewProject>(); var Activated = new HashSet<string>(StringComparer.Ordinal);
        while (Closure.Count != 0) {
            Project? Next = Closure.Values.OrderBy(Value => Value.Package!.Id, StringComparer.Ordinal)
                .FirstOrDefault(Value => Value.Package!.Dependencies(false).All(Activated.Contains));
            if (Next == null) throw new ProtocolError("ModuleError", "Required dependency cycle blocks preview activation.");
            Ordered.Add(ConvertProject(Next, Next == Selected ? Entry : null));
            Activated.Add(Next.Package!.Id); Closure.Remove(Next.Package.Id);
        }
        if (Selected.Package == null) Ordered.Add(ConvertProject(Selected, Entry));
        return (Ordered.Single(Value => Value.Id == Selected.Id), Ordered);
    }
    internal JObject Inspect(JObject Input)
    {
        Protocol.Fields(Input, "Folders");
        if (Input["Folders"] is not JArray Folders || Folders.Count > 32) throw new ProtocolError("InvalidRequest", "Expected at most 32 workspace folders.");
        var FolderIds = new HashSet<string>(StringComparer.Ordinal);
        int Aggregate = 0, Candidates = 0;
        foreach (JObject Folder in Folders) {
            Protocol.Fields(Folder, "Id", "Files");
            string FolderId = Protocol.Text(Folder, "Id", 128);
            if (!FolderIds.Add(FolderId)) throw new ProtocolError("InvalidRequest", "Duplicate folder identity.");
            if (Folder["Files"] is not JArray Files || Files.Count > 2048) throw new ProtocolError("InvalidRequest", "Workspace candidate count exceeds 2048.");
            Candidates += Files.Count;
            if (Candidates > 2048) throw new ProtocolError("InputLimit", "Workspace candidate count exceeds 2048 across all folders.");
            var Sources = new List<Source>(); var Seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject File in Files) {
                Protocol.Fields(File, "Path", "Text", "Archive");
                string Path = Protocol.Text(File, "Path", 512);
                if (Path.Length == 0 || Path.Contains('\\') || Path.Contains(':') || Path.Split('/').Any(Part => Part.Length == 0 || Part == "." || Part == "..") || !Seen.Add(Path))
                    throw new ProtocolError("InvalidRequest", "Unsafe or duplicate snapshot path.");
                if (Path.EndsWith(".claddon", StringComparison.Ordinal)) {
                    string Encoded = Protocol.Text(File, "Archive", 6 * 1024 * 1024);
                    if (File["Text"] != null) throw new ProtocolError("InvalidRequest", "Archive must contain bytes only.");
                    try {
                        byte[] Bytes = Convert.FromBase64String(Encoded);
                        Aggregate = checked(Aggregate + Bytes.Length);
                        if (Aggregate > 6 * 1024 * 1024) throw new ProtocolError("InputLimit", "Workspace snapshot exceeds 6 MiB.");
                        var Package = AddonPackageSnapshot.FromArchive(Bytes);
                        Projects.Add(new(FolderId + "/" + Path, FolderId, Path, "Archive", [], Package));
                    } catch (Exception Error) when (Error is InvalidOperationException || Error is FormatException) { Add(FolderId, Path, "Package", Error.Message); }
                    continue;
                }
                string Text = Protocol.Text(File, "Text", AddonPolicy.MaxSourceBytes + 1);
                if (File["Archive"] != null) throw new ProtocolError("InvalidRequest", "Source must contain text only.");
                int Size = Protocol.Utf8.GetByteCount(Text);
                Aggregate = checked(Aggregate + Size);
                if (Aggregate > 6 * 1024 * 1024) throw new ProtocolError("InputLimit", "Workspace snapshot exceeds 6 MiB.");
                if (Path.Split('/').Any(Part => Part.Equals(".config.luau", StringComparison.OrdinalIgnoreCase))) {
                    if (Size <= AddonPolicy.MaxSourceBytes) {
                        JObject ParsedConfig = NativeAnalysis.Parse(Text);
                        foreach (JObject Error in (JArray)ParsedConfig["Errors"]!)
                            Add(FolderId, Path, "RuntimeSyntax", (string)Error["Message"]!, (int)Error["Line"]!, Column(Text, (int)Error["Line"]!, (int)Error["Column"]!));
                    }
                    continue;
                }
                Sources.Add(new(FolderId, Path, Text));
            }
            string[] Roots = Sources.Where(File => File.Path == "addon.json" || File.Path.EndsWith("/addon.json", StringComparison.Ordinal))
                .Select(File => File.Path[..^10]).OrderByDescending(Path => Path.Length).ToArray();
            if (Roots.Length > AddonPolicy.MaxRegistrations) throw new ProtocolError("InputLimit", "Too many addon candidates.");
            var Owned = new HashSet<Source>();
            foreach (string Prefix in Roots) {
                var Selected = Sources.Where(File => !Owned.Contains(File) && File.Path.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
                foreach (var File in Selected) Owned.Add(File);
                AddonPackageSnapshot? Package = null;
                try { Package = Admit(Selected, Prefix); }
                catch (InvalidOperationException Error) { Add(FolderId, Prefix + "addon.json", "Package", Error.Message); }
                Projects.Add(new(FolderId + "/" + Prefix, FolderId, Prefix, "Addon", Selected, Package));
            }
            var Unowned = Sources.Where(File => !Owned.Contains(File)).ToList();
            if (Unowned.Count > 0) {
                string Kind = Unowned.Any(File => File.Path == "init.luau") ? "Root" : "Standalone";
                int Total = 0;
                foreach (var File in Unowned) {
                    try {
                        ModulePath.Validate(File.Path, true);
                        int Size = Protocol.Utf8.GetByteCount(File.Text); Total += Size;
                        if (Size > AddonPolicy.MaxSourceBytes) throw new InvalidOperationException("Source exceeds the CarbonLuau limit.");
                    } catch (InvalidOperationException Error) { Add(FolderId, File.Path, "Source", Error.Message); }
                }
                if (Total > AddonPolicy.MaxAggregateSourceBytes || Unowned.Count > AddonPolicy.MaxSourceModules + 1)
                    Add(FolderId, Unowned[0].Path, "SourceLimit", "Project source exceeds the CarbonLuau limit.");
                Projects.Add(new(FolderId + "/", FolderId, "", Kind, Unowned, null));
            }
        }
        var Index = Projects.Where(Project => Project.Package != null && Project.Kind == "Addon")
            .GroupBy(Project => Project.Package!.Id, StringComparer.Ordinal).ToDictionary(Group => Group.Key, Group => Group.ToArray(), StringComparer.Ordinal);
        foreach (var Group in Index.Where(Pair => Pair.Value.Length != 1))
            foreach (var Project in Group.Value) Add(Project.Folder, Project.Prefix + "addon.json", "AmbiguousDependency", "Multiple workspace addons have package ID \"" + Group.Key + "\".");
        foreach (var Project in Projects) {
            if (Project.Package != null) foreach (string Dependency in Project.Package.Dependencies(false).Concat(Project.Package.Dependencies(true)))
                if (!Index.TryGetValue(Dependency, out var Matches) || Matches.Length != 1)
                    Add(Project.Folder, Project.Kind == "Archive" ? Project.Prefix : Project.Prefix + "addon.json", "UnresolvedDependency", "Dependency \"" + Dependency + "\" has no unambiguous workspace source; the server may provide it.", Severity: "Warning");
            if (Project.Kind == "Archive" || (Project.Kind == "Addon" && Project.Package == null)) continue;
            foreach (var File in Project.Sources.Where(File => File.Path.EndsWith(".luau", StringComparison.Ordinal))) {
                if (Protocol.Utf8.GetByteCount(File.Text) > AddonPolicy.MaxSourceBytes) continue;
                JObject Analysis = NativeAnalysis.Parse(File.Text);
                foreach (JObject Error in (JArray)Analysis["Errors"]!)
                    Add(File.Folder, File.Path, "RuntimeSyntax", (string)Error["Message"]!, (int)Error["Line"]!, Column(File.Text, (int)Error["Line"]!, (int)Error["Column"]!));
                foreach (JObject Import in (JArray)Analysis["Requires"]!) Resolve(Project, File, Import, Index);
            }
        }
        return new JObject { ["Projects"] = new JArray(Projects.Select(Project => new JObject {
            ["Id"] = Project.Id, ["Folder"] = Project.Folder, ["Prefix"] = Project.Prefix, ["Kind"] = Project.Kind,
            ["PackageId"] = Project.Package?.Id, ["PackageVersion"] = Project.Package?.Version,
            ["PackageSchema"] = Project.Package == null ? null : AddonPolicy.Schema,
            ["ValidPackage"] = Project.Package != null })), ["Diagnostics"] = Diagnostics,
            ["Imports"] = Imports, ["CompilerValidation"] = "Unavailable", ["RuntimeExecution"] = false };
    }
    private static AddonPackageSnapshot Admit(List<Source> Sources, string Prefix)
    {
        // In-memory admission through the exact production ZIP parser, with no artifact written.
        using var Buffer = new MemoryStream();
        using (var Archive = new ZipArchive(Buffer, ZipArchiveMode.Create, true)) {
            foreach (var File in Sources.OrderBy(File => File.Path, StringComparer.Ordinal)) {
                byte[] Bytes = Protocol.Utf8.GetBytes(File.Text);
                if (Bytes.Length > AddonPolicy.MaxSourceBytes) throw new InvalidOperationException("Package source exceeds the CarbonLuau limit.");
                var Entry = Archive.CreateEntry(File.Path[Prefix.Length..], Bytes.Length == 0 ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                Entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var Stream = Entry.Open(); Stream.Write(Bytes);
            }
        }
        return AddonPackageSnapshot.FromArchive(Buffer.ToArray());
    }
    private void Resolve(Project Project, Source File, JObject Import, Dictionary<string, Project[]> Index)
    {
        string Name = (string)Import["Name"]!;
        bool Qualified = Name.StartsWith('@');
        string Dependency = Qualified ? Name[1..].Split('/')[0] : "";
        bool Declared = Project.Package != null && Project.Package.Dependencies(false).Concat(Project.Package.Dependencies(true)).Contains(Dependency, StringComparer.Ordinal);
        Project? Target = Project;
        if (Qualified) Target = Index.TryGetValue(Dependency, out var Matches) && Matches.Length == 1 ? Matches[0] : null;
        JObject Selection = NativeAnalysis.Resolve(Name, Declared, Target != null, Target?.Package?.Main, Target?.Package?.PublicModules() ?? []);
        int Code = (int)Selection["Code"]!;
        string Logical = (string)Selection["Logical"]!;
        string TargetPath = (Target?.Prefix ?? "") + (Target?.Kind == "Addon" ? "" : "modules/") + Logical + ".luau";
        if (Code == 0 && (Target == null || !Target.Sources.Any(Source => Source.Path == TargetPath))) Code = 7;
        int Line = (int)Import["Line"]!, ByteColumn = (int)Import["Column"]!;
        if (Code != 0) {
            string Message = Code switch {
                3 => "Dependency \"" + Dependency + "\" is not declared by this addon.",
                4 => "Dependency \"" + Dependency + "\" has no unambiguous workspace source.",
                5 => "Dependency \"" + Dependency + "\" has no main module.",
                6 => "Module \"" + Name + "\" is private.",
                7 => "Module \"" + Name + "\" does not exist.",
                _ => "Module path \"" + Name + "\" is not canonical."
            };
            Add(File.Folder, File.Path, "Require" + Code, Message, Line, Column(File.Text, Line, ByteColumn), Code == 4 ? "Warning" : "Error");
            return;
        }
        if (Imports.Count >= 8192) throw new ProtocolError("OutputLimit", "Workspace import count exceeds 8192.");
        Imports.Add(new JObject { ["Folder"] = File.Folder, ["Path"] = File.Path, ["TargetFolder"] = Target!.Folder, ["TargetPath"] = TargetPath,
            ["Name"] = Name, ["Line"] = Line, ["Column"] = ByteColumn,
            ["EndLine"] = Import["EndLine"]!.DeepClone(), ["EndColumn"] = Import["EndColumn"]!.DeepClone() });
    }
    private static int Column(string Text, int Line, int ByteColumn)
    {
        string Value = Text.Split('\n')[Line];
        byte[] Bytes = Protocol.Utf8.GetBytes(Value);
        return Protocol.Utf8.GetCharCount(Bytes, 0, Math.Min(Bytes.Length, ByteColumn));
    }
    private void Add(string Folder, string Path, string Code, string Message, int Line = 0, int Column = 0, string Severity = "Error")
    {
        if (Diagnostics.Count >= MaxDiagnostics) return;
        Diagnostics.Add(new JObject { ["Folder"] = Folder, ["Path"] = Path, ["Code"] = Code,
            ["Message"] = AddonPolicy.Diagnostic(Message), ["Severity"] = Severity,
            ["Line"] = Line, ["Column"] = Column, ["EndLine"] = Line, ["EndColumn"] = Column + 1 });
    }
}
