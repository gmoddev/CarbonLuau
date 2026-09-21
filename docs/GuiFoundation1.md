# GUI Foundation 1 architecture

Status: **approved architecture; Foundations 1A through 1F implement the retained
runtime, presentation projection, automatic synchronization, secure Activated
ingress and lifecycle/runtime closure. Authenticated-client qualification and
release planning remain deferred**.

[D15](Invariants.md#d15--gui-foundation-1-retained-presentation-model) is the canonical policy owner. This record preserves the detailed rationale and implementation guidance for future GUI phases. If this record and D15 conflict, D15 wins and both documents must be reconciled before implementation continues.

## Model and service boundary

GUI Foundation 1 is a small, server-driven retained GUI API over a CarbonLuau-owned backend:

```text
Luau retained objects (authoritative)
    -> internal presentation/render plan
    -> IGuiBackend
    -> Rust CUI adapter
    -> best-effort client projection
```

The backend may use supported Carbon/Rust CUI facilities, but Carbon LUI/CUI objects, JSON and commands are never the canonical state or public API. A deterministic in-memory backend is required for unit tests. Host-specific send and update behavior must be requalified against the supported Carbon/Rust revision instead of being frozen as a timeless public promise.

Scripts acquire a domain-bound service:

```lua
local Gui = game:GetService("Gui")
local Screen = Gui:Create("ScreenGui")
local Frame = Screen:Create("Frame")
```

`Parent:Create(ClassName)` creates through the parent object's owner-bound facade and parents the new object in one validated operation. `ScreenGui` roots may only be created through `Gui`. There is no `Instance.new`, `Gui.new`, generic Instance hierarchy or Roblox DataModel/replication claim.

## Public Foundation 1 boundary

The first public boundary is intentionally limited to:

| Kind | Members |
|---|---|
| Service | `Gui` |
| Classes | `ScreenGui`, `Frame`, `TextLabel`, `TextButton` |
| Values | `UDim`, `UDim2`, `Vector2`, `Color3` |
| Hierarchy | `Name`, `Parent`, `ClassName`, `Create`, `GetChildren`, `FindFirstChild`, `IsA` |
| Layout | `Position`, `Size`, `AnchorPoint`, `Visible`, sibling-local `ZIndex` |
| Visual | `BackgroundColor3`, `BackgroundTransparency` |
| Text | `Text`, `TextColor3`, `TextTransparency`, `TextSize`, `TextXAlignment`, `TextYAlignment` |
| Interaction | `TextButton.Activated` with exact `Player` argument |
| Screen lifecycle | `Show`, `Hide`, `IsShown` |
| Object lifecycle | `Clone`, `Destroy` |
| Runtime | retained hierarchy and automatic bounded/coalesced synchronization |

Foundation 1 explicitly excludes `ImageLabel`, `ImageButton`, `TextBox`, scrolling, automatic/grid/list layouts, advanced styling and constraints, raw CUI APIs, arbitrary client commands, client scripts, dynamic child indexing, recursive lookup, `WaitForChild`, absolute client geometry and a public `Presentation` type.

### Class behavior

`Name` is a UTF-8 label, never a client or security identity. Duplicate sibling names are permitted. `ClassName` is read-only. `GetChildren()` returns retained attachment order; `FindFirstChild(Name)` returns the first direct match in that order. `ScreenGui:IsA("GuiObject")` is false; `Frame`, `TextLabel` and `TextButton` return true.

`ScreenGui.Parent` is permanently `nil`. Only `ScreenGui` has `Show(Player)`, `Hide(Player)` and `IsShown(Player)`. It does not gain Roblox `Enabled`, `DisplayOrder`, `ResetOnSpawn` or `IgnoreGuiInset` properties in Foundation 1.

Common `GuiObject` properties and initial design defaults are:

| Property | Type | Initial default/validation |
|---|---|---|
| `Parent` | same-owner `ScreenGui`, `GuiObject`, or `nil` | detached; no cycles |
| `Position` | `UDim2` | zero |
| `Size` | `UDim2` | class-specific |
| `AnchorPoint` | `Vector2` | `(0, 0)`, components `0..1` |
| `Visible` | boolean | `true` |
| `BackgroundColor3` | `Color3` | white |
| `BackgroundTransparency` | number | class-specific, finite `0..1` |
| `ZIndex` | integer | `1`, initial range `0..1000` |

Initial class defaults are `100x100` and opaque background for `Frame`, `100x30` and transparent background for `TextLabel`, and `100x36` and opaque background for `TextButton`. Both text classes initially use empty text, black text color, zero text transparency, size 14 and centered horizontal/vertical alignment. Alignment values are `Left`/`Center`/`Right` and `Top`/`Center`/`Bottom`. Foundation 1 uses one backend-selected fixed font and exposes no `Font` property.

All invalid classes, properties, types, non-finite values, out-of-range values, cycles, cross-domain parents, stale lifetimes and resource-limit breaches raise controlled ordinary Luau errors and leave retained state unchanged. Values are not silently clamped.

### Value semantics

The four values are immutable CarbonLuau-owned userdata with read-only fields, component-wise equality, normalized negative zero, and rejection of NaN/infinity. They contain no host lifetime and may be shared across domains.

```lua
UDim.new(Scale, Offset)
UDim2.new(XScale, XOffset, YScale, YOffset)
UDim2.fromScale(XScale, YScale)
UDim2.fromOffset(XOffset, YOffset)
Vector2.new(X, Y)
Color3.new(R, G, B)
Color3.fromRGB(R, G, B)
```

Initial validation targets are `UDim.Scale` in `-8..8`, offsets and generic `Vector2` components in `-32768..32768`, normalized `Color3` components in `0..1`, and integer RGB components in `0..255`. Foundation 1 adds no arithmetic framework. Transparency remains author-facing `0 = opaque`, `1 = transparent` even if the backend uses alpha.

## Ownership and publication

Every domain receives a distinct `Gui` facade. An object created through that service or an existing object remains owned by the facade object's exact domain lifetime forever. A shared public module may return GUI references to another addon, and the consumer may use valid operations, but the reference does not transfer resource ownership. Cross-domain parenting is always rejected so one retained tree has one owner.

GUI operations preserve the existing separation:

```text
ResourceOwner        = domain lifetime bound to the Gui/object facade
AdmissionContext     = admitted Luau operation
PublicationContext   = current module/candidate publication scope
Deadline             = original admitted operation
Host-backed validity = owning domain lifetime
```

If provisional B mutates committed A-owned GUI state, the mutation enters a bounded publication journal owned by B's current publication context. Reads by B in that context see the overlay. Commit revalidates A, atomically applies the final staged retained state and presentation intent, and only then makes it eligible for a later GUI flush. Failure drops the overlay without changing committed A state or clients. Candidate `Create`, `Connect`, `Show` and `Hide` similarly produce no client effect or usable token before commit. This is a transaction over CarbonLuau-owned GUI state only.

Connecting a callback through an A-owned `Activated` Signal creates an A-owned host connection even if the closure was defined by B. Ordinary cross-domain Luau closures retain D4 behavior; captured host-backed B facades still fail closed after B retires.

## Retained trees and presentations

A `ScreenGui` exists without viewers. Showing the same tree to several exact Player connections creates one internal `Presentation` per connection; it does not clone the tree. Later committed mutations fan out to every current presentation. Authors clone explicitly for per-player retained state.

Each Presentation tracks the `ScreenGui`, exact D11 connection token, opaque client root identity, epoch, desired visibility, sent/believed revision, reconciliation state and presentation-specific action tokens. `IsShown` reports desired server state only. `Show` and `Hide` are idempotent for one exact connection; disconnect removes the presentation, and a reconnect with the same account never inherits it.

`Show` followed by `Hide` before a flush emits no obsolete creation. `Hide` followed by `Show` ends the old epoch and emits one later full replacement with fresh action tokens.

## Layout translation

CarbonLuau uses top-left, downward-positive familiar `UDim2` semantics while Rust CUI uses lower-left normalized anchors. For `Position = (pxS, pxO, pyS, pyO)`, `Size = (sxS, sxO, syS, syO)` and `AnchorPoint = (ax, ay)`, the initial adapter formula is:

```text
AnchorMin.x = pxS - ax * sxS
AnchorMax.x = pxS + (1 - ax) * sxS
OffsetMin.x = pxO - ax * sxO
OffsetMax.x = pxO + (1 - ax) * sxO

AnchorMin.y = 1 - pyS - (1 - ay) * syS
AnchorMax.y = 1 - pyS + ay * syS
OffsetMin.y = -pyO - (1 - ay) * syO
OffsetMax.y = -pyO + ay * syO
```

The adapter emits pivot `(ax, 1 - ay)`. Each private presentation root fills the CUI overlay. Offsets are CUI UI units, not guaranteed physical monitor pixels. Actual aspect ratio, UI-scale and cursor behavior require authenticated-client qualification.

## Mutation, ordering and reconciliation

A committed property assignment immediately changes the retained tree and is readable by the same operation. Each affected root advances an internal revision. Metadata-only changes need no client operation. Supported layout, visibility, color and text changes may be patched when the presentation is believed coherent. Creation, destruction, reparenting and `ZIndex` changes are structural and require a full presentation replacement in Foundation 1.

Dirty state is property-based and coalesces repeated writes to final state. A bounded owner-thread GUI phase runs only after Luau has returned. Dirty-detail overflow promotes the affected presentation to a full rebuild instead of dropping the authoritative mutation. Budget exhaustion carries one newest-revision obligation forward with round-robin domain fairness; it does not retain an unbounded history of intermediate updates.

Every attachment receives a parent-local ordinal. Render order is ascending `ZIndex`, attachment ordinal, then object identity, so higher Z renders later. Reparenting appends into the destination attachment order. Lookup and `GetChildren` continue to use attachment order.

Initial show, structural change, known delivery uncertainty and periodic reconciliation use a parent-before-child full replacement from current retained state. Pure supported property changes may use stable internal IDs and partial updates. Script-visible `Name` never contributes to client IDs. A locally accepted send is only `LastSent`/`BelievedCurrent`, never acknowledged client state.

Asynchronous send failure leaves retained state and desired visibility intact, records bounded diagnostics and marks the presentation for full resynchronization. Hide invalidates tokens immediately even when client destruction fails. A disconnect discovered before send drops the work. A later full replacement starts from a deterministic root replacement so partial or uncertain client state can converge. No exactly-once delivery guarantee exists.

## Interaction security and scheduling

CarbonLuau owns one private client-origin action path. Each visible button/presentation receives a cryptographically random opaque token whose server-side record binds VM generation, owner domain, `ScreenGui`, Presentation epoch, exact D11 Player connection and button. Tokens are authority lookup keys, not self-authenticating data, and never become Luau command-building primitives.

Initial show, hide/show, full rebuild, connection loss, domain replacement and VM retirement rotate or invalidate token epochs. Intake validates token state, exact sender connection, domain/screen/button lifetime and ancestry, desired visibility, rate limits and callback queue capacity. Lifetime-sensitive checks repeat before Luau callback entry. Invalid, forged, stale or over-limit submissions never enter Luau and only affect bounded diagnostics.

The host command handler may validate and enqueue work only. `Activated` callbacks run later through the existing bounded scheduler and receive the exact Player proxy. Destruction/hide between admission and execution suppresses stale work. Mutation inside a callback updates retained state but cannot synchronously re-enter Rust/Carbon GUI handling; it reaches clients in a later flush.

## Object lifecycle

`Clone()` deep-copies concrete classes, public properties, hierarchy and sibling order into new object identities with the same owner domain. It copies no parent, viewers, Presentation state, client IDs, tokens, dirty state, revisions or Signal connections. Each cloned button has a new empty `Activated` Signal.

`Destroy()` immediately and recursively marks nodes destroyed, removes parentage, disconnects GUI-owned Signals, invalidates affected tokens, suppresses not-yet-admitted callbacks and structurally dirties affected roots. Destroying a `ScreenGui` also makes all presentations desired-hidden and queues best-effort client removal. Repeated `Destroy()` is a cleanup-safe no-op; other operations through destroyed/stale references raise controlled errors.

Reparenting is atomic in the retained model. Cycles and cross-domain targets fail without changes. `Parent = nil`, same-tree moves and same-owner cross-`ScreenGui` moves are allowed. Cross-root moves dirty both roots and invalidate/rebuild affected presentation action identities from final state.

Healthy replacement keeps A1 published until A2 commits. Failed A2 leaves A1 GUI state unchanged. Successful A2 publication invalidates and retires A1 GUI state, after which clients converge by bounded best-effort destroy/rebuild. Fatal VM retirement invalidates all tokens and GUI resources; D9 reconstruction runs scripts from committed source/package snapshots and does not preserve runtime trees or callback closures.

## Bounds and tuning

The architecture requires hard configurable bounds at object, tree, domain, exact Player connection and global scopes. Implementations must bound at least object/depth/children counts, text and name bytes, screens/viewers/presentations, button/listener/token counts, dirty tracking, clone work, serialization, event rates and queue admission. A root must fit the configured bounded full-presentation serializer before it can be shown.

The approved review proposed these **initial safety-default candidates**, subject to implementation review and qualification: 128 objects per `ScreenGui`, depth 16, 64 children per object, 1,024 objects and 32 screens per domain, 8,192 objects globally, 16 active screens per Player connection, 256 viewers per screen, 512 presentations per domain, 4,096 presentations globally, 64 buttons per screen, 8 listeners per button, 256 GUI Signal connections per domain, 64 action tokens per presentation, 8,192 tokens per domain, 32,768 tokens globally, 64-byte UTF-8 names, 2,048-byte text values, 32 KiB aggregate text per screen, 512 detailed dirty objects per domain, and clone limits matching 128 objects/depth 16.

The following are **qualification/tuning targets, not compatibility guarantees or proven host limits**: 64 KiB per CUI operation, 64 presentation sends and 256 KiB serialized data per flush, a 1 ms GUI flush CPU budget, a full reconciliation after 32 patch batches, 20 accepted GUI interactions per second per Player with burst 20, and 8 per second per action with burst 8. Implementation may adjust them from evidence while preserving D15's boundedness and fairness. Public defaults and clamps must be documented when assigned.

## Compatibility and qualification gates

GUI Foundation 1's package and scripting API identity is **UNASSIGNED / release-planning gated**. The current package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau revision remain unchanged by this design record.

Implementation is split into explicitly scoped phases. [GUI Foundation 1A](GuiFoundation1A.md)
owns only the internal descriptor, limit, render-contract and deterministic mock
backend substrate. [GUI Foundation 1B](GuiFoundation1B.md) owns the retained
objects, values, lifecycle, ownership, Signal presence and publication journal;
it does not implement presentations or client rendering. [GUI Foundation 1C](GuiFoundation1C.md)
owns internal presentations, exact Player-bound Show/Hide state, deterministic
full-plan compilation and the production Rust CUI projection. It remains a
static projection phase. [GUI Foundation 1D](GuiFoundation1D.md) owns revisioned
dirty state, coalesced property patches, structural full reconciliation and the
shared bounded post-Luau GUI flush. [GUI Foundation 1E](GuiFoundation1E.md) owns
the private action command, presentation-bound authority, exact Player
validation and bounded scheduler ingress. [GUI Foundation 1F](GuiFoundation1F.md)
owns replacement, fatal recovery, provider/host teardown, backend retry,
operator diagnostics and leak/stress closure.

The exact remaining Foundation 1G work is authenticated-client visual, cursor,
click and reconciliation evidence; live multi-viewer qualification; current
Carbon/Rust adapter upgrade checks; and an explicit GUI release/API identity
decision after those gates pass. Foundation 1G is not authorization for another
public GUI feature.

Before public support, qualification must cover retained model and value semantics, layout golden cases, mutation ordering, shared-domain ownership, provisional foreign-owner publication, exact Player/token attacks and reconnects, bounds/queue exhaustion, no reentrant entry, backend fault injection, replacement/recovery cleanup, sanitizer coverage, live current Carbon/Rust rendering with an authenticated client, multi-viewer cost and host-upgrade adapter checks.

Authenticated-client evidence is required for claims about actual visual layout, click receipt, cursor behavior and client reconciliation. Controlled `BasePlayer` fixtures alone cannot establish those results. Exact supported Carbon/Rust revisions and any host-specific adapter assumptions belong in qualification evidence and compatibility documentation, not D15.
