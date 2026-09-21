# GUI Foundation 1C: presentations and Rust CUI projection

Verdict: **PASS for the scoped Foundation 1C implementation**.

Starting commit: `ef97d59c4bab196a64dcdcf3ef624b769130c3e7`.
Implementation and tested-source commit: `0ecd9337851fc368a67993367d29fb9f17c23c2c`.

Foundation 1C connects the retained GUI authority from Foundation 1B to a
bounded Rust CUI projection. It implements the internal Presentation lifecycle,
the Luau `ScreenGui` visibility methods, deterministic full-plan compilation and
the production Rust CUI adapter. It does not implement automatic property-patch
synchronization or client interaction ingress.

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

## Opaque client identity

Each new presentation receives a cryptographically random, bounded prefix.
Element IDs combine that prefix with the immutable internal object identity and
an internal element-kind suffix. Script-visible Name, Player input and addon
strings do not contribute. IDs remain stable throughout one presentation epoch,
including a retry after local send failure. A later hide-show epoch receives a
fresh prefix.

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
