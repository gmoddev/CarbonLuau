local Commands = game:GetService("Commands")
local Items = game:GetService("Items")
assert(Items:Exists("scrap"), "scrap definition unavailable")

-- A repeatable test reward, not a once-per-player entitlement system.
Commands:Register("reward", {
    permission = "carbonluau.example.reward",
}, function(Context)
    if Context.Player:GiveItem("scrap", 10, GiveItemBehavior.InventoryOnly) then
        Context.Player:SendMessage("Granted 10 scrap.")
    else
        Context.Player:SendMessage("No safe inventory capacity for the reward.")
    end
end)
