local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Avatar = Screen:Create("ImageLabel")
Avatar.Size = UDim2.fromOffset(128, 128)

local function Show(Player)
    Avatar.Image = ImageSource.SteamAvatar(Player.UserId)
    Screen:Show(Player)
end

for _, Player in Players:GetPlayers() do Show(Player) end
Players.PlayerAdded:Connect(Show)
