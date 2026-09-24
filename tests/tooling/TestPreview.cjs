/* Black-box preview execution through the real coordinator and contained worker. */
const Assert = require('node:assert/strict');
const Fs = require('node:fs');
const Path = require('node:path');
const Crypto = require('node:crypto');
const {spawn} = require('node:child_process');
const PackRoot = Path.resolve(process.argv[2]);
const Pack = JSON.parse(Fs.readFileSync(Path.join(PackRoot, 'pack.json'), 'utf8'));
const Protocol = {Name: 'CarbonLuau.Tooling', Major: 1, Minor: 0};
const Pending = new Map();
const Process = spawn(Path.join(PackRoot, Pack.Host), ['--stdio'], {windowsHide: true, stdio: ['pipe', 'pipe', 'pipe']});
let Buffer = BufferAlloc(), NextId = 0, Stderr = '';
function BufferAlloc() { return global.Buffer.alloc(0); }
Process.stderr.on('data', Data => { Stderr += Data.toString(); });
Process.stdout.on('data', Data => {
    Buffer = global.Buffer.concat([Buffer, Data]);
    while (true) {
        const Header = Buffer.indexOf('\r\n\r\n'); if (Header < 0) break;
        const Length = Number(Buffer.subarray(0, Header).toString().slice(16));
        Assert(Number.isSafeInteger(Length) && Length > 0 && Length <= 8388608);
        if (Buffer.length < Header + 4 + Length) break;
        const Message = JSON.parse(Buffer.subarray(Header + 4, Header + 4 + Length));
        Buffer = Buffer.subarray(Header + 4 + Length);
        const Entry = Pending.get(Message.Id); Assert(Entry, 'unexpected response ID'); Pending.delete(Message.Id);
        clearTimeout(Entry.Timer); Entry.Resolve(Message);
    }
});
Process.on('exit', Code => { for (const Entry of Pending.values()) { clearTimeout(Entry.Timer); Entry.Reject(new Error(`Coordinator exited ${Code}: ${Stderr}`)); } });
function Canonical(Value) {
    if (Array.isArray(Value)) return Value.map(Canonical);
    if (Value && typeof Value === 'object') return Object.fromEntries(Object.keys(Value).sort().map(Key => [Key, Canonical(Value[Key])]));
    return Value;
}
function Request(Method, Params = {}) {
    const Id = ++NextId, Message = {Protocol, Id, Method, Params};
    if (Method === 'preview') Message.ProjectRevision = 'sha256:' + Crypto.createHash('sha256').update(JSON.stringify(Canonical(Params)) + '\n' + Pack.ApiVersion + '\n' + Pack.PackVersion).digest('hex');
    const PromiseValue = new Promise((Resolve, Reject) => {
        const Timer = setTimeout(() => { Process.kill(); Reject(new Error(`Timed out waiting for ${Method}: ${Stderr}`)); }, 25000);
        Pending.set(Id, {Resolve, Reject, Timer});
    });
    const Body = global.Buffer.from(JSON.stringify(Message));
    Process.stdin.write(`Content-Length: ${Body.length}\r\n\r\n`); Process.stdin.write(Body);
    return {Id, Revision: Message.ProjectRevision, Promise: PromiseValue};
}
function Params(Source, Width = 1920, Height = 1080) {
    return {Snapshot: {Folders: [{Id: 'workspace', Files: [{Path: 'init.luau', Text: Source}]}]}, ProjectId: 'workspace/',
        Viewport: {Width, Height}, ApiVersion: Pack.ApiVersion, PackVersion: Pack.PackVersion, ToolingBuildId: Pack.ToolingBuildId,
        SemanticRevision: Pack.SemanticRevision, PreviewPlanSchema: 1, WorkspaceTrusted: true};
}
const Source = 'local Gui = game:GetService("Gui")\nlocal Screen = Gui:Create("ScreenGui")\nlocal Frame = Screen:Create("Frame")\nFrame.Position = UDim2.fromScale(0.5, 0.5)\nFrame.AnchorPoint = Vector2.new(0.5, 0.5)';
function FixtureSource(Operations) {
    const Quote = Text => '"' + [...global.Buffer.from(Text)].map(Byte => '\\' + String(Byte).padStart(3, '0')).join('') + '"';
    let Next = 0;
    const Lines = ['local Gui = game:GetService("Gui")', 'local Objects = {}'];
    for (const Fields of Operations) {
        if (Fields[0] === 'create') Lines.push(`Objects[${++Next}] = ${Fields[1] ? `Objects[${Fields[1]}]` : 'Gui'}:Create(${Quote(Fields[2])})`);
        else {
            const [Kind, ...Values] = Fields.slice(3); let Value;
            if (Kind === 'string') Value = Quote(Values[0]);
            else if (Kind === 'boolean') Value = Values[0] === '1' ? 'true' : 'false';
            else if (Kind === 'number' || Kind === 'integer') Value = Values[0];
            else if (Kind === 'guifont') Value = 'GuiFont.' + Values[0];
            else if (Kind === 'imagesource') Value = 'ImageSource.' + Values[0] + '(' + Values.slice(1).map((Text, Index) => Values[0] === 'Item' && Index === 0 ? Text : Quote(Text)).join(',') + ')';
            else Value = ({udim: 'UDim', udim2: 'UDim2', vector2: 'Vector2', color3: 'Color3'})[Kind] + '.new(' + Values.join(',') + ')';
            Lines.push(`Objects[${Fields[1]}].${Fields[2]} = ${Value}`);
        }
    }
    return Lines.join('\n');
}
(async () => {
    try {
        const Initialized = await Request('initialize', {ExtensionVersion: '0.0.1', ApiVersion: Pack.ApiVersion, PackageSchema: 1,
            Platform: Pack.Platform, PackVersion: Pack.PackVersion, Capabilities: ['StaticAnalysis', 'Metadata', 'PreviewExecution']}).Promise;
        Assert(!Initialized.Error, JSON.stringify(Initialized));
        if (process.argv.includes('--supervisor-fixture')) {
            const ManifestPath = Path.join(PackRoot, 'pack.json'), Original = Fs.readFileSync(ManifestPath);
            const FixtureName = Pack.Platform === 'win32-x64' ? 'PreviewFixture.exe' : 'PreviewFixture';
            const Replacement = {...Pack, Host: FixtureName, Files: {...Pack.Files}};
            for (const Name of Fs.readdirSync(PackRoot).filter(Name => Name.startsWith('PreviewFixture.')).concat(FixtureName))
                Replacement.Files[Name] = Crypto.createHash('sha256').update(Fs.readFileSync(Path.join(PackRoot, Name))).digest('hex');
            const Initial = await Request('preview', Params(Source)).Promise; Assert(!Initial.Error);
            Fs.writeFileSync(Path.join(PackRoot, 'FixturePlan.json'), JSON.stringify(Initial.Result));
            const PidGone = async () => {
                const Pid = Number(Fs.readFileSync(Path.join(PackRoot, 'FixturePid.txt')));
                let Alive = true;
                for (let Attempt = 0; Attempt < 100 && Alive; Attempt++) {
                    try {process.kill(Pid, 0);} catch {Alive = false;}
                    if (Alive) await new Promise(Resolve => setTimeout(Resolve, 20));
                }
                Assert(!Alive, 'orphan worker ' + Pid);
            };
            const WaitPid = async Previous => {
                for (let Attempt = 0; Attempt < 600; Attempt++) {
                    if (Fs.existsSync(Path.join(PackRoot, 'FixturePid.txt')) && Fs.readFileSync(Path.join(PackRoot, 'FixturePid.txt'), 'utf8') !== Previous) return;
                    await new Promise(Resolve => setTimeout(Resolve, 20));
                }
                Assert.fail('fixture worker did not start');
            };
            try {
                for (const Mode of ['crash', 'hang', 'memory', 'oversized', 'malformed', 'stderr', 'nonce', 'stale', 'protocol', 'duplicate', 'plan']) {
                    Fs.writeFileSync(ManifestPath, JSON.stringify(Replacement));
                    const Start = performance.now(), Response = await Request('preview', Params(Mode)).Promise;
                    Assert(Response.Error, Mode); Assert.notEqual(Response.Error.Code, 'Fixture', Mode);
                    Assert(performance.now() - Start < 20000, 'bounded recovery'); await PidGone();
                    if (Mode === 'memory' && Fs.existsSync(Path.join(PackRoot, 'FixtureMemory.json'))) {
                        const Bytes = JSON.parse(Fs.readFileSync(Path.join(PackRoot, 'FixtureMemory.json'))).AllocatedNativeBytes;
                        Assert(Bytes >= 64 * 1024 * 1024 && Bytes < 256 * 1024 * 1024, 'native process allocation ceiling');
                        console.log('[CarbonLuau:PreviewMemory] Native allocation refused after ' + Bytes + ' bytes, plus CLR/loaded process memory');
                    }
                    Fs.writeFileSync(ManifestPath, Original);
                    Assert(!(await Request('preview', Params(Source)).Promise).Error, 'real recovery after ' + Mode);
                    console.log('[CarbonLuau:PreviewSupervisor] ' + Mode + ': ' + Response.Error.Code + ', reaped; real preview recovered');
                }
                Fs.writeFileSync(ManifestPath, JSON.stringify(Replacement));
                const Previous = Fs.readFileSync(Path.join(PackRoot, 'FixturePid.txt'), 'utf8');
                const Running = Request('preview', Params('hang'));
                await WaitPid(Previous);
                Assert.equal((await Request('cancel', {RequestId: Running.Id, ProjectRevision: 'sha256:' + '0'.repeat(64)}).Promise).Error.Code, 'StaleCancellation');
                const Canceled = Request('cancel', {RequestId: Running.Id, ProjectRevision: Running.Revision});
                Assert.equal((await Canceled.Promise).Result.Canceled, true);
                Assert.equal((await Running.Promise).Error.Code, 'PreviewCanceled');
                await PidGone();
                const Stale = await Request('cancel', {RequestId: Running.Id, ProjectRevision: Running.Revision}).Promise;
                Assert.equal(Stale.Error.Code, 'StaleCancellation');
            } finally {Fs.writeFileSync(ManifestPath, Original);}
            Assert(!(await Request('preview', Params(Source)).Promise).Error);
            try {
                Fs.writeFileSync(ManifestPath, JSON.stringify(Replacement));
                const Previous = Fs.readFileSync(Path.join(PackRoot, 'FixturePid.txt'), 'utf8');
                const Running = Request('preview', Params('hang'));
                await WaitPid(Previous); Process.stdin.end();
                Assert.equal((await Running.Promise).Error.Code, 'PreviewCanceled'); await PidGone();
            } finally {Fs.writeFileSync(ManifestPath, Original);}
            console.log('[CarbonLuau:PreviewSupervisor] PASS hostile worker, external deadline/memory, protocol, cancellation and recovery');
            return;
        }
        if (process.argv.includes('--determinism-gate')) {
            const Cases = {
                PointerText: 'local Screen = game:GetService("Gui"):Create("ScreenGui")\nlocal Label = Screen:Create("TextLabel")\nLabel.Text = tostring({})',
                ObjectKeyOrder: 'local Screen = game:GetService("Gui"):Create("ScreenGui")\nlocal Values = {}\nfor Index = 1, 32 do Values[{}] = Index end\nlocal Text = ""\nfor Key, Value in Values do Text ..= tostring(Value) .. "," end\nlocal Label = Screen:Create("TextLabel")\nLabel.Text = Text'
            };
            let Failed = false;
            for (const [Name, Code] of Object.entries(Cases)) {
                const Witnesses = [];
                for (let Index = 0; Index < 4; Index++) {
                    const Value = await Request('preview', Params(Code)).Promise;
                    Assert(!Value.Error, JSON.stringify(Value));
                    Witnesses.push({Revision: Value.Result.ProjectRevision,
                        PlanSha256: Crypto.createHash('sha256').update(JSON.stringify(Canonical(Value.Result))).digest('hex'),
                        Text: Value.Result.Nodes[1].Retained.Text});
                }
                const Equal = Witnesses.every(Value => Value.PlanSha256 === Witnesses[0].PlanSha256);
                Failed ||= !Equal;
                console.log(JSON.stringify({Platform: Pack.Platform, Case: Name, Source: Code, Equal, Witnesses}));
            }
            const Stopped = await Request('shutdown').Promise;
            Assert.equal(Stopped.Result.Stopped, true); Process.stdin.end();
            process.exitCode = Failed ? 1 : 0;
            return;
        }
        const First = await Request('preview', Params(Source)).Promise;
        Assert(!First.Error, JSON.stringify(First));
        Assert.equal(First.Result.Nodes.length, 2);
        Assert.equal(First.Result.Nodes[1].Projected.RectPx.X, 910);
        Assert.equal(First.Result.Nodes[1].Projected.RectPx.Y, 490);
        const Again = await Request('preview', Params(Source)).Promise;
        Assert.deepEqual(Again.Result, First.Result);
        for (const [Width, Height] of [[1280, 720], [2560, 1440], [3440, 1440], [1111, 777]]) {
            const Value = await Request('preview', Params(Source, Width, Height)).Promise;
            Assert(!Value.Error, JSON.stringify(Value));
            Assert.equal(Value.Result.Nodes[1].Projected.RectPx.X, Width / 2 - 50);
        }
        const Fixtures = JSON.parse(Fs.readFileSync(Path.join(__dirname, 'PreviewFixtures.json')));
        const Goldens = JSON.parse(Fs.readFileSync(Path.join(__dirname, 'PreviewGoldens.json')));
        const Timings = [];
        for (const Fixture of Fixtures) {
            const Started = performance.now();
            const Input = Params(FixtureSource(Fixture.Operations), Fixture.Viewport.Width, Fixture.Viewport.Height);
            const Actual = await Request('preview', Input).Promise;
            Assert(!Actual.Error, Fixture.Name + ': ' + JSON.stringify(Actual.Error));
            const Expected = Goldens.find(Value => Value.Name === Fixture.Name).Plan;
            for (const Key of Object.keys(Expected)) Assert.deepEqual(Actual.Result[Key], Expected[Key], Fixture.Name + ': ' + Key);
            const Repeat = await Request('preview', Input).Promise;
            Assert.deepEqual(Repeat.Result, Actual.Result, Fixture.Name + ' fresh worker determinism');
            Timings.push({Fixture: Fixture.Name, TwoFreshRunsMs: Math.round(performance.now() - Started)});
            const Extra = Fixture.Name === 'objects-128' ? '\nObjects[2]:Create("Frame")' :
                Fixture.Name === 'arranged-children-64' ? '\nObjects[2]:Create("Frame")' :
                Fixture.Name === 'projected-elements-257' ? '\nObjects[1]:Create("Frame")' :
                Fixture.Name === 'clip-depth-four' ? '\nObjects[5]:Create("Frame").ClipsDescendants = true' : null;
            if (Extra) {
                const Failed = await Request('preview', {...Input, Snapshot: Params(FixtureSource(Fixture.Operations) + Extra).Snapshot}).Promise;
                Assert.equal(Failed.Error?.Code, 'GuiError', Fixture.Name + ' one over: ' + JSON.stringify(Failed));
            }
        }
        console.log('[CarbonLuau:PreviewPerformance] ' + JSON.stringify({Platform: Pack.Platform, Timings}));
        for (const [Bad, Code] of [['while true do end', 'PreviewDeadline'], ['local =', 'CompileError'], ['error("intentional")', 'RuntimeError'],
            ['game:GetService("Players"):GetPlayers()', 'UnsupportedPreviewApi'],
            ['game:GetService("DataStoreService")', 'UnsupportedPreviewApi'],
            ['game:GetService("DataStoreService"):GetDataStore("Preview"):GetAsync("Key", function() error("storage callback must not run") end)', 'UnsupportedPreviewApi'],
            ['game:GetService("DataStoreService"):GetDataStore("Preview"):SetAsync("Key", true, function() error("storage callback must not run") end)', 'UnsupportedPreviewApi'],
            ['game:GetService("DataStoreService"):GetDataStore("Preview"):RemoveAsync("Key", function() error("storage callback must not run") end)', 'UnsupportedPreviewApi'],
            ['local Value = string.rep("x", 100 * 1024 * 1024)', 'PreviewMemory']]) {
            const Failed = await Request('preview', Params(Bad)).Promise;
            Assert.equal(Failed.Error?.Code, Code, JSON.stringify(Failed));
            const Recovery = await Request('preview', Params(Source)).Promise;
            Assert(!Recovery.Error, JSON.stringify(Recovery));
        }
        const Untrusted = await Request('preview', {...Params(Source), WorkspaceTrusted: false}).Promise;
        Assert.equal(Untrusted.Error?.Code, 'WorkspaceUntrusted');
        for (const Attack of ['io.open("secret")', 'os.execute("bad")', 'require("../secret")', 'require("https://example.invalid")',
            'task.defer(function() end)', 'game:GetService("Gui"):Create("TextBox")', 'local X = loadstring("return 1")']) {
            Assert((await Request('preview', Params(Attack)).Promise).Error, Attack);
            Assert(!(await Request('preview', Params(Source)).Promise).Error, 'recovery after ' + Attack);
        }
        const SafeGlobals = await Request('preview', Params('assert(io == nil and os == nil and package == nil and loadstring == nil and debug == nil)\n' + Source)).Promise;
        Assert(!SafeGlobals.Error, JSON.stringify(SafeGlobals));
        const Huge = await Request('preview', Params('--' + 'x'.repeat(65536))).Promise; Assert(Huge.Error);
        const Traversal = Params(Source); Traversal.Snapshot.Folders[0].Files[0].Path = '../init.luau';
        Assert((await Request('preview', Traversal).Promise).Error);
        Assert((await Request('preview', {...Params(Source), Snapshot: {Folders: 'bad'}}).Promise).Error);
        const Modules = Params('local Module = require("a")\n' + Source);
        Modules.Snapshot.Folders[0].Files.push({Path: 'modules/a.luau', Text: 'return require("b")'}, {Path: 'modules/b.luau', Text: 'return require("a")'});
        Assert.equal((await Request('preview', Modules).Promise).Error?.Code, 'ModuleError');
        Modules.Snapshot.Folders[0].Files = [{Path: 'init.luau', Text: 'require("m1")\n' + Source}];
        for (let Index = 1; Index <= 34; Index++) Modules.Snapshot.Folders[0].Files.push({Path: `modules/m${Index}.luau`, Text: Index === 34 ? 'return {}' : `return require("m${Index + 1}")`});
        Assert.equal((await Request('preview', Modules).Promise).Error?.Code, 'ModuleError');
        const Selection = Params('local Gui = game:GetService("Gui")\nfor Index = 1, 5 do Gui:Create("ScreenGui") end');
        const Choices = await Request('preview', Selection).Promise; Assert.equal(Choices.Error.Details.Screens.length, 5);
        const Selected = await Request('preview', {...Selection, ScreenId: '3'}).Promise; Assert.equal(Selected.Result.Screen.Id, '3');
        for (const Change of [{ToolingBuildId: 'sha256:' + '0'.repeat(64)}, {PreviewPlanSchema: 2}, {SemanticRevision: '0'.repeat(40)}])
            Assert((await Request('preview', {...Params(Source), ...Change}).Promise).Error);
        Assert(!(await Request('preview', Params(Source)).Promise).Error);
        const Folder = (Id, Files) => ({Id, Files: Object.entries(Files).map(([Path, Text]) => ({Path, Text}))});
        const Addons = Params(''); Addons.ProjectId = 'consumer/';
        Addons.Snapshot.Folders = [
            Folder('consumer', {'addon.json': JSON.stringify({schema: 1, id: 'consumer', version: '1.0.0', dependencies: {required: ['library']}}),
                'init.luau': 'local A = require("@library")\nassert(A == require("@library"))\nassert(A.Value == require("@library/public").Value)\n' + Source}),
            Folder('library', {'addon.json': JSON.stringify({schema: 1, id: 'library', version: '1.0.0', main: 'api', publicModules: ['public']}),
                'init.luau': 'return nil', 'api.luau': 'return require("private")', 'private.luau': 'return {Value = 42}', 'public.luau': 'return require("private")'})];
        const StartedAddon = performance.now();
        const AddonPlan = await Request('preview', Addons).Promise; Assert(!AddonPlan.Error, JSON.stringify(AddonPlan));
        Assert.deepEqual((await Request('preview', Addons).Promise).Result, AddonPlan.Result);
        console.log('[CarbonLuau:PreviewPerformance] Multi-module addon two runs ms=' + Math.round(performance.now() - StartedAddon));
        for (const Import of ['@library/private', '@undeclared']) {
            Addons.Snapshot.Folders[0].Files.find(Value => Value.Path === 'init.luau').Text = `require(${JSON.stringify(Import)})\n` + Source;
            Assert.equal((await Request('preview', Addons).Promise).Error?.Code, 'ModuleError');
        }
        console.log('[CarbonLuau:Preview] PASS real worker, deterministic fixture repeat equality, viewport, loop/heap/compile/runtime/host failures and recovery');
        const Stopped = await Request('shutdown').Promise;
        Assert.equal(Stopped.Result.Stopped, true); Process.stdin.end();
    } catch (Error) { Process.kill(); console.error(Error); process.exitCode = 1; }
})();
