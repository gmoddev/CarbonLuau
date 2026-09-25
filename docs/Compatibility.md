# CarbonLuau compatibility and validation policy

This document owns compatibility promises, version identities and evidence requirements. Runtime rules are in [Invariants.md](Invariants.md); task workflow is in [AICONTEXT.md](../AICONTEXT.md). It records a narrow supported target direction, not universal compatibility.

## Support dimensions

| Dimension | Current commitment and evidence limit |
|---|---|
| Operating system | Windows and glibc-based Linux are the v0.1 targets. Worker Windows and Ubuntu 24.04 Docker passed Phase 0; this does not qualify every Windows release/Linux distribution or define a minimum glibc version. |
| Architecture | x64 process and native binaries only. ARM64, x86, macOS and other targets are unqualified and rejected by Phase 0. |
| Managed runtime | Keep deployed code compatible with the proven .NET Framework/Mono-style Carbon runtime. The `net48` loader test passed on Windows and Mono 6.8. A .NET SDK used for tooling is not the server runtime. |
| Carbon | CarbonPlugin and source `.cszip` are the integration/package model. Worker evidence is Carbon 2.0.259.0; no unrestricted future Carbon-version guarantee. |
| Rust server | Worker evidence is Rust 2633 / Steam build 25230300. New server builds need affected compatibility checks, not assumptions based on the same game name. |
| Luau | Vendored commit is recorded in [LUAU_REVISION.txt](../native/third_party/LUAU_REVISION.txt). Phase 1 links compiler/VM; its platform and containment evidence is separate in [Phase1-Validation.md](Phase1-Validation.md). |
| Hosting provider | The user's Shockbyte Linux server passed only the historical Phase 0 load/probe/unload/reload gate based on user-provided logs. Phase 1-5 full-runtime behavior remains unqualified there. By user decision on 2026-09-15, Shockbyte full-runtime qualification is deferred and is not a Phase 5/v0.1 gate; this is not a support claim, and requalification is required before advertising Shockbyte as qualified or supported. Other servers/plans and provider policy changes remain unqualified. |

Dates, source/artifact hashes, runtime paths, CI links and the different depths of worker versus Shockbyte testing are owned by [Phase0-Validation.md](Phase0-Validation.md). PROVEN describes the tested native-load gate, not the safety of the future VM or all features in the design.

Carbon currently documents targeting .NET Framework 4.8 with `Carbon.targets`. Do not introduce modern-.NET-only APIs or netstandard2.1-only dependencies into the plugin without proving support on the actual runtime and documenting an accepted compatibility change. Keep project-owned native code and managed runtime aligned. [Carbon project setup](https://carbonmod.gg/devs/creating-your-project). Source packaging follows [Carbon's ZIP documentation](https://carbonmod.gg/devs/features/zip-script-packages).

## Version identities

Developer tooling is separate from the production server support table above.
[ToolingBaseline.md](ToolingBaseline.md) owns its independent extension, protocol,
pack, metadata, preview-plan, tooling-native and LSP identities, developer-platform
targets and qualification requirements. In particular, macOS tooling does not
imply macOS CarbonLuau server support. Baseline adoption changes none of the
runtime identities below. For a documentation/bootstrap-only tooling change,
run document/contract checks, extension build/static/manifest checks and a
deterministic server-package exclusion check. Full runtime regressions become
mandatory when shared production code is actually extracted; see
[ToolingFoundationA.md](ToolingFoundationA.md).

Keep these identities conceptually separate and record those relevant to a result: Carbon build, Rust server build, pinned Luau commit/build options, CarbonLuau package version, project-owned native ABI compatibility, and CarbonLuau public scripting API compatibility.

Phase 0 package `0.0.1`, Phase 1 package `0.1.0`, native ABI `1.0`, and the probe magic are not scripting API versions. Native ABI major mismatch is rejected before runtime binding; layouts and ownership are specified in [Phase1.md](Phase1.md#native-boundary-and-ownership). A package bump does not automatically mean a script break. Phase 3 introduces package `0.3.0`, additive native ABI `1.2`, and the separate scripting identity `CarbonLuau` / `0.3.0-experimental` / `Experimental`, inspectable through read-only game fields and operator status. D8/D12 own the minimum policy; [public compatibility](api/Compatibility.md) documents it. Additive changes preserve existing contracts; removing/renaming an API or changing its types, lifetime, failures or authorization is breaking and requires an explicit version, documentation and migration decision. A larger deprecation/negotiation framework remains deferred.

The published `v0.3.0` release maps package `0.3.0`, scripting API
`0.3.0-experimental` and native ABI `1.2`. The roadmap's “v0.1” label is a scope
name, not another semantic version.

Post-release Foundation A source adds internal domain exports as additive native
ABI `1.3`. This does not alter the already-published v0.3.0/ABI 1.2 artifacts or
assign a new scripting API identity. A future release must deliberately map its
package, native ABI and still-pending addon scripting/protocol identities.

The qualified v0.4.0 candidate assigns the additive addon and GUI-capable identity
`CarbonLuau 0.4.0-experimental`. It maps package `0.4.0`, native ABI `1.4`,
provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau
revision `c6b830185af962c82003f86784e2fe036357c830`. The identities remain separate,
and v0.3.0 artifacts are unchanged. The machine-readable mapping is
[release.json](../release.json), with build and provenance instructions in the
[release guide](Release.md).

GUI Foundation 1 is implemented through Foundation 1F and publicly closed by
Foundation 1G. It is included in package `0.4.0` and scripting API
`0.4.0-experimental`. No 0.5.0 bump is warranted because 0.4.0 has not been
released and GUI is additive to that first public addon candidate. Native ABI
`1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and the
pinned Luau revision are unchanged. Authenticated-client visual layout, cursor,
actual click receipt and client reconciliation remain explicitly unqualified;
that evidence is no longer a gate for the experimental identity.

GUI Foundation 2A through 2C implement deterministic layout, typed images and
retained scrolling. [Foundation 2E](GuiFoundation2E.md) qualifies their
lifecycle, shared-view, cross-domain, replacement, recovery, provider and
failure behavior. [Foundation 2F](GuiFoundation2F.md) closes public documentation,
examples, compatibility, bounded scale and release preparation and assigns this
implemented subset to the existing package `0.4.0` and scripting API
`0.4.0-experimental`. No 0.5.0 identity is needed because 0.4.0 remains
unreleased and the subset is additive. `TextBox`
is **DEFERRED / NOT IMPLEMENTED**. Rust Dedicated Server app `258550`, build
`25353106`, was inspected for GUI-2D and trims the complete command and
`ConsoleSystem.Arg.FullString`; trailing and whitespace-only input cannot meet
D16's exact-preservation contract. Parsed argument reconstruction is not an
acceptable substitute. This finding is build-specific and may be revisited if
the host later exposes a bounded opaque text-preserving UI-input payload.

Public behavior changes need deliberate compatibility review, documentation and behavioral tests. Prefer adapting to host changes beneath the facade. If an accepted public behavior cannot be preserved, state the break and migration decision explicitly; do not silently expose new host internals to compensate.

Persistence Foundation 1 was explicitly assigned scripting API
`0.5.0-experimental` on 2026-09-23; it is not part of historical 0.4 availability.
[D12](Invariants.md#d12--scripting-and-protocol-identity) owns the decision and
[Release.md](Release.md) the independent development mapping: package/tag fields
stay 0.4.0/v0.4.0, while 0.5.0 is the intended future package, not a published
release. Existing catalog entries retain historical SinceApi values. New
persistence metadata follows actual bindings and is Experimental after scoped
1B qualification; preview remains unavailable. Its implementation-source CI is
recorded in [1B validation](PersistenceFoundation1B-Validation.md). Combined closure
and exact Windows/Linux live limits are recorded in [1C](PersistenceFoundation1C.md).
No version label establishes overall PASS.
The additive reserved completion export requires native ABI 1.5; no provider
protocol, addon schema or Luau change is implied.

Persistence Foundation 2 architecture is resolved by [D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query) and [PersistenceFoundation2.md](PersistenceFoundation2.md). Its additive future Query surface uses CarbonLuau-owned derived indexes: no declaration is required for basic Query, optional Indexes hints only prewarm/pin derived state, and there is no author-managed schema version. It joins the same unreleased `0.5.0-experimental` scripting identity. No Foundation 2 binding, generated metadata, package/tag/release bump, native ABI change or preview availability exists until implementation qualifies.

## Dependency and host upgrades

Luau upgrades must be explicit scoped changes. Preserve its pin and MIT/Lua attribution; review relevant upstream API, sandbox, compiler, bytecode and behavior changes. Validate affected builds and runtime failure paths against the candidate pin before claiming support. Do not edit the vendor tree or update dependencies incidentally while implementing another feature. Internal compiler/VM bytecode compatibility is tied to the selected pin; external untrusted bytecode remains excluded by I7.

For Rust/Carbon updates, record candidate versions, compile against the actual references where appropriate, then run the canonical smoke fixture and affected adaptation tests. Requalify target-host behavior when the native loader, platform dependencies, sandbox or host policy could change the outcome. Preserve old-version evidence as historical evidence, not proof for the new version. Setup scripts fetch mutable upstream releases: record what was actually installed each run.

## Validation selection

Use the narrowest levels that exercise the changed behavior, adding broader tests when the boundary requires them:

| Change | Relevant evidence |
|---|---|
| Documentation/policy | Check links, rule ownership, contradictions, diff scope and accuracy of existing evidence; no live-server rebuild required. |
| Native ABI/runtime | Native unit/ABI and negative-path tests on affected platforms, managed interop tests, then live Carbon for changed integration/lifecycle behavior. |
| Managed validation/configuration | Focused tests of accepted/rejected inputs; live Carbon when host APIs or lifecycle are affected. |
| Script sandbox/limits/reload | Adversarial failure and post-failure recovery tests as well as happy paths; integration/live fixture when claiming server behavior. |
| Host/platform/dependency upgrade | Candidate build identity, canonical smoke and affected behavior tests; actual provider validation for provider-specific claims. |

Revalidate final source when a subsequent implementation edit invalidates a result. Preserve evidence for unaffected code; do not rerun the entire matrix for a documentation fix. Compile-only CI does not prove live Carbon behavior. Label user-supplied logs, direct process inspection, CI and assumptions separately. A phase verdict states the tested scope, limitations and any blocked acceptance items; do not weaken a criterion to obtain PASS.

## Canonical Phase 0 fixture

The build commands live in [Phase0.md](Phase0.md). Reuse these existing fixtures rather than making each prompt invent another matrix:

- [ProbeHost.cpp](../tests/native/ProbeHost.cpp): dynamic export resolution, expected probe value, 100 native load/unload cycles through CTest.
- [Managed loader tests](../tests/managed/Program.cs): actual loader, RID/path checks, failure fixtures and 100 disposal cycles; Windows file deletion and Linux process-map checks.
- [Windows live fixture](../tools/Test-WindowsRuntime.ps1) and [Linux live fixture](../tools/Test-LinuxRuntime.py): ten plugin cycles, library release checks, controlled failure/recovery, reload and health evidence in isolated servers.
- [CI workflow](../.github/workflows/phase0.yml): Windows/Ubuntu builds, native/managed tests and packaging. The `.cszip` contains C# source only; the native runtime and compiler worker are deployed beside each other outside it.
- [Provider acceptance procedure](Phase0.md#shockbyte-acceptance): load/probe and unload/reload logs on the actual authorized target. Do not run destructive fixtures on a live user server.

Harness assertions have limits: the Windows live script waits for a general `Unavailable:` message and prints status replies; the recorded run was reviewed for exact error categories and normal replies. Linux explicitly checks expected failure text and health. Neither runner proves long-term leak freedom. Use current-run logs/offsets when reusing a server; a prior run's startup line is not new evidence. These limitations do not invalidate the recorded Phase 0 run or justify a policy-only harness refactor.

## Phase 1 evidence contract

The feature boundary remains [design section 31](CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases), not a new API specification. The relevant native tests and failure behaviors are in design sections 27 and 29. This section owns their phase-specific qualification; implemented [Phase 1 fixtures](Phase1.md#qualification-fixtures) and [actual results](Phase1-Validation.md) accompany the execution core.

Before marking the execution core complete, demonstrate compiler/VM create/destroy and source execution; controlled syntax/runtime errors; sandbox allowlist enforcement; configured allocation exhaustion and execution deadline failures; successful permitted execution after recoverable failure; deterministic cleanup after partial initialization/error/timeout; and failure diagnostics. Test the chosen protected-error mode and callback crossing rules, including low-memory error handling. Use sanitizers for the native lifecycle where practical, recording what actually ran.

Validate managed/native result and ownership transport, configuration bounds, and generation replacement. Candidate compile/load failure must preserve a working generation. Confirm status/reload and native lifetime inside actual Carbon on affected Windows/Linux environments. Revalidate the hosted target for claims specific to its new native dependencies or behavior; Phase 0's probe does not prove a Luau-linked runtime there.

Resolve the relevant [decision gates](Invariants.md#decision-register) before enabling their behavior and record the selected limits and remaining containment limits. Keep the accepted initial configuration candidates in the design until implementation qualifies them. Do not require Phase 2 gameplay APIs, events, modules or a full task scheduler to complete Phase 1; conversely, do not enable such features without their own validation.

## Phase 2 and Phase 3 evidence contracts

For Phase 2, use the [Phase 2 contract](Phase2.md) and
[qualification record](Phase2-Validation.md): package 0.2.0, additive native ABI
1.1, controlled source snapshots/modules and generation-owned scheduling. Required
coverage includes path/UTF-8/reparse confinement, module cache/cycles, queue and
frame bounds, timeout recovery allowance, atomic replacement, native allocation
faults/sanitizers, and live Windows/Linux Carbon reload/unload. Existing Phase 0/1
fixtures remain in CI; ordinary CI does not claim to run a Rust server. Provider
qualification remains separate. The historical consistency review below is not a
description of the current feature set.

Phase 3 uses the [facade contract](Phase3.md), [public API reference](api/README.md)
and [qualification record](Phase3-Validation.md). Required evidence includes
connection-lifetime invalidation, bounded signals and commands, A → failing B →
successful C registration transactions, permission enforcement before Lua, late
callback rejection, provisional-message rejection, scheduler/recovery regressions,
Windows/Linux actual Carbon integration, affected native sanitizers, public
documentation/examples agreement and green final-source CI. Controlled real host
objects must be labeled separately from authenticated client/session or delivery
evidence. Provider qualification remains separate.

Phase 5 uses the [hardening contract](Phase5.md) and
[qualification record](Phase5-Validation.md). It adds no scripting capability.
Its canonical matrix combines production-facade reload and failed-candidate
soaks, controlled-host connection churn, pressure and timeout boundaries, actual
plugin/native unload, sustained status/memory sampling and Carbon's profiler.
Windows/Linux live results, CI, provider deployment and authenticated real-client
evidence remain separate claims. Shockbyte full-runtime deployment is deferred
and non-gating by the recorded user decision, without converting its Phase 0
probe evidence into support. By user decision on 2026-09-15, authenticated
real-client qualification is also deferred and non-gating for Phase 5/v0.1; this
is a qualification deferral, not evidence. Authenticated Steam/network
establishment, visible client `Player:SendMessage` receipt, network-driven
PlayerAdded/PlayerRemoving and real same-account reconnect remain unqualified and
require future requalification before any support claim. A task-scoped Linux
worker substitution does not change the repository's general worker policy or
qualify another operating system.

## Foundation A evidence contract

[Foundation A](FoundationA.md) is an internal post-v0.3.0 runtime foundation, not
a public addon release or scripting API assignment. Its affected evidence is native
domain/cache/publication tests, real managed/native root-domain replacement and
facade-resource rollback tests, existing Phase 1–3 regressions, allocation faults,
Windows/Linux native and managed builds, and affected Linux sanitizers. Healthy
reload must retain `VmGenerationId` while changing `DomainLifetimeId`; fatal
retirement/reconstruction must change both. A caught failed first module load must
publish no cache entry and no CarbonLuau-owned module-created task, command or
listener. Historical Phase 0–5 records are not rewritten by these new results.

Foundation A does not qualify public addon registration, provider lifecycle,
package/archive parsing, dependency graph behavior, `@id` resolution, multi-domain
scale/fairness policy or an addon scripting identity. Those remain future gates in
D2/D5/D12/D14.

## Foundation B evidence contract

[Foundation B](FoundationB.md) is the experimental managed-provider/package
registration layer above Foundation A. Its affected evidence is strict bounded ZIP
and JSON ingestion, copy-before-return snapshots, provider/reference ownership,
registration state transitions, transactional addon activation/replacement,
deterministic unregister/provider-unload teardown, stale-token rejection, repeated
domain lifecycle checks, Phase 0-3 regressions, Windows/Linux managed runtime tests,
live Carbon load/unload ordering and the existing native sanitizer suite. Historical
Phase 0-5 records are not rewritten by these results.

At the Foundation B revision, dependency resolution, package-qualified `require`,
public module exports, provider C# capabilities, multiple versions, remote package
transport, scale/fairness policy and a public addon scripting API identity were not
qualified. Manifests declaring dependencies remained `Blocked` and were never
partially resolved; the Foundation C contract below supersedes only that dependency
lifecycle limitation.

## Foundation C evidence contract

[Foundation C](FoundationC.md) makes D14 required and optional declarations
operational as host/runtime lifecycle bindings. Its affected evidence is exact
VM-generation/domain-lifetime binding, deterministic required activation and
restoration order, required and optional loss, bounded SCC detection, one-attempt
restoration failure, dependency-aware transactional replacement, provider
unload/reload propagation, stale-binding rejection, graph bounds and teardown to
baseline. Re-run the Foundation A/B and Phase 0-3 regressions, Windows/Linux
managed runtime tests, live Carbon provider ordering and the existing native
sanitizer suite. Historical Phase 0-5 and Foundation A/B records are not rewritten
by these results.

Foundation C does not qualify package-qualified `require`, public module exports,
`addon:IsDependencyAvailable`, root-to-addon imports, provider capabilities,
version solving, multiple versions, downloads/registries, scale/fairness policy or
a public addon scripting API identity. Dependency declarations are lifecycle
relationships only; caught errors and runtime behavior never mutate the graph.

## Foundation D evidence contract

[Foundation D](FoundationD.md) makes D4/D5 public-module composition operational
on exact Foundation C bindings. Its affected evidence is strict `main` and
`publicModules` ingestion; local/public cache identity; same-reference values and
state; private visibility; ordinary error/retry/cycle/depth/yield behavior;
cross-domain module publication commit and rollback; stale A1 import rejection;
retained ordinary values versus stale host facades; required A2 reconstruction;
optional non-rebinding; addon metadata and dependency availability. Re-run the
Foundation A–C and Phase 0–3 regressions, Windows/Linux managed runtime tests,
affected native sanitizers and live Carbon provider/dependency ordering. Historical
records are not rewritten.

Foundation D does not qualify provider C# capabilities, root imports, addons
depending on root, version ranges or solving, multiple versions/instances,
downloads/registries, restricted exposure, async capabilities, Foundation E,
multi-domain fairness or a public addon scripting API identity.

## Foundation E evidence contract

[Foundation E](FoundationE.md) closes the first experimental public addon
surface without adding another major capability. Its required evidence is
root-only, 1, 10, 50 and 100-addon resource and latency measurement; bounded
global scheduling and progress under saturation; shared-heap exhaustion;
package/parser boundaries; provider and CarbonLuau lifecycle ordering; stale
token and exact dependency behavior; Windows/Linux regression workers; affected
ASan/UBSan/leak tests; public identity assignment; documentation and examples.

Foundation E qualifies the existing 64 MiB default for the tested representative
configurations and makes D4/D5/D7/D10/D14 addon behavior public under
`0.4.0-experimental`. It does not qualify provider-defined C# capabilities,
root-to-addon imports, addons depending on root, version solving, multiple
versions/instances, package downloads/registries, restricted exposure profiles,
async capabilities, per-addon hard heap isolation or adversarial multi-tenant
containment. Historical Phase 0–5 and Foundation A–D evidence remains scoped to
the revisions it tested.

## Foundation F evidence contract

[Foundation F](FoundationF.md) reorganizes implementation ownership without an
intentional semantic change. Its required evidence is the complete native and
managed regression matrix, ABI/interoperability checks, module/publication and
addon lifecycle coverage, the 100-addon representative fixture, package and API
checks, Windows and Linux builds, and ASan/UBSan/leak detection. Structural
review must also identify remaining shared state and responsibility
concentrations rather than treating smaller files as proof of safety.

Foundation F does not change the scripting API, native ABI, provider protocol,
package schema, Luau revision, limits or package format. It creates a synchronous
compiler owner for future work but does not qualify compiler containment,
off-thread compilation, compile deadlines, bytecode transport or lock-scope
changes. Historical evidence remains scoped to the revisions it tested.

## Foundation G evidence contract

[Foundation G](FoundationG.md) contains Luau compilation in a shipped helper
process built from the pinned vendor revision. Required evidence covers bounded
normal and adversarial source compilation, a real wall timeout, crash and
malformed-protocol recovery, exact provenance checks, publication rollback,
deterministic process teardown, Windows/Linux regressions, sanitizers and release
packaging. Compilation remains synchronous to the admitting owner thread, but no
Luau VM state crosses the worker boundary and the global VM registry lock is not
held while the worker is awaited.

Foundation G does not change the scripting API, native ABI, provider protocol,
package schema, source/package limits or pinned Luau revision. It does not add a
general bytecode or IPC surface and does not qualify unrelated post-v0.4 work.

The current Foundation G verdict is **PARTIAL** solely because Windows live/local
qualification is **DEFERRED / UNQUALIFIED**. Hosted Windows CI is recorded
separately and cannot close that gate. The later Windows supplement must cover
worker creation, wall termination, the 256 MiB job limit, crash/restart,
IPC/protocol rejection, packaging/deployment, live Carbon integration and
teardown without an orphan process. This Foundation-G-specific deferral neither
rewrites nor invalidates the historical Windows qualification of Foundations A-F.
The exact compiler-worker implementation revision requiring that supplement is
`e3025401c3085f0552bbfe3d045c3143c4ded005`.

## GUI Foundation 1 architecture gate

[D15](Invariants.md#d15--gui-foundation-1-retained-presentation-model) resolves
the GUI ownership, publication, presentation, interaction and reconciliation
model before implementation. [GuiFoundation1.md](GuiFoundation1.md) owns detailed
rationale and implementation guidance. Foundations 1A through 1F provide the
runtime evidence, and Foundation 1G owns public closure and identity.

GUI qualification covers deterministic retained/value behavior,
cross-domain ownership and provisional publication, exact Player/token lifetime,
bounded scheduling and overload, backend fault convergence, replacement and D9
recovery, teardown/leak behavior and affected Foundations A-G regressions. Claims
about actual rendering, layout, cursor behavior or button receipt still
require a current Carbon/Rust server with an authenticated client. Record the
qualified host revisions and keep adapter-specific CUI observations as evidence,
not permanent API guarantees. Numeric flush, payload and reconciliation targets
remain internal tuning values rather than public compatibility promises. Missing
authenticated-client evidence does not prevent experimental API availability,
but it does prevent claims about those unobserved client outcomes.

### GUI Foundation 1A evidence contract

[GUI Foundation 1A](GuiFoundation1A.md) implements internal managed descriptors,
validated limits, render/backend contracts and a deterministic in-memory backend.
Its evidence is schema completeness, explicit non-reflective ownership, bounded
deterministic request construction, backend ordering/fault behavior, pinned host
source inspection, managed Windows/Linux regression tests, package checks and
architecture/API/documentation checks. It changes no public or native interface.

This phase does not qualify a retained runtime, production Rust CUI adapter,
client rendering, UI interaction or D15 publication journal. Later Foundation 1
phases provide the runtime evidence. Authenticated-client evidence remains
required only for claims about actual rendering and click behavior.

### GUI Foundation 1B evidence contract

[GUI Foundation 1B](GuiFoundation1B.md) implements the domain-bound `Gui`
service, opaque retained object/value userdata, bounded tree lifecycle, GUI
Signal ownership and the D7/D10 publication journal. Its affected evidence is
constructor/field/equality validation; every class/property default and bound;
identity, ordering, parenting, clone/destroy and teardown behavior; nested and
caught publication rollback; exact foreign-owner commit/rollback/stale-owner
handling; shared-VM cross-domain values; GUI-1A plus Foundations A-G regressions;
Windows/Linux runtime tests; and affected sanitizer coverage.

Foundation 1B does not qualify presentations, Player/viewer state, production
rendering, layout translation, synchronization, action tokens, client event
ingress or visible/click behavior. Those remained later GUI gates at this phase;
Foundation 1G subsequently assigns the experimental identity.

### GUI Foundation 1C evidence contract

[GUI Foundation 1C](GuiFoundation1C.md) implements internal Presentations,
`ScreenGui:Show`/`Hide`/`IsShown`, exact D11 Player connection binding,
deterministic full render plans, D15 layout translation, opaque client IDs and
the production Rust CUI backend. Its affected evidence is presentation and
publication transitions; disconnect/reconnect and teardown; deterministic
parent/Z ordering; layout, color, text and cursor golden plans; bounded CUI JSON;
backend unavailable/failure containment; actual Luau facade calls; server-side
Carbon AddUI/DestroyUI integration where available; GUI-1A/1B and Foundations
A-G regressions; Windows/Linux runtime lanes; and affected sanitizer coverage.

Foundation 1C does not qualify automatic property patches, dirty tracking,
patch/full selection, periodic reconciliation, action tokens, the private client
command, interaction limits or Activated ingress/delivery. Authenticated-client
evidence remains mandatory for visual layout, cursor, click and client
reconciliation claims. The identity remained unassigned at this phase and was
subsequently resolved by Foundation 1G.

### GUI Foundation 1D evidence contract

[GUI Foundation 1D](GuiFoundation1D.md) implements committed ScreenGui revisions,
bounded property dirty state, coalesced Rust CUI `update=true` patches,
structural full replacement, periodic full reconciliation and one shared
owner-thread post-Luau GUI flush. Its affected evidence is patch and structural
classification; newest-state coalescing; Show/Hide/reparent/destroy ordering;
dirty overflow; actual serialized payload limits; Update/Replace fault
convergence; publication rollback; exact Player disconnect and teardown;
persistent domain/Presentation fairness; modeled 1, 10, 50, 100 and 256-viewer
cost; GUI-1A through 1C and Foundations A-G regressions; Windows/Linux runtime
lanes; packaging; and affected sanitizer coverage.

Foundation 1D does not qualify action tokens, the private client command,
interaction rate limiting, Activated ingress/delivery or GUI Foundation 1E.
Authenticated-client evidence remains mandatory for actual visual layout,
cursor behavior, client reconciliation and click receipt. Numeric flush, payload
and checkpoint values remain internal tuning values rather than public
compatibility promises. The identity remained unassigned at this phase and was
subsequently resolved by Foundation 1G.

### GUI Foundation 1E evidence contract

[GUI Foundation 1E](GuiFoundation1E.md) implements one private Player-origin
Carbon command, 128-bit opaque presentation-bound action tokens, exact Player
connection validation, bounded per-action and per-Player rates, atomic bounded
listener fanout and operation-9 revalidation immediately before Luau entry. Its
affected evidence is distinct per-Player tokens; malformed, forged,
cross-Player, stale, hidden and destroyed rejection; Hide/Show, full-rebuild,
uncertainty, disconnect, domain and VM lifecycle rotation; property-patch token
retention; queue, listener and registry bounds; publication rollback and commit;
normal Activated callback ordering and mutation; hostile-input stress; GUI-1A
through 1D and Foundations A-G regressions; Windows/Linux runtime lanes;
packaging; live Carbon registration where available; and affected sanitizer
coverage.

Authenticated real-client click receipt remains mandatory before claiming that
a Rust client delivered the private command or visibly completed an
interaction. Hosted or local model tests and server-side Carbon registration do
not substitute for that evidence. The identity remained unassigned at this
phase and was subsequently resolved by Foundation 1G.

### GUI Foundation 1F evidence contract

[GUI Foundation 1F](GuiFoundation1F.md) closes healthy root/addon replacement,
cross-domain owner/holder retirement, fatal VM reconstruction, provider and host
teardown, pending-work invalidation, backend retry convergence, bounded operator
diagnostics and registry leak/stress behavior. Its affected evidence is failed
and successful candidate publication; stale references/tokens across every
lifetime transition; callback pre-entry suppression; newest-state backend fault
recovery; baseline registry counts; 100 replacement/failure/recovery cycles;
1,000 Show/Hide, Clone/Destroy and reconnect cycles; GUI-1A through 1E and
Foundations A-G regressions; Windows/Linux runtime lanes; live Carbon lifecycle;
and affected sanitizer coverage.

Authenticated real-client rendering, layout, cursor, reconciliation and click
receipt remain mandatory before those client-observed behaviors can be
advertised as qualified. Controlled-host and server-side live lifecycle
evidence do not substitute for that gate. A GUI-specific live provider fixture
also remains unavailable; controlled provider/domain lifecycle evidence is
reported separately. The identity remained unassigned at this phase and was
subsequently resolved by Foundation 1G.

### GUI Foundation 1G evidence contract

[GUI Foundation 1G](GuiFoundation1G.md) closes public API documentation,
runnable root/addon examples, descriptor-to-document auditing, release identity,
release notes, bundle contents and final-head reproducibility for the complete
D15 scripting surface. Its affected evidence is GUI-1A through 1F, Foundations
A-G, addon/package/parser, replacement/recovery, synchronization, interaction
security, registry baseline, Windows/Linux runtime, sanitizer, packaging,
release and documentation checks.

The selected identity remains package `0.4.0` and scripting API
`0.4.0-experimental`; native ABI `1.4`, provider protocol `1.2`, package schema
`1` and the Luau pin do not change. Authenticated-client visual, cursor,
click-receipt and reconciliation evidence remains unqualified and non-gating.
Foundation G's Windows live/local compiler-worker supplement remains separately
deferred and is not replaced by hosted Windows CI.

## GUI Foundation 2 architecture gate

[D16](Invariants.md#d16--gui-foundation-2-deterministic-layout-and-rich-controls)
adopts the additive Foundation 2 architecture. [GuiFoundation2.md](GuiFoundation2.md)
owns detailed rationale, exact surface matrices, implementation sequencing and
qualification planning. GUI Foundation 2A implements and
qualifies `GuiObject.LayoutOrder`, `UIListLayout`, `UIPadding`, deterministic
affine layout projection and bounded layout dirty synchronization. Its exact
evidence is recorded in [GuiFoundation2A.md](GuiFoundation2A.md). GUI Foundation
2B implements immutable typed `ImageSource`, `ImageLabel`, `ImageButton`, bounded
image projection and the existing secure Activated path; its evidence is in
[GuiFoundation2B.md](GuiFoundation2B.md). GUI Foundation 2C implements retained
`ScrollingFrame` configuration, Presentation-local scroll state, bounded
private viewport/content projection and layout/image composition; its evidence
is in [GuiFoundation2C.md](GuiFoundation2C.md). This does not alter D15 evidence.

The implemented Foundation 2 subset preserves Foundation 1 behavior for scripts
that do not use Foundation 2 objects. Qualification covers deterministic
layout, retained versus Presentation-local state, projection cost and full
reconciliation bounds, typed image validation, exact action authority,
adversarial typed input, publication/replacement/recovery and affected
Foundation 1 regressions. Client-observed scrolling remains unqualified and
non-gating. TextBox requires current authenticated-client evidence before its
support claim. Image
source mapping and host payloads are qualified, while authenticated-client
visual image behavior remains unqualified and non-gating.

TextBox additionally requires exact supported single-line transport preservation
for spaces, leading/trailing and repeated whitespace, quotes, backslashes and
Unicode. Failure to prove that contract defers TextBox; it does not authorize
normalization or console-argument semantics. Concrete object/input/source/queue
bounds in D16 are the initial hard implementation envelope and must be qualified
before support. Submission rates and scheduling/timing values remain tuning
targets rather than permanent compatibility promises.

D16 and GUI Foundations 2A/2B/2C did not independently assign a release identity.
GUI Foundation 2F includes their qualified layout/image/scrolling surface in
package `0.4.0` and scripting API `0.4.0-experimental`. Native ABI `1.4`, provider
protocol `1.2`, package schema `1` and the pinned Luau revision are unchanged.
Server-side layout/image/scroll plans and serialized host mappings are
qualified; authenticated-client visual layout, image and scrolling behavior
remain unqualified and non-gating.

## GUI Foundation 3 architecture gate

[D17](Invariants.md#d17---gui-foundation-3-deterministic-grids-clipping-fonts-and-presentation-scroll-intent)
adopts the additive Foundation 3 architecture. [GuiFoundation3.md](GuiFoundation3.md)
owns detailed rationale, exact surface matrices, implementation sequencing and
qualification planning. GUI Foundations 3A through 3D implement the accepted
surface, and [GUI Foundation 3E](GuiFoundation3E.md) closes the combined
server-side model, lifecycle, scale, compatibility and release-candidate audit.

Foundation 3 server-side qualification establishes deterministic grid behavior,
bounded clipping projection, font identity and normalized one-way scroll-effect
publication. Authenticated-client qualification remains separate for clipping
visuals and hit eligibility, availability of every exposed `GuiFont`, and actual
scroll behavior. If
authenticated-client evidence cannot satisfy the clipping contract,
`ClipsDescendants` is omitted from the implemented/public release subset. If a
font is unavailable, that value is removed before qualification rather than
silently falling back. An unreliable scroll partial update may use bounded
structural replacement, but does not authorize `CanvasPosition` or readback.

[GUI Foundation 3B](GuiFoundation3B.md) implements D17's bounded
`Frame.ClipsDescendants` source slice with private mask projection, effective
depth four including ScrollingFrame viewport clips, structural action
reconciliation and unchanged projection envelopes. It remains **IMPLEMENTED /
CLIENT-UNQUALIFIED**. That limitation is explicit and non-gating for the
experimental source API; it is not a claim that visual or clipped-hit behavior
passed. Windows native/local GUI-3B evidence is separately **DEFERRED /
UNQUALIFIED** while DockerPC is unavailable; hosted Windows does not replace it.

[GUI Foundation 3C](GuiFoundation3C.md) implements D17's immutable `GuiFont`,
retained TextLabel/TextButton `Font`, canonical render identity, explicit
private backend mapping and patch-synchronization slice. It is **IMPLEMENTED /
CLIENT-UNQUALIFIED** until an authenticated supported current client proves all
four fonts render without fallback and qualifies mutation, multi-viewer,
replacement and recovery behavior. Windows native/local GUI-3C evidence is
separately **DEFERRED / UNQUALIFIED** while DockerPC is unavailable; hosted
Windows remains separate evidence.

[GUI Foundation 3D](GuiFoundation3D.md) implements D17's exact-Player
`ScrollTo`, `ScrollToTop` and `ScrollToBottom` Presentation effects with
top-left normalized public coordinates, backend-private vertical inversion,
bounded latest-wins state, transactional publication, rebuild-first retry and
lifecycle cleanup. It is **IMPLEMENTED / CLIENT-UNQUALIFIED** until an
authenticated current client qualifies orientation, both axes, two-viewer
isolation, coalescing, rebuild ordering, retry and lifecycle behavior. Windows
native/local GUI-3D evidence is separately **DEFERRED / UNQUALIFIED** while
DockerPC is unavailable; hosted Windows remains separate evidence.

GUI-3E assigns the additive implemented Foundation 3 surface to the still
unreleased package `0.4.0` and scripting API `0.4.0-experimental`. This is the
smallest accurate identity because no published 0.4 compatibility surface is
being superseded. Native ABI `1.4`, provider protocol `1.2`, package schema `1`
and the pinned Luau revision remain unchanged. `TextBox` remains deferred and
unimplemented under D16's exact-text transport gate.

Authenticated-client supplements must test the exact implementation revisions
`eb25da029c83b2d1a48c4b21bc86e40858ef4dcc` for clipping,
`74ac4075e3d369bc51ca24aa7f3a0b33688d8d61` for fonts and
`6543951131f6f4ac2b31a1d11d6a5ad0bcd36595` for scroll effects, or documented
source-equivalent descendants. GUI-3E does not invent client evidence.

GUI-3A Windows native/local qualification is **DEFERRED / UNQUALIFIED** because
the required DockerPC infrastructure is unavailable. This GUI-3A-specific gate
is non-gating by explicit scope decision and must remain distinct from both
hosted Windows CI and Foundation G's Windows live/local deferral. Hosted Windows
CI does not qualify the missing native/local DockerPC environment. A future
supplemental run must test GUI-3A source revision
`b292ecd6ff6c52081de0a991979968502a8353a0` or a documented source-equivalent
descendant. Historical Windows qualification for earlier foundations remains
valid within its original envelope.

## Player Interaction Foundation 1 architecture gate

[D18](Invariants.md#d18--player-interaction-foundation-1) adopts the additive
Player Interaction Foundation 1 architecture. The complete host rationale,
surface matrix, phase sequence and qualification plan are retained in
[PlayerInteractionFoundation1.md](PlayerInteractionFoundation1.md). Player-1A
implements immutable `Vector3` and read-only exact-connection `Player.Position`;
Player-1B implements live read-only exact-connection `Player.Health` and
`Player.MaxHealth`; Player-1C implements `Items:Exists` plus bounded physical
`Player:CountItem` and `Player:HasItem` under `0.4.0-experimental`. Their qualification records are
[PlayerInteractionFoundation1A.md](PlayerInteractionFoundation1A.md) and
[PlayerInteractionFoundation1B.md](PlayerInteractionFoundation1B.md) and
[PlayerInteractionFoundation1C.md](PlayerInteractionFoundation1C.md).
Player-1D implements committed-only `Player:Teleport(Vector3)` and records the
exact-build adapter plus available qualification in
[PlayerInteractionFoundation1D.md](PlayerInteractionFoundation1D.md).
Authenticated-client convergence remains unqualified. Player-1F-A implements
TakeItem; Player-1F-B implements InventoryOnly GiveItem with its
[target/platform qualification](PlayerInteractionFoundation1FB-Validation.md).

Supplemental authenticated-client Teleport qualification remains deferred.
Player-1F-C closes the combined lifecycle, stress, documentation and public
qualification planned as Player-1E within its recorded scope. Revised D13 routes Inventory-M1
for the deterministic model and mutation gate, Player-1F-A for TakeItem,
Player-1F-B for GiveItem and Player-1F-C for combined mutation closure.
Inventory-M2's G1 conclusion is superseded; G2-G5 evidence remains. Player-1F-A uses
that exact adapter for TakeItem; Player-1F-B records supported-host GiveItem
requalification. Player-1F-C records the combined mutation closure below.

Player-1F-C initially reproduced a D18 failure at `9b27ba5` on Windows and Linux:
cold modules loaded during committed execution could perform GiveItem, TakeItem
and Teleport before publication. The correction consults both outer admission
and current first-load publication; cached committed calls remain allowed.
See [the preserved failure and resumed qualification](PlayerInteractionFoundation1FC.md)
for platform, live-host, stress, packaging and client-evidence limits.

D13 now accepts CarbonLuau-serialized, definite-rejection inventory mutation:
bounded mutation-free PREPARE, explicit first-host-effect COMMIT and one bounded
physical VERIFY. `false` means COMMIT never began, `true` means the physical
postcondition was verified, and a controlled error after COMMIT means inventory
may have changed. Rust inventory is not described as globally atomic; no
rollback, plugin isolation or unchanged-state guarantee is added.

Player-1C establishes an exact target-build, bounded, nonrecursive physical
main/belt/wear adapter that does not accept a hook-virtualized count as the
canonical answer. Its hard limit is 128 direct entries across the three accepted
containers, compared with the target build's normal 24 + 6 + 8 capacities.
The reusable scanner and evidence are recorded in
[PlayerInteractionFoundation1C.md](PlayerInteractionFoundation1C.md).

Inventory mutation requires exact-connection serialization, committed execution
and target-build evidence. Inventory-M2 retains G2 returned-Item terminal-state
observability and G4 supported cleanup;
TakeItem's G3 host-result plus physical-delta verification; and shared G5 work
bounds on Rust build `25353106` plus Carbon `2.0.259`. The narrow adapter and
evidence are recorded in
[InventoryMutationM2Validation.md](InventoryMutationM2Validation.md). TakeItem
implementation and qualification are recorded in
[PlayerInteractionFoundation1FA.md](PlayerInteractionFoundation1FA.md).
GiveItem's historical G1 no-drop conclusion was incomplete and is superseded.
[I12](Invariants.md#i12--trusted-in-process-host-interference) defines the general
supported-host/interference scope; the required normal-host and separately
labeled hostile-mutation matrix is in
[Player-1F-B](PlayerInteractionFoundation1FB.md#required-1f-b-requalification).
Player-1F-B subsequently implemented GiveItem and the InventoryOnly enum and
recorded supported-host requalification in
[its validation record](PlayerInteractionFoundation1FB-Validation.md).
That historical evidence did not cover the Player-1F-C cold-module restriction;
the correction is qualified separately above. The complete historical rationale is retained in
[InventoryOwnershipFailureReassessment.md](InventoryOwnershipFailureReassessment.md).

Player-1D passed qualification of the exact Rust/Carbon relocation sequence on
the supported server build. Authenticated-client evidence for destination
convergence, rubber-band resistance, long-distance network-group transition,
fall state, mount/parent handling and repeated teleports remains unqualified.
Server/model evidence does not establish that client behavior. This does not
block the server-qualified experimental surface or the read-only Player slices.

D18 adoption and Player-1A through Player-1F-A change no identity. Package remains `0.4.0`, scripting API
remains `0.4.0-experimental`, native ABI remains `1.4`, provider protocol
remains `CarbonLuau.Addons` / `1.2`, package schema remains `1`, and the pinned
Luau revision is unchanged. Player-1A through Player-1F-A passed the available Linux
native/runtime, managed regression, deterministic packaging and sanitizer
matrix. Player-1B additionally passed three controlled-host live Carbon cycles
on the exact target build. In those historical runs the workstation Windows managed/native
runtime suite passed; its pre-existing Foundation G compiler-worker memory gate
did not qualify. DockerPC Windows local/live was unavailable and is not claimed.
Hosted Windows CI is recorded separately.
Player-1F-C subsequently passed native/managed/containment tests on the available
Windows worker; this does not retroactively qualify Foundation G's deferred
Windows live gate or provide a Windows live Carbon result.
Player-1D additionally passed exact-build live server qualification; its
authenticated-client behavior and DockerPC live/local result remain unqualified.
Later Player phases require their own
applicable Windows/Linux/native/live/sanitizer qualification.


## World/Entity Foundation 1 architecture gate

[D20](Invariants.md#d20--worldentity-foundation-1) adopts a design-only read-safe
world/entity baseline. [WorldEntityFoundation1.md](WorldEntityFoundation1.md)
owns the host research, exact-lifetime model, bounded surface and implementation
gates. No production `Workspace` or `Entity` API exists merely because the
architecture is adopted.

**HOST-PRIMITIVE-GATED / DEFERRED:** Entity-1A is BLOCKED by the missing supported
authoritative incarnation/retirement proof. The [investigation](WorldEntityLifetimeInvestigation.md)
records bypassed registry-transition hooks and kill veto, but no demonstrated
same-object pooled reincarnation. Snapshot heuristics cannot close this gate.
Only after the gate closes may the Entity-1A through Entity-1C sequence proceed.
Entity-1B may expose `game:GetService("Workspace")`,
`Workspace:GetEntityById(Id: string) -> Entity?`, and read-only
`Entity.Id`, `Entity.Prefab` and `Entity.Position` only after the exact
target proves keyed registry lookup, exact BaseEntity lifetime validation,
prefab capture/bounds, root world-space Transform reads and lifecycle behavior.
The starting feature-sensitive host target is Rust Dedicated Server app
`258550`, build `25353106`, with Carbon `2.0.259`, protocol
`2026.09.03.0`, revision `21063e8490adf412101bcc7d1cfe9d6280f61e80`,
matching the later Player/inventory evidence. Current upstream Rust metadata has
advanced beyond that build; current upstream source/docs are research input, not
a substitute for target-build qualification.

Foundation 1 intentionally has no collection query, radius query, lifecycle
Signal, Spawn or Destroy compatibility promise. A result limit does not authorize
an O(all server entities) scan, and D20 establishes no CarbonLuau world index.
Future collection/event APIs require explicit inspected-work/result/queue bounds.
Future Spawn/Destroy/Position mutations require their own exact-host evidence and
Player-1F-C's corrected committed-only mutation predicate.

D20 architecture adoption changes no package version, scripting API identity,
native ABI, provider protocol, package schema or Luau revision. The published
0.4.0 line is unchanged. An implemented additive World/Entity surface may later
be planned for `0.5.0-experimental`, but no such identity is assigned until
Entity-1C public closure.

## Persistence Foundation 1 qualification

[D21](Invariants.md#d21--persistence-foundation-1) and
[PersistenceFoundation1.md](PersistenceFoundation1.md) adopt the next runtime
foundation. Its original adoption was **design only**, not implementation of a
DataStoreService/worker/SQLite dependency or public metadata. [Design validation](PersistenceFoundation1-Validation.md)
records upstream research and local architecture checks, not crash/durability or
platform PASS. Original adoption changed no identities. The later D12 user
decision assigns persistence to 0.5.0-experimental without altering prior evidence.

[Persistence-1A investigation](PersistenceFoundation1A.md) records a Windows
durability qualification blocker: effective EXTRA readback does not imply the
inspected `win32` VFS performs post-delete directory synchronization. Its separate
Windows/Linux call-path probes are not production storage or power-loss PASS.

The [durability follow-up](PersistenceDurabilityInvestigation.md) recommends stock
PERSIST/EXTRA after Windows NTFS and Linux ext4-container process-crash, sync-fault
and Linux ASan/UBSan research checks. Its [D21 amendment](PersistenceD21Amendment-Proposed.md)
was **approved on 2026-09-23**: canonical storage now selects PERSIST/EXTRA.
That amendment alone did not close production 1A's implementation/qualification gates. Those research results
do not qualify OS crash, physical power loss or actual Carbon acknowledgement.

**Current private 1A: PASS within recorded scope.**
[PersistenceFoundation1A.md](PersistenceFoundation1A.md) records final implementation
`c6f894472887c2b9008d845b5d1e5dcdcf30ddcf`, Windows/Linux native and supervised-worker
tests, exact quotas/codec, real lost-ack/no replay, fault/crash/corruption,
ASan/UBSan, packaging/clean extraction and final-source CI. Inherited WAL,
including page one restored by hot-journal recovery, is rejected without
WAL/SHM creation; normal supported recovery remains intact. Hosted Windows UTF-8
stdin preamble framing is explicitly qualified. The amended 1,280-MiB figure is
an operational budget/qualification target/diagnostic threshold, not a hard
physical-allocation invariant. Hard logical and SQLite page/file-length limits
remain. No public persistence API, 1B, actual Carbon persistence integration,
Shockbyte or power-loss qualification is implied. Its recorded identities remain
historical; preserve its negative evidence. The separately authorized
[Persistence-1B](PersistenceFoundation1B.md) is implemented and qualified within
its recorded scope, including green implementation-source CI. The separately
authorized [Persistence-1C](PersistenceFoundation1C.md) records combined closure,
the fair-dispatch correction and newly measured Windows/Linux Carbon persistence
and server restart. Its exact final verdict/CI gate remains authoritative; 1B
alone does not establish combined closure.

Documentation/evidence-only closure needs canonical-routing, link, signature,
decision/bounds, whitespace, scope and fixture-syntax checks, not unrelated native,
live-server or sanitizer matrices. Preserved Entity research checks are historical
evidence, not newly executed Entity qualification.

Persistence-1A must pin the helper/SQLite source and qualify codec, worker process,
protocol, filesystem confinement, durable commit, journal recovery, disk/quota
limits and corruption behavior. Persistence-1B adds non-yielding callback admission,
namespace authority, publication/failed candidates, deadlines and stale completions.
Persistence-1C closes Windows x64 and glibc Linux x64 Carbon integration, replacement,
provider unload, shared-VM recovery, server restart, crash/lost-ack uncertainty,
failure injection, hard logical/backend extent and memory/queue bounds plus
qualified physical-allocation operational-budget checks, affected native sanitizer/
allocation-fault and existing regression matrices, public metadata/docs and
final-source CI. Process-kill tests do not prove power-loss safety on arbitrary
hardware. Record SQLite revision, effective PRAGMAs, filesystem/VFS, worker limits,
host versions and exact source/artifacts for each result. Shockbyte remains
separately unqualified. Do not promote private 1A evidence to public-service or
actual Carbon integration PASS.

## Phase 0 consistency review

Reviewed baseline `a88f2eb` on 2026-09-14 against the rules introduced by this policy task:

| Area | Finding |
|---|---|
| Loader/path | Absolute path derived from Carbon data, OS/RID checks, explicit OS loading, no alternate probe search; consistent with I6. This trusted native path is not proof of future module confinement. |
| Probe ABI | C linkage, explicit exported name, scalar magic result, no Luau/allocations across the boundary; consistent with I5's probe-specific contract. |
| Cleanup | Handle/delegate owned by the loader, partial load cleaned up, idempotent dispose and unload hook containment; consistent with current I5/I10 applicability. No VM resources exist yet. |
| Diagnostics | Distinct load/symbol/value failure messages and OS details; controlled failures proven. No protection against arbitrary native corruption is claimed. |
| Packaging/CI | Two partial sources in `.cszip`; native binaries separate. CI tests the loader, not a Carbon stub or server; live evidence is separately recorded. |
| Future guarantees | No sandbox, VM budgets, scheduler or generation replacement implemented; these are future obligations, not Phase 0 violations. |

No concrete runtime correctness contradiction was found that warrants altering the proven Phase 0 implementation. Source, fixtures, tooling, vendor and CI remain unchanged by this policy task. Validation here is static review plus documentation checks; previous valid build/live results remain applicable.
