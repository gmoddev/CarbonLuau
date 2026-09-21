# Globals

Availability: experimental API `0.3.0-experimental`. `game` is a frozen facade,
shared by entrypoint, modules and callbacks within one generation. It is not a
Rust/Carbon object or Roblox DataModel. Overwriting a private script global does
not modify the shared facade or grant host authority.

| Signature / field | Type / return | Behavior |
|---|---|---|
| `game:GetService(Name)` | string -> Players, Commands, Gui or Items service | Exact case-sensitive names `Players`, `Commands`, `Gui` and `Items`; repeated retrieval returns the same domain-bound service. |
| `game.ApiName` | read-only string | `CarbonLuau` |
| `game.ApiVersion` | read-only string | Current candidate: `0.4.0-experimental` |
| `game.ApiStatus` | read-only string | `Experimental` |

Wrong receiver, non-string service name or unknown service raises a Luau error.
No permission is required for discovery or version inspection. A service exists
only in its owning generation; there is no cross-reload state preservation.

`Vector3` is also a frozen global constructor table beginning with
`0.4.0-experimental`. See the [Vector3 reference](Types/Vector3.md).

```lua
print(game.ApiName, game.ApiVersion, game.ApiStatus)
local Items = game:GetService("Items")
```

See [Players](Services/Players.md), [Commands](Services/Commands.md), [Items](Services/Items.md), [GUI](Gui.md) and
[compatibility](Compatibility.md). No other services or global host-call primitive
are exposed. Existing standard-library/sandbox restrictions remain in force.
