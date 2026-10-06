# World/Entity Foundation 2B — public bounded discovery qualification

Status: implementation and local Windows/Linux public gates PASS; final hosted checks pending.
Starting checkout: `d48bbdb7747cadfdb8db9c4dcc88cb11ea0b384a`.
Qualification date: 2026-10-06. Final hosted receipts follow implementation publication.

## Authority and scope

[D20](Invariants.md#d20--worldentity-foundation-1) owns exact Entity identity,
startup continuity and bounded discovery architecture;
[I12](Invariants.md#i12--trusted-in-process-host-interference) owns the external
observation boundary. [Foundation 2A](WorldEntityFoundation2A.md) owns the
qualified private completed-Spawn catalog, position adapter and traversal
evidence. Its receipts do not qualify public Luau callback admission/conversion.

Discovery-2B consumes that substrate through one simple public callback method.
The [earlier callback proposal](WorldEntityAsyncTraversalInvestigation.md#small-public-proposal-not-an-implemented-contract)
supplies callback-third/options-last ordering; this task selects the optional
Limit field in addition to Prefab. The earlier research proposal remains
historical evidence, including its then-unselected limits. Production bindings,
bootstrap annotations, generated API artifacts and service/type references agree.

The author-facing contract and example are
[Entity discovery](api/Discovery.md) and
[examples/world/discovery/init.luau](../examples/world/discovery/init.luau).
Existing [keyed lookup](api/Services/Workspace.md), Entity properties and equality
retain their Foundation 1 contracts. No query DSL, spatial index, nearest sorting,
public cursor/cancellation object, lifecycle Signal or world mutation is added.

## Selected public contract

```luau
Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number,
    Callback: ({Entity}?, string?) -> (),
    Options: { Prefab: string?, Limit: number? }?) -> ()
```

Callback is required; Options is optional and last. Submission is non-yielding
and returns no value. Position is the existing immutable finite Vector3; Radius
is finite and nonnegative. The root-sphere comparison is inclusive, with exact
coincidence for Radius zero and overflow-safe arithmetic inherited from 2A.
Prefab is an optional exact full canonical string, nonempty valid UTF-8 of at
most 512 bytes without NUL; there is no normalization or resource lookup.
Limit is an optional integer 1..256, default 256. The next match beyond Limit
fails the whole query rather than truncating or implementing nearest-N.

Invalid input, stale authority and admission rejection raise controlled
synchronous errors without callback. An accepted query later supplies either
the entire dense Entity array plus nil error, including an empty array, or nil
array plus a controlled error string. Internal exception details are not a
script contract. No failed candidate is silently omitted to manufacture success.

## Submission, completion and lifetime

Submission requires an existing nonprovisional admission and no active publication
scope, including first-load module execution. Nested/shared calls, `pcall` and
foreign committed Workspace facades cannot launder provisional authority. A cold
module may export a closure used later by eligible committed execution.

Successful completion must validate every exact retained Entity lifetime and
host/session/VM/domain/publication authority at callback admission, then enter a
fresh bounded owner-thread Luau operation. Public proxy/result conversion must
remain bounded and must not introduce recursive host-driven VM entry. Submission
is never resumed and its execution deadline is not extended. Callback failure
or timeout cannot replay traversal or delivery. Retirement silently discards
pending completion and releases its resources; it cannot call a replacement
domain/VM merely because a package ID or source path matches.

Existing Entity lifetime checks and non-retargeting remain mandatory. Results
are non-owning facades; later property access revalidates and can fail after
admission. Query failure, retirement or recovery does not mutate Rust world state.

## Cohort, observation and bounds

Acceptance captures the completed-Spawn catalog extent and upper birth watermark.
Each physical slot is encountered at most once. Later births are excluded,
including reused-slot births; holes/reused slots consume raw work. Retired
pre-encounter entities are ineligible. Inclusion uses encounter-time root world
positions and exact prefab evidence. Result order is physical slot encounter
order, not distance. This is a complete watermarked-cohort traversal under those
observations, not an atomic world/pose snapshot. Movement after encounter does
not silently resample a candidate at callback time.

Trusted external writers may race scalar observations under I12. Exact lifetime,
native storage safety, resource accounting and CarbonLuau owner-thread ordering
are still hard requirements. The existing hash-pinned no-wait adapter and its
qualified structural-storage premise remain required; I12 grants no safety waiver.

The shared `EntityDiscoveryPolicy` remains the resource owner: 262,144 catalog
slots/total raw slots per request; 8 global pending requests, 2 per domain lifetime,
including completed-undelivered work; 1,024 shared scan/delivery units and at most
1,024 raw slots per frame; at most 256 results and 2 deliveries per frame within
that shared budget; a 120-second absolute deadline from acceptance through
completion admission. No slice or callback-queue delay resets the deadline.
Expiry is processed before further work/success when service resumes, rather
than promising a wall-clock callback SLA while the host is stalled.

First installation after world startup and full plugin unload/reload require
restart. Ordinary domain replacement or VM recovery may preserve the continuous
host observer but invalidates old public authority. Unavailable startup/catalog
state cannot be reported as successful empty discovery.

## Implementation and review corrections

Native `DiscoveryFacade.cpp` retains two fixed callback slots/domain and eight/VM,
independent of ordinary task/event queues. Existing `cl_domain_event` carries
only a private readiness notification: no C export signature/layout or ABI change.
Fresh fair scheduler admission owns callback execution/materialization deadline.
The managed host fetches/revalidates original weak catalog candidates at actual
entry, never by GetEntityById. All proxies are built before user callback.

Conversion is limited to 256 original candidates, four bounded weak-token sweep
opportunities each, and a final canonical pass. Token dictionary/queue capacity
is pre-sized cold for 262,144 entries; discovery refuses excess rather than
resize at admission. F1 keyed lookup is independent of catalog/conversion
exhaustion. Runtime object/GC/dictionary prime-sized array overhead is not an
exact cross-runtime byte claim. Native response: <=21,505 bytes/1,025 fields.
No host object or native pointer is owned by returned values.

Later codes: `DiscoveryDeadline`, `DiscoveryResultLimit`, `DiscoveryWorkLimit`,
`DiscoveryCancelled`, `DiscoveryStaleResult`, `DiscoveryReadFailed`,
`DiscoveryUnavailable`. Cancellation has no public handle. Retired authority
is discarded, not reported into replacement code.

Independent review corrections:

- Expiry formerly removed a pending route before readiness, potentially turning
  normal timeout into an intake fault. It now retains a ready deadline callback;
  one late first notification is harmless, genuine duplicates reject.
- Catalog availability formerly suppressed live failure callbacks. Callback
  authority is separate from result eligibility, including queued empty success.
- Pump failure/unexpected receiver destruction now use normal teardown to release
  native captures. Normal shutdown clears receiver ownership before destruction.
- Private release remains valid for its exact disposed session during native
  teardown, avoiding an integrity failure during healthy replacement.
- Final callback-free candidate check rejects qualification-delegate lifecycle
  reentry during conversion. Production authority predicates remain non-reentrant.

An initial token cap accidentally affected F1 lookup after catalog overflow.
The existing regression caught it; the restriction was removed from keyed lookup
and the 17,275-check suite rerun. No architecture was weakened.

## Final-source live receipts

Exact targets/images remain those pinned by [2A](WorldEntityFoundation2A.md):
Rust build 25653776, protocol 2634.289.1, revision 166494; Windows Carbon
2.0.262.0/8a81d70; Linux 2.0.261.0/c74c4ca; Harmony 2.4.2.0.
Final fixture package on both hosts SHA-256:
`40b4a42af6d320f992a5089a22a2b7fbde973f8ed63f6f9b438ebe9925a56791`.

Windows receipt:
`D:\Sandbox\Codex\Entity1AStartup\evidence\discovery-public-20261006-051516-202\server.log`,
SHA-256 `5e93d3f615440a420a3392da6e3697541548f12c1d9470a9ded712da9c66ad34`.
Linux receipt:
`/root/codex/world-movement-20261003/evidence/discovery-public-20261006-051524-1398295/server.log`,
SHA-256 `1454500325a0d19b23b9bcb7b45f41e4f55702fe0444275373bd2b4279a79375`.

| Artifact | Windows SHA-256 | Linux SHA-256 |
|---|---|---|
| Native | `83fdfc4bea656a8eb57256bffaf5dc4645a57083c6f9a1f0d588dd6e1f32cae2` | `511593f83202f431bae4f119afabb3cdd8ee992c13b6589fbc3b52287638ce5f` |
| Compiler | `887a0f66d2a86257b729833920fd0205c380e8b55836ac9a67a43ad31cf89390` | `884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92` |

Commands: `Test-DiscoveryPublicWindows.ps1 -RunCoordinated` through hidden
PowerShell scriptblock on dockerbox; `Test-DiscoveryPublicLinux.py
--run-coordinated` on BigKVM with those staged inputs. Both use only localhost
ports 28335/28337; no authenticated-client claim. Startup keyed completions:
1,018 Windows / 1,005 Linux. Both **28 public assertions PASS**.

Both run source through real pinned compiler/native VM: invalid input/zero
intake; provisional/cold/nested rejection/cached committed export; global/domain
limits through readiness; empty/one/many; inclusive/zero radius; exact prefab
case/prefix; options snapshot; 256 success/257 whole failure; death after
traversal before admission; synthetic ID reuse/no retarget; later birth exclusion;
movement before encounter/after observation; nested later-turn query; failed-root
preservation/successful-root cancellation; cancellation error; admission deadline;
callback error/no replay; fatal recovery/fresh authority; empty-ready/scanning
catalog loss; unexpected receiver teardown. Injected clocks and synthetic ID
reuse are fixtures, not natural pooling or waiting 120 real seconds.

Cleanup verifies zero requests/traversals/VMs/facade roots/owned entities.
Prior package/native/compiler/hooks/task-config hashes are restored; no
server/listener remains. Deterministic production package: 72 C# sources,
no live fixture. Historical negative research is unchanged.

Earlier Windows receipts `050232-246` and `050510-205` contain passed assertions
but runner failures (lost exit status, incorrect zero-exit assumption). Pinned
`ConVar.Global.quit` IL, method 0xa09e, calls Shutdown then Process.Kill at
IL_0061. Runners retain the handle and accept -1 Windows/137 Linux wrapper only
with all receipts and shutdown/save markers. This fixes the harness, not a
production-crash waiver. Earlier package `10b5813f...` receipts remain; both
final runs above repeat after the defensive conversion guard.

## Models, native, tooling and hosted gates

Lifetime/catalog model: 17,275 retained checks. Updated discovery/conversion
model: 3,925, covering exact public identity, ID/epoch/slot reuse, death,
continuity and final authority-check reentry. The unchanged 2A full-catalog
stress (262,144 slots/eight queries/2,097,152 inspections per lane) is inherited
within its recorded non-host limitations.

New real-VM native/managed tests cover reservation saturation, queued admission,
publication/laundering, callback error/yield/timeout, expiry around notification,
provider/domain retirement, replacement/recovery, allocation faults and cleanup.
Controlled-host public stress: 1,000 cycles in 64-cycle batches, held/queued zero,
no route reuse; Mono observed native heap 861,384 bytes before/after, 48 ms
observation (not SLA). This is capture/binding convergence, not simulated
position/catalog qualification. The example executes in the pinned compiler/VM
against the controlled host; real position/filter evidence is separate above.

API JSON and generated artifacts follow production annotations. Explicit
`RejectExtraArguments` permits guard-only ellipsis without public variadic typing.
Tooling negative tests catch reordered arguments, missing guards and
type/availability drift. .NET 10 tooling/schema/preview goldens, API/link,
architecture, release and production-package checks PASS. Preview unavailable;
no world simulation. New declarations retain SinceApi `0.6.0-experimental`.

Windows native cache: `D:\Sandbox\Codex\Discovery2BNative`, parallelism two;
managed/models: `D:\Sandbox\Codex\Discovery2B-20261006`; self-contained model
artifacts also run on linuxbox. Docker Desktop was unavailable and not restarted.
Linux uses existing cached builder images in isolated two-CPU task containers
under `/root/codex/discovery2b-20261006`. No host install/policy change, new
public port or unrelated container mutation. Native/sanitizer and final hosted
receipts follow implementation publication.

Windows and Linux each pass six affected native suites, plus the managed real-VM
`--discovery2b` test; Linux ELF unloadability also passes. Linux ASan/UBSan/leak
checks pass DiscoveryFacade and RuntimeAllocationFaults, including allocation
faults, expiry/notification races, callback error/yield/timeout, failed release,
retirement and no replay. Linux receipts:
`/root/codex/discovery2b-20261006/logs/release.log`, `sanitize.log`,
`final-models.log`, `discovery-public-mono.log`. Temporary containers exited;
298 MiB of task-owned caches/evidence retained. Final hosted full regressions
remain the last gate, not substituted by these targeted runs.

## Identities and handoff

API `0.6.0-experimental`, package `0.5.0`, ABI `1.5`, provider `1.2`, schema `1`
and Luau `c6b830185af962c82003f86784e2fe036357c830` unchanged. No release/tag,
Marketplace publication, Signals, Spawn/Destroy, Position writes, DSL, spatial
index or public cursor. Next: separately scoped combined Foundation 2
final/public-release-readiness closure; no closure implementation begins here.
