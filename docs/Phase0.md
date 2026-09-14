# Phase 0: native-load feasibility

Phase 0 contains a trivial native probe and a Carbon source plugin. Luau is not linked and no scripting runtime is implemented.

## Build and package

The user's development workflow runs builds and servers on the SSH worker `dockerbox`, under `C:\Sandbox\Codex`. Windows builds use MSVC 2022 with four jobs; Linux builds use the project Dockerfile with four jobs. Server validation runs sequentially.

On a Windows build worker with Visual Studio 2022 C++ tools, CMake and .NET SDK:

```powershell
./tools/build-native.ps1
dotnet build tests/managed/LoaderTests.csproj -c Release -o dist/tests
./dist/tests/LoaderTests.exe "$pwd/build/win-x64/Release/carbonluau_native.dll" "$pwd/build/win-x64/Release/WrongProbe.dll" "$pwd/build/win-x64/Release/MissingSymbol.dll"
./tools/package.ps1
```

On Linux with CMake, G++, make, Mono and .NET SDK:

```bash
bash tools/build-native.sh
dotnet build tests/managed/LoaderTests.csproj -c Release -o dist/tests
mono dist/tests/LoaderTests.exe "$PWD/build/linux-x64/libcarbonluau_native.so" "$PWD/build/linux-x64/libWrongProbe.so" "$PWD/build/linux-x64/libMissingSymbol.so"
nm -D --defined-only build/linux-x64/libcarbonluau_native.so
```

The .NET Framework 4.8 test project compiles the actual managed loader. It does not use a Carbon stub and does not claim to compile or run the Carbon plugin entry point. Live Carbon compiles both source files in the package. CI builds/tests both platforms and uploads the native probe and source package.

## Deployment

Copy `dist/CarbonLuau.cszip` to `carbon/plugins/CarbonLuau.cszip`. Copy only the correct platform's good probe binary to:

```text
carbon/data/CarbonLuau/native/win-x64/carbonluau_native.dll
carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so
```

The `.cszip` contains the two C# partial source files only. Test fixture libraries must never be deployed to a production server. The loader derives the path from Carbon's data directory, normalizes it to an absolute path and never searches other locations for the probe. Linux requires glibc's `libdl.so.2`.

At server initialization or plugin load on an initialized server, expect:

```text
[CarbonLuau:Native] Native probe loaded successfully. Platform: linux-x64; ABI probe: 0x4C554155; Path: <absolute path>
```

For Windows the RID is `win-x64`. On `c.unload CarbonLuau`, expect:

```text
[CarbonLuau:Native] Native library unloaded successfully.
```

Use `c.load CarbonLuau` and `c.reload CarbonLuau` from the server console/RCON to repeat the test. This is Carbon's plugin lifecycle, not a new CarbonLuau runtime reload feature.

## Failure behavior

Initialization marks the loader unavailable and logs `[CarbonLuau:Native] Unavailable:` followed by one of: `unsupported platform`, `native library missing`, `native library load failed`, `symbol missing`, `probe invocation failed`, or `probe magic mismatch`. Windows load errors include the Win32 code/message; Linux failures include `dlerror()` details. A partial load releases its handle; disposal is idempotent and reports any OS unload error without throwing through Carbon's unload hook.

The probe is trusted project-owned native code. Catching managed exceptions cannot contain an arbitrary native access violation, process exit or malicious library. Tests for a broken binary use an invalid file rejected by the OS loader, not native code designed to crash the process. No crash containment is claimed.

## Shockbyte acceptance

No Shockbyte credentials or target server access were supplied. Docker validation establishes behavior on the tested self-managed Linux server only. Upload the Linux `.so` and `.cszip` to the exact paths above using the host's permitted file manager/SFTP, then load the plugin and retain the success or failure log. Perform at least ten unload/load cycles and check normal server responses between them. If the provider prohibits custom native libraries or the loader reports an enforced restriction, retain that exact error and mark Phase 0 BLOCKED for that host. Do not change host policy or attempt a bypass.

Actual run results, paths and limitations are recorded in [Phase0-Validation.md](Phase0-Validation.md).

## References

- [Carbon plugin base class](https://carbonmod.gg/devs/creating-your-first-plugin)
- [Carbon partial-source ZIP packages](https://carbonmod.gg/devs/features/zip-script-packages)
- [Carbon installation](https://carbonmod.gg/owners/installing-carbon)
- [Carbon watcher and reload configuration](https://carbonmod.gg/owners/configuring-carbon)
