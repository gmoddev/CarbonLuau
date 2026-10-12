# InventoryChangeContext

Introduced at API `0.6.5-experimental`, included in published v0.7.0 but not
v0.5.0. Passed as the sole argument to
[PlayerInventory.ItemChanged](PlayerInventory.md#itemchanged). No public constructor.

| Read-only field | Type | Meaning |
|---|---|---|
| `Item` | `string` | Canonical lowercase Rust item shortname, 1..128 ASCII bytes without NUL. |
| `Before` | `number` | Quantity in the previous accepted physical observation; an exact integer from 0 through `2^53 - 1`. |
| `After` | `number` | Quantity in the current accepted physical observation; an exact integer from 0 through `2^53 - 1`. |
| `Delta` | `number` | Nonzero signed `After - Before`, exactly representable between `-(2^53 - 1)` and `2^53 - 1`. |

This is a frozen ordinary table with exactly these four fields. It contains no
Player, raw Item/container, private connection token, operation, cause or actor.
The quantities use the existing physical main/belt/wear acceptance rules.

A context describes two accepted samples, not a completed inventory operation.
Temporary transfers can produce decreases and increases. Balanced changes
between samples can disappear. There is no individual pickup/drop/consumption
receipt, historical replay or exactly-once delivery guarantee. The first sample
after registration or snapshot failure establishes a baseline without a callback.
See [PlayerInventory](PlayerInventory.md#itemchanged) for publication, suppression,
dropped intervals and shared resource bounds.

Retained scalar fields remain readable after Player disconnect or owner-domain
retirement while the ordinary value remains reachable in the same VM. They grant
no host authority and do not survive VM destruction or a server-process restart.
Preview support is unavailable; tooling does not invent a physical inventory
backend to emit these contexts.
