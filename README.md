# CarbonLuau

CarbonLuau is a planned Carbon-only Rust server plugin that embeds the open-source Luau VM behind a small, bounded C ABI.

The first-version architecture and implementation brief is in [`docs/CarbonLuau_FirstVersion_Design.md`](docs/CarbonLuau_FirstVersion_Design.md). It is the source of truth for the v0.1 scope, safety invariants, repository layout, and acceptance criteria.

## Status

Phase 0 implements a native-loading probe, an explicit Windows/Linux loader, tests, and CI. Luau remains vendored but is not linked or executed. See [build and deployment instructions](docs/Phase0.md) and [actual validation results](docs/Phase0-Validation.md).

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
