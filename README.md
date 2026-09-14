# CarbonLuau

CarbonLuau is a planned Carbon-only Rust server plugin that embeds the open-source Luau VM behind a small, bounded C ABI.

The first-version architecture and implementation brief is in [`docs/CarbonLuau_FirstVersion_Design.md`](docs/CarbonLuau_FirstVersion_Design.md). It is the source of truth for the v0.1 scope, safety invariants, repository layout, and acceptance criteria.

## Status

Phase 0 is **PROVEN for the tested environments**: Windows x64, Linux x64 on the Docker worker, and the user's Shockbyte server. Native loading, the ABI probe, and unload/reload are confirmed. Phase 1 (the bounded Luau execution core) is cleared to begin. Luau remains vendored but is not yet linked or executed. See [build and deployment instructions](docs/Phase0.md) and [actual validation results](docs/Phase0-Validation.md).

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
