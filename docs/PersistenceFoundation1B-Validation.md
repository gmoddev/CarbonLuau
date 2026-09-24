# Persistence Foundation 1B — validation

Date: 2026-09-23. Scope: public persistence facade and asynchronous completion.

**Interim verdict: PARTIAL — implementation and all recorded local gates pass;
hosted final-source CI remains pending.**
No release, tag, overall Persistence Foundation 1 closure or 1C work is implied.

## Provenance and identity

| Item | Value |
|---|---|
| Starting `main` / fetched `origin/main` | `4f665a3c4d801c951227a2557ad0019dfbdb6856` |
| Implementation / evidence commit | Not committed yet |
| Tested source | That baseline plus this task's uncommitted delta; final source receipt pending |
| Package / tag identity | Development `0.4.0` / `v0.4.0`; no tag or release created |
| Scripting API | `0.5.0-experimental`, explicit user assignment under D12 |
| Intended future package | `0.5.0`, not bumped by this work |
| Native ABI | `1.5`, new bounded `cl_domain_storage_completion` export |
| Provider / addon schema | `1.2` / `1`, unchanged |
| Luau pin | `c6b830185af962c82003f86784e2fe036357c830`, unchanged |

Native builds and tests ran on the authorized `dockerbox` worker, with two build
jobs and bounded containers. Source was hash-synchronized into
`C:\Sandbox\Codex\Workspaces\CarbonLuauPersistence1A-20260923` without copying
Git metadata. Its pre-existing Git HEAD is **not** the tested source identity.
Incremental builds are under `C:\Sandbox\Codex\Builds\CarbonLuauPersistence1A-20260923`;
logs under `C:\Sandbox\Codex\Logs\CarbonLuauPersistence1B-20260923`.
Ignored local copies are in `build/persistence1b/`; no database or secret is published.

Windows: Windows 11 Pro build 26200, MSVC 19.44, .NET SDK 9.0.317.
Linux: Ubuntu 24.04.5 container, GCC 13.3.0, CMake 3.28.3, glibc 2.39,
Mono 6.8.0.105, .NET SDK 9.0.318. Image
`carbonluau-foundation-f:latest` was
`sha256:29ce061210cde5c576e730154a201aca9787fe340b49a5317b448fcdf34e765f`.
Persistence tests used the dedicated ext4 volume, not Docker's unsupported overlay
filesystem. Tooling used its separate cached .NET 10.0.401 environment.

Reproduction entry points (resolve paths to the matching built platform):

```text
cmake --build <build> --config Release --parallel 2
ctest --test-dir <build> -C Release --output-on-failure
RuntimeTests.exe <native> <WrongAbi> <LegacyProbe> <source> <compiler>
RuntimeTests.exe --persistence1b <native> <storage> <owned-fixture-directory>
PersistenceAdapterTests.exe <native> <storage> <owned-fixture-directory>
ManagedTests.exe <storage> <StorageFixtureLostAck> <StorageFixtureHang>
LoaderTests.exe <native> <WrongProbe> <MissingSymbol>
RuntimeTests.exe --release-install <bundle>
tools/Test-PersistencePackaging.ps1 -Bundle <bundle> -ManagedTests <tests> -WorkDirectory <owned-directory>
```

Use Mono for the .NET Framework executables on Linux; Windows private-worker
regression additionally selects `--utf8-input`. Linux parent-death tests need a
reaping init and qualified ext4 fixture/TMPDIR. Sanitizers use the existing
`CARBONLUAU_SANITIZE=ON`, `ASAN_OPTIONS=detect_leaks=1:halt_on_error=1` and
`UBSAN_OPTIONS=halt_on_error=1:print_stacktrace=1` configuration. The committed
workflows route these tests and the authoritative tooling generation/drift checks.

## Evidence matrix

| Gate | Windows x64 | glibc Linux x64 | Evidence scope |
|---|---|---|---|
| Native production suite | 15/15 PASS | 16/16 PASS | Linux includes ELF unload guard |
| Real VM/compiler public persistence and full managed runtime | PASS | PASS | Actual production bindings; not managed mocks alone |
| Private 1A worker regression, lost ACK and hung worker | PASS | PASS | Real worker plus explicitly identified fault executables |
| Adapter intake/drain fault teardown | PASS | PASS | Actual Main/Persistence partials; Carbon glue stubs inject faults |
| 100 native loader cycles | PASS | PASS | Actual unload, not merely successful `dlclose` return |
| Native ASan/UBSan/leak suite | Not applicable | 16/16 PASS | Affected allocation/completion/retirement paths |
| Exact-64-KiB callback/input/decoded graph collection | PASS | Release + ASan PASS | Weak references checked before VM destruction |
| Deterministic bundles, private worker imports/clean extraction | PASS | PASS | No DB/fixture/debug storage content |
| Clean bundle examples | 54 compiled; executable fixtures PASS | Same | Compile success alone is not storage durability proof |
| Metadata/generation/schema/API drift | PASS | PASS | Final Experimental promotion regenerated/rechecked |
| Host/LSP/preview/supervisor | PASS | PASS | Final metadata-pack rebuild and payload checks passed |
| Actual Carbon representative reload/unload | PASS | Not measured | Windows host described below |
| Actual server restart with root/two-addon isolation | PASS | Not measured | Actual second Rust process and fresh read-only acquisitions |
| Hosted final-source CI | Not run | Not run | Requires published source revision |

The full runtime sweep includes Foundations A–G/addons/providers, publication,
module loader, scheduler, recovery, GUI through 3E, Player Interaction through
1F-C, parser/package checks and existing reload/cancellation stress. Their earlier
real-client or host-version limitations remain unchanged; this does not newly
qualify those gameplay surfaces. No macOS server-runtime claim is made.

After the final Experimental annotation promotion, native production was rebuilt
incrementally: Windows 15/15 PASS in 37.91 seconds; Linux 16/16 PASS in 38.11
seconds, including the new exact-envelope collection test. The prior complete
sanitizer run took 157.47 seconds; the added collection case then passed separately
under the same ASan/UBSan/leak configuration. Annotation comments are not a runtime
behavior change, but embedded bootstrap bytes do change the binary hashes.

| Final production input | SHA-256 |
|---|---|
| Windows native | `e0f072aa477d5343a60ddf6123db7809ca614789d58b98aae5e3f997da10def2` |
| Windows compiler | `cb52526fa8110f10fff22f6e0373fc376b27afdc261d568eba02416066b720dc` |
| Windows storage | `b5dcf8d049296a8346b94e4b0c06a88bd1005146e92b5b9992daf93dfdeb3707` |
| Linux native | `743b339fd2ad001bb72df07baeaa06ef0c5d9948b776a0bcbfce2208991122e4` |
| Linux compiler | `884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92` |
| Linux storage | `5f5fe829921626148151dc4ebed2e3879733f3060a668a44f8f6a3cf1a68cc1b` |

Final locally constructed development bundles (uncommitted-source provenance,
not releases) passed deterministic regeneration, 54-example clean installation
and extracted-worker checks on their respective platforms:

- Windows ZIP: `23858b02689422d1bc85e9f50dc7ed47b8ef276d01a69a31ca27d45b28f32258`.
- Linux ZIP: `fe9c3d57ce5c5d1d0dc140912970f47222a69e4df792a8bc3103ed2bf7be7ff7`.

The final bundled CSZIP is `775a4a18d57af97e4177e8f3c17def56327cecdaccde0aacb8a043f492621591`.
Every entry's content matches the earlier live CSZIP
`38ecf047dbb3746a16cc29bd180ad6885938229bed0c78d9a9120e956dc7c91a`;
the archive container bytes differ. This content comparison establishes managed
source equivalence, not equality of those two ZIP hashes.

### Public API tests and authority

`tests/runtime/PersistencePublicTests.cs`, `PersistenceBehaviorTests.cs`,
`PersistenceAdmissionTests.cs`, `PersistencePerformanceTests.cs` and their harness
run actual Luau through the pinned compiler/VM. They cover exact signatures and
callback argument validation; case-sensitive names/keys; malformed UTF-8 and
bounds; finite binary64 fidelity; strings/arrays/maps; nil, userdata, metatables,
functions, threads, holes/mixed keys, cycles and oversized graph rejection.
Raw conversion invokes no user metamethod. Empty tables follow D21's map rule.

Real worker round trips prove submitted-table snapshots, fresh Get tables,
missing Get and Remove, overwrite, durable root/addon namespaces, version/reload
reacquisition, same-name isolation, nested Get→Set/Set→Get/Remove→Set and repeated
read/write/delete consistency. Deterministic adapter completions separately
exercise every stable error mapping, malformed/stale completion admission,
ordering/fairness, exact race positions, callback error/timeout and quota-result
translation. An injected result is not represented as an actual disk fault.
Private 1A native quota tests are rerun for real transactional 16/256-MiB bounds.

Candidate/cold/nested/public/dependency module and foreign-facade laundering tests
verify zero backend requests when forbidden. Disk-free acquisition remains allowed;
a cached exported closure can submit later from a valid committed operation.
Namespace authority is the admitted ResourceOwner, not a package string supplied
by script, nor merely the owner of a called foreign closure. Root, addon and VM
replacement invalidate old callback/facade authority without deleting durable data.

Completion before retirement is delivered once. Retirement before backend result
or after result but before admission discards delivery. Validation and entry share
the serialized owner-thread admission; there is no unlocked validate-then-enter
window. VM recovery never delivers an old callback into the reconstructed VM.
Durable commit remains independent of callback success, error, timeout or discard.
Loss of a definite write acknowledgement yields `Indeterminate`; neither worker
recovery nor callback failure replays the request. Tests distinguish this from a
definite controlled failure and from `(nil, nil)` Get absence.

A final public-path race supplement also passes on both platforms:
`SetAsync` is dispatched without a definite result, an actual Luau timeout
retires/reconstructs the VM, and a fresh generation submits a successor. Three
owner turns cannot dispatch that successor ahead of the old in-flight write.
A synthetic terminal reply then settles the old operation, discards its callback
and releases its reservation; only the explicit successor dispatches and calls
back once. The real-worker fatal-durable case separately proves committed readback.
This distinction avoids presenting synthetic completion injection as disk evidence.

Useful exact fixture references: `RoundTrips` tests the real-worker mixed
Set/Get/Remove/Get/Remove FIFO sequence; `CrossDomain` tests provider-retired
completion and stable-package reassignment; `CallbackFailures` tests a fatal
first callback discarding another ready completion; `DeterministicAdmission`
tests each retirement/intake position, duplicate/forged responses and stable
error translation; `GlobalCapacity` tests 16-namespace rotation with 128 retained
results. `WorkerFailure(true/false)` uses the named lost-ack/hang executables,
not an unmodified production worker. `AttachedStorageRequiresAbi15` changes only
the reported managed version property and proves controlled rejection even when
the completion delegate was previously bound.

Focused security attempts cover namespace-field/extra-scope spoofing, path-like
names, malformed UTF-8, bounded graph conversion, unsupported host userdata,
metamethod execution, foreign closures/facades, stale response identities and
queue flooding. Public errors remain controlled strings with no SQLite numeric
code, exception class, SQL or physical path. This is a public-boundary review and
test matrix, not a repository-wide security scan.

Existing bounded status diagnostics retain aggregate submitted/completed,
queue/rate rejected, expired, quota/backend failure and discarded counts. The
new layer keeps no unbounded request history and exposes no key, value, callback
identity, SQL or storage path. Backend-unavailable acquisition is still disk-free;
an operation rejects before acceptance, rather than fabricating missing data or
hot-looping worker startup. Corruption/error translation does not add repair.

Queue saturation covers eight pending requests per namespace and 128 globally,
including completed-but-undelivered callbacks. Native allocation-fault tests
observed one injected failure and one successful retry of the *test operation*,
not automatic storage replay (last allocation point 1, search bound 128).
The fixed completion/route reservation is released on delivery, rejection,
expiry/discard or domain/VM shutdown. Large source graphs are not worker inputs.

### Resource and stress observations

The public real-worker stress performed 2,000 reads across ten namespaces:
Windows 31.220 seconds; Linux 14.564 seconds. Durable mutation, queue pressure,
replacement/recovery, failed callbacks and lost-ack/hang fixtures accompany this
bounded test. This is not 1C's extended soak.
The final test-only late-completion supplement reran the entire public suite:
Windows 2,000 reads in 31.295 seconds; Linux in 14.618 seconds, with all 18
performance observations, real store quota and lost-ack/hung-worker cases passing.

Native collection tests run 32 Set/Get cycles with exact 65,536-byte envelopes.
Weak references to original inputs, callback captures/closures and reconstructed
Get tables become empty after completion and owner-thread GC, before VM teardown.
After eight warm cycles the observed Windows allocator plateau was exactly
794,504 bytes (test tolerance +64 KiB); routes and reservations return to zero.
This fixture uses synthetic host transport with the real VM/codec; durable-worker
round trips and teardown are separate tests.

Private-worker 32-cycle resource checks observed Windows +856 managed bytes,
+5 handles, +1 thread; Linux +1,104 bytes, +0 descriptors, +1 thread, within the
existing 2-MiB/16-handle/8-thread tolerances. Residual process RSS alone was not
treated as a leak or as proof of complete collection.
Final extracted-package reruns additionally observed Windows +96 bytes/zero
handles/zero threads and Linux zero bytes/descriptors/threads after warmup.
These are bounded observations within the existing tolerances, not a global
zero-allocation or long-run leak guarantee.

### Performance observations, not guarantees

Three samples per operation/value size were taken through the public API.
Near-limit performance values used 65,269-byte envelopes; the collection fixture
separately used the exact envelope ceiling. Times below are milliseconds.

| Platform / operation | Submission range | Dispatch → native handoff observation | Callback admission/decode/execution |
|---|---:|---:|---:|
| Windows Get small | .204–.251 | 14.975–15.298 | .064–.077 |
| Windows Set small | .235–.271 | 13.541–15.161 | .041–.055 |
| Windows Remove small | .264–.282 | 14.943–15.463 | .047–.061 |
| Windows Get near-limit | .253–.298 | 14.598–15.074 | .562–.631 |
| Windows Set near-limit | .812–.965 | 14.353–15.326 | .034–.070 |
| Windows Remove near-limit | .301–.364 | 14.897–16.079 | .044–.060 |
| Linux Get small | .336–.359 | 5.123–5.138 | .056–.069 |
| Linux Set small | .396–.425 | 15.315–25.470 | .065–.084 |
| Linux Remove small | .336–.408 | 15.289–15.338 | .055–.078 |
| Linux Get near-limit | .370–.491 | 5.132–5.171 | .306–.367 |
| Linux Set near-limit | .836–2.142 | 15.309–15.344 | .054–.076 |
| Linux Remove near-limit | .354–.389 | 5.128–10.214 | .072–.075 |

Submission observations include Host.Execute/compiler IPC and script construction;
worker observations include polling/OS timer resolution, not isolated SQLite
execution. Raw logs separate observed reply/ingress and queued callback delay.
Unit stress uses 100-ms callback/20-ms drain budgets to isolate storage behavior;
actual Carbon uses the existing 3-ms/5-ms defaults. Durability was not weakened.

## Negative evidence and corrections

1. GNU Linux native unload initially failed despite zero live VM/worker state.
   A fresh `dlopen`/`dlclose` probe with no Mono or VM reproduced retained mapping.
   `LD_DEBUG` reported NODELETE from the dynamic GNU-unique make_shared tag.
   The affected SO hash was
   `fe957765b404d3f93f9027568ddb1bec6ddbccca58f0a8505804e5867f6c81cc`.
   `-fno-gnu-unique` removes that cause; the new ELF guard rejects the old SO and
   passes the fixed production/preview libraries. Full Linux runtime, adapter and
   100-cycle loader tests then pass without weakening unload assertions.
2. Private managed tests initially globbed the new host-only persistence partials.
   Their project now explicitly excludes those partials while retaining the four
   private transport types. Old queue fixtures reused one callback route for
   multiple requests; they now use unique routes and explicitly test duplicate
   rejection, without changing production admission rules.
3. A standalone Linux parent-death regression failed its process-exit observation
   when Mono was container PID 1. The identical suite passed under Docker `--init`.
   The failed run is preserved; orphan reaping/topology is the suspected cause,
   not a proven storage defect. No test assertion was removed.
4. The first two Windows live harness attempts are preserved. One reload attempt
   queried status immediately after `c.load`, before the new runtime-ready marker,
   and timed out waiting for RCON. The worker subsequently reported Ready. Waiting
   for the fresh readiness marker fixed harness sequencing; unchanged production
   artifacts passed the full representative live run.
5. The restart supplement initially stopped after acknowledged `quit`, addon
   retirement, native unload and completed world save. It did **not** establish
   a numeric nonzero Rust exit: PowerShell 5.1 lost the exit-code observation,
   and `$null -ne 0` was incorrectly interpreted as failure. Controlled processes
   with the same launch/redirect pattern reproduced unavailable codes for both
   exit 0 and exit 7; retaining the process handle captured their correct numeric
   values. The capture-only harness correction retains rejection of unknown or
   nonzero status and records the observation before asserting it. Those failed
   attempts remain failed; their pre-restart Root/A/B counters were Get/Set/Remove
   8/4/2, and no post-restart claim follows from them.
6. The capture-corrected run `persistence1b-20260923-190443-00c14b9f`
   recorded an available numeric Rust exit code **-1** at `CleanServerStop`.
   The first process started at `2026-09-24T02:04:43.5712207Z`; exit was observed
   at `02:05:56.8570876Z`, with zero owned helpers. Root/A/B isolation and the
   8/4/2 operation counters passed before acknowledged quit and native unload.
   The harness did not start a second process. `first-server-exit.json` preserves
   this distinct result; it is not the earlier unavailable-code defect. Subsequent
   read-only host inspection establishes that -1 is intentional for this command,
   as described below. The failed run itself still proves no actual restart.

### Qualified Rust quit semantics

The existing installed Mono.Cecil reader inspected metadata/IL without invoking
target methods or downloading tools. On this exact Windows installation:

| Assembly | SHA-256 | Relevant inspected behavior |
|---|---|---|
| Assembly-CSharp.dll | `543c0eb569ec6b3889de9aa23f61cd5af7ae5a8f0d07c2d6f1e9fb94b3d49d09` | `ConVar.Global.quit` calls `ServerMgr.Shutdown` (save/writecfg), stops networking with reason quit, then calls current process `Kill` at IL005c/0061; no catch handler in quit |
| System.dll | `6e03af58cd23577b5588c90a951d8b4f9e3dfe34c80e3e4171d83d29ba895aea` | `Process.Kill` supplies -1 at IL000b to `NativeMethods.TerminateProcess` at IL000c |

Thus numeric -1 matches the host's deliberate console-quit implementation, not
evidence by itself of a persistence fault. A universal zero-exit criterion was
an unsupported harness assumption, not a D21 requirement. The corrected harness
recognizes -1 only behind exact inspected-host hashes and still requires the
acknowledged quit, shutdown/save/native-unload evidence and zero owned helpers
before restarting. No runtime contract, Windows security setting or backend
durability assumption changed. This is a normal host-command process restart,
not proof of arbitrary crash, OS failure or physical power-loss survival.

## Actual Carbon receipt

Windows run `persistence1b-20260923-183232-0b671ad5` passed root Set/Get/Remove,
root reload, addon seed/replacement, full CarbonLuau unload/load, stale registration
token rejection, reacquisition of persisted root/addon values and worker/native
teardown. It ran Rust build **25230300**, protocol **2633.288.1**, changeset
**163794**, Unity **6000.3.15x1-11**, Carbon **2.0.259.0 [2026.09.03.0] 21063e8**.
No authenticated Rust client was required or used. This host version is not
silently substituted for inventory-build qualification.

Artifacts are under `C:\Sandbox\Codex\Artifacts\CarbonLuauPersistence1B-20260923`
in that run's directory: environment/input hashes, initial/reloaded/final status,
redacted RCON stages, server/console logs and release metadata. The isolated
fixture restored pre-existing configuration/source files byte-for-byte and left
no owned server/compiler/storage helper running. The earlier one-addon run is
not evidence of two-addon isolation across an actual server process restart.

Linux live Carbon, Shockbyte, authenticated-client receipt, physical power loss,
OS crash and 1C's extended lifecycle/soak are **not measured here**. Existing
qualified 1A crash/profile evidence remains scoped as originally recorded.

### Actual server-restart supplement — PASS

`persistence1b-20260923-191320-3167ec0f` passed in 112.9961167 seconds using
the corrected, exact-host-qualified quit observation. First Rust PID 9716 exited;
second Rust PID 20736 started on the same isolated server identity and persistence
directory. Both final captured exits were the inspected command's intentional -1,
with acknowledged quit, save/config/native-unload markers and zero helpers.

Root and addons `qualification.persistence1b-a` / `qualification.persistence1b-b`
used the same store/key with distinct retained values. Before restart the actual
completed counters were Get 8 / Set 4 / Remove 2. The second process began idle
at 0/0/0. Fresh facades then read the three prior values with counters **Get 3 /
Set 0 / Remove 0**, sent 3, and zero pending/errors/rejections. No seeding write
or old in-memory VM value supplied the post-restart result. The database hash
after first stop and before second launch was
`e5eb39ef1ec364b8c88103a0600406d7dd9b2827e19b1f79431de8428fecf563`.

Inputs: final Windows native hash above; unchanged live CSZIP
`38ecf047dbb3746a16cc29bd180ad6885938229bed0c78d9a9120e956dc7c91a`;
final harness `b94876b9b27d42b500c96916b64027a581e63fdc0d0951aefd4ea680a3d2086b`.
The source fixture remains outside production packages. Final native-unmapped
observation was true; independent cleanup found zero Rust/compiler/storage
processes, all five protected-file hashes restored, no leftover backup files and
no RCON secret. Earlier failed attempts remain negative harness evidence.
This is the requested bounded 1B restart check, not 1C's extended crash/soak matrix.

## Tooling, packaging and future work

The production annotations, bootstrap, canonical catalog, generated `.d.luau`,
documentation and six runnable examples agree. Recursive persisted-value types
are checked by the real pinned language server; unsupported editor persistence
returns the existing controlled preview diagnostic, with no fake database.
Both final Experimental packs ran eight host and six LSP tests, 17 deterministic
preview goldens and 11 hostile-worker/supervisor modes, including cancellation,
EOF, process reaping and recovery. Core net48/net10 and compiled metadata/drift
tests passed. Every manifest payload hash and all four generated artifact bytes
matched; seven persistence declarations are Experimental/Since0.5 with no WIP.
Local-development pack IDs (semantic baseline 4f665a3, not shipping attestations):

- Windows: `a8bdb29aec14006dfb7f71b5f6fc5ff684144c2accfdedc5a0fb94406486041b`.
- Linux: `739450bfdb0a62f3a660e73cd0a4bd6415d78a33c03e097953ee39d507b7648f`.

The future Query compatibility audit is in [the implementation record](PersistenceFoundation1B.md#internal-future-query-compatibility-note).
Schema/version checks, per-store identity and atomic Set/Remove transaction
boundaries stay private and extensible; there is no public opaque-forever promise,
SQL/layout/row identifier, reserved Query syntax or speculative schema change.

Next permitted task after 1B closure: separately authorize Persistence-1C's
canonical combined crash/lifecycle/quota/scale/platform/public-readiness work.
No Query, UpdateAsync, schema/index API or Persistence-1C work was implemented.

## Requested completion checklist

This checklist reports the task's 52 requested fields. Pending entries are not
PASS and must be resolved before the final closure verdict.

| # | Field | Result |
|---|---|---|
| 1 | Verdict | PARTIAL pending hosted CI; all recorded local gates PASS |
| 2 | Starting commit | `4f665a3c4d801c951227a2557ad0019dfbdb6856` |
| 3 | Implementation commit | Pending |
| 4 | Evidence/docs commit | Pending |
| 5 | Final tested commit | Uncommitted delta; publication/CI pending |
| 6 | DataStoreService | `game:GetService("DataStoreService")`; `GetDataStore(Name: string) -> DataStore` |
| 7 | DataStore | Only `GetAsync(Key, Callback)`, `SetAsync(Key, Value, Callback)`, `RemoveAsync(Key, Callback)` |
| 8 | Callbacks | Get `(PersistedValue?, string?) -> ()`; Set/Remove `(boolean?, string?) -> ()`; mandatory |
| 9 | Submission | Nonyielding, no return values; validate/snapshot/admit synchronously; rejection accepts no request |
| 10 | Completion | Later fresh owner-thread admission, exact surviving authority, at most once; no coroutine resumption |
| 11 | Get | Existing fresh value/nil error; absence nil/nil; failure nil/error |
| 12 | Set | Raw bounded snapshot before acceptance; later source mutation has no effect; durable success true/nil |
| 13 | Remove | Existing true/nil; missing false/nil; no tombstone or hidden retry |
| 14 | Conversion | Qualified finite binary64/UTF-8/array/map model; unsupported values, metatables and cycles rejected |
| 15 | Arrays/maps | Dense sequence or string-keyed map; empty table is map; mixed/sparse rejected; no map-order promise |
| 16 | Namespace authority | Host-bound durable root/stable addon ID; current admitted owner must match facade owner |
| 17 | Root namespace | Worker/VM reconstruction, actual CarbonLuau/root reload and actual server restart PASS |
| 18 | Addon namespace | Version/replacement/provider reassignment, actual host reload and server restart PASS |
| 19 | Same-name isolation | Two-addon real-worker VM and actual root/A/B server restart PASS |
| 20 | Facade lifetime | Exact domain/VM/publication validity; stale/foreign controlled rejection, never retargeted |
| 21 | Provisional/cold modules | Acquisition only; async submission rejects with zero requests, including nested/caught cases |
| 22 | Cached exports | Later valid committed admission may submit; closure creation does not permanently prohibit it |
| 23 | Cross-domain laundering | Foreign service/store/shared closure rejected; owner-scheduled work needs fresh owner admission |
| 24 | Completion authority | Exact host/VM/domain/route revalidated and consumed through serialized admission |
| 25 | Replacement before delivery | Old callback discarded; backend result preserved; fresh namespace reacquired |
| 26 | Fatal recovery before delivery | Ready and late old callbacks discarded; successor fenced behind in-flight result; real committed data read by recovered VM |
| 27 | Callback error/timeout | No rollback/replay; normal callback error or canonical VM-fatal timeout behavior |
| 28 | At-most-once/no replay | Duplicate/stale results rejected; worker fault/recovery does not repeat mutation |
| 29 | Ordering | Mixed same-namespace FIFO PASS; independent namespaces get bounded rotation, not global ordering |
| 30 | Nested callbacks | Get→Set, Set→Get, Remove→Set become new bounded requests; no synchronous worker recursion |
| 31 | Queue/backpressure | Existing eight/namespace, 128 global and rates remain authoritative through pending callback delivery |
| 32 | Quota | Real private byte/global quota regression; public actual store quota and controlled quota translation PASS |
| 33 | Unavailable/corruption | Controlled failure, never fabricated absence; no repair/reset/new recovery loop |
| 34 | Deadline/uncertainty | Existing five-second request deadline; definite failures separated from Indeterminate; no retry |
| 35 | Cleanup | Reservations/VMs/roots released; exact-envelope weak-reference collection; adapter worker exit/unmap PASS |
| 36 | Diagnostics | Bounded aggregate counters; no script keys/values, SQL/path or callback identities |
| 37 | Compiler/VM | Both platforms real pinned compiler/native facade tests PASS |
| 38 | Live Carbon | Representative Windows reload and actual server restart PASS; Linux live not measured |
| 39 | Stress/performance | 2,000 reads/ten namespaces plus mutation/fault/churn cases; 18 timing observations/platform, not SLA |
| 40 | Windows | Local native/runtime/adapter/loader/package/tooling PASS |
| 41 | Linux | Local native/runtime/adapter/loader/package/tooling PASS on qualified ext4 profile |
| 42 | Sanitizer/fault | ASan/UBSan/leak + native allocation and completion/retirement fault tests PASS |
| 43 | Broader regression | A–G/addons/publication/modules/scheduler/recovery/GUI/Player/parser/packaging suites PASS; prior scope limits retained |
| 44 | Metadata/definitions | Authoritative annotations regenerated all four artifacts, exact Since0.5; historical introductions unchanged |
| 45 | Tooling | Both final local packs host/LSP/preview/worker-supervisor PASS; persistence preview remains unsupported |
| 46 | Docs/examples | Guide + service/store/value reference + six examples; production/catalog/generated/docs agree |
| 47 | Future Query | Private versionable schema/store identity/atomic transaction seam preserved; no speculative API or storage migration |
| 48 | Identity | API0.5, intended future package0.5 but current development package0.4; ABI1.5 required; provider1.2/schema1/Luau unchanged |
| 49 | 1C handoff | Canonical extended crash/lifecycle/quota/scale/final platform/public-readiness work only after separate authorization |
| 50 | Query/Update/schema/index | Not implemented |
| 51 | Persistence-1C | Not started |
| 52 | Branch/worktree | `main`, task changes uncommitted; origin remains starting baseline; no release/tag |
