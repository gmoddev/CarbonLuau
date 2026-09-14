param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Package))
try {
    $Names = @($Archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
    $Expected = @('CarbonLuau.Main.cs', 'CarbonLuau.Native.cs', 'CarbonLuau.Runtime.cs')
    if (($Names -join '|') -ne ($Expected -join '|')) { throw "Unexpected production package contents: $Names" }
    Write-Output '[CarbonLuau:PackageTest] PASS: three production C# sources; no native files or live fixtures'
} finally { $Archive.Dispose() }
