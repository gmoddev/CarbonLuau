# Official CarbonLuau tooling baseline

Accepted under [D19](Invariants.md#d19--official-editor-tooling). This document is
the canonical tooling decision owner; runtime semantics remain in Invariants.
Implementation gates and exact source routing are in
[ToolingFoundationA.md](ToolingFoundationA.md). Baseline means contracts and a
minimal extension bootstrap, not completed Foundation A or a usable tooling pack.

The subsequent partial Foundation A implementation encountered executable LSP
configuration/type functions. [D19 analysis security](ToolingLanguageAnalysisSecurity.md)
resolves that architectural stop condition with a trusted-only supervised snapshot
profile. The implementation remains unqualified and its LSP startup stays disabled
until the amended gates pass. That amendment owns the exact analysis policy;
historical non-execution wording applies to StaticInspection, not all type analysis.

## Provenance and precedence

The complete [approved architecture](design/CarbonLuau_VSCode_Tooling_Architecture.md)
is preserved as supporting evidence, researched at
`1ec3287ea538641229c794e46a4d327784afb6fa`. Adoption was inspected at
`3f6a3196b28e2243dc8802d3b8ff2374196f3c89`. That newer source implements TakeItem;
the historical design's exclusion of TakeItem is stale. Future metadata must
include the selected revision's implemented TakeItem with its qualification limits;
GiveItem and TextBox remain unavailable at this adoption revision.

The implementation task explicitly supplies the A–E routing below, replacing the
supporting design's phase labels, not its technical ownership. It also reserves
Open GUI Inspector, while design section 31 integrates the inspector into Preview:
reserve it as a future focus/open alias for the same panel, not a second inspector.
The task's illustrative `getApiMetadata` operation is the API portion of the
design's `getMetadata`; protocol v1 reserves `getMetadata`, avoiding two aliases.

## Repository and shared-code ownership

| Owner | Responsibilities |
|---|---|
| `gmoddev/CarbonLuau` | CarbonLuau.Core, canonical API catalog, derived definitions/docs/limits, exact package/path/module policies, GUI descriptors/retained/layout/accounting semantics, tooling host/native bridge, stdio server, worker supervision and ToolingPreviewPlan generation |
| `gmoddev/carbonluau-vscode` | activation, workspace UX, project candidate discovery, upstream LSP client/analysis adapter, diagnostics presentation, commands/tasks, process client, future WebView/inspector, source navigation, configuration and VSIX packaging |

Core is a small Carbon-independent library/source set, not the plugin in disguise.
Share immutable values/descriptors and pure package/manifest/path validation,
retained-tree rules, layout/projection and accounting. Keep Carbon plugin objects,
BasePlayer, Item/ItemContainer, Unity objects, provider lifetimes, live hooks,
production CUI delivery/action tokens and native library loading outside Core.
Preserve production source `.cszip` deployment and its net48/C# 7.3 compatibility;
the modern tooling host is not a server-runtime upgrade.

If a rule affects project/package/GUI validity, module visibility, deterministic
geometry or resource/projection accounting, canonical code/generated metadata
owns it. TypeScript may find candidates or offer convenience hints, but host
validation is authoritative. It may not independently decide UDim/UDim2 geometry,
list/grid/padding, clipping topology/depth or projection cost.

## API contract and language tooling

[API contract model](ToolingContracts.md#api-catalog) defines schema 1. The catalog
will live at `api/carbonluau-api.json`; the schema exists at
[carbonluau-api.schema.json](../api/carbonluau-api.schema.json). This baseline
deliberately supplies no incomplete catalog claiming to describe the runtime.
Names, signatures, docs summaries, availability and tooling classifications have
one catalog; implementation behavior remains code. Numeric limits export from
canonical policy/descriptors, not hand-maintained catalog or TypeScript constants.
No Markdown scraping or runtime reflection is an API source.

Generate `.d.luau`, LSP docs, reference tables and tooling metadata deterministically.
Runtime binding/export assertions must prove both directions of catalog coverage;
GUI metadata must match descriptor IDs, members and types. Generation alone cannot
prove correctness. Negative drift fixtures must fail CI before the catalog ships.

Use pinned upstream `luau-lsp` in standard Luau mode with Roblox/Studio integration
disabled. No typechecker fork or required second extension. Its definition format
is only a generated adapter. Track LSP version and exact embedded Luau revision
separately from CarbonLuau's compiler/VM pin. The latter owns runtime compile
diagnostics. Qualify parser/type deltas, service/Create overload inference and
definition parsing against the selected pack; no LSP release is qualified here.

The extension-owned trusted require adapter consumes an exact legal module map
from canonical tooling. Literal requires are transformed only for analysis;
runtime source, visibility, imports and package contents never change. Pin the
experimental plugin API, adapter/configuration revision and source-map behavior
inside each tooling pack. Workspace code/configuration cannot select plugins.
Executable LSP analysis, including this trusted transform, runs only in the
separate supervised language process after Workspace Trust. Its snapshot/config,
type-function, proxy, resource and platform rules are defined by the
[analysis security amendment](ToolingLanguageAnalysisSecurity.md). No workspace
`.config.luau`, `.luaurc`, `.robloxrc` or arbitrary LSP/plugin settings enter that
session. Generated configuration is data-only and owned by CarbonLuau.

## Project discovery and local dependencies

Discover `addon.json` candidates (including malformed/missing-entry projects so
they receive diagnostics), validate `init.luau` and manifests canonically, and index
multiple addons across workspace folders. Inspect `.claddon` as built artifacts.
Unowned root-script trees and standalone `.luau` files need no new project file.
Standalone language support does not imply package-only operations are available.

Select explicit `carbonLuau.apiVersion`, then exact locally cached selection,
then bundled API. Unknown API/schema fails closed with a visible diagnostic;
never silently substitute. Schema-1 manifests gain no new required API field.

Bind a declared required/optional dependency only to one canonically valid local
addon with that exact ID. Duplicate IDs are ambiguous and bind neither; absent
local source is unresolved for analysis/preview, not proof a deployable package
is invalid (the server/provider may supply it). `@id` needs dependency main;
`@id/path` needs an exact public export. Local module ownership remains the
defining addon. Imports never activate packages. No registry, downloader, lockfile,
version solver, aliases that change runtime semantics, or root-addon dependency.
Optional `carbonLuau.localDependencies` maps IDs to external development folders;
ignore it in Restricted Mode. Archive analysis uses bounded immutable snapshots,
not arbitrary ZIP extraction.

## Host, protocol and execution security

`src/CarbonLuau.Tooling` is C#/.NET 10 LTS, self-contained per platform; NativeAOT
is optional after qualification. The long-lived `carbonluau-tooling --stdio`
coordinator owns validation, package inspection/build, metadata, graph resolution,
version negotiation and worker supervision. It must not depend on Carbon/Rust
assemblies or RustCuiBackend. [Protocol v1](ToolingContracts.md#local-protocol)
uses bounded Content-Length JSON framing over local stdio, never HTTP/WebSocket
or Rust CUI JSON. No operation is implemented by this baseline.

Workspace Luau is untrusted. Preview runs only in the same executable's fresh
`--preview-worker` child mode with a fresh VM/domain/module-cache lifecycle for
each snapshot. Never execute it in extension host, WebView or coordinator.
Language analysis is a distinct trust class with its own supervised LSP process,
not preview execution. The limits and fresh-worker rules in the following
paragraphs apply to preview; do not apply them as undocumented LSP limits.
Default limits are 1 second hard execution wall time, 64 MiB Luau heap and
256 MiB process ceiling, with 8 MiB framed input/result bounds. Heap policy is
internally versioned/configurable within the accepted 16..256 MiB range; defaults
are tooling policy, not server-runtime limit changes. Source/package limits come
from canonical code. Bound queues, logs, outstanding work and retained results.

The supervisor independently enforces deadline/process limits (Windows Job
Objects and qualified Linux/macOS facilities), kills and reaps timed-out, canceled,
oversized or invalid workers, and translates crashes into bounded diagnostics.
A cooperative VM interrupt alone is insufficient. Fail closed on unsupported
containment/platform or identity mismatch. Process separation is not by itself an
OS sandbox guarantee: platform tests must establish the claimed containment.
Admitted snapshots arrive via IPC; scripts get no arbitrary filesystem, process,
network, native loading, reflection, shell or server-object capability. No ambient
workspace file reads by the worker after admission. Errors/helpers stay headless,
never modal or focus-stealing. Internal logs use `[CarbonLuau:Tooling]` categories.

Source changes cancel old work and discard stale revision results. Auto-preview
runs on save; an explicit manual preview may snapshot unsaved buffers. Replace the
whole plan atomically. No stateful hot reload. Future preview may inspect retained
screens without claiming a real Player Presentation; unavailable mutations fail
explicitly. Empty Players service behavior is a later preview implementation,
not authorization for fixtures or synthesized events in this baseline.

## Preview semantics, fidelity and WebView

CarbonLuau tooling owns [ToolingPreviewPlan v1](ToolingContracts.md#preview-plan).
It receives viewport dimensions and supplies final geometry, retained hierarchy,
projected properties, clipping, paint data, fonts/images/scrolling, Z order,
accounting, optional source provenance and fidelity. Tooling IDs last one revision;
never leak production action/connection tokens or CUI IDs. Viewport changes
require new canonical projection, not scaling/recomputing layout in the WebView.

| Classification | Meaning |
|---|---|
| Authoritative | selected API signatures, package/module validity, retained hierarchy, deterministic geometry/list/grid/padding/clipping, retained/projected distinctions and resource accounting |
| Approximate | font rasterization, Rust sprites, PNG/item appearance, Steam avatars, scrollbars and Unity visual details |
| Convenience-only | viewport presets, filtering, source navigation, panel arrangement and preview-local scrolling |

Client receipt, authenticated clicks, reconciliation, live scroll position and
gameplay mutation are never authenticated by a tooling preview. Use local image
placeholders and CSS font fallbacks; no asset network fetch or bundled Rust fonts.
Approximate text metrics cannot change canonical rectangles.

The future WebView is paint-only: draw final rectangles/text/placeholders, select
objects, show inspection data and model labeled local scrolling. It cannot decide
validity, visibility rules, clip-depth limits or authoritative behavior. Strict CSP
defaults to no sources/network; scripts/styles/media come only from packaged
resources. No remote scripts/assets, eval, dynamic Function or injected HTML.
Use textContent for user strings, bounded discriminated messages validated in
both directions and against current revision, validated restored state, restricted
localResourceRoots containing packaged media only, and opaque source IDs mapped
to allowed URIs by the extension. No arbitrary workspace filesystem exposure.

## Trust, UX and privacy

Use VS Code Workspace Trust with `untrustedWorkspaces.supported = limited`.
Restricted Mode allows parse-only syntax, packaged API information/docs,
metadata and non-executing canonical diagnostics using official pinned tools;
it must not start luau-lsp. Trusted workspaces may automatically receive
ExecutableAnalysis under the qualified D19 analysis profile, including type
functions but excluding workspace executable configuration/custom plugins.
Check trust at launch and dispatch, handle grant and revocation/reload cleanup,
and preserve static functionality when analysis is unavailable. Explain this
once in status/Output without repeated prompts. Trust is consent, not an OS sandbox.
Never load workspace executables/plugins/tool paths. Preview, artifact-writing
builds/tasks, live integration, fixtures and external dependency mappings require
trust. Check `workspace.isTrusted` in handlers as well as `isWorkspaceTrusted` UI
conditions; hiding commands is insufficient. Trust does not waive worker bounds.

Reserve Validate Project, Build Addon, Preview GUI, Open GUI Inspector (same-panel
alias), Select Scripting API, Restart Tooling and Show Output. Contribute commands
only when their backend exists. Reserve apiVersion, preview.viewport,
preview.autoRefresh and localDependencies under `carbonLuau`; no raw binary paths,
ports, memory/protocol tuning or new mandatory project file. This bootstrap exposes
no nonfunctional command or setting.

No telemetry initially: no source, packages, preview state, workspace paths,
dependency graphs or server details leave the machine. Crash reporting requires
a separate explicit privacy decision. Normal installed language/validation/preview/
build operation is offline; no silent definition download during editing.

## Tooling packs, platforms and later seams

[Pack identity](ToolingContracts.md#tooling-pack) binds an immutable compatible
host, canonical metadata/definitions, native component where needed, LSP binary,
configuration and analysis adapter. Keep extension, protocol, pack, semantic
revision, API, package schema, metadata schema, preview schema, tooling-native ABI,
LSP version/Luau revision and runtime Luau revision distinct. Pack mismatch fails
closed. Bundled or already verified cached packs work offline. Explicit on-demand
official release assets require immutable names, SHA-256, signed manifest or
verifiable attestation and exact platform before executing anything.

Initial distribution targets are platform VSIX `win32-x64`, `linux-x64` (glibc),
`darwin-x64`, `darwin-arm64`, each with a qualified self-contained tooling pack.
`extensionKind = workspace` runs tools where the workspace resides. macOS is a
developer target, not a production server claim. No web, musl or other architecture
claim. Qualify macOS containment and signing/quarantine, all shipped platforms,
licenses and offline first use before publication. Publisher namespace remains
a release gate. No VSIX or pack is published by baseline adoption.

Live integration stays disabled and separate: explicit operator enablement,
authenticated bounded capability-scoped communication, TLS for remote transport,
no arbitrary commands or unauthenticated control endpoint; normal tooling needs
no server. Future DAP is a separate adapter. Visual authoring proposes source edits;
Luau stays authoritative with no second `.gui` format. These are seams only.
