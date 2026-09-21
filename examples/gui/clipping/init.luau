local Gui = game:GetService("Gui")

local Screen = Gui:Create("ScreenGui")
local Viewport = Screen:Create("Frame")
Viewport.Name = "Viewport"
Viewport.Position = UDim2.fromOffset(32, 32)
Viewport.Size = UDim2.fromOffset(320, 180)
Viewport.ClipsDescendants = true

local Overflowing = Viewport:Create("Frame")
Overflowing.Name = "Overflowing"
Overflowing.Position = UDim2.fromScale(0.75, 0)
Overflowing.Size = UDim2.fromScale(0.5, 1)

assert(Viewport.ClipsDescendants)
assert(Viewport:FindFirstChild("Overflowing") == Overflowing)
