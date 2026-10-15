"""Isolated D8 ledger fixtures: full census, negative history, physical authority and both-host public gates."""
import copy
import ast
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

sys.dont_write_bytecode = True
Root = Path(__file__).resolve().parent.parent
Spec = importlib.util.spec_from_file_location("LootQualification", Root / "tools/Generate-LootQualification.py")
Tool = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Tool)
Manifest = Tool.ManifestTool.ReadManifest(Root / "api/loot-items.pinned.json")
Manifest = copy.deepcopy(Manifest)
Definitions = [{"Name": Name, "Id": Id, "Stack": 1000, "Family": "Baseline"} for Name, Id in Tool.Baseline.items()]
Definitions += [{"Name": "x" + str(Index).zfill(4), "Id": 10000 + Index, "Stack": 10, "Family": "RawResource"} for Index in range(217)]
Definitions.sort(key=lambda Row: Row["Name"])
Manifest["Definitions"] = Definitions
Census = copy.deepcopy(Definitions) + [{"Name": "water", "Id": -1779180711, "Stack": 2147483647, "Family": "RawResource"}]
Census += [{"Name": "y" + str(Index).zfill(4), "Id": 20000 + Index, "Stack": 1, "Family": "Equivalent"} for Index in range(1085)]
Census.sort(key=lambda Row: Row["Name"])
assert len(Census) == 1307


def Reject(Action, Label):
    try:
        Action()
    except (ValueError, OSError):
        return
    raise AssertionError("accepted " + Label)


with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootQualification-") as Directory:
    Fixture = Path(Directory)
    ProbePaths, PublicPaths = {}, {Host: [] for Host in Tool.Hosts}
    RawTexts = {}

    def SaveReceipt(Path, Text, Incomplete=False):
        Log = Path.with_suffix(".log")
        Log.write_text(Text, encoding="utf-8")
        Value = Tool.Batch.ReadReceipt(Log, AllowIncomplete=Incomplete)
        Path.write_text(json.dumps(Value, sort_keys=True), encoding="utf-8")
        return Value

    def ProbeText(Host, Phase):
        Lines, Candidates = [], []
        for Index, Row in enumerate(Census):
            Name = Row["Name"]
            if Name in Tool.Baseline:
                Status, Reason = "SKIP", "previously_qualified_D2A_D7"
            elif Name == "water":
                Status, Reason = ("CANDIDATE", "simple_lifecycle_equivalent") if Phase == "Refined" else ("SKIP", "definition_NoDropping_flag")
            elif Name.startswith("x"):
                Candidate = Phase == "Initial" or Phase == "Refined" and Name == "x0000"
                Status, Reason = ("CANDIDATE", "simple_lifecycle_equivalent") if Candidate else ("SKIP", "previously_proved_physical_both_hosts")
            else:
                Status, Reason = "SKIP", "special_lifecycle:OnChanged"
            Lines.append("D8_DISCOVERY index=" + str(Index) + " name=" + Name + " item_id=" + str(Row["Id"]) +
                " stackable=" + str(Row["Stack"]) + " category=Resources family=RawResources condition_enabled=False roots=ItemDefinition mods= result=" + Status + " reason=" + Reason)
            if Status == "CANDIDATE":
                Candidates.append(Row)
        Passed, Failed = 0, 0
        for Row in Candidates:
            Name = Row["Name"]
            if Name == "water":
                Lines.append("D8_ITEM name=water item_id=-1779180711 result=FAIL reason=exact_cleanup_blocker first=unknown_returned_world")
                Failed += 1
                continue
            Status = "FAIL" if Name == "x0000" and Phase == "Initial" else "PASS"
            if Status == "PASS":
                Passed += 1
                for Amount in (1, 9, 10):
                    Lines.append("D8_PHYSICAL name=" + Name + " item_id=" + str(Row["Id"]) + " stackable=10 amount=" + str(Amount) +
                        " uid=" + str(Row["Id"] * 100 + Amount) + " world_id=" + str(Row["Id"] * 100 + Amount + 50) +
                        " world_prefab=assets/prefabs/misc/burlap sack/generic_world.prefab epoch=1 reciprocal=true completed_spawn=true")
                    Lines.append("D8_PHYSICAL_CLEAN name=" + Name + " amount=" + str(Amount) + " ledger=0 original_removed=true")
            else:
                Failed += 1
            Lines.append("D8_ITEM name=" + Name + " item_id=" + str(Row["Id"]) + " stackable=10 family=RawResources result=" + Status + " reason=fixture")
        if Phase != "Refined":
            Lines.append("PASS D8 bulk probe batch definitions=1307 candidates=" + str(len(Candidates)) + " pass=" + str(Passed) +
                " fail=" + str(Failed) + " skip=" + str(1307 - len(Candidates)))
        return "\n".join(Lines) + "\n"

    for Host in Tool.Hosts:
        for Phase in Tool.Phases:
            Path = Fixture / (Host + "-" + Phase + ".json")
            Text = ProbeText(Host, Phase)
            SaveReceipt(Path, Text, Phase == "Refined")
            ProbePaths[Host, Phase] = Path
            RawTexts[Host, Phase] = Text
    Pending = Tool.BuildLedger(Manifest, ProbePaths, PublicPaths)
    assert Pending["Status"] == "PENDING_PUBLIC" and Pending["Counts"]["PENDING_PUBLIC"] == 221
    Rows = {Row["Name"]: Row for Row in Pending["Definitions"]}
    assert [Attempt["Status"] for Attempt in Rows["x0000"]["PhysicalHistory"]] == ["FAIL", "PASS", "FAIL", "PASS"]
    assert Rows["water"]["Classification"] == "DROP_DISABLED" and Rows["water"]["Verdict"] == "SKIP" and all(Attempt["Status"] == "FAIL" for Attempt in Rows["water"]["PhysicalHistory"])
    assert Rows["x0001"]["PhysicalBothHosts"] and len(Rows["x0001"]["PhysicalHistory"][0]["Quantities"]) == 3
    assert Rows["stones"]["PhysicalGate"] == "PRIOR_D7_BASELINE" and not Rows["stones"]["PhysicalBothHosts"]
    assert all("CleanupLine" in Proof and Proof["OriginalRemoved"] for Row in Rows.values() for Attempt in Row["PhysicalHistory"] for Proof in Attempt["Quantities"])
    assert not any(Path.startswith("docs/api/") for Path in Tool.Outputs(Pending, Manifest, False))
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, RequirePublic=True), "missing actual public gates")

    # Historical normalized sources may omit Complete only when the raw log proves it.
    Initial = ProbePaths["Windows", "Initial"]
    OldBytes = Initial.read_bytes()
    Old = json.loads(OldBytes)
    del Old["Completion"]["Complete"]
    Initial.write_text(json.dumps(Old), encoding="utf-8")
    assert Tool.BuildLedger(Manifest, ProbePaths, PublicPaths)["Status"] == "PENDING_PUBLIC"
    Initial.write_bytes(OldBytes)

    # Do not trust normalized-only identity, completion or cleanup claims.
    Tampered = json.loads(OldBytes)
    Tampered["Physical"][0]["Uid"] = "999"
    Initial.write_text(json.dumps(Tampered), encoding="utf-8")
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths), "normalized UID tampering")
    Initial.write_bytes(OldBytes)
    Log = Initial.with_suffix(".log")
    OriginalLog = Log.read_bytes()
    Log.write_bytes(OriginalLog + b"changed\n")
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths), "raw SHA drift")
    Log.write_bytes(OriginalLog)
    MissingCleanup = RawTexts["Windows", "Initial"].replace("D8_PHYSICAL_CLEAN name=x0001 amount=9 ledger=0 original_removed=true\n", "")
    Log.write_text(MissingCleanup, encoding="utf-8")
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths), "missing keyed original cleanup")
    Log.write_bytes(OriginalLog)
    Changed = RawTexts["Windows", "Resumed"].replace("name=y0000 item_id=20000", "name=y0000 item_id=30000")
    SaveReceipt(ProbePaths["Windows", "Resumed"], Changed)
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths), "cross-phase census identity drift")
    SaveReceipt(ProbePaths["Windows", "Resumed"], RawTexts["Windows", "Resumed"])
    ChangedManifest = copy.deepcopy(Manifest)
    ChangedManifest["Definitions"][0]["Id"] += 1
    Reject(lambda: Tool.BuildLedger(ChangedManifest, ProbePaths, PublicPaths), "enabled identity drift")
    NoPhysical = copy.deepcopy(Manifest)
    NoPhysical["Definitions"][-1] = {"Name": "y0000", "Id": 20000, "Stack": 1, "Family": "RawResource"}
    Reject(lambda: Tool.BuildLedger(NoPhysical, ProbePaths, PublicPaths), "enabled unproved item")

    # Remote recorded paths remain provenance; a bounded explicit local map supplies the raw bytes.
    Remote = copy.deepcopy(json.loads(OldBytes))
    LocalLog = Remote["Log"]
    Remote["Log"] = "/external/pinned/server.log"
    Initial.write_text(json.dumps(Remote), encoding="utf-8")
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths), "missing raw log mapping")
    Mapped = Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, {Remote["Log"]: LocalLog})
    assert any(Source["Log"] == Remote["Log"] for Source in Mapped["Sources"])
    Initial.write_bytes(OldBytes)

    def PublicText(Offset, Count=32):
        Rows = Definitions[Offset:Offset + Count]
        Text = ""
        for Row in Rows:
            for Prefab in Tool.Prefabs:
                Text += "D8_PUBLIC_AMOUNTS name=" + Row["Name"] + " prefab=" + Prefab + " zero=0 minimum=1 maximum=" + str(min(Row["Stack"], 1000)) + " repeated=8 original_worlds_verified=true\n"
                Text += "D8_PUBLIC_SUPPRESSION name=" + Row["Name"] + " prefab=" + Prefab + " stored=true bonus=true controlled_bag=true natural_bag_branch=false\n"
                Text += "D8_STACK_CONTROL name=" + Row["Name"] + " prefab=" + Prefab + " original_stack=" + str(Row["Stack"]) + \
                    " actual_stack=" + str(999 if Row["Stack"] >= 1000 else Row["Stack"]) + " declared_max=" + str(1000 if Row["Stack"] >= 1000 else Row["Stack"] + 1) + \
                    " registration_succeeded=true expected_precommit_vanilla=true controlled_premise=" + str(Row["Stack"] >= 1000) + "\n"
            Text += "D8_PUBLIC_ITEM name=" + Row["Name"] + " item_id=" + str(Row["Id"]) + " stackable=" + str(Row["Stack"]) + " family=" + Row["Family"] + " result=PASS " + Tool.PublicReason + "\n"
        return Text + "PASS D8 bulk public batch offset=" + str(Offset) + " count=" + str(len(Rows)) + " pass=" + str(len(Rows)) + \
            " fail=0 common_publication_failure_recovery_budget_matrix=true per_item_receipts_authoritative=true ledger=0\n" + "\n".join(Tool.PublicMarkers) + "\n"

    for Host in Tool.Hosts:
        for Offset in range(0, len(Definitions), 32):
            Path = Fixture / (Host + "-Public-" + str(Offset) + ".json")
            SaveReceipt(Path, PublicText(Offset))
            PublicPaths[Host].append(Path)
    Final = Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, RequirePublic=True)
    assert Final["Status"] == "VANILLA_PUBLIC_COMPLETE" and Final["Counts"]["PASS"] == 221 and len(Final["Sources"]) == 20
    assert Final == Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, RequirePublic=True)
    assert len(Tool.Outputs(Final, Manifest, True)) == 2, "unqualified stack compatibility must not emit the public support page"
    Duplicate = copy.deepcopy(PublicPaths)
    Duplicate["Windows"].append(Duplicate["Windows"][0])
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, Duplicate, RequirePublic=True), "overlapping public batch")
    Gap = copy.deepcopy(PublicPaths)
    Gap["Linux"].pop(1)
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, Gap, RequirePublic=True), "public batch gap")
    assert Tool.BuildLedger(Manifest, ProbePaths, Gap)["Status"] == "PENDING_PUBLIC"
    Smaller = copy.deepcopy(PublicPaths)
    Smaller["Windows"].pop(0)
    for Offset in (0, 16):
        Path = Fixture / ("Windows-Smaller-" + str(Offset) + ".json")
        SaveReceipt(Path, PublicText(Offset, 16))
        Smaller["Windows"].append(Path)
    assert Tool.BuildLedger(Manifest, ProbePaths, Smaller, RequirePublic=True)["Status"] == "VANILLA_PUBLIC_COMPLETE"
    # A smaller declared manifest cannot silently omit an otherwise qualified candidate from D8 FINAL.
    Subset = copy.deepcopy(Manifest)
    Subset["Definitions"].pop()
    SubsetPublic = copy.deepcopy(PublicPaths)
    for Host in Tool.Hosts:
        Path = Fixture / (Host + "-Subset-192.json")
        SaveReceipt(Path, PublicText(192, 28))
        SubsetPublic[Host][-1] = Path
    SubsetDraft = Tool.BuildLedger(Subset, ProbePaths, SubsetPublic)
    assert SubsetDraft["Status"] == "PENDING_PUBLIC" and not SubsetDraft["CandidateSetComplete"]
    Reject(lambda: Tool.BuildLedger(Subset, ProbePaths, SubsetPublic, RequirePublic=True), "proper subset of all both-host physically proved candidates")
    FirstPublic = PublicPaths["Windows"][0]
    OriginalPublic = PublicText(0)
    for Bad, Label in ((OriginalPublic.replace("item_id=69511070", "item_id=69511071"), "public identity drift"),
                       (OriginalPublic.replace(Tool.PublicReason, "reason=partial", 1), "partial per-item matrix"),
                       (OriginalPublic.replace("original_worlds_verified=true", "original_worlds_verified=false", 1), "missing actual amount world proof"),
                       (OriginalPublic.replace("controlled_bag=true", "controlled_bag=false", 1), "missing controlled bag proof"),
                       (OriginalPublic.replace("expected_precommit_vanilla=true", "expected_precommit_vanilla=false", 1), "missing precommit stack proof"),
                       (OriginalPublic.replace(Tool.PublicMarkers[-1], ""), "missing B2 terminal convergence"),
                       (OriginalPublic.replace("common_publication_failure_recovery_budget_matrix=true", "common_publication_failure_recovery_budget_matrix=false"), "missing common controls"),
                       (OriginalPublic.replace("result=PASS", "result=FAIL", 1).replace("pass=32 fail=0", "pass=31 fail=1"), "completed public batch with item FAIL")):
        SaveReceipt(FirstPublic, Bad)
        Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, RequirePublic=True), Label)
    SaveReceipt(FirstPublic, OriginalPublic)
    NegativePaths = {}
    for Host in Tool.Hosts:
        Path = Fixture / (Host + "-Negative.json")
        SaveReceipt(Path, OriginalPublic.replace("result=PASS", "result=FAIL", 1).replace("pass=32 fail=0", "pass=31 fail=1"))
        NegativePaths[Host] = [Path]
    WithNegatives = Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, RequirePublic=True, NegativePaths=NegativePaths)
    NegativeItem = next(Row for Row in WithNegatives["Definitions"] if Row["Name"] == Definitions[0]["Name"])
    assert NegativeItem["Verdict"] == "PASS" and len(WithNegatives["Sources"]) == 22
    assert all([Attempt["Status"] for Attempt in NegativeItem["PublicAttempts"][Host]] == ["FAIL", "PASS"] for Host in Tool.Hosts)
    assert all(not NegativeItem["PublicAttempts"][Host][0]["QualifiesCurrentSupport"] for Host in Tool.Hosts)
    Reject(lambda: Tool.BuildLedger(Manifest, ProbePaths, PublicPaths, RequirePublic=True,
                                   NegativePaths={"Windows": PublicPaths["Windows"][:1]}), "positive source relabeled historical negative")

    # Exercise aggregation/restoration/packet provenance independently of the stack
    # parser's own real IL/event fixtures in Test-LootStack.py.
    StackPaths, StackRunners, StackPackages = {Host: [] for Host in Tool.Hosts}, {Host: [] for Host in Tool.Hosts}, {}
    StackRaw = {}
    for Kind in Tool.StackKinds:
        Packet = Fixture / (Kind + ".cszip")
        with zipfile.ZipFile(Packet, "w") as Archive:
            for Name in ("CarbonLuau.LootFixtures.cs", "CarbonLuau.LootStackFixtures.cs"):
                Archive.writestr(Name, "// isolated selector fixture")
            Archive.writestr("CarbonLuau.Loot.Carbon.cs", (Root / "src/CarbonLuau/CarbonLuau.Loot.Carbon.cs").read_bytes())
            if Kind != "gap-fixed":
                Part = {"matrix0": 0, "matrix1": 1, "terminal-uid": 2, "sweep": 3}[Kind]
                Archive.writestr("CarbonLuau.LootStackModeFixtures.cs", "namespace Carbon.Plugins { public partial class CarbonLuau { partial void SelectLootStackMode(ref bool GapOnly, ref int Part) { GapOnly = false; Part = " + str(Part) + "; } } }")
                Archive.writestr("CarbonLuau.LootFinalItemFixtures.cs" if Kind == "sweep" else "CarbonLuau.LootStackMatrixFixtures.cs",
                    "// isolated component" if Kind == "sweep" else (Root / "tests/live/CarbonLuau.LootStackMatrixFixtures.cs").read_bytes())
        PackageHash = hashlib.sha256(Packet.read_bytes()).hexdigest()
        StackPackages[PackageHash] = str(Packet)
        for Host in Tool.Hosts:
            RawPath = Fixture / (Host + "-" + Kind + ".log")
            Cases = [{"Name": Row["Name"], "Id": Row["Id"], "Stack": Row["Stack"], "Prefab": Prefab}
                     for Row in Manifest["Definitions"] for Prefab in Tool.Prefabs] if Kind == "sweep" else []
            Payload = {"SchemaVersion": 1, "Host": Host, "Kind": Kind, "Complete": True, "Cases": Cases,
                       "QualifiesCompatibility": Kind != "terminal-uid"}
            RawPath.write_text(json.dumps(Payload), encoding="utf-8")
            RawHash = hashlib.sha256(RawPath.read_bytes()).hexdigest()
            Payload.update({"Log": str(RawPath), "SHA256": RawHash})
            Receipt = RawPath.with_suffix(".json")
            Receipt.write_text(json.dumps(Payload), encoding="utf-8")
            Runner = RawPath.with_suffix(".runner")
            Lines = []
            if Kind in ("matrix0", "matrix1", "terminal-uid"):
                for Name in ("CarbonLuauStackProbeA.cs", "CarbonLuauStackProbeB.cs"):
                    Lines.append("[CarbonLuau:BarrelLootResearchRunner] INPUT " + Name + " SHA256=" +
                                 hashlib.sha256((Root / "tests/live" / Name).read_bytes()).hexdigest())
            if Kind == "terminal-uid":
                Lines.append("[CarbonLuau:BarrelLootResearchRunner] PROFILE_SNAPSHOT files=20 file_cap=128 byte_cap=67108864 manifest_sha256=" + "f" * 64)
            Lines += ["[CarbonLuau:BarrelLootResearchRunner] EVIDENCE " + str(RawPath) + " SHA256=" + RawHash +
                      " PACKAGE=" + PackageHash + " NATIVE=" + "1" * 64 + " COMPILER=" + "2" * 64,
                      "[CarbonLuau:BarrelLootResearchRunner] PROCESS_EXIT 0 qualified Shutdown -> Process.Kill"]
            if Kind == "terminal-uid":
                Lines.append("[CarbonLuau:BarrelLootResearchRunner] FullProfileRestored files=20 bytes=4096 file_cap=128 byte_cap=67108864 metadata=true scope=db_companions_saves_navmesh manifest_sha256=" + "f" * 64)
            Lines.append("[CarbonLuau:BarrelLootResearchRunner] CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored")
            Runner.write_text("\n".join(Lines) + "\n", encoding="utf-8")
            StackPaths[Host].append(Receipt); StackRunners[Host].append(Runner)
            StackRaw[Host, Kind] = RawPath
    StackNegative = {Host: [] for Host in Tool.Hosts}
    for Host in Tool.Hosts:
        RawPath = Fixture / (Host + "-stack-retained-gap.log")
        Payload = {"SchemaVersion": 1, "Host": Host, "Kind": "gap-negative", "Complete": True,
                   "Cases": [], "QualifiesCompatibility": False}
        RawPath.write_text(json.dumps(Payload), encoding="utf-8")
        Payload.update({"Log": str(RawPath), "SHA256": hashlib.sha256(RawPath.read_bytes()).hexdigest()})
        Receipt = RawPath.with_suffix(".json"); Receipt.write_text(json.dumps(Payload), encoding="utf-8")
        StackNegative[Host].append(Receipt)
    ActualStackReader = Tool.StackTool.ReadReceipt
    def ReadStackUnit(Path, Host, Kind, ExpectedManifest):
        assert ExpectedManifest == Manifest
        Value = json.loads(Path.read_text(encoding="utf-8"))
        assert Value["Host"] == Host and Value["Kind"] == Kind
        Value.update({"Log": str(Path), "SHA256": hashlib.sha256(Path.read_bytes()).hexdigest()})
        return Value
    Tool.StackTool.ReadReceipt = ReadStackUnit
    try:
        Closed = Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, StackPaths, StackNegative, StackRunners, {}, StackPackages, True)
        assert Closed["Status"] == "FINAL" and Closed["StackCompatibilityGate"] == "PASS" and len(Tool.Outputs(Closed, Manifest, True)) == 3
        assert all(len(Row["FinalSourcePublic"]["Windows"]["Cases"]) == 2 for Row in Closed["Definitions"] if Row["EnabledManifest"])
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, StackPaths, {}, StackRunners, {}, StackPackages, True), "final must retain both-host original gap counterexamples")
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, {}, {}, {}, {}, {}, True), "final stack gate cannot use vanilla-only proof")
        Missing = copy.deepcopy(StackPaths); Missing["Windows"] = Missing["Windows"][:-1]
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, Missing, {}, StackRunners, {}, StackPackages, True), "one host missing final sweep")
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, StackPaths, {}, {}, {}, StackPackages, True), "unchecked restored flag cannot replace actual runner transcript")
        Duplicate = copy.deepcopy(StackPaths); Duplicate["Windows"][-1] = Duplicate["Windows"][0]
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, Duplicate, {}, StackRunners, {}, StackPackages, True), "duplicate stack component cannot fill sweep slot")
        Runner = StackRunners["Windows"][0]; OriginalRunner = Runner.read_text()
        Runner.write_text(OriginalRunner.replace("CLEANUP owned process exited", "CLEANUP_FAILED owned process exited"))
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, StackPaths, {}, StackRunners, {}, StackPackages, True), "process restoration failure")
        Runner.write_text(OriginalRunner)
        TerminalRunner = StackRunners["Windows"][3]; OriginalTerminal = TerminalRunner.read_text()
        for Broken in (OriginalTerminal.replace("FullProfileRestored", "MissingFullProfileProof"),
                       OriginalTerminal.replace("files=20 bytes=4096", "files=19 bytes=4096"),
                       OriginalTerminal.replace("bytes=4096", "bytes=67108865"),
                       OriginalTerminal.replace("metadata=true", "metadata=false")):
            TerminalRunner.write_text(Broken)
            Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, StackPaths, StackNegative,
                StackRunners, {}, StackPackages, True), "terminal requires exact bounded profile byte/file/metadata restoration")
        TerminalRunner.write_text(OriginalTerminal)
        Receipt = StackPaths["Windows"][0]; OriginalReceipt = Receipt.read_text()
        Changed = json.loads(OriginalReceipt); Changed["Complete"] = False
        Receipt.write_text(json.dumps(Changed))
        Reject(lambda: Tool.ApplyStackQualification(copy.deepcopy(Final), Manifest, StackPaths, {}, StackRunners, {}, StackPackages, True), "normalized stack tampering")
        Receipt.write_text(OriginalReceipt)
        for Kind in Tool.StackKinds:
            PacketHash = next(Hash for Hash, File in StackPackages.items() if str(File).endswith(Kind + ".cszip"))
            WrongKind = "matrix0" if Kind == "gap-fixed" else "gap-fixed"
            Reject(lambda: Tool.StackPackageWitness(Fixture / (Kind + ".cszip"), PacketHash, WrongKind), "packet/component selector mismatch")
        with zipfile.ZipFile(Fixture / "matrix0.cszip") as Archive:
            Entries = {Name: Archive.read(Name) for Name in Archive.namelist()}
        MatrixName = "CarbonLuau.LootStackMatrixFixtures.cs"
        FixedCounter = b"AfterB[2] == BeforeB[2] + (LootStackDropMode == 3 ? 1 : 0)"
        assert FixedCounter in Entries[MatrixName]
        for Name, Replacement in ((MatrixName, Entries[MatrixName].replace(FixedCounter, b"AfterB[2] == BeforeB[2]", 1)),
            ("CarbonLuau.LootStackModeFixtures.cs", Entries["CarbonLuau.LootStackModeFixtures.cs"] + b"\n// extra selector source")):
            Changed = Fixture / "stale-matrix.cszip"
            with zipfile.ZipFile(Changed, "w") as Archive:
                for EntryName, Body in Entries.items():
                    Archive.writestr(EntryName, Replacement if EntryName == Name else Body)
            ChangedHash = hashlib.sha256(Changed.read_bytes()).hexdigest()
            Tool.StackPackageWitness(Changed, ChangedHash, "matrix0")  # Historical packets retain their original fixture.
            Reject(lambda: Tool.StackPackageWitness(Changed, ChangedHash, "matrix0", CurrentFixture=True),
                "final packet must contain the exact corrected callback assertion and selector source")
    finally:
        Tool.StackTool.ReadReceipt = ActualStackReader

    # Compile negatives are global infrastructure history, never invented item cases.
    CompileRaw = Fixture / "compile-negative.log"
    CompileText = "Failed compiling 'CarbonLuau.cszip':\n 1. Argument 1: cannot convert from 'Carbon.Plugins.CarbonLuau.BarrelResearchCase' to 'string' [CS1503]\n (CarbonLuau.LootStackMatrixFixtures 43 line 293)\n"
    CompileRaw.write_text(CompileText)
    for Host in Tool.Hosts:
        Runner = Fixture / (Host + "-compile-negative.runner")
        Packet = Fixture / "matrix0.cszip"
        PacketHash = hashlib.sha256(Packet.read_bytes()).hexdigest()
        Inputs = [("package.cszip", PacketHash), ("native.dll" if Host == "Windows" else "native.so", "1" * 64),
                  ("compiler.exe" if Host == "Windows" else "compiler", "2" * 64),
                  ("CarbonLuauStackProbeA.cs", "3" * 64), ("CarbonLuauStackProbeB.cs", "4" * 64)]
        Lines = ["[CarbonLuau:BarrelLootResearchRunner] INPUT " + Name + " SHA256=" + Digest for Name, Digest in Inputs]
        HostLog = "D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-033300-633/server.log" if Host == "Windows" else \
            "/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-033043-2019995/server.log"
        HostCompileText = 'Command Line: "RustDedicated" "-logfile" "' + HostLog + '"\n' + CompileText
        CompileRaw.write_text(HostCompileText)
        Lines += ["[CarbonLuau:BarrelLootResearchRunner] CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored",
                  "[CarbonLuau:BarrelLootResearchRunner] Compile/fixture failure: " + HostLog]
        RunnerText = "\n".join(Lines) + "\n"; Runner.write_text(RunnerText)
        Attempt = Tool.ReadCompileAttempt(CompileRaw, Runner, Host, StackPackages)
        assert Attempt["OverallStatus"] == "FAIL" and Attempt["RuntimeCases"] == 0 and not Attempt["QualifiesCompatibility"]
        assert Attempt["Restoration"]["SourceHashesRestored"] and not Attempt["Restoration"]["NormalShutdownClaim"]
        for Bad in (HostCompileText.replace("CS1503", "CS0001"), HostCompileText.replace("line 293", "line 294"),
                    HostCompileText + "D8_STACK_BEGIN mode=matrix\n", HostCompileText.replace("server.log", "wrong.log")):
            CompileRaw.write_text(Bad)
            Reject(lambda: Tool.ReadCompileAttempt(CompileRaw, Runner, Host, StackPackages), "compile-negative literal scope cannot hide runtime evidence")
        CompileRaw.write_text(HostCompileText)
        Runner.write_text(RunnerText.replace("CLEANUP owned", "CLEANUP_FAILED owned"))
        Reject(lambda: Tool.ReadCompileAttempt(CompileRaw, Runner, Host, StackPackages), "compile-negative source restoration not proved")
        Runner.write_text(RunnerText)
        Factories = ast.parse((Root / "tools/Test-LootStackParts.py").read_text())
        Functions = [Node for Node in Factories.body if isinstance(Node, ast.FunctionDef) and Node.name in ("Header", "Row", "Matrix")]
        Environment = {"Reader": Tool.StackTool}
        exec(compile(ast.Module(body=Functions, type_ignores=[]), "Test-LootStackParts.py", "exec"), Environment)
        MatrixLines = Environment["Matrix"](Host, 0).splitlines(keepends=True)
        Last = next(Index for Index, Line in enumerate(MatrixLines) if "phase=8 case=hook_invalid_type result=PASS" in Line)
        FailedText = 'Command Line: "RustDedicated" "-logfile" "' + HostLog + '"\n' + "".join(MatrixLines[:Last + 1]) + \
            "[CarbonLuau:LootFixture] FAIL owned cleanup incomplete; no success claim\n[CarbonLuau:LootFixture] CLEANUP_FAIL unknown ownership\n"
        CompileRaw.write_text(FailedText)
        Attempt = Tool.ReadFixtureAttempt(CompileRaw, Runner, Host, StackPackages)
        assert Attempt["RuntimeCases"] == 9 and Attempt["NextUncompletedPhase"] == 9 and not Attempt["QualifiesCompatibility"]
        for Bad in (FailedText.replace("max_calls=0", "max_calls=1", 1), FailedText.replace("uid=10000", "uid=0", 1),
                    FailedText.replace("[CarbonLuau:LootFixture] FAIL", "missing_fatal").replace("[CarbonLuau:LootFixture] CLEANUP_FAIL", "missing_cleanup")):
            CompileRaw.write_text(Bad)
            Reject(lambda: Tool.ReadFixtureAttempt(CompileRaw, Runner, Host, StackPackages), "failed whole fixture must retain only independently valid completed prefix")
        Last = next(Index for Index, Line in enumerate(MatrixLines) if "phase=16 case=drop_amount_interference result=PASS" in Line)
        FailedText = 'Command Line: "RustDedicated" "-logfile" "' + HostLog + '"\n' + "".join(MatrixLines[:Last + 1]) + \
            "[CarbonLuau:LootFixture] FAIL drop callback counters\n"
        CompileRaw.write_text(FailedText)
        Attempt = Tool.ReadFixtureAttempt(CompileRaw, Runner, Host, StackPackages)
        assert Attempt["RuntimeCases"] == 17 and Attempt["NextUncompletedPhase"] == 17 and not Attempt["QualifiesCompatibility"]
        for Bad in (FailedText + "D8_STACK_CASE invalid\n", FailedText.replace("phase=16 case=", "phase=17 case=", 1)):
            CompileRaw.write_text(Bad)
            Reject(lambda: Tool.ReadFixtureAttempt(CompileRaw, Runner, Host, StackPackages), "failed prefix cannot hide malformed or skipped cases")

    # Public table projection remains checkable in CI without access to private host evidence.
    Input = Fixture / "api/loot-items.pinned.json"
    Input.parent.mkdir()
    Input.write_text(json.dumps(Manifest), encoding="utf-8")
    Page = Fixture / "docs/api/LootItems.md"
    Page.parent.mkdir(parents=True)
    Page.write_text(Tool.PublicMarkdown(Manifest), encoding="utf-8")
    PublicLedger = copy.deepcopy(Final)
    PublicLedger["Status"] = "FINAL"
    PublicLedger["StackCompatibilityGate"] = "PASS"
    Machine = Fixture / "docs/qualification/GameplayD8-Items.json"
    Machine.parent.mkdir(parents=True)
    Readable = Machine.with_suffix(".md")
    Machine.write_text(json.dumps(PublicLedger), encoding="utf-8")
    Readable.write_text(Tool.LedgerMarkdown(PublicLedger), encoding="utf-8")
    Command = [sys.executable, str(Root / "tools/Generate-LootQualification.py"), "--root", str(Fixture), "--check-public"]
    assert subprocess.run(Command, capture_output=True, text=True, timeout=10).returncode == 0
    for Key, Value in (("Status", "VANILLA_PUBLIC_COMPLETE"), ("StackCompatibilityGate", "PENDING"),
                       ("ManifestSHA256", "0" * 64), ("CandidateSetComplete", False)):
        Broken = copy.deepcopy(PublicLedger); Broken[Key] = Value
        Machine.write_text(json.dumps(Broken), encoding="utf-8")
        assert subprocess.run(Command, capture_output=True, text=True, timeout=10).returncode != 0
    for Key, Value in (("Id", 123456), ("Stack", 17), ("Family", "Equivalent"), ("Verdict", "SKIP")):
        Broken = copy.deepcopy(PublicLedger)
        Row = next(Row for Row in Broken["Definitions"] if Row["EnabledManifest"])
        Row[Key] = Value
        Machine.write_text(json.dumps(Broken), encoding="utf-8")
        assert subprocess.run(Command, capture_output=True, text=True, timeout=10).returncode != 0
    Machine.write_text(json.dumps(PublicLedger), encoding="utf-8")
    Readable.write_text(Tool.LedgerMarkdown(PublicLedger) + "drift\n", encoding="utf-8")
    assert subprocess.run(Command, capture_output=True, text=True, timeout=10).returncode != 0
    Readable.write_text(Tool.LedgerMarkdown(PublicLedger), encoding="utf-8")
    Page.write_text(Page.read_text(encoding="utf-8").replace("x0000", "unproved"), encoding="utf-8")
    assert subprocess.run(Command, capture_output=True, text=True, timeout=10).returncode != 0

print("[CarbonLuau:LootQualificationTest] PASS complete1307 census, negative histories, independently verified raw UID/cleanup, identity drift rejection, both-host contiguous public gates and deterministic public table drift checks")
