# Persistence Foundation 2A — private derived-index substrate

## Final private-substrate closure — 2026-10-01

**PERSISTENCE-2A PASS — READY FOR PERSISTENCE-2B.** This qualifies the private
derived-index substrate, not a public Query API, a package release or any 2B
implementation. The implementation commit is `6330574`; the locally qualified
implementation and policy/evidence source is `c6c300b`. [Hosted validation
run 36839009473](https://github.com/gmoddev/CarbonLuau/actions/runs/36839009473)
passed Windows, Linux and sanitizer jobs at that exact revision, including
clean bundle installation and private-worker packaging. [Documentation
deployment 36839009399](https://github.com/gmoddev/CarbonLuau/actions/runs/36839009399)
passed and the published 2A Markdown returned HTTP 200 with this record's
title. Final closure wording is a docs-only follow-up to the tested source;
runtime/ABI/API files are unchanged.

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
documented below; it does not erase those failed fixtures. The strict
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

**No 2A qualification gate remains open.** The dated PARTIAL/BLOCKED results
below are historical evidence from earlier candidate designs, not a second
current readiness contract.

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

## Earlier attempts

The original resumes, blocked gates and discarded layout research remain in
[the historical attempt record](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts).
They retain their original scope; they do not reopen the closed qualification.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="final-qualification-continuation-2026-09-30-still-partial"></a>

- [Final-qualification continuation (2026-09-30; still PARTIAL)](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-final-qualification-continuation-2026-09-30-still-partial)

<a id="resumed-private-capacity-candidate-not-yet-qualified"></a>

- [Resumed private capacity candidate (not yet qualified)](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-resumed-private-capacity-candidate-not-yet-qualified)

<a id="physical-layout-investigation"></a>

- [Physical-layout investigation](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-physical-layout-investigation)

<a id="mandatory-production-layout-stop"></a>

- [Mandatory production-layout stop](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-mandatory-production-layout-stop)

<a id="other-production-layout-observations-insufficient-for-overall-pass"></a>

- [Other production-layout observations (insufficient for overall PASS)](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-other-production-layout-observations-insufficient-for-overall-pass)

<a id="preserved-implementation-candidate"></a>

- [Preserved implementation candidate](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-preserved-implementation-candidate)

<a id="intermediate-validation-and-limits"></a>

- [Intermediate validation and limits](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-intermediate-validation-and-limits)

<a id="unclosed-instruction-and-journal-obligations"></a>

- [Unclosed instruction and journal obligations](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-unclosed-instruction-and-journal-obligations)

<a id="original-implementation-stop-handoff"></a>

- [Original implementation-stop handoff](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-original-implementation-stop-handoff)

<a id="authorized-storage-fit-investigation--2026-09-29"></a>

- [Authorized storage-fit investigation — 2026-09-29](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-authorized-storage-fit-investigation--2026-09-29)

<a id="attribution-not-an-assumed-two-times-multiplier"></a>

- [Attribution, not an assumed two-times multiplier](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-attribution-not-an-assumed-two-times-multiplier)

<a id="isolated-layout-comparisons"></a>

- [Isolated layout comparisons](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-isolated-layout-comparisons)

<a id="quota-scale-feasibility-not-a-universal-guarantee"></a>

- [Quota-scale feasibility, not a universal guarantee](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-quota-scale-feasibility-not-a-universal-guarantee)

<a id="why-the-requested-worst-case-inequality-is-still-open"></a>

- [Why the requested worst-case inequality is still open](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-why-the-requested-worst-case-inequality-is-still-open)

<a id="reproduction-and-provenance"></a>

- [Reproduction and provenance](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-reproduction-and-provenance)

<a id="compact-proof-and-migration-continuation--2026-09-30"></a>

- [Compact proof and migration continuation — 2026-09-30](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-compact-proof-and-migration-continuation--2026-09-30)

<a id="expanded-evidence-and-an-infrastructure-exclusion"></a>

- [Expanded evidence and an infrastructure exclusion](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-expanded-evidence-and-an-infrastructure-exclusion)

<a id="capacity-proof-audit"></a>

- [Capacity proof audit](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-capacity-proof-audit)

<a id="concrete-migration-prototype-and-remaining-design-obligations"></a>

- [Concrete migration prototype and remaining design obligations](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-concrete-migration-prototype-and-remaining-design-obligations)

<a id="instruction-memory-and-maintenance-gates"></a>

- [Instruction, memory and maintenance gates](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-instruction-memory-and-maintenance-gates)

<a id="completion-report-and-resumption-boundary"></a>

- [Completion report and resumption boundary](history/persistence/PersistenceFoundation2A-Attempts.md#historical-attempts-completion-report-and-resumption-boundary)
