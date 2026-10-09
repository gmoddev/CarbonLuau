"""Run on BigVPS after coordination. Default is read-only artifact preflight."""
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


Prefix = "[CarbonLuau:EntitySpawnRunner]"
Parser = argparse.ArgumentParser(description=__doc__)
Parser.add_argument("--work", default="/root/codex/world-movement-20261003")
Parser.add_argument("--package", required=True)
Parser.add_argument("--native", required=True)
Parser.add_argument("--compiler", required=True)
Parser.add_argument("--run-coordinated", action="store_true")


def DatabaseFiles(Root):
    Expected = pathlib.Path("/root/codex/world-movement-20261003/server-linux/server/entity1a-adapter-check")
    WorldFiles = {"proceduralmap.1000.13579.289.sav", "proceduralmap.1000.13579.289.sav.1",
                  "proceduralmap.1000.13579.289.sav.2", "proceduralmap.1000.13579.289.navmesh"}
    if Root != Expected or Root.is_symlink() or not Root.is_dir() or Root.resolve() != Expected:
        raise RuntimeError("Unsafe isolated database directory")
    Files = [Path for Path in Root.iterdir() if re.search(r"\.db(?:-(?:wal|shm|journal))?$", Path.name) or Path.name in WorldFiles]
    if len(Files) > 128:
        raise RuntimeError("Database file bound exceeded")
    for Path in Files:
        if (not re.fullmatch(r"[A-Za-z0-9_.-]+\.db(?:-(?:wal|shm|journal))?", Path.name) and Path.name not in WorldFiles) or Path.is_symlink() or not Path.is_file() or Path.resolve().parent != Expected:
            raise RuntimeError("Unsafe database snapshot target")
    if sum(Path.stat().st_size for Path in Files) > 67108864:
        raise RuntimeError("Database byte bound exceeded")
    return Files


def Hash(Path):
    Digest = hashlib.sha256()
    with Path.open("rb") as Stream:
        for Block in iter(lambda: Stream.read(1048576), b""):
            Digest.update(Block)
    return Digest.hexdigest()


def Main():
    Args = Parser.parse_args()
    Work = pathlib.Path(Args.work).resolve()
    if Work != pathlib.Path("/root/codex/world-movement-20261003"):
        raise RuntimeError("Expected established task-owned Linux server")
    Root = Work / "server-linux"
    DatabaseRoot = Root / "server/entity1a-adapter-check"
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
        if Names.count("CarbonLuau.EntitySpawnFixtures.cs") != 1 or "CarbonLuau.EntityDiscoveryFixtures.cs" in Names:
            raise RuntimeError("Select the public fixture package exclusively")
    InputHashes = {}
    for Target, Source, Name in Inputs:
        if Source == Target.resolve():
            raise RuntimeError("Coordinated input must be staged outside its installed target")
        InputHashes[Name] = Hash(Source)
        print(Prefix + " INPUT " + Name + " SHA256=" + InputHashes[Name], flush=True)
    if not Args.run_coordinated:
        print(Prefix + " PREFLIGHT only; no server files/processes changed. Add --run-coordinated after coordination.", flush=True)
        return
    LeasePath = Work / "entityspawn.runner.lock"
    # Exclusive creation, never silently clear another run's lease.
    Lease = LeasePath.open("x")
    Evidence = Work / "evidence" / ("entityspawn-" + time.strftime("%Y%m%d-%H%M%S") + "-" + str(os.getpid()))
    Backups = []
    DatabaseBackups = {}
    DatabaseSnapshotReady = False
    Server = None
    RestoreErrors = []
    try:
        Evidence.mkdir(exist_ok=False)
        DatabaseEvidence = Evidence / "prior-player-databases"
        DatabaseEvidence.mkdir()
        for Target in DatabaseFiles(DatabaseRoot):
            Backup = DatabaseEvidence / Target.name
            shutil.copy2(Target, Backup)
            DatabaseBackups[Target.name] = (Target, Backup, Hash(Backup))
        DatabaseSnapshotReady = True
        Preserved = [(Target, Name) for Target, _, Name in Inputs] + [(Config, "carbon-config.json")]
        Preserved.extend((Hooks / Name, Name) for Name in ("Carbon.Hooks.Base.dll", "Carbon.Hooks.Community.dll", "Carbon.Hooks.Oxide.dll"))
        for Target, Name in Preserved:
            Backup = Evidence / ("prior-" + Name)
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
                "+server.identity", "entity1a-adapter-check", "+server.hostname", "CarbonLuauEntitySpawnFixture",
                "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
                "+server.saveinterval", "600", "+rcon.port", "0"], cwd=Root, stdout=Console,
                stderr=subprocess.STDOUT, start_new_session=True)
            Deadline = time.monotonic() + 600
            while Server.poll() is None and time.monotonic() < Deadline:
                Content = Log.read_text(errors="replace") if Log.exists() else ""
                if any(Marker in Content for Marker in ("Failed compiling", "Failed to compile", "[CarbonLuau:EntitySpawnFixture] FAIL", "[CarbonLuau:EntitySpawnFixture] CLEANUP_FAIL", "[CarbonLuau:GameplayPublic] FAIL")):
                    raise RuntimeError("Compile/fixture failure: " + str(Log))
                time.sleep(1)
            if Server.poll() is None:
                raise RuntimeError("Timeout: " + str(Log))
            # Pinned ConVar.Global.quit shuts down then Process.Kill. A direct
            # process yields -SIGKILL; a shell wrapper may yield 128+SIGKILL.
            if Server.returncode not in (0, -signal.SIGKILL, 128 + signal.SIGKILL):
                raise RuntimeError("Server exited with code " + str(Server.returncode) + ": " + str(Log))
        Content = Log.read_text(errors="replace") if Log.exists() else ""
        print("\n".join(Line for Line in Content.splitlines() if "EntitySpawnFixture" in Line or "EntityLifetime" in Line or "Server startup complete" in Line), flush=True)
        Markers = ("PASS completed outer Spawn", "CLEANUP owned entities retired", "NO_ORPHANS_PASS",
                   "PASS recursive full Spawn", "RECURSIVE_CLEANUP_PASS")
        if any("[CarbonLuau:EntitySpawnFixture] " + Marker not in Content for Marker in Markers) or "[CarbonLuau:EntitySpawnFixture] FAIL" in Content or "[CarbonLuau:EntitySpawnFixture] CLEANUP_FAIL" in Content or "[CarbonLuau:EntityLifetime] Private startup observer qualified" not in Content:
            raise RuntimeError("Missing/failed qualification receipts: " + str(Log))
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
            if DatabaseSnapshotReady:
                try:
                    for Target in DatabaseFiles(DatabaseRoot):
                        if Target.name not in DatabaseBackups:
                            Target.unlink()
                    for Target, Backup, Expected in DatabaseBackups.values():
                        shutil.copy2(Backup, Target)
                        if Hash(Target) != Expected:
                            raise RuntimeError("database restoration hash mismatch")
                    print(Prefix + " DATABASE_WORLD_RESTORE " + str(len(DatabaseBackups)) + " prior database/companion/world-save files restored", flush=True)
                except Exception as Error:
                    RestoreErrors.append("Database cleanup: " + str(Error))
            for Target, Backup, Expected in Backups:
                try:
                    shutil.copy2(Backup, Target)
                    if Hash(Target) != Expected:
                        raise RuntimeError("restoration hash mismatch")
                except Exception as Error:
                    RestoreErrors.append(str(Target) + ": " + str(Error))
        Lease.close()
        if RestoreErrors:
            raise RuntimeError("CLEANUP_FAILED lease/backups retained: " + "; ".join(RestoreErrors))
        LeasePath.unlink()
        print(Prefix + " CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored", flush=True)


if __name__ == "__main__":
    Main()
