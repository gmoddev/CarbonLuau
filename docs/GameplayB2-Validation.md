# Gameplay B2 — Entity death and attribution qualification

Status: **GAMEPLAY-B2 PASS — ENTITY DEATH SIGNAL QUALIFIED** within the exact
host/source/attribution and best-effort transport envelope below.
Starting main: `6d2307207138ccb4eec9cd5005d920b1aea98fd7`.
Implementation: `d79f06432dc14d8745331ebd16fd5049e5b458c0`.
Final tested source: `8ac62fb22676ea35ab926947fd7edcbd405be175`.
All applicable current-source local, live and hosted gates PASS. No earlier D3
hosted receipt substitutes for this gate. The subsequent closure edit is evidence-
only and reuses these immutable source runs under Compatibility's policy.

This separately authorized phase reuses [Gameplay A](GameplayFoundationA-Validation.md),
[B1](GameplayB1-Validation.md), [B3](GameplayB3-Validation.md) and
[D3](GameplayD3-Validation.md). It does not redo their lifetime, Spawn, removal,
loot or scheduler architecture. D20/D23 remain canonical owners.

## Exact-host research

Rust 25653776, protocol 2634.289.1, revision 166494; Windows Carbon
2.0.262.0/8a81d70; Linux Carbon 2.0.261.0/c74c4ca; Harmony 2.4.2.0.
Backing assembly hashes match existing A/B3/D3. New all-type inspection finds
461 combat type declarations (454 concrete), five Die(HitInfo) definitions,
55 combat OnDied definitions, six resource types and four resource OnDied bodies.
Completed-Spawn membership is reused, not inferred from a proxy/token cache.

Rust managed backing SHA256: Windows
`bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`,
Linux `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`.
Existing A/B3/D3 records retain the exact hook/Common/Startup hashes and different
Windows/Linux Carbon tuples. All 67 new processed death/reset body pins and both
melee writer pins were rechecked against actual loaded methods, not mixed-age
inventory. All 467 loaded combat/resource type chains pass the topology ledger.

BaseCombatEntity.Die sets health/lifestate before fallible mission, corpse,
virtual OnDied and finally work. A state write or early OnEntityDeath hook is
not completion. The pinned OnEntityDeath injections emit Call then Pop: returned
hook values do not veto these target deaths. OnPlayerDeath, damage vetoes,
skipped originals and hook side effects are distinct mechanisms.

The private adapter fences full effective original Die/OnDied completion,
requires original terminal progress and final dead/killed state or certified
original removal, and rejects throwing/skipped/poisoned nested attempts. Source
ID/prefab/optional bounded root Position precede invalidation. Repair/reset
episodes and Player life epochs must be qualified separately from Spawn epoch;
death does not mean successful removal. In ordinary barrel paths B3 may enqueue
EntityDestroyed before full Die returns and B2 can enqueue EntityDied afterward.
D3 payouts already occurred; this Signal cannot cancel or replace them.

`Research-EntityDeathHierarchy.ps1` and its negative checker preserve source
state-write/callsite/override evidence externally. `Reproduce-EntityDeathPins.ps1`
reproduces Carbon's known IPlayer publicization without executing Rust code,
and proves original/processed opcode, slot, local and EH parity. Both current
live hosts match all 67 reproduced body pins. New data in
`EntityDeathPins.Carbon.cs` is private compatibility data, not a runtime feature
by itself or permission to accept future host drift.

## Public shape under qualification

`Workspace.EntityDied:Connect(function(Context) ... end)` uses existing owned
Signal/Connection registration and deferred delivery. Context is a frozen
ordinary table with Id, Prefab and optional Position, Killer, KillerId,
DamageType and Weapon; no live Entity/HitInfo/host pointer is exposed.

Historical source snapshots retain a certified exact original completed epoch,
weak owner/witness and immutable scalars. Fresh callback admission checks that
certificate, observer/host continuity, listener/publication and VM/domain authority;
it never selects the current network-ID occupant. Later destruction or ID reuse
does not turn this context into a live proxy. Retirement cancels without replay.

Private B2 payloads have exactly 14 fields; existing kinds remain exactly 12.
Shared bounds remain 128 captures/frame, 128 deliveries/frame, 32/domain/frame,
4096 visits/frame, 512 reservations, 2048-byte payload and 2-MiB transport budget.
Death/removal Position attempts share the existing bounded 128-attempt allowance;
exhaustion supplies nil Position, not an unsafe getter or a second world scan.
Synchronous source frame depth is 16; cold type inventory is capped at 8192.
No new scheduler, unbounded history, synchronous Luau hook or retry queue exists.

## Attribution limits

[Attribution research](history/gameplay/DeathAttribution.md#gameplayb2-attribution-research) proves that
InitiatorPlayer can dispatch virtual ToPlayer owner/controller resolution.
Even a direct human Initiator field may already come from an indirect owner-ID
lookup before capture. It is not universal original-shooter/placer provenance.
The separately pinned writer fence passed the candidate real-RPC matrix on both
hosts. Deployment policy now opens only synchronous RPC melee, borrowing the original RPC
player/connection/hit/weapon only around the proven attack application call.
No Source actor is recovered by UserId at callback delivery.

Damage labels use at most 28 finite weights and fixed known labels; empty/invalid
weights produce nil, not the host's misleading all-zero Cannon default. Weapon
means a qualified cached full WeaponPrefab label, never an equipped Item or
owner/cause inference. Mutation or uncertain correlation omits metadata.

## Evidence and preserved negatives

- Both hosts match all 67 processed body pins. First-run compile failure from
  a missing AutoPatch namespace import is preserved, not PASS.
- Partial-file static initialization initially captured a null existing target;
  fresh token resolution fixed it. The next live runs arm source, marker and
  467-type table, observe completed barrel deaths and pass existing D3 payouts.
- Initial new-kind native admission was missing its discriminator: snapshots
  were safely rejected/refunded, not delivered. The integration was corrected.
- Codec tests exposed listener ID zero admission. Strict new-kind identity
  checking fixes it. A test-only premature refund masked that assertion with
  teardown INTERNAL_ERROR; fixture cleanup now retains native-held accounting.
- A recovery test incorrectly expected optional edges to order reconstruction.
  Canonical D14 does not do so; the fixture now checks Failed/unavailable then
  explicit consumer replacement. No dependency semantics changed.
- Corrected Windows real-VM tests pass frozen historical snapshots, exact
  captured D11 transport, malformed/duplicate payloads, publication/replacement,
  provider/dependency behavior, callback faults/recovery and 100-addon fairness.
  Host transitions in that suite remain synthetic, not physical Rust evidence.

The current source closes Windows/Linux combined gates and final-source hosted CI.

| Newly measured candidate evidence | Result / immutable receipt |
|---|---|
| Windows actual RPC source, public barrel callback, D3 physical regression | PASS; `loot-d2a-20261011-073119-184/server.log`, SHA256 `4f0bfb93898b348ee60f8e54d2696dbb8080b5b720f5e5f1b97db67003d63fa7` |
| Linux equivalent source/callback/physical matrix | PASS; `loot-d2a-20261011-073129-1737391/server.log`, SHA256 `e1e43165cb04efbe2c1bef44cd69c7586b12da04550a97221bb48a446e6491ab` |
| Linux expanded current-source live matrix | PASS; `loot-d2a-20261011-084947-1748420/server.log`, SHA256 `a591160cfcc6ac1312d8e60c868f8f3c08f8988e289155bdaa2b39a92ac6411e`; package `064c665f103298a1db9c6801cce32e5c0c08556fe394b95ccd46dcb840ea1d65` |
| Final Windows fifteen-case source/live matrix | PASS; `loot-d2a-20261011-090547-838/server.log`, SHA256 `4db177cfe5ddbb1489ae421d1ae4651713c9d124ab146688d0b865ad3fa7cf2a` |
| Final Linux equivalent matrix | PASS; `loot-d2a-20261011-090558-1749608/server.log`, SHA256 `a2a17b891a8e9a141b37acf11166153e476cfd4eb4e6043a79dff5510c400859` |
| Final identical qualification package | SHA256 `76198c5d6495f2a77be10fd66b3de967b5a7531ece46b41706b1125759a307f0`; native Windows `4c4fb6aef7c95c3a8ae02ce669e9df9ff6ec126f3d0e8f00756c41cf8bbaab32`, Linux `b353d6af130af96ceb6a30365e0f43036d689787ae7f15f28805d307233b2fff` |
| Both source runs | Qualification package SHA256 `ea5b5cd972ebc8125b6756d3e5bfe9fe80dd363366d3061ddd2f70b8421e20bf`; deployment flag still false during those runs; original source token proved, public actor fields intentionally nil |
| Windows native regression | 23/23 PASS; additional fourteen-field codec, ownership and allocation faults PASS |
| Linux native regression | 24/24 PASS; ASan/UBSan/leak suite 24/24 PASS; additional death-context allocation faults PASS |
| Windows/Linux focused real VM | PASS, including 100-addon scale; peak qualified VM accounting 9,254,816 bytes |
| Windows/Linux full managed runtime | PASS, including persistence, GUI, addons/providers, package parser, recovery and Player operations |
| Metadata / generated definitions / tooling | Generated parity, managed tooling, API/link audit and positive/negative pinned LSP PASS; no preview simulation |

These receipts predate subsequent host-adapter review corrections and are not
final-source PASS. Independent review found completion needed subtype-topology
revalidation, owner-thread rejection before NPC host reads, and alive retry after
a failed NPC respawn. Those paths are corrected and their final live regressions
pass on both hosts. An injected metadata exception must not escape into host death; optional
metadata is omitted on failure. Managed type selection after destruction uses CLR
identity rather than Unity false-null equality. The expanded live fixture's first
compile exposed a qualification-only UserID.ToString overload error, not a runtime
PASS. Preserve that negative receipt and rerun the corrected fixture.

The expanded Linux matrix covers all fifteen actual boundary cases, full NPC
failed-respawn/alive-retry and explicit Kill veto, both historical public episodes,
real original RPC actor plus same-account reconnect, actorless Decay/Explosion
death-associated hit labels, persistent and self-restored subtype interference,
and the constructed human's shared A/B2 finalizer. D3 physical replacement/partial/
cleanup remains PASS. No authenticated client, autonomous decay/explosive origin,
natural pooling or every prefab instance was measured by this fixture.

The negative Windows one-context receipt was traced exactly: all NPC operations
occurred in Unity frame 29289; FanoutRejected increased 57→58 under the unchanged
32-delivery/domain/frame policy, while stale/producer/queue errors did not grow.
This is the qualified overload behavior, not same-birth dedup or a timeout.
The nominal fixture now delivers phase one and continues only in a distinct
actual Unity frame (Windows 28809→28810, Linux 26918→26919), preserving all
original lifetime/no-inline/public-count/resource gates. It retains only one
bounded owned continuation, with eight wait attempts and 32 fixture poll turns.
Two direct Kill vetoes are counted separately from three total requests,
including the naturally deferred KillMessage request; no Invoke is cancelled.
Both final public NPC contexts pass. Production limits were not changed.
New model and real-VM tests independently prove same Id/birth with two distinct
certificates/nonces, fresh frozen tables after epoch supersession, no live
Entity lookup, and exact accepted/released/pending/VM resource baselines.
Final-source hosted CI PASS. Deterministic packaging/clean installation
on both platforms, all 68 examples and API/metadata/tooling gates pass. All 83
production entry names and uncompressed hashes agree across platforms (manifest
SHA256 `ce47fcdf9391d8ff61a7f87955c0b3359e52d46e83d3f41ab921006190b6e858`);
platform ZIP creator/permission fields and ten DEFLATE streams account for archive
byte differences, not source drift. No test fixtures or private state are packaged.
Local/live preliminary positives do not manufacture PASS.
Isolated server runners restore installed hashes and own only task servers.

The final live matrix also retains root/addon failed and successful publication,
provider unload and fatal recovery from the D3 combined fixture. Synthetic full
VM tests supply overload/100-addon fairness, old-listener cancellation, malformed
payload, callback ordinary/yield/fatal failures and provider/dependency matrices;
they are not relabeled physical Rust transitions. Both final live runners verify
startup observer qualification, fifteen-case completion, RPC source/public
receipts, D3 physical counts, shutdown/save and restoration of installed hashes.

Independent focused reviews covered full virtual completion/subtype drift,
original action versus indirect owner resolution, same-birth episode delivery,
weak retention, owner-thread guards, shared A finalizer cleanup and owned async
fixture teardown. Substantive findings were corrected and requalified rather
than waived. Final review reports no substantiated blocking production finding.
Model lifetime tests pass 17,289 checks on both platforms; same-birth model/VM
tests admit exactly two fresh nonces, release both reservations and retain zero
payload/VM/host-lookups at baseline.

Expanded live diagnostics additionally found that the scientist's two full
deaths, failed-outer-respawn/alive-retry sequence and private/public capture
authority completed correctly: completion/capture deltas 2, final Dead and
DeathSeen true, original D20 validation true, two returned corpses and zero
inline Luau entries. The fixture incorrectly assumed two immediate Kill calls;
the measured count was zero. A death must not be equated with immediate removal.
The corrected matrix exercises explicit post-death Kill vetoes and retains
the original exact event/resource assertions rather than relabeling the failed
fixture as PASS.

The actorless continuation subsequently exposed a test-injected shared-source
loss: its foreign Harmony Kill prefix correctly latched B3's observer
unavailable. New completed Spawns still had valid D20 admission but no B3 weak
watch, so the B2 producer could not enter. B2 availability now explicitly requires
the shared watch source's qualified continuity; no healthy Signal registration is
advertised after that source loss. No B3 observer behavior or lifetime rule was
weakened/rearmed. The NPC fixture uses the already-qualified ordinary scoped
Carbon OnEntityKill veto instead of patching the removal target. Both the
watch-missing diagnostics and temporary duplicate-partial compile failure are
preserved negative evidence. No unmeasured continuation is promoted to PASS.

Package 0.5.0, API 0.6.5-experimental, ABI 1.5, provider 1.2, schema 1 and Luau
pin remain unchanged. No release/tag, EntityDamaged, inventory Signal, extra Loot
mode, generic hook API or public Spawn/Destroy is authorized or implemented.

## Final-source hosted closure

- [CarbonLuau validation 38142294648](https://github.com/gmoddev/CarbonLuau/actions/runs/38142294648):
  PASS Windows x64, Linux x64 and ASan/UBSan/leak lifecycle/fault fixtures.
  Includes strict fourteen-field death codec/allocation tests, public B2 real-VM
  and same-birth two-certificate/two-nonce tests, 100-addon fairness, full existing
  runtime/loader/addon/publication/GUI/Player/Loot/discovery/persistence suites,
  deterministic packages, imports/exports and clean install/all 68 examples.
- [Tooling Foundations A/B 38142294688](https://github.com/gmoddev/CarbonLuau/actions/runs/38142294688):
  PASS Windows, Linux and macOS tooling; exact generated API/definitions/docs,
  metadata/type/link checks, analysis/VS Code contracts and unsupported-host
  preview semantics. No persistence/gameplay simulation was added. macOS is
  tooling-only, not a server runtime claim.
- [Tooling baseline contracts 38142294807](https://github.com/gmoddev/CarbonLuau/actions/runs/38142294807):
  PASS canonical schema/API/preview contracts on the same final tested SHA.

Independent reviews have no unresolved blocking finding. All shared watch,
completion, publication, at-most-once/no-replay and exact original D11/D20
authority gates remain intact. Historical failed receipts are retained, not
recast as PASS. The default package excludes qualification fixtures, logs,
vendor assemblies and storage state. Commits use GitHub no-reply metadata.
Existing untracked `.codexlock` and `derived-369547800317500/` remain preserved.
Dedicated live processes exited, runner leases cleared, prior installed hashes
restored and temporary test connections/owned scene resources converged.
Reusable Windows D: and BigKVM build/evidence caches are retained; unrelated
workloads and security policy were not changed.

## Handoff and explicit limits

Public signature is only `Workspace.EntityDied:Connect(function(Context) end)`
with the existing Connection surface and seven-field frozen EntityDeathContext.
It does not promise universal killer attribution, zero overload drops, an atomic
world snapshot, loot cancellation, client delivery, every prefab live-tested or
coverage of arbitrary CLR/plugin-defined entity types. Supported pinned combat/
resource hierarchy and loaded topology are structurally checked; physical
representatives are LootContainer, scientist NPC and constructed human, with
actual RPC melee for actor provenance. Autonomous environmental origins,
authenticated-client gameplay and natural pooling are not newly claimed.

Stop after B2. Next work must be separately scoped; Gameplay C2 remains the
completed net-quantity inventory-observation proof gate. EntityDamaged, extra
Loot prefabs/modes, DropResolved, conditional reward policies and public Spawn/
Destroy are not authorized by this PASS. No new release/tag or package bump.
