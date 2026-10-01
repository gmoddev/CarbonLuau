# DataStoreQuery

Since API `0.5.0-experimental`. A plain-table request for one exact, top-level
field in a private [DataStore](DataStore.md). Query is asynchronous and never
scans the primary store as a fallback.

```luau
type DataStoreQuery = {
    Field: string,
    Type: ("number" | "string" | "boolean")?,
    Equals: (number | string | boolean)?,
    Min: (number | string)?,
    Max: (number | string)?,
    Direction: ("Ascending" | "Descending")?,
    Limit: number?,
    Cursor: string?,
}
```

`Field` is required, exact, case-sensitive, and 1..64 UTF-8 bytes. It is a
literal key, not a nested path. `Equals` excludes `Min`/`Max`; both range bounds
are inclusive. Predicates must use one scalar type without coercion. Numbers
must be finite; string predicates are at most 1,024 UTF-8 bytes. Boolean
supports equality only, including `Equals = false`. `Min > Max` is invalid.

`Direction` defaults to `Ascending`; `Limit` defaults to 50 and accepts integral
values 1..100. A returned `NextCursor` continues the same selection and order;
`Limit` may change. The cursor is opaque and may be invalid after a worker
restart or index replacement. Pages each see current data, not one frozen
snapshot. Only these eight fields are accepted. The plain table has no
metatable. The snapped descriptor is at most 6 KiB; cursor ingress is at most
2 KiB.

`Equals` or numeric/string bounds select their type. For predicate-free order,
an explicit `Type` selects number or string; otherwise CarbonLuau selects the
sole represented scalar type. Mixed represented types return
`AmbiguousFieldType`. A boolean-only field without `Equals` returns
`InvalidQuery`. Missing or wrong-type fields do not match. A complete field
with no represented scalar values returns an empty page.

```luau
Store:Query({ Field = "Level", Min = 10, Max = 20, Limit = 25 }, function(Result, ErrorCode)
    if ErrorCode then
        print("[Persistence:Query] Query failed", ErrorCode)
        return
    end
    for _, Item in Result.Items do
        print(Item.Key)
    end
end)
```

See [Query result](DataStoreQueryResult.md) and the
[persistence examples](../Persistence.md). There is no SQL, arbitrary filter,
schema, migration, offset, or index-management API.
