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
| Luau | Vendored commit is recorded in [LUAU_REVISION.txt](../native/third_party/LUAU_REVISION.txt). It is not yet linked or runtime-qualified by Phase 0. |
| Hosting provider | The user's Shockbyte Linux server passed load/probe/unload/reload based on user-provided logs. Other servers/plans and provider policy changes remain unqualified. Its exact Carbon/OS build was not established by those excerpts. |

Dates, source/artifact hashes, runtime paths, CI links and the different depths of worker versus Shockbyte testing are owned by [Phase0-Validation.md](Phase0-Validation.md). PROVEN describes the tested native-load gate, not the safety of the future VM or all features in the design.

Carbon currently documents targeting .NET Framework 4.8 with `Carbon.targets`. Do not introduce modern-.NET-only APIs or netstandard2.1-only dependencies into the plugin without proving support on the actual runtime and documenting an accepted compatibility change. Keep project-owned native code and managed runtime aligned. [Carbon project setup](https://carbonmod.gg/devs/creating-your-project). Source packaging follows [Carbon's ZIP documentation](https://carbonmod.gg/devs/features/zip-script-packages).

## Version identities

Keep these identities conceptually separate and record those relevant to a result: Carbon build, Rust server build, pinned Luau commit/build options, CarbonLuau package version, project-owned native ABI compatibility, and CarbonLuau public scripting API compatibility.

The plugin's `0.0.1` version and the probe magic are not a scripting API version. A package bump does not automatically mean a script break; a host update should not redefine the facade. Before publishing a scripting API, document its version identity, supported behavior and change/migration policy. Before shipping an extended runtime ABI, define how incompatible managed/native pairs are detected before use. Mechanisms and SemVer/deprecation promises remain deferred in [D8](Invariants.md#decision-register).

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
- [CI workflow](../.github/workflows/phase0.yml): Windows/Ubuntu builds, native/managed tests and packaging. The package contains C# source only; the native library is deployed separately.
- [Provider acceptance procedure](Phase0.md#shockbyte-acceptance): load/probe and unload/reload logs on the actual authorized target. Do not run destructive fixtures on a live user server.

Harness assertions have limits: the Windows live script waits for a general `Unavailable:` message and prints status replies; the recorded run was reviewed for exact error categories and normal replies. Linux explicitly checks expected failure text and health. Neither runner proves long-term leak freedom. Use current-run logs/offsets when reusing a server; a prior run's startup line is not new evidence. These limitations do not invalidate the recorded Phase 0 run or justify a policy-only harness refactor.

## Phase 1 evidence contract

The feature boundary remains [design section 31](CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases), not a new API specification. The relevant native tests and failure behaviors are in design sections 27 and 29. This section owns their phase-specific qualification; executable Phase 1 fixtures do not exist yet and must accompany implementation.

Before marking the execution core complete, demonstrate compiler/VM create/destroy and source execution; controlled syntax/runtime errors; sandbox allowlist enforcement; configured allocation exhaustion and execution deadline failures; successful permitted execution after recoverable failure; deterministic cleanup after partial initialization/error/timeout; and failure diagnostics. Test the chosen protected-error mode and callback crossing rules, including low-memory error handling. Use sanitizers for the native lifecycle where practical, recording what actually ran.

Validate managed/native result and ownership transport, configuration bounds, and generation replacement. Candidate compile/load failure must preserve a working generation. Confirm status/reload and native lifetime inside actual Carbon on affected Windows/Linux environments. Revalidate the hosted target for claims specific to its new native dependencies or behavior; Phase 0's probe does not prove a Luau-linked runtime there.

Resolve the relevant [decision gates](Invariants.md#decision-register) before enabling their behavior and record the selected limits and remaining containment limits. Keep the accepted initial configuration candidates in the design until implementation qualifies them. Do not require Phase 2 gameplay APIs, events, modules or a full task scheduler to complete Phase 1; conversely, do not enable such features without their own validation.

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
