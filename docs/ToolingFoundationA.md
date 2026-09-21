# Tooling Foundation A implementation design

Status: partial implementation is preserved in isolated worktrees and remains
unqualified. The [analysis security amendment](ToolingLanguageAnalysisSecurity.md)
resolves its original non-execution stop condition and is the required security
handoff before resuming. No executable implementation ships in this documentation
branch. Start with [AICONTEXT](../AICONTEXT.md),
[D19](Invariants.md#d19--official-editor-tooling), [baseline](ToolingBaseline.md)
and [contract models](ToolingContracts.md). These tooling phases are separate
from historical runtime/addon Foundation A–G and GUI foundations.

## Inspected source and extraction sequence

Inspected CarbonLuau commit: `3f6a3196b28e2243dc8802d3b8ff2374196f3c89`.
The server uses nested types in partial `Carbon.Plugins.CarbonLuau`, source ZIP
packaging and net48/C# 7.3 tests; there is no production plugin csproj to turn into
a net10 library. Reconcile changes since this revision before extracting.

1. Freeze package, path, descriptor, retained/layout and API binding fixtures at
   the implementation revision. Preserve observed qualification limits.
2. Introduce a pure `src/CarbonLuau.Core/CarbonLuau.Core.csproj` targeting
   `net48;net10.0` with C# 7.3-compatible source. The preserved partial work
   corrected the original netstandard2.0 proposal because existing ZIP
   ExternalAttributes checks are unavailable there; preserve production checks
   and dependencies. Tooling references the assembly;
   Carbon's source ZIP compiles the same Core source with thin plugin adapters.
   Prove actual Carbon source compiler support before replacing production paths.
   No production runtime/library dependency upgrade is authorized by this plan.
3. Extract the smallest package/policy slice, preserving existing nested public
   provider entry types as wrappers where external callers rely on them. Then
   extract descriptors/values and pure retained/layout state behind snapshot inputs.
4. Add authoritative metadata/catalog generation and bidirectional API drift tests.
5. Add static project/metadata transport and the extension's detection/diagnostic
   integration. LSP analysis requires the D19 supervised snapshot process, trust
   transitions, owned configuration and bounded proxy. Preview execution stays
   out of Foundation A; do not classify type-function evaluation as static.

| Current files / exact seam | Destination and necessary split |
|---|---|
| `src/CarbonLuau/Addons/AddonPackage.cs`: AddonPolicy, AddonPackageSnapshot, ParseManifest, ZIP validation | Core/Addons: pure manifest/archive snapshot/parser, ID/version/path/size policy. Keep registration state/provider protocol/lifetime policy in plugin. Split ToScriptSnapshot adapter to remove runtime coupling. Preserve byte checks, strict UTF-8, hashing, ordering and rejection behavior. |
| `src/CarbonLuau/Scripts/ScriptSnapshot.cs`: ValidatePath, snapshot data, Resolve/Load | Core/Modules: canonical logical path rules and immutable source model. Move filesystem snapshot admission into tooling/host adapters with the same symlink/reparse checks; keep RuntimeConfig-coupled server discovery outside Core. |
| `src/CarbonLuau/Addons/DependencyGraph.cs`, `AddonActivation.cs`, `AddonRegistry.cs` | Extract pure declared-ID/publicModules/main resolution rules only. Leave provider registration, exact lifetime binding, activation/loss/replacement and VM publication in plugin. Tooling workspace graph consumes shared legality without copying runtime activation. |
| `src/CarbonLuau/Gui/GuiDescriptors.cs`, `GuiImageSource.cs` | Core/Gui: descriptors, enum IDs, immutable image/value identity. No asset fetching or server lookup. Preserve IDs and wire/bootstrap spellings. |
| `src/CarbonLuau/Gui/GuiConfig.cs` | Core/Policy: immutable GuiLimits and pure validation/defaults; host configuration ingress stays adapter-owned. Runtime limit values retain their canonical owner. |
| `src/CarbonLuau/Gui/GuiRenderPlan.cs`, `GuiModelContracts.cs` | Core/Gui: pure values/render/accounting contracts. Split host identity/backend-specific contracts first; do not expose transport IDs in preview. |
| `src/CarbonLuau/Gui/GuiRetainedRegistry.cs`: GuiStoredValue, GuiRetainedNode, GuiRetainedState | Separate pure tree/value snapshot from Presentations, Connections, staged effects and PendingDestroys. GuiStoredValue.Encode currently references FacadeException; use a pure validation result/exception mapped by adapter. Do not move GuiRetainedWorld wholesale: it owns PlayerDirectory, backend actions and registry lifetimes. |
| `src/CarbonLuau/Gui/GuiPresentation.cs`: GuiRenderCompiler, ProjectChildren, ProjectGridChildren, affine/padding/clip helpers | Extract deterministic geometry/accounting over pure tree snapshots. Compile currently accepts GuiPresentation, which carries PlayerToken/UserId, CUI names and action tokens. Split projection from delivery decoration; production adapter adds those fields after shared projection. Tooling viewport resolution consumes the same projection, not a copy of these algorithms. |
| `src/CarbonLuau/Gui/IGuiBackend.cs`, `InMemoryGuiBackend.cs` | Retain test/adapter seams, extracting only Carbon-independent data contracts required by Core. Production RustCuiBackend and delivery stay in plugin. |
| `src/CarbonLuau/Facade/FacadePolicy.cs`, `scripts/bootstrap.luau`, native facade registration | Extract only pure API identity/policy exports and explicit binding-contract declarations. Do not move FacadeSession, PlayerDirectory, inventory/teleport implementations, NativeFacade or runtime loaders into Core. |

Module legality crosses managed and native/bootstrap boundaries today; inspect the
selected runtime resolver and registration points before changing it. Core must
own shared semantic rules or generated tables consumed by those paths. A second
tooling-only resolver that merely resembles the runtime is not completion.

## Projects and generation

Proposed `src/CarbonLuau.Tooling/CarbonLuau.Tooling.csproj`: net10.0 executable,
assembly name `carbonluau-tooling`, references Core only plus explicitly pinned
non-server dependencies. Folders: Protocol, ProjectModel, Validation, Metadata;
Packaging and Preview are later phases. The static host does not load a VM or
server assemblies. Core must build on all developer targets without Carbon SDKs.

Populate `api/carbonluau-api.json` against the existing schema, deriving GUI
declaration shape and limits from existing canonical descriptors/policies. Use
explicit binding contract IDs for bootstrap/native public surface; generate
registration declaration tables where feasible. Handwritten docs summaries/types
belong to the single catalog, with runtime signature/behavior tests closing the
gap. Do not maintain two manual lists of exposed members. Stop if declarations
cannot share a source or be audited exactly without unsupported inference.

Introduce a pinned generator under `tools/Generate-Api` (implementation language
chosen with the Core exporter) with `--check`; emit definitions/LSP docs/reference
tables/tooling metadata as defined in the contract. For any shipped metadata,
assert exact source revision and release identities, declaration membership in
both directions, type/method/Signal shape and qualification. Deferred APIs must
not appear as shipped. Catalog availability must include this revision's TakeItem
without claiming authenticated-client convergence. Negative fixtures intentionally
add/remove/change a binding/signature/limit and must make the audit fail.

## Extension implementation sequence

Repository `gmoddev/carbonluau-vscode` starts with package.json, lockfile,
tsconfig.json, ESLint configuration, `extension/Extension.ts`, validation scripts
and CI. Only activate/deactivate exports use upstream lowercase lifecycle names;
project-owned symbols use PascalCase. Bootstrap activation does nothing and
registers no unavailable commands/settings. No server, workspace execution or
network dependency exists at activation.

Foundation A adds ProjectManager, LanguageClient, Diagnostics, ToolingClient and
Commands directories as needed, not empty speculative implementations. Package
contributions use workspace extension kind, limited Workspace Trust and `.luau`
language activation/root addon discovery. No WebView folder/renderer is required.

Detection flow: VS Code folder/document events -> scoped addon.json and .luau
candidate watchers -> immutable buffers/manifest snapshot -> host canonical
validation -> exact-ID project index -> dependency/module map -> LSP adapter.
Preserve invalid project candidates for diagnostics. Hash source/dependencies/API
and reject stale results. Missing local dependencies and invalid packages are
different diagnostics. Watch only relevant roots; external mappings require trust.

LSP distribution: bundled verified upstream executable per platform in a qualified
pack, spawned by the tooling supervisor with a controlled argument vector over stdio, standard
Luau mode, pinned generated definitions/docs and trusted transform. Admit bounded
snapshots only after Workspace Trust; apply the analysis security amendment's
configuration ancestry, proxy, process limit and cleanup gates. No shell
interpolation/workspace executable/plugin path. Pick the actual upstream version,
embedded Luau commit, flags and plugin API by corpus tests, not the design's
historical release example. Runtime compiler acceptance remains authoritative;
Foundation A may report compiler checking unavailable until a qualified isolated
compiler path exists, never report LSP acceptance as runtime validation.

Diagnostic flow: host structured package/module/API diagnostics and LSP language
diagnostics -> revision/source mapping -> Problems. Coalesce generic unresolved
requires only when a precise canonical diagnostic supersedes them. Do not discard
unrelated type errors. Unlocated errors go to bounded Output, never modal windows.

Foundation A needs a minimal coordinator transport for metadata/static validation
to make language integration useful. Implement initialize/getMetadata/
validateProject/resolveProjectGraph/shutdown there with bounded protocol tests;
Foundation B expands supervision and preview. This dependency adjustment keeps
the task's A language/diagnostic scope executable without pushing semantics into
TypeScript. No preview execution or addon build command is pulled into A.

## Validation and CI gates

- Core dependency scan rejects Carbon/Rust/Unity references, plugin facade types
  and accidental production native loading. Compile Core net48/net10.0 plus production net48
  source composition and tooling net10.0 on appropriate workers.
- Existing `tests/runtime/AddonTests.cs`, `FoundationETests.cs`, ScriptTests,
  GUI Foundation 1–3 and facade fixtures must pass after extraction. Add differential
  package/path acceptance and retained/layout/clip/accounting goldens on identical
  inputs; preserve 7-element scrolling cost and current resource limits.
- Update `tests/runtime/RuntimeTests.csproj` and `tests/managed/LoaderTests.csproj`
  source includes deliberately; wrappers retain public provider type identity.
- Update `tools/package.ps1` and Test-Package only when Core moves: explicit shared
  source allowlist, reject duplicate flattened C# basenames, exclude tooling/native/
  metadata files, deterministic repeated ZIP hashes. Requalify source deployment
  in real Carbon after composition changes; unchanged archive content is stronger
  evidence than a successful modern .NET build alone.
- Metadata schema/relationship checks, repeat generation byte equality, clean
  `--check`, negative binding drift fixtures and generated docs links gate CI.
- LSP corpus: service/Create singleton inference, Signals/value types, selected API,
  local/private/public/dependency requires, ambiguity, absent optional dependency,
  source-map locations and runtime compiler/LSP disagreement.
- Protocol boundary tests: oversized headers/bodies, bad IDs/JSON/versions/schema,
  queue pressure, cancellation/stale revisions and graceful shutdown. No script
  executes in static validation, including Restricted Mode. Trusted executable
  analysis additionally passes the [D19 security matrix](ToolingLanguageAnalysisSecurity.md#20-security-qualification-matrix).
- Extension: npm ci, TypeScript build, ESLint, manifest checks, trust/activation
  tests, then VS Code E2E for standalone/root/addon/multi-root and unsupported API.
- Carbon CI gets a separate tooling job; do not widen server-release globs.
  Extension CI tests Windows/Linux/macOS; packaging and worker platform support
  remain unqualified until later gates. Follow worker rules for sustained native
  builds/tests. No unrelated server rebuild for this docs/bootstrap baseline.

## Canonical phase routing

| Phase | Scope / exit |
|---|---|
| Baseline (this change) | ownership/rules/contracts, schema seam, concrete A plan, compiling inert extension; no Core extraction or runtime behavior |
| Tooling A | shared Core extraction, API catalog/definitions, project detection, qualified LSP/analysis adapter, minimal static host and canonical package/module diagnostics; parity, generation/drift and trust tests pass |
| Tooling B | full coordinator/supervision, bounded fresh preview worker, canonical execution and ToolingPreviewPlan; protocol/resource/security and semantic differential gates pass |
| Tooling C | paint-only WebView, presets, hierarchy/retained/projected/resource inspector and source navigation; injection/CSP/fidelity/E2E gates pass |
| Tooling D (optional, post-v1 capability) | versioned fixtures, mock Player/Items and local Activated simulation; separate interaction/security gates; not required for v1 distribution |
| Tooling E | canonical deterministic Build Addon (validate/snapshot/build/reparse/hash), distribution/offline/platform qualification, publisher readiness and onboarding; publish only explicitly qualified targets |
| Later | authenticated operator-enabled live integration, DAP and proposed visual source edits; independently scoped |

Supporting design phase translation: its A -> task A extraction; B -> task A
language; C -> task A static transport / B coordinator / E packaging; D -> task B
preview; E -> task C UX / E release; later F -> task D; later G -> Later.
This avoids accidentally treating fixtures as a v1 prerequisite or interpreting
the supporting design's old letters as new implementation authority.

Stop affected implementation if Core becomes the whole plugin, server assemblies
are required, TypeScript would duplicate semantics, metadata needs unaudited
manual runtime duplication, LSP requires changing runtime require, execution
would enter extension/coordinator/WebView, analysis would bypass D19 trust/process/
snapshot policy, or macOS requires server dependencies.
Report evidence and rule owner instead of silently weakening the baseline.
