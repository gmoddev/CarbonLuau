-- API 0.5 development example: Experimental; 1B scope only, not 1C closure or release approval.
local Store = game:GetService("DataStoreService"):GetDataStore("PersistenceExamples")

task.defer(function()
    -- Invalid logical key: rejection owes no callback.
    local Accepted, SubmissionError = pcall(function()
        Store:GetAsync("bad/key", function()
            print("[Persistence:Errors] Unexpected callback for a rejected key")
        end)
    end)
    if not Accepted then
        print("[Persistence:Errors] Rejected before acceptance", SubmissionError)
    end

    -- This fixed-value write can fail at submission or report a later outcome.
    local WriteAccepted, WriteSubmissionError = pcall(function()
        Store:SetAsync("FailureAware", { Version = 1, Enabled = false }, function(Saved, ErrorCode)
            if ErrorCode == "Indeterminate" then
                print("[Persistence:Errors] Write may have committed; no automatic retry")
            elseif ErrorCode == "StorageCorrupt" or ErrorCode == "FormatUnsupported" then
                print("[Persistence:Errors] Storage requires operator recovery", ErrorCode)
            elseif ErrorCode ~= nil then
                print("[Persistence:Errors] Write failed", ErrorCode)
            elseif Saved == true then
                print("[Persistence:Errors] Durable save completed")
            end
        end)
    end)
    if not WriteAccepted then
        print("[Persistence:Errors] Write submission rejected", WriteSubmissionError)
    end
end)
