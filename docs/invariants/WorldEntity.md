# WorldEntity: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D20 — World/Entity Foundation 1

**Entity-1A private lifetime substrate and Entity-1B read-only runtime qualified
on the pinned hosts; Entity-1C owns combined public closure.** On the exact [qualified host target](../WorldEntityFoundation1A-Validation.md),
CarbonLuau's initial-batch Carbon AutoPatch observer installs before world
restoration. A synchronous prefix on each of the 24 qualified full virtual
`Spawn` method bodies advances the per-object epoch and retires its prior
lifetime. Only the successful outermost invocation, with its original run and
entire base chain completed normally, marks the epoch `COMPLETED`; a skipped,
throwing, nested/unqualified or incomplete attempt never does. A bounded
`OnServerInitialized` reconciliation requires every then-keyed admissible
entity to have an observed completed epoch before the startup baseline is
qualified. Later Spawn attempts remain under the same continuous observer.
Neither current flags nor a world-load marker manufactures per-object history.

This is a **narrow, exact-host-qualified Carbon AutoPatch/Harmony adaptation**
for private Entity lifetime and the qualified B1 notification, not general
permission to patch Rust or Carbon.
The 24-method inventory, Carbon hook pair, patch topology/order and host binary
identities fail closed on drift. Initial installation into an already-running
server cannot establish the baseline: Entity admission stays unavailable until
a qualified server restart. Full CarbonLuau plugin unload/reload breaks observer
continuity and likewise requires restart. Ordinary root/addon replacement and
VM recovery preserve the still-installed host observer while retiring the old
VM/domain/publication facade authority. Host-process restart creates a fresh
observer and lifetime space; no Entity token persists across processes.

Gameplay B1 additively exposes `Workspace.EntitySpawned:Connect(function(Entity)
end)` on the qualified hosts in development API `0.6.5-experimental`. Only the
successful completed outermost virtual Spawn, after catalog enrollment and
startup qualification, may produce this notification. The early Carbon
`OnEntitySpawned` hook is not completion authority. Reconciliation, listener
registration, replacement and recovery produce no historical replay.

Delivery revalidates the original exact Entity lifetime and committed listener
domain/publication at fresh callback admission. An entity killed, replaced,
re-spawned or otherwise retired before admission is suppressed; network-ID
lookup must never choose a replacement for a stale event. The resulting proxy
uses ordinary D20 live reads, equality and weak host identity. Later host races
may still cause controlled stale errors. D7/D10 listener publication and the
existing D23 owned Signal, global producer/fanout/retention limits, native
reservations, cancellation and scheduler are reused; no second event system or
budget is introduced. Unsupported host/source registration fails closed.
[Gameplay B1 qualification](../GameplayB1-Validation.md) owns the exact tested
tuple and bounds. Entity death/destruction, damage, inventory and policies are
not approved by this addition.

Gameplay B3 separately adds `Workspace.EntityDestroyed:Connect(function(Context)
end)` in development API `0.6.5-experimental`. It observes actual original-
incarnation removal through the exact-host collective observer: successful
original TerminateOnServer completion with original registry/network authority
released and deactivation; actual hash-pinned pool stack admission after original
registry/network authority release with unchanged epoch; or bounded owner-thread
observation that the original weak Unity wrapper is invalid and its epoch
unchanged. Pool admission certifies before later callbacks/reuse and does not
require IsDestroyed or deactivation. Native cancellation is
only a prompt, not complete coverage; original-watch intake uses the existing
bounded frame pump without realm scans or ID reacquisition. Cancellation, Kill
attempt, veto, flags, registry churn or D20 lifetime
retirement alone are not proof. Native deletion may leave host bookkeeping ghosts;
the event guarantees no cleanup convergence, pool completion or client receipt.

The callback receives only an immutable ordinary `EntityDestroyedContext` with
captured `Id: string`, `Prefab: string` and optional pre-removal `Position: Vector3`.
It never receives a destroyed live Entity proxy. Native-only/direct-pool removal supplies nil
Position. Identity and deduplication bind to the original observed D20 epoch;
network ID must never reacquire a replacement. Missing weak identity before proof
cannot certify native destruction. Once certified, the historical scalar fact
may outlive the wrapper. Native fanout admits only listeners published by the
last positive live-observation cutoff; ambiguous recent-registration windows may
drop rather than replay. Snapshots are ordinary values and delivery is best-effort.

B3 retains D20 startup continuity/restart requirements and all 24 Spawn patch
targets unchanged. Its separately pinned three-target removal/pool topology and native observation
source fail closed on drift or continuity loss. It reuses D7/D10 listener
publication and D23/B1 scheduling, reservation, fairness, cancellation and global
event bounds, with bounded weak watch/text retention and removal nesting. No
registration, reconciliation, replacement, recovery or shutdown replay exists.
[Gameplay B3 qualification](../GameplayB3-Validation.md) owns exact completion
evidence, concrete bounds and final gate status. EntityDied, damage, inventory,
loot/policies and generic hooks remain outside this addition.

Gameplay B2 separately adds `Workspace.EntityDied:Connect(function(Context) end)`
in development API `0.6.5-experimental`. Its exact-host envelope covers pinned
BaseCombatEntity and ResourceEntity families, not every BaseEntity removal or
arbitrary plugin-defined CLR type. Full original effective virtual Die/OnDied
or separately pinned DieInstantly completion, original terminal progress,
unchanged D20 incarnation and qualified final terminal state establish the fact.
An early OnEntityDeath notification, a dead flag, skipped original, throwing
tail or lifetime retirement alone does not. Ordinary Kill is removal, not death.
Death may complete without successful removal; removal may occur inside death
before its full method returns. No universal cross-Signal order is promised.

The only public payload is a frozen ordinary EntityDeathContext: original Id
and Prefab plus optional pre-invalidation bounded root Position, Killer,
KillerId, DamageType and Weapon. It contains no live Entity or HitInfo. Certified
historical scalars may survive original destruction/ID reuse; fresh callback
admission validates original host/observer continuity and listener VM/domain/
publication authority, never the current ID occupant. Captured Killer is an
original D11 connection, not an account-based replacement. Only the separately
pinned original synchronous melee RPC application supplies actor authority;
NPC/environmental/indirect/unproved sources yield nil. DamageType is a strongest
positive finite component snapshot, not universal cause inference. Weapon is
an optional cached full WeaponPrefab label, not equipped inventory or ownership.

Private deduplication follows qualified death episodes within the existing
Spawn incarnation. Full qualified Player RespawnAt (including NPC/disconnected
and alive-retry cases), TrainCar repair and SamSite healing may rearm the latch;
failed resets do not. One Entity lifetime can therefore have multiple completed
deaths without an Entity proxy retarget. All relevant original methods are
host-pinned; attempt-local bounded weak Harmony record stamps reject subtype
patch/install/unpatch drift even if acceptable current topology is restored.
No promise protects against arbitrary CLR restoration of identical internal
records under I12. Narrow Carbon AutoPatch adaptation observes completion only;
it must preserve original return/exception behavior and never enter Luau inline.

B2 reuses D7/D10 and D23's shared bounded event transport, scheduling, fairness,
fresh deadline, cancellation and at-most-once/no-replay model, plus B3's bounded
weak completed-Spawn watch storage. Loss of that shared watch source's
qualification disables B2 admission/delivery; it cannot advertise a healthy
source while silently missing new completed-Spawn membership. It adds no damage
Signal, generic hook API, new scheduler, public mutation or release authority.
[Gameplay B2 qualification](../GameplayB2-Validation.md) records concrete bounds,
static versus live coverage, preserved negatives and final gate status.

The historical [registry-continuity investigation](../history/world/WorldEntityLifetimeInvestigation.md#worldentitylifetimeinvestigation)
remains valid negative evidence: object, network ID, prefab and current keyed
occupancy alone do not prove an incarnation, and actual same-object pooled reuse
was **not observed** in that historical run. B3 separately qualifies controlled
actual host-pool admission/Pop and same-object reuse with a new completed Spawn
epoch; it does not retroactively change the historical evidence or claim natural
world pooling was observed. The [Spawn-prefix probe](../history/world/WorldEntitySpawnEpochProbe.md#worldentityspawnepochprobe)
showed failed Spawn can leave fully-spawned/keyed state. The
[completion-fence investigation](../history/world/WorldEntityCompletionFenceInvestigation.md#worldentitycompletionfenceinvestigation)
showed base-method success can precede a throwing outer `BaseEntity.Spawn` tail.
These findings are superseded as blockers by the continuously installed
full-virtual-call fence and startup reconciliation, not erased. Registry
removal/reinsertion by itself is not a new incarnation: unobserved same-object,
same-epoch churn does not retarget; observed missing required occupancy may
conservatively retire. Trusted in-process interference remains bounded by I12.

World/Entity Foundation 1 approves exactly this read-only development surface:
`game:GetService("Workspace")`; `Workspace:GetEntityById(Id: string) -> Entity?`;
and the generic host-backed `Entity` properties `Id: string`, `Prefab: string`
and `Position: Vector3`. `Workspace` is Rust-world ergonomics, not a Roblox
DataModel hierarchy. No raw BaseNetworkable/BaseEntity/Transform/GameObject,
serverEntities collection, host type hierarchy, reflection or generic host-call
escape enters Luau. [WorldEntityFoundation1.md](../WorldEntityFoundation1.md) owns the
supporting host research and implementation-facing detail.

A public Entity represents one exact currently registered BaseEntity host lifetime.
CarbonLuau assigns an internal monotonically increasing EntityLifetimeToken, never
reused within the loaded CarbonLuau host instance, and binds it to the exact managed
host object, its completed SpawnEpoch, captured nonzero network ID, current keyed
registry occupancy and captured prefab-lifetime evidence. Private state uses weak
object identity and survives VM/domain replacement only while the observer is
continuous. Every host-backed operation revalidates the owning
domain/publication/VM lifetime plus that exact host evidence on the owner thread.
Destroyed, pooled, replaced, ID-changed or registry-replaced host state retires the
old lifetime permanently. A stale proxy never re-resolves by ID and never retargets a
replacement entity. The implementation must not strongly retain destroyed host
objects merely to preserve facade identity.

Entity equality compares the CarbonLuau host instance plus exact private entity
lifetime token and never performs a host access. Two proxies for the same exact host
lifetime may compare equal across domains; domain retirement can still make one proxy
unusable. Equality remains available for stale proxies, but every host-backed
property/method otherwise fails closed. Ordinary values already returned from an
Entity, including Vector3/string snapshots, remain ordinary Luau values after
retirement.

`Entity.Id` is **not** exact lifetime identity. It is the canonical decimal string
of the current nonzero 64-bit host network lookup key: 1..20 ASCII digits, no leading
zero/sign/whitespace/exponent, parsed exactly as UInt64. Luau numbers are not
accepted because they cannot exactly represent all UInt64 values. The ID is a
current-world/process lookup key only and must not be treated as a persistence key
or proof that a later lookup is the same entity lifetime. A well-formed unknown ID
returns nil; malformed/noncanonical input is a programming error.
`GetEntityById` must use a target-qualified keyed server-registry lookup plus
constant validation and must never scan the registry. Registry entries that are not
live BaseEntity values are ordinary absence for this service.

`Entity.Prefab` is the exact full canonical host PrefabName/resource identity
captured for the exact lifetime, after live validation. Foundation 1 exposes no
Unity object name, ShortPrefabName, numeric prefab ID or resource object. The
canonical design ceiling is 512 UTF-8 bytes with no NUL; exceeding it is a controlled
host-state failure, not truncation. Future prefab input/spawn must separately
qualify one bounded canonical server-prefab namespace and may not interpret a
path-looking string as arbitrary filesystem/resource access.

`Entity.Position` is a read-only live observation of the exact BaseEntity root
`Transform.position` in Rust/Unity world coordinates, reusing D18's immutable
project-owned Vector3. Parenting does not change the public coordinate space. No
local coordinate, collider center, eye offset, terrain projection, Transform object
or stale cached position is exposed. Nonfinite/unrepresentable host coordinates are
controlled host-state failures.

Entity proxies are non-owning domain-bound host facades. Lookup is allowed during
provisional/cold-module execution because it is a read, but a newly-created proxy
participates in D7/D10 publication: if the publication scope that created it fails,
that proxy is permanently stale even if an ordinary Luau reference escaped.
Sharing an already-committed proxy does not transfer its ResourceOwner or Rust entity
ownership. Failed candidates preserve existing committed proxies; successful
root/addon/provider retirement stales proxies owned by the retiring domain without
destroying the world entity. A new domain may resolve a fresh proxy to a still-live
host entity. Fatal VM recovery/reload invalidates old proxies, preserves Rust world
state and never replays effects.

Foundation 1 adopts no whole-world enumeration, prefab-filter query, spatial/radius
query, lifecycle Signal or synthetic startup event. Rust worlds can contain hundreds
of thousands of entities; a returned-result cap does not make an O(all entities)
scan bounded. The Foundation 2 async-traversal pivot explicitly permits a future
complete traversal with total O(world population) work only if it has hard
per-owner-thread-turn and aggregate work, retained-state, result, total-inspection
and absolute-deadline bounds. No unbounded synchronous O(all-world) discovery is
permitted. Mutation/continuation completeness and encounter-time versus snapshot
semantics must be explicit; a successful subset cannot masquerade as complete
discovery. Under I12, external in-process mutation may race candidate observations;
no atomic world snapshot or universal external-writer serialization is required.
CarbonLuau-owned operations remain ordered on the owner thread. Exact Entity-1A
admission, storage/memory safety and hard read/work bounds remain mandatory.
The private [Foundation 2A substrate](../WorldEntityFoundation2A.md) now implements
fixed-slot completed-Spawn membership and watermarked asynchronous traversal.
It is not a spatial index or mutable registry iterator. Later births are excluded,
holes/reused slots count as work, and all results are exact lifetime-revalidated
before successful delivery. Catalog/observer loss, unsafe read, excess depth/work/
results or expiry fails the whole query, never a truncated successful subset.

The hash-pinned current-position adapter copies at most 65 local TRS/parent
records and performs at most 64 compositions, without entering Unity, waiting/
helping jobs, calling user code or retaining pointers. Its storage premise is
supported Unity owner-thread topology/reclamation: an uninterrupted owner-thread
borrow cannot interleave with supported structural mutation. Supported Transform
jobs can race scalar values; copied positions are encounter observations, not
atomic poses. Exception catching or post-checking does not create a storage lease.
Unsupported off-thread structural mutation is not made supported by I12.

`EntityDiscoveryPolicy` centralizes 262,144 slots, eight requests/two per domain,
1,024 shared scan/delivery units and raw slots per Update, 256 results, two
deliveries, 120-second absolute deadline and bounded maintenance. A fixed private
Update receiver avoids Carbon's shared growing next-frame enqueue. Expiry is
checked before work/delivery when host service resumes; this is not a wall-clock
callback SLA under host starvation. [Discovery-2B](../WorldEntityFoundation2B.md)
implements the additive development API below; its public qualification is
separate from the private 2A evidence.

```luau
Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number,
    Callback: ({Entity}?, string?) -> (),
    Options: { Prefab: string?, Limit: number? }?) -> ()
```

Radius is finite and nonnegative (zero tests the exact sampled point). Options
are an ordinary table containing only an optional exact full Prefab (512 UTF-8
bytes, nonempty, no NUL) and integer Limit 1..256, default 256. There are no
predicates, nearest-N, cursors or cancellation objects. Submission is non-yielding
and committed-owner-only under the shared current-operation/publication predicate;
a foreign Workspace or committed closure cannot launder provisional work.
Cold modules may export closures that submit later in valid committed execution.
Synchronous validation/stale/admission rejection enqueues zero requests. Success
is a fresh Entity array plus nil error, including an empty array; later failure
is nil plus a controlled error code, never truncated success. Physical encounter
order is deterministic for the traversed catalog, not nearest-distance order or
an immutable order across queries. Results preserve encounter-time membership
and position; properties read from returned Entity proxies remain live reads.

Native captures are bounded independently of ordinary task/event queues, two
per domain and eight per VM, including traversal-complete/undelivered work.
Fresh owner-thread scheduler admission owns the callback deadline and exact
host/session/VM/domain authority. Every result is revalidated and converted from
its original weak catalog candidate, never by ID lookup; expiry/catalog loss or
stale results fails the whole request. No callback reaches a retired/replacement
authority. Detachment/release is at most once; callback failure/timeout/recovery
never replays the traversal. Initial-install/full-unload restart rules remain.
Public token conversion is limited to returned matches, with cold pre-sized
lookup storage and bounded weak-token maintenance; resource exhaustion fails
closed without disabling independent Foundation 1 keyed lookup.
Combined final lifecycle, scale and release-readiness qualification is owned by
[Foundation 2C](../WorldEntityFoundation2C.md); it changes no D20/I12 semantics or
package identity and does not authorize release publication.

The [observation-boundary continuation](../WorldEntityObservationBoundaryInvestigation.md),
[traversal investigation](../history/world/WorldEntityAsyncTraversalInvestigation.md#worldentityasynctraversalinvestigation),
[catalog proof](../history/world/WorldEntityCatalogInvestigation.md#worldentitycataloginvestigation),
[native ordering](../history/world/WorldEntityPositionOrderingInvestigation.md#worldentitypositionorderinginvestigation) and
[position alternatives](../history/world/WorldEntityPositionAlternativesInvestigation.md#worldentitypositionalternativesinvestigation) preserve
all earlier open gates/negative findings in their original historical scope.
Foundation 2A does not revive the rejected movement-index, mutable registry cursor,
guarded waiting getter or universal snapshot-phase mechanisms.
`GetEntityById` remains exact keyed lookup and must never scan. Current spawn/kill/load hook
ordering is also not adopted as a simple symmetric EntityAdded/EntityRemoving
contract. Any future collection/event surface requires explicit inspected-work,
returned-result/queue and lifecycle-ordering bounds.

Host-owned entities remain Rust-owned. If a future Spawn API succeeds, its entity
likewise becomes ordinary Rust world state and is **not** auto-destroyed on addon,
provider, domain, VM or CarbonLuau retirement. Persistent versus explicitly
temporary creation must be separate contracts. Entity proxy identity never crosses a
server process/restart merely because Rust may save an entity or reuse/restore a
network ID.

Spawn and Destroy are **deferred and unimplemented**. A future Spawn adapter must own
canonical prefab validation, Create+Spawn as one operation, temporary returned-host
resource responsibility, networking/registry verification, persistence semantics and
a post-COMMIT uncertainty model. A future Destroy adapter must use the qualified
normal Rust Kill path rather than raw Unity destruction, account for synchronous
hook veto/interference, and verify retirement. No public spelling/signature is
reserved by D20.

Future Spawn, Destroy, Position writes and specialized entity mutations are
committed-only irreversible host effects. They must reuse Player-1F-C's corrected
authoritative mutation predicate: an existing nonprovisional admission **and no active
publication scope**. Dependencies, nested/cold modules, pcall or scheduling cannot
launder eligibility. I12 applies only when there is concrete evidence that another
trusted in-process component synchronously changed operation-relevant state; it does
not excuse unsafe adapter selection, bounds, resource accounting, normal callback
outcomes or verification defects. Host-driven recursive VM entry remains prohibited;
any future CarbonLuau-owned resulting event is admitted later through I4 bounded
scheduling.

With the Entity-1A private gate closed, [Entity-1B](../WorldEntityFoundation1B-Validation.md)
implements the read-only Workspace/Entity runtime and qualifies keyed lookup,
Prefab, Position and stale behavior on exact Rust build `25653776` and pinned
Windows/Linux Carbon targets. [Entity-1C](../WorldEntityFoundation1C.md) records
cross-domain and replacement composition, provider/recovery behavior,
identity-table scale, public metadata/docs and release planning. Enumeration,
spatial query, Signals, Spawn, Destroy and specialized capabilities require later
explicit architecture rather than being implicitly authorized follow-on phases.

D20's Entity-1A/1B work changed no package/API/ABI/provider/schema/Luau
identity. The published `0.5.0-experimental` API covers Persistence, not Entity;
Entity-1C assigns the additive read-only Workspace/Entity surface to the
development scripting API `0.6.0-experimental`. Package `0.5.0` remains the
published identity until a separately authorized release. Native ABI `1.5`,
provider protocol `1.2`, package schema `1` and the Luau pin do not change.

<!-- END RECORD Invariants.md -->

