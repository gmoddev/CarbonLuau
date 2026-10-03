# Entity

Availability: development scripting API `0.6.0-experimental` on the
[qualified exact hosts](../Compatibility.md); not included in published
package `0.5.0`.

`Entity` is a read-only, non-owning facade for one exact live Rust entity
lifetime. It exposes no raw Rust, Unity or .NET object.

| Property | Type | Meaning |
|---|---|---|
| `Entity.Id` | `string` | Canonical current-world network lookup key, not persistent identity. |
| `Entity.Prefab` | `string` | Full canonical prefab name for this lifetime, at most 512 UTF-8 bytes. |
| `Entity.Position` | `Vector3` | Fresh root world-space position; parenting does not change its coordinate space. |

Each property read revalidates the exact entity and the originating script
domain. A destroyed, reused or replaced entity, retired domain, or unqualified
world produces a controlled stale/world error—not a cached value or `nil`.
Already-read strings and `Vector3` values remain ordinary snapshots. `Position`
is never writable through this facade.

Two Entity facades compare equal when they refer to the same exact host lifetime,
even if independently looked up in different domains. Equality does not make a
stale facade usable: each facade keeps its original domain authority. After a
script/addon/VM replacement, acquire a fresh facade. A later entity may reuse
the same `Id` without comparing equal to the old one. Identity does not survive
a server restart and must not be stored as a persistence key.

There is no `IsValid`, mutation, hierarchy, inventory, reflection or lifecycle
Signal on this type. See [Workspace](../Services/Workspace.md) and
[examples](../World-Examples.md).
