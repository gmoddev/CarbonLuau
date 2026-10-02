-- v0.5 experimental example; see PersistenceFoundation1C.md for qualification and release limits.
local Store = game:GetService("DataStoreService"):GetDataStore("PersistenceExamples")

task.defer(function()
    local Accepted, SubmissionError = pcall(function()
        Store:SetAsync("Welcome", { Version = 1, Enabled = true }, function(Saved, ErrorCode)
            if ErrorCode ~= nil then
                print("[Persistence:Set] Save failed or uncertain", ErrorCode)
                return
            end
            if Saved == true then
                print("[Persistence:Set] Durable save completed")
            end
        end)
    end)
    if not Accepted then
        print("[Persistence:Set] Submission rejected", SubmissionError)
    end
    -- Returning from SetAsync does not confirm a save.
end)
