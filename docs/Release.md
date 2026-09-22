# Release identity and reproducibility

CarbonLuau's addon and GUI-capable experimental release candidate uses this deliberate
identity mapping:

| Identity | Value |
|---|---|
| Intended release/tag | `v0.4.0` |
| Carbon package | `0.4.0` |
| Scripting API | `CarbonLuau 0.4.0-experimental` |
| API status | `Experimental` |
| Native ABI | `1.4` |
| Provider protocol | `CarbonLuau.Addons` / `1.2` |
| Addon package schema | `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

Published v0.3.0 remains the gameplay-facade baseline. The additive package and
scripting API minor bump identifies public addon composition, GUI Foundation 1
and the implemented Foundation 2 and Foundation 3 surfaces without claiming a
stable 1.0 API. The unreleased candidate remains 0.4.0
rather than advancing to 0.5.0 because no published 0.4 compatibility surface is
being superseded. Package, scripting API, native ABI, provider protocol, package
schema and pinned Luau are separate compatibility identities even though this
candidate records them together. [release.json](https://github.com/gmoddev/CarbonLuau/blob/main/release.json)
is the machine-readable owner of the mapping, and CI checks it against source and
documentation.

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
`v0.4.0` remains a separate, explicitly authorized action.

## Matching editor artifacts

The extension's `tooling-source.json` pins the exact runtime candidate. Its
`tools/Package.py` creates platform VSIX files from the canonical pack, records
both commits and payload identities, normalizes ZIP metadata, and checks repeated
packaging for identical hashes. Extension version `0.0.1` and tooling pack
`0.4.0-rc.1` are independent of scripting API `0.4.0-experimental` and protocol
`CarbonLuau.Tooling/1.0`; none changes the runtime ABI or package schema.
Windows/Linux x64 VSIX artifacts require clean installed-extension E2E; macOS
arm64 artifacts remain explicitly static-only. Hashes establish integrity, not
publisher signing or OS sandboxing. Candidate artifacts are local/CI evidence,
not a Marketplace, Open VSX or GitHub Release publication.
