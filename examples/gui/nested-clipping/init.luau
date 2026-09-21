local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Outer = Screen:Create("Frame")
Outer.Position = UDim2.fromOffset(32, 32)
Outer.Size = UDim2.fromOffset(360, 220)
Outer.ClipsDescendants = true

local Inner = Outer:Create("Frame")
Inner.Position = UDim2.fromOffset(80, 40)
Inner.Size = UDim2.fromOffset(220, 120)
Inner.ClipsDescendants = true

local Overflowing = Inner:Create("TextLabel")
Overflowing.Position = UDim2.fromOffset(150, 70)
Overflowing.Size = UDim2.fromOffset(180, 100)
Overflowing.Text = "Clipped by both frames"

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
