-- Development API 0.6.5-experimental, not published package v0.5.0.
local Players = game:GetService("Players")
local function Inspect(Player)
    local Ok, Result = pcall(function()
        local Inventory = Player:GetInventory()
        return Inventory:CountItem("scrap"), Inventory:HasItem("scrap", 100)
    end)
    if Ok then print("[Example:Inventory] Scrap", Result)
    else print("[Example:Inventory] Unavailable", Result) end
end
Players.PlayerAdded:Connect(Inspect)
for _, Player in Players:GetPlayers() do Inspect(Player) end
