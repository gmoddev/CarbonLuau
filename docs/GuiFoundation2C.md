# GUI Foundation 2C: retained scrolling

Starting commit: `d9b4b1b2b2116334ec30023679207341392c1a0f`.

GUI Foundation 2C implements only D16's scrolling slice. It adds the retained
`ScrollingFrame` container, explicit canvas configuration, bounded Rust CUI
ScrollView projection and composition with the existing deterministic layout
and typed-image surfaces. TextBox, typed text ingress and GUI-2D+ behavior
remain deferred.

## Public retained model

`ScrollingFrame` is an ordinary child-capable GuiObject. It inherits all common
layout, visual, parenting, clone, destroy and lifetime behavior and adds:

| Property | Type | Default | Accepted value |
|---|---|---|---|
| `CanvasSize` | `UDim2` | `UDim2.fromScale(1, 1)` | ordinary bounded UDim2 |
| `ScrollingDirection` | string | `"Y"` | `"X"`, `"Y"` or `"XY"` |
| `ScrollingEnabled` | boolean | `true` | boolean |

CanvasSize is explicit retained state. It is not inferred from child geometry,
list layout, text, a viewport or client measurements. Foundation 2C exposes no
CanvasPosition, AutomaticCanvasSize, scroll event, programmatic ScrollTo or
scrollbar styling API.

## Retained and Presentation-local state

Canvas configuration, ordinary GuiObject properties, children,
`UIListLayout` and `UIPadding` are shared retained state. Current scroll offset,
scrollbar drag state, gesture state and inertia belong only to the client and
Presentation. CarbonLuau does not read, store or fabricate that state. Players
viewing the same retained tree therefore have independent client scroll state.

A full reconciliation, Hide/Show, disconnect/reconnect, replacement or fatal
VM recovery can create a fresh Presentation and reset the host/client scroll
position. No preservation or acknowledgement is promised.

## Projection and layout

The backend-neutral render plan represents a ScrollingFrame as a typed
ScrollView element. The Rust adapter emits the supported
`UnityEngine.UI.ScrollView` component, an explicit content transform,
horizontal/vertical flags, clamped movement, disabled inertia and supported
scrollbars. It intentionally emits no normalized-position field.

The current Rust UI implementation creates private `___Viewport` and
`___Content` nodes for that component and registers the content name as a valid
parent. CarbonLuau projects direct retained children under that bounded private
content identity. Those nodes never enter the retained model and are not
visible to Luau.

The host mapping was rechecked on 2026-09-20 against Oxide RustCui revision
[`ca69c156`](https://github.com/OxideMod/Oxide.Rust/blob/ca69c156382acfbb62804de0710d9555d5a206de/src/RustCui.cs),
Facepunch Rust.Community revision
[`c1aba160`](https://github.com/Facepunch/Rust.Community/blob/c1aba1600e8cf3dc7087bed99fdc7f8aa174af13/CommunityEntity.UI.cs),
Carbon revision
[`4d1b081e`](https://github.com/CarbonCommunity/Carbon/blob/4d1b081eadef99da774e0342899bddcd638e26d2/src/Carbon.Components/Carbon.Common/src/Carbon/Components/CUI.cs)
and Carbon.Common LUI revision
[`f54d7936`](https://github.com/CarbonCommunity/Carbon.Common/blob/f54d7936382cfdf350a3e02e1ea0eab6d177658b/src/Carbon/Components/LUI.cs).
The supported ScrollView component, content transform, axis flags, private
viewport/content construction and scrollbar mapping remain consistent with
D16. No host contradiction was found.

CanvasSize maps deterministically to a top-left-pivoted content transform.
`"X"`, `"Y"` and `"XY"` select the corresponding axis flags. Setting
ScrollingEnabled false disables the ScrollView and configured scrollbar input
without changing retained children or CanvasSize.

`UIListLayout` and `UIPadding` continue to run in CarbonLuau's deterministic
server-side layout compiler. The compiler uses the explicit CanvasSize as the
content coordinate space, ignores hidden children for list spacing and never
depends on the unknown client scroll offset. Text, image and nested Frame
children compose through the same existing projection rules.

## Synchronization, publication and lifetime

CanvasSize, ScrollingDirection and ScrollingEnabled are structural mutations in
this phase and request a whole-Presentation replacement. Inherited patchable
GuiObject properties keep the existing GUI-1D update path and do not emit a
ScrollView component on a narrow patch. Backend failure leaves retained state
authoritative, marks delivery uncertain and retries the newest complete state;
no historical scroll-state queue or acknowledgement is introduced.

ScrollingFrame creation, mutation, parenting and Show intent use the existing
GUI publication journal. Provisional work cannot flush. Rollback produces no
client effect, including for foreign-domain mutations of an escaped owner-bound
ScrollingFrame; commit makes the atomic retained result eligible for the next
bounded flush.

Clone copies retained scrolling properties and descendants into fresh object
identities. It does not copy a Presentation or client scroll state. Destroy,
domain retirement, replacement and VM recovery use the existing D15 teardown
and stale-reference rules.

## Projection bounds and performance

One ScrollingFrame has a fixed worst-case projected-element charge of seven:
the authored outer element plus host-created viewport, content, two scrollbars
and two handles. The fixed worst case keeps direction changes from making an
already admitted retained tree exceed the authoritative envelope. Helpers
remain non-rendering, while ordinary and image children retain their existing
charges. Creation and reparenting reject an overflow atomically; serializer and
payload limits were not raised.

Focused measurements use 1, 10, 32 and 64 retained child configurations with
layout, padding and image composition. Results are recorded from the final
qualified Linux worktree below. Timing and managed-memory observations are
diagnostic measurements, not public guarantees.

| Children | Projected elements | Estimated bytes | Serialized bytes | Full compile ticks | Structural rebuild ticks | Observed managed bytes |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 9 | 720 | 1,230 | 344 | 317 | 0 |
| 10 | 18 | 2,821 | 4,087 | 1,076 | 1,202 | 8,200 |
| 32 | 40 | 7,989 | 11,103 | 2,943 | 3,020 | 0 |
| 64 | 75 | 16,255 | 22,164 | 6,068 | 6,168 | 2,376 |

The managed-memory deltas are noisy collection snapshots, not retained-size
guarantees. All configurations stayed below the unchanged 257-element and
65,536-byte authoritative operation bounds.

## Qualification

The focused managed suite covers defaults, type/range/direction rejection,
retained readback, absence of CanvasPosition and AutomaticCanvasSize, X/Y/XY
projection, disabled input, independent Presentations, private content IDs,
Rust CUI fields, nested children, layout/padding/hidden children, image
composition, inherited patches, structural rebuilds, Show/Hide,
disconnect/reconnect, clone/destroy, publication commit/rollback,
foreign-domain publication, backend recovery and projection/cardinality
boundaries. The native suite exercises the public Luau surface through the real
compiler and VM.

Qualification of the implementation worktree completed on 2026-09-20:

- A bounded four-CPU, 8 GiB BigVPS Linux container passed all five release
  native CTest suites, loader/export checks and the complete managed/native
  runtime suite. That suite retained GUI Foundations 1A through 1G, GUI-2A,
  GUI-2B, Foundations A through G and Phase 0 through 3 coverage.
- A separate ASan/UBSan/leak build passed all five native suites with leak
  detection and halt-on-error enabled.
- Local package, API and architecture audits passed. The package retained 34
  production C# sources and excluded native/live fixtures. Linux release
  construction included the scrolling example and reproduced byte for byte.
- DockerPC was unavailable. Windows native/runtime, import and release checks
  therefore rely on the hosted Windows workflow attached to the implementation
  commit rather than an unqualified local substitution.
- No RustDedicated/Carbon installation or active live worker was available on
  BigVPS, so live Carbon qualification was not run. This does not replace or
  invalidate earlier live evidence.

Authenticated-client scrolling, clipping, default scrollbar behavior and two
Players' independent observed positions remain unqualified and non-gating.
Only the retained model, deterministic projection and serialized host mapping
are claimed here.

## Identity and remaining scope

This phase changes no package, scripting API, native ABI, provider protocol,
package schema or pinned Luau identity. Foundation 2 still has no release/API
identity. Authenticated-client scrolling, clipping, scrollbar appearance and
independent-position observations remain unqualified unless separately recorded
with a real authenticated Rust client.

TextBox, Submitted, typed text ingress, CanvasPosition, AutomaticCanvasSize,
scroll events, scrollbar styling, programmatic ScrollTo, focus APIs,
UIGridLayout, general clipping and all GUI-2D+ work were not implemented.
