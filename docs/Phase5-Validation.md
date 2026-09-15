# Phase 5 qualification — 2026-09-15

Execution verdict: **PARTIAL**. The committed Phase 5 Linux qualification matrix
completed, including the sustained soak and Carbon profiler. Final-source CI is
green on Windows and Ubuntu, but an actual Windows Rust/Carbon server, Shockbyte
deployment and authenticated real-client session were unavailable. This is an
execution verdict and deliberately not an independent v0.1 release-readiness
decision.

## Revision and identities

| Field | Qualified value |
|---|---|
| Starting branch / baseline | `main` / `43f402d390de0a63eceb308b385a0e51a539789c`; clean and equal to `origin/main` before work |
| Full live-matrix commit | `0ae67e0483137bf496096e58a5269474e8404564` |
| Final tested source / profiler supplement | `5f272b98f1aed9d4bc0e3d4fa19e7eb7b1718946`; only test fixture/runner changed after the full matrix |
| Production corrective commits | None; qualification found no reproduced CarbonLuau production-source defect requiring a correction |
| Package / scripting API / native ABI | `0.3.0` / `CarbonLuau 0.3.0-experimental` / `1.2` |
| Luau | `c6b830185af962c82003f86784e2fe036357c830` |
| Rust | Protocol `2633.288.1`; build date 2026-09-10 11:23:47; Steam build `25230300` |
| Carbon | `2.0.259.0 [2026.09.03.0] 21063e8` on Linux |
| Linux worker | BigVPS isolated Docker; Ubuntu 24.04.5 LTS, x86_64; GCC 13.3.0; .NET SDK 9.0.318; Mono 6.8.0.105 |
| Final Linux native SHA-256 | `de15abe4129db3da05179995c29a1e80e0600685ddcc406d9e6b4874adc63aaf` |
| Production package SHA-256 | `af35d5365613d61b639b5b0ecc3843fd82314e2a7198c71debdade2c31e0f601` |

The user's task-specific direction replaced unavailable `dockerbox` with BigVPS
for Rust-server testing. Task files stayed under `/srv/codex/CarbonLuauPhase5`.
The live container used loopback ports 28215/28216/28217, three CPUs, 9 GiB RAM
and 10 GiB memory-plus-swap. Existing Pelican/Wings/Docker workloads were neither
stopped nor modified. BigVPS is Linux and therefore cannot provide Windows live
evidence.

The Phase 5 commit adds test-only fixture packaging and direct regression coverage
for the pre-existing 256-module and 4 MiB aggregate-source limits. It changes no
production plugin/native source, public API, version, limit or dependency.

## Final Linux live result

The full run used the clean `0ae67e0` checkout and release native build. The
subsequent `5f272b9` commit changes only the profiler's requested output/coverage;
all production C#/native sources and other fixture behavior are byte-for-byte
unchanged, so the full result remains applicable under the compatibility policy.
The run
started a fresh Rust process, ran the composite fixture, completed 10 actual
plugin unload/load cycles, then exercised supported functionality once per minute
for 30 minutes before profiling and controlled shutdown.

| Qualification | Final result |
|---|---|
| Successful reload | PASS — 100 real facade generations; latency min/median/p95/max `0.464/0.592/0.713/3.006 ms`; VM bytes `632928..632928`; one host command and two listeners at each committed steady state |
| Failed reload | PASS — 108 candidates: entry/module syntax and runtime failures, module cycle, listener bound, invalid/duplicate command, provisional messaging rejection; active generation/session/command and healthy queued work remained valid; one live VM |
| Connection churn | PASS — 1,000 controlled-host disconnect/reconnect cycles; PlayerAdded/PlayerRemoving, directory lookup, old-proxy latching, distinct lifetime tokens, command context, permission query and messaging eligibility exercised |
| Permission/command | PASS — actual Carbon command publication/dispatch; explicit denial did not enter Lua and explicit grant did; no command accumulation |
| Memory pressure | PASS — 16 repeated 64 MiB VM-cap failures, bounded logging, 5,000 scheduler admission attempts, 1,000 event admissions, replacement cleanup and candidate allocation failure |
| Timeout | PASS — candidate entry, scheduled, Signal and command infinite loops; exactly one D9 reconstruction per armed state, second retirement, stale rejection and successful operator rearm |
| Plugin lifecycle | PASS — 10/10 unload/load cycles begun with module/task/Signals/command/Player/facade state; host command removed, native library unmapped, server responsive, new instance ready |
| Sustained soak | PASS with observation — 30 minutes, 30 operation pulses and minute samples; final trend and counters are recorded below |
| Shutdown | Rust saved and Carbon unloaded the plugin/native library after `quit`; the launcher reported exit 137, as in prior controlled runs, after the clean shutdown sequence rather than during operation |

The connection fixture constructs a controlled `BasePlayer` and `Network.Connection`
inside the isolated host. It is stronger than a managed-only fake but is not an
authenticated Steam/network client and does not prove packet delivery.

The VM cap result is a per-VM Luau allocator result. The Rust process retained a
roughly 2.8–3.1 GiB resident working set during these tests; this is not evidence
that CarbonLuau's cap limits the whole process.

### Sustained measurements

The JSONL contains 35 samples: startup, composite fixture, lifecycle completion,
30 once-per-minute soak samples, soak completion and post-profiler final state.
Each contains complete operator status.

| Point | Elapsed | `RssAnon` | `VmRSS` | VM bytes | State |
|---|---:|---:|---:|---:|---|
| Startup | 38.906 s | 2,767,120 KiB | 2,884,388 KiB | 632,928 | generation 1; no registrations |
| Composite complete | 40.501 s | 2,904,556 KiB | 3,023,184 KiB | 632,928 | generation 228; 2 listeners/1 command |
| 10 lifecycle cycles | 52.835 s | 2,980,336 KiB | 3,098,864 KiB | 632,928 | fresh generation 1; no registrations |
| Soak minute 1 | 53.135 s | 2,982,608 KiB | 3,101,136 KiB | 632,928 | generation 3; 2 listeners/1 command |
| Soak minute 30 | 1,798.711 s | 3,013,868 KiB | 3,132,500 KiB | 632,928 | generation 33; 2 listeners/1 command |
| Exact 30-minute deadline | 1,853.040 s | 3,014,056 KiB | 3,132,688 KiB | 632,928 | ready; empty queue; no swap |
| Post-profiler | 1,877.805 s | 3,014,472 KiB | 3,133,104 KiB | 632,928 | generation 44; ready; empty queue |

Across the 30 numbered samples, `VmRSS` increased 31,364 KiB and `RssAnon`
31,260 KiB. The first-five versus last-five `RssAnon` average differed by
22,515.2 KiB. Growth was stepwise: an early rise, approximately 13.4 MiB at
minute 11 and 4.1 MiB at minute 19, each followed by a plateau; minute 19–30
rose about 1.5 MiB total. `VmSwap` stayed zero, CarbonLuau VM accounting stayed
exactly 632,928 bytes, registration counts stayed 2/1, and every sample reported
ready with an empty queue. This is bounded allocator/process high-water evidence,
not an attributable CarbonLuau leak. The data cannot separate residual Mono,
Carbon/Rust, Unity and fixture allocations. The long-soak sampler did not expose
managed heap bytes separately; the composite fixture recorded managed
`GC.GetTotalMemory(false)` at 924,225,536 bytes before plugin lifecycle reset.

One ordinary second soak pulse crossed the configured 3 ms callback deadline.
CarbonLuau recorded one timeout, one failed callback and one permitted D9 recovery;
the pulse completed, the following operator reload rearmed recovery, and later
samples remained ready. No stale entry, second-timeout loop or server failure was
observed. This did not reproduce in short development runs and is retained as an
unexplained timing/fixture-host-scheduling observation, not classified as a
CarbonLuau defect or omitted because the run continued.

## Carbon profiler

Carbon's built-in Mono profiler in Carbon 2.0.259.0 was enabled only in the
isolated server configuration with call tracking scoped to `CarbonLuau`.
`c.profile 10 -c -m -t -gc` captured idle and active periods and
`c.profiler.print -j` exported JSON. The exact supplement ran from clean commit
`5f272b9`. The loaded idle script retained one far-future bounded task but had no
event activity. The active interval captured ten full pulses: ten reloads, 100
disconnects, 100 reconnects, 200 facade player events, ten command dispatches,
task/module work and drains.

| Capture | Carbon summary | Selected JSON measurements |
|---|---|---|
| Loaded idle | 9.9997428 s; 26 ms parse; 1 assembly/26 method rows | 93.903 ms total plugin time (about 0.94% of one core); 2,346 drains totaling 78.799 ms, about 0.0336 ms each; facade flush 1.356 ms total |
| Active | 10.0243956 s; 25 ms parse; 1 assembly/100 method rows | 29.907 ms total plugin time; ten fixture pulses 29.056 ms total; ten reloads 16.257 ms; 264 drains 10.082 ms; 200 player events 0.731 ms at `FacadeWorld.Event`; ten command session invokes 0.203 ms |

The active total is lower than idle because synchronous RCON fixture work
displaced many of the server's normal per-tick drain polls; the captures are not a
throughput comparison. Neither window recorded a managed exception, allocation
row or GC cycle even though GC tracking was requested. That is a profiler-window
observation, not proof of allocation freedom or native/Rust allocation data.
No unexpected CarbonLuau hot path appeared: idle time was the already-designed
bounded drain polling; active time was dominated by fixture work, replacement and
drain. No threshold or source optimization was introduced after the measurement.

The exact JSON SHA-256 values are
`91a4d83941f42db9ed769f21c6aa6728700c673298c5a6729d7789abde7b77bb`
(idle) and
`e0546163c765a66cefd9e3f3aa45c909f340f19f01b64b7115c7039f1af60179`
(active). The profiler does not expose whole-process native allocation
attribution, so process, managed and VM measurements remain separate.

## Build, fault and sanitizer evidence

Fresh builds ran from clean commit `0ae67e0` in separate capped containers. The
later `5f272b9` profiler-only change touched no compiled runtime/native source:

- release CTest: 4/4 passed (`ScriptCore`, `RuntimeAllocationFaults`,
  `RuntimeCore`, `NativeLoadUnload`) in 0.92 seconds;
- ASan/UBSan/leak run: the same 4/4 passed in 11.00 seconds with
  `detect_leaks=1`, ASan/UBSan halt-on-error and stack traces enabled;
- net48 runtime and loader builds: zero warnings/errors under .NET SDK 9.0.318;
- Mono runtime/facade suite: passed real ABI, configuration, snapshot bounds,
  100 atomic replacements, 200 failed candidates, 100 × 1,000 callback
  cancellation cycles, 2,000 reconnects, permissions, limits and recovery;
- loader: 100 managed native load/unload cycles passed.

An initial attempt to build the managed tests with `/source` mounted read-only
failed before compilation because NuGet could not create `tests/runtime/obj`.
This was an environment/harness mistake, not a test failure. The identical clean
source was copied to container-local writable storage and the full managed matrix
then passed.

## CI and platform status

Full-matrix GitHub Actions run
[`34933160638`](https://github.com/gmoddev/CarbonLuau/actions/runs/34933160638)
passed all Ubuntu 24.04, Windows latest and sanitizer jobs. Windows CI compiled and
ran native/runtime/loader/package tests and checked native imports, but it did not
start a Rust/Carbon server and is not labeled Windows live qualification.
Final tested source run
[`34935991022`](https://github.com/gmoddev/CarbonLuau/actions/runs/34935991022)
also passed all three jobs after the profiler-only fixture/runner commit.

| Target | Result and limit |
|---|---|
| Linux x64 live | PASS for the matrix above on Ubuntu 24.04.5 / current recorded Rust and Carbon builds |
| Windows x64 build/runtime tests | PASS in GitHub Actions |
| Windows x64 Rust/Carbon live | PENDING — no Windows Rust worker was available; BigVPS is Linux |
| Shockbyte intended host | PENDING — the current environment had no saved Shockbyte SFTP/panel profile; only BigVPS and MiniVPS SSH aliases were present. No upload or provider change was attempted |
| Authenticated real client | PENDING — no authorized client/session automation was available; controlled-host evidence is labeled separately |

The production package check passed with exactly the six shipping C# sources and
no native binary or live fixture. Its SHA-256 is recorded above. Phase 5's seventh
partial source is included only by the explicit fixture switch.

## Development-run attribution

Across development and final evidence review, five validation-fixture defects or
coverage gaps were exposed and corrected:

1. the expected Phase 2 module string was initially wrong;
2. the bounded-log assertion assumed a lower aggregate than the accepted limit;
3. one 5,000-enqueue Lua callback exceeded the 3 ms cooperative deadline, so the
   same 5,000 admission attempts were divided into ten bounded callbacks.
4. the first full profiler runner requested its default protobuf export and did
   not request GC events; `5f272b9` requests JSON and GC tracking so results are
   attributable and machine-readable;
5. the first active pulse did not execute its published command, so `5f272b9`
   adds one actual host command dispatch per pulse.

All were Phase 5 validation-fixture defects. They did not identify or change a
production runtime behavior. Subsequent short live runs passed. No production
CarbonLuau source correction was made.

## Limitations and retained state

- Thirty minutes is a useful worker soak, not proof of multi-day leak freedom.
- The profiler captures managed calls scoped to the plugin; they do not fully
  attribute Rust, Unity, Mono allocator or native Luau high-water behavior.
- The long soak did not separately sample managed heap size after lifecycle reset;
  process and CarbonLuau VM measurements are complete, and short profiler windows
  requested GC/allocation evidence.
- Controlled host objects do not establish authenticated networking or visible
  chat delivery.
- Windows live, Shockbyte and real-client acceptance remain pending.
- Mutable upstream setup installed the exact Rust/Carbon builds recorded above;
  this is not an unrestricted future-version promise.
- Task build trees, server cache and evidence remain under
  `/srv/codex/CarbonLuauPhase5` for reproducibility. No task container/server
  remains running after handoff.

Phase 4 remains deferred under D13. No Items/GiveItem surface, other gameplay API,
post-v0.1 work, tag or GitHub Release was created.
