"""Run on BigKVM after coordination. Default is read-only artifact preflight."""
import argparse
import hashlib
import os
import pathlib
import shutil
import signal
import subprocess
import time
import zipfile
import json
import re
import stat


Prefix = "[CarbonLuau:BarrelLootResearchRunner]"
Parser = argparse.ArgumentParser(description=__doc__)
Parser.add_argument("--work", default="/root/codex/world-movement-20261003")
Parser.add_argument("--package", required=True)
Parser.add_argument("--native", required=True)
Parser.add_argument("--compiler", required=True)
Parser.add_argument("--run-coordinated", action="store_true")
Parser.add_argument("--qualification", action="store_true")
Parser.add_argument("--public", action="store_true")
Parser.add_argument("--entity-death", action="store_true")
Parser.add_argument("--barrel2", action="store_true")
Parser.add_argument("--loot-items", action="store_true")
Parser.add_argument("--loot-item-probe-only", action="store_true")
Parser.add_argument("--loot-registry", action="store_true")
Parser.add_argument("--loot-additional-items", action="store_true")
Parser.add_argument("--d7-probe-only", action="store_true")
Parser.add_argument("--loot-bulk-items", action="store_true")
Parser.add_argument("--d8-discovery-only", action="store_true")
Parser.add_argument("--d8-probe-only", action="store_true")
Parser.add_argument("--d8-stack-gap-only", action="store_true")
Parser.add_argument("--d8-stack-gap-fixed", action="store_true")
Parser.add_argument("--loot-stack-items", action="store_true")
Parser.add_argument("--d8-stack-part", type=int, default=0)
Parser.add_argument("--stack-probes")


def Hash(Path):
    Digest = hashlib.sha256()
    with Path.open("rb") as Stream:
        for Block in iter(lambda: Stream.read(1048576), b""):
            Digest.update(Block)
    return Digest.hexdigest()


def DatabaseFiles(Root):
    Expected = pathlib.Path("/root/codex/world-movement-20261003/server-linux/server/entity1a-adapter-check")
    WorldFiles = {"proceduralmap.1000.13579.289.sav", "proceduralmap.1000.13579.289.sav.1",
                  "proceduralmap.1000.13579.289.sav.2", "proceduralmap.1000.13579.289.navmesh"}
    if Root != Expected or Root.is_symlink() or not Root.is_dir() or Root.resolve() != Expected:
        raise RuntimeError("Unsafe isolated terminal profile directory")
    Files = []
    for Index, Path in enumerate(Root.iterdir()):
        if Index >= 256:
            raise RuntimeError("Terminal profile directory entry bound exceeded")
        if re.search(r"\.db(?:-(?:wal|shm|journal))?$", Path.name) or Path.name in WorldFiles:
            Files.append(Path)
    if len(Files) > 128:
        raise RuntimeError("Terminal profile file bound exceeded")
    for Path in Files:
        if (not re.fullmatch(r"[A-Za-z0-9_.-]+\.db(?:-(?:wal|shm|journal))?", Path.name) and Path.name not in WorldFiles) or \
                Path.is_symlink() or not Path.is_file() or Path.resolve().parent != Expected or Path.stat().st_nlink != 1:
            raise RuntimeError("Unsafe terminal profile snapshot target")
    if sum(Path.stat().st_size for Path in Files) > 67108864:
        raise RuntimeError("Terminal profile byte bound exceeded")
    return sorted(Files, key=lambda Path: Path.name)


def ProfileMetadata(Path):
    Value = Path.stat()
    return {"Mode": stat.S_IMODE(Value.st_mode), "Uid": Value.st_uid, "Gid": Value.st_gid,
            "AccessNs": Value.st_atime_ns, "WriteNs": Value.st_mtime_ns}


def RestoreProfileMetadata(Path, Value):
    Current = Path.stat()
    if (Current.st_uid, Current.st_gid) != (Value["Uid"], Value["Gid"]):
        os.chown(Path, Value["Uid"], Value["Gid"])
    if stat.S_IMODE(Path.stat().st_mode) != Value["Mode"]:
        os.chmod(Path, Value["Mode"])
    if (Current.st_atime_ns, Current.st_mtime_ns) != (Value["AccessNs"], Value["WriteNs"]):
        os.utime(Path, ns=(Value["AccessNs"], Value["WriteNs"]))
    if ProfileMetadata(Path) != Value:
        raise RuntimeError("Terminal profile metadata restoration mismatch")


def SnapshotProfile(Root, Evidence):
    DirectoryMetadata = ProfileMetadata(Root)
    Files = DatabaseFiles(Root)
    Snapshot = {"Root": Root, "DirectoryMetadata": DirectoryMetadata, "Files": {}}
    Evidence.mkdir()
    for Target in Files:
        Metadata = ProfileMetadata(Target)
        Backup = Evidence / Target.name
        shutil.copy2(Target, Backup)
        Snapshot["Files"][Target.name] = {"Target": Target, "Backup": Backup, "SHA256": Hash(Backup),
            "Bytes": Backup.stat().st_size, "Metadata": Metadata}
        RestoreProfileMetadata(Target, Metadata)
    Public = [{"Name": Name, "SHA256": Value["SHA256"], "Bytes": Value["Bytes"], "Metadata": Value["Metadata"]}
              for Name, Value in Snapshot["Files"].items()]
    Canonical = json.dumps(Public, sort_keys=True, separators=(",", ":")).encode()
    Snapshot["ManifestSHA256"] = hashlib.sha256(Canonical).hexdigest()
    (Evidence / "profile-manifest.json").write_text(json.dumps({"Root": str(Root), "Files": Public,
        "DirectoryMetadata": Snapshot["DirectoryMetadata"], "ManifestSHA256": Snapshot["ManifestSHA256"]},
        sort_keys=True, indent=2) + "\n")
    RestoreProfileMetadata(Root, DirectoryMetadata)
    return Snapshot


def RestoreProfile(Snapshot):
    Root = Snapshot["Root"]
    for Target in DatabaseFiles(Root):
        if Target.name not in Snapshot["Files"]:
            Target.unlink()
    for Value in Snapshot["Files"].values():
        Target, Backup = Value["Target"], Value["Backup"]
        if Target.is_symlink() or (Target.exists() and not Target.is_file()):
            raise RuntimeError("Unsafe terminal profile restore target")
        if Hash(Backup) != Value["SHA256"]:
            raise RuntimeError("Terminal profile backup changed")
        shutil.copy2(Backup, Target)
        if Hash(Target) != Value["SHA256"] or Target.stat().st_size != Value["Bytes"]:
            raise RuntimeError("Terminal profile restoration bytes mismatch")
        RestoreProfileMetadata(Target, Value["Metadata"])
    if {Path.name for Path in DatabaseFiles(Root)} != set(Snapshot["Files"]):
        raise RuntimeError("Terminal profile restored file set mismatch")
    RestoreProfileMetadata(Root, Snapshot["DirectoryMetadata"])
    return len(Snapshot["Files"]), sum(Value["Bytes"] for Value in Snapshot["Files"].values()), Snapshot["ManifestSHA256"]


def Main():
    Args = Parser.parse_args()
    if Args.d8_stack_gap_only and Args.d8_stack_gap_fixed:
        raise RuntimeError("Select old-negative or fixed stack gap mode")
    if Args.d8_stack_gap_only or Args.d8_stack_gap_fixed:
        Args.loot_stack_items = True
    if Args.loot_stack_items and (Args.loot_bulk_items or Args.d7_probe_only or Args.loot_item_probe_only):
        raise RuntimeError("Select stack qualification exclusively")
    if Args.d8_stack_part not in (0, 1, 2, 3):
        raise RuntimeError("Stack part must be0..3")
    StackMatrix = Args.loot_stack_items and not Args.d8_stack_gap_only and not Args.d8_stack_gap_fixed
    StackTerminal = StackMatrix and Args.d8_stack_part == 2
    StackSweep = StackMatrix and Args.d8_stack_part == 3
    if (StackMatrix and not StackSweep) != bool(Args.stack_probes):
        raise RuntimeError("Only stack ordinary/terminal matrix requires explicit separate probe directory")
    if StackTerminal and Args.entity_death:
        raise RuntimeError("Terminal UID negative does not claim B2 completion")
    if Args.loot_stack_items:
        Args.loot_additional_items = True
    if (Args.d8_discovery_only or Args.d8_probe_only) and not Args.loot_bulk_items:
        raise RuntimeError("D8 mode requires LootBulkItems selection")
    if Args.d8_discovery_only and Args.d8_probe_only:
        raise RuntimeError("Select D8 discovery or physical probe mode")
    if Args.loot_bulk_items and (Args.d7_probe_only or Args.loot_item_probe_only):
        raise RuntimeError("Select D8 mode exclusively")
    if Args.loot_bulk_items:
        Args.loot_additional_items = True
    D8EarlyOnly = Args.d8_discovery_only or Args.d8_probe_only
    if Args.d7_probe_only and not Args.loot_additional_items:
        raise RuntimeError("D7 probe requires LootAdditionalItems qualification selection")
    if Args.loot_additional_items and Args.loot_item_probe_only:
        raise RuntimeError("Select the D7 probe exclusively")
    if Args.loot_additional_items:
        Args.loot_registry = True
    if Args.loot_registry and Args.loot_item_probe_only:
        raise RuntimeError("D6 registry requires full public qualification")
    if Args.loot_registry:
        Args.loot_items = True
    if Args.loot_item_probe_only and not Args.loot_items:
        raise RuntimeError("D5 probe requires LootItems qualification selection")
    if Args.loot_items:
        Args.barrel2 = Args.public = Args.qualification = True
    FixtureMarker = "[CarbonLuau:LootFixture]" if Args.qualification else "[CarbonLuau:BarrelLootResearch]"
    if Args.public and not Args.qualification:
        raise RuntimeError("Public qualification requires the established D2A fixture substrate")
    if Args.entity_death and not Args.public:
        raise RuntimeError("Entity death qualification requires the public fixture substrate")
    if Args.barrel2 and not Args.public:
        raise RuntimeError("Barrel-2 qualification requires the public fixture substrate")
    PassMarker = "PASS D8 registry discovery" if Args.d8_discovery_only else "PASS D8 bulk probe batch" if Args.d8_probe_only else "PASS D7 stones probe" if Args.d7_probe_only else "PASS D5 item probe" if Args.loot_item_probe_only else "PASS public qualification" if Args.public else "PASS private qualification" if Args.qualification else "PASS mechanism"
    if StackTerminal:
        PassMarker = "D8_STACK_BATCH mode=terminal_uid completed=true cases=1 unsupported_i12=1 normal_cleanup_claim=false process_restoration_required=true"
    Work = pathlib.Path(Args.work).resolve()
    if Work != pathlib.Path("/root/codex/world-movement-20261003"):
        raise RuntimeError("Expected established task-owned Linux server")
    Root = Work / "server-linux"
    NativeDirectory = Root / "carbon/data/CarbonLuau/native/linux-x64"
    Inputs = [(Root / "carbon/plugins/CarbonLuau.cszip", pathlib.Path(Args.package).resolve(), "package.cszip"),
              (NativeDirectory / "libcarbonluau_native.so", pathlib.Path(Args.native).resolve(), "native.so"),
              (NativeDirectory / "carbonluau_compiler", pathlib.Path(Args.compiler).resolve(), "compiler")]
    Config = Root / "carbon/config.json"
    Hooks = Root / "carbon/managed/hooks"
    Required = [Root / "RustDedicated", Root / "carbon.sh", Config]
    for Target, Source, _ in Inputs:
        Required.extend([Target, Source])
    Required.extend(Hooks / Name for Name in ("Carbon.Hooks.Base.dll", "Carbon.Hooks.Community.dll", "Carbon.Hooks.Oxide.dll"))
    for Path in Required:
        if not Path.is_file():
            raise RuntimeError("Required file missing: " + str(Path))
    # No unrelated server is stopped, attached to, or overwritten.
    Probe = subprocess.run(["pgrep", "-x", "RustDedicated"], capture_output=True)
    if Probe.returncode != 1:
        raise RuntimeError("Existing RustDedicated or process inspection failure; left untouched")
    if Hash(Hooks / "Carbon.Hooks.Community.dll") != "4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343" or Hash(Hooks / "Carbon.Hooks.Oxide.dll") != "b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764":
        raise RuntimeError("Qualified Linux hook tuple required")
    with zipfile.ZipFile(Inputs[0][1]) as Archive:
        Names = Archive.namelist()
        if Names.count("CarbonLuau.BarrelLootResearch.cs") != 1 or "CarbonLuau.EntityDiscoveryFixtures.cs" in Names:
            raise RuntimeError("Select the public fixture package exclusively")
        if ("CarbonLuau.LootFixtures.cs" in Names) != Args.qualification:
            raise RuntimeError("Qualification fixture selection mismatch")
        if ("CarbonLuau.LootBarrel2Fixtures.cs" in Names) != Args.barrel2:
            raise RuntimeError("Barrel-2 fixture selection mismatch")
        if ("CarbonLuau.LootItemFixtures.cs" in Names) != Args.loot_items:
            raise RuntimeError("D5 fixture selection mismatch")
        if ("CarbonLuau.LootItemProbeFixtures.cs" in Names) != Args.loot_item_probe_only:
            raise RuntimeError("D5 probe selection mismatch")
        if ("CarbonLuau.LootRegistryFixtures.cs" in Names) != Args.loot_registry:
            raise RuntimeError("D6 registry selection mismatch")
        if ("CarbonLuau.LootAdditionalItemFixtures.cs" in Names) != Args.loot_additional_items:
            raise RuntimeError("D7 fixture selection mismatch")
        if ("CarbonLuau.LootAdditionalItemProbeFixtures.cs" in Names) != Args.d7_probe_only:
            raise RuntimeError("D7 probe selection mismatch")
        if ("CarbonLuau.LootBulkItemFixtures.cs" in Names) != Args.loot_bulk_items:
            raise RuntimeError("D8 fixture selection mismatch")
        if ("CarbonLuau.LootStackFixtures.cs" in Names) != Args.loot_stack_items:
            raise RuntimeError("D8 stack fixture selection mismatch")
        if ("CarbonLuau.LootStackMatrixFixtures.cs" in Names) != (StackMatrix and not StackSweep) or ("CarbonLuau.LootStackModeFixtures.cs" in Names) != StackMatrix:
            raise RuntimeError("D8 stack matrix selection mismatch")
        if ("CarbonLuau.LootFinalItemFixtures.cs" in Names) != StackSweep:
            raise RuntimeError("D8 final sweep selection mismatch")
        if "CarbonLuauStackProbeA.cs" in Names or "CarbonLuauStackProbeB.cs" in Names:
            raise RuntimeError("Hook probes must remain separate test plugins")
        if ("CarbonLuau.LootBulkItemModeFixtures.cs" in Names) != D8EarlyOnly:
            raise RuntimeError("D8 mode selection mismatch")
        if ("CarbonLuau.LootBulkItemManifestFixtures.cs" in Names) != (Args.loot_bulk_items and not D8EarlyOnly):
            raise RuntimeError("D8 public manifest selection mismatch")
    StackPreserved = []
    if StackMatrix and not StackSweep:
        for Name in ("CarbonLuauStackProbeA", "CarbonLuauStackProbeB"):
            Source = pathlib.Path(Args.stack_probes).resolve() / (Name + ".cs")
            if not Source.is_file():
                raise RuntimeError("Missing bounded separate stack probe: " + str(Source))
            Inputs.append((Root / "carbon/plugins" / (Name + ".cs"), Source, Name + ".cs"))
            StackPreserved.append((Root / "carbon/configs" / (Name + ".json"), "config-" + Name + ".json"))
    InputHashes = {}
    for Target, Source, Name in Inputs:
        if Source == Target.resolve():
            raise RuntimeError("Coordinated input must be staged outside its installed target")
        InputHashes[Name] = Hash(Source)
        print(Prefix + " INPUT " + Name + " SHA256=" + InputHashes[Name], flush=True)
    if not Args.run_coordinated:
        print(Prefix + " PREFLIGHT only; no server files/processes changed. Add --run-coordinated after coordination.", flush=True)
        return
    LeasePath = Work / "loot-d2a.runner.lock"
    # Exclusive creation, never silently clear another run's lease.
    Lease = LeasePath.open("x")
    Evidence = Work / "evidence" / ("loot-d2a-" + time.strftime("%Y%m%d-%H%M%S") + "-" + str(os.getpid()))
    Backups = []
    AbsentTargets = []
    ProfileSnapshot = None
    Server = None
    RestoreErrors = []
    try:
        Evidence.mkdir(exist_ok=False)
        if StackTerminal:
            ProfileSnapshot = SnapshotProfile(Root / "server/entity1a-adapter-check", Evidence / "prior-terminal-profile")
            print(Prefix + " PROFILE_SNAPSHOT files=" + str(len(ProfileSnapshot["Files"])) +
                  " file_cap=128 byte_cap=67108864 manifest_sha256=" + ProfileSnapshot["ManifestSHA256"], flush=True)
        Preserved = [(Target, Name) for Target, _, Name in Inputs] + [(Config, "carbon-config.json")] + StackPreserved
        Preserved.extend((Hooks / Name, Name) for Name in ("Carbon.Hooks.Base.dll", "Carbon.Hooks.Community.dll", "Carbon.Hooks.Oxide.dll"))
        for Target, Name in Preserved:
            Backup = Evidence / ("prior-" + Name)
            if not Target.exists():
                if Name not in ("CarbonLuauStackProbeA.cs", "CarbonLuauStackProbeB.cs", "config-CarbonLuauStackProbeA.json", "config-CarbonLuauStackProbeB.json"):
                    raise RuntimeError("Required installed file disappeared before backup: " + str(Target))
                AbsentTargets.append(Target)
                continue
            if not Target.is_file():
                raise RuntimeError("Preserved target is not a file: " + str(Target))
            shutil.copy2(Target, Backup)
            Backups.append((Target, Backup, Hash(Backup)))
        for Target, Source, _ in Inputs:
            shutil.copy2(Source, Target)
        # File mode comes from the coordinated artifact, never broad chmod.
        if not os.access(Inputs[2][0], os.X_OK):
            raise RuntimeError("Coordinated compiler must carry executable mode")
        Value = json.loads(Config.read_text())
        Value["SelfUpdating"]["Enabled"] = False
        Value["SelfUpdating"]["HookUpdates"] = False
        Config.write_text(json.dumps(Value, indent=2))
        Log = Evidence / "server.log"
        with (Evidence / "console.log").open("w") as Console:
            Server = subprocess.Popen(["bash", "carbon.sh", "-batchmode", "-nographics", "-logfile", str(Log),
                "+server.ip", "127.0.0.1", "+server.port", "28335", "+server.queryport", "28337",
                "+server.identity", "entity1a-adapter-check", "+server.hostname", "CarbonLuauBarrelLootResearch",
                "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
                "+server.saveinterval", "600", "+rcon.port", "0"], cwd=Root, stdout=Console,
                stderr=subprocess.STDOUT, start_new_session=True)
            Deadline = time.monotonic() + 600
            while Server.poll() is None and time.monotonic() < Deadline:
                Content = Log.read_text(errors="replace") if Log.exists() else ""
                if any(Marker in Content for Marker in ("Failed compiling", "Failed to compile", "[CarbonLuau:BarrelLootResearch] FAIL", "[CarbonLuau:LootFixture] FAIL", "[CarbonLuau:LootFixture] CLEANUP_FAIL")):
                    raise RuntimeError("Compile/fixture failure: " + str(Log))
                time.sleep(1)
            if Server.poll() is None:
                raise RuntimeError("Timeout: " + str(Log))
            # Pinned ConVar.Global.quit shuts down then Process.Kill. A direct
            # process yields -SIGKILL; a shell wrapper may yield 128+SIGKILL.
            if Server.returncode not in (0, -signal.SIGKILL, 128 + signal.SIGKILL):
                raise RuntimeError("Server exited with code " + str(Server.returncode) + ": " + str(Log))
        Content = Log.read_text(errors="replace") if Log.exists() else ""
        print("\n".join(Line for Line in Content.splitlines() if "LootFixture" in Line or "EntityDiedFixture" in Line or "EntityMeleeFixture" in Line or "MEASURED case=" in Line or "PASS mechanism" in Line or "EntityLifetime" in Line or "Server startup complete" in Line), flush=True)
        Markers = (PassMarker,)
        if Args.d8_stack_gap_only or Args.d8_stack_gap_fixed:
            StackRows = re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_GAP item=[^\r\n]+", Content)
            StackBatches = re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_BATCH [^\r\n]+", Content)
            Expected = "[CarbonLuau:LootFixture] D8_STACK_BATCH mode=gap_probe completed=true cases=12 " + ("pass=0 fail=12 cleanup=0 all_pass=False" if Args.d8_stack_gap_only else "pass=12 fail=0 cleanup=0 all_pass=True")
            RowPattern = r" result=FAIL committed=True created=0 accepted=0 selected_delta=1 partial_delta=1 original_stored=2 vanilla_worlds=0 reason=stack_drop_after_prepare_suppresses_vanilla$" if Args.d8_stack_gap_only else r" result=PASS committed=False created=0 accepted=0 selected_delta=1 partial_delta=0 original_stored=2 vanilla_worlds=2 reason=stack_drop_before_commit_preserves_vanilla$"
            if len(StackRows) != 12 or StackBatches != [Expected] or any(not re.search(
                    RowPattern, Row)
                    for Row in StackRows) or len(re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_CLEANUP [^\r\n]+ ledger=0", Content)) != 12:
                raise RuntimeError("Missing exact old-source stack-gap counterexample/cleanup receipts")
            if Args.d8_stack_gap_only:
                print(Prefix + " EXPECTED_NEGATIVE D8 stack gap old-source counterexample; compatibility remains unqualified", flush=True)
        if StackMatrix and not StackTerminal and not StackSweep:
            StackRows = re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_CASE [^\r\n]+", Content)
            Expected = "[CarbonLuau:LootFixture] D8_STACK_BATCH mode=matrix part=" + str(Args.d8_stack_part) + " completed=true cases=57 pass=57 fail=0 cleanup=0 all_pass=True"
            Pattern = r"\[CarbonLuau:LootFixture\] D8_STACK_CASE part=" + str(Args.d8_stack_part) + r" item=[a-z0-9._-]+ phase=([0-9]|1[0-8]) case=[a-z_]+ result=PASS profiles=2 cleanup=0"
            if len(StackRows) != 57 or len(set(StackRows)) != 57 or any(not re.fullmatch(Pattern, Row) for Row in StackRows) or re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_BATCH [^\r\n]+", Content) != [Expected]:
                raise RuntimeError("D8 stack ordinary matrix incomplete or failed")
        if StackTerminal:
            if "[CarbonLuau:LootFixture] D8_STACK_LEGACY before_terminal=true d3_d7_completed=true ledger=0 b2_terminal_claim=false" not in Content or "[CarbonLuau:LootFixture] PASS public qualification" in Content:
                raise RuntimeError("Terminal negative must preserve separate scope and refuse normal cleanup claim")
            Pattern = r"\[CarbonLuau:LootFixture\] D8_STACK_TERMINAL item=sulfur id=-1581843485 result=UNSUPPORTED_I12 original_uid=[1-9][0-9]* actual_uid=0 world=[1-9][0-9]* completed=true reciprocal_ref=true partial_delta=1 cleanup_failed_delta=1 max_calls=0 can_calls=0 split_calls=0 drop_calls=1 normal_cleanup_claim=false uid_restored=false authority_refreshed=false"
            Rows = re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_TERMINAL [^\r\n]+", Content)
            if len(Rows) != 1 or not re.fullmatch(Pattern, Rows[0]):
                raise RuntimeError("Missing exact terminal I12 negative")
        if StackSweep:
            Rows = re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_SWEEP name=[^\r\n]+", Content)
            Pattern = r"\[CarbonLuau:LootFixture\] D8_STACK_SWEEP name=([a-z0-9._-]+) id=(-?[0-9]+) stackable=([1-9][0-9]*) family=(Baseline|RawResource|Component|BasicMaterial|Consumable|Equivalent) prefab=(\S+) minimum=1 maximum=([1-9][0-9]*) result=PASS worlds=2 cleanup=0 original_uid_world_verified=true completed_spawn=true"
            if len(Rows) != 442 or len(set(Rows)) != 442 or any(not re.fullmatch(Pattern, Row) for Row in Rows) or len(re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_SWEEP_WORLD [^\r\n]+", Content)) != 884 or len(re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_SWEEP_BEGIN [^\r\n]+", Content)) != 1 or re.findall(r"\[CarbonLuau:LootFixture\] D8_STACK_SWEEP_BATCH [^\r\n]+", Content) != ["[CarbonLuau:LootFixture] D8_STACK_SWEEP_BATCH completed=true definitions=221 cases=442 pass=442 fail=0 cleanup=0 all_pass=True"]:
                raise RuntimeError("D8 final-source sweep incomplete, duplicated or failed")
        if any(FixtureMarker + " " + Marker not in Content for Marker in Markers) or "[CarbonLuau:BarrelLootResearch] FAIL" in Content or "[CarbonLuau:LootFixture] FAIL" in Content or "[CarbonLuau:LootFixture] CLEANUP_FAIL" in Content or "[CarbonLuau:EntityLifetime] Private startup observer qualified" not in Content:
            raise RuntimeError("Missing/failed qualification receipts: " + str(Log))
        if Args.barrel2 and not Args.loot_item_probe_only and not Args.d7_probe_only and not D8EarlyOnly and not StackTerminal and "[CarbonLuau:LootFixture] PASS barrel-2" not in Content:
            raise RuntimeError("Barrel-2 physical matrix receipt missing: " + str(Log))
        if Args.loot_items and not Args.loot_item_probe_only and not Args.d7_probe_only and not D8EarlyOnly and not StackTerminal and "[CarbonLuau:LootFixture] PASS D5 item expansion full public wood matrix" not in Content:
            raise RuntimeError("D5 physical item matrix receipt missing: " + str(Log))
        if Args.loot_registry and not Args.d7_probe_only and not D8EarlyOnly and not StackTerminal and "[CarbonLuau:LootFixture] PASS D6 bounded per-prefab registry" not in Content:
            raise RuntimeError("D6 registry matrix receipt missing: " + str(Log))
        if Args.loot_additional_items and not Args.d7_probe_only and not D8EarlyOnly and not StackTerminal and "[CarbonLuau:LootFixture] PASS D7 stones full public" not in Content:
            raise RuntimeError("D7 public stones matrix receipt missing: " + str(Log))
        if Args.loot_bulk_items and not D8EarlyOnly and "[CarbonLuau:LootFixture] PASS D8 bulk public" not in Content:
            raise RuntimeError("D8 public matrix receipt missing: " + str(Log))
        if Args.loot_bulk_items and not D8EarlyOnly:
            Batches = re.findall(r"D8 bulk public batch offset=(\d+) count=(\d+) pass=(\d+) fail=(\d+)", Content)
            Rows = re.findall(r"D8_PUBLIC_ITEM name=(\S+) item_id=(-?\d+) stackable=(\d+) family=(\S+) result=(PASS|FAIL)(?: |\r?$)", Content, re.MULTILINE)
            if (len(Batches) != 1 or not 1 <= int(Batches[0][1]) <= 32 or int(Batches[0][2]) != int(Batches[0][1]) or
                    int(Batches[0][3]) != 0 or len(Rows) != int(Batches[0][1]) or
                    len(re.findall(r"\bD8_PUBLIC_ITEM\b", Content)) != len(Rows) or
                    any(Row[4] != "PASS" for Row in Rows) or len({Row[0] for Row in Rows}) != len(Rows)):
                raise RuntimeError("D8 public per-item receipts incomplete, duplicated or failed: " + str(Log))
        if Args.entity_death and ("[CarbonLuau:EntityDiedFixture] PASS bounded public transport and actual RPC source" not in Content or
                "[CarbonLuau:EntityMeleeFixture] PASS actual RPC source/terminal proof and cleanup" not in Content or
                "[CarbonLuau:EntityMeleeFixture] FAIL" in Content or "[CarbonLuau:EntityMeleeFixture] CLEANUP FAIL" in Content):
            raise RuntimeError("Entity death receipt missing/failed: " + str(Log))
        if "Shutting down Carbon.." not in Content or "Saving complete" not in Content:
            raise RuntimeError("Missing qualified quit shutdown/save receipt: " + str(Log))
        print(Prefix + " PROCESS_EXIT " + str(Server.returncode) + " qualified Shutdown -> Process.Kill", flush=True)
        print(Prefix + " EVIDENCE " + str(Log) + " SHA256=" + Hash(Log) + " PACKAGE=" + InputHashes["package.cszip"] + " NATIVE=" + InputHashes["native.so"] + " COMPILER=" + InputHashes["compiler"], flush=True)
    finally:
        Exited = True
        if Server is not None:
            # Kill only the group this runner created, including carbon.sh's child.
            try:
                os.killpg(Server.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            # The carbon.sh leader may exit before its Rust child. Wait for the
            # whole owned group before restoring files a child could still load.
            for DeadlineSeconds, KillSignal in ((30, signal.SIGTERM), (10, signal.SIGKILL)):
                try:
                    os.killpg(Server.pid, KillSignal)
                except ProcessLookupError:
                    pass
                End = time.monotonic() + DeadlineSeconds
                while time.monotonic() < End:
                    Server.poll()  # Reap our child so its zombie cannot hold the group.
                    try:
                        os.killpg(Server.pid, 0)
                    except ProcessLookupError:
                        break
                    time.sleep(0.1)
                else:
                    continue
                break
            else:
                Exited = False
                RestoreErrors.append("Owned process group did not exit; backups retained")
        if Exited:
            if ProfileSnapshot is not None:
                try:
                    Count, Size, Digest = RestoreProfile(ProfileSnapshot)
                    print(Prefix + " FullProfileRestored files=" + str(Count) + " bytes=" + str(Size) +
                          " file_cap=128 byte_cap=67108864 metadata=true scope=db_companions_saves_navmesh manifest_sha256=" + Digest, flush=True)
                except Exception as Error:
                    RestoreErrors.append("Terminal profile restoration: " + str(Error))
            for Target, Backup, Expected in Backups:
                try:
                    shutil.copy2(Backup, Target)
                    if Hash(Target) != Expected:
                        raise RuntimeError("restoration hash mismatch")
                except Exception as Error:
                    RestoreErrors.append(str(Target) + ": " + str(Error))
            for Target in AbsentTargets:
                try:
                    if Target.is_file():
                        Target.unlink()
                    if Target.exists():
                        raise RuntimeError("created probe target absence not restored")
                except Exception as Error:
                    RestoreErrors.append(str(Target) + ": " + str(Error))
        Lease.close()
        if RestoreErrors:
            raise RuntimeError("CLEANUP_FAILED lease/backups retained: " + "; ".join(RestoreErrors))
        LeasePath.unlink()
        print(Prefix + " CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored", flush=True)


if __name__ == "__main__":
    Main()
