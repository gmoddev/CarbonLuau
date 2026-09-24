# Release identity and reproducibility

Current development mapping after the 2026-09-23 Persistence Foundation 1
assignment under [D12](Invariants.md#d12--scripting-and-protocol-identity).
Persistence-1B is **implemented and qualified within its recorded scope** in
[the 1B record](PersistenceFoundation1B.md). Persistence-1C is **NOT STARTED**;
combined closure remains separate. The [1B evidence](PersistenceFoundation1B-Validation.md)
records its green implementation-source CI. Experimental development API
availability is not release approval or an overall Foundation 1 PASS claim.

| Identity | Value |
|---|---|
| Retained development release/tag fields | `0.4.0` / `v0.4.0` (not publication authority) |
| Carbon development package | `0.4.0` (unchanged) |
| Intended future persistence package | `0.5.0` (not bumped or released) |
| Current scripting API | `CarbonLuau 0.5.0-experimental` |
| API status | `Experimental` |
| Native ABI | `1.5` (additive reserved persistence-completion export) |
| Provider protocol | `CarbonLuau.Addons` / `1.2` |
| Addon package schema | `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

Published v0.3.0 remains the gameplay-facade baseline. Addon, GUI and Player
declarations keep their historical introduction versions through 0.4. Persistence
is explicitly introduced at 0.5, without retroactive availability or a stable 1.0
claim. Package, scripting API, native ABI, provider protocol, package schema and
pinned Luau are separate compatibility identities. [release.json](https://github.com/gmoddev/CarbonLuau/blob/main/release.json)
is the machine-readable owner of the mapping, and CI checks it against source and
documentation.

`tools/Test-Release.ps1` requires releaseVersion/packageVersion/tag to agree but
does not require them to equal the scripting API version. No development package
move is therefore needed: package/release/tag remain 0.4.0/v0.4.0 while the API
advances to 0.5.0-experimental. Any resulting 0.4-named development bundle carries
the exact API and ABI in provenance; it must not be published as a persistence
release. The intended future package 0.5.0 requires a separate qualified release
manifest move and explicit publication authorization. The ABI 1.5 change is
independently required by the new `cl_domain_storage_completion` ingress; it is
not inferred from the API version. Provider 1.2, addon schema 1 and Luau are unchanged.

Authenticated-client visual layout, cursor behavior, actual click receipt,
clipping and hit regions, selected font rendering, one-way scroll behavior and
client reconciliation remain unqualified and are not release-artifact claims.
This evidence is non-gating for the experimental identity but must stay visible
in release notes and compatibility documentation.

`TextBox` and `Submitted` are not part of the release identity. Their D16 design
remains deferred because the inspected host transport cannot preserve submitted
text exactly.

## Reproduce a platform bundle

From a clean checkout at the candidate revision, build and test the native
library for the target platform, then run:

```powershell
./tools/New-ReleaseArtifacts.ps1 `
  -Rid win-x64 `
  -NativeLibrary ./build/win-x64/Release/carbonluau_native.dll `
  -CompilerWorker ./build/win-x64/Release/carbonluau_compiler.exe
```

On Linux with PowerShell 7:

```powershell
./tools/New-ReleaseArtifacts.ps1 `
  -Rid linux-x64 `
  -NativeLibrary ./build/linux-x64/libcarbonluau_native.so `
  -CompilerWorker ./build/linux-x64/carbonluau_compiler
```

The command creates a platform archive, provenance JSON and SHA-256 checksum in
`dist/release`. ZIP entries are sorted and use a fixed timestamp. CI builds each
target from a clean checkout, creates the archive twice and rejects a packaging
hash mismatch before uploading the release-candidate artifacts.

The archive contains only the production `.cszip`, the matching native runtime and compiler worker,
examples, installation/release notes, license/attribution and generated
provenance. It never contains live qualification fixtures or both platform
binaries.

No tag or GitHub Release is created by these scripts or workflows. Publishing
any tag or GitHub Release remains a separate, explicitly authorized action.

## Matching editor artifacts

The extension's `tooling-source.json` pins the exact runtime candidate. Its
`tools/Package.py` creates platform VSIX files from the canonical pack, records
both commits and payload identities, normalizes ZIP metadata, and checks repeated
packaging for identical hashes. Extension version `0.0.1` and tooling pack
`0.4.0-rc.1` describe the earlier tooling candidate and are independent of the
development scripting API `0.5.0-experimental` and protocol
`CarbonLuau.Tooling/1.0`; none changes the runtime ABI or package schema.
Windows/Linux x64 VSIX artifacts require clean installed-extension E2E; macOS
arm64 artifacts remain explicitly static-only. Hashes establish integrity, not
publisher signing or OS sandboxing. Candidate artifacts are local/CI evidence,
not a Marketplace, Open VSX or GitHub Release publication. Earlier packs are not
silently qualified for the new API; rebuild and qualify an exact compatible pack.
