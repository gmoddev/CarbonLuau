# GUI Foundation 2E: lifecycle and rich-control closure

Verdict: **PASS for the implemented Foundation 2 surface and the available
non-authenticated qualification envelope**. `TextBox` remains **DEFERRED / NOT
IMPLEMENTED** because its mandatory host transport gate failed.

Starting commit: `e97db2c1a222294470fc1917a4a118cd9cab00ac`.

GUI Foundation 2E adds no public class, property, event or version identity. It
qualifies the D16 surface implemented by GUI-2A through GUI-2C across the D15
ownership, publication, replacement, recovery and synchronization boundaries.
The focused evidence lives in
[`GuiFoundation2ETests.cs`](../tests/runtime/GuiFoundation2ETests.cs).

## Qualified surface

The lifecycle closure covers:

- `GuiObject.LayoutOrder`, `UIListLayout` and `UIPadding`;
- immutable `ImageSource`, `ImageLabel` and `ImageButton`;
- `ImageButton.Activated` through the existing D15 action ingress; and
- `ScrollingFrame`, `CanvasSize`, `ScrollingDirection` and
  `ScrollingEnabled`.

These controls continue to use the generic retained registry and publication
journal. Foundation 2E introduces no parallel ownership, scheduling,
synchronization or recovery mechanism.

## Shared views and ownership

One retained rich-control tree was shown to two exact Player connections.
LayoutOrder, padding, image source/color/transparency and scrolling
configuration remained one shared retained value and synchronized to both
Presentations. Each Presentation received a distinct action token. Structural
image or scrolling changes rotated both tokens without retargeting an old token.

The render plan contains no CanvasPosition or normalized scroll position.
Client scroll offset, inertia and gesture state therefore remain
Presentation-local and unobservable. Hide/Show, reconnect, replacement and full
reconciliation may reset them independently.

Cross-domain native tests used package-qualified shared GUI references. A
consumer changed owner-created layout, image and scrolling objects. Failed
consumer initialization rolled every retained change back with no client work;
successful initialization committed atomically while the owner remained valid.
Consumer retirement did not transfer or destroy owner resources. Owner
retirement staled escaped GUI references, while an immutable `ImageSource`
retained by an optional consumer remained an ordinary lifetime-independent
value.

## Replacement, provider and recovery behavior

Failed root and addon candidates left the committed lifetime, retained state and
ImageButton authority unchanged. Successful replacement committed the new
lifetime before retiring the old one, then invalidated all old handles,
Presentations and action tokens. A client-local scroll offset was never treated
as retained replacement state.

Provider unload with rich GUI present retired the owner tree, pending
synchronization and a queued ImageButton callback. A required consumer became
blocked and was reconstructed against the fresh dependency lifetime after
explicit provider re-registration. An optional consumer stayed in its original
lifetime and did not hot-rebind. Old registration and ImageButton tokens stayed
stale.

The real native-ABI fixture completed 100 fatal recovery cycles with a tree that
contained layout helpers, images, an ImageButton callback and scrolling. Every
cycle retired the old GUI registry, pending callback, Presentation and token
before reconstructing fresh state from the committed source snapshot. Host
dispose/recreate likewise returned to zero addon/GUI state and created fresh
VM, domain, Presentation and action identities.

## Publication and synchronization closure

The focused suite covers provisional LayoutOrder, layout padding, UIPadding,
ImageSource, ImageButton connection and scrolling mutations. Rollback restores
the exact committed tree, connection count and dirty state. Commit exposes one
atomic newest-state transition after owner-lifetime revalidation.

Fault injection covers failed image/scroll whole-Presentation replacement,
layout detail overflow, mutation while a Presentation requires full
resynchronization, disconnect and owner retirement with pending work. New
mutations collapse into the latest retained revision. Recovery sends a complete
authoritative plan rather than replaying historical layout or render versions.

ImageButton qualification covers exact-Player admission, forged and
cross-Player rejection, source-rebuild rotation, Hide/Show, Destroy, root/addon
replacement, fatal recovery, provider reload, queued work followed by target
retirement and callback mutation of layout/image/scrolling state. All use the
existing D15 `Activated(Player)` semantics and pre-entry stale-work gate.

## Layout, scrolling and resource closure

Clone copies retained layout, padding, image and scrolling properties into
fresh object identities but copies no Presentation, client scroll state,
Signal connection or token. Helper reparenting preserves the
one-helper-per-parent cardinality rule. Helper destruction restores authored
Position as projection authority. Recursive destruction and domain retirement
stale every rich-control handle and clear helper, dirty, action and private
scrolling projection state.

The combined stress fixture completed:

- 100 root replacements;
- 100 addon replacements;
- 100 failed candidates;
- 100 fatal VM recoveries;
- 1,000 rich-subtree Clone/Destroy cycles;
- 1,000 Show/Hide cycles;
- 1,000 same-account reconnect cycles;
- 100 ImageButton source rebuild/token rotations; and
- 100 failed scrolling replacements followed by full resynchronization.

Final assertions covered retained objects, helper descendants, Signal
connections, Presentations, action tokens, dirty/full-resync state, pending
callbacks and registered GUI worlds. Live counts returned to the expected root
or zero baseline. Existing fixed-cardinality diagnostics were sufficient:
aggregate object/Screen/Presentation/connection/action counts, dirty and
full-resync counts, backend failures, resource-limit rejection and bounded
action rejection counters covered the new state without exposing tokens or
image-source internals.

## TextBox compatibility record

`TextBox` and `Submitted(Player, Text)` are not implemented.

The GUI-2D gate inspected Rust Dedicated Server Steam app `258550`, build
`25353106`. The inspected files were:

- `Facepunch.Console.dll` SHA-256
  `55f1fe738d9741b43328c681e35ddf33492bd9f30cfe96118c7d4f7a73c77fca`;
- `Assembly-CSharp.dll` SHA-256
  `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.

The current Rust InputField callback constructs the server command as the
configured CarbonLuau-controlled command, one space and the user value. The
server's `ConsoleSystem.Arg.BuildCommand` then calls `Trim()` on the complete
command and again on `FullString`. Trailing whitespace and whitespace-only
input are therefore irreversibly lost before CarbonLuau can validate the
payload. Reconstructing `Arg.Args` is also prohibited because console argument
parsing reinterprets quotes, backslashes and whitespace.

This evidence applies to the inspected build, not every future Rust release.
TextBox may be reconsidered if the host exposes a bounded opaque UI-input
payload that reaches the plugin without trimming, tokenization or command
reinterpretation. D16's exact preservation contract must not be weakened to
ship the class.

## Qualification and remaining evidence

The final Linux worktree ran in a four-CPU, 8 GiB BigVPS container. All five
release native CTest suites, loader/export checks and the complete Mono
real-native managed suite passed. That suite includes GUI Foundations 1A
through 1G, GUI-2A through GUI-2C and GUI-2E, Foundations A through G, addon
package/parser/provider lifecycle and Phase 0 through Phase 3 regressions. A
separate ASan/UBSan/leak build passed all five native suites with leak detection
and halt-on-error enabled.

DockerPC was unavailable, so Windows live/local native qualification could not
run there. Hosted Windows CI remains the applicable Windows build/runtime gate.
No RustDedicated/Carbon process was available on BigVPS, so live Carbon
qualification was unavailable. These limitations do not replace or invalidate
historical Foundation 1 evidence.

Authenticated-client image rendering, scrolling behavior and actual
ImageButton click receipt remain unqualified. Foundation 2E claims retained
model, lifecycle, serialized projection and controlled ingress behavior only.

## Identity and remaining scope

Package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider
protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` were unchanged by GUI-2E. At that
phase Foundation 2 had no release/API identity. GUI-2F later assigned the
implemented layout/image/scrolling subset to the existing unreleased
`0.4.0-experimental` identity.

TextBox, Submitted,
focus APIs, alternate text transport, CanvasPosition, AutomaticCanvasSize,
UIGridLayout, URL images, advanced styling and Foundation 3 features were not
implemented.
