# CarbonLuau

CarbonLuau brings server-side Luau scripting to Carbon-modded Rust servers through a small, bounded native bridge.

The latest published release is [v0.3.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.3.0). The `main` branch is the qualified v0.4.0 release candidate and adds experimental addon composition and server-driven retained GUI. Start with the [hosted documentation](https://gmoddev.github.io/CarbonLuau/), the [installation guide](docs/Installation.md), or the [public API reference](docs/api/README.md).

## Install and write a script

CarbonLuau targets Windows x64 and glibc Linux x64 servers running Carbon. The qualified worker baseline is Carbon 2.0.259.0 with Rust 2633. See [compatibility](docs/Compatibility.md) for the complete support policy.

Use the release archive matching the server OS. Install `CarbonLuau.cszip` in `carbon/plugins` and both matching native files, the runtime DLL or SO and its compiler worker, in `carbon/data/CarbonLuau/native/win-x64` or `native/linux-x64`. On Linux, run `chmod 0755 carbon/data/CarbonLuau/native/linux-x64/carbonluau_compiler` after extraction. Put your entrypoint at `carbon/data/CarbonLuau/scripts/init.luau` and create its `modules` directory, even when empty. Do not mix platform binaries or overwrite existing scripts.

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Use the administrator commands `carbonluau.status` and `carbonluau.reload`. More examples are available for [player events](examples/player-events/init.luau), [permission-protected commands](examples/hello-command/init.luau), [GUI](examples/gui/hello/init.luau), and [addon composition](examples/addons/shop/init.luau). Startup messages must use `task.defer` because provisional generations cannot send them.

For clean-checkout builds, checksums, and provenance, see the [release reproducibility guide](docs/Release.md).

## What is included

The v0.4.0 candidate provides sandboxed Luau execution, bounded logging and memory, execution and compilation deadlines, reloads, controlled modules and tasks, and the Players, Signals, Commands and Gui facades. It also provides bounded provider-owned addon packages, exact dependency lifetimes, explicit public modules, and package-qualified `require("@id[/path]")` inside one shared VM. The scripting identity is `CarbonLuau 0.4.0-experimental`.

Addon packages are registered by a loaded Carbon provider plugin. CarbonLuau does not scan an addon directory or download packages. See [addon composition](docs/api/Addons.md), the [provider protocol](docs/api/Addon-Providers.md), and the [Foundation E qualification record](docs/FoundationE.md).

The GUI surface offers ScreenGui, Frame, TextLabel, TextButton, ImageLabel,
ImageButton, ScrollingFrame, typed ImageSource values, deterministic list and
padding layout, current-source Frame clipping, retained GuiFont values and
Presentation-specific one-way scroll methods,
explicit per-Player Show/Hide and secure Activated callbacks.
The implemented Foundation 2 and Foundation 3 controls have completed
server-side model, lifecycle, scale and release-candidate qualification.
See the
[GUI guide](docs/api/Gui.md). Authenticated-client visual,
cursor, click-receipt and reconciliation behavior remains unqualified.

## Limits

Callbacks and host inputs are bounded, deadlines are cooperative, and the VM cap is not a whole-server memory cap. Successful reload cancels old listeners and commands; failed candidates preserve them. CarbonLuau does not expose raw Rust objects, arbitrary console execution, filesystem or network APIs, or a Roblox hierarchy.

Provider-defined C# capabilities, root-to-addon imports, package downloads, version solving, multiple package instances, restricted exposure profiles, and async capabilities remain deferred. CarbonLuau has one shared VM heap cap, not per-addon hard heap isolation. Deterministic `UIListLayout`/`UIPadding`, typed images and `ScrollingFrame` are part of the experimental `0.4.0-experimental` surface. TextBox is not implemented because the current Rust command transport cannot preserve submitted text exactly; advanced GUI styling is also not implemented. D18 approves a future Player interaction surface, including implementation-gated `Player:GiveItem` and `Player:TakeItem` under revised D13, but none of it is implemented. Raw inventory objects, world-drop fallback and rollback APIs remain excluded.

`Frame.ClipsDescendants`, `GuiFont`, text `Font`, and the `ScrollTo` methods are
part of the experimental source surface. Their authenticated-client visual,
font-asset and scrolling behavior remains unqualified and is not implied by the
release-candidate identity.

Authenticated real-client behavior and Shockbyte full-runtime behavior remain outside the qualified support envelope. See [API compatibility and limits](docs/api/Compatibility.md) and [platform compatibility](docs/Compatibility.md) for details.

## Contributing

Start with [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture, security, and lifecycle requirements. [Compatibility](docs/Compatibility.md) owns support and validation policy. Historical phase contracts and qualification records remain in `docs/` for traceability.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
