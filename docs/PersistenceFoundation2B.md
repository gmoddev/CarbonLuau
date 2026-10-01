# Persistence Foundation 2B — optional hints and private automatic demand

Status: **IMPLEMENTED; FINAL PLATFORM/HOSTED QUALIFICATION PENDING.** This
record owns the 2B implementation and evidence. [D21](Invariants.md#d21--persistence-foundation-1)
continues to own authoritative primary storage and resource policy;
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query)
and [Foundation 2](PersistenceFoundation2.md) own the future Query contract.
[2A](PersistenceFoundation2A.md) owns the qualified private derived substrate.

## Implemented boundary

The only new public syntax is
`DataStoreService:GetDataStore(Name: string, Options: DataStoreOptions?)`.
`DataStoreOptions` is a plain table with optional `Indexes = {string}`.
Validation accepts a dense, distinct list of at most eight exact top-level
field names, each 1..64 UTF-8 bytes. Metatables, holes, duplicates, unknown
options, nonstrings, NUL and malformed UTF-8 reject synchronously without
invoking author code. Punctuation is literal. Acquisition is disk-free, does
not make a D21 storage request and does not imply readiness.

Native publication scopes snapshot and union hints per domain/store. A
provisional candidate or cold module may acquire a store; successful
publication sends bounded scalar/string hint intent to the managed owner
thread. Failed or nested-rolled-back publication sends none. The managed
session stages intent until committed, then admits it to the existing
supervised worker. Repeated acquisitions preserve a bounded union; an empty
later hint does not erase earlier intent. Successful replacement reasserts its
new desired hints; removed hints do not delete already retained fields. A
failed replacement does not change the active generation's desired intent.

One private CLPQ operation, `Demand=4`, identifies the exact
namespace/store/field. It joins retained ACTIVE/BUILDING state or starts the
existing bounded 2A preparation path; it neither creates a second index
engine nor exposes a Luau Query method. The private CLPS status is Preparing,
current-process-admitted ACTIVE, or Unavailable. ACTIVE field admission is not
a promise that every scalar facet is complete: an oversized string makes the
string facet unavailable while the primary value remains legal. Missing,
non-scalar and mixed values remain ordinary Foundation 1 data.

The private future-Query waiter seam holds one original D21 request
reservation and five-second absolute deadline. At most eight waiters per
namespace and 32 globally are a subset of D21's eight/128 pending ledger.
A waiting namespace head retains FIFO position; other namespaces progress.
Ready before deadline settles once; deadline first yields one private
`IndexPreparing` equivalent. If accepted ready work loses usable state, it
joins waiters only if capacity remains, otherwise settles once with a private
`QueryUnavailable` equivalent. No accepted request is replayed or
re-reserved. Retirement discards callback authority without undoing committed
preparation. There is no public waiter or Query callback in 2B.

The 2A eight-retained-field/store ceiling includes hinted and automatic
fields together. A ninth field is unavailable without eviction or primary
scan. BUILDING cannot serve Query; 2A foreground Set/Remove and exact
correct-or-withdraw behavior remain authoritative. D21 logical quotas and
the private DB/journal extent and operational allocation limits are unchanged.

## Qualification ledger

| Gate | Evidence/status |
|---|---|
| Public options/publication | Native real-VM fixtures cover omitted/empty, 1/8/9, duplicate/hole, malformed text, metatables, exact punctuation, snapshot, nested/cold/provisional/failed/successful publication, root/addon isolation and no Query binding. Windows focused PASS. |
| Private demand | Actual SQLite backend and framed worker fixtures cover duplicate demand, retained ACTIVE/BUILDING, process-local proof, eight/ninth field, type discovery, oversized strings and checkpoint-time type changes. Windows focused PASS. |
| Managed ledger | Deterministic tests cover 8/32 waiter bounds, deadline, FIFO, other-namespace progress, ready-state loss, literal punctuation, retirement, one reservation and no replay. Windows PASS. |
| Integrated Windows | Native 21/21 and real compiler/VM → managed hint staging → production worker demand PASS. Persistence-1B real-worker regression PASS. |
| Windows combined closure | Full real-VM/runtime regression PASS; Persistence-1C combined real-worker stress and 12 host/worker reopen cycles PASS. Final-source hosted qualification remains pending. |
| Linux native/sanitizer/fault | Pending hosted final-source qualification; local linuxbox has no installed build toolchain. |
| Hosted CI/documentation | Pending final-source run. |

Historical 2A storage-fit, compact-layout, split-storage, startup and D21
capacity investigations remain intact. 2B does not reopen their settled
architecture or change the 1-GiB database extent, 1,075,904,512-byte journal
extent, 2,560-MiB qualified operational allocation budget, PERSIST + EXTRA,
primary authority or D21 no-replay semantics.

## Identity and handoff

Scripting API remains `0.5.0-experimental`; package remains development
`0.4.0`. Native ABI `1.5`, provider protocol `1.2`, addon package schema `1`
and the Luau pin are unchanged. 2B does not release/tag a package. Public
`DataStore:Query`, result/cursor types, predicates and 2C execution remain
unimplemented. 2C is a separate authorization after the final 2B gates pass.
