# Gui3: qualification ledger

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../Invariants.md).

## Records

- [GuiFoundation3A.md](#guifoundation3a)
- [GuiFoundation3B.md](#guifoundation3b)
- [GuiFoundation3C.md](#guifoundation3c)
- [GuiFoundation3D.md](#guifoundation3d)
- [GuiFoundation3E.md](#guifoundation3e)

<!-- BEGIN RECORD GuiFoundation3A.md -->

<a id="guifoundation3a"></a>

<a id="guifoundation3a-gui-foundation-3a-deterministic-grid-layout"></a>

# GUI Foundation 3A: deterministic grid layout

Starting commit: `6c2b4ae3f7c88054695d84cbcf87b0447f82d619`.

GUI Foundation 3A implements only D17's deterministic `UIGridLayout` slice.
It adds the retained grid helper, explicit row/column topology, affine
server-side projection, list-or-grid exclusivity and bounded grid dirty
synchronization. It does not implement `ClipsDescendants`, `GuiFont`,
`ScrollTo*`, TextBox or any GUI-3B or later behavior.

<a id="guifoundation3a-public-retained-model"></a>

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

<a id="guifoundation3a-projection-compiler"></a>

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

<a id="guifoundation3a-synchronization-and-publication"></a>

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

<a id="guifoundation3a-focused-qualification"></a>

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
source revision `b292ecd6ff6c52081de0a991979968502a8353a0` or explicitly requalify a
documented descendant after confirming its production GUI-3A source is
unchanged.

<a id="guifoundation3a-identity-and-remaining-scope"></a>

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

<!-- END RECORD GuiFoundation3A.md -->

<!-- BEGIN RECORD GuiFoundation3B.md -->

<a id="guifoundation3b"></a>

<a id="guifoundation3b-gui-foundation-3b-frame-clipping"></a>

# GUI Foundation 3B: Frame clipping

Starting commit: `b292ecd6ff6c52081de0a991979968502a8353a0`.

GUI Foundation 3B implements only D17's bounded rectangular
`Frame.ClipsDescendants` slice. The production source revision requiring the
later authenticated-client supplement is
`eb25da029c83b2d1a48c4b21bc86e40858ef4dcc`.

The implementation is **IMPLEMENTED / CLIENT-UNQUALIFIED**. It is not part of
the already qualified `0.4.0-experimental` public release identity. The overall
GUI-3B verdict is PARTIAL solely because the mandatory authenticated-client
visual and hit-region qualification is unavailable.

<a id="guifoundation3b-public-retained-surface"></a>

## Public retained surface

`Frame.ClipsDescendants` is a writable boolean with default `false`. It exists
only on `Frame`, not generic GuiObject or `ScrollingFrame`. Clone copies it as
ordinary retained state. GetChildren and FindFirstChild continue to expose only
the retained tree.

When false, projection is unchanged. When true, descendants project beneath a
deterministic private clip node that fills the Frame rectangle. The Frame keeps
its independent author background element, while the private node serializes an
invisible image plus `UnityEngine.UI.Mask` with its mask graphic hidden. A fully
transparent Frame therefore retains clipping authority without using its author
background as the mask.

The helper has no GuiObject identity, retained properties, Luau reference,
clone identity or independent lifecycle. Destroy, reparent, replacement,
recovery and teardown remove obsolete helper projection through normal full
reconciliation.

<a id="guifoundation3b-bounds-layout-and-scrolling"></a>

## Bounds, layout and scrolling

Each explicit clipping Frame costs one projected element. The existing 257
projected-element limit is unchanged. Enabling clipping first validates the
candidate full plan and backend payload; a candidate that would exceed the
element or serialized envelope fails before retained state changes. Focused
tests accept an exact 257-element candidate and reject element 258 atomically.

Effective clipping depth is capped at four. Both explicit Frame clips and the
existing private `ScrollingFrame` viewport clip contribute one level. Property
mutation, creation and reparenting validate the resulting path before changing
the retained tree. Both a clipping Frame around a ScrollingFrame and a clipping
Frame inside one are supported.

List, grid, padding and ordinary retained geometry are compiled before clipping.
Descendants are parented through the private clip projection without modifying
retained Position, Size, attachment order or layout state. Clipping never feeds
geometry back into UIListLayout or UIGridLayout.

<a id="guifoundation3b-interaction-and-synchronization"></a>

## Interaction and synchronization

The projection compiler composes retained affine geometry through ordinary
nested Frames. A TextButton or ImageButton proved wholly outside the nearest
effective explicit clip receives no action command or server action token.
Pre-entry validation repeats the same eligibility check. ScrollingFrame client
offset is deliberately not guessed; controls not provably outside remain
subject to the host mask's clipped hit testing and the authenticated-client
gate.

`ClipsDescendants` is structural. Toggling it requests an authoritative full
rebuild and invalidates prior Presentation action authority. Geometry and layout
changes on a clipped screen with connected controls also rebuild conservatively
so newly visible controls receive fresh authority and newly clipped controls do
not retain host commands. Coalescing remains latest-state-wins.

A failed Replace keeps retained clipping authoritative, leaves the Presentation
in the existing resynchronization path and retries only current state after new
bounded work. There is no clipping history queue or client acknowledgement.

<a id="guifoundation3b-publication-and-lifecycle"></a>

## Publication and lifecycle

Clipping uses the existing D15 publication journal and ownership model.
Provisional mutation supports read-your-writes but produces no projection or
action change before commit. Rollback restores the previous value and dirty
state; commit exposes one newest-state transition. Foreign-domain resource
ownership, root/addon replacement, provider retirement, VM recovery and
CarbonLuau teardown continue through the established GUI registry lifecycle.

<a id="guifoundation3b-available-qualification"></a>

## Available qualification

Focused managed and native coverage includes schema/default/type rejection,
Frame-only exposure, readback, Clone, deterministic unclipped/clipped/transparent
and nested plans, private-node invisibility, host JSON mapping, depth 1 through
4, depth 5 rejection, ScrollingFrame participation, reparenting, grid/padding
composition, retained geometry preservation, fully outside TextButton and
ImageButton eligibility, action rotation, exact projection boundaries,
publication commit/rollback, Replace failure/recovery and 64-child bounded
reconciliation.

The final Linux 64-descendant sample recorded 67 projected elements, 14,814
estimated plan bytes and 22,417 Stopwatch ticks for construction plus the
latest-state clipping rebuild. Timings are worker measurements, not
compatibility guarantees.

The bounded BigVPS Linux worker passed the release native CTest suite, complete
managed runtime suite through the real pinned compiler/VM, loader suite,
GUI Foundations 1B through 3B, addon/provider/package/parser and Foundations A
through G regressions, architecture/API checks and deterministic release
packaging. The instrumented worker separately passed all five ASan/UBSan/leak
CTest targets: ScriptCore, RuntimeAllocationFaults, CompilerContainment,
RuntimeCore and NativeLoadUnload.

Local Windows managed builds and GUI Foundation 1B through 3B model suites pass,
as do architecture, API/link and deterministic release checks. Hosted Windows
and Linux CI results are recorded after the implementation commit is pushed.

Windows native/local GUI-3B qualification is **DEFERRED / UNQUALIFIED** because
DockerPC is unavailable. Available local Windows managed/static/API/package
checks and hosted Windows CI are reported separately and do not substitute for
that gate. Historical Windows evidence for earlier foundations is unchanged.

Authenticated-client visual and hit behavior is **DEFERRED / UNQUALIFIED**.
The later supplement must test normal, transparent and visible-background
Frames; nested clips; both ScrollingFrame nesting orders; partially clipped text
and image buttons; clipped-away versus visible clicks; false/true/false toggles;
recovery; and the effective depth boundary against exact source revision
`eb25da029c83b2d1a48c4b21bc86e40858ef4dcc`. Failure of the private mask to satisfy both visual and
interaction semantics requires omission from the implemented/public Foundation
3 release subset, not a weaker contract.

<a id="guifoundation3b-identity-and-remaining-scope"></a>

## Identity and remaining scope

No package version, scripting API version, native ABI, provider protocol,
package schema or pinned Luau revision changed. GUI-3C fonts, GUI-3D ScrollTo
effects, GUI-3E closure, TextBox, general GuiObject clipping, rounded clipping,
UIStroke and UICorner were not started.

<!-- END RECORD GuiFoundation3B.md -->

<!-- BEGIN RECORD GuiFoundation3C.md -->

<a id="guifoundation3c"></a>

<a id="guifoundation3c-gui-foundation-3c-retained-fonts"></a>

# GUI Foundation 3C: retained fonts

Starting commit: `eb25da029c83b2d1a48c4b21bc86e40858ef4dcc`.

GUI Foundation 3C implements only D17's immutable `GuiFont` and retained text
font slice. The exact implementation revision for later authenticated-client
font qualification is `74ac4075e3d369bc51ca24aa7f3a0b33688d8d61`.

<a id="guifoundation3c-implemented-surface"></a>

## Implemented surface

The Luau facade exposes four immutable singleton-like values:

```lua
GuiFont.RobotoCondensedRegular
GuiFont.RobotoCondensedBold
GuiFont.DroidSansMono
GuiFont.PermanentMarker
```

There is no constructor, string-to-font conversion, arbitrary host font name,
path or filesystem capability. Values compare by canonical identity, carry no host
resource and remain ordinary immutable values after a source GUI object or
domain retires.

`TextLabel.Font` and `TextButton.Font` are retained, writable `GuiFont`
properties. Both default to `RobotoCondensedRegular`, so existing scripts keep
the previous default rendering selection. Other GUI classes do not expose
`Font`.

<a id="guifoundation3c-projection-and-synchronization"></a>

## Projection and synchronization

The backend-neutral render plan carries `GuiFontIdentity`, never a host path.
The Rust CUI backend owns the explicit mapping to the four current Carbon font
asset identifiers. Initial projection includes the retained identity, and Font
changes use the existing bounded text-component patch path. Repeated writes
before a flush coalesce to the newest value. A failed patch enters the existing
full-resynchronization path and converges on retained authority.

Font mutations use the established GUI publication journal. Provisional writes
provide read-your-writes without client output; rollback restores the previous
identity and commit publishes the newest retained state. Clone copies Font.
Destroy, root/addon replacement, fatal VM recovery, provider retirement and
CarbonLuau teardown retain their established object-lifetime behavior without
making escaped GuiFont values stale.

<a id="guifoundation3c-qualification-status"></a>

## Qualification status

Verdict: **PARTIAL**.

Available implementation/model gates cover:

- all four values, immutability, equality and distinctness;
- absence of a constructor, arbitrary string conversion and public host paths;
- TextLabel/TextButton defaults, surface restriction and type enforcement;
- deterministic render identities and exact private backend mapping;
- initial non-default projection, patching, coalescing, multi-viewer updates and
  backend-failure reconciliation;
- provisional commit/rollback, Clone, Destroy, replacement, VM recovery,
  provider-style retirement and full teardown;
- complete available GUI, addon, provider, publication and recovery regressions.

Authenticated current-client rendering is **DEFERRED / UNQUALIFIED**. No
authenticated client was available for this task. A later supplement must test
all four fonts for successful rendering without missing-font fallback, changes
between fonts on TextLabel and TextButton, multi-viewer updates, replacement
and recovery. If any value is unavailable, remove that public member rather
than substitute another font.

Windows native/local GUI-3C qualification is **DEFERRED / UNQUALIFIED** because
DockerPC is unavailable. Hosted Windows results are separate evidence and do
not replace process creation, native runtime, deployment or live/local checks
on the required Windows worker.

<a id="guifoundation3c-evidence"></a>

## Evidence

| Gate | Result |
|---|---|
| Focused managed model | PASS |
| Embedded Luau/native public surface | PASS on Linux |
| GUI Foundations 1A through 3B regressions | PASS on Linux |
| Foundations A through G and Phase 0 through 3 regressions | PASS on Linux |
| Linux release/runtime/package | PASS |
| ASan/UBSan/leak detection | PASS, 5/5 native suites |
| Local Windows managed/static/API/package | PASS |
| Hosted Windows CI | [PASS on attempt 2](https://github.com/gmoddev/CarbonLuau/actions/runs/35555915196) |
| Authenticated-client fonts | DEFERRED / UNQUALIFIED |
| Windows native/local | DEFERRED / UNQUALIFIED |

<a id="guifoundation3c-deferred-scope-and-identities"></a>

## Deferred scope and identities

GUI-3C does not implement `ScrollTo*`, TextBox, FontFace, arbitrary fonts,
styling, outlines, rich text or any GUI-3D+ feature. Package `0.4.0`, scripting
API `0.4.0-experimental`, native ABI `1.4`, provider protocol `1.2`, package
schema `1` and the pinned Luau revision are unchanged. D17 still assigns no
Foundation 3 release identity.

<!-- END RECORD GuiFoundation3C.md -->

<!-- BEGIN RECORD GuiFoundation3D.md -->

<a id="guifoundation3d"></a>

<a id="guifoundation3d-gui-foundation-3d-presentation-specific-scroll-effects"></a>

# GUI Foundation 3D: Presentation-specific scroll effects

Starting commit: `74ac4075e3d369bc51ca24aa7f3a0b33688d8d61`.

GUI Foundation 3D implements only D17's one-way Presentation scroll-effect
slice. The implementation revision requiring later authenticated-client
qualification is `6543951131f6f4ac2b31a1d11d6a5ad0bcd36595`.

<a id="guifoundation3d-public-surface"></a>

## Public surface

```lua
Scroll:ScrollTo(Player, Vector2.new(0.5, 0.5))
Scroll:ScrollToTop(Player)
Scroll:ScrollToBottom(Player)
```

`ScrollTo` accepts finite normalized coordinates in `0..1`, with `(0, 0)` at
the top left and `(1, 1)` at the bottom right. It transmits only axes enabled by
the retained `ScrollingDirection`. Top and Bottom transmit only vertical intent
and fail synchronously when Y scrolling is unavailable. There is no clamping.

These methods are available only on `ScrollingFrame`. They require the exact
current D11 Player connection and an existing Presentation of the containing
ScreenGui. They do not queue for a future Show. Another viewer is unaffected.

<a id="guifoundation3d-effect-and-backend-model"></a>

## Effect and backend model

An effect is not retained scroll state. CarbonLuau exposes no `CanvasPosition`,
readback, completion event, acknowledgement or client geometry. The
backend-neutral request carries one Presentation epoch, one projected
ScrollingFrame identity and optional normalized X/Y intent. Only the Rust CUI
backend knows that the host uses `horizontalNormalizedPosition` directly and
the inverse `verticalNormalizedPosition` convention.

Current Carbon and Rust client source expose nullable horizontal and vertical
normalized update fields and apply them independently during CUI updates:

- [Carbon CUI source](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon.Components/Carbon.Common/src/Carbon/Components/CUI.cs)
- [Rust Community UI source](https://github.com/Facepunch/Rust.Community/blob/master/CommunityEntity.UI.cs)

<a id="guifoundation3d-bounds-publication-and-synchronization"></a>

## Bounds, publication and synchronization

Pending state is latest-wins per `(Presentation, ScrollingFrame)`, with no
historical queue. Hard bounds are 16 effects per Presentation, 512 per domain
and 4096 globally. Admission beyond a bound fails synchronously before changing
state.

Committed calls stage work for a later existing owner-thread GUI flush.
Provisional calls use the existing GUI publication snapshot. Rollback publishes
nothing. At outer commit, retained GUI state is already authoritative; the
effect resolves the resulting exact Player and Presentation and binds to that
Presentation. A vanished Player, Presentation or ScrollingFrame discards only
the ephemeral effect and does not roll back otherwise valid retained work.

Retained synchronization always runs first. A required rebuild is accepted
before a pending effect is attempted. A failed effect send retains only the
newest intent, requests authoritative reconciliation and retries after that
rebuild. Local acceptance consumes the effect. Later unrelated rebuilds do not
replay it.

Effect sends share the existing global GUI send, byte and time budgets. Domain
and Presentation cursors preserve progress when another effect repeatedly
fails. The flush never enters Luau.

<a id="guifoundation3d-lifecycle-and-diagnostics"></a>

## Lifecycle and diagnostics

Pending effects are discarded on Hide, object/screen destruction, disconnect,
cross-screen reparenting, domain replacement, provider retirement, VM recovery
and CarbonLuau teardown. Same-account reconnect receives a new connection
lifetime and no prior effect. Clone copies only retained ScrollingFrame state.

Bounded internal diagnostics count accepted, coalesced, bound-rejected,
Presentation-discarded, Player-discarded, backend-failed and locally accepted
effect events. CarbonLuau does not report an actual client position.

<a id="guifoundation3d-qualification-status"></a>

## Qualification status

Verdict: **PARTIAL**.

| Gate | Result |
|---|---|
| Focused managed model and stress | PASS |
| Embedded Luau/native public surface | PASS on Linux |
| GUI Foundations 1A through 3C regressions | PASS |
| Foundations A through G and Phase 0 through 3 regressions | PASS on Linux |
| Linux release/runtime/package | PASS |
| ASan/UBSan/leak detection | PASS |
| Local Windows managed/static/API/package | PASS |
| Hosted Windows CI | PASS at historical source-equivalent commit `31065a78f9184b164943b6d29d811ff10f6498aa`, retained on `codex/backup-main-pre-squash-20260921` |
| Authenticated-client scroll behavior | DEFERRED / UNQUALIFIED |
| Windows native/local | DEFERRED / UNQUALIFIED |

Authenticated current-client qualification must still establish top, bottom,
left, right, midpoint and XY behavior; exact two-viewer isolation; same-frame
latest-wins; Hide/Show; rebuild ordering; retry; disconnect and stale Player;
provisional commit/rollback; and owner replacement. No authenticated client was
available for this task.

Windows native/local GUI-3D qualification is **DEFERRED / UNQUALIFIED** because
DockerPC remains unavailable. Hosted Windows is separate evidence and does not
replace the required native/local environment.

Hosted Windows, Ubuntu and sanitizer jobs passed in
[validation run 35558447750](https://github.com/gmoddev/CarbonLuau/actions/runs/35558447750).
The documentation deployed successfully in
[documentation run 35558447742](https://github.com/gmoddev/CarbonLuau/actions/runs/35558447742).

<a id="guifoundation3d-scope-and-identities"></a>

## Scope and identities

GUI-3D does not implement readable or retained scroll position, scroll events,
animation, ScrollIntoView, TextBox, GUI-3E or Foundation 4. Package `0.4.0`,
scripting API `0.4.0-experimental`, native ABI `1.4`, provider protocol `1.2`,
package schema `1` and the pinned Luau revision remain unchanged. D17 still
assigns no Foundation 3 release identity.

<!-- END RECORD GuiFoundation3D.md -->

<!-- BEGIN RECORD GuiFoundation3E.md -->

<a id="guifoundation3e"></a>

<a id="guifoundation3e-gui-foundation-3e-qualification-and-release-candidate-closure"></a>

# GUI Foundation 3E: qualification and release-candidate closure

<a id="guifoundation3e-verdict"></a>

## Verdict

**PASS within the available qualification envelope.** GUI Foundation 3E closes
the combined server-side model, publication, lifecycle, scale, compatibility,
documentation and release-candidate gates for the D17 surface. The implemented
Foundation 3 surface is ready for experimental public release in the
still-unreleased package `0.4.0` and scripting API `0.4.0-experimental`.

This verdict does not claim authenticated-client clipping, font rendering or
scroll behavior, and does not claim Windows native/local qualification.

<a id="guifoundation3e-qualified-surface"></a>

## Qualified surface

The final implemented Foundation 3 surface is exactly:

- deterministic `UIGridLayout` with explicit cell geometry, fill topology,
  alignment, list-or-grid exclusivity and retained Position/Size restoration;
- bounded rectangular `Frame.ClipsDescendants` through private projection state;
- immutable `GuiFont` values and retained, patchable `TextLabel.Font` and
  `TextButton.Font`;
- exact-Player `ScrollingFrame:ScrollTo`, `ScrollToTop` and `ScrollToBottom`
  one-way Presentation effects.

No `CanvasPosition`, readback, client geometry or other new GUI feature was
introduced.

<a id="guifoundation3e-combined-interaction-and-shared-views"></a>

## Combined interaction and shared views

The GUI-3E model suite builds a representative tree containing a clipping Frame,
ScrollingFrame, UIPadding, UIGridLayout, text and image nodes, retained fonts and
interactive buttons. It verifies these combinations together:

- grid plus clipping;
- grid plus scrolling;
- grid plus fonts;
- clipping plus scrolling;
- clipping plus interaction authority;
- fonts plus interaction authority;
- a full combined screen under backend failure and recovery.

One retained tree produces equivalent independent Presentations for two Players.
Grid, clipping and font state remains shared retained authority. Scroll position,
pending scroll intent, action authority, client IDs and Presentation epochs stay
Presentation-local. A scroll effect for one exact Player never targets the other
viewer.

<a id="guifoundation3e-ownership-publication-and-lifecycle"></a>

## Ownership, publication and lifecycle

The existing Foundation 2E root and provider lifecycle fixtures now execute
with all Foundation 3 feature families active. They cover foreign-domain grid,
clip and font mutation plus a local exact-Player scroll effect, failed candidate
rollback, successful commit, owner retirement, required consumer reconstruction,
optional no-rebind behavior, provider unload/reload and CarbonLuau teardown.

The combined publication tests establish:

- provisional retained writes provide read-your-writes but no client effect;
- 100 failed combined candidates publish no retained mutation or scroll effect;
- committed retained state is synchronized before the associated one-shot
  Presentation effect;
- loss of a Player or Presentation discards only its effect;
- owner retirement stales escaped host-backed GUI references and action authority;
- immutable GuiFont and ImageSource values remain ordinary lifetime-independent
  values;
- a consumed effect is not replayed by an unrelated later rebuild.

Healthy root/addon replacement preserves the old authority until commit, then
uses fresh domain, object, action and Presentation identities. Fatal recovery
and CarbonLuau unload/reload rebuild fresh GUI authority and return registries,
objects, Presentations, actions, pending effects and queued work to baseline.

<a id="guifoundation3e-synchronization-and-fault-convergence"></a>

## Synchronization and fault convergence

Patchable font changes, grid layout dirties, structural clip changes, scroll
configuration rebuilds and Presentation effects share the established bounded
flush path. Injected update, replacement and scroll failures request an
authoritative rebuild and converge to the newest retained state. There is no
historical geometry or effect log. Rebuild precedes a pending scroll effect, and
successful local acceptance consumes that effect.

<a id="guifoundation3e-bounds-scale-and-stress"></a>

## Bounds, scale and stress

No configured limit changed. The tested combined screen contains 41 retained
objects and projects to 252 elements, close to the unchanged 257-element screen
limit. Its serialized authoritative operation is 24,641 bytes. The same screen
passed with 1, 10, 50 and 100 Presentations and left zero dirty or full-resync
backlog after each flush.

Measured Linux managed memory upper bounds for initial Presentation projection
were 94,200 bytes at 1 viewer, 154,560 at 10, 5,201,192 at 50 and 11,350,776 at
100. These are practical measurements, not compatibility guarantees. Work and
memory scaled with actual Presentations and projected elements; no persistent
per-viewer idle loop was introduced.

Stress coverage includes 100 healthy root replacements, 200 failed replacement
candidates, 100 rich fatal recoveries, 100 failed combined publications, 1,000
Clone/Destroy cycles, repeated Show/Hide and reconnect churn, 1,000 same-target
and alternating scroll effects, provider loss/restoration and full teardown.
Existing hard bounds remain 64 arranged grid children, clipping depth 4,
FillDirectionMaxCells 1 through 64, 257 projected elements, 16 pending scroll
effects per Presentation, 512 per domain and 4,096 globally.

<a id="guifoundation3e-public-examples-and-documentation"></a>

## Public examples and documentation

The release bundle contains runnable examples for all requested patterns:

1. inventory grid: `grid`;
2. vertical-fill grid: `grid-vertical`;
3. grid plus UIPadding: `grid-padding`;
4. grid inside ScrollingFrame: `grid-scrolling`;
5. clipping Frame: `clipping`;
6. nested clipping: `nested-clipping`;
7. font selection: `fonts`;
8. font patch/change: `font-patch`;
9. ScrollTo top/bottom and normalized positioning: `scroll-effects`;
10. two-viewer per-Player intent: `per-player-scroll`;
11. combined Foundation 3 screen: `foundation3-combined`.

The API checker verifies every example is present, uses no raw CUI or private
command token, and executes through the pinned compiler/VM during the runtime
suite. The GUI guide and reference distinguish desired server state from
unacknowledged client observation and retain the TextBox deferral prominently.

<a id="guifoundation3e-compatibility-and-identity"></a>

## Compatibility and identity

Foundation 3 is additive to Foundations 1 and 2. The full regression suite
preserves ordinary Position/Size, UIListLayout, UIPadding, LayoutOrder, ZIndex,
GetChildren attachment order, Clone/Destroy, TextButton/ImageButton Activated,
explicit CanvasSize, client-local scrolling, ImageSource, publication,
replacement/recovery and one-tree/multiple-Presentation behavior.

Foundation 3 remains part of the still-unreleased package `0.4.0` and scripting
API `CarbonLuau 0.4.0-experimental`. Completing an additive surface before the
first 0.4 publication does not justify a 0.5 bump. These identities are unchanged:

| Identity | Value |
|---|---|
| Native ABI | `1.4` |
| Provider protocol | `CarbonLuau.Addons` / `1.2` |
| Package schema | `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

Release scripts create deterministic Windows/Linux bundles, checksums and
provenance with the complete example and public-documentation set. GUI-3E does
not create a tag or GitHub Release.

<a id="guifoundation3e-deferred-qualification"></a>

## Deferred qualification

No authenticated current Rust client was available. These features remain
**IMPLEMENTED / AUTHENTICATED-CLIENT UNQUALIFIED**:

- `ClipsDescendants` visual, transparent-parent, nested and clipped-hit behavior;
- rendering and no-fallback behavior of all four GuiFont assets;
- ScrollTo orientation, midpoint, axis behavior, Player isolation, latest-wins,
  rebuild ordering and retry as observed by a real client.

Later supplements must test these exact implementation revisions, or a
documented source-equivalent descendant:

| Feature | Revision |
|---|---|
| ClipsDescendants | `eb25da029c83b2d1a48c4b21bc86e40858ef4dcc` |
| GuiFont | `74ac4075e3d369bc51ca24aa7f3a0b33688d8d61` |
| ScrollTo methods | `6543951131f6f4ac2b31a1d11d6a5ad0bcd36595` |

Windows native/local GUI-3A through GUI-3E qualification is **DEFERRED /
UNQUALIFIED** because DockerPC is unavailable. Hosted Windows CI is separate
evidence and does not substitute for worker process, IPC, native runtime,
packaging, teardown or live Carbon behavior on the intended Windows worker.
This deferral does not invalidate historical Windows evidence for earlier
foundations.

TextBox and Submitted remain unimplemented under D16's exact-text transport
gate. Foundation 4, automatic sizing, CanvasPosition, arbitrary fonts, URL
images, advanced styling, animations, drag/drop, focus/navigation, client
geometry and arbitrary client scripting remain outside this work.

<a id="guifoundation3e-validation-record"></a>

## Validation record

The closure worktree is based on
`6543951131f6f4ac2b31a1d11d6a5ad0bcd36595`. The final implementation/evidence
revision is recorded by the GUI-3E commit in repository history.

Available validation completed:

- local managed build and focused GUI-3E model suite;
- architecture, API, documentation-link and deterministic package checks;
- Linux release native tests and complete managed/runtime regressions;
- all bundled GUI examples through the pinned compiler and VM;
- deterministic linux-x64 release bundle, checksum and provenance checks;
- ASan, UBSan and leak-detection native tests;
- hosted Windows/Linux CI and documentation deployment at final head.

The last item is recorded by the final workflow URLs in the completion report.
No production Foundation 4 or TextBox work began.

<!-- END RECORD GuiFoundation3E.md -->

