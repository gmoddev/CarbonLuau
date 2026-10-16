using CarbonLuau.Core;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

internal static class InventoryChangedTypes
{
    private const string SinceApi = "0.6.5-experimental";
    private const string CurrentApi = "0.7.5-experimental";
    private const string ContextId = "InventoryChangeContext";
    private const string SignalId = "PlayerInventory.ItemChanged";
    private static void Check(bool Value, string Message, [System.Runtime.CompilerServices.CallerArgumentExpression("Value")] string Expression = "", [System.Runtime.CompilerServices.CallerFilePath] string File = "", [System.Runtime.CompilerServices.CallerLineNumber] int Line = 0)
    {
        if (!Value) TestDiagnostics.Pass(Value, Expression, File, Line); if (!Value) throw new InvalidOperationException(Message); }
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
    private static void Availability(JObject Declaration, string Preview, string Introduction = SinceApi, string Qualification = "Experimental")
    {
        Check((string?)Declaration["Availability"]!["SinceApi"] == Introduction &&
            (bool?)Declaration["Availability"]!["Implemented"] == true &&
            (string?)Declaration["Availability"]!["Qualification"] == Qualification &&
            (string?)Declaration["Preview"] == Preview, "C3 introduction/qualification/preview drift");
    }
    private static void Contract(JObject Catalog)
    {
        ApiCatalog.Validate(Catalog);
        Check((string?)Catalog["Api"]!["Version"] == CurrentApi, "C4 current API identity drift");
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
            .SequenceEqual(new[] { "PlayerInventory.CountItem", "PlayerInventory.GiveItem", "PlayerInventory.HasItem", SignalId, "PlayerInventory.TakeItem" }),
            "C4 inventory must expose exactly two reads, two mutations and ItemChanged");
        foreach (string Name in new[] { "CountItem", "HasItem", "GiveItem", "TakeItem" })
            Check(!((JArray)Catalog["Members"]!).Any(Value => (string?)Value["Id"] == "Player." + Name ||
                ((string?)Value["OwnerId"] == "Player" && (string?)Value["Name"] == Name)),
                "C4 legacy Player inventory member remains: " + Name);
        Check(!((JArray)Catalog["Types"]!).Any(Value => (string?)Value["Id"] == "Inventory"),
            "C3 must not introduce a global Inventory service");
        Availability(Type(Catalog, "PlayerInventory"), "ReadMock");
        foreach (string Id in new[] { "Player.GetInventory", "PlayerInventory.CountItem", "PlayerInventory.HasItem" })
            Availability(Member(Catalog, Id), "ReadMock");
        Signature(Member(Catalog, "Player.GetInventory"), "Method", Array.Empty<string>(), Array.Empty<string>(), new[] { "PlayerInventory" });
        Signature(Member(Catalog, "PlayerInventory.CountItem"), "Method", new[] { "ShortName" }, new[] { "string" }, new[] { "number" });
        Signature(Member(Catalog, "PlayerInventory.HasItem"), "Method", new[] { "ShortName", "Amount" }, new[] { "string", "number?" }, new[] { "boolean" });
        foreach (string Id in new[] { "PlayerInventory.GiveItem", "PlayerInventory.TakeItem" })
            Availability(Member(Catalog, Id), "HostMutation", "0.7.0-experimental", "ClientUnqualified");
        Signature(Member(Catalog, "PlayerInventory.GiveItem"), "Method", new[] { "ShortName", "Amount", "Behavior" },
            new[] { "string", "number", "GiveItemBehavior?" }, new[] { "boolean" });
        Signature(Member(Catalog, "PlayerInventory.TakeItem"), "Method", new[] { "ShortName", "Amount" },
            new[] { "string", "number" }, new[] { "boolean" });
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
        Check((string?)Release["apiVersion"] == CurrentApi && (string?)Release["packageVersion"] == "0.7.0",
            "complete C4 release candidate must use its authorized package identity");
        string Definitions = ApiArtifacts.Definitions(Catalog);
        string PlayerDefinition = Regex.Match(Definitions, @"(?ms)^declare extern type Player with\n(.*?)^end$").Groups[1].Value;
        string InventoryDefinition = Regex.Match(Definitions, @"(?ms)^declare extern type PlayerInventory with\n(.*?)^end$").Groups[1].Value;
        Check(PlayerDefinition.Contains("function GetInventory(self): (PlayerInventory)", StringComparison.Ordinal) &&
            !Regex.IsMatch(PlayerDefinition, @"function (CountItem|HasItem|GiveItem|TakeItem)\("),
            "C4 definitions retained legacy Player inventory members or lost GetInventory");
        Check(InventoryDefinition == "    function CountItem(self, ShortName: string): (number)\n" +
            "    function GiveItem(self, ShortName: string, Amount: number, Behavior: GiveItemBehavior?): (boolean)\n" +
            "    function HasItem(self, ShortName: string, Amount: number?): (boolean)\n" +
            "    read ItemChanged: SignalWith<(Context: InventoryChangeContext) -> ()>\n" +
            "    function TakeItem(self, ShortName: string, Amount: number): (boolean)\n",
            "C4 definitions must expose exactly five inventory members with unchanged signatures");
        Check(Definitions.Contains("read ItemChanged: SignalWith<(Context: InventoryChangeContext) -> ()>", StringComparison.Ordinal) &&
            Definitions.Contains("export type InventoryChangeContext = {\n    read After: number,\n    read Before: number,\n    read Delta: number,\n    read Item: string,\n}", StringComparison.Ordinal),
            "C3 generated callback must use the named four-field context");
        Check(Definitions.Contains("read Connect: (self: SignalWith<Callback>, Callback: Callback) -> (Connection)", StringComparison.Ordinal) &&
            Definitions.Contains("read PlayerAdded: Signal\n", StringComparison.Ordinal),
            "C3 specialization must preserve Connection and legacy Player Signal");
        var Documentation = ApiArtifacts.Documentation(Catalog);
        foreach (string Id in new[] { SignalId, "PlayerInventory.GiveItem", "PlayerInventory.TakeItem", ContextId + ".Item", ContextId + ".Before", ContextId + ".After", ContextId + ".Delta" })
            Check(Documentation["@carbonluau/globaltype/" + Id] != null, "C3 generated documentation omits " + Id);
        var Rebuilt = ApiCatalog.Build(Bootstrap, Release, Native);
        Check(JToken.DeepEquals(Catalog, Rebuilt) && Definitions == ApiArtifacts.Definitions(Rebuilt), "C3 generation is nondeterministic");

        var Drifts = new Dictionary<string, Action<JObject>> {
            ["current API"] = Value => Value["Api"]!["Version"] = SinceApi,
            ["mutation introduction"] = Value => Member(Value, "PlayerInventory.GiveItem")["Availability"]!["SinceApi"] = SinceApi,
            ["mutation qualification"] = Value => Member(Value, "PlayerInventory.TakeItem")["Availability"]!["Qualification"] = "Experimental",
            ["mutation preview"] = Value => Member(Value, "PlayerInventory.GiveItem")["Preview"] = "ReadMock",
            ["mutation behavior type"] = Value => Member(Value, "PlayerInventory.GiveItem")["Signatures"]![0]!["Parameters"]![2]!["Type"] = "string?",
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
        foreach (string Name in new[] { "CountItem", "HasItem", "GiveItem", "TakeItem" }) {
            var Broken = (JObject)Catalog.DeepClone();
            var Legacy = (JObject)Member(Broken, "PlayerInventory." + Name).DeepClone();
            Legacy["Id"] = "Player." + Name; Legacy["OwnerId"] = "Player";
            ((JArray)Broken["Members"]!).Add(Legacy);
            Reject(() => Contract(Broken), "C4 legacy Player alias accepted: " + Name);
        }

        // Mutate only in-memory source. Do not synthesize runtime annotations,
        // hand-edit generated catalogs or build a preview inventory backend.
        foreach (var Binding in new Dictionary<string, string> {
            ["function PlayerMethods.GetInventory(Player)"] = "function PlayerMethods.GetInventory(Player, Extra)",
            ["function PlayerInventoryMethods.CountItem(Inventory, ShortName)"] = "function PlayerInventoryMethods.CountItem(Inventory, Item)",
            ["function PlayerInventoryMethods.HasItem(Inventory, ShortName, Amount)"] = "function PlayerInventoryMethods.HasItem(Inventory, Amount, ShortName)",
            ["function PlayerInventoryMethods.GiveItem(Inventory, ShortName, Amount, Behavior)"] = "function PlayerInventoryMethods.GiveItem(Inventory, ShortName, Behavior, Amount)",
            ["function PlayerInventoryMethods.TakeItem(Inventory, ShortName, Amount)"] = "function PlayerInventoryMethods.TakeItem(Inventory, Amount, ShortName)",
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
        foreach (string Id in new[] { "PlayerInventory.GiveItem", "PlayerInventory.TakeItem" }) {
            foreach (var Drift in new Dictionary<string, string> {
                ["SinceApi"] = SinceApi, ["Qualification"] = "Experimental", ["Preview"] = "ReadMock"
            }) {
                string Broken = ChangeAnnotation(Bootstrap, Id, Declaration => Declaration[Drift.Key] = Drift.Value);
                Reject(() => Contract(ApiCatalog.Build(Broken, Release, Native)), "C4 source mutation drift accepted: " + Id + "." + Drift.Key);
            }
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
        Console.WriteLine("[CarbonLuau:InventoryChangedTypes] PASS exact five-member inventory, legacy Player absence, 0.7 API/0.6.5 introductions, mutation classification and immutable sampled Signal");
    }
}
