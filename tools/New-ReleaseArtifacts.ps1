param(
    [Parameter(Mandatory)][ValidateSet('win-x64','linux-x64')][string]$Rid,
    [Parameter(Mandatory)][string]$NativeLibrary,
    [Parameter(Mandatory)][string]$CompilerWorker,
    [string]$StorageWorker,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist\release')
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$NativePath = (Resolve-Path -LiteralPath $NativeLibrary).Path
$CompilerPath = (Resolve-Path -LiteralPath $CompilerWorker).Path
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
$ExpectedNativeName = if ($Rid -eq 'win-x64') { 'carbonluau_native.dll' } else { 'libcarbonluau_native.so' }
$ExpectedCompilerName = if ($Rid -eq 'win-x64') { 'carbonluau_compiler.exe' } else { 'carbonluau_compiler' }
$ExpectedStorageName = if ($Rid -eq 'win-x64') { 'carbonluau_storage.exe' } else { 'carbonluau_storage' }
if (!$StorageWorker) { $StorageWorker = Join-Path ([IO.Path]::GetDirectoryName($NativePath)) $ExpectedStorageName }
$StoragePath = (Resolve-Path -LiteralPath $StorageWorker).Path
if ([IO.Path]::GetFileName($StoragePath) -cne $ExpectedStorageName) { throw "Storage worker for $Rid must be named $ExpectedStorageName" }
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
    $SourceChanges = @(& git -C $Root status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE) { throw 'Unable to determine source working-tree state' }
    $SourceState = if ($SourceChanges.Count) { 'uncommitted-working-tree' } else { 'committed' }
    $Provenance = [ordered]@{
        releaseVersion = $Release.releaseVersion
        tag = $Release.tag
        packageVersion = $Release.packageVersion
        api = "$($Release.apiName) $($Release.apiVersion)"
        nativeAbi = $Release.nativeAbi
        providerProtocol = "$($Release.providerProtocolName)/$($Release.providerProtocolVersion)"
        packageSchema = $Release.packageSchema
        luauRevision = $Release.luauRevision
        rid = $Rid
        sourceRevision = $SourceRevision
        sourceState = $SourceState
        packageSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PackagePath).Hash.ToLowerInvariant()
        nativeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $NativePath).Hash.ToLowerInvariant()
        compilerSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $CompilerPath).Hash.ToLowerInvariant()
        storageSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $StoragePath).Hash.ToLowerInvariant()
        storageProtocol = 1
        sqliteVersion = '3.53.4'
        sqliteCompileOptions = @('THREADSAFE=0', 'OMIT_LOAD_EXTENSION', 'OMIT_WAL')
        sqliteSourceId = '2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc'
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
                @{ Source = $StoragePath; Name = "carbon/data/CarbonLuau/native/$Rid/$ExpectedStorageName"; Executable = ($Rid -eq 'linux-x64') },
                @{ Source = (Join-Path $Root 'examples/hello-command/init.luau'); Name = 'examples/hello-command/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-events/init.luau'); Name = 'examples/player-events/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-take-item/init.luau'); Name = 'examples/player-take-item/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-status/init.luau'); Name = 'examples/player-status/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-inventory/init.luau'); Name = 'examples/player-inventory/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-give-item/init.luau'); Name = 'examples/player-give-item/init.luau' },
                @{ Source = (Join-Path $Root 'examples/player-shop/init.luau'); Name = 'examples/player-shop/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/inventory-reward/init.luau'); Name = 'examples/gui/inventory-reward/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/hello/init.luau'); Name = 'examples/gui/hello/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/shared-live/init.luau'); Name = 'examples/gui/shared-live/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/per-player/init.luau'); Name = 'examples/gui/per-player/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/activated/init.luau'); Name = 'examples/gui/activated/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/images/init.luau'); Name = 'examples/gui/images/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/scrolling/init.luau'); Name = 'examples/gui/scrolling/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/layout-vertical/init.luau'); Name = 'examples/gui/layout-vertical/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/layout-horizontal/init.luau'); Name = 'examples/gui/layout-horizontal/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/padding/init.luau'); Name = 'examples/gui/padding/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/layout-order/init.luau'); Name = 'examples/gui/layout-order/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/image-label/init.luau'); Name = 'examples/gui/image-label/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/image-button/init.luau'); Name = 'examples/gui/image-button/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/item-skin/init.luau'); Name = 'examples/gui/item-skin/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/steam-avatar/init.luau'); Name = 'examples/gui/steam-avatar/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/scrolling-layout/init.luau'); Name = 'examples/gui/scrolling-layout/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/scroll-effects/init.luau'); Name = 'examples/gui/scroll-effects/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/grid/init.luau'); Name = 'examples/gui/grid/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/grid-vertical/init.luau'); Name = 'examples/gui/grid-vertical/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/grid-padding/init.luau'); Name = 'examples/gui/grid-padding/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/grid-scrolling/init.luau'); Name = 'examples/gui/grid-scrolling/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/clipping/init.luau'); Name = 'examples/gui/clipping/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/nested-clipping/init.luau'); Name = 'examples/gui/nested-clipping/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/fonts/init.luau'); Name = 'examples/gui/fonts/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/font-patch/init.luau'); Name = 'examples/gui/font-patch/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/per-player-scroll/init.luau'); Name = 'examples/gui/per-player-scroll/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/foundation3-combined/init.luau'); Name = 'examples/gui/foundation3-combined/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/shared-rich/init.luau'); Name = 'examples/gui/shared-rich/init.luau' },
                @{ Source = (Join-Path $Root 'examples/gui/per-player-rich/init.luau'); Name = 'examples/gui/per-player-rich/init.luau' },
                @{ Source = (Join-Path $Root 'examples/scripts/init.luau'); Name = 'examples/scripts/init.luau' },
                @{ Source = (Join-Path $Root 'examples/scripts/modules/message.luau'); Name = 'examples/scripts/modules/message.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/addon.json'); Name = 'examples/addons/economy/addon.json' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/init.luau'); Name = 'examples/addons/economy/init.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/api.luau'); Name = 'examples/addons/economy/api.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/economy/formatting.luau'); Name = 'examples/addons/economy/formatting.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/shop/addon.json'); Name = 'examples/addons/shop/addon.json' },
                @{ Source = (Join-Path $Root 'examples/addons/shop/init.luau'); Name = 'examples/addons/shop/init.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/guiowner/addon.json'); Name = 'examples/addons/guiowner/addon.json' },
                @{ Source = (Join-Path $Root 'examples/addons/guiowner/init.luau'); Name = 'examples/addons/guiowner/init.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/guiowner/api.luau'); Name = 'examples/addons/guiowner/api.luau' },
                @{ Source = (Join-Path $Root 'examples/addons/guiconsumer/addon.json'); Name = 'examples/addons/guiconsumer/addon.json' },
                @{ Source = (Join-Path $Root 'examples/addons/guiconsumer/init.luau'); Name = 'examples/addons/guiconsumer/init.luau' },
                @{ Source = (Join-Path $Root 'docs/Installation.md'); Name = 'INSTALLATION.md' },
                @{ Source = (Join-Path $Root "docs/releases/$($Release.releaseVersion).md"); Name = 'RELEASE-NOTES.md' },
                @{ Source = (Join-Path $Root 'docs/api/Gui.md'); Name = 'GUI.md' },
                @{ Source = (Join-Path $Root 'docs/api/Gui-Reference.md'); Name = 'GUI-REFERENCE.md' },
                @{ Source = (Join-Path $Root 'LICENSE'); Name = 'LICENSE' },
                @{ Source = (Join-Path $Root 'THIRD_PARTY_NOTICES.md'); Name = 'THIRD_PARTY_NOTICES.md' },
                @{ Source = (Join-Path $Root 'native/third_party/luau/LICENSE.txt'); Name = 'LUAU-LICENSE.txt' },
                @{ Source = (Join-Path $Root 'native/third_party/luau/lua_LICENSE.txt'); Name = 'LUA-LICENSE.txt' },
                @{ Source = $ProvenancePath; Name = 'PROVENANCE.json' }
            )
            # Include every existing public Luau/addon example, including the
            # position/health/Teleport and local-module examples.
            $Included = @{}; foreach ($File in $Files) { $Included[$File.Name] = $true }
            foreach ($Example in (Get-ChildItem -LiteralPath (Join-Path $Root 'examples') -Recurse -File)) {
                if ($Example.Extension -cne '.luau' -and $Example.Name -cne 'addon.json') { continue }
                $Name = 'examples/' + [IO.Path]::GetRelativePath((Join-Path $Root 'examples'), $Example.FullName).Replace('\','/')
                if (!$Included.ContainsKey($Name)) { $Files += @{ Source = $Example.FullName; Name = $Name } }
            }
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
