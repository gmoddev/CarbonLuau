# Vector3

Availability: experimental API `0.4.0-experimental`.

`Vector3` is an immutable CarbonLuau value representing three finite
world-coordinate components. It is not `UnityEngine.Vector3`, has no host or
domain lifetime, and may be shared as an ordinary same-VM Luau value.

```lua
local A = Vector3.new(10, 20, 30)
local B = Vector3.new(5, 0, -5)

print((A + B).X, (A * 2).Magnitude)
```

| Surface | Result |
|---|---|
| `Vector3.new(X, Y, Z)` | New Vector3 |
| `Value.X`, `Value.Y`, `Value.Z` | Read-only number |
| `Value.Magnitude` | Read-only Euclidean magnitude |
| `A == B` | Exact component equality |
| `A + B`, `A - B`, `-A` | New Vector3 |
| `A * Scalar`, `Scalar * A`, `A / Scalar` | New Vector3 |

Components must be numbers inside the finite System.Single range. NaN,
infinity, out-of-range components and arithmetic results raise controlled
errors; values are never clamped. Scalar operands must be finite and division
by zero errors. Vector3-by-Vector3 multiplication and division are unsupported.

`Unit`, `Dot` and `Cross` are not implemented. A Vector3 returned by
`Player.Position` remains usable after that Player disconnects because the value
contains no Player or host authority.
