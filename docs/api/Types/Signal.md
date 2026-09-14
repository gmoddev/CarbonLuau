# Signal

Availability: experimental API `0.3.0-experimental`. Only
Players.PlayerAdded and PlayerRemoving exist; no public constructor or generic hook.

`Signal:Connect(Callback: (Player) -> ()) -> Connection`

Registers one callback in the current generation and returns a
[Connection](Connection.md). No permissions are needed. Wrong receiver or
non-function callback raises an error. Each signal permits 128 live listeners,
256 total per generation; exceeding a limit raises an error. Repeated registration
of the same function creates distinct listeners in registration order.

Host events snapshot eligible listener identities in registration order. Each
listener gets a separate scheduler item and a fresh validation before entry.
Disconnecting an already-queued listener suppresses that delivery. One listener
error does not prevent unrelated callbacks while the VM is healthy. Timeouts can
retire the complete generation, cancel pending work and trigger one reconstruction.
Callbacks cannot yield. Queues are bounded; overload can reject event deliveries.

```lua
local Signal = game:GetService("Players").PlayerAdded
local Connection = Signal:Connect(function(Player) print(Player.UserId) end)
Connection:Disconnect()
```

Successful reload/unload removes registrations; failed candidate preserves active
listeners. No callbacks run inline in Carbon's event hook. See
[Players](../Services/Players.md) for exact join/leave semantics and
[limits](../Compatibility.md) for queue behavior.
