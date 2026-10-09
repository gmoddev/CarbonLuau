# PlayerDeathContext

Introduced in development API `0.6.5-experimental`. Passed to
`Players.PlayerDied` as its second callback argument. No public constructor.

| Read-only field | Type | Meaning |
|---|---|---|
| `Position` | `Vector3?` | Optional observed root world position when terminal death completed. |
| `Killer` | `Player?` | Exact original connection of a qualified direct human initiator. |
| `KillerId` | `string?` | Captured killer UserId when that initiator was available. |

This is a frozen ordinary table. Missing observations are nil, not invented
defaults. Position and KillerId remain ordinary snapshots; the Killer facade
keeps its original domain and exact connection validity. It never performs a
UserId lookup to select a new connection at delivery. Retaining the table does
not keep a Player connected or grant new host capabilities.

See [Players](../Services/Players.md) for suppression, overload and host limits.
