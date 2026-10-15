param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'), [switch]$IncludePhase1Fixtures, [switch]$IncludePhase3Fixtures, [switch]$IncludePhase5Fixtures, [switch]$IncludeFoundationEFixtures, [switch]$IncludePlayer1FBFixtures, [switch]$IncludeEntity1AFixtures, [switch]$IncludeEntity1BFixtures, [switch]$IncludeDiscoveryFixtures, [switch]$IncludeDiscovery2BFixtures, [switch]$IncludeGameplayHostProofFixtures, [switch]$IncludeGameplayPublicFixtures, [switch]$IncludeEntitySpawnFixtures, [switch]$IncludeEntityDestroyResearch, [switch]$IncludeEntityDestroyedFixtures, [switch]$IncludeC1Fixtures, [switch]$IncludeInventoryNetFixtures, [switch]$IncludeInventoryChangedFixtures, [switch]$IncludeD2Research, [switch]$IncludeD2Fixtures, [switch]$IncludeD3Fixtures, [switch]$IncludeB2Fixtures, [switch]$IncludeD4Fixtures, [switch]$IncludeD5Fixtures, [switch]$D5ProbeOnly, [switch]$IncludeD6Fixtures, [switch]$IncludeD7Fixtures, [switch]$D7ProbeOnly, [switch]$IncludeD8Fixtures, [switch]$D8DiscoveryOnly, [switch]$D8ProbeOnly, [string]$D8Manifest, [int]$D8BatchOffset = 0, [int]$D8BatchCount = 32, [string]$D8ProbeSkipNamesPath, [switch]$IncludeD8StackFixtures, [switch]$D8StackGapOnly, [int]$D8StackPart = 0, [switch]$IncludeCatalogD9Fixtures)
$ErrorActionPreference = 'Stop'
if ($D8StackGapOnly -and !$IncludeD8StackFixtures) { throw '[CarbonLuau:Package] Stack gap mode requires stack fixtures' }
if ($IncludeD8StackFixtures -and ($IncludeD8Fixtures -or $D7ProbeOnly -or $D5ProbeOnly)) { throw '[CarbonLuau:Package] Select stack qualification exclusively' }
if ($D8StackPart -lt 0 -or $D8StackPart -gt 3) { throw '[CarbonLuau:Package] Stack part must be0..3' }
$D8StackSweep = $IncludeD8StackFixtures -and !$D8StackGapOnly -and $D8StackPart -eq 3
if ($D8StackSweep -and !$D8Manifest) { throw '[CarbonLuau:Package] Final stack sweep requires exact pinned manifest' }
if ($IncludeD8StackFixtures) { $IncludeD7Fixtures = $true }
if (($D8DiscoveryOnly -or $D8ProbeOnly) -and !$IncludeD8Fixtures) { throw '[CarbonLuau:Package] D8 mode requires explicit D8 fixtures' }
if ($D8DiscoveryOnly -and $D8ProbeOnly) { throw '[CarbonLuau:Package] Select D8 discovery or physical probe mode' }
if ($IncludeD8Fixtures -and ($D7ProbeOnly -or $D5ProbeOnly)) { throw '[CarbonLuau:Package] Select D8 mode exclusively' }
if ($IncludeD8Fixtures) { $IncludeD7Fixtures = $true }
if ($IncludeD8Fixtures -and !$D8DiscoveryOnly -and !$D8ProbeOnly -and !$D8Manifest) { throw '[CarbonLuau:Package] D8 public batches require an exact pinned manifest' }
if ($D8BatchOffset -lt 0 -or $D8BatchCount -lt 1 -or $D8BatchCount -gt 32) { throw '[CarbonLuau:Package] D8 public batch must have nonnegative offset and count1..32' }
if ($D8ProbeSkipNamesPath -and !$D8ProbeOnly) { throw '[CarbonLuau:Package] Explicit proved-name skips require D8 physical probe mode' }
$D8ProbeSkips = @()
if ($D8ProbeSkipNamesPath) {
    if ((Get-Item -LiteralPath $D8ProbeSkipNamesPath).Length -gt 65536) { throw '[CarbonLuau:Package] D8 probe skip input bound' }
    $D8SkipData = Get-Content -Raw -LiteralPath $D8ProbeSkipNamesPath | ConvertFrom-Json
    $D8ProbeSkips = @($D8SkipData)
    if ($D8ProbeSkips.Count -gt 512) { throw '[CarbonLuau:Package] D8 probe skip name bound' }
    foreach ($Name in $D8ProbeSkips) { if ($Name -isnot [string] -or $Name -cnotmatch '^[a-z0-9][a-z0-9_.-]{0,14}$') { throw '[CarbonLuau:Package] Invalid proved probe name' } }
}
$D8Specs = @()
function Test-D8ManifestInteger {
    param($Value, [long]$Minimum, [long]$Maximum)
    return (($Value -is [int] -or $Value -is [long]) -and $Value -ge $Minimum -and $Value -le $Maximum)
}
function Test-D8ManifestFields {
    param($Value, [string[]]$Expected)
    if ($Value -isnot [pscustomobject]) { return $false }
    $Names = @($Value.PSObject.Properties | ForEach-Object { $_.Name })
    if ($Names.Count -ne $Expected.Count) { return $false }
    foreach ($Name in $Names) { if ($Expected -cnotcontains $Name) { return $false } }
    return $true
}
function Add-D8JsonValue {
    param([string]$Token, [bool]$Scalar, [Collections.Generic.List[object]]$Stack)
    if ($Token -eq '{') {
        $Stack.Add([pscustomobject]@{Kind='Object'; State='KeyOrEnd'; Names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)})
    } elseif ($Token -eq '[') { $Stack.Add([pscustomobject]@{Kind='Array'; State='ValueOrEnd'}) }
    elseif (!$Scalar) { throw '[CarbonLuau:Package] Invalid strict D8 JSON value' }
    if ($Stack.Count -gt 8) { throw '[CarbonLuau:Package] D8 JSON nesting exceeds schema bound' }
}
function Assert-D8Json {
    param([string]$Text)
    # ConvertFrom-Json accepts duplicate keys and non-JSON syntax in some runtimes.
    # Validate the bounded grammar and decoded object keys before using its values.
    $Tokens = [regex]::new('\G[ \t\r\n]*(?:(?<String>"(?:\\(?:["\\/bfnrt]|u[0-9a-fA-F]{4})|[^"\\\x00-\x1f])*")|(?<Punctuation>[{}\[\]:,])|(?<Scalar>-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null))')
    $Stack = [Collections.Generic.List[object]]::new()
    $Position = 0; $Count = 0; $RootSeen = $false
    while ($Position -lt $Text.Length) {
        $Match = $Tokens.Match($Text, $Position)
        if (!$Match.Success) {
            if ($Text.Substring($Position) -cmatch '\A[ \t\r\n]*\z') { break }
            throw '[CarbonLuau:Package] Invalid strict D8 JSON token'
        }
        if (++$Count -gt 32768) { throw '[CarbonLuau:Package] D8 JSON token bound' }
        $Position = $Match.Index + $Match.Length
        $Token = $Match.Groups['String'].Value + $Match.Groups['Punctuation'].Value + $Match.Groups['Scalar'].Value
        $Scalar = $Match.Groups['String'].Success -or $Match.Groups['Scalar'].Success
        if ($Stack.Count -eq 0) {
            if ($RootSeen) { throw '[CarbonLuau:Package] Multiple D8 JSON roots' }
            $RootSeen = $true; Add-D8JsonValue $Token $Scalar $Stack; continue
        }
        $Top = $Stack[$Stack.Count - 1]
        if ($Top.Kind -eq 'Object') {
            if ($Top.State -eq 'KeyOrEnd' -and $Token -eq '}') { $Stack.RemoveAt($Stack.Count - 1); continue }
            if ($Top.State -eq 'KeyOrEnd' -or $Top.State -eq 'Key') {
                if (!$Match.Groups['String'].Success) { throw '[CarbonLuau:Package] D8 JSON object key required' }
                $Key = [regex]::Unescape($Token.Substring(1, $Token.Length - 2))
                if (!$Top.Names.Add($Key)) { throw '[CarbonLuau:Package] Duplicate D8 JSON field' }
                $Top.State = 'Colon'; continue
            }
            if ($Top.State -eq 'Colon') {
                if ($Token -ne ':') { throw '[CarbonLuau:Package] D8 JSON colon required' }
                $Top.State = 'Value'; continue
            }
            if ($Top.State -eq 'Value') { $Top.State = 'CommaOrEnd'; Add-D8JsonValue $Token $Scalar $Stack; continue }
            if ($Token -eq '}') { $Stack.RemoveAt($Stack.Count - 1); continue }
            if ($Token -ne ',') { throw '[CarbonLuau:Package] D8 JSON comma required' }
            $Top.State = 'Key'
        } else {
            if ($Top.State -eq 'ValueOrEnd' -and $Token -eq ']') { $Stack.RemoveAt($Stack.Count - 1); continue }
            if ($Top.State -eq 'ValueOrEnd' -or $Top.State -eq 'Value') {
                $Top.State = 'CommaOrEnd'; Add-D8JsonValue $Token $Scalar $Stack; continue
            }
            if ($Token -eq ']') { $Stack.RemoveAt($Stack.Count - 1); continue }
            if ($Token -ne ',') { throw '[CarbonLuau:Package] D8 JSON comma required' }
            $Top.State = 'Value'
        }
    }
    if (!$RootSeen -or $Stack.Count -ne 0) { throw '[CarbonLuau:Package] Incomplete D8 JSON root' }
}
if (($IncludeD8Fixtures -and !$D8DiscoveryOnly -and !$D8ProbeOnly) -or $D8StackSweep) {
    if ((Get-Item -LiteralPath $D8Manifest).Length -gt 65536) { throw '[CarbonLuau:Package] D8 manifest input bound' }
    $D8Text = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes((Get-Item -LiteralPath $D8Manifest).FullName))
    Assert-D8Json $D8Text
    $D8Data = $D8Text | ConvertFrom-Json
    if (!(Test-D8ManifestFields $D8Data @('SchemaVersion','HostPins','Definitions')) -or
        !(Test-D8ManifestFields $D8Data.HostPins @('Windows','Linux')) -or $D8Data.Definitions -isnot [array]) {
        throw '[CarbonLuau:Package] Invalid exact D8 manifest fields'
    }
    foreach ($HostPin in @($D8Data.HostPins.Windows, $D8Data.HostPins.Linux)) {
        if (!(Test-D8ManifestFields $HostPin @('RustBuild','Carbon','AssemblySha256','Mvid')) -or
            !(Test-D8ManifestInteger $HostPin.RustBuild 1 2147483647) -or
            $HostPin.Carbon -isnot [string] -or $HostPin.Carbon -cnotmatch '\A[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+/[0-9a-f]{7}\z' -or
            $HostPin.AssemblySha256 -isnot [string] -or $HostPin.AssemblySha256 -cnotmatch '\A[0-9a-f]{64}\z' -or
            $HostPin.Mvid -isnot [string] -or $HostPin.Mvid -cnotmatch '\A[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\z') {
            throw '[CarbonLuau:Package] Invalid D8 host pin schema'
        }
    }
    if ($D8Data.HostPins.Windows.RustBuild -ne $D8Data.HostPins.Linux.RustBuild) { throw '[CarbonLuau:Package] D8 host Rust build mismatch' }
    $D8Specs = @($D8Data.Definitions)
    if (!(Test-D8ManifestInteger $D8Data.SchemaVersion 1 1) -or $D8Specs.Count -lt 1 -or $D8Specs.Count -gt 512 -or $D8BatchOffset -ge $D8Specs.Count) { throw '[CarbonLuau:Package] Invalid finite D8 manifest/batch' }
    $D8Previous = $null; $D8Ids = @{}
    foreach ($Spec in $D8Specs) {
        if (!(Test-D8ManifestFields $Spec @('Name','Id','Stack','Family')) -or
            $Spec.Name -isnot [string] -or $Spec.Name -cnotmatch '\A[a-z0-9][a-z0-9_.-]{0,14}\z' -or
            ($null -ne $D8Previous -and [StringComparer]::Ordinal.Compare($D8Previous, $Spec.Name) -ge 0) -or
            !(Test-D8ManifestInteger $Spec.Id -2147483648 2147483647) -or $Spec.Id -eq 0 -or $D8Ids.ContainsKey($Spec.Id) -or
            !(Test-D8ManifestInteger $Spec.Stack 1 2147483647) -or
            $Spec.Family -isnot [string] -or $Spec.Family -cnotin @('Baseline','RawResource','Component','BasicMaterial','Consumable','Equivalent')) {
            throw '[CarbonLuau:Package] Invalid copied D8 manifest identity/scalar'
        }
        $D8Previous = $Spec.Name; $D8Ids[$Spec.Id] = $true
    }
}
if ($D7ProbeOnly -and !$IncludeD7Fixtures) { throw '[CarbonLuau:Package] D7 probe requires explicit D7 fixtures' }
if ($IncludeD7Fixtures -and $D5ProbeOnly) { throw '[CarbonLuau:Package] Select the D7 probe exclusively' }
if ($IncludeD7Fixtures) { $IncludeD6Fixtures = $true }
if ($IncludeD6Fixtures -and $D5ProbeOnly) { throw '[CarbonLuau:Package] D6 registry requires full public qualification' }
if ($IncludeD6Fixtures) { $IncludeD5Fixtures = $true }
if ($D5ProbeOnly -and !$IncludeD5Fixtures) { throw '[CarbonLuau:Package] D5 probe requires explicit D5 fixtures' }
if ($IncludeD5Fixtures) { $IncludeD4Fixtures = $true }
if ($IncludeD4Fixtures) { $IncludeD3Fixtures = $true }
if ($IncludeInventoryChangedFixtures -and ($IncludeInventoryNetFixtures -or $IncludeC1Fixtures -or $IncludeD2Research -or $IncludeD2Fixtures -or $IncludeD3Fixtures -or $IncludeB2Fixtures)) {
    throw '[CarbonLuau:Package] Select public C3 fixture exclusively'
}
if ($IncludeInventoryNetFixtures -and ($IncludeC1Fixtures -or $IncludeD2Research -or $IncludeD2Fixtures -or $IncludeD3Fixtures -or $IncludeB2Fixtures)) {
    throw '[CarbonLuau:Package] Select private C2 fixture exclusively'
}
if ($IncludeB2Fixtures) { $IncludeD3Fixtures = $true }
if ($IncludeD3Fixtures) {
    if ($IncludeD2Research) { throw '[CarbonLuau:Package] Public qualification is not the standalone research fixture' }
    $IncludeD2Fixtures = $true
}
if (@($IncludeEntitySpawnFixtures,$IncludeEntityDestroyResearch,$IncludeEntityDestroyedFixtures).Where({$_}).Count -gt 1) {
    throw '[CarbonLuau:Package] Select B1 fixtures, B3 research or B3 qualification exclusively'
}
if ($IncludeDiscoveryFixtures -and $IncludeDiscovery2BFixtures) {
    throw '[CarbonLuau:Package] Select either private Discovery-2A or public Discovery-2B fixtures, never both'
}
if ($IncludeGameplayPublicFixtures -and !$IncludeGameplayHostProofFixtures) {
    throw '[CarbonLuau:Package] Public gameplay fixtures require the scoped A0 actor/cleanup substrate'
}
. (Join-Path $PSScriptRoot 'Get-CoreSources.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Add-DeterministicEntry {
    param([IO.Compression.ZipArchive]$Archive, [string]$Source, [string]$Name)
    $Entry = $Archive.CreateEntry($Name, [IO.Compression.CompressionLevel]::Optimal)
    $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
    $InputStream = [IO.File]::OpenRead($Source)
    $OutputStream = $Entry.Open()
    try { $InputStream.CopyTo($OutputStream) }
    finally { $OutputStream.Dispose(); $InputStream.Dispose() }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputPath = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'CarbonLuau.cszip'
$Stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create)
try {
    $Archive = New-Object IO.Compression.ZipArchive($Stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        $SourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\CarbonLuau'))
        $Sources = @(Get-ChildItem $SourceRoot -Recurse -File -Filter '*.cs') + @(Get-CoreSources (Join-Path $PSScriptRoot '..'))
        if (@($Sources | Group-Object Name | Where-Object Count -gt 1).Count) { throw 'Duplicate flattened production source filename' }
        $Sources | Sort-Object Name | ForEach-Object {
            Add-DeterministicEntry $Archive $_.FullName $_.Name
        }
        if ($IncludePhase1Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.Fixtures.cs') 'CarbonLuau.Fixtures.cs'
        }
        if ($IncludePhase3Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.FacadeFixtures.cs') 'CarbonLuau.FacadeFixtures.cs'
        }
        if ($IncludePhase5Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.Phase5Fixtures.cs') 'CarbonLuau.Phase5Fixtures.cs'
        }
        if ($IncludeFoundationEFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.FoundationEFixtures.cs') 'CarbonLuau.FoundationEFixtures.cs'
        }
        if ($IncludePlayer1FBFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.Player1FBFixtures.cs') 'CarbonLuau.Player1FBFixtures.cs'
        }
        if ($IncludeEntity1AFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityPrivateFixtures.cs') 'CarbonLuau.EntityPrivateFixtures.cs'
        }
        if ($IncludeEntity1BFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityReadFixtures.cs') 'CarbonLuau.EntityReadFixtures.cs'
        }
        if ($IncludeDiscoveryFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDiscoveryFixtures.cs') 'CarbonLuau.EntityDiscoveryFixtures.cs'
        }
        if ($IncludeDiscovery2BFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.DiscoveryPublicFixtures.cs') 'CarbonLuau.DiscoveryPublicFixtures.cs'
        }
        if ($IncludeGameplayHostProofFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.GameplayHostProofFixtures.cs') 'CarbonLuau.GameplayHostProofFixtures.cs'
        }
        if ($IncludeGameplayPublicFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.GameplayPublicFixtures.cs') 'CarbonLuau.GameplayPublicFixtures.cs'
        }
        if ($IncludeEntitySpawnFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntitySpawnFixtures.cs') 'CarbonLuau.EntitySpawnFixtures.cs'
        }
        if ($IncludeEntityDestroyResearch) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDestroyResearch.cs') 'CarbonLuau.EntityDestroyResearch.cs'
        }
        if ($IncludeEntityDestroyedFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDestroyedFixtures.cs') 'CarbonLuau.EntityDestroyedFixtures.cs'
        }
        if ($IncludeC1Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.PlayerInventoryFixtures.cs') 'CarbonLuau.PlayerInventoryFixtures.cs'
        }
        if ($IncludeInventoryNetFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.InventoryNetChangeFixtures.cs') 'CarbonLuau.InventoryNetChangeFixtures.cs'
        }
        if ($IncludeCatalogD9Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.CatalogD9Fixtures.cs') 'CarbonLuau.CatalogD9Fixtures.cs'
        }
        if ($IncludeInventoryChangedFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.InventoryChangedPublicFixtures.cs') 'CarbonLuau.InventoryChangedPublicFixtures.cs'
        }
        if ($IncludeD2Research -or $IncludeD2Fixtures) {
            if ($IncludeC1Fixtures) { throw '[CarbonLuau:Package] Select C1 or D2 research fixture exclusively' }
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.BarrelLootResearch.cs') 'CarbonLuau.BarrelLootResearch.cs'
        }
        if ($IncludeD2Fixtures) {
            if ($IncludeD2Research) { throw '[CarbonLuau:Package] Select D2 research or qualification exclusively' }
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootFixtures.cs') 'CarbonLuau.LootFixtures.cs'
        }
        if ($IncludeD3Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootPublicFixtures.cs') 'CarbonLuau.LootPublicFixtures.cs'
        }
        if ($IncludeD4Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootBarrel2Fixtures.cs') 'CarbonLuau.LootBarrel2Fixtures.cs'
        }
        if ($IncludeD5Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootItemFixtures.cs') 'CarbonLuau.LootItemFixtures.cs'
            if ($D5ProbeOnly) {
                $Entry = $Archive.CreateEntry('CarbonLuau.LootItemProbeFixtures.cs', [IO.Compression.CompressionLevel]::Optimal)
                $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
                $Writer = [IO.StreamWriter]::new($Entry.Open(), [Text.UTF8Encoding]::new($false))
                try { $Writer.Write('namespace Carbon.Plugins { public partial class CarbonLuau { partial void SelectLootItemProbe(ref bool ProbeOnly) { ProbeOnly = true; } } }') }
                finally { $Writer.Dispose() }
            }
        }
        if ($IncludeD6Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootRegistryFixtures.cs') 'CarbonLuau.LootRegistryFixtures.cs'
        }
        if ($IncludeD7Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootAdditionalItemFixtures.cs') 'CarbonLuau.LootAdditionalItemFixtures.cs'
            if ($D7ProbeOnly) {
                $Entry = $Archive.CreateEntry('CarbonLuau.LootAdditionalItemProbeFixtures.cs', [IO.Compression.CompressionLevel]::Optimal)
                $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
                $Writer = [IO.StreamWriter]::new($Entry.Open(), [Text.UTF8Encoding]::new($false))
                try { $Writer.Write('namespace Carbon.Plugins { public partial class CarbonLuau { partial void SelectLootAdditionalProbe(ref bool ProbeOnly) { ProbeOnly = true; } } }') }
                finally { $Writer.Dispose() }
            }
        }
        if ($IncludeD8StackFixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootStackFixtures.cs') 'CarbonLuau.LootStackFixtures.cs'
            if (!$D8StackGapOnly) {
                if ($D8StackSweep) {
                    Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootFinalItemFixtures.cs') 'CarbonLuau.LootFinalItemFixtures.cs'
                } else {
                    Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootStackMatrixFixtures.cs') 'CarbonLuau.LootStackMatrixFixtures.cs'
                }
                $Entry = $Archive.CreateEntry('CarbonLuau.LootStackModeFixtures.cs', [IO.Compression.CompressionLevel]::Optimal)
                $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
                $Writer = [IO.StreamWriter]::new($Entry.Open(), [Text.UTF8Encoding]::new($false))
                $Body = 'namespace Carbon.Plugins { public partial class CarbonLuau { partial void SelectLootStackMode(ref bool GapOnly, ref int Part) { GapOnly = false; Part = ' + $D8StackPart + '; } '
                if ($D8StackSweep) {
                    $Body += 'partial void PopulateLootStackSweepSpecs(System.Collections.Generic.List<LootStackSweepSpec> Specs) { '
                    foreach ($Spec in $D8Specs) { $Body += 'Specs.Add(new LootStackSweepSpec("' + $Spec.Name + '",' + $Spec.Id + ',' + $Spec.Stack + ',"' + $Spec.Family + '")); ' }
                    $Body += '}'
                }
                $Body += '} }'
                try { $Writer.Write($Body) }
                finally { $Writer.Dispose() }
            }
        }
        if ($IncludeD8Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.LootBulkItemFixtures.cs') 'CarbonLuau.LootBulkItemFixtures.cs'
            if ($D8DiscoveryOnly -or $D8ProbeOnly) {
                $Entry = $Archive.CreateEntry('CarbonLuau.LootBulkItemModeFixtures.cs', [IO.Compression.CompressionLevel]::Optimal)
                $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
                $Writer = [IO.StreamWriter]::new($Entry.Open(), [Text.UTF8Encoding]::new($false))
                $ModeBody = if ($D8DiscoveryOnly) { 'DiscoveryOnly = true;' } else { 'ProbeOnly = true;' }
                $SkipBody = 'partial void PopulateLootBulkProbeSkips(System.Collections.Generic.HashSet<string> Names) { '
                foreach ($Name in $D8ProbeSkips) { $SkipBody += 'Names.Add("' + $Name + '"); ' }
                $SkipBody += '}'
                try { $Writer.Write('namespace Carbon.Plugins { public partial class CarbonLuau { partial void SelectLootBulkMode(ref bool DiscoveryOnly, ref bool ProbeOnly) { ' + $ModeBody + ' } ' + $SkipBody + ' } }') }
                finally { $Writer.Dispose() }
            }
            else {
                $Entry = $Archive.CreateEntry('CarbonLuau.LootBulkItemManifestFixtures.cs', [IO.Compression.CompressionLevel]::Optimal)
                $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
                $Writer = [IO.StreamWriter]::new($Entry.Open(), [Text.UTF8Encoding]::new($false))
                $Body = 'namespace Carbon.Plugins { public partial class CarbonLuau { partial void PopulateLootBulkApproved(System.Collections.Generic.List<LootBulkItemSpec> Specs, ref int Offset, ref int Count) { Offset = ' + $D8BatchOffset + '; Count = ' + $D8BatchCount + '; '
                foreach ($Spec in $D8Specs) { $Body += 'Specs.Add(new LootBulkItemSpec("' + $Spec.Name + '",' + $Spec.Id + ',' + $Spec.Stack + ',"' + $Spec.Family + '")); ' }
                $Body += '} } }'
                try { $Writer.Write($Body) }
                finally { $Writer.Dispose() }
            }
        }
        if ($IncludeB2Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDiedFixtures.cs') 'CarbonLuau.EntityDiedFixtures.cs'
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDiedMeleeFixtures.cs') 'CarbonLuau.EntityDiedMeleeFixtures.cs'
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDeathBoundaryFixtures.cs') 'CarbonLuau.EntityDeathBoundaryFixtures.cs'
        }
    } finally { $Archive.Dispose() }
} finally { $Stream.Dispose() }
Write-Output $OutputPath
if ($IncludeD8StackFixtures -and !$D8StackGapOnly -and !$D8StackSweep) {
    $StackProbes = Join-Path $OutputDirectory 'stack-probes'
    New-Item -ItemType Directory -Force -Path $StackProbes | Out-Null
    foreach ($Name in @('CarbonLuauStackProbeA.cs','CarbonLuauStackProbeB.cs')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('../tests/live/' + $Name)) -Destination (Join-Path $StackProbes $Name) -Force
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/scripts') -Destination $OutputDirectory -Recurse -Force
$Examples = Join-Path $OutputDirectory 'examples'
New-Item -ItemType Directory -Force -Path $Examples | Out-Null
foreach ($Name in @('player-events','player-lifecycle','hello-command','player-teleport','player-take-item')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "../examples/$Name") -Destination $Examples -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/addons') -Destination $Examples -Recurse -Force
