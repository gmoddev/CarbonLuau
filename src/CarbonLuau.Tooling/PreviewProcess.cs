using System.Diagnostics;
using System.Security.Cryptography;
using CarbonLuau.Core;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal static class PreviewProcess
{
    internal static async Task<JObject> Run(JObject Params, string Revision, CancellationToken Cancellation)
    {
        PreviewPack.ValidateParams(Params);
        var Pack = new PreviewPack(); Pack.CheckIdentity(Params);
        Cancellation.ThrowIfCancellationRequested();
        var Start = new ProcessStartInfo(Pack.Launcher) { UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        Start.Environment.Clear();
        foreach (string Name in new[] { "SystemRoot", "WINDIR" }) {
            string? Value = Environment.GetEnvironmentVariable(Name); if (Value != null) Start.Environment[Name] = Value;
        }
        Start.Environment["DOTNET_EnableDiagnostics"] = "0";
        Start.Environment["DOTNET_GCHeapHardLimit"] = "4000000"; // Hex: 64 MiB managed heap, separate from the Luau heap.
        Start.Environment["DOTNET_PROCESSOR_COUNT"] = "2";
        Start.Environment["DOTNET_gcServer"] = "0";
        Start.Environment["DOTNET_GCConserveMemory"] = "9";
        foreach (string Arg in new[] { Environment.ProcessId.ToString(), Pack.Executable, "--preview-worker" }) Start.ArgumentList.Add(Arg);
        using Process Child = Process.Start(Start) ?? throw new ProtocolError("PreviewLaunch", "Preview launcher did not start.");
        using var Lifetime = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        Lifetime.CancelAfter(TimeSpan.FromSeconds(15)); // Admission/runtime startup bound; not the execution allowance.
        bool Execution = false; int LogBytes = 0; Exception? LogFailure = null;
        void Kill() {
            try { if (!Child.HasExited) Child.Kill(true); } catch (InvalidOperationException) { }
        }
        using var Stop = Lifetime.Token.Register(Kill);
        Task Logs = Task.Run(async () => {
            byte[] Buffer = new byte[4096];
            try {
                int Count;
                while ((Count = await Child.StandardError.BaseStream.ReadAsync(Buffer)) != 0) {
                    LogBytes = checked(LogBytes + Count);
                    if (LogBytes > 65536) { LogFailure = new ProtocolError("PreviewOutputLimit", "Preview worker diagnostic output exceeded its bound."); Kill(); return; }
                }
            } catch (IOException) { }
        });
        string Nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        JObject Envelope(string Phase) => new() { ["Protocol"] = PreviewPack.Identity, ["Nonce"] = Nonce, ["ProjectRevision"] = Revision, ["Phase"] = Phase };
        async Task<JObject> Read() {
            JObject? Value = await Task.Run(() => Protocol.Read(Child.StandardOutput.BaseStream)).WaitAsync(Lifetime.Token);
            if (Value == null) {
                await Child.WaitForExitAsync(Lifetime.Token);
                if (LogFailure != null) throw LogFailure;
                bool Memory = Child.ExitCode == 129 || Child.ExitCode == unchecked((int)0xc0000017);
                throw new ProtocolError(Memory ? "PreviewMemory" : "PreviewCrash", Memory ? "Preview exceeded its process resource limit." : "Preview worker exited without a complete result.");
            }
            return Value;
        }
        try {
            await Task.Run(() => Protocol.Write(Child.StandardInput.BaseStream, new JObject {
                ["Protocol"] = PreviewPack.Identity, ["Nonce"] = Nonce, ["ProjectRevision"] = Revision, ["Params"] = Params.DeepClone() })).WaitAsync(Lifetime.Token);
            JObject Ready = await Read();
            JObject Response;
            if (JToken.DeepEquals(Ready, Envelope("Ready"))) {
                Cancellation.ThrowIfCancellationRequested();
                Execution = true;
                Lifetime.CancelAfter(PreviewPack.ExecutionMilliseconds);
                await Task.Run(() => Protocol.Write(Child.StandardInput.BaseStream, Envelope("Execute"))).WaitAsync(Lifetime.Token);
                Response = await Read();
            } else {
                if (Ready["Error"] is not JObject || Ready["Result"] != null) throw new ProtocolError("PreviewProtocol", "Invalid preview readiness envelope.");
                Response = Ready;
            }
            Protocol.Fields(Response, "Protocol", "Nonce", "ProjectRevision", "Phase", "Result", "Error");
            if (!JToken.DeepEquals(Response["Protocol"], PreviewPack.Identity) || (string?)Response["Nonce"] != Nonce ||
                (string?)Response["ProjectRevision"] != Revision || (string?)Response["Phase"] != "Completed" ||
                (Response["Result"] == null) == (Response["Error"] == null)) throw new ProtocolError("PreviewProtocol", "Stale, mismatched or malformed preview result.");
            // Completion stops execution time. Reap remains separately bounded.
            Lifetime.CancelAfter(TimeSpan.FromSeconds(2));
            Child.StandardInput.Close();
            JObject? Extra = await Task.Run(() => Protocol.Read(Child.StandardOutput.BaseStream)).WaitAsync(Lifetime.Token);
            if (Extra != null) throw new ProtocolError("PreviewProtocol", "Preview worker emitted multiple results.");
            await Child.WaitForExitAsync(Lifetime.Token); await Logs.WaitAsync(Lifetime.Token);
            if (LogFailure != null) throw LogFailure;
            if (Child.ExitCode != 0) throw new ProtocolError("PreviewCrash", "Preview worker failed during teardown.");
            Cancellation.ThrowIfCancellationRequested();
            if (Response["Error"] is JObject Error) {
                Protocol.Fields(Error, "Code", "Message", "Screens");
                if (Error["Screens"] is not JArray Screens || Screens.Count > Carbon.Plugins.CarbonLuau.PreviewGuiSession.ScreenLimit || Screens.Any(Value => Value is not JObject Choice || Choice.Count != 2 ||
                    Choice["Id"]?.Type != JTokenType.String || ((string)Choice["Id"]!).Length > 20 || Choice["Name"]?.Type != JTokenType.String ||
                    Protocol.Utf8.GetByteCount((string)Choice["Name"]!) > Carbon.Plugins.CarbonLuau.PreviewGuiSession.NameByteLimit))
                    throw new ProtocolError("PreviewProtocol", "Invalid preview error details.");
                throw new ProtocolError(Protocol.Text(Error, "Code", 64), AddonPolicy.Diagnostic(Protocol.Text(Error, "Message", 1024)), new JObject { ["Screens"] = Screens.DeepClone() });
            }
            if (Response["Result"] is not JObject Plan || (int?)Plan["SchemaVersion"] != PreviewPack.Schema ||
                (string?)Plan["ProjectRevision"] != Revision || (string?)Plan["ApiVersion"] != (string?)Params["ApiVersion"] ||
                (string?)Plan["PackVersion"] != (string?)Params["PackVersion"] ||
                (string?)Plan["SemanticRevision"] != (string?)Params["SemanticRevision"] ||
                (string?)Plan["ToolingBuildId"] != (string?)Params["ToolingBuildId"] ||
                (string?)Plan["ProjectId"] != (string?)Params["ProjectId"] ||
                (Params["ScreenId"] != null && (string?)Plan["Screen"]?["Id"] != (string?)Params["ScreenId"]) ||
                (string?)Plan["Entry"] != ((string?)Params["Entry"] ?? "init.luau") ||
                (double?)Plan["Viewport"]?["Width"] != (double?)Params["Viewport"]?["Width"] ||
                (double?)Plan["Viewport"]?["Height"] != (double?)Params["Viewport"]?["Height"])
                throw new ProtocolError("PreviewProtocol", "Preview plan identity mismatch.");
            try {
                Protocol.Fields(Plan, "SchemaVersion", "SemanticRevision", "ToolingBuildId", "ProjectRevision", "ApiVersion", "PackVersion", "ProjectId", "Entry",
                    "Screen", "AvailableScreens", "Viewport", "Nodes", "Clips", "Accounting");
                Carbon.Plugins.CarbonLuau.PreviewGuiSession.ValidatePlan(Plan);
            } catch (Exception Failure) when (Failure is ProtocolError or InvalidOperationException or ArgumentException or InvalidCastException or FormatException or OverflowException) {
                throw new ProtocolError("PreviewProtocol", "Preview plan failed canonical validation.");
            }
            return Plan;
        } catch (OperationCanceledException) {
            throw new ProtocolError(Cancellation.IsCancellationRequested ? "PreviewCanceled" : "PreviewDeadline",
                Cancellation.IsCancellationRequested ? "Preview was canceled; no plan was retained." : Execution ? "Preview exceeded its execution or teardown deadline." : "Preview startup exceeded its deadline.");
        } catch (ProtocolError Error) when (Error.Code is "InvalidFrame" or "InvalidJson") {
            throw new ProtocolError("PreviewProtocol", "Preview worker returned invalid or oversized framing.");
        } catch (Exception Error) when (Error is IOException or System.Text.Json.JsonException or Newtonsoft.Json.JsonException or InvalidCastException) {
            throw new ProtocolError("PreviewProtocol", "Preview worker returned an invalid or incomplete protocol stream.");
        } finally {
            Kill();
            if (!Child.WaitForExit(5000)) throw new ProtocolError("PreviewReap", "Preview process could not be reaped; further execution must stop.");
        }
    }
}
