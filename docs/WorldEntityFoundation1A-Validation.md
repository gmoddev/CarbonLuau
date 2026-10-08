# Entity-1A — private startup-observed full-Spawn lifetime substrate

**Verdict: PASS for the pinned Windows/Linux host targets, private only.**
Starting source: `3c0d057c490e88bbcd9ed6d6552a2213ba7783b1` after `v0.5.0`.
This record closes D20's private exact-lifetime gate; it does not publish
`Workspace`, `Entity`, lookup, enumeration, Signals, Spawn or Destroy.
Entity-1B may begin only as a separately scoped exact lookup/read-only phase.

## Exact host and adaptation envelope

Both platforms reported Rust dedicated-server build `25653776`, protocol
`2634.289.1`, log revision `166494`. The Linux Carbon target was `2.0.261.0`
at `c74c4ca8d0f7a9c8e8a431b077c0b3010f476e44`; the Windows target was
`2.0.262.0` at `8a81d70`. Both shipped Harmony `2.4.2.0`. Selected identities:

| Binary | Linux SHA-256 | Windows SHA-256 |
|---|---|---|
| Rust `Assembly-CSharp.dll` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` |
| Carbon core `Carbon.dll` | `44d0e88a7c8c9c45f8a94a67896cb7a8da4f0f4ac73e4bdc85fba1d40f5584f4` | `b8873f74343de09c49ea285aa981b1b1e3a53d7f193a7d6c38b3de4bd467abe4` |
| Carbon `Carbon.Common.dll` | `5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb` | `0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a` |
| Carbon `Carbon.Hooks.Community.dll` | `4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343` | old `1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3`; updated `4364a8782fd012dd8cfa8acf1d778c1e6bee9bf6bbe0efb801deba685778f50e` |
| Carbon `Carbon.Hooks.Oxide.dll` | `b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764` | old `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d`; updated `0b668e1f4819ef3455c70781428c0afdd02601d2398eef71bd28428a8a073afa` |
| Harmony `0Harmony.dll` | `77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d` | same |

The two Windows hook pairs are individually pinned; arbitrary mixtures are
rejected. Carbon's updater can replace both hook components, so the adapter
does not treat a changed binary as automatically equivalent. The exact
selected Spawn-transpiler bodies in the updated pair were structurally checked.
The installed Rust/Carbon/Harmony files, MVIDs, 24-target inventory and active
Harmony patch topology are verified at the private boundary; unsupported drift
leaves Entity admission unavailable. The runtime does not promise an arbitrary
future Rust/Carbon build or third-party patch topology.

## Startup and completion proof

Carbon's initial plugin batch installs required AutoPatches after plugin `Init`
and before `Loaded`; `FileSystem_WarmupHalt` holds Rust world startup until
that batch finishes. `ServerMgr.Initialize` then enters the save/map restoration
path. CarbonLuau's exact `Initialize` prefix requires an empty keyed registry
and complete observer patch installation before beginning the observation
window. `OnServerInitialized` follows world restoration; it performs a one-time
bounded keyed-registry reconciliation and qualifies the baseline only when every
then-admissible keyed `BaseEntity` has an observed completed full-Spawn epoch.
Late Spawn calls remain covered by the continuously installed observer. No
current-state snapshot or marker substitutes for individual completion history.

There are 24 qualified Spawn method bodies: `BaseNetworkable.Spawn` and 23
`BaseEntity` override bodies. An outer invocation prefix advances its epoch
and immediately retires an older lifetime. The exact nested base-call chain,
`__runOriginal`, postfix and finalizer must establish a normal *outermost*
completion, including the `BaseEntity`/subclass tail. Throws, skipped originals,
missing callbacks, unexpected recursion, changed patch topology or incomplete
base chain cannot complete the epoch. The observed normal Carbon hook pair
coexists with this fence; an unexpected foreign prefix/postfix/finalizer or
transpiler rejects qualification. CarbonLuau never enters Luau from these
patches. The narrow residual possibility of a trusted component installing a
patch *during* an already-running Spawn is in I12's in-process interference
boundary, not a promise of hostile CLR isolation.

The research fixture separately exercised the known network-update and
`BaseEntity`-tail failures. Both demonstrated why a base-method or present-state
test is insufficient. Failed same-object retry advanced the epoch and did not
admit; a successful same-object retry was **not observed** in that fixture.
Naturally pooled same-managed-object reuse was **not observed**; qualified
fence coverage, not an observed pool reuse, supports the no-retargeting theorem.

## Private lifetime and authority

The private model belongs to the loaded CarbonLuau host observer, not a Luau VM.
It uses weak managed-object identity, monotonic per-object `SpawnEpoch`, an
invocation-scoped attempt object, monotonic host-local `EntityLifetimeToken`,
captured network ID and bounded prefab evidence, plus sticky retirement. The
model instance is the opaque host-lifetime identity. A process restart or full
observer gap creates no carry-over identity; same-process unload/reload is
permanently unqualified until server restart.

Private admission requires continuous qualified startup observation, the exact
object's current completed epoch, live/fully-spawned state, nonzero captured
network ID, exact keyed registry occupant, valid bounded prefab, and a current
FacadeSession VM/domain/publication witness. Validation rechecks each condition
on the owner thread. A mismatch permanently stales the affected binding/token;
the old binding never re-resolves by ID or attaches to a replacement object or
epoch. The publication witness follows D7/D10 begin/commit/rollback; nested
rollback, failed candidate, successful root/addon replacement, VM recovery and
session retirement cannot retarget old authority. Ordinary still-live same-
object/same-epoch unregister/reinsert between observations is not a new
incarnation; observed missing required occupancy may retire conservatively.
No private record strongly retains its Rust/Unity object.

## Live qualification and negative evidence

The exact-target structural checker passed **1,249** checks with both updated
Windows hook components; the pure lifetime model passed **154** checks. The
publication-witness managed regression covers nested commit/rollback, stale
root session, numeric-ID collision and addon replacement. Production packages
without test fixtures are deterministic and contain no public World/Entity API.

The task-owned disposable Windows host's final private fixture log
`D:\Sandbox\Codex\Entity1AStartup\evidence\adapter-clean-20261002-182848\server.log`
passed pre-existing and post-start entity admission, registry churn, sticky
retirement, failed same-object retry, failed/successful root replacement and
real fatal-VM timeout/recovery. Its final ordinary hook-demand run
`adapter-demand-20261002-181503` passed the same fixture with 1,730
`OnEntitySpawn` and 1,730 `OnEntitySpawned` observations. Final first-install
hotload `adapter-hotload-20261002-182756` reached server startup before plugin
load and rejected Entity baseline with an explicit restart-required diagnostic;
the following qualified initial-batch boot is the restart path. Windows
save-loaded restoration was observed separately
in [startup research](WorldEntityStartupObserverInvestigation.md).

The task-owned Linux host's final private fixture log under
`/root/codex/entity1a-25653776/adapter-check/evidence/entity-private-20261002-222545/server.log`
passed the same private admission/root replacement/VM recovery matrix with
ordinary hook demand (1,748 matching hook observations). The same-process full
plugin unload/reload probe at `adapter-reload-20261002-222908` reported
`RELOAD_FAIL_CLOSED` after an initially
qualified startup: the new instance did not trust history across the gap.
Prior research runs cover exact network-update and outer-tail failures,
save/load restoration and negative first-hotload behavior on both platforms.
The Linux disposable copy temporarily self-updated Carbon while networked;
the adapter correctly rejected that host hash. Restoring the pinned task-owned
assemblies and disabling updates **only in that disposable copy** allowed the
final pinned-host run. This is not evidence of support for the updated Carbon
binary. No public ports, authenticated client or production server were used.

No native code changed in Entity-1A, so native ASan/UBSan are not an Entity
adapter test; the unchanged ABI-1.5 release binaries were used for the live
VM/domain exercises. The weak-reference and churn/retirement model checks cover
managed retention. The final Windows full managed/runtime suite passed against
the `v0.5.0` release-native binaries whose provenance names the starting source
revision, including the new publication-witness test, GUI/Player/addon/provider,
persistence, real VM timeout/recovery and 100-addon scale regressions.
`Test-Api`, `Test-Architecture`, `Test-Release` and `Test-Package` passed;
two default production packages had identical SHA-256
`c7d29410dd883255cb028013993bcdf856e6cb7d9385b8b60b9fdf054e76f5c7`.
The production package contains 63 C# sources and no live Entity fixture.
Linux/Windows real Carbon startup and final managed/runtime regressions are
distinct from the pure model tests. Final-source hosted Windows/Linux CI is
recorded with the closing commit/push rather than inferred from earlier runs.

The earlier [registry-continuity investigation](WorldEntityLifetimeInvestigation.md),
[failed-Spawn probe](WorldEntitySpawnEpochProbe.md),
[base-completion follow-up](WorldEntityCompletionFenceInvestigation.md) and
[startup-observer investigation](WorldEntityStartupObserverInvestigation.md)
remain intact. Their negative observations were not rewritten as successes.

## Deployment and Entity-1B boundary

Initial install onto an already-running server and full CarbonLuau plugin
unload/reload require a server restart before Entity admission. Ordinary root,
addon or VM reconstruction does not if the observer remains continuously
installed. This applies only to the exact pinned hosts; a newer Rust/Carbon
build, changed hook components or competing patch topology requires fresh
structural and live qualification. Entity-1B may now implement only keyed
`Workspace:GetEntityById` and read-only `Entity.Id`, `Entity.Prefab` and
`Entity.Position` with their separate bounds, stale-error and platform gates.
No enumeration, Signals, Spawn, Destroy, package/API/version bump or release is
authorized by Entity-1A.
