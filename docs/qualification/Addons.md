# Addons: qualification ledger

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../Invariants.md).

## Records

- [FoundationA.md](#foundationa)
- [FoundationB.md](#foundationb)
- [FoundationC.md](#foundationc)
- [FoundationD.md](#foundationd)
- [FoundationE.md](#foundatione)
- [FoundationF.md](#foundationf)
- [FoundationG.md](#foundationg)

<!-- BEGIN RECORD FoundationA.md -->

<a id="foundationa"></a>

<a id="foundationa-foundation-a--shared-vm-multi-domain-runtime-foundations"></a>

# Foundation A — shared-VM multi-domain runtime foundations

Foundation A implements internal lifetime, admission and publication primitives for
the canonical addon architecture in [Invariants.md](../Invariants.md). It does not
expose addon registration, package transport, dependency resolution, public-module
imports or an addon scripting API. The current public scripting identity remains
`0.3.0-experimental`.

<a id="foundationa-lifetime-model"></a>

## Lifetime model

The managed/native ownership chain is now explicit:

```text
NativeRuntime / CarbonLuau host lifetime
└── RuntimeGeneration / one native VM generation
    ├── RuntimeDomain / operator-root lifetime R1
    ├── RuntimeDomain / operator-root candidate R2
    └── future addon-capable domains
```

`NativeRuntime.HostLifetimeId`, native `VmGenerationId` and native domain handles
are distinct monotonically allocated identities. A domain handle is also its
`DomainLifetimeId`; it is never reused within the native-library lifetime. Retired
domain records remain bounded tombstones while the VM lives so a closure retaining
a domain-bound native upvalue fails as stale instead of dereferencing freed memory.

A healthy operator reload creates and initializes a provisional root domain inside
the current VM. Ordinary failure destroys only that candidate. Successful commit
publishes it at the owner-thread boundary and retires the old root. A timeout or
integrity failure still retires the whole VM; D9 reconstruction creates a new VM
generation and then a new root domain.

<a id="foundationa-native-representation"></a>

## Native representation

Native ABI 1.3 adds internal domain operations while retaining ABI 1.0–1.2 exports
as compatibility wrappers for historical fixtures:

- `cl_vm_generation`
- `cl_domain_create` / `cl_domain_destroy` / `cl_domain_commit`
- `cl_domain_module` / `cl_domain_load_source`
- `cl_domain_facade` / `cl_domain_event`

One VM owns a bounded set of domain records. Each live domain owns its source/module
map, loading stack, committed and provisional callback queues, module-cache refs,
facade refs and host callback. The semantic cache key is therefore the containing
VM generation, defining domain handle and logical module path. Entry chunks and
first module executions each use a fresh `luaL_sandboxthread` environment populated
with domain-bound `require`, `task` and facade values. The protected base libraries
remain VM-wide.

The VM owns one `AdmissionContext` during every Luau resume. It records the admitted
domain, operation identity and provisional state; the existing monotonic deadline
continues to cover the complete synchronous call chain. The managed `InsideNative`
guard and native admission checks reject host-driven recursive execution. The
registry mutex is recursive only so a rejected reverse-call can return a controlled
status rather than deadlock; it does not authorize nested VM entry.

<a id="foundationa-publication-model"></a>

## Publication model

Every first module execution creates a nested native `PublicationScope`.

- Cache refs and native task callbacks are staged in that scope.
- A failed scope unreferences its cache candidates and task threads.
- A successful nested scope merges into its parent.
- A successful scope in an active operation publishes immediately.
- A successful scope under a provisional candidate merges into that candidate;
  domain commit publishes cache refs and callbacks together.
- Candidate destruction discards all remaining provisional state.

Domain-bound host calls that create or remove registrations open a matching managed
facade checkpoint. Nested commit merges the checkpoint; rollback removes commands
and listeners added by the failed module attempt. This is deliberately limited to
CarbonLuau-owned publication. Plain Luau tables, globals and leaked references are
not rolled back.

The module value contract is unchanged: first result only, `nil`/no result becomes
`true`, extra results are ignored, successful values are cached by reference,
failures retry, cycles are controlled, depth is 32, initialization cannot yield,
module globals are private and closures retain their defining environment. Ordinary
errors remain catchable; private deadline cancellation remains uncatchable and
VM-fatal.

<a id="foundationa-qualification-fixtures"></a>

## Qualification fixtures

The native `ScriptCore` fixture now proves:

- two independently owned domains coexist in one VM;
- equal logical module paths have distinct domain cache entries;
- provisional module cache/task state is invisible before commit and publishes
  together at commit;
- retiring one domain removes only its cache/queue while the other continues;
- stale domain entry is rejected;
- a caught failed module publishes no cache/task and a retry executes again.

Managed runtime/facade fixtures prove:

- CarbonLuau host, VM generation and root-domain identities are distinct;
- ordinary failed and successful root replacement keep the VM generation;
- fatal recovery changes the VM generation;
- caught failed module initialization can register a command and queue a task yet
  leaves neither resource nor cache entry eligible for candidate commit;
- the same failed module retries twice in one successful candidate;
- historical module, scheduler, facade, recovery and unload behavior remains green.

Windows qualification uses the native CTest suite and the real net48 managed/native
interop executable. Linux qualification uses the same sources/tests in the existing
Ubuntu container and sanitizer jobs. These tests qualify Foundation A primitives,
not public addons, provider lifecycle, package parsing, dependency graphs, scale
policy or an addon API version.

<a id="foundationa-deliberately-deferred"></a>

## Deliberately deferred

Foundation A does not implement D5 package-qualified imports, D14 registration or
provider protocols, dependency graph transitions, addon metadata, public module
visibility, provider archives, addon scheduler fairness/scale policy, operator-root
consumption of addons, or public addon documentation. Those remain gated by the
canonical compatibility and qualification work.

<!-- END RECORD FoundationA.md -->

<!-- BEGIN RECORD FoundationB.md -->

<a id="foundationb"></a>

<a id="foundationb-foundation-b---experimental-providerpackage-registration"></a>

# Foundation B - experimental provider/package registration

Foundation B implements the narrow D14 package/provider lifecycle on top of the
qualified shared-VM primitives in [Foundation A](#foundationa). It is an
experimental managed Carbon-plugin integration, not a completed public addon
system. The public Luau scripting identity remains `CarbonLuau
0.3.0-experimental`; the provider protocol has its own identity,
`CarbonLuau.Addons` / `1.0`.

<a id="foundationb-provider-protocol"></a>

## Provider protocol

A loaded Carbon plugin obtains the CarbonLuau plugin through the supported
`[PluginReference]` mechanism and calls these named hooks, passing its concrete
plugin object as the first argument on every stateful operation:

| Hook | Arguments after provider | Purpose |
| --- | --- | --- |
| `CarbonLuau_AddonProtocol` | none; no provider required | Query protocol name, version and availability |
| `CarbonLuau_RegisterAddonArchive` | `.claddon` bytes | Copy, parse and reserve an archive registration |
| `CarbonLuau_RegisterAddonSource` | ID, version, UTF-8 source bytes | Register one `init.luau` source through the same snapshot lifecycle |
| `CarbonLuau_GetAddonStatus` | token | Read owned registration status |
| `CarbonLuau_ReplaceAddonArchive` | token, `.claddon` bytes | Prepare a same-ID replacement |
| `CarbonLuau_ReplaceAddonSource` | token, version, UTF-8 source bytes | Prepare a single-source replacement |
| `CarbonLuau_UnregisterAddon` | token | Retire and release an owned registration |

The protocol returns only strings, string arrays and byte-array inputs. It does not
expose runtime/domain objects to providers or provider objects to Luau. Stateful
responses are a nine-element string array:

```text
result, token, state, id, version, diagnostic, snapshotSha256,
domainLifetimeId, pendingVersion
```

`result` is `OK` or `ERROR`. Diagnostics are sanitized and limited to 1024
characters. Tokens combine the CarbonLuau host lifetime with a monotonic sequence;
they are usable only by the same concrete provider plugin instance. Carbon's
`OnPluginUnloaded(Plugin)` lifecycle hook retires all registrations owned by that
instance before its identity is discarded. This is lifecycle ownership, not a
hostile managed-plugin security boundary.

<a id="foundationb-package-subset"></a>

## Package subset

A `.claddon` is a standard ZIP archive held and read in memory. It is never mounted
or extracted. It contains exactly one `addon.json`, one `init.luau`, and optionally
additional `.luau` modules. Nested archives and all other file types are rejected.
Foundation B accepts this strict manifest subset:

```json
{
  "schema": 1,
  "id": "creator.addon",
  "version": "1.0.0",
  "dependencies": {
    "required": ["creator.required"],
    "optional": ["creator.optional"]
  }
}
```

`dependencies` is optional. Unknown or duplicate fields, duplicate dependencies,
self-dependencies and noncanonical values are rejected. IDs have one or two
lowercase ASCII segments of 1-32 characters, joined by one dot; each segment starts
and ends alphanumeric and may contain internal `_` or `-`. The total is at most 65
characters. `carbonluau` and `carbonluau.*` are reserved. Versions are canonical
numeric `MAJOR.MINOR.PATCH` values.

Archive names use `/`, are relative, contain no empty, dot or traversal segments,
and are at most 127 characters and 32 segments. Duplicate normalized paths,
directories, links/special entries, encryption, unsupported compression, ZIP64,
multi-disk ZIP, malformed UTF-8/JSON and nested archives are rejected. Normal ZIP
handling remains `System.IO.Compression`; an explicit bounded header preflight is
used because that API does not expose encryption/compression flags needed for the
required rejection policy.

Accepted input bytes are copied before registration returns, decoded into a private
immutable source snapshot, and identified by SHA-256. Single-source registration is
normalized to the same representation with an implicit `init.luau`, schema 1 and no
dependencies.

<a id="foundationb-foundation-b-qualification-limits"></a>

## Foundation B qualification limits

These are conservative Foundation B values, not promises for later package policy:

| Resource | Limit |
| --- | ---: |
| Input archive | 4 MiB |
| Cumulative expanded archive data | 8 MiB |
| Manifest or one source | 64 KiB |
| Aggregate source per package | 4 MiB |
| Modules besides `init.luau` | 256 |
| ZIP entries | 512 |
| Dependencies | 32 |
| Live registrations / one provider | 128 / 32 |
| Aggregate live and pending snapshot source | 32 MiB |
| Retained idempotence tombstones | 1024 |

<a id="foundationb-lifecycle-and-publication"></a>

## Lifecycle and publication

Registration reserves its stable ID immediately. Dependency-free registrations move
from `Registered` to `Initializing`, then `Active` or `Failed`. A manifest declaring
any dependency is accepted as `Blocked` with an explicit diagnostic; Foundation B
does not execute it or infer partial dependency semantics.

Activation creates a Foundation A domain in the current shared VM. Entrypoint,
module-cache, task, command and listener publication uses the existing candidate
transaction. Compile/init failure publishes nothing. Caught ordinary module failure
retains the qualified retry/no-leak behavior.

Same-provider replacement retains the token and ID reservation. The old domain and
resources remain active while the candidate initializes. Success atomically
publishes the new domain and retires the old one; ordinary candidate failure drops
the candidate and preserves the old active domain. Dependency-bearing replacement is
rejected until dependency resolution exists.

Unregister transitions through `Stopping`, tears down the domain/resources, then
releases the ID. Repeating unregister with the same provider/token is idempotent
while its bounded tombstone remains; foreign and stale tokens fail. Provider unload
does the same for every owned registration in a deterministic owner-thread pass.
`Initializing` is synchronous and therefore not externally observable or
unload-interruptible on the owner thread.

<a id="foundationb-qualification-evidence"></a>

## Qualification evidence

Foundation B was qualified on 2026-09-16 from baseline
`333e8c9010aea47c83915ac5c82e46dbf40321d5`:

- Windows 11/MSVC 19.44: all four native CTest fixtures, import policy, managed
  loader tests, real native/managed runtime tests, package checks and API checks
  passed.
- Ubuntu 24.04/GCC 13.3: all four native fixtures, managed loader/runtime tests and
  exported-probe check passed.
- Ubuntu ASan/UBSan with leak detection: all four native fixtures passed.
- The addon fixture covered strict archive/manifest/path rejection, immutable
  archive and source copies, IDs/ownership/stale tokens, `Registered`, `Blocked`,
  `Active` and `Failed`, compile/runtime/module failure rollback, atomic successful
  and failed replacement, provider-wide teardown, 100 register/activate/unregister
  cycles, 100 replacements and return to one root domain/zero VMs at teardown.
- Live Carbon 2.0.259.0 on an isolated Windows Rust server compiled the production
  package using Carbon's supported assembly-reference directive. A separate provider
  reached `Active`; real provider unload retired its domain, long-delay task,
  command and ID reservation; reloading that provider reclaimed the same stable ID
  and reached `Active` again.

The live check also established why the package source carries
`// Reference: System.IO.Compression`: Carbon's plugin compilation set does not
reference that forwarded framework assembly by default. The directive is handled by
Carbon's supported script loader and avoids reflection or an environment-specific
ZIP implementation.

<a id="foundationb-deliberately-deferred"></a>

## Deliberately deferred

Foundation B does not implement `require("@addon")`, dependency graphs or binding,
public module exports, `addon:IsDependencyAvailable`, root-to-addon imports,
provider C# capabilities, restricted exposure, multiple versions/instances,
downloads/registries, version solving, or any Foundation C work. It assigns no new
addon-capable Luau scripting API version.

<!-- END RECORD FoundationB.md -->

<!-- BEGIN RECORD FoundationC.md -->

<a id="foundationc"></a>

<a id="foundationc-foundation-c---dependency-lifecycle"></a>

# Foundation C - dependency lifecycle

Foundation C implements the D14 dependency lifecycle above the qualified
[Foundation A](#foundationa) shared-VM domains and [Foundation B](#foundationb)
provider/package registry. Dependency declarations now control host/runtime
lifecycle. They do not expose one addon's Luau values to another addon.

The provider protocol is `CarbonLuau.Addons` / `1.1`. The existing registration,
status, replacement and unregister response shapes remain unchanged. The protocol
capability string now includes `dependencies`. The public Luau scripting identity
remains `CarbonLuau 0.3.0-experimental`; no addon scripting API version is assigned.
The package schema and native ABI remain unchanged.

<a id="foundationc-graph-and-binding-model"></a>

## Graph and binding model

The registry is bounded to 128 nodes, 32 declared edges per node and therefore at
most 4096 inspected graph edges. Package IDs remain the graph keys; v1 still owns
one registration and informational version per stable ID. Only manifest
declarations create edges. Initialization behavior, caught errors, module loads
and host calls cannot add or remove a dependency relationship.

Each successful activation records, for every present dependency, the exact
committed `(VmGenerationId, DomainLifetimeId)` of the dependency registration.
The binding is current only while that same registration remains Active at both
identities. Validation never resolves an ID to a later domain. Optional absence is
also recorded for the consumer lifetime.

Eligible activation work is selected by canonical package ID. A node becomes
eligible only after every required target is Active, so repeated selection gives a
deterministic required-edge topological order without an unbounded retry queue.
Required-cycle detection follows only required edges and blocks every member of a
required strongly connected component. Optional edges never contribute to that
cycle decision.

<a id="foundationc-loss-and-restoration"></a>

## Loss and restoration

A missing required target blocks before execution. Loss or replacement of the
exact required lifetime retires the direct consumer and then its affected required
subgraph. Unrelated Active registrations remain untouched. Once compatible targets
are Active again, eligible registrations receive one activation attempt in
deterministic topological order. An ordinary failed restoration becomes `Failed`
and is not retried by later unrelated graph activity; explicit replacement or
provider re-registration is required.

Optional absence never blocks. A present optional target binds exactly at consumer
activation. Later appearance does not hot-bind an absent binding. Loss or
replacement leaves the consumer Active and the old binding unavailable/stale;
the consumer is never silently retargeted. These host/runtime facts are not yet
script-visible.

Provider unload removes only registrations owned by that concrete provider
lifetime, propagates required loss, and preserves unrelated and optional consumers.
A provider loaded later registers explicitly, receives fresh tokens and domains,
and may trigger deterministic required restoration. CarbonLuau reload continues to
invalidate all provider tokens under D14.

<a id="foundationc-transactional-replacement"></a>

## Transactional replacement

An active addon's old domain, snapshot and dependency bindings remain committed
while a replacement waits for required targets or initializes. A failed candidate,
including a candidate that would introduce a required cycle, is discarded without
retargeting consumers. A successful candidate commits its new domain and exact
bindings, stales the old lifetime, and reinitializes only affected required
dependents. Optional consumers retain their old domain and stale binding.

The Foundation A publication transaction still covers only CarbonLuau-owned cache
and resource state. Foundation C does not add rollback for arbitrary Luau table or
global mutations. Fatal VM failure still follows D9 global reconstruction.

<a id="foundationc-diagnostics-and-qualification"></a>

## Diagnostics and qualification

The existing nine-field provider status reports missing required IDs, required
cycle/SCC blocking, bounded restoration scheduling/failure, replacement waiting or
failure, and optional absent/stale state. Exact binding inspection remains an
internal test surface; no script-visible dependency handle was added.

The Foundation C fixture covers dependency-free regression, required and optional
presence/absence, no hot-bind, direct/transitive loss, unrelated survival,
topological restoration, one failed restoration attempt, successful/failed and
dependency-changing replacement, required/optional replacement effects, required
and optional cycles, mixed graphs, stale binding rejection, provider unload/reload,
token regressions, 100 repeated replacements, and a reverse-registered 128-node
chain with one 32-edge fan-in node.

Foundation C was qualified on 2026-09-16 from starting baseline
`00ec3dc944b03254064724696f8596fa6637f8c5`:

- Windows 11/MSVC 19.44: all four native CTest fixtures, import policy, managed
  loader/runtime tests, package checks and API checks passed. The final managed
  runtime run included the complete Foundation A/B regression suite and the new
  dependency matrix.
- Ubuntu 24.04/GCC 13.3 in the authorized DockerPC Linux worker: all four native
  fixtures, managed loader/runtime tests and exported-probe check passed.
- Ubuntu ASan/UBSan with leak detection: all four native lifecycle/fault fixtures
  passed on the final source snapshot.
- Stress reached 128 simultaneously registered addon nodes across four providers,
  activated a reverse-registered required chain whose final node declared the
  maximum 32 required dependencies, rejected registration 129, propagated a
  bounded transitive loss, and returned to one root domain and zero addon
  registrations. Fifty consecutive dependency replacements reinitialized the
  required consumer against a fresh exact lifetime each time; the existing 100
  addon replacements and 100 register/activate/unregister cycles also passed.
- Live Carbon 2.0.259.0 on an isolated Windows Rust server loaded separate
  dependency and consumer plugins. Required and optional consumers activated;
  unloading the dependency provider blocked/retired only the required consumer
  while the optional consumer remained Active on domain 8. Explicit provider
  reload created dependency domain 10 and required-consumer domain 12. Replacing
  the dependency created domain 14 and reinitialized only the required consumer
  as domain 16; the optional consumer remained domain 8. The runner shut down the
  server and restored every temporary plugin/package/native deployment.

- Published implementation commit
  [`2f0481bd80505e8d6752060369dbe216393d2b5d`](https://github.com/gmoddev/CarbonLuau/commit/2f0481bd80505e8d6752060369dbe216393d2b5d)
  passed the complete Windows/Linux/sanitizer
  [GitHub Actions matrix](https://github.com/gmoddev/CarbonLuau/actions/runs/35072050975).
  Its separate [documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/35072050885)
  also passed. Compile-only CI is not the live Carbon evidence above.

<a id="foundationc-deliberately-deferred"></a>

## Deliberately deferred

Foundation C does not implement `require("@addon")`, `require("@addon/path")`,
public module exports, `addon:IsDependencyAvailable`, script-visible dependency
handles, root-to-addon imports, provider capabilities, version ranges or solving,
multiple versions/instances, downloads/registries, or any Foundation D work.

<!-- END RECORD FoundationC.md -->

<!-- BEGIN RECORD FoundationD.md -->

<a id="foundationd"></a>

<a id="foundationd-foundation-d--public-addon-modules"></a>

# Foundation D — public addon modules

Foundation D implements the first Luau-facing addon composition layer on the
qualified Foundation C graph. It is a post-v0.3.0 development feature, not part of
the published `v0.3.0` artifacts and not an assignment of a public addon scripting
API version.

Subsequent Foundation E qualification assigns this additive surface to
`CarbonLuau 0.4.0-experimental`; the statements below preserve Foundation D's
status at its tested revision.

<a id="foundationd-package-exports"></a>

## Package exports

Archive manifests may add two schema-1 fields:

```json
{
  "schema": 1,
  "id": "creator.economy",
  "version": "1.0.0",
  "main": "api",
  "publicModules": ["types", "format/money"],
  "dependencies": {
    "required": ["creator.database"],
    "optional": ["creator.analytics"]
  }
}
```

`main` and every `publicModules` item are canonical logical module paths without
an extension. Each must name an existing `.luau` source in the immutable package
snapshot. Duplicate, missing, absolute, dot-relative, traversal, backslash,
noncanonical or over-bound declarations are rejected before registration. `main`
is automatically public and must not also appear in `publicModules`.

`init.luau` remains the activation entrypoint. Importing a package never executes
`init.luau`; it lazily executes only the selected module after Foundation C has
already activated the dependency lifetime.

<a id="foundationd-resolution"></a>

## Resolution

Resolution is intentionally narrow:

```lua
require("private/util")       -- current defining addon's private namespace
require("@creator.economy")  -- exact dependency binding's main module
require("@creator.economy/api") -- exact exported module
```

Unqualified names retain the Phase 2/Foundation A behavior. Package IDs are the
declared dependency keys, not aliases. There is no `./`, `../`, filesystem
fallback, global search, undeclared lookup, user alias or dependency-handle
`Require` method. `@id` raises a controlled ordinary error when the bound package
has no `main`; `@id/path` raises one when the path is not explicitly public.

The native resolver uses the consumer domain's immutable Foundation C binding to
the exact target domain lifetime. It does not resolve the ID again. An absent,
lost or replaced target therefore rejects new imports; it never redirects an old
consumer to a replacement.

<a id="foundationd-shared-values-and-publication"></a>

## Shared values and publication

Public access calls the same native module loader and cache used by local access.
The cache identity remains VM generation, defining domain lifetime and logical
module path. No copy, proxy or RPC layer is introduced. Two consumers receive the
same ordinary Luau value by reference, including shared mutable tables and
closures retaining the defining module's private environment.

All existing module behavior remains unchanged: first return only, nil/no return
becomes `true`, extra returns are ignored, successful results cache by reference,
failures retry, cycles are controlled, depth is 32 and initialization cannot
yield.

Foundation A publication scopes also remain authoritative across a domain
boundary. A failed first public load publishes neither its cache candidate nor
its CarbonLuau-owned tasks/listeners, even when caught. A successful foreign load
inside provisional addon initialization merges into that candidate and publishes
only on candidate commit. Candidate failure discards both. Ordinary Luau memory
mutations are not rolled back.

<a id="foundationd-addon-context"></a>

## Addon context

Addon entrypoints and module environments receive a readonly `addon` table:

| Member | Behavior |
|---|---|
| `addon.Id` | Stable canonical package ID for the defining domain |
| `addon.Version` | Informational manifest version for that domain lifetime |
| `addon:IsDependencyAvailable(id)` | `true` only when this domain declared `id` and its exact binding is currently active |

Undeclared and activation-time-absent optional IDs return `false`.
Noncanonical arguments raise a controlled error. Availability is local to the
current addon's binding; it is not global package discovery. A method retained
from a retired addon still fails its domain-lifetime check.

<a id="foundationd-lifetime-behavior"></a>

## Lifetime behavior

Ordinary values already returned from A1 may remain usable after A1 retirement
while the VM survives. Captured host-backed A1 facades still fail closed. An old
consumer cannot make a fresh import through its stale A1 binding and no value
silently targets A2. Foundation C reconstructs eligible required consumers, which
then bind and resolve A2. Optional consumers keep their existing domain and stale
binding until explicitly reconstructed.

<a id="foundationd-identities-and-deferred-work"></a>

## Identities and deferred work

Foundation D adds native ABI `1.4` and provider protocol
`CarbonLuau.Addons` / `1.2` with the `public-modules` capability. Package schema
remains 1. The released gameplay scripting API remains
`CarbonLuau 0.3.0-experimental`; the addon-capable scripting identity is still
unassigned and these development sources are not a public addon release.

Provider C# capabilities, root-to-addon imports, addons depending on root,
version solving, multiple instances, downloads/registries, restricted exposure,
async capabilities and Foundation E remain deferred.

<a id="foundationd-qualification-surface"></a>

## Qualification surface

The Foundation D fixtures cover manifest bounds, local-require regression,
main/path imports, exact public/private visibility, shared identity and mutable
state, defining environments, nil normalization, retry, runtime cycle/depth/yield
guards, caught failure rollback, provisional foreign commit/rollback, A1/A2
replacement behavior, retained pure values versus stale facades,
`IsDependencyAvailable`, repeated replacement and Foundation A–C regressions.

<a id="foundationd-qualification-evidence"></a>

## Qualification evidence

Foundation D was qualified on 2026-09-16 from starting commit
`d5a17817fb0c1497cac22fbf2352196eb4444127`. The tested implementation revision
is `2e635c3136d0cd05ee5f4520271068abcc0f864a`.

- Windows: the established worker release matrix passed the MSVC Release build,
  all four native CTest fixtures, dynamic CRT rejection, the complete managed
  runtime/addon suite, 100 loader cycles, packaging and API documentation checks.
- Linux: the established worker matrix passed the Ubuntu 24.04 GCC 13.3 Release
  build, all four native CTest fixtures, the complete Mono runtime/addon suite,
  exported-package probe and packaging checks.
- Sanitizers: ASan, UBSan and leak detection passed all four native fixtures on
  the Linux worker.
- Live Carbon: the isolated Windows server passed initial public-module import,
  addon identity and availability checks, required-loss propagation, optional
  preservation, required-consumer reconstruction and replacement generation 2.
  The retained optional consumer did not hot-rebind.
- GitHub Actions: [Phase 0 through 3 run 35078222710](https://github.com/gmoddev/CarbonLuau/actions/runs/35078222710)
  passed Windows, Ubuntu and sanitizer jobs; [Documentation run
  35078222868](https://github.com/gmoddev/CarbonLuau/actions/runs/35078222868)
  passed.

The worker scripts retain their historical Foundation B/C names because they are
the repository's established full-platform runners. Their Foundation D runs used
fresh source and build directories. The live server was stopped and its fixture
state restored after qualification. No authenticated real-client behavior is
introduced by Foundation D, so that previously approved deferred surface remains
outside this qualification.

<!-- END RECORD FoundationD.md -->

<!-- BEGIN RECORD FoundationE.md -->

<a id="foundatione"></a>

<a id="foundatione-foundation-e-addon-production-qualification"></a>

# Foundation E: addon production qualification

Verdict: **PASS** for the first experimental public addon surface.

Foundation E adds no major addon capability. It qualifies Foundations A through
D at representative scale, corrects two scheduler defects found by inspection
and testing, assigns the public experimental identities, and records the support
boundary for the v0.4.0 candidate.

Starting commit: `d9bc2bba7844c0a837d1af80f7e88ee18bdeb5ba`.
Implementation checkpoint: `14d920c69cb343d0bab15df40b9c1d24fb48eb2d`.
Qualified implementation and documentation commit:
`8e0754eff36216230782b41acd4bcc44d4f0db69`.
Qualification date: 2026-09-17.

<a id="foundatione-scheduler-closure"></a>

## Scheduler closure

Native ready-work selection now rotates across domains while preserving FIFO
order inside each domain. Managed facade delivery also rotates across the root
and addon domains under one global maximum of 256 deliveries and the existing
frame stopwatch. A saturated addon therefore does not receive an independent
frame budget and cannot indefinitely hold the front of the global scheduler.

Future-only work exposes its next monotonic due time. CarbonLuau schedules one
host timer for that due time instead of requesting a frame drain on every frame.
An earlier due task invalidates the old wake token and schedules the earlier one.
The no-host-driven-reentrant-VM-entry rule remains unchanged.

<a id="foundatione-scale-and-resource-measurements"></a>

## Scale and resource measurements

The managed qualification fixture creates representative root-only, 1, 10, 50
and 100-addon configurations. Each addon owns state, a listener and a one-day
delayed task. Measurements include VM allocator bytes, immutable snapshots,
managed memory, process RSS, GC collections, activation/replacement latency,
service order and 10,000 idle readiness checks.

Windows x64 results on DockerPC/HostPC:

| Addons | VM bytes | Snapshot bytes | Managed bytes | Process RSS | Activation ms | Replacement ms | p50/p95/p99/max service ordinal |
|---:|---:|---:|---:|---:|---:|---:|---|
| 0 | 581,776 | 0 | 976,112 | 45,428,736 | 0.000 | 0.000 | 0/0/0/0 |
| 1 | 630,880 | 148 | 978,960 | 45,944,832 | 0.734 | 0.371 | 1/1/1/1 |
| 10 | 681,073 | 2,119 | 999,144 | 48,484,352 | 3.661 | 0.354 | 5/10/10/10 |
| 50 | 1,143,297 | 10,879 | 1,089,600 | 59,797,504 | 18.829 | 0.433 | 25/48/50/50 |
| 100 | 1,949,081 | 21,829 | 1,203,808 | 74,612,736 | 44.585 | 0.633 | 50/95/99/100 |

Ubuntu 24.04 container results on the same worker host:

| Addons | VM bytes | Snapshot bytes | Managed bytes | Process RSS | Activation ms | Replacement ms | p50/p95/p99/max service ordinal |
|---:|---:|---:|---:|---:|---:|---:|---|
| 0 | 581,776 | 0 | 4,477,424 | 81,281,024 | 0.024 | 0.000 | 0/0/0/0 |
| 1 | 630,880 | 148 | 4,482,000 | 81,416,192 | 0.778 | 0.458 | 1/1/1/1 |
| 10 | 681,073 | 2,119 | 4,528,936 | 82,108,416 | 3.609 | 0.475 | 5/10/10/10 |
| 50 | 1,143,297 | 10,879 | 4,618,424 | 89,006,080 | 21.840 | 0.556 | 25/48/50/50 |
| 100 | 1,949,081 | 21,829 | 4,765,224 | 92,770,304 | 45.248 | 0.840 | 50/95/99/100 |

GC deltas remained bounded to the collections induced by fixture setup and
explicit measurement. The 100-addon idle check performed no persistent frame
work and retained exactly one delayed wake. Native/compiler transient memory was
bounded by the clean worker builds and sanitizer jobs; no separate compiler peak
was isolated, so no narrower claim is made.

The live Carbon 100-addon fixture measured 1,752,713 VM bytes, 10,883 snapshot
bytes and 70.161 ms activation. The complete Rust process was about 3.02 GiB,
with a 38,748,160-byte RSS increase from the pre-scale sample. That process value
includes Rust, Unity, Carbon, Mono, map state and all plugins; it is not attributed
to the Luau heap.

<a id="foundatione-memory-policy-decision"></a>

## Memory-policy decision

The 64 MiB VM default remains appropriate for the qualified addon configuration.
Representative 100-addon use remained below 2 MiB. A separate pressure fixture
retained fifteen 4 MiB Luau buffers, reached 63,628,320 allocator bytes, rejected
the next activation with controlled `MEMORY_LIMIT`, and then successfully ran an
unrelated root operation in the same generation.

The clamp remains 16..256 MiB. There is one VM-wide cap and no per-addon hard
heap quota, retained-memory guarantee or whole-process memory limit.

<a id="foundatione-fairness-and-bounded-work"></a>

## Fairness and bounded work

- A domain with 256 CPU-bearing ready callbacks did not starve ten unrelated
  domains. All ten made progress in the first service frame on Windows and Linux.
- Event fanout produced one bounded delivery per listening addon. The global
  managed drain returned at most 256 results.
- Native two-domain coverage verifies round-robin progress while retaining
  per-domain due-time and sequence order.
- Queue limits, the global frame stopwatch and the global no-reentrant-entry rule
  remain in force.
- One-day delayed tasks did not cause persistent per-frame work.

<a id="foundatione-package-and-parser-boundaries"></a>

## Package and parser boundaries

The fixture accepts exactly 64 KiB for one source and rejects the next byte. It
accepts exactly 32 MiB of aggregate immutable snapshots and rejects the next
package. A forged ZIP uncompressed-size field cannot bypass the streamed actual
decompressed-byte check.

The qualified schema-1 bounds are 4 MiB archive, 8 MiB expanded data, 64 KiB
manifest, 64 KiB each source, 4 MiB aggregate source per package, 256 modules,
512 entries, 32 dependencies, 128 registrations, 32 registrations per provider
and 32 MiB aggregate snapshots. Existing malformed UTF-8/JSON, duplicate-key,
path, entry-type, encryption, nested-archive and export validation regressions
also passed.

<a id="foundatione-live-carbon-lifecycle"></a>

## Live Carbon lifecycle

The isolated Windows server ran Carbon 2.0.259.0 and Rust protocol 2633.288.1.
The current-run fixture passed:

- provider unload from Registered, Blocked, Active and Failed states;
- stale token rejection, idempotent unregister and explicit re-registration;
- same-provider replacement and fresh domain publication;
- required dependency loss, restoration and dependent reconstruction;
- dependency replacement without optional-consumer hot rebinding;
- 100 additional active addon domains in the shared VM;
- complete CarbonLuau teardown with those addons while providers stayed loaded;
- native DLL unmapping and continued Rust server responsiveness;
- CarbonLuau reload, rejection of every old provider token and explicit fresh
  registration by all retained providers.

The server stopped cleanly and the runner restored every deployed file. Server
log SHA-256: `4948d610e6f29483660c52fbdf5b241bc72daf9ddc9b0f57d14816c1f94d3a2b`.

<a id="foundatione-platform-and-regression-matrix"></a>

## Platform and regression matrix

- Windows 11 build 26200: MSVC Release build, four native CTest targets, CRT
  import check, complete managed runtime/addon suite, loader cycles, package and
  API checks passed.
- Ubuntu 24.04 / GCC 13.3: Release build, four native targets, complete Mono
  runtime/addon suite, loader and exported-symbol checks passed.
- Linux Debug with `CARBONLUAU_SANITIZE=ON`: all four native targets passed with
  ASan, UBSan and leak detection enabled.
- Foundations A through D and Phase 0 through 3 regression fixtures passed on
  both worker platforms.

Windows worker log SHA-256:
`436622734d1573ca836e9ca15a4c5a701791702950027901020de685aa63654f`.
[GitHub Actions validation run 35273700477](https://github.com/gmoddev/CarbonLuau/actions/runs/35273700477)
passed Windows, Ubuntu and sanitizer jobs. [Documentation run 35273700388](https://github.com/gmoddev/CarbonLuau/actions/runs/35273700388)
successfully deployed the hosted documentation.

<a id="foundatione-public-identities-and-remaining-limits"></a>

## Public identities and remaining limits

The v0.4.0 candidate assigns scripting API `CarbonLuau 0.4.0-experimental`.
Package version is `0.4.0`; native ABI is `1.4`; provider protocol is
`CarbonLuau.Addons` / `1.2`; package schema is `1`; the Luau pin remains
`c6b830185af962c82003f86784e2fe036357c830`. None is stable or 1.0.

Provider-defined C# capabilities, root-to-addon imports, addons depending on the
operator root, version ranges/solving, multiple versions or instances, downloads,
registries, lockfiles, restricted exposure profiles and async capabilities remain
deferred. No work on those features began in Foundation E.

<!-- END RECORD FoundationE.md -->

<!-- BEGIN RECORD FoundationF.md -->

<a id="foundationf"></a>

<a id="foundationf-foundation-f-invariant-owner-architecture"></a>

# Foundation F: invariant-owner architecture

Verdict: **PASS**.

Foundation F is a behavior-preserving decomposition of the v0.4.0 candidate.
It assigns implementation owners to the major runtime, lifecycle and security
invariants without changing scripting behavior, the ABI, provider protocol,
package schema, limits or the pinned Luau revision.

Starting commit: `f54d7678aa56bea1ad5104ed31350cd13e409169`.
Qualified implementation commit: `0530e578c9903888440de16918134ec62f1ecfe6`.

<a id="foundationf-managed-ownership"></a>

## Managed ownership

| Component | Primary ownership |
|---|---|
| `Runtime/RuntimeConfig.cs` | Validated runtime policy values and managed ABI value types |
| `Runtime/NativeLibraryLoader.cs` | Explicit platform library loading, probe validation and unload |
| `Runtime/NativeRuntime.cs` | Native ABI binding, host lifetime, owner-thread and reentry checks |
| `Runtime/RuntimeGeneration.cs` | Managed VM handle and generation lifetime |
| `Runtime/RuntimeHost.cs` | Legacy generation replacement and recovery coordination |
| `Scripts/ScriptSnapshot.cs` | Script path confinement and immutable source snapshots |
| `Scripts/NativeScripts.cs` | Domain, module and scheduler ABI adaptation |
| `Scripts/RuntimeDomain.cs` | Managed domain lifetime and stale-domain behavior |
| `Scripts/ScriptHost.cs` | Root admission, recovery and bounded global draining |
| `Facade/PlayerDirectory.cs` | Exact reconnect-safe player identity |
| `Facade/CommandRegistry.cs` | Command publication contract |
| `Facade/FacadeSession.cs` | Domain-bound facade session and transactional host resources |
| `Facade/NativeFacade.cs` | Native host-call bridge binding and callback roots |
| `Addons/AddonPackage.cs` | Package policy, ZIP/JSON parsing and immutable package snapshots |
| `Addons/AddonRegistry.cs` | Provider ownership, registration states and activation coordination |
| `Addons/DependencyGraph.cs` | Exact dependency bindings, loss propagation, cycles and restoration |
| `Addons/AddonActivation.cs` | Transactional addon-domain activation and retirement |

The Carbon plugin remains the composition root. Addons depend on script-domain
and publication behavior, scripts depend on runtime/domain behavior, and facade
objects depend on domain lifetime. No service locator, reflection framework or
new global managed registry was introduced.

<a id="foundationf-native-ownership"></a>

## Native ownership

| Component | Primary ownership |
|---|---|
| `Runtime.cpp` | Exported C ABI validation and execution coordination |
| `runtime/RuntimeInternal.hpp` | Private subsystem types and narrow cross-component declarations |
| `runtime/VmRegistry.cpp` | VM handle registry, generation identities and owner-thread lookup |
| `runtime/VmState.cpp` | VM allocation, sandbox initialization, domain teardown and thread cleanup |
| `runtime/Publication.cpp` | Nested cache, callback and facade publication commit/rollback |
| `runtime/Deadline.cpp` | Private uncatchable deadline interrupt |
| `scripts/Compiler.cpp` | Synchronous pinned Luau compiler invocation and compile options |
| `scripts/ModuleLoader.cpp` | Module resolution, visibility, cache, cycles, depth and task queues |
| `facade/FacadeBridge.cpp` | Bootstrap installation, host codec boundary and event admission |

`Runtime.cpp` no longer includes private implementation blobs. The former
`Scripts.inl` and `Facade.inl` implementations are independently compiled
subsystems. The C ABI still holds the same recursive registry lock and preserves
the same serialized owner-thread execution model.

Sandbox initialization remains with VM state because it is part of state
construction and fatal retirement. Domain destruction also remains with VM state
because it releases Luau references owned by that state. These are deliberate
deviations from a one-file-per-concept example, not missing owners.

<a id="foundationf-invariant-map"></a>

## Invariant map

| Invariant | Implementation owner |
|---|---|
| VM handle validity and generation identity | `VmRegistry.cpp`, `RuntimeGeneration.cs` |
| Domain lifetime and stale validation | `VmState.cpp`, `RuntimeDomain.cs` |
| Deadline cancellation | `Deadline.cpp` and the execution boundary in `Runtime.cpp` |
| Sandbox initialization | `VmState.cpp` |
| Source compilation | `Compiler.cpp` |
| Module resolution/cache/cycles | `ModuleLoader.cpp` |
| Provisional publication | `Publication.cpp`, `FacadeSession.cs` |
| Script path confinement | `ScriptSnapshot.cs` |
| Provider ownership and registration | `AddonRegistry.cs` |
| Dependency lifetime bindings | `DependencyGraph.cs` |
| Player reconnect identity | `PlayerDirectory.cs` |
| Command collision/publication | `CommandRegistry.cs`, `FacadeSession.cs` |
| Facade codec and validation | `FacadePolicy.cs`, `FacadeBridge.cpp` |

<a id="foundationf-remaining-concentrations"></a>

## Remaining concentrations

`Runtime.cpp` still owns source-load, callback-resume and thread-resume ABI
coordination. `FacadeSession.cs` retains listeners, commands and publication
checkpoints because they share one atomic host-resource transaction.
`RuntimeInternal.hpp` exposes the private VM/domain data model to native
subsystems, and the bounded process-wide VM registry and recursive mutex remain
global native state. These are explicit remaining coupling points for review.

This organization is not itself a security guarantee. The guarantee continues
to come from the canonical invariants and qualification tests.

<a id="foundationf-compiler-boundary-and-foundation-g"></a>

## Compiler boundary and Foundation G

All production `Luau::compile` calls now pass through `scripts/Compiler.cpp`.
The boundary deliberately remains synchronous, uses the existing optimization
and debug options, runs under the existing serialized execution path and returns
the same bytecode representation. No compiler worker, deadline, process,
transport or lock-scope change was introduced. Foundation G can begin at this
owner without first extracting compilation from runtime, module and facade code.

<a id="foundationf-identity-and-scope"></a>

## Identity and scope

Package `0.4.0`, scripting API `CarbonLuau 0.4.0-experimental`, native ABI 1.4,
provider protocol `CarbonLuau.Addons` / 1.2, package schema 1 and Luau revision
`c6b830185af962c82003f86784e2fe036357c830` remain unchanged. Foundation G and
compiler containment were not started.

<a id="foundationf-qualification"></a>

## Qualification

The clean DockerPC Windows worker passed all four native targets, static MSVC
runtime import checks, the complete managed runtime/addon suite, 100 managed
native load/unload cycles, deterministic packaging, API documentation checks and
the architecture guard. The representative 100-addon result retained the same
1,949,081 VM bytes and service ordinals 50/95/99/100; all unrelated saturated
scheduler consumers progressed in the first service frame. Windows log SHA-256:
`662eaa8d63c4610875231e377741b5b0642c02ab3be73767d9dafb6416da9a36`.

The clean Ubuntu 24.04 container passed all four release native targets, the
complete Mono managed runtime/addon suite, loader/export checks and the same
representative 100-addon fixture. A separate Debug build passed all four targets
with ASan, UBSan and leak detection enabled. Linux log SHA-256:
`e62b14b1093258b794df940541f9cbe31542e181dd7f16db274300bc723516af`.

The release identity, intended package contents and byte-for-byte deterministic
Windows release bundle check passed using the qualified native DLL. No live
Carbon rerun was required because this phase changed no host integration or
runtime semantics; Foundation E's live evidence remains scoped to its tested
commit rather than being relabeled as Foundation F evidence.

[GitHub Actions validation run 35278320215](https://github.com/gmoddev/CarbonLuau/actions/runs/35278320215)
passed the Windows, Ubuntu and sanitizer jobs for the qualified implementation.
[Documentation run 35278319941](https://github.com/gmoddev/CarbonLuau/actions/runs/35278319941)
successfully deployed the Foundation F documentation to GitHub Pages.

<!-- END RECORD FoundationF.md -->

<!-- BEGIN RECORD FoundationG.md -->

<a id="foundationg"></a>

<a id="foundationg-foundation-g-compiler-containment"></a>

# Foundation G compiler containment

Foundation G contains every production Luau compilation in a shipped helper
process. It is an availability and provenance change for the v0.4.0 candidate,
not a scripting API, native ABI, provider protocol, package schema or Luau pin
change.

Starting commit: `95214f53dddbd5c408bf4259d18e2dedcfe94846`.

<a id="foundationg-original-risk-and-pinned-compiler-findings"></a>

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

<a id="foundationg-selected-architecture"></a>

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

<a id="foundationg-isolation-and-lifecycle"></a>

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

<a id="foundationg-runtime-and-publication-semantics"></a>

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

<a id="foundationg-qualification-fixtures"></a>

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

<a id="foundationg-qualification-results"></a>

## Qualification results

Verdict: **PARTIAL**, solely because Foundation G Windows live/local
qualification is deferred. Every available Linux, sanitizer, managed regression,
packaging and live Carbon gate passed. The starting revision was
`95214f53dddbd5c408bf4259d18e2dedcfe94846`; the exact implementation revision
requiring the later supplemental Windows live/local run is
`e3025401c3085f0552bbfe3d045c3143c4ded005`.

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

Hosted CI passed at the implementation revision in
[CarbonLuau validation run 35423984097](https://github.com/gmoddev/CarbonLuau/actions/runs/35423984097):
Ubuntu 24.04 release/native/managed/package jobs, ASan/UBSan/leak detection and
hosted Windows release/native/managed/package jobs were all green. The hosted
Windows job exercised worker creation, timeout, crash/restart, protocol rejection,
the job memory limit, managed integration and release packaging. It is valuable
hosted-runner evidence, but not Windows live/local Carbon qualification.

<a id="foundationg-windows-foundation-g-deferral"></a>

### Windows Foundation G deferral

Foundation G Windows live/local qualification is **DEFERRED / UNQUALIFIED** by
task-owner decision because the Windows worker is unavailable. No DockerPC access
or restoration was attempted. Historical Windows evidence for Foundations A-F
remains valid for those revisions but does not qualify this compiler worker.
Hosted Windows CI passed for `e3025401c3085f0552bbfe3d045c3143c4ded005`
as recorded above; it is separate evidence and is not a substitute for this
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

<a id="foundationg-scope-and-limitations"></a>

## Scope and limitations

The one-second deadline is an availability bound, not a promise that compilation
normally consumes that time. The worker's 256 MiB limit is not the Luau VM cap or
a whole-server RSS cap. OS process creation and termination remain operating
system services, and the worker is trusted CarbonLuau code rather than an
adversarial tenant sandbox.

No multiple-VM trust tiers, immutable exports, provider capabilities, restricted
exposure, gameplay APIs, package solving/downloads or other post-v0.4 feature was
started.

<!-- END RECORD FoundationG.md -->

