local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Button = Screen:Create("TextButton")
Button.Position = UDim2.fromScale(0.5, 0.5)
Button.AnchorPoint = Vector2.new(0.5, 0.5)
Button.Size = UDim2.fromOffset(220, 44)
Button.Text = "Click"

Button.Activated:Connect(function(Player)
    Button.Text = `Clicked by {Player.Name}`
end)

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Screen:Show(Player)
end)
