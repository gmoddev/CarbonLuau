# Gameplay D0 supplied research — preserved source

This historical static report is source evidence, not a runtime PASS or public
API declaration. Gameplay D2A separately owns new exact-host qualification.
Its proposed limits remain proposals until qualified. `Enum.DropMode` is the
user-approved future naming direction; this private phase adds no public enum.

---

# CarbonLuau — Gameplay D0 Architecture & Host Evidence

**Research snapshot:** 2026-10-11 (America/Chicago project review date; current repository inspected against `main` commit [`35701d5c7986f863550c2d92300d48fa2793d44d`](https://github.com/gmoddev/CarbonLuau/commit/35701d5c7986f863550c2d92300d48fa2793d44d))
**Scope:** Gameplay D0, *static research/architecture only*. No repository changes, implementations, patch installations, builds or new live host experiments.
**Status:** **GAMEPLAY-D0 NEEDS HOST QUALIFICATION — prove complete per-prefab destruction payout paths and suppression before authorizing strict `Replace`; independently qualify exact drop origin, item transfer and world removal for a narrower collection mode.**

## 0. Executive decision record

1. **Keep the three-argument, declarative API** `Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)`. The latest accepted naming preference is **`Enum.DropMode`**, specifically recorded in `AICONTEXT.md`, `docs/GameplayCDArchitecture-Research.md` (user amendment), and `docs/GameplayC1-Validation.md`. The old research's `Loot.DropMode` suggestion is superseded. `Enum` is a small read-only namespace of registered typed enums, **not** a generic Roblox DataModel/Instance service; `GiveItemBehavior.InventoryOnly` remains unchanged.
2. **Strict `Replace` is an exact eligible *destruction-payout* policy:** suppress every applicable default item payout of a specifically qualified prefab/class and produce only its declared replacement entries, including stored contents *and* conditional bonus-scrap/default auxiliary item paths. It does **not** promise physical-world transaction atomicity or exactly-once execution. It cannot be inferred from `OnLootSpawn` alone.
3. **AstroBarrelPickup changes the feasibility landscape.** A bounded, opt-in **best-effort collect/redirect** mode is worth qualifying because the working reference demonstrates real observed loot `DroppedItem` traffic after nearby barrel deaths. **But this is not `Replace`.** It lacks exact source attribution, blindly reconstructs item quantity/definition, and does not verify inventory receipt or world-deletion completion. Those problems can be substantially reduced without solving strict suppression.
4. **Prefer identity-preserving transfer of the original Item** for collection *if* the host has a qualified operation that moves a world-owned `DroppedItem.item` into the exact Player inventory and safely retires its world entity. This minimizes clone duplication, lost metadata and compensating cleanup. It is only a hypothesis until current assembly IL, state machine and exact-host fixtures prove it. If no such ownership-transfer route exists, a clone-then-delete adapter must keep its narrower failure contract and must not pretend it reuses D13's qualified `GiveItem` implementation.
5. **The strongest proposed origin mechanism is a private synchronous causal frame + item/container identity**, not radial matching: associate the original D20 barrel epoch with verified `LootContainer`/`StorageContainer` payout frames and original container/item references; record the actual `Item.Drop` → returned world entity handoff. A synchronous frame alone is not sufficient if trusted plugin callbacks can synchronously spawn unrelated items, so anchor it to the proved source method/item identity. Drop events deferred outside that frame need separate source continuity or remain unattributed.
6. **Do not implement D1 as an inert public service.** D1 may privately implement deterministic frozen-rule validation/publication. Public exposure must wait for D2 host-path qualification. `ReplaceContents` is an honest fallback restricted to subsequent *loot fill* operations, not pre-existing barrels, ordinary bonus scraps or all physical destruction payouts. A separate later `Loot:RegisterCollectionRule`/`AutoCollect` capability could express the Astro behavior without confusing it with custom drop generation.
7. **No synchronous Luau callbacks from Rust gameplay hooks.** A future *deferred* `Loot.DropResolved`/`Loot.BarrelBroken` Signal is observational; it cannot suppress the same payout. If author conditions on a breaker are needed at decision time, investigate a prevalidated managed-only, bounded declarative predicate only after the actor and timing are qualified.

**Critical unresolved question:** On *Rust 25653776*, for **each exact prefab**, does one narrow method (or a small, fully enumerable and coordinated set of methods) dominate *every* vanilla physical destruction payout? Does it permit a replacement decision without skipping destruction, corrupting `Item` ownership or leaking a freshly created vetoed bonus item? The public hook excerpts alone do not answer this. There is no newly inspected D0 IL in this report; referenced exact-build IL is pre-existing B1/B3/A or historical D13 evidence.

---

## 1. Authority, current baseline, and evidence levels

### 1.1 Read and preserved

- **Policy/authority:** [`AGENTS.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/AGENTS.md), [`AICONTEXT.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/AICONTEXT.md), [`docs/Invariants.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/Invariants.md): I1/I3/I4/I8/I9/I10/I12; D7/D9/D10/D11/D13/D18/D20/D23. Do not silently create a stronger lifetime, callback, atomicity or publication guarantee than these owners.
- **Prior investigation:** [`docs/GameplayCDArchitecture-Research.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayCDArchitecture-Research.md) §§2.2–2.3, 3.3, 5, 8.2–10; [`docs/GameplayEventsArchitecture-Research.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayEventsArchitecture-Research.md) §§6, 7, 10–16. These are supporting research, not implemented D APIs. Avoid rerunning unrelated C2 inventory-signal research.
- **Qualified current features:** [`GameplayC1-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayC1-Validation.md) (C1 PASS, production `ae434697...`, final evidence `35701d5...`); [`GameplayB1-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayB1-Validation.md) (exact completed Spawn); [`GameplayB3-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayB3-Validation.md) (actual removal, pool-admission and native-validity complement); [`GameplayFoundationA-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayFoundationA-Validation.md) (Player terminal death/spawn, not generic barrel break); [`PlayerInteractionFoundation1FB-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/PlayerInteractionFoundation1FB-Validation.md) and [`PlayerInteractionFoundation1FC.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/PlayerInteractionFoundation1FC.md) (qualified *inventory-only* grants under D13/I12).
- **Implementation reuse:** [`scripts/bootstrap.luau`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/scripts/bootstrap.luau), [`FacadeSession.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/FacadeSession.cs), [`GameplayEventBudget.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/GameplayEventBudget.cs), [`GameplayEventFacade.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/GameplayEventFacade.cs), [`PlayerDirectory.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/PlayerDirectory.cs), [`InventoryGrant.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/InventoryGrant.cs), [`CarbonLuau.Inventory.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/CarbonLuau.Inventory.Carbon.cs), [`FacadeBridge.cpp`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/native/src/facade/FacadeBridge.cpp), [`Publication.cpp`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/native/src/runtime/Publication.cpp).

### 1.2 Evidence language — do not conflate

| Code | Meaning in this document |
|---|---|
| **R** | This review inspected CarbonLuau's versioned, current repository code or its exact recorded design owner. |
| **Q** | The repository reports previous Windows/Linux exact-host assembly inspection and/or real-host qualification; **not newly reproduced** by this review. Claim only its recorded scope. |
| **P** | Public Oxide/Carbon source excerpts, catalogs or documentation; **not** pinned to Rust 25653776's loaded assemblies. |
| **I** | Mechanism inferred from available source, proposed behavior or the Astro plugin's supplied code. Must be verified. |
| **U** | Material unknown; source/IL not available or still not sufficient. |
| **L** | Requires new, controlled Windows/Linux live Carbon experiment, beyond static proof. |

### 1.3 Exact host identity, and why the version matters

| Evidence family | Exact target | What it actually proves |
|---|---|---|
| **Current Gameplay A/B1/B3 + C1** | Rust build **25653776**, protocol **2634.289.1**, revision **166494**; Carbon Windows **2.0.262.0 / 8a81d70**; Carbon Linux **2.0.261.0 / c74c4ca**; Harmony **2.4.2.0** | Q: qualified Player/D20 lifetime, completed Spawn, original-incarnation actual world removal; C1 controlled actual-Player read parity on both OSes. **Not** D0 barrel/drop-path proof. |
| **Current Rust Assembly-CSharp** | Windows SHA-256 `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`; Linux SHA-256 `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` | Q: binary pins from B3 report. Record independently decoded backing IL, loaded publicized IL and target method hashes **when doing D0 IL**. |
| **Older inventory M2 / Player-1F** | Rust **25353106**, Carbon **2.0.259**, historical Windows Assembly SHA-256 `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a`, Linux `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b` | Q *older*: item creation, split/drop hazard, InventoryOnly adapter, cleanup and physical VERIFICATION. Cannot certify D0's current-build drop lifetimes. |
| **Development API** | `0.6.5-experimental`; package/tag **`0.5.0`/`v0.5.0`**, native ABI `1.5`, provider `1.2`, addon schema `1`; pinned Luau unchanged | R/Q. No version bump, tag or release from research. |

**Source-access limit:** Raw current Windows/Linux Rust binaries and their post-Carbon-publicizer D0 method bodies are **not committed** in the repository. The B3 report identifies local external evidence workspaces inaccessible to this review. Therefore a generated public `OnLootSpawn` snippet is **P**, not newly established current-host IL. The only exact IL tokens below are Q from already qualified non-loot work.

---

## 2. Barrel identities and class scope: actual catalog evidence vs qualified host

Historical catalogs consistently identify these five plausible target *full paths* and `LootContainer` inheritance. **This is external historical prefab evidence (P), not a fingerprint of the 25653776 asset bundle or proof of which payout methods execute for each instance.**

| Candidate full prefab | Historical prefab ID | Historical managed component/class | D0 treatment |
|---|---:|---|---|
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab` | `966676416` | `LootContainer : StorageContainer : DecayEntity : BaseCombatEntity` | Preferred **first** qualified test subject. |
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab` | `555882409` | Same family | Separately qualify; not automatically equivalent. |
| `assets/bundled/prefabs/radtown/loot_barrel_1.prefab` | `3364121927` | Same family | Separately qualify. |
| `assets/bundled/prefabs/radtown/loot_barrel_2.prefab` | `3269883781` | Same family | Separately qualify. |
| `assets/bundled/prefabs/radtown/oil_barrel.prefab` | `3438187947` | Same historical family | **Separate scope**: oil/low-grade contents and different configs; don't accidentally include under generic “barrel”. |

**P source:** [OrangeWulf/Rust-Docs `Entities.md`](https://github.com/OrangeWulf/Rust-Docs/blob/master/Entities.md), [independent 2025 prefab component catalog](https://gist.github.com/c0deR3D/a1484ff0925b5347bd1edb7661eba921), and [uMod historical paths](https://umod.org/community/rust/14495-prefab-name-for-this-barrel). [Carbon entity references](https://carbonmod.gg/references/entities/details?id=3364121927) are helpful discovery aids but the dynamically rendered pages are **not** current-host IL. A historical [loot table catalog](https://gist.github.com/c0deR3D/802c048fa6c48efc147d384077b4e715) associates `ScrapAmount=2` with sample barrel entries; it is not guaranteed live configured payout. [Facepunch entity reference](https://wiki.facepunch.com/rust/Entities) describes canonical full asset paths and the ability to inspect a live entity; use full `PrefabName`, not `ShortPrefabName.Contains`.

At D0 qualification record, **per prefab**: current full `PrefabName`, prefabID and asset hash, exact concrete managed type, instance components, inventory owner/container identity, loot definition/source config, actual `SpawnLoot` caller/timing, physical item categories, `OnDied`/`OnKilled` override dispatch, supported damage/despawn variants, and whether bonus-drop logic runs. Reject an unknown class/config/prefab instead of inventing coverage by substring or stale prefabID.

---

## 3. Available Rust/Carbon source: payout callgraph and temporal boundaries

### 3.1 What public hook source *does* establish (P)

```text
[LOOT FILL — not death]
LootContainer.SpawnLoot()
  validate inventory
  base.inventory.Clear()                    [host mutation]
  ItemManager.DoRemoves()                  [host mutation/cleanup]
  OnLootSpawn(this)                        [override only subsequent PopulateLoot branch]
    if null -> PopulateLoot() -> refresh/countdown actions
  [other fill caller] LootFill.DelayFill()
    OnLootSpawn(this) -> LootContainer.FillLoot(StorageContainer.inventory, ...)

[TERMINAL DAMAGE/DESTRUCTION — relationship to loot still UNPROVED per prefab]
BaseCombatEntity.Die(HitInfo)
  sets health=0 and life=Dead
  OnEntityDeath(this, info)                 [NOT a final payout fence]
  later OnDied / derived-class processing  [must inspect actual LootContainer dispatch]
  ... possible StorageContainer/LootContainer death/kill paths ... [U]
  BaseNetworkable.Kill(...)                [OnEntityKill veto possible]
  [B3 independently proves *successful world removal*, not loot payout]

[GENERIC CONTENT DROP — only if the prefab's path reaches it]
DropUtil.DropItems(ItemContainer, Position)
  if !Server.dropitems, invalid container or OnContainerDropItems != null -> return
  copies Item[] from container.itemList
  [per-item world-drop body: current exact IL REQUIRED]

[SEPARATE BONUS SCRAP — only if prefab calls it]
LootContainer.DropBonusItems(BaseEntity initiator, ItemContainer container)
  [modifier/quantity calculation including Scrap_Yield variable]
  ItemManager.Create(Scrap, computedAmount, ...)
  OnBonusItemDrop(created Item, basePlayer, container)
    if null -> Item.Drop(position, velocity) -> (DroppedItem).DropReason = Loot
             -> OnBonusItemDropped(item, basePlayer, container)
    if nonnull -> skip shown drop branch; ownership/cleanup of created Item U

[INDIVIDUAL ITEM WORLD DROP]
Item.Drop(...)
  [world-entity create/init/spawn details U]
  OnItemDropped(Item, BaseEntity)           [post-drop notification, no return behavior]
  RemoveFromContainer()
  return world entity

[WORLD SPAWN]
BaseNetworkable.Spawn()
  ... ServerInit/after-init/network group/isSpawned ...
  OnEntitySpawned(this)                    [inside Spawn BEFORE later network/return work]
  ... more work ... -> eventual outer return
```

**References (P):** [OnLootSpawn](https://docs.oxidemod.com/hooks/resource/OnLootSpawn); [OnContainerDropItems](https://docs.oxidemod.com/hooks/entity/OnContainerDropItems); [OnBonusItemDrop](https://docs.oxidemod.com/hooks/item/OnBonusItemDrop); [OnBonusItemDropped](https://docs.oxidemod.com/hooks/item/OnBonusItemDropped); [OnEntityDeath](https://docs.oxidemod.com/hooks/entity/OnEntityDeath); [OnEntityKill](https://docs.oxidemod.com/hooks/entity/OnEntityKill); [OnItemDropped](https://docs.oxidemod.com/hooks/item/OnItemDropped); [OnEntitySpawned](https://docs.oxidemod.com/hooks/entity/OnEntitySpawned); [OnItemRemove](https://docs.oxidemod.com/hooks/item/OnItemRemove); [OnDroppedItemCombined](https://docs.oxidemod.com/hooks/item/OnDroppedItemCombined).

**Important distinctions:**

- `OnLootSpawn` is **after** clearing the LootContainer's previous inventory and before vanilla `PopulateLoot`, not an all-payout destruction fence. `LootFill.DelayFill` is a distinct producer.
- `OnContainerDropItems` **vetoes only the `DropUtil.DropItems` invocation** and has only an `ItemContainer` input, not a proven initiating player or source Entity. It may suppress generic stored-content dumping but does not automatically suppress `DropBonusItems` or direct drops. If it returns early, previously stored Items may be left for later cleanup; that is an ownership question, not success proof.
- `OnBonusItemDrop` is **after `ItemManager.Create`**. Canceling a freshly created bonus is **not** a no-effects-before-COMMIT decision, and the public excerpt does not show its cleanup. `OnBonusItemDropped` is after `Item.Drop`, not a pre-drop veto. Its code assigns `DropReason=Loot` **after** `Item.Drop` returns, so a spawned callback *during* `Item.Drop` can observe a not-yet-tagged bonus entity. Never rely on DropReason at initial `OnEntitySpawned` as complete origin proof.
- `OnItemDropped` is after world-drop work has been entered and, per excerpt, before `RemoveFromContainer()` and the return. It cannot undo a completed spawn simply by returning a value; also, the `Item` may still have its old container association at hook time. Inspect exact current IL before choosing this as a stable observation fence.
- Public `OnEntitySpawned` is earlier than complete `BaseNetworkable.Spawn` return. **Existing B1 uses a separately qualified successful original/full-Spawn completion, not the public early callback**; B1's deferred signal cannot retroactively veto a world drop.
- Public `OnEntityDeath` (BaseCombatEntity) is after health/lifestate are set, but before all derived death/payout work; it does **not** mean all loot has dropped, nor does it provide original Item ownership. `OnEntityKill` is vetoable. `OnEntityDestroy` (Bradley/CH47 special) is **not** universal successful deletion.
- Public `DroppedItem.OnDroppedOn` merges stack quantities via `OnDroppedItemCombined` before the success predicate. Merging can change identity/amount or remove the originally captured world entity. Do not claim UID+amount world observation is timeless.

### 3.2 Exact host facts already qualified, *not* re-proved here (Q)

B3 pin table on Rust 25653776:

| Method | Token | Qualified order/meaning |
|---|---|---|
| `BaseNetworkable.Kill` | `0x06005177` | `OnKilled` before `DoEntityDestroy`/`TerminateOnServer`/`EntityDestroy`; kill attempted vs actual retirement distinguished. |
| `BaseNetworkable.TerminateOnServer` | `0x0600517a` | Original registry unregistration → network release → deactivate → normal completion. |
| `BaseNetworkable.EntityDestroy` | `0x060051b4` | `ResetState` and `GameManager.Retire`, not itself proof of all cleanup. |
| `GameManager.Retire` | `0x06006458` | Pool or Unity destroy branch; original identity preserved by B3.
| `PrefabPool.Push(Poolable)` | `0x06008344` | Observed after actual stack push; B3 additionally handles native-invalidity supplement. |

These certify **original-incarnation world retirement** only. They do not prove that `DroppedItem.item` was removed/consumed correctly, that a player received an item, that all payouts occurred, or that a barrel death had a particular breaker. The current repository's `CarbonLuau.EntitySpawned.Carbon.cs` explicitly attaches B1 **after full outer Spawn completion**; `EntityDestroyed.Carbon.cs` + `EntityPoolDestroyed.Carbon.cs` implement B3's separate removal evidence and source bounds. Preserve their patch topology and startup continuity.

### 3.3 Exact missing *current-build* callgraph: finite inspection order

For **each five-prefab candidate**, inspect loaded/processed and backing Windows/Linux IL (method tokens, MVID, hashes, EH, vtable dispatch, Carbon hook positions):

1. `LootContainer.SpawnLoot`, `PopulateLoot`, `FillLoot`, refresh/initialization/load paths, `LootFill.DelayFill`; record whether loot is prefilled at initial Spawn, on timer, on damage/death or on access. Identify existing contents from saved/third-party modifications.
2. `BaseCombatEntity.Die`, `OnDied`, concrete `LootContainer.OnDied`/`OnKilled`, `StorageContainer.OnKilled` and parent hierarchy; list every death/kill/equipment/loot method reached (including inherited calls), early-return/veto/throw/finalizer and destruction/respawn paths.
3. `DropUtil.DropItems` **all direct callers**, especially for the concrete container, and the conditional `Server.dropitems` gate. Does `LootContainer` use it on barrel break? Is `container.owner`/`container.playerOwner` a trustworthy pointer to *the same* original barrel incarnation, or merely an assignable/nullable field?
4. `LootContainer.DropBonusItems` all callers, actual `initiator` resolution from HitInfo, tea `Scrap_Yield` calculation, stack amount limits, Item creation timing, cleanup when hook nonnull, dropped entity type, bonus `DropReason` assignment, and thrown/null `Item.Drop` result.
5. Any additional direct `Item.Drop`, gibs, `DroppedItemContainer`, `WorldItem`, container release, alternate salvage/payout, `OnRandomItemAward`, and item paths that do not create `DroppedItem`. Call-reference inventory must account for virtual calls/indirect delegates, not merely literal call sites.
6. `Item.Drop` → `DroppedItem.item` assignment → `BaseNetworkable.Spawn` → `OnEntitySpawned` → `OnItemDropped` → parent unlink, plus `DroppedItem.OnKilled`/`OnDestroy`, real original `Item` terminal ownership, pool and merge paths. This establishes *when* each candidate callback can safely examine item/owner/drop reason.
7. `BasePlayer.GiveItem(Item, PickedUp)` and `PlayerInventory.GiveItem` current-build paths, including overflow world-drop fallback, transient ownership, callback effects, false/null/throw and whether the original world Item can be transferred directly with a supported bounded operation. **Do not use D13's `InventoryOnly` adapter as if it accepts a pre-existing world-owned Item.**
8. Prove whether a **single postdecision/pre-first-payout seam** dominates all relevant outgoing item paths, or enumerate the minimum two+ coordination seams. If a path escapes, strict `Replace` is not qualified for that prefab.

For every method provide both *backing* assembly identity and the **Carbon-transformed loaded** method fingerprint, exact source placement, observed postcondition, patch owner/order, exception regions, alternate callers, and positive/negative traces. Fail affected capability closed on drift, without weakening independent B1/B3/C1.

---

## 4. AstroBarrelPickup: exact supplied implementation audit

The supplied CarbonPlugin is accepted as **behavioral evidence** that the user has observed a practical workflow. It is **not** an exact-host proof of item provenance, guaranteed inventory admission, or successful world retirement.

### 4.1 What its code actually does

- `OnEntityDeath(BaseCombatEntity, HitInfo)` checks `entity.ShortPrefabName.Contains("barrel")`; if `info?.InitiatorPlayer` exists, records `Position`, a **strong** `BasePlayer` reference and `Time.realtimeSinceStartup`. Also computes a weapon string **only for logging**. Enqueues a distinct three-second `timer.Once` cleanup **per** death.
- Every global `OnEntitySpawned(BaseNetworkable)` checks for `DroppedItem`, nonnull `dropped.item`, `item.parent == null`; visits recent barrel records **in reverse insertion order**, discarding records older than three seconds. First death within three world units wins, *not nearest or unique origin*.
- If that record's Player appears connected and item amount positive, creates **a new** `Item` from **only** `item.info` and `item.amount`; calls `player.GiveItem(newItem, PickedUp)` if nonnull; then calls `dropped.Kill()` **regardless of whether Create returned null or GiveItem actually delivered**. The code neither inspects an inventory delta nor waits for/validates B3 removal.
- A failed/ineligible latest matched Player logs and **returns without checking other candidate barrels**. Successful matches do not remove or mark the `BarrelBreak` consumed; one break can claim any number of nearby world spawns for up to three seconds.

### 4.2 Positive scope — what it can plausibly catch

It can handle **any** `DroppedItem` whose `item` field is already initialized when the global early `OnEntitySpawned` hook executes, is parentless at that point, and happens to appear within the configured spatial/time neighborhood. If the actual barrel contents or bonus scrap are produced using `Item.Drop` → `DroppedItem`, the code may catch them. This is a useful observed result and justifies a scoped investigation. It **does not** directly intercept `OnLootSpawn`, `LootFill`, `DropUtil` or `DropBonusItems`, and it does not know which of those producers actually emitted the matched drop.

It will *miss*: fill-time changes with no world entity; item delivery that goes directly into an inventory; world payouts represented as `WorldItem`, `DroppedItemContainer`/loot bag or another entity type; a `DroppedItem` whose `item` is null/not ready at the hook; drops after three seconds or more than three units away; and barrels whose death has no directly available `InitiatorPlayer`. Some of these are conditional possibilities (I/U), not claims that vanilla barrels definitely take every route.

### 4.3 Failure and misattribution matrix

| Scenario | Reference-plugin behavior / failure | More defensible D2 target |
|---|---|---|
| Two barrels break within 3 m/3 s, two different Players | Most recent matching record claims **both sets** of nearby `DroppedItem`s; deterministic candidate-order bias, not exact origin. | Item/container identity or per-barrel causal frame; otherwise **ambiguous → leave vanilla**. |
| Player manually drops an item near a recent barrel | Wrongly eligible based on distance/time alone. | Require verified origin evidence; `DropReason` is only auxiliary and may be set *after* early Spawn. |
| Another plugin spawns a drop or calls `Item.Drop` during a barrel hook | Same mistaken proximity capture, and synchronous causal-frame-only context may also be polluted by nested plugin work. | Couple frame with source Item/owner container/callsite; qualify callback nesting. |
| `ItemManager.Create` returns null | Reference still invokes `dropped.Kill()`: **potential loss**. | No world removal unless an accepted/verified transfer happened; controlled diagnostic. |
| Generic `BasePlayer.GiveItem` cannot fit or elects world fallback | New item may be world-dropped rather than securely received; original is still killed. May produce a **recursive new `DroppedItem`** matching the same recent death. | Never use unconstrained generic GiveItem; qualify original-item transfer or D13-style exact-slot physical VERIFY for an authorized temporary new item. |
| Grant succeeds, `DroppedItem.Kill()` is vetoed | New item and original world item can both persist: **duplication**. | Record post-COMMIT partial result; attempt exact B3 terminal proof, no false success and no automatic retry. |
| Kill succeeds, grant fails/throws/was never verified | Original removed, Player receives nothing; item **loss** or indeterminate grant effect. | Prefer identity-preserving original Item move. If impossible, define unavoidable grant/delete failure window and don't use strict `Replace` label. |
| Normal `DroppedItem` merge/pickup/despawn races | Observed world identity/amount may change under other host callbacks. | Immediate original-lifetime + Item identity check; do not reacquire by network ID. Abort/leave vanilla on pre-COMMIT race. |
| Two world drops from one barrel, or a bonus yields more drops | The same unconsumed record can collect multiple, which is convenient but unbounded and not evidence of complete count. | Fixed *per-origin* accepted-item/drop cap, consumed correlation tokens and diagnostic dropped-overbudget count. |
| `item` contains skin/condition/custom name/instanceData/children/ammo/genetics | Recreating by `(item.info, item.amount)` discards state not represented by those two values. | Transfer original Item, or explicitly allow only plain-default Item subset and call result **quantity/definition-equivalent**, not Item-equivalent. |
| Player disconnect/reconnect with same UserId | Stored strong `BasePlayer` and `IsConnected` do not prove original D11 connection token/connection identity. | Capture private exact D11 lifetime, identity and connection; never look up by current UserId to retarget. |
| Burst of N breaks followed by M dropped entities | `RecentBarrels` is unbounded; scan is O(N) **per world drop**; N three-second timers also each scan the list: possible O(N²) cleanup work, plus retained strong Player refs. | Hard record cap, bounded scan/visits and one existing owner-frame expiry pump; weak/or scalar identity only; no per-death timers. |
| Proximity of oil barrel/props containing “barrel” | Substring admits unrelated classes/scopes (including possible player-deployed barrels). | Exact full `PrefabName` **and** qualified concrete class/instance component, no substring/wildcard. |
| No natural breaker (explosion, trap, environmental, NPC, another plugin) | `InitiatorPlayer` may be null; no pickup. A nonnull direct initiator is not guaranteed to be the actual economic beneficiary/owner. | Attribution optional, source-specific and captured at validated transition; null stays null. |
| `OnEntitySpawned` invoked before full Spawn | Host may still initialize/network/mutate drop; immediate `Kill` is inside an incomplete Spawn call and can interact with finalizers/continued work. | Observe after qualified full Spawn and track exact original lifetime; for pre-drop policy, patch a proved earlier producer seam instead. |

**Ordering proof used here (P):** `BaseNetworkable.Spawn` calls `OnEntitySpawned` before later network work; `OnEntityKill` can veto `Kill`; `Item.Remove` is separately vetoable; bonus code assigns `DroppedItem.DropReason` after `Item.Drop`. **B3's qualifying `EntityDestroyed` event is actual original-world removal, not a successful return from `Kill()`.**

### 4.4 Reliability ceiling: even a corrected post-spawn clone-and-delete cannot be atomic

- **Create/grant first, delete second:** a failed/vetoed delete can leave both original and reward. Safe to avoid loss but **not duplication-proof**.
- **Delete first, create/grant second:** original may disappear and grant fail. Safe to avoid duplicate copies but **not loss-proof**.
- **Transfer the original Item to Player inventory:** conceptually avoids the extra copy but may involve nontransactional detach/move/world cleanup. Requires current-host proof of exclusive Item ownership and partial-failure outcomes; cannot assume atomicity. This is the **preferred research avenue** for best-effort auto-collection.
- **Do not compensate automatically or retry** if outcome becomes indeterminate: D13/I12 prohibit inventing rollback or exactly-once execution. Limited follow-up observations can inform diagnostics, not guaranteed compensation.

The reference plugin is therefore legitimate practical evidence supporting a **useful lower-guarantee feature**, but a claim that it *replaces all barrel loot exactly once* would be false.

---

## 5. Reliable origin versus heuristic correlation

| Rank | Mechanism | Attribution strength | Cost/risk and qualification |
|---|---|---|---|
| **A — preferred** | **Qualified per-barrel payout scope**: original D20 Spawn epoch + weak exact Entity, exact inventory container identity, method-specific synchronous frame; matched source Item UID/identity and returned `DroppedItem` mapped during `Item.Drop`. | Potentially **exact causal** within an enumerated synchronous payout path, when method/callsite and Item pre-/post-state are pinned. | Needs targeted IL and patch control; framework callbacks can nest; off-thread/deferred payout would break frame and must be excluded or explicitly threaded through host-owned IDs. |
| **B** | `DropUtil.DropItems` container identity known as exact original barrel inventory; bonus `DropBonusItems` given original method context + created Item identity before Drop. | Potentially **exact class/source correlation** for two enumerated routes. | Does not automatically cover other direct producers; container ownership may be nullable/mutable; vetoing bonus happens after item creation. |
| **C** | `OnItemDropped(Item, BaseEntity)` combined with a recorded original pre-drop Item identity and source frame. | Potentially identity-preserving **post-drop** receipt. | Current public snippet is *after* world drop and before parent unlink; alone lacks original owner and cannot prevent creation. |
| **D** | Captured D20 barrel epoch at death + deferred `Workspace.EntitySpawned` on world `DroppedItem`, filtered by position/3 s. | **Heuristic only**; B1 ensures completed Spawn of the *drop*, not provenance linking it to barrel. | Ambiguity from concurrent breaks, players and plugins. Cannot claim exact, even if tests rarely collide. |
| **E — reject** | Bare `ShortPrefabName.Contains("barrel")` + latest death record + 3 m. | **No causal attribution**. | Useful proof-of-concept, unacceptable default to destroy/redirect another player's drops. |

**Exact identity is not the same as account identity or network ID:** Carry the source's **original D20 incarnation token** privately, a valid weak object and source Item/container identity when present. Never query `Workspace:GetEntityById` after death to fabricate the original; B3 itself intentionally provides a snapshot with no live proxy. `HitInfo.InitiatorPlayer` is captured only while it belongs to the *same qualified death/payout path*; use exact D11 Player connection binding or nil. A scalar captured `BreakerId` may be recorded for diagnostics where proof supports a human origin, but it never authorizes future re-resolution of a different connection.

**Heuristic fallback rule, if explicitly authorized:** hard cap recent origins; a fixed-time monotonic expiry; 3 m/3 s only a configurable *bounded* policy detail; require **exactly one** eligible nearby original-epoch candidate, verified `DroppedItem` and original Item still current, known definition/amount, and a tracked original Player connection. If 0 or >1 candidates, stale state, source mismatch, or hard-budget exhaustion → **do nothing and leave the vanilla drop alone**. This eliminates the reference's deterministic “last barrel wins” error but does not rule out an unrelated nearby player drop. Therefore default destructive interception should still require exact provenance or be an opt-in explicitly labeled heuristic.

---

## 6. Candidate architecture comparison and tradeoffs

| Approach | Suppresses all default destruction payouts? | Attribution/item behavior | Verdict |
|---|---|---|---|
| **A. One pre-first-payout death fence (`LootContainer.OnDied`/`OnKilled` adjacent)** | **Potentially** yes, if all actual subpaths dominated and cleanup/death still runs. | Exact original entity, HitInfo may be available. But skipping full OnDied/OnKilled is dangerous: may also skip Kill, side effects, cleanup. | **Preferred strict research goal**, contingent on loaded-IL complete-path proof and surgical suppression only. |
| **B. Coordinated `DropUtil.DropItems` + `DropBonusItems` + any other proven producer** | **Potentially** yes for a pinned class family if sources exhaustive. | May keep source Item/container and bonus origin. Bonus hook already follows creation; cancellation resource cleanup must be proved. | **Second strict candidate**. Small finite seams allowed; do not globally intercept all Item.Drop paths or assume hook return is transactional. |
| **C. Fill-time `OnLootSpawn` / `LootFill.DelayFill`** | **No**, only changes future stored contents; cannot inherently stop death-time bonus scrap or affect old filled entities. | Does not need a breaker; can validate inventory fill rules by exact class and current publication. | **Best narrower `Enum.DropMode.ReplaceContents` candidate** if this is the only proven safe seam. |
| **D. Exact `Item.Drop` source capture, later intercept/redirect** | **Not necessarily**. A post-drop choice is already an effect; hard to guarantee no extra vanilla payout. | Strong provenance possible with original item/call frame; can support best-effort collection. | **High-value D2B research**; may avoid modifying vanilla fill/death path. |
| **E. Astro proximity/time `OnEntitySpawned` pickup** | **No**. A post-spawn collection heuristic. | Practical, simple, but can affect wrong players' drops and may grant/kill inconsistently. | **Conditional optional narrow mode**, non-destructive on ambiguous candidates, no strict Replace naming. |
| **F. Immutable managed host policy** | **Only if paired with proven A/B/C producer seam**. | Validated frozen rule selected synchronously; no arbitrary Luau callback; D7/D10 lifecycle clean. | **Required control plane**, not an independent drop mechanism. |
| **G. Hybrid fill + death bonus suppression** | **Potentially**, but old already-filled contents/refresh and many independent paths complicate publication time. | Separate ownership and observation fences; source provenance may be split across time. | **Possible only with complete per-prefab inventory of all paths**; avoid as initial D2 if one seam can qualify. |
| **H. Additive rewards after qualified death** | **No**, intentionally preserves vanilla. | Could call separate proven InventoryOnly Give on a committed, exact Player and record errors, but requires a qualified barrel death producer/actor and cannot guarantee net-zero-world payout. | **Useful small future capability** `BonusReward`/`AfterBreakReward` distinctly named; not `Replace`. |
| **I. Global vanilla loot-table edits, wildcard patching, synchronous Luau return callbacks** | Unsupported/unsafe under current invariants. | Makes ownership, precedence and rollback hard to bound; host-driven recursive VM entry forbidden. | **Reject for D1–D3**. |

**Simplest viable architecture conditional branch:** D1 establishes the pure validated rule model. D2 tests **one** barrel class through the least invasive genuinely correct seam. If no strict fence is proved, independently qualify `ReplaceContents` and/or a separately named best-effort original-item auto-collection. **Do not build a general transaction coordinator, arbitrary hook bus, full inventory-event journal, cross-server rule engine or broad Unity patch layer.**

---

## 7. Recommended public API, Enum conventions and semantics

### 7.1 Desired strict signature — future, not shipped

```luau
local Loot = game:GetService("Loot")

Loot:RegisterDropRule(
    "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
    Enum.DropMode.Replace,
    {
        { Item = "scrap", Min = 5, Max = 12 },
        { Item = "metal.fragments", Min = 10, Max = 25 },
    }
)
```

**Naming verdict:** `Enum.DropMode.Replace` is the accepted direction, as now explicitly amended in the current repository. `scripts/bootstrap.luau` already has sealed userdata-like enum values such as `GiveItemBehavior.InventoryOnly`, plus `GuiFont` enum metadata; it **does not** currently expose a global `Enum`, `Enum.DropMode` or `Loot` service. The minimal additive solution is one immutable `Enum` namespace containing one frozen `DropMode` namespace with a sealed typed `Replace` value; route global visibility and metadata through existing script bootstrap/API/definitions, preserving the old `GiveItemBehavior` global *without* aliasing or changing its type/equality/arguments. No Roblox `EnumItem`, `Instance` or dynamic enum reflection. Add future members only with separately specified behavior.

**Exact semantics of strict `Replace`:** For an actively published rule and an **eligible, fully source-qualified** destruction event on a specific supported prefab, select a single immutable rule before the first payout effect and replace **all** applicable default *physical item payouts*: previously stored contents, generic item dumps, computed bonus scrap, and any other enumerated vanilla item path. Suppress normal default item creation/drop only at proved safe sites; leave the entity's normal death/world retirement processing intact. A successful observed operation must contain no extra vanilla drops from covered paths. **This does not promise every requested world entity always spawns** under post-COMMIT failures/interference. Unknown class, precondition failure or patch drift makes the rule **ineligible/disabled** and is explicitly diagnostic; never quietly claim that the rule was applied.

**If strict cannot be proved**, use a separate `Enum.DropMode.ReplaceContents`, narrowly defined as replacement of the **next qualifying loot-fill operation** of a supported entity. Already populated, save-loaded or externally refilled barrels, death bonus scrap and physical drop mechanics remain vanilla until each is separately proved. Do not call this `Replace`. A hypothetical `Enum.DropMode.Add`, per-break callback, modifier or `ReplaceAll` alias is **deferred**.

### 7.2 Keep automatic collection distinct from custom Drops

**Recommended future experimental expression (not committed API):**

```luau
-- Separate opt-in behavior, not a DropMode.Replace alias.
-- Precise name/details require its own qualification.
Loot:RegisterCollectionRule("assets/.../loot-barrel-1.prefab", {
    Delivery = "InventoryOnly",  -- conceptual only; validate typed semantics if adopted
    Attribution = "ExactSource",  -- reject if source identity is not qualified
})
```

It tries to move **vanilla items already destined for the world** to an exactly qualified Player after a verified barrel break. Not all drops or every breaker must qualify. If full-inventory or ambiguous origin, leave vanilla alone where it is physically safe to do so; after a transfer attempt, the result may be **indeterminate**. Do not expose the conceptual string options as final API without a narrow schema decision. A `BestEffortHeuristic` opt-in, if ever accepted, must explicitly warn that it can misattribute an otherwise unrelated nearby drop; prefer exact-source-only default.

### 7.3 Optional deferred observational API, independent of policy

```luau
-- FUTURE after a qualified completion/attribution contract exists:
Loot.DropResolved:Connect(function(context)
    -- Immutable diagnostic-only snapshot; not a synchronous loot override.
    -- Prefab, EntityId, Mode, Outcome, Breaker/BreakerId only when proven.
end)
```

`Outcome` needs at least `AppliedVerified`, `Skipped`, `PartialOrIndeterminate` **when actually provable**. Avoid `Success` from a `RegisterDropRule` return or an attempted `Kill`; no fabricated actor, world-drop count or guaranteed delivery. D23 shared reservations/callback scheduler must carry any published signal.

---

## 8. Immutable rules, ownership, lifetime and conflict handling

### 8.1 D1 rule descriptor and validation

- Exactly 3 arguments: **full exact prefab**, a trusted typed `Enum.DropMode` value, and a dense array of `{Item, Min, Max}`. Reject unknown keys/extra args, sparse arrays, maps masquerading as arrays, cycles/metatable-triggered access and nested callbacks; use bounded raw table inspection and copied host scalars. A script mutating its input table after registration must not change a compiled rule.
- Prefab: exact lowercase full canonical path, qualified allowlist, UTF-8 <= the existing D20 **512-byte** prefab bound; reject nonexistent/unsupported class/config; no regex, `Contains`, wildcard or child-of-prefab inference. Do not rely solely on historical numeric prefab ID.
- Item: reuse existing `Items` canonical 1..128 lower-case ASCII/no-NUL name rules and exact definition resolver; reject unsupported/complex host Item definitions when the chosen physical route cannot safely create/drop them. No `ItemDefinition`, raw Item, source Unity transform or arbitrary object crosses to Luau.
- `Min`, `Max`: exact finite whole numbers, checked `0 <= Min <= Max`, and a measured configured maximum **per entry and aggregate**. Inclusive bounds; `0` means omit entry. Avoid `Max+1` signed overflow; roll each entry once per eligible event using a managed host-controlled RNG, with no promise of seed reproducibility after restart.
- D1 **proposed initial policy caps, subject to exact-host measurement**: at most 8 entries per rule, 16 rules per domain, and at most 32 planned physical stack/drop entities per triggering source; cap total quantity separately based on actual stackable/max-stack constraints. Do **not** expose as guaranteed production limits until qualification. Rule count across the 129 root+addon domain bound needs a global limit and measured lookup/fanout cost. No unbounded host source scans.
- Favor `RegisterDropRule(...) -> ()` (declaration only), no reconfiguration/unregister API initially. Do not mix per-break script callback with declarative rule input. Only allow registration from permitted declaration/publication contexts; no gameplay host effect occurs while a candidate/module is provisional.

### 8.2 Publication and ownership

Each registration is bound to its **defining domain lifetime**, VM/host lifetime and **D7 publication witness**. The current `FacadeSession` checkpoint mechanism snapshots commands/listeners and `Gui` publications; extend that precise owner/publish/rollback infrastructure for immutable loot-rule records, including nested failed first-load modules, root replacement, addons, foreign shared modules, required-dependency reconstruction and provider unregister. Do not bolt on an independently mutable global dictionary with its own transaction semantics.

- **Conflict:** At most one active `Replace` rule per **exact prefab** globally. Same-owner candidate replacing its own old rule may stage and atomically supplant it. Two unrelated committed root/addon owners requesting the same prefab must fail deterministic commit/preflight; do not use priority, load-order winner or opportunistic override. A candidate colliding with an unrelated active owner fails **its own publication**, preserving the previous active mapping.
- **Commit order:** preflight all rule conflicts, limits, eligible host method guards and host-version gates **before** any new active-state swap or `Registrar.Publish`. Publish an immutable per-prefab lookup snapshot at the same owner-thread D7 safe point, then retire the old owner's mappings. If code cannot atomically integrate conflict preflight with existing `FacadeWorld.Commit` and `CommitAddon`, D1 must stop rather than leaving half-published rules.
- **Synchronous decision:** capture one strongly owned immutable *rule descriptor* at the qualified payout boundary, not a Luau table or callback. Rule replacement/unload changes **future admission**, not the selected rule halfway through an already entered host operation. Never block on publication or hold locks across hook calls. Host state access stays on owner thread; wrong-thread entry rejects without dereferencing Rust/Unity/VM.
- **Provisional call crossing domains:** D10's *admitted operation* publication context takes precedence; using an active domain's shared facade from a provisional candidate cannot publish active rules. No callback driven recursive Luau entry, no host-side Luau selection.
- **Recovery:** normal candidate failure discards staged rules and leaves active old owner; successful replacement retires exact old owner; fatal VM failure discards queued work/rules and performs the existing one-allowance reconstruction from committed source, **never replaying any failed/in-flight physical loot effect**. Full CarbonLuau unload breaks D20 observer continuity and still needs a restart for entity-dependent qualified mechanisms.
- **Compatibility:** current API remains `0.6.5-experimental` until a separately authorized additive release identity is assigned. D1 should not change native ABI, provider protocol, Luau pin, shipping package, or existing C1/Gameplay event surfaces by default. Update generated metadata/tooling only for an actually qualified/public D3 scope.

### 8.3 Resource budgets and no hidden work

Existing D23 gameplay Signals share **128 captures/frame, 128 deliveries/frame, 32 deliveries/domain/frame, 4,096 visits/frame, 512 reservations, 2 KiB event payload** and measured retained transport limits; per-session listeners are already capped at 128/signal, 256 total, with 256 pending events and an independently bounded native scheduler. **Policy execution is not an observational Signal:** do not consume callback reservations to make the synchronous mutation safe or assume budgets cover item/world work. Introduce a separately measured bounded per-payout plan/work gate, explicit per-frame intake and source-depth cap, and saturated diagnostics; fail *before* COMMIT on rule-capacity exhaustion where possible. A hard host-call envelope is required because VM deadlines do **not** interrupt a long managed/Rust method call.

A private post-spawn collection prototype may use a fixed-size expiration ring keyed by exact original entity epochs and item identities, maximum candidate visits/active origins, and one existing owner-thread pump; **no three-second timer per barrel, no growing `List<BasePlayer>`, no full-world entity search, no arbitrary retries or unbounded histories**. Strong host Item references live only during one qualified synchronous PREPARE/COMMIT/VERIFY; retained original identities are weak/scalar, with tracked at-most-once release.

---

## 9. Physical mutation, ownership, failure and verification

### 9.1 Distinct physical operations — don't reuse D13 facts beyond their scope

**Strict world `Replace` planned operation:**

1. **ADMIT/QUALIFY** exact source barrel incarnation, class, prefabs, active committed rule, reason/path eligibility and owner thread; identify every default payout branch *before* suppression. Unknown path/actor/source is a **pre-COMMIT skip** with untouched vanilla path (and diagnostics), not claimed successful `Replace`.
2. **PREPARE** roll bounded quantities once, validate max resulting stack count, target spawn/drop point, supported item identities, planned resource capacity, and postcondition probes without invoking callback-capable acceptors or mutating host inventory. A plan cannot promise subsequent host success.
3. **COMMIT** begins **before first host resource creation, clear, suppression or other irreversible host effect**, whichever occurs earliest. A two-seam path must ensure a shared single selected rule/batch; after COMMIT do not change to an old/new rule or “fall back to vanilla” and duplicate payouts silently.
4. **VERIFY** for every returned created Item: record exact temporary identity/amount, whether successfully transferred into a qualified world `DroppedItem` or terminally consumed/cleaned, check returned world entity original Spawn completion, reciprocal `DroppedItem.item`/Item source binding, definition/count and the relevant default-suppression observations. Count received and dropped quantities; report `AppliedVerified` only for fully observed operations. A return pointer alone is insufficient. For `Item.Drop` that may merge or despawn quickly, define the **immediate qualified host completion** scope; do not assert later player possession or long-term world persistence.
5. **Post-COMMIT exception/rejection** is **partial/indeterminate host failure**; never retry, roll back Rust inventory, grant compensation, pretend no effects, or publish a success. Clean **only** temporary returned Items with a *separately qualified cleanup* path and observe final terminal state; cancellation/failed `OnItemRemove` cleanup is indeterminate. Unreturned host-internal allocations remain the host failure domain, per D13.

**Optional auto-collection of an original world Item:**

1. Provenance by exact source causal Item/drop linkage; fresh D11 Player connection; a single observed still-live, original `DroppedItem`, valid parent/world association and bounded D18 physical before snapshot. Any unknown/ambiguous state **before** transfer → leave original vanilla world item.
2. Prefer the **same actual Item** transfer into main/belt/wear under a proved no-world-drop, exact-slot operation (if supported), with one Player mutation gate shared across domains (coordinate with the existing D13 gate rather than inventing a competing one). This is **not** already implemented for world-owned Items; the D13 `GiveInventory.Create` path starts from newly created temporary resources.
3. After COMMIT verify physical receiver delta, returned Item identity/terminal owner and exact D11 token. If required, retire world entity through a path whose **B3 completion** can be proved. If transfer auto-retires the original drop, do **not** `Kill()` again; double-deletion may destroy an accepted Item. If retirement is separate, its veto/failure prevents a claim of exclusive one-copy delivery. Any uncertain result is indeterminate, never a boolean claim that both sides were safely accounted for.
4. A clone-based fallback may transfer only fully supported **plain default** item identities; never claim skin/condition/mod/instanceData equivalence without explicit snapshots and qualified clone semantics. The policy must explicitly choose which item survives on any failure; no success-like “pickup completed” merely because `GiveItem()` returned or `Kill()` was requested.

### 9.2 External trusted interference and reconciliation

External plugins may override `OnLootSpawn`, `OnContainerDropItems`, `OnBonusItemDrop`, `OnEntityKill`, acceptance/stacking and item removal; those callbacks may mutate Item amounts, target slots, parent links or world state. **I12 does not forgive** choosing unsafe generic GiveItem, wrong method/flags, missing source checks, intentionally dropping when prohibited, stale original identity or failed VERIFY. Classify third-party interference **only when actual operation-relevant mutation is observed**. No full-process exclusivity, causal attribution for arbitrary plugins or exact-once across restart can be promised. A failure of a vanilla, qualified callback path under baseline is *our adapter's* problem.

A rule's three outcomes are **(a)** pre-COMMIT not admitted/skipped with vanilla untouched; **(b)** post-COMMIT applied and verified within its stated instantaneous host scope; **(c)** post-COMMIT partial/indeterminate, with no retry. This is the same D13 discipline, not the same *physical Give/Take verifier*. For deferred diagnostics, delivery is best effort and may be dropped on D23 backpressure; never make monitoring delivery a precondition for physical cleanup.

---

## 10. Exact Windows/Linux qualification matrix and mandatory receipts

All new D0/D2 host tests must use the recorded pinned Rust/Carbon/Unity tuple **separately on Windows and glibc Linux**, including *backing and loaded* IL hashes, Carbon Harmony patch topology and checked method tokens. A synthetic managed/native model does **not** qualify Rust object ownership.

| Gate | Controlled test and mandatory observation | Pass/stop condition |
|---|---|---|
| **Q0 identity/config** | Freshly spawn each of five historical barrel paths. Read canonical full prefab, prefabID, concrete type, inventory/container identity and settings; inspect asset variants and save-loaded instances. | Explicit allowlist for **only** actual supported class/path tuple; no substring. |
| **Q1 baseline callgraph** | Single vanilla break per prefab; trace `SpawnLoot`, fill, `Die/OnDied/OnKilled`, `DropUtil`, bonus, per-Item Drop, Spawn, `OnItemDropped`, Kill, B3 world removal. Record exact IL order and physical Item/world counts before/after. | Complete per-prefab source and **all outgoing item paths** or explicit omitted/unqualified branches. |
| **Q2 stored vs bonus** | Compare prefilled contents, save-loaded inventory, empty container, timed refresh, no fill, tea `Scrap_Yield`/modifiers, boosted/default scrap, oil-barrel unique contents. | Item source classed as stored, bonus, other; no assumption `OnLootSpawn` equals payout. |
| **Q3 spawn-time readiness** | Capture `DroppedItem.item`, parent, amount, UID, `DropReason` at early `OnEntitySpawned`, full B1 Spawn completion, `OnItemDropped`, and after `DropBonusItems` return. | Exact timing of source identity and `DropReason`; no early-read use-after-retire. |
| **Q4 kill/combine/ownership** | Existing drop `Killed` successfully/vetoed, `DroppedItem.OnKilled`, Item Remove veto, pooled/reused net ID, dropped-world merge, player pickup, despawn; count original Item ownership and B3 terminal certification. | No `Kill()` request counted as actual deletion; no original Item left in two owners/unobserved. |
| **Q5 player delivery** | Authored direct human, reconnect before transfer, full main/belt/wear, blocked placement, slot conflict, receiving another item, grant callback mutation, explicit generic Give fallback, transfer original Item if candidate exists. | One exact connection lifetime and fresh D13-style verify; no admitted accidental world-drop fallback. |
| **Q6 proximity adversaries** | Two overlapping barrels broken by different Players within <3 m/<3 s; one manually dropped Item, unrelated plugin drop, one stack merge, out-of-order spawned callbacks, timer expiry, >cap stress. | Exact-source path only associates intended original; heuristic path **refuses ambiguous** and never reports strict attribution. |
| **Q7 actor semantics** | Direct melee, direct gunshot/projectile, explosive, trap, environmental, NPC, no HitInfo, disconnect after death, last hit by different Player, wounded/nonfinal attempt. | `Breaker` only from proven direct qualified D11 lifetime; otherwise nil. Weapon string is diagnostic only. |
| **Q8 strict suppressors** | For each candidate `Replace` producer seam, compare vanilla vs configured min/max/zero rule; instrument all default/bonus outputs, with missing/throwing `ItemManager.Create`, `Item.Drop` null/throw, plugin veto, disabled `Server.dropitems`, and stale prefab config. | **No extra default payout** on every verified eligible path; no swallowed host failure or leaky created resource. Missing path **stops strict mode** for that prefab. |
| **Q9 fill-only fallback** | Register before/after initial `SpawnLoot`, on save-loaded incumbents, refresh, individual prefab paths and bonus scrap; destructive break check. | `ReplaceContents` changes only documented fills; zero implied destruction-wide behavior. |
| **Q10 publication/conflicts** | Root + 100 addons, duplicate rule across owners, same-owner replacement, nested first-load module error caught, active foreign facade from candidate, provider unload, timeout/rebuild, two policy publications during a host callback. | D7/D10 atomic staged/active mapping; selected immutable rule never switches mid-call; no effect/replay after failed candidate. |
| **Q11 bounds/teardown** | Worst-case 8 entries/16 rules (or measured final caps), 1/128/129+ burst, many simultaneous drops, wrong thread, recursion, callback exception, over-cap source visit, weak watch expiration, allocation faults and resource refund. | Zero unbounded scanning/timers/strong Player retention; pre-COMMIT definite rejection; no leaks, no recursive Luau entry. |
| **Q12 independent feature regressions** | D20 Spawn epoch and 24-target observer; B1 full completion; B3 removal/pool; C1 no-scan read facade, D13 Give/Take and shared gate, D23 four existing event kinds, GUI, addons, persistence, tooling, packaging. | Current gates remain PASS; new patch does not silently drift/invalidate qualified topology or quotas. |
| **Q13 shipping** | Windows x64 + Linux x64 actual managed/native real-VM, full tests, Linux ASan/UBSan/leak instrumentation, replay/reload/unload, clean install & final source CI; controlled client receipt only if a separately claimed client outcome. | Evidence and artifact hashes reported per platform and source revision; no claimed Shockbyte/untested build/client PASS. |

**Exact evidence bundle per host:** Rust and Carbon binary SHA-256/MVID and loaded hook fingerprints; per-method method token/IL hash/EH/call-reference inventory; actual source-prefab ID/type/config/position; per-hook owner thread and nesting stamp; pre/post `Item` UID, shortname, amount, parent, world entity original epoch, actor D11 token (privately), `DropReason` at each stage; intended rule/roll; physically verified final Player and world inventories; delta/counters; diff against vanilla; fixture source and SHA, stdout/server log hashes, error/negative receipts and explicit feature stop reason. Do not commit proprietary decompiled IL dumps into public source unless permitted; pin them with provenance and store locally as qualified evidence.

### Minimum two positive paths before any public D3

- **Pure vanilla and one custom `Replace` path** on the exact first prefab, with stored contents + bonus `Scrap_Yield` and physically observed absence of originals, repeated with zero/max/empty rules, on **both** OSes; OR evidence for a narrow `ReplaceContents` first if complete-suppression gate fails.
- **Original-item auto-collection** only if explicitly pursued: successful and full-inventory failure paths without clone duplication, proven `DroppedItem`/Item terminal ownership, verified exact breaker, successful/vetoed Kill, nearby simultaneous break negative, on both OSes. A single happy-path Astro log is **not** enough.

---

## 11. D1–D3 implementation roadmap (after D0 adoption, not authorized here)

| Phase | Contained implementation scope | Required exit gate / stop |
|---|---|---|
| **D0 — this task (research only)** | Reconcile qualified B1/B3/C1 and the Astro evidence, enumerate exact current host method and prefab proof requirements; choose honest modes. | This report; **strict physical source proof still U/L**. No production files changed. |
| **D1 — private pure rule/Enum/publication substrate** | Minimal frozen `Enum.DropMode` typed value/descriptor model; 3-arg validation; per-owner staged immutable registry; conflict preflight; no host loot mutation, no public placeholder. Model/update managed/native publication with exact ownership and bounded caps. | Staged commit/rollback and same-owner replacement, nested failure, foreign context, 100 addons, allocation-fault, zero-effect PASS. Stop if conflict preflight cannot be made atomic. |
| **D2A — strict one-prefab host proof and private adapter** | Select a single **proved** payout decision fence (or complete finite coordination seams). Trace all vanilla item paths including bonus modifiers; implement minimal guarded suppression + immutable managed roll, temporary Item/world accounting and post-COMMIT VERIFY only in controlled fixtures. | Both Windows/Linux current-build exact-IL + live physical tests show no extra default drops; correct per-Item cleanup/partial failures. If any class/bonus path escapes, **do not expose strict Replace**. |
| **D2B — independent narrow fallback/collection adapter (only if warranted)** | Either full proved `ReplaceContents` fill adapter **or** exact-origin/world-owned Item transfer prototype, separate from D2A. Strict provenance first; optionally opt-in heuristic with ambiguity refusal. | Distinct scope, grant/kill/merge/teardown and interference matrices PASS on both OSes. No rename of collection into Replace. |
| **D3 — public qualified minimal D surface** | Publish only mode(s) whose producer/ownership proof passed, positional `Loot:RegisterDropRule`, `Enum.DropMode` namespace, metadata/generated Luau definitions, tooling/docs/examples; if collection qualified, expose separately and document uncertainty. | Full Windows/Linux current-target regressions, runtime/VM, native fault/sanitizers, addon lifecycle and hosted source CI. Preserve `0.6.5`/v0.5.0 identity until explicit additive version/release approval. |

**Deferred beyond D3:** conditional per-player/weapon/permission policy predicates; `DropResolved`/`BarrelBroken` deferred Signal; arbitrary loot-table mutation; cross-break aggregated yield; a general EntityDied API; generic `Loot` interception of every Rust prefab; global enum migration; and user script callbacks synchronously deciding what a barrel drops.

---

## 12. Precise remaining architectural questions and stop conditions

**Must be resolved statically from exact current loaded Windows/Linux IL before D2A:**

1. For every allowlisted barrel prefab: exactly which managed `LootContainer` or derivative, loaded component configuration, fill time and death/destroy caller **actually** executes?
2. Does all default *physical* payout share a single safe seam, or which finite complete set includes contents, `DropUtil`, `DropBonusItems`, other direct `Item.Drop`, bags/gibs and modifiers?
3. At what point is `HitInfo` still the final qualifying human source, and is `LootContainer.DropBonusItems(initiator)` invoked inside the same source lifetime? Is exact private source frame maintained through all synchronous drops, including nested Carbon hooks?
4. Can `OnContainerDropItems` be suppressed without leaving orphaned/undropped original Items? Does `OnBonusItemDrop` after creation require a cleanup path that can be vetoed? Can original OnDied/Kill continue independently if payouts are suppressed?
5. When do `DroppedItem.item`, `Item.parent`, Item UID, `DropReason`, network ID, full Spawn and Item.Drop-return become valid and reciprocal? Are there alternative world-drop classes?
6. Does killing a `DroppedItem` retire/destroy the Item or only the world Entity? Can a direct original Item be safely transferred to Player main/belt/wear without a clone, and who owns the world wrapper on successful pickup?
7. Can Carbon's existing public hooks themselves provide complete and deterministic interception, or must a small exact-build guarded Harmony patch be used? How does it interact with B1/B3 patch topology and provider startup?

**Must be resolved by actual Windows/Linux host tests (not static inference):** real item world outcomes per prefab, tea and bonus behavior, simultaneous break/callback interference, stale same-account reconnect, Create/Drop/Kill veto and partial-state recovery, original-item merge/pickup effects, loaded patch fingerprints, resource/CPU budget and unchanged B1/B3/C1 regressions. **B3 successful entity removal cannot substitute for a confirmed Player grant or ownership cleanup.**

**Stop conditions:** unsupported prefab/class; unmatched original Item identity; unknown outgoing physical payout path; incomplete default suppressor; cleanup path that can lose host-owned Items without an explicit partial-failure policy; hotload without D20 continuity; wrong-thread host access; publication collision that could affect active domains; dependency on synchronous host→Luau callback; unbounded source scan; any requirement to claim guaranteed no world duplicates/rollback/atomic transfer despite unsupported host evidence. A feature blocked by one source must fail **only that source/prefab/mode** closed, not disable unrelated qualified Players/Workspace/inventory APIs.

### Conclusion and Codex handoff

**GAMEPLAY-D0 NEEDS HOST QUALIFICATION — the exact Rust 25653776 Windows/Linux per-prefab death→contents/bonus→Item.Drop callgraph, complete default-suppression seams, and source/Item ownership handoff remain unproved.**

The working Astro plugin justifies a *simpler, bounded, opt-in collection/redirect capability* that may be commercially useful sooner. It **does not** establish strict `Replace`, exact breaker attribution or ownership safety. **Do not spend effort on a general loot engine before first proving one barrel's exact physical path.** Start D1 as a private pure registry only after accepting this specification; prioritize the single-seam strict D2A hypothesis, and keep D2B fill-only/original-item collection independent if strict proof fails.

---

## Source index (stable links, public vs exact-host record)

**Current CarbonLuau authority and implementation (R/Q):**

- [`AICONTEXT.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/AICONTEXT.md), [`AGENTS.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/AGENTS.md), [`Invariants.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/Invariants.md), [`GameplayCDArchitecture-Research.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayCDArchitecture-Research.md).
- [`GameplayC1-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayC1-Validation.md), [`GameplayB1-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayB1-Validation.md), [`GameplayB3-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayB3-Validation.md), [`GameplayFoundationA-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/GameplayFoundationA-Validation.md).
- [`PlayerInteractionFoundation1FB-Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/PlayerInteractionFoundation1FB-Validation.md), [`PlayerInteractionFoundation1FC.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/PlayerInteractionFoundation1FC.md), [`InventoryMutationM2Validation.md`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/docs/InventoryMutationM2Validation.md).
- [`scripts/bootstrap.luau`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/scripts/bootstrap.luau), [`FacadeSession.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/FacadeSession.cs), [`GameplayEventBudget.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/GameplayEventBudget.cs), [`CarbonLuau.EntitySpawned.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/CarbonLuau.EntitySpawned.Carbon.cs), [`InventoryGrant.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/Facade/InventoryGrant.cs), [`CarbonLuau.Inventory.Carbon.cs`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/src/CarbonLuau/CarbonLuau.Inventory.Carbon.cs), [`FacadeBridge.cpp`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/native/src/facade/FacadeBridge.cpp), [`Publication.cpp`](https://github.com/gmoddev/CarbonLuau/blob/35701d5/native/src/runtime/Publication.cpp).

**Public/generated Rust/Oxide excerpts, *not pinned IL* (P):**

- [`OnLootSpawn`](https://docs.oxidemod.com/hooks/resource/OnLootSpawn); [`OnContainerDropItems`](https://docs.oxidemod.com/hooks/entity/OnContainerDropItems); [`OnBonusItemDrop`](https://docs.oxidemod.com/hooks/item/OnBonusItemDrop); [`OnBonusItemDropped`](https://docs.oxidemod.com/hooks/item/OnBonusItemDropped).
- [`OnEntityDeath`](https://docs.oxidemod.com/hooks/entity/OnEntityDeath); [`OnEntityKill`](https://docs.oxidemod.com/hooks/entity/OnEntityKill); [`OnEntitySpawned`](https://docs.oxidemod.com/hooks/entity/OnEntitySpawned); [`OnEntityDestroy`](https://docs.oxidemod.com/hooks/entity/OnEntityDestroy).
- [`OnItemDropped`](https://docs.oxidemod.com/hooks/item/OnItemDropped); [`OnItemPickup`](https://docs.oxidemod.com/hooks/item/OnItemPickup); [`OnItemRemove`](https://docs.oxidemod.com/hooks/item/OnItemRemove); [`OnDroppedItemCombined`](https://docs.oxidemod.com/hooks/item/OnDroppedItemCombined).
- [Historical prefab IDs/classes](https://github.com/OrangeWulf/Rust-Docs/blob/master/Entities.md); [2025 independent catalog](https://gist.github.com/c0deR3D/a1484ff0925b5347bd1edb7661eba921); [uMod barrel paths](https://umod.org/community/rust/14495-prefab-name-for-this-barrel); [Facepunch entity path conventions](https://wiki.facepunch.com/rust/Entities).

**Reference implementation (user-provided):** Full `AstroBarrelPickup` C# source in the current request, 2026-10-11. It is analyzed directly in §4; it is not a repository source or pinned-host IL artifact.
