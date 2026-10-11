# Release identity and reproducibility

Current development source targets scripting API `0.7.0-experimental` for the
explicitly authorized breaking PlayerInventory migration. Inventory operations
are `CountItem`, `HasItem`, `GiveItem`, `TakeItem` and sampled `ItemChanged`, acquired through
`Player:GetInventory()`. Legacy Player HasItem/CountItem/GiveItem/TakeItem are
removed without aliases. See the [migration guide](api/Inventory-Migration.md)
and [PlayerInventory contract](api/Types/PlayerInventory.md).
The preceding Workspace/Entity introduction remains `0.6.0-experimental`;
Gameplay A/B, Loot and C1/C3 introductions remain `0.6.5-experimental`.
The API bump is a breaking compatibility decision, not an automatic package release.
The latest published
package/tag remains `0.5.0`/`v0.5.0` with API `0.5.0-experimental`.
Development `release.json` deliberately has package/release/tag `0.5.0` and
API `0.7.0-experimental`; it is **not** a v0.5.0 reproducibility or publication
manifest. No v0.6.0 package, tag, release or editor artifact is authorized here.
The native ABI remains `1.5`, provider protocol `1.2`, addon schema `1`, and
the Luau pin unchanged.

[World/Entity Foundation 2C](WorldEntityFoundation2C.md) owns the final combined
qualification of its frozen `fc71db4` source and next-package readiness verdict.
Gameplay A–D or C4 is not retroactively qualified by that source. The callback-root
publication correction discovered during Gameplay A also needs affected
requalification/backport before any publication of the older candidate.
[Planned v0.6.0 release
notes](releases/0.6.0.md) retain that World scope and audit the later Gameplay A–D
and breaking C4 migration scope without reassigning historical evidence.
Publication remains a separate authorization. Its narrow identity change must
update `release.json`, the plugin Info/PackageVersion and native CMake package
version, current-release documentation/navigation, and hardcoded bundle/artifact
names in the validation workflow. Select the release scope explicitly: a frozen
v0.6.0 World-only backport keeps API `0.6.0-experimental` after affected
requalification; a package containing the C4 migration uses API
`0.7.0-experimental`. The next package number remains a separate publication
decision; the planned 0.6.0 filename does not authorize release scope. Keep ABI
`1.5`, provider `1.2`, schema `1` and Luau unchanged, then rebuild/test that exact release
commit and verify checksums/provenance before any tag or GitHub release.
Development dry-run archives named v0.5.0 contain the development API and must
never be uploaded as replacement v0.5.0 artifacts.

The v0.5.0 experimental mapping follows [D12](Invariants.md#d12--scripting-and-protocol-identity),
qualified [Persistence Foundation 1C](PersistenceFoundation1C.md) and
[Persistence Foundation 2D](PersistenceFoundation2D.md). The 2D source/CI gate
closed before the separate v0.5.0 release gate. D20 Entity is not a persistence
release prerequisite. The [D22 Query contract](PersistenceFoundation2.md)
joins scripting API `0.5.0-experimental` with structured field/value requests,
automatic preparation and optional field hints. No public schema, index or
migration version was introduced. The [0.5.0 release notes](releases/0.5.0.md)
own the experimental author-facing summary.

| Identity | Value |
|---|---|
| Package / tag | `0.5.0` / `v0.5.0` |
| Published scripting API | `CarbonLuau 0.5.0-experimental` |
| Development scripting API | `CarbonLuau 0.7.0-experimental` |
| API status | `Experimental` |
| Native ABI | `1.5` (additive reserved persistence-completion export) |
| Provider protocol | `CarbonLuau.Addons` / `1.2` |
| Addon package schema | `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

Published v0.3.0 remains the gameplay-facade baseline. Addon, GUI and Player
declarations keep their historical introduction versions through 0.4. Removed
Player inventory spellings remain in published release history, not current
bindings. Existing PlayerInventory acquisition/read/Signal introductions remain
0.6.5; new `PlayerInventory:GiveItem` and `PlayerInventory:TakeItem` are introduced at 0.7.
Persistence is explicitly introduced at 0.5, without retroactive availability or a stable 1.0
claim. Package, scripting API, native ABI, provider protocol, package schema and
pinned Luau are separate compatibility identities. [release.json](https://github.com/gmoddev/CarbonLuau/blob/main/release.json)
is the machine-readable owner of the mapping, and CI checks it against source and
documentation.

`tools/Test-Release.ps1` requires releaseVersion/packageVersion/tag to agree but
does not require them to equal the scripting API version. The v0.5.0 release
manifest and exact-source artifacts carry API `0.5.0-experimental` and ABI `1.5`
in provenance. The ABI 1.5 change was
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

## Gameplay A–D release readiness

This audit uses the qualified C3 baseline `cf86b91` and the linked immutable
phase records. It does not turn prior PASS receipts into final C4-source proof.

| Surface | Recorded qualification | Release consequence |
|---|---|---|
| A: PlayerDied/PlayerSpawned | [Gameplay A](GameplayFoundationA-Validation.md): exact-host Windows/Linux server, real-VM, lifecycle/resource, sanitizers, tooling, clean-install and implementation-source CI PASS. | Authenticated-client observations remain unqualified; introduction remains 0.6.5. |
| B: EntitySpawned/EntityDied/EntityDestroyed | [B1](GameplayB1-Validation.md), [B2](GameplayB2-Validation.md), [B3](GameplayB3-Validation.md): separate completion/lifetime/source, platform and hosted gates PASS. | Death/removal differ; B2 actor attribution is narrow original-connection synchronous RPC melee only. Keep pinned-host/startup requirements and best-effort delivery limits. |
| C: inventory acquisition/reads and sampled ItemChanged | [C1](GameplayC1-Validation.md) PASS; [C2](GameplayC2-Validation.md) historical PARTIAL for complete-operation observation; [C3](GameplayC3-Validation.md) combined sampled PASS with exact-source Windows/Linux live/VM, regressions, sanitizers, tooling, clean-install and hosted receipts. | Sampled semantics remain explicit. C3 does not qualify C4 method removal or mutation-facade admission. |
| D: Loot Replace | [D2A](GameplayD2A-Validation.md) private strict Replace PASS; [D3](GameplayD3-Validation.md) public conversion/publication, Windows/Linux physical/VM, sanitizer/tooling/package and final-source hosted PASS. | Only the allowlisted barrel prefab, scrap/metal.fragments and Enum.DropMode.Replace are supported. Partial payout has no rollback or vanilla fallback after COMMIT. |
| C4: breaking inventory migration | [C4 evidence](GameplayC4-Validation.md): PASS final-source Windows/Linux real-VM/live, regressions, sanitizers, metadata/tooling, deterministic clean installs and hosted CI under API 0.7. | Legacy runtime/metadata/definition methods are absent. D13 and sampled C3 remain unchanged. Subsystem readiness is not package publication. |

The A–D feature set is broader than the frozen World-only 2C candidate. C4 closes
the inventory migration and affected combined regression gates at its recorded
implementation revision; prior host proofs remain attributed to their actual
revisions. A later package/release task must still qualify its own final identity-
aligned artifacts and scope, preserve experimental/client limits and obtain final
release-source checks. No historical receipt is relabeled as evidence for changed
code or an authenticated client.

Package preparation must select and align package/tag/artifact identities, API 0.7
for C4-containing source, documentation navigation and bundle contents. Rebuild and
verify provenance/checksums from that exact release commit. Current development
archives still named v0.5.0 cannot replace published v0.5.0 artifacts. Matching
editor artifacts require their own source pin and qualification. Publication/tag
authorization is still separate; no Gameplay A–D phase receipt grants it.

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

The archive contains only the production `.cszip`, the matching native runtime,
compiler worker and private storage worker,
examples, installation/release notes, license/attribution and generated
provenance. It never contains live qualification fixtures or both platform
binaries.

No tag or GitHub Release is created by these scripts or workflows. The published
v0.5.0 tag/prerelease is historical. Any next publication requires separate
authorization after the exact-release-commit gate.

## Matching editor artifacts

The extension's `tooling-source.json` pins the exact v0.4.0 runtime candidate. Its
`tools/Package.py` creates platform VSIX files from the canonical pack, records
both commits and payload identities, normalizes ZIP metadata, and checks repeated
packaging for identical hashes. Extension version `0.0.1` and tooling pack
`0.4.0-rc.1` describe the earlier tooling candidate and are independent of the
then-current scripting API `0.5.0-experimental` and protocol
`CarbonLuau.Tooling/1.0`; none changes the runtime ABI or package schema.
Windows/Linux x64 VSIX artifacts require clean installed-extension E2E; macOS
arm64 artifacts remain explicitly static-only. Hashes establish integrity, not
publisher signing or OS sandboxing. The v0.4.0 VSIX bits are not qualified for
the v0.5.0 persistence/Query API and are not attached to this runtime release.
No Marketplace or Open VSX publication is authorized by this task; a matching
extension requires its own source pin, rebuild and qualification.
