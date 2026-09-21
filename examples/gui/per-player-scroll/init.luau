local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Scroll = Screen:Create("ScrollingFrame")
Scroll.Size = UDim2.fromOffset(360, 240)
Scroll.CanvasSize = UDim2.fromOffset(360, 960)
Scroll.ScrollingDirection = "Y"

for Index = 1, 12 do
    local Row = Scroll:Create("TextLabel")
    Row.Position = UDim2.fromOffset(12, 12 + (Index - 1) * 72)
    Row.Size = UDim2.fromOffset(320, 56)
    Row.Text = `Row {Index}`
end

local function Show(Player, Position)
    Screen:Show(Player)
    Scroll:ScrollTo(Player, Position)
end

for Index, Player in Players:GetPlayers() do
    Show(Player, Index % 2 == 0 and Vector2.new(0, 1) or Vector2.new(0, 0))
end
Players.PlayerAdded:Connect(function(Player)
    Show(Player, Vector2.new(0, 0.5))
end)
