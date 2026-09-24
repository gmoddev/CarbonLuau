-- API 0.5 development example: Experimental; 1B scope only, not 1C closure or release approval.
local Players = game:GetService("Players")
local Store = game:GetService("DataStoreService"):GetDataStore("PlayerPreferences")

Players.PlayerAdded:Connect(function(Player)
    -- UserId is already a string; never pass it through tonumber.
    local Key = tostring(Player.UserId)
    local Name = Player.Name
    local Accepted, SubmissionError = pcall(function()
        Store:GetAsync(Key, function(Value, ErrorCode)
            if ErrorCode ~= nil then
                print("[Persistence:Player] Read failed", Name, ErrorCode)
                return
            end
            if Value == nil then
                print("[Persistence:Player] No saved preferences", Name)
            else
                print("[Persistence:Player] Preferences read", Name, Value)
            end
            -- Player may have disconnected. Only captured ordinary data is used.
        end)
    end)
    if not Accepted then
        print("[Persistence:Player] Submission rejected", Name, SubmissionError)
    end
end)
