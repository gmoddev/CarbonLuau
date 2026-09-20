local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Label = Screen:Create("TextLabel")
Label.Size = UDim2.fromOffset(280, 36)
Label.Text = "Shared live GUI"

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Label.Text = `{Player.Name} joined`
    Screen:Show(Player)
end)

Players.PlayerRemoving:Connect(function(Player)
    Label.Text = `{Player.Name} left`
end)
