# Persistence Foundation 2A — private derived-index substrate

## Local final-source qualification — 2026-10-01 (hosted CI pending)

The private one-database implementation separates **D21 authoritative-primary
Ready** from **derived admission**. `Backend::Open` completes SQLite recovery,
schema/integrity, every primary envelope and quota proof, and file checks before
publishing the worker. It does not validate or quarantine the entire retained
derived graph before Ready. A new worker has no process-local admission for
retained ACTIVE or BUILDING generations. Subsequent bounded, foreground-
subordinate `VerifyStep` work checks derived authority, accounting, orphan rows
and representation against the current primary state; mismatched representations
are withdrawn in a bounded transaction before proof completion. Durable ACTIVE
metadata alone never makes an index queryable. This is a **global process-local
proof** in the present private implementation, not a claim that per-index lazy
admission or public Query exists.

While the proof is incomplete, a primary Set/Remove may commit without derived
maintenance only after invalidating the in-progress proof; retained generations
remain unadmitted until a fresh proof or withdrawal. Once the proof is complete,
foreground mutation and admitted derived maintenance share one transaction.
A recoverably failed derived-only proof/maintenance turn pauses derived work for
that worker lifetime without restarting an otherwise healthy primary worker;
physical SQLite faults or failed primary proof retain D21-wide classification.
Structurally inconsistent or over-bound inherited derived history is
quarantined by the process-local nonadmission fence: no retained generation
can serve Query, the primary remains available after its independent proof,
and the worker does not repeatedly attempt unsafe cleanup. Coherent
representation mismatches are withdrawn in bounded transactions and ordinary
withdrawn generations clean in bounded batches. Derived structural invalidity
remains preserved for diagnosis rather than guessed-away by a primary rewrite.
BUILDING is never queryable and its resumed maintenance/publication remains
gated by completed current-process verification. No author Query, hints,
cursors or Persistence-2B binding is implemented.

This change removes the historical global-derived-before-Ready failure mode
documented below; it does **not** erase those failed fixtures or by itself
establish final 2A PASS. The strict
100,000-record / 245,200,000-primary-byte / 746,496-retained-generation
fixture reached primary Ready in **7,884 ms on final-source Windows** and
**10,701 ms on Linux** (the latter predates only dead-code cleanup), against
the unchanged 30,000-ms supervisor deadline. A final-source Linux reopen of a
100,000-record/746,752-generation retained fixture reached Ready in **4,548
ms**. Retained indexes were unadmitted at Ready. These are observed timings on the qualified worker
hosts, not a platform-independent worst-case deadline theorem. Current local
native matrices passed **21/21 Windows** and **22/22 Linux** after the
obsolete global-quarantine removal and 87-family fixed-SQL instruction
certificate update; the final-source Linux ASan/UBSan/leak matrix passed
**22/22** (including allocation faults). Managed supervised-worker tests passed
on both platforms, including 32 lifecycle cycles, lost-ack/no-replay, deadline
kill/reap and an eight-field/512-record resumed build with 64 Gets, two Sets
and one Remove. The earlier isolated Linux Mono `ObjectDisposedException` was
not reproduced in three final-source Linux managed-worker cycles (including
the sampled run); its isolated observation remains in the history below.

The current code enforces a **1-GiB database EOF** (262,144 × 4-KiB pages), a
**1,075,904,512-byte retained-journal EOF** and a **2,560-MiB
filesystem-allocation operational budget**, adopted privately in D21 for 2A.
The first two are CarbonLuau-controlled file-extent limits; allocation is measured pre/postflight
under D21's filesystem-qualified operational policy, not a never-exceeded
allocated-block guarantee. Neither unused primary nor unused derived logical
quota reserves physical admission. Local FULL, journal write/sync and lost-ack
fault fixtures passed on final source. These are bounded injected VFS/page
failures, not a claim that a physical device was filled. A near-envelope
eight-field Set/overwrite fixture observed SQLite allocation high-water of
**464,352 bytes Linux / 464,792 bytes Windows**. Linux process observations
were **8,088 KiB VmHWM** for that fixture, **10,376 KiB VmHWM** for the full
derived suite and **9,564 KiB VmHWM / 12,156 KiB VmPeak** for the final-source
supervised worker (197 samples). The Windows
full derived suite peaked at **10,166,272 bytes** working set and **6,279,168
bytes** private memory (2,148 process samples); the near-envelope fixture
peaked at **7,712,768 bytes** working set. Sampling the Windows managed
supervisor's storage children over 32 lifecycle cycles (264 child samples)
observed **9,961,472 bytes** peak worker working set and **6,119,424 bytes**
peak worker private memory. These are observations, not
universal per-operation maxima. A conservative simultaneous working budget is
128 MiB SQLite allocator (hard limit, including savepoint/subjournal) + 4 MiB
for two maximum 4,096-entry decoded graphs + 1 MiB transport/envelope buffers
+ 1 MiB verification/build containers + 64 MiB runtime/stack/allocator
allowance = **198 MiB**, leaving **58 MiB** beneath the OS-enforced 256-MiB
worker limit. The 64-MiB runtime allowance is deliberately much larger than
the measured process baseline but is an engineering allowance, not a separate
C++ allocator cap. A resource fault can reject/retire the worker; it cannot
authorize a false durable success or an unverified ACTIVE index. The one-at-a-
time application savepoint remains necessary for atomic derived withdrawal;
its pager/subjournal allocations share the same SQLite hard cap, and injected
rollback/release failures are covered by the crash fixture.

**Pending before PASS:** commit qualified source/evidence, obtain hosted
final-head Windows/Linux/sanitizer and clean-package CI, verify documentation
deployment and the published remote state. No hosted-CI result, package
release or 2B authorization is claimed by this local continuation. The dated
PARTIAL/BLOCKED results below are
historical evidence from earlier candidate designs, not a second current
readiness contract.

**2026-10-01 startup-readiness continuation — still PARTIAL.** A new
test-only exact-format fixture isolates the authoritative primary proof. With
100,000 records, each containing 1,024 booleans and a 320-byte string, its
245,200,000-byte primary logical charge is below the 256-MiB global limit and
each of its 16 namespaces is below 16 MiB. It has no derived generations.
Before the current parser optimization, the unchanged 30-second backend reopen
took 11,071 ms on Windows and 26,952 ms on Linux. This shows that moving only
derived proof after Ready would not by itself give comfortable Linux startup
margin for valid primary shapes.

The uncommitted backend now verifies startup envelopes through the same bounded
decoder grammar and identity-bound checksum as normal `Decode`, but without
constructing transient value trees. Normal Get still materializes fresh values.
The identical dense-primary fixture reopened in 6,473 ms on Windows and
10,262 ms on Linux after this change. Format tests compare both parser modes
over valid nested values and 10,000 checksum-valid malformed payloads. The
derived graph pass also skips per-generation member/prefix scans when a proved
generation count is zero, relying on the final exact global orphan counts;
the representation pass skips member cursors for stores proved to have no
primary rows. A forged zero-member counter was added to the corruption suite.

A second direct-schema fixture combines those 100,000 dense primary records
with 46,656 distinct empty derived stores, 373,248 fields and 746,496 retained
generations. Its derived charge is 65,318,557 bytes, below 64 MiB; the
primary and derived namespaces are separate. Before the zero-member graph
shortcut it reopened in 18,848 ms on Windows and 26,736 ms on Linux. Subsequent
reopens on the retained fixture were 16,220/22,955 ms after the graph shortcut
and 23,019/22,895 ms after the empty-store shortcut (Windows/Linux). These
observations vary with host load and are not a worst-case startup proof.
The representation pass then began reading already-joined generation and field
columns directly, eliminating two redundant point queries per retained
generation. The indexed query-plan and 77-family instruction certificate passed
on Windows and Linux. Test-only phase timing before that join correction
observed Windows **8,699/5,933/9,285 ms** and Linux **1,062/10,260/11,575 ms**
for SQLite/schema/integrity, primary proof and derived proof respectively.
After the join correction, a run overlapping independent plan/budget tests
observed Windows **10,932/6,003/5,190 ms** and Linux
**1,061/10,622/5,434 ms**. These are phase observations, not timing guarantees
or an isolated performance comparison.
Further direct-schema startup fixtures tested the other retained-state axis.
Four namespaces with 640,000 charged, linked cleanup members used 46,728,189
derived logical bytes and reopened in **3,433 ms Windows / 6,193 ms Linux**.
Adding the same 245,200,000-byte primary corpus yielded **8,964 / 16,275 ms**.
An alternative with 1,920,000 charged oversized-string members (no sort-entry
row), three stores and 48 cleanup generations per namespace used 65,296,765
derived logical bytes, below the 64-MiB global and 16-MiB per-namespace limits;
combined with the primary corpus it reopened in **6,823 / 13,031 ms**.
All figures are Windows/Linux respectively. These fixtures use exact private
rows and quota ledgers but not observed public preparation histories. They
establish useful measured margin for high metadata and member cardinalities;
they do not prove that every legal corruption/quarantine history fits 30 seconds
or that derived-only logical failure can never block healthy primary readiness.
The fixture is direct private-schema seeding, not proof of a public preparation
history. No candidate private numeric ceiling, D21/D22 readiness amendment,
2A PASS, implementation commit or push follows from it. Full serial native
regressions after the joined correction passed on Windows (20/20) and Linux
(21/21). The focused Windows Format and Derived executables passed again after
the last test-only fixture additions. The affected Linux ASan/UBSan build and
Format, Derived, DerivedCrash, DerivedPlan and DerivedBudget tests also passed
(5/5). These are affected native checks, not final 2A qualification or hosted
CI. Independent source reviews found
no concrete parser-parity or quarantine defect, and requested targeted
malformed-value and orphan-state tests were added. The 30-second global Ready
gate remains authoritative and the
worker-memory/capacity/fault/hosted-CI gates below remain open.

The global pre-Ready model has a separate worst-case problem even when a
well-formed fixture reopens quickly: `Open` performs every derived
representation withdrawal before publishing Ready, at most 32 withdrawals per
transaction. A legal primary/derived cardinality can require thousands of
such batches after derived-only mismatch while the primary values and D21
quota ledgers remain healthy. The measured valid-state startup margin cannot
bound that recovery work under the 30-second supervisor deadline. The next
implementation step must separate authoritative primary readiness from
derived/query admission, with unproved retained generations unavailable for
Query and primary mutation unable to leave an effectively ACTIVE index stale.
This is a required design/implementation gate, not a reason to weaken D21
corruption handling or claim that the existing global gate is qualified.

**2026-09-30 routing update:** The user approved D21's separation of hard
logical quotas, hard database/journal file-byte extents and the qualified
filesystem-allocation operational budget. The earlier universal-fit gate below
is superseded as an active acceptance requirement; its failed fixtures and
investigations remain evidence. The one-database Foundation 1 primary layout is
again the preferred 2A candidate. This note does not canonically adopt a new
physical ceiling, qualify the uncommitted implementation or authorize public
Query/2B.

**2026-10-01 earlier final-closure verdict: NEEDS MORE ARCHITECTURE WORK.** A
quota-valid, integrity-valid combined primary/derived state exceeded the
existing 30-second Linux startup deadline; the same state reopened under an
extended test deadline. The exact evidence and limits are below. Production
and public contracts remain unchanged, the larger physical limits remain
candidates, and no 2A PASS/commit/push is claimed. Startup validation work
must be bounded or its readiness boundary canonically resolved before the
remaining closure matrix can finish; retained-derived-state treatment is a
separate contributing concern.

**2026-10-01 continuation — narrower blocker, verdict unchanged.** A private
validator statement-reuse correction reduced the original combined fixture's
Linux reopen from 36,918 ms to 23,969 ms under the actual 30-second deadline.
Ordinary `PersistenceDerived` passed on final local test source on Linux
(17.54 s) and Windows (9.84 s) after the correction. This does not close
startup readiness: an exact-format
offline fixture with 100,000 Foundation 1 primary records, 212,200,000 bytes
of primary logical charge, and 136 ACTIVE-only derived generations still
returned `DeadlineExceeded` at 30,001 ms on Linux. The unchanged database
reopened in 34,958 ms under a test-only 90-second deadline and read its
authoritative primary value. Its derived logical charge was only 14,933 bytes;
thus an added retained-generation cap alone cannot close the gate. A combined
heavy-primary/high-metadata fixture took 84,629 ms under the extended deadline.
The heavy values have an unindexed `Padding` member; the ACTIVE indexes are
complete empty-result indexes for fields `A`–`H`. These are direct private-schema
fixtures, not evidence of equivalent public preparation histories. The current
representation-validation pass decodes primary values separately for each
generation; next investigate a bounded per-store primary stream that decodes
each value once and compares it against at most eight fields/two generations
per field, preserving full graph proof, <=32-withdrawal transactions, and
crash/restart cursor safety. No D21/D22 policy or deadline amendment is adopted
from these timing samples.

**2026-10-01 further startup continuation — measured fixture recovered,
foundation still PARTIAL.** The uncommitted private representation pass now
seeks stores through `DerivedFieldNames` and decodes each primary record once
per store, comparing the snapshot with at most eight fields/two generations per
field. The complete derived authority/accounting graph and Foundation 1
primary proof still precede any quarantine. Store-local cursor progress
continues at a generation ID only when a 32-withdrawal transaction ended
mid-store; a process restart discards that cursor and revalidates the whole
database. The fixed SQL-family inventory/certificate was reviewed and updated
from 76 to 77; an `EXPLAIN QUERY PLAN` regression requires an indexed store
seek without a scan or temporary sort. A first grouped/OR cursor query was
rejected after it caused the retained high-cardinality fixture to miss even a
90-second test deadline; the final row-value index seek replaced it. This is
useful negative performance evidence, not a D21 deadline change.

With the indexed candidate, the retained **100,000 heavy-primary / 136
ACTIVE** fixture reopened on Linux in **7,784 ms**; the **100,000 empty-map /
746,656 generation** combined fixture in **19,861 ms**; and the **100,000
heavy-primary / 746,752 generation** combined fixture twice in **26,505 ms**
and **27,783 ms**, each under the unchanged 30-second startup deadline. The
last result leaves only 2,217 ms in the slower run. These are exact-schema
offline fixtures, not a proof of the worst public-reachable state or of
30-second readiness under varying host load. A functional regression now
scrambles generation IDs across lexical stores and puts the 32nd withdrawal
mid-store; a crash/restart regression verifies the committed first batch and
the remaining same-store generation. The affected four-test derived/crash/
plan/budget matrix passed on Windows MSVC and the focused functional,
crash, plan and budget tests passed on Linux GCC. Linux ASan/UBSan/leak and
broader final-source qualification are recorded separately when completed.
The affected Linux ASan/UBSan/leak four-test matrix passed after all four
executables were rebuilt from this source. An intermediate sanitizer run
contained a stale budget-test executable and failed its SQL-inventory
certificate; rebuilding it from the current 77-family source resolved that
provenance error. No sanitizer finding was observed in the final affected
run. The ordinary Windows and Linux runs and sanitizer checks do not replace
the still-required broader final-source and hosted matrices.

The broader **native** regression matrix on this uncommitted source passed
**21/21 Linux GCC 13.3** and **20/20 Windows MSVC 19.40**; architecture and
public-API structural checks also passed. Dockerbox's C: volume exhausted
free space during an attempted full Windows rebuild. No retained fixture or
unrelated file was removed to make room; Windows broad validation instead
used a bounded two-job local MSVC build with the exact D21-pinned SQLite
source/header hashes. That local build also created a fresh heavy-primary,
high-metadata offline fixture (100,000 primary records, 212,200,000 primary
logical bytes, 746,752 generations); its normal 30-second backend reopen
reported Ready in **13,724 ms**. These are native/model and exact-format
fixture results, not final hosted CI, real Carbon qualification, or a proof
that every legal retained state fits the startup deadline. Task-owned local
and remote fixtures remain outside tracked source; dockerbox disk pressure is
an infrastructure issue for later worker use.
No 2A PASS, numeric-limit adoption, commit or push follows from these samples;
the remaining startup upper-bound, physical-limit, combined process-memory,
fault, platform and hosted-CI gates remain open.

### Final-qualification continuation (2026-09-30; still PARTIAL)

The uncommitted candidate now carries a private startup generation cursor. The
backend validates the complete primary and derived authority graph before any
quarantine write. Later batches inspect only generations after the last checked
ID, withdrawing at most 32 per transaction. The exclusive worker makes no
intervening external writes; after a process crash, startup discards the
in-memory cursor and repeats complete validation before resuming. This removes
the accidental repeated full-graph scan per batch. It does **not** prove a
30-second worst-case startup.

**Closure correction (2026-10-01):** The previously recorded
262,144-generation estimate was not an upper bound. Foundation 1 limits
*nonempty primary* stores to
64 per namespace and acquired names to 64 per **domain lifetime**; D22 retains
derived fields across domain replacement and does not evict them when hints are
removed. `Derived::Prepare` does not require a primary row or primary store.
Thus 64 primary stores cannot bound retained derived store names across a
history of replacements. As a concrete logically admissible construction, four addon
namespaces with one-character package IDs can each retain 11,664 distinct
three-character alphanumeric store names. Eight one-character fields per store,
one ACTIVE and one BUILDING generation per field, give 373,248 fields and
746,496 retained generations. The exact minimum-format logical charge for
this construction is 65 + 4 * (23 + 93,312 * (35 + 2 * 70)) = 65,318,557 bytes:
each namespace is below 16 MiB and the aggregate below 64 MiB. Each store can
be acquired within the 64-name-per-domain limit over repeated domain lifetimes;
empty stores consume no Foundation 1 primary store slot. This is a source-based
quota/format argument, **not** a measured physical-admission or startup fixture,
nor proof that all such generations can mismatch primary data simultaneously.

The global 64-MiB derived charge and minimum 70-byte generation charge give a
conservative upper bound of floor((67,108,864 - 65) / 70) = 958,697 retained
generations, before charging their fields/namespaces. At 32 withdrawals per
transaction, the cardinality-only upper bound is 29,960 transactions; actual
maximum mismatches and an exact reachable generation maximum remain unproved.
The 30-second startup deadline is therefore **not qualified**. An exceeded
deadline fails closed rather than publishing Ready; that is not successful
startup qualification. A durable retained-derived-store/generation policy or
another bounded safe startup design must be resolved before adopting numerical
2A limits or claiming PASS.

**Final-closure stop-condition probe (2026-10-01):** The test-only
`PersistenceDerivedTests --startup-cardinality` mode seeds the exact private
schema with four one-character addon namespaces, 46,656 distinct three-character
empty store names, eight fields/store, and one ACTIVE plus one BUILDING
generation/field. It then uses a normal production `Backend` reopen, including
integrity, primary and complete derived-graph validation. This is an offline
structural fixture, not a claim that 746,496 public preparation requests were
observed. It is quota-valid and the qualified backend accepted its physical
layout. The empty-primary case reopened in **15,525 ms on Windows MSVC/NTFS**
and **24,327 ms on Linux GCC/ext4**, against the 30,000-ms supervisor deadline.

The combined case adds 100,000 exact Foundation 1 empty-map envelopes in ten
additional addon namespaces (10,000 keys and one nonempty store each), with
matching `Records`, `Quotas` and `Totals`. Eight fields and two complete/partial
generations for each populated store increase the totals to **373,328 fields**
and **746,656 generations**. Primary charge is **5,800,000 bytes**; derived
charge is **65,332,867 bytes** (under the 64-MiB global ceiling, with every
namespace under 16 MiB). An ACTIVE generation with no entries is complete for
these empty maps; its companion BUILDING generation has an empty checkpoint.
No primary value or derived entry is fabricated as a match.

The combined case reopened in **21,092 ms on Windows**, but on Linux returned
controlled `DeadlineExceeded` (error 8) at **30,000 ms** and published no Ready
backend. An unchanged second Linux open with a test-only 90-second deadline
completed in **37,870 ms** and read a retained primary record, verifying all
100,000 records and 746,656 generations. The failure is therefore a real
startup-deadline conflict for this valid private state, not observed corruption,
physical exhaustion or an inability to decode its values. The retained Linux
fixture's database EOF was **57,155,584 bytes** and journal EOF **46,632
bytes**, well below the proposed 1-GiB and 1,075,904,512-byte ceilings. The source/test
fixture creates no public Query/2B surface and changes no production deadline.
The final test-only `DerivedTests.cpp` SHA-256 is
`660316d2897ecc17e23049abea74b7d49123d67716febf0bf9c17f55f64eccbc`.
This hash identifies the earlier fixture revision, before the continuation's
additional heavy-primary and ACTIVE-only modes; it is not the final dirty-tree
test-source hash. The continuation's final local test-source SHA-256 is
`51598b1aca2199bf4aa055a70aa3b82e7cc58ce2570093b7c5688ff213cc6919`;
the private validator source SHA-256 is
`95a4af2937a86a652ea3334e953fc99968fde201554f393cc87166602ce74c6c`.
After the probe addition, the ordinary `PersistenceDerived` suite passed 1/1
on both Windows MSVC (9.73 s) and Linux GCC (19.57 s); API, architecture and
tracked-diff whitespace checks passed. These are local dirty-tree checks, not
final-source hosted CI. Test fixtures are retained only in the task-specific
worker directories; no qualification container remains running.

This satisfies the task's *legitimate startup cannot be bounded under the
current lifecycle/resource rules* stop condition. D22 currently promises no
pressure-driven eviction and retains prepared fields after hints disappear;
D21 keeps the 30-second startup supervisor. Choosing a new global retained
field/generation ceiling, deciding how an inherited over-ceiling database is
safely withdrawn/cleaned, or changing when a validated worker may publish Ready
requires a separate canonical policy and proof. Do not select a round-number
cap from these two timing samples, weaken the D21 deadline, or mark the private
1-GiB/journal/allocation candidates final. The other 2A gates remain open.

The supervised worker now configures SQLite memory accounting before its first
SQLite initialization and sets/verifies a 128-MiB SQLite hard heap limit. Pinned
SQLite counts its pager cache, in-memory subjournal, savepoint bitvecs and
prepared statements under this allocator limit; no alternate page cache is
configured in the worker. Source audit finds one application savepoint at a
time: Prepare or one derived generation mutation/build uses `Save`, and the
outer callers iterate them sequentially. The savepoint remains necessary to
undo partially applied derived logical-capacity changes before atomically
withdrawing the affected generation. Allocation failure remains a controlled worker/
transaction failure, not a successful primary write. This is a SQLite-only
backstop, **not** a complete proof of worker process use below the existing
256-MiB limit: non-SQLite decoded values, frames, C++ containers, runtime image
and rollback/fault behavior still need their combined qualification. A
representative derived suite with the same SQLite limit observed SQLite
allocated high-waters of 4,456,232 bytes on Windows and 4,455,792 bytes on
Linux; these are not worst-case bounds.

The revised source compiled on dockerbox Windows MSVC 19.44 and BigKVM Linux
GCC 13.3. Native suites passed 20/20 Windows and 21/21 Linux; the affected
derived/crash/plan/instruction subset passed 4/4 on each. The Windows managed
supervised-worker fixture passed. A first Linux/Mono fixture run failed with an
unexplained `ObjectDisposedException` after derived seeding; the unchanged
repeat passed, including 32 lifecycle cycles and 64 foreground Gets during
eight resumed builds. Do not classify that first failure as a proven flake.
Architecture/API checks and `git diff --check` passed. After the test-only
SQLite high-water print was added, the affected derived suite passed 1/1 on
Windows and 4/4 on Linux; the Linux ASan/UBSan/leak run passed the affected
Derived and DerivedCrash tests 2/2, while its Plan/Budget executables were
built from the immediately preceding unchanged test-source snapshot and passed
2/2. These are local checks, not final-head hosted CI.
No task-owned qualification server, worker or container remained running after
the tests. The one new Windows managed-derived fixture created under the SSH
account's default directory was moved intact to
`C:\Sandbox\Codex\Artifacts\CarbonLuauPersistence2A-derived-worker-2afeeb4a262840f2a37e89d6bb407870`;
older unrelated fixtures were not touched.

The exact Linux/Mono supervised-worker suite was repeated three times on
BigKVM in an isolated task-owned Docker work directory on 2026-10-01. All three
runs passed, including seed/verify, 32 worker lifecycle cycles, eight resumed
builds/512 records, 64 Gets, two Sets and one Remove. Observed maximum Get
latencies were 36, 31 and 36 ms. The earlier `ObjectDisposedException` was
**NOT REPRODUCED / isolated observation retained**; these passing repeats do
not identify its cause or prove that the disposal race is impossible. No
production source changed for this check.

The proposed 1-GiB database, 1,075,904,512-byte journal and 2,560-MiB
operational-allocation limits remain **candidates**, not a final D21 numeric
amendment. Worst-case startup and combined process/savepoint memory, near-limit
retained/hot-journal and controlled filesystem fault matrices, final numeric
extent qualification and final-head hosted CI remain mandatory. Therefore no
2A PASS, canonical numeric adoption, implementation commit or push is claimed.

### Resumed private capacity candidate (not yet qualified)

The current uncommitted one-database backend retains the exact Foundation 1
`Records`, `Quotas`, `Totals` and value-envelope representation. Its additive
private derived relations and primary Set/Remove share one SQLite transaction;
there is no compact-primary migration, ATTACH or super-journal. The production
candidate now tries 262,144 fixed 4-KiB pages (1,024 MiB DB EOF), a
1,075,904,512-byte retained-journal EOF allowance (1,026.0625 MiB), and a
2,560-MiB observed filesystem-allocation operational budget. The 64-MiB DB
allocation variance, 64-MiB journal variance, 8-MiB diagnostic allowance and
128-KiB auxiliary allowance are not reduced. Their conservative sum is
2,186.1875 MiB, leaving 373.8125 MiB inside the proposed operational budget.
This is a planning envelope, not a never-exceeded allocated-block theorem.

The 1-GiB page cap is deliberately twice the old cap: the retained original
layout failed at 512 MiB on a legal 100,000-record corpus, while the strongest
recorded compact-layout adversary reached about 511.35 MiB. The larger cap
provides room for the unchanged primary representation and derived growth,
without promising that every below-quota physical history fits. SQLite files
are not preallocated to these ceilings. Existing logical quotas remain 16 MiB
per primary namespace, 256 MiB primary globally, 16 MiB derived per namespace
and 64 MiB derived globally. A definite pre-COMMIT FULL must leave primary and
ACTIVE index state unchanged; an uncertain COMMIT remains D21 Indeterminate.

The proposed main-journal byte allowance is source-derived for pinned SQLite
3.53.4, SHA-256
`b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189`:
one header of at most 65,536 bytes plus at most 262,144 original page records
of 4,096 + 8 bytes. `pInJournal` records each original page once; its only
bit-clearing call is in `sqlite3PagerMovepage`'s fault path, reached through
auto-vacuum page relocation, while production requires `auto_vacuum=0`.
`cache_spill=OFF` suppresses the only pinned-source `syncJournal(..., 1)`
path that can add another header; commit uses `syncJournal(..., 0)`.
Application savepoints have their own memory subjournal and do not reset this
main-journal bitset. These observations justify the candidate formula, not a
claim about arbitrary SQLite versions, modified VFS settings, externally
edited files or unbounded savepoint/subjournal memory. Production still checks
the retained extent before/after operations and rejects a hot-journal original
page count above the database cap before recovery.

On this uncommitted candidate, targeted Windows MinGW/GCC 13.2 and BigKVM
Ubuntu/ext4 GCC 13.3 tests passed for derived behavior, crash points, exact-pin
instruction-budget fixture, physical VFS trace and corruption/oversize preflight.
These were initial focused checks; the broader MSVC/Linux regression and
sanitizer results are recorded below. Dockerbox's Docker CLI was unresponsive;
it was not repaired or substituted silently. The migration/upgrade edge,
startup-quarantine, savepoint-memory and final-head hosted gates remain open.
No PASS is claimed.

The resumed original-layout capacity probe completed two bounded corpus runs on
BigKVM Linux/ext4 and the fragmented run on dockerbox Windows/NTFS. In the
100,000-record fragmented run, authoritative primary logical charge was
219,070,610 bytes; peak derived logical charge was 67,105,711 bytes; and the
database reached 589,312,000 bytes (143,875 pages), above the old 512-MiB cap
but below the proposed 1-GiB cap. The retained journal reached 168,776 bytes.
Peak observed database-plus-journal filesystem allocation was 589,488,128
bytes on BigKVM/ext4 and 589,627,392 bytes on dockerbox/NTFS. Each platform
completed 11,468 bounded derived-maintenance batches and a final full reopen
validation (1,429 ms Linux; 2,003 ms Windows). A separate Linux near-primary-
quota corpus reached 267,393,087 logical primary bytes, 67,081,155 peak
derived bytes, 424,165,376 database bytes and 424,435,712 peak observed
allocation; it completed 3,826 batches and reopened in 1,578 ms. These are
observations, not a universal-fit theorem or proof of the proposed file-extent
limits under every supported transaction/failure path.

A test-only real SQLite page-cap injection (cap at the current page count)
rejected a near-64-KiB Set against an ACTIVE index with `StorageFull` before
COMMIT. The old primary record and ACTIVE index remained intact, the new key
was absent, and reopen validation succeeded on both Windows GCC and Linux
GCC. This establishes a focused rollback path, not complete capacity-failure
qualification. Startup quarantine now limits itself to 32 coherent derived
withdrawals per committed transaction. It validates the full graph before each
batch, checks file bounds after commit and publishes no worker Ready until all
required batches complete. A 40-generation fixture passed on Windows MSVC and
Linux GCC. Process kill after the first batch left exactly 32 CLEANUP and eight
ACTIVE generations; reopening completed the second batch with all primary
values intact on both platforms. The full graph is rescanned for each batch,
however, so worst-case startup completion under the 30-second supervisor
deadline and 256-MiB worker cap remains unqualified. Savepoint/subjournal
memory remains open. No numeric D21 ceiling should be finalized, and no 2A
PASS claimed, until those and the remaining fault and hosted gates close.

Broader candidate regression (2026-09-30): the complete configured native CTest
suite passed 20/20 on dockerbox Windows x64 with MinGW/GCC 13.2 and 21/21 on
BigKVM Linux x64 with GCC 13.3. The Linux budget certificate requires the
read-only pinned SQLite source mounted at its configured `/sqlite` path; an
initial suite invocation without that mount failed only that test, which then
passed alone and in the correctly mounted full rerun. Windows managed/net48
supervised-worker tests passed after placing the test compiler's three
transitive MinGW DLLs beside the **temporary** worker executable; the
production MSVC/package path was not exercised by this substitution. The
managed fixture covered 32 resource/lifecycle cycles, lost acknowledgement,
deadline kill/reap and a resumed eight-field/512-record derived build with
64 foreground Gets, two Sets and one Remove. The equivalent BigKVM Linux/Mono
worker fixture passed inside a Docker container with `--init`, including the
parent-death/reaping check and resumed build (maximum observed foreground Get:
61 ms, versus 32 ms on Windows). The first Linux container lacked an init
process and did not observe child exit within the test's five-second bound;
reaping under `--init` resolved that environment-specific result. These are
candidate-source checks, not final-source hosted CI or live Carbon.

An additional dockerbox MSVC 19.44 x64 Release build passed the complete native
suite 20/20 and the managed/net48 supervised-worker fixture, including
parent-death, lost-ack, deadline kill/reap, resource convergence and the
eight-field/512-record resumed derived build (maximum observed foreground
Get: 16 ms). This uses the qualified Windows toolchain rather than the
temporary MinGW runtime-DLL substitution, but is still candidate-source,
not final hosted CI or live Carbon evidence.

Targeted BigKVM Linux GCC 13.3 Debug ASan/UBSan with leak detection passed
6/6 persistence tests: Derived, DerivedCrash, DerivedPlan, DerivedBudget,
Physical and Corruption. This covers the tested native derived/fault paths but
does not close the maximum savepoint/subjournal memory or full worker-limit
qualification. The complete Linux native suite passed 21/21 and Windows GCC
native suite 20/20 as above; no final-source hosted CI result is claimed.

After the bounded-startup-quarantine correction, the complete local native
matrix was rerun against that revised source: dockerbox MSVC Release 20/20,
BigKVM Linux GCC 21/21. Both managed supervised-worker fixtures passed against
the revised worker binaries (Windows maximum observed foreground Get 32 ms;
Linux/Mono in an `--init` container 31 ms). The affected Linux ASan/UBSan
Derived and DerivedCrash tests passed 2/2 with leak detection; the other four
sanitizer tests above used the immediately preceding source with unchanged
paths. These are local candidate-source results; they are not a hosted CI run,
live Carbon qualification, or proof that every admissible derived history fits
the worker memory/startup envelope.

Historical status: **BLOCKED / UNQUALIFIED — physical-fit gate failed (2026-09-29)**.
The later [single-DB bound investigation](PersistenceSingleDbBoundInvestigation.md)
records **SINGLE-DB BOUND NOT PROVEN — PROCEED TO SPLIT ARCHITECTURE**.
No larger cap or D21/D22 amendment was selected; existing evidence below remains
historical and unchanged. The next step is architecture investigation, not 2B.
Starting source: fetched `origin/main`
`b67146be26952aa297206c91a4e2c2923ab1dea8`.

[D21](Invariants.md#d21--persistence-foundation-1) and
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query)
remain authoritative. The detailed architecture is
[PersistenceFoundation2.md](PersistenceFoundation2.md); its
[final correction audit](PersistenceFoundation2-Validation.md) is design evidence,
not implementation qualification. Qualified Foundation 1 evidence remains routed
through [1A](PersistenceFoundation1A.md),
[1B](PersistenceFoundation1B-Validation.md) and [1C](PersistenceFoundation1C.md).

This phase has no public Query, options/hints, descriptor, cursor, metadata or
generated-definition addition. There is no author schema/version/migration API.
Private database evolution does not change Foundation 1 value envelopes or the
package/API/ABI/provider/addon-schema/Luau identities.

## Physical-layout investigation

The initial generic flat `WITHOUT ROWID` relation failed the mandatory physical-fit
preflight on the exact pinned SQLite 3.53.4 build. At 4-KiB pages, a 1,025-byte
tagged string sort key exceeds SQLite's 1,002-byte maximum local index payload
before other columns. Removing duplicate namespace/store columns cannot remove
that overflow. This is a rejected candidate layout, not evidence that D22 is
impossible or permission to increase D21's database ceiling.

`tests/persistence/DerivedCapacityProbe.cpp` is a standalone research target,
excluded from production packaging. It uses the Foundation 1 codec, original
primary schema/quota rows and a production-backend reopen. The fixture has
32,085 records in 32 namespaces, one store per namespace, eight legal string
fields per record, and 267,393,087 primary logical bytes (below 256 MiB).
The largest namespace has 1,007 records and 8,392,235 primary bytes.
Derived population is deliberately partial, representing BUILDING rather than
complete ACTIVE state. It reaches only the first two fields and remains below
16 MiB per namespace. Offline batch seeding is not public-API write evidence.

The research charge is conservative: all authority/field/generation/sort/record
bytes with fixed length/counter allowances, plus a 1-MiB metadata allowance.
It is not the final production derived-charge format. That allowance is not
physically materialized and cannot establish production accounting or recovery.

| Preliminary corpus/layout | Platform | Outcome |
|---|---|---|
| Repeated 1,024-byte strings; flat full authority or compact IDs | Windows; Linux full authority | SQLITE_FULL after 54,784 committed derived entries; 61,559,077 charged bytes including allowance; database 536,850,432 bytes |
| Distinct deterministic 1,024-byte strings; flat full authority | Windows / Linux | SQLITE_FULL at 54,720 entries / 61,488,357 charged bytes; database 536,743,936 bytes |
| Distinct deterministic strings; compact IDs | Windows / Linux | SQLITE_FULL at 54,656 entries / 61,417,637 charged bytes; database 536,813,568 bytes |
| Distinct deterministic strings; split prefix/suffix | Windows | Target reached: 55,635 entries / 66,059,969 charged bytes; database 379,682,816 bytes |

The split prototype stores at most 512 sort-key bytes in a prefix directory and
at most 513 in a suffix relation. Prefix order followed by suffix/record-key order
preserves complete exact-byte ordering without truncation or a candidate-filter
scan. It is a private layout alternative allowed by D22, not a public semantic
amendment. Its feasibility result does not yet qualify the production generation,
accounting, bounded-seek or crash paths.

Distinct strings use reproducible varied ASCII with row identity near the start;
this avoids relying on repeated-value interning or exceptionally favorable
common-prefix sharing. Every primary envelope is decoded again after the final
probe, including failed insert/rollback. Primary charged totals remain unchanged.
No 1,280-MiB operational-budget breach was observed at the recorded checkpoints;
these observations are not a hard physical-allocation guarantee or continuous
in-flight measurement.

Runs used the authorized `dockerbox`, bounded two-job builds, Windows NTFS and
the existing dedicated Docker ext4 volume. Research logs are under
`C:\Sandbox\Codex\Logs\CarbonLuauPersistence2A-20260929` and fixture data under
the corresponding task artifact directory or `/data/2a-capacity-*` on the
qualified volume. No database is a repository/release artifact.

## Mandatory production-layout stop

The later split **production** layout passed the long-string corpus but failed a
legal, less page-efficient primary corpus. D22's physical-fit requirement in
[section 15](PersistenceFoundation2.md#15-derived-state-resource-accounting) is not satisfied. Implementation stopped;
no physical limit, derived quota or error classification was relaxed. This is a
counterexample to qualification of the current layout, **not a proof that every
D22-preserving layout is impossible**. Return to an explicitly scoped architecture/
storage-fit decision before resuming; no amendment is adopted by this record.

The `production-fragmented` fixture seeds 100,000 Foundation 1 records across 27
addon namespaces, each with eight distinct deterministic 256-byte string fields.
It uses real codec envelopes, unchanged primary schema and quota rows, at most
32 inserted records per offline seed transaction. Each namespace stays near
8 MiB (below 16 MiB), with fewer than 10,000 records. Global primary logical
charge is 219,070,610 bytes, below 256 MiB. This is offline capacity seeding,
not 100,000 public `SetAsync` observations. Production startup validates primary
codec/identity/quota state before the test uses actual `PrepareDerived` and
`MaintainDerived` operations for two fields per store.

| Windows NTFS observation | Bytes/count |
|---|---:|
| Primary logical charge | 219,070,610 |
| Primary database before preparation | 466,608,128 |
| Database at preparation failure | 536,870,912 (131,072 pages; hard ceiling) |
| Committed derived logical charge at failure | 38,317,063 (below 67,108,864) |
| Committed derived entries | 104,456 |
| Retained journal length | 152,360 |
| Observed allocated DB + journal at failure | 537,067,520 |

Preparation failed before the first rebuild cycle converged. It did not reach
the derived logical ceiling; the backend treats physical allocation failure as
a D21 failure, not a disposable derived-quota event. No 1,280-MiB operational
budget breach was observed. The blocker is the hard **512-MiB database ceiling**,
not the operational physical-allocation figure. The fragmented counterexample
has not been rerun on Linux; Windows alone fails the required platform gate.

Receipt: `capacity-production-fragmented-win.log` in the worker log directory
above. Retained fixture:
`C:\Sandbox\Codex\Artifacts\CarbonLuauPersistence2A-20260929\production-fragmented-win-1`.
Do not resume maintenance against it or delete it as part of unrelated cleanup.

Postfailure verification reopened that same database through the production
backend, validating all 100,000 primary envelopes, quotas and derived authority/
accounting/projection state. It succeeded without resuming maintenance or issuing
a primary mutation. Database SHA-256 was unchanged before/after:
`70439577ff836e51202e4950ea64e3a46797294b1e58a1cc660dce10f6810cd2`.
Receipt: `capacity-postfailure-verify.log`. The subsequently built diagnostic
probe (adds only the verification mode) has SHA-256
`7040788ef75b20396fcae3f8d96d58f8130b7c2555c8dc4d65a93b26f2710ded`.
Successful reopen is not capacity convergence: retrying the outstanding build
was deliberately not attempted.

Exact bundled SQLite: 3.53.4, source ID
`2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`.
The source and header remain pinned; no SQLite fork or alternate local SQLite
was used. Windows Release builds used the existing MSVC worker configuration,
two compilation jobs; Linux tests used the existing GCC 13 Ubuntu 24 container.
Task artifacts remain under the worker sandbox. No qualification server or
container remains running; unrelated `drycreek-bot` and `directus-db` were left
untouched.

### Other production-layout observations (insufficient for overall PASS)

The efficient 1,024-byte-string corpus reached the production derived quota and
converged via bounded withdrawal/cleanup, using the production charge format:

| Corpus/platform | Primary logical | Peak derived logical | DB length | Journal length | Observed allocated peak |
|---|---:|---:|---:|---:|---:|
| Global / Windows | 267,393,087 | 67,081,155 | 424,165,376 | 263,168 | 424,542,208 |
| Global / Linux ext4 container | 267,393,087 | 67,081,155 | 424,165,376 | 263,168 | 424,435,712 |
| Namespace / Linux ext4 container | 16,585,540 | 16,769,001 | 53,030,912 | 234,440 | 53,272,576 |

Global runs took 3,826 maintenance batches and ended with 1,970,405 charged bytes
and 1,736 entries. The namespace run took 954 batches and ended with 432 charged
metadata bytes and zero entries. Reopen observations were 1,890 ms Windows,
1,330 ms Linux global and 75 ms Linux namespace. These were **one-cycle** runs;
the subsequent three-cycle probe source does not retroactively qualify three
cycles. Allocation readings were taken after committed batches, not continuously
inside every transaction. Favorable corpus results do not override the failure.

## Preserved implementation candidate

All changes are uncommitted work based on the starting SHA, not a published or
qualified baseline. The private database format advances from 1 to 2 while value
envelopes and public identities remain unchanged. Candidate files are
`native/src/persistence/{Backend,Derived,Sortable,Worker}.*` and their tests.

- Fixed private tables map namespace/store/field to durable generation IDs.
  Field names are bound BLOB data, never SQL syntax. No index is prepared just
  because format upgrade occurs.
- Tagged finite binary64, UTF-8 and boolean sort keys; signed zero is normalized
  only in derived comparison. Primary codec remains authoritative.
- Split prefix (up to 512 bytes) / suffix (up to 513 bytes) ordering avoids the
  rejected flat layout's long-key overflow. Prefix IDs are not ordering ranks.
- ACTIVE/BUILDING/cleanup generations, keyset checkpoints, transactional primary
  Set/Remove integration, logical-capacity withdrawal and bounded cleanup.
- Derived charges include metadata and retained generations independently of
  primary quotas. Row charges: global 65; namespace `21 + namespace`; field
  `29 + namespace + store + name`; generation `70 + checkpoint`; prefix
  `37 + prefix`; entry `33 + suffix + key`; member `30 + key`, all in bytes.
  Physical B-tree/secondary-index overhead is not a free logical row but must
  independently pass physical qualification—which currently fails.
- Existing serialized worker checks foreground input before each maintenance
  batch. No new writer, worker protocol operation, native ABI or Luau entry.
- Fixed-plan test seams only: equality/range/order/keyset probes, candidate and
  primary point-lookup counts, VM steps, sort/full-scan counters. No public Query.

## Intermediate validation and limits

These results belong to synchronized **uncommitted source snapshots**, not a
final tested commit. Later instruction-reserve and physical-test edits invalidate
claims of a single final-source full matrix; no final hosted CI was submitted.

| Gate | Measured result / qualification limit |
|---|---|
| Windows native intermediate matrix | 19/19 passed; includes derived functional/crash/sort/plan plus affected Foundation 1 and native runtime tests |
| Linux native intermediate matrix | 20/20 passed including ELF check |
| Linux ASan/UBSan/leak intermediate matrix | 20/20 passed; predates final budget/physical-test changes |
| Sortable scalar corpus | 40,475 deterministic finite values plus boundary/type/string tests |
| Derived functional/fault matrix | 17 fixtures; 40 process-kill cases and 19 fault-validation fixtures; process crash is not power loss |
| Private real-worker Windows/Linux | Existing Foundation 1 protocol/lifetime/fault checks passed; new worker fixture resumed eight BUILDING fields over 512 records while serving 64 Gets, two Sets and one Remove; one worker start, no replay/restart; observed maximum Get 32/11 ms |
| Windows managed runtime | Existing broad runtime/addon/GUI/Player suites passed |
| Windows Persistence-1C regression | 2,200 Sets, 4,400 Gets, 2,200 Removes over root + 10 addons; 12 lifecycle reopen cycles; queue/nonce, public quota and callback retirement cases passed; real worker/native VM, not live Carbon |
| Latest Windows physical savepoint test | Passed; three overlap/rebuild/partial-edit rollback-to/withdraw/cleanup rounds with VFS write/sync/truncate observation, separately from historical 1A evidence |
| Latest Windows budget test | Passed measured boundary cases and current bytecode test, but proof gaps below prevent final qualification |
| Static API/architecture/tooling checks | Passed before stop; final documentation checks recorded separately |
| Final packaging / hosted CI / live Carbon | Not run for this candidate; earlier Foundation 1 evidence is not new 2A qualification |

The latest physical observation recorded 47,815 writes / 53,765 checkpoints,
12,763,136-byte database and 144,152-byte journal high-water lengths, with
12,959,744 bytes peak allocated. The derived rounds contributed 6,766 observed
writes. This small-workload trace does not qualify scale memory or storage fit.
The budget test observed 76 programs, maximum 50 opcodes/program, modeled maximum
unchecked span 45 and rollback length at most four. Threshold cases
100/500/1,000/5,000/20,000/934,464 observed respectively
94/503/993/4,989/14,405/14,405 VM steps, including rollback. The first four
interrupted and rolled back; the latter two committed. See the proof limits below.

After recording the stop, `Test-Api.ps1`, `Test-Architecture.ps1`,
`Test-ToolingContracts.ps1` (17 preview goldens) and `git diff --check` passed.
These consistency checks do not close a runtime gate. The main failure,
postfailure verification and latest targeted-test logs were also copied to the
ignored local `build/persistence2a/evidence/` directory; no DB was copied into
the repository.

### Unclosed instruction and journal obligations

The candidate reserves 65,536 of the 1,000,000 instruction budget for an unchecked
SQLite span and failure rollback. The corrected test models `Init` as non-polling,
rejects indirect `Return` and selected unsupported transfers, matches production
connection settings and analyzes whole outer ROLLBACK separately. Its passing
measurements are **not sufficient proof**: review still requires explicit
exclusion of nested execution (`SqlExec`, `ParseSchema`, `Vacuum`, unsupported
virtual-table opcodes), and complete fail-closed SQL-family inventory rather than
only checking a count of 76 extracted strings. No final hard-bound claim is made.

Pinned pager review found successful `ROLLBACK TO` preserves transaction
`pInJournal`, uses a separate replay bitmap, and restores the previous effective
main-journal end. It does not itself allow duplicate main-journal records. Keep
the no-auto-vacuum/fixed-workload restriction; the separate page-movement error
path can clear that bitmap. Memory subjournals use 4,100-byte page records and
still need memory-envelope qualification. Historical 1A proof excluded application
savepoints and cannot be cited as covering this new path.

## Original implementation-stop handoff

**Persistence-2A BLOCKED; do not begin Persistence-2B.** Preserve the candidate,
negative fixtures and logs. Required next authorization is a scoped storage-fit/
architecture decision: either establish a D21/D22-preserving physical design or
explicitly amend the appropriate canonical owner. Do not silently lower derived
quotas, raise page limits, compress/truncate primary semantics, or relabel
physical failure as logical withdrawal. Budget-proof and memory obligations also
remain open. No commit, push, release, tag or deployment is qualified by this task.

## Authorized storage-fit investigation — 2026-09-29

**Root cause established; compact-layout feasibility measured; universal fit NOT
PROVEN.** The user authorized architecture/prototype work only, preserving the
uncommitted runtime candidate and D21/D22. The following experiments do not adopt
a new production layout, close 2A, authorize 2B or amend a quota/page-size limit.
Instruction-budget proof and savepoint-memory qualification remain pending.

### Attribution, not an assumed two-times multiplier

The failing database was opened read-only using the exact pinned amalgamation
with `SQLITE_ENABLE_DBSTAT_VTAB` added **only to the standalone diagnostic build**.
SQLite documents [DBSTAT](https://www.sqlite.org/dbstat.html) as read-only B-tree
space accounting, excluding free-list and certain other non-B-tree pages; the
probe therefore records `page_count` and `freelist_count` separately. It does
not use the system SQLite or change the production compile flags.

Exact failing-corpus accounting:

| Component | Observation |
|---|---:|
| Primary envelopes | 218,500,000 bytes, exactly 2,185 per record |
| Charged primary store + key bytes | 570,610 |
| Uncharged repeated namespace bytes in primary SQL rows | 1,061,700 |
| Primary `Records` B-tree internal + leaf pages | 13,902 / 56,942,592 bytes |
| Primary overflow pages | 100,000 / 409,600,000 bytes |
| Payload in those overflow pages | 172,132,310 bytes |
| Unused space in those overflow pages | **237,067,690 bytes** |
| Free-list pages at failure | **0** |
| `DerivedPrefixes` table | 30,629,888 bytes |
| `DerivedPrefixOrder` secondary index | 31,100,928 bytes |

The complete `Records` table occupies 466,542,592 bytes. There is no separate
primary lookup index in the original `WITHOUT ROWID` layout. Most amplification
is **intra-overflow-page slack**, not free-list accumulation, identity strings,
an envelope-sized duplicate primary index, filesystem allocation granularity or
the journal. Store/key bytes already count in primary logical quota. The
44-byte envelope framing/checksum costs 4,400,000 bytes here and is part of the
authoritative codec, not an excuse to remove integrity checks.

For this pin, `sqlite3.c:76666–76669` gives 4-KiB-page index `maxLocal=1002`,
`minLocal=489`, and rowid-table leaf `maxLeaf=4061`. Overflow selection at
`74412–74434` keeps the minimum local payload when the surplus does not fit.
The original `WITHOUT ROWID` records are index-B-tree cells: all these medium
envelopes spill to one mostly empty overflow page. SQLite's
[WITHOUT ROWID guidance](https://www.sqlite.org/withoutrowid.html) likewise makes
clear that this is a workload-dependent optimization, not universally smaller
storage. No page-size change or SQLite fork was tested/adopted as a solution.

The failure had 208.92 MiB primary logical state and 36.54 MiB derived state,
not 219 MiB and 38 MiB. The corresponding combined-logical/file ratio is about
2.086, but that ratio is specific to this corpus and not a usable global bound.

### Isolated layout comparisons

`tests/persistence/storagefit/` is a new standalone research CMake project. No
production build includes it. Inputs are always read-only; destinations must not
exist. The source pin hashes are checked before compilation. Destination files
use 4-KiB pages, a 131,072-page cap, PERSIST/EXTRA, no spill/mmap, memory temp,
and the stock platform VFS. No compression, VACUUM, author-data truncation or
logical-quota reduction is used.

| Same 100,000-record failing corpus / Windows | Database bytes | MiB | Result |
|---|---:|---:|---|
| Original production candidate | 536,870,912 | 512.00 | Failed during derived preparation |
| Fresh recopy, same `WITHOUT ROWID` layout | — | — | SQLITE_FULL during recopy; no fit claim |
| Ordinary rowid `Records`, same composite unique key | 483,999,744 | 461.58 | All primary envelopes/identities/charges verified exactly |
| Normalized store IDs + bounded envelope chunks; original derived layout | 312,872,960 | 298.38 | Same exact primary verification |
| Same chunks + clustered derived relations | 315,232,256 | 300.63 | Same exact primary verification; **worse**, rejected optimization |

The chunk prototype uses a private store-ID directory, integer record identity,
the original exact record key/charge, and `Chunks(RecordId, Part, Payload)` with
at most 768 payload bytes per row. Concatenation must reproduce the complete
Foundation 1 envelope exactly; the production codec is unchanged. A maximum
64-KiB envelope requires at most 86 chunks. This is not a public storage contract.

The clustered derived alternative replaced prefix/suffix-order secondary indexes
with clustered primary keys and ID lookup indexes. SQLite stores the clustered
primary key in those secondary indexes, so the long sort bytes were still
duplicated. It did not remove the required reverse-lookup cost. Removing that
cost would need a different access-path/ownership design, not simply changing
`CREATE TABLE` syntax. The successful full-quota probes below retain the
**original derived relations and charge model**.

### Quota-scale feasibility, not a universal guarantee

The full-quota corpus adds one legal, unindexed `ResearchPadding` field using the
unchanged Foundation 1 encoder/decoder, while preserving all original indexed
field values. It reaches **exactly 268,435,456 primary logical bytes**, with
namespace/key/count limits checked. It is a new synthetic corpus, not a claim
that padding is byte-identical to the original records. Research verification
checks the exact expected padded envelope for every record.

The unchanged candidate `Derived` engine then continues or prepares indexes
against a **prototype-only compatibility view and bounded envelope reconstruction
function**. That function is not proposed production SQL or a public binding.
The real production backend correctly does not accept this alternate schema.
Derived validation runs before/after maintenance, and SQLite integrity checks
pass. This measures storage feasibility, not production migration, five-second
service qualification, total VM-instruction proof or runtime completion behavior.

| Full 256-MiB primary workload | Platform | Peak derived logical bytes | Peak database bytes | Headroom below 512 MiB |
|---|---|---:|---:|---:|
| 100,000 records, sequential private IDs | Windows NTFS | 67,105,711 | 432,832,512 | 99.22 MiB |
| Same corpus | Linux ext4 Docker volume | 67,105,711 | 432,832,512 | 99.22 MiB |
| 100,000 records, permuted private IDs | Windows NTFS | 67,105,711 | 482,287,616 | 52.05 MiB |
| 87,500 records, nearly four full chunks per envelope, permuted IDs | Windows NTFS | 67,098,261 | 461,479,936 | 71.89 MiB |
| Shrink/refill, 100,000 mixed-size records | Windows NTFS | 67,104,029 | 502,493,184 | **32.79 MiB** |
| Same shrink/refill corpus | Linux ext4 Docker volume | 67,104,029 | 502,493,184 | **32.79 MiB** |

The first two cases used 8,202 continuation/cleanup batches. The four-chunk case
used 11,465 batches and three prepared fields per store; the churn case used
11,428. Logical capacity withdrawal and cleanup are exercised, so peak retained
derived charge is just below the 64-MiB ceiling, not exactly every last byte.
Measurements are committed-state database extents, not continuous filesystem
allocation or journal-write high-water proof.

The permuted insertion sequence is exactly `(Sequence - 1) * 65537 mod N + 1`,
not a claim of random sampling or exhaustive insertion orders. Source-derived
field values remain case/byte-exact, without compression or deduplication of
distinct values.

Churn starts with 87,500 full-quota values, shrinks their envelopes to 1,536
bytes without changing the three indexed fields, then inserts 12,500 larger
values to refill the exact primary quota. All 100,000 final envelopes decode and
their charges agree; no namespace exceeds 16 MiB or 10,000 records. Shrink freed
25,159 pages; refill reused them without VACUUM and increased the database from
82,631 to 92,637 pages before derived preparation. Cleanup leaves free pages
inside the high-water file, not an unbounded leak or proof that the file shrinks.

### Why the requested worst-case inequality is still open

No tested compact-layout case establishes
`all permitted primary + all permitted derived + all metadata/history <= 512 MiB`.
In particular:

- Four observed record-size/insertion/churn shapes are not all allowed sizes,
  names/keys, type mixtures, metadata-only states or mutation histories.
- Eliminating overflow does not guarantee B-tree fill. The pinned balancing
  tests at `sqlite3.c:82368`, `83129`, `83227` can retain pages with up to roughly
  two-thirds free space. A proof assuming 70–90% occupancy from fresh fixtures is
  unsound. This observation does not prove all pages can simultaneously attain
  the worst fill under a valid workload; it shows why the simple bound fails.
- At 768 bytes/chunk, a conservative row-count bound is
  `ceil(256 MiB / 768) + 100,000 = 449,526`, before directory/index records.
  Combining that with loose SQLite occupancy bounds does **not** close 512 MiB.
  Empirical fit cannot replace the missing tighter reachable-state bound.
- Existing databases already near 512 MiB cannot be migrated by retaining a
  full old and full new copy in the same capped file. A safe bounded in-place
  migration, bootstrap headroom, crash states and mixed-format reads remain
  undesigned/unqualified. The offline copy experiment is not that migration.
- Maintenance journal growth, rollback memory, instruction work, ACTIVE/BUILDING
  overlap and repeated lifecycle qualification must be redone for any adopted
  adapter. Earlier production-candidate results cannot qualify a different one.

**Recommendation: do not amend D21/D22 yet.** The original overflow pathology is
avoidable, and quota-scale examples have meaningful but history-sensitive
headroom. The next scoped work should establish an auditable compact-layout
occupancy/metadata bound and migration strategy before adopting production
changes. If ordinary chunk relations cannot supply that bound, compare a bounded
packing/compaction design against the cost of a canonical amendment; do not
silently add a custom storage allocator or an unbounded maintenance pass.

If a later amendment becomes necessary, it must be an explicit decision—not a
new constant chosen to pass these fixtures. Increasing the database cap would
also require reviewing its journal extent, operational allocation budget, worker
memory and recovery envelope. Reducing either logical quota changes a canonical
capacity contract. Neither is approved or proposed as an adopted rule here.

### Reproduction and provenance

Research root: `tests/persistence/storagefit`. Configure with the exact pinned
`SQLITE_AMALGAMATION_DIR`; build target `StorageFitProbe`. The CLI is:

~~~text
StorageFitProbe source.sqlite3
StorageFitProbe source.sqlite3 NEW-destination.sqlite3 MODE
~~~

Modes: `without`, `rowid`, `chunks`, `chunks-compact`, `chunks-full`,
`chunks-full-random` (the fixed permutation), `chunks-square`, `chunks-churn`.
The last four require the recorded synthetic source shape. Never use these
research transforms against an operator database. They are not runtime upgrades.

New Windows research builds/logs/artifacts are beneath
`C:\Sandbox\Codex\{Builds,Logs,Artifacts}\CarbonLuauStorageFit-20260929`.
Linux uses the existing isolated durability volume and task-prefixed directories;
no ports or test server are needed. Windows uses MSVC/Release, Linux GCC 13/Release;
build concurrency is at most two jobs. Logs distinguish earlier research-source
snapshots from the final churn source. No release package includes these probes.

No production persistence source was changed during this investigation. The
original failed database and the prior uncommitted implementation are preserved.
No public Query/API, codec change, identity bump, commit, push, release or D21/D22
amendment is authorized by these results. **2A remains BLOCKED / UNQUALIFIED.**

Final research source SHA-256:

- `Probe.cpp`: `2765b2b66f37f98643234e58a6b0e740f54d791779892711e55f2b121d21d56b`.
- `CMakeLists.txt`: `8b978026a48eaf69f5782757564f06144436cf9d82d3ca9cb290666d08139926`.
- Windows final research executable:
  `735c79feeb297e4c2c5698447e1e71f59fda73b0e1a1c65bab2e7b5b311c24a4`.

The final source ran the Windows/Linux churn cases; earlier layout experiments
used the recorded preceding versions of the same probe, not a single falsely
claimed final-source full suite. All nine existing files in
`native/src/persistence/` were hash-compared before/after and are unchanged.
The original failure database still hashes to
`70439577ff836e51202e4950ea64e3a46797294b1e58a1cc660dce10f6810cd2`.
Logs were copied to ignored `build/persistence2a/storagefit-evidence/`; no database
was copied into the checkout. API/link, architecture and whitespace checks pass.
Research containers exited and were removed; the two unrelated worker containers
remain running. Retained fixtures/builds/logs stay in the task sandbox for future
verification. `main` and the last fetched `origin/main` remain `b67146b`; the
working tree remains intentionally uncommitted, including the earlier candidate.

## Compact proof and migration continuation — 2026-09-30

**STORAGE FIT BLOCKED — BACKEND/LAYOUT REDESIGN REQUIRED.** This means the
investigated layout has not established the required capacity/maintenance proof,
not that all private layouts are impossible. No D21/D22 amendment is established
as necessary or adopted. Do not promote this research into production or begin
2B. The four prerequisite gates (universal fit, migration safety, instruction
proof, savepoint-memory envelope) remain open.

Starting checkout was `b67146be26952aa297206c91a4e2c2923ab1dea8` with the prior
uncommitted implementation/research. Fetch found two README-only commits; a
non-overlapping fast-forward updated `main` to current fetched `origin/main`
`928b9d7b60fcdba508b322fbc824c83ccab5cc9f`. Existing work was preserved. This
continuation changes only standalone research and this evidence record; all nine
files in `native/src/persistence/` were hash-compared and remain unchanged from
the start of this continuation. No production layout or public API is selected.

### Expanded evidence and an infrastructure exclusion

The research matrix now accepts chunk sizes 512, 640, 768, 896 and 960 bytes.
These are test controls, not operator/author configuration. Five sizes passed
72 value-shape cases each on Windows Release and Linux Release/ASan/UBSan:
booleans, finite binary64 edge cases, strings from empty through 16 KiB, exact
64-KiB envelopes, depth 16, 4,096 total entries, 1,024-entry maps, 128-byte map
keys, root/addon identities, 64-byte store/128-byte record names and large private
integer IDs. Exact envelope reassembly/codec decode and savepoint rollback were
checked. DBSTAT reported no chunk overflow pages. This is **not** a quota-scale
test of every name/type/distribution combination or full transaction memory proof.

| Chunk bytes | Maximum chunks/value | Small shape corpus pages | Maximum observed single INSERT VDBE steps |
|---|---:|---:|---:|
| 512 | 128 | 104 | 19 |
| 640 | 103 | 98 | 19 |
| 768 | 86 | 99 | 19 |
| 896 | 74 | 105 | 19 |
| 960 | 69 | 98 | 19 |

Windows/Linux page and instruction observations matched. SQLite allocation
high-water for the shape process was 635,232 / 635,256 bytes respectively; these
are SQLite allocator observations, not process RSS or the complete worker budget.
The ASan/UBSan/leak-detection run also covered real-F1 migration and its expected
allocation-clamp failure with unchanged database hash. It exited zero with no
sanitizer diagnostic. No production allocator-fault matrix is newly qualified.

The expanded churn probe retains the original 87,500-record setup and 12,500
refill records, and checks all final envelopes, namespace/key quotas and exact
268,435,456-byte primary charge. Shrink writes are now `INSERT OR REPLACE` of
parts followed by bounded tail deletion; this is a research mutation path, not
the previously tested UPDATE-only path or an adopted production algorithm.
Original indexed F0..F2 values survive; derived builds use the unchanged private
engine and its original accounting. Active/building/cleanup state is not silently
given another quota. The following successful runs completed integrity and
derived validation:

| Chunk / shrink envelope | Platform | Peak derived logical bytes | Peak DB bytes | Headroom |
|---|---|---:|---:|---:|
| 512 / 1,024 | Windows | 67,104,029 | 456,044,544 | 77.08 MiB |
| 768 / 1,024 | Windows / Linux | 67,104,029 | 442,736,640 | 89.77 MiB |
| 896 / 1,024 | Windows | 67,104,029 | 454,877,184 | 78.20 MiB |
| 960 / 1,024 | Windows | 67,104,029 | 444,731,392 | 87.87 MiB |
| 768 / 2,048 | Windows | 67,104,029 | **536,190,976** | **679,936 bytes (664 KiB)** |

The 1,024-byte runs used 11,449 maintenance batches; the 2,048-byte run used
11,413. Shrinking to 2,048 freed only 6,931 pages in the 82,631-page initial
file; refill reached 100,864 pages with no free-list pages before derived work.
A smaller shrink therefore left much worse reusable space than the deeper
1,024-byte shrink. Fresh-file mean occupancy is not a valid history bound.

**Excluded/inconclusive runs:** Windows 768/1,537 reached 131,072 pages and
reported StorageFull at maintenance batch 4,725, with primary 268,435,456 and
derived 55,273,476 bytes. However, subsequent 768/1,792 refill failed at only
375,443,456 DB bytes, and a wider-ID probe also failed early. Docker then reported
its filesystem read-only, including its own container metadata database; SSH
subsequently timed out during handshake. Host free-space/health could not be
obtained. These later failures **must not be attributed solely to SQLite's
page cap**, used to prove a required quota amendment, or counted as qualified
counterexamples until repeated on healthy storage with filesystem capacity
monitored. The Linux boundary continuation ended with Docker exit 125 and is
not Linux capacity qualification. No host repair, Docker restart or security
change was attempted. Successful earlier runs remain observations with their
stated scope, not a universal fit claim.

No further probes were launched after the infrastructure fault was detected.
All launched Windows probe commands returned. Docker task-container removal and
the final remote process/fixture-hash checks could not be verified after SSH
became unavailable. No unrelated container was intentionally touched. Retain the
task files until the worker is healthy; any cleanup must name only these owned
fixtures and preserve the original negative evidence. No fixtures were deleted
in this continuation.

### Capacity proof audit

The original overflow diagnosis and every previous negative receipt above remain
unchanged. Chunking removes that particular overflow pathology, not the need for
an occupancy/history proof. Exact-pin source still gives index payload maxLocal
1,002, minLocal 489, 4,096-byte pages and the 131,072-page cap. At these proposed
sizes even an eight-byte record ID plus chunk ordinal/SQLite record headers fits
locally; the large-ID shape tests confirm representative cases, not all B-tree
history bounds.

The [Foundation 1 count limit](PersistenceFoundation1.md#5-logical-names-and-bounds)
is explicitly **100,000 records globally**, 10,000 per namespace, 64 nonempty
stores/namespace and 256 namespaces. Minimum boolean envelope is 46 bytes, so a
one-byte store and key charge at least 48 bytes. The byte-only estimate
`floor(268435456 / 48) = 5,592,405` does not override the stricter canonical
100,000 count. With chunk size C and N records, a conservative chunk-count bound
is `ceil(268435456 / C) + N`; it deliberately overcounts because names/keys also
consume primary quota. At C=768 this is 449,526 chunks. These bounds hold for
all codec shapes, not just the map fixture.

That does **not** finish the database inequality:

- Primary identity lookup duplicates record keys in a secondary index. Store
  normalization removes repetition but adds a directory/index. Production must
  bound or reclaim empty directory entries; never retain uncharged historical
  stores indefinitely. A record-ID allocator also needs an explicit internal
  width/reuse proof. The wider-ID experiment is inconclusive, not evidence that
  narrow initial IDs are an enduring guarantee.
- At most eight fields/store and two total retained generations/field are the
  candidate's bounds, including cleanup. All retained metadata/members/entries/
  prefixes consume the one 64-MiB global / 16-MiB namespace derived allowance.
  It must **not** be doubled for ACTIVE + BUILDING or made free during cleanup.
  Long/short sort keys, duplicate prefixes, record-key duplication, separate
  reverse indexes and metadata-only states have different physical/logical
  ratios. No complete worst-case derived-page bound has been certified.
- Pinned `balance()` can leave a page alone while free space is at most two
  thirds of usable bytes (`sqlite3.c:82368`); delete follows the corresponding
  threshold near `83129`. This is not a 70–90% minimum-fill promise. Root pages,
  internal fanout, sibling redistribution, cell sizes and transient allocations
  require separate accounting. A loose three-times-payload estimate already
  cannot close the requested 512-MiB inequality; it is neither an exact upper
  bound for the entire layout nor a proof that all worst fills coexist.
- Free-list pages are reusable, not permanently lost, but DB high-water does
  not shrink automatically. A proof must include pages temporarily needed for
  splits/updates before reclamation, not add all historical free pages forever
  or count only post-cleanup occupied pages. Near-cap success of one workload
  leaves no proven margin for other legal shapes or overlap.

Consequently the **total mathematical bound and guaranteed headroom are not
established**. The largest successfully verified compact sample in this
continuation is 511.3515625 MiB, not an upper bound. Neither increasing C nor
adding unbounded VACUUM establishes the missing invariant. No chunk size is
promoted to production. A defensible next design must explicitly control/prove
packing, transient headroom, ID/directory history and bounded reclamation, or
present a separately justified canonical capacity amendment. The investigation
does not justify an exact replacement D21/D22 limit yet.

### Concrete migration prototype and remaining design obligations

`tests/persistence/storagefit/Migration.cpp` copies a **closed test fixture** to
an isolated destination for test setup only. The migration inside that database
does not duplicate the whole primary store. The prototype:

1. Loads at most 32 validated complete envelopes / 256 KiB per batch.
2. Begins one PERSIST/EXTRA transaction, deletes those legacy rows first, then
   inserts normalized identities and ordered envelope chunks into reclaimed
   space. Quota rows are not changed or double-charged.
3. Bootstraps the destination tables plus a private migration state and research
   format marker atomically with the first batch. Remaining legacy rows form a
   durable worklist; committed chunk rows represent completed progress.
4. Commits each batch; a final private complete flag marks exhaustion. The
   research verifier retains the empty old table. Production schema retirement,
   corruption validation and bounded completion would still need qualification.

Every envelope, namespace/store/key identity and charge is compared with the
read-only original; original Quotas/Totals rows must agree exactly. This is not
a production adapter, a public migration API or a change to codec version 1.
The research marker `user_version=3` is **not adopted** as a production format.

The genuine qualified Foundation 1 fixture is the retained Windows live-server
restart file from the 1C receipt:
`persistence1b-20260924-041757-f7b9a974/fixture-retained/data/persistence/store.sqlite3`.
It has private format 1 and three root/addon records. SHA-256:
`e5eb39ef1ec364b8c88103a0600406d7dd9b2827e19b1f79431de8428fecf563`.

| Migration observation | Windows | Linux ext4 |
|---|---|---|
| Real F1 fixture, exact bytes/identities/quotas | 3/3; 4 to 10 pages | Same |
| Exit after delete, after schema, before COMMIT, after COMMIT; reopen/resume | All four verified | All four verified |
| SQLite memory high-water, one normal batch | 307,784 bytes | Approximately 308,000 bytes |
| Diagnostic max-page clamp to existing four pages | Controlled FULL; DB hash unchanged | Same under sanitizer |
| Retained 512-MiB original failure fixture | 100,000/100,000; 3,125 batches | Same |
| Near-cap final extent/free-list | 131,072 / 54,676 pages | Same |
| Near-cap SQLite allocation high-water | 4,565,536 bytes | 4,564,976 bytes |

The near-cap fixture is the **uncommitted format-2 failure corpus**, not a genuine
released format-1 file. It uses the unchanged Foundation 1 primary representation;
existing derived rows are preserved, not newly qualified by this migration.
Real-old-format compatibility and near-ceiling page reuse were measured on
different fixtures. The four-page clamp is failure injection, **not** a synthetic
512-MiB capacity counterexample. Process exit tests are not OS/power-loss tests.

For a production plan, choose primary migration **before derived build** and
transient dual-format private reads/writes. Do not hold startup or an accepted
request open for the entire multi-batch migration. Get performs exact-point
lookup in the appropriate old/new representation; Set must atomically remove
any legacy representation, write compact form and apply only its logical delta;
Remove must atomically remove the one authoritative representation. Duplicate
ownership or inconsistent transition metadata must fail closed. Background
conversion yields to foreground requests and resumes only committed progress.
This is the proposed architecture, **not implemented/qualified dual-format
foreground service**. The prototype only demonstrates conversion and recovery.

No author operation is replayed by conversion. A crash sees old or new complete
envelopes, never half a public value. On FULL/IO/corruption, preserve files and
roll back the current batch; prior converted batches remain authoritative.
If transition/profile validation cannot establish safe service, fail persistence
startup/availability controlledly rather than erase data or raise the cap.
Whether the first bounded batch can always release sufficient bootstrap pages
at the real ceiling remains unproven. The small clamp test shows why bootstrap
allocation cannot be assumed free. Normal reads/writes, mixed-state corruption,
mid-large-migration crash points and bootstrap selection need further proof.

No reverse migration is proposed. An older implementation must reject the new
private format without destructive repair. Actual downgrade-rejection testing
is still pending; don't claim that choosing an integer marker qualifies it.

### Instruction, memory and maintenance gates

The prior 76-family VDBE certificate gap is not closed. A compact adapter changes
statement families, and the research `ResearchEnvelope` SQL function performs
nested statements: it is **not** a proposed production route or evidence that
the outer statement's counter measures all work. Production would need direct
bounded chunk access, an aggregate budget across all statements/operations,
fail-closed opcode/family coverage and a separately bounded rollback reserve.
The observed 19 steps for one small chunk INSERT does not establish a complete
operation or D22's 1,000,000-instruction gate.

The 360 shape cases and migration allocation readings do not cover worst-case
max-size overwrite + eight fields + ACTIVE/BUILDING dual maintenance + partial
withdrawal/savepoint rollback. SQLite memory high-water also excludes application
buffers/process overhead. That **256-MiB worker-envelope proof remains open**;
no reliance on swap or larger worker limit is authorized. No production memory
or instruction threshold was changed.

Only ordinary transactional page reuse was exercised. No VACUUM, incremental
vacuum, custom VFS, compression or added compaction policy is adopted. Any
stronger packing/maintenance design must be bounded, worker-owned, crash-safe
and fit its own temporary-space/instruction/memory envelope before production.

### Completion report and resumption boundary

| Requested report item | Disposition |
|---|---|
| 1 | STORAGE FIT BLOCKED — BACKEND/LAYOUT REDESIGN REQUIRED; no universal-impossibility claim |
| 2, 37 | Started b67146b plus preserved dirty work; main fast-forwarded to fetched origin/main 928b9d7; still intentionally dirty |
| 3–4 | Original 512-MiB failure/overflow diagnosis preserved above |
| 5–7 | Normalized identities + chunked unchanged envelopes remain a candidate, not selected; five sizes; unchanged 4-KiB/131,072-page assumptions |
| 8 | Explicit 100,000 canonical record cap dominates byte-only minimum-value estimate |
| 9–15 | Chunk/record counts bounded; complete primary/derived/overlap/metadata/churn page inequality NOT PROVEN |
| 16–17 | Guaranteed headroom unknown; largest successful compact sample 536,190,976 bytes, only 679,936 bytes headroom; later failed samples inconclusive due worker fault |
| 18–19 | Windows and Linux results above are scoped observations; Linux boundary continuation interrupted by read-only storage; no final whole-matrix qualification |
| 20–27 | Bounded delete/reinsert migration prototype, real-F1/capacity/crash results above; online dual-format, universal bootstrap, corruption and downgrade still unqualified |
| 28–29 | Full instruction proof and maximum savepoint/process-memory qualification still pending |
| 30 | Transactional page reuse only; no new maintenance/compaction adopted |
| 31–32 | D21/D22 amendment required: **not established**; neither amended; no justified new numeric bound to propose |
| 33 | Before production changes: resolve layout/packing/headroom and ID/directory bounds, then dual-format migration/validation, aggregate instruction certificate and memory/fault qualification |
| 34 | Four proof gates plus healthy-worker reruns, capacity monitoring, source/artifact collection and cleanup verification |
| 35 | No public Query, hints, schema/index API or 2B work added |
| 36 | No commit/push/release/tag; incomplete research is not a qualified implementation baseline |

Remote logs/artifacts/builds are under
`C:\Sandbox\Codex\{Logs,Artifacts,Builds}\CarbonLuauStorageProof-20260930`;
some incremental binaries reused `Builds\CarbonLuauStorageFit-20260929`.
Linux fixtures are in the existing dedicated durability volume under
`/data/storageproof-20260930`. Source is the established persistence workspace.
Builds used at most two jobs; research containers had two CPUs and 3–4 GiB caps,
no published ports. The worker's storage-health fault prevents final receipt
collection and cleanup verification. Preserve both original fixtures and new
negative/inconclusive receipts when reclaiming only reproducible task data.

Final local research hashes (runs used successive snapshots, not a falsely
claimed single final-source full matrix):

- `CMakeLists.txt`: `ab976b9cb40ebcbdf188a85f1debb732734fae1ca2534720d05ee3266bf008f2`.
- `Migration.cpp`: `cdcf43cee495b44ba3d6342a78f732daab13168bdb494ce09beb2bc83f79906e`.
- `Probe.cpp`: `ddd0ba38edb272a3efc1496e9a36d4a497a0ddf8bce5d1eb7a75c13b18e4aad9`.
- `Shapes.cpp`: `814521ee5df5077bc483946b04d0ba988f0c9e9ef0ffd43d405195b2af6d834b`.

Next action is to restore/confirm worker storage health, collect receipts and
repeat the excluded boundary cases with adequate measured host/volume space.
Do not infer a D21 amendment or resume production implementation from this
continuation. Persistence-2B remains closed.

Local closing checks: `tools/Test-Api.ps1`, `tools/Test-Architecture.ps1` and
`git diff --check` pass. The two stalled, task-owned local SSH diagnostics were
terminated after another bounded connection check timed out; this is not proof
of remote cleanup. Final hosted CI, worker receipt download and remote artifact
hash/cleanup verification were not completed. No commit or push was made.
