# Gameplay D4 — offline loot expansion research

Scope note: this is the preserved offline baseline, not current implementation
eligibility. Subsequent [D4 live qualification](../GameplayD4-Validation.md)
implements the exact second autospawn barrel; this research's unqualified
verdicts, negatives and deferred item/registry proposals retain their original scope.

Status: **RESEARCH COMPLETE; NO ADDITIONAL PREFAB OR ITEM QUALIFIED**.
Repository baseline: `dabb872094f74e60d75de099379d198fb63a6bf8`, published
`v0.7.0`; research date: 2026-10-12. This report proposes later work and changes
no accepted invariant, runtime, allowlist, public API, identity or release asset.

## Existing qualification and authority

[I1–I12](../Invariants.md), [D7/D10/D11/D13](../invariants/Runtime.md),
[D18](../invariants/Player.md), [D20](../invariants/WorldEntity.md),
[D23](../invariants/Gameplay.md) and the
[qualification policy](../qualification/Policy.md) remain authoritative.
[D2A](../GameplayD2A-Validation.md) already qualifies the private strict
destruction-payout adapter; [D3](../GameplayD3-Validation.md) qualifies its public
binding. [Release 0.7.0](../Release-0.7.0-Validation.md) owns the combined release
evidence. These are retained receipts, not tests executed again for this report.
Historical [D0](../history/gameplay/GameplayD0Architecture-Research.md) supplies
candidate names and rejected alternatives; its original one-barrel blocker is
settled by D2A/D3 within their recorded envelope.

Public support is still exactly
`assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab`, ID
`966676416`, and `scrap` / `metal.fragments`. The signature stays
`Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)`.
Registration remains initialization-only, copied, staged and selected only after
publication. One global rule and one rule/domain, eight entries, 0..1000 per
entry, sum(Max) <=8000, eight worlds/occurrence, eight occurrences/actual frame
and one in-flight frame remain unchanged. Excess before COMMIT leaves vanilla;
suppression begins COMMIT; later uncertainty allows partial effects without
vanilla fallback, replay or compensation.

D2A's exact normal source frame, D20 original completed-Spawn identity, B3
original removal certification, factory-return capture, reciprocal Item/world
ownership and supported cleanup remain prerequisites. Rules do not confer
ownership of the source or accepted world payouts. B2 attribution remains
original RPC melee-only; ItemChanged remains sampled net observation. Neither
is a replacement-payout receipt, an exact breaker policy or a transfer API.

## Evidence inputs and reproduction

Read-only backing inputs are Rust build `25653776`, protocol `2634.289.1`,
revision `166494`. Retained loaded qualification uses Windows Carbon
`2.0.262.0` / `8a81d70`, Linux `2.0.261.0` / `c74c4ca`, Harmony `2.4.2.0`.

| Backing input | SHA256 | MVID |
|---|---|---|
| dockerbox `D:/Sandbox/Codex/Entity1AStartup/server/RustDedicated_Data/Managed/Assembly-CSharp.dll` | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e` |
| BigKVM `/root/codex/world-movement-20261003/server-linux/RustDedicated_Data/Managed/Assembly-CSharp.dll` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` | `616082a0-36f4-4680-a1ab-efc5766796cc` (new copy inspection agrees with D2A) |

The focused new [offline inspector](../../tools/Research-LootExpansionOffline.ps1)
uses the existing Windows worker's
`server/carbon/managed/lib/Mono.Cecil.dll`. It accepts only the two backing
assembly hashes, traverses metadata without resolving/loading Rust dependencies,
records exact instance signatures and nearest method owners, and optionally
records bodies and direct reference sites. Its Memory parameter set permits
inspection of remotely read bytes without creating an assembly copy on disk.
Only Cecil executes; no game method, Unity object, host patch or server executes.
Cecil SHA256 for these inspections is
`c41bdb9ffd3c5f6e17d2382c1012d73703e035e3f1100245fdd4e08c8dc6eb5b`.

```powershell
./tools/Research-LootExpansionOffline.ps1 `
    -AssemblyPath 'D:/Sandbox/Codex/Entity1AStartup/server/RustDedicated_Data/Managed/Assembly-CSharp.dll' `
    -CecilPath 'D:/Sandbox/Codex/Entity1AStartup/server/carbon/managed/lib/Mono.Cecil.dll' `
    -Body -References
```

Run this on dockerbox with the script supplied in memory or an independently
authorized research checkout. For Linux bytes read through the controlling
PC's existing BigKVM SSH profile, pass `-AssemblyBytes` instead of `-AssemblyPath`;
the same SHA256 check precedes Cecil loading. Neither mechanism runs a live
runner. JSON-lines output is reproducible external evidence; proprietary method
bodies and vendor binaries are not added to this repository.

The repeatable checker uses the existing hash-verified Linux copy on dockerbox;
its hash was also compared with a new read-only `sha256sum` of the BigKVM original.
Use the same Cecil path above and add:

```powershell
./tools/Research-LootExpansionOffline.ps1 `
    -AssemblyPath 'D:/Sandbox/Codex/Entity1AStartup/server/RustDedicated_Data/Managed/Assembly-CSharp.dll' `
    -CompareAssemblyPath 'C:/Sandbox/Codex/GameplayA-20261008/HostEvidence/Assembly-CSharp-Linux.dll' `
    -CecilPath 'D:/Sandbox/Codex/Entity1AStartup/server/carbon/managed/lib/Mono.Cecil.dll' `
    -Body
```

Comparison forces reference collection and requires both distinct pinned hashes,
their MVIDs and measured inventory counts. It checks exact selected method records
(tokens, sizes, instruction/EH counts, text hashes, call lists and requested bodies),
effective signatures, inheritance chains and fields. Every reference compares its
symbolic caller, IL offset, opcode and callee. Unselected callers' OS-specific token
differences are counted separately. A mismatch throws a console error and emits no
Parity PASS. Successful comparison emits two Assembly/Summary pairs and one compact
Parity record. Inspection without CompareAssemblyPath retains the original output.

`IlTextSha256` below means SHA256 of UTF-8, no BOM, LF-joined
`Mono.Cecil.Cil.Instruction.ToString()` lines, no final LF. It includes resolved
operand names and offsets. It is **not** a raw loaded IL hash, a universal
semantic-equivalence proof or a replacement for the production loaded pins.
Direct references are syntactic call sites, not exhaustive transitive execution
coverage of interfaces, delegates, components, analytics or trusted plugins.

## New static findings: effective classes and source paths

The Windows backing inventory contains 7,666 types including nested types,
87 types in the StorageContainer inheritance family and 11 in the LootContainer
family including the base. These are CLR type counts, not prefab counts.
There is no separate barrel-named type in this inspected LootContainer family.
The actual class of any new barrel remains an asset/instance question.

All 11 resolve `Die(HitInfo)` to BaseCombatEntity `0x0600149b`,
`DropItems(BaseEntity)` to LootContainer `0x06005772`, and
`DropBonusItems(BaseEntity, ItemContainer)` to LootContainer `0x0600576d`.
All resolve `ShouldDropItemsIndividually()` to LootContainer `0x06005771`,
whose original body returns true. Effective method lookup is exact-signature
metadata evidence, separate from current Harmony topology and prefab components.

| CLR class | Effective death/removal/fill distinction | Expansion treatment |
|---|---|---|
| LootContainer | Base qualified OnDied `0x06005773`; inherited empty OnKilled `0x06005176` | Prefer another asset proved to have this exact concrete type |
| FreeableLootContainer | Same selected death/drop bodies; own ServerInit `0x06002770` | Asset and release/freeing lifecycle qualification required |
| HackableLockedCrate | Same selected death/drop bodies; own ServerInit `0x06002836` | Lock/hack lifecycle and assets separately scoped |
| LockedByEntCrate | Same selected death/drop bodies | Relationship/configuration evidence still required |
| LootContainerAchievement | Same selected death/drop bodies | Class inheritance alone does not certify achievement callbacks |
| NaturalBeehive | Own OnDied `0x0600424f`, SpawnSwarm before base death | Special outer source; exclude from plain-barrel phase |
| SingleUseMissionStorageContainer | Own OnKilled `0x0600351c`, plus independent Cleanup_DropItems `0x0600351e` | Independent payout/removal path; exclude |
| Stocking | Inherited death/drop; own SpawnLoot `0x060040c7` | Fill/generation qualification differs |
| SupplyDrop | Inherited death/drop; own ServerInit `0x06005897` | Not inferred to be a barrel or ready for support |
| VisualStorageContainer | Inherited death/drop; own PopulateLoot `0x060058c2` | Distinct population path; asset qualification required |
| WhitelistLootContainer | Same selected death/drop bodies | Whitelist/configuration qualification required |

The original common chain remains:

```text
BaseCombatEntity.Die -> TryDropCorpse [before OnDied, outside payout cut]
  -> effective OnDied
     LootContainer.OnDied -> StorageContainer.OnDied
       -> virtual DropItems -> LootContainer.DropItems
         -> StorageContainer instance wrapper -> StorageContainer static cut
            -> bonus + individual stored items OR container/bag payout
       -> DecayEntity.OnDied -> debris [outside cut] -> base removal tail
```

New detail relevant to expansion: Storage's static original returns early for a
missing/empty item list (`IL_0007..001f`) or DropsLoot=false (`IL_0021..0028`).
The individual branch calls interface bonus at `IL_0053` only with a non-null
initiator, then DropUtil at `IL_005f`. The bag branch calls ItemContainer.Drop at
`IL_0093`, selecting ordinary/buoyant bag literals at `IL_006d` / `IL_0074`.
It does not call the bonus method in this body. Because all 11 original family
types select individual drops, bag coverage is a structural property of the
common cut, **not a new physical bag-branch test or a reason to allow all
StorageContainer subclasses**. Components or changed patches are separate gates.

LootContainer.DropItems marks existing Items' ownership at `IL_0030` before the
Storage wrapper call at `IL_0051`; the cut does not erase that earlier processing.
Storage.OnDied passes HitInfo.Initiator (`IL_0008`) into virtual DropItems
(`IL_000d`) and enters Decay.OnDied only afterward (`IL_0014`). This internal
initiator use is not B2's public actor authority and adds no conditional rule.
DropBonusItems first calls Storage's bonus body (`IL_0003`), then performs
modifier accounting before fresh ItemManager.Create (`IL_00e9`) and Item.Drop
(`IL_0125`). Suppressing only stored contents would miss that fresh payout;
the common cut precedes both. None of these calls proves a candidate asset's
scrapAmount, actual contents or modifier configuration.

NaturalBeehive.OnDied calls SpawnSwarm at `IL_0001`, then LootContainer.OnDied
at `IL_0008`. SpawnSwarm performs CreateEntity (`IL_0021`) and Spawn
(`IL_003c`) outside the current Loot outer frame. This proves a preceding entity
effect; it does not prove an item payout or the identity/configuration of the
referenced swarm asset. A fence around the base method cannot certify completion
of an unqualified outer method merely because that base body returned.

SingleUseMissionStorageContainer.Cleanup_DropItems calls virtual DropItems with
null initiator at `IL_0002`, then Kill at `IL_000a`, without LootContainer.OnDied.
The existing exact-frame rule intentionally does not replace this path. Its
OnKilled invokes mission processing (`IL_003c`) and mission cleanup; transitive
mission rewards are not inspected or declared harmless here.

The assembly-wide reference inventory also finds Storage payout calls in
ContainerIOEntity (`0x06002385`, `IL_0002`), IndustrialCrafter (`0x0600294e`,
`IL_0002`) and vehicle DoServerDestroy methods, for example TrainCar
(`0x06003804`, `IL_0064`). A global unconditional Storage cut would therefore
change unrelated producers. Reuse must keep exact source-frame and original
container identity guards; never turn the cut into a global loot interceptor.

### New method evidence

Windows and Linux backing measurements below use the text-hash convention above.
All 18 rows were checked against Pasteur's preserved successful inspection output;
the final checker proves equality of all 71 selected methods on the pinned pair. Sizes
are IL bytes; all rows have zero EH except LootContainer.DropItems (one).
The production raw/loaded hashes remain in D2A and are not overwritten.

| Method | Token | Bytes / instructions | IL text SHA256 |
|---|---|---|---|
| LootContainer.OnDied | `0x06005773` | 83 / 31 | `1f52442bc84b6a912757de77c16410cde09053f50d66a1da2b00d90ab02ebafd` |
| StorageContainer.OnDied | `0x060036fe` | 26 / 12 | `dff1df514fce1fea50d2f1d914311e6d3914b91f003e826c97ed80324b2a6053` |
| LootContainer.DropItems | `0x06005772` | 87 / 33 | `ee247ebe62cbe28dbb26fdfe02bc5e7bf30c41738387d88488924e20f557c2a8` |
| StorageContainer.DropItems, instance | `0x06003700` | 8 / 4 | `2e8acf9cc333a4d58c6e1d8e1739d848d5b4bec6be449402ab321e0bacbe050a` |
| StorageContainer.DropItems, static | `0x06003701` | 160 / 62 | `e9a2d71ee622473b58461ee871e7c925cfff253fe5f758d3ea785879ed86849c` |
| LootContainer.DropBonusItems | `0x0600576d` | 310 / 119 | `1d548002a61b1bfa9fbe5c472c2b150900821754e2d7fd7567e79a3c6cd1e6c5` |
| LootContainer.ShouldDropItemsIndividually | `0x06005771` | 2 / 2 | `e8064c7da2a619313705e93367d3e085587b780edef5fbaba4c1db61c03c3edc` |
| NaturalBeehive.OnDied | `0x0600424f` | 14 / 6 | `5fefac463fdda8b293932aba996ce88d41e6050aba5685ab8d9755c775a071ca` |
| NaturalBeehive.SpawnSwarm | `0x06004250` | 66 / 21 | `6e6e4ccf837beff8970b07eebdac70d64a5854179c94233b18be0a5ddd97bec7` |
| SingleUseMissionStorageContainer.OnKilled | `0x0600351c` | 99 / 38 | `031f68d1c0a8704a60d963b73a4b481742b6d0023d719df797807d05487c1e80` |
| SingleUseMissionStorageContainer.Cleanup_DropItems | `0x0600351e` | 16 / 8 | `fa5e5cc0338b6ff371e84641b3e2418f62608c19946a852178d3f95983422b5d` |
| ItemManager.Create | `0x06006c6d` | 179 / 66 | `c934fe2c636c513c99865ce5fc1002ac52aead243b2f20b8d24bb326a917993f` |
| Item.OnItemCreated | `0x06006a19` | 43 / 25 | `870f8ff35941a2485d1669d43aac2ce08a91f97deac3110e5b8a4524b9114130` |
| ItemModAssociatedEntity.CreateAssociatedEntity | `0x06006b25` | 128 / 43 | `2c8247b31b3f78edd5e88e8ada9d1cecc4956c5b859e21019fdb7983c55d940c` |
| ItemModContainer.CreateContents | `0x06006b69` | 325 / 107 | `d1d6706640ca00bf438f52dda33358f7c904e6e724e5a9d0c3edeeb009448cff` |
| ItemModEntity.CreateEntity | `0x06006ba3` | 186 / 71 | `f42786d513afd4c41fa22a4704d5b40954903d7e1838b564be9321302fd018eb` |
| Item.CreateWorldObject | `0x06006a37` | 121 / 52 | `e7bde81fbccbd24b8a8bc831f10ec34f5a42fedd6ce01976fb4446add0fa269d` |
| ItemDefinition.GetWorldModel | `0x06006af9` | 90 / 41 | `a04de78c44c091ec07592ece8dfddc913079873474b3045abea2469ed3e52d5e` |

## Prefab assets: explicitly separate evidence

No new prefab bundle, serialized component tree, GUID resolution or runtime
instance configuration was inspected. D2A's retained asset observation for the
supported barrel establishes its Transform/Spawnable/LootContainer roots,
scrapAmount=2, dropsLoot=true, no deployable corpse and empty debris GUID. This
receipt does not establish corresponding settings on another barrel.

| Full candidate path | ID from historical D0 only | Current asset evidence / recommendation |
|---|---:|---|
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab` | 555882409 | Unknown; first D4A candidate, conditional on exact plain-type/config proof |
| `assets/bundled/prefabs/radtown/loot_barrel_1.prefab` | 3364121927 | Unknown; separate asset variant, later |
| `assets/bundled/prefabs/radtown/loot_barrel_2.prefab` | 3269883781 | Unknown; separate asset variant, later |
| `assets/bundled/prefabs/radtown/oil_barrel.prefab` | 3438187947 | Unknown; oil/fuel hypotheses are historical, not a measured content table |

A later asset receipt must pin bundle/serialized asset bytes, extractor/version,
full path and actual prefabID, exact concrete type, root/child components,
InitShared cache, corpse/debris references, inventory ownership/capacity,
dropsLoot/dropFloats/dropPosition/loot-destroy settings, scrapAmount, lootDefinition
and lootSpawnSlots, refresh/initial fill callers, and referenced assets. Pin
Windows and Linux content separately, or establish shared bytes by hashes.
Reconcile saved/restored and fresh instances in later authorized live tests.
Asset settings select IL branches; IL does not identify those settings.

The oil candidate does not automatically require a different engine: if its
actual source is plain LootContainer with no extra producers, the same cut may
cover its stored contents. Different replacement item definitions are a second
independent gate. Conversely, a plain class name cannot exclude component,
corpse, debris or generation-time outputs. Unknown assets leave that candidate
unqualified; they do not invalidate settled support for barrel-1.

## Additional item definitions and reuse boundary

The current adapter resolves each planned definition before COMMIT, requires
stackable >= declared Max and at most eight itemMods, then rechecks premises
during payout. It creates one Item per nonzero entry and drops that original
returned Item; it does not split oversized replacement quantities. A new
definition with a smaller stack limit must use a compatible range or be rejected
before COMMIT. Increasing entry/stack/world limits is separate work.

New static reason to reject unrestricted item names: ItemManager.Create enters
Item.Initialize at `IL_008a`; Initialize calls OnItemCreated at `IL_0042`;
OnItemCreated loops the asset-selected itemMods and virtually invokes each at
`IL_001b`. A mod-count cap alone cannot bound each mod's transitive host work.
ItemModAssociatedEntity can CreateEntity/Spawn before the Item returns
(`0x06006b25`, `IL_0031` / `IL_0046`), then publish instanceData.subEntity.
ItemModEntity similarly creates/spawns a held entity (`0x06006ba3`, `IL_0034` /
`IL_00ad`). ItemModContainer allocates/configures nested contents and callbacks
(`0x06006b69`, pool at `IL_0059`, ServerInitialize at `IL_0112`). These resources
are outside the existing one-Item/one-world factory capture. This is a class
capability finding, **not evidence that any candidate item has those mods**.

ItemModBurnable.OnItemCreated (`0x06006b3c`, text SHA256
`27a893ebe901aafeeedcab91248ef33f1211280764ba649ddf6bcbfafd59861c`)
only sets fuel in that body. ItemModCookable.OnItemCreated (`0x06006b94`, text
SHA256 `6ccd0e0c59df6033c173d6ed5ba8428a799f50f3a9a66f5d14a125cc785e2daf`)
sets cook time and subscribes a cycle callback; its downstream lifecycle is
not qualified by that short body. Neither finding establishes an item's mod list.

Item.CreateWorldObject uses the fixed `generic_world.prefab` literal
(`IL_0017`), initializes a WorldItem (`IL_004c`) and Spawns it (`IL_0066`).
ItemDefinition.GetWorldModel separately selects asset-configured visual models
by quantity. That lookup alone does not prove a different world entity class;
model/component lifecycle still needs definition/asset and host evidence.

| Proposed definition candidates, not new support | Required evidence before admission |
|---|---|
| `wood`, `stones`, `cloth`, `metal.ore` | Exact definition assets/IDs/stack limits, complete mod list and initialization/drop/cleanup behavior; qualify one at a time |
| `crude.oil`, `lowgradefuel`, `diesel_barrel` | Same evidence plus relevant fuel/cooking/associated-entity checks; do not infer actual oil-barrel contents or mod lists |
| Weapons, wearable/conditioned items, containers, associated/held-entity items | Exclude from the smallest phase; need bounded extra-resource ownership and lifecycle qualification |

Names above are research requests, not proof that the pinned definitions exist.
Items:Exists is an existence observation, not loot-adapter eligibility.
No skin, condition, instanceData, attachments, nested contents, metadata cloning
or public ItemDefinition access is proposed. Keep the existing exact canonical
short-name contract and `{Item, Min, Max}` entry shape.

## Ownership, conflicts and smallest reuse design

Current code is deliberately single-prefab throughout: native string validation,
LootRulePolicy.Validate, PublishedLootRule's fixed key, global conflict preflight,
TryLootRule and EnterLoot's exact type/name/ID checks. An allowlist-only patch
would miss selection/publication and physical admission. No such patch is made.

For D4A, retain **one global rule total and one/domain**, selecting one of two
separately qualified plain prefab profiles. A candidate may replace only its
exact outgoing owner's rule; failed module/candidate publication restores prior
CarbonLuau-owned policy. A different owner's same-prefab declaration conflicts.
A different-prefab declaration also cannot coexist under the retained global
capacity of one; it is a capacity rejection, not evidence of shared prefab
ownership. Test both cases explicitly rather than silently relaxing the bound.

If separately requested later, a finite per-prefab registry can permit disjoint
owners with ordinal full-path keys and bounded total/domain capacity. Preflight
the complete candidate set before either native/managed publication, exclude
only its exact outgoing owner, reject every overlap with another committed
owner, and select exactly one immutable descriptor at occurrence entry.
Malformed or conflicting sets must not partly publish. Provider unload, required
dependency retirement, nested module failure, foreign facade/closure laundering
and fatal reconstruction preserve D7/D10 and no physical replay. This registry
extension is not necessary to qualify a second source and is deferred.

Reuse D2A's payout plan, common cut, genuine returned-world capture, D20 witnesses,
B3 verification and no-compensation cleanup for a proved plain second profile.
Keep the producer budget shared across prefabs; do not multiply per-frame
allowances by prefab/addon. Unsupported profile/configuration leaves vanilla
before COMMIT. Common assembly/topology/observer loss still invalidates the
shared source gate; a new profile-specific asset failure must not disable an
independently qualified profile or unrelated services. No new scheduler,
callbacks, attribution or post-COMMIT fallback is introduced.

## Rejected alternatives

- Substring or ShortPrefabName matching, historical IDs as current proof, or
  admitting every LootContainer/StorageContainer derivative: asset settings,
  outer completion and independent payout paths are not equivalent.
- Unconditional static Storage suppression: affects unrelated IO/vehicle and
  cleanup sources without an admitted original Loot frame.
- OnLootSpawn/fill replacement, OnContainerDropItems alone, or late bonus-hook
  veto as strict Replace: preserved D2A evidence already excludes these as a
  complete destruction-time suppression mechanism.
- Broad item-definition resolution or increasing bounds to accept complex mods:
  existence and mod count do not prove bounded creation or resource ownership.
- Post-drop cloning/nearby collection, inventory delivery, breaker-dependent
  rules or ItemChanged-based success: materially different APIs/attribution and
  ownership contracts; sampled changes cannot prove a loot transaction.
- Global engine redesign or special-class patches before a second plain asset:
  the new source inventory gives a smaller separately measurable candidate.

## Implementation and offline validation

Changes are only this report and Research-LootExpansionOffline.ps1. The inspector
is justified by the missing exact-signature inheritance inventory and item-mod
side-effect evidence; the existing Research-BarrelLootHost does not inventory
all Loot derivatives/effective dispatch. Production files, shared indexes,
released tags/assets and other agents' files are untouched. CodexLock owns only
these two paths; unrelated untracked files and historical evidence are preserved.

Pasteur's script, report and successful Windows command output were preserved.
Completion adds only comparison mode and final receipts; no research restart,
new runtime infrastructure or declaration implementation was needed. New final
backing comparison ran on Windows dockerbox; Linux means inspection of the pinned
Linux binary there, not execution under Linux/Mono or a live Linux host.

| Exact offline check | Result / measured scope |
|---|---|
| Windows/Linux backing SHA256 and MVID | PASS; inputs above, Linux original rehashed on BigKVM and existing worker copy inspected |
| Inventory | PASS; Windows 7,666 / Linux 7,668 total types; each has 87 Storage family types, 11 Loot family types, 143 effective signatures, 32 field records, 71 selected bodies and 234 direct references |
| Selected source parity, with Body | PASS; all 71 tokens/sizes/instruction counts/EH/text hashes/calls/bodies, all 143 signatures, 87 inheritance chains and 32 field records match |
| Reference parity | PASS; all 234 symbolic caller/offset/opcode/callee records match; six sites in five unselected callers have OS-specific caller-token differences |
| Valid Memory input plus Path comparison | PASS; Windows bytes read once into Memory, existing Linux copy read by Path; pair inspection/comparison took 10,915 ms including JSON conversion, excluding SSH transport |
| PowerShell parser and invalid Memory/Path hashes | PASS; Memory bytes `[1,2,3]` and Path pointing to the inspector's own source both reject before `Missing-Cecil.dll` can load |
| Duplicate backing input | PASS; using the Windows DLL as both inputs raises the distinct-pinned-assemblies error and emits no Parity PASS |
| Preserved report fingerprints | PASS; all 18 token/size/instruction/text-hash rows agree with recovered successful Windows evidence |
| Documentation and architecture | PASS; 234 documents, 3604 links/fragments, 97 retained spans; existing structural architecture/GiveItem checks pass |
| Owned-path navigation and whitespace | PASS; 11 report-relative links resolve; no trailing whitespace or owned-path diff-check errors |

No inventory count is a gameplay work budget. Both readers dispose assembly and
Memory stream in finally. The existing Path/Memory inspector plus comparison mode
is the reusable offline infrastructure; no separate runtime fixture is needed.

The initial command-line-encoded invocation exceeded Windows' command-length
limit; supplying the inspector through stdin corrected it. A first unwrapped
large Linux base64 transport was stopped without producing parity evidence.
These are research transport limitations, not host or payout failures.
The wrapped Linux transport then read 13,987,608 base64 characters but exited -1
without parity output; it is also not PASS. Completion reused the existing worker
copy rather than repeating that transport. The first new comparison rejected raw
Reference token equality: for example ConVar.Debugging.dropWorldItems is Windows
`0x06009f99` / Linux `0x06009f9b`, while both call sites remain at `IL_005c` and
`IL_006c` with identical callees. Focused metadata inspection confirmed the token
difference; the final checker compares symbolic reference sites and records these
differences, while retaining exact token comparison for all selected methods.

No native/managed build, live test, server launch, plugin installation, server
configuration change or publication occurred. Backing analysis does not prove
the newly inspected methods' loaded Carbon transformations, patch history,
physical output counts, cleanup behavior, CPU bounds or client observations.
No commit, push, index edit or CI dispatch was performed by this workstream.
The baseline above identifies the preserved research source; unrelated concurrent
work may advance checkout HEAD. No task file was staged and no task-owned persistent
process, listener or server was started.

## Remaining live gates and exact next phase

**Next phase: D4A — privately qualify only
`assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab`, retaining
scrap/metal.fragments and all current global/domain/producer limits.** First
obtain its exact Windows/Linux asset receipts. Proceed only if it proves the
same plain concrete type and a compatible no-extra-producer configuration;
otherwise record why it failed and stop that candidate. Do not silently
substitute a special class or broaden the phase. D4A is proposed, not begun by
this offline task. Public support changes require a later authorized closure.

| Separately authorized gate, required on both pinned Windows/Linux hosts | Acceptance evidence |
|---|---|
| Fresh/startup-restored asset/profile and loaded source/topology | Current full name/ID/type/components/settings, unchanged observer continuity, loaded pins and eligible outer death completion |
| Baseline physical source accounting | Stored/prepopulated, refreshed/restored, empty, normal/boosted bonus, nil initiator, direct Kill/cleanup distinction; identify every output and earlier generation leak separately |
| Strict replacement and scope isolation | Empty/all-zero, fixed/range/max entries, original source retirement, nearby barrel-1/2 and unrelated storage; no default payout on verified eligible occurrences |
| Ownership/publication | Same-owner replacement, same-prefab foreign conflict, disjoint-prefab global-capacity rejection, failed nested module/candidate, provider/dependency retirement and fatal recovery; no history/replay |
| Partial failure and original identities | Create null/throw, real late Spawn failure, source Kill veto, returned Item mutation, ID/UID/epoch reuse and cleanup veto; preserve accepted worlds and record uncertainty |
| Shared resource envelope | Eight worlds/occurrence, eight occurrences/actual frame, one in-flight frame, nested source rejection, bounded definition/component checks and owner-thread measurements; same-frame profile replacement must not reset the shared budget under the retained one-rule limit |
| Affected regression and closure | Existing barrel-1 behavior, B1/B3/B2 and sampled ItemChanged availability, public conversion/publication regression, affected native/sanitizer and packaging/metadata checks as required by the actual later code delta |

These live gates have **not** run here. Synthetic/offline success cannot satisfy
them. No authenticated-client pickup/rendering, long-term world persistence or
unreturned host-internal allocation cleanup is promised. Keep new-item admission
in a subsequent separately scoped D4B: first one proved plain definition on an
already-qualified source, with its own physical creation/drop/cleanup and bounds
receipt. Oil assets, simultaneous per-prefab owners and special class paths stay
independent later decisions.
