# Persistence Foundation 2 — bounded derived indexes and Query

Status: **CANONICAL ARCHITECTURE — FINAL CORRECTION BEFORE IMPLEMENTATION**, 2026-09-25.

Foundation 1 qualified baseline: 034f28f81c64e0f135ec882fb05de4ef7f33fcf7.
Initial D22 documentation baseline: ee208831efc5647dab553b5f9d0524e7e2e83353.

This document is the detailed authority for
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query).
[D21](Invariants.md#d21--persistence-foundation-1) remains authoritative for
durability, namespaces, Foundation 1 values, the supervised persistence worker,
publication/callback authority, primary-data quotas and PERSIST + EXTRA storage.

This correction supersedes both the initial D22 query-schema design and the intermediate
comparison-string design before any Foundation 2 production implementation began. There is
no author-managed schema/version, required index declaration, migration contract or write
freeze.

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

GetDataStore gains one optional options table, not a schema:

~~~luau
DataStoreService:GetDataStore(Name: string, Options: DataStoreOptions?) -> DataStore
~~~

The only author-facing hint form in Foundation 2 is:

~~~luau
local Players = DataStoreService:GetDataStore("Players", {
    Indexes = {
        "Coins",
        "Level",
        "Faction",
    },
})
~~~

Canonical rules:

- Options is optional.
- Indexes is optional.
- each entry is an exact top-level field name;
- at most eight distinct fields may be hinted for one store;
- hints are snapshotted pure data;
- hints mean that the addon expects to query those fields and permit proactive bounded preparation/retention;
- hints do not authorize Query and are never required before Query;
- hints do not select a query plan or scalar type;
- hints do not validate values, require fields or reinterpret records;
- every Foundation 1 persisted value remains legal.

Typed index hints are deferred. Query.Type is the escape hatch for actual type ambiguity.

Acquisition stays synchronous and disk-free. Hints captured by provisional code do not create
durable state. Preparation begins only after the owning domain commits.

Changing hints between committed addon generations is desired-state change, not migration.
Adding a hint requests proactive preparation/retention. Removing a hint removes that proactive
intent and permits bounded internal reconciliation. No author version increment or migration
call exists.


## 4. Query surface

Foundation 2 adds:

~~~luau
DataStore:Query(
    Request: DataStoreQuery,
    Callback: (DataStoreQueryResult?, string?) -> ()
) -> ()
~~~

The minimum request shape is:

~~~luau
DataStoreQuery = {
    Field: string,
    Type: ("number" | "string" | "boolean")?,
    Equals: (number | string | boolean)?,
    Min: (number | string)?,
    Max: (number | string)?,
    Direction: ("Ascending" | "Descending")?,
    Limit: number?,
    Cursor: string?,
}
~~~

The ordinary forms are:

~~~luau
Players:Query({
    Field = "Coins",
    Direction = "Descending",
    Limit = 25,
}, callback)

Players:Query({
    Field = "Faction",
    Equals = "Blue",
}, callback)

Players:Query({
    Field = "Level",
    Min = 10,
    Max = 20,
}, callback)
~~~

Direction defaults to Ascending.
Limit defaults to 50 and is bounded to 1..100.
Cursor is optional.
Type is optional and normally unnecessary.

## 5. Structured comparison semantics

Canonical rules:

- Field is required.
- Equals is mutually exclusive with Min and Max.
- Min and Max may be used independently or together.
- Min is inclusive.
- Max is inclusive.
- exclusive bounds are deferred;
- booleans support equality only;
- contradictory or mixed-type requests reject instead of coercing;
- explicit Type must agree with Equals/Min/Max values;
- Min greater than Max is invalid under the selected number/string ordering;
- numbers in the request must be finite;
- strings in the request must satisfy the bounded queryable-string rule;
- Cursor continues the same compatible logical query.

Foundation 2 exposes no comparison text, executable filter, query builder, SQL fragment or
expression tree. Internal normalization into fixed CarbonLuau-owned query forms is private.

## 6. Type semantics without a schema

Query examines one top-level map field.

A record participates when the root is a map, the field exists and the field value has the
scalar type selected by that Query. Missing fields, scalar/array roots, non-scalar field values
and values of another scalar type simply do not participate; they remain valid stored data.

Indexable scalar types are finite binary64 number, UTF-8 string and boolean.

Equals selects its type directly. Numeric Min/Max select number. String Min/Max select string.
Type is ordinarily unnecessary.

For an ordered Query with no Equals/Min/Max, CarbonLuau may automatically prepare the field
and discover represented usable scalar types:

1. explicit Request.Type selects that type when supplied;
2. if exactly one usable scalar type exists, use it;
3. if more than one usable scalar type exists, fail controlledly with AmbiguousFieldType;
4. if no queryable scalar value is represented, a healthy prepared field may return an empty page.

There is no cross-type ordering.

CarbonLuau may maintain separate private number/string/boolean representations for one prepared
field. That internal detail does not consume separate author-visible field slots and is not a
public lifecycle concept.

Foundation 2 never turns Foundation 1 into a schema-enforced record store.


## 7. Automatic index preparation

**Public Query never falls back to scanning authoritative primary records for matches.**

When Query needs derived state that is not ready, CarbonLuau starts or joins bounded
preparation. The same machinery serves automatic first-use demand and optional Indexes hints.

A store may have at most eight active or preparing derived fields total. Automatic and hinted
fields share that ceiling. Private type representations for one field do not consume additional
public field slots.

Foundation 2 performs no pressure-driven/LRU eviction. If the eight-field ceiling is exhausted,
a ninth distinct field request fails controlledly rather than scanning primary data, silently
evicting an existing field or asking the author to manage index slots.

The author never calls CreateIndex, EnsureIndex, PrepareIndex, RebuildIndex or a migration API.

### First-query behavior

~~~text
Query(...)
    -> suitable active derived state exists: execute
    -> otherwise: begin/join bounded preparation
    -> preparation completes during waiter lifetime: execute original Query once
    -> otherwise: complete once with IndexPreparing
~~~

Preparation waiters:

- do not occupy D21's 8-per-namespace / 128-global dispatched persistence slots while only waiting for maintenance;
- retain the normal host/VM/domain/callback authority;
- are capped at 8 per namespace and 32 globally;
- wait no more than 30 seconds from Query acceptance;
- never cause worker Luau entry.

The separate waiter pool is retained because preparation can legitimately outlive D21's
five-second foreground request lifetime; holding the ordinary foreground slots while waiting
could block unrelated persistence. The 8/32 counts and 30-second lifetime hard-bound retained
callback authority without exposing preparation handles.

If preparation becomes ready in time, the actual Query enters D21's ordinary foreground
request ledger and executes exactly once.

If preparation still legitimately needs work after 30 seconds, the callback completes once
with IndexPreparing. The original Query is finished and is never replayed later. Already
committed CarbonLuau-owned preparation may continue independently, and a later Query may
benefit from it.

D21's at-most-once completion, stale-completion discard and VM/domain lifetime rules remain
authoritative.

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

Primary persisted values are authoritative. Query indexes are subordinate derived state.

Normally Set/Remove and all active/building derived changes occur in the same SQLite
transaction.

For a successful primary Set/Remove affecting ACTIVE derived state, the committed transaction
must end as either:

~~~text
new primary state + corresponding correct ACTIVE derived state
~~~

or:

~~~text
new primary state + affected derived state atomically not ACTIVE
~~~

never:

~~~text
new primary state + stale ACTIVE derived state
~~~

An otherwise valid primary mutation must not fail merely because subordinate derived state has
exhausted its separate logical capacity, become unmaintainable or needs repair.

When the primary mutation itself is provably safe, the same transaction may commit the primary
mutation and D21 accounting while atomically withdrawing the affected derived state from Query
service and marking it for bounded repair/rebuild.

Physical SQLite/storage failure, uncertain COMMIT outcome and primary corruption remain governed
by D21 and may still fail or make the primary mutation Indeterminate.

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

Queryable strings are bounded to 1,024 UTF-8 bytes. A longer string remains legal
Foundation 1 primary data and does not make SetAsync invalid. CarbonLuau must never claim
complete string Query state while omitting such a value; only the affected string Query
availability is withdrawn and the public outcome is QueryUnavailable.


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

Publicly:

- Limit defaults to 50;
- Limit is at most 100;
- a page may contain fewer items when the bounded response envelope is full;
- when compatible continuation exists, NextCursor is returned.

| Resource | Ceiling |
|---|---:|
| Query descriptor | 2 KiB |
| Field name | 64 UTF-8 bytes |
| Queryable string scalar | 1,024 UTF-8 bytes |
| Returned items | 100 |
| Default items | 50 |
| Cursor | 512 bytes |
| Encoded response page | 66 KiB |
| Aggregate expanded value entries | 8,192 |
| Candidate derived rows | at most 101 |
| Primary point lookups | at most 101 |
| SQLite VM backstop | 1,000,000 VDBE instructions |

The worker stops before adding an item that would exceed page/decode limits and returns a
continuation when compatible results remain. It never returns a partial record.

The 66 KiB page ceiling preserves Foundation 1's 68 KiB IPC frame after protocol overhead.

Numeric OFFSET is not supported. Pagination uses an opaque keyset cursor. Public semantics only
promise that the same compatible logical Query may continue with NextCursor, the cursor is
opaque, and InvalidCursor is reported when it is no longer valid after restart or internal
derived-state replacement.

Each page is consistent at that call's execution time. Pagination is not a frozen multi-page
snapshot; writes between pages may alter membership/order and can cause omissions or repeats
relative to an imaginary frozen snapshot.

Internal cursor format versions, field IDs, generations, sort-key encoding, integrity secrets
and other compatibility metadata remain private.


## 12. Ordering and query-plan proof

Ordering is deterministic:

1. canonical selected field value;
2. exact record key as tie-breaker.

Ascending orders both ascending; Descending reverses both. Equality results therefore have
deterministic record-key ordering. No Query relies on incidental SQLite row order.

Every public Query must resolve to complete healthy derived state and a fixed prepared
seek/range statement family. No public field name, scalar value or ordering choice is
concatenated into SQL syntax.

Persistence-2C qualification must run EXPLAIN QUERY PLAN against the exact bundled SQLite
revision and prove every public statement family performs the intended bounded derived-index
access without primary full scans or temporary arbitrary sorting.

Maintenance scans used to build/rebuild/repair derived state are separate bounded,
resumable CarbonLuau-owned work, never Query execution fallback.


## 13. Queue, fairness and lifecycle

A ready Query reuses D21's existing foreground persistence admission:

- 8 pending per namespace;
- 128 pending globally;
- namespace request rate 20/s, burst 32;
- global request rate 200/s, burst 256;
- per-namespace FIFO;
- fair cross-namespace dispatch;
- five-second accepted-request lifetime;
- retained transport/completion reservations;
- later owner-thread callback admission;
- stale callback discard and replacement fencing.

Query is a read request and does not consume D21's mutation-only bucket.

Foundation 2 adds **no dedicated Query rate bucket**. The superseded bucket had no documented
distinct starvation/resource problem outside D21 queue/rate admission, Query result/work
ceilings, worker deadline/progress controls, fair dispatch and foreground priority over
maintenance. A Query flood is bounded by the same general foreground controls that already
bound a Get flood.

If exact implementation evidence later demonstrates a distinct resource class not bounded by
those mechanisms, D22 must be amended; tuning should remain internal/operator policy where
possible.

Within one namespace, accepted foreground persistence ordering remains authoritative.
Worker code never enters Luau.


## 14. Optional hints are desired state, not migrations

No author schema, schema version, index version or migration version exists.

After successful domain publication CarbonLuau compares the committed string-list Indexes hints
with desired proactive preparation/retention state.

Adding Level requests proactive preparation/retention for Level.
Removing Coins removes that proactive intent and permits bounded internal reconciliation.

Those changes never reinterpret or rewrite authoritative primary values. No public version
increment or migration call exists.

A Query-created field and a hinted field share the same eight-field ceiling. Foundation 2 does
not silently evict an existing field merely to admit a ninth request.

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

Luau cannot supply SQL text/fragments, table/column/index identifiers, ordering fragments,
SQLite collations, row IDs, numeric offsets or plan hints.

Structured Field/Type/Equals/Min/Max/Direction input is validated and normalized by CarbonLuau.
Only canonical bound values and CarbonLuau-owned internal IDs reach fixed prepared SQL
statements.

The single supervised persistence worker remains the only database owner.

SQLite transaction atomicity, serialized writes, byte comparison and WITHOUT ROWID B-tree
behavior are implementation primitives, not public semantics; the exact pinned SQLite 3.53.4
build still requires 2A/2C/2D qualification.


## 18. Diagnostics, public errors and tooling seam

The compact Query-specific public error set is:

~~~text
InvalidQuery
AmbiguousFieldType
IndexPreparing
QueryUnavailable
InvalidCursor
~~~

D21 persistence/backend failures remain authoritative where they already apply. Malformed
request shape, contradictory selection and incompatible explicit types reject synchronously as
InvalidQuery before acceptance where knowable at submission. Accepted work may later complete
through the callback with the other Query-specific codes or an applicable D21 failure.

Internal build/checkpoint/plan/generation/work-limit details map to stable public outcomes and
bounded operator diagnostics.

Diagnostics may expose aggregate counts/status for active/preparing/unavailable derived fields,
automatic versus hinted fields, build progress, waiter saturation, query rejections,
work-limit rejections, derived bytes and rebuilds. They do not expose values, keys, SQL, cursor
payloads or unbounded request history.

Future API metadata must represent GetDataStore(Name, Options?), the string-list Indexes hint,
Query, Field, Type, Equals, Min, Max, Direction, Limit, Cursor and result/error shapes.

There is no comparison-text parser to implement or mirror in tooling.
Persistence preview simulation remains deferred.


## 19. Release identity

Foundation 2 remains additive to scripting API **0.5.0-experimental**.

The package remains 0.4.0 and no 0.5.0 package/tag/release is created by this correction.
release.json continues to separate package 0.4.0 from development scripting API
0.5.0-experimental.

There is no separate public Query/index/schema version. CarbonLuau owns storage/index/cursor
format versions internally.

This correction changes no native ABI by itself, provider protocol, addon package schema or
Luau pin. No Foundation 2 metadata/API becomes available until the applicable implementation
phase qualifies.


## 20. Implementation routing

### Persistence-2A — private derived-index substrate

- private backend format upgrade;
- generic derived index storage and private sortable values;
- internal field/generation metadata;
- separate derived-state quota/accounting;
- active/building publication state;
- bounded resumable online preparation;
- foreground Set/Remove maintenance of building and active state;
- atomic withdrawal of affected active state;
- crash/reopen and exact-pinned storage/write-amplification qualification.

No public Query binding is implemented in 2A.

### Persistence-2B — automatic demand and optional hints

- GetDataStore(Name, Options?) publication semantics;
- simple string-list Indexes hints;
- automatic field demand;
- eight-field store ceiling;
- bounded preparation waiters;
- online build scheduling/fairness;
- field type discovery/completeness;
- replacement/reload/restart desired-state behavior.

### Persistence-2C — public structured Query

- request validation;
- Equals/Min/Max and type inference;
- AmbiguousFieldType behavior;
- deterministic order;
- bounded result pages;
- opaque keyset cursor;
- exact work limits;
- exact-pinned EXPLAIN QUERY PLAN assertions;
- metadata/generated definitions and author docs.

### Persistence-2D — combined closure

- Windows/Linux live qualification;
- concurrent build/write stress;
- crash during build/publication/active maintenance;
- replacement/VM recovery/stale callback/waiter/cursor matrix;
- derived quota/resource convergence;
- physical-allocation requalification;
- final API/release-readiness audit.

No phase is implemented by this correction.


## 21. Canonical decision summary

| Decision | Final canonical result |
|---|---|
| Basic Query | no declaration required |
| Optional hints | Indexes = { "Coins", "Level" } string-list only |
| Typed hints | deferred |
| Public schema/version | none |
| Record schema enforcement | none |
| Query signature | Query(Request, Callback) |
| Request selection | required Field; optional Type/Equals/Min/Max/Direction/Limit/Cursor |
| Equality | structured Equals |
| Range | structured Min/Max; independently optional; both inclusive |
| Exclusive bounds | deferred |
| Boolean | equality only |
| Ordered type inference | sole usable scalar type; otherwise explicit Type or AmbiguousFieldType |
| Missing/wrong-type fields | no match; Foundation 1 data remains legal |
| Derived fields per store | maximum 8 active/preparing |
| Index creation | automatic bounded preparation |
| First-query wait | <=30 seconds; 8/ns and 32 global waiters |
| Wait timeout | one IndexPreparing callback; original Query never replayed |
| Writes during build | continue; building state maintained transactionally |
| ACTIVE consistency | update correctly or atomically withdraw affected derived state |
| Primary authority | Foundation 1 persisted values |
| Derived accounting | separate 16 MiB/ns + 64 MiB/global logical pool |
| Results | Items + optional NextCursor |
| Count | default 50, max 100 |
| Page | max 66 KiB and 8,192 expanded entries |
| Query work | <=101 candidates/lookups + <=1,000,000 VDBE instructions |
| Ordering | selected field value then record key; descending reverses both |
| Pagination | opaque keyset cursor; no OFFSET |
| Between pages | each call current snapshot; no frozen multi-page guarantee |
| Queue/fairness | reuse D21 8/128 FIFO/fair foreground ledger |
| Query rate limit | no dedicated bucket; reuse D21 general request admission |
| Number semantics | finite binary64; deterministic order; signed-zero canonicalization |
| String semantics | exact UTF-8 bytes; case-sensitive; no normalization/locale |
| Queryable string bound | 1,024 bytes; longer primary value remains legal |
| Public errors | InvalidQuery, AmbiguousFieldType, IndexPreparing, QueryUnavailable, InvalidCursor |
| Corruption | D21 for primary/physical; derived-only logical state may be withdrawn/rebuilt |
| Query fallback scan | forbidden |
| SQL boundary | fixed prepared statement families; no author SQL/syntax fragments |
| API identity | 0.5.0-experimental; package remains 0.4.0 |
| Production implementation | not started |


## 22. Deferred features

Deferred unless separately authorized:

- UpdateAsync;
- author schemas;
- author versions;
- migration APIs;
- typed index hints;
- exclusive range comparisons;
- compound indexes;
- unique indexes;
- nested paths;
- OR/NOT;
- arbitrary expressions;
- regex/substring/prefix/full-text search;
- aggregates;
- joins;
- arbitrary enumeration/scans;
- key Query where GetAsync already handles exact keys;
- cross-store/cross-namespace Query;
- arbitrary sort expressions;
- numeric OFFSET;
- frozen multi-page snapshots;
- TTL;
- subscriptions/watchers;
- cloud persistence;
- persistence preview simulation.

No speculative public hooks are reserved for them.


## 23. Stop conditions

Return for architecture amendment instead of beginning implementation if:

- structured Equals/Min/Max requires equally complicated public replacement machinery;
- structured Query cannot express equality/range/top-N cleanly;
- automatic preparation forces authors to manage index lifecycle;
- online construction cannot preserve Set/Remove correctness;
- affected active derived state cannot be atomically withdrawn when derived maintenance fails while a primary mutation can still safely commit;
- automatic preparation requires unbounded primary work in one maintenance operation;
- Query would need primary-store scan fallback;
- Query result memory/work cannot remain hard-bounded;
- simple field hints would implicitly become schemas;
- removing author versions makes private derived-state evolution ambiguous;
- Foundation 1 values would have to become schema-enforced records;
- safe cursor behavior would require exposing internal generations;
- Query would require arbitrary SQL or execution of author text;
- simplification would contradict D21 durability/publication/lifetime semantics.

Do not retain public complexity because it was previously written.
Do not remove private correctness because it is complicated.

~~~text
dead-simple Luau API
+
rigorous CarbonLuau-owned internals
~~~

## 24. Verdict

The developer-facing model is:

1. save ordinary Foundation 1 values;
2. Query one field with structured equality/range/order;
3. optionally hint important fields early;
4. CarbonLuau owns everything else.

**CANONICAL BASELINE READY — FINAL CORRECTION BEFORE PERSISTENCE-2A.**

No production Persistence Foundation 2 implementation began under the initial D22 adoption or
either documentation correction.
