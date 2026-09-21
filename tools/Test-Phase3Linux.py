"""Isolated worker only: actual Carbon/Rust types, no real-client session claim."""
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import time
import uuid
import websocket

Root = Path('/work/server-linux')
Artifacts = Path('/artifacts')
RunId = time.strftime('%Y%m%d-%H%M%S')
Log = Artifacts / ('phase3-server-linux-' + RunId + '.log')
Library = Root / 'carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so'
Compiler = Root / 'carbon/data/CarbonLuau/native/linux-x64/carbonluau_compiler'
Library.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2('/work/linux-x64/libcarbonluau_native.so', Library)
shutil.copy2('/work/linux-x64/carbonluau_compiler', Compiler)
Compiler.chmod(0o755)
shutil.copy2(Artifacts / 'phase3-fixture/CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
shutil.copytree(Artifacts / 'phase3-fixture/scripts', Root / 'carbon/data/CarbonLuau/scripts', dirs_exist_ok=True)
Secret = uuid.uuid4().hex
Socket = None
Identifier = 0

def ReadLog():
    return Log.read_text(errors='replace') if Log.exists() else ''

def WaitLog(Offset, Expected, Seconds=60):
    Deadline = time.monotonic() + Seconds
    while time.monotonic() < Deadline:
        Text = ReadLog()[Offset:]
        if '[CarbonLuau:HostFixture] FAIL' in Text:
            raise RuntimeError('Current fixture failed: ' + Text)
        if Expected in Text:
            return
        if Server.poll() is not None:
            raise RuntimeError('Server exited before ' + Expected)
        time.sleep(0.25)
    raise RuntimeError('Missing current-run log: ' + Expected)

def Send(Command, Expected=''):
    global Identifier
    Identifier += 1
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'CarbonLuauPhase3'}))
    while True:
        Reply = json.loads(Socket.recv())
        if Reply.get('Identifier') == Identifier:
            Message = Reply.get('Message', '')
            if not Expected or Expected in Message:
                return Message

def CheckUnmapped():
    for Process in Path('/proc').iterdir():
        if Process.name.isdigit():
            try:
                if (Process / 'exe').resolve() == Root / 'RustDedicated':
                    if str(Library) in (Process / 'maps').read_text():
                        raise RuntimeError('Native library remained mapped')
                    return
            except (FileNotFoundError, PermissionError):
                continue
    raise RuntimeError('Rust process missing')

with (Artifacts / ('phase3-console-linux-' + RunId + '.log')).open('w') as Output:
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
        Socket = websocket.create_connection('ws://127.0.0.1:28116/' + Secret, timeout=15)
        for Cycle in range(1, 4):
            print(Send('carbonluau.status', 'CarbonLuau: ready'), flush=True)
            Offset = len(ReadLog())
            print(Send('carbonluau.phase3fixture', 'CarbonLuau Phase3 fixture scheduled'), flush=True)
            WaitLog(Offset, 'PASS teardown: owned chat registrations removed and native library released')
            Text = ReadLog()[Offset:]
            for Expected in ['PASS live Health/MaxHealth', 'PASS actual Carbon command dispatch', 'PASS A -> rejected B -> committed C', 'PASS reconnect, D10', 'production NextFrame event']:
                if Expected not in Text:
                    raise RuntimeError('Incomplete fixture evidence: ' + Expected)
            CheckUnmapped()
            print(f'[CarbonLuau:LiveTest] PASS Linux Phase 3 cycle {Cycle}; real host types, 100 command replacements, native unmapped', flush=True)
            print(Send('status', 'hostname: CarbonLuauPhase0'), flush=True)
            Send('c.unload CarbonLuau')
            if Cycle < 3:
                Offset = len(ReadLog())
                Send('c.load CarbonLuau')
                WaitLog(Offset, 'Ready; generation=')
        print('[CarbonLuau:LiveTest] PASS Linux Phase 3: 3 controlled-host cycles; no real-client delivery claim', flush=True)
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
            os.killpg(Server.pid, signal.SIGTERM)
            Server.wait(timeout=15)
            raise RuntimeError('Server required termination instead of clean quit')
