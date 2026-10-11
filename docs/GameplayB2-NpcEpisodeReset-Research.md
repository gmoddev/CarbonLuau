# Gameplay B2 NPC episode reset: narrow structural follow-up

Static research only. No host-core, patch, public contract, existing pin-array,
or runtime-test changes. The source baseline remains `6d23072`; host references
below describe the concurrently modified shared checkout inspected during this
follow-up, not a new committed implementation. D20 and D23 remain authoritative.

## Finding and smallest candidate

`BasePlayer.RespawnAt(Vector3, Quaternion, BaseEntity)` is the smallest existing
**complete respawn** boundary shared by all 20 NPCPlayer-family types (including
NPCPlayer itself) on the
pinned Windows/Linux Rust build `25653776`. It is nonvirtual; neither RespawnAt
nor Respawn nor InitializeHealth nor ResetState has an NPC descendant override
on these binaries. A's existing GameplaySpawnPatch already observes its full
original completion, but A's human admission prevents it from clearing an NPC's
B2 watch.

The proposed correction is a separate bounded B2 reset observation carried by
the existing GameplaySpawnPatch, even when the A GameplayFrame/PlayerLifetime
is absent. This is a research recommendation, not an implemented or live-qualified
NPC reset. It needs no additional Rust respawn patch target if that existing
carrier can be qualified for B2 without changing A's human contract/topology.

Do not use a base InitializeHealth return or a ResetState return as complete
respawn authority. The former precedes fallible outer RespawnAt work; the latter
does not set lifestate Alive. No new Spawn or removal proof is claimed here.

## Why the current watch misses a repeat death

[Gameplay lifecycle source](../src/CarbonLuau/CarbonLuau.GameplayLifecycle.Carbon.cs):
`CurrentGameplayPlayer` rejects `Player.IsNpc` and requires an exact connected
human PlayerLifetime. `EnterGameplay` returns null when that lifetime is absent.
`ExitGameplay` clears B2's DeathSeen/DeathPlayerToken/DeathPlayerEpoch only after
that human frame/lifetime survives validation and full RespawnAt progress.
GameplaySpawnPatch currently does nothing for a null A frame.

[B2 source](../src/CarbonLuau/CarbonLuau.EntityDied.Carbon.cs): NPC death carries
no A PlayerToken. `EnterEntityDeath` therefore deduplicates using
`Watch.DeathSeen`, which full death completion sets true. Its generic reset paths
currently recognize only DoRepair/SelfHeal. A successful NPC RespawnAt can leave
the same observed Spawn epoch/watch with DeathSeen still true, suppressing the
next completed death. This follows from the source; it was not newly run live.

## Exact IL and completion boundaries

All offsets, tokens, sizes, opcode/resolved-operand text, locals, MaxStack and EH
shapes below agree across the two backing assemblies and their selected-body
IPlayer-injection/Cecil serialization reproductions.

| Method | Token | Bytes | Relevant exact IL |
|---|---|---:|---|
| BaseCombatEntity.InitializeHealth(float,float) | `0x0600148f` | 22 | `_maxHealth` write `0002`; `_health` write `0009`; Alive(0) lifestate write `0010`; ret `0015`. No EH. |
| BaseCombatEntity.ResetState() | `0x060014b1` | 145 | virtual MaxHealth `0024`, set_health `0029`; ResetLifeStateOnSpawn=true `0058`; ret `0090`. No lifestate write or InitializeHealth call; no EH. |
| BasePlayer.ResetState() | `0x06001bd2` | 85 | base ResetState `0001`, then fallible disposal of eyes/inventory/collider wrappers; ret `0054`. No lifestate write, InitializeHealth or RespawnAt call; no EH. |
| BasePlayer.Respawn() | `0x06001abf` | 61 | FindSpawnPoint `0003`; direct RespawnAt call `0037`; ret `003c`. No post-RespawnAt work; no EH. |
| BasePlayer.RespawnAt(Vector3,Quaternion,BaseEntity) | `0x06001abe` | 618 | refusal ret `0018`; new GUID `0043`, N-format respawnId write `0055`; virtual InitializeHealth `017c`; full ret `0269`. No EH. |

RespawnAt has no IsNpc/connection prerequisite before its alive reset. Its
game-mode CanPlayerRespawn check is at `0011`. A refusal returns before the GUID
and health reset. After InitializeHealth, execution can still fail in loadout
or GiveDefaultItems (`01b3`/`01c3`), network update/RPC, analytics, game-mode
OnPlayerRespawn (`0232`), mission (`0258`) and injury-state (`0264`) work.
OnPlayerRespawned's already-recorded integration point `024f` also precedes the
last mission/injury tail. Therefore even new respawnId + Alive flags cannot
replace full original completion at `0269`.

The earlier full lifestate writer inventory remains pinned evidence: 12 direct
writes in 11 methods, with no lifestate address-taking path. No NPC descendant
declares a direct lifestate writer. InitializeHealth and BaseCombatEntity.Load
can establish Alive for a BasePlayer; ServerInit also has its own Alive write.
The existing loaded-state/initialization routes are not universal in-place
episode-reset completion authority. ResetState restores health without restoring
life state. Wound/revive is not a completed dead-to-new-life respawn.

The new reference scan retained exactly four InitializeHealth callsites:

| Caller | IL offset | Meaning relevant to this follow-up |
|---|---|---|
| BaseCombatEntity.ServerInit | `002b` | initialization, followed by its own Alive write |
| BasePlayer.RespawnAt | `017c` | shared full-respawn path |
| Planner.SpawnConstruction | `00e1` | construction initialization, not NPC episode authority |
| BaseModularVehicle.SetUpModule | `000d` | vehicle-module initialization |

There are nine direct BasePlayer Respawn/RespawnAt reference sites, including
Respawn's shared call to RespawnAt. None is declared on an NPC descendant. This
does not prove that trusted plugins or generic BasePlayer callers cannot invoke
RespawnAt on an NPC, nor that vanilla NPC death naturally reuses the object.

## Pins and evidence

These are **processed original method bytes before Harmony transformations**,
not raw backing-PE pins. Every selected method has zero EH clauses.

| Method | Windows processed SHA256 | Linux processed SHA256 |
|---|---|---|
| BaseCombatEntity.InitializeHealth | `7504f937bddd6835a5b11d3728a0dd45af9700b5c666a88f163636698b05c1b2` | same |
| BaseCombatEntity.ResetState | `00cbd1be10e3ecde40dc8c5709e88e7345f53d024371f602b3227481e5c4d974` | same |
| BasePlayer.RespawnAt | `ae7730d132edacf55b32d62947aeba9149cdac022862bc7266456d629d8d7f17` | `30f2bd0521a68902d2acce8687014f10f6f92938e4e2690576308311ab475bc5` |
| BasePlayer.Respawn | `017e5c86ec2d2421b8447d2af36a6a92488da19f5d4a274d71b4672dbefbf404` | `cdc8041f2b5b9fd88863a792333039a60d38ba95d8b2f51c1187378c1c68cb35` |
| BasePlayer.ResetState | `1c0f09617d87c86c26fdea6e022ae9b1007e0dad0d774e4888258b7569ef124c` | same |

RespawnAt hashes exactly match A's existing qualified live pins. The other four
are independently reproduced diagnostic pins; this follow-up gives them no new
live qualification and proposes no patch on them. Original bytes and exact
resolved IL/EH/local shapes are preserved externally under:

`C:\Users\aiden\AppData\Local\Temp\CarbonLuau-B2-NpcEpisodeReset-20261011-03`

That directory contains `NpcEpisodeReset.json`, `NpcEpisodeResetPins.tsv` and
the five original/processed raw bodies and decoded IL per platform.
[Research script](../tools/Research-NpcDeathEpisodeReset.ps1) imports only pure
metadata helper definitions from the existing reproduction tool; it does not
execute that tool's 67-target top-level run or load Rust through reflection.

The derived NPC set is ApartmentVendor, BanditGuard, BasePet, FrankensteinPet,
GingerbreadNPC, HumanNPC, LivestockVendor, NPCApartmentSecurity, NPCFarmAccess,
NPCMissionProvider, NPCPlayer, NPCShopKeeper, NPCSimpleMissionProvider,
NPCTalking, ScarecrowNPC, ScientistNPC, TunnelDweller, TutorialNPC,
UnderwaterDweller and VehicleVendor.

## Candidate completion rule and honest limits

Capture the exact NPC host/watch, original D20 witness and bounded entry
respawnId at RespawnAt entry, independently of human A admission. Require the
existing pinned source/body/topology, owner thread, original-run and successful
full-method finalizer/postfix proof, unchanged watch/incarnation/registry binding,
no poisoned/nested frame, a fresh N-format respawnId, Alive, finite positive
health and non-wounded state. Only then clear the existing watch's death latch
and Player death token/epoch fields. Do not publish PlayerSpawned for an NPC or
fabricate a D11 lifetime; this only rearms future B2 death observation.

The completion carrier should also accept a successful full alive reset when
the prior DeathSeen remains true: a previously throwing respawn may already have
left Alive state. Requiring entry Dead forever would prevent a later successful
full respawn from repairing that conservative suppression. Current flags alone
still never clear a latch.

This rule covers completed RespawnAt resets, including Respawn's forwarding
route. It does not cover arbitrary direct InitializeHealth, Load, manual
lifestate writes or ServerInit reentry on an unchanged Spawn incarnation. If B2
claims those episodes too, each needs independently qualified completion/reset
authority or explicit fail-closed coverage. If an NPC reset carrier cannot be
qualified, exclude the 20 NPCPlayer-family types from repeat-episode
B2 support; do not advertise their DeathSeen boolean as one latch per life.

## Regression candidate for Main

Use a task-owned NPC and an already-authorized fixture retention/veto mechanism
that allows completed death while keeping the original object keyed. No new
live fixture or patches were installed by this research.

1. Record the NPC, completed D20 Spawn epoch, witness/watch, ID, no A lifetime
   and initial DeathSeen=false. Complete one qualified death; assert one B2
   attempt and DeathSeen=true. Repeat Die while dead; assert no second attempt.
2. Invoke original RespawnAt with valid NPC fixture state. Assert a changed
   respawnId, normal original completion, Alive/positive health, unchanged
   object/ID/Spawn witness/watch, DeathSeen=false and no human PlayerSpawned.
3. Complete a second death on that same incarnation. Assert the second B2
   attempt; this fails under the present human-only reset path.
4. Negative: game-mode refusal at `0018`, skipped original, wrong thread,
   nested/poisoned respawn, changed witness/watch, and a task-owned exception
   after `017c` must not clear DeathSeen. The exception case specifically tests
   Alive/new respawnId despite failed outer completion.
5. After the throwing case, run a successful full RespawnAt again and verify
   rearming even if entry was already Alive. Then verify another completed
   death. No post-failure replay or ID-based replacement is allowed.

Static result: the proposed shared boundary is proved; the NPC reset carrier,
same-incarnation regression and Windows/Linux live behavior remain unqualified.
