local Workspace = game:GetService("Workspace")
local Ok, Equal = pcall(function()
    local First = Workspace:GetEntityById("123")
    local Second = Workspace:GetEntityById("123")
    return First ~= nil and Second ~= nil and First == Second
end)
if Ok then
    print("[CarbonLuau:World] Same exact live lifetime: " .. tostring(Equal))
else
    print("[CarbonLuau:World] Entity lookup unavailable")
end
