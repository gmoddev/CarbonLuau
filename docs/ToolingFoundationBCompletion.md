# Tooling Foundation B completion and qualification

This record covers the isolated Foundation B implementation on 2026-09-21.
[Implementation](ToolingFoundationB.md), [protocol](ToolingContracts.md#foundation-b-preview-operation)
and [plan schema](../tooling/preview-plan.schema.json) own the technical details.
The accepted scoped determinism rule is in [the canonical baseline](ToolingBaseline.md).

## Delivery record

Local qualification passed before the implementation commit. The following
implementation revisions and CI outcomes are recorded below. A Windows extension
CI run exposed a private analysis-directory sharing violation during rapid
snapshot replacement. The correction retries only Windows sharing/lock violations
for a one-second budget after process/watch disposal; a persistent failure still
closes the supervisor. Real held-file tests cover delayed release and persistent
failure. Preview/analysis execution limits and Workspace Trust are unchanged.

| Item | Recorded result |
|---|---|
| Runtime implementation/evidence commit | [`6552a3b3ff127a897a205d76a9f79e974a720d78`](https://github.com/gmoddev/CarbonLuau/commit/6552a3b3ff127a897a205d76a9f79e974a720d78) |
| Extension implementation commit | [`30e82cdb55b70e09fc6daf12e04daf197ac43b6b`](https://github.com/gmoddev/carbonluau-vscode/commit/30e82cdb55b70e09fc6daf12e04daf197ac43b6b) |
| Runtime validation CI | [PASS: Windows, Linux, sanitizers](https://github.com/gmoddev/CarbonLuau/actions/runs/35677388316) |
| Runtime tooling CI | [PASS: Windows/Linux execution and macOS static](https://github.com/gmoddev/CarbonLuau/actions/runs/35677388350) |
| Runtime baseline CI | [PASS](https://github.com/gmoddev/CarbonLuau/actions/runs/35677388358) |
| Extension CI | [Initial run](https://github.com/gmoddev/carbonluau-vscode/actions/runs/35677413183): Linux and macOS static passed; Windows exposed the cleanup race above. Corrected run pending. |

## Required completion report

1. **Verdict:** PASS for the agreed Windows/Linux Foundation B scope after the
   recorded local gates. macOS execution remains unqualified/static-only. Remote
   CI status is reported separately above.
2. **CarbonLuau starting commit:** current main
   `c37c36e0508a759c899dfdd4a1545cc923a88fc6`.
3. **Extension starting commit:** current main
   `85236a0b66b680bda46d94f0a3ed038253a193a7`.
4. **Foundation A reconciliation:** fast-forwarded isolated runtime to
   `4fd3b8010e0002f74ce620c3098135ba4acccbc5` and extension to
   `e7256299d78e8c24c10272ecb326e8512cd8758e`. No reconciliation commit or conflict;
   completed Player work was preserved.
5. **Foundation B commits:** see delivery record. Implementation, tests, protocol,
   shared-source routing and determinism evidence are committed together.
6. **Final tested commits:** the delivery record identifies immutable CI heads.
   Pre-commit execution used the same source in isolated worker directories with
   explicit local pack provenance; that provenance is not a release identity.
7. **Worker architecture:** extension -> dedicated data-only coordinator ->
   native containment launcher -> fresh `--preview-worker` -> pinned VM and
   canonical retained/compiler implementation -> validated ToolingPreviewPlan.
   The worker exits after one result; no VM is constructed in the coordinator.
8. **Limits:** 1,000 ms external execution deadline, native entry maximum 100 ms,
   64 MiB Luau heap, 256 MiB process profile. Windows enforces process commit in
   a suspended-launch Job with ActiveProcessLimit=1. Linux hard RLIMIT_DATA is
   256 MiB; address-space reservation ceiling is 2 GiB and RSS is sampled against
   256 MiB every 10 ms. RSS sampling permits transient overshoot; these are not
   identical memory metrics. CLR heap also has a 64 MiB cap within the process
   budget. Startup 15 s, completion 2 s, fallback reap 5 s; frames 8 MiB, headers
   4 KiB, JSON depth 32, stderr 64 KiB. No canonical limit was raised.
9. **Capability boundary:** workspace inputs are bounded logical IPC snapshots;
   no workspace directory is needed by preview. Canonical path/package admission
   rejects traversal. Luau has no exposed arbitrary filesystem, network, process,
   environment, native-loading or Rust host capability. Trusted pack/runtime
   startup files remain readable. There is no portable OS filesystem/network
   sandbox: a native/VM escape is outside this capability guarantee. Trust is
   required and does not itself provide isolation.
10. **Preview API:** real retained Gui create/set/get/query/clone/destroy and
    immutable GUI/value types, with publication validation/rollback. Gameplay
    services, Show/Hide, GUI signal connection, client scroll commands and queued
    task callbacks report unavailable errors. Initialization is synchronous;
    there are no Player/Items mocks or silently successful host calls.
11. **Modules:** actual native loader/cache/domain semantics, required/optional
    local dependency binding, `require("local/module")`, `@addon` and public
    addon modules. Private/undeclared imports, cycles and depth overflow fail.
    No Node resolution or ambient filesystem fallback. Entry defaults to
    `init.luau`; an explicit admitted relative source can be selected.
12. **Viewport:** explicit finite width/height in 1..8192 pixels, including
    fractions; 1280x720, 1920x1080, 2560x1440, 3440x1440 and custom 1111x777
    qualified. One ScreenGui selects automatically; multiple screens return a
    bounded choice list and accept an explicit run-local ScreenId.
13. **Plan:** schema 1 with exact API, pack, semantic revision and content build
    identity, snapshot revision, project/entry and viewport. Core owns generation
    and retained reconstruction/recompilation validation. The extension stores
    copied data only after compatibility/trust/staleness checks.
14. **Hierarchy/geometry:** parent-first nodes, run-local object IDs, class/name,
    ordered children, final pixel rectangles, effective clips/visibility and
    paint order. Helpers remain inspectable. No production action/client tokens.
15. **Inspection:** typed retained wire values remain separate from final
    projected rectangles. Layout owner, clip ancestors and helper objects explain
    list/grid/padding effects without requiring TypeScript layout calculations.
16. **Accounting:** canonical retained objects, arranged-child maximum,
    projected elements, clip depth, associated limits and explicitly scoped
    projection byte estimate. Private scroll/clip elements are counted by the
    production compiler. Exact object 128, arranged-child 64, projected-element
    257 and clip-depth 4 fixtures pass; one-over failures are controlled.
17. **Fidelity:** retained values and geometry are Authoritative; text rasterization
    Approximate; image pixels Unavailable. No false exact game-rendering claim.
18. **Images/fonts/scrolling:** None/Sprite/Png/Item-skin/SteamAvatar identity and
    canonical GuiFont are preserved without downloads/assets/fonts. Scroll
    canvas/content geometry is canonical, offset authority is PresentationLocal,
    and preview's InitialTopLeft is ConvenienceOnly. No readable CanvasPosition.
19. **Source mapping:** optional `Source` seam is explicitly null. No invented
    creation-site mapping or production identity change.
20. **Determinism:** all 17 deterministic fixtures compare complete semantic
    goldens and repeat across fresh workers on both platforms. Identical retained
    state projects deterministically. Arbitrary scripts can observe VM pointer
    strings/object-key order; real counterexamples are retained in
    [evidence](evidence/ToolingB-DeterminismLimit.json). The user approved this
    scope on 2026-09-21. The negative probe is intentionally excluded from
    passing CI; neither VM semantics nor visible output is rewritten.
21. **Production equivalence:** all 17 fixtures run through actual production
    retained registry/Player Presentation/compiler and preview. Geometry,
    clip/visibility/order and accounting agree on Windows and Linux/Mono.
    Rust CUI JSON equality and authenticated client rendering are not claimed.
22. **Adversarial qualification:** loops, heap allocation, huge sources, malformed
    snapshots, traversal, forbidden globals/host APIs, cycles/depth, private and
    undeclared addon imports, identity mismatch and schema mismatch fail closed.
    Hostile test-only workers exercise crash/hang/native-memory refusal,
    malformed/oversized frames, log flooding, wrong nonce/revision/protocol,
    duplicate responses and fabricated geometry. Every worker failure is
    followed by a successful real preview. Malformed outer framing closes that
    connection; a new coordinator connection is required by the protocol.
23. **Recovery:** real worker PIDs are checked gone after failures, cancel and
    input EOF. Wrong-revision cancellation is rejected. Extension source/trust
    changes clear prior plans; errors never retain stale output. Preview failure
    does not disable static tooling or language analysis. A reap failure is the
    explicit fail-closed exception requiring Restart Tooling.
24. **Performance:** observed two-fresh-run round trips, including launch, hashing
    and validation: Windows trivial 757 ms, text/font moderate 893 ms, objects-128
    934 ms, projected-257 876 ms, multi-module addon 803 ms. Linux bind-mounted
    container: trivial 4158 ms, text/font 4089 ms, objects-128 4024 ms,
    projected-257 3782 ms, multi-module 3821 ms. These include startup outside execution time and are
    observations, not latency guarantees or isolated VM timings.
25. **Foundation A regression:** metadata/generated definitions, static host and
    project graph tests, exact require transform/LSP tests, executable config
    exclusion, type-function deadline/heap controls and trust policy pass.
    Preview does not run through luau-lsp or replace its separate security model.
26. **Windows:** x64 real native worker, hostile process tests, full managed/native
    runtime, production equivalence, deterministic packages and real VS Code
    Restricted Mode/reopen/trust/preview recovery tests pass.
27. **Linux:** x64 real native worker under the installed limits, hostile process
    tests, full native/Mono runtime, production equivalence, deterministic
    packages and real VS Code under Xvfb pass. Docker is the test environment;
    it is not part of the shipped extension's claimed sandbox.
28. **macOS:** user explicitly retained static-only status. No local macOS runner
    was available. CI may build/check static tooling; it does not enable or
    qualify executable analysis/preview on either macOS architecture.
29. **Runtime CI:** existing runtime/native/sanitizer/package workflow plus
    Core/schema/goldens/worker/security tooling workflow cover B. See delivery
    record for actual run conclusions; workflow presence alone is not evidence.
30. **Extension CI:** pinned canonical source, provisioned pack, TypeScript/lint,
    25 unit/integration/security tests on qualified platforms, and actual VS Code
    trust/recovery coverage. macOS retains static checks. See delivery record.
31. **Runtime/package regressions:** 5 native CTests and the full managed runtime
    suites pass on both platforms, including existing GUI, Player/inventory,
    addon, compiler/deadline and lifecycle fixtures. All 5 Linux ASan/UBSan/leak
    tests pass; managed loader lifecycle tests pass on both platforms and Windows
    import checks reject dynamic MSVC CRT dependencies. Source package has 54
    intended production C# files; tooling adapters, workers and fixtures are
    excluded. Windows/Linux release bundles are byte-for-byte reproducible.
32. **Versions:** package 0.4.0, scripting API 0.4.0-experimental, native ABI 1.4,
    addon protocol 1.2, package/API metadata schema 1 unchanged. Tooling protocol
    1.0; preview-worker protocol 1.0, preview plan/policy/bridge 1; pack
    `foundation-b-development`. Runtime Luau
    `c6b830185af962c82003f86784e2fe036357c830`; LSP 1.70.0 and its Foundation A
    analysis policy/proxy/transform pins remain unchanged.
33. **Documentation:** canonical baseline records approved determinism scope;
    AICONTEXT routes implementation/completion evidence; protocol/schema and
    extension architecture record the internal seam and qualification limits.
34. **Foundation C handoff:** call extension `RequestPreview(Selection)` and
    `GetPreviewState()`. Selection supplies ProjectId, optional Entry/ScreenId,
    Viewport. Paint emitted geometry, select Nodes, inspect Retained/Projected,
    show Accounting/fidelity, and handle refresh/errors/selection. C must not
    recalculate layout, UDim geometry, clipping or projection costs. It must
    preserve trust, cancellation and stale-result rejection.
35. **Scope:** no WebView, GUI inspector UI, Player/Items mock, interaction
    simulation, live server, debugger, source editing, stateful hot reload,
    VSIX or Marketplace/Open VSX publication began.
36. **Branches/worktrees:** both use `codex/tooling-foundation-b`, runtime
    `CarbonLuau-tooling-b` and extension `carbonluau-vscode-b`, isolated from the
    shared main checkout. Earlier worktrees/stashes and
    `carbonluau-cleanup-ZPFPMR` remain untouched. Delivery does not merge main.

## Reproduction and worker ownership

Run `tools/Build-Tooling.ps1 -Extension <extension>` for the canonical pack,
Core/goldens, real worker and hostile-worker gates. Run
`tools/Test-ToolingContracts.ps1`, `tools/Test-Api.ps1`,
`tools/Test-Architecture.ps1`, runtime native/managed tests and
`tools/Test-Release.ps1` with the appropriate native/compiler artifacts.
The extension uses `npm ci --ignore-scripts`, `npm run check` and
`npm run test:e2e` (Xvfb on Linux). Actual analysis tests require the pack and
native fixture environment variables configured in its workflow.

Sustained local qualification ran through SSH host `dockerbox`, with native
build parallelism 2 and Linux test containers limited to 2 CPUs/4 GiB. Source,
incremental builds and artifacts are under the matching
`C:\Sandbox\Codex\Workspaces`, `Builds`, and `Artifacts` directories named
`CarbonLuauToolingB-20260921`; the Windows editor workspace is
`CarbonLuauVscodeB-20260921`. Linux used the retained .NET 10 tooling and .NET 9
runtime/Mono images. Task containers exit after tests; reusable build caches and
artifacts remain. No permanent service or user-facing window was started on the
controlling PC.
