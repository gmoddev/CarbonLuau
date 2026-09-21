# GUI Foundation 3A: deterministic grid layout

Starting commit: `1ede1fb2ea5557f9de4ee634748106461569c587`.

GUI Foundation 3A implements only D17's deterministic `UIGridLayout` slice.
It adds the retained grid helper, explicit row/column topology, affine
server-side projection, list-or-grid exclusivity and bounded grid dirty
synchronization. It does not implement `ClipsDescendants`, `GuiFont`,
`ScrollTo*`, TextBox or any GUI-3B or later behavior.

## Public retained model

`UIGridLayout` is a public, owner-bound, non-rendering `GuiNode` leaf. It can
only be parented to a GuiObject, cannot own children and participates in the
ordinary Clone, Destroy, reparenting, lifetime and publication rules.

| Property | Type | Default | Validation |
|---|---|---|---|
| `CellSize` | UDim2 | `UDim2.fromOffset(100, 100)` | Every scale and offset component is nonnegative and within the ordinary UDim2 maximum. |
| `CellPadding` | UDim2 | `UDim2.fromOffset(0, 0)` | Every scale and offset component is nonnegative and within the ordinary UDim2 maximum. |
| `FillDirection` | string | `"Horizontal"` | `"Horizontal"` or `"Vertical"`. |
| `FillDirectionMaxCells` | integer | `1` | `1..64`. |
| `HorizontalAlignment` | string | `"Left"` | `"Left"`, `"Center"` or `"Right"`. |
| `VerticalAlignment` | string | `"Top"` | `"Top"`, `"Center"` or `"Bottom"`. |

A parent may own one `UIListLayout` or one `UIGridLayout`, never both. One
`UIPadding` may coexist with either. Creation and reparenting validate the
destination before changing the retained tree, including provisional and clone
paths, so a conflict fails atomically.

## Projection compiler

The compiler resolves optional `UIPadding`, then derives cell size and cell gap
as affine functions of that padded content rectangle. Horizontal fill advances
columns first and wraps after `FillDirectionMaxCells`; vertical fill advances
rows first and wraps after the same explicit count. Topology never depends on a
client viewport, available pixels, text measurement or automatic fit.

Direct visible GuiObject children are sorted by `LayoutOrder`, retained
attachment ordinal and object identity. `ZIndex` remains independent and
`GetChildren()` remains attachment ordered. Hidden children consume no cell or
gap. Alignment places the complete algebraic grid in the padded content area;
oversized grids remain oversized rather than changing cell size or topology.
Nested grids operate independently on their own direct children, and a grid in
a `ScrollingFrame` uses its explicit retained canvas content rectangle.

While a child is grid managed, the grid cell determines both its projected
position and projected size. The authored `Position` and `Size` remain
synchronously readable and writable retained values but cause no projection
work. `AnchorPoint` only encodes the computed cell rectangle. Removing the
grid, moving the child away or changing to list layout restores the latest
retained values under the pre-existing projection rules. Projection never
rewrites them.

`UIGridLayout` emits no render element, empty cell, Unity layout component or
other host-authoritative geometry. The ordinary backend-neutral RectTransform
plan remains the sole grid output.

## Synchronization and publication

Grid creation, destruction and reparenting are structural. Grid property
changes, `LayoutOrder` or `Visible` beneath a grid, and grid-managed
`AnchorPoint` changes synthesize geometry dirtiness only for the relevant
direct GuiObject children. Grid-managed `Position` and `Size` changes are
retained-only. Multiple changes coalesce to the newest retained state; there is
no historical geometry queue.

The existing 64 arranged-child bound caps one recomputation. Synthesized dirty
detail continues to use the existing per-domain envelope, and overflow requests
the established authoritative whole-presentation reconciliation. The existing
257 projected-element screen limit is unchanged.

Every grid object, property and tree mutation uses D15's existing publication
journal. Provisional reads observe staged state, no presentation work is
visible before commit, rollback restores the prior retained and dirty state,
and commit publishes one newest-state transition. Resource ownership, foreign
domain mutation and stale-lifetime checks are unchanged.

## Focused qualification

The focused managed/native suite covers schema defaults, property types and
ranges; horizontal and vertical topology; max-cell values 1, middle and 64;
every alignment combination; zero children; mixed scale/offset cell geometry;
four-sided `UIPadding`; oversized grids; hidden children; duplicate
`LayoutOrder`; `ZIndex` and attachment-order independence; retained
Position/Size reads, writes and restoration; AnchorPoint; list/grid conflicts;
nested grids; ScrollingFrame composition; identical geometry for independent
viewers; Clone, Destroy and reparent; publication commit and rollback; backend
failure recovery; dirty overflow; replacement and teardown; and the 64-child
boundary.

The suite records full compilation and direct-child recomputation measurements
for 1, 10, 32 and 64 children plus nested grids. These measurements demonstrate
bounded scaling in the tested worker and are not compatibility or timing
guarantees. Final Windows, Linux, sanitizer, full regression, architecture,
API, packaging and hosted CI results belong in the implementation completion
report for the tested commit.

The available Linux worker run recorded:

| Children | Full ticks | Patch ticks | Synthesized dirty | Projected elements | Plan bytes |
|---:|---:|---:|---:|---:|---:|
| 1 | 254 | 116 | 1 | 3 | 625 |
| 10 | 1,467 | 652 | 10 | 12 | 2,608 |
| 32 | 4,310 | 3,274 | 32 | 34 | 7,509 |
| 64 | 8,444 | 6,037 | 64 | 66 | 14,641 |

The same Linux run passed the complete native runtime suite, the public grid
userdata/example path, addon/provider/package/parser and Foundations A through G
regressions, deterministic release packaging, and all five ASan/UBSan/leak
CTest targets. Local Windows managed model, architecture, API/link and package
checks also pass.

Windows native/local GUI-3A qualification is **DEFERRED / UNQUALIFIED** because
the required DockerPC infrastructure is unavailable. The user approved this
GUI-3A-specific deferral as non-gating. Hosted Windows CI is recorded separately
and does not substitute for Windows native/local qualification. This deferral
does not rewrite or invalidate historical Windows evidence for earlier
foundations, and it is distinct from Foundation G's own Windows live/local
deferral.

Any future supplemental Windows-native GUI-3A qualification must test exact
source revision `5f87ba0ff79326f2a6d24c05030e4918eb57e836` or explicitly requalify a
documented descendant after confirming its production GUI-3A source is
unchanged.

## Identity and remaining scope

Foundation 3A does not assign or bump a package version, scripting API version,
native ABI, provider protocol, package schema or pinned Luau revision. The grid
surface is implemented in source but is not part of the already qualified
`0.4.0-experimental` public identity until later Foundation 3 qualification and
release planning explicitly include it.

Authenticated-client visual grid behavior is unqualified and non-gating for
this server-computed slice. GUI-3B clipping, GUI-3C fonts, GUI-3D scroll effects
and GUI-3E lifecycle/public closure were not started. TextBox remains deferred
under D16's exact-text transport gate.
