"""Generate the two existing finite Loot name checks; never discover host items here."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import re
import sys

MaximumManifestBytes = 65536
MaximumDefinitions = 512
MaximumNameCharacters = 15  # Preserve D6's retained descriptor payload proof.
Families = {"Baseline", "RawResource", "Component", "BasicMaterial", "Consumable", "Equivalent"}


def Object(Pairs):
    Result = {}
    for Key, Value in Pairs:
        if Key in Result:
            raise ValueError("duplicate manifest field: " + Key)
        Result[Key] = Value
    return Result


def Fields(Value, Expected, Label):
    if type(Value) is not dict or set(Value) != set(Expected):
        raise ValueError("invalid " + Label + " fields")


def Integer(Value, Minimum, Maximum):
    return type(Value) is int and Minimum <= Value <= Maximum


def ReadManifest(Path):
    Raw = Path.read_bytes()
    if len(Raw) > MaximumManifestBytes:
        raise ValueError("Loot manifest byte bound exceeded")
    Value = json.loads(Raw.decode("utf-8"), object_pairs_hook=Object)
    Fields(Value, ("SchemaVersion", "HostPins", "Definitions"), "manifest")
    if type(Value["SchemaVersion"]) is not int or Value["SchemaVersion"] != 1:
        raise ValueError("unsupported Loot manifest schema")
    Fields(Value["HostPins"], ("Windows", "Linux"), "host pin set")
    for Pin in Value["HostPins"].values():
        Fields(Pin, ("RustBuild", "Carbon", "AssemblySha256", "Mvid"), "host pin")
        if not Integer(Pin["RustBuild"], 1, 2147483647):
            raise ValueError("invalid pinned Rust build")
        for Key, Pattern in (("Carbon", r"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+/[0-9a-f]{7}"),
                             ("AssemblySha256", r"[0-9a-f]{64}"),
                             ("Mvid", r"[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}")):
            if type(Pin[Key]) is not str or re.fullmatch(Pattern, Pin[Key]) is None:
                raise ValueError("invalid host pin " + Key)
    if Value["HostPins"]["Windows"]["RustBuild"] != Value["HostPins"]["Linux"]["RustBuild"]:
        raise ValueError("host Rust build mismatch")
    Rows = Value["Definitions"]
    if type(Rows) is not list or not 1 <= len(Rows) <= MaximumDefinitions:
        raise ValueError("Loot definition count bound exceeded")
    Names, Ids = [], set()
    for Row in Rows:
        Fields(Row, ("Name", "Id", "Stack", "Family"), "definition")
        Name = Row["Name"]
        if type(Name) is not str or re.fullmatch(r"[a-z0-9][a-z0-9._-]{0,14}", Name) is None:
            raise ValueError("item name violates exact ASCII/D6 payload bound")
        if not Integer(Row["Id"], -2147483648, 2147483647) or Row["Id"] == 0 or Row["Id"] in Ids:
            raise ValueError("invalid or duplicate item ID")
        if not Integer(Row["Stack"], 1, 2147483647) or type(Row["Family"]) is not str or Row["Family"] not in Families:
            raise ValueError("invalid item stack/family")
        Names.append(Name)
        Ids.add(Row["Id"])
    if Names != sorted(set(Names)):
        raise ValueError("item names must be unique and ordinal sorted")
    return Value


def ReplaceBlock(Source, Begin, End, Legacy, Block):
    if Begin in Source or End in Source:
        if Source.count(Begin) != 1 or Source.count(End) != 1:
            raise ValueError("ambiguous generated Loot region")
        Start = Source.index(Begin)
        Stop = Source.index(End, Start) + len(End)
        return Source[:Start] + Block + Source[Stop:]
    if Source.count(Legacy) != 1:
        raise ValueError("existing Loot check drifted; refusing broad rewrite")
    return Source.replace(Legacy, Block, 1)


def Projections(Root, Manifest):
    Canonical = json.dumps(Manifest, sort_keys=True, separators=(",", ":")).encode("ascii")
    Digest = hashlib.sha256(Canonical).hexdigest()
    Names = [Row["Name"] for Row in Manifest["Definitions"]]
    CsBegin = "// BEGIN GENERATED LOOT ITEMS"
    CsEnd = "// END GENERATED LOOT ITEMS"
    CsIndent = "                    "
    CsBlock = CsBegin + "\n" + CsIndent + "// Manifest SHA256: " + Digest + "\n"
    CsBlock += CsIndent + "if (" + (" &&\n" + CsIndent + "    ").join(
        'Value.Item != "' + Name + '"' for Name in Names) + ")\n"
    CsBlock += CsIndent + '    throw new FacadeException("unsupported loot rule item");\n' + CsIndent + CsEnd
    CsLegacy = 'if (Value.Item != "scrap" && Value.Item != "metal.fragments" && Value.Item != "wood" && Value.Item != "stones")\n' + CsIndent + '    throw new FacadeException("unsupported loot rule item");'
    LuaBegin = "-- BEGIN GENERATED LOOT ITEMS"
    LuaEnd = "-- END GENERATED LOOT ITEMS"
    LuaIndent = "        "
    LuaBlock = LuaBegin + "\n" + LuaIndent + "-- Manifest SHA256: " + Digest + "\n"
    LuaBlock += LuaIndent + "if " + (" and\n" + LuaIndent + "    ").join(
        'Item ~= "' + Name + '"' for Name in Names) + ' then Fail("unsupported loot rule item") end\n' + LuaIndent + LuaEnd
    LuaLegacy = 'if Item ~= "scrap" and Item ~= "metal.fragments" and Item ~= "wood" and Item ~= "stones" then Fail("unsupported loot rule item") end'
    Result = {}
    for Relative, Begin, End, Legacy, Block in (
        ("src/CarbonLuau/Facade/LootRulePolicy.cs", CsBegin, CsEnd, CsLegacy, CsBlock),
        ("scripts/bootstrap.luau", LuaBegin, LuaEnd, LuaLegacy, LuaBlock)):
        Path = Root / Relative
        Source = Path.read_text(encoding="utf-8")
        if len(Block.encode("ascii")) > 32768:
            raise ValueError("generated Loot check byte bound exceeded")
        Result[Path] = ReplaceBlock(Source, Begin, End, Legacy, Block)
    return Result


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    Mode = Parser.add_mutually_exclusive_group(required=True)
    Mode.add_argument("--check", action="store_true")
    Mode.add_argument("--write", action="store_true")
    Mode.add_argument("--patch", action="store_true", help="print an apply_patch payload without changing shared files")
    Args = Parser.parse_args()
    Manifest = ReadManifest(Args.root / "api/loot-items.pinned.json")
    Projection = Projections(Args.root, Manifest)
    if Args.patch:
        Patch = ["*** Begin Patch\n"]
        for OutputPath, Expected in Projection.items():
            Actual = OutputPath.read_text(encoding="utf-8")
            if Actual == Expected:
                continue
            Patch.append("*** Update File: " + OutputPath.resolve().as_posix() + "\n")
            Diff = list(difflib.unified_diff(Actual.splitlines(keepends=True), Expected.splitlines(keepends=True)))
            Patch.extend("@@\n" if Line.startswith("@@") else Line for Line in Diff[2:])
        Patch.append("*** End Patch\n")
        sys.stdout.write("".join(Patch))
        return
    for OutputPath, Expected in Projection.items():
        Actual = OutputPath.read_text(encoding="utf-8")
        if Actual != Expected:
            if Args.check:
                raise ValueError("generated Loot check drift: " + str(OutputPath))
            OutputPath.write_text(Expected, encoding="utf-8", newline="\n")
    print("[CarbonLuau:LootItems] PASS deterministic finite manifest projections; definitions=" + str(len(Manifest["Definitions"])))


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, OSError, UnicodeError) as Error:
        print("[CarbonLuau:LootItems] FAIL " + str(Error), file=sys.stderr)
        sys.exit(1)
