# CarbonLuau

CarbonLuau is a planned Carbon-only Rust server plugin that embeds the open-source Luau VM behind a small, bounded C ABI.

Start contribution work at [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture/security/lifecycle requirements; [Compatibility](docs/Compatibility.md) owns support and validation policy. The [first-version design](docs/CarbonLuau_FirstVersion_Design.md) remains the accepted v0.1 phase plan, API direction and initial configuration reference.

## Status

Phase 0 remains **PROVEN for its tested environments**, including Shockbyte.
Phase 1 now implements the bounded Luau execution core and passes native/managed,
Linux sanitizer, and actual Windows/Linux Carbon worker qualification. It includes
compilation/execution, sandboxed libraries, bounded logging, VM memory caps,
monotonic timeouts, and atomic runtime status/reload. See the [Phase 1 contract](docs/Phase1.md)
and [validation evidence](docs/Phase1-Validation.md) for exact scope, CI status and limitations.

Shockbyte Phase 1 is not yet qualified. No player API or other Phase 2 functionality
is implemented; Phase 2 requires a new task.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
