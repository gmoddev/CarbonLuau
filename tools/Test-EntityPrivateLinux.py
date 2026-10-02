"""Run the private Entity-1A production adapter in an isolated Carbon copy."""
import argparse
import os
import pathlib
import signal
import subprocess
import time


Parser = argparse.ArgumentParser()
Parser.add_argument("--work", required=True)
Args = Parser.parse_args()
Work = pathlib.Path(Args.work).resolve()
Root = Work / "server-linux"
Package = Root / "carbon/plugins/CarbonLuau.cszip"
if not Package.is_file():
    raise RuntimeError("Task-owned CarbonLuau package missing")
Evidence = Work / "evidence" / ("entity-private-" + time.strftime("%Y%m%d-%H%M%S"))
Evidence.mkdir(parents=True, exist_ok=False)
Log = Evidence / "server.log"

with (Evidence / "console.log").open("w") as Console:
    Server = subprocess.Popen(
        ["bash", "carbon.sh", "-batchmode", "-nographics", "-logfile", str(Log),
         "+server.ip", "127.0.0.1", "+server.port", "28335", "+server.queryport", "28337",
         "+server.identity", "entity1a-adapter-check", "+server.hostname", "Entity1APrivate",
         "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
         "+server.saveinterval", "600", "+rcon.port", "0"],
        cwd=Root, stdout=Console, stderr=subprocess.STDOUT, start_new_session=True)
    try:
        Deadline = time.monotonic() + 900
        while time.monotonic() < Deadline:
            Content = Log.read_text(errors="replace") if Log.exists() else ""
            if "[CarbonLuau:EntityPrivateFixture] PASS" in Content:
                if "[CarbonLuau:EntityLifetime] Private startup observer qualified" not in Content:
                    raise RuntimeError("Fixture passed without qualified startup observer")
                print("\n".join(Line for Line in Content.splitlines()
                                if "CarbonLuau:Entity" in Line or "Server startup complete" in Line),
                      flush=True)
                break
            if ("Failed compiling" in Content or "Failed to compile" in Content or
                    "[CarbonLuau:EntityPrivateFixture] FAIL" in Content):
                raise RuntimeError("Compilation/private fixture failure; log=" + str(Log))
            if Server.poll() is not None:
                raise RuntimeError("Server exited: " + str(Server.returncode))
            time.sleep(1)
        else:
            raise RuntimeError("Private fixture timeout; log=" + str(Log))
    finally:
        if Server.poll() is None:
            os.killpg(Server.pid, signal.SIGTERM)
        try:
            Server.wait(timeout=30)
        except subprocess.TimeoutExpired:
            os.killpg(Server.pid, signal.SIGKILL)
            Server.wait(timeout=10)
