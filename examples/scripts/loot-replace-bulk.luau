-- Post-v0.7.0 development example: requires Gameplay D8's qualified build.
-- Published v0.7.0 does not include this finite bulk item expansion.
-- Use this as a standalone root; D6 publishes both owned keys together.
local Loot = game:GetService("Loot")
local Barrel1 = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"
local Barrel2 = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"

local Drops: {LootDropEntry} = {
    {Item = "sulfur", Min = 50, Max = 100},
    {Item = "sulfur.ore", Min = 25, Max = 50},
    {Item = "gears", Min = 1, Max = 3},
    {Item = "techparts", Min = 1, Max = 2},
    {Item = "lowgradefuel", Min = 10, Max = 25},
    {Item = "crude.oil", Min = 5, Max = 10},
    {Item = "fat.animal", Min = 10, Max = 25},
    {Item = "chocolate", Min = 1, Max = 2},
}

Loot:RegisterDropRule(Barrel1, Enum.DropMode.Replace, Drops)
Loot:RegisterDropRule(Barrel2, Enum.DropMode.Replace, Drops)
-- Amounts must fit the actual definition's stack limit before COMMIT.
-- Both barrels share the eight-occurrence frame budget; a declaration is
-- not a payout-success receipt. Partial failures have no fallback or replay.
