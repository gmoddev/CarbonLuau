"""Research runner: ONLY a new task-owned copy, in Docker with no published ports."""
import argparse
import pathlib
import subprocess
import time

Parser = argparse.ArgumentParser()
Parser.add_argument('--work', required=True)
Args = Parser.parse_args()
Work = pathlib.Path(Args.work).resolve()
Root = Work / 'runtime/server-linux'
Evidence = Work / 'evidence' / ('lifecycle-live-' + time.strftime('%Y%m%d-%H%M%S'))
Evidence.mkdir(parents=True, exist_ok=False)
Log = Evidence / 'server.log'
with (Evidence / 'console.log').open('w') as Console:
    Server = subprocess.Popen(['bash', 'carbon.sh', '-batchmode', '-nographics',
        '-logfile', str(Log), '+server.ip', '127.0.0.1', '+server.port', '28315',
        '+server.queryport', '28317', '+server.identity', 'carbonluau-g1',
        '+server.hostname', 'CarbonLuauEntityResearch', '+server.worldsize', '1000',
        '+server.seed', '13579', '+server.maxplayers', '1', '+server.saveinterval', '600',
        '+rcon.port', '0'], cwd=Root, stdout=Console, stderr=subprocess.STDOUT,
        start_new_session=True)
    try:
        Deadline = time.monotonic() + 900
        while time.monotonic() < Deadline:
            Text = Log.read_text(errors='replace') if Log.exists() else ''
            if '[CarbonLuau:EntityLive] FAIL' in Text or 'Failed compiling' in Text:
                raise RuntimeError('Fixture failed: ' + str(Log))
            if '[CarbonLuau:EntityLive] PASS' in Text:
                print('\n'.join(Line for Line in Text.splitlines() if 'EntityLive' in Line))
                break
            if Server.poll() is not None:
                raise RuntimeError('Server exited: ' + str(Server.returncode))
            time.sleep(1)
        else:
            raise RuntimeError('Fixture timeout: ' + str(Log))
    finally:
        # Own isolated process group only; never discover/kill unrelated servers.
        import os
        import signal
        os.killpg(Server.pid, signal.SIGTERM) if Server.poll() is None else None
        try:
            Server.wait(timeout=30)
        except subprocess.TimeoutExpired:
            os.killpg(Server.pid, signal.SIGKILL)
            Server.wait(timeout=10)
