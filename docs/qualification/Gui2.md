# Gui2: qualification ledger

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../Invariants.md).

## Records

- [GuiFoundation2A.md](#guifoundation2a)
- [GuiFoundation2B.md](#guifoundation2b)
- [GuiFoundation2C.md](#guifoundation2c)
- [GuiFoundation2E.md](#guifoundation2e)
- [GuiFoundation2F.md](#guifoundation2f)

<!-- BEGIN RECORD GuiFoundation2A.md -->

<a id="guifoundation2a"></a>

<a id="guifoundation2a-gui-foundation-2a-deterministic-layout"></a>

# GUI Foundation 2A: deterministic layout

Starting commit: `7bd0dba0873f4ef1a99711afd68889ed5c39a649`.

GUI Foundation 2A implements only D16's deterministic retained-layout slice.
It adds `GuiObject.LayoutOrder`, `UIListLayout`, `UIPadding`, server-side affine
projection and bounded layout dirty synchronization. Scrolling, images,
TextBox, typed input and all GUI-2B through GUI-2F behavior remain deferred.

<a id="guifoundation2a-public-retained-model"></a>

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

<a id="guifoundation2a-projection-compiler"></a>

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

<a id="guifoundation2a-synchronization-and-publication"></a>

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

<a id="guifoundation2a-focused-qualification"></a>

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

<a id="guifoundation2a-identity-and-remaining-scope"></a>

## Identity and remaining scope

This phase changes no package, scripting API, native ABI, provider protocol,
package schema or pinned Luau identity. Foundation 2 remains unreleased and has
no separately assigned API version. The existing 0.4.0 experimental identity
continues to describe the previously planned candidate; this implementation
record does not by itself add Foundation 2A to a published artifact.

Authenticated-client visual layout remains unqualified and non-gating. GUI-2B+
scrolling, ImageSource, ImageLabel, ImageButton, TextBox, Submitted, typed text
ingress, grid layout, automatic sizing and advanced styling were not started.

<!-- END RECORD GuiFoundation2A.md -->

<!-- BEGIN RECORD GuiFoundation2B.md -->

<a id="guifoundation2b"></a>

<a id="guifoundation2b-gui-foundation-2b-typed-images"></a>

# GUI Foundation 2B: typed images

Starting commit: `e6e925ff513c499f99898695ef4fc1782706a9e7`.

GUI Foundation 2B implements only D16's typed-image slice. It adds the
immutable `ImageSource` value, retained `ImageLabel` and `ImageButton` classes,
image projection, bounded projection-cost accounting and `ImageButton.Activated`
through the existing GUI-1E ingress. ScrollingFrame, TextBox, submitted text and
all GUI-2C+ behavior remain deferred.

<a id="guifoundation2b-public-retained-model"></a>

## Public retained model

`ImageSource.None()`, `Sprite(Name)`, `Png(Id)`, `Item(ItemId, SkinId?)` and
`SteamAvatar(UserId)` create immutable, value-comparable userdata. The value has
no domain or host lifetime and can remain ordinary Luau state after a creating
domain retires. Assigning it to an image object creates no new capability.

Sprite keys contain 1 through 256 UTF-8 bytes and use only ASCII letters,
digits, underscore, hyphen, dot and slash in canonical non-traversing segments.
PNG IDs are canonical decimal strings of at most 20 digits. Item IDs are signed
32-bit integers; optional skin IDs and Steam user IDs are canonical unsigned
64-bit decimal strings supplied as strings so Luau number precision cannot
alter them. URLs, paths, Carbon image-database names and arbitrary media
metadata are not accepted.

`ImageLabel` and `ImageButton` are ordinary child-capable GuiObjects. Both add
`Image`, `ImageColor3` and `ImageTransparency`; their defaults are
`ImageSource.None()`, white and zero. Their default size is 100 by 100 and
their background is transparent. One retained source is shared by every
Presentation of the same object. Client cache or asset-load success is neither
retained state nor acknowledged to Luau.

<a id="guifoundation2b-backend-mapping-and-synchronization"></a>

## Backend mapping and synchronization

The backend-neutral plan carries a typed image source. Rust CUI projection maps
Sprite to `sprite`, Png to `png`, Item to signed `itemid` plus optional unsigned
`skinid`, and SteamAvatar to a RawImage `steamid`. None emits no graphic
component. The adapter never writes `url`, performs a download or resolves a
server filesystem path.

Every image object projects a retained container and an image-content element.
ImageButton adds a transparent interaction overlay. ImageLabel therefore costs
two projected elements and ImageButton three. The ScreenGui root and every
existing class also have an explicit worst-case projection cost. Creation and
reparenting reject an attachment atomically when the resulting complete screen
would exceed the configured full-presentation element envelope. The default
envelope remains the existing 257-element render-operation bound; serializer
and payload limits were not raised.

ImageColor3 and ImageTransparency use bounded image patches. Image is
structural and requests whole-presentation reconciliation. A failed patch uses
the existing uncertainty path and later full replacement. The retained value
remains authoritative across backend failure; an unavailable client asset does
not roll it back or create a false acknowledgement.

<a id="guifoundation2b-interaction-publication-and-lifetime"></a>

## Interaction, publication and lifetime

ImageButton uses the exact existing Activated Signal, connection limits,
presentation-specific opaque token, exact Player connection binding, epoch,
VM/domain/ScreenGui/object validation, rate limits, queue admission and
pre-entry revalidation. No second command or transport exists. Image color
patches preserve current action authority; structural source replacement,
Hide, Destroy, replacement and recovery invalidate it under the established
D15 rules.

Image state, connections and Show intent participate in the existing GUI
publication journal. Provisional work cannot flush or create a usable token.
Rollback exposes no client effect, while commit makes one atomic retained state
eligible for a later bounded flush. Clone copies source/color/transparency into
new object identities but copies no listeners, viewers, token or Presentation
state. Destroy and domain retirement stale host-backed objects without revoking
ordinary ImageSource values.

<a id="guifoundation2b-qualification"></a>

## Qualification

The focused managed suite covers every constructor and source kind; identifier,
range and traversal rejection; defaults; retained readback; value equality;
clone/destroy/stale behavior; shared multi-viewer state; deterministic render
plans; exact Rust CUI fields; absence of URL/filesystem projection; image color
and transparency patches; structural source reconciliation; backend failure;
ImageButton valid and cross-Player action paths; action rotation and Destroy;
publication commit/rollback; and exact projection boundary/overflow behavior.

The native suite exercises constructors, equality, ordinary catchable errors,
retained assignment, both image classes and scheduler-gated ImageButton
callbacks through the real compiler and VM. The full runtime suite retains GUI
Foundations 1A through 1G, GUI-2A, Foundations A through G and Phase 0 through 3
coverage.

Qualification of the implementation worktree completed on 2026-09-20:

- Windows x64 built the native library and compiler worker, passed the complete
  managed/native runtime suite, passed API and architecture audits, passed both
  production import checks, and produced a deterministic release bundle. The
  previously recorded Foundation G Windows worker-memory-limit case remains
  deferred and is not claimed as qualified by GUI-2B.
- A bounded BigVPS Linux container passed all five native CTest suites and the
  complete managed/native runtime suite. A separate ASan/UBSan/leak build
  passed all five native suites with leak detection enabled.
- Windows and Linux package checks found 34 production C# sources, excluded
  native/live fixtures from the plugin package, and reproduced the release
  bundle byte for byte. The image example is included in both platform release
  bundles.
- DockerPC was unavailable and no RustDedicated/Carbon test server was active
  on BigVPS, so live Carbon and authenticated-client visual qualification were
  not available. This does not replace or invalidate earlier live evidence.

The final hosted Windows/Linux and sanitizer CI result is recorded by the
workflow attached to the implementation commit.

Authenticated-client visual loading, tint/transparency appearance and click
receipt remain unqualified and non-gating. The controlled host mapping and
serialized payload are qualified; no claim is made that a particular asset was
available or visibly rendered for an authenticated client.

<a id="guifoundation2b-identity-and-remaining-scope"></a>

## Identity and remaining scope

This phase changes no package, scripting API, native ABI, provider protocol,
package schema or pinned Luau identity. Foundation 2 still has no release/API
identity. ScrollingFrame, CanvasSize, TextBox, typed text ingress, Submitted,
grid/automatic layout, arbitrary URL images, Carbon image-database integration,
advanced styling and GUI-2C+ work were not implemented.

<!-- END RECORD GuiFoundation2B.md -->

<!-- BEGIN RECORD GuiFoundation2C.md -->

<a id="guifoundation2c"></a>

<a id="guifoundation2c-gui-foundation-2c-retained-scrolling"></a>

# GUI Foundation 2C: retained scrolling

Starting commit: `bc39b4da0b149b6186dc01bd742adf9852d1b769`.

GUI Foundation 2C implements only D16's scrolling slice. It adds the retained
`ScrollingFrame` container, explicit canvas configuration, bounded Rust CUI
ScrollView projection and composition with the existing deterministic layout
and typed-image surfaces. TextBox, typed text ingress and GUI-2D+ behavior
remain deferred.

<a id="guifoundation2c-public-retained-model"></a>

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

<a id="guifoundation2c-retained-and-presentation-local-state"></a>

## Retained and Presentation-local state

Canvas configuration, ordinary GuiObject properties, children,
`UIListLayout` and `UIPadding` are shared retained state. Current scroll offset,
scrollbar drag state, gesture state and inertia belong only to the client and
Presentation. CarbonLuau does not read, store or fabricate that state. Players
viewing the same retained tree therefore have independent client scroll state.

A full reconciliation, Hide/Show, disconnect/reconnect, replacement or fatal
VM recovery can create a fresh Presentation and reset the host/client scroll
position. No preservation or acknowledgement is promised.

<a id="guifoundation2c-projection-and-layout"></a>

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

<a id="guifoundation2c-synchronization-publication-and-lifetime"></a>

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

<a id="guifoundation2c-projection-bounds-and-performance"></a>

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

<a id="guifoundation2c-qualification"></a>

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

<a id="guifoundation2c-identity-and-remaining-scope"></a>

## Identity and remaining scope

This phase changes no package, scripting API, native ABI, provider protocol,
package schema or pinned Luau identity. Foundation 2 still has no release/API
identity. Authenticated-client scrolling, clipping, scrollbar appearance and
independent-position observations remain unqualified unless separately recorded
with a real authenticated Rust client.

TextBox, Submitted, typed text ingress, CanvasPosition, AutomaticCanvasSize,
scroll events, scrollbar styling, programmatic ScrollTo, focus APIs,
UIGridLayout, general clipping and all GUI-2D+ work were not implemented.

<!-- END RECORD GuiFoundation2C.md -->

<!-- BEGIN RECORD GuiFoundation2E.md -->

<a id="guifoundation2e"></a>

<a id="guifoundation2e-gui-foundation-2e-lifecycle-and-rich-control-closure"></a>

# GUI Foundation 2E: lifecycle and rich-control closure

Verdict: **PASS for the implemented Foundation 2 surface and the available
non-authenticated qualification envelope**. `TextBox` remains **DEFERRED / NOT
IMPLEMENTED** because its mandatory host transport gate failed.

Starting commit: `11e2facacebb0f76fd512dd29f5063222b2e7ce2`.

GUI Foundation 2E adds no public class, property, event or version identity. It
qualifies the D16 surface implemented by GUI-2A through GUI-2C across the D15
ownership, publication, replacement, recovery and synchronization boundaries.
The focused evidence lives in
[`GuiFoundation2ETests.cs`](../../tests/runtime/GuiFoundation2ETests.cs).

<a id="guifoundation2e-qualified-surface"></a>

## Qualified surface

The lifecycle closure covers:

- `GuiObject.LayoutOrder`, `UIListLayout` and `UIPadding`;
- immutable `ImageSource`, `ImageLabel` and `ImageButton`;
- `ImageButton.Activated` through the existing D15 action ingress; and
- `ScrollingFrame`, `CanvasSize`, `ScrollingDirection` and
  `ScrollingEnabled`.

These controls continue to use the generic retained registry and publication
journal. Foundation 2E introduces no parallel ownership, scheduling,
synchronization or recovery mechanism.

<a id="guifoundation2e-shared-views-and-ownership"></a>

## Shared views and ownership

One retained rich-control tree was shown to two exact Player connections.
LayoutOrder, padding, image source/color/transparency and scrolling
configuration remained one shared retained value and synchronized to both
Presentations. Each Presentation received a distinct action token. Structural
image or scrolling changes rotated both tokens without retargeting an old token.

The render plan contains no CanvasPosition or normalized scroll position.
Client scroll offset, inertia and gesture state therefore remain
Presentation-local and unobservable. Hide/Show, reconnect, replacement and full
reconciliation may reset them independently.

Cross-domain native tests used package-qualified shared GUI references. A
consumer changed owner-created layout, image and scrolling objects. Failed
consumer initialization rolled every retained change back with no client work;
successful initialization committed atomically while the owner remained valid.
Consumer retirement did not transfer or destroy owner resources. Owner
retirement staled escaped GUI references, while an immutable `ImageSource`
retained by an optional consumer remained an ordinary lifetime-independent
value.

<a id="guifoundation2e-replacement-provider-and-recovery-behavior"></a>

## Replacement, provider and recovery behavior

Failed root and addon candidates left the committed lifetime, retained state and
ImageButton authority unchanged. Successful replacement committed the new
lifetime before retiring the old one, then invalidated all old handles,
Presentations and action tokens. A client-local scroll offset was never treated
as retained replacement state.

Provider unload with rich GUI present retired the owner tree, pending
synchronization and a queued ImageButton callback. A required consumer became
blocked and was reconstructed against the fresh dependency lifetime after
explicit provider re-registration. An optional consumer stayed in its original
lifetime and did not hot-rebind. Old registration and ImageButton tokens stayed
stale.

The real native-ABI fixture completed 100 fatal recovery cycles with a tree that
contained layout helpers, images, an ImageButton callback and scrolling. Every
cycle retired the old GUI registry, pending callback, Presentation and token
before reconstructing fresh state from the committed source snapshot. Host
dispose/recreate likewise returned to zero addon/GUI state and created fresh
VM, domain, Presentation and action identities.

<a id="guifoundation2e-publication-and-synchronization-closure"></a>

## Publication and synchronization closure

The focused suite covers provisional LayoutOrder, layout padding, UIPadding,
ImageSource, ImageButton connection and scrolling mutations. Rollback restores
the exact committed tree, connection count and dirty state. Commit exposes one
atomic newest-state transition after owner-lifetime revalidation.

Fault injection covers failed image/scroll whole-Presentation replacement,
layout detail overflow, mutation while a Presentation requires full
resynchronization, disconnect and owner retirement with pending work. New
mutations collapse into the latest retained revision. Recovery sends a complete
authoritative plan rather than replaying historical layout or render versions.

ImageButton qualification covers exact-Player admission, forged and
cross-Player rejection, source-rebuild rotation, Hide/Show, Destroy, root/addon
replacement, fatal recovery, provider reload, queued work followed by target
retirement and callback mutation of layout/image/scrolling state. All use the
existing D15 `Activated(Player)` semantics and pre-entry stale-work gate.

<a id="guifoundation2e-layout-scrolling-and-resource-closure"></a>

## Layout, scrolling and resource closure

Clone copies retained layout, padding, image and scrolling properties into
fresh object identities but copies no Presentation, client scroll state,
Signal connection or token. Helper reparenting preserves the
one-helper-per-parent cardinality rule. Helper destruction restores authored
Position as projection authority. Recursive destruction and domain retirement
stale every rich-control handle and clear helper, dirty, action and private
scrolling projection state.

The combined stress fixture completed:

- 100 root replacements;
- 100 addon replacements;
- 100 failed candidates;
- 100 fatal VM recoveries;
- 1,000 rich-subtree Clone/Destroy cycles;
- 1,000 Show/Hide cycles;
- 1,000 same-account reconnect cycles;
- 100 ImageButton source rebuild/token rotations; and
- 100 failed scrolling replacements followed by full resynchronization.

Final assertions covered retained objects, helper descendants, Signal
connections, Presentations, action tokens, dirty/full-resync state, pending
callbacks and registered GUI worlds. Live counts returned to the expected root
or zero baseline. Existing fixed-cardinality diagnostics were sufficient:
aggregate object/Screen/Presentation/connection/action counts, dirty and
full-resync counts, backend failures, resource-limit rejection and bounded
action rejection counters covered the new state without exposing tokens or
image-source internals.

<a id="guifoundation2e-textbox-compatibility-record"></a>

## TextBox compatibility record

`TextBox` and `Submitted(Player, Text)` are not implemented.

The GUI-2D gate inspected Rust Dedicated Server Steam app `258550`, build
`25353106`. The inspected files were:

- `Facepunch.Console.dll` SHA-256
  `55f1fe738d9741b43328c681e35ddf33492bd9f30cfe96118c7d4f7a73c77fca`;
- `Assembly-CSharp.dll` SHA-256
  `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.

The current Rust InputField callback constructs the server command as the
configured CarbonLuau-controlled command, one space and the user value. The
server's `ConsoleSystem.Arg.BuildCommand` then calls `Trim()` on the complete
command and again on `FullString`. Trailing whitespace and whitespace-only
input are therefore irreversibly lost before CarbonLuau can validate the
payload. Reconstructing `Arg.Args` is also prohibited because console argument
parsing reinterprets quotes, backslashes and whitespace.

This evidence applies to the inspected build, not every future Rust release.
TextBox may be reconsidered if the host exposes a bounded opaque UI-input
payload that reaches the plugin without trimming, tokenization or command
reinterpretation. D16's exact preservation contract must not be weakened to
ship the class.

<a id="guifoundation2e-qualification-and-remaining-evidence"></a>

## Qualification and remaining evidence

The final Linux worktree ran in a four-CPU, 8 GiB BigVPS container. All five
release native CTest suites, loader/export checks and the complete Mono
real-native managed suite passed. That suite includes GUI Foundations 1A
through 1G, GUI-2A through GUI-2C and GUI-2E, Foundations A through G, addon
package/parser/provider lifecycle and Phase 0 through Phase 3 regressions. A
separate ASan/UBSan/leak build passed all five native suites with leak detection
and halt-on-error enabled.

DockerPC was unavailable, so Windows live/local native qualification could not
run there. Hosted Windows CI remains the applicable Windows build/runtime gate.
No RustDedicated/Carbon process was available on BigVPS, so live Carbon
qualification was unavailable. These limitations do not replace or invalidate
historical Foundation 1 evidence.

Authenticated-client image rendering, scrolling behavior and actual
ImageButton click receipt remain unqualified. Foundation 2E claims retained
model, lifecycle, serialized projection and controlled ingress behavior only.

<a id="guifoundation2e-identity-and-remaining-scope"></a>

## Identity and remaining scope

Package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider
protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` were unchanged by GUI-2E. At that
phase Foundation 2 had no release/API identity. GUI-2F later assigned the
implemented layout/image/scrolling subset to the existing unreleased
`0.4.0-experimental` identity.

TextBox, Submitted,
focus APIs, alternate text transport, CanvasPosition, AutomaticCanvasSize,
UIGridLayout, URL images, advanced styling and Foundation 3 features were not
implemented.

<!-- END RECORD GuiFoundation2E.md -->

<!-- BEGIN RECORD GuiFoundation2F.md -->

<a id="guifoundation2f"></a>

<a id="guifoundation2f-gui-foundation-2f-public-qualification-and-release-candidate-closure"></a>

# GUI Foundation 2F: public qualification and release-candidate closure

Verdict: **PASS for experimental public release of the implemented Foundation 2
surface**. Authenticated-client observations remain explicitly unqualified and
non-gating under the existing compatibility policy.

Starting commit: `a3f970a395c430410cc3e80edb98726c0e8e8259`.

Foundation 2F adds no production GUI behavior. It closes public documentation,
examples, API compatibility, scale evidence, release contents and identity for
the D16 subset implemented and lifecycle-qualified by GUI-2A, GUI-2B, GUI-2C
and GUI-2E.

<a id="guifoundation2f-public-surface-and-identity"></a>

## Public surface and identity

The experimentally public Foundation 2 surface is:

- `GuiObject.LayoutOrder`, `UIListLayout` and `UIPadding`;
- immutable `ImageSource` values with `None`, `Sprite`, `Png`, `Item` and
  `SteamAvatar` source kinds;
- `ImageLabel`, `ImageButton` and `ImageButton.Activated`; and
- `ScrollingFrame`, `CanvasSize`, `ScrollingDirection` and
  `ScrollingEnabled`.

This additive subset joins the still-unreleased package `0.4.0` and scripting
API `CarbonLuau 0.4.0-experimental`. There is no 0.5.0 bump because no published
0.4 compatibility surface is being superseded. Native ABI `1.4`, provider
protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` are unchanged.

<a id="guifoundation2f-documentation-and-examples"></a>

## Documentation and examples

The [GUI guide](../api/Gui.md) owns author-facing behavior and a scenario-to-example
index. The [GUI reference](../api/Gui-Reference.md) owns exact classes, defaults,
accepted values and hard bounds. [Scripting compatibility](../api/Compatibility.md),
the [release notes](../releases/0.4.0.md), [installation](../Installation.md), the
repository README and changelog identify the experimental surface and its
limits.

The release bundle includes focused runnable examples for:

1. vertical `UIListLayout`;
2. horizontal `UIListLayout`;
3. `UIPadding`;
4. `LayoutOrder` independent of `ZIndex`;
5. sprite and PNG `ImageLabel` values;
6. secure `ImageButton.Activated`;
7. item and skin image sources;
8. Steam avatar image sources;
9. `ScrollingFrame` with explicit `CanvasSize`;
10. layout and padding inside scrolling content;
11. one rich retained tree shared by multiple Players; and
12. cloned retained state for each Player.

No example contains raw CUI, action tokens or client commands.

<a id="guifoundation2f-api-and-compatibility-audit"></a>

## API and compatibility audit

The public reference was checked against `GuiDescriptors.cs`,
`GuiImageSource.cs`, the private bootstrap and the retained/render adapters.
Every documented implemented class, property, method, event, default, enum-like
string and ImageSource constructor matches the runtime descriptor or bootstrap.
The audit preserves these distinctions:

- `LayoutOrder` controls list geometry; `ZIndex` controls render order;
- `GetChildren()` remains retained attachment order;
- list layout never rewrites author-retained `Position`;
- hidden children consume no list space;
- client rendering and asset loading have no acknowledgement contract;
- `CanvasSize`, direction and enabled state are retained, while current scroll
  position, drag and inertia are client-local; and
- `ImageButton.Activated` uses the same exact-Player, Presentation-bound secure
  path as `TextButton.Activated`.

Foundation 1 behavior is unchanged when scripts do not create Foundation 2
objects or properties. Position without layout, coordinates without padding,
`TextButton.Activated`, ZIndex, attachment-order children, Clone/Destroy,
shared-view behavior and D15 publication/replacement/recovery all retain their
qualified behavior.

<a id="guifoundation2f-textbox-deferral-audit"></a>

## TextBox deferral audit

**TextBox is not implemented.** `Submitted` is not implemented. Neither name is
present in production descriptors or bootstrap globals, and no public example
or release note advertises it as usable.

The preserved D16 future design requires exact supported single-line text. On
Rust Dedicated Server app `258550`, build `25353106`, the current InputField
command path trims the command and `FullString` before CarbonLuau can validate
the payload. Trailing whitespace and whitespace-only text are irreversibly
lost, while argument reconstruction would reinterpret whitespace, quotes and
backslashes. CarbonLuau therefore exposes no lossy console-argument substitute.
The design may be reconsidered if a future host provides a bounded opaque
text-preserving input transport.

<a id="guifoundation2f-resource-and-scale-evidence"></a>

## Resource and scale evidence

`GuiFoundation2FTests.cs` builds one representative retained screen with 116
objects: Frame, UIListLayout, UIPadding, text controls, image controls and 27
ScrollingFrames containing layout, padding and image state. Its authoritative
plan charges 254 projected elements against the existing 257-element bound.
No limit was raised.

| Viewers | Projected elements | Estimated bytes | Serialized bytes per plan | Aggregate serialized bytes | Managed memory upper-bound delta |
|---:|---:|---:|---:|---:|---:|
| 1 | 254 | 24,516 | 38,772 | 38,772 | 1,671,512 B |
| 10 | 254 | 24,516 | 38,772 | 387,720 | 1,821,200 B |
| 50 | 254 | 24,516 | 38,772 | 1,938,600 | 9,700,368 B |
| 100 | 254 | 24,516 | 38,772 | 3,877,200 | 19,413,864 B |

The managed figure is the maximum observed conservative process-GC delta across
the recorded Windows and Linux runs. It includes retained in-memory backend
evidence calls and is not a per-Presentation contract. Every scale
case received exactly one initial authoritative plan. A shared image patch plus
scrolling structural reconciliation converged all Presentations with zero dirty
or full-resync backlog. Retirement returned retained objects, registries,
Presentations, actions, player rates and pending work to zero. There was no
unbounded dirty, layout, retry or historical render-version growth.

<a id="guifoundation2f-authenticated-client-and-foundation-g-status"></a>

## Authenticated-client and Foundation G status

No authenticated current Rust client was available. Actual sprite/PNG/item/skin
and avatar appearance, source replacement, tint/transparency, ImageButton click
receipt, vertical/horizontal/XY scrolling, clipping, scrollbars, independent
Player scroll position and rebuild reset behavior remain **UNQUALIFIED**. API
availability does not claim those outcomes.

No RustDedicated/Carbon process was available on BigVPS, and DockerPC was
unavailable, so live Carbon qualification was not rerun for GUI-2F.

Foundation G remains **PARTIAL** solely for its separate Windows live/local
compiler-worker qualification. That gate is **DEFERRED / UNQUALIFIED** for exact
Foundation G source `e3025401c3085f0552bbfe3d045c3143c4ded005`. Hosted Windows
CI is not relabeled as equivalent evidence, and historical Windows qualification
for Foundations A-F remains valid.

<a id="guifoundation2f-release-preparation-and-remaining-scope"></a>

## Release preparation and remaining scope

The deterministic release bundle includes the Foundation 2 examples, GUI guide,
GUI reference, installation notes, release notes, license/attribution,
production package, matching native runtime/compiler worker and generated
checksums/provenance. Preparing these artifacts creates no tag or GitHub Release.

Still unsupported or deferred: `TextBox`, `Submitted`, focus APIs,
`UIGridLayout`, `AutomaticSize`, `AbsoluteContentSize`, `AutomaticCanvasSize`,
`CanvasPosition`, arbitrary URL images, `UIStroke`, `UICorner`, `TextScaled`,
rich text, drag/drop, client geometry and Foundation 3 features.

<a id="guifoundation2f-qualification-matrix"></a>

## Qualification matrix

The release-candidate matrix covers GUI Foundations 1A-G, GUI-2A/2B/2C/2E/2F,
D15/D16 architecture, addon/provider lifecycle, root/addon replacement, VM
recovery, publication/rollback, synchronization/reconciliation, ImageButton
security, layout/projection bounds, scrolling lifecycle, resource teardown,
packaging, public API/docs, deterministic release artifacts, Windows and Linux
runtime tests, plus ASan/UBSan/leak checks. The completion report records the
exact tested revision and hosted CI runs.

The clean BigVPS Linux worker passed all five release native CTests, loader and
export checks, the complete Mono real-native managed suite, every bundled GUI
example, deterministic Linux release artifacts and all five ASan/UBSan/leak
suites. The controlling Windows PC passed managed compilation, every model
suite and deterministic Windows packaging. Its non-worker full native run twice
reached the pre-existing GUI-2E 100-cycle fatal-recovery stress before diverging
late in that stress; no local retry was used as substitute worker evidence.
DockerPC remained unavailable. Hosted Windows is the applicable clean Windows
build/runtime gate for this phase and remains distinct from Foundation G's
deferred Windows live/local Carbon qualification.

GUI Foundation 3 and TextBox implementation were not started.

<!-- END RECORD GuiFoundation2F.md -->

