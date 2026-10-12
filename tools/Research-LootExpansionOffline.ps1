# Offline metadata/IL only. Never loads Assembly-CSharp into the CLR or runs Rust.
[CmdletBinding(DefaultParameterSetName = 'Path')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Path')][string]$AssemblyPath,
    [Parameter(Mandatory = $true, ParameterSetName = 'Memory')][byte[]]$AssemblyBytes,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [switch]$Body,
    [switch]$References,
    # Compare the two pinned backing binaries; emits compact evidence on success.
    [string]$CompareAssemblyPath
)
$ErrorActionPreference = 'Stop'
function Read-LootAssembly {
param([string]$AssemblyPath, [byte[]]$AssemblyBytes, [bool]$MemoryInput)
$Expected = @(
    'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2',
    'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450'
)
if ($MemoryInput) {
    $Resolved = '<memory>'
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { $Hash = ([BitConverter]::ToString($Hasher.ComputeHash($AssemblyBytes))).Replace('-', '').ToLowerInvariant() }
    finally { $Hasher.Dispose() }
} else {
    $Resolved = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $Hash = (Get-FileHash -LiteralPath $Resolved -Algorithm SHA256).Hash.ToLowerInvariant()
}
if ($Hash -cnotin $Expected) { throw '[CarbonLuau:LootResearch] Unqualified backing assembly hash' }
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$Stream = $null; $Assembly = $null
function Write-Record($Record) { ConvertTo-Json -InputObject $Record -Depth 8 -Compress }
function Get-TextHash([string]$Text) {
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($Hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '').ToLowerInvariant() }
    finally { $Hasher.Dispose() }
}
try {
    if ($MemoryInput) {
        $Stream = [IO.MemoryStream]::new($AssemblyBytes, $false)
        $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Stream)
    } else { $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Resolved) }
    Write-Record ([ordered]@{ Kind = 'Assembly'; Path = $Resolved; Sha256 = $Hash; Mvid = [string]$Assembly.MainModule.Mvid })
    $Types = @{}
    $Pending = [Collections.Generic.Queue[object]]::new()
    foreach ($Type in $Assembly.MainModule.Types) { $Pending.Enqueue($Type) }
    while ($Pending.Count) {
        $Type = $Pending.Dequeue(); $Types[$Type.FullName] = $Type
        foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
    }
    function Get-Chain($Type) {
        $Chain = [Collections.Generic.List[string]]::new()
        while ($Type) {
            if ($Chain.Count -ge 64 -or $Chain.Contains($Type.FullName)) { throw '[CarbonLuau:LootResearch] Invalid inheritance chain' }
            $Chain.Add($Type.FullName)
            if (!$Type.BaseType -or !$Types.ContainsKey($Type.BaseType.FullName)) { break }
            $Type = $Types[$Type.BaseType.FullName]
        }
        return ,$Chain.ToArray()
    }
    $Lifecycle = @('Die','OnDied','OnKilled','DropItems','DropBonusItems','TryDropCorpse',
        'ShouldDropItemsIndividually','get_DropsLoot','get_DropFloats','get_DestroyLootPercent','GetDropPosition','SpawnLoot','PopulateLoot')
    $Supporting = @('BaseCombatEntity','DecayEntity','BaseEntity','BaseNetworkable','StorageContainer',
        'LootFill','DropUtil','Item','ItemManager','ItemManager/ItemLookup','WorldItem','DroppedItem','ItemContainer','ItemDefinition',
        'ItemMod','ItemModContainer','ItemModEntity','ItemModAssociatedEntity`1','ItemModBurnable','ItemModCookable','LootSpawn')
    $Methods = '^(Die|OnDied|OnKilled|DropItems|DropBonusItems|TryDropCorpse|SpawnLoot|PopulateLoot|GenerateScrap|FillLoot|DelayFill|Create|CreateByName|CreateWorldObject|CreateEntity|Drop|Initialize|InitializeItem|OnItemCreated|OnItemRemoved|OnItemDropped|ServerInit|GetWorldModel|GetItemMods|FindItemDefinition|ShouldDropItemsIndividually|get_DropsLoot|get_DropFloats|get_DestroyLootPercent|GetDropPosition|SpawnSwarm|CreateAssociatedEntity|CreateContents|Cleanup_DropItems)$'
    $ReferencePattern = '(LootContainer|StorageContainer|DropUtil)::(DropItems|DropBonusItems|GenerateScrap)|Item::(CreateWorldObject|Drop)\(|ItemContainer::Drop\(|ItemManager::Create\('
    $MethodCount = 0; $ReferenceCount = 0; $LootCount = 0; $StorageCount = 0
    foreach ($Type in ($Types.Values | Sort-Object FullName)) {
        $Chain = Get-Chain $Type
        $IsLoot = $Chain -ccontains 'LootContainer'
        if ($Chain -ccontains 'StorageContainer') {
            $StorageCount++
            Write-Record ([ordered]@{ Kind = 'StorageType'; Type = $Type.FullName; Chain = $Chain })
        }
        if ($IsLoot) {
            $LootCount++
            foreach ($Name in $Lifecycle) {
                # Match exact instance parameter types, including optional parameters.
                # Overloads are recorded below; do not confuse static DropItems with dispatch.
                $Effective = $null
                foreach ($Owner in $Chain) {
                    $Signature = switch ($Name) {
                        'DropItems' { 'BaseEntity' }
                        'DropBonusItems' { 'BaseEntity,ItemContainer' }
                        'OnKilled' { '' }
                        { $_ -in @('Die','OnDied','TryDropCorpse') } { 'HitInfo' }
                        default { '' }
                    }
                    $Matches = @($Types[$Owner].Methods | Where-Object {
                        !$_.IsStatic -and $_.Name -ceq $Name -and
                        (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ceq $Signature
                    })
                    if ($Matches.Count -gt 1) { throw '[CarbonLuau:LootResearch] Ambiguous effective method' }
                    if ($Matches.Count) { $Effective = $Matches[0]; break }
                }
                Write-Record ([ordered]@{ Kind = 'Effective'; Type = $Type.FullName; Name = $Name; Method = [string]$Effective; Token = $(if ($Effective) { '0x{0:x8}' -f $Effective.MetadataToken.ToUInt32() } else { $null }) })
            }
        }
        if ($IsLoot -or $Supporting -ccontains $Type.FullName) {
            Write-Record ([ordered]@{ Kind = 'Fields'; Type = $Type.FullName; Fields = @($Type.Fields | ForEach-Object { [string]$_ }) })
            foreach ($Method in $Type.Methods) {
                if (($Method.Name -cnotmatch $Methods -and !($Type.FullName -ceq 'ItemManager/ItemLookup' -and $Method.Name -ceq '.ctor')) -or !$Method.HasBody) { continue }
                $MethodCount++
                $Lines = @($Method.Body.Instructions | ForEach-Object { $_.ToString() })
                $Calls = @($Method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.ToString() })
                $Record = [ordered]@{ Kind = 'Method'; Method = $Method.FullName; Token = '0x{0:x8}' -f $Method.MetadataToken.ToUInt32(); Size = $Method.Body.CodeSize; Instructions = $Lines.Count; Eh = $Method.Body.ExceptionHandlers.Count; IlTextSha256 = Get-TextHash ($Lines -join "`n"); Calls = $Calls }
                if ($Body) { $Record.Body = $Lines }
                Write-Record $Record
            }
        }
        if ($References) {
            foreach ($Method in $Type.Methods) {
                if (!$Method.HasBody) { continue }
                foreach ($Instruction in $Method.Body.Instructions) {
                    if ($Instruction.Operand -is [Mono.Cecil.MethodReference] -and $Instruction.Operand.FullName -cmatch $ReferencePattern) {
                        $ReferenceCount++
                        Write-Record ([ordered]@{ Kind = 'Reference'; Method = $Method.FullName; Token = '0x{0:x8}' -f $Method.MetadataToken.ToUInt32(); Instruction = $Instruction.ToString() })
                    }
                }
            }
        }
    }
    Write-Record ([ordered]@{ Kind = 'Summary'; Types = $Types.Count; StorageTypes = $StorageCount; LootTypes = $LootCount; Methods = $MethodCount; References = $ReferenceCount; AssetMappings = 'NotInspected' })
} finally { if ($Assembly) { $Assembly.Dispose() }; if ($Stream) { $Stream.Dispose() } }
}

$MemoryInput = $PSCmdlet.ParameterSetName -ceq 'Memory'
if (!$CompareAssemblyPath) {
    Read-LootAssembly $AssemblyPath $AssemblyBytes $MemoryInput
    return
}
# Reference parity is part of the checker even when -References is omitted.
$References = $true
$PrimaryRecords = @(Read-LootAssembly $AssemblyPath $AssemblyBytes $MemoryInput | ConvertFrom-Json)
$ComparedRecords = @(Read-LootAssembly $CompareAssemblyPath $null $false | ConvertFrom-Json)
$PrimaryAssembly = @($PrimaryRecords | Where-Object Kind -CEQ 'Assembly')
$ComparedAssembly = @($ComparedRecords | Where-Object Kind -CEQ 'Assembly')
if ($PrimaryAssembly.Count -ne 1 -or $ComparedAssembly.Count -ne 1 -or
    $PrimaryAssembly[0].Sha256 -ceq $ComparedAssembly[0].Sha256) {
    throw '[CarbonLuau:LootResearch] Comparison requires both distinct pinned backing assemblies'
}
$Pins = @{
    'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2' = @('c1c11bd1-baa1-4d85-b64e-1d4a4664d62e', 7666)
    'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450' = @('616082a0-36f4-4680-a1ab-efc5766796cc', 7668)
}
foreach ($Records in @($PrimaryRecords, $ComparedRecords)) {
    $Header = @($Records | Where-Object Kind -CEQ 'Assembly')[0]
    $Summary = @($Records | Where-Object Kind -CEQ 'Summary')
    $Pin = $Pins[$Header.Sha256]
    if ($Header.Mvid -cne $Pin[0] -or $Summary.Count -ne 1 -or
        $Summary[0].Types -ne $Pin[1] -or $Summary[0].StorageTypes -ne 87 -or
        $Summary[0].LootTypes -ne 11 -or $Summary[0].Methods -ne 71 -or
        $Summary[0].References -ne 234 -or $Summary[0].AssetMappings -cne 'NotInspected') {
        throw '[CarbonLuau:LootResearch] Pinned inventory measurement mismatch'
    }
}
$Counts = [ordered]@{}
foreach ($Kind in @('StorageType', 'Effective', 'Fields', 'Method', 'Reference')) {
    $PrimaryKind = @($PrimaryRecords | Where-Object Kind -CEQ $Kind)
    $ComparedKind = @($ComparedRecords | Where-Object Kind -CEQ $Kind)
    # Caller tokens outside the selected bodies can differ by OS. Compare every
    # symbolic caller, offset, opcode and callee; retain/count token differences.
    # Selected Method records still require exact tokens and complete text parity.
    $Primary = @($PrimaryKind | ForEach-Object {
        if ($Kind -ceq 'Reference') { ConvertTo-Json ([ordered]@{ Method = $_.Method; Instruction = $_.Instruction }) -Compress }
        else { ConvertTo-Json $_ -Depth 8 -Compress }
    })
    $Compared = @($ComparedKind | ForEach-Object {
        if ($Kind -ceq 'Reference') { ConvertTo-Json ([ordered]@{ Method = $_.Method; Instruction = $_.Instruction }) -Compress }
        else { ConvertTo-Json $_ -Depth 8 -Compress }
    })
    if (!$Primary.Count -or ($Primary -join "`n") -cne ($Compared -join "`n")) {
        Compare-Object $Primary $Compared -CaseSensitive | Select-Object -First 4 | ForEach-Object {
            [Console]::Error.WriteLine($_.SideIndicator + ' ' + $_.InputObject)
        }
        throw ('[CarbonLuau:LootResearch] Backing parity mismatch: ' + $Kind)
    }
    $Counts[$Kind] = $Primary.Count
    if ($Kind -ceq 'Reference') {
        $CallerTokens = @{}; $TokenSites = 0
        for ($Index = 0; $Index -lt $PrimaryKind.Count; $Index++) {
            if ($PrimaryKind[$Index].Token -cne $ComparedKind[$Index].Token) {
                $TokenSites++; $CallerTokens[$PrimaryKind[$Index].Method] = $true
            }
        }
        $Counts.ReferenceTokenDifferences = $TokenSites
        $Counts.ReferenceCallerTokenDifferences = $CallerTokens.Count
    }
}
if ($Counts.Effective -ne 143) { throw '[CarbonLuau:LootResearch] Effective signature count mismatch' }
foreach ($Records in @($PrimaryRecords, $ComparedRecords)) {
    $Records | Where-Object { $_.Kind -cin @('Assembly', 'Summary') } | ForEach-Object { ConvertTo-Json $_ -Depth 8 -Compress }
}
ConvertTo-Json -InputObject ([ordered]@{ Kind = 'Parity'; Status = 'PASS'; Counts = $Counts; AssetMappings = 'NotInspected' }) -Depth 8 -Compress
