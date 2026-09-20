# GUI Foundation 2 architecture

Status: **READY FOR IMPLEMENTATION DESIGN / CANONICALLY ADOPTED**.

Baseline reviewed: `f546653224d6597c18b8a2eb7b4e1094b150abc8`, after GUI
Foundation 1G.

[D16](Invariants.md#d16--gui-foundation-2-deterministic-layout-and-rich-controls)
is the canonical policy owner. This document preserves the complete supporting
rationale, public-surface matrices, implementation guidance and qualification
plan. It does not prove implementation, expose a public API, assign a version or
override D15.

## Final scope

Foundation 2 is the practical application-UI slice above Foundation 1:

| Area | Accepted Foundation 2 surface |
|---|---|
| Layout | `UIListLayout`, `UIPadding`, `GuiObject.LayoutOrder` |
| Scrolling | `ScrollingFrame`, `CanvasSize`, `ScrollingDirection`, `ScrollingEnabled` |
| Images | immutable `ImageSource`, `ImageLabel`, `ImageButton` |
| Input | `TextBox`, `PlaceholderText`, `MaxLength`, `TextEditable`, `Submitted` |
| Existing interaction | `ImageButton.Activated` through the D15 action architecture |

The architectural center is that CarbonLuau owns the semantics. Rust CUI is a
projection backend. CarbonLuau computes deterministic layout rather than making
host/Unity layout groups authoritative.

Foundation 2 intentionally excludes the following scope:

| Feature | Foundation 2 disposition | Reason |
|---|---|---|
| `UIGridLayout` | defer | materially larger geometry model |
| `AbsoluteContentSize` | exclude | CarbonLuau has no authoritative client pixels |
| `AutomaticCanvasSize` | defer | introduces client-geometry and circular-sizing cases |
| `CanvasPosition` | defer | client-controlled Presentation state without readback |
| general `ClipsDescendants` | defer | requires a separately qualified clipping contract |
| `UIStroke` | defer | Unity Outline is not an honest semantic equivalent |
| `UICorner` | defer | no clean current host primitive |
| `TextScaled` / `TextBounds` | defer/exclude | requires resolved client text/container geometry |
| rich text / wrapping / public fonts | defer | outside the practical-control slice and needs independent host qualification |
| `FocusLost` | exclude | host reports submission, not Roblox focus-loss reasons |
| `Focused` / capture/focus queries | defer | no authoritative corresponding ingress/state |
| multiline/password TextBox | defer | newline/submission and security semantics need separate work |
| arbitrary URL images | exclude | would grant undeclared remote-origin/network behavior |
| Carbon image-database source | defer | would couple the facade to a Carbon-specific resource subsystem |
| advanced scrollbars | defer | unnecessary for the bounded first scrolling surface |
| drag/drop | later phase | separate interaction architecture |
| client geometry queries | later phase | fundamentally Presentation/client state |
| arbitrary client scripting | exclude | contradicts the server-driven capability boundary |

These dispositions are Foundation 2 boundaries, not permanent rejection from a
future separately designed phase unless explicitly marked as a capability
exclusion.

## Host capability rationale

Current Rust CUI contains horizontal/vertical/grid layout groups, content-size
fitters and layout elements. That establishes host feasibility but not the right
CarbonLuau abstraction: those components resolve geometry in the client Unity
domain. CarbonLuau's accepted list/padding subset is representable from retained
affine `UDim`/`UDim2` state without viewport or text measurement, so server-side
compilation preserves deterministic backend-neutral reconciliation.

Rust CUI also exposes a ScrollView/content transform, but no authoritative
client-to-server current-offset callback. CarbonLuau can therefore project a
scrolling container honestly while omitting `CanvasPosition`.

The Rust input field supports text, a character limit, read-only state,
placeholder and a submission command. That supports a bounded submission event,
not Roblox's richer focus lifecycle. CarbonLuau consequently exposes
`Submitted`, never a counterfeit `FocusLost`.

Rust CUI has distinct sprite, server-known PNG, item/skin and Steam-avatar image
inputs, plus raw URLs. The typed source model preserves the useful host-known
sources while deliberately withholding arbitrary HTTP/network capability.

Supporting host references:

- [Rust CUI structures](https://github.com/OxideMod/Oxide.Rust/blob/develop/src/RustCui.cs)
- [Carbon Lightweight UI](https://carbonmod.gg/devs/features/lightweight-ui)
- [Carbon release notes](https://carbonmod.gg/references/release-notes/)
- [Rust client-command hook](https://docs.oxidemod.com/hooks/player/OnClientCommand)

These references are feasibility evidence, not permanent promises about one
adapter method.

## Exact retained classes

| Class | Kind | Children | Interaction |
|---|---|---:|---|
| `UIListLayout` | retained non-rendering helper | No | None |
| `UIPadding` | retained non-rendering helper | No | None |
| `ScrollingFrame` | `GuiObject` container | Yes | client-local scrolling |
| `ImageLabel` | `GuiObject` | Yes | None |
| `ImageButton` | `GuiObject` | Yes | `Activated(Player)` |
| `TextBox` | `GuiObject` | Yes | `Submitted(Player, Text)` |

No new public abstract hierarchy is required. `UIListLayout` and `UIPadding`
participate in `Name`, `Parent`, `ClassName`, `Clone`, `Destroy`, `GetChildren`,
`FindFirstChild` and `IsA`, but `IsA("GuiObject")` is false, they cannot have
children and they emit no CUI element directly.

## ImageSource

The new immutable, host-lifetime-independent value type has these constructors:

```lua
ImageSource.None()
ImageSource.Sprite(Name)
ImageSource.Png(Id)
ImageSource.Item(ItemId, SkinId?)
ImageSource.SteamAvatar(UserId)
```

It behaves like other immutable GUI values rather than a host-backed resource.
It grants no ownership or access to Rust files, FileStorage, Carbon image
databases, Steam assets, network requests or server filesystem paths.

Source validation is typed:

- `Sprite` is canonical UTF-8, at most 256 bytes, contains no NUL/control
  characters, uses a restricted asset-key character set and is never treated as
  a server path;
- `Png` is an opaque canonical decimal identifier of at most 20 ASCII digits;
- `Item` uses a signed 32-bit item ID and an optional canonical unsigned-decimal
  skin string, avoiding Luau number precision loss for 64-bit IDs; and
- `SteamAvatar` uses a canonical decimal user-ID string consistent with D11.

A syntactically invalid source fails the retained mutation. A syntactically
valid but unavailable client asset does not roll back retained state or produce
a fictitious acknowledgement; it may produce bounded diagnostics.

## Common LayoutOrder property

```text
GuiObject.LayoutOrder : integer
default: 0
range: -32768..32767
```

It has no geometric effect without a sibling `UIListLayout`.

## UIListLayout

| Member | Type | Default | Accepted values/meaning |
|---|---|---|---|
| `Padding` | `UDim` | `UDim.new(0, 0)` | gap between arranged children |
| `FillDirection` | string | `"Vertical"` | `"Vertical"`, `"Horizontal"` |
| `HorizontalAlignment` | string | `"Left"` | `"Left"`, `"Center"`, `"Right"` |
| `VerticalAlignment` | string | `"Top"` | `"Top"`, `"Center"`, `"Bottom"` |

Foundation 2 has no `SortOrder`; it always sorts geometrically by:

```text
LayoutOrder ascending
then retained attachment ordinal
then object identity
```

`ZIndex` remains draw ordering. `GetChildren()` remains attachment ordering.
Changing `LayoutOrder` must never reorder the retained child list.

A parent may contain at most one `UIListLayout`. Creating or reparenting a
second one fails atomically; there is no last-one-wins behavior.

For managed direct `GuiObject` children, list layout ignores retained `Position`
only while computing projected geometry. Reads still return the author value,
and removing the layout restores that retained value as projection authority.
Foundation 2 never rewrites it.

Child `Size` remains authoritative. Foundation 2 adds no flex, fill, automatic,
minimum/maximum or text-derived sizing. Hidden children consume no list space.

List coordinates remain deterministic affine combinations of retained scales
and offsets. Start/center/end alignment, accumulated child sizes and gaps remain
representable without querying client pixels. `AnchorPoint` is retained and
unchanged; projection derives the position needed to place rendered bounds in
the computed slot.

## UIPadding

| Member | Type | Default |
|---|---|---|
| `PaddingTop` | `UDim` | zero |
| `PaddingBottom` | `UDim` | zero |
| `PaddingLeft` | `UDim` | zero |
| `PaddingRight` | `UDim` | zero |

Padding scale is `0..1`, offset is `0..32768`, and negative padding is rejected.
A parent may contain at most one `UIPadding`.

Padding defines a parent content rectangle for direct public children, list
layout and built-in text content such as TextLabel/TextButton/TextBox text and
placeholder. It does not shrink the parent's own background or image graphic.
The compiler composes `UDim` terms symbolically without client-pixel queries.

## ScrollingFrame

`ScrollingFrame` is a normal retained `GuiObject` container.

| Member | Type | Default | Accepted values |
|---|---|---|---|
| `CanvasSize` | `UDim2` | `UDim2.fromScale(1, 1)` | ordinary bounded `UDim2` |
| `ScrollingDirection` | string | `"Y"` | `"X"`, `"Y"`, `"XY"` |
| `ScrollingEnabled` | boolean | `true` | boolean |

Shared retained state includes canvas configuration, ordinary GuiObject state,
children, UIListLayout and UIPadding. Each Presentation/client privately owns
its current scroll offset, scrollbar drag state, inertia and gesture state.

There is no scroll-position property/event. A full rebuild, Hide/Show,
disconnect, domain replacement or fatal recovery may reset client-local scroll
position. CarbonLuau neither fabricates nor reports current client state.

List layout under a ScrollingFrame lays out scrolling content like any other
container. `CanvasSize` stays explicit; it is not inferred from children.

## ImageLabel and ImageButton

| Member | Type | Default |
|---|---|---|
| `Image` | `ImageSource` | `ImageSource.None()` |
| `ImageColor3` | `Color3` | white |
| `ImageTransparency` | number `0..1` | `0` |

`ImageButton` additionally exposes `Activated(Player)` through the existing
Signal type and D15/GUI-1E delivery semantics. Backend projection may use
separate image and interaction elements; that remains hidden by the render IR
and counts toward bounded projection cost.

## TextBox

TextBox reuses the existing `Text`, `TextColor3`, `TextTransparency`, `TextSize`,
`TextXAlignment` and `TextYAlignment` properties and adds:

| Member | Type | Default |
|---|---|---|
| `PlaceholderText` | string | `""` |
| `MaxLength` | integer `1..256` | `256` |
| `TextEditable` | boolean | `true` |
| `Submitted` | Signal | none |

The callback signature is `function(Player, Text)`. TextBox is single-line.

### Retained text versus client draft

`TextBox.Text` is shared retained server state. A player's current draft,
focus, cursor and selection remain Presentation-local and cannot implicitly
mutate it. For a shared tree, another Player does not observe a draft. On submit,
the callback receives the submitting exact Player and immutable draft payload,
while retained `Text` stays unchanged unless script assigns it explicitly.

Authors use `Clone()` when independently retained per-player state is required.
Assigning `TextBox.Text = Text` in a submission callback intentionally publishes
that value to every viewer of the shared tree.

### Submission instead of FocusLost

The host supplies submission, not authoritative focus-loss reasons or equivalent
client input metadata. `Submitted(Player, Text)` is therefore the honest public
contract. Foundation 2 does not add `FocusLost`, `Focused`, focus capture/query,
multiline or password behavior.

## Retained versus Presentation-local authority

| State | Authority |
|---|---|
| tree/hierarchy | retained shared |
| `LayoutOrder` and helper properties | retained shared |
| derived layout position | deterministic projection |
| `CanvasSize`, direction, enabled | retained shared |
| actual scroll offset/inertia/gesture | Presentation-local/client-only |
| image source | retained shared |
| image cache/download state | client/host-only |
| `TextBox.Text` | retained shared |
| placeholder/max length/editability | retained shared |
| typed draft/focus/cursor/selection | Presentation-local/client-only |
| submitted text | immutable event payload |
| action authority | Presentation-specific |

The governing rule is that client-controlled transient state never silently
enters the retained shared tree.

## Typed private input ingress

Foundation 2 generalizes the existing D15 action record to distinguish:

```text
Activated
TextSubmitted
```

It does not add a second command family. The existing private GUI ingress
remains the sole client path. The immutable admitted record carries action and
owner identity, Presentation epoch, exact Player connection, VM generation,
owner domain, ScreenGui, target, action kind, validated payload and listener
registration.

The submission path is:

```text
Rust InputField
  -> fixed private CarbonLuau GUI command and opaque token
  -> exact Player/connection validation
  -> typed TextSubmitted lookup
  -> UTF-8/scalar/schema validation
  -> rate, queue-slot and payload-byte admission
  -> owner-thread scheduler
  -> pre-entry lifetime/kind/editability/listener revalidation
  -> Submitted(Player, Text)
```

Authority is bound to VM generation, owner domain lifetime, ScreenGui, TextBox,
exact Player connection lifetime, Presentation epoch and action kind. Tokens are
not transferable between Players, controls, epochs or action kinds. Hide,
disconnect, destruction, invalidating reparent, replacement and VM retirement
retain D15 stale-authority behavior.

Luau supplies no command, token, target ID or Presentation ID. Client text is
untrusted data and is never executed, concatenated into another command,
reinterpreted as a command name or sent through generic console execution.
Dispatch validates/admit-only and never synchronously enters Luau.

Fanout reserves all listener queue slots and text-payload accounting atomically.
Where practical, immutable payload bytes are stored once per admitted action,
not copied for every listener.

## Text preservation qualification gate

CarbonLuau's public contract requires exact preservation of supported
single-line text through the authenticated current Rust/Carbon transport. Tests
must cover:

- spaces, leading/trailing whitespace and repeated whitespace;
- quotes and backslashes;
- empty text;
- BMP and supplementary Unicode; and
- maximum scalar and UTF-8 byte boundaries.

If the current `InputField` to managed-command path cannot establish that
contract, TextBox is deferred from Foundation 2. It must not be weakened to
console-command argument semantics, silently trim/collapse whitespace or
normalize input merely to ship the class. This is a qualification gate, not an
open architecture decision.

## Input validation and admission bounds

Server validation is authoritative; the client character limit is UX only.
Reject before Luau entry malformed encoding/scalars, oversized bytes/scalars,
stale/wrong-kind tokens, wrong Player or connection, dead owner/target, hidden
or unavailable target, disabled editing and over-limit input.

The hard initial envelope is:

```text
TextBox.MaxLength        1..256 Unicode scalars
Submitted text           <= configured MaxLength scalars
Submitted UTF-8          <= 1,024 bytes
Raw input command tail   <= 1,536 UTF-8 bytes before semantic parsing
Queued text payload      <= 64 KiB/domain, 256 KiB/global
```

Retain the existing global per-Player GUI interaction bucket. An initial
input-specific target is 4 accepted submissions/sec/token with burst 4. That
rate is a qualification/tuning target, not a permanent public timing guarantee.

## Synchronization classifications

Foundation 2 adds one internal `layout-affecting` classification that synthesizes
ordinary projected-rectangle changes. It is not a new consistency model.

| Mutation | Initial classification |
|---|---|
| `LayoutOrder` without list parent | metadata-only |
| `LayoutOrder` under list | layout-affecting |
| UIListLayout/UIPadding property | layout-affecting |
| managed child `Size` | patchable plus layout-affecting |
| managed child visibility | D15 visibility/token rules plus layout-affecting |
| create/destroy/reparent helper | structural/full rebuild |
| CanvasSize/direction/enabled | structural/full rebuild initially |
| image color/transparency | patchable |
| Image source | structural/full rebuild initially |
| retained TextBox Text | patchable |
| placeholder/editability/MaxLength | patchable where backend permits |
| connect/disconnect Submitted or ImageButton Activated | structural/full rebuild |
| actual scroll state or TextBox draft/focus | Presentation-local/untracked |

Only the layout parent's relevant direct arranged children are recomputed. A
layout mutation never recursively marks arbitrary descendants. The existing 64
direct-child bound caps one recomputation. There is no historical work queue;
latest retained state wins. Detailed-dirty overflow collapses to existing D15
whole-presentation reconciliation.

Scroll configuration can remain structural initially because ScrollView has
client-local state. Later patch optimization is permitted only when public
semantics remain unchanged.

## Ownership and publication

D15 applies directly:

- layout-helper ownership comes from the creating GUI facade and never creates
  a second ownership domain for arranged children;
- a foreign domain may use a shared helper reference while the original owner
  remains valid, but ownership does not transfer;
- ImageSource is lifetime-independent and owns no external host resource;
- Submitted connections follow TextBox resource ownership;
- input authority is Presentation-specific; and
- local scroll state disappears with its Presentation and is not a D7 resource.

Every retained Foundation 2 creation, property mutation, Signal connection and
presentation intent participates in the existing GUI publication journal.
Provisional image assignment, text connection, Show or layout mutation produces
no client effect, interaction authority or geometry change before commit.

The foreign-owner rule remains:

```text
ResourceOwner      = original GUI owner
PublicationContext = currently admitted provisional operation
```

Commit revalidates the original owner. Failure discards staged retained changes
without attempting rollback of ordinary Luau memory.

## Resource bounds and projection cost

Existing Foundation 1 limits remain the base. Foundation 2 adds:

| Resource | Initial hard safety bound |
|---|---:|
| UIListLayout per parent | 1 |
| UIPadding per parent | 1 |
| arranged children per layout | existing 64-child bound |
| TextBox MaxLength | 256 Unicode scalars |
| submitted UTF-8 text | 1,024 bytes |
| raw command tail | 1,536 UTF-8 bytes |
| sprite source | 256 UTF-8 bytes |
| PNG/Steam/skin identifiers | 20 ASCII digits |
| queued text payload/domain | 64 KiB |
| queued text payload/global | 256 KiB |
| synthesized dirty detail | existing 512/domain envelope |

Helpers count toward ordinary object limits. Interactive controls remain under
existing presentation-token limits.

One retained object may project to several client elements: a TextBox can need a
container/input/placeholder and an ImageButton can need image plus interaction
elements. Each class therefore needs a bounded internal projection cost. Reject
a ScreenGui configuration whose complete authoritative projection cannot fit
configured full-presentation limits. Do not merely raise serializer limits.

Exact scheduling, flush and rate values remain implementation tuning unless
deliberately documented and qualified as public bounds.

## Foundation 1 compatibility

Foundation 2 is additive. Existing scripts behave identically unless they use a
new object or property:

- Position remains projection authority without UIListLayout;
- child coordinates remain unchanged without UIPadding;
- TextButton.Activated and its security semantics remain unchanged;
- ZIndex remains rendering order and GetChildren remains attachment order;
- Clone/Destroy learn the new classes/properties without changing their model;
- D15 publication, replacement, recovery and failure behavior remain intact;
  and
- one retained tree may still have several independent Presentations.

No D15 rewrite or new ownership model is required.

## Differences from Roblox

Foundation 2 uses Roblox-familiar names without claiming DataModel or pixel-level
compatibility:

| Roblox behavior/surface | CarbonLuau Foundation 2 |
|---|---|
| full Instance/DataModel hierarchy | retained GUI-specific objects only |
| broad enum system | bounded canonical strings for small selections |
| `UIListLayout.SortOrder` | always `LayoutOrder` |
| `AbsoluteContentSize`, rich/flex layout | absent |
| `AutomaticSize` / `AutomaticCanvasSize` | absent |
| readable/writable `CanvasPosition` | absent |
| client-known scroll state | opaque Presentation-local state |
| local typing mutates TextBox.Text | draft never mutates shared retained Text |
| `FocusLost` | `Submitted(Player, Text)` |
| focus/cursor/selection APIs | absent |
| arbitrary ContentId/web image references | bounded typed `ImageSource`; no arbitrary URL |
| `UIStroke`, `UICorner`, `TextScaled` | absent |
| full font surface | fixed backend font for Foundation 2 |
| client pixel geometry | unavailable |

These are deliberate semantic boundaries, not incomplete attempts at exact
Roblox compatibility.

## Author-facing examples

These examples describe the eventual surface and are not runnable until their
own implementation phases complete.

### List and padding

```lua
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(420, 500)

local Padding = Panel:Create("UIPadding")
Padding.PaddingTop = UDim.new(0, 12)
Padding.PaddingBottom = UDim.new(0, 12)
Padding.PaddingLeft = UDim.new(0, 12)
Padding.PaddingRight = UDim.new(0, 12)

local Layout = Panel:Create("UIListLayout")
Layout.FillDirection = "Vertical"
Layout.Padding = UDim.new(0, 8)
Layout.HorizontalAlignment = "Center"
Layout.VerticalAlignment = "Top"

local First = Panel:Create("TextButton")
First.LayoutOrder = 10
First.Size = UDim2.new(1, 0, 0, 40)
```

### Scrolling

```lua
local Scroll = Screen:Create("ScrollingFrame")
Scroll.Size = UDim2.fromScale(1, 1)
Scroll.CanvasSize = UDim2.new(1, 0, 0, 1200)
Scroll.ScrollingDirection = "Y"
Scroll.ScrollingEnabled = true

local Layout = Scroll:Create("UIListLayout")
Layout.Padding = UDim.new(0, 6)
```

There is intentionally no `Scroll.CanvasPosition`.

### Images

```lua
local Icon = Panel:Create("ImageLabel")
Icon.Image = ImageSource.Sprite("assets/icons/info.png")
Icon.ImageColor3 = Color3.fromRGB(255, 255, 255)

local Item = Panel:Create("ImageButton")
Item.Image = ImageSource.Item(-151838493, "0")
Item.Activated:Connect(function(Player)
    print(Player.Name .. " selected the item")
end)
```

### Text input

```lua
local Input = Panel:Create("TextBox")
Input.PlaceholderText = "Enter amount"
Input.MaxLength = 32

Input.Submitted:Connect(function(Player, Text)
    -- Text is this Presentation's submitted draft.
    -- Input.Text is still shared retained state.
    Input.Text = Text
end)
```

## Implementation sequence

### GUI Foundation 2A: schema and deterministic layout

Implement descriptors, LayoutOrder, UIListLayout, UIPadding, server layout
compilation, mock-backend golden cases and publication/Clone/Destroy/reparent
behavior. Do not add ScrollView, images or input.

### GUI Foundation 2B: images

Implement ImageSource, backend-neutral image descriptors, ImageLabel,
ImageButton, ImageButton.Activated and projection-cost accounting.

### GUI Foundation 2C: scrolling

Implement ScrollingFrame, content projection, inherent clipping, list layout in
scroll content and structural reconciliation. Do not add CanvasPosition.

### GUI Foundation 2D: TextBox and typed ingress

Only after ordinary rendering is stable, generalize action records, add bounded
payload/queue-byte accounting, TextBox projection, placeholder and Submitted,
and run adversarial security tests. Apply the text-preservation gate.

### GUI Foundation 2E: lifecycle and shared-view closure

Qualify root/addon replacement, foreign-domain references, provisional changes,
failure rollback, disconnect/reconnect, backend uncertainty, fatal recovery and
multi-viewer transient-state separation.

### GUI Foundation 2F: public qualification

Add public guide/reference, runnable examples, current-host and authenticated
client checks, and the final compatibility audit. Version assignment still
requires separate release planning.

## Qualification plan

### Layout

Cover vertical/horizontal lists; all alignment combinations; mixed scale/offset;
padding; hidden children; duplicate LayoutOrder; order/Size/visibility mutation;
add/remove/reparent; removing layout; LayoutOrder independent of ZIndex; nested
layouts; 64-child worst case; dirty overflow; publication rollback; Clone and
Destroy.

### Scrolling

With a current authenticated client, cover X/Y/XY scrolling, clipping,
scrollbars, layout inside scroll content, two Players independently scrolling
one shared ScreenGui, retained mutation not adopting either offset, CanvasSize
replacement, Hide/Show and documented reset after full rebuild.

### Images

Cover each accepted source kind, invalid syntax, missing assets, source
replacement, shared Presentations and ImageButton click. Do not test arbitrary
HTTP because it is not a capability.

### TextBox

Authenticated-client cases include ASCII, all required whitespace forms,
quotes, backslashes, BMP/supplementary Unicode, empty and boundary text,
malformed/oversized input, disabled editing, placeholder, simultaneous viewers,
retained Text versus local drafts, server Text assignment, rebuild during/after
entry, disconnect/reconnect, stale epochs and owner replacement.

Adversarial fixtures include large repeated sets of forged, stale,
cross-Player, oversized and rate-limited submissions. Queue slots, queued text
bytes and registry sizes must remain bounded.

### Lifecycle and regressions

Repeat Foundation 1 style replacement, failed-candidate, recovery, Show/Hide,
disconnect/reconnect, Clone/Destroy, backend failure and queued-work teardown
stress with Foundation 2 objects. All state returns to baseline. Re-run affected
Foundations A-G, addon, Windows/Linux, sanitizer, package and architecture/API
checks appropriate to each implementation phase.

## Version status

Architecture adoption assigns no package or scripting API identity. Current
implemented identities remain package `0.4.0`, scripting API
`0.4.0-experimental`, native ABI `1.4`, provider protocol `1.2`, package schema
`1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830`.

Foundation 2 release identity remains gated on implementation, qualification
and later explicit release planning. This record does not begin GUI-2A.
