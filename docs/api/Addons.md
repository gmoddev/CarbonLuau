# Addon composition (development)

This page documents the implemented Foundation D source surface. It is not part
of the published v0.3.0 artifacts and does not yet have an assigned public addon
scripting API identity. See [Foundation D](../FoundationD.md) for lifetime,
qualification and version details.

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
