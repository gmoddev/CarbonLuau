param(
    [ValidateSet('win-x64','linux-x64')][string]$Rid,
    [string]$NativeLibrary,
    [string]$CompilerWorker
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
if ($Release.tag -cne "v$($Release.releaseVersion)" -or $Release.packageVersion -cne $Release.releaseVersion) {
    throw 'Release tag/package identity mapping is inconsistent'
}
$Checks = @(
    @{ Path = 'src/CarbonLuau/CarbonLuau.Main.cs'; Text = "[Info(`"CarbonLuau`", `"gmoddev`", `"$($Release.packageVersion)`")]" },
    @{ Path = 'src/CarbonLuau/CarbonLuau.Main.cs'; Text = "PackageVersion = `"$($Release.packageVersion)`"" },
    @{ Path = 'src/CarbonLuau/Facade/FacadePolicy.cs'; Text = "ApiVersion = `"$($Release.apiVersion)`"" },
    @{ Path = 'native/CMakeLists.txt'; Text = "project(CarbonLuauNative VERSION $($Release.packageVersion)" },
    @{ Path = 'native/src/Runtime.cpp'; Text = 'carbonluau_abi_version(void) { return 0x00010004; }' },
    @{ Path = 'src/CarbonLuau/Addons/AddonPackage.cs'; Text = "ProtocolName = `"$($Release.providerProtocolName)`", ProtocolVersion = `"$($Release.providerProtocolVersion)`"" },
    @{ Path = 'src/CarbonLuau/Addons/AddonPackage.cs'; Text = "Schema = $($Release.packageSchema)" },
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
    if ($Rid -or $NativeLibrary -or $CompilerWorker) {
        if (!$Rid -or !$NativeLibrary -or !$CompilerWorker) { throw 'Rid, NativeLibrary and CompilerWorker must be supplied together' }
        $FirstRelease = Join-Path $Temp 'release-first'
        $SecondRelease = Join-Path $Temp 'release-second'
        & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') -Rid $Rid -NativeLibrary $NativeLibrary -CompilerWorker $CompilerWorker -OutputDirectory $FirstRelease | Out-Null
        & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') -Rid $Rid -NativeLibrary $NativeLibrary -CompilerWorker $CompilerWorker -OutputDirectory $SecondRelease | Out-Null
        $Name = "CarbonLuau-v$($Release.releaseVersion)-$Rid.zip"
        if ((Get-FileHash -Algorithm SHA256 (Join-Path $FirstRelease $Name)).Hash -cne
            (Get-FileHash -Algorithm SHA256 (Join-Path $SecondRelease $Name)).Hash) {
            throw "Release bundle for $Rid is not byte-for-byte reproducible"
        }
        $ExpectedCompiler = if ($Rid -eq 'win-x64') { 'carbonluau_compiler.exe' } else { 'carbonluau_compiler' }
        $Bundle = [IO.Compression.ZipFile]::OpenRead((Join-Path $FirstRelease $Name))
        try {
            $WorkerEntry = "carbon/data/CarbonLuau/native/$Rid/$ExpectedCompiler"
            if (!($Bundle.Entries | Where-Object { $_.FullName -ceq $WorkerEntry })) {
                throw "Release bundle is missing compiler worker: $WorkerEntry"
            }
            foreach ($ExampleEntry in @('examples/player-take-item/init.luau','examples/player-status/init.luau','examples/player-inventory/init.luau','examples/player-give-item/init.luau','examples/player-shop/init.luau','examples/gui/inventory-reward/init.luau','examples/gui/hello/init.luau','examples/gui/shared-live/init.luau',
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
                    throw "Release bundle is missing public GUI example: $ExampleEntry"
                }
            }
            foreach ($DocumentEntry in @('GUI.md','GUI-REFERENCE.md','RELEASE-NOTES.md')) {
                if (!($Bundle.Entries | Where-Object { $_.FullName -ceq $DocumentEntry })) {
                    throw "Release bundle is missing public GUI documentation: $DocumentEntry"
                }
            }
            $ProvenanceEntry = $Bundle.Entries | Where-Object { $_.FullName -ceq 'PROVENANCE.json' }
            $Reader = New-Object IO.StreamReader($ProvenanceEntry.Open())
            try { $BundleProvenance = $Reader.ReadToEnd() | ConvertFrom-Json } finally { $Reader.Dispose() }
            if (!$BundleProvenance.compilerSha256) { throw 'Release provenance is missing compiler worker hash' }
        } finally { $Bundle.Dispose() }
    }
} finally {
    if (Test-Path -LiteralPath $Temp) { Remove-Item -LiteralPath $Temp -Recurse -Force }
}
Write-Output '[CarbonLuau:ReleaseTest] PASS identity mapping, intended contents and deterministic packaging'
