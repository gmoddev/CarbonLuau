-- API 0.5 development example: Experimental; see PersistenceFoundation1C.md for qualification, not package publication.
local Store = game:GetService("DataStoreService"):GetDataStore("PersistenceExamples")

task.defer(function()
    local Accepted, SubmissionError = pcall(function()
        Store:GetAsync("Welcome", function(Value, ErrorCode)
            if ErrorCode ~= nil then
                print("[Persistence:Get] Read failed", ErrorCode)
                return
            end
            if Value == nil then
                print("[Persistence:Get] Key is absent")
            else
                -- false is present data, not an absent value.
                print("[Persistence:Get] Read completed", Value)
            end
        end)
    end)
    if not Accepted then
        print("[Persistence:Get] Submission rejected", SubmissionError)
    end
end)
