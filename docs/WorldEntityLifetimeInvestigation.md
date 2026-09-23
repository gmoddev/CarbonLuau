# Entity-1A authoritative lifecycle investigation

Research only, 2026-09-22 (EDT); CarbonLuau baseline
`72efcf253f46c5d4499bd7f2b5a69ae4ceb23946`. This follows the preserved
[initial Entity-1A investigation](WorldEntityFoundation1A.md), not an implementation
of [D20](Invariants.md#d20--worldentity-foundation-1).

## 1. Verdict and minimum architecture question

**Canonical disposition adopted 2026-09-23:** D20 is
**HOST-PRIMITIVE-GATED / DEFERRED**, with Entity-1A **BLOCKED**. The user resolved
the question below in favor of waiting for a supported authoritative host primitive,
not adding Harmony/detours or weakening exact lifetime. Investigation results below
remain historical evidence; their passing checks do not constitute Entity PASS.

**LIFETIME PROOF BLOCKED — D20 REQUIRES REDESIGN.**

No supported, complete incarnation/retirement mechanism was established on the
qualified Rust `25353106` / Carbon `2.0.259` target. This verdict means the proposed
adapter proof is inadequate, not that exact-lifetime facades are theoretically
impossible. D20's no-retargeting guarantee is unchanged. No I12 waiver, weaker
identity, invasive instrumentation, production Entity substrate, Workspace API or
Entity-1B work is adopted.

The minimum architecture question is: **must Entity support wait for a
host-supported incarnation or versioned-registry/retirement primitive, or may a
separately reviewed instrumentation dependency be introduced?** Waiting for a
supported primitive is the recommended path. Existing kill/spawn notifications
alone do not cover the stated registry-removal/reinsertion contract. Do not turn
this into a choice about tolerating rare stale-reference retargeting.

## 2. Scope and reproducible evidence

The initial record owns the retained Windows/Linux `Assembly-CSharp.dll` and
Linux `Facepunch.Network.dll` hashes. Additional inspected artifacts:

| Artifact | SHA-256 |
|---|---|
| `Rust.Global.dll` | `7da81515e138d6b702e59e3cecbc3296f62dcb51f7e645a0853a8b37c4b598e7` |
| `Carbon.Hooks.Base.dll` | `2a10b01aa707c870b4e17ba341b376466e05757bf22680d79d5227205d9c922e` |
| `Carbon.Hooks.Community.dll` | `65992f91bd1b416a01f55a69c156c8e5160311a691127dc4c2d14b3750f5b40e` |
| `Carbon.Hooks.Oxide.dll` | `028d8ee801937d97447a71a459684281cd44653b59416fd6968b63fe097d33c0` |

The extended [structural checker](../tools/Test-EntityLifetimeEvidence.ps1) passed
**1,029 assertions** on BigKVM: selected IL/visibility comparison for **20 methods**
across the Windows/Linux Rust assemblies, ordering/registry/pooling checks, five
snapshot-model assertions, and a negative metadata check for each shipped hook
patch. The large assertion count mostly reflects metadata inventory; it does not
mean 1,024 independently tested lifecycle scenarios. Pass both optional
`-CarbonHookDirectory` and `-RustGlobalAssembly` arguments to reproduce this full
run. Without them, only the narrower structural/model subset runs.

The inspection read all fields on BaseNetworkable/BaseEntity, the Poolable and
network-object implementations, both registries, selected save/load paths, and
the three shipped hook assemblies. Decompilation and Mono.Cecil were read-only;
game code was not executed by the structural checker. No proprietary decompiled
source is committed. Working copies remain under ignored `dist/entity1a/evidence`
and `/srv/codex/CarbonLuauEntity1A20260922/evidence`.

Current upstream Carbon `main` was separately resolved through GitHub to
`08bcfd854b6692e4566a859d050eb1ccd8f9dc9e`. Its
[OnEntitySpawn source](https://github.com/CarbonCommunity/Carbon/blob/08bcfd854b6692e4566a859d050eb1ccd8f9dc9e/src/Carbon.Hooks/Carbon.Hooks.Community/src/Entity/OnEntitySpawn.cs)
agrees with the inspected prefix. Current source discovery is supplementary;
the shipped binaries, not current upstream HEAD, define the target examined here.

## 3. Why the sampled proof fails

Object O, ID X, prefab P and registry[X] = O can all match before and after an
unobserved retirement/reincarnation. Host/domain/VM/publication authority need not
change during that interval. A CarbonLuau token or permanent stale latch cannot
detect an event that never reaches it. The same problem applies to a removed and
reinserted occupancy under D20's permanently retired registry-lifetime rule.

Different-object/same-ID and same-object/different-ID changes remain detectable.
Those tests do not cover the combined ABA case. Assigning a fresh token every
access would break same-lifetime reuse/equality, not solve it.

## 4. Pooling, IDs and candidate fields

### Exact Rust pooling path

`GameManager.Retire` returns eligible objects to `PrefabPoolCollection` when the
world is loaded, not unloading, pooling is enabled, and `SupportsPooling` succeeds.
The latter checks for a Poolable with nonzero prefab ID. `PrefabPool.Push` stores
that component; `Pop` returns its existing GameObject after repositioning and
`LeavePool`. There is no new BaseEntity construction on that branch. Prefab
identity can therefore remain identical. `Poolable.ServerCount == 0` is not proof
pooling cannot occur: the inspected Push/Pop path does not reject at that capacity.

**Important correction to the initial concern:** generic pooling code is not proof
that an ordinary dedicated-server BaseEntity retains a Poolable. Poolable implements
`IClientComponent`; server prefab preprocessing normally strips client-only
components. RealmedRemove/preprocessing options can affect stripping, so this is
not a universal no-pooling theorem either. The first controlled live fixture found
the wooden-box Poolable absent and stopped rather than claiming reuse. No host
component was injected to manufacture a successful pooling test.

Poolable records component enable-state/hierarchy snapshots and restores them; it
does not maintain an entity incarnation counter. Pool statistics (`Pushed`,
`Popped`, `Missed`) are per prefab pool, not per entity lifetime.

### Network allocation and restoration

`Network.Server.TakeUID` increments a UInt64 high-water counter; `ReturnUID` does
nothing. At exhaustion, the inspected code requests server stop and does not wrap
into a fresh usable ID. Ordinary fresh allocation therefore does not immediately
recycle a killed entity's ID.

`CreateNetworkable(NetworkableId)` instead assigns the supplied ID and advances the
high-water mark only if necessary. `BaseNetworkable.InitLoad` uses that overload
and registers it. `EntityRealm.RegisterID` overwrites an existing entry for the
same key. Thus the API does not guarantee that a supplied ID has never been used
before in the process. SaveRestore restores serialized IDs through InitLoad;
call-site inspection also found InitLoad in Rust copy/paste and Nexus transfer
code. This is not a claim that those callers routinely select retired IDs.

### Candidate identity evidence

| Candidate | Verdict |
|---|---|
| `creationFrame` | Private frame number, assigned at Spawn; multiple incarnations may occur in one frame. Not a unique generation. |
| `isSpawned` / `IsFullySpawned()` | Boolean; reset on server destroy and set on Spawn. A missed transition can return to true. |
| `IsDestroyed` | Set on destruction and cleared in SpawnShared. A missed transition can return to false. |
| `net` reference | Networkable itself is pooled; reference identity is not an incarnation counter. |
| Network ID / registry occupancy / prefab | Useful necessary checks, insufficient together for unobserved ABA. |
| Spawn timestamp / creation sequence / destroy generation | No universal monotonically changing field established in the inspected base types. Subclass-specific fields cannot establish the generic contract. |
| Network group, visibility, position, transfer timers, flags | Mutable during a live entity lifetime and repeatable across incarnations. Not lifetime identities. |
| TransformHandle | Identifies the existing transform; pooling reuses that transform. No entity-incarnation guarantee established. |
| Registry collection version/counter | No supported per-occupancy generation established. A global mutation count would also change for unrelated entities and cannot attribute retirement to this one. |

## 5. Unity and managed/native identity verdicts

Unity describes [GetInstanceID](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.GetInstanceID.html)
as identity of the in-memory object, not a Rust entity incarnation. Pooling keeps
the Unity object; an incarnation need not allocate a new Unity identity. No
process-history no-reuse promise is adopted, and identities do not persist across
sessions. Even assuming unique IDs for concurrently existing Unity objects would
not close the pooled-same-object case.

| Mechanism | Why it cannot supply the missing proof |
|---|---|
| Managed reference equality | The exact same object can be reused. |
| `RuntimeHelpers.GetHashCode` / object hash | Hashes are not unique lifetime tokens; same-object reuse need not change the hash. Overrides can be mutable/colliding. |
| GCHandle | Identifies a handle to an object, not the object's Rust incarnation. Allocating a new handle proves a new observation, not a new entity lifetime; strong/pinned handles also retain objects. |
| WeakReference | Appropriate non-owning reachability mechanism, but it can still resolve the same pooled object. |
| Native pointer | Address/object identity can persist through pooling or be reused after free. Unsupported raw-pointer retention would add hazards, not a proof. |
| Unity InstanceID | Useful object diagnostic; not an incarnation identifier. |

These mechanisms may help implement bounded references after a real incarnation
proof exists. None creates that proof alone or in combination with the sampled
fields already shown insufficient.

## 6. Exact transition ordering and hook coverage

### Spawn and load

The normal creation path obtains a pooled or new GameObject, performs activation
setup, and returns its BaseEntity. Carbon `OnEntitySpawn` is a prefix on Spawn.
The Rust body then runs SpawnShared (clearing destroyed state), assigns a network
object if absent, captures creationFrame, runs initialization including
`ServerInit -> RegisterID`, sets up networking/groups, and sets isSpawned.
Carbon inserts `OnEntitySpawned` before the immediate network-update tail.

Save loading calls CreateEntity, InitLoad (ID assignment and registry insertion),
PreServerLoad, then later Spawn and Load. `OnEntityLoaded` patches Load, not
InitLoad or every insertion. Registry occupancy may therefore precede Spawn.
Neither insertion alone nor a spawn callback should be treated as a completed
generic initialization transaction.

A fresh authoritative incarnation stamp would have to change **before the object
can be accepted as a new live registry occupant**, including restoration paths.
OnEntitySpawn could invalidate a previously observed object for normal Spawn
calls, but it does not observe every registry discontinuity, nor establish the
complete lifecycle contract required here. OnEntitySpawned is later still and
cannot retroactively invalidate authority used by earlier synchronous host code.

### Kill and retirement

The shipped OnEntityKill transpiler inserts a hook call before the normal
retirement body; a non-null return exits Kill. Registry removal and pooling have
not yet happened. Therefore invocation is not proof of completed retirement.
Retiring a token unconditionally at that hook would wrongly classify a vetoed
kill as actual retirement; deferring a snapshot to the next frame can miss reuse.

The ordinary non-vetoed path runs OnKilled, DoEntityDestroy (sets IsDestroyed,
shared/server teardown and transform-registry removal), client termination,
TerminateOnServer (server-registry removal, network free, deactivation), then
EntityDestroy (ResetState and GameManager.Retire). Callbacks/failures can interrupt
this sequence. A timestamp sampled after a notification does not make it atomic.

No generic post-retirement patch is present in the inspected hook metadata.
`OnEntityDestroy` hooks for Bradley/CH47 are subclass death behavior, not universal
BaseEntity retirement. The construction-cancellation patch can also call
DoServerDestroy/TerminateOnServer/EntityDestroy directly in its alternate path;
it is additional evidence against equating every teardown with OnEntityKill.

### Registries and other paths

`EntityRealm.UnregisterID` directly removes the current ID; RegisterID directly
adds/replaces it. Neither exposes a CLR event or invokes a plugin callback in the
inspected body. `Rust.Registry.Entity` is a separate transform-to-IEntity dictionary
with direct register/remove operations, also without CLR events. No shipped
Carbon patch targets these registries, Poolable, prefab pools, InitLoad,
TerminateOnServer or EntityDestroy in the inspected metadata.

Consequently a tracker fed only by the identified Carbon hooks has no complete
input for remove/reinsert or replacement/restoration that bypasses those hooks.
This conclusion does not declare every arbitrary plugin field edit supported;
it identifies a missing proof for the requested host operations. I12 is not used
to waive that gap.

Direct Unity destruction is also not the same as Kill. When it actually destroys
the object, Unity invalidity provides a fail-closed access check, but a component's
OnDestroy notification is not a general registry-incarnation stream. Attaching
new tracking components would instrument host objects and still miss pure registry
remove/reinsert. No such components or detours are introduced.

## 7. Lazy observation, reload, shutdown and bounded tracking

Lazy observation of an already-existing entity would be sound **if** a complete
future-retirement notification or host incarnation stamp existed. Historical
spawn events are unnecessary; first observation could assign the local token and
later authoritative retirement would permanently latch it. The missing part is
the complete authoritative input, not discovery of entities that predate hotload.

Current CarbonLuau `ScriptHost.ReleaseVm` releases addon/root domains before VM
disposal, and `NativeRuntime.Dispose` destroys remaining VMs before unloading the
library. NativeRuntime gives each host a monotonic HostLifetimeId. Old facade
authority must retire with these existing boundaries. Transitions while CarbonLuau
is fully unloaded need no persistent history: a reloaded host cannot accept old
VM values/tokens. This is source inspection, not a new Entity-specific reload test.

Server shutdown/world-unavailable state should retire/disable current facade
authority, not report an empty world. Carbon has shutdown hooks, but they do not
repair within-world registry ABA. No world shutdown or restart identity mapping
is implemented by this research.

If the missing primitive becomes available, use a private owner-thread map of
**observed** objects with weak/refcounted host references and permanent retirement
of local tokens. Notifications for unobserved objects should do constant lookup
work without allocating records. Release records with facade/domain/VM/host
lifetime and never reset the host's monotonic token counter. Such a tracker can
remain bounded by retained observed facades; it needs no startup enumeration,
world mirror or per-frame scan. This is a conditional design, not a qualified
implementation or a public Signal proposal.

## 8. Rejected workarounds and implementation handoff

- A kill-only tracker misses other paths and confuses veto with completed death.
- A spawn-only tracker misses registry removal/reinsertion without Spawn.
- Polling on access retains the same missing-history problem; polling each frame
  additionally violates the task's no-polling constraint and still misses in-frame ABA.
- Patching every lifecycle/registry method with Harmony/detours or injecting state
  might provide new evidence, but changes the host adaptation contract and requires
  coverage, ordering, failure, compatibility and patch-ownership qualification.
  Carbon's own use of Harmony is not authorization for CarbonLuau to add patches.
- Weak handles, hashes, time/frame numbers and statistically unlikely ID reuse
  cannot replace a universal incarnation/retirement guarantee.

No production Entity-1A change is ready to implement. If an authoritative primitive
is obtained, the exact follow-on work is: capture it at lazy observation; add it
to the single validation predicate; retire the existing local token synchronously
at proven lifetime discontinuity; give later observations fresh tokens; integrate
facade state into existing session/publication/domain/VM teardown; qualify the
matrix below. Equality stays host-plus-token without host access. D20's mechanism
description would then need a narrow canonical amendment identifying the newly
qualified primitive, without weakening no-retargeting.

## 9. Qualification matrix and evidence limits

| Scenario | Required future outcome | Evidence in this investigation |
|---|---|---|
| Repeated same lifetime, same object/ID/prefab | Same token | Snapshot model only; no production token implementation. |
| Actual retirement then same object/ID/prefab | Old stale, new token, old != new | Snapshot counterexample; live fixture status recorded below. |
| Same object/new ID | Old stale | Model check; live ordinary allocator gave a new ID to a different object, not this combined case. |
| Different object/same ID | Old stale, fresh token | Model and forced live InitLoad restoration; production tokens unimplemented. |
| Registry remove/reinsert | Old token never revives | Live path bypassed tested hooks; no authoritative token invalidation mechanism established. |
| Kill veto | Do not claim retirement | Transpiler inspection and live veto preserved keyed occupancy. |
| Pool reuse | Fresh incarnation when actually retired | Generic pool code inspected; live wooden box had no Poolable and did not reuse its object. |
| Failed/partial spawn or kill | Fail closed without fabricated completed lifetime | Static ordering only; not a full callback-fault matrix. |
| Pre-existing entity / hotload | Lazy observation, future retirement covered | Conditional design only. |
| CarbonLuau reload / fatal recovery | Old authority dead; no cross-host identity | Existing teardown source inspected; no new Entity fixture. |
| World shutdown / save-restart | Fail closed; no persisted token | Static lifecycle reasoning, not a live restart qualification. |
| Bounded retention | Records return to baseline; no world index | Conditional design only; no registry stress PASS. |

## 10. Live evidence

The research fixture is [CarbonLuau.EntityLifetimeEvidence.cs](../tests/live/CarbonLuau.EntityLifetimeEvidence.cs)
with [Test-EntityLifetimeLinux.py](../tools/Test-EntityLifetimeLinux.py). It creates
only fixture-owned, nonsaving wooden boxes on a copied, isolated server and uses
normal Kill, CreateEntity/Spawn, and an explicitly **forced InitLoad restoration**
of an old ID. It also exercises direct server-registry remove/reinsert. These are
research operations, not a public API or a claim about normal gameplay frequency.

The server uses the pinned target, two CPUs, 6 GiB, loopback binds, no published
ports and Docker `--network=none`; the existing production workloads are untouched.
The runner owns and terminates only its isolated process group. Offline map-upload
and update attempts may produce expected network errors, which are not lifecycle
evidence.

### Actual results

First run, `lifecycle-live-20260923-023212`, exited 1: the fixture's assumption
that the wooden box retained a usable Poolable failed. It stopped immediately,
cleaned up its fixture object, and the runner terminated its isolated server.
That failure is preserved, not converted into pooling evidence.

The fixture was corrected to record pooling availability/reuse instead of
requiring it. Final run, `lifecycle-live-20260923-024024`, exited **0** with **13
checks passed**, after the server reported startup complete:

- Pooling config: enabled, mode 2; wooden-box Poolable absent, SupportsPooling false.
- Spawn prefix: not fully spawned; initial net absent. Spawned callback: fully
  spawned and current keyed occupant.
- Kill hook: destroyed false and registry still occupied. Returning true vetoed
  destruction and preserved occupancy.
- Unregister removed keyed occupancy; Register restored the same object/ID.
  OnEntitySpawn, OnEntitySpawned, OnEntityKill and OnEntityLoaded counters did not
  change during that pair.
- Ordinary non-vetoed Kill returned with destroyed true, net null and key absent.
- The subsequent CreateEntity returned a **different** managed object and a
  different Unity InstanceID; ordinary Spawn gave it a different network ID.
- A further different object accepted the old ID via InitLoad and became its keyed
  occupant before Spawn. Spawn completed with that ID and the same prefab.
- Same-object/same-ID/same-prefab reincarnation was **NOT OBSERVED**. No injected
  Poolable, manual field corruption, detour or fabricated incarnation was used.
- Final fixture cleanup ran before the PASS marker. The runner terminated the
  disposable server; no research server/container remains running.

This is direct Linux controlled-host evidence, not an authenticated-client test,
Windows live result, general pooling theorem, complete hook-order qualification,
or an implemented Entity lifetime proof. The forced explicit-ID restoration and
manual registry test are labeled separately from ordinary gameplay.

Retained final log:
`/srv/codex/CarbonLuauEntity1A20260922/evidence/lifecycle-live-20260923-024024/server.log`;
local ignored copy: `dist/entity1a/evidence/lifecycle-live-final.log`. The first
failed run remains in the neighboring directory. The task-owned 5.6 GiB server
copy is retained for reproducibility; the original worker cache was not modified.
Final log SHA-256:
`aa1d5ac210d10e7e9e25544bb6cb76c613934e87f1286b86af2699c563406fc6`.
Post-run hashes of the runtime Rust assembly and all three hook assemblies matched
the inspected inputs; offline update attempts did not silently change the target.

To reproduce, prepare a **new disposable copy** under a research work directory,
remove other test plugins from that copy, and install the C# fixture as
`runtime/server-linux/carbon/plugins/CarbonLuauEntityLifetimeEvidence.cs`. Run
`python3 tools/Test-EntityLifetimeLinux.py --work <research-copy-parent>` inside
the pinned tool image, with the CPU/memory/network restrictions above. Do not
point the runner at a production server or treat its comments as an access-control
boundary; it starts a real host and the fixture mutates its own test entities.

## 11. Repository disposition

At the conclusion of the investigation (before the separately approved closure):

Initial negative evidence is preserved. This investigation changes only research
fixtures, evidence and routing; no runtime/Core/native code or public metadata.
Package/API/ABI/provider/schema/Luau identities remain unchanged. Runtime regression
and sanitizer suites are not represented as newly run. No false PASS is committed;
research remains uncommitted while the proof is blocked. Entity-1B has not started.
Local PowerShell/Python syntax, evidence-document links, whitespace and unchanged
production/canonical-file checks passed. No new CI revision was pushed. The remote
worker skill kept host inspection and the isolated server off the controlling PC;
CodexLock/apply_patch preserved the original investigation files while extending
their evidence.

## 12. Canonical closure and upstream question

On 2026-09-23 the user authorized committing this negative evidence and minimally
amending D20. The closure commit containing this section adopts the investigation;
it does not claim a successful Entity implementation. The initial 54-check record,
expanded 1,029 structural/model assertions, 13 Linux live checks, first failed
pooling assumption, corrected no-observed-reuse result, fixtures and reproduction
instructions are preserved. Proprietary host assemblies/decompilation and raw
server logs remain outside Git. Entity-1B has not started.

Question prepared for later Facepunch/Carbon outreach (not sent):

> Is there a supported server-side mechanism that uniquely identifies a BaseEntity
> incarnation independently of NetworkableId/object-reference snapshots, or an
> authoritative lifecycle/registry transition that CarbonLuau can observe to know
> an exact entity lifetime has permanently retired before the same validation
> premises can describe another incarnation?

The needed coverage includes registry remove/reinsert, explicit-ID load/restore,
vetoed/partial destruction and lazy observation after hotload. A proposed mechanism
must be qualified on the exact supported host before Entity-1A can reopen.
