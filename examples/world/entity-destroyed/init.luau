-- Development API 0.6.5-experimental; exact qualified host/source only.
local Workspace = game:GetService("Workspace")

Workspace.EntityDestroyed:Connect(function(Context)
    print("destroyed", Context.Id, Context.Prefab)
    if Context.Position then
        print("observed world position", Context.Position)
    else
        print("world position unavailable")
    end
    -- Context is immutable. Its Id must not be used to retarget this lifetime.
end)
