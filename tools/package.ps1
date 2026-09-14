param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'), [switch]$IncludePhase1Fixtures)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputPath = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'CarbonLuau.cszip'
$Stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create)
try {
    $Archive = New-Object IO.Compression.ZipArchive($Stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        Get-ChildItem (Join-Path $PSScriptRoot '..\src\CarbonLuau') -Filter '*.cs' | Sort-Object Name | ForEach-Object {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($Archive, $_.FullName, $_.Name) | Out-Null
        }
        if ($IncludePhase1Fixtures) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($Archive,
                (Join-Path $PSScriptRoot '../tests/live/CarbonLuau.Fixtures.cs'), 'CarbonLuau.Fixtures.cs') | Out-Null
        }
    } finally { $Archive.Dispose() }
} finally { $Stream.Dispose() }
Write-Output $OutputPath
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../examples/scripts') -Destination $OutputDirectory -Recurse -Force
