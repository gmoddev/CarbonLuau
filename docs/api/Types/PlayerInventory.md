# PlayerInventory

Available in published package v0.7.0 / API `0.7.0-experimental`, not v0.5.0.
Acquisition, inventory reads and ItemChanged retain their `0.6.5-experimental`
introduction metadata. New `PlayerInventory:GiveItem` / `PlayerInventory:TakeItem` methods
are introduced in `0.7.0-experimental`. See the [migration guide](../Inventory-Migration.md).
Obtain it from an exact connected Player:

```luau
local Inventory = Player:GetInventory()
if Inventory:HasItem("scrap", 100) then
    print("[Example:Inventory] Scrap", Inventory:CountItem("scrap"))
end
```

| Signature / property | Return | Behavior |
|---|---|---|
| `Inventory:CountItem(ShortName: string)` | number | Checked physical quantity across top-level main/belt/wear stacks. |
| `Inventory:HasItem(ShortName: string, Amount: number?)` | boolean | Whether the quantity reaches Amount; omitted/nil Amount is 1. |
| `Inventory:GiveItem(ShortName: string, Amount: number, Behavior: GiveItemBehavior?)` | boolean | Verified complete inventory delivery; omitted/nil Behavior is InventoryOnly. Committed execution only. |
| `Inventory:TakeItem(ShortName: string, Amount: number)` | boolean | Verified exact physical removal. Committed execution only. |
| `Inventory.ItemChanged` | Signal `(InventoryChangeContext) -> ()` | Deferred sampled net-change observations. |

## CountItem and HasItem

HasItem defaults to
one; Amount must be an exact integer 1..2^53-1. Names are canonical lowercase
ASCII, 1..128 bytes, no NUL or silent normalization. Valid unknown names return
zero/false. Reads use the existing nonrecursive main/belt/wear physical scanner,
with the same 128 inspected-stack ceiling and errors.
Counts do not reserve items. This is not a snapshot or transaction.

Only current positive stacks with the exact item definition and reciprocal
container membership count; invalid or removal-pending entries do not. Nested
contents, backpacks, external/plugin storage, world items and corpses are excluded.
The scanner never invokes hook-virtualized count APIs. Exceeding the bound fails
before returning a partial answer; accumulation must remain an exact Luau integer.

## Acquisition and lifetime

Acquisition performs no inventory scan or subscription. Reading `ItemChanged`
also creates no sampling demand; sampling starts only for connected listeners.
Repeated acquisition in the same publication scope reuses a live weak-cached
facade while retained;
there is no identity guarantee across intervening publication scopes or domains.
Fields are immutable. There are no ItemAdded/ItemRemoved Signals, GetItem/GetItems,
RemoveItem, legacy method aliases or raw items/containers.

Every operation validates its original connection and owning domain/publication/VM.
Disconnect/reconnect, owner replacement/provider unload or VM retirement makes
old authority stale; it never reconnects by UserId. A new Player obtains a new
facade. Successful cached module exports can be used later; facade acquisition
and reads are allowed during provisional execution, but a facade created in a
failed module/candidate stays stale. Sharing a live facade preserves its defining
owner. GiveItem/TakeItem retain these checks and the existing shared exact-Player mutation
gate. The removed Player inventory methods are unavailable; use this facade.

## GiveItem

GiveItem requires an exact integer Amount from 1 through `Int32.MaxValue`. Omitted/nil
Behavior selects the sole typed [GiveItemBehavior.InventoryOnly](GiveItemBehavior.md)
value; strings, booleans and unrelated values are errors. Unknown canonical item
names are configuration errors before creation, unlike CountItem/HasItem absence or TakeItem
rejection. Invalid inputs, stale authority and unavailable host operations raise
controlled Luau errors.

PREPARE uses a complete bounded plan for the exact Player's ordinary main/belt/wear
inventory: at most 128 total slots/entries and 128 transfer chunks, compatible
default-item stacks before empty slots. There is no caller-selected container or
slot. Complex/skinned/custom-data stacks and unqualified clothing, belt conflicts,
slot-mask and backpack/parachute placements may be excluded. Multi-stack grants
require the whole request to fit the safe plan; visible Rust UI space alone is
not sufficient.

`false` means definite PREPARE rejection before item creation begins. COMMIT starts
immediately before the first creation call. `true` requires every transfer to
report success, every returned item to reach accepted inventory or terminal stack
consumption, no deliberate/fallback world drop, the original connection still
valid and one fresh physical count `After >= Before + Amount`. CountItem alone cannot
prove success. Callback rejection after creation, partial delivery, failed cleanup
or any uncertain post-COMMIT result raises an error saying inventory may have
changed; it never returns false or partial success.

```luau
local Inventory = Player:GetInventory()
local CallOk, Granted = pcall(function()
    return Inventory:GiveItem("scrap", 10, GiveItemBehavior.InventoryOnly)
end)
if not CallOk then
    print("[Inventory:GiveItem] Call failed; reconcile host state before retrying", Granted)
elseif Granted then
    print("[Inventory:GiveItem] Verified 10 scrap")
else
    print("[Inventory:GiveItem] No grant began")
end
```

## TakeItem

TakeItem requires an exact integer Amount from 1 through `Int32.MaxValue`. A canonical
unknown item or insufficient physical quantity returns `false` in PREPARE before
removal begins. Invalid arguments, stale authority and provisional use are errors.
COMMIT starts immediately before the qualified host removal call. `true` requires
both the host-reported removal to equal Amount and a fresh bounded physical scan
to prove `Before - After == Amount` on the original connection. A mismatch or
other failure after COMMIT is indeterminate and raises a controlled error.

```luau
local Inventory = Player:GetInventory()
local CallOk, Removed = pcall(function()
    return Inventory:TakeItem("scrap", 100)
end)
if not CallOk then
    print("[Inventory:TakeItem] Call failed; reconcile host state before retrying", Removed)
elseif Removed then
    print("[Inventory:TakeItem] Verified removal of 100 scrap")
else
    print("[Inventory:TakeItem] No removal began")
end
```

## Mutation authority and failure

GiveItem/TakeItem reuse D13 PREPARE/COMMIT/VERIFY and one synchronous exact-connection gate
shared across root/addon domains. Recursive same-Player mutation raises an error;
there is no waiting queue. No additional Player permission is required by these
methods; command permissions remain the caller's responsibility.

Both require committed execution. Root/addon candidates, first-load modules and
provisional dependency/shared-module chains cannot mutate, including through
captured facades. Later committed calls to successfully cached exports may mutate.
Use `task.defer` for startup work; failed publication never drains it.

Neither operation promises reservation, rollback, automatic retry or exactly-once
recovery. Separate TakeItem/GiveItem calls are not an atomic exchange; a failed grant does
not refund an earlier payment. Trusted equally privileged host plugins can change
inventory inside callbacks. Observed uncertainty cannot become success. Returned
temporary items remain under supported cleanup/accounting; inaccessible resources
never returned by a failing host call remain in the host failure domain. A later
script error, timeout or VM recovery does not undo or replay physical effects.

ItemChanged retains sampled semantics alongside mutations: GiveItem/TakeItem do not force
an immediate callback, report a cause or guarantee one callback per operation.
Host callbacks never synchronously enter Luau.

## ItemChanged

```luau
local Inventory = Player:GetInventory()
local Connection = Inventory.ItemChanged:Connect(function(Context: InventoryChangeContext)
    print("[Inventory:Sample]", Context.Item, Context.Before, Context.After, Context.Delta)
end)
-- When no longer needed:
-- Connection:Disconnect()
```

The callback receives only an immutable [InventoryChangeContext](InventoryChangeContext.md)
with `Item: string`, `Before: number`, `After: number` and `Delta: number`.
`Before` and `After` are quantities from the previous and current accepted
physical observations of main/belt/wear inventory. `Delta` is `After - Before`;
zero differences produce no callback. No constructor or additional fields exist.

This Signal reports sampled net changes. It does not identify completed pickups,
drops, consumption, transfers or their causes. A transfer or split spanning
samples can appear as a temporary decrease followed by an increase. Balanced
gain/loss between samples can be missed entirely. Two samples bracketing a
completed same-Player transfer or split/merge have zero net change. Sampling is
not an atomic inventory transaction, including under other trusted plugins.

Connecting establishes a future observation cutoff. The first eligible accepted
sample establishes the listener's baseline without reporting existing contents
or changes before registration. There is no historical replay, catch-up or
exactly-once delivery guarantee. Invalid, incomplete or failed snapshots discard
the interval; the next accepted sample establishes a fresh baseline without a
fabricated loss or gain. Queue or budget overload drops observations. Accepted
baselines advance even when delivery cannot be retained; dropped intervals are
never retried or turned into compensating changes.

Callbacks run later through the existing bounded scheduler. Listener, original
D11 Player connection and defining session/domain/publication/VM authority are
revalidated before entry. Disconnecting, retiring the owner or retiring the VM
suppresses pending callbacks. Reconnecting with the same UserId cannot retarget
an old facade or listener; acquire and subscribe through the new Player.
Candidate initialization and first-load modules stage listeners until successful
publication; they do not sample before commit. Failed scopes discard staged
listeners, and failed replacement preserves existing committed listeners.
Sharing a facade keeps its defining owner and does not transfer authority.

Sampling requires an enabled, ready runtime and the shared frame pump initialized
by Entity discovery. `Connect` raises a controlled error when the observation
source or pump is unavailable. A successful inventory read does not establish
Signal availability.

## Sampling bounds

| Resource/work | Ceiling |
|---|---:|
| Watched original Player connections | 64 |
| Listener demands, including staged demands | 128 |
| Complete scans per actual Unity frame | 4 |
| Inspected stacks per scan | 128 |
| New source records per frame | 32 |
| Total pending inventory records across managed buffering and native-held delivery | 256 |
| Retained/scratch scalar accounting envelope | 4 MiB |

These are global inventory limits shared by the root and all addons. Each pending
record has a 2,048-byte policy charge within the 4 MiB envelope, alongside watched
snapshot, listener-demand and scratch charges. Moving a record into deferred
native delivery keeps it charged against the same 256-record total until its
reservation is consumed or destroyed. Cancellation does not free native-held
capacity early.

Scans, fanout and delivery share D23's gameplay event budgets with other gameplay
Signals. Shared budget or queue pressure can lower effective service below these
ceilings. Four scans can inspect at most 512 stacks per frame. Without failed
samples, record saturation or shared-budget contention, four scans per pump can
revisit 64 committed watches in 16 pumps. This is not a wall-time or delivery
guarantee. The 4 MiB envelope is policy accounting, not a
process-memory or GC limit. There are no per-Player sampling timers or unbounded
history queues.

Tooling retains `ReadMock` metadata for acquisition and inventory reads.
GiveItem/TakeItem have unavailable preview support; static preview does not mutate inventory.
`ItemChanged` and its context have unavailable preview support; a static GUI
preview does not simulate physical inventory changes.

See [Player](Player.md), [Signal](Signal.md),
[C1 facade qualification](../../GameplayC1-Validation.md) and
[C2 private sampled evidence and limitations](../../GameplayC2-Validation.md),
[C3 public sampled qualification](../../GameplayC3-Validation.md),
[original grant qualification](../../qualification/PlayerInteraction.md#playerinteractionfoundation1fb-validation)
and [combined mutation closure](../../qualification/PlayerInteraction.md#playerinteractionfoundation1fc).
These historical receipts retain their tested names/versions; the separate C4
migration and exact final-source qualification passed within the scope of
[C4 evidence](../../GameplayC4-Validation.md) and the
[v0.7 release record](../../Release-0.7.0-Validation.md).
