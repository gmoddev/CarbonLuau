# CarbonLuau VS Code Extension and Tooling Architecture

**Status:** Architecture / UX / technical design only  
**Research baseline:** `gmoddev/CarbonLuau` `main` at commit `1ec3287ea538641229c794e46a4d327784afb6fa` (2026-09-21)  
**Target CarbonLuau identity:** package `0.4.0` candidate, scripting API `0.4.0-experimental`, native ABI `1.4`, addon provider protocol `CarbonLuau.Addons/1.2`, package schema `1`, pinned Luau revision `c6b830185af962c82003f86784e2fe036357c830`.

---

## 1. TL;DR

Build **two repositories with one semantic owner**:

- `gmoddev/CarbonLuau` remains the owner of runtime semantics and gains Carbon-independent shared core libraries plus a versioned `carbonluau-tooling` host.
- `gmoddev/carbonluau-vscode` contains only the VS Code client, Luau-LSP integration, WebView renderer/inspector, UX, and tests. It must not contain a second implementation of package, module, GUI layout, resource-limit, or runtime semantics.

The extension launches two separate tools:

1. an upstream, pinned **`luau-lsp`** process for syntax/type intelligence; and
2. an official **`carbonluau-tooling`** process for all CarbonLuau-authoritative behavior: project detection, manifest/package validation, exact module/dependency resolution, deterministic package construction, authoritative compilation, preview execution, GUI semantic projection, limits, and API/tooling metadata.

The current repository already has the key GUI seam needed for this design: `GuiRenderPlan`, `IGuiBackend`, `InMemoryGuiBackend`, and `RustCuiBackend` are distinct. Refactor the Carbon-independent descriptor/retained/layout/render-plan pieces into a shared core library, then add a **tooling semantic projection**. Do not make the WebView calculate `UDim2`, list/grid layout, padding, clipping, projected-element costs, or other CarbonLuau geometry. The tooling host receives a viewport size and returns resolved projected geometry plus retained state and accounting. TypeScript only paints the result.

Use a stable CarbonLuau-owned API metadata format as the public tooling contract. Generate `*.d.luau`, concise hover documentation, hosted API reference inputs, completion metadata, availability/qualification metadata, and release tooling metadata from that contract. Keep implementation behavior in code, but CI must prove the runtime bindings and generated public API surface match the contract.

For CarbonLuau's nonstandard `require` semantics, use a **CarbonLuau-owned source-transform adapter for the pinned `luau-lsp`** to map exact, already-resolved CarbonLuau module targets to ordinary filesystem requires for analysis only. `carbonluau-tooling` remains authoritative for whether a dependency/module is legal. The LSP transform is a convenience/type-inference bridge, never the authority for visibility or package validity.

Preview execution is always out of process. `carbonluau-tooling` acts as a coordinator and spawns the same executable in `--preview-worker` mode for each fresh preview run. The preview worker has no Carbon/Rust/Unity dependency, no arbitrary filesystem/process/network capability, a canonical 64 MiB Luau heap by default, a 256 MiB process ceiling, and a 1-second hard wall-clock deadline. A source change cancels the old worker and starts a new clean VM; v1 does not preserve preview state across edits.

The preview WebView is a **derived visualization**, not a custom editor and not a Rust-client emulator. It promises authoritative retained hierarchy, CarbonLuau layout/projection decisions, viewport geometry, clipping topology, Z ordering, visibility, and resource accounting. Exact font rasterization, game images, Steam avatars, scrollbars, and Unity-specific pixels are approximate or placeholders and are labeled as such. Network delivery, authenticated clicks, reconciliation, client scroll position, and gameplay mutations are never represented as authoritative.

**Recommended v1:** language tooling, API/version awareness, addon/project validation, local dependency understanding, deterministic `.claddon` build, static deterministic GUI preview, hierarchy/property/resource inspector, source navigation, multi-resolution preview, strict workspace-trust behavior, Windows/Linux/macOS developer tooling, Marketplace/Open VSX packaging. Defer mock player/item fixtures, callback interaction simulation, live-server integration, DAP debugging, and visual source authoring.

**Final verdict: READY FOR IMPLEMENTATION DESIGN.**

---

## 2. Product goals and non-goals

### Goals

1. Make CarbonLuau feel like one coherent Luau platform instead of a Carbon/Rust plugin that happens to host Luau.
2. Give authors normal Luau editor features: syntax, type checking, completion, hover, signatures, definitions, references where practical, and source diagnostics.
3. Make CarbonLuau's own API feel native: `game:GetService("Players")`, `Gui:Create("Frame")`, Player properties/methods, immutable value types, signals, image/font identities, and package imports should all carry useful types and docs.
4. Make addon/package mistakes visible before deployment using the exact server rules.
5. Provide deterministic visual GUI inspection using CarbonLuau semantics rather than a TypeScript clone.
6. Make resource limits inspectable rather than mysterious runtime failures.
7. Keep ordinary use low-friction: install extension, open project, code, preview, build.
8. Keep workspace source untrusted and preview execution killable.
9. Keep API, package schema, tooling protocol, native/tooling revision, Luau revision, and extension version independent.
10. Leave clean seams for live-server tooling, debugging, and visual source-edit suggestions later.

### Non-goals

- No custom Luau parser or type checker.
- No second implementation of CarbonLuau GUI layout in TypeScript.
- No package registry, resolver, lockfile ecosystem, or downloader.
- No Rust/Unity/CUI emulator.
- No claim of pixel-perfect client rendering.
- No arbitrary host mutation in local preview.
- No hidden remote-control server.
- No user account/cloud service/database.
- No v1 replacement for Roblox Studio or full visual authoring environment.
- No new `.gui` source format.
- No runtime reflection or Markdown scraping to discover the API.
- No requirement that a developer install Rust, Carbon, or a local Rust server for normal editor use.

---

## 3. Researched current Luau / VS Code tooling landscape

### CarbonLuau repository findings

The current repository already exposes the correct architectural direction:

- `src/CarbonLuau/Gui/GuiDescriptors.cs` contains canonical GUI class/value/property/method/event descriptors.
- `GuiRenderPlan.cs` defines a backend-neutral render-plan model with typed properties, node kinds, projected-element accounting, serialization bounds, parent-before-child validation, and the existing seven-element charge for `ScrollingFrame`.
- `IGuiBackend.cs` isolates Replace/Update/Destroy/Scroll operations.
- `InMemoryGuiBackend.cs` provides a deterministic non-Rust backend used for state/testing.
- `RustCuiBackend.cs` is already a separate production adapter.
- `GuiRetainedRegistry.cs`, `GuiPresentation.cs`, and related files hold the retained model and presentation compiler.
- `AddonPackage.cs` owns exact package/archive/manifest limits and parser rules, including ZIP hardening, path canonicalization, package IDs, versions, source limits, dependencies, `main`, and `publicModules`.
- `docs/api/Addons.md` defines the exact module contract: local `require("private/util")`; declared dependency `require("@id")` to dependency `main`; `require("@id/path")` only to an exact public export; imports never activate packages; returned values are ordinary cached same-VM Luau values.
- `ScriptSnapshot.cs` already enforces canonical source paths, source-size/module limits, no symlink/reparse-point traversal, and snapshot-before-VM execution.
- the existing compiler worker is already a killable sibling process with a 64 KiB request bound, 1 MiB response bound, one-second wall deadline, and 256 MiB process limit.

These are reusable architectural seams, not just implementation details.

### Luau

Upstream Luau supplies the gradual type system, parser/compiler/runtime APIs, and C debug APIs. `lua_getinfo` exposes source identity and current-line information, which is enough for initial tooling-only creation-site mapping without exposing `debug` to scripts.

The current generalized Luau require system is filesystem/alias oriented. CarbonLuau's addon visibility rules are stricter and semantically different, so the extension must not pretend `.luaurc` aliases alone are the CarbonLuau module model.

### `luau-lsp`

As of this design, `luau-lsp` 1.70.0 was released 2026-09-20 and synced upstream Luau 0.739. It supports diagnostics, completion, hover, signature help, definitions, references, semantic tokens, inlay hints, code actions, symbols, and related LSP features. It can run standalone over stdin/stdout and accepts custom definition/documentation files.

Two caveats materially affect this architecture:

1. its definition-file syntax is explicitly described as unstable/undocumented, so `.d.luau` must be a generated adapter, not CarbonLuau's canonical API database;
2. its Luau source-transform plugin system is useful for custom require resolution and preserves LSP source mappings, but remains experimental. CarbonLuau may use a **pinned, extension-owned adapter** against a qualified LSP version, but package/module legality must stay in `carbonluau-tooling`.

Do not depend on Roblox platform mode, Rojo sourcemaps, or the Roblox Studio companion plugin. CarbonLuau is a standard Luau embedding with its own host API.

### VS Code

The relevant current extension architecture is mature:

- LSP clients normally run language analysis in a separate process.
- Workspace Trust supports `limited` behavior and feature gating.
- Webview panels are appropriate for derived visualizations; custom editors are for replacing the editor for a resource and are not the right v1 abstraction here.
- Webviews support restrictive `localResourceRoots`, CSP, and message passing.
- `createFileSystemWatcher` supports scoped project watching; recursive watchers should be minimized.
- Task Providers can expose detected build/validation tasks.
- platform-specific VSIX packages support Windows, Linux, and macOS native payloads.
- `extensionKind: ["workspace"]` lets language/tooling processes run on the machine that owns the workspace, including Remote SSH/WSL/Containers.

### Toolchain language

Use **C# on .NET 10 LTS** for the coordinator/tooling host because the authoritative CarbonLuau package and GUI semantic code is already C#. Publish self-contained/NativeAOT platform tooling so users do not install a .NET runtime manually. Keep Luau native code as a narrow adjacent native component, just as CarbonLuau already has a managed/native boundary.

---

## 4. Recommended repository structure

### `gmoddev/CarbonLuau`

This remains the semantic owner.

```text
CarbonLuau/
├─ api/
│  ├─ carbonluau-api.schema.json
│  └─ carbonluau-api.json              # canonical public API contract
├─ src/
│  ├─ CarbonLuau.Core/
│  │  ├─ Api/
│  │  ├─ Addons/
│  │  ├─ Gui/
│  │  ├─ Modules/
│  │  └─ Policy/
│  ├─ CarbonLuau.Tooling/
│  │  ├─ Protocol/
│  │  ├─ ProjectModel/
│  │  ├─ Validation/
│  │  ├─ Packaging/
│  │  ├─ Preview/
│  │  └─ Metadata/
│  └─ CarbonLuau/                       # Carbon plugin adapter / current product
├─ native/
│  ├─ runtime/
│  ├─ compiler/
│  └─ tooling/                          # no Carbon/Rust dependency
├─ generated/
│  ├─ carbonluau.d.luau
│  ├─ carbonluau-docs.json
│  └─ tooling-metadata.json
└─ tests/
   ├─ core-parity/
   ├─ tooling/
   └─ preview-golden/
```

The exact folder names can differ, but the ownership boundary should not.

### `gmoddev/carbonluau-vscode`

```text
carbonluau-vscode/
├─ extension/
│  ├─ project-manager/
│  ├─ language-client/
│  ├─ diagnostics/
│  ├─ tooling-client/
│  ├─ commands/
│  └─ tasks/
├─ webview/
│  ├─ preview-renderer/
│  ├─ hierarchy/
│  ├─ inspector/
│  └─ resource-meter/
├─ media/
├─ tests/
│  ├─ unit/
│  └─ vscode-e2e/
└─ package.json
```

This repository contains **no canonical CarbonLuau package parser, layout compiler, module visibility rules, or hard-coded API limits**.

### Why not one monorepo?

The extension should have independent Marketplace/Open VSX releases and can update UX without forcing a server/runtime release. Conversely, a CarbonLuau runtime release can publish a new tooling pack/API metadata without requiring an immediate extension code release. Shared semantics nevertheless belong with CarbonLuau itself, so a separate tooling host repository would create exactly the drift risk this design is trying to avoid.

---

## 5. Complete component architecture diagram

```text
┌────────────────────────────────────────────────────────────────────┐
│ VS Code                                                            │
│                                                                    │
│  CarbonLuau extension (TypeScript, workspace extension)            │
│  ├─ Project index / project detection                              │
│  ├─ LSP client + diagnostic aggregator                             │
│  ├─ CarbonLuau command/task surface                                │
│  ├─ Tooling protocol client                                        │
│  ├─ Status bar / API selector                                      │
│  └─ Preview WebView                                                │
│       ├─ renderer (paint only)                                     │
│       ├─ hierarchy                                                 │
│       ├─ property inspector                                        │
│       └─ resource accounting                                       │
│                                                                    │
│           LSP stdio                     Tooling stdio               │
│               │                             │                      │
└───────────────┼─────────────────────────────┼──────────────────────┘
                ▼                             ▼
      ┌──────────────────┐        ┌──────────────────────────────┐
      │ pinned luau-lsp  │        │ carbonluau-tooling          │
      │ upstream binary  │        │ coordinator (.NET)          │
      │ + generated defs │        │                              │
      │ + trusted require│        │ ├─ project/manifest model    │
      │   transform      │        │ ├─ exact module resolver    │
      └──────────────────┘        │ ├─ validator / packager     │
                                  │ ├─ metadata exporter        │
                                  │ └─ worker supervisor        │
                                  └─────────────┬────────────────┘
                                                │ fresh bounded child
                                                ▼
                                  ┌──────────────────────────────┐
                                  │ carbonluau-tooling          │
                                  │ --preview-worker            │
                                  │                              │
                                  │ pinned Luau VM/compiler     │
                                  │ preview-safe host facades   │
                                  │ exact module semantics      │
                                  │ canonical retained GUI      │
                                  │ canonical layout/projection │
                                  └─────────────┬────────────────┘
                                                │
                                                ▼
                                  ┌──────────────────────────────┐
                                  │ ToolingPreviewPlan v1       │
                                  │ resolved geometry           │
                                  │ hierarchy / retained state  │
                                  │ paint identity              │
                                  │ clip / z / scrolling        │
                                  │ resource accounting         │
                                  │ source provenance           │
                                  │ fidelity annotations        │
                                  └──────────────────────────────┘

                         Shared by production + tooling
                                  ▲
                                  │
             ┌────────────────────┴─────────────────────┐
             │ CarbonLuau.Core                         │
             │ Addons | Modules | API | GUI | Policy   │
             └────────────────────┬─────────────────────┘
                                  │
                                  ▼
             ┌──────────────────────────────────────────┐
             │ Production CarbonLuau plugin            │
             │ Carbon/Rust adapters + RustCuiBackend   │
             └──────────────────────────────────────────┘
```

---

## 6. Ownership / responsibility table

| Concern | Authoritative owner | Extension responsibility |
|---|---|---|
| Luau parsing/type inference | pinned upstream `luau-lsp` | host client, feed generated definitions/docs |
| Runtime-valid Luau compilation | CarbonLuau pinned compiler | display diagnostics |
| Public API names/signatures/docs metadata | `CarbonLuau` API catalog | render completion/hover UX |
| API implementation existence | runtime binding contract + CI | none |
| addon manifest/package semantics | `CarbonLuau.Core.Addons` | surface diagnostics/actions |
| module/dependency visibility | `CarbonLuau.Core.Modules` | completion/navigation presentation |
| `.claddon` construction | `carbonluau-tooling` | choose project/output and display result |
| retained GUI semantics | `CarbonLuau.Core.Gui` | none |
| list/grid/padding/clipping projection | `CarbonLuau.Core.Gui` | none |
| viewport pixel geometry | tooling projection using core semantics | paint rectangles |
| Rust CUI transport | production `RustCuiBackend` | never consume it |
| preview plan serialization | `carbonluau-tooling` | schema validation and display |
| exact client rasterization/assets | real Rust client | explicitly not claimed |
| resource limits | shared policy metadata | meters/warnings only |
| preview VM execution | preview worker | start/cancel/restart |
| WebView DOM/security | extension | CSP, sanitization, structured messages |
| source navigation | tooling provenance + extension | open/reveal source |
| server deployment/runtime state | future live-server adapter | deferred |

---

## 7. Language-server / type-system integration

### Decision

Run an official **pinned upstream `luau-lsp` binary as an internal language engine**. Do not require the user to install a second extension, and do not build a CarbonLuau type checker.

Launch it in standard Luau mode with Roblox definitions/Studio integration disabled. Supply:

- generated `carbonluau.d.luau` definition file;
- generated `carbonluau-docs.json` hover/signature documentation;
- a CarbonLuau-owned, extension-managed source-transform plugin for literal `require` adaptation;
- controlled flags/settings qualified against the target runtime revision.

The API definitions should encode narrow singleton-string overloads where useful, for example conceptually:

```luau
GetService: ((DataModel, "Players") -> Players)
          & ((DataModel, "Gui") -> Gui)
          & ((DataModel, "Items") -> Items)
```

and equivalent overloads for `Gui:Create("Frame")`, `Gui:Create("TextButton")`, etc. That gives inference such that:

```luau
local Players = game:GetService("Players")
```

produces a typed `Players` service, while `Player.Position`, `Player.Health`, `Player:Teleport`, `Player:CountItem`, and `Player:HasItem` expose the actual selected API. Unimplemented APIs such as `GiveItem` and `TakeItem` are not emitted until they ship under an API identity.

### CarbonLuau require bridge

CarbonLuau's resolver remains authoritative. For each source snapshot, `carbonluau-tooling` returns an **analysis module map** containing exact legal literal requires and their resolved physical source target when one exists locally.

The extension generates a trusted source-transform mapping for `luau-lsp`:

- `require("private/util")` -> the canonical defining addon's module file;
- `require("@economy")` -> the exact declared dependency's `main` source;
- `require("@economy/formatting")` -> the exact declared public module source.

The transform only adapts a require that the tooling host has already resolved. It does not decide whether a dependency is declared/public/current. Illegal requires remain illegal and get a CarbonLuau diagnostic.

Because the current `luau-lsp` plugin API preserves mapping between transformed source and original source, hover/definition/diagnostic positions remain usable. The plugin API is experimental, so **the extension pins the exact LSP version and plugin adapter together as one qualified tooling pack**. A future plugin-API change is a tooling-pack maintenance issue, not a CarbonLuau language-contract change.

### LSP/runtime Luau revision mismatch

Do not imply that `luau-lsp`'s bundled Luau revision is the runtime compiler. Track both:

- `runtimeLuauRevision` — exact CarbonLuau compiler/runtime pin;
- `languageServerVersion` and `languageServerLuauRevision` — editor analysis engine.

`carbonluau-tooling` performs authoritative compile checks using the runtime pin. If the LSP accepts a newer syntax construct that the runtime compiler rejects, the authoritative CarbonLuau compile diagnostic wins. Release CI must maintain a compatibility corpus and choose an LSP build with acceptable parser/type behavior for the target API/tooling pack.

---

## 8. Canonical API metadata pipeline

### Decision

Add a versioned, machine-readable **CarbonLuau API catalog** to the CarbonLuau repository. It is the single authority for public names, type signatures, member shape, author-facing documentation summaries, availability metadata, and tooling classification.

Recommended shape:

```json
{
  "schemaVersion": 1,
  "api": {
    "name": "CarbonLuau",
    "version": "0.4.0-experimental",
    "status": "Experimental"
  },
  "services": [],
  "classes": [],
  "valueTypes": [],
  "singletons": [],
  "members": [],
  "availability": [],
  "documentation": {}
}
```

Each member should be able to carry:

- stable tooling ID;
- owner class/service/value type;
- member kind: method/property/signal/constructor/singleton;
- overloads, arguments, return types;
- read/write status;
- concise author-facing docs;
- `sinceApi` / optional `deprecatedSince` / removal identity;
- qualification status such as `supported`, `experimental`, `client-unqualified`, `deferred`, `unavailable`;
- preview class such as `pure`, `readMock`, `presentationEffect`, `hostMutation`, `unavailable`;
- optional link key into the hosted versioned docs.

Do not encode C# implementation details or Rust object names.

### Generated outputs

```text
api/carbonluau-api.json
          │
          ├─> carbonluau.d.luau
          ├─> carbonluau-docs.json
          ├─> completion/semantic metadata
          ├─> generated public API reference tables
          └─> tooling-metadata.json
```

Numeric runtime/resource policy values should be exported from their canonical shared policy code into `tooling-metadata.json`; they should not be manually duplicated in the API catalog.

### Drift prevention

CI fails unless all of these hold:

1. generated artifacts are clean/reproducible;
2. runtime binding/export contract tests exactly match the API catalog's shipped members;
3. GUI descriptor IDs/classes/properties match the catalog;
4. native/bootstrap globals expected by the catalog exist;
5. no runtime-exposed public member lacks catalog metadata;
6. no catalog member marked shipped is missing from the selected implementation/API version;
7. public docs reference the same generated versioned metadata.

Long-form design/architecture Markdown remains handwritten. The extension never scrapes it.

---

## 9. Project / addon detection model

The extension maintains a `ProjectIndex` per workspace.

### Detection priority

1. **Addon authoring project:** folder containing `addon.json` and `init.luau`. Validate with the selected package schema.
2. **Multiple local addons:** a workspace or multi-root workspace may contain multiple folders that each satisfy (1). Each is an independent project and is indexed by exact addon ID after validation.
3. **Opened `.claddon`:** treat as a built artifact for validation/inspection, not as the normal editable source tree.
4. **Root-script project:** if no addon manifest owns the file, a workspace with CarbonLuau-style Luau source can operate in root-script mode. No manifest is required.
5. **Standalone `.luau`:** activate language/API tooling for the file. Project-only commands are limited; `Preview GUI` can run the active file as a root-script snapshot when trusted.

### Scripting API selection

Current schema-1 `addon.json` does not carry a CarbonLuau scripting API target, so v1 does not invent a new required manifest field.

Selection order:

1. explicit `carbonLuau.apiVersion` workspace/folder setting;
2. an exact locally cached project tooling identity if one was previously selected;
3. the extension's bundled current CarbonLuau API metadata.

The selected identity is always visible in the status bar, for example:

```text
CarbonLuau 0.4.0-experimental
```

`CarbonLuau: Select Scripting API` changes the workspace/folder setting. No silent fallback occurs if the selected version is unsupported.

### Package schema selection

`addon.json.schema` is authoritative for addon packaging. Unknown/newer schemas fail closed.

---

## 10. Module / dependency tooling

Use the exact current semantics:

- local module: `require("private/util")` resolves within the defining project/module snapshot;
- `require("@id")` requires an exact declared dependency and that dependency's `main`;
- `require("@id/path")` requires an exact declared dependency and exact `publicModules` export;
- importing a dependency does not activate it;
- optional dependencies are usable only when an exact local binding is available;
- no hot package resolution, version solving, registry, or npm/Cargo behavior is invented.

### Local multi-addon workspace model

Index every valid addon in all workspace folders by exact manifest ID.

For addon `shop` depending on `economy`:

- if exactly one local project has ID `economy`, bind it for tooling;
- if none exists, report it as unresolved locally but do not claim the package is invalid merely because a server/provider could supply it;
- if two projects expose the same ID, emit an ambiguity diagnostic and bind neither;
- only declared required/optional dependencies can be used;
- public/private visibility comes from the dependency manifest and canonical resolver.

For a dependency outside the opened workspace, support one optional setting:

```json
"carbonLuau.localDependencies": {
  "economy": "../economy"
}
```

This setting is convenience-only, path-sensitive, and ignored in Restricted Mode. It does not alter the manifest.

### Public module type surfaces

When the dependency's source is locally available, the LSP analysis adapter maps the CarbonLuau import to the real module source, allowing ordinary Luau return-type inference. Do not introduce CarbonLuau-specific interface files in v1.

For dependency packages that are available only as a local `.claddon`, the tooling host may canonically parse the package and expose an immutable analysis snapshot in extension storage; it must never treat arbitrary ZIP extraction as trusted filesystem content.

---

## 11. Package validation / build model

### Ownership

`carbonluau-tooling` owns both validation and `.claddon` construction through `CarbonLuau.Core.Addons`.

Do not package with a separate TypeScript ZIP implementation.

The shared core owns:

- schema-1 manifest parsing;
- exact ID/version grammar;
- `main` / `publicModules` existence;
- required/optional dependency lists and bounds;
- source path canonicalization;
- source count/byte bounds;
- archive and expanded-byte limits;
- ZIP format restrictions;
- symlink/special-entry rejection;
- duplicate/case/path normalization rejection.

### Deterministic build

Add a canonical directory-to-package builder next to the current parser. It should:

1. take an immutable project snapshot rather than reread mutable files during packaging;
2. validate first;
3. emit entries in canonical lexical order;
4. normalize path separators and metadata;
5. use deterministic timestamps/permissions/compression settings;
6. reparse the produced archive using the same canonical parser before returning success;
7. compute and return SHA-256.

The VS Code command writes the returned bytes to:

```text
dist/<id>-<version>.claddon
```

unless the user chooses another target through an explicit save action. Packaging never mutates the manifest or source.

---

## 12. Companion tooling-host design

### Decision: yes

Create `carbonluau-tooling` in the CarbonLuau repository.

### Implementation language

**C# / .NET 10 LTS**, compiled self-contained (prefer NativeAOT if qualification remains straightforward). This gives direct reuse of the current C# package and GUI implementation instead of porting it.

### Modes

```text
carbonluau-tooling --stdio
carbonluau-tooling --preview-worker
```

`--stdio` is the normal long-lived coordinator used by VS Code.

`--preview-worker` is a short-lived child process used for untrusted Luau execution. It loads the CarbonLuau tooling native runtime/pinned Luau, receives an immutable snapshot over an inherited pipe/stdin, produces one result, and exits.

### Coordinator responsibilities

- protocol/version negotiation;
- metadata access;
- project detection helpers;
- canonical project/package validation;
- module/dependency resolution;
- deterministic package build;
- generation of LSP analysis module maps;
- preview worker launch, resource policy, timeout, cancellation, crash translation;
- preview-plan validation before forwarding it to VS Code.

The coordinator must not load Carbon, Facepunch server assemblies, Rust server code, or `RustCuiBackend`.

---

## 13. Process sandbox / resource model

Treat all workspace Luau and package contents as malicious input.

### Preview worker

Initial hard bounds:

- **Luau heap:** 64 MiB by default, matching current CarbonLuau default runtime configuration; never exceed the existing 16..256 MiB accepted range without a future tooling decision.
- **process memory:** 256 MiB hard ceiling, reusing the compiler-worker policy model.
- **wall time:** 1 second per preview execution; timeout terminates the entire worker process.
- **source:** canonical per-file 64 KiB and aggregate/package limits apply before execution.
- **callbacks/tasks:** same bounded queue model as the selected CarbonLuau semantic revision.
- **protocol input:** 8 MiB maximum framed message in v1.
- **preview-plan output:** 8 MiB maximum framed message in v1.
- **logs:** bounded; truncation is explicit.

Platform resource enforcement belongs in the worker supervisor/native launcher (Windows Job Object; Linux/macOS platform-appropriate process/rlimit mechanism). A cooperative Luau deadline is still useful but is **not** the only kill boundary.

### No ambient authority

The preview worker receives source snapshots and metadata through IPC. Preview Luau gets no API for:

- arbitrary filesystem access;
- process launch;
- arbitrary network access;
- native library loading;
- reflection;
- shell/console command execution;
- arbitrary Rust/Carbon object access.

The worker itself should not need to resolve source from the workspace filesystem after the snapshot is admitted.

### Crash handling

A crash, invalid response, oversized frame, memory kill, or timeout invalidates the worker. The coordinator starts clean for the next request. The extension may automatically restart the coordinator after an unexpected coordinator failure with bounded retry/backoff; after repeated failures it stops and exposes `CarbonLuau: Restart Tooling`.

---

## 14. Tooling protocol

Use a small local stdio protocol with **LSP-style `Content-Length` framing** and JSON payloads. Do not expose a TCP listener.

### Envelope

```json
{
  "protocol": 1,
  "id": 42,
  "method": "preview",
  "projectRevision": "sha256:...",
  "params": {}
}
```

Responses include the same `id` and project revision. Notifications are reserved for bounded logs/progress only.

### v1 operations

- `initialize`
- `getMetadata`
- `validateProject`
- `resolveProjectGraph`
- `buildAddon`
- `preview`
- `shutdown`

Do **not** make `compile` a public protocol method unless another client actually needs it; compilation is an implementation step of validation/preview. Do not make `inspectObject` a round trip in v1 because all inspector state is already contained in the preview plan.

### Initialize negotiation

Client sends:

- supported tooling protocol major/minor;
- extension version;
- requested API identity;
- requested package schema;
- platform/architecture;
- optional capabilities.

Host returns:

- selected protocol;
- tooling revision/build ID;
- supported API identities;
- supported package schemas;
- runtime Luau revision(s);
- preview-plan schema versions;
- limit/feature capability flags.

Unknown major protocol or package schema fails closed. No newer schema is interpreted as an older one.

### Cancellation/staleness

Every preview/validation request is associated with a project revision. When new edits invalidate the revision:

- cancel the old request;
- terminate an active preview worker if necessary;
- discard any response whose revision is no longer current.

This prevents stale previews from replacing newer editor state.

---

## 15. GUI preview execution model

### Initial execution

For an addon project:

1. build immutable canonical source snapshots for the addon and locally available dependencies;
2. create the same shared-VM/domain/module topology required by the selected CarbonLuau semantic revision;
3. install only preview-safe facades;
4. execute the addon entrypoint under the usual module cache/environment rules;
5. drain bounded immediate/deferred work within the preview deadline;
6. enumerate retained `ScreenGui` objects created by the preview domain(s);
7. compile a selected screen for the requested reference viewport using canonical GUI code;
8. emit `ToolingPreviewPlan`.

### Screen selection

The preview panel may show **retained screens even when no real Player Presentation exists**. This is explicitly a tooling inspection convenience, not a claim that a screen was delivered to a client. This makes normal top-level GUI construction previewable without inventing a fake authenticated connection.

If multiple ScreenGuis exist, the panel provides a screen picker using retained names/IDs.

### Preview-safe services in v1

**Available:**

- `Gui`;
- immutable value types (`UDim`, `UDim2`, `Vector2`, `Vector3`, `Color3`, `ImageSource`, `GuiFont` as available in selected API);
- bounded `task` behavior needed for normal initialization;
- `game` API/version identity;
- addon/module environment and dependency model.

**Read-only empty preview behavior:**

- `Players` service can exist with zero synthetic players so top-level code that obtains the service and iterates `GetPlayers()` does not fail. `PlayerAdded`/`PlayerRemoving` are valid signals but no events are synthesized in v1.

**Unavailable with controlled tooling error when called:**

- host gameplay mutations such as `Teleport`;
- item mutation APIs when/if present;
- real command/permission publication;
- any API requiring a real Rust server state that has no explicit mock contract.

Do not silently treat a mutation as successful.

---

## 16. Backend-neutral preview-plan design

Do **not** send Rust CUI JSON to VS Code.

Define a separate, versioned `ToolingPreviewPlan` produced after CarbonLuau semantic projection.

Conceptual structure:

```json
{
  "schema": 1,
  "semanticRevision": "...",
  "apiVersion": "0.4.0-experimental",
  "viewport": { "width": 1920, "height": 1080 },
  "screen": {
    "id": "tool:screen:1",
    "name": "Main",
    "source": { "module": "init.luau", "line": 3 },
    "metrics": {}
  },
  "nodes": [
    {
      "id": "tool:obj:4",
      "parentId": "tool:obj:1",
      "className": "TextButton",
      "name": "Buy",
      "source": { "module": "ui/shop.luau", "line": 28 },
      "retained": {
        "position": {},
        "size": {},
        "anchorPoint": {},
        "layoutOrder": 2,
        "visible": true,
        "zIndex": 3
      },
      "projected": {
        "rectPx": { "x": 100, "y": 200, "w": 180, "h": 40 },
        "effectiveClipRectPx": {},
        "clipDepth": 1,
        "visible": true,
        "layoutOwner": "tool:obj:2"
      },
      "paint": {
        "kind": "button",
        "text": "Buy",
        "background": {},
        "fontIdentity": "RobotoCondensedRegular"
      },
      "accounting": {
        "projectedElementCost": 1
      },
      "fidelity": {
        "geometry": "authoritative",
        "fontRasterization": "approximate",
        "clientInteraction": "not-authoritative"
      }
    }
  ]
}
```

### Important rule

The host returns **resolved pixel rectangles for the chosen viewport**. The WebView does not re-evaluate UDim/UDim2, grid/list layout, anchor math, padding, clipping, or topology.

Retained values are included separately for inspection. For a grid-managed child, this naturally lets the panel show:

```text
Retained Size:   UDim2.fromOffset(...)
Projected Size:  240 x 52 px (UIGridLayout)
```

instead of conflating them.

### Tooling IDs

Preview IDs are tooling-only, opaque, and valid for one preview revision. Never expose production action tokens, connection tokens, CUI element IDs, or security capabilities to the WebView.

---

## 17. WebView renderer architecture

Use `window.createWebviewPanel`, not a custom editor.

The WebView is a paint/inspection client for `ToolingPreviewPlan`:

- position nodes using host-provided absolute geometry;
- apply background/text/color/visibility/z-order from structured fields;
- apply clipping using host-provided clip rectangles/topology;
- render ScrollingFrame content using host-provided viewport/content geometry;
- never run user Luau;
- never parse addon manifests;
- never resolve module paths;
- never compute CarbonLuau layout.

Prefer plain DOM/CSS over a large UI framework initially. At the maximum authoritative screen size (257 projected elements), a straightforward DOM is sufficient and easier to audit.

The panel should have three regions:

```text
┌───────────────┬──────────────────────────┬──────────────────┐
│ Hierarchy     │ Preview viewport         │ Inspector        │
│               │                          │ + resource meter │
└───────────────┴──────────────────────────┴──────────────────┘
```

Selection is shared across hierarchy and viewport.

---

## 18. Hierarchy / property inspector

The hierarchy is the retained CarbonLuau tree, not the projected CUI tree.

Example:

```text
ScreenGui Main
└─ Frame Main
   ├─ UIPadding
   ├─ UIGridLayout
   ├─ TextLabel Title
   ├─ TextButton Button1
   └─ TextButton Button2
```

Selecting an object displays:

- Class / Name / source creation location;
- owner domain/tooling identity where useful;
- retained Position / Size / AnchorPoint;
- projected rectangle;
- retained vs projected Size/Position authority;
- LayoutOrder / active layout owner;
- ZIndex and effective paint order;
- Visible / effective visibility;
- ClipsDescendants and effective clip depth;
- image source identity and preview status;
- font identity and approximation note;
- ScrollingFrame canvas configuration;
- projected-element cost;
- relevant per-object limits.

Do not expose internal Rust component names or private command/action strings.

---

## 19. Resource-limit visualization

All limits come from the selected tooling metadata/shared policy revision.

Example panel:

```text
Screen resources
Objects                 116 / 128
Projected elements      252 / 257
Max children on a node   60 / 64
Effective clip depth      3 / 4
Text bytes             19.4 / 32 KiB

Package resources
Modules                  42 / 256
Source                 1.3 / 4 MiB
Dependencies              5 / 32
```

Three states:

- **normal** — under 80%;
- **approaching** — at/above 80% (convenience warning only);
- **limit/rejected** — authoritative CarbonLuau limit failure.

The 80% threshold is editor UX, not a runtime contract. It should appear in the preview/resource panel, not as a source error. Actual invalid/overflow state is a diagnostic/error from the tooling host.

Resource data should include a stable limit key, current value, authoritative bound, and origin identity so old previews cannot silently display new limits.

---

## 20. Viewport / resolution model

Built-in presets:

- 1280×720
- 1920×1080 (default)
- 2560×1440
- 3440×1440
- custom width/height

Changing the viewport requests a new semantic projection from the tooling host. The WebView must **not** simply scale an old plan because scale+offset geometry and clipping need to be resolved against the requested viewport by canonical code.

### Definition of “preview accurate”

**Authoritative for the selected viewport:**

- retained hierarchy/state;
- list/grid/padding decisions;
- retained vs projected geometry;
- affine UDim/UDim2 rectangle resolution;
- visibility;
- Z order;
- clipping topology/rectangles;
- projected-element accounting and canonical resource limits.

**Approximate:**

- text glyph metrics/rasterization;
- game/Unity asset appearance;
- scrollbar chrome;
- Unity-specific subpixel/raster quirks.

**Not represented as authoritative:**

- actual client receipt;
- authenticated click admission;
- network delivery/reconciliation;
- live client scroll position/inertia;
- gameplay state mutation.

The UI should surface a small fidelity legend rather than repeatedly warning on every object.

---

## 21. Images, fonts, clipping, and scroll preview fidelity

### Images

v1 policy:

- `ImageSource.None()` — no image.
- `Sprite(name)` — deterministic local placeholder showing type/name.
- `Png(id)` — deterministic placeholder showing opaque ID.
- `Item(itemId, skinId?)` — deterministic item placeholder with identifiers.
- `SteamAvatar(userId)` — generic avatar placeholder with identifier.

No network fetch. No arbitrary URL. No Steam/CDN lookup. No Rust asset extraction. A later authenticated dev-server integration may supply an explicitly requested local preview asset cache, but it is not needed for v1.

### Fonts

Do not bundle Rust/Unity font files.

Map the four `GuiFont` identities to clearly documented CSS fallbacks only for visual approximation. Because CarbonLuau layout does not depend on WebView text auto-measurement, approximate rasterization must not alter authoritative rectangle geometry.

### Clipping

The host supplies effective clip topology and pixel clip rectangles. The WebView applies them; it does not decide clip depth or projected cost.

### Scrolling

The WebView may allow local scrolling of a ScrollingFrame for inspection. That scroll offset is labeled **preview-local** and never flows back into retained CarbonLuau state.

A `ScrollTo` effect executed during preview may be represented as a one-way plan/event that adjusts the preview-local offset. It is still not exposed as readable `CanvasPosition`, and does not change the public runtime contract.

---

## 22. Mock Player / Items fixture decision

**Defer user-authored fixtures from v1.**

V1 only provides the minimal empty `Players` preview environment described above so normal top-level GUI construction can execute without inventing a fake player session. It does not synthesize `PlayerAdded`.

Foundation after v1 may introduce a versioned tooling-only fixture format, for example:

```json
{
  "players": [
    {
      "name": "TestPlayer",
      "userId": "123",
      "health": 75,
      "maxHealth": 100,
      "position": [0, 10, 0],
      "inventory": { "scrap": 250 }
    }
  ]
}
```

That fixture is not addon metadata and is never packed into `.claddon` unless a later explicit feature says so. It is validated/bounded input and exposes only APIs with explicit mock semantics.

This keeps the first preview trustworthy and small while leaving a direct path to conditional UI testing.

---

## 23. Interaction-preview decision

**Defer Luau callback execution from clicked preview buttons until the fixture foundation exists.**

V1 may visually render buttons and show that `Activated` has listeners, but clicking a preview control only selects it; it does not pretend to be an authenticated Rust client action.

Later interaction mode:

1. requires a trusted workspace;
2. runs in a live bounded preview-worker session;
3. dispatches a tooling-specific click directly to the retained button callback;
4. supplies an explicit mock Player fixture;
5. never uses production action tokens/network admission machinery;
6. preserves Luau callback semantics and ordinary errors;
7. rejects unavailable host mutations with a clear tooling error.

This is simpler and safer than simulating CarbonLuau's authenticated production action-token protocol locally.

---

## 24. Source-mapping strategy

Initial source provenance is captured at object creation in the tooling runtime using Luau debug information:

- canonical chunk/module name;
- current source line;
- optional function/source identity.

Luau's C API exposes call-stack source/current-line information through `lua_getinfo`; the preview worker can capture it internally without giving scripts a `debug` library.

`ToolingPreviewPlan` carries this creation site. Selecting an object exposes **Go to Source**, and the extension opens/reveals that line.

V1 guarantee: **file/module + line**.

A later refinement may use upstream Luau AST information to identify the exact `Gui:Create(...)` expression span on that line when unambiguous. Do not modify production GUI object semantics or add a public source-location property merely for the editor.

Objects cloned or produced through helper functions should display both the runtime creation site available to tooling and, when useful later, an origin/clone relationship; do not fabricate a source expression when dynamic code makes it ambiguous.

---

## 25. Refresh / hot-reload strategy

Use a simple full-rerun model.

```text
relevant source/manifest change
        ↓
short debounce / save boundary
        ↓
cancel stale preview request
        ↓
kill active preview worker if needed
        ↓
rebuild immutable project snapshot
        ↓
fresh VM + fresh domains/module cache
        ↓
execute preview
        ↓
full ToolingPreviewPlan
        ↓
replace panel model atomically
```

### V1 trigger policy

- language diagnostics/completion update while typing through LSP;
- authoritative project validation is debounced on document changes;
- automatic **execution preview refresh occurs on save** when `carbonLuau.preview.autoRefresh` is true;
- manual `Preview GUI` may snapshot current unsaved editor buffers explicitly.

Do not preserve preview VM state across source changes. Do not build incremental render-plan patching until profiling proves full plans are a problem. Current GUI bounds make complete plans small enough for v1.

---

## 26. Diagnostics / error UX

### Static CarbonLuau diagnostics

Produced by tooling host/project model:

- malformed/invalid addon ID;
- invalid version;
- unknown package schema;
- malformed `publicModules` path;
- missing `main`;
- public module not found;
- undeclared cross-addon require;
- private cross-addon require;
- missing dependency main;
- noncanonical module/source path;
- ambiguous local dependency project;
- package/module/source/archive bounds;
- unsupported API for selected version;
- known unavailable class/member such as `TextBox`.

### Luau language diagnostics

Produced by the pinned LSP: syntax, type, lint, inference, etc. Duplicate unresolved-require errors that are superseded by a more precise CarbonLuau module diagnostic should be filtered/coalesced.

### Authoritative compile diagnostics

Produced by the target CarbonLuau compiler revision. These override assumptions from a newer editor type/parser engine.

### Preview/runtime diagnostics

Examples:

```text
CarbonLuau preview exceeded its 1-second execution limit.
CarbonLuau preview exceeded the 64 MiB Luau memory limit.
Teleport is unavailable in local preview because no Rust server is attached.
Cannot preview @economy/formatting because the local economy dependency is not available.
ScreenGui "Shop" exceeds the 257 projected-element limit.
```

Map errors to Problems when a reliable source range exists. Otherwise show them in the preview panel and `CarbonLuau` output channel.

### Logging

Use three surfaces:

- **Problems** — source/project errors and warnings;
- **CarbonLuau Output** — bounded tooling operational logs;
- **Preview Console** — bounded user `print` output and preview runtime errors.

Internal exception stacks are hidden by default but available through an explicit diagnostic log level.

---

## 27. Workspace Trust / security

Declare:

```json
"capabilities": {
  "untrustedWorkspaces": {
    "supported": "limited"
  }
}
```

### Allowed in Restricted Mode

- syntax highlighting;
- pinned `luau-lsp` language analysis using CarbonLuau-provided trusted definitions;
- reading text through VS Code APIs;
- static manifest/module/package validation **that does not execute workspace code**;
- API/version docs/hover;
- exact require visibility diagnostics based on parsed source/project structure.

The extension must not accept workspace-configured executables, LSP plugin paths, or tooling paths in Restricted Mode.

### Disabled until trusted

- preview Luau execution;
- build/package commands that write artifacts;
- Task Provider execution;
- any future live-server connection;
- any user-supplied fixture execution/interaction mode;
- path-based `carbonLuau.localDependencies` outside the normal opened workspace trust boundary.

Use `isWorkspaceTrusted` in both UI `when` clauses and command implementation checks. Hiding a command is not sufficient; the handler must also reject it.

### Workspace code is never host code

No `.luau`, manifest, config, or fixture can select an executable, load a Node module, alter command-line arguments to an arbitrary process, or provide a source-transform plugin path. The require-transform plugin is shipped/generated by CarbonLuau tooling itself.

---

## 28. WebView security

Use a strict CSP such as conceptually:

```text
default-src 'none';
img-src <webview-csp-source> data: blob:;
style-src <webview-csp-source>;
script-src <webview-csp-source>;
connect-src 'none';
font-src <webview-csp-source>;
```

Further requirements:

- `localResourceRoots` contains only the extension's packaged preview media directory; never the workspace root by default;
- no remote scripts, styles, fonts, or images;
- no `eval`, dynamic Function, or injected script strings;
- workspace/user strings are inserted with DOM `textContent`, never trusted HTML;
- all messages use small discriminated structured payloads;
- extension validates every inbound WebView message against expected schema and current preview revision;
- WebView never receives arbitrary filesystem paths unless needed for display; use opaque source IDs and have the extension map them back to URIs;
- resource/image placeholders are generated locally;
- panel state restoration validates the persisted schema/version before use.

---

## 29. Versioning / compatibility / update model

Track these independently:

| Identity | Meaning |
|---|---|
| Extension version | VS Code UX/client release |
| Tooling protocol version | extension ↔ `carbonluau-tooling` IPC contract |
| Preview-plan schema | tooling host ↔ WebView semantic plan contract |
| CarbonLuau scripting API version | author-facing Luau API |
| Package schema | `.claddon`/manifest structure |
| Tooling semantic revision | exact shared-core/tooling build |
| Native/tooling ABI revision | managed ↔ tooling-native Luau bridge |
| Runtime Luau revision | compiler/VM semantics |
| `luau-lsp` version | editor language server |
| LSP Luau revision | Luau revision embedded in the language server |

### Compatibility behavior

Example:

```text
Project targets API 0.5.0-experimental.
Installed tooling pack supports 0.4.0-experimental only.
```

Result: clear project diagnostic/status item, preview/build disabled for that target, offer to install a compatible official tooling pack if available. Never silently analyze it as 0.4.

Unknown package schema behaves the same way: fail closed.

### API definition updates

A CarbonLuau release publishes an immutable **tooling pack** containing:

- API metadata;
- generated definitions/docs;
- shared tooling host/native preview runtime for each platform when required;
- selected `luau-lsp` identity/config adapter;
- checksums/signature/provenance manifest.

The extension bundles one current qualified tooling pack for offline first use. Additional/older/newer packs can be downloaded on demand from official CarbonLuau release assets and cached immutably.

The extension therefore does not need a new Marketplace release merely because a method description changes or a new API metadata pack is published, provided the existing extension understands that pack/protocol schema.

### Integrity

For on-demand executable/tooling packs, require:

1. official release location only;
2. immutable versioned asset name;
3. SHA-256 verification;
4. detached signed manifest (Ed25519 or equivalent project release signing key embedded in the extension) or an equivalent verifiable release-attestation mechanism;
5. exact platform/architecture match;
6. no execution before all verification succeeds.

Offline mode uses the bundled or already-cached verified packs. If the requested target is unavailable offline, report that condition instead of substituting another version.

---

## 30. Tooling distribution / platform support

### Extension distribution

Publish platform-specific VSIX packages:

- `win32-x64`
- `linux-x64` (glibc)
- `darwin-x64`
- `darwin-arm64`

These contain the current tooling pack for that platform, including the coordinator/preview runtime and pinned upstream `luau-lsp` binary. Publish the same extension version to Visual Studio Marketplace and Open VSX.

Do not bundle the production Carbon/Rust plugin binary.

### macOS

macOS is a **developer tooling target**, even though the CarbonLuau server runtime targets Windows/Linux. This is feasible precisely because shared GUI/package/tooling code must not depend on Carbon/Rust.

Qualification work:

- build .NET tooling host for x64/arm64;
- build the tooling native Luau bridge for x64/arm64;
- qualify preview worker process limits;
- qualify the pinned `luau-lsp` artifact;
- satisfy code-signing/quarantine/notarization requirements needed for smooth VS Code execution.

### Remote VS Code

Set:

```json
"extensionKind": ["workspace"]
```

so the tooling binaries execute on the machine that owns the workspace in Remote SSH/WSL/Containers. The correct platform VSIX must be installed in that extension host.

### Not v1

- VS Code for the Web / `web` target;
- Alpine/musl Linux;
- Windows ARM64;
- Linux ARM64.

Do not claim partial binary functionality on unsupported targets. Syntax-only web support can be a later small package if useful.

---

## 31. Configuration and commands

### Initial configuration surface

Keep only four user-facing settings:

```text
carbonLuau.apiVersion              "auto" | exact API identity
carbonLuau.preview.viewport        "1920x1080" | preset/custom
carbonLuau.preview.autoRefresh     true | false
carbonLuau.localDependencies       optional ID -> folder mapping
```

`localDependencies` is only for unusual multi-folder layouts; ordinary multi-root workspace discovery should require no setting.

Do not expose compiler path, tooling host path, native library path, definition path, preview server port, VM memory, protocol version, or implementation tuning knobs in v1.

### Command palette

Ship:

- `CarbonLuau: Validate Project`
- `CarbonLuau: Build Addon`
- `CarbonLuau: Preview GUI`
- `CarbonLuau: Select Scripting API`
- `CarbonLuau: Restart Tooling`
- `CarbonLuau: Show Output`

Do not ship a separate `Open GUI Inspector`; the inspector is part of the preview panel.

### Tasks

For trusted workspace addon projects, contribute detected tasks:

- `CarbonLuau: Validate`
- `CarbonLuau: Build Addon`

Use process/custom execution without shell interpolation. Commands remain the primary UX; tasks exist for normal VS Code build workflows and automation.

---

## 32. Complete first-use workflow

1. Install **CarbonLuau**.
2. Open an addon folder or workspace containing addons.
3. The extension detects `addon.json` + `init.luau` and indexes local addon IDs.
4. The status bar shows the selected CarbonLuau scripting API.
5. The bundled/pinned `luau-lsp` starts automatically with generated CarbonLuau types/docs.
6. The tooling coordinator validates the manifest/module graph and supplies exact CarbonLuau diagnostics.
7. Completion understands services/classes/methods/value types, and local declared addon imports map to their source where available.
8. The developer runs `CarbonLuau: Preview GUI`.
9. If the workspace is untrusted, VS Code trust is required before execution. No custom CarbonLuau trust bypass is offered.
10. A bounded preview worker executes a fresh snapshot.
11. The preview opens beside source at the default 1920×1080 viewport, with hierarchy and resource inspector.
12. Selecting an object highlights it; `Go to Source` reveals its creation line.
13. Saving a relevant file reruns the preview when auto-refresh is enabled.
14. The developer runs `CarbonLuau: Build Addon`.
15. The canonical tooling pack validates, builds, reparses, hashes, and writes `dist/<id>-<version>.claddon`.

No manual compiler/tooling/Carbon/Rust path configuration is required.

---

## 33. Performance strategy

### Language path

Let `luau-lsp` do what it is designed to do: incremental editor analysis. Do not route every keystroke through the CarbonLuau preview process.

### Project model

Maintain hashes for:

- manifest;
- source file content;
- module graph;
- dependency graph;
- selected API/tooling identities.

Revalidate only affected structural slices, but favor correctness over a bespoke incremental engine.

### File watching

Use VS Code `RelativePattern` watchers scoped to detected project roots and relevant patterns (`addon.json`, `**/*.luau`, local `.claddon` dependencies). Avoid workspace-wide recursive watchers when a smaller root suffices.

### Preview

- one active preview per panel/project;
- cancel stale work;
- full fresh VM on refresh;
- full plan replacement;
- save-triggered auto-refresh;
- cache immutable API metadata/tooling packs;
- cache validated package/dependency snapshots by content hash;
- do not add stateful hot reload or incremental GUI plan patches until measurement justifies them.

The authoritative GUI size bound is small enough that WebView painting should not require a virtualized renderer initially.

---

## 34. Testing / qualification plan

### Language/API

- generated definitions parse under the pinned `luau-lsp`;
- every catalog class/service/method/property/signal/value type has the expected type;
- `game:GetService` singleton inference;
- `Gui:Create` class inference;
- `Player` method/property inference;
- GuiFont/ImageSource completion;
- hover/signature docs;
- selected API version changes definitions/availability correctly;
- runtime compiler rejects constructs the editor must not claim authoritative support for.

### Addons/modules

- valid package/project;
- malformed manifest;
- invalid ID/version;
- missing `main`;
- public module missing;
- private/public cross-addon boundary;
- required/optional dependency resolution;
- missing optional dependency behavior;
- duplicate local addon ID;
- canonical/noncanonical module paths;
- module count/source bounds;
- archive entry/expanded bounds;
- traversal/symlink/reparse-point/special ZIP rejection;
- dependency `@id` and `@id/path` type navigation through analysis transform.

### Tooling protocol/process

- initialize/negotiation;
- protocol major mismatch;
- package schema mismatch;
- malformed JSON/frame;
- oversized request/response;
- bad request ID;
- stale project revision;
- cancellation;
- coordinator crash/restart;
- preview worker crash;
- timeout;
- 64 MiB VM memory failure;
- 256 MiB process kill;
- repeated restart/backoff.

### GUI semantic parity

Create shared golden fixtures that run against the exact extracted GUI core and verify:

- simple ScreenGui;
- nested Frames;
- text/button/image classes;
- list layout;
- grid layout;
- padding;
- retained vs projected Position/Size;
- clipping depth/topology;
- ScrollingFrame seven-element charge;
- viewport changes;
- visibility/Z order;
- resource accounting;
- max projected elements;
- source creation-site mapping.

A critical differential test should prove the production in-memory/render-plan path and tooling semantic projection consume the same retained/layout state. Do not compare screenshots to establish semantic parity.

### WebView

- plan schema validation;
- no layout math beyond absolute paint;
- hierarchy-selection sync;
- source navigation;
- CSP blocks remote network/script execution;
- text is not interpreted as HTML;
- malicious names/text/source paths cannot inject DOM/script;
- oversized/malformed WebView messages rejected;
- placeholder images never trigger network access.

### Security

- malicious ZIP corpus;
- path traversal / Unicode/path alias cases;
- huge manifest/source;
- preview script attempts `io`, `os`, process, network, native load;
- worker fuzzed protocol;
- hostile preview plan rejected by extension schema checker;
- untrusted workspace cannot invoke preview/build through command URI or hidden command path;
- workspace settings cannot replace trusted binaries/plugins.

### Extension E2E

Use VS Code extension test infrastructure on each supported platform:

- first open;
- multi-root project discovery;
- edit diagnostics;
- API switch;
- preview open/refresh;
- worker crash recovery;
- build output;
- unsupported version;
- Restricted Mode;
- remote/container smoke test where practical.

### Release gates

No Marketplace/Open VSX release unless Windows x64, Linux x64, macOS x64, and macOS arm64 tooling bundles pass their required smoke/integration suites. If one platform is intentionally absent from a release, do not publish a package claiming support for it.

---

## 35. Telemetry / privacy decision

**No telemetry in v1.**

No source code, manifest/package contents, file paths, dependency graphs, server information, preview plans, player fixture data, or usage analytics leave the machine.

If crash reporting is considered later, make it explicit opt-in and send only:

- extension/tooling versions;
- platform architecture;
- sanitized crash category/stack from CarbonLuau-owned code;
- no workspace content;
- no source line text;
- no absolute paths;
- no server addresses/tokens;
- no preview/player data.

Local operational logs remain bounded and user-controlled.

---

## 36. Future live-server integration seam

Do not ship it in v1, but preserve a target abstraction:

```text
PreviewTarget
├─ LocalToolingTarget     (v1)
└─ DevelopmentServerTarget (future)
```

Future server capabilities may include:

- deploy/reload addon snapshot;
- query runtime/API/tooling identity;
- retrieve status/diagnostics/logs;
- inspect active addons;
- optionally capture authoritative server-side GUI semantic/render plans.

Security requirements are fixed now:

- disabled by default;
- explicit operator enablement;
- localhost or local named pipe/Unix socket preferred for same-machine development;
- authenticated short-lived development token;
- remote mode requires TLS and explicit bind/address configuration;
- bounded messages/rate limits;
- no arbitrary shell/RCON/Carbon command execution;
- capability-scoped operations;
- production deployments can fully disable the endpoint;
- credentials stored in VS Code SecretStorage, never workspace files.

Do not reuse the local tooling stdio protocol directly as a public network service without a separate network threat review/versioned transport envelope.

---

## 37. Future debugging seam

A future Luau debugger is feasible through VS Code's Debug Adapter Protocol, but should be a separate component/adapter.

Possible later features:

- local preview breakpoints;
- Luau stack traces;
- scoped locals/upvalues where safe;
- exception breakpoints;
- server attach;
- addon reload and log correlation.

The current architecture helps because execution is already isolated in a tooling process and source chunk names are canonical. Do not implement DAP in v1 merely to expose logs; the Preview Console is enough.

---

## 38. Future visual-authoring seam

Luau remains source of truth.

Future workflow:

```text
select Frame in preview
        ↓
change Position/Size/property in inspector
        ↓
tooling computes a suggested semantic edit
        ↓
extension shows WorkspaceEdit/code-action preview
        ↓
user applies source edit
        ↓
normal fresh preview rerun
```

Do not make the preview DOM or a hidden `.gui` file authoritative. Arbitrary Luau is not safely round-trippable as a WYSIWYG scene format. When the source expression cannot be identified/edited unambiguously, the editor should decline the visual edit rather than rewrite code heuristically.

---

## 39. Exact v1 scope

### Ship in v1

- extension branding `CarbonLuau`;
- separate `gmoddev/carbonluau-vscode` repository;
- shared CarbonLuau core extraction needed for exact tooling semantics;
- C#/.NET tooling coordinator and bounded preview-worker mode;
- pinned upstream `luau-lsp` integration;
- generated API definitions/docs and version selection;
- exact addon/project detection and schema-1 validation;
- local multi-addon dependency graph;
- CarbonLuau `require` analysis adapter for locally known modules;
- static/project diagnostics;
- deterministic `.claddon` build;
- GUI preview for the currently implemented surface: ScreenGui, Frame, TextLabel, TextButton, ImageLabel, ImageButton, ScrollingFrame, UIListLayout, UIGridLayout, UIPadding, relevant value types, GuiFont, clipping, visibility, Z ordering;
- viewport presets/custom viewport;
- retained/projected hierarchy/property inspector;
- resource-limit visualization;
- deterministic image placeholders;
- approximate font fallback;
- preview-local scrolling;
- source navigation at file+line granularity;
- full fresh preview rerun on save;
- Workspace Trust gating;
- strict WebView CSP;
- no telemetry;
- Windows x64, Linux x64 glibc, macOS x64, macOS arm64 tooling packages;
- Marketplace and Open VSX release.

### v1 does not require a Rust server installation.

---

## 40. Exact deferred scope

- user-defined Player/Items fixtures;
- synthesized PlayerAdded/PlayerRemoving flows;
- preview callback interaction execution;
- authenticated-client action-token simulation;
- TextBox/text input (runtime itself does not currently expose it);
- exact Rust/Unity font or image asset reproduction;
- URL image fetching;
- stateful hot reload;
- incremental GUI patch preview protocol;
- package registry/download/version solver;
- live-server deployment/attach;
- remote server endpoint;
- DAP debugger;
- visual drag/resize source editing;
- `.gui` authoring format;
- VS Code Web target;
- unsupported CPU/libc targets;
- cloud service/accounts/telemetry.

---

## 41. Implementation phases

### Tooling Foundation A — semantic extraction + API contract

- extract Carbon-independent Addons/Modules/GUI/Policy components from the plugin partial-class structure;
- preserve production behavior with thin adapters;
- create API catalog schema/data;
- generate `.d.luau`, docs metadata, tooling metadata;
- add drift/parity CI.

**Independent value:** establishes a machine-readable, versioned API/tooling contract and reusable exact semantics.

### Tooling Foundation B — project/language integration

- create `carbonluau-vscode`;
- project detection/index;
- pinned `luau-lsp` client;
- generated definitions/docs;
- CarbonLuau authoritative diagnostics;
- local addon graph;
- require-analysis transform;
- API selector/status bar;
- Restricted Mode static behavior.

**Independent value:** useful first-class editor even without GUI preview.

### Tooling Foundation C — coordinator + validate/build

- `carbonluau-tooling --stdio` protocol;
- project snapshots;
- exact validate/build operations;
- deterministic package builder;
- Task Provider;
- tooling-pack packaging/version negotiation.

**Independent value:** exact project validator/builder usable from VS Code and later CLI/CI clients.

### Tooling Foundation D — preview semantic worker

- `--preview-worker` process isolation;
- preview-safe services;
- exact VM/module/domain execution;
- tooling projection from canonical retained GUI;
- viewport geometry;
- resource accounting;
- source creation-site mapping;
- golden/differential tests.

**Independent value:** headless deterministic preview-plan generator even before WebView UI.

### Tooling Foundation E — WebView/inspector/release closure

- paint-only WebView;
- hierarchy/inspector/resource panel;
- viewport picker;
- image/font fidelity UX;
- preview-local scrolling;
- source navigation;
- crash/restart UX;
- platform-specific VSIX CI;
- Marketplace/Open VSX readiness;
- final security/E2E qualification.

**Independent value:** complete v1 product.

### Later Foundation F — fixtures and interaction simulation

- fixture schema;
- mock Players/Items;
- conditional UI preview;
- local `Activated` callback dispatch;
- interaction-specific resource/security tests.

### Later Foundation G — live server / debugger / visual edit seams

Separate scoped foundations; none are v1 blockers.

---

## 42. Qualification gates for each phase

### Foundation A gate

PASS only if:

- production tests still pass after semantic extraction;
- package parser/build policy parity is proven;
- GUI retained/layout/render goldens are unchanged where behavior is unchanged;
- generated API artifacts are deterministic;
- catalog/runtime drift check catches intentional negative fixtures;
- no Carbon/Rust dependency leaks into `CarbonLuau.Core`.

### Foundation B gate

PASS only if:

- API types/docs load with the pinned `luau-lsp`;
- language features work on root scripts and addons;
- exact package/module diagnostics beat/filter conflicting generic LSP errors;
- local `@dependency` navigation/type inference works for qualified fixtures;
- unsupported API version fails clearly;
- untrusted workspaces cannot execute project code or workspace-supplied plugins/executables.

### Foundation C gate

PASS only if:

- protocol fuzz/boundary suite passes;
- validate result equals canonical runtime parser/policy decisions;
- deterministic build reproducibility holds across supported platforms;
- produced archives reparse successfully and negative archives fail identically;
- version/schema mismatch fails closed;
- build cannot escape requested output path through manifest/source input.

### Foundation D gate

PASS only if:

- infinite-loop preview is killed at the deadline;
- memory-exhausting preview is contained;
- preview has no filesystem/network/process capability;
- exact current GUI fixture corpus yields canonical hierarchy/layout/projection/accounting;
- viewport geometry is deterministic across runs/platforms within the integer/float contract;
- retained/projected distinctions are correct for list/grid/padding/scrolling/clipping;
- production Rust backend is not linked/required;
- source mapping reliably reaches module+line;
- worker crash cannot crash/freeze the VS Code extension host.

### Foundation E gate

PASS only if:

- WebView performs no CarbonLuau geometry/layout computation;
- CSP/security injection suite passes;
- preview fidelity labels are accurate;
- image/font placeholders never perform remote access;
- complete first-use flow passes E2E on every published platform;
- offline install can code/validate/build/preview the bundled API pack;
- Marketplace/Open VSX artifacts contain only expected platform binaries and licenses/notices;
- user-facing failures are semantic messages, not internal protocol codes.

---

## 43. Genuinely unresolved external / tool-dependent questions

These do not block the architecture, but implementation qualification must answer them:

1. **Exact `luau-lsp` qualification pairing.** Determine the best upstream `luau-lsp` release for CarbonLuau's pinned Luau revision and verify its parser/typechecker delta is acceptable. The design intentionally records both revisions and uses the CarbonLuau compiler as authority, so an exact revision match is desirable but not architecturally required.
2. **`luau-lsp` plugin API stability at the chosen pin.** The current source-transform API explicitly remains experimental. Freeze a known version in each tooling pack and lock its behavior with CarbonLuau require-resolution fixtures.
3. **macOS process-limit/signing details.** Verify the exact process memory/deadline enforcement and distribution signing/notarization steps for both Intel and Apple Silicon binaries in VS Code Marketplace/Open VSX delivery.
4. **Marketplace/Open VSX publisher namespace administration.** Claim/verify the desired publisher identity before release CI is finalized.
5. **Visual fidelity on real Rust clients.** Current CarbonLuau documentation correctly leaves several authenticated-client GUI behaviors unqualified. Those remain runtime qualification questions, not prerequisites for a deterministic tooling preview whose fidelity contract is narrower.

No unresolved question requires inventing a second GUI engine, a package manager, or a cloud service.

---

## 44. Final verdict

# READY FOR IMPLEMENTATION DESIGN

The architecture is sufficiently constrained to begin detailed implementation design and work decomposition.

The decisive choices are:

1. **separate VS Code repository, shared semantics in CarbonLuau;**
2. **official C#/.NET tooling host exists;**
3. **Carbon-independent core is extracted from current production code;**
4. **pinned upstream `luau-lsp` is the language engine, not a new parser/type checker;**
5. **stable CarbonLuau API metadata is canonical; `.d.luau` is generated;**
6. **exact package/module validation remains CarbonLuau-owned;**
7. **LSP require adaptation is analysis-only and fed by the canonical resolver;**
8. **preview execution is a fresh bounded worker process;**
9. **the tooling host produces resolved semantic geometry and inspection state;**
10. **the WebView only renders;**
11. **Rust CUI JSON/action tokens are not editor protocols;**
12. **fixtures/interactions/live server/debug/visual authoring are deliberate later seams, not v1 baggage.**

This is the smallest architecture that can deliver the requested Roblox/Luau-familiar experience without creating a second CarbonLuau implementation inside VS Code.

---

# Research references

## CarbonLuau repository

- `AICONTEXT.md`: https://github.com/gmoddev/CarbonLuau/blob/main/AICONTEXT.md
- `docs/Invariants.md`: https://github.com/gmoddev/CarbonLuau/blob/main/docs/Invariants.md
- `docs/Compatibility.md`: https://github.com/gmoddev/CarbonLuau/blob/main/docs/Compatibility.md
- `docs/api/Addons.md`: https://github.com/gmoddev/CarbonLuau/blob/main/docs/api/Addons.md
- `docs/api/Compatibility.md`: https://github.com/gmoddev/CarbonLuau/blob/main/docs/api/Compatibility.md
- GUI API reference: https://github.com/gmoddev/CarbonLuau/tree/main/docs/api
- `GuiDescriptors.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Gui/GuiDescriptors.cs
- `GuiRenderPlan.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Gui/GuiRenderPlan.cs
- `IGuiBackend.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Gui/IGuiBackend.cs
- `InMemoryGuiBackend.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Gui/InMemoryGuiBackend.cs
- `RustCuiBackend.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Gui/RustCuiBackend.cs
- `AddonPackage.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Addons/AddonPackage.cs
- `ScriptSnapshot.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Scripts/ScriptSnapshot.cs
- `RuntimeConfig.cs`: https://github.com/gmoddev/CarbonLuau/blob/main/src/CarbonLuau/Runtime/RuntimeConfig.cs
- `release.json`: https://github.com/gmoddev/CarbonLuau/blob/main/release.json

## Luau / language tooling

- Luau C API: https://luau.org/api/
- Luau type system: https://luau.org/types/
- Luau require-by-string RFC: https://rfcs.luau.org/new-require-by-string-semantics.html
- Luau alias RFC: https://rfcs.luau.org/require-by-string-aliases.html
- `luau-lsp`: https://github.com/JohnnyMorganz/luau-lsp
- `luau-lsp` language client setup / definitions: https://github.com/JohnnyMorganz/luau-lsp/blob/main/editors/README.md
- `luau-lsp` source-transform plugin: https://github.com/JohnnyMorganz/luau-lsp/blob/main/src/Plugin/README.md
- `luau-lsp` releases: https://github.com/JohnnyMorganz/luau-lsp/releases

## VS Code

- Language Server Extension Guide: https://code.visualstudio.com/api/language-extensions/language-server-extension-guide
- Workspace Trust Extension Guide: https://code.visualstudio.com/api/extension-guides/workspace-trust
- Webview API/security: https://code.visualstudio.com/api/extension-guides/webview
- Custom Editor API: https://code.visualstudio.com/api/extension-guides/custom-editors
- Task Provider: https://code.visualstudio.com/api/extension-guides/task-provider
- VS Code API / file watchers: https://code.visualstudio.com/api/references/vscode-api
- Platform-specific extension publishing: https://code.visualstudio.com/api/working-with-extensions/publishing-extension
- Extension Host / `extensionKind`: https://code.visualstudio.com/api/advanced-topics/extension-host
- Virtual Workspaces: https://code.visualstudio.com/api/extension-guides/virtual-workspaces

## Toolchain

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
