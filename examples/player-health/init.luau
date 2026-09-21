local Players = game:GetService("Players")

local function PrintHealth(Player)
    print(Player.Name, Player.Health, Player.MaxHealth)
end

for _, Player in Players:GetPlayers() do
    PrintHealth(Player)
end

Players.PlayerAdded:Connect(PrintHealth)
