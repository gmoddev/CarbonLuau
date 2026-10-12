# DropMode

Introduced at API `0.6.5-experimental`, included in published v0.7.0 but not v0.5.0.
The public type `DropMode` has one sealed immutable userdata value:
`Enum.DropMode.Replace`. Its containing namespace and the [Enum](Enum.md)
global are frozen tables.

```luau
local Mode: DropMode = Enum.DropMode.Replace
print(tostring(Mode)) -- DropMode.Replace
```

Pass this value to [Loot:RegisterDropRule](../Services/Loot.md). Strings,
numbers, booleans, tables and unrelated userdata do not substitute for it.
It exposes no fields, constructor or underlying numeric value. Existing
[GiveItemBehavior.InventoryOnly](GiveItemBehavior.md) remains unchanged.

Replace affects only eligible qualified destruction-time payouts, including
stored items, bonus items and bags. It does not change prior fill or reclaim
earlier fill-time drops. Before commit, ineligibility leaves vanilla behavior;
after commit, partial failure has no fallback, replay or compensation.
