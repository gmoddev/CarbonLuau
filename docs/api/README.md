# CarbonLuau scripting API

Package [v0.7.0](../releases/0.7.0.md) uses the complete experimental API
`0.7.0-experimental`; [qualification](../Release-0.7.0-Validation.md) records
the exact tested host and publication envelope. Earlier v0.5.0 uses API 0.5.0.
Current `main` retains `0.7.0-experimental`. The world, gameplay, Loot and
PlayerInventory features are included in v0.7.0, but not in v0.5.0. This is local
server-side Luau, not Roblox cloud/DataModel/replication compatibility.

Current post-release development adds [D4's barrel-2 target](../GameplayD4-Validation.md)
under the same Loot signature and identities. It is not in published v0.7.0
assets; use the qualified D4 build. D4/D5 retained one global rule.

[D5](../GameplayD5-Validation.md) adds only `wood` to the replacement item
allowlist, alongside scrap and metal.fragments. Use the
[wood example](../../examples/scripts/loot-replace-wood.luau) in a D5 build;
published v0.7.0 does not include it. Signatures, identities and budgets remain
unchanged. [D6](../GameplayD6-Validation.md) permits one owner per prefab and two
rules globally/per domain, using complete owner-set publication and unchanged
shared producer bounds. See the [dual-barrel example](../../examples/scripts/loot-replace-both-barrels.luau).

## Availability and migration

| Introduction | Surface |
|---|---|
| 0.3 | Players, Player, Commands, Signals/Connections and bounded script/task/module runtime |
| 0.4 | Addons/providers, GUI and Player spatial/health/item/inventory operations |
| 0.5 | Local DataStoreService, Get/Set/Remove, structured Query and optional hints |
| 0.6 introduction (included in v0.7.0) | Exact read-only Workspace/Entity and bounded asynchronous discovery |
| 0.6.5 introduction (included in v0.7.0) | Player/Entity lifecycle Signals, PlayerInventory acquisition/reads, sampled ItemChanged and declarative Loot Replace |
| 0.7 (published) | Breaking inventory migration: GiveItem/TakeItem move to PlayerInventory; legacy Player HasItem/CountItem/GiveItem/TakeItem are removed without aliases |

Use [Player:GetInventory](Types/Player.md) and
[PlayerInventory](Types/PlayerInventory.md) for CountItem, HasItem, GiveItem,
TakeItem and sampled ItemChanged. The [migration guide](Inventory-Migration.md)
owns the change. Existing acquisition/read/Signal introductions remain 0.6.5;
new facade mutations are introduced at 0.7. Package version is a separate
[release decision](../Release.md).

## API reference

| Implemented surface | Reference |
|---|---|
| `game` and service discovery | [Globals](Globals.md) |
| Players, connected-player snapshots, join/leave events | [Players](Services/Players.md) |
| terminal-death and initial/full-respawn observations | [Players](Services/Players.md), [PlayerDeathContext](Types/PlayerDeathContext.md), [PlayerSpawnContext](Types/PlayerSpawnContext.md), [example](../../examples/player-lifecycle/init.luau) |
| Player-issued chat commands | [Commands](Services/Commands.md) |
| Player identity, live position/health observation, messaging and permission query | [Player](Types/Player.md) |
| Rust item identity, bounded physical inventory CountItem/HasItem and verified GiveItem/TakeItem | [Items](Services/Items.md), [PlayerInventory](Types/PlayerInventory.md), [GiveItemBehavior](Types/GiveItemBehavior.md) |
| Player inventory facade and sampled net-change Signal | [PlayerInventory](Types/PlayerInventory.md), [InventoryChangeContext](Types/InventoryChangeContext.md), [example](../../examples/players/inventory-changed/init.luau) |
| Breaking API 0.7 inventory removal and migration | [Migration guide](Inventory-Migration.md), [release-readiness audit](../Release.md#gameplay-ad-release-readiness) |
| init-only destruction-payout declarations | [Loot](Services/Loot.md), [Enum](Types/Enum.md), [DropMode](Types/DropMode.md), [LootDropEntry](Types/LootDropEntry.md), [example](../../examples/scripts/loot-replace.luau) |
| Immutable world-coordinate values | [Vector3](Types/Vector3.md) |
| read-only exact world lookup | [Workspace](Services/Workspace.md), [Entity](Types/Entity.md), [examples](World-Examples.md) |
| completed-Spawn Entity observations | [Workspace.EntitySpawned](Services/Workspace.md#entityspawned), [example](../../examples/world/entity-spawned/init.luau), [qualification](../GameplayB1-Validation.md) |
| successful Entity removal snapshots | [Workspace.EntityDestroyed](Services/Workspace.md#entitydestroyed), [EntityDestroyedContext](Types/EntityDestroyedContext.md), [example](../../examples/world/entity-destroyed/init.luau), [qualification](../GameplayB3-Validation.md) |
| full-death snapshots; source and narrow actor gates are separate | [Workspace.EntityDied](Services/Workspace.md#entitydied), [EntityDeathContext](Types/EntityDeathContext.md), [example](../../examples/world/entity-died/init.luau) |
| Command payload | [CommandContext](Types/CommandContext.md) |
| Event subscription | [Signal](Types/Signal.md), [Connection](Types/Connection.md) |
| Versions and limits | [Compatibility](Compatibility.md) |
| Player status, inventory, rewards and nontransactional shop examples | [Player examples](Player-Examples.md) |
| Existing `require` and `task.spawn/defer/delay` | [Phase 2 script contract](../Phase2.md) |
| Addon manifests, dependencies and package-qualified imports | [Addon composition](Addons.md) |
| Carbon provider registration protocol | [Addon providers](Addon-Providers.md) |
| Server-driven retained GUI with qualified Foundation 2A layout, 2B typed images and 2C scrolling source | [GUI guide](Gui.md), [GUI reference](Gui-Reference.md) |
| Local persistence (introduced in v0.5) ([combined qualification](../PersistenceFoundation2D.md)) | [Guide and examples](Persistence.md), [DataStoreService](Services/DataStoreService.md), [DataStoreOptions](Types/DataStoreOptions.md), [DataStore](Types/DataStore.md), [DataStoreQuery](Types/DataStoreQuery.md), [DataStoreQueryResult](Types/DataStoreQueryResult.md), [PersistedValue](Types/PersistedValue.md) |

## Execution and safety

Methods use colon syntax. Services/proxies/contexts expose no raw Rust/Unity/CLR
object or filesystem/SQL authority. API misuse raises controlled errors; read
each reference for its exact absence, stale and failure behavior.

Callbacks enter later through bounded owner-thread admission, not inline host
hooks. Publication, root/addon replacement and VM recovery retain exact original
authority; no old handle silently retargets a replacement lifetime.

- [Discovery](Discovery.md) is bounded asynchronous complete traversal, evaluated
  when candidates are encountered—not an atomic world snapshot. Exact host and
  initial-startup observer requirements apply.
- [Persistence](Persistence.md) submits without yielding. Submission is not
  durable success; callbacks have exact lifetime/at-most-once authority and uncertain
  mutations are not replayed. Query is index-backed, not an unbounded scan.
- [ItemChanged](Types/PlayerInventory.md#itemchanged) is sampled net change, not
  causal/completed transfer tracking. Intermediate changes may appear and balanced
  changes may be missed.
- [Loot](Services/Loot.md) replaces only qualified destruction-time payouts;
  it does not reclaim earlier generated world drops or promise rollback.
- [GUI](Gui.md) remains server-driven retained presentation, not client replication.
  Authenticated-client visual/input/reconciliation observations remain unqualified;
  TextBox and Submitted are not implemented.

Read [scripting compatibility](Compatibility.md), [current contract routing](../Index.md)
and [qualification](../qualification/README.md) before treating API availability
as evidence for a host, client, durability or release claim.
