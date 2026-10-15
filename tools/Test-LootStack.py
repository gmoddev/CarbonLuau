"""Exercise actual D8 stack parsing without promoting an intentional negative to compatibility PASS."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
Spec = importlib.util.spec_from_file_location("LootStackReader", Path(__file__).with_name("Read-LootStack.py"))
Reader = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Reader)


def Text(Host, Fixed):
    Version, Revision, Mvid, Hash = Reader.Pins[Host]
    Result = "Carbon " + Version + " [2026.10.01.0] " + Revision + " on " + Host + " \n" + \
        "Rust   115/2634.289.1 on release (01/10/2026 13:49:11) 166494\n" + \
        "LIVE_BODY method=LootContainer.Void OnDied(HitInfo) token=0x06005773 mvid=" + Mvid + \
        " raw_il_bytes=83 raw_il_sha256=" + Hash + " eh=0 raw_reflection_body_not_harmony_replacement=true\n" + \
        "D8_STACK_BEGIN mode=gap_probe representatives=6 profiles=2 production_engine_hooks_added=0 world_cap=8 amount_cap=1000\n"
    for Name, Id in Reader.Representatives.items():
        for Prefab in Reader.Prefabs:
            Result += "D8_STACK_MUTATION point=after_prepare item=" + Name + " old=2 new=1 planned=2 committed=false created=0\n" + \
                "D8_STACK_GAP item=" + Name + " id=" + str(Id) + " prefab=" + Prefab + " result=" + ("PASS" if Fixed else "FAIL") + \
                " committed=" + str(not Fixed) + " created=0 accepted=0 selected_delta=1 partial_delta=" + str(0 if Fixed else 1) + \
                " original_stored=2 vanilla_worlds=" + str(2 if Fixed else 0) + " reason=" + \
                ("stack_drop_before_commit_preserves_vanilla" if Fixed else "stack_drop_after_prepare_suppresses_vanilla") + "\n" + \
                "D8_STACK_CLEANUP item=" + Name + " prefab=" + Prefab + " ledger=0\n"
    return Result + "D8_STACK_BATCH mode=gap_probe completed=true cases=12 pass=" + str(12 if Fixed else 0) + \
        " fail=" + str(0 if Fixed else 12) + " cleanup=0 all_pass=" + str(Fixed) + "\n"


def Reject(WorkPath, Content, Host, Kind):
    WorkPath.write_text(Content, encoding="utf-8")
    try:
        Reader.ReadReceipt(WorkPath, Host, Kind)
    except ValueError:
        return
    raise AssertionError("invalid stack receipt accepted")


with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootStack-") as Directory:
    WorkPath = Path(Directory) / "stack.log"
    for Host in Reader.Pins:
        for Fixed in (False, True):
            Kind = "gap-fixed" if Fixed else "gap-negative"
            Good = Text(Host, Fixed)
            WorkPath.write_text(Good, encoding="utf-8")
            Parsed = Reader.ReadReceipt(WorkPath, Host, Kind)
            assert Parsed == Reader.ReadReceipt(WorkPath, Host, Kind)
            assert Parsed["Complete"] and Parsed["QualifiesCompatibility"] == Fixed and len(Parsed["Cases"]) == 12
            Result = subprocess.run([sys.executable, str(Path(Reader.__file__)), str(WorkPath), "--host", Host,
                                     "--kind", Kind, "--output", str(Path(Directory) / "receipt.json")],
                                    capture_output=True, text=True, timeout=10)
            assert Result.returncode == 0, Result.stderr
            assert json.loads((Path(Directory) / "receipt.json").read_text(encoding="utf-8")) == Parsed
            Lines = Good.splitlines(keepends=True)
            for Bad in (Good.replace("cases=12", "cases=11"), Good.replace("ledger=0", "ledger=1", 1),
                        Good.replace("created=0 accepted=0", "created=1 accepted=0", 1),
                        Good.replace("id=479143914", "id=479143915", 1),
                        Good.replace("original_stored=2", "original_stored=0", 1),
                        Good.replace("selected_delta=1", "selected_delta=0", 1),
                        Good.replace("old=2 new=1", "old=2 new=2", 1),
                        Good.replace("D8_STACK_GAP item=sulfur", "D8_STACK_GAP item=cloth", 1),
                        Good.replace("D8_STACK_GAP", "D8_STACK_UNKNOWN", 1),
                        Good.replace("representatives=6", "representatives=5"),
                        Good.replace("amount_cap=1000", "amount_cap=2000"),
                        Good.replace("world_cap=8", "world_cap=9"),
                        Good.replace(Reader.Prefabs[1], Reader.Prefabs[0], 1),
                        Good.replace("2634.289.1", "2634.289.2"),
                        Good.replace(Reader.Pins[Host][2], "00000000-0000-0000-0000-000000000000"),
                        "".join(Lines[:-1]), "".join(Lines[:5] + Lines[6:]), Good + Lines[-1], Good + Lines[-2],
                        "".join(Lines[:6] + [Lines[6]] + Lines[6:])):
                Reject(WorkPath, Bad, Host, Kind)
            Reject(WorkPath, Good, Host, "gap-negative" if Fixed else "gap-fixed")
            Reject(WorkPath, Good, "Linux" if Host == "Windows" else "Windows", Kind)
    PreviousMaximum = Reader.MaximumBytes
    try:
        Reader.MaximumBytes = 64
        Reject(WorkPath, Text("Windows", False), "Windows", "gap-negative")
    finally:
        Reader.MaximumBytes = PreviousMaximum

print("[CarbonLuau:LootStackTest] PASS both exact hosts, distinct old-gap negative/fixed behavior, exact twelve mutation/result/cleanup pairs, malformed/duplicate/identity/drift rejection and actual CLI")
