"""D8 receipt reader must distinguish completion, item evidence and partial failures."""
import importlib.util
from pathlib import Path
import sys
import tempfile

sys.dont_write_bytecode = True
Spec = importlib.util.spec_from_file_location("LootBatchReader", Path(__file__).with_name("Read-LootBatch.py"))
Reader = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Reader)

with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootReceipt-") as Directory:
    Path = Path(Directory) / "fixture.log"
    Discovery = "D8_DISCOVERY index=0 name=test material item_id=-7 stackable=20 category=Resources family=RawResources condition_enabled=False result=CANDIDATE reason=fixture\n"
    Evidence = "".join("D8_PHYSICAL name=test material item_id=-7 stackable=20 amount=" + str(Amount) +
        " uid=" + str(100 + Amount) + " world_id=" + str(200 + Amount) + " world_prefab=fixture epoch=1 reciprocal=true completed_spawn=true\n" +
        "D8_PHYSICAL_CLEAN name=test material amount=" + str(Amount) + " ledger=0 original_removed=true\n" for Amount in (1, 19, 20))
    Item = "D8_ITEM name=test material item_id=-7 stackable=20 family=RawResources result=PASS reason=fixture\n"
    Finish = "PASS D8 bulk probe batch definitions=1 candidates=1 pass=1 fail=0 skip=0\n"
    Good = Discovery + Evidence + Item + Finish
    Path.write_text(Good, encoding="utf-8")
    Result = Reader.ReadReceipt(Path)
    assert Result["Discovery"][0]["Name"] == "test material" and Result["Completion"]["Pass"] == 1
    assert len(Result["Physical"]) == 3
    assert Result == Reader.ReadReceipt(Path), "normalized receipts not deterministic"
    for Bad in (Good.replace(Finish, ""), Good.replace("definitions=1", "definitions=2"),
                Good.replace("pass=1 fail=0", "pass=0 fail=1"), Discovery + Good,
                Good.replace("D8_PHYSICAL_CLEAN name=test material amount=19 ledger=0 original_removed=true\n", ""),
                Good.replace("D8_ITEM name=test material item_id=-7", "D8_ITEM name=test material item_id=-8"),
                Good + Item, Good.replace("uid=101", "uid=0")):
        Path.write_text(Bad, encoding="utf-8")
        try:
            Reader.ReadReceipt(Path)
        except ValueError:
            pass
        else:
            raise AssertionError("partial/contradictory/duplicate ownership receipt accepted")
    # A cleanly completed batch containing an item failure is valid data, never all-items PASS.
    Failed = Discovery + Item.replace("result=PASS", "result=FAIL") + Finish.replace("pass=1 fail=0", "pass=0 fail=1")
    Path.write_text(Failed, encoding="utf-8")
    assert Reader.ReadReceipt(Path)["Completion"]["Fail"] == 1
    Incomplete = Discovery + Evidence + Item + \
        "D8_DISCOVERY index=1 name=water item_id=-8 stackable=100 category=Resources family=RawResources flags=NoDropping result=CANDIDATE reason=fixture\n" + \
        "D8_ITEM name=water item_id=-8 result=FAIL reason=exact_cleanup_blocker first=unknown_returned_world\n"
    Path.write_text(Incomplete, encoding="utf-8")
    Partial = Reader.ReadReceipt(Path, AllowIncomplete=True)
    assert Partial["Completion"]["Complete"] is False and Partial["Completion"]["Fail"] == 1
    assert Partial["Items"][0]["Status"] == "PASS" and Partial["Items"][1]["Name"] == "water"
    try:
        Reader.ReadReceipt(Path)
    except ValueError:
        pass
    else:
        raise AssertionError("incomplete negative silently accepted as completed batch")
    Public = "D8_PUBLIC_ITEM name=test material item_id=-7 stackable=20 family=RawResources result=PASS reason=fixture\n" + \
        "PASS D8 bulk public batch offset=0 count=1 pass=1 fail=0\n"
    Path.write_text(Public, encoding="utf-8")
    assert Reader.ReadReceipt(Path)["Completion"]["Kind"] == "Public"
    for Extra in ("D8_PUBLIC_ITEM name=extra item_id=8 stackable=20 family=RawResources result=PENDING reason=fixture\n",
                  "D8_PUBLIC_ITEM name=extra unexpected=true\n"):
        Path.write_text(Public + Extra, encoding="utf-8")
        try:
            Reader.ReadReceipt(Path)
        except ValueError:
            pass
        else:
            raise AssertionError("extra unknown/malformed public record silently discarded")

print("[CarbonLuau:LootBatchTest] PASS deterministic census/per-item reading, real names with spaces, incomplete/duplicate/identity/cleanup rejection and retained item FAIL")
