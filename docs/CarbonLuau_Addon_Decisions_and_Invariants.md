# CarbonLuau addon architecture — revised design proposal — compatibility routing

This stable compatibility page routes to the complete dated
[record](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants). It is not a second contract or qualification receipt.
For current documentation roles, use the [documentation index](Index.md).

## Original sections

<a id="carbonluau-addon-architecture--revised-design-proposal"></a>

- [CarbonLuau addon architecture — revised design proposal](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-carbonluau-addon-architecture--revised-design-proposal)

<a id="1-direction-and-deliberately-limited-guarantees"></a>

- [1. Direction and deliberately limited guarantees](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-1-direction-and-deliberately-limited-guarantees)

<a id="2-shared-modules-and-lifetime"></a>

- [2. Shared modules and lifetime](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-2-shared-modules-and-lifetime)

<a id="3-execution-and-host-effects"></a>

- [3. Execution and host effects](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-3-execution-and-host-effects)

<a id="4-publication-replacement-and-recovery"></a>

- [4. Publication, replacement and recovery](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-4-publication-replacement-and-recovery)

<a id="5-package-identity-snapshots-and-transport"></a>

- [5. Package identity, snapshots and transport](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-5-package-identity-snapshots-and-transport)

<a id="6-manifest-and-imports--selective-simplification"></a>

- [6. Manifest and imports — selective simplification](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-6-manifest-and-imports--selective-simplification)

<a id="7-dependency-bindings"></a>

- [7. Dependency bindings](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-7-dependency-bindings)

<a id="8-provider-and-host-coordination"></a>

- [8. Provider and host coordination](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-8-provider-and-host-coordination)

<a id="9-scheduling-and-memory"></a>

- [9. Scheduling and memory](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-9-scheduling-and-memory)

<a id="10-delivery-gates-and-exclusions"></a>

- [10. Delivery gates and exclusions](history/design/CarbonLuau_Addon_Decisions_and_Invariants.md#carbonluau_addon_decisions_and_invariants-10-delivery-gates-and-exclusions)
