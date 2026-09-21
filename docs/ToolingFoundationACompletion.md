# Tooling Foundation A implementation and qualification

Recorded 2026-09-21. This is the completion record for the resumed implementation,
separate from [the historical stopped attempt](ToolingFoundationAValidation.md).
[D19](ToolingLanguageAnalysisSecurity.md) remains the security policy owner.
Verdict: **FOUNDATION A IMPLEMENTED — WINDOWS/LINUX QUALIFIED; macOS STATIC-ONLY**.
macOS is explicitly static-only at the user's direction, with no execution
qualification claimed. No preview or release publication is part of this work.

## Reconciliation and provenance

Runtime work began at current main
`9b27ba5625c86eb32d9168effa2c6e72d8866576`, incorporated canonical security
`9c33a98ee123238ee073a819a4938827596e5b25`, and merged the subsequent main
`c37c36e0508a759c899dfdd4a1545cc923a88fc6` before final runtime qualification.
The latter's Player-1F-C cold-module mutation fix, inventory behavior, fixtures,
examples and qualification documents are retained. Extension work began at
`85236a0b66b680bda46d94f0a3ed038253a193a7` with routing amendment
`b5daf8d35b3da0997e35326ddb65e3d0628f6216`.

The uncommitted partial work was reused from `CarbonLuau-tooling-a` at
`03b6d68346e01747e3b9250f1f4c4668ae3d8383` and `carbonluau-vscode-a` at
`85236a0b66b680bda46d94f0a3ed038253a193a7`. Shared Core, catalog/generator,
static host, project resolution and initial editor transport were retained.
The direct language-client launcher, extension-generated transform and original
non-executing-LSP assumption were replaced by the canonical supervised profile.
The original worktrees remain recovery evidence.

Implementation is isolated in `CarbonLuau-tooling-complete` and
`carbonluau-vscode-complete`, both on `codex/tooling-foundation-a-complete`.
The shared main checkout was not edited. The runtime implementation commit
introducing this record and the extension's exact `tooling-source.json` revision
identify the canonical source pair. Tooling pack manifests include semantic base
revision, payload hashes, build identity and configuration/supervisor source
hashes. Uncommitted development builds are not shipping attestations.

## Shared implementation and API

`CarbonLuau.Core` targets net48 and net10.0 with C# 7.3 shared sources. It owns
the package parser/policy, canonical module paths, GUI descriptors and pure GUI
configuration needed by metadata. It references Newtonsoft.Json 13.0.3 and
framework reference assemblies, with no Carbon/Rust/Unity/server assemblies.
The inherited `Carbon.Plugins.CarbonLuau` nested type container for GUI values
does not introduce a server dependency. Runtime source packaging compiles the
same Core source via `SharedSources.props`; thin adapters preserve the public
nested provider types. Native runtime and parser use the same ModulePolicy.
Production lifecycle, inventory, Player tokens and host effects remain outside
Core. Preview-specific retained/layout extraction is intentionally deferred.

Explicit binding annotations and canonical descriptors generate
`api/carbonluau-api.json`, `generated/carbonluau.d.luau`, LSP documentation and
tooling metadata. Markdown is not a semantic input. Current services, Player,
GUI, Signals, values and enums include `TakeItem`, `GiveItem` and
`GiveItemBehavior.InventoryOnly`; deferred TextBox/preview APIs are absent.
Repeat/culture determinism, checked-in goldens and negative binding/signature,
service/font/native/type-reference drift tests enforce the source relationship.
Runtime qualification caveats are preserved; editor definitions do not expand
the live-server support envelope.

Identities are unchanged: runtime package 0.4.0, scripting API
0.4.0-experimental, native ABI 1.4, addon protocol CarbonLuau.Addons/1.2,
package schema 1. Tooling uses CarbonLuau.Tooling/1.0, metadata schema 1,
development pack `foundation-a-development`, analysis policy 1, proxy revision 1
and owned transform revision 2. Runtime Luau and analysis Luau remain separately
pinned as described in `tooling/language-server.json`.

## Static host and project model

The net10 self-contained `carbonluau-tooling --stdio` provides initialize/version
negotiation, getMetadata, validateProject, resolveProjectGraph and shutdown.
Project operations include discovery and addon/archive validation; there are no
speculative preview operations or HTTP service. The parse-only native library
links Luau.Ast/Common, never the VM. Ordinary source bodies and type functions
are not evaluated by this host.

Bounded snapshots support root/standalone, nested addon, multi-folder and sibling
addon sources, unsaved buffers and `.claddon` inspection without a new project
format. Exact-ID declared dependencies, optional/required declarations, main,
public/private modules and local imports use canonical Core/native legality.
Undeclared siblings and ambiguous IDs cannot become analysis imports. Missing
workspace dependencies are diagnostics, not invitations to download packages.
The package parser remains the production ZIP parser through in-memory admission.

Problems covers malformed manifests, ID/version/schema/API, unsafe paths,
missing main/public modules, undeclared/private/unresolved imports and package/
source/module bounds. Diagnostics and imports are not independently reimplemented
in TypeScript. Protocol rejects malformed/duplicate/deep/oversized data and stale
snapshot revisions. Limits include 8 MiB frames, 4 KiB headers, depth 32, 32
folders, 2048 candidate files, 6 MiB source/archive inputs and 256 diagnostics.
Runtime compiler validation is explicitly unavailable; parse/type results are
not a claim that the server compiler accepted the program.

## Executable analysis boundary

Restricted Mode never creates an analysis supervisor or luau-lsp. It provides
syntax, parse diagnostics, canonical API reference hover and project/package
validation. A status-bar indication explains why richer analysis needs trust.

Trusted qualified Windows/Linux workspaces automatically use a separate
`--analysis-stdio` supervisor and native launcher. It writes an immutable private
snapshot with opaque hashed filenames; workspace paths never become physical
analysis paths. Generated JSON settings, definitions and the canonical source
transform are pack-owned. Workspace `.config.luau`, `.luaurc`, `.robloxrc`, LSP
settings, executable paths and arbitrary transforms/plugins are excluded.
Ancestry is checked to the filesystem root, rejects links/reparse points and
configuration, and is watched/polled throughout the session. Unexpected ancestry
configuration fails closed without modifying the user's file.

Type functions are preserved and execute only in the LSP's restricted VM.
Its 64 MiB heap limit is supplemented by a fixed 15-second request deadline,
30-second initialization deadline and 1-second owned-plugin limit. Requests are
serialized through a bounded proxy; traffic cannot extend the deadline. Owned
transform literals are fully byte-escaped with an exact source guard. Canonical
require targets map to snapshot files. Dynamic/aliased/unresolved/invalid
requires, including expressions under `typeof`, syntax errors and invalid addon
packages are withheld, with exclusion propagated to importers. No arbitrary
source stripping or replacement type-function results are used.

Only diagnostics, hover, completion, signatures and definitions cross the proxy.
Results have bounded structure, admitted URI mapping, source-checked UTF-16
ranges, plain-text documentation and no server commands, arbitrary workspace
edits, external navigation or plugin selection. Trust and revision are checked
before requests and before results are published. Existing queued snapshots
also honor a newly latched failure; the Windows E2E test caught and closed this
otherwise possible timeout replay.

Windows launches suspended and assigns a non-breakaway Job before resume, with
1 GiB process commit, one active process and kill-on-close. Linux installs a
2 GiB hard address-space ceiling before exec, owns the child process group,
monitors 1 GiB RSS every 50 ms and handles parent loss. Disposal kills/reaps
before snapshot removal. The extension host, static coordinator and analysis
process are separate failure domains. Timeout/resource/protocol failures latch
static mode until explicit Restart Tooling. Unexpected crash gets at most one
restart in five minutes. Normal edits do not replay a failed session. Grant
starts a fresh session; trusted reopen works; revocation/reload and deactivation
terminate analysis. The extension host remains responsive during looping analysis.

| Boundary | Qualified guarantee | Explicit limit |
|---|---|---|
| VS Code extension host | No workspace Luau VM/evaluation; asynchronous bounded child protocol | Native/VS Code defects are outside this application-level guarantee |
| Static host | Parse/metadata/canonical checks only | Not an OS sandbox; bounded input does not prove absence of parser defects |
| Analysis process | Separate supervisor, deadlines, reaping, failure latch and bounded output | Child separation alone does not restrict filesystem/network authority |
| Luau VM | Pinned restricted type-function capabilities and heap bound | Not equivalent to arbitrary native process execution; VM defects remain relevant |
| Windows OS controls | Job commit/process/parent-lifetime controls installed before execution | No filesystem/network isolation |
| Linux OS controls | Pre-exec address-space cap, process group, parent-loss and soft RSS monitor | RSS may overshoot between samples; no seccomp or filesystem/network isolation |
| macOS | Analysis disabled; static-only policy | No build/runtime execution evidence from a macOS runner |
| Workspace Trust | Explicit prerequisite for executable type analysis | Consent is not sandboxing; workspace config/plugins remain excluded even after trust |

## Qualification evidence

Builds/tests ran headlessly on the trusted Windows `dockerbox` worker and its
Linux containers, with native compilation limited to two jobs and containers
limited to two CPUs/4 GiB. Worker source/build/artifacts are under
`C:\Sandbox\Codex\{Workspaces,Builds,Artifacts}\CarbonLuauToolingA-20260921`.
Local checks were lightweight metadata, TypeScript and deterministic packaging.

| Gate | Evidence |
|---|---|
| Core and metadata | Both target frameworks build; generator check and negative drift suite pass |
| Static host | Eight actual-host groups pass on Windows/Linux: protocol, identities, metadata, project/package/dependency/ownership and archive fixtures |
| Trusted supervisor | Five actual-LSP groups pass on Windows/Linux: ordinary type function/API, excluded config, untrusted rejection, require mapping and unsafe-source withholding |
| Security suite | Twelve groups: config/settings/plugin exclusion; normal/infinite/heap type functions; untrusted/command rejection; Unicode/invalid/typeof imports; unknown/aggregate inputs; malformed/crashed server; process memory; controls installed before child instruction; ancestor config rejection; launcher/child teardown |
| VS Code | Version 1.95.3, real workbench trust UI with dedicated profile: Restricted Mode, grant, trusted reopen, type diagnostics/hover/completion/signature, two-crash latch, explicit restart, looping type-function responsiveness/no replay, revocation/reload |
| Queue regression | Deterministic test proves an already queued snapshot cannot start after the preceding request fails |
| Runtime | Reconciled Windows/Linux native CTests and full net48 runtime suite pass, including latest Player-1F-C, GUI, module/package/parser, addon and resource/lifecycle stress |
| Packaging | Windows/Linux byte-for-byte deterministic runtime release bundles and source ZIP audit pass; Core sources included, tooling/analysis/preview payloads excluded |
| Architecture/API | API audit, GiveItem structural supplement, architecture and schema positive/negative checks pass |
| macOS | No available runner. Explicit user direction: retain static-only status; no executable language qualification claimed |

The upstream `.config.luau` execution discovery remains true. These tests prove
its exclusion from this adapter, not an upstream non-execution flag. Native
malformed/crash/memory fixtures are trusted test payloads, not workspace-selectable
executables. Process resource tests are separate from VM heap tests.

Runtime CI retains Windows/Linux native/net48/packaging and sanitizer jobs, adding
Core, metadata/definitions, static host and tooling-pack jobs. Extension CI pins
the canonical runtime source and builds packs, tests transport/security, then
runs real VS Code E2E on Windows/Linux. macOS CI is configured for build/static
checks only; configuration is not evidence that a run occurred. Hosted CI status
must be reported separately from the worker results.

## Developer workflow, UX and Foundation B handoff

Build the extension with Node 22 (`npm ci --ignore-scripts`, `npm run check`).
Use PowerShell 7 and `tools/Build-Tooling.ps1 -Extension <extension-root>` with
.NET SDK 10, Python, CMake and a native compiler to provision a local self-contained
pack. Supply `-LanguageServerArchive` for an already downloaded pinned archive.
The explicit build can download it; activation never downloads tooling. Start a
VS Code development host with `--extensionDevelopmentPath=<extension-root>`.
Only installed API targets appear in Select Scripting API. Validate Project,
Restart Tooling and Show Output are working commands; no Preview command exists.

Offline operation after provisioning uses no telemetry, remote API, package
registry, HTTP listener or live server connection. OS-authority limits above
still apply to native analysis. Ordinary workspace Luau configuration is excluded
by design; unsupported/dynamic imports and incomplete syntactically invalid
sources retain static checks rather than misleading full type results.

Foundation B receives reusable Core, canonical API metadata and definitions,
bounded coordinator/protocol, project validation, editor tooling client and
qualified language integration. B must separately implement the bounded preview
worker, preview VM and ToolingPreviewPlan generation, including any further pure
GUI extraction needed for canonical shared geometry. No preview VM/worker,
WebView, hierarchy/resource visualization, mock Player/Items, Activated
simulation, live server mode, debugger or visual authoring began. No VSIX was
built or published, and no Marketplace identity/support claim was added.
