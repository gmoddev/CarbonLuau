local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Viewport = Screen:Create("Frame")
Viewport.Size = UDim2.fromOffset(520, 340)
Viewport.ClipsDescendants = true

local Scroll = Viewport:Create("ScrollingFrame")
Scroll.Size = UDim2.fromScale(1, 1)
Scroll.CanvasSize = UDim2.fromOffset(520, 900)
Scroll.ScrollingDirection = "Y"

local Padding = Scroll:Create("UIPadding")
Padding.PaddingTop = UDim.new(0, 12)
Padding.PaddingLeft = UDim.new(0, 12)

local Grid = Scroll:Create("UIGridLayout")
Grid.CellSize = UDim2.fromOffset(150, 92)
Grid.CellPadding = UDim2.fromOffset(12, 12)
Grid.FillDirectionMaxCells = 3

for Index = 1, 18 do
    local Button = Scroll:Create("TextButton")
    Button.LayoutOrder = Index
    Button.Text = `Entry {Index}`
    Button.Font = (Index % 2 == 0) and GuiFont.DroidSansMono or GuiFont.RobotoCondensedBold
    Button.Activated:Connect(function(Player)
        Button.Font = GuiFont.PermanentMarker
        print(`Entry {Index} activated by {Player.Name}`)
    end)
end

local function Show(Player)
    Screen:Show(Player)
    Scroll:ScrollToTop(Player)
end

for _, Player in Players:GetPlayers() do Show(Player) end
Players.PlayerAdded:Connect(Show)
