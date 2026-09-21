local Players = game:GetService("Players")
local Screen = game:GetService("Gui"):Create("ScreenGui")
local Button = Screen:Create("TextButton")
Button.Size = UDim2.fromOffset(220, 44)
Button.Position = UDim2.fromScale(0.5, 0.5)
Button.AnchorPoint = Vector2.new(0.5, 0.5)
Button.Text = "Test reward: 10 scrap"
Button.Activated:Connect(function(Player)
    if not Player:HasPermission("carbonluau.example.reward") then return end
    if Player:GiveItem("scrap", 10) then
        Player:SendMessage("Granted 10 scrap.")
    else
        Player:SendMessage("No safe inventory capacity for the reward.")
    end
end)
for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
