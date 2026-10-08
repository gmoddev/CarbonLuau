"""Research only: bounded loopback server run in a task-owned exact-host copy."""
import argparse
import os
import pathlib
import shutil
import signal
import subprocess
import time

Parser = argparse.ArgumentParser()
Parser.add_argument("--work", required=True)
Parser.add_argument("--package")
Args = Parser.parse_args()
Work = pathlib.Path(Args.work).resolve()
if not str(Work).startswith("/root/codex/world-movement-"):
    raise RuntimeError("Expected isolated movement-research workspace")
Root = Work / "server-linux"
Source = Work / "CarbonLuau.WorldMovementResearch.cs"
Target = Root / "carbon/plugins/CarbonLuau.WorldMovementResearch.cs"
if not (Root / "RustDedicated").is_file() or not Source.is_file() or Target.exists():
    raise RuntimeError("Fixture/server ownership precondition failed")
if subprocess.run(["pgrep", "-x", "RustDedicated"], capture_output=True).returncode == 0:
    raise RuntimeError("Existing RustDedicated process protected")
Evidence = Work / "evidence" / ("movement-" + time.strftime("%Y%m%d-%H%M%S"))
Evidence.mkdir(parents=True, exist_ok=False)
Log = Evidence / "server.log"
Server = None
Package = Root / "carbon/plugins/CarbonLuau.cszip"
Backup = Evidence / "prior-package.cszip"
PackageReplaced = False
try:
    if Args.package:
        if not pathlib.Path(Args.package).is_file() or not Package.is_file():
            raise RuntimeError("Package precondition failed")
        shutil.copy2(Package, Backup)
        PackageReplaced = True
        shutil.copy2(Args.package, Package)
    shutil.copy2(Source, Target)
    with (Evidence / "console.log").open("w") as Console:
        Server = subprocess.Popen(
            ["bash", "carbon.sh", "-batchmode", "-nographics", "-logfile", str(Log),
             "+server.ip", "127.0.0.1", "+server.port", "28335", "+server.queryport", "28337",
             "+server.identity", "entity1a-adapter-check", "+server.hostname", "MovementResearch",
             "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
             "+server.saveinterval", "600", "+rcon.port", "0"],
            cwd=Root, stdout=Console, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            Server.wait(timeout=900)
        finally:
            if Server.poll() is None:
                os.killpg(Server.pid, signal.SIGTERM)
                try:
                    Server.wait(timeout=30)
                except subprocess.TimeoutExpired:
                    os.killpg(Server.pid, signal.SIGKILL)
                    Server.wait(timeout=10)
    Content = Log.read_text(errors="replace") if Log.exists() else ""
    print("\n".join(Line for Line in Content.splitlines()
                    if "WorldMovementResearch" in Line or "Server startup complete" in Line), flush=True)
    if ("[CarbonLuau:WorldMovementResearch] PASS" not in Content or
            "[CarbonLuau:WorldMovementResearch] FAIL" in Content):
        raise RuntimeError("Research probe failed; log=" + str(Log))
    print("[CarbonLuau:WorldMovementResearch] PASS log=" + str(Log), flush=True)
finally:
    Target.unlink(missing_ok=True)
    if PackageReplaced:
        shutil.copy2(Backup, Package)
    print("[CarbonLuau:WorldMovementResearch] CLEANUP own process stopped; unique fixture removed", flush=True)
