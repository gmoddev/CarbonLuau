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

Scripts receive simple values and validated proxies based on stable identity, never raw `BasePlayer`, `BaseEntity`, `Item`, Unity/Carbon objects, managed references, native pointers or GCHandles. Every operation re-resolves the live object and checks validity and authority. Host-backed script references are also bound to their owning script lifetime: VM-generation retirement invalidates every domain in that VM, while root/addon domain retirement invalidates host-backed references owned by that domain even when the shared VM remains healthy. A reused host identifier, replacement domain or later VM generation must never accidentally validate a reference from an earlier object or domain lifetime. Phase 3's player connection identity contract remains D11; other object kinds remain deferred.

Steam/user IDs remain strings in the public API, as accepted in design section 15; upstream numeric features do not silently change that contract. D11 binds proxies to one connection, not future connections from the same account. Neither proxy retention nor Lua garbage collection transfers ownership of the host object.

## I4 — Runtime ownership and thread context

CarbonLuau distinguishes the loaded **CarbonLuau host lifetime**, a **VM generation**, and the **domain lifetimes** hosted inside that VM. One VM generation owns exactly one native Luau VM. In addon-capable operation, that VM may contain one operator-root domain and multiple addon domains. Each root/addon activation receives a distinct opaque domain lifetime which owns its source namespace, dependency bindings, domain-bound host facade, queues, registrations and module-cache namespace. Healthy root/addon replacement may retire a domain without retiring the VM; fatal VM retirement invalidates every domain in that VM.

A domain is not one shared mutable globals table. The operator/addon entry chunk and every first module execution receive separate private mutable sandbox environments. Closures retain their defining environment. The VM provides the protected standard-library base; each execution environment receives the appropriate domain-bound host bindings such as `require`, `task`, services, Signals and Commands. Successful module values may be shared by reference according to D4.

All CarbonLuau VM access and Rust/Unity state access belongs to the server main thread and is serialized, including lifecycle, bytecode loading, module publication and callback teardown. Source compilation is requested synchronously by that owner thread but executes only in the isolated pinned compiler worker; the worker receives bounded source, returns bounded bytecode and never receives or enters a VM. No background thread enters the VM. Luau coroutines are not parallel OS threads. Verify each incoming hook/completion's context; do not infer that every Carbon API callback runs on the main thread. Marshal an off-thread result as bounded data back through an explicit host dispatch boundary before touching the VM or game state.

**Host-driven recursive VM entry is prohibited globally.** While Luau is executing, or while CarbonLuau is servicing that execution through a native-to-managed host callback, any Carbon/provider/timer/command/event path capable of causing further Luau execution must only validate/admit bounded work for later execution; it must not recursively enter the VM. Ordinary synchronous Luau-to-Luau calls, synchronous module loading and coroutine execution already inside the admitted operation remain part of that operation rather than new VM admission.

Carbon's scheduling primitives remain host integration mechanisms, not evidence that every callback arrives on the correct thread. The scheduler must preserve owner-thread serialization and the no-reentrant-entry rule. [Carbon timers](https://carbonmod.gg/devs/features/timers), [Luau thread API](https://luau.org/api/#threads).

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

Server input is Luau source compiled by CarbonLuau's shipped worker built from the exact pinned compiler. The bounded private protocol validates its version, Luau revision, request nonce and response size before the owner thread loads bytecode. Never accept arbitrary user-supplied bytecode as validated source. Luau assumes compiler-produced bytecode; sandboxing alone does not establish its safety. [Luau sandbox guidance](https://luau.org/sandbox/).

## I8 — Bounded work is correctness

Create no runtime VM without allocation accounting and a configured cap. Execute/resume no script without a monotonic deadline enforced through the VM interrupt mechanism. Bound callback queues, host-call argument sizes/work, recursive module work and retained registrations/handles as those features appear. Reject or invalidate excess work with controlled diagnostics. Numeric defaults are policy candidates, not immutable architecture.

Do not describe a VM heap cap as a cap on compiler, managed, bridge or whole-process memory. Compilation has a 64 KiB request bound, 1 MiB response bound, fixed one-second wall deadline and a killable worker with a 256 MiB process-memory limit in production. A timeout or invalid response terminates the worker; the next request starts a clean worker. Execution deadlines remain cooperative at VM safepoints. An interrupt does not preempt a long C#/native host operation, so host operations and exposed standard-library work still need review. [Luau sandbox guidance](https://luau.org/sandbox/).

## I9 — Failure scope and diagnostics

Compile/runtime/timeout/resource errors return controlled failures; they must not intentionally terminate Rust, corrupt ownership or leak generation resources. Continue unrelated callbacks where the VM remains usable. If state integrity cannot be established, disable and safely retire the affected runtime rather than continuing in suspect state. A shared VM means failures can affect the generation; no adversarial tenant isolation or crash-proof host process is promised.

Report category, script/callback identity and generation when available, with traceback where supported. Bound/rate-limit repeated diagnostics and retain enough detail to distinguish setup, compile, runtime, memory and timeout failures. Never use process abort or an unprotected panic as normal script-error handling (design sections 24, 26–27).

## I10 — Deterministic teardown and replacement

**VM-generation teardown** stops intake; prevents further script admission; disconnects/unregisters host callbacks and commands; cancels and invalidates queued work and host-backed proxies; ensures no execution remains active; releases domain references, module/cache references and threads; destroys VM state; releases native runtime resources; and unloads the native library last during CarbonLuau unload. A late completion must validate the current host/VM/domain lifetime before admission. Partial startup and repeated teardown must be safe.

**Domain teardown** performs the corresponding cleanup for one root/addon lifetime without requiring destruction of an otherwise healthy shared VM. CarbonLuau releases its references and host registrations for that domain; ordinary Luau values retained by another live domain are not recursively discovered or revoked.

In addon-capable operation, normal `carbonluau.reload` prepares a new operator-root domain inside the current healthy VM generation. The previous root remains published while the candidate initializes. An ordinary candidate failure preserves the previous published root lifetime but does not promise rollback of arbitrary same-VM Luau mutations performed by the candidate. Successful publication atomically replaces the root at an owner-thread safe point and then retires the previous root lifetime.

Addon replacement follows the same domain-replacement model for that addon and its affected dependency graph.

A timeout or other integrity-invalidating failure during any root/addon/candidate operation retires the **complete VM generation**. No previous root or addon domain is promised to survive a fatal VM retirement. No general user-state preservation requirement is introduced.

## I11 — Intentional scripting facade

Rust/Carbon implementation → managed adaptation → CarbonLuau scripting facade → scripts. Host upgrades should be absorbed in the adaptation layer where feasible. A public contract must not expose implementation classes or make all host methods callable by name.

Signals, `task`-style functions, modules and services are accepted ergonomic directions, not Roblox emulation. Roblox-familiar GUI names and value semantics do not introduce a generic Roblox `Instance`/DataModel or client-replication contract. Do not add Workspace, ReplicatedStorage, `Instance.new`, generic Instance/DataModel behavior, client replication or Roblox networking because Luau is the language. API compatibility/version identity belongs to [Compatibility.md](Compatibility.md#version-identities).

## Decision register

This is the single location for unresolved architecture/policy choices. Accepted directions are not reopened merely because implementation has not begun.

| ID / status | Retained decision and unresolved detail | Resolve by |
|---|---|---|
| D1 — resolved for Phase 1 | Default Luau C++ protected errors; private non-standard deadline cancellation crosses only internal native frames and immediately retires the entire VM. Caller-owned fixed result buffers; no script-to-managed callbacks. Full contract and upgrade qualification in [Phase1.md](Phase1.md#budgets-containment-and-recovery). | Requalify on VM/error-mode changes |
| D2 — resolved addon-capable limits | Defaults and clamps remain unchanged after Foundation E scale, exhaustion and live-host qualification. Addon-capable operation uses one VM-wide heap cap, never a hard per-domain quota; see the canonical D2 detail below. | Requalify any VM/memory-policy change or materially larger supported scale |
| D3 — resolved Phase 1 allowlist | Explicit base allowlist plus math/string/table/coroutine/bit32/utf8/buffer/vector and bounded print. No getfenv/setfenv, os/debug, loaders or host objects. Shared state and per-chunk thread sandbox helpers are mandatory. Exact list in [Phase1.md](Phase1.md#sandbox-surface). | Revalidate any exposed capability change |
| D4 — resolved shared-VM module semantics | Module cache and publication semantics are owned by the canonical D4 detail below. Historical Phase 2 semantics remain preserved exactly. | Requalify module/publication changes and addon module behavior before public support |
| D5 — resolved package/module resolution direction | Existing local resolution is preserved and addon dependencies add only the canonical package-qualified namespace described below. | Requalify resolver/source-ingestion changes and addon package resolution before public support |
| D6 — resolved Phase 3 first facade | Narrow Players/Commands facade with generation-owned Signals, bounded player identity/message/permission operations, and the private bundled Luau bootstrap. Exact first surface is in [Phase3.md](Phase3.md) and [API reference](api/README.md); D11/D12 resolve its identity/version gates. Other services remain deferred. | Requalify facade changes |
| D7 — resolved CarbonLuau-owned publication transaction | Candidate and nested module publication cover only CarbonLuau-owned cache/resource state; see the canonical D7 detail below. | Requalify any new host effect or publication kind |
| D8 — native ABI and minimum scripting identity resolved | Native ABI encodes major/minor in uint32; managed validates major before runtime binding. Phase 3 adds ABI 1.2 without changing existing layouts. Package version and exact Luau revision remain separate identities. D12 specifies the minimum experimental scripting API identity and additive/breaking policy; a larger deprecation/negotiation framework remains deferred. | Requalify affected ABI/API changes |
| D9 — approved shared-VM recovery | One automatic reconstruction allowance exists for the complete VM, governed by the canonical D9 detail below. | Requalify shared-VM reconstruction and operator rearm before addon public support |
| D10 — approved admitted-operation and provisional-effect model | Admission, resource ownership, publication and deadline are orthogonal as specified in the canonical D10 detail below. | Requalify admitted-operation, cross-domain facade or provisional-effect changes |
| D11 — resolved Phase 3 identity contract; domain binding added | Existing exact connection-token semantics remain, with host-backed facade validity now also bound to the owning domain lifetime; see D11 detail below. | Requalify host identity/adapter or domain-lifetime changes |
| D12 — resolved addon and GUI-capable experimental identity | The additive addon, GUI Foundation 1 and implemented GUI Foundation 2 layout/image/scrolling surfaces are assigned `CarbonLuau 0.4.0-experimental`; package, API, ABI, provider protocol, schema and Luau identities remain separate. `TextBox` is not implemented. Authenticated-client GUI observations remain unqualified and non-gating. | Requalify affected public behavior and assign an explicit migration/version decision for breaks |
| D13 — resolved/deferred for v0.1 | User approved deferral on 2026-09-14. No maintainable supported path has been established that guarantees deterministic ownership and cleanup across qualified Rust item construction, insertion, partial mutation and removal callbacks. Player:GiveItem and the entire Phase 4 item convenience surface, including Items/Items:Exists, are deferred from v0.1; no independent read-only Items use case is accepted. Preserve I1–I11 unchanged rather than excluding failure paths. [Phase4.md](Phase4.md#ownership-gate-d13) records the rejected candidate and evidence; [roadmap](CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases) records the revised scope. | Reconsider only with a stronger supported Rust/Carbon transactional item API or evidence of a safe adapter, followed by explicit scope approval and qualification |
| D14 — resolved experimental addon package/dependency/provider lifecycle | Stable package identity, lifecycle states, exact dependency bindings, provider ownership, immutable snapshots and bounded parser/registry limits are specified below and qualified by Foundation E. | Requalify lifecycle, parser, limits or protocol changes before expanding support |
| D15 - resolved GUI Foundation 1 retained presentation model; qualified for experimental public release through 1G | The retained GUI authority, ownership, presentation, interaction, publication, reconciliation, recovery and scope rules are specified below. [GuiFoundation1.md](GuiFoundation1.md) owns supporting rationale and implementation guidance; Foundations 1A through 1F record implementation/runtime evidence and [GuiFoundation1G.md](GuiFoundation1G.md) records public documentation, examples, final available qualification and the identity decision. Authenticated-client visual, cursor, click-receipt and reconciliation observations remain explicitly unqualified but no longer gate the experimental identity. | Requalify affected GUI behavior; do not claim unobserved client behavior without authenticated-client evidence |
| D16 - resolved GUI Foundation 2 architecture; implemented subset qualified for experimental public release through 2F | Foundation 2 additively specializes D15 as specified below. GUI-2A/2B/2C/2E/2F implement and qualify deterministic layout, typed images and retained scrolling under the existing `0.4.0-experimental` identity. `TextBox` and typed text ingress remain deferred and unimplemented after the exact text-preservation gate failed. [GuiFoundation2.md](GuiFoundation2.md) retains the complete supporting design. | Requalify affected behavior; reconsider TextBox only with a bounded opaque text-preserving host transport |
| D17 - resolved GUI Foundation 3 architecture | Foundation 3 additively specializes D15/D16 with deterministic grids, bounded Frame clipping, immutable project-owned fonts and one-way per-Presentation scroll effects as specified below. [GuiFoundation3.md](GuiFoundation3.md) retains the complete supporting design, implementation sequencing and qualification gates. No Foundation 3 production code or release identity is assigned by this decision. | Implement and qualify the applicable GUI-3A through GUI-3E slices before public support; omit any host-dependent feature that cannot meet its recorded gate |

### Canonical detail for resolved decisions

#### D2 — limits in addon-capable operation

Defaults remain 64 MiB/3 ms; clamps remain 16..256 MiB and 1..100 ms. Source remains 64 KiB, loaded bytecode 1 MiB, log buffer 4 KiB, with the existing native registry bound of 32 live VMs and one host thread per VM. Compiler/bridge/managed/source-snapshot memory remains outside the VM heap cap. Foundation G adds a fixed one-second compiler wall deadline and a production 256 MiB worker-process memory limit; neither is a whole-process cap.

In addon-capable operation the hard Luau allocation boundary remains **one VM-wide heap cap**, not a per-addon quota. Memory categories or equivalent accounting may be used for diagnostics but do not establish hard retained-memory isolation or guaranteed per-addon reclamation. The 32-live-VM registry bound limits VM instances, not the number of domains inside one shared VM.

Foundation E requalified the existing 64 MiB default and 16..256 MiB clamp with root-only, 1, 10, 50 and 100-addon configurations, shared-heap exhaustion and live Carbon operation. At 100 representative addons, measured VM allocation remained under 2 MiB on both Windows and Linux workers; fifteen retained 4 MiB buffers reached a controlled global rejection near the configured cap while the VM and root remained usable. The default therefore remains 64 MiB. This is a shared safety boundary, not a per-addon guarantee or a whole-process cap.

#### D4 — shared-VM module semantics

One global Luau VM exists per VM generation. Each root/addon activation has a distinct domain lifetime. Entry chunks and every first module execution receive separate private mutable sandbox environments; modules do not inherit the requiring entrypoint's mutable globals, and closures retain their defining module environment.

Module-cache identity is **VM generation + defining domain lifetime + logical module path**. Local and public access to the same module in the same defining domain lifetime use the same cache entry.

Preserve the qualified Phase 2 module contract:

- Once a module cache entry is successfully published, the module executes once for that cache lifetime.
- Exactly the first return value is cached by reference.
- `nil` or no return caches `true`; additional returns are ignored.
- Failed loads are not cached and may be retried.
- Recursive loading raises a controlled cycle error; recursive module depth remains bounded at 32.
- Module initialization may not yield.
- Module mutable globals remain private to that execution environment; returned closures retain it.

Public modules return ordinary same-VM Luau values. CarbonLuau does not recursively copy, proxy, inspect or revoke their object graphs. Ordinary Luau values retained by another domain may survive retirement of the defining domain while the VM remains alive; they never silently become values from a replacement domain. Any captured host-backed facade or handle still validates its original domain lifetime.

Every first-load attempt has a nested module-publication scope for its cache entry and CarbonLuau-owned resources created synchronously during initialization. On module failure, no cache entry is published and all resources staged by that attempt are discarded, including when `pcall`/`xpcall` catches the ordinary module error. A successful first load during an ordinary committed operation publishes its cache entry and module-created resources at module completion. During a provisional root/addon operation it merges them into the outer provisional publication; outer commit publishes both and outer failure discards both.

These rules are not a transaction over arbitrary Luau memory. Ordinary table/global mutations and plain references leaked into already-reachable shared state are not rolled back, are not canonical cache entries, and are not deeply discovered or revoked.

#### D5 — package/module resolution

Preserve bounded UTF-8 source snapshots, canonical lowercase ASCII logical segments separated by one `/`, no absolute/traversal/dot-relative path, filesystem fallback, search path or implicit loader, and existing filesystem/reparse-point confinement for operator snapshots. Unqualified `require("private/util")` remains a logical module path in the current defining source namespace.

Addon dependencies extend resolution only with `require("@addon")`, `require("@addon/path")`, `require("@creator.addon")` and `require("@creator.addon/path")`. The text after `@` is the declared stable package ID, not a resolver alias. Resolution requires the caller's declared binding and targets the exact committed dependency lifetime bound to that consumer. `@id/path` may resolve only a public module; `@id` resolves the declared `main` and errors if none exists. `main` is inherently public, resolves a module, and never implicitly executes `init.luau`.

No `./`, `../`, arbitrary filesystem access, fallback search, undeclared dependency import, user-defined alias or `GetDependency():Require()` layer is introduced. The operator root remains outside the addon dependency graph for the first addon release: addons cannot depend on it and root-to-addon consumption remains deferred.

#### D7 — candidate and module publication

Root/addon candidate initialization is transactional only over **CarbonLuau-owned publication**, not arbitrary same-VM Luau memory. A candidate stages its domain/package visibility, commands, subscriptions/listeners, queued-work admission and other host-owned registrations. D4 also stages first-load cache entries and module-created host resources required by the active publication scope.

An ordinary candidate failure discards every uncommitted CarbonLuau-owned publication from that candidate while preserving the previously committed domain lifetime. Plain same-VM Luau mutations may remain. A failed first-load module scope never merges its cache entry or staged resources into a containing candidate, even if its error is caught and the candidate later succeeds.

A successful candidate commits at an owner-thread safe point and only then retires the previous domain. Commit revalidates every resource-owner domain lifetime. A timeout or integrity-invalidating failure retires the complete VM generation under D1/I9/I10; an old domain is not preserved across that fatal retirement. Historical Phase 1–3 candidate fixtures remain evidence only for the implementations they tested.

#### D9 — global recovery

Exactly one automatic reconstruction allowance exists for the complete shared VM generation, never one per domain. A successful initial operator load arms it; a successful explicit operator reload/rebuild rearms it; a failed operator action does not.

Fatal VM retirement consumes the allowance and, when available, creates a fresh generation from committed immutable state: discard old work and VM-local references; reconstruct the operator root; reconstruct addons from snapshots owned by still-live provider registrations; then activate addons in deterministic required-dependency order. The failed operation is never resumed or replayed.

An ordinary addon reconstruction failure marks that registration Failed and blocks required dependents while unrelated eligible addons may continue if the VM stays healthy. Root reconstruction failure, or a fatal failure during reconstruction, aborts it and leaves scripting unavailable. Automatic recovery, successful automatic reconstruction, addon/dependency/provider activity and provider churn do not rearm the allowance.

When healthy, `carbonluau.reload` replaces the root domain. When unavailable, it requests a full reconstruction. Only a successful explicit operator action arms/rearms one future automatic reconstruction. No callback replay, host-effect rollback or exactly-once guarantee is introduced.

#### D10 — admitted-operation and provisional-effect model

Every Luau entry admitted by CarbonLuau has one operation context containing deadline/budget ownership, provisional/publication state and diagnostic identity. It follows the entire synchronous chain, including exported dependency calls, nested modules and synchronous coroutine execution. Crossing a domain boundary does not reset the deadline or remove provisional restrictions.

- `ResourceOwner` is the domain lifetime bound to the API/facade object used.
- `PublicationContext` is the current admitted operation/module/candidate publication scope.
- `Deadline` belongs to the original admitted operation.
- `LifetimeCheck` uses the owning domain lifetime associated with the bound host object.
- `Diagnostics` combine admitted-operation identity with source provenance.

If provisional B calls active A and A uses A's captured facade, the resource is A-owned but remains subject to B's provisional publication context. It cannot publish merely because A is active. Explicit use of a valid B-bound facade makes the resource B-owned under the same admitted operation. Failed nested publication scopes never become eligible for parent commit.

Irreversible host mutation remains prohibited while the operation is provisional. Deferred candidate work cannot run before commit, failed candidates never drain it, and dependencies or scheduling cannot launder a provisional effect. Ordinary errors remain catchable by `pcall`/`xpcall`; only an uncaught ordinary error escaping the admitted boundary fails the operation. D1 deadline cancellation remains uncatchable and VM-fatal. CarbonLuau does not roll back ordinary Luau mutations or promise exactly-once host effects.

#### D11 — player identity and domain lifetime

Each host-observed connection still receives a monotonically increasing token, never reused within the CarbonLuau plugin instance, bound to exact managed player identity, exact connection identity and string user ID. Every operation re-resolves and validates connection/account identity; invalidity latches, disconnect preserves readable snapshot identity but invalidates mutation, only a new connection creates a new token, and exhaustion fails closed.

A domain-bound facade retains bounded identity snapshots and opaque project tokens. Root/addon retirement invalidates host-backed Player/facade mutation through that domain even when the shared VM remains healthy. An ordinary retained Player/table value may keep a readable script-side snapshot, but mutation/permission operations through its retired owning facade fail closed. Complete VM retirement invalidates every domain. CarbonLuau reload destroys the VM and no new host instance accepts old host/domain/provider tokens.

#### D12 — scripting and protocol identity

The gameplay facade introduced as `CarbonLuau 0.3.0-experimental` remains compatible. Foundation E assigns the additive addon-capable scripting identity `CarbonLuau 0.4.0-experimental`. GUI Foundation 1G keeps that unreleased identity and adds the complete D15 scripting surface without another version increment. GUI Foundation 2F keeps the same identity and adds the implemented D16 layout, typed-image and retained-scrolling subset. The identity includes package-qualified `require("@id")` and `require("@id/path")`, existing local `require("path")`, `addon.Id`/`addon.Version`, `addon:IsDependencyAvailable(id)` for a declared binding, inherently public `main`, and the domain-bound `Gui` service/value/object surface specified by D15 plus the implemented D16 subset. `TextBox` and `Submitted` are not part of the identity.

The v0.4.0 candidate maps package `0.4.0` to scripting API `0.4.0-experimental`, native ABI `1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1`, and the unchanged pinned Luau revision. These are separate identities even when one release records them together. Provider-defined C# capabilities are not part of protocol 1.2.

Additive APIs preserve accepted names, types, authorization and lifetime/failure behavior unless an explicit breaking-version/migration decision says otherwise. Experimental status does not authorize silent breaks. D4/D7 same-reference sharing, stale-binding rejection and CarbonLuau-owned publication semantics are public compatibility behavior in 0.4.0-experimental. No stable or 1.0 identity is assigned. The lack of authenticated-client evidence does not block this experimental identity, but visual correctness, cursor behavior, actual client click receipt and client-side reconciliation remain unqualified and must not be implied by API availability.

#### D14 — addon package, dependency and provider lifecycle

Stable addon IDs are `addonname` or `creator.addonname`. Each lowercase ASCII segment is 1–32 characters, starts and ends alphanumeric, and may contain internal `_`/`-`; the two-segment form is at most 65 characters. `carbonluau` and `carbonluau.*` are reserved. Noncanonical case is rejected, not aliased. Exactly one registration owns an ID; package version is informational/provenance. Parallel versions, ranges, solving, downloads, registries and lockfiles are deferred.

The internal registration states are `Registered`, `Blocked`, `Initializing`, `Active`, `Failed` and `Stopping`. Acceptance reserves the ID. Missing required dependencies block before execution. Successful initialization creates a fresh opaque domain lifetime and becomes Active. An uncaught ordinary initialization error becomes Failed. Failed registrations do not retry on unrelated activity. A relevant required-dependency restoration may schedule one bounded activation attempt during that graph transition; failure then requires explicit retry/reload/replacement. Stopping retains the ID until cleanup completes.

Activation binds declared dependencies to exact committed Active domain lifetimes. Required absence blocks; optional absence is fixed for that consumer lifetime. Required loss retires/blocks the affected required-dependent graph. Optional loss leaves the consumer active but makes availability false and new imports fail. Already returned ordinary values may remain. Replacement never retargets old bindings. Successful required-dependency replacement reinitializes eligible required dependents in deterministic topological order; optional consumers do not automatically rebind. Required cycles block their strongly connected component; optional edges alone do not. Runtime module cycles remain D4 errors. Declarations, not caught imports, define lifecycle requirements.

A provider lifetime is the concrete loaded Carbon `Plugin` object plus the current CarbonLuau host lifetime. It controls lifecycle ownership, replacement/unregister authority and stale-token detection, not authentication against other trusted in-process plugins. Only the owner may replace/unregister. Provider unload retires its registrations. CarbonLuau unload invalidates all provider tokens; a surviving provider must explicitly register again with the new CarbonLuau instance. Old tokens stay stale and Luau never receives the `Plugin` object.

The primary package input is an immutable CarbonLuau-owned source snapshot. Archive and single-source/provider transports normalize to it, and accepted provider input is copied before registration returns. Parsing rejects malformed UTF-8/JSON, duplicate keys, unknown semantic fields, noncanonical/duplicate normalized paths, absolute/traversal/dot/backslash paths, encrypted/unsupported/symlink-style entries and nested archives. Packages are bounded snapshots, never mounted or extracted. Schema 1 limits are 4 MiB archive bytes, 8 MiB expanded bytes, 64 KiB manifest, 64 KiB per source, 4 MiB aggregate source per package, 256 source modules, 512 archive entries, 32 dependencies, 128 live registrations, 32 registrations per provider and 32 MiB of aggregate immutable snapshots. Streamed decompressed bytes are enforced independently of ZIP metadata. Authors use D5 `require` plus `addon:IsDependencyAvailable`; tokens, IDs, graph transitions and lifecycle machinery stay internal.

#### D15 — GUI Foundation 1 retained presentation model

GUI Foundation 1 is a CarbonLuau-owned, server-driven retained model. Its public boundary is the domain-bound `Gui` service; `ScreenGui`, `Frame`, `TextLabel` and `TextButton`; immutable `UDim`, `UDim2`, `Vector2` and `Color3` values; retained `Name`/`Parent`/`ClassName`, `Create`, `GetChildren`, `FindFirstChild`, `IsA`, `Clone`, `Destroy`, `Show`, `Hide`, `IsShown`, common layout/visual and text properties, `TextButton.Activated`, and automatic bounded synchronization. It does not introduce `Instance.new`, a generic Instance/DataModel, raw CUI access, arbitrary client commands, images, text input, scrolling, automatic layouts or advanced styling/constraints. [GuiFoundation1.md](GuiFoundation1.md#public-foundation-1-boundary) owns the detailed implementation-facing surface.

Every GUI object and GUI-owned resource has one immutable `ResourceOwner`: the domain lifetime bound to the `Gui` service or object facade used to create it. Sharing an ordinary same-VM Luau reference does not transfer ownership. Operations revalidate that owner lifetime. Cross-domain parenting is rejected, while a live foreign domain may otherwise use a valid shared reference. Retirement of the owner recursively retires its GUI objects, GUI Signals, presentations, action tokens and queued GUI work. A destroyed or stale GUI reference retains identity equality only; host-backed operations fail with a controlled error.

The retained tree and desired presentation set are authoritative. A `Presentation` is internal state for exactly one `(ScreenGui, D11 exact Player connection lifetime)` pair and owns client element identities, synchronization state, an epoch and presentation-specific action identities. One `ScreenGui` tree may be shown to multiple Players; committed mutations affect all of its current presentations. Per-player retained state requires an explicit deep `Clone`. `Show`/`Hide`/`IsShown` describe desired server state, not acknowledged client state.

Committed retained mutations are synchronous and immediately readable. Client synchronization occurs later, on the owner thread outside Luau entry, and is bounded and coalesced toward the newest retained revision. Rust CUI is a best-effort projection without a client acknowledgement contract. Pure supported property changes may use deterministic patches; structural change, known delivery uncertainty and bounded reconciliation checkpoints require deterministic whole-presentation replacement from final retained state. Backend failure never rolls back the retained model or retroactively raises from a completed mutation: it records bounded diagnostics, invalidates unsafe interaction state and leaves the presentation needing a later full resynchronization. Overload collapses obsolete dirty detail into full resynchronization, preserves newest authoritative state and bounded fair work, and rejects excess client events before Luau entry.

Each client interaction is untrusted input admitted through one private CarbonLuau path. Every `TextButton` action uses a cryptographically opaque, presentation-specific token bound to VM generation, owner domain, `ScreenGui`, exact D11 Player connection, presentation epoch and button. Luau neither supplies nor receives raw CUI commands or authority-bearing token data. Hide/show, full rebuild, disconnect, owner retirement and VM retirement make earlier epochs stale. Forged, stale, cross-Player and over-limit input fails before Luau entry; lifetime-sensitive state is revalidated again before callback entry. Rust/Carbon GUI intake may only validate and enqueue bounded work under I4 and must never recursively enter the VM. GUI mutation from an `Activated` callback synchronizes only in a later flush.

GUI resources and mutations participate in D7/D10 publication. Creation, GUI Signal connection, mutation and Show/Hide intent during provisional execution are staged as CarbonLuau-owned state; no client effect or usable action token exists before commit. The critical foreign-owner case remains `ResourceOwner = A` and `PublicationContext = B`: when provisional B mutates committed A-owned GUI state, a bounded GUI publication journal overlays the committed tree and gives B read-your-writes within that publication context. Commit first revalidates A's exact lifetime, then atomically applies the staged retained changes and presentation intent before they become eligible for later client synchronization. Failure discards the journal, leaving committed A state and clients unchanged. This transaction does not inspect or roll back arbitrary Luau tables or other ordinary memory.

Sibling attachment order is deterministic. Rendering orders siblings by `ZIndex`, then attachment ordinal, then object identity; `GetChildren` and duplicate-name `FindFirstChild` use retained attachment order rather than rendered Z order. A deep `Clone` copies classes, public properties, hierarchy and sibling order into new identities owned by the source object's domain, but copies no parent, viewers, presentation/client state, action tokens, pending dirties or Signal connections. `Destroy` is idempotent, recursively marks the subtree destroyed, removes retained parentage, disconnects GUI-owned Signals, invalidates tokens and prevents later queued callback entry. Reparenting is atomic in retained state: cycles and cross-domain parents fail without mutation; detachment and same-owner cross-`ScreenGui` moves are allowed and structurally reconcile all affected roots.

Healthy domain replacement keeps the old committed GUI publication active until candidate commit. A failed candidate leaves it unchanged. Successful commit publishes the new domain, invalidates and retires the old domain's GUI state, then converges affected clients by bounded best-effort reconciliation. Fatal VM retirement invalidates all old GUI tokens and resources; D9 reconstruction does not preserve runtime GUI trees or callbacks, and reconstructed scripts must recreate and re-show GUI through normal publication.

Implementations must enforce hard configurable safety bounds at appropriate object, tree, domain, exact Player connection and global scopes, including retained objects/depth/text, presentations/viewers, Signal connections, tokens, dirty state, serialization and event admission. Numeric transport and scheduling values in [the design record](GuiFoundation1.md#bounds-and-tuning) are initial implementation/qualification targets, not permanent compatibility guarantees. Any public hard limit or default must be documented and qualified before support. Host-specific CUI mechanics remain adapter evidence; the invariant is deterministic bounded convergence from retained authority, not a promise about a particular Carbon/Rust method.

GUI Foundation 1 is part of the experimental `CarbonLuau
0.4.0-experimental` scripting identity and package `0.4.0`. This is the smallest
accurate identity because the addon-capable candidate has not been released and
GUI is an additive part of its first public compatibility surface. Native ABI
`1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and the
pinned Luau revision are unchanged. Authenticated-client GUI observations remain
unqualified and non-gating under the explicit Foundation 1G scope decision.

#### D16 — GUI Foundation 2 deterministic layout and rich controls

GUI Foundation 2 additively extends D15 with retained `UIListLayout`,
`UIPadding`, `ScrollingFrame`, `ImageLabel`, `ImageButton` and `TextBox`, the
immutable `ImageSource` value type, and `GuiObject.LayoutOrder`.
`ImageButton.Activated` reuses D15 interaction semantics and `TextBox` adds
`Submitted(Player, Text)`. D15 remains authoritative for ownership,
publication, Presentation lifetime, synchronization, interaction security,
replacement, recovery and bounded resources except where this decision
specializes new Foundation 2 state.

CarbonLuau computes Foundation 2 layout from retained `UDim`/`UDim2` state.
Host or Unity layout groups, client viewport measurements and client text
measurements are not authoritative. `UIListLayout` and `UIPadding` are ordinary
owner-bound, non-rendering retained children; a parent may have at most one of
each. `LayoutOrder` controls list geometry independently of `ZIndex`, retained
attachment order and `GetChildren`. List layout never rewrites script-visible
`Position`; removing it restores retained `Position` as projection authority.
Child `Size` remains retained authority, hidden children consume no list space,
and recomputation is bounded to the relevant direct arranged children.

`ScrollingFrame` is a retained `GuiObject` container. `CanvasSize`,
`ScrollingDirection` and `ScrollingEnabled` are shared retained state. Current
scroll offset, inertia, gesture/drag state and related transient behavior belong
only to each Presentation/client. Foundation 2 exposes neither
`CanvasPosition` nor `AutomaticCanvasSize`, and CarbonLuau does not claim to
know current client scroll state. Full reconciliation, Hide/Show, replacement,
disconnect or recovery may reset client-local scroll position.

`ImageSource` is an immutable, host-lifetime-independent value with only
`None`, `Sprite`, `Png`, `Item` and `SteamAvatar` source kinds. It grants no
filesystem, FileStorage, Carbon image-database, network or other host
capability. Foundation 2 provides no arbitrary URL image source. `ImageLabel`
and `ImageButton` consume this typed value. `ImageButton.Activated` uses the
existing opaque, exact-Player, Presentation-bound D15 action authority.

`TextBox.Text` is shared retained server state; the currently typed client draft
and focus/cursor/selection state are Presentation-local. Client editing never
implicitly mutates retained `Text`. Submission produces the validated immutable
payload `Submitted(Player, Text)` and changes retained `Text` only when script
explicitly assigns it. Foundation 2 TextBox is single-line and adds retained
`PlaceholderText`, `MaxLength` and `TextEditable`; it does not expose counterfeit
Roblox `FocusLost` semantics.

Text submission extends the one existing private GUI ingress with typed action
records rather than adding another client-command transport. Action kind
distinguishes `Activated` from `TextSubmitted`; authority remains bound to VM
generation, owner domain lifetime, `ScreenGui`, target, exact Player connection
and Presentation epoch. Payloads are bounded, validated and immutable; queue
and rate admission plus listener fanout are atomic; lifetime, action kind,
target availability, editability and listener registration are revalidated
before scheduled Luau entry. Luau never supplies commands, tokens, target IDs
or Presentation IDs, and Carbon dispatch never synchronously enters Luau.

The public `Submitted` contract requires exact preservation of supported
single-line text through the current authenticated Rust/Carbon transport,
including spaces, leading/trailing and repeated whitespace, quotes,
backslashes and Unicode. If current authenticated-client/host qualification
cannot establish that contract, `TextBox` is deferred from Foundation 2. The
contract must not be weakened into console-command argument semantics and input
must not be silently normalized merely to ship the class. This is a
qualification gate, not an unresolved architecture choice.

Foundation 2 adds an internal layout-affecting dirty classification. A layout
mutation recomputes only relevant direct arranged children; there is no
historical layout-work queue and latest retained state wins. Overflow collapses
to D15 whole-presentation reconciliation. Image color/transparency and retained
TextBox text/placeholder/editability may patch where supported; image source and
scrolling configuration may initially require full reconciliation.
Presentation-local drafts and scroll state are not retained dirty state. These
classifications do not create a second consistency model.

D15 ownership and publication apply without exception. Layout helpers are
ordinary domain-owned retained resources; `ImageSource` has no domain lifetime;
`Submitted` Signal ownership follows the `TextBox` owner; input authority is
Presentation-specific; and local scroll state belongs only to the Presentation.
All retained Foundation 2 mutations participate in the existing GUI publication
journal. Provisional work creates no client effect or usable interaction
authority before commit. Sharing references never transfers ownership.

Foundation 2 implementations must bound layout cardinality/work, TextBox scalar
and UTF-8 input, raw typed payload, input admission, image-source
representations, projected render elements and queued GUI text bytes. The
initial hard safety envelope is one `UIListLayout` and one `UIPadding` per
parent; the existing 64 direct-child layout bound; `MaxLength` at most 256
Unicode scalars; submitted text at most 1,024 UTF-8 bytes; raw input command tail
at most 1,536 UTF-8 bytes; sprite source at most 256 UTF-8 bytes; canonical PNG,
Steam and skin identifiers at most 20 ASCII digits; queued text at most 64 KiB
per domain and 256 KiB globally; and a full authoritative Presentation that
fits configured projection/reconciliation limits. Layout helpers count against
normal object limits and interactive controls against existing token limits.
Per-token submission rates and exact scheduling/timing values remain
implementation qualification targets, not permanent public compatibility
guarantees. Existing serializer limits must not simply be raised to fit richer
controls.

Foundation 2 excludes `UIGridLayout`, `AbsoluteContentSize`,
`AutomaticCanvasSize`, `CanvasPosition`, general `ClipsDescendants`, `UIStroke`,
`UICorner`, `TextScaled`, `TextBounds`, rich text, wrapping controls, public font
selection, `FocusLost`/`Focused`/`CaptureFocus`/`IsFocused`, multiline or
password TextBox behavior, arbitrary URL images, Carbon image-database
integration, drag/drop, client geometry queries and arbitrary client scripting.
These are outside Foundation 2, not permanent rejection of a separately designed
future phase.

Existing Foundation 1 behavior remains unchanged unless an author uses a new
Foundation 2 object/property: ordinary `Position`, coordinates without padding,
`TextButton.Activated`, `ZIndex`, attachment-order `GetChildren`, `Clone`,
`Destroy`, D15 publication/replacement/recovery and one-tree/multiple-Presentation
semantics are preserved. GUI Foundation 2F assigns the implemented layout,
typed-image and retained-scrolling subset to the already-unreleased package
`0.4.0` and scripting API `0.4.0-experimental`. This is additive to the first
public 0.4 compatibility surface and does not warrant 0.5.0. Native ABI `1.4`,
provider protocol `1.2`, package schema `1` and the pinned Luau revision remain
unchanged. `TextBox`, `Submitted` and typed text ingress remain deferred and
unimplemented; their design above is preserved for possible future reconsideration.

#### D17 - GUI Foundation 3 deterministic grids, clipping, fonts and Presentation scroll intent

GUI Foundation 3 additively extends D15 and D16 with deterministic
`UIGridLayout`, bounded rectangular `Frame.ClipsDescendants`, immutable
project-owned `GuiFont` values and retained `TextLabel.Font` and
`TextButton.Font`, plus one-way per-Presentation `ScrollingFrame:ScrollTo`,
`ScrollToTop` and `ScrollToBottom` effects. No other Foundation 3 public feature
is approved. D15 and D16 remain authoritative for GUI ownership, publication,
Presentation lifetime, synchronization, interaction security, retained versus
client-local scrolling state, typed images, replacement, recovery and resource
discipline except where this decision explicitly specializes the new surface.

`UIGridLayout` is an ordinary owner-bound, non-rendering retained layout helper
computed by CarbonLuau from retained affine geometry. Its properties are
`CellSize` (`UDim2`, default `UDim2.fromOffset(100, 100)`, non-negative scale
and offset components), `CellPadding` (`UDim2`, default
`UDim2.fromOffset(0, 0)`, non-negative scale and offset components),
`FillDirection` (`"Horizontal"` or `"Vertical"`, default `"Horizontal"`),
`FillDirectionMaxCells` (integer `1..64`, default `1`),
`HorizontalAlignment` (`"Left"`, `"Center"` or `"Right"`, default `"Left"`)
and `VerticalAlignment` (`"Top"`, `"Center"` or `"Bottom"`, default `"Top"`).
A parent may contain at most one active `UIListLayout` or `UIGridLayout`, not
one of each; `UIPadding` may coexist with either. Conflicting layout-manager
creation or reparenting fails atomically.

Grid ordering is `LayoutOrder`, then retained attachment ordinal, then object
identity. `ZIndex` remains independent and `GetChildren` remains attachment
order. Hidden children consume no cell. `UIPadding` establishes the content
rectangle before grid geometry is computed. Explicit `FillDirectionMaxCells`
determines row or column topology without client pixels or automatic
fit-to-parent behavior. Grid overflow does not resize cells, change topology,
clip automatically or query available client space. Nested grids are allowed;
each layout operates only on its own direct children. Foundation 3 has no
`StartCorner`, `SortOrder`, `AbsoluteContentSize`, automatic cell sizing or
client-dependent wrapping.

While a direct child is governed by `UIGridLayout`, its retained `Position` and
`Size` remain synchronously readable and writable but neither determines its
projected outer rectangle. The grid cell determines projected position and
projected size. `AnchorPoint` remains retained and participates only in encoding
the computed cell rectangle; it does not change the cell bounds. Grid projection
never rewrites retained `Position` or `Size`. Removing or leaving the grid
restores their latest retained values as projection authority. This intentionally
differs from `UIListLayout`, under which retained child `Size` remains projection
authority.

`Frame.ClipsDescendants` is retained shared boolean state with default `false`.
When true, CarbonLuau projects private rectangular clipping state below the
Frame and routes projected descendants through it. That representation is not
retained or Luau-addressable, does not depend on Frame background visibility or
transparency, and affects descendant rendering and interaction eligibility.
Nested clipping is allowed within the effective depth bound. The property is
initially structural/full-rebuild for synchronization. `ScrollingFrame` keeps
its existing private viewport clipping and is not redefined by this property.

Authenticated current-client qualification must establish visible descendant
clipping, transparent-parent behavior, nested clipping, hit rejection outside
clipped regions and interoperability with `ScrollingFrame` clipping. If that
contract cannot be established, `ClipsDescendants` must be omitted from the
implemented/public Foundation 3 release subset rather than weakened. This is a
qualification gate, not an unresolved architecture choice.

`GuiFont` is an immutable host-lifetime-independent value with exactly
`RobotoCondensedRegular`, `RobotoCondensedBold`, `DroidSansMono` and
`PermanentMarker`. It has no constructor and exposes no arbitrary string, path,
filesystem or other host capability. `TextLabel.Font` and `TextButton.Font` are
retained shared properties, default to `RobotoCondensedRegular`, and are
patchable where qualified. Host font identifiers remain backend details. Each
member requires supported-client qualification before public release; an
unavailable member must be removed before qualification rather than silently
falling back or exposing host strings. Existing visual behavior remains
unchanged until an author explicitly uses the feature.

`ScrollingFrame:ScrollTo(Player, Vector2)`, `ScrollToTop(Player)` and
`ScrollToBottom(Player)` are Presentation-specific one-way effects, not
retained scroll state, `CanvasPosition`, readable state or shared viewer state.
`ScrollTo` accepts normalized CarbonLuau coordinates from `0..1`, where `(0, 0)`
is top-left and `(1, 1)` is bottom-right, and transmits only enabled axes.
Top/Bottom change only vertical intent and require Y scrolling. Each operation
targets one exact D11 Player connection, one current eligible Presentation and
the specified `ScrollingFrame`; absence produces a controlled programming error
under the existing GUI error model. Other viewers remain unaffected, and
`Clone` copies no pending effect.

A scroll intent is bounded one-shot Presentation effect state, not retained
authority. During committed execution, CarbonLuau validates the
`ScrollingFrame` owner, exact Player and current Presentation, stages the latest
pending effect and sends it during a later GUI flush. During provisional
execution, `ResourceOwner` is the `ScrollingFrame` owner and
`PublicationContext` is the admitted provisional operation. The journaled
effect cannot become client-visible before commit. Commit first publishes
retained changes, then resolves the resulting current Presentation, binds the
effect to that Presentation epoch and enqueues it for a later flush. If the exact
Player or Presentation disappears before commit, the ephemeral effect is
discarded with bounded diagnostics without rolling back otherwise valid retained
publication.

Pending scroll effects are bounded and latest-wins per
`(Presentation, ScrollingFrame)` with no historical log. If a rebuild and effect
are due together, the rebuild is emitted first, or the effect may equivalently
be folded into the replacement projection. A local host rejection retains the
latest effect for a later eligible synchronization attempt. Local host acceptance
consumes it; there is no client acknowledgement, and a later unrelated rebuild
does not replay it. Before public qualification, current-client evidence must
cover top/bottom orientation, midpoint, horizontal/vertical/XY behavior, exact
Player isolation, repeated same-frame latest-wins behavior, rebuild ordering and
local failure/retry. A bounded structural replacement may implement the same
one-way contract if partial update is unreliable; `CanvasPosition` remains
excluded.

Foundation 3 adds these synchronization classifications: grid
creation/destruction/reparenting and `ClipsDescendants` are structural; grid
property mutation, `LayoutOrder` under list/grid, `Visible` under grid,
`AnchorPoint` under grid and `UIPadding` with grid are layout-affecting;
`LayoutOrder` without layout is metadata-only; `Position` and `Size` under grid
are retained-only while the grid governs projection; `Font` is patchable; and
`ScrollTo*` is a Presentation-local effect. Grid recomputation touches only
direct arranged children. There is no historical geometry queue, and existing
dirty overflow still collapses to authoritative whole-presentation
reconciliation.

D15/D16 ownership and retained publication apply without a new ownership model.
`UIGridLayout` is an ordinary owner-bound retained GUI resource;
`ClipsDescendants` and `Font` are ordinary retained properties; `GuiFont` owns no
host resource; and foreign-domain mutation continues using the existing
`ResourceOwner` and `PublicationContext` semantics. Only `ScrollTo*` uses the
new bounded Presentation-effect publication rule.

Foundation 3 preserves existing global envelopes. Initial hard additions are
one active `UIListLayout`/`UIGridLayout` total and one `UIPadding` per parent;
at most 64 arranged grid children; `FillDirectionMaxCells` `1..64`;
O(direct-child) grid work with no synthesized empty retained cells; effective
nested clipping depth four, counting explicit Frame clips and private
`ScrollingFrame` viewport clips; one private clipping projection element per
clipping Frame; the unchanged 257 projected-element screen limit; at most 16
pending scroll effects per Presentation, 512 per domain and 4096 globally; and
one latest pending value per `(Presentation, ScrollingFrame)`. A
`ClipsDescendants` mutation that would exceed authoritative projection bounds
fails atomically. Timing and rate measurements are qualification targets, not
public compatibility guarantees.

Foundation 3 excludes `AutomaticSize`, `AutomaticCanvasSize`,
`AbsoluteContentSize`, `CanvasPosition`, `UIAspectRatioConstraint`,
`UISizeConstraint`, generic constraints, `UIStroke`, a CarbonLuau outline
helper, `UICorner`, advanced image scale modes, arbitrary fonts or font paths,
`TextBox`, animations/tweens, drag/drop, focus/navigation, client geometry and
arbitrary client scripting. `TextBox` remains deferred under D16's exact-text
transport gate and is not reopened.

Existing Foundation 1/2 behavior remains unchanged unless an author explicitly
uses a Foundation 3 feature. Ordinary `Position`/`Size`, `UIListLayout`,
`UIPadding`, `LayoutOrder`, `ZIndex`, attachment-order `GetChildren`,
`Clone`/`Destroy`, `TextButton`/`ImageButton.Activated`, explicit `CanvasSize`,
client-local scrolling, `ImageSource`, D15/D16 publication,
replacement/recovery and one-tree/multiple-Presentation semantics are preserved.
Grid projection overrides `Position`/`Size` only while the child is actively
governed by `UIGridLayout`.

This architecture adoption assigns no package or scripting API identity and
does not change package `0.4.0`, scripting API `0.4.0-experimental`, native ABI
`1.4`, provider protocol `1.2`, package schema `1` or the pinned Luau revision.
Foundation 3 release identity remains gated on implementation, qualification
and later release planning.

**Evidence separation:** [Phase1-Validation.md](Phase1-Validation.md) owns the scoped execution-core results. [Phase2-Validation.md](Phase2-Validation.md) owns module/callback/recovery qualification; Phase 1 does not establish their safety. [Phase3-Validation.md](Phase3-Validation.md) owns first-facade qualification; [Phase4-Validation.md](Phase4-Validation.md) records the blocked item investigation, not an implemented item API.

## Evidence behind the rules

Rules I1–I11 consolidate the [accepted design](CarbonLuau_FirstVersion_Design.md), especially sections 3–12, 15, 21–27 and 33, with [Phase 0 evidence](Phase0-Validation.md). Upstream inspection on 2026-09-14 confirms available mechanisms, not a completed CarbonLuau sandbox:

- Pinned [lua.h](../native/third_party/luau/VM/include/lua.h) declares allocation and interrupt hooks, shared across coroutines; its limited interrupt-setter threading allowance does not change I4.
- Pinned [linit.cpp](../native/third_party/luau/VM/src/linit.cpp) shows the library set and sandbox helpers. [luacode.h](../native/third_party/luau/Compiler/include/luacode.h) and [lcode.cpp](../native/third_party/luau/Compiler/src/lcode.cpp) show compiler-owned/STL allocations distinct from the VM allocator.
- Pinned [ldo.cpp](../native/third_party/luau/VM/src/ldo.cpp) implements exception/longjmp paths; upstream [CMake configuration](../native/third_party/luau/CMakeLists.txt) ties `LUAU_EXTERN_C` to `LUA_USE_LONGJMP`. Do not assume the project's C exports require that upstream setting. D1 must account for the selected build mode.
