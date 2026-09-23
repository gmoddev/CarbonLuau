# CarbonLuau World/Entity Foundation 1

Status: **CANONICAL ARCHITECTURE BASELINE — no production Workspace/Entity implementation**

Adopted by D20. This document owns the supporting host research, design rationale,
phase routing and qualification gates for the first CarbonLuau world/entity surface.
The concise normative rules are in [D20](Invariants.md#d20--worldentity-foundation-1).

Foundation 1 deliberately does **not** expose Rust's entity object model. It defines a
small world-facing facade:

~~~luau
local Workspace = game:GetService("Workspace")

local entity = Workspace:GetEntityById(id)

if entity then
    print(entity.Prefab)
    print(entity.Position)
end
~~~

The public model is Roblox/Luau-familiar in service/facade ergonomics, not Roblox
DataModel compatibility. There is no Instance tree, Parent/Children contract,
client/server Instance model, raw Transform, raw BaseEntity, reflection escape or
generic host-call bridge.

## 1. Baseline and evidence classification

Architecture review started from CarbonLuau \`main\` at
\`943b58fb5d2f8f147113a7720c67556bb785068c\`.

CarbonLuau's current feature-qualified host record for the Player/inventory work is
Rust Dedicated Server app \`258550\`, build \`25353106\`, plus Carbon \`2.0.259\`,
protocol \`2026.09.03.0\`, revision
\`21063e8490adf412101bcc7d1cfe9d6280f61e80\`. That exact target is the required
starting point for Entity-1 implementation qualification. Historical Phase 0 worker
evidence names an older Rust build and does not supersede the later exact-build
feature evidence.

Evidence is separated as follows.

### Exact/current CarbonLuau evidence

- [Player-1F-B validation](PlayerInteractionFoundation1FB-Validation.md) records the
  exact Rust/Carbon target above and the exact downloaded depots used by the latest
  host-sensitive mutation qualification.
- [Player-1F-C](PlayerInteractionFoundation1FC.md) owns the corrected committed-only
  mutation predicate: a host mutation requires an existing nonprovisional admission
  and **no active publication scope**.
- [D18](Invariants.md#d18--player-interaction-foundation-1) already qualifies the
  project-owned \`Vector3\` value and the public meaning of a live world-space
  \`Position\` read for Player.
- I4, D7, D10, D11, D14 and I12 remain authoritative for owner-thread access,
  publication, domains, replacement, recovery and trusted in-process interference.

### Current Carbon evidence

Current Carbon source exposes the following useful lifecycle facts:

- Carbon's
  [OnEntitySpawn patch](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon.Hooks/Carbon.Hooks.Community/src/Entity/OnEntitySpawn.cs)
  is a prefix on \`BaseNetworkable.Spawn()\`. It proves that synchronous plugin code
  can run on the spawn path before the normal Spawn body completes.
- Carbon's
  [server-initialized patch](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon.Hooks/Carbon.Hooks.Base/src/Static/IOnServerInitialized.cs)
  marks the server initialized after startup has completed and is also used for
  hotloaded plugins.
- Carbon exposes
  [server shutdown metadata](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon.Hooks/Carbon.Hooks.Community/src/Server/OnServerShutdown.cs).
- Carbon's current production metadata still identifies Carbon \`2.0.259\` /
  protocol \`2026.09.03.0\`; current public Rust release metadata has moved since
  CarbonLuau's exact build qualification. New Rust releases therefore remain
  requalification inputs rather than proof for build \`25353106\`.

These sources are current upstream evidence. They do not replace exact-binary
qualification where Foundation 1 depends on a specific Rust implementation detail.

### Current Rust/Oxide generated evidence

Current generated Oxide hook documentation exposes useful Rust call ordering:

- [OnEntitySpawned](https://docs.oxidemod.com/hooks/entity/OnEntitySpawned) is called
  from \`BaseNetworkable.Spawn()\` after server initialization/network-group setup and
  after the entity is marked spawned, but before the immediate/global network update
  tail completes.
- [OnEntityKill](https://docs.oxidemod.com/hooks/entity/OnEntityKill) is called from
  \`BaseNetworkable.Kill(...)\` and a non-null hook result overrides the normal kill
  path. Destroy is therefore not a callback-free primitive.
- [OnEntityLoaded](https://docs.oxidemod.com/hooks/entity/OnEntityLoaded) is called
  while a network object is being loaded from save. A load callback is not the same
  thing as a CarbonLuau exact-lifetime publication event.
- [CanNetworkTo](https://docs.oxidemod.com/hooks/network/CanNetworkTo) and network
  group hooks demonstrate that entity visibility/network groups have their own host
  behavior. Foundation 1 intentionally hides them.

This generated evidence is useful for architecture and for selecting exact-build
fixtures. It is not treated as a substitute for inspecting/running the target build
during implementation.

### NetworkableId representation evidence

Facepunch's current
[\`Rust.Data.NetworkableId\`](https://github.com/Facepunch/Rust.Polyfill/blob/master/Rust.Data/NetworkableId.cs)
is a struct containing a \`ulong Value\` with zero meaning invalid. A Luau number is
not an acceptable lossless representation for every 64-bit unsigned value.
Foundation 1 therefore uses a canonical decimal **string** for the public lookup
key and keeps the host \`NetworkableId\` type private.

### Position and entity-count evidence

Unity defines
[\`Transform.position\`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Transform-position.html)
as the world-space position. D18 already adopts the same world/local distinction for
Player. Entity.Position reuses that semantic.

A full Rust world can be very large. Facepunch has published a production example
with
[362,299 entities](https://rust.facepunch.com/news/bags-to-riches). Third-party
server reports commonly describe hundreds of thousands of entities later in a wipe.
That makes an arbitrary whole-\`serverEntities\` scan from a Luau callback an
unacceptable default design merely to provide convenient enumeration.

## 2. Host model adopted by CarbonLuau

Foundation 1 distinguishes three things that Rust itself often combines.

1. **Host lookup key** — the entity's current nonzero network ID. This is represented
   publicly only as \`Entity.Id: string\`.
2. **Exact host entity lifetime** — one concrete live server-registered BaseEntity
   object for as long as that exact object remains the registry occupant for the
   captured host ID.
3. **Luau facade lifetime** — one domain/publication-bound Entity proxy giving a
   particular script authority to observe that exact host entity lifetime.

The public ID is not the lifetime identity. The exact-lifetime token is private and
never exposed.

### BaseNetworkable versus BaseEntity

Rust's global server registry is a BaseNetworkable registry. Foundation 1 does not
make BaseNetworkable the public abstraction.

A Foundation 1 \`Entity\` exists only for a currently live, server-registered object
that resolves as \`BaseEntity\`. Registry entries that are not BaseEntity are normal
lookup absence for this API. This prevents the public contract from inheriting the
widest and least gameplay-specific Rust base class.

BasePlayer instances are not specially excluded. If a live BasePlayer is present as
a server-registered BaseEntity, Workspace may represent that world object as Entity.
That Entity lifetime is the Rust world-object lifetime, **not** D11's exact player
connection lifetime. There is no Foundation 1 conversion between Player and Entity,
and Player permissions/identity remain exclusively the Players facade's concern.

### Registry

Foundation 1 may use \`BaseNetworkable.serverEntities\` internally, but it never
exposes that collection or a live view of it. The first public operation is keyed
lookup only. Entity-1B must prove that the exact target's keyed \`Find(NetworkableId)\`
path is available and does not implement the lookup as a scan. If that gate fails,
\`GetEntityById\` does not ship.

### World readiness

The Workspace service may be obtained while a domain exists, but host-backed world
operations require the server world to be initialized and not shutting down.
Calling a world operation outside that interval raises a controlled operational
error. CarbonLuau must not fabricate an empty world while startup is incomplete.

## 3. Foundation 1 public surface

The entire approved public surface is:

~~~luau
game:GetService("Workspace") -> Workspace

Workspace:GetEntityById(Id: string) -> Entity?

Entity.Id: string
Entity.Prefab: string
Entity.Position: Vector3
~~~

Entity equality is also specified below.

No constructor for Entity exists.

### Workspace

\`Workspace\` is the service name. It is intentionally familiar to Roblox/Luau
authors, while its documentation explicitly says it represents the Rust server world
and is not a Roblox DataModel container.

A Workspace facade is bound to the domain lifetime through which it was obtained.
Using a Workspace facade after its owning domain retires is a controlled stale-facade
error.

### GetEntityById

\`Workspace:GetEntityById(Id)\` is the only Foundation 1 discovery operation.

The input is a canonical decimal ASCII representation of a nonzero unsigned 64-bit
lookup key:

- 1 through 20 bytes/digits;
- digits \`0\` through \`9\` only;
- no sign, whitespace, decimal point, exponent or NUL;
- no leading zero;
- parsed value must be 1 through \`UInt64.MaxValue\`.

Malformed/noncanonical input is a programming error. A well-formed ID that is not
currently a live server-registered BaseEntity returns \`nil\`.

The method performs one target-qualified keyed registry lookup plus constant bounded
validation. It must never scan \`serverEntities\`.

### Entity.Id

\`Entity.Id\` is the canonical decimal string corresponding to the captured host
network lookup key.

It is **not** a persistent entity identity:

- it is scoped to the current Rust world/process state;
- CarbonLuau does not promise that Rust never reuses a value;
- scripts must not persist it as proof that a later entity is the same lifetime;
- a later \`GetEntityById(oldId)\` may legitimately return a different Entity lifetime
  if the host has reused that key.

The safety promise is narrower and stronger: an **existing Entity proxy never
retargets** because an ID was reused.

Numbers are never accepted as entity IDs. This avoids 64-bit precision ambiguity.

### Entity.Prefab

\`Entity.Prefab\` is the exact canonical full Rust prefab identity captured for the
exact host lifetime, using the host's full PrefabName/resource identity rather than a
Unity GameObject name or ShortPrefabName.

Foundation 1 does not expose:

- \`Name\`;
- \`ShortPrefabName\`;
- \`prefabID\`;
- a Unity resource object.

The full prefab string is preferred because short prefab names can be ambiguous and a
Unity object name can be mutable or implementation-specific.

For the read-only property, CarbonLuau validates the exact entity lifetime before
returning the captured ordinary string. A previously returned string remains an
ordinary Luau value after the Entity later goes stale.

Foundation 1 does not accept prefab strings as a spawning or resource-loading API.
Future prefab input must use a separately qualified canonical validator and supported
server-prefab registry. A path-looking string is never permission for arbitrary
filesystem/resource loading.

### Entity.Position

\`Entity.Position\` is a read-only live observation of the exact BaseEntity root
\`Transform.position\`, converted to D18's existing immutable project-owned Vector3.

Semantics:

- Rust/Unity world coordinates;
- no axis or unit remapping;
- world-space even when the entity is parented;
- no local-position fallback;
- no collider/center/eye/terrain offset;
- no Transform object;
- no cached stale position fallback.

The entity lifetime is revalidated before the host read. Nonfinite or otherwise
unrepresentable host coordinates are a controlled host-state error rather than a
fabricated \`Vector3.new(0, 0, 0)\`.

A Vector3 already returned to Luau remains an ordinary immutable value after the
entity or domain retires.

### Deliberately absent generic properties

Foundation 1 does not expose \`Entity.Name\`, \`OwnerId\`, \`IsValid\`,
\`ClassName\`, Rust type names, parent/children, network group, rotation, velocity,
health or inventory.

\`OwnerID\` is a Rust field with 64-bit representation and host-specific meaning. It
does not become a generic CarbonLuau ownership concept merely because the field
exists.

An \`IsValid\` property would also invite a check-then-use race. Every host-backed
operation is required to validate exact lifetime at the operation boundary anyway.

## 4. Exact host-lifetime identity

Entity lifetime is the critical Foundation 1 rule.

CarbonLuau maintains an owner-thread-only identity registry for **observed** entity
lifetimes. It is not a copy/index of the entire Rust world.

When a Workspace lookup first observes a qualifying BaseEntity, CarbonLuau assigns a
private monotonically increasing \`EntityLifetimeToken\`, never reused within the
loaded CarbonLuau host instance. The identity record captures enough information to
prove that future operations still refer to the same exact host lifetime:

- CarbonLuau host instance/lifetime;
- private lifetime token;
- weak managed object identity for the exact BaseEntity wrapper;
- captured nonzero NetworkableId value;
- captured prefab identity/prefab ID evidence needed by the target adapter;
- live/retired latch.

The token is not the host network ID and is never exposed to Luau.

### Validation predicate

Every host-backed Entity operation must, on the owner thread, fail closed unless all
applicable checks still hold:

1. the CarbonLuau host/VM context is current;
2. the proxy's owning domain is still current;
3. any publication scope that created the proxy successfully published;
4. the identity record is not retired;
5. the captured managed object is still obtainable and is not Unity-destroyed;
6. the host object does not report destroyed/removal state under the qualified
   adapter;
7. its network object still exists and carries the captured nonzero ID;
8. a keyed lookup of that captured ID resolves to the **same managed host object**;
9. captured prefab-lifetime evidence has not changed incompatibly.

Failure latches that exact identity record/proxy stale where the adapter can establish
retirement. No operation changes the record to point at the registry's new occupant.

The implementation must not keep destroyed Unity objects alive merely to preserve a
proxy. Identity records use weak/refcounted host references or equivalent bounded
bookkeeping; equality is preserved by the immutable token rather than a strong host
reference.

### Pooling/reuse

Foundation 1 assumes neither that managed wrappers are never pooled nor that network
IDs are never reused. Safety must hold even if either occurs.

If the same managed object later has a different network ID, or the same network ID
later resolves to a different object, the old exact lifetime retires. A subsequent
lookup creates a new exact-lifetime token. Old proxies remain stale forever.

This deliberately avoids making an undocumented Rust reuse policy part of the public
contract.

### Entity equality

Two Entity proxies compare equal iff they refer to the same CarbonLuau host instance
and the same private exact \`EntityLifetimeToken\`.

The proxy's owning domain is not part of entity identity. Therefore two proxies
obtained by different live domains can compare equal while both refer to the same
exact Rust entity lifetime.

Equality:

- never performs a host call;
- remains readable after a proxy goes stale;
- does not make the stale proxy usable;
- never considers an ID-reused replacement equal to the retired entity.

This is stronger and more useful than ordinary userdata identity without confusing
public \`Id\` with lifetime identity.

## 5. Facade/publication lifetime

Entity proxies are non-owning host-backed CarbonLuau facades.

Each proxy carries:

- exact EntityLifetimeToken;
- immutable origin \`ResourceOwner\` domain;
- VM/host generation identity;
- publication eligibility for the scope in which that proxy was created.

Creating an Entity does **not** make the origin domain the owner of the Rust world
object. It only owns the facade/capability.

### Candidate and module publication

Entity lookup is a read and is allowed during provisional/cold-module execution.
However, a newly created host-backed proxy participates in the existing D7/D10
publication machinery so that a failed candidate or failed first-load module cannot
leak a newly published usable host capability through ordinary same-VM memory.

A proxy created in a publication scope may be used for permitted reads while that
scope executes. If that scope fails, the proxy is permanently stale even if an
ordinary Luau reference escaped. If the scope commits, the proxy becomes an ordinary
domain-bound host facade.

This reuses D7/D10; it is not a new provisional model.

Returning or sharing an already-committed Entity proxy is ordinary same-VM value
sharing and does not transfer its origin domain.

## 6. Cross-domain sharing and replacement

Sharing an Entity proxy through a public module does not transfer:

- Rust entity ownership;
- facade ResourceOwner;
- exact-lifetime identity;
- mutation authority.

A foreign live domain may read through a valid shared proxy. Future mutations through
that proxy would still be subject to the proxy's ResourceOwner lifetime and the
current admitted operation's publication context.

Replacement rules:

| Transition | Old proxy | Rust world entity |
|---|---|---|
| Failed root candidate | Existing committed proxy remains valid if host lifetime remains live | unchanged |
| Successful root replacement | Old root-owned proxy becomes stale | not destroyed |
| Failed addon candidate | Existing committed addon proxy remains valid | unchanged |
| Successful addon replacement | Retiring addon-owned proxy becomes stale | not destroyed |
| Required dependency reconstruction | New domain resolves fresh proxies as needed | unchanged unless Rust changed it |
| Optional provider/addon loss | Proxies owned by retiring domain stale; foreign committed owners follow their own lifetimes | not destroyed |
| Provider unload | Provider-owned addon domains retire; their Entity proxies stale | not destroyed |
| CarbonLuau unload | all Entity proxies/tokens cease to be usable | Rust world remains host-owned |

A new domain may resolve a fresh proxy to a still-live host entity. If the same
CarbonLuau host instance/identity registry survives the domain replacement, that fresh
proxy may carry the same exact entity-lifetime token and therefore compare equal to
the old stale proxy. It has fresh domain authority. The old proxy is never retargeted.

## 7. Fatal recovery, reload and server lifecycle

### VM-fatal recovery

Fatal VM retirement invalidates every old Luau Entity proxy as part of the VM/domain
retirement. World entities remain Rust-owned. Reconstructed scripts may perform fresh
Workspace lookups. No world mutation is replayed and CarbonLuau invents no persistent
entity state.

### CarbonLuau reload

A CarbonLuau plugin reload starts a new host/identity epoch. No old private token is
accepted by the new instance. Rust entities that survived may be resolved by their
current public ID into fresh proxies, but object equality does not cross the destroyed
VM/host instance.

### Server startup/hotload

Carbon's initialized-state hook is the readiness boundary to qualify. A hotloaded
CarbonLuau instance queries current host state on demand. Foundation 1 sends no
synthetic \`EntityAdded\` events for already-existing entities.

### Shutdown

Once server shutdown/world teardown begins, Workspace host operations fail closed.
All observed exact-lifetime records are retired as part of CarbonLuau teardown.
CarbonLuau must not walk the world destroying entities merely because facade state is
being released.

### Save/load and restart

A server save may serialize ordinary Rust world entities according to host rules.
Foundation 1 does not alter \`enableSaving\`, force persistence or attach
CarbonLuau-owned persistence metadata.

A loaded entity in a later world/process is a new host lifetime even if Rust happens
to restore the same numeric network ID. Entity proxy identity never crosses a server
restart. Persistent script references require a separate future design using an
application-level persistence key, not \`Entity.Id\`.

## 8. Ownership model

### Host-owned entities

Entities already present in Rust are host-owned. CarbonLuau owns only its facade and
identity bookkeeping. Domain/VM/provider retirement never implies host destruction.

### Future CarbonLuau-created entities

If Spawn is later approved, successful Spawn transfers the entity into ordinary Rust
world ownership. It is **not** a domain-owned resource that is automatically killed
when an addon unloads.

That rule is deliberate: spawned gameplay entities can participate in server state
and persistence, and deleting them on script reload would be destructive and
surprising.

A future API that explicitly creates temporary/domain-scoped entities must be a
different contract with explicit teardown semantics. Foundation 1 reserves no public
name for that future concept.

## 9. Discovery/query model

Foundation 1 adopts only keyed lookup.

It rejects these APIs for now:

~~~luau
Workspace:GetEntities()
Workspace:GetEntitiesByPrefab(...)
Workspace:FindEntities(...)
Workspace:GetEntitiesInRadius(...)
~~~

### Why enumeration is deferred

A returned result cap alone does not bound the work required to find matching entries.
A prefab filter implemented by scanning the complete server registry would still be
O(all world entities), and real worlds can contain hundreds of thousands of entries.

Foundation 1 will not create and reconcile a CarbonLuau-wide world index merely to
make such calls convenient. A future query foundation may add enumeration only if it
can state both:

- a hard inspected-work bound; and
- a hard returned-result bound

without silently returning a misleading partial "all entities" result.

### Why spatial queries are deferred

Rust has physics/visibility helpers commonly used by plugins, but their exact
completeness depends on layers, colliders, masks and supported host semantics.
Foundation 1 does not equate a physics overlap result with "all Workspace entities in
radius" without exact-target qualification.

A future spatial query may be adopted if an efficient supported primitive can state
clear inclusion/exclusion semantics and hard result/work bounds. No Foundation 1
world index is authorized.

## 10. Lifecycle Signals

Foundation 1 does **not** expose \`Workspace.EntityAdded\` or
\`Workspace.EntityRemoving\`.

The host currently provides useful hooks, but their ordering is not a simple symmetric
CarbonLuau lifecycle contract:

- a Carbon \`OnEntitySpawn\` prefix can run before Spawn completes;
- the generated \`OnEntitySpawned\` callback runs inside Spawn before the networking
  tail completes;
- \`OnEntityKill\` can veto the normal kill path;
- save-loaded entities can exist before CarbonLuau hotload;
- shutdown and world loading have separate lifecycles.

A correct Signal design would require exact ordering, startup reconciliation, kill
veto handling, queue bounds and exact Entity lifetime during callbacks. That is
unnecessary for the read-only first surface.

If adopted later, CarbonLuau-owned entity events must reuse the existing bounded
Signal/Connection system and enter Luau later through I4 scheduling. No Rust hook may
synchronously reenter the VM.

## 11. Prefab identity and future prefab input

The public read-only prefab identity is the exact full host PrefabName string.

Foundation 1 intentionally does not expose a second short-name namespace. A future
spawn/query input must use one canonical identity only.

Future prefab input is required to:

- be a bounded string;
- use the canonical resource form accepted by the qualified server-prefab registry;
- resolve to a supported BaseEntity prefab before mutation;
- reject arbitrary filesystem paths, URI-like input, traversal/backslashes and
  unsupported resource classes;
- perform no fuzzy/display-name lookup;
- not expose prefab IDs or Unity resource objects.

\`Workspace:PrefabExists\` is not useful enough before Spawn/query support exists, so
it is deferred rather than creating another thin service method.

## 12. Spawn research and decision

**Decision: deferred from World/Entity Foundation 1.**

No \`Workspace:Spawn\` spelling/signature is reserved by D20.

The likely host adapter shape is a single CarbonLuau operation that internally owns
both entity creation and Spawn. Luau must never receive a pre-spawn BaseEntity or be
required to call \`CreateEntity\`, \`Spawn\`, \`SendNetworkUpdate\` or network-group
methods separately.

Before a Spawn API can be adopted, an exact-build implementation design must qualify:

1. canonical server-prefab resolution and the supported spawnable subset;
2. whether creation can return resources or invoke lifecycle code before returning;
3. the exact COMMIT point, conservatively no later than entry into the first host
   creation call;
4. position/rotation defaults and whether a two-argument prefab/position API is
   semantically sufficient;
5. Spawn hook/callback ordering;
6. network registration and current registry visibility;
7. default ownership/OwnerID behavior;
8. save/persistence behavior;
9. cleanup responsibility for a returned but not successfully spawned host object;
10. strongest defensible post-Spawn verification;
11. I12 interference fixtures.

Successful spawn must become ordinary Rust world state, not domain teardown state.

Any failure after COMMIT is a controlled host-operation error that states world state
may have changed. No rollback or exactly-once claim is permitted. A cleanup attempt
for a returned unattached resource must use a target-qualified supported path; raw
Unity destruction is not an architectural fallback.

Because those exact-host gates are not necessary for a useful read-only base and are
not all qualified by current CarbonLuau evidence, Spawn remains out of Foundation 1.

## 13. Destroy research and decision

**Decision: deferred from World/Entity Foundation 1.**

No \`Entity:Destroy()\` spelling is assigned to the current public identity.

The correct future direction is the normal Rust entity kill path, not raw
\`UnityEngine.Object.Destroy\`, manual registry removal or separate network update
calls. Current generated host evidence shows \`BaseNetworkable.Kill\` calls
\`OnEntityKill\` and allows a non-null plugin result to override normal kill behavior.

A future Destroy API must therefore qualify on the exact target:

- exact live-lifetime validation immediately before COMMIT;
- which normal/default DestroyMode is appropriate without exposing Rust enums;
- COMMIT no later than entry into the hookable Kill operation;
- callback veto/exception and trusted mutation behavior;
- post-call registry/destroy-state verification;
- whether host teardown is synchronous enough for one-turn verification;
- repeated destroy behavior;
- no host-driven recursive VM entry.

Before COMMIT, stale/programming/ineligible-context failures are controlled errors.
After COMMIT, CarbonLuau may not claim "nothing changed" merely because the exact
entity is still present: synchronous trusted callbacks may have performed other host
effects. Uncertain post-COMMIT results require a controlled host-operation failure,
no rollback and no replay.

D20 does not authorize this mutation until those gates close.

## 14. Publication and irreversible mutation policy

Read-only Workspace lookup and Entity property reads are not irreversible host
effects and may execute in provisional/cold-module contexts subject to facade
publication lifetime above.

The following classes are committed-only host mutations:

- future Spawn;
- future Destroy;
- future Position/transform mutation;
- future entity-specific world mutation.

They must use the **same** corrected mutation-eligibility predicate established by
Player-1F-C:

> there is an existing nonprovisional admitted operation and there is no active
> publication scope.

No dependency call, already-active foreign domain, pcall, nested module or scheduler
hop may launder that restriction. Deferred work becomes eligible only after the
publication that created it has committed.

No separate world provisional model is introduced.

## 15. I12 and reentrancy

I12 applies to future host-backed world mutations in exactly the same general form it
already applies to gameplay mutation.

CarbonLuau remains responsible for:

- selecting a supported adapter;
- validating all input and exact lifetime;
- not knowingly passing invalid host premises;
- bounds;
- resource responsibility for any host resource returned to it;
- verification;
- normal host callback outcomes;
- correct failure classification.

Another trusted in-process component qualifies as I12 external interference only when
there is evidence that it synchronously changed operation-relevant host state in a
way that invalidated the qualified premises. An unexplained failure is not evidence
of I12.

Read-only Position/Id/Prefab access intentionally avoids invoking hookable mutation
paths.

For Spawn, Destroy and future mutations, any Rust/Carbon callback caused
synchronously by the operation may not reenter Luau. CarbonLuau-owned resulting
events are copied/admitted as bounded work and execute later under I4 if/when such
events are designed.

## 16. Error model

Foundation 1 uses ordinary CarbonLuau error conventions rather than Result<T,E>.

| Situation | Luau behavior |
|---|---|
| malformed/noncanonical entity ID | programming error |
| well-formed ID absent from current live BaseEntity registry | \`nil\` |
| Workspace used before world readiness/during shutdown | controlled operational error |
| stale Entity proxy | controlled stale-reference error |
| destroyed/pooled/reused host object discovered during validation | controlled stale-reference error |
| host Position is nonfinite/unrepresentable | controlled host-state error |
| malformed captured prefab state | controlled host-state error |
| future mutation rejected before COMMIT | operation-specific controlled rejection/error |
| future failure after COMMIT | controlled host-operation error; state may have changed |

A stale Entity does not return nil properties or fabricated defaults. Equality remains
the one operation that does not require a live host reference.

## 17. Hard safety bounds

Foundation 1 canonical hard bounds are intentionally small because the surface is
small.

### Public input

- Entity ID: 1..20 ASCII digits, canonical nonzero unsigned-64 decimal.
- No Foundation 1 user-supplied prefab input.
- One entity may be returned by one \`GetEntityById\` call.

### Host work

- \`GetEntityById\`: one keyed registry lookup plus constant validation; no registry
  iteration.
- \`Entity.Id\` / \`Prefab\`: constant lifetime validation plus bounded snapshot
  return.
- \`Entity.Position\`: constant lifetime validation plus one root world-position read.
- No lifecycle event queue exists in Foundation 1.
- No enumeration/spatial result array exists in Foundation 1.
- No Spawn/Destroy request exists in Foundation 1.

### Prefab snapshot bound

The implementation must establish a target-qualified maximum retained prefab string
length before shipping Entity-1B. D20 sets the canonical design ceiling at **512
UTF-8 bytes with no NUL**; encountering host state beyond the bound is a controlled
host-state failure and is never silently truncated. If exact target evidence proves a
smaller complete bound, implementation may adopt the smaller number. Raising the
canonical 512-byte ceiling requires an explicit D20/compatibility review.

### Identity bookkeeping

Foundation 1 creates identity records only for entities actually observed through
Workspace. It does not pre-index every world entity.

There is no separate high fixed "maximum Entity proxies" compatibility promise.
Existing VM memory limits, domain lifetime and ordinary Luau GC remain the retained
memory authority. Implementation must prove that discarded/stale proxies do not
cause the host identity table to grow permanently and that token/record cleanup is
bounded. The private monotonically increasing lifetime-token counter is never reused;
counter exhaustion fails closed.

Future collection/query/event APIs must add explicit inspected-work, returned-result
and queued-event bounds before D20 can be expanded.

## 18. Performance model

The public performance model is deliberately predictable:

- ID lookup is keyed, not a scan;
- no per-call O(all world entities) operation exists;
- no CarbonLuau world index is maintained;
- Position is a direct owner-thread host read;
- Entity proxy construction does not enumerate or inspect unrelated entities;
- repeated reads still revalidate exact lifetime rather than trusting a stale pointer.

All host state access remains on the I4 owner thread. No background worker may touch
Unity Transform, BaseEntity or the server registry.

## 19. Future specialized-capability seam

Foundation 1 chooses **typed specialized facades composed over the same exact Entity
lifetime** as the future direction.

Future surfaces such as storage, doors, building blocks, vehicles or NPCs must not
mirror the Rust inheritance tree or expose every BaseEntity subclass method. A future
specialized facade:

- reuses the same private exact-lifetime token;
- adds only a project-defined capability contract;
- performs an explicit adapter/type/capability qualification;
- remains domain/publication-bound;
- exposes no raw host object.

The exact author-facing acquisition spelling for those future facades is deferred
until the first real capability is designed. What is decided now is the architectural
direction: **typed CarbonLuau capability facades, not reflection, host-class
inheritance or generic \`CallMethod\`.**

## 20. Implementation routing

Architecture adoption alone authorizes no production API.

### Entity-1A — exact identity/lifetime substrate

Implement internal owner-thread world-readiness and observed-entity lifetime records,
including:

- private monotonic lifetime tokens;
- weak/refcounted host identity;
- exact ID/object/registry validation;
- pool/reuse/id-reuse negative cases;
- domain/publication binding;
- stale latch;
- exact-lifetime equality;
- no public Workspace/Entity surface yet if the substrate cannot be isolated cleanly.

Required tests include managed fake registry reuse, same-ID/new-object, same-object/
new-ID, destroy/fake-null, failed module/candidate leakage, cross-domain sharing,
successful/failed replacement, provider unload and VM recovery.

### Entity-1B — read-only Workspace/Entity surface

Add only:

~~~luau
game:GetService("Workspace")
Workspace:GetEntityById(Id)
Entity.Id
Entity.Prefab
Entity.Position
~~~

Qualify exact Rust build \`25353106\` plus Carbon \`2.0.259\` for:

- keyed registry lookup;
- live BaseEntity discrimination;
- ID/prefab capture;
- root world Transform position;
- destroyed/removal validation;
- startup/hotload/shutdown behavior;
- prefab bound;
- representative parented entities;
- high-churn create/kill/reuse behavior without adding public mutation.

No collection, Signal, Spawn or Destroy is pulled into 1B to make tests convenient.

### Entity-1C — combined lifecycle/scale/public closure

Close:

- root/addon cross-domain sharing;
- failed/successful replacement;
- provider unload;
- CarbonLuau reload;
- fatal recovery with no replay;
- shutdown;
- save/load/restart semantics;
- proxy/identity-table churn and memory;
- API metadata/generation;
- public docs/examples;
- compatibility and release planning.

Only Entity-1C may assign the implemented surface to a future package/scripting
identity.

### Later foundations

Enumeration/prefab query, spatial query, lifecycle Signals, Spawn, Destroy and
specialized capabilities require separate explicit architecture/qualification work.
They are not hidden Entity-1D/1E implementation authorization under this D20.

## 21. Version planning

This architecture is designed after the published 0.4.0 line.

D20 adoption changes **no** package version, scripting API identity, native ABI,
provider protocol, package schema or Luau pin. It does not retroactively add
Workspace/Entity to \`0.4.0-experimental\`.

If Entity-1A through 1C later complete as an additive release, \`0.5.0-experimental\`
is the natural release-planning candidate, but D20 intentionally leaves that identity
unassigned until implementation/public-closure evidence exists.

## 22. Explicitly deferred

The following remain outside Foundation 1:

- whole-world enumeration;
- prefab-filtered enumeration;
- spatial/radius queries;
- lifecycle Signals;
- Spawn;
- Destroy;
- prefab-existence service calls;
- Name/ShortPrefabName;
- OwnerId;
- IsValid;
- health/damage;
- inventories/containers;
- doors/locks;
- building privilege;
- vehicles;
- NPC AI;
- entity ownership mutation;
- Parent/Children;
- parenting mutation;
- rotation/CFrame;
- velocity/physics;
- Position writes/teleport;
- arbitrary RPC/client commands;
- network-group control;
- raw hooks/delegates;
- raw BaseEntity/BaseNetworkable/Transform/GameObject/UnityEngine.Object;
- serverEntities collection exposure;
- arbitrary prefab/resource loading;
- generic reflection/property access/CallMethod.

## 23. Resolved decision matrix

1. **Service name:** \`Workspace\`.
2. **Generic abstraction:** one project-owned \`Entity\` facade over live registered
   BaseEntity world objects.
3. **Exact lifetime:** private monotonic EntityLifetimeToken plus exact managed object,
   captured ID, registry occupancy and domain/VM/publication checks.
4. **Stale behavior:** permanent fail-closed host access; equality only remains.
5. **Equality:** same CarbonLuau host instance + same exact lifetime token.
6. **Public IDs:** yes, because keyed interop/lookup is the only bounded Foundation 1
   discovery primitive.
7. **ID representation:** canonical nonzero decimal string for current-world lookup;
   not persistence or lifetime identity.
8. **Position:** read-only BaseEntity root world Transform.position -> D18 Vector3.
9. **Prefab/name:** \`Prefab\` is full canonical PrefabName; Name and ShortPrefabName
   deferred.
10. **Lookup:** \`Workspace:GetEntityById(string) -> Entity?\`, keyed/no scan.
11. **Enumeration/query:** none in Foundation 1; deferred rather than scan the world.
12. **Spatial query:** deferred pending exact efficient inclusion semantics.
13. **Lifecycle Signals:** deferred; current hook ordering is not adopted as a simple
    symmetric public lifecycle.
14. **Existing entities:** queried current state on demand; no synthetic Added events.
15. **Host-owned ownership:** Rust world owns the entity; CarbonLuau owns only facade
    state.
16. **Spawned ownership:** if future Spawn succeeds, ownership transfers to ordinary
    Rust world state; no addon-unload auto-destroy.
17. **Domain sharing:** proxy may cross domains; ResourceOwner/origin and exact
    lifetime do not transfer.
18. **Replacement:** retiring-origin proxy stales; world entity survives; new domain
    may resolve fresh proxy; old proxy never retargets.
19. **Fatal recovery:** old proxies die with VM; world remains; fresh lookup after
    reconstruction; no replay.
20. **Save/restart:** no proxy/identity promise across restart; public Id is not a
    persistence key.
21. **Spawn:** deferred from Foundation 1.
22. **Spawn adapter if later added:** one hidden Create+Spawn adapter with canonical
    prefab validation, explicit COMMIT, host resource responsibility and VERIFY.
23. **Destroy:** deferred from Foundation 1.
24. **Destroy adapter if later added:** qualified normal Kill path, hook-aware COMMIT
    and post-call verification; no raw Unity destroy.
25. **Mutation publication:** future Spawn/Destroy/Position writes use Player-1F-C's
    corrected nonprovisional-admission + no-active-publication predicate.
26. **I12:** general gameplay interference boundary applies only with concrete
    operation-relevant mutation evidence; it excuses no adapter/verification bug.
27. **Bounds:** 20-byte canonical ID, 512-byte prefab ceiling, one keyed result,
    constant work; no unbounded collections.
28. **Performance:** no O(all) public call and no Foundation 1 world index.
29. **Capability seam:** future typed project-owned specialized facades composed over
    the same exact lifetime; no Rust hierarchy mirror/reflection.
30. **Implementation phases:** Entity-1A identity substrate, Entity-1B read-only
    public surface/exact-host qualification, Entity-1C lifecycle/scale/public closure;
    mutation/query expansions require later explicit foundations.

## 24. Stop conditions retained for implementation

Entity-1A/1B stop rather than weaken D20 if:

- the target registry cannot provide a keyed lookup without scanning;
- exact object/ID/registry validation cannot distinguish an ID-reused replacement;
- destroyed/pool reuse cannot be made fail-closed without retaining raw unsafe host
  pointers;
- prefab identity cannot be bounded/canonicalized without exposing host resources;
- Position cannot be read as a defensible world-space BaseEntity root observation;
- domain/publication failure can leak a usable newly-created Entity proxy;
- implementing the public surface requires BaseEntity/reflection/serverEntities
  exposure.

Later query work stops rather than returning misleading partial "all" results if both
work and result bounds cannot be stated.

Later Spawn/Destroy work stops rather than ships if exact-build callback/resource/
verification behavior cannot distinguish safe pre-COMMIT rejection from
post-COMMIT uncertainty.

## 25. Validation required before public implementation

Architecture adoption is documentation/policy work only. It does not claim that the
runtime currently contains Workspace or Entity.

Entity-1 implementation must add target-specific fixtures rather than relying only on
ecosystem convention:

- exact Windows/Linux assembly/source inspection where practical;
- keyed lookup and BaseEntity discrimination;
- representative prefab/position/parenting reads;
- create/kill/reuse churn observed without public mutation;
- same-ID/new-object and same-object/new-ID injected adapter tests;
- server startup/hotload/shutdown;
- save/load observations where relevant;
- failed candidate/cold-module publication;
- root/addon/provider replacement;
- VM fatal recovery/reload;
- identity-table memory/churn bounds;
- existing native/managed containment regressions.

Current upstream sources are architecture evidence. Exact-build implementation claims
must be recorded separately in Entity-1A/1B/1C qualification documents.

No production Workspace/Entity code is authorized or implemented by this document.
