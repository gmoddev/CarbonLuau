# Players

Availability: experimental API `0.3.0-experimental`; no permission needed for
identity lookup or listening. Obtain with `game:GetService("Players")`.

| Signature / property | Return | Behavior and failure |
|---|---|---|
| `Players:GetPlayers()` | `{Player}` | New mutable snapshot list of currently valid connections, ordered by ordinal string UserId. At most 1024. Host/context/population failure raises an error. |
| `Players:GetPlayerByUserId(UserId: string)` | `Player?` | Live connection or nil when absent. Accepts 1–20 ASCII decimal digits; numbers and malformed strings raise errors, never coerce through floating point. |
| `Players.PlayerAdded` | Signal | Future observed connections after registration; callback receives one Player. |
| `Players.PlayerRemoving` | Signal | Observed disconnection; callback receives one Player with safe identity snapshot. |

Snapshots are not live collections: changing a returned table does not change
the host. Its proxies still become stale on disconnect. The same connection
returns the same proxy while it remains retained in this generation. No startup
or reload event is synthesized for existing players; enumerate explicitly instead.

Events are delayed, so an Added callback can already observe a disconnected player.
Removing always invalidates that connection before callback execution: identity
is readable, `IsConnected` is false and mutation fails. A later connection from
the same account cannot reactivate the old proxy. Listeners and pending events
are cancelled when their generation retires; failed reload preserves old listeners.

```lua
local Players = game:GetService("Players")
for _, Player in Players:GetPlayers() do print(Player.Name, Player.UserId) end
Players.PlayerAdded:Connect(function(Player) print("joined", Player.UserId) end)
Players.PlayerRemoving:Connect(function(Player) print("left", Player.UserId) end)
local Player = Players:GetPlayerByUserId("76561198000000001")
if Player then print(Player.IsConnected) end
```

See [Player](../Types/Player.md), [Signal](../Types/Signal.md) and
[limits](../Compatibility.md). This is not all-account, sleeper or entity enumeration.
