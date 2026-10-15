// Run on a hidden desktop or Xvfb. This does not disable Workspace Trust.
const Fs = require('node:fs');
const Path = require('node:path');
const Process = require('node:process');
const { spawn: Spawn } = require('node:child_process');
const { setTimeout: Delay } = require('node:timers/promises');
const Extension = Path.resolve(Process.argv[2]);
const Work = Path.resolve(Process.argv[3]);
const Symbols = Path.resolve(Process.argv[4]);
const { downloadAndUnzipVSCode: Download } = require(Path.join(Extension, 'node_modules/@vscode/test-electron'));
const Marker = Path.join(Work, 'result.json');

async function Trust() {
  const Pages = await (await fetch('http://127.0.0.1:9237/json', { signal: AbortSignal.timeout(2000) })).json();
  const Page = Pages.find(Value => Value.type === 'page' && Value.url.includes('workbench'));
  if (!Page) return;
  const Socket = new WebSocket(Page.webSocketDebuggerUrl);
  await new Promise((Resolve, Reject) => { Socket.addEventListener('open', Resolve, { once: true }); Socket.addEventListener('error', Reject, { once: true }); });
  try {
    const Reply = new Promise(Resolve => Socket.addEventListener('message', Event => { const Value = JSON.parse(Event.data); if (Value.id === 1) Resolve(Value); }));
    Socket.send(JSON.stringify({ id: 1, method: 'Runtime.evaluate', params: { expression: "(() => { const Button=[...document.querySelectorAll('[role=\"button\"]')].find(Value => Value.textContent.trim()==='Trust' || Value.getAttribute('aria-label')?.startsWith('Trust, Keyboard')); if(Button) Button.click(); })()" } }));
    await Reply;
  } finally { Socket.close(); }
}

async function Main() {
  const Profile = Path.join(Work, 'profile'), Workspace = Path.join(Work, 'workspace'), Harness = Path.join(Work, 'harness');
  for (const Directory of [Work, Path.join(Profile, 'User'), Workspace, Harness, Path.join(Workspace, 'modules'), Path.join(Workspace, 'addon')]) Fs.mkdirSync(Directory, { recursive: true });
  Fs.writeFileSync(Marker, JSON.stringify({ Stage: 'starting' }));
  Fs.writeFileSync(Path.join(Profile, 'User/settings.json'), JSON.stringify({
    'security.workspace.trust.startupPrompt': 'never', 'telemetry.telemetryLevel': 'off', 'update.mode': 'none',
    'workbench.startupEditor': 'none', 'extensions.autoUpdate': false, 'editor.wordBasedSuggestions': 'off', 'carbonLuau.apiVersion': '0.7.5-experimental'
  }));
  Fs.writeFileSync(Path.join(Harness, 'package.json'), JSON.stringify({ name: 'catalog-d9-e2e', publisher: 'carbonluau-tests', version: '0.0.0', engines: { vscode: '^1.95.0' }, main: './Harness.cjs', activationEvents: ['onStartupFinished'], capabilities: { untrustedWorkspaces: { supported: true } } }));
  const Suite = Path.join(__dirname, 'Suite.cjs');
  Fs.writeFileSync(Path.join(Harness, 'Harness.cjs'), `const Vscode=require('vscode'); exports.activate=async()=>{ try { await Vscode.commands.executeCommand('workbench.trust.manage'); const End=Date.now()+30000; while(!Vscode.workspace.isTrusted && Date.now()<End) await new Promise(R=>setTimeout(R,100)); await require(${JSON.stringify(Suite)}).run(); } catch(Error) { require('node:fs').writeFileSync(${JSON.stringify(Marker)},JSON.stringify({Stage:'failed',Error:String(Error.stack)})); console.error(Error); } await Vscode.commands.executeCommand('workbench.action.quit'); };`);
  // Keep the snapshot syntactically admitted by D19; request completion immediately
  // after each dot, before the existing member text (no typed prefix).
  const Source = 'local Value = Item.Scrap\nlocal A = Item.Scrap\nlocal B = Prefab.Loot\nlocal C = Prefab.Loot.Barrel1\nlocal Barrel = Prefab.Loot.Barrel1\n';
  for (const Relative of ['init.luau', 'modules/module.luau', 'addon/init.luau', 'addon/module.luau'])
    Fs.writeFileSync(Path.join(Workspace, Relative), Source + (Relative.endsWith('init.luau') ? "local Imported = require('module')\n" : '') + 'return Value\n');
  Fs.writeFileSync(Path.join(Workspace, 'addon/addon.json'), JSON.stringify({ schema: 1, id: 'catalogd9editor', version: '1.0.0' }));
  // Workspace configuration must remain unexecuted under the existing supervisor.
  Fs.writeFileSync(Path.join(Workspace, '.config.luau'), 'while true do end');
  const WorkspaceFile = Path.join(Work, 'catalog.code-workspace');
  Fs.writeFileSync(WorkspaceFile, JSON.stringify({ folders: [{ path: Workspace }, { path: Path.join(Workspace, 'addon') }] }));
  const Executable = await Download({ version: '1.95.3', cachePath: Path.join(Work, 'vscode-cache') });
  const Child = Spawn(Executable, [WorkspaceFile, '--no-sandbox', '--disable-gpu', '--disable-updates', '--skip-welcome', '--skip-release-notes',
    '--user-data-dir=' + Profile, '--extensions-dir=' + Path.join(Work, 'extensions'), '--remote-debugging-port=9237',
    '--extensionDevelopmentPath=' + Extension, '--extensionDevelopmentPath=' + Harness], {
    env: { ...Process.env, CARBONLUAU_CATALOG_MARKER: Marker, CARBONLUAU_CATALOG_SYMBOLS: Symbols }, windowsHide: true, shell: false,
    stdio: ['ignore', 'pipe', 'pipe']
  });
  let Output = '', Exited = false, Code;
  const Exit = new Promise(Resolve => Child.once('exit', Value => { Exited = true; Code = Value; Resolve(); }));
  for (const Stream of [Child.stdout, Child.stderr]) Stream.on('data', Value => { Output = (Output + Value).slice(-131072); });
  try {
    const End = Date.now() + 180000;
    while (!Exited && Date.now() < End) { try { await Trust(); } catch { /* startup/reload gap */ } await Delay(250); }
    if (!Exited) throw new Error('CATALOG-D9 VS Code deadline exceeded');
    await Exit;
    const Result = JSON.parse(Fs.readFileSync(Marker, 'utf8'));
    if (Code !== 0 || Result.Stage !== 'complete') throw new Error(JSON.stringify(Result) + '\n' + Output);
    Process.stdout.write('[CarbonLuau:CatalogVSCode] PASS actual VS Code 1.95.3, trusted supervisor/LSP, 221/1/2 exact completions and canonical capability hovers in root/addon/imported modules\n');
  } finally { if (!Exited) Child.kill(); Fs.writeFileSync(Path.join(Work, 'vscode.log'), Output); }
}
Main().catch(Error => { Process.stderr.write(String(Error.stack) + '\n'); Process.exitCode = 1; });
