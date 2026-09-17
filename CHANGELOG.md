# Changelog

All notable public changes to CarbonLuau are recorded here.

## Unreleased

### Added

- Experimental addon composition under scripting API `0.4.0-experimental`.
- Bounded provider-owned schema-1 packages and immutable source snapshots.
- Required and optional dependency lifetimes with exact replacement bindings.
- Explicit public modules and `require("@id")` / `require("@id/path")`.
- Readonly `addon.Id`, `addon.Version` and `addon:IsDependencyAvailable(id)`.
- Cross-domain fair scheduling and delayed wakeups without persistent idle-frame work.

### Validation

- Qualified root-only, 1, 10, 50 and 100-addon configurations on Windows and Linux.
- Qualified shared-heap exhaustion, parser boundaries, scheduler saturation,
  provider lifecycle, CarbonLuau reload with providers retained, and native teardown.
- Passed Windows, Ubuntu, ASan, UBSan and leak-detection regressions.

### Limits

- The addon API is experimental, not stable or 1.0.
- Provider-defined C# capabilities, root imports, downloads, registries, version
  solving, multiple instances, restricted exposure and async capabilities remain deferred.
- The 64 MiB cap is shared across the VM; there is no per-addon hard heap isolation.

## 0.3.0

First qualified experimental release, published as prerelease `v0.3.0`.

### Added

- Embedded, pinned Luau compiler and VM behind a Windows/Linux native bridge.
- Sandboxed server-side Luau with bounded VM memory and cooperative execution
  deadlines.
- Transactional script generations, atomic reload, controlled modules and
  `require`.
- Bounded `task.spawn`, `task.defer` and `task.delay` scheduling.
- Experimental `game:GetService` facade with Players, Player proxies,
  PlayerAdded/PlayerRemoving, Signals/Connections and Commands.
- Carbon permission enforcement, `Player:HasPermission`, bounded
  `Player:SendMessage`, and `carbonluau.status`/`carbonluau.reload`
  administration.

### Security / containment

- Allowlisted libraries with no script filesystem, network, reflection, raw host
  object or arbitrary console access.
- Generation-owned callbacks, listeners, commands and proxies are invalidated on
  successful reload or unload.
- Bounded queues, host payloads, log output and Lua VM allocations; timeout
  recovery is limited by the documented one-attempt policy.

### Validation

- Native, managed, fault-path and sanitizer suites pass on Windows x64 and glibc
  Linux x64 CI.
- Live Carbon qualification passed on controlled Windows and Linux Rust workers,
  including reload, rejected candidates, churn, pressure, lifecycle, profiler,
  measured dispatch latency and 30-minute command-bearing soaks.

### Known limitations

- The scripting API remains `0.3.0-experimental`; this is not a stable 1.0 API or
  a claim of production readiness.
- Authenticated Steam/network establishment, visible client message receipt,
  network-driven player lifecycle events and real same-account reconnect are
  unqualified.
- Shockbyte full-runtime behavior is unqualified and unsupported beyond the
  historical Phase 0 native-load probe.
- Compatibility is limited to the recorded Rust, Carbon, OS and architecture
  envelope; it is not universal compatibility.
- Deadlines are cooperative, not hard real-time guarantees. The VM allocation cap
  is not whole-process or whole-server memory containment.
- Items, `Items:Exists` and `Player:GiveItem` are deferred under D13 and are not
  shipped.
