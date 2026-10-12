# Gameplay B2 attribution expansion — offline design and evidence

Status: **OFFLINE STATIC MAPPING COMPLETE; EXPANDED ATTRIBUTION UNQUALIFIED AND DISABLED**.
Research date: 2026-10-12. Repository baseline:
`dabb872094f74e60d75de099379d198fb63a6bf8`, published package `0.7.0`,
API `0.7.0-experimental`. This report owns a proposed research continuation,
not a canonical amendment or production eligibility change.

Only this report and [the offline checker](../../tools/Research-AttributionExpansionOffline.ps1)
were added. No production implementation, public API/codec, new event bus,
release identity, tag, asset, deployment or shared documentation index changes.
Cached exact assemblies and saved receipts were read locally; neither remote
host was contacted. No RustDedicated, live runner, plugin installation or host
build was performed. Other agents' files and historical evidence remain owned
by their authors. Independent subagents were authorized; no subagent execution
tool was available in this session, so this is one investigator's result.

## Existing authority and qualification retained

Read with [I1–I12](../Invariants.md),
[D7/D10/D11](../invariants/Runtime.md),
[D20](../invariants/WorldEntity.md), [D23](../invariants/Gameplay.md),
[Compatibility](../Compatibility.md) and its [qualification policy](../qualification/Policy.md).
Existing [architecture attribution research](../history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-11-damage-and-kill-attribution)
is historical direction; its earlier field-human/ID-only suggestion is superseded
for B2 by the preserved [original-action finding](../history/gameplay/DeathAttribution.md#gameplayb2-attribution-research-original-action-connection-smallest-defensible-eligibility-proposal)
and current D20.

[Gameplay B2 qualification](../GameplayB2-Validation.md) remains PASS in its
recorded envelope: implementation `d79f06432dc14d8745331ebd16fd5049e5b458c0`,
final tested source `8ac62fb22676ea35ab926947fd7edcbd405be175`, exact
Windows/Linux full death/reset completion, representative physical deaths,
bounded shared transport and **original synchronous RPC melee only** for
Killer/KillerId. Its final Windows/Linux live receipts are
`4db177cfe5ddbb1489ae421d1ae4651713c9d124ab146688d0b865ad3fa7cf2a`
and `a2a17b891a8e9a141b37acf11166153e476cfd4eb4e6043a79dff5510c400859`.
Those are inherited receipts, not newly executed tests. The record distinguishes
synthetic real-VM tests, constructed host connections, actual original RPCs
and authenticated-client evidence. Autonomous decay/explosive provenance,
natural pooling and authenticated-client shooting remain unqualified.

A/B1/B3 completion, D20 startup/epoch authority, qualified NPC episode reset,
D3 payouts and C3/C4 sampled inventory semantics are reused through their
current records, not reopened. In particular, qualified same-incarnation NPC
repeat death is settled; this report concerns the *actor*, not another death
episode mechanism. Public support remains one exact barrel prefab with
`scrap`/`metal.fragments`, sampled ItemChanged and melee-only B2 attribution.

The public EntityDeathContext remains the frozen seven-field shape:
Id, Prefab, optional Position, Killer, KillerId, DamageType and Weapon.
DamageType observes a strongest positive finite component of the death-associated
hit; Weapon observes a bounded cached full WeaponPrefab label. Neither proves
the actor. No Cause, Owner, Attacker, launch token or projectile ID field is proposed
for public exposure here.

Current source observations (SHA256, read only):

| File | SHA256 |
|---|---|
| `CarbonLuau.EntityDiedAttribution.Carbon.cs` | `55f0fd1d538a46b121fe3111486e07df3f40df1934d2d68174c0e7963f602ee5` |
| `CarbonLuau.EntityDied.Carbon.cs` | `717149203a7cad6f56f66b771c17d046dd452c9eed41551262a4d1ecb7aeb673` |
| `CarbonLuau.GameplayLifecycle.Carbon.cs` | `7755d30f64a3353543991fcfcd120b2d004f760c974963a3304036d6bd7250c8` |

The current melee source borrows original actor/connection/hit/weapon/victim
through two pinned calls, correlates the original D11 token and D20 witness,
rejects nesting/mutation, copies bounded scalars before invalidation and checks
terminal correlation. Its capture validates the original connection; terminal
correlation deliberately performs no later account-directory lookup.
A later captured actor disconnect can leave an original stale facade, never
substitute a reconnect. The 16 synchronous frames and 128 captures/actual frame
are existing private bounds; source pin/topology failure closes admission.

## Inputs, evidence reuse and scope of static proof

The local read-only cache is
`C:/Users/aiden/AppData/Local/Temp/CarbonLuau-Entity1A-25653776`.
It contains the exact bytes identified by the user for these remote paths:

| Platform/input | Authorized origin | SHA256 / MVID |
|---|---|---|
| Windows Rust backing PE | `dockerbox D:/Sandbox/Codex/Entity1AStartup/server/RustDedicated_Data/Managed/Assembly-CSharp.dll` | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`; `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e` |
| Linux Rust backing PE | `BigKVM /root/codex/world-movement-20261003/server-linux/RustDedicated_Data/Managed/Assembly-CSharp.dll` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`; `616082a0-36f4-4680-a1ab-efc5766796cc` |
| Mono.Cecil 0.11.6.0 | cached metadata reader from `server/carbon/managed/lib/Mono.Cecil.dll` | `0c6798722b72899c54648e58a950a945163f6e2d6ebbc1a1e11c6f771cdaed0f` |

The inherited host tuple is Rust 25653776 / protocol 2634.289.1 / revision
166494; Windows Carbon 2.0.262.0/8a81d70, Linux Carbon 2.0.261.0/c74c4ca,
Harmony 2.4.2.0. No fresh loaded-host identity is claimed.

Saved research root:
`C:/Users/aiden/AppData/Local/Temp/CarbonLuau-B2-Attribution-20261011`.
The checker validates both PE hashes, Cecil and these six existing receipt hashes
before reading Rust metadata. These passes already established direct initiator,
connection and copy/dispatch negatives; their investigations were not rerun as
new qualification.

| Reused pass | Windows IL receipt SHA256 | Linux IL receipt SHA256 |
|---|---|---|
| origins | `de81999768caabcfcd21b4407cb553c5962d77da2cc7534c94a3f26d4c99fae1` | `6ac070258c4b1302246a108ea5e681907b8e656726566974187acc5c1565bcbc` |
| connections | `d52719be0227996218629fda881d307f196709c47b5502cca34d9a780c3bbe75` | `c9260eae67bb60176c2ddab19a08d5007dc515a2f1c2bc356d240c4781f3e052` |
| copy-and-dispatch | `dec3d1d3216d75898d727ae40e5f1ef193dc5394bb3c96b4adbaff1df506aa49` | `4962d95dba0fc9fcaebac61e1ea3d4d44294c0d731ac61647021fb3c2a69a76e` |

Fresh offline inspection confirms 45,570 methods per PE, 19 direct
`stfld HitInfo.Initiator` sites and four direct Predicted writers; FiredProjectile
stores no Network.Connection field. Its proof scope is these backing managed
assemblies, not native damage producers, Carbon transformations or arbitrary
trusted plugins. The historical prose says 7,666 types on both platforms;
the preserved SCAN_COMPLETE lines and fresh enumeration actually give
**Windows 7,666; Linux 7,668**. The old record remains untouched.

Method hashes below use the historical convention: SHA256 of UTF-8 Cecil
instruction strings joined with LF and no trailing LF. They are neither raw
method-byte pins nor processed/live Harmony pins. Backing tokens/offsets describe
these PEs. The full 52-method selected hash/size/instruction/EH parity passes.
Call edges include virtual/interface dispatch; they prove an operand/callsite,
not every dynamic implementation or successful execution.

## Expanded source map: attacker, ownership and missing certificate

In this table, “actor” means an original human D11 action connection eligible
for Killer. “Source” means the Rust object supplied as damage initiator.
“Owner” is a placement/account/controller association and is not equivalent
to either. Every non-melee row still produces **nil Killer and KillerId** today.

| Family | Exact static chain and token/callsite evidence | What is proved / expansion gate |
|---|---|---|
| Qualified synchronous RPC melee | PlayerAttack `0600179e` writes actor at `004f`, Predicted at `0069`, calls DoAttackShared at `0bd7`; base shared `06001795` calls HitEntity.OnAttacked at `0029`. | Existing qualified action certificate. Only effective base shared implementation and unchanged same hit/victim/connection pass; subclass name is not authority. |
| Client firearm/projectile, including thrown melee projectile | BaseProjectile.CLProject `06001d15` -> NoteFiredProjectile at `02d9`; BaseMelee.CLProject `06001793` -> same at `0269`. Note `06001a41` stores shooter object at `0297`; OnProjectileAttack `06001a3a` creates another HitInfo, assigns this at `0049`, report RPC Predicted at `009e`, applies at `171c`. | **New launch edges** distinguish a throwing melee projectile from synchronous PlayerAttack. Launch and report connections are separate observations. Original launch connection does not survive in the host record. Need independently bounded launch-to-hit certificate and cleanup/ID-reuse proof. Bullet/Arrow/melee weapon class do not solve it. |
| NPC/server weapon | BaseProjectile.ServerUse `06001cec` calls AssignInitiator `06001ced` at `0436`; assignment uses GetOwnerPlayer at `0002` then parent fallback at `001c`. ServerNoteFiredProjectile `06001a42` also stores a player object. | Object/parent source, possibly NPC. No original human RPC certificate. ServerUse is not made a player action by equipment ownership. |
| Rocket/server projectile | BaseLauncher.SV_Launch `06001767` reads RPC player, stores creatorEntity at `023d`, initializes ServerProjectile at `027b`, Spawns at `0282`. ProcessHit `06005b89` dispatches IProjectileImpact at `004a`; TimedExplosive.ProjectileImpact `06003779` calls Explode at `0013`. | **New interface dispatch chain**: source creation -> later impact -> explosive application. Creator object survives; connection certificate does not. TimedExplosive is one compatible target, not universal proof of every IProjectileImpact implementation/prefab. |
| Thrown explosive/fuse | DoThrow `06003760` forwards RPC player to DoThrowImpl `06003762` at `008a`; implementation calls SetCreatorEntity at `0095`. ServerInit `06003774` calls SetFuse at `0018`; SetFuse `06003777` schedules virtual Explode through Invoke at `0017`. | **New delayed boundary**: creator assignment is separated from detonation. Need exact original created incarnation, launch token and all accepted detonation branches. Disconnected placer, restored save and transferred ownership must not select a current account connection. |
| Explosion/splash | TimedExplosive.Explode(Vector3) `0600377d` passes creatorEntity to RadiusDamage at `020f`, and writes initiator for other hits at `02f0/038c/03d6`. RadiusDamage `0600813a` copies supplied initiator at `01cd` and weapon prefab at `01d5`. | Radius helper propagates caller data; it cannot authenticate caller actor. Multi-victim and additional direct hits need per-child correlation and bounds. Chain reaction attribution is not inherited merely from an outer explosion. |
| Projectile ammunition splash | OnProjectileAttack invokes item-mod callbacks at `16b4/16de` before direct application `171c` and ServerProjectileHitEntity at `1732` afterward. Radial mod `06006bee` allocates child hits at `022c/025b` and applies at `0231/0260`. | Preserved separate-hit negative. A future direct-hit certificate must exclude these children initially; ambient RPC scope and identical source fields cannot bless every nested hit. Post-direct callbacks remain a separate source too. |
| Fire propagation | FireBall.DoRadialDamage `060055b6` chooses creatorEntity if Unity-valid, otherwise the fireball at `008f`. FlameTurret.DoFlame `0600573e` passes turret to radial calls at `00eb/0138` and fireball.creatorEntity at `021c`. | Fire source/creator propagation is proved, human causation is not. Fire spawned by a deployable does not turn its owner into the attacker. Original fire lineage and bounded child retention would be a later independent gate. |
| Autonomous turret | AutoTurret.FireGun `0600130a` calls ApplyDamage `0600130b` at `0125` and miss fallback `01d5`; ApplyDamage writes this into Initiator at `0062`. | **New two-application edge map** proves the autonomous turret source, not its placed owner. Owner-based human attribution would change meaning and needs a separate semantics decision, even with a perfect owner certificate. |
| Traps/deployables | BearTrap.DelayedFire `06001f04` constructs a hit with this at `0037`; GunTrap.FireBullet `06002820` does so at `00fc`; Landmine.Explode `06002ab5` passes this to RadiusDamage at `005d`. | Physical trap source; trigger victim, placer, current owner and remote detonation caller are different roles. None is automatically the Killer. No ownership search is proposed. |
| RF restore / bee owner rewrite | RFTimedExplosive.Load `060032ba`: saved creatorID -> FindByID `0057` -> creatorEntity `005c`. BeeTriggerHurt.ModifyHit `0600430f`: Initiator.OwnerID -> FindByID -> rewritten Initiator `0033`. | Preserved explicit retarget hazard: even a final direct BasePlayer field/current D11 validation can describe a replacement account connection. Require absence of this path for any first certificate; never patch around it with another lookup. |
| NPC direct attack | BaseNpc.StartAttack `06001837` passes this NPC to Hurt at `006f`; NPC weapon paths also use ServerUse. | Nonhuman source; no human actor. Current human conversion through ToPlayer is not adopted for mounted/drone/controller cases either. |
| Environmental fall/metabolism/decay | ApplyFallDamageFromVelocity `06001ab4` -> Hurt at `0085` with null attacker; base metabolism `0600561b` at `0044/0099`; DecayEntity.OnDecay `06002418` at `01cc`, null attacker. | Source categories can be observed, no actor manufactured. This is static call/data proof, not a natural timed decay or environmental live trace. |
| Delayed bleeding / wound expiry | PlayerMetabolism.DoMetabolismDamage `06002ffa` passes Bleeding(6) and null attacker to Hurt at `0158`. WoundingTick `06001bae` passes null HitInfo to Die at `0052/008f`. | **New exact null boundaries**: the earlier injuring hit/action need not accompany terminal death. LastAttacker and wound-start analytics cannot restore original provenance. Nil optional hit metadata is legitimate on null-hit death. |
| Generic/plugin Hurt, copied hit, suicide | Hurt overloads propagate supplied attacker; CopyFrom propagates Initiator/Predicted; NPC transition Trigger `0600ae71` calls CopyFrom at `001b`. ConVar.Global.kill uses the same player as Hurt attacker (Windows `0600a0a7`, Linux `0600a0a9`). | An upstream public field or arbitrary supplied argument is not an original action certificate. Suicide would need its own command-source proof. Copies keep values, not authority; no new eligible source follows. |

The existing six ToPlayer implementations and their owner/current-controller
resolutions remain excluded. So do boomerang creator propagation, BatteringRam
supplied-source, HelicopterDebris self-source and TriggerHurtEx/NotChild selected
sources recorded in the origins/supplement evidence. The global direct-write
inventory covers their writes, but this continuation does not certify every
dispatch branch, vehicle weapon, NPC override or native producer.

## New terminal and pooling findings

The following are fresh interpretations of the pinned bodies, beyond the
already-settled producer inventory.

1. **Action provenance crosses distinct lifetimes.** CLProject's Note call
   belongs to launch; OnProjectileAttack's Predicted belongs to a later report.
   FiredProjectile has no original connection/epoch slot. CleanupExpiredProjectiles
   `06001a3f` removes dictionary keys at `0059` and frees original records
   at `0069`; EnterPool `06001c61` clears weaponSource/weaponPrefab at
   `0038/003f`, id at `00f3`, attacker at `00fa`. Record reference plus
   projectile ID is not a permanent identity, and cleanup is not connection
   retirement. The preserved absence of direct disconnect/ResetState clearing
   remains a negative inventory observation, not proof of an exploitable reconnect.
   A retained reference cannot turn a reused record into an old launch certificate.

2. **The full direct damage sink still has intervening mutation/virtual work.**
   BaseCombatEntity.OnAttacked(HitInfo) `060014c8` invokes component callbacks
   at `0039`, Hurt at `0069`, and base OnAttacked at `007c`.
   Hurt(HitInfo) `06001494` invokes virtual ScaleDamage at `0042`,
   component Hurt at `0156`, writes lastDamage/lastAttacker at
   `02fb/0307`, then virtual Die with the same hit at `0413`.
   Static dataflow proves a possible synchronous route, not successful death,
   immutable weights or trustworthy last-attacker history. Keep B2 full
   completion and exact hit/victim/source correlation; I12 does not waive it.

3. **Wound-start attribution does not survive as the terminal hit.**
   WoundedStartSharedCode `06001bb1` calls InitiatorPlayer at `0039` only
   for game-mode OnPlayerWounded at `0040`; that body establishes no original
   connection certificate. WoundingTick later calls Die(null). Metabolic
   bleeding creates a new null-attacker Hurt. Reusing those analytics/lastAttacker
   fields would silently substitute historical cause for current B2 meaning.

4. **HitInfo is a borrowed pool object, not a queued snapshot.**
   Explicit IPooled.EnterPool `060064d7` calls Clear at `0001`;
   LeavePool `060064d8` is a bare ret. IDisposable.Dispose `060064d9`
   calls Pool.Free<HitInfo> at `0004`. Clear `060064da` nulls Initiator
   at `0002`, WeaponPrefab at `0009`, Weapon at `0010`, Predicted
   at `003a`, HitEntity at `0048`, zeroes ProjectileID at `00b8`,
   and clears damageTypes at `0117`.
   PlayerAttack obtains a hit at `003a`, applies it at `0bd7`, and frees
   it on its normal path at `0bec`. OnProjectileAttack obtains a hit at
   `0026`, applies at `171c`, runs further callbacks/effects, and its
   finally disposes that hit at `180f`. Thus “RPC still active” is not a
   sufficient child-hit identity or pool lifetime certificate.
   The static Pool.Free call and explicit callback prove the disposal/clear
   contract surfaces; the external pool implementation and actual reuse/exception
   behavior remain live/implementation gates, not a new leak-freedom claim.

5. **Convenience damage has a different release boundary.**
   Hurt(float,type,attacker,protection) `06001493` gets a pooled HitInfo at
   `000c`, initializes supplied attacker/target at `0022`, calls Hurt at
   `0031` and frees at `0038`. The free is on the normal path; its finally
   disposes TimeWarning, not that hit. Do not claim guaranteed pool return on
   arbitrary exceptions or repair upstream behavior in this phase.
   A host call throwing after partial damage still cannot publish B2 completion.

6. **CopyFrom is not an immutable clone.** `060064db` copies host references
   and Predicted at `0067`; it copies array weights using the destination's
   mutable length at `019d`. The source array is neither shape-qualified nor
   length-matched by this loop. Retaining/copying this object for deferred
   provenance would carry references, variable work and possible array exceptions.
   CarbonLuau instead snapshots the already-bounded 28 weights/scalars inside
   the source borrow. No host CopyFrom call is recommended.

No HitInfo, FiredProjectile, array, weapon, actor, Unity wrapper or pointer enters
deferred transport. Existing synchronous frames may borrow host references and
must clear them on every CarbonLuau exit. Reference equality alone cannot certify
a pool generation after a release/reuse boundary; a future launch certificate
needs its own bounded original record incarnation and invalidation rules.

## Conceptual extension through the existing context

An expanded source, if separately authorized, can only fill existing optional
Killer/KillerId after stronger producer proof. Keep DamageType and Weapon
independent and preserve seven public fields, fourteen private fields, same
Signal, owner/publication authority and scheduler. A certified private original
action token may feed the existing observation; it is never a script capability
or an excuse to add a generic damage bus.

For a first direct client-projectile proof, capture original actor host identity,
original Network.Connection and existing D11 token at the exact CLProject-to-Note
boundary. Bind evidence to the actual original fired-record admission, weapon
and projectile identifier **plus a nonreused private launch incarnation**.
Match that exact record at the direct OnProjectileAttack application only.
Require original report connection equality, unchanged D20 victim birth,
same borrowed hit and unpoisoned source topology, with normal terminal completion.
Never infer launch from a final hit or reconstruct it from current account/owner
state. Client reports are server-validated claims, not independent proof of
physical ballistics or authenticated input.

This candidate deliberately keeps attribution nil if the original actor cannot
be validated at capture/hit admission. Disconnection **after certified capture**
may preserve the existing historical original ID and stale facade through later
delivery; reconnect must never retarget. Disconnection **before a proposed delayed
hit certificate is established** remains a gate and conservative nil.
Allowing a disconnected launch snapshot to identify a later delayed killer would
require an explicit source-specific semantics/qualification decision, not a
CurrentGameplayPlayer call on an account's replacement session.

Delayed explosives/fire need an original creator-action certificate bound to one
created D20 incarnation and each downstream child damage application. Restored
RF entities and owner-lookup rewritten hits lack that original certificate.
No persistence of action certificates across server restart/save-load is proposed.
Autonomous traps/turrets expose an ownership-versus-actor semantics question even
after perfect placement provenance; preserve nil until that question is separately
resolved. NPC/environment/decay paths remain actorless.

Any future retained launch evidence must have explicit global/per-actor admission,
byte, TTL, per-turn cleanup, child-fanout and lookup-work caps. Drop excess or
ambiguous evidence without replay; do not increase current event capacity.
Existing shared limits remain 128 captures/frame, 128 deliveries/frame,
32/domain/frame, 4,096 visits/frame, 512 reservations, 2,048-byte payload and
2-MiB transport budget. Numeric launch-record limits are intentionally not
adopted without measurements. Host FiredProjectile cleanup's LINQ/list traversal
is upstream behavior, not a qualified bounded CarbonLuau cleanup primitive.

## Rejected alternatives

- Final Initiator cast/current D11 alone, InitiatorPlayer/virtual ToPlayer,
  OwnerID/saved creatorID/current controller lookup, and callback-time UserId
  resolution: conflate a present person/owner with the original action connection.
- ProjectileID, Predicted, Bullet/Arrow labels or weapon class alone: public
  mutable/copyable values and a report connection do not certify launch lifetime.
- Ambient RPC/explosion scope for all nested hits: radial callbacks allocate
  distinct child hits and downstream callbacks can cause unrelated damage.
- Cached lastAttacker/lastDamage, wound-start analytics or retained copied HitInfo:
  substitute history, owner resolution and mutable pooled storage for producer proof.
- Reinterpreting a trap owner as actual attacker or adding Owner/Cause/Attacker
  fields: changes semantics/public API and remains outside this task.
- Global damage history, a generic hook classifier, another scheduler/bus or
  unbounded projectile/entity dictionaries: unnecessary breadth and unqualified
  resource/lifetime rules.
- More gameplay enabled on static evidence, a version bump or edited released
  artifact: backing IL is not loaded transformed source/topology or live provenance.

## Offline implementation and validation

The checker is concretely justified by two risks: stale/silent evidence substitution,
and confusing the launch/report/pool boundaries. It requires explicit local cache
and saved-evidence directories, verifies all nine input/receipt hashes before
loading only Mono.Cecil, walks metadata, inventories direct writers/record fields,
checks 18 exact callsites per platform, and checks selected method/token parity.
With `-VerifyReport`, it also compares exact signature/token/shape/hash ledger
rows against this report. It emits JSON to stdout; it has no output path,
file writer, network/remote call, build, runtime reflection-load, server launch or
patch installation. Project-owned identifiers use PascalCase; failures are
console-only `[Gameplay:AttributionOffline]` diagnostics.

Reproduction from the repository:

```powershell
& ./tools/Research-AttributionExpansionOffline.ps1 `
  -InputDirectory 'C:/Users/aiden/AppData/Local/Temp/CarbonLuau-Entity1A-25653776' `
  -EvidenceDirectory 'C:/Users/aiden/AppData/Local/Temp/CarbonLuau-B2-Attribution-20261011' `
  -VerifyReport
```

Use `-IncludeEdges` for call/field observations and `-IncludeInstructions`
with a focused `-MethodPattern` to inspect an unresolved body; neither flag
changes execution scope. Full vendor bodies should remain console/Temp evidence,
not copied into the repository. This report persists hashes/callsite conclusions.

Final checker SHA256:
`6b739c82e16a44bda80fe55abecdd70060f92219d54c76b3d109a1f4cc3df6f8`.
Final positive scan: PASS, 6,406 ms wall time in PowerShell on the controlling
Windows PC, inspecting both cached backing PEs. This is an offline measurement,
not a host build, in-game latency measurement or Linux process execution.

| Check actually executed | Result |
|---|---|
| Three pinned metadata inputs and six preserved IL receipt files | PASS, nine exact SHA256 matches before metadata inspection |
| Full direct-writer/field inventory | PASS: Windows 7,666 / Linux 7,668 types; 45,570 methods each; 19 Initiator and four Predicted stores each; no stored FiredProjectile Network.Connection field |
| Selected signature/token/body shape/hash parity | PASS, 52 methods on each platform |
| Exact launch/impact/fuse/turret/sink/pool/delayed-death sites | PASS, 18 assertions per platform, 36 total |
| Persistent report ledger compared to metadata | PASS, all 52 full signature/token/shape/hash rows |
| Missing metadata input and missing saved receipt | PASS rejection, exit 1 with bounded console error |
| Invalid method regex and empty method selection | PASS rejection, exit 1 with console error; no successful empty evidence result |
| PowerShell parser; report local links; owned-file whitespace | PASS, zero parser errors, ten existing local link targets, no trailing whitespace |
| Git diff whitespace and owned scope | PASS; only the two owned files authored here; concurrent release/other research changes are preserved |

The rejection cases were run through separate noninteractive PowerShell processes.
No tampered-PE execution, fault injection into a host, full native/managed matrix,
live-server test, remote build or CI was run. The hash guard was exercised with
the genuine pinned files; missing-file rejection does not itself measure a
tampered-file mismatch test. Tests do not certify production attribution.

A development checker failure initially assumed the historical prose's same
type count on Linux. Comparing the actual preserved SCAN_COMPLETE line and fresh
GetTypes enumeration resolved it without changing historical evidence. The final
checker pins separate counts. The initial failure is not a host/runtime failure
or expanded-attribution PASS.

## Remaining static evidence gaps

- No processed original method-byte/EH reproduction or Carbon injection inventory
  was generated for newly proposed launch, interface-impact or record cleanup
  fences. Backing IL-text parity cannot supply those future live-source pins.
- The exact accepted fired-record dictionary insertion/collision/replacement
  and expiry predicate, all callers capable of admission/retirement, and a
  nonreused certificate lifetime are not qualified by this path map. AE1 must
  resolve that focused dataflow before selecting an adapter. The existing
  no-disconnect-clear negative is retained, not replaced with a reconnect claim.
- The external Facepunch.Pool implementation was not inspected. Its referenced
  Free calls and HitInfo's explicit Clear callback are established here;
  precise pool admission/order, exceptional return and arbitrary nested reuse
  need further exact-source inspection and subsequent live gates.
- This is not an exhaustive dispatch/caller proof of every server projectile
  subtype, IProjectileImpact implementation, item mod, NPC/vehicle weapon,
  native producer or trusted plugin. Nor is there complete explosive/fire child
  lineage or action provenance across RF save/load/current-owner rewrites.
- No bounded launch-certificate model, memory/TTL/cleanup measurements or new
  processed-source regression fixture was implemented. They are the proposed
  private AE1 proof work, not hidden completed gates.

These are static investigation gaps, distinct from the physical provenance,
loaded topology, authenticated-client and lifecycle gates below. They do not
invalidate the unchanged existing melee qualification.

## Remaining Windows/Linux live gates

These gates are **required before future eligibility**, and none was run here.
They are not blockers for unchanged published melee support.

| Gate | Required evidence on each exact Windows/Linux host |
|---|---|
| Loaded launch/report methods | Processed original body hashes, actual Carbon hook injection and bounded subtype/interface topology; no arbitrary foreign patch admission. Preserve return/exception behavior and early rejection branches. |
| Original action -> record -> hit | Actual RPC launch and actual report through the selected weapon path; exact original connection/D11 token and fired-record incarnation, direct-hit/victim correlation, callback mutation and ordinary accepted/rejected host paths. Constructed direct Hurt/HitInfo alone cannot prove launch provenance. |
| Retirement and retarget negatives | Disconnect/reconnect same account before report, after capture/before delivery, ID collision/reuse, expired record, pooled record reuse, weapon destruction/transfer, source unload/reload, observer loss and server restart. No replacement-session certificate. |
| HitInfo borrow and child isolation | Real pool release/reuse, callback exception, Clear/CopyFrom mutation, nested same/different hits, item-mod splash before and effects/child hits after direct damage. No borrowed raw object reaches deferred work. |
| Completion composition | Existing D20/B2 full completion and B3 watch continuity, skipped/vetoed/throwing deaths, NPC repeat episodes and destruction/ID reuse; preserve original certified snapshots and no inline Luau. |
| Resource/lifecycle | Measured capped launch state, bounded cleanup and multi-projectile bursts; unchanged root/addon/provider publication, fatal recovery, cancellation, overload/fairness and at-most-once/no replay accounting. |
| Explosive/fire or owner semantics (later) | Actual placed/thrown/rocket origin, delayed fuse/impact/splash/child propagation, disconnected actor and save/load; explicit actor-vs-owner meaning first. No autonomous-origin claim from manually constructed Explosion/Decay labels. |

Authenticated-client firearm/ballistic behavior remains separately gated if
claimed. An offline/static or constructed-server RPC proof must be labeled
accordingly. Broad Windows/Linux native/VM/sanitizer and hosted final-source
checks become relevant only if production code is later changed; no new such
PASS follows from this report.

## Exact next phase

Recommended separately authorized phase:
**B2-AE1 — private launch-to-direct-projectile-hit provenance proof, eligibility
disabled**. Choose one exact BaseProjectile.CLProject path/effective weapon
implementation; initially exclude ammunition splash, RPC melee CLProject throws,
ServerUse, rockets, explosives, fire, traps, NPCs and delayed wound/metabolism death.

First resolve exact original fired-record admission/retirement callsites,
processed body/topology pins and bounded certificate identity in a private
offline model. Then, only under new live authorization, qualify actual
Windows/Linux launch/report, pool/retirement, source mutation and same-account
reconnect negatives with existing event transport. Deliver an evidence report
and explicit verdict before any production eligibility is opened. No public
field or production attribution change is part of that proof phase.
If original connection or record incarnation cannot be proved, retain nil and
record the failed premise; never use account lookup as fallback.

Only after AE1 succeeds may a separately scoped eligibility implementation/
qualification phase be considered under D20/D23. Explosive lineage and autonomous
owner semantics are separate later decisions. No new release, tag or identity
is implied by either phase.

## Exact selected metadata ledger

All rows below have equal full signatures, body sizes, instruction counts, EH
counts and IL-text SHA256 on the two pinned PEs. Tokens shown are Windows
backing tokens; the selected Linux tokens were observed equal here. This does
not generalize token parity to the whole assembly. Shape column is
bytes/instructions/EH. The ledger includes reused producer bodies for correlation
and the new launch, delayed, sink and explicit pool boundaries.

| Method | Token | Shape | IL-text SHA256 |
|---|---|---:|---|
| `System.Void AutoTurret::FireGun(UnityEngine.Vector3,System.Single,UnityEngine.Transform,BaseCombatEntity)` | `0x0600130a` | 522/190/0 | `2f669fb7366b8b3d62e066ceccc93d1b5f0f19a6c3d65e9ae691055a20dcb3f0` |
| `System.Void AutoTurret::ApplyDamage(BaseCombatEntity,UnityEngine.Vector3,UnityEngine.Vector3)` | `0x0600130b` | 197/69/0 | `1a40bd447ee1c33593a671c6ba149af8201141e2b274d4907f8e24dfaa216943` |
| `System.Void BaseCombatEntity::Hurt(System.Single)` | `0x06001492` | 16/8/0 | `68fb4f228df2f73eb8b8a031cfb677dbbb1d7b22b78fbb76919f5008c1b9ac1a` |
| `System.Void BaseCombatEntity::Hurt(System.Single,Rust.DamageType,BaseEntity,System.Boolean)` | `0x06001493` | 74/30/1 | `3aa897e12bc57cbb8daf762568302e1a68fd9387f849875a871865152c5d28b9` |
| `System.Void BaseCombatEntity::Hurt(HitInfo)` | `0x06001494` | 1136/387/1 | `f66f0f741244df553a3687c5df670c88517a15db56a88cdc8a782be28530a1eb` |
| `System.Boolean BaseCombatEntity::OnAttacked(System.Single,Rust.DamageType,BaseEntity,System.Boolean)` | `0x060014c5` | 60/28/0 | `b9a93075be3ce8b2be5d68b2d0db55c740c8d47b5f030dc7de77d8125daf0b60` |
| `System.Boolean BaseCombatEntity::OnAttacked(HitInfo,System.Boolean)` | `0x060014c7` | 50/20/0 | `efe10e963a055ba94209b8a7800578fa44555b4e09aab4d00fc2f73fb130ebc5` |
| `System.Void BaseCombatEntity::OnAttacked(HitInfo)` | `0x060014c8` | 130/54/1 | `d0f109a4196f16e7ce9ad98d12881ce00c53db9b4ada10da562cd14bfab08eef` |
| `System.Void BaseLauncher::SV_Launch(BaseEntity/RPCMessage)` | `0x06001767` | 752/267/0 | `c79256f78ccfffaa4214beb3ead5a71856172b225129fac46c69f9746762454e` |
| `System.Void BaseMelee::CLProject(BaseEntity/RPCMessage)` | `0x06001793` | 802/278/2 | `45d638f623f63acfcffc13afc0ba94e467022ae794a814b15fb68f4c4e0e2fa8` |
| `System.Void BaseMelee::PlayerAttack(BaseEntity/RPCMessage)` | `0x0600179e` | 3080/1177/3 | `6a1ce7c1d0499e1a875d49581d6c45992765c4755461320a085c13571c9e1787` |
| `System.Void BaseNpc::StartAttack()` | `0x06001837` | 186/65/0 | `833df0ed9225411bd482ec9c083e9586be040ed4a27019d69d46ece5d3fb806c` |
| `System.Void BasePlayer::OnProjectileAttack(BaseEntity/RPCMessage)` | `0x06001a3a` | 6176/2043/2 | `5b1b63d4b2977dea983ea8bf3b3491601ca15fec88e9e122791ef564213e88b6` |
| `System.Void BasePlayer::CleanupExpiredProjectiles()` | `0x06001a3f` | 136/43/1 | `9c540f633f2a1c7070f81401e0b23e54c347bf903c396b8b55849f25cd51962d` |
| `System.Void BasePlayer::NoteFiredProjectile(System.Int32,UnityEngine.Vector3,UnityEngine.Vector3,AttackEntity,ItemDefinition,System.Guid,UnityEngine.Vector3,Item)` | `0x06001a41` | 706/247/0 | `d258faf6fa76a34529dd93ca400d12239ab3917e285ba1326b96f9bd38a19d7c` |
| `System.Void BasePlayer::ServerNoteFiredProjectile(System.Int32,UnityEngine.Vector3,UnityEngine.Vector3,AttackEntity,ItemDefinition,Item)` | `0x06001a42` | 342/115/0 | `8f6ed37997f2a79dcdf3c568fc5772cb31988270612f6140b53ee9d26d2b7bee` |
| `System.Void BasePlayer::BecomeWounded(HitInfo)` | `0x06001ba8` | 94/42/0 | `f22741746faee0b41dd5e39f5bee99bb5c220604a2d067c596ca136028514d64` |
| `System.Void BasePlayer::WoundingTick()` | `0x06001bae` | 209/75/1 | `605a3b1209f0c2600cc2720b0d67f180af841f780ae45b9383ddb1f65ae0b696` |
| `System.Void BasePlayer::WoundedStartSharedCode(HitInfo)` | `0x06001bb1` | 82/28/0 | `84b3309a11cef5c7560f6430efd5205a38901ecbfc7de89556fe0e0336dc476a` |
| `System.Void BasePlayer::OnAttacked(HitInfo)` | `0x06001bf2` | 1069/365/0 | `9ef962003c413aa9d0736eb4052e1d265e520938004b1b0aefe26479144abf4c` |
| `System.Void BasePlayer/FiredProjectile::EnterPool()` | `0x06001c61` | 285/91/0 | `7215c7031d2c6c19c3aa99023355f79292053c24adf7b8046da58adc50ed8143` |
| `System.Void BasePlayer/FiredProjectile::LeavePool()` | `0x06001c62` | 1/1/0 | `a114be0d9fd9d9fa14abf290096752d5538200da0e18a1662c7aa01057686206` |
| `System.Void BaseProjectile::ServerUse(HeldEntityServerUseParams)` | `0x06001cec` | 1655/560/0 | `cdb83cb84a564a4bf952326e9b460305e0c39e9ef7b2fe976b370a1a9a65896b` |
| `System.Void BaseProjectile::AssignInitiator(HitInfo)` | `0x06001ced` | 39/14/0 | `2cd5c603480518c04b028dcb471877cb8f05eab8e9f1711ec9d41ffb8db58b70` |
| `System.Void BaseProjectile::CLProject(BaseEntity/RPCMessage)` | `0x06001d15` | 990/333/2 | `ded283d6bb640709f6befe2fe97e34c7a89e256c2f399e0f44408ea88d518c6a` |
| `System.Void BearTrap::DelayedFire()` | `0x06001f04` | 122/42/0 | `86aae9486f7644cf937bdb9f7a0942c176b1f9f69dce4e119391eba9d095fa52` |
| `System.Void DecayEntity::OnDecay(Decay,System.Single)` | `0x06002418` | 478/189/1 | `717493d58d07eabb6d842ee347d70c13c90275fa3bdf1c19210f65f97ddeb359` |
| `System.Void GunTrap::FireBullet()` | `0x06002820` | 409/136/1 | `94946cec621afd198f81fb57adc526a07d6c201613b36b19ef74609bb20bb860` |
| `System.Void Landmine::Explode()` | `0x06002ab5` | 133/52/0 | `5c28f01c151bf15c9fb696d41e808ed3bf7c6dd7ae74b772fe1563363d970a21` |
| `System.Void PlayerMetabolism::DoMetabolismDamage(BaseCombatEntity,System.Single)` | `0x06002ffa` | 616/205/0 | `79cfd5cebf5da7a4af63077aceb76a45209bd430fe77ca2d479544f33b8cdcff` |
| `System.Void RFTimedExplosive::Load(BaseNetworkable/LoadInfo)` | `0x060032ba` | 98/31/0 | `35e58f6cf15871676d8d03383f5ba51c93ecb50c8d292fbb3421a9115058ad32` |
| `System.Void ThrownWeapon::DoThrow(BaseEntity/RPCMessage)` | `0x06003760` | 177/71/0 | `f544c23e870346d3fb99efe6a94f4d40a0dba3f92cb0aef18ae404559b5cc079` |
| `System.Void ThrownWeapon::DoThrowImpl(UnityEngine.Vector3,UnityEngine.Vector3,BasePlayer,BaseEntity&,System.Single,UnityEngine.Vector3,Item,System.UInt32)` | `0x06003762` | 355/127/0 | `226759ae700c959910537d340178f72793e0f1ee61e573b11ce070b8a627eb9f` |
| `System.Void TimedExplosive::ServerInit()` | `0x06003774` | 95/31/0 | `311cea316725015b488a23ccc3c75ff84b670fad6f55a6e007489c2114c012a6` |
| `System.Void TimedExplosive::SetFuse(System.Single)` | `0x06003777` | 67/25/1 | `4b29118e95d2af784c0a4d99d9d30289c129dcf54037a909bcfe032f613a7a30` |
| `System.Void TimedExplosive::ProjectileImpact(UnityEngine.RaycastHit,UnityEngine.Vector3)` | `0x06003779` | 25/8/0 | `ab64d2d08dc7d9448a24a9131e6e4f258932016bc28ea16c4a563ae2763e7073` |
| `System.Void TimedExplosive::Explode()` | `0x0600377b` | 13/5/0 | `6b8094f5da22b732bdd785e2e5d965410cf447c01c92e45816085e9f201b5e59` |
| `System.Void TimedExplosive::Explode(UnityEngine.Vector3)` | `0x0600377d` | 1300/449/2 | `5d876a8d3c05fabe036f4cf3095025695520d850bb769c7370e2ff76a977c808` |
| `System.Void BeeTriggerHurt::ModifyHit(HitInfo)` | `0x0600430f` | 89/27/0 | `87abe1af7bf8899d17f94ec1665b1eec8039e15d9653024b3f41368c27a264fa` |
| `System.Void FireBall::DoRadialDamage()` | `0x060055b6` | 472/152/1 | `9aed5d9291fba6f31f44ef8047e01d2c7410c56703ce59d7afc94406ffa69162` |
| `System.Void BaseMetabolism`1::DoMetabolismDamage(BaseCombatEntity,System.Single)` | `0x0600561b` | 171/61/2 | `0f74c5a1b7b011dfc0cc7bde6fbb48801fe7c832ebf3a78643f5a8fbec444386` |
| `System.Void FlameTurret::DoFlame(System.Single)` | `0x0600573e` | 553/179/0 | `1f88086ca6e6821d67579427051ce76c06c5b8e256a5a73e725417693bf4ff2b` |
| `System.Void ServerProjectile::ProcessHit(UnityEngine.RaycastHit,BaseEntity,UnityEngine.Vector3)` | `0x06005b89` | 119/41/0 | `9c7e6bfe8d0c6df2bb7833a5415a3f0355faeecc78862fc68b052c214245fe25` |
| `System.Void HitInfo::Facepunch.Pool.IPooled.EnterPool()` | `0x060064d7` | 7/3/0 | `4a413dabb6a22b2133af2a1f6418af2aad9590e84fd1b701f7887ed2461bfacb` |
| `System.Void HitInfo::Facepunch.Pool.IPooled.LeavePool()` | `0x060064d8` | 1/1/0 | `a114be0d9fd9d9fa14abf290096752d5538200da0e18a1662c7aa01057686206` |
| `System.Void HitInfo::System.IDisposable.Dispose()` | `0x060064d9` | 10/5/0 | `6949ad9c03111325aa94cd227acb0f59e4fe796ebe03814012d1ef3ffdd73c5f` |
| `System.Void HitInfo::Clear()` | `0x060064da` | 310/106/0 | `ca4e98913960c8da242bdd08e9912b1eb4048071423609bee870e41384421429` |
| `System.Void HitInfo::CopyFrom(HitInfo)` | `0x060064db` | 459/161/0 | `cd579d0dfd505d8e6fd752ac9d88fdc775d32de772e0a77b901d1ce58160cac9` |
| `System.Void HitInfo::Init(BaseEntity,BaseEntity,Rust.DamageType,System.Single,UnityEngine.Vector3)` | `0x060064e0` | 63/24/0 | `4a8ac07643c2d239e7dbd80e5d36fa78bee50630867997ffeff013fe1c39e774` |
| `System.Void ItemModProjectileRadialDamage::ServerProjectileHit(HitInfo)` | `0x06006bee` | 663/212/1 | `7ff1c1ef0b74f8b4bdb8a40523ea87c32bc63a522ef32c59aab3f0e6fbc56fe5` |
| `System.Void DamageUtil::RadiusDamage(BaseEntity,BaseEntity,UnityEngine.Vector3,System.Single,System.Single,System.Collections.Generic.List`1<Rust.DamageTypeEntry>,System.Int32,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Collections.Generic.List`1<Rust.DamageTypeEntry>,System.Boolean,System.Boolean,BaseEntity)` | `0x0600813a` | 758/267/1 | `b63e837a4c0fa0cfdd67157b3ae6eaecae1d9bf642a152b704c885b69f90663f` |
| `System.Void Rust.Ai.Gen2.Trans_Triggerable_HitInfo::Trigger(HitInfo)` | `0x0600ae71` | 56/22/0 | `a94b3fd05807c4fcf8fb159c0d7fa34236e0d6fbfb1448c6882ddac9d8e87d60` |
