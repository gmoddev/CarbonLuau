# Release identity and reproducibility

CarbonLuau v0.7.0 is the experimental package for the complete `0.7.0-experimental` API,
including the breaking PlayerInventory migration. It is not the earlier
World-only 0.6 backport. [Release notes](releases/0.7.0.md) describe its scope;
[qualification](Release-0.7.0-Validation.md) owns exact source/hosts, measured
results, preserved failures and final publication gates.

## Identity mapping

| Identity | Value |
|---|---|
| Package / annotated tag | `0.7.0` / `v0.7.0` |
| Published v0.7.0 scripting API | `CarbonLuau 0.7.0-experimental` |
| Current development scripting API | `CarbonLuau 0.7.5-experimental` (CATALOG-D9; no new release/tag) |
| API / GitHub release status | `Experimental` / prerelease, not stable |
| Native ABI | `1.5` |
| Provider protocol / addon schema | `CarbonLuau.Addons` / `1.2`; schema `1` |
| Luau pin | `c6b830185af962c82003f86784e2fe036357c830` |

[release.json](../release.json) is the machine-readable owner. CI checks the
managed plugin, native CMake version, API metadata, ABI, protocol and pin.
These are separate identities. The 2026-10-11 user decision authorizes the full
0.7 package only after final gates pass. Earlier tags/assets, curated history
and archive branches remain unchanged; use forward commits, never a main rewrite.
Earlier development bundles named v0.5.0 must not replace published v0.5.0 assets.

## Post-v0.7.0 development readiness

The published release scope below is historical. Development API
`0.7.5-experimental` adds the D4–D8 Loot expansion and D9 string catalogs;
it does not change published v0.7.0 assets. The separate editor candidate is
synchronized through its exact canonical tooling source pin. See the
[cross-repository readiness report](CrossRepo-ReleaseReadiness.md) for platform
qualification, reproducibility and the unresolved inventory CI release gate.
No next package version, release, tag or editor publication is assigned here.

## Gameplay A–D release readiness

The [release record](Release-0.7.0-Validation.md) owns combined qualification.
Scoped [A](GameplayFoundationA-Validation.md), [B1](GameplayB1-Validation.md),
[B2](GameplayB2-Validation.md), [B3](GameplayB3-Validation.md),
[C1](GameplayC1-Validation.md), [C2](GameplayC2-Validation.md),
[C3](GameplayC3-Validation.md), [C4](GameplayC4-Validation.md) and
[D3](GameplayD3-Validation.md) remain evidence for their actual revisions.
C2's completed-operation rejection remains valid: ItemChanged is sampled.

C4 removed Player HasItem/CountItem/GiveItem/TakeItem without aliases.
Use `Player:GetInventory()`; see the [migration guide](api/Inventory-Migration.md).
Existing read/acquisition/Signal introductions remain 0.6.5; facade mutations begin
at 0.7. Workspace/Entity/discovery retain 0.6.0, gameplay/Loot 0.6.5, and
persistence/Query 0.5. Inclusion here does not rewrite earlier availability.

The [0.6 planning notes](releases/0.6.0.md) retain the frozen World-only
candidate; no 0.6 package/tag was published. Persistence [1C](PersistenceFoundation1C.md)
and [2D](PersistenceFoundation2D.md) retain their unchanged contract/evidence;
[0.5 notes](releases/0.5.0.md) remain historical.

## Compatibility envelope

World/gameplay require the exact qualified Rust/Carbon/Harmony tuple and hook
topology, plus initial-startup observer continuity. First hotload/full plugin
unload requires restart for World; root/addon replacement and VM recovery retain
a continuous observer. Host updates require affected requalification.

Discovery is encounter-time, not atomic. ItemChanged is sampled physical net change.
Death/removal differ with no universal cross-Signal ordering. Entity killer
attribution is narrow original-connection synchronous RPC melee; unproved
provenance is nil. Loot supports one barrel prefab, scrap/metal.fragments and
Enum.DropMode.Replace: destruction-time payout replacement, not earlier drops.
Post-COMMIT partial payouts have no rollback, fallback, compensation or replay.

Persistence retains local PERSIST/EXTRA. Logical quotas do not reserve physical
capacity; file extents are hard bounded, allocated blocks have a qualified
operational budget, and indeterminate mutations are never replayed.
Process-crash evidence is not physical power-loss qualification.

The subsequent [D4 qualification](GameplayD4-Validation.md) adds only the exact
autospawn barrel-2 to current development builds under unchanged identities and
budgets. It does not change the published v0.7.0 scope, tag or assets. A future
release must separately qualify/package that extension; this task publishes no
release or tag and selects no future package version.

Authenticated-client GUI rendering/clicks/fonts/clipping/scrolling/reconciliation
and Teleport convergence remain unqualified and non-gating for this experimental
identity. No Shockbyte full-runtime or macOS server claim is made. TextBox is
not implemented. Broader Loot, world mutation, generic hooks, cloud persistence,
SQL and other deferred APIs remain outside the release.

## Reproduce a platform bundle

Build/test native x64 Release from clean final source and audit imports/ABI:

```powershell
./tools/Test-Release.ps1 -Rid win-x64 `
  -NativeLibrary ./build/win-x64/Release/carbonluau_native.dll `
  -CompilerWorker ./build/win-x64/Release/carbonluau_compiler.exe
./tools/New-ReleaseArtifacts.ps1 -Rid win-x64 `
  -NativeLibrary ./build/win-x64/Release/carbonluau_native.dll `
  -CompilerWorker ./build/win-x64/Release/carbonluau_compiler.exe
```

On Linux with PowerShell 7, select `linux-x64`, `libcarbonluau_native.so` and
`carbonluau_compiler` from the Linux build. The sibling storage worker is required.
The scripts create a ZIP, provenance JSON and SHA-256 sidecar in `dist/release`.
Entries are sorted with fixed timestamps; repeated packaging from identical inputs
must match. Different compiler builds are not promised identical binaries.

Bundles contain only the production .cszip, matching runtime/compiler/storage
workers, public examples/reference, canonical metadata/generated definitions,
installation/release notes and attribution. No fixture DB/journal, host assembly,
SQLite CLI, probe, credential, log, tooling executable or VSIX is allowed.
Linux workers have Unix ZIP creator metadata and mode 0755. Clean extraction
checks payload hashes and executes the real-VM install/example fixtures.

## Publication gate

Scripts/workflows do not publish releases. Require clean/synchronized tested source,
final-source Windows/Linux/native/managed/real-VM/live/sanitizer/tooling/documentation
checks, deterministic committed-source archives and clean installs before tagging.
Provenance records the exact final revision and tested payloads.

Create annotated `v0.7.0` at that commit, push without moving historical tags,
and publish only a GitHub prerelease with qualified ZIPs/checksums/provenance.
Download public assets and compare hashes. Never publish a partially qualified release.

Annotated v0.7.0 was published at `dabb872094f74e60d75de099379d198fb63a6bf8`.
The [post-release audit](qualification/Release-0.7.0-PostReleaseAudit.md) confirms
its identities/assets and final CI. Forward bundle generation corrects relocated
document links; released v0.7.0 ZIPs remain immutable. This navigation fix does
not authorize a new release or expand host/API support.

## Matching editor artifacts

The published v0.4.0 VSIX is not qualified for the full v0.7.0 API. A matching
extension needs separate source-pin, pack, installed-extension and publisher
qualification. Windows/Linux analysis/preview do not imply macOS server support;
macOS tooling remains static-only. No new VSIX, Marketplace or Open VSX publication
is included here. Hashes/annotated tags establish identity/integrity, not publisher
signing, OS sandboxing or stable production readiness.
