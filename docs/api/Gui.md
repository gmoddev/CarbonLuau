# GUI

Availability: experimental API `0.4.0-experimental`.

Source status: the additive Foundation 2A layout, Foundation 2B typed-image and
Foundation 2C scrolling surfaces below are implemented and have completed
Foundation 2E lifecycle qualification, but Foundation 2 has not received a
release/API identity yet. `TextBox` and `Submitted` are deferred and not
implemented because the current Rust command transport cannot preserve the
required submitted text exactly.

CarbonLuau provides a small server-driven retained GUI API. You create a tree
once, show its `ScreenGui` to one or more connected Players, and then update the
same objects. CarbonLuau synchronizes committed changes after Luau returns.

## Quick start

```lua
local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Button = Screen:Create("TextButton")
Button.Position = UDim2.fromScale(0.5, 0.5)
Button.AnchorPoint = Vector2.new(0.5, 0.5)
Button.Size = UDim2.fromOffset(220, 44)
Button.Text = "Click"
Button.BackgroundColor3 = Color3.fromRGB(45, 120, 210)
Button.TextColor3 = Color3.fromRGB(255, 255, 255)

local Padding = Button:Create("UIPadding")
Padding.PaddingLeft = UDim.new(0, 12)
Padding.PaddingRight = UDim.new(0, 12)

Button.Activated:Connect(function(Player)
    Button.Text = Player.Name
end)

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Screen:Show(Player)
end)
```

`Activated` receives the exact connected [Player](Types/Player.md) that
submitted the accepted action. The shared button text changes for every viewer
because they are all viewing the same retained tree.

## One tree or one tree per Player

Calling `Screen:Show(Player)` for several Players creates several views of one
live tree. A later property change is reflected in every current view. Showing a
screen does not clone it.

Use `Clone()` when each Player needs independent retained state:

```lua
local Template = Gui:Create("ScreenGui")
local Label = Template:Create("TextLabel")
Label.Name = "Greeting"

local Screen = Template:Clone()
Screen:FindFirstChild("Greeting").Text = `Hello {Player.Name}`
Screen:Show(Player)
```

A clone receives new object identities and copies the subtree's public
properties and child order. It does not copy viewers, action state, pending
updates or Signal connections.

## Deterministic lists and padding

`UIListLayout` arranges the direct visible GuiObject children of its parent.
Geometry is computed by CarbonLuau from retained `UDim` and `UDim2` values; it
does not use a Unity layout group or client viewport measurement.

```lua
local Panel = Screen:Create("Frame")
Panel.Size = UDim2.fromOffset(320, 240)

local Padding = Panel:Create("UIPadding")
Padding.PaddingTop = UDim.new(0, 12)
Padding.PaddingBottom = UDim.new(0, 12)
Padding.PaddingLeft = UDim.new(0, 16)
Padding.PaddingRight = UDim.new(0, 16)

local Layout = Panel:Create("UIListLayout")
Layout.FillDirection = "Vertical"
Layout.Padding = UDim.new(0, 8)
Layout.HorizontalAlignment = "Center"
Layout.VerticalAlignment = "Top"

local First = Panel:Create("TextLabel")
First.LayoutOrder = 10
local Second = Panel:Create("TextButton")
Second.LayoutOrder = 20
```

Geometric order is `LayoutOrder`, then attachment order, then object identity.
`ZIndex` independently controls render order, and `GetChildren()` still returns
attachment order. A hidden child consumes no list space. Child `Size` remains
authoritative; no automatic or text-derived sizing is performed.

List layout never rewrites retained `Position`. Reading it returns the author's
value, and removing the `UIListLayout` restores that value as projection
authority. `UIPadding` defines the content rectangle for direct children, list
layout and built-in label/button text. It does not shrink the parent's own
background. Each parent may contain at most one helper of each kind.

## Typed images

Choose an explicit host-known image source and assign it to an ImageLabel or
ImageButton. Values are immutable, comparable and safe to retain or share
between domains.

```lua
local Icon = Screen:Create("ImageLabel")
Icon.Size = UDim2.fromOffset(64, 64)
Icon.Image = ImageSource.Sprite("assets/icons/info.png")
Icon.ImageColor3 = Color3.fromRGB(255, 255, 255)

local Item = Screen:Create("ImageButton")
Item.Image = ImageSource.Item(-932201673, "12345678901234567890")
Item.Activated:Connect(function(Player)
    print(`{Player.Name} selected the item`)
end)
```

Other accepted constructors are `ImageSource.None()`, `ImageSource.Png(Id)`
and `ImageSource.SteamAvatar(UserId)`. PNG, skin and Steam identifiers are
strings, preserving 64-bit identity. Sprite names are restricted asset keys,
not server paths. No constructor accepts a URL, downloads media or exposes
Carbon's image database. Client asset loading is best effort and not
acknowledged to scripts.

Changing ImageColor3 or ImageTransparency can use a patch. Changing Image is a
structural reconciliation. ImageButton uses the same presentation-bound,
exact-Player secure Activated path as TextButton.

## Retained scrolling

`ScrollingFrame` is a retained container with an explicit canvas. Its children
are projected below a private clipped content root. The ordinary layout and
image rules apply inside that content.

```lua
local Scroll = Screen:Create("ScrollingFrame")
Scroll.Size = UDim2.fromOffset(320, 280)
Scroll.CanvasSize = UDim2.fromOffset(320, 720)
Scroll.ScrollingDirection = "Y"

local Layout = Scroll:Create("UIListLayout")
Layout.Padding = UDim.new(0, 8)
```

`CanvasSize`, `ScrollingDirection` and `ScrollingEnabled` are shared retained
state. The current scroll position, drag state and inertia are client state for
each Presentation. CarbonLuau does not expose or claim to know them. Two Players
viewing one tree can scroll independently.

Changing the canvas, direction or enabled state structurally reconciles the
affected Presentation. Hide/Show, replacement, reconnect and full
reconciliation may reset its client-local scroll position. There is no
`CanvasPosition` or `AutomaticCanvasSize`; scripts must set `CanvasSize`
explicitly.

## Parenting and lifetime

`Gui:Create("ScreenGui")` creates a root. `Parent:Create("Frame")` creates and
parents a child in one operation. Frame, text, image and scrolling controls may also
be created detached through `Gui:Create`. Assign `Parent` to another live object
from the same owner domain, or to `nil`, to reparent or detach it. Cycles and
cross-domain parenting raise an ordinary Luau error without changing the tree.

Every object belongs permanently to the root or addon domain whose `Gui` facade
created it. Passing an object through a public addon module does not transfer
ownership. Other live addons may use that shared reference, but replacing or
unloading the owner makes all escaped host-backed references stale. Unloading a
consumer does not destroy the owner's GUI.

`Destroy()` recursively destroys a subtree, disconnects its GUI Signals and
invalidates queued interactions. Repeated `Destroy()` is safe; other operations
on destroyed or stale references raise a controlled error. Healthy replacement
keeps the old GUI active until the new candidate commits. Fatal VM recovery
creates fresh GUI state rather than preserving runtime trees.

## Showing and synchronization

`Show`, `Hide` and `IsShown` use the exact current Player connection.
`IsShown` reports desired server state. It is not confirmation that a client
rendered or removed the interface.

Property writes update the retained model immediately. CarbonLuau later
coalesces bounded work toward the newest state. Simple property changes may be
patched; hierarchy, `ZIndex`, known delivery uncertainty and periodic
reconciliation use a full replacement. Backend failure does not undo a
completed Luau mutation. CarbonLuau retains the newest desired state and retries
with a later full synchronization.

Client rendering has no acknowledgement contract. Real-client visual layout,
cursor behavior, click receipt and reconciliation remain unqualified in the
current release candidate.

## Interaction security

`TextButton.Activated` and `ImageButton.Activated` share one GUI event path. CarbonLuau generates private,
opaque action authority separately for every presentation and exact Player
connection. Scripts never receive tokens or raw client commands. Forged, stale,
cross-Player, hidden, retired and over-limit actions are rejected before Luau
entry, and lifetime checks run again before a queued callback starts.

Do not build authorization around button visibility alone. Check normal server
permissions and game state inside the callback before performing sensitive
actions.

## Current hard bounds

The current experimental candidate rejects operations that would exceed these
author-visible bounds:

| Resource | Bound |
|---|---:|
| Objects in one ScreenGui / tree depth / arranged GuiObject children of one object | 128 / 16 / 64 |
| UIListLayout / UIPadding children of one parent | 1 / 1 |
| Objects / ScreenGuis in one domain | 1,024 / 32 |
| Objects globally | 8,192 |
| Screens for one Player connection / viewers of one ScreenGui | 16 / 256 |
| Presentations per domain / globally | 512 / 4,096 |
| Interactive buttons per ScreenGui | 64 |
| Projected elements in one authoritative ScreenGui | 257 |
| Projected-element charge for one ScrollingFrame | 7 |
| Activated listeners per button / GUI Signal connections per domain | 8 / 256 |
| Name / one Text value / aggregate text per ScreenGui | 64 B / 2,048 B / 32 KiB UTF-8 |
| Clone size / clone depth | 128 objects / 16 |
| Accepted interactions per Player / per action | 20/s burst 20 / 8/s burst 8 |

Value and property ranges are listed in the [GUI reference](Gui-Reference.md).
Payload sizes, flush budgets, patch checkpoints and sends per flush are internal
bounded synchronization tuning, not author-facing compatibility promises.

## Differences from Roblox

CarbonLuau uses Roblox-familiar names and retained values, but it is not Roblox
DataModel compatibility:

- UI is server-driven and shown explicitly with `ScreenGui:Show(Player)`.
- There is no `Instance.new`, `PlayerGui` replication or client `LocalScript`.
- `ScreenGui` is a CarbonLuau root and always has `Parent == nil`.
- Rust CUI is an implementation detail and is not exposed to Luau.
- Client rendering state is best effort and is not acknowledged to scripts.
- TextBox, advanced styling and hover/focus events are not implemented.
  Foundation 2A layout, Foundation 2B typed images and Foundation 2C scrolling
  are implemented but have no assigned release identity yet.

See the [complete reference](Gui-Reference.md), the
[GUI examples](https://github.com/gmoddev/CarbonLuau/tree/main/examples/gui),
and [compatibility limits](Compatibility.md).
