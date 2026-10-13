-- Post-v0.7.0 development example: requires the qualified Gameplay D6 build.
-- Published v0.7.0 has one rule, barrel-1 and scrap/metal.fragments only.
-- One owner per prefab; this root owns both. Do not install another example
-- or addon that declares either target at the same time.
local Loot = game:GetService("Loot")
Loot:RegisterDropRule(
    "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
    Enum.DropMode.Replace,
    {{Item = "scrap", Min = 5, Max = 12}}
)
Loot:RegisterDropRule(
    "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
    Enum.DropMode.Replace,
    {{Item = "wood", Min = 50, Max = 100}}
)
-- Successful initialization publishes this complete owner set. Failure
-- leaves the previous committed set; payouts share the global frame budget.
