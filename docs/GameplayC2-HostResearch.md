# C2 bounded inventory source sidecar — 2026-10-11

Conclusion: the inspected exact binaries support a private **sampled physical net-change** candidate. They do not establish a universal completed-operation inventory observer. Direct amount writers and observable synchronous intermediate states are sufficient negative evidence; further universal transaction enumeration is unnecessary for this candidate. No public C3 qualification or C2 live PASS follows from this report.

Scope: CarbonLuau main `e7b230963ba583213fcad8620c7c6306bed3a174`. Read AICONTEXT, AGENTS, the d725522a C2 attachment, GameplayCDArchitecture-Research, C1, D13/D18 and previous inventory/host evidence. No D18 scanner rerun. Read-only Cecil/IL/decompiler work on dockerbox and read-only BigKVM checks. No server, build, patch installation, runtime mutation, commit or push. The only repository edit is the separately authorized new `tools/Research-InventoryNetHost.ps1`; other current edits belong to the main task.

## Deliverables and verification status

- Checker: `C:/Users/aiden/Documents/ChatGPT/RustCarbonLuau/tools/Research-InventoryNetHost.ps1`.
- Final pin artifact: `C:/Users/aiden/.codex/InventoryNetHost-C2Sidecar-20261011-v2.json`.
- Same artifact on dockerbox: `C:/Sandbox/Codex/Evidence/InventoryNetHost-C2Sidecar-20261011-v2.json`.
- Artifact SHA256: `66329340e3ba4784d7f27ee39a7335de42c9dd55b846d28d723e649be2a3dc3e`.
- Earlier v1 is retained, not overwritten. v2 adds the BigKVM Linux Base hook catalog.

The checker PASS means **24 selected methods/platform have identical resolved instruction, EH, local, static/virtual/slot and body-shape evidence**, including after the established selected-body publicizer reproduction. It records original raw hashes, reproduced raw hashes, instruction text hashes, tokens, code sizes, exception regions, call references and representative amount-store offsets. It does not load Rust or Carbon through reflection or invoke their methods. The pinned Startup InjectIPlayer source body is checked before importing Common's IPlayer field and serializing the assembly in memory. This reproduces selected expected bodies, not an entire processed PE or any Harmony execution wrapper.

Every method explicitly has `LoadedBodyMatch: null` and `HarmonyTopologyMeasured: false`. These are **expected pins for main's live fixture**, not new loaded-method receipts. Hook catalog presence/absence is likewise static evidence, not patch activation, patch order or absence of trusted plugin patches. The fixture must compare actual loaded bytes and effective Harmony topology before calling the read path currently-loaded-qualified.

## Exact inputs

Rust build 25653776, protocol 2634.289.1. Recorded Windows Carbon 2.0.262.0/8a81d70; Linux 2.0.261.0/c74c4ca. These are not the older 25353106/2.0.259 inventory pins.

| Input | Windows | Linux |
|---|---|---|
| Assembly-CSharp SHA256 | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| Assembly-CSharp MVID | `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e` | `616082a0-36f4-4680-a1ab-efc5766796cc` |
| Common SHA256 | `0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a` | `5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb` |
| Startup SHA256 | `b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28` | `7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0` |
| Base hook SHA256 | `6aec02a1c263a38fc93c2c21326b6a180b65c19a213c22799888827785d6babb` | `847eebbb34dc0db7636e3a1c2ee4380d03d0e828e6a55b7eee6ff885c200f8bc` |
| Community hook SHA256 | `1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3` | `4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343` |
| Oxide hook SHA256 | `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d` | `b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764` |

Windows source: `D:/Sandbox/Codex/Entity1AStartup/server/RustDedicated_Data/Managed/Assembly-CSharp.dll`. Linux cached copy: `C:/Sandbox/Codex/GameplayA-20261008/HostEvidence/Assembly-CSharp-Linux.dll`; its hash matches a newly read BigKVM `/root/codex/world-movement-20261003/server-linux/RustDedicated_Data/Managed/Assembly-CSharp.dll`. BigKVM Common and Startup hashes also matched the copies. Linux Base catalog was copied read-only into the sidecar's external evidence path and its hash verified. The checker resolves serialization dependencies from the existing Windows managed/lib directories and records those directories; it does not claim dependency-wide binary equivalence.

Important tuple distinction: the Windows server's presently restored on-disk Oxide hook DLL is `3f6cf14e2b87a59fb4afcfcbf34590895ba03245cb1e8858c18a0fbb8664cc80`. It differs from the historical qualified hook copy used by C1/A/B2 runners. Do not accidentally inspect that restored file and attribute it to the historical fixture tuple. Main's next live receipt must state what was actually loaded. Nothing was replaced by this sidecar.

## Read-path pins for main's loaded checks

`Item.IsValid()` is token `0x06006a4a`, 17 bytes, nonvirtual, instance, no locals, no EH and **no call instructions**. It reads `removeTime`, branches on comparison with zero, and returns false only for a positive value (the IL uses `ble.un`). No item lifecycle, acceptance, Unity access or hook dispatcher is called by this body.

- Original raw SHA256, both OS: `8124edc8d653310d9d4b9b455c8f3516b3d2693fde7e8b8eeeda9a987aa655e2`.
- Expected publicizer-reproduced raw SHA256, both OS: `477480dbc895bfd4d0b69e6660104e5c98b23c6bebd9ae0c01182382da848951`.
- Resolved instruction text SHA256: `ab70ccf2ad960c79a2eacedc6bf47c98bdda74dda579e4f70316a7e26abf9480`.

`Item.IsRemoved()` is token `0x06006a3b`, 14 bytes, nonvirtual, no locals/EH/calls; it tests `removeTime > 0`. Reproduced raw SHA256 both OS: `d80e2530bb0ad5c9dd617a1e19ac1370be37b6e7e0c4416b3f653645ca058a38`.

No Patch attributes targeting `IsValid`, `IsRemoved` or `GetWorldEntity` were found in the three supplied Base/Community/Oxide catalogs on either platform. This does not prove no active Harmony patch from another plugin.

The current scanner's `Value.parent`, `Value.info`, `Value.amount` and `Value.position` are direct field reads; it calls `Value.IsValid()`. Definition shortname is a field read. This is a narrow no-callback read candidate once loaded IsValid/topology matches. `GetWorldEntity()` is separately pinned, but **is not a call-free read**: token `0x06006a4e`, 18 bytes, calls `EntityRef.Get(bool)`. Do not extend the scanner's no-hook claim to that method merely because its name is a getter.

## Mutation and intermediate-state evidence

All offsets below refer to the pinned original, pre-Harmony body. Selected Windows/Linux resolved bodies and EH are identical. Exact original/reproduced hashes and full signatures are in JSON.

| Method | Token / size / EH | Concrete ordering and consequence |
|---|---|---|
| `ItemContainer.Insert(Item, BasePlayer)` | `0x06006a88` / 170 / 0 | Add to itemList at IL0022; write parent at IL0029; FindPosition at IL0030 can return false after those writes; MarkDirty at IL003a; added delegate at IL0050; later cycle/content callbacks; true at IL00a8/00a9. Neither entry nor early callback is an operation-wide completion fence. |
| `ItemContainer.Remove(Item, BasePlayer)` | `0x06006a91` / 196 / 0 | Pre-remove delegate IL001e; list removal IL002a; parent=null IL0032; parent-change and removed delegates IL004a/005e; content callback IL00b7; MarkDirty IL00bd; true IL00c2/00c3. A whole stack is physically absent during callbacks before an enclosing transfer reinserts it. |
| `Item.SetParent(ItemContainer, BasePlayer)` | `0x06006a2d` / 232 / 0 | Old container Remove IL001a; parent=null IL0022; new parent assignment IL0035; new Insert IL0042; MarkDirty IL005f and further parent-entity/saving work. Removal completion does not mean enclosing SetParent completion. |
| `Item.MoveToContainer(ItemContainer, int, bool, bool, BasePlayer, bool)` | `0x06006a35` / 1612 / 2 | Merge destination amount store IL027f precedes `onItemAddedToStack` IL0309; source decrement only IL0318. A scan in that delegate temporarily overcounts if both stacks belong to the same Player. Ordinary move removes at IL0614, then world/conflict work, then SetParent IL0635. Swaps detach both items at IL0463/046c and invoke nested moves/restoration before return. False can occur after partial merge; true is also used by the oversized split/drop fallback. |
| `Item.SplitItem(int)` | `0x06006a45` / 467 / 0 | Original amount decreases IL0028 before CreateByItemID IL0041 and metadata/lifecycle work. Returned split remains unparented until its caller places it. A SplitItem completion postfix alone is still inside a larger split/move operation. |
| `PlayerInventory.MoveItem(RPCMessage)` | `0x06002f9c` / 1366 / 2 | Split then placement, with failed-placement amount restoration at IL048c/04d9. It drains removals and calls ServerUpdate(0) on several paths. Lower-level completion is not this enclosing RPC's completion. |

Thus main/belt/wear transfers, swaps and splits/merges can produce transient physical loss or gain at synchronous callback boundaries, even without concurrent threads. Taking one validated sample after the entire synchronous operation returns yields zero if the final accepted top-level quantity is unchanged. Sampling at an intermediate callback does not.

## Representative direct writers: enough to reject universal named-hook coverage

`Item.amount` is a public field, token `0x040054fd`; assigning it requires no setter and no hook dispatcher. Trusted in-process plugins can alter it between samples, with or without MarkDirty. No patch on a finite set of named mutators can intercept every external `stfld` or reflection write.

- `HeldEntity.UseItemAmount(int, bool)`, token `0x060028a7`, 105 bytes, no EH: direct amount decrement IL0031, then MarkDirty, optional ownership reduction and exhausted-item destruction. It bypasses Item.UseItem, and a partial decrement need not cause container insertion/removal.
- `StringLights.UseItemAmount(int)`, token `0x0600371e`, 64 bytes, no EH: direct decrement IL0019, then MarkDirty/ownership and optional Remove. Same missing OnItemUse producer.
- `ItemContainer.TryTakeOne(int, out Item)`, token `0x06006aa2`, 150 bytes, one EH: direct stores IL003f/0052; separately present in the pin ledger.
- `ItemCrafter.FinishCrafting(ItemCraftTask)`, token `0x060069ce`, 920 bytes, two EH: direct crafted output amount assignment IL0063; ingredient use and later inventory GiveItem or world-drop fallback. Creation or craft notification is not proof the output reached main/belt/wear.

MarkDirty is itself callback-capable: Item.MarkDirty calls OnChanged, parent.MarkDirty and OnDirty; ItemContainer.MarkDirty recursively marks its parent and invokes onDirty. Neither proves completion. A dirty-only watch misses a trusted direct write that omits MarkDirty. A periodic bounded sample can detect its lasting net result.

## Carbon hook classification and fixed-index caveat

Both inspected Oxide catalogs use the same identifiers and fixed input-instruction indices:

| Hook | Identifier | Stock index / original projection | Classification |
|---|---|---|---|
| OnItemAddedToContainer | `07028e4043844169a32320d4e478fe63` | 70 / IL00a8 in Insert | Successful local Insert branch hint; hook result discarded. Still inside enclosing move. |
| OnItemRemovedFromContainer | `d3510dbe53d94192a5c6f0bb89633d03` | 81 / IL00c2 in Remove | Successful local Remove branch hint; result discarded. Still before possible destination insert. |
| OnItemUse | `ef9eae1a6aff47bda9d32c21f4255fbc` | 4 / IL0005 | Before amount decrement; int return substitutes amount argument. Attempt/override. |
| OnItemSplit | `6a8b630df0074b4a88647c1276749291` | 16 / IL001f | Before decrement/create; Item return replaces original path. Attempt/override. |
| OnItemRemove | `27f507c85b7b4d028032e8d85aa38ad1` | 5 / IL000e | Before removal lifecycle, amount zero and queue admission; non-null veto. Attempt. |
| OnItemPickup | `b2210896d5bd4c8b874a35ca5106f824` | 13 / IL0020 | Before pickup transfer; non-null veto. Attempt. |
| OnItemCraftFinished | `cdc7a83c307c468e80ee9d55cd03775d` | 215 / IL0285 | Before final inventory GiveItem/drop branch. Not inventory receipt. |
| OnItemDropped | `c7a744b449924f2ebf9c05833f7f1635` | 102 / IL012a | Stock projection before final RemoveFromContainer; cannot certify full Drop completion. |
| OnItemStacked [1] | `023a4a2920c64b8181f026cdbfd6be63` | 304 / IL0324 | Both quantity writes occurred, but source cleanup, recursion and enclosing return remain. |
| OnItemStacked [2] | `b45be407a45b4ee991637aaec8f29eec` | 595 / IL0605 | Stock projection is a CanAccept-failure result-store, not an evident terminal merge. Do not assume completion from the name. |

These are decoded **transpiler input indices**, not certified effective loaded hook offsets. Preceding transpilers may alter the instruction stream. The index-595 discrepancy is an additional reason to avoid universal semantic claims and to inspect actual effective patch chains if promoting a specific hook. The sampled candidate needs no promotion of these hooks.

Pickup stock flow removes the world item, applies ownership and calls BasePlayer.GiveItem with BackpackOverflow. Main/belt/wear receipt must be measured independently; pickup attempt is insufficient. Drop creates a world object or invokes Remove, then detaches from its container. Take stock flow distinguishes collected splits/detaches from consumed stacks, drains queued removal, and traverses main, belt, wear. Preserve D13's existing specific physical verification; none of these paths makes global inventory transactional.

## Selected contract and main qualification handoff

Use bounded periodic owner-thread reconciliation of opted-in exact D11 Players. One signed snapshot comparator reports difference between consecutive accepted physical main/belt/wear samples. Hooks may shorten latency as dirty hints, but cannot be the sole producer or supply Amount/direction. A net +10/-10 between samples is zero; a lasting direct write is observable; a transfer split across sampling turns can appear as temporary loss/gain under the expressly sampled contract.

Owner-thread deferral avoids the inspected synchronous intermediate callbacks, provided no sampling runs recursively during active host mutation. One later frame, two equal samples, or a quiet dirty flag is not proof that arbitrary plugins will never retain and place an unparented split later. Debounce can hide some windows but cannot justify completed-operation semantics. ServerUpdate is also called from MoveItem and invokes crafting plus per-container updates; it is not a universal quiescence fence.

For stronger semantics, reasonable *separate* narrow avenues are: verified net observation only around CarbonLuau's existing Give/Take adapter; or separately pinned full effective original completion of explicitly allowlisted top-level mutators, with exceptions/nesting and physical postconditions. Those do not cover arbitrary direct writers and should have source-scoped names. No need to pursue either to qualify the private sampled candidate.

Main should compare live IsValid against the reproduced hash, code size, no EH/calls and no effective prefix/postfix/transpiler/finalizer that can invoke callbacks. Read no raw Rust/Unity objects into Luau; retain current D18 validity and D11 identity rules. Qualify final same-Player transfer/split/merge samples as zero and explicitly sample an intermediate callback or multi-turn split as weaker observations. Reset baseline on invalidity/staleness rather than fabricating a delta. Advance baseline after bounded delivery rejection; no replay or attribution claim.

This evidence supports reporting **GAMEPLAY-C2 PARTIAL — SAMPLED OBSERVATION ONLY**, conditional on main's actual runtime/live/resource/lifecycle qualification. C3 should remain unexposed here; an explicitly sampled `ItemChanged` is the defensible future candidate. No completed-operation PASS, universal hook coverage or current-loaded read qualification is claimed by this sidecar.
