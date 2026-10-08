"""Isolated private discovery package; restores the task-owned prior package."""
import argparse
import hashlib
import os
import pathlib
import shutil
import signal
import subprocess
import time

Parser = argparse.ArgumentParser()
Parser.add_argument("--work", required=True)
Parser.add_argument("--package", required=True)
Args = Parser.parse_args()
Work = pathlib.Path(Args.work).resolve()
if Work != pathlib.Path("/root/codex/world-movement-20261003"):
    raise RuntimeError("Expected established task-owned server")
Root = Work / "server-linux"
Package = Root / "carbon/plugins/CarbonLuau.cszip"
Source = pathlib.Path(Args.package).resolve()
if not Source.is_file() or not Package.is_file() or subprocess.run(["pgrep", "-x", "RustDedicated"], capture_output=True).returncode == 0:
    raise RuntimeError("Isolated source/server precondition failed")
Evidence = Work / "evidence" / ("discovery-" + time.strftime("%Y%m%d-%H%M%S"))
Evidence.mkdir(exist_ok=False)
Backup = Evidence / "prior-package.cszip"
shutil.copy2(Package, Backup)
Log = Evidence / "server.log"
Server = None
try:
    shutil.copy2(Source, Package)
    with (Evidence / "console.log").open("w") as Console:
        Server = subprocess.Popen(["bash", "carbon.sh", "-batchmode", "-nographics", "-logfile", str(Log),
            "+server.ip", "127.0.0.1", "+server.port", "28335", "+server.queryport", "28337",
            "+server.identity", "entity1a-adapter-check", "+server.hostname", "DiscoveryPrivate",
            "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
            "+server.saveinterval", "600", "+rcon.port", "0"], cwd=Root, stdout=Console,
            stderr=subprocess.STDOUT, start_new_session=True)
        Server.wait(timeout=600)
    Content = Log.read_text(errors="replace") if Log.exists() else ""
    print("\n".join(Line for Line in Content.splitlines() if "Discovery" in Line or "EntityLifetime" in Line or "Server startup complete" in Line), flush=True)
    if "[CarbonLuau:DiscoveryFixture] PASS" not in Content or "[CarbonLuau:DiscoveryFixture] FAIL" in Content or "[CarbonLuau:EntityLifetime] Private startup observer qualified" not in Content:
        raise RuntimeError("Discovery qualification failed: " + str(Log))
    print("[CarbonLuau:DiscoveryFixture] EVIDENCE " + str(Log) + " SHA256=" + hashlib.sha256(Log.read_bytes()).hexdigest() + " PACKAGE=" + hashlib.sha256(Source.read_bytes()).hexdigest(), flush=True)
finally:
    if Server is not None and Server.poll() is None:
        os.killpg(Server.pid, signal.SIGTERM)
        try:
            Server.wait(timeout=30)
        except subprocess.TimeoutExpired:
            os.killpg(Server.pid, signal.SIGKILL)
            Server.wait(timeout=10)
    shutil.copy2(Backup, Package)
    print("[CarbonLuau:DiscoveryFixture] CLEANUP owned process exited; prior package restored", flush=True)
