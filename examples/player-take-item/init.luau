local Commands = game:GetService("Commands")

Commands:Register("takescrap", {
    permission = "carbonluau.example.takescrap",
}, function(Context)
    local Player = Context.Player
    local Removed = Player:TakeItem("scrap", 100)

    if Removed then
        Player:SendMessage("Removed 100 scrap")
    else
        Player:SendMessage("You need at least 100 scrap")
    end
end)
