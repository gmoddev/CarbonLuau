# Persistence Foundation 1A — durability qualification blocker

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
