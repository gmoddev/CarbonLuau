# CommandContext

Availability: experimental API `0.3.0-experimental`; passed to Commands callbacks.
The context and its argument array are frozen snapshots. No constructor is exposed.

| Property | Type | Meaning |
|---|---|---|
| `Context.Player` | Player | Validated player-issued caller; never a fabricated console player. |
| `Context.Name` | string | Canonical registered command name without chat prefix. |
| `Context.Arguments` | `{string}` | Ordered copy of Carbon's parsed arguments; no live managed collection. |

Admission accepts at most 16 arguments, 512 UTF-8 bytes per argument and 4096 total
argument bytes. NUL, malformed strings and excessive input are rejected before Lua
entry. It does not parse the raw chat command again. There is no callback for an
unauthorized caller, stale connection, retired generation or unsupported caller
type. Permission is checked at both admission and scheduler entry.

```lua
game:GetService("Commands"):Register("echo", {}, function(Context)
    print(Context.Name, Context.Player.UserId, Context.Arguments[1] or "")
end)
```

Property reads need no extra permission. Attempted edits raise Lua errors. The
context may be retained inside this generation, but its Player can disconnect and
every later Player operation revalidates the connection. No object survives a
successful generation replacement. See [Commands](../Services/Commands.md).
