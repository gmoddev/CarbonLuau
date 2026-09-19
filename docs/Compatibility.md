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

Keep these identities conceptually separate and record those relevant to a result: Carbon build, Rust server build, pinned Luau commit/build options, CarbonLuau package version, project-owned native ABI compatibility, and CarbonLuau public scripting API compatibility.

Phase 0 package `0.0.1`, Phase 1 package `0.1.0`, native ABI `1.0`, and the probe magic are not scripting API versions. Native ABI major mismatch is rejected before runtime binding; layouts and ownership are specified in [Phase1.md](Phase1.md#native-boundary-and-ownership). A package bump does not automatically mean a script break. Phase 3 introduces package `0.3.0`, additive native ABI `1.2`, and the separate scripting identity `CarbonLuau` / `0.3.0-experimental` / `Experimental`, inspectable through read-only game fields and operator status. D8/D12 own the minimum policy; [public compatibility](api/Compatibility.md) documents it. Additive changes preserve existing contracts; removing/renaming an API or changing its types, lifetime, failures or authorization is breaking and requires an explicit version, documentation and migration decision. A larger deprecation/negotiation framework remains deferred.

The published `v0.3.0` release maps package `0.3.0`, scripting API
`0.3.0-experimental` and native ABI `1.2`. The roadmap's “v0.1” label is a scope
name, not another semantic version.

Post-release Foundation A source adds internal domain exports as additive native
ABI `1.3`. This does not alter the already-published v0.3.0/ABI 1.2 artifacts or
assign a new scripting API identity. A future release must deliberately map its
package, native ABI and still-pending addon scripting/protocol identities.

The qualified v0.4.0 candidate assigns the additive addon-capable identity
`CarbonLuau 0.4.0-experimental`. It maps package `0.4.0`, native ABI `1.4`,
provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau
revision `c6b830185af962c82003f86784e2fe036357c830`. The identities remain separate,
and v0.3.0 artifacts are unchanged. The machine-readable mapping is
[release.json](../release.json), with build and provenance instructions in the
[release guide](Release.md).

Public behavior changes need deliberate compatibility review, documentation and behavioral tests. Prefer adapting to host changes beneath the facade. If an accepted public behavior cannot be preserved, state the break and migration decision explicitly; do not silently expose new host internals to compensate.

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
