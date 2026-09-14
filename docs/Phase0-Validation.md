# Phase 0 validation — 2026-09-14

Overall verdict: **PROVEN for the tested environments**. The native-load feasibility gate passes on Windows x64, self-managed Linux Docker, and the user's Shockbyte Linux server. Phase 1 is cleared to begin. Long-term soak testing remains separate from this feasibility verdict.

## Verified build and loader results

Both builds ran on `dockerbox`. No Rust servers or native build workloads ran on the controlling PC.

| Check | Windows x64 | Linux x64 |
|---|---|---|
| Toolchain | MSVC 19.44.35228, VS 2022 | GCC 13.3, Ubuntu 24.04 Docker |
| Native build | PASS | PASS |
| Exact `carbonluau_probe` export | PASS, dumpbin: sole named export | PASS, nm: exported T symbol |
| Native load/call/unload test | PASS, 100 cycles | PASS, 100 cycles |
| Actual managed loader | PASS, .NET Framework 4.8 | PASS, Mono 6.8 |
| Managed lifecycle | PASS, 100 cycles; DLL deleted after each unload | PASS, 100 cycles; SO absent from `/proc/self/maps` |
| Missing, invalid image, missing symbol, wrong magic | PASS, all four failures and subsequent recovery | PASS, all four failures and subsequent recovery |

The loader test project builds without warnings or errors. It tests process architecture/RID rejection, filenames, rooted paths, state transitions and idempotent disposal. The native probe is independent of vendored Luau.

## Windows Carbon runtime

PASS on Carbon `2.0.259.0` / Rust `2633`, Steam build `25230300`. Carbon compiled the two-file `.cszip`, initialized the native loader, and logged `0x4C554155`.

Actual library path:

```text
C:\Sandbox\Codex\Builds\CarbonLuau\server-win\carbon\data\CarbonLuau\native\win-x64\carbonluau_native.dll
```

Ten consecutive unload/load cycles passed. On every unload, the DLL was absent from the Rust process module list and could be overwritten before the next load. Four controlled failures (missing file, invalid file, missing symbol, wrong magic) each logged a local error, kept the server responsive to a correlated RCON `status` request, and recovered after restoring the good probe. A final `c.reload CarbonLuau` and `status` passed. The server received `quit` after validation.

Windows diagnostics included Win32 error 193 for the invalid binary, error 127 for the absent symbol, and a magic mismatch showing `0x00000007`. Live logs are retained on the worker under `C:\Sandbox\Codex\Artifacts\CarbonLuau\server-win.log`.

## Linux Carbon runtime

PASS on Carbon `2.0.259.0` / Rust `2633`, Steam build `25230300`, inside the Ubuntu 24.04 container on `dockerbox`. Carbon compiled the `.cszip` and invoked the probe successfully from:

```text
/work/server-linux/carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so
```

Ten unload/load cycles passed, with the SO absent from the Rust process's `/proc/<pid>/maps` after every unload. Missing-file, invalid-image, missing-symbol and wrong-value fixtures all failed locally, left RCON `status` responsive and recovered when replaced with the good library. A final `c.reload CarbonLuau` passed. The server then handled `quit`, logged native unload and completed shutdown. The test harness exited successfully.

Linux errors include `file too short` for the invalid image, `undefined symbol: carbonluau_probe` for the missing export, and `0x00000007` for the wrong value. Live logs are retained at `C:\Sandbox\Codex\Artifacts\CarbonLuau\server-linux.log`.

## CI

**PASS:** [GitHub Actions run 34822946437](https://github.com/gmoddev/CarbonLuau/actions/runs/34822946437) passed both `probe (windows-latest)` and `probe (ubuntu-24.04)` for implementation commit `2c9ce38cce41a37a9a6392fe08ee73fff6720fa6`.

The workflow builds the native library and .NET Framework loader tests, executes the native/managed tests, packages the C# source and uploads platform artifacts. It does not claim to run Carbon; the live results above came from `dockerbox`.

## Worker storage and operational notes

- Source: `C:\Sandbox\Codex\Workspaces\CarbonLuau`.
- Incremental Windows build: `C:\Sandbox\Codex\Builds\CarbonLuau\win-x64`.
- Initial Linux build: `C:\Sandbox\Codex\Builds\CarbonLuau\linux-x64`.
- Linux server: `/work/server-linux` inside task container `codex-carbonluau-linux`; no published container ports.
- Artifacts and logs: `C:\Sandbox\Codex\Artifacts\CarbonLuau`.
- Four compiler jobs; Linux runtime container limited to four CPUs and 10 GiB. Server runs were sequential.
- SteamCMD's initial self-update required resuming setup. Linux Steam installation on a Windows bind mount was excessively slow; the live server uses Docker storage instead.
- RCON uses a persistent loopback connection with unique request IDs to avoid rapid reconnect throttling and distinguish replies from asynchronous log broadcasts.
- The fixture scripts use private, isolated test servers. Do not run them on production: they temporarily replace the probe with invalid fixtures.
- Both Rust servers shut down after testing. The task image, stopped Linux container/server installation, Windows installation, caches and build artifacts are retained for reuse.
- Windows' launcher returned exit code 1 after the requested `quit`; its log records Carbon shutdown, native unload and completed saving. Linux's full validation harness returned 0. The Windows server's shutdown exit code is recorded separately from the passing build, loader and live lifecycle tests.

## Tested deployment artifacts

The local ignored `dist` directory contains the tested package and native binaries, copied from the worker:

| Artifact | SHA-256 |
|---|---|
| `CarbonLuau.cszip` | `3AE904F9B8909C5064BBF81A5A32217BCD392A94CA3B3EE8BAB0237C2B3D06D2` |
| `native/win-x64/carbonluau_native.dll` | `ECCC43615D2EC23EE5F60D8ED2885D0C9A31CAFC3EDDED3AD9179FB2BDC7477F` |
| `native/linux-x64/libcarbonluau_native.so` | `EE8539015DFD162657005B855D5B0387FEF03AEEA4DB7E5E53CC392D54DCCFD3` |

CI artifacts are separately rebuilt and may have different binary/package hashes.

## Shockbyte runtime evidence

The tested Linux probe and plugin package were uploaded through the saved SFTP profile to `/1. Roost/carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so` and `/1. Roost/carbon/plugins/CarbonLuau.cszip`. Both were downloaded back and their SHA-256 hashes matched the tested artifacts above.

The user supplied Shockbyte console evidence dated September 14, 2026 (timestamps as displayed by the hosting panel):

- **04:39:47 AM:** native probe loaded successfully on `linux-x64`, returning `0x4C554155` from `/server/carbon/data/CarbonLuau/native/linux-x64/libcarbonluau_native.so`.
- **04:40:50 AM and 04:40:51 AM:** `c.reload CarbonLuau` logged successful native unload, plugin unload, plugin load and another successful probe. The plugin version was `0.0.1`.

The SFTP prefix `/1. Roost` maps to the server's runtime root `/server`; the reported absolute native path is consistent with the deployment layout. These observations prove the deployed native library can load, resolve and invoke the C ABI, unload and reload on this hosting environment. No hosting restriction blocked the tested operations.

The console excerpt contains repeated identical lines. They are not counted as additional reload cycles and do not by themselves prove duplicate plugin instances. Shockbyte evidence is user-provided console output, not an agent-run RCON or process-map inspection. Ten-cycle testing, controlled failure fixtures and process-level unload checks were performed on the Docker worker as recorded above; those checks and long-term handle-leak/soak testing are not claimed for Shockbyte.

## Next phase

Proceed to Phase 1: VM creation/destruction, source compilation/execution, logging, sandboxing, memory limits, execution interrupts, and runtime status/reload, within the existing design. No Phase 1 features are included in this documentation update.
