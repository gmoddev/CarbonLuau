-- v0.5 experimental example: bounded keyset pagination, at most three pages here.
local Store = game:GetService("DataStoreService"):GetDataStore("QueryExamples")

local function ReadPage(Cursor, Page)
    local Request = { Field = "Coins", Type = "number", Direction = "Descending", Limit = 10 }
    if Cursor then
        Request.Cursor = Cursor
    end
    Store:Query(Request, function(Result, ErrorCode)
        if ErrorCode then
            print("[Persistence:Query] Page unavailable", ErrorCode)
            return
        end
        for _, Item in Result.Items do
            print("[Persistence:Query] Page", Page, Item.Key)
        end
        if Result.NextCursor and Page < 3 then
            ReadPage(Result.NextCursor, Page + 1)
        end
    end)
end

task.defer(function()
    ReadPage(nil, 1)
end)
