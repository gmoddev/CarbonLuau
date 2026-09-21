local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(320, 120)

local Padding = Panel:Create("UIPadding")
Padding.PaddingTop = UDim.new(0, 12)
Padding.PaddingBottom = UDim.new(0, 12)
Padding.PaddingLeft = UDim.new(0, 20)
Padding.PaddingRight = UDim.new(0, 20)

local Label = Panel:Create("TextLabel")
Label.Size = UDim2.new(1, 0, 1, 0)
Label.Text = "Padded content"

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
