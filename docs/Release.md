# Release identity and reproducibility

CarbonLuau's first qualified experimental release candidate uses one deliberate
identity mapping:

| Identity | Value |
|---|---|
| Intended release/tag | `v0.3.0` |
| Carbon package | `0.3.0` |
| Scripting API | `CarbonLuau 0.3.0-experimental` |
| API status | `Experimental` |
| Native ABI | `1.2` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

The original “v0.1” roadmap name describes first-version scope; it is not a
second package or tag identity. Package `0.3.0` already identifies the completed
facade increment, so the release candidate preserves it and avoids a misleading
`v0.1.0` alias. [release.json](https://github.com/gmoddev/CarbonLuau/blob/main/release.json)
is the machine-readable owner of this mapping, and CI checks it against source
and documentation.

## Reproduce a platform bundle

From a clean checkout at the candidate revision, build and test the native
library for the target platform, then run:

```powershell
./tools/New-ReleaseArtifacts.ps1 `
  -Rid win-x64 `
  -NativeLibrary ./build/win-x64/Release/carbonluau_native.dll
```

On Linux with PowerShell 7:

```powershell
./tools/New-ReleaseArtifacts.ps1 `
  -Rid linux-x64 `
  -NativeLibrary ./build/linux-x64/libcarbonluau_native.so
```

The command creates a platform archive, provenance JSON and SHA-256 checksum in
`dist/release`. ZIP entries are sorted and use a fixed timestamp. CI builds each
target from a clean checkout, creates the archive twice and rejects a packaging
hash mismatch before uploading the release-candidate artifacts.

The archive contains only the production `.cszip`, the matching native library,
examples, installation/release notes, license/attribution and generated
provenance. It never contains live qualification fixtures or both platform
binaries.

No tag or GitHub Release is created by these scripts or workflows. Publishing
`v0.3.0` remains a separate, explicitly authorized action.
