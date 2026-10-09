param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'), [switch]$IncludePhase1Fixtures, [switch]$IncludePhase3Fixtures, [switch]$IncludePhase5Fixtures, [switch]$IncludeFoundationEFixtures, [switch]$IncludePlayer1FBFixtures, [switch]$IncludeEntity1AFixtures, [switch]$IncludeEntity1BFixtures, [switch]$IncludeDiscoveryFixtures, [switch]$IncludeDiscovery2BFixtures, [switch]$IncludeGameplayHostProofFixtures, [switch]$IncludeGameplayPublicFixtures)
$ErrorActionPreference = 'Stop'
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
