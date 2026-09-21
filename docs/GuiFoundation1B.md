# GUI Foundation 1B: retained runtime

Status: **implemented and qualified on the Foundation 1B boundary**.

Starting commit: `b81510c23b25feb1635d9e97a8595c867c056416`.
Implementation and qualification commit: `ef97d59c4bab196a64dcdcf3ef624b769130c3e7`.

Foundation 1B implements D15's server-side retained GUI and Luau object/value
semantics. It deliberately stops before presentations, client identities,
serialization, Rust CUI rendering, dirty synchronization, action tokens or
client event ingress. The implementation is available for qualification on the
development branch but remains outside the assigned `0.4.0-experimental`
release identity.

## Retained owner

`GuiRetainedRegistry` owns one exact `(VmGenerationId, DomainLifetimeId)` and
stores only CarbonLuau state. Each live object has a monotonic `GuiObjectId`, an
explicit class descriptor, typed properties, optional parent, and deterministic
child order. Script-visible `Name` is never identity. Destroyed identities are
never reused, and a retained userdata continues to compare only as the same
ordinary Luau value after its backing object becomes invalid.

`GuiRetainedWorld` owns the shared-VM object count. Each `FacadeSession` owns one
domain registry and disposes it during candidate rejection, addon/root
replacement, domain retirement and full host teardown. The model contains no
Carbon LUI/CUI objects, JSON, viewers, client IDs or render state.

## Luau surface and value types

The trusted build-embedded facade now provides:

```lua
local Gui = game:GetService("Gui")
local Screen = Gui:Create("ScreenGui")
local Frame = Screen:Create("Frame")
```

`ScreenGui`, `Frame`, `TextLabel` and `TextButton` are opaque userdata whose
metamethods retain only a private object identity and call the owning domain's
validated host facade. The implemented object boundary is `Name`, `ClassName`,
`Parent`, `Create`, `GetChildren`, `FindFirstChild`, `IsA`, `Clone`, `Destroy`,
the D15 layout/visual/text properties, and `TextButton.Activated` Signal
presence. Invalid members, types, lifetimes and ranges produce ordinary
catchable Luau errors.

`UDim`, `UDim2`, `Vector2` and `Color3` are immutable userdata with explicit
constructors, read-only fields, component equality, negative-zero
normalization, finite-number rejection and D15 bounds. `Color3.fromRGB` accepts
integer components from 0 through 255. Values have no host lifetime and the VM
uses one shared equality function so values compare component-wise across addon
domains. No arithmetic or wider math API was introduced.

## Parenting and lifecycle

`ScreenGui` remains root-level. Other objects can be detached or parented to a
same-owner `ScreenGui` or GUI object. Assignment validates the complete proposed
move before changing either tree: cycles, depth, child, screen object/button and
aggregate text limits therefore fail without partial mutation. Reparenting
appends to the destination's attachment order, including same-owner moves
between ScreenGuis. Duplicate names are allowed; lookup and `GetChildren` use
attachment order.

`Clone` prevalidates the entire bounded subtree, reserves new monotonic IDs and
then deep-copies classes, public properties and child order. It copies no parent
or Signal connections. `Destroy` detaches and recursively removes the subtree,
disconnects its GUI Signal registrations immediately, and is idempotent.
Every other operation through a destroyed or stale object fails closed.

## Publication journal

The existing D7/D10 native publication scope enlists a facade whenever a
provisional operation performs GUI mutation. The owning registry pushes a
bounded full retained-state checkpoint, while subsequent calls through the same
owning userdata read and mutate the overlay. Nested module publication maps to
nested checkpoints.

Outer commit discards the checkpoint only after the exact owner facade/lifetime
accepts the commit. Rollback restores the prior retained tree, properties,
objects and GUI Signal registrations plus the shared-VM object accounting.
Monotonic object IDs are intentionally not reused after rollback. Ordinary Luau
table mutations remain outside this transaction.

The foreign-owner case uses the same path. If provisional B calls an A-owned
userdata, that userdata remains bound to A's host facade while B's native
publication scope enlists A. B reads A's overlay. B failure restores A; B
success commits the final A state. If A is retired before commit, its facade
rejects the publication control call and the candidate aborts rather than
publishing into a stale owner.

## Bounds

Foundation 1B enforces the validated GUI-1A limits for live objects per screen,
domain and VM; screens per domain; depth; children; buttons; name/text bytes;
aggregate screen text; clone object/depth work; and GUI Signal connections per
button/domain. Transport, presentation, viewer, token, dirty and flush tuning
limits remain dormant because their owning phases are not implemented.

## Signal status

`TextButton.Activated` returns a GUI-owned Signal and `Connect` creates an
owner-domain registration subject to publication, per-button/domain bounds,
clone omission, object destruction and domain teardown. Foundation 1B has no
Rust/client intake, action token, queued delivery or callback dispatch for this
Signal. A connected callback therefore cannot be triggered externally yet.

## Validation

Focused model and real compiler/VM facade tests cover the retained tree, value
userdata, every class/property family, equality, parenting, clone/destroy,
bounds, Signal ownership, nested/caught rollback, foreign-owner commit/rollback,
cross-domain parenting rejection, stale teardown and prior regressions. Exact
qualification completed on 2026-09-19:

- Windows-local .NET Framework Release compilation completed with zero warnings
  or errors. The GUI-1A and GUI-1B model suites, architecture contract, API
  contract and deterministic release checks passed.
- An isolated Linux worker built the Release native runtime and compiler worker.
  All five native CTest targets passed, followed by the complete managed/native
  runtime suite. That suite included GUI-1A, both GUI-1B suites, Foundations A-G,
  addon lifecycle, scale/fairness, package and Phase 0-3 regressions.
- The isolated Linux ASan/UBSan/leak build passed all five native CTest targets
  with `detect_leaks=1` and halt-on-error enabled. No sanitizer or leak failure
  was reported.
- Hosted validation for the implementation commit passed Windows, Ubuntu and
  sanitizers in [run 35432914596](https://github.com/gmoddev/CarbonLuau/actions/runs/35432914596).
  The Windows job compiled and exercised the real native/userdata runtime suite;
  the Ubuntu job exercised the equivalent Linux runtime and packaging path.
- Documentation validation and deployment passed in
  [run 35432914637](https://github.com/gmoddev/CarbonLuau/actions/runs/35432914637).

No live Carbon GUI claim is made. Foundation 1B intentionally has no Carbon/Rust
rendering or client-event path to qualify; those gates belong to GUI-1C and later.

## Deferred work

GUI-1C and later still own `Presentation`, `Show`/`Hide`/`IsShown`, viewer and
exact Player state, render-plan production, `RustCuiBackend`, client IDs, dirty
synchronization/flush, action tokens, private Rust commands, `Activated` ingress
and delivery, reconciliation, layout/live rendering qualification and public
release/API identity assignment. No package, scripting API, native ABI,
provider protocol, package schema or pinned Luau revision changed in 1B.
