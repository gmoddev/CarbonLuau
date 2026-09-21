# Tooling baseline adoption evidence

Verdict: **CANONICAL BASELINE READY** for contract/bootstrap adoption.
Tooling Foundation A production behavior is still planned. This verdict does not
claim Core extraction, a runtime-complete catalog, LSP integration or usable preview.

## Source and isolation

- Inspected/start commit: `3f6a3196b28e2243dc8802d3b8ff2374196f3c89`.
- Supporting design reference: `1ec3287ea538641229c794e46a4d327784afb6fa`.
- Worktree: `CarbonLuau-tooling-baseline`; branch `codex/tooling-baseline-20260921`.
- The active `RustCarbonLuau` main checkout was not edited, staged, reset or switched.
  Another task's `docs/PlayerInteractionFoundation1FB-Checkpoint.md` was observed
  there and left untouched. Future integration must reconcile that task's source
  and API changes rather than treating this inspection revision as latest forever.
- The complete supplied design was preserved with content equality after line-ending
  normalization. Task-specific phase/command routing and newer TakeItem status are
  explicitly reconciled in [ToolingBaseline](ToolingBaseline.md).

## Local checks (2026-09-21, Windows x64)

| Check | Result / limit |
|---|---|
| `pwsh -NoProfile -File tools/Test-ToolingContracts.ps1` | PASS valid API-model fixture and rejection of unknown schema, missing method signature, missing property type, unknown qualification and undeclared numeric policy |
| `pwsh -NoProfile -File tools/Test-Api.ps1` | PASS existing API identity/surface/examples and relative-link audit |
| `pwsh -NoProfile -File tools/Test-Architecture.ps1` | PASS existing production invariant-owner/source-structure audit |
| `git diff --check` | PASS |
| Production diff vs start | No changes in src, native, scripts, release.json, tests, package.ps1 or Test-Package.ps1 |
| Two `tools/package.ps1` runs into isolated build folders | Identical SHA-256 `5379e44ddc3f94cf7a9121c08d7d494d6a94b0c4beda84803d63b593529e1605` |
| `tools/Test-Package.ps1` | PASS exactly 38 production C# files, no tooling/schema/docs/native/fixture files |
| Extension `npm ci --ignore-scripts` / `npm run check` | PASS locked install, TypeScript build, ESLint, manifest contract and 3 bootstrap tests |
| Extension `vsce ls --tree` (3.6.0) | PASS packaging inventory inspection only; no VSIX built or published |

Extension checks ran with Node 20.18.0, npm 11.4.1, TypeScript 5.9.3 and ESLint
9.39.1. The pinned linter reports upstream deprecation. The temporary vsce tool's
dependency reports a Node >=20.18.1 engine warning; inventory still completed.
These do not establish release toolchain qualification. Extension CI uses Node 22
on Windows/Linux/macOS; its results must be read separately from these local checks.

The schema fixture is **not** a runtime API catalog. Generated definitions, metadata
determinism and negative runtime/catalog drift tests are Foundation A exit gates,
not passed tests in this baseline. The schema/generation/audit seams and exact
implementation plan are in [ToolingContracts](ToolingContracts.md) and
[ToolingFoundationA](ToolingFoundationA.md).

No shared production source moved, so package/addon/GUI runtime suites and native/
live-server qualification were not rerun. Existing evidence retains its original
limits. Carbon's broad push CI can be skipped for this docs/schema-only adoption;
the added targeted schema workflow is available for future qualifying changes.

## Identity and scope confirmation

Unchanged: package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`,
provider protocol `CarbonLuau.Addons/1.2`, package schema `1`, runtime Luau
`c6b830185af962c82003f86784e2fe036357c830`. New protocol/model reservations do not
modify these identities. Extension `0.0.1` is an unpublished bootstrap identity;
no actual tooling-pack release or LSP version is qualified/assigned.

No GUI preview execution, WebView, mock Players/Items, live-server integration,
debugger, visual authoring, asset download, telemetry, VSIX or Marketplace/Open VSX
publication began. Extension activation acquires no resources or host modules.
No persistent preview/build/server process was started.

Repository ownership, Core/host/editor boundaries, API/generation/LSP policy,
project/dependency model, protocol, worker bounds, preview-plan/fidelity/WebView,
Trust, pack/platform/offline/privacy and later routing are canonically recorded in
[ToolingBaseline](ToolingBaseline.md); do not restate them in future prompts.
