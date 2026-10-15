# Gameplay D8 — bulk Loot item qualification

Status: **PASS WITHIN THE RECORDED PINNED-HOST SCOPE**.
Starting main: `0afea7dcb5f8e7f3d8c22a6b184be1cc015d7e25`.
Date: 2026-10-15. [D2A](GameplayD2A-Validation.md),
[D3](GameplayD3-Validation.md), [D4](GameplayD4-Validation.md),
[D5](GameplayD5-Validation.md), [D6](GameplayD6-Validation.md),
[D7](GameplayD7-Validation.md), [D23](invariants/Gameplay.md),
[D20](invariants/WorldEntity.md) and D7/D10/D13/I4/I8/I9/I10/I12 remain
authoritative. The exact adapter, ownership authority and registry are reused.

The finite development allowlist contains **221 definitions: 217 added here and
the four D7 baseline definitions**. The [public item table](api/LootItems.md)
records every canonical name, numeric ID, vanilla stack and positive maximum;
the [complete ledger](qualification/GameplayD8-Items.md) retains all 1307
discovered definitions, exclusions and failed attempts. Family counts are forty
raw resources, eighteen components, fifty-three consumables, three basic
materials, 103 equivalent definitions and four baseline definitions.

Only these exact prefab identities remain supported:

| Prefab | Numeric prefab ID |
|---|---:|
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab` | 966676416 |
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab` | 555882409 |

Every enabled definition has individual physical/public Windows/Linux evidence.
The final adapter sweep verifies 884 original Item/UID/world/Spawn results per
host. Stored, bonus and controlled-bag payouts are suppressed through the
existing adapter. Pre-COMMIT rejection preserves vanilla; accepted worlds remain
host-owned, and post-COMMIT partial failures have no fallback, rollback, replay
or compensation. D6 per-prefab ownership, atomic whole-owner publication,
conflicts and retirement remain unchanged. Numerical limits are unchanged.
The only production adapter change is the bounded pre-COMMIT stack-premise
recheck justified below; the larger item list is generated into existing checks.

## Admission and discovery

Inventory the exact loaded pinned registries, bounded at 4096 definitions,
one definition per existing owner-thread fixture turn. Retain every discovered
name/ID/stack/category, direct components, condition/repair state, relevant
definition/mod fields and all six creation/world/removal/change callback bodies'
identity/hash/classification. Discovery is a test-only activity; it introduces
no production registry scan or unrestricted item-definition support.

Investigate all three requested families and additional equivalent definitions.
Candidate names are not presumed canonical. Screening reasons are provisional:
mod names alone cannot prove incompatibility. Inspect plausible simple paths
before final exclusion. Definitions with unsupported state/ownership or
unproved initialization remain excluded with their exact reason. Isolated
candidate failure must not abort unrelated candidates; inability to discharge
exact cleanup responsibility is a safety blocker for that batch.

Every newly enabled item requires individual Windows/Linux identity, supported
quantity creation, original Item/UID/world reciprocity, completed Spawn witness,
exact keyed world and cleanup proof. After both closed-allowlist physical probes,
stage only the proven intersection for actual public declaration qualification.
Final capability publication/commit requires individual public amounts and
stored/bonus/controlled-bag suppression on both prefabs/hosts. Pending batches
and a completed-run marker with individual FAIL rows never count as item PASS.

The initial closed package SHA256 is
`07c7fc8b4664fcc3e58abd49c05a32c688c1cff2b0469c5c45e1ba2c95ef92fd`.
It discovers all definitions and probes eligible candidates at one, the capped
maximum and the preceding boundary. Existing D7 native/managed allowlists remain
closed; test-only initial probes bypass prior public matrices rather than
relabeling them as new evidence. Initial outcomes follow; they did not complete
final qualification.

Initial closed batch completes safely on both hosts: 1307 definitions, ninety
mod types, 44 physical candidates, forty individual PASS, four FAIL and 1263
SKIP. These are provisional discovery/probe classifications, not the final
enabled set or proof that all skipped definitions are incompatible.

| Host | Initial actual receipt | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-013050-124/server.log` | `e7a3b5f9fcaa0827b416e104fd152ce15b0be7b262d541a041609f3373e2fef3` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-013107-1987915/server.log` | `b548b7da456202009c68eee08ffd76ef8ff43ade8c06111ca3b67430141c89dc` |

All physical ledgers return to zero and runners restore installed inputs, stop
the owned servers and release their leases. `animalfat` is not a canonical
definition; `fat.animal` is found and individually passes on both hosts.

Four retained initial failures (`barrelcostume`, `cratecostume`, `santahat`,
`spraycandecal`) fail the combined fresh-default assertion before world payout.
That first assertion does not identify the offending scalar. Subsequent exact
IL inspection and refined probes identify and check the definition-derived
defaults, as recorded below. None of these four is enabled by the initial run.

Components and lowgradefuel initially screened only for ItemBlueprint roots
need exact metadata qualification. Ores/crude oil and bone fragments initially
screened for cooking/composting callbacks need their real initialization and
event ownership examined. These investigations concern newly considered item
types, not a redesign or repeated investigation of the qualified payout engine.

Targeted Linux IL shows cooking/composting initialization is **not scalar-only**:
it initializes cookTimeLeft and subscribes the static CycleCooking callback on
the same Item's onCycle event. The subscription has no global owner; DoRemove
and pool entry clear it. New per-item physical proof must still verify the exact
default, exactly one static callback and its release on exact Item removal.
Metadata-only roots have no Unity/Item lifecycle initialization; unrelated
projectile/use/equip methods are not universally inert and remain outside this
creation/drop qualification. No item is admitted solely from this inspection.

The refined closed package is
`e2c3b3f5a12040beadc4141c3d175a74a24bf63b6ecd79972ef94bfa4e23e095`.
Its explicit skip list names the forty independently passing initial items on
both hosts (name/ID/stack/outcome compared exactly), SHA256
`3fead57e45e9961c21a882582db1722a00a237b05a8aa4abb4903c7c9d314d24`.
It retains the initial negative outcomes and independently tests newly eligible
definitions plus the four previously failing initial-default cases. Existing
D7 predicates and producer budgets are unchanged.

The revised per-definition default assertion is justified by exact supported
initialization: Item.Initialize unconditionally copies Definition.condition.max
into condition/maxCondition, even when condition.enabled is false. hasCondition
independently requires condition.enabled. Dormant configured 100/200 is not
active durability. Assert the exact definition-derived scalar and false
enabled/hasCondition; do not simply ignore condition. EnterPool explicitly
resets cookTimeLeft to zero; ordered Cookable and positive Compostable mods
then initialize it and the single Item-owned static callback. Removal clears
the callback. The initial hardcoded-zero premise was too narrow for new targets,
not evidence of a production adapter defect.

The new offline reader initially rejected both full censuses as incomplete
because four genuine shortnames contain spaces (`legacy bow`, `lumberjack
hoodie`, `military flamethrower`, `mini fridge`). The raw host receipts were
complete; the reader's token assumption was incorrect. Parsing now uses explicit
field boundaries, with deterministic and malformed/partial/duplicate/identity/
cleanup negative fixtures. This reader error is separate from the four physical
item failures. No missing row is silently discarded or treated as PASS.

## Completed physical intersection and retained batch stop

The refined runs each complete 168 new individual physical proofs, then stop
at `water` / `-1779180711`. Its actual definition has NoDropping. Item.Drop's
qualified flag branch removes the Item and returns no world rather than calling
CreateWorldObject. The conservative research ledger cannot manufacture proof
for a possibly initialized unreturned world; it refuses cleanup and stops the
batch. No assertion or ownership check is relaxed. Both runners stop their
owned process and restore installed inputs before unrelated candidates resume.

| Host | Refined incomplete negative receipt | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-014456-541/server.log` | `8b6caf3fe481f965574a54982a65fbd0953976b106653b7e4ed4563b8cb621fc` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-014500-1990735/server.log` | `fe49630e59354f40d89f3e90b68e1e99053ec1febf3a938148dd531bf4b3ca3a` |

These batches are not overall PASS. Their preceding independent item receipts
each prove all intended quantities and exact cleanup before the later unsupported
item; those valid proofs remain individually attributable after server
restoration. An explicit incomplete-reader mode retains this distinction and
the unattempted tail instead of silently declaring completion.

The resumed closed package SHA256 is
`88ea8dfafe605bcbba6aa96c1a4564f2d4b9eccf9f8915341da178b676ee815d`.
Its 208-name prior-PASS skip list SHA256 is
`8395d826bbaeab732385cd7fbf54f6f6e63a7393a10d350825a1593510a4db49`.
It excludes exact NoDropping flags before creation/drop (including water/salt),
retains the adapter's eight-mod limit and individually probes newly qualified
projectile metadata derivatives and all unrelated unreached candidates.
Both hosts PASS nine/nine, with zero physical resources retained:

| Host | Resumed actual receipt | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-014952-656/server.log` | `4fef9010532fa96f076a61b535d50896e2bf1f76eef0ca095a2a2ec45ab06d57` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-014958-1992403/server.log` | `8512e52047675eeebec055c86d631efbcb161a8f01d52e87681234a5665724e6` |

Cross-host ordinal comparison proves 217 unique new name/ID/stack/family pairs
with individual physical quantity/ownership/cleanup receipts: 43 Resources,
18 Components, 53 BasicConsumables and 103 equivalent definitions (17
Ammunition, 53 Attire, four Items, 25 Misc, four Tool). These are physical
results, not yet public support. The four initial inactive-condition failures
now separately PASS their exact definition-derived defaults; initial negative
receipts remain intact. Both barrels, existing four definitions and D6 semantics
are retained. Water's failed attempt and salt's pre-probe exclusion are distinct.

Only this proven intersection plus four prior definitions is staged in the
221-name candidate manifest for actual public-path testing. The candidate is
not committed or publicly documented as supported before every enabled name's
required public Windows/Linux gates pass. Failed public candidates must be
excluded with evidence rather than weakening assertions.

## Finite manifest and unchanged resource envelope

### Item initialization evidence

Pinned Rust build 25653776, protocol 2634.289.1, revision 166494 and Harmony
2.4.2.0 remain unchanged. The manifest pins Windows Carbon 2.0.262.0/8a81d70
and Linux 2.0.261.0/c74c4ca separately, including backing Assembly-CSharp hashes
and MVIDs. Loaded Carbon reflection bodies and backing-file IL hashes are
distinct evidence: token encodings can differ despite identical MVIDs. Both
field-resolved paths agree on the admitted initialization semantics.

Metadata roots ItemBlueprint, ItemModConsumable, ItemModProjectile,
ItemModEntityReference, ItemFootstepSounds and ItemCorpseOverride have no
Unity initialization or six Item lifecycle callbacks in the inspected chains.
ItemModProjectileSpawn, ItemModProjectileRadialDamage and ItemModProjectileDart
likewise add future projectile behavior without creation/drop callbacks.
This claim concerns the examined initialization/drop path; subsequent use,
equip, hit and consumption behavior is outside Loot qualification.

Cookable copies cookTime into Item.cookTimeLeft and subscribes one static
CycleCooking(Item,float) delegate on that same Item. Positive Compostable
initialization copies the server composter interval and subscribes the same
delegate; a nonpositive branch preserves the prior ordered-mod result.
Subscription removes then adds the same callback, has null delegate target
and creates no global owner. OnItemCreated clears prior onCycle before ordered
mods; DoRemove/EnterPool clear it, and EnterPool resets cookTimeLeft. Individual
physical probes verify the exact ordered default, one callback when expected
and removal-time clearing. Scalar Burnable initialization remains the qualified
fuel copy. No new public initialization input is exposed.

FoodSpoiling is excluded: OnItemCreated allocates InstanceData, sets its spoil
time, disables pooling and inserts the Item into a separate persistent global
food-spoil queue. Removal/parent callbacks own additional queue/refrigeration
behavior. NoDropping liquids are separately excluded by their actual flag;
Item.Drop removes the Item and returns null without creating a world.
Active condition/repair, nonempty unqualified lifecycle callbacks, nested state,
extra associated ownership and the unchanged name/mod bounds remain explicit
exclusions rather than permission to add another ownership model.

Targeted evidence is retained under `/root/codex/gameplay-d8-20261015`:

| Receipt | SHA256 |
|---|---|
| `targeted-item-il.log` | `5b066ed21fdb9b0283cadf811668009a6da185533123ba85fce29fbd319cd2c8` |
| `targeted-metadata-event-il.log` | `b12a814ca85fc703f96c8cd6a68935e35a984da9817a4fa7dcb7fd321730b884` |
| `targeted-cycle-il.log` | `236d9e68fb2dde98197f115020629d90c37ae4764f4c7dd79e1cf9b73b9fc532` |
| `targeted-food-followup-il.log` | `bce50acad1fdc368ac3bddc24ed7149e9f246b6a7410896e003e0462869faeeb` |
| `targeted-state-premise-il.log` | `a5ba86d797611a2e4cbeb88b710ba9decadc03f6ff5737ecda44a6f3a1cf29be` |
| `targeted-nodropping-il.log` | `26d384feaefa10fb21f9228d4168e22694e46e934ff00780f3aeeb44358b6843` |
| `targeted-raw-il-hashes.json` | `d9dde9fbdd9143100e97ba3a4e74ae42d779aedd8778f3c43ab0b1c687564651` |

Windows targeted metadata receipt
`C:/Sandbox/Codex/D8Windows20261015/Metadata-Targeted-Windows.txt`, SHA256
`d921c9c5d992922e190d127e1b525c509fa62662ac7cd82672661cda6edc33b1`,
independently confirms the inspected initialization chains on that exact host.

The manifest was initially seeded with the four already-qualified D7 definitions.
No candidate is authorized by its existence in the registry. A strict,
version-pinned manifest records exact Name/Id/Stack/Family and separate Windows/
Linux host pins. Deterministic generation projects its finite names into the
two existing comparison checks; there is no new runtime table, dictionary,
definition resolver, callback, hook or engine. The generated comparison work is
bounded by a 64-KiB/512-row input and 32-KiB output block limit, with malformed,
duplicate, unsorted, unknown-field and semantic-input/source drift rejection.
These are generator input limits, not an item-count success target.

The first actual generator CLI run exposed an UnboundLocalError: a local output
loop variable named Path shadowed the imported Path type before argument parsing.
Module-level projection tests had not exercised that entry point. Rename it
OutputPath and add actual subprocess check/patch/write, idempotence and semantic
drift rejection tests with the same ten-second test deadline. Those now PASS;
the error is infrastructure evidence, not a Rust or Loot payout failure.

The initial PowerShell7 public packaging command rejects canonical JSON because
its parser represents integer IDs as Int64 while the new guard initially
requires Int32 objects. This observation is retained from the tool transcript;
no standalone raw-log hash was captured. The corrected packager accepts only
Int32/Int64 representations within the same Int32 ranges, rejecting booleans,
fractional values and out-of-range integers. Actual PowerShell5/7 tests exercise
ten malformed inputs per runtime and deterministic successful regeneration.
All seven original PowerShell5 packets reproduce byte-for-byte. Framework/Core
Deflate implementations differ, so cross-runtime validation proves exact
decompressed sources and timestamps rather than identical compressed bytes.
The first package-test prototype separately fails from a Path variable shadow,
then from that invalid cross-runtime compressed-byte premise. These are retained
as transcript-derived infrastructure negatives, without invented standalone
receipt hashes. The final tests still require same-runtime byte determinism and
exact cross-runtime source/timestamp identity. Subsequent audit reproduces
unknown manifest fields and incorrectly typed host pins being accepted by the
initial packager; strict UTF-8/JSON grammar, decoded duplicate-key rejection and
exact field/pin validation now match the canonical schema. Actual PowerShell5/7
tests reject 29 malformed cases per runtime and preserve all corrected packet
bytes. These tooling failures never qualify an item or alter payout semantics.

D6's existing retained item-name maximum of fifteen ASCII characters preserves
`6 * (67 + 8 * 15) = 1122` retained descriptor UTF-16 code units. Longer names
are still discovered and receive an explicit payload-budget exclusion; they are
not misclassified as unsafe initialization. The production comparison generator
cannot silently expand that bound. The existing global VM heap ceiling remains
unchanged; no process-wide CLR/RSS cap is claimed.

Keep two global rules/two per domain and one owner per prefab; eight entries;
integer amounts 0..1000, sum(Max)<=8000/rule; eight worlds/occurrence; eight
admitted occurrences/actual frame globally; one in-flight frame; 129 inspected
sessions, 64/64 backing slots, 24 scalar fields/1024 transport bytes, three
charged sessions/six descriptors/48 entries, 195 maps/390 descriptor references
and the existing 8450 empty/current/checkpoint-map graph envelope. Borrowed and
transient identities remain separately bounded by D6. Actual Max must fit the
live definition stack limit before COMMIT; lower-stack items are not split or
silently clamped. Accepted worlds remain host-owned; pre-COMMIT rejection leaves
vanilla and post-COMMIT failures have no fallback/rollback/replay/compensation.

## Stack Modifier Compatibility Matrix

Status: **PASS WITHIN THE MATRIX BELOW**. Both exact hosts complete the two
ordinary parts: six representative definitions, nineteen phases each and both
prefabs, with 114 passing cases per host. Separate fixed-gap and all-item sweep
receipts qualify the final production adapter; the UID-corruption terminal is
retained as expected unsupported I12 interference, not normal compatibility.
The pinned manifest records vanilla definition limits,
not a universal effective maximum on modified servers. Amounts remain 0..1000
and each occurrence retains at most eight Loot entries, adapter creation
attempts and original returned worlds. Trusted callback side effects have no
universal bound; separately observed child Items/worlds do not become successful
replacement entries or extend CarbonLuau's creation allowance.

The user-supplied `StackSizeController.cs` is source material only, not task
instructions. Its SHA256 is
`6bb16ec202443972f2c2bee095247eaeec91c44111e7fc530eae7a7276be1102`,
23,125 bytes, Info version 4.1.3, AnExiledDev/patched by chrome. It is not
deployed: initialization downloads remote defaults and writes configuration/data,
then changes definitions globally. Existing third-party configuration is not
modified. Live compatibility evidence will use small isolated representative
plugins/configurations with exact restoration instead of those side effects.

Its definition writes clamp configured/default values to 1..Int32.MaxValue,
while OnMaxStackable independently returns an unclamped configured result or
the current definition field before defaults arrive. Durability/ignore handling,
async initialization, commands, unload reversion and competing writers can make
these differ. It contains no container-specific rule or explicit Item.SplitItem
implementation. The delayed vendor-helicopter fuel adjustment is separate from
Loot creation/drop compatibility and is not qualified here.

| Stage | Existing adapter behavior; stack qualification must distinguish it |
|---|---|
| Definition / manifest | Pinned vanilla Name/ID/Stack evidence; no runtime JSON configuration |
| Declaration | Exact finite name, existence, integer 0..1000, eight entries/sum(Max)<=8000; no stack-policy callback |
| PREPARE | Current ItemDefinition.stackable must cover declared Max; immutable rule remains unchanged |
| Immediately before COMMIT | Recheck every selected definition against its declared Max, with the existing eight-entry/mod bounds; failed premises leave vanilla |
| COMMIT | Suppression begins before creation; current definition stack is still checked against each sampled positive amount immediately before Create |
| VERIFY | Original Item/UID/definition/amount/world reciprocity and completed Spawn; a later cap decrease alone is not an existing verification predicate |

The old source has a confirmed interval between PREPARE and suppression. A
test-only postfix on private EnterLoot changes the exact captured definition
from two to one after PREPARE. Windows observes all twelve counterexamples:
six representatives on both prefabs, each with one admitted occurrence,
COMMIT true, zero created/accepted worlds, partial counter +1 and both original
stored payouts suppressed. Each case independently returns its physical ledger
to zero. The new production guard rechecks at most eight captured definitions
against declared Max immediately before suppression; it adds no allocation,
host lookup, callback, engine hook, scheduler or registry change. Both exact
hosts now pass all twelve fixed-source cases: COMMIT false, selected +1,
partial +0, zero created/accepted replacement worlds, both original stored
Item/UID/world payouts preserved, and twelve independent ledger-zero cleanups.
The complete unchanged D3–D7 and B2/RPC terminal matrix also passes.

| Host | Fixed-source gap receipt | SHA256 | Duration |
|---|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-032425-870/server.log` | `8cc4a7c2f379417dc1dc2278d50d42282981849e809376766d33bac49dbe9509` | 156.46 s |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-032256-2017492/server.log` | `21cd166d0565aeea6e3a905529d410079dde8f5bac05cb7209abd95ab3d431d6` | 149.23 s |

The first diagnostic packet SHA256
`184b15459f85c9b8cc3279c8401259c27a61654411ce57d93192bafce28f4831`
then fails the unchanged B2 two-world handoff assertion: the gap fixture leaves
its final one-entry wood rule active. The whole packet is FAIL even though its
twelve independently cleaned gap counterexamples remain valid negative evidence.
Raw Windows log
`D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-030440-943/server.log`
has SHA256 `81f971612d969274c01be20530e55dee3c66acf0f3d0735fae5e90ea8e195667`.
All ten installed input hashes restore and no owned server/helper remains.

The corrected old-source packet SHA256
`030b556f7dad3dde7f6a569092a7dc1ac69c636330686c4dfce04cbc1a552e80`
republishes the original two-entry rule before B2 without changing that assertion.
It exposes a second fixture handoff defect on both hosts: reloading restores the
descriptor but also resets the domain's state module; no death has occurred in
that new domain when B2 asserts its existing Died counter. Both whole runs FAIL,
while all twelve gap counterexamples and cleanups per host remain valid. Linux
never executes the known-bad first packet.

| Host | Second handoff negative receipt | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-031438-954/server.log` | `d5d81d3ab4d4a50f57c6cbf3897a7a640196056dc4e2a8cbad328dee9b28fa00` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-031233-2012458/server.log` | `58ce933583d2a23a47dba29640dec7ff8e703fd34c4da510a21f9a436a65f075` |

The fixed-source gap packet SHA256
`1ec3ff3646702fb87eb2906344396c08e58d42424908d50d04b5b7db8b754f59`
performs an actual verified two-entry payout after the restoration reload. That
supplies the current domain's real EntityDied delivery before unchanged B2.
No assertion, deadline or resource ceiling changes. The immutable old-source
packets and their distinct failures remain separate from the production fix.
Post-COMMIT changes retain partial/indeterminate outcomes without fallback,
rollback, replay or compensation.

Backing pinned IL on both hosts distinguishes three mechanisms: definition
stackable; Item.MaxStackable's parent-container min/max adjustment; and Carbon's
hook-transformed runtime behavior. Backing Create stores the requested positive
amount directly. Drop/CreateWorldObject/WorldItem.InitializeItem/DroppedItem.ServerInit
do not themselves call MaxStackable or SplitItem. DroppedItem merging consults
effective limits and can retire another Item/world; it is a different path.
Loaded hook behavior and competing callbacks still require actual live evidence.

| Configuration | Windows | Linux | Supported scope / remaining evidence |
|---|---|---|---|
| Vanilla definition payout | 221 individual public PASS | 221 individual public PASS | Stable definition-field Create/Drop path; effective inventory diagnostics are separate |
| Increased definition stack | PASS | PASS | Field 2000, public quantity 1000, one original world/entry; quantities above vanilla where vanilla is below 1000 |
| Decreased definition stack | PASS | PASS | Field two admits quantity two; declared three with live field two leaves vanilla. A stack-one definition cannot have a smaller positive cap; its field-two case is an increase |
| Hook-only limits | PASS | PASS | Actual MaxStackable overrides 20/1/0/-1 and wrong-type fallback are observed separately. Physical Create/Drop does not call those hooks or normalize to them; live definition remains admission authority |
| Container-specific limits | PASS | PASS | Both min/max policies and hook-after-container precedence observed. Same parented Item amount ten drops unchanged above effective seven, exact original UID/world, no automatic split |
| Dynamic limits before PREPARE / COMMIT | PASS | PASS | Field two to three before PREPARE admits three; three to two rejects three. Captured two to one after PREPARE rejects before suppression through the new bounded guard |
| Dynamic limits after COMMIT | PASS, partial contract | PASS, partial contract | First completed world retains amount two after field becomes one; second Create is refused, partial +1, no fallback or compensation |
| Conflicting modifier plugins | PASS, bounded fixture | PASS, bounded fixture | Both subscribers run; actual competing 7/9 result is observed without plugin-order assumption. No universal priority or arbitrary-plugin promise |
| Explicit splitting | PASS, diagnostic scope | PASS, diagnostic scope | Default and hook-returned child Items have distinct exact UIDs/worlds and completed Spawn. Loot does not split entries |
| Drop amount / split interference | PASS, partial contract | PASS, partial contract | Mutated original amount and extra plugin child worlds fail exact planned-payout verification; original/child ownership is retained and independently cleaned by the test owner |
| Drop UID corruption | UNSUPPORTED_I12, expected | UNSUPPORTED_I12, expected | No UID repair or refreshed authority; partial and cleanup-failed counters increase. Unknown completed world is left to process teardown; full profile bytes/metadata restore is mandatory |

Representatives cover every manifest family: sulfur (raw resource), gears
(component), antiradpills (consumable), lowgradefuel (basic material),
hat.bunnyhat (equivalent) and wood (baseline). Direct definition changes are
distinct from Item.MaxStackable, CanStackItem and OnItemSplit outcomes. The
representative hook plugins are neutral by default, finite and private to the
controlled fixtures; they use genuine Carbon dispatch without changing another
plugin's configuration. The supplied StackSizeController is statically examined,
not deployed or universally certified.

Positive amounts above a vanilla stack limit can remain in one physical world
when the live definition admits them. Hook/container effective maxima can differ
without transforming this immediate creation/drop path. Subsequent pickup,
container insertion, use and world-item merging are outside this qualification.
The eight-world bound counts CarbonLuau's own planned creations; an interfering
trusted plugin can independently create worlds, which are not attributed by
proximity or incorporated into a successful payout. Callback mutation preserves
the existing partial/indeterminate contract after suppression.

Final representative packets are R6 part zero
`85638e1219020ff8d4aeb2cb6924135f2e9fe0ae6c2c5b7d600fc22eee8d46db`,
part one `9da053fd4dfe741527cbba5666f16fff60f6489f8e23264f44aa64c5fcb597ac`
and terminal part two
`f3cf863df0c99e2db77aec7ed12467ace59f28d6641d70fe108fa9fca7b7df39`.
All contain the same final production adapter SHA256
`08ddc4454242e1af09ecc30fb948cf8a1f264a89111023d239a808e1dd494004`.
An independent archive comparison proves R5 to R6 changes only the second
plugin's nested Split callback assertion; the other 98 entries and production
adapter are identical. No amount/world/frame cap or deadline changes.

| Host / component | Exact raw receipt | SHA256 | Duration |
|---|---|---|---|
| Windows matrix 0, 57 PASS | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-040414-761/server.log` | `84794e34d7f15090427130e695824522ac2ddaa4d8d9ec43f891b59b42d1374f` | 198.01 s |
| Windows matrix 1, 57 PASS | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-040732-805/server.log` | `364cc2c4c123066f07606b21087b064ec4f33159e0c3e83b2622372622536ecd` | 201.09 s |
| Windows terminal, expected UNSUPPORTED_I12 | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-041053-907/server.log` | `e7abfc5b8687c54cf2f36d739841edbaea86d401d9a9e71d56f1baf55bf43ebb` | 147.71 s |
| Linux matrix 0, 57 PASS | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-040726-2027930/server.log` | `709a68c42c158f1817903671f70c29a761220d486b138291a815358af4724848` | 184.33 s |
| Linux matrix 1, 57 PASS | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-041030-2028461/server.log` | `af956dca163d5f78b3363c860cdbe9c4f52e80616ccb324cf16f5f6057726242` | 183.31 s |
| Linux terminal, expected UNSUPPORTED_I12 | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-041334-2028955/server.log` | `fc80714ed5c9b516b12c7e8bb86b261da5ceb9e849e55c0bdfe6c010839678cc` | 137.25 s |

Terminal Windows preserves original UID 76142 versus actual zero and completed
world 76143; Linux preserves original UID 86294 versus zero and world 86295.
Each records partial +1 and cleanup-failed +1, without repairing UID or refreshing
authority. The owned processes then exit through the pinned Shutdown/Process.Kill
path (Windows -1, Linux 137), before exact profile restoration. Windows restores
twenty files / 16,642,732 bytes with manifest digest
`85c9195bb771444c68a4d34dc976d28259c79d9c35498c52165703bfcefc3f5b`;
Linux restores twenty files / 13,280,100 bytes with canonical manifest digest
`b95e4316b167f0cd5224989fb187ac094c687a19dc138baba9252642df9e2378`.
Both restore matching snapshot bytes/file set/platform metadata within unchanged
128-file/64-MiB test limits, all prior installed input hashes and absent probe
plugins. This is controlled process/profile recovery after unsupported host
interference; normal world cleanup and B2 terminal completion are not claimed
for the UID-corruption packet.

Official hook descriptions identify the relevant API surfaces, not evidence for
the pinned transformed hosts: [OnMaxStackable](https://docs.oxidemod.com/hooks/item/OnMaxStackable),
[OnItemSplit](https://docs.oxidemod.com/hooks/item/OnItemSplit),
[CanStackItem](https://docs.oxidemod.com/hooks/item/CanStackItem),
[OnItemDropped](https://docs.oxidemod.com/hooks/item/OnItemDropped) and
[CanCombineDroppedItem](https://docs.oxidemod.com/hooks/item/CanCombineDroppedItem).
Exact-host IL, actual callback witnesses and physical receipts remain required.
The host evidence uses Carbon's Oxide-compatible hook dispatch on the pinned
Carbon servers; it does not qualify a standalone Oxide host.

## Qualification and restoration

### Stack fixture and receipt-tool negatives

The first ordinary R3 stack packet fails actual Windows/Linux Carbon compilation before
any matrix case starts: its dynamic-before-PREPARE case calls the string-based
three-argument payout overload with a BarrelResearchCase. The case-based overload
requires the existing explicit cleanup argument. Raw
`/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-033043-2019995/server.log`
has SHA256 `077ba0969812fb1403ce7a9d3c01113b8aa65646baabdbfd6135038e915f9685`;
the runner transcript SHA256 is
`1f597bdc07ac7ffc46421dd5764bbbc507520b6599ea3d9a15d6573d13de5c8f`.
All installed inputs and both test plugins restore; no owned server remains.
No matrix case or compatibility claim is admitted from that packet.

The paired Windows R3 raw receipt
`D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-033300-633/server.log`
has SHA256 `bd56811c996d06907a96ac5060a94ae5217674266f7906ac14d842da44466a05`.
R3 parts one and two are not executed.

R4 reaches nine passing sulfur phases, then fails the container diagnostic on
both hosts. The fixture retained natural source contents while assuming slot
zero was empty, and marked host-call ownership uncertain before evaluating a
normal move result. Cleanup conservatively refuses that uncertainty. These
whole packets FAIL; the failure does not prove item or stack incompatibility.
Cleanup masks the first exception, so this fixture diagnosis comes from source
inspection; no effective-limit diagnostic is emitted for that failed phase.
R5 uses deterministic source contents, verifies empty slot two and exact
Item/UID/parent/slot after a normally returned move, and preserves conservative
refusal when the host call actually throws. Its container diagnostic also drops
that same parented Item, verifying amount ten survives effective limit seven
without UID replacement, automatic splitting or additional stack-policy calls.

| Host | R4 container-fixture negative | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-033554-706/server.log` | `681d4082181564ac11cbc80d8145f2384d63f669fff41b86199235f96f0ffe6d` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-033654-2022049/server.log` | `83267b5a8443dd49ae4c05e4843b223625252ae4a2abcbe98e93d6619fe3d7c9` |

R5 passes the first seventeen sulfur phases on both hosts, including that
container fix, then fails the callback-count assertion during deliberate
OnItemDropped splitting. The assertion expects the second plugin's split
counter to remain unchanged although genuine SplitItem dispatch reaches both
subscribers. Exact split-child/world receipts precede the failure. These are
retained whole-packet failures; neither remaining ordinary part nor terminal
part runs from these failed sets.

| Host | R5 split-counter fixture negative | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-035507-917/server.log` | `57b12ba46184306b104b9a5dfcd965cbca75fd8bc84dc1b8230d9d222e12ea4f` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-035116-2025019/server.log` | `e9848be0f6d74615e0eb6e5513710752d6d436824e1846a8ae1e5b6c9fab5a46` |

Before any deliberate UID-corruption terminal run, review finds that the Loot
runner's installed-input restoration alone would not restore the controlled
server's database/save profile after refusing unknown ownership. Terminal
dispatch is held until a bounded full-profile snapshot/restore is implemented
and tested. The terminal-only helper permits at most 128 profile files, 64 MiB
of contents and 256 directory entries under the exact fixture identity;
database companions, saves and navmesh are included. It rejects wrong roots,
links and unsupported entries. After the owned process exits, it verifies the
original file set, bytes and platform metadata: owner/mode/timestamps on Linux,
attributes/timestamps/ACL on Windows. Snapshot and restore manifest digests
must match in the actual runner receipt before terminal evidence is admitted.
Actual Python and PowerShell5/7 helper tests pass. This safety correction is
test infrastructure, not a broader production cleanup or interference promise.

The new stack reader distinguishes gap negatives, fixed gaps, two ordinary
parts, terminal unsupported UID interference and the complete final-source
sweep. Independent malformed-input tests initially expose a missing cross-link
between an override's private returned child UID and the actual split result.
The reader now requires their equality, distinct original/child UIDs and worlds,
exact physical quantities and all bounded case/detail/cleanup records. An
earlier terminal test itself substitutes a nonexistent cleanup field and leaves
its input unchanged; the corrected test mutates the actual ledger field and
asserts its malformed input differs. These are transcript-derived tool negatives,
not invented host failures or standalone raw-log hashes. Both exact-host header
profiles, actual CLI entry points and all six receipt kinds now pass their
positive/drift/duplicate/incomplete/identity/ownership negative tests.

### Retained initial public-batch negatives

The first full public packet (offset zero, original package SHA256
`62a438db227aae27e2ab26708b4ec2fda3841e2debca43bff55a028d064f270c`)
completes with three individual PASS and 29 FAIL on each host. The first failure
is ammo.paintball during phase five: the research evidence array reaches its
sixteen-slot cap. Repeated actual Scrap_Yield modifier Add calls record the
same MethodInfo repeatedly. This is a fixture retention defect, not evidence
that those definitions are incompatible with the payout adapter. The complete
negative outcomes and resource restoration are retained before fresh retries.

| Host | Original negative receipt | SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-020233-589/server.log` | `1d009ea3e25e1972eaa05ce3684631cd5434aa1b4e5e53fdd524e82e50cd19cb` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-020234-1997353/server.log` | `85c4fd1cce2cc3920e7ca4762d3f3eff926410af0a48685039ba03c965d3a402` |

The inherited runner incorrectly accepts the completed-batch marker despite
individual FAIL rows. Both task drivers prevent the next old packet before
server mutation. Both servers restore all installed inputs and retain zero
owned processes/leases before corrected runs. No old partial result is admitted
as final public qualification.

The correction retains each exact MethodInfo once using Equals in a bounded
sixteen-entry scan. Every repeated call still checks and logs its complete IL,
exception clauses and hash; a seventeenth unique method still fails the original
cap. Cleanup retains every unique method identity. No production hook, adapter,
allocation bound or timeout changes. Corrected seven-packet comparison proves
the only decompressed source change is BarrelLootResearch.cs, SHA256
`a4c72b934806a2295786ceb6aa454058fc8948431638d40b71bf97db45ea9661`.
Its stable non-snapshot entry inventory SHA256 is
`b6766a596a4a9ad882873765ea56aa2565981c0f8b71e6b58b4c729c13e5668f`.

Both public runners now require one completed batch, 1..32 items, pass=count,
fail=0 and exactly that many unique individual PASS rows. Actual runner gates
are exercised offline through their parsed Python/PowerShell syntax trees:
thirteen good/bad receipts reject missing/duplicate rows or batches, failed items,
false zero-failure totals, unknown statuses and zero/excess counts. An independent
audit also reproduces an extra unknown-status row being ignored alongside a
valid row. Both runners now count every public-record prefix, and the independent
reader rejects every malformed/unknown public row; new fixtures cover extras
alongside a valid complete result. These gates
PASS under Windows PowerShell5 and PowerShell7 without starting a host. The
full original negative logs remain independently machine-readable.

### Completed vanilla public matrix

Both pinned hosts complete all seven corrected public batches at offsets
0/32/64/96/128/160/192: 32 items in each of the first six, then 29. Each host
has 221 distinct PASS and zero FAIL, with exact manifest Name/ID/Stack/Family
reconciliation. Every item has both-prefab amount, eight repeated entries,
inclusive range/mixed entries, stack-boundary and stored/bonus/controlled-bag
suppression receipts. Each batch also completes its common publication,
failure/recovery/shared-budget matrix, existing D3–D7/B2 terminal receipts,
zero physical ledger and installed-input restoration. All stay within the
unchanged 600-second deadline. These are vanilla public results; the additional
stack modifier matrix remains a separate required gate.

| Host | Completed batches / item results | Longest batch | Final 29-item raw receipt | SHA256 |
|---|---|---|---|---|
| Windows | 7 / 221 PASS, 0 FAIL | 457.68 s | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-025521-715/server.log` | `df92ee8449ea10f5d750855b49068bd0d67469f4003ede7d8a0bf89af8e4f89e` |
| Linux | 7 / 221 PASS, 0 FAIL | 413.89 s | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-025133-2005796/server.log` | `0019d6c6d56d0cb6e655afb446ed6a81851fd9838cf9ebe094598317fe578f40` |

Windows's independent reconciliation report is
`C:/Sandbox/Codex/D8Windows20261015/Windows-Vanilla-Public-Coverage.json`,
SHA256 `19e8b1e66cc0a0bff062f8ac34759bfbd0c27b94962f11fa416db75837136589`.
The complete source ledger retains all seven raw paths/hashes per host, each
item's exact receipt lines, and the earlier failed attempts. Original remote
log addresses remain distinct from local evidence copies; renormalizing an
address does not imply another host execution.

After the production pre-COMMIT guard changes, both hosts independently repeat
the entire 221-item set through actual public rules on both prefabs. Each of
442 occurrences creates two planned worlds, for 884 exact Item/UID/world and
completed-Spawn verifications per host, followed by exact cleanup. The minimum
is one and maximum is min(vanilla definition stack, 1000); equal endpoints still
produce two independent entries. Both final-source sweeps also complete the
unchanged legacy D3–D7 and B2/RPC matrix within the same 600-second deadline.

| Host | Final adapter sweep receipt | SHA256 | Duration |
|---|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261015-032702-373/server.log` | `f0b5559e2a342e15a914546cf7bae9a38e00717dc99599a8e63b1e1d7ed455d9` | 330.30 s |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261015-032525-2017864/server.log` | `c58cab67c615adb28755e03e1aa4ea33559ba644a10ffdae1821621c5bbcbc5e` | 307.04 s |

Candidate offline gates PASS on both operating systems: native Release builds,
managed builds with zero warnings/errors, policy validation and actual public
Luau VM declarations for all 221 names on both prefab keys. The tests cover
zero/minimum/capped maximum, eight repeated entries, mixed families, copied
inputs, missing definitions and invalid/ninth/1001 declarations preserving the
exact incumbent pair and charged-session count. Existing recovery and 100-addon
cases remain. This is executable validation; live public gates remain separate.

| Executable gate | Windows | Linux |
|---|---|---|
| Native Release build | PASS, 63.47 s | PASS, 89.82 s; compatible cached targets reused |
| Native CTest | 23/23 PASS, 98.71 s | 25/25 PASS, 175.81 s test time |
| Managed build | PASS, zero warnings/errors | PASS, zero warnings/errors |
| Focused policy / actual public VM | PASS, 0.217 / 10.38 s | PASS, 13.56 s combined |
| Full managed runtime | PASS, 132.77 s | PASS, 410.22 s including actual-worker Persistence1C and preview equivalence |

Linux's two additional CTests are platform/cache scope, not two extra gameplay
tests: ElfNoUnique_carbonluau_native is Linux-specific, while
ElfNoUnique_carbonluau_preview follows the inherited preview-enabled cache.
The ordinary Linux matrix has 24; Windows has 23. No test is silently omitted
to equalize counts. Windows runtime includes 100-addon fairness/shared heap,
parser limits, deadline/allocation recovery, 100 atomic replacements and 200
failed candidates. Existing storageasync, EntityDied certificates/100 addons,
C1/C4, inventory net/change, discovery and seventeen preview goldens also PASS.

Windows core gate hash manifest
`C:/Sandbox/Codex/D8Windows20261015/Core-Gates-Windows.json` has SHA256
`6c9cf4fb9f0872918cd8533662d9d6c6ec7ee8fd83a842f4163f3f850b9a4191`.
It records fifteen raw logs separately from live Carbon receipts.

Windows Persistence1C's first remote wrapper starts in the SSH user's home
directory, causing a relative example lookup to fail at
`C:/Users/host/examples/persistence/get/init.luau`. The failed transcript has
SHA256 `2d9da6f699302e9726fb2866624971579bdfdabd8cd009f6a8567c70c7653342`.
The wrapper then selects the existing source directory explicitly; product
source, assertions and deadlines remain unchanged. Its corrected complete run
passes in 282.30 seconds: six real examples, Set/Get/Remove 2200/4400/2200 across
root plus ten addons and two stores, peak pending eleven, zero duplicate/replay,
byte quota and twelve complete lifecycle/reopen cycles. Receipt
`D:/Sandbox/Codex/GameplayD8/persistence1c-windows.log` has SHA256
`68f91ae7c869b02124eae26ba7b1a4fd7f18bf1ba2a5e907fce3fdc52724adec`.
The earlier wrapper failure remains a separate negative.

Linux's complete runtime receipt
`/root/codex/gameplay-d8-20261015/runtime-linux.log`, SHA256
`913ab9b4f2c0f276ba13818784faaaf1696f82540a1ed0f0b5211959b428a2f0`,
includes the full shared-VM/100-addon and gameplay-scale/replacement matrix,
actual-worker Persistence1C and seventeen production/preview equivalence cases.
Persistence repeats the 2200/4400/2200 workload with peak pending eleven and
zero duplicate/replay, exact 16-MiB quota and +1/concurrent rejection, and twelve
complete host/worker reopen cycles. These are real-VM/worker results, separate
from actual Carbon server receipts.

Windows also passes standalone actual Query2D in 105.90 seconds, separately
from its already-passing namespace restart and real-VM Query suite. Raw receipt
`D:/Sandbox/Codex/GameplayD8/Query2DEvidence/persistence1b-20261015-040117-602f0e2c/server.log`
has SHA256 `4a07f413bb39b07fcc2daf8802aa9508259bd5ff26a9a61d28038e5506ac217d`;
the runner SHA256 is
`cad8e3dc9cf85088439e4068d3ad9af5b6deadadc33b30fc2a17c2e1113030b8`.
Owned workers exit, native mappings unload and prior installed trees restore.

Independent packet audit PASS: seven public+B2 packages, offsets
0/32/64/96/128/160/192, each with 98 unique entries and the same 221-name
snapshot. The first six select 32 definitions and the last selects 29. All 97
non-snapshot entries are byte-identical across packages. Sorted entry hash
inventory SHA256:
`13e0a10283c8b2dc6fb1ea0e82c34059525571f137cb5e08a8059ea83b1f704a`.
The snapshot copies exact Name/Id/Stack/Family, with no runtime manifest lookup.

The generated managed and Luau blocks contain exactly the same 221 ordinal
names, at most fifteen ASCII characters each. They occupy 12196 and 8375 bytes;
the finite vocabulary has 2267 characters. Worst-case comparison counts are
1768 per eight-entry rule and 3536 per complete two-rule set. The fixed VM/code
constant cost remains subject to the existing VM heap/deadline limits; it is
distinct from D6's owned descriptor retention. No mutable runtime dictionary,
resolver, registry scan or change to publication/constants is introduced.

Actual SDK10 canonical metadata generation and check-generated PASS; only the
RegisterDropRule summary changes in API/docs JSON. Generated definitions and
tooling metadata are byte-identical to baseline. The new preview case declares
sulfur/gears/fat.animal/chocolate through the existing signature and must return
UnsupportedPreviewApi under the unchanged preview host-mutation boundary.

Linux ASan/UBSan/leak qualification passes all 24 selected tests (321.43 seconds
test time, 405.94 seconds total). Receipt
`/root/codex/gameplay-d8-20261015/asan-linux.log` has SHA256
`89205ffc0f24851802b000d23a55fab86fb09f7fc426aefd130170cb00f14590`.
No Windows sanitizer result is inferred from that Linux run. Linux SDK10
metadata/generated definitions, seventeen preview goldens, the added bulk-item
UnsupportedPreviewApi case and unchanged hostile supervisor tests also pass.
Task containers use the existing init reaper; no host configuration changes.

Windows final packaging repeats the default production CSZIP byte-for-byte,
SHA256 `e7fee7a175b7650236376aafe5100d75e294b2e3b24b951e85dd1ce41ddceda7`.
The repeated unpublished development bundle has SHA256
`d1da97208a7571c96829c99b734ef26e0f62c3736bcf23482f99b749d7cf6c6e`.
Clean installation compiles all 74 public examples and executes the existing
actual-VM GUI/Player/C4 checks and teardown; log SHA256
`9fe63d4e979b6e489ce4d6eac15c997153285e78c0dcf3f238f2c44313576b24`.
Extracted private-worker/import/ownership/failure checks pass (5.998 seconds),
log SHA256 `50cffedc124b51dd5e2577e9af84d18d17a8aceb2f8b5ceeb878e42ecf2804e5`.
Release identity, negative fixtures, exact contents and determinism pass with
log SHA256 `08756f355d81f4f436edfeb5b1567fc0f6ceb629d8ce453b91a145e5563ee1c0`.
These are local development artifacts, not a release or uploaded v0.7.0 assets.

Retained final infrastructure negatives: a Windows worker Python preflight
resolves to the WindowsApps Store alias and exits 9009 before any test body;
no toolchain is installed. Python infrastructure tests instead run on the
controlling Windows Python/PowerShell5/7 environment, Linux and hosted CI.
A local attempt to run ToolingContracts under PowerShell5 stops because Test-Json
is unavailable; the unchanged test passes under its required PowerShell7 runtime.
Those two setup attempts are not item/host failures or successful tests.
Read-only review also reproduces a CI failure-propagation hole: an earlier
native Python exit under default PowerShell can be hidden by a later successful
command. The new Python-only workflow step now explicitly uses Bash fail-fast
semantics; the Ubuntu baseline already does. These observations are retained
from tool transcripts, without invented standalone receipt hashes.

The final staged whitespace check also reports one extra blank EOF line in
each test-only StackProbeA/B source. These cosmetic findings are retained:
the exact helper bytes are pinned by the live runner receipts, and no repository
gate forbids those blank lines. The staged whitespace check is not reported as
PASS; fixture provenance and functional assertions are unchanged.

Require all per-item receipts, bounded independent public batches, both-profile
independent owners, mixed/repeated entries, shared-budget exhaustion, source
veto/incomplete Spawn/creation faults, publication conflicts/replacement/unload,
allocation/fatal recovery and cleanup convergence. Run existing gameplay,
inventory, GUI/persistence regressions; native/managed/real-VM, sanitizers,
100-addon fairness, metadata/types/tooling/preview, all examples, deterministic
generation and packaging, clean installation and exact final-source hosted CI.
Preserve every negative without changing assertions, deadlines or ceilings.

Both exact hosts are available and their D7 binaries/pins are rechecked. New
task roots preserve prior evidence/caches and unrelated workloads. Windows uses
the session-approved `D:/Sandbox/Codex/GameplayD8` override. Linux uses
`/root/codex/gameplay-d8-20261015`; builds retain compatible timestamps/CMake
container paths and omit historical fixture databases to preserve disk space.
Two build jobs/CPUs and bounded memory are retained; no local heavy build or
toolchain installation occurs. Task-only Linux supervisor containers use an
init reaper, preserving D7's diagnosed zombie negative and unchanged tests.

Keep published v0.7.0 tag/assets and package/API/ABI/provider/schema/Luau
identities immutable. No release/tag or additional prefab/mode/attribution/API
is introduced. No pending local or hosted result is counted as PASS.

## Combined regression and artifact closure

The independent final ledger gate freshly reads all six closed probes, fourteen
public batches, ten final stack components, three retained old gap negatives
and six R3–R5 infrastructure attempts. It requires exact raw hashes, original
packet bytes/current adapter, normalized receipt equality, UID/world/Spawn and
cleanup predicates, actual runner process/input restoration and matching
terminal profile snapshots. The result is 221 PASS and 1086 explicit SKIP,
with ten initial physical FAIL outcomes and 58 initial public FAIL outcomes
retained in their histories. An earlier partial packet never becomes overall PASS.

The shared generator's final artifacts are byte-for-byte reproduced in source:

| Artifact | Bytes | SHA256 |
|---|---:|---|
| `docs/qualification/GameplayD8-Items.json` | 12,927,111 | `e527067bdc18ae6baeab40cd81efb2df3cad0ba212227705f02bc7707428c692` |
| `docs/qualification/GameplayD8-Items.md` | 148,286 | `feec25a57e7863164a6d0d0f1205778511fbc545408581ad00a8ea626a13c901` |
| `docs/api/LootItems.md` | 13,503 | `a56e691d9347f8f8be48aa6118c4711e3318aafac605fde10e8dc82f2aa405dd` |

The canonical manifest digest is
`37688a539718d8f80879b66bc88e9594bf90c37cf4d23f7425ac6813f45a47e8`.
The public projection check passes against the actual complete ledger. CI
rechecks generation/drift rejection, malformed manifests, exact receipt readers,
runner failure propagation, profile restoration and finite public projection.
Original raw/normalized receipts and generation arguments remain under
`C:/Sandbox/Codex/D8LedgerControl20261015`; final arguments SHA256 is
`124f8d567f1e915ee0183a85457176833e59bf63145e797a4f2177f1e39db27a`.

| Regression | Windows | Linux |
|---|---|---|
| D3–D7, allocation/recovery/publication, both prefabs, shared frame limit, source Kill veto/incomplete Spawn | PASS in public batches, final guard/sweep and ordinary packets | PASS in the corresponding exact-host packets |
| Standalone B1 and B3 | PASS B1 and all eight B3 modes | PASS B1 and all eight B3 modes |
| Gameplay A, inventory C3/C4, Discovery2B | PASS actual Carbon | PASS actual Carbon |
| Persistence1C / 100 addons / actual workers | PASS | PASS |
| Actual namespace / Query2D process restart | PASS separate namespace and Query runs | PASS borrowed combined Query/restart run |
| Actual GUI lifecycle | PASS one ScreenGui/TextButton/Activated, ten root reloads and ten Carbon unload/load cycles | PASS matching ten-plus-ten lifecycle |

GUI lifecycle verifies the recorded 1/2/1/0/1/0 resource baseline, native unmap
and owned compiler/worker exit on unload. It is server lifecycle evidence, not
authenticated-client rendering or ten distinct GUI examples/addon providers.
Windows raw GUI receipt
`D:/Sandbox/Codex/GameplayD8/GuiEvidence/persistence1b-20261015-041444-30099717/server.log`
has SHA256 `e170b6d0bc473f80bf908a7db2a7948532c2baff89b078cf21ba91f206c45ef6`;
runner SHA256 `4df3c53233a6fe7b4df69c1feaf2531dc46a3190d0a367ee7386a7ed58049dc9`.
Linux GUI wrapper `gui-borrow-20261015-042025/runner.log` has SHA256
`405a8c0ac6af71ca12ff75c737664ebfb3d18873ab6ef7d8d472727628af336c`.
Linux actual persistence wrapper `persistence-borrow-20261015-042400/runner.log`
has SHA256 `a547a011710b8993e626700e6191d81d4d3dc1de93eb4c45019a07628a690758`.
It verifies root/addon Set/Get/Remove, Query across host reload and actual
RustDedicated restart, restart Query and teardown. Both borrowed cleanup records
have SHA256 `2fdd77ff2d81f485debb12e7b947c03adfe86bd9c3c617e3c81fe4f84ab7ba52`,
reproducing all 183 prior files' hashes and UID/GID/modes, with no owned process.
Those task evidence directories are under `/root/codex/gameplay-d8-20261015`.
B1/B3 profile restoration uses each actual snapshot's count (nineteen for three
Linux watch/depth runs, twenty for the other recorded runs), not a fabricated
uniform twenty-file count.

Windows final gate manifest
`C:/Sandbox/Codex/D8Windows20261015/Final-Gates-Windows.json` has SHA256
`1d4429c0855f78977cc841ca1f21180d31b516fc3833258bacc81d988675d0d8`.
Its independent restoration record SHA256 is
`0ceeedfa60dc9b827243028802b81cf4202724078a0fa3f8255e55cf8a8452d6`:
all ten installed hashes match, with zero owned processes, runner leases,
test plugins, clone backup entries or held owner lock. The complete local
`CarbonLuau-D8-Windows-Evidence.zip` has 236 entries and SHA256
`0f3fbcb6a529f532245fe9a4ca042e93646e3495d145318f9a88e64f7833a81f`.
It contains redacted logs/scripts/maps and terminal profile metadata, not profile
database/save payloads or credentials.

Linux repeats the default production CSZIP with SHA256
`fbda6bcdaa09b35edd5164676a0399489ee81fc66f0e6ea8fa3a4f07390f4ce9`.
Its repeated unpublished development bundle is byte-identical, SHA256
`365606e1b91df639ffce1fcb6060ae48936e6e2c4f52bb0a966c3029bc59db56`.
Clean extracted installation compiles all 74 examples and passes actual-VM
GUI/Player/C4, installed persistence worker, fencing and restart checks.
Receipt `/root/codex/gameplay-d8-20261015/bundle-linux.log` has SHA256
`cee18120abf587c9d3c4cd18acd240df786b88665d01172e03bbd921dbd13716`;
the combined bundle gate takes 8.79 seconds. Per-host byte determinism is
qualified; cross-runtime Deflate-byte identity is not claimed.

Linux final schema/documentation/qualification tools pass in 32.29 seconds.
Its source-input manifest SHA256 is
`b40f5d5b98463009a4019a3ef54212bc9dff896958749645863224aded38136e`;
the final evidence manifest SHA256 is
`fe26defa34b398ab9ae58c1abdc49e0d3f2bcd5c6ff8263704e5ab05e68fd902`.
The 2,120,298-byte archived evidence has SHA256
`3ed5a71924a340f355a9ad54846e4b07882ff4014f16134e37e6cfb6c09cc89c`.
Final `Final-Restoration-Audit.json` SHA256 is
`ea72a486281ca403e4242787c6cd216b034de9327dbc66745d885a3c42e63ebf`:
all eight protected running container ID/name/image tuples are unchanged;
there are zero owned containers, Rust/task executables or runner leases. All
eight shared installed inputs reproduce prior hashes and UID/GID/mode; both
borrowed restorations reproduce all 183 files and metadata before lease release.
Task evidence remains under `/root/codex/gameplay-d8-20261015` and its local
archive copy under `C:/Users/aiden/.codex/Temp/GameplayD8Linux`.

The final worker overlay covers 56 intended files, tar SHA256
`52d74f3a5970a143cd547fad23e4114ae4093342db0362e5461d332a53f479c5`,
with exact file-hash manifest
`2984c5a868df4745fdc3978b5c6e5e1a0aab4b3a1ad7062ab9d9261a0534d359`.
No native, managed-runtime or script input changes after its earlier passing
tests; both hosts verify the overlay. Subsequent closure changes affect this
report and the Python workflow shell only. Final hosted CI rebuilds and tests
the pushed source, including those final documentation/workflow bytes.

Recommended next phase: separately investigate the longer canonical item names
excluded by the retained fifteen-character/1122-code-unit descriptor budget.
Any larger name envelope requires its own bounded retention and host qualification;
this phase does not authorize that change or another prefab.

## Hosted source closure

Implementation, tests, API, examples and complete evidence commit:
`3650dc06b1b37663eaec09bb040402830edfc2fd`. All four workflows PASS on that
exact source:

- [Core validation 38706533335](https://github.com/gmoddev/CarbonLuau/actions/runs/38706533335):
  Windows/Linux native and managed/real-VM, lifecycle/scale/persistence,
  allocation and failure recovery, Linux sanitizers, all 74 examples,
  deterministic development packaging and clean installation.
- [Tooling 38706533275](https://github.com/gmoddev/CarbonLuau/actions/runs/38706533275):
  Windows/Linux/macOS SDK10, finite manifest/receipt/profile/ledger gates,
  generated metadata/types, preview goldens and hostile supervisor checks.
- [Contracts 38706533341](https://github.com/gmoddev/CarbonLuau/actions/runs/38706533341):
  finite public projection, schemas, documentation/rules/fragments,
  read-only release negative fixtures and relocated document navigation.
- [Documentation 38706533245](https://github.com/gmoddev/CarbonLuau/actions/runs/38706533245):
  generated reference build and deployment.

The first evidence closure, `bc73ee9077dc69f13ce8359f7edc5ed451143dc4`,
changes only this report. Its CI negative and the resulting diagnostic-only
runtime-test change are retained below. Production source, API/generated
metadata, examples, live fixtures and the three qualification artifacts remain
byte-identical to the tested implementation. The closing diagnostic/evidence
commit is independently subjected to all four workflows on its own SHA before
the final verdict; the final response records that SHA and its workflow links.
No pending job is counted as PASS.

Main is synchronized and tracked files are clean. The pre-existing `.codexlock`
and `derived-369547800317500/` remain untouched. Both hosts and borrowed test
servers are restored, with no owned processes, containers or leases left.
Read-only GitHub inspection confirms immutable published v0.7.0 tag object
`7c7d36f648a5506efd07c1b6d6bcf4b94d3f7d2b` and all six release-asset
content digests unchanged from the [D7 closure](GameplayD7-Validation.md#hosted-source-closure).
No release, tag or version-identity change is made.

### Retained closing-source CI negative

The first evidence-only closure's Windows core job fails its existing
`Private sampled inventory candidate (Windows)` step. The exact
[failed job 116333604423](https://github.com/gmoddev/CarbonLuau/actions/runs/38708449523/job/116333604423)
reports `old PlayerInventory acquisition still scan/watch free` at
InventoryNetObservationTests.RealVm. The downloaded 65,131-byte raw job log
`C:/Sandbox/Codex/D8Control20261015/CI-bc73ee9-Windows-failed.log` has SHA256
`e6387c375980279a66ae641f292bc26cd66b97cb0f3d202179fd76855aa7f7d9`.
That assertion combines successful Host.Reload with zero scans and zero watches;
the original message records none of those values. Its output cannot identify
which predicate failed. The original failure's cause remains unknown.

The test now captures the same single Reload result and includes its status,
error and the two counters in the assertion message. Its three success
predicates, execution/compiler deadlines and resource limits are unchanged;
there is no automatic retry or warm-up added to the test. Focused remote
Windows investigation passes the original test and six fresh diagnostic
processes (0.484–0.505 seconds), establishing OK/zero scans/zero watches for
those runs. These positive runs do not explain or replace the earlier negative.
The local receipt `C:/Sandbox/Codex/D8Windows20261015/CiInventoryInvestigation/Focused-Results.json`
has SHA256 `42fec079a2c0a46e78ebb35742acd2ab37175d7a49ef89d823fa503eedf37d85`
and records each process's exit code, duration and raw-log hash.
No timeout, cold-start or inventory defect is asserted without evidence.
The diagnostic-only source and updated report then receive the full hosted
matrix on their final commit; a pending or failed workflow is not a final PASS.
