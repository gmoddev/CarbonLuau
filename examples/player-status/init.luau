local Commands = game:GetService("Commands")
Commands:Register("playerstatus", {}, function(Context)
    local Player = Context.Player
    local Position = Player.Position
    Player:SendMessage(`Health: {Player.Health}/{Player.MaxHealth}; position: {Position.X}, {Position.Y}, {Position.Z}`)
end)
