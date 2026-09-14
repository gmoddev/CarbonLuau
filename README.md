# CarbonLuau

CarbonLuau is a planned Carbon-only Rust server plugin that embeds the open-source Luau VM behind a small, bounded C ABI.

Start contribution work at [AICONTEXT.md](AICONTEXT.md), which maps each rule to its canonical document. [Invariants](docs/Invariants.md) owns architecture/security/lifecycle requirements; [Compatibility](docs/Compatibility.md) owns support and validation policy. The [first-version design](docs/CarbonLuau_FirstVersion_Design.md) remains the accepted v0.1 phase plan, API direction and initial configuration reference.

## Status

Phase 0 is **PROVEN for the tested environments**: Windows x64, Linux x64 on the Docker worker, and the user's Shockbyte server. Native loading, the ABI probe, and unload/reload are confirmed. Phase 1 (the bounded Luau execution core) is cleared to begin. Luau remains vendored but is not yet linked or executed. See [build and deployment instructions](docs/Phase0.md) and [actual validation results](docs/Phase0-Validation.md).

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
