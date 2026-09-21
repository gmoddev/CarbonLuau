#!/usr/bin/env bash
set -euo pipefail

SourceRoot="${1:-/work/src}"
WorkerRoot="${2:-/tmp/carbonluau-gui3c-worker}"
Image="carbonluau-gui3c"

mkdir -p "$WorkerRoot/build" "$WorkerRoot/artifacts"
docker build --file "$SourceRoot/tools/phase5-linux.Dockerfile" --tag "$Image" "$SourceRoot"
docker run --rm --cpus 4 --memory 8g \
    --volume "$SourceRoot:/work/src" \
    --volume "$WorkerRoot/build:/work/build" \
    --volume "$WorkerRoot/artifacts:/work/artifacts" \
    --workdir /work/src \
    "$Image" bash -lc '
set -euo pipefail
cmake -S native -B /work/build/release -DCMAKE_BUILD_TYPE=Release
cmake --build /work/build/release --parallel 4
ctest --test-dir /work/build/release --output-on-failure
dotnet build tests/managed/LoaderTests.csproj -c Release -o /work/artifacts/loader
dotnet build tests/runtime/RuntimeTests.csproj -c Release -o /work/artifacts/runtime
mono /work/artifacts/runtime/RuntimeTests.exe \
    /work/build/release/libcarbonluau_native.so \
    /work/build/release/libWrongAbi.so \
    /work/build/release/libLegacyProbe.so \
    /work/src \
    /work/build/release/carbonluau_compiler
mono /work/artifacts/loader/LoaderTests.exe \
    /work/build/release/libcarbonluau_native.so \
    /work/build/release/libWrongProbe.so \
    /work/build/release/libMissingSymbol.so
nm -D --defined-only /work/build/release/libcarbonluau_native.so | grep -w carbonluau_probe
pwsh -NoProfile -File tools/Test-Architecture.ps1
pwsh -NoProfile -File tools/Test-Api.ps1
pwsh -NoProfile -File tools/Test-Release.ps1 \
    -Rid linux-x64 \
    -NativeLibrary /work/build/release/libcarbonluau_native.so \
    -CompilerWorker /work/build/release/carbonluau_compiler
cmake -S native -B /work/build/asan -DCMAKE_BUILD_TYPE=Debug -DCARBONLUAU_SANITIZE=ON
cmake --build /work/build/asan --parallel 4
ASAN_OPTIONS=detect_leaks=1:halt_on_error=1 \
UBSAN_OPTIONS=halt_on_error=1:print_stacktrace=1 \
ctest --test-dir /work/build/asan --output-on-failure
echo "[CarbonLuau:Gui3CWorker] LINUX RUNTIME, RELEASE CHECKS AND SANITIZERS PASS"
'
