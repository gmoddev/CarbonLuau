# Player Interaction Foundation 1F-C — correction and combined closure

**Current verdict: PASS for the recorded server-authoritative experimental
scope.** The D18 cold-module defect is corrected; Windows/Linux focused,
combined, native/runtime, loader, package and Linux sanitizer/live gates passed.
Final-source CI and documentation deployment are reported after publication,
not inferred from worker runs. No tag or GitHub Release is created.

The initial stopped investigation is preserved below, explicitly describing
the unfixed `9b27ba5` baseline. The correction and resumed results follow it.

## Historical stopped investigation — before correction

Date: 2026-09-21. Production revision during this initial investigation:
`9b27ba5625c86eb32d9168effa2c6e72d8866576`, branch `main`.

**Historical verdict: BLOCKED / FAIL — D18 first-load module mutation restriction.**
No production fix, architecture change, release/tag, commit or push was made.
The local test/evidence changes are uncommitted. This is not Foundation 1 closure
or a new release-readiness claim. Existing qualification records remain historical
evidence for their tested paths; they do not establish the missing gate below.

### Reproduced stop condition

[D18](Invariants.md#d18--player-interaction-foundation-1) prohibits Teleport,
GiveItem and TakeItem from first-load modules, as well as provisional candidates
and dependency chains. A cold `require` inside an already committed operation
does not currently enforce that restriction.

The focused regression starts a real compiled/native Luau runtime with the
production managed facade and deterministic host adapters. Root candidate calls
are correctly rejected with zero mutations. After commit, it separately loads
three previously unloaded modules. Each module invokes one mutation and then
raises an ordinary error. The outer committed operation catches the module error.

Observed on both Windows x64 and Linux x64:

| Failed module | Cumulative Create calls | Cumulative Take calls | Cumulative Teleports | Physical scrap |
|---|---:|---:|---:|---:|
| Initial state | 0 | 0 | 0 | 5 |
| Give 2, then error | 1 | 0 | 0 | 7 |
| Take 1, then error | 1 | 1 | 0 | 6 |
| Teleport, then error | 1 | 1 | 1 | 6 |

Expected mutation counts are all zero. The test intentionally fails instead of
accepting these effects. It confirms gate/reference release and native VM teardown
before reporting failure. No external trusted-plugin mutation is involved; I12
cannot excuse this CarbonLuau publication-context error. Individual Give/Take
physical verification succeeds, but the calls should never reach COMMIT here.

### Source explanation at the failing revision

- [HostPrimitive](../native/src/facade/FacadeBridge.cpp) checks only
  `Runtime.Admission->Provisional` for mutation opcodes 28, 29 and 30.
- [ModuleLoader](../native/src/scripts/ModuleLoader.cpp) creates a nested
  `PublicationScope` for cold module initialization.
- [PublicationScope](../native/src/runtime/RuntimeInternal.hpp) changes
  `Runtime.Publication`, not the outer admission's provisional flag.
- The [managed facade](../src/CarbonLuau/Facade/FacadeSession.cs) sees an active
  owning session, so its committed-domain checks do not reject these calls.

The affected implementation is stopped under the user's explicit D18 contradiction
condition. The proposed follow-up is to enforce the existing mutation restriction
for first-load publication scopes, without changing ownership or resetting the
admitted deadline, then qualify nested/shared-module paths and resume closure.
No new semantics or compensating rollback are proposed.

### Tests and reproduction before correction

[PlayerInteractionFoundation1FCTests](../tests/runtime/PlayerInteractionFoundation1FCTests.cs)
is wired into the runtime suite. The focused entry points are:

```text
RuntimeTests.exe --player1fc-only
RuntimeTests.exe --player1fc-module-boundary <Carbon-data-directory>
```

On Linux run the executable through Mono. The data directory must contain the
matching native library and compiler under `CarbonLuau/native/<rid>/` using the
normal loader layout. Build `tests/runtime/RuntimeTests.csproj` in Release.
The module-boundary command currently exits 1 on both platforms, as required by
the failing contract assertion. It uses synthetic host adapters, not a Rust
server or authenticated client.

The combined model passed on Windows and Linux: all four recursive Give/Take
pairs, different-Player independence, shared-gate release, separate-definition
shop rejection/error with no payment rollback, non-reserving reads and 9,000
repeated success/rejection/programming/stale/post-COMMIT calls. Saturating
diagnostics and zero gate/tracked-reference baselines were checked. This does
not establish the unfinished addon/lifecycle/live closure matrix.

The Windows full managed/native suite passed earlier Player/model/GUI checks but
stopped at the new cold-module assertion. Later suite sections did not run to
completion. No final full-suite PASS is claimed.

### Original environment and artifacts

Production native/compiler binaries are the unchanged qualified 1F-B artifacts.
Native SHA-256:

- Windows: `08432a5cad3ef08c60f2a7b632a574fd4acfc5fd6f6c284d61856ffd5b7dd51e`.
- Linux: `b01494c3af2affbfffb1fa2f2552f45c1d83f25b7b79841d2c2319881eff29bd`.

Windows uses the local net48 runtime; focused log:
`dist/player1fc/windows-module-boundary.log`.
Linux uses user-authorized BigKVM, image `carbonluau-gui3e:latest`, one CPU/2 GiB,
task directory `/srv/codex/CarbonLuauPlayer1FC20260921`, exited container
`codex-carbonluau-1fc-module-boundary` (exit 1). Its Docker log holds the model
PASS and boundary failure. The native cache was mounted read-only. No ports were
published, no game server was started, and unrelated workloads were untouched.

### Closure still pending at the initial stop

No final-head CI/docs deployment, new live Carbon validation, release packaging,
checksums/provenance package, sanitizer rerun, complete API/example audit,
root/addon/provider/recovery stress closure or experimental readiness verdict is
claimed. These remain pending after correction and revalidation. The full
completion report cannot truthfully report PASS while the mandatory gate fails.

Package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider
`CarbonLuau.Addons` / `1.2`, schema `1` and Luau
`c6b830185af962c82003f86784e2fe036357c830` are unchanged. No gameplay API was added.
Teleport authenticated-client convergence, Windows live Carbon, historical
Foundation G platform limitations and Shockbyte/full-host qualification remain
as recorded in their owning evidence, not silently promoted by these tests.
DropRemainder, transactions/rollback, raw inventory/container access, health
mutation and other deferred Player/entity APIs remain unimplemented.

## Correction — 2026-09-21

Starting commit: `9b27ba5625c86eb32d9168effa2c6e72d8866576`. The correction,
regressions, examples and this evidence are committed together after applicable
worker gates. Git history identifies that commit; this file cannot embed its
own SHA. Final-source CI identifies the exact tested revision after push.

Only three production native files change: the internal declaration in
`RuntimeInternal.hpp`, `CanMutateHost` in `Publication.cpp`, and its use by
`HostPrimitive` in `FacadeBridge.cpp`. The single eligibility predicate requires
an existing nonprovisional admission **and no active publication scope**.
It governs opcodes 28/29/30 (Teleport/Take/Give), plus opcode 4 (SendMessage),
which already belongs to the same committed-only effect class. There is no
new managed rule, transport, field, C ABI or public API.

The current publication scope belongs to the VM execution chain, not the
lexical owner of a closure or captured facade. Existing RAII nesting/restoration
therefore carries the restriction through local/nested/public/transitive/shared
calls and restores it on success, ordinary errors and fatal unwind. Cached
exports called later in committed execution are not permanently provisional.
No admission creation, clock/deadline assignment, module loader, cache,
protected-call, scheduler or ownership implementation changes. A cold module
still inherits the original admitted deadline; the uncatchable timeout test
retires the VM through the existing recovery path.

### Focused result after correction

Windows x64 and Linux x64 both report the following cumulative counters for the
preserved three-module regression. Each error is caught by the outer operation:

| Cold module | Create | Take | Teleport | Physical scrap |
|---|---:|---:|---:|---:|
| Give | 0 | 0 | 0 | 5 |
| Take | 0 | 0 | 0 | 5 |
| Teleport | 0 | 0 | 0 | 5 |

The mutation itself now errors before entering the managed host operation,
instead of merely reaching the later explicit module error. I12 is not involved.
The live Linux supplement separately proves unchanged server position, physical
quantity, and Give/Take host-attempt counters through the production package.

## Resumed qualification

| Gate | Result and evidence |
|---|---|
| Cold execution | PASS: root local/nested modules; command, GUI Activated and deferred committed callbacks; public/transitive/shared addon modules and foreign captured Player facade. No cold mutation reaches host COMMIT. |
| Reads/cache/restoration | PASS: Position, Health, MaxHealth, Items.Exists, CountItem, HasItem and Vector3 during initialization. Same cached function identity subsequently grants/takes/teleports. Ordinary failed modules remain catchable, retry twice, publish no command or deferred mutation; nested restoration permits later committed work. |
| Shared mutation gate | PASS: all four recursive Give/Take pairings, other-Player independence, true/false/stale/programming/post-COMMIT release. Existing addon test holds the exact shared world gate while root/addon deferred Give/Take attempt entry, without recursive VM entry. |
| D11 | PASS: 20 disconnect/reconnect cycles retain a non-nil actual old Player proxy and check stale errors for reads and all three D18 mutations; no retarget or host effect. |
| Root/addon lifecycle | PASS: failed candidate preserves active authority and emits zero mutation; successful replacement drops stale queued GUI work. Required dependency reconstruction gets fresh authority; optional binding remains stale and captured old exports cannot mutate the replacement. Provider unload/re-registration returns gate/reference baselines with no replay. |
| Recovery | PASS: 10 explicit reload/rearmed fatal cold-module timeouts after representative reads and Give/Take/Teleport; effects already performed stay performed, queued work is discarded, reconstructed authority works. No replay or exactly-once claim. |
| Result-contract audit | PASS: both inventory operations return false only from PREPARE; true follows operation-specific physical VERIFY. COMMIT failures are errors; `finally` releases gates. Programming/stale errors may also occur before COMMIT. Source audit plus existing 1F-A/B fault cases; no production inventory adapter changes. |
| Physical scanner | PASS: existing Player-1C and 1F-A/B suites cover authoritative direct reciprocal main/belt/wear membership, exact definition, no recursion, 128 bound and no partial overflow answer. Count/Has do not reserve subsequent mutations. |
| I12 | PASS within its recorded boundary: explicit stack-limit, amount and slot mutation fixtures remain separately labeled external interference, not supported-host success. Take matching-item interference is indeterminate; unrelated-item mutation still permits verified removal. Ordinary accept/reject/exception behavior remains CarbonLuau responsibility. |
| Returned resources | PASS: rerun 1F-B creation/null/transfer/accepted-then-throw/VERIFY/stale/UID/cleanup-failure cases. Accepted/consumed/qualified-cleanup outcomes or explicit indeterminate errors; no retained references, double cleanup or rollback. Unreturned callback allocations are not claimed cleaned. |
| Workflows/examples | PASS: separate-definition shop payment followed by grant false/error does not refund; later explicit grant works. Command permission denial never enters Luau, authorized command/GUI/deferred rewards work. Six bundled Player examples execute through the real compiler/VM, using deterministic adapters and synthetic GUI ingress. |
| Stress | PASS: 9,000 combined model calls plus rerun 4,000 1F-B stress operations and 100 root/addon contention rounds (400 rejected calls); bounded/saturating counters, zero busy gates/tracked references. Native 1,000 VM cycles and 1,000 script generation cycles; managed 100 atomic replacements/200 failed candidates; existing 0/1/10/50/100-addon and GUI scale/fairness suites. |

### Platform and live scope

- **Windows:** MSVC 19.40 x64 Release, full net48/native suite on `dockerbox`
  (`HostPC`), task root `C:\Sandbox\Codex\Workspaces\CarbonLuau1FC`.
  All five native test executables passed, including allocation faults and
  compiler 256 MiB containment/recovery. Final managed suite includes new
  examples. Workstation loader 100 cycles, native/compiler import audits and
  deterministic Windows packaging also passed. Native code used the existing
  one-job incremental cache; worker tests were serial. Crash fixtures inherited
  process-local Windows error-dialog suppression, restored on completion.
- **Linux:** BigKVM, Ubuntu 24, image `carbonluau-gui3e:latest`, existing
  `/srv/codex/CarbonLuauPlayer1FB20260921` cache. Two build jobs/2 CPUs/3 GiB;
  full Mono runtime, loader 100 cycles, Release CTest **5/5** and instrumented
  Luau/bridge ASan + UBSan + leak/allocation-fault CTest **5/5** passed.
  `codex-carbonluau-1fc-regression`, `codex-carbonluau-1fc-final` and final
  contention/documentation rerun `codex-carbonluau-1fc-closure` exited 0.
  Final runtime includes Foundations A–G affected tests, Player-1A–1D/1F-A/B,
  inventory models, GUI, modules/parser, providers, scheduler and examples.
- **Live Linux:** Rust app `258550`, build `25353106`; Carbon `2.0.259`, protocol
  `2026.09.03.0`, revision `21063e8490adf412101bcc7d1cfe9d6280f61e80`.
  Exact assembly evidence and unchanged adapter calls are routed by
  [1F-B validation](PlayerInteractionFoundation1FB-Validation.md). The amended
  `CarbonLuau.Player1FBFixtures.cs` and `Test-Player1FBLinux.py` passed twice
  through actual Carbon/Rust: Give/Take, merge/empty/full, ordinary callbacks,
  300 repeated operations per cycle, explicit I12 fixtures, reconnect,
  provisional/failed reload, and the new zero-COMMIT cold-module supplement.
  `/proc` confirmed native unmapping after each unload. Container
  `codex-carbonluau-1fc-live` exited 0; log
  `evidence/live-production-20260921-205306/server.log`. Two CPUs/6 GiB,
  no published ports, synthetic fixture Player, **no authenticated client**.

Exact Windows/Linux host-assembly static evidence was also rerun read-only:
Inventory-M2 **92 checks**, callback/drop exposure **51 checks**. The exposure
checker confirms the historical hazard, not G1 safety; supported-host safety
is established only within the separately recorded adapter/live scope.

Task-owned test containers/servers exited; unrelated services were untouched.
Incremental caches and diagnostic logs remain. The live rerun uses final
production/runtime/fixture source; subsequent changes only add examples,
test coverage and documentation. Unaffected prior live host evidence is retained,
not relabeled as newly executed.

### Release-facing audit

API, architecture/GiveItem-safety, relative links, package contents and
deterministic platform-bundle checks passed. Production `.cszip` excludes live
fixtures. Public docs/navigation/release notes route to this correction and the
new Player examples; the bundle content audit requires all six Player examples.
Clean-commit bundles/checksums/provenance are generated after commit, and hosted
CI independently rebuilds both platforms at that revision. No tag/release is
created by this qualification task.

Corrected tested native SHA-256:

| Platform | SHA-256 |
|---|---|
| Windows | `89f54153df9021e3add9603133cb0f2d12d8cb80e2bdd59eba1143c0099f60a6` |
| Linux | `34e640d594b49c3806740a2ec441523ade9e2097061c9deb5d0f5d11938a468d` |

Compiler source/binaries, pinned Luau and all six version identities above are
unchanged. No new gameplay API, raw host object, drop behavior, transaction or
health/entity mutation was introduced. Canonical I12/D11/D13/D18 were not weakened.

## Readiness and remaining limitations

The implemented Player Interaction Foundation 1 surface is qualified for the
existing unreleased **0.4.0-experimental server-authoritative candidate**, within
the target-build and I12 boundary. This closes the combined lifecycle/public
qualification planned as Player-1E together with inventory Player-1F-C; it does
not authorize deferred gameplay work.

Windows live Carbon was not rerun or newly qualified. The new Windows worker
native/managed/containment PASS does not erase Foundation G's historical
live/local deferral. Teleport authenticated-client convergence, GUI receipt/UI,
exhaustive item/mod combinations and Shockbyte full-runtime qualification remain
unqualified. Existing Phase 0 Shockbyte evidence is unchanged. No operating-system
isolation from hostile installed C#, rollback, automatic retry, hard preemption
of host callbacks or exactly-once delivery is promised.
