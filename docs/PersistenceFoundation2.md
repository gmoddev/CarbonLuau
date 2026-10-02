# Persistence Foundation 2 — bounded derived indexes and Query

Status: **CANONICAL ARCHITECTURE; PRIVATE 2A PASS; 2B PASS; PUBLIC 2C PASS; 2D PASS — FOUNDATION 2 QUALIFIED**.
The 2026-09-29 final public-design correction preceded implementation.
[2A evidence](PersistenceFoundation2A.md) records qualified private derived
state. The [2B options/hint/demand slice](PersistenceFoundation2B.md) is
qualified; [2C](PersistenceFoundation2C.md) records the separately authorized
public Query implementation and its qualification. [2D](PersistenceFoundation2D.md)
records the passing combined closure.

Foundation 1 qualified baseline: 034f28f81c64e0f135ec882fb05de4ef7f33fcf7.
Initial D22 documentation baseline: ee208831efc5647dab553b5f9d0524e7e2e83353.
Reviewed/superseded correction baseline: `97bc514fb94a3739ab9fbad91c04c2d40a9c3534`.
[Correction evidence](PersistenceFoundation2-Validation.md) records the final
consistency review, private-bound corrections and documentation gates.

This document is the detailed authority for
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query).
[D21](Invariants.md#d21--persistence-foundation-1) remains authoritative for
durability, namespaces, Foundation 1 values, the supervised persistence worker,
publication/callback authority, primary-data quotas and PERSIST + EXTRA storage.

This correction supersedes both the initial D22 query-schema design and the intermediate
comparison-string design before any Foundation 2 production implementation began. There is
no author-managed schema/version, required index declaration, migration contract or write
freeze.

The Query examples below describe the implemented 2C API. Current
development bindings and generated definitions provide Foundation 1
Get/Set/Remove, 2B optional `GetDataStore` hints, and 2C `Query`.

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

Players:Query({
    Field = "Coins",
    Direction = "Descending",
    Limit = 25,
}, function(Results, ErrorCode)
    if ErrorCode then
        return
    end

    for _, Item in Results.Items do
        print(Item.Key, Item.Value.Coins)
    end
end)
~~~

CarbonLuau prepares and maintains the bounded derived index needed for Coins.
The call returns immediately; results arrive through the required callback.
If preparation cannot finish during the bounded request lifetime, the callback
reports `IndexPreparing`. That call is finished; a later Query may benefit from
preparation that continues in the background. Storage calls already accepted in
the same namespace keep their normal order. To query after a write succeeds,
submit Query from that write's success callback.

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

Options is a plain table with only `Indexes`; Indexes is a dense array of distinct
field-name strings, never a string-keyed type map. Unknown members, holes,
duplicates, non-string entries and metatables reject synchronously. An absent or
empty list requests no additional fields. Repeated acquisitions in one domain
accumulate a bounded union per store; they cannot erase another acquisition's
hints. The union and automatic demand share the eight-field ceiling.

Typed index hints are deferred. Query.Type is the escape hatch for actual type ambiguity.

Acquisition stays synchronous and disk-free. Hints captured by provisional code do not create
durable state. Hints use existing domain/module publication staging and become
eligible only after that publication succeeds in a committed owner domain.
Failed candidates or cold modules discard their staged hints. A committed caller
inside a cold/nested/public module cannot start maintenance through acquisition.
No hint bypasses D21's current-admission/resource-owner checks.

Changing hints between committed addon generations is desired-state change, not migration.
Adding a hint requests proactive preparation/retention. Removing a hint removes that proactive
intent; already prepared fields remain available and still count toward the
ceiling. Foundation 2 does not remove an allocated field merely because a hint
disappears. No author version increment, slot-management or migration call exists.


## 4. Query surface

Foundation 2 adds:

~~~luau
DataStore:Query(
    Request: DataStoreQuery,
    Callback: (DataStoreQueryResult?, string?) -> ()
) -> ()
~~~

Query uses D21's current admitted owner, committed-only/no-active-publication
check, non-yielding submission and later callback admission. A cold module may
acquire a store and stage hints, but cannot submit Query. Foreign facades/shared
closures do not transfer namespace authority. Failed or retired domains cannot
dispatch new demand or receive stale completions.

The request shape is:

~~~luau
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
~~~

The ordinary forms are:

~~~luau
Players:Query({
    Field = "Coins",
    Direction = "Descending",
    Limit = 25,
}, Callback)

Players:Query({
    Field = "Faction",
    Equals = "Blue",
}, Callback)

Players:Query({
    Field = "Level",
    Min = 10,
    Max = 20,
}, Callback)
~~~

Direction defaults to Ascending.
Limit defaults to 50 and is bounded to 1..100.
Cursor is optional.
Type is optional and normally unnecessary.

Continue a page by passing its cursor with the same selection and ordering:

~~~luau
Players:Query({
    Field = "Coins",
    Direction = "Descending",
    Limit = 25,
    Cursor = Previous.NextCursor,
}, Callback)
~~~

Only continue when `Previous.NextCursor` is non-nil. Each page sees current data;
writes between pages may change membership or order. An invalid continuation
reports `InvalidCursor`. The cursor is opaque and may become invalid after
restart or replacement of CarbonLuau's query state.

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
- string-valued Equals/Min/Max satisfy the bounded queryable-string rule;
  Field, enum strings and Cursor use their own bounds;
- Cursor continues the same compatible logical query.

Request must be a plain table containing only the eight listed members. Inspect
and snapshot raw entries without invoking user code. Reject metatables, unsupported
values, unknown members, malformed UTF-8, NUL, nonintegral/out-of-range Limit and
oversized input before acceptance. Optional nil members are absent; `Equals = false`
is present and selects boolean. Callback is mandatory and must be a function.
Field is an exact 1..64-byte UTF-8 top-level map key, case-sensitive and without
normalization; punctuation is literal and never a nested-path or SQL selector.
The same field-name validation applies to hints. Foundation 1 map keys outside
these Query field bounds remain legal persisted data.

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
4. if no scalar value is represented, a complete healthy prepared field returns an empty page.

Discovery records represented scalar categories independently of derived-state
availability. An oversized string or an unavailable representation must not
disappear from type discovery and turn incomplete data into an empty result or
silently choose another type. Multiple represented categories require Type;
after selection, unavailable selected state reports QueryUnavailable. Explicit
number/string Type with no matching values returns an empty page when that state
is complete and healthy.

Boolean selection always requires Equals. `Type = "boolean"` without Equals is
InvalidQuery before acceptance. If an untyped ordered request discovers only
boolean values, it completes with InvalidQuery; it never invents false/true
ordering. For boolean equality, Direction orders matching record keys.

There is no cross-type ordering.

CarbonLuau may maintain separate private number/string/boolean representations for one prepared
field. That internal detail does not consume separate author-visible field slots and is not a
public lifecycle concept.

Foundation 2 never turns Foundation 1 into a schema-enforced record store.


## 7. Automatic index preparation

**Public Query never falls back to scanning authoritative primary records for matches.**

When Query needs derived state that is not ready, CarbonLuau starts or joins bounded
preparation. The same machinery serves automatic first-use demand and optional Indexes hints.

A store may have at most eight retained derived fields total, including active,
preparing and unavailable fields awaiting repair. Automatic and hinted fields
share that ceiling. Private type representations and replacement generations
for one field do not consume additional field slots, but all their bytes count.
No global eager indexing of arbitrary fields occurs.

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

Preparation waiters are a subset of D21's ordinary accepted-request ledger:

- at most 8 per namespace and 32 globally, within the total 8/128 pending bounds;
- general request tokens and the single callback/result reservation are acquired once
  at initial acceptance; there is no mutation token or second Query rate bucket;
- the original absolute five-second deadline covers waiting, queueing and execution;
- the request retains its FIFO position, host/VM/domain/callback authority and reserved
  completion capacity until delivery/discard;
- no promotion into a second ledger, deadline reset or later synchronous rejection;
- no worker Luau entry.

The earlier separate 30-second waiter model is superseded: it allowed later
same-namespace operations to overtake an accepted Query and introduced a second
deadline. The concrete replacement above follows the qualified D21 ledger and
limits directly. It is an additive Query state, not a change to Get/Set/Remove.

If preparation becomes ready before the original deadline, execute that Query
once in its existing FIFO position. A cold head request holds later requests in
that namespace under normal bounded FIFO; it does not freeze the store for the
duration of an index build. Other namespaces continue through fair dispatch.
When no foreground head can execute, bounded maintenance may advance preparation
for a waiting head. Executable foreground work has priority; later same-namespace
requests cannot overtake the waiter. Maintenance never blocks the owner thread.

If the request is still waiting for preparation at its deadline, record one
IndexPreparing completion and release its FIFO position. If Query execution
itself times out, use D21's DeadlineExceeded. Callback delivery uses normal later
owner-thread admission; neither outcome promises delivery during a paused or
retired VM. The original Query is finished and never replayed. Already-committed
CarbonLuau-owned preparation may continue, and a later Query may benefit.

Waiter saturation known at submission rejects synchronously using D21's controlled
admission convention, with zero accepted request or callback. Retirement cancels
waiting work and frees reservations without revoking committed maintenance intent.
If an accepted ready Query loses its usable state before execution, it may join
the waiting subset only within that subset's capacity and its original deadline.
If all waiting slots are occupied, complete it once with QueryUnavailable in FIFO
order. Never exceed the cap or turn that already accepted call into a later
synchronous rejection.

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
uses a persistent keyset checkpoint, and is subject to at most 1,000,000 SQLite
VM instructions and the existing five-second worker operation backstop. Decode
one bounded Foundation 1 value at a time; stop before advancing past an unprocessed
record. Batch entries, completeness/type accounting and checkpoint advancement
commit atomically. Maintenance yields between batches whenever executable
foreground persistence work is ready, including work from other namespaces.

At the end of the bounded keyset traversal, one transaction verifies the build
identity, completeness and absence of recorded maintenance failures, then
atomically publishes the generation and matching type metadata. No final full
primary scan or unbounded catch-up phase is permitted. Queries cannot observe a
partly published generation. Crash/reopen sees either the previous publication
state or the complete new one.

Before each batch, honor the earliest waiting request deadline as well as the
operation backstop. Deadline expiry, cancellation and queued terminal outcomes
are processed in namespace order; IndexPreparing is never scheduled ahead of an
earlier accepted request. Waiting is not permission for a maintenance transaction
to monopolize the worker after ready foreground work can be selected.

Crash/restart resumes committed build state. No author's Query is replayed.
Resume is gated by current-process derived verification. A persisted BUILDING
checkpoint is not permission to publish or maintain an unverified generation.
If verification finds that intervening authoritative primary state makes the
retained build incomplete, withdraw it and restart bounded preparation instead
of publishing its old checkpoint.

Foundation 1 primary Ready requires its D21 recovery, integrity, schema,
envelope and quota checks, not the entire retained derived proof. Every worker
start clears process-local derived admission. A retained generation is usable
only when its durable state is ACTIVE **and** its exact generation has passed
current-process authority, accounting and primary-representation verification.
Until then Query is unavailable/preparing and never scans primary records.
Background/lazy proof is bounded and yields to foreground persistence work.
The proof and any admitted identity are invalidated if an intervening primary
mutation could make the proof stale. No process-local admission survives a
worker crash or reload. The qualified private 2A implementation uses one
process-local proof for the retained graph; per-index lazy admission is not a
current implementation claim. Primary Ready is independent of that proof.


## 9. Derived state must not make primary writes fragile

Primary persisted values are authoritative. Query indexes are subordinate derived state.

Normally Set/Remove and all active/building derived changes occur in the same SQLite
transaction.

For a successful primary Set/Remove affecting admitted ACTIVE derived state, the committed transaction
must end as either:

~~~text
new primary state + corresponding correct ACTIVE derived state
~~~

or:

~~~text
new primary state + affected derived state atomically not Query-admitted
~~~

never:

~~~text
new primary state + stale Query-admitted ACTIVE derived state
~~~

During post-restart verification, a successful Set/Remove may invalidate the
process-local admission fence before committing the primary write and defer
derived maintenance. Persisted ACTIVE/BUILDING rows behind that fence are not
Query-usable or publishable; they must be reverified against the new primary
snapshot or withdrawn before admission. This does not replay the primary
mutation, and durable ACTIVE metadata alone never bypasses the fence.

An otherwise valid primary mutation must not fail merely because subordinate derived state has
exhausted its separate logical capacity, become unmaintainable or needs repair.

When the primary mutation itself is provably safe, the same transaction may commit the primary
mutation and D21 accounting while atomically withdrawing the affected derived state from Query
service and marking it for bounded repair/rebuild.

Physical SQLite/storage failure, uncertain COMMIT outcome and primary corruption remain governed
by D21 and may still fail or make the primary mutation Indeterminate.

The same rule protects future publication: if a primary mutation commits without
successfully maintaining a BUILDING generation, that transaction marks it
ineligible for activation. Resume only from a provably correct checkpoint or
restart bounded preparation; never publish a silently incomplete build. Undo
partial derived changes and reconcile their accounting before a safe primary
commit. A failed rollback, database-full/I/O error or uncertain transaction is
not a merely logical derived-capacity failure; D21 decides the actual outcome.

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

The future result contract, using the existing PersistedValue type, is:

~~~luau
type DataStoreQueryResult = {
    Items: { { Key: string, Value: PersistedValue } },
    NextCursor: string?,
}
~~~

Publicly:

- Limit defaults to 50;
- Limit is at most 100;
- a page may contain fewer items when the bounded response envelope is full;
- when compatible continuation exists, NextCursor is returned.

| Resource | Ceiling |
|---|---:|
| Query descriptor | 6 KiB |
| Field name | 64 UTF-8 bytes |
| Queryable string scalar | 1,024 UTF-8 bytes |
| Returned items | 100 |
| Default items | 50 |
| Cursor | 2 KiB; generated encoding bound below |
| Encoded response page | 66 KiB |
| Aggregate expanded value entries | 8,192 |
| Candidate derived rows | at most 101 |
| Primary point lookups | at most 101 |
| SQLite VM backstop | 1,000,000 VDBE instructions |

The worker stops before adding an item that would exceed page/decode limits and returns a
continuation when compatible results remain. It never returns a partial record.
Reserve result framing and continuation capacity before adding an item. A
continuation resumes strictly after the last returned item, not an examined item
excluded by the page bound. The one lookahead row counts toward the 101 limits.
One maximum legal record must fit so page limits cannot cause an endless empty page.

The 66 KiB page ceiling preserves Foundation 1's 68 KiB IPC frame after protocol overhead.
It does not by itself qualify the current 64-KiB result-buffer reservation for
Query. Persistence-2C must reserve Query-sized results while preserving D21's
18-MiB total transport cap: 128 maximum 68-KiB request plus 66-KiB result pairs
use 16.75 MiB before other bounded transport overhead. Account for that overhead,
decoding scratch and native callback payloads explicitly; never allocate a page
outside its reserved capacity. The aggregate 8,192 expanded entries includes
result wrappers as well as values. Values retain every Foundation 1 individual
depth/count/envelope bound and are materialized only at valid callback admission.

Numeric OFFSET is not supported. Pagination uses an opaque keyset cursor. Public semantics only
promise that the same compatible logical Query may continue with NextCursor, the cursor is
opaque, and InvalidCursor is reported when it is no longer valid after restart or internal
derived-state replacement.

Each page is consistent at that call's execution time. Pagination is not a frozen multi-page
snapshot; writes between pages may alter membership/order and can cause omissions or repeats
relative to an imaginary frozen snapshot.

Internal cursor format versions, field IDs, generations, sort-key encoding, integrity secrets
and other compatibility metadata remain private.

### Private cursor and byte-bound reconciliation

The reviewed 512-byte cursor could not carry every allowed 1,024-byte string
boundary. Use a bounded self-contained authenticated cursor, not retained
per-cursor server state or a later lookup of a mutable/deleted boundary record.
Its private header is at most 64 bytes (format, field/generation identity,
selected type and direction); add a 32-byte binding digest, a 2-byte scalar-payload
length plus at most 1,024 bytes, a 1-byte record-key length plus at most 128 bytes,
and a 32-byte integrity tag. Maximum raw size is 1,283 bytes; canonical base64url
requires at most 1,712 bytes, within the 2-KiB ingress cap. Oversized/noncanonical
encodings reject before allocation. Compression is not needed for this proof.
The cursor's scalar payload is untagged; its type is already in the header. The
private database SortKey type tag is reconstructed after cursor validation.

Bind to the current persistent namespace, exact store/field, selected type,
normalized Equals/Min/Max and Direction, plus the exact healthy internal
generation. Use a private secret scoped to the loaded persistence session;
restart/worker replacement invalidates older cursors. Verify integrity and
authority before using a boundary. No key/value, secret, digest or generation is
logged or exposed as a separate API member; opaque integrity protection is not
an encryption promise. Never reconstruct or hot-retarget a stale continuation.

Limit may change within 1..100 between pages. Omitted Type continues the type
selected on the first page; adding another represented type later cannot retarget
the cursor. An explicit Type must agree. A changed selection/direction/store or
unusable old generation reports InvalidCursor; it cannot request a fresh first page.
Missing/corrupt primary rows under otherwise healthy derived state follow the
corruption rules, never silent skipping.

The 6-KiB descriptor allows two 1,024-byte bounds, a 2,048-byte cursor, a 64-byte
field and at most 512 bytes of fixed framing/type/direction/Limit data: at most
4,672 bytes. These are private encoding allowances, not author calculations.
A page containing one maximum 65,536-byte envelope, 128-byte key, maximum
1,712-byte generated cursor and at most 192 bytes of result framing uses 67,568
bytes, below 66 KiB (67,584). The 192-byte framing ceiling is mandatory, not an
estimate; this maximum case has only 16 bytes of spare page capacity. Enforce
the aggregate cap for all multi-item pages.
2C must prove these encodings and their 68-KiB outer frame with boundary fixtures.


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

Type/completeness selection, cursor/generation validation, derived seeks, exact
primary point lookups and continuation detection use one read transaction per
Query execution. No SQLite snapshot/transaction survives the response or spans
pages. Qualification includes equality, each inclusive range shape and top-N,
both directions, first/continuation pages and all permitted scalar types.


## 13. Queue, fairness and lifecycle

Every accepted Query reuses D21's existing foreground persistence admission;
section 7's waiting subset changes neither its FIFO position nor deadline:

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
Removing Coins removes that proactive intent; retained state continues to count
under the no-eviction rule in section 3.

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

Charge the complete canonical encoded bytes of each retained derived entry and
metadata record, including lengths/type tags, authority/field/generation IDs,
sort and record keys, counters and checkpoints. No metadata-only allocation is
free. Old, shadow, withdrawn and pending-cleanup state all count until deleted
in a committed bounded cleanup transaction. Every allocation and release updates
the separate durable accounting in the same transaction; rollback restores both.
Checked arithmetic and startup validation are required. Private encoding widths
are frozen/qualified in 2A; filesystem/B-tree overhead is additionally constrained
by the unchanged database extent and operational-budget rules, not equated to
this logical charge. Creating derived metadata never invents a primary record or
changes Foundation 1 primary store/key/namespace quota counters.

D21 primary-data quotas remain unchanged. Query indexes therefore do not silently
shrink the addon's user-data quota.

The 16-MiB namespace and 64-MiB global derived logical limits are hard
admission ceilings, not physical-space reservations. Neither unused primary
nor unused derived logical quota guarantees that every otherwise-valid mutation
can be admitted under the independent database and journal file-byte limits.
If physical capacity is definitely exhausted before COMMIT, the mutation fails
with a controlled D21 capacity result and no primary change. Uncertain outcomes
after COMMIT begins retain D21 Indeterminate/no-replay semantics. No successful
operation may leave an affected ACTIVE index stale. A derived *logical* capacity
or independently classified representation mismatch may withdraw the affected
index atomically as specified in section 9; SQLite FULL, I/O, corruption and
uncertain COMMIT are not reclassified as disposable-index failures.

The qualified Foundation 1 baseline is a 512-MiB database EOF and its bounded
retained journal, with 1,280 MiB as a qualified operational allocation budget,
not an instantaneous allocated-block guarantee. Persistence-2A adopts a
1-GiB database EOF, a 1,075,904,512-byte retained-journal EOF and a 2,560-MiB
filesystem-allocation operational budget for the combined one-database layout
under D21. Final hosted qualification is recorded in the [2A evidence](PersistenceFoundation2A.md).
It must enforce the chosen byte/
page extents and bounded work/memory, but need not prove that every logical
state below both quotas is physically realizable under every fragmentation or
journal history. File growth is not preallocation; physical exhaustion must
fail safely. The numerical amendment is adopted with the separate private 2A
implementation, not by the earlier architecture correction alone.

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

D21 persistence/backend failures remain authoritative where they already apply.
Success invokes Callback(Result, nil); an accepted failure invokes
Callback(nil, ErrorCode). Empty results use an empty Items array and nil NextCursor.
Malformed request shape, contradictory selection and incompatible explicit types
reject synchronously as InvalidQuery before acceptance where knowable at submission.
An error discovered only from stored state (including boolean-only ordered
selection) completes through the callback once. Invalid/stale/foreign facade,
publication, callback and general admission failures reuse D21's synchronous
controlled convention; a rejected call owes no callback.

Unavailable selected string state, an exhausted eight-field ceiling, logical
derived capacity, untrusted derived state or a rejected private plan/work bound
map to QueryUnavailable when known after acceptance. Preparation that is still
eligible but unfinished at the request deadline maps to IndexPreparing. Invalid
cursor encoding/authority/query binding maps to InvalidCursor; known malformed
input may reject synchronously. Actual database/physical failure never gets
relabelled as harmless preparation or absence. Operator diagnostics preserve
the precise cause without exposing it as an author protocol.

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
- atomic checkpoint/final publication and failure fencing for building state;
- crash/reopen and exact-pinned storage/write-amplification qualification.

No public Query binding is implemented in 2A.

### Persistence-2B — automatic demand and optional hints

- GetDataStore(Name, Options?) publication semantics;
- simple string-list Indexes hints;
- automatic field demand;
- eight-field store ceiling;
- preparation waiters inside D21's existing ledger/deadline;
- online build scheduling/fairness;
- field type discovery/completeness;
- replacement/reload/restart desired-state behavior.

### Persistence-2C — public structured Query

- request validation;
- Equals/Min/Max and type inference;
- AmbiguousFieldType behavior;
- deterministic order;
- bounded result pages;
- opaque authenticated keyset cursor with the byte-bound proof above;
- exact work limits;
- exact-pinned EXPLAIN QUERY PLAN assertions;
- metadata/generated definitions and author docs.

Qualification must exercise false equality, one/two-sided inclusive bounds,
mixed types, empty and boolean-only fields, oversized string availability,
changed type membership between pages, forged/cross-namespace/stale cursors,
deleted boundary records, maximum two-string-bound descriptors, and maximum
record-plus-continuation pages. Prove raw snapshot conversion, publication
rejection, FIFO/deadline/rate/reservation conservation and at-most-once delivery
for cold and ready queries. No private maintenance command counts as executing
or replaying the user's Query; run the query statement at most once.

### Persistence-2D — combined closure

[The combined qualification record](PersistenceFoundation2D.md) owns the
measured results and final disposition for this phase.

- Windows/Linux live qualification;
- concurrent build/write stress;
- crash during build/publication/active maintenance;
- replacement/VM recovery/stale callback/waiter/cursor matrix;
- derived quota/resource convergence;
- physical-allocation requalification;
- final API/release-readiness audit.

The original documentation correction implemented no phase. Separately
authorized [2A](PersistenceFoundation2A.md) is PASS. Separately authorized
[2B](PersistenceFoundation2B.md) qualifies optional hints and private
demand/waiters; Query remains a separate 2C task.


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
| Derived fields per store | maximum 8 retained, including active/preparing/unavailable |
| Index creation | automatic bounded preparation |
| First-query wait | 8/ns and 32 global waiting subset of D21 8/128; original five-second deadline includes wait and execution |
| Wait timeout | one IndexPreparing callback; original Query never replayed |
| Writes during build | continue; building state maintained transactionally |
| ACTIVE consistency | update correctly or atomically withdraw affected derived state |
| Primary authority | Foundation 1 persisted values |
| Derived accounting | separate 16 MiB/ns + 64 MiB/global logical pool |
| Results | Items + optional NextCursor |
| Count | default 50, max 100 |
| Page | max 66 KiB and 8,192 expanded entries |
| Private descriptor/cursor | 6 KiB / 2 KiB ingress; emitted cursor <=1,712 bytes |
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
| API identity | 0.5.0-experimental; released experimentally in package 0.5.0 after the separate 2D/release gates |
| Production implementation | private 2A PASS; 2B PASS; public 2C Query PASS; [combined 2D qualification](PersistenceFoundation2D.md) PASS |


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

No production Persistence Foundation 2 implementation began under the initial
D22 adoption or either documentation correction. Private implementation began
only under separately authorized Persistence-2A work.

The separately authorized [Persistence-2A work](PersistenceFoundation2A.md)
failed the former universal physical-fit gate. That negative result remains
historical evidence, but the 2026-09-30 approved D21 capacity correction removes
the implication that unused logical quota guarantees physical admission.
The resumed one-database, Foundation 1 primary representation implementation now
publishes authoritative-primary Ready before bounded process-local derived
verification; unadmitted retained ACTIVE state cannot serve Query. The private
extent, correctness, work/memory, platform, failure and hosted gates are closed
in [PersistenceFoundation2A.md](PersistenceFoundation2A.md). Private 2A is
PASS; Persistence-2B was separately authorized and is
[qualified](PersistenceFoundation2B.md), not work performed during 2A.
Public Query implementation and its distinct evidence are in
[PersistenceFoundation2C.md](PersistenceFoundation2C.md).
