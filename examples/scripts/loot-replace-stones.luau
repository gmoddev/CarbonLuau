-- Post-v0.7.0 development example: requires Gameplay D7's qualified build.
-- Published v0.7.0 does not support stones or wood.
-- Declare this during root/addon initialization on the exact qualified host.
-- D6 permits a disjoint barrel-2 owner; same-prefab foreign owners conflict.
local Loot = game:GetService("Loot")
local Prefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"
local Drops: {LootDropEntry} = {
    {Item = "stones", Min = 50, Max = 250},
    {Item = "wood", Min = 50, Max = 100},
}

Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)
-- Publication activates future eligible destruction payouts. Registration
-- is not a payout-success receipt; partial failure has no fallback or replay.
