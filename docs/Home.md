# CarbonLuau documentation

CarbonLuau lets you write bounded, server-side Luau scripts for Carbon-modded
Rust servers. Start with [installation](Installation.md), then the
[API reference](api/README.md) and [compatibility limits](api/Compatibility.md).

## Which version are you reading?

| Source | Availability |
|---|---|
| `v0.7.0` experimental | Complete current API `0.7.0-experimental` ([notes](releases/0.7.0.md), [qualification](Release-0.7.0-Validation.md)) |
| Earlier `v0.5.0` | Experimental API `0.5.0-experimental`: runtime, addons, Player gameplay, GUI, local persistence and structured Query |
| Current development | API `0.7.0-experimental`: also read-only Workspace/Entity, bounded async discovery, gameplay Signals, Loot Replace and PlayerInventory |

Development features are not retroactively in v0.5.0. C4 removed the four legacy
Player inventory methods: use `Player:GetInventory()` and the
[migration guide](api/Inventory-Migration.md). Package/API/ABI identities are
independent; [release planning](Release.md) and
[planned 0.6 notes](releases/0.6.0.md) do not publish a package.

## Quick start

Create `carbon/data/CarbonLuau/scripts/init.luau`:

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Create the sibling `modules` directory, start the server and run
`carbonluau.status`. Apply an edited script with `carbonluau.reload`.
Controlled-host message dispatch is qualified; visible receipt by an
authenticated client is not implied by this example.

## Guides

- [Players and commands](api/README.md), [Player examples](api/Player-Examples.md).
- [Addon composition](api/Addons.md) and [provider registration](api/Addon-Providers.md).
- [GUI guide](api/Gui.md) and [member reference](api/Gui-Reference.md).
- [Local persistence and Query](api/Persistence.md).
- Development [world lookup](api/Services/Workspace.md),
  [discovery](api/Discovery.md), [inventory](api/Types/PlayerInventory.md)
  and [Loot](api/Services/Loot.md).

This is server-side Luau, not Roblox replication or a generic Rust/Unity bridge.
No raw host objects, arbitrary filesystem/network APIs or generic hooks are
exposed. GUI client observations, host-specific adaptation and process/storage
guarantees remain limited to their recorded qualification envelope.

## Contracts, evidence and history

The [documentation index](Index.md) distinguishes current contracts,
[qualification](qualification/README.md) and [historical research](history/README.md).
Contributors start at [AICONTEXT](../AICONTEXT.md), then the
[invariant register](Invariants.md) and [validation policy](Compatibility.md).
Failed proofs remain available; they are not current open implementation gates.

Published release notes: [0.3](releases/0.3.0.md), [0.4](releases/0.4.0.md),
[0.5](releases/0.5.0.md).
