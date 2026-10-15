"""Exercise the actual Windows/Linux public receipt gates without starting a host."""
import ast
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from types import SimpleNamespace

Root = Path(__file__).resolve().parent.parent
Row = "D8_PUBLIC_ITEM name=sulfur item_id=-1581843485 stackable=1000 family=RawResource result=PASS reason=fixture\n"
Batch = "PASS D8 bulk public batch offset=0 count=1 pass=1 fail=0\n"
Cases = [
    {"Name": "single", "Content": Row + Batch, "Accept": True},
    {"Name": "pair", "Content": Row + Row.replace("name=sulfur", "name=cloth") + Batch.replace("count=1 pass=1", "count=2 pass=2"), "Accept": True},
    {"Name": "missing_batch", "Content": Row, "Accept": False},
    {"Name": "duplicate_batch", "Content": Row + Batch + Batch, "Accept": False},
    {"Name": "completed_with_failure", "Content": Row.replace("result=PASS", "result=FAIL") + Batch.replace("pass=1 fail=0", "pass=0 fail=1"), "Accept": False},
    {"Name": "false_zero_fail", "Content": Row.replace("result=PASS", "result=FAIL") + Batch, "Accept": False},
    {"Name": "missing_row", "Content": Row + Batch.replace("count=1 pass=1", "count=2 pass=2"), "Accept": False},
    {"Name": "duplicate_row", "Content": Row + Row + Batch.replace("count=1 pass=1", "count=2 pass=2"), "Accept": False},
    {"Name": "zero_count", "Content": Batch.replace("count=1 pass=1", "count=0 pass=0"), "Accept": False},
    {"Name": "excess_count", "Content": Row + Batch.replace("count=1 pass=1", "count=33 pass=33"), "Accept": False},
    {"Name": "unknown_status", "Content": Row.replace("result=PASS", "result=PENDING") + Batch, "Accept": False},
    {"Name": "extra_unknown_status", "Content": Row + Row.replace("name=sulfur", "name=cloth").replace("result=PASS", "result=PENDING") + Batch, "Accept": False},
    {"Name": "extra_malformed_record", "Content": Row + "D8_PUBLIC_ITEM name=cloth unexpected=true\n" + Batch, "Accept": False},
]

Tree = ast.parse((Root / "tools/Test-BarrelLootLinux.py").read_text(encoding="utf-8-sig"))
Expected = ast.dump(ast.parse("Args.loot_bulk_items and not D8EarlyOnly", mode="eval").body)
Gates = [Node for Node in ast.walk(Tree) if isinstance(Node, ast.If) and ast.dump(Node.test) == Expected]
assert len(Gates) == 1, "actual Linux public receipt gate is ambiguous/missing"
Code = compile(ast.Module(body=Gates, type_ignores=[]), "actual-linux-public-receipt-gate", "exec")
for Case in Cases:
    Accepted = True
    try:
        exec(Code, {"Args": SimpleNamespace(loot_bulk_items=True), "D8EarlyOnly": False,
                    "Content": Case["Content"], "Log": "offline-receipt", "re": re})
    except RuntimeError:
        Accepted = False
    assert Accepted == Case["Accept"], ("Linux", Case["Name"])

Shells = [ShellPath for Name in ("powershell.exe", "pwsh") if (ShellPath := shutil.which(Name))]
assert Shells, "actual Windows receipt gate requires an available PowerShell runtime"
with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootReceipt-") as Directory:
    Fixture = Path(Directory)
    Input = Fixture / "cases.json"
    Input.write_text(json.dumps(Cases), encoding="utf-8")
    Script = Fixture / "Test-ActualGate.ps1"
    Script.write_text(r'''param([string]$Root, [string]$CasesPath)
$ErrorActionPreference = 'Stop'
$Tokens = $null; $ParseErrors = $null
$Tree = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $Root 'tools/Test-BarrelLootWindows.ps1'), [ref]$Tokens, [ref]$ParseErrors)
if ($ParseErrors.Count -ne 0) { throw 'Actual Windows runner parse failed' }
$Gates = @($Tree.FindAll({ param($Node)
    $Node -is [System.Management.Automation.Language.IfStatementAst] -and
    $Node.Extent.Text.StartsWith('if ($LootBulkItems -and !$D8EarlyOnly) {', [StringComparison]::Ordinal)
}, $true))
if ($Gates.Count -ne 1) { throw 'Actual Windows public receipt gate is ambiguous/missing' }
$Gate = [scriptblock]::Create($Gates[0].Extent.Text)
foreach ($Case in (Get-Content -Raw -LiteralPath $CasesPath | ConvertFrom-Json)) {
    $LootBulkItems = $true; $D8EarlyOnly = $false; $Content = $Case.Content
    $Log = 'offline-receipt'; $Prefix = '[CarbonLuau:LootReceiptTest]'; $Accepted = $true
    try { & $Gate } catch { $Accepted = $false }
    if ($Accepted -ne $Case.Accept) { throw ('Windows gate disagrees: ' + $Case.Name) }
}
''', encoding="utf-8")
    for Shell in Shells:
        Result = subprocess.run([Shell, "-NoProfile", "-NonInteractive", "-File", str(Script),
                                 "-Root", str(Root), "-CasesPath", str(Input)],
                                capture_output=True, text=True, timeout=20)
        assert Result.returncode == 0, (Shell, Result.stdout, Result.stderr)
print("[CarbonLuau:LootReceiptTest] PASS actual Linux/Windows gates; complete individual PASS required; cases=" +
      str(len(Cases)) + " PowerShell_runtimes=" + str(len(Shells)))
