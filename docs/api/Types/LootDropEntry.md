# LootDropEntry

Development scripting API `0.6.5-experimental`; not in published v0.5.0.
This is an ordinary input table alias, with no constructor or host capability:

```luau
export type LootDropEntry = {Item: string, Min: number, Max: number}
```

Item is `scrap` or `metal.fragments`. Min and Max are inclusive finite integer
bounds, with `0 <= Min <= Max <= 1000`. All three fields are required;
extra fields and metatables are rejected. Min = Max selects a fixed quantity;
zero is allowed.

[Loot:RegisterDropRule](../Services/Loot.md) accepts a plain dense array of
zero through eight entries, indexed from 1 without holes or other keys.
The sum of Max cannot exceed 8000. The declaration copies the records, so later
input edits do not alter the staged rule.

Use a fresh small table literal for each entry and the Drops array. Oversized
preallocated tables raise a controlled Luau resource error even after they are
emptied or cleared to a few fields. The current implementation allows at most
64 allocated array slots and 64 allocated hash slots per input table. This
storage guard is separate from the logical limit of eight entries and exactly
three fields per entry; use a fresh small table instead of a large reused one.

```luau
local Drops: {LootDropEntry} = {
    {Item = "scrap", Min = 2, Max = 2},
    {Item = "metal.fragments", Min = 10, Max = 20},
}
```
