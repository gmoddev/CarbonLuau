# CarbonLuau AI and contributor policy

This document owns contribution workflow and prompt construction. It applies to CarbonLuau only. Phase 0's accepted source baseline is `a88f2eb`; read the current checkout and [validation record](docs/Phase0-Validation.md) before relying on that baseline. Phase 1 implementation and qualification are documented in [Phase1.md](docs/Phase1.md) and [Phase1-Validation.md](docs/Phase1-Validation.md). Phase 2 is documented in [Phase2.md](docs/Phase2.md), [Phase2-Validation.md](docs/Phase2-Validation.md) and invariant D9. The authorized Phase 3 facade, approved provisional-effect policy and qualification status are in [Phase3.md](docs/Phase3.md), [Phase3-Validation.md](docs/Phase3-Validation.md) and D10–D12. Script authors start at the [public API reference](docs/api/README.md). Revised D13 owns inventory ownership/failure semantics, D18 owns Player Interaction Foundation 1, and D20 owns the read-only World/Entity Foundation 1 architecture; their implemented or design-only status is routed below.

## Authority and reading order

| Question | Canonical owner |
|---|---|
| How to scope work and construct prompts | This document |
| Official editor tooling ownership, contracts and security | [D19 in Invariants.md](docs/Invariants.md#d19--official-editor-tooling) and [ToolingBaseline.md](docs/ToolingBaseline.md) |
| Tooling Foundation A implementation design and later routing | [ToolingFoundationA.md](docs/ToolingFoundationA.md) |
| Tooling Foundation A implementation, platform qualification and Foundation B handoff | [ToolingFoundationACompletion.md](docs/ToolingFoundationACompletion.md) |
| Tooling Foundation B preview implementation, scoped determinism and Foundation C interface | [ToolingFoundationB.md](docs/ToolingFoundationB.md) |
| Tooling Foundation B qualification, delivery revisions and platform limits | [ToolingFoundationBCompletion.md](docs/ToolingFoundationBCompletion.md) |
| Language-analysis execution, Workspace Trust, process limits and Foundation A security handoff | [D19 analysis security amendment](docs/ToolingLanguageAnalysisSecurity.md) and [investigation evidence](docs/ToolingLanguageAnalysisSecurityEvidence.md) |
| Product purpose, architecture, trust, ownership, ABI, threading, lifecycle and resource rules | [Invariants.md](docs/Invariants.md) |
| Provisional/open/deferred decisions and implementation gates | [Decision register in Invariants.md](docs/Invariants.md#decision-register) |
| Canonical GUI Foundation 1 semantics | [D15 in Invariants.md](docs/Invariants.md#d15--gui-foundation-1-retained-presentation-model) |
| GUI Foundation 1 rationale and implementation guidance | [GuiFoundation1.md](docs/GuiFoundation1.md) |
| GUI Foundation 1A internal substrate and evidence | [GuiFoundation1A.md](docs/GuiFoundation1A.md) |
| GUI Foundation 1B retained runtime and evidence | [GuiFoundation1B.md](docs/GuiFoundation1B.md) |
| GUI Foundation 1C presentation/CUI projection and evidence | [GuiFoundation1C.md](docs/GuiFoundation1C.md) |
| GUI Foundation 1D synchronization/reconciliation and evidence | [GuiFoundation1D.md](docs/GuiFoundation1D.md) |
| GUI Foundation 1E secure Activated ingress and evidence | [GuiFoundation1E.md](docs/GuiFoundation1E.md) |
| GUI Foundation 1F lifecycle/runtime closure and evidence | [GuiFoundation1F.md](docs/GuiFoundation1F.md) |
| GUI Foundation 1G public closure, identity and evidence | [GuiFoundation1G.md](docs/GuiFoundation1G.md) |
| Canonical GUI Foundation 2 additive semantics | [D16 in Invariants.md](docs/Invariants.md#d16--gui-foundation-2-deterministic-layout-and-rich-controls) |
| GUI Foundation 2 rationale, surface matrices and implementation guidance | [GuiFoundation2.md](docs/GuiFoundation2.md) |
| GUI Foundation 2A deterministic layout implementation and evidence | [GuiFoundation2A.md](docs/GuiFoundation2A.md) |
| GUI Foundation 2B typed-image implementation and evidence | [GuiFoundation2B.md](docs/GuiFoundation2B.md) |
| GUI Foundation 2C scrolling implementation and evidence | [GuiFoundation2C.md](docs/GuiFoundation2C.md) |
| GUI Foundation 2E lifecycle and rich-control closure | [GuiFoundation2E.md](docs/GuiFoundation2E.md) |
| GUI Foundation 2F public qualification and release-candidate closure | [GuiFoundation2F.md](docs/GuiFoundation2F.md) |
| Canonical GUI Foundation 3 additive semantics | [D17 in Invariants.md](docs/Invariants.md#d17---gui-foundation-3-deterministic-grids-clipping-fonts-and-presentation-scroll-intent) |
| GUI Foundation 3 rationale, surface matrices, implementation guidance and qualification gates | [GuiFoundation3.md](docs/GuiFoundation3.md) |
| GUI Foundation 3A deterministic-grid implementation and evidence | [GuiFoundation3A.md](docs/GuiFoundation3A.md) |
| GUI Foundation 3B clipping implementation and evidence | [GuiFoundation3B.md](docs/GuiFoundation3B.md) |
| GUI Foundation 3C retained-font implementation and evidence | [GuiFoundation3C.md](docs/GuiFoundation3C.md) |
| GUI Foundation 3D Presentation-scroll-effect implementation and evidence | [GuiFoundation3D.md](docs/GuiFoundation3D.md) |
| GUI Foundation 3E lifecycle, scale, compatibility and release-candidate closure | [GuiFoundation3E.md](docs/GuiFoundation3E.md) |
| Canonical Player Interaction Foundation 1 semantics | [D18 in Invariants.md](docs/Invariants.md#d18--player-interaction-foundation-1) |
| Player Interaction Foundation 1 rationale, host evidence, phases and qualification gates | [PlayerInteractionFoundation1.md](docs/PlayerInteractionFoundation1.md) |
| Player Interaction Foundation 1A Vector3/Position implementation and evidence | [PlayerInteractionFoundation1A.md](docs/PlayerInteractionFoundation1A.md) |
| Player Interaction Foundation 1B Health/MaxHealth implementation and evidence | [PlayerInteractionFoundation1B.md](docs/PlayerInteractionFoundation1B.md) |
| Player Interaction Foundation 1C Items/inventory observation implementation and evidence | [PlayerInteractionFoundation1C.md](docs/PlayerInteractionFoundation1C.md) |
| Player Interaction Foundation 1D Teleport implementation and qualification | [PlayerInteractionFoundation1D.md](docs/PlayerInteractionFoundation1D.md) |
| Player Interaction Foundation 1F-A TakeItem implementation and qualification | [PlayerInteractionFoundation1FA.md](docs/PlayerInteractionFoundation1FA.md) |
| Player Interaction Foundation 1F-B GiveItem implementation and historical investigation | [PlayerInteractionFoundation1FB.md](docs/PlayerInteractionFoundation1FB.md), [qualification](docs/PlayerInteractionFoundation1FB-Validation.md) |
| Player Interaction Foundation 1F-C combined closure and cold-module mutation correction | [PlayerInteractionFoundation1FC.md](docs/PlayerInteractionFoundation1FC.md) |
| Canonical World/Entity Foundation 1 semantics | [D20 in Invariants.md](docs/Invariants.md#d20--worldentity-foundation-1) |
| World/Entity Foundation 1 rationale, host evidence, phase routing and qualification gates | [WorldEntityFoundation1.md](docs/WorldEntityFoundation1.md) |
| World/Entity Foundation 1A blocked lifetime-proof investigation (not implementation) | [WorldEntityFoundation1A.md](docs/WorldEntityFoundation1A.md) |
| Authoritative Entity lifetime investigation, negative evidence and D20 deferral | [WorldEntityLifetimeInvestigation.md](docs/WorldEntityLifetimeInvestigation.md) |
| Canonical Persistence Foundation 1 architecture (resolved; private 1A qualified) | [D21 in Invariants.md](docs/Invariants.md#d21--persistence-foundation-1) and [PersistenceFoundation1.md](docs/PersistenceFoundation1.md) |
| Persistence design source evidence, consistency review and qualification limits | [PersistenceFoundation1-Validation.md](docs/PersistenceFoundation1-Validation.md) |
| Persistence-1A private backend/codec/namespace/queue substrate PASS; WAL rejection and supported recovery qualified; no public API qualified by 1A | [PersistenceFoundation1A.md](docs/PersistenceFoundation1A.md); historical DELETE and physical-proof negative evidence preserved |
| Persistence-1B public facade/admission implemented and qualified within recorded scope; experimental API 0.5.0-experimental; 1C NOT STARTED | [PersistenceFoundation1B.md](docs/PersistenceFoundation1B.md); D12 owns the assignment and [Release.md](docs/Release.md) the separate development package mapping. No release or overall closure is implied. |
| Persistence physical-allocation investigation and adopted operational-budget amendment, qualification condition closed by 1A | [Investigation](docs/PersistencePhysicalAllocationInvestigation.md) and [adopted D21 amendment](docs/PersistencePhysicalD21Amendment-Proposed.md); 1,280 MiB is an operational safety budget/qualification target/diagnostic threshold, not a hard physical invariant |
| Persistence durability follow-up: approved PERSIST path, platform probes and adoption record | [PersistenceDurabilityInvestigation.md](docs/PersistenceDurabilityInvestigation.md) and [approved D21 amendment](docs/PersistenceD21Amendment-Proposed.md) |
| Revised D13 inventory ownership/failure rationale and target-build gates | [InventoryOwnershipFailureReassessment.md](docs/InventoryOwnershipFailureReassessment.md) |
| Inventory-M2 exact-build evidence; G1 conclusion superseded, G2-G5 retained | [InventoryMutationM2Validation.md](docs/InventoryMutationM2Validation.md) |
| Supported environments, API/version policy and required validation | [Compatibility.md](docs/Compatibility.md) |
| Phase scope, planned API examples and initial configuration candidates | [First-version design](docs/CarbonLuau_FirstVersion_Design.md), especially sections 3 and 31 |
| What Phase 0 actually proved | [Phase0-Validation.md](docs/Phase0-Validation.md) |
| Phase 0 build/deployment commands | [Phase0.md](docs/Phase0.md) |
| Phase 1 execution/ABI contract and evidence | [Phase1.md](docs/Phase1.md), [Phase1-Validation.md](docs/Phase1-Validation.md) |

Read this policy, Invariants and Compatibility before implementation; then read the current phase evidence and only the design sections and source needed for the task. The original design remains the accepted phase plan, not a claim that its future features exist. Runtime invariants apply when their owning feature is implemented. Do not implement a later feature simply to satisfy its future invariant now.

The user approved the original D13 closure on 2026-09-14, deferring the Phase 4
item convenience surface from v0.1. Revised D13 preserves the historical facts
that Rust inventory is nontransactional and lacks universal rollback, but
supersedes categorical mutation deferral with a per-operation
PREPARE/COMMIT/VERIFY model. D18 now architecturally accepts bounded read-only
item observation plus implementation-gated `Player:GiveItem` and `TakeItem`.
Preserve [Phase4.md](docs/Phase4.md),
[Phase4-Validation.md](docs/Phase4-Validation.md) and the historical structural
checker as evidence. Inventory-M2 historically reported revised D13 gates
G1-G5 PASS on Rust build `25353106` plus Carbon `2.0.259`, as
recorded in [InventoryMutationM2Validation.md](docs/InventoryMutationM2Validation.md).
Player-1F-A uses that qualification for the narrow public TakeItem adapter;
Player-1F-B adds separately requalified GiveItem InventoryOnly. Broader mutation
remains unimplemented.
The Player-1F-B follow-up found an uncovered callback-to-split/drop path on the
same pinned build, superseding the incomplete G1 conclusion. I12 now owns the
general trusted in-process interference boundary; D13/D18 adopt that scope and
the intended `GiveItem(ShortName, Amount, Behavior?)` shape with InventoryOnly
default. The historical investigation and subsequent supported-host G1
requalification are recorded in [PlayerInteractionFoundation1FB.md](docs/PlayerInteractionFoundation1FB.md)
and [its validation record](docs/PlayerInteractionFoundation1FB-Validation.md).
Phase 5 hardening/qualification is complete within its recorded controlled-host
envelope in [Phase5.md](docs/Phase5.md) and
[Phase5-Validation.md](docs/Phase5-Validation.md). Authenticated real-client
qualification is explicitly deferred, non-gating and unqualified; that decision
is not evidence of network delivery or a support claim. No inventory-mutation
runtime/API addition is authorized outside separately scoped Inventory-M and
Player-1F work.
The release-candidate identity and reproducibility procedure are owned by
[release.json](release.json) and [Release.md](docs/Release.md). Public setup starts
at [Installation.md](docs/Installation.md); release preparation must not reinterpret
the first-version roadmap label as a second package version or weaken recorded
qualification limits.

If documents, code, a task request or upstream evidence conflict, identify the conflict and its rule owner. Preserve accepted decisions while investigating. Stop the affected implementation if it would require silently weakening a rule or making an unsupported architectural decision; report the evidence and proposed resolution. Do not expand documentation to manufacture certainty. An explicit user-approved design change must also update its canonical document.

## Working rules

Post-v0.3.0 addon architecture is canonically owned by the addon amendments in
[Invariants.md](docs/Invariants.md), including D14. The
[addon design proposal](docs/CarbonLuau_Addon_Decisions_and_Invariants.md) and
[design validation](docs/Addon-Design-Validation.md) are supporting design/review
evidence and do not override the canonical register. Foundation A authorizes only
the internal shared-VM/domain primitives recorded in
[FoundationA.md](docs/FoundationA.md). Foundation B's experimental managed-provider
registration, bounded package transport and lifecycle implementation is recorded in
[FoundationB.md](docs/FoundationB.md). Foundation C's bounded dependency graph,
exact lifetime binding, loss/restoration and dependency-aware replacement behavior
is recorded in [FoundationC.md](docs/FoundationC.md). Foundation D's explicit
exports, exact-bound package-qualified imports and addon context are recorded in
[FoundationD.md](docs/FoundationD.md). Foundation E's scale, fairness, lifecycle,
parser and public-identity closure is recorded in [FoundationE.md](docs/FoundationE.md).
It assigns `0.4.0-experimental` to the qualified addon surface but does not
authorize provider-defined capabilities, root imports or package/version solving.
Foundation F's behavior-preserving invariant-owner decomposition and current
implementation routing are recorded in [FoundationF.md](docs/FoundationF.md).
Foundation G's isolated pinned compiler, deadline, protocol, deployment and
qualification scope are recorded in [FoundationG.md](docs/FoundationG.md).
It does not authorize unrelated post-v0.4 features.
GUI Foundation 1 architecture is canonically owned by D15 in
[Invariants.md](docs/Invariants.md#d15--gui-foundation-1-retained-presentation-model).
[GuiFoundation1.md](docs/GuiFoundation1.md) is the supporting design and
implementation-guidance record; it does not override D15 or by itself prove an
implementation phase. Future GUI phases must preserve that public model and stop rather
than invent conflicting semantics. Foundation 1G completed implementation,
available qualification and release planning and assigns GUI to D12's existing
unreleased `0.4.0-experimental` identity. Authenticated-client observations
remain unqualified and non-gating.
The internal descriptors, limit snapshot, Carbon-independent render/backend
contracts and deterministic mock backend implemented by GUI Foundation 1A are
recorded in [GuiFoundation1A.md](docs/GuiFoundation1A.md). They do not authorize
public GUI bindings, retained-object behavior, presentations or CUI rendering.
GUI Foundation 1B's retained objects, immutable value userdata, ownership,
tree lifecycle, GUI Signals and D7/D10 publication journal are recorded in
[GuiFoundation1B.md](docs/GuiFoundation1B.md). They do not authorize
presentations, client rendering/event ingress or a GUI-capable release identity.
GUI Foundation 1C's internal presentations, public ScreenGui visibility methods,
full retained-tree compilation and production Rust CUI projection are recorded
in [GuiFoundation1C.md](docs/GuiFoundation1C.md).
GUI Foundation 1D's revisioned dirty synchronization, bounded fair GUI flush,
Rust CUI property patches, structural reconciliation and newest-state failure
convergence are recorded in [GuiFoundation1D.md](docs/GuiFoundation1D.md). It
does not by itself authorize client action ingress or a GUI-capable release
identity. GUI Foundation 1E's private client command, opaque presentation-bound
actions, exact Player validation, bounded admission and pre-entry stale-work
suppression are recorded in [GuiFoundation1E.md](docs/GuiFoundation1E.md). It
does not assign a GUI-capable release identity or authorize later GUI features.
GUI Foundation 1F's replacement, recovery, teardown, backend-retry, bounded
diagnostic and leak/stress closure is recorded in
[GuiFoundation1F.md](docs/GuiFoundation1F.md). Authenticated-client rendering,
reconciliation and click evidence plus GUI release planning were left to
Foundation 1G; Foundation 1F itself authorized no new GUI surface or identity.
GUI Foundation 1G assigns the already-unreleased package `0.4.0` and scripting
API `0.4.0-experimental` to the complete D15 GUI surface, records the public
documentation/examples and closes available non-authenticated qualification in
[GuiFoundation1G.md](docs/GuiFoundation1G.md). Authenticated-client visual,
cursor, click-receipt and reconciliation behavior remains unqualified but is
explicitly non-gating. Do not reinterpret API availability as evidence for
those client-observed outcomes or as independent authorization to implement GUI
Foundation 2 outside D16 and an explicitly scoped implementation phase.
GUI Foundation 2 architecture is canonically owned by D16 in
[Invariants.md](docs/Invariants.md#d16--gui-foundation-2-deterministic-layout-and-rich-controls).
[GuiFoundation2.md](docs/GuiFoundation2.md) is supporting rationale, exact
surface guidance, implementation sequencing and qualification planning; it does
not override D16 or prove implementation. D16 is additive to D15, assigns no
release identity and authorizes no production surface until the applicable
GUI-2A through GUI-2F work is implemented and qualified. Exact supported
single-line text preservation is a TextBox qualification gate: defer TextBox if
the current authenticated client/host path cannot preserve it, rather than
weakening or normalizing the contract.
GUI Foundation 2A implements only D16's deterministic layout slice: `LayoutOrder`,
`UIListLayout`, `UIPadding`, affine projection and bounded layout dirty
synchronization. Its implementation and qualification record is
[GuiFoundation2A.md](docs/GuiFoundation2A.md). It does not assign a Foundation 2
release identity or authorize scrolling, images, TextBox, typed input or later
GUI-2B through GUI-2F work.
GUI Foundation 2B implements only D16's typed-image slice: immutable
`ImageSource`, `ImageLabel`, `ImageButton`, bounded image projection and
`ImageButton.Activated` through the existing secure action ingress. Its
implementation and qualification record is [GuiFoundation2B.md](docs/GuiFoundation2B.md).
It assigns no Foundation 2 release identity and does not authorize later work.
GUI Foundation 2C implements only D16's scrolling slice: retained
`ScrollingFrame` configuration, Presentation-local client scroll state, bounded
private viewport/content projection and Foundation 2 layout/image composition.
Its implementation and qualification record is [GuiFoundation2C.md](docs/GuiFoundation2C.md).
It assigns no Foundation 2 release identity and does not authorize TextBox,
typed text ingress or later GUI-2D through GUI-2F work.
GUI Foundation 2E closes D15/D16 lifecycle, shared-view, cross-domain,
replacement, recovery, provider, publication and failure behavior for the
implemented GUI-2A through GUI-2C surface. Its evidence is recorded in
[GuiFoundation2E.md](docs/GuiFoundation2E.md). The GUI-2D host gate failed on
the inspected Rust build, so `TextBox` and typed text ingress remain explicitly
deferred and unimplemented. At completion, GUI-2E assigned no Foundation 2
release identity and did not itself authorize GUI-2F or Foundation 3 work.
GUI Foundation 2F assigns the implemented deterministic-layout, typed-image and
retained-scrolling subset to the existing unreleased package `0.4.0` and
scripting API `0.4.0-experimental`. Its public documentation, examples,
compatibility audit, scale evidence and release preparation are recorded in
[GuiFoundation2F.md](docs/GuiFoundation2F.md). `TextBox` and `Submitted` remain
deferred and unimplemented. Authenticated-client image, click, scrolling and
clipping observations remain explicitly unqualified and are not implied by the
experimental identity. GUI-2F authorizes no Foundation 3 work.
GUI Foundation 3 architecture is canonically owned by D17 in
[Invariants.md](docs/Invariants.md#d17---gui-foundation-3-deterministic-grids-clipping-fonts-and-presentation-scroll-intent).
[GuiFoundation3.md](docs/GuiFoundation3.md) is supporting rationale, exact
surface guidance, implementation sequencing and qualification planning; it does
not override D17, prove implementation or assign a release identity. D17 is
additive to D15/D16 and authorizes only explicitly scoped GUI-3A through GUI-3E
implementation work. `TextBox` remains deferred under D16 and is not reopened.
GUI Foundation 3A implements only D17's deterministic `UIGridLayout` slice:
retained descriptors, list-or-grid exclusivity, affine direct-child projection,
retained Position/Size authority restoration and bounded dirty synchronization.
Its implementation and qualification record is
[GuiFoundation3A.md](docs/GuiFoundation3A.md). It assigns no Foundation 3
release identity and authorizes no clipping, font, scroll-effect or GUI-3B+
work.
GUI Foundation 3B implements only D17's bounded `Frame.ClipsDescendants`
slice: private projection-only mask nodes, effective depth accounting with
ScrollingFrame, retained-geometry interaction eligibility, structural action
reconciliation and atomic projection-bound validation. Its implementation and
qualification record is [GuiFoundation3B.md](docs/GuiFoundation3B.md).
The feature is IMPLEMENTED / CLIENT-UNQUALIFIED pending D17's mandatory
authenticated-client visual and hit-region supplement. It assigns no Foundation
3 release identity and authorizes no GuiFont, ScrollTo or GUI-3C+ work.
GUI Foundation 3C implements only D17's immutable `GuiFont`, retained
`TextLabel.Font`/`TextButton.Font`, backend mapping and patch-synchronization
slice. Its implementation and qualification record is
[GuiFoundation3C.md](docs/GuiFoundation3C.md). The feature is IMPLEMENTED /
CLIENT-UNQUALIFIED pending D17's mandatory authenticated-client font-rendering
supplement. It assigns no Foundation 3 release identity and authorizes no
ScrollTo or GUI-3D+ work.
GUI Foundation 3D implements only D17's exact-Player, bounded latest-wins
`ScrollingFrame:ScrollTo`, `ScrollToTop` and `ScrollToBottom` Presentation
effects, publication specialization, backend mapping, retry and lifecycle
integration. Its implementation and qualification record is
[GuiFoundation3D.md](docs/GuiFoundation3D.md). The feature is IMPLEMENTED /
CLIENT-UNQUALIFIED pending D17's mandatory authenticated-client scroll
supplement. GUI Foundation 3E closes combined interaction, shared-view,
cross-domain, replacement, recovery, publication, scale, compatibility and
release-candidate qualification for the implemented D17 surface. Its evidence
is recorded in [GuiFoundation3E.md](docs/GuiFoundation3E.md). The additive
surface remains in the still-unreleased package `0.4.0` and scripting API
`0.4.0-experimental`; its client-observed gates and Windows native/local gate
remain explicitly unqualified. GUI-3E authorizes no Foundation 4 or TextBox work.

Player Interaction Foundation 1 architecture is canonically owned by D18 in
[Invariants.md](docs/Invariants.md#d18--player-interaction-foundation-1).
[PlayerInteractionFoundation1.md](docs/PlayerInteractionFoundation1.md) is the
supporting rationale, host-evidence, implementation-sequencing and qualification
record; it does not override D18, prove implementation or assign a release
identity. The revised D13 rationale and mutation gates are in
[InventoryOwnershipFailureReassessment.md](docs/InventoryOwnershipFailureReassessment.md).
D18 authorizes only explicitly scoped future Player-1A through Player-1F and
Inventory-M work. Player-1A is `Vector3` plus Position; Player-1B is read-only
Health/MaxHealth; Player-1C is bounded item identity and physical inventory
observation; Player-1D is committed-only Teleport with exact-host and
authenticated-client gates; Player-1E is combined read-only/spatial closure.
Inventory-M1 owns the deterministic model without Rust mutation; Inventory-M2
retains G2-G5 exact target-build evidence while G1's supported-host requalification
under I12 is recorded by Player-1F-B; Player-1F-A owns TakeItem,
Player-1F-B owns GiveItem, and Player-1F-C owns combined mutation closure.
TakeItem is implemented by Player-1F-A and GiveItem InventoryOnly by Player-1F-B; they
qualify independently.
Player-1A implements immutable `Vector3` and read-only `Player.Position` as
recorded in [PlayerInteractionFoundation1A.md](docs/PlayerInteractionFoundation1A.md).
Player-1B implements read-only `Player.Health` and `Player.MaxHealth` as recorded
in [PlayerInteractionFoundation1B.md](docs/PlayerInteractionFoundation1B.md).
Player-1C implements the `Items` existence service plus bounded physical
`Player:CountItem` and `Player:HasItem` as recorded in
[PlayerInteractionFoundation1C.md](docs/PlayerInteractionFoundation1C.md).
Player-1D implements committed-only `Player:Teleport(Vector3)` as recorded in
[PlayerInteractionFoundation1D.md](docs/PlayerInteractionFoundation1D.md).
Player-1F-A implements committed-only verified `Player:TakeItem` as recorded in
[PlayerInteractionFoundation1FA.md](docs/PlayerInteractionFoundation1FA.md).
The exact-build server adapter is qualified; authenticated-client convergence
remains unqualified. The separate Player-1F-B qualification is linked above;
neither API authorizes Player-1F-C closure or other gameplay APIs.


World/Entity Foundation 1 architecture is canonically owned by D20 in
[Invariants.md](docs/Invariants.md#d20--worldentity-foundation-1).
[WorldEntityFoundation1.md](docs/WorldEntityFoundation1.md) is the supporting
host-evidence, rationale, exact-lifetime, phase-routing and qualification record.
D20 is **HOST-PRIMITIVE-GATED / DEFERRED**: accepted future architecture, with
Entity-1A **BLOCKED** until a supported authoritative incarnation/retirement
mechanism closes the exact-lifetime proof. Preserve the negative evidence and
its correction: actual pooled BaseEntity reuse was not demonstrated on the tested
prefab. No production `Workspace` or `Entity` API may proceed while gated.
After that gate closes, Entity-1A is the internal exact-lifetime/publication
substrate; Entity-1B is the keyed read-only `Workspace:GetEntityById` plus
`Entity.Id`, `Entity.Prefab` and `Entity.Position` exact-host qualification;
Entity-1C is lifecycle/scale/public closure. Whole-world enumeration, prefab
filtering, spatial queries, lifecycle Signals, Spawn, Destroy and specialized
entity capabilities require later explicit architecture rather than being
implicitly authorized by D20. D20 changes no current package/API/ABI/provider/
schema/Luau identity.

Persistence Foundation 1 is the next runtime foundation under
[D21](docs/Invariants.md#d21--persistence-foundation-1). Its
[design](docs/PersistenceFoundation1.md) selects private root/addon namespaces,
callback-based non-yielding storage and a bounded private SQLite worker;
[design validation](docs/PersistenceFoundation1-Validation.md) is not runtime
qualification. Separately scoped Persistence-1A/1B/1C work must implement and
qualify it. The private 1A substrate is **PASS within its recorded qualification
scope** in [PersistenceFoundation1A.md](docs/PersistenceFoundation1A.md), with no
public persistence API. The user explicitly approved and adopted the
[physical-budget amendment](docs/PersistencePhysicalD21Amendment-Proposed.md) on
2026-09-23, conditional on scoped startup/profile qualification. Hard 16-MiB
namespace / 256-MiB global logical quotas and SQLite page/file-length bounds
remain; 1,280 MiB is an operational safety budget, qualification target and
diagnostic threshold, not a hard physical invariant. No breach observed is
empirical evidence, not a theorem. The WAL startup rejection fix is mandatory,
including page-1 restoration through hot-journal recovery, before unsupported
conversion or WAL/SHM creation; 1A implements and qualifies this while preserving
supported recovery. D21 is resolved,
not another architecture investigation. Preserve historical DELETE and
physical-allocation negative evidence. The later explicit authorization starts
[1B](docs/PersistenceFoundation1B.md), implemented and qualified within its recorded
scope, including [implementation-source CI](docs/PersistenceFoundation1B-Validation.md#final-implementation-source-hosted-ci).
Persistence-1C is NOT STARTED; combined closure remains separate. The experimental
development availability is not release approval.
The user assigned persistence API `0.5.0-experimental` under D12; it is not
retroactive 0.4 availability or permission to publish. Package 0.5.0 is intended
for a future release; [Release.md](docs/Release.md) records why the development
package remains 0.4.0. Original design adoption alone did not authorize these
changes. Entity work remains gated;
persistence does not weaken Entity identity or serialize Player/Entity facades.

- Identify whether the request is investigation, design, implementation, review or validation. Stay within its modification authority and current phase; keep unrelated refactors out.
- Modify only CarbonLuau unless explicitly authorized otherwise. Do not edit Carbon, Rust, Gargantuan, or casually change vendored Luau. Read upstream sources to resolve assumptions; prefer documented/public adaptation APIs.
- Use PascalCase for project-owned identifiers by default. Preserve required OS/upstream names and accepted public API spellings; `self = setmetatable(...)` is the stated local exception. Use `GetFolder`/`GetObj`, not `GetOrCreateFolder`/`GetOrCreateObj`.
- Use `[System:SubSystem]` logging, e.g. `[CarbonLuau:Native]`, with concise relevant diagnostics. Prevent task processes from showing error dialogs or stealing focus; use headless/hidden server and helper launches.
- Preserve user changes and concurrent work. Use `apply_patch` and the available CodexLock workflow; do not bypass another task's claims.
- Run native builds and test servers on `DockerPC` through the authorized Windows profile connection. Keep worker host files under `C:\Sandbox\Codex`, bound resource use, reuse caches, and leave unrelated workloads alone. Use Linux containers on that host for Linux tests. Do not silently move sustained builds or servers to the controlling PC.
- Keep credentials in their existing profile helpers, out of code/logs/artifacts. Deployment and destructive fixture tests have different scopes: production uploads never authorize replacing live libraries with failure fixtures or modifying host policy.
- Select validation through Compatibility.md. Preserve valid previous evidence; rerun affected checks after implementation changes. Report source revision, results, environment, remaining uncertainty and any persistent task processes.
- Add durable accepted rules to their canonical owner and link them from task notes. Do not duplicate them across prompts or create parallel policy documents.

## Prompt construction

A task prompt supplies the delta, not the architecture. Specify objective, task kind, permitted repository/paths, current revision/evidence, exclusions and stop conditions. Route to this policy and canonical phase criteria; use the smallest authoritative context. State prior evidence with its limits, prescribe order only where dependencies or test attribution require it, and do not optimize for a desired verdict. Unresolved choices belong in the decision register, not hidden prompt assumptions.

```text
Task: <phase and concrete outcome>; mode: <design/implementation/review/validation>.
Scope: CarbonLuau at <revision>; permitted changes: <paths/components>.
Read AICONTEXT.md and its canonical architecture, compatibility and phase references.
Established evidence: <link and applicable limits>.
Change: <task-specific delta>; exclude <later phases/unrelated work>.
Validate: <canonical fixture/criteria links plus only genuinely new requirements>.
Stop and report if <task-specific blocker> or an accepted invariant must change.
Report changes, final-source evidence, unresolved decisions and verdict.
```

For a Phase 1 prompt, route to the design's Phase 1 scope and the [Phase 1 evidence contract](docs/Compatibility.md#phase-1-evidence-contract). Do not request a complete scheduler, gameplay bindings, or the rest of v0.1 as part of the execution core.
