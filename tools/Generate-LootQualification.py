"""Generate the complete D8 evidence ledger; final support requires both physical and public gates."""
import argparse
import copy
import difflib
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import zipfile
from urllib.parse import unquote

sys.dont_write_bytecode = True
Root = Path(__file__).resolve().parent.parent
Hosts = ("Windows", "Linux")
Phases = ("Initial", "Refined", "Resumed")
ExpectedDefinitions = 1307
MaximumReceiptBytes = 8 * 1024 * 1024
MaximumOutputBytes = 32 * 1024 * 1024
PublicReason = "reason=both_prefabs_public_zero_min_max_range_repeat_mixed_stack_boundary_stored_bonus_controlled_bag"
Baseline = {"scrap": -932201673, "metal.fragments": 69511070, "wood": -151838493, "stones": -2099697608}
Prefabs = ("assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
           "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab")
PublicMarkers = ("PASS public qualification", "PASS barrel-2 public qualification",
                 "PASS D5 item expansion full public wood matrix", "PASS D6 bounded per-prefab registry",
                 "PASS D7 stones full public", "PASS bounded public transport and actual RPC source",
                 "PASS actual RPC source/terminal proof and cleanup")
BaselineLogs = {
    "Windows": {"Log": "D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261013-171416-257/server.log",
                "SHA256": "fc3fbd96afdb9fa625fea38e5c3847a616c8ec23dd475d098f6e794c03e69aae"},
    "Linux": {"Log": "/root/codex/world-movement-20261003/evidence/loot-d2a-20261013-171752-1926279/server.log",
              "SHA256": "e8072fc4e80fa0bfeeadaa420b0bf84458e80ceed24af7e603347c16c114c485"}}


def Module(Name, Filename):
    Spec = importlib.util.spec_from_file_location(Name, Root / "tools" / Filename)
    Result = importlib.util.module_from_spec(Spec)
    Spec.loader.exec_module(Result)
    return Result


Batch = Module("LootQualificationBatch", "Read-LootBatch.py")
ManifestTool = Module("LootQualificationManifest", "Generate-LootItems.py")
StackTool = Module("LootQualificationStack", "Read-LootStack.py")
StackKinds = ("gap-fixed", "matrix0", "matrix1", "terminal-uid", "sweep")


def Need(Value, Message):
    if not Value:
        raise ValueError(Message)


def ReadJson(Path, Limit=MaximumReceiptBytes):
    with Path.open("rb") as Stream:
        Raw = Stream.read(Limit + 1)
    Need(len(Raw) <= Limit, "bounded JSON input exceeded: " + str(Path))
    return json.loads(Raw.decode("utf-8-sig"), object_pairs_hook=ManifestTool.Object), hashlib.sha256(Raw).hexdigest()


def ReadVerified(InputPath, Key, LogMap, AllowIncomplete=False):
    Value, Digest = ReadJson(InputPath)
    Need(type(Value) is dict and type(Value.get("Log")) is str and
         re.fullmatch(r"[0-9a-f]{64}", Value.get("SHA256", "")) is not None, "invalid normalized source identity")
    Local = Path(LogMap.get(Value["Log"], Value["Log"]))
    Need(Local.is_file(), "raw log required for UID/cleanup verification: " + Value["Log"])
    Fresh = Batch.ReadReceipt(Local, AllowIncomplete=AllowIncomplete)
    Need(Fresh["SHA256"] == Value["SHA256"], "raw log SHA256 differs from normalized receipt: " + Key)
    Fresh["Log"] = Value["Log"]
    Compatible = copy.deepcopy(Value)
    # Early normalized receipts predate this field; only the actual completed raw log permits adding it.
    if "Complete" not in Compatible.get("Completion", {}) and Fresh["Completion"].get("Complete") is True:
        Compatible["Completion"]["Complete"] = True
    Need(Compatible == Fresh, "normalized receipt disagrees with independently re-read raw log: " + Key)
    Raw = Local.read_bytes()
    Need(hashlib.sha256(Raw).hexdigest() == Value["SHA256"], "raw log changed during evidence verification: " + Key)
    Lines = Raw.decode("utf-8-sig").splitlines()
    Source = {"Key": Key, "Log": Value["Log"], "SHA256": Value["SHA256"], "ReceiptSHA256": Digest,
              "Completion": Fresh["Completion"], "Inspections": []}
    Refs = {"Discovery": {}, "Items": {}, "Public": {}, "Physical": {}, "Clean": {}}
    for Number, Line in enumerate(Lines, 1):
        Match = Batch.Discovery.search(Line)
        if Match:
            Refs["Discovery"][unquote(Match[2])] = {"Line": Number, "Details": unquote(Match[7])}
        Match = Batch.Item.search(Line)
        if Match:
            Refs["Public" if Match[1] == "PUBLIC_ITEM" else "Items"][unquote(Match[2])] = Number
        Match = Batch.CleanupBlocker.search(Line)
        if Match:
            Refs["Items"][unquote(Match[1])] = Number
        Match = Batch.Physical.search(Line)
        if Match:
            Refs["Physical"][unquote(Match[1]), int(Match[4])] = Number
        Match = Batch.Clean.search(Line)
        if Match:
            Refs["Clean"][unquote(Match[1]), int(Match[2])] = Number
        if " D8_MOD_CALLBACK " in Line or " D8_METADATA_" in Line or " D8_CYCLE_" in Line:
            Need(len(Source["Inspections"]) < 4096, "bounded method inspection inventory exceeded")
            Source["Inspections"].append({"Line": Number, "Detail": Line[Line.index("D8_"):]})
    return Fresh, Source, Refs, Lines


def Identity(Row):
    return Row["Name"], Row["Id"], Row["Stack"]


def DiscoveryIdentity(Row):
    return Identity(Row) + (Row["Category"], Row["Family"])


def FamilyMatches(Approved, Discovered):
    return (Approved == "Baseline" or Approved in ("RawResource", "BasicMaterial") and Discovered == "RawResources" or
            Approved == "Component" and Discovered == "Components" or Approved == "Consumable" and Discovered == "BasicConsumables" or
            Approved == "Equivalent" and Discovered.startswith("Equivalent:"))


def Exclusion(Reason):
    if Reason == "retained_shortname_budget15":
        return "BOUND_EXCLUDED"
    if Reason == "adapter_item_mod_limit8":
        return "BOUND_EXCLUDED"
    if Reason == "definition_NoDropping_flag":
        return "DROP_DISABLED"
    if Reason == "condition_durability_or_repair" or Reason == "nested_instance_data_and_global_spoil_queue":
        return "LIFECYCLE_EXCLUDED"
    if Reason.startswith("special_lifecycle:"):
        return "UNQUALIFIED_LIFECYCLE"
    return "UNPROVED"


def PublicControls(Lines, Spec, End, Begin):
    Controls = []
    for Prefab in Prefabs:
        Maximum = min(Spec["Stack"], 1000)
        Needles = {
            "Amounts": "D8_PUBLIC_AMOUNTS name=" + Spec["Name"] + " prefab=" + Prefab +
                " zero=0 minimum=1 maximum=" + str(Maximum) + " repeated=8 original_worlds_verified=true",
            "Suppression": "D8_PUBLIC_SUPPRESSION name=" + Spec["Name"] + " prefab=" + Prefab +
                " stored=true bonus=true controlled_bag=true natural_bag_branch=false",
            "StackBoundary": "D8_STACK_CONTROL name=" + Spec["Name"] + " prefab=" + Prefab +
                " original_stack=" + str(Spec["Stack"]) + " actual_stack=" + str(999 if Spec["Stack"] >= 1000 else Spec["Stack"]) +
                " declared_max=" + str(1000 if Spec["Stack"] >= 1000 else Spec["Stack"] + 1) +
                " registration_succeeded=true expected_precommit_vanilla=true controlled_premise=" + str(Spec["Stack"] >= 1000)}
        Result = {"Prefab": Prefab}
        for Kind, Needle in Needles.items():
            Matches = [Number for Number in range(Begin + 1, End) if Lines[Number - 1].endswith(Needle)]
            Need(len(Matches) == 1, "public item lacks exact distinct " + Kind + " proof on both prefabs: " + Spec["Name"])
            Result[Kind + "Line"] = Matches[0]
        Controls.append(Result)
    return Controls


def BuildLedger(Manifest, ProbePaths, PublicPaths, LogMap=None, RequirePublic=False, NegativePaths=None, PackageHashes=None):
    LogMap = LogMap or {}
    NegativePaths, PackageHashes = NegativePaths or {}, PackageHashes or {}
    Sources, Probes, Public, PublicAttempts, Canonical = {}, {}, {}, {}, None
    for Host in Hosts:
        for Phase in Phases:
            Key = "Probe." + Host + "." + Phase
            Result = ReadVerified(ProbePaths[Host, Phase], Key, LogMap, AllowIncomplete=Phase == "Refined")
            Receipt, Source, Refs, Lines = Result
            Need(Receipt["Completion"]["Kind"] == "Probe" and len(Receipt["Discovery"]) == ExpectedDefinitions,
                 "D8 requires the complete 1307-definition census for each probe source")
            Need(Phase == "Refined" or Receipt["Completion"]["Complete"], "initial/resumed probe must complete")
            Rows = Receipt["Discovery"]
            Need([Row["Name"] for Row in Rows] == sorted({Row["Name"] for Row in Rows}), "census must be unique ordinal names")
            Need(len({Row["Id"] for Row in Rows}) == len(Rows), "census repeats a numeric identity")
            Identities = [DiscoveryIdentity(Row) for Row in Rows]
            if Canonical is None:
                Canonical = Identities
            Need(Identities == Canonical, "discovered name/ID/stack/category/family drift across phases or hosts")
            Sources[Key] = Source
            Probes[Host, Phase] = Result
    Approved = {Row["Name"]: Row for Row in Manifest["Definitions"]}
    for Name, Id in Baseline.items():
        Need(Name in Approved and Approved[Name]["Id"] == Id and Approved[Name]["Stack"] == 1000 and
             Approved[Name]["Family"] == "Baseline", "four exact D7 baseline identities required")
    Names = {Row[0] for Row in Canonical}
    Need(set(Approved).issubset(Names), "enabled manifest name missing from complete discovery")
    for Host in Hosts:
        for Path in sorted(NegativePaths.get(Host, []), key=str):
            Receipt, Source, Refs, Lines = ReadVerified(Path, "PublicNegative." + Host, LogMap)
            Completion = Receipt["Completion"]
            Need(Completion["Kind"] == "Public" and Completion["Complete"] and Completion["Fail"] > 0,
                 "separate negative public source must retain its completed batch with actual item FAIL")
            Offset, Count = Completion["Offset"], Completion["Count"]
            Need(Offset >= 0 and Offset < len(Approved) and 1 <= Count <= 32 and Offset + Count <= len(Approved),
                 "negative public source does not match a bounded manifest slice")
            Need([Identity(Row) + (Row["Family"],) for Row in Receipt["Public"]] ==
                 [Identity(Row) + (Row["Family"],) for Row in Manifest["Definitions"][Offset:Offset + Count]],
                 "negative public identity drifts from manifest")
            Key = "PublicNegative." + Host + "." + str(Offset) + "." + Receipt["SHA256"][:12]
            Need(Key not in Sources, "duplicate negative public source")
            Source["Key"] = Key
            Sources[Key] = Source
            for Row in Receipt["Public"]:
                PublicAttempts.setdefault((Host, Row["Name"]), []).append({"Source": Key, "Line": Refs["Public"][Row["Name"]],
                    "Status": Row["Status"], "Detail": Row["Detail"], "QualifiesCurrentSupport": False})
        Slots = {}
        for Path in PublicPaths.get(Host, []):
            Receipt, Source, Refs, Lines = ReadVerified(Path, "Public." + Host, LogMap)
            Completion = Receipt["Completion"]
            Need(Completion["Kind"] == "Public" and Completion["Complete"] and Completion["Fail"] == 0,
                 "public batch is incomplete or contains failures")
            Offset, Count = Completion["Offset"], Completion["Count"]
            Need(type(Offset) is int and Offset >= 0 and Offset < len(Approved) and 1 <= Count <= 32 and
                 Offset + Count <= len(Approved) and Completion["Pass"] == Count and Offset not in Slots,
                 "public offsets/counts overlap or do not match exact bounded manifest batches")
            Expected = Manifest["Definitions"][Offset:Offset + Count]
            Need([Identity(Row) + (Row["Family"],) for Row in Receipt["Public"]] ==
                 [Identity(Row) + (Row["Family"],) for Row in Expected], "public batch names/IDs/stacks/families drift from exact manifest slice")
            Need(all(Row["Status"] == "PASS" and Row["Detail"] == PublicReason for Row in Receipt["Public"]),
                 "public item lacks the full both-prefab amount/suppression/stack matrix receipt")
            Need(any("PASS D8 bulk public batch " in Line and
                     "common_publication_failure_recovery_budget_matrix=true per_item_receipts_authoritative=true ledger=0" in Line
                     for Line in Lines), "public batch lacks common publication/failure/recovery/budget convergence")
            Need(all(any(Marker in Line for Line in Lines) for Marker in PublicMarkers) and
                 not any("[CarbonLuau:LootFixture] FAIL" in Line or "[CarbonLuau:LootFixture] CLEANUP_FAIL" in Line for Line in Lines),
                 "public batch lacks unchanged legacy/B2 convergence or contains fixture failure")
            Key = "Public." + Host + "." + str(Offset)
            Source["Key"] = Key
            Sources[Key] = Source
            Slots[Offset] = Count
            Begin = 0
            for Row, Spec in zip(Receipt["Public"], Expected):
                Need((Host, Row["Name"]) not in Public, "duplicate public item across batches")
                End = Refs["Public"][Row["Name"]]
                Public[Host, Row["Name"]] = {"Source": Key, "Line": End, "Status": "PASS", "Detail": Row["Detail"],
                    "BothPrefabControls": PublicControls(Lines, Spec, End, Begin)}
                PublicAttempts.setdefault((Host, Row["Name"]), []).append({**Public[Host, Row["Name"]], "QualifiesCurrentSupport": True})
                Begin = End
        if RequirePublic:
            Next = 0
            for Offset, Count in sorted(Slots.items()):
                Need(Offset == Next, "public batch gap/overlap; exact contiguous complete manifest required on " + Host)
                Next += Count
            Need(Next == len(Approved), "public batches missing; exact contiguous complete manifest required on " + Host)
    Entries = []
    for Index, (Name, Id, Stack, Category, Family) in enumerate(Canonical):
        Entry = {"Name": Name, "Id": Id, "Stack": Stack, "Category": Category, "DiscoveryFamily": Family,
                 "EnabledManifest": Name in Approved, "DiscoveryHistory": [], "LatestInspection": {},
                 "PhysicalHistory": [], "Public": {}, "PublicAttempts": {}, "PhysicalBothHosts": False}
        LatestReasons, PhysicalHosts = [], set()
        for Host in Hosts:
            for Phase in Phases:
                Receipt, Source, Refs, Lines = Probes[Host, Phase]
                Row = Receipt["Discovery"][Index]
                Entry["DiscoveryHistory"].append({"Source": Source["Key"], "Line": Refs["Discovery"][Name]["Line"],
                    "Status": Row["Status"], "Reason": Row["Reason"]})
                if Phase == "Resumed":
                    LatestReasons.append(Row["Reason"])
                    Entry["LatestInspection"][Host] = {"Source": Source["Key"], **Refs["Discovery"][Name]}
                for Attempt in Receipt["Items"]:
                    if Attempt["Name"] != Name:
                        continue
                    Need(Identity(Attempt) == (Name, Id, Stack), "physical result identity drifts from discovery")
                    Proofs = []
                    for Proof in Receipt["Physical"]:
                        if Proof["Name"] != Name:
                            continue
                        Amount = Proof["Amount"]
                        Need(Identity(Proof) == (Name, Id, Stack) and int(Proof["Uid"]) > 0 and int(Proof["World"]) > 0,
                             "physical UID/world identity missing or inconsistent")
                        Need((Name, Amount) in Refs["Clean"], "physical proof has no exact original cleanup")
                        Proofs.append({**Proof, "Line": Refs["Physical"][Name, Amount], "CleanupLine": Refs["Clean"][Name, Amount],
                                       "Reciprocal": True, "CompletedSpawn": True, "OriginalRemoved": True, "Ledger": 0})
                    if Attempt["Status"] == "PASS":
                        Maximum = min(Stack, 1000)
                        Need({Proof["Amount"] for Proof in Proofs} == {1, Maximum, max(1, Maximum - 1)},
                             "physical PASS lacks exact min/max/boundary quantities")
                        PhysicalHosts.add(Host)
                    Entry["PhysicalHistory"].append({"Source": Source["Key"], "Line": Refs["Items"][Name],
                        "Status": Attempt["Status"], "Detail": Attempt["Detail"], "Quantities": sorted(Proofs, key=lambda Proof: Proof["Amount"])})
            if (Host, Name) in Public:
                Entry["Public"][Host] = Public[Host, Name]
            if (Host, Name) in PublicAttempts:
                Entry["PublicAttempts"][Host] = PublicAttempts[Host, Name]
        Need(len(set(LatestReasons)) == 1, "latest cross-host discovery reason differs for " + Name)
        Entry["LatestDiscoveryReason"] = LatestReasons[0]
        Entry["PhysicalBothHosts"] = PhysicalHosts == set(Hosts)
        if Name in Baseline:
            Entry["PriorQualification"] = {"Phase": "D7", "Reference": "docs/GameplayD7-Validation.md",
                "EarlierBoundaries": ["docs/GameplayD2A-Validation.md", "docs/GameplayD5-Validation.md"],
                "CombinedPublicLogs": BaselineLogs,
                "Scope": "Four previously qualified baseline items; intentionally skipped by D8 physical probes. D8 public receipts remain required."}
            Entry["PhysicalGate"] = "PRIOR_D7_BASELINE"
        else:
            Entry["PhysicalGate"] = "BOTH_D8_PHYSICAL_PASS" if Entry["PhysicalBothHosts"] else "UNPROVED"
        if Name in Approved:
            Need(Identity(Approved[Name]) == (Name, Id, Stack) and FamilyMatches(Approved[Name]["Family"], Family),
                 "enabled identity/family differs from discovered definition")
            Need(Name in Baseline or Entry["PhysicalBothHosts"], "enabled manifest lacks independent both-host physical proof: " + Name)
            Entry["Family"] = Approved[Name]["Family"]
            Entry["MaximumPositiveAmount"] = min(Stack, 1000)
            Entry["Classification"] = "PASS" if len(Entry["Public"]) == len(Hosts) else "PENDING_PUBLIC"
        else:
            Entry["Classification"] = Exclusion(Entry["LatestDiscoveryReason"])
        Entry["Verdict"] = "PASS" if Entry["Classification"] == "PASS" else "PENDING_PUBLIC" if Entry["Classification"] == "PENDING_PUBLIC" else "SKIP"
        Entries.append(Entry)
    QualifiedNames = {Entry["Name"] for Entry in Entries if Entry["Name"] in Baseline or Entry["PhysicalBothHosts"]}
    CandidateSetComplete = set(Approved) == QualifiedNames
    Need(not RequirePublic or CandidateSetComplete,
         "D8 final manifest must equal four baseline names plus the complete both-host physical PASS intersection")
    Final = CandidateSetComplete and all(Entry["Verdict"] == "PASS" for Entry in Entries if Entry["EnabledManifest"])
    Need(not RequirePublic or Final, "public proof missing for one or more enabled items")
    Counts = {Status: sum(Entry["Classification"] == Status for Entry in Entries) for Status in sorted({Entry["Classification"] for Entry in Entries})}
    VerdictCounts = {Verdict: sum(Entry["Verdict"] == Verdict for Entry in Entries) for Verdict in sorted({Entry["Verdict"] for Entry in Entries})}
    FamilyCounts = {Family: sum(Row["Family"] == Family for Row in Approved.values()) for Family in sorted({Row["Family"] for Row in Approved.values()})}
    for Source in Sources.values():
        if Source["SHA256"] in PackageHashes:
            Source["PackageSHA256"] = PackageHashes[Source["SHA256"]]
    return {"SchemaVersion": 1, "Phase": "GameplayD8", "Status": "VANILLA_PUBLIC_COMPLETE" if Final else "PENDING_PUBLIC",
        "StackCompatibilityGate": "PENDING_WINDOWS_LINUX_STACK_MATRICES",
        "ManifestSHA256": hashlib.sha256(json.dumps(Manifest, sort_keys=True, separators=(",", ":")).encode("ascii")).hexdigest(),
        "HostPins": Manifest["HostPins"], "DefinitionCount": len(Entries), "EnabledCount": len(Approved), "Counts": Counts,
        "VerdictCounts": VerdictCounts, "EnabledFamilies": FamilyCounts, "CandidateSetComplete": CandidateSetComplete,
        "PhysicalIntersectionCount": len(QualifiedNames),
        "Interpretation": "Per-definition PASS requires enabled manifest, both physical gates (prior D7 for four baseline names), and actual public proof on both hosts. Overall FINAL additionally requires the Windows and Linux stack matrices, fixed PREPARE/COMMIT gap, final-source all-item sweep and retained I12 terminal negatives with process restoration. Skips express a bound or unqualified premise, not universal item unsafety. Incomplete batches remain negative; their independently cleaned individual PASS rows remain distinct.",
        "Sources": [Sources[Key] for Key in sorted(Sources)], "Definitions": Entries}


def Cell(Value):
    return str(Value).replace("|", "\\|").replace("\n", " ").replace("\r", " ")


def LedgerMarkdown(Ledger):
    Lines = ["# Gameplay D8 complete item ledger", "", "Generated deterministically by `tools/Generate-LootQualification.py`.", "",
        "Status: **" + Ledger["Status"] + "**. Census: " + str(Ledger["DefinitionCount"]) + "; enabled manifest: " + str(Ledger["EnabledCount"]) + ".", "",
        Ledger["Interpretation"], "", "The [machine ledger](GameplayD8-Items.json) retains every discovery history, latest condition/root/mod detail, individual failure and min/max/boundary UID/world/epoch/cleanup line reference. Source keys resolve to exact raw log paths and SHA256 below. [Qualification scope](../GameplayD8-Validation.md).", "",
        "| Name | ID | Stack | Category | Verdict | Classification | Latest discovery reason |", "|---|---:|---:|---|---|---|---|"]
    for Row in Ledger["Definitions"]:
        Lines.append("| " + " | ".join(Cell(Row[Key]) for Key in ("Name", "Id", "Stack", "Category", "Verdict", "Classification", "LatestDiscoveryReason")) + " |")
    Lines += ["", "## Exact evidence sources", "", "| Source | Completion | Log | SHA256 |", "|---|---|---|---|"]
    for Source in Ledger["Sources"]:
        Completion = Source["Completion"]
        Complete = ("complete" if Completion["Complete"] else "INCOMPLETE NEGATIVE") + "; PASS=" + str(Completion["Pass"]) + "; FAIL=" + str(Completion["Fail"])
        Lines.append("| " + " | ".join(Cell(Value) for Value in (Source["Key"], Complete, Source["Log"], Source["SHA256"])) + " |")
    Stack = Ledger.get("StackQualification")
    if Stack:
        Lines += ["", "## Stack compatibility evidence", "", "Gate: **" + Ledger["StackCompatibilityGate"] + "**. " + Stack["Scope"], "",
            "| Host | Component | Qualification | Raw log | SHA256 | Packet SHA256 | Runner transcript SHA256 |",
            "|---|---|---|---|---|---|---|"]
        for Host in Hosts:
            Sources = list(Stack["HistoricalNegatives"][Host]) + [Stack["Proofs"][Host][Kind] for Kind in StackKinds if Kind in Stack["Proofs"][Host]]
            for Source in Sources:
                Runner = Source.get("Runner", {})
                Verdict = ("expected UNSUPPORTED_I12; process restored" if Source["Kind"] == "terminal-uid" else
                           "qualified component; restored") if Source["QualifiesOverallPass"] else "retained negative only"
                Lines.append("| " + " | ".join(Cell(Value) for Value in (Host, Source["Kind"], Verdict, Source["Log"],
                    Source["SHA256"], Runner.get("PackageSHA256", "not supplied"), Runner.get("RunnerSHA256", "not supplied"))) + " |")
    if Ledger.get("InfrastructureAttempts"):
        Lines += ["", "## Retained infrastructure negatives", "",
            "Compiler failures ran zero cases; fixture failures retain only their independently verified completed prefix. Every whole attempt remains FAIL. These failures do not classify an item as unsafe or qualify stack compatibility.", "",
            "| Host | Result | Diagnostic | Runtime cases | Raw log SHA256 | Packet SHA256 | Runner SHA256 | Restoration |",
            "|---|---|---|---:|---|---|---|---|"]
        for Attempt in Ledger["InfrastructureAttempts"]:
            Diagnostic = (Attempt["CompilerDiagnostic"]["Code"] + " " + Attempt["CompilerDiagnostic"]["File"] + ":293") \
                if Attempt["Kind"] == "COMPILE_FAIL" else ("phase " + str(Attempt["NextUncompletedPhase"]) + " " +
                    StackTool.PhaseNames[Attempt["NextUncompletedPhase"]] + "; fatal/cleanup refusal")
            Lines.append("| " + " | ".join(Cell(Value) for Value in (Attempt["Host"], Attempt["OverallStatus"],
                Diagnostic, Attempt["RuntimeCases"],
                Attempt["SHA256"], Attempt["PackageSHA256"], Attempt["RunnerSHA256"], "owned process stopped; sources restored")) + " |")
    return "\n".join(Lines) + "\n"


def PublicMarkdown(Manifest):
    Digest = hashlib.sha256(json.dumps(Manifest, sort_keys=True, separators=(",", ":")).encode("ascii")).hexdigest()
    Lines = ["# Qualified Loot items", "", "Generated from the finite [pinned manifest](../../api/loot-items.pinned.json); canonical SHA256 `" + Digest + "`.", "",
        "These exact names support Loot Replace on the two qualified barrel prefabs through the pinned generic Item creation/drop/removal lifecycle. Future item use, equip, projectile hits, cooking or consumption are outside this qualification. The table records vanilla definition stack limits and vanilla positive maxima 1..min(stack,1000); zero is supported. Public declaration quantities remain bounded at 0..1000. Trusted plugins can change the live definition.stackable field. A declaration whose Max exceeds that live field leaves the occurrence vanilla before COMMIT; the declaration can remain published, and amounts are neither clamped nor split. Effective OnMaxStackable hooks and container policies govern separate inventory operations; this is not universal third-party, pickup or container compatibility. See the qualified stack matrix in the evidence below. Eight entries, shared actual-frame budget eight and all existing ownership/publication limits remain unchanged.", "",
        "Both pinned Windows and Linux hosts qualify each listed name's public declaration, zero/minimum/maximum, inclusive range, repeated/mixed entries, stack boundary and stored/bonus/controlled-bag suppression on both prefabs. Batch controls qualify publication, conflicts, replacement, unload, failure/recovery and the shared budget. See [Loot service](Services/Loot.md), [LootDropEntry](Types/LootDropEntry.md), the [complete census](../qualification/GameplayD8-Items.md) and [qualification evidence](../GameplayD8-Validation.md).", "",
        "The manifest is a build and qualification input, not a runtime setting; editing it does not enable arbitrary definitions. Runtime admission uses the generated finite checks. NoDropping definitions, unsupported lifecycle state and names outside the retained 15-character bound remain excluded; a skipped definition is not a claim of universal unsafety.", "",
        "| Exact item name | Numeric ID | Vanilla definition stack | Vanilla maximum positive amount | Family |", "|---|---:|---:|---:|---|"]
    for Row in Manifest["Definitions"]:
        Lines.append("| " + " | ".join(Cell(Value) for Value in (Row["Name"], Row["Id"], Row["Stack"], min(Row["Stack"], 1000), Row["Family"])) + " |")
    return "\n".join(Lines) + "\n"


def Outputs(Ledger, Manifest, RequirePublic):
    Result = {"docs/qualification/GameplayD8-Items.json": json.dumps(Ledger, indent=2, sort_keys=True, ensure_ascii=True) + "\n",
              "docs/qualification/GameplayD8-Items.md": LedgerMarkdown(Ledger)}
    if RequirePublic and Ledger["Status"] == "FINAL":
        Result["docs/api/LootItems.md"] = PublicMarkdown(Manifest)
    Need(sum(len(Text.encode("utf-8")) for Text in Result.values()) <= MaximumOutputBytes, "bounded generated output exceeded")
    return Result


def CheckPublicProjection(Directory, Manifest):
    Ledger, _ = ReadJson(Directory / "docs/qualification/GameplayD8-Items.json", MaximumOutputBytes)
    Need(type(Ledger) is dict and type(Ledger.get("SchemaVersion")) is int and Ledger.get("SchemaVersion") == 1 and Ledger.get("Phase") == "GameplayD8" and
         Ledger.get("Status") == "FINAL" and Ledger.get("StackCompatibilityGate") == "PASS",
         "tracked public ledger lacks final stack compatibility gate")
    Digest = hashlib.sha256(json.dumps(Manifest, sort_keys=True, separators=(",", ":")).encode("ascii")).hexdigest()
    Need(Ledger.get("ManifestSHA256") == Digest and Ledger.get("HostPins") == Manifest["HostPins"],
         "tracked public ledger manifest/host pins drift")
    Rows = Ledger.get("Definitions")
    Need(type(Rows) is list and len(Rows) == ExpectedDefinitions and Ledger.get("DefinitionCount") == len(Rows),
         "tracked public ledger complete census bound")
    Need(all(type(Row) is dict and type(Row.get("Name")) is str and type(Row.get("EnabledManifest")) is bool and
             Row.get("Verdict") == ("PASS" if Row["EnabledManifest"] else "SKIP") for Row in Rows) and
         len({Row["Name"] for Row in Rows}) == len(Rows), "tracked public ledger duplicate/malformed definition")
    Enabled = []
    for Row in Rows:
        if not Row["EnabledManifest"]:
            continue
        Need(Row.get("Verdict") == "PASS" and type(Row.get("Id")) is int and type(Row.get("Stack")) is int,
             "tracked enabled definition lacks PASS/integer identity")
        Enabled.append({Key: Row.get(Key) for Key in ("Name", "Id", "Stack", "Family")})
    Need(Enabled == Manifest["Definitions"] and Ledger.get("EnabledCount") == len(Enabled) and Ledger.get("CandidateSetComplete") is True,
         "tracked public ledger enabled name/ID/stack/family differs from manifest")
    Need((Directory / "docs/qualification/GameplayD8-Items.md").read_text(encoding="utf-8") == LedgerMarkdown(Ledger),
         "tracked public ledger Markdown projection drift")
    Need((Directory / "docs/api/LootItems.md").read_text(encoding="utf-8") == PublicMarkdown(Manifest),
         "public Loot support table drift")


def StackRunnerWitness(InputPath):
    with InputPath.open("rb") as Stream:
        Raw = Stream.read(64 * 1024 * 1024 + 1)
    Need(len(Raw) <= 64 * 1024 * 1024, "bounded stack runner transcript exceeded")
    Lines = Raw.decode("utf-8-sig").splitlines()
    Need(all(len(Line) <= 65536 for Line in Lines), "bounded stack runner transcript line exceeded")
    Prefix = r"\[CarbonLuau:BarrelLootResearchRunner\] "
    Evidence = re.compile(Prefix + r"EVIDENCE (.+) SHA256=([0-9a-fA-F]{64}) PACKAGE=([0-9a-fA-F]{64}) NATIVE=([0-9a-fA-F]{64}) COMPILER=([0-9a-fA-F]{64})$")
    Rows = [(Index, Evidence.search(Line)) for Index, Line in enumerate(Lines)]
    Rows = [(Index, Match) for Index, Match in Rows if Match]
    Need(len(Rows) == 1, "stack runner requires exactly one actual evidence witness")
    Cleanup = [Index for Index, Line in enumerate(Lines) if re.search(Prefix +
        r"CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored$", Line)]
    Exit = [Index for Index, Line in enumerate(Lines) if re.search(Prefix + r"PROCESS_EXIT (0|-1|-9|137) qualified Shutdown -> Process.Kill$", Line)]
    Need(len(Cleanup) == 1 and len(Exit) == 1 and max(Rows[0][0], Exit[0]) < Cleanup[0] and
         not any("CLEANUP_FAILED" in Line or "[CarbonLuau:LootFixture] FAIL" in Line or
                 "[CarbonLuau:LootFixture] CLEANUP_FAIL" in Line for Line in Lines),
         "stack runner lacks successful owned-process exit and exact source restoration")
    Match = Rows[0][1]
    Inputs = {}
    for Line in Lines:
        Input = re.search(Prefix + r"INPUT ([A-Za-z0-9_.-]+) SHA256=([0-9a-fA-F]{64})$", Line)
        if Input:
            Need(Input[1] not in Inputs, "duplicate actual runner input hash")
            Inputs[Input[1]] = Input[2].lower()
    Profile = None
    Snapshots = [(Index, re.search(Prefix + r"PROFILE_SNAPSHOT files=(\d+) file_cap=128 byte_cap=67108864 manifest_sha256=([0-9a-f]{64})$", Line))
                 for Index, Line in enumerate(Lines)]
    Snapshots = [(Index, Match) for Index, Match in Snapshots if Match]
    Restores = [(Index, re.search(Prefix + r"FullProfileRestored files=(\d+) bytes=(\d+) file_cap=128 byte_cap=67108864 metadata=true scope=db_companions_saves_navmesh manifest_sha256=([0-9a-f]{64})$", Line))
                for Index, Line in enumerate(Lines)]
    Restores = [(Index, Match) for Index, Match in Restores if Match]
    if Snapshots or Restores:
        Need(len(Snapshots) == 1 and len(Restores) == 1 and Snapshots[0][0] < min(Rows[0][0], Exit[0]) and
             max(Rows[0][0], Exit[0]) < Restores[0][0] < Cleanup[0] and
             int(Snapshots[0][1][1]) == int(Restores[0][1][1]) <= 128 and int(Restores[0][1][2]) <= 67108864 and
             Snapshots[0][1][2] == Restores[0][1][3], "terminal full-profile restoration missing, unbounded or inconsistent")
        Profile = {"Files": int(Restores[0][1][1]), "Bytes": int(Restores[0][1][2]), "ManifestSHA256": Restores[0][1][3],
            "SnapshotLine": Snapshots[0][0] + 1, "RestorationLine": Restores[0][0] + 1, "MetadataRestored": True,
            "Scope": "db_companions_saves_navmesh", "FileCap": 128, "ByteCap": 67108864}
    return {"RunnerLog": str(InputPath), "RunnerSHA256": hashlib.sha256(Raw).hexdigest(),
        "Log": Match[1], "SHA256": Match[2].lower(), "PackageSHA256": Match[3].lower(),
        "NativeSHA256": Match[4].lower(), "CompilerSHA256": Match[5].lower(),
        "EvidenceLine": Rows[0][0] + 1, "ProcessExitLine": Exit[0] + 1, "RestorationLine": Cleanup[0] + 1,
        "OwnedProcessExited": True, "SourceHashesRestored": True, "Inputs": Inputs, "FullProfileRestored": Profile}


def StackPackageWitness(Path, ExpectedHash, Kind, CurrentFixture=False):
    with Path.open("rb") as Stream:
        Raw = Stream.read(16 * 1024 * 1024 + 1)
    Need(len(Raw) <= 16 * 1024 * 1024 and hashlib.sha256(Raw).hexdigest() == ExpectedHash,
         "stack packet bytes differ from runner package witness")
    try:
        with zipfile.ZipFile(Path) as Archive:
            Need(len(Archive.infolist()) <= 256 and len(set(Archive.namelist())) == len(Archive.namelist()),
                 "bounded unique stack packet entries required")
            Names = set(Archive.namelist())
            Need({"CarbonLuau.LootFixtures.cs", "CarbonLuau.LootStackFixtures.cs"}.issubset(Names) and
                 not {"CarbonLuauStackProbeA.cs", "CarbonLuauStackProbeB.cs"}.intersection(Names),
                 "stack packet must select fixture and keep hook probes separate")
            Matrix = Kind in ("matrix0", "matrix1", "terminal-uid")
            Need(("CarbonLuau.LootStackMatrixFixtures.cs" in Names) == Matrix and
                 ("CarbonLuau.LootFinalItemFixtures.cs" in Names) == (Kind == "sweep") and
                 ("CarbonLuau.LootStackModeFixtures.cs" in Names) == (Kind != "gap-fixed"),
                 "actual stack packet selector differs from normalized component")
            if Kind != "gap-fixed":
                Mode = Archive.getinfo("CarbonLuau.LootStackModeFixtures.cs")
                Need(Mode.file_size <= 65536, "bounded generated stack selector exceeded")
                Part = {"matrix0": 0, "matrix1": 1, "terminal-uid": 2, "sweep": 3}[Kind]
                Selector = Archive.read(Mode).decode("utf-8")
                Need("GapOnly = false; Part = " + str(Part) + ";" in Selector,
                     "actual stack packet selected different bounded part")
                if CurrentFixture and Matrix:
                    ExpectedSelector = "namespace Carbon.Plugins { public partial class CarbonLuau { partial void SelectLootStackMode(ref bool GapOnly, ref int Part) { GapOnly = false; Part = " + str(Part) + "; } } }"
                    Need(Selector.strip() == ExpectedSelector, "final stack selector differs from exact generated source")
            if Matrix and CurrentFixture:
                Entry = Archive.getinfo("CarbonLuau.LootStackMatrixFixtures.cs")
                Need(Entry.file_size <= 256 * 1024 and Archive.read(Entry) ==
                     (Root / "tests/live/CarbonLuau.LootStackMatrixFixtures.cs").read_bytes(),
                     "final stack matrix differs from current corrected fixture source")
            Entry = Archive.getinfo("CarbonLuau.Loot.Carbon.cs")
            Need(Entry.file_size <= 256 * 1024, "bounded production stack adapter entry exceeded")
            Body = Archive.read(Entry)
    except zipfile.BadZipFile as Error:
        raise ValueError("invalid original stack packet ZIP") from Error
    return hashlib.sha256(Body).hexdigest()


def ApplyStackQualification(Ledger, Manifest, StackPaths, NegativePaths, RunnerPaths, LogMap, PackageMap, RequireStack=False):
    Need(all(len(StackPaths.get(Host, [])) <= 5 and len(NegativePaths.get(Host, [])) <= 4 and
             len(RunnerPaths.get(Host, [])) <= 5 for Host in Hosts), "stack receipt count exceeds bounded components")
    Runners = {Host: {} for Host in Hosts}
    for Host in Hosts:
        for Input in RunnerPaths.get(Host, []):
            Runner = StackRunnerWitness(Input)
            Need(Runner["SHA256"] not in Runners[Host], "duplicate stack runner raw-log witness")
            Runners[Host][Runner["SHA256"]] = Runner
    Proofs, Histories, Production = {Host: {} for Host in Hosts}, {Host: [] for Host in Hosts}, set()
    for Host in Hosts:
        for IsNegative, Paths in ((True, NegativePaths.get(Host, [])), (False, StackPaths.get(Host, []))):
            for Input in Paths:
                Value, Digest = ReadJson(Input)
                Need(type(Value) is dict, "normalized stack receipt must be an object")
                Kind = Value.get("Kind")
                Need(Value.get("Host") == Host and (Kind == "gap-negative" if IsNegative else Kind in StackKinds) and
                     type(Value.get("Log")) is str and re.fullmatch(r"[0-9a-f]{64}", Value.get("SHA256", "")),
                     "invalid normalized stack identity/kind/host")
                Local = Path(LogMap.get(Value["Log"], Value["Log"]))
                Fresh = StackTool.ReadReceipt(Local, Host, Kind, Manifest)
                Fresh["Log"] = Value["Log"]
                Need(Fresh == Value and Fresh["Complete"] is True, "normalized stack evidence differs from fresh raw proof")
                Need(hashlib.sha256(Local.read_bytes()).hexdigest() == Fresh["SHA256"], "stack raw evidence changed during verification")
                Source = copy.deepcopy(Fresh)
                Source["Key"] = "Stack." + Host + "." + Kind + "." + Source["SHA256"][:12]
                Source["ReceiptSHA256"] = Digest
                if IsNegative:
                    Need(not any(Row["SHA256"] == Source["SHA256"] for Row in Histories[Host]), "duplicate retained stack negative")
                    Source["QualifiesOverallPass"] = False
                    Histories[Host].append(Source)
                    continue
                Need(Kind not in Proofs[Host], "duplicate final stack component")
                Need(Source["SHA256"] in Runners[Host], "stack component lacks independently read actual runner restoration")
                Runner = Runners[Host][Source["SHA256"]]
                Need((Runner["FullProfileRestored"] is not None) == (Kind == "terminal-uid"),
                     "terminal UID proof requires bounded full-profile restoration; ordinary selectors do not mutate profile restore scope")
                Need(Runner["Log"].replace("\\", "/") == Source["Log"].replace("\\", "/"),
                     "stack runner evidence names a different original host log")
                HelperNames = {"CarbonLuauStackProbeA.cs", "CarbonLuauStackProbeB.cs"}
                ActualHelpers = HelperNames.intersection(Runner["Inputs"])
                Need(ActualHelpers == (HelperNames if Kind in ("matrix0", "matrix1", "terminal-uid") else set()),
                     "stack hook probes must be present only for explicit ordinary/terminal selectors")
                for Name in ActualHelpers:
                    Need(Runner["Inputs"][Name] == hashlib.sha256((Root / "tests/live" / Name).read_bytes()).hexdigest(),
                         "actual separate hook probe differs from qualified fixture source")
                Need(Runner["PackageSHA256"] in PackageMap, "stack runner package requires original local packet bytes")
                Production.add(StackPackageWitness(Path(PackageMap[Runner["PackageSHA256"]]), Runner["PackageSHA256"], Kind, CurrentFixture=True))
                Source["Runner"] = Runner
                Source["QualifiesOverallPass"] = True
                Proofs[Host][Kind] = Source
    ComponentsComplete = all(set(Proofs[Host]) == set(StackKinds) for Host in Hosts)
    Need(not RequireStack or ComponentsComplete, "both hosts require fixed gap, two representative matrices, terminal I12 negative and final-source sweep")
    Need(not RequireStack or all(Histories[Host] for Host in Hosts),
         "final stack qualification must retain original both-host gap counterexamples")
    Complete = ComponentsComplete and all(Histories[Host] for Host in Hosts)
    Need(len(Production) <= 1, "final stack components use different production adapter source")
    if Production:
        Need(Production == {hashlib.sha256((Root / "src/CarbonLuau/CarbonLuau.Loot.Carbon.cs").read_bytes()).hexdigest()},
             "stack evidence production adapter differs from current source")
    for Host in Hosts:
        Used = {Proof["SHA256"] for Proof in Proofs[Host].values()}
        Need(set(Runners[Host]) == Used, "unmatched stack runner transcript")
        for Key in ("NativeSHA256", "CompilerSHA256"):
            Need(len({Runner[Key] for Runner in Runners[Host].values()}) <= 1,
                 "same-host stack components use different native/compiler inputs")
    Ledger["StackQualification"] = {"RequiredComponents": list(StackKinds), "Proofs": Proofs,
        "HistoricalNegatives": Histories, "Complete": Complete,
        "ProductionAdapterSHA256": next(iter(Production)) if Production else None,
        "Scope": "Six representative definitions qualify the shared Create/Drop path; every enabled definition has a final-source public sweep. Effective inventory caps and terminal I12 interference do not imply universal third-party compatibility."}
    if Complete:
        for Entry in Ledger["Definitions"]:
            if not Entry["EnabledManifest"]:
                continue
            Entry["FinalSourcePublic"] = {}
            for Host in Hosts:
                Sweep = Proofs[Host]["sweep"]
                Cases = [Case for Case in Sweep["Cases"] if Case.get("Name") == Entry["Name"]]
                Need(len(Cases) == 2 and {Case.get("Prefab") for Case in Cases} == set(Prefabs),
                     "final-source sweep omits enabled definition's exact prefab pair")
                Entry["FinalSourcePublic"][Host] = {"Source": Sweep["Key"], "Cases": Cases}
        if Ledger["Status"] == "VANILLA_PUBLIC_COMPLETE":
            Ledger["Status"] = "FINAL"
            Ledger["StackCompatibilityGate"] = "PASS"
    return Ledger


def FailureRunnerWitness(RunnerPath, Host, PackageMap, Kind):
    Need(Host in Hosts, "unknown failure-attempt host")
    with RunnerPath.open("rb") as Stream:
        RunnerRaw = Stream.read(64 * 1024 * 1024 + 1)
    Need(len(RunnerRaw) <= 64 * 1024 * 1024, "bounded failure runner transcript exceeded")
    RunnerText = re.sub(r"\x1b\[[0-?]*[ -/]*[@-~]", "", RunnerRaw.decode("utf-8-sig"))
    RunnerLines = RunnerText.splitlines()
    Need(all(len(Line) <= 65536 for Line in RunnerLines), "bounded failure runner line exceeded")
    Prefix = r"\[CarbonLuau:BarrelLootResearchRunner\] "
    Inputs = {}
    for Number, Line in enumerate(RunnerLines):
        Match = re.search(Prefix + r"INPUT ([A-Za-z0-9_.-]+) SHA256=([0-9a-fA-F]{64})$", Line)
        if Match:
            Need(Match[1] not in Inputs, "duplicate failure-attempt input")
            Inputs[Match[1]] = {"SHA256": Match[2].lower(), "Line": Number + 1}
    Expected = {"package.cszip", "native.dll" if Host == "Windows" else "native.so",
        "compiler.exe" if Host == "Windows" else "compiler", "CarbonLuauStackProbeA.cs", "CarbonLuauStackProbeB.cs"}
    Need(set(Inputs) == Expected, "failed attempt requires exact packet/native/compiler/two-helper input hashes")
    Cleanup = [Number for Number, Line in enumerate(RunnerLines) if re.search(Prefix +
        r"CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored$", Line)]
    Need(len(Cleanup) == 1 and max(Value["Line"] for Value in Inputs.values()) <= Cleanup[0] and
         "Compile/fixture failure:" in RunnerText and "CLEANUP_FAILED" not in RunnerText and
         not any(re.search(Prefix + r"EVIDENCE ", Line) for Line in RunnerLines),
         "failed attempt lacks failed-run and owned-process/source restoration proof")
    HostLog = (r"D:[\\/]Sandbox[\\/]Codex[\\/]Entity1AStartup[\\/]evidence[\\/]loot-d2a-[0-9-]+[\\/]server\.log"
               if Host == "Windows" else r"/root/codex/world-movement-20261003/evidence/loot-d2a-[0-9-]+/server\.log")
    Logs = set(Match.replace("\\", "/") for Match in re.findall(HostLog, RunnerText))
    Need(len(Logs) == 1, "failed runner lacks one exact original host log path")
    PacketHash = Inputs["package.cszip"]["SHA256"]
    Need(PacketHash in PackageMap, "failed attempt requires original source packet bytes")
    Production = StackPackageWitness(Path(PackageMap[PacketHash]), PacketHash, Kind)
    return {"Log": next(iter(Logs)), "RunnerLog": str(RunnerPath), "RunnerSHA256": hashlib.sha256(RunnerRaw).hexdigest(),
        "Inputs": Inputs, "PackageSHA256": PacketHash, "ProductionAdapterSHA256": Production,
        "Restoration": {"OwnedProcessExited": True, "SourceHashesRestored": True, "RunnerLine": Cleanup[0] + 1,
            "NormalShutdownClaim": False}}


def ReadCompileAttempt(RawPath, RunnerPath, Host, PackageMap):
    with RawPath.open("rb") as Stream:
        Raw = Stream.read(64 * 1024 * 1024 + 1)
    Need(len(Raw) <= 64 * 1024 * 1024, "bounded compile-attempt input exceeded")
    Lines = Raw.decode("utf-8-sig").splitlines()
    Need(all(len(Line) <= 65536 for Line in Lines), "bounded compile-attempt line exceeded")
    Need(not any(re.search(r"\bD8_STACK_[A-Z_]+\b", Line) for Line in Lines),
         "compile-only attempt cannot contain runtime stack cases")
    Fail = [Index for Index, Line in enumerate(Lines) if "Failed compiling 'CarbonLuau.cszip':" in Line]
    Error = [Index for Index, Line in enumerate(Lines) if "[CS1503]" in Line and "BarrelResearchCase" in Line and "'string'" in Line]
    Need(len(Fail) == 1 and len(Error) == 1 and Fail[0] < Error[0] <= Fail[0] + 4 and
         Error[0] + 1 < len(Lines) and re.search(r"\(CarbonLuau\.LootStackMatrixFixtures [0-9]+ line 293\)", Lines[Error[0] + 1]),
         "compile attempt lacks exact original CS1503 matrix-file/line diagnostic")
    Runner = FailureRunnerWitness(RunnerPath, Host, PackageMap, "matrix0")
    Origins = [(Number, re.search(r'Command Line:.*?-logfile["\s]+([^"\s]+)', Line)) for Number, Line in enumerate(Lines, 1)]
    Origins = [(Number, Match) for Number, Match in Origins if Match]
    Need(len(Origins) == 1 and Origins[0][1][1].replace("\\", "/") == Runner["Log"],
         "failed raw command line names a different runner evidence path")
    return {"Host": Host, "Kind": "COMPILE_FAIL", "OverallStatus": "FAIL", "QualifiesCompatibility": False,
        "RuntimeCases": 0, "RawLocal": str(RawPath), "SHA256": hashlib.sha256(Raw).hexdigest(), **Runner,
        "HostOriginLine": Origins[0][0], "CompilerDiagnostic": {"Code": "CS1503", "File": "CarbonLuau.LootStackMatrixFixtures.cs", "SourceLine": 293,
            "LogLine": Error[0] + 1, "LocationLogLine": Error[0] + 2, "Text": Lines[Error[0]].strip()},
        "Scope": "Fixture compilation failed before the adapter loaded; no OnDied MVID, item behavior or runtime stack matrix witness is claimed."}


def ReadFixtureAttempt(RawPath, RunnerPath, Host, PackageMap):
    with RawPath.open("rb") as Stream:
        Raw = Stream.read(64 * 1024 * 1024 + 1)
    Need(len(Raw) <= 64 * 1024 * 1024, "bounded failed fixture raw input exceeded")
    Lines = Raw.decode("utf-8-sig").splitlines()
    Need(all(len(Line) <= 65536 for Line in Lines), "bounded failed fixture raw line exceeded")
    Matches = [(Number, re.search(r"\bD8_STACK_CASE part=([01]) item=([a-z0-9._-]+) phase=(\d+) case=([a-z_]+) result=PASS profiles=2 cleanup=0$", Line))
               for Number, Line in enumerate(Lines, 1)]
    Matches = [(Number, Match) for Number, Match in Matches if Match]
    Need(1 <= len(Matches) < 57 and not any("D8_STACK_BATCH" in Line for Line in Lines) and
         sum("D8_STACK_CASE" in Line for Line in Lines) == len(Matches),
         "retained fixture failure requires a bounded exact partial case prefix and no completed batch")
    Records = StackTool.Records(Lines[:Matches[-1][0]])
    Need(Records and Records[0]["Tag"] == "BEGIN", "failed fixture lacks original matrix begin")
    Part = int(Matches[0][1][1])
    StackTool.Equal(StackTool.Fields(Records[0], "mode part representatives phases world_cap amount_cap engine_hooks_added"),
        {"mode": "matrix", "part": Part, "representatives": 3, "phases": 19, "world_cap": 8, "amount_cap": 1000, "engine_hooks_added": 0})
    Group, Cases, BeginLine = [], [], Records[0]["Line"]
    for Record in Records[1:]:
        if Record["Tag"] != "CASE":
            Group.append(Record); continue
        Name = tuple(StackTool.Representatives)[Part * 3 + len(Cases) // 19]
        Phase = len(Cases) % 19
        StackTool.Equal(StackTool.Fields(Record, "part item phase case result profiles cleanup"),
            {"part": Part, "item": Name, "phase": Phase, "case": StackTool.PhaseNames[Phase], "result": "PASS", "profiles": 2, "cleanup": 0})
        Worlds = []
        for Number in range(BeginLine + 1, Record["Line"]):
            Match = StackTool.WorldRecord.search(Lines[Number - 1])
            if Match:
                Worlds.append({"Name": Match[1], "Amount": int(Match[2]), "Uid": StackTool.Positive(Match[3]),
                    "World": StackTool.Positive(Match[4]), "Class": Match[5], "Entry": int(Match[6]),
                    "Planned": int(Match[7]), "Prefab": Match[8], "Line": Number})
        StackTool.MatrixGroup(Group, Worlds, Name, Phase)
        Cases.append({"Name": Name, "Phase": Phase, "Status": "PASS", "Line": Record["Line"], "Evidence": Group, "Worlds": Worlds})
        Group, BeginLine = [], Record["Line"]
    Errors = [{"Line": Number, "Text": Line.strip()[:2048]} for Number, Line in enumerate(Lines, 1)
              if Number > Matches[-1][0] and re.search(r"\[CarbonLuau:LootFixture\] (?:FAIL|CLEANUP_FAIL)\b", Line)]
    Need(len(Cases) == len(Matches) and not Group and 1 <= len(Errors) <= 8,
         "failed fixture lacks exact completed prefix and subsequent fatal/cleanup refusal")
    Runner = FailureRunnerWitness(RunnerPath, Host, PackageMap, "matrix" + str(Part))
    Origins = [(Number, re.search(r'Command Line:.*?-logfile["\s]+([^"\s]+)', Line)) for Number, Line in enumerate(Lines, 1)]
    Origins = [(Number, Match) for Number, Match in Origins if Match]
    Need(len(Origins) == 1 and Origins[0][1][1].replace("\\", "/") == Runner["Log"],
         "failed runtime raw command line names a different runner evidence path")
    return {"Host": Host, "Kind": "FIXTURE_FAIL", "OverallStatus": "FAIL", "QualifiesCompatibility": False,
        "RuntimeCases": len(Cases), "NextUncompletedPhase": len(Cases) % 19,
        "NextUncompletedName": tuple(StackTool.Representatives)[Part * 3 + len(Cases) // 19], "Cases": Cases, "Errors": Errors,
        "HostWitness": StackTool.HostWitness(Lines, Host), "HostOriginLine": Origins[0][0],
        "RawLocal": str(RawPath), "SHA256": hashlib.sha256(Raw).hexdigest(), **Runner,
        "Scope": "Only the independently completed ordered stack-case prefix qualifies as historical case evidence. The next phase's outcome is not inferred, the full matrix remains failed and no item is classified unsafe."}


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("--root", type=Path, default=Root)
    Parser.add_argument("--manifest", type=Path)
    for Host in Hosts:
        for Phase in Phases:
            Parser.add_argument("--" + Host.lower() + "-" + Phase.lower(), type=Path)
        Parser.add_argument("--" + Host.lower() + "-public", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-public-negative", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack-negative", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack-runner", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack-compile-negative", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack-compile-runner", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack-fixture-negative", type=Path, action="append", default=[])
        Parser.add_argument("--" + Host.lower() + "-stack-fixture-runner", type=Path, action="append", default=[])
    Parser.add_argument("--log-map", type=Path, help="JSON object mapping recorded remote Log strings to local raw log paths")
    Parser.add_argument("--package-hashes", type=Path, help="optional JSON object mapping raw log SHA256 to its separately recorded package SHA256")
    Parser.add_argument("--require-public", action="store_true", help="require exact complete original public batches on both hosts; stack gates remain separate")
    Parser.add_argument("--require-stack", action="store_true", help="require both-host stack matrices, terminal restoration, fixed gap and final-source sweep for FINAL support")
    Parser.add_argument("--stack-package-map", type=Path, help="JSON map from actual stack runner package SHA256 to original local cszip path")
    Mode = Parser.add_mutually_exclusive_group(required=True)
    Mode.add_argument("--output-directory", type=Path, help="write generated relative paths beneath an isolated directory")
    Mode.add_argument("--patch", action="store_true", help="print an apply_patch payload without changing shared files")
    Mode.add_argument("--check", action="store_true", help="check complete ledger projections from raw evidence")
    Mode.add_argument("--check-public", action="store_true", help="check the public support table against the manifest without external evidence dependencies")
    Args = Parser.parse_args()
    Need(not Args.require_stack or Args.require_public, "final stack qualification also requires all original public item gates")
    Manifest = ManifestTool.ReadManifest(Args.manifest or Args.root / "api/loot-items.pinned.json")
    if Args.check_public:
        CheckPublicProjection(Args.root, Manifest)
        print("[CarbonLuau:LootQualification] PASS finite public support table projection")
        return
    ProbePaths = {(Host, Phase): getattr(Args, Host.lower() + "_" + Phase.lower()) for Host in Hosts for Phase in Phases}
    Need(all(Path is not None for Path in ProbePaths.values()), "all six explicit probe receipt paths are required")
    LogMap = ReadJson(Args.log_map, 65536)[0] if Args.log_map else {}
    Need(type(LogMap) is dict and len(LogMap) <= 456 and all(type(Key) is str and type(Value) is str for Key, Value in LogMap.items()),
         "invalid bounded local raw log map")
    PublicPaths = {Host: getattr(Args, Host.lower() + "_public") for Host in Hosts}
    NegativePaths = {Host: getattr(Args, Host.lower() + "_public_negative") for Host in Hosts}
    Need(all(len(Paths) <= 221 for Paths in PublicPaths.values()) and all(len(Paths) <= 4 for Paths in NegativePaths.values()),
         "public receipt path count exceeds bound")
    PackageHashes = ReadJson(Args.package_hashes, 65536)[0] if Args.package_hashes else {}
    Need(type(PackageHashes) is dict and len(PackageHashes) <= 456 and all(type(Key) is str and type(Value) is str and
         re.fullmatch(r"[0-9a-f]{64}", Key) and re.fullmatch(r"[0-9a-f]{64}", Value) for Key, Value in PackageHashes.items()),
         "invalid bounded package hash provenance map")
    Ledger = BuildLedger(Manifest, ProbePaths, PublicPaths, LogMap, Args.require_public, NegativePaths, PackageHashes)
    StackPackageMap = ReadJson(Args.stack_package_map, 65536)[0] if Args.stack_package_map else {}
    Need(type(StackPackageMap) is dict and len(StackPackageMap) <= 20 and all(type(Key) is str and
         re.fullmatch(r"[0-9a-f]{64}", Key) and type(Value) is str for Key, Value in StackPackageMap.items()),
         "invalid bounded stack packet map")
    Ledger = ApplyStackQualification(Ledger, Manifest,
        {Host: getattr(Args, Host.lower() + "_stack") for Host in Hosts},
        {Host: getattr(Args, Host.lower() + "_stack_negative") for Host in Hosts},
        {Host: getattr(Args, Host.lower() + "_stack_runner") for Host in Hosts}, LogMap, StackPackageMap, Args.require_stack)
    Ledger["InfrastructureAttempts"] = []
    for Host in Hosts:
        for Kind, Reader in (("compile", ReadCompileAttempt), ("fixture", ReadFixtureAttempt)):
            RawPaths = getattr(Args, Host.lower() + "_stack_" + Kind + "_negative")
            RunnerPaths = getattr(Args, Host.lower() + "_stack_" + Kind + "_runner")
            Need(len(RawPaths) == len(RunnerPaths) and len(RawPaths) <= 4, "bounded paired failure raw/runner paths required")
            for RawPath, RunnerPath in zip(RawPaths, RunnerPaths):
                Attempt = Reader(RawPath, RunnerPath, Host, StackPackageMap)
                Need(not any(Source["SHA256"] == Attempt["SHA256"] for Source in Ledger["InfrastructureAttempts"]),
                     "duplicate infrastructure attempt")
                Ledger["InfrastructureAttempts"].append(Attempt)
    Projection = Outputs(Ledger, Manifest, Args.require_public)
    if Args.patch:
        Patch = ["*** Begin Patch\n"]
        for Relative, Expected in Projection.items():
            Target = Args.root / Relative
            if Target.exists():
                Actual = Target.read_text(encoding="utf-8")
                if Actual == Expected:
                    continue
                Patch.append("*** Update File: " + Target.resolve().as_posix() + "\n")
                Diff = list(difflib.unified_diff(Actual.splitlines(keepends=True), Expected.splitlines(keepends=True)))
                Patch.extend("@@\n" if Line.startswith("@@") else Line for Line in Diff[2:])
            else:
                Patch.append("*** Add File: " + Target.resolve().as_posix() + "\n")
                Patch.extend("+" + Line for Line in Expected.splitlines(keepends=True))
        sys.stdout.write("".join(Patch) + "*** End Patch\n")
        return
    for Relative, Expected in Projection.items():
        if Args.check:
            Need((Args.root / Relative).read_text(encoding="utf-8") == Expected, "generated qualification ledger drift: " + Relative)
        else:
            Target = Args.output_directory / Relative
            Target.parent.mkdir(parents=True, exist_ok=True)
            Target.write_text(Expected, encoding="utf-8", newline="\n")
    print("[CarbonLuau:LootQualification] " + Ledger["Status"] + " " + json.dumps(Ledger["Counts"], sort_keys=True))


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, OSError, UnicodeError, KeyError, TypeError) as Error:
        print("[CarbonLuau:LootQualification] FAIL " + str(Error), file=sys.stderr)
        sys.exit(1)
