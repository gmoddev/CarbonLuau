local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Scroll = Screen:Create("ScrollingFrame")
Scroll.Size = UDim2.fromOffset(360, 260)
Scroll.CanvasSize = UDim2.fromOffset(720, 900)
Scroll.ScrollingDirection = "XY"

for Index = 1, 12 do
    local Row = Scroll:Create("TextLabel")
    Row.Position = UDim2.fromOffset(20 + (Index % 3) * 220, 20 + Index * 64)
    Row.Size = UDim2.fromOffset(200, 48)
    Row.Text = `Scrollable row {Index}`
end

local function ShowAndCenter(Player)
    Screen:Show(Player)
    Scroll:ScrollTo(Player, Vector2.new(0.5, 0.5))
end

for _, Player in Players:GetPlayers() do
    ShowAndCenter(Player)
end

Players.PlayerAdded:Connect(ShowAndCenter)
