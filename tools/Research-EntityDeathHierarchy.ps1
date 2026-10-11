# Static B2 research only: no Rust loading, host calls, patches, or server startup.
# Generated JSON, decoded IL, and original PE method bytes stay outside the checkout.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/')
if ($OutputDirectory -eq $Repository -or $OutputDirectory.StartsWith($Repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '[Research:EntityDeath] Evidence must be outside the repository.'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw '[Research:EntityDeath] Use a new evidence directory; existing evidence is never overwritten.' }
[void](New-Item -ItemType Directory -Path $OutputDirectory)
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path

function GetHash([byte[]]$Bytes) {
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($Hasher.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $Hasher.Dispose() }
}
function GetToken($Member) { return '0x{0:x8}' -f $Member.MetadataToken.ToUInt32() }
function GetAncestors([string]$Name, $Types) {
    $Seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    while ($Types.ContainsKey($Name)) {
        if (!$Seen.Add($Name)) { throw '[Research:EntityDeath] Cyclic ancestry.' }
        $Name
        $Type = $Types[$Name]
        if (!$Type.BaseType) { break }
        $Name = $Type.BaseType.FullName
        # Generic constructed references use the same declaration for ancestry.
        if ($Type.BaseType -is [Mono.Cecil.GenericInstanceType]) { $Name = $Type.BaseType.ElementType.FullName }
    }
}
function TestDeathSignature($Method) {
    return $Method.Name -in @('Die', 'OnDied') -and $Method.Parameters.Count -eq 1 -and $Method.Parameters[0].ParameterType.FullName -eq 'HitInfo'
}
function GetRawBody($Method, [byte[]]$Image, $Sections) {
    foreach ($Section in $Sections) {
        if ($Method.RVA -lt $Section.VirtualAddress -or $Method.RVA -ge $Section.VirtualAddress + $Section.Size) { continue }
        $Offset = [int]($Section.RawAddress + $Method.RVA - $Section.VirtualAddress)
        if (($Image[$Offset] -band 3) -eq 2) {
            $Size = $Image[$Offset] -shr 2
            $Start = $Offset + 1
        } elseif (($Image[$Offset] -band 3) -eq 3) {
            $Header = [BitConverter]::ToUInt16($Image, $Offset)
            $Start = $Offset + (($Header -shr 12) * 4)
            $Size = [BitConverter]::ToInt32($Image, $Offset + 4)
        } else { throw '[Research:EntityDeath] Unsupported CLI body header.' }
        if ($Size -ne $Method.Body.CodeSize -or $Start + $Size -gt $Image.Length) { throw '[Research:EntityDeath] Invalid method extent.' }
        $Bytes = [byte[]]::new($Size)
        [Array]::Copy($Image, $Start, $Bytes, 0, $Size)
        return ,$Bytes
    }
    throw '[Research:EntityDeath] Method RVA is outside PE sections.'
}
function GetInstructions($Method) {
    foreach ($Instruction in $Method.Body.Instructions) {
        $Operand = $Instruction.Operand
        $Target = $null
        $Targets = @()
        if ($Operand -is [Mono.Cecil.Cil.Instruction]) { $Targets = @($Operand.Offset) }
        elseif ($Operand -is [Mono.Cecil.Cil.Instruction[]]) { $Targets = @($Operand | ForEach-Object { $_.Offset }) }
        elseif ($Operand -is [Mono.Cecil.MethodReference]) { $Target = $Operand.FullName }
        [ordered]@{
            Offset = $Instruction.Offset; Op = $Instruction.OpCode.Name
            Flow = $Instruction.OpCode.FlowControl.ToString(); Operand = [string]$Operand
            Target = $Target; Targets = $Targets; Text = $Instruction.ToString()
        }
    }
}
function GetMethodRecord($Method, [string]$Category, [string]$PlatformDirectory, [byte[]]$Image, $Sections) {
    $RawFile = $null; $RawHash = $null; $TextHash = $null; $Size = 0
    $Instructions = @(); $Handlers = @(); $LocalTypes = @()
    if ($Method.HasBody) {
        $Instructions = @(GetInstructions $Method)
        $Text = ($Instructions | ForEach-Object { $_.Text }) -join "`n"
        $TextHash = GetHash ([Text.Encoding]::UTF8.GetBytes($Text))
        $Raw = GetRawBody $Method $Image $Sections
        $RawHash = GetHash $Raw
        $Size = $Raw.Length
        $Stem = GetToken $Method
        $RawFile = Join-Path $PlatformDirectory ($Stem + '.il.bin')
        [IO.File]::WriteAllBytes($RawFile, $Raw)
        [IO.File]::WriteAllText((Join-Path $PlatformDirectory ($Stem + '.il.txt')), $Text, [Text.UTF8Encoding]::new($false))
        $LocalTypes = @($Method.Body.Variables | ForEach-Object { $_.VariableType.FullName })
        $Handlers = @($Method.Body.ExceptionHandlers | ForEach-Object {
            [ordered]@{
                Kind = $_.HandlerType.ToString(); TryStart = $_.TryStart.Offset
                TryEnd = $(if ($_.TryEnd) { $_.TryEnd.Offset } else { $Size })
                HandlerStart = $_.HandlerStart.Offset
                HandlerEnd = $(if ($_.HandlerEnd) { $_.HandlerEnd.Offset } else { $Size })
                CatchType = $(if ($_.CatchType) { $_.CatchType.FullName } else { $null })
                FilterStart = $(if ($_.FilterStart) { $_.FilterStart.Offset } else { $null })
            }
        })
    }
    return [ordered]@{
        Identity = $Method.FullName; Type = $Method.DeclaringType.FullName; Name = $Method.Name
        Token = GetToken $Method; ReturnType = $Method.ReturnType.FullName
        Parameters = @($Method.Parameters | ForEach-Object { $_.ParameterType.FullName })
        Virtual = $Method.IsVirtual; NewSlot = $Method.IsNewSlot; Final = $Method.IsFinal
        Abstract = $Method.IsAbstract; Static = $Method.IsStatic
        Overrides = @($Method.Overrides | ForEach-Object { $_.FullName }); Category = $Category
        CodeSize = $Size; RawPath = $RawFile; RawSHA256 = $RawHash; ILTextSHA256 = $TextHash
        InitLocals = $(if ($Method.HasBody) { $Method.Body.InitLocals } else { $false })
        Locals = $LocalTypes; Handlers = $Handlers; Instructions = $Instructions
    }
}

$Platforms = [ordered]@{ Windows = $WindowsAssembly; Linux = $LinuxAssembly }
$Reports = [Collections.Generic.List[object]]::new()
foreach ($Entry in $Platforms.GetEnumerator()) {
    $Path = (Resolve-Path -LiteralPath $Entry.Value).Path
    $Image = [IO.File]::ReadAllBytes($Path)
    $Pe = [BitConverter]::ToInt32($Image, 0x3c)
    if ([BitConverter]::ToUInt32($Image, $Pe) -ne 0x4550) { throw '[Research:EntityDeath] Invalid PE signature.' }
    $SectionCount = [BitConverter]::ToUInt16($Image, $Pe + 6)
    $SectionOffset = $Pe + 24 + [BitConverter]::ToUInt16($Image, $Pe + 20)
    $Sections = @(for ($Index = 0; $Index -lt $SectionCount; $Index++) {
        $Offset = $SectionOffset + $Index * 40
        [ordered]@{
            VirtualAddress = [BitConverter]::ToUInt32($Image, $Offset + 12)
            Size = [Math]::Max([BitConverter]::ToUInt32($Image, $Offset + 8), [BitConverter]::ToUInt32($Image, $Offset + 16))
            RawAddress = [BitConverter]::ToUInt32($Image, $Offset + 20)
        }
    })
    $PlatformDirectory = Join-Path $OutputDirectory $Entry.Key
    [void](New-Item -ItemType Directory -Path $PlatformDirectory)
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        $Types = @{}
        $Pending = [Collections.Generic.Queue[object]]::new()
        foreach ($Type in $Assembly.MainModule.Types) { $Pending.Enqueue($Type) }
        while ($Pending.Count) {
            $Type = $Pending.Dequeue(); $Types[$Type.FullName] = $Type
            foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
        }
        $TypeRecords = [Collections.Generic.List[object]]::new()
        $Methods = [Collections.Generic.List[object]]::new()
        $StateWrites = [Collections.Generic.List[object]]::new()
        $Callers = [Collections.Generic.List[object]]::new()
        $Addresses = [Collections.Generic.List[object]]::new()
        $AlternateCallers = [Collections.Generic.List[object]]::new()
        foreach ($Type in ($Types.Values | Sort-Object FullName)) {
            $Ancestors = @(GetAncestors $Type.FullName $Types)
            $Combat = $Ancestors -contains 'BaseCombatEntity'
            $Resource = $Ancestors -contains 'ResourceEntity'
            $Entity = $Ancestors -contains 'BaseEntity'
            $Slots = @($Type.Methods | Where-Object { TestDeathSignature $_ })
            # Retain all module types so the checker independently walks ancestry.
            $TypeRecords.Add([ordered]@{
                Name = $Type.FullName; Base = $(if ($Type.BaseType) {
                    if ($Type.BaseType -is [Mono.Cecil.GenericInstanceType]) { $Type.BaseType.ElementType.FullName } else { $Type.BaseType.FullName }
                } else { $null })
                Abstract = $Type.IsAbstract; Combat = $Combat; Resource = $Resource; Entity = $Entity
                Player = $Ancestors -contains 'BasePlayer'; NPCPlayer = $Ancestors -contains 'NPCPlayer'
                DeclaredDeathMethods = @($Slots | ForEach-Object { $_.FullName })
            })
            foreach ($Method in $Type.Methods) {
                $Death = TestDeathSignature $Method
                $Support = ($Entity -and $Method.Name -in @('Hurt', 'OnAttacked', 'IsDead', 'IsAlive', 'OnKilled', 'TryDropCorpse')) -or
                    ($Type.FullName -eq 'BaseCombatEntity' -and $Method.Name -in @('set_health', 'InitializeHealth', 'ResetState'))
                $NeedsBody = $Death -or $Support
                if ($Method.HasBody) {
                    foreach ($Instruction in $Method.Body.Instructions) {
                        $Operand = $Instruction.Operand
                        if ($Operand -is [Mono.Cecil.FieldReference] -and $Operand.DeclaringType.FullName -in @('BaseCombatEntity', 'ResourceEntity') -and
                            $Operand.Name -match '^(lifestate|isKilled|_health|health)$' -and $Instruction.OpCode.Name -in @('stfld', 'stsfld', 'ldflda', 'ldsflda')) {
                            $StateWrites.Add([ordered]@{ Method = $Method.FullName; Token = GetToken $Method; Offset = $Instruction.Offset; Op = $Instruction.OpCode.Name; Field = $Operand.FullName })
                            $NeedsBody = $true
                        }
                        if ($Operand -is [Mono.Cecil.MethodReference] -and $Operand.Name -in @('DieInstantly', 'TurnIntoCorpse') -and
                            $Instruction.OpCode.Name -in @('call', 'callvirt')) {
                            $AlternateCallers.Add([ordered]@{ Method = $Method.FullName; Token = GetToken $Method; Offset = $Instruction.Offset; Op = $Instruction.OpCode.Name; Target = $Operand.FullName })
                            $NeedsBody = $true
                        }
                        if ($Operand -is [Mono.Cecil.MethodReference] -and (TestDeathSignature $Operand)) {
                            $Record = [ordered]@{ Method = $Method.FullName; Token = GetToken $Method; Offset = $Instruction.Offset; Op = $Instruction.OpCode.Name; Target = $Operand.FullName }
                            if ($Instruction.OpCode.Name -in @('call', 'callvirt')) { $Callers.Add($Record); $NeedsBody = $true }
                            else { $Addresses.Add($Record) }
                        }
                    }
                }
                if ($NeedsBody) {
                    $Category = if ($Death) { if ($Combat) { 'CombatDeath' } elseif ($Resource) { 'ResourceDeath' } else { 'OtherDeath' } } else { 'Support' }
                    $Methods.Add((GetMethodRecord $Method $Category $PlatformDirectory $Image $Sections))
                }
            }
        }
        $Report = [ordered]@{
            Schema = 1; Platform = $Entry.Key; AssemblyPath = $Path; AssemblySHA256 = GetHash $Image
            MVID = $Assembly.MainModule.Mvid.ToString(); Version = $Assembly.Name.Version.ToString()
            EvidenceKind = 'Original PE static analysis; not Carbon-publicized/live reflection qualification'
            Types = $TypeRecords.ToArray(); Methods = $Methods.ToArray(); StateWrites = $StateWrites.ToArray()
            DeathCallsites = $Callers.ToArray(); DeathAddressReferences = $Addresses.ToArray()
            AlternateTransitionCallsites = $AlternateCallers.ToArray()
        }
        $Reports.Add($Report)
        $ReportPath = Join-Path $OutputDirectory ($Entry.Key + '.json')
        [IO.File]::WriteAllText($ReportPath, ($Report | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        Write-Output "[Research:EntityDeath] $($Entry.Key) types=$($Types.Count) death-declarations=$(@($Methods | Where-Object { $_.Category -ne 'Support' }).Count) retained-bodies=$($Methods.Count) calls=$($Callers.Count) state-writes-or-addresses=$($StateWrites.Count) report=$ReportPath"
    } finally { $Assembly.Dispose() }
}
Write-Output "[Research:EntityDeath] Evidence=$OutputDirectory; independently check with tests/research/CheckEntityDeathHierarchy.py. No live qualification claimed."
