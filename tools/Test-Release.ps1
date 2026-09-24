param(
    [ValidateSet('win-x64','linux-x64')][string]$Rid,
    [string]$NativeLibrary,
    [string]$CompilerWorker,
    [string]$StorageWorker
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
if ($Release.tag -cne "v$($Release.releaseVersion)" -or $Release.packageVersion -cne $Release.releaseVersion) {
    throw 'Release tag/package identity mapping is inconsistent'
}
# Package, scripting API and native ABI are deliberately separate identities.
$NativeAbi = [version]$Release.nativeAbi
if ($NativeAbi.Major -gt 65535 -or $NativeAbi.Minor -gt 65535) { throw 'Native ABI component exceeds uint16' }
$NativeAbiLiteral = '0x{0:x8}' -f (($NativeAbi.Major -shl 16) -bor $NativeAbi.Minor)
$Checks = @(
    @{ Path = 'src/CarbonLuau/CarbonLuau.Main.cs'; Text = "[Info(`"CarbonLuau`", `"gmoddev`", `"$($Release.packageVersion)`")]" },
    @{ Path = 'src/CarbonLuau/CarbonLuau.Main.cs'; Text = "PackageVersion = `"$($Release.packageVersion)`"" },
    @{ Path = 'src/CarbonLuau/Facade/FacadePolicy.cs'; Text = "ApiVersion = `"$($Release.apiVersion)`"" },
    @{ Path = 'native/CMakeLists.txt'; Text = "project(CarbonLuauNative VERSION $($Release.packageVersion)" },
    @{ Path = 'native/src/Runtime.cpp'; Text = "carbonluau_abi_version(void) { return $NativeAbiLiteral; }" },
    @{ Path = 'src/CarbonLuau.Core/Addons/CoreAddonPackage.cs'; Text = "ProtocolName = `"$($Release.providerProtocolName)`", ProtocolVersion = `"$($Release.providerProtocolVersion)`"" },
    @{ Path = 'src/CarbonLuau.Core/Addons/CoreAddonPackage.cs'; Text = "Schema = $($Release.packageSchema)" },
    @{ Path = 'src/CarbonLuau/Addons/AddonPackage.cs'; Text = 'ProtocolName = global::CarbonLuau.Core.AddonPolicy.ProtocolName' },
    @{ Path = 'src/CarbonLuau/Addons/AddonPackage.cs'; Text = 'Schema = global::CarbonLuau.Core.AddonPolicy.Schema' },
    @{ Path = 'native/third_party/LUAU_REVISION.txt'; Text = "Pinned commit: $($Release.luauRevision)" },
    @{ Path = 'docs/Release.md'; Text = $Release.tag }
)
foreach ($Check in $Checks) {
    if (!(Get-Content -Raw -LiteralPath (Join-Path $Root $Check.Path)).Contains($Check.Text)) {
        throw "Release identity missing from $($Check.Path): $($Check.Text)"
    }
}

$Temp = Join-Path ([IO.Path]::GetTempPath()) ('CarbonLuauReleaseTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Temp | Out-Null
try {
    $First = Join-Path $Temp 'first'
    $Second = Join-Path $Temp 'second'
    & (Join-Path $PSScriptRoot 'package.ps1') -OutputDirectory $First | Out-Null
    & (Join-Path $PSScriptRoot 'package.ps1') -OutputDirectory $Second | Out-Null
    $FirstPackage = Join-Path $First 'CarbonLuau.cszip'
    $SecondPackage = Join-Path $Second 'CarbonLuau.cszip'
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Package $FirstPackage | Out-Null
    if ((Get-FileHash -Algorithm SHA256 $FirstPackage).Hash -cne (Get-FileHash -Algorithm SHA256 $SecondPackage).Hash) {
        throw 'Production package is not byte-for-byte reproducible'
    }
    if ($Rid -or $NativeLibrary -or $CompilerWorker -or $StorageWorker) {
        if (!$Rid -or !$NativeLibrary -or !$CompilerWorker) { throw 'Rid, NativeLibrary and CompilerWorker must be supplied together' }
        $FirstRelease = Join-Path $Temp 'release-first'
        $SecondRelease = Join-Path $Temp 'release-second'
        & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') -Rid $Rid -NativeLibrary $NativeLibrary -CompilerWorker $CompilerWorker -StorageWorker $StorageWorker -OutputDirectory $FirstRelease | Out-Null
        & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') -Rid $Rid -NativeLibrary $NativeLibrary -CompilerWorker $CompilerWorker -StorageWorker $StorageWorker -OutputDirectory $SecondRelease | Out-Null
        $Name = "CarbonLuau-v$($Release.releaseVersion)-$Rid.zip"
        if ((Get-FileHash -Algorithm SHA256 (Join-Path $FirstRelease $Name)).Hash -cne
            (Get-FileHash -Algorithm SHA256 (Join-Path $SecondRelease $Name)).Hash) {
            throw "Release bundle for $Rid is not byte-for-byte reproducible"
        }
        $ExpectedCompiler = if ($Rid -eq 'win-x64') { 'carbonluau_compiler.exe' } else { 'carbonluau_compiler' }
        $ExpectedStorage = if ($Rid -eq 'win-x64') { 'carbonluau_storage.exe' } else { 'carbonluau_storage' }
        $ExpectedNative = if ($Rid -eq 'win-x64') { 'carbonluau_native.dll' } else { 'libcarbonluau_native.so' }
        $Bundle = [IO.Compression.ZipFile]::OpenRead((Join-Path $FirstRelease $Name))
        try {
            $DeploymentEntries = @('carbon/plugins/CarbonLuau.cszip',
                "carbon/data/CarbonLuau/native/$Rid/$ExpectedNative",
                "carbon/data/CarbonLuau/native/$Rid/$ExpectedCompiler",
                "carbon/data/CarbonLuau/native/$Rid/$ExpectedStorage")
            foreach ($Entry in $Bundle.Entries) {
                if ($Entry.FullName.StartsWith('carbon/') -and $Entry.FullName -cnotin $DeploymentEntries) {
                    throw "Unexpected deployed file (data/source/fixture/other platform): $($Entry.FullName)"
                }
                if ($Entry.FullName -match '(^/|\\|(?:^|/)\.\.(?:/|$))') { throw 'Unsafe release entry' }
            }
            if (@($Bundle.Entries.FullName | Sort-Object -Unique).Count -ne $Bundle.Entries.Count) { throw 'Duplicate release entry' }
            foreach ($Required in $DeploymentEntries) {
                if ($Required -cnotin $Bundle.Entries.FullName) { throw "Missing deployed file: $Required" }
            }
            if ($Rid -eq 'linux-x64') {
                foreach ($Worker in @($ExpectedCompiler, $ExpectedStorage)) {
                    $Entry = $Bundle.GetEntry("carbon/data/CarbonLuau/native/$Rid/$Worker")
                    if ((($Entry.ExternalAttributes -shr 16) -band 511) -ne 493) { throw "Worker lacks archive mode 0755: $Worker" }
                }
            }
            $WorkerEntry = "carbon/data/CarbonLuau/native/$Rid/$ExpectedCompiler"
            if (!($Bundle.Entries | Where-Object { $_.FullName -ceq $WorkerEntry })) {
                throw "Release bundle is missing compiler worker: $WorkerEntry"
            }
            if (!($Bundle.Entries | Where-Object { $_.FullName -ceq "carbon/data/CarbonLuau/native/$Rid/$ExpectedStorage" })) {
                throw 'Release bundle is missing the private storage worker'
            }
            if ($Bundle.Entries | Where-Object { $_.FullName -match 'store\.sqlite3|StorageFixture|Persistence.*Tests|sqlite3\.(c|h|exe)$|(?:^|/)sqlite3$' }) {
                throw 'Release bundle contains persistence data, source, CLI or fixtures'
            }
            foreach ($ExampleEntry in @('examples/player-take-item/init.luau','examples/player-status/init.luau','examples/player-inventory/init.luau','examples/player-give-item/init.luau','examples/player-shop/init.luau','examples/gui/inventory-reward/init.luau','examples/gui/hello/init.luau','examples/gui/shared-live/init.luau',
                    'examples/persistence/get/init.luau','examples/persistence/set/init.luau','examples/persistence/remove/init.luau',
                    'examples/persistence/player-key/init.luau','examples/persistence/snapshot/init.luau','examples/persistence/errors/init.luau',
                    'examples/gui/per-player/init.luau','examples/gui/activated/init.luau','examples/gui/images/init.luau',
                    'examples/gui/scrolling/init.luau','examples/gui/layout-vertical/init.luau',
                    'examples/gui/layout-horizontal/init.luau','examples/gui/padding/init.luau',
                    'examples/gui/layout-order/init.luau','examples/gui/image-label/init.luau',
                    'examples/gui/image-button/init.luau','examples/gui/item-skin/init.luau',
                    'examples/gui/steam-avatar/init.luau','examples/gui/scrolling-layout/init.luau',
                    'examples/gui/grid/init.luau','examples/gui/grid-vertical/init.luau',
                    'examples/gui/grid-padding/init.luau','examples/gui/grid-scrolling/init.luau',
                    'examples/gui/clipping/init.luau','examples/gui/nested-clipping/init.luau',
                    'examples/gui/fonts/init.luau','examples/gui/font-patch/init.luau',
                    'examples/gui/scroll-effects/init.luau','examples/gui/per-player-scroll/init.luau',
                    'examples/gui/foundation3-combined/init.luau',
                    'examples/gui/shared-rich/init.luau','examples/gui/per-player-rich/init.luau',
                    'examples/addons/guiowner/addon.json','examples/addons/guiowner/init.luau','examples/addons/guiowner/api.luau',
                    'examples/addons/guiconsumer/addon.json','examples/addons/guiconsumer/init.luau')) {
                if (!($Bundle.Entries | Where-Object { $_.FullName -ceq $ExampleEntry })) {
                    throw "Release bundle is missing public example: $ExampleEntry"
                }
            }
            foreach ($DocumentEntry in @('GUI.md','GUI-REFERENCE.md','RELEASE-NOTES.md',
                    'docs/api/Persistence.md','docs/api/Services/DataStoreService.md',
                    'docs/api/Types/DataStore.md','docs/api/Types/PersistedValue.md')) {
                if (!($Bundle.Entries | Where-Object { $_.FullName -ceq $DocumentEntry })) {
                    throw "Release bundle is missing public documentation: $DocumentEntry"
                }
            }
            $ProvenanceEntry = $Bundle.Entries | Where-Object { $_.FullName -ceq 'PROVENANCE.json' }
            $Reader = New-Object IO.StreamReader($ProvenanceEntry.Open())
            try { $BundleProvenance = $Reader.ReadToEnd() | ConvertFrom-Json } finally { $Reader.Dispose() }
            if (!$BundleProvenance.compilerSha256) { throw 'Release provenance is missing compiler worker hash' }
            if (!$BundleProvenance.storageSha256 -or $BundleProvenance.sqliteVersion -cne '3.53.4' -or
                $BundleProvenance.sqliteSourceId -cne '2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc' -or
                $BundleProvenance.storageProtocol -ne 1 -or
                ($BundleProvenance.sqliteCompileOptions -join ',') -cne 'THREADSAFE=0,OMIT_LOAD_EXTENSION,OMIT_WAL') { throw 'Release provenance is missing the pinned storage worker' }
            if ($BundleProvenance.sourceState -cnotin @('committed','uncommitted-working-tree')) { throw 'Release provenance has no source-state label' }
        } finally { $Bundle.Dispose() }
        $Extracted = Join-Path $Temp 'clean-install'
        [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $FirstRelease $Name), $Extracted)
        $Payloads = @{
            packageSha256 = 'carbon/plugins/CarbonLuau.cszip'
            nativeSha256 = "carbon/data/CarbonLuau/native/$Rid/$ExpectedNative"
            compilerSha256 = "carbon/data/CarbonLuau/native/$Rid/$ExpectedCompiler"
            storageSha256 = "carbon/data/CarbonLuau/native/$Rid/$ExpectedStorage"
        }
        foreach ($Field in $Payloads.Keys) {
            $Actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Extracted $Payloads[$Field])).Hash.ToLowerInvariant()
            if ($Actual -cne $BundleProvenance.$Field) { throw "Extracted payload does not match provenance: $Field" }
        }
        & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Package (Join-Path $Extracted 'carbon/plugins/CarbonLuau.cszip') | Out-Null
        Write-Output "[CarbonLuau:ReleaseTest] $Rid SHA256=$((Get-FileHash -Algorithm SHA256 (Join-Path $FirstRelease $Name)).Hash.ToLowerInvariant()) source=$($BundleProvenance.sourceState)"
    }
} finally {
    if (Test-Path -LiteralPath $Temp) { Remove-Item -LiteralPath $Temp -Recurse -Force }
}
Write-Output '[CarbonLuau:ReleaseTest] PASS identity mapping, intended contents and deterministic packaging'
