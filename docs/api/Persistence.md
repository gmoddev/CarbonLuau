# Persistence examples and author guide

API `0.5.0-experimental`: **1B implemented and qualified within recorded scope**.
These examples target the new production bindings, not historical 0.4 artifacts
or GUI preview. [Persistence-1B](../PersistenceFoundation1B.md) owns the evidence
and limits. **1C is NOT STARTED**; combined closure and final-source CI remain
separate. These are experimental development examples, not release approval.

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

Read [DataStoreService](Services/DataStoreService.md), [DataStore](Types/DataStore.md)
and [PersistedValue](Types/PersistedValue.md) for signatures and behavior. The
[canonical contract](../PersistenceFoundation1.md) owns detailed limits and
durability assumptions. Its 1,280-MiB allocated-file figure is an operational
safety budget/qualification target, not a hard never-exceeded physical disk quota.

Initialization may acquire a store, but all async calls, including reads, require
committed execution with no active publication. Examples use deferred or event
callbacks. A caught submission error means no acceptance and no callback owed.
A later callback error cannot undo storage. Missing data is not a storage error:
never replace corrupt or unavailable data with a default save.

Local persistence has no shared/cloud backend, queries, key enumeration, atomic
Update/increment or multikey transactions. Generated definitions and examples do
not supply editor storage or prove live Carbon behavior. Preview reports
UnsupportedPreviewApi for DataStoreService; it does not simulate storage success.
