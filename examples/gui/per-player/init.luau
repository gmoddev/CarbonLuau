local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Template = Gui:Create("ScreenGui")
local Label = Template:Create("TextLabel")
Label.Name = "Greeting"
Label.Size = UDim2.fromOffset(280, 36)

local Screens = {}

local function ShowFor(Player)
    local Screen = Template:Clone()
    Screen:FindFirstChild("Greeting").Text = `Hello {Player.Name}`
    Screens[Player.UserId] = Screen
    Screen:Show(Player)
end

for _, Player in Players:GetPlayers() do
    ShowFor(Player)
end

Players.PlayerAdded:Connect(ShowFor)
Players.PlayerRemoving:Connect(function(Player)
    local Screen = Screens[Player.UserId]
    if Screen then
        Screen:Destroy()
        Screens[Player.UserId] = nil
    end
end)
