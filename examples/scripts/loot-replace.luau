-- Development API 0.6.5-experimental; eligible host/startup source gates required.
-- To run, use this file as the root init.luau on the qualified development host.
-- Declare during initialization; install this example in only one root/addon.
local Loot = game:GetService("Loot")
local Prefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"
local Drops: {LootDropEntry} = {
    {Item = "scrap", Min = 2, Max = 2},
    {Item = "metal.fragments", Min = 10, Max = 20},
}

Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)
-- Publication activates future eligible destruction payouts. This is not a
-- payout-success receipt; after physical commit there is no fallback or replay.
