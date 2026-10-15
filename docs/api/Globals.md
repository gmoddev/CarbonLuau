# Globals

Availability: experimental API `0.3.0-experimental`. `game` is a frozen facade,
shared by entrypoint, modules and callbacks within one generation. It is not a
Rust/Carbon object or Roblox DataModel. Overwriting a private script global does
not modify the shared facade or grant host authority.

| Signature / field | Type / return | Behavior |
|---|---|---|
| `game:GetService(Name)` | string -> Players, Commands, Gui, Items, Workspace, DataStoreService or Loot | Exact case-sensitive names. Workspace was introduced at API 0.6, Loot at 0.6.5; both are included in v0.7.0 but not v0.5.0. |
| `game.ApiName` | read-only string | `CarbonLuau` |
| `game.ApiVersion` | read-only string | Development `0.7.5-experimental`; published v0.7.0 retains `0.7.0-experimental` |
| `game.ApiStatus` | read-only string | `Experimental` |

Wrong receiver, non-string service name or unknown service raises a Luau error.
No permission is required for discovery or version inspection. A service exists
only in its owning generation; there is no cross-reload state preservation.

Development API `0.7.5-experimental` also introduces frozen ordinary-string
[Item and Prefab catalogs](Catalogs.md): 221 flat `Item` constants and exactly
`Prefab.Loot.Barrel1` / `Prefab.Loot.Barrel2`. They are shared immutable data
per VM generation; script-owned environments may shadow their own binding.
`Item` is distinct from the existing host-facing `Items` service. Catalog access
does not authorize an operation or broaden any validator or physical support.

`Vector3` is also a frozen global constructor table beginning with
`0.4.0-experimental`. See the [Vector3 reference](Types/Vector3.md).

`GiveItemBehavior` is a frozen global table in `0.4.0-experimental`, with only
the typed `InventoryOnly` member. See [GiveItemBehavior](Types/GiveItemBehavior.md).

`Enum` is a frozen global namespace in development API `0.6.5-experimental`.
Its frozen `DropMode` namespace contains only the sealed typed userdata
`Enum.DropMode.Replace`, with public type `DropMode` and string form
`DropMode.Replace`. See [Enum](Types/Enum.md), [DropMode](Types/DropMode.md) and
the init-only [Loot](Services/Loot.md) declaration. Existing GiveItemBehavior
continues to use its global table.

```lua
print(game.ApiName, game.ApiVersion, game.ApiStatus)
local Items = game:GetService("Items")
```

The existing services retain their earlier availability. Persistence's API version
assignment does not establish qualification or retroactive 0.4 availability.
See [Players](Services/Players.md), [Commands](Services/Commands.md), [Items](Services/Items.md), [GUI](Gui.md),
[DataStoreService](Services/DataStoreService.md), [Workspace](Services/Workspace.md), [Loot](Services/Loot.md) and
[compatibility](Compatibility.md). No other services or global host-call primitive
are exposed. Existing standard-library/sandbox restrictions remain in force.
