# CarbonLuau documentation

CarbonLuau embeds a pinned Luau VM for bounded, server-side scripting on
Carbon-modded Rust servers. Published `v0.3.0` contains the first gameplay facade.
The qualified `v0.4.0` candidate adds experimental addon composition and GUI;
Windows x64, glibc Linux x64, sanitizers and live Carbon passed the recorded matrix.

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
- Carbon permission checks and administrator status/reload commands.
- VM memory, callback, queue, payload and logging bounds.
- Provider-owned addon packages, exact dependency lifetimes and public modules.
- `require("@addon")`, `require("@addon/path")` and dependency availability.
- ScreenGui, Frame, text/image controls and immutable layout, color and typed image values.
- Deterministic UIListLayout/UIPadding and retained ScrollingFrame configuration.
- Explicit per-Player Show/Hide and secure button Activated callbacks.

Items/inventory, arbitrary Rust hooks, filesystem/network access, Roblox
replication and `task.wait` are not included. See the [0.3.0 release notes](releases/0.3.0.md)
for the published baseline and the [0.4.0 release notes](releases/0.4.0.md)
plus [Foundation E qualification](FoundationE.md) and
[GUI Foundation 1G](GuiFoundation1G.md) for the candidate envelope.
The current source ownership map is recorded in
[Foundation F](FoundationF.md), and compiler containment is recorded in
[Foundation G](FoundationG.md).

## Architecture direction

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
[GUI Foundation 3D](GuiFoundation3D.md). These slices have no assigned release
identity; their authenticated-client gates remain explicit, and Foundation 3
does not reopen deferred TextBox work.
