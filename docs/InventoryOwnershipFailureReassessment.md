# D13 Inventory Ownership / Failure Reassessment

**Repository baseline:** `main` at `ab221bc55ab7e58d62383a712ecbd793d98782b1` — *Adopt Player Interaction Foundation 1 architecture*.
**Scope:** architecture/research only. No implementation or repository modification.

> **Current canonical amendment (2026-09-21):** This rationale is subordinate
> to I12/D13/D18 in [Invariants.md](Invariants.md). I12 now defines the general
> trusted in-process host-interference boundary; it does not excuse unsafe
> adapters or ordinary acceptance/rejection. GiveItem's intended signature is
> `Player:GiveItem(ShortName, Amount, Behavior?) -> boolean`, defaulting to
> `GiveItemBehavior.InventoryOnly`. Two-argument examples remain valid intended
> calls; neither method nor enum is implemented. `DropRemainder` is future
> design only, not a reserved member. M2's original G1 conclusion is superseded
> as incomplete; [Player-1F-B](PlayerInteractionFoundation1FB.md) requires
> requalification under the supported-host model, not a documentation-only PASS.
> TakeItem's qualified implementation and PREPARE/COMMIT/VERIFY are unchanged.

The question is correctly reframed as what **CarbonLuau** must guarantee rather than whether Rust can provide globally transactional inventory operations.

---

## 1. TL;DR

**Original D13 is too strict in one important respect.**

Its factual findings about Rust remain correct:

* Rust item/inventory operations are live mutations, not transactions.
* stack/insertion/removal can partially mutate before completion;
* item construction can perform meaningful lifecycle work before returning an `Item`;
* callbacks and plugin hooks can run during inventory operations;
* arbitrary Rust/plugin side effects cannot generally be rolled back;
* there is no universal Rust transaction/undo receipt.

Current Rust source still demonstrates this directly. `MoveToContainer` mutates the destination stack and decrements the source before completing the overall operation, can recurse with a remainder, and has a split path capable of world-dropping an item. `ItemContainer.Insert` puts the item into `itemList` and assigns its parent before `FindPosition` and subsequent callbacks complete.

**What should change is the guarantee CarbonLuau demands from itself.**

D13 currently effectively requires:

> every host effect after item creation must either complete successfully or be universally rollbackable/cleanable.

That is stronger than CarbonLuau needs, inconsistent with the more general D10 model for irreversible host effects, and in part makes CarbonLuau responsible for exception safety *inside a supported Rust API before Rust even returns an object to it*.

I recommend replacing that requirement with:

```text
PREPARE
   ↓
COMMIT
   ↓
VERIFY
```

and three externally distinct outcomes:

```text
REJECTED
    no CarbonLuau inventory COMMIT began
    → false

SUCCESS
    host operation completed and CarbonLuau verified its physical postcondition
    → true

INDETERMINATE HOST FAILURE
    COMMIT began, but success could not be established
    → controlled Luau error
    → no rollback/unchanged-state claim
```

Under that contract:

* **`Player:GiveItem(shortName, amount) -> boolean` is architecturally viable.**
* **`Player:TakeItem(shortName, amount) -> boolean` is also architecturally viable**, and arguably easier to verify than GiveItem.
* neither operation is globally atomic with respect to arbitrary Carbon plugins;
* neither promises rollback after COMMIT;
* both remain implementation-gated until the exact target Rust build establishes the required no-world-drop and physical-verification paths.

**Final verdict: READY FOR CANONICAL ADOPTION.**

---

# 2. Where original D13 is too strict

D13 was correct to reject this claim:

> `GiveItem` is an all-or-nothing Rust transaction.

It was also correct to reject this:

> If Rust returns false or throws, the inventory necessarily remained unchanged.

Those remain false.

The excessive part is the stronger ownership requirement that effectively says:

> CarbonLuau cannot invoke `ItemManager.Create` unless it can deterministically clean every nested host resource Rust may have created before `Create` returns.

The historical investigation found a legitimate counterexample: `Create` calls initialization before returning, and initialization can create UIDs, containers, held entities and other item-mod resources. If an internal lifecycle failure occurs before `Create` returns, CarbonLuau never receives the `Item` reference.

But that should be classified as:

> **failure inside a supported host operation before ownership/control of the returned host resource reached CarbonLuau**

rather than:

> **CarbonLuau leaked one of its resources**.

That narrower rule is more consistent with I1: Rust, Carbon and Unity own their host objects; CarbonLuau owns its own runtime resources and temporary adaptation responsibilities.

This change does **not** excuse CarbonLuau once an `Item` has successfully returned to it.

---

# 3. Revised ownership boundary

Use three categories.

### A. CarbonLuau-owned resources

Anything actually owned by CarbonLuau remains under the existing strong rules.

Examples:

* mutation-gate state;
* temporary snapshots/plans;
* native/managed buffers;
* temporary lists;
* domain/session metadata.

Every path must deterministically release them.

**No change to I1/I10 cleanup discipline.**

### B. Returned host resources under CarbonLuau responsibility

If:

```csharp
Item item = ItemManager.Create(...);
```

successfully returns an `Item`, CarbonLuau now knows about a concrete host object.

Until one of these terminal outcomes is established:

```text
transferred into accepted player inventory
removed through the supported host cleanup path
consumed during a successful stack merge
otherwise explicitly transferred to another accepted host owner
```

CarbonLuau has **temporary responsibility** for it.

CarbonLuau must never merely discard its reference on an ordinary failure path.

If supported cleanup itself is vetoed/fails, however, that becomes **INDETERMINATE HOST FAILURE**. CarbonLuau must not start reproducing private Rust teardown internals to force cleanup.

Current Rust reinforces why cleanup cannot be an unconditional rollback mechanism: `Item.Remove()` can be canceled by `OnItemRemove`, and only after that hook does it invoke item-mod `OnRemove`, zero the amount and schedule host removal.

### C. Unreturned host-internal resources

If CarbonLuau enters a supported Rust API correctly and that API fails before returning the resource:

```text
CarbonLuau → ItemManager.Create(...)
                 ↓
             internal host work
                 ↓
               failure
                 X
          no Item returned
```

then inaccessible resources created entirely inside that invocation belong to the **host failure domain**.

CarbonLuau:

* reports the operation as an indeterminate host failure;
* does not claim rollback;
* does not fabricate a host object it never received;
* does not call private lifecycle methods trying to reconstruct unreachable internals;
* does not classify that inaccessible object as a CarbonLuau resource leak.

That is the exact place D13 should be narrowed.

---

# 4. Precise atomicity taxonomy

Avoid describing `GiveItem` or `TakeItem` simply as "atomic."

| Property                                                            | CarbonLuau guarantee           |
| ------------------------------------------------------------------- | ------------------------------ |
| Provisional candidate/module execution performs inventory mutation  | **Never**                      |
| Two Luau-originated mutations for the same exact Player interleave  | **No**                         |
| CarbonLuau validates and performs bounded PREPARE first             | **Yes**                        |
| Another Luau addon recursively enters the VM halfway through COMMIT | **No**                         |
| CarbonLuau knows whether COMMIT has started                         | **Yes**                        |
| Rust itself makes inventory mutation all-or-nothing                 | **No**                         |
| Other Carbon plugins are isolated during COMMIT                     | **No**                         |
| Arbitrary plugin/Rust effects are rollbackable                      | **No**                         |
| An exception after COMMIT proves inventory unchanged                | **No**                         |
| `false` means no inventory COMMIT began                             | **Yes**                        |
| `true` means the requested physical postcondition was verified      | **Yes**                        |
| controlled post-COMMIT error may mean state changed                 | **Yes, explicitly documented** |

The strongest useful term is:

> **CarbonLuau-serialized, definite-rejection inventory mutation**

not "transactional Rust inventory."

D7's publication transaction remains a different concept.

---

# 5. PREPARE semantics

PREPARE must be **pure with respect to inventory mutation**.

For either Give or Take it performs:

1. exact D11 Player/domain validation;
2. committed-operation validation;
3. canonical short-name validation and definition lookup;
4. amount/type/work-bound validation;
5. bounded physical snapshot of D18's accepted top-level inventory;
6. operation-specific feasibility planning.

For GiveItem, inspect:

* current compatible physical stacks;
* stack capacities;
* free slots;
* container capacity;
* item definition properties;
* accepted container restrictions that can be inspected without callbacks;
* the number of host item objects/placements the request would require.

For TakeItem:

* physically count the target definition across main, belt and wear;
* record relevant item identities/amounts for verification;
* reject if there is definitely less than requested.

### PREPARE must not invoke `CanAcceptItem`

Current `CanAcceptItem` is not a pure predicate. It invokes a container delegate and the `CanAcceptItem` Carbon/Oxide hook before returning the result.

A plugin could mutate arbitrary state from that hook.

Therefore CarbonLuau should not say:

```text
PREPARE is mutation-free
```

and then call arbitrary plugin code during PREPARE.

`CanAcceptItem` and equivalent hookable checks belong to COMMIT.

### What PREPARE guarantees

PREPARE can prove:

> This operation is already structurally impossible, so CarbonLuau rejected it without beginning inventory mutation.

PREPARE **cannot** prove:

> COMMIT must succeed.

Facepunch itself uses this style. Marketplace work added `ItemSafety`/fit simulation specifically to predict whether a move is likely to fit before attempting the real transaction. Current marketplace code calls `CheckItem.CanFitSimple` before charging the player, but still contains explicit compensation logic later if the transaction fails.

That is strong evidence for PREPARE as a useful filter, not as a transaction guarantee.

---

# 6. Exact COMMIT boundary

For `GiveItem`, COMMIT begins **immediately before entering the first item-creation call**:

```text
COMMIT_BEGUN = true
ItemManager.Create / CreateByItemID(...)
```

Not:

```text
Item successfully returned
```

and not:

```text
MoveToContainer began
```

Why?

Because historical target-build evidence established that item initialization occurs before `Create` returns and may allocate UID/item-mod/nested host state.

Therefore entering item creation is the first point after which CarbonLuau can no longer truthfully promise:

> nothing host-visible may have happened.

This also makes the phase boundary trivial to implement and audit:

```text
PREPARE
  all pure validation/planning

set phase = COMMIT

Create(...)
transfer(...)
verify(...)
```

For `TakeItem`, COMMIT begins immediately before entering the host removal call:

```csharp
player.inventory.Take(...)
```

because that method invokes `OnInventoryItemsTake` before proceeding into sequential container removal.

---

# 7. VERIFY semantics

VERIFY is not rollback.

It answers:

> Given that COMMIT has finished control flow, can CarbonLuau safely say the requested physical gameplay result is now true?

## GiveItem verification

Capture before COMMIT:

```text
Q0 = physical count of ShortName across accepted inventory
S0 = bounded relevant stack identity/amount snapshot
```

Retain references to every successfully returned item created as part of this operation until it reaches a known terminal state.

After all transfer calls finish:

```text
Q1 = fresh physical count
S1 = fresh bounded inventory snapshot
```

A verified Give success requires **all** of:

1. exact D11 Player is still the same connection;
2. every CarbonLuau-issued transfer step reported host success;
3. no CarbonLuau transfer path intentionally world-dropped anything;
4. every returned created Item is accounted for as either:

   * physically within one of the accepted Player containers, or
   * fully consumed into stack merging/removal through the successful transfer path;
5. final physical quantity satisfies:

```text
Q1 >= Q0 + requestedAmount
```

The count condition is deliberately `>=`, not `==`.

A synchronous plugin may legitimately add a bonus item during the operation. That does not invalidate the fact that the Player finishes with at least the requested net increase, provided CarbonLuau's own transfer steps also all reported success and the created resources are accounted for.

### Why count delta alone is insufficient

This alone is too weak:

```text
Q1 - Q0 == requestedAmount
```

because another plugin may add or remove the same item during a synchronous hook.

Likewise, the originally created Item's UID cannot be the only proof because a normal successful stack merge can consume/remove the source `Item`. Current `MoveToContainer` increases the destination stack, decrements the source, invokes callbacks, migrates ownership and then removes the exhausted source object.

So verification must combine:

```text
host transfer outcome
+ returned-object accounting
+ final physical state
```

### Provenance limitation

CarbonLuau cannot prove causal provenance against an arbitrary trusted plugin deliberately doing something like:

```text
destroy CarbonLuau's 100 scrap
create a different 100 scrap
```

inside a synchronous callback.

But if the final physical postcondition is correct and every CarbonLuau transfer reported success, that distinction is not meaningful to the scripting facade.

CarbonLuau guarantees the **observable physical result**, not forensic provenance against trusted managed plugins.

## TakeItem verification

TakeItem is cleaner.

Capture:

```text
Q0
```

Call the host removal path.

Then:

```text
Q1
```

SUCCESS requires:

```text
host reported removedAmount == requestedAmount
AND
Q0 - Q1 == requestedAmount
```

using CarbonLuau's physical D18 count rather than the hook-virtualized host `GetAmount`.

This catches an `OnInventoryItemsTake` plugin that simply returns the requested integer without physically removing anything.

---

# 8. Operation outcome model

Adopt exactly three operational outcomes.

### REJECTED

Occurs **only before COMMIT**.

Examples:

* insufficient inventory space known from PREPARE;
* insufficient physical item quantity for Take;
* operation would exceed bounded placement work;
* no accepted top-level placement is structurally possible.

Public result:

```lua
false
```

Guarantee:

> No CarbonLuau inventory COMMIT began.

Programming errors and stale Player state are still **errors**, not `false`.

### SUCCESS

COMMIT completed and VERIFY established the public postcondition.

Public result:

```lua
true
```

### INDETERMINATE HOST FAILURE

Any of these after COMMIT begins:

* `Create` throws;
* `Create` unexpectedly returns unusable state;
* a host transfer fails;
* a Rust lifecycle callback fails;
* expected resource ownership cannot be established;
* cleanup is vetoed/fails;
* host returns an ambiguous result;
* Player disconnects;
* final verification mismatches;
* prohibited world-drop state is detected.

Public result:

```lua
error("GiveItem host failure after commit began; inventory state may have changed")
```

or the Take equivalent.

The exact bounded diagnostic can differ, but **it must be distinguishable from normal `false`**.

Once COMMIT begins, CarbonLuau should conservatively **never downgrade the outcome to `false`**.

That keeps the contract extremely simple:

```text
false = definitely never committed
true  = verified physical outcome
error = may have changed
```

---

# 9. Per-Player CarbonLuau mutation serialization

Use one host-level mutation gate keyed by:

```text
D11 exact connection lifetime token
```

not UserId alone and not domain ID.

The gate is:

* domain-independent;
* shared by root and all addons;
* owner-thread only;
* held for one synchronous PREPARE→COMMIT→VERIFY operation;
* always released in `finally`.

Do **not** use a blocking mutex.

CarbonLuau already serializes VM/game-state execution on the owner thread. The gate is therefore a logical invariant rather than an OS concurrency primitive.

If a mutation for the same exact Player is already in flight:

```text
reject recursive/inconsistent admission with a controlled error
```

Do not queue and wait while holding host state.

Deferred callbacks execute later after release.

### Cross-addon behavior

Addon A:

```lua
Player:GiveItem("scrap", 100)
```

and addon B attempting another same-player mutation cannot interleave their host operations.

Sequential operations are fine.

### Disconnect

If disconnect occurs:

* before COMMIT: stale Player error; no host mutation;
* after COMMIT: VERIFY normally fails exact D11 identity and the result is indeterminate.

The gate still disappears in `finally`.

### Replacement/recovery

The gate is ephemeral host-operation state, not domain-owned retained publication.

Root/addon replacement cannot inherit it.

VM retirement/reconstruction cannot replay the failed operation.

---

# 10. Synchronous plugin interference model

This needs one correction to the historical D13 threat model.

Current Carbon's hook dispatcher wraps ordinary plugin/module hook invocation in exception handling and logs hook failures. So a normal Carbon plugin `throw` generally does **not** simply unwind through the underlying Rust method.

[Carbon HookCaller source](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon.Components/Carbon.Common/src/Carbon/Components/HookCaller.cs?utm_source=chatgpt.com)
[Carbon HookCallerInternal source](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon/src/Hooks/HookCallerInternal.cs?utm_source=chatgpt.com)

Therefore D13 should not collapse these into one category:

```text
Carbon plugin hook exception
Rust/internal lifecycle exception
```

They are different.

However plugin interference remains very relevant because a plugin can synchronously:

* reject acceptance;
* override a result;
* change the same stack;
* add/remove another item;
* move an item elsewhere;
* spawn entities;
* perform unrelated side effects.

For example, `CanAcceptItem` explicitly permits a hook to override acceptance. `PlayerInventory.Take` similarly accepts an integer hook override before default physical removal.

Also, direct Rust delegates such as:

```text
onPreItemRemove
onItemAddedRemoved
slot reservation callbacks
item-mod lifecycle methods
```

are not the same as Carbon's ordinary plugin hook dispatcher and retain their own failure behavior. `ItemContainer.Remove`, for example, invokes `onPreItemRemove` before unlinking and several delegates before finishing bookkeeping.

CarbonLuau therefore guarantees:

> serialization against **Luau-originated** mutations.

It explicitly does **not** guarantee isolation against arbitrary trusted Carbon plugins.

---

# 11. GiveItem decision

## ACCEPT ARCHITECTURALLY

Adopt:

```lua
Player:GiveItem(ShortName, Amount, Behavior?) -> boolean
```

under the PREPARE/COMMIT/VERIFY contract.

Do **not** characterize it as transactional Rust inventory.

Its API contract is stronger and more useful:

```text
true
    verified physical delivery

false
    definite PREPARE rejection before inventory COMMIT

error
    host COMMIT began but CarbonLuau cannot safely establish success
```

This is Option A from the requested analysis and is the best author-facing design.

---

# 12. Exact GiveItem public contract

```lua
local Given = Player:GiveItem("scrap", 100)
```

## Inputs

`ShortName`:

* D18 canonical lowercase Rust short-name rules;
* 1–128 ASCII bytes;
* no NUL;
* malformed/noncanonical → programming error;
* canonical but nonexistent item definition → controlled programming/configuration error.

`Amount`:

* required;
* integer;
* `1 <= Amount <= Int32.MaxValue`;
* actual work additionally bounded by the placement plan.

Using `Int32.MaxValue` matches the underlying host amount representation without inventing an arbitrary low gameplay cap.

A huge value still normally PREPARE-rejects because the accepted inventory cannot contain it within bounded placement work.

## Inventory destination

"GiveItem" means:

> deliver the requested physical item quantity into this exact Player's ordinary accepted top-level inventory.

Accepted containers are the same conceptual inventory D18 observes:

```text
main
belt
wear
```

CarbonLuau does **not** guarantee a specific slot or which accepted top-level container receives an item.

Excluded:

* backpacks;
* nested item containers;
* corpses;
* external storage;
* world entities.

The adapter must use bounded preplanned explicit slots on the qualified path,
not automatic slot selection. The public contract remains container-neutral
within the accepted set.

## Stacking

Allowed.

## Multiple stacks

Allowed.

## Partial placement

Never returned as normal success.

If part of the grant lands and the rest fails:

```text
INDETERMINATE HOST FAILURE
```

not `false`.

## Full inventory

If PREPARE can establish it cannot fit:

```lua
false
```

without creating an Item.

## Exact Player

Every phase uses D11 exact connection identity.

## Provisional

Rejected before PREPARE/COMMIT as a controlled provisional-effect error.

---

# 13. World-drop decision

**World drop is forbidden as an implementation of `GiveItem`.**

This API must never mean:

```text
give it to the player, or if that fails throw it on the floor
```

Historical D13 correctly rejected `BasePlayer.GiveItem` for exactly this reason.

Current `MoveToContainer` still contains a split path that can call `Drop` when it cannot place a generated split remainder.

Therefore the implementation must satisfy D13's revised G1 no-drop wording
within I12's supported-host boundary. It cannot select or knowingly permit
drop fallback; normal non-mutating callback acceptance/rejection remains in
scope. Premise-invalidating external mutation is not isolated, but observed
world delivery or uncertainty cannot be converted into InventoryOnly success.

The recommended strategy is:

```text
PREPARE computes explicit bounded placement chunks
    ↓
each created chunk is no larger than its planned target stack/container capacity
    ↓
transfer through a no-swap/no-drop qualified path
```

Do not hand an oversized amount to a host path and hope its splitting behavior stays inventory-only.

If the exact supported Rust build cannot provide a reliable no-world-drop path for the planned chunks:

> **GiveItem implementation remains gated/deferred.**

Do not weaken the public API.

---

# 14. TakeItem decision

## ACCEPT ARCHITECTURALLY

The revised model actually makes `TakeItem` at least as defensible as GiveItem.

Current `PlayerInventory.Take` naturally traverses:

```text
main → belt → wear
```

which matches D18's physical inventory model.

Adopt:

```lua
Player:TakeItem(ShortName, Amount) -> boolean
```

with identical outcome classes:

```text
true
    requested physical quantity removal verified

false
    PREPARE proved there was insufficient physical quantity;
    COMMIT never began

error
    host removal began but exact final result could not be established
```

### VERIFY

Require:

```text
hostReturnedAmount == requestedAmount
AND
physicalBefore - physicalAfter == requestedAmount
```

A plugin hook that reports success without performing removal therefore cannot produce a false CarbonLuau success.

### Why this is now acceptable

Original D13 treated partial removal as fatal to the API because it could not be rolled back.

Under the revised contract:

```text
partial removal + later failure
```

is simply:

```text
INDETERMINATE HOST FAILURE
```

and the Luau author is not lied to.

CarbonLuau does not need to restore consumed items to claim a useful, honest API.

---

# 15. Provisional, D11 and deadline behavior

## Provisional

Both:

```lua
Player:GiveItem(...)
Player:TakeItem(...)
```

are committed-only irreversible host operations.

They fail from:

* candidate initialization;
* provisional module initialization;
* provisional cross-domain call chains.

A dependency cannot launder provisional state.

This remains valid:

```lua
task.defer(function()
    Player:GiveItem("scrap", 100)
end)
```

after successful publication.

Failed candidates never execute it.

## D11

The exact Player is revalidated:

```text
before PREPARE
immediately before COMMIT
during/after VERIFY where required
```

An old Player proxy never mutates a reconnect.

## Deadlines

The existing Luau execution deadline does not hard-preempt managed/Rust host code. That is already an I8 limitation.

PREPARE→COMMIT→VERIFY should therefore execute inside one synchronous managed facade operation.

Do not:

```text
COMMIT
return to Luau
later VERIFY
```

because a deadline or script behavior could intervene.

A timeout after a completed host mutation does not roll it back.

A fatal VM recovery never replays the mutation.

Exactly-once remains explicitly unpromised under D9/D10.

---

# 16. Safety and resource bounds

### Short name

Reuse D18:

```text
1..128 ASCII bytes
canonical lowercase
no NUL
```

### Amount

```text
integer
1..2,147,483,647
```

plus the placement/removal work bound below.

### Physical inventory inspection

Bound:

* containers inspected;
* stacks inspected;
* matching identities retained in the temporary snapshot.

Only:

```text
main
belt
wear
```

No recursive nested traversal.

### Placement work

The canonical rule should be:

> one operation may create/transfer no more item chunks than the bounded accepted inventory can physically contain.

The exact numeric maximum should derive from the qualified target build's supported container-capacity envelope rather than a fabricated `64` or `1000`.

Clamp modded/abnormal container capacity to a documented CarbonLuau inspection bound; exceed it → controlled failure.

### Verification

One fresh bounded scan.

No repeated polling until state "looks right."

### Mutation gate

At most one synchronous in-flight inventory mutation per exact Player.

No unbounded waiting queue.

### Diagnostics

Bounded category:

```text
inventory rejected
inventory host failure after commit
inventory verification mismatch
inventory world-drop violation
inventory resource cleanup failure
```

No raw host exception/object dump to scripts.

---

# 17. D13 amendments

## Keep

Preserve these findings as canonical:

1. Rust inventory operations are not globally transactional.
2. item creation can execute lifecycle behavior before returning;
3. insertion/stacking/splitting can publish partial mutation;
4. cleanup is itself a host lifecycle operation;
5. callbacks/plugins may synchronously affect operations;
6. no universal rollback point exists;
7. arbitrary plugin effects are not CarbonLuau-rollbackable;
8. `BasePlayer.GiveItem`/world-drop fallback is not an acceptable implementation of inventory-only `GiveItem`;
9. raw Item/ItemContainer internals stay hidden from Luau.

Current Rust still materially supports those findings.

## Amend

Replace this implicit requirement:

> An inventory API is unacceptable unless every post-construction failure can be transactionally rolled back and every nested host resource deterministically recovered.

with:

> CarbonLuau provides bounded PREPARE, explicit COMMIT classification, per-exact-Player Luau serialization and post-COMMIT VERIFY. Before COMMIT, definite rejection guarantees no CarbonLuau inventory mutation began. After COMMIT begins, CarbonLuau makes no rollback guarantee: verified success is returned only when the operation's physical postcondition is established; otherwise the operation raises an indeterminate host-failure error. Resources successfully returned to CarbonLuau remain under temporary responsibility until transfer or supported cleanup is attempted. Resources created solely inside a supported host operation that fails before returning them remain in the host failure domain.

Also clarify:

> Carbon plugin hook exceptions are ordinarily isolated by Carbon's hook dispatcher; plugin mutation/override behavior and direct Rust lifecycle callbacks remain relevant interference.

## Supersede

Supersede D13's current conclusion:

> item mutation can only be reconsidered with a stronger Rust transactional API or universal safe abort adapter.

That condition is unnecessarily strong.

Replace it with:

> item mutation is eligible when a specific operation has a bounded PREPARE path, explicit COMMIT boundary, strongest-defensible VERIFY postcondition, exact Player serialization, no forbidden fallback behavior, controlled indeterminate-failure semantics and exact target-build qualification.

This reopens **GiveItem and TakeItem architecture** without erasing any historical Phase 4 evidence.

---

# 18. D18 implications

Do not modify D18 as part of this research task.

A later canonical adoption should amend D18's current mutation deferral to add:

```text
Player:GiveItem(shortName, amount) -> boolean
Player:TakeItem(shortName, amount) -> boolean
```

under revised D13.

I recommend adding a separate phase rather than disturbing already-defined Player-1A through Player-1E:

### Player-1F — inventory mutation

Contains only:

```lua
Player:GiveItem(...)
Player:TakeItem(...)
```

with D13 PREPARE/COMMIT/VERIFY semantics.

If GiveItem's no-world-drop host gate fails while TakeItem passes, Player-1F may qualify TakeItem alone. Public symmetry is not an invariant.

---

# 19. Roblox-like author-facing examples

Normal Give:

```lua
if Player:GiveItem("scrap", 100) then
    print("Delivered")
else
    print("Not enough inventory space")
end
```

Normal Take:

```lua
if Player:TakeItem("scrap", 100) then
    print("Payment accepted")
else
    print("Not enough scrap")
end
```

Host failure remains catchable:

```lua
local Ok, Result = pcall(function()
    return Player:GiveItem("rifle.ak", 1)
end)

if not Ok then
    print("The host could not establish a safe final result:", Result)
elseif not Result then
    print("The inventory could not accept it")
end
```

No author-facing:

```lua
Item
ItemContainer
ItemDefinition
transaction handles
rollback tokens
container enums
cleanup receipts
```

Rust's complexity remains behind the facade.

---

# 20. Exact behavior still deferred/rejected

Do **not** add:

```lua
Player:GetInventory()
Player:GiveItemToContainer(...)
Player:GiveItemAtSlot(...)
Player:MoveItem(...)
Player:SplitItem(...)
Player:DropItem(...)
Player:RemoveItemByUid(...)
```

Do not expose:

```text
Item
ItemDefinition
ItemContainer
UID
Rust container IDs
Rust move flags
ignoreStackLimit
allowSwap
```

Also deferred:

* nested inventory mutation;
* backpack mutation;
* world-drop APIs;
* item condition/custom-data editing;
* skins/instance data/attachments as GiveItem options;
* transactional multi-item batches;
* rollback/undo APIs;
* cross-player inventory transfer.

Foundation mutation remains deliberately small.

---

# 21. Implementation phases

### Inventory-M1 — deterministic transaction model

Managed/model-only.

Implement/test:

* exact-Player mutation gate;
* PREPARE plans;
* phase state;
* REJECTED/SUCCESS/INDETERMINATE outcomes;
* fake host failure injection;
* verification algorithms;
* bounded snapshots.

No Rust mutation yet.

### Inventory-M2 — exact target-build adapter qualification

Inspect current Windows/Linux target assemblies and establish:

* exact Create path;
* item creation return/ownership behavior;
* no-drop transfer path;
* accepted-container semantics;
* stacking/chunk placement;
* Take behavior;
* cleanup behavior for returned unattached items;
* useful verification identities/state.

No public API until this passes.

### Player-1F-A — TakeItem

Implement Take first.

It has no newly created resource and directly matches D18's main/belt/wear model.

### Player-1F-B — GiveItem

Implement only after no-world-drop transfer qualification.

### Player-1F-C — combined lifecycle/public closure

Run:

* cross-addon;
* reload;
* provider;
* VM recovery;
* stress;
* Windows/Linux live Carbon;
* docs/API identity review.

---

# 22. Qualification plan

## Deterministic model tests

For both APIs:

* pre-COMMIT rejection changes nothing;
* `true` requires VERIFY;
* post-COMMIT host false cannot become ordinary `false`;
* exception after COMMIT becomes indeterminate;
* verification mismatch becomes indeterminate;
* gate always releases;
* no recursive same-player mutation;
* two addons serialize;
* different domain lifetimes cannot bypass D11/D10;
* provisional operation cannot mutate.

## GiveItem host cases

Test:

* empty inventory;
* partially filled compatible stack;
* multiple compatible stacks;
* exact stack completion;
* free-slot insertion;
* full inventory → pre-COMMIT `false`;
* non-stackable item;
* condition-bearing item;
* wearable/belt-main placement;
* nested-content item;
* held-entity-producing item;
* item-mod-created resources;
* invalid short name;
* unknown canonical short name;
* amount bounds;
* placement-work limit;
* disconnect before PREPARE;
* disconnect immediately before COMMIT;
* disconnect during COMMIT if injectable;
* failure during creation;
* failure after item return;
* failure during insertion;
* stack callback mutation;
* CanAcceptItem rejection;
* plugin changes same stack during callback;
* plugin removes granted item before VERIFY;
* plugin adds bonus same-short-name quantity;
* cleanup hook rejection;
* verification mismatch;
* prohibited Drop path;
* repeated grants.

## TakeItem host cases

Test:

* main only;
* belt only;
* wear only;
* amount split across all three;
* multiple stacks;
* exact depletion;
* partial stack reduction;
* insufficient amount → `false`;
* `OnInventoryItemsTake` returns requested count without mutation → error;
* hook physically removes requested amount and returns matching count → success;
* hook removes wrong quantity → error;
* plugin adds same item during Take → verification error;
* mid-removal failure → indeterminate;
* stale/disconnect scenarios;
* repeated removals.

## Callback/reentrancy

Controlled Carbon plugin fixtures must:

* mutate target inventory;
* reject acceptance;
* alter stack quantity;
* add bonus items;
* attempt CarbonLuau-triggering actions;
* throw from normal Carbon hooks.

Confirm normal Carbon hook exceptions are contained by Carbon and do not recursively enter Luau, while host result/physical-state changes are still detected.

Direct non-Carbon delegates/item-mod paths need separate injected failure coverage because Carbon HookCaller containment does not apply to them.

## Lifecycle

Cover:

* 100 healthy root replacements;
* failed candidates;
* addon replacement;
* provider unload/reload;
* VM fatal recovery;
* CarbonLuau unload;
* reconnect with same Steam ID;
* gate/resource state returns to baseline.

## Platforms

Required:

* current qualified Windows Rust build;
* current qualified Linux Rust build;
* compare relevant method semantics across platforms;
* live Carbon.

No authenticated Rust client is required merely to verify server-authoritative inventory quantity/ownership.

Client evidence is required only if later API semantics make an assertion about client UI/notices/equipped-state presentation.

---

# 23. Genuinely unresolved host-dependent gates

Architecture is resolved. Only these implementation gates remain.

### G1 — GiveItem no-world-drop adapter

The exact target build must demonstrate D13's supported-host no-drop path under
I12. The original M2 G1 conclusion is superseded, not restored by adopting that
boundary. The required normal-host and separate hostile-mutation matrix is in
[Player-1F-B](PlayerInteractionFoundation1FB.md#required-1f-b-requalification).

Failure of G1:

```text
GiveItem stays deferred.
```

Do not redefine GiveItem to permit dropping.

### G2 — returned-Item terminal-state observability

After a successful `Create`, CarbonLuau must be able to determine enough state to classify each created Item as:

```text
accepted inventory
consumed by successful stack merge
still under temporary responsibility
unexpected/indeterminate
```

If this cannot be established on the target build, GiveItem does not qualify.

### G3 — target-build Take verification

Confirm the current `Take` semantics and any `collect` behavior needed to perform bounded structural verification.

The current generated source already strongly supports main→belt→wear sequential semantics and hook override behavior.

### G4 — cleanup handling for returned unattached items

Establish the supported cleanup request and exact observable state after it.

CarbonLuau is **not** required to defeat a trusted plugin that deliberately vetoes cleanup. Such veto/failure remains indeterminate.

### G5 — work bounds from actual container maxima

Select concrete stack/placement inspection limits from the target build and qualified modded-envelope policy.

These are tuning/qualification gates, not unresolved public semantics.

---

# 24. Final verdict

## READY FOR CANONICAL ADOPTION

D13 should be revised.

The historical evidence was valuable and should remain untouched, but the conclusion drawn from it was too strong.

The canonical model should become:

```text
CarbonLuau does not promise globally atomic Rust inventory.

CarbonLuau does promise:

    exact-Player serialization
    +
    bounded pure PREPARE
    +
    explicit first-host-effect COMMIT boundary
    +
    no mutation during provisional execution
    +
    strongest-defensible physical VERIFY
    +
    false only for definite pre-COMMIT rejection
    +
    true only for verified success
    +
    controlled error for post-COMMIT uncertainty
```

Under that model:

| API                              | Architecture     |
| -------------------------------- | ---------------- |
| `Player:GiveItem(name, amount)`  | **ACCEPT**       |
| `Player:TakeItem(name, amount)`  | **ACCEPT**       |
| rollback/undo                    | **REJECT**       |
| global plugin isolation          | **NOT PROMISED** |
| world-drop fallback for GiveItem | **FORBIDDEN**    |
| raw Item/container exposure      | **REJECT**       |

The most important change is conceptual:

> **A lack of rollback no longer makes an operation unexposable. It means CarbonLuau must distinguish definite non-commit from verified success and from indeterminate post-commit failure.**

That is already consistent with D10's treatment of irreversible host effects. It gives Luau authors the simple semantics the project wants without making a false claim that Rust suddenly became transactional.
