# GUI Foundation 1A: internal substrate

Verdict: **PASS** after final-source validation.

Starting commit: `971c045d735e8c9f4020c1834b0f36218a6ed58f`.

Foundation 1A implements only the internal, Carbon-type-free substrate routed by
[D15](Invariants.md#d15--gui-foundation-1-retained-presentation-model). It does
not install `game:GetService("Gui")`, expose Luau userdata or value constructors,
create presentations, render CUI, synchronize dirty state, admit GUI actions or
implement publication-journal behavior.

## Subsystem ownership

The managed `Gui/` component owns:

| Component | Responsibility |
|---|---|
| `GuiConfig.cs` | Internal GUI safety/default candidates and validated immutable limit snapshot |
| `GuiDescriptors.cs` | Explicit class, property, method, event and value-type IDs/descriptors |
| `GuiModelContracts.cs` | Opaque object identity, owner/publication scope and retained-registry boundary |
| `GuiRenderPlan.cs` | Carbon-independent target, element, value, full-plan and patch contracts |
| `IGuiBackend.cs` | Full replacement, patch and destruction submission with controlled results |
| `InMemoryGuiBackend.cs` | Deterministic state, attempt ordering and send-failure injection for tests |
| `GuiHostCapabilities.cs` | Exact source-inspection identity and supported adapter assumptions |

No native change was required. The canonical retained model is not a Carbon LUI
container, Rust CUI object or serialized JSON document.

## Descriptor model

The schema uses explicit numeric IDs and fixed descriptor arrays. It performs no
reflection-based member discovery. Internal nonconstructible `GuiNode` and
`GuiObject` descriptors support inheritance and `IsA`; the exact public class
set remains `ScreenGui`, `Frame`, `TextLabel` and `TextButton`.

Descriptors cover D15's `Name`, `ClassName`, `Parent`, layout, visibility,
background, Z, text, screen-lifecycle and common object methods plus
`TextButton.Activated`. Class-specific writability and defaults distinguish the
read-only `ScreenGui.Parent` from writable `GuiObject.Parent`. `UDim`, `UDim2`,
`Vector2` and `Color3` have explicit immutable-value field/range metadata. These
are internal schemas, not working Luau types.

## Backend and render-plan contracts

`IGuiBackend` has three operations:

```text
Replace(Target, FullPlan)
Update(Target, Patch)
Destroy(Target)
```

Results distinguish local acceptance, unavailable target and send failure. They
do not claim client acknowledgement. Requests contain only CarbonLuau render
primitives: stable bounded client IDs, parent IDs, node kinds and typed render
properties. Full plans enforce unique IDs, one root and parent-before-child
ordering. Properties canonicalize by explicit ID, strings/numbers have invariant
representations, and full/patch requests enforce configured element/property and
estimated-byte bounds.

The in-memory backend records every attempt with a monotonic sequence, maintains
accepted full/patch state, treats destruction as idempotent, rejects update of a
missing target, and injects deterministic per-operation send failures without
mutating accepted state. It is the test boundary for later render golden,
ordering, reconciliation and fault tests; it is not a production renderer.

## Limits and configuration

`GuiConfig.Validate()` produces an immutable `GuiLimits` snapshot and rejects
nonpositive or contradictory scope relationships. It records the review's
object/tree/domain/global, viewer/presentation, Signal/token, string/clone and
request bounds as internal initial defaults.

The 64 KiB operation size, 64 sends and 256 KiB per flush, 1 ms flush budget,
32-patch reconciliation interval and interaction rates remain configurable
qualification/tuning candidates. No runtime currently consumes them, and this
phase does not promote them to public compatibility guarantees.

## Host capability inspection

The compatibility target remains Carbon `2.0.259.0`, Rust protocol
`2633.288.1` and Steam build `25230300`.
Foundation 1A also recorded the exact public source revisions inspected on
2026-09-19:

- Carbon LUI `4d1b081eadef99da774e0342899bddcd638e26d2`: update containers,
  AddUI RPC sending, RectTransform parent/index updates, cursor and button command
  components. [Pinned source](https://github.com/CarbonCommunity/Carbon/blob/4d1b081eadef99da774e0342899bddcd638e26d2/src/Carbon.Components/Carbon.Common/src/Carbon/Components/LUI.cs)
- Facepunch Rust.Community `c1aba1600e8cf3dc7087bed99fdc7f8aa174af13`:
  ordered element processing, `destroyUi` before create, `update` target lookup,
  anchors/offsets, update-only parent/index changes, `NeedsCursor` and button
  `ClientRunOnServer`. [Pinned source](https://github.com/Facepunch/Rust.Community/blob/c1aba1600e8cf3dc7087bed99fdc7f8aa174af13/CommunityEntity.UI.cs)
- Oxide Rust adapter `ca69c156382acfbb62804de0710d9555d5a206de`:
  AddUI/DestroyUI helper success means a connected local RPC send was issued, not
  that a client applied or acknowledged the tree. [Pinned source](https://github.com/OxideMod/Oxide.Rust/blob/ca69c156382acfbb62804de0710d9555d5a206de/src/RustCui.cs)

These findings support the adapter design but do not qualify a production
`RustCuiBackend`, the exact target binaries or authenticated-client rendering.
Those remain later live qualification gates. Host source observations are not
new canonical invariants.

## Validation

`GuiFoundation1ATests` covers descriptor completeness/uniqueness and class
relationships, exact public class/value/event boundaries, config validation,
deterministic plan representation, parent/order and request bounds, mock
replace/update/destroy, missing targets, failure injection and documentation
identity consistency. `Test-Architecture.ps1` owns the new implementation paths
and rejects managed reflection APIs in the GUI component.

Implementation commit `d2bb1527d749ea4fe1b4de8cc26aa472a3bb6b69`
passed the following final-source checks:

- Release managed build with zero warnings and zero errors;
- focused `--gui-only` descriptor, limit, plan, mock-backend and capability tests;
- architecture, API and deterministic release/package checks; and
- `git diff --check`.

Hosted [validation run 35430231936](https://github.com/gmoddev/CarbonLuau/actions/runs/35430231936)
passed the Windows and Ubuntu native/runtime matrices, packaging, and the
ASan/UBSan/leak job. The Windows matrix needed two failed-job retries because
the pre-existing Foundation G compiler-containment memory-limit test failed on
the first two attempts; the unchanged revision passed that test and the full
Windows lane on attempt 3. Hosted
[documentation run 35430231906](https://github.com/gmoddev/CarbonLuau/actions/runs/35430231906)
also passed.

## Deferred work

[GUI Foundation 1B](GuiFoundation1B.md) now owns the retained registry, public
userdata/value constructors, `Create`/parenting/clone/destroy behavior, GUI
Signal presence and the D15 publication journal. GUI-1C and later still own
presentations, Show/Hide, real Rust CUI rendering, dirty synchronization, action
tokens and `Activated` ingress/delivery. No package, scripting API, native ABI,
provider protocol, package schema or Luau revision changed.
