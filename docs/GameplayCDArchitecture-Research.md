# Gameplay C/D supplied static research — preserved source

User amendment (2026-10-10): future typed enum namespaces use `Enum.DropMode`,
not `Loot.DropMode`. References below to service-local DropMode or rejecting
`Enum` are superseded by that explicit naming direction. C1 introduces neither
Loot nor an Enum implementation and preserves existing GiveItemBehavior.
Only C1 is authorized here; C2 quantity observation and all D work remain scoped
future work. Canonical C1 adoption: D18 and GameplayC1-Validation.md.

---

# CarbonLuau — Gameplay C & D: Static Architecture Research and Decision Record

**Date:** 2026-10-09  
**Repository:** [gmoddev/CarbonLuau](https://github.com/gmoddev/CarbonLuau)  
**Inspected `main` commit:** [`35bd8c3bb5d041063ba3388d23cc2cd37daa2808`](https://github.com/gmoddev/CarbonLuau/commit/35bd8c3bb5d041063ba3388d23cc2cd37daa2808)  
**Status:** **Design / static research only. Not implemented, qualified or published.**  
**Authority:** `AICONTEXT.md` → `docs/Invariants.md` → current implementation and qualification records. This report proposes *additive* contracts; it does not supersede canonical decisions until explicitly adopted.

## 0. Executive decisions

1. **C1 first: implement `Player:GetInventory()` as an exact-connection, domain-owned, read-only inventory facade.** Delegate its `CountItem`/`HasItem` methods to the already qualified D18 scanner and preserve all current `Player` inventory methods unaltered. No global `Inventory` service.
2. **C events remain gated.** `OnItemAddedToContainer`, `OnItemRemovedFromContainer`, `OnItemStacked`, `OnItemUse` and related hooks are change *hints*, not a universal post-mutation transaction log. The preferred common engine computes positive/negative differences between bounded, physically validated main/belt/wear snapshots at a **proved quiescent observation boundary**. If no complete boundary/producer coverage can be demonstrated, do not expose unqualified `ItemAdded`/`ItemRemoved`; a deliberately labeled sampled/reconciled `ItemChanged` may be a separately approved weaker alternative.
3. **D public syntax should be positional:** `Loot:RegisterDropRule(prefab, Loot.DropMode.Replace, drops)`. `Loot.DropMode` is a small immutable typed enum local to the service, modeled on existing `GiveItemBehavior`; do not create a global Roblox-like `Enum` service.
4. **Do not equate `Replace` with `OnLootSpawn` cancellation.** The public Rust sources show separate loot fill, generic container-drop and bonus-scrap branches. Full replacement of *physical destruction payouts* requires an exact-class, exact-build complete-path suppression fence. A narrower **`ReplaceContents` / `ReplaceLootFill`** operation could be accepted earlier if the required full `Replace` proof fails; it must not masquerade as complete replacement.
5. **Never run a synchronous Luau decision callback from damage/death/drop hooks.** If the author wants to react to the player breaking a barrel, choose either a **prevalidated declarative condition evaluated in managed host code** at a proved decision boundary, or a distinct **deferred observational** `Loot.DropResolved`/`Loot.BarrelBroken` Signal that cannot affect that break. Human breaker attribution is a separate proof obligation, not inherited from `Workspace.EntityDestroyed`.
6. **Phase ordering:** C1 read-only facade first; C2 private net-delta research and D1 static/pure policy registry can follow independently. Implement D's irreversible world-loot path only after its host proof, and release C Signals only after their observation contract is qualified. Neither needs the other as a prerequisite.

**Qualification classification throughout:** **R** = directly checked current repository source; **Q** = previously recorded exact-host proof, *not independently rerun here*; **P** = public generated source/docs only; **I** = inference/design hypothesis; **U** = unproved; **L** = requires new live Windows/Linux tests. New pinned-build IL for inventory Signals or loot is **not available in the versioned repository**; no claim of newly disassembled current-host IL is made. Existing B3/A reports contain their own exact-host IL evidence and checksums.

## 1. Repository baseline and invariants that must survive

### 1.1 Current capability, not a new invention

| Capability | Status / owner | Implication for C or D |
|---|---|---|
| Connection-bound `Player` proxy | Qualified, D11; `PlayerDirectory`, bootstrap | Reuse the original monotonically assigned token **and** original host Player/connection objects. Never look up a reconnect by account ID during deferred delivery. |
| `Player:CountItem` / `HasItem` | Qualified D18 / Player-1C; physical main/belt/wear | C facade should call these exact managed operations or their shared adapter, not a new query implementation. |
| `Player:TakeItem` | Qualified D13 / Player-1F-A | Preserve exact before/after comparison, host returned amount, single shared gate and post-COMMIT indeterminate semantics. |
| `Player:GiveItem(..., GiveItemBehavior.InventoryOnly?)` | Qualified in supported-host scope D13 / Player-1F-B/C | Do not route drop spawning through GiveItem. Existing `ItemManager.Create`, exact-slot `MoveToContainer`, terminal returned-item observations and cleanup are **inventory-only** evidence, not world-drop qualification. |
| Immutable `Items` service, item lookup | Existing | Reuse exact shortname normalization and item-definition lookup for drop declarations; do not expose `ItemDefinition`. |
| `Signal:Connect`/`Connection:Disconnect` | Existing Gameplay A/B1/B3 | Reuse existing callback roots, host ops 6/7, D7 checkpoints and existing callback scheduling. Inventory-targeted listener metadata is an additive typed extension. |
| `Workspace.EntitySpawned` | B1 PASS | Exact D20 completed-Spawn, original incarnation and no replay. Could help **identify eligible prefabs**, but does not prove loot-generation timing. |
| `Workspace.EntityDestroyed` | B3 PASS | Only immutable `{Id, Prefab, Position?}` of proven original-incarnation removal. Has **no Breaker/Killer** and cannot retroactively suppress drops. |
| Owner-thread VM/host access and recovery | I4/I9/I10, D7/D9/D10 | No host recursion, unbounded callback, new scheduler or ad hoc publication registry. |

Current development API is `0.6.5-experimental`; last published package/tag remains `0.5.0`/`v0.5.0` according to repository records. Native ABI `1.5`, provider protocol `1.2`, addon schema `1`, pinned Luau revision `c6b830185af962c82003f86784e2fe036357c830`. Research changes none of these. Do not retroactively assign a public `SinceApi` until authorization and qualification.

### 1.2 Hard invariant map

- **I1/I3:** Rust → Carbon → managed adapter → native bridge → Luau; no raw `BasePlayer`, `BaseEntity`, `Item`, `ItemContainer`, `HitInfo`, Unity object, managed pointer or GCHandle crosses the script boundary. A returned facade never assumes host ownership.
- **I4/I8:** game-state read/write and VM entry on owner thread; host ingress must not recursively call Luau. Bounded per-call CPU/work, retained bytes, captures, fanout and queue admission; wrong-thread ingress must not touch unsafe host state.
- **I9/I10/D9:** controlled errors; fatal integrity failure retires whole shared VM; no callback/policy/effect replay or attempted host rollback; deterministic domain/provider/VM teardown.
- **D7/D10:** a declaration belongs to its **defining resource-owner domain**, but host effects/registrations are governed by **the admitted caller's publication context**. A committed foreign facade cannot launder a provisional call. Candidate/module failure discards staged resources; an active old domain remains authoritative until an atomic safe-point replacement.
- **D11:** use exact original Player connection lifetime at registration, production, admission and mutable query; same UserId reconnect can never validate a stale inventory facade or actor proxy.
- **D13/D18:** inventory is not transactional. PREPARE cannot call callback-capable acceptance; COMMIT begins before first irreversible host call; VERIFY requires physical facts; false only pre-COMMIT; success only after specific proof; uncertain post-COMMIT result is indeterminate. Shared main/belt/wear scan is bounded to 128 entries. Existing `Give`/`Take` methods and their gate are unchanged.
- **D20:** world entity epoch includes successful full Spawn chain, original weak identity/registry occupancy, and fail-closed hash/topology; do not treat network ID or an attempted Kill as a lifecycle identity.
- **D23:** host observations are deferred best-effort Signals; no callback result may change the triggering Rust operation. Policies are separate immutable prevalidated host decisions; observational delivery can drop on overload.
- **I12:** trusted equal-privilege plugins can mutate operation-relevant host state. This prevents claims of whole-process isolation but **does not excuse** wrong adapters, expected vanilla failure cases, invalid inputs, omitted verification or silent physical drops.

## 2. Recommended public APIs

### 2.1 Gameplay C — Player-owned `PlayerInventory`

```luau
local inventory = player:GetInventory()

print(inventory:CountItem("scrap"))
if inventory:HasItem("scrap", 25) then
    -- Existing Player:TakeItem / GiveItem remain supported unchanged.
end

-- Expose these only AFTER the net-change capture contract qualifies:
local added = inventory.ItemAdded:Connect(function(context)
    print(context.Item, context.Amount)
end)
local removed = inventory.ItemRemoved:Connect(function(context)
    print(context.Item, context.Amount)
end)

added:Disconnect()
removed:Disconnect()
```

**Facade public contract:** `Player:GetInventory() -> PlayerInventory`; `PlayerInventory:CountItem(ShortName) -> number`, `PlayerInventory:HasItem(ShortName, Amount?) -> boolean`; optionally `PlayerInventory.ItemAdded` and `ItemRemoved` later. It owns **one Player connection**, not an inventory-UID or a service-wide subscription. `GetInventory()` validates the exact live Player and returns a domain-bound immutable proxy. Reads revalidate on each call and retain D18's missing/invalid-state controlled error. `GetInventory()` itself should not eagerly scan, create a watch or subscribe.

**Compatibility:** Preserve `Player:HasItem`, `CountItem`, `GiveItem`, `TakeItem` exactly. A `PlayerInventory:GiveItem`/`:TakeItem` alias is **not required** for C and should be deferred rather than accidentally inventing a second mutation gate or distinct behavior. Existing `GiveItemBehavior.InventoryOnly` remains the only qualified grant behavior. A facade object obtained in addon A and exported to addon B keeps A's resource ownership; D10 admission still governs B's provisional context.

**Qualified-event context (conditional):**

```luau
export type InventoryQuantityContext = {
    Item: string,    -- canonical shortname, copied scalar
    Amount: number,  -- positive checked integer = |After - Before|
    Before: number,  -- last complete accepted physical observation
    After: number,   -- latest complete accepted physical observation
}

-- Optional, same event engine, not a new host source:
export type InventoryChangedContext = {
    Item: string,
    Delta: number,  -- signed checked integer
    Before: number,
    After: number,
}
```

`ItemAdded` fires for positive **net** quantity change; `ItemRemoved` for negative, with positive `Amount`. Both represent changes **between two accepted inventory observations**, not per-item-UID insertion/removal, attempted operation count, guaranteed causal attribution or destruction. A main→belt transfer, split, merge or equipment move with unchanged total yields zero net event if both observations bracket the complete operation. A world drop, consumption or transfer to another Player yields a negative delta on the source if captured; receipt by another Player yields independent positive delta. A batched `+10, -10` between observations yields **no event**. There is no promise of every intermediate action, paired cross-Player atomicity, guaranteed delivery, or source/actor classification.

**Important:** This wording alone does **not** prove the observations bracket stable committed states. If a selected host boundary samples while a multi-turn transfer is half-complete, it can still manufacture a transient gain/loss despite technically valid physical snapshots. A real qualified boundary—or a materially weaker “sampled inventory state” API with truthful documentation—is a release gate. Do not advertise the two preferred Signals before that decision.

**`ItemChanged` evaluation:** Internally use exactly **one** signed-delta record and one snapshot comparator for all three names. Public `ItemChanged` can provide concise sign-agnostic subscription and expose `Before`/`After`; it neither fixes the missing completion fence nor requires a third producer. Recommended: **conditionally accept** as a convenience alias if meaningful demand exists; do not broaden the initial feature merely for symmetry. If only sampled reconciliation proves viable, `ItemChanged` with explicit sampled semantics is a less misleading first public shape than unqualified `ItemAdded`/`ItemRemoved`.

### 2.2 Gameplay D — positional, immutable `Loot` rules

```luau
local Loot = game:GetService("Loot")

Loot:RegisterDropRule(
    "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
    Loot.DropMode.Replace,
    {
        { Item = "scrap", Min = 5, Max = 12 },
        { Item = "metal.fragments", Min = 10, Max = 25 },
    }
)
```

The illustrative prefab is **known from public historical prefab catalogs**, not yet verified against the repository's latest pinned server assets and actual class chain. Strictly identify canonical full prefab names; no substring matching. The service exposes a frozen typed `DropMode` namespace with sealed singleton `Replace`. No global `Enum`, stringly typed mode, Roblox Instance hierarchy or arbitrary hook names. No implicit priority. **Use exactly three positional parameters** in the first edition; do not add a fourth callback argument merely to make the signature extensible.

**Proposed full `Replace` meaning (strict):** For a **new, eligible and fully qualified** entity/drop attempt within the rule's declared trigger, the rule substitutes for **all normal physical item payouts covered by the barrel's documented destruction behavior**—including applicable stored contents, generic container dumping and bonus drops. It does *not* promise a transactional world effect. If the host source cannot provide a single reliable suppression/selection model over all those routes, **full `Replace` is unsupported**. `OnLootSpawn` substitution by itself qualifies only *content generation*, and must have a distinct future `Loot.DropMode.ReplaceContents`/`ReplaceLootFill` name and limitations if exposed.

**Rule descriptor:** Validate an exact prefab, supported `LootContainer`/class family, typed mode, strictly sequential dense `Drops` array, allowlisted keys `Item`, `Min`, `Max`, canonical 1–128 ASCII item identifiers through existing `Items` resolver, finite **integer** Min/Max with `0 <= Min <= Max`, fixed hard number-of-entries and total maximum quantity/work/host entity creation, creation/drop viability and no raw host data. A zero roll is an intentional omission. No Lua closures, function-valued drop fields, metatables or host method references in a compiled rule. Unknown keys error. Copy into a fixed-size managed immutable descriptor **during declaration**; changes to the original table never affect a published rule. An empty replacement rule (`Drops={}`) intentionally suppresses all covered loot **only if** such suppression can be proved; otherwise reject.

**Random semantics:** Each matching qualified event selects each inclusive range once in the managed owner-thread adapter. Checked `Max + 1` where required; no overflow; no promise of global deterministic RNG sequences, reproducible seeds across server launches, or special luck/tea modifiers unless explicitly defined. No arbitrary Luau execution at policy decision time.

### 2.3 Player-break callbacks: two genuinely different requirements

**A. React after someone broke the barrel (eligible for later observational work):**

```luau
-- Future API, not yet proven or implemented.
Loot.DropResolved:Connect(function(context)
    -- Context.Prefab, Context.EntityId; optional exact-lifetime Breaker,
    -- BreakerId and an explicitly defined status, only if independently proven.
end)
```

This is a D23-style bounded deferred Signal; its callbacks cannot change or cancel the same drop. Never infer `Breaker` from B3 `EntityDestroyed`'s nonexistent killer field, or re-resolve `BreakerId` to a current Player at delivery. Prefer a status that distinguishes **confirmed applied**, **failed/partial**, and **unverified**, never “success” from mere rule selection. Do not publish `DropResolved` without a defensible host completion fence.

**B. Choose different drops depending on who broke it (NOT safe as a synchronous Lua callback):** Prefer a separate future immutable **declarative condition** evaluated by managed host code at an exact proven pre-drop decision fence. Human connected-player requirement is only an example after provenance is proven. Avoid promising permission/team/weapon/last-damager predicates until their source validity and pre-COMMIT query timing are independently qualified. Fill-time decisions usually cannot know a future breaker. If full `Replace` can be performed only during fill, per-break conditional drops are **not supported by that mechanism**. Do not introduce `function(player, entity) return drops end` as an interchangeable option.

## 3. Exact evidence and host-source ledger

### 3.1 Two historical host tuples must not be conflated

| Area | Exact recorded host | Evidence strength / limit |
|---|---|---|
| Prior Player-1C / Inventory-M2 / 1F-B item mutation | Rust build **25353106**, Carbon **2.0.259**, original Win `Assembly-CSharp` SHA-256 `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a`, Linux `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b` | **Q older host**: item creation/move/callback split/drop, returned-item cleanup and physical counts. G1 M2 broad conclusion superseded; supported-host Give G1 separately requalified. **Not proof for 25653776.** |
| Gameplay A/B1/B3 currently recorded | Rust build **25653776**, protocol **2634.289.1**; Win Carbon **2.0.262.0 / 8a81d70**; Linux Carbon **2.0.261.0 / c74c4ca**; Harmony **2.4.2.0**. Win `Assembly-CSharp` SHA-256 `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`; Linux `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`. | **Q current host** for *specified Player lifecycle, full Spawn, world removal/pool and native invalidation*. **No new C/D mutation/drop IL proof follows.** |

**B3 proven IL**, relevant only for entity lifetime: `BaseNetworkable.Kill` token `0x06005177` (OnKilled before DoEntityDestroy/Terminate/EntityDestroy); `TerminateOnServer` `0x0600517a` (registry unregister → network authority release → deactivation → normal return); `EntityDestroy` `0x060051b4`; `GameManager.Retire` `0x06006458`; `PrefabPool.Push(Poolable)` `0x06008344` immediately after actual stack push. Three-source B3 observation plus bounded weak native-invalidity checks prove actual original-incarnation removal, **not damage culprit, inventory mutation finality, or that any loot dropped**. Complete underlying host IL receipts live in external qualification workspaces listed in B3, not in Git. [B3 evidence](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/GameplayB3-Validation.md#host-proof-and-preserved-counterexamples).

**Gameplay A death IL** provides useful **non-equivalence**: `BasePlayer.Die` `0x06001aea` and later base-death progress prove *Player death* only; `BaseCombatEntity.Die` `0x0600149b` health/life change precedes a hook, while the full OnDied tail can continue. Hook data may describe a direct human initiator but does not independently prove who physically broke a particular loot container or when the payout committed. [Gameplay A evidence](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/GameplayFoundationA-Validation.md#exact-host-a0-proof).

### 3.2 C item paths — evidence versus needed proof

| Candidate | Current evidence | Why it is insufficient for `ItemAdded`/`ItemRemoved` | Needed next |
|---|---|---|---|
| `ItemContainer.Insert` / `OnItemAddedToContainer` | **Q older**, **P** public hook. Physical `itemList`/parent changes can precede later callbacks. | Insertion may be undone, rejected later or combined; does not capture in-place amount modifications or all item writers. | Current-host IL timing, post-tail reciprocal membership, callbacks/nesting, alternative routes. |
| `ItemContainer.Remove` / `OnItemRemovedFromContainer` | **Q older**, **P** public hook. | Removal from one container is not consumption; move within Player produces both removal/add. Hooks can run before all bookkeeping and ownership settles. | Source/destination association, late completion, actual consumed amount. |
| `Item.MoveToContainer` / `OnItemStacked` | **Q older**, **P** generated hook shows destination stack update, source amount subtraction, **hook before `MigrateItemOwnership`** and potential source removal. | Intermediate source/dest ownership and split/merge, conditional drop/cleanup; neither hook receipt nor success boolean is universal final physical Player delta. | Complete normal and exceptional method return, nested transfer and owner migration on current host. |
| `Item.UseItem` / `OnItemUse` | **P** generated source: hook may override `amountToConsume` **before** actual `item.amount -= amountToConsume`. | A pre-use hook is not completed consumption; amounts may change without container unlink. | Post-operation capture and alternate consume/ammo/build/craft paths. |
| `PlayerInventory.Take` | **R** existing adapter `Take(null,itemid,amount)`, **Q older** exact return+physical VERIFY. | Qualified for **CarbonLuau's own call**, not all external writers or global producer sequencing. | Current-host writer inventory and any credible terminal fence. |
| `Player:GiveItem` | **R** exact-slot creation/move/returned-resource tracking and physical Q1; **Q older** with I12 limits. | No universal transfer journal; additional plugin mutations can affect snapshots; item creation can fail before returning. | Use as a dedicated controlled-call fixture only; no global event guarantee. |
| Direct `Item.amount` changes, split/craft/recycler/stack decay, wear/belt shortcuts, loot-to-player | **P/I** possible pathways. | If any amount writer bypasses dirty hooks or the proposed patched paths, quantity changes become undetectable until sampling; exactly-once impossible. | Enumerate setter/direct-field writes and actual callgraph, then measure gap coverage. |

**Public documentation links (P only):** [OnItemStacked](https://docs.oxidemod.com/hooks/item/OnItemStacked), [OnItemUse](https://docs.oxidemod.com/hooks/item/OnItemUse), [OnItemAddedToContainer](https://docs.oxidemod.com/hooks/item/OnItemAddedToContainer), [OnItemRemovedFromContainer](https://docs.oxidemod.com/hooks/item/OnItemRemovedFromContainer). Do not mistake an autogenerated public excerpt for current 25653776 IL.

### 3.3 D loot/drop paths — evidence versus needed proof

| Candidate source | Verified scope at this time | Danger / unproved part |
|---|---|---|
| `LootContainer.SpawnLoot` + `OnLootSpawn` | **P:** generated code clears `base.inventory`, calls `ItemManager.DoRemoves`, then invokes cancellable `OnLootSpawn` **before `PopulateLoot`**. | Existing contents may already have been removed; `OnLootSpawn` suppression does **not** prove bonus scraps/physical destruction drops are suppressed. Refresh, save-loaded incumbents and plugin fill interference unproven. |
| `LootFill.DelayFill` + `OnLootSpawn` | **P:** separate method invoking `LootContainer.FillLoot` if hook returns null. | Separate class/contract; not safe to assume barrel uses this. |
| `DropUtil.DropItems(ItemContainer, Vector3)` + `OnContainerDropItems` | **P:** hook tested **before** copying `container.itemList` and dropping via generic utility; non-null prevents default `DropItems` body. | Caller identity/class, which barrel families call it, item cleanup if canceled, concurrent other payout paths and patch order are **U**. Hook signature exposes container only, not a proven breaker. |
| `LootContainer.DropBonusItems(BaseEntity initiator, ItemContainer container)` + `OnBonusItemDrop` | **P:** item **already created** before pre-drop hook; successful path `Item.Drop(...)` then `OnBonusItemDropped`. | Pre-drop veto of a **single bonus** is not a complete loot replacement, and a canceled freshly created item needs qualified ownership/cleanup. Post-hook cannot stop default action. |
| `OnEntityDeath` / `BaseCombatEntity.Die` | **P**, **Q** Player-specific lifecycle and B3 generic removal proof. | Event means neither all loot is suppressed nor payout completed. Depending on class, may be before OnDied/drop work; attacker may be non-human or ambiguous. |
| `OnEntityKill` | **P**, **Q** B3 negative: vetoable before actual world removal. | Neither physical deletion nor complete default payout. |
| B3 `Workspace.EntityDestroyed` | **R/Q** original-incarnation completed actual removal snapshot. | **Too late** for default suppression; no Breaker and no guarantee item world-spawn results. |

**Generated source citations (P only):** [OnLootSpawn](https://docs.oxidemod.com/hooks/resource/OnLootSpawn), [OnContainerDropItems](https://docs.oxidemod.com/hooks/entity/OnContainerDropItems), [OnBonusItemDrop](https://docs.oxidemod.com/hooks/item/OnBonusItemDrop), [OnBonusItemDropped](https://docs.oxidemod.com/hooks/item/OnBonusItemDropped), [OnEntityDeath](https://docs.oxidemod.com/hooks/entity/OnEntityDeath), [OnEntityKill](https://docs.oxidemod.com/hooks/entity/OnEntityKill).

**Historical prefab candidates** from [OrangeWulf/Rust-Docs](https://github.com/OrangeWulf/Rust-Docs/blob/master/Entities.md) and [a public 2025 prefab/loot catalog](https://gist.github.com/c0deR3D/802c048fa6c48efc147d384077b4e715):

```text
assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab
assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab
assets/bundled/prefabs/radtown/loot_barrel_1.prefab
assets/bundled/prefabs/radtown/loot_barrel_2.prefab
assets/bundled/prefabs/radtown/oil_barrel.prefab
```

Older catalogs suggest several of these are `LootContainer` subclasses/instances, but no list above is **current-server, current-class or current-drop-path qualified**. Facepunch's [oil-barrel page](https://wiki.facepunch.com/rust/oil-barrel) lists crude oil/low-grade fuel contents; that describes game content, **not the managed drop call graph**. Validate exact prefab identity, component class and live payout for each tested variant. Do not assume "barrel" is a single host class or drop sequence.

## 4. Gameplay C: candidate architectures and disposition

### C-A. Read-only facade wrapping D18 — **ACCEPT FIRST**

- Bootstrap `Player:GetInventory` produces frozen/bound `PlayerInventory` with `Record(Player)`'s exact Token/UserId/domain context; optionally cache by existing Player proxy in a weak-key/weak-value table to avoid duplicate persistent hosts. No raw host object and no eager inventory scan. `__index` is a fixed whitelist.
- Host count/has call the *same* `FacadeSession.Operation(26/27)` or shared `PhysicalInventoryObservation` adapter; avoid duplicating shortname validation and semantics. Preserve `CountItem`'s unknown-definition zero and `HasItem` amount rules, checked exact-integer accumulation and stale errors.
- Resource-owner domain retirement always invalidates host reads; do not build a UserId-based cache that magically reconnects.
- Minimal tests: equality within lifetime, same UserId reconnect stale old, root/addon export semantics, read during provisional module (permitted), initialization/teardown, malformed/overbound physical stacks, historical Player methods unchanged.

### C-B. End-of-operation verified net-delta journal — **PREFERRED ONLY IF SOURCE-PROVABLE**

**Core observation invariant:** A change is reportable only after two accepted, bounded physical snapshots of the same exact D11 Player inventory at **qualified terminal points**. Snapshots map canonical `ItemDefinition` shortnames to checked net totals summed over main/belt/wear; compare after full collection. No item UID, `Item` reference, container object or nested container crosses into VM/retained events.

Private algorithm (design, not proved):

```text
on FIRST COMMITTED DEMAND(player token):
    validate exact Player; capture bounded baseline; publish no history
on qualified producer/terminal signal:
    check source owner-thread + expected original player token
    mark watched token dirty (bounded), do not invoke Luau
at qualified complete-operation/owner-thread observation fence:
    revalidate Player; take at most one accepted full physical snapshot
    if either snapshot/bound invalid: mark NEEDS_BASELINE; emit nothing
    else for each union shortname (bounded, stable ordering):
        delta = after[item] - before[item]  -- checked
        if delta != 0: capture immutable snapshot and use D23 queue/fanout
    baseline = after even when callback queue drops, with counted loss
at disconnect/unpublish/teardown:
    release original token watch, pending work, charges; no replay
```

**Crucial distinctions:**

- The existing D18 scanner takes an **item definition and returns one count**; it is not already an all-item snapshot enumerator. Add a **private** reusable bounded full-inventory snapshot primitive using exactly the existing 128-stack physical validity/reciprocity rules. This is a small addition, not a second inventory subsystem.
- A "dirty" notification is a *trigger to reconcile*, never proof of the direction or amount. No event is constructed in the hook itself.
- A full snapshot is physically validated but is not necessarily a **global transactionally isolated snapshot** under I12. Pin owner-thread storage access and define what concurrent trusted writers can invalidate.
- If `+5` and `-3` are both completed between snapshots, emit `Added.Amount=2`, not two operations. If `+5` then `-5`, emit nothing. This is explicitly *sampled net delta*, not a ledger of individual actions.
- If an internal transfer's remove and add straddle different accepted fences, a naive implementation can produce false events. **No public release** until a proved boundary makes that impossible for the supported operation scope, or until the public contract is deliberately narrowed to *observed change between sampling points*.
- For a third-party container/stack plugin that mutates during an intermediate callback, results are I12-scoped. The algorithm still must not claim an event if a known ordinary, uncompleted host mutation was observed mid-flight.
- If an event is dropped for backpressure, **do not rewind the baseline or replay**; otherwise later scans double-count deltas. Saturating rejected/dropped diagnostics disclose lost observational notifications.
- On a new subscriber while an old watch has uncommitted dirty history, conservatively reset the active watch baseline or use a listener publication cut-off/witness to prevent delivering pre-subscription state changes. Losing an ambiguous interval is better than manufacturing retrospective activity. Candidate listeners staged by D7 get **no baseline/event** until successful commit.

**Gate:** enumerate and pin every producer that can change quantity; prove terminal observation after normal host mutators, nested plugin callbacks and host updates. If this requires a large set of fragile patches, prefer C-C or defer public Signals. An end-of-*Unity frame* pump alone does not prove host operation finality for mutations that span frames.

### C-C. Bounded periodic reconciliation — **SIMPLE, HONEST BUT WEAKER**

Without full hook coverage, a round-robin sampling of **opt-in subscribed, exact** Players detects net changes between snapshots, not individual committed operations. It tolerates missing dirty hooks but cannot guarantee it observes a short-lived gain, loss, or an entire multi-turn transfer. Its correctness claim is "difference between two sampled physical states" only. Avoid labeling it a universal `ItemAdded`/`ItemRemoved` history; if valuable, expose explicitly **observational `ItemChanged`** semantics and state the sampling blind spots. Admission hard-bounds watched players, visits per frame and total retained map bytes. Do not scan all `1024 * 128` possible entries every frame or add a separate event scheduler. This may be a reasonable **optional product compromise** after measurement, not the default semantic promise.

### C-D. Patch terminal Rust inventory mutators — **INVESTIGATE, HIGH FRAGILITY**

Potential Harmony complete-method postfix/finalizer on exact `Item.MoveToContainer`, `Item.UseItem`, `Item.Take`, `Item.Remove`, `ItemContainer.Insert/Remove`, split/merge/craft/recycle and direct amount writers, with original-run/exception/nesting tracking analogous to qualified Spawn/Die patches. It may supply strong boundaries for a *restricted set* of mutations; it cannot be declared universal just because it sees `MoveToContainer`. Prove all direct writes, exception paths, alternative callers, same-item nesting and thread context. Each patch must have exact method token/signature/IL/EH/hash, Carbon transformation owner and patch topology, producer bounds, and fail-closed drift. If the proof set grows out of proportion to the minimal two Signals, **reject/defer** rather than erecting a generic inventory transaction framework.

### C-E. Only observe CarbonLuau Give/Take — **SAFE LIMITED ALTERNATIVE, NOT A DROP-IN**

Existing operations already calculate before/after specific item quantity and complete within one D13 synchronous call. A **verified-operation** Signal scoped *only* to CarbonLuau-originated successful `GiveItem`/`TakeItem` could be added with a clear separate name. It is **not** a general `inventory.ItemAdded`/`Removed` (misses vanilla pickup, drop, consumption and other plugins); do not silently substitute it. Positive `GiveItem` success is `Q1 >= Q0+Amount`, which intentionally allows extra trusted/plugin effects, so an event describing the **exact amount caused by this operation** is not generally provable; describing only measured net delta is more defensible. Indeterminate COMMIT can still change inventory and may need a baseline invalidation.

### C decision table

| Feature | Decision |
|---|---|
| Player `GetInventory` and query facade | **Accepted, C1 implementation-ready**, subject to standard regressions. |
| Global `Inventory` service | **Rejected** for C; ownership and ergonomics favor Player facade. |
| Existing `Player:HasItem/CountItem/GiveItem/TakeItem` | **Preserve exactly**; no behavioral migration. |
| Common private physical net-delta snapshot engine | **Accepted for bounded C2 research**, not public availability. |
| Public `ItemAdded` / `ItemRemoved` as verified net observations | **Conditional** on complete boundaries and honest time-window semantics. |
| `ItemChanged` signed common context | **Optional**, possibly preferable for sampled-only fallback; cannot fix source proof. |
| Event per raw `ItemContainer` callback / guaranteed per action | **Rejected** as false semantics. |
| Every Item, world, nested backpack, NPC, offline/retired Player | **Deferred/rejected** for initial C. |
| Guaranteed no drop, exactly once, perfect event ordering, actor attribution | **Rejected**. |

## 5. Gameplay D: candidate mechanisms and disposition

### D-A. Replace at loot-fill decision (`LootContainer.SpawnLoot`) — **BEST NARROW FIRST CANDIDATE**

**P source order:** `Clear()` -> `ItemManager.DoRemoves()` -> cancelable `OnLootSpawn` -> `PopulateLoot()` if unhandled. This is **already after removal of previous inventory contents**; the hook cannot retroactively make that clear atomic. It could provide an exact host decision for **future fills** of allowlisted LootContainer prefabs if current pinned IL, Carbon hook order and callbacks confirm suppression. `LootFill.DelayFill` is a different source/class and must be separately qualified or excluded.

Implementation candidate: at a fully proved pre-default-populate fence for an allowed prefab, choose one committed immutable rule, roll bounded amounts, use qualified inventory/container placement/cleanup for the *barrel's own* loot inventory and prove final expected contents. The existing Player grant adapter must **not** be repurposed: barrel `ItemContainer` is not a Player main/belt/wear destination and different capacity/ownership/callbacks apply. Account for preexisting cleared items and returned item resources from the first host side effect. A callback override alone does not prove successful all-item creation.

**Semantics if this is all that qualifies:** only `DropMode.ReplaceContents`/`ReplaceLootFill`, applying to eligible fills **after rule publication**. Already-filled barrels, save-loaded barrels, refresh/fill order and separate destruction bonuses remain outside scope unless proved. No silent promotion to strict `Replace`.

### D-B. Replace at destruction's last complete payout decision — **IDEAL UX; HIGH HOST-PROOF GATE**

A strict `Replace` needs a point before normal physical world drops at which **all** default paths for the exact eligible class can be suppressed. Inspect current-host `LootContainer.OnKilled`/`OnDied`, `BaseCombatEntity.Die` → OnDied tail, `DropBonusItems`, `DropUtil.DropItems`, any loot bag/individual item drop, `StorageContainer` death/drop routines, and same-path Carbon/Oxide hook transformations. Build one callgraph **per prefab/class family** and enumerate early returns, bypassed normal Kill, duplicate calls, despawn/admin removal, modifiers, parent world-entity effects and external plugin vetoes.

**Potential safe architecture:** one prevalidated managed decision coordinator invoked once per proven original D20 entity incarnation at a host method/patch whose normal and exceptional exits are pinned. Select one committed descriptor and reserve bounded resources **before** irreversible mutation. Suppress every qualified default branch exactly once; perform bounded creation and target world-drop calls with verified physical ownership/position and cleanup as applicable. If any default path can still fire or any paid-out item can be silently abandoned under supported vanilla failure, the full `Replace` proof fails. Prefixing `OnEntityKill` is not enough: Kill can be vetoed and world removal may not occur; B3's successful removal observation is too late to select a suppressive policy.

Even with qualified suppression, **world spawning is nontransactional**. A rule may cause a partial physical payout before a later error. A host-invoked policy has no synchronous Lua call to throw to; record an indeterminate/partial operational diagnostic and do not report "replacement succeeded" or retry that occurrence. Fail-closed registration/availability on host drift; preserve unrelated world gameplay and other services.

### D-C. Override `DropUtil.DropItems` plus bonus-specific prehooks — **MULTI-SEAM, CONDITIONALLY VIABLE**

Public `OnContainerDropItems` is before generic `DropUtil.DropItems` enumeration, but it only observes a **container**. A bonus scrap path separately creates an Item then calls `OnBonusItemDrop` before dropping; `OnBonusItemDropped` is after. Intercepting both may cover some barrel variants, but still may miss direct `Item.Drop`, crate/gib/loot definitions, custom plugin paths or existing dropped world Items. Need prove which calls belong to **one same original entity instance**, how the parent container maps to the original D20 lifetime and how to suppress every branch without picking up the wrong container. Without a qualified shared decision identity and callback-safe cleanup, this approach is too fragile to advertise as single `Replace`.

### D-D. Use gameplay death/destruction Signals and `Player:GiveItem` — **REJECT AS DROP REPLACEMENT**

`Workspace.EntityDestroyed` means original entity world retirement. Its deferred callback cannot stop vanilla drops, has no Breaker, and `GiveItem` is inventory-only. A post-break **separate bonus reward** can be a different future feature if intentionally scoped, but cannot be disguised as suppressing/replacing barrel physical loot. `Players.PlayerDied` has no relationship to an NPC/loot barrel's complete payout.

### D-E. Mutate global Rust loot tables/definitions — **REJECT FOR INITIAL D**

Direct edits can leak across other plugins, server restarts and unrelated prefabs, break domain publication/rollback and expose mutable host-owned assets; they're not an immutable domain-owned per-prefab policy. Do not rely on arbitrary reflection or patches from Luau. A fully host-owned and isolated override model could be reconsidered after a separate qualification, but is larger than the desired API.

### D-F. Arbitrary dynamic Luau callback when hit/broken — **REJECT SYNCHRONOUSLY**

Example **not supported**:

```luau
Loot:RegisterDropRule(prefab, function(player, entity, hitInfo)
    return ...  -- would require unsafe host-driven synchronous Luau execution
end)
```

A Lua callback in the Rust drop path could recurse into the same VM while `GiveItem`, an addon or GUI callback is already running. It also has a deadline that cannot preempt long host calls, mutable side effects prior to item decision, nontransactional exception/timeout and unsafe `HitInfo` exposure. Its return value cannot be deferred without missing the decision point. **Do not emulate this with an async callback and pretend it is synchronous.** If author-specific choices are important, declare a limited set of **host-evaluable predicates** after exact-breaker proof, or a declarative table keyed by known conditions. An after-break Signal can coexist but is not a policy decision.

### D decision table

| Feature | Decision |
|---|---|
| Separate `game:GetService("Loot")` | **Accepted architecture**; no current public implementation. |
| `Loot:RegisterDropRule(Prefab, Loot.DropMode.Replace, Drops)` | **Preferred user-facing strict goal**, contingent on an exact complete-payout gate. |
| `Loot.DropMode` service-local typed enum | **Accepted design**; no global `Enum`. |
| Immutable prevalidated declarations and deterministic one-owner conflict rule | **Accepted D1 private model**. |
| Narrow `ReplaceContents`/`ReplaceLootFill` | **Conditional fallback** with distinct semantics if full `Replace` fails. |
| `Add`/bonus/additive random loot | **Deferred**; additive world creation still needs independent source/ownership proof. |
| `Loot.DropResolved` after-event + exact human Breaker | **Deferred** until actor/path/completion proved; best-effort only. |
| Per-break declarative human predicate | **Deferred** and **requires destruction-time decision**, not fill-only. |
| Synchronous dynamic Luau item/loot callback, global mutation, generic hook bus | **Rejected**. |

## 6. Internal integration: minimal changes, maximum reuse

### 6.1 Files/modules to extend only after phases are authorized

| Existing owner | Proposed smallest additive work |
|---|---|
| `scripts/bootstrap.luau` | C: implement `PlayerMethods.GetInventory`, sealed local `PlayerInventory` methods and later targeted `Signal`; D: `Loot` service and `Loot.DropMode` immutable singleton, dense-array value validation before host call. Do not add raw host objects or global `Enum`. |
| `src/CarbonLuau/Facade/FacadeSession.cs` | C: reuse ops 26/27, stage typed **per-Player-targeted** listener metadata instead of treating it as a global kind; preserve existing ops 6/7, D7 listener checkpoints, dispatch admission and limits. D: add a private declaration operation that stages compiled policy records and participates in begin/commit/rollback. |
| `src/CarbonLuau/Facade/InventoryObservation.cs` | C2 only: bounded full-inventory per-shortname count snapshot sharing validated `PhysicalInventorySource` / validity rules. No second scanner semantics. |
| `src/CarbonLuau/Facade/GameplayEventBudget.cs`, `GameplayEventFacade.cs` | Extend the existing kind discriminator, global capture/fanout/visit quotas, 2-KiB packed payload, reservation accounting, publication witness and cancellation. A high-volume inventory source **competes for**, does not multiply, quotas with A/B1/B3. |
| `native/src/facade/FacadeBridge.cpp`, `native/src/runtime/Publication.cpp`, `native/src/Runtime.cpp` | Recognize only new typed private event kind(s), validate fixed field counts/payloads, and include authorized *new resource-declaration* op in existing native publication scopes. Keep one callback per scheduler admission and no recursive Luau. Audit `HostPrimitive` supported op-code range before assigning new numbers; it currently permits 1..35 with reserved exceptions. |
| `src/CarbonLuau/CarbonLuau.Carbon.cs` and a narrow new `CarbonLuau.Loot.Carbon.cs` | C: exact Player inventory read adapter + **qualified** dirty/completion input; D: pinned prefab/class/adaptation, rule lookup and host mutation seam; no unrestricted reflection/hooks from Luau. |
| `src/CarbonLuau/Facade/PlayerDirectory.cs` | Reuse exact D11 `PlayerLifetime` and `Resolve`. No UserId-only retarget, new identity type or global player registry. |
| `src/CarbonLuau/Facade/InventoryMutation.cs`, `InventoryGrant.cs`, `CarbonLuau.Inventory.Carbon.cs` | **Preserve existing Give/Take** code and mutation gate. Use their PREPARE/COMMIT/VERIFY design as a policy taxonomy, not a drop-creation shortcut. |
| `src/CarbonLuau.Core/Api/ApiCatalog.cs`, API JSON, generated `.d.luau`, docs/preview/tooling/examples | Only after qualified public surface: add explicit source-owned metadata, `SignalWith` typed contexts, enum/service overload and `SinceApi`; regenerate and run tooling/ABI/package consistency checks. |

### 6.2 Listener identity and publication details

A per-Player inventory Signal must carry **kind + exact original Player token + defining domain/publication witness** in its managed registration. `Signal:Connect` returns the existing `Connection`, rooted in the existing callback-root store. Do not encode the target in a mutable Lua table, hook name or Player UserId. Either extend `SortedDictionary<ulong,string>` to a typed narrow registration record without altering old semantics, or keep a separately indexed **bounded adjunct keyed by existing listener ID**; the latter minimizes regression risk. Every listener still consumes one per-signal and per-domain registration slot. If a targeted source becomes unavailable or detached, drop/cancel rather than resolving the account again.

An inventory facade containing Signals may outlive its Player connection as a Luau value; connecting/reading after disconnect fails closed. If an already-queued observation is processed after disconnect, **drop** it (same policy as D23 victim admission). If the user wants historical disconnected final quantity, that would require a different immutable snapshot API, not resuscitated `PlayerInventory` authority.

D1 rules are committed **only at D7 safe-point publication**, not when a module executes. Registration should be **initialization/staging-only**, mirroring `Commands:Register`, so active gameplay cannot unexpectedly mutate global rules in the middle of a host call. A failed cold module or candidate rolls back its staged descriptor. A root/addon replacement can replace its own *outgoing* rule atomically at commit, while a different domain's occupied exact prefab is a deterministic conflict; never last-writer-wins. One active full-`Replace` owner per `(qualified prefab, mode/trigger family)`. **Preflight all staged rules and collisions against the outgoing-owner exception *before* modifying any committed index or finishing publication**; the new immutable rule index must swap at one owner-thread safe point. Do not assume existing per-session GUI/listener checkpoints automatically make a new cross-domain loot registry atomic. If two candidates introduce the same prefab within one publication graph, conflict fails that publication, preserving the old one. Cross-addon shared module calls obey the original D10 operation context; a foreign committed service cannot launder provisional registration.

At a qualified host decision, hold an **immutable rule snapshot selected once**, with explicit original entity incarnation and publication/owner capture sufficient to complete the already-started decision even if domain publication changes during trusted synchronous host callbacks. Domain teardown unregisters future selection, not effects already begun. No asynchronous policy callback, implicit priority, auto replay after recovery or indefinite reference to an entity. If a rule would be invalid due to changed host capability or qualified source drift, fail closed **for that rule/source** rather than corrupting B3 or broadly disabling other services.

## 7. Lifetimes, bounds, timing and ordering

### 7.1 C work/resource contract

**Existing hard limits** (R): 128 inspected physical stacks for D18, `FacadePolicy.Players=1024`, 128 listeners per named signal/domain, 256 total listeners/domain, 256 managed pending/session, `GameplayEventPolicy` global 128 captures/frame, 128 deliveries/frame, 32 deliveries/domain/frame, 4096 visits/frame, 512 retained reservations, 2048-byte gameplay payload and its global retained transport budget. These are shared with existing B3; do not claim inventory automatically fits them.

**Proposed *measurement targets*, not accepted constants:** max 64 globally watched Player lifetimes; max 8–16 128-entry scans per frame **subject to remaining shared 4096 gameplay visits**; one dirty marker per watched lifetime; max 128 distinct item shortnames per snapshot; full snapshot charge budget derived from 64 × at most 128 × (128-byte shortname + amount + managed overhead), hard-capped by measured retained bytes. Deduplicate multiple subscriptions to one original Player watch only after proving new-subscriber publication cutoff logic. Cap work *before* host/native data reads and before fanout. Allocate/debit the watch and scalar map at admission, refund precisely at retirement. If a requested subscription exceeds a hard cap, fail its `Connect` with controlled error; an event burst after subscription drops, counts diagnostics and resynchronizes without historical replay.

C capture order is owner-thread **accepted snapshot order**, not universal game transaction order. Within a watch, one comparison's item keys can be sorted ordinally for deterministic best-effort enqueue order; each listener sees a subsequence of that order and may miss events under cap/stale state. There is no player-to-player atomic transfer notification; a Player leaving before second scan may produce no removal event. A native reservation persists through queue transfer to callback completion, and a canceled subscription does not prematurely refund it.

### 7.2 D rule/resource contract

**Targets to qualify** rather than arbitrary promises: initially one supported prefab family and `Replace` mode only if full suppression exists; max 64–128 global active rules, max 16 drop entries per rule, configured max total physical quantity and **max item creation/drop calls per occurrence** tied to qualified stack limits. Exact numeric values must derive from host capacity, item stackability and measured per-call latency. A max total amount alone does not bound number of spawned world entities when splits occur. Reject inputs that cannot be bounded **before any irreversible host effect**; use a fixed admission state/short-lived temporary resource ledger with a known ceiling and no blocking locks. Rule metadata charges must include copied prefab/shortnames, descriptor allocations, per-domain staging and globally retained data.

Policy selection is synchronous on the owner thread, with **no VM call**. Ordinary nonmatching prefab/source falls through to vanilla without host effects from CarbonLuau. Explicit after-COMMIT failure becomes an internal indeterminate diagnostic; no outward boolean completion is invented for a registered declaration. A canceled/retired owner cannot be selected for a *new* decision, but an already-started nontransactional host payout is never rolled back by domain change. Separate rolling/fanout counters distinguish attempts, no-rule, rejected prefab, selected policy, default suppression, created/accepted/dropped/cleaned, partial, uncertain, trusted interference and supported-host faults.

### 7.3 Failure taxonomy (applies to both, with different exposure)

| Situation | C action | D action |
|---|---|---|
| Malformed declaration / stale Player / provisional mutation | Controlled error, no host mutation | Controlled declaration error, no active rule. |
| Source hash/IL/topology drift | C facade reads may remain available; **only affected event source unavailable** | Fail closed for affected policy mode/prefab; no unsupported suppression or host item creation. |
| Wrong-thread hook | Reject safely before host/VM access; account later | Refuse policy source; never call Unity/VM off thread. |
| Partial/intermediate host mutation | Do not emit raw hook or partial scan as final delta | Mark indeterminate/partial; no rollback claim or automatic re-drop. |
| Overbudget or missing snapshot | Drop event, mark need for new baseline; do not invent offsetting deltas | No new policy work admitted; if COMMIT already started, classified partial/unknown and cleanup qualified owned resources. |
| Trusted plugin modifies inventory, stack limit, drop/owner state | I12; verify captured physical fact, do not invent causal operation attribution | I12; still cannot claim all custom drops/world ownership or suppress verification. |
| Disconnect/reconnect or same ID reused | Exact lifetime watch retired; queued callback dropped; new connection receives new initial baseline | Captured breaker facade cannot retarget, human condition false/unknown absent capture-time proof. |
| Candidate failed, normal replacement, provider unload | D7 rollback and exact cleanup; no historical replay | Old committed rule retained on candidate failure; atomic replacement for same owner; no partial rule publication. |
| VM-fatal timeout or plugin unload | Cancel and retire watchers/queues, no replay | Remove future policy registrations; already performed Rust effects remain, no replay; D20 host observer restart gate preserved. |

## 8. Remaining static research work: exact questions and stop conditions

### 8.1 C source/IL audit order

1. On **both** recorded 25653776 Windows/Linux assemblies and loaded Carbon-publicized transform, enumerate `Item.amount` field accesses and any property wrappers: assignment sites in split, stack, move, consume, craft, recycle, ammo load/unload, pickup, drop, spawn and plugin interfaces. Produce full caller graph, not a handful of candidate hooks.
2. Enumerate the actual signatures/tokens/body/EH, callback injection offsets and execution order of `ItemContainer.Insert/Remove`, `Item.MoveToContainer`, `Item.Take`, `Item.UseItem`, `Item.Remove`, `PlayerInventory.Take`, and qualified owner reassignment. Record pre/after mutation, nested call sites, failure returns and exception paths.
3. Prove which source container maps **reciprocally** to `BasePlayer.inventory.containerMain/Belt/Wear` and whether any stock pathway directly replaces those container references. Preserve `IsValid`, parent, position, `GetSlot` and list membership rules from D18.
4. Evaluate three fences independently: (i) complete supported Rust inventory method postfix; (ii) owner-thread end-of-Rust-frame reconciliation; (iii) explicit low-frequency sampling. Demonstrate on a split/merge/transfer that no proposed *committed semantic* capture occurs between related physical changes. If not possible, **do not publish universal net signals**.
5. Check whether Carbon hook subscription zero→one and one→zero changes the 24 D20 Spawn and B3 Kill/Terminate/Pool patch topology. If patch interaction cannot be qualified, use minimal narrowly scoped adapter or leave C events unavailable; B3 remains authoritative.

**C stop condition:** An ordinary supported vanilla mutation can change a watched quantity between supposed terminal observations and later undo it, generating a false semantic delta; an important amount writer escapes both hooks and bounded sampling contract; or safe bounded storage-lifetime reading cannot be proved. In that case keep C1 and report C2 deferred, not a fake PASS.

### 8.2 D source/IL audit order

1. From actual pinned server **prefab manifest/asset metadata**, map each requested canonical barrel path to actual type (`LootContainer`, subclass, StorageContainer), attached components, `inventory` layout, `lootDefinition`, refresh settings, capacity and available default payout paths. Enumerate actual assets; historical prefab names are discovery inputs only.
2. Disassemble, in both OS assemblies and Carbon-loaded transformed state, complete `LootContainer.SpawnLoot`, `PopulateLoot`, `DropBonusItems`, `OnDied`/`OnKilled`, `LootFill.DelayFill`, `LootContainer.FillLoot`, `DropUtil.DropItems`, `StorageContainer` destruction, `Item.Drop`, relevant `BaseCombatEntity.Die`, `BaseNetworkable.Kill`, and any special barrel code. Record exact token/signature, method hash, IL offsets, EH, virtual dispatch and Carbon patch owner/order. Include **all** default drop entry points and direct calls, not merely named hooks.
3. Construct **per-prefab** sequence traces: normal spawn→fill; reload existing item-filled instance; loot refresh; player hit→death/wounded/kill; bonus-scrap calculation; generic container dump; DropItems/individual world entity creation; eventual B3 removal. Locate the **earliest universal default-payout decision** and the **latest safe suppression boundary**. An OnLootSpawn skip is valid only for future filling; `OnBonusItemDrop` is after creation; `OnContainerDropItems` is only generic container handling.
4. Prove transferred item ownership and verification: how `ItemManager.Create`, temporary items, `ItemContainer.Insert/MoveToContainer`, `Item.Drop`, resulting `DroppedItem` world entity, partial stacking/splits, cancelable `OnItemRemove` and cleanup behave on these exact paths. What does success mean if an Item is created but never materializes as a world entity? No inference from `GiveItem.InventoryOnly`.
5. Prove actor availability at the **same** pre-drop fence. `LootContainer.DropBonusItems` public source includes `BaseEntity initiator` and a `BasePlayer` for scrap modifiers, but that does **not** automatically prove actual final breaker identity, or availability for the separate stored-content drop. Verify exact D11 token for eligible connected human only, and document NPC, offline, traps, explosives, projectiles, decay, self and plugin-driven destruction as nil/unsupported when ambiguous.
6. Verify compatibility with *other trusted plugins* returning non-null from `OnLootSpawn`, `OnBonusItemDrop` or `OnContainerDropItems` and with ordinary access/stack/tea/luck callbacks. Characterize actual mutation and override order; no blanket monopoly claim.

**D stop condition:** One ordinary vanilla path creates/surfaces loot outside the candidate complete suppression model; a bonus item can be spawned after rule replacement without safe veto/cleanup; world Item creation/drop cannot be bounded or verified; or no registered hook can select one immutable rule before default mutation. Then **defer strict `Replace`**, optionally propose distinct `ReplaceContents` as the qualified smaller API.

### 8.3 Proof-record format for each promoted source

Keep a structured 12-point record in a dedicated **future** qualification document: (1) OS, Rust/Carbon tuple, binary SHA/MVID and loaded body; (2) method/signature/token/IL/hook owner; (3) ordering relative to every relevant mutation and default effect; (4) normal/skip/veto/throw/nesting semantics; (5) actual postcondition and what is *not* proved; (6) exact Player/Entity lifetime and original identity; (7) scalar capture and no-raw-object copying; (8) owner-thread/reentrancy; (9) hard producer and retained-work bounds; (10) publication/rollback interaction; (11) patch brittleness and expected upgrade behavior; (12) Windows/Linux live positive+negative receipts. Preserve deliberate counterexamples as durable regressions. No mock-only or hook-name-only PASS.

## 9. Qualification test matrix

### 9.1 Gameplay C — controlled host and runtime

| Test group | Cases and required observations |
|---|---|
| Query facade | No inventory scan at GetInventory; normal empty/full/split stack Count/Has, invalid entry, over-128 entries and checked large totals; old Player methods return identical results and failures. |
| Original identity | Existing connection vs disconnect/reconnect same UserId; retained old facade and queued Signals cannot retarget; old Name/UserId snapshot remains only on original Player. |
| Net movement | main↔belt↔wear, stack split/merge, equipment, full/partial same-Player moves: **no spurious net deltas**. |
| Genuine net change | pickup external world Item, crafted receipt, drop to world, consume/use stack in place, remove entire stack, receive/send cross-Player; each accepted comparator produces correct sign/amount when proof applies. |
| Nontransactionality | Null/throw after insert, Remove callbacks, nested item-mod lifecycle, stack limit change/split/drop, paired `Give/Take` recursive gate, host plugin changing amount while callback runs; ambiguous windows fail closed/rebaseline. |
| Missed producer | Change amount without Add/Remove callback; verify correct sampled-only behavior or prove matching terminal producer. Catch a +X/-X within one observation interval and ensure zero **net** event. |
| Subscription/publication | subscribe before/after dirty interval, nested module failure caught, failed candidate rollback, root/addon cross-domain shared module, 100 addons, listener disconnect while queued and late native completion; no historical catch-up or leaked callback roots. |
| Bounded intake | 64+/configured cap watches, max-per-frame scans, 128-stacks/watcher, 512 reservations, mixed B1/B3/A burst and 256 pending/session; fairness, accurate refund, allocation faults, saturating counters. |
| OS/host/live | On both **exact** current Windows/Linux Carbon hosts: at least one ordinary genuine move, split, stack, use, pickup, drop and trusted callback scenario with **physical before/after checks**; source hash/patch and wrong-thread fail closed. |

### 9.2 Gameplay D — controlled host and runtime

| Test group | Cases and required observations |
|---|---|
| Asset/class | Five historical candidate paths checked against actual current prefab manifest; each supported family independently tested; nonexistent/non-loot prefab rejected with zero host changes. |
| Baseline vanilla | Capture actual fill + destruction sequence, number/type/amount of stored contents, generic item dumps, bonus scrap/tea modifiers; world entity counts and host ownership, not hook logs alone. |
| Strict Replace | Freshly spawned supported barrel, exactly one selected rule, min and max endpoints, empty zero-rolls, no extra vanilla content **or bonus**; verify physical world items plus absence of default, not merely a `OnLootSpawn` return. |
| Fill-only candidate | Register before fill, after an existing barrel filled, refresh and loaded incumbent; ensure separate `ReplaceContents` scope is explicit and no falsely promised already-spawned retroactivity. |
| Alternative exits | Vetoed/failed Kill, manual admin removal, damage-to-zero, successful B3 world retirement, pool reuse, direct native destroy, disabled server drops, repeated/equivocal death hook, nested callback and normal throw. |
| Actor cases | Direct tracked human breaker, reconnect before deferred notification, NPC/environment/no human, projectile/explosive/trap/owner ambiguity, melee hit versus final lethal hit; nil if not provable. |
| Resource ownership | ItemManager.Create null/throw, created-but-not-placed, insertion rejection, bonus Item created before veto, `Item.Drop` null/throw, world spawn failure, Cleanup veto, partial multi-item payout, missing drop capacity; classify partial/unknown, no replay. |
| External interference | Different plugin suppresses OnLootSpawn, overrides generic/bonus drops, mutates container/stack limits/Item.amount, changes world state; I12 recorded separately from ordinary supported failures. |
| Publication & conflicts | Duplicate prefab rules root/addon, failed reload, same-owner replacement, dependency rebuild, provider unload, first-load module failure, failed candidate, selected rule while replacement occurs in a synchronous host callback; no old/new mixture. |
| Bounds & cross-feature | Drop-entry count/amount/max calls, 100 addons, malformed/mutable/deep/cyclic input and allocation faults; shared D23 intake; no B3 patch hash/topology disruption, deterministic teardown/recovery. |
| OS & shipping | Fresh Windows/Linux actual Carbon fixtures, source/loaded hash and full path traces, real world-drop entity proof, native/managed/ASan/UBSan/leak/worker/tooling/packaging/clean install/CI; no authenticated client or untested hosts claimed. |

**PASS standard:** static callgraph + exact loaded-host IL + controlled real host physical result + full affected regressions, not a successful hook callback, returned `Item` pointer, empty console log, or mere mock. If a host path is unqualified, *disable only that mode/prefab/source* and preserve previously qualified independent surfaces.

## 10. Separate phased roadmaps and decision gates

### Gameplay C

| Phase | Scope | Exit / stop |
|---|---|---|
| **C0 — source delta audit** | Reconfirm canonical D18 and latest B3/A contracts. Pin newer host current item method topology and list every quantity-change path. **No code.** | Evidence ledger with producer/postcondition matrix and explicit missing paths. |
| **C1 — PlayerInventory facade** | Public `Player:GetInventory`, `:HasItem`, `:CountItem` as exact scoped aliases; runtime/API/tooling/docs/tests. No Signals, no new mutation method. | Qualified same D18 physical outcomes, stale connection/domain checks, publication/regression and both OS managed/live compatibility as relevant. |
| **C2 — private observation prototype** | Shared bounded full physical snapshot and one signed net comparator; instrument host candidates, capture no public callbacks. Test source completeness and completion fences. | If a true terminal fence and safe bounded monitoring are not proved, remain private; report negative evidence. |
| **C3 — owner-bound event publication** | If C2 passes, conditionally expose `Inventory.ItemAdded`, `.ItemRemoved` (optional `.ItemChanged`) through existing Signals/codec/budget/callback roots. Formalize no replay, backpressure, sampled window and exact lifetime. | Synthetic + real VM, 100-addons, worst-case quotas, failed candidate and stale callback work PASS. |
| **C4 — exact host closure** | Windows/Linux live normal/negative net-delta matrix, patch drift/callback interference, source CI, tooling/docs/compatibility. | Explicit PASS of a **narrow semantics**; otherwise retain C1 with no misleading Signals. |

### Gameplay D

| Phase | Scope | Exit / stop |
|---|---|---|
| **D0 — exact barrel/drop static proof** | Audit prefab manifest, class dispatch, all fill and destruction physical payout paths, default suppression, actor availability and Item ownership on both current binaries. **No code.** | Choose **strict Replace**, *separately named ReplaceContents*, or **no public D mutation** based on evidence. |
| **D1 — pure declaration and publication model** | Typed `Loot.DropMode`, positional 3-arg rule validation, immutable descriptor, per-prefab conflict, D7 staged commit/rollback, bounds. **No live host mutation.** | Managed/native/VM publication conflict and fault qualification. May stay private until mode itself qualifies. |
| **D2 — narrow host adapter fixture** | One pinned supported barrel prefab and one proved suppressible mechanism; temporary item/world ownership accounting, exact pre-COMMIT plan and post-COMMIT physical observations. No public promise yet. | Verified no extra vanilla payouts for intended mode, no unsupported world drop and no unaccounted returned resources under baseline. Fail closed on any missing path. |
| **D3 — public narrow mode** | Publish either qualified **strict `Replace`** or distinct **`ReplaceContents`**, never relabel; add scripting metadata, examples/tooling and host version gating. | Full Windows/Linux live, compatibility, addon/reload/recovery, final CI/packaging matrix. |
| **D4 — conditional per-break policies** | Only if D0 locates a destruction-time pre-drop decision and actor is captured at the same exact lifetime: add a small host-evaluable declarative predicate/options contract. | Human/NPC/indirect ownership, source timing, nil fallback and rule conflict tests. Otherwise defer. |
| **D5 — deferred after-drop Signal** | Optional `Loot.DropResolved`/`BarrelBroken` immutable snapshot with proven world outcome, exact breaker, shared D23 transport. | Independent event completion and bounded delivery qualification. Not part of initial `Replace`. |

### Which phase first?

**Implement C1 first.** It adds exactly the desired author-facing Player-owned object with the **least new host-dependent behavior**, directly reuses the qualified D18 observation path and is separable from both high-risk C3 events and nontransactional D world loot. **Run D0 and C0 research next** (possibly in parallel) so Codex receives finite proof questions rather than a vague instruction to implement everything. D1 pure model can proceed before D2, but publishing `Loot:RegisterDropRule` while it cannot safely affect any qualified prefab would be a misleading placeholder. If developers specifically want barrel customization immediately, the earliest defensible opportunity is a proven **fill-only** mode, clearly named; this does **not** supersede the user's preferred eventual strict `Replace`.

## 11. Final architectural verdict

**C: READ-ONLY FACADE READY TO IMPLEMENT; VERIFIED NET-CHANGE SIGNALS NOT YET PROVED.** The core distinction is between **physical scanner correctness** (already qualified for queries and narrow D13 mutations) and **complete cross-host event chronology** (not qualified). A single signed comparator avoids duplicating event semantics, but only a proven capture boundary can stop phantom internal-transfer deltas.

**D: DECLARATIVE API/OWNERSHIP MODEL ACCEPTABLE; STRICT PHYSICAL BARREL-LOOT `Replace` NOT YET PROVED.** The positional call and service-local enum fit existing ergonomics. Public `OnLootSpawn`, `OnContainerDropItems` and bonus hooks are independent partial seams; they do not establish a complete pre-drop suppression point. Until current-host method topology, Item ownership and live world effects close that proof, D must not claim to replace all barrel loot. Synchronous per-break Luau callbacks are rejected; qualified declarative predicates and later observational Signals cover the real use cases without undermining I4/D23.

**Research-only note:** This assessment read the current repository through a read-only GitHub connection and public host documentation. It neither built/dissassembled the private exact host assemblies nor executed new Windows/Linux game fixtures. All presently unproved C/D facts are intentionally marked U/L. No implementation, repository edit, commit, push, tag or release was performed.

## 12. Source index — pinned repository and public host evidence

**Authority / qualification:**

- [AGENTS.md](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/AGENTS.md); [AICONTEXT.md](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/AICONTEXT.md); [Invariants.md](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/Invariants.md) (I1–I12, D7/D9/D10/D11/D13/D18/D20/D23).
- [Gameplay Events architecture research](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/GameplayEventsArchitecture-Research.md) (§13 loot, §14 inventory; older *global* Inventory service suggestion intentionally superseded here).
- [Player-1C observation](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/PlayerInteractionFoundation1C.md); [Inventory-M2](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/InventoryMutationM2Validation.md); [D13 reassessment](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/InventoryOwnershipFailureReassessment.md); [Player-1F-A](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/PlayerInteractionFoundation1FA.md); [Player-1F-B](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/PlayerInteractionFoundation1FB-Validation.md); [Player-1F-C](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/PlayerInteractionFoundation1FC.md).
- [Gameplay A qualification](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/GameplayFoundationA-Validation.md); [Gameplay B1 qualification](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/GameplayB1-Validation.md); [Gameplay B3 qualification](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/GameplayB3-Validation.md); [Compatibility](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/docs/Compatibility.md).

**Implementation inspected:**

- [PlayerDirectory.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/PlayerDirectory.cs), [CarbonLuau.Carbon.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/CarbonLuau.Carbon.cs), [InventoryObservation.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/InventoryObservation.cs), [InventoryMutation.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/InventoryMutation.cs), [InventoryGrant.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/InventoryGrant.cs), [CarbonLuau.Inventory.Carbon.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/CarbonLuau.Inventory.Carbon.cs).
- [FacadeSession.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/FacadeSession.cs), [GameplayEventFacade.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/GameplayEventFacade.cs), [GameplayEventBudget.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau/Facade/GameplayEventBudget.cs), [bootstrap.luau](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/scripts/bootstrap.luau), [native FacadeBridge.cpp](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/native/src/facade/FacadeBridge.cpp), [native Publication.cpp](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/native/src/runtime/Publication.cpp), [ApiCatalog.cs](https://github.com/gmoddev/CarbonLuau/blob/35bd8c3bb5d041063ba3388d23cc2cd37daa2808/src/CarbonLuau.Core/Api/ApiCatalog.cs).

**Public generated host source and prefab catalogs:** [OnLootSpawn](https://docs.oxidemod.com/hooks/resource/OnLootSpawn), [OnContainerDropItems](https://docs.oxidemod.com/hooks/entity/OnContainerDropItems), [OnBonusItemDrop](https://docs.oxidemod.com/hooks/item/OnBonusItemDrop), [OnBonusItemDropped](https://docs.oxidemod.com/hooks/item/OnBonusItemDropped), [OnItemStacked](https://docs.oxidemod.com/hooks/item/OnItemStacked), [OnItemUse](https://docs.oxidemod.com/hooks/item/OnItemUse), [OnItemAddedToContainer](https://docs.oxidemod.com/hooks/item/OnItemAddedToContainer), [OnItemRemovedFromContainer](https://docs.oxidemod.com/hooks/item/OnItemRemovedFromContainer), [OnEntityDeath](https://docs.oxidemod.com/hooks/entity/OnEntityDeath), [OnEntityKill](https://docs.oxidemod.com/hooks/entity/OnEntityKill), [older prefab entity catalog](https://github.com/OrangeWulf/Rust-Docs/blob/master/Entities.md), [public loot/prefab metadata gist](https://gist.github.com/c0deR3D/802c048fa6c48efc147d384077b4e715), [Facepunch oil barrel](https://wiki.facepunch.com/rust/oil-barrel).
