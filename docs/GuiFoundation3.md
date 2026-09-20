# GUI Foundation 3 architecture

Status: **READY FOR IMPLEMENTATION DESIGN / CANONICALLY ADOPTED**.

Baseline reviewed: `7999340d4a8918e0bfa2eb05a1f97310978c8f76`, after GUI
Foundation 2F.

[D17](Invariants.md#d17---gui-foundation-3-deterministic-grids-clipping-fonts-and-presentation-scroll-intent)
is the canonical policy owner. This document preserves the complete supporting
rationale, public-surface matrices, implementation guidance and qualification
plan. It does not override D15/D16, prove implementation, expose a public
Foundation 3 API or assign a version.

## Final scope

Foundation 3 contains exactly four additive areas:

| Area | Accepted Foundation 3 surface |
|---|---|
| Layout | deterministic server-computed `UIGridLayout` |
| Clipping | rectangular `Frame.ClipsDescendants` |
| Typography | immutable `GuiFont`, `TextLabel.Font`, `TextButton.Font` |
| Scrolling | per-Presentation `ScrollTo`, `ScrollToTop`, `ScrollToBottom` effects |

The central decision is to deepen the existing deterministic retained model.
Foundation 3 does not introduce client-driven layout, retained scroll position,
host paths, or another ownership/publication model.

No other Foundation 3 public feature is approved by D17.

## Host capability rationale

The pinned Rust CUI surface includes Unity grid layout groups, content fitters,
masks, outlines, font selection, image types and writable normalized ScrollView
positions. Carbon also exposes related Lightweight UI operations. These are
feasibility evidence, not the CarbonLuau public abstraction.

CarbonLuau must continue to own public semantics:

- Grid geometry is compiled from retained affine values by CarbonLuau rather
  than delegated to Unity `GridLayoutGroup`.
- A private host mask may implement clipping because clipping is projection,
  not authoritative retained layout.
- Font selection uses a project-owned immutable set instead of paths or host
  strings.
- Normalized scrolling is a write-only Presentation effect because the host
  provides no authoritative client scroll readback.

The current input field still submits text through command transport. No new
evidence resolves D16's exact-text preservation failure, so `TextBox` remains
deferred.

Supporting host references:

- [Rust CUI structures](https://github.com/OxideMod/Oxide.Rust/blob/develop/src/RustCui.cs)
- [Carbon Lightweight UI](https://carbonmod.gg/devs/features/lightweight-ui)
- [Unity Mask](https://docs.unity3d.com/2017.4/Documentation/ScriptReference/UI.Mask.html)
- [Roblox UI position and size](https://github.com/Roblox/creator-docs/blob/main/content/en-us/ui/position-and-size.md)

Host APIs and asset names may change. D17 owns the CarbonLuau contract; adapters
must absorb host differences where feasible.

## Exact accepted public surface

### UIGridLayout

```lua
local Grid = Frame:Create("UIGridLayout")

Grid.CellSize = UDim2.fromOffset(120, 40)
Grid.CellPadding = UDim2.fromOffset(8, 8)
Grid.FillDirection = "Horizontal"
Grid.FillDirectionMaxCells = 4
Grid.HorizontalAlignment = "Center"
Grid.VerticalAlignment = "Top"
```

| Member | Type | Default | Accepted values |
|---|---|---|---|
| `CellSize` | `UDim2` | `UDim2.fromOffset(100, 100)` | non-negative scale and offset components |
| `CellPadding` | `UDim2` | `UDim2.fromOffset(0, 0)` | non-negative scale and offset components |
| `FillDirection` | string | `"Horizontal"` | `"Horizontal"`, `"Vertical"` |
| `FillDirectionMaxCells` | integer | `1` | `1..64` |
| `HorizontalAlignment` | string | `"Left"` | `"Left"`, `"Center"`, `"Right"` |
| `VerticalAlignment` | string | `"Top"` | `"Top"`, `"Center"`, `"Bottom"` |

`UIGridLayout` is retained, domain-owned and non-rendering. It participates in
the existing retained object lifecycle but has no independent host element.

### Frame.ClipsDescendants

```lua
Frame.ClipsDescendants = true
```

The property is boolean, defaults to `false`, and is exposed on `Frame` only.
`ScrollingFrame` retains its inherent private viewport clip.

### GuiFont

The exact immutable values are:

```lua
GuiFont.RobotoCondensedRegular
GuiFont.RobotoCondensedBold
GuiFont.DroidSansMono
GuiFont.PermanentMarker
```

There is no constructor, string conversion, path conversion or filesystem
surface.

`TextLabel` and `TextButton` add:

```lua
Label.Font = GuiFont.RobotoCondensedBold
```

The default is `GuiFont.RobotoCondensedRegular`.

### ScrollingFrame methods

```lua
Scroll:ScrollTo(Player, Vector2.new(0, 0.5))
Scroll:ScrollToTop(Player)
Scroll:ScrollToBottom(Player)
```

`ScrollTo` uses normalized CarbonLuau coordinates:

```text
X = 0 left, 1 right
Y = 0 top,  1 bottom
```

It is not `CanvasPosition`. Foundation 3 adds no event.

## UIGridLayout semantics

### Deterministic ordering

Visible direct `GuiObject` children are arranged by:

```text
LayoutOrder ascending
then retained attachment ordinal
then object identity
```

This is the same deterministic ordering basis as `UIListLayout`. `ZIndex`
remains independent, and `GetChildren()` remains retained attachment order.

### Layout-manager exclusivity

A parent may contain at most one active layout manager:

```text
one UIListLayout OR one UIGridLayout
```

`UIPadding` may coexist with either. Creating or reparenting a conflicting
layout manager fails atomically.

### Explicit topology

For `N` visible children and `M = FillDirectionMaxCells`:

Horizontal fill:

```text
columns = min(N, M)
rows    = ceil(N / M)
```

Vertical fill:

```text
rows    = min(N, M)
columns = ceil(N / M)
```

No result depends on how many cells fit on a client. There is no automatic
fit-to-parent count, pixel-dependent wrapping, `StartCorner`, `SortOrder`,
`AbsoluteContentSize` or automatic cell sizing.

### Position and Size authority

While a direct child is governed by `UIGridLayout`:

- retained `Position` remains synchronously readable and writable;
- retained `Size` remains synchronously readable and writable;
- neither controls the projected outer rectangle;
- the grid cell controls projected position and projected size;
- grid projection never rewrites either retained value.

Removing the grid or moving the child out of it immediately restores the latest
retained `Position` and `Size` as projection authority. This intentionally
differs from `UIListLayout`, under which retained child `Size` remains
authoritative.

`AnchorPoint` remains retained. It participates only in encoding the computed
cell rectangle into backend coordinates and must not change the cell bounds.

### Hidden children, padding and alignment

`Visible = false` consumes no cell, gap, row or column position. Following
children close the gap.

`UIPadding` first establishes the available content rectangle. `CellSize` and
`CellPadding` are interpreted relative to that padded rectangle. The complete
occupied grid rectangle is then aligned within it.

Alignment remains algebraic even when content overflows. Centered oversized
content can overflow both sides without introducing a client-dependent fit
branch.

### Overflow and nesting

Grid overflow does not:

- clip automatically;
- alter row or column topology;
- shrink cells;
- wrap according to available pixels.

A `Frame` may explicitly use `ClipsDescendants`; a `ScrollingFrame` uses its
private viewport clip. Nested grids are allowed. Each layout operates only on
its own direct children, so work remains linear in direct child count rather
than becoming a global constraint solve.

A grid within `ScrollingFrame` lays out against the retained content coordinate
system exactly as a list does. `CanvasSize` remains explicit.

## Clipping semantics

`Frame.ClipsDescendants = true` provides rectangular descendant clipping.
Clipping is retained shared state implemented through private projection state.
The private clip representation is never retained or Luau-addressable.

An initial backend may project:

```text
Frame host element
  -> private CarbonLuau clip root
       -> public projected descendants
```

The private root occupies the Frame content rectangle, uses an internal
rectangular graphic/mask, hides the mask graphic and remains inaccessible to
scripts. This model prevents clipping correctness from depending on Frame
background visibility or transparency. It is implementation guidance, not a
permanent promise about one Carbon/Unity mechanism.

Clipping is rectangular only. Foundation 3 has no rounded or arbitrary
image-shaped clipping. Nested clips intersect within the effective depth bound.
Clipping affects descendant rendering and interaction eligibility: an action
outside an ancestor clip is not eligible merely because its underlying control
exists.

Changing `ClipsDescendants` is initially structural/full-rebuild. Foundation 3
does not require component add/remove patching.

### Clipping qualification gate

Authenticated current-client qualification must establish:

- visible descendant clipping;
- behavior under a fully transparent parent;
- nested clipping;
- rejection of interactions outside clipped regions;
- clipping inside and around `ScrollingFrame`.

If evidence cannot satisfy this contract, omit `ClipsDescendants` from the
implemented/public Foundation 3 release subset. Do not weaken the contract.

## GuiFont semantics

`GuiFont` is a project-owned immutable, host-lifetime-independent value. It owns
no host resource and uses ordinary immutable value equality.

Rules:

- exactly four initial values;
- no arbitrary string or path;
- no public constructor;
- no filesystem or network capability;
- no host identifier visible to Luau;
- default `RobotoCondensedRegular` preserves existing behavior;
- `Font` mutation is patchable where qualified.

If `TextBox` is reconsidered in a later phase, it should reuse `GuiFont`.
Foundation 3 does not expose `TextBox` merely because the value type exists.

### Font qualification gate

All four values require supported-current-client rendering qualification. If a
font is unavailable, remove that `GuiFont` member before public qualification.
Do not silently fall back or expose arbitrary host strings. Existing visuals
must remain unchanged until the author explicitly uses a Foundation 3 font.

## Programmatic scrolling semantics

### One-way Presentation effect

`ScrollTo` requires both normalized coordinates in `0..1`. CarbonLuau uses
top-left `(0, 0)` and bottom-right `(1, 1)`; the backend may translate to a
different host convention. Only axes enabled by retained scrolling configuration
are transmitted.

`ScrollToTop` and `ScrollToBottom` change only vertical intent. They do not read
or synthesize the current horizontal position and produce a controlled error if
Y scrolling is unavailable.

Each method resolves one exact D11 Player connection and applies only when that
connection currently has an eligible Presentation of the containing
`ScreenGui`. No current Presentation produces a controlled programming error
under the established GUI error model.

After local host acceptance:

- no retained scroll value remains;
- `Clone()` copies no effect;
- other viewers are unaffected;
- a later full rebuild may reset client-local scroll position;
- CarbonLuau does not claim where the client ultimately ended up.

### Rebuild ordering and failure

If a rebuild and scroll effect are due in one flush:

```text
rebuild first
then scroll intent
```

The backend may equivalently fold the intent into the replacement projection.

Only the newest unsent intent per `(Presentation, ScrollingFrame)` is retained.
If the host locally rejects the send, the latest intent remains for the next
eligible synchronization attempt. Local host acceptance consumes it. There is
no client acknowledgement and no replay during a later unrelated rebuild.

### Scroll-effect publication

During committed execution:

```text
validate ResourceOwner
validate exact Player
validate current Presentation
stage latest bounded effect
send during later GUI flush
```

During provisional execution:

```text
ResourceOwner      = ScrollingFrame owner
PublicationContext = admitted provisional operation
```

The effect may be journaled but cannot become client-visible before commit. At
commit:

1. publish committed retained changes first;
2. resolve the resulting current Presentation;
3. bind the effect to that resulting Presentation epoch;
4. enqueue it for a later GUI flush.

If the exact Player disconnects or the Presentation disappears before commit,
discard the ephemeral effect with bounded diagnostics. Do not fail or roll back
otherwise valid retained publication.

This is the only new Foundation 3 publication concept. It adds bounded
one-shot effect publication, not retained authority or another ownership model.

### Scroll qualification gate

Authenticated-client qualification must cover:

- top and bottom orientation;
- midpoint behavior;
- horizontal, vertical and XY scrolling;
- exact Player isolation;
- repeated same-frame calls with latest-wins behavior;
- rebuild-before-effect ordering;
- local failure and retry.

If host partial update is unreliable, bounded structural replacement may
implement the same one-way public effect. Backend difficulty does not authorize
`CanvasPosition` or readable scroll state.

## Retained, projected and Presentation state

| State | Classification |
|---|---|
| `UIGridLayout` and properties | retained shared |
| child `LayoutOrder` | retained shared |
| child `Position` and `Size` under grid | retained shared, projection temporarily overridden |
| computed grid cell rectangle | deterministic projection |
| `Frame.ClipsDescendants` | retained shared |
| private clip root | deterministic projection |
| `GuiFont` value | lifetime-independent immutable value |
| text `Font` properties | retained shared |
| actual scroll position | Presentation/client-local |
| pending `ScrollTo*` | bounded one-shot Presentation effect |
| consumed scroll intent | no retained state |

The model remains one retained tree projected to multiple Presentations. No
per-Player retained tree is introduced.

## Synchronization classification

| Mutation | Classification |
|---|---|
| create/destroy/reparent `UIGridLayout` | structural/full rebuild |
| grid property mutation | layout-affecting |
| `LayoutOrder` under list/grid | layout-affecting |
| `LayoutOrder` without a layout | metadata-only |
| child `Visible` under grid | existing visibility plus layout-affecting |
| child `Position` under grid | retained-only while grid governs projection |
| child `Size` under grid | retained-only while grid governs projection |
| child `AnchorPoint` under grid | layout-affecting |
| `UIPadding` with grid | existing layout-affecting path |
| `Frame.ClipsDescendants` | structural/full rebuild initially |
| `Font` | patchable |
| `ScrollTo*` | Presentation-local effect |

Grid recomputation touches only direct arranged children. There is no historical
geometry queue. Existing dirty overflow continues to collapse to authoritative
whole-presentation reconciliation.

## Ownership and publication

D15/D16 remain authoritative:

- `UIGridLayout` is an ordinary owner-bound retained GUI resource;
- `ClipsDescendants` and `Font` are ordinary retained properties;
- `GuiFont` owns no host resource;
- retained Foundation 3 mutation uses the existing GUI publication journal;
- foreign-domain retained mutation preserves existing `ResourceOwner` and
  `PublicationContext` separation;
- provisional retained work creates no client effect before commit;
- `ScrollTo*` alone uses the new bounded Presentation-effect rule.

Sharing a reference does not transfer ownership. No second consistency or
ownership model is introduced.

## Hard resource bounds

No existing projection or serialization envelope is raised.

| Resource | Initial hard bound |
|---|---:|
| active `UIListLayout`/`UIGridLayout` per parent | 1 total |
| `UIPadding` per parent | 1 |
| grid-arranged direct children | 64 |
| `FillDirectionMaxCells` | `1..64` |
| grid work | O(direct children), maximum 64 |
| synthesized empty retained cells | 0 |
| effective nested clip depth | 4 |
| private clipping projection elements | 1 per clipping Frame |
| projected elements per screen | existing 257 |
| pending scroll effects per Presentation | 16 |
| pending scroll effects per domain | 512 |
| pending scroll effects globally | 4096 |
| pending history per Presentation/ScrollingFrame | 1 latest value |
| public fonts | 4 |

Effective clipping depth counts both explicit Frame clips and private
`ScrollingFrame` viewport clips. A `ClipsDescendants = true` mutation that would
exceed authoritative projection limits fails atomically rather than increasing
those limits.

Timing, latency and rate measurements remain implementation qualification
targets rather than public compatibility guarantees.

## Explicit exclusions

Foundation 3 does not include:

- `AutomaticSize`;
- `AutomaticCanvasSize`;
- `AbsoluteContentSize`;
- `CanvasPosition` or readable scroll state;
- `UIAspectRatioConstraint`;
- `UISizeConstraint` or generic constraints;
- `UIStroke` or a CarbonLuau outline helper;
- `UICorner`;
- advanced image scale modes;
- arbitrary fonts or font paths;
- `TextBox` or typed text ingress;
- animations or tweens;
- drag/drop;
- focus or navigation;
- client geometry queries;
- arbitrary client scripting.

`UIAspectRatioConstraint` and scale-aware size constraints are not generally
server-computable because comparing or converting affine X/Y values requires
actual viewport dimensions. Host content fitters would move authority into
client layout and therefore do not resolve the architectural issue.

Unity Outline applies to a specific projected Graphic, while one retained
CarbonLuau object may project to multiple graphics. Foundation 3 does not invent
an ambiguous outline target model.

Host image types do not honestly map to a source-independent Roblox-style
Stretch/Fit/Crop/Tile contract, so `ImageSource` remains unchanged.

These are Foundation 3 scope exclusions, not permanent rejection from a future
separately designed phase unless the capability itself conflicts with canonical
authority rules. `TextBox` specifically remains deferred under D16's existing
exact-text transport gate and is not reopened here.

## Foundation 1/2 compatibility

Foundation 3 is additive. Existing behavior remains unchanged unless an author
explicitly uses a Foundation 3 feature, including:

- ordinary retained `Position` and `Size`;
- `UIListLayout`, `UIPadding` and `LayoutOrder`;
- `ZIndex` and attachment-order `GetChildren`;
- `Clone` and `Destroy`;
- `TextButton.Activated` and `ImageButton.Activated`;
- explicit `CanvasSize` and client-local scrolling;
- immutable `ImageSource`;
- D15/D16 publication, replacement and recovery;
- one retained tree with multiple Presentations.

Grid-specific projection authority applies only while a direct child is
actively governed by `UIGridLayout`. `Clone()` copies retained grid properties,
`ClipsDescendants`, Font values and ordinary retained children, but no pending
scroll effect or Presentation state.

## Differences from Roblox

Foundation 3 is Roblox-familiar, not Roblox-equivalent:

| Roblox behavior | CarbonLuau Foundation 3 |
|---|---|
| grid can derive/clamp count from absolute space | explicit nonzero `FillDirectionMaxCells` |
| grid can depend on resolved client bounds | topology never depends on client pixels |
| `StartCorner`, automatic sizing, constraints | absent |
| general `GuiObject.ClipsDescendants` | Frame only |
| rounded/rotated clipping | rectangular only |
| broad font/FontFace catalog | four project-owned values |
| `CanvasPosition` and readable offset | absent |
| pixel scroll assignment | normalized one-way Presentation effect |
| broad outline/corner/image styling | absent |

## Implementation sequence

No implementation begins as part of architecture adoption.

### GUI-3A: schema and deterministic grid

Implement only class/property descriptors, list-or-grid exclusivity, affine grid
compilation, retained Position/Size preservation, hidden children, layout dirty
behavior, and Clone/reparent/destroy behavior. Do not add clipping, font or
scroll-effect work.

### GUI-3B: clipping

Implement `Frame.ClipsDescendants`, private clip-root render IR, projection
accounting, nested-depth validation and structural reconciliation. Then perform
current-client visual and hit-region qualification.

### GUI-3C: fonts

Implement `GuiFont`, four singleton values, text Font descriptors, backend
mapping and patch support. Do not add generic Enum infrastructure.

### GUI-3D: scroll effects

Implement normalized scroll-effect representation, exact Player/Presentation
validation, latest-wins bounded pending storage, provisional publication,
rebuild ordering and failure behavior. Do not add `CanvasPosition`.

### GUI-3E: lifecycle, scale and public qualification

Repeat the Foundation 2E/2F closure pattern across the implemented Foundation 3
subset. Public identity remains a separate release-planning decision.

## Qualification plan

### Grid

Cover horizontal and vertical fill; max-cell values 1, middle and 64; rejection
of 0 and 65; every alignment combination; mixed scale/offset `CellSize` and
`CellPadding`; `UIPadding`; hidden children; duplicate `LayoutOrder`; `ZIndex`
independence; Position/Size mutation while arranged; grid removal restoring
retained geometry; grid/list conflicts; nested grids; grid in `ScrollingFrame`;
overflow; 64-child boundary; publication rollback; Clone/Destroy/reparent; and
dirty overflow.

Golden tests must prove identical retained state compiles to identical geometry
independent of viewer.

### Clipping

With an authenticated current Rust client, cover normal and transparent Frame
clipping, nested clips, both nesting orders with `ScrollingFrame`, partially
clipped `TextButton`/`ImageButton`, hit rejection in clipped-away regions,
toggle/rebuild/recovery and effective-depth boundaries.

### Fonts

For all four values, cover initial render, patching between fonts, shared-view
updates, Clone, replacement/recovery, internal-only serialized host paths and
rejection of forged values.

### Scroll effects

With an authenticated client, cover top, bottom, midpoint, horizontal and XY;
two-viewer isolation; same-frame latest-wins; Hide/Show; rebuild-before-send;
no replay after a consumed effect; local backend failure; disconnect and stale
Player; provisional success/failure; owner replacement; and
Presentation/domain/global effect bounds.

### Regressions

Re-run affected Foundation 1/2 lifecycle, publication, replacement, recovery,
shared-view and scale suites plus architecture/API/release checks. Preserve the
257 projected-element limit.

## Host-dependent qualification questions

Three host questions remain. They are qualification gates, not unresolved API
decisions:

1. Prove that the private invisible mask projection clips rendering and hit
   eligibility, composes with nested masks and `ScrollingFrame`, and works under
   a transparent parent. Omit `ClipsDescendants` if it cannot meet the contract.
2. Render all four proposed fonts on the supported client. Remove an unavailable
   member before qualification instead of falling back.
3. Verify normalized ScrollView updates, orientation and unrelated-state
   preservation. Use bounded structural replacement if partial update is
   unreliable.

## Version status

Architecture adoption assigns no package or scripting API identity. Current
identities remain package `0.4.0`, scripting API `0.4.0-experimental`, native
ABI `1.4`, provider protocol `1.2`, package schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830`.

Foundation 3 release identity remains gated on implementation, qualification
and later explicit release planning. This record does not begin GUI-3A and does
not alter any production source.
