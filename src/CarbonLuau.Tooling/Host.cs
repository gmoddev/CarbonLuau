using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CarbonLuau.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal sealed class Host
{
    private readonly JObject Metadata = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tooling-metadata.json")));
    private readonly JObject Catalog = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "carbonluau-api.json")));
    private readonly JObject Pin = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "language-server.json")));
    private bool Initialized;
    internal int Run(Stream Input, Stream Output)
    {
        while (true) {
            JObject? Request = Protocol.Read(Input);
            if (Request == null) return 0;
            // An unidentifiable request cannot safely be correlated; close the stream.
            if (Request["Id"]?.Type != JTokenType.Integer || !int.TryParse(Request["Id"]!.ToString(), out int Id) || Id < 1)
                throw new ProtocolError("InvalidId", "Request Id must be an integer from 1 to 2147483647.");
            var Response = new JObject { ["Protocol"] = Protocol.Identity, ["Id"] = Id };
            bool Stop = false;
            try {
                Protocol.Fields(Request, "Protocol", "Id", "Method", "Params", "ProjectRevision");
                if (Request["Protocol"] is not JObject Contract || !JToken.DeepEquals(Contract, Protocol.Identity))
                    throw new ProtocolError("IncompatibleProtocol", "Unsupported CarbonLuau tooling protocol.");
                string Method = Protocol.Text(Request, "Method", 64);
                if (Request["Params"] is not JObject Params) throw new ProtocolError("InvalidRequest", "Params must be an object.");
                if (Method != "initialize" && !Initialized) throw new ProtocolError("NotInitialized", "Initialize tooling before making requests.");
                JObject Result;
                if (Method == "initialize") Result = Initialize(Params);
                else if (Method == "getMetadata") { Protocol.Fields(Params); Result = new JObject { ["Catalog"] = Catalog.DeepClone(), ["Metadata"] = Metadata.DeepClone() }; }
                else if (Method == "validateProject" || Method == "resolveProjectGraph") {
                    string Revision = Protocol.Text(Request, "ProjectRevision", 71);
                    if (Revision != RevisionFor(Params)) throw new ProtocolError("RevisionMismatch", "Project snapshot revision does not match its content and selected tooling.");
                    Response["ProjectRevision"] = Revision;
                    Result = new ProjectModel().Inspect(Params);
                    Result["Api"] = Metadata["Api"]!.DeepClone();
                } else if (Method == "shutdown") { Protocol.Fields(Params); Result = new JObject { ["Stopped"] = true }; Stop = true; }
                else throw new ProtocolError("UnknownMethod", "This tooling operation is not implemented.");
                if (Method != "validateProject" && Method != "resolveProjectGraph" && Request["ProjectRevision"] != null)
                    throw new ProtocolError("InvalidRequest", "ProjectRevision is only valid for project operations.");
                Response["Result"] = Result;
            } catch (Exception Error) when (Error is ProtocolError || Error is InvalidOperationException || Error is ArgumentException || Error is InvalidCastException || Error is FormatException || Error is JsonException) {
                Response["Error"] = new JObject { ["Code"] = Error is ProtocolError Known ? Known.Code : "InvalidRequest", ["Message"] = AddonPolicy.Diagnostic(Error.Message) };
            }
            Protocol.Write(Output, Response);
            if (Stop) return 0;
        }
    }
    private JObject Initialize(JObject Params)
    {
        Protocol.Fields(Params, "ExtensionVersion", "ApiVersion", "PackageSchema", "Platform", "PackVersion", "Capabilities");
        if (Initialized) throw new ProtocolError("AlreadyInitialized", "Tooling has already been initialized.");
        Protocol.Text(Params, "ExtensionVersion", 64);
        if (Protocol.Text(Params, "ApiVersion") != (string)Metadata["Api"]!["Version"]!)
            throw new ProtocolError("UnsupportedApi", "Unknown CarbonLuau scripting API " + Protocol.Text(Params, "ApiVersion") + ".");
        if (Params["PackageSchema"]?.Type != JTokenType.Integer || (int)Params["PackageSchema"]! != AddonPolicy.Schema)
            throw new ProtocolError("UnsupportedSchema", "Unknown CarbonLuau package schema.");
        string Platform = (OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "darwin" : "unsupported") + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        if (Protocol.Text(Params, "Platform") != Platform || !(new[] { "win32-x64", "linux-x64", "darwin-x64", "darwin-arm64" }).Contains(Platform))
            throw new ProtocolError("UnsupportedPlatform", "Tooling platform does not match this process.");
        if (Protocol.Text(Params, "PackVersion") != (string)Pin["PackVersion"]!) throw new ProtocolError("IncompatiblePack", "Tooling pack identity mismatch.");
        if (Params["Capabilities"] is not JArray Capabilities || Capabilities.Any(Value => Value.Type != JTokenType.String || !new[] { "StaticAnalysis", "Metadata" }.Contains((string)Value!)))
            throw new ProtocolError("UnsupportedCapability", "Requested tooling capability is unavailable.");
        Initialized = true;
        return new JObject { ["Protocol"] = Protocol.Identity, ["Api"] = Metadata["Api"]!.DeepClone(),
            ["PackageSchemas"] = new JArray(AddonPolicy.Schema), ["ApiMetadataSchema"] = 1, ["PreviewPlanSchema"] = 1,
            ["RuntimeLuauRevision"] = Metadata["RuntimeLuauRevision"]!.DeepClone(), ["Pack"] = Pin.DeepClone(),
            ["Platform"] = Platform, ["Capabilities"] = new JArray("StaticAnalysis", "Metadata"), ["Limits"] = Metadata["Limits"]!.DeepClone(),
            ["MaxFrameBytes"] = Protocol.MaxFrame, ["MaxOutstandingRequests"] = 1 };
    }
    internal string RevisionFor(JObject Params) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Protocol.Utf8.GetBytes(
        Canonical(Params).ToString(Formatting.None) + "\n" + (string)Metadata["Api"]!["Version"]! + "\n" + (string)Pin["PackVersion"]!)));
    private static JToken Canonical(JToken Value)
    {
        if (Value is JObject Object) return new JObject(Object.Properties().OrderBy(Property => Property.Name, StringComparer.Ordinal).Select(Property => new JProperty(Property.Name, Canonical(Property.Value))));
        if (Value is JArray Array) return new JArray(Array.Select(Canonical));
        return Value.DeepClone();
    }
}
