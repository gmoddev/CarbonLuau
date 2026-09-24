-- API 0.5 development example: Experimental; 1B scope only, not 1C closure or release approval.
local Store = game:GetService("DataStoreService"):GetDataStore("PersistenceExamples")

local function Submit(Action)
    local Accepted, SubmissionError = pcall(Action)
    if not Accepted then
        print("[Persistence:Snapshot] Submission rejected", SubmissionError)
    end
end

task.defer(function()
    local Original = { Version = 1, Settings = { Theme = "Blue" } }
    Submit(function()
        Store:SetAsync("Snapshot", Original, function(Saved, ErrorCode)
            if ErrorCode ~= nil then
                print("[Persistence:Snapshot] Save failed or uncertain", ErrorCode)
                return
            end
            if Saved ~= true then return end
            print("[Persistence:Snapshot] Save completed")
            Submit(function()
                Store:GetAsync("Snapshot", function(Value, ReadError)
                    if ReadError ~= nil then
                        print("[Persistence:Snapshot] Read failed", ReadError)
                        return
                    end
                    if type(Value) ~= "table" or type(Value.Settings) ~= "table" then
                        print("[Persistence:Snapshot] Missing or unexpected application value")
                        return
                    end
                    print("[Persistence:Snapshot] Saved theme (Blue without another writer)", Value.Settings.Theme)
                    Value.Settings.Theme = "Green"
                    -- Editing this returned graph does not save it.
                    Submit(function()
                        Store:GetAsync("Snapshot", function(Fresh, FreshError)
                            if FreshError ~= nil then
                                print("[Persistence:Snapshot] Fresh read failed", FreshError)
                                return
                            end
                            if type(Fresh) == "table" and type(Fresh.Settings) == "table" then
                                print("[Persistence:Snapshot] Fresh stored theme", Fresh.Settings.Theme)
                            end
                        end)
                    end)
                end)
            end)
        end)
    end)
    -- Set captured Blue before acceptance; this cannot alter that request.
    Original.Settings.Theme = "Red"
end)
