using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal sealed class ProtocolError(string Code, string Message, JObject? Details = null) : Exception(Message)
{
    internal string Code { get; } = Code;
    internal JObject? Details { get; } = Details;
}

internal static class Protocol
{
    internal const int MaxFrame = 8 * 1024 * 1024;
    internal static JObject Identity => new() { ["Name"] = "CarbonLuau.Tooling", ["Major"] = 1, ["Minor"] = 0 };
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    internal static JObject? Read(Stream Input)
    {
        var Header = new List<byte>();
        while (true) {
            int Next = Input.ReadByte();
            if (Next < 0 && Header.Count == 0) return null;
            if (Next < 0 || Next > 127 || Header.Count == 4096) throw new ProtocolError("InvalidFrame", "Invalid or oversized protocol header.");
            Header.Add((byte)Next);
            if (Header.Count >= 4 && Header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
        }
        string Text = Encoding.ASCII.GetString(Header.ToArray());
        var Lines = Text.Split("\r\n", StringSplitOptions.None);
        if (Lines.Length != 3 || !Lines[0].StartsWith("Content-Length: ", StringComparison.Ordinal) ||
            !int.TryParse(Lines[0][16..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int Length) ||
            Length <= 0 || Length > MaxFrame) throw new ProtocolError("InvalidFrame", "Invalid protocol content length.");
        byte[] Body = new byte[Length];
        Input.ReadExactly(Body);
        // System.Text.Json enforces JSON grammar; the explicit walk also rejects duplicate keys.
        using var Document = JsonDocument.Parse(Body, new JsonDocumentOptions { MaxDepth = 32 });
        CheckKeys(Document.RootElement);
        using var Reader = new Newtonsoft.Json.JsonTextReader(new StringReader(Utf8.GetString(Body))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None, MaxDepth = 32 };
        return JObject.Load(Reader);
    }
    private static void CheckKeys(JsonElement Element)
    {
        if (Element.ValueKind == JsonValueKind.Object) {
            var Names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var Property in Element.EnumerateObject()) {
                if (!Names.Add(Property.Name)) throw new ProtocolError("InvalidJson", "Duplicate JSON property.");
                CheckKeys(Property.Value);
            }
        } else if (Element.ValueKind == JsonValueKind.Array) foreach (var Item in Element.EnumerateArray()) CheckKeys(Item);
    }
    internal static void Write(Stream Output, JObject Value)
    {
        byte[] Body = Utf8.GetBytes(Value.ToString(Newtonsoft.Json.Formatting.None));
        if (Body.Length > MaxFrame) throw new ProtocolError("OutputLimit", "Tooling result exceeds protocol limit.");
        Output.Write(Encoding.ASCII.GetBytes($"Content-Length: {Body.Length}\r\n\r\n"));
        Output.Write(Body); Output.Flush();
    }
    internal static void Fields(JObject Value, params string[] Allowed)
    {
        if (Value.Properties().Any(Property => !Allowed.Contains(Property.Name, StringComparer.Ordinal)))
            throw new ProtocolError("InvalidRequest", "Unknown request field.");
    }
    internal static string Text(JObject Value, string Name, int Max = 256)
    {
        if (Value[Name]?.Type != JTokenType.String || ((string)Value[Name]!).Length > Max || ((string)Value[Name]!).Contains('\0'))
            throw new ProtocolError("InvalidRequest", "Invalid " + Name + ".");
        return (string)Value[Name]!;
    }
}
