local Players = game:GetService("Players")
Players.PlayerAdded:Connect(function(Player)
    if Player.IsConnected then Player:SendMessage("Welcome to the server.") end
end)
Players.PlayerRemoving:Connect(function(Player)
    print("left", Player.Name, Player.UserId)
end)
