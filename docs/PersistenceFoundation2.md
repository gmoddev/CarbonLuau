# Persistence Foundation 2 — bounded derived indexes and Query

Status: **CANONICAL ARCHITECTURE — AMENDED BEFORE IMPLEMENTATION**, 2026-09-25.

Foundation 1 qualified baseline: 034f28f81c64e0f135ec882fb05de4ef7f33fcf7.
Initial D22 documentation baseline: ee208831efc5647dab553b5f9d0524e7e2e83353.

This document is the detailed authority for
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query).
[D21](Invariants.md#d21--persistence-foundation-1) remains authoritative for
durability, namespaces, Foundation 1 values, the supervised persistence worker,
publication/callback authority, primary-data quotas and PERSIST + EXTRA storage.

This amendment supersedes the initial D22 query-schema design before any Foundation 2
production implementation began. There is no author-managed schema version, required
index declaration, record migration contract or write freeze.

## 1. Public design principle

Authors describe **what they want to query**. CarbonLuau owns the database machinery.

Internal index IDs, generations, format versions, fingerprints, rebuild checkpoints,
quota ledgers, SQLite plans, cursor integrity and corruption repair are derived
CarbonLuau state. They are not concepts an ordinary Luau author must manage.

The common path is intentionally Roblox-like: short, obvious Luau with progressively
available control when an addon actually needs it.

## 2. Dead-simple path

No schema or index declaration is required.

~~~luau
local DataStoreService = game:GetService("DataStoreService")
local Players = DataStoreService:GetDataStore("Players")

Players:SetAsync("76561198000000000", {
    Coins = 150,
    Level = 12,
    Faction = "Blue",
}, function(ok, err)
    -- ...
end)

Players:Query({
    Field = "Coins",
    Direction = "Descending",
    Limit = 25,
}, function(results, err)
    if err then
        return
    end

    for _, item in results.Items do
        print(item.Key, item.Value.Coins)
    end
end)
~~~

CarbonLuau prepares and maintains the bounded derived index needed for Coins.

The existing Foundation 1 call remains unchanged:

~~~luau
DataStoreService:GetDataStore("Players")
~~~

## 3. Optional index hints

GetDataStore gains one optional **options** table, not a schema:

~~~luau
DataStoreService:GetDataStore(Name: string, Options: DataStoreOptions?) -> DataStore
~~~

Untyped hints:

~~~luau
local Players = DataStoreService:GetDataStore("Players", {
    Indexes = {
        "Coins",
        "Level",
        "Faction",
    },
})
~~~

Typed hints are available only when an addon wants an explicit type-specific query
view:

~~~luau
local Players = DataStoreService:GetDataStore("Players", {
    Indexes = {
        Coins = "number",
        Level = "number",
        Faction = "string",
    },
})
~~~

Canonical rules:

- Options is optional.
- Indexes is optional.
- there is **no public Version field**;
- list form means fields with no type preference;
- map form means field -> number, string or boolean;
- at most eight distinct fields may be pinned for one store;
- list and map forms may not be mixed;
- hints are snapshotted pure data;
- hints are optimization/control metadata, not record schemas;
- missing fields do not invalidate SetAsync;
- wrong-type fields do not invalidate SetAsync merely because a hint exists;
- scalar roots, arrays and every other Foundation 1 value remain legal.

Acquisition stays synchronous and disk-free. Hints captured by provisional code do not
create durable state. Preparation begins only after the owning domain commits.

Compatible hints from repeated acquisitions in one domain are combined. Conflicting
typed hints for one field are a programming error. A committed replacement may add,
remove or change hints without a developer-managed migration number.

## 4. Query surface

Foundation 2 adds:

~~~luau
DataStore:Query(Request: DataStoreQuery, Callback: (DataStoreQueryResult?, string?) -> ()) -> ()
~~~

The normal ordered form is:

~~~luau
Players:Query({
    Field = "Coins",
    Direction = "Descending",
    Limit = 25,
}, callback)
~~~

Direction defaults to Ascending.
Limit defaults to 50 and is bounded to 1..100.
Cursor is optional.
Type is optional and normally unnecessary.

## 5. Where comparisons

Query also accepts a deliberately small human-readable Where surface.

~~~luau
Players:Query({
    Where = {
        "Level >= 10",
        "Level <= 20",
    },
    Limit = 50,
}, callback)
~~~

~~~luau
Players:Query({
    Where = {
        'Faction == "Blue"',
    },
    Limit = 50,
}, callback)
~~~

A single clause may be written directly:

~~~luau
Where = "Coins > 100"
~~~

Grammar:

~~~text
Clause   := Field Operator Literal
Operator := == | < | <= | > | >=
Literal  := finite-number | quoted-UTF8-string | true | false
~~~

Bounds and semantics:

- at most two clauses;
- an array means logical AND;
- all clauses name the same field;
- equality appears alone;
- two clauses normalize to one lower and one upper bound;
- booleans support equality only;
- number and string support equality/range operators;
- Field may be omitted when Where identifies it;
- if Field and Where are both present, they must identify the same field;
- the right side is a literal, not another field;
- field-to-field comparisons are deferred;
- OR, NOT, arithmetic, calls, parentheses, regex, substring and arbitrary
  expressions are deferred;
- Where text is parsed by a bounded CarbonLuau parser, never evaluated as Luau;
- raw Where text never becomes SQL.

Thus the intended X > Y shape is supported when Y is the literal operand. A bare
identifier on the right is not treated as another record field.

## 6. Type semantics without a schema

Query examines one top-level map field.

A record participates when the root is a map, the field exists and the value has the
scalar type selected by that query. Missing fields, scalar/array roots and non-scalar
field values simply do not match; they remain valid stored data.

Indexable scalar types are finite binary64 number, UTF-8 string and boolean.

A Where literal selects its type automatically:

- Coins > 100 selects number;
- Faction == "Blue" selects string;
- Enabled == true selects boolean.

For an ordered query without Where, CarbonLuau resolves type from:

1. Request.Type, if supplied;
2. an active typed index hint;
3. the sole scalar type represented by that field index.

If multiple scalar types exist and none selects one, Query returns
AmbiguousFieldType. The uncommon mixed-data case can specify Type explicitly.

No cross-type ordering is invented.

## 7. Automatic index preparation

**Query never falls back to scanning primary records for matches.**

When Query needs a field index that is not ready, CarbonLuau starts or joins bounded
derived-index preparation. The same machinery serves automatic demand and optional
Indexes hints.

A store may have at most eight active or preparing field indexes total. If that bound
is exhausted, a ninth field request fails controlledly rather than scanning.

The author never calls CreateIndex, Migrate, Rebuild or Prepare.

### First-query behavior

A first Query may wait for preparation so the common case stays transparent.

Preparation waiters:

- do not consume D21's 8/namespace or 128/global dispatched request slots;
- retain the normal host/VM/domain/callback authority;
- are capped at 8 per namespace and 32 globally;
- wait no more than 30 seconds;
- consume the query rate token at admission.

If the index becomes ready, the actual Query then executes under D21's ordinary
five-second persistence request deadline and produces one normal callback.

If preparation still legitimately needs work after 30 seconds, the callback returns
IndexPreparing. The internal build may continue; a later Query reuses it.

## 8. Online build: normal writes continue

Preparing an index does **not** freeze GetAsync, SetAsync or RemoveAsync.

A build scans authoritative primary records in bounded key order using a durable
keyset checkpoint and a private shadow generation.

While that build is active, foreground SetAsync/RemoveAsync transactions also update
the shadow generation:

- Set derives the current field entry from the new value;
- Remove removes its building entry;
- a write to a key already passed by the checkpoint corrects that entry;
- a write to a future key is represented immediately and later re-derived from the
  same current primary value;
- deletion before scan means the later scan sees no record.

The existing single worker serializes foreground transactions and build batches, so
they never race transactionally.

GetAsync, SetAsync and RemoveAsync therefore continue normally during index
preparation. Queries using other ready indexes continue normally.

One build batch inspects at most 32 records and at most 256 KiB of primary envelopes,
uses a persistent keyset checkpoint, and is subject to a fixed qualified instruction
budget. Background maintenance yields whenever foreground persistence work is ready.

Crash/restart resumes committed build state. No author's Query is replayed.

## 9. Derived state must not make primary writes fragile

Primary persisted values are authoritative. Query indexes are subordinate derived
state.

Normally Set/Remove and all ready/building index changes occur in the same SQLite
transaction. Query therefore never observes a new primary value with an old active
index entry.

However, an otherwise valid primary mutation must not fail merely because a derived
index has exhausted its separate logical capacity or needs repair.

When the primary mutation itself is still valid, the same transaction may instead:

1. commit primary data and D21 quota changes;
2. atomically withdraw the affected derived index from Query service;
3. mark it stale/rebuild-required;
4. reclaim/rebuild derived rows later through bounded maintenance.

The observable state is never "new value + old active index". It is "new value + that
Query index temporarily unavailable".

Physical SQLite/storage failure remains governed by D21 and can still fail or make the
primary mutation indeterminate.

## 10. Private index model

A generic private derived-index relation is the intended implementation direction:

~~~text
IndexEntries(
    Namespace,
    Store,
    InternalFieldId,
    InternalGeneration,
    SortKey,
    RecordKey,
    PRIMARY KEY(
        Namespace,
        Store,
        InternalFieldId,
        InternalGeneration,
        SortKey,
        RecordKey
    )
) WITHOUT ROWID
~~~

Exact table/schema names are private.

Author field names map to CarbonLuau-owned IDs and never become SQL identifiers.
SortKey contains an internal type tag plus a canonical comparable representation.

Number ordering uses an explicit sortable finite-binary64 encoding, with -0
canonicalized to +0 for query equality/order. Strings use exact case-sensitive UTF-8
bytes with no Unicode normalization or locale collation. Booleans have deterministic
tags and support equality only.

Queryable string literals are bounded to 1,024 UTF-8 bytes. CarbonLuau must never
silently claim a complete string index while omitting a stored value that cannot be
represented by the qualified index format; that query facet becomes unavailable
instead.

## 11. Result, work and pagination bounds

A successful result is:

~~~luau
{
    Items = {
        { Key = "765611...", Value = { ... } },
    },
    NextCursor = "...", -- or nil
}
~~~

Values are fresh Foundation 1 snapshots.

| Resource | Ceiling |
|---|---:|
| Where clauses | 2 |
| One Where clause | 256 UTF-8 bytes |
| Query descriptor | 2 KiB |
| Field name | 64 UTF-8 bytes |
| Queryable string literal | 1,024 UTF-8 bytes |
| Returned items | 100 |
| Default items | 50 |
| Cursor | 512 bytes |
| Encoded response page | 66 KiB |
| Aggregate expanded value entries | 8,192 |
| Candidate index rows | at most 101 |
| Primary point lookups | at most 101 |
| SQLite VM backstop | 1,000,000 VDBE instructions |

The worker stops before adding an item that would exceed page/decode limits and
returns a cursor. It never returns a partial record.

The 66 KiB page ceiling preserves Foundation 1's 68 KiB IPC frame after protocol
overhead.

Numeric OFFSET is not supported. Pagination uses an opaque keyset cursor bound
internally to store authority, index generation, type, normalized predicate,
direction and last sort/key position. Cursor internals are versioned and
integrity-protected and expose no SQL/rowid/path.

Each page is consistent at its own execution time. There is no frozen multi-page
snapshot guarantee.

## 12. Ordering and query-plan proof

Ordering is deterministic:

1. canonical selected field value;
2. exact record key as tie-breaker.

Ascending uses both ascending; Descending reverses both.

Every public Query must resolve to a complete active derived index and a fixed prepared
seek/range statement. No public field name, Where fragment, operator or ordering text is
concatenated into SQL.

Persistence-2C qualification must run EXPLAIN QUERY PLAN against the exact bundled
SQLite build and prove that every public query form performs bounded derived-index
access without primary full scans or temporary arbitrary sorting.

Maintenance scans used to build/rebuild an index are separate bounded CarbonLuau-owned
work, never a Query fallback.

## 13. Queue, fairness and lifecycle

Ready Query work reuses D21:

- 8 pending per namespace;
- 128 globally;
- per-namespace FIFO;
- fair cross-namespace dispatch;
- five-second request deadline;
- later owner-thread callback admission;
- stale callback discard on retirement/replacement.

Query additionally consumes a rate bucket of 5/s burst 8 per namespace and 50/s burst
64 globally.

Within one namespace, an accepted Set before a ready Query resolves before that Query
executes, preserving the existing ordering model.

Worker code never enters Luau.

## 14. Optional hints are desired state, not migrations

No author migration version exists.

After successful domain publication CarbonLuau compares current explicit Indexes hints
with the desired pinned set.

Adding Level means prepare Level.
Removing Coins means it may be unpinned/retired in bounded maintenance.
Changing Coins from number to string changes the desired type-specific view.

No action rewrites authoritative primary values and no developer increments a version.

Automatic indexes may remain cached after explicit unpinning, but Foundation 2 does not
silently evict a currently active index to admit a ninth field.

## 15. Derived-state resource accounting

Derived indexes consume real resources but their accounting is internal runtime policy,
not an author schema model.

Foundation 2 retains a separate logical derived-state pool:

- 16 MiB per namespace;
- 64 MiB globally;
- active and preparing generations count;
- internal index metadata counts.

D21 primary-data quotas remain unchanged. Query indexes therefore do not silently
shrink the addon's user-data quota.

D21's 1,280 MiB filesystem-qualified operational budget and 512 MiB database/page
ceiling remain unchanged. Persistence-2A must prove the derived-state bounds fit that
existing physical envelope on qualified Windows/Linux storage profiles or stop for an
architecture amendment.

## 16. Corruption and repair

Primary/physical SQLite corruption remains D21 fail-closed behavior.

If CarbonLuau can independently establish that primary storage is trustworthy but a
derived index is logically inconsistent, it may atomically quarantine that index,
preserve normal primary operations, and rebuild the derived state online.

There is no Luau RebuildIndex API.

If internal authority/generation metadata itself cannot be trusted, affected Query
state fails closed. Physical database corruption is never downgraded to "index-only"
merely because the failing page was expected to contain derived rows.

## 17. SQL/security boundary

Luau cannot supply SQL, table/column/index identifiers, WHERE/ORDER BY fragments,
SQLite collations/operators, row IDs, offsets or plan hints.

Where strings are parsed into a fixed internal predicate representation. Only canonical
bound values reach prepared SQL parameters.

The single supervised persistence worker remains the only database owner.

SQLite transaction atomicity, serialized writes, BLOB comparison and WITHOUT ROWID
B-tree behavior are implementation primitives, not public semantics; the exact pinned
SQLite 3.53.4 build still requires 2A/2C/2D qualification.

## 18. Diagnostics and tooling seam

Bounded operator diagnostics may expose aggregate counts/status for active/preparing/
stale indexes, automatic versus pinned fields, build progress, waiter saturation,
query rejections, work-limit rejections, derived bytes and rebuilds.

They do not expose values, keys, Where literals, SQL, cursor payloads or unbounded
request history.

Future API metadata must represent GetDataStore(Name, Options?), optional Indexes,
Query, Field, Type, Where, Direction, Limit, Cursor and result/error shapes.

Persistence preview simulation remains deferred.

## 19. Release identity

Foundation 2 remains additive to the still-unpublished persistence surface and joins
scripting API **0.5.0-experimental**.

This amendment changes no package/tag/release, native ABI by itself, provider protocol,
addon package schema or Luau pin.

No Foundation 2 metadata/API becomes available until its implementation phases qualify.

## 20. Implementation routing

### Persistence-2A — private derived-index substrate

- private backend format upgrade;
- generic derived index storage;
- canonical sortable values/type tags;
- internal field/generation metadata;
- derived-state quota ledger;
- online shadow build + checkpoints;
- concurrent Set/Remove dual-write;
- atomic index withdrawal without primary-write failure;
- crash/reopen and exact-pinned storage/planner qualification.

### Persistence-2B — automatic demand and optional hints

- GetDataStore(Name, Options?) publication semantics;
- explicit hint union/conflict rules;
- automatic field demand;
- eight-field store ceiling;
- bounded preparation waiters;
- online build fairness;
- typed/untyped field completeness;
- replacement/reload/restart behavior.

### Persistence-2C — public Query

- request validation;
- bounded Where parser;
- Field/Type inference;
- fixed equality/range/top-N prepared seeks;
- result/cursor encoding;
- work limits;
- exact-pinned EXPLAIN QUERY PLAN assertions;
- metadata and author docs.

### Persistence-2D — combined closure

- Windows/Linux live qualification;
- concurrent write/build stress;
- crash during build/publication/index maintenance;
- lifecycle/stale waiter/cursor tests;
- max-page/max-index/query-rate scale;
- derived capacity/invalidation/rebuild matrix;
- physical-allocation requalification;
- final release-candidate audit.

No phase is implemented by this amendment.

## 21. Canonical decision summary

| Decision | Amended result |
|---|---|
| Basic Query | no index declaration required |
| Index declarations | optional prewarm/pin hints only |
| Public schema Version | none |
| Record schema enforcement | none |
| Existing Foundation 1 stores | unchanged |
| Index types | number, string, boolean |
| Missing/wrong-type fields | do not match selected typed query; Set remains valid |
| Derived fields per store | maximum 8 active/preparing |
| Compound/unique indexes | deferred |
| Key Query | deferred; GetAsync remains exact-key API |
| Index creation | automatic bounded preparation |
| Writes during build | continue with building-index dual-write |
| Reads during build | Get and other ready Query continue |
| Index evolution | compare desired hints/demand directly; no migration version |
| Query predicates | Where with ==, <, <=, >, >= |
| Multiple predicates | at most two AND bounds on one field |
| Where field-to-field | deferred |
| Ordering | canonical field value then record key |
| Results | Items + optional NextCursor |
| Count | default 50, max 100 |
| Page | max 66 KiB and 8,192 expanded entries |
| Query work | <=101 candidates/lookups + VDBE backstop |
| Pagination | opaque keyset cursor; no OFFSET |
| Ready request deadline | D21 five seconds |
| Preparation wait | <=30 seconds, separately bounded |
| Query fallback scan | forbidden |
| Primary/index consistency | update atomically or withdraw index atomically |
| Corrupt derived index | quarantine + bounded rebuild |
| SQL boundary | bounded parser -> fixed predicate -> prepared bound SQL |
| Tooling preview | deferred |
| API identity | 0.5.0-experimental |
| Production implementation | not started |

## 22. Deferred features

Deferred unless separately authorized:

- author-managed schema/migration versions;
- general record schemas and required fields;
- UpdateAsync;
- compound/unique indexes;
- nested field paths;
- field-to-field comparisons;
- OR/NOT/general expression trees;
- prefix/substring/regex/full-text/geospatial search;
- arbitrary scans/enumeration;
- arbitrary SQL;
- joins;
- cross-store/cross-namespace Query;
- aggregates;
- arbitrary sorting/collations;
- OFFSET;
- frozen multi-page snapshots;
- TTL/cloud replication/live watchers;
- persistence preview simulation.

## 23. Stop conditions

Return for architecture amendment instead of weakening D22 if:

- automatic Query cannot remain index-backed without scan fallback;
- online build + concurrent writes cannot prove correct publication;
- primary writes would have to fail solely to preserve a derived index;
- an inconsistent index cannot be withdrawn atomically;
- preparation waiters cannot preserve lifecycle/callback authority within bounds;
- result memory cannot remain below transport/VM envelopes;
- exact pinned plans cannot prove bounded index access;
- derived-state limits cannot fit D21's physical envelope;
- crash recovery can expose a partial index as active;
- Where would require executing author text or arbitrary SQL generation.

## 24. Verdict

The developer-facing model is:

1. save ordinary Foundation 1 values;
2. ask Query what field/range/order you want;
3. optionally hint important indexes early;
4. CarbonLuau owns everything else.

**CANONICAL BASELINE READY — AMENDED BEFORE PERSISTENCE-2A.**

No production Persistence Foundation 2 implementation began under either the initial
D22 design or this amendment.
