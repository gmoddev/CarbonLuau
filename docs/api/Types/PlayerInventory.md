# PlayerInventory

Development API `0.6.5-experimental`; not part of published package `0.5.0`.
Obtain it from an exact connected Player:

```luau
local Inventory = Player:GetInventory()
if Inventory:HasItem("scrap", 100) then
    print("[Example:Inventory] Scrap", Inventory:CountItem("scrap"))
end
```

Only `CountItem(ShortName: string): number` and
`HasItem(ShortName: string, Amount: number?): boolean` exist. HasItem defaults to
one; Amount must be an exact integer 1..2^53-1. Names are canonical lowercase
ASCII, 1..128 bytes, no NUL or silent normalization. Valid unknown names return
zero/false. Reads use the same nonrecursive main/belt/wear physical scanner as
Player:CountItem/HasItem, with the same 128 inspected-stack ceiling and errors.
Counts do not reserve items. This is not a snapshot or transaction.

Acquisition performs no inventory scan or subscription. Repeated acquisition
in the same publication scope reuses a live weak-cached facade while retained;
there is no identity guarantee across intervening publication scopes or domains.
Fields are immutable; ItemAdded,
ItemRemoved, mutation aliases and raw items/containers are not exposed.

Every read validates its original connection and owning domain/publication/VM.
Disconnect/reconnect, owner replacement/provider unload or VM retirement makes
old authority stale; it never reconnects by UserId. A new Player obtains a new
facade. Successful cached module exports can be used later; facade acquisition
and reads are allowed during provisional execution, but a facade created in a
failed module/candidate stays stale. Sharing a live facade preserves its defining
owner. Existing Player methods, including GiveItem/TakeItem, are unchanged.

[Player](Player.md), [qualification](../../GameplayC1-Validation.md).
