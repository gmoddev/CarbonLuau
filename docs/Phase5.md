# Phase 5 — v0.1 hardening and qualification contract

Phase 5 qualifies the existing v0.1 feature set. It does not add scripting APIs,
reopen the deferred Phase 4 item surface, or make the independent release
decision. Actual results and limitations belong in
[Phase5-Validation.md](Phase5-Validation.md).

## Scope and evidence rules

The starting source is `43f402d390de0a63eceb308b385a0e51a539789c`
on `main`. Phase 0–3 evidence remains historical evidence within the limits in
[Compatibility.md](Compatibility.md). D13 remains resolved by deferral.

Every result records the tested source, environment and cycle count. A controlled
`BasePlayer` host object is not an authenticated network client. Carbon profiler
data, process resident memory, managed GC memory and CarbonLuau-accounted VM
memory are separate measurements. Allocator high-water is not called a leak
without an attributable continuing trend.

The task owner directed Linux Rust-server qualification to the BigVPS host because
the repository's `dockerbox` target is unavailable. All task state is isolated
under `/srv/codex/CarbonLuauPhase5`; task containers use distinct loopback ports,
names and resource caps. This exception does not qualify BigVPS as the new general
repository worker or permit changes to its Pelican workloads. BigVPS cannot
substitute for Windows live qualification.

## Canonical Phase 5 fixtures

[CarbonLuau.Phase5Fixtures.cs](../tests/live/CarbonLuau.Phase5Fixtures.cs) is a
test-only partial plugin source, included only when `package.ps1` receives
`-IncludePhase5Fixtures`. It is not part of the production package or public API.
It exercises the real Phase 3 facade with modules, scheduled work, Players,
Signals, commands and permissions.

[Test-Phase5Linux.py](../tools/Test-Phase5Linux.py) deploys the fixture and native
library into an already prepared, isolated Linux server tree. Its default run is:

- 100 successful facade reloads;
- 108 representative rejected candidate reloads;
- 1,000 controlled-host disconnect/reconnect cycles;
- VM allocation, bounded-log, queue, event and candidate-pressure tests;
- entry, scheduled, Signal and command timeouts with D9 recovery/rearm checks;
- 10 actual plugin unload/load cycles with active state and native map checks;
- 30 minutes of once-per-minute supported-operation pulses and memory/status
  samples;
- Carbon Mono profiler captures for idle and active periods.

The live runner requires an externally prepared Rust/Carbon tree and build output;
it does not download or mutate shared server state. Its task-local Docker image is
defined by [phase5-linux.Dockerfile](../tools/phase5-linux.Dockerfile). Production
packaging remains the default; fixture inclusion is explicit.

The managed script tests directly exercise UTF-8/path/reparse confinement, the
65,536-byte per-source bound, the 256-module bound, the 4 MiB aggregate snapshot
bound, the 1,024-node traversal bound, module caching/cycles, scheduler bounds,
atomic replacement and D9 timeout recovery. Native CTest remains the canonical
ABI, execution, allocation-fault and dynamic unload suite.

## Required observations

Successful and failed reload tests must preserve coherent generation ownership:
no old task, Signal, command or Player proxy may enter a replacement generation;
failed candidates must not publish effects and must leave the healthy generation
usable. Registration and native VM counts must return to their expected steady
state.

Connection churn must verify PlayerAdded/PlayerRemoving, directory lookup,
retained-proxy invalidation, lifetime tokens, permission checks, messaging
eligibility, command context and same-account reconnect. The controlled fixture
does not prove packet delivery or an authenticated session.

Pressure and timeout tests must fail in a controlled way without weakening limits.
The 64 MiB Luau allocator cap is per VM, not a process limit. D9 permits exactly
one automatic reconstruction after a timeout; the next timeout retires the
runtime until a successful operator reload rearms it.

Plugin lifecycle qualification starts each unload with modules, queued work,
Signals, a command, Player state and facade callbacks active. Unload must remove
host registrations, retire managed/native state, unmap the project native library
and leave the server responsive before the next clean instance loads.

The sustained soak records process `VmRSS`, `RssAnon`, `RssFile`, `VmSize` and
`VmSwap`, plus the full operator status. Profiler captures cover idle and active
reload/event/command/player/scheduler activity using Carbon's Mono profiler. No
post-observation performance threshold is introduced.

## Platform and provider boundaries

Final Linux evidence requires current Rust, Carbon, OS, compiler, Mono, .NET SDK,
Luau, package, API and ABI identities. Native release tests, managed tests and
ASan/UBSan/leak/allocation-fault tests run against the final affected source.

Windows x64 requires an actual Windows Rust/Carbon run; CI or a Linux host is not
a replacement. Shockbyte receives production artifacts only and only when the
authorized profile and safe operational verification are available. Provider
policy is never bypassed. Real-client evidence requires an authorized actual Rust
client and is reported separately from controlled-host fixtures. Missing safe
access is recorded as pending rather than inferred.

## Correction and stop policy

Only a reproduced CarbonLuau-owned defect or a narrow fixture correction may
change source. The affected and invalidated broader checks must then run again on
the final source. Stop the affected path if evidence requires an architecture or
public-API change, weakens a boundary, violates transactional replacement or D9,
permits stale entry, or reveals sustained attributable growth that cannot be
corrected narrowly.

Phase 5's execution verdict may be complete, partial or blocked. It is not the
independent v0.1 release-readiness verdict, and this phase creates no release tag.
