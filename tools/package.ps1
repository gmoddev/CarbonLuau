param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'), [switch]$IncludePhase1Fixtures, [switch]$IncludePhase3Fixtures, [switch]$IncludePhase5Fixtures, [switch]$IncludeFoundationEFixtures)
$ErrorActionPreference = 'Stop'
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
        Get-ChildItem $SourceRoot -Recurse -File -Filter '*.cs' | Sort-Object FullName | ForEach-Object {
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
    } finally { $Archive.Dispose() }
} finally { $Stream.Dispose() }
Write-Output $OutputPath
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/scripts') -Destination $OutputDirectory -Recurse -Force
$Examples = Join-Path $OutputDirectory 'examples'
New-Item -ItemType Directory -Force -Path $Examples | Out-Null
foreach ($Name in @('player-events','hello-command','player-teleport')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "../examples/$Name") -Destination $Examples -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/addons') -Destination $Examples -Recurse -Force
