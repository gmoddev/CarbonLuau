# CarbonLuau scripting API

The current candidate implements the **experimental** `CarbonLuau`
`0.4.0-experimental` scripting API. It preserves the gameplay facade introduced
in 0.3.0-experimental and adds qualified addon composition plus the GUI
Foundation 1 surface.
Windows/Linux workers, sanitizers and live Carbon qualification passed; exact
scope and limitations are maintained in [Foundation E](../FoundationE.md).
This is server-side Luau, not Roblox API compatibility.

| Implemented surface | Reference |
|---|---|
| `game` and service discovery | [Globals](Globals.md) |
| Players, connected-player snapshots, join/leave events | [Players](Services/Players.md) |
| Player-issued chat commands | [Commands](Services/Commands.md) |
| Player identity, messaging, permission query | [Player](Types/Player.md) |
| Command payload | [CommandContext](Types/CommandContext.md) |
| Event subscription | [Signal](Types/Signal.md), [Connection](Types/Connection.md) |
| Versions and limits | [Compatibility](Compatibility.md) |
| Existing `require` and `task.spawn/defer/delay` | [Phase 2 script contract](../Phase2.md) |
| Addon manifests, dependencies and package-qualified imports | [Addon composition](Addons.md) |
| Carbon provider registration protocol | [Addon providers](Addon-Providers.md) |
| Server-driven retained GUI and qualified Foundation 2A layout source | [GUI guide](Gui.md), [GUI reference](Gui-Reference.md) |

Players and Commands are available beginning with API `0.3.0-experimental`.
Addon composition and GUI are available beginning with
`0.4.0-experimental`. They are implemented but experimental; see the evidence
records before deploying.
Methods use colon syntax. Services, proxies and command contexts cannot be edited.
API misuse and failed player operations raise controlled Luau errors; catch with
`pcall` when appropriate. Lookup absence returns nil; `IsConnected` returns false.
No API returns a host object or transfers ownership of a Rust player to Lua.

Successful healthy reload replaces the operator-root domain inside the current VM.
Failed candidate initialization preserves the old root, including its listeners,
commands and queued callbacks.
Callbacks run later through a bounded main-thread scheduler, not inline in hooks.
Timeout may retire the entire generation under the existing one-attempt recovery
policy. Previously delivered messages are not undone or deduplicated on recovery.

```lua
local Players = game:GetService("Players")
Players.PlayerAdded:Connect(function(Player)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Deferred/not supported: inventory, entities, health, teleport, moderation/admin
mutation, networking, HTTP, filesystem APIs, arbitrary hooks/console execution,
reflection, Roblox hierarchy/replication and `task.wait`. No Phase 4 API is shipped.
The entire item convenience surface (`Items`, `Items:Exists`, `Player:GiveItem`)
is deferred from v0.1 by [D13](../Invariants.md#decision-register), not pending
implementation in this scripting API version.

Addon packages use exact dependency bindings, explicit exports and the readonly
`addon` context. They are public experimental behavior beginning with
`0.4.0-experimental`; they are not present in published v0.3.0 artifacts.

GUI Foundation 1 provides `game:GetService("Gui")`, ScreenGui, Frame,
TextLabel, TextButton, retained properties, immutable layout/color values,
explicit per-Player Show/Hide and `TextButton.Activated`. Foundation 2A adds
implemented but not yet release-versioned `LayoutOrder`, `UIListLayout` and
`UIPadding`. Images, TextBox, scrolling, advanced styling, hover/focus and raw
CUI remain unsupported. Authenticated-client visual, cursor, click-receipt and
reconciliation observations remain unqualified even though the experimental
API is available.
