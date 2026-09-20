# GUI

Availability: experimental API `0.4.0-experimental`.

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

## Parenting and lifetime

`Gui:Create("ScreenGui")` creates a root. `Parent:Create("Frame")` creates and
parents a child in one operation. `Frame`, `TextLabel` and `TextButton` may also
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

`TextButton.Activated` is the only GUI event. CarbonLuau generates private,
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
| Objects in one ScreenGui / tree depth / children of one object | 128 / 16 / 64 |
| Objects / ScreenGuis in one domain | 1,024 / 32 |
| Objects globally | 8,192 |
| Screens for one Player connection / viewers of one ScreenGui | 16 / 256 |
| Presentations per domain / globally | 512 / 4,096 |
| TextButtons per ScreenGui | 64 |
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
- Images, TextBox, scrolling, layout helpers, advanced styling and hover/focus
  events are not implemented.

See the [complete reference](Gui-Reference.md), the
[GUI examples](https://github.com/gmoddev/CarbonLuau/tree/main/examples/gui),
and [compatibility limits](Compatibility.md).
