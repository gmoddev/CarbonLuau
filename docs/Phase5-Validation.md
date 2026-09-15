# Phase 5 qualification — 2026-09-15

Execution verdict: **PARTIAL**. The committed Windows and Linux live matrices,
final command-bearing sustained soaks, actual p95 measurement and profiler
captures passed. Authenticated real-client chat receipt remains unqualified and
is still required by the canonical v0.1 definition of done. Shockbyte
full-runtime qualification is deferred and non-gating by explicit user decision;
that does not make it supported. This execution verdict is deliberately not the
independent v0.1 release-readiness decision.

## Revision and identities

| Field | Qualified value |
|---|---|
| Starting branch / baseline | `main` / `43f402d390de0a63eceb308b385a0e51a539789c`; clean and equal to `origin/main` before work |
| Full live-matrix commit | `0ae67e0483137bf496096e58a5269474e8404564` |
| Original Linux full matrix | `0ae67e0483137bf496096e58a5269474e8404564`; retained historical evidence |
| Original profiler supplement | `5f272b98f1aed9d4bc0e3d4fa19e7eb7b1718946` |
| Windows runner commit | `4f073a99df877bcd92a9fa135dde6cbc414815df` |
| Final tested production source | `ae612c9ca86619d5e9d9591a17fd917a3e5b6645` |
| Final fixture/instrumentation source | `e1a7e64bebd55faffd32fbeda01d89d070ff21e8`; no `src/` or `native/` differences from final production source |
| Production corrective commits | None; qualification found no reproduced CarbonLuau production-source defect requiring a correction |
| Package / scripting API / native ABI | `0.3.0` / `CarbonLuau 0.3.0-experimental` / `1.2` |
| Luau | `c6b830185af962c82003f86784e2fe036357c830` |
| Rust | Protocol `2633.288.1`; build date 2026-09-10 11:23:47; Steam build `25230300` |
| Carbon | `2.0.259.0 [2026.09.03.0] 21063e8` on Windows and Linux |
| Linux worker | BigVPS isolated Docker; Ubuntu 24.04.5 LTS, x86_64; GCC 13.3.0; .NET SDK 9.0.318; Mono 6.8.0.105 |
| Windows worker | DockerPC / HostPC; Windows 11 Pro 64-bit, build 26200; MSVC 19.44.35228; .NET SDK 9.0.317; Unity Mono runtime |
| Windows native SHA-256 | `b693092c271023d7f08b80ed8a4ee1ea84fb0442be307b8a623ec1684c8532d9` |
| Final Linux native SHA-256 | `de15abe4129db3da05179995c29a1e80e0600685ddcc406d9e6b4874adc63aaf` |
| Production package SHA-256 | `af35d5365613d61b639b5b0ecc3843fd82314e2a7198c71debdade2c31e0f601` |

The compact machine-readable ledger is
[Phase5-Closure.json](evidence/Phase5-Closure.json); larger raw files remain on
the isolated workers at the paths and hashes recorded below.

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

## Final-source Windows live result

The isolated DockerPC server used Rust protocol `2633.288.1`, build date
2026-09-10 04:23:47 local display / Steam build `25230300`, and Carbon
`2.0.259.0 [2026.09.03.0] 21063e8`. A fresh Windows native build passed 4/4
CTest, the static-CRT import check, net48 runtime/facade/loader tests, packaging
and API/link checks before live execution. The live fixture then passed the same
100 successful reloads, 108 failed candidates, 1,000 controlled reconnects,
pressure and D9 timeout matrix as Linux. Ten of ten actual plugin unload/load
cycles removed the host command, confirmed `carbonluau_native.dll` absent from
the Rust process module list, kept the server responsive and loaded a clean next
instance.

The final Windows soak ran 30 once-per-minute command-bearing pulses. All 662
sustained callbacks completed, with zero failures, timeouts, recoveries or frame
budget overruns; the queue stayed empty, registration state stayed at two
listeners/one command and VM accounting stayed exactly 632,928 bytes. Numbered
samples moved from 3,195,707,392 to 3,221,749,760 working-set bytes (+26,042,368),
while first-five versus last-five averages differed by 19,076,710.4 bytes. Private
memory moved +22,966,272 bytes and its average shift was 6,526,566.4 bytes. The
late samples plateaued; this is process allocator high-water evidence, not an
attributable CarbonLuau leak.

The runner logged plugin/native teardown after `quit`, the Rust process was gone,
and the temporary RCON secret was removed. Windows PowerShell did not expose a
numeric child exit code, so no numeric clean-exit claim is made. The sample,
runner and server-log SHA-256 values are respectively
`c771577e5ab40c1e60451f5015dcbf980cd89a59041766f21da7975bf3ae7607`,
`707c08650af72765f60f82ef873b6304ac7f4ce01fcad2c2e944ec13f6975593`
and `64700e408d3fd9be46e046c837bf7d838219adab90fdebc16de1d89689d90f5c`.
Raw artifacts remain under
`C:\Sandbox\Codex\Artifacts\CarbonLuauPhase5Windows`; profiler JSON remains
under the isolated server's `carbon\profiles` directory.

## Final-source Linux sustained supplement

The clean `e1a7e64` runner/fixture revision reran the complete live matrix, not
only the soak, using production runtime/native bytes unchanged from `ae612c9`.
It passed the composite matrix and 10/10 plugin lifecycle cycles, then completed
30 command-bearing pulses and 30 numbered samples. All 662 sustained callbacks
completed with zero failures, callback timeouts or D9 recoveries; the queue stayed
empty, registration state stayed two/one, VM accounting stayed 632,928 bytes and
swap stayed zero.

`VmRSS` moved from 3,049,820 to 3,091,780 KiB (+41,960 KiB); first-five versus
last-five averages differed by 38,913.6 KiB. `RssAnon` moved +41,736 KiB. Growth
was stepwise through minute 17 and then plateaued: minute 18 to minute 30 added
only 228 KiB RSS. One frame-drain budget overrun appeared at minute 17 and did not
repeat. It was not a 3 ms callback timeout, caused no failed callback or D9
recovery, and did not affect responsiveness. The historical ordinary timeout
above did not recur, but remains retained and unexplained.

The sample, runner and server-log SHA-256 values are respectively
`53802783d05d1b7f57429d34e66af54a64f9a19f894c721beb9eaa43cfaa3fe5`,
`b93490fe02d1c8a4465255f12644fa66b0cb9ee7983682e16866687b581cd490`
and `840c6c3105d7530cca694a94461b6730f4471a47db288fb02f6ad2f50fa9af5b`.
Raw artifacts remain under `/srv/codex/CarbonLuauPhase5/runtime/artifacts`.
Rust saved and Carbon/native teardown completed; the surrounding launcher again
reported 137, which is preserved as non-clean wrapper termination rather than
misreported as a clean process exit.

## Dispatch latency distribution

The test-only `carbonluau.phase5latency` fixture warms 100 samples, then measures
2,000 individual no-op `PlayerAdded` callbacks through the production
`FacadeWorld.Event` admission and native `ScriptHost.Drain` path. It does not
bypass the facade/scheduler and adds no shipping telemetry. The existing p95
target is less than 0.25 ms per callback.

| Platform | Count | p50 | p95 | p99 | Max | Result |
|---|---:|---:|---:|---:|---:|---|
| Windows | 2,000 | 0.0124 ms | 0.0881 ms | 0.2065 ms | 11.1957 ms | PASS p95; isolated max retained |
| Linux | 2,000 | 0.0272 ms | 0.0387 ms | 0.0789 ms | 1.8419 ms | PASS p95; isolated max retained |

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

The final `e1a7e64` runs repeated the same 10-second idle and active captures on
both platforms. Both idle exports contained no tracked CarbonLuau call rows. The
active interval on each platform executed eleven requested pulses, of which ten
complete pulses fell within Carbon's capture window:

| Platform | Duration | Tracked plugin time / calls | Selected active paths |
|---|---:|---:|---|
| Windows | 10.0340192 s | 26.663 ms / 25,432 | 10 reloads 19.209 ms; 200 facade events 0.518 ms; 220 drains 5.394 ms; 10 host-command invokes 0.434 ms |
| Linux | 10.0319894 s | 23.620 ms / 25,420 | 10 reloads 13.381 ms; 200 facade events 0.687 ms; 220 drains 7.525 ms; 10 host-command invokes 0.652 ms |

No tracked exception, allocation row or GC cycle appeared in either final active
window despite the GC request; this remains a profiler-window observation, not
allocation-freedom evidence. Final idle/active JSON SHA-256 values are
`64c548786fd3cff3e18bb4faf3219e42c5a288a1d9607b48faaf6cd9ee6613ff` /
`47cef0a232b0dfab1b0aba587995192027d85293819f8529d015c2b2616c85a7`
on Windows and
`376ce72042b1cbe2d7e3fa2af2bc19561253aebdd26f3cf83976f30b8e7c53f6` /
`2fa4dcce7c2bff0d54b4f09769660d26bd1c62bd7bb7962e21ad7239276aad1a`
on Linux.

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
Earlier final-production-source run
[`34935991022`](https://github.com/gmoddev/CarbonLuau/actions/runs/34935991022)
also passed all three jobs after the profiler-only fixture/runner commit.

| Target | Result and limit |
|---|---|
| Linux x64 live | PASS full matrix: reloads, rejected candidates, connection churn, pressure/D9, 10 plugin cycles, p95, profiler, final 30-minute command-bearing soak; launcher exit 137 retained |
| Windows x64 live | PASS full matrix: reloads, rejected candidates, connection churn, pressure/D9, 10 plugin cycles, p95, profiler, final 30-minute command-bearing soak; process absent after teardown, numeric exit unavailable |
| Windows/Linux CI and sanitizers | PASS on the earlier final production source; final fixture/docs CI recorded below after completion |
| Shockbyte intended host | DEFERRED/NON-GATING by user decision; only historical Phase 0 probe evidence exists, Phase 1-5 full runtime is unqualified, and no support claim is made |
| Authenticated real client | PENDING/GATING — no authorized client/session automation was available; controlled-host evidence does not prove chat receipt required by the current definition of done |

| Criterion | Windows | Linux |
|---|---|---|
| Successful reload soak | Live PASS, 100 | Live PASS, 100 |
| Failed reloads | Live PASS, 108 | Live PASS, 108 |
| Controlled connection churn | Live PASS, 1,000; not authenticated | Live PASS, 1,000; not authenticated |
| Memory pressure | Live PASS | Live PASS |
| Timeout/D9 | Live PASS; composite fixture 6 timeouts/3 recoveries | Live PASS; composite fixture 6 timeouts/3 recoveries |
| Plugin lifecycle | Live PASS, 10/10; module-list DLL absence | Live PASS, 10/10; `/proc` map absence |
| Profiler/performance | Live PASS; 2,000-sample p95 plus idle/active JSON | Live PASS; 2,000-sample p95 plus idle/active JSON |
| Long soak | Live PASS, 30 command-bearing pulses; no observations | Live PASS, 30 command-bearing pulses; one non-recurring frame-budget overrun |
| Sanitizers/faults | Windows native/managed fault suite PASS; sanitizers are Linux-only | ASan/UBSan/leak and native/managed fault suites PASS |
| Server shutdown | Teardown logged and process absent; numeric exit unavailable | Save/teardown logged; wrapper exit 137 retained as non-clean |

The production package check passed with exactly the six shipping C# sources and
no native binary or live fixture. Its SHA-256 is recorded above. Phase 5's seventh
partial source is included only by the explicit fixture switch.

## Development-run attribution

Across development and final evidence review, seven validation-fixture defects or
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
6. the first Windows runner wrote the profiler JSON with Windows PowerShell 5.1's
   UTF-8 BOM, which Carbon's native parser rejected; BOM-free UTF-8 passed on the
   next short run and the final run;
7. aggregate profiler values did not establish the existing event-dispatch p95
   target, so `e1a7e64` added the bounded test-only 2,000-sample distribution.

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
- Authenticated real-client chat receipt remains unqualified and gates the current
  definition of done.
- Shockbyte Phase 1-5 full runtime is deferred and non-gating, but unqualified and
  unsupported; Phase 0 probe evidence remains historical.
- Mutable upstream setup installed the exact Rust/Carbon builds recorded above;
  this is not an unrestricted future-version promise.
- Task build trees, server cache and evidence remain under
  `/srv/codex/CarbonLuauPhase5` and
  `C:\Sandbox\Codex\{Builds,Artifacts,Workspaces}\CarbonLuauPhase5Windows*`
  for reproducibility. No task container/server remains running after handoff.

Phase 4 remains deferred under D13. No Items/GiveItem surface, other gameplay API,
post-v0.1 work, tag or GitHub Release was created.
