local Players = game:GetService("Players")
local Items = game:GetService("Items")

local function PrintScrap(Player)
    if Items:Exists("scrap") and Player:HasItem("scrap", 100) then
        print(Player.Name, Player:CountItem("scrap"))
    end
end

for _, Player in Players:GetPlayers() do
    PrintScrap(Player)
end

Players.PlayerAdded:Connect(PrintScrap)
