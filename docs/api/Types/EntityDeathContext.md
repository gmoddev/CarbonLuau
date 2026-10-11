# EntityDeathContext

Development API `0.6.5-experimental`; not in published package v0.5.0.
Passed as the sole argument to `Workspace.EntityDied`. No public constructor.
This development contract and generated tooling do not establish host qualification.

| Read-only field | Type | Meaning |
|---|---|---|
| `Id` | `string` | Original canonical nonzero UInt64 network ID, retained for event correlation. |
| `Prefab` | `string` | Original full prefab snapshot, at most 512 UTF-8 bytes without NUL. |
| `Position` | `Vector3?` | Optional safe root-world observation before the death operation invalidates the source. |
| `Killer` | `Player?` | Optional original D11 actor facade captured by a separately qualified attribution source. |
| `KillerId` | `string?` | The same captured actor's string UserId; never a delivery-time account lookup. |
| `DamageType` | `string?` | Optional bounded label from the qualified killing-hit snapshot, at most 32 UTF-8 bytes. |
| `Weapon` | `string?` | Optional cached full WeaponPrefab label, at most 512 UTF-8 bytes; not an Item. |

The context is a frozen ordinary table with exactly these seven possible fields.
Optional unknown or unavailable values are nil, rather than guessed values.
Position is not a damage point, spawn-time cache or delivery-time live read.
DamageType describes a captured positive finite damage component; it is not a
universal death-cause classifier. Weapon does not prove equipped inventory,
ownership or a weapon Item lifetime.

Actor attribution is qualified for the narrow original, synchronous RPC melee
path on the supported exact host targets. The source captures the original D11
connection inside that attack's qualified application and revalidates the same
writer scope at full death completion. This does not establish broad attribution.
Indirect owner/account resolution, projectile history and last-attacker guesses
do not supply an actor. Unknown provenance gives nil Killer and KillerId.
Callbacks never resolve a UserId to select a replacement connection.

Killer retains its defining owner and original captured D11 connection. A later
disconnect/reconnect cannot retarget it; host operations through a disconnected
or retired facade can fail. Retain KillerId for correlation rather than looking
up a currently connected Player and assuming it is the captured actor.

Id, Prefab, optional scalar labels and Vector3 are historical values and can
remain readable after the original Entity is retired, pooled or its ID is reused.
The context contains no live Entity, private epoch/token, HitInfo or raw host
reference. Keeping it does not keep the Rust entity alive or grant authority to
an incarnation returned by a later lookup. It does not survive VM destruction
or a server-process restart.

A full death completion is distinct from successful removal. EntityDied may
arrive after EntityDestroyed, or without a removal observation. See
[Workspace.EntityDied](../Services/Workspace.md#entitydied) for source availability,
completion, scheduler and overload semantics, and the
[example](../../../examples/world/entity-died/init.luau).
