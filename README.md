# CarbonLuau

CarbonLuau adds bounded server-side Luau scripting to Carbon-modded Rust servers.
This is an experimental source snapshot: package **0.4.0**, scripting
API **0.4.0-experimental**. A source snapshot is not a published binary release.

- [Build and test](docs/Build.md)
- [Installation](docs/Installation.md)
- [Scripting API](docs/api/README.md)
- [Compatibility and evidence scope](docs/Support.md)
- [Safety and ownership](docs/Safety.md)
- [Release identity](docs/Release.md)

The complete API reference and examples accompany this source. Runtime behavior,
ABI and finite item/prefab admission follow the shipped implementation. Public
Item/Prefab constants, where exposed by that release, are canonical strings,
not extra gameplay authority.

AI tools are used during development. The project's architecture, requirements,
design decisions, and overall direction are substantially human-designed and reviewed.

See [LICENSE](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md).

PUBLICATION.json records the source revision, explicit projection policy digest,
transformations and complete public payload hashes. The public Git history is
independent; a private source revision is an audit identifier, not a public URL.
