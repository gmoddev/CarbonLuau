# Historical documentation

These are preserved source/evidence records, not current implementation
instructions, open gates or new PASS claims. Their negative findings, host/source
hashes, approval chronology and scoped receipts remain intact. Use the
[current index](../Index.md) and [qualification index](../qualification/README.md)
before consulting an original proposal.

The coverage manifest tracks original blobs and retained record spans. Full
records are relocated or consolidated, not silently rewritten as if their final
architecture were obvious. Former URLs have compatibility landing pages; old
commit/tag links continue to identify their actual immutable source.

## World/Entity

- Original lifetime gate: [1A report](world/WorldEntityFoundation1A.md#worldentityfoundation1a),
  [registry continuity](world/WorldEntityLifetimeInvestigation.md#worldentitylifetimeinvestigation).
- Completion attempts: [Spawn epoch](world/WorldEntitySpawnEpochProbe.md#worldentityspawnepochprobe),
  [base-only completion](world/WorldEntityCompletionFenceInvestigation.md#worldentitycompletionfenceinvestigation),
  [early startup observer](world/WorldEntityStartupObserverInvestigation.md#worldentitystartupobserverinvestigation).
- Original [Foundation 1](world/WorldEntityFoundation1.md#worldentityfoundation1)
  and [Foundation 2](world/WorldEntityFoundation2.md#worldentityfoundation2) designs.
- Rejected discovery avenues: [movement](world/WorldEntityDiscoveryMovementInvestigation.md#worldentitydiscoverymovementinvestigation),
  [registry traversal](world/WorldEntityAsyncTraversalInvestigation.md#worldentityasynctraversalinvestigation),
  [catalog](world/WorldEntityCatalogInvestigation.md#worldentitycataloginvestigation),
  [position ordering](world/WorldEntityPositionOrderingInvestigation.md#worldentitypositionorderinginvestigation),
  [position alternatives](world/WorldEntityPositionAlternativesInvestigation.md#worldentitypositionalternativesinvestigation),
  [observation-boundary adoption](world/WorldEntityObservationBoundaryInvestigation.md#worldentityobservationboundaryinvestigation).

## Persistence

- [Durability investigation/adoption](persistence/Durability.md) retains the
  DELETE/EXTRA Windows failure and PERSIST/EXTRA resolution.
- [Allocated-block policy investigation/adoption](persistence/PhysicalAllocation.md)
  retains the failed hard-allocation theorem, operational-budget distinction,
  approval condition and inherited-WAL correction.
- [Single-DB bound](persistence/PersistenceSingleDbBoundInvestigation.md#persistencesingledbboundinvestigation)
  and [Candidate B split proof](persistence/PersistenceCandidateBSplitProof.md#persistencecandidatebsplitproof)
  retain abandoned fit/migration/journal/instruction work. They are not active
  backend directions or logical-to-physical admission promises.
- [1A attempts](persistence/PersistenceFoundation1A-Attempts.md#historical-attempts)
  and [2A attempts](persistence/PersistenceFoundation2A-Attempts.md#historical-attempts).
- Design audits: [Foundation 1](persistence/PersistenceFoundation1-Validation.md#persistencefoundation1-validation),
  [Foundation 2](persistence/PersistenceFoundation2-Validation.md#persistencefoundation2-validation).

Current D21/D22 and current private extent/operational-budget policy remain in
[the canonical persistence rules](../invariants/Persistence.md), not these snapshots.

## Gameplay and inventory

- [Original Events research](gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research),
  [C/D research](gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research),
  [D0 research](gameplay/GameplayD0Architecture-Research.md#gameplayd0architecture-research).
- [Death attribution and NPC episode-reset research](gameplay/DeathAttribution.md).
- [C2 host research](gameplay/GameplayC2-HostResearch.md#gameplayc2-hostresearch)
  retains direct amount writers and intermediate transfer/split/merge counterexamples.
- [Phase 4](inventory/Phase4.md#phase4),
  [Phase 4 validation](inventory/Phase4-Validation.md#phase4-validation),
  [M2 original](inventory/InventoryMutationM2Validation.md#inventorymutationm2validation),
  [ownership/failure reassessment](inventory/InventoryOwnershipFailureReassessment.md#inventoryownershipfailurereassessment),
  [Player original proposal](design/PlayerInteractionFoundation1.md#playerinteractionfoundation1).

## Tooling and original designs

- [Analysis history](tooling/AnalysisHistory.md) consolidates baseline adoption,
  the stopped initial attempt and boundary probes, with each original scope.
- [Supplied tooling architecture](tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture)
  retains complete source; [current deferred routing](../design/CarbonLuau_VSCode_Tooling_Architecture.md)
  reconciles its accepted future seams with current D19/baseline/contracts.
- [First-version prototype](design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design),
  [addon proposal](design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants),
  [addon review](design/Addon-Design-Validation.md#addon-design-validation).

## Maintenance history

[First curation/privacy record](maintenance/HistoryCuration-Validation.md#historycuration-validation)
and [full-history mapping](maintenance/HistoryCuration-FullHistory-Validation.md#historycuration-fullhistory-validation)
retain old/new SHA mappings, tags, privacy/recovery scope and the separate main
cutover outcome. Git history and published release identities are not rewritten
by documentation cleanup.
