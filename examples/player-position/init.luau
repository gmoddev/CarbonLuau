local Players = game:GetService("Players")

local function PrintPosition(Player)
    local Position = Player.Position
    print(Player.Name, Position.X, Position.Y, Position.Z, Position.Magnitude)
end

for _, Player in Players:GetPlayers() do
    PrintPosition(Player)
end

Players.PlayerAdded:Connect(PrintPosition)
