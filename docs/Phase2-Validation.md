# Phase 2 validation — 2026-09-14

**Phase 2 verdict: PASS for the tested Windows/Linux x64 environments.**
Worker qualification and final-source GitHub CI passed. No Phase 3 functionality
is included.

## Baseline, approval and source identity

Started from `30bde19b7d3f9112becbe8e979502969ae18dc69` on main.
Implementation published as `6fc97d1c4f4d779b7fbce0bf32be3f2216cbd651` on main.
Historical [Phase 0](Phase0-Validation.md) and [Phase 1](Phase1-Validation.md) evidence is unchanged.
Luau remains pinned to `c6b830185af962c82003f86784e2fe036357c830`; vendor files were not changed.

The initial inspection paused at the task's recovery-policy stop condition:
Phase 1's smoke-only recovery would not restore persistent scripts, while unlimited
entrypoint replay could recreate a runaway callback. The user approved D9's
one-attempt reconstruction policy on 2026-09-14 before runtime implementation.
The accepted rule is in [Invariants](Invariants.md#decision-register), and exact
implemented semantics/limits are in [Phase2.md](Phase2.md).

Local and worker runtime/example sources match aggregate SHA-256:
`950563537b36859dc2d8f00e7c488d5211721735e50498a875576444b899d9aa`.

Algorithm: UTF-8 text with CRLF normalized to LF; hash each file, then hash rows
`relative-path lowercase-sha256` joined with LF and no trailing newline. Ordered
files: native/CMakeLists.txt, native/include/carbonluau_native.h,
native/src/probe.cpp, native/src/Runtime.cpp, native/src/Scripts.inl,
native/third_party/LUAU_REVISION.txt,
src/CarbonLuau/CarbonLuau.Main.cs, src/CarbonLuau/CarbonLuau.Native.cs,
src/CarbonLuau/CarbonLuau.Runtime.cs, src/CarbonLuau/CarbonLuau.Scripts.cs,
examples/scripts/init.luau, examples/scripts/modules/message.luau.
This identifies the tested implementation independently of later evidence-only edits.

## Environments

All substantive builds, stress tests and servers ran on `dockerbox`. No build or
server workload ran on the controlling PC.

| Component | Windows | Linux |
|---|---|---|
| OS | Windows 11 Pro 10.0.26200 | Ubuntu 24.04.4 LTS container |
| Compiler | MSVC 19.44.35228 / VS2022 | GCC 13.3.0 |
| Managed test tooling | .NET SDK 9.0.317, net48 assemblies | Same assemblies on Mono 6.8.0.105 |
| Carbon | 2.0.259.0 | 2.0.259.0 |
| Rust | 2633, Steam build 25230300 | 2633, retained installed build 25230300 |
| CarbonLuau | Package 0.2.0, native ABI 1.1 | Package 0.2.0, native ABI 1.1 |

Windows private static CRT/import guard remains intact. Builds used at most four
compiler jobs, persistent incremental/vendor caches and task-owned paths. Live
servers ran sequentially, with loopback RCON and zero players.

## Unit, interop and stress results

| Validation | Windows x64 | Linux x64 |
|---|---|---|
| Native build and CTest | PASS 4/4 | PASS 4/4 |
| Original native load/unload | PASS, 100 cycles | PASS, 100 cycles |
| Original managed loader/failure matrix | PASS, 100 cycles | PASS, 100 cycles |
| Phase 1 runtime/lifecycle | PASS, 1000 native lifecycles, 100 interleaved failure sequences | PASS, same counts |
| Phase 1 managed transactions | PASS, 100 replacements + 200 rejected candidates | PASS, same counts |
| Modules: cache, values, private environments, errors, cycles, retry | PASS | PASS |
| Source UTF-8/BOM/NUL/size/path tests | PASS | PASS |
| Filesystem link escape fixture | Junction rejected | Symlink rejected |
| Scheduler ordering, primitive arguments, not-before delays, recursion boundary | PASS | PASS |
| Callback runtime/memory errors, yield rejection, timeout and module-timeout retirement | PASS | PASS |
| Queue saturation | 10000 attempts, 4096 accepted, 5904 rejected | Same |
| Varied delayed work at scale | 4096 queued and drained | Same |
| Phase 2 native lifecycle | 1000 generations: 500 drain ten tasks, 500 cancel ten tasks | Same |
| Managed queue/reload stress | 100 cycles: queue 1000, reject candidate without disturbing queue, replace and cancel old work | Same |
| Managed frame budget | PASS; successful callback overrun stops further draining, no timeout substitution | PASS |
| Approved recovery allowance and unload with queued work | PASS | PASS |
| Allocation faults | 256 VM-init + 64 source-load + 768 Phase 2 fault positions | Same |
| ASan + UBSan + leak detection | Not run | PASS 4/4, no suppressions |

The Phase 2 allocator fixture covers installation, module execution/cache ownership,
callback registration, callback execution/re-enqueue, and partial destruction.
Every tested teardown returns the allocator's tracked live-byte count to zero.
Phase 2 saturation uses ten 1000-attempt resumes without draining between them;
this respects the production 100ms maximum even under sanitizer instrumentation.
Native callback references never cross the ABI, and retired queues are checked as
discarded rather than migrated.

Final native CTest totals were 0.87s Windows, 1.14s Linux Release and 5.91s Linux
sanitizers. Managed compilation completed with zero warnings/errors.
No sanitizer findings were suppressed or dismissed.

## Basic overhead and memory samples

These are observations, not performance gates or broad hardware promises.
Compile/setup overhead is included where stated.

| Measurement | Windows | Linux |
|---|---|---|
| 1000 cached requires, including chunk compile/setup | 0.118 ms | 0.101 ms |
| 10000 enqueue attempts, including ten chunk setups | 21.347 ms | 17.769 ms |
| Native drain of 4096 trivial callbacks | 7.628 ms | 4.947 ms |
| 100 managed reload/cancel cycles, including file edits and candidate loads | 488.40 ms | 185.77 ms |
| VM bytes after each of 100 successful replacement samples | 252528..252528 | 252528..252528 |

The native bulk-drain benchmark intentionally drives calls directly; production
managed draining uses the per-frame budget and 256-attempt ceiling. The combined
reload measurement is an upper-bound characterization of creation/loading, not a
standalone VM-create latency. These results do not establish multi-day leak freedom.

## Live Carbon qualification

Both platforms used the production four-source package, not a test-only command
package. Harnesses modify only the isolated worker's script files and exercise real
admin commands and real Carbon NextFrame draining. Production initialization
observed:

```text
[gen=1][bootstrap] CarbonLuau Phase 2 module loading works
[gen=1][scheduler] deferred callback
[gen=1][scheduler] delayed callback
CarbonLuau: ready
Native ABI: 1.1 OK
VM bytes: 252528 / 67108864
Callback deadline: 3 ms; frame budget: 5 ms
```

On each platform the completed current-run sequence demonstrated:

- healthy entry/module execution and normal source reload;
- entry compile/runtime failure and module compile/runtime failure, preserving the
  healthy generation and its delayed callback (`old queue preserved`);
- rejected candidate callbacks never execute;
- successful replacement cancels an old delayed callback;
- a scheduled runtime error logs its context and an unrelated callback prints
  `callback error survived`;
- a scheduled infinite loop times out; entrypoint reconstruction prints
  `recovery entry ran` exactly twice total (initial load plus one recovery);
- the reconstructed runaway callback times out again, leaving generation 0,
  unavailable, Timeouts=2, Recoveries=1, recovery allowance false;
- a successful operator reload restores execution/rearms recovery;
- a broken on-disk entrypoint during the next timeout causes recovery failure and
  leaves the runtime unavailable until fixed/operator-reloaded;
- ten repeated runtime cancellation cycles, each cancelling 100 delayed callbacks;
- ten full Carbon plugin unload/load cycles with queued work and native DLL/SO
  absent from process modules/maps after every unload;
- no stale runtime, plugin or retired-generation callback markers appeared.

A correlated Rust `status` response verified responsiveness throughout.
Both fixture runners returned **0**. Final logs:

- Worker `C:\Sandbox\Codex\Artifacts\CarbonLuau\phase2-server-win-2.log`.
- Worker/container `/artifacts/phase2-server-linux-20260914-103925.log`.

Windows' server launcher returned **1** after quit, with native unload, completed
saving and `Config Saved`. Linux's official wrapper returned **137** after quit,
with native unload, completed saving, `Config Saved` and
`[Raknet] Server Shutting Down (quit)`. No forced-termination fallback ran.
Linux cgroup oom and oom_kill counters were both zero. These launcher codes are
not represented as exit-zero shutdowns.

An earlier Windows run also passed, but the reported cycle counts use only the
final-source run after cleanup/cancellation accounting was tightened.

## Issues found and corrected

- The first sanitizer saturation test exceeded the unchanged 100ms execution
  maximum under instrumentation. The fixture now uses ten bounded batches; no
  production limit was increased.
- The original managed frame-budget fixture could pass via timeout recovery.
  It now explicitly requires a successful callback, no timeout, preserved queued
  work and a measured budget overrun.
- Review caught cancellation-counter undercounting if a failing callback enqueued
  additional work before retirement. Native retired-state statistics now retain
  the complete discarded queue count, with a module-timeout regression.
- Normal VM disposal explicitly releases queued/module/host-thread references
  before closing the state. Timeout disposal still immediately closes the unwound
  VM without attempting operations on suspect state.
- No Phase 2-attributable Carbon crash or live integration failure occurred.

## Packaging, review and publication

Production package guard passes: exactly four required C# partial sources; no
native payload, live fixtures, build trees, logs or secrets in the zip. Examples
are delivered alongside it. Runtime DLL/SO files are separate as in Phase 0/1.

Final review covered generation ownership, native registry lifetime, source
confinement, queue/input bounds, timeout invalidation and teardown ordering.
Diff/relative documentation-link checks pass. No vendor, proprietary server files,
secrets or historical Phase 0/1 evidence were modified. Windows profile keyring
authentication confirmed `gmoddev` and the repository is public with default main.

Worker artifacts are retained at
`C:\Sandbox\Codex\Artifacts\CarbonLuau\phase2-release`; matching files were copied
to ignored local `dist/phase2`, preserving the previous Phase 1 artifacts.

| Artifact | SHA-256 |
|---|---|
| CarbonLuau.cszip | `39AC11D2715FEF78D0B11EA9FA73A90B3105C5A55F0418F0ACA390E331DAA279` |
| native/win-x64/carbonluau_native.dll | `BBF8924FA86C96BEC45A75925BD20898C76FFE53EE3F14B7BAEFD5B76DAA932D` |
| native/linux-x64/libcarbonluau_native.so | `9D1D1C0F12720B63781CC876A8D2D36F95FD40BE1D56DD08490CBA8FF2FBD3F4` |

CI preserves existing regressions and runs Phase 2 native/managed tests on Windows
and Ubuntu plus the instrumented Linux suite. CI is not the source of live Carbon
evidence. Independent implementation CI:
[run 34834974054](https://github.com/gmoddev/CarbonLuau/actions/runs/34834974054)
at `6fc97d1c4f4d779b7fbce0bf32be3f2216cbd651`: **PASS**, all three jobs.
Windows completed in 2m3s, Ubuntu in 1m31s and Linux sanitizers in 48s.
The subsequent evidence-only documentation commit changes no tested runtime,
fixture, package or artifact and intentionally skips a redundant CI build.

## Cleanup, limitations and deferred work

Both Rust servers exited. Container `codex-carbonluau-linux` is stopped; installed
servers, image and incremental/sanitizer caches remain. Unrelated `drycreek-bot`
and `directus-db` remained running. No global Docker, host runtime or machine
configuration changes were made.

Deliberate implementation choices: spawn/defer are equivalent later-drain enqueue;
the native VM retains the callback heap while managed code drives draining; module
sources are bounded snapshots rather than on-demand filesystem reads; ambiguous
paths and all reparse points are rejected; nil/no module return becomes true.
D9 recovery supersedes smoke-only production recovery with explicit user approval.

Known limits include cooperative deadlines, non-preemptible bounded snapshot I/O
and compilation, VM-only rather than process-wide allocation caps, bounded log
emission outside the execution-drain stopwatch, shared mutable module results,
and per-frame checks while future delayed work exists. See the full
[execution contract](Phase2.md) rather than interpreting PASS as universal safety.

Shockbyte Phase 2 was **not deployed or validated**. Its earlier native-probe
evidence does not qualify the new runtime. Players, Signals, game:GetService,
permissions/gameplay bindings, arbitrary event bridges, task.wait and long-term
soak testing remain deferred. **Phase 3 was not started.**
