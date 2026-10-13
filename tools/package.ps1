param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'), [switch]$IncludePhase1Fixtures, [switch]$IncludePhase3Fixtures, [switch]$IncludePhase5Fixtures, [switch]$IncludeFoundationEFixtures, [switch]$IncludePlayer1FBFixtures, [switch]$IncludeEntity1AFixtures, [switch]$IncludeEntity1BFixtures, [switch]$IncludeDiscoveryFixtures, [switch]$IncludeDiscovery2BFixtures, [switch]$IncludeGameplayHostProofFixtures, [switch]$IncludeGameplayPublicFixtures, [switch]$IncludeEntitySpawnFixtures, [switch]$IncludeEntityDestroyResearch, [switch]$IncludeEntityDestroyedFixtures, [switch]$IncludeC1Fixtures, [switch]$IncludeInventoryNetFixtures, [switch]$IncludeInventoryChangedFixtures, [switch]$IncludeD2Research, [switch]$IncludeD2Fixtures, [switch]$IncludeD3Fixtures, [switch]$IncludeB2Fixtures, [switch]$IncludeD4Fixtures, [switch]$IncludeD5Fixtures, [switch]$D5ProbeOnly, [switch]$IncludeD6Fixtures, [switch]$IncludeD7Fixtures, [switch]$D7ProbeOnly)
$ErrorActionPreference = 'Stop'
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
        if ($IncludeB2Fixtures) {
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDiedFixtures.cs') 'CarbonLuau.EntityDiedFixtures.cs'
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDiedMeleeFixtures.cs') 'CarbonLuau.EntityDiedMeleeFixtures.cs'
            Add-DeterministicEntry $Archive (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.EntityDeathBoundaryFixtures.cs') 'CarbonLuau.EntityDeathBoundaryFixtures.cs'
        }
    } finally { $Archive.Dispose() }
} finally { $Stream.Dispose() }
Write-Output $OutputPath
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/scripts') -Destination $OutputDirectory -Recurse -Force
$Examples = Join-Path $OutputDirectory 'examples'
New-Item -ItemType Directory -Force -Path $Examples | Out-Null
foreach ($Name in @('player-events','player-lifecycle','hello-command','player-teleport','player-take-item')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "../examples/$Name") -Destination $Examples -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/addons') -Destination $Examples -Recurse -Force
