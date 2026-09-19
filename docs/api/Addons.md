# Addon composition

Availability: experimental API `0.4.0-experimental`. This surface is in the
qualified v0.4.0 candidate and is not part of published v0.3.0 artifacts. See
[Foundation E](../FoundationE.md) for qualification and [addon providers](Addon-Providers.md)
for package registration.

## Manifest

An addon is an immutable schema-1 source snapshot. Archive packages use this
shape:

```json
{
  "schema": 1,
  "id": "economy",
  "version": "1.0.0",
  "main": "api",
  "publicModules": ["formatting"],
  "dependencies": {
    "required": ["database"],
    "optional": ["metrics"]
  }
}
```

`init.luau` is the activation entrypoint. `main` and every `publicModules` value
are canonical module paths without `.luau`; `main` is automatically public.
Importing a module never activates the package or executes `init.luau`.

An addon's archive manifest declares `main` and/or `publicModules`. Consumers may
import only through their own declared Foundation C dependencies:

```lua
-- creator.shop/addon.json declares creator.economy as required.
local Economy = require("@creator.economy")
local Money = require("@creator.economy/format/money")

print(addon.Id, addon.Version)
if addon:IsDependencyAvailable("creator.analytics") then
    local Analytics = require("@creator.analytics")
end
```

`require("private/util")` remains local to the defining addon. `require("@id")`
uses the exact bound dependency's `main`; `require("@id/path")` requires an exact
public export. Missing main, private paths, undeclared IDs and stale/absent
bindings raise catchable ordinary Luau errors. Imports never activate an addon.

Public modules return the defining addon's ordinary cached same-VM value. Tables
and mutable state are shared by reference, and closures retain the defining
module environment. Do not treat an imported table as a revocable capability:
already-retained plain Luau values can outlive the defining addon domain. Host
operations captured from that domain still validate its exact lifetime and fail
after retirement.

`addon:IsDependencyAvailable(id)` returns `false` for undeclared, absent or stale
bindings and `true` only for the current exact active binding. Optional
dependencies do not hot-bind or hot-rebind.

## Limits and deferred work

Packages are provider-registered snapshots. There is no addon-directory scan,
download registry, lockfile, version solver or parallel package version. IDs are
one or two lowercase ASCII segments such as `economy` or `creator.economy`.
Provider-defined C# capabilities, root-to-addon imports, addons depending on the
operator root, restricted exposure profiles and async capabilities are deferred.

The [economy and shop examples](https://github.com/gmoddev/CarbonLuau/tree/main/examples/addons) show the
complete required and optional import pattern.
