# GiveItemBehavior

Available in experimental API `0.4.0-experimental`.

`GiveItemBehavior` is a frozen global table containing one immutable typed value:

| Value | Meaning |
|---|---|
| `GiveItemBehavior.InventoryOnly` | Complete delivery into accepted Player inventory; no intentional overflow/drop fallback. |

Used by [Player:GiveItem](Player.md#giveitem). Omitted/nil Behavior selects
InventoryOnly. Strings, booleans, numbers, tables and unrelated value types are
rejected; there is no constructor or raw numeric representation. Values are
usable across addon/module domains like the existing immutable value types.

```lua
local Granted = Player:GiveItem("scrap", 10, GiveItemBehavior.InventoryOnly)
```

The value may be read during provisional initialization, but GiveItem itself
requires committed execution. `DropRemainder` and a boolean DropIfFull are not
exposed. See [Player](Player.md) for bounds, stale-player, capacity, error and
irreversible-effect semantics.
