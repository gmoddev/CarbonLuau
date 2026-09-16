param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Package))
try {
    $Names = @($Archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
    $Expected = @('CarbonLuau.Addons.Carbon.cs', 'CarbonLuau.Addons.cs', 'CarbonLuau.Carbon.cs', 'CarbonLuau.Facade.cs', 'CarbonLuau.Main.cs', 'CarbonLuau.Native.cs', 'CarbonLuau.Runtime.cs', 'CarbonLuau.Scripts.cs')
    if (($Names -join '|') -ne ($Expected -join '|')) { throw "Unexpected production package contents: $Names" }
    Write-Output '[CarbonLuau:PackageTest] PASS: eight production C# sources; no native files or live fixtures'
} finally { $Archive.Dispose() }
