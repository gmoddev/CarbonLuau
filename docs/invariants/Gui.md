# Gui: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D15 — GUI Foundation 1 retained presentation model

GUI Foundation 1 is a CarbonLuau-owned, server-driven retained model. Its public boundary is the domain-bound `Gui` service; `ScreenGui`, `Frame`, `TextLabel` and `TextButton`; immutable `UDim`, `UDim2`, `Vector2` and `Color3` values; retained `Name`/`Parent`/`ClassName`, `Create`, `GetChildren`, `FindFirstChild`, `IsA`, `Clone`, `Destroy`, `Show`, `Hide`, `IsShown`, common layout/visual and text properties, `TextButton.Activated`, and automatic bounded synchronization. It does not introduce `Instance.new`, a generic Instance/DataModel, raw CUI access, arbitrary client commands, images, text input, scrolling, automatic layouts or advanced styling/constraints. [GuiFoundation1.md](../GuiFoundation1.md#public-foundation-1-boundary) owns the detailed implementation-facing surface.

Every GUI object and GUI-owned resource has one immutable `ResourceOwner`: the domain lifetime bound to the `Gui` service or object facade used to create it. Sharing an ordinary same-VM Luau reference does not transfer ownership. Operations revalidate that owner lifetime. Cross-domain parenting is rejected, while a live foreign domain may otherwise use a valid shared reference. Retirement of the owner recursively retires its GUI objects, GUI Signals, presentations, action tokens and queued GUI work. A destroyed or stale GUI reference retains identity equality only; host-backed operations fail with a controlled error.

The retained tree and desired presentation set are authoritative. A `Presentation` is internal state for exactly one `(ScreenGui, D11 exact Player connection lifetime)` pair and owns client element identities, synchronization state, an epoch and presentation-specific action identities. One `ScreenGui` tree may be shown to multiple Players; committed mutations affect all of its current presentations. Per-player retained state requires an explicit deep `Clone`. `Show`/`Hide`/`IsShown` describe desired server state, not acknowledged client state.

Committed retained mutations are synchronous and immediately readable. Client synchronization occurs later, on the owner thread outside Luau entry, and is bounded and coalesced toward the newest retained revision. Rust CUI is a best-effort projection without a client acknowledgement contract. Pure supported property changes may use deterministic patches; structural change, known delivery uncertainty and bounded reconciliation checkpoints require deterministic whole-presentation replacement from final retained state. Backend failure never rolls back the retained model or retroactively raises from a completed mutation: it records bounded diagnostics, invalidates unsafe interaction state and leaves the presentation needing a later full resynchronization. Overload collapses obsolete dirty detail into full resynchronization, preserves newest authoritative state and bounded fair work, and rejects excess client events before Luau entry.

Each client interaction is untrusted input admitted through one private CarbonLuau path. Every `TextButton` action uses a cryptographically opaque, presentation-specific token bound to VM generation, owner domain, `ScreenGui`, exact D11 Player connection, presentation epoch and button. Luau neither supplies nor receives raw CUI commands or authority-bearing token data. Hide/show, full rebuild, disconnect, owner retirement and VM retirement make earlier epochs stale. Forged, stale, cross-Player and over-limit input fails before Luau entry; lifetime-sensitive state is revalidated again before callback entry. Rust/Carbon GUI intake may only validate and enqueue bounded work under I4 and must never recursively enter the VM. GUI mutation from an `Activated` callback synchronizes only in a later flush.

GUI resources and mutations participate in D7/D10 publication. Creation, GUI Signal connection, mutation and Show/Hide intent during provisional execution are staged as CarbonLuau-owned state; no client effect or usable action token exists before commit. The critical foreign-owner case remains `ResourceOwner = A` and `PublicationContext = B`: when provisional B mutates committed A-owned GUI state, a bounded GUI publication journal overlays the committed tree and gives B read-your-writes within that publication context. Commit first revalidates A's exact lifetime, then atomically applies the staged retained changes and presentation intent before they become eligible for later client synchronization. Failure discards the journal, leaving committed A state and clients unchanged. This transaction does not inspect or roll back arbitrary Luau tables or other ordinary memory.

Sibling attachment order is deterministic. Rendering orders siblings by `ZIndex`, then attachment ordinal, then object identity; `GetChildren` and duplicate-name `FindFirstChild` use retained attachment order rather than rendered Z order. A deep `Clone` copies classes, public properties, hierarchy and sibling order into new identities owned by the source object's domain, but copies no parent, viewers, presentation/client state, action tokens, pending dirties or Signal connections. `Destroy` is idempotent, recursively marks the subtree destroyed, removes retained parentage, disconnects GUI-owned Signals, invalidates tokens and prevents later queued callback entry. Reparenting is atomic in retained state: cycles and cross-domain parents fail without mutation; detachment and same-owner cross-`ScreenGui` moves are allowed and structurally reconcile all affected roots.

Healthy domain replacement keeps the old committed GUI publication active until candidate commit. A failed candidate leaves it unchanged. Successful commit publishes the new domain, invalidates and retires the old domain's GUI state, then converges affected clients by bounded best-effort reconciliation. Fatal VM retirement invalidates all old GUI tokens and resources; D9 reconstruction does not preserve runtime GUI trees or callbacks, and reconstructed scripts must recreate and re-show GUI through normal publication.

Implementations must enforce hard configurable safety bounds at appropriate object, tree, domain, exact Player connection and global scopes, including retained objects/depth/text, presentations/viewers, Signal connections, tokens, dirty state, serialization and event admission. Numeric transport and scheduling values in [the design record](../GuiFoundation1.md#bounds-and-tuning) are initial implementation/qualification targets, not permanent compatibility guarantees. Any public hard limit or default must be documented and qualified before support. Host-specific CUI mechanics remain adapter evidence; the invariant is deterministic bounded convergence from retained authority, not a promise about a particular Carbon/Rust method.

GUI Foundation 1 is part of the experimental `CarbonLuau
0.4.0-experimental` scripting identity and package `0.4.0`. This is the smallest
accurate identity because the addon-capable candidate has not been released and
GUI is an additive part of its first public compatibility surface. Native ABI
`1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and the
pinned Luau revision are unchanged. Authenticated-client GUI observations remain
unqualified and non-gating under the explicit Foundation 1G scope decision.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D16 — GUI Foundation 2 deterministic layout and rich controls

GUI Foundation 2 additively extends D15 with retained `UIListLayout`,
`UIPadding`, `ScrollingFrame`, `ImageLabel`, `ImageButton` and `TextBox`, the
immutable `ImageSource` value type, and `GuiObject.LayoutOrder`.
`ImageButton.Activated` reuses D15 interaction semantics and `TextBox` adds
`Submitted(Player, Text)`. D15 remains authoritative for ownership,
publication, Presentation lifetime, synchronization, interaction security,
replacement, recovery and bounded resources except where this decision
specializes new Foundation 2 state.

CarbonLuau computes Foundation 2 layout from retained `UDim`/`UDim2` state.
Host or Unity layout groups, client viewport measurements and client text
measurements are not authoritative. `UIListLayout` and `UIPadding` are ordinary
owner-bound, non-rendering retained children; a parent may have at most one of
each. `LayoutOrder` controls list geometry independently of `ZIndex`, retained
attachment order and `GetChildren`. List layout never rewrites script-visible
`Position`; removing it restores retained `Position` as projection authority.
Child `Size` remains retained authority, hidden children consume no list space,
and recomputation is bounded to the relevant direct arranged children.

`ScrollingFrame` is a retained `GuiObject` container. `CanvasSize`,
`ScrollingDirection` and `ScrollingEnabled` are shared retained state. Current
scroll offset, inertia, gesture/drag state and related transient behavior belong
only to each Presentation/client. Foundation 2 exposes neither
`CanvasPosition` nor `AutomaticCanvasSize`, and CarbonLuau does not claim to
know current client scroll state. Full reconciliation, Hide/Show, replacement,
disconnect or recovery may reset client-local scroll position.

`ImageSource` is an immutable, host-lifetime-independent value with only
`None`, `Sprite`, `Png`, `Item` and `SteamAvatar` source kinds. It grants no
filesystem, FileStorage, Carbon image-database, network or other host
capability. Foundation 2 provides no arbitrary URL image source. `ImageLabel`
and `ImageButton` consume this typed value. `ImageButton.Activated` uses the
existing opaque, exact-Player, Presentation-bound D15 action authority.

`TextBox.Text` is shared retained server state; the currently typed client draft
and focus/cursor/selection state are Presentation-local. Client editing never
implicitly mutates retained `Text`. Submission produces the validated immutable
payload `Submitted(Player, Text)` and changes retained `Text` only when script
explicitly assigns it. Foundation 2 TextBox is single-line and adds retained
`PlaceholderText`, `MaxLength` and `TextEditable`; it does not expose counterfeit
Roblox `FocusLost` semantics.

Text submission extends the one existing private GUI ingress with typed action
records rather than adding another client-command transport. Action kind
distinguishes `Activated` from `TextSubmitted`; authority remains bound to VM
generation, owner domain lifetime, `ScreenGui`, target, exact Player connection
and Presentation epoch. Payloads are bounded, validated and immutable; queue
and rate admission plus listener fanout are atomic; lifetime, action kind,
target availability, editability and listener registration are revalidated
before scheduled Luau entry. Luau never supplies commands, tokens, target IDs
or Presentation IDs, and Carbon dispatch never synchronously enters Luau.

The public `Submitted` contract requires exact preservation of supported
single-line text through the current authenticated Rust/Carbon transport,
including spaces, leading/trailing and repeated whitespace, quotes,
backslashes and Unicode. If current authenticated-client/host qualification
cannot establish that contract, `TextBox` is deferred from Foundation 2. The
contract must not be weakened into console-command argument semantics and input
must not be silently normalized merely to ship the class. This is a
qualification gate, not an unresolved architecture choice.

Foundation 2 adds an internal layout-affecting dirty classification. A layout
mutation recomputes only relevant direct arranged children; there is no
historical layout-work queue and latest retained state wins. Overflow collapses
to D15 whole-presentation reconciliation. Image color/transparency and retained
TextBox text/placeholder/editability may patch where supported; image source and
scrolling configuration may initially require full reconciliation.
Presentation-local drafts and scroll state are not retained dirty state. These
classifications do not create a second consistency model.

D15 ownership and publication apply without exception. Layout helpers are
ordinary domain-owned retained resources; `ImageSource` has no domain lifetime;
`Submitted` Signal ownership follows the `TextBox` owner; input authority is
Presentation-specific; and local scroll state belongs only to the Presentation.
All retained Foundation 2 mutations participate in the existing GUI publication
journal. Provisional work creates no client effect or usable interaction
authority before commit. Sharing references never transfers ownership.

Foundation 2 implementations must bound layout cardinality/work, TextBox scalar
and UTF-8 input, raw typed payload, input admission, image-source
representations, projected render elements and queued GUI text bytes. The
initial hard safety envelope is one `UIListLayout` and one `UIPadding` per
parent; the existing 64 direct-child layout bound; `MaxLength` at most 256
Unicode scalars; submitted text at most 1,024 UTF-8 bytes; raw input command tail
at most 1,536 UTF-8 bytes; sprite source at most 256 UTF-8 bytes; canonical PNG,
Steam and skin identifiers at most 20 ASCII digits; queued text at most 64 KiB
per domain and 256 KiB globally; and a full authoritative Presentation that
fits configured projection/reconciliation limits. Layout helpers count against
normal object limits and interactive controls against existing token limits.
Per-token submission rates and exact scheduling/timing values remain
implementation qualification targets, not permanent public compatibility
guarantees. Existing serializer limits must not simply be raised to fit richer
controls.

Foundation 2 excludes `UIGridLayout`, `AbsoluteContentSize`,
`AutomaticCanvasSize`, `CanvasPosition`, general `ClipsDescendants`, `UIStroke`,
`UICorner`, `TextScaled`, `TextBounds`, rich text, wrapping controls, public font
selection, `FocusLost`/`Focused`/`CaptureFocus`/`IsFocused`, multiline or
password TextBox behavior, arbitrary URL images, Carbon image-database
integration, drag/drop, client geometry queries and arbitrary client scripting.
These are outside Foundation 2, not permanent rejection of a separately designed
future phase.

Existing Foundation 1 behavior remains unchanged unless an author uses a new
Foundation 2 object/property: ordinary `Position`, coordinates without padding,
`TextButton.Activated`, `ZIndex`, attachment-order `GetChildren`, `Clone`,
`Destroy`, D15 publication/replacement/recovery and one-tree/multiple-Presentation
semantics are preserved. GUI Foundation 2F assigns the implemented layout,
typed-image and retained-scrolling subset to the already-unreleased package
`0.4.0` and scripting API `0.4.0-experimental`. This is additive to the first
public 0.4 compatibility surface and does not warrant 0.5.0. Native ABI `1.4`,
provider protocol `1.2`, package schema `1` and the pinned Luau revision remain
unchanged. `TextBox`, `Submitted` and typed text ingress remain deferred and
unimplemented; their design above is preserved for possible future reconsideration.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D17 - GUI Foundation 3 deterministic grids, clipping, fonts and Presentation scroll intent

GUI Foundation 3 additively extends D15 and D16 with deterministic
`UIGridLayout`, bounded rectangular `Frame.ClipsDescendants`, immutable
project-owned `GuiFont` values and retained `TextLabel.Font` and
`TextButton.Font`, plus one-way per-Presentation `ScrollingFrame:ScrollTo`,
`ScrollToTop` and `ScrollToBottom` effects. No other Foundation 3 public feature
is approved. D15 and D16 remain authoritative for GUI ownership, publication,
Presentation lifetime, synchronization, interaction security, retained versus
client-local scrolling state, typed images, replacement, recovery and resource
discipline except where this decision explicitly specializes the new surface.

`UIGridLayout` is an ordinary owner-bound, non-rendering retained layout helper
computed by CarbonLuau from retained affine geometry. Its properties are
`CellSize` (`UDim2`, default `UDim2.fromOffset(100, 100)`, non-negative scale
and offset components), `CellPadding` (`UDim2`, default
`UDim2.fromOffset(0, 0)`, non-negative scale and offset components),
`FillDirection` (`"Horizontal"` or `"Vertical"`, default `"Horizontal"`),
`FillDirectionMaxCells` (integer `1..64`, default `1`),
`HorizontalAlignment` (`"Left"`, `"Center"` or `"Right"`, default `"Left"`)
and `VerticalAlignment` (`"Top"`, `"Center"` or `"Bottom"`, default `"Top"`).
A parent may contain at most one active `UIListLayout` or `UIGridLayout`, not
one of each; `UIPadding` may coexist with either. Conflicting layout-manager
creation or reparenting fails atomically.

Grid ordering is `LayoutOrder`, then retained attachment ordinal, then object
identity. `ZIndex` remains independent and `GetChildren` remains attachment
order. Hidden children consume no cell. `UIPadding` establishes the content
rectangle before grid geometry is computed. Explicit `FillDirectionMaxCells`
determines row or column topology without client pixels or automatic
fit-to-parent behavior. Grid overflow does not resize cells, change topology,
clip automatically or query available client space. Nested grids are allowed;
each layout operates only on its own direct children. Foundation 3 has no
`StartCorner`, `SortOrder`, `AbsoluteContentSize`, automatic cell sizing or
client-dependent wrapping.

While a direct child is governed by `UIGridLayout`, its retained `Position` and
`Size` remain synchronously readable and writable but neither determines its
projected outer rectangle. The grid cell determines projected position and
projected size. `AnchorPoint` remains retained and participates only in encoding
the computed cell rectangle; it does not change the cell bounds. Grid projection
never rewrites retained `Position` or `Size`. Removing or leaving the grid
restores their latest retained values as projection authority. This intentionally
differs from `UIListLayout`, under which retained child `Size` remains projection
authority.

`Frame.ClipsDescendants` is retained shared boolean state with default `false`.
When true, CarbonLuau projects private rectangular clipping state below the
Frame and routes projected descendants through it. That representation is not
retained or Luau-addressable, does not depend on Frame background visibility or
transparency, and affects descendant rendering and interaction eligibility.
Nested clipping is allowed within the effective depth bound. The property is
initially structural/full-rebuild for synchronization. `ScrollingFrame` keeps
its existing private viewport clipping and is not redefined by this property.

Authenticated current-client qualification must establish visible descendant
clipping, transparent-parent behavior, nested clipping, hit rejection outside
clipped regions and interoperability with `ScrollingFrame` clipping. If that
contract cannot be established, `ClipsDescendants` must be omitted from the
implemented/public Foundation 3 release subset rather than weakened. This is a
qualification gate, not an unresolved architecture choice.

`GuiFont` is an immutable host-lifetime-independent value with exactly
`RobotoCondensedRegular`, `RobotoCondensedBold`, `DroidSansMono` and
`PermanentMarker`. It has no constructor and exposes no arbitrary string, path,
filesystem or other host capability. `TextLabel.Font` and `TextButton.Font` are
retained shared properties, default to `RobotoCondensedRegular`, and are
patchable where qualified. Host font identifiers remain backend details. Each
member requires supported-client qualification before public release; an
unavailable member must be removed before qualification rather than silently
falling back or exposing host strings. Existing visual behavior remains
unchanged until an author explicitly uses the feature.

`ScrollingFrame:ScrollTo(Player, Vector2)`, `ScrollToTop(Player)` and
`ScrollToBottom(Player)` are Presentation-specific one-way effects, not
retained scroll state, `CanvasPosition`, readable state or shared viewer state.
`ScrollTo` accepts normalized CarbonLuau coordinates from `0..1`, where `(0, 0)`
is top-left and `(1, 1)` is bottom-right, and transmits only enabled axes.
Top/Bottom change only vertical intent and require Y scrolling. Each operation
targets one exact D11 Player connection, one current eligible Presentation and
the specified `ScrollingFrame`; absence produces a controlled programming error
under the existing GUI error model. Other viewers remain unaffected, and
`Clone` copies no pending effect.

A scroll intent is bounded one-shot Presentation effect state, not retained
authority. During committed execution, CarbonLuau validates the
`ScrollingFrame` owner, exact Player and current Presentation, stages the latest
pending effect and sends it during a later GUI flush. During provisional
execution, `ResourceOwner` is the `ScrollingFrame` owner and
`PublicationContext` is the admitted provisional operation. The journaled
effect cannot become client-visible before commit. Commit first publishes
retained changes, then resolves the resulting current Presentation, binds the
effect to that Presentation epoch and enqueues it for a later flush. If the exact
Player or Presentation disappears before commit, the ephemeral effect is
discarded with bounded diagnostics without rolling back otherwise valid retained
publication.

Pending scroll effects are bounded and latest-wins per
`(Presentation, ScrollingFrame)` with no historical log. If a rebuild and effect
are due together, the rebuild is emitted first, or the effect may equivalently
be folded into the replacement projection. A local host rejection retains the
latest effect for a later eligible synchronization attempt. Local host acceptance
consumes it; there is no client acknowledgement, and a later unrelated rebuild
does not replay it. Before public qualification, current-client evidence must
cover top/bottom orientation, midpoint, horizontal/vertical/XY behavior, exact
Player isolation, repeated same-frame latest-wins behavior, rebuild ordering and
local failure/retry. A bounded structural replacement may implement the same
one-way contract if partial update is unreliable; `CanvasPosition` remains
excluded.

Foundation 3 adds these synchronization classifications: grid
creation/destruction/reparenting and `ClipsDescendants` are structural; grid
property mutation, `LayoutOrder` under list/grid, `Visible` under grid,
`AnchorPoint` under grid and `UIPadding` with grid are layout-affecting;
`LayoutOrder` without layout is metadata-only; `Position` and `Size` under grid
are retained-only while the grid governs projection; `Font` is patchable; and
`ScrollTo*` is a Presentation-local effect. Grid recomputation touches only
direct arranged children. There is no historical geometry queue, and existing
dirty overflow still collapses to authoritative whole-presentation
reconciliation.

D15/D16 ownership and retained publication apply without a new ownership model.
`UIGridLayout` is an ordinary owner-bound retained GUI resource;
`ClipsDescendants` and `Font` are ordinary retained properties; `GuiFont` owns no
host resource; and foreign-domain mutation continues using the existing
`ResourceOwner` and `PublicationContext` semantics. Only `ScrollTo*` uses the
new bounded Presentation-effect publication rule.

Foundation 3 preserves existing global envelopes. Initial hard additions are
one active `UIListLayout`/`UIGridLayout` total and one `UIPadding` per parent;
at most 64 arranged grid children; `FillDirectionMaxCells` `1..64`;
O(direct-child) grid work with no synthesized empty retained cells; effective
nested clipping depth four, counting explicit Frame clips and private
`ScrollingFrame` viewport clips; one private clipping projection element per
clipping Frame; the unchanged 257 projected-element screen limit; at most 16
pending scroll effects per Presentation, 512 per domain and 4096 globally; and
one latest pending value per `(Presentation, ScrollingFrame)`. A
`ClipsDescendants` mutation that would exceed authoritative projection bounds
fails atomically. Timing and rate measurements are qualification targets, not
public compatibility guarantees.

Foundation 3 excludes `AutomaticSize`, `AutomaticCanvasSize`,
`AbsoluteContentSize`, `CanvasPosition`, `UIAspectRatioConstraint`,
`UISizeConstraint`, generic constraints, `UIStroke`, a CarbonLuau outline
helper, `UICorner`, advanced image scale modes, arbitrary fonts or font paths,
`TextBox`, animations/tweens, drag/drop, focus/navigation, client geometry and
arbitrary client scripting. `TextBox` remains deferred under D16's exact-text
transport gate and is not reopened.

Existing Foundation 1/2 behavior remains unchanged unless an author explicitly
uses a Foundation 3 feature. Ordinary `Position`/`Size`, `UIListLayout`,
`UIPadding`, `LayoutOrder`, `ZIndex`, attachment-order `GetChildren`,
`Clone`/`Destroy`, `TextButton`/`ImageButton.Activated`, explicit `CanvasSize`,
client-local scrolling, `ImageSource`, D15/D16 publication,
replacement/recovery and one-tree/multiple-Presentation semantics are preserved.
Grid projection overrides `Position`/`Size` only while the child is actively
governed by `UIGridLayout`.

The architecture adoption itself assigned no identity. GUI Foundation 3E later
qualified the implemented additive server-side model and assigned it to the
still-unreleased package `0.4.0` and scripting API `0.4.0-experimental`. Native
ABI `1.4`, provider protocol `1.2`, package schema `1` and the pinned Luau
revision remain unchanged. Authenticated-client clipping, font and scroll
behavior remains outside the qualified evidence envelope.

<!-- END RECORD Invariants.md -->

