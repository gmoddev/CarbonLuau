# Gameplay B2 exact-host attribution sidecar

Status: **STATIC BACKING-ASSEMBLY RESEARCH COMPLETE; NO API COMMITMENT OR LIVE B2 QUALIFICATION**.
Inspected on 2026-10-11 against main `6d2307207138ccb4eec9cd5005d920b1aea98fd7`.
Only this research note and `tools/Research-EntityDeathAttribution.ps1` are owned
by this sidecar. The main task owns the complete Die/OnDied death seam, original
D20 epoch and completion-time capture. This report does not qualify that seam,
its transformed live IL, patch topology, pooled HitInfo availability or delivery.

Read with [D23/I4/I12/D11/D20](Invariants.md),
[architecture research section 11](GameplayEventsArchitecture-Research.md#11-damage-and-kill-attribution),
and completed [A](GameplayFoundationA-Validation.md),
[B1](GameplayB1-Validation.md), [B3](GameplayB3-Validation.md),
[D2A](GameplayD2A-Validation.md) and [D3](GameplayD3-Validation.md).
These are inherited constraints/evidence, not additional implementation authority.

## Exact inputs and receipts

Cached input directory:
`C:/Users/aiden/AppData/Local/Temp/CarbonLuau-Entity1A-25653776`.
Rust build 25653776, protocol 2634.289.1, revision 166494 is the previously
qualified tuple; hashes were freshly measured and match A/B3/D2A.

| Input | SHA256 | MVID/version |
|---|---|---|
| `Assembly-CSharp-win.dll` | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e` |
| `Assembly-CSharp-linux.dll` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` | `616082a0-36f4-4680-a1ab-efc5766796cc` |
| `Mono.Cecil.dll` | `0c6798722b72899c54648e58a950a945163f6e2d6ebbc1a1e11c6f771cdaed0f` | Cecil `0.11.6.0` |
| New research script | `4a12441669803ff0ed09498db67821f6f2ef65cc1abad5c2ae6d4ce2bfea4823` | Metadata inspection only |

Previous D2A artifacts were located in
`C:/Users/aiden/AppData/Local/Temp/CarbonLuau-D2A-20261011`:
`backing-methods.log` SHA256
`14c5e98c75777deecf4b760acd088d37b6f221e437d1a4b8dab5d1bdb0f45e2f`,
`backing-semantic.log` SHA256
`4e2af781812f921410d170fefe72b80fba773adf5e83d1aeaacba69854a73d59`.
Their input identities agree. Payout architecture was not reinvestigated.

New external receipt root:
`C:/Users/aiden/AppData/Local/Temp/CarbonLuau-B2-Attribution-20261011`.
`initial-accessors.log` uses the existing `Research-GameplayDeathHost.ps1`.
Four new scanner passes (`inventory`, `origins`, `representation`, `supplement`)
each contain `Assembly-CSharp-{win,linux}.il.txt` and `.methods.tsv`.
The IL receipts record actual method/field references and nearby writes, selected
full bodies, tokens, attributes, body bytes, instructions, EH ranges and hashes.
Raw vendor IL stays in Temp; none is copied into the repository.

| Pass | Windows IL receipt SHA256 | Linux IL receipt SHA256 |
|---|---|---|
| inventory | `3257c9e52a6099abbb62c858c881635bf0f9738db1492ddaa3f155ffb3a4657e` | `a3afa09fa889346380d2ee4f4685f97f6f32e18848429fd6916e4083e8a4878b` |
| origins | `de81999768caabcfcd21b4407cb553c5962d77da2cc7534c94a3f26d4c99fae1` | `6ac070258c4b1302246a108ea5e681907b8e656726566974187acc5c1565bcbc` |
| representation | `01d8c49d207386137958277f1141706d4b6586083e51a234fc52807010cae6e9` | `ed92bf57bb89299034538890fd1b1535b068284895af0ed6f7b2a14d93c039bb` |
| supplement | `38ec654563b2e74a6c7e97adee34c4db5dc9f83a0aa5059861189b1537db339a` | `9ac389278ceecc8c80cc0b850b2be7e7f65157789e9fb703b48fa0be877b3652` |

`verification.txt`, SHA256
`ffe36962d040952e1d97e1bdd72536da790c44548bcd4d91c841bd52be70de50`,
records all TSV hashes, input/script hashes, parity and guard checks.
Each pass traversed 7,666 types / 45,570 methods per platform. The inventory
found 724 matching references in 380 methods, including all 19 direct
`stfld HitInfo.Initiator` sites in this assembly. This is a managed static
inventory, not complete dynamic coverage of external plugins/native producers.
The other passes selected 277, 260 and 129 methods respectively. Every matched
method has identical IL-text SHA256, body size, instruction count and EH count
across platforms; EH ranges are available in raw receipts. Tokens differ in some
ConVar/analytics methods and must not be treated as cross-platform constants.
IL-text SHA256 means UTF-8 Cecil instruction strings joined by LF, without a
trailing LF; it is not a live raw-byte hash or a publicizer-normalized hash.

## Direct field versus virtual attribution

| Source | Exact backing metadata, both platforms | Interpretation |
|---|---|---|
| `HitInfo.Initiator` | Public `BaseEntity` field `0x04004fa7` | One direct field read; no `get_Initiator`, owner walk or virtual dispatch |
| `HitInfo.WeaponPrefab` | Public `BaseEntity` field `0x04004fa8` | Object reference; not a string, hash, item or guaranteed prefab template |
| `HitInfo.Weapon` | Public `AttackEntity` field `0x04004fa9` | Attack component reference; not a weapon short name or Item |
| `HitInfo.damageTypes` | Public `Rust.DamageTypeList` field `0x04004fc6` | Mutable hit damage weights |
| `DamageTypeList.types` | Public `Single[]` field `0x0400a0a1` | Replaceable array; usual length 28 is not an enforced field invariant |

`HitInfo.InitiatorPlayer` (`0x060064dd`, 27 bytes / 10 instructions / 0 EH;
IL-text SHA256 `48a9a817a253feea28b35a441167ae635e683990ba5a4c67b64add301361258a`)
is itself nonvirtual, but uses Unity's implicit validity test and then virtual
`BaseEntity.ToPlayer()`.
It does not prove a direct human initiator or bound the called override's work.
The complete six-definition ToPlayer inventory is:

| Definition | Token; bytes/instructions/EH | Actual behavior |
|---|---|---|
| `BaseEntity.ToPlayer` | `0x060015b6`; 2/2/0 | null |
| `BasePlayer.ToPlayer` | `0x06001bb8`; 2/2/0 | this, including NPC subclasses |
| `AttackHelicopterRockets.ToPlayer` | `0x0600127b`; 22/9/0 | owner vehicle's passenger |
| `AttackHelicopterTurret.ToPlayer` | `0x06001295`; 22/9/0 | owner vehicle's passenger |
| `MountedWeapon.ToPlayer` | `0x06002daf`; 35/17/0 | owner seat's currently mounted Player |
| `Drone.ToPlayer` | `0x0600262f`; 105/42/0 | controller cache, with `FindByID` / `FindSleeping` fallback |

The direct-field cast used by Gameplay A is a necessary identity check, not
sufficient original-action provenance for B2. Read the same death-associated
HitInfo's Initiator once, require a tracked connected non-NPC human, and verify
exact D11 managed identity, connection identity and UserId at capture. The latest
strict B2 recommendation additionally requires the exact producer witness in
the original-connection section below. Save only token and bounded identity
strings. Missing identity or missing producer provenance yields nil for both
killer fields, even when the current Initiator happens to be a valid human.
Do not fill KillerId alone from OwnerID, saved creatorID or a later lookup.
Captured killer disconnect may stale the original facade but must never retarget.

Latest main-task capture plan: freeze scalars before OnDied can invalidate the
victim/source, then require normal terminal completion and reference identity of
the original HitInfo and its original Initiator at completion. Reuse
CurrentGameplayPlayer for the direct D11 capture; do not call InitiatorPlayer.
If original-source correlation fails, or hook mutation leaves attribution
unknown, omit attribution. A legitimately captured actor-ID snapshot survives
later actor disconnect; its captured Killer may be stale with IsConnected=false.
This is not an ID-only fallback for an actor that never had a proved original
D11 capture. Reference equality is a correlation gate, not proof that every
mutable HitInfo scalar remained unchanged or that owner-derived provenance is
an original action connection. The proposed 14-field internal blob does not
itself authorize or prove any public field.

The direct field plus CurrentGameplayPlayer alone proves **the human directly
stored in this hit at capture**. It does not
universally prove the original shooter/placer connection: producer-side owner
resolution can already have occurred before this field is read. If B2 requires
that stronger causal meaning, omit attribution for paths lacking separate
original-connection provenance; field type plus D11 alone cannot establish it.
The broader recorded-final-initiator interpretation is not the latest requested
B2 Killer contract and must not be substituted as an implementation fallback.
No production correction to A is claimed or implemented by this research.

## Concrete producer origins and call references

Tokens below are equal on Windows/Linux except the explicit suicide pair.
These are measured writes/calls in real class methods, not hook-name inference.

| Family and method | Token; body bytes/instructions/EH | Source writes or calls; attribution consequence |
|---|---|---|
| Human melee: `BaseMelee.PlayerAttack` | `0x0600179e`; 3080/1177/3 | RPCMessage.player -> Initiator at `004f`; this -> Weapon `0056` and WeaponPrefab `005d`. Candidate eligible only with the exact RPC connection/callsite witness below. |
| Reported projectile hit: `BasePlayer.OnProjectileAttack` | `0x06001a3a`; 6176/2043/2 | this -> Initiator `0049`; FiredProjectile.weaponSource -> Weapon `031b`; weaponPrefab -> WeaponPrefab `0331`. |
| Server projectile weapon: `BaseProjectile.ServerUse` / `AssignInitiator` | `0x06001cec`; 1655/560/0; `0x06001ced`; 39/14/0 | ServerUse calls AssignInitiator at `0436`. AssignInitiator writes GetOwnerPlayer at `0007`, falling back to parent entity at `0021`. No universal direct-human guarantee. |
| Turret: `AutoTurret.ApplyDamage` | `0x0600130b`; 197/69/0 | this -> Initiator `0062`; placed-owner account is not a direct human source. |
| Bear trap: `BearTrap.DelayedFire` | `0x06001f04`; 122/42/0 | HitInfo constructor at `0037` receives this as initiator; Bite plus Stab weights. Owner attribution is nil. |
| Shotgun trap: `GunTrap.FireBullet` | `0x06002820`; 409/136/1 | HitInfo constructor `00fc` receives this, target, Bullet and damage. Owner attribution is nil. |
| Landmine: `Landmine.Explode` | `0x06002ab5`; 133/52/0 | RadiusDamage at `005d` receives this as initiator and LookupPrefab as weapon prefab. Owner attribution is nil. |
| Flame trap: `FlameTurret.DoFlame` | `0x0600573e`; 553/179/0 | RadiusDamage `00eb`/`0138` receives turret as initiator; spawned fireball.creatorEntity is assigned this at `021c`. Nil human attribution. |
| Explosive: `TimedExplosive.Explode(Vector3)` | `0x0600377d`; 1300/449/2 | creatorEntity + LookupPrefab passed to RadiusDamage `020f`; additional direct HitInfo Initiator writes `02f0`, `038c`, `03d6` use creatorEntity. Creator may be human, NPC, indirect or null. |
| Saved RF explosive: `RFTimedExplosive.Load` | `0x060032ba`; 98/31/0 | Saved creatorID -> creatorPlayerID; `FindByID` at `0057` -> creatorEntity at `005c`. Original placing connection is unproved, even if later field type is BasePlayer. |
| Thrown weapon origin: `ThrownWeapon.DoThrowImpl` | `0x06003762`; 355/127/0 | Player argument passed to SetCreatorEntity `0095`; SetCreatorEntity `0x06001544` (8/4/0) stores the reference. No connection token/history is stored there. |
| Fire: `FireBall.DoRadialDamage` | `0x060055b6`; 472/152/1 | Initiator `008f` is creatorEntity if Unity-valid, otherwise this fireball's BaseEntity. Fire category alone never proves a human. |
| Bee damage: `BeeTriggerHurt.ModifyHit` | `0x0600430f`; 89/27/0 | Initiator.OwnerID -> FindByID -> Initiator `0033`; WeaponPrefab `0053` from grenade prefab component. Current-account lookup is not original actor connection proof. |
| Boomerang: `ThrownBoomerangServerProjectile.OnHit` | `0x06004315`; 436/145/0 | creatorEntity -> Initiator `0021`; LookupPrefab -> WeaponPrefab `002d`. No ID-to-human inference. |
| NPC attack: `BaseNpc.StartAttack` | `0x06001837`; 186/65/0 | Hurt at `006f` receives this NPC as attacker; nil human attribution. NPC projectile weapons also use server weapon paths. |
| Fall: `BasePlayer.ApplyFallDamageFromVelocity` | `0x06001ab4`; 205/76/0 | Hurt `0085` receives Fall and null attacker; nil. |
| Hunger/thirst: `BaseMetabolism<T>.DoMetabolismDamage` | `0x0600561b`; 171/61/2 | Hurt `0044`/`0099` receives category 1/2 and null attacker; nil. PlayerMetabolism `0x06002ffa` (616/205/0) adds other null-attacker metabolism damage paths. |
| Decay: `DecayEntity.OnDecay` | `0x06002418`; 478/189/1 | Hurt `01cc` receives Decay and null attacker; nil. |
| Suicide: `ConVar.Global.kill` | Win `0x0600a0a7`, Linux `0x0600a0a9`; 128/54/0 | Hurt `0060` receives Suicide and the same Player as attacker. An independently qualified command-source witness could prove self attribution; omit in the proposed melee-only slice. |
| Radial helper: `DamageUtil.RadiusDamage` | `0x0600813a`; 758/267/1 | first argument -> Initiator `01cd`; second -> WeaponPrefab `01d5`. Caller semantics determine provenance. |
| Trigger damage | `TriggerHurtEx.DoDamage` `0x06007af4`; 379/123/1; `TriggerHurtNotChild.OnTick` `0x060067f5`; 768/268/1 | Ex uses its GameObject's BaseEntity at `00f8`; NotChild writes its selected V_7 source at `027b`, V_8 WeaponPrefab at `0273`. Neither proves a human by method name. |

The remaining direct Initiator writes are BatteringRam.DamageEntities
`0x06001eca` (279/100/0), supplied entity argument at `0048`;
HelicopterDebris.OnAttacked `0x0600579e` (210/69/0), this at `005a`;
HitInfo.Clear `0x060064da`, null; CopyFrom `0x060064db`, supplied hit's field;
and Init `0x060064e0`, supplied initiator at `0002`.
Init is 63/24/0 with IL-text SHA256
`4a8ac07643c2d239e7dbd80e5d36fa78bee50630867997ffeff013fe1c39e774`.
CopyFrom copies host references and is not a safe retained event snapshot.

## DamageType and weapon representation recommendations

`DamageTypeList.GetMajorityDamageType` is `0x0600a4aa`, 62 bytes /
37 instructions / 0 EH; IL-text SHA256
`2582b32b27d9e5898259421c36b6051d2aa670184e89e204036b179d88c740e1`.
It starts with Generic index 0 and maximum 0, scans `types.Length`, skips NaN
and infinity, and updates on equal or greater weight. Thus positive ties select
the highest numeric index. All-zero ordinary arrays return **Cannon (27)**;
all-negative/all-nonfinite or empty arrays return Generic (0). `LAST` is 28,
an enum sentinel rather than a damage slot. Constructor `0x0600a4b0`
(20/7/0; SHA256 `9c15be27f7f30b6f72cace02d0591e74096b45fa928149841dfeac6bf225c457`)
allocates 28 Singles, but the public field can be replaced by an arbitrary-length
array. Calling the getter without a shape premise does not provide a hard bound.

If separately accepted, a precise optional DamageType projection is: at the
main-owned death capture boundary, read damageTypes and its array once into
borrowed locals; require length exactly 28; inspect exactly 28 weights; choose
the highest-index greatest **positive finite** weight; map only 0..27 through a
fixed table of exact qualified category names. Return nil for absent/wrong-shaped
storage or no positive finite weight. This intentionally avoids the getter's
zero-weight Cannon result. It is the observed hit's greatest damage-weight
category, not ultimate cause, a strict >50% majority, accumulated damage history,
actual health lost, responsible actor or weapon identity. Do not use Enum.ToString
on unvalidated values, reflect/iterate arbitrary arrays, clone the hit/array or
retain either reference. I12 allows observation races, not unsafe/unbounded reads.

Weapon representations are not uniform. Melee writes the live attack component
into **both** Weapon and WeaponPrefab. Player projectile hits copy the two
AttackEntity fields of FiredProjectile (`0x04000935`, `0x04000936`). ServerUse
writes this to Weapon and FindPrefab(PrefabName).GetComponent<AttackEntity>()
to WeaponPrefab. Explosives/radial sources can supply only WeaponPrefab.
These are not Item definitions, ammunition names, translated display labels,
inventory identity or a guaranteed original equipped weapon.
BaseNetworkable.PrefabName `0x0600519e` (32/11/0; SHA256
`06ae2bb8a0b70cae8f7f2f3bcc5f9dae6f5a8e4c8c4b88654208a8937827a7dd`)
can lazily call StringPool.Get(prefabID) and mutate its cached string.
Representation inspection alone does not qualify that entire call graph or a
new prefab resolver at the death boundary.

| Candidate field | Recommendation for the main task |
|---|---|
| `Killer`, `KillerId` | Exact qualified original-action producer witness plus original D11 capture only. Field-human/current-D11 alone is insufficient; no witness means nil together. |
| `DamageType` | Candidate proven label source: fixed mapping of the 28 qualified categories through the bounded projection above, <=32 UTF-8 bytes, ordinary nil. No majority getter invocation. |
| `Weapon` | Candidate scalar only: full prefab label from captured HitInfo.WeaponPrefab's qualified `_prefabName` field, <=512 UTF-8 bytes; missing/cold/unproved label is nil. No item/short-name or last-attacker classifier meaning. See the follow-up below. |
| `WeaponPrefab` | Omit initially. A later scalar could mean only canonical full prefab path of the provided source component, with an independently bounded validity/path reader and nil on failure; never a live Entity/weapon proxy or inferred item. |
| `Cause`, `Damage`, `Attacker: Entity`, `Headshot` | Omit; no general causal, health-loss, live attacker-lifetime or anatomical proof follows from this sidecar. |

## Bounds, retention and validation limits

The proposed direct attribution read is constant work; the proposed damage
projection has 28 element reads and fixed name selection. All host access must
occur on the I4 owner thread with main-owned completion/original-incarnation
proof. No virtual InitiatorPlayer/ToPlayer, owner/account traversal, prefab search,
historical damage lookup, new observer or scheduler is recommended.
Deferred transport receives only immutable bounded scalars, the original opaque
Player token/identity snapshots and already-qualified victim scalar snapshots.
No HitInfo, damage array, PlayerLifetime object, BasePlayer/BaseEntity/AttackEntity,
Unity reference, strong wrapper, delegate closure over them or new GCHandle is
retained in the payload. Existing directory ownership is separate from payload
retention. Any borrowed frame locals must be cleared on success/error/skip;
delivery never reacquires a killer by UserId or an attacker by network ID.
Reuse existing shared event payload/reservation/fanout bounds if fields are
later authorized; do not derive new budgets from this research.

Verification: PowerShell syntax parsed with zero errors; all four exact-host
scanner passes completed on both files and selected-body parity passed.
The scanner rejects repository raw-output destinations and existing receipt
paths, both exercised without overwriting evidence. It loads only Mono.Cecil,
reads Rust as metadata and disposes assembly readers/writers. It never executes,
patches or rewrites a host assembly. No live host changes, server launches,
production builds, API/version changes, commits or canonical document edits.
No persistent task process remains. Static evidence cannot certify ordinary
runtime producer reachability, trusted plugin mutations, a live transformed-body
gate, original shooter history or authenticated-client behavior.

For reproduction, run the new script with both cached AssemblyPath values,
the cached CecilPath and a fresh Temp OutputDirectory. Its default pass is the
inventory above. Each receipt's first four lines preserve the exact TypePattern,
MethodPattern, ReferencePattern and InventoryOnly options for the other passes.
All field/method tokens are backing tokens; do not assume Carbon publicization
preserves operand indexes or substitute these hashes for main's live-body proof.

## Main-task follow-up: labels and actual death-hook return behavior

The additional `labels` pass proves BaseNetworkable._prefabName's exact private
String field (`0x04003b7a`) on both backing targets. It finds one direct stfld
writer in this assembly: get_PrefabName `0x0600519e`, which copies
StringPool.Get(prefabID) into that field at `0014`. An uninitialized field is
null. This supports a **cache-only scalar observation**, not a cold getter call
or guaranteed availability. Main can pin/reuse the existing private field reader
established by Entity discovery; do not invoke the full ReadDiscoveryEntityEvidence
function on prefab templates, because its network/realm lifetime checks concern
live entities. No source Entity proxy or spawned-epoch claim follows from reading
this component's cached string.

For the proposed Weapon label, pick one explicit source: the component held in
the original HitInfo.WeaponPrefab field before OnDied. Read only its pinned
_prefabName String field, capture the exact string without normalization, and
validate full canonical `assets/... .prefab` path semantics and size. Check
UTF-16 length <=512 before strict UTF-8 byte counting, then require <=512 bytes;
reject missing/empty, invalid encoding, controls/NUL, noncanonical or truncated
labels. Every string scan follows the length precheck. A known scalar from a qualified source is retained; a missing/cold or
unproved one is nil. Do not use ShortPrefabName, LookupPrefab, FindPrefab,
GetOwnerItem, last-attacker history or inferred item/ammunition names as fallback.
WeaponPrefab can be a live melee component or a prefab component; the field's
captured full-prefab label is the consistent meaning, not proof of an equipped
weapon or inventory Item. This is a static bounded-source recommendation;
main must still qualify reader setup, source validity and frame cleanup.

The 28 exact DamageType names are ASCII with maximum length **17**, so a fixed
validated map fits the proposed 32-byte field. In numeric order 0..27 they are:
Generic, Hunger, Thirst, Cold, Drowned, Heat, Bleeding, Poison, Suicide, Bullet,
Slash, Blunt, Fall, Radiation, Bite, Stab, Explosion, RadiationExposure,
ColdExposure, Decay, ElectricShock, Arrow, AntiVehicle, Collision, Fun_Water,
BeeSting, Paintball, Cannon. No name is inferred from weapon/initiator class.
The label pass has seven selected method summaries, identical including tokens
across both hosts. Receipt hashes: Windows
`c3d6af234d2d3e95d37b1d6d07edb5ea3c28ff21b1a8fba03ba5d641a2ab2961`,
Linux `f58d9f502a369ddfe9a4e7428b729a4e73764b3176094050e1e2f5600a82e127`;
both methods.tsv hashes are
`75ffce3152abce7267789cd0793b98497083220d72597eb787a856f73a6e1d4d`.
`followup-verification.txt`, SHA256
`34e870f9f5db2c32d15cd3c55780af15b3ebf308971aa096e99d2ceec2fc4d64`,
records label-pass parity, follow-up receipt hashes and the narrow conclusions.

The same external root contains `death-hook-veto.il.txt`, SHA256
`be073ce3f697743260dc40fdcfcf7f501f876cb94197af2f98aebaa62b10c702`,
produced by the existing Research-GameplayDeathHost tool with both Rust inputs
and both cached Carbon.Hooks.Oxide inputs. Oxide input hashes are Windows
`71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d`,
Linux `b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764`.

**Neither inspected OnEntityDeath injection implements a return-value veto.**
The BaseCombatEntity hook owner identifier is
`165ca5e2aedc4e16bb94f45744faead3`, targeting Die(HitInfo). Its generated
MoveNext is `0x06000b91`, 580 bytes / 202 instructions / one fault handler,
identical on both platforms with IL-text SHA256
`67de3e24867097976bbacc266342fa230db8f2b8bdc976e322d8e358a4376205`.
It inserts at backing instruction index 15 (offset `0028`), emits arguments and
the HookCaller call, then an emitted **Pop** (generator IL `01ce`). It does not
branch on the returned hook value or emit a death-method early return.
The ResourceEntity.OnDied OnEntityDeath injection
`e29a6af5261444aa96d48c13fde8cc9d` likewise emits Call then Pop (generator
IL `01d8`); MoveNext `0x06000cb1`, 590 bytes / 205 instructions / one fault
handler, SHA256
`d2f730a86e1b836a1d0acc25393812248544eeaf007e6ce7172a0fc434c73276`.
Hook metadata also specifies System.Void. This is not the vetoable OnPlayerDeath
path. Trusted hook side effects/throws or other Harmony patches remain separate
from an ordinary returned-value veto; no universal noninterference claim follows.

Backing BaseCombatEntity.Die is `0x0600149b`, 337 bytes / 116 instructions /
one finally handler (try 308..324, handler 324..336), identical SHA256
`4a0dc724d9bbe35b5da42d935ac4714afac2c7b30c4bd14e74c9e53133b83186`.
It returns early if already dead; otherwise health is set to zero, lifestate is
written at `0023`, and full virtual OnDied is called at `013d` after TryDropCorpse
at `0136`, with final return `0150`. This agrees with main's stated base marker
pattern. The early hook after the lifestate write is not terminal-completion
authority. Full normal base/outer completion and original-lifetime proof remain
main's gate; source offsets will shift under injection/publicization and must be
matched semantically against the actual live body.

## Original action connection: smallest defensible eligibility proposal

**Supersedes the earlier field-human-only candidate for B2 Killer eligibility.**
No production tracking or new observer was implemented. Main can ship the
optional context with Killer/KillerId nil for every unqualified source; this
does not block independently proved optional DamageType/Weapon scalar labels.
Original action connection is not interchangeable with a currently connected
human whose reference an owner lookup inserted into the final hit.

Two additional exact backing passes were saved under the same Temp receipt root:

| Pass | Windows IL SHA256 | Linux IL SHA256 |
|---|---|---|
| connections | `d52719be0227996218629fda881d307f196709c47b5502cca34d9a780c3bbe75` | `c9260eae67bb60176c2ddab19a08d5007dc515a2f1c2bc356d240c4781f3e052` |
| copy-and-dispatch | `dec3d1d3216d75898d727ae40e5f1ef193dc5394bb3c96b4adbaff1df506aa49` | `4962d95dba0fc9fcaebac61e1ea3d4d44294c0d731ac61647021fb3c2a69a76e` |

Selected method sizes/instructions/EH counts and IL-text hashes agree across
platforms: 759 connection-pass methods and 24 copy/dispatch-pass methods.
`connection-verification.txt` SHA256
`3c4988cc8c8b228102ce430fa9af5879ad47a01d60b774bcfb68a0ace21e5279`
records all four TSV/IL hashes, parity and the narrow conclusions.

### Measured connection chains

| Producer | Actual hit/connection chain | Strict original-action verdict |
|---|---|---|
| RPC melee `BaseMelee.PlayerAttack` | RPCMessage.player (`0x04000529`) -> local actor -> Hit.Initiator at `004f`; RPCMessage.connection (`0x04000528`) -> Hit.Predicted (`0x04004faf`) at `0069`. Same local HitInfo is passed synchronously to virtual DoAttackShared at `0bd7`. | Small candidate with an exact callsite witness. Snapshot and validate the RPC's connection, not merely actor.Connection later. |
| Client projectile report `BasePlayer.OnProjectileAttack` | this -> Hit.Initiator `0049`; RPCMessage.connection -> Hit.Predicted `009e`; original hit reaches HitEntity.OnAttacked at `171c`. Item-mod callbacks at `16b4`/`16de` precede that call. | The report RPC connection is evidenced; original launch connection remains unproved. Omit from the first strict slice. |
| `NoteFiredProjectile` / `ServerNoteFiredProjectile` | `0x06001a41` (706/247/0) and `0x06001a42` (342/115/0) write this into FiredProjectile.attacker (`0x0400094b`) at `0297` / `0142`; weapon component fields are also stored. | FiredProjectile has no stored Network.Connection or connection-epoch/token field. An attacker object, projectile ID, weapon parent or current player directory cannot replace that missing history. |
| `BaseProjectile.ServerUse` | AssignInitiator uses GetOwnerPlayer, with parent fallback; no RPC Predicted assignment. | No original human action-connection certificate. Nil, including NPC/server weapon paths. |
| Timed/RF explosives, fire and thrown projectiles | creatorEntity is copied; RF Load reconstructs it through saved-ID FindByID. | Creator reference or saved owner ID does not certify original action connection. Nil. |
| Bee trigger | Initiator.OwnerID -> FindByID -> Initiator; source can now be a reconnected account session. | Nil even if final field is a human and CurrentGameplayPlayer succeeds. |
| Turrets/traps/radial/trigger sources | Physical entity, creator or supplied argument is recorded. | No original human RPC certificate. Nil. |
| Environment, delayed bleed/wound completion, generic/plugin Hurt and death calls | No eligible synchronous original melee application witness established here. | Nil. No last-attacker or cached-human fallback. |

The complete backing assembly has only four direct Predicted writers:
PlayerAttack, OnProjectileAttack, HitInfo.Clear (null at `003a`) and
HitInfo.CopyFrom (source Predicted at `0067`). Predicted is public, mutable and
copyable; it is a corroborating connection reference, not a signed producer tag.
One static CopyFrom caller appears: Rust.Ai.Gen2.Trans_Triggerable_HitInfo.Trigger
`0x0600ae71`, at `001b`. It carries references into NPC transition state;
the existence of a matching Predicted field alone cannot certify the original
application or justify attribution to a later copied hit.

The firedProjectiles field-reference inventory contains constructor allocation,
note/report/update/ricochet operations and CleanupExpiredProjectiles; it does not
show direct clearing in PlayerInit/ResetState/disconnect methods. This is a
negative inspection result, not proof that every conceivable host path preserves
records across reconnect. No complete connection-bound launch-to-hit record
lifetime was qualified here. A pure Bullet label plus matched Predicted/report
connection therefore must not be advertised as original shooter proof.

ItemModProjectileRadialDamage.ServerProjectileHit `0x06006bee` (663/212/1;
SHA256 `7ff1c1ef0b74f8b4bdb8a40523ea87c32bc63a522ef32c59aab3f0e6fbc56fe5`)
creates new HitInfo instances at `022c`/`025b` and applies them via OnAttacked
at `0231`/`0260`. An outer projectile RPC context must not bless these other
hits, even if they contain a player Initiator or a familiar damage category.

### One temporary direct-melee witness, not an attribution history engine

If main elects to qualify a narrow additional source, the smallest recommended
slice is the exact `BaseMelee.PlayerAttack -> DoAttackShared` application callsite
at `0bd7`, rather than a prefix on every melee/weapon damage method.
At that pinned callsite, borrow the actual local HitInfo, local original
RPCMessage.player, original RPCMessage.connection, weapon instance and intended
HitEntity; capture the actor's existing exact D11 token/UserId/name only if
CurrentGameplayPlayer succeeds and its connection is reference-equal to that
RPC connection. Compare the RPC connection's player identity too. A reconnect
between RPC receipt and application therefore cannot substitute a new connection.
The witness exists only around the synchronous original DoAttackShared call.

BaseMelee.DoAttackShared is `0x06001795`, 187 bytes / 72 instructions / three
finally handlers, SHA256
`261a344bcfc90575c2727f42e69835e70d75abc19813dd2d07cbf69769e59e89`.
It invokes GetAttackStats at `0002`, then calls Hit.HitEntity.OnAttacked(Hit)
at `0029` synchronously. A witness must preserve its original actor, connection,
hit and intended victim through those intervening calls; unknown mutation
poisons attribution. Source insertion must preserve vanilla virtual dispatch,
and eligibility can initially be restricted to the actual base DoAttackShared
implementation. Five implementations were found: BaseMelee, Chainsaw
`0x06002225` (28/11/0), Handcuffs `0x06002856` (63/27/0), Toolgun
`0x060037cd` (44/15/0), and Hammer `0x06005119` (164/68/1). Handcuffs,
Hammer and Toolgun do not automatically gain eligibility from a melee class
name; Chainsaw's base call also requires its own scope qualification if included.

The proposed predicate at main's pre-OnDied capture requires every condition:

1. One valid owner-thread witness from that exact qualified application callsite
   is active. No generic stack inspection, callback-name classification or
   arbitrary DoAttackShared prefix manufactures it. Unknown/nested source
   interference invalidates it; do not use a last-writer-wins frame.
2. Death HitInfo is reference-equal to the witnessed HitInfo. Victim and original
   D20 incarnation match the intended target; Hit.HitEntity still matches.
3. Hit.Initiator is reference-equal to the original RPC player; Hit.Predicted is
   reference-equal to the original RPC connection. CurrentGameplayPlayer with
   the expected captured lifetime still validates that same connection at capture.
4. Hit.Weapon and Hit.WeaponPrefab are both reference-equal to the witnessed
   melee component. ProjectileID is zero and ProjectilePrefab is null. These
   are consistency checks after provenance proof, not standalone producer tags.
5. Inspect the already-bounded 28-element damage vector, not just its majority:
   require finite nonnegative weights, at least one positive weight, and positive
   weights only in Blunt/Slash/Stab. Any Explosion/Heat/Poison/ElectricShock/Bullet/
   Arrow/other positive category or wrong storage shape yields nil attribution.
   This deliberate first-slice filter may conservatively omit other true hits.
6. Main subsequently proves its normal terminal completion and original hit/
   initiator correlation. Known or ambiguous hook mutation suppresses attribution.
   Never reconstruct a replacement actor from an ID or re-read an owner chain.

This is a proposal for future exact-host qualification, not a newly qualified
runtime mechanism or implicit permission to patch more sources. If main keeps
only the existing death seam and does not qualify this temporary witness, the
correct first result is **Killer=nil and KillerId=nil**, including apparently
pure melee/bullet hits. Category blacklists, Weapon type checks, ProjectileID,
Predicted and CurrentGameplayPlayer together remain insufficient to certify
an arbitrary final HitInfo's original producing action.

One fixed borrowed frame, with nested entry failing closed, is enough for this
slice; no dictionary keyed by UserId/projectile/entity, damage history, delayed
owner association or persistent hit registration is needed. Finalizers release
the borrowed HitInfo/player/connection/weapon/victim references on every exit.
Only the original private token and bounded scalar snapshots reach deferred
transport. A later actor disconnect may stale that captured facade without
changing its original ID/token; inability to prove the source at capture cannot
be repaired by snapshot-only actorId. Optional damage/weapon labels remain
independent and make no promise that a killer is known.
