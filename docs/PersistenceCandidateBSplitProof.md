# Persistence-2A Candidate B split-storage proof gate

**Historical alternative:** The 2026-09-30 approved D21 capacity correction
restores the one-database/Foundation 1 primary candidate as the active 2A route.
The failed split proof and measured cross-database behavior below remain
preserved; Candidate B/C are not authorized merely because a below-quota
logical state can encounter controlled physical StorageFull.

Date: 2026-09-30. **CANDIDATE B FAILED THE REQUIRED PROOF GATE — PROCEED TO AUTHORITATIVE-PRIMARY / DISPOSABLE-DERIVED ARCHITECTURE INVESTIGATION.**

This is a qualification rejection, **not a mathematical counterexample to every possible two-database design**. The tested PERSIST/EXTRA cross-database commit boundary works. The required universal derived-EOF, transaction-specific dirty-page, retained-journal, allocation, memory, instruction and near-ceiling-upgrade bounds do not close. Selecting a plausible derived page cap or shrinking the 64-MiB filesystem allowances from samples would violate the task's decision rule. No D21/D22 amendment or production split is authorized by this record.

## Authority and preservation

Read [AICONTEXT](../AICONTEXT.md), [D21 and D22](Invariants.md), [Foundation 2](PersistenceFoundation2.md), [2A evidence](PersistenceFoundation2A.md), and the [single-DB proof gate](PersistenceSingleDbBoundInvestigation.md). The supplied split-storage architecture investigation is prior design input, not proof. Foundation 1 and D21/D22 retain authority; historical negative evidence remains unchanged.

`main` and freshly fetched `origin/main` both pointed at `928b9d7b60fcdba508b322fbc824c83ccab5cc9f`. The many existing uncommitted 2A production/test/research files were preserved. This task added only the standalone, non-production [split proof target](../tests/persistence/splitproof/CMakeLists.txt) and this investigation. The probe's final source SHA-256 is `1175ab9bd6a5d86e0bbbaabcf7fc0050b8930600d7626d3723f5d171b5d7e`.

Pinned SQLite: 3.53.4, source ID `2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`, amalgamation SHA-256 `b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189`, header SHA-256 `919e7f2e8ed1d8f56ac17b412b8971c76aa5d1a879752cc6058f75e7d5910e1d`. The CMake target fails on a different pin. This probe is not a production package/test-runner target.

## Exact analyzed Candidate B

The primary participant can retain the qualified Foundation 1 representation and filename `store.sqlite3`, with **no primary-record migration and no required primary table change**. The candidate's one new fixed file would be `derived.sqlite3` in the same guarded persistence directory. The standalone fixture deliberately uses `primary.sqlite`/`derived.sqlite` to prevent confusion with a production store; those names are not proposed runtime names.

```sql
CREATE TABLE Records(Namespace BLOB NOT NULL,Store BLOB NOT NULL,Key BLOB NOT NULL,Envelope BLOB NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Namespace,Store,Key)) WITHOUT ROWID;
CREATE TABLE Quotas(Namespace BLOB PRIMARY KEY,Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Stores INTEGER NOT NULL) WITHOUT ROWID;
CREATE TABLE Totals(Id INTEGER PRIMARY KEY CHECK(Id=1),Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Namespaces INTEGER NOT NULL);
```

The candidate derived participant relocates the **unchanged private 2A relations** to a fixed attached database. Exact statements, with `derived.` schema qualifiers and no author-controlled SQL identifiers, are in [SplitAtomicityProbe.cpp](../tests/persistence/splitproof/SplitAtomicityProbe.cpp); the original unqualified strings are in `Derived::Schema()` in [Derived.cpp](../native/src/persistence/Derived.cpp). They are `DerivedTotals`, `DerivedNamespaces`, `DerivedFields` plus `DerivedFieldNames`, `DerivedGenerations` plus `DerivedGenerationFields` and partial `DerivedGenerationWork`, `DerivedPrefixes` plus unique `DerivedPrefixOrder`, `DerivedEntries` plus unique `DerivedEntryOrder`, and `DerivedMembers` plus partial unique `DerivedMemberEntries`. This is the exact frozen research candidate, not an implemented production schema. Scalar type is in member/type-tagged sort-key data; ACTIVE/BUILDING/CLEANUP and checkpoint live in `DerivedGenerations`; quota and work cursors live in `DerivedTotals`/`DerivedNamespaces`.

The current production backend **cannot simply ATTACH and run unchanged**: `SQLITE_LIMIT_ATTACHED` is zero, preflight permits only the primary database/journal and two locks, the journal-tail guard rejects super-journal pointers, `Derived::Create()` creates unqualified tables in `main`, and both schema validators read `main.sqlite_schema`. Those are necessary future implementation/qualification changes, not findings that D21 may be weakened. A fixed internal ATTACH, one connection/worker, no author-supplied path, and limit one attached database remain the analyzed model.

## Logical population and page geometry

D22's 64 MiB global/16 MiB per-namespace pool charges **all** ACTIVE, BUILDING, withdrawn/CLEANUP, metadata and ledgers together. It is not 64 MiB per generation. A field has at most two retained generations total, one ACTIVE and one BUILDING at most; cleanup occupies a slot. At most eight fields belong to a store, 10,000 primary records to a namespace, and 100,000 primary records globally. The current charge formulas are global 65; namespace `21+|Ns|`; field `29+|Ns|+|Store|+|Field|`; generation `70+|Checkpoint|`; prefix `37+|Prefix|`; entry `33+|Suffix|+|RecordKey|`; member `30+|RecordKey|` bytes.

Conservative **independent** upper row counts from `(64 MiB - 65) / minimum row charge` are namespace 3,050,399; field 2,097,149; generation 958,697; prefix 1,766,021; entry 1,973,788; member 2,164,799. These are not simultaneous populations: each requires charged parents and shares one quota. There is no valid shortcut that bounds historical derived-only fields by current nonempty primary stores; retained field metadata remains charged after primary deletion.

At 4,096 bytes/page, zero reserved bytes, pinned SQLite computes index `maxLocal=1,002`, `minLocal=489`, table-leaf `maxLocal=4,061`; overflow payload is 4,092. The [prior exact-integer ledger](../tests/persistence/storagefit/SingleDbBounds.py) bounds every current derived row/index payload at or below 665 bytes (the largest are `DerivedEntries` 665, its order index 664, `DerivedPrefixes` 543 and its order index 533). Thus **these derived row shapes do not need overflow pages**. This is only a cell-size theorem. It does not prove a minimum B-tree fill, number of interior pages, freelist/high-water over legal insertion/deletion/rebuild histories, or a maximum derived EOF. The prior ledger's arithmetic self-check passed again; its `ProvenAllHistoryDbPages` and `ProvenTransactionDirtyPages` remain null.

**Proven maximum derived pages/bytes: not established. Selected derived hard EOF/headroom: none.** A fresh/sampled file length is a lower observation, not a universal page theorem; the requested 80/96/128-MiB-style empirical selection is not made.

## Transaction and journal gate

Primary Get is read-only (zero rollback-journal page images after startup recovery). Primary new Set, overwrite (including 64-KiB envelope/overflow changes), Remove, `Quotas`/`Totals` accounting and any future split metadata all require a distinct-original-page theorem through B-tree split/rebalance/freelist behavior. `CheckRecords()` at startup is read-only. In this split, `Derived::Validate(true)` quarantine targets derived rows, not primary data, but it can withdraw **many** trusted-but-mismatched generations inside one startup transaction; it is not constrained by the 32-record maintenance batch. Derived transaction families also include field/generation creation, ACTIVE and BUILDING dual maintenance for up to eight fields, savepoint rescue, build/checkpoint, publication, withdrawal, cleanup and initialization. No certified family-specific maximum was derived. The only unconditional primary original-page ceiling is its full 131,072-page hard cap; the derived fallback is its as-yet-unselected full page cap. The worst cross-database family is therefore **not identified**.

Pinned source `sqlite3.c:61332-61412` establishes that a multi-file PERSIST journal gets a super-journal pointer of `N+20` bytes, where `N` is its super-journal pathname length, and EXTRA/full-sync first aligns the journal offset to the next sector boundary. With one initial header of actual sector size `H`, `D` distinct original pages and no spill-generated extra header, a conditional transaction extent is:

```text
Single-participant J(D,H) = H + 4104*D
Super-participant J(D,H,N) = align_up(H + 4104*D, H) + N + 20
Retained PERSIST high-water = max(accepted inherited EOF, maximum legal transaction J)
H <= 65536; D <= original page count at BEGIN.
```

This formula needs a **proved** D for each transaction family, validation of any extra header/page-move path, and an inherited-journal policy before it can replace D21's whole-primary allowance. `writeSuperJournal()` may truncate a pre-existing longer PERSIST journal to the pointer end, but that behavior is not an approved startup normalization scheme and does not remove the need to accept/check an inherited file before the transaction. The current single-DB 513.0625-MiB journal EOF allowance does not itself include a new super-pointer/alignment worst case. Neither primary nor derived retained-journal high-water is newly certified.

Pinned source `sqlite3.c:90909-91038` writes the two participant journal filenames plus NUL terminators into the super-journal, syncs it, phases one both participants, then deletes it with directory sync as the cross-DB commit point. For fixed pathname lengths `Lp,Ld`, its EOF is exactly `Lp+1+Ld+1`; with both journal paths bounded by VFS `mxPathname=M`, `EOF<=2*(M+1)` (pinned default Unix M=512, Win32 M=1040). The generated super filename also needs pathname headroom. This is an EOF bound, **not** a filesystem allocation or orphan-count theorem. Crash-leftover/orphan super-journal scanning and ownership rules remain unproved.

## Physical budget gate

The existing D21 deliberately conservative accounting is:

```text
primary DB EOF                 512.0000 MiB
primary retained journal EOF  513.0625 MiB
existing DB allocation margin  64.0000 MiB
existing journal margin        64.0000 MiB
diagnostics + two locks         8.1250 MiB
                              -----------
baseline                     1161.1875 MiB
D21 operational budget       1280.0000 MiB
unassigned                    118.8125 MiB
```

The 64-MiB-per-file terms are **operational allowances**, not proved cluster-rounding constants. Foundation 1 physical-allocation evidence intentionally did not claim an instant-by-instant hard block bound. They cannot be silently reduced or mechanically cloned for every new file. The split equation is:

```text
512 + max(513.0625, proved primary super-journal allowance)
    + 64 + 64 + 8 + 0.125
    + derived DB EOF + derived DB allocation variance
    + retained derived journal EOF + its allocation variance
    + super-journal allocation + auxiliary/rebuild allocation
    + explicit safety reserve <= 1280 MiB.
```

The right-hand space **before** super-pointer growth is only 118.8125 MiB. No derived EOF, derived dirty-page, smaller primary high-water, new allocation variances, or reserve theorem was established. Therefore the full inequality is **unsolved**, not violated by a measured sample. The 1,280-MiB figure remains an operational qualification target, not a new hard physical-block invariant. No replacement hard DB ceiling is selected.

## Measured crash boundary (research only)

The standalone probe uses one connection, one fixed parameter-bound ATTACH, the exact primary table shapes, the exact 13 derived CREATE statements, PERSIST/EXTRA on both databases, 4-KiB pages, no WAL, no mmap, temp MEMORY, cache spill OFF, `SQLITE_LIMIT_ATTACHED=1`, and a 131,072-page **primary** cap. No derived cap was guessed. A wrapped VFS makes the process exit just before or just after the super-journal delete; a fresh process then reopens both files.

| Platform | Before delete | After delete | Mid-commit file observation |
|---|---|---|---|
| Windows x64, dockerbox NTFS | old `(0,0)` recovered | new `(1,1)` recovered | primary/derived journal EOF 9,330 bytes each; super EOF 182, allocated 184 bytes in one fixture |
| Linux x64, BigKVM ext4 container | old `(0,0)` recovered | new `(1,1)` recovered | primary/derived journal EOF 9,282 bytes each; super EOF 86, allocated 4,096 bytes in one fixture |

These are **small fixed-shape crash probes**; they do not exercise the real worker, every specified crash point, near-ceiling files, fault/FULL classification, namespace/quota transactions or full production startup checks. A Windows derived-only `BEGIN IMMEDIATE` probe still created a super-journal even though primary data was not updated; the primary journal retained its old 8,720-byte EOF. Thus a transaction named “derived-only” does not justify assuming no super-journal under the current BEGIN pattern. Linux GCC ASan+UBSan compiled and ran normal commit without a sanitizer report; this is not broad sanitizer/fault qualification. Docker Desktop on dockerbox was unresponsive; Linux ran in a bounded `--network none`, `--cpus 2`, `--memory 2g` container on the already-authorized BigKVM host. No persistent container was left.

The measured super sizes equal the two journal path lengths plus two terminators, and the measured PERSIST journal extents include the super-pointer suffix. The NTFS/ext4 allocated-byte differences illustrate why one sample cannot replace filesystem variance proof.

To reproduce with the pinned amalgamation, configure [the standalone CMake target](../tests/persistence/splitproof/CMakeLists.txt) with `SQLITE_AMALGAMATION_DIR` pointing at exact `sqlite3.c`/`sqlite3.h`, build `SplitAtomicityProbe`, then use separate fresh fixture directories for `new -> before -> inspect -> check0` and `new -> after -> inspect -> check1`. The intentional `before`/`after` process exit is nonzero; the subsequent read-only inspection and recovery check are the assertions. `new -> normal` checks an ordinary cross-DB commit. Do not run against a production persistence directory.

## Remaining mandatory gates and disposition

| Gate | Result |
|---|---|
| Derived B-tree all-history EOF and page cap | **OPEN**; cell-size theorem only |
| Primary per-family distinct dirty pages | **OPEN**; whole 131,072-page fallback only |
| Derived per-family distinct dirty pages | **OPEN**; bulk startup quarantine unbatched |
| Both PERSIST high-water bounds | **OPEN**; conditional formula only; inherited primary journal unresolved |
| Super EOF | Source-bounded by two VFS paths; exact allocation/orphan policy **OPEN** |
| Filesystem allowances and 1,280-MiB reserve | **OPEN**; equation unsolved |
| Near-ceiling genuine Foundation 1 upgrade | **NOT MEASURED/NOT PROVEN**; no primary rewrite is needed conceptually |
| Full crash/FULL/error classification matrix | **OPEN**; two commit-point cuts only |
| Savepoint/subjournal plus two-pager worker memory | **OPEN**; no 256-MiB proof |
| Exact VDBE/instruction family coverage | **OPEN**; prior single-DB certificate does not cover split |
| Derived corruption isolation and bounded rebuild/cleanup | **OPEN**; do not treat derived as disposable before SQLite recovery |
| Worker FIFO/fairness/supervision | Design unchanged; **not integration-tested** |
| Windows/Linux | Research commit-point probe only; no full platform qualification |

The missing capacity and dirty-page theorems are **material**, not formatting gaps: without them there is no defensible derived `max_page_count`, retained-journal allowance or non-negligible budget reserve. Candidate B therefore fails the prescribed adoption test. This does not claim SQLite multi-DB atomicity is broken or that a later independently scoped proof could never work. Per the supplied decision rule, the next architecture task may investigate Candidate C: authoritative Foundation 1 primary plus separately recoverable/disposable derived state, while keeping D21/D22 public semantics unchanged. Do not begin 2B or production split from this record.

No exact D21/D22 amendment can be proposed yet. Package/API/native ABI/provider/package-schema/Luau identities are unchanged. No production split, public Query binding, `Indexes` hints, cursor, schema/version API or Persistence-2B code was added. No commit or push was made because mandatory Candidate B qualification gates failed; the branch remains `main` with the pre-existing dirty 2A work and these new research files.

## Requested completion ledger

| # | Result |
|---:|---|
| 1 | Candidate B failed the required proof gate; proceed to Candidate C investigation, not implementation. |
| 2 | Starting commit `928b9d7b60fcdba508b322fbc824c83ccab5cc9f`. |
| 3 | Existing dirty 2A work preserved; this task added only standalone research/proof files. |
| 4 | Qualified Foundation 1 `Records`, `Quotas`, `Totals` primary schema above. |
| 5 | Exact 13 qualified-name derived CREATE statements in the linked research probe; current 2A column/index shapes unchanged. |
| 6 | No primary table/record change is conceptually required; current backend routing would need implementation changes. |
| 7 | Independent safe derived row ceilings above; coupled maximum simultaneous population not proved. |
| 8 | Derived cells avoid overflow; all-history B-tree page theorem missing. |
| 9 | Maximum derived EOF not proved. |
| 10 | No derived hard ceiling or headroom selected. |
| 11 | Primary Get, Set-new, Set-overwrite, Remove, accounting, startup/possible split metadata enumerated above. |
| 12 | Only full 131,072-page primary fallback; no family-specific dirty-page maxima. |
| 13 | Startup quarantine is derived-targeted but may be unbatched across many generations. |
| 14 | Conditional primary PERSIST formula above; inherited/super-pointer high-water not resolved. |
| 15 | Derived prepare, ACTIVE/BUILDING maintenance, batch/checkpoint, publish, withdraw, cleanup, init and quarantine enumerated above. |
| 16 | Derived dirty-page maxima not proved. |
| 17 | Same conditional formula; derived retained-journal high-water not proved. |
| 18 | Worst cross-DB transaction cannot be ranked without dirty-page proof. |
| 19 | Super EOF is two bounded journal pathnames plus NULs; allocation/orphan bound open. |
| 20 | Existing +64 MiB DB and +64 MiB journal allowances retained; new-file variances unproved. |
| 21 | Complete 1,280-MiB equation above is unsolved. |
| 22 | No certified safety reserve. |
| 23 | Genuine near-ceiling F1 upgrade not measured/proved. |
| 24 | Exact-pin small-fixture before/after super-delete recovery passed Windows/Linux; full crash-point matrix open. |
| 25 | SQLITE_FULL and I/O-fault matrix not run. |
| 26 | Both attached pagers explicitly verified PERSIST and EXTRA with the pinned source. |
| 27 | Two-pager/savepoint/subjournal worker memory bound open. |
| 28 | Exact split VDBE/instruction certificate open. |
| 29 | Derived corruption isolation after cross-DB recovery not proved. |
| 30 | Derived rebuild/cleanup physical, journal, fairness and memory bounds open. |
| 31 | One worker/connection/FIFO/fairness is the analyzed design, not an integrated test result. |
| 32 | Windows x64 standalone exact-pin commit-point probe passed; production integration unqualified. |
| 33 | Linux x64 standalone exact-pin commit-point probe passed on BigKVM; production integration unqualified. |
| 34 | Linux GCC ASan+UBSan normal probe passed; broad sanitizer/fault matrix open. |
| 35 | Candidate B rejected for adoption on material missing resource/work proof, not on a demonstrated atomicity defect. |
| 36 | No defensible exact D21 amendment yet. |
| 37 | No D22 amendment; public contract unchanged. |
| 38 | Package, scripting API, native ABI, provider protocol, schema and Luau identities unchanged. |
| 39 | A later approved split would need fixed ATTACH/routing, dual schema/preflight/recovery ownership, bounded page/journal/work/memory guards and platform qualification. |
| 40 | Remaining 2A gates are the open table above; general 2A not resumed. |
| 41 | Public Luau API unchanged; Query/Indexes hints not exposed. |
| 42 | No production split or Persistence-2B work. |
| 43 | No commit/push; mandatory adoption gates did not pass. |
| 44 | `main` synchronized to `origin/main` at start; tracked/untracked 2A worktree remains dirty by design. |
