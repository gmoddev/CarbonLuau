local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Button = Screen:Create("ImageButton")
Button.Size = UDim2.fromOffset(96, 96)
Button.Image = ImageSource.Sprite("assets/icons/accept.png")

Button.Activated:Connect(function(Player)
    print(`{Player.Name} activated the image button`)
    Button.ImageColor3 = Color3.fromRGB(120, 255, 160)
end)

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
