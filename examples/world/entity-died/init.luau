--!strict
-- Development API 0.6.5-experimental; source/attribution qualification is separate.
-- This example observes future death snapshots and performs no host mutation.
local Workspace = game:GetService("Workspace")

local Connected, ConnectionOrError = pcall(function()
    return Workspace.EntityDied:Connect(function(Context)
        print("[World:EntityDied] completed death", Context.Id, Context.Prefab)
        if Context.Position then
            print("[World:EntityDied] observed world position", Context.Position)
        end
        if Context.KillerId then
            print("[World:EntityDied] captured actor", Context.KillerId)
        end
        local Killer = Context.Killer
        if Killer then
            local Readable, Name = pcall(function() return Killer.Name end)
            if Readable then print("[World:EntityDied] captured actor name", Name) end
        end
        if Context.DamageType then
            print("[World:EntityDied] captured damage label", Context.DamageType)
        end
        if Context.Weapon then
            print("[World:EntityDied] captured weapon prefab", Context.Weapon)
        end
        -- Frozen snapshots may outlive removal. Id/UserId never reacquire that lifetime.
        -- Actor attribution is qualified only for original synchronous RPC melee.
        -- Other or unknown provenance supplies nil actor fields, never an account lookup.
    end)
end)

if not Connected then
    print("[World:EntityDied] observation unavailable", tostring(ConnectionOrError))
end
