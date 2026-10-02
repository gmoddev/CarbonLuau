-- v0.5 experimental example; see PersistenceFoundation1C.md for qualification and release limits.
local Store = game:GetService("DataStoreService"):GetDataStore("PersistenceExamples")

task.defer(function()
    local Accepted, SubmissionError = pcall(function()
        Store:RemoveAsync("Welcome", function(Removed, ErrorCode)
            if ErrorCode ~= nil then
                print("[Persistence:Remove] Removal failed or uncertain", ErrorCode)
                return
            end
            if Removed == true then
                print("[Persistence:Remove] Key removed")
            elseif Removed == false then
                print("[Persistence:Remove] Key was absent")
            end
        end)
    end)
    if not Accepted then
        print("[Persistence:Remove] Submission rejected", SubmissionError)
    end
end)
