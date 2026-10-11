"""Run on BigKVM after coordination. Default is read-only artifact preflight.
Package with tools/Package.ps1 -IncludeInventoryNetFixtures ONLY.
Reuse main-coordinated B2 native/compiler artifacts; C2 has no native changes.
"""
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


Prefix = "[CarbonLuau:InventoryNetChangeRunner]"
Parser = argparse.ArgumentParser(description=__doc__)
Parser.add_argument("--work", default="/root/codex/world-movement-20261003")
Parser.add_argument("--package", required=True)
Parser.add_argument("--native", required=True)
Parser.add_argument("--compiler", required=True)
Parser.add_argument("--run-coordinated", action="store_true")


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
        Fixtures = [Name for Name in Names if re.search(r"(Fixtures?|Research|Evidence|Provider|Consumer)\.cs$", Name, re.IGNORECASE)]
        if Names.count("CarbonLuau.InventoryNetChangeFixtures.cs") != 1 or len(Fixtures) != 1:
            raise RuntimeError("Select the private C2 sampled fixture package exclusively")
    InputHashes = {}
    for Target, Source, Name in Inputs:
        if Source == Target.resolve():
            raise RuntimeError("Coordinated input must be staged outside its installed target")
        InputHashes[Name] = Hash(Source)
        print(Prefix + " INPUT " + Name + " SHA256=" + InputHashes[Name], flush=True)
    if not Args.run_coordinated:
        print(Prefix + " PREFLIGHT only; no server files/processes changed. Add --run-coordinated after coordination.", flush=True)
        return
    LeasePath = Work / "inventory-c2.runner.lock"
    # Exclusive creation, never silently clear another run's lease.
    Lease = LeasePath.open("x")
    Evidence = Work / "evidence" / ("inventory-c2-" + time.strftime("%Y%m%d-%H%M%S") + "-" + str(os.getpid()))
    Backups = []
    Server = None
    RestoreErrors = []
    try:
        Evidence.mkdir(exist_ok=False)
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
                "+server.identity", "entity1a-adapter-check", "+server.hostname", "CarbonLuauInventoryNetChange",
                "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
                "+server.saveinterval", "600", "+rcon.port", "0"], cwd=Root, stdout=Console,
                stderr=subprocess.STDOUT, start_new_session=True)
            Deadline = time.monotonic() + 600
            while Server.poll() is None and time.monotonic() < Deadline:
                Content = Log.read_text(errors="replace") if Log.exists() else ""
                if any(Marker in Content for Marker in ("Failed compiling", "Failed to compile", "[CarbonLuau:InventoryNetChangeFixture] FAIL")):
                    raise RuntimeError("Compile/fixture failure: " + str(Log))
                time.sleep(1)
            if Server.poll() is None:
                raise RuntimeError("Timeout: " + str(Log))
            # Pinned ConVar.Global.quit shuts down then Process.Kill. A direct
            # process yields -SIGKILL; a shell wrapper may yield 128+SIGKILL.
            if Server.returncode not in (0, -signal.SIGKILL, 128 + signal.SIGKILL):
                raise RuntimeError("Server exited with code " + str(Server.returncode) + ": " + str(Log))
        Content = Log.read_text(errors="replace") if Log.exists() else ""
        print("\n".join(Line for Line in Content.splitlines() if "InventoryNetChangeFixture" in Line or "EntityLifetime" in Line or "Server startup complete" in Line), flush=True)
        Markers = (
            "PASS loaded IsValid/IsRemoved exact tokens/body hashes, no EH/locals/calls and zero Harmony patches",
            "PASS loaded IL/topology receipts and Item.IsValid prefixes=0 before physical tests",
            "PASS create/use/direct amounts and balanced interval",
            "PASS completed main/belt/wear transfers and split/merge zero net",
            "NEGATIVE intermediate transfer sampled -80/+80; completed transfer zero",
            "NEGATIVE intermediate split sampled -30/+30; completed split zero",
            "PASS world drop/pickup sampled quantities",
            "PASS callback interference sampled +12 for requested +5; no causal attribution",
            "PASS Take/Remove and bounded ordered changes",
            "PASS controlled D11 lifetime and C1 read parity; no authenticated client",
            "PASS live registry Stage/Tick/Consume/Cancel, distinct frames, root replacement, reconnect and fatal recovery",
            "CLEANUP fixture-owned Player/items/world/delegates released",
            "PASS actual physical sampled inventory; no public Signals or completed-operation claim",
        )
        if any("[CarbonLuau:InventoryNetChangeFixture] " + Marker not in Content for Marker in Markers) or "[CarbonLuau:InventoryNetChangeFixture] FAIL" in Content or "[CarbonLuau:EntityLifetime] Private startup observer qualified" not in Content:
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
