# Workspace

Availability: development scripting API `0.6.0-experimental`, for the exact
qualified Rust/Carbon targets only. The latest published package remains
`0.5.0`; it does **not** contain this API. Obtain the service with
`game:GetService("Workspace")`.

| Method | Result | Behavior |
|---|---|---|
| `Workspace:GetEntityById(Id: string)` | `Entity?` | One keyed lookup of a currently admitted Rust `BaseEntity`; `nil` if a valid ID is absent. |

`Id` must be a canonical nonzero unsigned-64 decimal string: 1–20 ASCII
digits, no leading zero, sign, whitespace or numeric conversion. A malformed
ID raises a controlled error. The service never scans or enumerates the world.

If CarbonLuau was first installed after this server process had already loaded
its world, or if the CarbonLuau plugin was fully unloaded and reloaded, Entity
lookup fails with a controlled world-unavailable error. **Restart the server**
with CarbonLuau in the initial plugin batch before using this service. A
failed/unqualified observer is not reported as an empty world. Ordinary script,
addon or VM recovery does not itself require a server restart while that
observer remains continuously installed.

```luau
local Workspace = game:GetService("Workspace")
local Entity = Workspace:GetEntityById("123")
if Entity then
    print(Entity.Prefab, Entity.Position)
end
```

An ID is a lookup key in the *current* Rust world, not durable Entity identity;
it may later refer to another object. See [Entity](../Types/Entity.md),
[examples](../World-Examples.md) and the [exact compatibility envelope](../Compatibility.md).
There is no enumeration, spatial query, Signal, Spawn or Destroy method.
