# EntityDestroyedContext

Introduced in development API `0.6.5-experimental`. Passed as the sole argument
to `Workspace.EntityDestroyed`. No public constructor.

| Read-only field | Type | Meaning |
|---|---|---|
| `Id` | `string` | Original canonical nonzero UInt64 network ID, captured before invalidation. |
| `Prefab` | `string` | Original full prefab identity, at most 512 UTF-8 bytes without NUL. |
| `Position` | `Vector3?` | Optional safely observed root world position before invalidation. |

This is a frozen ordinary table, not a destroyed Entity proxy. It has no private
epoch/token, Entity, cause, killer or loot fields. Position is nil when the source
has no qualified safe observation, including the native-deletion path; no
spawn-time or later position is invented.

All fields are ordinary immutable snapshots and remain usable after entity or
owner-domain retirement while an ordinary same-VM value remains reachable.
They do not persist across VM destruction or a server-process restart.
Retaining the context does not retain the Rust entity or grant a host capability.
Id is event correlation, not exact lifetime authority: a later lookup with that
Id may name an unrelated incarnation.

See [Workspace](../Services/Workspace.md#entitydestroyed) for completion proof,
bookkeeping limits, suppression, overload and host qualification.
