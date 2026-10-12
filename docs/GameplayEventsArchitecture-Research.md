# Gameplay Events, Hooks & Policies: supporting research — compatibility routing

This stable compatibility page routes to the complete dated
[record](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research). It is not a second contract or qualification receipt.
For current documentation roles, use the [documentation index](Index.md).

## Original sections

<a id="gameplay-events-hooks--policies-supporting-research"></a>

- [Gameplay Events, Hooks & Policies: supporting research](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-gameplay-events-hooks--policies-supporting-research)

<a id="carbonluau--gameplay-events-hooks--policies-architecture"></a>

- [CarbonLuau — Gameplay Events, Hooks & Policies Architecture](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-carbonluau--gameplay-events-hooks--policies-architecture)

<a id="1-executive-verdict"></a>

- [1. Executive verdict](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-1-executive-verdict)

<a id="2-existing-architecture-and-integration-inventory"></a>

- [2. Existing architecture and integration inventory](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-2-existing-architecture-and-integration-inventory)

<a id="canonical-authority-to-preserve"></a>

- [Canonical authority to preserve](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-canonical-authority-to-preserve)

<a id="system-boundary"></a>

- [System boundary](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-system-boundary)

<a id="3-final-recommended-public-api"></a>

- [3. Final recommended public API](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-3-final-recommended-public-api)

<a id="accepted-ergonomic-shape-subject-to-host-proof"></a>

- [Accepted ergonomic shape, subject to host proof](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-accepted-ergonomic-shape-subject-to-host-proof)

<a id="explicitly-excluded-now"></a>

- [Explicitly excluded now](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-explicitly-excluded-now)

<a id="4-naming-decisions"></a>

- [4. Naming decisions](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-4-naming-decisions)

<a id="5-minimal-event-context-types"></a>

- [5. Minimal event context types](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-5-minimal-event-context-types)

<a id="gameplay-a-target"></a>

- [Gameplay A target](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-gameplay-a-target)

<a id="gameplay-b-target"></a>

- [Gameplay B target](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-gameplay-b-target)

<a id="6-signals-versus-policies--two-abstractions-only"></a>

- [6. Signals versus policies — two abstractions only](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-6-signals-versus-policies--two-abstractions-only)

<a id="signals-passive-observational-stream"></a>

- [Signals: passive observational stream](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-signals-passive-observational-stream)

<a id="policies-pre-registered-validated-decisions"></a>

- [Policies: pre-registered, validated decisions](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-policies-pre-registered-validated-decisions)

<a id="7-host-mapping-and-proof-ledger"></a>

- [7. Host mapping and proof ledger](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-7-host-mapping-and-proof-ledger)

<a id="required-12-point-proof-record-per-candidate"></a>

- [Required 12-point proof record per candidate](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-required-12-point-proof-record-per-candidate)

<a id="8-timing-delivery-and-event-order"></a>

- [8. Timing, delivery and event order](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-8-timing-delivery-and-event-order)

<a id="9-player-lifetime-integration-and-edge-cases"></a>

- [9. Player lifetime integration and edge cases](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-9-player-lifetime-integration-and-edge-cases)

<a id="required-playerdied-semantics"></a>

- [Required PlayerDied semantics](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-required-playerdied-semantics)

<a id="required-playerspawned-semantics"></a>

- [Required PlayerSpawned semantics](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-required-playerspawned-semantics)

<a id="first-phase-acceptance-checklist"></a>

- [First-phase acceptance checklist](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-first-phase-acceptance-checklist)

<a id="10-entity-lifetime-integration"></a>

- [10. Entity lifetime integration](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-10-entity-lifetime-integration)

<a id="11-damage-and-kill-attribution"></a>

- [11. Damage and kill attribution](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-11-damage-and-kill-attribution)

<a id="12-destruction-versus-death--explicit-state-machine"></a>

- [12. Destruction versus death — explicit state machine](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-12-destruction-versus-death--explicit-state-machine)

<a id="13-custom-barrel-loot-and-destruction-rewards"></a>

- [13. Custom barrel loot and destruction rewards](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-13-custom-barrel-loot-and-destruction-rewards)

<a id="observation-use-cases-accepted-separate-from-mutation"></a>

- [Observation use cases accepted, separate from mutation](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-observation-use-cases-accepted-separate-from-mutation)

<a id="why-onlootspawn-is-not-automatically-barrel-drop-replacement"></a>

- [Why `OnLootSpawn` is not automatically barrel-drop replacement](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-why-onlootspawn-is-not-automatically-barrel-drop-replacement)

<a id="conditional-loot-service-target"></a>

- [Conditional `Loot` service target](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-conditional-loot-service-target)

<a id="declarative-policy-admission-ownership-conflict-and-failure"></a>

- [Declarative policy admission, ownership, conflict and failure](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-declarative-policy-admission-ownership-conflict-and-failure)

<a id="required-live-proof"></a>

- [Required live proof](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-required-live-proof)

<a id="14-inventory-event-feasibility"></a>

- [14. Inventory event feasibility](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-14-inventory-event-feasibility)

<a id="why-two-generic-callbacks-are-currently-unsound"></a>

- [Why two generic callbacks are currently unsound](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-why-two-generic-callbacks-are-currently-unsound)

<a id="feasibility-decision"></a>

- [Feasibility decision](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-feasibility-decision)

<a id="15-resource-accounting-and-queue-limits"></a>

- [15. Resource accounting and queue limits](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-15-resource-accounting-and-queue-limits)

<a id="proposed-event-specific-hard-admission-budget-requires-measurement"></a>

- [Proposed event-specific hard admission budget (requires measurement)](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-proposed-event-specific-hard-admission-budget-requires-measurement)

<a id="16-publication-addon-replacement-provider-unload-vm-recovery"></a>

- [16. Publication, addon replacement, provider unload, VM recovery](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-16-publication-addon-replacement-provider-unload-vm-recovery)

<a id="17-compatibility-and-host-version-contract"></a>

- [17. Compatibility and host-version contract](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-17-compatibility-and-host-version-contract)

<a id="18-security-safety-and-failure-analysis"></a>

- [18. Security, safety and failure analysis](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-18-security-safety-and-failure-analysis)

<a id="19-accepted-deferred-and-rejected-features"></a>

- [19. Accepted, deferred and rejected features](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-19-accepted-deferred-and-rejected-features)

<a id="20-phased-roadmap-ranked"></a>

- [20. Phased roadmap (ranked)](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-20-phased-roadmap-ranked)

<a id="21-validation-matrix-and-evidence-contract"></a>

- [21. Validation matrix and evidence contract](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-21-validation-matrix-and-evidence-contract)

<a id="common-negative-fixtures"></a>

- [Common negative fixtures](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-common-negative-fixtures)

<a id="pass-definition"></a>

- [PASS definition](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-pass-definition)

<a id="22-canonical-invariant-additionsamendments"></a>

- [22. Canonical invariant additions/amendments](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-22-canonical-invariant-additionsamendments)

<a id="23-v060-release-impact-and-freeze-strategy"></a>

- [23. v0.6.0 release impact and freeze strategy](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-23-v060-release-impact-and-freeze-strategy)

<a id="24-open-host-proof-questions-must-be-resolved-not-guessed"></a>

- [24. Open host-proof questions (must be resolved, not guessed)](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-24-open-host-proof-questions-must-be-resolved-not-guessed)

<a id="25-exact-recommended-first-implementation-phase--gameplay-a"></a>

- [25. Exact recommended first implementation phase — Gameplay A](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-25-exact-recommended-first-implementation-phase--gameplay-a)

<a id="scope-and-sequence"></a>

- [Scope and sequence](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-scope-and-sequence)

<a id="scope-exclusions"></a>

- [Scope exclusions](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-scope-exclusions)

<a id="stop-conditions"></a>

- [Stop conditions](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-stop-conditions)

<a id="suggested-codex-task-delta-not-an-authorization-to-implement"></a>

- [Suggested Codex task delta (not an authorization to implement)](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-suggested-codex-task-delta-not-an-authorization-to-implement)

<a id="reference-register"></a>

- [Reference register](history/gameplay/GameplayEventsArchitecture-Research.md#gameplayeventsarchitecture-research-reference-register)
