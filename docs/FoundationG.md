# Foundation G compiler containment

Foundation G contains every production Luau compilation in a shipped helper
process. It is an availability and provenance change for the v0.4.0 candidate,
not a scripting API, native ABI, provider protocol, package schema or Luau pin
change.

Starting commit: `95214f53dddbd5c408bf4259d18e2dedcfe94846`.

## Original risk and pinned-compiler findings

Before Foundation G, entry chunks, local modules, public addon modules and the
private facade bootstrap called `Luau::compile` synchronously in the server
process while holding the process-wide recursive VM registry mutex. The compile
call does not use a live `lua_State`: the pinned implementation creates a local
AST allocator, name table, parser result and bytecode builder, with compiler and
STL allocation outside the VM allocator. It reads pinned Luau feature flags but
does not enter the VM.

The pinned compiler exposes no safe cancellation hook. Moving the same call to
an in-process thread would move CPU work away from the owner thread but would not
provide a safe way to stop and reclaim a stuck compiler. Measuring elapsed time
after return has the same defect. Compiler-produced bytecode is an ordinary
bounded byte string and can be loaded later on the owner thread without any VM
state crossing the boundary.

## Selected architecture

`carbonluau_compiler` is a narrow persistent worker built and shipped beside the
native runtime from the same exact vendored Luau revision. `Compiler.cpp` owns
worker launch, deadline, validation and restart. `CompilerWorker.cpp` is the only
production file that links the Luau compiler or calls `Luau::compile`.

The private binary protocol carries one source request and one result at a time:

- request magic and protocol version;
- exact 40-byte Luau revision;
- monotonically changing request nonce;
- at most 64 KiB of source;
- response status and at most 1 MiB of compiler output.

The runtime rejects a wrong magic/version, revision, nonce, size, truncated
response, empty result, worker exception or unexpected exit. Script and addon
authors cannot supply bytecode or select a worker path. A clean production build
derives the fixed worker filename from the loaded native library's directory.

A request has a fixed one-second monotonic wall deadline. Timeout, crash,
internal failure or invalid protocol response terminates the worker; the next
request launches a clean worker. The persistent model avoids a process launch on
every first module load while retaining a killable boundary.

## Isolation and lifecycle

Windows launches the worker hidden with an explicit three-handle allowlist,
redirected stdin/stdout, discarded stderr and a job object. The job permits one
process, limits it to 256 MiB and kills it when the runtime closes the job.

Linux uses private local socket pairs, closes every unrelated inherited file
descriptor before `exec`, applies `no_new_privs`, disables core dumps, bounds
open descriptors and applies a 256 MiB address-space limit in production. The
sanitizer worker omits the address-space limit because ASan reserves a large
virtual range; the wall deadline and external kill boundary remain active.

The worker protocol exposes no filename, network operation, compiler option or
general command. Closing/unloading the native runtime first closes worker input,
allows a short graceful exit, then forcibly terminates a nonresponsive worker.
Provider unload and CarbonLuau unload cannot race an admitted compile on the
current owner-thread-only synchronous API: the owner thread is blocked until the
bounded request returns. Calls from another thread fail owner validation.

## Runtime and publication semantics

Compilation remains synchronous to the admitting owner thread. No worker thread
or process enters Luau. The global VM registry mutex is released only while the
isolated compilation request is pending and reacquired before inspecting or
changing VM state. The blocked owner thread is the only thread allowed to mutate
that VM/domain lifetime, so unrelated VM owners may progress without permitting
stale mutation of the compiling lifetime.

Entry compilation timeout returns `TIMEOUT` with no thread handle and does not
retire the untouched VM. A crash or invalid response during module compilation
becomes an ordinary catchable module compile error. A module compilation timeout
consumes the original admitted operation's wall deadline; it therefore remains
uncatchable and VM-fatal under D1/D10 even if `pcall` catches the ordinary module
error. The existing nested publication scope remains active around first module
loads, so no cache, task, command, listener or other CarbonLuau-owned resource
from a failed attempt publishes. Ordinary candidate compile failure continues to
preserve the active root/addon; a deadline timeout retires the complete shared VM
generation as it did before Foundation G.

The private facade bootstrap uses the same worker and validation path before any
facade resource is installed. Local and package-qualified module resolution,
shared values, dependency lifetimes, cycles, depth, yield rejection and retry
semantics are unchanged.

## Qualification fixtures

Native regression coverage retains bounded near-64-KiB statement, table and type
corpora and reports their compile latency beside a normal script. Dedicated
workers simulate a hang, crash, malformed header, truncated payload, oversized
payload, wrong revision, stale nonce and a 300 MiB allocation. White-box runtime
coverage catches a fast crashed first module load with `pcall`, verifies that its
cache and staged task do not publish, and retries through a restarted worker. A
separate caught module hang proves that the original admitted deadline still
retires the VM. Entry compilation timeout proves that an untouched VM can use a
fresh worker successfully afterward.

Final Windows, Linux, sanitizer, packaging, stress, process-memory and lifecycle
measurements are recorded below after qualification.

## Qualification results

Verdict: **PARTIAL**, solely because Foundation G Windows live/local
qualification is deferred. Every available Linux, sanitizer, managed regression,
packaging and live Carbon gate passed. The tested worktree is based on
`95214f53dddbd5c408bf4259d18e2dedcfe94846`; the committed implementation and CI
revision are recorded after final-source CI.

The Linux worker was Ubuntu 24.04.5 LTS, kernel 6.8, x86-64, Docker 29.8.0,
GCC 13.3, CMake 3.28.3 and Mono 6.8. Release CTest passed all five suites:
`ScriptCore`, `RuntimeAllocationFaults`, `CompilerContainment`, `RuntimeCore` and
`NativeLoadUnload`. Managed loader/runtime tests passed the Phase 0-3 and
Foundations A-F matrix, including 100-addon scale, dependency replacement,
package bounds, scheduling, allocation pressure and native unload coverage.

The final release measurements were:

| Corpus | Bytes | Isolated worker | Direct baseline |
|---|---:|---:|---:|
| `return 42`, warm | 9 | 0.013 ms | 0.008 ms |
| statements | 65,533 | 5.417 ms | 5.348 ms |
| table fields | 65,530 | 3.280 ms | 2.978 ms |
| type aliases | 65,514 | 1.131 ms | 0.905 ms |

Cold process start plus first compile was 1.131 ms. A separate process sampler
over the complete containment fixture observed 9,376 KiB peak test-harness RSS,
8,840 KiB peak compiler-worker RSS, 3,580.575 ms elapsed, 112.122 ms harness user
CPU and 58.468 ms harness system CPU. These are representative worker-host
measurements, not server-wide memory claims.

The forced hang returned at 1,102.518 ms and the worker was gone before return.
Three timeout/recovery cycles, 20 crash/restart cycles and 500 successful requests
passed. Missing worker, crash, malformed header, truncated response, oversized
response, wrong revision, stale nonce, empty/invalid payload and a 65,537-byte
request all failed closed. The release worker's 256 MiB process limit terminated
and reclaimed a 300 MiB allocation fixture, followed by a successful clean-worker
compile. The address-space fixture is intentionally skipped in ASan builds because
ASan reserves a large virtual range; the external wall deadline remains tested.

ASan, UBSan and leak detection passed all five native suites with zero reported
errors. Allocation-fault coverage also returned to zero retained allocator bytes.
The compiler and native runtime hashes used for Linux packaging were respectively
`5b82c00c70d6d6ea0b38557d6f462067c5fd25d0b02a7062c1f3aa3884d39132`
and `c73b0d21c799b950ef460bcc9944d3a3285c0aef39e1d38a8834276babb0b6a9`.
The deterministic release test passed twice, provenance includes the worker hash,
and an isolated extraction/deployment proved the documented Linux `chmod 0755`
step, worker startup/EOF teardown and native dynamic dependencies.

An isolated live Linux run used Carbon 2.0.259.0 and Rust protocol 2633.288.1.
The real plugin loaded ABI 1.4, launched the adjacent compiler worker, completed
100 successful reloads, 108 rejected candidates, 1,000 controlled-host reconnect
cycles, memory/queue/event pressure and the canonical timeout/recovery fixture.
Two plugin unload/load cycles began with modules, tasks, signals, a command and a
player-directory entry active. Each cycle unmapped the native library, reclaimed
the compiler worker, kept the server responsive and launched a fresh worker on
reload. Final server teardown left no worker process. This controlled-host run is
not authenticated-client or provider-hosting evidence.

The final hosted CI result is appended to the evidence commit after that gate
completes.

### Windows Foundation G deferral

Foundation G Windows live/local qualification is **DEFERRED / UNQUALIFIED** by
task-owner decision because the Windows worker is unavailable. No DockerPC access
or restoration was attempted. Historical Windows evidence for Foundations A-F
remains valid for those revisions but does not qualify this compiler worker.
Hosted Windows CI is useful separate evidence and is not a substitute for this
deferred gate.

A supplemental Foundation G Windows run must test the exact committed source for:

- worker process creation beside the loaded DLL;
- one-second wall termination and the 256 MiB job limit;
- crash, kill, restart and subsequent successful compilation;
- malformed, truncated, oversized, stale and revision-mismatched IPC responses;
- release archive deployment and executable discovery;
- live Carbon bootstrap, reload, compilation and plugin lifecycle integration;
- unload/server teardown with no orphan worker process.

That supplemental run can close the remaining gate without redesigning or
reimplementing Foundation G. Until then the compiler containment feature must not
be described as fully Windows-qualified.

## Scope and limitations

The one-second deadline is an availability bound, not a promise that compilation
normally consumes that time. The worker's 256 MiB limit is not the Luau VM cap or
a whole-server RSS cap. OS process creation and termination remain operating
system services, and the worker is trusted CarbonLuau code rather than an
adversarial tenant sandbox.

No multiple-VM trust tiers, immutable exports, provider capabilities, restricted
exposure, gameplay APIs, package solving/downloads or other post-v0.4 feature was
started.
