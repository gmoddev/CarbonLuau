# Persistence-2A single-database bound investigation

**Historical proof gate:** The 2026-09-30 approved capacity correction removes
the requirement that every below-quota logical state fit every permitted
physical history. The negative proof result and measurements below remain
intact, but its direction to split storage is superseded. See
[D21](Invariants.md#d21--persistence-foundation-1) and
[Foundation 2](PersistenceFoundation2.md#15-derived-state-resource-accounting).

Date: 2026-09-30. **SINGLE-DB BOUND NOT PROVEN — PROCEED TO SPLIT ARCHITECTURE.**

This is the task's unsuccessful-proof disposition, **not a theorem that every
single-database design is impossible**. No larger ceiling is selected. A split
architecture investigation is the next permitted architecture task, not an
approved implementation. Persistence-2A remains blocked; 2B remains closed.

## Authority, provenance and preservation

[D21](Invariants.md#d21--persistence-foundation-1),
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query),
[Foundation 2](PersistenceFoundation2.md), and qualified Foundation
[1A](PersistenceFoundation1A.md), [1B](PersistenceFoundation1B-Validation.md) and
[1C](PersistenceFoundation1C.md) remain unchanged. This record owns investigation
results, not a replacement persistence contract.

Started and fetched `origin/main` at
`928b9d7b60fcdba508b322fbc824c83ccab5cc9f`; local `main` already matched it.
All existing dirty production/test/research work was preserved. The supplied
“Backend/Layout Redesign” attachment is an investigation input; its unresolved
Option A is evaluated here, not treated as a proved cap amendment. Historical
[2A evidence](PersistenceFoundation2A.md) remains intact, including original
overflow failure, unchanged postfailure integrity/hash, compact feasibility,
511.3515625-MiB successful sample, only 664-KiB headroom, earlier Windows/Linux
migration results, and excluded results after worker storage failure.

Inspected exact SQLite 3.53.4 source ID:
`2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`.
Amalgamation SHA-256:
`b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189`.
Source line references below refer to that file, not an arbitrary system SQLite.

Candidate SHA-256 identities:

| File | SHA-256 |
|---|---|
| `native/src/persistence/Backend.cpp` | `945ab07d82899c6b2691f8277def2505c2216ec402b2aabb7a9a0440114cb579` |
| `native/src/persistence/Derived.cpp` | `0b6678809eea54a26bb4f4197688fdf25c1a03c1fe744b59e1379d9b3309e5b0` |
| `tests/persistence/storagefit/Probe.cpp` | `ddd0ba38edb272a3efc1496e9a36d4a497a0ddf8bce5d1eb7a75c13b18e4aad9` |
| `tests/persistence/storagefit/Migration.cpp` | `cdcf43cee495b44ba3d6342a78f732daab13168bdb494ce09beb2bc83f79906e` |
| `tests/persistence/storagefit/Shapes.cpp` | `814521ee5df5077bc483946b04d0ba988f0c9e9ef0ffd43d405195b2af6d834b` |

No production source, canonical limits, SQLite pin, public API or version identity
was changed in this investigation. No DB was deleted or mutated.

## 1. Exact candidate and representation boundary

Select the best-supported **768-byte chunk prototype** for analysis, retaining
the unmodified split derived engine. This is a selection for proof, not production
adoption. Do not select a different chunk size based on the most favorable sample.

Page size and usable bytes are 4,096, reserved bytes zero; auto-vacuum NONE,
cache spill OFF, mmap zero, temp storage MEMORY, PERSIST/EXTRA, journal limit -1.
Production still has legacy primary storage. The compact form exists only in
the standalone research target:

| Relation | Representation and indexes |
|---|---|
| Stores | rowid table; `Id INTEGER PRIMARY KEY`, Namespace BLOB, Store BLOB; UNIQUE(Namespace,Store) |
| RecordIdentity | rowid table; `Id INTEGER PRIMARY KEY`, StoreId, Key BLOB, Charge; UNIQUE(StoreId,Key) |
| Chunks | WITHOUT ROWID; `(RecordId,Part)` primary key, Payload BLOB <=768 bytes |
| Quotas | unchanged WITHOUT ROWID; Namespace primary key, Bytes, Keys, Stores |
| Totals | unchanged rowid table; singleton Id, Bytes, Keys, Namespaces |
| DerivedTotals | singleton rowid table; NextField, NextGeneration, NextPrefix, NextEntry, WorkGeneration, Bytes, Charge |
| DerivedNamespaces | WITHOUT ROWID; Namespace primary key, Bytes, Charge |
| DerivedFields | rowid Id, Namespace, Store, Name, Charge; UNIQUE(Namespace,Store,Name) |
| DerivedGenerations | rowid Id, FieldId, State, Checkpoint, Members, Booleans, Numbers, Strings, Oversized, Charge; indexes `(FieldId,Id)` and partial `(Id) WHERE State IN (1,3)` |
| DerivedPrefixes | rowid Id, Generation, Prefix, Refs, Charge; UNIQUE(Generation,Prefix) |
| DerivedEntries | rowid Id, PrefixId, Suffix, RecordKey, Charge; UNIQUE(PrefixId,Suffix,RecordKey) |
| DerivedMembers | WITHOUT ROWID; primary key `(Generation,RecordKey)`, Type, EntryId, Charge; partial UNIQUE(EntryId) WHERE EntryId<>0 |

The exact CREATE statements are in `Probe.cpp`, `Backend.cpp` and
`Derived::Schema()`. All ordinary integer/BLOB fields are NOT NULL. Primary-key
aliases use SQLite rowids; the other keys are SQLite index B-trees. Secondary
indexes include the rowid or missing WITHOUT ROWID primary-key suffix, not merely
the explicitly named columns.

The prototype initially calls RecordIdentity `Records`, then renames it and
creates a compatibility view `Records` using `ResearchEnvelope(Id)`. That test
UDF performs nested chunk SELECTs. Its comment explicitly disclaims a production
adapter. Thus **there is no complete compact production statement inventory to
certify**. In particular, durable directory reclamation, ID allocation history,
dual-format foreground operations and schema retirement are not implemented by
this research adapter.

The envelope/checksum codec is unchanged. Namespace is the one-byte root/addon
tag plus package ID (<=66 bytes); Store <=64, Key <=128. Tagged sort keys are
boolean (2 bytes), finite number (9) or UTF-8 string (<=1,025). They split into
Prefix <=512 and Suffix <=513; duplicate prefixes share a charged directory row.
Private IDs can grow to positive signed-64-bit values; small fixture IDs are not
a lifetime width bound. No SQLite object or ID is public.

## 2. Proven cardinality and cell-size bounds

The offline, dependency-free [proof ledger](../tests/persistence/storagefit/SingleDbBounds.py)
uses integer arithmetic, SQLite serial-type/varint rules and explicit nulls for
unproved totals. It neither opens SQLite nor changes a page limit:

```powershell
py -X utf8 tests/persistence/storagefit/SingleDbBounds.py --sqlite-source build/persistence2a/single-db-proof/sqlite3.c
```

It accepts primary-byte/record/derived-byte maxima for sensitivity checks. Only
the defaults are canonical; changing a script argument does not amend D21/D22.
Exit zero means its arithmetic self-checks passed, **not** that the DB bound passed.

### Primary counts

Let Q=268,435,456, N be record count, E_i envelope bytes, C=768.
The smallest boolean envelope is 46 bytes. Store/key each cost at least one:

```text
N <= min(100000, floor(Q/48)) = 100000
sum(E_i) <= Q - 2*N
K = sum(ceil(E_i/C))
K <= floor((Q + (C-3)*N)/C) <= 449134
chunks/value <= ceil(65536/768) = 86
```

The 100,000-record ceiling is explicit and attainable with small values spread
over legal namespaces; it is not an assumption that the previous fixture was
worst case. The chunk ceiling is a conservative relaxation, not an attainability
claim: unique keys and store lengths can consume more than the minimum charged
bytes. It improves the earlier loose 449,526 bound without assuming insertion
order, concentration, value size or favorable final-chunk remainders.

Nonempty primary namespaces <=256, nonempty primary stores <=16,384, quota rows
<=256, Totals one. Those are **not** automatically bounds on retained empty compact
Stores directory rows: a production lifecycle/reclamation rule is missing.
They also do not bound historical derived-only stores/namespaces.

### Derived counts and coexistence

Exact logical row charges in the candidate are:

```text
global     65
namespace  21 + NamespaceBytes
field      29 + NamespaceBytes + StoreBytes + FieldBytes
generation 70 + CheckpointBytes
prefix     37 + PrefixBytes
entry      33 + SuffixBytes + RecordKeyBytes
member     30 + RecordKeyBytes
```

All coexist within the **same** 64-MiB global / 16-MiB namespace pool. Independent
conservative count bounds from `(64 MiB-65)/minimum row charge` are:

| Row kind | Independent upper count |
|---|---:|
| namespace | 3,050,399 |
| field | 2,097,149 |
| generation | 958,697 |
| prefix | 1,766,021 |
| entry | 1,973,788 |
| member | 2,164,799 |

These cannot all be attained together: required parent rows, per-namespace limits
and correlated keys consume the same pool. They deliberately overestimate and
must not be presented as allocated objects in a measured workload.

`Generations()` enforces **two total retained generations/field**, <=one ACTIVE
and <=one BUILDING. Cleanup occupies a slot. ACTIVE+BUILDING+third stale is not
a legal candidate state. All checkpoint/member/prefix/entry/cleanup bytes remain
charged until deletion commits. Fields and namespace ledgers are retained and
charged, even without primary records. The 8-fields/store bound is not a global
field-count bound derived from *currently nonempty* primary stores.

Charge checks happen before allocations inside savepoints. This bounds retained
logical state, not transaction undo images, SQLite's free-page high-water,
temporary primary old/new coexistence or allocator overhead.

### SQLite cell theorem (not a packing theorem)

Exact source `sqlite3.c:74412-74434,76666-76669` gives:

```text
U = 4096
index maxLocal = floor((U-12)*64/255)-23 = 1002
index minLocal = floor((U-12)*32/255)-23 = 489
table-leaf maxLocal = U-35 = 4061
overflow payload/page = U-4 = 4092
```

Above maxLocal, local payload is `489+(P-489)%4092` if <=maxLocal,
otherwise 489; the remaining bytes require overflow pages. A leaf header is
8 bytes, an interior header 12; every cell has a two-byte page pointer. Page 1
also has the 100-byte DB header. Index interior cells add a four-byte child
pointer; table leaves add a rowid varint (up to nine bytes). Record headers
include serial-type varints and their own length varint. Integer data occupies
at most eight bytes. Rowid aliases store NULL in the payload.

The ledger conservatively uses eight bytes for every non-alias integer, including
small counters/Part, and includes secondary-key suffixes. Maximum serialized
payloads are 789 bytes for Chunks, 665 for DerivedEntries, 664 for its secondary
index, 543 for DerivedPrefixes and 533 for its secondary. All twenty enumerated
data/index record shapes are below 1,002. **Those compact rows need zero overflow
pages** even at maximum IDs/keys. Legacy Records, sqlite_schema SQL text and
migration schema/bootstrap are not covered by that statement.

This removes the old primary overflow amplification. It does **not** certify
minimum occupancy of every page. For example, fitting five chunk cells in a
page does not mean every reachable page contains five cells.

## 3. Missing occupancy/high-water theorem

`balance()` (`sqlite3.c:82368`) may skip rebalancing when at most two thirds of
usable space is free. This branch is not a proof that all outputs of
balance_quick/deeper/nonroot, roots, index separator pages and delete/rebalance
paths always satisfy one global minimum-fill ratio. Interior fanout/tree height,
sibling redistribution and the transient pages allocated before freeing old
ones still require induction over supported operations. The file format's cell
capacity gives an upper fanout, not the required lower fanout/height proof.

No such all-history induction was established here. Consequently there is no
certified sum for primary trees, derived trees, metadata/secondary indexes and
transient pages. Charging one page per possible chunk alone would allow
1,839,652,864 bytes; that loose estimate does not fit and is **not** evidence that
this bad packing is reachable. Substituting a convenient 2x/3x ratio is not proof.

Freelist pages are reused by ordinary SQLite allocation, but EOF need not shrink.
The required bound is the maximum *simultaneously needed* tree/freelist/temporary
state over every legal history, not current live payload plus all past free
pages. Fill/delete/shrink/refill, random keys, wide IDs, chunk-count changes,
generation withdrawal/rebuild, crash/reopen and migration must share that bound.
No VACUUM or new maintenance operation is assumed. The previous 511.3515625-MiB
sample is a lower bound on required capacity for its history, not a universal
upper bound. The 512-MiB clamp limits successful files by rejecting work; it
cannot prove all legal logical states can be accommodated.

**Proven maximum reachable EOF sufficient for all legal states: not established.**
No new hard ceiling or guaranteed headroom follows.

## 4. Complete transaction audit and dirty-page status

Let N0 be database pages at BEGIN. For every write transaction the unconditional
distinct-original-page bound is N0 (<=131,072 under the unchanged cap). That is
not an assertion that the transaction actually dirties every page. The tighter
family-specific values below are **not proved**, rather than guessed from row
counts. Page 1, all touched secondary indexes, sibling/ancestor balancing and
freelist trunk/allocation changes must be added before a row limit becomes a
page limit. Newly appended pages need truncation on rollback, not original-page
journal images. Reused pre-existing pages require SQLite's appropriate undo.

| Family | Bounded logical work / affected relations | Certified original-page ceiling |
|---|---|---|
| Get on ready backend | point read, decode; startup recovery separate | 0 |
| new Set | <=86 chunks, record/store identity and unique indexes, Quotas/Totals; derived work below | N0 only |
| same-chunk overwrite | old/new chunks + metadata + derived work | N0 only |
| growing overwrite | <=86 old / <=86 new chunks; allocation/splits | N0 only |
| shrinking overwrite | chunk replacement/deletion and rebalance/freelist | N0 only |
| Remove | <=86 chunks, identity/index removal, Quotas/Totals, derived removal | N0 only |
| primary accounting | namespace/global rows; store/key count and empty-namespace removal | part of Set/Remove; N0 only |
| ACTIVE maintenance | at most 8 fields, old/new member/entry/prefix + indexes/counters | part of Set/Remove; N0 only |
| BUILDING dual maintenance | <=2 non-cleanup generations/field, <=16 sequential derived savepoints | part of Set/Remove; N0 only |
| withdrawal | state update and partial work-index change; not full generation deletion | N0 only |
| derived accounting | namespace/global ledgers and ID counters, repeated updates share pages | part of relevant outer transaction; N0 only |
| Prepare/retry | bounded namespace/field/generation admission and accounting under savepoint | N0 only |
| build batch/checkpoint | <=32 records / 256 KiB envelopes for one generation; entry/counter changes and checkpoint | N0 only |
| final publication | end of bounded build, withdraw prior ACTIVE and publish BUILDING | part of build; N0 only |
| stale cleanup batch | <=32 members + entry/prefix/index removals; delete exhausted generation, retain field/namespace | N0 only |
| migration batch/checkpoint | intended insert compact, delete legacy, checkpoint; <=32 / 256 KiB | no complete adapter; N0 is only distinct-original-page bound |
| initial/schema upgrade | fixed tables/indexes, format marker; legacy->derived version upgrade | N0 only; fresh DB still has appended pages/cache cost |
| startup logical quarantine | Validate(true) may withdraw every mismatching trusted generation | N0 only; **not a 32-row transaction** |

The last row matters: `Backend::Open()` owns one BEGIN IMMEDIATE around primary
validation and `Derived::Validate(true)`. The latter streams all generations and
can call Withdraw repeatedly before COMMIT. It has a startup deadline, not a
32-generation count. Time is not a machine-independent dirty-page theorem.
Healthy validation is read-only, but the legal repair case cannot be excluded.

**Worst family:** no tight comparative maximum is proved. Startup quarantine is
the unbatched family; migration and maintenance remain separate unresolved maxima.
Neither “32 records per transaction” nor “86 chunks per Set” proves the requested
global journal allowance. No evidence establishes that all DB pages really must
be journaled, either; the tighter theorem remains absent.

## 5. PERSIST formula and inherited high-water

The existing exact-pin pager map in `PhysicalTests.cpp` was rechecked:

- journal starts at offset zero (`65567-65570`);
- original pages are tracked in pInJournal; record is page+8 (`65711-65714`,
  `65770-65775`);
- spill OFF suppresses syncJournal(...,1); commit uses (...,0), hence no
  spill-generated additional headers (`64317-64345`, `66293`);
- header sector is at most 65,536;
- stale-next-header invalidation only writes after an existing successful read,
  so it cannot extend the file (`64015-64019`);
- rollback-to retains transaction pInJournal and restores effective journalOff
  (`63098-63190`); it uses a separate replay bitmap;
- PERSIST/-1 zeroes the first 28 bytes without truncation (`61034-61064`).

Under the fixed no-auto-vacuum/no-ATTACH/no-page-relocation workload, with D
original pages journaled and H the one actual sector-sized header:

```text
new journal extent <= H + D*(4096+8), H <= 65536
retained extent <= max(inherited extent, 65536 + 4104*max_transaction_D)
```

There is no super-journal. Incompatible hot headers remain rejected before
recovery. A future page-moving/schema path must re-establish these preconditions;
the pager's page-move error path can clear pInJournal (`66946-66960`).

The formula is useful, but **does not supply D**. With only D<=131,072 the
unchanged allowance is 537,985,024 bytes = 513.0625 MiB. There is no certified
smaller retained allowance for this candidate.

Further, the existing backend admits a nonhot journal up to that historical EOF
limit. A future small-write theorem alone cannot shrink an already-retained
journal. One must distinguish accepted on-disk inputs from files reachable by a
specific qualified workload. This investigation does not assert that Foundation 1
single-key operations actually produced a 513-MiB journal. It establishes that
new transaction size alone is insufficient to replace the current retained-file
contract. Manual truncation or a positive journal_size_limit would change the
approved profile and is not adopted.

## 6. Savepoints, subjournal and memory

`temp_store=MEMORY` passes the in-memory choice to `sqlite3PagerBegin`
(`76942`); `openSubJournal` uses the memory-only journal (`64200-64216`). A
subjournal record is 4,096+4 bytes (`64246`), distinct from the main journal.
It can capture pages appended since transaction BEGIN but present at a later
savepoint. Main-journal D alone does not bound it.

`pagerOpenSavepoint` allocates a PagerSavepoint and Bitvec per open savepoint.
On x64 a Bitvec is 512 bytes, with 496-byte union, 3,968 bits or 62 child pointers
(`56286-56360`). The ledger bounds a fully populated tree recursively by:

```text
B(n) = 512                                      if n <= 3968
d = max(ceil(n/62), 3968)
B(n) = 512 + ceil(n/d)*B(d)                     otherwise
B(131072) = 17920 bytes
```

This bounds one bitvec's object storage, **not total memory**: malloc bookkeeping,
rehash scratch, savepoint structures, replay bitmap, statement journals, page
cache, decoded values and process baseline are additional. Rehash uses temporary
hash-array storage (`56457-56478`); rollback allocates pDone (`63098`).

Do not multiply memory by 16 sequential savepoints blindly. RELEASE may truncate
an in-memory subjournal and restore nSubRec (`66715-66723`), but nested/statement
savepoint interactions can clear bTruncateOnRelease (`60708-60711`). That path and
maximum retained subjournal records require a complete statement/failure-path
analysis. The compact statements are not yet complete. With spill OFF the
4-MiB cache setting is not a hard bound on dirty cache memory.

**Savepoint/process-memory gate remains OPEN.** No claimed 256-MiB proof; no
disk-backed subjournal added, no memory/worker cap increased. The prior 635,256-byte
shape-process and ~4.57-MB migration SQLite high-water observations do not bound
full process memory or worst Set+eight-field dual maintenance.

## 7. Migration and crash boundary

Requested architecture: read <=32 legacy rows / 256 KiB, insert compact rows,
delete legacy rows, advance durable checkpoint, COMMIT. That preserves values
and quota atomically but requires transient old+new space before reclamation.
The new compact chunk count per batch is bounded by
`floor((262144+767*32)/768)=373`; this is not a complete page bound.

Existing `Migration.cpp` instead **deletes first**, then bootstraps destination
tables and inserts chunks in the same transaction. It uses remaining legacy rows
as a durable worklist, with CompactState.Complete and a research version marker.
The source fixture copy is test setup, not a second DB in the migration design.
Quotas/Totals remain unchanged. Empty legacy schema is retained for verification.

Existing results are preserved, not reassigned to a different algorithm:

- genuine format-1 live-restart fixture: three exact rows, Windows/Linux,
  4->10 pages, four process-exit boundaries and resume;
- 512-MiB format-2 failure fixture with unchanged legacy primary representation:
  100,000 exact rows, 3,125 batches, final 131,072 pages / 54,676 freelist pages;
- four-page diagnostic clamp: controlled FULL and unchanged hash, not an
  actual near-ceiling universal-migration counterexample;
- Linux sanitizer/fault observations remain scoped to those old probes.

None proves insert-before-delete at a genuinely full format-1 file with no free
bootstrap pages. A larger cap must cover first schema creation, both
representations, unique indexes, checkpoint, balancing and journal/cache cost.
No universal transient EOF or dirty-page maximum was established. No new
migration/crash qualification ran this turn. Crash atomicity of the measured
delete-first prototype is useful evidence, not a proof of the unimplemented
mixed-format online service or requested migration peak.

## 8. Envelope algebra, not a ceiling selection

Preserve the existing 64-MiB DB allocation variance, 64-MiB journal variance and
8-MiB diagnostic allowance. Backend::CheckFiles admits two lock files, each
with <=64-KiB allocated bytes (EOF <=4 KiB), so use **128 KiB** for existing
auxiliary allocation rather than silently budgeting only one. No new migration
sidecar is assumed; its in-DB state still needs the occupancy proof.

The current conservative equation is:

```text
512 + 513.0625 + 64 + 64 + 8 + 0.125 = 1161.1875 MiB
1280 - 1161.1875 = 118.8125 MiB unassigned operational margin
```

This accounts for existing file allowances, not proof of compact-state capacity
or every filesystem's physical blocks. The 1,280-MiB figure remains D21's
operational target, not a hard filesystem theorem.

If the whole-DB journal bound remains necessary, its size grows with the cap.
With N page slots and **zero** new reserve/auxiliary state:

```text
4096*N + (65536 + 4104*N) + 128 MiB + 8 MiB + 128 KiB <= 1280 MiB
N <= 146265, DB <= 599101440 bytes = 571.34765625 MiB
```

This is only an algebraic necessary limit under that conservative journal model,
**not** a sufficient capacity theorem or proposed ceiling. Holding the old
513.0625-MiB journal constant yields 630.8125 MiB before reserve, but requires
the very tighter dirty-page/inheritance proof still missing. It cannot be used
to select a cap. A safety reserve would reduce both numbers further.

No secondary allowance was reduced; no new DB size, max_page_count, physical
guarantee or reserve is adopted. There is no defensible exact D21 amendment to
propose on this evidence. D22 public semantics need no change.

## 9. Instruction certificate and platform evidence

The existing `DerivedBudgetTests.cpp` models 76 extracted SQL families, measured
maximum 50 opcodes/program, unchecked span 45, outer rollback four. Existing
observations up to 14,405 steps do not certify a new compact adapter. It still
needs fail-closed family coverage and exclusion/accounting of nested execution
(including SqlExec/ParseSchema/Vacuum and unsupported virtual-table operations).
The test UDF's nested chunk statements cannot be qualified by measuring only an
outer statement. The runtime's aggregate progress-handler and five-second
backstop remain unchanged; no threshold was raised.

Foreground chunk operations, dual-format writes, schema conversion and migration
are not all in the candidate engine's certificate. **Exact VDBE gate remains
OPEN**; there is no claim that the unknown maximum fits D22's 1,000,000 limit.

This turn recovered read-only access to dockerbox and downloaded prior logs and
the exact source. Its C: drive reported **442,417,152 free bytes**. No new capacity
probe/build was launched on that constrained drive. Legacy SCP worked; the
modern SFTP/PowerShell SSH attempts did not complete successfully. The earlier
Docker read-only fault is not presumed repaired. No Docker restart, NIC/security
change, fixture deletion or unrelated-workload cleanup was attempted.

| Evidence | Status |
|---|---|
| Local Windows exact-integer ledger, exact source SHA verification | Newly run; PASS arithmetic only |
| Exact-pin source review | Newly performed; partial formula/cardinality/cell certificates above |
| Per-transaction dirty-page instrumentation | **Not measured this turn**; prior VFS file extents are not distinct page-set measurements |
| Windows native adversarial/page-boundary rerun | **Not measured**; worker storage prerequisite unhealthy |
| Linux exact-pin compact/journal rerun | **Not measured**; old read-only-volume failure unresolved |
| ASan/UBSan/leak/fault | Inherited shape/migration evidence only; no new native instrumentation or sanitizer run |
| Hosted CI / production package | Not run; no production changes or qualification claim |

Required before any future production adoption: healthy measured worker/volume
space; new exact-pin page-set/extent and subjournal instrumentation; split/deep-tree,
freelist, wide-ID, dual-generation, bulk startup-quarantine and migration cases;
Windows/Linux reruns; sanitizer/allocation/I/O/process-exit checks. Instrumented
observations must test a previously stated bound, never define it by maximum+margin.

## 10. Completion ledger and next boundary

| Requested items | Result |
|---|---|
| 1 | SINGLE-DB BOUND NOT PROVEN — PROCEED TO SPLIT ARCHITECTURE; not a universal impossibility proof |
| 2–3 | Start 928b9d7; prior dirty implementation/research preserved |
| 4–5 | Selected 768-byte compact prototype + unchanged split derived schema; 4096/4096 page/usable bytes |
| 6 | Maximum legal primary record count 100,000; conservative chunks 449,134 |
| 7–9 | Cell/overflow and cardinality results established; complete physical-page/metadata/index packing theorem NOT PROVEN |
| 10 | <=2 total retained generations/field, all charged, no third free stale generation |
| 11–12 | All-history fragmentation/transient EOF theorem and sufficient maximum DB pages/bytes NOT PROVEN |
| 13–15 | Families enumerated in section 4; Get=0, writes only N0 original-page ceiling; no proved tight worst family |
| 16 | New empirical dirty-page validation not measured |
| 17–18 | Conditional max(inherited,65536+4104*D); smaller retained high-water not proved; existing 537,985,024 bytes unchanged |
| 19–20 | Memory subjournal/bitvec behavior reviewed; one x64 bitvec <=17,920 bytes at current cap excluding allocator; aggregate memory gate OPEN |
| 21–25 | Insert-before-delete intended; prototype delete-first; universal peak/dirty bound and new near-ceiling/crash proof absent; historical results preserved |
| 26–27 | No new hard DB ceiling or guaranteed headroom selected |
| 28–30 | DB variance 64 MiB, journal variance 64 MiB, diagnostics 8 MiB, existing two-lock auxiliary 128 KiB retained |
| 31–32 | Existing equation 1161.1875 MiB and 118.8125 MiB unassigned margin; no solved replacement envelope/reserve |
| 33 | Compact aggregate instruction proof OPEN |
| 34–36 | Local Windows arithmetic new; native Windows/Linux and sanitizer/fault results inherited only; new runtime reruns not measured |
| 37–38 | D21 amendment required **before any larger cap**, but no exact defensible replacement exists; none proposed/adopted |
| 39 | D22 public amendment: no; future private architecture changes need separate review |
| 40 | Production changes not authorized by this result; next architecture must define bounded layout, migration, accounting and resource proofs before implementation |
| 41 | Storage/history fit, transaction journal, migration, instruction and savepoint-memory gates plus healthy-worker validation remain open |
| 42 | No public Query/hints/cursor/schema/version API, no 2B work |
| 43–44 | No commit/push/release/tag; main remains 928b9d7 synchronized with fetched origin/main, intentionally dirty |

The supplied decision rule directs the next task to **split architecture
investigation**, not another empirically chosen single-DB cap. That task must
preserve D21/D22 and prove cross-storage failure/atomicity, migration and the same
operational/resource bounds. This record does not choose or implement that design.

### Local closing checks

- Exact-pin ledger run: PASS arithmetic, 65,491 legal envelope lengths checked;
  candidate verdict remains NOT PROVEN. Script SHA-256:
  `4f8dd18fce0664fd9df47461aa3b877ebfb444f5db8af3c19e9a3a9a25769022`.
- `tools/Test-Api.ps1`: PASS.
- `tools/Test-Architecture.ps1`: PASS, including structural GiveItem supplement.
- `git diff --check`: PASS; existing checkout CRLF conversion warnings are not
  runtime evidence.
- All previously dirty production/test/research files outside the 2A document
  retained their captured hashes. The 2A document received only a new routing
  paragraph; its historical evidence was preserved. New work is this report and
  the standalone arithmetic ledger, not a production implementation.
- No new worker processes, test servers, DBs or remote artifacts were created.
  Previously retained worker fixtures were not cleaned up; their original
  preservation/cleanup status remains as recorded in 2A.
