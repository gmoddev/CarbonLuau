# Gameplay C4 — PlayerInventory migration

Status: **GAMEPLAY-C4 PASS — PLAYERINVENTORY API COMPLETE, LEGACY METHODS REMOVED**.
Experimental qualification is complete within the recorded exact-host envelope;
no package release/tag is authorized.
Starting main: `cf86b919713eb9a9127c2822bad2753dbe1c90b5`.
Implementation and final tested production revision:
`e95b8b91f665b16fc0107fe30deff2852868a613`.
Incoming README-only commit `026afd08b8e1058716fad2e0b34fc109d40b1b98` was preserved
by merge `063b7ed9ae2c4a4b44b8e8f1a8594b132edabbc9`; no production path changed.
The subsequent closure updates documentation only.

## Contract and compatibility decision

The user explicitly authorized removing the four legacy Player inventory methods
before the next package release. D12/Compatibility require an explicit breaking
identity, so development advances to `0.7.0-experimental`. Package `0.5.0`, ABI
`1.5`, provider `1.2`, schema `1` and Luau pin remain unchanged. The new facade
mutations have SinceApi 0.7.0; existing facade reads/acquisition/Signal keep 0.6.5
introductions. Published v0.5.0 artifacts and historical qualification stay intact.
There is no alias/deprecation grace runtime: migration documentation records
deprecation and removal in this development contract.

Final intended surface:

```luau
Player:GetInventory(): PlayerInventory
Inventory:HasItem(ShortName: string, Amount: number?): boolean
Inventory:CountItem(ShortName: string): number
Inventory:GiveItem(ShortName: string, Amount: number, Behavior: GiveItemBehavior?): boolean
Inventory:TakeItem(ShortName: string, Amount: number): boolean
Inventory.ItemChanged:Connect(function(Context: InventoryChangeContext) end): Connection
```

HasItem defaults to one (exact positive integer through 2^53-1). Mutation Amount
is required and exact positive through Int32.MaxValue. GiveItem Behavior defaults
to the unchanged typed GiveItemBehavior.InventoryOnly singleton; no other mode.
Count/Has retain D18 physical acceptance, canonical 1..128-byte shortnames and
128-stack main/belt/wear traversal. Acquisition scans nothing and subscribes to
nothing. Facades are immutable, not read-only inventories.

Player HasItem/CountItem/GiveItem/TakeItem are removed; RemoveItem did not exist.
No GetItem/GetItems or raw Item surface exists. The old Foundation 1 roadmap's
GetItems reference is deferred enumeration, not implemented capability; it would
require separate bounded immutable item-snapshot semantics and host qualification.
RemoveItem would duplicate TakeItem without a distinct qualified contract and is
not added. No global Inventory service or second scanner is introduced.

## Preserved authority and physical behavior

Private host operation 41 validates the original facade publication witness and
delegates to unchanged operations 26/27/29/30. Native admission applies the same
CanMutateHost predicate to give/take modes before managed dispatch. Shared foreign
facades and closures therefore cannot launder provisional caller authority.
Defining domain, VM and original D11 connection remain bound; no UserId reconnect
retargeting. D7 rollback and D10 deadline ownership remain unchanged.

D13 PREPARE/COMMIT/VERIFY and one cross-domain exact-connection mutation gate are
unchanged. False is definite pre-COMMIT rejection, true requires physical VERIFY,
and post-COMMIT uncertainty raises a controlled error (state may have changed).
No world-drop fallback, compensation, automatic replay or transaction claim.
ItemChanged remains C3 sampled net observation, not a mutation receipt; incomplete
snapshots rebaseline, overload drops, balanced/intermediate changes retain their
documented limitations. Existing C2/D23 shared resource policy is unchanged.

## Qualification

Windows/Linux real-VM C4 tests PASS legacy absence, exact typed optional behavior,
physical backend parity, malformed private envelopes, failed/cold/nested publication,
successful-child/failed-parent facade retirement, shared gate, definite rejection,
indeterminate failures, original connection and fatal recovery without replay.
Foreign shared facade/closure tests include synchronous coroutine resume/wrap:
provisional/cold scopes make zero backend attempts, later committed calls succeed.
Provider unload/reload, required reconstruction and optional stale binding PASS.

C1/C2/C3 focused regressions PASS, including 10,000 mixed churn cycles, 64-watch/
128-demand saturation, shared D23 budgets, callback faults, cleanup and 100 addons.
Mixed ItemChanged/A/B1/B2/B3 transport tests retain synthetic source labels, not
new host death coverage. Full Windows/Linux runtime regressions PASS Foundations
A–G, GUI, Player/Workspace, Loot, persistence, modules/publication, scheduler,
recovery, providers/parser and scale; 100 replacements and 200 failed candidates.
Mutation/scanner production sources themselves have no changes.

Independent review found no production defect, and requested the two extra
coroutine/ancestor-rollback tests above; both were added and passed. The first
native C4 fixture wrongly tried to register a cold module after commit. Registration
now precedes execution/commit; the failed Windows/Linux receipts remain in the
isolated `c4-ctest.log` files. All other native tests passed; corrected allocation-
fault reruns PASS on both. Final sanitizer and hosted whole-suite receipts are
recorded below rather than relabeling the original failed suite. Linux final-source
ASan/UBSan with leak detection PASS all 24 tests (`c4-asan.log`, 280.91 seconds).
This instruments native paths, not the managed heap. Windows/Linux full managed
teardown and bounded-resource tests separately qualify managed convergence.

The first staged full-runtime pass encountered an in-progress docs/example
sidecar using unrequested shortened names. The main review restored exact
CountItem/HasItem/GiveItem/TakeItem names before the final successful full suite
and clean installs. No shortened API was implemented or adopted. Remote artifact
staging initially lacked license/vendor/Git provenance context; it was completed
from the unchanged starting commit, without changing production or weakening checks.

Metadata generation/drift, exact five-member inventory and four legacy-member
absence, signature/SinceApi/preview checks, 17 preview goldens, API links/architecture
and tooling contracts PASS. Private deterministic Windows/Linux bundles and clean
installs PASS compilation of 69 examples and real-VM GUI/Player execution. Migration
guide is bundled. No DB/journal, fault/live fixture, vendor server assembly, credential
or storage content enters normal packages. Hosted tooling remains a separate gate.

## Controlled live Carbon receipts

Rust 25653776/protocol 2634.289.1, Windows Carbon 2.0.262.0/8a81d70 and Linux
2.0.261.0/c74c4ca remain the qualified tuples. Loaded IsValid/IsRemoved hashes and
zero Harmony topology are rechecked by the C3 fixture. These are actual controlled
BasePlayer connections/physical inventories, not authenticated Rust clients.

Final C3 integration package SHA256:
`b23d9dd5a5050bbe952fe7137f85833e6048ba32bd4155b808a2bdfd1ff61eb6`.
It exercises public Give/Take alongside accepted sampled changes, balanced missed
changes and intermediate transfers, immutable context, publication, reconnect,
replacement, provider retirement, callback faults/fatal recovery and cleanup.

| Receipt | Windows | Linux |
|---|---|---|
| C3 log | `D:\Sandbox\Codex\Entity1AStartup\evidence\inventory-c3-20261011-175253-761\server.log` | `/root/codex/world-movement-20261003/evidence/inventory-c3-20261011-175259-1781373/server.log` |
| C3 SHA256 | `e94aa2fe79929f73a5edbc0954450ba4a40429d752924a2c366ae4fc6a723504` | `a639bc1c38b539fb8791e247473b51bdf02bee2757ea436636c26387b01f71e9` |
| C4 mutation log | `D:\Sandbox\Codex\Entity1AStartup\evidence\inventory-c1-20261011-180050-254\server.log` | `/root/codex/world-movement-20261003/evidence/inventory-c1-20261011-180058-1785450/server.log` |
| C4 SHA256 | `364cd6e6f66f86910d67c35ce31fd5082724bc8cd8f296b4b2b51bf2aceb6c26` | `0ebbff2655aa014944195ef063f3a0e9908e97ad03c6b6eefda4a1f3b9510fda` |
| Native SHA256 | `3883b86f88a2bcb3468c3b53c2e573d07b4b5d4d19669b57fc126fc1bc79109f` | `679099bf4e83b0386f9d0b5ceba6b24df7a8fab8928ec580b7d3ed7bd2907be2` |

C4 mutation package SHA256:
`07c0c0a339456c09f9a3b653c1824755757de5121c965dd3feae1c1bbf180282`.
Default/explicit InventoryOnly, full main/belt stack capacity, definite no-change
rejection, exact Take/Give, acceptance rejection/exception after COMMIT, original
facade staleness and recovery PASS. The first fault fixture installed only a main
container delegate after removal left its remaining stack in belt; it therefore
failed its fault expectation. The corrected fixture covers both possible planner
destinations, restores both delegates, and qualifies controlled indeterminate error
and resource convergence. Preserve the failed receipts at `inventory-c1-20261011-175635-378`
(Windows) and `inventory-c1-20261011-175641-1782698` (Linux).

All runners use loopback-only disposable servers, gracefully quit, verify owned
process exit and restore installed package/native/compiler/hooks/config hashes.
No qualification server remains. Existing caches/evidence are retained. Native
builds reuse qualified dependencies with parallelism two; Linux containers are
CPU/memory bounded. Evidence roots: `D:\Sandbox\Codex\GameplayC4` and
`/root/codex/gameplay-c4-20261011`; native/sanitizer receipts use the existing
Discovery2BNative/gameplay-a20261008 caches. Final hosted CI is not inherited.

## Historical qualification and release handoff

Final implementation-source hosted gates at `e95b8b91f665b16fc0107fe30deff2852868a613`:

| Gate | Receipt |
|---|---|
| Windows/Linux native, managed/real-VM C4/C1/C2/C3, full regressions, persistence adapter, deterministic packaging/clean installs and sanitizer/leak matrix | [Validation 38179036356](https://github.com/gmoddev/CarbonLuau/actions/runs/38179036356): PASS all three jobs; Windows 23/23 native, Linux 24/24 native |
| Windows/Linux/macOS tooling, source metadata/drift, actual host/preview, extension/LSP and goldens | [Tooling 38179036217](https://github.com/gmoddev/CarbonLuau/actions/runs/38179036217): PASS all platforms; macOS tooling is not macOS server support |
| Baseline contracts | [Contracts 38179036370](https://github.com/gmoddev/CarbonLuau/actions/runs/38179036370): PASS |

All new task commits use configured GitHub no-reply author/committer metadata.
Tracked work is clean after closure; pre-existing `.codexlock` and
`derived-369547800317500/` are intentionally retained/untracked, not deleted or
committed. Owned agents, servers and build/test containers are stopped; only
reusable caches, receipts and qualification branch history remain. Final `main`
hosted checks/documentation deployment are verified at publication separately;
the production bytes are those of the tested implementation revision above.

Historical [C1](GameplayC1-Validation.md), [C2](GameplayC2-Validation.md),
[C3](GameplayC3-Validation.md), and [Player-1F-C](qualification/PlayerInteraction.md#playerinteractionfoundation1fc)
remain evidence for their recorded revisions, not proof of this migration.

No release/tag or unrelated Gameplay phase is authorized. C4's migration gate is
closed; next permitted work is separately scoped final Gameplay A–D package/release
preparation, with explicit package identity/scope, final release-source qualification,
provenance/checksums and matching tooling artifact planning before any publication.
It must use the complete current source and API 0.7, not publish an older 0.6.5
manifest with removed methods. Authenticated-client GUI/Teleport outcomes,
universal inventory-operation signals and broader Loot modes remain outside scope.
