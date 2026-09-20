local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
local Layout = Panel:Create("UIListLayout")
Layout.FillDirection = "Vertical"

local Behind = Panel:Create("TextLabel")
Behind.Text = "First geometrically, lower ZIndex"
Behind.LayoutOrder = 10
Behind.ZIndex = 1

local InFront = Panel:Create("TextLabel")
InFront.Text = "Second geometrically, higher ZIndex"
InFront.LayoutOrder = 20
InFront.ZIndex = 10

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
