-- Replace this canonical decimal ID with a current entity ID from this server.
local Workspace = game:GetService("Workspace")
local Ok, Entity = pcall(function()
    return Workspace:GetEntityById("123")
end)
if not Ok then
    print("[CarbonLuau:World] Entity lookup unavailable; check server startup/host qualification")
elseif Entity then
    print("[CarbonLuau:World] " .. Entity.Id .. " " .. Entity.Prefab)
    print("[CarbonLuau:World] position " .. tostring(Entity.Position))
end
