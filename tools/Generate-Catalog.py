"""Project finite qualified data into shared VM catalogs; no host discovery."""
import argparse
import difflib
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding="utf-8", newline="\n")
Spec = importlib.util.spec_from_file_location("LootItems", Path(__file__).with_name("Generate-LootItems.py"))
Loot = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Loot)
Reserved = set("and break do else elseif end false for function if in local nil not or repeat return then true until while continue export type typeof read Get Item Prefab Loot".split())
Algorithm = 1


def Json(Value):
    return json.dumps(Value, indent=2, ensure_ascii=True) + "\n"


def Digest(Value):
    return hashlib.sha256(json.dumps(Value, sort_keys=True, separators=(",", ":")).encode("ascii")).hexdigest()


def Symbol(Name):
    if type(Name) is not str or re.fullmatch(r"[a-z0-9][a-z0-9._-]{0,14}", Name) is None:
        raise ValueError("catalog symbol requires a canonical D8 name")
    Parts = re.split(r"[._-]", Name)
    if any(not Part for Part in Parts):
        raise ValueError("empty catalog symbol segment")
    Result = "".join(re.sub(r"[a-z]", lambda Match: Match[0].upper(), Part, count=1) for Part in Parts)
    if Result[0].isdigit():
        Result = "N" + Result
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", Result) or Result in Reserved:
        raise ValueError("reserved or invalid catalog symbol: " + Result)
    return Result


def ReadPrefabs(Path):
    Raw = Path.read_bytes()
    if len(Raw) > 4096:
        raise ValueError("prefab manifest exceeds 4096 bytes")
    Value = json.loads(Raw.decode("utf-8"), object_pairs_hook=Loot.Object)
    Loot.Fields(Value, ("SchemaVersion", "SourceQualification", "Prefabs"), "prefab manifest")
    if type(Value["SchemaVersion"]) is not int or Value["SchemaVersion"] != 1 or Value["SourceQualification"] != "GameplayD6/ee944d2":
        raise ValueError("unsupported prefab qualification/schema")
    Rows = Value["Prefabs"]
    if type(Rows) is not list or len(Rows) != 2:
        raise ValueError("CATALOG-D9 requires exactly two prefabs")
    for Row in Rows:
        Loot.Fields(Row, ("Group", "Symbol", "Path", "Id", "Capabilities"), "prefab")
        if Row["Group"] != "Loot" or Row["Symbol"] not in ("Barrel1", "Barrel2") or Row["Capabilities"] != ["Loot.Replace"]:
            raise ValueError("unsupported prefab group/symbol/capability")
        if type(Row["Path"]) is not str or re.fullmatch(r"assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-[12]\.prefab", Row["Path"]) is None:
            raise ValueError("unsupported exact prefab path")
        if not Loot.Integer(Row["Id"], 1, 4294967295):
            raise ValueError("invalid prefab ID")
    for Key in ("Symbol", "Path", "Id"):
        if len({Row[Key] for Row in Rows}) != 2:
            raise ValueError("duplicate prefab " + Key)
    if [Row["Symbol"] for Row in Rows] != ["Barrel1", "Barrel2"]:
        raise ValueError("prefabs must be ordinal sorted")
    return Value


def AuditAdmission(Root, Items, Prefabs):
    # Transitional two-way equivalence: D6 stays independently authoritative.
    Source = (Root / "src/CarbonLuau/Facade/LootRulePolicy.cs").read_text(encoding="utf-8")
    Constants = re.findall(r'internal const (string|uint) (Prefab|PrefabId|SecondPrefab|SecondPrefabId) = ("[^"\n]+"|[0-9]+);', Source)
    Expected = []
    for Prefix, Row in zip(("", "Second"), Prefabs["Prefabs"]):
        Expected.extend([("string", Prefix + "Prefab", json.dumps(Row["Path"])), ("uint", Prefix + "PrefabId", str(Row["Id"]))])
    if Constants != Expected:
        raise ValueError("D6 prefab constants differ from catalog declaration")
    Body = re.search(r"internal static bool TryPrefabId\(string Name, out uint Id\)\s*\{([^}]+\}[^}]+\}[^}]+)\}", Source)
    if not Body or re.sub(r"\s+", " ", Body[1]).strip() != 'if (String.Equals(Name, Prefab, StringComparison.Ordinal)) { Id = PrefabId; return true; } if (String.Equals(Name, SecondPrefab, StringComparison.Ordinal)) { Id = SecondPrefabId; return true; } Id = 0; return false;':
        raise ValueError("D6 prefab admission differs from exact two-key predicate")
    Bootstrap = (Root / "scripts/bootstrap.luau").read_text(encoding="utf-8")
    Predicate = '    if Prefab ~= "' + Prefabs["Prefabs"][0]["Path"] + '" and\n        Prefab ~= "' + Prefabs["Prefabs"][1]["Path"] + '" then Fail("unsupported loot rule prefab") end'
    if Bootstrap.count(Predicate) != 1:
        raise ValueError("bootstrap prefab predicate differs from D6 catalog")
    for Path, ExpectedSource in Loot.Projections(Root, Items).items():
        if Path.read_text(encoding="utf-8") != ExpectedSource:
            raise ValueError("D8 admission projection drift")


def Projections(Root):
    Items = Loot.ReadManifest(Root / "api/loot-items.pinned.json")
    Prefabs = ReadPrefabs(Root / "api/loot-prefabs.pinned.json")
    AuditAdmission(Root, Items, Prefabs)
    ItemRows = [{"Name": Row["Name"], "Symbol": Symbol(Row["Name"])} for Row in Items["Definitions"]]
    if len({Row["Symbol"] for Row in ItemRows}) != len(ItemRows):
        raise ValueError("catalog symbol collision; explicit compatibility review required")
    Combined = {"SymbolAlgorithmVersion": Algorithm, "ItemsSha256": Digest(Items), "PrefabsSha256": Digest(Prefabs),
                "Items": ItemRows, "Prefabs": Prefabs["Prefabs"]}
    Snapshot = Root / "generated/catalog-symbols.json"
    if Snapshot.exists():
        Raw = Snapshot.read_bytes()
        if len(Raw) > 131072:
            raise ValueError("catalog symbol snapshot exceeds 128 KiB")
        Old = json.loads(Raw.decode("utf-8"), object_pairs_hook=Loot.Object)
        Loot.Fields(Old, ("SymbolAlgorithmVersion", "ItemsSha256", "PrefabsSha256", "Items", "Prefabs"), "catalog snapshot")
        if type(Old["SymbolAlgorithmVersion"]) is not int or Old["SymbolAlgorithmVersion"] != Algorithm:
            raise ValueError("catalog algorithm compatibility review required")
        for Key in ("ItemsSha256", "PrefabsSha256"):
            if type(Old[Key]) is not str or re.fullmatch(r"[0-9a-f]{64}", Old[Key]) is None:
                raise ValueError("invalid catalog snapshot fingerprint")
        if type(Old["Items"]) is not list or not 1 <= len(Old["Items"]) <= Loot.MaximumDefinitions:
            raise ValueError("catalog snapshot row bound")
        OldNames, OldSymbols = [], set()
        for Row in Old["Items"]:
            Loot.Fields(Row, ("Name", "Symbol"), "catalog symbol")
            Symbol(Row["Name"])
            if type(Row["Symbol"]) is not str or re.fullmatch(r"[A-Z][A-Za-z0-9]{0,31}", Row["Symbol"]) is None or Row["Symbol"] in OldSymbols:
                raise ValueError("invalid or duplicate catalog snapshot symbol")
            OldNames.append(Row["Name"]); OldSymbols.add(Row["Symbol"])
        if OldNames != sorted(set(OldNames)):
            raise ValueError("catalog snapshot names must be unique and ordinal sorted")
        New = {Row["Name"]: Row["Symbol"] for Row in ItemRows}
        Changes = [{"Name": Row["Name"], "OldSymbol": Row["Symbol"], "NewSymbol": New.get(Row["Name"])}
                   for Row in Old["Items"] if New.get(Row["Name"]) != Row["Symbol"]]
        PrefabChanged = Old["Prefabs"] != Combined["Prefabs"]
        if Changes or PrefabChanged:
            Diff = {"Changed": len(Changes), "Rows": Changes[:16], "Remaining": max(0, len(Changes)-16), "PrefabChanged": PrefabChanged}
            raise ValueError("catalog compatibility review required; diff=" + json.dumps(Diff, sort_keys=True, separators=(",", ":")))
    Fingerprint = Digest(Combined)
    Header = "// Generated by tools/Generate-Catalog.py. Do not edit.\n"
    Native = Header + 'constexpr char CatalogFingerprint[] = "' + Fingerprint + '";\n'
    Native += 'constexpr const char* CatalogItems[][2] = {\n' + "".join('    {"' + Row["Symbol"] + '", "' + Row["Name"] + '"},\n' for Row in ItemRows) + '};\n'
    Native += 'constexpr const char* CatalogPrefabs[][2] = {\n' + "".join('    {"' + Row["Symbol"] + '", "' + Row["Path"] + '"},\n' for Row in Prefabs["Prefabs"]) + '};\n'
    Native += 'static_assert(sizeof(CatalogItems) / sizeof(CatalogItems[0]) == ' + str(len(ItemRows)) + ');\nstatic_assert(sizeof(CatalogPrefabs) / sizeof(CatalogPrefabs[0]) == 2);\n'
    Cs = Header + 'namespace CarbonLuau.Core {\n    internal static class CatalogData {\n'
    Cs += '        internal const string Fingerprint = "' + Fingerprint + '";\n'
    Cs += '        internal static readonly string[][] Items = {\n' + "".join('            new[] {"' + Row["Symbol"] + '", "' + Row["Name"] + '"},\n' for Row in ItemRows) + '        };\n'
    Cs += '        internal static readonly string[][] Prefabs = {\n' + "".join('            new[] {"' + Row["Symbol"] + '", "' + Row["Path"] + '"},\n' for Row in Prefabs["Prefabs"]) + '        };\n    }\n}\n'
    if max(len(Native.encode("ascii")), len(Cs.encode("ascii"))) > 32768:
        raise ValueError("generated catalog block exceeds 32 KiB")
    Loader = Root / "native/src/scripts/ModuleLoader.cpp"
    Source = Loader.read_text(encoding="utf-8")
    Marker = "// Catalog SHA256: " + Fingerprint
    if "// Catalog SHA256: " in Source:
        if len(re.findall(r"// Catalog SHA256: [0-9a-f]{64}", Source)) != 1:
            raise ValueError("ambiguous native catalog fingerprint")
        Source = re.sub(r"// Catalog SHA256: [0-9a-f]{64}", Marker, Source)
    else:
        Needle = "void InstallDomainBindings(lua_State* State, Domain& Owner)\n{"
        if Source.count(Needle) != 1:
            raise ValueError("native domain installer drift")
        Source = Source.replace(Needle, Marker + "\n" + Needle)
    Identity = 'const char* CatalogIdentity() { return "' + Fingerprint + '"; }'
    Pattern = r'const char\* CatalogIdentity\(\) \{ return "[0-9a-f]{64}"; \}'
    if "const char* CatalogIdentity()" in Source:
        if len(re.findall(Pattern, Source)) != 1:
            raise ValueError("ambiguous native catalog identity")
        Source = re.sub(Pattern, lambda Match: Identity, Source)
    else:
        Source = Source.replace(Marker + "\n", Marker + "\n" + Identity + "\n", 1)
    return {Root / "native/src/runtime/generated/CatalogData.inc": Native,
            Root / "src/CarbonLuau.Core/Api/CatalogData.g.cs": Cs,
            Snapshot: Json(Combined), Loader: Source}


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    Mode = Parser.add_mutually_exclusive_group(required=True)
    Mode.add_argument("--check", action="store_true")
    Mode.add_argument("--write", action="store_true")
    Mode.add_argument("--patch", action="store_true")
    Args = Parser.parse_args()
    Outputs = Projections(Args.root)
    Patch = ["*** Begin Patch\n"]
    for Target, Expected in Outputs.items():
        Actual = Target.read_text(encoding="utf-8") if Target.exists() else None
        Exact = Target.exists() and Target.read_bytes() == Expected.encode("utf-8")
        if Exact:
            continue
        if Args.check:
            raise ValueError("catalog source/output drift: " + str(Target))
        if Args.patch:
            if Actual is None:
                Patch.extend(["*** Add File: " + Target.resolve().as_posix() + "\n", "".join("+" + Line + "\n" for Line in Expected.splitlines())])
            else:
                Patch.append("*** Update File: " + Target.resolve().as_posix() + "\n")
                Patch.extend("@@\n" if Line.startswith("@@") else Line for Line in list(difflib.unified_diff(Actual.splitlines(keepends=True), Expected.splitlines(keepends=True)))[2:])
        else:
            Target.parent.mkdir(parents=True, exist_ok=True)
            Target.write_text(Expected, encoding="utf-8", newline="\n")
    if Args.patch:
        sys.stdout.write("".join(Patch) + "*** End Patch\n")
    else:
        print("[CarbonLuau:Catalog] PASS finite deterministic catalog projections")


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, OSError, UnicodeError, KeyError, TypeError) as Error:
        print("[CarbonLuau:Catalog] FAIL " + str(Error), file=sys.stderr)
        sys.exit(1)
