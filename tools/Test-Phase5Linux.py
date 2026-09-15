"""Run Phase 5 only in the isolated, resource-capped CarbonLuau Linux container."""
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import time
import uuid

import websocket


Root = Path('/work/server-linux')
Artifacts = Path('/work/artifacts')
Build = Path('/build')
RunId = time.strftime('%Y%m%d-%H%M%S')
Log = Artifacts / ('phase5-server-linux-' + RunId + '.log')
Console = Artifacts / ('phase5-console-linux-' + RunId + '.log')
Samples = Artifacts / ('phase5-samples-linux-' + RunId + '.jsonl')
RunnerLog = Artifacts / ('phase5-runner-linux-' + RunId + '.log')
Library = Root / 'carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so'
Fixture = Artifacts / 'phase5-fixture'
LifecycleCycles = int(os.environ.get('PHASE5_LIFECYCLE_CYCLES', '10'))
SoakMinutes = int(os.environ.get('PHASE5_SOAK_MINUTES', '30'))
ProfilerSeconds = int(os.environ.get('PHASE5_PROFILER_SECONDS', '10'))
Socket = None
Identifier = 0


class Tee:
    def __init__(self, *Streams):
        self.Streams = Streams

    def write(self, Text):
        for Stream in self.Streams:
            Stream.write(Text)
        return len(Text)

    def flush(self):
        for Stream in self.Streams:
            Stream.flush()


RunnerOutput = RunnerLog.open('w', encoding='utf-8')
sys.stdout = Tee(sys.stdout, RunnerOutput)
sys.stderr = Tee(sys.stderr, RunnerOutput)


def ReadLog():
    return Log.read_text(errors='replace') if Log.exists() else ''


def WaitLog(Offset, Expected, Seconds=60):
    Deadline = time.monotonic() + Seconds
    while time.monotonic() < Deadline:
        Text = ReadLog()[Offset:]
        if '[CarbonLuau:Phase5Fixture] FAIL' in Text:
            raise RuntimeError('Phase 5 fixture failed: ' + Text)
        if Expected in Text:
            return
        if Server.poll() is not None:
            raise RuntimeError('Server exited before: ' + Expected)
        time.sleep(0.25)
    raise RuntimeError('Missing current-run log: ' + Expected)


def Send(Command, Expected=''):
    global Identifier
    Identifier += 1
    Socket.send(json.dumps({'Identifier': Identifier, 'Message': Command, 'Name': 'CarbonLuauPhase5'}))
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


def CheckUnmapped():
    Process = Path('/proc') / str(RustPid())
    if str(Library) in (Process / 'maps').read_text():
        raise RuntimeError('Native library remained mapped after plugin unload')


def Sample(Label):
    Process = Path('/proc') / str(RustPid())
    Status = {}
    for Line in (Process / 'status').read_text().splitlines():
        if ':' in Line:
            Key, Value = Line.split(':', 1)
            if Key in {'VmSize', 'VmRSS', 'RssAnon', 'RssFile', 'VmSwap'}:
                Status[Key] = Value.strip()
    Record = {'elapsed_seconds': round(time.monotonic() - Started, 3), 'label': Label,
              'process': Status, 'carbonluau_status': Send('carbonluau.status', 'CarbonLuau:')}
    with Samples.open('a', encoding='utf-8') as Output:
        Output.write(json.dumps(Record, sort_keys=True) + '\n')
    print('[CarbonLuau:Phase5] sample ' + json.dumps(Record, sort_keys=True), flush=True)


def RunProfiler():
    if ProfilerSeconds <= 0:
        print('[CarbonLuau:Phase5] profiler skipped by development-run setting', flush=True)
        return
    print('[CarbonLuau:Phase5] profiler commands: ' + Send('c.find profile'), flush=True)
    for Label, Active in [('idle', False), ('active', True)]:
        Reply = Send('c.profile ' + str(ProfilerSeconds) + ' -c -m -t -gc')
        if 'disabled' in Reply.lower():
            raise RuntimeError('Carbon profiler unexpectedly disabled: ' + Reply)
        print('[CarbonLuau:Phase5] profiler ' + Label + ' start: ' + Reply, flush=True)
        Deadline = time.monotonic() + ProfilerSeconds + 2
        while time.monotonic() < Deadline:
            if Active:
                print(Send('carbonluau.phase5pulse', 'Phase5 pulse PASS'), flush=True)
            time.sleep(min(1, max(0.1, Deadline - time.monotonic())))
        print('[CarbonLuau:Phase5] profiler ' + Label + ' status: ' + Send('c.profilestatus'), flush=True)
        print('[CarbonLuau:Phase5] profiler ' + Label + ' result: ' + Send('c.profiler.print -j'), flush=True)


Library.parent.mkdir(parents=True, exist_ok=True)
(Root / 'carbon/plugins').mkdir(parents=True, exist_ok=True)
ProfilerConfig = Root / 'carbon/config.profiler.json'
ProfilerSettings = json.loads(ProfilerConfig.read_text()) if ProfilerConfig.exists() else {}
ProfilerSettings.update({'Enabled': True, 'TrackCalls': True, 'SourceViewer': False,
                         'Assemblies': [], 'Plugins': ['CarbonLuau'], 'Modules': [],
                         'Extensions': [], 'Harmony': []})
ProfilerConfig.write_text(json.dumps(ProfilerSettings, indent=2) + '\n')
shutil.copy2(Build / 'libcarbonluau_native.so', Library)
shutil.copy2(Fixture / 'CarbonLuau.cszip', Root / 'carbon/plugins/CarbonLuau.cszip')
shutil.copytree(Fixture / 'scripts', Root / 'carbon/data/CarbonLuau/scripts', dirs_exist_ok=True)

with Console.open('w') as Output:
    Server = subprocess.Popen([
        'bash', 'carbon.sh', '-batchmode', '-nographics', '-logfile', str(Log),
        '+server.ip', '127.0.0.1', '+server.port', '28215', '+server.queryport', '28217',
        '+server.identity', 'carbonluau-phase5', '+server.hostname', 'CarbonLuauPhase5',
        '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
        '+server.saveinterval', '600', '+rcon.ip', '127.0.0.1', '+rcon.port', '28216',
        '+rcon.web', '1', '+rcon.password', uuid.uuid4().hex,
    ], cwd=Root, stdout=Output, stderr=subprocess.STDOUT, start_new_session=True)
    try:
        Started = time.monotonic()
        print('[CarbonLuau:Phase5] log: ' + str(Log), flush=True)
        WaitLog(0, 'Server startup complete', 600)
        Secret = next(Argument for Index, Argument in enumerate(Server.args) if Server.args[Index - 1] == '+rcon.password')
        Socket = websocket.create_connection('ws://127.0.0.1:28216/' + Secret, timeout=30)
        WaitLog(0, 'Ready; generation=1', 120)
        Sample('startup')

        Offset = len(ReadLog())
        print(Send('carbonluau.phase5fixture', 'CarbonLuau Phase5 fixture scheduled'), flush=True)
        WaitLog(Offset, 'PASS complete Phase 5 controlled-host fixture', 900)
        Sample('composite-fixture')
        print(Send('carbonluau.phase5latency', 'Phase5 latency PASS'), flush=True)

        for Cycle in range(1, LifecycleCycles + 1):
            print(Send('carbonluau.phase5arm', 'Phase5 armed'), flush=True)
            Armed = Send('carbonluau.status', 'CarbonLuau: ready')
            if 'Facade pending/listeners/commands: 0/2/1' not in Armed or 'Queued: 1' not in Armed:
                raise RuntimeError('Lifecycle state was not fully armed: ' + Armed)
            Offset = len(ReadLog())
            Send('c.unload CarbonLuau')
            WaitLog(Offset, 'Unloaded plugin CarbonLuau')
            CheckUnmapped()
            if 'hostname: CarbonLuauPhase5' not in Send('status'):
                raise RuntimeError('Server unresponsive after unload')
            Offset = len(ReadLog())
            Send('c.load CarbonLuau')
            WaitLog(Offset, 'Ready; generation=1', 120)
            print('[CarbonLuau:Phase5] PASS plugin unload/load cycle ' + str(Cycle) + '; native unmapped and server responsive', flush=True)
        Sample('lifecycle-soak')

        print(Send('carbonluau.phase5arm', 'Phase5 armed'), flush=True)
        SoakDeadline = time.monotonic() + SoakMinutes * 60
        Pulse = 0
        while time.monotonic() < SoakDeadline:
            Pulse += 1
            print(Send('carbonluau.phase5pulse', 'Phase5 pulse PASS'), flush=True)
            Sample('soak-' + str(Pulse))
            time.sleep(min(60, max(0, SoakDeadline - time.monotonic())))
        Sample('soak-final')
        RunProfiler()
        print(Send('carbonluau.phase5cleanup', 'controlled player removed'), flush=True)
        Sample('final')
        print('[CarbonLuau:Phase5] PASS Linux Phase 5 runner; lifecycle cycles=' + str(LifecycleCycles) +
              '; soak minutes=' + str(SoakMinutes), flush=True)
    finally:
        if Socket:
            try:
                Send('quit')
            except (websocket.WebSocketException, json.JSONDecodeError):
                print('[CarbonLuau:Phase5] quit sent; RCON closed during shutdown', flush=True)
            Socket.close()
        try:
            Code = Server.wait(timeout=60)
            print('[CarbonLuau:Phase5] server exit code: ' + str(Code), flush=True)
        except subprocess.TimeoutExpired:
            os.killpg(Server.pid, signal.SIGTERM)
            Server.wait(timeout=15)
            raise RuntimeError('Server required termination instead of clean quit')
