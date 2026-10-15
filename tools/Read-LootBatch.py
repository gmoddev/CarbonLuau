"""Read bounded D8 receipts as data; batch completion is not an all-items PASS."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote

MaximumBytes = 64 * 1024 * 1024
Discovery = re.compile(r"D8_DISCOVERY index=(\d+) name=(.*?) item_id=(-?\d+) stackable=(\d+) category=(\S+) family=(\S+) (.*?) result=(CANDIDATE|SKIP|FAIL) reason=(.*)$")
Item = re.compile(r"D8_(PUBLIC_ITEM|ITEM) name=(.*?) item_id=(-?\d+) stackable=(\d+) family=(\S+) result=(PASS|FAIL) (.*)$")
Physical = re.compile(r"D8_PHYSICAL name=(.*?) item_id=(-?\d+) stackable=(\d+) amount=(\d+) uid=(\d+) world_id=(\d+) world_prefab=(.*?) epoch=(\d+) reciprocal=true completed_spawn=true(?: |$)")
Clean = re.compile(r"D8_PHYSICAL_CLEAN name=(.*?) amount=(\d+) ledger=0 original_removed=true")
CleanupBlocker = re.compile(r"D8_ITEM name=(.*?) item_id=(-?\d+) result=FAIL reason=exact_cleanup_blocker(.*)$")


def Need(Condition, Message):
    if not Condition:
        raise ValueError(Message)


def ReadReceipt(Path, AllowIncomplete=False):
    Raw = Path.read_bytes()
    Need(len(Raw) <= MaximumBytes, "receipt exceeds bounded input")
    Result = {"Log": str(Path), "SHA256": hashlib.sha256(Raw).hexdigest(), "Discovery": [], "Items": [], "Public": [], "Physical": []}
    Indices, Names, PhysicalNames, Cleaned = set(), set(), {}, set()
    Completion = None
    for Line in Raw.decode("utf-8-sig").splitlines():
        Need(len(Line) <= 65536, "receipt line exceeds bound")
        Match = Discovery.search(Line)
        if Match:
            Index, Name, Id, Stack, Category, Family, Details, Status, Reason = Match.groups()
            Name = unquote(Name); Index = int(Index)
            Need(Index not in Indices and Name not in Names and len(Indices) < 4096, "duplicate/excess discovery identity")
            Indices.add(Index); Names.add(Name)
            Result["Discovery"].append({"Index": Index, "Name": Name, "Id": int(Id), "Stack": int(Stack),
                "Category": Category, "Family": Family, "Status": Status, "Reason": unquote(Reason)})
        Match = Item.search(Line)
        if re.search(r"\bD8_PUBLIC_ITEM\b", Line):
            Need(Match is not None and Match[1] == "PUBLIC_ITEM", "unknown/malformed public item record")
        if Match:
            Kind, Name, Id, Stack, Family, Status, Detail = Match.groups()
            Target = "Public" if Kind == "PUBLIC_ITEM" else "Items"
            Name = unquote(Name)
            Need(not any(Row["Name"] == Name for Row in Result[Target]) and len(Result[Target]) < 512,
                 "duplicate/excess per-item result")
            Result[Target].append({"Name": Name, "Id": int(Id), "Stack": int(Stack), "Family": Family,
                                  "Status": Status, "Detail": unquote(Detail)})
        Match = Physical.search(Line)
        if Match:
            Name, Id, Stack, Amount, Uid, World, Prefab, Epoch = Match.groups()
            Name = unquote(Name); Amount = int(Amount)
            Need(int(Uid) > 0 and int(World) > 0 and int(Epoch) > 0 and 1 <= Amount <= min(int(Stack), 1000),
                 "invalid claimed physical identity/quantity")
            Need(len(Result["Physical"]) < 1536, "physical receipt count bound exceeded")
            Result["Physical"].append({"Name": Name, "Id": int(Id), "Stack": int(Stack), "Amount": Amount,
                "Uid": Uid, "World": World, "Prefab": Prefab, "Epoch": int(Epoch)})
            Need((Name, Amount) not in PhysicalNames, "duplicate physical quantity receipt")
            PhysicalNames[Name, Amount] = True
        Match = CleanupBlocker.search(Line)
        if Match:
            Name, Id, Detail = Match.groups()
            Name = unquote(Name)
            Definition = next((Row for Row in Result["Discovery"] if Row["Name"] == Name and Row["Id"] == int(Id)), None)
            Need(Definition is not None and not any(Row["Name"] == Name for Row in Result["Items"]),
                 "cleanup blocker lacks exact discovered identity or repeats an item")
            Result["Items"].append({"Name": Name, "Id": int(Id), "Stack": Definition["Stack"], "Family": Definition["Family"],
                "Status": "FAIL", "Detail": "reason=exact_cleanup_blocker" + unquote(Detail)})
        Match = Clean.search(Line)
        if Match:
            Name, Amount = Match.groups()
            Key = unquote(Name), int(Amount)
            Need(Key not in Cleaned, "duplicate physical cleanup receipt")
            Cleaned.add(Key)
        if "PASS D8 bulk probe batch " in Line:
            Match = re.search(r"definitions=(\d+) candidates=(\d+) pass=(\d+) fail=(\d+) skip=(\d+)", Line)
            Need(Match is not None and Completion is None, "invalid/duplicate probe completion")
            Completion = {"Kind": "Probe", "Definitions": int(Match[1]), "Candidates": int(Match[2]),
                          "Pass": int(Match[3]), "Fail": int(Match[4]), "Skip": int(Match[5])}
        if "PASS D8 bulk public batch " in Line:
            Match = re.search(r"offset=(\d+) count=(\d+) pass=(\d+) fail=(\d+)", Line)
            Need(Match is not None and Completion is None, "invalid/duplicate public completion")
            Completion = {"Kind": "Public", "Offset": int(Match[1]), "Count": int(Match[2]),
                          "Pass": int(Match[3]), "Fail": int(Match[4])}
    if Completion is None:
        Need(AllowIncomplete and Result["Discovery"] and not Result["Public"],
             "no completed D8 batch; preserve this log as negative evidence")
        Completion = {"Kind": "Probe", "Complete": False, "Definitions": len(Indices),
            "Candidates": sum(Row["Status"] == "CANDIDATE" for Row in Result["Discovery"]),
            "Pass": sum(Row["Status"] == "PASS" for Row in Result["Items"]),
            "Fail": sum(Row["Status"] == "FAIL" for Row in Result["Items"])}
    else:
        Completion["Complete"] = True
    Rows = Result["Items"] if Completion["Kind"] == "Probe" else Result["Public"]
    Need(Completion["Pass"] == sum(Row["Status"] == "PASS" for Row in Rows) and
         Completion["Fail"] == sum(Row["Status"] == "FAIL" for Row in Rows), "per-item totals disagree with completion")
    if Completion["Kind"] == "Probe":
        Need(Completion["Definitions"] == len(Indices) and Indices == set(range(len(Indices))), "discovery census is incomplete")
        Need(Completion["Candidates"] == len(Rows) if Completion["Complete"] else Completion["Candidates"] >= len(Rows),
             "probe candidate row count inconsistent")
        if not Completion["Complete"]:
            Attempted = {Row["Name"] for Row in Rows}
            Result["Unattempted"] = [Row["Name"] for Row in Result["Discovery"]
                                     if Row["Status"] == "CANDIDATE" and Row["Name"] not in Attempted]
        for Row in Rows:
            if Row["Status"] != "PASS":
                continue
            Maximum = min(Row["Stack"], 1000)
            Expected = {(Row["Name"], Amount) for Amount in {1, Maximum, max(1, Maximum - 1)}}
            Need(Expected.issubset(PhysicalNames) and Expected.issubset(Cleaned), "item PASS lacks individual physical/cleanup quantities")
            Need(all(Proof["Id"] == Row["Id"] and Proof["Stack"] == Row["Stack"] for Proof in Result["Physical"]
                     if Proof["Name"] == Row["Name"]), "physical name/ID/stack disagrees with item result")
    else:
        Need(Completion["Count"] == len(Rows) and Completion["Count"] <= 32, "public per-item batch rows incomplete")
    Result["Completion"] = Completion
    return Result


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("log", type=Path)
    Parser.add_argument("--output", type=Path, help="write a normalized receipt to an isolated artifact path")
    Parser.add_argument("--allow-incomplete", action="store_true", help="retain an explicitly incomplete negative batch and separate prior individual receipts")
    Args = Parser.parse_args()
    Result = ReadReceipt(Args.log, Args.allow_incomplete)
    if Args.output:
        Args.output.write_text(json.dumps(Result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    Summary = dict(Result["Completion"], SHA256=Result["SHA256"])
    print("[CarbonLuau:LootBatch] " + json.dumps(Summary, sort_keys=True))


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, OSError, UnicodeError) as Error:
        print("[CarbonLuau:LootBatch] FAIL " + str(Error), file=sys.stderr)
        sys.exit(1)
