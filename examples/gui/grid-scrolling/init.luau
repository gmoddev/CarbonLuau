local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Scroll = Screen:Create("ScrollingFrame")
Scroll.Size = UDim2.fromOffset(420, 260)
Scroll.CanvasSize = UDim2.fromOffset(420, 720)
Scroll.ScrollingDirection = "Y"

local Grid = Scroll:Create("UIGridLayout")
Grid.CellSize = UDim2.fromOffset(120, 64)
Grid.CellPadding = UDim2.fromOffset(8, 8)
Grid.FillDirectionMaxCells = 3

for Index = 1, 24 do
    local Cell = Scroll:Create("TextLabel")
    Cell.LayoutOrder = Index
    Cell.Text = `Item {Index}`
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
