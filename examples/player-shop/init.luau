local Commands = game:GetService("Commands")
Commands:Register("buywood", {
    permission = "carbonluau.example.buywood",
}, function(Context)
    local Player = Context.Player
    -- Separate mutations, NOT an atomic exchange. No automatic refund or retry.
    if not Player:TakeItem("scrap", 100) then
        Player:SendMessage("You need 100 scrap.")
        return
    end
    local Ok, Granted = pcall(function()
        return Player:GiveItem("wood", 1000)
    end)
    if Ok and Granted then
        Player:SendMessage("Granted 1000 wood.")
    else
        -- Payment stays removed, including on an indeterminate grant error.
        Player:SendMessage("Grant failed; payment was not refunded. Contact an operator.")
    end
end)
