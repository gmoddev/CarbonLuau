local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Scroll = Screen:Create("ScrollingFrame")
Scroll.Size = UDim2.fromOffset(320, 280)
Scroll.CanvasSize = UDim2.fromOffset(320, 720)
Scroll.ScrollingDirection = "Y"

local Layout = Scroll:Create("UIListLayout")
Layout.Padding = UDim.new(0, 8)
local Padding = Scroll:Create("UIPadding")
Padding.PaddingLeft = UDim.new(0, 12)
Padding.PaddingRight = UDim.new(0, 12)

for Index = 1, 16 do
    local Row = Scroll:Create("TextLabel")
    Row.LayoutOrder = Index
    Row.Size = UDim2.fromOffset(280, 34)
    Row.Text = `Scrollable row {Index}`
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
