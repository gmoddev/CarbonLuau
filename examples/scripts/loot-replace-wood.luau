-- Post-v0.7.0 development example: requires Gameplay D5's qualified build.
-- Published v0.7.0 supports neither wood nor barrel-2.
-- Install this barrel-2 target in one root/addon. D6 permits disjoint targets
-- independently; other declarations for barrel-2 still conflict.
local Loot = game:GetService("Loot")
local Prefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"
local Drops: {LootDropEntry} = {
    {Item = "wood", Min = 50, Max = 100},
    {Item = "scrap", Min = 5, Max = 12},
}

Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)
-- Publication activates future eligible destruction payouts. Registration
-- is not a payout-success receipt; partial failure has no fallback or replay.
