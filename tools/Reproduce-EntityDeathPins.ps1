# B2 sidecar: reproduce selected Carbon-publicized body bytes using Cecil only.
# Never loads Rust/Common/Startup through Reflection.Assembly, changes a host,
# installs a patch, or launches a server. All generated evidence is external.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$WindowsCommon,
    [Parameter(Mandatory = $true)][string]$LinuxCommon,
    [Parameter(Mandatory = $true)][string]$WindowsStartup,
    [Parameter(Mandatory = $true)][string]$LinuxStartup,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string[]]$DependencyDirectory = @(),
    # Future main fixture schema: { Methods: [{ Platform, Identity, Token (int),
    # CodeSize, RawSHA256, ExpectedEH: [{ Kind, TryOffset, TryLength,
    # HandlerOffset, HandlerLength, CatchType, FilterOffset }] }] }
    [string]$LivePinsPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/')
if ($OutputDirectory -eq $Repository -or $OutputDirectory.StartsWith($Repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '[Research:DeathPins] Evidence must be external.'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw '[Research:DeathPins] Use a fresh evidence directory.' }
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path

function Require([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw ('[Research:DeathPins] ' + $Message) }
}
function GetHash([byte[]]$Bytes) {
    $Hash = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($Hash.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $Hash.Dispose() }
}
function GetTypes($Module) {
    $Types = @{}
    $Pending = [Collections.Generic.Queue[object]]::new()
    foreach ($Type in $Module.Types) { $Pending.Enqueue($Type) }
    while ($Pending.Count) {
        $Type = $Pending.Dequeue()
        Require ($Types.Count -lt 8192) 'Cold type inventory exceeds 8192.'
        $Types[$Type.FullName] = $Type
        foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
    }
    return $Types
}
function GetAncestry([string]$Name, $Types) {
    $Seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    while ($Types.ContainsKey($Name)) {
        Require ($Seen.Add($Name)) 'Ancestry cycle.'
        $Name
        $Base = $Types[$Name].BaseType
        if (!$Base) { break }
        $Name = if ($Base -is [Mono.Cecil.GenericInstanceType]) { $Base.ElementType.FullName } else { $Base.FullName }
    }
}
function TestSlot($Method, [string]$Name) {
    return $Method.Name -eq $Name -and $Method.IsVirtual -and !$Method.IsStatic -and
        $Method.ReturnType.FullName -eq 'System.Void' -and $Method.Parameters.Count -eq 1 -and
        $Method.Parameters[0].ParameterType.FullName -eq 'HitInfo'
}
function GetEffective([string[]]$Ancestry, [string]$Name, $Types) {
    foreach ($Parent in $Ancestry) {
        $Matches = @($Types[$Parent].Methods | Where-Object { TestSlot $_ $Name })
        Require ($Matches.Count -le 1) ('Ambiguous slot: ' + $Parent + '.' + $Name)
        if ($Matches.Count) { return $Matches[0].FullName }
    }
    return $null
}
function GetSections([byte[]]$Bytes) {
    $Pe = [BitConverter]::ToInt32($Bytes, 0x3c)
    Require ([BitConverter]::ToUInt32($Bytes, $Pe) -eq 0x4550) 'Invalid PE signature.'
    $Count = [BitConverter]::ToUInt16($Bytes, $Pe + 6)
    $Start = $Pe + 24 + [BitConverter]::ToUInt16($Bytes, $Pe + 20)
    for ($Index = 0; $Index -lt $Count; $Index++) {
        $Offset = $Start + 40 * $Index
        [pscustomobject]@{
            RVA = [BitConverter]::ToUInt32($Bytes, $Offset + 12)
            Size = [Math]::Max([BitConverter]::ToUInt32($Bytes, $Offset + 8), [BitConverter]::ToUInt32($Bytes, $Offset + 16))
            Raw = [BitConverter]::ToUInt32($Bytes, $Offset + 20)
        }
    }
}
function GetRaw($Method, [byte[]]$Bytes, $Sections) {
    foreach ($Section in $Sections) {
        if ($Method.RVA -lt $Section.RVA -or $Method.RVA -ge $Section.RVA + $Section.Size) { continue }
        $Offset = [int]($Section.Raw + $Method.RVA - $Section.RVA)
        $Kind = $Bytes[$Offset] -band 3
        if ($Kind -eq 2) { $Start = $Offset + 1; $Size = $Bytes[$Offset] -shr 2 }
        elseif ($Kind -eq 3) {
            $Start = $Offset + (([BitConverter]::ToUInt16($Bytes, $Offset) -shr 12) * 4)
            $Size = [BitConverter]::ToInt32($Bytes, $Offset + 4)
        } else { throw '[Research:DeathPins] Unsupported CLI body header.' }
        Require ($Size -eq $Method.Body.CodeSize -and $Size -gt 0 -and $Start + $Size -le $Bytes.Length) 'Invalid CLI body extent.'
        $Raw = [byte[]]::new($Size)
        [Array]::Copy($Bytes, $Start, $Raw, 0, $Size)
        return ,$Raw
    }
    throw '[Research:DeathPins] Body RVA outside sections.'
}
function GetEH($Method) {
    foreach ($Handler in $Method.Body.ExceptionHandlers) {
        $TryEnd = if ($Handler.TryEnd) { $Handler.TryEnd.Offset } else { $Method.Body.CodeSize }
        $HandlerEnd = if ($Handler.HandlerEnd) { $Handler.HandlerEnd.Offset } else { $Method.Body.CodeSize }
        [ordered]@{
            Kind = $Handler.HandlerType.ToString(); TryOffset = $Handler.TryStart.Offset
            TryLength = $TryEnd - $Handler.TryStart.Offset
            HandlerOffset = $Handler.HandlerStart.Offset; HandlerLength = $HandlerEnd - $Handler.HandlerStart.Offset
            CatchType = $(if ($Handler.CatchType) { $Handler.CatchType.FullName } else { $null })
            FilterOffset = $(if ($Handler.FilterStart) { $Handler.FilterStart.Offset } else { -1 })
        }
    }
}
function GetShape($Method) {
    $Text = @($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
    return [ordered]@{
        Text = $Text; ILTextSHA256 = GetHash ([Text.Encoding]::UTF8.GetBytes($Text))
        ExpectedEH = @(GetEH $Method); CodeSize = $Method.Body.CodeSize
        Instructions = $Method.Body.Instructions.Count; InitLocals = $Method.Body.InitLocals
        Locals = @($Method.Body.Variables | ForEach-Object { $_.VariableType.FullName })
        MaxStack = $Method.Body.MaxStackSize; Token = [int]$Method.MetadataToken.ToUInt32()
        Virtual = $Method.IsVirtual; NewSlot = $Method.IsNewSlot
    }
}
function GetJson($Value) { return ConvertTo-Json -InputObject $Value -Depth 30 -Compress }
function SaveJson([string]$Name, $Value) {
    [IO.File]::WriteAllText((Join-Path $OutputDirectory $Name), (ConvertTo-Json -InputObject $Value -Depth 30), [Text.UTF8Encoding]::new($false))
}

$Pinned = @{
    Windows = @{
        Assembly = 'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2'
        Common = '0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a'
        Startup = 'b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28'
    }
    Linux = @{
        Assembly = 'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450'
        Common = '5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb'
        Startup = '7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0'
    }
}
$OwnerPins = @{
    'System.Boolean Carbon.Publicizer.AssemblyCSharp::Execute()' = 'af32d0c1ea495d16a2ce10b3a10369d8ea6c1901e170ac9ea413af34dcc26b44'
    'System.Void Carbon.Publicizer.AssemblyCSharp::InjectIPlayer()' = '7203f6d8341f7b13d338d640edb811c03d071b45cc82cbda52351d1a3dc478fe'
    'System.Boolean Carbon.Publicizer.Patch::Execute()' = '21f1febe85e18ea4392745c03e66a6d89b52c3998f60a98c5f37f640e9f8ca9c'
    'System.Void Carbon.Publicizer.Patch::UpdateBuffer()' = '3f1d5e12cb762a23345d1ffe1a521e5c8ebb9e9d560969390939d898e9977157'
    'System.Void Carbon.Publicizer.Patch::Load()' = 'd2c245e0bad724cc7baf23d50a9c46c86c5635ef820d3435833006b423af21eb'
}
$Inputs = @(
    @{ Platform = 'Windows'; Assembly = $WindowsAssembly; Common = $WindowsCommon; Startup = $WindowsStartup },
    @{ Platform = 'Linux'; Assembly = $LinuxAssembly; Common = $LinuxCommon; Startup = $LinuxStartup }
)
$AllPins = [Collections.Generic.List[object]]::new()
$Ledgers = [Collections.Generic.List[object]]::new()
$Owners = [Collections.Generic.List[object]]::new()
[void](New-Item -ItemType Directory -Path $OutputDirectory)
foreach ($Input in $Inputs) {
    $Platform = $Input.Platform
    foreach ($Key in @('Assembly', 'Common', 'Startup')) {
        $Input[$Key] = (Resolve-Path -LiteralPath $Input[$Key]).Path
        Require ((Get-FileHash -LiteralPath $Input[$Key] -Algorithm SHA256).Hash.ToLowerInvariant() -eq $Pinned[$Platform][$Key]) ('Backing binary drift: ' + $Platform + '/' + $Key)
    }
    $Resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    foreach ($Directory in @([IO.Path]::GetDirectoryName($Input.Assembly), [IO.Path]::GetDirectoryName($Input.Common)) + $DependencyDirectory) {
        $Resolver.AddSearchDirectory((Resolve-Path -LiteralPath $Directory).Path)
    }
    $Reader = [Mono.Cecil.ReaderParameters]::new()
    $Reader.AssemblyResolver = $Resolver
    $Original = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Input.Assembly, $Reader)
    $Common = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Input.Common, $Reader)
    $Startup = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Input.Startup, $Reader)
    $Buffer = [IO.MemoryStream]::new()
    $Copy = $null
    try {
        $OwnerCount = 0
        foreach ($Type in $Startup.MainModule.Types) {
            foreach ($Method in $Type.Methods) {
                if (!$OwnerPins.ContainsKey($Method.FullName)) { continue }
                $Shape = GetShape $Method
                Require ($Shape.ILTextSHA256 -eq $OwnerPins[$Method.FullName]) ('Transform instruction drift: ' + $Method.FullName)
                $Owners.Add([ordered]@{ Platform = $Platform; Identity = $Method.FullName; Token = $Shape.Token; CodeSize = $Shape.CodeSize; ILTextSHA256 = $Shape.ILTextSHA256; ExpectedEH = $Shape.ExpectedEH })
                $OwnerCount++
            }
        }
        Require ($OwnerCount -eq $OwnerPins.Count) 'Incomplete pinned transformation owner inventory.'
        $Types = GetTypes $Original.MainModule
        $Selected = @{}
        $Ledger = [Collections.Generic.List[object]]::new()
        foreach ($Type in ($Types.Values | Sort-Object FullName)) {
            $Ancestry = @(GetAncestry $Type.FullName $Types)
            $Combat = $Ancestry -contains 'BaseCombatEntity'
            $Resource = $Ancestry -contains 'ResourceEntity'
            if (!$Combat -and !$Resource) { continue }
            $Die = GetEffective $Ancestry 'Die' $Types
            $OnDied = GetEffective $Ancestry 'OnDied' $Types
            $Kind = if ($Resource) { 'ResourceKilled' } elseif ($Ancestry -contains 'BaseVehicleModule') { 'NoopVehicleModule' }
                elseif ($Ancestry -contains 'BasePlayer') { 'ExistingBasePlayerCarrier' }
                elseif ($Ancestry -contains 'SamSite') { 'OuterSamSite' }
                elseif ($Ancestry -contains 'BuildingPrivlidge') { 'OuterBuildingPrivlidge' } else { 'FullBaseDie' }
            $Ledger.Add([ordered]@{
                Type = $Type.FullName; TypeToken = [int]$Type.MetadataToken.ToUInt32(); Base = $(if ($Ancestry.Count -gt 1) { $Ancestry[1] } else { $null })
                Abstract = $Type.IsAbstract; Ancestry = $Ancestry; Combat = $Combat; Resource = $Resource
                Player = $Ancestry -contains 'BasePlayer'; NPCPlayer = $Ancestry -contains 'NPCPlayer'
                EffectiveDie = $Die; EffectiveOnDied = $OnDied; CompletionClass = $Kind
                OnDiedCanRestoreAlive = $Ancestry -contains 'WildlifeHazard'
                QualifiedALifeEpisodeRequired = $Kind -eq 'ExistingBasePlayerCarrier' -and !($Ancestry -contains 'NPCPlayer')
                NPCRequiresSeparateQualification = $Ancestry -contains 'NPCPlayer'
                ResetObserver = $(if ($Ancestry -contains 'TrainCar') { 'System.Void TrainCar::DoRepair(BasePlayer)' }
                    elseif ($Ancestry -contains 'SamSite') { 'System.Void SamSite::SelfHeal()' } else { $null })
            })
            foreach ($Method in $Type.Methods) {
                $Core = (TestSlot $Method 'Die') -and $Combat
                $Topology = (TestSlot $Method 'OnDied') -and $Combat
                $ResourceTarget = (TestSlot $Method 'OnDied') -and $Resource
                $Extra = $Method.FullName -in @('System.Void BaseCombatEntity::DieInstantly()', 'System.Void TrainCar::DoRepair(BasePlayer)', 'System.Void SamSite::SelfHeal()')
                if (!$Core -and !$Topology -and !$ResourceTarget -and !$Extra) { continue }
                Require ($Method.HasBody -and !$Method.IsAbstract -and $Method.Overrides.Count -eq 0) ('Unqualified method shape: ' + $Method.FullName)
                if ($Core -or $Topology) { Require ($Method.IsNewSlot -eq ($Type.FullName -eq 'BaseCombatEntity')) 'Unexpected hiding combat virtual slot.' }
                $Selected[$Method.FullName] = @{ Method = $Method; Shape = GetShape $Method; Role = $(if ($Topology) { 'CombatOnDiedTopology' } elseif ($ResourceTarget) { 'ResourceOuterCompletion' } else { 'CoreCandidate' }) }
            }
        }
        Require (@($Selected.Values | Where-Object Role -eq 'CombatOnDiedTopology').Count -eq 55) 'Pinned 55-OnDied inventory drift.'
        Require ($Selected.Count -eq 67) 'Pinned 12 core/resource/reset plus 55 topology inventory drift.'
        $OriginalBytes = [IO.File]::ReadAllBytes($Input.Assembly)
        $OriginalSections = @(GetSections $OriginalBytes)
        # Exactly the selected-body reproduction used by Gameplay A. The
        # transformation owner also publicizes metadata / injects bootstrap;
        # this is NOT a claim of byte-identical whole processed PE recreation.
        $Player = $Types['BasePlayer']
        Require (@($Player.Fields | Where-Object Name -eq 'IPlayer').Count -eq 0) 'Input already has IPlayer.'
        $IPlayer = $Common.MainModule.GetType('Oxide.Core.Libraries.Covalence', 'IPlayer')
        Require ($null -ne $IPlayer -and $IPlayer.IsInterface) 'Pinned Common IPlayer missing.'
        $Player.Fields.Add([Mono.Cecil.FieldDefinition]::new('IPlayer', [Mono.Cecil.FieldAttributes]134, $Original.MainModule.ImportReference($IPlayer)))
        $Original.Write($Buffer)
        $ProcessedBytes = $Buffer.ToArray()
        $Buffer.Position = 0
        $Copy = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Buffer, $Reader)
        $ProcessedSections = @(GetSections $ProcessedBytes)
        $ProcessedTypes = GetTypes $Copy.MainModule
        Require ($Types.Count -eq $ProcessedTypes.Count) 'Transformation changed type inventory.'
        $Field = @($ProcessedTypes['BasePlayer'].Fields | Where-Object Name -eq 'IPlayer')
        Require ($Field.Count -eq 1 -and [int]$Field[0].Attributes -eq 134 -and $Field[0].FieldType.FullName -eq $IPlayer.FullName) 'Injected field shape drift.'
        $Directory = Join-Path $OutputDirectory $Platform
        [void](New-Item -ItemType Directory -Path $Directory)
        $Found = 0
        foreach ($Type in $ProcessedTypes.Values) {
            foreach ($Method in $Type.Methods) {
                if (!$Selected.ContainsKey($Method.FullName)) { continue }
                $Before = $Selected[$Method.FullName].Shape
                $After = GetShape $Method
                foreach ($Key in @('Text', 'ExpectedEH', 'Locals', 'InitLocals', 'MaxStack', 'CodeSize', 'Instructions', 'Token', 'Virtual', 'NewSlot')) {
                    Require ((GetJson $Before[$Key]) -ceq (GetJson $After[$Key])) ('Selected-body semantic/EH/shape drift: ' + $Method.FullName + '/' + $Key)
                }
                Require ($After.CodeSize -le 4096 -and $After.Instructions -le 1024) 'Target exceeds existing decoder bounds.'
                $Raw = GetRaw $Method $ProcessedBytes $ProcessedSections
                $OriginalRaw = GetRaw $Selected[$Method.FullName].Method $OriginalBytes $OriginalSections
                $Stem = '0x{0:x8}' -f $After.Token
                [IO.File]::WriteAllBytes((Join-Path $Directory ($Stem + '.processed.il.bin')), $Raw)
                [IO.File]::WriteAllBytes((Join-Path $Directory ($Stem + '.original.il.bin')), $OriginalRaw)
                [IO.File]::WriteAllText((Join-Path $Directory ($Stem + '.resolved.il.txt')), $After.Text, [Text.UTF8Encoding]::new($false))
                $AllPins.Add([ordered]@{
                    Platform = $Platform; Identity = $Method.FullName; DeclaringType = $Type.FullName; Name = $Method.Name
                    Token = $After.Token; TokenHex = $Stem; Role = $Selected[$Method.FullName].Role
                    CodeSize = $After.CodeSize; RawSHA256 = GetHash $Raw; OriginalRawSHA256 = GetHash $OriginalRaw
                    ILTextSHA256 = $After.ILTextSHA256; ExpectedEH = $After.ExpectedEH
                    InitLocals = $After.InitLocals; Locals = $After.Locals; Instructions = $After.Instructions
                    OriginalMaxStack = $Before.MaxStack; ProcessedMaxStack = $After.MaxStack
                    SemanticParity = $true; EHParity = $true; LiveMatch = $false
                })
                $Found++
            }
        }
        Require ($Found -eq $Selected.Count) 'Transformation lost a selected method.'
        foreach ($Row in $Ledger) {
            $ProcessedType = $ProcessedTypes[$Row.Type]
            $ProcessedAncestry = @(GetAncestry $Row.Type $ProcessedTypes)
            Require ([int]$ProcessedType.MetadataToken.ToUInt32() -eq $Row.TypeToken -and $ProcessedType.IsAbstract -eq $Row.Abstract) ('Processed type shape drift: ' + $Row.Type)
            Require ((GetJson $ProcessedAncestry) -ceq (GetJson $Row.Ancestry)) ('Processed ancestry drift: ' + $Row.Type)
            Require ((GetEffective $ProcessedAncestry 'Die' $ProcessedTypes) -ceq $Row.EffectiveDie -and
                (GetEffective $ProcessedAncestry 'OnDied' $ProcessedTypes) -ceq $Row.EffectiveOnDied) ('Processed effective-method drift: ' + $Row.Type)
        }
        $Ledgers.Add([ordered]@{
            Platform = $Platform; AssemblySHA256 = $Pinned[$Platform].Assembly; CommonSHA256 = $Pinned[$Platform].Common
            StartupSHA256 = $Pinned[$Platform].Startup; OriginalMVID = $Original.MainModule.Mvid.ToString()
            StartupMVID = $Startup.MainModule.Mvid.ToString(); CommonMVID = $Common.MainModule.Mvid.ToString()
            ColdTypeCap = 8192; ActualModuleTypes = $Types.Count; Types = $Ledger.ToArray()
            HotLookup = 'Exact original System.Type key in a precomputed bounded ledger; no hot assembly/ancestry scan.'
        })
        Write-Output "[Research:DeathPins] $Platform methods=$Found types=$($Types.Count) ledger=$($Ledger.Count) selected semantic/EH parity=PASS"
    } finally {
        if ($Copy) { $Copy.Dispose() }
        $Buffer.Dispose(); $Original.Dispose(); $Common.Dispose(); $Startup.Dispose(); $Resolver.Dispose()
    }
}
$WindowsPins = @($AllPins | Where-Object Platform -eq 'Windows' | Sort-Object Identity)
$LinuxPins = @($AllPins | Where-Object Platform -eq 'Linux' | Sort-Object Identity)
for ($Index = 0; $Index -lt $WindowsPins.Count; $Index++) {
    foreach ($Key in @('Identity', 'Token', 'CodeSize', 'ILTextSHA256', 'ExpectedEH', 'Locals', 'InitLocals')) {
        Require ((GetJson $WindowsPins[$Index][$Key]) -ceq (GetJson $LinuxPins[$Index][$Key])) ('Cross-platform semantic/slot drift: ' + $WindowsPins[$Index].Identity + '/' + $Key)
    }
}
# Anchors were independently matched against Gameplay A LIVE receipts. Newly
# reproduced B2 bodies remain candidates, even when they share this transform.
foreach ($Pin in $AllPins) {
    $Anchor = switch ($Pin.Identity) {
        'System.Void BaseCombatEntity::Die(HitInfo)' { 'ab193eb2cb6a1abe502927eb2ca174993e02674b8883b265a2931e12b661c363' }
        'System.Void BasePlayer::Die(HitInfo)' { '3e6d22f6ace067b6b16a8cc209946f91d39c605bd6c43e5aa61b190e5662b731' }
        'System.Void BasePlayer::OnDied(HitInfo)' {
            if ($Pin.Platform -eq 'Windows') { '617412ad28c15aedd49143a2057e1b4c9c72ce835c97e12b1bc864a83576e9c7' }
            else { '2462879e57b67cbac0ea8f0e079c353cca437458f422554cf1de51d12ec40b1b' }
        }
        default { $null }
    }
    if ($Anchor) { Require ($Pin.RawSHA256 -eq $Anchor) ('Gameplay A processed-live anchor mismatch: ' + $Pin.Identity) }
}
$LeftLedger = @($Ledgers[0].Types | ForEach-Object { $Row = [ordered]@{}; foreach ($Key in $_.Keys) { if ($Key -ne 'TypeToken') { $Row[$Key] = $_[$Key] } }; $Row })
$RightLedger = @($Ledgers[1].Types | ForEach-Object { $Row = [ordered]@{}; foreach ($Key in $_.Keys) { if ($Key -ne 'TypeToken') { $Row[$Key] = $_[$Key] } }; $Row })
Require ((GetJson $LeftLedger) -ceq (GetJson $RightLedger)) 'Cross-platform derived type/effective-method ledger drift.'
if ($LivePinsPath) {
    $Live = Get-Content -Raw -LiteralPath $LivePinsPath | ConvertFrom-Json
    Require (@($Live.Methods).Count -eq $AllPins.Count) 'Live fixture must include all 134 platform/target rows.'
    foreach ($Pin in $AllPins) {
        $Matches = @($Live.Methods | Where-Object { $_.Platform -ceq $Pin.Platform -and $_.Identity -ceq $Pin.Identity })
        Require ($Matches.Count -eq 1) ('Missing/duplicate live pin: ' + $Pin.Identity)
        foreach ($Key in @('Token', 'CodeSize', 'RawSHA256')) { Require ($Matches[0].$Key -ceq $Pin[$Key]) ('Live fixture mismatch: ' + $Pin.Identity + '/' + $Key) }
        Require ((GetJson @($Matches[0].ExpectedEH)) -ceq (GetJson $Pin.ExpectedEH)) ('Live EH mismatch: ' + $Pin.Identity)
        $Pin.LiveMatch = $true
    }
}
$Combined = [Collections.Generic.List[object]]::new()
foreach ($Pin in $WindowsPins) {
    $Linux = @($LinuxPins | Where-Object Identity -ceq $Pin.Identity)[0]
    $Combined.Add([ordered]@{
        Identity = $Pin.Identity; Token = $Pin.Token; TokenHex = $Pin.TokenHex; CodeSize = $Pin.CodeSize; Role = $Pin.Role
        WinSHA = $Pin.RawSHA256; LinuxSHA = $Linux.RawSHA256; ExpectedEH = $Pin.ExpectedEH; ILTextSHA256 = $Pin.ILTextSHA256
    })
}
SaveJson 'ProcessedPins.json' @{
    Status = $(if ($LivePinsPath) { 'MATCHED supplied live fixture; topology qualification still separate' } else { 'REPRODUCED CANDIDATES; future main LIVE fixture must match' })
    CecilSHA256 = (Get-FileHash -LiteralPath $CecilPath -Algorithm SHA256).Hash.ToLowerInvariant()
    CecilVersion = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $CecilPath).Path).Version.ToString()
    Transform = 'Import Common IPlayer as BasePlayer field Public|NotSerialized (134), serialize to memory; selected-body reproduction, not full-PE equivalence'
    Methods = $AllPins.ToArray(); Combined = $Combined.ToArray(); TransformOwners = $Owners.ToArray()
}
SaveJson 'DerivedTypeLedger.json' @{ Platforms = $Ledgers.ToArray(); CrossPlatformSemanticParity = $true }
$Lines = [Collections.Generic.List[string]]::new()
$Lines.Add("Identity`tToken`tTokenHex`tCodeSize`tRole`tWinSHA`tLinuxSHA`tExpectedEH")
foreach ($Pin in $Combined) { $Lines.Add("$($Pin.Identity)`t$($Pin.Token)`t$($Pin.TokenHex)`t$($Pin.CodeSize)`t$($Pin.Role)`t$($Pin.WinSHA)`t$($Pin.LinuxSHA)`t$(GetJson $Pin.ExpectedEH)") }
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'ProcessedPins.tsv'), $Lines, [Text.UTF8Encoding]::new($false))
Write-Output "[Research:DeathPins] PASS: 67 unique targets/platform = 12 core/resource/reset candidates + 55 combat OnDied topology bodies; 467 combat/resource type rows/platform; six A raw anchors match. Output=$OutputDirectory"
Write-Output '[Research:DeathPins] No Rust reflection load or host changes. New hashes remain unqualified until the main live fixture and topology checks pass.'
