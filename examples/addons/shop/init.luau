local Economy = require("@economy")
local Formatting = require("@economy/formatting")

local Balance = Economy.Add(25)
print(Formatting(Balance))

if addon:IsDependencyAvailable("metrics") then
    local Metrics = require("@metrics")
    Metrics.Count("shop.started")
end

return true
