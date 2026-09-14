# CarbonLuau scripting API

The Phase 3 working tree implements the **experimental** `CarbonLuau`
`0.3.0-experimental` facade. Qualification status is maintained in
[Phase3-Validation](../Phase3-Validation.md); implementation is not a PASS claim.
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

All facade APIs below are available beginning with API `0.3.0-experimental`.
They are implemented but experimental; see the evidence record before deploying.
Methods use colon syntax. Services, proxies and command contexts cannot be edited.
API misuse and failed player operations raise controlled Luau errors; catch with
`pcall` when appropriate. Lookup absence returns nil; `IsConnected` returns false.
No API returns a host object or transfers ownership of a Rust player to Lua.

Successful reload replaces the whole generation. Failed candidate initialization
preserves the old one, including its listeners, commands and queued callbacks.
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
mutation, UI, networking, HTTP, filesystem APIs, arbitrary hooks/console execution,
reflection, Roblox hierarchy/replication and `task.wait`. No Phase 4 API is shipped.
