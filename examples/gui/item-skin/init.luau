local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Item = Screen:Create("ImageLabel")
Item.Size = UDim2.fromOffset(128, 128)
Item.Image = ImageSource.Item(-932201673)

local Skin = Screen:Create("ImageLabel")
Skin.Position = UDim2.fromOffset(140, 0)
Skin.Size = UDim2.fromOffset(128, 128)
Skin.Image = ImageSource.Item(-932201673, "12345678901234567890")

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
