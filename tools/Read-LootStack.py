"""Read bounded exact-host D8 stack receipts; an intentional gap negative never qualifies compatibility."""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True

MaximumBytes = 64 * 1024 * 1024
Representatives = {"sulfur": -1581843485, "gears": 479143914, "antiradpills": -1432674913,
                   "lowgradefuel": -946369541, "hat.bunnyhat": 23391694, "wood": -151838493}
Prefabs = ("assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
           "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab")
Pins = {
    "Windows": ("2.0.262.0", "8a81d70", "c1c11bd1-baa1-4d85-b64e-1d4a4664d62e",
                "a6851b9116507fcb7e054118e476e0cac8b6a51d39cd10e0824b19e68d0694af"),
    "Linux": ("2.0.261.0", "c74c4ca", "616082a0-36f4-4680-a1ab-efc5766796cc",
              "993ae54aa5e6c11444abee185f2fb90bd3c5be17d883b3357d88150bc0ad7329")}
Begin = re.compile(r"D8_STACK_BEGIN mode=gap_probe representatives=6 profiles=2 production_engine_hooks_added=0 world_cap=8 amount_cap=1000$")
Mutation = re.compile(r"D8_STACK_MUTATION point=after_prepare item=([a-z0-9._-]+) old=2 new=1 planned=2 committed=false created=0$")
Gap = re.compile(r"D8_STACK_GAP item=([a-z0-9._-]+) id=(-?\d+) prefab=(\S+) result=(PASS|FAIL) committed=(True|False) created=0 accepted=0 selected_delta=1 partial_delta=(\d+) original_stored=(\d+) vanilla_worlds=(\d+) reason=(\S+)$")
Cleanup = re.compile(r"D8_STACK_CLEANUP item=([a-z0-9._-]+) prefab=(\S+) ledger=0$")
Batch = re.compile(r"D8_STACK_BATCH mode=gap_probe completed=true cases=12 pass=(\d+) fail=(\d+) cleanup=0 all_pass=(True|False)$")
PhaseNames = ("vanilla", "definition_increase", "definition_decrease", "definition_decrease_reject",
              "hook_higher", "hook_lower", "hook_zero", "hook_negative", "hook_invalid_type", "container_limits",
              "can_stack_overrides", "split_default", "split_override", "competing_hooks", "dynamic_before_prepare",
              "postcommit_definition_drop", "drop_amount_interference", "drop_split_interference", "fixed_caps")
VanillaStacks = {"sulfur": 1000, "gears": 20, "antiradpills": 10, "lowgradefuel": 500, "hat.bunnyhat": 1, "wood": 1000}
WorldRecord = re.compile(r"\[CarbonLuau:LootFixture\] WORLD item=(\S+) amount=(\d+) uid=(\d+) id=(\d+) class=(\S+) entry=(\d+) planned=(\d+) prefab=(\S+)$")
Kinds = ("gap-negative", "gap-fixed", "matrix0", "matrix1", "terminal-uid", "sweep")


def Need(Value, Message):
    if not Value:
        raise ValueError(Message)


def Fields(Record, Names):
    Need(set(Record["Values"]) == set(Names.split()), "unknown/missing fields in " + Record["Tag"])
    return Record["Values"]


def Equal(Values, Expected):
    Need(all(Values.get(Key) == str(Value) for Key, Value in Expected.items()), "stack record premise/outcome drift")


def Positive(Value):
    Need(re.fullmatch(r"[1-9][0-9]{0,19}", Value) is not None and int(Value) <= 18446744073709551615,
         "invalid physical UID/world/epoch")
    return int(Value)


def Records(Lines):
    Result = []
    for Number, Line in enumerate(Lines, 1):
        Match = re.search(r"\b(D8_STACK_[A-Z_]+) (.*)$", Line)
        if Match is None:
            Need(re.search(r"\bD8_STACK_[A-Z_]+\b", Line) is None, "malformed stack record")
            continue
        Values = {}
        Tokens = Match[2].split(" ")
        Need(len(Tokens) <= 32 and len(Result) < 4096, "stack record count/field bound exceeded")
        for Token in Tokens:
            Pair = Token.split("=")
            Need(len(Pair) == 2 and Pair[0] not in Values and Pair[0] and Pair[1], "malformed/duplicate stack field")
            Values[Pair[0]] = Pair[1]
        Result.append({"Tag": Match[1][9:], "Values": Values, "Line": Number})
    return Result


def PairRecords(Group, Tag, Schema, Expected):
    Selected = [Record for Record in Group if Record["Tag"] == Tag]
    Need(len(Selected) == 2, "missing/duplicate both-prefab " + Tag + " evidence")
    Need({Fields(Record, Schema)["prefab"] for Record in Selected} == set(Prefabs), "both-prefab evidence drift")
    for Record in Selected:
        Equal(Record["Values"], Expected)
    return Selected


def MatrixGroup(Group, Worlds, Name, Phase):
    Label = PhaseNames[Phase]
    ExpectedCounts = Counter()
    if Phase not in (14, 15, 18):
        ExpectedCounts["PUBLIC"] = 2
        Planned = min(VanillaStacks[Name], 1000) if Phase == 0 else 1000 if Phase == 1 else 1 if Phase == 3 else 10 if Phase == 4 else 2
        Stack = VanillaStacks[Name] if Phase == 0 else 2000 if Phase == 1 else 2 if Phase == 2 else 3 if Phase == 3 else 10
        PairRecords(Group, "PUBLIC", "item case prefab planned worlds success definition_stack max_calls can_calls split_calls drop_calls diagnostic_calls_excluded cleanup",
            {"item": Name, "case": "same_frame_prime" if Phase == 3 else Label, "planned": Planned,
             "worlds": 2 if Phase == 17 else 1, "success": Phase not in (16, 17), "definition_stack": Stack,
             "max_calls": 0, "can_calls": 0, "split_calls": 1 if Phase == 17 else 0,
             "drop_calls": 2 if Phase == 17 else 1, "diagnostic_calls_excluded": "true", "cleanup": 0})
    if 4 <= Phase <= 9:
        Expected = {Label: (20 if Phase == 4 else 1 if Phase == 5 else 0 if Phase == 6 else -1 if Phase == 7 else 10, "False")}
        if Phase == 9:
            Expected = {"container_lower_no_increase": (3, "True"), "container_lower_allow_increase": (10, "True"),
                        "container_higher_no_increase": (10, "True"), "container_higher_allow_increase": (20, "True"),
                        "hook_after_container": (7, "True")}
        Effective = [Record for Record in Group if Record["Tag"] == "EFFECTIVE"]
        Need(len(Effective) == len(Expected) and {Record["Values"].get("case") for Record in Effective} == set(Expected),
             "effective/container limit evidence incomplete or duplicate")
        ExpectedCounts["EFFECTIVE"] = len(Expected)
        for Record in Effective:
            Values = Fields(Record, "item case actual definition_stack parent")
            Actual, Parent = Expected[Values["case"]]
            Equal(Values, {"item": Name, "actual": Actual, "definition_stack": 10, "parent": Parent})
    if Phase == 3:
        ExpectedCounts["MUTATION"] = 2
        PairRecords(Group, "MUTATION", "point item old new planned vanilla_worlds prefab cleanup",
                    {"point": "before_prepare_decrease", "item": Name, "old": 3, "new": 2, "planned": 3, "vanilla_worlds": 2, "cleanup": 0})
    if Phase == 4:
        ExpectedCounts["HIGHER_REJECT"] = 2
        PairRecords(Group, "HIGHER_REJECT", "item prefab definition_stack effective_hook declaration_max vanilla_worlds no_commit cleanup",
                    {"item": Name, "definition_stack": 10, "effective_hook": 20, "declaration_max": 20,
                     "vanilla_worlds": 2, "no_commit": "true", "cleanup": 0})
    if Phase == 10:
        ExpectedCounts["CAN"] = 3
        Selected = [Record for Record in Group if Record["Tag"] == "CAN"]
        Need(len(Selected) == 3 and {Record["Values"].get("mode") for Record in Selected} == {"0", "1", "2"}, "CanStack override coverage")
        Pairs = set()
        for Record in Selected:
            Values = Fields(Record, "item mode actual first_uid second_uid")
            Equal(Values, {"item": Name, "actual": Values["mode"] != "1"})
            Need(Positive(Values["first_uid"]) != Positive(Values["second_uid"]), "CanStack diagnostic Item alias")
            Pairs.add((Values["first_uid"], Values["second_uid"]))
        Need(len(Pairs) == 1, "CanStack modes must examine the same exact two Items")
    if Phase == 9:
        ExpectedCounts.update({"CONTAINER_MOVE": 1, "CONTAINER_DROP": 1})
        Moves = [Record for Record in Group if Record["Tag"] == "CONTAINER_MOVE"]
        Drops = [Record for Record in Group if Record["Tag"] == "CONTAINER_DROP"]
        Need(len(Moves) == len(Drops) == 1, "exact parented Item move/drop evidence missing or duplicate")
        Moved = Fields(Moves[0], "item moved uid original_uid slot parent_exact slot_exact")
        Equal(Moved, {"item": Name, "moved": True, "slot": 2, "parent_exact": True, "slot_exact": True})
        Dropped = Fields(Drops[0], "item uid original_uid amount effective_before parent_removed world epoch completed reciprocal max_calls can_calls split_calls drop_calls original_slot_empty")
        Equal(Dropped, {"item": Name, "amount": 10, "effective_before": 7, "parent_removed": "true", "completed": "true",
                        "reciprocal": "true", "max_calls": 0, "can_calls": 0, "split_calls": 0, "drop_calls": 1, "original_slot_empty": "true"})
        Need(Positive(Moved["uid"]) == Positive(Moved["original_uid"]) == Positive(Dropped["uid"]) == Positive(Dropped["original_uid"]),
             "container move/drop retargeted the original Item UID")
        Positive(Dropped["world"]); Positive(Dropped["epoch"])
    if Phase in (11, 12):
        ExpectedCounts["SPLIT"] = 1
        Selected = [Record for Record in Group if Record["Tag"] == "SPLIT"]
        Need(len(Selected) == 1, "missing/duplicate explicit split proof")
        Values = Fields(Selected[0], "item override original_uid clone_uid original_amount clone_amount original_world clone_world reciprocal completed a_split_calls b_split_calls hook_returned_exact")
        Equal(Values, {"item": Name, "override": Phase == 12, "original_amount": 4, "clone_amount": 1,
                       "reciprocal": "true", "completed": "true", "a_split_calls": 1, "b_split_calls": 1,
                       "hook_returned_exact": Phase == 12})
        Need(Positive(Values["original_uid"]) != Positive(Values["clone_uid"]) and
             Positive(Values["original_world"]) != Positive(Values["clone_world"]), "split aliases original Item/world")
    if Phase in (12, 17):
        ExpectedCounts["CHILD"] = 1 if Phase == 12 else 2
        Children = [Record for Record in Group if Record["Tag"] == "CHILD"]
        for Index, Record in enumerate(Children):
            Values = Fields(Record, "item uid amount world reciprocal")
            Equal(Values, {"item": Name, "amount": 1, "reciprocal": Phase == 17})
            Positive(Values["uid"])
            if Phase == 12:
                Equal(Values, {"world": 0})
                Split = next(Record["Values"] for Record in Group if Record["Tag"] == "SPLIT")
                Need(Values["uid"] == Split["clone_uid"], "private returned override child differs from actual SplitItem result")
            else:
                Positive(Values["world"])
                Need(Index < len(Prefabs), "excess callback child world")
                Original = next((World for World in Worlds if World["Prefab"] == Prefabs[Index]), None)
                Need(Original is not None and Positive(Values["uid"]) != Original["Uid"] and Positive(Values["world"]) != Original["World"],
                     "callback split child aliases original Item/world")
    if Phase == 13:
        ExpectedCounts["CONFLICT"] = 1
        Selected = [Record for Record in Group if Record["Tag"] == "CONFLICT"]
        Need(len(Selected) == 1, "competing plugin witness missing/duplicate")
        Values = Fields(Selected[0], "item a b actual a_calls b_calls order_assumed")
        Equal(Values, {"item": Name, "a": 7, "b": 9, "a_calls": 1, "b_calls": 1, "order_assumed": "false"})
        Need(Values["actual"] in ("7", "9"), "unobserved conflict result")
    if Phase == 14:
        ExpectedCounts["MUTATION"] = 2
        PairRecords(Group, "MUTATION", "point item old new planned public_worlds prefab cleanup",
                    {"point": "before_prepare", "item": Name, "old": 2, "new": 3, "planned": 3, "public_worlds": 1, "cleanup": 0})
    if Phase == 15:
        ExpectedCounts.update({"MUTATION": 2, "POSTCOMMIT_WORLD": 2, "POSTCOMMIT": 2})
        for Record in (Record for Record in Group if Record["Tag"] == "MUTATION"):
            Values = Fields(Record, "point item new first_planned second_planned")
            Equal(Values, {"point": "after_first_capture", "item": Name, "new": 1, "first_planned": 2, "second_planned": 2})
        Physical = PairRecords(Group, "POSTCOMMIT_WORLD", "item prefab uid world amount live_definition_stack accepted reciprocal completed second_created",
                    {"item": Name, "amount": 2, "live_definition_stack": 1, "accepted": "true", "reciprocal": "true", "completed": "true", "second_created": "false"})
        for Record in Physical:
            Values = Record["Values"]
            Need(any(World["Prefab"] == Values["prefab"] and World["Uid"] == Positive(Values["uid"]) and
                     World["World"] == Positive(Values["world"]) for World in Worlds), "post-COMMIT original world proof disagrees")
        PairRecords(Group, "POSTCOMMIT", "item prefab preserved_worlds preserved_amount live_definition_stack second_created partial_delta fallback compensated cleanup",
                    {"item": Name, "preserved_worlds": 1, "preserved_amount": 2, "live_definition_stack": 1,
                     "second_created": "false", "partial_delta": 1, "fallback": "false", "compensated": "false", "cleanup": 0})
    Need(Counter(Record["Tag"] for Record in Group) == ExpectedCounts, "missing/extra stack phase evidence")
    ExpectedWorlds = 8 if Phase == 18 else 1
    Need(len(Worlds) == 2 * ExpectedWorlds, "matrix phase lacks exact original physical worlds")
    Planned = min(VanillaStacks[Name], 1000) if Phase == 0 else 1000 if Phase in (1, 18) else 1 if Phase == 3 else 10 if Phase == 4 else 3 if Phase == 14 else 2
    Amount = 3 if Phase == 16 else 1 if Phase == 17 else Planned
    for Prefab in Prefabs:
        Selected = [World for World in Worlds if World["Prefab"] == Prefab]
        Need(len(Selected) == ExpectedWorlds and {World["Entry"] for World in Selected} == set(range(ExpectedWorlds)) and
             len({World["Uid"] for World in Selected}) == ExpectedWorlds and len({World["World"] for World in Selected}) == ExpectedWorlds and
             all(World["Name"] == Name and World["Amount"] == Amount and World["Planned"] == Planned and
                 World["Class"] == "DroppedItem" for World in Selected), "matrix physical quantity/identity/prefab mismatch")


def MatrixReceipt(Lines, Kind):
    Part = int(Kind[-1])
    All = Records(Lines)
    Need(All and All[0]["Tag"] == "BEGIN" and All[-1]["Tag"] == "BATCH", "matrix begin/terminal missing")
    Equal(Fields(All[0], "mode part representatives phases world_cap amount_cap engine_hooks_added"),
          {"mode": "matrix", "part": Part, "representatives": 3, "phases": 19, "world_cap": 8, "amount_cap": 1000, "engine_hooks_added": 0})
    Equal(Fields(All[-1], "mode part completed cases pass fail cleanup all_pass"),
          {"mode": "matrix", "part": Part, "completed": "true", "cases": 57, "pass": 57, "fail": 0, "cleanup": 0, "all_pass": True})
    Group, Cases, BeginLine = [], [], All[0]["Line"]
    Names = tuple(Representatives)[Part * 3:Part * 3 + 3]
    for Record in All[1:-1]:
        if Record["Tag"] != "CASE":
            Group.append(Record)
            continue
        Need(len(Cases) < 57, "matrix case count exceeded")
        Name, Phase = Names[len(Cases) // 19], len(Cases) % 19
        Equal(Fields(Record, "part item phase case result profiles cleanup"),
              {"part": Part, "item": Name, "phase": Phase, "case": PhaseNames[Phase], "result": "PASS", "profiles": 2, "cleanup": 0})
        Worlds = []
        for Number in range(BeginLine + 1, Record["Line"]):
            Match = WorldRecord.search(Lines[Number - 1])
            if Match:
                Worlds.append({"Name": Match[1], "Amount": int(Match[2]), "Uid": Positive(Match[3]), "World": Positive(Match[4]),
                               "Class": Match[5], "Entry": int(Match[6]), "Planned": int(Match[7]), "Prefab": Match[8], "Line": Number})
        MatrixGroup(Group, Worlds, Name, Phase)
        Cases.append({"Name": Name, "Id": Representatives[Name], "Phase": Phase, "Case": PhaseNames[Phase],
                      "Status": "PASS", "Line": Record["Line"], "Ledger": 0, "Evidence": Group, "Worlds": Worlds})
        Group, BeginLine = [], Record["Line"]
    Need(len(Cases) == 57 and not Group, "matrix incomplete or dangling detail")
    return {"Scope": "StackMatrixPart", "QualifiesCompatibility": True, "Cases": Cases,
            "Completion": {"Cases": 57, "Pass": 57, "Fail": 0, "Cleanup": 0}}


def TerminalReceipt(Lines):
    All = Records(Lines)
    Need([Record["Tag"] for Record in All] == ["LEGACY", "BEGIN", "TERMINAL", "BATCH"], "terminal record coverage/order drift")
    Equal(Fields(All[0], "before_terminal d3_d7_completed ledger b2_terminal_claim"),
          {"before_terminal": "true", "d3_d7_completed": "true", "ledger": 0, "b2_terminal_claim": "false"})
    Equal(Fields(All[1], "mode representatives profiles normal_cleanup_claim"),
          {"mode": "terminal_uid", "representatives": 1, "profiles": 1, "normal_cleanup_claim": "false"})
    Values = Fields(All[2], "item id result original_uid actual_uid world completed reciprocal_ref partial_delta cleanup_failed_delta max_calls can_calls split_calls drop_calls normal_cleanup_claim uid_restored authority_refreshed")
    Equal(Values, {"item": "sulfur", "id": Representatives["sulfur"], "result": "UNSUPPORTED_I12", "actual_uid": 0,
                   "completed": "true", "reciprocal_ref": "true", "partial_delta": 1, "cleanup_failed_delta": 1,
                   "max_calls": 0, "can_calls": 0, "split_calls": 0, "drop_calls": 1, "normal_cleanup_claim": "false",
                   "uid_restored": "false", "authority_refreshed": "false"})
    Positive(Values["original_uid"]); Positive(Values["world"])
    Equal(Fields(All[3], "mode completed cases unsupported_i12 normal_cleanup_claim process_restoration_required"),
          {"mode": "terminal_uid", "completed": "true", "cases": 1, "unsupported_i12": 1,
           "normal_cleanup_claim": "false", "process_restoration_required": "true"})
    return {"Scope": "UnsupportedUidInterference", "QualifiesCompatibility": False, "ExpectedInterference": True,
            "RequiresProcessRestoration": True, "Cases": [All[2]], "Evidence": All,
            "Completion": {"Cases": 1, "UnsupportedI12": 1, "NormalCleanupClaim": False}}


def SweepReceipt(Lines, Manifest):
    Need(type(Manifest) is dict and type(Manifest.get("Definitions")) is list and len(Manifest["Definitions"]) == 221,
         "sweep requires the exact complete finite manifest")
    All = Records(Lines)
    Need(All and All[0]["Tag"] == "SWEEP_BEGIN" and All[-1]["Tag"] == "SWEEP_BATCH", "sweep begin/terminal missing")
    Equal(Fields(All[0], "manifest profiles cases world_cap amount_cap"),
          {"manifest": 221, "profiles": 2, "cases": 442, "world_cap": 8, "amount_cap": 1000})
    Equal(Fields(All[-1], "completed definitions cases pass fail cleanup all_pass"),
          {"completed": "true", "definitions": 221, "cases": 442, "pass": 442, "fail": 0, "cleanup": 0, "all_pass": True})
    Need(len(All) == 2 + 442 * 3, "sweep requires exactly two worlds and one result per name/prefab")
    Cases = []
    for Index in range(442):
        Spec, Prefab = Manifest["Definitions"][Index // 2], Prefabs[Index % 2]
        Group = All[1 + Index * 3:4 + Index * 3]
        Need([Record["Tag"] for Record in Group] == ["SWEEP_WORLD", "SWEEP_WORLD", "SWEEP"], "sweep world/result order drift")
        Maximum = min(Spec["Stack"], 1000)
        Physical = []
        for Entry, Record in enumerate(Group[:2]):
            Values = Fields(Record, "name id prefab entry amount uid world epoch reciprocal completed_spawn")
            Equal(Values, {"name": Spec["Name"], "id": Spec["Id"], "prefab": Prefab, "entry": Entry,
                           "amount": 1 if Entry == 0 else Maximum, "reciprocal": "true", "completed_spawn": "true"})
            Physical.append((Positive(Values["uid"]), Positive(Values["world"]), Positive(Values["epoch"])))
        Need(Physical[0][0] != Physical[1][0] and Physical[0][1] != Physical[1][1], "sweep original Items/worlds alias")
        Equal(Fields(Group[2], "name id stackable family prefab minimum maximum result worlds cleanup original_uid_world_verified completed_spawn"),
              {"name": Spec["Name"], "id": Spec["Id"], "stackable": Spec["Stack"], "family": Spec["Family"], "prefab": Prefab,
               "minimum": 1, "maximum": Maximum, "result": "PASS", "worlds": 2, "cleanup": 0,
               "original_uid_world_verified": "true", "completed_spawn": "true"})
        Cases.append({"Name": Spec["Name"], "Id": Spec["Id"], "Stack": Spec["Stack"], "Family": Spec["Family"],
                      "Prefab": Prefab, "Status": "PASS", "Line": Group[2]["Line"], "Ledger": 0, "Evidence": Group})
    return {"Scope": "FinalSourceEveryItemPublicSweep", "QualifiesCompatibility": True, "Cases": Cases,
            "Completion": {"Definitions": 221, "Cases": 442, "Worlds": 884, "Pass": 442, "Fail": 0, "Cleanup": 0}}


def HostWitness(Lines, Host):
    Need(Host in Pins, "unknown host selector")
    Version, Revision, Mvid, Hash = Pins[Host]
    Carbon = re.compile(r"Carbon " + re.escape(Version) + r" \[[^\]\r\n]+\] " + Revision + " on " + Host + r"\s*$")
    Need(sum(Carbon.search(Line) is not None for Line in Lines) == 1, "missing/duplicate exact Carbon host witness")
    Need(any(re.search(r"Rust\s+115/2634\.289\.1 on release .* 166494\s*$", Line) for Line in Lines), "missing pinned Rust protocol/revision")
    Body = "LIVE_BODY method=LootContainer.Void OnDied(HitInfo) token=0x06005773 mvid=" + Mvid + \
        " raw_il_bytes=83 raw_il_sha256=" + Hash + " eh=0 raw_reflection_body_not_harmony_replacement=true"
    Need(any(Body in Line for Line in Lines), "missing exact host OnDied body witness")
    return {"Carbon": Version + "/" + Revision, "Mvid": Mvid, "OnDiedRawSHA256": Hash,
            "Protocol": "2634.289.1", "RustRevision": 166494}


def ReadReceipt(InputPath, Host, Kind, Manifest=None):
    Need(Kind in Kinds, "unsupported stack receipt kind")
    with InputPath.open("rb") as Stream:
        Raw = Stream.read(MaximumBytes + 1)
    Need(len(Raw) <= MaximumBytes, "stack receipt exceeds bounded input")
    Lines = Raw.decode("utf-8-sig").splitlines()
    Need(all(len(Line) <= 65536 for Line in Lines), "stack receipt line exceeds bound")
    Pin = HostWitness(Lines, Host)
    if Kind not in ("gap-negative", "gap-fixed"):
        Value = MatrixReceipt(Lines, Kind) if Kind.startswith("matrix") else TerminalReceipt(Lines) if Kind == "terminal-uid" else SweepReceipt(Lines, Manifest)
        return {"SchemaVersion": 1, "Log": str(InputPath), "SHA256": hashlib.sha256(Raw).hexdigest(), "Host": Host,
                "HostWitness": Pin, "Kind": Kind, "Complete": True, **Value}
    Started, Completed, Pending = False, False, None
    Rows, Seen = [], set()
    for Number, Line in enumerate(Lines, 1):
        Prefix = re.search(r"\bD8_STACK_[A-Z_]+\b", Line)
        if Prefix is None:
            continue
        Need(not Completed, "stack record after terminal batch")
        if Begin.search(Line):
            Need(not Started and Pending is None and not Rows, "duplicate/misordered stack begin")
            Started = True
            continue
        Need(Started, "stack record before begin")
        Match = Mutation.search(Line)
        if Match:
            Need(Pending is None and Match[1] in Representatives, "duplicate/unknown stack mutation")
            Pending = {"Name": Match[1], "MutationLine": Number}
            continue
        Match = Gap.search(Line)
        if Match:
            Name, Id, Prefab, Status, Committed, Partial, Stored, Vanilla, Reason = Match.groups()
            Key = Name, Prefab
            Need(Pending is not None and "ResultLine" not in Pending and Pending["Name"] == Name,
                 "stack result lacks its distinct preceding mutation")
            Need(Name in Representatives and int(Id) == Representatives[Name] and Prefab in Prefabs and Key not in Seen,
                 "stack representative identity/prefab duplicate or drift")
            Need(int(Stored) == 2, "stack gap requires both original stored Items")
            Expected = ("FAIL", "True", 1, 0, "stack_drop_after_prepare_suppresses_vanilla") if Kind == "gap-negative" else \
                ("PASS", "False", 0, 2, "stack_drop_before_commit_preserves_vanilla")
            Need((Status, Committed, int(Partial), int(Vanilla), Reason) == Expected,
                 "stack gap outcome disagrees with explicit negative/fixed selector")
            Pending.update({"Id": int(Id), "Prefab": Prefab, "Status": Status, "Committed": Committed == "True",
                            "Created": 0, "Accepted": 0, "SelectedDelta": 1, "PartialDelta": int(Partial),
                            "OriginalStored": int(Stored), "VanillaWorlds": int(Vanilla), "Reason": Reason,
                            "ResultLine": Number})
            Seen.add(Key)
            continue
        Match = Cleanup.search(Line)
        if Match:
            Need(Pending is not None and "ResultLine" in Pending and
                 (Match[1], Match[2]) == (Pending["Name"], Pending["Prefab"]), "stack cleanup lacks exact original row")
            Pending.update({"CleanupLine": Number, "Ledger": 0})
            Rows.append(Pending)
            Pending = None
            continue
        Match = Batch.search(Line)
        if Match:
            Expected = (0, 12, "False") if Kind == "gap-negative" else (12, 0, "True")
            Need(tuple((int(Match[1]), int(Match[2]), Match[3])) == Expected and Pending is None and
                 Seen == {(Name, Prefab) for Name in Representatives for Prefab in Prefabs},
                 "incomplete/contradictory stack terminal batch")
            Completed = True
            continue
        raise ValueError("unknown/malformed stack record at line " + str(Number))
    Need(Started and Completed and Pending is None and len(Rows) == 12, "stack batch did not complete twelve exact cleaned cases")
    return {"SchemaVersion": 1, "Scope": "PrepareCommitGapOnly", "Log": str(InputPath), "SHA256": hashlib.sha256(Raw).hexdigest(), "Host": Host,
            "HostWitness": Pin, "Kind": Kind, "Complete": True,
            "QualifiesCompatibility": Kind == "gap-fixed", "Cases": Rows,
            "Completion": {"Cases": 12, "Pass": 0 if Kind == "gap-negative" else 12,
                           "Fail": 12 if Kind == "gap-negative" else 0, "Cleanup": 0}}


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("log", type=Path)
    Parser.add_argument("--host", choices=tuple(Pins), required=True)
    Parser.add_argument("--kind", choices=Kinds, required=True)
    Parser.add_argument("--manifest", type=Path, help="finite pinned manifest required for the final-source sweep")
    Parser.add_argument("--output", type=Path, help="write normalized data beneath an isolated evidence directory")
    Args = Parser.parse_args()
    Manifest = None
    if Args.kind == "sweep":
        Spec = importlib.util.spec_from_file_location("LootStackManifest", Path(__file__).with_name("Generate-LootItems.py"))
        Module = importlib.util.module_from_spec(Spec); Spec.loader.exec_module(Module)
        Manifest = Module.ReadManifest(Args.manifest or Path(__file__).resolve().parent.parent / "api/loot-items.pinned.json")
    Result = ReadReceipt(Args.log, Args.host, Args.kind, Manifest)
    if Args.output:
        Args.output.write_text(json.dumps(Result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print("[CarbonLuau:LootStack] " + json.dumps({Key: Result[Key] for Key in
          ("Host", "Kind", "Complete", "QualifiesCompatibility", "SHA256", "Completion")}, sort_keys=True))


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, OSError, UnicodeError) as Error:
        print("[CarbonLuau:LootStack] FAIL " + str(Error), file=sys.stderr)
        sys.exit(1)
