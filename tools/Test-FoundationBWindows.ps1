param(
    [string]$Source = (Join-Path $PSScriptRoot '..'),
    [Parameter(Mandatory)][string]$Build,
    [Parameter(Mandatory)][string]$Artifacts
)
$ErrorActionPreference = 'Stop'
$Source = [IO.Path]::GetFullPath($Source)
$Build = [IO.Path]::GetFullPath($Build)
$Artifacts = [IO.Path]::GetFullPath($Artifacts)
New-Item -ItemType Directory -Force $Build,$Artifacts | Out-Null

function Assert-Exit([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE" }
}

Push-Location $Source
try {
    & cmake -S native -B $Build -G 'Visual Studio 17 2022' -A x64
    Assert-Exit 'CMake configure'
    & cmake --build $Build --config Release --parallel 4
    Assert-Exit 'CMake build'
    & ctest --test-dir $Build -C Release --output-on-failure
    Assert-Exit 'Native tests'
    $NativeOutput = if (Test-Path "$Build\Release\carbonluau_native.dll") { "$Build\Release" } else { $Build }
    & .\tools\Test-WindowsImports.ps1 -Library "$NativeOutput\carbonluau_native.dll"

    & dotnet build tests\managed\LoaderTests.csproj -c Release -o "$Artifacts\loader"
    Assert-Exit 'Loader test build'
    & dotnet build tests\runtime\RuntimeTests.csproj -c Release -o "$Artifacts\runtime"
    Assert-Exit 'Runtime test build'
    & "$Artifacts\runtime\RuntimeTests.exe" "$NativeOutput\carbonluau_native.dll" `
        "$NativeOutput\WrongAbi.dll" "$NativeOutput\LegacyProbe.dll" $Source `
        "$NativeOutput\carbonluau_compiler.exe"
    Assert-Exit 'Runtime tests'
    & "$Artifacts\loader\LoaderTests.exe" "$NativeOutput\carbonluau_native.dll" `
        "$NativeOutput\WrongProbe.dll" "$NativeOutput\MissingSymbol.dll"
    Assert-Exit 'Loader tests'

    & .\tools\package.ps1 -OutputDirectory "$Artifacts\package"
    & .\tools\Test-Package.ps1 -Package "$Artifacts\package\CarbonLuau.cszip"
    & .\tools\Test-Api.ps1
    Write-Output '[CarbonLuau:FoundationBWorker] WINDOWS PASS'
} finally { Pop-Location }
