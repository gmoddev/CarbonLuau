# DataStoreQueryResult

Since API `0.5.0-experimental`. A successful `DataStore:Query` callback receives:

```luau
type DataStoreQueryResult = {
    Items: { { Key: string, Value: PersistedValue } },
    NextCursor: string?,
}
```

`Items` are ordered by the selected field value, then exact record key;
Descending reverses both. Equality orders matching record keys. Values are
fresh [Foundation 1 snapshots](PersistedValue.md): editing a returned table
does not persist it. At most 100 items, 8,192 expanded entries and 66 KiB of
encoded result data are returned. A page may stop before `Limit` to stay within
those bounds. When more compatible results exist, `NextCursor` continues after
the last returned item. An empty page has an empty `Items` array and no cursor.

A cursor is authenticated, not encrypted. Treat it as an opaque temporary
continuation; do not parse it or expose it as a durable bookmark. A later page
reads current data, so writes between pages can change membership or order.
