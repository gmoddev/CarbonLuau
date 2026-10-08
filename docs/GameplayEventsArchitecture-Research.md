# Gameplay Events, Hooks & Policies: supporting research

Status: research/design input supplied for Gameplay Foundation A. This record is not a canonical register, public API authorization, or qualification claim. Current repository invariants and qualified implementation evidence govern. Later gameplay events, policies, inventory signals and general hook surfaces are not implemented by Foundation A.

The source response is preserved below. Its proposed bounds and later-phase surfaces require separately scoped evidence and authorization.

---

# CarbonLuau — Gameplay Events, Hooks & Policies Architecture

**Document status:** Research/design recommendation; **NOT implemented or qualified**
**Repository:** <https://github.com/gmoddev/CarbonLuau>
**Examined revision:** [`fc71db4ddb2053a98ad3fd65811e64b58f75b150`](https://github.com/gmoddev/CarbonLuau/tree/fc71db4ddb2053a98ad3fd65811e64b58f75b150), `main`, 2026-10-08
**Baseline:** World/Entity Foundations 1 and 2 qualified; package `0.5.0` published; package `0.6.0` release-ready but not published; development scripting API `0.6.0-experimental`.
**Scope:** Architecture, integration, target-host research, contract decisions, staged implementation and qualification. No source modifications, tag, package release, or host experiments performed by this review.

> **Evidence discipline.** This design directly inspected the public CarbonLuau repository and the exact-host findings recorded in its qualification reports. It also consulted public Rust/Oxide and Carbon hook descriptions. The pinned Rust/Carbon assemblies and live target servers were **not independently inspected or exercised during this review**. A documented hook location is *not* a verified exact-build postcondition. Recommendations below explicitly identify proof that remains required.

---

## 1. Executive verdict

**VERDICT: PROCEED WITH A NARROW SIGNAL EXTENSION; DO NOT APPROVE A GENERAL HOOK BUS OR LOOT/INVENTORY MUTATION AS PART OF THE FIRST PHASE.**

The smallest useful progression is:

1. **Gameplay Foundation A — player lifecycle.** Preserve `PlayerAdded` and `PlayerRemoving`. Qualify and add `Players.PlayerDied`; add `Players.PlayerSpawned` **only after** proving a common terminal initial-spawn/respawn state boundary. If that cannot be established, ship the narrower truthful `Players.PlayerRespawned` after its separate proof, or omit spawning from A rather than mislabel it.
2. **Gameplay Foundation B — entity lifecycle.** Reuse the existing complete-Spawn observer for `Workspace.EntitySpawned` (subject to event-specific proof); derive `EntityDied` from qualified post-death transitions; add `EntityDestroyed` **only if** actual successful retirement/removal can be observed, not from `OnEntityKill`.
3. **Gameplay Foundation C — inventory observation.** Private mutation/reconciliation research first. Public `Inventory.ItemAdded`/`ItemRemoved` remain deferred until amounts, transfers and source/destination ownership have reliable committed semantics.
4. **Gameplay Foundation D — declarative loot policy.** Investigate the specific barrel class and default loot pathways. A `Loot:RegisterDropRule` API is an attractive *conditional* design, not an authorized feature. Replacement cannot be inferred from `OnEntityDeath`, `OnEntityKill`, `OnEntityDestroy`, or `OnLootSpawn` alone.

**Release decision:** Preserve the qualified and frozen v0.6.0 release candidate. The safest action is to publish v0.6.0 using its existing, completed evidence, then implement Gameplay A against a subsequently assigned development API/package identity. If the operator explicitly decides events must enter v0.6.0, **reopen the release gate**, update the package/API metadata and run the full combined cross-platform release qualification again. Merely adding an experimental API marker does not qualify or publish the feature.

The public model remains **service-owned `Signal:Connect` for observations, validated declarative policies for gameplay decisions**. A third generic hook framework is neither necessary nor authorized.

## 2. Existing architecture and integration inventory

### Canonical authority to preserve

- [`AGENTS.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/AGENTS.md) routes contributors to [`AICONTEXT.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/AICONTEXT.md). This design adds a specific delta, not a replacement architecture.
- [`docs/Invariants.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/Invariants.md) owns I1–I12, D7 (staged publication), D9 (one shared-VM recovery), D10 (admission and provisional effects), D11 (exact Player connection), D13 (nontransactional inventory PREPARE/COMMIT/VERIFY), D14 (providers/addons), D18 (Player interaction) and D20 (exact Entity lifetime).
- [`docs/Compatibility.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/Compatibility.md) and [`docs/WorldEntityFoundation2C.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/WorldEntityFoundation2C.md) own the pinned platform envelope and World/Entity release-readiness record.
- [`src/CarbonLuau/CarbonLuau.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/CarbonLuau.Carbon.cs) already adapts `OnPlayerConnected` and `OnPlayerDisconnected` to `Gameplay.Event("added"/"removing", PlayerLifetime)`.
- [`src/CarbonLuau/Facade/PlayerDirectory.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Facade/PlayerDirectory.cs) already establishes D11 tokens bound to exact `BasePlayer`, `Network.Connection`, and string UserId; removal preserves safe identity snapshots.
- [`src/CarbonLuau/Facade/FacadeSession.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Facade/FacadeSession.cs) already owns per-domain subscription identities, staging, bounded pending payloads and enqueue/flush. Registration host operation 6 currently accepts only `added`/`removing`; operation 7 disconnects. Extend these whitelisted kinds rather than adding arbitrary hook names.
- [`scripts/bootstrap.luau`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/scripts/bootstrap.luau) implements `Signal`, `Connection`, the `Players` and `Workspace` service tables, and a private event `Dispatch`. That dispatcher currently assumes almost all non-command event payloads are Player-shaped; it **must be made explicitly event-kind-aware** before entity/context events, never simply fed different field layouts.
- [`native/src/facade/FacadeBridge.cpp`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/native/src/facade/FacadeBridge.cpp) owns bounded `cl_domain_event` admission to the existing native callback queue; [`native/src/Runtime.cpp`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/native/src/Runtime.cpp) and [`src/CarbonLuau/Scripts/ScriptHost.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Scripts/ScriptHost.cs) own scheduler deadlines, serialized delivery, frame budgets and VM recovery. **Reuse these.**
- [`src/CarbonLuau/CarbonLuau.EntityLifetime.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/CarbonLuau.EntityLifetime.Carbon.cs) and [`src/CarbonLuau/Facade/EntityLifetimeModel.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Facade/EntityLifetimeModel.cs) implement the qualified continuous observer for a **completed outermost full virtual Spawn** and weak per-epoch entity identity. They **do not implement a universal post-Kill/destruction observer**.
- [`docs/PlayerInteractionFoundation1FC.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/PlayerInteractionFoundation1FC.md), [`docs/PlayerInteractionFoundation1FB.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/PlayerInteractionFoundation1FB.md) and [`docs/InventoryOwnershipFailureReassessment.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/InventoryOwnershipFailureReassessment.md) close the existing bounded Give/Take implementation under nontransactional host conditions; they do not qualify change events.

### System boundary

```text
Rust / Unity operation
    -> actual qualified host callback or completion fence (managed C#)
    -> copy limited scalars + exact private lifetime keys on owner thread
    -> optional bounded per-domain listener fan-out (existing FacadeWorld/Session)
    -> existing cl_domain_event / per-domain scheduler queue
    -> fresh, protected, time-bounded Luau callback admission
    -> service-owned Signal callback (domain resource owner)
```

No host object, `HitInfo`, Unity reference, `Item`, `BasePlayer`, raw `BaseEntity`, delegate, CLR type, or arbitrary Carbon hook name crosses into Luau. The observer may refer to host objects **only during its qualified read window**, never as a retained strong reference in the asynchronous payload. Keep the existing compiled bootstrap and owned C ABI model, rather than adding a competing native event subsystem.

## 3. Final recommended public API

### Accepted ergonomic shape, subject to host proof

```luau
local Players = game:GetService("Players")
local Workspace = game:GetService("Workspace")

-- Existing, unchanged:
Players.PlayerAdded:Connect(function(player) end)
Players.PlayerRemoving:Connect(function(player) end)

-- Gameplay A:
Players.PlayerDied:Connect(function(player, context)
    if context.Killer then
        print(player.Name, "was killed by", context.Killer.Name)
    end
end)

-- Gameplay A only after unified initial/respawn proof:
Players.PlayerSpawned:Connect(function(player, context)
    print(player.Name, "spawned")
end)

-- Gameplay B, each independently gated:
Workspace.EntitySpawned:Connect(function(entity)
    print(entity.Prefab)
end)

Workspace.EntityDied:Connect(function(context)
    print(context.Prefab, context.Position)
end)

Workspace.EntityDestroyed:Connect(function(context)
    print(context.Prefab, context.Id)
end)
```

`Signal:Connect(function)` returns the **existing** `Connection` facade; `Connection:Disconnect()` stays idempotent. Signal instances are read-only service members, not constructible objects. No `OnPlayerDeath(callback)`, `RegisterHook("OnEntityKill")`, `Events:Subscribe(name)`, script hook priorities, reflective hook API or global EventBus.

**Do not publish the above block wholesale.** Each new member is enabled only when its specific evidence gate passes, and API catalog/preview/docs version metadata must accurately reflect availability.

### Explicitly excluded now

- `Workspace.EntityDamaged` as a post-damage event; `OnEntityTakeDamage` describes an **attempt/pre-application interception**, not committed damage.
- `Inventory.ItemAdded`, `Inventory.ItemRemoved` before verified ownership, delta, transfer and merge semantics.
- `Loot:RegisterDropRule` before a prefab-specific, default-loot-safe policy boundary is demonstrated.
- `Player:OnDeath`, `Entity:OnDestroyed` or per-object Signals: these would require extra host lifetime subscriptions, retention and proxy semantics without adequate benefit at this phase.

## 4. Naming decisions

| Concept | Decision | Reason / condition |
|---|---|---|
| Player connection | Keep `PlayerAdded`, `PlayerRemoving` | Already live, do not duplicate. `Removing` means disconnected before callback admission, not a cancellable pre-disconnect callback. |
| Player terminal death | **`Players.PlayerDied`** | Past-tense `Signal` member; not confused with host's *vetoable* `OnPlayerDeath`. `PlayerDeath` optional alternative **rejected** as needless alias. |
| Player activation/spawn | **`Players.PlayerSpawned`**, gated | Must include **qualified eligible initial spawn and subsequent respawn**; may not simply forward `OnPlayerRespawned` and claim broader coverage. |
| Narrow fallback | `Players.PlayerRespawned` (only if needed) | Name exactly the proved post-respawn path if initial spawn cannot be unified. Do not create both in first release by default. |
| Entity successful birth | `Workspace.EntitySpawned` | Must be the previously qualified completed outer Spawn, not `OnEntitySpawned` alone. |
| Entity health death | `Workspace.EntityDied` | A proven final-death path; may precede removal, or removal might never occur. |
| Entity actual removal | `Workspace.EntityDestroyed` | Only after qualified successful world retirement/removal; **not** synonymous with `OnEntityKill` or specialized `OnEntityDestroy`. |
| Damage | Defer `Workspace.EntityDamaged` | Require a post-application delta/commit fence first. |
| Inventory changes | Defer `Inventory` service | No universally reliable committed semantic evidence. |
| Gameplay modification | Conditional `Loot:RegisterDropRule` | Declaration, not a mutable/deferred Signal callback. |

This matches the author's preferred `OnPlayerDeath`/`OnPlayerSpawn` concepts while using existing Signal conventions and avoiding naming collisions with Rust hooks.

## 5. Minimal event context types

Only captured, supported scalars and **exact-lifetime** proxies are allowed. Contexts and nested value objects are frozen/immutable; no writable fields, metatable access to the host, object methods or hidden `HitInfo` wrappers. Do not publish a field merely because it exists in host code: qualify its value and lifecycle at the event boundary.

### Gameplay A target

```luau
export type PlayerDeathContext = {
    Position: Vector3?,  -- event-time finite world-space snapshot, if safely available
    Killer: Player?,      -- only an EXACT observed Player connection lifetime
    KillerId: string?,    -- only a validated human Steam/UserId from qualified HitInfo
}

export type PlayerSpawnContext = {
    Position: Vector3?,  -- event-time world-space sample if qualified
}
```

- No `Cause`, `Weapon`, `Damage`, `DamageType` or `Headshot` in the first shipped schema. Add them in separate, proved extensions. In particular **`Cause = "Player"` from a non-null hit initiator is not authoritative** when explosions, projectiles, traps, fall damage, fire, decay, suicides and environmental paths are involved.
- `Killer` must be materialized from the **captured exact PlayerLifetime**. Do not perform a later user-ID lookup and accidentally attach a reconnected account. It may be nil while `KillerId` remains a legitimate event-time snapshot. Never synthesize a connected proxy for an NPC.
- The victim `player` is the same D11 connection-bound facade semantics as existing Signals. It may be stale/disconnected by deferred delivery: `Name`/`UserId` snapshot reads remain safe; `IsConnected` can be false; mutation requires live revalidation.
- `Position` is nullable rather than fabricating a vector or calling a retired host object after delivery. If qualification establishes a mandatory valid position for the selected event surface, the optional type can later be tightened before the initial public publication, but do not overstate it now.
- A Player spawn context carries **no unproven `IsInitialSpawn`, `Kind`, `RespawnCount`, or `SpawnReason`**. Such metadata requires exact-build state-machine proof, especially sleeping/waking versus respawning.

### Gameplay B target

```luau
export type EntityDeathContext = {
    Id: string,          -- captured canonical network ID at this exact lifetime
    Prefab: string,      -- captured full canonical prefab
    Position: Vector3?, -- safe event-time last observation; NEVER a late Entity read
    Killer: Player?,
    KillerId: string?,
}

export type EntityDestroyedContext = {
    Id: string,
    Prefab: string,
    Position: Vector3?,
}
```

`EntityDied` and `EntityDestroyed` take **snapshot-only contexts**, not an Entity proxy that is expected to remain live. The `Id` field is diagnostic/event correlation, not a reusable object identity. Do not expose the private `SpawnEpoch` or `EntityLifetimeToken`. Internally, a transient private token can connect an entity's death and later actual removal, and can deduplicate hook observations. Public `Cause` is deferred; cause-independent destruction is not automatically damage death.

`EntitySpawned` receives an **Entity proxy** because it is an observation of a newly admitted *live* object, not the last-known record of a destroyed one. Drop its queued callback if the **original** lifetime cannot be revalidated at admission. Never re-acquire by network ID. A rapid spawn-and-kill can therefore produce a death snapshot without a delivered spawn proxy; this is consistent with bounded, deferred observational Signals.

**Payload constraints (proposed, not yet qualified):** Canonical uint64 IDs serialized as ASCII decimal strings; prefab at most the existing 512 UTF-8 bytes; names at most 128 bytes; finite `Vector3` floats; no variable-length damage dictionaries; at most one victim, one optional attacker and one optional entity token per event. Use a typed internal event codec with explicitly checked field count, event-kind table, UTF-8 lengths and version private to this implementation—not an exposed public schema revision mechanism. Maintain the existing maximum 16 KiB host-to-native payload even if stricter event-specific caps are adopted.

## 6. Signals versus policies — two abstractions only

### Signals: passive observational stream

A Signal informs script authors of **an already observed transition**. Event admission copies data; callback execution is deferred and may be dropped if overloaded. No callback return value affects the triggering Rust/Carbon operation. It cannot cancel damage/death/kill, alter loot or authorize reentry during the host call. Domain ownership, `Connection:Disconnect`, staging, failed-candidate rollback, successful replacement and fatal recovery are exactly those of existing Signals.

### Policies: pre-registered, validated decisions

A policy is validated and published while a script/module/domain is allowed to stage CarbonLuau-owned resources under D7. The host uses a compiled immutable policy **at a specifically qualified decision boundary**, synchronously **without executing arbitrary Luau**, and with a deterministic owner/conflict rule. A policy's *declaration* is publication state; its later effect on Rust inventory/loot is an irreversible host operation subject to D10, D13-style post-COMMIT analysis and I12. No arbitrary synchronous script `return false` or raw `HitInfo` mutations.

There is no conceptual third framework. Internal hook adapters and private lifetime observers are implementation details underneath one of these two public concepts.

## 7. Host mapping and proof ledger

**Evidence labels:** **R** = directly inspected CarbonLuau repository code; **Q** = exact-host evidence reported in existing CarbonLuau validation (not reproduced in this review); **P** = public generated Oxide/Carbon hook documentation, not itself a pinned-build guarantee; **U** = unproven target contract requiring host IL/live fixture.

| Desired event or policy | Actual candidate producer / method | Observed phase / semantic risk | Current evidence / disposition |
|---|---|---|---|
| `PlayerAdded` | CarbonLuau `OnPlayerConnected(BasePlayer)` -> `PlayerDirectory.Connect` | Host connection and exact token; delivery deferred; may disconnect before delivery | **R/Q. Existing; unchanged.** |
| `PlayerRemoving` | CarbonLuau `OnPlayerDisconnected(BasePlayer,string)` -> `PlayerDirectory.Disconnect` | Snapshot identity after connection removal | **R/Q. Existing; unchanged.** |
| `PlayerDied` | `BasePlayer.Die(HitInfo)` calls `OnPlayerDeath` **before** base death; `BaseCombatEntity.Die` calls `OnEntityDeath` after health/lifestate death change | `OnPlayerDeath` may be vetoed or wounded instead. Prefer `OnEntityDeath` after actual state transition or an independently proven post-die fence | **P; U exact host. A** |
| `PlayerSpawned` | `ServerMgr.SpawnNewPlayer` -> `OnPlayerSpawn`; `BasePlayer.RespawnAt` -> `OnPlayerRespawned` | `OnPlayerSpawn` early, may be veto/override; `OnPlayerRespawned` late in RespawnAt but some post-hook updates continue, and initial path is separate | **P; U unified state machine. A conditional** |
| `EntitySpawned` | CarbonLuau 24-method Spawn prefix/postfix/finalizer; `OnEntitySpawned` is earlier inside `BaseNetworkable.Spawn` | Only **successful outermost original-and-entire-chain normal completion** can authorize an event, after keyed Entity admission check | **R/Q for private completion; U event attachment/capture. B** |
| `EntityDied` | `OnEntityDeath` in `BaseCombatEntity.Die` and `ResourceEntity.OnDied` | Health/life or killed flag set, **before** possible Kill; may omit special entity types and lead to multiple observations | **P; U per-class proof/dedup. B** |
| `EntityDestroyed` | `OnEntityKill` in `BaseNetworkable.Kill`; private `TerminateOnServer`/`EntityDestroy`/`GameManager.Retire` paths to inspect | `OnEntityKill` is **vetoable pre-kill**, not success. Existing private Entity observer only proves Spawn/lazy invalidity, not universal removal notification | **R/P negative; U qualified post-retire observer. B gated** |
| `EntityDamaged` | `OnEntityTakeDamage`, `IOnBaseCombatEntityHurt`, `IOnBasePlayerAttacked`, ResourceEntity routes | Often **pre-damage and mutable/vetoable**; not actual applied delta | **P; defer** |
| `Inventory.ItemAdded` | `ItemContainer.Insert` -> `OnItemAddedToContainer`; `Item.MoveToContainer` stack/merge path | Called around physical insert but callback may alter state; merge can occur without a new item, owner transfer is later | **R/P; U stable committed boundary. C private** |
| `Inventory.ItemRemoved` | `ItemContainer.Remove` -> `OnItemRemovedFromContainer`, `Item.Remove`, `Item.UseItem`, `Item.Take` | Does not mean consumed/destroyed; could be transfer/split/drop/partial amount | **R/P; U stable delta/owner. C private** |
| Inventory stack delta | `Item.MoveToContainer` -> `OnItemStacked` | Hook may run **before** ownership migration and cleanup; different path from container insert | **P; U composite transaction. C private** |
| Custom barrel loot `Replace` | `LootContainer.SpawnLoot`/`LootFill.DelayFill` -> `OnLootSpawn`; `DropBonusItems` -> `OnBonusItemDrop`/`OnBonusItemDropped` | `OnLootSpawn` affects **filling** an inventory, often after clear, not universal barrel-death drop suppression; bonus scrap/drop has separate paths | **P; U exact prefab/class/atomicity. D gated** |

**Especially important naming trap:** Public Oxide `OnEntityDestroy` is **not** a universal post-removal callback. The documented sources are specialized, **vetoable** `BradleyAPC.OnDied` and `CH47HelicopterAIController.OnDied` paths. Its spelling cannot justify `Workspace.EntityDestroyed`.

### Required 12-point proof record per candidate

For each row promoted from U to qualified, record in a new `docs/GameplayEventsHostEvidence.md`: **(1)** exact source / method signature and full binary hash; **(2)** time relative to action; **(3)** attempted vs committed postcondition; **(4)** supported copied fields; **(5)** repeat, nesting and reentrancy behavior; **(6)** safe deferred admission; **(7)** proxy validity; **(8)** required snapshot/copy boundary; **(9)** observed and worst-case frequency; **(10)** implementation/adaptation effort; **(11)** patch/hook and compatibility fragility; **(12)** exact Windows/Linux qualification receipts. Record **positive and negative traces**. A hook name or a passing mock alone never closes a proof item.

## 8. Timing, delivery and event order

Define a strict event lifecycle:

```text
HOST SOURCE -> QUALIFY POSTCONDITION -> CAPTURE -> FANOUT/ENQUEUE
            -> DOMAIN-PUBLISHED -> NATIVE QUEUE ADMISSION
            -> FRESH LU AU CALLBACK ADMISSION -> CONSUME OR DROP
```

1. **Capture:** The managed owner-thread adapter checks the actual source state at its proved boundary and copies scalar fields plus private weak/exact-lifetime keys. If a source hook is off-thread, it must not read Unity or call Luau there; marshal limited safe data and qualify that the sampled state remains meaningful when the owner thread receives it. Prefer owner-thread direct hooks for first phase.
2. **Listener snapshot:** After the host transition, take a per-domain snapshot of eligible listener **registration IDs** in existing registration order. No recursive Luau call. Each listener is its own scheduler item, retaining existing `Signal:Connect` semantics.
3. **Publication:** Only currently committed active domains receive real host-event admissions. Candidate listeners may be staged during startup, but they receive no real historical events and cannot cause a host effect before commit. On publication, staged registrations become eligible for *future* capture only.
4. **Queue:** Use existing FacadeSession bounded pending queue and existing `cl_domain_event` to schedule private dispatcher calls; never create a second work scheduler. A single source event may generate zero, one or multiple per-listener deliveries subject to quotas.
5. **Delivery:** Before entry, validate the exact host instance, VM generation, domain lifetime, publication witness, listener still registered, and any **live proxy** required by the callback (`Player` as D11 snapshot-aware or `Entity` as D20 revalidated). Validate **original weak token**, never rebind a stale `Entity` from its string `Id`.
6. **Failure:** An ordinary callback throw or rejected yield affects that invocation; unrelated listeners continue while the VM is healthy. Fatal deadline/integrity retirement follows D9 and does **not** replay events. A disconnected listener suppresses not-yet-admitted callbacks.
7. **Order:** Per owning domain, captured events are enqueued in owner-thread capture order; listeners for one event preserve registration order **among admitted events**. This is a **subsequence**, not gap-free delivery. Host source sequencing across different native/Carbon hooks, inter-domain callback start order and what trusted third-party patches observe are not promised until separately proven. Dedupe per exact event source *before* fanout.
8. **No snapshots of history:** No replay of initial online players or existing world entities on subscription; use `Players:GetPlayers()` and qualified `Workspace` discovery for snapshots.
9. **Backpressure:** At most once for each eligible listener/event pair; zero or one invocation, never retry. Under overload, **drop with accounted diagnostics**, not an unbounded spill buffer, host pause, blocking wait, or hidden synchronous work. Do not claim guaranteed receipt or a wall-clock latency SLA.

**Subtle ordering case:** If a Player death and Entity death are two views of the same Rust transition, they may legitimately each notify one service. They are **not duplicates within either Signal**. If both were qualified and subscription exists, correlation uses the original internal event identity/lifetime, not a new attacker/user lookup. If entity Spawned was queued and then retired, its live-proxy delivery is dropped, while a snapshot-only death/removal notification may still deliver.

## 9. Player lifetime integration and edge cases

**Admission predicate:** D11 exact connected host object + network connection + current user ID at **capture time**, without assuming the connection survives the later callback. An observed death for an NPC, untracked sleeper, disconnected player or old account session is not automatically a `Players.PlayerDied` event.

### Required PlayerDied semantics

- Fires for an eligible, tracked human connection when the **final server death state transition** occurs. Do not fire on ordinary wounded entry, down-but-revivable state, healing, sleeping/waking or disconnection alone.
- `OnPlayerDeath` itself is unsuitable as success proof: Rust's `BasePlayer.Die` may choose wounding and its `OnPlayerDeath` interception may suppress the subsequent `base.Die`.
- Candidate reliable source is the later `BaseCombatEntity.Die` / `OnEntityDeath` call after life-state transition. Prove exact `BasePlayer` dispatch behavior, when that call occurs relative to other hooks, all alternate death paths, and whether a plugin can restore/change state before capture.
- A single connected Player can die multiple times over time; use an internal **life/death state-machine latch** keyed by exact D11 connection lifetime and qualified life epoch. Reset only after the verified resurrection/respawn transition, not from a user ID or a transient health observation. Duplicate hook invocations while still dead must not duplicate notification.
- Death can be recorded before disconnect and delivered afterward as a readable stale Player snapshot. If the exact PlayerLifetime never existed at capture time, **do not synthesize PlayerDied from a disconnected player ID**.
- A pre-death final hit may have `HitInfo == null`, indirect sources, NPCs or owners of explosives who no longer have a valid Player connection. Killer fields stay nil unless proven.

### Required PlayerSpawned semantics

- Target meaning: **a completed transition into an eligible alive/spawned server player state**, on a tracked human player connection. Includes initial player activation **and** respawn *only where independently demonstrated*. It is not a generic `BaseEntity.Spawn` event.
- `OnPlayerSpawn` is invoked early in `ServerMgr.SpawnNewPlayer`, before additional life/health setup, and can change the path. It is not a safe notification source by itself.
- `OnPlayerRespawned` is later in `BasePlayer.RespawnAt` and has no return value, but its placement alone proves neither the entire operation has returned normally nor the initial join case. Capture only after an exact-build terminal postcondition (or a qualified complete-method postfix) and ensure the connected token is known.
- **Do not infer** `PlayerSpawned` on sleeper waking, sleeping, respawn UI selection, wound/revive, or a player who never progressed to the active state. Test reconnecting survivors versus brand-new characters and server-side initial spawn before `OnPlayerConnected`.
- If initial spawn cannot be observed with an existing D11 token without retaining or fabricating history, **rename/narrow the first public event to `PlayerRespawned`**, or defer it. Returning a silently incomplete `PlayerSpawned` is unacceptable.
- Candidate domain activation/reload must not replay pre-existing initial spawn/respawn states.

### First-phase acceptance checklist

A successful Player lifecycle implementation requires the test fixture to distinguish: first-time connect/new entity; connection after an already-spawned sleeper; normal death -> respawn; death blocked by hook; wounded -> revive; wounded -> terminal death; suicide; PvP/indirect/environmental death; disconnection during queued death; duplicate death hook; successful/failed candidate reload; one guest player reconnecting with the same UserId but different connection. Include NPC exclusion and a proof of no recursive entry when a trusted plugin triggers nested Rust calls.

## 10. Entity lifetime integration

D20 is **not** merely `networkId + IsDestroyed`. It is a continuously observed, exact-host full-Spawn epoch with a non-reused private token, weak object evidence, bounded prefab capture, current nonzero ID, keyed-registry occupancy and publication-lifetime validation. Preserve every one of those predicates.

- `EntitySpawned`: consider a small **listener notification at the existing outer full-Spawn completion**, after current catalog enrollment and live keyed verification. Never attach to Carbon's early prefix or the generated `OnEntitySpawned` alone. A successful completion that produces an entity **while no committed subscriber exists** does not need retained event data. Do not synthesize events for save-loaded startup objects or hotloaded incumbents. A completed Spawn inside startup reconciliation is only eligible if subscribers were already committed (typically none).
- `EntityDied`: capture the same exact lifetime **before** any host death/kill path invalidates it; after verifying the death state is committed. Use the existing prefab/ID snapshots and a safe position observation if available. Reject unqualified BaseNetworkable subclasses. Ensure `OnEntityDeath` subtype hooks map to distinct actual transitions; dedupe by per-lifetime death latch. This event does not authorize a stale proxy.
- `EntityDestroyed`: require a **new independently qualified** actual-retirement observer; current D20 does not supply one. The old lifetime may retire at a new Spawn or on a failed/partial observation, but that retirement by itself is **not proof that the Rust entity was physically destroyed**. Emit only from verified actual world-removal terminal path, with a pre-retirement captured snapshot linked to the same exact lifetime. If Kill was vetoed, do not emit. If an object is pooled/reused, the old token stays retired; never map its event to the new one.
- Include admin removal, timed decay, building stability failure, combat death, barrel destruction, `Kill()` veto, save/load, plugin-driven Kill, world shutdown, server restart, natural pool recycle and entity load/despawn paths. Distinguish ordinary kill/destruction from plugin unload: CarbonLuau unloading **does not destroy Rust entities and must not emit synthetic EntityDestroyed**.
- Entity signals may intentionally overlap with Player signals for a BasePlayer that is also an admissible BaseEntity, but no single stream may deliver duplicate records for one transition. Document whether Player objects are in `Workspace` semantics; the default proposal is **yes, for any qualified admissible BaseEntity**, not a special silent exclusion.

If universal removal cannot be proven, ship **only** `EntitySpawned` and/or a narrowly qualified `EntityDied`; keep `EntityDestroyed` absent rather than invent a weaker meaning.

## 11. Damage and kill attribution

No Player is presumed to be a killer. `HitInfo.InitiatorPlayer` is an **optional clue**; `Initiator`, weapon/projectile, trap owner, explosive owner and the last damage dealer can be different entities/identities. A death with `HitInfo == null` or no trustworthy initiator has nil attribution. Current public damage interception hooks may alter or veto a hit before application.

**Recommended staged attribution algorithm:**

1. At the qualified committed-death capture boundary, inspect the **same** `HitInfo` actually associated with that death (if present), on the owner thread.
2. If it resolves to a tracked **human** BasePlayer with a valid D11 exact PlayerLifetime at that instant, retain only that private token and a bounded user-ID snapshot. Do not expose the host object. If disconnected before callback, a previously captured `Killer` Player facade may be stale but never retargeted.
3. If an attributable human UserId is present but a live exact token is not verifiable, populate `KillerId` **only if** the path proves that ID is the actual human initiator; do not fabricate a `Killer` proxy.
4. If the initiator is an NPC, an untracked entity, environment, fall, fire, radiation, decay, self-inflicted source or ambiguous owner chain, set `Killer=nil` and (absent separate proof) `KillerId=nil`. Self-death may legitimately yield a self `Killer` if the host initiator proves it; do not hard-code exclusion.
5. Do not look up an attacker by UserId during callback delivery. Do not convert weapon short names/prefab hashes to full weapon identity without exact evidence. Separate physical damage source, attacker and ownership semantics if added later.

Future `DamageType`, `Weapon`, `Cause`, `Attacker: Entity?` and damage amount have **separate evidence gates**; the minimal first version should not carry a 20-field death context.

## 12. Destruction versus death — explicit state machine

```text
COMPLETE SPAWN / ALIVE
    | actual qualified damage-to-final-death transition
    v
DIED (might remain in world, corpse/gib/drop effects may be pending)
    | if and only if actual qualified successful removal
    v
DESTROYED / RETIRED FROM RUST WORLD

ALIVE -------------------------------------------> DESTROYED
          (ordinary admin/timer/scripted removal; no DIED necessary)

ALIVE -> ATTEMPTED KILL -> VETO -> ALIVE
          (neither EntityDied nor EntityDestroyed based on the attempt)
```

`EntityDied` is **not necessarily prior to** every `EntityDestroyed`; there are destroy-only paths. Conversely, `EntityDied` may fire without a later documented removal in the same tick/ever. `OnEntityKill` must never immediately be translated to successful `EntityDestroyed`; a non-null hook result can override the default kill. Similarly public `OnEntityDestroy` is a specialized, vetoable action on certain NPC vehicles, not general retirement.

**Invariant:** A terminal event payload refers to the original immutable snapshot of the original exact incarnation; it never requires a post-destruction property access, resurrects a stale entity, or asserts that vanilla loot was paid out.

## 13. Custom barrel loot and destruction rewards

### Observation use cases accepted, separate from mutation

Once a suitably qualified `Workspace.EntityDied` exists, Luau can log destruction, grant XP through a **separately qualified** gameplay reward API, or react to a barrel death. XP/point state can be stored in an existing DataStore (under its own async/durability limits). `EntityDestroyed` can log proven removal regardless of cause. These are *postevent observations*; they **cannot reliably prevent default loot already generated**. `Player:GiveItem` is an existing bounded InventoryOnly operation, not a qualified way to spawn world drops.

### Why `OnLootSpawn` is not automatically barrel-drop replacement

Public generated Rust hook source shows `LootContainer.SpawnLoot` clears inventory and calls `OnLootSpawn` before its **PopulateLoot** action. `LootFill.DelayFill` has a different fill path. `LootContainer.DropBonusItems` uses separate `OnBonusItemDrop`/`OnBonusItemDropped` paths. None of those excerpts proves which **barrel prefab** routes through them, when its inventory is filled versus destroyed, whether bonus scrap uses that inventory, whether the barrel drops already-existing loot, or whether a plugin can safely replace *all* default items. A plugin example even manually emits `OnEntityDeath` for barrel rewards, illustrating that some legacy plugin behavior can produce synthetic hooks; that is **not** canonical Rust death proof.

### Conditional `Loot` service target

Only after the exact-host gate passes, prefer:

```luau
local Loot = game:GetService("Loot")

Loot:RegisterDropRule({
    Prefab = "assets/...exact-qualified-barrel.prefab",
    Mode = "Replace",
    Drops = {
        { Item = "scrap", Min = 5, Max = 12 },
        { Item = "metal.fragments", Min = 10, Max = 25 },
    },
})
```

This is **reserved design shape, not an API commitment**. The host must identify a supported prefab subset and a real **pre-default-fill or pre-default-drop decision fence** where the complete default path can be suppressed or replaced without partial duplication, unmanaged temporary ownership, or silent loss. Prefer **fill-time policy** if barrels simply release prefilled contents on death; do not assume destruction-time replacement is possible. Do not call Luau from that host path.

### Declarative policy admission, ownership, conflict and failure

- **Registration:** Validated during Luau declaration/publication. Validates exact prefab namespace (same 512-byte full canonical rules), `Mode`, allowed item short names through the existing `Items` directory, min/max integers, maximum drops, total max items, stacking and actual capacity. Reject unknown keys and nested custom functions; reject all supplied raw `Item`/Unity objects. No dynamic Lua functions in `Drops`.
- **Recommended first policy:** `Mode="Replace"` only for a pinned, explicitly allowlisted *loot-fill family* after feasibility is proved. `Add`/`Append`, multiplier, priorities and global wildcard prefabs are **deferred**; adding more modes multiplies combinatorial failures.
- **Conflict rule:** At most **one active `Replace` rule per exact prefab** globally. Candidate may stage a duplicate but commit must detect collision deterministically and fail its **publication** without changing the old rule. Do not expose priorities or promise arbitrary inter-plugin coordination. Root/addon domain lifetime is the policy owner. A provider cannot overwrite another provider's policy merely by registering a later callback.
- **Policy selection:** At the qualified host decision boundary, select one immutable *committed* policy snapshot; the operation does not switch midway if the domain retires. Retirement removes the rule from *future* operation selection. A failed candidate never changes the active rule.
- **Determinism:** Define inclusive min/max and bounded random selection performed exactly once by the managed host, not the VM; do not promise reproducible global RNG across Rust restarts unless a separate seed/source guarantee is proved. No per-hit reroll, mixed rules, order-dependent addon registration or unbounded retries.
- **COMMIT:** Mark before any host action with irreversible effects, which may be **before** creation/clear/suppression. PREPARE may not call hookable acceptance that can mutate state; follow the D13 distinction. Inventory/callback failure after COMMIT is **indeterminate host failure**, never an assertion that the original loot exists or that custom loot was fully delivered. Explicitly account for temporary items, consumed merges, physical spawned drop entities and cleanup, using only qualified cleanup paths.
- **Safety gate:** If suppressing default loot plus delivering all custom drops cannot be made safe enough for the proposed `Replace` success contract, **reject/defer `Replace`**. Do not silently degrade to partial loot, duplicate the normal drops, or fake rollback. A future narrower **configured loot-fill** policy may be viable even if universal barrel destruction replacement is not.

### Required live proof

For exact common barrel prefabs: creation/Spawn/loot fill, population timing, damage-to-zero, default break contents, bonus scrap and tea modifiers, victim/breaker attribution, normal and bypassed kill, world/drop entity creation, and inter-plugin changes. Inject: empty config, invalid item, no capacity, item-create throw/null, acceptance rejection, another plugin changing inventory, rule replacement during in-flight host call, duplicate rule from an addon, candidate failure, and provider unload. Verify **physical world/inventory outcomes**, not log messages alone.

## 14. Inventory event feasibility

### Why two generic callbacks are currently unsound

`OnItemAddedToContainer` and `OnItemRemovedFromContainer` are *physical container method observations*, not a universal player-inventory transaction log. There can be separate changes in source/destination containers, partial stack amount changes, Item source consumption, transfers between players, world drop, container reassignment, stack splitting, merges, item burn/use/craft and item destruction. Some hooks execute **before** migration or cleanup of the transfer. D13 already proves that host item operations can partially mutate and invoke trusted plugins between intermediate states; the qualified `GiveItem` / `TakeItem` **return verification** does not imply a complete global change stream.

### Feasibility decision

**Foundation C is private only at first.** Investigate whether there is a bounded post-commit observation boundary (or sequence of owner-thread physical snapshots) for a **specific restricted scope** such as a connected player's main/belt/wear containers, not arbitrary nested world inventories. If not, do not publish misleading `ItemAdded`/`ItemRemoved`.

Candidate future public surface **only if proven**:

```luau
local Inventory = game:GetService("Inventory")
Inventory.ItemAdded:Connect(function(context) end)
Inventory.ItemRemoved:Connect(function(context) end)
```

A future `context` must specify at least **what the words mean** (net quantity change, physical item insertion/removal, or stable stack delta) and the scoped owning inventory; those are materially different contracts. Recommended future semantics, if feasible, are **confirmed net quantity deltas in a named scoped container** rather than pretending a physical insert hook is item creation. A transfer may generate two independent inventory delta observations; no universal atomic pair or actor-of-transfer claim. Item context is a **bounded scalar snapshot** (shortname, signed or absolute change, owner Player exact lifetime, container enum, optional item UID only if safe); **not** an `Item` proxy.

| Rust case | Insufficient naive interpretation | Required proof |
|---|---|---|
| Create item | Created outside container | Do not emit `Added` until confirmed committed in scope |
| Insert into empty slot | Insert callback occurs | Verify final owner/slot/state after nested callbacks |
| Stack merge | Source amount consumed / destination changed | Account net delta even without new destination item |
| Split | Parent amount decreases, child created | No net player quantity gain from internal rearrangement |
| Transfer between inventories | One removal + one addition | Source/destination correlation; no phantom creation |
| Drop into world | Source unparent then world entity | No `Removed = destroyed` assertion |
| Consume/use | Stack count reduced in place | Capture quantity delta without requiring `Remove` hook |
| Destruction/recycle | Item may already be detached | Separate physical lifetime/quantity accounting |
| Owner change | Container owner may be null/world/NPC | No fabricated Player attribution |

A private event journal over every Item is **not** an approved substitute: it risks unbounded scanning, process-memory retention and UID-reuse ambiguity. Count-style physical observation is bounded by D18's 128-stack Player inventory scope but still needs a verified **when** and **what** contract. Do not add a public `Inventory` service as an empty placeholder.

## 15. Resource accounting and queue limits

**Existing facts:** `FacadePolicy.ListenersPerSignal=128`, `Listeners=256` (per session), `PendingEvents=256` (per session cap), 16,384-byte packed payload ceiling; native per-domain callback queue bounded by configured `MaxQueuedCallbacks` up to 4096; scheduler has bounded frame work and per-callback deadlines. `GetEntitiesInRadiusAsync` separately has 8 global/2 per-domain pending and 1024 units per frame; those are discovery-only limits, **not free budgets for events**. There may be up to 128 live addon registrations by D14. Therefore “256 per domain” multiplied by 129 domains is **not** a safe global event-work bound by itself.

### Proposed event-specific hard admission budget (requires measurement)

| Limit | Recommended initial proposal | Rationale / action on excess |
|---|---:|---|
| Listeners per named Signal per domain | Existing 128 | Fail `Connect` at admission; keep compatibility. |
| Total Signal listeners per domain | Existing **256 shared** | Do not silently expand; adding signals shares the existing total unless a separate evidenced policy change. |
| Per-domain pending managed queue | Existing **256 shared** | Event, GUI and command competition must be tested; bounded reject/drop. |
| Event payload | **2 KiB target**, 16 KiB existing hard transport max | Compact fixed snapshot; reject malformed/oversized, never truncate identity/prefab. |
| Event captures before fanout per frame | Candidate **128 global**, measured | Cap *producer work*, not just eventual callback count. Excess counted/dropped. |
| New event listener deliveries admitted per frame | Candidate **128 global / 32 per domain** | Bound fanout cost and prevent one busy domain monopolizing admission. |
| Aggregate retained event payload bytes | Candidate **2 MiB global managed envelope** | Bound the multiplier of many addon domains; includes completed but undelivered event captures. |
| Per-event callback execution | Existing fresh callback deadline and VM-wide memory cap | Never one synchronous callback that invokes all listeners. |
| Entity Spawn observer overhead with no subscribers | Near-zero, measured | No new permanent unbounded object table or world scan. |

Numbers marked **candidate** are a starting validation target, **not an assertion of current implementation or a public promise**. Make a bounded global budget over all domains **in addition** to existing per-domain queues. Use owner-thread turn counters or another existing cheap frame-budget owner; do not use clock-based blocking or unbounded polling. Where possible, avoid allocations and host data-copy work before confirming any active listeners. A game burst may still execute many *host* callbacks in one tick; cap CarbonLuau's per-callback work (including fanout) and reserve adequate protection for existing GUI/Commands/Discovery.

**Overflow contract:** Events may be dropped with a per-kind/per-domain bounded counter and throttled warning. Do not coalesce PlayerDied with PlayerSpawned into one success-like “latest state” notification. Do not queue an incomplete context. Do not promise exactly once or zero loss; *at-most-once attempt with best effort delivery* is the strongest practical initial claim. Diagnostics should separately count captured/accepted/delivered/rejected/stale/disconnected/oversize and native queue rejections, with rollover-safe saturating counters.

## 16. Publication, addon replacement, provider unload, VM recovery

| Lifecycle action | Required outcome |
|---|---|
| New candidate builds listener during entry | Registration staged under D7; no live host events delivered before commit |
| Nested module `require` raises (even if caller catches) | Any subscription created by failed module scope rolled back; cannot escape as active via shared table |
| Candidate load fails normally | Current committed root/addon remains active, candidate listeners and queued event captures discarded |
| Candidate load succeeds | Atomically make new subscription snapshot eligible, then retire old domain and cancel its undelivered work |
| Root replacement with unrelated addons | Preserve other active addon domains and their event subscriptions unless dependency-graph semantics explicitly retire them |
| Addon replacement / required-dependency rebuild | Exact old domain/subscription identity permanently stale, children reconstructed under existing D14 rules |
| Optional dependency provider unload | Existing consumer may survive per D14, but a **captured foreign host-backed listener/facade** remains owned by its defining retired domain and fails closed; no authority laundering |
| Provider unload | Retire registrations owned by provider and queued callbacks; do not unpatch or interrupt the global Entity Spawn observer if other active users rely on it |
| VM-fatal timeout/integrity | Retire whole VM generation; drop event queues and listener captures; one D9 reconstruction allowance; **no event replay** |
| CarbonLuau plugin unload | Stop intake and tear down subscriptions; existing D20 observer continuity breaks; future world operations require server restart |
| Server shutdown | Stop capture, suppress late notifications; **no synthetic EntityDestroyed for every survivor** |

Subscriptions, staged policies and their immutable captures are **CarbonLuau-owned domain resources**. Root/addon publication is over registration ownership; it does not rollback arbitrary Luau tables or Rust gameplay effects. For service objects exported via shared modules, the resource owner is the defining bound facade, while admission follows the caller's D10 publication context; foreign committed facades must not launder provisional registration, mutation or a new owner identity.

**Important native staging detail:** Existing host op 6/7 participate in native publication control in `FacadeBridge.cpp`, and facade session checkpoints handle listener snapshots. Extend this *same* checkpoint/release mechanism; verify callback ID reuse/exhaustion, dynamic host hook demand on zero/first/last listener, and defensive `Connection:Disconnect` for queued and already-retired listeners.

## 17. Compatibility and host-version contract

- v0.6.0 **existing** pinned Rust target: build `25653776`, protocol `2634.289.1`, revision `166494`; Linux Carbon `2.0.261.0` (`c74c4ca`), Windows Carbon `2.0.262.0` (`8a81d70`), Harmony `2.4.2.0`, plus pinned Rust/Carbon hook DLL hashes. Reuse the recorded exact binary tuple. Do not assume Oxide documentation from a web crawl is that binary's IL.
- Windows x64 and glibc Linux x64 remain the qualification targets. Existing unsupported OS/runtime/host providers do not become supported through a new event API.
- **Dynamic hooks:** Public Carbon docs suggest hook activation/demand patterns, but dynamic hook attach/detach must be qualified under the actual Carbon target. If adding demand shifts patch topology seen by D20's 24-method observer, gate Entity availability and fail closed rather than quietly weakening D20. Use a dedicated test for zero listener and many listener scenarios.
- Each new hook requires cross-OS method/hook location, dispatch signature, ordering and exception containment receipts. Unknown host version or changed binary hash disables the **affected new signal**; it does not automatically disable unrelated Players, persistence or read-only World features, unless shared observer integrity actually changes.
- No change is recommended to the native ABI `1.5`, provider protocol `1.2`, addon schema `1`, bundled Luau pin or package identity **for research**. If implementation extends a managed-private event-kind discriminator / host operation 6, document private protocol changes and run native bridge compatibility tests; do not call a private payload change an ABI bump without a public/exported C ABI change.
- New scripting API introduction identities must reflect actual release (suggest next development version, not retroactive `0.6.0-experimental` after v0.6 frozen). Update canonical metadata comments, generated API catalog, preview/tooling, docs/examples and `Compatibility.md` together after public qualification.
- Entity `Workspace` use retains *initial plugin batch* and *server restart after first hotload or full plugin unload/reload*. Player-only signals do not themselves justify imposing that Entity-specific restart requirement.

## 18. Security, safety and failure analysis

| Threat / failure | Defense / failure scope |
|---|---|
| Lua registers arbitrary Carbon/Rust hook | Only static service `Signal` kinds accepted in managed registration; unknown kind errors |
| Host synchronously calls `OnEntityDeath` during a Luau host mutation | Snapshot/enqueue bounded data; **never invoke Luau recursively**; callbacks run in next normal scheduler admission |
| A hook executes off owner thread | Reject or marshal **data only**; game/Unity/VM reads prohibited before owner-thread verification |
| Destroyed or pooled Entity reused the same network ID | D20 original weak token/epoch + registry occupancy; no lookup-by-ID retargeting |
| Player reconnects with same Steam ID | D11 exact connection token; killer/victim proxies never repoint |
| Untrusted context field/metatable | Private codec with canonical field types, byte/field bounds, no script-provided event payloads, immutable shallow snapshot |
| High-frequency damage or inventory burst | No initial damage/inventory Signal; bounded producer fanout, global/per-domain queue budgets, diagnostics |
| One addon monopolizes event registration | Existing 128/Signal and 256 total per domain plus independently bounded global fanout and managed memory envelope |
| Callback throws, yields, times out | Per-callback protected/interrupt admission; no retry; ordinary error isolated, fatal VM recovery per D9 |
| Failed candidate retains callbacks | D7 publication checkpoint, resource release and stale exact domain witness checks |
| Another plugin vetoes death/Kill | Commit state proof after cancellable point; no phantom `Died`/`Destroyed` |
| Other plugin mutates state mid-operation | I12 exact interference evidence; don't misclassify a normal supported callback, never fake rollback |
| Entity observer patch drift on host update | Existing exact hash/patch topology rejection; new adapter must independently pin any added method/patch |
| Loot policy partial creation after default suppression | Post-COMMIT indeterminate; no success/rollback claim; feature **deferred** if safe replacement boundary cannot be proven |
| Server shutdown causes mass removals | Suppress artificial gameplay destruction notifications and discard queues safely |

Additional tests must verify exception isolation *both ways*: plugin exceptions cannot escape to corrupt CarbonLuau ownership, and script exceptions cannot escape into Rust or the native C ABI. Treat the true host operation as uninterruptible by VM deadline; do not run unbounded work in a host callback because the Luau interrupt will not preempt it.

## 19. Accepted, deferred and rejected features

| Feature | Verdict | Basis |
|---|---|---|
| Extend existing `Players` service with `PlayerDied` | **ACCEPT with exact-host proof gate** | High utility, no new service, host death state candidate |
| `PlayerSpawned` | **CONDITIONALLY ACCEPT** | Requires unified initial + subsequent transition proof; fallback `PlayerRespawned` only if narrower semantics qualify |
| Preserve PlayerAdded/Removing | **ACCEPT unchanged** | Already implemented and qualified |
| `Workspace.EntitySpawned` | **ACCEPT as B target** | Reuses existing complete-Spawn lifetime observer, still needs event-specific capture/admission proof |
| `Workspace.EntityDied` | **ACCEPT as B target, conditional** | Known death hooks, exact subtype coverage and dedupe not yet proved |
| `Workspace.EntityDestroyed` | **DEFER pending actual-removal observer** | Current Kill/Destroy hook names are pre-veto/specialized and insufficient |
| `Workspace.EntityDamaged` | **DEFER** | Pre-damage/veto paths don't prove applied delta; potentially huge frequency |
| `Inventory.ItemAdded/ItemRemoved` | **DEFER** | Insert/remove hooks don't establish logical committed quantity/ownership |
| Declarative `Loot:RegisterDropRule` | **CONDITIONAL D-only target** | Host/gameplay policy feasible only after prefab-specific safe replacement proof |
| `Entity:Destroy`, `Workspace:Spawn`, Player/Item raw host mutation | **OUT OF SCOPE** | D20 deferrals and D13 qualification are separate capabilities |
| Generic `Hooks:On`, `Events:Subscribe(name)`, CLR reflection, dynamic callbacks inside Rust hooks | **REJECT** | Violates I1–I4/I11 simplicity and containment |
| Automatic initial backlog, replay, exactly-once guaranteed event delivery | **REJECT** | Contradicts deferred bounded delivery and D9 |

## 20. Phased roadmap (ranked)

| Phase / size | Author value | Evidence / difficulty | Release target | Hard stop |
|---|---|---|---|---|
| **A — Player lifecycle** (small–medium) | **Highest**; kills, death logs, respawn rewards | Existing Player tokens and Signals; two new host state transitions need pinned proof | Post-v0.6 suggested | No verified final death path or truthful spawn boundary |
| **B1 — EntitySpawned** (medium) | High; entity type observers | Existing D20 full-Spawn fence Q, plus fanout and exact proxy admission tests | Later additive | Observer continuity/patch topology not preserved |
| **B2 — EntityDied** (medium–high) | **Very high**; barrels, XP, kill logs | Death state and subtype mapping U; snapshots and dedupe | Later additive | No complete supported prefab/class subset proof |
| **B3 — EntityDestroyed** (high) | High; cleanup and removal logs | Post-veto actual retirement authority **not present** | Optional later | Only `OnEntityKill`/`OnEntityDestroy` available |
| **C — Inventory observation** (high) | Medium–high; inventory telemetry | Nontransactional host, merge/split/transfer, high frequency | Later private research | Cannot prove a bounded committed scope/delta |
| **D — Loot/drop policy** (very high) | High; barrel customization | Default suppression/bonus item/ownership/rollback unresolved | Last; separate product gate | Cannot prove exact safe replace COMMIT boundary |

**Ordering correction:** B1 may be technically easier than A's unified `PlayerSpawned`, because D20 already proves complete Spawn. But A offers larger immediate utility and avoids making the Entity-specific initial-install/restart requirement a prerequisite for every gameplay event. Build A first while allowing a separate, small B1 research branch after A's signal codec stabilizes. B2/B3 must not be considered one indivisible entity milestone: B3 is materially harder. C and D are **not** automatically authorized by a passing B.

## 21. Validation matrix and evidence contract

| Category | A: Player | B1: Spawn | B2: Died | B3: Destroyed | C: Inventory | D: Loot policy |
|---|---|---|---|---|---|---|
| Method/IL + hook ordering on both pinned hosts | **Required** | Existing Q + new event fence | **Required** subtype table | **Required** complete retire path | **Required** per mutation path | **Required** barrel class and fill/drop chain |
| Attempted vs committed transition | wound/veto/death/respawn | outermost all-normal Spawn | life-state/killed proven | veto vs actual registry retire | stack + quantity post-state | default suppressed + physical drops verified |
| Context snapshots | victim/attacker/position | live proxy original token | dying object ID/prefab/position | pre-retire snapshots, post-retire emit | Item/owner/container/delta | rule/selection/item ownership |
| Duplication/ordering | repeated death, reconnect, two hooks | nested virtual calls/reuse | recursive death/reentrancy | repeated Kill/pooling | split/merge/transfers | multiple loot creation and rules |
| Domain/publication/VM tests | all | all | all | all | all | all including rule replacement |
| Queue/stress tests | death bursts, 100 addons | high Spawn and save load | mass object deaths | mass decay/shutdown | rapid inventory churn | mass barrels, policy budget |
| Native + managed bridge tests | required | required | required | required | required | required |
| Windows/Linux live Carbon | **Required** | **Required** | **Required** | **Required** | **Required** | **Required** |
| Sanitizers/fault injection | callback/recovery | weak ref/reuse | copied HitInfo only | use-after-retire/race | resource accounting | temporary Item/drop failure |
| API metadata/clean install | required for public | required for public | required for public | required for public | required for public | required for public |

### Common negative fixtures

- listener count 129 on a Signal and 257th total per domain; wrong receiver; malformed host event tags, truncated/overlong UTF-8, nonnumeric IDs, invalid Vector3, callback after disconnect; high producer fanout for 100 addons; native queue full; frame starvation; managed/node memory under pressure;
- `pcall`-caught nested module publication failure, root candidate failure, old callback retained through successful replacement, addon provider unload while a callback is enqueued, required and optional dependency changes, shared module that captures another domain's facade, VM-fatal callback timeout plus one recovery and no replay;
- Carbon host plugin exceptions/foreign synchronous nested hooks, nested Luau-origin mutation generating events while already inside a host call; off-thread hook attempt rejected/marshaled;
- mismatched Rust/Carbon hash or unexpected patch topology; initial hotload vs initial batch; same-process unload/reload; normal shutdown; host kill veto and incomplete Spawn throwing tail.

### PASS definition

A phase is **PASS** only when the final committed source was tested against both specified pinned host tuples with the phase's advertised contract, and the evidence ledger records executable fixtures, independent source/IL mapping, native/managed/API regressions, bounds measurements, teardown/recovery, clean package contents and negative cases. A passing model test without the host-path proof is **PARTIAL / BLOCKED** for that public event. Do not collapse inference, tested mock behavior or mere hook invocation into exact-build support.

## 22. Canonical invariant additions/amendments

Do **not rewrite** I1–I12, D7/D9/D10/D11/D13/D14/D18/D20. Add a small **new D23 — Gameplay observation and declarative policy** (number subject to next available decision slot). Recommended text:

1. **Observation/authority:** Gameplay Signals are read-only, deferred, bounded observations of qualified host transitions, never script hooks for veto or mutation. No arbitrary Rust/Carbon hook names or raw host objects enter the VM. Event capture is separate from Luau execution and never reenters host-driven VM execution.
2. **Proof:** Each public event's name corresponds to an explicit attempted/committed postcondition, exact supported host methods/versions, and measured coverage. Vetoable hooks are insufficient as success proof. The event ledger enumerates missing paths and does not infer guarantees from names.
3. **Identity:** Any live Player/Entity proxy follows D11/D20 exact original identity and domain/publication authority. Terminal payloads are bounded immutable snapshots obtained before host invalidation; no lookup-by-ID retargeting or raw host retention.
4. **Bounded delivery:** Per-listener callback items reuse existing scheduler, deadline and resource ownership; delivery is best effort at most once, not replayed or promised lossless. Listener disconnect, domain retirement and VM fatal recovery cancel undelivered work. Overflow drops are accounted and diagnosed.
5. **Policies:** Gameplay modification is a separate, declarative prevalidated publication resource. Exact host decision/commit/suppress boundary, owner, conflict, physical result/verification, failure and post-COMMIT uncertainty must be qualified *per supported action*. Arbitrary synchronous Luau callbacks during Rust gameplay are prohibited.
6. **Compatibility:** Exact host/hook/patch pins fail closed only the affected capability wherever isolation is safe; Entity D20 observer and its continuous startup proof cannot be weakened. New public members require independent cross-platform qualification and additive API identity assignment.
7. **Scope:** Generic EventBus, arbitrary hooks, damage mutation, inventory lifetime proxies, world drop creation, Spawn/Destroy scripting operations and unproved loot replace are explicitly not authorized by this decision.

Also amend `docs/Compatibility.md` with a **Gameplay Events** qualification heading and per-event feature ledger, link `AICONTEXT.md` to the new design/evidence, and update the future API docs after qualification. D20's own text explicitly says lifecycle Signals require a later architecture; cross-link D23 without pretending D20 already authorizes them.

## 23. v0.6.0 release impact and freeze strategy

Current release candidate is explicitly documented in [`docs/releases/0.6.0.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/releases/0.6.0.md): read-only `Workspace`, exact Entity access and bounded radius discovery; **no Entity Signals**. World/Entity 2C has completed qualification for the frozen features.

**Default action:** Finalize/publish v0.6.0 separately **without Gameplay events**. Use the next development scripting identity for A after it passes; update README/docs and metadata only in its own changeset. Do not rewrite historical `0.3`, `0.4`, `0.5` API introductions and do not claim v0.6 is already released. If business/release priority truly requires Player lifecycle in v0.6.0, treat that as a release-candidate **scope change** requiring requalification of both existing World/Entity and new events across Windows/Linux and clean install. Do not simply cherry-pick the Signal code into an already-green release.

**No release/tag is authorized by this architecture task.**

## 24. Open host-proof questions (must be resolved, not guessed)

1. On the exact target `Assembly-CSharp.dll`, does `BaseCombatEntity.Die`/`OnEntityDeath` represent a single completed Player death for all `BasePlayer` paths? Does the later full `BasePlayer.Die` tail add conditions? Can other hook handlers mutate life-state between checkpoint and capture?
2. Which initial-spawn, first-connect, sleeper-reconnect and respawn operations create a tracked D11 token **before** the candidate terminal event? Is there one verified alive state transition for both initial and later spawn? Can a player become alive without `OnPlayerRespawned`?
3. Is `BasePlayer.RespawnAt` normal return sufficient, or is a complete outer call/connected/health/lifestate predicate needed? How do bed/mission spawn overrides and wounded recovery differ?
4. Can `HitInfo.InitiatorPlayer`, `Initiator`, projectiles, explosive ownership, NPC attacks, suicides and decay identify a current human Player without a stale or fabricated connection? When must `KillerId` remain nil?
5. Can the existing D20 complete-Spawn observer cheaply capture and enqueue **after** all required metadata, catalog enrollment and patch validations? Can it do so during startup without generating false historical backlog? What is the highest observed spawn burst?
6. What exact pinned class hierarchy and callbacks represent final death of barrels, loot containers, resources, buildings, animals, vehicles and BasePlayer? Which `OnEntityDeath` variants are real/duplicated/synthetic from another plugin?
7. Is there any complete **post-actual-Kill/TerminateOnServer/EntityDestroy/retire** observer covering every admissible BaseEntity, including vetoes and non-Kill removals? Does a new patch conflict with existing D20 hook topology? Can physical removal be proven even if Unity object destruction is deferred?
8. Does `OnEntityDestroy` apply only to specialized Bradley/CH47 pre-destruction flows on the pinned target, as the public generated source shows? What other similarly named hooks exist, and can they veto?
9. What is the actual Rust prefab and class for each targeted road barrel? What is the fill path, default drop path, bonus scrap path and when loot is materialized? Does a fill-time `OnLootSpawn` interception always prevent the later release of vanilla items?
10. Can the host suppress *all* default barrel drops and install a custom set with bounded owned Items and a safe verify/cleanup result? Does the missing rollback make public `Replace` untenable on the selected host?
11. Is there a bounded, source-complete, post-operation physical snapshot for player main/belt/wear inventory that observes consumption, stack movement, merges, splits and transfers without holding Item references across callbacks? Are there hidden nested containers invalidating the scoped contract?
12. What real burst rates, per-hook fanout costs and memory overhead result with root plus 100 addons and 128 listeners each? Does proposed global event admission preserve GUI/Commands/Discovery fairness under saturation?
13. Which Carbon generated hooks are dynamically demand-patched on subscribed/unsubscribed transitions, and what active patch topology does D20 see with all new listeners enabled? How does an unexpected foreign patch fail closed?
14. Does the new Signal codec preserve all existing command/GUI `Dispatch` tags, listener semantics, native ABI and preview compiler behavior, including deliberately malformed payload tests?
15. Which exact updated Carbon pair/binary hashes on Windows and Linux must be recorded as the supported-event tuple (do not infer these from web docs or older version labels)?

Any material answer that conflicts with the proposed public semantics should trigger a documented **design adjustment before implementation**, not a workaround hidden behind an event named as though it succeeded.

## 25. Exact recommended first implementation phase — Gameplay A

**Recommended first implementation task:** `Gameplay-A — exact-host Player death/respawn qualification and bounded Player Signal extension`.

### Scope and sequence

1. **Read existing authority:** `AICONTEXT.md`, D7/D9/D10/D11/D18, `docs/api/Types/Signal.md`, `docs/api/Services/Players.md`, current `PlayerDirectory`, `FacadeSession`, `bootstrap.luau`, native `FacadeBridge.cpp`, runtime/domain publication tests and relevant target-host compatibility receipts.
2. **A0 proof first:** Inspect exact pinned Windows/Linux host assemblies/hook components for `BasePlayer.Die`, `BaseCombatEntity.Die`, `OnPlayerDeath`, `OnEntityDeath`, initial `SpawnNewPlayer`, `BasePlayer.RespawnAt`, and `OnPlayerRespawned`. Produce an explicit source/IL timing table and private owner-thread live trace of the states across first spawn, normal respawn, wounding/revive, death veto and reconnect. No new public member until timing is proven.
3. **Minimal managed observer adapter:** Add only necessary private **Player death and qualifying spawn** host hooks/complete-method observation and state latch, with exact D11 token capture, immutable bounded context snapshot, no retained `HitInfo`, no host-recursive Luau. Do not touch D20's Entity observer or add global hooks unless exact-host evidence requires them.
4. **Extend existing Signals:** Reuse `FacadeWorld.Event`/`FacadeSession` listener registration/disconnect/checkpoints and pending queue. Add explicit whitelisted private event tags and context serialization, update `scripts/bootstrap.luau` type-aware `Dispatch`, `Players` table and API metadata; preserve existing `added`, `removing`, `command`, GUI `activated` behavior and `Connection:Disconnect` semantics. No second scheduler, public event strings, new `Hooks`/`Entities` services, policy engine or ABI bump absent demonstrated need.
5. **Public gate:** Add `Players.PlayerDied(player, context)` **only** after actual completed death state is proven. Add `Players.PlayerSpawned(player, context)` **only** after initial + respawn equivalence is proved. If latter fails, either a distinctly scoped `PlayerRespawned` may be proposed and separately approved, or omit it; do **not** silently narrow `PlayerSpawned`.
6. **Resource integration:** Prove listener limit 128/signal and total 256/domain, pending 256/domain, global event fanout bounds across up to 128 registrations, queues and diagnostic counters under maximum plausible player events. One callback per listener with fresh existing deadline, no yield, no replay.
7. **Validation:** Native codec, managed event-state model, full real bootstrap/compiler/VM, root/addon/provider replacement, dynamic demand/host patch topology, callback faults/timeout, 100-addons saturation, exact Windows/Linux Rust/Carbon live fixtures; negative pre-death veto/wounded and reconnect tests; clean install/docs/catalog preview. Assert no regressions to current Players, GUI, Commands, persistence or World/Entity.
8. **Report:** Per-member `QUALIFIED`/`DEFERRED` verdict; exact host hashes; source/IL mapping and delivery timing; final-source Windows/Linux evidence; queue/memory observations; old/current API metadata; deferred cases; D23 amendment recommendation and release impact. If a gate fails, keep the feature absent and record the negative evidence. Do **not** publish v0.6.0 or change its frozen scope as part of A.

### Scope exclusions

No `EntitySpawned`, `EntityDied`, `EntityDestroyed`, `EntityDamaged`, Inventory events, loot rules, damage interception, synchronous Lua hooks, Player/Entity mutation, public priority, wildcard event names, reflection, third-party patch topologies, native ABI/protocol/schema changes or release/tag in Gameplay A.

### Stop conditions

**STOP and report partial/blocking evidence**, rather than changing semantics to force a PASS, if: the only death source remains vetoable pre-death; initial and respawn cannot be unified for `PlayerSpawned`; an observer requires unsafe Unity/VM access from an off-thread callback; a new hook breaks D20 patch/observer invariants; real-world memory/work cannot be bounded; a stale Player can be reattached by user ID; provisional listeners receive runtime events or replace active ones on candidate failure; root/addon/provider/VM recovery can replay a callback; or exact pinned Windows/Linux host receipts are unavailable.

### Suggested Codex task delta (not an authorization to implement)

```text
Task: Gameplay-A — Player lifecycle observation; mode: implementation + qualification.
Scope: CarbonLuau at fc71db4ddb2053a98ad3fd65811e64b58f75b150 (or verified descendant).
Read AICONTEXT.md; follow canonical Invariants.md, Compatibility.md and the
Gameplay Events/Hooks/Policies Architecture document, section 25.
Established: PlayerAdded/Removing, D11 tokens, D7/D10 publication, shared Signal
transport and D20 entity observer already exist. Do not redesign them.
Change: Prove exact target death and spawn transitions first. Extend existing
Players Signals with PlayerDied; add PlayerSpawned only if both initial and
respawn semantics pass. Reuse owned queues, native callback admission and
bounded, immutable snapshots. Preserve all existing external contracts.
Exclude: entity signals, damage, inventory events, loot rules, general hooks,
new scheduler, world mutation, ABI expansion and v0.6.0 publication.
Validate: exact IL + Windows/Linux live host, full VM/managed/native tests,
no reentrancy, bounded producer/delivery fanout, 100-addon load, failures,
replacements, provider unload, fatal recovery, old API regression and D20.
Stop/report on failed postcondition proof, lifetime retarget, patch drift,
unbounded work or incompatible publication. No invented supported semantics.
Report final HEAD, source provenance, PASS/PARTIAL/DEFERRED per event, concrete
evidence, API identity and separate release-readiness implications.
```

**Suggested run setting:** **GPT-6 — High** for the implementation task; use the repository's prompt-construction policy so Codex receives this task-specific delta, not a copy of every established invariant.

---

## Reference register

**CarbonLuau exact-revision primary sources**

- [`AICONTEXT.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/AICONTEXT.md) and [`docs/Invariants.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/Invariants.md)
- [`docs/Compatibility.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/Compatibility.md), [`docs/releases/0.6.0.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/releases/0.6.0.md)
- [`docs/WorldEntityFoundation1A-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/WorldEntityFoundation1A-Validation.md), [`docs/WorldEntityFoundation1.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/WorldEntityFoundation1.md), [`docs/WorldEntityFoundation2C.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/WorldEntityFoundation2C.md)
- [`src/CarbonLuau/CarbonLuau.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/CarbonLuau.Carbon.cs), [`src/CarbonLuau/Facade/FacadeSession.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Facade/FacadeSession.cs), [`src/CarbonLuau/Facade/PlayerDirectory.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Facade/PlayerDirectory.cs)
- [`scripts/bootstrap.luau`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/scripts/bootstrap.luau), [`native/src/facade/FacadeBridge.cpp`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/native/src/facade/FacadeBridge.cpp), [`native/src/Runtime.cpp`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/native/src/Runtime.cpp)
- [`src/CarbonLuau/CarbonLuau.EntityLifetime.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/CarbonLuau.EntityLifetime.Carbon.cs), [`src/CarbonLuau/Facade/EntityLifetimeModel.cs`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/src/CarbonLuau/Facade/EntityLifetimeModel.cs)
- [`docs/PlayerInteractionFoundation1FC.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/PlayerInteractionFoundation1FC.md), [`docs/InventoryOwnershipFailureReassessment.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/InventoryOwnershipFailureReassessment.md)
- [`docs/api/Services/Players.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/api/Services/Players.md), [`docs/api/Services/Workspace.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/api/Services/Workspace.md), [`docs/api/Types/Signal.md`](https://github.com/gmoddev/CarbonLuau/blob/fc71db4/docs/api/Types/Signal.md)

**Public hook research — **describes generated/public hook locations, *not an independent exact-build IL proof***

- [Oxide: OnPlayerDeath](https://docs.oxidemod.com/hooks/player/OnPlayerDeath); [OnEntityDeath](https://docs.oxidemod.com/hooks/entity/OnEntityDeath); [OnPlayerRespawn](https://docs.oxidemod.com/hooks/player/OnPlayerRespawn); [OnPlayerRespawned](https://docs.oxidemod.com/hooks/player/OnPlayerRespawned); [OnPlayerSpawn](https://docs.oxidemod.com/hooks/player/OnPlayerSpawn)
- [Oxide: OnEntitySpawned](https://docs.oxidemod.com/hooks/entity/OnEntitySpawned); [OnEntityKill](https://docs.oxidemod.com/hooks/entity/OnEntityKill); [OnEntityDestroy (specialized)](https://docs.oxidemod.com/hooks/entity/OnEntityDestroy); [OnEntityTakeDamage](https://docs.oxidemod.com/hooks/entity/OnEntityTakeDamage)
- [Oxide: OnItemAddedToContainer](https://docs.oxidemod.com/hooks/item/OnItemAddedToContainer); [OnItemRemovedFromContainer](https://docs.oxidemod.com/hooks/item/OnItemRemovedFromContainer); [OnItemStacked](https://docs.oxidemod.com/hooks/item/OnItemStacked)
- [Oxide: OnLootSpawn](https://docs.oxidemod.com/hooks/resource/OnLootSpawn); [OnBonusItemDropped](https://docs.oxidemod.com/hooks/item/OnBonusItemDropped); [Carbon hook reference](https://carbonmod.gg/references/hooks/)
- [Third-party barrel pickup plugin README](https://github.com/l3rady/rust-oxide-auto-pickup-barrel) (illustrative ecosystem behavior only; never treated as host proof).

**Final recommendation:** Proceed with A only after pinned host proof. Publish the already-qualified v0.6.0 separately; treat Entity events, inventory observation and loot replacement as independently gated follow-on capabilities, not extensions automatically authorized by a Signal transport that already works.
