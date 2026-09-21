# GUI reference

Availability: experimental API `0.4.0-experimental`.

Foundation 2 deterministic layout, typed images and retained scrolling are
included in package `0.4.0` and API `0.4.0-experimental` after Foundation 2F
qualification. `TextBox` is not implemented and `Submitted` is not implemented
because the current Rust command transport failed D16's exact text-preservation
gate.

Foundation 3A's `UIGridLayout` is implemented in current source for
qualification. It has not yet been assigned to a package or scripting API
release identity; do not infer its availability from published `0.4.0` artifacts.
Foundation 3B's `Frame.ClipsDescendants` is also implemented in current source,
but its mandatory authenticated-client visual and hit-region qualification is
still pending. It is not part of a qualified public release surface.
Foundation 3C's `GuiFont` and retained TextLabel/TextButton `Font` are
implemented in current source. Actual rendering of all four fonts remains
client-unqualified, and this slice has no assigned release identity.

## Service and construction

```lua
local Gui = game:GetService("Gui")
local Screen = Gui:Create("ScreenGui")
local Frame = Screen:Create("Frame")
```

`Gui:Create(ClassName)` accepts `ScreenGui`, `Frame`, `TextLabel`, `TextButton`,
`ImageLabel`, `ImageButton` and `ScrollingFrame`. `ScreenGui` roots can only be created through
`Gui`. Calling `Create` on a GuiObject accepts those non-screen GuiObjects plus
`UIListLayout`, `UIGridLayout` or `UIPadding` and parents the new child to the receiver.

## Classes

All concrete classes expose:

| Member | Type | Behavior |
|---|---|---|
| `Name` | string | Writable UTF-8 label; defaults to the concrete class name. Duplicate sibling names are allowed. |
| `ClassName` | read-only string | Concrete class name. |
| `Create(ClassName)` | method | Creates a supported child owned by the receiver's domain. |
| `Clone()` | method | Deep detached clone with no viewers, pending work or Signal connections. |
| `Destroy()` | method | Recursively destroys; a repeated call is a no-op. |
| `GetChildren()` | method | New array in retained attachment order. |
| `FindFirstChild(Name)` | method | First direct child with that name, or `nil`; not recursive. |
| `IsA(ClassName)` | method | Tests `GuiNode`, the concrete class and, for non-screen objects, `GuiObject`. |

### ScreenGui

`ScreenGui.Parent` is always `nil` and read-only. A ScreenGui is a `GuiNode`,
not a `GuiObject`.

| Method | Result |
|---|---|
| `Show(Player)` | Adds desired visibility for the exact current connection; idempotent. |
| `Hide(Player)` | Removes desired visibility and invalidates its current actions; idempotent. |
| `IsShown(Player)` | `true` when that exact connection is in the desired presentation set. |

These methods describe server intent, not acknowledged client state.

### GuiObject properties

`Frame`, text controls, image controls and `ScrollingFrame` are GuiObjects and share:

| Property | Type | Default | Validation |
|---|---|---|---|
| `Parent` | GuiNode or nil | `nil` | Same-owner ScreenGui or GuiObject; no cycle. |
| `Position` | UDim2 | `UDim2.new(0, 0, 0, 0)` | Finite component ranges below. |
| `Size` | UDim2 | Class-specific | Finite component ranges below. |
| `AnchorPoint` | Vector2 | `Vector2.new(0, 0)` | Each component `0..1`. |
| `Visible` | boolean | `true` | Exact boolean. |
| `BackgroundColor3` | Color3 | `Color3.new(1, 1, 1)` | Each component `0..1`. |
| `BackgroundTransparency` | number | Class-specific | Finite `0..1`; `0` is opaque. |
| `ZIndex` | integer | `1` | `0..1000`; sibling-local render order. |
| `LayoutOrder` | integer | `0` | `-32768..32767`; geometric order under a sibling UIListLayout or UIGridLayout. |

Class-specific defaults:

| Class | Size | BackgroundTransparency | Children |
|---|---|---:|---|
| `Frame` | `UDim2.fromOffset(100, 100)` | 0 | Yes |
| `TextLabel` | `UDim2.fromOffset(100, 30)` | 1 | Yes |
| `TextButton` | `UDim2.fromOffset(100, 36)` | 0 | Yes |
| `ImageLabel` | `UDim2.fromOffset(100, 100)` | 1 | Yes |
| `ImageButton` | `UDim2.fromOffset(100, 100)` | 1 | Yes |
| `ScrollingFrame` | `UDim2.fromOffset(100, 100)` | 0 | Yes |

`Frame` additionally exposes:

| Property | Type | Default | Behavior |
|---|---|---|---|
| `ClipsDescendants` | boolean | `false` | Rectangular descendant clipping through private projection state. Implemented in current source, but authenticated-client visual and hit behavior remains unqualified. |

The property is not available on other GuiObject classes. It does not modify
retained child geometry or add a script-visible child. Explicit Frame clips and
private ScrollingFrame viewport clips share an effective nesting limit of four.
Each explicit Frame clip consumes one of the unchanged 257 projected elements.

### TextLabel and TextButton

| Property | Type | Default | Validation |
|---|---|---|---|
| `Text` | string | `""` | At most 2,048 UTF-8 bytes and within the screen aggregate bound. |
| `TextColor3` | Color3 | `Color3.new(0, 0, 0)` | Each component `0..1`. |
| `TextTransparency` | number | `0` | Finite `0..1`. |
| `TextSize` | integer | `14` | `1..128`. |
| `TextXAlignment` | string | `"Center"` | `"Left"`, `"Center"` or `"Right"`. |
| `TextYAlignment` | string | `"Center"` | `"Top"`, `"Center"` or `"Bottom"`. |
| `Font` | GuiFont | `GuiFont.RobotoCondensedRegular` | Exactly one of the four immutable GuiFont members. Implemented in current source; authenticated-client rendering remains unqualified. |

`TextButton` additionally exposes:

```lua
Button.Activated:Connect(function(Player)
    -- Player is the exact accepted connection.
end)
```

`Activated` uses the standard [Connection](Types/Connection.md) lifecycle but
has GUI-specific listener and admission bounds. It is not `MouseButton1Click`
and exposes no raw client command.

### ImageLabel and ImageButton

| Property | Type | Default | Validation |
|---|---|---|---|
| `Image` | ImageSource | `ImageSource.None()` | One of the typed constructors below. Source replacement is structural. |
| `ImageColor3` | Color3 | `Color3.new(1, 1, 1)` | Each component `0..1`; patchable. |
| `ImageTransparency` | number | `0` | Finite `0..1`; patchable. |

`ImageButton` also exposes the same `Activated(Player)` Signal and private
exact-connection action path as TextButton. A shared image object has one
retained source for every viewer. Client asset availability is not acknowledged.

### ScrollingFrame

| Property | Type | Default | Validation/behavior |
|---|---|---|---|
| `CanvasSize` | UDim2 | `UDim2.fromScale(1, 1)` | Explicit retained content size; finite component ranges below. |
| `ScrollingDirection` | string | `"Y"` | `"X"`, `"Y"` or `"XY"`; controls enabled scroll axes. |
| `ScrollingEnabled` | boolean | `true` | Exact boolean; disabled state retains the canvas and children. |

Children are projected beneath a private clipped content root. CarbonLuau
retains the canvas configuration but not the current client scroll position,
drag state or inertia. That state belongs separately to each Presentation and
may reset after full reconciliation, Hide/Show, reconnect or replacement.
`CanvasPosition` and `AutomaticCanvasSize` are not exposed.

`UIListLayout`, `UIPadding`, text controls and image controls compose normally
inside a ScrollingFrame. Scripts set `CanvasSize` explicitly; CarbonLuau does
not infer it from layout or client measurements. A ScrollingFrame is charged as
seven projected elements against the 257-element authoritative screen bound,
covering its host-created viewport, content root and possible scrollbar nodes.

### UIListLayout

`UIListLayout` is a retained, non-rendering `GuiNode`, not a `GuiObject`. It can
only be created under a GuiObject, cannot have children and is limited to one
per parent.

| Property | Type | Default | Validation/behavior |
|---|---|---|---|
| `Parent` | GuiObject or nil | `nil` | Same owner, no cycle, one UIListLayout per parent. |
| `Padding` | UDim | `UDim.new(0, 0)` | Gap between arranged visible children. |
| `FillDirection` | string | `"Vertical"` | `"Vertical"` or `"Horizontal"`. |
| `HorizontalAlignment` | string | `"Left"` | `"Left"`, `"Center"` or `"Right"`. |
| `VerticalAlignment` | string | `"Top"` | `"Top"`, `"Center"` or `"Bottom"`. |

Direct visible GuiObject children are arranged by `LayoutOrder`, retained
attachment order and object identity. Hidden children consume no list space.
Retained `Size` and `AnchorPoint` participate in projection; retained `Position`
is preserved but ignored for arranged placement until the layout is removed.

### UIGridLayout

`UIGridLayout` is a retained, non-rendering `GuiNode`, not a `GuiObject`. It can
only be created under a GuiObject and cannot have children. A parent may contain
one `UIListLayout` or one `UIGridLayout`, never both. One `UIPadding` may coexist
with either layout manager.

| Property | Type | Default | Validation/behavior |
|---|---|---|---|
| `Parent` | GuiObject or nil | `nil` | Same owner, no cycle, and no sibling list/grid manager. |
| `CellSize` | UDim2 | `UDim2.fromOffset(100, 100)` | Each scale component `0..8`; each offset component `0..32768`. |
| `CellPadding` | UDim2 | `UDim2.fromOffset(0, 0)` | Each scale component `0..8`; each offset component `0..32768`. |
| `FillDirection` | string | `"Horizontal"` | `"Horizontal"` or `"Vertical"`. |
| `FillDirectionMaxCells` | integer | `1` | `1..64`; explicit columns for horizontal fill or rows for vertical fill before wrapping. |
| `HorizontalAlignment` | string | `"Left"` | `"Left"`, `"Center"` or `"Right"`. |
| `VerticalAlignment` | string | `"Top"` | `"Top"`, `"Center"` or `"Bottom"`. |

The grid arranges direct visible GuiObject children by `LayoutOrder`, retained
attachment order and object identity. Hidden children consume no cell. The
explicit max-cell property defines topology; CarbonLuau never derives wrapping
from client pixels. `ZIndex` and `GetChildren()` behavior remain independent.

While a child is grid managed, its cell controls both projected position and
projected size. The child's retained `Position` and `Size` remain immediately
readable and writable but are not rewritten and do not control that projected
rectangle. `AnchorPoint` participates only in encoding the computed rectangle.
Removing the grid restores the latest retained Position and Size as projection
authority. Grid helpers emit no render element or host layout-group component.

### UIPadding

`UIPadding` is also a retained, non-rendering `GuiNode`, cannot have children and
is limited to one per GuiObject parent.

| Property | Type | Default | Validation |
|---|---|---|---|
| `Parent` | GuiObject or nil | `nil` | Same owner, no cycle, one UIPadding per parent. |
| `PaddingTop` | UDim | zero | Scale `0..1`, offset `0..32768`. |
| `PaddingBottom` | UDim | zero | Scale `0..1`, offset `0..32768`. |
| `PaddingLeft` | UDim | zero | Scale `0..1`, offset `0..32768`. |
| `PaddingRight` | UDim | zero | Scale `0..1`, offset `0..32768`. |

Padding affects the content rectangle used for direct child projection, list or
grid layout and built-in text. It does not resize the parent's background.

## Immutable value types

Values are immutable userdata with read-only fields and component equality.
NaN, infinity and out-of-range values raise ordinary errors. Negative zero is
normalized to zero.

| Type | Constructors | Fields and ranges |
|---|---|---|
| `UDim` | `UDim.new(Scale, Offset)` | `Scale -8..8`; `Offset -32768..32768` |
| `UDim2` | `UDim2.new(XScale, XOffset, YScale, YOffset)`; `UDim2.fromScale(XScale, YScale)`; `UDim2.fromOffset(XOffset, YOffset)` | Read-only `X` and `Y` UDim values |
| `Vector2` | `Vector2.new(X, Y)` | `X`, `Y` each `-32768..32768`; AnchorPoint narrows this to `0..1` |
| `Color3` | `Color3.new(R, G, B)`; `Color3.fromRGB(R, G, B)` | Normalized fields `0..1`; `fromRGB` requires integer `0..255` components |
| `ImageSource` | `ImageSource.None()`; `ImageSource.Sprite(Name)`; `ImageSource.Png(Id)`; `ImageSource.Item(ItemId, SkinId?)`; `ImageSource.SteamAvatar(UserId)` | Read-only `Kind` and source-specific fields; Sprite is a canonical asset key, Png an opaque decimal string, ItemId signed int32, SkinId/UserId unsigned decimal strings |
| `GuiFont` | No constructor | `GuiFont.RobotoCondensedRegular`, `GuiFont.RobotoCondensedBold`, `GuiFont.DroidSansMono`, `GuiFont.PermanentMarker` |

The value constructors are globals in every entrypoint and module environment.
They do not carry a host/domain lifetime and may be shared safely between
domains.

GuiFont members are singleton-like canonical identities. They expose no host
font filename, arbitrary string/path conversion or filesystem capability.
Equality compares canonical identity. Their private Carbon font mapping is an
adapter detail; current-client visual availability is not yet qualified.

ImageSource grants no filesystem, FileStorage, Carbon image-database, URL or
network capability. Sprite names are 1..256 bytes and restricted to canonical
asset-key characters. Decimal identifiers contain at most 20 ASCII digits;
skin and Steam IDs must also fit unsigned 64-bit range. Omitted Item SkinId
reads as the empty string. A syntactically valid but unavailable asset remains
retained state and receives no client-load acknowledgement.

## Deferred text input

`TextBox`, `Submitted`, `FocusLost`, focus APIs and typed text ingress do not
exist in this release. The canonical future design requires exact supported
single-line text preservation. The current host command transport trims input,
so CarbonLuau does not expose partial or console-argument-based behavior.
