"""Isolated task-owned server only; run in a disposable container without published ports."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
import websocket

Parser = argparse.ArgumentParser()
Parser.add_argument('--work', default='/work')
Args = Parser.parse_args()
Work = Path(Args.work)
Root = Work / 'runtime/server-linux'
Evidence = Work / 'evidence' / ('live-production-' + time.strftime('%Y%m%d-%H%M%S'))
Evidence.mkdir(parents=True, exist_ok=False)
Log = Evidence / 'server.log'
Data = Root / 'carbon/data/CarbonLuau'
Native = Data / 'native/linux-x64'
Native.mkdir(parents=True, exist_ok=True)
for Name in ['libcarbonluau_native.so', 'carbonluau_compiler']:
    shutil.copy2(Work / 'build/release' / Name, Native / Name)
(Data / 'scripts').mkdir(parents=True, exist_ok=True)
(Data / 'scripts/modules').mkdir(parents=True, exist_ok=True)
(Data / 'scripts/init.luau').write_text('-- Isolated Player-1F-B qualification root\n')
# Cold-module regression for the Player-1F-C publication-context correction.
for Name, Mutation in {
    'give': 'P:GiveItem("scrap",1)',
    'take': 'P:TakeItem("scrap",1)',
    'teleport': 'P:Teleport(Vector3.new(1,2,3))',
}.items():
    (Data / 'scripts/modules' / ('player1fc' + Name + '.luau')).write_text(
        'local P=game:GetService("Players"):GetPlayers()[1]; assert(P~=nil); '
        + Mutation + '; error("module failed after mutation")\n')
OldFixture = Root / 'carbon/plugins/CarbonLuau.GiveItemG1Evidence.cs'
if OldFixture.exists():
    shutil.move(str(OldFixture), str(Evidence / OldFixture.name))
shutil.copy2(Work / 'tools/CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
Secret = uuid.uuid4().hex
Console = (Evidence / 'console.log').open('w')
Server = subprocess.Popen(['bash', 'carbon.sh', '-batchmode', '-nographics', '-logfile', str(Log),
    '+server.ip', '127.0.0.1', '+server.port', '28315', '+server.queryport', '28317',
    '+server.identity', 'carbonluau-g1', '+server.hostname', 'CarbonLuau1FB',
    '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
    '+server.saveinterval', '600', '+rcon.ip', '127.0.0.1', '+rcon.port', '28316',
    '+rcon.web', '1', '+rcon.password', Secret], cwd=Root, stdout=Console, stderr=subprocess.STDOUT)
Socket = None

def Wait(Marker, Seconds=180, Offset=0):
    End = time.monotonic() + Seconds
    while time.monotonic() < End:
        Text = Log.read_text(errors='replace')[Offset:] if Log.exists() else ''
        if '[CarbonLuau:Player1FBLive] FAIL' in Text or 'Failed compiling' in Text or '[bootstrap] INTERNAL_ERROR' in Text:
            raise RuntimeError('Fixture/compile failure; inspect ' + str(Log))
        if Marker in Text:
            return
        if Server.poll() is not None:
            raise RuntimeError('Server exited: ' + str(Server.returncode))
        time.sleep(1)
    raise RuntimeError('Timeout: ' + Marker)

def Command(Text):
    Socket.send(json.dumps({'Identifier': 1, 'Message': Text, 'Name': 'Player1FBFixture'}))

try:
    Wait('Server startup complete', 900)
    Socket = websocket.create_connection('ws://127.0.0.1:28316/' + Secret, timeout=10)
    Wait('[CarbonLuau:Runtime] Ready;')
    for Cycle in range(2):
        Offset = len(Log.read_text(errors='replace'))
        Command('carbonluau.player1fbfixture')
        Wait('[CarbonLuau:Player1FBLive] PASS production', Offset=Offset)
        Command('c.unload CarbonLuau')
        Wait('[CarbonLuau:Native] Native library unloaded successfully.', Offset=Offset)
        # Native mapping absence independently verifies loader teardown.
        time.sleep(1)
        RustPids = subprocess.check_output(['pgrep', '-x', 'RustDedicated'], text=True).split()
        for Pid in RustPids:
            if 'libcarbonluau_native.so' in Path('/proc', Pid, 'maps').read_text():
                raise RuntimeError('Native mapping survived unload')
        print('[CarbonLuau:Player1FBRunner] Cycle ' + str(Cycle + 1) + ' PASS including native unmap', flush=True)
        if Cycle == 0:
            Offset = len(Log.read_text(errors='replace'))
            Command('c.load CarbonLuau')
            Wait('[CarbonLuau:Runtime] Ready;', Offset=Offset)
finally:
    if Socket:
        Command('quit')
        Socket.close()
    try:
        Server.wait(timeout=45)
    except subprocess.TimeoutExpired:
        Server.terminate()
        try:
            Server.wait(timeout=15)
        except subprocess.TimeoutExpired:
            Server.kill()
    Console.close()
