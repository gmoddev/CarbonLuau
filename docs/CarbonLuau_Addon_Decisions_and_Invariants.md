# CarbonLuau Addon Architecture --- Decisions and Invariants

> Repository review (2026-09-15): imported design proposal, not an amendment to
> [canonical invariants](Invariants.md) or authorization to implement addons.
> The D-A/I-A labels and “Accepted” wording below describe the supplied proposal.
> Shared-VM topology remains research-gated. Validation found additional unresolved
> export, transaction and attribution contracts; see
> [Addon-Design-Validation.md](Addon-Design-Validation.md) before using this as an
> implementation brief. Existing D1/D4/D9–D13 and release qualification remain unchanged.
>
> Source: user-supplied `CarbonLuau_Addon_Decisions_and_Invariants.md`. The pasted
> discussion is background: its earlier per-addon-VM/source-copy recommendation is
> superseded by this proposal's shared-state preference, not adopted alongside it.


**Status:** Design baseline for post-`v0.3.0` addon/provider work.\
**Implementation:** Not authorized by this document. Research-gated
decisions must be proven before implementation commits to them.\
**Baseline:** CarbonLuau `v0.3.0`, release revision
`3046390b5ee668f02c20bcde01d10138f5ca7489`.

## 1. Governing principle

**D-A01 --- Complexity belongs below the public boundary.** Addon
authors should receive conventional Luau semantics. CarbonLuau owns
provider lifetimes, runtime identities, transactions, stale references,
scheduling, recovery, package snapshots, and resource accounting
internally.

**I-A01:** Internal implementation convenience must not impose
surprising public semantics. A dependency that looks stateful/shared
must not secretly create separate authoritative state per consumer
unless that is explicit.

## 2. Runtime topology and failure domains

**D-A02 --- Preferred direction: one shared Luau VM with isolated addon
domains. RESEARCH-GATED.**

``` text
CarbonLuau VM
├── operator root domain
├── addon economy domain
├── addon admin domain
└── addon metrics domain
```

Each domain owns its environment, local modules, lifecycle, tasks,
registrations, dependency bindings, recovery state, diagnostics, and
resource attribution.

**I-A02:** Shared VM does not mean shared globals. Addons retain
separate sandboxed environments and package namespaces.

**I-A03:** Parallel Luau is not part of the initial architecture. VM
access remains serialized on CarbonLuau's owner thread.

**D-A03 --- Addon-local uncatchable cancellation is the gating research
item.** Before shared VM is accepted, prove that timeout remains
uncatchable by Luau, unwinds the offending addon to a trusted host
boundary, invalidates only that addon domain, leaves the VM provably
usable, and leaves unrelated addons/root operational. Cover `pcall`,
`xpcall`, nested protected calls, coroutines, metamethods, modules,
recursive calls, native callbacks, GC/finalization, allocation during
unwind, and cross-addon call stacks.

**I-A04:** Luau code must never catch CarbonLuau's deadline signal and
continue.

**D-A04 --- Per-addon VMs are the fallback.** If safe addon-local
cancellation cannot be proven, use one VM per addon. Public
package/provider APIs must not depend on VM topology.

**I-A05:** Never claim addon-local containment that the actual VM
boundary cannot provide.

## 3. Failure, recovery, and execution ownership

**D-A05 --- Fail local by default.** A broken addon normally affects
only itself and the required-dependency subgraph that becomes
unsatisfied.

**I-A06:** Ordinary callback errors are not automatically addon-fatal if
runtime integrity remains trusted.

**D-A06:** Recovery accounting is per addon/failure domain. A timeout in
A cannot consume B's recovery allowance.

**D-A07 --- Execution ownership follows executing code.** If B calls an
A-owned closure, execution attribution switches to A for A-owned code
and returns to B afterward. CPU/deadline accounting, diagnostics, future
capability authorization, failure ownership, and reliable memory
attribution follow the executing owner.

**I-A07:** Caller identity and execution owner are distinct.

## 4. Memory and resources

**D-A08:** Investigate Luau memory categories for shared-VM per-addon
attribution.

**I-A08:** Attribution is not assumed to equal enforceable hard quota
isolation. Hard per-addon quotas require separate proof.

**I-A09:** Equal trust does not remove resource bounds. Addons remain
bounded in queued work, event admissions, modules/source, registrations,
diagnostics, dependency graph size, and memory/work where enforceable.

## 5. Package identity and packaging

**D-A09 --- Roblox-like package IDs.** Stable IDs are `addonname` or
`creator.addonname`, e.g. `economy`, `admin`, `gmoddev.economy`.
Reverse-DNS IDs are not required.

**I-A10:** Creator prefix is a coordination namespace, not
provider/author authentication.

**D-A10:** Duplicate stable package IDs are rejected in v1. Do not
create dependency identities such as `economy-1`.

**D-A11:** One active version per stable package ID in v1.
Multi-version/multi-instance packages are deferred.

**I-A11:** Runtime lifetime identity is distinct from package identity.
Every activation/replacement gets a fresh opaque lifetime identity;
stale references never retarget.

**D-A12:** Primary format is a packed `.claddon`; a single `.luau`
source is a convenience form. Both normalize into the same internal
package model.

**D-A13:** Accepted package data becomes an immutable CarbonLuau-owned
snapshot.

**I-A12:** Package loading is not filesystem authority.

**I-A13:** Archive processing is bounded/canonical: reject traversal,
absolute paths, duplicate normalized paths, unsafe aliases, malformed
encoding, excessive expansion, encrypted/unsupported entries, and
ambiguity.

**D-A14:** Manifest metadata is authoritative for entrypoint, ID,
version, dependencies, compatibility, and public modules. Directory
naming alone does not define visibility.

## 6. Modules and dependencies

**I-A14:** Local `require("foo")` never falls through into another
addon. Cross-addon access is explicit.

**D-A15:** Support required/optional dependencies with bounded
deterministic version matching, one active version per ID, bounded graph
depth, and explicit cycle handling. CarbonLuau is not a package manager:
no automatic downloads, registry, SAT solver, or remote installation.

**D-A16:** Required dependency loss affects only the required-dependency
subgraph. Optional dependency loss may leave the consumer active.

**I-A15:** Dependency handles bind a specific runtime lifetime. A handle
to A1 never silently targets A2.

**D-A17:** Required dependency restoration may reinitialize affected
dependents in deterministic order. Failed dependents must not
retry-loop.

## 7. Public cross-addon modules

**D-A18 --- Preferred shared-VM semantic: canonical shared module
state.** A public module owned by A executes/caches canonically in A's
module domain. Consumers access the same A-owned state.

``` text
economy/api
    ↓
one Economy state
    ├── admin
    └── metrics
```

**I-A16:** Module ownership remains A's ownership. Calls into A-owned
code transfer execution attribution to A.

**D-A19:** Public export access validates the owning addon lifetime. Old
A1 dependency/export handles become stale after A1 retires.

**I-A17:** A1 exports never silently become A2 exports.

**Fallback invariant:** If per-addon VMs become necessary, cross-addon
live Luau values cannot be pretended to be shared. Module semantics must
be deliberately redesigned and documented.

## 8. Provider ownership and registration

**D-A20:** A Carbon provider lifetime owns every addon it registers.

**I-A18:** Provider unload unloads all provider-owned addons and
invalidates their queued work, registrations, dependencies, and future
capability handles.

**D-A21:** Dynamic registration is supported and serialized through
CarbonLuau's owner-thread lifecycle.

**D-A22:** Activation is transactional. Candidate addons are not
externally visible until initialization succeeds.

**D-A23:** Dynamic unregister and transactional replacement are
supported: failed replacement preserves A1; successful replacement
commits A2 then retires A1.

**I-A19:** Provider authority must not be established solely from a
caller-supplied provider-name string. Bind it to the strongest supported
actual Carbon plugin/provider lifetime mechanism.

## 9. Operator root

**D-A24:** Preserve existing operator-root behavior: `carbonluau.reload`
remains root reload; addon failure does not logically imply root
failure; root failure does not logically imply addon failure; addons
cannot depend on root; root consumption of public addon functionality
may be added later.

If shared-VM cancellation cannot preserve root safety after addon
timeout, fallback topology must.

## 10. Scheduler, events, Players, Commands, Signals

**D-A25:** Each addon owns its queued work/event admissions while a
global CarbonLuau arbiter enforces total host work and fairness.

**I-A20:** One addon must not permanently starve unrelated addons.

**I-A21:** Each addon cannot receive the entire global frame budget
independently.

**D-A26:** Rust/Carbon events fan out through bounded addon-owned
intake; hooks do not directly reenter arbitrary addon Luau.
Saturation/failure in A should not prevent B/C from receiving their
event where feasible.

**D-A27:** Existing connection-specific Player lifetime semantics remain
authoritative. Same-account reconnect never reactivates an old proxy; no
raw `BasePlayer` crosses into Luau.

**D-A28:** Commands and Signals become addon-owned resources. Retiring A
removes only A's registrations.

**I-A22:** Command collisions are deterministic. Do not silently rename
conflicting commands.

## 11. Trust and sandbox

**D-A29:** Normal addons initially use one equal `FullTrusted` scripting
exposure profile.

**I-A23:** Equal trust does not mean shared ownership, private-module
visibility, stale-handle reuse, arbitrary provider capability access, or
raw host access.

**D-A30:** Future restricted exposure is architecturally reserved but
CLOSED until real defined/tested semantics exist.

**I-A24:** Addon support must not accidentally expose arbitrary
filesystem, sockets, HTTP, process execution, reflection, Carbon
objects, Unity/Rust objects, native loading, or unrestricted
console/hooks.

Per-addon/domain isolation is primarily a reliability boundary, not
OS/process isolation.

## 12. Custom provider capabilities

**D-A31 --- Design now; implementation CLOSED for the first addon
release.**

Future providers may expose narrow custom host APIs, but basic addon
support must not require them.

Future capabilities must use provider-bound identity/lifetimes, explicit
method schemas, bounded value types, host-side authorization,
stale-handle rejection, dependency/visibility policy, provisional-effect
policy, and owner-thread/non-reentrant execution.

**I-A25:** Never implement an arbitrary reflection/dynamic
managed-object bridge.

**I-A26:** Capability authorization is enforced at the host boundary,
not by mutable Luau wrappers.

**D-A32:** Initial future capability execution should be synchronous and
bounded; async semantics are deferred. CarbonLuau cannot hard-preempt
arbitrary C# provider work.

## 13. Provisional effects and teardown

**I-A27:** Existing provisional host-effect policy applies independently
to addon candidates. Candidates may perform allowed read-only
operations, load modules/dependencies, stage registrations, and queue
deferred work. Irreversible host mutations remain prohibited until
commit unless a later explicit contract changes that rule.

**I-A28 --- Teardown is deterministic and ownership-driven.** Teardown
must stop intake, invalidate lifetime, prevent/cancel new queued work,
remove Commands/Signals/exports, stale dependency/capability handles,
release module/runtime references, destroy/recover the domain according
to selected topology, and release package/registration ownership when
appropriate.

Provider unload applies this to all provider-owned addons. CarbonLuau
unload stops provider registration/event intake first and unloads the
native runtime only after all runtime resources are gone.

## 14. Developer-experience invariants

**I-A29:** Addon authors should not need to understand VM topology.

**I-A30:** Addon authors should not manually manage runtime tokens,
scheduler queues, provider lifetimes, or transaction objects.

**I-A31:** Stateful public dependencies should behave like shared
stateful Luau dependencies under the preferred architecture.

**I-A32:** Public APIs must distinguish dependency absence, stale
lifetime, authorization/failure, and programming errors rather than
returning ambiguous `nil` for everything.

**I-A33:** Simple addons remain simple. A trivial provider can register
one Luau source without learning the complete archive/dependency system.

## 15. First implementation boundary

After the shared-VM cancellation research is resolved, initial addon
support should focus on:

-   provider registration/discovery;
-   package IDs;
-   `.claddon` ingestion and single-file normalization;
-   immutable package snapshots;
-   addon lifecycle/transactionality;
-   local/private and explicit public modules;
-   required/optional dependencies;
-   stable dependency lifetimes;
-   addon-owned tasks/Commands/Signals;
-   scheduler fairness;
-   dynamic unregister/replacement;
-   provider unload;
-   bounded status/diagnostics.

Explicitly deferred:

-   arbitrary provider C# capabilities;
-   restricted exposure profiles;
-   async provider APIs;
-   multiple simultaneous versions;
-   multi-instance packages;
-   remote registry/downloads;
-   SAT dependency solving;
-   raw cross-addon objects;
-   client Luau;
-   package filesystem mounting;
-   script shutdown hooks unless later justified.

## 16. Research gates

### R-A01 --- Shared-VM cancellation

Prove or reject safe uncatchable addon-local cancellation in the pinned
Luau VM.

**Blocks final VM topology:** Yes.

### R-A02 --- Shared-VM memory attribution/quota feasibility

Measure memory categories and determine what can be attributed/enforced
per addon.

**Blocks shared-VM hard per-addon memory claims:** Yes.\
**Blocks package/provider work:** No.

### R-A03 --- Scheduler defaults

Measure fair global/per-addon scheduling under representative addon
counts before freezing numeric public defaults.

### R-A04 --- Provider interoperability

Confirm the strongest supported Carbon plugin-to-plugin provider
identity/lifecycle mechanism before freezing the public provider API.

## 17. Qualification invariants

Before addon support is advertised as qualified, validation must cover:

-   representative addon scaling;
-   compile/init failure locality;
-   timeout locality;
-   memory/resource failure locality;
-   queue flooding/fairness;
-   required/optional dependency failure;
-   dependency restoration/replacement;
-   duplicate IDs and cycles;
-   package traversal/collision/compression abuse;
-   private/public module enforcement;
-   shared module state semantics;
-   stale dependency/export handles;
-   provider unload/reload;
-   CarbonLuau reload;
-   command collisions;
-   repeated register/replace/unregister;
-   deterministic teardown;
-   Windows/Linux live Carbon;
-   sanitizer/fault coverage for affected native behavior.

If shared-VM cancellation cannot meet fail-local requirements, per-addon
VMs become the fallback and cross-addon module semantics must be
re-reviewed before implementation continues.

## 18. Version identities

Keep independent:

| Identity | Purpose |
|---|---|
| CarbonLuau package version | Shipped plugin/release |
| Scripting API version | Luau author-facing API |
| Native ABI | Managed/native bridge |
| Addon package schema | `.claddon`/manifest contract |
| Provider API version | Carbon plugin → CarbonLuau integration |

Addon support is experimental initially. Experimental status does not
permit silent changes to lifetime, ownership, visibility, dependency, or
authorization semantics.

## 19. Decision summary

### Accepted

-   Simple IDs: `addonname` or `creator.addonname`.
-   Duplicate stable IDs rejected.
-   Provider owns all registered addons; provider unload unloads them.
-   Dynamic registration/unregistration/replacement.
-   Transactional activation.
-   Packed archive primary, single-file convenience.
-   Explicit private/public modules.
-   Required/optional dependencies.
-   Fail-local behavior.
-   Equal trusted exposure initially.
-   Restricted exposure reserved but closed.
-   Provider capabilities designed but closed initially.
-   No raw host/.NET/Rust/Unity objects in Luau.
-   Developer-facing semantics remain simple.

### Revised from the initial isolated-VM design

Preferred direction is now **one shared Luau VM with isolated addon
domains and shared stateful public-module semantics**, because per-addon
VM source-copy semantics create surprising duplicate authoritative
state.

This preference is **not final until addon-local uncatchable
cancellation is proven safe**.

### Fallback

If shared-VM cancellation cannot safely isolate an addon without
retiring the entire VM, use per-addon VMs and explicitly redesign
cross-addon module semantics rather than hiding the difference.

## 20. Core invariants

``` text
1. Public semantics are simple; internal complexity stays internal.
2. Stable package identity is not runtime lifetime identity.
3. Stale identities never silently retarget replacements.
4. Addons fail locally wherever the actual runtime boundary permits it.
5. A timeout cannot be catchable by addon Luau.
6. Shared VM never implies shared globals/private visibility.
7. Public stateful dependencies have one authoritative state under the preferred model.
8. Execution cost/failure is attributed to the addon whose code executes.
9. Provider lifetime owns every addon it registered.
10. Candidate activation/replacement is transactional.
11. Required dependency failure affects only its dependency subgraph.
12. Optional dependency failure does not automatically kill the consumer.
13. One addon cannot monopolize global CarbonLuau scheduling/resources.
14. Raw Carbon/Rust/Unity/.NET/native objects never cross into Luau.
15. Equal trust is not equal ownership or visibility.
16. Provider capabilities remain closed until separately qualified.
17. The operator root preserves its existing user-facing semantics.
18. If evidence contradicts these invariants, stop and resolve architecture rather than silently weakening them.
```


---

## 21. Package-ID and runtime identity details

**D-A33 — Keep naming intentionally simple.** Stable IDs use `addonname` or `creator.addonname`. IDs should be canonical lowercase ASCII with bounded segments; `_` and `-` may be permitted internally. Noncanonical uppercase or malformed IDs are rejected rather than silently normalized.

**I-A34:** Unscoped `economy` remains exactly `economy`; it is not secretly rewritten to `<provider>.economy`.

**I-A35:** Creator prefix is namespace coordination only, never authentication.

**D-A34:** Reserve `carbonluau` / `carbonluau.*` for project-owned packages if an internal namespace is needed.

**D-A35 — Runtime identity is separate.**

```text
economy / lifetime 184
    ↓ retires
economy / lifetime 213
```

**I-A36:** References bound to lifetime 184 remain stale forever. Reusing package ID, version, provider name, or source hash never revives an old runtime identity.

## 22. Addon context and dependency lookup

A likely minimal Luau context is:

```lua
addon.Id
addon.Name
addon.Version

addon:GetDependency("economy")
addon:GetOptionalDependency("metrics")
```

Exact names remain subject to implementation review.

**I-A37:** Do not expose provider objects, VM handles, runtime tokens, package snapshots, scheduler internals, or native handles.

**D-A36:** Local modules continue to use normal `require`; cross-addon access remains explicit.

**D-A37 — Absence, stale lifetime, and misuse are distinct.**

```lua
addon:GetOptionalDependency("metrics")
    -- nil when no compatible optional dependency was bound

addon:GetDependency("economy")
    -- handle when valid
    -- controlled error if declared required dependency is unavailable

oldDependency:Require("api")
    -- controlled stale-dependency error
```

**I-A38:** Do not return `nil` for every malformed request, stale reference, authorization failure, or host failure.

**D-A38:** V1 dependency lookup normally requires a manifest-declared dependency; arbitrary global package discovery is not part of v1.

## 23. Dependency versions and graph

**D-A39:** V1 package versions use `MAJOR.MINOR.PATCH`.

Support a deliberately small constraint grammar:

```text
1.2.3
>=1.2.0 <2.0.0
```

Simple comparator conjunctions may be supported; defer `^`, `~`, wildcards, OR expressions, and npm-style grammar.

**I-A39:** Resolution is deterministic and never depends on generated suffixes or registration order.

**D-A40:** Required dependency cycles block the affected strongly connected component. Unrelated addons remain active.

**I-A40:** Graph node/edge/depth counts are bounded.

**D-A41:** Dynamic graph changes evaluate the affected subgraph; global restart must not become public semantics.

## 24. Dependency loss/restoration

**D-A42:** Required dependency loss invalidates dependent runtime assumptions.

```text
A unavailable
B requires A  -> blocked/retired
C requires B  -> blocked/retired
D unrelated   -> active
```

**D-A43:** Compatible restoration may reactivate affected dependents in deterministic topological order.

**I-A41:** Failed dependents do not enter automatic retry loops.

**D-A44:** Optional dependency loss may leave the consumer active, but its old dependency handle becomes stale.

**I-A42:** A replacement optional dependency never silently appears inside an already-running consumer lifetime.


## 25. Shared public module details

**D-A45 — Public modules are owned by the exporting addon.**

Preferred shared-VM behavior:

```text
Addon A public module
    ↓ executes once in A domain
canonical A-owned result
    ├── consumer B
    └── consumer C
```

**I-A43:** Stateful public modules intentionally share one authoritative state.

**I-A44:** Calls into A-owned exported code switch execution ownership to A.

**I-A45:** A public module may use A-private helper modules internally without exposing those helpers directly.

**D-A46 — Prefer lifetime-validating dependency/export handles.**

Conceptually:

```text
DependencyHandle(A1)
    ↓
PublicModuleHandle(A1, "api")
    ↓
validate A1 lifetime
    ↓
invoke A-owned result
```

The final Luau representation should preserve normal ergonomics and hide lifetime machinery.

**I-A46:** A1 export handles never mutate into A2 handles.

## 26. Candidate manifest and single-file normalization

A candidate schema-1 package may resemble:

```json
{
  "schema": 1,
  "id": "gmoddev.economy",
  "name": "Economy",
  "version": "1.2.0",
  "entrypoint": "init.luau",
  "compatibility": {
    "scriptingApi": "0.3"
  },
  "publicModules": ["api/formatting"],
  "dependencies": {
    "required": {
      "gmoddev.core": ">=1.0.0 <2.0.0"
    },
    "optional": {
      "metrics": "1.0.0"
    }
  }
}
```

Field names are not frozen, but the semantics are.

**D-A47:** Package schema, provider API, scripting API, CarbonLuau package version, and native ABI remain separate compatibility identities.

**D-A48:** Schema v1 should favor strict parsing so misspelled semantic fields do not silently disappear.

**D-A49:** Single-file registration normalizes into the same package/lifecycle model. A trivial provider should be able to supply ID/version/source without building an archive.

**I-A47:** Single-file addons do not get weaker/different lifecycle, security, scheduling, or dependency rules.

## 27. Provider API principles

**D-A50:** Provider integration uses supported Carbon interoperability and should avoid requiring providers to reference CarbonLuau private implementation types.

Conceptually required operations:

```text
query provider API compatibility
register archive
register single source
query status
replace registration
unregister
```

**D-A51:** Registration returns an opaque handle/token bound to CarbonLuau instance + provider lifetime + addon registration.

**I-A48:** Handles become stale across provider or CarbonLuau lifetime replacement.

**D-A52:** Registration mutations remain owner-thread serialized unless a separately qualified thread-safe ingress is designed.

**D-A53:** Provider unload/reload creates a new provider lifetime.

**D-A54:** CarbonLuau reload while provider plugins remain loaded is a supported lifecycle case; providers may re-register into the new CarbonLuau instance, but old tokens remain stale.

**I-A49:** CarbonLuau cleans provider-owned state even if a provider fails to explicitly unregister.

## 28. Addon lifecycle state model

Recommended externally visible states:

```text
Registered
Blocked
Initializing
Active
Failed
Stopping
Stopped
```

Use `State + Reason`, with reasons such as:

```text
DependencyMissing
DependencyVersionMismatch
DependencyCycle
PackageInvalid
CompileError
InitializationError
Timeout
MemoryLimit
ProviderUnavailable
ExplicitUnregister
```

**I-A50:** Candidate exports, Commands, Signals, and scheduled work are not externally visible before commit.

**D-A55:** Replacement may leave A1 externally Active while internal A2 initializes.

Recommended activation:

```text
validate package
→ reserve ID
→ resolve dependencies
→ prepare domain
→ install sandbox/facade/addon context
→ run entrypoint/modules
→ stage registrations/exports
→ commit
→ Active
```

**D-A56:** Entrypoint return value has no special v1 meaning.

**D-A57:** Script-visible shutdown hooks are deferred initially; host correctness must never depend on Luau cleanup succeeding.


## 29. Scheduler and event-fanout detail

**D-A58:** Scheduling uses per-addon ownership plus a global arbiter.

Conceptually:

```text
global drain
→ snapshot runnable domains
→ persistent rotating start
→ bounded round-robin slice per domain
→ stop on global budget or no work
```

**I-A51:** A continuously-ready addon cannot take unlimited consecutive turns while other continuously-ready addons wait.

**I-A52:** Work created during a drain remains subject to later-drain semantics; recursive scheduling cannot create unbounded same-drain execution.

**D-A59:** Host event admission is independent per addon:

```text
PlayerAdded
├── A admitted
├── B full -> B rejects
└── C admitted
```

**I-A53:** B saturation does not make A/C delivery fail.

**D-A60:** Shared managed host snapshots may be reused internally, while runtime-local Player proxies remain separately owned/lifetime-safe.

## 30. Future provider capabilities — reserved only

Capabilities remain CLOSED for initial addon implementation.

Future architecture must preserve:

- provider lifetime;
- package/addon identity;
- capability identity/version;
- consumer identity;
- visibility;
- explicit method schema;
- effect classification;
- stale lifetime.

Candidate visibility classes:

```text
Private
Dependency
Public
```

Candidate initial transport values:

```text
nil
boolean
finite number
bounded integer
bounded UTF-8 string
Player identity/proxy
```

Never arbitrary managed/Rust/Unity/native objects.

Candidate effect classes:

```text
ReadOnly
Mutation
```

**I-A54:** Provisional addons cannot invoke future Mutation capabilities before commit.

**I-A55:** Provider exceptions become bounded controlled errors.

**I-A56:** CarbonLuau does not claim hard preemption of arbitrary provider C#.

## 31. Explicit v1 non-goals

Addon v1 is not:

- a remote package manager or marketplace;
- npm/Cargo;
- an automatic downloader;
- a hostile multi-tenant sandbox;
- Parallel Luau;
- a raw Carbon/Rust reflection bridge;
- a filesystem mount;
- a multiple-version resolver;
- a cross-VM RPC framework;
- an async provider-capability system.

## 32. Implementation sequencing

### Phase A — Cancellation/runtime-domain research
Prove shared-VM addon-local cancellation and memory attribution feasibility. No public addon API.

### Phase B — Package/provider foundation
Implement provider lifetime, registration ownership, stable IDs, package parsing, immutable snapshots, and single-file normalization.

### Phase C — Addon lifecycle and transactions
Implement domain creation, initialization, commit, failure, unregister, replacement, and provider teardown.

### Phase D — Dependency graph
Implement required/optional dependencies, version constraints, cycles, stale lifetime identities, and restoration.

### Phase E — Shared public modules
Implement canonical A-owned public module state, explicit visibility, dependency/export handles, execution-owner switching, and stale export behavior.

### Phase F — Multi-addon scheduler/facade
Implement per-addon queues, global fairness, event fanout, Player proxies, Commands/Signals ownership, and collision handling.

### Phase G — Qualification/public docs
Run scale, failure, teardown, Windows/Linux Carbon, sanitizer/fault, compatibility, and author-experience validation.

### Later
- operator-root dependency consumption;
- provider capabilities;
- restricted exposure profiles.

## 33. Author-experience acceptance test

A Luau/Roblox developer should be able to understand the common case without learning CarbonLuau internals:

```lua
local Economy = addon:GetDependency("economy")
local EconomyApi = Economy:Require("api")

local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    local Balance = EconomyApi:GetBalance(Player.UserId)
    Player:SendMessage(`Balance: {Balance}`)
end)
```

They should not need to understand:

```text
VM topology
execution-owner stacks
runtime lifetime tokens
package snapshots
scheduler arbitration
provider lifetime
native handles
transaction generations
```

**I-A57:** If common addon code requires understanding those internals, the public abstraction is too complex and should be redesigned.

## 34. Final runtime-topology gate

Before implementation selects runtime topology, answer:

> Can CarbonLuau safely cancel and retire one addon execution domain inside a shared Luau VM without allowing `pcall`, coroutines, nested Luau execution, or other language mechanisms to intercept/bypass cancellation, while leaving the shared VM provably consistent?

### If YES

Use:

```text
one VM
+ isolated addon domains
+ canonical shared stateful public modules
+ execution-owner attribution
```

### If NO

Use:

```text
per-addon VMs
```

but stop and explicitly redesign cross-addon stateful module semantics before proceeding. Do not silently fall back to source-copy semantics that look shared but create duplicate authoritative state.

**I-A58:** The result of this research gate must be recorded as a durable architecture decision before addon implementation proceeds beyond topology-neutral package/provider work.
