using System.Threading.Channels;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

// Separate from Host: analysis failure never kills static project validation.
internal sealed class AnalysisSupervisor : IDisposable
{
    private AnalysisSnapshot? Snapshot;
    private AnalysisProcess? Process;
    private string Revision = "";
    private bool Latched;
    private readonly object Lifecycle = new();
    private bool Stopped;
    internal async Task<int> Run(Stream Input, Stream Output)
    {
        var Requests = Channel.CreateBounded<JObject>(16);
        _ = Task.Run(() => {
            try {
                while (Protocol.Read(Input) is JObject Message)
                    if (!Requests.Writer.TryWrite(Message)) throw new ProtocolError("InputLimit", "Analysis request queue exceeded its bound.");
                Requests.Writer.TryComplete();
            } catch (Exception Error) { Requests.Writer.TryComplete(Error); }
            finally { Dispose(); }
        });
        try {
            await foreach (JObject Request in Requests.Reader.ReadAllAsync()) {
                if (Stopped) break;
                if (Request["Id"]?.Type != JTokenType.Integer || (long)Request["Id"]! < 1 || (long)Request["Id"]! > int.MaxValue) return 2;
                var Response = new JObject { ["Protocol"] = Protocol.Identity, ["Id"] = Request["Id"]!.DeepClone() };
                try {
                    Protocol.Fields(Request, "Protocol", "Id", "Method", "Params", "ProjectRevision");
                    if (!JToken.DeepEquals(Request["Protocol"], Protocol.Identity) || Request["Params"] is not JObject Params) throw new ProtocolError("AnalysisProtocol", "Invalid analysis proxy request.");
                    string Method = Protocol.Text(Request, "Method");
                    if (Method == "shutdown") { Response["Result"] = new JObject { ["Stopped"] = true }; Protocol.Write(Output, Response); return 0; }
                    if (Params["Trusted"]?.Type != JTokenType.Boolean || !(bool)Params["Trusted"]!) throw new ProtocolError("TrustRequired", "Language analysis requires Workspace Trust.");
                    if (Latched) throw new ProtocolError("AnalysisLatched", "Language analysis stopped after failure. Use Restart Tooling.");
                    string RequestedRevision = Protocol.Text(Request, "ProjectRevision", 71);
                    if (Method == "snapshot") {
                        Protocol.Fields(Params, "Trusted", "Snapshot");
                        if (Params["Snapshot"] is not JObject Source) throw new ProtocolError("InvalidRequest", "Missing analysis snapshot.");
                        if (new Host().RevisionFor(Source) != RequestedRevision) throw new ProtocolError("RevisionMismatch", "Analysis snapshot identity mismatch.");
                        lock (Lifecycle) { StopSession(); }
                        JObject Inspection = new ProjectModel().Inspect(Source);
                        lock (Lifecycle) {
                            if (Stopped) return 0;
                            Snapshot = new AnalysisSnapshot(Source, Inspection);
                            Process = new AnalysisProcess(Snapshot);
                            Revision = RequestedRevision;
                        }
                        await Process.Initialize();
                        Response["Result"] = new JObject { ["Admitted"] = new JArray(Snapshot.Uris.Keys), ["Withheld"] = Snapshot.Withheld.DeepClone() };
                    } else if (Method == "language") {
                        Protocol.Fields(Params, "Trusted", "Operation", "Folder", "Path", "Position");
                        if (Snapshot == null || Process == null || Revision != RequestedRevision) throw new ProtocolError("RevisionMismatch", "Language request has no current snapshot.");
                        string Key = AnalysisSnapshot.Key(Protocol.Text(Params, "Folder", 128), Protocol.Text(Params, "Path", 512));
                        if (!Snapshot.Uris.TryGetValue(Key, out string? Uri)) throw new ProtocolError("AnalysisWithheld", "This source is excluded from executable analysis; static diagnostics remain available.");
                        string Operation = Protocol.Text(Params, "Operation");
                        if (Operation is not ("textDocument/diagnostic" or "textDocument/hover" or "textDocument/completion" or "textDocument/signatureHelp" or "textDocument/definition"))
                            throw new ProtocolError("AnalysisProtocol", "Language operation is not allowed.");
                        JObject LspParams = new() { ["textDocument"] = new JObject { ["uri"] = Uri } };
                        if (Operation != "textDocument/diagnostic") {
                            if (Params["Position"] is not JObject Position || Position["line"]?.Type != JTokenType.Integer || Position["character"]?.Type != JTokenType.Integer)
                                throw new ProtocolError("InvalidRequest", "Invalid language position.");
                            Protocol.Fields(Position, "line", "character");
                            string[] Lines = Snapshot.Sources[Key].Split('\n');
                            int Line = (int)Position["line"]!, Column = (int)Position["character"]!;
                            if (Line < 0 || Line >= Lines.Length || Column < 0 || Column > Lines[Line].Length) throw new ProtocolError("InvalidRequest", "Language position exceeds source bounds.");
                            LspParams["position"] = Position.DeepClone();
                        }
                        Process.Open(Key);
                        JToken Result = await Process.Request(Operation, LspParams);
                        ValidateResult(Result, Snapshot, Key);
                        Response["Result"] = new JObject { ["Value"] = Result };
                    } else throw new ProtocolError("UnknownMethod", "Unknown analysis operation.");
                    Response["ProjectRevision"] = RequestedRevision;
                } catch (Exception Error) {
                    Latched = Error is not ProtocolError { Code: "AnalysisCrash" };
                    lock (Lifecycle) { StopSession(); }
                    Response["Error"] = new JObject { ["Code"] = Error is ProtocolError Known ? Known.Code : "AnalysisFailure", ["Message"] = CarbonLuau.Core.AddonPolicy.Diagnostic(Error.Message) };
                }
                Protocol.Write(Output, Response);
            }
            return 0;
        } finally { Dispose(); }
    }
    private static void ValidateResult(JToken Result, AnalysisSnapshot Snapshot, string DocumentKey)
    {
        int Nodes = 0;
        void Walk(JToken Value, int Depth, string Key) {
            if (++Nodes > 65536 || Depth > 32) throw new ProtocolError("AnalysisProtocol", "Analysis result exceeds its bound.");
            if (Value is JObject Object) {
                string? Uri = (string?)Object["uri"] ?? (string?)Object["targetUri"];
                if (Uri != null) Key = Snapshot.Uris.FirstOrDefault(Pair => Pair.Value == Uri).Key ?? throw new ProtocolError("AnalysisProtocol", "Unadmitted analysis result URI.");
                foreach (JProperty Property in Object.Properties().ToArray()) {
                    if (Property.Name is "command" or "additionalTextEdits" or "data" or "codeDescription" or "relatedDocuments") { Property.Remove(); continue; }
                    if (Property.Name is "uri" or "targetUri") {
                        string? MappedKey = Snapshot.Uris.FirstOrDefault(Pair => Pair.Value == (string?)Property.Value).Key;
                        if (MappedKey == null) throw new ProtocolError("AnalysisProtocol", "Language result references an unadmitted file.");
                        Property.Value = MappedKey;
                    } else if (Property.Name is "items" && Property.Value is JArray Items && Items.Count > 256) {
                        while (Items.Count > 256) Items.RemoveAt(Items.Count - 1);
                    }
                    if (Property.Name is "range" or "targetRange" or "targetSelectionRange" or "selectionRange" or "insert" or "replace") {
                        if (Property.Value is not JObject Range || Range["start"] is not JObject Start || Range["end"] is not JObject End)
                            throw new ProtocolError("AnalysisProtocol", "Malformed analysis range.");
                        string[] Lines = Snapshot.Sources[Key].Split('\n');
                        foreach (var Position in new[] { Start, End }) {
                            if (Position["line"]?.Type != JTokenType.Integer || Position["character"]?.Type != JTokenType.Integer)
                                throw new ProtocolError("AnalysisProtocol", "Invalid analysis position.");
                            int Line = (int)Position["line"]!, Column = (int)Position["character"]!;
                            if (Line < 0 || Line >= Lines.Length || Column < 0 || Column > Lines[Line].Length) throw new ProtocolError("AnalysisProtocol", "Analysis range exceeds admitted source.");
                        }
                        if ((int)End["line"]! < (int)Start["line"]! || (int)End["line"]! == (int)Start["line"]! && (int)End["character"]! < (int)Start["character"]!)
                            throw new ProtocolError("AnalysisProtocol", "Reversed analysis range.");
                    }
                    Walk(Property.Value, Depth + 1, Key);
                }
            } else if (Value is JArray Array) foreach (var Item in Array) Walk(Item, Depth + 1, Key);
            else if (Value.Type == JTokenType.String && ((string)Value!).Length > 65536) throw new ProtocolError("AnalysisProtocol", "Oversized language result string.");
        }
        Walk(Result, 0, DocumentKey);
    }
    private void StopSession() { Process?.Dispose(); Process = null; Snapshot?.Dispose(); Snapshot = null; Revision = ""; }
    public void Dispose() { lock (Lifecycle) { Stopped = true; StopSession(); } }
}
