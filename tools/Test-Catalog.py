"""CATALOG-D9 bounded inputs, stable symbols, exact admission and byte drift gates."""
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
Root = Path(__file__).resolve().parent.parent
Spec = importlib.util.spec_from_file_location("CatalogGenerator", Root / "tools/Generate-Catalog.py")
Generator = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Generator)


def Check(Value, Message):
    if not Value:
        raise AssertionError(Message)


def Reject(Action):
    try:
        Action()
    except (ValueError, TypeError, KeyError):
        return
    raise AssertionError("invalid catalog input accepted")


for Name, Expected in {"scrap": "Scrap", "metal.fragments": "MetalFragments", "ammo.pistol.hv": "AmmoPistolHv",
                       "2module.car": "N2ModuleCar", "diesel_barrel": "DieselBarrel", "3module.car": "N3ModuleCar"}.items():
    Check(Generator.Symbol(Name) == Expected, "canonical naming rule drift")
for Name in ("", "wood.", "wood..x", "wood__x", "wood-_x", "Wood", " wood", "wood\0", "Get", "get", "item", "prefab", "loot", "é", "a" * 16):
    Reject(lambda: Generator.Symbol(Name))
Check(Generator.Symbol("a.b") == Generator.Symbol("a_b"), "collision fixture invalid")

with tempfile.TemporaryDirectory(prefix="CarbonLuau-Catalog-") as Temporary:
    Fixture = Path(Temporary)
    for Relative in ("api/loot-items.pinned.json", "api/loot-prefabs.pinned.json", "generated/catalog-symbols.json",
                     "native/src/scripts/ModuleLoader.cpp", "scripts/bootstrap.luau", "src/CarbonLuau/Facade/LootRulePolicy.cs"):
        Target = Fixture / Relative
        Target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(Root / Relative, Target)
    PrefabPath = Fixture / "api/loot-prefabs.pinned.json"
    Prefabs = Generator.ReadPrefabs(PrefabPath)
    Items = Generator.Loot.ReadManifest(Fixture / "api/loot-items.pinned.json")
    First = Generator.Projections(Fixture)
    Check(len(Items["Definitions"]) == 221 and len(Prefabs["Prefabs"]) == 2, "qualified counts")
    for Target, Text in First.items():
        Target.parent.mkdir(parents=True, exist_ok=True)
        Target.write_bytes(Text.encode("utf-8"))
    Check(First == Generator.Projections(Fixture), "projection not idempotent")
    for Mutate in (lambda V: V.update(SchemaVersion=True), lambda V: V.update(SourceQualification="other"),
                   lambda V: V.update(Prefabs=[]), lambda V: V["Prefabs"].reverse(),
                   lambda V: V["Prefabs"][0].update(Id=True), lambda V: V["Prefabs"][0].update(Id=0),
                   lambda V: V["Prefabs"][0].update(Id=4294967296),
                   lambda V: V["Prefabs"][0].update(Group="World"), lambda V: V["Prefabs"][0].update(Symbol="Get"),
                   lambda V: V["Prefabs"][0].update(Path="assets/unsupported.prefab"),
                   lambda V: V["Prefabs"][0].update(Capabilities=["Spawn"]),
                   lambda V: V["Prefabs"][1].update(Id=V["Prefabs"][0]["Id"]),
                   lambda V: V["Prefabs"][1].update(Symbol=V["Prefabs"][0]["Symbol"]),
                   lambda V: V["Prefabs"][1].update(Path=V["Prefabs"][0]["Path"]),
                   lambda V: V["Prefabs"][0].update(Extra=1)):
        Value = copy.deepcopy(Prefabs)
        Mutate(Value)
        PrefabPath.write_text(json.dumps(Value), encoding="utf-8")
        Reject(lambda: Generator.ReadPrefabs(PrefabPath))
    for Raw in ('{"SchemaVersion":1,"SchemaVersion":1}', " " * 4097, '\ufeff{}'):
        PrefabPath.write_text(Raw, encoding="utf-8")
        Reject(lambda: Generator.ReadPrefabs(PrefabPath))
    PrefabPath.write_text(Generator.Json(Prefabs), encoding="utf-8")
    Changed = copy.deepcopy(Prefabs)
    Changed["Prefabs"][0]["Id"] += 1
    Reject(lambda: Generator.AuditAdmission(Fixture, Items, Changed))
    for Key, Replacement in (("Id", 1), ("Stack", 123), ("Family", "Component")):
        ChangedItems = copy.deepcopy(Items)
        ChangedItems["Definitions"][0][Key] = Replacement
        Reject(lambda: Generator.AuditAdmission(Fixture, ChangedItems, Prefabs))
    ChangedItems = copy.deepcopy(Items)
    ChangedItems["HostPins"]["Windows"]["AssemblySha256"] = "0" * 64
    Reject(lambda: Generator.AuditAdmission(Fixture, ChangedItems, Prefabs))
    Snapshot = Fixture / "generated/catalog-symbols.json"
    OriginalSnapshot = Snapshot.read_bytes()
    for Mutate in (lambda V: V.update(Extra=1), lambda V: V.update(SymbolAlgorithmVersion=True),
                   lambda V: V.update(SymbolAlgorithmVersion=2), lambda V: V.update(ItemsSha256="bad"),
                   lambda V: V.update(Items=[]), lambda V: V["Items"].reverse(),
                   lambda V: V["Items"][0].update(Extra=1),
                   lambda V: V["Items"][0].update(Symbol=V["Items"][1]["Symbol"]),
                   lambda V: V["Items"][0].update(Name=V["Items"][1]["Name"]),
                   lambda V: V["Prefabs"][0].update(Symbol="Changed")):
        Value = json.loads(OriginalSnapshot)
        Mutate(Value)
        Snapshot.write_text(Generator.Json(Value), encoding="utf-8")
        Reject(lambda: Generator.Projections(Fixture))
    for Raw in (b" " * 131073, b'{"SymbolAlgorithmVersion":1,"SymbolAlgorithmVersion":1}'):
        Snapshot.write_bytes(Raw)
        Reject(lambda: Generator.Projections(Fixture))
    Snapshot.write_bytes(OriginalSnapshot)
    Value = json.loads(OriginalSnapshot)
    Value["Items"][0]["Symbol"] = "Retargeted"
    Snapshot.write_text(Generator.Json(Value), encoding="utf-8")
    try:
        Generator.Projections(Fixture)
    except ValueError as Error:
        Check('"OldSymbol":"Retargeted"' in str(Error) and '"NewSymbol":"N2ModuleCar"' in str(Error), "explicit deterministic compatibility diff missing")
    else:
        raise AssertionError("snapshot retargeting accepted")
    Snapshot.write_bytes(OriginalSnapshot)
    Command = [sys.executable, str(Root / "tools/Generate-Catalog.py"), "--root", str(Fixture)]
    def Run(Mode):
        return subprocess.run(Command + [Mode], capture_output=True, timeout=10)
    Check(Run("--check").returncode == 0, "CLI exact source gate")
    Before = {Target: Target.read_bytes() for Target in First}
    Check(Run("--patch").stdout == b"*** Begin Patch\n*** End Patch\n", "idempotent patch")
    for Target in First:
        Original = Target.read_bytes()
        if Target.name == "ModuleLoader.cpp":
            Target.write_bytes(Original.replace(b"// Catalog SHA256: ", b"// Catalog SHA256: 0", 1))
            Check(Run("--check").returncode != 0, "native fingerprint drift accepted")
            Target.write_bytes(Original)
            continue
        for Corruption in (Original + b"\n", b"\xef\xbb\xbf" + Original, Original.replace(b"\n", b"\r\n")):
            Target.write_bytes(Corruption)
            Check(Run("--check").returncode != 0 and Target.read_bytes() == Corruption, "side-effect-free exact byte rejection")
        Target.write_bytes(Original)
    Check(Run("--write").returncode == 0 and Run("--check").returncode == 0, "isolated write/check")
    Check(Before == {Target: Target.read_bytes() for Target in First}, "CLI regeneration nondeterministic")
    Collision = copy.deepcopy(Items)
    Collision["Definitions"][0]["Name"] = "a.b"
    Collision["Definitions"][1]["Name"] = "a_b"
    Collision["Definitions"].sort(key=lambda Row: Row["Name"])
    (Fixture / "api/loot-items.pinned.json").write_text(Generator.Json(Collision), encoding="utf-8")
    for Target, Text in Generator.Loot.Projections(Fixture, Collision).items():
        Target.write_text(Text, encoding="utf-8")
    Reject(lambda: Generator.Projections(Fixture))

print("[CarbonLuau:CatalogTest] PASS 221/2 exact rows; strict bounded manifests, stable v1 symbols, hostile inputs, two-way D6/D8 admission and deterministic byte/source drift gates")
