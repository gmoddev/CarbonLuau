# Phase 1 execution-core contract

Scope: CarbonLuau package `0.1.0`, project native ABI `1.0`, and pinned Luau
`c6b830185af962c82003f86784e2fe036357c830`. This is an execution core, not the complete v0.1 scripting API.
Architecture rules remain in [Invariants](Invariants.md); evidence belongs in
[Phase1-Validation](Phase1-Validation.md). Historical [Phase 0 evidence](Phase0-Validation.md) is unchanged.

## Build and deployment

Use the existing [worker workflow](Phase0.md). CMake links only `Luau.Compiler`,
`Luau.VM` and their required static dependencies. Vendor CLI, tests, web, analysis,
require and CodeGen targets are excluded from the default build. No JIT is linked or enabled.
The pinned vendor tree is unmodified; no dependency fetch occurs during CMake configuration.

Windows uses a private static MSVC CRT for the bridge and its Luau dependencies.
Rust can preload an older app-local MSVCP140; a current dynamic-CRT build is not
safe merely because it passes standalone tests. `Test-WindowsImports.ps1` rejects
dynamic MSVC CRT imports. No CRT allocations, C++ objects or exceptions cross the
project ABI, so ownership never transfers between CRT heaps.

On `dockerbox`, use four compiler jobs and existing incremental build directories:

```powershell
cmake -S native -B C:/Sandbox/Codex/Builds/CarbonLuau/win-x64 -G "Visual Studio 17 2022" -A x64
cmake --build C:/Sandbox/Codex/Builds/CarbonLuau/win-x64 --config Release --parallel 4
ctest --test-dir C:/Sandbox/Codex/Builds/CarbonLuau/win-x64 -C Release --output-on-failure
dotnet build tests/managed/LoaderTests.csproj -c Release -o C:/Sandbox/Codex/Artifacts/CarbonLuau/managed
dotnet build tests/runtime/RuntimeTests.csproj -c Release -o C:/Sandbox/Codex/Artifacts/CarbonLuau/runtime
```

Run both resulting managed executables: `LoaderTests` takes good/WrongProbe/MissingSymbol
native paths; `RuntimeTests` takes good/WrongAbi/LegacyProbe paths. Use `mono` on Linux.
These are real interop tests, not Carbon mocks.

Inside the task's Linux container:

```sh
cmake -S /src/native -B /work/linux-x64 -DCMAKE_BUILD_TYPE=Release
cmake --build /work/linux-x64 --parallel 4
ctest --test-dir /work/linux-x64 --output-on-failure
cmake -S /src/native -B /work/linux-asan -DCMAKE_BUILD_TYPE=Debug -DCARBONLUAU_SANITIZE=ON
cmake --build /work/linux-asan --parallel 4
ASAN_OPTIONS=detect_leaks=1:halt_on_error=1 UBSAN_OPTIONS=halt_on_error=1:print_stacktrace=1 ctest --test-dir /work/linux-asan --output-on-failure
```

`tools/package.ps1` packages exactly three production C# partial sources. Deploy
`CarbonLuau.cszip` to `carbon/plugins`, and the RID-specific DLL/SO separately to
`carbon/data/CarbonLuau/native/<rid>/`. Stop/unload the plugin before replacing a
native library. Match the managed and native ABI. Never put DLL/SO files in the zip.

## Native boundary and ownership

[The public header](../native/include/carbonluau_native.h) is the ABI definition.
The probe remains unchanged. `carbonluau_abi_version()` returns major in the high
16 bits, minor in the low 16 bits; managed code rejects a different major before
binding runtime operations. Minor changes must preserve existing layouts/signatures.
`cl_luau_revision` copies the compiled-in pin into a caller-owned buffer.

VM/thread handles are opaque nonzero 64-bit tokens, not pointers. A bounded registry
holds at most 32 live VMs, with at most one outstanding host execution thread per VM.
Tokens never repeat during a library lifetime. Null, invalid, stale and wrong-owner-thread
tokens are rejected without dereferencing caller-provided addresses. Destroying zero
is a no-op; destroying an already-destroyed nonzero token reports INVALID_ARGUMENT.
As with any C ABI, non-null input/output pointers must designate valid storage of
the stated size; this is not an arbitrary native-pointer validation facility.

All state access is serialized and owner-thread-affine. Carbon lifecycle/commands
use the server thread, with managed and native checks. No parallel worker execution
or script-to-managed callback is installed. The registry mutex guards native handle
validation as well as VM entry; it is not a promise of concurrent script execution.

Load accepts UTF-8 source up to 65,536 bytes and an explicit ASCII chunk identity
of 1..127 letters/digits/underscore/dash/dot, not a filesystem path. Compiler options
are fixed at optimization 1/debug 1. Only compiler-produced bytecode is loaded;
compiled output above 1 MiB is rejected. No external bytecode, files or modules are accepted.

`cl_vm_load_source` returns an owned execution-thread token. Each resume requires
a fresh relative budget in nanoseconds, converted internally into a `steady_clock`
deadline. Native yields return YIELDED and can be resumed with zero arguments.
The managed Phase 1 wrapper performs one resume and releases the thread even on
yield; it does not schedule continuation. Results transport only the first numeric
value (when present), bounded error text and bounded print output.

`ClResult` is 6,160 bytes: double, two uint32 values, 2,048 error bytes and 4,096 log
bytes. Text is NUL-terminated UTF-8, truncatable, copied immediately into managed
strings. The caller owns the storage; no borrowed error pointer escapes. Statuses
are OK, YIELDED, RUNTIME_ERROR, COMPILE_ERROR, MEMORY_LIMIT, TIMEOUT,
INVALID_ARGUMENT and INTERNAL_ERROR. Flags identify retired state and truncated logs.

## Sandbox surface

Initialization opens base into a temporary global table, then copies an explicit
allowlist into the actual shared global environment:

`assert`, `error`, `getmetatable`, `next`, `ipairs`, `pairs`, `pcall`, `xpcall`,
`rawequal`, `rawget`, `rawset`, `rawlen`, `select`, `setmetatable`, `tonumber`,
`tostring`, `type`, `typeof`, `_VERSION`, plus `_G` referring to the shared table.

Only math, string, table, coroutine, bit32, utf8, buffer and vector libraries are
opened there. `print` is the sole host primitive. OS/debug/io/package libraries,
getfenv/setfenv, newproxy, gcinfo, collectgarbage, load/loadstring/loadfile/dofile,
require, networking, reflection and Carbon/Rust/gameplay objects are absent.
Compiled-in upstream library code is not the script-exposed library surface.

`luaL_sandbox` freezes shared globals, library tables and builtin metatables.
Every loaded chunk gets a fresh `luaL_sandboxthread` environment. Script-local
global writes are allowed but do not survive into the next chunk. No module cache,
callbacks or shared script environments exist in Phase 1. Bootstrap runs in this
same restricted environment, not a privileged script environment.

Print handles zero arguments and nil/boolean/number/string values. Other values
are rendered as type labels without addresses or metamethod calls. Each call
examines at most 32 arguments and 256 bytes per string; output is capped at 4,095
bytes per resume, with a truncation flag. String control bytes become dots. Native
code buffers output; Carbon receives at most one bounded print log call per
execution, tagged with original generation/chunk. Logging is not a callback API.

## Budgets, containment and recovery

Production configuration is intentionally limited to:

```json
{
  "Enabled": true,
  "MaxVmMemoryMiB": 64,
  "MaxCallbackMilliseconds": 3
}
```

Numeric values clamp to memory 16..256 MiB and callback 1..100 ms, with a warning.
Missing fields use defaults. Null/malformed configuration leaves the plugin
unavailable with a diagnostic instead of overwriting the operator's file.
Changing configuration takes a Carbon plugin reload; `carbonluau.reload` uses
the already-validated settings and replaces only the runtime generation.

Every VM uses an overflow-safe capped realloc allocator from its first allocation.
Accounting changes only after successful realloc, including shrink; frees release
the original size, and a fresh allocation ignores Luau's old-size type tag.
A caught allocation refusal still produces MEMORY_LIMIT. Runtime-memory failures
invalidate the execution thread; releasing it and collecting its environment permits
later valid execution in the same healthy VM. Initialization/load failures retire
partially initialized state. System/compiler allocation failures outside the VM
allocator are INTERNAL_ERROR, not mislabeled VM-cap exhaustion.

Luau uses its default C++ exception mode, not LUAU_EXTERN_C/longjmp. Fallible setup
runs inside `lua_cpcall`, bytecode load uses Luau's protected loader plus outer native
containment, and execution uses `lua_resume`. Every project entry point contains
exceptions. Error text is copied while its Lua state remains alive; runtime errors
include bounded `lua_debugtrace` context where available.

Deadline cancellation is a private non-`std::exception` marker, thrown only at
non-GC interrupt safepoints and caught by the outer project ABI. This bypasses
Luau's script-catchable protected errors, including nested pcall/xpcall, coroutine,
metamethod and table-sort callbacks. The interrupted global VM is immediately
closed in full; it is **never resumed, reset or reused** after that unwind. Its
thread tokens become invalid and memory usage becomes zero. The VM token remains
queryable/destroyable with Ready=0. This is tied to the inspected pinned C++ VM;
upgrading Luau requires requalifying unwind/close behavior and all escape fixtures.

The managed host returns/logs TIMEOUT unchanged, retires that generation, and
attempts a new sandboxed bootstrap generation. It never retries the failed source.
If replacement fails, it reports unavailable; it does not continue in suspect state.

The deadline is cooperative, **not hard real-time**. It excludes compilation and
cannot preempt native/C# or standard-library work until control reaches a VM
safepoint. Source/output/log/handle bounds limit separate ingestion/bridge work,
but the VM heap cap does not cap compiler AST/STL memory, managed memory, native
bridge buffers, allocator metadata or process RSS. No claim of adversarial-process
isolation or immunity to upstream native defects is made.

## Lifecycle and operator commands

Loaded validates config, explicitly loads/probes the native library and binds the
versioned ABI. OnServerInitialized creates a candidate generation and runs
`print('hello from Luau'); assert(1 + 2 == 3); return 3` before reporting ready.

`carbonluau.status` reports readiness/reason, generation, ABI, Luau pin, current
VM bytes/cap and deadline. It does not return paths, secrets or native addresses.
`carbonluau.reload` creates/sandboxes/smokes a separate candidate. Only successful
completion returning 3 commits it; failed candidates and their staged print output
are discarded. The old healthy generation remains unchanged. After commit, the old
VM is destroyed exactly once. No gameplay side effects exist to roll back.

Both commands use Carbon's documented `ConsoleCommand` with `AuthLevel(2)` for
owner/admin access, including server console/RCON. [Carbon command model](https://carbonmod.gg/devs/features/commands).
No Luau-defined commands or permission API are installed.

Unload stops access by invalidating the managed generation, releases any execution
thread, closes its VM and unloads the native library last. Partial setup and repeat
dispose are supported. No finalizer enters a VM on the GC thread. Unexpected teardown
failure retains the library and reports the failure rather than unloading callable code.
Idle runtime has no tick hook, timer, watcher or per-frame work.

## Qualification fixtures

Native CTest preserves the Phase 0 load/probe test and adds execution/sandbox/failure
and white-box allocator fixtures. Fault controls compile only into the standalone
test executable, never into the shipped native library. Managed tests use the actual
native DLL/SO and test ownership, configuration, ABI mismatch and transactional reload.

For isolated live testing only, `package.ps1 -IncludePhase1Fixtures` adds
`tests/live/CarbonLuau.Fixtures.cs`. Its admin-only `carbonluau.fixture` accepts only
fixed timeout, memory, valid and failed-reload cases. It cannot evaluate operator
source. This fourth partial source is excluded from default production packaging.

`Test-Phase1Windows.ps1` consumes the current run's unique log path and uses the
existing persistent RCON helper. `Test-Phase1Linux.py` creates a unique log on every
run and launches the official Carbon wrapper. Both exercise containment/recovery,
ten runtime replacements and ten complete plugin unload/load cycles, inspect actual
library unmapping, then switch to and qualify the production package before quit.
These fixtures belong only on the authorized isolated worker servers, not production.

No Players, Signals, game:GetService, scheduler, modules/require, script discovery,
watchers, dynamic commands, network/gameplay bridge or other Phase 2 feature is implemented.
