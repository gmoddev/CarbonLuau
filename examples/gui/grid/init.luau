local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(420, 260)

local Padding = Panel:Create("UIPadding")
Padding.PaddingLeft = UDim.new(0, 12)
Padding.PaddingTop = UDim.new(0, 12)

local Grid = Panel:Create("UIGridLayout")
Grid.CellSize = UDim2.fromOffset(96, 48)
Grid.CellPadding = UDim2.fromOffset(8, 8)
Grid.FillDirection = "Horizontal"
Grid.FillDirectionMaxCells = 4
Grid.HorizontalAlignment = "Center"
Grid.VerticalAlignment = "Top"

for Index = 1, 8 do
    local Cell = Panel:Create("TextLabel")
    Cell.LayoutOrder = Index
    Cell.Text = `Cell {Index}`
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
