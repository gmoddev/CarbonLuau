# Persistence Foundation 1A — backend and qualification

## Current closure — amended budget and qualified private 1A

On 2026-09-23 the user approved the [physical-budget amendment](history/persistence/PhysicalAllocation.md#persistencephysicald21amendment-proposed).
The 1,280 MiB figure is an **operational safety budget / qualification target**,
not a hard filesystem-allocation invariant. Logical quotas and qualified SQLite
page/file-length limits remain hard bounds. Recorded Windows/Linux adversarial
workloads observed no budget breach; this is empirical evidence, not a theorem.

**PASS — private Persistence-1A within the recorded qualification scope.** The inherited-WAL correction uses
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

Pre-CI worker SHA-256 (before the IPC/startup diagnostic corrections below):

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
`1b160ef0d9b574c327889976f455f4f9487ea7c2`. Implementation and physical-budget
adoption were committed/pushed as `80d364fcd34299512b3cb257a9eee6974385a2d9`.
Architecture/API/link/whitespace checks passed locally. The separate closure
commit is identifiable by Git history; it cannot contain its own SHA.

Hosted implementation-source validation is recorded by
[runtime run 35928271952](https://github.com/gmoddev/CarbonLuau/actions/runs/35928271952),
[tooling run 35928271825](https://github.com/gmoddev/CarbonLuau/actions/runs/35928271825)
and [documentation run 35928271826](https://github.com/gmoddev/CarbonLuau/actions/runs/35928271826).
Linux runtime, sanitizers, all-platform tooling and documentation passed. The
first Windows attempt passed all persistence native tests but failed the existing
CompilerContainment oversized-allocation WorkerFailure assertion. That compiler
code/fixture is unchanged from the starting revision and passed locally; the
initial hosted log does not identify the actual returned status. The unchanged
Windows-job retry passed all 14 native tests, then failed the new managed worker
fixture. Its optimized stack did not identify the exact assertion; test-only
line/status and filesystem diagnostics were added for that separate failure.
These failed attempts are retained, not counted as an overall pass or proof of
a diagnosed transient cause. They did not close 1A; the successful corrected-source
qualification below does.

#### Hosted Windows follow-up

Test diagnostics (`fdb7148`, `ab4a2b1`) separated two failures. The compiler
allocation-refusal fixture left `std::bad_alloc` unhandled, allowing Windows
crash reporting to participate in its exit. `96410c8` makes the expected refusal
exit explicitly and logs any unexpected status; allocation success still fails
the unchanged WorkerFailure assertion. Five consecutive Windows and five Linux
containment runs passed. The original failed logs did not record actual status,
so they do not establish crash reporting as their demonstrated sole cause.

The persistence failure was reproduced deterministically on dockerbox by enabling
a UTF-8 input encoding in the test process. Framework
[Process.Start](https://github.com/microsoft/referencesource/blob/main/System/services/monitoring/system/diagnosticts/Process.cs)
constructs redirected stdin with the console input encoding; the StreamWriter
can emit its UTF-8 preamble before BaseStream writes. Those three bytes were
misread as the private binary frame length. `c6f8944` accepts one exact UTF-8
preamble **only before the initialization frame**, with constant bounded reads
before any frame allocation. All subsequent frames retain strict length checks;
an actual-IPC late-preamble rejection test passes. Windows CI explicitly selects
the reproducing UTF-8 test-process profile. Production changes no global console
encoding, uses no reflection into Framework internals and introduces no public API.
Other unexpected encodings/prefixes fail closed rather than being auto-detected.

`89fe689` also replaces Framework
[FileStream.FlushAsync](https://github.com/microsoft/referencesource/blob/main/mscorlib/system/io/filestream.cs)
on the pipe with an off-owner-thread managed-buffer Flush under the same immutable
deadline. The former calls FlushFileBuffers, which masked the early rejection with
"pipe has been ended" in hosted logs. This is **IPC**, not SQLite durability
flushing: database/journal synchronization is unchanged. Startup/protocol failure
emits at most one fixed-stage/code stderr line, with no keys, values or paths.
The diagnostic fixture captures a bounded excerpt only after child exit.

Both platform worker/lost-ack/hang/parent-death/retirement/resource matrices pass
after these corrections. Windows UTF-8 tests and the Linux non-preamble profile
both preserve no-replay behavior and later-frame rejection. Earlier binary/bundle
hashes above describe their explicitly pre-CI snapshots, not the corrected worker.

#### Final committed-source qualification

Runtime implementation and test corrections conclude at
`c6f894472887c2b9008d845b5d1e5dcdcf30ddcf`:

- [Runtime CI 35931897673](https://github.com/gmoddev/CarbonLuau/actions/runs/35931897673):
  Windows x64, Linux x64 and sanitizer jobs all PASS, including full native tests,
  managed supervised-worker faults, broader runtime regressions, release validation,
  deterministic packaging, clean extracted worker and bundled-example execution.
- [Tooling CI 35931897751](https://github.com/gmoddev/CarbonLuau/actions/runs/35931897751):
  Windows, Linux and macOS PASS. No tooling/API additions were made.
- Final dockerbox full native reruns: Windows 14/14 in 40.42 s; Linux
  ASan/UBSan/leak 14/14 in 149.68 s. Earlier production backend/allocation probes
  remain valid: the hosted follow-up changed worker transport/diagnostics, not
  SQLite, SQL, quota, file accounting or durability behavior.

Downloaded CI release-candidate artifacts identify the exact committed source
above in provenance. SHA-256 independently read back:

| Artifact | SHA-256 |
|---|---|
| Windows complete ZIP | `aacef74ab6e04e3d2a1ba0435ce4a8c981b8434c39d5dee7a0f0a10f742bc312` |
| Linux complete ZIP | `31b9263d82859cc5642d59f492827984c24b19c54350cf95e91106299bcc55dd` |
| Windows storage worker (provenance) | `9d64de49b9e7b7d07a372d0b29ba38e04000a624054029811e689b549f5421fa` |
| Linux storage worker (provenance) | `5f5fe829921626148151dc4ebed2e3879733f3060a668a44f8f6a3cf1a68cc1b` |

SQLite source ID is `2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`;
archive and C/header pins remain in `native/cmake/SQLite.cmake`. These are CI
artifacts, not a new GitHub release/tag or Shockbyte deployment. The following
evidence-only commit changes documentation/routing; its own run is addressable
by that commit in Actions and is not invented inside this record.

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

### Closure report (current, not the historical reports below)

| Requested item | Final result |
|---|---|
| 1. Verdict | PASS for private 1A within this record's OS/VFS/process-crash scope. |
| 2. Starting commit | `1b160ef0d9b574c327889976f455f4f9487ea7c2`. |
| 3. D21 adoption/push | Durability amendment already pushed at baseline; physical-budget adoption pushed in `80d364f`. |
| 4. Implementation | `80d364fcd34299512b3cb257a9eee6974385a2d9`, with hosted qualification corrections through `c6f894472887c2b9008d845b5d1e5dcdcf30ddcf`. |
| 5. Evidence/docs | This separate closure commit; exact SHA supplied by Git history/completion handoff. |
| 6. Tested source | Committed-source runtime/tooling PASS at `c6f8944`; evidence-only follow-up does not modify runtime. |
| 7. SQLite | Private static 3.53.4, exact source ID and verified archive/C/header pins above. |
| 8. Startup | Effective PERSIST/EXTRA and all required settings read back; OMIT_WAL required; unsupported main/replayed WAL and readonly write-version cases rejected. |
| 9. Worker | One supervised process, one outstanding operation, off-owner-thread I/O, 256-MiB OS cap, parent-death/job containment. |
| 10–11. Namespace/isolation | Stable tagged Root or canonical PackageId; private authority/route validation, independent of provider/version/domain/VM. |
| 12. Schema | Records/Quotas/Totals schema 1; keyed BLOB identities and bounded complete startup validation. |
| 13–15. Codec/integrity | Bounded tagged trees; exact finite binary64 including signed zero/subnormal/extremes; CLPV 1 and identity-bound SHA-256. 9,996 finite stream cases plus boundaries passed. |
| 16–17. Atomicity/quotas | Per-key Set/Remove and quota accounting commit together; exact 16/256-MiB and count limits, +/-1, overwrite/shrink/removal tested. |
| 18. Storage bound | Hard database/journal byte extents; 1,280 MiB remains an operational budget, not a never-exceeded physical invariant; no measured breach. |
| 19–20. Queue/deadline | 8/128 limits, namespace FIFO/rotating fairness, retained rate buckets, immutable monotonic five-second deadline. |
| 21–22. Durability/faults | PERSIST/EXTRA sync trace and process-crash recovery; 49 Windows/47 Linux backend cases; short-write/flush/allocation/corruption faults. Not power-loss proof. |
| 23. Lost acknowledgement | Actual committed write + lost reply returns Indeterminate; durable value survives; no replay. |
| 24. Corruption | Controlled terminal failure with preservation, no silent recreation/repair or false absence. |
| 25. Startup/shutdown | Duplicate-owner fencing, startup interruption, bounded off-thread stop/reap, parent death, strict frames and Windows UTF-8 initialization qualified. |
| 26–28. Lifetime/publication/reentrancy | Stale VM/domain/owner completions discarded; durable state separate from callback authority; existing committed-publication guard reused; worker never enters Luau. |
| 29. Diagnostics | Bounded counters/status plus single fixed-stage/code worker failure line; no stored keys/values in diagnostics. |
| 30. Stress | Durable churn, 1,024 failed namespaces, 1,000 stale completions, 100 root reloads, measured 32-cycle resources; no unbounded maintenance/retry. |
| 31. Performance | Recorded startup/keyed/near-full/quota/reopen observations, not an SLA. |
| 32–34. Platforms/native | Windows and Linux native/managed PASS; Linux ASan/UBSan/leaks and native allocation faults PASS. |
| 35–36. Packaging/regressions | Deterministic private worker packaging, imports/provenance and clean extraction PASS; existing runtime/addon/provider/module/scheduler/recovery/GUI/Player/tooling regressions PASS. |
| 37. CI/docs | Final implementation-source CI green; failed attempts/corrections preserved; canonical routing, API and whitespace checks passed. |
| 38. Identity | Package 0.4.0, API 0.4.0-experimental, ABI 1.4, provider 1.2, addon schema 1 and Luau pin unchanged. |
| 39–40. Handoff/exclusion | Ready for separately authorized 1B as described above; no public persistence service, bindings, metadata or examples; 1B not started. |
| 41. Git state | Main implementation pushed; this evidence-only closure follows on main. Final exact SHA/worktree state reported after commit/push. |

## Earlier attempts

The original resumes, blocked gates and discarded layout research remain in
[the historical attempt record](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts).
They retain their original scope; they do not reopen the closed qualification.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="historical-approved-resume--physical-ceiling-qualification-blocked"></a>

- [Historical approved resume — physical-ceiling qualification blocked](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-historical-approved-resume--physical-ceiling-qualification-blocked)

<a id="preliminary-evidence--not-final-source-pass"></a>

- [Preliminary evidence — not final-source PASS](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-preliminary-evidence--not-final-source-pass)

<a id="private-implementation-map-and-limits"></a>

- [Private implementation map and limits](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-private-implementation-map-and-limits)

<a id="physical-bound-blocker-and-required-decision"></a>

- [Physical-bound blocker and required decision](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-physical-bound-blocker-and-required-decision)

<a id="preliminary-performance-observations"></a>

- [Preliminary performance observations](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-preliminary-performance-observations)

<a id="persistence-1b-handoff-boundary"></a>

- [Persistence-1B handoff boundary](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-persistence-1b-handoff-boundary)

<a id="final-bounded-test-and-packaging-evidence"></a>

- [Final bounded-test and packaging evidence](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-final-bounded-test-and-packaging-evidence)

<a id="current-task-completion-report"></a>

- [Current-task completion report](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-current-task-completion-report)

<a id="historical-initial-attempt-record"></a>

- [Historical initial-attempt record](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-historical-initial-attempt-record)

<a id="finding-and-stop-condition"></a>

- [Finding and stop condition](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-finding-and-stop-condition)

<a id="exact-upstream-candidate-and-provenance"></a>

- [Exact upstream candidate and provenance](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-exact-upstream-candidate-and-provenance)

<a id="reproducible-call-path-probe"></a>

- [Reproducible call-path probe](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-reproducible-call-path-probe)

<a id="environments-and-reproduction"></a>

- [Environments and reproduction](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-environments-and-reproduction)

<a id="completion-and-1b-handoff"></a>

- [Completion and 1B handoff](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-completion-and-1b-handoff)

<a id="final-source-checks"></a>

- [Final-source checks](history/persistence/PersistenceFoundation1A-Attempts.md#historical-attempts-final-source-checks)
