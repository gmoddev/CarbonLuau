# DataStoreService

Since API `0.5.0-experimental`. **Experimental; 1B implemented and qualified within
recorded scope.** The [1B record](../../PersistenceFoundation1B.md) owns evidence
and limits; [1C](../../PersistenceFoundation1C.md) owns combined qualification.
The service is included in the experimental v0.5.0 package. No persistence
exists in historical 0.4 API artifacts, and preview provides no storage backend.

Retrieve with `game:GetService("DataStoreService")` in the owning root/addon.

| Signature | Return | Effect |
|---|---|---|
| `DataStoreService:GetDataStore(StoreName: string, Options: DataStoreOptions?)` | DataStore | Synchronous, disk-free facade acquisition; allowed during candidate/module publication. |

`Options` may be omitted or be a plain table containing only `Indexes`, an
optional dense array of at most eight distinct strings. Each string names an
exact, case-sensitive top-level field (1..64 UTF-8 bytes, no NUL). Punctuation
is literal, not a path or SQL. Metatables, holes, duplicate names, unknown
options and malformed text raise a controlled argument error. For example:

```luau
local DataStoreService = game:GetService("DataStoreService")
local Players = DataStoreService:GetDataStore("Players", {
    Indexes = {"Coins", "Level"},
})
```

These are optional preparation hints, not a schema, uniqueness rule or promise
that an index is immediately usable. Acquiring the facade does no disk I/O or
scan. Repeated acquisitions in one committed generation accumulate the bounded
field union; empty later hints do not erase earlier intent. Provisional and
cold-module hints cannot start maintenance before successful publication, and
failed publications discard their intent. Replacing a domain may reassert a
different hint set without deleting already retained private indexes. There is
no public `Query` method yet. See [DataStoreOptions](../Types/DataStoreOptions.md).

Acquisition does not create persistent rows or confirm storage readiness. A failed
publication stales its newly acquired facade. Repeated acquisition identifies the
same logical store, without promising facade reference equality. The sealed
service and store expose no path, namespace selector, connection or host object.

Store names are exact, case-sensitive, 1..64 UTF-8 bytes, never filenames. No
normalization or coercion occurs. Reject malformed UTF-8, NUL, ASCII controls,
or `/`, `\`, `:` anywhere in a name; also reject the complete names `.` and `..`.
At most 64 distinct store names
may be acquired per domain. Full rules and durable store accounting are in the
[canonical bounds](../../PersistenceFoundation1.md#5-logical-names-and-bounds).

The host selects Root or Addon/stable PackageId. A provider/version/domain change
does not select a new durable namespace. Reassigning a stable package ID grants
access to its retained data under the operator's installed-provider trust model.
Current admitted domain must equal the facade owner at service acquisition and
every store call. Passing a facade or invoking another addon's exported closure
does not transfer authority; foreign use is rejected. Replacement requires fresh
facades and suppresses stale callbacks while retaining stored records.

See [DataStore](../Types/DataStore.md), [PersistedValue](../Types/PersistedValue.md)
and [the examples](../Persistence.md).
