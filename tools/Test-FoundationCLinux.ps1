param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Build,
    [Parameter(Mandatory)][string]$Artifacts,
    [string]$Image = 'carbonluau-foundation-c'
)
$ErrorActionPreference = 'Stop'
$Source = [IO.Path]::GetFullPath($Source)
$Build = [IO.Path]::GetFullPath($Build)
$Artifacts = [IO.Path]::GetFullPath($Artifacts)
New-Item -ItemType Directory -Force $Build,$Artifacts | Out-Null

& docker build --file (Join-Path $Source 'tools\phase5-linux.Dockerfile') --tag $Image $Source
if ($LASTEXITCODE -ne 0) { throw "Foundation C Linux image build failed with exit code $LASTEXITCODE" }

$Script = @'
set -euo pipefail
cmake -S /work/src/native -B /work/build/release -DCMAKE_BUILD_TYPE=Release
cmake --build /work/build/release --parallel 4
ctest --test-dir /work/build/release --output-on-failure
dotnet build /work/src/tests/managed/LoaderTests.csproj -c Release -o /work/artifacts/loader
dotnet build /work/src/tests/runtime/RuntimeTests.csproj -c Release -o /work/artifacts/runtime
mono /work/artifacts/runtime/RuntimeTests.exe \
    /work/build/release/libcarbonluau_native.so \
    /work/build/release/libWrongAbi.so \
    /work/build/release/libLegacyProbe.so \
    /work/src
mono /work/artifacts/loader/LoaderTests.exe \
    /work/build/release/libcarbonluau_native.so \
    /work/build/release/libWrongProbe.so \
    /work/build/release/libMissingSymbol.so
nm -D --defined-only /work/build/release/libcarbonluau_native.so | grep -w carbonluau_probe
cmake -S /work/src/native -B /work/build/asan -DCMAKE_BUILD_TYPE=Debug -DCARBONLUAU_SANITIZE=ON
cmake --build /work/build/asan --parallel 4
ASAN_OPTIONS=detect_leaks=1:halt_on_error=1 \
UBSAN_OPTIONS=halt_on_error=1:print_stacktrace=1 \
ctest --test-dir /work/build/asan --output-on-failure
echo '[CarbonLuau:FoundationCWorker] LINUX AND SANITIZERS PASS'
'@

& docker run --rm `
    --volume "${Source}:/work/src" `
    --volume "${Build}:/work/build" `
    --volume "${Artifacts}:/work/artifacts" `
    --workdir /work/src `
    $Image bash -lc $Script
if ($LASTEXITCODE -ne 0) { throw "Foundation C Linux/sanitizer validation failed with exit code $LASTEXITCODE" }
