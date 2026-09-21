# Items

Availability: experimental API `0.4.0-experimental`. Retrieve the frozen,
domain-bound service with `game:GetService("Items")`.

| Signature | Return | Behavior |
|---|---|---|
| `Items:Exists(ShortName: string)` | boolean | Returns whether the exact canonical Rust item short name exists. |

Short names are 1 through 128 ASCII bytes, lowercase, untrimmed, and contain no
NUL. Leading or trailing whitespace, uppercase, non-ASCII, empty, oversized, and
wrong-type input raises a controlled programming error. CarbonLuau never
lowercases or trims input. A syntactically valid unknown name returns `false`.

`Exists` exposes no definition, numeric ID, display name, stack size, category,
skin, or other host metadata. It is a bounded direct definition lookup and may
run during provisional initialization. A retained service from a retired domain
fails closed; its returned booleans are ordinary Luau values.

```lua
local Items = game:GetService("Items")
if Items:Exists("scrap") then
    print("This server recognizes scrap")
end
```

See [Player](../Types/Player.md) for physical inventory observation and
[compatibility](../Compatibility.md) for bounds.
