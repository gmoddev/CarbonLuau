# CarbonLuau contributor policy

This is the contributor entry point for CarbonLuau. It owns workflow and task
scope, not a second runtime/API contract. Start here, then read the relevant
canonical rules and qualification records in the [documentation index](docs/Index.md).

## Authority and reading order

1. Read this policy and [Invariants](docs/Invariants.md): I1–I12 and the decision
   register apply to all work. Read the delegated canonical detail for the
   affected runtime, Player, GUI, World/Entity, persistence, tooling or gameplay
   boundary. Delegated pages are part of the register, not competing proposals.
2. Read [Compatibility](docs/Compatibility.md) and its delegated
   [qualification policy](docs/qualification/Policy.md). Select checks by the
   changed boundary; do not infer live-host evidence from compile/model tests.
3. Read the affected current contract and source-qualified phase receipts below.
   API authors start at the [public reference](docs/api/README.md).
4. Consult [history](docs/history/README.md) only for the relevant rationale,
   rejected approach or adoption record. A historical BLOCKED verdict is not a
   new gate; a historical PASS is not proof of changed source or a different host.

If a request, document, implementation or upstream fact conflicts, identify the
rule owner and preserve accepted semantics while investigating. Stop the affected
change if it requires an unauthorized amendment. Do not manufacture certainty by
restating a proposal as a rule or broadening a receipt's tested scope.

## Current routing

| Boundary | Canonical contract / implementation and evidence |
|---|---|
| Runtime ownership, deadlines, modules, publication, recovery and Player identity | [Runtime details](docs/invariants/Runtime.md); [Phase 0](docs/Phase0.md), [Phase 1](docs/Phase1.md), [Phase 2](docs/Phase2.md), [Phase 3](docs/Phase3.md), [Phase 5](docs/Phase5.md), with their dated validation records |
| Addons/providers, dependencies, exports, fairness, invariant owners and isolated compiler | D4/D5/D7/D9/D10/D14 in [Runtime](docs/invariants/Runtime.md); [A–G ledger](docs/qualification/Addons.md) |
| GUI retained state, projection, layout, images, scrolling, grids, clipping and fonts | [D15–D17](docs/invariants/Gui.md); [GUI 1](docs/GuiFoundation1.md), [2](docs/GuiFoundation2.md), [3](docs/GuiFoundation3.md); [1 ledger](docs/qualification/Gui1.md), [2 ledger](docs/qualification/Gui2.md), [3 ledger](docs/qualification/Gui3.md) |
| Player spatial/health/item observation and inventory mutation | D11/D13 in [Runtime](docs/invariants/Runtime.md), [D18](docs/invariants/Player.md); [current Player foundation](docs/PlayerInteractionFoundation1.md), [phase ledger](docs/qualification/PlayerInteraction.md), [retained M2 scope](docs/InventoryMutationM2Validation.md) |
| Current PlayerInventory acquisition, sampled ItemChanged and breaking migration | [C1](docs/GameplayC1-Validation.md), [C2 evidence](docs/GameplayC2-Validation.md), [C3](docs/GameplayC3-Validation.md), [C4](docs/GameplayC4-Validation.md); [migration](docs/api/Inventory-Migration.md) |
| Exact Entity lifetime, startup observer and read-only lookup | [D20](docs/invariants/WorldEntity.md); [Foundation 1](docs/WorldEntityFoundation1.md), [1A](docs/WorldEntityFoundation1A-Validation.md), [1B](docs/WorldEntityFoundation1B-Validation.md), [1C](docs/WorldEntityFoundation1C.md) |
| Completed-Spawn catalog and bounded asynchronous complete discovery | [Foundation 2](docs/WorldEntityFoundation2.md), [private 2A](docs/WorldEntityFoundation2A.md), [public 2B](docs/WorldEntityFoundation2B.md), [combined 2C](docs/WorldEntityFoundation2C.md) |
| Local persistence, durability, codec, quotas, callback authority and capacity | [D21](docs/invariants/Persistence.md#d21--persistence-foundation-1), [Foundation 1](docs/PersistenceFoundation1.md), [1A](docs/PersistenceFoundation1A.md), [1B ledger](docs/qualification/Persistence1B.md), [1C](docs/PersistenceFoundation1C.md) |
| Structured Query, automatic derived indexes, optional hints and bounded cursors | [D22](docs/invariants/Persistence.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query), [Foundation 2](docs/PersistenceFoundation2.md), [2A](docs/PersistenceFoundation2A.md), [2B](docs/PersistenceFoundation2B.md), [2C](docs/PersistenceFoundation2C.md), [2D](docs/PersistenceFoundation2D.md) |
| Official editor tooling, canonical metadata, trusted analysis and preview | [D19](docs/invariants/Tooling.md), [baseline](docs/ToolingBaseline.md), [contracts](docs/ToolingContracts.md), [analysis security](docs/ToolingLanguageAnalysisSecurity.md), [A completion](docs/ToolingFoundationACompletion.md), [B completion](docs/ToolingFoundationBCompletion.md), [deferred roadmap](docs/design/CarbonLuau_VSCode_Tooling_Architecture.md) |
| Player and Entity lifecycle Signals | [D23](docs/invariants/Gameplay.md) and [D20](docs/invariants/WorldEntity.md); [A](docs/GameplayFoundationA-Validation.md), [B1](docs/GameplayB1-Validation.md), [B2](docs/GameplayB2-Validation.md), [B3](docs/GameplayB3-Validation.md) |
| Declarative Loot Replace | [D23](docs/invariants/Gameplay.md); [private D2A](docs/GameplayD2A-Validation.md), [public D3](docs/GameplayD3-Validation.md) |
| Package/API/ABI/provider/schema/Luau identities and publication | [D12](docs/invariants/Runtime.md#d12--scripting-and-protocol-identity), [release.json](release.json), [Release](docs/Release.md), [Installation](docs/Installation.md) |

The current package/API mapping is `0.7.0` / `0.7.0-experimental`.
[Release qualification](docs/Release-0.7.0-Validation.md) owns the final-source
gates for annotated `v0.7.0` prerelease publication. C4 removed Player HasItem/CountItem/GiveItem/TakeItem without aliases;
use PlayerInventory. Earlier introduction versions and source receipts remain
historical facts, not permission to restore removed bindings. API assignment,
subsystem readiness and package/tag publication are separate decisions.

## Working rules

- Scope work as investigation, design, implementation, review or validation.
  A phase receipt or planned feature does not authorize another phase. Keep
  unrelated refactors and deferred APIs out of the change.
- Modify CarbonLuau only unless explicitly authorized otherwise. Do not edit
  Carbon, Rust, Gargantuan or vendored Luau incidentally. Inspect exact host/source
  boundaries and requalify affected adapters when host or patch topology changes.
- Preserve user changes and other worktrees. Use `apply_patch` and CodexLock;
  do not bypass claims, reset unrelated work or delete user-owned qualification data.
- Use PascalCase for project-owned identifiers. Preserve required upstream/OS/API
  spellings and the stated `self = setmetatable(...)` exception. Prefer GetFolder /
  GetObj over GetOrCreate names. Use `[System:SubSystem]` logs, for example
  `[CarbonLuau:Native]`, without sensitive values or unbounded histories.
- Use headless/hidden helpers and servers: no error dialogs or focus stealing.
  Default sustained builds/servers to DockerPC/dockerbox through the authorized
  profile, with bounded parallelism, cached dependencies and task-owned files
  under `C:\Sandbox\Codex` (or the explicitly approved task override). Linux
  workers/containers follow their authorized infrastructure policy; do not
  silently move heavy workloads onto the controlling PC or alter host security.
- Keep credentials in existing profile helpers, never source/logs/artifacts.
  Production deployment is not permission to install failure fixtures or modify
  unrelated services, files, ports or host policy.
- Treat published support, authenticated-client observations, live Carbon,
  models, native/managed tests and CI as distinct evidence. Preserve negative
  findings and unchanged valid receipts; rerun affected checks on final source.
  Do not claim Shockbyte full-runtime, macOS server, power-loss or client behavior
  beyond the explicitly recorded qualification envelope.
- Keep architecture/ownership/publication/resource rules at their canonical
  owner. New accepted rules update that owner and routing; they do not become
  duplicate prompt policy. No docs-only API or hand-maintained type-definition
  authority: metadata follows production bindings and the canonical generator.
- Keep D21 logical quotas, hard file extents and operational allocated-block
  budgets distinct. Preserve DELETE/EXTRA, allocation-proof, WAL and abandoned
  layout negatives; no logical-quota promise of guaranteed physical admission.
- Preserve D20 initial-startup observer continuity, exact lifetime/non-retargeting
  and qualified bounded reads. Failed Spawn/current flags are not lifetime proof.
  Discovery is encounter-time, not an atomic snapshot; trusted observation races
  never authorize memory unsafety, unsupported structural mutation or unbounded work.
- Preserve sampled ItemChanged semantics, exact-connection inventory ownership,
  D13 PREPARE/COMMIT/VERIFY and strict destruction-time Loot payout scope. Source
  observation is not transaction completion, rollback, replay or attribution by ID.

## Task handoff

Provide the objective/task class, current source/evidence, allowed paths and
change, exclusions, routed contract, genuinely new checks and stop conditions.
Report final source, qualification scope, remaining uncertainty and persistent
task processes. A handoff supplies the delta; do not duplicate the architecture.

```text
Task: <phase and outcome>; mode: <design/implementation/review/validation>.
Scope: CarbonLuau at <source>; permitted changes: <paths/components>.
Read AICONTEXT, canonical rules, compatibility and relevant phase evidence.
Change: <specific delta>; exclude <later phases and unrelated work>.
Validate: <routed criteria plus new requirements>; stop on <contract conflict>.
Report: final source, results/environment, uncertainty and repository state.
```
