param(
    [Parameter(Mandatory)][string]$Extension,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/tooling'),
    [string]$LanguageServerArchive
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$Platform = if ($IsWindows) { "win32-$Architecture" } elseif ($IsLinux) { "linux-$Architecture" } else { "darwin-$Architecture" }
$Rid = if ($IsWindows) { "win-$Architecture" } elseif ($IsLinux) { "linux-$Architecture" } else { "osx-$Architecture" }
$Pin = Get-Content -Raw (Join-Path $Root 'tooling/language-server.json') | ConvertFrom-Json
if (!$Pin.UpstreamAssets.$Platform) { throw "Unsupported tooling platform $Platform" }
$Output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $Output | Out-Null
function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
}
Invoke-Checked dotnet @('build', (Join-Path $Root 'src/CarbonLuau.Core'), '-c', 'Release', '-m:2', '--nologo')
Invoke-Checked dotnet @('run', '--project', (Join-Path $Root 'src/CarbonLuau.Tooling'), '--', '--check-generated', $Root)
Invoke-Checked dotnet @('run', '--project', (Join-Path $Root 'tests/tooling'), '--', $Root)
$Native = Join-Path $Output 'native'
Invoke-Checked cmake @('-S', (Join-Path $Root 'native/tooling'), '-B', $Native, '-DCMAKE_BUILD_TYPE=Release', '-DCARBONLUAU_ANALYSIS_TESTS=ON')
Invoke-Checked cmake @('--build', $Native, '--config', 'Release', '--parallel', '2')
$Publish = Join-Path $Output 'publish'
Invoke-Checked dotnet @('publish', (Join-Path $Root 'src/CarbonLuau.Tooling'), '-c', 'Release', '-r', $Rid, '--self-contained', 'true', '-o', $Publish, '-m:2')
if (!$LanguageServerArchive) {
    $Asset = $Pin.UpstreamAssets.$Platform.Name
    $LanguageServerArchive = Join-Path $Output $Asset
    if (!(Test-Path -LiteralPath $LanguageServerArchive)) {
        # Explicit build/provisioning only. Editor activation never downloads.
        Invoke-WebRequest "https://github.com/JohnnyMorganz/luau-lsp/releases/download/$($Pin.LanguageServerVersion)/$Asset" -OutFile $LanguageServerArchive
    }
}
$Library = if ($IsWindows) { Join-Path $Native 'Release/carbonluau_analysis.dll' } elseif ($IsLinux) { Join-Path $Native 'libcarbonluau_analysis.so' } else { Join-Path $Native 'libcarbonluau_analysis.dylib' }
$Launcher = if ($IsWindows) { Join-Path $Native 'Release/carbonluau-analysis-launcher.exe' } else { Join-Path $Native 'carbonluau-analysis-launcher' }
Invoke-Checked python @((Join-Path $Root 'tools/Provision-ToolingPack.py'), '--platform', $Platform, '--publish', $Publish, '--analysis', $Library, '--launcher', $Launcher, '--lsp-archive', $LanguageServerArchive, '--extension', $Extension)
$Pack = Join-Path ([IO.Path]::GetFullPath($Extension)) "tooling/$Platform"
$env:CARBONLUAU_TOOLING_HOST = Join-Path $Pack $(if ($IsWindows) { 'carbonluau-tooling.exe' } else { 'carbonluau-tooling' })
Invoke-Checked python @((Join-Path $Root 'tests/tooling/TestHost.py'))
if ($Platform -in $Pin.QualifiedAnalysisPlatforms) { Invoke-Checked python @((Join-Path $Root 'tests/tooling/TestAnalysis.py')) }
Write-Output "[CarbonLuau:ToolingPack] Ready for platform qualification: $Pack"
