# Foundation D — public addon modules

Foundation D implements the first Luau-facing addon composition layer on the
qualified Foundation C graph. It is a post-v0.3.0 development feature, not part of
the published `v0.3.0` artifacts and not an assignment of a public addon scripting
API version.

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

## Lifetime behavior

Ordinary values already returned from A1 may remain usable after A1 retirement
while the VM survives. Captured host-backed A1 facades still fail closed. An old
consumer cannot make a fresh import through its stale A1 binding and no value
silently targets A2. Foundation C reconstructs eligible required consumers, which
then bind and resolve A2. Optional consumers keep their existing domain and stale
binding until explicitly reconstructed.

## Identities and deferred work

Foundation D adds native ABI `1.4` and provider protocol
`CarbonLuau.Addons` / `1.2` with the `public-modules` capability. Package schema
remains 1. The released gameplay scripting API remains
`CarbonLuau 0.3.0-experimental`; the addon-capable scripting identity is still
unassigned and these development sources are not a public addon release.

Provider C# capabilities, root-to-addon imports, addons depending on root,
version solving, multiple instances, downloads/registries, restricted exposure,
async capabilities and Foundation E remain deferred.

## Qualification surface

The Foundation D fixtures cover manifest bounds, local-require regression,
main/path imports, exact public/private visibility, shared identity and mutable
state, defining environments, nil normalization, retry, runtime cycle/depth/yield
guards, caught failure rollback, provisional foreign commit/rollback, A1/A2
replacement behavior, retained pure values versus stale facades,
`IsDependencyAvailable`, repeated replacement and Foundation A–C regressions.
Final platform, sanitizer, live Carbon and CI evidence is recorded below after
qualification of the final source revision.
