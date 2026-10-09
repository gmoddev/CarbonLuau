# PlayerSpawnContext

Introduced in development API `0.6.5-experimental`. Passed to
`Players.PlayerSpawned` as its second callback argument. No public constructor.

`Position: Vector3?` is an optional snapshot of the exact Player's root world
position at completed initial activation or full respawn. It is not a current
client pose, eye position, terrain projection or later live Player.Position read.

The context is a frozen ordinary table. It has no Killer, cause, spawn-reason or
life-ID contract. Registering a callback never synthesizes history for existing
players. See [Players](../Services/Players.md) for the full observation contract.
