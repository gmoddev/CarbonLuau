using CarbonLuau.Core;
using Newtonsoft.Json.Linq;
using Gui = Carbon.Plugins.CarbonLuau.PreviewGuiSession;

namespace CarbonLuau.Tooling;

internal static class PreviewWorker
{
    internal static int Run(Stream Input, Stream Output)
    {
        var Pack = new PreviewPack();
        JObject Request = Protocol.Read(Input) ?? throw new ProtocolError("PreviewProtocol", "Preview request is missing.");
        Protocol.Fields(Request, "Protocol", "Nonce", "ProjectRevision", "Params");
        if (!JToken.DeepEquals(Request["Protocol"], PreviewPack.Identity) || Request["Params"] is not JObject Params)
            throw new ProtocolError("PreviewProtocol", "Preview worker protocol mismatch.");
        string Nonce = Protocol.Text(Request, "Nonce", 64), Revision = Protocol.Text(Request, "ProjectRevision", 71);
        PreviewPack.ValidateParams(Params); Pack.CheckIdentity(Params);
        if (new Host().RevisionFor(Params) != Revision) throw new ProtocolError("RevisionMismatch", "Preview inputs do not match their revision.");
        JObject Envelope(string Phase) => new() { ["Protocol"] = PreviewPack.Identity, ["Nonce"] = Nonce, ["ProjectRevision"] = Revision, ["Phase"] = Phase };
        JObject Response = Envelope("Completed");
        Gui? Selected = null;
        try {
            // Admission errors are structured results. No workspace code runs before Execute.
            var Project = new ProjectModel().Preview((JObject)Params["Snapshot"]!, (string)Params["ProjectId"]!, (string?)Params["Entry"]);
            using var Runtime = new NativePreview(Pack.Native, (string)Pack.Metadata["RuntimeLuauRevision"]!);
            Protocol.Write(Output, Envelope("Ready"));
            JObject? Grant = Protocol.Read(Input);
            if (Grant == null || !JToken.DeepEquals(Grant, Envelope("Execute"))) throw new ProtocolError("PreviewProtocol", "Missing exact preview execution grant.");
            var Active = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (PreviewProject Value in Project.Activation) {
                var Domain = Runtime.CreateDomain(Value, Active);
                // Native entry chunk identifiers deliberately exclude path separators.
                // Module loading keeps its canonical qualified module names separately.
                Runtime.Execute(Domain.Domain, Value.Package == null ? "preview.entry" : "addon." + Value.Package.Id + ".init", Value.Sources.EntrySource);
                if (Value.Package != null) Active.Add(Value.Package.Id, Domain.Domain);
                if (Value.Id == Project.Selected.Id) Selected = Domain.Gui;
            }
            if (Selected == null) throw new ProtocolError("PreviewInternal", "Selected preview domain was not activated.");
            JObject Plan = Selected.Plan((string?)Params["ScreenId"]!, (double)Params["Viewport"]!["Width"]!, (double)Params["Viewport"]!["Height"]!);
            Plan["SchemaVersion"] = PreviewPack.Schema; Plan["ProjectRevision"] = Revision;
            Plan["SemanticRevision"] = Params["SemanticRevision"]!.DeepClone();
            Plan["ToolingBuildId"] = Params["ToolingBuildId"]!.DeepClone();
            Plan["ApiVersion"] = Params["ApiVersion"]!.DeepClone(); Plan["PackVersion"] = Params["PackVersion"]!.DeepClone();
            Plan["ProjectId"] = Project.Selected.Id; Plan["Entry"] = Project.Selected.EntryName;
            Plan["AvailableScreens"] = Selected.Screens;
            Response["Result"] = Plan;
        } catch (Exception Error) when (Error is ProtocolError or InvalidOperationException or ArgumentException or InvalidCastException or FormatException or OverflowException) {
            Response["Error"] = new JObject { ["Code"] = Error is ProtocolError Known ? Known.Code : "GuiError",
                ["Message"] = AddonPolicy.Diagnostic(Error.Message), ["Screens"] = Selected?.Screens ?? [] };
        }
        Protocol.Write(Output, Response);
        return 0;
    }
}
