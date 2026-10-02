# DataStoreOptions

Since scripting API `0.5.0-experimental`. This optional plain-table argument to
[`DataStoreService:GetDataStore`](../Services/DataStoreService.md) requests private
preparation of exact top-level fields:

```luau
type DataStoreOptions = {
    Indexes: {string}?,
}
```

`Indexes` is an optional dense array of zero to eight distinct strings. Each
field name is case-sensitive, 1..64 UTF-8 bytes, and cannot contain NUL or
malformed UTF-8. Punctuation is literal. No metatable, typed hint, author
schema, index name, version or migration is accepted. Invalid options reject
synchronously; valid hints do not make storage requests or guarantee immediate
preparation. CarbonLuau owns the derived-index lifecycle. Public `Query` is not
implemented in this phase.
