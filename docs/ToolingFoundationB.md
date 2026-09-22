# Tooling Foundation B: bounded GUI preview

Implemented on `codex/tooling-foundation-b`. The preview profile is qualified for
Windows x64 and Linux x64; macOS remains static-only. Qualification evidence and
delivery revisions are in [the completion record](ToolingFoundationBCompletion.md).
This is not a Foundation C visual preview or a distribution release.

## Reconciliation and ownership

The isolated runtime worktree started at main
`c37c36e0508a759c899dfdd4a1545cc923a88fc6` and fast-forwarded to completed
Foundation A `4fd3b8010e0002f74ce620c3098135ba4acccbc5`. The isolated extension
worktree started at main `85236a0b66b680bda46d94f0a3ed038253a193a7` and
fast-forwarded to `e7256299d78e8c24c10272ecb326e8512cd8758e`. No reconciliation
commit or semantic conflict was needed. Newer Player/inventory work is retained.
Shared main checkouts, earlier worktrees/stashes and `carbonluau-cleanup-ZPFPMR`
are not modified by B.

`CarbonLuau.Core/Gui` now owns the actual production retained mutations, values,
image validation, tree state, render contracts, affine layout/list/grid/padding,
clipping, visibility/order and projection accounting. Production adapters retain
Player Presentations, action tokens, synchronization, delivery and scroll effects.
The preview adapter uses the same shared tree and compiler. `GuiPixelProjection`
resolves that compiler's affine output at an explicit viewport; it does not
implement another layout engine. Core remains net48/C# 7.3 compatible and has no
Carbon/Rust/Unity dependency. Tooling-only API adapters are excluded from the
server source set. The production source package intentionally includes the
shared pure GUI implementation, including pixel projection.

## Execution and containment

The extension's internal Preview service opens a dedicated coordinator session.
The coordinator launches its same executable in `--preview-worker` mode through
a pack-pinned native launcher. Every request gets a fresh VM, domains, module
cache and retained tree. A nonce/revision-bound Ready/Execute exchange separates
bounded admission/runtime startup from workspace execution. Only the worker
constructs `NativePreview`; the coordinator validates data without a Luau VM.

| Boundary | Enforcement and qualification scope |
|---|---|
| Extension host | Stdio data only; no Luau/native bridge loading or layout implementation |
| Coordinator | Bounded framing, four outstanding requests, one active worker, independent cancellation/EOF reader |
| Luau VM | Exact runtime pin, bootstrap, module loader and allocator; 64 MiB heap; no filesystem, network, process, environment, reflection or native-loading capabilities |
| Windows x64 process | Suspended launch into kill-on-close Job; 256 MiB process commit; one active process; no breakaway |
| Linux x64 process | Before exec: 256 MiB hard RLIMIT_DATA and 2 GiB virtual-address reservation ceiling; 256 MiB RSS watchdog every 10 ms; owned process group and parent-loss signals |
| OS filesystem/network sandbox | Not provided. A compromised native process retains user OS authority. Process limits are not capability isolation |
| Workspace Trust | Required before launch and dispatch, checked again before storing output; revocation/reload and source changes terminate pending execution and clear stored results |
| macOS | Static-only; no preview or executable-analysis qualification/promotion |

Linux RLIMIT_DATA is not total RSS or file-backed memory. The RSS threshold is
sampled, so transient overshoot is possible. The 2 GiB address-space allowance is
for runtime reservations, not an increased physical-memory allowance. Managed GC
also has a separate 64 MiB hard-heap setting; it does not replace the Luau or
process controls. Native allocations, loaded runtime pages and managed/Luau heaps
share the process budget. These are resource controls, not a proof against a VM
or native-code sandbox escape.

Admission and trusted runtime setup have a 15-second bound. After Execute, the
coordinator enforces the 1-second execution wall deadline independently of VM
interrupts. The reused native entry API additionally limits each entry to 100 ms.
Completion has a separate 2-second EOF/exit allowance; kill/reap fallback is
bounded at 5 seconds. Stderr is capped at 64 KiB, and input/result frames at 8 MiB
with a 4 KiB header and JSON depth 32. Source/module/package bounds remain those
of canonical admission. A reap failure blocks further execution until explicit
Restart Tooling. Other failures permit a fresh subsequent preview.

The snapshot is logical, bounded and supplied entirely through IPC. Preview
requires no materialized workspace directory and never reads workspace paths.
Trusted pack/runtime files are still read for startup and verified against the
pack manifest. Workspace settings cannot select the host, native library,
compiler, limits, plugins or transforms. The preview native bridge uses the exact
production VM/module/bootstrap source and shared bounded compiler configuration
inside the disposable worker. Production retains its separate compiler process.
Preview does not use luau-lsp and does not alter the Foundation A analysis model.

## Initialization, modules and API

The request selects an admitted project and defaults to `init.luau`. An explicit
relative entry may select a supplied source. No editor-specific public API, GUI
DSL or `.gui` format exists. Required dependencies activate in canonical ordinal
ready order; available optional bindings follow existing activation semantics.
Actual native require/cache/public/private/dependency/domain behavior is reused.
Root projects use their canonical `modules/` snapshot; addon modules are the
canonical package source map. There is no Node resolution, downloading or ambient
filesystem fallback.

GUI creation, mutation, querying, values, clone/destroy and publication rollback
use shared retained logic. Gameplay/server services fail with a bounded
`UnsupportedPreviewApi` diagnostic. No Player/Items mocks are supplied. GUI
Signal connection/dispatch, Show/Hide and client-local scroll commands are
unavailable in static preview; they are not silently skipped. Asynchronous task
callbacks are rejected: B captures synchronous initialization only. Synchronous
native/Luau module initialization is supported. No Activated simulation or
authenticated command ingress occurs.

One produced ScreenGui is selected automatically. Multiple screens produce a
selection diagnostic with bounded `Details.Screens`; the caller resubmits the
same snapshot with `ScreenId`. Selection creates a tooling projection at the
requested viewport, not a real Player Presentation. Viewport dimensions must be
finite, between 1 and 8192 pixels inclusive; fractions are supported. No desktop
or game-client dimensions are queried.

## Plan, fidelity and validation

The [schema](../tooling/preview-plan.schema.json) and
[protocol](ToolingContracts.md#foundation-b-preview-operation) own the C handoff.
Plans include exact API/pack/build/semantic revision, project/entry, viewport,
selected/available screens, parent-first retained nodes (including helpers),
canonical typed retained wire values, final pixel rectangles, visibility/order,
layout-manager ownership, paint, clip topology, scrolling and resource counts.
Run-local numeric IDs never include production action tokens or client IDs.

The coordinator reconstructs the bounded retained data using Core's real value
and mutation validation, runs the shared compiler, and compares every semantic
plan field before forwarding. It also checks identities and rejects unknown
top-level fields. This catches malformed hierarchy, wrong property kinds,
invented geometry/accounting, private output and source/identity mismatches.
The extension separately checks protocol and exact plan identity before storing
a copied result; changing inputs or receiving an error removes the previous plan.

Retained and projected Position/Size remain separate when list/grid geometry
overrides retained values. Accounting uses stable schema-1 fields paired with
their canonical limits: RetainedObjects/MaxObjectsPerScreen,
ProjectedElements/MaxProjectedElementsPerScreen,
MaximumArrangedChildren/MaxChildrenPerObject and
MaximumClipDepth/MaxEffectiveClipDepth. Values originate in `GuiConfig.Validate`
and `GuiRenderCompiler`, not extension constants. Projected costs include private
clip roots and scroll components; a scrolling node costs more than one element.
`CanonicalProjectionEstimatedBytes` explicitly describes run-local canonical
projection estimation, not production token lengths or exact Rust CUI delivery.

Geometry and retained/image/font identities are Authoritative. Text
rasterization is Approximate. Image pixels are Unavailable: None, Sprite, Png,
Item/skin and SteamAvatar identity is preserved without fetching images or
bundling Rust assets. Fonts preserve GuiFont identity without font redistribution.
Scrolling preserves retained canvas settings and resolved content geometry;
InitialTopLeft is ConvenienceOnly, with PresentationLocal offset authority.
There is no readable retained CanvasPosition. `Source` is explicitly null until
a reliable creation-site mapping mechanism exists.

## Determinism and qualification

The user approved the [baseline's scoped determinism rule](ToolingBaseline.md#preview-semantics-fidelity-and-webview)
on 2026-09-21. Pointer text and object-key iteration visibly vary in the pinned
VM; [Windows/Linux counterexamples](evidence/ToolingB-DeterminismLimit.json) retain
the evidence. Their probe intentionally reports unequal arbitrary-script plans.
It is not a failing deterministic-fixture gate or a universal nondeterminism
detector. The VM starts with a fixed math seed; production semantics are unchanged.

Seventeen deterministic fixtures cover five viewports, nested/anchored Frames,
text/buttons/four fonts, image identities, list/grid/padding, hidden children,
nested layouts, clip-depth four, scrolling, ZIndex/order and exact retained,
arranged-child and projected-element limits. Full semantic goldens are generated
by Core and compared against real native-worker output and fresh repeats.
Separate production fixtures use the real retained registry, Player Presentation
and compiler to compare geometry, clip/visibility/order and accounting. These
tests do not claim authenticated game-client rendering or Rust CUI JSON equality.

Qualification commands: `tests/tooling` (Core/goldens),
`RuntimeTests --preview-equivalence <root>` (production comparison),
`TestPreview.cjs <pack>` (real execution/resource/module/recovery), and
`TestPreview.cjs <test-pack> --supervisor-fixture` (hostile disposable worker).
The hostile worker is a test-only executable, never part of a provisioned pack.
`--determinism-gate` demonstrates the documented arbitrary-script limitation and
is not included in passing CI. Foundation A static/LSP/Workspace Trust tests and
affected native/managed/server-package tests remain required.

## Foundation C handoff

The extension exports internal `RequestPreview(Selection)` and
`GetPreviewState()` seams. Selection contains ProjectId, optional Entry/ScreenId
and Viewport. C must invoke that seam, paint final Paint geometry, select Nodes,
display Retained versus Projected and Accounting, and handle refresh/selection
errors. It must not calculate UDim/UDim2, layouts or projection costs. It must
treat text as text and preserve fidelity labels. C owns the WebView and inspector;
B adds no visual command, WebView, mock fixture, live server, debugger, visual
editing, stateful hot reload or VSIX publication.
