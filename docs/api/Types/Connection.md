# Connection

Availability: experimental API `0.3.0-experimental`; GUI Activated connections
are added in `0.4.0-experimental`. Returned by Signal:Connect.
It is a frozen subscription facade, not a network connection or Carbon hook object.

`Connection:Disconnect() -> ()`

Removes that listener and prevents its queued future deliveries. Repeating the
call is harmless and returns no values. It cannot stop a callback already running.
No permission is required; wrong receiver raises a controlled error. There is no
public constructor, Connected property, reconnect or cross-generation reuse.

```lua
local Connection = game:GetService("Players").PlayerRemoving:Connect(function(Player)
    print(Player.Name)
end)
Connection:Disconnect()
Connection:Disconnect() -- idempotent
```

The owning domain automatically disconnects it on successful replacement or
unload. Failed replacement does not affect it. Keeping the Lua object does not
keep a retired domain or VM alive. See [Signal](Signal.md) and
[limits](../Compatibility.md).
