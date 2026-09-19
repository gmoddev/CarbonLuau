# CarbonLuau documentation

CarbonLuau embeds a pinned Luau VM for bounded, server-side scripting on
Carbon-modded Rust servers. Published `v0.3.0` contains the first gameplay facade.
The qualified `v0.4.0` candidate adds the first experimental public addon surface;
Windows x64, glibc Linux x64, sanitizers and live Carbon passed the recorded matrix.

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
- Provider-owned addon packages, exact dependency lifetimes and public modules.
- `require("@addon")`, `require("@addon/path")` and dependency availability.

Items/inventory, arbitrary Rust hooks, filesystem/network access, Roblox
replication and `task.wait` are not included. See the [0.3.0 release notes](releases/0.3.0.md)
for the published baseline and the [0.4.0 release notes](releases/0.4.0.md)
plus [Foundation E qualification](FoundationE.md) for the addon-capable envelope.
The current source ownership map is recorded in
[Foundation F](FoundationF.md), and compiler containment is recorded in
[Foundation G](FoundationG.md).

## Architecture direction

[Invariants](Invariants.md) owns canonical runtime policy. The approved,
unimplemented GUI Foundation 1 contract is decision
[D15](Invariants.md#d15--gui-foundation-1-retained-presentation-model), with
detailed rationale and future implementation guidance in
[GUI Foundation 1 architecture](GuiFoundation1.md). GUI is not part of the
current public API and its release identity remains unassigned. The internal
descriptor/render/backend substrate is recorded separately in
[GUI Foundation 1A](GuiFoundation1A.md).
