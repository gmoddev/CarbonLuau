local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Panel = Screen:Create("Frame")
Panel.Position = UDim2.fromScale(0.5, 0.5)
Panel.AnchorPoint = Vector2.new(0.5, 0.5)
Panel.Size = UDim2.fromOffset(280, 96)

local Layout = Panel:Create("UIListLayout")
Layout.FillDirection = "Horizontal"
Layout.Padding = UDim.new(0, 12)
Layout.HorizontalAlignment = "Center"
Layout.VerticalAlignment = "Center"

local Icon = Panel:Create("ImageLabel")
Icon.Size = UDim2.fromOffset(64, 64)
Icon.Image = ImageSource.Sprite("assets/icons/info.png")

local Avatar = Panel:Create("ImageButton")
Avatar.Size = UDim2.fromOffset(64, 64)
Avatar.Activated:Connect(function(Player)
    Avatar.Image = ImageSource.SteamAvatar(Player.UserId)
end)

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Screen:Show(Player)
end)
