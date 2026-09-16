# Changelog

All notable public changes to CarbonLuau are recorded here.

## Unreleased

### Internal

- Adopted the canonical addon architecture amendments and added Foundation A:
  host, VM-generation and domain-lifetime identities; shared-VM domain
  isolation; domain-bound ownership and admission; and transactional module
  cache/resource publication. This is internal infrastructure only and does not
  expose public addon loading or assign an addon scripting API version.
- Added Foundations B–D development infrastructure: bounded provider/package
  registration, exact dependency lifetimes, explicit public exports,
  package-qualified `require`, readonly addon metadata and dependency availability.
  Foundation D uses additive native ABI 1.4 and provider protocol 1.2; no public
  addon scripting API version or release support claim is assigned yet.

## 0.3.0

First qualified experimental release candidate. The intended future tag is
`v0.3.0`; no release tag has been created yet.

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
