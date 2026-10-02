-- v0.5 experimental example: inclusive numeric range.
local Store = game:GetService("DataStoreService"):GetDataStore("QueryExamples")

task.defer(function()
    Store:Query({ Field = "Level", Min = 10, Max = 20, Direction = "Ascending" }, function(Result, ErrorCode)
        if ErrorCode then
            print("[Persistence:Query] Range unavailable", ErrorCode)
            return
        end
        for _, Item in Result.Items do
            print("[Persistence:Query] Level 10..20", Item.Key, Item.Value.Level)
        end
    end)
end)
