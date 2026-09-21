local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Sprite = Screen:Create("ImageLabel")
Sprite.Size = UDim2.fromOffset(96, 96)
Sprite.Image = ImageSource.Sprite("assets/icons/info.png")

local Png = Screen:Create("ImageLabel")
Png.Position = UDim2.fromOffset(108, 0)
Png.Size = UDim2.fromOffset(96, 96)
Png.Image = ImageSource.Png("1234567890")
Png.ImageColor3 = Color3.fromRGB(220, 240, 255)
Png.ImageTransparency = 0.1

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
