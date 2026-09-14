# Phase 2 — script generations

Package 0.2.0; native ABI 1.1 (backward-compatible additions); pinned Luau unchanged.
Architecture rules live in [Invariants](Invariants.md); actual qualification is in
[Phase2-Validation](Phase2-Validation.md). The user-approved task replaces the old
Phase 2 gameplay bridge with scripting infrastructure only. Phase 3 is not included.

## Deployment and configuration

`tools/package.ps1` produces four C# partial sources in `CarbonLuau.cszip` plus
`scripts/init.luau` and `scripts/modules/message.luau` alongside it. Deploy the zip
under `carbon/plugins`, the appropriate DLL/SO separately under
`carbon/data/CarbonLuau/native/<rid>`, and scripts under
`carbon/data/CarbonLuau/scripts`. Match managed/native versions; ABI 1.0 lacks the
new exports and cannot initialize a Phase 2 script generation.

Copy examples deliberately; runtime startup never overwrites operator scripts.
Missing files leave the runtime unavailable with a diagnostic. The module directory
must exist even if empty. No native payload, test fixture or configuration secret
belongs in the source zip.

```json
{
  "Enabled": true,
  "MaxVmMemoryMiB": 64,
  "MaxCallbackMilliseconds": 3,
  "ScriptRoot": "scripts",
  "EntryScript": "init.luau",
  "ModuleRoot": "modules",
  "FrameDrainBudgetMilliseconds": 5,
  "MaxQueuedCallbacks": 4096
}
```

Existing memory/callback names and 16..256 MiB / 1..100 ms clamps remain.
Frame budget clamps to 1..20 ms; capacity to 1..4096. Paths fail closed, not clamped.
Configuration changes require Carbon plugin reload; `carbonluau.reload` rereads
source files using the already-validated configuration.

## Source snapshots and paths

Before entering Luau, the managed host captures one independent candidate source
snapshot. EntryScript is relative to ScriptRoot; ModuleRoot is relative to
ScriptRoot; ScriptRoot is relative to CarbonLuau's data directory. Absolute paths,
drive prefixes, UNC, dots/traversal, backslashes, duplicate/trailing separators,
uppercase and non-ASCII names are rejected. Segments permit only `a-z`, `0-9`, `_`
and `-`; Windows device names are rejected. Only file paths have a `.luau` suffix.
Logical paths are at most 127 characters; entry paths are at most 121 including
extension, reserving six characters for the native `entry.` chunk prefix.

Exact ordinal segment matching prevents Windows case aliases. All actual path
components and configured ancestors are checked for reparse points. Module-tree
symlinks/junctions are rejected, including links on ignored files. No fallback
follows links. These checks assume the administrator does not concurrently replace
filesystem objects; defending against a malicious filesystem owner is out of scope.

Limits: 65,536 input bytes per file, 256 module files, 4 MiB total decoded UTF-8
source including entry, 1024 module-tree entries, and 1024 entries inspected per
configured-path segment. All source must be valid UTF-8; one leading BOM is
stripped, malformed encoding and NUL bytes are rejected. Other non-`.luau` regular
files are ignored. A malformed or oversized unused `.luau` file rejects the source
snapshot. An unused syntactically invalid module does not fail until required.

Bounded disk reads occur only during operator load/reload or the one automatic
reconstruction. `require` never reads files or invokes a managed callback. Changes
on disk do not affect active module sources until generation replacement. Snapshot
I/O and compilation are not hard-preemptible and are outside the callback/frame
execution budget. This is an explicit ingestion boundary, not filesystem access
for scripts.

## require

`require("foo")` resolves snapshot `ModuleRoot/foo.luau`;
`require("util/bar")` resolves `ModuleRoot/util/bar.luau`. There is no extension,
relative-directory lookup, search path, fallback or case normalization. Equivalent
spellings are rejected rather than assigned multiple cache keys.

Successful modules execute once per generation. The first return value (table,
function, primitive) is cached by reference; nil or no return maps to cached true.
Additional returns are ignored. Cached tables are intentionally shared mutable
script values, not host objects. Module-local globals remain private, while a
returned closure retains its defining module environment. Modules do not inherit
the requiring entrypoint's mutable globals.

Failures are not cached as success and may be retried within the same healthy
generation. Explicit Loading state catches cycles; dependency depth is capped at
32. Bounded errors name the logical module and error category, with traceback for
runtime failures. Native debug chunk names use virtual `modules/<name>.luau`
identities, independent of the configured physical ModuleRoot. A module compile
failure propagates through require as a script runtime failure containing the
`COMPILE_ERROR` category; entry compilation still returns COMPILE_ERROR directly.
Modules cannot yield. A timeout in nested require retires the whole VM.

## Task execution and ownership

`task.spawn` and `task.defer` are equivalent in Phase 2: enqueue now, never execute
inline, and run no earlier than a later Carbon scheduler drain. Both functions
return no handle/value. `task.delay(seconds, callback, ...)` accepts finite seconds
in 0..86400; negative values, NaN and infinities are errors. Delay uses monotonic
steady-clock nanoseconds and rounds upward, then waits for an eligible Carbon
drain. It does not promise exact wall-clock timing.

Only functions are valid callbacks. Up to 16 arguments may be nil, boolean,
number or string; strings are at most 4096 bytes each. Tables/functions/userdata
as arguments are rejected; callbacks may naturally close over script-owned values.
No general serialization or `task.wait` is provided. A callback that yields fails
locally and is released; no implicit coroutine resumption is scheduled.

Each native VM owns a reserved bounded min-heap ordered by due time, then uint64
enqueue sequence. The managed host owns the only drain policy. This differs from
placing a queue of native handles in C#: callback/thread references stay entirely
inside their owning native VM, eliminating a cross-generation callback-handle
transport. The four ABI additions install script support, register bounded module
source, read scheduler state, and consume one eligible callback. They retain opaque
VM tokens, fixed POD results and exception containment. `ClSchedulerInfo` is 56 bytes.
Sequence exhaustion rejects scheduling rather than wrapping.

The queue cap includes delayed and ready work. Overflow raises a script-visible
error and increments a counter. It is checked before registering another callback.
There are no per-callback OS threads or Carbon timers.

Managed draining captures native time and enqueue sequence once. Work created
during the drain cannot run until a later drain. A drain attempts at most 256
callbacks, and starts no further callback once its Stopwatch budget expires.
Every callback has the existing native execution deadline. One callback/GC step
can overrun the frame budget: deadlines are cooperative, not hard real-time.
The heap prevents newly enqueued immediate work from indefinitely starving already
due delayed work. Normal callback release uses incremental GC, not a full scan of
every remaining callback after each completion; memory failure forces collection.

## Carbon integration and logging

One `NextFrame` chain exists only while there is queued work. Idle generations have
no per-frame VM work. Future delayed work keeps this chain active until it is due;
there is no dedicated sleeping timer. Native and managed owner-thread checks still
guard entry. Each posted drain checks plugin stopping state, host identity and
generation identity; a mismatched generation schedules a fresh drain instead of
executing under the stale dispatch. Unload marks stopping before VM teardown.

The integration uses Carbon's documented [NextFrame facility](https://carbonmod.gg/devs/features/timers),
qualified on the live server builds in the evidence record. No player hooks or
background VM access are introduced.

Phase 1 fixed log/error buffers remain. Carbon emits at most eight result records
plus an output-suppression notice per drain. Callback errors are limited to five
records per 60-second monotonic window, with one suppression notice. Status retains
attempted/completed/failed/cancelled/invalidated/rejected, timeout/recovery and budget
overrun counters. Carbon log emission follows the measured execution drain and is
bounded by record count, not included in its Stopwatch execution budget.

## Transactions, recovery and teardown

A candidate gets a new VM, source snapshot, module cache and queue. It executes
only the configured entrypoint during initialization; queued initialization work
does not drain before commit. Entrypoints need not return 3 or any value. Failed
candidates and their staged output/queue are destroyed without touching a healthy
active generation. Success cancels old work and installs the candidate. IDs remain
monotonic even across rejected candidates. No state or callback migrates.

Scheduled runtime/memory errors release that callback and permit unrelated work
while the VM is healthy. Native timeout retains Phase 1 whole-VM retirement; a
retired VM is never resumed/reset. Retired scheduler stats preserve the actual
discarded queue count, including work enqueued during the failing callback.

Approved D9 policy: the host has one automatic recovery allowance, armed only by
a successful operator load/reload. Retirement cancels everything, consumes that
allowance and attempts a fresh snapshot/entrypoint reconstruction. A failed recovery
or another retirement leaves the runtime unavailable until operator intervention.
Successful automatic recovery and failed operator reload do not rearm it. The
failed callback is never resumed, though rerunning initialization can recreate it.
This bounded reconstruction intentionally supersedes smoke-only production recovery;
the independent Phase 1 harness retains its original smoke contract for regressions.

Normal destruction prevents further drains/scheduling, releases callback references,
module references and outstanding host thread references, then closes the VM and
unloads the native library last. Exceptional timeout skips operations on the
unwound state and closes the complete VM immediately; its entire registry dies
with it. Partial initialization and repeated disposal use the same ownership paths.

## Scope limits

The VM allocator cap is not a process/managed/compiler/native-container memory cap.
Source snapshot and queue cardinality have separate bounds. Shared modules are not
isolated tenants. Only tested Windows/Linux x64 builds are qualified; no universal
Rust/Carbon promise or multi-day soak is implied. Shockbyte Phase 2 is pending.

Players, Signals, game:GetService, permissions, gameplay commands, entities, UI,
networking, arbitrary file access, task.wait and Phase 3 remain unimplemented.
