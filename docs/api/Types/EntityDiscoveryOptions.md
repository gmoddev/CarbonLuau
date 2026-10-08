# EntityDiscoveryOptions

Discovery-2B development contract for scripting API `0.6.0-experimental`;
not included in published package `0.5.0`. Public qualification is recorded in
[Foundation 2B](../../WorldEntityFoundation2B.md).

This plain-table alias is the optional last argument to
[`Workspace:GetEntitiesInRadiusAsync`](../Services/Workspace.md#getentitiesinradiusasync):

```luau
type EntityDiscoveryOptions = {
    Prefab: string?,
    Limit: number?,
}
```

| Field | Contract | Default |
|---|---|---|
| `Prefab` | Exact full canonical prefab string, nonempty valid UTF-8 of at most 512 bytes, no NUL. | No prefab filter. |
| `Limit` | Integer 1..256. | 256. |

Omit Options, pass nil, use an empty table or omit either field to use the
corresponding default. Only Prefab and Limit are supported; other keys and
metatables are rejected. Wrong types are not coerced. Invalid options raise a
controlled synchronous submission error without callback.

Prefab comparison is exact and case-sensitive. No short-name matching,
wildcards, normalization or resource/path lookup is performed. A well-formed
prefab with no observed matches produces an empty successful result array.

Limit caps the whole result, not the scan or a nearest-N selection. A match
beyond Limit fails the whole query with nil array and a controlled error rather
than returning a partial array. Smaller Limit does not relax the shared work,
admission or absolute-deadline bounds. Results retain physical encounter order.

The type names an ordinary options table; it has no constructor or host object
authority. See the [discovery guide](../Discovery.md) and
[runnable example](../../../examples/world/discovery/init.luau).
