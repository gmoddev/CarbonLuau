"""Reject incomplete or altered stack matrices, terminal interference and final-source physical sweeps."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
Spec = importlib.util.spec_from_file_location("LootStackPartReader", Path(__file__).with_name("Read-LootStack.py"))
Reader = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Reader)
Manifest = json.loads((Path(__file__).resolve().parent.parent / "api/loot-items.pinned.json").read_text(encoding="utf-8"))


def Header(Host):
    Version, Revision, Mvid, Digest = Reader.Pins[Host]
    return "Carbon " + Version + " [2026.10.01.0] " + Revision + " on " + Host + "\n" + \
        "Rust   115/2634.289.1 on release (01/10/2026 13:49:11) 166494\n" + \
        "LIVE_BODY method=LootContainer.Void OnDied(HitInfo) token=0x06005773 mvid=" + Mvid + \
        " raw_il_bytes=83 raw_il_sha256=" + Digest + " eh=0 raw_reflection_body_not_harmony_replacement=true\n"


def Row(Tag, **Values):
    return "D8_STACK_" + Tag + " " + " ".join(Key + "=" + str(Value) for Key, Value in Values.items()) + "\n"


def Matrix(Host, Part):
    Output = Header(Host) + Row("BEGIN", mode="matrix", part=Part, representatives=3, phases=19, world_cap=8, amount_cap=1000, engine_hooks_added=0)
    for Name in tuple(Reader.Representatives)[Part * 3:Part * 3 + 3]:
        for Phase, Label in enumerate(Reader.PhaseNames):
            Planned = min(Reader.VanillaStacks[Name], 1000) if Phase == 0 else 1000 if Phase in (1, 18) else 1 if Phase == 3 else 10 if Phase == 4 else 3 if Phase == 14 else 2
            for PrefabIndex, Prefab in enumerate(Reader.Prefabs):
                for Entry in range(8 if Phase == 18 else 1):
                    Uid = 10000 + PrefabIndex * 100 + Entry
                    Output += "[CarbonLuau:LootFixture] WORLD item=" + Name + " amount=" + str(3 if Phase == 16 else 1 if Phase == 17 else Planned) + \
                        " uid=" + str(Uid) + " id=" + str(Uid + 1000) + " class=DroppedItem entry=" + str(Entry) + \
                        " planned=" + str(Planned) + " prefab=" + Prefab + "\n"
                if Phase not in (14, 15, 18):
                    Stack = Reader.VanillaStacks[Name] if Phase == 0 else 2000 if Phase == 1 else 2 if Phase == 2 else 3 if Phase == 3 else 10
                    Output += Row("PUBLIC", item=Name, case="same_frame_prime" if Phase == 3 else Label, prefab=Prefab, planned=Planned,
                                  worlds=2 if Phase == 17 else 1, success=Phase not in (16, 17), definition_stack=Stack,
                                  max_calls=0, can_calls=0, split_calls=1 if Phase == 17 else 0,
                                  drop_calls=2 if Phase == 17 else 1, diagnostic_calls_excluded="true", cleanup=0)
                if Phase == 3:
                    Output += Row("MUTATION", point="before_prepare_decrease", item=Name, old=3, new=2, planned=3, vanilla_worlds=2, prefab=Prefab, cleanup=0)
                if Phase == 4:
                    Output += Row("HIGHER_REJECT", item=Name, prefab=Prefab, definition_stack=10, effective_hook=20, declaration_max=20,
                                  vanilla_worlds=2, no_commit="true", cleanup=0)
                if Phase == 14:
                    Output += Row("MUTATION", point="before_prepare", item=Name, old=2, new=3, planned=3, public_worlds=1, prefab=Prefab, cleanup=0)
                if Phase == 15:
                    Output += Row("MUTATION", point="after_first_capture", item=Name, new=1, first_planned=2, second_planned=2)
                    Output += Row("POSTCOMMIT_WORLD", item=Name, prefab=Prefab, uid=10000 + PrefabIndex * 100, world=11000 + PrefabIndex * 100,
                                  amount=2, live_definition_stack=1, accepted="true", reciprocal="true", completed="true", second_created="false")
                    Output += Row("POSTCOMMIT", item=Name, prefab=Prefab, preserved_worlds=1, preserved_amount=2, live_definition_stack=1,
                                  second_created="false", partial_delta=1, fallback="false", compensated="false", cleanup=0)
                if Phase == 17:
                    Output += Row("CHILD", item=Name, uid=9000 + PrefabIndex, amount=1, world=8000 + PrefabIndex, reciprocal=True)
            if 4 <= Phase <= 8:
                Output += Row("EFFECTIVE", item=Name, case=Label, actual={4: 20, 5: 1, 6: 0, 7: -1, 8: 10}[Phase], definition_stack=10, parent=False)
            if Phase == 9:
                for LimitCase, Actual in (("container_lower_no_increase", 3), ("container_lower_allow_increase", 10),
                                          ("container_higher_no_increase", 10), ("container_higher_allow_increase", 20), ("hook_after_container", 7)):
                    Output += Row("EFFECTIVE", item=Name, case=LimitCase, actual=Actual, definition_stack=10, parent=True)
                Output += Row("CONTAINER_MOVE", item=Name, moved=True, uid=4000, original_uid=4000, slot=2, parent_exact=True, slot_exact=True)
                Output += Row("CONTAINER_DROP", item=Name, uid=4000, original_uid=4000, amount=10, effective_before=7,
                              parent_removed="true", world=4001, epoch=4002, completed="true", reciprocal="true",
                              max_calls=0, can_calls=0, split_calls=0, drop_calls=1, original_slot_empty="true")
            if Phase == 10:
                for Mode in range(3):
                    Output += Row("CAN", item=Name, mode=Mode, actual=Mode != 1, first_uid=7000, second_uid=7001)
            if Phase in (11, 12):
                if Phase == 12:
                    Output += Row("CHILD", item=Name, uid=6001, amount=1, world=0, reciprocal=False)
                Output += Row("SPLIT", item=Name, override=Phase == 12, original_uid=6000, clone_uid=6001, original_amount=4, clone_amount=1,
                              original_world=5000, clone_world=5001, reciprocal="true", completed="true", a_split_calls=1, b_split_calls=1,
                              hook_returned_exact=Phase == 12)
            if Phase == 13:
                Output += Row("CONFLICT", item=Name, a=7, b=9, actual=9, a_calls=1, b_calls=1, order_assumed="false")
            Output += Row("CASE", part=Part, item=Name, phase=Phase, case=Label, result="PASS", profiles=2, cleanup=0)
    return Output + Row("BATCH", mode="matrix", part=Part, completed="true", cases=57, **{"pass": 57}, fail=0, cleanup=0, all_pass=True)


def Terminal(Host):
    return Header(Host) + Row("LEGACY", before_terminal="true", d3_d7_completed="true", ledger=0, b2_terminal_claim="false") + \
        Row("BEGIN", mode="terminal_uid", representatives=1, profiles=1, normal_cleanup_claim="false") + \
        Row("TERMINAL", item="sulfur", id=-1581843485, result="UNSUPPORTED_I12", original_uid=100, actual_uid=0, world=200,
            completed="true", reciprocal_ref="true", partial_delta=1, cleanup_failed_delta=1, max_calls=0, can_calls=0, split_calls=0,
            drop_calls=1, normal_cleanup_claim="false", uid_restored="false", authority_refreshed="false") + \
        Row("BATCH", mode="terminal_uid", completed="true", cases=1, unsupported_i12=1, normal_cleanup_claim="false", process_restoration_required="true")


def Sweep(Host):
    Output = Header(Host) + Row("SWEEP_BEGIN", manifest=221, profiles=2, cases=442, world_cap=8, amount_cap=1000)
    for Spec in Manifest["Definitions"]:
        for Prefab in Reader.Prefabs:
            Maximum = min(Spec["Stack"], 1000)
            for Entry in range(2):
                Output += Row("SWEEP_WORLD", name=Spec["Name"], id=Spec["Id"], prefab=Prefab, entry=Entry, amount=1 if Entry == 0 else Maximum,
                              uid=100 + Entry, world=200 + Entry, epoch=300 + Entry, reciprocal="true", completed_spawn="true")
            Output += Row("SWEEP", name=Spec["Name"], id=Spec["Id"], stackable=Spec["Stack"], family=Spec["Family"], prefab=Prefab,
                          minimum=1, maximum=Maximum, result="PASS", worlds=2, cleanup=0, original_uid_world_verified="true", completed_spawn="true")
    return Output + Row("SWEEP_BATCH", completed="true", definitions=221, cases=442, **{"pass": 442}, fail=0, cleanup=0, all_pass=True)


def Reject(InputPath, Content, Host, Kind):
    InputPath.write_text(Content, encoding="utf-8")
    try:
        Reader.ReadReceipt(InputPath, Host, Kind, Manifest)
    except ValueError:
        return
    raise AssertionError("altered/partial stack part accepted: " + Kind)


with tempfile.TemporaryDirectory(prefix="CarbonLuau-StackParts-") as Directory:
    InputPath = Path(Directory) / "part.log"
    for Host in Reader.Pins:
        for Kind, Good in (("matrix0", Matrix(Host, 0)), ("matrix1", Matrix(Host, 1)), ("terminal-uid", Terminal(Host)), ("sweep", Sweep(Host))):
            InputPath.write_text(Good, encoding="utf-8")
            Parsed = Reader.ReadReceipt(InputPath, Host, Kind, Manifest)
            assert Parsed == Reader.ReadReceipt(InputPath, Host, Kind, Manifest) and Parsed["Complete"]
            assert Parsed["QualifiesCompatibility"] == (Kind != "terminal-uid")
            Command = [sys.executable, str(Path(Reader.__file__)), str(InputPath), "--host", Host, "--kind", Kind]
            Result = subprocess.run(Command, capture_output=True, text=True, timeout=10)
            assert Result.returncode == 0, Result.stderr
            Lines = Good.splitlines(keepends=True)
            for Bad in ("".join(Lines[:-1]), Good + Lines[-1],
                        Good.replace("ledger=0", "ledger=1", 1) if Kind == "terminal-uid" else Good.replace("cleanup=0", "cleanup=1", 1),
                        Good.replace("world_cap=8", "world_cap=9", 1) if Kind != "terminal-uid" else Good.replace("uid_restored=false", "uid_restored=true"),
                        Good.replace("D8_STACK_", "D8_STACK_UNRECOGNIZED_", 1), Good + "D8_STACK_EXTRA item=sulfur\n"):
                assert Bad != Good, "test mutation did not alter the fixture"
                Reject(InputPath, Bad, Host, Kind)
            if Kind.startswith("matrix"):
                for Bad in (Good.replace("max_calls=0", "max_calls=1", 1), Good.replace("planned=1000", "planned=1001", 1),
                            Good.replace("original_amount=4", "original_amount=5", 1), Good.replace("b_split_calls=1", "b_split_calls=0", 1),
                            Good.replace("clone_uid=6001", "clone_uid=6999", 2),
                            Good.replace("hook_returned_exact=True", "hook_returned_exact=False", 1), Good.replace("actual=9 a_calls", "actual=10 a_calls", 1),
                            Good.replace("parent=True", "parent=False", 1), Good.replace("no_commit=true", "no_commit=false", 1),
                            Good.replace("effective_before=7", "effective_before=10", 1), Good.replace("original_uid=4000", "original_uid=4003", 1),
                            Good.replace("partial_delta=1", "partial_delta=0", 1), Good.replace("compensated=false", "compensated=true", 1),
                            Good.replace("first_planned=2", "first_planned=1", 1), Good.replace("uid=10000", "uid=0", 1)):
                    Reject(InputPath, Bad, Host, Kind)
            elif Kind == "terminal-uid":
                for Bad in (Good.replace("original_uid=100", "original_uid=0"), Good.replace("cleanup_failed_delta=1", "cleanup_failed_delta=0"),
                            Good.replace("authority_refreshed=false", "authority_refreshed=true"), Good.replace("normal_cleanup_claim=false", "normal_cleanup_claim=true")):
                    Reject(InputPath, Bad, Host, Kind)
            else:
                for Bad in (Good.replace("epoch=300", "epoch=0", 1), Good.replace("world=201", "world=200", 1),
                            Good.replace("uid=101", "uid=100", 1), Good.replace("family=Equivalent", "family=Baseline", 1),
                            Good.replace("entry=1", "entry=0", 1), Good.replace("id=-866121090", "id=-866121091", 1)):
                    Reject(InputPath, Bad, Host, Kind)

print("[CarbonLuau:LootStackPartsTest] PASS exact57-case matrices, causal hook counts, split-return identity, conflicting effective limits, post-COMMIT partials, unsupported terminal UID boundary and221-item442-case884-world sweep; actual CLI and drift rejection")
