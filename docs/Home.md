# CarbonLuau documentation

CarbonLuau embeds a pinned Luau VM for bounded, server-side scripting on
Carbon-modded Rust servers. Published `v0.3.0` contains the first gameplay facade;
`v0.4.0` adds experimental addons, Player inventory mutation, Teleport, GUI and
the older pinned VS Code tooling pack. The experimental `v0.5.0` package adds
local persistence and structured Query under scripting API
`0.5.0-experimental`. See [release notes](releases/0.5.0.md) and
[combined Foundation 2 qualification](PersistenceFoundation2D.md).
Platform and client limits remain in [Compatibility](Compatibility.md).
Development scripting API `0.6.0-experimental` adds read-only
[Workspace/Entity](api/Services/Workspace.md) on an exact pinned host envelope;
the published package remains v0.5.0.

Start with [installation](Installation.md), then use the
[experimental API reference](api/README.md). Read the
[compatibility limits](api/Compatibility.md) before deploying.

## Quick start

Create `carbon/data/CarbonLuau/scripts/init.luau`:

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(Player)
    print(`Connected: {Player.Name}`)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Create the sibling `modules` directory, start the server and run
`carbonluau.status`. After editing the script, apply a candidate generation with
`carbonluau.reload`.

`Player:SendMessage` host dispatch passed controlled-host qualification. Visible
receipt by an authenticated client was not tested and is not claimed.

## What is included

- Transactional scripts and controlled `require` modules.
- Bounded `task.spawn`, `task.defer` and `task.delay`.
- Players, Player proxies, Signals/Connections and Commands.
- Items existence plus bounded physical main/belt/wear inventory observation.
- Carbon permission checks and administrator status/reload commands.
- VM memory, callback, queue, payload and logging bounds.
- Provider-owned addon packages, exact dependency lifetimes and public modules.
- `require("@addon")`, `require("@addon/path")` and dependency availability.
- ScreenGui, Frame, text/image controls and immutable layout, color and typed image values.
- Deterministic UIListLayout/UIPadding and retained ScrollingFrame configuration.
- Explicit per-Player Show/Hide and secure button Activated callbacks.
- Private local durable DataStores with asynchronous Get/Set/Remove and bounded,
  index-backed structured Query.

InventoryOnly GiveItem and verified TakeItem are implemented; raw inventory objects, arbitrary Rust hooks, filesystem/network access,
Roblox replication and `task.wait` are not included. See the [0.3.0 release notes](releases/0.3.0.md)
for the initial baseline, the [0.4.0 release notes](releases/0.4.0.md) for
addons/GUI, and the [0.5.0 release notes](releases/0.5.0.md) for persistence.
[Foundation E](FoundationE.md) and [GUI Foundation 1G](GuiFoundation1G.md)
retain their distinct qualification evidence.
The current source ownership map is recorded in
[Foundation F](FoundationF.md), and compiler containment is recorded in
[Foundation G](FoundationG.md).

## Architecture direction

Persistence Foundation 1 implements [DataStoreService](api/Services/DataStoreService.md)
and callback-based Get/Set/Remove in private root/addon stores. Start with the
[guide and examples](api/Persistence.md); [1C](PersistenceFoundation1C.md) records
the combined qualification envelope. [D21](Invariants.md#d21--persistence-foundation-1)
and the [canonical design](PersistenceFoundation1.md) remain authoritative.
[Persistence Foundation 2](PersistenceFoundation2.md) implements structured
`Query` requests using `Field`, `Equals` or inclusive `Min`/`Max` under
[D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query).
Preparation is automatic, with optional field-name hints. Authors manage no
schemas, versions or migrations. [2D](PersistenceFoundation2D.md) records
combined Query and derived-index qualification; the
[final correction audit](PersistenceFoundation2-Validation.md) preserves design history.
[World/Entity Foundation 1](WorldEntityFoundation1.md) has a qualified
private lifetime substrate and read-only keyed runtime on pinned Windows/Linux
hosts. Earlier [negative lifetime evidence](WorldEntityLifetimeInvestigation.md)
remains historical; full plugin reload requires restart before Entity lookup.

[Invariants](Invariants.md) owns canonical runtime policy. The approved GUI
Foundation 1 contract is decision
[D15](Invariants.md#d15--gui-foundation-1-retained-presentation-model), with
detailed rationale and future implementation guidance in
[GUI Foundation 1 architecture](GuiFoundation1.md). GUI is part of the
experimental `0.4.0-experimental` scripting API. The internal
substrate is recorded in [GUI Foundation 1A](GuiFoundation1A.md), and the
retained Luau runtime through object lifecycle/publication is recorded in
[GUI Foundation 1B](GuiFoundation1B.md). Static exact-Player presentations and
the Rust CUI projection are recorded in [GUI Foundation 1C](GuiFoundation1C.md).
Bounded retained-tree synchronization and reconciliation are recorded in
[GUI Foundation 1D](GuiFoundation1D.md). Secure `TextButton.Activated` ingress is
recorded in [GUI Foundation 1E](GuiFoundation1E.md). Replacement, recovery,
teardown, diagnostics and leak/stress closure are recorded in
[GUI Foundation 1F](GuiFoundation1F.md). Public documentation, examples, final
available qualification and the retained 0.4.0 experimental identity are
recorded in [GUI Foundation 1G](GuiFoundation1G.md). Authenticated real-client
visual, cursor, reconciliation and click evidence remains unqualified and
non-gating. The additive GUI Foundation 2 architecture is resolved by
[D16](Invariants.md#d16--gui-foundation-2-deterministic-layout-and-rich-controls),
with detailed design and future GUI-2A through GUI-2F guidance in
[GUI Foundation 2](GuiFoundation2.md). Deterministic layout is implemented and
qualified in [GUI Foundation 2A](GuiFoundation2A.md), and typed images in
[GUI Foundation 2B](GuiFoundation2B.md), and retained scrolling in
[GUI Foundation 2C](GuiFoundation2C.md). Their lifecycle and rich-control
closure is qualified in [GUI Foundation 2E](GuiFoundation2E.md). Public
qualification, examples, compatibility and release preparation are closed by
[GUI Foundation 2F](GuiFoundation2F.md) under the existing
`0.4.0-experimental` identity. TextBox is not implemented and typed text input
remains deferred after the current Rust transport failed the exact-preservation
gate.

The additive GUI Foundation 3 architecture is resolved by
[D17](Invariants.md#d17---gui-foundation-3-deterministic-grids-clipping-fonts-and-presentation-scroll-intent),
with its detailed grid, clipping, font, scroll-effect, bounds and qualification
guidance in [GUI Foundation 3](GuiFoundation3.md). Current source implements the
deterministic grid in [GUI Foundation 3A](GuiFoundation3A.md), bounded clipping
in [GUI Foundation 3B](GuiFoundation3B.md), retained project-owned fonts in
[GUI Foundation 3C](GuiFoundation3C.md), and exact-Player one-way scrolling in
[GUI Foundation 3D](GuiFoundation3D.md). [GUI Foundation 3E](GuiFoundation3E.md)
closes their server-side lifecycle, scale and release-candidate qualification
under `0.4.0-experimental`; authenticated-client gates remain explicit, and Foundation 3
does not reopen deferred TextBox work.

Player Interaction Foundation 1 is resolved by
[D18](Invariants.md#d18--player-interaction-foundation-1), with complete
rationale and qualification routing in
[PlayerInteractionFoundation1](PlayerInteractionFoundation1.md). It approves
immutable `Vector3`, read-only exact-Player position/health/inventory
observation, read-only item existence, committed-only Teleport and
implementation-gated GiveItem/TakeItem. Player-1A implements immutable
`Vector3` and read-only exact-connection `Player.Position`; Player-1B implements
live read-only `Player.Health` and `Player.MaxHealth`; Player-1C implements
`Items:Exists`, `Player:CountItem` and `Player:HasItem`. See
[Player Interaction Foundation 1A](PlayerInteractionFoundation1A.md) and
[Player Interaction Foundation 1B](PlayerInteractionFoundation1B.md) and
[Player Interaction Foundation 1C](PlayerInteractionFoundation1C.md). Player-1D
implements committed-only `Player:Teleport(Vector3)` with exact-build server
qualification; authenticated-client convergence remains unqualified. See
[Player Interaction Foundation 1D](PlayerInteractionFoundation1D.md). Player-1F-A
implements committed-only verified `Player:TakeItem` using the exact-build
Inventory-M2 adapter; see [Player Interaction Foundation 1F-A](PlayerInteractionFoundation1FA.md).
Player-1F-B adds [InventoryOnly GiveItem](PlayerInteractionFoundation1FB-Validation.md);
combined mutation closure remains unimplemented. Revised D13 defines bounded PREPARE/COMMIT/VERIFY,
exact-Player Luau serialization and false/true/indeterminate-error outcomes;
the full rationale is in
[Inventory Ownership / Failure Reassessment](InventoryOwnershipFailureReassessment.md).
Health mutation and richer/raw inventory APIs remain deferred.
