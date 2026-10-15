-- Development API 0.7.5-experimental; absent from published v0.7.0.
-- Constants are ordinary strings; existing declaration and payout limits apply.
local Loot = game:GetService("Loot")
Loot:RegisterDropRule(Prefab.Loot.Barrel1, Enum.DropMode.Replace, {
    {Item = Item.Scrap, Min = 5, Max = 12},
    {Item = "wood", Min = 50, Max = 100},
})
