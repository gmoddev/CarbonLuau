# Runtime: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D2 — limits in addon-capable operation

Defaults remain 64 MiB/3 ms; clamps remain 16..256 MiB and 1..100 ms. Source remains 64 KiB, loaded bytecode 1 MiB, log buffer 4 KiB, with the existing native registry bound of 32 live VMs and one host thread per VM. Compiler/bridge/managed/source-snapshot memory remains outside the VM heap cap. Foundation G adds a fixed one-second compiler wall deadline and a production 256 MiB worker-process memory limit; neither is a whole-process cap.

In addon-capable operation the hard Luau allocation boundary remains **one VM-wide heap cap**, not a per-addon quota. Memory categories or equivalent accounting may be used for diagnostics but do not establish hard retained-memory isolation or guaranteed per-addon reclamation. The 32-live-VM registry bound limits VM instances, not the number of domains inside one shared VM.

Foundation E requalified the existing 64 MiB default and 16..256 MiB clamp with root-only, 1, 10, 50 and 100-addon configurations, shared-heap exhaustion and live Carbon operation. At 100 representative addons, measured VM allocation remained under 2 MiB on both Windows and Linux workers; fifteen retained 4 MiB buffers reached a controlled global rejection near the configured cap while the VM and root remained usable. The default therefore remains 64 MiB. This is a shared safety boundary, not a per-addon guarantee or a whole-process cap.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

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

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D5 — package/module resolution

Preserve bounded UTF-8 source snapshots, canonical lowercase ASCII logical segments separated by one `/`, no absolute/traversal/dot-relative path, filesystem fallback, search path or implicit loader, and existing filesystem/reparse-point confinement for operator snapshots. Unqualified `require("private/util")` remains a logical module path in the current defining source namespace.

Addon dependencies extend resolution only with `require("@addon")`, `require("@addon/path")`, `require("@creator.addon")` and `require("@creator.addon/path")`. The text after `@` is the declared stable package ID, not a resolver alias. Resolution requires the caller's declared binding and targets the exact committed dependency lifetime bound to that consumer. `@id/path` may resolve only a public module; `@id` resolves the declared `main` and errors if none exists. `main` is inherently public, resolves a module, and never implicitly executes `init.luau`.

No `./`, `../`, arbitrary filesystem access, fallback search, undeclared dependency import, user-defined alias or `GetDependency():Require()` layer is introduced. The operator root remains outside the addon dependency graph for the first addon release: addons cannot depend on it and root-to-addon consumption remains deferred.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D7 — candidate and module publication

Root/addon candidate initialization is transactional only over **CarbonLuau-owned publication**, not arbitrary same-VM Luau memory. A candidate stages its domain/package visibility, commands, subscriptions/listeners, queued-work admission and other host-owned registrations. D4 also stages first-load cache entries and module-created host resources required by the active publication scope.

An ordinary candidate failure discards every uncommitted CarbonLuau-owned publication from that candidate while preserving the previously committed domain lifetime. Plain same-VM Luau mutations may remain. A failed first-load module scope never merges its cache entry or staged resources into a containing candidate, even if its error is caught and the candidate later succeeds.

A successful candidate commits at an owner-thread safe point and only then retires the previous domain. Commit revalidates every resource-owner domain lifetime. A timeout or integrity-invalidating failure retires the complete VM generation under D1/I9/I10; an old domain is not preserved across that fatal retirement. Historical Phase 1–3 candidate fixtures remain evidence only for the implementations they tested.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D9 — global recovery

Exactly one automatic reconstruction allowance exists for the complete shared VM generation, never one per domain. A successful initial operator load arms it; a successful explicit operator reload/rebuild rearms it; a failed operator action does not.

Fatal VM retirement consumes the allowance and, when available, creates a fresh generation from committed immutable state: discard old work and VM-local references; reconstruct the operator root; reconstruct addons from snapshots owned by still-live provider registrations; then activate addons in deterministic required-dependency order. The failed operation is never resumed or replayed.

An ordinary addon reconstruction failure marks that registration Failed and blocks required dependents while unrelated eligible addons may continue if the VM stays healthy. Root reconstruction failure, or a fatal failure during reconstruction, aborts it and leaves scripting unavailable. Automatic recovery, successful automatic reconstruction, addon/dependency/provider activity and provider churn do not rearm the allowance.

When healthy, `carbonluau.reload` replaces the root domain. When unavailable, it requests a full reconstruction. Only a successful explicit operator action arms/rearms one future automatic reconstruction. No callback replay, host-effect rollback or exactly-once guarantee is introduced.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D10 — admitted-operation and provisional-effect model

Every Luau entry admitted by CarbonLuau has one operation context containing deadline/budget ownership, provisional/publication state and diagnostic identity. It follows the entire synchronous chain, including exported dependency calls, nested modules and synchronous coroutine execution. Crossing a domain boundary does not reset the deadline or remove provisional restrictions.

- `ResourceOwner` is the domain lifetime bound to the API/facade object used.
- `PublicationContext` is the current admitted operation/module/candidate publication scope.
- `Deadline` belongs to the original admitted operation.
- `LifetimeCheck` uses the owning domain lifetime associated with the bound host object.
- `Diagnostics` combine admitted-operation identity with source provenance.

If provisional B calls active A and A uses A's captured facade, the resource is A-owned but remains subject to B's provisional publication context. It cannot publish merely because A is active. Explicit use of a valid B-bound facade makes the resource B-owned under the same admitted operation. Failed nested publication scopes never become eligible for parent commit.

Irreversible host mutation remains prohibited while the operation is provisional. Deferred candidate work cannot run before commit, failed candidates never drain it, and dependencies or scheduling cannot launder a provisional effect. Ordinary errors remain catchable by `pcall`/`xpcall`; only an uncaught ordinary error escaping the admitted boundary fails the operation. D1 deadline cancellation remains uncatchable and VM-fatal. CarbonLuau does not roll back ordinary Luau mutations or promise exactly-once host effects.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D11 — player identity and domain lifetime

Each host-observed connection still receives a monotonically increasing token, never reused within the CarbonLuau plugin instance, bound to exact managed player identity, exact connection identity and string user ID. Every operation re-resolves and validates connection/account identity; invalidity latches, disconnect preserves readable snapshot identity but invalidates mutation, only a new connection creates a new token, and exhaustion fails closed.

A domain-bound facade retains bounded identity snapshots and opaque project tokens. Root/addon retirement invalidates host-backed Player/facade mutation through that domain even when the shared VM remains healthy. An ordinary retained Player/table value may keep a readable script-side snapshot, but mutation/permission operations through its retired owning facade fail closed. Complete VM retirement invalidates every domain. CarbonLuau reload destroys the VM and no new host instance accepts old host/domain/provider tokens.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D12 — scripting and protocol identity

Gameplay C4 explicitly removes the four legacy Player inventory methods and
assigns this breaking development contract to `0.7.0-experimental` under the
user-authorized migration. `Player:GetInventory()` owns facade acquisition;
`PlayerInventory` owns CountItem, HasItem, GiveItem, TakeItem and sampled ItemChanged.
New facade mutations have `SinceApi = 0.7.0-experimental`; existing declarations
retain historical introductions. Experimental status is not permission to reuse
an incompatible identity. Package `0.5.0`, native ABI `1.5`, provider `1.2`, schema
`1` and Luau remain unchanged. No release/tag follows. The migration and
qualification disposition are routed through [Gameplay C4](../GameplayC4-Validation.md).

Entity-1C assigns the additive read-only D20 `Workspace`/`Entity` surface to
development scripting API `CarbonLuau 0.6.0-experimental`. Published package
`0.5.0` and its `0.5.0-experimental` API remain historical and unchanged;
Entity is not retroactively available there. Package/release/tag stay at
`0.5.0`/`v0.5.0` until a separately authorized future release. Native ABI
`1.5`, provider protocol `1.2`, addon schema `1` and the Luau pin are unchanged.
Entity metadata `SinceApi` is `0.6.0-experimental`; previous declarations
retain their exact introduction identities. [Entity-1C](../WorldEntityFoundation1C.md)
owns combined public qualification and release-readiness evidence.
Discovery-2B joins this same additive development identity; it does not bump the
package or native ABI and is not a package-release/final Foundation 2 closure.
The separately scoped combined Foundation 2 closure and next-package handoff are
recorded in [Foundation 2C](../WorldEntityFoundation2C.md).

On 2026-09-23 the user assigned Persistence Foundation 1 to scripting API
`CarbonLuau 0.5.0-experimental`. Persistence is not retroactively available in
`0.4.0-experimental`; existing declarations retain their explicit historical
`SinceApi` values. The current development API identity advances to 0.5 so
implemented persistence metadata can satisfy `SinceApi <= current`. Availability
and qualification remain separate. After the recorded local 1B functional gates,
the user authorized `Experimental` metadata instead of `WorkInProgress`; preview
remains unavailable. 1B is implemented and qualified within its recorded scope,
not by itself an overall Persistence Foundation 1 PASS. [1C](../PersistenceFoundation1C.md)
owns combined closure and final-source CI; package publication authorization
remains separate.

D22 assigns the additive derived-index `DataStore:Query` surface to the same unreleased
`0.5.0-experimental` scripting identity. Basic Query uses structured field/value
requests and automatic preparation; optional string-list hints request early
preparation. Authors manage no schemas, versions or migrations. This is
architecture identity only: no Foundation 2
metadata or API availability exists until its implementation phases qualify, and D22 does not
change the development package, tag or release.

The preceding 0.5 planning assignment was fulfilled by published package
`0.5.0`; the historical pre-release `0.4.0` package mapping below is retained
as chronology, not current development state. [Release.md](../Release.md) records
the separate current development API/package identities. Native ABI changes
only for an actual boundary change, never by inference from API version.

The following records the earlier 0.4 assignment, not persistence availability.

The gameplay facade introduced as `CarbonLuau 0.3.0-experimental` remains compatible. Foundation E assigns the additive addon-capable scripting identity `CarbonLuau 0.4.0-experimental`. GUI Foundation 1G keeps that unreleased identity and adds the complete D15 scripting surface without another version increment. GUI Foundation 2F keeps the same identity and adds the implemented D16 layout, typed-image and retained-scrolling subset. The identity includes package-qualified `require("@id")` and `require("@id/path")`, existing local `require("path")`, `addon.Id`/`addon.Version`, `addon:IsDependencyAvailable(id)` for a declared binding, inherently public `main`, and the domain-bound `Gui` service/value/object surface specified by D15 plus the implemented D16 subset. `TextBox` and `Submitted` are not part of the identity.

The v0.4.0 candidate maps package `0.4.0` to scripting API `0.4.0-experimental`, native ABI `1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1`, and the unchanged pinned Luau revision. These are separate identities even when one release records them together. Provider-defined C# capabilities are not part of protocol 1.2.

Additive APIs preserve accepted names, types, authorization and lifetime/failure behavior unless an explicit breaking-version/migration decision says otherwise. Experimental status does not authorize silent breaks. D4/D7 same-reference sharing, stale-binding rejection and CarbonLuau-owned publication semantics are public compatibility behavior in 0.4.0-experimental. No stable or 1.0 identity is assigned. The lack of authenticated-client evidence does not block this experimental identity, but visual correctness, cursor behavior, actual client click receipt and client-side reconciliation remain unqualified and must not be implied by API availability.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D13 — inventory ownership and failure model

Rust item creation, insertion, stacking, splitting and removal remain live host
mutations rather than globally atomic transactions. They may publish partial
state, invoke callbacks or trusted plugins, and fail without a universal
rollback point. World-drop fallback is unacceptable for an inventory-only
grant, raw Item/container objects remain hidden, and D7 CarbonLuau publication
is conceptually distinct from inventory mutation. The supported description is
**CarbonLuau-serialized, definite-rejection inventory mutation**, not
transactional Rust inventory.

Every eligible operation is one synchronous owner-thread
`PREPARE → COMMIT → VERIFY` facade call. PREPARE is bounded and mutation-free
with respect to inventory: it revalidates the exact D11 Player/domain and
committed context, validates canonical item identity and amount, performs a
bounded nonrecursive physical main/belt/wear snapshot, and constructs a bounded
feasibility plan. It must not invoke hookable predicates such as
`CanAcceptItem`; host/plugin acceptance belongs to COMMIT. PREPARE can establish
definite impossibility, but never guarantees COMMIT success.

GiveItem COMMIT begins immediately before the first item-creation call, because
host lifecycle/resource effects may occur before creation returns. TakeItem
COMMIT begins immediately before entering the host removal operation. From that
point onward an unexpected failure is potentially mutating and must never be
downgraded to ordinary `false`.

The public operational outcomes are exactly: **REJECTED**, only before COMMIT,
returns `false` and guarantees no CarbonLuau inventory COMMIT began;
**SUCCESS**, only after COMMIT and strongest-defensible physical verification,
returns `true`; and **INDETERMINATE HOST FAILURE**, after COMMIT when success
cannot safely be established, raises a controlled Luau error stating that
inventory state may have changed. Programming errors, stale Player use,
provisional execution and invalid arguments are errors rather than rejection.
No outcome promises rollback, unchanged state or exactly-once host effects.

CarbonLuau-owned plans, snapshots, buffers, gate state and domain/session data
retain existing deterministic cleanup. A host resource successfully returned to
CarbonLuau is under temporary responsibility until it is accepted into Player
inventory, consumed by successful stack merge, removed through the supported
cleanup path or transferred to another explicitly accepted host owner. It may
not be silently abandoned; failed or vetoed supported cleanup is indeterminate.
If a correctly invoked supported host API fails before returning a resource,
inaccessible host-internal resources remain in the host failure domain:
CarbonLuau records failure, claims no rollback, fabricates no reference and does
not reproduce private Rust teardown. This narrow boundary does not weaken I1,
I10 or CarbonLuau ownership generally.

GiveItem VERIFY combines host transfer results, accounting for every returned
created Item, and one fresh bounded physical scan. Success requires the same
exact D11 connection, every CarbonLuau transfer reporting success, no deliberate
or fallback world drop, every returned Item reaching accepted inventory or a
successful terminal stack-consumption state, and `Q1 >= Q0 + Amount`. Count
alone is insufficient and forensic provenance against arbitrary trusted plugins
is not promised. TakeItem success requires both the host-reported removed amount
equaling Amount and `Q0 - Q1 == Amount` using D18's physical scan rather than a
hook-virtualized count. Any mismatch after COMMIT is indeterminate, not false.

One logical in-flight mutation gate is keyed by D11 exact Player connection
lifetime, independent of root/addon domain. It is owner-thread-only, spans one
synchronous PREPARE/COMMIT/VERIFY call, is always released in `finally`, has no
blocking mutex or waiting queue, and rejects recursive same-Player mutation with
a controlled error. Sequential operations and operations for other Players may
proceed after their applicable gate is free. Disconnect, replacement and VM
recovery do not inherit or replay a gate or operation.

CarbonLuau serializes only Luau-originated inventory mutations. Trusted Carbon
plugins may synchronously reject, override or mutate inventory and perform other
effects. Ordinary Carbon hook exceptions are generally contained by Carbon's
dispatcher and remain distinct from direct Rust delegates/item-mod lifecycle
failure; both interference classes require target-build qualification. Host-
driven recursive VM entry remains prohibited, and resulting CarbonLuau work is
admitted later through existing bounded scheduling.

Both inventory mutations are committed-only irreversible host effects under
D10. They revalidate the exact Player before PREPARE, immediately before COMMIT
and during/after VERIFY where needed. Before-COMMIT disconnect is a controlled
stale error; after-COMMIT disconnect is indeterminate. Candidate/module/cross-
domain provisional paths cannot mutate or launder context, while deferred work
may run only after successful publication. COMMIT and VERIFY are never split
across Luau turns. Deadlines do not hard-preempt host code; timeout/fatal
recovery after mutation implies neither rollback nor replay.

Canonical names retain D18's 1..128 lowercase ASCII byte/no-NUL rules. Mutation
Amount is a required exact integer from 1 through `Int32.MaxValue`. Physical
inspection remains bounded, nonrecursive main/belt/wear traversal. Creation,
transfer and removal work use a target-build-qualified capacity envelope, one
fresh bounded VERIFY scan and no polling. One operation may never perform
unbounded item work.

Inventory-M2 historically reported G1-G5 PASS, including a GiveItem planned
no-world-drop adapter. **That G1 conclusion is superseded as incomplete** by
the callback-ordering investigation and I12 adoption. M2 retains G2,
returned-Item terminal-state observability; G3, TakeItem removal and
verification behavior; G4, supported cleanup for returned unattached Items; and
G5, concrete work bounds derived from supported container maxima, on Rust build
`25353106` plus Carbon `2.0.259`. The exact adapter and evidence are recorded in
[InventoryMutationM2Validation.md](../InventoryMutationM2Validation.md). This
evidence does not implement either public API. Failure during later
implementation or requalification leaves that individual API unimplemented
without weakening its contract or blocking an independently qualified sibling.
Player-1F-B's [exact-build follow-up](../qualification/PlayerInteraction.md#playerinteractionfoundation1fb) found
an uncovered acceptance-callback/stack-limit path into the split/drop fallback.
I12 resolves the semantic isolation boundary. Player-1F-B now records the
supported-host G1 requalification and production implementation in
[its validation record](../qualification/PlayerInteraction.md#playerinteractionfoundation1fb-validation); this does
not restore the superseded blanket M2 conclusion. Historical M2 cases and
independently qualified TakeItem remain evidence.

For the intended `GiveItemBehavior.InventoryOnly` mode, G1 now requires:

> CarbonLuau must not select, invoke, or knowingly permit world-drop fallback as
> part of its qualified GiveItem adapter. On the qualified supported-host
> baseline, absent external trusted interference that mutates operation-relevant
> state during synchronous callbacks, requested inventory delivery must not
> become world delivery.

This is not permission to ignore an observed drop or unexplained failure.
InventoryOnly's existing success predicates above remain mandatory; detected
world delivery of the requested grant or uncertain returned-resource state
cannot be reported as success, even if external interference caused it.
Known-invalid premises must not be knowingly passed to the host as a qualified
transfer. Ordinary callback rejection, including rejection of a generated
split under baseline behavior, cannot be relabeled external interference.
The required supported-host and separately classified boundary-test matrix is
in [Player-1F-B requalification](../qualification/PlayerInteraction.md#playerinteractionfoundation1fb-required-1f-b-requalification).
This amendment changes qualification scope, not PREPARE/COMMIT/VERIFY,
false/true/indeterminate, or TakeItem semantics.
Historical
[Phase4.md](../history/inventory/Phase4.md#phase4-ownership-gate-d13) and
[Phase4-Validation.md](../history/inventory/Phase4-Validation.md#phase4-validation) findings remain evidence of host
nontransactionality, not the current categorical architecture conclusion.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D14 — addon package, dependency and provider lifecycle

Stable addon IDs are `addonname` or `creator.addonname`. Each lowercase ASCII segment is 1–32 characters, starts and ends alphanumeric, and may contain internal `_`/`-`; the two-segment form is at most 65 characters. `carbonluau` and `carbonluau.*` are reserved. Noncanonical case is rejected, not aliased. Exactly one registration owns an ID; package version is informational/provenance. Parallel versions, ranges, solving, downloads, registries and lockfiles are deferred.

The internal registration states are `Registered`, `Blocked`, `Initializing`, `Active`, `Failed` and `Stopping`. Acceptance reserves the ID. Missing required dependencies block before execution. Successful initialization creates a fresh opaque domain lifetime and becomes Active. An uncaught ordinary initialization error becomes Failed. Failed registrations do not retry on unrelated activity. A relevant required-dependency restoration may schedule one bounded activation attempt during that graph transition; failure then requires explicit retry/reload/replacement. Stopping retains the ID until cleanup completes.

Activation binds declared dependencies to exact committed Active domain lifetimes. Required absence blocks; optional absence is fixed for that consumer lifetime. Required loss retires/blocks the affected required-dependent graph. Optional loss leaves the consumer active but makes availability false and new imports fail. Already returned ordinary values may remain. Replacement never retargets old bindings. Successful required-dependency replacement reinitializes eligible required dependents in deterministic topological order; optional consumers do not automatically rebind. Required cycles block their strongly connected component; optional edges alone do not. Runtime module cycles remain D4 errors. Declarations, not caught imports, define lifecycle requirements.

A provider lifetime is the concrete loaded Carbon `Plugin` object plus the current CarbonLuau host lifetime. It controls lifecycle ownership, replacement/unregister authority and stale-token detection, not authentication against other trusted in-process plugins. Only the owner may replace/unregister. Provider unload retires its registrations. CarbonLuau unload invalidates all provider tokens; a surviving provider must explicitly register again with the new CarbonLuau instance. Old tokens stay stale and Luau never receives the `Plugin` object.

The primary package input is an immutable CarbonLuau-owned source snapshot. Archive and single-source/provider transports normalize to it, and accepted provider input is copied before registration returns. Parsing rejects malformed UTF-8/JSON, duplicate keys, unknown semantic fields, noncanonical/duplicate normalized paths, absolute/traversal/dot/backslash paths, encrypted/unsupported/symlink-style entries and nested archives. Packages are bounded snapshots, never mounted or extracted. Schema 1 limits are 4 MiB archive bytes, 8 MiB expanded bytes, 64 KiB manifest, 64 KiB per source, 4 MiB aggregate source per package, 256 source modules, 512 archive entries, 32 dependencies, 128 live registrations, 32 registrations per provider and 32 MiB of aggregate immutable snapshots. Streamed decompressed bytes are enforced independently of ZIP metadata. Authors use D5 `require` plus `addon:IsDependencyAvailable`; tokens, IDs, graph transitions and lifecycle machinery stay internal.

<!-- END RECORD Invariants.md -->

