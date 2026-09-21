# Changelog

All notable public changes to CarbonLuau are recorded here.

## Unreleased

### Added

- Immutable `Vector3` arithmetic and live read-only exact-connection
  `Player.Position` under Player Interaction Foundation 1A.
- Experimental addon composition under scripting API `0.4.0-experimental`.
- Bounded provider-owned schema-1 packages and immutable source snapshots.
- Required and optional dependency lifetimes with exact replacement bindings.
- Explicit public modules and `require("@id")` / `require("@id/path")`.
- Readonly `addon.Id`, `addon.Version` and `addon:IsDependencyAvailable(id)`.
- Cross-domain fair scheduling and delayed wakeups without persistent idle-frame work.
- Experimental retained GUI with ScreenGui, Frame, TextLabel and TextButton.
- Roblox-familiar UDim, UDim2, Vector2 and Color3 values; explicit per-Player
  Show/Hide; bounded synchronization; and secure TextButton.Activated.
- Deterministic UIListLayout and UIPadding with LayoutOrder independent of ZIndex.
- Typed ImageSource values, ImageLabel, ImageButton and secure ImageButton.Activated.
- Retained ScrollingFrame configuration with explicit CanvasSize and client-local
  scroll position.
- Runnable root and addon GUI examples, including shared and per-player trees.
- Focused runnable Foundation 2 examples for layout, images, scrolling, shared
  retained trees and cloned per-Player state.
- Deterministic `UIGridLayout`, bounded `Frame.ClipsDescendants`, immutable
  `GuiFont` values, retained text fonts and exact-Player one-way scroll effects.
- Focused Foundation 3 examples for horizontal and vertical grids, padding,
  scrolling grids, nested clipping, font patching, isolated Player scrolling
  and a combined screen.

### Validation

- Qualified Vector3 bounds/arithmetic, cross-domain value lifetime and
  Player.Position lifecycle/provisional behavior on the Linux runtime and
  sanitizer matrix; hosted Windows evidence is recorded separately.
- Qualified root-only, 1, 10, 50 and 100-addon configurations on Windows and Linux.
- Qualified shared-heap exhaustion, parser boundaries, scheduler saturation,
  provider lifecycle, CarbonLuau reload with providers retained, and native teardown.
- Passed Windows, Ubuntu, ASan, UBSan and leak-detection regressions.
- Qualified GUI ownership, publication, replacement, VM recovery, backend fault
  convergence, action-token rejection, lifecycle teardown and bounded stress.
- Qualified the implemented Foundation 2 surface at 1, 10, 50 and 100 viewers
  with a 254-element rich screen inside the 257-element projection bound.
- Qualified the combined Foundation 3 server-side surface at 1, 10, 50 and 100
  viewers with a 252-element screen, transactional publication, lifecycle,
  replacement, recovery, backend failure and bounded stress coverage.

### Limits

- The addon API is experimental, not stable or 1.0.
- Provider-defined C# capabilities, root imports, downloads, registries, version
  solving, multiple instances, restricted exposure and async capabilities remain deferred.
- The 64 MiB cap is shared across the VM; there is no per-addon hard heap isolation.
- Authenticated-client visual layout, cursor behavior, actual click receipt and
  client reconciliation remain unqualified. This does not block the experimental
  API identity and is not evidence that those client-observed outcomes passed.
- TextBox is not implemented because the current host transport cannot preserve
  submitted text exactly. Automatic sizing/canvas sizing, CanvasPosition,
  advanced styling and hover/focus events remain deferred.
- Foundation 3 clipping, font rendering and actual scroll behavior remain
  authenticated-client unqualified. Windows native/local Foundation 3
  qualification is deferred; hosted Windows CI is separate evidence.

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
