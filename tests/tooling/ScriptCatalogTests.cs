using CarbonLuau.Core;
using Newtonsoft.Json.Linq;

internal static class ScriptCatalogTests
{
    internal static void Run(string Root, JObject Catalog, string Bootstrap, JObject Release, string Native)
    {
        void Check(bool Value, string Message) { if (!Value) throw new Exception("CATALOG-D9: " + Message); }
        void Reject(string Changed) {
            try { ApiCatalog.Build(Bootstrap, Release, Changed); }
            catch (InvalidOperationException) { return; }
            throw new Exception("CATALOG-D9 native drift accepted");
        }
        var Snapshot = JObject.Parse(File.ReadAllText(Path.Combine(Root, "generated/catalog-symbols.json")));
        var Types = ((JArray)Catalog["Types"]!).Cast<JObject>().ToDictionary(Value => (string)Value["Id"]!);
        var Members = ((JArray)Catalog["Members"]!).Cast<JObject>().ToDictionary(Value => (string)Value["Id"]!);
        string Definitions = ApiArtifacts.Definitions(Catalog);
        var Documentation = ApiArtifacts.Documentation(Catalog);
        Check((string?)Types["Item"]["Kind"] == "Global" && (string?)Types["Prefab"]["Kind"] == "Global" &&
            (string?)Types["PrefabLoot"]["Representation"] == "Record", "ordinary global/record namespaces");
        var Rows = (JArray)Snapshot["Items"]!;
        Check(Rows.Count == 221 && Members.Values.Count(Value => (string?)Value["OwnerId"] == "Item") == 221 &&
            Members.Values.Count(Value => (string?)Value["OwnerId"] == "PrefabLoot") == 2 &&
            Members.Values.Count(Value => (string?)Value["OwnerId"] == "Prefab") == 1, "exact finite public members");
        foreach (JObject Row in Rows) {
            string Symbol = (string)Row["Symbol"]!, Name = (string)Row["Name"]!;
            var Member = Members["Item." + Symbol];
            Check((string?)Member["ValueType"] == "\"" + Name + "\"" && (bool?)Member["Writable"] == false &&
                (string?)Member["Kind"] == "Singleton" && (string?)Member["Preview"] == "Pure" &&
                (string?)Member["Availability"]!["SinceApi"] == "0.7.5-experimental", "literal readonly Pure Item field: " + Name);
            Check(Definitions.Contains("    read " + Symbol + ": \"" + Name + "\",\n", StringComparison.Ordinal) &&
                ((string?)Documentation["@carbonluau/global/Item." + Symbol]!["documentation"])!.Contains(Name, StringComparison.Ordinal),
                "definitions/documentation exact field projection");
        }
        Check(Definitions.Contains("declare Prefab: {\n    read Loot: PrefabLoot,\n}", StringComparison.Ordinal) &&
            !Definitions.Contains("declare extern type Item ", StringComparison.Ordinal) &&
            !Definitions.Contains("declare extern type Prefab ", StringComparison.Ordinal), "nested static readonly namespace formatting");
        Check(Documentation["@carbonluau/global/Prefab.Loot"] != null, "nested global documentation key");
        Check(Members["Loot.RegisterDropRule"]["Signatures"]![0]!["Parameters"]![0]!["Type"]!.Value<string>() == "string" &&
            Members["PlayerInventory.GiveItem"]["Signatures"]![0]!["Parameters"]![0]!["Type"]!.Value<string>() == "string",
            "catalog must not narrow existing string signatures");
        Reject(Native.Replace("lua_setglobal(State, \"Item\");", "lua_setglobal(State, \"Other\");", StringComparison.Ordinal));
        Reject(Native.Replace("// Catalog SHA256: ", "// Catalog SHA256: 0", StringComparison.Ordinal));
        Reject(Native.Replace("{\"Scrap\", \"scrap\"}", "{\"Scrap\", \"wood\"}", StringComparison.Ordinal));
        Reject(Native + "\n" + Native.Substring(Native.IndexOf("constexpr const char* CatalogItems", StringComparison.Ordinal)));
        Console.WriteLine("[CarbonLuau:CatalogMetadata] PASS exact 221/2 fields, literal strings, nested readonly namespaces, Pure access, 0.7.5 identity, docs and runtime registration/data drift rejection");
    }
}
