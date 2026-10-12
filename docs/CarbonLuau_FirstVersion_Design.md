# First-version design: current routing and retained phase plan

The initial v0.1 prototype proposal is preserved in
[the complete historical design](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design).
Its conceptual APIs/configuration candidates are not the current scripting contract.
[Invariants](Invariants.md), [Compatibility](Compatibility.md),
[Release](Release.md) and the [public API](api/README.md) own current behavior,
support, independent identities and availability.

The accepted initial phase plan remains below as sequencing/evidence context.
Completion retains each record's exact source, host and client/provider limits;
it does not assign another package version or authorize later features.

## 3. Non-goals for v0.1

The original non-goals are retained in the
[source section](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-3-non-goals-for-v01).
Do not infer Roblox DataModel/replication, arbitrary host/filesystem/network APIs
or a generic bridge from Luau familiarity. Deferred prototype examples are not
implemented declarations. Current capability/scope rules are I2/I3/I11 and the
decision register.

## 8. Native bridge design

The [original ABI proposal](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-8-native-bridge-design)
is historical. I5, the qualified [Phase 1 contract](Phase1.md), the current native
ABI checks and [Release identity](Release.md) own implementation compatibility.
No raw pointer or host object crosses the public script boundary.

## 27. Failure behavior

Use I8–I10 and [runtime details](invariants/Runtime.md) for bounded work,
failure scope, publication and recovery. The
[original failure list](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-27-failure-behavior)
does not substitute for later qualified D9/D10/D13 behavior.

## 29. Tests Codex must implement

Current change-based validation is in [Compatibility](Compatibility.md#validation-selection)
and [qualification policy](qualification/Policy.md). Preserve the
[initial test inventory](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-29-tests-codex-must-implement)
and [Phase 5 evidence](Phase5-Validation.md), with their explicit unqualified
authenticated-client and Shockbyte full-runtime dimensions.

## 31. Suggested implementation phases

Current accepted roadmap after D13 closure (2026-09-14):

| Phase | Scope | Status |
|---|---|---|
| 0 | Native boundary | [PROVEN in tested environments](Phase0-Validation.md) |
| 1 | Execution core | [PASS within recorded qualification](Phase1-Validation.md) |
| 2 | Scripts/modules/scheduler | [PASS within recorded qualification](Phase2-Validation.md) |
| 3 | Gameplay facade | [PASS within recorded qualification](Phase3-Validation.md) |
| 4 | Item conveniences | DEFERRED from v0.1 by D13; no runtime/API implementation |
| 5 | v0.1 hardening/qualification | COMPLETE within the recorded controlled-host envelope; authenticated real-client qualification deferred and unqualified |

READY is sequencing readiness, not completed hardening, a release verdict or
provider qualification. Prior phase evidence retains its original limits.

### Phase 0 — native-load proof

- Carbon plugin loads native probe library on Windows/Linux.
- Stop if target host blocks native libraries.

### Phase 1 — Luau execution core

- VM create/destroy;
- compile/run source;
- logging;
- sandbox;
- memory cap;
- timeout interrupt;
- `carbonluau.status` and `carbonluau.reload`.

No player APIs yet.

### Phase 2 — scripts, modules and scheduler (revised by user task, 2026-09-14)

- configured entrypoint and controlled module snapshots/cache;
- generation-owned bounded `task.defer`, `task.spawn`, `task.delay`;
- transactional script replacement and one-attempt recovery (approved invariant D9);
- no gameplay bindings. [Phase 2 contract](Phase2.md) owns exact qualified semantics.

The old Phase 2 gameplay/event bridge is deferred to a separately authorized API
phase; no `game:GetService`, Signals, Player proxies or player hooks are included.

### Phase 3 — commands and permissions

- dynamic script-defined commands;
- permission registration/checks;
- command cleanup on reload;
- command collision handling.

### Phase 4 — item conveniences: deferred from v0.1

D13 deferred Player:GiveItem and Items/Items:Exists together from v0.1. They are
not prerequisites for Phase 5 or remaining v0.1 implementation requirements. D18
later reopens only bounded read-only identity and observation for a separately
scoped future foundation. Revised D13 additionally accepts implementation-gated
GiveItem/TakeItem through Inventory-M and Player-1F work; it does not create a
new v0.1 Phase 4. The historical
[investigation and rejected adapter](history/inventory/Phase4.md#phase4) remain preserved. Existing task
primitives belong to the qualified Phase 2 substrate, not this deferral.

### Phase 5 — hardening and live-host validation

Executed separately after D13 closure; current evidence and remaining limits are
recorded in [Phase5-Validation.md](Phase5-Validation.md).

- 100 reload soak;
- connect/disconnect soak;
- memory pressure fixture;
- timeout fixture;
- Carbon profiler validation;
- actual managed-host deployment.

Stop v0.1 here.

---

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="carbonluau--first-version-architecture-and-codex-implementation-brief"></a>

- [CarbonLuau — First-Version Architecture and Codex Implementation Brief](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-carbonluau--first-version-architecture-and-codex-implementation-brief)

<a id="1-objective"></a>

- [1. Objective](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-1-objective)

<a id="2-research-conclusions-that-constrain-the-design"></a>

- [2. Research conclusions that constrain the design](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-2-research-conclusions-that-constrain-the-design)

<a id="21-carbon-is-suitable-for-a-modular-carbon-only-host"></a>

- [2.1 Carbon is suitable for a modular Carbon-only host](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-21-carbon-is-suitable-for-a-modular-carbon-only-host)

<a id="22-carbon-plugin-development-currently-targets-net-framework-48"></a>

- [2.2 Carbon plugin development currently targets .NET Framework 4.8](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-22-carbon-plugin-development-currently-targets-net-framework-48)

<a id="23-carbon-already-provides-the-host-side-primitives-needed-for-an-mvp"></a>

- [2.3 Carbon already provides the host-side primitives needed for an MVP](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-23-carbon-already-provides-the-host-side-primitives-needed-for-an-mvp)

<a id="24-luau-is-designed-to-be-embedded-and-provides-the-safety-primitives-we-need"></a>

- [2.4 Luau is designed to be embedded and provides the safety primitives we need](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-24-luau-is-designed-to-be-embedded-and-provides-the-safety-primitives-we-need)

<a id="4-top-level-architecture"></a>

- [4. Top-level architecture](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-4-top-level-architecture)

<a id="core-trust-rule"></a>

- [Core trust rule](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-core-trust-rule)

<a id="5-repository-layout"></a>

- [5. Repository layout](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-5-repository-layout)

<a id="6-deployment-layout"></a>

- [6. Deployment layout](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-6-deployment-layout)

<a id="7-phase-0--deployment-feasibility-gate"></a>

- [7. Phase 0 — deployment feasibility gate](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-7-phase-0--deployment-feasibility-gate)

<a id="phase-0-objective"></a>

- [Phase 0 objective](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-phase-0-objective)

<a id="required-targets"></a>

- [Required targets](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-required-targets)

<a id="native-loader-requirement"></a>

- [Native loader requirement](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-native-loader-requirement)

<a id="stop-condition"></a>

- [Stop condition](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-stop-condition)

<a id="required-abi-characteristics"></a>

- [Required ABI characteristics](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-required-abi-characteristics)

<a id="suggested-abi"></a>

- [Suggested ABI](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-suggested-abi)

<a id="9-luau-vm-setup"></a>

- [9. Luau VM setup](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-9-luau-vm-setup)

<a id="91-vm-composition"></a>

- [9.1 VM composition](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-91-vm-composition)

<a id="92-state-model"></a>

- [9.2 State model](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-92-state-model)

<a id="93-libraries"></a>

- [9.3 Libraries](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-93-libraries)

<a id="94-sandboxing"></a>

- [9.4 Sandboxing](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-94-sandboxing)

<a id="95-memory-cap"></a>

- [9.5 Memory cap](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-95-memory-cap)

<a id="96-execution-watchdog"></a>

- [9.6 Execution watchdog](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-96-execution-watchdog)

<a id="10-managed-runtime-lifecycle"></a>

- [10. Managed runtime lifecycle](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-10-managed-runtime-lifecycle)

<a id="loaded"></a>

- [`Loaded()`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-loaded)

<a id="onserverinitialized"></a>

- [`OnServerInitialized()`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-onserverinitialized)

<a id="unload"></a>

- [`Unload()`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-unload)

<a id="11-script-discovery-and-module-loading"></a>

- [11. Script discovery and module loading](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-11-script-discovery-and-module-loading)

<a id="root-directory"></a>

- [Root directory](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-root-directory)

<a id="root-scripts"></a>

- [Root scripts](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-root-scripts)

<a id="require"></a>

- [`require`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-require)

<a id="12-bootstrap-api-architecture"></a>

- [12. Bootstrap API architecture](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-12-bootstrap-api-architecture)

<a id="13-first-version-luau-api"></a>

- [13. First-version Luau API](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-13-first-version-luau-api)

<a id="131-game"></a>

- [13.1 `game`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-131-game)

<a id="14-signalevent-primitive"></a>

- [14. Signal/event primitive](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-14-signalevent-primitive)

<a id="15-players-service"></a>

- [15. Players service](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-15-players-service)

<a id="api"></a>

- [API](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-api)

<a id="player-proxy"></a>

- [Player proxy](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-player-proxy)

<a id="identity-rule"></a>

- [Identity rule](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-identity-rule)

<a id="lifetime-rule"></a>

- [Lifetime rule](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-lifetime-rule)

<a id="16-permissions-service"></a>

- [16. Permissions service](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-16-permissions-service)

<a id="17-commands-service"></a>

- [17. Commands service](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-17-commands-service)

<a id="api-1"></a>

- [API](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-api-1)

<a id="host-integration"></a>

- [Host integration](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-host-integration)

<a id="validation"></a>

- [Validation](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-validation)

<a id="18-items-service--deferred-from-v01"></a>

- [18. Items service — deferred from v0.1](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-18-items-service--deferred-from-v01)

<a id="19-server-service"></a>

- [19. Server service](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-19-server-service)

<a id="20-taskscheduler-api"></a>

- [20. Task/scheduler API](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-20-taskscheduler-api)

<a id="required-in-v01"></a>

- [Required in v0.1](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-required-in-v01)

<a id="deferred-to-v02-unless-trivial"></a>

- [Deferred to v0.2 unless trivial](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-deferred-to-v02-unless-trivial)

<a id="21-carbon-hook-bridge"></a>

- [21. Carbon hook bridge](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-21-carbon-hook-bridge)

<a id="dispatch-rule"></a>

- [Dispatch rule](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-dispatch-rule)

<a id="22-scheduler"></a>

- [22. Scheduler](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-22-scheduler)

<a id="requirements"></a>

- [Requirements](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-requirements)

<a id="drain-algorithm"></a>

- [Drain algorithm](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-drain-algorithm)

<a id="no-parallel-vm-access"></a>

- [No parallel VM access](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-no-parallel-vm-access)

<a id="23-reload-behavior"></a>

- [23. Reload behavior](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-23-reload-behavior)

<a id="atomic-reload"></a>

- [Atomic reload](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-atomic-reload)

<a id="24-diagnostics"></a>

- [24. Diagnostics](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-24-diagnostics)

<a id="25-configuration"></a>

- [25. Configuration](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-25-configuration)

<a id="26-security-invariants"></a>

- [26. Security invariants](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-26-security-invariants)

<a id="s1--no-raw-host-object-exposure"></a>

- [S1 — no raw host object exposure](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s1--no-raw-host-object-exposure)

<a id="s2--no-arbitrary-host-reflection"></a>

- [S2 — no arbitrary host reflection](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s2--no-arbitrary-host-reflection)

<a id="s3--no-arbitrary-filenetworkprocess-access"></a>

- [S3 — no arbitrary file/network/process access](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s3--no-arbitrary-filenetworkprocess-access)

<a id="s4--bounded-vm-memory"></a>

- [S4 — bounded VM memory](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s4--bounded-vm-memory)

<a id="s5--bounded-execution"></a>

- [S5 — bounded execution](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s5--bounded-execution)

<a id="s6--bounded-host-work"></a>

- [S6 — bounded host work](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s6--bounded-host-work)

<a id="s7--main-thread-game-access"></a>

- [S7 — main-thread game access](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s7--main-thread-game-access)

<a id="s8--stale-proxies-fail-local"></a>

- [S8 — stale proxies fail local](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s8--stale-proxies-fail-local)

<a id="s9--module-resolution-stays-inside-root"></a>

- [S9 — module resolution stays inside root](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s9--module-resolution-stays-inside-root)

<a id="s10--reload-cannot-partially-replace-runtime"></a>

- [S10 — reload cannot partially replace runtime](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-s10--reload-cannot-partially-replace-runtime)

<a id="28-performance-requirements"></a>

- [28. Performance requirements](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-28-performance-requirements)

<a id="native-unit-tests"></a>

- [Native unit tests](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-native-unit-tests)

<a id="managed-unitintegration-tests"></a>

- [Managed unit/integration tests](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-managed-unitintegration-tests)

<a id="live-carbon-acceptance-fixture"></a>

- [Live Carbon acceptance fixture](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-live-carbon-acceptance-fixture)

<a id="join-messageluau"></a>

- [`join-message.luau`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-join-messageluau)

<a id="commandluau"></a>

- [`command.luau`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-commandluau)

<a id="timeoutluau"></a>

- [`timeout.luau`](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-timeoutluau)

<a id="30-build-and-ci"></a>

- [30. Build and CI](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-30-build-and-ci)

<a id="native-ci"></a>

- [Native CI](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-native-ci)

<a id="managed-ci"></a>

- [Managed CI](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-managed-ci)

<a id="packaging"></a>

- [Packaging](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-packaging)

<a id="32-explicitly-deferred-follow-up-roadmap"></a>

- [32. Explicitly deferred follow-up roadmap](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-32-explicitly-deferred-follow-up-roadmap)

<a id="33-codex-implementation-rules"></a>

- [33. Codex implementation rules](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-33-codex-implementation-rules)

<a id="34-definition-of-done-for-v01"></a>

- [34. Definition of done for v0.1](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-34-definition-of-done-for-v01)

<a id="35-reference-material"></a>

- [35. Reference material](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-35-reference-material)

<a id="36-architectural-summary"></a>

- [36. Architectural summary](history/design/CarbonLuau_FirstVersion_Design.md#carbonluau_firstversion_design-36-architectural-summary)
