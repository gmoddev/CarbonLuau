"""Isolated behavioral tests of actual terminal-only runner profile helpers; no server is started."""
import ast
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
Root = Path(__file__).resolve().parent.parent
Source = Root / "tools/Test-BarrelLootLinux.py"
Names = {"Hash", "DatabaseFiles", "ProfileMetadata", "RestoreProfileMetadata", "SnapshotProfile", "RestoreProfile"}

with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootProfile-") as Directory:
    Base = Path(Directory).resolve()
    Profile = Base / "profile"; Profile.mkdir()
    Original = {"players.db": b"before-db", "players.db-wal": b"before-wal",
                "proceduralmap.1000.13579.289.sav": b"before-save",
                "proceduralmap.1000.13579.289.navmesh": b"before-navmesh"}
    for Name, Data in Original.items():
        (Profile / Name).write_bytes(Data)
    (Profile / "unrelated.txt").write_text("preserved")
    Tree = ast.parse(Source.read_text())
    Functions = [Node for Node in Tree.body if isinstance(Node, ast.FunctionDef) and Node.name in Names]
    assert {Node.name for Node in Functions} == Names
    class IsolatedRoot(ast.NodeTransformer):
        def visit_Constant(self, Node):
            if Node.value == "/root/codex/world-movement-20261003/server-linux/server/entity1a-adapter-check":
                return ast.copy_location(ast.Constant(str(Profile)), Node)
            return Node
    Module = ast.fix_missing_locations(IsolatedRoot().visit(ast.Module(body=Functions, type_ignores=[])))
    Environment = {"pathlib": __import__("pathlib"), "hashlib": hashlib, "os": os, "shutil": shutil,
                   "stat": stat, "json": json, "re": re}
    exec(compile(Module, str(Source), "exec"), Environment)
    Save, Restore, Files = (Environment[Name] for Name in ("SnapshotProfile", "RestoreProfile", "DatabaseFiles"))
    Snapshot = Save(Profile, Base / "backup")
    for Name in Original:
        (Profile / Name).write_bytes(b"changed")
    (Profile / "players.db-wal").unlink()
    (Profile / "new.db-shm").write_bytes(b"new companion")
    Count, Size, Digest = Restore(Snapshot)
    assert Count == 4 and Size == sum(map(len, Original.values())) and len(Digest) == 64
    assert all((Profile / Name).read_bytes() == Data for Name, Data in Original.items())
    assert not (Profile / "new.db-shm").exists() and (Profile / "unrelated.txt").read_text() == "preserved"
    # Reads above can affect access time; verify explicit metadata with the real helper.
    Restore(Snapshot)
    assert all(Environment["ProfileMetadata"](Value["Target"]) == Value["Metadata"] for Value in Snapshot["Files"].values())
    Wrong = Base / "wrong"; Wrong.mkdir()
    try:
        Files(Wrong)
        raise AssertionError("wrong root accepted")
    except RuntimeError:
        pass
    for Index in range(129):
        (Profile / ("bound" + str(Index) + ".db")).touch()
    try:
        Files(Profile)
        raise AssertionError("129-file profile accepted")
    except RuntimeError:
        pass
    for Path in Profile.glob("bound*.db"):
        Path.unlink()
    (Profile / "large.db").touch()
    with (Profile / "large.db").open("r+b") as Stream:
        Stream.truncate(67108865)
    try:
        Files(Profile)
        raise AssertionError("over64MiB profile accepted")
    except RuntimeError:
        pass
    (Profile / "large.db").unlink()
    os.link(Profile / "players.db", Profile / "hard.db")
    try:
        Files(Profile)
        raise AssertionError("hard link accepted")
    except RuntimeError:
        pass
    (Profile / "hard.db").unlink()
    if os.name == "posix":
        (Profile / "linked.db").symlink_to(Profile / "players.db")
        try:
            Files(Profile)
            raise AssertionError("symlink accepted")
        except RuntimeError:
            pass
        (Profile / "linked.db").unlink()
    Snapshot["Files"]["players.db"]["Backup"].write_bytes(b"tampered")
    try:
        Restore(Snapshot)
        raise AssertionError("changed backup accepted")
    except RuntimeError:
        pass

    Runtimes = []
    if os.name == "nt":
        Candidates = [shutil.which("powershell"), shutil.which("pwsh")]
        Runtimes = list(dict.fromkeys(Path for Path in Candidates if Path))
        assert Runtimes, "Windows PowerShell runtime is required"
        # Extract the production functions through the PowerShell parser, changing
        # only the pinned root literal inside this isolated test harness.
        Script = Base / "Test-Windows.ps1"
        Script.write_text("""param([string]$Runner,[string]$TestRoot)
$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
$Errors=$null
$Tree=[System.Management.Automation.Language.Parser]::ParseFile($Runner,[ref]$null,[ref]$Errors)
if($Errors.Count){throw 'Runner parse failure'}
$Names=@('Get-LootProfileFiles','Get-LootProfileMetadata','Set-LootProfileMetadata','Save-LootProfile','Restore-LootProfile')
$Definitions=@($Tree.FindAll({param($Node) $Node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -in $Names},$true))
if($Definitions.Count -ne 5){throw 'Missing actual profile helper'}
$Profile=Join-Path $TestRoot 'profile'
New-Item -ItemType Directory -Path $Profile | Out-Null
foreach($Definition in $Definitions){
  $Body=$Definition.Extent.Text.Replace('D:\\Sandbox\\Codex\\Entity1AStartup\\server\\server\\entity1a-adapter-windows',$Profile)
  . ([ScriptBlock]::Create($Body))
}
$Prefix='[CarbonLuau:ProfileTest]'
$File=Join-Path $Profile 'players.db'
[IO.File]::WriteAllText($File,'before')
$Prior=Get-LootProfileMetadata $File
$Snapshot=Save-LootProfile $Profile (Join-Path $TestRoot 'backup')
[IO.File]::WriteAllText($File,'changed')
[IO.File]::WriteAllText((Join-Path $Profile 'new.db-wal'),'new')
$Proof=@(Restore-LootProfile $Snapshot)
if([IO.File]::ReadAllText($File) -cne 'before' -or (Test-Path -LiteralPath (Join-Path $Profile 'new.db-wal'))){throw 'File/absence restoration failed'}
$Actual=Get-LootProfileMetadata $File
foreach($Key in @('Attributes','Created','Written','Sddl')){if($Actual.$Key -cne $Prior.$Key){throw ('Metadata mismatch '+$Key)}}
if($Proof.Count -ne 1 -or $Proof[0] -notmatch 'FullProfileRestored files=1 bytes=6 .*metadata=true'){throw 'Missing explicit profile proof'}
$Rejected=$false;try{Get-LootProfileFiles $TestRoot | Out-Null}catch{$Rejected=$true};if(!$Rejected){throw 'Wrong root accepted'}
foreach($Index in 0..128){[IO.File]::WriteAllText((Join-Path $Profile ('bound'+$Index+'.db')),'')}
$Rejected=$false;try{Get-LootProfileFiles $Profile | Out-Null}catch{$Rejected=$true};if(!$Rejected){throw 'File cap accepted'}
foreach($Index in 0..128){Remove-Item -LiteralPath (Join-Path $Profile ('bound'+$Index+'.db'))}
$Large=Join-Path $Profile 'large.db';$Stream=[IO.File]::OpenWrite($Large);try{$Stream.SetLength(67108865)}finally{$Stream.Dispose()}
$Rejected=$false;try{Get-LootProfileFiles $Profile | Out-Null}catch{$Rejected=$true};if(!$Rejected){throw 'Byte cap accepted'}
Remove-Item -LiteralPath $Large
$Hard=Join-Path $Profile 'hard.db';New-Item -ItemType HardLink -Path $Hard -Target $File | Out-Null
$Rejected=$false;try{Get-LootProfileFiles $Profile | Out-Null}catch{$Rejected=$true};if(!$Rejected){throw 'Hard link accepted'}
Remove-Item -LiteralPath $Hard
[IO.File]::WriteAllText($Snapshot.Files['players.db'].Backup,'tampered')
$Rejected=$false;try{Restore-LootProfile $Snapshot | Out-Null}catch{$Rejected=$true};if(!$Rejected){throw 'Backup tampering accepted'}
Write-Output '[CarbonLuau:ProfileTest] PASS actual Windows helpers'
""", encoding="utf-8")
        for Index, Runtime in enumerate(Runtimes):
            Target = Base / ("windows" + str(Index)); Target.mkdir()
            Environment = os.environ.copy()
            for Key in list(Environment):
                if Key.lower() == "psmodulepath":
                    del Environment[Key]
            Result = subprocess.run([Runtime, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(Script),
                "-Runner", str(Root / "tools/Test-BarrelLootWindows.ps1"), "-TestRoot", str(Target)],
                capture_output=True, text=True, timeout=30, env=Environment)
            assert Result.returncode == 0, Result.stdout + Result.stderr

print("[CarbonLuau:LootProfileTest] PASS actual bounded profile helpers: exact bytes/file set/mode/owner/timestamps, wrong-root/file/byte/link guards, backup tampering; Windows_runtimes=" + str(len(Runtimes)))
