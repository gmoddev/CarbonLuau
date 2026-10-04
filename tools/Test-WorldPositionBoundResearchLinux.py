"""Task-owned exact-host position inspection, not timing-as-a-bound qualification."""
import argparse
import pathlib
import shutil
import signal
import subprocess
import time

Parser = argparse.ArgumentParser()
Parser.add_argument("--work", required=True)
Parser.add_argument("--research", choices=("Bound", "Phase", "Access"), default="Bound")
Args = Parser.parse_args()
Work = pathlib.Path(Args.work).resolve()
if Work != pathlib.Path("/root/codex/world-movement-20261003"):
    raise RuntimeError("Expected preserved task-owned exact-host research copy")
Root = Work / "server-linux"
Fixture = "CarbonLuau.WorldPosition" + Args.research + "Research.cs"
Marker = "Position" + Args.research + "Research"
Source = Work / Fixture
Target = Root / "carbon/plugins" / Fixture
if not Source.is_file() or not (Root / "RustDedicated").is_file() or Target.exists():
    raise RuntimeError("Fixture/server ownership precondition failed")
if subprocess.run(["pgrep", "-x", "RustDedicated"], capture_output=True).returncode == 0:
    raise RuntimeError("Existing RustDedicated protected")
Evidence = Work / "evidence" / ("position-" + Args.research.lower() + "-" + time.strftime("%Y%m%d-%H%M%S"))
Evidence.mkdir(exist_ok=False)
Log = Evidence / "server.log"
Server = None
try:
    shutil.copy2(Source, Target)
    with (Evidence / "console.log").open("w") as Console:
        Server = subprocess.Popen(["bash", "carbon.sh", "-batchmode", "-nographics", "-logfile", str(Log),
            "+server.ip", "127.0.0.1", "+server.port", "28335", "+server.queryport", "28337",
            "+server.identity", "entity1a-adapter-check", "+server.hostname", "PositionResearch",
            "+server.worldsize", "1000", "+server.seed", "13579", "+server.maxplayers", "1",
            "+server.saveinterval", "600", "+rcon.port", "0"], cwd=Root, stdout=Console,
            stderr=subprocess.STDOUT, start_new_session=True)
        Server.wait(timeout=600)
    Content = Log.read_text(errors="replace") if Log.exists() else ""
    print("\n".join(Line for Line in Content.splitlines() if Marker in Line or "Server startup complete" in Line), flush=True)
    Passed = any("[CarbonLuau:" + Marker + "] OBSERVATION_PASS" in Line and "NOT_HARD_BOUND" in Line
                 for Line in Content.splitlines())
    if not Passed or Marker + "] FAIL" in Content:
        raise RuntimeError("Position research evidence failed: " + str(Log))
    print("[CarbonLuau:" + Marker + "] EVIDENCE " + str(Log))
finally:
    if Server is not None and Server.poll() is None:
        subprocess.run(["kill", "-TERM", "--", "-" + str(Server.pid)], check=False)
        try:
            Server.wait(timeout=30)
        except subprocess.TimeoutExpired:
            import os
            os.killpg(Server.pid, signal.SIGKILL)
            Server.wait(timeout=10)
    if Target.exists():
        Target.unlink()
    print("[CarbonLuau:" + Marker + "] CLEANUP owned process/fixture only", flush=True)
