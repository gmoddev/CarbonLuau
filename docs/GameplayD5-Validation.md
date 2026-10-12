# Gameplay D5 — Loot item expansion qualification

Status: **GAMEPLAY-D5 PASS — ADDITIONAL LOOT ITEMS QUALIFIED**.
Starting main: `4cef0d0f5571784616e36692548c82c7a8d11405`.
Date: 2026-10-12. This extends the existing service within
[D2A](GameplayD2A-Validation.md), [D3](GameplayD3-Validation.md),
[D4](GameplayD4-Validation.md), [D23](invariants/Gameplay.md),
[D20](invariants/WorldEntity.md) and D13/I12. Exact prefab interception,
completed Spawn authority and physical Item/world ownership are reused.

## Scope and registry decision

The preferred sole candidate is `wood`. The public API, modes, prefab allowlist,
`scrap`/`metal.fragments`, numerical budgets and package/API/ABI/provider/schema/
Luau identities are unchanged. No new release or tag is authorized. Published
v0.7.0 tag/assets remain immutable and exclude subsequent D4/D5 development.

The optional per-prefab registry materially expands publication/cardinality and
domain ownership qualification. It is recorded as a separate
[D6 proposal](design/GameplayD6-PrefabRuleRegistry.md). D5 keeps one global rule
and one per domain: only its exact matching profile is replaced, with the other
profile remaining vanilla even while both sources are active. Per-frame producer
use remains shared across profile replacements; independent simultaneous rules
are not claimed.

## Candidate proof and staged implementation

The initial test-only package uses the unchanged D4 production/native source and
directly creates/drops/cleans the candidate while public validation stays closed.
`IncludeD5Fixtures` builds on the D4/D3 fixture; `D5ProbeOnly` exits after probe
cleanup without declaring a public wood rule. Production packages exclude both
selectors, injected faults, qualification code and state.

Require both pinned host proofs of exact name/ID lookup identity, stack limit,
condition and root/mod configuration, scalar initialization, quantity-specific
world type, original UID and reciprocal Item/world references, completed Spawn
epoch and exact original removal/cleanup before opening the allowlist. Then run
actual copied public declarations and the full failure/publication/recovery
matrix, minimum/zero/range/max amounts, eight duplicate stacks, mixed supported
entries, both profiles and controlled stack-premise rejection. Both corrected
final public matrices and final-source hosted gates PASS as attributed below.

## Exact candidate proof — both live hosts PASS

The corrected probe package SHA256 is
`82c28cca4782159f1f69d46aee2a77fcdbb85d549346495477acb64b5685723c`.
This is the direct physical candidate gate, not public declaration qualification.

| Host | Actual probe receipt | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261012-155601-813/server.log` | `95a2eba2b750a993a0c49adc95be42a0ea8e992c962311379e0969cbf73ae7de` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261012-155603-1849609/server.log` | `aa0f3c4ed41906f1f6303dc33a20c653347ef33b44daeb7e96a39ed9562f79c2` |

Both hosts measured `wood`, item ID `-151838493`, identical name/ID lookup
references, stackable=1000, disabled condition and exactly one each of the three
mods below. Fresh actual amounts 1/50/1000 have skin=0, condition/maxCondition=0,
fuel=10 equal to the configured scalar, no contents/instanceData/held entity.
Each returned original Item drops into a real `DroppedItem` using the unchanged
generic_world prefab. Its roots are Transform, DroppedItem, PhysicsEffects,
EntityCollisionMessage and Rigidbody at all three amounts. Original UID,
reciprocal Item/world references, original keyed world and completed Spawn epoch
are proved before exact cleanup; every ledger returns to zero and original
world/Item removal is verified. Neither proximity nor current-ID attribution is
used. This authorized the smallest two allowlist checks; final public verification
now independently passes as recorded below.

## Final public physical qualification

The combined D5/D4/B2 fixture package SHA256 is
`4be815aa1c7fc75e3db839889e2ff853d2743770a2e21521af928c192b12706a`.
Declarations use the real public service and copied tables, then mutate/clear
the original input. No managed descriptor is substituted for public admission.

| Platform | Final actual Carbon receipt | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261012-161442-958/server.log` | `93b93b46be87b5d93eeec4dd25e54a073a0d60fb10e1fbb8a65fb1caacfecb13` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261012-161511-1859586/server.log` | `ea00b9e797cc3b57b6fe334265361da303f033e0c47ce78cd2a1c0019dd67a2c` |

Both PASS wood min=1, zero-only and mixed zero entries, inclusive range 50..100,
max=1000, eight separate 1000-unit wood stacks and mixed scrap/metal entries on
each exact profile. Every accepted returned world has the original Item/UID,
exact planned definition reference/name, reciprocal references and D20 witness;
successful receipts also prove exact planned amounts and certified original
source removal. Eight repeated entries produce eight distinct worlds, not one
oversized stack or split-created resources. Range observations prove inclusion,
not RNG distribution. Wood 1001 and unsupported stones declarations reject
without replacing the old owner. A controlled actual wood stackable=999 against
declared Max=1000 rejects before COMMIT, leaves seeded vanilla worlds and restores
the original definition in `finally`.

The full inherited matrix targets wood for real late Spawn failure, returned
amount interference and second-creation null/throw dispatch. The first accepted
scrap world survives later failure. Source Kill veto, incomplete Spawn and
post-COMMIT uncertainty produce partial/indeterminate receipts without fallback,
replay, compensation or inaccessible unreturned-allocation claims. Exact
temporary cleanup remains mandatory; final cleanup_failed counters are zero.
Actual forced alternate bag baseline=1 holding original stored Items, replaced
bag=0 and intended worlds=2 prove the unchanged common cut. Natural bag behavior
and earlier generation-time drop reclamation remain unclaimed.

Failed/successful root and addon replacement, same/disjoint foreign-owner
conflicts, caught nested-module failure, provider unload, fatal VM reconstruction,
source ineligibility and same-frame exhaustion/profile switches PASS. Both
barrels coexist; only the selected rule's exact profile replaces, the other
remains vanilla. The final combined matrices also PASS real B1/B3 callback
availability and B2 original RPC/terminal-boundary controls after the corrected
fixture coordination described below. No authenticated-client result is inferred.

## Effective contract and limits

The API remains `Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)`.
Exact supported names are `scrap`, `metal.fragments`, `wood`; exact prefab
suffixes under `assets/bundled/prefabs/autospawn/resource/loot/` are
`loot-barrel-1.prefab` (966676416) and `loot-barrel-2.prefab` (555882409).
Short names, case variants, other items/prefabs and other modes remain rejected.

One global rule and one per domain; eight entries/worlds per occurrence;
integer `0 <= Min <= Max <= 1000`; sum(Max)<=8000; eight admitted occurrences
per actual Unity frame; one in-flight frame; at most 129 inspected root/addon
sessions. Each input table remains capped at 64 array and 64 hash backing slots;
descriptor transport remains 24 scalar fields / 1024 UTF-8 bytes. Limits do not
multiply by item, prefab or addon. Creation/drop/mod work uses the newly proved
wood path within the existing budgets; no numerical increase is justified.

Only the two allowlists and generated summary expand. All production host hooks,
physical adapter/verifier/cleanup, publication registry, lifetimes and execution
boundaries are unchanged. Package/API `0.7.0`/`0.7.0-experimental`, ABI `1.5`,
provider `1.2`, schema `1` and pinned Luau remain unchanged. SinceApi, signatures
and generated definitions retain their introductions. Wood/barrel-2 are
post-release development capabilities, absent from immutable v0.7.0 assets.

## Native, managed, tooling and affected regressions

Windows native SHA256
`ae7dd05b4fdff460601fd9db6182daac0fddf00abec25da9d4df70bbb4d50454`,
compiler `6e03b903809bfb335366db1121ca3d7762d5c1beda0e94a0e673c2ed6ad1c090`.
Linux native `df36d6cc21f3600a60ded6a5e82fe69941f1edbbd9352fcd4f857ea10bf99778`,
compiler `884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92`.
Windows MSVC 19.44/static CRT and Linux GNU 13.3 Release builds use parallelism
two with existing pinned dependency caches. Work/evidence roots are
`D:/Sandbox/Codex/GameplayD5` (existing approved Windows D: override) and
`/root/codex/gameplay-d5-20261012`. Container builds use two CPUs / two GiB;
the isolated Linux server uses the established four-GiB allowance. No machine
security, timers, toolchain installation or unrelated workload changed.

Final native suites PASS Windows 23/23 (108.72s), Linux 24/24 (129.43s).
Focused managed/pinned-VM policy/public suites PASS both platforms, including
exact wood spelling, mixed descriptor immutability, maximum cardinality,
publication failure/missing definition and all retained D3/D4 negatives.
Full actual-VM regressions PASS 100 atomic replacements / 200 failed candidates,
provider/module/GUI/inventory/persistence, timeout/memory recovery and scale;
Windows full-runtime 123.38s. Managed Persistence-1C combined lifecycle/scale,
real worker/lost-ack/derived IPC and loader teardown PASS both platforms.
The memory cap remains VM-wide, not a process or hard per-addon cap.

Separate unchanged public D3/B2 scrap/metal receipts:
Windows `loot-d2a-20261012-160138-311/server.log`, SHA256
`022d3c3bdc9e7f8a4947d3cb8287f6b4c2890fe17887c026e8c7cfaefe1ce492`;
Linux `loot-d2a-20261012-160652-1855772/server.log`, SHA256
`15ea473c04f276b8846acceda101225e9cf352c215663367e57be7b51255a14f`.
Separate default D4 scrap/metal matrices also PASS Windows `160733-996`, SHA256
`471b6aaf446313feb07c065c280227be876b1faa13261a0826cd3ec4b5305c51`,
Linux `160806-1856160`, SHA256
`00d699c07c821802175263d1d7e31211cb59d43f571f4fc954ccb6586c6dbbb1`.
All use the established `loot-d2a-20261012-` evidence roots above. Unaffected
earlier fixture variants remain scoped physical receipts; the final combined
package independently exercises the corrected shared fixture on both hosts.

Fresh C3/C4 sampled live receipts, under the same host evidence roots:
Windows `inventory-c3-20261012-161121-852/server.log`, SHA256
`60fb22436dbf290f2cfa6914e2699fc7605a8041f518f0655f83cb9cc1eee38a`;
Linux `inventory-c3-20261012-161117-1857767/server.log`, SHA256
`b0b7a3c898e56c247ad34309132409347085bf7b6fd58365cd0f735924a3d939`.
Sampled/intermediate-transfer and balanced-missed-change limitations remain.

Linux disposable persistence-2D receipt is
`linux-live-2d-20261012-201235` under the D5 root, SHA256
`da0bf8f42a11211262acdf7dc805346d909c2e8b3f3b0f2120de64a4c947d4b6`;
cleanup `ff062fb6f78d73b25d7d9eebb7a4a3f2ea5f66863354fbf5d21b2c5646d3df65`.
Actual Get/Set/Remove/Query, reload/restart and two acknowledged -9 exits PASS,
helpers=0/unmapped. Allocated bytes 118784 is an observation, not an allocation
or power-loss invariant. Windows actual namespace/restart supplement PASS at
`D:/Sandbox/Codex/GameplayD5/PersistenceEvidence/persistence1b-20261012-161549-44a626f5`:
seeded Root/A/B Get/Set/Remove counters 8/4/2, restarted read-only 3/0/0,
two acknowledged -1 exits, helpers=0 and FinalNativeUnmapped=True. Initial server
log SHA256 `439bdae1feebf24e19d3c64cc079b58618cb4a4d44febd9fb55beb555e38a519`,
restart log `95f5fa93a86e790b6234b38530cdb4ca40f3da7155e3e0606e5f6c6bf96e550d`,
restart proof `7254cc8e8758c97037f5eeaf08ca2c4d83ddc8abaae94eeff2601919e35905ad`.
Both disposable clones restored their prior installed trees and retain only
task evidence; there is no live fixture or helper deployment.

SDK10 authoritative generation/check-generated/type/drift and 17 preview-plan
goldens PASS; only API/docs summary metadata changes, `.d.luau` and tooling
metadata remain identical. Public wood preview remains UnsupportedPreviewApi,
with an added actual-preview regression for hosted execution, never a mock.
API/architecture/document checks PASS preserving 97 historical spans/I1–I12.
Both repeated development bundles are byte-identical and fixture/state-free;
Windows ZIP SHA256 `ca98e8a41e53f4f3bee60f2b46c93073a8943cd29b2562a2b011b8d7cb2700f2`,
Linux `fc766f3cfaa07870a269b80bbc495b5cd03eb9270c2a03e233b178ffffca7c14`.
Both clean extracted installs load the real VM, compile all 71 bundled examples,
and PASS private worker/import audit. These dirty-tree development artifacts are
unpublished; final-source CI packages the committed source separately.

Final live runners verify owned process exit (-1 Windows / 137 Linux), lease
removal and restoration of installed package/native/compiler/hooks/config hashes.
Disposable persistence process/helper/restore receipts remain separate. Retain
all source/build/evidence caches and negatives; exclude them from Git/packages.

Pinned Rust build 25653776 / protocol 2634.289.1 / revision 166494, Windows
Carbon 2.0.262.0 (`8a81d70`), Linux 2.0.261.0 (`c74c4ca`), Harmony 2.4.2.0 are
unchanged from D4. Both actual backing assembly hashes match D4. Targeted callback
inspection retains proprietary IL in external task receipts, not packages.
XP/Tax declare no override for creation, remove, parent-change, moved-to-world,
removed-from-world or changed callbacks; those base bodies are ret-only. The
existing host drop/removal ownership path therefore gains no extra mod-created
resource. No general item-name eligibility or consume/tax behavior is inferred.

## Preserved negative evidence

The first prototype incorrectly compared `ItemDefinition.Condition` with null.
The exact host exposes that field as a struct, and Carbon compilation rejected
CS0019. The fixture now checks `condition.enabled` directly. Both runners stopped
their owned process and restored installed package/native/compiler/hooks/config
hashes. These compile failures qualify no candidate and are not relabeled PASS:

- Windows `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261012-155209-961/server.log`.
- Linux `/root/codex/world-movement-20261003/evidence/loot-d2a-20261012-155227-1847971/server.log`.

The next probe found that wood has three mods, rather than the initial guessed
single Burnable mod: Burnable, XPWhenUsed and ApartmentTax. The restrictive probe
correctly stopped before public admission. Targeted exact assembly inspection
shows XPWhenUsed declares only its consumption-specific `GiveConsumeXP` and
constructor; ApartmentTax declares only its constructor. Both inherit the
ret-only base `ItemMod.OnItemCreated`. Burnable's 13-byte creation callback only
copies `fuelAmount` into `Item.fuel`. This justifies inspecting a scalar default
fuel value on this path without adding a public fuel API or qualifying unrelated
consumption/tax behavior. The corrected actual creation/world/cleanup proof
remains a separate required gate; static callbacks alone are not a physical PASS.

Combined-matrix negatives also identified fixture coordination defects:

- The final D5 root replacement cleared the prior state module's death count.
  The unchanged B2 assertion correctly rejected that fresh owner with no later
  death. A real payout now occurs after publication; no callback history is copied
  or replayed. Initial Windows receipt `loot-d2a-20261012-155943-815`, SHA256
  `793fb68650fc680cd214ee608978335f88e90da7d1215cb325d5b34ccff7a5eb`.
- Initial world cleanup followed only one bounded `Host.Drain`. On Linux the
  second original world could be destroyed before its queued EntitySpawned
  callback's fresh lifetime validation. The test then incorrectly expected two
  callbacks after making one ineligible. Both D5 and unchanged scrap/metal D3
  reproduced this. The shared fixture now retains its exact worlds through the
  existing sixteen-frame observation bound and cleans them only after the same
  positive assertions; no scheduler, predicate, deadline or public lifetime rule
  changes. Linux receipts `160115-1853787` (SHA256
  `1d7258e82f627f3a51c520b447ae03455e79ef6f393c2781af6b326f7dba43f5`)
  and `160312-1854687` remain negative evidence.
- D5's last step admitted six payouts; B2 then attempted four terminal cases in
  that same actual frame. The adapter exhausted its unchanged eight-occurrence
  budget and correctly left the excess vanilla. B2's replacement-only scene
  cleanup refused ownership of those original vanilla worlds. D5 now starts the
  independent B2 phase only after a bounded strictly new Unity-frame check, logs
  both frames and prior use, and never resets production accounting. Windows
  receipts `160339-154` (SHA256
  `f3acb1481aad50666689d0157323aa653cbb014d4ef4aef34a490e1b1586a15e`)
  and `160530-460` (SHA256
  `f0acabd1e494bc688b9369ceace8f83bc7c9af9097afd8e56a989c166b4a0bd1`)
  are preserved, including exact cleanup refusal. No external-interference cause
  or production ownership defect is inferred from these controls.
- The later B2 terminal-boundary fixture used a fixed barrel-1 label while the
  deliberately selected D5 source was barrel-2. It now snapshots the original
  case source's exact prefab before death and checks it against both the case
  and D20 lifetime record; the callback's exact equality assertion is unchanged.
  No generic two-name exception or ID re-resolution is introduced. Negative
  Windows receipt `160908-535`, SHA256
  `b101202b0832330c3cc861c69710bc89e686eeecf73670e135f6a746366f2b58`,
  and Linux `160922-1856852`, SHA256
  `15796a0f2eb86f4fc17b361ef1337105e00c521aaddc2a96fab7b1b1193abaa8`,
  retain all earlier successful D5 physical rows but are not combined PASS runs.

Linux full-runtime invocations initially used unsupported container overlay
storage and then the wrong working directory for bundled examples. Corrected
execution uses the qualified host-mounted temporary directory and source cwd.
Windows invocation/default-parameter harness errors likewise remain separate
negative logs; no production limits or implementation changed to resolve them.
The initial Windows persistence invocation combined Query2D and the namespace
restart supplement. Its actual Query controls passed but extra writes made the
namespace fixture's exact 8/4/2 counter expectation invalid (actual 8/7/2,
Query=4). Preserve `c7cdc0e2`, RCON SHA256
`c5d7f13bb470bc0bde45ceb35864b0cc2945a9ef32fc3be0948be78d4836a80a`.
The unchanged namespace-only rerun passes separately; no counter assertion or
production semantics were relaxed. Windows PowerShell 5 packaging lacks
GetRelativePath; the existing installed PowerShell 7 pipeline resolves that
harness failure without a toolchain change.

Implementation/platform/live/CI receipts retain their exact successful source
and artifact attribution below. No pending or synthetic
result qualifies physical host payout, authenticated-client behavior, arbitrary
host upgrades, macOS server support or power-loss durability.

## Hosted closure and source attribution

Implementation/tests/evidence and final tested candidate:
`d2fc21489d9a511c9204a32e709c04b13479795b`.
All required exact-host, native/managed/real-VM, tooling model/generation,
deterministic development packaging and clean-install gates above are complete.

- [Core validation 38303648464](https://github.com/gmoddev/CarbonLuau/actions/runs/38303648464):
  PASS Windows x64 and Linux x64 native, managed/real-VM, exact public Loot
  declarations/publication/providers/recovery, B1/B2/B3, C3/C4, persistence
  lifecycle/scale/lost-ack/worker/import, deterministic packages and clean
  installation of 71 examples. ASan/UBSan/leak/lifecycle/allocation-fault job PASS.
- [Tooling 38303648543](https://github.com/gmoddev/CarbonLuau/actions/runs/38303648543):
  PASS Windows/Linux/macOS developer tooling, deterministic generation/type/
  drift/contracts and the actual wood UnsupportedPreviewApi regression on
  Windows/Linux. No macOS server or mock payout qualification.
- [Contracts 38303649017](https://github.com/gmoddev/CarbonLuau/actions/runs/38303649017):
  PASS documents, retained historical evidence, API/schema/preview and links.
- [Documentation 38303648539](https://github.com/gmoddev/CarbonLuau/actions/runs/38303648539):
  PASS deployment of the candidate documentation.

All candidate jobs pass on their first attempts, with no failed/cancelled job
or CI rerun. Earlier host/fixture/harness negatives remain preserved above;
they are not silently relabeled or attributed to unproved interference.

The subsequent closure commit changes only this evidence record, preserves all
executable inputs and reuses these qualified implementation/live receipts under
Compatibility's documentation-only policy. Its own final main workflows and
exact SHA are verified and supplied in the completion report. Published v0.7.0
tag object `7c7d36f648a5506efd07c1b6d6bcf4b94d3f7d2b` and all six uploaded asset
digests were rechecked unchanged. No release/tag, ABI/provider/schema/API bump
or untested production edit is part of closure. Pre-existing `.codexlock` and `derived-369547800317500/` are
retained untracked; no fixture, raw IL, database/journal, host binary or credential
is committed or shipped.

Next recommended phase: separately scope and qualify the finite per-prefab
[D6 registry](design/GameplayD6-PrefabRuleRegistry.md), retaining the shared
producer budget and exact owner/set publication proof. Additional item names,
conditioned/held/container/fuel configuration, modes, other prefabs, attribution
and unrelated gameplay APIs remain excluded; no later phase begins here.
