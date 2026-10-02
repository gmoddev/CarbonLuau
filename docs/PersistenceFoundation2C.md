# Persistence Foundation 2C — public structured Query

Status: **PERSISTENCE-2C PASS — READY FOR PERSISTENCE-2D**. This record owns
the 2C public Query boundary and its evidence. [D21](Invariants.md#d21--persistence-foundation-1)
still owns authoritative primary values, durability, quotas, the worker and
callback authority. [D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query)
and [Foundation 2](PersistenceFoundation2.md) own the public contract.
[2A](PersistenceFoundation2A.md) and [2B](PersistenceFoundation2B.md) are the
previously qualified private substrate. Combined Foundation 2 closure is 2D,
not this task.

Starting revision: `0bf0918e96b5943085895ec35a29ab08a3e88ae1`.
Implementation revision: `d9d1197657b8a1f33f6bddd87522d2cfc3882410`.
The final tested production source is this implementation revision; later
evidence-only commits do not change production code. Hosted validation is
recorded below; no package release or tag belongs to 2C.

## Implemented boundary

`DataStore:Query(Request, Callback)` accepts the exact plain eight-member
request: required exact top-level `Field`; optional `Type`, `Equals`, inclusive
`Min`/`Max`, `Direction`, `Limit` and opaque `Cursor`. It is a non-yielding,
committed-only D21 submission with one original FIFO reservation and five-second
deadline, including 2B automatic preparation. Callback success receives a
fresh `DataStoreQueryResult` (`Items`, optional `NextCursor`); accepted failure
receives `nil, ErrorCode`. Synchronous malformed input owes no callback.
No public SQL, schema, migration, index lifecycle, OFFSET, primary-store scan
fallback, Query builder, `UpdateAsync` or extra persistence method was added.

Native code snapshots and validates the request without invoking author
metamethods. The managed queue retains the original admitted route while 2B
preparation waits, consuming ordinary request rates but no mutation rate.
The worker chooses a healthy admitted derived representation and executes one
bounded read transaction. Four fixed derived statement shapes (prefix and
entry ASC/DESC), plus exact member/primary point lookups, use bound scalar
parameters and `INDEXED BY` selected private indexes; author text never forms
SQL. Exact-pinned `EXPLAIN QUERY PLAN` assertions are in the Query worker test.

One page examines at most 101 derived candidates and performs at most 101
primary point lookups, returns at most 100 items, 8,192 aggregate expanded
entries and 67,584 encoded bytes, with a one-million-VDBE-instruction
backstop. The bounded walker returns controlled `QueryUnavailable` if it
cannot establish a complete page within its proof budget; it cannot treat a
truncated scan as an empty or complete success. A maximum legal one-record
page fits. Page results contain authoritative Foundation 1 envelopes; Luau
values are constructed only after current callback authority is admitted.

The self-contained base64url cursor has a private session-scoped HMAC-SHA256
tag and binds namespace, store, field, selected type, normalized selection,
direction and exact healthy derived generation. It carries the last returned
sort value/key, not a row ID; deleting that boundary record does not prevent
continuation. Limit may change between pages. A new worker session, changed
selection or withdrawn/replaced generation rejects old cursors rather than
retargeting them. Pagination is keyset-based over current per-page data, not a
frozen multi-page snapshot.

## Transport and identity

The existing native ABI remains 1.5. A private worker operation and response
encoding were added; no managed/Rust object or SQLite identity crosses to
Luau. The largest existing D21 Set request frame is at most 65,849 bytes
(56 fixed + 65 package + 64 store + 128 key + 65,536 envelope). With its
65,536-byte reserved response, 128 simultaneous requests retain at most
16,817,280 bytes. Query's 6,393-byte maximum request and 67,584-byte
reserved result are smaller per reservation. One bounded process reply,
one staging frame, 128 request records and at most eight callback admissions
per owner turn fit the separate 18-MiB managed transport envelope. Worker
decode/scratch remains under the existing supervised worker memory bound;
retained Luau callback values remain under VM/domain bounds. Reservation
release, retirement and no-replay rules are unchanged.

Scripting API stays `0.5.0-experimental`; development package stays 0.4.0,
native ABI 1.5, provider protocol 1.2, package schema 1 and pinned Luau
revision stay unchanged. Tooling metadata is generated from the production
binding annotations; preview still reports the established unsupported-host
diagnostic rather than simulating persistence.

## Qualification ledger

| Gate | Observed evidence |
|---|---|
| Windows worker | Real SQLite Query fixture covers ordering, duplicate values, range/equality/false, inference/ambiguity, large and expanded pages, forged/restart cursor, indexed plans and bounded sparse-prefix failure; focused PASS on task-owned D: worker. |
| Windows native regression | 22/22 CTest cases PASS, including the Query worker, facade, Foundation 1 backend/crash/quota and 2A/2B derived suites. |
| Windows real VM | Production native library, managed queue and supervised SQLite worker: full Foundation 1B regression and focused public Query automatic preparation, cursor, publication/type cases, and five runnable examples PASS. |
| Linux native | 23/23 production native CTest cases PASS in bounded Ubuntu container, including Query and prior persistence/regression tests. |
| Linux ASan/UBSan/leak | Changed Query worker and facade focused tests PASS with leak detection. |
| Linux real VM | Full Foundation 1B regression and focused public Query test with five runnable examples PASS against the pinned native VM and production worker. |
| Managed queue | Query original FIFO reservation, deadline, ready dispatch, unavailable cleanup and retirement PASS; 1,000 pre-existing VM-retirement cycles PASS. |
| Metadata/tooling/package | Generated metadata drift, API, architecture, tooling contracts and deterministic release-content checks PASS locally. |
| Hosted Windows/Linux/sanitizer | [Validation at the evidence baseline](https://github.com/gmoddev/CarbonLuau/actions/runs/36936621021): Windows x64, Linux x64, ASan/UBSan/leak, runtime/lifecycle, deterministic package and clean-install jobs all PASS. The tested production source is `d9d1197`. |
| Hosted tooling | [Foundation A/B tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/36936620989): Windows, Linux and macOS jobs PASS. [Baseline tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/36936305914) PASS on the unchanged production-source commit. |
| Documentation deployment | [GitHub Pages](https://github.com/gmoddev/CarbonLuau/actions/runs/36936620957) deployed the evidence baseline successfully. |

The test workspaces are task-owned (`D:\Sandbox\Codex\Workspaces\CarbonLuauPersistence2C`
on dockerbox and `/root/codex/carbonluau-2c` on BigKVM). No live Carbon/Rust
server or authenticated client evidence is claimed by these fixtures. Prior
2A/2B live/platform evidence remains separate and is not rewritten.
The first implementation push omitted this new evidence file while an active
compatibility page linked to it; hosted API link checking caught that
documentation-only defect. The evidence baseline added the file, and its
Windows/Linux/macOS tooling workflow passed. No runtime correction resulted.

## Handoff

2D must perform combined Foundation 2 lifecycle, scale and release-readiness
closure against this public Query surface. It must not infer package
publication from 2C implementation or locally passing fixtures.
