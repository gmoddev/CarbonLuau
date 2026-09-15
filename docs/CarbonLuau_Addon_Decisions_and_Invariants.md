# CarbonLuau addon architecture — revised design proposal

**Status:** design only, revised after AI review on 2026-09-15. No addon implementation,
canonical policy amendment, API release or runtime qualification is authorized here.
Runtime baseline: package `0.3.0`, revision
`3046390b5ee668f02c20bcde01d10138f5ca7489`.
Review baseline: `3b086f43db3eb8e700513c8836d794603b295c9b`.

[Invariants.md](Invariants.md) remains authoritative. This document proposes changes
for a future addon release; examples below do not run on the current public API.
See [validation and remaining gates](Addon-Design-Validation.md).
The [original proposal](https://github.com/gmoddev/CarbonLuau/blob/3b086f43db3eb8e700513c8836d794603b295c9b/docs/CarbonLuau_Addon_Decisions_and_Invariants.md)
and [first review](https://github.com/gmoddev/CarbonLuau/blob/3b086f43db3eb8e700513c8836d794603b295c9b/docs/Addon-Design-Validation.md)
remain historical evidence. This revision replaces that proposal, not the canonical
D/I register. Its D-A01–D-A60 and I-A01–I-A58 identifiers are historical and are not
reused as current rules; section references below own the consolidated proposal.

## 1. Direction and deliberately limited guarantees

Use one shared Luau VM with separate operator-root and addon domains. Each domain
has a private sandbox environment, source namespace, lifetime, queues and
CarbonLuau-owned registrations. Public modules return ordinary canonical shared
Luau values. No per-consumer source copies, RPC or revocation membranes.

Domains are ownership/scheduling units, **not independent VM failure boundaries**.
Normal addons are operator-installed, equally trusted users of the allowlisted
facade; `FullTrusted` does not mean unrestricted Carbon, Rust, .NET or OS access.

| Event | Proposed scope |
|---|---|
| Invalid package or registration request | Reject request without reserving its ID |
| Missing required dependency | Block registration; no candidate execution |
| Ordinary compile/init error | Fail candidate; discard its staged publication |
| Ordinary active callback/module error with healthy VM | Fail current operation; shared mutations already made may remain |
| Timeout, including a candidate timeout | Retire the complete shared VM, root and all addons |
| Integrity-invalidating VM failure | Same global retirement |
| Allocation error with demonstrably healthy VM | Fail operation as supported by the VM; no addon-local memory isolation claim |
| Provider unload | Retire its registrations and affected required dependents logically |
| Native/process corruption | Outside recoverable addon guarantees |

Keep existing uncatchable timeout behavior unless separately qualified changes are
approved. Addon-local cancellation is optional future research, **not a prerequisite
for shared-VM addons**. There is no automatic per-addon-VM fallback. A topology
change would require a new shared-state contract and explicit approval.

## 2. Shared modules and lifetime

Cache identity is shared-VM generation + package runtime lifetime + logical module
path. Local and public access to that module use the same cache entry. Every
consumer bound to A1 receives the exact same returned value from A1's module;
A2 has a different entry. Modules execute in their defining package environment,
resolve private helpers in that package, and use that package's bound dependencies.

Return ordinary Luau tables/functions/scalars under the module contract. Do not
recursively proxy nested values, function returns, metatables or iterators.
Do not promise consumer-independent authoritative state or deep revocation.

Retiring A1 prevents new imports through A1 bindings and invalidates A1's host
facades, resource handles, queued work and registrations. It does **not** poison a
table or closure already retained by B. Such values may continue pure Luau work
and mutation until unreachable. They never silently become A2's values.

A retained closure using A1's captured host facade receives a controlled stale
error after retirement. Code explicitly passed a valid B-owned facade is subject
to that facade's normal authorization/lifetime rules; source authorship alone is
not a hostile-code boundary. Package-bound `require` closures also check lifetime,
even for cached imports. Retained plain values can still be leaked deliberately
between equally trusted addons; export privacy controls resolution, not secrecy
against cooperating code.

Teardown deterministically releases CarbonLuau's references and host registrations,
not all heap objects transitively retained by other addons. Immediate memory
reclamation, revocation of ordinary functions and rollback of shared state are
explicitly not promised.

## 3. Execution and host effects

The admitted scheduled operation owns its entire synchronous execution budget.
B → A → B calls consume B's original deadline without owner switching or deadline
refresh. Candidate initialization and module initialization also have bounded
admitted operations; nested imports consume the enclosing operation's budget.
Record budget owner and source provenance separately in diagnostics.

No closure instrumentation is required to move CPU ownership at every function
call. This does not remove resource-lifetime checks or host authorization.
Player identity remains the established exact connection lifetime, re-resolved
before host use; no raw managed/native objects reach Luau.

The provisional restriction follows the **whole admitted operation**. Provisional
B → active A → host mutation must still be rejected, even if A's captured facade
is active. Captured resource lifetime and admitted operation context are separate
checks. Deferred candidate work cannot run before commit; failed candidates never
drain it. Neither a dependency call nor scheduling may launder provisional effects.

The existing single-active facade is insufficient for these checks. Exact ownership
of new tasks, listeners and commands created through another domain's captured
facade, including lazy public-module initialization, remains gate G2. No such path
may publish outside the candidate transaction; reject unsupported crossings rather
than inventing ownership or silently weakening the effect rule.

## 4. Publication, replacement and recovery

“Transactional activation” means **transactional publication of CarbonLuau-owned
resources**, not a transaction over ordinary Luau memory.

A candidate stages package visibility, commands, subscriptions and work admissions.
An ordinary failure discards that staged publication. A failed registration can
remain Failed and reserve its ID for diagnostics; “rollback” does not mean erasing
the accepted registration record.

If provisional B imports active A and changes A's plain balance table, then fails,
that table change may remain. Lazy module initialization/cache population can also
be observable. Existing queued callbacks and shared tables must not be advertised
as unchanged in every respect. Irreversible host effects remain prohibited while
provisional, independent of this explicit limitation.

Healthy-VM replacement prepares a new domain alongside the old domain, commits
publication at an owner-thread safe point, and retires the old lifetime. Failed
ordinary replacement preserves the old publication/lifetime, **not arbitrary
shared data touched by the candidate**. A candidate timeout retires both old and
candidate domains with the VM. It cannot preserve the old live generation.

The operator root is not a package and cannot be an addon dependency. Normal
`carbonluau.reload` should replace only the root domain while the shared VM is
healthy. Fatal root or addon failure retires everyone. Root imports of addons
remain deferred. This requires explicit migration from today's separate candidate
VM/whole RuntimeGeneration replacement; D7's current preservation guarantee must
not silently be weakened.

Proposed global recovery follows D9's anti-loop intent: one bounded reconstruction
allowance for the shared VM, never one allowance per addon. Use committed immutable
snapshots and still-live providers, discard old work, and rebuild in deterministic
dependency order. An ordinary addon initialization error fails that addon and blocks
its required dependents; a fatal error during reconstruction stops reconstruction.
Never repeatedly retry a Failed addon because unrelated registration events occur.

Automatic recovery, dependency restoration, addon registration and provider churn
must not rearm the global allowance. The explicit operator rearm/reset action and
its success criteria are gate G3, not a claim that current D9 already implements
multi-domain recovery. No rollback of completed host effects, callback replay or
exactly-once delivery is promised.

## 5. Package identity, snapshots and transport

Stable IDs remain `addonname` or `creator.addonname`. The creator prefix is a
namespace, not authentication. Proposed grammar: one or two lowercase ASCII
segments, each 1–32 characters, alphanumeric at both ends with internal `_`/`-`;
one-character alphanumeric segments are valid. Maximum full length is 65.
Reserve `carbonluau` and `carbonluau.*`. Reject uppercase/noncanonical IDs.

One registration reserves each stable ID, including Blocked/Failed registrations.
Duplicates fail; versions do not create distinct IDs and no suffixes are generated.
Replacement is explicit and provider-owned. A Stopping registration retains its ID
until teardown completes. Every activation receives a fresh opaque lifetime.

The primary package abstraction is an immutable, CarbonLuau-owned source snapshot.
`.claddon` ZIP and single-file registration are transports normalized into that
model. Hashes are diagnostics/provenance, not package identity. Provider mutation of
input bytes after acceptance must not alter the snapshot.

Reject malformed UTF-8/JSON, duplicate JSON keys, unknown semantic fields, absolute
or traversal paths, dot components, backslashes, case collisions, duplicate paths,
encrypted/symlink ZIP entries and unsupported compression. Only `addon.json` and
canonical `.luau` source files are admitted; no arbitrary extraction or nested
archives. Stop decompression on cumulative limits, not just reported sizes/ratios.

Conservative parser candidates retained from the earlier proposal: 64 KiB manifest
and individual source, 4 MiB archive and aggregate source, 8 MiB expanded bytes,
256 source modules, 512 ZIP entries, 127-character paths, depth 32, 128 exports and
32 dependencies. These are internal qualification candidates, not shipped limits.
Bound aggregate snapshots, requests, registrations and diagnostics too.

## 6. Manifest and imports — selective simplification

Keep explicit schema, export visibility and required/optional ID declarations.
These enable preflight, private-module protection and deterministic dependency
loss without inferring intent from conditional or caught imports. They do not
require downloading packages, a registry, lockfiles or a solver.

Defer dependency version expressions in v1; retain a `MAJOR.MINOR.PATCH` package
version for diagnostics. One active version does **not** make compatibility
checking useless: deferral deliberately leaves version compatibility to the
operator. No automatic compatibility promise follows from a matching ID.

Proposed manifest, illustrating the retained small metadata surface:

```json
{
  "schema": 1,
  "id": "admin",
  "version": "1.0.0",
  "publicModules": [],
  "dependencies": {
    "required": ["economy"],
    "optional": ["metrics"]
  }
}
```

Entrypoint is `init.luau`; display name is optional. Default empty exports and
dependency lists mean no exports/dependencies. A package can optionally declare
`main: "api"` with `"api"` in `publicModules` to map its short import to
`api.luau`. Without main, the short import is a controlled error. Main never
implicitly executes the addon entrypoint. Module paths are extensionless logical
paths; directories such as `public/` are conventions, not authority.

Proposed import syntax:

```lua
local Economy = require("@economy") -- economy's declared main
local EconomyApi = require("@economy/api") -- explicitly exported api.luau
local Util = require("private/util") -- current source package, not filesystem-relative
```

Both Economy variables above are the same value if main is `api`.
Use one package-aware `require`, not `GetDependency():Require()`.
This is inspired by the [Luau alias RFC](https://rfcs.luau.org/require-by-string-aliases.html),
not an implementation of its filesystem/.luaurc resolver. CarbonLuau retains
canonical lowercase package IDs; the RFC's case-insensitive aliases are not adopted.
Current embedded `require` does not implement `@` imports at all.

Keep today's package-root-relative logical local paths. Do not add `./`, `../`,
filesystem access, fallback search or implicit external imports. Adding addon
aliases requires an explicit D5/API extension without changing existing root paths.

A proposed `addon:IsPackageAvailable("metrics")` reports whether that declared
binding is currently usable, not whether an arbitrary global package exists.
Undeclared IDs are controlled errors. Availability does not mean module
initialization will succeed. An unavailable optional import raises a controlled
dependency-unavailable error; a failing available module raises its actual module
error. `addon.Id` and `addon.Version` are metadata, not authority tokens.

Do not claim the existing `0.3.0-experimental` API supports these additions or that
a `"0.3"` compatibility-family matcher already exists. Package schema, provider
protocol, scripting API, package release and native ABI remain separate version
identities; public negotiation/version assignments are gate G4.

## 7. Dependency bindings

Activation binds declared dependencies to exact committed Active lifetimes.
Required absence blocks activation. Optional absence is fixed for that consumer
lifetime; a later provider does not silently appear until consumer reload.

After optional A1 disappears, B stays active; availability becomes false and new
imports fail even if cached. Already returned ordinary A1 values may survive.
A2 never retargets B's A1 binding. Required A1 loss retires/blocks its affected
required-dependent subgraph. Escaped plain values still cannot be revoked.

Successful replacement commits A2, invalidates A1 bindings and reinitializes eligible
required dependents in deterministic topological order. Optional consumers do not
rebind. Ordinary failed replacement leaves old bindings intact, subject to the
shared-state and fatal-failure limitations in section 4.

Required cycles block the affected strongly connected component. Optional edges
do not block activation by themselves. Runtime recursive module imports produce
controlled cycle errors, preserving CarbonLuau's bounded loading-stack behavior.
Do not infer required edges/retry obligations merely because a require was attempted
or caught; initialization errors are not automatically DependencyMissing.

A relevant required-dependency restoration schedules at most one attempt for each
eligible Blocked registration in that graph transition. Its own failure makes it
Failed, requiring explicit retry/reload. Bound graph size, depth and transition work;
no package installation, version solving or unbounded retry loops.

## 8. Provider and host coordination

Managed CarbonLuau owns provider lifetimes, registrations/snapshots, dependency
graph, publication, shared player directory and the global scheduling coordinator.
Native code owns the VM, module cache, execution boundaries and VM-local values;
it must not know Carbon Plugin objects.

Bind provider registration to the actual loaded Plugin object plus a CarbonLuau
lifetime/token, not a plugin-name string. Generic Plugin.Call is not authenticated
caller provenance. Installed C# providers are trusted in-process code, not sandboxed
adversaries. Actual lifecycle/reference/thread behavior still needs live evidence.

Provider API calls validate bounded input and ownership on the owner thread; reject
wrong-thread calls. Activation is queued outside the registration hook, with no
nested VM entry. Keep private managed types out of the protocol. Offer discovery,
archive/source registration, status, replacement and idempotent unregister;
exact responses and stale-token distinctions are gate G4.

Provider unload invalidates all its registrations even if it forgets to unregister.
Tokens from an old CarbonLuau instance remain stale. Optional provider integration
and re-registration after CarbonLuau reload must be tested, not assumed.

Global unload stops intake, invalidates host lifetimes, cancels work, unpublishes
resources, releases domains and destroys the shared VM; unload the native library
last. No arbitrary Luau shutdown hook or provider callback is needed for correctness.

## 9. Scheduling and memory

Use bounded domain-specific ready/delayed/event queues and a persistent round-robin
arbiter under **one global frame budget**. A continuously ready domain gets no
second turn in a round before peers have an opportunity; preserve progress across
frame boundaries. New work during a drain is eligible only in a later drain.
Idle domains must not cause persistent native drains.

Retain per-operation deadlines; do not grant every addon a full independent frame
budget or reset deadlines on dependency calls. No inline Luau event fanout or
recursive VM entry. One saturated domain must not consume another's admission
capacity. Commands need globally deterministic collision checks/publication across
root, addons and Carbon registries.

The VM-wide heap cap is the hard Luau allocation boundary. Per-addon categories
are optional diagnostics/best effort, not hard quotas or reliable retained-object
ownership. Shared/interned allocations, GC and retained foreign values preclude
claims of automatic per-addon heap reclamation. Global heap exhaustion can affect
unrelated addons. Managed source snapshots and native/compiler containers need
separate bounds; a VM cap is not a process RSS cap.

Measure 1/10/50/100 domains in the shared VM, including source, managed memory,
allocator bytes, RSS, activation/replacement and service gaps. The old 632,928-byte
root reading is not an addon forecast. The existing 32-VM registry does not limit
this model to 32 addons; increasing it to 128 is no longer an addon requirement.

## 10. Delivery gates and exclusions

Before implementation, approve the necessary canonical deltas explicitly. Build
internal fixtures first; do not expose production addons before all gates below.

| Gate | Required closure |
|---|---|
| G1 — domain lifetimes and modules | Same-value caches, private provenance, immutable bindings, stale import/facade rejection, surviving plain values, bounded graph transitions |
| G2 — publication and host effects | Multi-domain facade coordinator; operation-wide provisional guard; explicit cross-domain registration/task ownership; command collision, fanout and failed-candidate tests |
| G3 — VM-wide recovery | Root domain replacement migration, fatal candidate behavior, global rearm policy, bounded reconstruction and no replay/retry loops |
| G4 — provider/package protocol | Exact parser/transport/status rules, aggregate limits, version negotiation and actual Carbon unload/reload/re-registration evidence |
| G5 — qualification | Fairness, flood, shared-heap exhaustion, 1,000 lifecycle cycles, scale measurements, Windows/Linux Carbon tests, affected sanitizers/fault tests and synchronized API docs |

No addon-local cancellation experiment is required for this v1 topology. Such
research must remain separate from shipping addon semantics.

First delivery includes shared-VM domains, package/provider registry, declared
dependencies, canonical public modules, coordinated facade/scheduler and operator
status/reload. Public documentation belongs in a future dedicated addon reference
only when its contract is approved and implemented.

Excluded: provider C# capabilities, restricted exposure profiles, hard per-addon
heap quotas, deep export revocation, closure-owner switching, shared-state rollback,
cross-VM RPC/source copies, multiple versions per ID, version ranges, package
downloads/registry/lockfiles, async provider APIs, arbitrary filesystem/network
access, root addon consumption and script shutdown hooks. Deferred item APIs remain
deferred by D13. Existing Phase 0–5 evidence is unchanged, not addon qualification.
