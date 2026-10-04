# World/Entity Foundation 2A — private bounded asynchronous traversal

**FOUNDATION 2 ASYNC TRAVERSAL QUALIFIED — PUBLIC DISCOVERY MAY BEGIN**

Starting revision: `f36ff7c83a9e273816f7b726fe3062618b430539`, with the
intentionally uncommitted Foundation 2 research preserved. Qualification date:
2026-10-04. This record owns private implementation and final-source receipts.

## Scope and consistency

[D20](Invariants.md#d20--worldentity-foundation-1) and
[I12](Invariants.md#i12--trusted-in-process-host-interference) distinguish:

- Hard exact lifetime/non-retargeting, memory/resource safety and bounded work.
- CarbonLuau-owned owner-thread ordering, including request acceptance, traversal
  turns, cancellation, retirement and delivery authority.
- Non-atomic external observations. Trusted Rust/Unity/Carbon/Oxide/physics/job
  value writes can race a candidate sample. There is no universal external-writer
  serialization or atomic world/pose snapshot.

This distinction does not permit unsupported off-thread structural mutation,
invalid Entity admission, unchecked native lifetime, unbounded waits or raw host
escape. No Entity-1A predicate is weakened. Public `Entity.Position` is unchanged.

Implemented: private completed-Spawn catalog, encounter-time root position copy,
bounded incremental filtering, exact result validation and private C# completion
seam. Not implemented: public discovery binding/metadata/callback signature,
spatial index, movement maintenance, Signals, Spawn/Destroy, generic DSL or new
release identity. Public discovery may consume this substrate only after separately
qualifying fresh bounded Luau completion admission and metadata/documentation.

## Catalog and traversal theorem

`EntityLifetimeModel` receives a fixed catalog capacity before world restoration.
Qualified outer completed Spawn enrolls immutable weak identity/epoch/birth state
directly, even if no proxy/token was requested. Beginning another Spawn fences old
membership immediately; failed/PENDING attempts cannot enroll. Existing exact
startup reconciliation checks membership alongside its original lifetime proof.
Observer/startup continuity loss remains sticky. Overflow/enrollment loss disables
discovery, not independent Foundation 1 exact keyed lookup.

A query captures catalog extent and upper birth ordinal. It walks each physical
slot once. Holes and post-watermark births consume budget; a reused slot cannot
retarget an earlier candidate. Entities retired before encounter are ineligible;
later births are outside that request. Current properties are sampled at encounter,
not submission. Results preserve physical encounter order. A same-object/same-epoch
unobserved registry remove/reinsert is not an invented incarnation; observed
occupancy loss may latch retirement. Different object/ID/epoch/prefab cannot revive
an old record. Restart/unload never carries token or cursor authority forward.

Before successful delivery every retained exact lifetime is revalidated against
host, VM/domain/publication witness, keyed occupant, epoch and Spawn patch authority.
Discovery uses direct immutable candidate state/birth plus latched ID/prefab evidence,
not a global public token. It does not probe the CWT, acquire public proxy tokens,
grow token dictionaries/queues or sweep them inside query turns. Public proxy
conversion is separately bounded work for the public phase.
A final callback-free model pass prevents an earlier result from becoming stale
while a later validator runs. Invalid result/authority/continuity fails the whole
request. Retired callback authority is discarded, never retargeted into replacement
code. Results are weak identity plus copied scalars/strings, never native pointers.

Round-robin scan quanta share one global work budget. Delivery is FIFO by readiness,
not submission. Requests accepted inside completion cannot run until a later turn;
recursive intake is suppressed. Detachment precedes callback and resources clear
after success/error/discard/cancel/expiry/Dispose. Callback failure cannot replay.
The private completion consumer must remain bounded and must not recursively enter
Luau; this phase does not qualify arbitrary injected delegates.

## Exact-host bounded position adapter

The cached `BaseNetworkable.TransformHandle` getter is exactly three IL instructions:
load `this`, load `_transformHandle`, return. Its sole write is SpawnShared's root
capture. It avoids Component getters/native wrappers during the borrow. Pinned
TransformHandle is 16 bytes: descriptor pointer at 0, EntityId at 8.

The exact hierarchy descriptor supplies capacity at `H+0x10`, 48-byte TRS records
through `H+0x18`, and four-byte parent indices through `H+0x20`. Position, quaternion
and scale are copied at offsets 0/16/32. `H+0x28` subtree counts are not an index
bound. Every index/offset is checked, capacity cannot exceed `Int32.MaxValue/48`,
and only parent `-1` terminates. Cycles/excess depth/nonfinite data are whole-query
failure, not skip/defer/subset success. Up to 65 records, 64 compositions and 722
native scalar/pointer/index loads bound this read, independent of job backlog.
The fixed 2,600-byte scalar scratch does not retain pointers or call user code.

Storage safety reuses the supported host's main-thread structural mutation and
reclamation premise. The uninterrupted owner-thread borrow never enters Unity,
waits/helps jobs, acquires a shared monitor, schedules work or invokes callbacks.
Supported TransformAccess job methods write existing scalar TRS/dirty state, not
reparent/destruction/capacity. Thus supported structural reclaim cannot interleave
inside this borrow, even though scalar writers can. Catching exceptions or checking
the descriptor afterwards is diagnostic, not a fabricated storage lease. A managed
wrapper by itself is not claimed to provide another engine lease.

Exact source/native inspection distinguishes scalar setters from structural
reparent/capacity/free paths. Linux local setter `0x803ce0..0x803d86` has no calls,
allocation or parent-pointer writes; Windows internal setter `0x5d8d0..0x5d9a4`
likewise updates existing lanes/dirty state. Structural capacity/reparent/free
paths wait registered work and replace/free storage; they are not reachable from
the supported job value-write surface. This is a supported-host contract, not
protection against arbitrary unsafe external CLR/native memory mutation.

The hot evidence path reads `_prefabName` directly (cached FieldInfo), or searches
a cold immutable sorted copy of the already initialized manifest pool. It never
invokes `PrefabName`'s cold StringPool.Init/logging path. Carbon's loaded host image
publicizes fields, so resolution accepts public or private visibility while exact
field types/image identities remain checked. The snapshot contains 42,112 entries
on both qualified hosts, capped at 262,144. No SQL, file or reflection authority
is exposed to scripts. Registry Find reuses the qualified non-enumerating F1 path;
source cardinality is checked at every evidence read against 262,144, bounding even
the collision-chain case rather than promising worst-case O(1). Managed collection
and GC overhead is not claimed to be a hard nanosecond bound.

Cold startup pins Rust/Carbon through Entity-1A, plus UnityPlayer/CoreModule hashes
and descriptor layout. Unsupported/drifted hosts fail discovery closed. Runtime
types/effective Spawn paths are inventoried cold (4,096 types, 24 targets/path).
Patch verification uses a zero-timeout monitor attempt, at most 8,192 patch records
and reference stamp comparisons; contention fails the query rather than waiting
or omitting authority. No new Harmony/native patch is installed for positions.

## Shared private resource policy

| Resource | Hard private bound |
|---|---:|
| Catalog slots / raw slots per request | 262,144 |
| Requests global / per domain lifetime | 8 / 2 |
| Shared scan/delivery units per Update | 1,024 |
| Raw slots per Update | 1,024 |
| Results per request | 256 |
| Deliveries per Update | 2 |
| Absolute query deadline | 120 seconds |
| Maintenance per opportunity | 64 catalog entries |
| Root hierarchy records / compositions | 65 / 64 |
| Prefab UTF-8 bytes | 512, existing D20 limit |

The capacity inherits qualified startup's registry ceiling; it is not a promise
that every world/history stays admitted. Catalog loss fails closed, never creates
a successful subset. Two fixed catalog arrays require 3 MiB of payload on x64
(eight-byte references plus four-byte free-slot indices). Membership objects/weak
handles are capped by slots plus at most 2,048 retained query observations. Queries
retain at most eight fixed 256-entry result arrays, eight cursors/requests, bounded
completion queue and one candidate scratch. A cold manifest snapshot adds at most
262,144 uint/string-reference pairs (3 MiB payload), sorted once; a hot lookup uses
at most 19 comparisons. Strings remain existing bounded prefab
references, not per-query copies of host objects. Runtime object headers/GC bookkeeping
are implementation overhead, not a fabricated cross-runtime exact-byte claim.

Each scan quantum counts holes too; delivery costs `1 + 2*Count` shared units.
Two maximum deliveries cost 1,026, so cannot both fit in one 1,024-unit turn.
Round-robin selection itself scans at most eight request slots. Deadline applies
to queued delivery, not only scanning. Paused/busy host frames do not reset it;
expiry is processed before further work when service resumes. No callback wall-time
SLA under arbitrary host starvation is promised.

One fixed private MonoBehaviour Update receiver is created cold and torn down with
the plugin. There is no per-query/frame enqueue into Carbon's shared lock/growing
NextFrame list. Owner-thread and Host.Busy checks exclude recursive VM entry;
OnTick only sweeps when no query is active. Unload clears receiver ownership before
deferred Unity destruction, silently disposes pending work and releases captured
session/runtime/delegates/results. No new component is exposed to Luau.

The 1,024/65 cap is supported by native operation counts and live observations:
1,024 maximum-depth samples took about 16–17 ms; ordinary ~1,000-candidate turns
took about 6–9 ms on these hosts. Eight full catalogs need at least 2,048 scan
frames (~68 seconds at 30 fps); 120 seconds gives practical headroom, not guaranteed
completion under contention/GC/host load. These timing observations are not hard
time guarantees and do not replace the operation bound.

## Exact targets and live final-source receipts

Rust build `25653776`, protocol `2634.289.1`, revision `166494`.
Windows Carbon `2.0.262.0` / `8a81d70`; Linux `2.0.261.0` / `c74c4ca`;
Harmony `2.4.2.0`; Unity `6000.3.15x1-13 (a91cf34396ee)`.

| Image | Windows SHA-256 | Linux SHA-256 |
|---|---|---|
| Assembly-CSharp | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| UnityPlayer | `6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a` | `ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1` |
| CoreModule | `93b0e7e8e1b9a82f34d09740c45be2cbd7c13a82683a192af2858831b63ce45a` | `ada97d7037c7c928d8d432f734115d82a3d805a2da628f481901da5d165795e2` |

Both final live packages SHA-256:
`0975ab146b89e4d98321a04a5b28c14ea2fe717e96f17a362f74f64c214d0152`.

Windows receipt: `D:\Sandbox\Codex\Entity1AStartup\evidence\discovery-20261004-053852\server.log`,
SHA-256 `c08c38f07e76ae4e1eb4b58f6ae2d3c2161433b7982f541e8146db5090036b8d`.
Linux receipt: `/root/codex/world-movement-20261003/evidence/discovery-20261004-053900/server.log`,
SHA-256 `24662f68924de9826a35c5aa58d411ac7235f00f98e96384457011ce554fc5e8`.

Both pass: catalog membership before proxy creation; root and non-Entity rotated/
scaled ancestry; depth 65 accept/66 reject; no-wait borrow while a deliberately
two-second Transform job is demonstrably incomplete before and after read; later
birth excluded; nested request includes current membership; moved-out candidate
excluded at encounter; root replacement discards old completion; new authority
reacquires; killed lifetime invalid; bounded cleanup and graceful unload. Shared
F1 token count remains four (fixture-created proxies only), with no scan growth.
Explicit cold-prefab fixture clears only its owned box cache, obtains exact
manifest/keyed evidence without populating the cache, then restores it. This
adds branch evidence without a production change relative to `527f3fb`.

Windows: catalog 1,019, first query 1,021 raw slots/two results, 189.866 ms elapsed,
5.511 ms maximum measured turn; 1,024 depth-65 samples 16.921 ms.
Linux: catalog 1,004, first query 1,006 raw slots/two results, 177.546 ms elapsed,
7.471 ms maximum turn; depth-65 samples 15.829 ms.
These are disposable-server measurements, not authenticated-client claims.
Both use localhost-only ports 28335/28337. Processes exited; prior package restored.
Windows additionally restored task config and prior hooks after temporarily using
the qualified historical hook tuple (not the drifted Oxide DLL). Security policy
was not changed. Unrelated workloads/data/caches remain untouched.

## Model, regression and review evidence

Windows and Linux: lifetime/catalog model 17,275 checks; scheduler/direct observer 3,896 checks;
float composition 1,667 checks. Coverage includes slot reuse/ID reuse/epoch failure,
continuity loss, weak cleanup, overflow, restart/authority replacement, deadline,
raw/result limits, fair concurrent work, stale results, callback faults/nesting,
at-most-once disposal and retirement around admission/delivery.

Full-catalog opt-in benchmark: eight requests/four domains, 262,144 populated slots,
2,097,152 inspections per lane, 256 retained results per observed query, zero fairness
spread, 293,023 checks on each platform. Maximum units/raw/calls: 1,024. Skip lane
2,052 turns; observed lane 2,056. Windows median/p95 turns 0.147/0.153 ms (Skip),
0.230/0.257 ms (Observed256); Linux 0.115/0.124 and 0.119/0.133 ms. Measured total
turn allocations 384/480 bytes with no collections. This is .NET 9 managed-only
traversal: Skip uses a trivial producer, Observed256 measures direct managed evidence
latching/validation. Neither measures Mono/native host reads. Both preserve zero
public tokens throughout. Earlier pre-admitted benchmark lanes did not measure
first-pass token growth and are not used to justify the corrected hot path.

Exact-host structural checker: 35 checks, pinned images/root/prefab fields/capped read shape.
Full Windows real pinned compiler/native runtime regression passes GUI, Player,
publication/modules, provider/addon, scheduler/recovery, packaging and persistence
worker paths, including 2,000 reads and lost-ack/hang cases. Actual production
Main/Persistence adapter teardown tests pass both scheduler-failure paths with zero
reservations/callback roots/thread/native mappings. API/architecture/release/tooling
checks remain mandatory after final docs. Hosted Windows/Linux/runtime/sanitizer
workflow includes the new managed models; its final receipt is appended below.
No custom Unity native sanitizer instrumentation is claimed: no Unity/native code
was changed, and UnityPlayer is not rebuilt with CarbonLuau's sanitizers.

Independent review: fixed-depth arithmetic/native reader reviewed; a too-short job
witness was corrected and rerun. Shared NextFrame queue contention/resizing was
identified and replaced by one fixed receiver, then rerun live on both hosts. The
fixed receiver teardown/owner ordering was reviewed without further findings.
Final resource review identified proxy-table/queue growth hidden inside first-pass
TryAdmit. Discovery now observes direct canonical candidate state, latches exact
ID/prefab and never grows shared token storage. Corrected deterministic/full-catalog
and both live runs pass. Cold prefab field resolution initially failed against
Carbon's publicized image; fail-closed receipts `discovery-20261004-052350`,
`-052358` and `-052606` remain. Visibility-compatible exact-field resolution was
corrected, not treated as a reason to weaken host identity or skip candidates.
Independent review of the final direct observer, post-position/result validation
and capped cold manifest path at `527f3fb` found no further concrete regression.

## History, identities and handoff

### Final-source hosted closure

Implementation/tested production revision:
`527f3fbcd486a549be7ba89ed7f839b1eee55185`.
The evidence revision carrying this final section changes only this record,
closure routing and the isolated cold-prefab live fixture; production code is
identical. Both final live receipts above execute that exact production source.

- [CarbonLuau validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37192588981)
  **PASS**: Windows x64 and Ubuntu 24.04 native/runtime/worker/import/export,
  lifetime/discovery/composition models, adapter teardown, combined persistence
  scale, deterministic package and clean extraction/install gates; ASan, UBSan,
  leak detection and lifecycle/fault fixtures also **PASS**.
- [Tooling Foundations A/B](https://github.com/gmoddev/CarbonLuau/actions/runs/37192589138)
  **PASS**, including metadata/definitions/preview drift regressions.
- [Documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/37192589129)
  **PASS**. Final evidence publication uses the same normal Pages workflow.

Local architecture/API/release/tooling/package and 35-check exact-host structural
audits pass. Final review reports no unresolved safety/resource finding. Test
servers/processes and fixture listeners exited; packages/task config/hooks were
restored. Reusable task evidence/build caches are retained. The tracked worktree
is committed; unrelated pre-existing `derived-369547800317500/` research databases
and `.codexlock` remain untracked and untouched. No credentials, binaries, test
database or journals enter these commits. Git author/committer use GitHub no-reply.
No mandatory private-substrate gate remains open. Public completion/proxy
conversion/metadata remains separately scoped, not silently qualified here.

[Foundation 2](WorldEntityFoundation2.md) routes all retained negative evidence:
movement/history publisher completeness, unsafe host enumerator, guarded getter,
phase/job snapshots and prior position alternatives. None is rewritten as PASS.
The new observation boundary changes the required consistency theorem, not Entity
incarnation safety. Initial-startup-only Entity observer deployment remains.
First-time hotload/full unload-reload cannot manufacture lost Spawn history.

Package `0.5.0`, development scripting API `0.6.0-experimental`, ABI `1.5`, provider
`1.2`, schema `1` and Luau pin are unchanged. No public discovery definition,
release/tag, World mutation or movement index was added. Next permitted task:
separately scoped simple callback-based public discovery bindings over this exact
substrate, with bounded value/result conversion, committed authority/completion
admission, retirement/recovery tests and canonical metadata. No generic query DSL.
