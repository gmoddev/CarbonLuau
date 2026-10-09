# CarbonLuau

Among the first public Luau runtimes built specifically for Rust server modding

CarbonLuau brings server-side Luau scripting to Carbon-modded Rust servers through a Roblox-familiar programming model, with gameplay APIs, composable addons, persistent data, retained GUI, and dedicated development tooling.

The latest published release is [v0.5.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.5.0). The `0.5.0` release is experimental. Start with the [hosted documentation](https://gmoddev.github.io/CarbonLuau/), [installation](docs/Installation.md), or the [public API reference](docs/api/README.md).

Development `main` adds exact-host player death/spawn Signals and
`Workspace.EntitySpawned` and snapshot-only `Workspace.EntityDestroyed` in
`0.6.5-experimental`. See
[Players](docs/api/Services/Players.md), [Workspace](docs/api/Services/Workspace.md)
and the [B1](docs/GameplayB1-Validation.md) / [B3](docs/GameplayB3-Validation.md)
qualification records for current
limits. These additions are not in the published v0.5.0 archives.

## Install and write a script

CarbonLuau targets Windows x64 and glibc Linux x64 servers running Carbon. Persistence-2D live host evidence uses Rust build `25653776` and Carbon `2.0.261.0` on Linux; earlier gameplay/GUI evidence uses other recorded builds. See [compatibility](docs/Compatibility.md) for exact platform and feature limits.

Use the release archive matching the server OS. Install `CarbonLuau.cszip` in `carbon/plugins` and all three matching native files—the runtime DLL or SO, compiler worker and storage worker—in `carbon/data/CarbonLuau/native/win-x64` or `native/linux-x64`. On Linux, make both workers executable after extraction. Put your entrypoint at `carbon/data/CarbonLuau/scripts/init.luau` and create its `modules` directory, even when empty. Do not mix platform binaries or overwrite existing scripts.

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Use the administrator commands `carbonluau.status` and `carbonluau.reload`. More examples are available for [player events](examples/player-events/init.luau), [player positions](examples/player-position/init.luau), [player health](examples/player-health/init.luau), [physical inventory reads](examples/player-inventory/init.luau), [permission-protected commands](examples/hello-command/init.luau), [GUI](examples/gui/hello/init.luau), and [addon composition](examples/addons/shop/init.luau). Startup messages must use `task.defer` because provisional generations cannot send them.

For clean-checkout builds, checksums, and provenance, see the [release reproducibility guide](docs/Release.md).

## What is included

The `CarbonLuau 0.5.0-experimental` API retains controlled modules/tasks, Player events, permission-protected commands, live position/health reads, inventory checks, verified GiveItem/TakeItem and Teleport. It adds private local durable stores with callback-based Get/Set/Remove and bounded structured Query. Execution uses bounded logging, memory and deadlines. Provider-owned addon packages share one VM with exact dependency lifetimes and explicit public modules imported through `require("@id[/path]")`.

Development API `0.6.0-experimental` adds exact read-only `Workspace`/`Entity`
lookup and [bounded async discovery](docs/api/Discovery.md) on the pinned hosts.
See [qualification and release readiness](docs/WorldEntityFoundation2C.md).
The latest published package remains 0.5.0 and does not contain these APIs.

Addon packages are registered by a loaded Carbon provider plugin. CarbonLuau does not scan an addon directory or download packages. See [addon composition](docs/api/Addons.md), the [provider protocol](docs/api/Addon-Providers.md), and the [Foundation E qualification record](docs/FoundationE.md).

The GUI surface offers ScreenGui, Frame, TextLabel, TextButton, ImageLabel,
ImageButton, ScrollingFrame, typed ImageSource values, deterministic list and
padding/grid layout, bounded Frame clipping, retained GuiFont values and
Presentation-specific one-way scroll methods,
explicit per-Player Show/Hide and secure Activated callbacks.
See the [GUI guide](docs/api/Gui.md). Authenticated-client visual,
cursor, click-receipt and reconciliation behavior remains unqualified.

[Official VS Code tooling](https://github.com/gmoddev/carbonluau-vscode) provides
syntax, API reference and project diagnostics, plus trusted language analysis
and GUI preview on Windows/Linux x64. The preview includes hierarchy, properties,
viewport controls and resource usage; geometry comes from the runtime's shared
implementation. The v0.4.0 VSIX is pinned to the older runtime/API and is not
a qualified matching v0.5.0 artifact; a compatible editor pack requires separate
qualification. macOS remains static-only. Preview uses offline image/font approximations and
does not simulate gameplay or connect to a server.

## Limits

Callbacks and host inputs are bounded, deadlines are cooperative, and the VM cap is not a whole-server memory cap. Successful reload cancels old listeners and commands; failed candidates preserve them. CarbonLuau does not expose raw Rust objects, arbitrary console execution, filesystem or network APIs, or a Roblox hierarchy.

Inventory mutation is not transactional: false means mutation never started, true means its physical postcondition was verified, and a post-mutation error may leave changes. GiveItem supports only InventoryOnly. Raw inventory objects, world-drop fallback, health mutation, TextBox, package downloads and version solving are unavailable. Teleport is server-qualified but not authenticated-client-qualified.

TextBox is not implemented because the current host cannot preserve submitted text exactly.

`Frame.ClipsDescendants`, `GuiFont`, text `Font`, and the `ScrollTo` methods are
part of the experimental source surface. Their authenticated-client visual,
font-asset and scrolling behavior remains unqualified and is not implied by the
release-candidate identity.

Authenticated real-client behavior and Shockbyte full-runtime behavior remain outside the qualified support envelope. See [API compatibility and limits](docs/api/Compatibility.md) and [platform compatibility](docs/Compatibility.md) for details.

## Contributing

Current persistence foundation: [Foundation 1C](docs/PersistenceFoundation1C.md)
records combined local durability and asynchronous API qualification; [Foundation
2D](docs/PersistenceFoundation2D.md) closes derived-index and public Query
qualification. Structured Query uses `Field`, `Equals` or inclusive `Min`/`Max`.
Preparation is automatic; optional `Indexes = { "Coins", "Level" }` hints ask
for early preparation. Authors manage no schemas, versions or migrations. See
the [author guide](docs/api/Persistence.md), [0.5.0 release notes](docs/releases/0.5.0.md),
and [canonical design](docs/PersistenceFoundation2.md).
[World/Entity Foundation 1](docs/WorldEntityFoundation1.md) has a qualified
private exact-lifetime substrate and read-only runtime on the pinned hosts.
Historical [negative lifetime evidence](docs/WorldEntityLifetimeInvestigation.md)
remains preserved; development public docs start at [Workspace](docs/api/Services/Workspace.md).

Start with [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture, security, and lifecycle requirements. [Compatibility](docs/Compatibility.md) owns support and validation policy. Historical phase contracts and qualification records remain in `docs/` for traceability.

## AI Disclaimer

AI tools are used during the development of this addon. However, the project's architecture, design decisions, requirements, and overall direction are substantially human-designed and reviewed.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
