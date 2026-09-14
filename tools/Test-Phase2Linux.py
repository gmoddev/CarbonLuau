"""Phase 2 fixture, only in the authorized isolated Docker worker container."""
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
Log = Artifacts / ('phase2-server-linux-' + RunId + '.log')
Library = Root / 'carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so'
Library.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2('/work/linux-x64/libcarbonluau_native.so', Library)
shutil.copy2(Artifacts / 'phase2-release/CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
shutil.copytree(Artifacts / 'phase2-release/scripts', Root / 'carbon/data/CarbonLuau/scripts', dirs_exist_ok=True)
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
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'CarbonLuauPhase2'}))
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

with (Artifacts / ('phase2-console-linux-' + RunId + '.log')).open('w') as Output:
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

        Healthy = """local Message=require('message'); print(Message); task.defer(function() print('deferred callback') end); task.delay(0.1,function() print('delayed callback') end)"""
        Entry = Root / 'carbon/data/CarbonLuau/scripts/init.luau'
        Module = Root / 'carbon/data/CarbonLuau/scripts/modules/message.luau'
        def Source(Text):
            Entry.write_text(Text, encoding='utf-8')
        def Reload(Text, Expected='reload OK'):
            Source(Text)
            Expect('carbonluau.reload', Expected)
            Expect('status', 'hostname: CarbonLuauPhase0')
        WaitLog(0, 'CarbonLuau Phase 2 module loading works')
        WaitLog(0, 'deferred callback')
        WaitLog(0, 'delayed callback')
        Expect('carbonluau.status', 'CarbonLuau: ready')
        Offset = len(ReadLog())
        Reload("task.delay(2,function() print('old queue preserved') end)")
        Reload("local =", 'reload COMPILE_ERROR')
        Module.write_text("local =", encoding='utf-8')
        Reload("require('message')", 'reload RUNTIME_ERROR')
        Module.write_text("error('intentional module failure')", encoding='utf-8')
        Reload("require('message')", 'reload RUNTIME_ERROR')
        Module.write_text('return "CarbonLuau Phase 2 module loading works"', encoding='utf-8')
        Reload("task.defer(function() print('candidate leaked') end); error('entry failure')", 'reload RUNTIME_ERROR')
        WaitLog(Offset, 'old queue preserved')
        if 'candidate leaked' in ReadLog()[Offset:]:
            raise RuntimeError('Rejected candidate callback escaped')
        Offset = len(ReadLog())
        Reload("task.delay(2,function() print('stale callback executed') end)")
        Reload(Healthy)
        time.sleep(3)
        if 'stale callback executed' in ReadLog()[Offset:]:
            raise RuntimeError('Old callback survived successful reload')
        Offset = len(ReadLog())
        Reload("task.defer(function() error('intentional callback failure') end); task.defer(function() print('callback error survived') end)")
        WaitLog(Offset, 'callback error survived')
        WaitLog(Offset, 'intentional callback failure')
        Offset = len(ReadLog())
        Reload("print('recovery entry ran'); task.delay(1,function() while true do end end); task.delay(100,function() print('retired stale callback') end)")
        WaitLog(Offset, 'callback deadline exceeded')
        Deadline = time.monotonic() + 15
        while time.monotonic() < Deadline:
            Reply = Send('carbonluau.status', 'CarbonLuau:')
            if 'CarbonLuau: unavailable' in Reply and 'automatic recovery exhausted' in Reply:
                print(Reply, flush=True)
                break
            time.sleep(0.25)
        else:
            raise RuntimeError('Second timeout did not latch unavailable')
        if ReadLog()[Offset:].count('recovery entry ran') != 2:
            raise RuntimeError('Entrypoint was not reconstructed exactly once')
        Expect('status', 'hostname: CarbonLuauPhase0')
        Reload(Healthy)
        Expect('carbonluau.status', 'CarbonLuau: ready')
        # An entry snapshot failure during automatic recovery must stay unavailable.
        Offset = len(ReadLog())
        Reload("task.delay(1,function() while true do end end)")
        Source('local =')
        WaitLog(Offset, 'callback deadline exceeded')
        time.sleep(1)
        Expect('carbonluau.status', 'automatic recovery failed')
        Reload(Healthy)
        for Cycle in range(1, 11):
            Reload("for I=1,100 do task.delay(100,function() print('old runtime work') end) end")
            Reload(Healthy)
            Expect('carbonluau.status', 'CarbonLuau: ready')
            print(f'[CarbonLuau:LiveTest] PASS runtime cycle {Cycle}; cancelled 100 delayed callbacks', flush=True)
        for Cycle in range(1, 11):
            Reload("task.delay(2,function() print('old plugin work') end)")
            Unload()
            Source(Healthy)
            Offset = len(ReadLog())
            Load()
            WaitLog(Offset, 'deferred callback')
            WaitLog(Offset, 'delayed callback')
            Expect('carbonluau.status', 'CarbonLuau: ready')
            print(f'[CarbonLuau:LiveTest] PASS plugin cycle {Cycle}; library unmapped during unload', flush=True)
        time.sleep(3)
        Text = ReadLog()
        if 'old plugin work' in Text or 'old runtime work' in Text or 'retired stale callback' in Text:
            raise RuntimeError('Stale scheduled callback executed')
        Expect('status', 'hostname: CarbonLuauPhase0')
        print('[CarbonLuau:LiveTest] PASS Linux Phase 2 production package; module/errors/recovery; 10 runtime cancellation cycles and 10 plugin cycles', flush=True)
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
