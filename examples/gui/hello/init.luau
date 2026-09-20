local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Frame = Screen:Create("Frame")
Frame.Position = UDim2.fromScale(0.5, 0.5)
Frame.AnchorPoint = Vector2.new(0.5, 0.5)
Frame.Size = UDim2.fromOffset(320, 120)
Frame.BackgroundColor3 = Color3.fromRGB(30, 35, 45)

local Label = Frame:Create("TextLabel")
Label.Position = UDim2.fromScale(0.5, 0.5)
Label.AnchorPoint = Vector2.new(0.5, 0.5)
Label.Size = UDim2.fromOffset(280, 40)
Label.Text = "Hello from CarbonLuau"
Label.TextColor3 = Color3.fromRGB(255, 255, 255)

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Screen:Show(Player)
end)
