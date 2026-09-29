# Persistence Foundation 2 — final canonical correction audit

Date: 2026-09-29. Scope: documentation/policy only, before Persistence-2A.
The detailed contract remains [PersistenceFoundation2.md](PersistenceFoundation2.md)
under [D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query).
This record is design evidence, not implementation or platform qualification.

## Provenance and correction

Starting fetched `origin/main`: `97bc514fb94a3739ab9fbad91c04c2d40a9c3534`.
The checkout was clean and advanced by fast-forward. That baseline had already
replaced the intermediate comparison language with structured requests on
2026-09-25. Its predecessor `6dd5f7a` is the superseded comparison-language
amendment; initial adoption/recovered record is `ee20883`. This pass preserves
the structured direction and closes the remaining consistency/documentation gaps.

`git diff 034f28f..97bc514 --name-only` contains only seven documentation files.
Production storage still has exactly Get/Set/Remove operations; bootstrap,
authoritative API metadata and generated declarations have no Query/options/index
binding. No Foundation 2 production implementation preceded this amendment.

The amendment/final tested revision is the immutable commit that introduces this
file (`git log --diff-filter=A --format=%H -- docs/PersistenceFoundation2-Validation.md`).
The task handoff supplies its SHA and final hosted checks. No version or package
publication is performed by this correction.

### Concrete corrections required by the review

| Gap at the reviewed baseline | Correction and reason |
|---|---|
| Separate 30-second accepted waiter followed by foreground admission | Waiting is now an 8/namespace, 32/global subset of D21's 8/128 ledger. The original five-second deadline covers preparation and execution; FIFO, rate charging and callback reservation are preserved. Later same-namespace writes cannot overtake an accepted Query. |
| Foreground priority could prevent a waiting head from preparing | Maintenance may run when no foreground head can execute; ready foreground work has priority between bounded batches. Preparation may outlive a particular Query, which terminates once. |
| Boolean-only ordered discovery conflicted with equality-only semantics | Boolean requires Equals. Explicit invalid shape rejects synchronously; stored-state discovery can produce one InvalidQuery completion. Type presence is tracked independently of representation health, preventing false empty results. |
| A 512-byte cursor could not preserve an arbitrary 1,024-byte string boundary | A self-contained authenticated cursor has a 1,283-byte raw/1,712-byte encoded proof and 2-KiB ingress ceiling. It requires no retained cursor registry and no lookup of a changed/deleted boundary record. |
| Two maximum string bounds already exceeded the old 2-KiB descriptor | Private descriptor ceiling is 6 KiB; the complete maximum field/bounds/cursor/framing allocation is at most 4,672 bytes. |
| IPC fit alone did not establish safe Query result reservations | Keep the 66-KiB page and 68-KiB frame, prove a maximum record plus continuation fits, and require Query-sized reservations within the unchanged 18-MiB transport cap. |
| Atomic build completion and failed building maintenance were implicit | Checkpoint/entries/accounting commit together; publication is atomic. Failed building maintenance prevents activation. ACTIVE state remains correct or is atomically withdrawn with a safe primary commit. |
| Historical handoff and release routing disagreed with D22 | Mark the old schema-oriented Foundation 1 handoff explicitly historical, preserve its tested SHA, and route current release/API/navigation text to structured Query and optional field-name hints. |

The five-second waiting replacement is based on
[D21 FIFO/deadlines](PersistenceFoundation1.md#8-serialization-scheduling-and-lifecycle),
[D21 bounds/reservations](PersistenceFoundation1.md#5-logical-names-and-bounds),
`StorageQueue.Submit`/`Dispatch` and the qualified
[1B admission/lifecycle evidence](PersistenceFoundation1B-Validation.md#public-api-tests-and-authority).
No existing Get/Set/Remove deadline, ordering or authority rule is amended.

The separate Query token bucket remains removed. Inspection found no implemented
Query workload or evidence of a distinct unbounded resource class requiring it.
The common D21 request buckets, pending/transport bounds, namespace fairness,
bounded Query pages/work and operation deadlines are the selected controls.
Actual performance and plan qualification remain 2C/2D gates.

## Stale-contract audit

Scope: all Markdown under `docs`, plus AICONTEXT, README and CHANGELOG; review
current canonical routing, public API descriptions, implementation phases,
decision summary, examples, stop conditions, release text and tooling requirements.
The required literal search terms were evaluated individually, with an additional
case-insensitive search for persistence language/schema/version/hint contracts.

The following terms describe the audit and superseded designs only; they do not
authorize any API:

| Search term/family | Disposition |
|---|---|
| `Where`, `Clause`, `Operator`, `Predicate`, `Expression` | No active Query language, comparison parser, AST or filter callback. Ordinary prose, server-operator references, publication predicates and unrelated tooling/GUI expressions are not persistence Query contracts. |
| `typed hint`, `Coins = "number"` | No active typed hint form. Typed hints appear only as explicitly deferred; active hints are string lists. |
| `IndexVersion`, `SchemaVersion`, `MigrationVersion` | No author persistence version. Existing tooling protocol SchemaVersion fields retain their independent owner; they are unrelated to DataStore. |
| `Version =` | Foundation 1 application payload examples may store an ordinary field called Version. They do not require a storage schema/version; preserving arbitrary application fields is part of the schemaless value contract. The language-analysis policy version is unrelated. |
| Earlier Foundation 1C Query handoff | Explicitly historical/non-normative for Foundation 2; D22 owns the current design. Historical Foundation 1 qualification and negative evidence are preserved. |
| Metadata/phases/ledger/stops/navigation | Structured Field/Type/Equals/Min/Max/Direction/Limit/Cursor only; no production metadata or placeholder API added. |

Internal fixed statement families, comparison values, format identities and
accounting remain private correctness mechanisms. Their presence is not a public
expression/schema/migration protocol.

## Validation and evidence limits

Required before publication: existing `Test-Api.ps1` (including relative links),
`Test-Architecture.ps1`, `Test-ToolingContracts.ps1`, release consistency, Markdown
fences/headings and changed-route fragment checks; literal/semantic stale-contract
review; D21 section equality; metadata/runtime/version diff exclusion; no-reply
Git identity and clean synchronized main.

Local results: **PASS** API/relative links, architecture checks, tooling contracts
and release consistency/deterministic source packaging. The Markdown fence and
fragment audit checked 13 changed documentation files and 559 relative links/
fragments. Exact D21 section comparison and diff exclusions prove no D21,
Foundation 1 contract, production source, generated API or release-manifest
change. The private cursor/descriptor/page arithmetic above was checked directly.
The literal and semantic stale-contract reviews found only the historical,
explicitly deferred and unrelated uses classified above. Reading the ordinary
API sections requires only a store, a field/value request, optional field hints
and a callback; no author database lifecycle machinery is required.

An independent read-only consistency review confirmed the corrected waiting,
boolean, cursor/page and atomic-publication model. Its final follow-up corrected
cursor versus comparison-string bounds, the untagged cursor scalar payload and
the bounded ready-to-waiting saturation outcome. These are design checks, not
claims of implemented behavior.

Final hosted documentation and architecture/API checks run on the amendment
commit. Their immutable run links and result are supplied at handoff; the
Documentation workflow deploys this record. Runtime/build jobs triggered by
repository workflows are regression evidence only and do not prove Query exists
or qualifies on SQLite/Windows/Linux. No live server or production change is
needed for the documentation-only gate in Compatibility.

GitHub release/tag inspection on 2026-09-29 found v0.3.0 and v0.4.0 releases and
no 0.5 tag/release. The unchanged release manifest maps package 0.4.0, scripting
API 0.5.0-experimental, native ABI 1.5, provider protocol 1.2, package schema 1
and the existing Luau pin. Future Foundation 2 metadata must follow actual
qualified bindings. No package bump, tag, release or editor publication occurs.

## Completion report

This checklist routes to the authoritative detail instead of establishing a
second contract. PASS/READY here means design consistency, not runtime proof.

| # | Item | Result |
|---|---|---|
| 1 | Verdict | CANONICAL BASELINE READY; final commit hosted verification remains a handoff gate |
| 2 | Starting commit | 97bc514fb94a3739ab9fbad91c04c2d40a9c3534 |
| 3 | Superseded D22 | Reviewed structured correction 97bc514; comparison-language predecessor 6dd5f7a; initial record ee20883 |
| 4 | Amendment commit | Introducing commit of this file, supplied in handoff |
| 5 | Final tested commit | Same amendment commit; hosted results verified at handoff |
| 6 | D22 status | Resolved architecture; implementation NOT STARTED |
| 7 | Prior production implementation | None; doc-only history and current bindings inspected |
| 8 | Query signature | DataStore:Query(Request: DataStoreQuery, Callback: (DataStoreQueryResult?, string?) -> ()) -> () |
| 9 | Request | Field required; Type, Equals, Min, Max, Direction, Limit, Cursor optional; exact types in section 4 |
| 10 | Equality | Equals scalar selects exact type; exclusive with Min/Max; false is a value |
| 11 | Range | Min and/or Max of one number/string type; contradictory bounds invalid |
| 12 | Inclusion | Both bounds inclusive; exclusive controls deferred |
| 13 | Inference | Equals or bounds select type; explicit Type must agree |
| 14 | Ordered ambiguity | Sole represented scalar type selected; multiple require Type/return AmbiguousFieldType; boolean still needs Equals |
| 15 | Hint form | Optional Indexes = { "Coins", "Level" } |
| 16 | Typed hints | Deferred; not accepted |
| 17 | Author versioning | No schema/version/migration/index-lifecycle ceremony |
| 18 | Automatic preparation | Cold Query begins/joins bounded work without a declaration |
| 19 | Fields | Eight retained fields/store, automatic and hinted combined; no automatic eviction |
| 20 | First Query | Ready state executes once; cold state waits boundedly, else one IndexPreparing |
| 21 | Waiter bounds | 8/ns, 32 global subset of 8/128; five seconds total from original acceptance |
| 22 | Online build | Durable bounded keyset/checkpoint batches; atomic completion/publication; restart resumption |
| 23 | Set/Remove during build | Normal D21 admission/FIFO; maintain building state transactionally or make it ineligible |
| 24 | ACTIVE invariant | Safe primary commit ends with correct ACTIVE state or affected state atomically not ACTIVE |
| 25 | Primary authority | Unchanged Foundation 1 schemaless values |
| 26 | Derived accounting | Separate 16 MiB/ns, 64 MiB/global; all retained entry/metadata generations charged atomically |
| 27 | Result | Items = array of {Key, Value}; optional NextCursor; fresh Foundation 1 snapshots |
| 28 | Count | Default 50; 1..100 |
| 29 | Memory/bytes | 66-KiB page; 8,192 expanded entries including wrappers; D21 individual value/18-MiB transport limits; private descriptor 6 KiB |
| 30 | Work | At most 101 derived candidates/primary point lookups; 1,000,000 VM instructions; original request deadline |
| 31 | Ordering | Selected field then exact record key; descending reverses both |
| 32 | Pagination | Opaque keyset continuation; no numeric offset |
| 33 | Cursor | Same authority/selection/type/direction and valid private state; authenticated bounded token; invalid continuation is InvalidCursor |
| 34 | Between pages | Each call has its own transactional snapshot; writes may change membership/order |
| 35 | Queue/fairness | Existing FIFO/fair ledger and reserved completion; waiting cannot overtake or reset deadlines |
| 36 | Query rate bucket | None; evidence/rationale above; D21 general request buckets apply once |
| 37 | Strings | Exact case-sensitive UTF-8 bytes; 1,024-byte queryable scalar; longer primary strings remain legal, selected string state unavailable |
| 38 | Numbers | Finite binary64; signed zero canonicalized only for Query comparison; primary value fidelity unchanged |
| 39 | Boolean | Equality only, deterministic key tie ordering |
| 40 | Missing/wrong type | No participation; legal primary data; incomplete selected state never returns false absence |
| 41 | Public errors | InvalidQuery, AmbiguousFieldType, IndexPreparing, QueryUnavailable, InvalidCursor plus applicable D21 failures |
| 42 | Corruption/repair | Trusted-primary derived logical failures withdraw/rebuild boundedly; physical/primary failures remain D21 |
| 43 | No-scan/plan | No primary Query fallback; exact pinned SQLite plan proof for all families is mandatory in 2C |
| 44 | SQL/security | Fixed statements/bound values/internal IDs; no author SQL, operators, identifiers or raw host authority |
| 45 | Foundation 1 | Existing bindings, values, quotas, namespaces and evidence preserved |
| 46 | D21 | Authoritative text unchanged; waiting correction conforms to its deadline/FIFO/reservations |
| 47 | API identity | Future additive 0.5.0-experimental; no package publication |
| 48 | Phases | 2A private substrate; 2B demand/hints; 2C public structured Query; 2D combined qualification |
| 49 | Deferred | Full explicit list in detailed record section 22; no speculative hooks |
| 50 | Local validation | Mandatory architecture/documentation audits described above |
| 51 | Hosted CI | Final commit documentation and architecture/API results supplied at handoff |
| 52 | New production API | None; no Query/schema/index implementation or generated metadata |
| 53 | Branch/worktree | main; final synchronization/clean-tree verification at handoff |

Persistence-2A may begin only after this correction's architecture/documentation
gates pass and under a separately authorized implementation task. No phase is
implemented here.

**D22 FINAL CANONICAL BASELINE READY → Persistence-2A may begin.**
