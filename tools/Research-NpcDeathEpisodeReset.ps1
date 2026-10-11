# Narrow B2 NPC episode reset research. No host edits, patching, or server runs.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyDirectory,
    [Parameter(Mandatory = $true)][string]$InputDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/')
if ($OutputDirectory -eq $Repository -or $OutputDirectory.StartsWith($Repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '[Research:NpcEpisode] Use external evidence.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw '[Research:NpcEpisode] Preserve old evidence; use a fresh output path.' }

# Import only inspected pure metadata helpers; never execute the general
# reproduction script's top-level body or redo its 67-target inventory.
$HelperPath = Join-Path $PSScriptRoot 'Reproduce-EntityDeathPins.ps1'
$Tokens = $null; $Errors = $null
$Ast = [Management.Automation.Language.Parser]::ParseFile($HelperPath, [ref]$Tokens, [ref]$Errors)
if ($Errors.Count) { throw '[Research:NpcEpisode] Helper syntax error.' }
foreach ($Name in @('Require','GetHash','GetTypes','GetAncestry','GetSections','GetRaw','GetEH','GetShape','GetJson')) {
    $Functions = @($Ast.FindAll({ param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq $Name }, $false))
    if ($Functions.Count -ne 1) { throw '[Research:NpcEpisode] Helper definition drift.' }
    . ([scriptblock]::Create($Functions[0].Extent.Text))
}
$CecilPath = Join-Path $AssemblyDirectory 'Mono.Cecil.dll'
Add-Type -Path $CecilPath
[void](New-Item -ItemType Directory -Path $OutputDirectory)
$Reports = [Collections.Generic.List[object]]::new()
$Pins = [Collections.Generic.List[object]]::new()
foreach ($Platform in @('Windows','Linux')) {
    $Suffix = if ($Platform -eq 'Windows') { 'win' } else { 'linux' }
    $AssemblyPath = Join-Path $AssemblyDirectory ('Assembly-CSharp-' + $Suffix + '.dll')
    $CommonPath = Join-Path $InputDirectory ('Carbon.Common-' + $Suffix + '.dll')
    $StartupPath = Join-Path $InputDirectory ('Carbon.Startup-' + $Suffix + '.dll')
    $ExpectedAssembly = if ($Platform -eq 'Windows') { 'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2' } else { 'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450' }
    $ExpectedCommon = if ($Platform -eq 'Windows') { '0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a' } else { '5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb' }
    $ExpectedStartup = if ($Platform -eq 'Windows') { 'b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28' } else { '7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0' }
    Require ((Get-FileHash $AssemblyPath).Hash.ToLowerInvariant() -eq $ExpectedAssembly) 'Rust backing drift.'
    Require ((Get-FileHash $CommonPath).Hash.ToLowerInvariant() -eq $ExpectedCommon) 'Common backing drift.'
    Require ((Get-FileHash $StartupPath).Hash.ToLowerInvariant() -eq $ExpectedStartup) 'Startup backing drift.'
    $Resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    $Resolver.AddSearchDirectory($AssemblyDirectory); $Resolver.AddSearchDirectory($InputDirectory)
    $Reader = [Mono.Cecil.ReaderParameters]::new(); $Reader.AssemblyResolver = $Resolver
    $Original = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath, $Reader)
    $Common = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($CommonPath, $Reader)
    $Buffer = [IO.MemoryStream]::new(); $Copy = $null
    try {
        $Types = GetTypes $Original.MainModule
        $NPCs = [Collections.Generic.List[object]]::new()
        $Declarations = [Collections.Generic.List[object]]::new()
        $Callers = [Collections.Generic.List[object]]::new()
        $Names = @('InitializeHealth','ResetState','Respawn','RespawnAt')
        foreach ($Type in ($Types.Values | Sort-Object FullName)) {
            $Ancestors = @(GetAncestry $Type.FullName $Types)
            if ($Ancestors -contains 'NPCPlayer') { $NPCs.Add([ordered]@{ Type = $Type.FullName; Abstract = $Type.IsAbstract; Ancestors = $Ancestors }) }
            foreach ($Method in $Type.Methods) {
                if ($Ancestors -contains 'BasePlayer' -and $Method.Name -in $Names) {
                    $Declarations.Add([ordered]@{ Identity = $Method.FullName; Token = [int]$Method.MetadataToken.ToUInt32(); Virtual = $Method.IsVirtual })
                }
                if (!$Method.HasBody) { continue }
                foreach ($Instruction in $Method.Body.Instructions) {
                    if ($Instruction.Operand -isnot [Mono.Cecil.MethodReference]) { continue }
                    $Reference = $Instruction.Operand
                    if (($Reference.Name -eq 'InitializeHealth') -or ($Reference.DeclaringType.FullName -eq 'BasePlayer' -and $Reference.Name -in @('Respawn','RespawnAt'))) {
                        $Callers.Add([ordered]@{ Identity = $Method.FullName; Target = $Reference.FullName; TargetName = $Reference.Name; Offset = $Instruction.Offset; Op = $Instruction.OpCode.Name; NPC = $Ancestors -contains 'NPCPlayer' })
                    }
                }
            }
        }
        Require ($NPCs.Count -eq 20) 'NPC family drift.'
        Require ($Declarations.Count -eq 3 -and @($Declarations | Where-Object { $_.Identity -notlike '* BasePlayer::*' }).Count -eq 0) 'NPC reset override/declaration drift.'
        $InitializeCallers = @($Callers | Where-Object TargetName -eq 'InitializeHealth')
        Require ($InitializeCallers.Count -eq 4) ('InitializeHealth caller inventory drift: count=' + $InitializeCallers.Count + ' callers=' + (GetJson $Callers.ToArray()))
        Require (@($Callers | Where-Object NPC).Count -eq 0) 'New direct NPC reset caller needs review.'
        $Selected = @{}
        foreach ($TypeName in @('BaseCombatEntity','BasePlayer')) {
            foreach ($Method in $Types[$TypeName].Methods) {
                if ($Method.Name -notin $Names) { continue }
                $Selected[$Method.FullName] = @{ Method = $Method; Shape = GetShape $Method }
            }
        }
        Require ($Selected.Count -eq 5) 'Expected exactly five reset/respawn evidence bodies.'
        $Respawn = $Selected['System.Void BasePlayer::RespawnAt(UnityEngine.Vector3,UnityEngine.Quaternion,BaseEntity)'].Method
        Require (!$Respawn.IsVirtual -and $Respawn.Body.CodeSize -eq 618 -and $Respawn.Body.ExceptionHandlers.Count -eq 0) 'RespawnAt shape drift.'
        Require (@($Respawn.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' }).Count -eq 2) 'RespawnAt early/final return drift.'
        Require (@($Respawn.Body.Instructions | Where-Object { $_.Offset -eq 0x17c -and $_.OpCode.Name -eq 'callvirt' -and $_.Operand.Name -eq 'InitializeHealth' }).Count -eq 1) 'Alive-reset call seam drift.'
        Require (@($Respawn.Body.Instructions | Where-Object { $_.Offset -eq 0x269 -and $_.OpCode.Name -eq 'ret' }).Count -eq 1) 'Full RespawnAt terminal return drift.'
        foreach ($Identity in @('System.Void BasePlayer::ResetState()','System.Void BaseCombatEntity::ResetState()')) {
            Require (@($Selected[$Identity].Method.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'lifestate' }).Count -eq 0) 'ResetState now writes lifestate.'
        }
        $OriginalBytes = [IO.File]::ReadAllBytes($AssemblyPath); $OriginalSections = @(GetSections $OriginalBytes)
        $IPlayer = $Common.MainModule.GetType('Oxide.Core.Libraries.Covalence','IPlayer')
        Require (@($Types['BasePlayer'].Fields | Where-Object Name -eq 'IPlayer').Count -eq 0) 'Input already publicized.'
        $Types['BasePlayer'].Fields.Add([Mono.Cecil.FieldDefinition]::new('IPlayer',[Mono.Cecil.FieldAttributes]134,$Original.MainModule.ImportReference($IPlayer)))
        $Original.Write($Buffer); $ProcessedBytes = $Buffer.ToArray(); $Buffer.Position = 0
        $Copy = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Buffer,$Reader)
        $ProcessedSections = @(GetSections $ProcessedBytes)
        $Directory = Join-Path $OutputDirectory $Platform; [void](New-Item -ItemType Directory -Path $Directory)
        foreach ($Type in $Copy.MainModule.Types) {
            foreach ($Method in $Type.Methods) {
                if (!$Selected.ContainsKey($Method.FullName)) { continue }
                $Before = $Selected[$Method.FullName].Shape; $After = GetShape $Method
                foreach ($Key in @('Text','ExpectedEH','Locals','InitLocals','MaxStack','CodeSize','Token','Virtual','NewSlot')) {
                    Require ((GetJson $Before[$Key]) -ceq (GetJson $After[$Key])) ('Processed semantic/EH drift: ' + $Method.FullName + '/' + $Key)
                }
                $Raw = GetRaw $Method $ProcessedBytes $ProcessedSections
                $OriginalRaw = GetRaw $Selected[$Method.FullName].Method $OriginalBytes $OriginalSections
                $Stem = '0x{0:x8}' -f $After.Token
                [IO.File]::WriteAllBytes((Join-Path $Directory ($Stem + '.processed.il.bin')),$Raw)
                [IO.File]::WriteAllBytes((Join-Path $Directory ($Stem + '.original.il.bin')),$OriginalRaw)
                [IO.File]::WriteAllText((Join-Path $Directory ($Stem + '.il.txt')),$After.Text,[Text.UTF8Encoding]::new($false))
                $RawHash = GetHash $Raw
                if ($Method.Name -eq 'RespawnAt') {
                    $Expected = if ($Platform -eq 'Windows') { 'ae7730d132edacf55b32d62947aeba9149cdac022862bc7266456d629d8d7f17' } else { '30f2bd0521a68902d2acce8687014f10f6f92938e4e2690576308311ab475bc5' }
                    Require ($RawHash -eq $Expected) 'Existing A live RespawnAt anchor mismatch.'
                }
                $Pins.Add([ordered]@{ Platform = $Platform; Identity = $Method.FullName; Token = $After.Token; TokenHex = $Stem; CodeSize = $After.CodeSize; ProcessedRawSHA256 = $RawHash; OriginalRawSHA256 = GetHash $OriginalRaw; ILTextSHA256 = $After.ILTextSHA256; ExpectedEH = $After.ExpectedEH; Shape = $After; SemanticParity = $true })
            }
        }
        $Reports.Add([ordered]@{ Platform = $Platform; AssemblySHA256 = $ExpectedAssembly; NPCs = $NPCs.ToArray(); ResetDeclarations = $Declarations.ToArray(); Callers = $Callers.ToArray() })
    } finally { if ($Copy) { $Copy.Dispose() }; $Buffer.Dispose(); $Original.Dispose(); $Common.Dispose(); $Resolver.Dispose() }
}
Require ($Pins.Count -eq 10) 'Incomplete five-body/two-platform evidence.'
Require ((GetJson $Reports[0].NPCs) -ceq (GetJson $Reports[1].NPCs)) 'NPC ancestry parity drift.'
Require ((GetJson $Reports[0].Callers) -ceq (GetJson $Reports[1].Callers)) 'Caller parity drift.'
foreach ($Pin in @($Pins | Where-Object Platform -eq 'Windows')) {
    $Other = @($Pins | Where-Object { $_.Platform -eq 'Linux' -and $_.Identity -eq $Pin.Identity })[0]
    foreach ($Key in @('Token','CodeSize','ILTextSHA256','ExpectedEH','Shape')) { Require ((GetJson $Pin[$Key]) -ceq (GetJson $Other[$Key])) 'Cross-platform reset shape drift.' }
}
$Receipt = [ordered]@{
    Status = 'STATIC NPC RESET RESEARCH; no B2 reset carrier or live regression qualified'
    HelperSHA256 = (Get-FileHash $HelperPath).Hash.ToLowerInvariant()
    Scope = 'Five method bodies only; 20 NPC ancestry entries; direct reset/respawn caller references. No Spawn/removal requalification.'
    Platforms = $Reports.ToArray(); Pins = $Pins.ToArray()
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'NpcEpisodeReset.json'),(ConvertTo-Json -InputObject $Receipt -Depth 30),[Text.UTF8Encoding]::new($false))
$Lines = [Collections.Generic.List[string]]::new()
$Lines.Add("Identity`tTokenHex`tCodeSize`tWindowsProcessedSHA256`tLinuxProcessedSHA256`tEH")
foreach ($Pin in ($Pins | Where-Object Platform -eq 'Windows' | Sort-Object Token)) {
    $Other = @($Pins | Where-Object { $_.Platform -eq 'Linux' -and $_.Identity -eq $Pin.Identity })[0]
    $Lines.Add("$($Pin.Identity)`t$($Pin.TokenHex)`t$($Pin.CodeSize)`t$($Pin.ProcessedRawSHA256)`t$($Other.ProcessedRawSHA256)`t$(GetJson $Pin.ExpectedEH)")
}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'NpcEpisodeResetPins.tsv'),$Lines,[Text.UTF8Encoding]::new($false))
Write-Output "[Research:NpcEpisode] Static PASS: five bodies/platform, 20 NPC types, semantic/EH parity; existing A RespawnAt live anchors match. Output=$OutputDirectory"
