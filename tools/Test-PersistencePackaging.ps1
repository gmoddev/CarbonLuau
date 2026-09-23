param(
    [Parameter(Mandatory)][string]$Bundle,
    [Parameter(Mandatory)][string]$ManagedTests,
    [string]$WorkDirectory = [IO.Path]::GetTempPath()
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Persistence-1A is private substrate. Remove this phase gate only when 1B is authorized.
foreach ($Relative in @('scripts/bootstrap.luau', 'src/CarbonLuau.Core/Api/ApiCatalog.cs')) {
    if ((Get-Content -Raw -LiteralPath (Join-Path $Root $Relative)) -match '\b(DataStoreService|GetDataStore|GetAsync|SetAsync|RemoveAsync)\b') {
        throw "Persistence-1A unexpectedly exposes public persistence metadata/bindings: $Relative"
    }
}
$ArchivePath = (Resolve-Path -LiteralPath $Bundle).Path
$TestsPath = (Resolve-Path -LiteralPath $ManagedTests).Path
$Parent = (Resolve-Path -LiteralPath $WorkDirectory).Path
$Work = Join-Path $Parent ('CarbonLuauPersistencePackage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Work | Out-Null
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, (Join-Path $Work 'install'))
    $Provenance = Get-Content -Raw -LiteralPath (Join-Path $Work 'install/PROVENANCE.json') | ConvertFrom-Json
    $WindowsHost = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
    $ExpectedRid = if ($WindowsHost) { 'win-x64' } else { 'linux-x64' }
    if ($Provenance.rid -cne $ExpectedRid) { throw 'Cannot execute a different-platform package' }
    $Name = if ($WindowsHost) { 'carbonluau_storage.exe' } else { 'carbonluau_storage' }
    $Worker = Join-Path $Work "install/carbon/data/CarbonLuau/native/$ExpectedRid/$Name"
    $NativeDirectory = [IO.Path]::GetDirectoryName($Worker)
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $Worker).Hash.ToLowerInvariant() -cne $Provenance.storageSha256) { throw 'Extracted worker hash differs' }
    if (!$WindowsHost) {
        # ZipFile extraction may omit Unix modes; mirrors documented deployment chmod.
        & chmod 755 $Worker
        if ($LASTEXITCODE) { throw 'Unable to set extracted worker executable mode' }
        foreach ($Binary in @('libcarbonluau_native.so','carbonluau_compiler','carbonluau_storage')) {
            $Imports = @(& readelf -d (Join-Path $NativeDirectory $Binary))
            if ($LASTEXITCODE) { throw "Unable to inspect Linux imports: $Binary" }
            $Dependencies = @($Imports | Select-String '\(NEEDED\).*\[([^\]]+)\]' | ForEach-Object { $_.Matches[0].Groups[1].Value })
            if (!$Dependencies.Count) { throw 'No dynamic dependency evidence found' }
            foreach ($Dependency in $Dependencies) {
                if ($Dependency -cnotin @('libstdc++.so.6','libgcc_s.so.1','libc.so.6','libm.so.6','libpthread.so.0','libdl.so.2','librt.so.1','ld-linux-x86-64.so.2')) {
                    throw "Unexpected dependency (SQLite/sanitizer/host library): $Binary -> $Dependency"
                }
            }
            Write-Output "[CarbonLuau:PersistencePackage] $Binary imports: $($Dependencies -join ', ')"
        }
    } else {
        foreach ($Binary in @('carbonluau_native.dll','carbonluau_compiler.exe','carbonluau_storage.exe')) {
            & (Join-Path $PSScriptRoot 'Test-WindowsImports.ps1') -Library (Join-Path $NativeDirectory $Binary)
        }
    }
    Push-Location $Work
    try {
        if ($WindowsHost) { & $TestsPath $Worker } else { & mono $TestsPath $Worker }
        if ($LASTEXITCODE) { throw 'Clean extracted production storage worker tests failed' }
    } finally { Pop-Location }
    Write-Output "[CarbonLuau:PersistencePackage] PASS clean extracted worker; private API; source=$($Provenance.sourceState); worker=$($Provenance.storageSha256)"
} finally {
    # Only this newly allocated test directory is disposable; never live storage.
    $Resolved = [IO.Path]::GetFullPath($Work)
    if ([IO.Path]::GetDirectoryName($Resolved) -cne [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar)) { throw 'Unexpected cleanup parent' }
    Remove-Item -LiteralPath $Resolved -Recurse -Force
}
