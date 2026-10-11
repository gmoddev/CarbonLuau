# PlayerInventory

Development API `0.6.5-experimental`; not part of published package `0.5.0`.
Obtain it from an exact connected Player:

```luau
local Inventory = Player:GetInventory()
if Inventory:HasItem("scrap", 100) then
    print("[Example:Inventory] Scrap", Inventory:CountItem("scrap"))
end
```

The read methods are `CountItem(ShortName: string): number` and
`HasItem(ShortName: string, Amount: number?): boolean`. HasItem defaults to
one; Amount must be an exact integer 1..2^53-1. Names are canonical lowercase
ASCII, 1..128 bytes, no NUL or silent normalization. Valid unknown names return
zero/false. Reads use the same nonrecursive main/belt/wear physical scanner as
Player:CountItem/HasItem, with the same 128 inspected-stack ceiling and errors.
Counts do not reserve items. This is not a snapshot or transaction.

Acquisition performs no inventory scan or subscription. Reading `ItemChanged`
also creates no sampling demand; sampling starts only for connected listeners.
Repeated acquisition in the same publication scope reuses a live weak-cached
facade while retained;
there is no identity guarantee across intervening publication scopes or domains.
Fields are immutable; ItemAdded, ItemRemoved, mutation aliases and raw
items/containers are not exposed.

Every read validates its original connection and owning domain/publication/VM.
Disconnect/reconnect, owner replacement/provider unload or VM retirement makes
old authority stale; it never reconnects by UserId. A new Player obtains a new
facade. Successful cached module exports can be used later; facade acquisition
and reads are allowed during provisional execution, but a facade created in a
failed module/candidate stays stale. Sharing a live facade preserves its defining
owner. Existing Player methods, including GiveItem/TakeItem, are unchanged.

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
`ItemChanged` and its context have unavailable preview support; a static GUI
preview does not simulate physical inventory changes.

See [Player](Player.md), [Signal](Signal.md),
[C1 facade qualification](../../GameplayC1-Validation.md) and
[C2 private sampled evidence and limitations](../../GameplayC2-Validation.md).
