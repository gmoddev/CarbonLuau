-- Development API 0.7.0-experimental; Inventory owns inventory operations.
local Commands = game:GetService("Commands")
Commands:Register("buywood", {
    permission = "carbonluau.example.buywood",
}, function(Context)
    local Player = Context.Player
    local Inventory = Player:GetInventory()
    -- Separate mutations, NOT an atomic exchange. No automatic refund or retry.
    if not Inventory:TakeItem("scrap", 100) then
        Player:SendMessage("You need 100 scrap.")
        return
    end
    local Ok, Granted = pcall(function()
        return Inventory:GiveItem("wood", 1000)
    end)
    if Ok and Granted then
        Player:SendMessage("Granted 1000 wood.")
    else
        -- Payment stays removed, including on an indeterminate grant error.
        Player:SendMessage("Grant failed; payment was not refunded. Contact an operator.")
    end
end)
