# World/Entity Foundation 2 — bounded asynchronous complete discovery research

**FOUNDATION 2 ASYNC TRAVERSAL QUALIFIED — PUBLIC DISCOVERY MAY BEGIN.**
[Foundation 2A](WorldEntityFoundation2A.md) owns private implementation,
exact-host/resource policy and final qualification. No public discovery binding
is implemented by this closure.

The paragraphs below preserve the pre-implementation gate history. Their open
catalog/position/policy conclusions are superseded by Foundation 2A's supported
owner-thread storage premise, capped no-wait observations, fixed frame receiver
and Windows/Linux evidence. The negative findings themselves are unchanged.
The [observation-boundary continuation](WorldEntityObservationBoundaryInvestigation.md)
adopts the user's practical I12 distinction: external observation races are allowed,
while lifetime/memory safety, bounded work and CarbonLuau-owned ordering remain
hard requirements. Universal freshness serialization is no longer an acceptance
gate. The new Windows/Linux mapping probes are not storage-lease qualification.
The user pivoted away from movement-maintained indexing on
2026-10-03. The active design, collection mechanics, callback proposal, resource
model are in [the async traversal investigation](WorldEntityAsyncTraversalInvestigation.md).
The user subsequently selected a lifetime-fed nonspatial catalog; [the catalog proof](WorldEntityCatalogInvestigation.md)
owns the active fixed-slot continuation design, preserved 768-case finite model,
and the 2026-10-04 real lifetime-model integration (17,275 Windows/Linux checks).
The optional catalog is connected directly to completed Spawn/retirement, not
lazy tokens, but production activation/live reconciliation remain unqualified.
New Windows/Linux native mapping and task-owned no-wait/depth guard observations
narrow, but do not close, the current-position synchronization/work theorem.
The [focused native ordering follow-up](WorldEntityPositionOrderingInvestigation.md)
identifies callback registration, an inlined publisher and a fixed completed-wait
fast path; publisher/read serialization remains unproved for that historical
guarded-getter route. The [position-alternatives continuation](WorldEntityPositionAlternativesInvestigation.md)
instead compares cached/world values, host phases and bounded snapshot jobs,
including new Windows/Linux isolated phase observations and concrete batch-setup
wait/helping paths. No universal phase or bounded job admission is qualified.
Those investigations retain their original scoped conclusions; the revised
observational boundary is owned by I12/D20 and the continuation linked above.
No difficult-candidate deferral, arbitrary old cache or successful subset is adopted.
Guarded host-enumerator continuation is no longer the preferred implementation.
Complete discovery is still required; network-group subsets are rejected. Total
O(world population) traversal is permitted only across hard-bounded turns with
separate retained-state, result, total-inspection and deadline limits, as now
clarified by D20. No synchronous scan, public API or private Discovery-2A runtime
is qualified by the pivot. Foundation 1 lifetime/publication rules are unchanged.

## Preserved pre-pivot sparse-grid research

**All remaining text below records the earlier research direction and verdicts.**
Its references to an active movement gate, sparse-grid requirement, synchronous
API proposal or blanket scan prohibition are superseded as future Foundation 2
design by the async record above; its measurements and negative findings remain
historical evidence. Do not repeat those investigations or treat their failed
movement theorem as a prerequisite for encounter-time traversal.

**Verdict: WORLD/ENTITY FOUNDATION 2 NEEDS REVIEW — bounded, exhaustive
movement freshness is not yet proven; no Discovery-2A baseline is adopted.**
The user requires complete discovery and explicitly rejects network-group
subset semantics. That product choice is resolved. Successful radius discovery
must include every currently D20-admissible Entity whose root Position is in
the sphere, subject only to an optional exact Prefab filter. Excess work or
results must cause a controlled failure, never a partial success. The smallest
candidate is a CarbonLuau-owned sparse XZ grid, with exact 3D root filtering.
The [movement investigation](WorldEntityDiscoveryMovementInvestigation.md)
now includes a writer-side multi-avenue review: the native change stream is
downstream of population-dependent collection, managed wrappers miss native
physics/jobs, and a closed producer inventory/query cut remains unproved.
This is research, not a D20 amendment, production index or public API contract.

## Authority and exact target

[D20](Invariants.md#d20--worldentity-foundation-1),
[Foundation 1](WorldEntityFoundation1.md), its [private host qualification](WorldEntityFoundation1A-Validation.md),
[read-only runtime qualification](WorldEntityFoundation1B-Validation.md) and
[combined public closure](WorldEntityFoundation1C.md) remain authoritative.
Every future discovery result must use the existing exact Entity admission,
current keyed occupancy, owner-thread access, and D7/D10 publication witness.
This research does not change the initial-install or full-plugin-reload
restart requirement.

The inspected Windows Rust `Assembly-CSharp.dll` is SHA-256
`bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`,
MVID `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e`. Inspected
`Facepunch.Network.dll` is SHA-256
`ca0181b2458ebeaa59f4e9f9aaf09e73cdc3a808ccd26b8cfd503ab30c466fc7`.
This network-assembly digest is a research observation, not a newly adopted
compatibility pin. The qualified host envelope remains Rust
build `25653776`, protocol `2634.289.1`, Windows Carbon `2.0.262.0` at
`8a81d70`, Linux Carbon `2.0.261.0` at `c74c4ca`, Harmony `2.4.2.0`.
The Linux assembly/hook identities and exact combined hashes are in the 1A
record. The initial Windows files matched that qualified assembly. The linked
movement follow-up now records new Windows/Linux probes, a one-type marker
alternative and Windows hook-drift rejection; these are not index qualification.
A host upgrade is separately requalification-
gated, not silently equivalent.

`tools/Research-WorldDiscoveryHost.ps1` is a read-only Mono.Cecil inspection
aid, not a runtime qualification checker. It was run against the task-owned
disposable Windows host's pinned assemblies. Its method tokens/IL observations
below are repeatable and more precise than plugin examples.

## Exact-host primitive inventory

| Primitive | Exact observation | Discovery disposition |
|---|---|---|
| `Vis.Buffer(Vector3, radius, mask, triggers)` | `Vis::.cctor` allocates a shared 32,768-collider array. Sphere `Buffer` calls `Physics.OverlapSphereNonAlloc`, stores the count and warns when count reaches array length. | Collider hits are bounded in the managed buffer and saturation is conservatively detectable, but Unity does not specify a bound on native broadphase work. Not an adopted hard-work primitive. |
| `Vis.Entities<T>` / `Vis.EntityComponents<T>` | Both call `Vis.Buffer`, traverse returned colliders, discard disabled/null, resolve `BaseEntity` through `GameObjectEx.ToBaseEntity`, and deduplicate using a shared `HashSet<object>`. `Vis.Components<T>` does not deduplicate. | Collider-derived, not all-world Entity semantics; `Vis.Entities` itself does not reject saturation and may return an incomplete list. Multiple/child colliders and parent resolution affect results. |
| `NetworkVisibilityGrid` and `EntityRealm.FindInGroup` | Positional layers and visibility groups exist. `FindInGroup` loops every member of one group's `networkables` buffer. `GetVisibleFrom` adds global groups; its layer helper uses `GetOrCreateFromLayer`, so it can create groups. `EntityRealm.TryFindGroup` is a lookup; `EntityRealm.GetEnumerator` traverses the complete keyed registry. | A private caller could inspect a pre-counted, bounded set of existing groups, but the host does not establish that a group query is a complete radius query over current root positions. A single dense group can be arbitrarily large unless rejected before iteration. Convenience traversal is not suitable as-is. |
| `CoarseQueryGrid` | Stores static/dynamic bounds and exposes boolean `Check`/job checks; no `BaseEntity` result enumeration. | Occupancy accelerator, not a World Entity discovery source. |
| `RoomSpatialIndex` | Dictionary of room cells and `Gather` of `Room` objects. | Specialized room index; not a general BaseEntity source. |
| `BaseEntity.Query.Server.EntityTree` | Uses `Spatial.Grid<BaseEntity>` plus specialized Player/brain grids. `TransformChanged` moves entries, normally from `NetworkPositionTick`. | Live proof: a fully-spawned/keyed BoxStorage moved through `ServerWorldPosition` is absent at its new position until its position tick; moving a parent similarly leaves the child's cell stale until the child's tick. Not a complete current-position source. |
| Unity `ObjectDispatcher.GlobalTRS` | Pinned internal CoreModule API tracks exact component types; the live probe detects direct, local, ancestor and physics movement. A borrowed-data callback exposes lengths without the copying Get overload. | Preferred movement research candidate, not yet a qualified primitive. Tracking BaseEntity does not include BoxStorage. Native dispatch has no inspected capacity/count-before-dispatch argument or per-instance registration, and its collection/history bounds remain unproven. |
| `BaseNetworkable.serverEntities` | `EntityRealm` is keyed by `NetworkableId`; `Find` is the qualified Foundation 1 point lookup, while `GetEnumerator` iterates all keyed values. | Exact IDs only. An arbitrary callback-time traversal violates D20's whole-world work prohibition. |
| `PrefabAttribute.server` / prefab metadata | Its library maps prefab IDs to `PrefabAttribute` collections, i.e. prefab metadata, not a live-entity-by-full-PrefabName index. | No qualified global live-prefab lookup. |
| Building/loot/type-specific caches and Carbon helpers | No inspected candidate established a complete, bounded, generic live `BaseEntity` radius or full-prefab enumeration contract. | Not promoted from common plugin usage or naming alone. A later narrow feature may research a particular category separately. |

Unity documents that [`Physics.OverlapSphereNonAlloc`](https://docs.unity3d.com/cn/current/ScriptReference/Physics.OverlapSphereNonAlloc.html)
does not grow its result array and returns the array length when full. It does
**not** document a maximum number of internal broadphase nodes/colliders
examined or a stable subset/order when full. A managed count of at most 32,768
therefore bounds postprocessing, not the native owner-thread call. This is an
inference from the API's missing work guarantee, not a measurement of native
complexity on the pinned Unity build.

## Why the tempting radius API is not yet honest

Candidate A would be `Workspace:GetEntitiesInRadius(Position, Radius, Options?)`
with optional exact `Prefab` and an output `Limit`. Candidate B packages the
same arguments in a query table. Candidate C adds global
`GetEntitiesByPrefab`. None is adopted today.

For a collider-based API, the only supportable inclusion wording would be
approximately: *eligible live BaseEntity values associated with enabled
colliders touching the sphere on a fixed private layer/trigger policy, after
deduplication and exact Entity admission*. That does **not** include every
Entity whose root `Position` lies in the sphere. Colliderless or disabled-
collider entities can be missed; a large collider may touch the sphere with
its root outside; child colliders can map to a parent; colliders without a
BaseEntity yield nothing; multiple colliders deduplicate. Players, building
blocks, deployables, storage, dropped items, NPCs and trigger-only entities
have **not** been individually qualified for a single layer/trigger policy.
No layer mask is proposed for Luau authors. A final implementation would need
explicit category tests and a single project-owned private mask/trigger rule.

`Vis` saturation can be treated as `TooManyCandidates` when the count is
32,768; count equal to capacity cannot certify completeness even if exactly
that many colliders exist. Retrying with a smaller radius is possible advice,
not a correctness proof. A public `Limit` cannot hide excess matches: if
bounded candidates produce more than the result cap, the safe proposed
behavior is a controlled `TooManyResults`, not silent truncation. However,
neither rule solves unbounded native overlap work. `Vis`'s static buffer also
performs up to 32,768 managed collider visits before CarbonLuau can apply a
smaller author-visible result limit.

The visibility-grid alternative can make *managed enumeration* bounded by
pre-counting groups and refusing any group/aggregate beyond a hard candidate
cap before walking members. But `Network.Networkable.UpdateGroups` explicitly
updates group membership from a position and `EntityNetworkRange`; the query
has no proof that every currently nearby entity has already had that update,
and special/global/restricted groups do not have ordinary local-cell meaning.
`GetVisibleFrom` mixes layers and global groups for networking, not a
geometrically complete radius. The earlier possibly incomplete network-group
proposal is **rejected by the user**, not an active fallback. Group membership
may remain diagnostic evidence; it cannot define public discovery inclusion
or certify a complete empty result.

An owned spatial index could avoid a callback-time world scan and cover
colliderless entities, but needs a qualified initial baseline plus complete
movement/reparenting/teleport/removal/reuse observation. An incomplete freshness
contract is not authorized. The full-Spawn observer alone is not a motion
observer. An index keyed only at Spawn can miss moved-in entities and is not
an honest current-position query. Index memory/maintenance in a world with
hundreds of thousands of entities, dense bucket overflow, and observer gaps
must be bounded separately. Foundation 1 deliberately owns no such index.

## Resource model and measurements

No safe numeric radius, inspected-candidate, returned-Entity, or temporary-
byte hard limit is adopted. Provisional engineering targets such as a 128-
result cap or 512/1,024-candidate cap are **tuning candidates only**, not
evidence or API commitments. `Vis`'s 32,768 slots are an observed host
constant, not a CarbonLuau work budget. A future proof must independently
bound group/cell visits `G`, raw candidates `C`, deduplicated values `D <= C`,
admitted results `R <= D`, temporary storage `O(G+C+R)`, and the worst
owner-thread work of every host call. If an underlying host operation can
examine more than `C`, the proof fails regardless of these managed bounds.
Choosing `RadiusMax` requires a cell/physics work proof; finite input
validation alone is insufficient.

The Foundation 1 live fixtures observed 1,744 Windows and 1,732 Linux
qualified startup entities and selected world/deployable/NPC categories.
Those are **not** dense discovery measurements. This research did not run
open-world, dense-base, storage/deployable, dropped-item, NPC-heavy or
intentional collider-saturation queries. No radius timing, duplicate rate,
per-category inclusion, parented-hit or overflow observation is claimed.
The structural 32,768 saturation check is not a substitute for a dense live
probe. It would be misleading to use the Foundation 1 lookup timing as a
discovery timing/SLA.

## Conditional public semantics, not adopted

If a true bounded spatial primitive is proven, prefer the small form:

```luau
Workspace:GetEntitiesInRadius(Position: Vector3, Radius: number,
    Options: { Prefab: string? }?) -> { Entity }
```

The form is a proposal, not metadata or an implemented method. A single
project-owned hard result cap is simpler than a user `Limit` until completeness
and overflow behavior are qualified. If retained, `Limit` must be 1..hard cap
and excess must error or be explicitly flagged, never silently look complete.
Validate finite center/radius, positive finite radius under a proven maximum,
exact-case full canonical Prefab (UTF-8, no NUL, at most 512 bytes), and
option keys/types before host work. Prefab filtering may only run over already
bounded candidates and compare the same canonical identity as `Entity.Prefab`;
it may not load a prefab, interpret a path, use a short/fuzzy name or convert
arbitrary values. Invalid input uses controlled errors. Host exceptions and
Unity/CLR object references never leak into Luau.

Candidate order should be unspecified; no nearest-N, distance order, or
host-order guarantee. Deterministic `Entity.Id` order could be added only
after a hard candidate cap makes sorting bounded and a use case justifies it.
**NO CURSOR** for a single bounded local query; a cursor would otherwise
requery an unstable world or retain snapshot state. **GLOBAL PREFAB QUERY
DEFERRED; NO GLOBAL INDEX NEEDED for this phase.** There is no evidenced
host live-prefab index and no approved CarbonLuau lifecycle index. These are
research decisions, not authorization to add a public method.

Any future query runs on the owner thread and would be synchronous only if
its full host and managed work fit existing operation deadlines under dense
live qualification. No callback/yielding or new scheduler exemption is
adopted here. Each candidate must resolve to `BaseEntity`, deduplicate by
exact host object/lifetime, pass the 1A completed/baseline SpawnEpoch and
current keyed validation, then create/reuse the canonical token/proxy under
the calling domain's publication witness. PENDING/failed Spawn and stale or
unqualified baseline never publish. Read-only provisional/cold execution and
rollback would follow Foundation 1 D7/D10 rules; a failed scope stales only
newly created proxies, not existing committed ones. No raw collider or group
handle enters Luau.

Owner-thread serialization prevents another CarbonLuau callback from
interleaving with one query, but does not make an engine-wide snapshot or
preclude a host operation changing state. Candidate admission and each later
property access revalidate independently. Entities can move, die or leave
the keyed registry after a result array is returned; such proxies may be
stale on use. No result-order or snapshot-isolation promise is made.

Security review: query spam, huge radius, dense colliders, one group with
unbounded members, duplicate colliders, sorting, allocation and host exception
paths remain DoS concerns until hard work proof and live measurements exist.
The future operation must use bounded owner-thread admission/rate policy
without exposing arbitrary masks, CLR types, reflection, prefab loading or
cross-domain authority. Discovery does not require lifecycle Signals; they
remain a separate foundation.

## Smallest owned index candidate

Use one sparse uniform XZ grid, not an octree, per-prefab global index or network
visibility cache. An Entity root occupies exactly one cell; Y is applied in the
final 3D sphere test. Collider/trigger/layer presence has no inclusion role.
Slots hold non-owning exact object/lifetime evidence and intrusive previous/next
links; buckets hold a head and raw count. No Luau/domain authority resides in
the index. All returned proxies still use Foundation 1 admission/publication.

For cell width `H`, select all cells intersecting the sphere's XZ bounding
rectangle. In exact arithmetic, one axis visits at most `ceil(2*Radius/H)+1`
cells. Checked conservative floating-point boundary handling must be proved
before adopting the implementation. Pre-count the aggregate raw membership
before visiting a single member; stale entries count toward that bound. Reject
if cells exceed `GMax`, raw membership exceeds `CMax`, or admitted filtered
results exceed `RMax`. Prefab filtering does not discount inspected work.

Use a bounded-depth cell directory, not an assumption of constant-time hashing
under arbitrary coordinates. A balanced tree is a small candidate: a red-black
tree with at most `B` buckets has height at most `2*log2(B+1)`. Conditional on
fresh membership, collection costs `O(G*log(B+1)+C+R)`, has no world traversal,
and requires bounded `O(G+R)` query scratch. Retained slots/buckets are `O(N+B)`
with `B <= N`; byte limits require actual layout/allocator qualification, not
this asymptotic statement. No numeric grid/result/candidate policy is adopted.

Bootstrap reuses the qualified startup lifetime observer but must separately
prove spatial seeding. Movement tracking must precede seed reads and cover all
changes up to readiness. Every later completed Spawn must establish membership;
new/PENDING/failed epochs must not publish. Observed or unobserved keyed-registry
churn must not silently drop a still-qualified object's membership: removal
on temporary registry absence alone would need a re-admission mechanism.
Native Unity component IDs are only change-routing keys, never Entity lifetime
authority; resolve against current weak identity/epoch before applying a delta.

Add an explicit continuous slot/bucket/dirty-storage cap. Foundation 1's 262,144
startup inspection cap is **not** already a continuous world-size limit.
Overflow must make discovery unavailable rather than omit entities or interfere
with Rust Spawn. Partial maintenance, lost updates, unknown types or an
uncertified freshness barrier must fail every query, including empty queries.
No scan-on-query fallback, sampled membership, or silent truncation is allowed.

## Required proof and next research task

The required theorem is: at each successful query cut, every D20-admissible
Entity is represented in its current root-position cell; all membership-affecting
changes through that cut have been incorporated; establishing freshness and
collecting candidates each obey independent hard work/allocation bounds.

The current task conditionally authorizes private Discovery-2A once the proof
passes. Its prerequisite remains **research, not index implementation**. The
downstream ObjectDispatcher callback is not the movement correctness substrate:
its length cannot bound preceding native collection. The active gate is a
complete bounded producer mechanism, all-writer synchronization/exclusion,
and independent topology/dependency bounds, rather than another frame-history
setting or dense benchmark. The
[writer-side decision matrix](WorldEntityDiscoveryMovementInvestigation.md#writer-side-multi-avenue-investigation--2026-10-03)
assesses A through L and records concrete managed/native/job bypasses. A fixed
owned dirty-queue model is feasible but supplies no missing emitter. The
finite managed inventory explicitly leaves engine and later plugin routes
unresolved. No broad native adapter or numeric hierarchy policy is adopted;
the remaining native-adaptation/resource tradeoff needs review before runtime
implementation. Preserve bootstrap/Spawn/registry and Windows/Linux gates.

After producer-coverage, freshness and resource proofs pass, implementation may be phased into private
bounded candidate collection, exact Entity admission/publication integration,
real pinned-VM tests, dense/overflow/category/parenting/race live matrices,
Windows/Linux resource/owner-thread qualification, then public metadata/docs.
API identities remain unchanged until that separate implementation and release
decision. Query, global prefab index, cursors, lifecycle Signals, Spawn,
Destroy and other mutation APIs are explicitly deferred by this record.

## Research validation and publication state

The read-only Cecil probe completed against the pinned Windows assembly copy.
Repository `Test-Api.ps1`, `Test-Architecture.ps1` and `Test-Release.ps1`
passed after this documentation change; `git diff --check` found no whitespace
error. These are document/invariant/packaging checks, not live dense-world,
Linux discovery, Unity native-work or public-VM qualification. The additional
Windows movement probe passed its exact-type assertions and borrowed-view
count checks; this is limited feasibility evidence, not exhaustive movement
or native-work qualification. Independent review identified missing freshness,
resource, type-coverage, bootstrap and registry-reinsertion proofs. The design
is not internally closed, so this record does not authorize Discovery-2A or a
D20 amendment. No runtime source, metadata, generated definition or release
identity was changed.

The [movement-proof follow-up](WorldEntityDiscoveryMovementInvestigation.md#movement-proof-follow-up--2026-10-03)
records new disabled/inactive, grandparent, physics and borrowed-data probes on
both platforms, plus a research-only root-marker alternative. A one-frame
history setting retained an undrained change for many frames; it is not an
established pending-byte/entry cap. Native collection bounds and exhaustive
writer synchronization through the query cut remain unproven after the
available seam/source/IL/symbol review. The verdict remains **NEEDS REVIEW**, not
nonviability. Private 2A and later public 2B remain unimplemented; no weaker
semantics or empirical substitute for I8 is adopted.

The narrower [native-history continuation](WorldEntityDiscoveryMovementInvestigation.md#narrow-native-history-and-freshness-investigation--2026-10-03)
resolves exact Linux function bodies without patching Unity. Lifecycle rings
are distinct from transform dirty masks/pending hierarchies. The borrowed
delegate runs after dynamically sized native collection, not at movement
publication. Marker population alone does not bound global pending-list,
matching-hierarchy, component-filter or ancestor work. A separate structural
callback registry misses ordinary translation, and the inspected job barrier
covers enrolled hierarchy dependencies rather than a proven all-writer query
cut. Complete semantics and the sparse grid remain unchanged; the remaining
research is an upstream bounded publication mechanism plus its synchronization
proof, not a new public API or alternate index design.

The subsequent writer-side review adds exact quaternion/scale/combined,
mask-only and deferred native producers, a Rust NPC TransformAccess batch path,
and later selected physics pose-writing phases. Managed Harmony wrappers and
supported hooks cover specific families, not the full writer graph. Ancestor
membership additionally needs non-Entity depth/topology bounds; an Entity cap
alone is insufficient. The owned slot/token queue passed finite model tests,
without qualifying native thread visibility or discovery. Complete semantics,
the sparse grid, D20 and public/runtime identities remain unchanged. This is
NEEDS REVIEW, not a proof of nonviability or permission to begin Discovery-2B.
