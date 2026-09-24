"""Disposable dockerbox Linux Carbon persistence qualification, not a client test.

Run only with the dedicated /data/1c-server ext4 installation and /src, /build,
/artifacts task-owned mounts. No public ports, host policy changes or DB repair.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
import websocket

Root = Path('/data/1c-server')
Source = Path('/src')
Build = Path('/build/production-linux')
Run = Path('/artifacts') / ('linux-live-' + time.strftime('%Y%m%d-%H%M%S'))
Run.mkdir(parents=True, exist_ok=False)
Data = Root / 'carbon/data/CarbonLuau'
Native = Data / 'native/linux-x64'
Scripts = Data / 'scripts'
Plugins = Root / 'carbon/plugins'
Secret = uuid.uuid4().hex
Server = None
Socket = None
Output = None
Log = None
Identifier = 0
Exits = []
Stage = 'prepare'
Failure = None


def Hash(File):
    return hashlib.sha256(File.read_bytes()).hexdigest()


def ReadLog():
    return Log.read_text(errors='replace') if Log and Log.exists() else ''


def WaitLog(Offset, Marker, Seconds=60):
    End = time.monotonic() + Seconds
    while time.monotonic() < End:
        Text = ReadLog()[Offset:]
        if 'Persistence1BLive] FAIL' in Text or 'Persistence1CLive] FAIL' in Text:
            raise RuntimeError('live script assertion failed')
        if Marker in Text:
            return
        if Server.poll() is not None:
            raise RuntimeError('server exited before ' + Marker)
        time.sleep(0.25)
    raise RuntimeError('missing marker: ' + Marker)


def Send(Command):
    global Identifier
    Identifier += 1
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'Persistence1C'}))
    End = time.monotonic() + 15
    while time.monotonic() < End:
        Reply = json.loads(Socket.recv())
        if Reply.get('Identifier') == Identifier:
            return Reply.get('Message', '')
    raise RuntimeError('RCON reply deadline')


def Ready():
    End = time.monotonic() + 45
    while time.monotonic() < End:
        Status = Send('carbonluau.status')
        if 'CarbonLuau: ready' in Status and '[CarbonLuau:Persistence] ready=True' in Status:
            assert '0.5.0-experimental' in Status and 'Native ABI: 1.5 OK' in Status
            (Run / (Stage + '-status.txt')).write_text(Status)
            return
        time.sleep(0.5)
    raise RuntimeError('storage readiness deadline')


def Start(Index):
    global Server, Socket, Output, Log
    Log = Run / ('server-' + str(Index) + '.log')
    Output = (Run / ('console-' + str(Index) + '.log')).open('w')
    # Use Carbon's installed environment, then exec so PID/maps/exit evidence
    # belongs to Rust rather than carbon.sh's waiting bash parent.
    Args = ['bash', '-c', 'source ./carbon/tools/environment.sh; exec ./RustDedicated "$@"',
            'CarbonLuauPersistence1C', '-batchmode', '-nographics', '-logfile', str(Log),
            '+server.ip', '127.0.0.1', '+server.port', '28545', '+server.queryport', '28547',
            '+server.identity', 'carbonluau-persistence1c', '+server.hostname', 'CarbonLuauPersistence1C',
            '+server.worldsize', '1000', '+server.seed', '24682', '+server.maxplayers', '1',
            '+server.saveinterval', '600', '+rcon.ip', '127.0.0.1', '+rcon.port', '28546',
            '+rcon.web', '1', '+rcon.password', Secret]
    Server = subprocess.Popen(Args, cwd=Root, stdout=Output, stderr=subprocess.STDOUT, start_new_session=True)
    WaitLog(0, 'Server startup complete', 600)
    Socket = websocket.create_connection('ws://127.0.0.1:28546/' + Secret, timeout=15)
    Ready()


def Helpers():
    Found = []
    for P in Path('/proc').iterdir():
        if P.name.isdigit():
            try:
                if str((P / 'exe').resolve()).startswith(str(Native) + '/'):
                    Found.append(int(P.name))
            except (FileNotFoundError, PermissionError):
                pass
    return Found


def Unload():
    Offset = len(ReadLog())
    Send('c.unload CarbonLuau')
    WaitLog(Offset, 'Native library unloaded successfully')
    End = time.monotonic() + 15
    while Helpers() and time.monotonic() < End:
        time.sleep(0.1)
    assert not Helpers(), 'owned helpers remain'
    # Start uses exec; this is the actual Rust process, not a wrapper shell.
    assert str(Native / 'libcarbonluau_native.so') not in Path('/proc', str(Server.pid), 'maps').read_text()


def Stop():
    global Socket, Output
    Offset = len(ReadLog())
    Send('quit')
    Socket.close()
    Socket = None
    Code = Server.wait(timeout=30)
    Text = ReadLog()[Offset:]
    assert 'Saving complete' in Text and 'Config Saved' in Text, 'host quit/save markers missing'
    assert not Helpers(), 'owned workers after quit'
    Exits.append({'pid': Server.pid, 'exitCode': Code, 'quitAcknowledged': True,
                  'saveAndConfigObserved': True, 'ownedHelpers': 0})
    Output.close()
    Output = None


def RootScript(Body, Marker):
    Text = "local S=game:GetService('DataStoreService'):GetDataStore('Persistence1BLive')\n"
    Text += 'task.defer(function()\n' + Body + '\nend)\n'
    (Scripts / 'init.luau').write_text(Text)
    Offset = len(ReadLog())
    assert 'reload OK' in Send('carbonluau.reload')
    WaitLog(Offset, Marker)


def Pair(Seed):
    Offset = len(ReadLog())
    Send('clpersistence1b.abseed' if Seed else 'clpersistence1b.abrestartread')
    for Name in ('AddonA', 'AddonB'):
        WaitLog(Offset, 'PASS ' + Name + ('Seed' if Seed else 'ReadServerRestart'))


try:
    assert (Root / 'RustDedicated').is_file()
    assert not (Data / 'persistence/store.sqlite3').exists(), 'fresh isolated fixture required; preserve earlier DB manually'
    Native.mkdir(parents=True, exist_ok=True)
    Scripts.mkdir(parents=True, exist_ok=True)
    (Scripts / 'modules').mkdir(parents=True, exist_ok=True)
    Plugins.mkdir(parents=True, exist_ok=True)
    Inputs = {}
    for Name in ('libcarbonluau_native.so', 'carbonluau_compiler', 'carbonluau_storage'):
        shutil.copy2(Build / Name, Native / Name)
        (Native / Name).chmod(0o755)
        Inputs[Name] = Hash(Native / Name)
    Package = Path('/artifacts/live-package/CarbonLuau.cszip')
    shutil.copy2(Package, Plugins / 'CarbonLuau.cszip')
    Fixture = Source / 'tests/live/CarbonLuau.Persistence1BFixture.cs'
    shutil.copy2(Fixture, Plugins / 'CarbonLuauPersistence1BFixture.cs')
    Inputs['package'] = Hash(Package)
    Inputs['fixture'] = Hash(Fixture)
    Inputs['harness'] = Hash(Path(__file__))
    (Run / 'inputs.json').write_text(json.dumps(Inputs, indent=2))
    shutil.copy2(Root / 'steamapps/appmanifest_258550.acf', Run / 'rust-manifest.acf')
    (Scripts / 'init.luau').write_text('return true')
    Stage = 'first-start'
    Start(1)
    Stage = 'root-seed'
    RootScript("""
local V={Owner='Root',Coins=12}
S:SetAsync('Retained',V,function(OK,E)
 assert(OK==true and E==nil,'[CarbonLuau:Persistence1CLive] FAIL Set')
 S:SetAsync('Temporary',true,function(OK2,E2)
  assert(OK2==true and E2==nil)
  S:RemoveAsync('Temporary',function(Removed,E3)
   assert(Removed==true and E3==nil)
   S:GetAsync('Temporary',function(Missing,E4)
    assert(Missing==nil and E4==nil)
    S:GetAsync('Retained',function(R,E5)
     assert(E5==nil and R.Owner=='Root' and R.Coins==12)
     print('[CarbonLuau:Persistence1CLive] PASS RootSeed')
    end)
   end)
  end)
 end)
end)
V.Owner='changed after submission'
""", 'PASS RootSeed')
    Stage = 'addon-seed'
    Pair(True)
    Stage = 'host-unload'
    Unload()
    (Scripts / 'init.luau').write_text('return true')
    Stage = 'host-reload'
    Offset = len(ReadLog())
    Send('c.load CarbonLuau')
    WaitLog(Offset, '[CarbonLuau:Runtime] Ready; generation=')
    Ready()
    Read = "S:GetAsync('Retained',function(V,E) assert(E==nil and V.Owner=='Root' and V.Coins==12); print('[CarbonLuau:Persistence1CLive] PASS RootRead') end)"
    RootScript(Read, 'PASS RootRead')
    Stage = 'server-stop'
    (Scripts / 'init.luau').write_text('return true')
    Unload()
    Stop()
    Database = Data / 'persistence/store.sqlite3'
    Before = Hash(Database)
    Stage = 'server-restart'
    Start(2)
    Idle = Send('carbonluau.status')
    assert 'sent=0;' in Idle
    RootScript(Read, 'PASS RootRead')
    Pair(False)
    Status = Send('carbonluau.status')
    assert 'sent=3;' in Status and 'completed_get=3;' in Status and 'completed_set=0;' in Status
    assert 'pending=0;' in Status and 'failure=None' in Status
    (Run / 'restart-status.txt').write_text(Status)
    Stage = 'final-unload'
    Unload()
    Stop()
    (Run / 'receipt.json').write_text(json.dumps({'verdict': 'PASS', 'exits': Exits,
        'databaseAfterFirstStop': Before, 'restartReadCount': 3, 'restartMutationCount': 0,
        'noAuthenticatedClient': True, 'powerLossClaim': False,
        'allocatedBytesAfterStop': sum(P.stat().st_blocks * 512 for P in (Data / 'persistence').iterdir())}, indent=2))
    print('[CarbonLuau:Persistence1C] Linux live root/addon Set/Get/Remove, host reload, actual server restart and teardown PASS', flush=True)
except Exception as Error:
    Failure = {'stage': Stage, 'errorType': type(Error).__name__}
    (Run / 'failure.json').write_text(json.dumps(Failure))
    print('[CarbonLuau:Persistence1C] Linux live FAIL stage=' + Stage + ' type=' + type(Error).__name__, flush=True)
finally:
    if Server and Server.poll() is None:
        try:
            if Socket:
                Send('quit')
            Server.wait(timeout=30)
        except Exception:
            Server.kill()
            Server.wait(timeout=10)
    if Socket:
        Socket.close()
    if Output:
        Output.close()
    for File in Run.glob('*.log'):
        File.write_text(File.read_text(errors='replace').replace(Secret, '[REDACTED]'))
    (Run / 'cleanup.json').write_text(json.dumps({'serverExited': Server is None or Server.poll() is not None,
                                                'remainingOwnedHelpers': Helpers()}))
if Failure or Helpers():
    raise SystemExit(1)
