# Tooling Foundation A implementation and validation

Historical record of the first, stopped implementation attempt. Its execution
assumption was superseded by the [D19 security amendment](ToolingLanguageAnalysisSecurity.md).
Current implementation and qualification are recorded in
[Foundation A completion](ToolingFoundationACompletion.md). Preserve the evidence
below; its BLOCKED verdict describes that earlier attempt.

Recorded 2026-09-21. Verdict: **BLOCKED**. Work is retained in isolated, uncommitted
worktrees. Foundation A is not qualified and Foundation B must not begin.

## Stop-condition evidence

The task explicitly requires stopping if "tooling needs to execute workspace
Luau for Foundation A." D19 and the tooling baseline require non-executing static
analysis. Candidate luau-lsp 1.70.0 executes both workspace `.config.luau` and
user-defined type functions during analysis. Disabling the transformation
plugin's filesystem API does not disable either execution path.

Candidate source is `876d84b8caf4454c8df806ddc9eee0565228d7b6` (annotated tag
object `e7db6b4a2fbc1fdee1c864c920000fc5a0f3ee4c`); embedded Luau is
`a62362a53ddc9c629b0e29378a84abb4534d8b64`. The exact downloaded Windows
release archive SHA-256 is
`26e6d32069cb5dd74f06bbdc2f3033d3ba66da76d3dbdfc3dcc2b085ddaa274a`.
All candidate platform hashes are in [language-server.json](../tooling/language-server.json).

The actual Windows binary was tested with harmless, task-owned fixtures:

```text
python tests/tooling/ProbeLspExecution.py build/lsp/luau-lsp.exe
CONFIG 1
[INFO] Loading Luau configuration from .../.config.luau
.../.luaurc: config:1: CARBONLUAU_CONFIG_EXECUTED

TYPE_FUNCTION 1
main.luau(5,14): TypeError: 'Executed' type function errored at runtime:
[string "Executed"]:3: CARBONLUAU_TYPE_FUNCTION_EXECUTED
```

The probe writes `error(...)` sentinels into a temporary configuration and a
temporary type function. These errors demonstrate execution, not just parsing.
It runs headlessly with a timeout and removes its temporary directory.
The [upstream resolver](https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/WorkspaceFileResolver.cpp)
loads Luau configurations through `Luau::extractLuauConfig`; no configuration
opt-out was found in the inspected path. The
[upstream changelog](https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/CHANGELOG.md)
records executable configuration before the transformation-plugin feature.
This is not a claim that every possible integration is impossible. A demonstrably
non-executing integration, or an explicitly approved change to the canonical
policy, is required before proceeding. Snapshot rewriting, workspace isolation
or an exception for sandboxed type functions was not silently substituted.

The candidate manifest records `RejectedNonExecutionGate` and
`LanguageServerQualified: false`. Extension `CheckLanguageServer` also rejects
startup unconditionally: changing that manifest bit cannot bypass the gate.
Static Core/host validation remains available in development. Functional LSP
probes below are evidence from controlled fixtures, not permission to run the
candidate on user workspaces or a shipping qualification.

## Requested completion report

1. **Verdict:** BLOCKED. Core, generation, static transport and editor client have
   partial implementation and passing focused checks. Required language, E2E,
   platform, deployment and current-main gates are incomplete.
2. **CarbonLuau starting commit:**
   `3f6a3196b28e2243dc8802d3b8ff2374196f3c89`, the observed origin/main at start.
3. **Extension starting commit:**
   `85236a0b66b680bda46d94f0a3ed038253a193a7`, also its observed origin/main.
4. **Baseline reconciliation:** applied
   `33f9c75c62759cc093e06477b25b2fe05f0f03e5` without committing, preserving
   runtime changes. Subsequently incorporated
   `03b6d68346e01747e3b9250f1f4c4668ae3d8383` by task-only stash, fast-forward
   and inspected reapplication. While closing the blocker, origin/main advanced
   to `9b27ba5625c86eb32d9168effa2c6e72d8866576` (implemented and qualified
   InventoryOnly GiveItem). That newer commit is **not incorporated** here.
   Reconcile it and refresh metadata/regressions before resuming. Do not treat
   older statements that GiveItem is unimplemented as current-main status.
5. **Implementation/evidence commits:** none in either repository; no push.
   The user's commit/push gate requires all applicable checks to pass.
6. **Final tested commits:** no immutable final implementation commit exists.
   Tests apply to the dirty runtime snapshot based on `03b6d68` plus reconciled
   baseline and these changes, and the dirty extension snapshot based on
   `85236a0`. They do not qualify `9b27ba5` or future reconciliations.
7. **Actual Core extraction:** shared package/archive parser and policy,
   logical-path validation, GUI descriptors and GUI config/limits. An explicit
   `SharedSources.props` allowlist feeds production source packaging and net48
   test composition. Legacy nested package types retain thin compatibility
   wrappers and the production `ToScriptSnapshot` adapter. GUI nested type
   containers preserve names without requiring a plugin object. Core targets
   net48/net10.0, C# 7.3: netstandard2.0 lacked the existing ZIP
   `ExternalAttributes` API. No production dependency upgrade was made.
   Retained/layout/presentation/lifetime code was not moved. Native runtime and
   tooling both consume extracted `semantics/ModulePolicy.hpp` legality rules.
8. **Runtime preservation evidence:** affected managed and native regression
   suites pass on the tested snapshot (item 23). Production wrappers, parser
   checks and module error semantics were retained. This is bounded regression
   evidence, not proof of every runtime behavior. Actual Carbon source
   deployment after extraction and newer GiveItem qualification remain pending.
9. **API metadata:** Core `ApiCatalog` consumes explicit annotations beside
   bootstrap/native bindings, shared descriptors, fonts, limits and release
   identity. It validates covered bindings and relationships without reflection
   over server assemblies or parsing Markdown. Generated catalog includes the
   tested snapshot's Player/Items/TakeItem, GUI, Signals and value surface.
   **It is stale relative to GiveItem commit `9b27ba5`.** TextBox is not advertised.
   Additive schema fields describe operators and structural records.
10. **Definitions:** `ApiArtifacts` deterministically emits
    `generated/carbonluau.d.luau`, docs and tooling metadata from the catalog.
    Typed GetService/Create overloads, readonly properties and Vector3
    metamethods were probed with the candidate new solver. Definitions are
    generated output, not the semantic authority.
11. **Drift/determinism:** Core console tests pass for byte-identical generation,
    culture independence, golden output, IDs/type references/inheritance,
    signatures and negative binding/font/service/native mismatches.
    `--check-generated` uses byte equality. Schema validation and API/architecture
    checks pass. New Foundation A CI enforcement is still pending; existing
    baseline CI covers contract fixtures only.
12. **Static host operations:** `initialize`, `getMetadata`, `validateProject`,
    `resolveProjectGraph`, `shutdown`; generation/check modes are development
    CLI operations. The net10 host loads no Carbon/Rust assemblies or production
    native runtime. The separate `carbonluau_analysis` ABI 1 library links only
    upstream Luau Ast/Common and shared module policy, with no VM/compiler.
13. **Protocol:** CarbonLuau.Tooling 1.0, bounded Content-Length stdio framing,
    strict UTF-8/JSON, duplicate-field rejection, depth 32, 4096-byte headers,
    8 MiB messages, positive IDs and explicit initialization/capability/API/schema/
    platform/pack checks. Processing is serial; client permits one outstanding
    request and kills a timed-out host after 15 seconds. Revision hashes bind
    canonical snapshots to selected API/pack; stale results are rejected.
14. **Project detection:** snapshot folders/files detect addon manifests,
    supported root/standalone Luau and nested/multi-folder addons; invalid
    candidates retain diagnostics. VS Code supplies open-document contents.
    Host does not read arbitrary workspace paths. Bounds include 32 folders,
    2048 files, 6 MiB graph source, 256 diagnostics and 8192 import mappings.
15. **Local dependencies:** exact declared package IDs bind sibling addons for
    analysis only. Duplicate IDs are ambiguous; public/main visibility and
    undeclared imports use shared rules. Missing local sources are reported
    separately from package validity, including absent optional dependencies.
    No downloader, registry, solver, lockfile or undeclared dependency is added.
16. **Package validation:** `.claddon` and loose addon snapshots use the shared
    production archive parser. Loose snapshots form an in-memory ZIP preserving
    compression checks; empty entries use NoCompression for ZIP compatibility.
    Manifest, IDs, versions, entry/main/public modules, dependencies, paths,
    UTF-8, traversal and canonical limits remain owned by CarbonLuau.
17. **LSP:** pinned candidate 1.70.0 is **rejected**, as demonstrated above.
    The development client/pack wiring exists but startup is disabled. Hover,
    completion and signature help are not delivered or qualified in this state.
18. **Require analysis:** upstream AST parsing recognizes direct literal global
    requires and excludes shadowed local require calls. Shared module rules
    produce legal targets. A trusted generated transform uses only those
    mappings, checks the exact original source and decimal-escapes every byte.
    Controlled fixtures verified sibling public/main imports and type errors
    with original require-by-string semantics. It does not change runtime
    require and is not enabled on workspaces while LSP remains blocked.
19. **Editor UX:** Validate Project, Select Scripting API, Restart Tooling and
    Show Output; snapshot watching/coalescing, canonical Problems, bounded
    Output and status. Selected installed API and package schema are shown;
    failures are headless. No Preview command. Tests stub the VS Code API;
    actual extension-host E2E and full diagnostics-location UX are unqualified.
20. **Workspace Trust:** development activation uses the same static snapshot/
    packaged-host path in trusted and restricted modes. No workspace executable,
    plugin path or external mapping is accepted. Tests cover both modes and
    assert no LSP launch. Virtual workspaces are unsupported. This is not a
    blanket qualification of all VS Code filesystem/provider behavior.
21. **Offline/security:** locally provisioned static host/metadata need no editing
    downloads or network service; no telemetry. Pack loading verifies bounded
    names, confined files and SHA-256 payload hashes; processes use fixed
    packaged paths without a shell. Snapshot/protocol/output admission is
    bounded. Pack provenance is explicitly LocalDevelopmentBuild, not shipping
    attestation. Licensing/provenance closure and complete distribution/offline
    qualification remain pending. Runtime compiler validation reports Unavailable.
22. **Platforms:** Windows x64 Core/host/native parser and focused extension tests
    pass. Linux x64 parser, self-contained net10 host, Core tests and all eight
    black-box host groups pass in the task Docker worker, including the final
    host source corrections. Linux runtime and extension E2E were not run.
    macOS x64/arm64 were not run; platform entries/hashes are not qualification.
23. **Runtime/package checks:** Windows worker passes ScriptCore, RuntimeCore
    and NativeLoadUnload CTest targets and the net48 runtime test executable:
    package/parser/modules/dependencies/lifetimes, GUI 1-3, Player
    Position/Health/Items/Teleport/TakeItem, Foundation E scale/resources and
    replacement/recovery coverage. API and architecture scripts pass. Two
    server packages contain 43 production sources and share SHA-256
    `84d6d90ec0587f9ce3be3cfb5ad91e8e8e28e5b26fd3124e14d7acf04f66cd3c`;
    tooling/native/metadata are excluded. This does not cover newer GiveItem
    changes or real Carbon source deployment after extraction.
24. **Extension checks:** `npm ci --ignore-scripts`, TypeScript, ESLint and seven
    Node tests passed. Coverage includes manifest commands, stubbed activation/
    trust/diagnostics/commands/failures, framing/revision/transform boundaries,
    and a controlled real host/LSP import fixture. Both false and true manifest
    qualification flags are rejected by the startup guard. No VS Code E2E or
    Foundation A CI run was completed. Integration tests require a local pack.
25. **Identities:** runtime/package 0.4.0; scripting API
    0.4.0-experimental/Experimental; native runtime ABI 1.4; provider protocol
    1.2; package schema 1; runtime Luau
    `c6b830185af962c82003f86784e2fe036357c830`, all unchanged. Extension 0.0.1;
    tooling protocol 1.0; API metadata schema 1; parse-only ABI 1;
    development pack `foundation-a-development`; transform revision 1;
    candidate LSP/embedded Luau are distinct identities listed above.
26. **Documentation:** this record, canonical plan/contract/status routing and
    extension README/architecture now describe partial implementation and the
    blocker. Historical baseline validation remains historical. Onboarding
    does not promise working language features or a released tooling pack.
27. **Foundation B handoff:** not ready. First resolve the non-execution
    contradiction explicitly; reconcile latest main and regenerate metadata;
    finish A drift CI, current runtime/package/source-deployment regression,
    LSP corpus/hover/completion/signature, extension E2E, trust and platform
    qualification; record immutable tested commits and pack provenance.
    Only then scope B's full coordinator/supervision, bounded fresh preview
    worker, canonical execution and ToolingPreviewPlan, with protocol/resource/
    security and semantic differential gates. C owns WebView/inspector; optional
    D owns mocks; E owns deterministic addon build and distribution qualification.
28. **Excluded work:** no preview execution/worker, WebView, inspector, mock
    Player/Items, Activated simulation, live-server integration, debugger,
    authoring or VSIX/Marketplace/Open VSX publication began.
29. **Worktree state:** runtime branch `codex/tooling-foundation-a` at
    `C:\Users\aiden\Documents\ChatGPT\CarbonLuau-tooling-a`, HEAD `03b6d68`,
    baseline staged and implementation/evidence unstaged/untracked. Extension
    branch of the same name at
    `C:\Users\aiden\Documents\ChatGPT\carbonluau-vscode-a`, HEAD `85236a0`,
    implementation/evidence unstaged/untracked. Original runtime checkout
    `C:\Users\aiden\Documents\ChatGPT\RustCarbonLuau` was read only throughout;
    last observed clean at `9b27ba5`. Recovery stash
    `8914bc74b902ebabca8759ad500f460c31c47c65` is retained. Configured author is
    Not_Lowest with the existing GitHub no-reply identity. No implementation
    commits or pushes occurred.

## Reproduction and worker scope

Core metadata tests: `dotnet run --project tests/tooling -- <runtime-root>`.
Generation: `dotnet run --project src/CarbonLuau.Tooling -- --generate <runtime-root>`;
check: replace `--generate` with `--check-generated`. Protocol tests:
set `CARBONLUAU_TOOLING_HOST` to the self-contained host beside the platform
analysis library, then run `python tests/tooling/TestHost.py`. Candidate execution
probe is separate and deliberately demonstrates rejection; it never runs user
workspace code. `tools/Provision-ToolingPack.py --help` describes local pack
assembly from already downloaded, hash-pinned inputs; it performs no downloads.

Sustained native/runtime work ran on `dockerbox`, with task state confined to
`C:\Sandbox\Codex\{Workspaces,Builds,Artifacts}\CarbonLuauToolingA-20260921`.
Native builds use two jobs; Linux tests use a disposable container capped at
two CPUs and 4 GiB. Existing worker containers were preserved. Linux uses a
task image based on the pinned .NET 10 SDK image; no SDK or host setting was
installed/changed. The worker clock lag required normalizing task-source
timestamps and an invocation-scoped signed-package future-date tolerance;
the host clock and signature verification were not changed. Build/test scripts
and development binaries remain in ignored task build/artifact directories.
