# GUI Foundation 1G: public closure

Starting commit: `806c9c52a9a4454e9a14e2c57d5da20a8c8308cb`.

GUI Foundation 1G closes public documentation, runnable examples, API auditing,
release identity and available final qualification for the D15 surface. It adds
no runtime GUI behavior and does not begin GUI Foundation 2.

## Public documentation

The author-facing surface is split into:

- [GUI guide](api/Gui.md): getting started, shared/per-player trees, ownership,
  lifecycle, synchronization, interaction security, current bounds and concise
  Roblox differences;
- [GUI reference](api/Gui-Reference.md): every class, property, default, method,
  event, value constructor and validation range; and
- [scripting compatibility](api/Compatibility.md): public resource bounds and
  the separation between scripting contracts and internal synchronization
  tuning.

The guide describes observable retained behavior and uses Roblox-familiar names
without claiming a Roblox DataModel, PlayerGui replication or LocalScripts. Rust
CUI details remain outside the author workflow except where the absence of
client acknowledgement must be explained.

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

## Authenticated-client status

Authenticated-client visual layout, UI scale/aspect behavior, cursor behavior,
actual private-command click receipt and client-side reconciliation remain
**UNQUALIFIED**. An explicit scope decision makes that evidence non-gating for
the experimental API identity. Server-side render plans, Carbon command
registration and lifecycle evidence do not substitute for those observations,
and the release documentation does not claim them.

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
