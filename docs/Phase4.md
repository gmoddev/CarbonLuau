# Phase 4 — item conveniences: investigation, not implementation

Status: **DEFERRED from v0.1 by approved D13 closure (2026-09-14)**. This file
preserves the completed item investigation and rejected candidate boundaries.
It does not declare Items or Player:GiveItem available. The canonical decision owner is
[Invariants.md](Invariants.md#decision-register); actual evidence is in
[Phase4-Validation.md](Phase4-Validation.md).

The entire surface, including read-only Items:Exists, is deferred. No independent
v0.1 use case was accepted for shipping Items alone. The
[roadmap](CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases)
now moves to Phase 5 hardening in a separate task. This is a product-scope
resolution, not a finding that the adapter became safe. The investigation-time
blocked verdict, proposed semantics and reopening evidence below are historical;
none is an outstanding v0.1 implementation obligation.

## Qualified host API findings

Inspected the retained Windows and Linux Rust 2633 / Steam build 25230300
Assembly-CSharp.dll files used by the Carbon 2.0.259.0 worker environments.
This is direct IL/metadata evidence, not copied historical Oxide examples.
Selected method bodies agree across both platform assemblies.

| Operation | Observed host semantics |
|---|---|
| ItemManager.FindItemDefinition(string) | Calls Initialize, then itemDictionaryByName lookup. The dictionary uses StringComparer.OrdinalIgnoreCase. Exact short-name lookup is therefore case-insensitive on this build, not fuzzy/display-name lookup. Absent key returns null. |
| ItemManager.CreateByName | Searches itemList then delegates through CreateByItemID to Create; it does not provide a safer ownership boundary. |
| ItemManager.CreateByItemID | Resolves the definition and delegates to Create. Missing definition returns null. |
| ItemManager.Create(ItemDefinition,int,ulong,bool,ulong) | Defaults amount 1, skin/attachment 0 and server-side true. Invalid definition/nonpositive amount returns null. Otherwise allocates or obtains one Item from the pool, sets fields and calls Initialize before returning it. It does not enforce a conservative facade amount/stack/work bound. |
| Item.Initialize / OnItemCreated | Obtains a network UID and runs definition item-mod callbacks synchronously. ItemModContainer can create a contents container during initialization; one requested Item does not imply allocation of only one host resource. |
| BasePlayer.GiveItem | Returns void. Calls inventory transfer, sends inventory notification on success, but calls Item.Drop on failure. It cannot back an inventory-only no-drop promise. Reason values can also cause statistics/mission side effects. |
| PlayerInventory.GiveItem | Returns bool. Tries selected/ideal container, main, optionally backpack, then belt via MoveToContainer. The bool alone is not an atomic-grant receipt. |
| Item.MoveToContainer | May merge into existing stacks, mutate amounts, invoke callbacks, recurse for the remainder, split items, swap entries or remove conflicting slots. Partial stacking can precede a false return. Its container-max-stack split path can drop a split item into the world. |
| Item.SetParent / ItemContainer.Insert | Insertion and callbacks are synchronous. SetParent can schedule Item removal if insertion fails. MoveToContainer may return true after calling SetParent; postconditions must not be inferred solely from the bool. |
| Item.Remove(float) | Calls item-mod OnRemove callbacks before setting removeTime, zeroing amount and queueing ItemManager.RemoveItem. A completed scheduled removal is guarded against rescheduling; an exception before scheduling is a separate failure path. |
| ItemManager.DoRemoves | Processes the host removal queue, calls DoRemove, then returns pooled Items when pooling is enabled. DoRemove returns the UID, removes world/container ownership and frees contents. Manually calling both DoRemove and queued removal would risk duplicate cleanup. |

The normal full-inventory result depends on the chosen transfer path. A failed
BasePlayer convenience grant may leave a world drop. A failed lower-level stacking
attempt may already have delivered part of the amount. Documentation must not
claim either failure means zero mutation.

Carbon's [hook reference](https://carbonmod.gg/references/hooks/) lists dynamic item
hooks including acceptance, stack and container-add/remove hooks. Direct host IL
also invokes item/container delegates synchronously. Subscription-time patching and
other plugins can change behavior; reference documentation is not proof that every
hook is active in the retained server. No general Lua item hook is authorized.

## Narrow direction investigated, not accepted

The approved D13 follow-up evaluated an even narrower candidate than the initial
single-stack proposal: **amount exactly 1**, one initial Item, explicit free
compatible main-inventory slot, no stacking, splitting, swapping, conflict eviction,
backpack/belt fallback or world drop. Full inventory would reject even if an
existing stack had space. Slot scanning would also need a fixed work cap and
unsupported container configurations would reject. No such cap or public numeric
contract is accepted: the ownership gate fails first. Earlier 1..1000/64-byte
suggestions are not implemented limits.

With unchanged host state, an explicit free slot and disabled stack/swap paths
avoid ordinary merging; amount 1 cannot exceed a positive integral maxStackSize.
They do not prevent callbacks during initialization, acceptance, slot reservation,
insertion or teardown. Preflight is not a reservation or transaction. See the
rejected candidate below. No live-qualified narrow grant contract was found.

The investigation identified host case-insensitive exact-name lookup as a possible
bounded read-only Exists implementation, potentially usable provisionally. No
public implementation was qualified; approved closure defers it with the whole
item convenience surface.

The original roadmap's candidate GiveItem result was boolean plus optional error. The final
distinction between malformed-input errors and operational failure results must
be documented and tested when the ownership strategy is selected. Do not copy an
example that reports success without checking an operational failure result.

## Ownership gate D13

The required guarantee is one ownership outcome on every path after item creation:
transfer to Rust/player ownership or deterministic host-appropriate cleanup.

The inspected factory has this sequence:

1. Allocate/pool an Item and hold it in a local variable.
2. Set fields and call Item.Initialize, which allocates a UID and runs item-mod callbacks.
3. Return the Item reference only after initialization and logging finish.

There is **no exception handler in Create** around this sequence. If an
initialization callback throws, the normal caller's assignment never happens.
A catch/finally around `Owned = ItemManager.Create(...)` cannot clean up that
unreturned object. Null-return failure handling does not cover this path.
This is a static failure-path finding, not a claim that normal rock creation leaked
in a live run. No failure injection or real item grant has been performed.

Cleanup has a related problem: Item.Remove invokes item-mod callbacks before
putting the object on Rust's removal queue. Assuming Remove always completes,
retrying it blindly, invoking DoRemove directly, or flushing the global removal
queue does not establish correct once-only item-mod/held-entity/pool cleanup.

### Approved adapter investigation result

**No supported, maintainable every-path adapter was established. D13 remains
blocked.** This is a rejection of the investigated paths on the qualified build,
not a theorem that no future Rust API or differently approved product could work.
The findings do not depend on process-wide OOM or a claim that all Carbon plugin
exceptions escape its dispatcher: the inspected host directly invokes virtual
item-mod methods and container delegates without lifecycle compensation.

The public `new Item()` constructor only initializes amount=1, maxCondition=100
and calls Object's constructor. It has no item callback. CarbonLuau could retain
that reference before calling public Initialize, avoiding the factory's outer
reference gap. The factory itself uses this path when pooling is disabled.
The explicit IPooled.LeavePool implementation is a no-op; EnterPool is a reset,
not an abort-initialization/destructor contract. Direct construction is not itself
private reflection, but duplicating the factory's lifecycle is version-sensitive.

The candidate preparation sequence is: resolve an initialized definition; acquire
a fresh Item into a local; set public isServer=true, info, amount=1, skin=0 and
attachment=0; call Initialize(definition). This is **design pseudocode**, not a
replacement factory proven equivalent to all factory policy (including skin
redirection). Initialize assigns `new ItemId(Net.sv.TakeUID())`, sets condition and
maxCondition from info.condition.max, sets radioactivity, initializes ownership
shares if supported, and invokes OnItemCreated/item mods. The constructor alone
does not establish a valid inventory item. UID assignment is separable in code,
but manually assigning one and then calling Initialize would allocate another.

On the inspected Windows Facepunch.Network build, TakeUID advances a monotonic
counter; ReturnUID is a no-op. Do not describe cleanup as recycling IDs. Its ceiling
branch stops the server and sleeps indefinitely; that is a host limitation, not a
controlled adapter failure or the principal reason for rejecting this candidate.
No UID-exhaustion test was run. This dependency was not compared to Linux.

Crucially, retaining the outer Item does not retain every child resource:
`ItemModEntity.CreateEntity` obtains an entity into its own local, sets fields and
calls Spawn **before** SetHeldEntity attaches it to the Item. Spawn invokes virtual
initializers. An exception at that boundary can leave the adapter without the
child reference through the Item. There is no compensating exception handler.
ItemModContainer can similarly allocate/initialize contents. Skipping those mods
or silently accepting only definitions without them is not the approved general
short-name grant and does not repair container insertion failures.

### Candidate ownership state machine

These states describe the attempted adapter, **not an implemented transaction**.

| State / transition | Observable ownership and failure consequence |
|---|---|
| S0: validate before allocation | No new Item; malformed input, stale player, provisional generation or known full inventory can reject without creating one. |
| S1: fresh constructor returns | Adapter has the bare Item reference, no UID/contents/entity yet. No callbacks ran. This closes only the outer reference gap. |
| S2: Initialize in progress | Adapter holds Item and its recorded fields/UID, but mods may hold new resources only in their own locals or publish to the host. Failure can reach unresolved state U1. |
| S3: initialized, unattached | Adapter still has the Item. Acceptance/reservation callbacks run before/during transfer; they are not pure queries guaranteed to preserve destination or item state. Failure requires host-appropriate cleanup, not just dropping the reference. |
| S4: insertion in progress | Container list/parent link is already published before position finding and callbacks finish. Item may be partially linked, invalidly positioned, removed or moved by synchronous callbacks. Failure can reach U2. |
| S5: normal transfer and postconditions complete | Intended success boundary would require exact player/container lifetime, reciprocal membership, valid slot/amount, no scheduled removal/world ownership and completed bookkeeping. This is a candidate success observation, not a proven all-path commit. |
| C: disposal requested | Remove invokes mods, then schedules host removal; DoRemoves later removes its queue entry, calls DoRemove, then pools. Failure can reach U3; scheduling is not completed destruction. |
| U1/U2/U3: unresolved | U1: child resource not reachable through Item; U2: published but incomplete insertion; U3: interrupted mod/container/entity cleanup. Retaining Item, logging or retiring the VM alone does not satisfy cleanup. No safe universal transition out was found. |

**No exact supported commit point exists for this candidate under the required
failure model.** Treating Insert's initial list/parent publication as commit is
too early: position finding can subsequently fail or throw, before bookkeeping.
Treating its return (or MoveToContainer's return) as commit leaves intermediate
failures requiring rollback that itself invokes fallible callbacks. Postcondition
checks cannot reconstruct what a callback did or reverse external effects.

### Insertion, failure and cleanup details

In the inspected base metadata, Insert/Remove/Clear on ItemContainer and DoRemove/
RemoveFromWorld on Item are non-public. SetParent and MoveToContainer are public.
Publicized compilation references, if used elsewhere by Carbon, would not turn
these operations into a supported transactional contract.

- `Insert`: list Add at IL_0022, parent assignment at IL_0029, FindPosition at
  IL_0030, then MarkDirty and onItemAddedRemoved before cycle registration.
  FindPosition calls SlotTaken, which can invoke slotIsReserved. A throwing
  reservation callback after the link yields a partially inserted item even with
  amount=1 and a previously free slot. FindPosition can also return false after
  linking. Direct Insert additionally bypasses normal acceptance checks.
- `Remove`: onPreItemRemove invocation at IL_001d precedes list removal at
  IL_0029. onItemParentChanged and onItemAddedRemoved follow unlinking, before
  remaining bookkeeping. A pre-remove exception can prevent rollback; a later
  exception can leave incomplete bookkeeping. SetParent(null) uses this path.
- `SplitItem` decrements the source amount before CreateByItemID. Stacking in
  MoveToContainer changes destination amount before a callback and before
  decrementing source amount. There is no inspected transaction/undo receipt.
  Reversing amounts by hand cannot reverse ownership migration, hooks, consumed
  items or follow-on transfers. The candidate excludes stacking/splitting rather
  than attempting that rollback.
- `Remove` calls every mod's OnRemove before setting removeTime and queueing
  removal. Retrying after a mid-list exception can repeat earlier callbacks.
  ItemModEntity.OnRemove calls Kill before clearing the held-entity reference;
  Kill itself invokes virtual lifecycle methods without a local cleanup handler.
- `DoRemoves` removes the queue entry before DoRemove. DoRemove clears the UID,
  frees contents, removes world/container links and checks the held entity; it
  does not replace item-mod cleanup, and can itself be interrupted. Calling it
  after queueing risks a second cleanup; directly calling it also needs non-public
  coupling. Flushing a global host queue is not scoped rollback.
- Item IPooled.EnterPool clears info/UID/parent/held/world references and frees
  contents/ownership shares. It does not first perform all entity destruction and
  container unlinking. Container pool reset/Kill ultimately calls Clear, which
  calls child Remove and can stop mid-loop. Pool.Free is therefore not a universal
  cleanup escape hatch. Direct field/list edits, nulling callbacks, manual entity
  killing or retaining unresolved items indefinitely would reproduce/bypass host
  internals, not prove supported exactly-one ownership.

The exact ordinary cleanup path is `Item.Remove(0)` → `ItemManager.RemoveItem` →
host `DoRemoves` → `Item.DoRemove` → optional pool return. **No exact safe failure
cleanup path covers all candidate states. Cleanup itself can fail.** The paired
slot-reservation/pre-remove exceptions above are a static counterexample trace,
not an executed deterministic injection or a reproduced normal-host leak.

### Overflow, reentrancy and compatibility

The candidate would reject a full/unsupported main inventory and would never call
BasePlayer.GiveItem or Item.Drop. The public convenience path and general
MoveToContainer still have the documented drop paths; Item.Drop creates a world
object via CreateWorldObject and removes the item from its container. The inspected
Windows CreateWorldObject creates the generic world prefab, initializes WorldItem,
optionally parents it, calls Spawn, then SetWorldEntity, without exception cleanup.
This is another non-transactional lifecycle, not an alternative disposal path. A no-drop
flag does not exist as a universal ownership transaction. We have not proven that
callbacks cannot introduce additional mutation/drops, and no no-drop public
contract is accepted by this investigation.

Existing I4 and Phase 3 intake remain applicable: synchronous host callbacks must
not enter active Lua. Runtime InsideNative rejects nested entry; FacadeSession
queues bounded event payloads for later drain. Those mechanisms prevent nested
Lua execution, not arbitrary Rust/other-plugin mutation inside transfer. No new
hook subscriptions, callback suppression, host patches or ABI changes were made.
D9–D11 are preserved, not redefined here.

An adapter recreating constructor/init order, mod-specific compensators, private
container bookkeeping, UID/held-entity state and pool teardown would track Rust
internals and other callback behavior, not just adapt a stable grant API. Two
platforms sharing selected IL is not future-version support. A blacklist of mods
or temporary removal of container callbacks is not a maintainable substitute.

### Recommendation and reopening evidence

The investigation recommended **deferring GiveItem from v0.1**, rather than
weakening every-path ownership. The user subsequently approved deferring the
entire item convenience surface and updating the roadmap, as recorded in D13.
No viable candidate emerged, so no runtime prototype was added. Shipping Exists
alone is not part of the approved v0.1 scope.

Reopening requires a supported ownership/abort mechanism covering nested resources,
a precise commit boundary and callback-failure-safe compensation or explicit
completed host ownership for every partial state. A narrower product proposal
must specify its restrictions and retain the same ownership requirement. Before
exposure, deterministic failure injection must cover allocation, UID/init, child
creation, acceptance/insert publication, cleanup and reentrant callbacks, followed
by actual qualified Windows/Linux item-state/lifecycle checks and the affected
canonical regression matrix. Successful ordinary grants alone would not resolve
these counterexamples. No real-client, live grant or rollback evidence is claimed.

## Preserved rules and deferred work

The task explicitly applies D10 to GiveItem: reject provisional grants, allow
post-commit deferred work, never drain failed candidates. D11 player identity and
existing main-thread/non-reentry checks must be reused. A committed grant cannot
be rolled back on script error, reload or timeout; D9 reconstruction is not an
exactly-once mechanism. These requirements do not solve the factory ownership gap.

Installed-script capability is distinct from a command caller's permission. No
extra player permission check is proposed for GiveItem; permission-protected
commands must still reject callers before Lua. No raw item/definition handles may
cross the facade. ABI/export/layout and API version changes have not been made.

General inventory/container/slot/entity APIs, item proxies, item modification,
world drops as a public capability and all Phase 5 work remain excluded.
