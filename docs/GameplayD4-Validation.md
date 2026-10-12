# Gameplay D4 — barrel-2 strict Replace qualification

Status: **GAMEPLAY-D4 PASS — BARREL-2 STRICT REPLACE QUALIFIED**.
Starting main: `a54d34f5e72554056756e9ad9585bcc53e7c7365`.
Date: 2026-10-12. This is the scoped continuation of
[D4 offline research](design/GameplayD4-LootExpansion.md), not another loot engine.
[D2A](GameplayD2A-Validation.md), [D3](GameplayD3-Validation.md),
[D23](invariants/Gameplay.md) and [D20](invariants/WorldEntity.md) remain authoritative.
Final source/CI attribution appears below; no pending check is counted as PASS.

## Exact extension and compatibility

The unchanged signature is
`Loot:RegisterDropRule(Prefab: string, Mode: DropMode, Drops: {LootDropEntry}): ()`,
obtained through `game:GetService("Loot")`; only `Enum.DropMode.Replace`.

| Exact supported full path suffix under `assets/bundled/prefabs/autospawn/resource/loot/` | Host prefab ID |
|---|---:|
| `loot-barrel-1.prefab` | 966676416 |
| `loot-barrel-2.prefab` | 555882409 |

Only `scrap` and `metal.fragments` remain replacement entries. Names are exact,
ordinal and case-sensitive; other prefabs/items remain rejected. The vanilla
negative control uses a real radtown barrel, but qualifies no additional target.

The private descriptor now retains its declared full prefab instead of returning
the old fixed barrel-1 constant. The bounded sole-rule publication accessor and
lookup retain one global owner and return no owner/rule for a nonmatching target.
Host entry checks the exact name/ID pair, plain LootContainer and existing
component/settings/lifetime premises. No new production hook is added: the
qualified outer OnDied, common Storage payout cut and genuine returned-world
factory fence are unchanged. Their exact loaded pins and interception theorem
remain in D2A/D3; live source checks revalidate them.

One rule globally / one per domain is retained, **not one per prefab**. Same-owner
replacement may switch profiles; different owners conflict even for disjoint
profiles. Both barrels can exist and die simultaneously: only the selected
profile is replaced, the other remains vanilla. Profile changes do not reset
the actual Unity-frame producer budget.

Limits remain eight entries; integer `0 <= Min <= Max <= 1000`; sum(Max) <=8000;
eight worlds/occurrence; eight admitted occurrences/actual frame; one in-flight
frame; at most 129 inspected root/addon sessions. Table capacity guards remain
64 array / 64 hash slots per table. These do not multiply by prefab or addon.

Registration is copied, immutable and initialization-only, staged until successful
publication. It is not a physical payout receipt. PREPARE rejection leaves vanilla;
suppression begins nontransactional COMMIT. Partial/indeterminate results have no
vanilla fallback, rollback, automatic replay or compensation. Accepted host-owned
worlds survive later failures. Original UID/world reciprocity, completed Spawn
epochs and certified original source removal remain mandatory.

Package `0.7.0`, API `0.7.0-experimental`, ABI `1.5`, provider `1.2`, schema `1`
and Luau `c6b830185af962c82003f86784e2fe036357c830` are unchanged. SinceApi
introductions/signatures/types are unchanged. Published v0.7.0 still supports only
barrel-1: its tag and six assets are immutable. D4 is post-release development
qualification, not a release/tag or broader attribution/item/mode assignment.

## Pinned hosts and actual prefab evidence

Rust build 25653776 / protocol 2634.289.1 / revision 166494; Windows Carbon
2.0.262.0 (`8a81d70`), Linux Carbon 2.0.261.0 (`c74c4ca`), Harmony 2.4.2.0.
Backing Rust hashes were rechecked:

- Windows `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`.
- Linux `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`.

Both live hosts observed barrel-2's exact full name/ID, concrete LootContainer,
Transform/Spawnable/LootContainer root components, empty initialized component
cache, inventory capacity six, dropsLoot=true, scrapAmount=2, corpse disabled and
empty debris GUID. The ordinary individual-drop selector returns true. Same
profile observations were verified for barrel-1; no prefab equivalence was
inferred merely from class inheritance or a historical ID.

The prototype kept production validation closed and changed only the established
research fixture's prefab literal inside a test-only package, using
[New-Barrel2ResearchPackage.ps1](../tools/New-Barrel2ResearchPackage.ps1).
Package SHA256 `2d3b18c33ae172364f2a4fc99522f923155ad7a506bbb97e4d144761d71df73e`.
It observed two vanilla/stored worlds, a boosted third world containing two bonus
scrap, zero worlds for suppression and two intended replacement worlds.
Prototype receipts are not the final public PASS below.

## Final public physical receipts

Public fixture package SHA256:
`4a2fd66bed36a2b86df817826b30a71050a9f095dad01e77eccb30b3302da4e8`.
Declaration, replacement, addon and recovery source use the real public binding;
the original Lua declaration tables are then mutated/cleared to prove snapshots.

| Platform | Final D4 live receipt | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261012-050417-880/server.log` | `752bfdfd9901242853e1d53942967a8255cc68556bf4d384cddaceb8a0f7f46a` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261012-050428-1827737/server.log` | `367b68e9a54d1d1ee38c516c0d9faf3a29e0ba3659e3835288c28045fc6aee7b` |

Windows native/compiler SHA256:
`73874a00e6d3c98c0f77c6b27f8e3af555d9c85336861e3d7a912d82ab428b11` /
`0fe6626c2a107245065581c013b29b06713756cd1d9ab66af845efacc755e06c`.
Linux native/compiler:
`6d13b7723180985cb741aaa5796a62606e641bdf074909d90b910fc1f9f26dbd` /
`884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92`.
Windows MSVC 19.44.35228.0/static CRT and Linux GNU 13.3.0 Release builds use
parallelism two. Workspaces/caches are isolated under GameplayD4 on the Windows
D: sandbox and `/root/codex/gameplay-d4-20261012` on BigKVM. No host policy changed.

Both final live matrices PASS:

- Normal/empty/all-zero/fixed/range/max public declarations and snapshot independence;
  stored and boosted bonus suppression; exact returned Item/UID/world ownership.
- Original-source Kill veto, actual IOnSendNetworkUpdate late Spawn exception,
  returned amount mutation and actual Create null/throw dispatch. Partial worlds
  remain accepted; no fallback/replay/compensation or hidden-allocation claim.
- Failed/successful root replacement, addon/provider retirement and fatal VM
  reconstruction; original B1/B3 callback availability. Controlled provider
  retirement and connection fixtures are not authenticated-client observations.
- Both physical profiles in either selected direction; nonmatching original
  inventory remains untouched and then drops vanilla. Same-frame profile switches
  at used=8 remain exhausted. Same/disjoint foreign-owner conflicts preserve the
  original sole rule. Caught failed nested modules leave no staged rule or history.
- A real unsupported source, name/ID mismatch and missing-cache-at-entry reject
  before COMMIT. The missing-cache control calls full OnDied directly and is
  labeled payout-only, not a new full combat-death proof.

### Controlled alternate bag branch

No natural bag branch is claimed for these plain barrel assets. A test-only
exact-source selector override forces false; the unmodified instance
`ItemContainer.Drop(string, Vector3, Quaternion, float)`, token `0x06006a77`,
returns the genuine bag. Its original body has CreateEntity at IL0023,
TakeFrom at IL0051 and Spawn at IL0057; the common payout calls it at IL0093.
The private collector observes only the exact original container, never distance
or current-ID lookup. This is test instrumentation, not a new production hook.

Both hosts measured one fully spawned/keyed bag holding the two original seeded
Items/UIDs, destroyPercent=0. With the public rule, bag returns=0, selector calls=0
and replacement worlds=2: the entire common body is suppressed before the branch.
The override/collector are removed and original observer topology revalidated.
This proves a controlled physical branch, not natural bag configuration, subclass
admission or buoyant-bag live evidence; the common cut covers the alternative
literal structurally without expanding accepted assets.

## Affected regressions and preservation

Native suites PASS Windows 23/23 (165.76s), Linux 24/24 (114.35s). Focused managed
policy and pinned-VM public suites PASS on both platforms, including exact ordinal
paths, immutability, no nonmatching outputs, publication/recovery and 100 addons.
Full real-VM regressions PASS in `GameplayD4/full-runtime-windows.log` and
`gameplay-d4-20261012/full-runtime-linux.log`: 100 atomic replacements, 200 failed
candidates, modules/providers/GUI/inventory/persistence, recovery and bounds.
The 100-addon VM measurement was 8,759,577 bytes (~8.35 MiB), not a process cap.

Fresh live D3/B2/B1/B3 regression receipts with the new production/native source:

- Windows `loot-d2a-20261012-044219-073/server.log`, SHA256
  `258e2196dee154121832a4819d8b915509664c825665c7dbcdb2d0cc339b3f47`.
- Linux `loot-d2a-20261012-044229-1825619/server.log`, SHA256
  `0d549d8124fcce5bcbe2852dd03da15af4e3007bfd24d1890c33c9759687a5ab`.

Fresh C3/C4 sampled live regression receipts (same established evidence roots):
Windows `inventory-c3-20261012-043759-305/server.log`, SHA256
`c82ba806c332daefb6e745631367d17370e91c432599a7cf3faf7e81cb8a3998`;
Linux `inventory-c3-20261012-043807-1825272/server.log`, SHA256
`05b9e0f8539bfb5e95852554b5bbf6d3a13ae53fa67db73b58845b0e320a7d5c`.
Intermediate transfer/split and balanced-missed-change negatives remain sampled
limitations, never upgraded to completed-operation guarantees.

Disposable persistence regressions also PASS. Windows evidence:
`D:/Sandbox/Codex/GameplayD4/PersistenceEvidence/persistence1b-20261012-045419-6b0531c6`;
Root/A/B same-store/key seeded counters 8/4/2, actual restart read-only counters
3/0/0, two acknowledged -1 exits, zero helpers and final native unmap. Linux
`linux-live-2d-20261012-085429` under the D4 task root records Get/Set/Remove,
Query, unload/reload/restart, two acknowledged -9 exits and zero helpers.
Receipt SHA256 `b2ca057697c41e7a0f46159ed29517b73c50d2b7f896b8f7ce0cf08798ddaad4`;
cleanup SHA256 `ff062fb6f78d73b25d7d9eebb7a4a3f2ea5f66863354fbf5d21b2c5646d3df65`.
Allocated bytes 118,784 is an observation, not an allocation invariant. No D21
semantic, durability, power-loss or new corruption/platform claim follows.

## Preserved failures and corrections

- Initial runtime build still used the old two-argument descriptor constructor
  in one existing test. The test now supplies its original prefab; no public
  compatibility constructor or ambiguous default was added.
- Public run 1 corrupted a cached prefab name to a nonexistent asset. Rust's own
  lookup failed, and cleanup masked the first assertion. A real unsupported
  radtown source replaced that invalid control; first-cause reporting and bounded
  exact original-inventory cleanup now preserve failure provenance.
- Public run 2 nulled components before Hurt, whose ordinary prelude repopulates
  that cache. Replacement was correctly eligible at entry. The negative control
  now calls full OnDied with the null cache, explicitly payout-only.
- Public runs 3/4 observed the wrong static Drop overload, so the forced branch
  executed but its bag return escaped the fixture collector. Exact callsite IL
  identified the instance/float overload; the corrected collector captures genuine
  returns before assertions. The false failures and cleanup diagnostics remain
  recorded, not relabeled PASS. Production interception did not change.

Negative public receipts: Windows `041011-511`, `043003-931`, `044841-802`,
`045815-111`; Linux `041021-1823724`, `043014-1824709`, `044852-1826108`,
`045825-1827214`, each under the established `loot-d2a-20261012-` evidence root.

Every runner verified owned process exit and restored prior package/native/compiler/
hook/config hashes. Windows qualified quit exits -1; Linux wrapper exits 137.
Disposable persistence clones retain task evidence, not a running deployment.
All source, vendor, release and original storage research evidence is preserved.
No fixture, DB/journal, proprietary IL, server binary or credential belongs in
production packages. Main keeps the pre-existing untracked coordination/research
files untouched and uses GitHub no-reply commit metadata.

## Remaining closure and next boundary

Generated metadata/drift/type and deterministic preview-plan checks PASS through
the existing SDK 10 tooling pipeline; `.d.luau` is unchanged because signatures
and types are unchanged. API/architecture/document checks PASS, preserving all
97 historical spans and I1–I12. Windows/Linux repeated dry-run packages are
byte-for-byte deterministic, exclude all fixtures/state and cleanly load the
real VM; all **70** bundled examples compile, including barrel-2. These outputs
are uncommitted-working-tree development artifacts, not published v0.7.0 assets.
The added actual preview regression must still pass hosted tooling; no mock is added.

Final hosted preview, ASan/UBSan/leak and source qualification all PASS at the
candidate revision below. Definitions/signatures remain unchanged; generated
summary metadata follows the real binding. Preview remains UnsupportedPreviewApi.

### Hosted closure and revision attribution

Implementation/tests: `514991ef6b5e3d3868c53fe63630c3c13728f8aa`.
Evidence/docs and final tested candidate:
`3c7beded508ef186e92dbe7e71e13544716c6447`.

- [Core validation 38228873736](https://github.com/gmoddev/CarbonLuau/actions/runs/38228873736),
  attempt 2: PASS Windows x64 and Linux x64 native, managed/real-VM, focused public
  Loot, publication/providers/recovery, B1/B2/B3, inventory C3/C4, persistence/lost-ack/
  lifecycle/scale, deterministic packages, clean example installation and worker/import
  audit. ASan/UBSan/leak/lifecycle/fault job PASS is retained from attempt 1.
- [Tooling 38228873660](https://github.com/gmoddev/CarbonLuau/actions/runs/38228873660):
  PASS Windows/Linux/macOS developer tooling, generation/type/drift checks and
  the added barrel-2 UnsupportedPreviewApi regression. No macOS server claim.
- [Contracts 38228873565](https://github.com/gmoddev/CarbonLuau/actions/runs/38228873565):
  PASS document preservation, API/schema/preview contract and navigation fixtures.

Attempt 1's Windows Persistence-1C stress failed after 7,433 submissions when a
completion callback exceeded the fixture's existing 100-ms deadline. Backend
status showed ready=True, pending=3, no expiry/corruption/backend failures.
Linux was cancelled by matrix fail-fast, not PASS. The failed/cancelled jobs were
rerun once on identical source and both passed. No production/default or fixture
deadline was relaxed, and no persistence implementation changed. The first
failure remains timing-sensitive negative evidence; no unproved cause is attributed
to I12 external interference. Local full suites and actual restart receipts remain
separate evidence, not a substitute for the corrected hosted result.

The subsequent closure commit changes only this evidence record. Under the
documentation/evidence-only policy it reuses the tested implementation above;
the final main push also runs hosted checks and documentation deployment, whose
exact final SHA/run IDs are supplied in the completion report. No untested
production change, new release/tag or later Loot implementation is included.

Next recommended Loot work: separately qualify one simple additional replacement
item through definition/mod/creation/world/cleanup proof (the D4B candidate in
the offline record), or explicitly scope a multi-rule per-prefab registry decision.
Neither is begun here. Other prefabs, modes, breaker attribution, new Signals,
Spawn/Destroy, generic transactions, release/tag and unrelated APIs are excluded.
