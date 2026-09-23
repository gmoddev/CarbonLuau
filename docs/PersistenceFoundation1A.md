# Persistence Foundation 1A — backend and qualification

## Current resume — amended budget and final qualification

On 2026-09-23 the user approved the [physical-budget amendment](PersistencePhysicalD21Amendment-Proposed.md).
The 1,280 MiB figure is an **operational safety budget / qualification target**,
not a hard filesystem-allocation invariant. Logical quotas and qualified SQLite
page/file-length limits remain hard bounds. Recorded Windows/Linux adversarial
workloads observed no budget breach; this is empirical evidence, not a theorem.

**Local production qualification PASS; committed-source CI pending.** The inherited-WAL correction uses
SQLite's supported [SQLITE_OMIT_WAL](https://www.sqlite.org/compile.html#omit_wal)
build option, required by backend startup, plus a writable-database check. SQLite
itself rejects unsupported read versions, including a header restored by journal
replay; a newer write-only header cannot yield a read-only Ready backend. Normal
hot-journal recovery remains SQLite-owned. There is no new recovery parser or VFS.
`WalAdmissionTests` covers unsupported main/replayed headers, no WAL/SHM attempts,
byte preservation without recovery, and successful torn-header recovery/reopen.

The previous reports below are historical snapshots, not the current contract or
an instruction to repeat the architecture investigation. The initial DELETE
failure and the physical-proof gap remain preserved. No public persistence API,
version change or Persistence-1B work is authorized here.

### Final production evidence — 2026-09-23

The final correction also closes two concrete review findings without changing
the architecture: definite failed requests no longer permanently retain empty
namespace buckets (known data and indeterminate mutations remain retained; rate
tokens are not refunded), and failed file-budget/shape preflight disables the
backend and supervisor admission without automatic restart. A definite preflight
budget failure returns StorageUnavailable; ordinary SQLite FULL with successful
rollback remains distinguishable and need not disable a healthy backend. A
post-commit check failure remains Indeterminate/no replay. Get also checks files
after its read transaction. These are correctness fixes, not new public outcomes.

| Final-source fixture | Windows x64 / NTFS | Linux x64 / ext4 |
|---|---|---|
| Complete native CTest suite | 14/14, 39.91 s | 14/14, 43.23 s |
| Production backend crash/I/O/allocation-fault cases | 49 | 47 |
| Codec allocation-fault positions, encode/decode | 39/166 | 31/161 |
| WAL admission + hot-journal recovery | 11 cases; no WAL opens or SHM calls | Same |
| Real supervised worker, duplicate ownership, parent death, shutdown | PASS | PASS |
| Lost committed acknowledgement and five-second hang/reap | PASS, no replay | PASS, no replay |
| Namespace failure churn and preflight terminal-unavailability | PASS | PASS |
| Full managed/native runtime, addons, GUI, Player, publication and recovery | PASS | PASS |
| Repeat packaging, imports, clean extracted worker and bundled examples | PASS | PASS |
| ASan/UBSan/leak and native allocation faults | Not claimed | Complete 14/14, 163.28 s |

All native suites used the private pinned SQLite 3.53.4 with THREADSAFE=0,
OMIT_LOAD_EXTENSION and **OMIT_WAL**. Startup requires the exact source ID and
OMIT_WAL, and rejects NO_SYNC/DISABLE_DIRSYNC. PERSIST/EXTRA, 4 KiB pages,
131072 max pages, no spill/mmap/auto-vacuum, MEMORY temp and retained journals
are still read back before Ready. The observed commit trace remains ordered
journal sync, database sync, zero-header commit marker, journal sync, success.

WAL cases cover main and replayed write/read versions 2/2, 2/1 and 1/2. Errors
were StorageCorrupt (6) for unsupported read formats and FormatUnsupported (7)
for newer write-only formats. Cold files remained byte-identical on rejection.
Hot-journal cases deliberately allow SQLite's normal replay/finalization, prove
page one was replayed, then reject the unsupported restored image. Four torn
main-header fixtures recover the canonical value and reopen successfully. These
offline malformed-header tests supplement actual process-crash cases; they are
not themselves evidence that production writes malformed headers.

The managed regression passes 1,024 distinct definitively failed namespaces,
known-data preservation, uncertain/lost-write retention, unsent/read failures,
and rate-bucket preservation until full refill. Actual IPC also proves that an
unexpected file introduced after Ready causes terminal unavailability, one worker
start, no restart, rejected subsequent submission and preserved database/files.
After four warmups, 32 real shutdown/reacquire cycles retained zero measured GC
bytes, handles/FDs and threads on both platforms in the final direct-worker run
(finite fixture tolerances remain 2 MiB/16 handles or FDs/eight threads).

### Allocation-budget and filesystem qualification

The final Windows/Linux allocation probes reran exact 128-page and 131072-page
exhaustion, failed insert rollback/accounting, six small-cap and two actual-cap
process kills, actual 16/256-MiB logical quota, overwrite/shrink/remove/reinsert
churn and full restart. The 512-MiB extent experiment uses a separately labeled
toy schema; it is not a public-quota workload. No auxiliary opens occurred in
normal operation. Final production-churn peak allocated-file observations were
270,991,360 bytes on Windows and 270,884,864 on Linux; main EOF was 270,823,424 and
journal EOF 53,864. Actual 512-MiB toy-cap peaks were 537,001,984 / 536,903,680.
No 1,280-MiB operational-budget breach was observed. These are measurements, not
a physical-allocation theorem or permission to advertise a hard physical quota.

Qualified profiles, read back without changing host configuration:

- Windows 11 Pro, NTFS 3.1/LFS 2.0, 4,096-byte clusters, 512-byte logical and
  4,096-byte physical sectors, 1,024-byte file records; aligned fixed local disk.
  Production fixture attributes were Archive, not compressed/sparse/reparse.
- Docker's dedicated task volume on ext4 `/dev/sde`, rw,relatime, 4,096-byte
  block/fragment size, 256-byte inodes. A read-only `dumpe2fs -h` device view
  reported has_journal, ext_attr, resize_inode, dir_index, filetype, extent,
  64bit, flex_bg, sparse_super, large_file, huge_file, dir_nlink, extra_isize
  and metadata_csum; **no bigalloc**. Directory/database flags were extent-only.
  The running filesystem's needs_recovery flag is expected with its journal.
  Host filesystem journal, virtual-disk backing and metadata are not included
  in the file-allocation measurement. No remount, quota or device write occurred.

The worker's NTFS/ext-family check is a coarse rejection gate, not automatic
qualification of every matching filesystem feature set. Other geometries,
compression/deduplication/snapshot profiles, network/cloud/FUSE and bigalloc
remain unqualified. Oversized EOF inputs (including sparse lengths) and observed
out-of-budget allocations fail preflight, without automatic repair. The retained
historical source analysis bounds normal no-chunk/no-mmap preallocation behavior;
it does not establish a portable physical allocation maximum.

### Final performance, artifacts and provenance

Release direct-backend observations (not SLA or script throughput):

| Operation | Windows | Linux |
|---|---:|---:|
| Fresh startup | 3.685 ms | 16.781 ms |
| 1,000 keyed reads | 596.663 ms total | 109.920 ms total |
| Near-full 65,531-byte envelope Get/Set/Remove | 1.243/2.605/3.771 ms | 0.532/1.277/4.054 ms |
| Exact quota fill/mutations | 16,276.3 ms | 20,035.0 ms |
| Full verification/reopen | 1,361.5 ms | 1,610.2 ms |

Final tested worker SHA-256:

- Windows: `5e94897569b09151c147371aa190c81bec0d6327fa136afb8dde948c5e62472e`
- Linux: `5a27eaeb8a048fab3a40247adee7d2747c63d81f8ec25407df49ad656d5ba6fd`
- Managed worker-test executable: `8cceb6d5c766d5f74b1f358ffacb71575488e361dc95b3dada68ce89bf74aec7`

Pre-commit final-source bundle SHA-256 (provenance honestly labels
`uncommitted-working-tree`; not a published release):

- Windows: `d3e44d2352d8da9c8eee47a9fb1dd552772d886d4100fab6d5544a5adf7581e9`
- Linux: `3c5b7f1a9b8add5a7c4947372a000d86919dc1e50abbd59b9397b11045ef1a4d`

Both repeated builds were byte-deterministic, passed payload/provenance checks,
no system SQLite/dynamic MSVC CRT/test artifacts, and clean extracted-worker
execution. Runtime installation tests compiled 48 bundled examples and exercised
GUI/Player behavior. No public persistence symbols were found in bootstrap,
API metadata, generated declarations, examples or public scripting docs.

Worker root remains `C:\Sandbox\Codex\Builds\CarbonLuauPersistence1A-20260923`.
Final logs: `production-{msvc,linux,asan}/closure-test.log`,
`allocation-{msvc,linux}/closure-{result,startup}.log`,
`managed-owned/closure-{windows,linux}.log`,
`managed-runtime-owned/closure-{windows,linux}.log`. Packaging logs are under
`C:\Sandbox\Codex\Artifacts\CarbonLuauPersistence1A-20260923\closure\{win,linux}\validation.log`.
The historical SQL-plan and research sanitizer evidence is retained; unchanged
fixed SQL did not require another design investigation. Final affected native
and production-backend sanitizers were rerun above.

A Windows test initially used File.ReadAllBytes against SQLite's open writable
handle and failed sharing admission. The fixture now explicitly shares read/write
while no request is active; both final platform runs passed. An initial CMake
selection ran zero newly added WAL tests before explicit reconfiguration; it is
not counted. Final commands require nonempty test selection. A Linux packaging
launcher initially queried Git from `/data`; the corrected package run passed.
These fixture/launcher corrections did not relax production assertions.

### Current completion and 1B handoff

Starting revision and already-pushed durability adoption remain
`1b160ef0d9b574c327889976f455f4f9487ea7c2`. The physical-budget amendment is now
adopted. Implementation/evidence revisions and hosted CI will be recorded after
the local qualification commit; no self-referential or invented tested SHA is
assigned here. Architecture/API/link/whitespace checks passed locally.

The implementation/limits map below still describes the final private substrate:
schema/envelope 1; exact finite binary64; stable tagged namespaces; atomic per-key
Set/Remove and quota state; 8/128 queue bounds, fair dispatch and original five-second
deadline; committed-publication guard; late lifetime rejection; no worker VM entry;
bounded diagnostics; nonblocking owner-thread teardown. Final tests supersede the
older blocked verdict, not its historical evidence. Package 0.4.0, API
0.4.0-experimental, native ABI 1.4, provider 1.2, addon schema 1 and pinned Luau
`c6b830185af962c82003f86784e2fe036357c830` are unchanged.

1B may be separately authorized only after this 1A closure: it owns public service/
store facades, Luau snapshots, callback references/materialization, reserved later
owner-thread admission, runtime-authority checks and public metadata/docs. **1B
was not started.** No new live Carbon/server, real-client, Shockbyte, OS-crash or
physical power-loss qualification is claimed. Actual Carbon persistence integration
remains in the later phase's scope; helper/Mono tests are not a substitute.

## Historical approved resume — physical-ceiling qualification blocked

Date: 2026-09-23. Current verdict: **BLOCKED at the in-flight physical-allocation
ceiling; implementation remains partial, uncommitted and not qualified for release**.
Resume baseline/D21 amendment: `1b160ef0d9b574c327889976f455f4f9487ea7c2`.
The approved amendment and preceding investigation were pushed before implementation.
The initial DELETE/EXTRA Windows blocker remains preserved below, not rewritten as
the rationale for an originally selected PERSIST backend.

The current working draft adds a private statically linked SQLite 3.53.4 worker,
bounded managed transport/supervision, stable namespace and lifetime records,
typed binary64/UTF-8 codec, transactional quota bookkeeping, and a native
committed-publication/resource-owner guard with dedicated completion-intake
reservations. It exposes **no public Luau persistence API**. Package/API/ABI/provider/
schema/Luau identities are unchanged. New release bundles include a platform-specific
`carbonluau_storage` executable, not SQLite source, a CLI, databases or test fixtures.

The build-time archive/source/header pins are owned by
[`SQLite.cmake`](../native/cmake/SQLite.cmake). Production uses the stock platform
VFS, verifies PERSIST/EXTRA and the fixed page/cache/journal settings, and preserves
files on corruption/unknown format. The fixed schema is `Records`, `Quotas`,
`Totals`; namespace/store/key are BLOBs. Format 1 is `CLPV`, little-endian version
and payload length, tagged payload, then SHA-256 over length-delimited identity
plus header/payload. This is corruption detection, not authenticity against an
operator who can rewrite the database. No raw Lua/host references reach the worker.

All filesystem work is worker-side. A nonblocking supervisor ownership guard
(Windows named mutex / Linux abstract local socket) spans plugin assembly reloads;
the worker separately holds an exclusive `owner.lock`. Startup is supervised for
30 seconds; request deadlines remain the original monotonic five seconds. A fault
allows one restart only after confirmed old-process death. Lost mutation replies
are potentially indeterminate and are never replayed. The initial worker storage
profiles are local Windows NTFS and Linux ext4; other filesystems remain unqualified.

### Preliminary evidence — not final-source PASS

Work was run on the authorized `dockerbox` Windows worker, with Linux containers
on its task-owned ext4 volume. Windows and Linux release builds passed codec,
backend, exact byte/key/namespace quotas, and 24 production-backend crash/fault
cases before subsequent validation additions. The 100,000-key fixture is seeded
offline with the production codec; production startup and boundary mutations then
validate its complete data/accounting state. Windows passed actual IPC lost-ack and
deadline kill/reap fixtures, the complete ten-test native suite, and the existing
managed addon/GUI/Player/publication/scheduler/recovery regression suite. These are
intermediate source snapshots, not evidence for untested later edits.

The first Linux ASan/UBSan run passed nine tests; `PersistenceQuota` timed out at
600 seconds. Its fixture directory was on the Windows bind mount, not the intended
ext4 qualification volume. On ext4 the same quota test passed in 77.24 seconds.
The next uncovered issue was a test assumption that both Set and Remove reached
32 SQLite allocator calls; Windows normal paths reached 11 and 4, Linux 10 and 3. The
fixture now enumerates every reachable position until the first unhit position,
reports that exhaustion separately, and does not count it as an injected fault.
The corrected 12-test ASan/UBSan/leak run passed in 154.15 seconds. Subsequent
startup/corruption/physical fixtures expand the suite to 13; final-source results
remain a separate gate, not inferred from this earlier run.

Windows and Linux release builds subsequently passed all 13 native tests. The
production-backend crash fixture now exercises 49 Windows / 47 Linux cases: 30
transaction crash/I/O-failure cases, 15/13 reached SQLite allocation failures, and
four first-schema-creation kills. Incomplete initial databases remain preserved
and unavailable; a committed initial schema reopens successfully. Production
trace order includes database synchronization, retained-journal zero-header
invalidation, and journal synchronization before success. The historical 184-case
research matrix covered four modes; it was **not** 184 PERSIST/EXTRA cases.

The codec separately passed all reached allocation positions (Windows: 39 encode,
166 decode; Linux: 31 encode, 161 decode), a 10,000-pattern binary64 sample stream
(nonfinite patterns excluded) and 10,000 checksum-valid bounded payload-fuzz
probes. The corruption fixture verifies unknown application/schema/value
versions, unexpected schema objects, checksums, byte-accounting disagreement,
wrong page size/auto-vacuum, oversized database/journal files, links, unsafe
super-journal references and an out-of-bound hot-journal original page count.
The physical fixture also probes an oversized replay page and a later journal
header claiming an oversized original database. These are synthetic offline
malformations, not production-generated crash states.

The expanded ASan/UBSan/leak run passed all 13 tests in 179.46 seconds. Windows
release passed 13/13 in 45.17 seconds; Linux release passed 13/13 in 48.01 seconds,
then reran the updated backend/performance fixtures (2/2 in 25.76 seconds).
Managed private worker tests and the full existing runtime suite passed on both
platforms, including real-native root/addon replacement, failed candidates,
provider reassignment, fatal recovery and 100 root reloads. Worker tests prove
parent-death termination, duplicate writer fencing, corrupt startup disable,
lost committed acknowledgement/no replay, deadline kill/reap, 1,000 simulated
stale-completion retirement cycles and 32 actual active shutdown/reacquire cycles.
The lifecycle queue replies are synthetic; actual IPC tests are separate. None
of these tests invokes a public Luau persistence callback.

The physical-allocation proof remains blocked, as detailed below. No green test
count supersedes that gate. Packaging/resource checks are recorded separately;
final-source CI cannot be claimed for an uncommitted implementation.
No implementation/evidence commit is assigned until the applicable gates pass.
Actual Carbon/client integration and Shockbyte are not inferred from helper tests.
No OS crash or physical power-loss test has been performed.

### Private implementation map and limits

| Owner | Responsibility |
|---|---|
| `native/src/persistence/Format.*` | Private typed value model, strict UTF-8, exact finite binary64, bounded canonical encoding and integrity validation |
| `native/src/persistence/Backend.*` | Sole SQLite connection, fixed SQL/schema, startup verification, atomic row/quota transactions and file preflight |
| `native/src/persistence/Worker.cpp` | Bounded private frames, OS containment, exclusive writer lock, stock SQLite VFS |
| `src/CarbonLuau/Persistence/StorageQueue.cs` | Owner-thread admission, stable namespace rates, FIFO/round-robin dispatch, lifetime-bound bounded completion storage |
| `StorageProcess.cs`, `StorageSupervisor.cs`, `StorageOwnership.cs` | Background launch/pipe I/O/watchdog/reaping and reload-spanning ownership; no Luau/native-library calls |
| Native `Publication.cpp` and `RuntimeDomain` | Existing publication/resource-owner predicate, separate 8/domain and 128/VM intake reservations, exact retirement routing |

Schema 1 uses a one-byte root/addon namespace discriminator and canonical package
ID, not domain/VM generation or package version. Names and keys remain exact,
case-sensitive BLOB data. Store/key limits are 64/128 UTF-8 bytes. The codec accepts
only boolean, finite number, UTF-8 string, dense nonempty array and string-keyed map;
the empty container is a map. It expands shared acyclic values, rejects cycles,
sorts map keys by unsigned UTF-8 bytes and enforces depth 16, 1,024 entries/container,
4,096 aggregate entries, 16 KiB strings and a 64 KiB complete envelope. There is no
Luau object conversion or host-object transport in 1A.

Set/Remove update Records, Quotas and Totals in one IMMEDIATE transaction. Startup
checks every bounded record, envelope and counter, with no automatic repair or
format migration. Byte limits are exactly 16 MiB/namespace and 256 MiB/global;
counts are 64 nonempty stores/namespace, 10,000 keys/namespace, 100,000 keys/global
and 256 nonempty namespaces. Row charge is repeated store-name + key + envelope
bytes. Removed last rows release the corresponding store/namespace counts only
at commit. PERSIST journal allocation is not charged as user data.

Managed admission retains at most 8 requests/namespace and 128 globally, including
active and completed-undelivered requests. Each reserves a maximum 68 KiB request
frame plus 64 KiB result (16.5 MiB combined worst-case buffers, within the 18 MiB
transport ceiling). Namespace/global token buckets enforce 20/200 requests per
second with 32/256 bursts, and 5/50 mutations per second with 8/64 bursts. Replacement
does not reset the stable namespace's tokens. Intake is at most eight completions
per owner tick. One outstanding worker operation fences old-generation writes
before successor work. There is no write replay or exactly-once guarantee.

Requests bind host, VM, domain, nonce, routing ID and the original monotonic
five-second deadline. Completion bytes are accepted only for that exact tuple.
Retirement discards callback authority independently of a possibly committed
write. The native guard requires existing `CanMutateHost` authorization and exact
resource-owner equality; provisional/cold-module calls reserve zero dispatches.
These private seams do not themselves expose the 1B callback API.

The worker is statically linked to the pinned SQLite, has a 256 MiB OS process
limit, no SQLite extensions/CLI/system-library dependency, and bounded framed I/O.
Windows uses a kill-on-close Job Object; Linux uses parent-death SIGKILL and
RLIMIT_AS. Unload does not join or wait on the owner thread. A detached supervisor
may retain ownership until an OS-stuck child is confirmed dead; it holds no VM or
native-library callback. A new worker is never started on unconfirmed death.
The one restart allowance does not rearm automatically. Corruption/unknown format
disables without a restart or silent reset. Ordinary diagnostics contain bounded
counters/error codes, never stored keys/values; no diagnostic files are created.

### Physical-bound blocker and required decision

**2026-09-23 follow-up:** the [physical-allocation investigation](PersistencePhysicalAllocationInvestigation.md)
adds exact page-cap/FULL/crash, maximum-quota/churn, SQL-plan and inherited-WAL
startup evidence. No cap breach was observed. Its verdict requires the
[separate D21 proposal](PersistencePhysicalD21Amendment-Proposed.md), which is
**not adopted**, plus scoped startup/profile qualification. The implementation
and prior results below remain preserved; 1A is still blocked and uncommitted.

At 4 KiB pages the database length cap is 536,870,912 bytes. With no spill,
auto-vacuum, savepoint or ATTACH path, one rollback record per original page plus
one sector-sized header bounds the journal length at 537,985,024 bytes. The pinned
SQLite evidence is `sqlite3.c` lines 65567–65570 (restart at zero), 65770–65775
(`pInJournal`), 65711–65714 (page + 8 bytes), 64317–64345 (no-spill path), 66293
(commit without another header), and 61034–61064/61736–61740 (PERSIST invalidation).
Recovery uses the first header's bounded original size (62586–62594), skipping
record pages above it (61987–61989). Startup now also verifies auto-vacuum is OFF;
schema equality alone did not prove that inherited setting.

These are **logical-length** bounds. Before/after allocated-byte checks and measured
churn are not, by themselves, proof of an in-flight physical-allocation ceiling.
The review did not establish the allocation overhead/headroom for even the
inspected filesystem profiles strongly enough to prove the never-exceeded
1,280 MiB directory ceiling. Specifically:

- NTFS 4 KiB clusters were observed, but the evidence does not establish the
  maximum allocated stream space during open-file growth within the reserved
  headroom. Allocation size and end-of-file length are distinct quantities.
- Linux ext4 4 KiB blocks were observed, but block size alone does not prove
  bigalloc is absent. A bound on the packed size of extent records also does not
  bound all allocated extent-tree metadata or reported delayed-allocation blocks
  across every accepted existing-file layout and recovery state.
- The current allocation preflight/postflight rejects or detects excess; it is
  not an in-flight enforcement boundary. All measured fixtures stayed below the
  ceiling. No physical-cap breach or general SQLite unsafety was demonstrated.

D21/Persistence Foundation 1 section 6 requires that physical ceiling to be proven
or the task stop for a canonical amendment. Therefore this task stopped without
committing/pushing the implementation or marking 1A PASS. The 256 MiB logical
quota cannot substitute for physical allocation proof. The approved PERSIST/EXTRA
durability contract, stock-VFS rule, historical DELETE negative evidence and all
Phase 0–3/later completed-foundation invariants remain unchanged.

Next work requires explicit direction: investigate a supported enforceable cap
mechanism/stronger filesystem evidence, or separately approve an accurately
conditional filesystem-qualified allocation contract. No custom production VFS,
filesystem quota, volume configuration, preallocation policy or weaker cap was
silently introduced. Persistence-1B remains closed.

### Preliminary performance observations

Windows release direct-backend fixture (not script throughput or a latency SLA):
fresh startup 5.118 ms; 1,000 repeated keyed reads 384.305 ms total; at the exact
global quota, a 65,531-byte-envelope Get/Set/Remove took 1.051/2.956/4.387 ms.
The 4,096-write exact-byte-quota fixture plus boundary mutations took 18,106.8 ms;
reopen/full verification took 1,498.2 ms. Timing is observational and includes the
qualified PERSIST/EXTRA flushes; no durability setting was weakened for speed.
Linux release observations were 17.328 ms fresh startup, 67.351 ms for 1,000 keyed
reads, 0.571/1.207/5.117 ms near-full Get/Set/Remove, 22,025.4 ms quota fill/mutations
and 1,626.1 ms full reopen. ASan is tested separately, not used as a release
performance claim. The physical fixture performs 768 Set and 576 Remove operations;
it observed 41,029 writes with peak allocated bytes 12,779,520 (Windows) and
12,754,944 (Linux ASan), not a near-ceiling physical allocation experiment.

### Persistence-1B handoff boundary

Only private substrate and reservation/authority seams exist here. 1B still owns
the public facades, raw Luau value conversion, callback references/materialization,
later owner-thread callback admission and public metadata/docs. It must pair native
reservations with managed request acceptance, release them on every rejection or
retirement, and revalidate exact host/VM/domain identity before delivery. An ordinary
task flood must not consume those separate reserved slots. Do not expose a service
merely because the helper can store values.

### Final bounded-test and packaging evidence

The final private managed test executable SHA-256 is
`a4aec3fea6bd6c01a8105b7429d575328846664982ed8d68f17f977e1e2d47da`.
It additionally rejects unknown CLPR startup error codes, duplicate completion,
wrong routing identities and impossible success flags. The final targeted parser
and full private-worker matrices passed Windows/.NET Framework and Linux/Mono.
The full existing runtime suites passed before the last narrow CLPR-parser change;
the subsequent worker matrix, not an invented full-runtime rerun, covers that change.

After four warmup cycles, 32 real Set/Remove/in-flight shutdown cycles measured:

| Process-wide retained-resource delta | Windows | Linux |
|---|---:|---:|
| GC-retained bytes | 0 | +1,104 |
| Handles / file descriptors | 0 | 0 |
| Threads | 0 | +1 |

The fixture allows 2 MiB retained bytes, 16 handles/FDs and eight threads of
process-wide runtime noise; it separately requires zero queue reservations,
supervisor completion and reusable writer ownership after every cycle. This is
finite churn evidence, not unlimited-duration leak freedom or a whole-process RSS
bound. The extracted-bundle rerun also passed (Windows +856 bytes/+5 handles/+1
thread; Linux zero for all three).

Preliminary uncommitted-source release ZIP SHA-256 values:

- Windows: `5aadc6baff0cb8642fc82626d8d917d9460384e09d57abee84dc1a1520049316`
- Linux: `0012acfe23b78ab603a812a1710883a42af57dbd8db1981bb0e6bc143fb882ea`

Both passed repeated-run byte determinism, safe-entry/exclusion checks, clean
extraction, payload/provenance hashes, finalized managed transport inclusion,
Windows static-CRT/Linux dynamic-import audits and actual extracted-worker tests.
They are **not release-qualified** and their provenance explicitly says
`uncommitted-working-tree`; later source/document edits change bundle hashes.
They contain no test databases, probe binaries, SQLite CLI or SQLite source.

Worker evidence is retained under `C:\Sandbox\Codex`, principally:

- workspace `Workspaces\CarbonLuauPersistence1A-20260923`;
- builds `Builds\CarbonLuauPersistence1A-20260923\production-msvc`,
  `production-linux`, `production-asan`, `managed-owned` and `managed-runtime-owned`;
- logs `Logs\CarbonLuauPersistence1A-20260923-native-{win,linux,asan}-final.log`
  and `Logs\CarbonLuauPersistence1A-20260923-managed-runtime-win.txt`;
- packaging `Artifacts\CarbonLuauPersistence1A-20260923\packaging-clpr-final\{win,linux}\validation.log`.

The Linux database fixtures use the task-owned ext4 Docker volume
`codex-carbonluau-persistence-durability-20260923`, not the Windows source/build
bind mount. Builds used bounded two-job concurrency and reused incremental caches.
Qualification used Windows 11 Pro/NTFS, MSVC 19.44.35228/SDK 10.0.28000 and Ubuntu
24.04.5/GCC 13.3/glibc 2.39 under the dockerbox WSL2 kernel 6.18.33.2. No actual
Carbon server/client, Shockbyte, OS-crash or physical power-loss qualification is
claimed from these private-worker tests. Reusable caches/fixtures remain; task
containers and storage workers are stopped. Unrelated worker services were untouched.

### Current-task completion report

| # | Requested result | Recorded outcome |
|---|---|---|
| 1 | Verdict | **BLOCKED** at strict in-flight physical-allocation proof; partial implementation preserved. |
| 2 | Starting commit | `1b160ef0d9b574c327889976f455f4f9487ea7c2`. |
| 3 | D21 adoption/push | Approved amendment pushed; remote `main` independently read back at that SHA. |
| 4 | Implementation commit | None: applicable gates are not all passed. |
| 5 | Evidence/docs commit | None for this resume; current evidence is uncommitted. Historical investigation remains preserved. |
| 6 | Final tested commit | None for the implementation; tests used the recorded working-tree snapshots. No false committed-source claim. |
| 7 | SQLite | Private static 3.53.4; exact source ID/archive/C/header pins in `native/cmake/SQLite.cmake`. |
| 8 | Configuration | PERSIST, EXTRA, 4 KiB pages, no spill/mmap/auto-vacuum, fixed page ceiling verified at startup; incompatible configuration rejected. |
| 9 | Worker | One supervised process/one outstanding operation; background I/O, 256 MiB OS limit, parent-death/job containment. |
| 10 | Namespace | Stable tagged root or exact canonical package ID; independent of version/VM/domain. |
| 11 | Isolation | Exact binding/resource-owner and host/VM/domain route tests passed; no manual namespace public input. |
| 12 | Schema | Records/Quotas/Totals, schema 1, keyed BLOB identities, bounded complete startup validation. |
| 13 | Codec | Private bounded tagged boolean/number/string/array/map tree; no Luau/host object conversion. |
| 14 | Binary64 | Exact finite bits, including signed zero/subnormal/extremes; 9,996 finite cases in the 10,000-pattern stream plus explicit boundaries passed. |
| 15 | Integrity | CLPV format 1 plus identity-bound SHA-256; malformed/checksum/unknown-format failures controlled. |
| 16 | Atomicity | Production Set/Remove value and quota state commits together; crash reopen accepts only complete states. |
| 17 | Quotas | Exact byte, store, key and namespace bounds, +/-1 and overwrite/remove accounting passed. |
| 18 | Physical ceiling | Logical lengths bounded and measured allocation stayed below cap; strict in-flight allocated-byte guarantee **not proven**. |
| 19 | Queue/fairness | 8/9 namespace, 128/129 global, namespace FIFO/rotating fairness and stable rate buckets passed. |
| 20 | Deadline | Original monotonic five seconds; no extension/replay; expiry and hung-worker kill/reap passed. |
| 21 | Production durability | Stock-VFS PERSIST/EXTRA trace and process-crash recovery passed within recorded scope, not power-loss proof. |
| 22 | Faults | 49 Windows/47 Linux production-backend cases, corruption and separate codec/native allocation-fault fixtures passed. |
| 23 | Lost acknowledgement | Actual commit-before-IPC-reply child exit returned indeterminate; state survived; sent-count proved no replay. |
| 24 | Corruption | Controlled disable/preservation; no silent recreation, overwrite or false absence. |
| 25 | Startup/shutdown | Supervised startup, initial-schema interruptions, duplicate owner, bounded off-thread stop/reap and parent death tested. |
| 26 | Retirement | Real-native replacement/recovery plus bounded synthetic completion routing passed; durable state and callback authority remain separate. |
| 27 | Publication | Existing mutation predicate reused; provisional/cold/public-module laundering rejected; committed owner admitted. |
| 28 | Reentrancy | Worker contains no Luau callback/native-library delegate; owner-thread seam only, no recursive entry. |
| 29 | Diagnostics | Bounded ready/pending/completion/rejection/expiry/failure/corruption/discard counters; no keys/values. |
| 30 | Stress/resources | Durable churn, 1,000 stale completions, 100 root reloads and measured 32-cycle resource retention passed; finite scope. |
| 31 | Performance | Startup/keyed-read/near-full Get/Set/Remove/reopen observations recorded above; no SLA. |
| 32 | Windows | Native 13/13, private worker, lifecycle and affected managed regressions passed; overall 1A still blocked. |
| 33 | Linux | Native 13/13 plus final affected 2/2, private worker and managed regressions passed on ext4; overall 1A still blocked. |
| 34 | Sanitizers/native | Linux ASan/UBSan/leak 13/13; SQLite/model/native fault coverage passed. ASan backend fixtures do not claim helper OS-cap qualification under ASan. |
| 35 | Packaging | Both uncommitted-source platform bundles deterministic, audited and clean-extraction tested; not published. |
| 36 | Regressions | Existing runtime/addon/provider/module/scheduler/recovery/Player/GUI/parser and release tests passed in recorded snapshots. |
| 37 | CI/docs | Local policy/API/link/whitespace checks passed; no final-source implementation CI because no implementation commit/push. |
| 38 | Identities | Package 0.4.0, API 0.4.0-experimental, native ABI 1.4, provider 1.2, addon schema 1, Luau `c6b830185af962c82003f86784e2fe036357c830` unchanged. |
| 39 | 1B handoff | Blocked until 1A physical-cap decision/qualification closes; later facades/conversion/callback admission remain separate work. |
| 40 | Public API exclusion | No public persistence service/bindings/metadata/examples added; 1B not started. |
| 41 | Branch/worktree | `main`/remote `main` at `1b160ef`; implementation/tests/docs changes deliberately remain uncommitted. |

## Historical initial-attempt record

Date: 2026-09-23. Verdict: **BLOCKED before production implementation**.
Starting/current baseline: `271bff7df3e13bf287cafecaa2e2feff9c80689f` (`main`,
matching fetched `origin/main` at investigation start).

**Historical initial-attempt record:** the completion table and source hashes
below describe that attempt, before the separately authorized follow-up. The
[durability investigation](PersistenceDurabilityInvestigation.md) now recommends
PERSIST/EXTRA and preserves this DELETE negative evidence. Its
[D21 amendment](PersistenceD21Amendment-Proposed.md) was subsequently approved on
2026-09-23 and applied canonically. The historical research commit did not adopt
it or claim production 1A PASS; approval also does not establish that PASS.
Current CMake includes the new
mode probe; the original CMake hash below is historical, not its current hash.

[D21](Invariants.md#d21--persistence-foundation-1) and
[Persistence Foundation 1](PersistenceFoundation1.md) remain authoritative.
This is an implementation-gate investigation, not a replacement architecture,
storage implementation, production dependency pin, or Persistence-1A PASS.

## Finding and stop condition

The selected upstream SQLite Windows VFS accepts `PRAGMA synchronous=EXTRA`
and the pager requests synchronized journal deletion, but `winDelete` explicitly
ignores its `syncDir` argument. Successful `DeleteFileW` is its success boundary;
there is no subsequent directory/file flush in that delete path. The Linux
`unixDelete` path instead invokes directory synchronization after unlinking the
journal. Checking PRAGMA results alone cannot establish equivalent VFS behavior.

The architecture's [durability section](PersistenceFoundation1.md#7-durability-atomicity-and-uncertainty)
relies on EXTRA's post-delete directory synchronization, qualified on both target
platforms. That mechanism is not supplied by the inspected stock Windows VFS.
No independently established equivalent Windows filesystem guarantee was found
in this investigation. The backend/durability stop condition therefore applies.

This does **not** prove loss of a transaction on NTFS, general SQLite unsafety,
or that a supported Windows solution is impossible. It establishes a missing
durability premise, not a physical power-failure experiment. An ordinary process
exit/reopen cannot settle durability of filesystem metadata after OS/power loss.
The existing hardware-honesty caveat is not evidence for a missing software flush.

No fallback to FULL, WAL, PERSIST, TRUNCATE, volume-wide flushing or a custom VFS
has been implemented. Any different journal contract requires an explicit canonical
decision. A supported equivalent Windows DELETE durability path could instead be
investigated and qualified without assuming that a reported setting proves it.

## Exact upstream candidate and provenance

Investigated SQLite **3.53.4**, not a newly adopted production dependency:

- Official [download page](https://www.sqlite.org/download.html) and
  [amalgamation archive](https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip).
- Source ID: `2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`.
- Published archive SHA3-256, independently matched locally:
  `628a44cfe82c66aed1ccbbe85a562d2e33ebe64b3288981ed76285612227934e`.
- `sqlite3.c` SHA-256:
  `b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189`.
- `sqlite3.h` SHA-256:
  `919e7f2e8ed1d8f56ac17b412b8971c76aa5d1a879752cc6058f75e7d5910e1d`.
- SQLite is [public domain](https://www.sqlite.org/copyright.html). Only the
  verified amalgamation C/header were copied to the isolated build workspace;
  no SQLite CLI, runtime dependency, vendor source or binary was added to production.

Pinned amalgamation locations: `sqlite3.c` lines 54377–54463 (`winDelete`),
47030–47065 (`unixDelete`), 63306–63317 (pager `extraSync` selection), and
61755 (pager passes `extraSync` to VFS deletion). These are upstream archive
line references, not links to a file shipped in CarbonLuau.

Primary context: SQLite's [synchronous contract](https://www.sqlite.org/pragma.html#pragma_synchronous)
distinguishes rollback-mode durability from consistency; its
[atomic commit discussion](https://www.sqlite.org/atomiccommit.html) relies on
filesystem/flush assumptions. Microsoft's
[file caching guidance](https://learn.microsoft.com/en-us/windows/win32/fileio/file-caching)
describes cached filesystem metadata, and
[FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers)
documents explicit file/volume flushing. These sources do not, in this review,
establish that the observed successful ordinary DeleteFileW supplies the missing
post-delete durable boundary. No volume flush or host policy change was attempted.

## Reproducible call-path probe

[Standalone CMake project](../tests/persistence/CMakeLists.txt) and
[DurabilityProbe.cpp](../tests/persistence/DurabilityProbe.cpp) are research-only.
They are not included by production CMake, managed projects or packaging.

The project statically builds the supplied exact amalgamation, checking both
source hashes before configuration. It uses `SQLITE_THREADSAFE=0` and
`SQLITE_OMIT_LOAD_EXTENSION`; it does not alter SQLite synchronization code.
The probe delegates VFS deletion to the original VFS and records the pager's
sync argument. Windows traces public VFS system-call overrides for DeleteFileW
and FlushFileBuffers; Linux link wrappers trace unlink/fsync/fdatasync while
delegating the actual calls unchanged. It counts calls only during COMMIT.

Each FULL/EXTRA case creates a fresh private fixture database, verifies DELETE
and synchronous settings, commits one fixed UPDATE, then verifies the value on
ordinary close/reopen. No production paths or existing databases are opened.
Fixtures are retained under the build directory for inspection. The test has a
15-second CTest timeout and suppresses Windows error dialogs.

Observed calls for one COMMIT (SQLite status 0 means SQLITE_OK):

| Platform / mode | Reported synchronous | COMMIT | Delete | Pager requests directory sync | File syncs | Directory syncs | Syncs after delete |
|---|---:|---:|---:|---:|---:|---:|---:|
| Windows / FULL | 2 | 0 | 1 | 0 | 3 | 0 | 0 |
| Windows / EXTRA | 3 | 0 | 1 | 1 | 3 | 0 | 0 |
| Linux / FULL | 2 | 0 | 1 | 0 | 3 | 1 | 0 |
| Linux / EXTRA | 3 | 0 | 1 | 1 | 3 | 2 | 1 |

Linux's earlier directory sync concerns journal creation; it is not the
post-delete sync being investigated. The probe's `Observation PASS` means these
call-path assertions and ordinary reopen passed, **not** durable-storage PASS.

### Environments and reproduction

Both probes ran on SSH worker `dockerbox` / `HostPC`, with bounded two-job builds,
separate incremental directories and no server processes:

- Windows 11 Pro x64, fixed local NTFS C: volume, MSVC 19.44.35228.0,
  Windows SDK 10.0.28000.0, CMake 3.29.2, stock `win32` VFS.
- Linux x64 container, Ubuntu 24.04.5, GCC 13.3.0, glibc 2.39,
  WSL2 kernel `6.18.33.2-microsoft-standard-WSL2`, stock `unix` VFS.
  Image `carbonluau-foundation-f:latest`, inspected image ID
  `sha256:29ce061210cde5c576e730154a201aca9787fe340b49a5317b448fcdf34e765f`.
  Container limited to 2 CPUs / 2 GiB, network disabled, removed after exit.
  Databases were on a Docker Desktop Windows-backed bind mount: this is a
  **call-path comparison, not qualification of a dedicated Linux filesystem**.
- An initial tooling-image configuration failed because that image lacked the
  C/C++ build tools. No result is attributed to it; the successful Linux build
  used the already-installed Foundation F image.

Remote workspace: `C:\Sandbox\Codex\Workspaces\CarbonLuauPersistence1A-20260923`.
Verified upstream C/header are under `sqlite`; the two project files under `probe`.
Remote build root: `C:\Sandbox\Codex\Builds\CarbonLuauPersistence1A-20260923`.
Windows results are in `windows/Testing/Temporary/LastTest.log`; Linux results
in `linux-gcc/Testing/Temporary/LastTest.log`. Keep these incremental artifacts;
there is no persistent task-owned container, worker or server.

With a verified extracted amalgamation supplied explicitly (no runtime download):

```powershell
cmake -S tests/persistence -B build/persistence-probe-windows -G "Visual Studio 17 2022" -A x64 -DSQLITE_AMALGAMATION_DIR=C:/path/to/sqlite-amalgamation-3530400
cmake --build build/persistence-probe-windows --config Release --parallel 2
ctest --test-dir build/persistence-probe-windows -C Release -V
```

```sh
cmake -S tests/persistence -B build/persistence-probe-linux -DCMAKE_BUILD_TYPE=Release -DSQLITE_AMALGAMATION_DIR=/path/to/sqlite-amalgamation-3530400
cmake --build build/persistence-probe-linux --parallel 2
ctest --test-dir build/persistence-probe-linux -V
```

## Completion and 1B handoff

The requested implementation report is deliberately explicit about work not done.
No applicable production gate is promoted by this investigation.

| Report item | Result |
|---|---|
| 1. Verdict | BLOCKED at Windows durable-commit qualification. |
| 2. Starting commit | `271bff7df3e13bf287cafecaa2e2feff9c80689f`. |
| 3. Implementation/evidence commits | None. User requires all applicable 1A gates before commit/push; changes remain uncommitted. |
| 4. Final tested commit | Baseline above plus the uncommitted research fixture; no final implementation SHA exists. |
| 5. SQLite | 3.53.4 investigated, exact provenance above; not adopted into production. |
| 6–7. Worker architecture/lifecycle | No production worker implemented or launched. Canonical process design unchanged. |
| 8–9. Namespace implementation/isolation | Not implemented/tested; canonical private authority unchanged. |
| 10. Database schema | No production schema. Probe's single disposable table is not a proposed storage schema. |
| 11–14. Codec, numbers, envelope, logical names | Not implemented/tested. |
| 15–17. Atomicity, quota accounting/tests | Not implemented/qualified; probe COMMIT/reopen does not qualify these. |
| 18. Physical ceiling | Not implemented/qualified. |
| 19–20. Queue/fairness, five-second deadline | Not implemented/tested; probe timeout is not the request deadline. |
| 21. Durability | Required Windows premise unqualified; do not equate EXTRA readback with directory synchronization. |
| 22–23. Crash/recovery, lost acknowledgement | Not tested. No OS/power-loss or server-crash experiment performed. |
| 24. Corruption | Production behavior unimplemented/untested; no user database modified. |
| 25–28. Retirement, publication, reentrancy, diagnostics | Existing runtime untouched; no persistence seam implemented or claimed. |
| 29. Stress/resources | No storage stress, queue or leak qualification. Research builds bounded, probes exit normally. |
| 30. Performance | No representative storage performance qualification; tiny probe timing is not an API benchmark. |
| 31. Windows | Call-path/ordinary-reopen observation PASS; Persistence-1A durability BLOCKED. |
| 32. Linux | Call-path/ordinary-reopen observation PASS; production filesystem/Carbon durability NOT QUALIFIED. |
| 33. Sanitizer/native | Focused standalone native probes built and ran. Production sanitizer/allocation-fault matrix not rerun; production native code unchanged. |
| 34. Packaging | No production package/dependency changes. Fixture not wired into release outputs; no packaging PASS claimed. |
| 35. Broader regressions | Existing results preserved, not rerun for research/docs-only changes. |
| 36. CI/docs | Local documentation/architecture checks recorded below; final-source hosted CI not run because no push. |
| 37. Identities | Package 0.4.0; API 0.4.0-experimental; ABI 1.4; provider 1.2; addon schema 1; Luau `c6b830185af962c82003f86784e2fe036357c830`, all unchanged. |
| 38. Exact 1B handoff | **NOT READY**. Resolve the Windows durability gate, then implement/qualify all 1A substrate before 1B. No completion/admission/backend seam exists yet. |
| 39. Public API | No persistence service, facade, binding, callback, metadata, tooling or runtime example added. 1B not begun. |
| 40. Git state | `main`, no commit/push; only research fixture and documentation/routing edits. |

Next decision: authorize a focused investigation of a supported Windows
DELETE-equivalent durable boundary, or explicitly approve review of a different
journal/VFS contract in D21. Neither path is silently selected by this record.

## Final-source checks

Both platform probes were rebuilt and rerun after the final fixture edit. Each
CTest run passed its one observation test (Windows 0.03 seconds total, Linux
0.11 seconds total); these durations are not performance qualification.
Exact tested local source SHA-256 values (LF bytes, before Git line conversion):

- `DurabilityProbe.cpp`: `22ba837166786075fe8cfa951829196534a4f4132cab39408af381eb86852860`.
- `CMakeLists.txt`: `4138b7b516f8ece60fdaf3c8e3a9b047e25d93e6fffa1e32e6adfcfdca0fa0c0`.

`tools/Test-Architecture.ps1`, `tools/Test-Api.ps1` and `git diff --check` passed.
These are static architecture/surface/link/scope checks, not new runtime evidence.
CodexLock coordinated edits; the remote-worker skills kept native compilation
and Linux container execution on dockerbox. Unrelated containers remained running.
