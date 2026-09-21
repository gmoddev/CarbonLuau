local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(420, 220)

local Layout = Panel:Create("UIListLayout")
Layout.Padding = UDim.new(0, 8)

local Fonts = {
    {"Regular", GuiFont.RobotoCondensedRegular},
    {"Bold", GuiFont.RobotoCondensedBold},
    {"Mono", GuiFont.DroidSansMono},
    {"Marker", GuiFont.PermanentMarker},
}

for Index, Entry in Fonts do
    local Label = Panel:Create("TextLabel")
    Label.LayoutOrder = Index
    Label.Size = UDim2.fromOffset(400, 44)
    Label.Text = Entry[1]
    Label.Font = Entry[2]
end

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Screen:Show(Player)
end)
