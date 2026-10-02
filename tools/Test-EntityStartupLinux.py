"""Run the startup-order fixture in a task-owned isolated Carbon copy."""
import argparse
import os
import pathlib
import shutil
import signal
import subprocess
import time


Parser = argparse.ArgumentParser()
Parser.add_argument("--work", required=True)
Parser.add_argument("--identity", default="entity1a-startup")
Parser.add_argument("--hotload", action="store_true")
Args = Parser.parse_args()
Work = pathlib.Path(Args.work).resolve()
Root = Work / "server-linux"
Evidence = Work / "evidence" / (("hotload-" if Args.hotload else "startup-") +
                                time.strftime("%Y%m%d-%H%M%S"))
Evidence.mkdir(parents=True, exist_ok=False)
Log = Evidence / "server.log"
Fixture = Root / "carbon/plugins/CarbonLuau.EntityStartupEvidence.cs"
Staged = Work / "hotload-fixture.cs"
if Args.hotload:
    if Fixture.exists() or not Staged.is_file():
        raise RuntimeError("Hotload requires an absent plugin path and staged fixture")

with (Evidence / "console.log").open("w") as Console:
    Server = subprocess.Popen(
        ["bash", "carbon.sh", "-batchmode", "-nographics", "-logfile", str(Log),
         "+server.ip", "127.0.0.1", "+server.port", "28335", "+server.queryport", "28337",
         "+server.identity", Args.identity, "+server.hostname", "CarbonLuauEntityStartupResearch",
         "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
         "+server.saveinterval", "600", "+rcon.port", "0"],
        cwd=Root, stdout=Console, stderr=subprocess.STDOUT, start_new_session=True,
    )
    try:
        Deadline = time.monotonic() + 900
        Hotloaded = False
        while time.monotonic() < Deadline:
            Text = Log.read_text(errors="replace") if Log.exists() else ""
            if Args.hotload and not Hotloaded and "Server startup complete" in Text:
                shutil.copy2(Staged, Fixture)
                Hotloaded = True
            Marker = ("[CarbonLuau:EntityStartup] HOTLOAD_UNQUALIFIED" if Args.hotload
                      else "[CarbonLuau:EntityStartup] PASS")
            if Marker in Text:
                print("\n".join(Line for Line in Text.splitlines()
                                if "EntityStartup" in Line or "Spawning World" in Line
                                or "Server startup complete" in Line or "Loaded plugin" in Line))
                break
            if ("Failed compiling" in Text or "Failed to compile" in Text
                    or "[CarbonLuau:EntityStartup] SAVE_FAILED" in Text):
                raise RuntimeError("Fixture compilation failed: " + str(Log))
            if Server.poll() is not None:
                raise RuntimeError("Server exited: " + str(Server.returncode))
            time.sleep(1)
        else:
            raise RuntimeError("Fixture timeout: " + str(Log))
    finally:
        if Server.poll() is None:
            os.killpg(Server.pid, signal.SIGTERM)
        try:
            Server.wait(timeout=30)
        except subprocess.TimeoutExpired:
            os.killpg(Server.pid, signal.SIGKILL)
            Server.wait(timeout=10)
