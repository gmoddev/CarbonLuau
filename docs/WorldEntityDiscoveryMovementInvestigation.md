# Complete discovery — owned index movement investigation

Status: **NEEDS REVIEW — no production discovery/index implementation.**
Complete discovery is required; network-group subsets are rejected. This
record supports [Foundation 2 research](WorldEntityFoundation2.md), not a new
runtime invariant or an amendment to qualified [D20](Invariants.md#d20--worldentity-foundation-1).
Starting and current source is `f36ff7c83a9e273816f7b726fe3062618b430539`.
Existing uncommitted research and unrelated scratch state are preserved.

## Exact inputs

The disposable Windows host is the existing qualified Rust build `25653776`,
protocol `2634.289.1`, Windows Carbon `2.0.262.0` / `8a81d70`, Harmony
`2.4.2.0`. Foundation 1 owns its full pin and deployment restrictions.

| Inspected binary | SHA-256 |
|---|---|
| Assembly-CSharp.dll | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` |
| UnityEngine.CoreModule.dll | `93b0e7e8e1b9a82f34d09740c45be2cbd7c13a82683a192af2858831b63ce45a` |
| UnityPlayer.dll | `6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a` |

CoreModule MVID is `dce824bf-6f64-481d-908e-6fe02c35a666`. The UnityPlayer
hash identifies the native runtime used for these measurements; it is not
proof of native implementation complexity or an adopted compatibility pin.
The initial investigation made no Linux movement measurement or native
sanitizer claim. The follow-up below records new Linux measurements separately.

## Rust's own tree is not a fresh discovery source

`BaseEntity.Query.Server.EntityTree` owns Spatial.Grid instances for entities,
players and brains. Its Move method reads root `transform.position` and moves
the entity grid's XZ entry. Radius methods query those cells before their
optional center/bounds narrow phase.

Exact IL: `TransformChanged` (`0x1555`) calls the tree Move; `NetworkPositionTick`
(`0x1554`) checks `hasChanged`, invokes TransformChanged, then clears the flag.
`ToggleNetworkPositionTick` (`0x1553`) schedules position ticks when enabled.
`set_ServerWorldPosition` (`0x1571`) writes Transform.position/hasChanged but
does not synchronously move the tree. `BasePlayer.MovePosition` is an important
exception: it calls NetworkPositionTick synchronously. Do not generalize the
delayed box case to that player method.

The fixture moves a fully-spawned, keyed BoxStorage from `(-300,100,-300)` to
`(300,100,300)`. Querying the tree around its new live root position returns no
such object until an explicit position tick. Moving a parent to `(0,100,0)`
and ticking only that parent leaves the child at live `(2,100,0)` but absent
from its new query cell until the child's own tick. Direct Transform.position
also produces the delayed-tree result. These are concrete completeness
counterexamples, not an inferred timing risk.

Managed reference inventory found 264 direct Transform.set_position
instructions in 204 methods, 83 set_localPosition instructions in 66 methods,
and five Rigidbody.MovePosition instructions in four methods. These are
case-insensitive Cecil reference matches against exact full type/method names;
they include non-server/non-root paths. They neither enumerate all admissible
root movement nor cover native physics, hierarchy propagation or jobs.
Patching TransformChanged or a short list of teleports is not a coverage proof.

## Native ObjectDispatcher feasibility

Pinned CoreModule contains an **internal**, sealed `UnityEngine.ObjectDispatcher`.
Research reflection is confined to the isolated plugin; nothing is exposed to
Luau. Tracking modes are GlobalTRS, LocalTRS and Hierarchy. The inspected wrapper
validates a Component-derived type, then enters native code. Enable/disable
accept types, not individual entity instances. The enumerated dispatch/get
signatures have no capacity or resumable bounded-drain argument.

Tracking `BaseEntity` alone does **not** report the fixture BoxStorage. Tracking
its exact concrete type does. A same-assembly BaseEntity ancestry inventory
finds 682 types, 672 nonabstract/nongeneric types. That is an assembly inventory,
not an exhaustive runtime-registration proof: external/dynamic types and
inherited implementations must be handled fail-closed. Foundation 1's 24 Spawn
method bodies are not the concrete-type count.

The Component-array overload (`0x2098`) receives a native-generated array.
Rejecting its length afterwards cannot bound preceding native work/allocation.
The allocator Get overload (`0x2099`) copies the returned native arrays in
`DispatchCallback` (`0x2090`), so it also cannot establish pre-copy rejection.

The callback overload (`0x2094`) instead enters native dispatch (`0x20b5`) and
its pinned bridge (`0x20bb`) wraps six existing native buffers as NativeArray
views before invoking the supplied callback. No data-copy constructor is in
that bridge. The fixture's generic reflection callback inspects transformed-ID
length during the borrow, never retains/disposes/enumerates the views, and
does not call the copying Get overload. Native collection before the callback
is still opaque; a count available there is **not** a bound on that work.

Unity's [current C# reference](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Misc/ObjectDispatcher.bindings.cs)
corroborates the overload distinction and internal status. It is a different
revision, not evidence replacing pinned IL. Neither this source nor the
inspected wrappers establishes native queue size, history-loss behavior,
unchanged-population cost or synchronization with transform jobs.

## Live observations and assertion scope

Research files:

- `tests/live/CarbonLuau.WorldMovementResearch.cs`
- `tools/Test-WorldMovementResearchWindows.ps1`
- `tools/Research-WorldDiscoveryHost.ps1`

The fixture owns three wooden boxes and one naturally dropped wood item;
all have saving disabled and are killed during cleanup. It uses no authenticated
player. The server binds loopback only, with no RCON. The runner refuses another
RustDedicated process, launches hidden, and removes only its unique plugin.
Each run exits gracefully; a timeout stops only its own process. No production
index, Harmony movement patch or API metadata is installed.

| Movement | Exact-type component dispatch | Borrowed BoxStorage count |
|---|---|---|
| ServerWorldPosition, before position tick | BoxStorage found immediately | 1 |
| Reparent | Child found immediately | 2 |
| Parent translation | Child found immediately | 2 |
| Parent rotation | Child found immediately | 2 |
| Parent scale | Child found immediately | 2 |
| Child localPosition | Child found immediately | 1 |
| Unparent | Child found immediately | 1 |
| Direct root position | BoxStorage found immediately | 1 |
| SetPositionAndRotation | BoxStorage found immediately | 1 |
| 100 successive root writes, one drain | BoxStorage found; one record observed | 1 |
| Real DroppedItem native physics, after 0.25-second timer | DroppedItem found; displacement approximately `(1.23,-0.38,0)` with Rigidbody present | 0: this independent borrowed stream tracks BoxStorage only |

Tracking all Transform objects returned six changes for a single fixture box
and 922 changes at the later physics observation, versus one DroppedItem record.
This illustrates irrelevant-object/child amplification, not a maximum. Exact
type filtering is promising; it is not per-admitted-instance filtering.

Final assertion-bearing/borrowed-callback run:
`D:\Sandbox\Codex\Entity1AStartup\evidence\movement-20261003-161220\server.log`.
The exercised fixture SHA-256 is
`6054c52d4af92fada9bf99255648538176305bd875a1ad09fe7cf4fe71c9b609`.
Earlier receipts remain preserved at `movement-20261003-155807` (host-tree
counterexample), `160416` (initial dispatcher), `160701` (expanded movement),
and `161010` (exact-type assertions). Early runs logged dispatcher observations
without asserting them. Final code requires exact-type target presence for
each tested move, borrowed counts for the matching BoxStorage stream, and
actual physics displacement. Its PASS means **these probes passed**, not that
all movement/index/resource gates passed. Borrowed count checks do not prove
every returned native ID is the expected lifetime.

## Exact remaining theorem

Before any successful radius query, every current D20-admissible entity must
occupy the cell containing its current root position, and obtaining that
freshness must have independently proven hard work/memory bounds. No prior
tick snapshot, partial dirty prefix or live filtering of stale cells suffices.

The remaining obligations are:

1. Exhaustive movement delivery for all qualified exact types: native physics,
   sleeping/waking, inactive/disabled cases, ancestor changes, TransformAccess/
   native jobs, NavMesh and entity-specific paths. The current cases establish
   feasibility, not an exhaustive theorem.
2. Native collection/history/resource bounds, including irrelevant/unspawned
   instances of tracked types, asset behavior, hierarchy fanout, many unchanged
   objects, lost history, repeated changes and shutdown. `maxDispatchHistoryFramesCount`
   bounds a frame policy, not a demonstrated entry/byte limit. There is no
   inspected pre-dispatch count/cap API. Stopping managed enumeration late
   cannot establish I8.
3. A qualified freshness cut across every tracked type and native transform
   job; no current mutation may occur after its type was drained and before
   publication without being incorporated or making the query fail.
4. Gap-free startup seeding and successful-Spawn membership, plus temporary
   keyed-registry absence/reinsertion. Foundation 1 completion history alone
   does not seed a spatial index. Unity EntityId routing must resolve current
   weak managed identity/epoch, never retarget a lifetime after ID reuse.
5. Continuous bounded slots/buckets/dirty records and safe recovery from any
   lost delta, type drift or partial update. Foundation 1's 262,144 startup
   cap does not bound all ongoing tracked native objects. An uncertified index
   must reject even an apparently empty query.
6. Windows/Linux native equivalence, dense-world memory/work/timing, disposal
   and weak-retention convergence. These are not newly measured here.

Independent read-only review confirmed these gates and found that the initial
fixture's logged observations were not assertions. That finding is resolved in
the final probe, not retroactively applied to the early receipts. The review
also identified exact-type coverage, bootstrap and keyed-reinsertion hazards,
which remain explicitly open rather than hidden in a grid implementation.

## Disposition

The conditional sparse XZ design in Foundation 2 remains the smallest candidate.
Movement is promising enough to continue narrowly with the borrowed native
change stream; it is not yet proven safe to implement. A timed batch experiment
may corroborate a native-source theorem but cannot replace it. If this stream
cannot supply bounded freshness, research an exact-host bounded notification/
count/drain mechanism. Do not restore network subsets, spawn-only membership,
query-time world scans or silent truncation.

D20, production runtime, public metadata, versions and release identities are
unchanged. No Discovery-2A or later foundation work began. No commit/push is
made while the research baseline is internally unresolved.

Repository API/link, architecture and release-contract checks passed after the
research edits; tracked whitespace checking passed. These lightweight checks
are not native movement qualification or hosted CI. Read-only final worker
inspection found no RustDedicated process and no remaining research plugin at
its unique installed path. The server/evidence sandbox is retained for further
research; no unrelated files or processes were removed. The runner's final
cleanup-report addition follows the live receipt and changes no tested fixture
behavior. The main worktree remains intentionally uncommitted, including the
prior research; `.codexlock` and unrelated derived scratch state are preserved.

## Movement-proof follow-up — 2026-10-03

**WORLD/ENTITY FOUNDATION 2 NEEDS REVIEW — native collection/history bounds
and an exhaustive current-position freshness cut remain unproven.** This is
not proof that complete bounded discovery is nonviable. Complete discovery
remains required. The current task explicitly authorizes private Discovery-2A
after this proof passes; the conditional implementation gate has not passed.
No grid is shipped around an uncertified movement source.

### New platform evidence and scope

The fixture now also asserts immediate exact-type delivery for disabled
BoxStorage components, inactive GameObjects and grandparent translation,
rotation and scale. These objects remain fully spawned and keyed while disabled
or inactive; excluding them would weaken D20 inclusion. Three boxes and one
naturally dropped wood item remain the owned fixture population. No Player,
vehicle, building block or exhaustive NPC movement qualification is claimed.

The alternative seam adds a research-only `MovementMarker : MonoBehaviour` to
each owned root and tracks that one exact component type. After clearing
creation records, it asserts exact marker presence independently for each
direct/local/hierarchy/inactive movement case. The native physics assertion
also clears marker creation first, requires actual displacement, then requires
the exact physics marker in the next dispatch. This avoids treating component
creation as proof of movement. It is a feasible consolidation of types, **not
an adopted production host-component dependency or a native work theorem**.

Default `maxDispatchHistoryFramesCount` was 64 on both hosts. Setting it to 1
still left the box's undrained change present after many actual Unity frames.
In the clean production-package observations, it remained present after 39
Windows / 24 Linux frames; independent earlier corrected runs observed 38 / 51.
The copying result contained that exact BoxStorage and the independent borrowed
callback reported one ID. The record was not drained by the physics-only
stream. This rules out treating the setting as a one-frame expiry guarantee
for these pending records; it proves neither unlimited retention nor a maximum
pending entry/byte count. History observations are reviewed log evidence;
immediate movement and physics-marker assertions govern the fixture verdict.

The Linux 0.25-second first observation initially occurred without physics
displacement. The fixture now waits in bounded 0.25-second intervals (at most
21 observations) and still fails if displacement or exact delivery is absent.
It does not manually simulate physics, wake/mutate the body or fabricate a
physics result. The successful clean-package observation recorded approximately
`(1.23,-0.38,0)`; Windows recorded `(1.53,-0.57,0)`. These are observations,
not per-step delivery, sleep/wake or timing guarantees.

The final fixture digest is
`ac171b5b46171ba341d9e13c365ccad58a34874c9c28dd2718aecd1cefd8e36a`.
A clean default source package was generated from unchanged production source
at `f36ff7c83a9e273816f7b726fe3062618b430539`; its SHA-256 is
`6cb5d7f6e74ca49809aeb87df7d79d416eb0b593206120fcbbe18e66ee552366`.
It contains no Entity test partial class. The runner temporarily installs it
and restores the task server's prior package afterward. Earlier receipts used
the preserved Entity-1A test package and are not clean production-package evidence.

Linux clean-package receipt:
`/root/codex/world-movement-20261003/evidence/movement-20261003-170045/server.log`,
SHA-256 `740a040a8878306b8e288e81a9829a65f5ccb8821a440b701296137b07437b82`.
It logged the pinned Carbon target and qualified Entity startup observer
with 978 keyed completions. The independent marker stream reported one exact
physics marker, with four tracked roots. Direct/local, parent and grandparent,
disabled/inactive, repeated writes, physics and borrowed callback probes passed.
This is not Discovery-2A qualification or a dense-world completeness oracle.

Windows clean-package receipt `movement-20261003-170034/server.log` passed the
standalone movement assertions, but the production observer rejected newly
updated Carbon hooks. Oxide SHA-256 was
`3f6cf14e2b87a59fb4afcfcbf34590895ba03245cb1e8858c18a0fbb8664cc80`;
Community was `1512cdfe929bfc8508272206fd5e2e83199477ed9d87533b25641285c0c24ab5`.
This is drift/fail-closed evidence, **not integrated pinned-Entity evidence**.
The prior accepted hook pair was located in the preserved Persistence-2D task
server for a separately recorded scoped rerun. No new hook identity is adopted.

Windows qualified-pair clean-package rerun:
`D:\Sandbox\Codex\Entity1AStartup\evidence\movement-20261003-170625\server.log`,
SHA-256 `ceb8c6b0db4d2e88740b82d65b54a817acc918f46756f2c4b74e674934105437`.
The production startup observer qualified with 990 keyed completions. All
immediate movement assertions, exact marker assertions, actual physics and
borrowed-length probes passed. The undrained BoxStorage change remained after
39 Unity frames with the history setting at 1; the all-Transform stream returned
928 entries. This is exact-host integrated probe evidence, not a native bound.
Only the disposable task server temporarily used the already accepted historical
Community/Oxide pair and corresponding Base hook assembly. Its updater was
disabled for this run only. After exit, read-only hash comparisons confirmed all
three prior hook files and the prior Carbon configuration restored byte-for-byte;
the prior package was restored to
`c07607566ea35abfcacc7f1c371f8b6d0a90ff82205dc2bb880b2776c454d17b`.
The unique research fixture was removed and no research server remained.

Two failed first receipts (`Windows 164825`, `Linux 164907`) are preserved:
the research setter passed UInt32 to the actual Int32 history property.
The probe was corrected, not production. Windows `164931` exercised movement,
but an earlier all-stream drain invalidated its history experiment; its empty
history observation is not evidence of expiry. The corrected history experiment
and subsequent marker assertions were rerun on both hosts. Linux `165124`
records the initial physics-observation timing failure; `165415` and `165732`
passed after the bounded wait. None is rewritten as a theorem or final index PASS.

### Additional exact identities

Rust build/protocol and Carbon/Harmony support targets remain those above.
Unity reports `6000.3.15x1-13`; Windows native identity inspection additionally
found revision `a91cf34396ee`. Linux runtime identity is separately hashed,
not inferred from Windows.

| Linux movement binary | SHA-256 |
|---|---|
| Assembly-CSharp.dll | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| UnityEngine.CoreModule.dll | `ada97d7037c7c928d8d432f734115d82a3d805a2da628f481901da5d165795e2` |
| UnityPlayer.so | `ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1` |

`tools/Research-UnityNativeSymbols.py` inspected the exact Linux ELF. It found
only the dynamic symbol table (8,760 bytes), no static/debug symbol sections,
and zero named ObjectDispatcher/TransformDispatch symbols. That explains why
ordinary named native inspection did not supply the missing bodies; it does
not prove native behavior unsafe or rule out deeper binary analysis.

### Native proof and alternatives review

Pinned IL supplies additional negative capabilities:

- `valid` (`0x2085`) checks only that the native pointer is nonzero. It is not
  a loss/overflow/continuity certificate.
- History access (`0x2086–0x2087`) enters native getter/setter
  `0x20ae–0x20af`; there is no managed entry/byte policy.
- Enable (`0x209e`) loops registered types, not independently capped instances.
- Borrowed dispatch (`0x2094`) enters `0x20b5` before lengths become visible.
  Its bridge has no inspected bounded-drain, precollection count, overflow flag,
  continuation or all-writer barrier. Six borrowed views are wrapped without
  copying; the probe inspects only their lengths within the callback.

The published [Unity bindings at a fixed reference revision](https://github.com/Unity-Technologies/UnityCsReference/blob/88ce7b60434ba7a8ca0218590a4cb509971788ad/Runtime/Export/Misc/ObjectDispatcher.bindings.cs)
route collection to `Runtime/Misc/ObjectDispatcher.h`, not published C# bodies.
That reference is supporting evidence, not the exact target's native source.
Independent review found no primary native implementation establishing hard
collection/history bounds or writer synchronization. Callback-length rejection
cannot bound work already performed before the callback.

| Reasonable alternative | Investigation result |
|---|---|
| Owned marker / one exact tracking type | Newly tested on both platforms. Removes entity-type drain multiplicity and could bound CarbonLuau-owned marker population. Still depends on opaque native collection, history/churn and hierarchy work. A marker cap is not yet a dispatcher resource proof. |
| Track all Transform objects | Newly reproduced 928 changes in the delayed observation versus one exact box change. Includes unrelated children/world state; no independently controlled population or precollection bound. |
| Poll root `hasChanged` / current positions | Requires population traversal to find moved-in entities. Rust resets `hasChanged` at NetworkPositionTick; a shared flag is not an owned event queue. No query-time world scan is permitted. |
| Parent/children notifications | Hierarchy-change notifications are not general root/ancestor translation, rotation, scale or physics movement notifications. Descendant expansion still needs a hard bound. |
| Managed setter/teleport/TransformChanged patches | Cannot cover native physics or all ancestor/engine writes. The host tree's actual stale-child and teleport counterexamples remain. No broad native detour is introduced. |
| Physics contacts / active actors | Do not cover colliderless, noncontact and nonphysics roots. No active-actor getter was found in the pinned managed Physics signatures. |
| Physics.SyncTransforms | A physics synchronization operation, not an exhaustive root-change stream or general transform-writer barrier. [Unity reference](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.SyncTransforms.html). |
| Known JobHandle completion / PlayerLoop phase | Completing supplied jobs does not certify all native writers. An empty ObjectDispatcherPostLateUpdate marker supplies no inspected synchronization body. A sequential all-type drain can be correct under proven quiescence; such quiescence is not yet established for arbitrary allowed queries. |
| Newer transform hierarchy dependency/queue helpers | Present in newer Unity reference, absent from the pinned CoreModule inventory. They cannot be silently borrowed from another runtime. |
| Network groups / Vis / all-world reconciliation | Already rejected as complete semantics or hard-work mechanisms. They do not become fallbacks because the dispatcher proof is difficult. |

The [documented hierarchy capacity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Transform-hierarchyCapacity.html)
grows when exceeded; it is not a hard descendant cap. Neither adding markers
nor setting history frames proves that native work scales only with bounded
admitted roots rather than irrelevant objects/hierarchies or retained churn.

The exact outstanding gate requires source and/or defensible exact-binary
evidence for: finite pending bytes/entries, allocation/coalescing/loss rules,
detectable exhaustion, bounded work before the callback, and completion of all
relevant root writers through the query cut. Supported host callbacks or an
engine-owned bounded adapter could also close it. No adequate such primitive
was established by the inspected available seams. Scaling a benchmark or
relaxing I8 is not a substitute. This is the task's movement/resource review
stop condition, not a request for routine implementation permission.

### Required completion ledger

| # | Requested item | Result |
|---:|---|---|
| 1 | Verdict | NEEDS REVIEW: hard native bounds and exhaustive freshness unproven; not a nonviability finding. |
| 2 | Starting commit | `f36ff7c83a9e273816f7b726fe3062618b430539`; preserved intentionally dirty research checkout. |
| 3 | Rust target | Build 25653776, protocol 2634.289.1; Foundation 1 owns revision 166494. |
| 4 | Carbon targets | Windows 2.0.262.0 / 8a81d70; Linux 2.0.261.0 / c74c4ca; Harmony 2.4.2.0. Windows hook drift separately recorded, not adopted. |
| 5 | Unity assemblies | Unity 6000.3.15x1-13; both platform CoreModule/native digests above. |
| 6 | Change mechanism | Internal GlobalTRS exact-type dispatch and research-only one-type marker alternative. |
| 7 | Completeness proof | Exercised cases pass; exhaustive native delivery theorem not established. |
| 8 | Type coverage | BoxStorage and DroppedItem measured; all 672 concrete inventory types, Player/NPC/vehicle/building/custom/load cases not exhaustively qualified. Marker feasibility does not erase this gate. |
| 9 | Parent/ancestor | Child delivered immediately for parent and grandparent translation/rotation/scale on both platforms; no arbitrary-hierarchy work theorem. |
| 10 | Physics | Actual dropped-item movement and exact component/marker delivery pass on both platforms; per-step/sleep/wake completeness unproven. |
| 11 | Freshness model | Flush-before-query candidate; writer quiescence and full-cut delivery unproven. No per-frame stale fallback. |
| 12 | History hard bound | Not established. Frame setting 1 does not expire the measured undrained record within one frame. |
| 13 | Overflow | No inspected precollection cap/loss certificate. Proposed discovery must disable on uncertified freshness; not implemented. |
| 14 | Borrowed data | Length-only callback within borrow tested on both; no retention/disposal. Element iteration, nested invalidation and native lifetime/resource theorem remain unqualified. |
| 15 | Spawn integration | Existing qualified 1A retained; no spatial membership integration implemented before proof. |
| 16 | Retirement integration | Existing sticky lifetime retirement retained; no second index/removal model introduced. |
| 17 | Grid | Sparse XZ + intrusive raw-counted cells remains conditional candidate; not implemented. |
| 18 | CellSize | Not selected or measured; no numeric policy adopted. |
| 19 | Exact 3D filter | Proposed current-root finite squared-distance test; not implemented as discovery. |
| 20 | Query work | Required bounded flush + bounded cells + capped raw candidates + exact validation + capped results; flush term remains unproven. |
| 21 | CandidateLimit | No number adopted; raw-count overflow must reject before iteration. |
| 22 | Memory | 1k/10k/100k grid/native history byte bounds not measured; four fixture markers are not a production memory theorem. |
| 23 | Bootstrap | Foundation 1 baseline retained; spatial seed/continuous bounds not implemented or qualified. |
| 24 | Dense/high-motion | 100 coalesced writes, three-level hierarchy and physics measured; dense/churn-scale qualification not performed before prerequisite theorem. |
| 25 | Oracle | No production candidate index; no scan-oracle index completeness result claimed. |
| 26 | Windows | Clean-package qualified-pair integrated probes pass (`170625`); earlier `170034` observer drift rejection preserved. No complete-discovery/resource theorem claimed. |
| 27 | Linux | New clean-production-package live probes pass; qualified startup logged. No inference from Windows. |
| 28 | Sanitizer/leak | No native production changes; no new ASan/UBSan or index/native-history leak proof claimed. Disposal/process cleanup is separately checked. |
| 29 | Drift checks | Existing Foundation 1 fail-closed behavior observed; research Cecil/ELF inventory only, no newly qualified runtime movement checker. |
| 30 | Canonical update | Research/routing evidence updated; D20 not amended without proof. Complete semantics and rejection of network subsets preserved. |
| 31 | Discovery-2A | Not implemented: prerequisite proof failed to close. |
| 32 | Commits | None. No production/correction commit. |
| 33 | Final tested source | Uncommitted fixture digest and unchanged production package digest above; not an invented tested Git commit. |
| 34 | CI/checks | API/link, architecture, release and whitespace checks rerun after evidence edits; hosted CI not run/published without PASS. |
| 35 | Network subset API | None implemented or adopted. |
| 36 | Later features | No public discovery, Signals, Spawn/Destroy, Position writes or global prefab enumeration. |
| 37 | Identities | Package 0.5.0; development API 0.6.0-experimental; ABI 1.5; provider 1.2; schema 1; Luau pin unchanged. No release/tag. |
| 38 | Historical evidence | Initial discovery, earlier negative proofs and all failed/successful probe receipts preserved with their limitations. |
| 39 | Commit/push | Not performed while proof gate is unresolved. |
| 40 | Worktree | main at starting commit; existing research, `.codexlock` and unrelated derived scratch preserved; new probes/docs intentionally uncommitted. |
| 41 | 2B handoff | Not authorized by this result. First close native resource/freshness proof, then qualify private Discovery-2A; do not start 2B. |

New runner `tools/Test-WorldMovementResearchLinux.py` stages only its unique
fixture in an isolated exact-host copy, binds loopback, starts one owned
process group, and removes the fixture/restores its prior package after exit.
The copied server borrows read-only asset links from the preserved task asset
installation; no source asset files or other servers were changed. Both worker
evidence sandboxes remain available for further research. No running research
server or installed research fixture remains after the completed runs.

## Narrow native-history and freshness investigation — 2026-10-03

This continuation keeps complete discovery and the sparse-grid candidate
unchanged. It investigates only the producer-to-query notification path,
upstream resource bounds and writer synchronization. Earlier statements that
the native bodies were unavailable describe the earlier evidence state;
read-only runtime resolution and offline machine-code inspection now establish
the concrete paths below. This does not qualify a production native adapter.

### New final-fixture receipts and scope

The research fixture is SHA-256
`dd8cfd463f8a055cba396d37c8b82757672b0786df48aac9d6522fb032124c92`.
Both hosts used the same unchanged clean production package identified above.

| Host | New receipt | SHA-256 | Qualified startup |
|---|---|---|---|
| Windows | `D:\Sandbox\Codex\Entity1AStartup\evidence\movement-20261003-172645\server.log` | `ad0946b63bc9ef170d1d8737518844d9ee25a5b43dfba14c1c97a78c833fcdb7` | 992 keyed completions |
| Linux | `/root/codex/world-movement-20261003/evidence/movement-20261003-172635/server.log` | `862693f1be160a4dad3bf1684f7ecfddcda4723605fd0d289715dadc212f1272` | 979 keyed completions |

Both reruns passed the existing direct/local, ancestor, disabled/inactive,
marker, actual-physics and borrowed-length assertions. The undrained exact box
record remained after 39 Windows / 34 Linux frames with history setting 1.
Both logged `PRODUCER_CALLBACKS directMove=0` and
`PRODUCER_CALLBACKS betweenDrains=0`. Every borrowed callback occurred only
inside the explicit owner-thread drain. These observations agree with the
native call order; they are not a universal loss/freshness theorem.

A further Linux-only resolver extension added Physics methods without changing
production: fixture SHA-256
`806e2d16b41e5f4a0f9cb27a6378481548c9e44e86db2a9b9c5fb5943eb80995`,
receipt `/root/codex/world-movement-20261003/evidence/movement-20261003-174107/server.log`,
SHA-256 `2d0554fc2e670155fcc02cd00047d6593e97bd761cc5616d92f0d248a33a654e`.
It again passed movement assertions, with 979 startup completions and the exact
box change retained after 30 frames. Physics `Simulate_Internal_Injected`
resolved to `0x840f00` → `0xd83f90`; `SyncTransforms` to `0x840f90` → `0xd84990`.
The Windows receipt above remains evidence for the earlier fixture digest,
not a fabricated test of this Linux resolver extension.

Linux additionally resolved the existing Mono internal calls through
`mono_lookup_internal_call`, checked method names, and identified the containing
ELF through `dladdr`. The probe did not execute the resolved functions, replace
icalls, dereference native Unity objects, or install a native patch. The
research-only resolver is not a new production dependency.

`tools/Research-UnityDispatcherNative.py` reads ELF bytes and uses the existing
bundled Capstone dependency. It never loads Unity. Disassembly is capped at
32,768 bytes per requested window. Its raw direct-call and seven-byte
RIP-relative MOV/LEA searches emit **candidates**, not an exhaustive call graph
or proof of instruction boundaries. Findings below were followed through
actual function bodies; a raw search hit alone is not evidence of coverage.

All native addresses below are image virtual addresses in the exact Linux
`UnityPlayer.so` hash recorded above, not ASLR-dependent runtime pointers.
Windows live evidence is separately measured; these Linux body findings have
not been independently mapped to Windows machine code.

### Exact native paths

| Operation | Runtime-resolved wrapper | Inspected core |
|---|---|---|
| Create dispatcher | `0x7f84d0` | `0xc45fd0` / `0xc46030` |
| Get/set history frames | `0x7f8500` / `0x7f8520` | `0xc474b0` / `0xc474f0` |
| Enable transform tracking | `0x7f8600` | `0xc470f0` → `0xd13250` → `0xd219e0` |
| Lifecycle/type drain | `0x7f86c0` | `0xc47530` |
| Borrowed transform drain | `0x7f8790` | `0xc48990` → `0xc48ee0` → `0xd14610` |
| Component-array transform drain | `0x7f8840` | `0xc49330` |
| Position read | `0x8023d0` | `0xd14e40` |
| World position write | `0x802400` | `0xd19610` → `0xd19650` |
| Local position write | `0x802440` | `0xd18ff0` → `0xd19030` |
| Read-only transform-job schedule | `0x7d58b0` | `0xc0ce50` |

### Lifecycle history is not the movement dirty container

Independent read-only inspection distinguishes the two mechanisms:

- Per-dispatcher records have stride 40 bytes; the history setter stores signed
  `max(Frames,1)` at offset `+0x20` (`0xc47507–0xc47520`). No inspected upper
  item/byte cap is established by that setting.
- Shared per-type lifecycle history uses two hash indexes and growing rings of
  four-byte / eight-byte entries. Rings grow rather than overwrite on fullness;
  helpers `0xc4fb70` / `0xc4fc90` double capacity at `0xc4fbab` / `0xc4fccb`,
  initially 16 entries.
- Lifecycle records coalesce by instance ID (`0xc45c10`), cancel corresponding
  records across histories (`0xc45960` / `0xc45de0`), and reclaim consumed entries
  using subscriber cursors (`0xc47893–0xc47c16`).
- Frame cleanup compares age against the history limit (`0xc456ff–0xc4570f`);
  exceeding it disables lifecycle tracking via `0xc46570`. Equality survives.
  Last-subscriber removal releases the shared hash/ring storage.
- That age path uses the lifecycle active byte at subscription offset `+8`.
  Transform tracking instead uses three interest handles at
  `+0xc/+0x10/+0x14`. Lifecycle expiry must not be presented as a transform
  pending-byte bound or transform continuity certificate.

Thus the long-delayed transform observation is no longer explained by an
assumed lifecycle ring. No inspected ring growth/expiry behavior supplies the
required hard bound for complete movement discovery.

### Transform dirtiness, allocation and pre-callback work

The inspected world/local position setters first wait on an existing hierarchy
job handle where present, update TRS, and OR relevant interest bits into node
and hierarchy dirty masks. They propagate applicable bits through the affected
subtree. World-position propagation is at `0xd19730–0xd19758`; local-position
propagation is at `0xd190e0–0xd19104`.

Each inspected producer coalesces an already-pending hierarchy through its
stored pending index; otherwise it appends a 40-byte hierarchy record inline.
For example `0xd1977f–0xd197f8` covers update/append. Pending storage grows via
`0xd28630`, which doubles capacity and allocates/reallocates native storage.
This coalesces pending-list entries by distinct hierarchies, **not** by
CarbonLuau's marker count or history frames. It does not bound repeated
write-time subtree work, retained allocation capacity, or establish a hard byte cap.
No overwrite/drop branch was established in these inspected append paths;
that is not an exhaustive no-loss proof for all producers or allocation failure.

The transform-interest allocator has 64 registration bits (`0xd219e0`). This is
a subscription count, not a limit on transforms, hierarchy nodes or history.
No synchronous callback pointer is stored in this interest registration path.

The borrowed core calls its collector **before** invoking the managed delegate.
Collector `0xc48ee0` invokes `0xd14610`, grows output buffers, then runs the
type-filter/TRS job `0xc4e510` and waits for it. Six temporary native views are
prepared before callback entry and disposed afterward. Limiting copied IDs or
setting a dirty bit in that callback cannot bound this earlier work.

`0xd14610` invokes pending enumeration `0xd22070`. Its concrete costs include:

1. scratch hash/vector capacity derived from the global pending-hierarchy count;
2. traversal of that pending list, including entries that fail the requested mask;
3. matching-hierarchy tasks partitioned into chunks of 8,192 nodes;
4. job `0xd22880` visiting hierarchy nodes and gathering those with selected bits;
5. output-buffer growth before managed callback admission;
6. component/type filtering and ancestor-chain TRS evaluation in `0xc4e510`.

The chunk size bounds one partition, not the number of partitions or total work.
For a successful drain, hard bounds are still needed on global pending records,
matching hierarchy nodes, component-filter work, ancestor depth and participating
job/dependency work. A cap on admitted Entity markers alone does not establish
those bounds. The normal position getter also walks ancestor links
(`0xd14ec0–0xd14f68`); grid candidate counts alone do not bound that native term.

### Synchronization through the query cut

The read-only scheduler is a narrower useful mechanism, not a proven global
barrier. `0xc0ce50` prepares the supplied TransformAccessArray (`0xd292f0`),
collects handles from its represented hierarchies (`0xd2ab40` → `0xd2a9e0`),
combines them with `dependsOn` (`0xc009d0`), and schedules/executes read-only
work (`0xc0c590`). It records the resulting handle on those hierarchies
(`0xd2abd0` → `0xd23800`). Nonzero scheduling mode carries dependencies forward;
scheduling alone does not complete them. Zero mode waits via `0xc044c0`.

The inspected wait chain follows the supplied dependency graph and may execute
work/retry. It does not by itself prove all native position producers enroll,
that their completion includes dirty publication, or that no writer can enter
between the barrier and the query. The movement collector similarly waits on
recorded dependencies, not an established global all-writer exclusion scope.
Existing same-turn synchronous setter observations remain valid but cannot
stand in for this all-writer theorem.

Additional dependency enrollment is concrete: function `0xb940e0` selects
producer-owned entries, schedules `0xb98f60`, and records the handle on those
hierarchies through `0xd23800`. That worker publishes selected node/aggregate
change masks. Completing its **enrolled** hierarchy dependency therefore covers
this particular deferred publication. It is not proof that every physics,
animation or other native writer uses the same enrollment. Functions
`0xa592f0` / `0xa5e060` call `0xd23920` after `0xd14440` waits/clears the existing
hierarchy handle; the resulting pending record carries a zero dependency.
Function-entry attribution here uses the exact ELF unwind-header table.

The resolved physics path further closes a specific chain. Simulation
`0xd83f90` reaches `0xd84820` → `0x116c250`, which waits the scene's prior
dependency and obtains a backend completion token; `0xd84971` waits that token.
On success with writeback stage bit 4 selected, `0xd84a90` performs writeback,
reaching `0xd19ce0` through `0xdabb8f` / `0xd84d99`. Its setter `0xd19d20` waits
the destination hierarchy dependency, writes TRS, then publishes subscribed
dirty masks and pending state inline before returning. Thus successful
simulation **with that writeback selected** includes those subscribed writes'
publication; this is not merely an empirical later-frame observation.

`SyncTransforms` follows `0xd84990` → `0xda4430` → `0xd22a40` → `0xd22070` to
collect selected existing pending masks, completes collection at `0xd22d07`,
and completes two processing jobs through `0xda4688` → `0xc045b0` → `0xc05c30`
before selected physics updates return. This closes its selected work, not
unrelated/not-yet-enrolled writers. Neither physics route supplies a bounded
movement-time managed callback or repairs the pre-callback native work terms.

### Owned bounded dirty-set fallback

A fixed slot-indexed coalescing bitset can bound CarbonLuau-owned storage and
drain work. It must use current weak identity/epoch routing, latch discovery
unavailable on lost routing/overflow, and complete maintenance before any
successful query. No prototype is claimed to close upstream notification merely
because that downstream data structure is bounded.

The existing borrowed delegate is a **requested-drain callback**, not a
movement-time producer callback. Using it to feed an owned dirty set still
inherits the native collection costs above.

A separate synchronous native callback registry was found (`0xd2c410` /
`0xd2be40` registration; `0xd2cc10` single-node, `0xd2cc80` ancestor and
`0xd2cdf0` subtree invocation). Follow-up classified all discovered direct
delivery callers and one tail-jump: masks 1/2/4/8/16 correspond behaviorally to
hierarchy reconstruction, children/content changes, parent change, pre-removal,
and transform/component rebinding. These are inferred event meanings, not
recovered enum names. Registration associates callbacks with matching nodes,
but the inspected world/local position setters use different dirty storage
and invoke none of these delivery functions. It can notify structural movement
such as reparenting; it is **not established as a complete translation
notification seam**. Direct-call inspection does not exhaust indirect mechanisms.

Nor is `0xd28630` a common publication hook: it runs only on capacity growth,
missing coalesced updates and appends with spare capacity. Common helper
`0xd23920` updates/appends pending hierarchy state, but world/local position
setters implement equivalent publication inline rather than calling it.
Patching that helper alone would therefore miss already-proven movement.
No native patch was installed and no production ABI change was made.

### Disposition and preserved boundaries

The prerequisite remains open: prove a bounded upstream producer path plus
all-writer publication/exclusion through the query cut. Do not implement the
sparse grid around an unqualified drain, impose network visibility semantics,
sample positions, or scan/rebuild the world at query time. D20 and Foundation 1
remain unchanged; no numeric discovery policy, public discovery API or private
Discovery-2A runtime is adopted by these research findings.

The exact remaining gate is no longer merely an unavailable native body:
the available borrowed callback is downstream of population-dependent
collection, the inspected structural callback mechanism does not cover the
position setters, and synchronization is proved for particular enrolled jobs
and selected physics writeback rather than every permitted writer through the
query cut. A producer-side bounded adapter or defensible precollection guard
would still need complete coverage, detectable loss/exhaustion, dependency
ordering and bounds on hierarchy/component work. No such adapter/guard is
qualified here. This is **NEEDS REVIEW**, not a proof that complete discovery
or a simple sparse grid is impossible.

Independent review tightened three claims: pending-entry coalescing is not a
bound on repeated subtree work or retained capacity; absence of direct callback
calls is not a binary-wide impossibility theorem; ring-doubling instruction
sites are distinguished from their helper entries. Those findings are resolved
above. The synchronization trace independently established the selected
physics/enrolled-job scope, not universal producer coverage.

Final local API/link, architecture, release-policy/content checks and research
Python syntax checks pass. They do not qualify native movement bounds, dense
world discovery or public VM behavior. No new native production code exists,
so no new ASan/UBSan/index-leak or hosted-CI PASS is claimed. Production
`src`, `native`, bootstrap, API metadata and release identities have no diff.
Main and origin/main remain at the starting revision; no commit or push was
made, consistent with the unresolved proof gate. Existing research, lock and
unrelated derived scratch remain intentionally uncommitted. The prior 41-item
ledger is historical; this section owns the additional receipts/body findings,
while all unimplemented Discovery-2A/2B gates in that ledger remain open.

Both final-fixture servers exited. Read-only cleanup checks found no remaining
RustDedicated process or installed research fixture; prior packages matched
their recorded hash on both hosts. Windows prior Base/Community/Oxide hooks and
Carbon configuration matched their per-run backups byte-for-byte. Task evidence
and the read-only native image copy are retained; unrelated scratch is untouched.

## Writer-side multi-avenue investigation — 2026-10-03

**WORLD/ENTITY FOUNDATION 2 NEEDS REVIEW — no closed all-writer adapter or
all-writer query cut; owned ancestor/topology/dependency bounds also remain
unqualified.** This is not a proof that complete discovery is impossible.
The prerequisite for conditional Discovery-2A implementation has not passed.
No weaker inclusion rule, native detour, runtime index, numeric resource policy
or D20 amendment is adopted. All earlier receipts and failed approaches above
remain historical evidence, not erased or reclassified as PASS.

Six independent investigations covered common native publication (A/G), finite
managed inventory (B), physics/jobs (A/J/K), hierarchy/version maintenance (I/F),
shipped Harmony/AutoPatch (C), and host hooks/publication/scheduling (D/E/J/K/L).
The main investigation separately modeled owned dirty-state mechanics (H),
reviewed the new native bodies and integrated the findings. These are different
scopes, not six votes on the same proposed solution.

### Reproducible finite inventory, not an exhaustive writer proof

New `tools/Research-WorldWriterInventory.ps1` reads explicitly hashed modules
through Mono.Cecil, including nested types. It records relevant call references,
definition/body/native boundaries, indirect dispatch candidates and other Unity
targets whose relevance still needs review. Duplicate-signature definitions are
reported as ambiguous, not collapsed into fictitious unique targets. Wrong
input hashes fail. Its output status is always
`RESEARCH_ONLY_NOT_COVERAGE_PASS`; unresolved routes never become covered merely
because the input hash is pinned. Timing, identity, bookkeeping and thread
obligations remain explicit in its output.

Final script SHA-256: `7b519e97027f613893905de706aca398ebe228e254f416fb7df2f4d830614e99`.
The independent platform scans used the preserved readonly inputs
`C:\Users\aiden\AppData\Local\Temp\CarbonLuau-Entity1A-25653776\Assembly-CSharp-win.dll`
and `Assembly-CSharp-linux.dll`, and existing `Mono.Cecil.dll` in that directory.
Input digests are the Windows/Linux Assembly-CSharp pins already recorded above.
Reproduce separately per platform with `-AssemblyPath`, `-ExpectedSha256` and
`-CecilPath`; the tool emits evidence objects and does not write host files.
The final evidence-only correction exposes whole-input P/Invoke/InternalCall
definition counts explicitly. A separate definitions-only Cecil check confirmed
109/105 P/Invokes and zero InternalCalls in these Assembly-CSharp inputs;
that zero does not apply to Unity modules. The final count-field addition was
syntax/wrong-hash checked; the complete IL counts were obtained from the earlier
script variant, not represented as a full rerun after that reporting-only change.

| Inventory | Windows | Linux |
|---|---:|---:|
| Method definitions including nested types | 45,570 | 45,570 |
| Broad movement-family/host-helper references | 9,767 | 9,767 |
| Reflection/delegate/indirect-dispatch candidates | 254 | 255 |
| Native P/Invoke definitions in the supplied module | 109 | 105 |
| Other distinct Unity targets requiring relevance review | 3,448 | 3,448 |

Broad counts include readers, configuration, client paths and non-root objects.
They are not counts of admissible Entity writers. Platform counts are separately
measured; matching counts do not establish native equivalence. Unity definitions
not supplied to a scan remain `ExternalDefinitionNotSupplied`. Later compiled
plugins, other modules and native engine producers are outside these inputs.

| Selected target, both platforms | Instructions / caller methods |
|---|---|
| Transform position / localPosition | 264 / 204; 83 / 66 |
| Transform rotation / localRotation | 194 / 129; 92 / 72 |
| Transform localScale | 59 / 49 |
| Transform parent setter | 8 / 8 |
| SetParent one / two arguments | 14 / 12; 35 / 30 |
| SetPositionAndRotation / SetLocalPositionAndRotation | 36 / 33; 5 / 5 |
| Rigidbody MovePosition / MoveRotation | 5 / 4; 6 / 5 |
| Rigidbody position / rotation | 5 / 5 each |
| CharacterController.Move | 2 / 1 |
| NavMeshAgent.Move / Warp | 2 / 1; 1 / 1 |
| TransformAccess.SetPositionAndRotation | 2 / 2 |
| TransformAccess position / rotation | 1 / 1 each |

A specific managed-writer gap is now proven, rather than merely guessed:
`RustNavMeshAgent.FlushTransformBatch` (`0x0600af8f`) switches from ordinary
Transform writes to `RunMovementTransformJob` at 32 pending updates
(`IL_0119`). `MovementTransformJob.Execute` (`0x0600af96`) writes through
TransformAccess at `IL_00fd`, `IL_010b`, `IL_011e`. The scheduling helper
(`0x0600af91`) completes its supplied handle at `IL_0099` after scheduling at
`IL_0090`. That is a useful exact completion point for this job, not a universal
engine barrier. Earlier `TryQueueTransformUpdate` is a potential agent-level
notification seam, but its lifetime/hierarchy routing and interception are not
qualified. Patching only ordinary Transform setters misses the job route.

### Shipped managed interception and exact native bypasses

The Windows CoreModule's public `Transform.set_position` (`0x06002860`),
localPosition (`0x06002862`), rotation (`0x06002871`), localScale
(`0x06002879`), `SetParent(Transform,bool)` (`0x06002880`) and
`SetPositionAndRotation` (`0x06002883`) have managed bodies. Ordinary Harmony
interception is a candidate for calls crossing those wrappers; no experiment
here claims that such a patch was installed or qualified. Corresponding injected
setters, such as `0x060028da` / `0x060028dc`, are bodyless InternalCalls.

The shipped Harmony 2.4.2.0 hash remains the Foundation 1 pin; MVID is
`b9e6cf65-9433-482b-8860-83cff28d0128`. Its MethodBodyReader (`0x060000e9`)
creates an empty stream for absent IL. `HandleNativeMethod` (`0x060000ec`)
requires DllImport to synthesize a native bridge; it does not automatically
preserve Unity InternalCalls. Managed detouring (`0x060001bc`) is not evidence
of interception of Unity's direct native calls. The versioned
[Harmony method copier](https://github.com/pardeike/Harmony/blob/v2.4.2.0/Harmony/Internal/MethodCopier.cs)
and [patch tools](https://github.com/pardeike/Harmony/blob/v2.4.2.0/Harmony/Internal/PatchTools.cs)
support those inspected DLL findings; exact shipped IL remains primary evidence.

Selected physics writeback calls `0xd19ce0` directly at `0xd84d99`; it reaches
`0xd19d20`, which writes position at `0xd19dce` and rotation at `0xd19e1e`.
The position icall instead starts at `0x802400` and jumps to `0xd19610`.
Thus physics bypasses the ordinary managed position wrapper **and its icall**.
An InternalCall-preserving trampoline would still not close that route.

Additional exact Linux cores independently publish inline: quaternion writers
`0xd19200` / `0xd19400`, scale writer `0xd19b30`, and combined TR writers
`0xd19fe0`, `0xd1a240`, `0xd1a4e0`, `0xd1a700`. Mask-only position/rotation
writers `0xd1a920` / `0xd1aa40` return without appending the pending list;
validated native callers include `0x1308385` / `0x13083c5`. Their subsystem
names and admissible-Entity reachability remain unresolved. The deferred worker
`0xb98f60` publishes node/aggregate masks inside an input-count loop. These are
a lower bound on distinct producer surfaces, not a closed patch manifest.
Intercepting `0xd23920`, allocation helper `0xd28630`, or enrollment helper
`0xd23800` alone misses demonstrated bodies or later individual writes.

The main review reproduced the mask-only position body from `0xd1a920` through
its normal return at `0xd1aa1f`, and the physics write/dirty sequence from
`0xd19d20`, against the exact ELF hash. No native patch, object-memory read or
engine load was used. Windows native bodies have not been independently mapped;
this Linux evidence must not be presented as Windows native proof.

### Hooks and synchronization: stronger partial chains, no complete cut

Hook metadata across the six Windows/Linux Base/Community/Oxide assemblies has
30/72/849 patch attributes per platform. Inspected targets include player tick,
elevator requests, deep-sea teleport and Bradley activity, not general Transform,
Rigidbody, CharacterController, NavMeshAgent or the inspected BaseEntity position
setters. This is a scoped metadata search, not absence proof for every dispatcher
or dynamically loaded plugin. `set_ServerWorldPosition` (`0x06001571`) writes
at `IL_001b`, sets hasChanged and returns at `IL_002c` without network publication.
`set_ServerPosition` (`0x0600156f`) similarly writes local position. Supported
network/event hooks therefore do not supply complete current-position delivery.

The inspected older Windows Oxide `OnTick` transpiler (`0x06001c29`) inserts
hook dispatch before original `ServerMgr.DoTick` instructions; it is an entry
hook, not a post-writer barrier. Qualified Linux `CarbonProcessor.Update`
(`0x060000c5`) swaps queues and invokes actions at `IL_005a`; LateUpdate
(`0x060000c6`) processes timers, and Plugin.NextFrame (`0x0600063d`) enqueues
an action. Those bodies schedule execution, not transform completion. A legal
ordering is callback A directly moving an Entity, then callback B querying it.
Waiting one more frame does not manufacture the missing owned notification.

New physics traces extend the selected completion proof: `0xd84dd0` waits its
scene dependency and obtains/writes selected poses through `0xd19ce0` at
`0xd84e9a`. `0xd84eb0` waits and collects selected masks, constructs/sorts actor
records and invokes `0xd85310`; that worker writes adjusted poses through
`0xd19ce0` at `0xd8563c`. Interpolation/extrapolation names are inferred from
arithmetic/mode branches, not recovered symbols. Additional phase entry
`0xd838e0` reaches the first body; `0xd83830` reaches `0xd85650`, which can
invoke the second. Simulation return therefore does not exhaust these inspected
physics-adjacent pose-writing entries. Exact PlayerLoop ordering/activation,
animation/controller/navigation enrollment and exclusion until query completion
remain unproved. A later phase can be part of a solution but cannot alone bound
notification collection or prove all writers are covered.

### Hierarchy bounds and the owned dirty-state model

Let N be admitted slots, L maximum root-chain length, A tracked ancestor slots,
P ancestor/Entity membership pairs, and D distinct dirty ancestors. Maintained
transitive membership gives P <= N*L. Marking an already-resolved ancestor slot
can be O(1); expansion costs O(D + PDirty), with PDirty <= P. Refreshing U
distinct affected roots costs O(U*L) ancestor evaluation plus separately bounded
dependency completion and grid updates. An N-only cap proves none of L/A/P.
A single Entity under many non-Entity Transforms is the counterexample.

Reparenting a subtree containing K Entities can require O(K*L) membership repair.
It must be notified completely and finished outside queries while discovery is
unavailable, or handled by another proven bounded mechanism. Example:
`A -> B -> E`, then non-Entity B is reparented beneath C. Merely refreshing E's
current cell without repairing membership for C misses a later translation of C.
Immediate-parent-only membership also misses movement of A. A readonly model
checked 125 labeled four-node forests, 7,500 affected-set checks and 23,340 valid
reparent transitions; membership algebra passed and the unrepaired counterexample
reproduced. This is synthetic algebra, not Unity topology coverage.

Native ancestor notification `0xd2cc80` walks parents and can allocate scratch
for depth; subtree notification `0xd2cdf0` sizes scratch from the subtree and
visits nodes before delegate entry. Adding a constant-work callback there does
not bound that selected notification mechanism. These observations do **not**
require bounding all pre-existing Rust/Unity simulation work: the obligation is
CarbonLuau-added adaptation and host calls initiated by its maintenance/query.
The position getter/dependency work is relevant when CarbonLuau calls it.

New `tools/Research-WorldDirtySetModel.py` isolates H from writer coverage. With
fixed slot routing, each slot has current token, dirty token and queued flag;
a fixed ring holds slot indices. Retirement clears tokens but leaves queued
ownership until drained, so reuse cannot add duplicate queued slots. A new
lifetime must receive its own valid mark; stale-token input cannot mark it.
Insert/coalesce/pop each use a fixed number of array operations, without a scan
or allocation. Queue saturation latches UNTRUSTED and prevents further delivery.
Readiness requires the complete dirty drain, not success after a partial prefix.

Hypothetical packed storage is `17*N + 4*Q + O(1)` bytes: two uint64 token arrays,
one byte flag/slot, Q uint32 indices. A conservative aligned slot layout is
`24*N + 4*Q + O(1)`, for representable N/Q and checked allocation arithmetic.
This excludes identity routing, weak references, membership, grid, managed array
headers and concurrency machinery; it is **not** Python heap measurement or a
production memory budget. Q <= N, with no numeric N/Q policy adopted. The model
is single-owner; native producer-thread publication/atomics still need proof.
Monotonic token exhaustion must fail closed in any eventual implementation.

The finite model passed 125 reachable synthetic states / 1,388 reported checks,
including stale input, two lifetimes per slot, reuse while queued, coalescing,
at-most-once drain and a separate 1,000-repeat/full-ring saturation case.
The model script SHA-256 is
`771824b67ccc23802e6073fc02ef66715b07d611ad41a0c731da4aa9b6eb1227`;
the same script passed on controlling Windows (a lightweight subsecond model)
and via stdin to existing Python on BigKVM Linux, without installing software
or leaving a remote script/process. These are platform-neutral model runs,
not native writer qualification on either platform.
This proves the exercised data-structure mechanics, **not** an installed emitter,
O(1) host identity routing, Unity thread visibility or complete query freshness.
Unknown/lost routing must disable discovery; stale retired-token input can be
ignored only when the route proves it cannot affect a current admitted lifetime.
Recovery would keep discovery disabled through a bounded maintenance rebuild or
require restart; neither is implemented or selected by this model.

Independent review reproduced the model results without a bookkeeping defect.
The 1,388 count is manually accumulated reported checks, not a claim to count
every assertion/transition. A drain is bounded by Q dequeues only with producers
excluded. A None result may be an empty retired slot, not end-of-drain: callers
must inspect Count and Untrusted. No callback/lifetime authority is conveyed by
reusing a queued slot.

### Decision matrix before any implementation

In this matrix C means conditional on an additional proof; U means unproved;
No identifies a demonstrated insufficiency of the avenue alone. No row is an
accepted implementation. W/L evidence means independent managed/live evidence,
not automatic native equivalence.

| Avenue | Complete coverage | Added writer work | Pending bound | Detectable loss | Query freshness | Patch burden / fragility | Complexity | Platform evidence | Disposition |
|---|---|---|---|---|---|---|---|---|---|
| A common native seam | U; inline/mask-only/job surfaces | U routing/thread | U | U | U | Multiple native cores, high exact-build fragility | High | Linux bodies; W/L live movement inherited | No single seam found; native hybrid unresolved |
| B finite writer inventory | No; native/plugins outside inputs | U | U | U | U | Enumerable managed references, not closed producer graph | High for reachability | Independent W/L IL counts | Evidence/checker aid only |
| C AutoPatch/Harmony | No alone; physics/job bypass | C fixed-slot managed prefix | C H | C H | U | Managed wrappers feasible; InternalCall/native preservation separate | Moderate managed, high native | Windows Core/Harmony and Linux bypass | Conditional hybrid component |
| D supported movement hooks | No alone; direct writes escape | C specific hooks | C H | U uncovered writers | No alone | Low for supported hooks | Low, incomplete | W/L hook inventory | Specific families only |
| E network/publication | No; current writes precede publication | U | U | U | No current-position cut | Low patch burden, delayed semantics | Low | W/L IL; inherited stale-tree live proof | Rejected as authority |
| F versions | No; moved-in miss | C candidate validation | C fixed slots | No missing notifications | No alone | Low | Low | IL/native and algebra | Secondary validation only |
| G earlier native pending registry | U; mask-only/deferred enrollment | Added interception/routing cost U | No owned hard cap | U | U | Private native layout, high fragility | High | Linux bodies | Not an accepted substrate |
| H owned slot dirty queue | C emitter/routing | O(1) model only | Proven conditional N/Q | Latch on model saturation | U emitter/barrier | No host seam supplied | Small queue | Synthetic, platform-neutral | Data structure viable; not movement proof |
| I ancestor membership | C topology notifications | C O(1) mark, bounded later repair | C N/L/A/P caps | C loss route | U topology/getter/jobs | Structural native callbacks add traversal | Moderate/high | Linux bodies; finite algebra | Conditional hybrid component |
| J barrier | U all-writer enrollment/exclusion | U host waits | U | U | Selected jobs only | Native phase identity fragile | High | Linux physics, W/L Rust NPC job | Partial positive chains only |
| K defer phase | No alone; missing marks remain | C qualified scheduling | C H | U | U all-writer phase | Small scheduling surface | Small only after proof | Qualified Linux queue IL | Conditional with J, not repair |
| L incremental health check | No current-completeness proof | Bounded chosen prefix | C fixed slots | Later detection only | No alone | Low | Low | Analytical counterexample | Secondary health check only |

### Exact writer graph and stopping disposition

| Writer family | Affected state -> publication/barrier evidence | Contract classification |
|---|---|---|
| Direct world/local setters, normal plugins and Rust helpers | Root or arbitrary ancestor -> inline native masks/pending; managed wrapper candidate | Unresolved owned notification/routing; native publication proved for inspected bodies |
| Quaternion/scale/combined setters | Root/ancestor -> separate inline masks/pending | Unresolved producer inventory and ancestor expansion |
| Reparent/hierarchy reconstruction | Changed hierarchy -> structural callback family, population/depth work | Unresolved bounded topology notification/repair |
| Selected physics simulation writeback | Actor pose -> d19ce0 -> masks/pending before selected completion | Partial chain covered; owned emitter/all-physics cut unresolved |
| Later selected physics pose phases | Actor pose -> d84dd0 or d85310 -> d19ce0 | Partial chain covered; phase activation/order unresolved |
| Rust NPC batch | TransformAccess job -> supplied handle explicitly completed | Partial managed job fence covered; adapter/ancestor routing unresolved |
| Deferred native publication | Producer/job -> b98f60 mask writes; enrolled dependency | Partial publication path covered; universal enrollment unresolved |
| Mask-only native writers | Transform -> masks without local pending append | Proven distinct bodies; admissible reachability/enrollment unresolved |
| CharacterController / navigation | Managed references enter engine -> affected roots | Native producer closure unresolved |
| Animator / other native jobs | Engine writers -> hierarchy | Root/ancestor relevance and complete enrollment unresolved |
| Save/load and successful Spawn | Qualified 1A completion -> future spatial insertion | Lifetime proof reused; movement seeding/interception not implemented |

None of these unresolved families is declared impossible for an admissible
Entity without evidence. A complete graph requires zero unresolved routes.
The hybrid of managed wrappers + narrow host hooks + selected physics callbacks
still has native job/mask-only/ancestor gaps. Connecting it to H would hide those
gaps, not close them. Exhausting these reasonable avenues therefore reaches the
task's review condition, not conditional implementation authorization.

The concrete remaining tradeoff is whether to pursue a substantially broader
exact-native Unity adaptation, including full producer inventory/thread barriers
and independent non-Entity hierarchy/topology/dependency caps. It is not yet a
small qualified adapter comparable to the finite Spawn inventory. Installing
unproven native detours or declaring every below-cap grid fresh would weaken
the required safety boundary. No broad adapter is installed just to get PASS.
An engine-supported bounded producer primitive could instead close the same
gate; no such primitive is established on the inspected target. Complete
semantics and sparse-grid choice remain resolved and unchanged.

### Completion ledger for the writer-side task

| # | Requested item | Result |
|---:|---|---|
| 1 | Verdict | NEEDS REVIEW: unresolved complete native-writer/freshness and owned hierarchy/resource adaptation. Not nonviability. |
| 2 | Starting commit | f36ff7c83a9e273816f7b726fe3062618b430539, intentionally dirty main. |
| 3 | Rust | Build 25653776, protocol 2634.289.1, qualified revision 166494. |
| 4 | Carbon | Windows 2.0.262.0 / 8a81d70; Linux 2.0.261.0 / c74c4ca; Harmony 2.4.2.0. |
| 5 | Unity/runtime | Exact platform hashes above; native Linux ELF independently rehashed; Windows Assembly-CSharp/UnityPlayer rechecked on worker. |
| 6 | Avenues | A through L assessed with six distinct review scopes plus main H/integration. |
| 7 | Decision matrix | Above; no accepted complete avenue/hybrid. |
| 8 | Selected architecture | None qualified; sparse XZ remains preferred conditional index. |
| 9 | Common seam | Inline rotation/scale/combined, mask-only and deferred producers prevent a single-helper claim. |
| 10 | Finite inventory | New pinned research script; W/L counts above; native/dynamic/plugin unresolved status explicit. |
| 11 | Host hooks | Family-specific only; direct Transform writes can escape them. |
| 12 | Harmony | Managed wrappers candidate; bodyless icalls not automatically preserved; no movement patch installed. |
| 13 | Physics/jobs | New selected pose-phase chains and Rust NPC batch fence; universal coverage/order unqualified. |
| 14 | Hierarchy | N alone insufficient; bounded membership/repair conditional on L/A/P and complete topology notifications. |
| 15 | Plugins/host | Direct wrapper and native/job distinction explicit; no closed producer inventory. |
| 16 | Synchronization | No universal query cut selected. OnTick/NextFrame alone insufficient. |
| 17 | Freshness | Not proved; neither candidate-only validation nor delayed execution fixes moved-in omissions. |
| 18 | Dirty set | Synthetic fixed-slot token/coalescing ring tested; no production emitter. |
| 19 | Hard bound | Symbolic 24*N+4*Q+O(1) aligned queue storage only; no numeric runtime cap or total index budget. |
| 20 | Overflow/loss | Model saturation latches UNTRUSTED; emitter/route loss detection unqualified. |
| 21 | Recovery | Discovery-disabled maintenance/restart remains conditional, unimplemented. |
| 22 | Grid integration | Not implemented before prerequisite proof. |
| 23 | Bootstrap | Existing 1A startup authority preserved; spatial seed not implemented. |
| 24 | Spawn insertion | Not implemented; no second lifetime model. |
| 25 | Retirement removal | Not implemented; existing sticky 1A retirement unchanged. |
| 26 | Exact 3D filter | Preferred future current-root test retained, not claimed as discovery implementation. |
| 27 | Oracle | No index/oracle qualification; finite hierarchy/queue models are not live completeness tests. |
| 28 | Dense/high-motion | No new live stress this continuation; inherited 100-write probe retained; queue repeat/saturation synthetic only. |
| 29 | Windows | New independent pinned IL inventory and Core/Harmony review; earlier live receipts reused, no new native equivalence claim. |
| 30 | Linux | New independent pinned IL inventory and exact native body review; earlier live receipts reused. |
| 31 | Sanitizer/leak | No native production changes; no new ASan/UBSan or adapter/index leak qualification claimed. |
| 32 | Drift | Research input hash failure tested; no runtime all-writer fail-closed checker adopted. |
| 33 | Canonical update | Research/routing only; D20 unchanged. Complete inclusion and no scan fallback retained. |
| 34 | Discovery-2A | Not implemented: zero-unresolved-writer gate not satisfied. |
| 35 | Commits | No implementation/correction commit. |
| 36 | Tested source | Unchanged production HEAD; uncommitted research scripts, not a new tested production commit. |
| 37 | Checks/CI | Local API/link, architecture, release, script syntax/model and whitespace results recorded below; no new hosted CI PASS. |
| 38 | Network subsets | None adopted or implemented. |
| 39 | Later features | No discovery API, Signals, Spawn/Destroy, Position writes or global prefab enumeration. |
| 40 | Identities | Package 0.5.0, dev API 0.6.0-experimental, ABI 1.5, provider 1.2, schema 1, Luau pin unchanged. |
| 41 | History | All existing uncommitted research/receipts/probes and unrelated scratch preserved. |
| 42 | Commit/push | None; user conditions publication on PASS. |
| 43 | Worktree | main at starting origin/main; intentionally dirty research, lock and prior scratch preserved. |
| 44 | 2B handoff | Not permitted by this result. Resolve native-adapter/hierarchy/barrier review first, then qualify private 2A. |

No new servers, listeners, installs or patches were started in this continuation.
Successful readonly worker checks rehashed the exact Windows/Linux Rust and
native Unity binaries, found no RustDedicated process and no installed unique
movement fixture. The owned sandboxes/receipts remain available. No unrelated
resource was removed. Final validation below is research/document hygiene only,
not closure of the movement theorem or Discovery-2A.

Final local `Test-Api.ps1`, `Test-Architecture.ps1` (including GiveItem structural
safety), and `Test-Release.ps1` pass after the new evidence/routing edits.
PowerShell parsing passes for the writer inventory and existing movement
research/checker runners; Python AST parsing passes for the four research/model
and Linux runner scripts. `git diff --check` passes for tracked edits, and
research files are separately checked for whitespace because they are untracked.
No production `src`, `native`, bootstrap or release-manifest diff exists.
HEAD and the current origin/main reference remain the starting commit; no
production-source hosted run is claimed. A Git CRLF conversion warning is not a
whitespace/test failure. Prior research and unrelated scratch remain untouched.
