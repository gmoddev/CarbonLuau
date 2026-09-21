"""Run GUI Foundation 1F lifecycle checks only in an isolated Linux Carbon worker."""
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
Artifacts = Path('/work/live-artifacts')
Build = Path('/work/build-release')
Log = Artifacts / ('gui1f-server-' + time.strftime('%Y%m%d-%H%M%S') + '.log')
Console = Artifacts / ('gui1f-console-' + time.strftime('%Y%m%d-%H%M%S') + '.log')
Library = Root / 'carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so'
Compiler = Root / 'carbon/data/CarbonLuau/native/linux-x64/carbonluau_compiler'
Socket = None
Identifier = 0


def ReadLog():
    return Log.read_text(errors='replace') if Log.exists() else ''


def WaitLog(Offset, Expected, Seconds=120):
    Deadline = time.monotonic() + Seconds
    while time.monotonic() < Deadline:
        if Expected in ReadLog()[Offset:]:
            return
        if Server.poll() is not None:
            raise RuntimeError('server exited before: ' + Expected)
        time.sleep(0.25)
    raise RuntimeError('missing current-run log: ' + Expected)


def Send(Command, Expected=''):
    global Identifier
    Identifier += 1
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'CarbonLuauGui1F'}))
    while True:
        Reply = json.loads(Socket.recv())
        if Reply.get('Identifier') == Identifier:
            Message = Reply.get('Message', '')
            if not Expected or Expected in Message:
                return Message


def RustPid():
    for Process in Path('/proc').iterdir():
        if not Process.name.isdigit():
            continue
        try:
            if (Process / 'exe').resolve() == Root / 'RustDedicated':
                return int(Process.name)
        except (FileNotFoundError, PermissionError):
            pass
    raise RuntimeError('Rust process missing')


def CompilerPids():
    Result = []
    for Process in Path('/proc').iterdir():
        if not Process.name.isdigit():
            continue
        try:
            if (Process / 'exe').resolve() == Compiler:
                Result.append(int(Process.name))
        except (FileNotFoundError, PermissionError):
            pass
    return Result


def CheckUnloaded():
    if str(Library) in (Path('/proc') / str(RustPid()) / 'maps').read_text():
        raise RuntimeError('native library remained mapped after CarbonLuau unload')
    Deadline = time.monotonic() + 10
    while CompilerPids() and time.monotonic() < Deadline:
        time.sleep(0.1)
    if CompilerPids():
        raise RuntimeError('compiler worker remained after CarbonLuau unload')


Artifacts.mkdir(parents=True, exist_ok=True)
Library.parent.mkdir(parents=True, exist_ok=True)
(Root / 'carbon/plugins').mkdir(parents=True, exist_ok=True)
shutil.copy2(Build / 'libcarbonluau_native.so', Library)
shutil.copy2(Build / 'carbonluau_compiler', Compiler)
Compiler.chmod(0o755)
shutil.copy2('/work/dist/CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
shutil.copytree('/work/dist/scripts', Root / 'carbon/data/CarbonLuau/scripts', dirs_exist_ok=True)
(Root / 'carbon/data/CarbonLuau/scripts/init.luau').write_text(
    "local S=game:GetService('Gui'):Create('ScreenGui')\n"
    "local B=S:Create('TextButton')\n"
    "B.Text='Lifecycle'\n"
    "B.Activated:Connect(function() print('unexpected client action') end)\n",
    encoding='utf-8')
ConfigPath = Root / 'carbon/configs/CarbonLuau.json'
ConfigPath.parent.mkdir(parents=True, exist_ok=True)
ConfigPath.write_text(json.dumps({
    'Enabled': True,
    'EntryScript': 'init.luau',
    'FrameDrainBudgetMilliseconds': 20,
    'MaxCallbackMilliseconds': 100,
    'MaxQueuedCallbacks': 4096,
    'MaxVmMemoryMiB': 64,
    'ModuleRoot': 'modules',
    'ScriptRoot': 'scripts',
}, indent=2) + '\n', encoding='utf-8')

Secret = uuid.uuid4().hex
with Console.open('w') as Output:
    Server = subprocess.Popen([
        'bash', 'carbon.sh', '-batchmode', '-nographics', '-logfile', str(Log),
        '+server.ip', '127.0.0.1', '+server.port', '28315', '+server.queryport', '28317',
        '+server.identity', 'carbonluau-gui1f', '+server.hostname', 'CarbonLuauGui1F',
        '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
        '+server.saveinterval', '600', '+rcon.ip', '127.0.0.1', '+rcon.port', '28316',
        '+rcon.web', '1', '+rcon.password', Secret,
    ], cwd=Root, stdout=Output, stderr=subprocess.STDOUT, start_new_session=True)
    try:
        print('[CarbonLuau:Gui1F] log: ' + str(Log), flush=True)
        WaitLog(0, 'Server startup complete', 600)
        WaitLog(0, 'Ready; generation=', 120)
        Socket = websocket.create_connection('ws://127.0.0.1:28316/' + Secret, timeout=30)
        Expected = 'GUI live registries/objects/screens/presentations/connections/actions: 1/2/1/0/1/0'
        Status = Send('carbonluau.status', Expected)
        if Expected not in Status:
            raise RuntimeError('initial GUI registry state missing: ' + Status)
        for Cycle in range(1, 11):
            Reply = Send('carbonluau.reload', 'reload OK')
            Status = Send('carbonluau.status', Expected)
            if Expected not in Status:
                raise RuntimeError('root reload leaked GUI state at cycle ' + str(Cycle) + ': ' + Status)
            print('[CarbonLuau:Gui1F] PASS root GUI reload ' + str(Cycle) + ': ' + Reply, flush=True)
        for Cycle in range(1, 11):
            Offset = len(ReadLog())
            Send('c.unload CarbonLuau')
            WaitLog(Offset, 'Unloaded plugin CarbonLuau')
            CheckUnloaded()
            if 'hostname: CarbonLuauGui1F' not in Send('status'):
                raise RuntimeError('server unresponsive after unload')
            Offset = len(ReadLog())
            Send('c.load CarbonLuau')
            WaitLog(Offset, 'Ready; generation=', 120)
            Status = Send('carbonluau.status', Expected)
            if Expected not in Status:
                raise RuntimeError('Carbon reload did not create one fresh GUI registry: ' + Status)
            print('[CarbonLuau:Gui1F] PASS Carbon unload/reload ' + str(Cycle) +
                  '; native unmapped, compiler exited, fresh GUI baseline restored', flush=True)
        print('[CarbonLuau:Gui1F] PASS Linux live lifecycle; no authenticated-client claim', flush=True)
    finally:
        if Socket:
            try:
                Send('quit')
            except (websocket.WebSocketException, json.JSONDecodeError):
                print('[CarbonLuau:Gui1F] quit sent; RCON closed during shutdown', flush=True)
            Socket.close()
        try:
            Code = Server.wait(timeout=60)
            print('[CarbonLuau:Gui1F] server exit code: ' + str(Code), flush=True)
        except subprocess.TimeoutExpired:
            os.killpg(Server.pid, signal.SIGTERM)
            Server.wait(timeout=15)
            raise RuntimeError('server required termination instead of clean quit')
