# CarbonLuau

CarbonLuau adds server-side Luau scripting to Carbon-modded Rust servers through a bounded native bridge.

**Release candidate:** `v0.3.0` — first qualified experimental release. Read the
[hosted documentation](https://gmoddev.github.io/CarbonLuau/),
[installation guide](docs/Installation.md), or [public API reference](docs/api/README.md).
The Players/Signals/Commands facade passed Windows/Linux controlled-worker
qualification and CI. Real-client delivery and Shockbyte full-runtime behavior
remain unqualified.

## Install and write a script

Target environments are Windows x64 and glibc Linux x64 with Carbon. The qualified
worker baseline is Carbon 2.0.259.0 / Rust 2633; see [compatibility](docs/Compatibility.md).

Use the release archive matching the server OS. Install `CarbonLuau.cszip` in
`carbon/plugins` and its single native DLL/SO in
`carbon/data/CarbonLuau/native/win-x64` or `native/linux-x64`. Put your entrypoint
at `carbon/data/CarbonLuau/scripts/init.luau` and create its `modules` directory,
even when empty. Do not install both native binaries or overwrite existing scripts.

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Use administrator commands `carbonluau.status` and `carbonluau.reload`. See
[player-event](examples/player-events/init.luau) and
[permission-protected command](examples/hello-command/init.luau) examples.
Startup messages must use `task.defer`; provisional generations cannot send them.
For clean-checkout builds, checksums and provenance, see the
[release reproducibility guide](docs/Release.md). Current changes prepare the
candidate only; no tag or GitHub Release exists yet.

## Limits

Callbacks and host inputs are bounded, deadlines are cooperative, and the VM cap
is not a whole-server memory cap. Successful reload cancels old listeners/commands;
failed candidates preserve them. No raw Rust objects, arbitrary console execution,
filesystem/network API or Roblox hierarchy is exposed. The API is experimental;
see [API compatibility and limits](docs/api/Compatibility.md). No Phase 4 features.

Start contribution work at [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture/security/lifecycle requirements; [Compatibility](docs/Compatibility.md) owns support and validation policy. The [first-version design](docs/CarbonLuau_FirstVersion_Design.md) remains the accepted v0.1 phase plan, API direction and initial configuration reference.

## Status

Phase 0 remains **PROVEN for its tested environments**, including Shockbyte.
Phase 1 now implements the bounded Luau execution core and passes native/managed,
Linux sanitizer, and actual Windows/Linux Carbon worker qualification. It includes
compilation/execution, sandboxed libraries, bounded logging, VM memory caps,
monotonic timeouts, and atomic runtime status/reload. See the [Phase 1 contract](docs/Phase1.md)
and [validation evidence](docs/Phase1-Validation.md) for exact scope, CI status and limitations.

Phase 2 adds configured script loading, controlled modules, bounded tasks and
transactional script generations. See the [Phase 2 contract](docs/Phase2.md) and
[qualification record](docs/Phase2-Validation.md) for those historical results.
Phase 3 adds the gameplay facade described above; its new evidence is separate.
Shockbyte Phase 1/2/3 remain unqualified.

Phase 4 item conveniences (`Player:GiveItem` and `Items:Exists`, including the Items
service) are **deferred from v0.1** by [D13](docs/Invariants.md#decision-register).
The [investigation](docs/Phase4.md) and [evidence](docs/Phase4-Validation.md) are
preserved; no safe all-path ownership adapter was established. The
[roadmap](docs/CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases)
places Phase 5 hardening/qualification after the implemented facade. Its committed
Windows/Linux live matrices, profiler, latency-distribution and sustained-soak
evidence are in [Phase5-Validation](docs/Phase5-Validation.md). The execution
verdict is complete within the recorded controlled-host qualification envelope.
Authenticated real-client qualification is user-deferred and non-gating, but
authenticated Steam/network establishment, visible client chat receipt,
network-driven player lifecycle events and real same-account reconnect remain
unqualified. Shockbyte full-runtime qualification is separately user-deferred and
non-gating, but remains unqualified and unsupported beyond its historical Phase 0
probe. This is not the independent v0.1 release decision.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
