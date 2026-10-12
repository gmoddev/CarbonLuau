# Documentation index: current, qualification and historical

This index separates **what CarbonLuau currently promises**, **the evidence for
those promises**, and **how earlier proposals failed or changed**. These roles
are not interchangeable. A compatibility landing page preserves an old URL; it
does not create another contract or qualification receipt.

## Current author and operator documentation

- [Home](Home.md), [Installation](Installation.md), [public API](api/README.md).
- [Scripting compatibility/limits](api/Compatibility.md) and
  [host support/validation selection](Compatibility.md).
- [Release identities and readiness](Release.md). Published package remains
  `v0.5.0`; current development API is `0.7.0-experimental`.
- Published release notes: [0.3](releases/0.3.0.md), [0.4](releases/0.4.0.md),
  [0.5](releases/0.5.0.md). [Planned 0.6 notes](releases/0.6.0.md) remain explicitly
  unpublished scope/number planning; package identity need not match API identity.
- [0.7 release candidate](releases/0.7.0.md): complete current API, final
  qualification and publication pending.

Public service/type/guide URLs under `api/` remain stable, including offline
package contents. Their documentation follows production bindings and canonical
generated metadata; Markdown is not the API generator's source of truth.

## Current canonical rules and detailed contracts

The [invariant register](Invariants.md) owns I1–I12 and the decision register.
Its delegated detail remains canonical, not a competing proposal:

| Boundary | Rules and current supporting contract |
|---|---|
| Runtime, modules, publication, recovery, Player identity, addons | [Runtime detail](invariants/Runtime.md) |
| Player/inventory | [D18](invariants/Player.md), [Player foundation](PlayerInteractionFoundation1.md), [inventory failure rationale](InventoryOwnershipFailureReassessment.md), [C4 migration](api/Inventory-Migration.md) |
| World/Entity and discovery | [D20](invariants/WorldEntity.md), [Foundation 1](WorldEntityFoundation1.md), [Foundation 2](WorldEntityFoundation2.md), [observation boundary](WorldEntityObservationBoundaryInvestigation.md) |
| Persistence | [D21/D22](invariants/Persistence.md), [Foundation 1](PersistenceFoundation1.md), [Foundation 2](PersistenceFoundation2.md) |
| GUI | [D15–D17](invariants/Gui.md), [GUI 1](GuiFoundation1.md), [GUI 2](GuiFoundation2.md), [GUI 3](GuiFoundation3.md) |
| Tooling | [D19](invariants/Tooling.md), [baseline](ToolingBaseline.md), [contracts](ToolingContracts.md), [analysis security](ToolingLanguageAnalysisSecurity.md), [deferred roadmap](design/CarbonLuau_VSCode_Tooling_Architecture.md) |
| Gameplay Signals/Loot | [D20](invariants/WorldEntity.md), [D23](invariants/Gameplay.md), current public references and scoped receipts |

An accepted deferred design is still useful current planning; it is not an
implemented API. In particular, TextBox, world mutation, additional Loot modes,
generic hooks and later tooling features are not authorized by this index.

## Current qualification basis

Use the [qualification index](qualification/README.md). Completed phases remain
an accepted qualification basis where their implementation paths are unchanged.
Every record retains its own source revision, exact host, tested scope, negative
findings and unqualified dimensions. The latest record does not automatically
requalify every earlier environment or an authenticated client.

The [qualification policy](qualification/Policy.md) is the delegated evidence
contract from Compatibility; it is criteria, not a PASS receipt.

## Historical records

Use the [history index](history/README.md) for original proposals, failed proofs,
policy adoption, earlier stopped attempts and history/privacy mappings.
Those findings are retained because they prevent repeated research and unsafe
assumptions. Historical means superseded scope/status, not expendable evidence.

Mixed records keep current routing/closure at stable paths and place their
original bodies or earlier attempts in history. The documentation coverage
manifest records source blobs and retained spans; it does not alter tested SHAs.
Only the two superseded, unreferenced interruption checkpoints were deleted.

## Documentation maintenance

[Consolidation receipt](Documentation-Consolidation.md) records the approved
cleanup, coverage/URL/test consumers and qualification. Do not introduce duplicate
authority, erase negative results, rewrite old publication identities, or use
file-count reduction as a substitute for readable current documentation.
