# GUI Foundation 3B: Frame clipping

Starting commit: `b012028ec4ff4b97b4f5a80776fa91664fdcdbf4`.

GUI Foundation 3B implements only D17's bounded rectangular
`Frame.ClipsDescendants` slice. The production source revision requiring the
later authenticated-client supplement is
`caad3cc6f08312a62f92b882783e0107d3782f8f`.

The implementation is **IMPLEMENTED / CLIENT-UNQUALIFIED**. It is not part of
the already qualified `0.4.0-experimental` public release identity. The overall
GUI-3B verdict is PARTIAL solely because the mandatory authenticated-client
visual and hit-region qualification is unavailable.

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

## Publication and lifecycle

Clipping uses the existing D15 publication journal and ownership model.
Provisional mutation supports read-your-writes but produces no projection or
action change before commit. Rollback restores the previous value and dirty
state; commit exposes one newest-state transition. Foreign-domain resource
ownership, root/addon replacement, provider retirement, VM recovery and
CarbonLuau teardown continue through the established GUI registry lifecycle.

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
`caad3cc6f08312a62f92b882783e0107d3782f8f`. Failure of the private mask to satisfy both visual and
interaction semantics requires omission from the implemented/public Foundation
3 release subset, not a weaker contract.

## Identity and remaining scope

No package version, scripting API version, native ABI, provider protocol,
package schema or pinned Luau revision changed. GUI-3C fonts, GUI-3D ScrollTo
effects, GUI-3E closure, TextBox, general GuiObject clipping, rounded clipping,
UIStroke and UICorner were not started.
