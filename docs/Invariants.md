# CarbonLuau architecture and invariants

This is the canonical owner of architecture, security, ownership and lifecycle rules. Workflow and evidence policy live in [AICONTEXT.md](../AICONTEXT.md) and [Compatibility.md](Compatibility.md). **Invariant** means a required property; **accepted direction** preserves the first-version design without claiming implementation; provisional/open/deferred choices live only in the decision register below. No VM or scripting API exists in the Phase 0 baseline.

## I1 — Product and host ownership

CarbonLuau adds server-side Luau scripting to a Carbon-modded Rust dedicated server. Carbon remains the plugin host. The ownership chain is Rust server → Carbon → CarbonLuau managed host → CarbonLuau native bridge → Luau VM → scripts. CarbonLuau does not become a competing server host or replace Carbon's lifecycle.

The managed layer adapts Rust/Carbon operations; the native layer owns Luau implementation details. Rust, Carbon and Unity retain ownership of their objects. CarbonLuau owns only its runtime resources and registrations. Client scripting, Oxide compatibility, generic hook dispatch, reflection, Harmony from scripts and native codegen/JIT are outside v0.1 (design sections 3–4, 33).

## I2 — Script trust and capabilities

Server administrators choose and install scripts. That trust decision does not grant a script all the privileges of the Carbon plugin. Even an administrator's script can fail, exhaust resources or invoke an API incorrectly.

| Concept | Meaning here |
|---|---|
| Trust policy | Who may install code/configuration/native binaries; administrators and the host control these |
| Sandbox boundary | Which values, libraries and host operations script code can reach |
| Permissions | Host-side authorization for an exposed operation or command, checked through the host permission system |
| Resource containment | Limits on work, allocation and retained resources, independent of whether an operation is authorized |

Every new capability needs a named use case, validated inputs, an authorization rule where relevant, ownership/lifetime semantics, a work bound and failure tests. A bootstrap wrapper is not authorization: validate at the host boundary even if scripts bypass the wrapper. This is a narrow explicit capability surface, not a claim that per-script manifests or a capability framework already exist.

Do not expose arbitrary filesystem/network/process access, native loading, .NET reflection, unrestricted console execution, arbitrary Carbon hook names or raw game objects. Approved file discovery/module resolution is a host-owned operation confined to its script root, not a general script filesystem API (design sections 11–12, 26).

## I3 — Stable proxies, not host objects

Scripts receive simple values and validated proxies based on stable identity, never raw `BasePlayer`, `BaseEntity`, `Item`, Unity/Carbon objects, managed references, native pointers or GCHandles. Every operation re-resolves the live object and checks validity and authority. Runtime generation invalidation must reject stale references; a reused identifier must not accidentally target another lifetime/object. Phase 3's player encoding/lifetime contract is D11; other object kinds remain deferred.

Steam/user IDs remain strings in the public API, as accepted in design section 15; upstream numeric features do not silently change that contract. D11 binds proxies to one connection, not future connections from the same account. Neither proxy retention nor Lua garbage collection transfers ownership of the host object.

## I4 — Runtime ownership and thread context

The managed runtime generation owns one native VM and all its script threads, references, callbacks, commands and timers. The native implementation owns allocation/destruction of Luau state. Generation-scoped resources become unusable when that generation ends.

The accepted first-version direction is one global VM per runtime generation, sandboxed script execution environments, and module results cached per generation (design sections 9, 11). This is not per-script VM or process isolation. Stronger trust separation would require a deliberate design revision.

All CarbonLuau VM access and Rust/Unity state access belongs to the server main thread and is serialized, including lifecycle, compilation through the runtime and callback teardown. No background thread enters the VM. Luau coroutines are not parallel OS threads. Verify each incoming hook/completion's context; do not infer that every Carbon API callback runs on the main thread. Marshal an off-thread result as bounded data back through an explicit host dispatch boundary before touching the VM or game state. Avoid reentrant script execution from nested host callbacks.

Carbon documents `NextFrame`/`NextTick` and cancellable timers, but this is not blanket evidence for all hook thread contexts. The future scheduler should use those host primitives. Phase 1 needs only its scoped dispatch/lifecycle integration, not the full scheduling API. [Carbon timers](https://carbonmod.gg/devs/features/timers), [Luau thread API](https://luau.org/api/#threads).

## I5 — Project-owned ABI

The managed host binds CarbonLuau's exported C ABI, never Luau C++ symbols, STL layouts or internal VM structs. The native bridge may use Luau's public embedding API internally. Export names, calling conventions, scalar widths, buffer lengths, encoding and struct layouts must be explicit wherever used. Handles crossing this ABI are opaque to C# and must never become script-visible host pointers.

Specify borrowed versus owned data and its lifetime for every parameter/result; allocations have one owner and a matching release operation. Keep delegates/callback contexts alive until native code can no longer call them. Destroy resources deterministically, including partial initialization.

No C++ exception, managed exception or Luau nonlocal error transfer may cross a managed/native boundary. Catch errors within their owning side; use protected Luau execution and structured status/error propagation. A managed callback must return a failure before native code raises a script error. Failure reporting must work under allocation failure. Do not treat `extern "C"` alone as error containment.

Phase 0's `int carbonluau_probe(void)` returning `0x4C554155` is an intentional fixed probe contract, not a runtime status-return API or future ABI-version handshake. Preserve it. Future fallible runtime operations need explicit status semantics; the design's complete function list and serializer are not frozen (design section 8).

## I6 — Explicit native loading and packaging

Preserve the proven production loader: derive a normalized absolute path under Carbon's data directory, check the file, use Windows `LoadLibraryW`/`GetProcAddress` or Linux `dlopen`/`dlsym`, and release with the matching OS call. Do not search arbitrary locations for the project library. Native files remain outside the C#-source `.cszip` (design sections 6–7).

Admin-installed native binaries and their OS dependencies are trusted process code. The absolute path is not an isolation barrier against a malicious administrator or changed binary. A host prohibition is a deployment blocker, not an invitation to bypass host policy.

## I7 — Sandbox setup and code provenance

Install only the intended standard-library/host surface, protect shared tables, and apply `luaL_sandbox` and `luaL_sandboxthread` before executing server scripts. Keep diagnostic access in the host; do not equate upstream defaults with the project's script allowlist. Privileged bootstrap code must not rely on mutable script globals for authorization. Environment manipulation and shared module values need explicit review.

Server input is Luau source compiled by the trusted pinned compiler. Never accept arbitrary user-supplied bytecode as validated source. Luau assumes compiler-produced bytecode; sandboxing alone does not establish its safety. [Luau sandbox guidance](https://luau.org/sandbox/).

## I8 — Bounded work is correctness

Create no runtime VM without allocation accounting and a configured cap. Execute/resume no script without a monotonic deadline enforced through the VM interrupt mechanism. Bound callback queues, host-call argument sizes/work, recursive module work and retained registrations/handles as those features appear. Reject or invalidate excess work with controlled diagnostics. Numeric defaults are policy candidates, not immutable architecture.

Do not describe a VM heap cap as a cap on compiler, managed, bridge or whole-process memory. Compilation and source/module ingestion need their own bounded-input/work strategy. An interrupt does not preempt a long C#/native function, so host operations and exposed standard-library work need review. Deadline enforcement is cooperative at safepoints, not a hard real-time guarantee. [Luau sandbox guidance](https://luau.org/sandbox/).

## I9 — Failure scope and diagnostics

Compile/runtime/timeout/resource errors return controlled failures; they must not intentionally terminate Rust, corrupt ownership or leak generation resources. Continue unrelated callbacks where the VM remains usable. If state integrity cannot be established, disable and safely retire the affected runtime rather than continuing in suspect state. A shared VM means failures can affect the generation; no adversarial tenant isolation or crash-proof host process is promised.

Report category, script/callback identity and generation when available, with traceback where supported. Bound/rate-limit repeated diagnostics and retain enough detail to distinguish setup, compile, runtime, memory and timeout failures. Never use process abort or an unprotected panic as normal script-error handling (design sections 24, 26–27).

## I10 — Deterministic teardown and replacement

On unload: stop intake; disconnect/unregister host callbacks and commands; cancel timers; invalidate queued work and proxies; ensure no execution is active; release script references and threads; destroy VM state; release native runtime resources; unload the library last. Invalidation need not execute queued user code. Cancellation alone is insufficient: a late completion must check generation validity before entry. Partial startup and repeated unload must be safe.

Script/runtime reload prepares a new generation; failure to compile/load the candidate preserves the current generation. Only a validated candidate may replace it, and each resource must have an unambiguous owner throughout the transition. No state preservation is required in v0.1. This future reload is distinct from Carbon's proven plugin unload/load (design sections 10, 23).

## I11 — Intentional scripting facade

Rust/Carbon implementation → managed adaptation → CarbonLuau scripting facade → scripts. Host upgrades should be absorbed in the adaptation layer where feasible. A public contract must not expose implementation classes or make all host methods callable by name.

Signals, `task`-style functions, modules and services are accepted ergonomic directions, not Roblox emulation. Do not add Workspace, ReplicatedStorage, Instance/DataModel behavior, client replication or Roblox networking because Luau is the language. API compatibility/version identity belongs to [Compatibility.md](Compatibility.md#version-identities).

## Decision register

This is the single location for unresolved architecture/policy choices. Accepted directions are not reopened merely because implementation has not begun.

| ID / status | Retained decision and unresolved detail | Resolve by |
|---|---|---|
| D1 — resolved for Phase 1 | Default Luau C++ protected errors; private non-standard deadline cancellation crosses only internal native frames and immediately retires the entire VM. Caller-owned fixed result buffers; no script-to-managed callbacks. Full contract and upgrade qualification in [Phase1.md](Phase1.md#budgets-containment-and-recovery). | Requalify on VM/error-mode changes |
| D2 — resolved Phase 1 limits | Defaults 64 MiB/3 ms; clamp 16..256 MiB and 1..100 ms. Source 64 KiB, loaded bytecode 1 MiB, log buffer 4 KiB, 32 live VMs and one host thread per VM. Compiler/bridge memory is outside the VM cap; no compilation deadline or whole-process cap is claimed. | Revisit before broader ingestion or host work |
| D3 — resolved Phase 1 allowlist | Explicit base allowlist plus math/string/table/coroutine/bit32/utf8/buffer/vector and bounded print. No getfenv/setfenv, os/debug, loaders or host objects. Shared state and per-chunk thread sandbox helpers are mandatory. Exact list in [Phase1.md](Phase1.md#sandbox-surface). | Revalidate any exposed capability change |
| D4 — resolved Phase 2 | One global VM per generation; private entry/module environments; callback closures retain their defining environment. Successful module values are shared within a generation, nil/no return maps to true; failures retry; cycles and depth 32 are bounded. Native references/heap belong to the VM; managed Carbon drain owns admission to execution. | Requalify changes to [Phase2.md](Phase2.md) |
| D5 — resolved Phase 2 | Host snapshots UTF-8 source before execution: 64 KiB/file, 256 modules, 4 MiB total, 1024 tree entries. Canonical lowercase ASCII segments and single slash only; no aliases, absolute paths or traversal. Reject symlinks/reparse points including ancestors. Filesystem owner is trusted against concurrent rewrites. | Requalify resolver changes |
| D6 — Phase 3 first facade selected | Narrow Players/Commands facade with generation-owned Signals, bounded player identity/message/permission operations, and the private bundled Luau bootstrap. Exact first surface is in [Phase3.md](Phase3.md) and [API reference](api/README.md); D11/D12 resolve its identity/version gates. Other services remain deferred. | Qualify before public release |
| D7 — resolved Phase 2 transaction, extended Phase 3 | Production candidate executes its configured entrypoint before swap; initialization work queues but does not drain until committed. Candidate output/queue are discarded on failure; the complete healthy old generation remains. Success destroys old resources. Phase 1 smoke remains a regression fixture. Phase 3 stages signal/command definitions and atomically publishes commands through the contract in [Phase3.md](Phase3.md#transactions-and-permissions); D10 gates provisional messages. | Requalify any new host effect |
| D8 — native ABI and minimum scripting identity resolved | Native ABI encodes major/minor in uint32; managed validates major before runtime binding. Phase 3 adds ABI 1.2 without changing existing layouts. Package version and exact Luau revision remain separate identities. D12 specifies the minimum experimental scripting API identity and additive/breaking policy; a larger deprecation/negotiation framework remains deferred. | Requalify affected ABI/API changes |
| D9 — approved Phase 2 recovery | User approved on 2026-09-14: one automatic entrypoint/module-tree reconstruction per successful operator load/reload; failed reconstruction or another retirement leaves the scripting runtime unavailable until operator intervention. Automatic recovery and failed operator reload do not rearm the allowance. Never resume the failed callback or transfer old queued work. Initialization can recreate callbacks, but the host-level allowance prevents an automatic restart loop. | Qualify through [Phase2-Validation.md](Phase2-Validation.md) |
| D10 — approved Phase 3 provisional effects | User approved on 2026-09-14: reject `Player:SendMessage` with a controlled Luau error while its generation is provisional, including entry/module initialization during initial load, operator reload and D9 reconstruction. Reads and generation-local registrations remain available; `task.defer` may request a message after commit, subject to fresh connection/generation checks. Failed candidates never drain that work. No exactly-once external-effect guarantee across D9 reconstruction is introduced. | Qualify through Phase 3 tests before publication |
| D11 — selected Phase 3 identity contract | Each host-observed connection event receives a monotonically increasing token, never reused within its owning plugin instance, bound to exact managed player/connection identities plus string user ID. The host verifies connection flags and account identity on every operation; once invalidity is observed, that token stays invalid even if the host objects are reused. Only a new connection event creates a new token. A generation-owned facade retains bounded identity snapshots and opaque project tokens. Disconnect preserves readable snapshot identity but invalidates mutation. Plugin reload destroys old VMs and old sessions cannot enter the new host instance. Exhaustion fails closed. | Qualify real Carbon/Rust adapter and reconnect tests |
| D12 — selected Phase 3 API identity | First experimental gameplay facade identifies as `CarbonLuau`, scripting API `0.3.0-experimental`, through read-only game fields and operator status. Additive APIs preserve existing contracts; changing/removing names, types, failure, permission or lifetime semantics is breaking and requires an explicit version/documentation/migration decision. Experimental does not mean silent breaks. Native ABI and package versions remain separate. Bootstrap is bundled into the native build from project-owned Luau source; it does not expose its host primitive to scripts. | Qualify runtime/docs/examples agreement before declaring public |

**Evidence separation:** [Phase1-Validation.md](Phase1-Validation.md) owns the scoped execution-core results. [Phase2-Validation.md](Phase2-Validation.md) owns module/callback/recovery qualification; Phase 1 does not establish their safety. [Phase3-Validation.md](Phase3-Validation.md) owns first-facade qualification; later gameplay capabilities remain deferred.

## Evidence behind the rules

Rules I1–I11 consolidate the [accepted design](CarbonLuau_FirstVersion_Design.md), especially sections 3–12, 15, 21–27 and 33, with [Phase 0 evidence](Phase0-Validation.md). Upstream inspection on 2026-09-14 confirms available mechanisms, not a completed CarbonLuau sandbox:

- Pinned [lua.h](../native/third_party/luau/VM/include/lua.h) declares allocation and interrupt hooks, shared across coroutines; its limited interrupt-setter threading allowance does not change I4.
- Pinned [linit.cpp](../native/third_party/luau/VM/src/linit.cpp) shows the library set and sandbox helpers. [luacode.h](../native/third_party/luau/Compiler/include/luacode.h) and [lcode.cpp](../native/third_party/luau/Compiler/src/lcode.cpp) show compiler-owned/STL allocations distinct from the VM allocator.
- Pinned [ldo.cpp](../native/third_party/luau/VM/src/ldo.cpp) implements exception/longjmp paths; upstream [CMake configuration](../native/third_party/luau/CMakeLists.txt) ties `LUAU_EXTERN_C` to `LUA_USE_LONGJMP`. Do not assume the project's C exports require that upstream setting. D1 must account for the selected build mode.
