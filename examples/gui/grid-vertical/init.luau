local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(440, 280)

local Grid = Panel:Create("UIGridLayout")
Grid.CellSize = UDim2.fromOffset(96, 48)
Grid.CellPadding = UDim2.fromOffset(8, 8)
Grid.FillDirection = "Vertical"
Grid.FillDirectionMaxCells = 4

for Index = 1, 10 do
    local Cell = Panel:Create("TextLabel")
    Cell.LayoutOrder = Index
    Cell.Text = `Vertical {Index}`
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
