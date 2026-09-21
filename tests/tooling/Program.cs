using CarbonLuau.Core;
using Newtonsoft.Json.Linq;
using System.Globalization;

string[] Args = Environment.GetCommandLineArgs().Skip(1).ToArray();
string Root = Path.GetFullPath(Args.Length == 0 ? "../.." : Args[0]);
string Bootstrap = File.ReadAllText(Path.Combine(Root, "scripts/bootstrap.luau")).Replace("\r\n", "\n");
string Native = File.ReadAllText(Path.Combine(Root, "native/src/scripts/ModuleLoader.cpp")).Replace("\r\n", "\n");
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
foreach (string Path in new[] { "../module", "/module", "Module", "a\\b", "a//b", "a.luau" })
    Reject(() => ModulePath.Validate(Path, false), "invalid module accepted: " + Path);
foreach (string Id in new[] { "carbonluau", "carbonluau.a", "A", "a..b", "_a", "a-" })
    Reject(() => AddonPolicy.ValidateId(Id), "invalid package ID accepted: " + Id);
Console.WriteLine("[CarbonLuau:ToolingTests] PASS metadata relationships, deterministic goldens, negative binding/signature/service/font/native drift, identities and canonical path/ID policy");
