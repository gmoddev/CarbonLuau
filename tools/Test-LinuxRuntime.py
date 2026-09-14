"""Run only inside the disposable CarbonLuau Linux test container.

Requires python3-websocket, installed from Ubuntu packages.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
import websocket

Root = Path('/work/server-linux')
Build = Path('/work/linux-x64')
Artifacts = Path('/artifacts')
Log = Artifacts / 'server-linux.log'
Library = Root / 'carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so'
Good = Build / 'libcarbonluau_native.so'
Library.parent.mkdir(parents=True, exist_ok=True)
(Root / 'carbon/plugins').mkdir(parents=True, exist_ok=True)
shutil.copy2(Good, Library)
shutil.copy2(Artifacts / 'CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
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
            raise RuntimeError('Server exited before: ' + Expected)
        time.sleep(0.5)
    raise RuntimeError('Missing log: ' + Expected)


def Send(Command):
    global Identifier
    Identifier += 1
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'CarbonLuauPhase0'}))
    while True:
        Reply = json.loads(Socket.recv())
        if Reply.get('Identifier') == Identifier:
            return Reply.get('Message', '')


def Health():
    Reply = Send('status')
    if 'hostname: CarbonLuauPhase0' not in Reply:
        raise RuntimeError('Unexpected status: ' + Reply)
    print('[CarbonLuau:LiveTest] Server status responsive', flush=True)


def Unload():
    Offset = len(ReadLog())
    Send('c.unload CarbonLuau')
    WaitLog(Offset, 'Unloaded plugin CarbonLuau')
    for Process in Path('/proc').iterdir():
        if Process.name.isdigit():
            try:
                if (Process / 'exe').resolve() == Root / 'RustDedicated':
                    if str(Library) in (Process / 'maps').read_text():
                        raise RuntimeError('Library still mapped after unload')
                    return
            except (FileNotFoundError, PermissionError):
                continue
    raise RuntimeError('Rust process missing')


def Load(Expected='Native probe loaded successfully'):
    Offset = len(ReadLog())
    Send('c.load CarbonLuau')
    WaitLog(Offset, Expected)


with (Artifacts / 'server-linux-console.log').open('w') as Output:
    Server = subprocess.Popen([
        'bash', 'carbon.sh', '-batchmode', '-nographics', '-logfile', str(Log),
        '+server.ip', '127.0.0.1', '+server.port', '28115', '+server.queryport', '28117',
        '+server.identity', 'carbonluau-phase0', '+server.hostname', 'CarbonLuauPhase0',
        '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
        '+server.saveinterval', '600', '+rcon.ip', '127.0.0.1', '+rcon.port', '28116',
        '+rcon.web', '1', '+rcon.password', Secret,
    ], cwd=Root, stdout=Output, stderr=subprocess.STDOUT, start_new_session=True)
    try:
        print('[CarbonLuau:LiveTest] Waiting for Linux Carbon startup', flush=True)
        WaitLog(0, 'Server startup complete', 600)
        WaitLog(0, 'Native probe loaded successfully')
        Socket = websocket.create_connection('ws://127.0.0.1:28116/' + Secret, timeout=15)
        Health()
        for Cycle in range(1, 11):
            Unload()
            shutil.copy2(Good, Library)
            Load()
            print(f'[CarbonLuau:LiveTest] PASS cycle {Cycle}; SO absent from process maps during unload', flush=True)
        for Fixture, Expected in [
            ('Missing', 'native library missing'), ('Broken', 'native library load failed'),
            ('MissingSymbol', 'symbol missing'), ('WrongProbe', 'probe magic mismatch'),
        ]:
            Unload()
            if Fixture == 'Missing':
                Library.unlink()
            elif Fixture == 'Broken':
                Library.write_text('invalid native image')
            else:
                shutil.copy2(Build / ('lib' + Fixture + '.so'), Library)
            Load(Expected)
            Health()
            Unload()
            shutil.copy2(Good, Library)
            Load()
            print(f'[CarbonLuau:LiveTest] PASS {Fixture} failure and recovery', flush=True)
        Offset = len(ReadLog())
        Send('c.reload CarbonLuau')
        WaitLog(Offset, 'Native probe loaded successfully')
        Health()
        print('[CarbonLuau:LiveTest] PASS Linux runtime validation', flush=True)
    finally:
        if Socket:
            try:
                Send('quit')
            finally:
                Socket.close()
        try:
            Server.wait(timeout=45)
        except subprocess.TimeoutExpired:
            # This isolated process group belongs to this test only.
            import signal
            os.killpg(Server.pid, signal.SIGTERM)
            Server.wait(timeout=15)
