local Workspace = game:GetService("Workspace")
local Id = "18446744073709551615" -- valid UInt64 text; never convert through a Luau number.
local Ok, Entity = pcall(function()
    return Workspace:GetEntityById(Id)
end)
if Ok and Entity then
    print("[CarbonLuau:World] " .. Entity.Prefab)
elseif not Ok then
    print("[CarbonLuau:World] Entity lookup unavailable")
end
