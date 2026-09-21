# Player

Availability: core proxy in experimental API `0.3.0-experimental`; Position is
added in `0.4.0-experimental`. Obtained from Players,
Signals or CommandContext; there is no constructor. A frozen proxy represents
one connection in one generation, never a BasePlayer or a transferable host handle.

| Signature / property | Return | Behavior |
|---|---|---|
| `Player.Name` | read-only string | Current bounded host display name while connected; creation-time name snapshot when disconnected. |
| `Player.UserId` | read-only string | Decimal account ID, never a floating-point number. |
| `Player.IsConnected` | read-only boolean | Fresh host resolution and connection identity check. False when the connection is gone/replaced. |
| `Player.Position` | read-only [Vector3](Vector3.md) | Fresh exact-connection root world position. Stale/disconnected access errors; no snapshot is retained. |
| `Player:SendMessage(Message: string)` | no values | Sends system chat through Rust `BasePlayer.ChatMessage`; 1024 UTF-8 bytes maximum, no NUL. The command name/channel is host-fixed, not script-controlled. |
| `Player:HasPermission(Permission: string)` | boolean | Fresh Carbon permission query for this live connection; no permission mutation. |

Every operation revalidates identity, including property access. Position is a
bounded direct observation and is allowed during provisional initialization.
Sleeping, wounded and mounted/parented host-valid players remain readable;
mounted/parented reads use the root Transform's world position. Each read returns
a new immutable value that remains usable after later movement or disconnect.
After disconnect, Name/UserId remain safe snapshot values; Position, SendMessage and HasPermission raise
`Player is no longer connected`. Same-account reconnect creates a different proxy.
Generation retirement destroys VM-local script references and rejects late host work.
Once host invalidity is observed, the old token stays invalid even if the host
reuses an object; a newly observed connection receives a new lifetime.

Malformed receivers, attempted writes, wrong input types, invalid permission names,
oversized/NUL input and host failure raise controlled errors. Unknown properties
return nil. Permission names are 1–128 ASCII bytes, start with a lowercase letter,
contain lowercase letters/digits/underscore/hyphen and nonempty dot-separated
segments, and include a namespace dot. HasPermission itself requires no permission.

SendMessage needs a live player but no additional permission: it cannot grant
items, run arbitrary commands or change administrator state. **It is prohibited
during provisional initialization**, including reload/recovery entrypoints and
modules they require. Use deferred work for startup messages:

```lua
local Players = game:GetService("Players")
task.defer(function()
    local Player = Players:GetPlayers()[1]
    if Player then
        print(Player.Name, Player.UserId, Player.IsConnected)
        print(Player:HasPermission("carbonluau.example.hello"))
        local Ok, Error = pcall(function() Player:SendMessage("Hello") end)
        if not Ok then print(Error) end
    end
end)
```

Failed candidates never deliver their deferred messages. A successful host call
does not acknowledge client receipt; there is no replay deduplication or formatting
sanitization beyond the bounded string contract. `IsConnected` is not a lease:
always allow the later operation to fail. See [compatibility](../Compatibility.md).
