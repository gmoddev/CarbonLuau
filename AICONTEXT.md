# CarbonLuau AI and contributor policy

This document owns contribution workflow and prompt construction. It applies to CarbonLuau only. Phase 0's accepted source baseline is `a88f2eb`; read the current checkout and [validation record](docs/Phase0-Validation.md) before relying on that baseline. Phase 1 implementation and qualification are documented in [Phase1.md](docs/Phase1.md) and [Phase1-Validation.md](docs/Phase1-Validation.md). Phase 2 is documented in [Phase2.md](docs/Phase2.md), [Phase2-Validation.md](docs/Phase2-Validation.md) and invariant D9. The authorized Phase 3 facade, approved provisional-effect policy and qualification status are in [Phase3.md](docs/Phase3.md), [Phase3-Validation.md](docs/Phase3-Validation.md) and D10–D12. Script authors start at the [public API reference](docs/api/README.md). Phase 4 is deferred from v0.1 by D13.

## Authority and reading order

| Question | Canonical owner |
|---|---|
| How to scope work and construct prompts | This document |
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
| Supported environments, API/version policy and required validation | [Compatibility.md](docs/Compatibility.md) |
| Phase scope, planned API examples and initial configuration candidates | [First-version design](docs/CarbonLuau_FirstVersion_Design.md), especially sections 3 and 31 |
| What Phase 0 actually proved | [Phase0-Validation.md](docs/Phase0-Validation.md) |
| Phase 0 build/deployment commands | [Phase0.md](docs/Phase0.md) |
| Phase 1 execution/ABI contract and evidence | [Phase1.md](docs/Phase1.md), [Phase1-Validation.md](docs/Phase1-Validation.md) |

Read this policy, Invariants and Compatibility before implementation; then read the current phase evidence and only the design sections and source needed for the task. The original design remains the accepted phase plan, not a claim that its future features exist. Runtime invariants apply when their owning feature is implemented. Do not implement a later feature simply to satisfy its future invariant now.

The user approved D13 closure on 2026-09-14: the entire Phase 4 item convenience
surface (Items, Items:Exists and Player:GiveItem) is deferred from v0.1. The
[canonical roadmap](docs/CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases)
reflects that scope change. Preserve [Phase4.md](docs/Phase4.md),
[Phase4-Validation.md](docs/Phase4-Validation.md) and the read-only structural
checker as historical evidence; deferral is not proof of a safe adapter.
Phase 5 hardening/qualification is complete within its recorded controlled-host
envelope in [Phase5.md](docs/Phase5.md) and
[Phase5-Validation.md](docs/Phase5-Validation.md). Authenticated real-client
qualification is explicitly deferred, non-gating and unqualified; that decision
is not evidence of network delivery or a support claim. No Phase 4 runtime/API
additions or weaker ownership rules are authorized.
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
deferred and unimplemented. GUI-2E assigns no Foundation 2 release identity and
does not authorize GUI-2F or Foundation 3 work.

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
