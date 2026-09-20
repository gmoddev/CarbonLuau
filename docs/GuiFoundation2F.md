# GUI Foundation 2F: public qualification and release-candidate closure

Verdict: **PASS for experimental public release of the implemented Foundation 2
surface**. Authenticated-client observations remain explicitly unqualified and
non-gating under the existing compatibility policy.

Starting commit: `9fab87f4ffd7da1ca1a1d45c417dff781627e425`.

Foundation 2F adds no production GUI behavior. It closes public documentation,
examples, API compatibility, scale evidence, release contents and identity for
the D16 subset implemented and lifecycle-qualified by GUI-2A, GUI-2B, GUI-2C
and GUI-2E.

## Public surface and identity

The experimentally public Foundation 2 surface is:

- `GuiObject.LayoutOrder`, `UIListLayout` and `UIPadding`;
- immutable `ImageSource` values with `None`, `Sprite`, `Png`, `Item` and
  `SteamAvatar` source kinds;
- `ImageLabel`, `ImageButton` and `ImageButton.Activated`; and
- `ScrollingFrame`, `CanvasSize`, `ScrollingDirection` and
  `ScrollingEnabled`.

This additive subset joins the still-unreleased package `0.4.0` and scripting
API `CarbonLuau 0.4.0-experimental`. There is no 0.5.0 bump because no published
0.4 compatibility surface is being superseded. Native ABI `1.4`, provider
protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` are unchanged.

## Documentation and examples

The [GUI guide](api/Gui.md) owns author-facing behavior and a scenario-to-example
index. The [GUI reference](api/Gui-Reference.md) owns exact classes, defaults,
accepted values and hard bounds. [Scripting compatibility](api/Compatibility.md),
the [release notes](releases/0.4.0.md), [installation](Installation.md), the
repository README and changelog identify the experimental surface and its
limits.

The release bundle includes focused runnable examples for:

1. vertical `UIListLayout`;
2. horizontal `UIListLayout`;
3. `UIPadding`;
4. `LayoutOrder` independent of `ZIndex`;
5. sprite and PNG `ImageLabel` values;
6. secure `ImageButton.Activated`;
7. item and skin image sources;
8. Steam avatar image sources;
9. `ScrollingFrame` with explicit `CanvasSize`;
10. layout and padding inside scrolling content;
11. one rich retained tree shared by multiple Players; and
12. cloned retained state for each Player.

No example contains raw CUI, action tokens or client commands.

## API and compatibility audit

The public reference was checked against `GuiDescriptors.cs`,
`GuiImageSource.cs`, the private bootstrap and the retained/render adapters.
Every documented implemented class, property, method, event, default, enum-like
string and ImageSource constructor matches the runtime descriptor or bootstrap.
The audit preserves these distinctions:

- `LayoutOrder` controls list geometry; `ZIndex` controls render order;
- `GetChildren()` remains retained attachment order;
- list layout never rewrites author-retained `Position`;
- hidden children consume no list space;
- client rendering and asset loading have no acknowledgement contract;
- `CanvasSize`, direction and enabled state are retained, while current scroll
  position, drag and inertia are client-local; and
- `ImageButton.Activated` uses the same exact-Player, Presentation-bound secure
  path as `TextButton.Activated`.

Foundation 1 behavior is unchanged when scripts do not create Foundation 2
objects or properties. Position without layout, coordinates without padding,
`TextButton.Activated`, ZIndex, attachment-order children, Clone/Destroy,
shared-view behavior and D15 publication/replacement/recovery all retain their
qualified behavior.

## TextBox deferral audit

**TextBox is not implemented.** `Submitted` is not implemented. Neither name is
present in production descriptors or bootstrap globals, and no public example
or release note advertises it as usable.

The preserved D16 future design requires exact supported single-line text. On
Rust Dedicated Server app `258550`, build `25353106`, the current InputField
command path trims the command and `FullString` before CarbonLuau can validate
the payload. Trailing whitespace and whitespace-only text are irreversibly
lost, while argument reconstruction would reinterpret whitespace, quotes and
backslashes. CarbonLuau therefore exposes no lossy console-argument substitute.
The design may be reconsidered if a future host provides a bounded opaque
text-preserving input transport.

## Resource and scale evidence

`GuiFoundation2FTests.cs` builds one representative retained screen with 116
objects: Frame, UIListLayout, UIPadding, text controls, image controls and 27
ScrollingFrames containing layout, padding and image state. Its authoritative
plan charges 254 projected elements against the existing 257-element bound.
No limit was raised.

| Viewers | Projected elements | Estimated bytes | Serialized bytes per plan | Aggregate serialized bytes | Managed memory upper-bound delta |
|---:|---:|---:|---:|---:|---:|
| 1 | 254 | 24,516 | 38,772 | 38,772 | 1,671,512 B |
| 10 | 254 | 24,516 | 38,772 | 387,720 | 1,821,200 B |
| 50 | 254 | 24,516 | 38,772 | 1,938,600 | 9,700,368 B |
| 100 | 254 | 24,516 | 38,772 | 3,877,200 | 19,413,864 B |

The managed figure is the maximum observed conservative process-GC delta across
the recorded Windows and Linux runs. It includes retained in-memory backend
evidence calls and is not a per-Presentation contract. Every scale
case received exactly one initial authoritative plan. A shared image patch plus
scrolling structural reconciliation converged all Presentations with zero dirty
or full-resync backlog. Retirement returned retained objects, registries,
Presentations, actions, player rates and pending work to zero. There was no
unbounded dirty, layout, retry or historical render-version growth.

## Authenticated-client and Foundation G status

No authenticated current Rust client was available. Actual sprite/PNG/item/skin
and avatar appearance, source replacement, tint/transparency, ImageButton click
receipt, vertical/horizontal/XY scrolling, clipping, scrollbars, independent
Player scroll position and rebuild reset behavior remain **UNQUALIFIED**. API
availability does not claim those outcomes.

No RustDedicated/Carbon process was available on BigVPS, and DockerPC was
unavailable, so live Carbon qualification was not rerun for GUI-2F.

Foundation G remains **PARTIAL** solely for its separate Windows live/local
compiler-worker qualification. That gate is **DEFERRED / UNQUALIFIED** for exact
Foundation G source `e3025401c3085f0552bbfe3d045c3143c4ded005`. Hosted Windows
CI is not relabeled as equivalent evidence, and historical Windows qualification
for Foundations A-F remains valid.

## Release preparation and remaining scope

The deterministic release bundle includes the Foundation 2 examples, GUI guide,
GUI reference, installation notes, release notes, license/attribution,
production package, matching native runtime/compiler worker and generated
checksums/provenance. Preparing these artifacts creates no tag or GitHub Release.

Still unsupported or deferred: `TextBox`, `Submitted`, focus APIs,
`UIGridLayout`, `AutomaticSize`, `AbsoluteContentSize`, `AutomaticCanvasSize`,
`CanvasPosition`, arbitrary URL images, `UIStroke`, `UICorner`, `TextScaled`,
rich text, drag/drop, client geometry and Foundation 3 features.

## Qualification matrix

The release-candidate matrix covers GUI Foundations 1A-G, GUI-2A/2B/2C/2E/2F,
D15/D16 architecture, addon/provider lifecycle, root/addon replacement, VM
recovery, publication/rollback, synchronization/reconciliation, ImageButton
security, layout/projection bounds, scrolling lifecycle, resource teardown,
packaging, public API/docs, deterministic release artifacts, Windows and Linux
runtime tests, plus ASan/UBSan/leak checks. The completion report records the
exact tested revision and hosted CI runs.

The clean BigVPS Linux worker passed all five release native CTests, loader and
export checks, the complete Mono real-native managed suite, every bundled GUI
example, deterministic Linux release artifacts and all five ASan/UBSan/leak
suites. The controlling Windows PC passed managed compilation, every model
suite and deterministic Windows packaging. Its non-worker full native run twice
reached the pre-existing GUI-2E 100-cycle fatal-recovery stress before diverging
late in that stress; no local retry was used as substitute worker evidence.
DockerPC remained unavailable. Hosted Windows is the applicable clean Windows
build/runtime gate for this phase and remains distinct from Foundation G's
deferred Windows live/local Carbon qualification.

GUI Foundation 3 and TextBox implementation were not started.
