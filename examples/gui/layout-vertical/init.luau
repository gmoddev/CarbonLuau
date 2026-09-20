local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(280, 220)

local Layout = Panel:Create("UIListLayout")
Layout.FillDirection = "Vertical"
Layout.Padding = UDim.new(0, 8)

for Index = 1, 4 do
    local Row = Panel:Create("TextLabel")
    Row.Size = UDim2.fromOffset(260, 36)
    Row.LayoutOrder = Index
    Row.Text = `Vertical row {Index}`
end

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
