using CarbonLuau.Core;
using Newtonsoft.Json.Linq;

internal static class GameplaySignalTypes
{
    private const string CurrentApi = "0.99.0-experimental";
    private static void Check(bool Value, string Message)
    { if (!Value) throw new InvalidOperationException(Message); }
    private static void Reject(Action Operation, string Message)
    {
        try { Operation(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(Message);
    }
    private static JObject Availability()
    { return new JObject { ["SinceApi"] = CurrentApi, ["Implemented"] = true, ["Qualification"] = "Experimental" }; }
    private static JObject Parameter(string Name, string Type)
    { return new JObject { ["Name"] = Name, ["Type"] = Type, ["Optional"] = false, ["Variadic"] = false }; }
    private static JObject SyntheticCatalog(JObject Existing)
    {
        var Catalog = (JObject)Existing.DeepClone();
        Catalog["Api"]!["Version"] = CurrentApi;
        if (!((JArray)Catalog["Types"]!).Any(Value => (string?)Value["Id"] == "PlayerDeathContext"))
            ((JArray)Catalog["Types"]!).Add(new JObject {
            ["Id"] = "PlayerDeathContext", ["Name"] = "PlayerDeathContext", ["Kind"] = "Value",
            ["Representation"] = "Record", ["Summary"] = "Synthetic context for generator tests only.",
            ["Availability"] = Availability(), ["Preview"] = "Unavailable"
        });
        if (!((JArray)Catalog["Members"]!).Any(Value => (string?)Value["Id"] == "PlayerDeathContext.Position"))
            ((JArray)Catalog["Members"]!).Add(new JObject {
            ["Id"] = "PlayerDeathContext.Position", ["Name"] = "Position", ["OwnerId"] = "PlayerDeathContext",
            ["Kind"] = "Property", ["ValueType"] = "Vector3?", ["Writable"] = false,
            ["Summary"] = "Synthetic captured position.", ["Availability"] = Availability(), ["Preview"] = "Unavailable"
        });
        foreach (JToken Previous in ((JArray)Catalog["Members"]!).Where(Value => (string?)Value["Id"] == "Players.PlayerDied").ToArray())
            Previous.Remove();
        ((JArray)Catalog["Members"]!).Add(new JObject {
            ["Id"] = "Players.PlayerDied", ["Name"] = "PlayerDied", ["OwnerId"] = "Players", ["Kind"] = "Signal",
            ["Signatures"] = new JArray(new JObject {
                ["Parameters"] = new JArray(Parameter("Player", "Player"), Parameter("Context", "PlayerDeathContext")),
                ["Returns"] = new JArray()
            }),
            ["Summary"] = "Synthetic two-argument Signal; no runtime or public availability claim.",
            ["Availability"] = Availability(), ["Preview"] = "Unavailable"
        });
        return Catalog;
    }
    private static JObject Signal(JObject Catalog)
    { return ((JArray)Catalog["Members"]!).Cast<JObject>().Single(Value => (string?)Value["Id"] == "Players.PlayerDied"); }
    private static string ExistingSignalDeclaration(string Definitions)
    {
        int Start = Definitions.IndexOf("declare extern type Signal with\n", StringComparison.Ordinal);
        Check(Start >= 0, "legacy Signal declaration missing");
        int End = Definitions.IndexOf("end\n\n", Start, StringComparison.Ordinal);
        Check(End >= 0, "legacy Signal declaration incomplete");
        return Definitions.Substring(Start, End + 5 - Start);
    }
    public static void Run(JObject Existing)
    {
        string Before = ApiArtifacts.Definitions(Existing);
        var LegacyOnly = (JObject)Existing.DeepClone();
        foreach (JObject Member in ((JArray)LegacyOnly["Members"]!).Cast<JObject>().Where(Value => (string?)Value["Kind"] == "Signal").ToArray()) {
            var Signatures = (JArray)Member["Signatures"]!;
            var Parameters = (JArray)Signatures[0]!["Parameters"]!;
            if (Signatures.Count != 1 || Parameters.Count != 1 || (string?)Parameters[0]!["Type"] != "Player" ||
                (bool)Parameters[0]!["Optional"]! || (bool)Parameters[0]!["Variadic"]!) Member.Remove();
        }
        Check(!ApiArtifacts.Definitions(LegacyOnly).Contains("export type SignalWith<", StringComparison.Ordinal),
            "legacy-only catalog acquired a specialization");
        var Catalog = SyntheticCatalog(Existing);
        string Definitions = ApiArtifacts.Definitions(Catalog);
        Check(Definitions.Contains("export type SignalWith<Callback> = {\n    read Connect: (self: SignalWith<Callback>, Callback: Callback) -> (Connection),\n}", StringComparison.Ordinal),
            "typed callback specialization lost same Connection return");
        Check(Definitions.Contains("read PlayerDied: SignalWith<(Player: Player, Context: PlayerDeathContext) -> ()>", StringComparison.Ordinal),
            "Signal event parameters were erased from Connect callback type");
        Check(Definitions.Contains("read PlayerAdded: Signal\n", StringComparison.Ordinal) &&
            Definitions.Contains("read PlayerRemoving: Signal\n", StringComparison.Ordinal) &&
            Definitions.Contains("read Activated: Signal\n", StringComparison.Ordinal) &&
            ExistingSignalDeclaration(Before) == ExistingSignalDeclaration(Definitions), "legacy Players/GUI/Signal declarations changed");
        Check(Definitions == ApiArtifacts.Definitions(Catalog), "Signal specialization generation is nondeterministic");
        Check(!ApiArtifacts.Documentation(Catalog).Properties().Any(Value => Value.Name.Contains("SignalWith", StringComparison.Ordinal)),
            "static callback adapter escaped into catalog documentation");
        var Changed = (JObject)Catalog.DeepClone();
        Signal(Changed)["Signatures"]![0]!["Parameters"]![1]!["Type"] = "string";
        Check(ApiArtifacts.Definitions(Changed).Contains("Context: string) -> ()>", StringComparison.Ordinal),
            "Signal callback type does not follow actual member signature");
        Changed = (JObject)Catalog.DeepClone();
        ((JArray)Signal(Changed)["Signatures"]!).Add(new JObject {
            ["Parameters"] = new JArray(Parameter("Player", "Player")), ["Returns"] = new JArray()
        });
        Check(ApiArtifacts.Definitions(Changed).Contains(
            "read PlayerDied: SignalWith<((Player: Player, Context: PlayerDeathContext) -> ()) & ((Player: Player) -> ())>",
            StringComparison.Ordinal), "Signal signature alternatives lost callback overloads");
        Changed = (JObject)Catalog.DeepClone();
        Signal(Changed)["Signatures"]![0]!["Parameters"] = new JArray();
        Check(ApiArtifacts.Definitions(Changed).Contains("read PlayerDied: SignalWith<() -> ()>", StringComparison.Ordinal),
            "zero-argument Signal was incorrectly typed as a Player callback");
        var Broken = (JObject)Catalog.DeepClone();
        Signal(Broken)["Signatures"]![0]!["Parameters"]![1]!["Type"] = "MissingContext";
        Reject(() => ApiArtifacts.Definitions(Broken), "unknown context type drift accepted");
        Broken = (JObject)Catalog.DeepClone();
        Signal(Broken)["Signatures"]![0]!["Parameters"]![1]!["Name"] = "Player";
        Reject(() => ApiArtifacts.Definitions(Broken), "duplicate Signal callback argument accepted");
        Broken = (JObject)Catalog.DeepClone();
        Signal(Broken)["Signatures"]![0]!["Parameters"]![0]!["Optional"] = true;
        Reject(() => ApiArtifacts.Definitions(Broken), "required Signal argument after optional accepted");
        Broken = (JObject)Catalog.DeepClone();
        Signal(Broken)["Signatures"]![0]!["Returns"] = new JArray("boolean");
        Reject(() => ApiArtifacts.Definitions(Broken), "observation Signal acquired a decision return");
        Broken = (JObject)Catalog.DeepClone();
        Signal(Broken)["Signatures"] = new JArray();
        Reject(() => ApiArtifacts.Definitions(Broken), "Signal without callback signature accepted");
        Broken = (JObject)Catalog.DeepClone();
        ((JArray)Broken["Types"]!).Add(new JObject {
            ["Id"] = "SignalWith", ["Name"] = "SignalWith", ["Kind"] = "Value", ["Representation"] = "Alias",
            ["TypeExpression"] = "string", ["Summary"] = "Synthetic conflicting type.",
            ["Availability"] = Availability(), ["Preview"] = "Unavailable"
        });
        Reject(() => ApiArtifacts.Definitions(Broken), "static specialization alias collision accepted");
        ExportLspEvidence(Definitions);
        Console.WriteLine("[CarbonLuau:GameplaySignalTypes] PASS synthetic context callback, legacy compatibility, deterministic generation and invalid signature drift");
    }

    private static void ExportLspEvidence(string Definitions)
    {
        string? Requested = Environment.GetEnvironmentVariable("CARBONLUAU_GAMEPLAY_TYPE_EVIDENCE_DIR");
        if (String.IsNullOrEmpty(Requested)) return;
        string DirectoryPath = Path.GetFullPath(Requested);
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "SyntheticGameplay.d.luau"), Definitions);
        File.WriteAllText(Path.Combine(DirectoryPath, "Valid.luau"), "--!strict\n" +
            "local Players = game:GetService('Players')\n" +
            "local DeathConnection = Players.PlayerDied:Connect(function(Player, Context)\n" +
            "    local Name: string = Player.Name\n" +
            "    local Position: Vector3? = Context.Position\n" +
            "    if Position then local X: number = Position.X; print(Name, X) end\n" +
            "end)\nDeathConnection:Disconnect()\n" +
            "local AddedConnection = Players.PlayerAdded:Connect(function(Player)\n" +
            "    local UserId: string = Player.UserId; print(UserId)\n" +
            "end)\nAddedConnection:Disconnect()\n");
        File.WriteAllText(Path.Combine(DirectoryPath, "WrongCallback.luau"), "--!strict\n" +
            "game:GetService('Players').PlayerDied:Connect(function(Player: string, Context: number)\n" +
            "    print(Player, Context)\nend)\n");
        File.WriteAllText(Path.Combine(DirectoryPath, "ReadonlyContext.luau"), "--!strict\n" +
            "game:GetService('Players').PlayerDied:Connect(function(Player, Context)\n" +
            "    Context.Position = nil\n    print(Player.Name)\nend)\n");
        File.WriteAllText(Path.Combine(DirectoryPath, "UnknownContextField.luau"), "--!strict\n" +
            "game:GetService('Players').PlayerDied:Connect(function(Player, Context)\n" +
            "    print(Player.Name, Context.UnqualifiedCause)\nend)\n");
        Console.WriteLine("[CarbonLuau:GameplaySignalTypes] LSP_EVIDENCE " + DirectoryPath);
    }
}
