-- Development API 0.6.5-experimental, qualified current host only.
local Workspace = game:GetService("Workspace")
Workspace.EntitySpawned:Connect(function(Entity)
    local Success, Message = pcall(function()
        print("spawned", Entity.Id, Entity.Prefab, Entity.Position)
    end)
    if not Success then print("entity changed after admission", Message) end
end)
