local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Scroll = Screen:Create("ScrollingFrame")
Scroll.CanvasSize = UDim2.fromOffset(320, 640)
local Layout = Scroll:Create("UIListLayout")
Layout.Padding = UDim.new(0, 8)
local Banner = Scroll:Create("ImageLabel")
Banner.Image = ImageSource.Png("1234567890")
local Button = Scroll:Create("ImageButton")
Button.Image = ImageSource.Sprite("assets/icons/accept.png")

Button.Activated:Connect(function(Player)
    Banner.ImageColor3 = Color3.fromRGB(120, 200, 255)
    print(`Shared tree activated by {Player.Name}`)
end)

for _, Player in Players:GetPlayers() do Screen:Show(Player) end
Players.PlayerAdded:Connect(function(Player) Screen:Show(Player) end)
