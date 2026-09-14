# Phase 1 validation — 2026-09-14

**Worker qualification: PASS.** Final-source CI is recorded after the validated
implementation is pushed. No Phase 2 functionality is included. This record is
separate from the unchanged historical [Phase 0 evidence](Phase0-Validation.md).

## Baseline and source identity

Started from clean `7509aa6`, the policy-only successor to proven `a88f2eb`.
The unchanged native and actual managed Phase 0 tests passed on Windows and Linux
before implementation. An initial command typo named a nonexistent wrong-probe
fixture; correcting the path required no source change.

Pinned Luau: `c6b830185af962c82003f86784e2fe036357c830`. The vendored files and pin
were not changed. Phase 1 links Compiler/VM only, with code generation disabled.
Implementation details and limitations: [Phase1.md](Phase1.md).

The local checkout and worker source matched this runtime-source SHA-256:
`f12b4e1ea067ddf84a66e95cfd6d86d66c5394fa871b94f71a7267a283998955`.
It hashes UTF-8/LF-normalized contents of, in order: native/CMakeLists.txt,
native/include/carbonluau_native.h, native/src/probe.cpp, native/src/Runtime.cpp,
native/third_party/LUAU_REVISION.txt, and src/CarbonLuau/{CarbonLuau.Main.cs,
CarbonLuau.Native.cs,CarbonLuau.Runtime.cs}. Each manifest row is
`relative-path lowercase-content-sha256`; the aggregate hashes those rows joined
with LF, without a trailing newline. This identifies the tested uncommitted source
independently of the later evidence/documentation commit.

All builds and Rust/Carbon runs used `dockerbox`; no compilation/server workload
ran on the controlling PC. Worker: Windows MSVC 19.44.35228 / VS2022, .NET SDK
9.0.317 tools with net48 test assemblies; Ubuntu 24.04 Docker / GCC13.3 / Mono6.8.
Four compiler jobs, persistent incremental directories, and sequential live servers.

## Unit and interop results

| Validation | Windows x64 | Linux x64 |
|---|---|---|
| Pinned Luau native build | PASS, private static CRT | PASS |
| Original native loader fixture | PASS, 100 load/call/unload cycles | PASS, 100 cycles |
| Original managed loader/failure fixture | PASS, 100 cycles | PASS, 100 cycles |
| Runtime execution/sandbox/logging/result/handle tests | PASS | PASS |
| Syntax/runtime errors and traceback | PASS | PASS |
| Memory limit and valid execution afterward | PASS | PASS |
| Timeout, nested catches/coroutines/non-yieldable callbacks | PASS | PASS |
| Repeated VM lifecycle | PASS, 1,000 cycles; 100 interleaved error/memory/timeout sequences | PASS, same counts |
| Allocator fault injection | PASS, 256 initialization and 64 load fault positions | PASS, same counts |
| Managed transactional generation lifecycle | PASS, 100 replacements + 200 rejected candidates | PASS, same counts |
| ABI mismatch/legacy probe, config clamps, partial teardown, affinity | PASS | PASS |
| ASan/UBSan + leak detection + Debug assertions | Not run | PASS, all three CTest fixtures |
| Actual Carbon + failure recovery + production package | PASS | PASS |
| Final live runtime replacements / plugin cycles | 10 / 10, plus one production runtime reload | 10 / 10, plus one production runtime reload |

Runtime tests check actual prohibited globals are nil, required libraries work,
shared tables/metatables reject writes, and per-chunk globals do not leak into the
next environment. Print primitive formatting, hostile tostring values and truncation
are exercised. Invalid/stale/null tokens and wrong-thread calls are rejected.

Fault injection covers failed realloc shrink preserving accounting, successful
shrink/growth/free, exact-cap and integer-overflow refusal, partial VM initialization
and load failure. It reports zero retained allocator bytes after teardown. Fault
controls exist only in the white-box test executable. Of the 256 initialization
fault positions, 22 exercised failed/partial startup; all returned MEMORY_LIMIT
and zero handles, with zero retained allocator bytes. ASan/UBSan use no suppressions.
Repeated-cycle and sanitizer evidence is not a multi-day leak-free guarantee.

Timeout recovery deliberately replaces the entire retired global VM. Native tests
prove destruction and fresh-state recovery; managed tests prove a new generation
can execute afterward. Runtime memory failures recover after releasing the failed
thread in the same VM. No failed source is automatically retried.

## Issues found and corrected during qualification

- **Native error classification:** pinned `luau_load` returns 1 for OOM rather
  than LUA_ERRMEM. Fault injection caught the incorrect INTERNAL_ERROR mapping.
  The bridge now consults its allocator failure flag; all fault positions pass.
- **Carbon test integration:** current `ConsoleSystem.Arg.Args` entries are
  `Facepunch.StringView`. The test-only fixed fixture now explicitly converts
  its argument to string. The production package has no fixture command.
- **Windows native dependency/host integration:** first live VM creation crashed
  in the Rust-loaded MSVCP140 `14.16.27012.6` with a bridge built by MSVC14.44.
  Stack: MSVCP140 → cl_vm_create → managed Create. The bridge and Luau now link
  a private static CRT. Verified DLL imports contain only KERNEL32.dll, and a CI
  import guard prevents reintroducing dynamic MSVC CRT dependencies. Rust/Carbon
  files and machine-wide runtimes were not changed. Microsoft documents that
  runtime versions must meet the toolset's requirements: [MSVC redistribution](https://github.com/MicrosoftDocs/cpp-docs/blob/main/docs/windows/latest-supported-vc-redist.md).
- **Live harness:** Carbon logs emitted during a command can share the command's
  RCON identifier. The runner now waits for the terminal expected reply rather
  than mistaking an intermediate timeout diagnostic for fixture failure.
- **Production handoff harness:** immediately requesting compilation while a
  changed zip triggered Windows Carbon's watcher raced command registration.
  Linux did not automatically load the replaced, explicitly unloaded package.
  The fixture now lets notifications settle, then issues one `c.load`. A reload
  request against the unloaded Linux plugin returned no correlated reply. These
  were harness/host-integration issues; the servers remained responsive.

## Live Carbon and CI

Windows and Linux run Carbon `2.0.259.0` on Rust `2633` (installed Steam build
`25230300`), with loopback-only test RCON, zero players and a one-player limit.
Both use the same production source, plus the separately packaged fixed fixture
partial for failure tests. No Carbon/Rust assembly or server log dump is committed.

Observed on both platforms:

```text
[gen=1][bootstrap] hello from Luau
CarbonLuau: ready
Generation: 1
Native ABI: 1.0 OK
Luau: c6b830185af962c82003f86784e2fe036357c830
VM memory: 0.225 / 64 MiB
Callback deadline: 3 ms
PASS timeout; TIMEOUT; generation=2
PASS valid; OK; generation=2
PASS memory; MEMORY_LIMIT; generation=2
PASS valid; OK; generation=2
PASS failed-reload; COMPILE_ERROR; generation=2
```

Each failure was followed by a successful correlated Rust `status` response.
`post-failure execution OK` appeared through Carbon logging. Failed replacement
preserved generation 2; ten operator reloads advanced generations 3..12, each ready
at approximately 0.225 MiB. Ten full plugin unload/load cycles then passed, with
the native library absent from Windows process modules/Linux `/proc` maps after
every unload. The production three-source package also initialized and reloaded
successfully, with no fixture source included.

The completed Linux runner returned 0; its official server wrapper returned 137
after `quit`. Logs record Carbon/native unload, completed saves, `Config Saved`
and `[Raknet] Server Shutting Down (quit)`. No forced-termination fallback ran;
cgroup memory.events reported oom=0 and oom_kill=0. Windows records the same
Carbon/native teardown and completed saving; its launcher returns 1 after quit,
as in Phase 0. These nonzero server-launcher exit codes are explicitly distinguished
from fixture failures and are not described as exit-zero shutdowns.

Final Linux log: worker `Artifacts/CarbonLuau/phase1-server-linux-20260914-094715.log`.
Final Windows log: worker `Artifacts/CarbonLuau/phase1-server-win-4.log`.
Both complete fixture runners returned 0. Earlier failed-attempt logs remain on the
worker for attribution; their repeated cycles are not added to the final-run counts.
The expected 1→2 timeout recovery is additional to the ten operator replacements.

CI preserves original Phase 0 coverage and adds runtime interop, allocation faults,
static-CRT import checking, package-content verification and Linux ASan/UBSan.
The new GitHub run is started only after worker qualification passes.

Shockbyte Phase 1 has not been deployed or qualified; its Phase 0 result does not
prove the new execution runtime there.

## Production artifacts

Tested worker artifacts are retained under
`C:\Sandbox\Codex\Artifacts\CarbonLuau\phase1-release` and copied back into the
ignored local `dist` directory. Copy-back SHA-256 values match:

| Artifact | SHA-256 |
|---|---|
| CarbonLuau.cszip | `2458C60819E05258E28D0DED871936182AF8D7153CDD215C6295FB75AF054E6C` |
| native/win-x64/carbonluau_native.dll | `94838F4298DDF3CE314A6165CC564FC2769372835B6F809826B9E4B419A2062C` |
| native/linux-x64/libcarbonluau_native.so | `235E0DDDA4F3DEA0A966CB8A8C5CE0AB1B3134970611BDAC0FB25DAF017EE4CC` |

CI rebuilds separately and need not produce byte-identical binaries/zips.

## Known limitations and deferred work

The [execution contract](Phase1.md#budgets-containment-and-recovery) documents
cooperative deadlines, VM-versus-process/compiler memory limits, and whole-VM
retirement on timeout. Only the tested x64 host/runtime combinations are qualified.
No broad OS/host-version guarantee or long-term soak result is claimed.

Phase 2 is not implemented. Players, Signals, game:GetService, task scheduling,
modules/require, gameplay commands, event bridges and long-term soak qualification
remain deferred to new work.
