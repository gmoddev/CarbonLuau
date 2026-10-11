using CarbonLuau.Core;
using Newtonsoft.Json.Linq;

internal static class InventoryChangedTypes
{
    private const string SinceApi = "0.6.5-experimental";
    private const string ContextId = "InventoryChangeContext";
    private const string SignalId = "PlayerInventory.ItemChanged";
    private static void Check(bool Value, string Message)
    { if (!Value) throw new InvalidOperationException(Message); }
    private static void Reject(Action Operation, string Message)
    {
        try { Operation(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(Message);
    }
    private static JObject Member(JObject Catalog, string Id)
    { return ((JArray)Catalog["Members"]!).Cast<JObject>().Single(Value => (string?)Value["Id"] == Id); }
    private static JObject Type(JObject Catalog, string Id)
    { return ((JArray)Catalog["Types"]!).Cast<JObject>().Single(Value => (string?)Value["Id"] == Id); }
    private static void Signature(JObject Declaration, string Kind, string[] Names, string[] Types, string[] Returns)
    {
        var Signatures = (JArray)Declaration["Signatures"]!;
        Check((string?)Declaration["Kind"] == Kind && Signatures.Count == 1, "C3 exact signature kind/count drift");
        var Parameters = ((JArray)Signatures[0]!["Parameters"]!).Cast<JObject>().ToArray();
        Check(Parameters.Select(Value => (string?)Value["Name"]).SequenceEqual(Names) &&
            Parameters.Select(Value => (string?)Value["Type"]).SequenceEqual(Types) &&
            Parameters.Select(Value => (bool?)Value["Optional"]).SequenceEqual(Types.Select(Value => (bool?)Value.EndsWith('?'))) &&
            Parameters.All(Value => (bool?)Value["Variadic"] == false) &&
            ((JArray)Signatures[0]!["Returns"]!).Values<string>().SequenceEqual(Returns),
            "C3 exact argument names/types/optionality/returns drift");
    }
    private static void Availability(JObject Declaration, string Preview)
    {
        Check((string?)Declaration["Availability"]!["SinceApi"] == SinceApi &&
            (bool?)Declaration["Availability"]!["Implemented"] == true &&
            (string?)Declaration["Availability"]!["Qualification"] == "Experimental" &&
            (string?)Declaration["Preview"] == Preview, "C3 introduction/qualification/preview drift");
    }
    private static void Contract(JObject Catalog)
    {
        ApiCatalog.Validate(Catalog);
        var Context = Type(Catalog, ContextId);
        Check((string?)Context["Kind"] == "Class" && (string?)Context["Representation"] == "Record" &&
            Context["BaseTypeId"] == null && Context["TypeExpression"] == null,
            "C3 named context must remain a standalone immutable record");
        var Fields = ((JArray)Catalog["Members"]!).Cast<JObject>()
            .Where(Value => (string?)Value["OwnerId"] == ContextId)
            .OrderBy(Value => (string?)Value["Name"], StringComparer.Ordinal).ToArray();
        Check(Fields.Select(Value => (string?)Value["Name"]).SequenceEqual(new[] { "After", "Before", "Delta", "Item" }) &&
            Fields.Select(Value => (string?)Value["Id"]).SequenceEqual(new[] {
                ContextId + ".After", ContextId + ".Before", ContextId + ".Delta", ContextId + ".Item" }) &&
            Fields.Select(Value => (string?)Value["ValueType"]).SequenceEqual(new[] { "number", "number", "number", "string" }) &&
            Fields.All(Value => (string?)Value["Kind"] == "Property" && (bool?)Value["Writable"] == false),
            "C3 context must have exactly four required readonly scalar fields");
        var Signal = Member(Catalog, SignalId);
        Signature(Signal, "Signal", new[] { "Context" }, new[] { ContextId }, Array.Empty<string>());
        foreach (var Declaration in Fields.Concat(new[] { Context, Signal })) Availability(Declaration, "Unavailable");
        Check(((JArray)Catalog["Members"]!).Cast<JObject>().Where(Value => (string?)Value["OwnerId"] == "PlayerInventory")
            .Select(Value => (string?)Value["Id"]).OrderBy(Value => Value, StringComparer.Ordinal)
            .SequenceEqual(new[] { "PlayerInventory.CountItem", "PlayerInventory.HasItem", SignalId }),
            "C3 inventory acquired an extra API or lost an existing read");
        Check(!((JArray)Catalog["Types"]!).Any(Value => (string?)Value["Id"] == "Inventory"),
            "C3 must not introduce a global Inventory service");
        Availability(Type(Catalog, "PlayerInventory"), "ReadMock");
        foreach (string Id in new[] { "Player.GetInventory", "PlayerInventory.CountItem", "PlayerInventory.HasItem" })
            Availability(Member(Catalog, Id), "ReadMock");
        Signature(Member(Catalog, "Player.GetInventory"), "Method", Array.Empty<string>(), Array.Empty<string>(), new[] { "PlayerInventory" });
        Signature(Member(Catalog, "PlayerInventory.CountItem"), "Method", new[] { "ShortName" }, new[] { "string" }, new[] { "number" });
        Signature(Member(Catalog, "PlayerInventory.HasItem"), "Method", new[] { "ShortName", "Amount" }, new[] { "string", "number?" }, new[] { "boolean" });
    }
    private static string ChangeAnnotation(string Bootstrap, string Id, Action<JObject> Change)
    {
        const string Marker = "-- @carbonluau-api ";
        string[] Lines = Bootstrap.Split('\n');
        int Matches = 0;
        for (int Index = 0; Index < Lines.Length; Index++) {
            if (!Lines[Index].StartsWith(Marker, StringComparison.Ordinal)) continue;
            var Declaration = JObject.Parse(Lines[Index].Substring(Marker.Length));
            string? DeclarationId = (string?)Declaration["Type"] ??
                (string?)Declaration["Owner"] + "." + (string?)Declaration["Name"];
            if (DeclarationId != Id) continue;
            Change(Declaration); Matches++;
            Lines[Index] = Marker + Declaration.ToString(Newtonsoft.Json.Formatting.None);
        }
        Check(Matches == 1, "C3 source annotation not unique: " + Id);
        return string.Join('\n', Lines);
    }
    private static string ChangeBinding(string Bootstrap, string Binding, string Replacement)
    {
        string Target = "\n" + Binding + "\n";
        Check(Bootstrap.Contains(Target, StringComparison.Ordinal) &&
            Bootstrap.IndexOf(Target, StringComparison.Ordinal) == Bootstrap.LastIndexOf(Target, StringComparison.Ordinal),
            "C3 source binding missing or ambiguous: " + Binding);
        return Bootstrap.Replace(Target, "\n" + Replacement + "\n", StringComparison.Ordinal);
    }
    public static void Run(JObject Catalog, string Bootstrap, JObject Release, string Native)
    {
        Contract(Catalog);
        Check((string?)Catalog["Api"]!["Version"] == SinceApi && (string?)Release["packageVersion"] == "0.5.0",
            "C3 development API must not change published package identity");
        string Definitions = ApiArtifacts.Definitions(Catalog);
        Check(Definitions.Contains("read ItemChanged: SignalWith<(Context: InventoryChangeContext) -> ()>", StringComparison.Ordinal) &&
            Definitions.Contains("export type InventoryChangeContext = {\n    read After: number,\n    read Before: number,\n    read Delta: number,\n    read Item: string,\n}", StringComparison.Ordinal),
            "C3 generated callback must use the named four-field context");
        Check(Definitions.Contains("read Connect: (self: SignalWith<Callback>, Callback: Callback) -> (Connection)", StringComparison.Ordinal) &&
            Definitions.Contains("read PlayerAdded: Signal\n", StringComparison.Ordinal),
            "C3 specialization must preserve Connection and legacy Player Signal");
        var Documentation = ApiArtifacts.Documentation(Catalog);
        foreach (string Id in new[] { SignalId, ContextId + ".Item", ContextId + ".Before", ContextId + ".After", ContextId + ".Delta" })
            Check(Documentation["@carbonluau/globaltype/" + Id] != null, "C3 generated documentation omits " + Id);
        var Rebuilt = ApiCatalog.Build(Bootstrap, Release, Native);
        Check(JToken.DeepEquals(Catalog, Rebuilt) && Definitions == ApiArtifacts.Definitions(Rebuilt), "C3 generation is nondeterministic");

        var Drifts = new Dictionary<string, Action<JObject>> {
            ["missing field"] = Value => Member(Value, ContextId + ".Item").Remove(),
            ["extra field"] = Value => {
                var Extra = (JObject)Member(Value, ContextId + ".Item").DeepClone();
                Extra["Id"] = ContextId + ".Player"; Extra["Name"] = "Player"; Extra["ValueType"] = "Player";
                ((JArray)Value["Members"]!).Add(Extra);
            },
            ["optional field"] = Value => Member(Value, ContextId + ".Before")["ValueType"] = "number?",
            ["wrong scalar"] = Value => Member(Value, ContextId + ".Delta")["ValueType"] = "string",
            ["writable field"] = Value => Member(Value, ContextId + ".After")["Writable"] = true,
            ["renamed field"] = Value => Member(Value, ContextId + ".Item")["Name"] = "ShortName",
            ["context alias"] = Value => Type(Value, ContextId)["Representation"] = "Alias",
            ["context preview"] = Value => Type(Value, ContextId)["Preview"] = "ReadMock",
            ["signal preview"] = Value => Member(Value, SignalId)["Preview"] = "ReadMock",
            ["read preview"] = Value => Member(Value, "PlayerInventory.CountItem")["Preview"] = "Unavailable",
            ["callback type"] = Value => Member(Value, SignalId)["Signatures"]![0]!["Parameters"]![0]!["Type"] = "Player",
            ["callback name"] = Value => Member(Value, SignalId)["Signatures"]![0]!["Parameters"]![0]!["Name"] = "Player",
            ["optional callback"] = Value => Member(Value, SignalId)["Signatures"]![0]!["Parameters"]![0]!["Optional"] = true,
            ["variadic callback"] = Value => Member(Value, SignalId)["Signatures"]![0]!["Parameters"]![0]!["Variadic"] = true,
            ["empty callback"] = Value => Member(Value, SignalId)["Signatures"]![0]!["Parameters"] = new JArray(),
            ["callback return"] = Value => Member(Value, SignalId)["Signatures"]![0]!["Returns"] = new JArray("boolean"),
            ["callback overload"] = Value => ((JArray)Member(Value, SignalId)["Signatures"]!).Add(Member(Value, SignalId)["Signatures"]![0]!.DeepClone()),
            ["historical context introduction"] = Value => Type(Value, ContextId)["Availability"]!["SinceApi"] = "0.4.0-experimental",
            ["historical signal introduction"] = Value => Member(Value, SignalId)["Availability"]!["SinceApi"] = "0.6.0-experimental",
            ["historical field introduction"] = Value => Member(Value, ContextId + ".Delta")["Availability"]!["SinceApi"] = "0.5.0-experimental",
            ["future introduction"] = Value => Type(Value, ContextId)["Availability"]!["SinceApi"] = "99.0.0-experimental",
            ["extra inventory signal"] = Value => {
                var Extra = (JObject)Member(Value, SignalId).DeepClone(); Extra["Id"] = "PlayerInventory.ItemAdded"; Extra["Name"] = "ItemAdded";
                ((JArray)Value["Members"]!).Add(Extra);
            }
        };
        foreach (var Drift in Drifts) {
            var Broken = (JObject)Catalog.DeepClone(); Drift.Value(Broken);
            Reject(() => Contract(Broken), "C3 exact contract accepted " + Drift.Key + " drift");
        }

        // Mutate only in-memory source. Do not synthesize runtime annotations,
        // hand-edit generated catalogs or build a preview inventory backend.
        foreach (var Binding in new Dictionary<string, string> {
            ["function PlayerMethods.GetInventory(Player)"] = "function PlayerMethods.GetInventory(Player, Extra)",
            ["function PlayerInventoryMethods.CountItem(Inventory, ShortName)"] = "function PlayerInventoryMethods.CountItem(Inventory, Item)",
            ["function PlayerInventoryMethods.HasItem(Inventory, ShortName, Amount)"] = "function PlayerInventoryMethods.HasItem(Inventory, Amount, ShortName)",
            ["        if Key == \"ItemChanged\" then"] = "        if Key == \"ItemAdded\" then",
            ["        InventoryContext.Item = Fields[5]"] = "",
            ["        InventoryContext.Before = tonumber(Fields[7])"] = "",
            ["        InventoryContext.After = tonumber(Fields[8])"] = "",
            ["        InventoryContext.Delta = tonumber(Fields[9])"] = ""
        }) {
            string Broken = ChangeBinding(Bootstrap, Binding.Key, Binding.Value);
            Reject(() => ApiCatalog.Build(Broken, Release, Native), "C3 source binding/signature drift accepted: " + Binding.Key);
        }
        foreach (string Id in new[] { ContextId, SignalId, ContextId + ".Item", ContextId + ".Before", ContextId + ".After", ContextId + ".Delta" }) {
            string Broken = ChangeAnnotation(Bootstrap, Id, Declaration => Declaration["SinceApi"] = "0.4.0-experimental");
            Reject(() => Contract(ApiCatalog.Build(Broken, Release, Native)), "C3 source SinceApi drift accepted: " + Id);
        }
        foreach (string Field in new[] { "Item", "Before", "After", "Delta" }) {
            string Broken = ChangeAnnotation(Bootstrap, ContextId + "." + Field, Declaration => Declaration["Writable"] = true);
            Reject(() => Contract(ApiCatalog.Build(Broken, Release, Native)), "C3 source writable field drift accepted: " + Field);
        }
        string WrongCallback = ChangeAnnotation(Bootstrap, SignalId, Declaration => Declaration["Args"] = new JArray { new JArray("Context", "Player") });
        Reject(() => Contract(ApiCatalog.Build(WrongCallback, Release, Native)), "C3 source named callback context drift accepted");
        string MissingAnnotation = string.Join('\n', Bootstrap.Split('\n').Where(Line =>
            !Line.StartsWith("-- @carbonluau-api {\"Owner\":\"PlayerInventory\",\"Name\":\"ItemChanged\"", StringComparison.Ordinal)));
        Check(MissingAnnotation != Bootstrap, "C3 Signal annotation removal did not target source");
        Reject(() => Contract(ApiCatalog.Build(MissingAnnotation, Release, Native)), "C3 missing Signal annotation accepted");
        Console.WriteLine("[CarbonLuau:InventoryChangedTypes] PASS exact immutable context, named Signal callback, source signatures, SinceApi and unavailable-preview/read-mock drift");
    }
}
