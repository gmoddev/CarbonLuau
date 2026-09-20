# GUI reference

Availability: experimental API `0.4.0-experimental`.

## Service and construction

```lua
local Gui = game:GetService("Gui")
local Screen = Gui:Create("ScreenGui")
local Frame = Screen:Create("Frame")
```

`Gui:Create(ClassName)` accepts `ScreenGui`, `Frame`, `TextLabel` and
`TextButton`. `ScreenGui` roots can only be created through `Gui`. Calling
`Create` on a retained object accepts `Frame`, `TextLabel` or `TextButton` and
parents the new child to the receiver.

## Classes

All four concrete classes expose:

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

`Frame`, `TextLabel` and `TextButton` are GuiObjects and share:

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

Class-specific defaults:

| Class | Size | BackgroundTransparency | Children |
|---|---|---:|---|
| `Frame` | `UDim2.fromOffset(100, 100)` | 0 | Yes |
| `TextLabel` | `UDim2.fromOffset(100, 30)` | 1 | Yes |
| `TextButton` | `UDim2.fromOffset(100, 36)` | 0 | Yes |

### TextLabel and TextButton

| Property | Type | Default | Validation |
|---|---|---|---|
| `Text` | string | `""` | At most 2,048 UTF-8 bytes and within the screen aggregate bound. |
| `TextColor3` | Color3 | `Color3.new(0, 0, 0)` | Each component `0..1`. |
| `TextTransparency` | number | `0` | Finite `0..1`. |
| `TextSize` | integer | `14` | `1..128`. |
| `TextXAlignment` | string | `"Center"` | `"Left"`, `"Center"` or `"Right"`. |
| `TextYAlignment` | string | `"Center"` | `"Top"`, `"Center"` or `"Bottom"`. |

`TextButton` additionally exposes:

```lua
Button.Activated:Connect(function(Player)
    -- Player is the exact accepted connection.
end)
```

`Activated` uses the standard [Connection](Types/Connection.md) lifecycle but
has GUI-specific listener and admission bounds. It is not `MouseButton1Click`
and exposes no raw client command.

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

The value constructors are globals in every entrypoint and module environment.
They do not carry a host/domain lifetime and may be shared safely between
domains.
