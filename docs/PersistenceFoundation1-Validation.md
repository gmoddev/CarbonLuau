# Persistence Foundation 1 — architecture validation

Date: 2026-09-23. Verdict: **CANONICAL BASELINE READY (design only)**.

Subsequent durability research and the user-approved PERSIST/EXTRA amendment are
recorded in [PersistenceDurabilityInvestigation.md](PersistenceDurabilityInvestigation.md)
and the [adoption record](PersistenceD21Amendment-Proposed.md). D21 and the design
now incorporate that amendment. This initial validation remains historical
architecture evidence, not production durability qualification.

Starting CarbonLuau commit: `72efcf253f46c5d4499bd7f2b5a69ae4ceb23946`.
Fetched origin/main still matched that baseline. Entity negative evidence and D20
adoption commit: `771742634bd65b0a3183e8d876c91554b12db9f7`.
The subsequent commit adding this record is the Persistence architecture commit;
its exact SHA belongs in the completion report/Git history, not a self-referential
hash embedded in its own source. No release/tag is created.

This is source/architecture validation, not an implemented storage adapter, SQLite
benchmark, crash test or Windows/Linux Carbon result. All concrete design choices
are in [D21](Invariants.md#d21--persistence-foundation-1) and the
[30-decision index](PersistenceFoundation1.md#13-resolved-decision-index).

## Evidence inspected

| Evidence | Finding and limit |
|---|---|
| Carbon source `08bcfd854b6692e4566a859d050eb1ccd8f9dc9e`, Configuration/DynamicConfigFile.cs and DataFileSystem.cs | ReadObject can create missing storage; Save/WriteObject directly use File.WriteAllText; sync refreshes the in-memory dictionary. No durable atomic transaction claim inferred. Current upstream source, not a new exact-host binary run. |
| Same Carbon revision, SQLite/SQLite.cs | Mono.Data.Sqlite worker with Queue.Enqueue, NextTick delivery, worker Join on Shutdown; query exceptions logged. No inspected queue bound or the required explicit durable completion/error contract. Evidence against simply inheriting that wrapper, not against SQLite. |
| Oxide.Core `b5001d6b3f82da0227448fe2a80e1316e8b8f2b6`, DynamicConfigFile.cs | Same direct-write and in-memory sync distinction. Carbon's implementation remains the relevant host adaptation target. |
| Microsoft FileStream.Flush(Boolean), File.Replace docs | File APIs exist; do not by themselves prove crash-safe parent directory changes, delete transactions or quota consistency on the actual Mono host. |
| SQLite atomiccommit, pragma synchronous, transaction, limits and defensive guidance | Documented rollback transactions and EXTRA/VFS synchronization provide a defensible backend choice under stated filesystem/device assumptions; size/SQL limits can be reduced. Need separately pinned binary/platform/fault qualification. |
| `native/src/Runtime.cpp`, cl_vm_callback | Owner-thread queued work, one admitted deadline, LUA_YIELD reported as scheduled-callback error, then thread released. No supported async coroutine continuation to assume. |
| `native/src/runtime/Publication.cpp`, CanMutateHost | Existing nonprovisional admission plus no active publication is the mutation predicate; D21 reuses it, including conservative async-read restriction. |
| D4/D7/D9/D10; Foundation G | Modules do not yield; provisional resources stage; foreign calls do not switch admission/deadlines; VM retirement discards old work. Compiler worker demonstrates project containment direction but is synchronous, not a ready async storage supervisor. |
| Compatibility / ToolingBaseline / release.json | Production net48/C# 7.3, existing Core/API-catalog ownership, separate identities. No modern-.NET game-runtime assumption or tooling implementation. |

Pinned primary-source links are in [the backend section](PersistenceFoundation1.md#6-backend-selection-and-evidence);
SQLite/Microsoft documentation was read on the review date. These are research
sources, not code vendored or executed in this task. No prototype was needed to
choose the documented transaction mechanism; do not imply one was run.

## Architecture review

- Service and all signatures have explicit submission versus completion results.
  Callback-based Async is intentionally different from Roblox yielding calls.
  It avoids changing current no-yield module/callback semantics.
- Stable persistent namespace is separate from facade lifetime. Explicit same-
  admission-owner authorization prevents foreign facade use; new generations may
  reacquire their own durable data. Same package ID reinstallation is documented.
- Failed candidate/cold-module calls cannot submit I/O. Read restriction and
  task.defer examples agree with the corrected mutation/publication predicate.
- Snapshot bounds apply before acceptance; binary64 encoding avoids lossy JSON or
  SQLite numeric coercion. Tables are copied values, not retained live state.
- SQLite selection is driven by per-key + quota transaction/recovery requirements,
  not query features. Private helper avoids blocking the server or binding a modern
  database package into Mono. Bounded supervision must still be implemented.
- COMMIT is the success boundary. Dispatched write/lost reply can be indeterminate;
  retirement drops callback authority, not a committed value. Replacement fencing,
  no replay and stop-on-unreaped-worker rules close the stale writer race.
- FIFO namespace order and fair global dispatch do not make Get/Set atomic together.
  Update is explicitly deferred rather than running Luau under a storage lock.
- Resource policy distinguishes logical quota, database pages, active rollback
  journal, process memory, IPC and callback retention. Physical directory ceiling
  remains an explicit implementation gate, not inferred from logical bytes.
- Corruption/format mismatch is not missing data. Preservation, bounded decoder,
  checksum limitations and no portable hostile-code sandbox are explicit.
- Tooling metadata and 0.5.0 are future implementation/release work only. Entity
  remains independently deferred, including the no-demonstrated-pooling correction.

## Checks and deliberately unclaimed evidence

Local checks cover relative documentation targets, new heading routes, balanced
fences, D20 status, all 30 persistence decisions, API/example spelling, unchanged
production/version paths and `git diff --check`. The preserved PowerShell research
checker is syntax-checked; Python research-runner syntax is checked without starting
it. These checks detect document/scope errors, not implementation correctness.

`tools/Test-Architecture.ps1` passed its static architecture-owner checks (including
the existing GiveItem safety supplement); `tools/Test-Api.ps1` passed current API
identity, surface/example and repository-wide relative-link checks. These are
lightweight static checks, not a new runtime matrix. Initial focused review checked
308 local links across nine documents plus fence balance and all 30 numbered
decisions; final checks additionally include the documentation-site routing.

Entity history is retained: initial 54 structural/model checks; expanded 1,029
assertions including 20 selected Windows/Linux method comparisons; 13 isolated
Linux host checks; first Poolable assumption failure and corrected **no actual
same-object pooled reuse observed**. No live test rerun in this closure.

No Rust/Carbon servers, builds, SQLite prototypes, sanitizer/fault matrices or
editor tooling were started. No Entity or Persistence production API, native/Core
runtime, public API catalog or release identity changed. Existing valid runtime
results are preserved; no new implementation or crash-consistency PASS is claimed.
Current identities remain package 0.4.0, API 0.4.0-experimental, native ABI 1.4,
provider 1.2, addon schema 1 and Luau
`c6b830185af962c82003f86784e2fe036357c830`.

Hosted CI is separate from local architecture checks; commit/push is not itself
proof of green CI. Persistence runtime/platform qualification remains NOT RUN.
