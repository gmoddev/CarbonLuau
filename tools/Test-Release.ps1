param(
    [ValidateSet('win-x64','linux-x64')][string]$Rid,
    [string]$NativeLibrary
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
if ($Release.tag -cne "v$($Release.releaseVersion)" -or $Release.packageVersion -cne $Release.releaseVersion) {
    throw 'Release tag/package identity mapping is inconsistent'
}
$Checks = @(
    @{ Path = 'src/CarbonLuau/CarbonLuau.Main.cs'; Text = "[Info(`"CarbonLuau`", `"gmoddev`", `"$($Release.packageVersion)`")]" },
    @{ Path = 'src/CarbonLuau/CarbonLuau.Facade.cs'; Text = "ApiVersion = `"$($Release.apiVersion)`"" },
    @{ Path = 'native/CMakeLists.txt'; Text = "project(CarbonLuauNative VERSION $($Release.packageVersion)" },
    # The published v0.3.0 artifact remains ABI 1.2. Post-release Foundation A
    # source is the additive ABI 1.3 development line and does not rewrite that tag.
    @{ Path = 'native/src/Runtime.cpp'; Text = 'carbonluau_abi_version(void) { return 0x00010003; }' },
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
    if ($Rid -or $NativeLibrary) {
        if (!$Rid -or !$NativeLibrary) { throw 'Rid and NativeLibrary must be supplied together' }
        $FirstRelease = Join-Path $Temp 'release-first'
        $SecondRelease = Join-Path $Temp 'release-second'
        & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') -Rid $Rid -NativeLibrary $NativeLibrary -OutputDirectory $FirstRelease | Out-Null
        & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') -Rid $Rid -NativeLibrary $NativeLibrary -OutputDirectory $SecondRelease | Out-Null
        $Name = "CarbonLuau-v$($Release.releaseVersion)-$Rid.zip"
        if ((Get-FileHash -Algorithm SHA256 (Join-Path $FirstRelease $Name)).Hash -cne
            (Get-FileHash -Algorithm SHA256 (Join-Path $SecondRelease $Name)).Hash) {
            throw "Release bundle for $Rid is not byte-for-byte reproducible"
        }
    }
} finally {
    if (Test-Path -LiteralPath $Temp) { Remove-Item -LiteralPath $Temp -Recurse -Force }
}
Write-Output '[CarbonLuau:ReleaseTest] PASS identity mapping, intended contents and deterministic packaging'
