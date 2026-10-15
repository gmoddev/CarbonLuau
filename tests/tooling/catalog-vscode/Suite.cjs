// Executed only inside a real VS Code Extension Host with the generated pack.
const Assert = require('node:assert/strict');
const Fs = require('node:fs');
const Path = require('node:path');
const Process = require('node:process');
const { setTimeout: Delay } = require('node:timers/promises');
const Vscode = require('vscode');

exports.run = async () => {
  const Marker = Process.env.CARBONLUAU_CATALOG_MARKER;
  const Symbols = JSON.parse(Fs.readFileSync(Process.env.CARBONLUAU_CATALOG_SYMBOLS, 'utf8'));
  const Expected = Symbols.Items.map(Row => Row.Symbol).sort();
  Assert.equal(Expected.length, 221);
  Assert.equal(Vscode.workspace.isTrusted, true, 'catalog completion requires actual Workspace Trust');
  const Extension = Vscode.extensions.getExtension('gmoddev.carbonluau-vscode');
  Assert.ok(Extension); await Extension.activate();
  const Results = [];
  for (const Relative of ['init.luau', 'modules/module.luau', 'addon/init.luau', 'addon/module.luau']) {
    const Uri = Vscode.Uri.joinPath(Vscode.workspace.workspaceFolders[0].uri, ...Relative.split('/'));
    const Document = await Vscode.workspace.openTextDocument(Uri);
    const ProjectFolder = Vscode.workspace.getWorkspaceFolder(Uri);
    Assert.ok(ProjectFolder && (Relative.startsWith('addon/') ? ProjectFolder.uri.fsPath.endsWith(Path.sep + 'addon') : !ProjectFolder.uri.fsPath.endsWith(Path.sep + 'addon')),
      'root/addon project selection');
    await Vscode.window.showTextDocument(Document);
    await Vscode.commands.executeCommand('carbonLuau.validateProject');
    async function Completion(Line, Character, Names) {
      const End = Date.now() + 30000;
      let Actual = [];
      while (Date.now() < End) {
        const Value = await Vscode.commands.executeCommand('vscode.executeCompletionItemProvider', Uri, new Vscode.Position(Line, Character));
        Actual = [...new Set((Value?.items ?? []).map(Item => typeof Item.label === 'string' ? Item.label : Item.label.label))].sort();
        if (JSON.stringify(Actual) === JSON.stringify(Names)) return Actual;
        await Delay(100);
      }
      Assert.deepEqual(Actual, Names, `exact catalog completion in ${Relative}:${Line}; actual=${JSON.stringify(Actual)}; diagnostics=${JSON.stringify(Vscode.languages.getDiagnostics(Uri).map(Value => Value.message))}`);
    }
    const Items = await Completion(1, 'local A = Item.'.length, Expected);
    const Prefab = await Completion(2, 'local B = Prefab.'.length, ['Loot']);
    const Loot = await Completion(3, 'local C = Prefab.Loot.'.length, ['Barrel1', 'Barrel2']);
    const Hovers = await Vscode.commands.executeCommand('vscode.executeHoverProvider', Uri, new Vscode.Position(0, 21));
    const Hover = Hovers.flatMap(Value => Value.contents.map(Content => Content.value ?? String(Content))).join('\n');
    Assert.match(Hover, /scrap/, 'canonical string hover');
    Assert.match(Hover, /Loot|D8-qualified/, 'capability/provenance hover');
    const PrefabHovers = await Vscode.commands.executeCommand('vscode.executeHoverProvider', Uri, new Vscode.Position(4, 29));
    const PrefabHover = PrefabHovers.flatMap(Value => Value.contents.map(Content => Content.value ?? String(Content))).join('\n');
    // The extension's MarkdownString.appendText escapes Markdown punctuation.
    const PrefabText = PrefabHover.replace(/\\([.-])/g, '$1');
    Assert.ok(PrefabText.includes('assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab'), `canonical prefab path hover: ${JSON.stringify(PrefabHover)}`);
    Assert.ok(!Items.includes('Revolver') && !Items.includes('Get') && !Prefab.includes('Entity'));
    Results.push({ Relative, ItemFields: Items.length, PrefabFields: Prefab, LootFields: Loot, Hover, PrefabHover });
  }
  Fs.writeFileSync(Marker, JSON.stringify({ Stage: 'complete', ApiVersion: '0.7.5-experimental', Results }, null, 2));
};
