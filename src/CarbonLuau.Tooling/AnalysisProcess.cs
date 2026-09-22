using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Channels;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal sealed class AnalysisProcess : IDisposable
{
    private readonly Process Child;
    private readonly AnalysisSnapshot Snapshot;
    private readonly Channel<JObject> Messages = Channel.CreateBounded<JObject>(32);
    private readonly Timer Guard;
    private readonly List<FileSystemWatcher> Watchers = [];
    private readonly object WriteLock = new();
    private readonly object StopLock = new();
    private long Deadline;
    private int NextId, Stopped;
    private Exception? Failure;
    private int LogBytes;
    private volatile bool MemoryFailure;
    private readonly HashSet<string> Opened = new(StringComparer.Ordinal);
    internal AnalysisProcess(AnalysisSnapshot Snapshot)
    {
        this.Snapshot = Snapshot;
        JObject Pack = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pack.json")));
        string Platform = (OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsLinux() ? "linux" : "darwin") + "-" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string Profile = Platform switch { "win32-x64" => "WindowsJobCommit1GiB-Active1-Suspended-v1", "linux-x64" => "LinuxAddressSpace2GiB-Rss1GiB-ProcessGroup-v1", _ => "Unqualified" };
        if ((int?)Pack["AnalysisSecurityPolicyVersion"] != 1 || (string?)Pack["AnalysisProfile"] != "TrustedSnapshotAnalysis" ||
            (int?)Pack["AnalysisProxyRevision"] != 1 || (int?)Pack["TransformRevision"] != 2 ||
            (string?)Pack["LanguageServerVersion"] != "1.70.0" || (string?)Pack["LanguageServerLuauRevision"] != "a62362a53ddc9c629b0e29378a84abb4534d8b64" ||
            (string?)Pack["Platform"] != Platform || Profile == "Unqualified" || (string?)Pack["AnalysisContainmentProfile"] != Profile ||
            (bool?)Pack["LanguageServerQualified"] != true || Pack["QualifiedAnalysisPlatforms"] is not JArray Qualified || !Qualified.Values<string>().Contains(Platform))
            throw new ProtocolError("IncompatiblePack", "Language-analysis policy or pin mismatch.");
        string Payload(string Field) {
            string Name = Protocol.Text(Pack, Field);
            if (Path.GetFileName(Name) != Name || Name.IndexOfAny(['/', '\\', ':']) >= 0 || Name is "." or "..") throw new ProtocolError("IncompatiblePack", "Unsafe pack payload.");
            string FilePath = Path.Combine(AppContext.BaseDirectory, Name);
            var Info = new FileInfo(FilePath);
            if (!Info.Exists || Info.Length > 128 * 1024 * 1024 || (Info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(FilePath))) != (string?)Pack["Files"]?[Name])
                throw new ProtocolError("IncompatiblePack", "Language-analysis payload integrity failure.");
            return FilePath;
        }
        string Launcher = Payload("AnalysisLauncher"), Server = Payload("LanguageServer"), Definitions = Payload("Definitions"), Documentation = Payload("Documentation");
        var Start = new ProcessStartInfo(Launcher) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Snapshot.Root,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        Start.Environment.Clear();
        foreach (string Name in new[] { "SystemRoot", "WINDIR", "LANG", "LC_ALL" }) {
            string? Value = Environment.GetEnvironmentVariable(Name); if (Value != null) Start.Environment[Name] = Value;
        }
        Start.Environment["HOME"] = Snapshot.Root; Start.Environment["TMPDIR"] = Snapshot.Root; Start.Environment["TEMP"] = Snapshot.Root; Start.Environment["TMP"] = Snapshot.Root;
        foreach (string Arg in new[] { Environment.ProcessId.ToString(), Server, "lsp", "--stdio", "--flag:LuauSolverV2=true", "--flag:DebugLuauTypeFunctionRuntimeHeapLimit=67108864",
            "--base-luaurc=" + Path.Combine(Snapshot.Root, "base.json"), "--settings=" + Path.Combine(Snapshot.Root, "settings.json"),
            "--definitions:@carbonluau=" + Definitions, "--docs=" + Documentation }) Start.ArgumentList.Add(Arg);
        Snapshot.Check();
        Deadline = Stopwatch.GetTimestamp() + 30 * Stopwatch.Frequency;
        Child = Process.Start(Start) ?? throw new ProtocolError("LaunchFailed", "Analysis launcher failed.");
        Guard = new Timer(_ => {
            try {
                Snapshot.Check();
                if (Volatile.Read(ref Deadline) != 0 && Stopwatch.GetTimestamp() >= Volatile.Read(ref Deadline)) Abort(new ProtocolError("AnalysisTimeout", "Language analysis exceeded its deadline. Use Restart Tooling."));
            } catch (Exception Error) { Abort(Error); }
        }, null, 0, 50);
        try {
            for (DirectoryInfo? Directory = new(Snapshot.Root); Directory != null; Directory = Directory.Parent) {
                var Watcher = new FileSystemWatcher(Directory.FullName) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, IncludeSubdirectories = false };
                FileSystemEventHandler Change = (_, _) => { try { Snapshot.Check(); } catch (Exception Error) { Abort(Error); } };
                Watcher.Created += Change; Watcher.Renamed += (Sender, Event) => Change(Sender, Event);
                Watcher.Error += (_, _) => Abort(new ProtocolError("AnalysisEnvironment", "Analysis configuration watcher failed."));
                Watcher.EnableRaisingEvents = true; Watchers.Add(Watcher);
            }
        } catch { Dispose(); throw; }
        _ = Task.Run(() => {
            try {
                while (Protocol.Read(Child.StandardOutput.BaseStream) is JObject Message)
                    if (!Messages.Writer.TryWrite(Message)) throw new ProtocolError("AnalysisProtocol", "Analysis output queue exceeded its bound.");
                Child.WaitForExit(1000);
                bool Resource = MemoryFailure || Child.HasExited && (Child.ExitCode == 129 || Child.ExitCode == unchecked((int)0xc0000017));
                throw new ProtocolError(Resource ? "AnalysisMemory" : "AnalysisCrash", Resource ? "Language analysis exceeded a process resource limit. Use Restart Tooling." : "Language analysis stopped unexpectedly.");
            } catch (Exception Error) { Abort(Error); }
        });
        _ = Task.Run(async () => {
            byte[] Buffer = new byte[4096];
            try { int Count; while ((Count = await Child.StandardError.BaseStream.ReadAsync(Buffer)) != 0) {
                if (Interlocked.Add(ref LogBytes, Count) > 65536) throw new ProtocolError("AnalysisLogLimit", "Analysis log exceeded its bound.");
                string Text = System.Text.Encoding.UTF8.GetString(Buffer, 0, Count);
                if (Text.Contains("bad_alloc", StringComparison.Ordinal) || Text.Contains("out of memory", StringComparison.OrdinalIgnoreCase)) MemoryFailure = true;
            } } catch (Exception Error) { Abort(Error); }
        });
    }
    internal async Task Initialize()
    {
        await Request("initialize", new JObject {
            ["processId"] = Environment.ProcessId, ["rootUri"] = new Uri(Snapshot.Root + Path.DirectorySeparatorChar).AbsoluteUri,
            ["workspaceFolders"] = new JArray(new JObject { ["uri"] = new Uri(Snapshot.Root + Path.DirectorySeparatorChar).AbsoluteUri, ["name"] = "CarbonLuau snapshot" }),
            ["capabilities"] = JObject.Parse("{\"workspace\":{\"configuration\":true},\"textDocument\":{\"diagnostic\":{\"dynamicRegistration\":false},\"hover\":{\"contentFormat\":[\"plaintext\"]},\"completion\":{\"completionItem\":{\"snippetSupport\":false,\"documentationFormat\":[\"plaintext\"]}},\"signatureHelp\":{\"signatureInformation\":{\"documentationFormat\":[\"plaintext\"]}}}}"),
            ["initializationOptions"] = JObject.FromObject(new { fflags = new { LuauSolverV2 = "true", DebugLuauTypeFunctionRuntimeHeapLimit = "67108864" } })
        }, true);
        Send(new JObject { ["jsonrpc"] = "2.0", ["method"] = "initialized", ["params"] = new JObject() });
    }
    internal async Task<JToken> Request(string Method, JObject Params, bool Initial = false)
    {
        Snapshot.Check();
        if (Failure != null) throw Failure;
        Interlocked.CompareExchange(ref Deadline, Stopwatch.GetTimestamp() + (Initial ? 30 : 15) * Stopwatch.Frequency, 0);
        int Id = ++NextId;
        Send(new JObject { ["jsonrpc"] = "2.0", ["id"] = Id, ["method"] = Method, ["params"] = Params });
        try {
            while (await Messages.Reader.WaitToReadAsync()) {
                while (Messages.Reader.TryRead(out JObject? Message)) {
                    if ((string?)Message["jsonrpc"] != "2.0") throw new ProtocolError("AnalysisProtocol", "Invalid LSP identity.");
                    if (Message["method"] is JToken Operation) {
                        string Name = (string)Operation!;
                        if (Message["id"] != null) {
                            if (Name != "workspace/configuration" || Message["params"]?["items"] is not JArray Items || Items.Count > 32)
                                throw new ProtocolError("AnalysisProtocol", "Unapproved language-server request.");
                            Send(new JObject { ["jsonrpc"] = "2.0", ["id"] = Message["id"]!.DeepClone(), ["result"] = new JArray(Items.Select(_ => Snapshot.Settings())) });
                        } else if (Name is not ("window/logMessage" or "window/showMessage" or "textDocument/publishDiagnostics" or "$/progress" or "$/logTrace"))
                            throw new ProtocolError("AnalysisProtocol", "Unapproved language-server notification.");
                        else if (Name != "textDocument/publishDiagnostics" && Interlocked.Add(ref LogBytes, Message.ToString().Length) > 65536)
                            throw new ProtocolError("AnalysisLogLimit", "Analysis operational output exceeded its bound.");
                        continue;
                    }
                    if ((int?)Message["id"] != Id || (Message["result"] == null) == (Message["error"] == null)) throw new ProtocolError("AnalysisProtocol", "Stale or malformed LSP response.");
                    Volatile.Write(ref Deadline, 0);
                    if (Message["error"] != null) throw new ProtocolError("AnalysisError", "Language server could not complete this operation.");
                    return Message["result"]!.DeepClone();
                }
            }
            throw Failure ?? new ProtocolError("AnalysisCrash", "Language server stream closed.");
        } catch (Exception Error) { Abort(Error); throw; }
    }
    internal void Open(string Key)
    {
        if (!Opened.Add(Key)) return;
        Snapshot.Check();
        Interlocked.CompareExchange(ref Deadline, Stopwatch.GetTimestamp() + 15 * Stopwatch.Frequency, 0);
        Send(new JObject { ["jsonrpc"] = "2.0", ["method"] = "textDocument/didOpen", ["params"] = new JObject {
            ["textDocument"] = new JObject { ["uri"] = Snapshot.Uris[Key], ["languageId"] = "luau", ["version"] = 1, ["text"] = Snapshot.Sources[Key] } } });
    }
    private void Send(JObject Value)
    {
        lock (WriteLock) {
            if (Failure != null) throw Failure;
            try { Protocol.Write(Child.StandardInput.BaseStream, Value); }
            catch (IOException) {
                // A dead server can close stdin before the stdout reader reports
                // EOF. Preserve crash/resource classification for that race.
                if (Failure != null) throw Failure;
                if (Child.WaitForExit(100)) {
                    bool Resource = MemoryFailure || Child.ExitCode == 129 || Child.ExitCode == unchecked((int)0xc0000017);
                    throw new ProtocolError(Resource ? "AnalysisMemory" : "AnalysisCrash", Resource ? "Language analysis exceeded a process resource limit. Use Restart Tooling." : "Language analysis stopped unexpectedly.");
                }
                throw;
            }
        }
    }
    private void Abort(Exception Error)
    {
        lock (StopLock) {
            if (Stopped != 0) return;
            Stopped = 1; Failure = Error;
            try { if (!Child.HasExited) Child.Kill(true); Child.WaitForExit(5000); } catch (InvalidOperationException) { }
            // Consumers may dispose the snapshot as soon as completion is visible.
            Messages.Writer.TryComplete(Error);
        }
    }
    public void Dispose()
    {
        Guard.Dispose(); foreach (var Watcher in Watchers) Watcher.Dispose();
        Abort(new ProtocolError("AnalysisStopped", "Language analysis stopped."));
        lock (StopLock) Child.Dispose();
    }
}
