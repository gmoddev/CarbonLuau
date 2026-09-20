local Panel = require("@guiowner")

Panel.Label.Text = "Updated by guiconsumer"
assert(Panel.Screen.ClassName == "ScreenGui")

return true
