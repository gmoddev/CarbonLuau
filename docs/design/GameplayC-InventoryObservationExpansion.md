# Inventory observation expansion — offline feasibility

Date: 2026-10-12. Task class: investigation/design, with a focused cold IL checker.
Repository baseline: `dabb872094f74e60d75de099379d198fb63a6bf8`, published
`v0.7.0`, API `0.7.0-experimental`. This report is a proposal, not a canonical
contract amendment, implementation authorization or new qualification PASS.

## Finding and recommended scope

A global transaction coordinator is unnecessary for a **bounded reconciliation
hint** or a **receipt restricted to one explicitly named synchronous operation**.
It would still not make arbitrary Rust/plugin inventory activity transactional.
The new outer paths below make narrower observation technically plausible, but
do not supply a universal terminal fence. Preserve public sampled
`PlayerInventory.ItemChanged` and defer `ItemAdded`/`ItemRemoved`.

The smallest next experiment is a private, scalar hint after CarbonLuau's own
existing GiveItem/TakeItem call has unwound, including Give cleanup and gate
release. It should only influence selection in the existing observer's four-scan
frame allowance; it must not emit an operation delta, scan inline, enter Luau,
create another inventory scanner, or change D13 outcomes. Start with an offline
model and a disabled private seam, not production host patches. A useful priority
hint has a measurable fairness cost at saturation; that cost needs an explicit
decision and requalification rather than an implied performance improvement.

**Implemented here:** this report and
[Research-InventoryObservationExpansionOffline.ps1](../../tools/Research-InventoryObservationExpansionOffline.ps1).
No production, API, metadata, identities, shared indexes, tags or release assets
changed. Independent-agent work stays on disjoint paths. No commit/push, host
build, RustDedicated process, live fixture, plugin installation or server change
was performed. Remote artifacts are cold inspection output only.

## Existing qualification and retained negative premises

Authority: [I1–I12 and decision register](../Invariants.md),
[D7/D10/D11/D13](../invariants/Runtime.md), [D18](../invariants/Player.md),
[D23](../invariants/Gameplay.md), [compatibility](../Compatibility.md) and its
[qualification policy](../qualification/Policy.md). Exact original connection,
defining domain/publication/VM authority, owner-thread access, bounded work,
deferred delivery and no replay remain mandatory.

[C1](../GameplayC1-Validation.md) qualified lightweight acquisition and shared
physical reads. [C2](../GameplayC2-Validation.md) qualified the private sampled
substrate and preserved a PARTIAL verdict for the stronger operation contract.
[C3](../GameplayC3-Validation.md) qualified public sampled ItemChanged on the
recorded Windows/Linux tuple. [C4](../GameplayC4-Validation.md) migrated mutations
to PlayerInventory without changing the scanner, mutation algorithm or shared
gate. [Release 0.7.0](../Release-0.7.0-Validation.md) records combined qualification
and publication scope; this offline task does not rerun or extend those receipts.
Controlled BasePlayer connections in those receipts are not authenticated clients.

Retain the C2 transfer `-80/+80`, split `-30/+30`, requested `+5` becoming sampled
`+12`, direct amount-write and balanced `+10/-10` negatives. Intermediate callbacks,
equal samples, a quiet frame and dirty notifications are not transaction proof.
`Item.amount` remains a public field (`0x040054fd`); unnotified external writes
remain outside finite named-mutator coverage. These are starting premises from
[the saved C2 research](../history/gameplay/GameplayC2-HostResearch.md#gameplayc2-hostresearch),
not newly rediscovered conclusions. Its hook catalog and selected publicizer
reproduction are reused as historical evidence, not rerun or relabeled loaded.
The immutable unrelated public envelope remains one qualified barrel with
`scrap`/`metal.fragments`, RPC melee-only attribution, and sampled ItemChanged.

## New exact-source inspection and provenance

The checker uses Mono.Cecil metadata/IL and direct PE byte reads. It neither
reflection-loads nor invokes Rust/Carbon, resolves external implementation bodies,
rewrites/publicizes an assembly, scans hook catalogs, nor enumerates global amount
writers. Forty selected methods per platform connect new enclosing/notification
paths with necessary C2 support bodies. Fifteen methods/platform overlap the
saved C2 ledger; all thirty overlapping original-IL and instruction-text hashes
match. Twenty-five methods/platform are newly recorded by this focused selection.

| Input | Location / SHA256 |
|---|---|
| Windows original Assembly-CSharp | `dockerbox D:/Sandbox/Codex/Entity1AStartup/server/RustDedicated_Data/Managed/Assembly-CSharp.dll`; `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` |
| Linux original Assembly-CSharp | `BigKVM /root/codex/world-movement-20261003/server-linux/RustDedicated_Data/Managed/Assembly-CSharp.dll`; `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| Read-only Linux copy inspected on dockerbox | `C:/Sandbox/Codex/GameplayA-20261008/HostEvidence/Assembly-CSharp-Linux.dll`; same Linux hash, newly compared with BigKVM's file hash |
| Mono.Cecil | `D:/Sandbox/Codex/Entity1AStartup/server/carbon/managed/lib/Mono.Cecil.dll`; `c41bdb9ffd3c5f6e17d2382c1012d73703e035e3f1100245fdd4e08c8dc6eb5b` |
| Saved C2 v2 ledger, reused | `C:/Users/aiden/.codex/InventoryNetHost-C2Sidecar-20261011-v2.json`; `66329340e3ba4784d7f27ee39a7335de42c9dd55b846d28d723e649be2a3dc3e` |
| Final new checker | `tools/Research-InventoryObservationExpansionOffline.ps1`; `5923e2e1686b4655ed957eb7432d6e7221ec3dc25cfdd5f6c6efbd2f405f8903` |
| New final ledger v2 and deterministic v3 | `dockerbox C:/Sandbox/Codex/InventoryObservationExpansion-20261012/SelectedPaths-v2.json` and `SelectedPaths-v3.json`; each `3ce3b616cb3fd63b45f92bd7a51e590b442b9ad24962edf9586243d22abf26c2` |
| Local v2 copy | `C:/Users/aiden/AppData/Local/Temp/CarbonLuau-InventoryObservationExpansion-20261012/SelectedPaths-v2.json`; same final ledger hash |

Windows MVID `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e`; Linux MVID
`616082a0-36f4-4680-a1ab-efc5766796cc`. The ledger retains each full signature,
token, original raw-IL SHA256, resolved instruction-text SHA256, locals, EH ranges,
static/virtual/slot flags, stack/header shape, calls/function pointers, returns
and selected amount stores. All forty selected resolved shapes match across OSs.
That is selected original-body parity, not universal inventory or loaded-wrapper
parity. Every record deliberately says `LoadedBodyMatch: null` and
`HarmonyTopologyMeasured: false`.

The SSH PowerShell file invocation initially hit the worker's execution policy
and did not run the checker. Subsequent invocations evaluated the same inspected
script text in a noninteractive, encoded PowerShell command without changing
execution policy or machine settings. Consequently the ledger's optional
`CheckerSHA256` field is null for that invocation form; the final source hash
above is separately measured. Earlier 37-method `SelectedPaths-v1.json`, SHA256
`8f3e433782c073c5edd39433daf256847cd9fc69f55cc366e328170502bf58a3`,
is retained as the earlier, smaller selection, not substituted for v2.

## New path findings: static facts and their implications

Offsets and tokens here refer to the original pre-publicizer, pre-Harmony PE.
Resolved `callvirt` targets are declared references; actual override dispatch,
Carbon rewriting and trusted plugin callback effects require loaded evidence.
The hash table below fingerprints new methods underlying these findings. C2
support-method fingerprints remain in both saved and new ledgers.

### Outer moves, transfers, stacks and splits

`PlayerInventory.MoveItem` (`0x06002f9c`) is the existing C2 outer RPC candidate,
not a new universal solution. Its saved split/restoration and nested update facts
remain valid. New `SwapHotbarWithMainRow` (`0x06002f9e`) broadens the caller analysis:
it detaches belt items at `IL0084`, moves main items at `IL00c6`, moves detached
items at `IL0109`, then tries GiveItem at `IL0115` and Drop at `IL013e` on the
fallback branch. It drains removals at `IL015d` and invokes ServerUpdate(0) at
`IL0168` before returning. A completed swap can therefore have an actual loss
if its fallback drops an item; successful outer return is not proof of zero net.

New `PlayerInventory.GiveItem` overloads (`0x06002fbe`, `0x06002fbf`) show why an
outer **success boolean** also needs physical scope checks. The three-argument
overload delegates at `IL0005`. The four-argument body tries a chosen container
(`IL002c`), main (`IL0041`), optional backpack contents (`IL006a`) and belt
(`IL007f`). Backpack placement can return true at `IL0072` without a D18
main/belt/wear gain. Some false returns can follow lower-level partial effects.
This is host source observation, not permission to use generic GiveItem in the
qualified CarbonLuau InventoryOnly adapter.

**Inference:** an allowlisted synchronous outer invocation can be bracketed with
accepted before/after reads, provided full normal completion, nesting, original
connection and loaded topology are proven. That yields an observed net interval
for that invocation, including callback effects. It cannot identify every nested
operation, separate causes or cover plugin-held splits placed on later turns.
Per-invocation nesting suppression can avoid duplicate scoped receipts without
a global transaction coordinator; unknown/nested envelopes must fail closed for
the stronger private classification rather than claim universal completion.

### Pickup and drop

The reused `WorldItem.Pickup` (`0x06003ba3`) now joins the new outer GiveItem
analysis: RemoveItem at `IL00a7`, ownership at `IL00b4`, virtual
`BaseEntity.GiveItem` at `IL00c2`, then a broadcast and return. New
`WorldItem.RemoveItem` (`0x06003baf`) unsubscribes OnDirty at `IL0030` and clears
the world item field at `IL0037`; this body is not an inventory receipt.
New `BasePlayer.GiveItem` (`0x06001ae2`) calls PlayerInventory.GiveItem at
`IL0052`; if false, it calls Drop at `IL00fb`. Thus even an uninterrupted normal
Pickup return cannot prove receipt in the three accepted containers. Dynamic
dispatch and the backpack option must be checked separately in a future fixture.

New `PlayerInventory.ItemCmd` (`0x06002f99`) supplies a useful enclosing drop
boundary beyond Item.Drop. Its partial-drop branch calls SplitItem at `IL0158`
and Drop at `IL018d`, then invokes `onItemRemovedFromStack` at `IL01fc`.
The whole-stack branch calls Drop at `IL022d` and can invoke
`onItemAddedRemoved(false)` at `IL0295`. Both reach the outer broadcast/return
at `IL02a8/02ad`. The non-drop branch calls Item.ServerCommand at `IL02b6`,
DoRemoves at `IL02bc` and ServerUpdate(0) at `IL02c7`. New
`Item.ServerCommand` (`0x06006a55`) dispatches to a held entity at `IL0019`
and each ItemMod at `IL0034`; those virtual downstream bodies/configurations
are not exhaustively qualified here.

**Inference:** post-ItemCmd reconciliation can avoid observing a split before its
immediate drop, but an ItemCmd return is not a promised quantity, world-spawn
success or unique removal. World payout success would additionally need the
existing exact-lifetime/ownership rules. No world producer is added here.

### Consume, amount writes and crafting

New `PlayerInventory.UseAmount` (`0x06002fca`) visits main, belt and wear at
`IL000f/0023/0036`. New `ItemContainer.UseAmount` (`0x06006a92`) calls UseItem
at `IL004c` and decreases a caller-held remaining amount at `IL0056`.
Those local requests are not substitutes for physical net verification. The
saved C2 HeldEntity/StringLights/TryTakeOne direct writers remain necessary
coverage negatives, and are not re-enumerated globally.

New `ItemModConsume.DoAction` (`0x06006b5e`) applies effects, can call
OnAttacked (`IL0190`), and can create/grant product items (`IL027d/0289`)
**before** deciding between LoseCondition (`IL02d3`, return `IL02d8`) and
UseItem (`IL02dc`, return `IL02e1`). A consume action need not remove quantity;
it can also add other shortnames or alter the Player's lifecycle. A lower
UseItem receipt misses earlier products and a condition-only branch. This
method body is directly proved; which item/RPC selects it is a configuration
and virtual-dispatch gate, not assumed universal command coverage.

New craft callgraph separates reservation from eventual output:

```text
CraftItem IL0050 -> CollectIngredients IL002f -> CollectIngredient IL001e -> Take
                -> CollectIngredients IL007f stores ItemCraftTask.takenItems
CraftItem IL00d9 -> queue.AddLast -> returns; task persists across updates
ItemCrafter.ServerUpdate IL013d -> FinishCrafting
FinishCrafting IL0102/0111 -> Workbench upgrades/bonus items
FinishCrafting IL01b7 -> UseItem on task-held ingredients
FinishCrafting IL02eb -> PlayerInventory.GiveItem; failure -> Drop IL0391
CancelTask IL0123 -> MoveToContainer(main); failure -> Drop IL0180
```

CraftItem (`0x060069cd`) has already collected ingredients before queue insertion
and returning true. CollectIngredients (`0x060069cc`) retains them in the task;
FinishCrafting's saved output amount store `IL0063` remains a direct write on a
new output. New ItemCrafter.ServerUpdate (`0x060069ca`) processes a timed queue
element; it does not complete the whole queued request. New CancelTask
(`0x060069cf`) marks cancelled at `IL0058`, may return true without an owner at
`IL006c`, and otherwise returns/re-drops remaining task-held items before clearing
the list at `IL01d4`. Quantity reservation, consumption, bonus receipt, ordinary
output and cancellation return are distinct physical intervals.

**Inference:** a completed craft enqueue may legitimately produce an immediate
negative sampled delta; waiting for a future finish to conceal it would change
D18's physical meaning. A whole-craft transaction spanning task lifetime requires
new correlation/retention/failure semantics and is not the smallest improvement.
Neither FinishCrafting's outer return nor the craft notification alone covers
bonus-item effects, every queued iteration or accepted-container receipt.

### Dirty notification and post-operation reconciliation

New `PlayerInventory.ServerInit` (`0x06002f90`) installs OnContentsDirty handlers
on main/belt/wear at `IL0174/018b/01a2`. It assigns main/belt
onItemAddedRemoved delegates and combines the wear delegate at `IL01ed`.
New OnContentsDirty (`0x06002f96`) only invalidates the network cache at
`IL0014`; OnItemRemoved (`0x06002fa3`) likewise invalidates it. There is no
operation epoch, quantity, commit marker or completion result in those bodies.
OnItemAddedOrRemoved also schedules holster work and processes a mission event;
its return does not complete an enclosing transfer.

The saved MarkDirty chain is confirmed only as graph support:
Item.MarkDirty -> OnChanged -> parent.MarkDirty -> parent-item.MarkDirty and
onDirty delegates -> Item.OnDirty. It can recurse through nested containers and
calls synchronous delegates before the enclosing operation returns. Subscribing
directly must preserve those delegates and exact container lifetimes; it is more
intrusive than a hint emitted by CarbonLuau's own adapter.

New surrounding evidence clarifies ServerUpdate's scope: it calls PlayerLoot.Check
at `IL0006`; with positive delta and eligible Player it calls crafting.ServerUpdate
at `IL0034`, then updates main/belt/wear at `IL0050/0060/0070`. UpdateContainer
can call OnCycle (`IL0016`) before testing dirty and sending inventory (`IL0028`).
DeferredServerUpdate simply calls ServerUpdate(0). SendUpdatedInventory selects
network projection modes, not a terminal inventory certificate. A later frame
avoids the inspected synchronous intermediate windows only if sampling is not
entered recursively inside an active host call; it does not prove arbitrary
held work has completed.

## Method fingerprints supporting the new findings

These full SHA256 values hash UTF-8 resolved instruction text joined with LF
(no trailing LF); they are **not raw loaded-method hashes**. Tokens/sizes below
are identical in the two inspected originals. Full signatures, EH and raw hashes
are in the final ledger, avoiding a duplicate IL dump in this document.

| Method | Token / IL bytes | Resolved IL SHA256 |
|---|---|---|
| PlayerInventory.ServerInit(BasePlayer) | `0x06002f90` / 602 | `cf005f2fdf6ee429c428175c303659ac4ab50556812e525249b59eb6c44d7edd` |
| PlayerInventory.OnContentsDirty() | `0x06002f96` / 26 | `204a8c24ab4b70b74381f0adc646ef9fc62688e2ebfdc51d3157754252f030c2` |
| PlayerInventory.ItemCmd(RPCMessage) | `0x06002f99` / 717 | `e2367bec82732c06596f5af1bc3a7defd3377dafc7792648e4b29c5e4daf3abf` |
| PlayerInventory.SwapHotbarWithMainRow() | `0x06002f9e` / 366 | `370f933093fe245ffc5a5171193ad02c0a8e340492306d7440f96cc7bb781ad8` |
| PlayerInventory.ServerUpdate(float) — C2 support | `0x06002fa9` / 118 | `cc5b2361ac5770e2a85910deebf3fb14558788a64cfa18fabf2b8a154553282c` |
| PlayerInventory.GiveItem(Item,ItemContainer,GiveItemOptions) | `0x06002fbe` / 11 | `9149b508eeeab9c5a3ba60ca083f47b6718359b4fb6ad8d8f97e4f1e752e40f2` |
| PlayerInventory.GiveItem(Item,ItemMoveModifier,ItemContainer,GiveItemOptions) | `0x06002fbf` / 138 | `2409d6a19cd156fbf517fbdc18d0c3cc2560055f2bae34223d5da06a8131328d` |
| PlayerInventory.UseAmount(ItemDefinition,int) | `0x06002fca` / 60 | `b2353c622e0bfb70b32f40120d96dd72f05dab542f3ff206f1a632fae15a39e2` |
| BasePlayer.GiveItem(Item,GiveItemReason,GiveItemOptions) | `0x06001ae2` / 258 | `3d8d38ab0ebe20143d3bfbc2ada6eb0b93916e9ac1a2b9e01b3e05a6cc2c1bf6` |
| ItemCrafter.CollectIngredients | `0x060069cc` / 133 | `c61e564d8814f7402ccd4b4b340c5de87bb308d8f5d69e29955d01feed65c8a7` |
| ItemCrafter.CraftItem | `0x060069cd` / 295 | `2ea0d8a740d2e6be02bf1137b5304f5f1d71ec52de4a2c86f13e835567248478` |
| ItemCrafter.CancelTask(int) | `0x060069cf` / 475 | `5be8f39bebbab8a5266ba46175ba6bfabe6f3be5a02e944533a224c3951d79d2` |
| ItemCrafter.ServerUpdate(float) | `0x060069ca` / 355 | `f5549c1a76cca5ba819533df41f468f8ee41c601a9f96f096275ebf6f74bcf20` |
| ItemModConsume.DoAction(Item,BasePlayer) | `0x06006b5e` / 738 | `b30e6936a097cb0507c46206dcb3d61a31eac5b7280b37c34e92e40db0a5dc30` |
| Item.ServerCommand(string,BasePlayer) | `0x06006a55` / 68 | `7d78db59677a340d972c1ee5a50b49187df6b275e2b7480c78b45eec5e0c7c35` |
| ItemContainer.UseAmount(ItemDefinition,ref int) | `0x06006a92` / 101 | `e4add38feac921d894797ff8f6e12c031016c726513d5bd12443c675c583bc67` |

## Smallest candidate and rejected alternatives

Current source facts at the baseline: RunInventorySampleFrame rejects Host.Busy,
then calls InventorySamples.Tick on the existing Unity frame receiver. Tick
reserves 128 inspection visits before each shared Snapshot, scans at most four
watches, compares scalar baselines and retains the current D23 transport.
CountForMutation's per-definition VERIFY is not a complete quantity snapshot.
Take releases its gate in finally; Give classifies/cleans returned temporary
resources in finally before releasing the gate. Source hashes:

| Current file | SHA256 |
|---|---|
| InventoryNetObservation.cs | `053dde121e45546ee4d88ac3cad7f34088e2658a3af988a20f79f574f7983351` |
| CarbonLuau.Inventory.Carbon.cs | `1e88c6e7ae7a87749c063980aee0e55e5cc7e31e69e6ed97b917235ca693e787` |
| InventoryGrant.cs | `90771282f682a1ccb0225b0621dc725a941034d4891156de1ea24cfa8a183096` |
| InventoryMutation.cs | `201a4a20e194194ef01f9acf1f7f07d7fdcfceac3ac68c8c5d1d8c7837b80b8d` |

Proposed private hint rules, requiring a later implementation task:

1. Mark only an already watched original token/UserId after the outer facade
   call has unwound, after cleanup/gate exit. No hint creates demand or resolves
   a reconnect by UserId. A post-COMMIT error may still merit reconciliation;
   do not restrict hints to verified success or use them to alter that error.
2. Store at most one bit per existing watch (64 logical bits; account the actual
   managed representation in the retained policy, not an assumed eight bytes). Discard it on
   watch/domain/VM retirement and fresh-baseline creation. Admission allocates
   no host object, callback root, queue, timer or operation history. Hint failure
   must not mask a mutation result/error or cause another host effect.
3. At the existing owner-frame boundary, optionally allocate **one of the four**
   scans to a rotating hinted watch and at least three to regular round-robin.
   Charge the same 128 visits and use the same Snapshot/Difference/fanout path.
   Do not sample an already sampled watch twice in one frame. Clear a selected
   hint before observation; invalidity rebaselines, overload advances/discards
   under existing semantics. No immediate scan after Give/Take and no fabricated
   delta derived from requested Amount.
4. Leave periodic scans active for unnotified amount writes. A new hint cannot
   revive a stale baseline, grant a new subscriber earlier history or refund
   native-held reservations. It changes scheduling only; completed-operation,
   causality and every-action promises remain unavailable.

With an uncontended full scan/record budget, current four-way rotation services
64 watches in 16 pumps. A conservative three-regular/one-hint allocation has a
22-pump regular-service bound (`ceil(64/3)`), with hint service separately
rotating. These are arithmetic bounds under stated premises, not measured server
latency or SLAs. At four saturated scans/frame there is no free capacity: extra
early service must delay another watch or raise the budget. Preserving the exact
16-pump sweep leaves no guaranteed priority improvement at saturation. An
experiment must report this tradeoff, compare latency under realistic demand,
and retain current scheduling if the benefit does not justify requalification.

| Alternative | Disposition |
|---|---|
| Inline sample inside Insert/Remove/Stack/MarkDirty | Reject: enclosing transfer and callback effects are unfinished; adds callback-side host work. |
| Dirty-only observation / onDirty counts as Amount | Reject: no quantity/epoch; direct writes can omit dirty; recursive delegates are intermediate. |
| Add container callbacks as the first improvement | Defer: exact attachment/replacement/teardown and callback-chain qualification add complexity; own-adapter hint needs no host patch/delegate. |
| Patch every amount write or enumerate every mutator | Reject: unnecessary repetition of C2 and no coverage of arbitrary trusted writes; patch explosion. |
| Treat any normal outer return/true as receipt | Reject: backpack/drop/condition-only/partial-effect branches require physical postconditions. |
| Reuse Give/Take requested Amount or VERIFY count as ItemChanged delta | Reject: per-definition mutation verification is not the previous full sampled baseline, and Give accepts a lower-bound gain. |
| Add immediate before/after scans around every operation | Defer: extra work, baseline ordering, nesting and shared scan/record budgets need a separate scoped receipt contract. |
| Hold a craft transaction open until finish/cancel | Reject for this scope: physical reservation spans turns and would need new coordinator/correlation semantics. |
| One quiet frame / debounce / repeated equal samples | Reject as completion proof; may only tune sampled latency. |
| Public ItemAdded/ItemRemoved as sign filters | Technically derivable from the existing signed record, but defer: no new observation proof and public/API change is unauthorized. If ever considered, semantics must remain explicitly sampled with a separate API decision. |

## Offline checks and measurements executed

- Final-source PowerShell parser PASS; all 13 local Markdown links resolve;
  all 16 fingerprint-table rows match both platforms' tokens/sizes/text hashes;
  all 40 selected method tokens also match across platforms. No compilation was necessary.
- Cold checker PASS on the exact Windows original and hash-matching Linux copy:
  40 methods/platform, equal selected IL/EH/local/header/dispatch shape. Final
  v2 measured **871 ms** inside worker PowerShell, including Cecil load and JSON
  generation; this is inspection time, not inventory/server performance.
- Deterministic final-source rerun v3 produced the identical ledger hash.
- Swapping the Linux file into the Windows parameter failed with controlled
  `Windows assembly drift`; no output or host execution was accepted.
- Reusing an existing evidence path failed via CreateNew and preserved its hash.
- Reused C2 ledger's 30 overlapping original/text pins matched. This verifies
  artifact continuity without rerunning C2 live or model investigations.
- The checker bounds input PE size to 64 MiB, selected overload sets to four,
  and each selected body to 4096 instructions. No assembly-wide callgraph,
  global writer census or runtime inventory traversal is performed.
- Whitespace inspection PASS. HEAD remains the starting revision. Concurrent
  tracked/untracked changes outside the two owned files were observed and left
  untouched. No background process was started; inspection commands exited.

No new runtime tests, sanitizer run, live timing, allocated-process-memory claim
or production behavior PASS follows from these checks. C2/C3's prior managed
timing and resource receipts retain their own source/environment labels.

## Remaining Windows/Linux gates and exact next phase

**Next phase: C-Observation-1A — private own-adapter hint model and scheduling
decision, production disabled.** It should reuse the existing sampled substrate
in scoped tests rather than add a second scanner. Test sparse/full 64-watch
workloads, continuous hints, exact four-scan/128-visit accounting, ordinary-watch
fairness, record/visit exhaustion, post-COMMIT error, no hint on fresh/stale demand,
new-subscription cutoff, invalid snapshots, failed publication/replacement,
reconnect, domain/provider retirement and fatal recovery. Compare hint latency
against current rotation and document the 16-versus-22-pump tradeoff. No public
Signal or semantic upgrade belongs in this phase. This report does not authorize
its production implementation.

After a separately authorized production scheduling change, required Windows
and Linux live gates include original D11 revalidation, actual adapter cleanup
and gate-unwind ordering, owner-frame/no-recursive-entry behavior, existing loaded
IsValid/IsRemoved/read-path pins and topology, mixed gameplay budget/fairness,
resource convergence and the preserved C2 intermediate/balanced/direct-write
negatives. Demonstrate latency benefit without extra scans or mutation-result
changes. Controlled server objects are still separate from authenticated pickup,
drop, consume and craft client RPC evidence.

An optional later **C-Observation-1B** could investigate private certificates
for an explicit allowlist of outer ItemCmd/MoveItem or selected craft transitions.
Before claiming those operations completed, each OS needs original/transformed
method pins, full effective loaded Harmony prefix/postfix/transpiler/finalizer
topology, actual virtual dispatch and container scope, skipped original/throwing/
nested/reentrant/veto cases, callback interference, exact success/postconditions
and shared bounds. Static identical originals cannot close any of those gates.
Different ordinary operations may be eligible independently; failed coverage
must not weaken sampled ItemChanged or require a global transaction coordinator.

Stop on unexpected host/source drift, a conflicting CodexLock claim, required
public API/resource-contract amendment, live-only evidence need or an expansion
into gameplay behavior. Keep all settled receipts and unrelated files intact.
