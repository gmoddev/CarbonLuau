using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal sealed class PreviewPack
{
    internal const int Schema = 1, Policy = 1, Bridge = 1, ExecutionMilliseconds = 1000;
    internal readonly JObject Manifest, Metadata;
    internal readonly string Launcher, Executable, Native;
    internal PreviewPack()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
            throw new ProtocolError("PreviewUnavailable", "Preview execution is unqualified on this platform; static tooling remains available.");
        string Platform = OperatingSystem.IsWindows() ? "win32-x64" : "linux-x64";
        string Profile = OperatingSystem.IsWindows() ? "WindowsJobCommit256MiB-Active1-Suspended-v1" : "LinuxData256MiB-AddressSpace2GiB-Rss256MiB-10ms-v1";
        for (DirectoryInfo? Parent = new(AppContext.BaseDirectory); Parent != null; Parent = Parent.Parent)
            if ((Parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new ProtocolError("IncompatiblePack", "Preview pack path contains a filesystem link.");
        Manifest = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pack.json")));
        var Pin = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "language-server.json")));
        Metadata = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tooling-metadata.json")));
        string SemanticRevision = Protocol.Text(Manifest, "SemanticRevision", 40), BuildId = Protocol.Text(Manifest, "ToolingBuildId", 71);
        if (SemanticRevision.Length != 40 || SemanticRevision.Any(Value => !"0123456789abcdef".Contains(Value)) ||
            BuildId.Length != 71 || !BuildId.StartsWith("sha256:", StringComparison.Ordinal) || BuildId[7..].Any(Value => !"0123456789abcdef".Contains(Value)))
            throw new ProtocolError("IncompatiblePack", "Invalid preview semantic/build identity.");
        if ((int?)Manifest["PreviewSecurityPolicyVersion"] != Policy || (int?)Manifest["PreviewBridgeVersion"] != Bridge ||
            (int?)Manifest["PreviewPlanSchema"] != Schema || (bool?)Manifest["PreviewQualified"] != true ||
            (string?)Manifest["Platform"] != Platform || (string?)Manifest["PreviewContainmentProfile"] != Profile ||
            (string?)Manifest["PackVersion"] != (string?)Pin["PackVersion"] ||
            (string?)Manifest["RuntimeLuauRevision"] != (string?)Metadata["RuntimeLuauRevision"] ||
            (string?)Manifest["ApiVersion"] != (string?)Metadata["Api"]?["Version"])
            throw new ProtocolError("IncompatiblePack", "Preview policy, platform, API or tooling pin mismatch.");
        Launcher = Payload("PreviewLauncher"); Executable = Payload("Host"); Native = Payload("PreviewNative");
        if (Manifest["Files"] is not JObject Files || Files.Count > 512) throw new ProtocolError("IncompatiblePack", "Invalid preview payload manifest.");
        foreach (JProperty File in Files.Properties()) Verify(File.Name);
        Verify("tooling-metadata.json"); Verify("language-server.json");
        Verify(OperatingSystem.IsWindows() ? "carbonluau_analysis.dll" : "libcarbonluau_analysis.so");
    }
    private string Payload(string Field) => Verify(Protocol.Text(Manifest, Field, 128));
    private string Verify(string Name)
    {
        if (Path.GetFileName(Name) != Name || Name.IndexOfAny(['/', '\\', ':']) >= 0 || Name is "." or "..")
            throw new ProtocolError("IncompatiblePack", "Unsafe preview pack payload.");
        string Value = Path.Combine(AppContext.BaseDirectory, Name); var Info = new FileInfo(Value);
        if (!Info.Exists || Info.Length > 128 * 1024 * 1024 || (Info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Value))) != (string?)Manifest["Files"]?[Name])
            throw new ProtocolError("IncompatiblePack", "Preview payload integrity failure: " + Name);
        return Value;
    }
    internal static JObject Identity => new() { ["Name"] = "CarbonLuau.PreviewWorker", ["Major"] = 1, ["Minor"] = 0 };
    internal static void ValidateParams(JObject Params)
    {
        Protocol.Fields(Params, "Snapshot", "ProjectId", "Entry", "ScreenId", "Viewport", "ApiVersion", "PackVersion", "ToolingBuildId", "SemanticRevision", "PreviewPlanSchema", "WorkspaceTrusted");
        if ((bool?)Params["WorkspaceTrusted"] != true) throw new ProtocolError("WorkspaceUntrusted", "Preview execution requires Workspace Trust.");
        if (Params["Snapshot"] is not JObject) throw new ProtocolError("InvalidRequest", "Preview requires a bounded project snapshot.");
        Protocol.Text(Params, "ProjectId", 640);
        if (Params["Entry"] != null) Protocol.Text(Params, "Entry", 127);
        if (Params["ScreenId"] != null) {
            string Id = Protocol.Text(Params, "ScreenId", 20);
            if (!ulong.TryParse(Id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong Value) || Value == 0 || Value.ToString() != Id)
                throw new ProtocolError("InvalidRequest", "Preview ScreenId must be a canonical run-local object ID.");
        }
        if (Params["Viewport"] is not JObject Viewport) throw new ProtocolError("InvalidViewport", "Preview requires an explicit viewport.");
        Protocol.Fields(Viewport, "Width", "Height");
        foreach (string Dimension in new[] { "Width", "Height" }) {
            JToken? Value = Viewport[Dimension];
            if (Value?.Type is not (JTokenType.Integer or JTokenType.Float) || !double.IsFinite((double)Value) || (double)Value < 1 || (double)Value > 8192)
                throw new ProtocolError("InvalidViewport", "Preview dimensions must be finite and within 1..8192 pixels.");
        }
        if ((int?)Params["PreviewPlanSchema"] != Schema) throw new ProtocolError("IncompatiblePreviewSchema", "Unsupported preview-plan schema.");
    }
    internal void CheckIdentity(JObject Params)
    {
        if (Protocol.Text(Params, "ApiVersion") != (string)Metadata["Api"]!["Version"]! || Protocol.Text(Params, "PackVersion") != (string)Manifest["PackVersion"]! ||
            Protocol.Text(Params, "ToolingBuildId", 71) != Protocol.Text(Manifest, "ToolingBuildId", 71) ||
            Protocol.Text(Params, "SemanticRevision", 40) != Protocol.Text(Manifest, "SemanticRevision", 40))
            throw new ProtocolError("IncompatiblePack", "Preview request API or tooling pack does not match.");
    }
}
