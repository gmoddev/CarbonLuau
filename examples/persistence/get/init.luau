-- API 0.5 development example: Experimental; 1B scope only, not 1C closure or release approval.
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
