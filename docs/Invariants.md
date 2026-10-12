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

## I12 — Trusted in-process host interference

CarbonLuau preserves its API semantics against its own implementation, the
qualified supported Rust/Carbon host behavior, normal documented callback
outcomes, and host uncertainty covered by existing failure semantics.

**External interference** occurs when another trusted in-process component
mutates host state relevant to an admitted CarbonLuau operation
in a way that invalidates the operation's qualified premises. Examples include
changes to Item.amount, effective stack limits, target slot/container contents,
Item parent/state, inventory contents or operation-relevant world state during
an inventory callback. This definition does not require malicious intent.
Equally privileged trusted components can have equivalent host authority;
CarbonLuau does not promise transactional isolation from such mutation. This is
a semantic isolation boundary, not a security boundary. It applies generally
to host-backed gameplay operations, including future operations whose supported
Rust/Carbon path synchronously invokes trusted in-process code.

The boundary does **not** excuse unsafe API selection, incorrect PREPARE,
bounds or adapter arguments; prohibited generic GiveItem or automatic slots;
incorrect swap/ignoreStackLimit flags; oversized planned chunks; intentional
drop fallback; ordinary vanilla behavior on the qualified path; documented
non-mutating callbacks or normal acceptance/rejection; CarbonLuau-controlled
callbacks/effects; resource-accounting or VERIFY bugs; or failures already
classified as indeterminate by D13. These remain CarbonLuau responsibilities.
An unexplained failure must never be attributed to external interference
without evidence of actual operation-relevant mutation outside CarbonLuau's
control. An indeterminate result alone is not that evidence.

Observed interference does not waive verification: claim success only if the
existing operation-specific success predicate still proves it. If the effects
leave the result uncertain, use the existing controlled indeterminate failure
where appropriate, never claim rollback, never conceal prohibited effects, and
retain bounded diagnostics where useful. No new outcome, recovery or replay
mechanism is introduced. I1/I3/I4/I8/I9/I10 and D7/D10/D11 retain ownership,
non-reentrancy, publication, exact-Player lifetime and failure authority.

For host observations, distinguish three guarantees. **Safety and lifetime**
remain mandatory: exact admission/revalidation, no stale-reference retargeting,
no use-after-free or raw-host escape, and bounded CarbonLuau work/allocation.
**CarbonLuau-owned ordering** follows I4's serialized owner-thread operations
and their existing publication/admission rules; an async traversal's turns and
CarbonLuau-originated effects must have a defined order, not race each other.
**External observation** is not transactionally isolated: Rust, Unity, Carbon,
Oxide, physics/jobs and other trusted in-process writers may race an observation,
including between traversal turns. Discovery need not freeze the world or prove
universal writer serialization to observe a candidate.

This observational boundary is not a native-memory access permission. A reader
still needs a qualified storage-lifetime/access premise and a hard work bound;
checking after an unsafe read cannot repair an earlier invalid dereference, and
a deadline cannot interrupt an unbounded native wait already entered. Ordinary
qualified host behavior remains CarbonLuau's responsibility. An operation's
position-observation semantics must be explicit; this boundary does not silently
substitute local coordinates, arbitrarily old caches, an incomplete candidate
set, or failed lifetime validation. It creates no new inventory outcome or replay
rule and does not weaken Entity-1A's completed-Spawn/continuity gates.

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
| D12 — resolved experimental identities | Existing addon/GUI/Player surfaces retain their recorded introduction versions through `0.4.0-experimental`. Persistence Foundation 1 is assigned `0.5.0-experimental` by the 2026-09-23 user decision, without retroactive 0.4 availability. Package, API, ABI, provider protocol, schema and Luau identities remain separate. | Assignment is not qualification or publication; requalify affected behavior and preserve historical availability |
| D13 — resolved inventory ownership/failure model and I12 scope; TakeItem/GiveItem implemented | Bounded mutation-free PREPARE, explicit COMMIT, physical VERIFY and pre-COMMIT false / verified true / post-COMMIT indeterminate remain unchanged. Returned resources remain under temporary responsibility; inaccessible resources never returned by a supported failing host call remain in the host failure domain. [Reassessment](InventoryOwnershipFailureReassessment.md) retains the rationale. [M2](InventoryMutationM2Validation.md) retains G2–G5 evidence; its G1 conclusion is superseded. [Player-1F-B](qualification/PlayerInteraction.md#playerinteractionfoundation1fb-validation) records supported-host G1 requalification and GiveItem InventoryOnly; [Player-1F-A](qualification/PlayerInteraction.md#playerinteractionfoundation1fa) retains qualified TakeItem. | Requalify host, bounds or adapter changes; preserve per-target limits |
| D14 — resolved experimental addon package/dependency/provider lifecycle | Stable package identity, lifecycle states, exact dependency bindings, provider ownership, immutable snapshots and bounded parser/registry limits are specified below and qualified by Foundation E. | Requalify lifecycle, parser, limits or protocol changes before expanding support |
| D15 - resolved GUI Foundation 1 retained presentation model; qualified for experimental public release through 1G | The retained GUI authority, ownership, presentation, interaction, publication, reconciliation, recovery and scope rules are specified below. [GuiFoundation1.md](GuiFoundation1.md) owns supporting rationale and implementation guidance; Foundations 1A through 1F record implementation/runtime evidence and [GuiFoundation1G.md](qualification/Gui1.md#guifoundation1g) records public documentation, examples, final available qualification and the identity decision. Authenticated-client visual, cursor, click-receipt and reconciliation observations remain explicitly unqualified but no longer gate the experimental identity. | Requalify affected GUI behavior; do not claim unobserved client behavior without authenticated-client evidence |
| D16 - resolved GUI Foundation 2 architecture; implemented subset qualified for experimental public release through 2F | Foundation 2 additively specializes D15 as specified below. GUI-2A/2B/2C/2E/2F implement and qualify deterministic layout, typed images and retained scrolling under the existing `0.4.0-experimental` identity. `TextBox` and typed text ingress remain deferred and unimplemented after the exact text-preservation gate failed. [GuiFoundation2.md](GuiFoundation2.md) retains the complete supporting design. | Requalify affected behavior; reconsider TextBox only with a bounded opaque text-preserving host transport |
| D17 - resolved GUI Foundation 3 architecture; implemented and release-candidate qualified through 3E | Foundation 3 additively specializes D15/D16 with deterministic grids, bounded Frame clipping, immutable project-owned fonts and one-way per-Presentation scroll effects as specified below. [GuiFoundation3.md](GuiFoundation3.md) retains the complete supporting design; [GuiFoundation3A.md](qualification/Gui3.md#guifoundation3a), [GuiFoundation3B.md](qualification/Gui3.md#guifoundation3b), [GuiFoundation3C.md](qualification/Gui3.md#guifoundation3c), [GuiFoundation3D.md](qualification/Gui3.md#guifoundation3d) and [GuiFoundation3E.md](qualification/Gui3.md#guifoundation3e) record implementation and qualification. GUI-3E assigns the additive surface to the still-unreleased package `0.4.0` and scripting API `0.4.0-experimental`. Authenticated-client clipping, font and scroll gates and Windows native/local qualification remain explicit. | Requalify affected behavior; do not claim unobserved client or deferred Windows-native behavior |
| D18 — resolved Player Interaction Foundation 1 architecture plus inventory-mutation amendment; TakeItem/GiveItem implemented | Foundation 1 additively approves immutable `Vector3`; exact-connection Player position, health and bounded physical inventory observation; read-only item existence; committed-only teleport; and scoped `GiveItem`/`TakeItem` under revised D13. [PlayerInteractionFoundation1.md](PlayerInteractionFoundation1.md) retains the original rationale, [InventoryOwnershipFailureReassessment.md](InventoryOwnershipFailureReassessment.md) owns the mutation amendment, Player-1A through Player-1D record read/spatial implementations, [PlayerInteractionFoundation1FA.md](qualification/PlayerInteraction.md#playerinteractionfoundation1fa) records TakeItem and [PlayerInteractionFoundation1FB-Validation.md](qualification/PlayerInteraction.md#playerinteractionfoundation1fb-validation) records GiveItem InventoryOnly. Authenticated-client Teleport behavior and later closure work remain unqualified or unimplemented as documented. | Implement only through scoped Player-1A–1F and Inventory-M phases; qualify exact host adapters, bounds, lifetime, publication, mutation gates and applicable client behavior before support |
| D19 — accepted official editor tooling baseline | Shared semantics, API metadata, tooling host and preview plans belong to CarbonLuau; editor integration belongs to carbonluau-vscode. Detailed contracts are in [ToolingBaseline.md](ToolingBaseline.md). Adoption is not Foundation A completion. | Qualify each tooling phase; LSP pairing, platform containment and distribution administration remain implementation gates |
| D20 — exact-host Entity Foundation 1 read-only public-development surface | A continuously installed, exact-host full-Spawn observer plus successful-startup reconciliation establishes private spawn epochs and no-retargeting Entity lifetime tokens. [Entity-1A qualification](WorldEntityFoundation1A-Validation.md) owns the narrow host/deployment envelope; [Entity-1B validation](WorldEntityFoundation1B-Validation.md) owns keyed runtime behavior; [Entity-1C](WorldEntityFoundation1C.md) owns combined public closure, metadata and release planning. Additive [B1](GameplayB1-Validation.md) completed-Spawn and [B3](GameplayB3-Validation.md) successful-removal observations retain separate proof/gates. Older negative investigations remain historical evidence. | Requalify host/patch drift; original Entity API `0.6.0-experimental` and B1/B3 `0.6.5-experimental` introductions imply no package release |
| D21 — resolved Persistence Foundation 1 architecture; private 1A PASS; public API assigned 0.5 | Private root/addon DataStoreService, callback-based GetAsync/SetAsync/RemoveAsync, bounded snapshots and durable per-key transactions in a private SQLite worker. [PersistenceFoundation1.md](PersistenceFoundation1.md) owns signatures, limits, backend contract and qualification gates. The approved PERSIST/EXTRA and physical-budget amendments preserve hard logical/backend extent bounds; 1,280 MiB is an operational safety budget. Historical negative evidence is preserved. | [Private 1A qualification](PersistenceFoundation1A.md) and [public 1B](qualification/Persistence1B.md#persistencefoundation1b) retain their scoped evidence. [1C](PersistenceFoundation1C.md) owns combined closure and the exact final verdict. D12 experimental availability is not package publication approval. |
| D22 — resolved Persistence Foundation 2 bounded derived-index Query architecture; 2A/2B PASS, public 2C implemented | DataStore:Query uses required Field plus structured Equals/Min/Max/order with automatic CarbonLuau-owned derived indexes. Authors manage no schema, version, migration or index lifecycle; optional Indexes string-list hints only request proactive preparation/retention. Online bounded preparation keeps ordinary persistence available. [PersistenceFoundation2.md](PersistenceFoundation2.md) owns exact API, bounds and qualification gates. | Implement only through Persistence-2A–2D. Public Query is assigned to unreleased `0.5.0-experimental`; package identity is unchanged. [2C evidence](PersistenceFoundation2C.md) owns its qualification, distinct from 2D combined closure. |
| D23 — resolved Gameplay A exact-host Player lifecycle observations | Adds only Players.PlayerDied(Player, PlayerDeathContext) and PlayerSpawned(Player, PlayerSpawnContext) through the existing owned Signal path. Completed terminal death and eligible initial/full respawn require the exact host completion fences, original connection and bounded capture/queue/fanout. [Gameplay A evidence](GameplayFoundationA-Validation.md) owns the tested tuple, context fields and qualification scope. | Development API `0.6.5-experimental`; no Entity/inventory signals, policies, generic hooks or release/tag authority. D7/D10/D11 remain authoritative. |

### Canonical detail for resolved decisions

The following pages are the delegated normative detail of this register, not
independent proposals. Existing I/D anchors remain stable for references.

#### D19 — official editor tooling

Read the [canonical detail](invariants/Tooling.md#d19--official-editor-tooling).

#### D20 — World/Entity Foundation 1

Read the [canonical detail](invariants/WorldEntity.md#d20--worldentity-foundation-1).

#### D21 — Persistence Foundation 1

Read the [canonical detail](invariants/Persistence.md#d21--persistence-foundation-1).

#### D22 — Persistence Foundation 2 — bounded derived indexes and Query

Read the [canonical detail](invariants/Persistence.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query).

#### D2 — limits in addon-capable operation

Read the [canonical detail](invariants/Runtime.md#d2--limits-in-addon-capable-operation).

#### D4 — shared-VM module semantics

Read the [canonical detail](invariants/Runtime.md#d4--shared-vm-module-semantics).

#### D5 — package/module resolution

Read the [canonical detail](invariants/Runtime.md#d5--packagemodule-resolution).

#### D7 — candidate and module publication

Read the [canonical detail](invariants/Runtime.md#d7--candidate-and-module-publication).

#### D9 — global recovery

Read the [canonical detail](invariants/Runtime.md#d9--global-recovery).

#### D10 — admitted-operation and provisional-effect model

Read the [canonical detail](invariants/Runtime.md#d10--admitted-operation-and-provisional-effect-model).

#### D11 — player identity and domain lifetime

Read the [canonical detail](invariants/Runtime.md#d11--player-identity-and-domain-lifetime).

#### D12 — scripting and protocol identity

Read the [canonical detail](invariants/Runtime.md#d12--scripting-and-protocol-identity).

#### D13 — inventory ownership and failure model

Read the [canonical detail](invariants/Runtime.md#d13--inventory-ownership-and-failure-model).

#### D14 — addon package, dependency and provider lifecycle

Read the [canonical detail](invariants/Runtime.md#d14--addon-package-dependency-and-provider-lifecycle).

#### D15 — GUI Foundation 1 retained presentation model

Read the [canonical detail](invariants/Gui.md#d15--gui-foundation-1-retained-presentation-model).

#### D16 — GUI Foundation 2 deterministic layout and rich controls

Read the [canonical detail](invariants/Gui.md#d16--gui-foundation-2-deterministic-layout-and-rich-controls).

#### D17 - GUI Foundation 3 deterministic grids, clipping, fonts and Presentation scroll intent

Read the [canonical detail](invariants/Gui.md#d17---gui-foundation-3-deterministic-grids-clipping-fonts-and-presentation-scroll-intent).

#### D18 — Player Interaction Foundation 1

Read the [canonical detail](invariants/Player.md#d18--player-interaction-foundation-1).

#### D23 — Gameplay A Player lifecycle Signals

Read the [canonical detail](invariants/Gameplay.md#d23--gameplay-a-player-lifecycle-signals).

## Evidence behind the rules

Rules I1–I11 consolidate the [accepted design](CarbonLuau_FirstVersion_Design.md), especially sections 3–12, 15, 21–27 and 33, with [Phase 0 evidence](Phase0-Validation.md). Upstream inspection on 2026-09-14 confirms available mechanisms, not a completed CarbonLuau sandbox:

- Pinned [lua.h](../native/third_party/luau/VM/include/lua.h) declares allocation and interrupt hooks, shared across coroutines; its limited interrupt-setter threading allowance does not change I4.
- Pinned [linit.cpp](../native/third_party/luau/VM/src/linit.cpp) shows the library set and sandbox helpers. [luacode.h](../native/third_party/luau/Compiler/include/luacode.h) and [lcode.cpp](../native/third_party/luau/Compiler/src/lcode.cpp) show compiler-owned/STL allocations distinct from the VM allocator.
- Pinned [ldo.cpp](../native/third_party/luau/VM/src/ldo.cpp) implements exception/longjmp paths; upstream [CMake configuration](../native/third_party/luau/CMakeLists.txt) ties `LUAU_EXTERN_C` to `LUA_USE_LONGJMP`. Do not assume the project's C exports require that upstream setting. D1 must account for the selected build mode.
