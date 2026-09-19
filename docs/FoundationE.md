# Foundation E: addon production qualification

Verdict: **PASS** for the first experimental public addon surface.

Foundation E adds no major addon capability. It qualifies Foundations A through
D at representative scale, corrects two scheduler defects found by inspection
and testing, assigns the public experimental identities, and records the support
boundary for the v0.4.0 candidate.

Starting commit: `d9bc2bba7844c0a837d1af80f7e88ee18bdeb5ba`.
Implementation checkpoint: `14d920c69cb343d0bab15df40b9c1d24fb48eb2d`.
Qualified implementation and documentation commit:
`8e0754eff36216230782b41acd4bcc44d4f0db69`.
Qualification date: 2026-09-17.

## Scheduler closure

Native ready-work selection now rotates across domains while preserving FIFO
order inside each domain. Managed facade delivery also rotates across the root
and addon domains under one global maximum of 256 deliveries and the existing
frame stopwatch. A saturated addon therefore does not receive an independent
frame budget and cannot indefinitely hold the front of the global scheduler.

Future-only work exposes its next monotonic due time. CarbonLuau schedules one
host timer for that due time instead of requesting a frame drain on every frame.
An earlier due task invalidates the old wake token and schedules the earlier one.
The no-host-driven-reentrant-VM-entry rule remains unchanged.

## Scale and resource measurements

The managed qualification fixture creates representative root-only, 1, 10, 50
and 100-addon configurations. Each addon owns state, a listener and a one-day
delayed task. Measurements include VM allocator bytes, immutable snapshots,
managed memory, process RSS, GC collections, activation/replacement latency,
service order and 10,000 idle readiness checks.

Windows x64 results on DockerPC/HostPC:

| Addons | VM bytes | Snapshot bytes | Managed bytes | Process RSS | Activation ms | Replacement ms | p50/p95/p99/max service ordinal |
|---:|---:|---:|---:|---:|---:|---:|---|
| 0 | 581,776 | 0 | 976,112 | 45,428,736 | 0.000 | 0.000 | 0/0/0/0 |
| 1 | 630,880 | 148 | 978,960 | 45,944,832 | 0.734 | 0.371 | 1/1/1/1 |
| 10 | 681,073 | 2,119 | 999,144 | 48,484,352 | 3.661 | 0.354 | 5/10/10/10 |
| 50 | 1,143,297 | 10,879 | 1,089,600 | 59,797,504 | 18.829 | 0.433 | 25/48/50/50 |
| 100 | 1,949,081 | 21,829 | 1,203,808 | 74,612,736 | 44.585 | 0.633 | 50/95/99/100 |

Ubuntu 24.04 container results on the same worker host:

| Addons | VM bytes | Snapshot bytes | Managed bytes | Process RSS | Activation ms | Replacement ms | p50/p95/p99/max service ordinal |
|---:|---:|---:|---:|---:|---:|---:|---|
| 0 | 581,776 | 0 | 4,477,424 | 81,281,024 | 0.024 | 0.000 | 0/0/0/0 |
| 1 | 630,880 | 148 | 4,482,000 | 81,416,192 | 0.778 | 0.458 | 1/1/1/1 |
| 10 | 681,073 | 2,119 | 4,528,936 | 82,108,416 | 3.609 | 0.475 | 5/10/10/10 |
| 50 | 1,143,297 | 10,879 | 4,618,424 | 89,006,080 | 21.840 | 0.556 | 25/48/50/50 |
| 100 | 1,949,081 | 21,829 | 4,765,224 | 92,770,304 | 45.248 | 0.840 | 50/95/99/100 |

GC deltas remained bounded to the collections induced by fixture setup and
explicit measurement. The 100-addon idle check performed no persistent frame
work and retained exactly one delayed wake. Native/compiler transient memory was
bounded by the clean worker builds and sanitizer jobs; no separate compiler peak
was isolated, so no narrower claim is made.

The live Carbon 100-addon fixture measured 1,752,713 VM bytes, 10,883 snapshot
bytes and 70.161 ms activation. The complete Rust process was about 3.02 GiB,
with a 38,748,160-byte RSS increase from the pre-scale sample. That process value
includes Rust, Unity, Carbon, Mono, map state and all plugins; it is not attributed
to the Luau heap.

## Memory-policy decision

The 64 MiB VM default remains appropriate for the qualified addon configuration.
Representative 100-addon use remained below 2 MiB. A separate pressure fixture
retained fifteen 4 MiB Luau buffers, reached 63,628,320 allocator bytes, rejected
the next activation with controlled `MEMORY_LIMIT`, and then successfully ran an
unrelated root operation in the same generation.

The clamp remains 16..256 MiB. There is one VM-wide cap and no per-addon hard
heap quota, retained-memory guarantee or whole-process memory limit.

## Fairness and bounded work

- A domain with 256 CPU-bearing ready callbacks did not starve ten unrelated
  domains. All ten made progress in the first service frame on Windows and Linux.
- Event fanout produced one bounded delivery per listening addon. The global
  managed drain returned at most 256 results.
- Native two-domain coverage verifies round-robin progress while retaining
  per-domain due-time and sequence order.
- Queue limits, the global frame stopwatch and the global no-reentrant-entry rule
  remain in force.
- One-day delayed tasks did not cause persistent per-frame work.

## Package and parser boundaries

The fixture accepts exactly 64 KiB for one source and rejects the next byte. It
accepts exactly 32 MiB of aggregate immutable snapshots and rejects the next
package. A forged ZIP uncompressed-size field cannot bypass the streamed actual
decompressed-byte check.

The qualified schema-1 bounds are 4 MiB archive, 8 MiB expanded data, 64 KiB
manifest, 64 KiB each source, 4 MiB aggregate source per package, 256 modules,
512 entries, 32 dependencies, 128 registrations, 32 registrations per provider
and 32 MiB aggregate snapshots. Existing malformed UTF-8/JSON, duplicate-key,
path, entry-type, encryption, nested-archive and export validation regressions
also passed.

## Live Carbon lifecycle

The isolated Windows server ran Carbon 2.0.259.0 and Rust protocol 2633.288.1.
The current-run fixture passed:

- provider unload from Registered, Blocked, Active and Failed states;
- stale token rejection, idempotent unregister and explicit re-registration;
- same-provider replacement and fresh domain publication;
- required dependency loss, restoration and dependent reconstruction;
- dependency replacement without optional-consumer hot rebinding;
- 100 additional active addon domains in the shared VM;
- complete CarbonLuau teardown with those addons while providers stayed loaded;
- native DLL unmapping and continued Rust server responsiveness;
- CarbonLuau reload, rejection of every old provider token and explicit fresh
  registration by all retained providers.

The server stopped cleanly and the runner restored every deployed file. Server
log SHA-256: `4948d610e6f29483660c52fbdf5b241bc72daf9ddc9b0f57d14816c1f94d3a2b`.

## Platform and regression matrix

- Windows 11 build 26200: MSVC Release build, four native CTest targets, CRT
  import check, complete managed runtime/addon suite, loader cycles, package and
  API checks passed.
- Ubuntu 24.04 / GCC 13.3: Release build, four native targets, complete Mono
  runtime/addon suite, loader and exported-symbol checks passed.
- Linux Debug with `CARBONLUAU_SANITIZE=ON`: all four native targets passed with
  ASan, UBSan and leak detection enabled.
- Foundations A through D and Phase 0 through 3 regression fixtures passed on
  both worker platforms.

Windows worker log SHA-256:
`436622734d1573ca836e9ca15a4c5a701791702950027901020de685aa63654f`.
[GitHub Actions validation run 35273700477](https://github.com/gmoddev/CarbonLuau/actions/runs/35273700477)
passed Windows, Ubuntu and sanitizer jobs. [Documentation run 35273700388](https://github.com/gmoddev/CarbonLuau/actions/runs/35273700388)
successfully deployed the hosted documentation.

## Public identities and remaining limits

The v0.4.0 candidate assigns scripting API `CarbonLuau 0.4.0-experimental`.
Package version is `0.4.0`; native ABI is `1.4`; provider protocol is
`CarbonLuau.Addons` / `1.2`; package schema is `1`; the Luau pin remains
`c6b830185af962c82003f86784e2fe036357c830`. None is stable or 1.0.

Provider-defined C# capabilities, root-to-addon imports, addons depending on the
operator root, version ranges/solving, multiple versions or instances, downloads,
registries, lockfiles, restricted exposure profiles and async capabilities remain
deferred. No work on those features began in Foundation E.
