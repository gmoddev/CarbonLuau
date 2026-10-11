using CarbonLuau.Core;
using Newtonsoft.Json.Linq;
using System.Globalization;

string[] Args = Environment.GetCommandLineArgs().Skip(1).ToArray();
string Root = Path.GetFullPath(Args.Length == 0 ? "../.." : Args[0]);
string Bootstrap = File.ReadAllText(Path.Combine(Root, "scripts/bootstrap.luau")).Replace("\r\n", "\n");
string Native = (File.ReadAllText(Path.Combine(Root, "native/src/scripts/ModuleLoader.cpp")) + "\n" +
    File.ReadAllText(Path.Combine(Root, "native/src/facade/PersistenceFacade.cpp"))).Replace("\r\n", "\n");
JObject Release = JObject.Parse(File.ReadAllText(Path.Combine(Root, "release.json")));
JObject Catalog = ApiCatalog.Build(Bootstrap, Release, Native);
void Check(bool Value, string Message) { if (!Value) throw new Exception(Message); }
void Reject(Action Action, string Message) { try { Action(); } catch (InvalidOperationException) { return; } throw new Exception(Message); }
string Definitions = ApiArtifacts.Definitions(Catalog);
void CheckLootCatalog(JObject Value)
{
    var Types = ((JArray)Value["Types"]!).Cast<JObject>().ToDictionary(Type => (string)Type["Id"]!);
    var Members = ((JArray)Value["Members"]!).Cast<JObject>().ToDictionary(Member => (string)Member["Id"]!);
    Check((string?)Types["Enum"]["Kind"] == "Global" && (string?)Types["DropMode"]["Kind"] == "Enum" &&
        (string?)Types["Loot"]["Kind"] == "Service", "D3 namespace/value/service classification drift");
    Check((string?)Types["LootDropEntry"]["Kind"] == "Value" &&
        (string?)Types["LootDropEntry"]["Representation"] == "Alias" &&
        (string?)Types["LootDropEntry"]["TypeExpression"] == "{Item: string, Min: number, Max: number}",
        "D3 ordinary drop-entry alias drift");
    foreach (string Owner in new[] { "Enum", "DropMode", "Loot", "LootDropEntry" }) {
        string[] Expected = Owner == "Enum" ? new[] { "Enum.DropMode" } : Owner == "DropMode" ? new[] { "DropMode.Replace" } :
            Owner == "Loot" ? new[] { "Loot.RegisterDropRule" } : Array.Empty<string>();
        Check(Members.Values.Where(Member => (string?)Member["OwnerId"] == Owner)
            .Select(Member => (string)Member["Id"]!).OrderBy(Id => Id, StringComparer.Ordinal).SequenceEqual(Expected), "D3 acquired an extra public member: " + Owner);
    }
    var Namespace = Members["Enum.DropMode"];
    Check((string?)Namespace["Kind"] == "Property" && (bool?)Namespace["Writable"] == false &&
        (string?)Namespace["ValueType"] == "DropMode" && (string?)Members["DropMode.Replace"]["Kind"] == "Singleton" &&
        (string?)Members["DropMode.Replace"]["ValueType"] == "DropMode" && (bool?)Members["DropMode.Replace"]["Writable"] == false,
        "D3 frozen nested namespace lost nominal value type");
    var Rule = Members["Loot.RegisterDropRule"];
    var Signature = (JObject)((JArray)Rule["Signatures"]!).Single();
    var Parameters = ((JArray)Signature["Parameters"]!).Cast<JObject>().ToArray();
    Check((string?)Rule["Kind"] == "Method" &&
        string.Join(",", Parameters.Select(Parameter => (string?)Parameter["Name"])) == "Prefab,Mode,Drops" &&
        string.Join(",", Parameters.Select(Parameter => (string?)Parameter["Type"])) == "string,DropMode,{LootDropEntry}" &&
        Parameters.All(Parameter => (bool?)Parameter["Optional"] == false && (bool?)Parameter["Variadic"] == false) &&
        ((JArray)Signature["Returns"]!).Count == 0, "D3 declaration must have exactly three required positional arguments and no return");
    foreach (JObject Declaration in Types.Values.Concat(Members.Values).Where(Declaration =>
        new[] { "Enum", "DropMode", "Loot", "LootDropEntry" }.Contains((string?)Declaration["Id"]) ||
        new[] { "Enum", "DropMode", "Loot" }.Contains((string?)Declaration["OwnerId"])))
        Check((string?)Declaration["Availability"]!["SinceApi"] == "0.6.5-experimental" &&
            (string?)Declaration["Availability"]!["Qualification"] == "Experimental" &&
            (string?)Declaration["Preview"] == ((string?)Declaration["Id"] == "LootDropEntry" ? "Unavailable" :
                (string?)Declaration["Id"] == "Loot" || (string?)Declaration["OwnerId"] == "Loot" ? "HostMutation" : "Pure"),
            "D3 availability/preview drift");
    var Services = ((JArray)Members["Game.GetService"]["Signatures"]!).Cast<JObject>();
    Check(Services.Count(Service => (string?)Service["Parameters"]![0]!["Type"] == "\"Loot\"" &&
        (string?)Service["Returns"]![0] == "Loot") == 1 &&
        !Services.Any(Service => new[] { "\"Enum\"", "\"DropMode\"", "\"LootDropEntry\"" }
            .Contains((string?)Service["Parameters"]![0]!["Type"])), "D3 namespace leaked into service discovery");
    string Text = ApiArtifacts.Definitions(Value);
    Check(Text.Contains("declare Enum: {\n    read DropMode: {read Replace: DropMode},\n}") &&
        Text.Contains("declare extern type DropMode with\nend") && !Text.Contains("declare DropMode:") &&
        Text.Contains("export type LootDropEntry = {Item: string, Min: number, Max: number}") &&
        Text.Contains("function RegisterDropRule(self, Prefab: string, Mode: DropMode, Drops: {LootDropEntry}): ()"),
        "D3 generated namespace/nominal type/alias/signature drift");
    var Documentation = ApiArtifacts.Documentation(Value);
    Check(Documentation["@carbonluau/global/Enum.DropMode"] != null &&
        Documentation["@carbonluau/global/Enum.DropMode.Replace"] != null &&
        Documentation["@carbonluau/global/DropMode.Replace"] == null &&
        Documentation["@carbonluau/globaltype/Loot.RegisterDropRule"] != null,
        "D3 generated documentation lost namespace or method");
}
// Qualify current implementation-owned annotations entirely in memory. The
// focused mode never writes artifacts or requires main's pending regeneration.
string LootBootstrap = Bootstrap;
JObject LootCatalog = Catalog;
CheckLootCatalog(LootCatalog);
Check(JToken.DeepEquals(LootCatalog, ApiCatalog.Build(LootBootstrap, Release, Native)) &&
    ApiArtifacts.Definitions(LootCatalog) == ApiArtifacts.Definitions(ApiCatalog.Build(LootBootstrap, Release, Native)),
    "D3 generation repeat determinism");
Check(Definitions.Contains("declare GiveItemBehavior: {\n    read InventoryOnly: GiveItemBehavior,\n}") &&
    ApiArtifacts.Documentation(Catalog)["@carbonluau/global/GiveItemBehavior.InventoryOnly"] != null,
    "D3 changed existing GiveItemBehavior global");
Reject(() => ApiCatalog.Build(LootBootstrap + "\nfunction Loot.Future(Self) end\n", Release, Native), "unannotated Loot method accepted");
Reject(() => ApiCatalog.Build(LootBootstrap.Replace("\nlocal Enum = table.freeze({DropMode = DropMode})\n", "\n"), Release, Native),
    "D3 removed enum namespace binding accepted");
Reject(() => ApiCatalog.Build(LootBootstrap.Replace("\nlocal DropMode = table.freeze({Replace = MakeUserdata(DropModeMeta)})\n", "\n"), Release, Native),
    "D3 removed typed Replace binding accepted");
Reject(() => ApiCatalog.Build(LootBootstrap.Replace("    if Name == \"Loot\" then return Loot end\n", ""), Release, Native),
    "D3 removed Loot service registration accepted");
var LootBinding = System.Text.RegularExpressions.Regex.Match(LootBootstrap,
    @"(?m)^function Loot\.RegisterDropRule\([^\r\n]+\)").Value;
Check(LootBinding.Length > 0, "D3 declaration wrapper missing");
Reject(() => ApiCatalog.Build(LootBootstrap.Replace("    local Prefab, Mode, Drops = ...", "    local Prefab, Mode, Entries = ..."), Release, Native),
    "D3 changed argument names accepted");
Reject(() => ApiCatalog.Build(LootBootstrap.Replace("\n" + LootBinding + "\n", "\n" + LootBinding.Replace(", ...", "") + "\n"), Release, Native),
    "D3 removed surplus-argument guard accepted");
Reject(() => ApiCatalog.Build(LootBootstrap.Replace("if select(\"#\", ...) ~= 3 then Fail(", "if select(\"#\", ...) ~= 4 then Fail("), Release, Native),
    "D3 changed exact-arity guard accepted");
var InvalidLoot = (JObject)LootCatalog.DeepClone();
((JArray)InvalidLoot["Members"]!).Single(Member => (string?)Member["Id"] == "Enum.DropMode")["ValueType"] = "MissingDropMode";
Reject(() => ApiCatalog.Validate(InvalidLoot), "D3 unknown nested enum value type accepted");
InvalidLoot = (JObject)LootCatalog.DeepClone();
((JArray)InvalidLoot["Types"]!).Single(Type => (string?)Type["Id"] == "LootDropEntry")["TypeExpression"] = "{Item: MissingItem, Min: number, Max: number}";
Reject(() => ApiCatalog.Validate(InvalidLoot), "D3 unknown drop-entry field type accepted");
Console.WriteLine("[CarbonLuau:ToolingTests] PASS D3 namespace, nominal enum, entry alias, exact signature, discovery and negative drift; in-memory metadata only");
if (Args.Contains("--d3-sidecar")) return;
InventoryChangedTypes.Run(Catalog, Bootstrap, Release, Native);
if (Args.Contains("--c3-sidecar") || Args.Contains("--c4-sidecar")) return;
Check(Definitions == File.ReadAllText(Path.Combine(Root, "generated/carbonluau.d.luau")).Replace("\r\n", "\n"), "definition golden drift");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
Check(JToken.DeepEquals(Catalog, ApiCatalog.Build(Bootstrap, Release, Native)), "catalog repeat/culture determinism");
Check(Definitions == ApiArtifacts.Definitions(ApiCatalog.Build(Bootstrap, Release, Native)), "definition repeat determinism");
GameplaySignalTypes.Run(Catalog);
Check(Definitions.Contains("GiveItem") && Definitions.Contains("GiveItemBehavior") && Definitions.Contains("InventoryOnly") && !Definitions.Contains("TextBox") && Definitions.Contains("TakeItem"), "current implemented inventory API exposure");
Check(Definitions.Contains("declare extern type Entity") && Definitions.Contains("declare extern type Workspace") &&
    Definitions.Contains("function GetEntityById(self, Id: string): (Entity?)") &&
    Definitions.Contains("read Id: string") && Definitions.Contains("read Prefab: string") &&
    Definitions.Contains("read Position: Vector3") && !Definitions.Contains("IsValid"),
    "Entity-1C public definition differs from the read-only runtime");
Check((string)Catalog["Api"]!["Version"]! == (string)Release["apiVersion"]!, "API identity");
Reject(() => ApiCatalog.Build(Bootstrap + "\nfunction PlayerMethods.Future(Player) end\n", Release, Native), "unannotated binding accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("\nfunction PlayerMethods.Teleport(Player, Position)\n", "\nfunction PlayerMethods.Teleport(Player, Target)\n"), Release, Native), "changed signature accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("\nfunction Workspace.GetEntityById(Self, Id)\n", "\nfunction Workspace.GetEntityById(Self, Key)\n"), Release, Native), "changed Entity lookup signature accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("    PermanentMarker = Font(\"PermanentMarker\"),", ""), Release, Native), "removed singleton accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("    if Name == \"Items\" then return Items end\n", ""), Release, Native), "removed service accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("    if Name == \"Workspace\" then return Workspace end\n", ""), Release, Native), "removed Workspace service accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("-- @carbonluau-api {\"Type\":\"Workspace\"", "-- @removed-api {\"Type\":\"Workspace\""), Release, Native), "unannotated Workspace service accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("\nWorkspace.EntityDied = Signal(\"entitydied\")\n", "\n"), Release, Native),
    "removed EntityDied Signal binding accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("        DeathContext.Id = Fields[4]\n", ""), Release, Native),
    "removed EntityDeathContext scalar binding accepted");
Reject(() => ApiCatalog.Build(Bootstrap, Release, Native.Replace("        lua_setfield(State, -2, \"IsDependencyAvailable\");", "")), "removed native binding accepted");
var Broken = (JObject)Catalog.DeepClone();
((JObject)Broken["Types"]![0]!)["BaseTypeId"] = "Missing";
Reject(() => ApiCatalog.Validate(Broken), "unknown inheritance accepted");
Broken = (JObject)Catalog.DeepClone();
((JObject)Broken["Members"]![0]!)["ValueType"] = "UnimplementedType";
Reject(() => ApiCatalog.Validate(Broken), "unknown type accepted");
var PersistenceTypes = new[] { "DataStoreService", "DataStore", "DataStoreOptions", "DataStoreQuery", "DataStoreQueryResult", "PersistedValue" };
var EntityTypes = new[] { "Workspace", "Entity", "EntityDiscoveryOptions" };
foreach (JObject Declaration in ((JArray)Catalog["Types"]!).Concat((JArray)Catalog["Members"]!)) {
    bool Persistence = PersistenceTypes.Contains((string?)Declaration["Id"]) || PersistenceTypes.Contains((string?)Declaration["OwnerId"]);
    bool Entity = EntityTypes.Contains((string?)Declaration["Id"]) || EntityTypes.Contains((string?)Declaration["OwnerId"]);
    bool GameplayA = new[] { "PlayerDeathContext", "PlayerSpawnContext", "EntityDestroyedContext", "EntityDeathContext", "PlayerInventory", "InventoryChangeContext" }.Contains((string?)Declaration["Id"]) ||
        new[] { "PlayerDeathContext", "PlayerSpawnContext", "EntityDestroyedContext", "EntityDeathContext", "PlayerInventory", "InventoryChangeContext" }.Contains((string?)Declaration["OwnerId"]) ||
        new[] { "Players.PlayerDied", "Players.PlayerSpawned", "Workspace.EntitySpawned", "Workspace.EntityDestroyed", "Workspace.EntityDied", "Player.GetInventory" }.Contains((string?)Declaration["Id"]);
    bool Loot = new[] { "Enum", "DropMode", "Loot", "LootDropEntry" }.Contains((string?)Declaration["Id"]) ||
        new[] { "Enum", "DropMode", "Loot" }.Contains((string?)Declaration["OwnerId"]);
    bool InventoryMutation = new[] { "PlayerInventory.GiveItem", "PlayerInventory.TakeItem" }.Contains((string?)Declaration["Id"]);
    Version Since = Version.Parse(((string)Declaration["Availability"]!["SinceApi"]!).Split('-')[0]);
    Check(InventoryMutation ? Since == new Version(0, 7, 0) : GameplayA || Loot ? Since == new Version(0, 6, 5) : Entity ? Since == new Version(0, 6, 0) :
        Persistence ? Since == new Version(0, 5, 0) : Since <= new Version(0, 4, 0), "historical introduction version changed");
    if (Persistence) Check((string)Declaration["Availability"]!["Qualification"]! == "Experimental" &&
        (string)Declaration["Preview"]! == "Unavailable", "persistence qualification/preview drift");
}
var DestroyedSignal = ((JArray)Catalog["Members"]!).Cast<JObject>().Single(Value => (string?)Value["Id"] == "Workspace.EntityDestroyed");
var DestroyedSignature = (JObject)((JArray)DestroyedSignal["Signatures"]!).Single();
Check((string?)DestroyedSignal["Kind"] == "Signal" && ((JArray)DestroyedSignature["Parameters"]!).Count == 1 &&
    (string?)DestroyedSignature["Parameters"]![0]!["Type"] == "EntityDestroyedContext" &&
    ((JArray)DestroyedSignature["Returns"]!).Count == 0 &&
    Definitions.Contains("read EntityDestroyed: SignalWith<(Context: EntityDestroyedContext) -> ()>"),
    "B3 Signal lost snapshot-only callback signature");
var DestroyedFields = ((JArray)Catalog["Members"]!).Cast<JObject>()
    .Where(Value => (string?)Value["OwnerId"] == "EntityDestroyedContext").OrderBy(Value => (string?)Value["Name"]).ToArray();
Check(string.Join(",", DestroyedFields.Select(Value => (string?)Value["Name"])) == "Id,Position,Prefab" &&
    string.Join(",", DestroyedFields.Select(Value => (string?)Value["ValueType"])) == "string,Vector3?,string" &&
    DestroyedFields.All(Value => (string?)Value["Kind"] == "Property" && (bool?)Value["Writable"] == false) &&
    Definitions.Contains("export type EntityDestroyedContext = {"), "B3 immutable context surface changed");
Check(Definitions.Contains("export type PersistedValue = boolean | number | string | {PersistedValue} | {[string]: PersistedValue}"), "recursive persisted value alias missing");
Check(Definitions.Contains("export type DataStoreOptions = {Indexes: {string}?}") &&
    Definitions.Contains("function GetDataStore(self, StoreName: string, Options: DataStoreOptions?): (DataStore)"),
    "bounded optional index hints missing");
Check(Definitions.Contains("export type DataStoreQuery = {Field: string, Type: (\"number\" | \"string\" | \"boolean\")?") &&
    Definitions.Contains("export type DataStoreQueryResult = {Items: {{Key: string, Value: PersistedValue}}, NextCursor: string?}") &&
    Definitions.Contains("function Query(self, Request: DataStoreQuery, Callback: (DataStoreQueryResult?, string?) -> ()): ()"),
    "structured Query definition missing");
Check(Definitions.Contains("Callback: (PersistedValue?, string?) -> ()") && Definitions.Contains("Callback: (boolean?, string?) -> ()"), "persistence callback shape drift");
Check(Definitions.Contains("export type EntityDiscoveryOptions = {Prefab: string?, Limit: number?}") &&
    Definitions.Contains("function GetEntitiesInRadiusAsync(self, Position: Vector3, Radius: number, Callback: ({Entity}?, string?) -> (), Options: EntityDiscoveryOptions?): ()"),
    "Discovery-2B signature/options/callback drift");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("function Workspace.GetEntitiesInRadiusAsync(Self, Position, Radius, Callback, Options, ...)",
    "function Workspace.GetEntitiesInRadiusAsync(Self, Position, Radius, Callback, Options)"), Release, Native), "missing surplus-argument guard accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("function Workspace.GetEntitiesInRadiusAsync(Self, Position, Radius, Callback, Options, ...)",
    "function Workspace.GetEntitiesInRadiusAsync(Self, Position, Radius, Options, Callback, ...)"), Release, Native), "discovery argument order drift accepted");
Reject(() => ApiCatalog.Build(Bootstrap, Release, Native.Replace("int GetDataStore(lua_State* State)\n", "int RenamedGetDataStore(lua_State* State)\n")), "removed persistence binding accepted");
Reject(() => ApiCatalog.Build(Bootstrap, Release, Native.Replace("lua_setfield(State, -2, \"GetAsync\");", "lua_setfield(State, -2, \"ChangedGetAsync\");")), "renamed persistence registration accepted");
Reject(() => ApiCatalog.Build(Bootstrap, Release, Native + "\nlua_pushcfunction(State, Controlled<FutureStorage>, \"FutureStorage\"); lua_setfield(State, -2, \"FutureStorage\");\n"), "unannotated persistence registration accepted");
Broken = (JObject)Catalog.DeepClone();
((JArray)Broken["Types"]!).Single(Value => (string)Value["Id"]! == "PersistedValue")["TypeExpression"] = "UnimplementedType";
Reject(() => ApiCatalog.Validate(Broken), "unknown alias type accepted");
Broken = (JObject)Catalog.DeepClone();
((JArray)Broken["Types"]!).Single(Value => (string)Value["Id"]! == "PersistedValue")["Availability"]!["SinceApi"] = "99.0.0-experimental";
Reject(() => ApiCatalog.Validate(Broken), "future introduction accepted");
foreach (string Path in new[] { "../module", "/module", "Module", "a\\b", "a//b", "a.luau" })
    Reject(() => ModulePath.Validate(Path, false), "invalid module accepted: " + Path);
foreach (string Id in new[] { "carbonluau", "carbonluau.a", "A", "a..b", "_a", "a-" })
    Reject(() => AddonPolicy.ValidateId(Id), "invalid package ID accepted: " + Id);
Console.WriteLine("[CarbonLuau:ToolingTests] PASS metadata relationships, deterministic goldens, negative binding/signature/service/font/native drift, identities and canonical path/ID policy");
var Preview = new Carbon.Plugins.CarbonLuau.PreviewGuiSession(1, Change => { });
string Screen = Preview.Call(21, new[] { "create", "", "ScreenGui" })[0];
string Frame = Preview.Call(21, new[] { "create", Screen, "Frame" })[0];
Preview.Call(21, new[] { "set", Frame, "Position", "udim2", "0.5", "0", "0.5", "0" });
Preview.Call(21, new[] { "set", Frame, "AnchorPoint", "vector2", "0.5", "0.5" });
JObject Plan = Preview.Plan(Screen, 1920, 1080);
Check((double)Plan["Nodes"]![1]!["Projected"]!["RectPx"]!["X"]! == 910, "preview resolves canonical horizontal anchor");
Check((double)Plan["Nodes"]![1]!["Projected"]!["RectPx"]!["Y"]! == 490, "preview resolves canonical vertical anchor");
Check(JToken.DeepEquals(Plan, Preview.Plan(Screen, 1920, 1080)), "preview repeat determinism");
Plan["AvailableScreens"] = Preview.Screens;
Carbon.Plugins.CarbonLuau.PreviewGuiSession.ValidatePlan(Plan);
JObject InvalidPlan = (JObject)Plan.DeepClone();
InvalidPlan["Nodes"]![1]!["Projected"]!["RectPx"]!["X"] = 123;
Reject(() => Carbon.Plugins.CarbonLuau.PreviewGuiSession.ValidatePlan(InvalidPlan), "noncanonical geometry accepted");
InvalidPlan = (JObject)Plan.DeepClone(); InvalidPlan["Accounting"]!["ProjectedElements"] = 0;
Reject(() => Carbon.Plugins.CarbonLuau.PreviewGuiSession.ValidatePlan(InvalidPlan), "false accounting accepted");
Reject(() => Preview.Plan(Screen, double.NaN, 1080), "nonfinite preview viewport accepted");
Reject(() => Preview.Call(21, new[] { "set", Frame, "Parent", "object", Frame }), "preview hierarchy cycle accepted");
Console.WriteLine("[CarbonLuau:ToolingTests] PASS shared preview projection, anchor, determinism and hierarchy checks");
PreviewGoldens.Run(Root, Args.Contains("--update-preview-goldens"));
SnapshotCleanupTests.Run();
