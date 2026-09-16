# Foundation C - dependency lifecycle

Foundation C implements the D14 dependency lifecycle above the qualified
[Foundation A](FoundationA.md) shared-VM domains and [Foundation B](FoundationB.md)
provider/package registry. Dependency declarations now control host/runtime
lifecycle. They do not expose one addon's Luau values to another addon.

The provider protocol is `CarbonLuau.Addons` / `1.1`. The existing registration,
status, replacement and unregister response shapes remain unchanged. The protocol
capability string now includes `dependencies`. The public Luau scripting identity
remains `CarbonLuau 0.3.0-experimental`; no addon scripting API version is assigned.
The package schema and native ABI remain unchanged.

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

GitHub CI remains required for the published implementation commit. Compile-only
CI is not the live Carbon evidence above.

## Deliberately deferred

Foundation C does not implement `require("@addon")`, `require("@addon/path")`,
public module exports, `addon:IsDependencyAvailable`, script-visible dependency
handles, root-to-addon imports, provider capabilities, version ranges or solving,
multiple versions/instances, downloads/registries, or any Foundation D work.
