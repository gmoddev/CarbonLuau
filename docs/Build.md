# Build and test

This tree builds without private repository access or publishing credentials.
Use Windows x64 or Linux x64, CMake 3.20+, a C++17 compiler, Python 3.12+, PowerShell
7 and .NET SDK 9 (runtime fixtures) / 10 (optional tooling). Linux .NET Framework
fixtures require Mono. NuGet and the pinned SQLite download are public build
dependencies; offline runtime operation is distinct from dependency provisioning.

```sh
cmake -S native -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release --parallel 2
ctest --test-dir build -C Release --output-on-failure
dotnet build tests/runtime/RuntimeTests.csproj -c Release -o dist/runtime -m:2
dotnet build tests/managed/LoaderTests.csproj -c Release -o dist/tests -m:2
python tools/publication/Documents.py
```

The public workflow runs existing native, managed, real-VM and sanitizer fixtures,
resource/fairness, gameplay, persistence, failure cleanup, deterministic packaging
and clean installation checks. Dispatch it with release_qualification=true for
the existing combined lifecycle/release lane. Test assertions and deadlines are
the same as development; this snapshot does not recreate live Rust hosts.

For Linux full real-VM execution:

```sh
mono dist/runtime/RuntimeTests.exe "$PWD/build/libcarbonluau_native.so" "$PWD/build/libWrongAbi.so" "$PWD/build/libLegacyProbe.so" "$PWD" "$PWD/build/carbonluau_compiler"
```

On Windows use dist/runtime/RuntimeTests.exe with corresponding absolute paths
under build/Release (carbonluau_native.dll, WrongAbi.dll, LegacyProbe.dll and
carbonluau_compiler.exe). Keep native binaries and workers separate from the
source-only CarbonLuau.cszip. `tools/package.ps1` defaults to production sources;
development live-fixture switches require excluded internal fixtures and are
not part of the public build interface. Use `tools/Test-Release.ps1` for source
package identity/determinism and the public workflow for platform bundles.

Optional editor pack generation uses tools/Build-Tooling.ps1, its exact public
LSP asset pins, and the separately distributed official editor. Until cutover
assigns public commits, existing editor pins remain on their qualified source;
do not silently replace a private SHA with an unrelated public commit.
