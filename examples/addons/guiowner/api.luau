local Gui = game:GetService("Gui")

local Screen = Gui:Create("ScreenGui")
local Label = Screen:Create("TextLabel")
Label.Name = "Status"
Label.Size = UDim2.fromOffset(280, 36)
Label.Text = "Owned by guiowner"

return {
    Screen = Screen,
    Label = Label,
}
