"""Phase 1 fixture, only in the authorized isolated Docker worker container."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
import websocket

Root = Path('/work/server-linux')
Artifacts = Path('/artifacts')
RunId = time.strftime('%Y%m%d-%H%M%S')
Log = Artifacts / ('phase1-server-linux-' + RunId + '.log')
Library = Root / 'carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so'
Library.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2('/work/linux-x64/libcarbonluau_native.so', Library)
shutil.copy2(Artifacts / 'phase1-test/CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
Secret = uuid.uuid4().hex
Socket = None
Identifier = 0

def ReadLog():
    return Log.read_text(errors='replace') if Log.exists() else ''

def WaitLog(Offset, Expected, Seconds=30):
    Deadline = time.monotonic() + Seconds
    while time.monotonic() < Deadline:
        if Expected in ReadLog()[Offset:]:
            return
        if Server.poll() is not None:
            raise RuntimeError('Server exited before ' + Expected)
        time.sleep(0.5)
    raise RuntimeError('Missing current-run log: ' + Expected)

def Send(Command, Expected=''):
    global Identifier
    Identifier += 1
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'CarbonLuauPhase1'}))
    while True:
        Reply = json.loads(Socket.recv())
        if Reply.get('Identifier') == Identifier:
            Message = Reply.get('Message', '')
            if not Expected or Expected in Message or '[CarbonLuau:LiveTest] FAIL:' in Message:
                return Message

def Expect(Command, Expected):
    Reply = Send(Command, Expected)
    if Expected not in Reply:
        raise RuntimeError(Command + ' returned: ' + Reply)
    print(Reply, flush=True)

def Unload():
    Offset = len(ReadLog())
    Send('c.unload CarbonLuau')
    WaitLog(Offset, 'Native library unloaded successfully')
    for Process in Path('/proc').iterdir():
        if Process.name.isdigit():
            try:
                if (Process / 'exe').resolve() == Root / 'RustDedicated':
                    if str(Library) in (Process / 'maps').read_text():
                        raise RuntimeError('Library remains mapped after unload')
                    return
            except (FileNotFoundError, PermissionError):
                continue
    raise RuntimeError('Rust process missing')

def Load():
    Offset = len(ReadLog())
    Send('c.load CarbonLuau')
    WaitLog(Offset, '[CarbonLuau:Runtime] Ready; generation=1')

with (Artifacts / ('phase1-console-linux-' + RunId + '.log')).open('w') as Output:
    Server = subprocess.Popen([
        'bash', 'carbon.sh', '-batchmode', '-nographics', '-logfile', str(Log),
        '+server.ip', '127.0.0.1', '+server.port', '28115', '+server.queryport', '28117',
        '+server.identity', 'carbonluau-phase0', '+server.hostname', 'CarbonLuauPhase0',
        '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
        '+server.saveinterval', '600', '+rcon.ip', '127.0.0.1', '+rcon.port', '28116',
        '+rcon.web', '1', '+rcon.password', Secret,
    ], cwd=Root, stdout=Output, stderr=subprocess.STDOUT, start_new_session=True)
    try:
        print('[CarbonLuau:LiveTest] Linux current-run log: ' + str(Log), flush=True)
        WaitLog(0, 'Server startup complete', 600)
        WaitLog(0, 'hello from Luau')
        WaitLog(0, '[CarbonLuau:Runtime] Ready; generation=1')
        Socket = websocket.create_connection('ws://127.0.0.1:28116/' + Secret, timeout=15)
        Expect('carbonluau.status', 'CarbonLuau: ready')
        for Fixture in ['timeout', 'valid', 'memory', 'valid', 'failed-reload', 'valid']:
            Expect('carbonluau.fixture ' + Fixture, 'PASS ' + Fixture)
            Expect('status', 'hostname: CarbonLuauPhase0')
        for Cycle in range(10):
            Expect('carbonluau.reload', 'reload OK')
            Expect('carbonluau.status', 'CarbonLuau: ready')
        for Cycle in range(1, 11):
            Unload()
            Load()
            Expect('carbonluau.status', 'CarbonLuau: ready')
            print(f'[CarbonLuau:LiveTest] PASS plugin cycle {Cycle}; SO unmapped during unload', flush=True)
        Unload()
        shutil.copy2(Artifacts / 'CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
        # Let host watcher requests settle before an explicit serialized reload.
        time.sleep(5)
        Offset = len(ReadLog())
        Send('c.load CarbonLuau')
        WaitLog(Offset, '[CarbonLuau:Runtime] Ready; generation=1')
        Expect('carbonluau.status', 'CarbonLuau: ready')
        Expect('carbonluau.reload', 'reload OK')
        print('[CarbonLuau:LiveTest] PASS Linux Phase 1; 10 runtime replacements, 10 plugin cycles, production package smoke/reload', flush=True)
    finally:
        if Socket:
            try:
                Send('quit')
            except (websocket.WebSocketException, json.JSONDecodeError):
                print('[CarbonLuau:LiveTest] quit sent; RCON closed during shutdown', flush=True)
            finally:
                Socket.close()
        try:
            Code = Server.wait(timeout=45)
            print('[CarbonLuau:LiveTest] Server exit code: ' + str(Code), flush=True)
        except subprocess.TimeoutExpired:
            import signal
            os.killpg(Server.pid, signal.SIGTERM)
            Server.wait(timeout=15)
            raise RuntimeError('Server required termination instead of clean quit')
