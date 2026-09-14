param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\build\win-x64'))
$ErrorActionPreference = 'Stop'
cmake -S (Join-Path $PSScriptRoot '..\native') -B $BuildDirectory -G 'Visual Studio 17 2022' -A x64
if ($LASTEXITCODE) { throw 'CMake configure failed' }
cmake --build $BuildDirectory --config Release --parallel 4
if ($LASTEXITCODE) { throw 'Native build failed' }
ctest --test-dir $BuildDirectory -C Release --output-on-failure
if ($LASTEXITCODE) { throw 'Native tests failed' }
