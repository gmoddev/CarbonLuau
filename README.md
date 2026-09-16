# CarbonLuau

CarbonLuau brings server-side Luau scripting to Carbon-modded Rust servers through a small, bounded native bridge.

The current release is [v0.3.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.3.0), an experimental prerelease. Start with the [hosted documentation](https://gmoddev.github.io/CarbonLuau/), the [installation guide](docs/Installation.md), or the [public API reference](docs/api/README.md).

## Install and write a script

CarbonLuau targets Windows x64 and glibc Linux x64 servers running Carbon. The qualified worker baseline is Carbon 2.0.259.0 with Rust 2633. See [compatibility](docs/Compatibility.md) for the complete support policy.

Use the release archive matching the server OS. Install `CarbonLuau.cszip` in `carbon/plugins` and its single native DLL or SO in `carbon/data/CarbonLuau/native/win-x64` or `native/linux-x64`. Put your entrypoint at `carbon/data/CarbonLuau/scripts/init.luau` and create its `modules` directory, even when empty. Do not install both native binaries or overwrite existing scripts.

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Use the administrator commands `carbonluau.status` and `carbonluau.reload`. More examples are available for [player events](examples/player-events/init.luau) and [permission-protected commands](examples/hello-command/init.luau). Startup messages must use `task.defer` because provisional generations cannot send them.

For clean-checkout builds, checksums, and provenance, see the [release reproducibility guide](docs/Release.md).

## What is included

The v0.3.0 release provides sandboxed Luau execution, bounded logging and memory, deadlines, reloads, controlled modules and tasks, and the Players, Signals, and Commands gameplay facade. The API remains experimental.

Current development on `main` also includes Foundations A through D: a shared-VM multi-domain runtime, bounded addon package registration, exact dependency lifetimes, and explicit public addon modules through `require("@id[/path]")`. This work is documented in [Foundation D](docs/FoundationD.md). It is newer than the v0.3.0 release artifacts and does not yet have a public addon scripting API version.

## Limits

Callbacks and host inputs are bounded, deadlines are cooperative, and the VM cap is not a whole-server memory cap. Successful reload cancels old listeners and commands; failed candidates preserve them. CarbonLuau does not expose raw Rust objects, arbitrary console execution, filesystem or network APIs, or a Roblox hierarchy.

Provider capabilities, root-to-addon imports, package downloads, version solving, multiple package instances, restricted exposure profiles, async capabilities, and Foundation E remain deferred. The item convenience APIs `Player:GiveItem` and `Items:Exists` are also deferred because no safe ownership adapter has been established.

Authenticated real-client behavior and Shockbyte full-runtime behavior remain outside the qualified support envelope. See [API compatibility and limits](docs/api/Compatibility.md) and [platform compatibility](docs/Compatibility.md) for details.

## Contributing

Start with [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture, security, and lifecycle requirements. [Compatibility](docs/Compatibility.md) owns support and validation policy. Historical phase contracts and qualification records remain in `docs/` for traceability.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
