-- Discovery-2B development example; public qualification is recorded separately.
-- Published package v0.5.0 does not include this API.
local Workspace = game:GetService("Workspace")
local Position = Vector3.new(0, 0, 0)
local Radius = 25
local Options = {
    Prefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
    Limit = 32,
}

-- Deferred work runs after successful entry-script publication has committed.
task.defer(function()
    local Accepted, SubmissionError = pcall(function()
        Workspace:GetEntitiesInRadiusAsync(Position, Radius, function(Entities, ErrorCode)
            if ErrorCode ~= nil then
                print("[CarbonLuau:Discovery] Query failed", ErrorCode)
                return
            end
            if #Entities == 0 then
                print("[CarbonLuau:Discovery] No matching entities")
                return
            end

            print("[CarbonLuau:Discovery] Matching entities", #Entities)
            for _, Entity in ipairs(Entities) do
                -- Properties remain live reads and can fail after callback admission.
                local ReadOk, Id, Prefab = pcall(function()
                    return Entity.Id, Entity.Prefab
                end)
                if ReadOk then
                    print("[CarbonLuau:Discovery] Result", Id, Prefab)
                else
                    print("[CarbonLuau:Discovery] Result became unavailable", Id)
                end
            end
        end, Options)
    end)
    if not Accepted then
        print("[CarbonLuau:Discovery] Submission rejected", SubmissionError)
    end
end)
