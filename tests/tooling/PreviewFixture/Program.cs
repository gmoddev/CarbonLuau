using System.Runtime.InteropServices;
using CarbonLuau.Tooling;
using Newtonsoft.Json.Linq;

// Test-only hostile worker, never provisioned by Build-Tooling.
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "FixturePid.txt"), Environment.ProcessId.ToString());
JObject Request = Protocol.Read(Console.OpenStandardInput())!;
string Mode = (string)Request["Params"]!["Snapshot"]!["Folders"]![0]!["Files"]![0]!["Text"]!;
JObject Envelope(string Phase) => new() { ["Protocol"] = Request["Protocol"]!.DeepClone(), ["Nonce"] = Request["Nonce"]!.DeepClone(),
    ["ProjectRevision"] = Request["ProjectRevision"]!.DeepClone(), ["Phase"] = Phase };
Stream Output = Console.OpenStandardOutput();
Protocol.Write(Output, Envelope("Ready"));
_ = Protocol.Read(Console.OpenStandardInput());
switch (Mode) {
    case "crash": return 86;
    case "hang": Thread.Sleep(Timeout.Infinite); break;
    case "memory":
        var Blocks = new List<IntPtr>();
        try {
            while (true) {
                IntPtr Block = Marshal.AllocHGlobal(8 * 1024 * 1024); Blocks.Add(Block);
                for (int Offset = 0; Offset < 8 * 1024 * 1024; Offset += 4096) Marshal.WriteByte(Block, Offset, 123);
            }
        } catch (OutOfMemoryException) {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "FixtureMemory.json"), new JObject { ["AllocatedNativeBytes"] = Blocks.Count * 8L * 1024 * 1024 }.ToString());
            return 87;
        }
    case "oversized": Console.Write("Content-Length: 8388609\r\n\r\n"); Console.Out.Flush(); return 0;
    case "malformed": Console.Write("Content-Length: 1\r\n\r\nx"); Console.Out.Flush(); return 0;
    case "stderr": Console.Error.Write(new string('x', 65537)); Console.Error.Flush(); Thread.Sleep(Timeout.Infinite); break;
}
var Response = Envelope("Completed");
Response["Error"] = new JObject { ["Code"] = "Fixture", ["Message"] = "hostile test fixture", ["Screens"] = new JArray() };
if (Mode == "nonce") Response["Nonce"] = new string('0', 64);
if (Mode == "stale") Response["ProjectRevision"] = "sha256:" + new string('0', 64);
if (Mode == "protocol") Response["Protocol"]!["Major"] = 99;
if (Mode == "plan") {
    Response.Remove("Error");
    var Plan = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FixturePlan.json")));
    Plan["ProjectRevision"] = Request["ProjectRevision"]!.DeepClone();
    Plan["Nodes"]![1]!["Projected"]!["RectPx"]!["X"] = 12345;
    Response["Result"] = Plan;
}
Protocol.Write(Output, Response);
if (Mode == "duplicate") Protocol.Write(Output, Response);
return 0;
