-- v0.5 experimental example: exact scalar equality. The first query may report IndexPreparing.
local Store = game:GetService("DataStoreService"):GetDataStore("QueryExamples")

task.defer(function()
    Store:Query({ Field = "Faction", Equals = "Blue", Limit = 25 }, function(Result, ErrorCode)
        if ErrorCode then
            print("[Persistence:Query] Equality unavailable", ErrorCode)
            return
        end
        for _, Item in Result.Items do
            print("[Persistence:Query] Blue member", Item.Key)
        end
    end)
end)
