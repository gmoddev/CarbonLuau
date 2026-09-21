local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Button = Screen:Create("TextButton")
Button.Size = UDim2.fromOffset(320, 64)
Button.Text = "Click to change the shared font"
Button.Font = GuiFont.RobotoCondensedRegular

Button.Activated:Connect(function(Player)
    Button.Font = GuiFont.PermanentMarker
    Button.Text = `Changed by {Player.Name}`
end)

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
