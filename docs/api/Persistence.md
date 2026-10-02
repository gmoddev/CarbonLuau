# Persistence examples and author guide

API `0.5.0-experimental`: Foundation 1 is qualified and the structured 2C Query
surface is implemented in development source. These examples target production
bindings, not historical 0.4 artifacts or GUI preview. [Persistence-1C](../PersistenceFoundation1C.md)
records Foundation 1 combined qualification; [2C](../PersistenceFoundation2C.md)
owns Query-specific evidence. These are experimental development examples, not
package publication approval.

Install one example as the root `scripts/init.luau` at a time in a disposable
development installation with storage Ready. The Set, Remove, snapshot and error
examples change named keys in `PersistenceExamples`; the Player example reads
`PlayerPreferences`. As an addon, the same code uses that addon's private namespace.
It does not share the root's data. Reload does not wipe data. These examples are
not a currency ledger, automatic player save system or migration framework.

| Example | Demonstrates |
|---|---|
| [Get](../../examples/persistence/get/init.luau) | Separate submission/storage errors; nil absence preserves false. |
| [Set](../../examples/persistence/set/init.luau) | Fixed-value write after publication; success only in the callback. |
| [Remove](../../examples/persistence/remove/init.luau) | true removed, false absent, nil/error failure. |
| [Player key](../../examples/persistence/player-key/init.luau) | `tostring(Player.UserId)` as an exact string; callback captures ordinary name/key, not Player authority. |
| [Snapshot](../../examples/persistence/snapshot/init.luau) | Mutating the submitted table cannot change the snapshot; editing a Get result does not save. |
| [Errors](../../examples/persistence/errors/init.luau) | Synchronous rejected request versus later operational/indeterminate outcome; no automatic retry or default overwrite. |
| [Query equality](../../examples/persistence/query-equals/init.luau) | Exact scalar equality. |
| [Query range](../../examples/persistence/query-range/init.luau) | Inclusive number range. |
| [Query top-N](../../examples/persistence/query-top/init.luau) | Descending values and deterministic key tie-break. |
| [Query pages](../../examples/persistence/query-pages/init.luau) | Opaque keyset continuation, bounded to three pages in the example. |
| [Query type](../../examples/persistence/query-ambiguity/init.luau) | Resolve mixed-type ambiguity with explicit `Type`. |

Read [DataStoreService](Services/DataStoreService.md), [DataStoreOptions](Types/DataStoreOptions.md), [DataStore](Types/DataStore.md),
[DataStoreQuery](Types/DataStoreQuery.md), [DataStoreQueryResult](Types/DataStoreQueryResult.md)
and [PersistedValue](Types/PersistedValue.md) for signatures and behavior. The
[canonical contract](../PersistenceFoundation1.md) owns detailed limits and
durability assumptions. The qualified private 2A backend has a 2,560-MiB
filesystem-allocation operational budget, not a never-exceeded physical disk
quota. Logical quota is an admission ceiling, not a promise that every mutation
below it will fit within the independent file-extent limits.

Initialization may acquire a store, but all async calls, including reads, require
committed execution with no active publication. Examples use deferred or event
callbacks. A caught submission error means no acceptance and no callback owed.
A later callback error cannot undo storage. Missing data is not a storage error:
never replace corrupt or unavailable data with a default save.

Current local persistence has no shared/cloud backend, key enumeration, atomic
Update/increment or multikey transactions. Generated definitions and examples do
not supply editor storage or prove live Carbon behavior. Preview reports
UnsupportedPreviewApi for DataStoreService; it does not simulate storage success.

Structured Query uses bounded derived indexes, automatic preparation and
opaque keyset cursors. No prior hint is required. `GetDataStore` accepts optional
field-name hints for early preparation. The first Query may finish with
`IndexPreparing`; an accepted call is never silently replayed. Each page reads
current data rather than a frozen multi-page snapshot. There is no arbitrary
SQL, schema, query builder, OFFSET or primary-store scan fallback.
