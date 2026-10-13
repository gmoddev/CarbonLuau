-- Post-v0.7.0 development example: requires the qualified Gameplay D4 build.
-- Published v0.7.0 assets still support only barrel-1.
-- D4/D5 permit one global rule; D6 permits independent owners per prefab.
-- Install this target in one root/addon; other barrel-2 declarations conflict.
local Loot = game:GetService("Loot")
local Prefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"
local Drops: {LootDropEntry} = {
    {Item = "scrap", Min = 5, Max = 12},
    {Item = "metal.fragments", Min = 10, Max = 25},
}

Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)
-- Only successful publication activates future eligible destruction payouts.
-- Registration is not a payout-success receipt. No post-commit fallback/replay.
