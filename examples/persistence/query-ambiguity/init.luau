-- API 0.5 development example: select a type explicitly when a field has mixed scalar types.
local Store = game:GetService("DataStoreService"):GetDataStore("QueryExamples")

task.defer(function()
    Store:Query({ Field = "Score", Direction = "Descending" }, function(Result, ErrorCode)
        if ErrorCode == "AmbiguousFieldType" then
            Store:Query({ Field = "Score", Type = "number", Direction = "Descending" }, function(Numeric, NumericError)
                if NumericError then
                    print("[Persistence:Query] Numeric Score unavailable", NumericError)
                    return
                end
                print("[Persistence:Query] Numeric Score records", #Numeric.Items)
            end)
        elseif ErrorCode then
            print("[Persistence:Query] Score unavailable", ErrorCode)
        else
            print("[Persistence:Query] Unambiguous Score records", #Result.Items)
        end
    end)
end)
