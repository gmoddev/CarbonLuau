# CarbonLuau documentation

CarbonLuau embeds a pinned Luau VM for bounded, server-side scripting on
Carbon-modded Rust servers. Release candidate `v0.3.0` is the first qualified
experimental release: Windows x64 and glibc Linux x64 passed the recorded
controlled-worker matrix.

Start with [installation](Installation.md), then use the
[experimental API reference](api/README.md). Read the
[compatibility limits](api/Compatibility.md) before deploying.

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
`carbonluau.status`. After editing the script, apply a candidate generation with
`carbonluau.reload`.

`Player:SendMessage` host dispatch passed controlled-host qualification. Visible
receipt by an authenticated client was not tested and is not claimed.

## What is included

- Transactional scripts and controlled `require` modules.
- Bounded `task.spawn`, `task.defer` and `task.delay`.
- Players, Player proxies, Signals/Connections and Commands.
- Carbon permission checks and administrator status/reload commands.
- VM memory, callback, queue, payload and logging bounds.

Items/inventory, arbitrary Rust hooks, filesystem/network access, Roblox
replication and `task.wait` are not included. See the [0.3.0 release-note
draft](releases/0.3.0.md) and [qualification record](Phase5-Validation.md) for the
precise envelope.
