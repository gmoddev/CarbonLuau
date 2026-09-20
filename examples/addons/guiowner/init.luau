local Players = game:GetService("Players")
local Panel = require("api")

for _, Player in Players:GetPlayers() do
    Panel.Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Panel.Screen:Show(Player)
end)

return true
