param(
    [Parameter(Mandatory)][ValidateSet('win-x64','linux-x64')][string]$Rid,
    [Parameter(Mandatory)][string]$NativeLibrary,
    [Parameter(Mandatory)][string]$CompilerWorker,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist\release')
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$NativePath = (Resolve-Path -LiteralPath $NativeLibrary).Path
$CompilerPath = (Resolve-Path -LiteralPath $CompilerWorker).Path
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
$ExpectedNativeName = if ($Rid -eq 'win-x64') { 'carbonluau_native.dll' } else { 'libcarbonluau_native.so' }
$ExpectedCompilerName = if ($Rid -eq 'win-x64') { 'carbonluau_compiler.exe' } else { 'carbonluau_compiler' }
if ([IO.Path]::GetFileName($NativePath) -cne $ExpectedNativeName) {
    throw "Native library for $Rid must be named $ExpectedNativeName"
}
if ([IO.Path]::GetFileName($CompilerPath) -cne $ExpectedCompilerName) {
    throw "Compiler worker for $Rid must be named $ExpectedCompilerName"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Add-DeterministicEntry {
    param([IO.Compression.ZipArchive]$Archive, [string]$Source, [string]$Name, [bool]$Executable = $false)
    $Entry = $Archive.CreateEntry($Name.Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal)
    $Entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
    if ($Executable) { $Entry.ExternalAttributes = -2115174400 } # Unix regular file, mode 0755.
    $InputStream = [IO.File]::OpenRead($Source)
    $OutputStream = $Entry.Open()
    try { $InputStream.CopyTo($OutputStream) }
    finally { $OutputStream.Dispose(); $InputStream.Dispose() }
}

$ResolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $ResolvedOutput | Out-Null
$Work = Join-Path ([IO.Path]::GetTempPath()) ('CarbonLuauRelease-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Work | Out-Null
try {
    $PackageDirectory = Join-Path $Work 'package'
    & (Join-Path $PSScriptRoot 'package.ps1') -OutputDirectory $PackageDirectory | Out-Null
    $PackagePath = Join-Path $PackageDirectory 'CarbonLuau.cszip'
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Package $PackagePath | Out-Null

    $SourceRevision = (& git -C $Root rev-parse HEAD).Trim()
    if ($LASTEXITCODE) { throw 'Unable to determine source revision' }
    $Provenance = [ordered]@{
        releaseVersion = $Release.releaseVersion
        tag = $Release.tag
        packageVersion = $Release.packageVersion
        api = "$($Release.apiName) $($Release.apiVersion)"
        nativeAbi = $Release.nativeAbi
        luauRevision = $Release.luauRevision
        rid = $Rid
        sourceRevision = $SourceRevision
        packageSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PackagePath).Hash.ToLowerInvariant()
        nativeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $NativePath).Hash.ToLowerInvariant()
        compilerSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $CompilerPath).Hash.ToLowerInvariant()
    }
    $ProvenancePath = Join-Path $Work 'PROVENANCE.json'
    [IO.File]::WriteAllText($ProvenancePath, (($Provenance | ConvertTo-Json) + "`n"), (New-Object Text.UTF8Encoding($false)))

    $ArchiveName = "CarbonLuau-v$($Release.releaseVersion)-$Rid.zip"
    $ArchivePath = Join-Path $ResolvedOutput $ArchiveName
    $Stream = [IO.File]::Open($ArchivePath, [IO.FileMode]::Create)
    try {
        $Archive = New-Object IO.Compression.ZipArchive($Stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            $Files = @(
                @{ Source = $PackagePath; Name = 'carbon/plugins/CarbonLuau.cszip' },
                @{ Source = $NativePath; Name = "carbon/data/CarbonLuau/native/$Rid/$ExpectedNativeName" },
                @{ Source = $CompilerPath; Name = "carbon/data/CarbonLuau/native/$Rid/$ExpectedCompilerName"; Executable = ($Rid -eq 'linux-x64') },
                @{ Source = (Join-Path $Root 'examples/hello-command/init.luau'); Name = 'examples/hello-command/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-events/init.luau'); Name = 'examples/player-events/init.luau' },
                @{ Source = (Join-Path $Root 'examples/scripts/init.luau'); Name = 'examples/scripts/init.luau' },
                @{ Source = (Join-Path $Root 'examples/scripts/modules/message.luau'); Name = 'examples/scripts/modules/message.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/addon.json'); Name = 'examples/addons/economy/addon.json' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/init.luau'); Name = 'examples/addons/economy/init.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/api.luau'); Name = 'examples/addons/economy/api.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/formatting.luau'); Name = 'examples/addons/economy/formatting.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/shop/addon.json'); Name = 'examples/addons/shop/addon.json' },
                @{ Source = (Join-Path $Root 'examples/addons/shop/init.luau'); Name = 'examples/addons/shop/init.luau' },
                @{ Source = (Join-Path $Root 'docs/Installation.md'); Name = 'INSTALLATION.md' },
                @{ Source = (Join-Path $Root "docs/releases/$($Release.releaseVersion).md"); Name = 'RELEASE-NOTES.md' },
                @{ Source = (Join-Path $Root 'LICENSE'); Name = 'LICENSE' },
                @{ Source = (Join-Path $Root 'THIRD_PARTY_NOTICES.md'); Name = 'THIRD_PARTY_NOTICES.md' },
                @{ Source = $ProvenancePath; Name = 'PROVENANCE.json' }
            )
            foreach ($File in ($Files | Sort-Object Name)) {
                Add-DeterministicEntry $Archive $File.Source $File.Name ([bool]$File.Executable)
            }
        } finally { $Archive.Dispose() }
    } finally { $Stream.Dispose() }

    $PublishedProvenance = Join-Path $ResolvedOutput "CarbonLuau-v$($Release.releaseVersion)-$Rid.provenance.json"
    Copy-Item -LiteralPath $ProvenancePath -Destination $PublishedProvenance -Force
    $ChecksumPath = Join-Path $ResolvedOutput "CarbonLuau-v$($Release.releaseVersion)-$Rid.sha256"
    $ArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $ArchivePath).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($ChecksumPath, "$ArchiveHash  $ArchiveName`n", (New-Object Text.UTF8Encoding($false)))
    Write-Output $ArchivePath
    Write-Output $PublishedProvenance
    Write-Output $ChecksumPath
} finally {
    if (Test-Path -LiteralPath $Work) { Remove-Item -LiteralPath $Work -Recurse -Force }
}
