# Players

Availability: experimental API `0.3.0-experimental`; no permission needed for
identity lookup or listening. Obtain with `game:GetService("Players")`.

| Signature / property | Return | Behavior and failure |
|---|---|---|
| `Players:GetPlayers()` | `{Player}` | New mutable snapshot list of currently valid connections, ordered by ordinal string UserId. At most 1024. Host/context/population failure raises an error. |
| `Players:GetPlayerByUserId(UserId: string)` | `Player?` | Live connection or nil when absent. Accepts 1–20 ASCII decimal digits; numbers and malformed strings raise errors, never coerce through floating point. |
| `Players.PlayerAdded` | Signal | Future observed connections after registration; callback receives one Player. |
| `Players.PlayerRemoving` | Signal | Observed disconnection; callback receives one Player with safe identity snapshot. |
| `Players.PlayerDied` | Signal `(Player, PlayerDeathContext)` | Completed terminal death of a tracked human life, not wound entry or a vetoed death attempt. Added in development API `0.6.5-experimental`. |
| `Players.PlayerSpawned` | Signal `(Player, PlayerSpawnContext)` | Completed eligible initial activation and subsequent full respawn, including an alive full-respawn reset. Added in development API `0.6.5-experimental`. |

Snapshots are not live collections: changing a returned table does not change
the host. Its proxies still become stale on disconnect. The same connection
returns the same proxy while it remains retained in this generation. No startup
or reload event is synthesized for existing players; enumerate explicitly instead.

Events are delayed, so an Added callback can already observe a disconnected player.
Removing always invalidates that connection before callback execution: identity
is readable, `IsConnected` is false and mutation fails. A later connection from
the same account cannot reactivate the old proxy. Listeners and pending events
are cancelled when their generation retires; failed reload preserves old listeners.

The new death/spawn Signals require the exact qualified host observation adapter.
If that source is unavailable, connecting raises a controlled error. Sleeping,
waking, revive and reconnect of an already-spawned sleeper are not new spawns.
No startup, registration, replacement or VM-recovery history is replayed.

Unlike the existing Added/Removing snapshots, a queued death/spawn delivery is
suppressed if the victim's original connection is gone. A replacement connection
with the same UserId never receives that old event. A captured killer may become
disconnected before delivery without cancelling the live victim's event; its
proxy never retargets a reconnect.

[PlayerDeathContext](../Types/PlayerDeathContext.md) and
[PlayerSpawnContext](../Types/PlayerSpawnContext.md) are immutable ordinary Luau
tables. Position is an optional captured root world-space Vector3, not a later
Player.Position read. Killer and KillerId are optional and come only from a
qualified direct tracked human initiator, including self-attribution where
present. Environmental, NPC, indirect and unavailable sources normally give nil.
There are no cause, weapon or damage-type fields. Queues and global producer/fanout
work are bounded; overload drops are counted, not replayed or retried.

The [Gameplay A qualification record](../../GameplayFoundationA-Validation.md)
separates exact-host server traces, synthetic VM fixtures and authenticated-client
evidence. Development availability is not a published v0.5.0 artifact claim.

```lua
local Players = game:GetService("Players")
for _, Player in Players:GetPlayers() do print(Player.Name, Player.UserId, Player.Position) end
Players.PlayerAdded:Connect(function(Player) print("joined", Player.UserId) end)
Players.PlayerRemoving:Connect(function(Player) print("left", Player.UserId) end)
local Player = Players:GetPlayerByUserId("76561198000000001")
if Player then print(Player.IsConnected) end
```

See [Player](../Types/Player.md), [Signal](../Types/Signal.md) and
[limits](../Compatibility.md). This is not all-account, sleeper or entity enumeration.
