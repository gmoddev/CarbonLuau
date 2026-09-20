local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Template = Gui:Create("ScreenGui")
local Panel = Template:Create("Frame")
Panel.Name = "Panel"
local Layout = Panel:Create("UIListLayout")
local Avatar = Panel:Create("ImageLabel")
Avatar.Name = "Avatar"
local Label = Panel:Create("TextLabel")
Label.Name = "Greeting"

local Screens = {}
local function Show(Player)
    local Screen = Template:Clone()
    local PlayerPanel = Screen:FindFirstChild("Panel")
    PlayerPanel:FindFirstChild("Avatar").Image = ImageSource.SteamAvatar(Player.UserId)
    PlayerPanel:FindFirstChild("Greeting").Text = `Hello {Player.Name}`
    Screens[Player.UserId] = Screen
    Screen:Show(Player)
end

for _, Player in Players:GetPlayers() do Show(Player) end
Players.PlayerAdded:Connect(Show)
Players.PlayerRemoving:Connect(function(Player)
    local Screen = Screens[Player.UserId]
    if Screen then
        Screen:Destroy()
        Screens[Player.UserId] = nil
    end
end)
