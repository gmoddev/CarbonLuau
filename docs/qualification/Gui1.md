# Gui1: qualification ledger

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../Invariants.md).

## Records

- [GuiFoundation1A.md](#guifoundation1a)
- [GuiFoundation1B.md](#guifoundation1b)
- [GuiFoundation1C.md](#guifoundation1c)
- [GuiFoundation1D.md](#guifoundation1d)
- [GuiFoundation1E.md](#guifoundation1e)
- [GuiFoundation1F.md](#guifoundation1f)
- [GuiFoundation1G.md](#guifoundation1g)

<!-- BEGIN RECORD GuiFoundation1A.md -->

<a id="guifoundation1a"></a>

<a id="guifoundation1a-gui-foundation-1a-internal-substrate"></a>

# GUI Foundation 1A: internal substrate

Verdict: **PASS** after final-source validation.

Starting commit: `971c045d735e8c9f4020c1834b0f36218a6ed58f`.

Foundation 1A implements only the internal, Carbon-type-free substrate routed by
[D15](../Invariants.md#d15--gui-foundation-1-retained-presentation-model). It does
not install `game:GetService("Gui")`, expose Luau userdata or value constructors,
create presentations, render CUI, synchronize dirty state, admit GUI actions or
implement publication-journal behavior.

<a id="guifoundation1a-subsystem-ownership"></a>

## Subsystem ownership

The managed `Gui/` component owns:

| Component | Responsibility |
|---|---|
| `GuiConfig.cs` | Internal GUI safety/default candidates and validated immutable limit snapshot |
| `GuiDescriptors.cs` | Explicit class, property, method, event and value-type IDs/descriptors |
| `GuiModelContracts.cs` | Opaque object identity, owner/publication scope and retained-registry boundary |
| `GuiRenderPlan.cs` | Carbon-independent target, element, value, full-plan and patch contracts |
| `IGuiBackend.cs` | Full replacement, patch and destruction submission with controlled results |
| `InMemoryGuiBackend.cs` | Deterministic state, attempt ordering and send-failure injection for tests |
| `GuiHostCapabilities.cs` | Exact source-inspection identity and supported adapter assumptions |

No native change was required. The canonical retained model is not a Carbon LUI
container, Rust CUI object or serialized JSON document.

<a id="guifoundation1a-descriptor-model"></a>

## Descriptor model

The schema uses explicit numeric IDs and fixed descriptor arrays. It performs no
reflection-based member discovery. Internal nonconstructible `GuiNode` and
`GuiObject` descriptors support inheritance and `IsA`; the exact public class
set remains `ScreenGui`, `Frame`, `TextLabel` and `TextButton`.

Descriptors cover D15's `Name`, `ClassName`, `Parent`, layout, visibility,
background, Z, text, screen-lifecycle and common object methods plus
`TextButton.Activated`. Class-specific writability and defaults distinguish the
read-only `ScreenGui.Parent` from writable `GuiObject.Parent`. `UDim`, `UDim2`,
`Vector2` and `Color3` have explicit immutable-value field/range metadata. These
are internal schemas, not working Luau types.

<a id="guifoundation1a-backend-and-render-plan-contracts"></a>

## Backend and render-plan contracts

`IGuiBackend` has three operations:

```text
Replace(Target, FullPlan)
Update(Target, Patch)
Destroy(Target)
```

Results distinguish local acceptance, unavailable target and send failure. They
do not claim client acknowledgement. Requests contain only CarbonLuau render
primitives: stable bounded client IDs, parent IDs, node kinds and typed render
properties. Full plans enforce unique IDs, one root and parent-before-child
ordering. Properties canonicalize by explicit ID, strings/numbers have invariant
representations, and full/patch requests enforce configured element/property and
estimated-byte bounds.

The in-memory backend records every attempt with a monotonic sequence, maintains
accepted full/patch state, treats destruction as idempotent, rejects update of a
missing target, and injects deterministic per-operation send failures without
mutating accepted state. It is the test boundary for later render golden,
ordering, reconciliation and fault tests; it is not a production renderer.

<a id="guifoundation1a-limits-and-configuration"></a>

## Limits and configuration

`GuiConfig.Validate()` produces an immutable `GuiLimits` snapshot and rejects
nonpositive or contradictory scope relationships. It records the review's
object/tree/domain/global, viewer/presentation, Signal/token, string/clone and
request bounds as internal initial defaults.

The 64 KiB operation size, 64 sends and 256 KiB per flush, 1 ms flush budget,
32-patch reconciliation interval and interaction rates remain configurable
qualification/tuning candidates. No runtime currently consumes them, and this
phase does not promote them to public compatibility guarantees.

<a id="guifoundation1a-host-capability-inspection"></a>

## Host capability inspection

The compatibility target remains Carbon `2.0.259.0`, Rust protocol
`2633.288.1` and Steam build `25230300`.
Foundation 1A also recorded the exact public source revisions inspected on
2026-09-19:

- Carbon LUI `4d1b081eadef99da774e0342899bddcd638e26d2`: update containers,
  AddUI RPC sending, RectTransform parent/index updates, cursor and button command
  components. [Pinned source](https://github.com/CarbonCommunity/Carbon/blob/4d1b081eadef99da774e0342899bddcd638e26d2/src/Carbon.Components/Carbon.Common/src/Carbon/Components/LUI.cs)
- Facepunch Rust.Community `c1aba1600e8cf3dc7087bed99fdc7f8aa174af13`:
  ordered element processing, `destroyUi` before create, `update` target lookup,
  anchors/offsets, update-only parent/index changes, `NeedsCursor` and button
  `ClientRunOnServer`. [Pinned source](https://github.com/Facepunch/Rust.Community/blob/c1aba1600e8cf3dc7087bed99fdc7f8aa174af13/CommunityEntity.UI.cs)
- Oxide Rust adapter `ca69c156382acfbb62804de0710d9555d5a206de`:
  AddUI/DestroyUI helper success means a connected local RPC send was issued, not
  that a client applied or acknowledged the tree. [Pinned source](https://github.com/OxideMod/Oxide.Rust/blob/ca69c156382acfbb62804de0710d9555d5a206de/src/RustCui.cs)

These findings support the adapter design but do not qualify a production
`RustCuiBackend`, the exact target binaries or authenticated-client rendering.
Those remain later live qualification gates. Host source observations are not
new canonical invariants.

<a id="guifoundation1a-validation"></a>

## Validation

`GuiFoundation1ATests` covers descriptor completeness/uniqueness and class
relationships, exact public class/value/event boundaries, config validation,
deterministic plan representation, parent/order and request bounds, mock
replace/update/destroy, missing targets, failure injection and documentation
identity consistency. `Test-Architecture.ps1` owns the new implementation paths
and rejects managed reflection APIs in the GUI component.

Implementation and qualification commit `b81510c23b25feb1635d9e97a8595c867c056416`
passed the following final-source checks:

- Release managed build with zero warnings and zero errors;
- focused `--gui-only` descriptor, limit, plan, mock-backend and capability tests;
- architecture, API and deterministic release/package checks; and
- `git diff --check`.

Hosted [validation run 35430231936](https://github.com/gmoddev/CarbonLuau/actions/runs/35430231936)
passed the Windows and Ubuntu native/runtime matrices, packaging, and the
ASan/UBSan/leak job. The Windows matrix needed two failed-job retries because
the pre-existing Foundation G compiler-containment memory-limit test failed on
the first two attempts; the unchanged revision passed that test and the full
Windows lane on attempt 3. Hosted
[documentation run 35430231906](https://github.com/gmoddev/CarbonLuau/actions/runs/35430231906)
also passed.

<a id="guifoundation1a-deferred-work"></a>

## Deferred work

[GUI Foundation 1B](#guifoundation1b) now owns the retained registry, public
userdata/value constructors, `Create`/parenting/clone/destroy behavior, GUI
Signal presence and the D15 publication journal. GUI-1C and later still own
presentations, Show/Hide, real Rust CUI rendering, dirty synchronization, action
tokens and `Activated` ingress/delivery. No package, scripting API, native ABI,
provider protocol, package schema or Luau revision changed.

<!-- END RECORD GuiFoundation1A.md -->

<!-- BEGIN RECORD GuiFoundation1B.md -->

<a id="guifoundation1b"></a>

<a id="guifoundation1b-gui-foundation-1b-retained-runtime"></a>

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

<a id="guifoundation1b-retained-owner"></a>

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

<a id="guifoundation1b-luau-surface-and-value-types"></a>

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

<a id="guifoundation1b-parenting-and-lifecycle"></a>

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

<a id="guifoundation1b-publication-journal"></a>

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

<a id="guifoundation1b-bounds"></a>

## Bounds

Foundation 1B enforces the validated GUI-1A limits for live objects per screen,
domain and VM; screens per domain; depth; children; buttons; name/text bytes;
aggregate screen text; clone object/depth work; and GUI Signal connections per
button/domain. Transport, presentation, viewer, token, dirty and flush tuning
limits remain dormant because their owning phases are not implemented.

<a id="guifoundation1b-signal-status"></a>

## Signal status

`TextButton.Activated` returns a GUI-owned Signal and `Connect` creates an
owner-domain registration subject to publication, per-button/domain bounds,
clone omission, object destruction and domain teardown. Foundation 1B has no
Rust/client intake, action token, queued delivery or callback dispatch for this
Signal. A connected callback therefore cannot be triggered externally yet.

<a id="guifoundation1b-validation"></a>

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

<a id="guifoundation1b-deferred-work"></a>

## Deferred work

GUI-1C and later still own `Presentation`, `Show`/`Hide`/`IsShown`, viewer and
exact Player state, render-plan production, `RustCuiBackend`, client IDs, dirty
synchronization/flush, action tokens, private Rust commands, `Activated` ingress
and delivery, reconciliation, layout/live rendering qualification and public
release/API identity assignment. No package, scripting API, native ABI,
provider protocol, package schema or pinned Luau revision changed in 1B.

<!-- END RECORD GuiFoundation1B.md -->

<!-- BEGIN RECORD GuiFoundation1C.md -->

<a id="guifoundation1c"></a>

<a id="guifoundation1c-gui-foundation-1c-presentations-and-rust-cui-projection"></a>

# GUI Foundation 1C: presentations and Rust CUI projection

Verdict: **PASS for the scoped Foundation 1C implementation**.

Starting commit: `ef97d59c4bab196a64dcdcf3ef624b769130c3e7`.
Implementation and tested-source commit: `0ecd9337851fc368a67993367d29fb9f17c23c2c`.

Foundation 1C connects the retained GUI authority from Foundation 1B to a
bounded Rust CUI projection. It implements the internal Presentation lifecycle,
the Luau `ScreenGui` visibility methods, deterministic full-plan compilation and
the production Rust CUI adapter. It does not implement automatic property-patch
synchronization or client interaction ingress.

<a id="guifoundation1c-public-development-surface"></a>

## Public development surface

The current source supports the following unversioned GUI-development surface:

```lua
local Players = game:GetService("Players")
local Gui = game:GetService("Gui")

local Player = Players:GetPlayers()[1]
local Screen = Gui:Create("ScreenGui")
local Button = Screen:Create("TextButton")
Button.Text = "Open"

Screen:Show(Player)
assert(Screen:IsShown(Player))
Screen:Hide(Player)
```

Only `ScreenGui` has `Show`, `Hide` and `IsShown`. Each method validates the
opaque Player proxy's exact D11 connection lifetime. `IsShown` reports desired
server state, not client receipt. Duplicate Show and Hide are idempotent. A
show-hide pair before the owner-thread flush emits no obsolete AddUI operation;
a hide-show pair starts a new presentation epoch and uses a fresh opaque client
root.

This source surface remains outside the assigned `0.4.0-experimental` scripting
API. Foundation 1C changes no package version, scripting API version, native ABI,
provider protocol, package schema or pinned Luau revision.

<a id="guifoundation1c-presentation-lifecycle"></a>

## Presentation lifecycle

`GuiRetainedRegistry` owns one internal `GuiPresentation` for each
`(ScreenGui, exact Player connection token)` pair. A Presentation records the
screen identity, epoch, exact token and user ID, opaque client-ID prefix, whether
a full tree was locally issued, and whether synchronization is uncertain. One
retained ScreenGui may have several presentations; authors use `Clone` when they
need separate retained state.

The presentation set and pending destruction queue participate in the existing
GUI publication checkpoints. Provisional Show and Hide are immediately readable
inside the operation, but no backend call occurs until Luau returns and the
outer candidate commits. Rollback restores desired state and shared presentation
accounting. Disconnect, stale exact resolution, ScreenGui destruction, domain
retirement and host teardown remove desired state. Previously sent roots receive
best-effort destruction where the exact connection still exists.

<a id="guifoundation1c-deterministic-render-compilation"></a>

## Deterministic render compilation

`GuiRenderCompiler` compiles only CarbonLuau retained nodes into the GUI-1A full
render-plan contract. It emits the private overlay-filling root first, then
parents before children. Siblings use ascending `ZIndex`, attachment ordinal and
object identity. Logical TextLabel and TextButton objects produce private text
children; these children and every client ID remain unavailable to Luau.

The D15 top-left to lower-left layout transform is implemented directly:

```text
AnchorMin.x = pxS - ax * sxS
AnchorMax.x = pxS + (1 - ax) * sxS
OffsetMin.x = pxO - ax * sxO
OffsetMax.x = pxO + (1 - ax) * sxO

AnchorMin.y = 1 - pyS - (1 - ay) * syS
AnchorMax.y = 1 - pyS + ay * syS
OffsetMin.y = -pyO - (1 - ay) * syO
OffsetMax.y = -pyO + ay * syO
Pivot = (ax, 1 - ay)
```

Colors preserve normalized RGB and translate author-facing transparency to CUI
alpha with `1 - transparency`. Text uses the Foundation 1 fixed backend font,
size, color and combined alignment. A root requests `NeedsCursor` exactly when
its effectively visible retained tree contains a TextButton.

<a id="guifoundation1c-opaque-client-identity"></a>

## Opaque client identity

Each new presentation receives a cryptographically random, bounded prefix.
Element IDs combine that prefix with the immutable internal object identity and
an internal element-kind suffix. Script-visible Name, Player input and addon
strings do not contribute. IDs remain stable throughout one presentation epoch,
including a retry after local send failure. A later hide-show epoch receives a
fresh prefix.

<a id="guifoundation1c-rust-cui-backend"></a>

## Rust CUI backend

`RustCuiBackend` serializes validated full plans to the qualified Rust CUI JSON
schema with invariant numbers, deterministic element/property order and a final
UTF-8 payload bound. Full replacement places `destroyUi` on the private root and
emits parent-before-child AddUI elements. Destroy uses the private root only.
The Carbon-only transport re-resolves the exact Player token and connection
immediately before `CuiHelper.AddUi` or `CuiHelper.DestroyUi`; no BasePlayer or
CUI object enters retained state.

Backend outcomes distinguish local acceptance, target unavailability and send
failure. Local acceptance means only that the server issued the CUI RPC. A send
failure leaves retained and desired state authoritative, marks bounded
synchronization uncertainty and keeps no historical plan versions. Duplicate
Show explicitly retries that presentation with the same IDs. Destroy failure is
contained and never restores desired visibility.

<a id="guifoundation1c-static-foundation-1c-boundary"></a>

## Static Foundation 1C boundary

Initial Show compiles the current final retained tree. Creation, destruction,
reparenting and ZIndex changes can require a full replacement for structural
correctness. Ordinary layout, visibility, color and text property assignment
continues to update retained state synchronously but does not automatically emit
a patch in 1C. GUI Foundation 1D owns dirty masks, coalesced automatic
synchronization, patch/full selection and periodic reconciliation. Later GUI
work owns action tokens, the private client command, rate limiting and Activated
ingress/delivery.

Foundation 1C does not claim authenticated-client visual layout, cursor behavior,
button receipt or reconciliation. A controlled server-side AddUI fixture can
qualify serialization and local dispatch only.

<a id="guifoundation1c-validation"></a>

## Validation

`GuiFoundation1CTests` covers Show/Hide/IsShown, duplicate and preflush
transitions, fresh epochs, multi-viewer and cloned trees, exact disconnect and
reconnect isolation, publication commit/rollback, screen/domain cleanup,
deterministic/private IDs, full replacement and injected backend failures. Golden
render checks cover parent order, sibling Z order, nested scale/offset/anchor
translation, negative offsets, visual/text conversion and cursor presence and
absence. Rust backend checks cover deterministic JSON, full replacement,
controlled results and payload rejection. The native suite exercises the actual
Luau methods, publication timing and candidate rollback.

Final qualification on 2026-09-19 produced the following results:

- Local Windows .NET Framework Release compilation completed with zero warnings
  and errors. The focused GUI-1C model suite, architecture contract, API
  contract, deterministic release/package checks and `git diff --check` passed.
- Hosted Windows built all native targets and the isolated compiler worker. All
  five native tests passed, followed by the complete real compiler/VM managed
  suite. GUI-1A, GUI-1B model/native, GUI-1C model/native, Foundations A-G,
  package/parser, addon scale/fairness and Phase 0-3 regressions passed.
- Hosted Ubuntu 24.04 passed the equivalent five native tests, complete
  managed/native suite, packaging and release checks.
- The ASan/UBSan/leak lane passed ScriptCore, RuntimeAllocationFaults,
  CompilerContainment, RuntimeCore and NativeLoadUnload with all sanitizer halt
  and leak checks enabled.
- The immutable implementation source passed the complete matrix in
  [validation run 35434634667](https://github.com/gmoddev/CarbonLuau/actions/runs/35434634667).
  Documentation validation passed in
  [run 35434634495](https://github.com/gmoddev/CarbonLuau/actions/runs/35434634495).

Live Carbon was unavailable for this qualification: the configured DockerPC
worker tunnel refused the connection, and read-only BigVPS inspection found no
Rust server container. No host configuration or unrelated workload was changed.
The production adapter therefore retains the GUI-1A pinned-source basis, but no
new live AddUI/DestroyUI result is claimed. There was also no authenticated
client, so actual visual layout, cursor presentation, click receipt and client
reconciliation remain unqualified exactly as required by D15.

<!-- END RECORD GuiFoundation1C.md -->

<!-- BEGIN RECORD GuiFoundation1D.md -->

<a id="guifoundation1d"></a>

<a id="guifoundation1d-gui-foundation-1d-retained-synchronization-and-reconciliation"></a>

# GUI Foundation 1D: retained synchronization and reconciliation

Verdict: **PASS for the scoped Foundation 1D implementation**.

Starting commit: `0ecd9337851fc368a67993367d29fb9f17c23c2c`.
Implementation and qualification commit: `31d0301c62d8a9f1cade08136bd2d269919dcf8a`.
Recovery correction and final tested-source commit:
`31d0301c62d8a9f1cade08136bd2d269919dcf8a`.

GUI Foundation 1D connects committed retained mutations to already-shown
Presentations. It adds revisioned dirty state, deterministic Rust CUI property
updates, structural full replacement, bounded owner-thread flushing, persistent
fairness and newest-state failure convergence. It does not add client event
ingress or change the unversioned GUI development surface.

<a id="guifoundation1d-revision-and-dirty-model"></a>

## Revision and dirty model

Each retained ScreenGui owns a monotonically advancing revision. Each
Presentation records the newest revision it has locally issued. Render-affecting
committed mutations advance the ScreenGui revision; Name remains metadata-only
and creates no synchronization work.

Patchable dirty state is a bounded map from immutable GUI object identity to a
set of property descriptors. Repeated writes coalesce in that map and the patch
compiler always reads the newest retained value. There is no historical mutation
queue. Dirty detail remains available until every current Presentation of that
ScreenGui has reached the newest revision.

If the per-domain dirty-object capacity is exhausted, the detailed map is
discarded and the affected ScreenGui is marked for full replacement. The valid
retained mutation is never rejected because synchronization detail is full.
Hidden ScreenGuis retain their newest state without client work; a later Show
starts with a full projection of that state.

<a id="guifoundation1d-patch-and-structural-classification"></a>

## Patch and structural classification

The following properties are patched:

- Position, Size and AnchorPoint;
- Visible;
- BackgroundColor3 and BackgroundTransparency;
- Text, TextColor3, TextTransparency and TextSize;
- TextXAlignment and TextYAlignment.

Combined backend properties are regenerated from the current retained pair. For
example, a BackgroundColor3 change emits the current color and current
BackgroundTransparency-derived alpha. Position, Size or AnchorPoint changes emit
the complete five-field RectTransform derived from the current layout tuple.

Object creation or attachment, Destroy, Parent, cross-ScreenGui reparent and
ZIndex are structural. The affected ScreenGui Presentations receive one full
replacement compiled from final retained state. Cross-root reparent marks both
roots. Destroyed objects cannot leave obsolete property patches behind.

A Visible change that changes whether the tree needs a cursor is promoted to a
full replacement because removing the Foundation 1 root cursor marker is not a
safe partial CUI operation. Visible remains patchable when cursor state is
unchanged.

<a id="guifoundation1d-bounded-gui-flush"></a>

## Bounded GUI flush

`ScriptHost.Drain` now performs GUI work only after the bounded Luau callback
loop has returned and the VM is no longer entered:

```text
callback/task drain
    -> VM returned and host not busy
    -> one shared bounded GUI flush
```

The GUI phase has the current internal 1 ms target plus the existing global
64-send and 256 KiB serialized-byte limits. These are qualification values, not
public API guarantees. An operation already in flight may finish after the time
target, but no next operation begins after the deadline. Exhausted work remains
dirty for a later host turn.

One `FacadeWorld` budget is shared across root and addon domains. Persistent
domain and Presentation cursors provide deterministic round-robin progress. A
failed Presentation receives at most one attempt in a world flush cycle, so it
cannot consume the full shared send budget. Standalone test drains do not share
the world-cycle attempt namespace.

<a id="guifoundation1d-rust-cui-updates"></a>

## Rust CUI updates

`IGuiBackend` measures the actual operation before transport. `RustCuiBackend`
serializes property patches with `update=true`, stable opaque element names and
only the components and fields required by the dirty descriptors. It validates
the final UTF-8 size before calling transport. Text-only updates do not emit
RectTransform or DestroyUI fields.

If a patch cannot fit or cannot be represented safely, the same Presentation is
promoted to a full replacement. If the complete current full projection also
cannot fit, that revision enters a controlled blocked state with no send and no
hot retry. A newer retained revision clears the block and is evaluated from its
newest state.

<a id="guifoundation1d-failure-and-reconciliation"></a>

## Failure and reconciliation

Any failed or unavailable Update makes the Presentation uncertain and requires
a later full replacement. A failed Replace remains pending as a full replacement.
No failed path queues old plans or render revisions. Exact Player connection
resolution is repeated immediately before work; stale presentations are removed.
Destroy and Hide remain authoritative even when best-effort client cleanup fails.

After 32 successful patch batches for a Presentation, its next dirty
synchronization is a full replacement. The threshold is internal and
configurable. It reduces long-lived silent drift without claiming client
acknowledgement.

Show followed by Hide before a flush sends nothing. Hide followed by Show
destroys the retired epoch before sending a new full projection with fresh IDs.
A mutation before first Show is folded into the initial full tree. Publication
rollback restores retained, dirty and Presentation state and therefore produces
no backend operation.

<a id="guifoundation1d-modeled-performance-evidence"></a>

## Modeled performance evidence

The focused model uses one 50-object Foundation 1 ScreenGui and the production
Rust serializer. Representative local Windows results were:

| Viewers | Retained presentation memory | Initial full | Text patch | Position patch | Structural rebuild |
|---:|---:|---:|---:|---:|---:|
| 1 | 31,752 B | 4.060 ms / 15,592 B | 0.107 ms / 138 B | 0.154 ms / 211 B | 4.492 ms / 15,924 B |
| 10 | 34,208 B | 32.831 ms / 155,920 B | 0.546 ms / 1,390 B | 1.036 ms / 2,110 B | 39.831 ms / 159,250 B |
| 50 | 45,120 B | 204.750 ms / 779,600 B | 2.151 ms / 6,950 B | 4.853 ms / 10,550 B | 276.963 ms / 796,250 B |
| 100 | 52,144 B | 292.338 ms / 1,559,200 B | 2.629 ms / 14,000 B | 5.030 ms / 21,100 B | 233.903 ms / 1,592,600 B |
| 256 | 87,328 B | 732.416 ms / 3,991,552 B | 11.293 ms / 35,840 B | 15.884 ms / 54,016 B | 582.932 ms / 4,077,056 B |

The elapsed columns measure complete modeled backlog serialization, not one
bounded host turn. Text and Position workloads converged in one operation per
viewer, with one send per direct model flush. Structural byte totals show why
full replacements must remain globally budgeted across turns. A separate shared
flush fixture stops after one 3 ms in-flight backend send and carries the other
two presentations forward without duplication. The current limits required no
tuning. A 101-presentation same-domain fairness fixture serviced the unrelated
ScreenGui in four bounded turns on the recorded run and always within one full
round-robin pass.

<a id="guifoundation1d-validation-status"></a>

## Validation status

`GuiFoundation1DTests` covers coalescing, every patch class, multiple objects,
metadata suppression, structural cases, Show/Hide ordering, hidden mutation,
dirty overflow, reconciliation checkpoints, exact Rust update JSON, payload and
flush budgets, injected Update/Replace failures, newest-state recovery,
publication rollback/commit, disconnect/teardown, multi-domain and same-domain
fairness, viewer scale and post-Luau native flushing.

Final qualification on 2026-09-19 produced the following results:

- Local Windows .NET Framework Release compilation completed with zero warnings
  and errors. GUI-1A, GUI-1B, GUI-1C and GUI-1D focused model suites passed, as
  did architecture, API, release, deterministic package and diff checks.
- Hosted Windows built every native target and the compiler worker. All five
  native tests, import checks, complete real compiler/VM managed suite, loader
  tests, packaging and release artifact generation passed.
- Hosted Ubuntu 24.04 passed the equivalent native build/tests, complete
  managed/native suite, loader/export checks, packaging and release artifacts.
- The ASan/UBSan/leak lane passed ScriptCore, RuntimeAllocationFaults,
  CompilerContainment, RuntimeCore and NativeLoadUnload with sanitizer halt and
  leak detection enabled.
- The immutable corrected source passed all three lanes in
  [validation run 35478372835](https://github.com/gmoddev/CarbonLuau/actions/runs/35478372835).
  GitHub Pages documentation deployed successfully in
  [run 35478234017](https://github.com/gmoddev/CarbonLuau/actions/runs/35478234017).

The first hosted run exposed a null VM recovery-path dereference in the new
post-callback GUI guard. Commit `31d0301` added the missing live-VM check; the
complete corrected-source matrix above then passed. The failed run is not cited
as qualification evidence.

DockerPC live/local qualification was unavailable. The
DockerPC SSH path reached MiniVPS, but the expected loopback reverse-tunnel
listener on port 2222 was absent. No tunnel, host or unrelated workload was
changed. No live Carbon or authenticated-client result is claimed.

<a id="guifoundation1d-deferred-scope-and-identities"></a>

## Deferred scope and identities

Foundation 1D does not implement Activated ingress, action tokens, a private GUI
client command, interaction rate limiting, click callbacks, images, TextBox,
scrolling, layouts, advanced styling or any GUI Foundation 1E behavior.

No package version, scripting API version, native ABI, provider protocol,
package schema or pinned Luau revision changed. The GUI scripting identity
remains unassigned and outside `0.4.0-experimental`. Authenticated-client visual
layout, cursor behavior, reconciliation and click receipt remain unqualified.

<!-- END RECORD GuiFoundation1D.md -->

<!-- BEGIN RECORD GuiFoundation1E.md -->

<a id="guifoundation1e"></a>

<a id="guifoundation1e-gui-foundation-1e-secure-activated-ingress"></a>

# GUI Foundation 1E: secure Activated ingress

Verdict: **PASS for the scoped Foundation 1E implementation and available
qualification**. Authenticated real-client click receipt remains
**UNQUALIFIED**.

Starting commit: `31d0301c62d8a9f1cade08136bd2d269919dcf8a`.
Implementation and qualification commit: `918632a97255887eafc91e09e12287a2caa137c5`.

GUI Foundation 1E implements the one approved client interaction:
`TextButton.Activated`. It adds no other GUI event, class, input surface or
release identity.

<a id="guifoundation1e-private-command-path"></a>

## Private command path

Rust CUI receives one CarbonLuau-owned command in this fixed form:

```text
carbonluau.gui.action <32-lowercase-hex-token>
```

The Carbon handler accepts only a connected Player-origin command with exactly
one bounded argument. It verifies that Carbon's command connection is the same
object as the current Player connection, resolves the current Player lifetime,
and then asks the retained GUI world to validate and enqueue the action. It does
not enter Luau, render CUI or expose command construction to scripts.

The command text is assembled exclusively by CarbonLuau's render compiler.
Addon strings, public Commands registrations and GUI properties cannot supply a
client command. Tokens are not exposed through GUI userdata or properties.

<a id="guifoundation1e-token-authority-and-lifecycle"></a>

## Token authority and lifecycle

Each action token is 128 random bits encoded as 32 lowercase hexadecimal
characters. The string is only an opaque lookup key. Its server record binds:

- VM generation and owner domain lifetime;
- ScreenGui object identity and presentation epoch;
- exact Player identity, connection, connection token and user ID;
- TextButton object identity;
- the owning retained registry and the per-action limiter.

Each Player presentation receives distinct tokens. An accepted full
presentation replacement publishes a fresh token set only after the backend
accepts the replacement. Failed or oversized replacements publish no action
authority. Pure property patches retain the current token. Structural changes,
known uncertainty, full reconciliation, Hide, destruction, disconnect, domain
retirement and VM recovery retire prior tokens and fail closed.

Active registries are bounded per presentation, per domain and globally.
Retired-token diagnostic tombstones are also globally bounded. Token generation
rejects collisions with active, retired and same-candidate identities.

<a id="guifoundation1e-ingress-and-scheduler-admission"></a>

## Ingress and scheduler admission

Before queue admission, CarbonLuau validates canonical token encoding, active
registry membership, the exact current Player connection, live VM/domain,
desired Presentation and epoch, live ScreenGui and TextButton, current ancestry,
effective visibility and live Activated listeners. Per-action and per-Player
token buckets then bound repeated actions.

One activation creates one normal scheduler work item per current listener in
registration order. The complete fanout is packed and capacity-checked before
any item is queued, so insufficient queue capacity rejects the action
atomically. Existing Signal connection bounds remain authoritative. Tokens are
repeatable within an epoch and delivery is not exactly once.

Native operation 9 revalidates the immutable action payload immediately before
scheduled Luau entry. Hide, destruction, reparenting, listener removal,
disconnect, domain retirement or epoch replacement after admission suppresses
the stale callback without a script-visible error.

<a id="guifoundation1e-activated-semantics-and-mutation"></a>

## Activated semantics and mutation

Scripts continue to use only the existing Signal surface:

```lua
Button.Activated:Connect(function(Player)
    Button.Text = "Done"
end)
```

The callback receives the normal Player proxy for the exact connection that
submitted the valid action. Existing scheduler deadlines, Signal ordering,
Connection ownership and teardown rules apply. Callback mutations update the
retained model normally. They cannot recursively process CUI; GUI-1D performs
the later bounded flush. A callback that destroys its button completes, while
later queued listeners are suppressed by pre-entry revalidation.

<a id="guifoundation1e-publication-and-diagnostics"></a>

## Publication and diagnostics

GUI-1B publication snapshots include presentation epochs, token maps and pending
invalidation state. Provisional Show/Connect work cannot flush or activate a
token. Rollback restores the prior authority exactly. Commit permits normal
post-publication synchronization, and tokens become active only after an
accepted full presentation send.

`carbonluau.status` reports saturating counters for accepted, malformed,
unknown, stale, cross-Player, target-unavailable, rate-limited, queue-full and
pre-entry-stale actions, plus active and bounded retired-token counts. Hostile
requests do not produce per-request logs or script-visible failures.

<a id="guifoundation1e-validation-status"></a>

## Validation status

Focused deterministic tests cover exact Player delivery, ordered listener
fanout, repeat admission, malformed/oversized/forged/cross-Player input, token
rotation and retention, hidden/destroyed targets, disconnect/reconnect,
uncertainty, rate and queue limits, registry bounds, publication rollback and
commit, pre-entry suppression, callback mutation, self-destruction, fatal VM
recovery and domain replacement. A hostile-input fixture submits 10,000 each of
forged, cross-Player, rate-limited and stale requests while checking bounded
registry, queue and diagnostic state.

The Windows real-ABI managed suite passed GUI-1A through 1E model and native
coverage, Foundations A through G and addon regressions, 100 replacement cycles,
publication failures, fatal recovery and loader lifecycle. The production
package contained exactly 33 C# sources, no native binary or live fixture, and
two independent builds produced SHA-256
`7DFD88DF7A0278EAA158316F675D1565A9AE8A91CBFC395BDB5DF311CCC070B0`.

The Linux Release lane passed all five native tests, the complete real-compiler
managed suite, loader, architecture and API checks. ASan, UBSan and leak-enabled
qualification passed the same five native tests. A focused security diff review
reported no findings across all changed production surfaces.

The disposable live Carbon overlay compiled and loaded the production package,
reached runtime generation 2, answered RCON health checks, completed ten plugin
unload/load cycles with the native library unmapped during unload, and recovered
from missing, broken, missing-symbol and wrong-probe native fixtures. The
single-attempt RCON test helper was changed to a bounded retry after a startup
race; the repeated run passed. No production server tree or Pelican workload was
modified.

The existing Foundation G Windows live/local compiler-containment qualification
remains deferred as recorded in [Foundation G](Addons.md#foundationg). A native CTest on
this non-DockerPC host reproduced its unqualified worker-memory-limit case; it
does not change the passing GUI-1E Windows managed evidence or historical
Foundations A through F evidence. Hosted Windows CI remains the native Windows
gate for this change.

Live Carbon qualification proves command compilation/registration, plugin load
and lifecycle only. No authenticated Rust client performed a click, so actual
client command receipt and visible interaction remain unqualified.

<a id="guifoundation1e-deferred-scope-and-identities"></a>

## Deferred scope and identities

MouseButton1Click, hover and pointer movement, TextBox/input submission,
ImageButton, arbitrary client commands, client acknowledgement, new GUI classes
and GUI Foundation 1F or later behavior remain deferred. Authenticated-client
visual layout, cursor, reconciliation and click evidence also remain deferred.

No CarbonLuau package version, scripting API version, native ABI, provider
protocol, package schema or pinned Luau revision changed. The GUI scripting
identity remains unassigned and outside `0.4.0-experimental`.

<!-- END RECORD GuiFoundation1E.md -->

<!-- BEGIN RECORD GuiFoundation1F.md -->

<a id="guifoundation1f"></a>

<a id="guifoundation1f-gui-foundation-1f-lifecycle-and-runtime-closure"></a>

# GUI Foundation 1F: lifecycle and runtime closure

Verdict: **PASS for the scoped Foundation 1F implementation and available
qualification**. Authenticated real-client rendering and click evidence remains
**UNQUALIFIED** and is not claimed by this result.

Starting commit: `918632a97255887eafc91e09e12287a2caa137c5`.
Implementation and qualification commit: `806c9c52a9a4454e9a14e2c57d5da20a8c8308cb`.

GUI Foundation 1F closes replacement, fatal recovery, teardown, retry,
diagnostic and leak behavior for the D15 surface implemented by Foundations 1A
through 1E. It adds no public GUI class, event, service or version identity.

<a id="guifoundation1f-replacement-and-ownership"></a>

## Replacement and ownership

Root and addon candidates retain their GUI state provisionally. A failed
candidate publishes no object, presentation, action token or client effect and
leaves the active lifetime unchanged. A successful candidate commits before the
old lifetime retires; retirement then invalidates old action authority, disposes
its registry and queues bounded best-effort client cleanup. This is an atomic
server-authority transition, not an acknowledged client-side transition.

Controlled tests cover successful and failed root candidates, 100 successful
root replacements, 100 successful addon replacements and 100 failed
GUI-producing candidates. Registry counts return to baseline after every
sequence.

Shared GUI references retain the creating facade's exact domain ownership. A
consumer can mutate, show or connect through an exported owner reference while
the owner remains live. Retiring the consumer leaves the owner's GUI intact.
Retiring or replacing the owner stales every escaped object and Signal reference
held by the consumer. The resulting host connection remains owner-owned, in
accordance with D15, even when its closure was defined by another domain.

<a id="guifoundation1f-fatal-recovery-and-teardown"></a>

## Fatal recovery and teardown

Fatal VM recovery retires the complete old GUI world before VM release. Active
tokens become stale, queued callbacks fail their pre-entry lifetime check,
registries and presentations are disposed, and old client roots receive
best-effort destruction. Reconstruction starts from committed source/package
snapshots and creates fresh GUI authority; it preserves no runtime tree, token,
presentation or closure.

The real native-ABI fixture completed 100 fatal recovery cycles. Each cycle
suppressed old queued work, rejected old references and tokens, reconstructed a
fresh runtime, and returned final registry state to baseline.

Domain/provider retirement and host unload use the same owner-bound disposal
path. Controlled GUI domain-retirement coverage and the retained Foundation B/C
provider lifecycle regressions jointly cover unload, replacement, fresh
registration and stale-identity rejection. A GUI-specific live provider fixture
was not available, so provider GUI behavior is not represented as live Carbon
evidence. Live Carbon did prove ten complete
CarbonLuau unload/reload cycles with GUI state present: each unload unmapped the
native library and ended the compiler worker, and each reload reported a fresh
GUI baseline while the server remained responsive.

<a id="guifoundation1f-pending-work-and-backend-recovery"></a>

## Pending work and backend recovery

Retirement and destruction make obsolete patches, full rebuilds, destroys,
Show/Hide intent, callbacks, full-resynchronization state, blocked
presentations and backend retry state non-executable. Scheduler nodes may remain
until normal bounded draining, but immutable lifetime and presentation gates
prevent entry into retired authority.

Fault-injection coverage includes repeated failed Replace followed by success,
failed Update promoted to a full rebuild, failed Destroy followed by disconnect,
retirement during full resynchronization, screen destruction during retry,
disconnect during retry and Hide/Show around a failed rebuild. Retained state is
authoritative, only the newest revision remains pending, and an accepted later
full replacement converges from that state. No historical render revision queue
is introduced.

<a id="guifoundation1f-bounded-operator-diagnostics"></a>

## Bounded operator diagnostics

`carbonluau.status`, which remains restricted to Carbon authentication level 2,
now includes one aggregate GUI record covering:

- registries, live objects, ScreenGuis, presentations and GUI Signal
  connections;
- dirty, full-resynchronization, blocked and pending-destroy presentations;
- active and bounded retired action-token counts;
- accepted, rejected and pre-entry-stale action totals;
- backend failures, accepted full rebuilds and accepted patches; and
- GUI resource-limit rejections.

All totals are fixed-cardinality saturating counters or current aggregate
counts. Token values, Player identifiers and per-object labels are never
reported. Diagnostics allocate no unbounded history and are cleared with their
owning GUI world.

<a id="guifoundation1f-stress-and-leak-closure"></a>

## Stress and leak closure

The focused suite completed:

- 100 root replacements, 100 addon replacements and 100 failed candidates;
- 100 fatal VM recoveries;
- 1,000 Show/Hide, 1,000 Clone/Destroy and 1,000 reconnect cycles;
- repeated full-rebuild token rotation and backend failure/recovery;
- dirty-detail overflow, action-token saturation and presentation saturation.

Final assertions cover objects, screens, Signals/connections, presentations,
dirty and retry state, action tokens, callback queues and registered GUI worlds.
All returned to the expected live-root or zero baseline. Resource-limit and
diagnostic counters saturated without wrapping or growing cardinality.

<a id="guifoundation1f-qualification-evidence"></a>

## Qualification evidence

On Windows, the Release build and the full real-native-ABI managed suite passed
GUI Foundations 1A through 1F, Foundations A through G, addon/package/parser and
Phase 0 through 3 regressions. The focused 1E regression and 1F suites also
passed independently.

On Linux, an isolated two-CPU, 8 GiB worker container passed all five native
CTest targets, the net48 managed build and the complete Mono real-compiler
suite. The same five native targets passed ASan with leak detection and
halt-on-error, and UBSan with halt-on-error.

Live qualification used Carbon `2.0.259.0 [2026.09.03.0]` at commit `21063e8`
with Rust protocol `2633.288.1` and the 2026-09-16 server build. Ten root GUI
reloads and ten full plugin unload/reload cycles passed. Every unload removed
the native mapping and compiler process; every reload produced exactly one GUI
registry, two objects, one ScreenGui, no presentation, one GUI connection and
no active action token. No authenticated Rust client participated, so this does
not qualify visual layout, cursor behavior, client reconciliation or click
receipt.

A focused security diff review of the production lifecycle, diagnostics and
live-runner changes completed with no findings. The live runner is a privileged
fixture for a disposable isolated worker and uses loopback-only RCON with an
ephemeral secret.

Foundation G's separate Windows live/local compiler-worker qualification remains
deferred exactly as recorded in [Foundation G](Addons.md#foundationg). Foundation 1F
does not reinterpret that historical qualification status.

<a id="guifoundation1f-remaining-gui-foundation-1g-work"></a>

## Remaining GUI Foundation 1G work

Foundation 1G is qualification and release-planning work, not authorization for
another public GUI feature. It must provide:

- authenticated current-client evidence for visual layout, cursor behavior,
  private command receipt and visible click callback behavior;
- authenticated-client reconciliation evidence across patches, full rebuilds,
  Hide/Show, disconnect/reconnect and lifecycle replacement;
- live multi-viewer cost and behavior evidence beyond the controlled/model
  measurements;
- current supported Carbon/Rust host-adapter upgrade checks; and
- an explicit package/scripting identity and release decision only after those
  gates close.

Until then the GUI package and scripting API identity remains **UNASSIGNED /
release-planning gated**. Package `0.4.0`, scripting API
`0.4.0-experimental`, native ABI `1.4`, provider protocol
`CarbonLuau.Addons` / `1.2`, package schema `1` and the pinned Luau revision are
unchanged.

<a id="guifoundation1f-foundation-1g-disposition"></a>

## Foundation 1G disposition

Foundation 1G subsequently resolved the release-planning gate by assigning the
complete D15 surface to the still-unreleased package `0.4.0` and scripting API
`0.4.0-experimental`. The authenticated-client items listed above remain
unqualified, but an explicit scope decision made them non-gating for the
experimental identity. This does not retroactively turn Foundation 1F's
server-side evidence into visual, cursor, click-receipt or client-reconciliation
evidence.

<!-- END RECORD GuiFoundation1F.md -->

<!-- BEGIN RECORD GuiFoundation1G.md -->

<a id="guifoundation1g"></a>

<a id="guifoundation1g-gui-foundation-1g-public-closure"></a>

# GUI Foundation 1G: public closure

Starting commit: `806c9c52a9a4454e9a14e2c57d5da20a8c8308cb`.

GUI Foundation 1G closes public documentation, runnable examples, API auditing,
release identity and available final qualification for the D15 surface. It adds
no runtime GUI behavior and does not begin GUI Foundation 2.

<a id="guifoundation1g-public-documentation"></a>

## Public documentation

The author-facing surface is split into:

- [GUI guide](../api/Gui.md): getting started, shared/per-player trees, ownership,
  lifecycle, synchronization, interaction security, current bounds and concise
  Roblox differences;
- [GUI reference](../api/Gui-Reference.md): every class, property, default, method,
  event, value constructor and validation range; and
- [scripting compatibility](../api/Compatibility.md): public resource bounds and
  the separation between scripting contracts and internal synchronization
  tuning.

The guide describes observable retained behavior and uses Roblox-familiar names
without claiming a Roblox DataModel, PlayerGui replication or LocalScripts. Rust
CUI details remain outside the author workflow except where the absence of
client acknowledgement must be explained.

<a id="guifoundation1g-runnable-examples"></a>

## Runnable examples

The release bundle includes four root examples:

- `gui/hello`: a centered hello panel;
- `gui/shared-live`: one live tree shown to several Players;
- `gui/per-player`: independent retained state through `Clone()`;
- `gui/activated`: exact-Player `TextButton.Activated` handling.

The `guiowner` addon creates and exports an owner-bound ScreenGui through its
public main module. `guiconsumer` imports and mutates that shared reference. The
pair demonstrates that same-VM sharing does not transfer resource ownership and
that owner retirement, not consumer reference possession, controls validity.

<a id="guifoundation1g-api-audit"></a>

## API audit

The public reference is checked against the build-embedded facade and managed
descriptors for:

- the exact `Gui` service and ScreenGui, Frame, TextLabel and TextButton classes;
- all common/layout/visual/text properties and their implemented defaults;
- Create, Clone, Destroy, GetChildren, FindFirstChild, IsA, Show, Hide and
  IsShown;
- UDim, UDim2, Vector2 and Color3 constructors/fields/ranges; and
- the `Activated(function(Player))` callback signature.

The real compiler/VM loads every root example. The real package parser,
dependency binder and shared VM activate both GUI addon examples. Documentation
states that Show/Hide/IsShown is desired server state, Clone carries no viewers
or connections, Destroy/stale operations fail closed, cross-domain references
retain their original owner, and no client acknowledgement is implied.

<a id="guifoundation1g-version-decision"></a>

## Version decision

The first public addon plus GUI prerelease remains:

| Identity | Value |
|---|---|
| Package | `0.4.0` |
| Scripting API | `CarbonLuau 0.4.0-experimental` |
| Native ABI | `1.4` |
| Provider protocol | `CarbonLuau.Addons` / `1.2` |
| Package schema | `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

No 0.5.0 increment is warranted: 0.4.0 has not been released, the GUI surface is
additive to its first public compatibility boundary, and no accepted 0.4
contract is being broken. Experimental status does not permit silent later
breaks.

<a id="guifoundation1g-authenticated-client-status"></a>

## Authenticated-client status

Authenticated-client visual layout, UI scale/aspect behavior, cursor behavior,
actual private-command click receipt and client-side reconciliation remain
**UNQUALIFIED**. An explicit scope decision makes that evidence non-gating for
the experimental API identity. Server-side render plans, Carbon command
registration and lifecycle evidence do not substitute for those observations,
and the release documentation does not claim them.

<a id="guifoundation1g-release-preparation-boundary"></a>

## Release preparation boundary

The changelog, 0.4.0 release notes, installation guide, compatibility matrix,
public API index, release bundle contents and reproducibility checks include GUI
Foundation 1. No tag or GitHub Release is created by this phase.

Foundation G's Windows live/local compiler-worker supplement remains separately
**DEFERRED / UNQUALIFIED**. Hosted Windows CI does not close it and GUI
Foundation 1G does not reinterpret historical Foundations A through F evidence.

Images, ImageButton, TextBox, scrolling, automatic layouts, advanced
styling/constraints, hover/focus, raw CUI, arbitrary client code, provider
capabilities, restricted exposure, new gameplay APIs and GUI Foundation 2 remain
out of scope.

<a id="guifoundation1g-final-qualification"></a>

## Final qualification

Verdict: **GUI FOUNDATION 1 QUALIFIED FOR EXPERIMENTAL PUBLIC RELEASE** within
the documented server-side and non-authenticated evidence envelope.

The final worktree passed:

- Windows Release build and the complete real-native-ABI managed runtime suite,
  including GUI-1A through 1F, Foundations A through G, addon/provider/package
  lifecycle, replacement, recovery, synchronization, action security,
  scale/fairness and the six bundled root examples;
- Windows package, public API, architecture and deterministic `win-x64` release
  bundle validation;
- Linux Release native tests, the complete Mono managed runtime/loader suites,
  package/API/architecture checks and deterministic `linux-x64` release bundle
  validation in an isolated two-CPU, 8 GiB worker;
- all five native test groups under ASan, UBSan and leak detection; and
- exact release-bundle presence checks for all root and addon GUI examples.

The live Carbon lifecycle evidence recorded by Foundation 1F remains applicable:
Foundation 1G changes documentation, examples, tests and release assembly only,
not production runtime behavior. Expensive prior stress was not repeated beyond
the complete final-head regression matrix. Hosted CI results are reported with
the final tested commit.

The authenticated-client observations listed above remain **UNQUALIFIED** and
non-gating. This verdict does not qualify them. No tag or GitHub Release was
created.

<!-- END RECORD GuiFoundation1G.md -->

