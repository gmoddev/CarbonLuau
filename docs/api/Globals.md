# Globals

Availability: experimental API `0.3.0-experimental`. `game` is a frozen facade,
shared by entrypoint, modules and callbacks within one generation. It is not a
Rust/Carbon object or Roblox DataModel. Overwriting a private script global does
not modify the shared facade or grant host authority.

| Signature / field | Type / return | Behavior |
|---|---|---|
| `game:GetService(Name)` | string -> Players or Commands service | Exact case-sensitive names `Players` and `Commands`; repeated retrieval returns the same service within this generation. |
| `game.ApiName` | read-only string | `CarbonLuau` |
| `game.ApiVersion` | read-only string | `0.3.0-experimental` |
| `game.ApiStatus` | read-only string | `Experimental` |

Wrong receiver, non-string service name or unknown service raises a Luau error.
No permission is required for discovery or version inspection. A service exists
only in its owning generation; there is no cross-reload state preservation.

```lua
print(game.ApiName, game.ApiVersion, game.ApiStatus)
local Commands = game:GetService("Commands")
```

See [Players](Services/Players.md), [Commands](Services/Commands.md) and
[compatibility](Compatibility.md). No other services or global host-call primitive
are exposed. Existing standard-library/sandbox restrictions remain in force.
