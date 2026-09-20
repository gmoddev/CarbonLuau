# GUI Foundation 2A: deterministic layout

Starting commit: `dd7755af1293fad822a40d5be5f1d80b387acaa8`.

GUI Foundation 2A implements only D16's deterministic retained-layout slice.
It adds `GuiObject.LayoutOrder`, `UIListLayout`, `UIPadding`, server-side affine
projection and bounded layout dirty synchronization. Scrolling, images,
TextBox, typed input and all GUI-2B through GUI-2F behavior remain deferred.

## Public retained model

`LayoutOrder` is an integer with default 0 and range -32768 through 32767. It is
retained metadata without an applicable sibling UIListLayout. Under a list it
controls geometric order, followed by attachment ordinal and object identity.
It never changes retained child order or `ZIndex` rendering order.

UIListLayout and UIPadding are public, owner-bound, non-rendering GuiNode leaves.
They support the ordinary Name, Parent, ClassName, Clone, Destroy, GetChildren,
FindFirstChild and IsA behavior applicable to GuiNode. They are not GuiObjects,
cannot own children and can only be parented under a GuiObject. A destination
may own at most one helper of each class. Creation and reparenting validate that
cardinality before changing either tree.

Helpers count toward ordinary object, tree-depth and screen limits. Up to 64
direct GuiObject children remain arrangeable; the two helper slots do not reduce
that established arranged-child bound.

## Projection compiler

The compiler resolves each parent's optional UIPadding into a symbolic content
rectangle represented as affine scale and offset pairs. Child retained Size is
composed with that rectangle. Without a list, retained Position is composed in
the same way. With a list, visible direct GuiObject children are sorted, their
main-axis sizes and affine gaps are accumulated, and start, center or end
alignment derives each projected top-left position. AnchorPoint is then applied
to produce the ordinary projected Position and Size consumed by the existing
backend-neutral RectTransform plan.

No viewport, text or client geometry measurement is used. UIListLayout and
UIPadding emit no CUI elements and no Unity layout component. Nested containers
work because every projection remains parent-relative. Built-in TextLabel and
TextButton text uses the parent's padded content rectangle while the parent
background remains unchanged.

Retained Position is never rewritten. A list-managed read returns the authored
value, while projection uses the computed slot. Removing, destroying or
reparenting the layout makes the retained Position authoritative again. Child
Size remains authoritative; Foundation 2A adds no automatic, flex, fill,
text-derived, minimum or maximum sizing.

## Synchronization and publication

The descriptor model now includes an internal layout-affecting mutation class.
LayoutOrder without a list produces no projection work. List and padding
properties synthesize rectangle dirtiness only for relevant direct GuiObject
children. Managed Size and Visible changes keep their existing patch behavior
and also recompute that direct sibling set. Hidden children consume no list
space. Helper create, destroy and reparent remain structural full
reconciliations.

Synthesized detail uses the existing 512-object per-domain dirty envelope. The
existing 64 arranged-child bound caps one recomputation. Overflow clears detail
and requests the established whole-presentation reconciliation. No historical
layout queue exists; the retained state at compile time wins.

All helper creation, parenting, destruction and property changes use the D15
publication journal. Provisional reads observe staged state, no presentation
flush occurs before commit, rollback restores the prior tree and synchronization
state, and commit publishes one atomic newest-state transition. Existing
owner-domain validation and foreign publication behavior apply unchanged.

## Focused qualification

The managed focused suite covers descriptor defaults, types and ranges;
vertical and horizontal lists; every start, center and end alignment pairing;
zero and mixed scale/offset gaps; four-sided padding; built-in text padding;
mixed child sizes; AnchorPoint; hidden children and visibility toggles; duplicate
order ties; ZIndex independence; retained attachment order; retained Position
restoration; Size, order, add, remove and reparent changes; helper cardinality;
same-domain cross-root helper moves; nested layouts; deterministic plans;
publication commit and rollback; cloning; destruction; teardown; dirty overflow;
and the 64-child limit.

One Windows focused run recorded the following non-contractual Stopwatch ticks
and estimated canonical plan sizes. Results include test harness and JIT noise
and are evidence of bounded scaling, not timing guarantees.

| Arranged children | Full compile ticks | Patch/recompute ticks | Dirty elements | Estimated plan bytes |
|---:|---:|---:|---:|---:|
| 1 | 761 | 293 | 1 | 625 |
| 10 | 2,821 | 2,094 | 10 | 2,615 |
| 32 | 8,346 | 5,962 | 32 | 7,540 |
| 64 | 17,113 | 12,498 | 64 | 14,708 |

The Rust CUI golden asserts ordinary RectTransform output and absence of helper
or host layout-group components. Final Windows, Linux, sanitizer, full runtime,
architecture, API, packaging and hosted CI results are recorded in the task
completion report for the implementation commit.

## Identity and remaining scope

This phase changes no package, scripting API, native ABI, provider protocol,
package schema or pinned Luau identity. Foundation 2 remains unreleased and has
no separately assigned API version. The existing 0.4.0 experimental identity
continues to describe the previously planned candidate; this implementation
record does not by itself add Foundation 2A to a published artifact.

Authenticated-client visual layout remains unqualified and non-gating. GUI-2B+
scrolling, ImageSource, ImageLabel, ImageButton, TextBox, Submitted, typed text
ingress, grid layout, automatic sizing and advanced styling were not started.
