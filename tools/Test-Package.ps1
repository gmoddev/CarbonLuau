param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Get-CoreSources.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Package))
try {
    $Names = @($Archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
    $SourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\CarbonLuau'))
    $Sources = @(Get-ChildItem $SourceRoot -Recurse -File -Filter '*.cs') + @(Get-CoreSources (Join-Path $PSScriptRoot '..'))
    if (@($Sources | Group-Object Name | Where-Object Count -gt 1).Count) { throw 'Duplicate flattened production source filename' }
    $Expected = @($Sources | ForEach-Object {
        $_.Name
    } | Sort-Object)
    if (($Names -join '|') -ne ($Expected -join '|')) { throw "Unexpected production package contents: $Names" }
    Write-Output "[CarbonLuau:PackageTest] PASS: $($Expected.Count) production C# sources; no native files or live fixtures"
} finally { $Archive.Dispose() }
