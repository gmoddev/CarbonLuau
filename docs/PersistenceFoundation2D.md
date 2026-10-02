# Persistence Foundation 2D — combined qualification

Status: **final-source hosted gate pending**. This is the combined closure
record for the already implemented [2A](PersistenceFoundation2A.md),
[2B](PersistenceFoundation2B.md), and [2C](PersistenceFoundation2C.md) work.
[D21](Invariants.md#d21--persistence-foundation-1) continues to own primary
authority, durability, worker, quotas, and capacity semantics;
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query)
and [Foundation 2](PersistenceFoundation2.md) own the derived and public Query
contract. This record does not amend either decision.

Starting `origin/main`: `4f644599ec4177025601b01130e0cd05f92e78ca`.
Combined regression/harness commit: `a2668f7` (no production runtime change).
The qualified 2C implementation remains `d9d1197`; the 2A/2B implementation
and hosted baselines remain in their respective records. No 0.5.0 package,
tag, or GitHub release was created by the combined-test commit.

## Combined workload and failure boundaries

The new native `PersistenceQuery` fixture uses the production SQLite backend,
one root and one addon namespace, 240 initial records, and 180 mixed
Get/Set/Remove operations. It holds an ACTIVE and a replacement BUILDING
generation under foreground writes, periodically walks paginated Query
results, and compares every page with an independent current-state model.
An old cursor rejects after generation replacement. The public real-VM fixture
submits 20 paced writes, performs first-use Query, then Remove, Set and Get
between pages; continuation sees the current page state, not an old snapshot.
The callback chain returns with zero pending requests and waiters. A separate
real-worker Query reaches owner-thread completion admission while Luau callback
drain is withheld; root replacement then discards the old callback, without
entering the new domain or retaining its reservation.

The already qualified private 2A crash fixture is rerun as part of the final
native matrix. It injects process termination around derived preparation,
checkpoint, final publication, savepoint/release, rollback/withdrawal,
COMMIT, and cleanup, including nonfinal build progress. After restart it
compares authoritative primary state with old/new transaction outcomes and
checks that query-admitted ACTIVE state is correct or withdrawn. The 2B
managed fixture separately exercises original FIFO reservations, 8/namespace
and 32/global waiter saturation, five-second expiry, ready-state loss,
worker restart, cross-namespace progress, retirement, and 1,024 terminal
field-demand cycles without retained intent. The 2C public/native fixtures
exercise stale/forged/session/generation cursors, exact index-backed plans,
maximum result/page/cursor bounds and publication rejection. These are
distinct layers of evidence; a private crash fixture is not described as a
public Luau callback crash.

Query uses the same owner-thread callback admission and domain-lifetime
machinery as qualified Foundation 1. Retired callback authority is discarded,
not retargeted or replayed. No new worker writer, public method, native ABI,
schema, or SQL authority was introduced by 2D.

## Final local matrix

| Gate | Observation |
|---|---|
| Windows x64 native | 22/22 CTest PASS, including combined Query, derived crash/fault/budget/plan, Foundation 1 quota/corruption/physical and native teardown. |
| Linux x64 native | 23/23 CTest PASS on qualified ext4, same paths plus Linux-specific coverage. |
| Linux ASan/UBSan/leak | 23/23 CTest PASS with leak detection enabled; no sanitizer suppression added. |
| Windows and Linux managed worker | PASS: real supervised SQLite worker, D21 queue/replay/rate/lifecycle, 2B demand/waiter/retirement, 8 builds/512 records, and 1,000 stale-completion cycles. |
| Windows and Linux real compiler/VM | Focused 2C/2D public Query and Foundation 1C combined stress PASS. The latter runs 2,200 Sets, 4,400 Gets, 2,200 Removes, root plus ten addons and twelve complete host/worker reopen cycles. |
| Full runtime regressions | PASS on both platforms, including addon/provider, GUI, Player, module/publication, scheduler/recovery and persistence fixtures. |
| Physical/resource backstops | The existing production-path `PersistencePhysical`, `PersistenceDerivedBudget`, `PersistenceQuota`, `PersistenceDerivedFaults` and `PersistenceDerivedCrash` tests PASS on both platforms and under Linux sanitizers. Limits remain 1-GiB DB extent, 1,075,904,512-byte journal extent, 2,560-MiB operational allocation budget, 128-MiB SQLite heap, 256-MiB worker and one-million-VDBE backstop. The allocated-block budget is not a hard filesystem guarantee. |
| API/tooling/packaging preflight | `Test-Api`, `Test-Architecture`, `Test-ToolingContracts`, and `Test-Release` PASS on the combined-test source. Exact release-commit packaging/clean-install must be repeated after version reconciliation. |

## Live Carbon/Rust

Windows uses a fresh isolated Rust server on dockerbox under the approved
`D:\Sandbox\Codex\Workspaces\CarbonLuauPersistence2D` tree: Rust Steam build
`25653776`, current production Carbon Windows archive SHA-256
`cb39462628b80a0e430ae5130eb45755eb303ffac4f8344ca2e955de3ac623ba`.
Receipt `persistence1b-20261001-200049-c96b6e56` records actual plugin load,
root/addon Get/Set/Remove, Query hint/automatic readiness, equality, inclusive
range, descending order and continuation after a Set, plus plugin unload/reload,
native unmap, stopped worker/compiler and restored isolated installation.
The Query marker is in the live server log, generation 6. This is a server-side
fixture; no authenticated client receipt is claimed. The new Windows run did
not include an actual Rust process restart; the qualified unchanged Foundation
1C Windows restart result remains separate historical evidence.

Linux uses an isolated BigKVM ext4 server with Rust build `25653776` and
Carbon `2.0.261.0`. Receipt
`/root/codex/carbonluau-2d/artifacts/linux-live-2d-20261001-234346/receipt.json`
records actual load, root/addon persistence, Query first-use/pagination/range/
order, plugin unload/reload, fresh Rust process restart, three read-only
restart operations, and zero owned helpers after teardown. Post-stop allocated
persistence files total 118,784 bytes (observation, not a ceiling proof).
Both host-command exits were `-9` after acknowledged quit and observed
save/config markers; this is not called a normal zero exit or power-loss test.
The first Linux attempt timed out during outdated Rust/Carbon setup and is
preserved in its separate task-owned artifact directory, not counted as PASS.
No live qualification server remains running.

## Identity and limits

Combined closure targets scripting API `0.5.0-experimental`; before the
separate release step the development package remains `0.4.0`. Native ABI
`1.5`, provider protocol `1.2`, addon package schema `1`, SQLite `3.53.4`,
and the pinned Luau revision are unchanged. Query is server-local, indexed,
bounded and asynchronous. It does not supply cloud persistence, arbitrary
SQL, a primary-scan fallback, `UpdateAsync`, schemas, migrations, or an
author-managed index lifecycle. Physical allocated blocks depend on the
qualified filesystem/VFS/device envelope; no sudden-power-loss result or
macOS server/runtime claim is made. Authenticated-client GUI behavior and
Shockbyte/full-provider qualification remain separate recorded limits.

## Hosted and release gate

The combined-test commit requires exact-source hosted Windows/Linux/native,
runtime, sanitizer, tooling/API, deterministic package and clean-install PASS.
Only after that gate and final documentation audit may this record change to
**PERSISTENCE-2D PASS — FOUNDATION 2 QUALIFIED**. Release version, artifacts,
tag, and GitHub prerelease require a subsequent exact-release-commit gate.
