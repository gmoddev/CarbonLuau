-- API 0.5 development example: highest values first, with record-key tie breaking.
local Store = game:GetService("DataStoreService"):GetDataStore("QueryExamples")

task.defer(function()
    Store:Query({ Field = "Coins", Type = "number", Direction = "Descending", Limit = 10 }, function(Result, ErrorCode)
        if ErrorCode then
            print("[Persistence:Query] Top-N unavailable", ErrorCode)
            return
        end
        for _, Item in Result.Items do
            print("[Persistence:Query] Top coins", Item.Key, Item.Value.Coins)
        end
    end)
end)
