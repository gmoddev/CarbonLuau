# Player Interaction Foundation 1: current routing

Current development inventory operations belong to `PlayerInventory`, acquired
through `Player:GetInventory()`. C4 removed Player HasItem/CountItem/GiveItem/TakeItem
without aliases under API `0.7.0-experimental`. Older phase receipts retain their
tested Player method spellings; they do not restore those bindings.

The canonical owners are [D11/D13](invariants/Runtime.md),
[D18](invariants/Player.md), and [C4 qualification](GameplayC4-Validation.md).
This page is current routing, not a replacement safety or serialization model.

## Surface and authority

- Immutable Vector3 and live Player Position/Health/MaxHealth observation.
- Committed-only Player Teleport with the qualified host/client limitations.
- Exact-connection PlayerInventory CountItem/HasItem through the single D18
  physical main/belt/wear scanner, not hook-virtualized counts or a new scanner.
- GiveItem/TakeItem with the unchanged D13 PREPARE/COMMIT/VERIFY and original
  Player serialization gate; typed GiveItemBehavior.InventoryOnly is retained.
- Sampled ItemChanged, not individual/completed pickup/drop/transfer events.

Facades retain their original Player connection and defining-domain authority.
Reconnect with the same UserId cannot retarget a stale facade. D7/D10 publication,
provider lifecycle, replacement and VM recovery remain authoritative. No raw Item,
container, CLR/Unity object, new mutation mode or global Inventory service exists.

## Implementation and evidence

| Record | Scope |
|---|---|
| [Player phase ledger](qualification/PlayerInteraction.md) | Vector3/Position, health, item identity/reads, Teleport, TakeItem/GiveItem and combined publication correction |
| [Retained M2 scope](InventoryMutationM2Validation.md) | Historical G1 conclusion superseded; exact-build G2–G5 evidence retained |
| [C1](GameplayC1-Validation.md) | Connection-bound facade acquisition and reads |
| [C2](GameplayC2-Validation.md), [C3](GameplayC3-Validation.md) | Private sampled observer and separately qualified public ItemChanged |
| [C4](GameplayC4-Validation.md) | Current breaking migration, absence of aliases, lifecycle and final-source checks |

Public authors use [Player](api/Types/Player.md),
[PlayerInventory](api/Types/PlayerInventory.md),
[GiveItemBehavior](api/Types/GiveItemBehavior.md),
[examples](api/Player-Examples.md) and the [migration guide](api/Inventory-Migration.md).
Receipts retain exact host/source and authenticated-client limits; they do not
promise rollback, compensating refunds, automatic replay or whole-process isolation.

## Original rationale

The [complete original Player proposal](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1)
and [inventory reassessment](history/inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment)
preserve earlier scope, rejected return models and the I12/D13 adoption chronology.
The [original Phase 4 research](history/inventory/Phase4.md#phase4) remains history,
not an outstanding v0.1 implementation obligation.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="carbonluau-player-interaction-foundation-1--proposed-d18-architecture"></a>

- [CarbonLuau Player Interaction Foundation 1 — proposed D18 architecture](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-carbonluau-player-interaction-foundation-1--proposed-d18-architecture)

<a id="1-tldr-and-exact-recommended-foundation-1-surface"></a>

- [1. TL;DR and exact recommended Foundation 1 surface](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-1-tldr-and-exact-recommended-foundation-1-surface)

<a id="2-current-rustcarbon-technical-findings"></a>

- [2. Current Rust/Carbon technical findings](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-2-current-rustcarbon-technical-findings)

<a id="position"></a>

- [Position](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-position)

<a id="teleportation-is-more-than-assigning-a-position"></a>

- [Teleportation is more than assigning a position](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-teleportation-is-more-than-assigning-a-position)

<a id="health-is-not-just-one-mutable-number"></a>

- [Health is not just one mutable number](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-health-is-not-just-one-mutable-number)

<a id="inventory-reads-and-removals-have-materially-different-risk"></a>

- [Inventory reads and removals have materially different risk](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-inventory-reads-and-removals-have-materially-different-risk)

<a id="item-identity-remains-short-name-based"></a>

- [Item identity remains short-name based](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-item-identity-remains-short-name-based)

<a id="3-vector3-design"></a>

- [3. `Vector3` design](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-3-vector3-design)

<a id="accepted-semantics"></a>

- [Accepted semantics](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-accepted-semantics)

<a id="representation"></a>

- [Representation](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-representation)

<a id="equality"></a>

- [Equality](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-equality)

<a id="arithmetic"></a>

- [Arithmetic](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-arithmetic)

<a id="magnitude"></a>

- [Magnitude](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-magnitude)

<a id="defer"></a>

- [Defer](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-defer)

<a id="4-playerposition-semantics"></a>

- [4. `Player.Position` semantics](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-4-playerposition-semantics)

<a id="meaning"></a>

- [Meaning](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-meaning)

<a id="state-behavior"></a>

- [State behavior](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-state-behavior)

<a id="provisional-execution"></a>

- [Provisional execution](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-provisional-execution)

<a id="5-teleport-semantics-and-failure-model"></a>

- [5. Teleport semantics and failure model](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-5-teleport-semantics-and-failure-model)

<a id="public-contract"></a>

- [Public contract](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-public-contract)

<a id="mounted-and-parented-players"></a>

- [Mounted and parented players](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-mounted-and-parented-players)

<a id="destination-semantics"></a>

- [Destination semantics](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-destination-semantics)

<a id="required-backend-responsibilities"></a>

- [Required backend responsibilities](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-required-backend-responsibilities)

<a id="triggers-and-collision"></a>

- [Triggers and collision](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-triggers-and-collision)

<a id="success"></a>

- [Success](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-success)

<a id="failure-after-mutation-begins"></a>

- [Failure after mutation begins](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-failure-after-mutation-begins)

<a id="provisional-execution-1"></a>

- [Provisional execution](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-provisional-execution-1)

<a id="6-health-read-semantics"></a>

- [6. Health read semantics](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-6-health-read-semantics)

<a id="health"></a>

- [`Health`](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-health)

<a id="maxhealth"></a>

- [`MaxHealth`](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-maxhealth)

<a id="life-states"></a>

- [Life states](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-life-states)

<a id="stale-behavior"></a>

- [Stale behavior](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-stale-behavior)

<a id="7-health-mutation-decision"></a>

- [7. Health mutation decision](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-7-health-mutation-decision)

<a id="8-inventory-observation-semantics"></a>

- [8. Inventory observation semantics](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-8-inventory-observation-semantics)

<a id="what-inventory-means"></a>

- [What "inventory" means](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-what-inventory-means)

<a id="countitem"></a>

- [`CountItem`](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-countitem)

<a id="hasitem"></a>

- [`HasItem`](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-hasitem)

<a id="do-not-route-this-through-hook-virtualized-inventory-counting"></a>

- [Do not route this through hook-virtualized inventory counting](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-do-not-route-this-through-hook-virtualized-inventory-counting)

<a id="provisional-execution-2"></a>

- [Provisional execution](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-provisional-execution-2)

<a id="9-item-identity-and-items-service-decision"></a>

- [9. Item identity and `Items` service decision](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-9-item-identity-and-items-service-decision)

<a id="why-include-it"></a>

- [Why include it](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-why-include-it)

<a id="canonical-identity-contract"></a>

- [Canonical identity contract](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-canonical-identity-contract)

<a id="relationship-to-d13"></a>

- [Relationship to D13](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-relationship-to-d13)

<a id="10-takeitem-safety-investigation-and-decision"></a>

- [10. `TakeItem` safety investigation and decision](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-10-takeitem-safety-investigation-and-decision)

<a id="why-boolean-all-or-nothing-is-wrong"></a>

- [Why boolean all-or-nothing is wrong](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-why-boolean-all-or-nothing-is-wrong)

<a id="why-removedamount-is-not-clean-enough-either"></a>

- [Why `removedAmount` is not clean enough either](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-why-removedamount-is-not-clean-enough-either)

<a id="11-giveitem--d13-status"></a>

- [11. `GiveItem` / D13 status](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-11-giveitem--d13-status)

<a id="12-exact-player-lifetime-and-stale-proxy-behavior"></a>

- [12. Exact Player lifetime and stale-proxy behavior](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-12-exact-player-lifetime-and-stale-proxy-behavior)

<a id="13-threading-and-reentrancy-analysis"></a>

- [13. Threading and reentrancy analysis](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-13-threading-and-reentrancy-analysis)

<a id="read-paths"></a>

- [Read paths](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-read-paths)

<a id="teleport"></a>

- [Teleport](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-teleport)

<a id="14-provisionalpublication-classification"></a>

- [14. Provisional/publication classification](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-14-provisionalpublication-classification)

<a id="15-error-and-return-value-conventions"></a>

- [15. Error and return-value conventions](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-15-error-and-return-value-conventions)

<a id="16-bounds-and-resource-model"></a>

- [16. Bounds and resource model](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-16-bounds-and-resource-model)

<a id="vector3"></a>

- [Vector3](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-vector3)

<a id="item-names"></a>

- [Item names](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-item-names)

<a id="amount"></a>

- [Amount](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-amount)

<a id="count-accumulation"></a>

- [Count accumulation](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-count-accumulation)

<a id="inventory-traversal"></a>

- [Inventory traversal](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-inventory-traversal)

<a id="diagnostics"></a>

- [Diagnostics](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-diagnostics)

<a id="17-roblox-like-semantics-versus-rust-reality"></a>

- [17. Roblox-like semantics versus Rust reality](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-17-roblox-like-semantics-versus-rust-reality)

<a id="18-exact-accepted-public-api-matrix"></a>

- [18. Exact accepted public API matrix](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-18-exact-accepted-public-api-matrix)

<a id="19-exact-deferredrejected-apis"></a>

- [19. Exact deferred/rejected APIs](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-19-exact-deferredrejected-apis)

<a id="20-compatibility-with-the-current-player-api"></a>

- [20. Compatibility with the current Player API](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-20-compatibility-with-the-current-player-api)

<a id="existing-bootstrapvalue-architecture"></a>

- [Existing bootstrap/value architecture](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-existing-bootstrapvalue-architecture)

<a id="private-host-bridge"></a>

- [Private host bridge](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-private-host-bridge)

<a id="scripting-api-identity"></a>

- [Scripting API identity](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-scripting-api-identity)

<a id="d13-compatibility"></a>

- [D13 compatibility](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-d13-compatibility)

<a id="21-proposed-canonical-d18-text"></a>

- [21. Proposed canonical D18 text](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-21-proposed-canonical-d18-text)

<a id="22-implementation-phases"></a>

- [22. Implementation phases](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-22-implementation-phases)

<a id="player-1a--vector3--position"></a>

- [Player-1A — `Vector3` + Position](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-player-1a--vector3--position)

<a id="player-1b--health-observation"></a>

- [Player-1B — Health observation](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-player-1b--health-observation)

<a id="player-1c--item-identity--inventory-observation"></a>

- [Player-1C — Item identity + inventory observation](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-player-1c--item-identity--inventory-observation)

<a id="player-1d--teleport"></a>

- [Player-1D — Teleport](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-player-1d--teleport)

<a id="player-1e--combined-lifecyclepublic-closure"></a>

- [Player-1E — Combined lifecycle/public closure](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-player-1e--combined-lifecyclepublic-closure)

<a id="23-qualification-plan"></a>

- [23. Qualification plan](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-23-qualification-plan)

<a id="controlled-modelmanaged-qualification"></a>

- [Controlled model/managed qualification](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-controlled-modelmanaged-qualification)

<a id="vector3-1"></a>

- [`Vector3`](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-vector3-1)

<a id="positionhealth"></a>

- [Position/health](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-positionhealth)

<a id="inventory-observation"></a>

- [Inventory observation](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-inventory-observation)

<a id="teleport-host-tests"></a>

- [Teleport host tests](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-teleport-host-tests)

<a id="teleport-reentrancy"></a>

- [Teleport reentrancy](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-teleport-reentrancy)

<a id="provisional-tests"></a>

- [Provisional tests](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-provisional-tests)

<a id="windowslinux"></a>

- [Windows/Linux](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-windowslinux)

<a id="sanitizers"></a>

- [Sanitizers](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-sanitizers)

<a id="authenticated-real-client-gate"></a>

- [Authenticated real-client gate](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-authenticated-real-client-gate)

<a id="24-genuinely-unresolved-host-dependent-gates"></a>

- [24. Genuinely unresolved host-dependent gates](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-24-genuinely-unresolved-host-dependent-gates)

<a id="a-exact-target-build-teleport-sequence"></a>

- [A. Exact target-build Teleport sequence](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-a-exact-target-build-teleport-sequence)

<a id="b-authenticated-client-teleport-convergence"></a>

- [B. Authenticated-client Teleport convergence](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-b-authenticated-client-teleport-convergence)

<a id="c-exact-host-layout-verification-for-deterministic-inventory-reads"></a>

- [C. Exact host-layout verification for deterministic inventory reads](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-c-exact-host-layout-verification-for-deterministic-inventory-reads)

<a id="25-final-verdict"></a>

- [25. Final verdict](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-25-final-verdict)

<a id="ready-for-canonical-adoption"></a>

- [**READY FOR CANONICAL ADOPTION**](history/design/PlayerInteractionFoundation1.md#playerinteractionfoundation1-ready-for-canonical-adoption)
