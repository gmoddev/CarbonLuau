# CarbonLuau

CarbonLuau is a planned Carbon-only Rust server plugin that embeds the open-source Luau VM behind a small, bounded C ABI.

The first-version architecture and implementation brief is in [`docs/CarbonLuau_FirstVersion_Design.md`](docs/CarbonLuau_FirstVersion_Design.md). It is the source of truth for the v0.1 scope, safety invariants, repository layout, and acceptance criteria.

## Status

This repository is initialized with the design specification and a pinned Luau vendor checkout. Runtime implementation begins with the Phase 0 native-loading proof described in the design.

## License

Project licensing and third-party attribution are documented in [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

