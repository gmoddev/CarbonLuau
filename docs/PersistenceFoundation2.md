# Persistence Foundation 2 — bounded query schemas and indexed Query

Status: **CANONICAL ARCHITECTURE — IMPLEMENTATION NOT STARTED**, 2026-09-24.

Starting baseline: `034f28f81c64e0f135ec882fb05de4ef7f33fcf7`, the qualified
Persistence Foundation 1 closure. This document is the detailed design owned by
[D22](Invariants.md#d22--persistence-foundation-2--bounded-query-schemas-and-indexed-query).
[D21](Invariants.md#d21--persistence-foundation-1) remains authoritative for
durability, namespace ownership, Foundation 1 values, worker isolation, request
publication, callback authority, quotas and the PERSIST + EXTRA storage contract.

No production schema/index/Query code, generated API metadata, preview behavior,
package bump, tag or release is authorized by this architecture record.

## 1. Design objective

Foundation 2 adds one capability: efficient bounded queries over explicitly indexed
record fields.

The public design follows CarbonLuau's Roblox-like API direction: ordinary Luau
tables, short named operations, useful defaults and familiar async callbacks.
"Roblox-like" means easy to learn and powerful in normal gameplay code, not Roblox
DataStore compatibility. SQLite plans, physical index generations, rebuild
checkpoints, quota ledgers and lifecycle fencing stay below the facade.

The target shape is intentionally small:

```luau
local DataStoreService = game:GetService("DataStoreService")

local Players = DataStoreService:GetDataStore("Players", {
    Version = 1,
    Indexes = {
        Coins = { Type = "number" },
        Level = { Type = "number" },
        Faction = { Type = "string", Optional = true },
    },
})

Players:Query({
    Index = "Coins",
    Direction = "Descending",
    Limit = 25,
}, function(page, err)
    if err then
        return
    end

    for _, item in page.Items do
        print(item.Key, item.Value.Coins)
    end
end)
```

This example illustrates the adopted shape. Foundation 2 does not add SQL, an ORM,
expression callbacks, promises, synchronous disk access or arbitrary scans.

## 2. Foundation 1 compatibility audit

The qualified Foundation 1 backend is a viable base.

At the starting revision:

- the private database is versioned with `PRAGMA user_version=1`;
- `Records` is private and keyed by `(Namespace, Store, Key)`;
- Set/Remove already perform the primary record and quota mutation inside one
  `BEGIN IMMEDIATE` transaction;
- the worker is single-threaded and serialized behind the existing fair managed
  dispatcher;
- SQLite schema, row layout and representation are not public API;
- values are decoded by the worker, so indexed-field extraction does not require
  Luau entry on the worker;
- the current transport remains hard-bounded to 68 KiB per IPC frame;
- no public promise requires persisted values to remain opaque to CarbonLuau.

Foundation 2 therefore requires a private physical schema upgrade and new bounded
wire operations, but no Foundation 1 public contract blocks indexing. Physical
schema migration from backend format 1 is an implementation task for
Persistence-2A and must remain transactional, crash-qualified and private.

## 3. Public schema declaration

`GetDataStore` gains one additive optional argument:

```luau
DataStoreService:GetDataStore(StoreName: string, Schema: DataStoreQuerySchema?) -> DataStore
```

The original one-argument call is unchanged.

A schema is an ordinary declarative Luau table snapshotted and canonicalized at
acquisition. No constructor is required.

```luau
{
    Version = 1,
    Indexes = {
        Coins = { Type = "number" },
        Level = { Type = "number" },
        Faction = { Type = "string", Optional = true },
    },
}
```

Rules:

- `Version` is an integer in `1..2147483647`.
- `Indexes` contains at most eight entries.
- each entry name is both the top-level record field and public index name;
- names are exact, case-sensitive UTF-8, 1..64 bytes, using the existing logical
  name safety grammar;
- `Type` is exactly `"number"`, `"string"` or `"boolean"`;
- `Optional` defaults to `false`;
- the encoded canonical descriptor is at most 4 KiB;
- Foundation 2 paths have depth 1 only: nested paths are deferred;
- the descriptor is data only. Functions, metatables, callbacks and executable
  index logic are rejected.

This is a **query schema**, not a general ORM schema. It validates the record shape
needed for indexes and leaves all unlisted record fields governed by the existing
`PersistedValue` rules.

Acquisition remains synchronous and disk-free. Supplying a schema during
provisional/cold-module execution is allowed because acquisition only snapshots
the declaration. It does not register, rebuild or mutate durable state.

Within one domain, any number of no-schema acquisitions may coexist with one
canonical schema declaration for a store. Repeating a semantically identical
schema succeeds regardless of Luau table insertion order. Supplying two different
schema declarations for the same store in one domain rejects synchronously.

## 4. Record enforcement

A store with an active query schema is record-oriented.

Every successful `SetAsync` against that durable store, including through an
untyped facade, must store a string-keyed map. Scalar and array roots reject.

For every declared index:

- required field missing -> reject the entire Set;
- optional field missing -> valid record, no entry in that index;
- present field of the wrong type -> reject the entire Set;
- string longer than 256 UTF-8 bytes -> reject the entire Set;
- non-finite numbers remain impossible under Foundation 1;
- malformed records never create a partial set of index entries.

Unlisted fields remain unrestricted within Foundation 1 value/depth/count/byte
limits. `nil` remains absence and is not an indexed value.

Existing schemaless stores remain fully Foundation-1-compatible until a query
schema is explicitly declared by code and a schema-aware operation reaches the
worker. No store is migrated merely because Foundation 2 exists.

## 5. Schema identity

The durable identity is:

```text
(namespace, store, schema-version, canonical-fingerprint)
```

The fingerprint is SHA-256 over a canonical binary schema encoding containing:

1. an internal schema-format tag;
2. `Version`;
3. index entries sorted by exact UTF-8 name bytes;
4. for each index, name, type and Optional flag.

Ordinary Luau table insertion order therefore has no effect.

Schema version is authored persistence state, not addon/package version. Releasing
addon code does not require a schema bump.

Rules:

- same version + same fingerprint -> match;
- same version + different fingerprint -> `SchemaMismatch`;
- lower declared version than active -> `SchemaMismatch`;
- a higher version may be adopted only if every active index definition is
  unchanged and the target is an identical schema or a strict additive superset;
- removing an index, renaming one, changing type, changing Optional semantics or
  otherwise reinterpreting an active index is incompatible in Foundation 2.

A version-only bump with unchanged indexes is allowed. It is a cheap metadata
transition and can also be used as an explicit retry identity after an earlier
failed adoption once data has been repaired.

## 6. Registration and adoption

There is no separate public migration language and no required `AdoptSchema`
method.

The schema supplied to `GetDataStore` is a desired declaration. Durable activation
occurs only from committed execution:

### Empty schemaless store

A first successful `SetAsync` through a schema-bound facade atomically installs the
schema, record, quota changes and index entries in one transaction.

A schema-bound `Query` against an actually empty schemaless store may return an
empty page without materializing durable metadata. `GetAsync` and a missing
`RemoveAsync` likewise remain disk-free with respect to schema state.

### Existing schemaless store

The first schema-aware Query/Set/Remove through a schema-bound facade atomically
creates bounded BUILDING metadata and returns controlled `SchemaBuilding` without
performing the requested Query/Set/Remove. The durable background rebuild then
validates existing primary values and constructs shadow indexes in bounded
worker-owned batches.

### Additive active-schema evolution

A higher compatible schema version behaves the same way. If new indexes are
needed, the triggering schema-aware operation establishes BUILDING and returns
`SchemaBuilding`. A version-only compatible change may publish in the initiating
bounded transaction without a scan.

These transitions are not destructive silent migrations: the declaration/version
is explicit, only no-schema -> schema and additive evolution are admitted, and
incompatible changes reject without modifying durable schema state.

## 7. Rebuild model

Rebuild is CarbonLuau-owned durable maintenance, not replay of the author's
triggering request.

Each building index uses a fresh private internal index ID. Active indexes are
never overwritten in place.

A rebuild batch is bounded by all of:

- at most 32 primary records inspected;
- at most 256 KiB of primary envelopes decoded;
- the normal monotonic worker safety checks;
- a fixed implementation VDBE/instruction budget established in 2A qualification.

The worker scans the store by primary-key keyset position, never OFFSET. Each batch
transaction atomically commits:

- newly derived shadow index entries;
- index logical-byte accounting;
- last processed primary key/checkpoint;
- record/entry counters needed to resume.

Writes are frozen for the affected store while BUILDING. `GetAsync` continues from
primary values. `Query` is unavailable. `SetAsync` and `RemoveAsync` return
controlled `SchemaBuilding` without mutation, regardless of whether the caller
uses a schema-bound or Foundation 1 facade.

The existing worker/fair dispatcher is reused. Background maintenance receives at
most one maintenance batch per scheduling turn and may not run back-to-back while
foreground persistence work is ready. It owns bounded internal maintenance
capacity rather than consuming an unbounded number of public requests.

On restart, committed BUILDING state resumes from its checkpoint. No original
author request is replayed.

## 8. Rebuild completion and failure

After the final batch, one transaction validates the completed build metadata and
publishes the target schema/index mapping atomically.

Until that commit, Query cannot observe the new indexes.

On success:

- the target schema becomes active;
- new queries use the published internal index IDs;
- writes resume under the new validation/index rules;
- obsolete shadow/old derived rows may be reclaimed later in bounded maintenance;
- stale cursors bound to a changed schema/index ID fail.

On failure:

- primary records are unchanged;
- the previous active schema/index mapping remains active, if one existed;
- an initially schemaless store remains schemaless;
- the failed target version/fingerprint is recorded so the same declaration does
  not repeatedly rescan unchanged data;
- temporary build rows are reclaimed in bounded maintenance;
- a later higher schema version may retry after the author repairs data using the
  still-active prior schema or schemaless Foundation 1 surface.

No primary data is deleted or rewritten by schema adoption.

## 9. Private index representation

Foundation 2 uses one generic private index-entry relation rather than creating SQL
columns or SQL index names from script input.

Conceptually:

```text
IndexEntries(
    Namespace,
    Store,
    InternalIndexId,
    SortKey,
    RecordKey,
    PRIMARY KEY(Namespace, Store, InternalIndexId, SortKey, RecordKey)
) WITHOUT ROWID
```

Exact table names remain private.

Schema metadata maps the public index name to a validated type/Optional descriptor
and opaque internal index ID. A rebuild creates a fresh ID, so publication is a
metadata swap rather than in-place exposure of partial rows.

This representation gives one fixed family of prepared statements. Luau field
names and operators are never concatenated into SQL identifiers or fragments.

Query first walks the index B-tree to obtain bounded record keys, then point-reads
the corresponding `Records` envelopes inside the same SQLite read transaction.
It does not need a public SQL join and does not expose rowid.

## 10. Canonical sortable keys

Index comparison semantics do not inherit SQLite type coercion.

All `SortKey` values are BLOBs with a CarbonLuau canonical encoding:

### string

Exact UTF-8 bytes, case-sensitive, no Unicode normalization and no locale
collation. BLOB byte order defines equality/order.

### boolean

One byte: false `0x00`, true `0x01`. Boolean indexes support equality and
unfiltered deterministic ordering; range predicates reject.

### number

Finite IEEE-754 binary64 only. `-0` is canonicalized to `+0` for indexing so Luau
numeric equality and indexed equality agree. The 64-bit representation is
transformed into an 8-byte unsigned big-endian sortable key: negative encodings
are bitwise inverted; non-negative encodings have the sign bit flipped. This
orders finite extremes, normals and subnormals by numeric value while avoiding
SQLite INTEGER/REAL affinity and coercion. NaN and infinities remain invalid.

Ascending string/number/boolean order uses `SortKey`, then exact record-key UTF-8
bytes. Descending reverses both. Ordering never depends on incidental SQLite row
order.

## 11. Index quotas and write amplification

Derived indexes do not consume the existing 16 MiB namespace / 256 MiB global
**user-data** logical quota. Making author data quota change merely because an
index is declared would make usage hard to reason about.

Indexes instead have a separate hard logical pool:

| Resource | Foundation 2 ceiling |
|---|---:|
| Indexes per store / indexed fields per record | 8 |
| Schema canonical bytes | 4 KiB |
| Index/field name | 64 UTF-8 bytes |
| Path depth | 1 |
| Indexed string value | 256 UTF-8 bytes |
| Index entries produced by one record | 8 |
| Schema/index metadata per namespace | 256 KiB |
| Derived-index logical bytes per namespace | 16 MiB |
| Derived-index logical bytes globally | 64 MiB |

One index entry is charged conservatively as:

```text
96 + StoreBytes + RecordKeyBytes + SortKeyBytes
```

The fixed 96-byte charge covers namespace/internal identity and per-entry
bookkeeping without making the public data quota depend on physical SQLite page
geometry. Schema/build metadata is charged by canonical bytes plus fixed bounded
record overhead and is included in the derived-index pool.

Active plus BUILDING/shadow entries count simultaneously. Indexes are therefore
not free, and a rebuild can fail with `QuotaExceeded` without altering the active
schema or primary data.

D21's 1,280 MiB allocated-file operational budget remains unchanged. The 512 MiB
database/page limit and PERSIST journal bound remain D21 storage concerns. 2A must
qualify the worst permitted active+building index workload against those existing
physical limits on Windows and Linux. If the fixed Foundation 2 logical ceilings
cannot fit the D21 physical envelope on the exact pinned backend, implementation
must stop for architecture amendment rather than silently raising D21 limits.

## 12. Atomic Set/Remove maintenance

Once a schema is active, one `SetAsync` transaction performs all of:

1. read/decode the current record;
2. validate the incoming record against active indexed-field rules;
3. compute old/new canonical index keys;
4. update only changed index entries;
5. update the primary record;
6. update Foundation 1 user-data quota accounting;
7. update derived-index quota/counter accounting;
8. commit under the existing D21 PERSIST + EXTRA path.

A successful state never exposes new primary value + old active index or old
primary value + new active index.

`RemoveAsync` reads the existing record, removes all of its active index entries,
removes the primary value and updates both quota ledgers in the same transaction.
Missing Remove creates nothing.

The caller never supplies the old value.

## 13. Public Query API

Foundation 2 adds:

```luau
DataStore:Query(Request: DataStoreQuery, Callback: (DataStoreQueryPage?, string?) -> ()) -> ()
```

It is non-yielding submission with the same accepted/rejected and later
owner-thread callback model as Get/Set/Remove.

A fresh request is:

```luau
{
    Index = "Level",
    AtLeast = 10,
    AtMost = 20,
    Direction = "Ascending",
    Limit = 50,
    Cursor = nil,
}
```

Fields:

- `Index`: required public index name;
- `Equals`: exact equality;
- one lower bound: `GreaterThan` or `AtLeast`;
- one upper bound: `LessThan`or `AtMost`;
- `Direction`: `"Ascending"` (default) or `"Descending"`;
- `Limit`: integer 1..100, default 50;
- `Cursor`: optional opaque continuation string.

Rules:

- `Equals` is mutually exclusive with lower/upper bounds;
- at most one lower and one upper bound may be supplied;
- lower/upper values must have the declared index type;
- boolean indexes accept `Equals` or no predicate, not range bounds;
- string/number indexes support equality, `<`, `<=`, `>`, `>=` and bounded ranges;
- no predicate means bounded ordered traversal of the selected index;
- the request contains exactly one selected index;
- prefix, substring, regex and arbitrary expressions are deferred.

This supports the Foundation 2 use cases:

- leaderboard-like highest/lowest indexed numeric values;
- `Faction == "Blue"`;
- bounded level ranges;
- recent-record ordering when the author explicitly stores/indexes a numeric
  timestamp.

## 14. Query result

Success returns:

```luau
{    Items = {
        { Key = "765611...", Value = { ... } },
        -- ...
    },
    NextCursor = "..." -- or nil
}
```

Values are fresh decoded snapshots with the same semantics as `GetAsync`.
SQLite row IDs/internal index IDs are never exposed.

Hard page bounds:

| Resource | Ceiling |
|---|---:|
| Requested/returned items | 100 |
| Default items | 50 |
| Encoded Query request descriptor | 2 KiB |
| Opaque cursor | 512 bytes |
| Encoded Query response page | 66 KiB |
| Aggregate expanded persisted-value entries in one page | 8,192 |
| Candidate index rows inspected | at most 101 |
| Primary record point-lookups | at most 101 |

The 66 KiB page ceiling intentionally fits beneath Foundation 1's 68 KiB
request/response IPC frame after protocol overhead, including the case where one
returned value approaches the existing 64 KiB envelope maximum. Foundation 2 does
not weaken the Foundation 1 frame bound.

The worker stops before adding an item that would exceed count/byte/expanded-entry
limits and returns `NextCursor`. Because any individual Foundation 1 value and key
fit within the page headroom and any individual value has at most 4,096 expanded
entries, every valid indexed record can make forward progress alone. No partial
record is returned.

## 15. Bounded query execution

Query never scans primary records to discover matches.

For every request:

1. the public index name must resolve to an active declared index;
2. the predicate is normalized to a fixed internal operation;
3. fixed prepared SQL seeks the generic IndexEntries primary-key prefix;
4. keyset bounds select at most `Limit + 1` index entries;
5. at most 101 primary point-lookups fetch the values;
6. all work runs in one SQLite read transaction/snapshot.

Pagination and byte/node stopping are the expected continuation mechanism. A
separate runtime SQLite VM-instruction ceiling of **1,000,000 VDBE instructions**
per public Query is a backstop against planner/regression surprises. Exceeding it
fails the Query with controlled `QueryWorkExceeded`; it does not return a timeout
cursor or partial success.

The existing five-second persistence request deadline remains the wall-clock upper
bound. Deadline failure remains failure, not pagination.

Qualification on the exact pinned SQLite build must assert with
`EXPLAIN QUERY PLAN` that every generated Query statement performs a bounded
`SEARCH` over the intended IndexEntries primary-key prefix and does not introduce a
full table `SCAN` or temporary ORDER BY B-tree. The runtime work ceiling is an
additional defense, not a substitute for plan qualification.

## 16. Pagination and cursors

Numeric OFFSET is not supported.

`NextCursor` is a versioned opaque base64url token containing only bounded
continuation metadata: process/lifetime binding, store identity digest, schema
fingerprint, internal index ID, normalized query fingerprint and last
`(SortKey, RecordKey)` position.

The token is integrity-protected with a per-process secret and validated against
the current facade. It contains no SQL, path, rowid or user value other than the
bounded encoded continuation position needed for keyset traversal.

Consequences:

- cursors do not survive server process restart or CarbonLuau host lifetime change;
- schema publication/index rebuild invalidates affected cursors;
- namespace/store/index/query mismatch returns controlled `InvalidCursor`;
- malformed/oversized cursor rejects before SQLite work;
- `Limit` may change between pages within 1..100; the selected index, predicates
  and direction must remain identical.

Cursors are position-based, not frozen snapshots.

Each Query call is transactionally consistent at its own execution time. Mutations
between pages may cause an updated record to move across the cursor position, so
multi-page traversal may observe inserts, omissions or repeats relative to an
imaginary frozen snapshot. Foundation 2 promises no multi-call snapshot isolation.

## 17. Queue, ordering and fairness

Query uses the existing Foundation 1 public request ledger:

- 8 pending per namespace;
- 128 pending globally;
- same per-namespace FIFO;
- same fair cross-namespace dispatcher;
- same five-second request deadline;
- same later owner-thread completion admission and stale callback discard.

Query also consumes a dedicated internal query-rate token bucket, in addition to
ordinary request tokens:

- namespace: 5/s, burst 8;
- global: 50/s, burst 64.

This protects mutations/Get from a script issuing maximum-page Queries
continuously without creating a second ordering model.

Within one namespace, accepted FIFO remains authoritative. If Set A is accepted
before Query B, B executes after A's resolved worker outcome and sees the
corresponding current state when successful. Requests accepted after B do not
overtake it within that namespace.

## 18. Callback and lifecycle authority

Query completion reuses Foundation 1 unchanged:

- submission requires current committed admission and no active publication scope;
- accepted Query is non-yielding;
- worker never enters Luau;
- completion is later admitted on the owner thread;
- delivery is at most once;
- retirement/replacement/recovery may discard the callback;
- no Query is replayed;
- callback error does not affect storage;
- callback timeout follows the existing VM-fatal policy.

Schema BUILDING maintenance belongs to durable CarbonLuau state. If the triggering
callback/domain disappears after BUILDING was committed, maintenance may continue
because this is not replay of the author's lost operation.

## 19. Corruption behavior

Three cases remain distinct.

### Primary/envelope or physical SQLite corruption

D21 remains authoritative: fail closed, preserve storage/journal, do not default,
delete or overwrite.

### Schema metadata corruption

Fail closed. CarbonLuau cannot safely interpret index authority or validation rules
without trustworthy schema metadata.

### Derived-index logical corruption with intact primary storage

If an index lookup produces a missing record, mismatching canonical indexed value,
invalid accounting, or another derived-index inconsistency while SQLite/primary
integrity remains trustworthy:

- quarantine the store's Query surface;
- preserve primary records;
- Get remains available;
- Set/Remove may continue under active record-schema validation but Query stays
  unavailable and derived indexes are treated as stale;
- record bounded diagnostics;
- require a bounded rebuild from primary values before Query is re-enabled.

Corruption repair is derived-state maintenance, not permission to rewrite primary
values. Physical database corruption is never downgraded to "index-only" merely
because the failing page was expected to hold an index.

## 20. Rebuild after derived-index quarantine

A rebuild uses fresh shadow internal index IDs, the same 32-record/256-KiB batch
bounds and persistent keyset checkpoints as schema adoption.

During the actual rebuild publication window, Set/Remove are frozen for the store;
Get continues and Query remains unavailable. On complete success, one transaction
publishes the rebuilt index mapping and queries resume. On crash, the rebuild
resumes from committed maintenance state. No half-rebuilt index is public.

Operator diagnostics/maintenance may request this rebuild; Foundation 2 does not
add a Luau `RebuildIndex` method.

## 21. SQL and security boundary

Luau can never supply:

- SQL text/fragments;
- column/table/index identifiers;
- WHERE/ORDER BY fragments;
- SQLite operators;
- collations;
- raw offsets or row IDs.

All public requests map to fixed prepared statement families with bound values.
SQLite parameters are used for all author-controlled values. Internal index IDs
are CarbonLuau-generated integers, not raw field names.

The worker remains the only database owner. No owner-thread DB access, no one-
connection-per-query model and no worker Luau entry are introduced.

## 22. SQLite basis

The design relies only on ordinary SQLite behavior available to the pinned
Foundation 1 backend:

- transactions make the primary/index/quota mutation one commit unit;
- PERSIST journal mode changes retained-journal invalidation, not the logical
  transaction boundary;
- BLOB comparisons use bytewise `memcmp`;
- `WITHOUT ROWID` uses the declared primary key as the clustered B-tree;
- prepared statement parameters bind values without accepting SQL fragments;
- `EXPLAIN QUERY PLAN` distinguishes bounded SEARCH from SCAN and is used only in
  qualification tooling, not as public API.

Primary references:

- <https://www.sqlite.org/lang_transaction.html>
- <https://www.sqlite.org/atomiccommit.html>
- <https://www.sqlite.org/datatype3.html>
- <https://www.sqlite.org/withoutrowid.html>
- <https://www.sqlite.org/eqp.html>
- <https://www.sqlite.org/c3ref/bind_blob.html>
- <https://www.sqlite.org/c3ref/progress_handler.html>

SQLite capability does not itself define CarbonLuau semantics. The generic BLOB
sort encoding, public predicates, quotas and cursor behavior above are CarbonLuau
contracts.

## 23. Prototype evidence

An isolated architecture prototype (not repository production code) checked the
canonical number encoding against finite extremes, negatives, subnormals, both
zero signs and positives. Sorting the transformed 8-byte keys matched numeric
ordering after `-0` canonicalization.
A separate in-memory SQLite 3.46.1 prototype of the proposed
`WITHOUT ROWID` composite primary key reported `SEARCH ... USING PRIMARY KEY` for
ascending and descending bounded range/keyset statements. This is feasibility
evidence only. It is **not** qualification of the pinned SQLite 3.53.4 build;
Persistence-2A/2C must rerun plan, work and crash tests against the exact bundled
pin.

## 24. Tooling metadata seam

When implementation reaches public metadata, canonical API metadata must be able
to describe:

- optional `GetDataStore(StoreName, Schema)` schema descriptor;
- schema/index descriptor fields and bounds;
- `DataStore:Query`;
- Query request fields;
- Query page/item/cursor types and errors.

The VS Code preview receives no storage simulation in Foundation 2 architecture.
Tooling may statically validate literal descriptors later, but runtime remains the
authority for dynamically constructed pure-data descriptors.

## 25. Release identity

Foundation 2 is additive to the still-unpublished persistence surface and joins
scripting API **`0.5.0-experimental`**.

Reasons:

- Foundation 1 already moved the development API to 0.5;
- package 0.5.0 has not been published;
- the new surface is additive rather than a breaking revision of shipped 0.5
  behavior;
- a second unreleased API bump would add identity churn without compatibility
  value.

This architecture decision does not change the development package, tag, release,
native ABI, provider protocol, addon schema or Luau pin. Actual Query/schema
metadata remains unavailable until its implementation phase qualifies.

## 26. Implementation routing

### Persistence-2A — private schema/index substrate

- backend physical schema v1 -> v2 upgrade;
- schema/index/build metadata;
- canonical schema/fingerprint codec;
- canonical string/boolean/binary64 sort keys;
- generic IndexEntries storage;
- derived-index quota ledger;
- atomic hidden index maintenance on internal fixtures;
- exact-pinned planner/work/storage amplification prototypes;
- crash/reopen tests for index maintenance and physical upgrade.

No public Query/schema binding yet.

### Persistence-2B — schema binding and adoption

- `GetDataStore(Name, Schema?)` snapshot/freeze;
- durable schema match/conflict state;
- empty-store atomic activation;
- no-schema/additive BUILDING initiation;
- bounded resumable rebuild;
- write freeze/Get availability;
- failure state/retry version behavior;
- lifecycle/restart/replacement qualification.

Still no public Query completion surface until the index substrate and rebuild
semantics pass.

### Persistence-2C — public Query

- Query request validation/predicates;
- fixed prepared seek statements;
- 66 KiB bounded result page;
- page/item decode;
- opaque keyset cursor;
- 1,000,000-instruction work ceiling;
- five-second deadline and query-rate buckets;
- exact-pinned `EXPLAIN QUERY PLAN` assertions;
- metadata/generated definitions and author docs.

### Persistence-2D — combined closure

- Windows/Linux live qualification;
- indexed Set/Remove crash matrix;
- rebuild restart/failure/quota/corruption matrix;
- replacement/VM recovery/stale callback/cursor tests;
- maximum-page/maximum-index scale and write amplification;
- packaging/storage-allocation requalification;
- final documentation/release-candidate audit.

No phase is implemented by this architecture adoption.

## 27. Explicitly deferred features

Foundation 2 does **not** include:

- UpdateAsync/author-defined transactional transforms;
- compound indexes;
- unique indexes;
- nested index paths;
- key as a built-in Query index;
- exact-key Query syntax duplicating GetAsync;
- prefix search;
- substring/full-text/regex/geospatial search;
- arbitrary scans or enumeration;
- arbitrary SQL;
- joins exposed to Luau;
- cross-store or cross-namespace Query;
- COUNT/SUM/AVG or other aggregation;
- user-defined collations;
- arbitrary sort expressions;
- numeric OFFSET;
- multi-call snapshot cursors;
- online zero-downtime incompatible migrations;
- index rename/type/Optional mutation/removal;
- TTL;
- cloud replication;
- live subscriptions/watchers;
- persistence preview simulation.

These may be separate future decisions. SQLite support alone is not authorization.

## 28. Required decision ledger

| # | Decision | Canonical result |
|---:|---|---|
| 1 | API identity | Joins `0.5.0-experimental`; package remains unchanged |
| 2 | Declaration | Optional pure-data schema on `GetDataStore` |
| 3 | Identity/version | Explicit integer version + canonical SHA-256 fingerprint |
| 4 | Enforcement | Record map + declared indexed-field validation; unlisted fields remain Foundation 1 values |
| 5 | Record shape | Query-schema stores use string-keyed map roots |
| 6 | Index types | finite number, UTF-8 string, boolean |
| 7 | Missing fields | required missing rejects; optional missing creates no entry |
| 8 | Maximum fields | 8 declared indexed fields |
| 9 | Maximum indexes | 8 per store |
| 10 | Compound indexes | deferred |
| 11 | Unique indexes | deferred |
| 12 | Built-in key index | deferred; GetAsync remains exact-key API |
| 13 | Registration timing | acquisition disk-free; activation only from committed schema-aware operation |
| 14 | Existing untyped stores | unchanged until explicit schema declaration reaches worker |
| 15 | Adoption | bounded BUILDING state initiated by first schema-aware operation |
| 16 | Rebuild | worker-owned keyset batches, durable checkpoints, shadow index IDs |
| 17 | Writes during rebuild | Set/Remove rejected for the store |
| 18 | Reads during rebuild | Get allowed; Query unavailable |
| 19 | Evolution | identical/version-only or strict additive higher schema only |
| 20 | Incompatible schema | reject without durable replacement |
| 21 | Index quota | separate 16 MiB namespace / 64 MiB global derived pool |
| 22 | Physical budget | D21 1,280 MiB operational budget unchanged; 2A must requalify |
| 23 | Set maintenance | primary/quota/index/schema bookkeeping in one transaction |
| 24 | Remove maintenance | primary/index/quota deletion in one transaction |
| 25 | Query signature | `Query(Request, Callback)` |
| 26 | Predicates | equality, lt/lte/gt/gte, bounded range, unfiltered ordered traversal |
| 27 | Multiple predicates | at most lower+upper bounds on one selected index |
| 28 | Ordering | selected index value then key, exact deterministic asc/desc |
| 29 | Result | `{ Items = {{Key,Value},...}, NextCursor = ...? }` |
| 30 | Result count | default 50, maximum 100 |
| 31 | Result bytes | maximum 66 KiB encoded page + 8,192 aggregate value entries |
| 32 | Work | <=101 index candidates, <=101 primary lookups, <=1,000,000 VDBE instructions |
| 33 | Pagination | yes, opaque keyset cursor; no OFFSET |
| 34 | Cursor | <=512 B, process/store/schema/index/query bound, integrity-protected |
| 35 | Between pages | each call current snapshot; inserts/moves may cause omissions/repeats |
| 36 | Deadline | existing five-second request deadline |
| 37 | Queue/fairness | existing 8/128 FIFO/fair queue + bounded query-rate tokens |
| 38 | Corruption | D21 for primary/physical; quarantine derived logical index corruption |
| 39 | Corrupt-index rebuild | bounded shadow rebuild from primary; no Luau rebuild API |
| 40 | Crash/recovery | active state atomic; BUILDING resumable; no request replay |
| 41 | Diagnostics | bounded schema/index/build/query counters/status, no values/history |
| 42 | SQL boundary | fixed prepared families and bound values only |
| 43 | Tooling | metadata seam reserved; preview simulation deferred |
| 44 | Phases | 2A substrate, 2B binding/rebuild, 2C Query, 2D closure |
| 45 | Deferred Query features | compound/unique/nested/prefix/aggregate/scan/etc. listed above |

## 29. Architecture verdict

The Foundation 1 seam is sufficient and the required boundedness can be expressed
without leaking SQL or introducing a second consistency model.

**CANONICAL BASELINE READY.**

Implementation must still stop rather than claim support if exact-pinned 2A/2C
qualification disproves the fixed plan/search assumptions, physical quotas do not
fit D21's existing storage envelope, the 66 KiB page cannot transport every valid
single record, or crash/rebuild tests cannot preserve active-index atomicity.

No production Persistence Foundation 2 implementation began in this architecture
task.
