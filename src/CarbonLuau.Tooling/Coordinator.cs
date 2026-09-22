using System.Threading.Channels;
using CarbonLuau.Core;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

// Independent bounded reader keeps cancel/EOF responsive while a worker runs.
internal sealed class Coordinator(Host Host)
{
    private sealed record Ticket(JObject Request, int Id, string? Revision, CancellationTokenSource Cancellation);
    internal async Task<int> Run(Stream Input, Stream Output)
    {
        var Queue = Channel.CreateBounded<Ticket>(4);
        var Pending = new Dictionary<int, Ticket>();
        var Gate = new object(); var OutputGate = new object();
        using var Lifetime = new CancellationTokenSource();
        void Write(JObject Value) { lock (OutputGate) Protocol.Write(Output, Value); }
        JObject Error(int Id, string Code, string Message) => new() { ["Protocol"] = Protocol.Identity, ["Id"] = Id,
            ["Error"] = new JObject { ["Code"] = Code, ["Message"] = AddonPolicy.Diagnostic(Message) } };
        Task Reader = Task.Run(() => {
            try {
                JObject? Request;
                while ((Request = Protocol.Read(Input)) != null) {
                    if (Request["Id"]?.Type != JTokenType.Integer || !int.TryParse(Request["Id"]!.ToString(), out int Id) || Id < 1)
                        throw new ProtocolError("InvalidId", "Request Id must be an integer from 1 to 2147483647.");
                    if ((string?)Request["Method"] == "cancel") {
                        try {
                            Protocol.Fields(Request, "Protocol", "Id", "Method", "Params");
                            if (!JToken.DeepEquals(Request["Protocol"], Protocol.Identity) || Request["Params"] is not JObject Params)
                                throw new ProtocolError("InvalidRequest", "Invalid cancellation envelope.");
                            Protocol.Fields(Params, "RequestId", "ProjectRevision");
                            if (Params["RequestId"]?.Type != JTokenType.Integer || !int.TryParse(Params["RequestId"]!.ToString(), out int TargetId))
                                throw new ProtocolError("InvalidRequest", "Cancellation requires a request ID.");
                            string Revision = Protocol.Text(Params, "ProjectRevision", 71);
                            Ticket? Target;
                            lock (Gate) {
                                if (Pending.ContainsKey(Id)) throw new ProtocolError("InvalidId", "Cancellation ID is already pending.");
                                Pending.TryGetValue(TargetId, out Target);
                                if (Target == null || Target.Revision != Revision || (string?)Target.Request["Method"] != "preview")
                                    throw new ProtocolError("StaleCancellation", "Cancellation does not identify a pending preview revision.");
                                Target.Cancellation.Cancel();
                            }
                            Write(new JObject { ["Protocol"] = Protocol.Identity, ["Id"] = Id, ["Result"] = new JObject { ["Canceled"] = true } });
                        } catch (ProtocolError Failure) { Write(Error(Id, Failure.Code, Failure.Message)); }
                        continue;
                    }
                    lock (Gate) {
                        if (Pending.Count >= 4 || Pending.ContainsKey(Id)) { Write(Error(Id, "Busy", "Tooling outstanding request bound reached or request ID reused.")); continue; }
                        var Ticket = new Ticket(Request, Id, (string?)Request["ProjectRevision"], CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token));
                        Pending.Add(Id, Ticket);
                        if (!Queue.Writer.TryWrite(Ticket)) throw new ProtocolError("InputLimit", "Tooling request queue exceeded its bound.");
                    }
                    if ((string?)Request["Method"] == "shutdown") break;
                }
                Queue.Writer.TryComplete();
            } catch (Exception Failure) { Queue.Writer.TryComplete(Failure); }
            finally { Lifetime.Cancel(); }
        });
        try {
            await foreach (Ticket Ticket in Queue.Reader.ReadAllAsync()) {
                try {
                    JObject Response = await Host.Handle(Ticket.Request, Ticket.Cancellation.Token);
                    Write(Response);
                    if ((string?)Ticket.Request["Method"] == "shutdown" && Response["Error"] == null) return 0;
                } finally {
                    lock (Gate) { Pending.Remove(Ticket.Id); Ticket.Cancellation.Dispose(); }
                }
            }
            await Reader;
            return 0;
        } finally {
            Lifetime.Cancel();
            lock (Gate) foreach (Ticket Ticket in Pending.Values) Ticket.Cancellation.Dispose();
        }
    }
}
