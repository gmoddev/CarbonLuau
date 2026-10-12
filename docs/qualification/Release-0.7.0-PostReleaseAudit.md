# v0.7.0 post-release audit — offline

Date: 2026-10-12. Starting source: `dabb872094f74e60d75de099379d198fb63a6bf8`.
Task class: published-artifact verification and forward documentation/packaging
fixes. **OFFLINE PASS; no new live-host qualification.** The
[release receipt](../Release-0.7.0-Validation.md) retains actual runtime, host,
durability and client evidence. No tag, released asset or historical receipt changed.

## Published identity and CI

Annotated `v0.7.0` object `7c7d36f648a5506efd07c1b6d6bcf4b94d3f7d2b` peels to
the starting source. The [GitHub prerelease](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.7.0)
is not draft and has exactly six uploaded assets. `targetCommitish: main` is not
the source pin; the tag and sourceRevision establish identity. Tag/source metadata
use GitHub no-reply identities. No publication or history rewrite occurred.

Manifest and provenance remain package `0.7.0`, API `0.7.0-experimental`, ABI
`1.5`, provider `1.2`, schema `1`, Luau `c6b830185af962c82003f86784e2fe036357c830`.
SQLite remains 3.53.4 and the recorded source ID / THREADSAFE=0,
OMIT_LOAD_EXTENSION, OMIT_WAL. The audit checks provenance declarations and
payload hashes, not freshly disassembled compiler options or a fresh binary build.

Previously pending release runs are now complete, all at the starting SHA:

| Trigger | Validation | Tooling | Contracts / docs |
|---|---|---|---|
| Final pre-publication dispatch | [38200778991](https://github.com/gmoddev/CarbonLuau/actions/runs/38200778991) SUCCESS | [38200778987](https://github.com/gmoddev/CarbonLuau/actions/runs/38200778987) SUCCESS | [38200778986](https://github.com/gmoddev/CarbonLuau/actions/runs/38200778986) SUCCESS |
| Main push | [38202205454](https://github.com/gmoddev/CarbonLuau/actions/runs/38202205454) SUCCESS | [38202205706](https://github.com/gmoddev/CarbonLuau/actions/runs/38202205706) SUCCESS | [38202205487](https://github.com/gmoddev/CarbonLuau/actions/runs/38202205487) SUCCESS; [docs 38202205466](https://github.com/gmoddev/CarbonLuau/actions/runs/38202205466) SUCCESS |
| Tag push | [38202705379](https://github.com/gmoddev/CarbonLuau/actions/runs/38202705379) SUCCESS | [38202705403](https://github.com/gmoddev/CarbonLuau/actions/runs/38202705403) SUCCESS | [38202705343](https://github.com/gmoddev/CarbonLuau/actions/runs/38202705343) SUCCESS |

New forward changes need their own final-source CI; the completion report supplies
those commit/run IDs. Earlier green runs are not substituted for it.

## Assets, source and author-reference parity

Current public asset digests match the previously downloaded immutable files.
[Test-PublishedRelease.py](../../tools/Test-PublishedRelease.py) independently
hashes both ZIPs and payloads, checks checksums and embedded/sidecar provenance,
reads the annotated source with git archive, and compares all **86** production
C# sources using the actual SharedSources.props allowlist, every public reference,
metadata, generated definitions and **69** Luau/addon example files.
Each bundle contains exactly **128 entries**, no unexpected production/fixture
payload; Linux workers retain Unix creator metadata and mode 0755.
Root document aliases and all four license/attribution texts also match tagged
source. Independent review found that the initial verifier checked attribution
filenames only; source-byte comparisons and four mismatch fixtures close that gap.

Text comparison permits only Git/working-tree CRLF versus LF normalization.
Payload/ZIP hashes are exact bytes. The verifier never extracts entries, loads
binaries, runs examples, contacts hosts or writes artifacts. Its 512-entry /
32-MiB compressed/expanded audit limits are not new production contracts.
Integrity matching is not publisher signing, runtime execution or durability proof.

| Asset suffix | Windows x64 SHA256 | Linux x64 SHA256 |
|---|---|---|
| `.zip` | `64b3fcfe0e7652325f6405e811bb6e2a9a92fc979ebfb7abf9bcb9c55a91e7d9` | `ed3ad4cf9937e628ea14661ce4a2e2c8b0ed61b372b250fab9753a357c846a48` |
| `.sha256` | `3381c34efa14c44aa938869341f9824429cd56cc8568ac6a062ce74a35ecc39a` | `1fd633e17e2da5177687dad1a85c23b6e51753bd9f1fa842d8b212658adf0658` |
| `.provenance.json` | `b84a24592153fdf11346d4b50b59dc4f8f01ed2e3dbb30da4c3b9ac7f1ef06fa` | `3c9304d5d444285a88b11f0c5d63acb7b14e2df3e692ea8834928771d959190b` |

Reproduce using an operator-selected download directory:

```powershell
python tools/Test-PublishedRelease.py --assets <DownloadedAssetsDirectory> --tag v0.7.0
pwsh -NoProfile -File tools/Test-ReleaseDocumentLinks.ps1
pwsh -NoProfile -File tools/Test-ToolingContracts.ps1
python tools/Test-Documents.py
```

Bindings/examples/metadata are unchanged. Pinned-VM example/runtime, 100-addon
and sanitizer qualification is inherited from unchanged release paths and
final-source CI; source parity is not new execution or authenticated-client proof.
No new VSIX, preview backend or API is introduced.

## Defects and forward corrections

- Current guides still called 0.5 the latest release, newer features development
  only, or completed C4 qualification pending. Correct current migration,
  discovery, inventory, release/navigation and register summaries. Retain original
  SinceApi introductions, protected record bodies and all negative evidence.
- Each immutable bundle has **128 unresolved relative Markdown links**: relocated
  root aliases retain source-relative navigation; evidence pages are not shipped.
  This is navigation damage, not missing production/reference files. Do not
  replace published ZIPs. Forward [ReleaseDocumentLinks.ps1](../../tools/ReleaseDocumentLinks.ps1)
  rewrites generated package documents only: bundled targets remain relative;
  unbundled evidence links use immutable source-revision GitHub URLs. External
  links/local anchors remain unchanged. Evidence fallbacks need internet; author
  reference/examples remain offline. Packaging checks now reject broken relative
  links and test relocation/escape handling.
- Early D21/D22/D23 register summaries were stale. Update routing to already
  adopted closure/capacity policy, not canonical semantics: current 2A owns the
  1-GiB DB extent, bounded journal extent and 2,560-MiB operational allocation
  budget. Do not resurrect the historical 1,280-MiB figure as current policy.

Rejected: republishing v0.7.0, moving tags, rewriting history, new identities,
or certifying loaded hook behavior / causal inventory from cold inspection.

## Offline checks and remaining gates

Published bundle/source audits PASS on both assets; malformed/path/count ZIP
negatives PASS. Link relocation, immutable fallback, anchor/external link and
escape fixtures PASS. Tooling schema positive/negative checks PASS. Windows
dry-run packaging from reused released payloads is deterministic and cleanly
extracts with matching hashes; all remaining relative document links resolve.
Its uncommitted-working-tree output is not published or replacement release evidence.
Linux helper fixtures and deterministic packaging / clean extraction also PASS
on BigKVM in an isolated 2-CPU / 4-GiB container using the same new helper sources,
the release's unchanged reference tree and reused qualified Linux binaries.
The first shared-clone attempt could not resolve Git alternates inside the
container; mounting only the existing object directory read-only corrected the
harness. No source/binary/contract change was needed. Both dry runs are unpublished;
final-head CI qualifies the combined corrected reference tree separately.
The task container exited and was removed; the isolated source cache is retained
under `/root/codex/post07-offline-20261012`, with no server/plugin installation.
Documentation validation preserves 97 retained spans and I1–I12.

No RustDedicated process, live fixture, plugin installation or server mutation
was performed. Existing caches/negative receipts and unrelated files remain.
GUI/Teleport authenticated-client, expanded Loot/attribution, causal inventory,
Shockbyte full-runtime, macOS server and power-loss limits are unchanged.

## Independent research and exact next tasks

- [D4 Loot expansion](../design/GameplayD4-LootExpansion.md): additional prefab /
  item activation still requires exact asset/configuration, loaded topology and
  Windows/Linux physical payout qualification.
- [B2 attribution](../design/GameplayB2-AttributionExpansion.md): next is a disabled
  bounded launch-to-direct-hit provenance model, not ID/owner inference.
- [Inventory](../design/GameplayC-InventoryObservationExpansion.md): next is a
  private own-adapter hint model / scheduling tradeoff, not operation events.

Next release/tooling work is separately scoped future qualification using the
corrected generator, or a matching editor-artifact phase. No new release/tag
or expanded production eligibility follows from this offline completion.
