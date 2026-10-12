-- Post-v0.7.0 development example: requires the qualified Gameplay D4 build.
-- Published v0.7.0 assets still support only barrel-1. Install one Loot rule
-- in one root/addon; do not install both barrel examples together.
local Loot = game:GetService("Loot")
local Prefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"
local Drops: {LootDropEntry} = {
    {Item = "scrap", Min = 5, Max = 12},
    {Item = "metal.fragments", Min = 10, Max = 25},
}

Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)
-- Only successful publication activates future eligible destruction payouts.
-- Registration is not a payout-success receipt. No post-commit fallback/replay.
