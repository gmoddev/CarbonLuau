# CarbonLuau

Write Rust server mods in Luau, with Carbon doing the hosting.

CarbonLuau brings a Roblox-familiar scripting style to Carbon-modded Rust servers.
Use it for chat commands, player rewards, custom menus, persistent progress, and
addons that work together. It's server-side scripting—not Roblox running inside Rust.

[Get started](docs/Installation.md) · [Documentation](https://gmoddev.github.io/CarbonLuau/)
· [API reference](docs/api/README.md) · [Examples](examples)

## What can I build?

- **Player features:** welcome messages, permission-checked commands, inventory
  rewards, health checks, and teleports.
- **Menus and interfaces:** buttons, images, scrolling panels, and layouts through
  the [GUI API](docs/api/Gui.md).
- **Saved progress:** local server persistence for things like coins or player
  settings, with asynchronous reads/writes and structured queries.
- **Reusable addons:** split a larger project into modules and share public modules
  between addons. See the [addon guide](docs/api/Addons.md).

The current version also includes world/entity lookup and radius searches,
player/entity lifecycle events, sampled inventory changes, and a small loot
replacement API. These features require the qualified host/startup configuration.

## Pick your version

**v0.7.0 experimental:** [release page](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.7.0).
It includes the complete current API: persistence, world discovery, gameplay,
inventory, addons, loot replacement, and GUI.
Start here if you want a published package rather than a development build.

**Runtime identity:** package `0.7.0`, scripting API `0.7.0-experimental`.
See the [release notes](docs/releases/0.7.0.md) and
[qualification record](docs/Release-0.7.0-Validation.md). Check [compatibility](docs/api/Compatibility.md) before mixing
runtime, script, or editor versions.

One important change from v0.5.0: inventory operations now live on
`Player:GetInventory()`, not directly on Player. For a connected `Player`:

```luau
local Inventory = Player:GetInventory()
local Scrap = Inventory:CountItem("scrap")
local HasEnough = Inventory:HasItem("scrap", 100)
```

Use `Inventory:GiveItem` and `Inventory:TakeItem` for writes. The old Player
methods are removed in API 0.7; the [migration guide](docs/api/Inventory-Migration.md)
shows the replacements. `ItemChanged` reports sampled net changes, not every
individual pickup or transfer. Loot replacement currently supports only one
qualified barrel prefab and its allowlisted items—see the [Loot reference](docs/api/Services/Loot.md).

## Try a first script

You'll need a Windows x64 or glibc Linux x64 server running Carbon. Download the
matching release archive and follow the [installation guide](docs/Installation.md)
to copy its files into the right Carbon folders. Keep the runtime and its workers
together; don't mix files from different releases.

Put your entry script at `carbon/data/CarbonLuau/scripts/init.luau`, and create
its `modules` folder even if it's empty. Here's a simple welcome message:

```luau
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`[Players:Welcome] Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau!")
end)
```

Use `carbonluau.status` to check the runtime and `carbonluau.reload` after editing
scripts. If you want to send messages during startup, use `task.defer` so they
run after the script has loaded successfully.

For something bigger, try the [command example](examples/hello-command/init.luau),
[GUI example](examples/gui/hello/init.luau), or [addon shop](examples/addons/shop/init.luau).
Examples on `main` target the development API; use the examples bundled with your
release when running v0.5.0.

## Editing your scripts

The [official VS Code extension](https://github.com/gmoddev/carbonluau-vscode)
offers syntax support, API information, project checks, and GUI preview.
Use a tooling pack that matches your runtime: the published v0.4.0 VSIX isn't a
qualified match for v0.5.0, v0.7.0 or current `main`. This release does not publish a new VSIX.

Language analysis and GUI preview are qualified on Windows/Linux x64; macOS is
static-only. Preview is an offline helper, not a Rust client or gameplay simulator,
and images/fonts can look different in game.

## A few things to keep in mind

CarbonLuau is experimental. It limits script work, memory, callbacks, and logging,
but those limits don't cap the memory of your whole Rust server or make every
host operation instantly interruptible. Scripts use deliberate APIs rather than
raw Rust objects or arbitrary filesystem, network, or console access.

Inventory writes aren't transactions: `false` means no inventory mutation began,
`true` means the result was verified, and an error after mutation starts may leave
changes. There is no automatic rollback or replay. `GiveItemBehavior.InventoryOnly`
doesn't intentionally drop overflow into the world.

Some real-client GUI visuals, fonts, scrolling, click delivery, and Teleport
convergence remain unqualified. TextBox is **not implemented** because the host
cannot preserve submitted text exactly. Client-side Luau, automatic addon
downloads, raw inventory objects, and health writes aren't provided.

Addons are registered through Carbon provider plugins rather than downloaded or
found by scanning an addon folder. Check the [provider guide](docs/api/Addon-Providers.md)
if you're packaging one. Hosting-provider support also varies; Shockbyte's full
runtime remains unqualified. The [compatibility guide](docs/Compatibility.md)
has the detailed tested limits.

## Contributing

Start with [AICONTEXT.md](AICONTEXT.md) for the contributor workflow and canonical
rules. [Release preparation](docs/Release.md) covers reproducible builds and the
separate publication gate. The qualification records in `docs/` keep both passing
tests and earlier failed approaches available for review.
The [history audit](docs/history/maintenance/HistoryCuration-Validation.md#historycuration-validation) records the unreleased
squash and scoped email cleanup without changing published releases.

AI tools are used during development. The project's architecture, requirements,
design decisions, and overall direction are substantially human-designed and reviewed.

## License

See [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) for
project licensing and third-party attribution.
