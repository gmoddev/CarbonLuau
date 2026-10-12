# Inventory ownership/failure rationale: current scope

[I12](Invariants.md#i12--trusted-in-process-host-interference),
[D13](invariants/Runtime.md#d13--inventory-ownership-and-failure-model) and
[D18](invariants/Player.md) own the accepted rules. The
[complete original reassessment](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment)
retains its pre-implementation source, proposals and subsequent amendments.

## Accepted distinction

Rust inventory mutations are not transactions. PREPARE must be bounded and
mutation-free; COMMIT begins irreversible host effects; VERIFY decides whether
the qualified physical result is established. False is a pre-COMMIT rejection,
true requires verified completion, and a post-COMMIT uncertain result is an error.
There is no universal rollback, refund, exactly-once or replay promise.

CarbonLuau retains responsibility for resources actually returned to its adapter
until they are accepted, consumed or safely disposed under the qualified model.
Objects never returned by a failing host call remain in the host failure domain;
that does not excuse an unsafe adapter or an unverified success claim.

Trusted external in-process mutation is not semantically isolated. It may race
the supported host operation, but lifetime safety, bounds, conservative guards,
physical verification and truthful failure classification remain mandatory.
I12 is not permission to ignore ordinary acceptance/rejection or to weaken D13.

## Current application

Use [PlayerInventory](api/Types/PlayerInventory.md). Its GiveItem/TakeItem reuse
the original-connection gate and one scanner across domains. Only the qualified
InventoryOnly behavior is implemented; no DropRemainder or compensating world
deletion is implied. [The Player ledger](qualification/PlayerInteraction.md),
[retained M2 evidence](InventoryMutationM2Validation.md) and
[C4](GameplayC4-Validation.md) keep implementation/source/host scopes distinct.

Sampled [ItemChanged](api/Types/PlayerInventory.md#itemchanged) is observation,
not a host transaction completion receipt. Balanced/intermediate changes and
overload behavior retain their separately qualified C2/C3 semantics.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="d13-inventory-ownership--failure-reassessment"></a>

- [D13 Inventory Ownership / Failure Reassessment](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-d13-inventory-ownership--failure-reassessment)

<a id="1-tldr"></a>

- [1. TL;DR](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-1-tldr)

<a id="2-where-original-d13-is-too-strict"></a>

- [2. Where original D13 is too strict](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-2-where-original-d13-is-too-strict)

<a id="3-revised-ownership-boundary"></a>

- [3. Revised ownership boundary](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-3-revised-ownership-boundary)

<a id="a-carbonluau-owned-resources"></a>

- [A. CarbonLuau-owned resources](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-a-carbonluau-owned-resources)

<a id="b-returned-host-resources-under-carbonluau-responsibility"></a>

- [B. Returned host resources under CarbonLuau responsibility](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-b-returned-host-resources-under-carbonluau-responsibility)

<a id="c-unreturned-host-internal-resources"></a>

- [C. Unreturned host-internal resources](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-c-unreturned-host-internal-resources)

<a id="4-precise-atomicity-taxonomy"></a>

- [4. Precise atomicity taxonomy](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-4-precise-atomicity-taxonomy)

<a id="5-prepare-semantics"></a>

- [5. PREPARE semantics](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-5-prepare-semantics)

<a id="prepare-must-not-invoke-canacceptitem"></a>

- [PREPARE must not invoke `CanAcceptItem`](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-prepare-must-not-invoke-canacceptitem)

<a id="what-prepare-guarantees"></a>

- [What PREPARE guarantees](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-what-prepare-guarantees)

<a id="6-exact-commit-boundary"></a>

- [6. Exact COMMIT boundary](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-6-exact-commit-boundary)

<a id="7-verify-semantics"></a>

- [7. VERIFY semantics](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-7-verify-semantics)

<a id="giveitem-verification"></a>

- [GiveItem verification](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-giveitem-verification)

<a id="why-count-delta-alone-is-insufficient"></a>

- [Why count delta alone is insufficient](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-why-count-delta-alone-is-insufficient)

<a id="provenance-limitation"></a>

- [Provenance limitation](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-provenance-limitation)

<a id="takeitem-verification"></a>

- [TakeItem verification](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-takeitem-verification)

<a id="8-operation-outcome-model"></a>

- [8. Operation outcome model](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-8-operation-outcome-model)

<a id="rejected"></a>

- [REJECTED](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-rejected)

<a id="success"></a>

- [SUCCESS](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-success)

<a id="indeterminate-host-failure"></a>

- [INDETERMINATE HOST FAILURE](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-indeterminate-host-failure)

<a id="9-per-player-carbonluau-mutation-serialization"></a>

- [9. Per-Player CarbonLuau mutation serialization](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-9-per-player-carbonluau-mutation-serialization)

<a id="cross-addon-behavior"></a>

- [Cross-addon behavior](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-cross-addon-behavior)

<a id="disconnect"></a>

- [Disconnect](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-disconnect)

<a id="replacementrecovery"></a>

- [Replacement/recovery](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-replacementrecovery)

<a id="10-synchronous-plugin-interference-model"></a>

- [10. Synchronous plugin interference model](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-10-synchronous-plugin-interference-model)

<a id="11-giveitem-decision"></a>

- [11. GiveItem decision](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-11-giveitem-decision)

<a id="accept-architecturally"></a>

- [ACCEPT ARCHITECTURALLY](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-accept-architecturally)

<a id="12-exact-giveitem-public-contract"></a>

- [12. Exact GiveItem public contract](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-12-exact-giveitem-public-contract)

<a id="inputs"></a>

- [Inputs](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-inputs)

<a id="inventory-destination"></a>

- [Inventory destination](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-inventory-destination)

<a id="stacking"></a>

- [Stacking](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-stacking)

<a id="multiple-stacks"></a>

- [Multiple stacks](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-multiple-stacks)

<a id="partial-placement"></a>

- [Partial placement](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-partial-placement)

<a id="full-inventory"></a>

- [Full inventory](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-full-inventory)

<a id="exact-player"></a>

- [Exact Player](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-exact-player)

<a id="provisional"></a>

- [Provisional](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-provisional)

<a id="13-world-drop-decision"></a>

- [13. World-drop decision](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-13-world-drop-decision)

<a id="14-takeitem-decision"></a>

- [14. TakeItem decision](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-14-takeitem-decision)

<a id="accept-architecturally-1"></a>

- [ACCEPT ARCHITECTURALLY](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-accept-architecturally-1)

<a id="verify"></a>

- [VERIFY](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-verify)

<a id="why-this-is-now-acceptable"></a>

- [Why this is now acceptable](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-why-this-is-now-acceptable)

<a id="15-provisional-d11-and-deadline-behavior"></a>

- [15. Provisional, D11 and deadline behavior](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-15-provisional-d11-and-deadline-behavior)

<a id="provisional-1"></a>

- [Provisional](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-provisional-1)

<a id="d11"></a>

- [D11](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-d11)

<a id="deadlines"></a>

- [Deadlines](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-deadlines)

<a id="16-safety-and-resource-bounds"></a>

- [16. Safety and resource bounds](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-16-safety-and-resource-bounds)

<a id="short-name"></a>

- [Short name](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-short-name)

<a id="amount"></a>

- [Amount](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-amount)

<a id="physical-inventory-inspection"></a>

- [Physical inventory inspection](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-physical-inventory-inspection)

<a id="placement-work"></a>

- [Placement work](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-placement-work)

<a id="verification"></a>

- [Verification](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-verification)

<a id="mutation-gate"></a>

- [Mutation gate](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-mutation-gate)

<a id="diagnostics"></a>

- [Diagnostics](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-diagnostics)

<a id="17-d13-amendments"></a>

- [17. D13 amendments](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-17-d13-amendments)

<a id="keep"></a>

- [Keep](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-keep)

<a id="amend"></a>

- [Amend](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-amend)

<a id="supersede"></a>

- [Supersede](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-supersede)

<a id="18-d18-implications"></a>

- [18. D18 implications](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-18-d18-implications)

<a id="player-1f--inventory-mutation"></a>

- [Player-1F — inventory mutation](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-player-1f--inventory-mutation)

<a id="19-roblox-like-author-facing-examples"></a>

- [19. Roblox-like author-facing examples](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-19-roblox-like-author-facing-examples)

<a id="20-exact-behavior-still-deferredrejected"></a>

- [20. Exact behavior still deferred/rejected](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-20-exact-behavior-still-deferredrejected)

<a id="21-implementation-phases"></a>

- [21. Implementation phases](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-21-implementation-phases)

<a id="inventory-m1--deterministic-transaction-model"></a>

- [Inventory-M1 — deterministic transaction model](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-inventory-m1--deterministic-transaction-model)

<a id="inventory-m2--exact-target-build-adapter-qualification"></a>

- [Inventory-M2 — exact target-build adapter qualification](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-inventory-m2--exact-target-build-adapter-qualification)

<a id="player-1f-a--takeitem"></a>

- [Player-1F-A — TakeItem](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-player-1f-a--takeitem)

<a id="player-1f-b--giveitem"></a>

- [Player-1F-B — GiveItem](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-player-1f-b--giveitem)

<a id="player-1f-c--combined-lifecyclepublic-closure"></a>

- [Player-1F-C — combined lifecycle/public closure](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-player-1f-c--combined-lifecyclepublic-closure)

<a id="22-qualification-plan"></a>

- [22. Qualification plan](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-22-qualification-plan)

<a id="deterministic-model-tests"></a>

- [Deterministic model tests](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-deterministic-model-tests)

<a id="giveitem-host-cases"></a>

- [GiveItem host cases](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-giveitem-host-cases)

<a id="takeitem-host-cases"></a>

- [TakeItem host cases](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-takeitem-host-cases)

<a id="callbackreentrancy"></a>

- [Callback/reentrancy](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-callbackreentrancy)

<a id="lifecycle"></a>

- [Lifecycle](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-lifecycle)

<a id="platforms"></a>

- [Platforms](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-platforms)

<a id="23-genuinely-unresolved-host-dependent-gates"></a>

- [23. Genuinely unresolved host-dependent gates](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-23-genuinely-unresolved-host-dependent-gates)

<a id="g1--giveitem-no-world-drop-adapter"></a>

- [G1 — GiveItem no-world-drop adapter](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-g1--giveitem-no-world-drop-adapter)

<a id="g2--returned-item-terminal-state-observability"></a>

- [G2 — returned-Item terminal-state observability](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-g2--returned-item-terminal-state-observability)

<a id="g3--target-build-take-verification"></a>

- [G3 — target-build Take verification](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-g3--target-build-take-verification)

<a id="g4--cleanup-handling-for-returned-unattached-items"></a>

- [G4 — cleanup handling for returned unattached items](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-g4--cleanup-handling-for-returned-unattached-items)

<a id="g5--work-bounds-from-actual-container-maxima"></a>

- [G5 — work bounds from actual container maxima](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-g5--work-bounds-from-actual-container-maxima)

<a id="24-final-verdict"></a>

- [24. Final verdict](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-24-final-verdict)

<a id="ready-for-canonical-adoption"></a>

- [READY FOR CANONICAL ADOPTION](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment-ready-for-canonical-adoption)
