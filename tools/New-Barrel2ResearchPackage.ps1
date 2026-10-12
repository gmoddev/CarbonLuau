# Test-only delta over the established D2 research fixture. Never a production bundle.
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'package.ps1') -OutputDirectory $OutputDirectory -IncludeD2Research | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Package = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'CarbonLuau.cszip'
$Archive = [IO.Compression.ZipFile]::Open($Package, [IO.Compression.ZipArchiveMode]::Update)
try {
    $Entry = $Archive.GetEntry('CarbonLuau.BarrelLootResearch.cs')
    if ($null -eq $Entry -or $null -ne $Archive.GetEntry('CarbonLuau.LootFixtures.cs')) { throw '[CarbonLuau:D4Research] Wrong fixture package' }
    $Reader = [IO.StreamReader]::new($Entry.Open())
    try { $Source = $Reader.ReadToEnd() } finally { $Reader.Dispose() }
    $Original = 'private const string BarrelResearchPrefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab";'
    $Replacement = 'private const string BarrelResearchPrefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab";'
    if ([regex]::Matches($Source, [regex]::Escape($Original)).Count -ne 1) { throw '[CarbonLuau:D4Research] Research fixture delta drift' }
    $Source = $Source.Replace($Original, $Replacement)
    $Entry.Delete()
    $Entry = $Archive.CreateEntry('CarbonLuau.BarrelLootResearch.cs')
    $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
    $Stream = $Entry.Open()
    try { $Bytes = [Text.UTF8Encoding]::new($false).GetBytes($Source); $Stream.Write($Bytes,0,$Bytes.Length) }
    finally { $Stream.Dispose() }
} finally { $Archive.Dispose() }
Write-Output ('[CarbonLuau:D4Research] TEST ONLY barrel-2 prototype: ' + $Package)
Write-Output ((Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash.ToLowerInvariant())
