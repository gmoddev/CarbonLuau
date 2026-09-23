# Persistence-1A — cross-platform durability investigation

Date: 2026-09-23. Baseline: `271bff7df3e13bf287cafecaa2e2feff9c80689f`.

**Subsequent adoption:** the user approved the [D21 amendment](PersistenceD21Amendment-Proposed.md)
on 2026-09-23. Canonical D21 and PersistenceFoundation1.md now select PERSIST/EXTRA.
The report below preserves the findings and pending-approval status as recorded
in research commit `a163bec302f13cc4c091c2cb7b80cd882a26ed5f`; its statements that
DELETE remains canonical or approval is pending describe that earlier point only.
Adoption adds no production implementation or platform qualification evidence.

**DURABILITY PATH RESOLVED — READY TO RESUME PERSISTENCE-1A**, subject to approval
of the [separate proposed D21 amendment](PersistenceD21Amendment-Proposed.md).
This is a research verdict, not production implementation/qualification PASS or
permission to bypass that approval. D21 still selects DELETE until amended.

Recommend **SQLite 3.53.4, PERSIST journal, synchronous=EXTRA**, using stock
`win32`/`unix` VFS implementations. Keep the one-process private backend, one-key
transaction plus accounting, bounded async transport, no uncertain-write replay
and corruption-preserving policy. No custom VFS, external commit barrier, WAL
maintenance subsystem or replacement database is needed for this path.

The [original Windows negative evidence](PersistenceFoundation1A.md) remains
valid: accepting EXTRA did not prove post-delete synchronization. Neither the
original nor this investigation observed committed data loss on Windows.

## 1–4. Exact source and platform findings

Same verified amalgamation and hashes as the original investigation, source ID
`2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`.
No upstream source was patched. Research builds statically link SQLite with
`SQLITE_THREADSAFE=0`, `SQLITE_OMIT_LOAD_EXTENSION`; neither `SQLITE_NO_SYNC` nor
`SQLITE_DISABLE_DIRSYNC` is enabled. The probe explicitly checks those unsafe
compile options. mmap is disabled. These are research builds, not shipped workers.

| Pinned sqlite3.c location | Finding |
|---|---|
| `sqlite3PagerSetFlags`, around 63306 | EXTRA sets `extraSync`; FULL and EXTRA both set `fullSync`. PRAGMA level is not the same thing as the VFS xSync flag named FULL. |
| `pager_end_transaction`, 61721–61755 | TRUNCATE truncates to zero then xSyncs at FULL/EXTRA; PERSIST calls `zeroJournalHdr`; DELETE closes then xDeletes with `extraSync`. |
| `zeroJournalHdr`, 61034–61076 | With nonzero/unlimited journal retention, writes 28 zero header bytes then xSyncs the journal using DATAONLY plus syncFlags. This happens after database synchronization and before successful transaction completion. |
| `winTruncate`, 51788 onward | Truncates through the Windows file API; truncation itself is not the final durability barrier. |
| `winSync`, 51877 onward | Calls FlushFileBuffers on the actual open file handle; returns an I/O error on failure. The DATAONLY hint does not suppress this Windows flush. |
| `winDelete`, 54377 onward | Ignores `syncDir`; calls DeleteFileW. No post-delete flush is added for EXTRA. This is the established proof gap, not a reproduced loss. |
| `full_fsync` / `unixSync`, 43975 / 44108 onward | Linux uses fdatasync (or its build-selected fsync equivalent), propagating file-sync failure. Journal creation can also trigger directory synchronization. |
| `unixDelete`, 47030 onward | EXTRA requests directory sync after unlink. Failure opening the directory is tolerated; the VFS is not a universal proof for arbitrary mounts/permissions. |
| `walFrames`, around 71700 | FULL/EXTRA sync the WAL commit frame sequence; NORMAL omits the per-commit barrier. Database synchronization belongs to checkpointing. |

Important qualification limit: `unixSync` attempts initial journal/WAL directory
sync but does not propagate every directory-sync failure. PERSIST removes the
**per-commit deletion dependency**, not all filesystem/bootstrap assumptions.
Initial directory/database/journal creation and recovery must be qualified before
Ready in 1A; do not reinterpret successful PRAGMAs as proof of those operations.

Primary sources: the [pinned amalgamation](https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip),
[SQLite file format](https://sqlite.org/fileformat.html),
[atomic commit](https://sqlite.org/atomiccommit.html) and
[synchronous settings](https://sqlite.org/pragma.html#pragma_synchronous).
The atomic-commit page's old statement that TRUNCATE is not followed by a sync
does not describe the inspected FULL/EXTRA implementation: the pinned source
explicitly syncs the truncated journal, and both platform traces confirm it.

## 5–10. Journal/synchronization comparison

Common rollback ordering, observed on both platforms:

```text
write original pages to journal -> sync journal/header
-> write database pages -> sync database -> finalize and sync commit marker
```

| Mode | FULL | EXTRA | Marker and success boundary | Verdict |
|---|---|---|---|---|
| DELETE | Journal + DB file sync, then journal deletion | Also requests post-delete directory sync; honored by inspected Unix path, not win32 | Deletion commits; process recovery works, but common post-delete durable namespace proof is missing | Preserve negative evidence; do not select |
| TRUNCATE | Journal + DB sync, truncate journal to zero, sync journal | Same normal transaction sequence | Durably synchronized zero length makes old journal non-hot | Viable; not chosen because commit mutates file length/allocation metadata |
| PERSIST | Journal + DB sync, zero journal header, sync journal | Same normal transaction sequence | Durably synchronized invalid header makes retained journal non-hot | **Recommended with EXTRA**; least namespace/length change at commit |
| WAL | Append commit frames and sync WAL before success | Same per-commit sync behavior | Valid synced WAL commit survives before checkpoint; database can lag | Viable but adds checkpoint/storage/recovery work not needed for one serial connection |

For PERSIST, success is after the **last journal-header sync**, not merely after
database sync. File identity remains unchanged across ordinary transactions;
do not delete the persistent journal at shutdown. Setting `journal_size_limit=0`
would switch finalization to truncation: the proposed path instead retains it
with `journal_size_limit=-1` and proves a separate physical bound. No automatic
post-commit trim is needed. Every connection must set/verify PERSIST; unlike WAL,
that mode choice is not persistently remembered on reopening the database.

FULL and EXTRA are equivalent in these pinned ordinary PERSIST/TRUNCATE/WAL
paths. Keep EXTRA to minimize the policy delta and retain its behavior if SQLite
needs a deletion path during recovery; it is not advertised as an extra PERSIST
barrier. NORMAL is rejected for the proposed success contract. The WAL NORMAL
control performed no commit xSync; its quicker timings do not justify adoption.
MEMORY/OFF remove required recovery/durability mechanisms and offer no useful
alternative. Exclusive locking is not a new journal mode or a durability fix.

### WAL-specific assessment

The [WAL design](https://sqlite.org/wal.html) permits durable FULL commits before
checkpoint. The observed sequence is WAL frame writes -> WAL sync -> success,
without database xSync during that commit. Checkpoint subsequently syncs WAL,
writes/syncs database and can reset/truncate WAL. NORMAL moves the relevant
barrier to checkpoint and can lose recent transactions after hard reboot.

The `-wal` file is durable database state; `-shm` is a rebuildable index/coordination
file, not an additional author database. Neither is a packaged binary, but both
affect directory allowlists, ownership, recovery, backup and size budgets. Closing
the last connection normally checkpoints and removes them; an unclean exit can
leave them for recovery. Never delete an extant WAL manually to reclaim space or
copy only the database as a live backup. SHM locks and reader snapshots assume
same-host cooperation; CarbonLuau still needs one exclusive worker owner.

Automatic checkpoint thresholds and `journal_size_limit` are **not hard active
WAL caps**. Readers, failed checkpoints or a large transaction can prevent reset.
A bounded WAL implementation would need transaction-growth headroom, an explicit
checkpoint/admission policy, failure handling and bounded shutdown. In the small
probe with autocheckpoint deliberately disabled, WAL reached 5,912,232 bytes
while the database was 4,096 bytes; explicit TRUNCATE checkpoint reduced WAL to
zero. This demonstrates the maintenance obligation, not an unbounded production
configuration. WAL was faster here, but is not simpler to qualify than PERSIST.

## 11–13. Filesystem, OS and container envelope

Proposed initial Windows target: **local NTFS**, ordinary stock win32 VFS, working
file flushes and stable host-owned storage location. Microsoft documents
[FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers)
and [cached metadata](https://learn.microsoft.com/en-us/windows/win32/fileio/file-caching).
PERSIST uses SQLite's existing file handle to synchronize an in-file marker;
it does not pretend DeleteFileW is POSIX unlink+directory-fsync.
[DeleteFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-deletefilew)
describes deletion disposition/last-handle closure, not the missing barrier.

Windows directory handles are not assumed to be portable POSIX sync handles.
No directory-flush workaround or undocumented NT native call was used. Volume
FlushFileBuffers requires administrative access and expands the affected scope;
reject it here. [CreateFile write-through flags](https://learn.microsoft.com/windows/win32/api/fileapi/nf-fileapi-createfilea)
do not retroactively change the inspected SQLite handles. API support on ReFS
is not CarbonLuau qualification: **ReFS remains unqualified**.

Proposed initial Linux target: **local ext4**, working fdatasync/fsync and directory
creation synchronization, stock unix VFS, correct locks and supported mount/storage
settings. XFS is a reasonable later target but was not tested and is not promoted
by this report. Unsupported: SMB/NFS, FUSE, cloud-sync folders, network block/file
stacks without separate qualification, custom SQLite VFS, disabled flush/barrier
behavior, external editing/deletion, faulty media/controllers or hostile host code.

Actual Linux follow-up used an inspected **ext4 Docker named volume**, not the
Windows-backed bind mount from the initial probe. Docker's
[volume](https://docs.docker.com/engine/storage/volumes/) and
[storage driver](https://docs.docker.com/engine/storage/drivers/) documentation
distinguishes volumes from writable container layers. A named volume is not
inherently durable: its driver/backing filesystem and flush propagation matter.
Ordinary bind mounts inherit their backing stack; overlayfs adds another layer
that this result does not qualify. Docker Desktop's WSL2/ext4 virtual-disk stack
is the tested process-crash environment, not proof for native Linux hardware,
all Docker drivers, VM hard reset or physical power failure. No generic Docker
durability support claim is made.

## 14–16. Rejected extra layers and replacement backends

- **Custom VFS:** unnecessary. The transparent research VFS only delegates/traces
  stock methods and injects failures; it is not shipped or proposed for production.
  Reimplementing Windows directory/volume durability would be a major maintenance
  obligation, not a small missing call.
- **External post-commit flush:** unnecessary for PERSIST. Reopening guessed
  database/journal paths after SQLite has released locks creates handle/lifetime
  and ordering questions and cannot flush a journal already deleted by DELETE.
  `sqlite3_db_cacheflush` is not a transaction-commit barrier, and
  [SQLITE_FCNTL_SYNC](https://sqlite.org/c3ref/c_fcntl_begin_atomic_write.html)
  is an internal VFS notification, not an application command to commit safely.
  Do not reach into SQLite private structs or use volume-wide flushes.
- **Atomic files/log+snapshot:** not simpler. It requires record + accounting
  atomicity, torn-log recovery, bounded replay/compaction, snapshot generation
  selection and directory/rename durability on both OSes. Microsoft's
  [ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)
  documents its WRITE_THROUGH flag as unsupported;
  [MoveFileExW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw)
  does not by itself solve a complete transactional store. Replacing SQLite
  reproduces the harder mechanisms we already have. No alternative database
  evaluation is necessary now that built-in rollback modes provide a path.

## 17–21. Strongest common success statement and assumptions

Proposed common contract: **after verified successful Set/Remove completion, the
single transaction containing the key and accounting has completed SQLite's
ordered rollback-journal/database/commit-marker synchronization. A subsequent
ordinary worker or server-process crash does not roll it back on reopening the
same intact storage.** The host must receive/validate the successful result before
reporting success; an unreceived result remains uncertain and is never replayed.

| Failure | Honest claim |
|---|---|
| Process/server termination | Supported by SQLite recovery semantics and the killed-process fixtures below. Actual Carbon supervisor/callback integration remains a 1A/1B/1C gate. |
| OS crash / hard reboot | Intended durability is conditional on the full qualified filesystem/VFS/virtualization/device stack honoring synchronization and namespace recovery assumptions. Not directly tested here. |
| Sudden power loss | Same conditional design basis, **no actual power-cut qualification**. No universal device/power-loss guarantee. |
| Malicious/catastrophic storage failure | Outside envelope; a checksum detects some corruption, not an authenticity, repair or backup guarantee. |

Remaining premises: genuine pinned source/build; verified settings on every
connection; bootstrap file/directory existence durability; no competing writer or
external mutation; correct atomicity/locking, write ordering and flush behavior;
all required files retained for SQLite recovery; functioning devices/virtualization;
bounded/fail-closed handling of errors and corruption. A disk/controller that lies
about flush completion is not repaired by another PRAGMA.

Physical power-cut infrastructure is not required to select this evidence-backed
implementation path or demonstrate process-crash recovery. It **is** required for
a claim of tested power-loss survival of a particular deployment. No host reboot,
power cut or unrelated workload termination was attempted.

## 22–25. Recommended configuration, implementation delta and qualification

Proposed only: stock pinned SQLite **PERSIST + EXTRA**, `journal_size_limit=-1`,
normal locking plus existing exclusive worker ownership, no mmap, no automatic
VACUUM/WAL/checkpoints. Retain the existing D21 backend and public semantics.
Implement and verify `cache_spill=OFF` for the single-key transaction workload,
then prove the resulting memory/journal bounds rather than relying on a cache
target as a hard limit. This additional setting was **not** part of the comparison
benchmark; its bound/fault qualification belongs to 1A.

Required next-task changes after approval:

1. Pin/package the helper and configure/verify PERSIST/EXTRA on every connection
   before serving work; preserve the existing process/queue/publication design.
2. Treat `store.sqlite3-journal` as an expected retained SQLite file, including on
   clean close/restart. No manual deletion, zeroing, compaction or migration.
   Leave hot-journal recovery to SQLite; preserve corruption and unsupported files.
3. Return success only after checked COMMIT and response validation. Sync failure,
   worker death or missing acknowledgement does not prove rollback. No replay.
4. Qualify first creation/readiness and recovery, plus database + metadata + quota
   mutations against the **real** 1A schema; the probe's counter is not that schema.
5. Prove physical active/retained-journal bounds, process memory, startup checks,
   shutdown/parent death, stale completion fencing and all original 1A gates.

### What was executed

[ModeProbe.cpp](../tests/persistence/ModeProbe.cpp) adds no production code. For
each of four modes, FULL/EXTRA and Set/Remove, it launches separate processes and
terminates them through TerminateProcess (Windows) or SIGKILL (Linux) at selected
points, bypassing SQLite close/destructors. Exact OS self-termination is real
process-crash testing, **not an externally killed Rust server or a power cut**.

| Point | Probe result / assertion |
|---|---|
| Before transaction; after BEGIN/key/accounting; before COMMIT | Old state recovered, key/accounting agree |
| During COMMIT at the first recorded write/sync | Complete old or new state; never torn key/accounting |
| After DB sync, before rollback marker | Old state recovered |
| After PERSIST zero-header / TRUNCATE zero-length, before marker sync | Complete old/new; no power-loss inference from warm OS cache |
| After marker sync / WAL commit-frame sync | New state recovered |
| Immediately after COMMIT; before conceptual acknowledgement; after conceptual result boundary | New state recovered without replay |
| Inject failure of final PERSIST/TRUNCATE journal xSync | COMMIT does not report success; reopen yields internally consistent old/new state |

Per platform: **184 cases passed** (176 OS-terminated children + 8 injected final
sync failures), including 52 PERSIST cases. Windows + Linux: **368 cases**.
The Linux ASan/UBSan build reran the same 184 cases with leak detection and
halt-on-error enabled and passed. The initial sanitizer run found over-alignment
in the research wrapper (SQLite guarantees eight-byte allocation alignment).
The wrapper was corrected; Windows, Linux and sanitizer matrices were rerun on
the final source. This was a probe bug, not a SQLite durability finding.

All reopens check SQLite integrity plus equality of a toy byte-accounting row
and stored value lengths. The fixture does not implement namespace quotas,
production IPC/callback acknowledgement, a server supervisor, or the 1A codec.
Its named acknowledgement points model ordering only; no delivery claim follows.

Still required in 1A/1C: external worker and actual Carbon process termination;
parent-death containment; real acknowledgement lost after commit; startup timeout;
disk full/short write/failed sync at **every** relevant file stage; first creation
and recovery directory failures; corruption/schema/envelope preservation;
maximum-size quotas/overwrite/remove; retained-journal recovery; lifecycle fencing
and no replay. For WAL, if reconsidered, add checkpoint-crash, reader-pinned WAL,
SHM reconstruction, WAL-header reset/reuse and bounded-growth failures. Do not
inject a simulated xSync failure and call it a physical device/power fault.

### Physical budget

PERSIST reuses the journal from offset zero; retention is its high-water file size,
not the sum of all writes over time. The small benchmark retained 16,928 journal
bytes and a 143,360-byte database; this is not a ceiling test. TRUNCATE retained an
empty journal. WAL accumulated roughly 5.64 MiB before explicit checkpoint.

Keep D21's 512 MiB database / 1,280 MiB directory / 8 MiB diagnostics ceilings.
A prospective single-header rollback bound with spill disabled is
`131072 * (4096 + 8) + <=64KiB header/padding`, about 513.063 MiB, leaving room
within the directory budget. **This is a proposed proof obligation, not a measured
maximum or an accepted new numeric limit.** 1A must validate sector/header behavior,
page journaling, recovery, file-allocation rounding, existing-file sizes, SQLite
temporary files and bounded transaction memory under the actual fixed schema.
No broad SQL, savepoints, ATTACH, multikey transactions or arbitrary maintenance.
If this bound fails, stop rather than treating journal_size_limit or logical quota
as an active-file cap. Do not introduce unbounded VACUUM/compaction to hide growth.

## 26. Performance observations

Final corrected Release probe; 32 samples per operation/mode, one connection,
fresh small database. Values are raw 1 KiB/64 KiB BLOBs, not the unimplemented
envelope; Remove deletes a reseeded 64 KiB value. Preparation for Remove is outside
its timing. Toy accounting is in the same transaction. Medians in milliseconds:

| Platform / mode (EXTRA) | Set 1 KiB | Set 64 KiB | Remove 64 KiB |
|---|---:|---:|---:|
| Windows DELETE | 1.253 | 1.444 | 1.317 |
| Windows TRUNCATE | 1.034 | 1.139 | 1.046 |
| Windows PERSIST | 0.931 | 1.067 | 0.985 |
| Windows WAL | 0.288 | 0.504 | 0.319 |
| Linux DELETE | 3.597 | 3.813 | 3.619 |
| Linux TRUNCATE | 3.405 | 3.547 | 3.444 |
| Linux PERSIST | 2.934 | 3.128 | 2.902 |
| Linux WAL | 1.115 | 1.254 | 1.094 |

PERSIST Set64KiB p95: Windows 1.260 ms; Linux 3.858 ms. FULL and EXTRA produced
the same PERSIST commit sequence; timing differences are not new semantics.
WAL NORMAL's faster numbers are a deliberately weaker comparison, not a choice.
These are hardware/cache/virtualization/workload-specific observations, not API
latency, callback latency, near-quota throughput or a durability-based speed promise.

## 27–30. Packaging, amendment, verdict and repository handling

PERSIST requires no daemon/service/system SQLite/privileges/runtime download.
The existing proposed statically linked private helper packaging remains suitable.
Keep the SQLite public-domain provenance, verified source hashes and per-platform
binary identity. Journal files are runtime data, never release artifacts. No new
public API, native ABI, package/API/provider/schema identity or Luau pin is assigned.

D21 must change **only after approval** because it explicitly names DELETE. The
[exact proposed replacement and dependent edits](PersistenceD21Amendment-Proposed.md)
are separate and marked NOT ADOPTED. The recommendation replaces the commit-marker
mechanism rather than weakening success to enqueue or unflushed process memory.
It also makes process-crash evidence and conditional OS/power-loss assumptions
explicit. Current production and the canonical DELETE choice remain untouched.

Research verdict: **DURABILITY PATH RESOLVED — READY TO RESUME PERSISTENCE-1A
after amendment approval**. No backend redesign required. Do not begin 1B.

Research/probe/history changes may be committed under this task's explicit
repository instruction; a research commit is not canonical adoption or 1A PASS.
No push/release/deployment is performed by this investigation. Prior uncommitted
negative evidence is preserved in that same research history.

### Reproduction and retained resources

Use the [standalone CMake project](../tests/persistence/CMakeLists.txt) with the
verified amalgamation directory from the original evidence. Build with two jobs,
then run `ctest --test-dir <build> -C Release -V` on Windows. On Linux execute the
two probes with the working directory on the filesystem being investigated, not
silently on a Windows-backed build bind mount. Optional Linux-only
`-DPERSISTENCE_PROBE_SANITIZE=ON` instruments both SQLite and the probes.

Worker: dockerbox/HostPC, Windows 11 Pro x64 / NTFS, MSVC 19.44.35228.0.
Linux: Ubuntu 24.04.5 x64, GCC 13.3, glibc 2.39, Docker Desktop WSL2
`6.18.33.2-microsoft-standard-WSL2`, inspected ext4 named volume. Same image ID as
the original record: `sha256:29ce061210cde5c576e730154a201aca9787fe340b49a5317b448fcdf34e765f`.

Remote workspace/build roots remain
`C:\Sandbox\Codex\Workspaces\CarbonLuauPersistence1A-20260923` and
`C:\Sandbox\Codex\Builds\CarbonLuauPersistence1A-20260923`.
Builds: `windows`, `linux-gcc`, `linux-sanitize`. Windows CTest log:
`windows/Testing/Temporary/LastTest.log`. Named volume
`codex-carbonluau-persistence-durability-20260923` retains disposable databases
and `mode-verified.log` / `mode-sanitize-verified.log` for reproduction. Containers
are removed on exit; no task server/worker remains running. Unrelated workloads
were not modified. CodexLock coordinated edits; remote-worker skills kept the
native builds and tests off the controlling PC.

### Final-source checks

Local and worker probe source SHA-256 values matched before research commit
(LF bytes before Git working-tree line conversion):

- `CMakeLists.txt`: `232874a5fe3a42ee2a657f6dcd847d7e55f82b2d470f48059687e738759b05da`.
- `DurabilityProbe.cpp`: `22ba837166786075fe8cfa951829196534a4f4132cab39408af381eb86852860`.
- `ModeProbe.cpp`: `fef62af1e3bd76b42fbd1dd22322ad5c5f6817512e097d4d8bfa6ef0530d0715`.

Windows CTest passed 2/2 tests; final Linux release and ASan/UBSan mode logs
ended PASS. `tools/Test-Architecture.ps1`, `tools/Test-Api.ps1` and Git whitespace
checks passed. Hosted CI was not run: no push or production change. Final worker
audit found only the pre-existing `drycreek-bot` and `directus-db` containers;
the temporary audit container removed itself. Research fixtures/logs and the
named volume remain intentionally retained, not live services.
