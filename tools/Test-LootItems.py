"""Finite Loot manifest schema, deterministic generation and fail-closed drift fixtures."""
import copy
import importlib.util
import json
from pathlib import Path
import sys
import subprocess
import tempfile

sys.dont_write_bytecode = True
Root = Path(__file__).resolve().parent.parent
Spec = importlib.util.spec_from_file_location("LootGenerator", Root / "tools/Generate-LootItems.py")
Generator = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Generator)
Manifest = Generator.ReadManifest(Root / "api/loot-items.pinned.json")


def Check(Value, Message):
    if not Value:
        raise AssertionError(Message)


with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootManifest-") as Directory:
    Fixture = Path(Directory)
    Input = Fixture / "input.json"

    def Reject(Value):
        Input.write_text(json.dumps(Value), encoding="utf-8")
        try:
            Generator.ReadManifest(Input)
        except ValueError:
            return
        raise AssertionError("invalid manifest accepted")

    for Name in ("Wood", " wood", "wood\0", "wood.extra-long-name", "wo\"od", "wóod", "", "x\tand true"):
        Value = copy.deepcopy(Manifest)
        Value["Definitions"][0]["Name"] = Name
        Reject(Value)
    for Key, Invalid in (("Id", 0), ("Id", True), ("Id", 2147483648), ("Stack", 0),
                         ("Stack", True), ("Stack", 1.5), ("Family", "Unrestricted"), ("Family", [])):
        Value = copy.deepcopy(Manifest)
        Value["Definitions"][0][Key] = Invalid
        Reject(Value)
    Value = copy.deepcopy(Manifest)
    Value["Definitions"].reverse()
    Reject(Value)
    Value = copy.deepcopy(Manifest)
    Value["Definitions"][1]["Id"] = Value["Definitions"][0]["Id"]
    Reject(Value)
    for Key, Invalid in (("SchemaVersion", True), ("SchemaVersion", 2), ("Definitions", []),
                         ("Definitions", Manifest["Definitions"] * (Generator.MaximumDefinitions + 1))):
        Value = copy.deepcopy(Manifest)
        Value[Key] = Invalid
        Reject(Value)
    Value = copy.deepcopy(Manifest)
    Value["Extra"] = True
    Reject(Value)
    Value = copy.deepcopy(Manifest)
    Value["HostPins"]["Linux"]["AssemblySha256"] = "unknown"
    Reject(Value)
    Value = copy.deepcopy(Manifest)
    Value["HostPins"]["Linux"]["RustBuild"] += 1
    Reject(Value)
    for Text in ('{"SchemaVersion":1,"SchemaVersion":1}', " " * (Generator.MaximumManifestBytes + 1)):
        Input.write_text(Text, encoding="utf-8")
        try:
            Generator.ReadManifest(Input)
        except ValueError:
            pass
        else:
            raise AssertionError("duplicate fields or over-limit input accepted")

    Sources = {
        "src/CarbonLuau/Facade/LootRulePolicy.cs":
            'if (Value.Item != "scrap" && Value.Item != "metal.fragments" && Value.Item != "wood" && Value.Item != "stones")\n'
            '                        throw new FacadeException("unsupported loot rule item");',
        "scripts/bootstrap.luau":
            'if Item ~= "scrap" and Item ~= "metal.fragments" and Item ~= "wood" and Item ~= "stones" then Fail("unsupported loot rule item") end'
    }
    for Relative, Text in Sources.items():
        Path = Fixture / Relative
        Path.parent.mkdir(parents=True, exist_ok=True)
        Path.write_text(Text, encoding="utf-8")
    First = Generator.Projections(Fixture, Manifest)
    for Path, Text in First.items():
        Path.write_text(Text, encoding="utf-8")
    Check(First == Generator.Projections(Fixture, Manifest), "regeneration not idempotent")
    ReorderedFields = json.loads(json.dumps(Manifest, sort_keys=True))
    Check(First == Generator.Projections(Fixture, ReorderedFields), "object field order changes projections")
    ManifestPath = Fixture / "api/loot-items.pinned.json"
    ManifestPath.parent.mkdir()
    ManifestPath.write_text(json.dumps(Manifest), encoding="utf-8")
    Command = [sys.executable, str(Root / "tools/Generate-LootItems.py"), "--root", str(Fixture)]
    def Run(Mode):
        return subprocess.run(Command + [Mode], capture_output=True, text=True, timeout=10)
    Check(Run("--check").returncode == 0, "actual generator CLI rejects exact projections")
    Check(Run("--patch").stdout == "*** Begin Patch\n*** End Patch\n", "idempotent CLI patch output differs")
    for Mutate in (lambda Value: Value["Definitions"][0].update(Stack=999),
                   lambda Value: Value["HostPins"]["Windows"].update(Carbon="2.0.262.0/0000000")):
        Value = copy.deepcopy(Manifest)
        Mutate(Value)
        Check(First != Generator.Projections(Fixture, Value), "semantic input drift not detected")
    ManifestPath.write_text(json.dumps(Value), encoding="utf-8")
    Check(Run("--check").returncode != 0, "actual CLI ignores semantic manifest drift")
    Patch = Run("--patch")
    Check(Patch.returncode == 0 and Patch.stdout.count("*** Update File:") == 2, "CLI patch does not cover both checks")
    Check(Run("--write").returncode == 0 and Run("--check").returncode == 0, "isolated CLI regeneration is not deterministic")
    Path = Fixture / "scripts/bootstrap.luau"
    Path.write_text(Path.read_text(encoding="utf-8") + "\n-- BEGIN GENERATED LOOT ITEMS", encoding="utf-8")
    try:
        Generator.Projections(Fixture, Manifest)
    except ValueError:
        pass
    else:
        raise AssertionError("ambiguous source region accepted")

print("[CarbonLuau:LootItemsTest] PASS strict bounded schema, ordinal names/IDs, deterministic generation and input/source drift rejection")
