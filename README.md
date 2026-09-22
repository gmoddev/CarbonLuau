# CarbonLuau

CarbonLuau brings server-side Luau scripting to Carbon-modded Rust servers, with a Roblox/Luau-familiar scripting model, addons, Player gameplay APIs and retained GUI.

The latest published release is [v0.3.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.3.0). The `0.4.0` candidate is experimental. Start with the [hosted documentation](https://gmoddev.github.io/CarbonLuau/), [installation](docs/Installation.md), or the [public API reference](docs/api/README.md).

## Install and write a script

CarbonLuau targets Windows x64 and glibc Linux x64 servers running Carbon. Current host evidence uses Carbon 2.0.259 and Rust build 25353106. See [compatibility](docs/Compatibility.md) for the exact platform and feature qualification limits.

Use the release archive matching the server OS. Install `CarbonLuau.cszip` in `carbon/plugins` and both matching native files, the runtime DLL or SO and its compiler worker, in `carbon/data/CarbonLuau/native/win-x64` or `native/linux-x64`. On Linux, run `chmod 0755 carbon/data/CarbonLuau/native/linux-x64/carbonluau_compiler` after extraction. Put your entrypoint at `carbon/data/CarbonLuau/scripts/init.luau` and create its `modules` directory, even when empty. Do not mix platform binaries or overwrite existing scripts.

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

The `CarbonLuau 0.4.0-experimental` API provides controlled modules/tasks, Player events, permission-protected commands, live position/health reads, inventory checks, verified GiveItem/TakeItem and Teleport. Execution uses bounded logging, memory and deadlines. Provider-owned addon packages share one VM with exact dependency lifetimes and explicit public modules imported through `require("@id[/path]")`.

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
implementation. Install a matching platform VSIX without a source checkout.
macOS remains static-only. Preview uses offline image/font approximations and
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

Start with [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture, security, and lifecycle requirements. [Compatibility](docs/Compatibility.md) owns support and validation policy. Historical phase contracts and qualification records remain in `docs/` for traceability.

## AI Disclaimer

AI tools are used during the development of this addon. However, the project's architecture, design decisions, requirements, and overall direction are substantially human-designed and reviewed. AI is used primarily as a development and implementation aid rather than as the source of the project's design.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
