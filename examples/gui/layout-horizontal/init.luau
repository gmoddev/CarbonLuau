local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(360, 100)

local Layout = Panel:Create("UIListLayout")
Layout.FillDirection = "Horizontal"
Layout.Padding = UDim.new(0, 8)
Layout.VerticalAlignment = "Center"

for Index = 1, 4 do
    local Cell = Panel:Create("TextLabel")
    Cell.Size = UDim2.fromOffset(76, 48)
    Cell.LayoutOrder = Index
    Cell.Text = tostring(Index)
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
