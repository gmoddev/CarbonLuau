local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(460, 280)

local Padding = Panel:Create("UIPadding")
Padding.PaddingTop = UDim.new(0, 16)
Padding.PaddingBottom = UDim.new(0, 16)
Padding.PaddingLeft = UDim.new(0, 20)
Padding.PaddingRight = UDim.new(0, 20)

local Grid = Panel:Create("UIGridLayout")
Grid.CellSize = UDim2.fromOffset(92, 52)
Grid.CellPadding = UDim2.fromOffset(10, 10)
Grid.FillDirectionMaxCells = 4

for Index = 1, 8 do
    local Cell = Panel:Create("TextLabel")
    Cell.LayoutOrder = Index
    Cell.Text = `Padded {Index}`
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
