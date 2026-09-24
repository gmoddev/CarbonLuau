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
Check(Definitions == File.ReadAllText(Path.Combine(Root, "generated/carbonluau.d.luau")).Replace("\r\n", "\n"), "definition golden drift");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
Check(JToken.DeepEquals(Catalog, ApiCatalog.Build(Bootstrap, Release, Native)), "catalog repeat/culture determinism");
Check(Definitions == ApiArtifacts.Definitions(ApiCatalog.Build(Bootstrap, Release, Native)), "definition repeat determinism");
Check(Definitions.Contains("GiveItem") && Definitions.Contains("GiveItemBehavior") && Definitions.Contains("InventoryOnly") && !Definitions.Contains("TextBox") && Definitions.Contains("TakeItem"), "current implemented inventory API exposure");
Check((string)Catalog["Api"]!["Version"]! == (string)Release["apiVersion"]!, "API identity");
Reject(() => ApiCatalog.Build(Bootstrap + "\nfunction PlayerMethods.Future(Player) end\n", Release, Native), "unannotated binding accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("\nfunction PlayerMethods.Teleport(Player, Position)\n", "\nfunction PlayerMethods.Teleport(Player, Target)\n"), Release, Native), "changed signature accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("    PermanentMarker = Font(\"PermanentMarker\"),", ""), Release, Native), "removed singleton accepted");
Reject(() => ApiCatalog.Build(Bootstrap.Replace("    if Name == \"Items\" then return Items end\n", ""), Release, Native), "removed service accepted");
Reject(() => ApiCatalog.Build(Bootstrap, Release, Native.Replace("        lua_setfield(State, -2, \"IsDependencyAvailable\");", "")), "removed native binding accepted");
var Broken = (JObject)Catalog.DeepClone();
((JObject)Broken["Types"]![0]!)["BaseTypeId"] = "Missing";
Reject(() => ApiCatalog.Validate(Broken), "unknown inheritance accepted");
Broken = (JObject)Catalog.DeepClone();
((JObject)Broken["Members"]![0]!)["ValueType"] = "UnimplementedType";
Reject(() => ApiCatalog.Validate(Broken), "unknown type accepted");
var PersistenceTypes = new[] { "DataStoreService", "DataStore", "PersistedValue" };
foreach (JObject Declaration in ((JArray)Catalog["Types"]!).Concat((JArray)Catalog["Members"]!)) {
    bool Persistence = PersistenceTypes.Contains((string?)Declaration["Id"]) || PersistenceTypes.Contains((string?)Declaration["OwnerId"]);
    Version Since = Version.Parse(((string)Declaration["Availability"]!["SinceApi"]!).Split('-')[0]);
    Check(Persistence ? Since == new Version(0, 5, 0) : Since <= new Version(0, 4, 0), "historical introduction version changed");
    if (Persistence) Check((string)Declaration["Availability"]!["Qualification"]! == "Experimental" &&
        (string)Declaration["Preview"]! == "Unavailable", "persistence qualification/preview drift");
}
Check(Definitions.Contains("export type PersistedValue = boolean | number | string | {PersistedValue} | {[string]: PersistedValue}"), "recursive persisted value alias missing");
Check(Definitions.Contains("Callback: (PersistedValue?, string?) -> ()") && Definitions.Contains("Callback: (boolean?, string?) -> ()"), "persistence callback shape drift");
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
