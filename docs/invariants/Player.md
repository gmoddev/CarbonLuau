# Player: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D18 — Player Interaction Foundation 1

D18 approves exactly this public surface (C4 supersedes the historical Player
inventory spellings, without changing their scanner or mutation contracts): immutable `Vector3` construction,
X/Y/Z/Magnitude, exact equality, addition, subtraction, unary negation, scalar
multiplication in either operand order and scalar division;
`Player.Position`, `Player:Teleport(Position)`, `Player.Health`,
`Player.MaxHealth`, `PlayerInventory:CountItem(ShortName)`,
`PlayerInventory:HasItem(ShortName, Amount?)`; and `game:GetService("Items")` with
`Items:Exists(ShortName)`; plus implementation-gated
`PlayerInventory:GiveItem(ShortName, Amount, Behavior?)` and
`PlayerInventory:TakeItem(ShortName, Amount)` under D13. Gameplay C1 additively approved
`Player:GetInventory() -> PlayerInventory`, `PlayerInventory:CountItem(ShortName)`
and `PlayerInventory:HasItem(ShortName, Amount?)` under exactly the observation
semantics below. No other gameplay API is approved by this decision.

PlayerInventory is immutable and bound to its defining domain,
publication witness, VM and original D11 connection token/UserId pair. Acquisition
validates the live connection but performs no inventory scan, disk work, watch or
subscription. Each read revalidates that authority and delegates to the existing
D18 CountItem/HasItem adapter; C4 mutations delegate to the unchanged D13
GiveItem/TakeItem backends and shared exact-connection gate. No new scanner,
mutation aliases or global Inventory
service. A facade escaping failed candidate/module publication remains stale;
sharing never transfers ownership. Reconnect with the same account cannot retarget
it. C4 removes Player:CountItem, HasItem, GiveItem and TakeItem; no hidden aliases
remain. GiveItemBehavior and unrelated Player methods remain unchanged. Native
admission checks the current operation's committed eligibility before dispatching
facade mutations, even through a captured foreign facade or shared closure.
Gameplay C3
additively accepts only `PlayerInventory.ItemChanged(Context)` as sampled net
observation, not completed-operation tracking; ItemAdded/ItemRemoved remain deferred.
See [C1 qualification](../GameplayC1-Validation.md).

Private C2 sampled reconciliation is recorded in
[GameplayC2-Validation.md](../GameplayC2-Validation.md). It shares this physical
predicate and exact definition/connection authority, but a difference between
accepted samples is not proof of a completed host inventory operation. It
introduced no public Signal. C3's explicit sampled contract does not inherit
universal operation coverage or causal attribution from the private candidate.
ItemChanged uses an immutable ordinary `{Item, Before, After, Delta}` table,
where Delta equals After minus Before for two accepted physical observations.
Initial/new publication demand establishes a baseline without history. Intermediate
transfers may appear as decreases/increases; balanced changes between samples may
be missed. Failed snapshots invalidate the interval; overload drops and advances or
rebaselines safely, never rewinds or replays. Registration/rollback, defining owner,
original D11 connection and fresh callback admission reuse D7/D10/D23 machinery.
The shared 256 pending cap includes managed/native-held inventory reservations;
exact-owner refunds remain held until consumption/teardown. No synchronous Luau
entry, new scanner, mutation API or independent event scheduler is introduced.
See [C3 qualification](../GameplayC3-Validation.md) for platform status and numeric policy.

`Vector3` is a project-owned immutable value with no host, Player or domain
lifetime. X/Y/Z are finite Luau numbers; every stored component and arithmetic
result must be convertible to a finite Unity/System.Single coordinate at a host
boundary. The GUI `Vector2` coordinate bound does not apply. Equality compares
components exactly. Division by zero, nonfinite or host-unrepresentable results,
wrong operand types, `Vector3 * Vector3` and `Vector3 / Vector3` are programming
errors. `Unit`, `Dot` and `Cross` remain deferred.

`Player.Position` is a read-only live observation of the exact `BasePlayer` root
world position with no axis remapping, scale conversion, eye offset, terrain
projection, parent-local coordinate or exposed Transform. Mounted, parented,
sleeping, wounded and otherwise still-host-valid exact Players may be observed.
`Player.Health` and `Player.MaxHealth` are likewise read-only live host
observations. Health is not fabricated or clamped to MaxHealth; MaxHealth uses
current host maximum-health semantics and is not assumed constant or 100. A
nonfinite host value is a controlled host-state failure. Wounded and
dead-but-still-host-valid exact Players may be observed. None of these live
world-state properties gains D11's bounded disconnected Name/UserId snapshot.

Foundation 1 inventory is bounded, nonrecursive physical observation of the
exact Player's top-level main, belt and wear containers only. `CountItem` sums
positive physical stack amounts with checked arithmetic and excludes invalid,
destroying, removal-pending or non-reciprocal entries according to the qualified
adapter; condition does not exclude an otherwise valid item. `HasItem` defaults
an omitted Amount to one and may stop once the requested quantity is observed.
An explicit Amount is an exact positive Luau integer from 1 through `2^53 - 1`.
The result and accumulation must remain exactly representable under that public
integer-number contract. Traversal has a target-build-qualified hard inspected-
stack/work bound and fails closed when exceeded. It performs no nested,
backpack or special-container discovery and must not obtain its canonical answer
from a hook-virtualized count result. The concrete traversal limit belongs in
compatibility/facade policy after target-build inspection, not in D18.

The item identifier is the canonical lowercase Rust short name: a string of
1..128 ASCII bytes with no NUL, accepted exactly without silent lowercasing,
display-name lookup, fuzzy matching, numeric ID or exposed `ItemDefinition`.
Malformed or noncanonical names are programming errors. A syntactically valid
unknown name is ordinary absence: `Items:Exists` returns false, `CountItem`
returns zero and `HasItem` returns false. The `Items` service exposes no metadata
beyond bounded read-only `Exists` in Foundation 1. Its facade remains bound to
and revalidates its owning domain lifetime under D10, and no host
item/container object crosses into Luau.

`GiveItem` and `TakeItem` are committed-only inventory mutations under D13's
PREPARE/COMMIT/VERIFY model. TakeItem is implemented by Player-1F-A; GiveItem
InventoryOnly is implemented by Player-1F-B within its recorded qualification.
Both return `true` only for a verified physical postcondition, `false` only for
definite PREPARE rejection before COMMIT, and a controlled error for any
post-COMMIT uncertainty. Their required Amount is an exact integer from 1
through `Int32.MaxValue`. For TakeItem, a canonical unknown item is a definite
PREPARE rejection and returns false before COMMIT. GiveItem treats an unknown
canonical item as a configuration error before creation.

The GiveItem signature returns boolean and uses the project-owned
`GiveItemBehavior` enum, initially only `GiveItemBehavior.InventoryOnly`.
Omitting Behavior selects InventoryOnly; a boolean DropIfFull parameter is not
accepted. Unsupported behavior values are programming errors before mutation.
Player-1F-B exposes only InventoryOnly. `DropRemainder` is only a
possible future design name, not a reserved/accepted enum member or runtime
value. It needs separate design and qualification of inserted/dropped amounts,
ownership, partial placement, world-entity verification, return semantics,
plugin interference and failures after partial placement/drop. Generic Rust
GiveItem must not be assumed to satisfy that future contract.

InventoryOnly means delivery into the exact Player's accepted ordinary top-level
main/belt/wear inventory, with stacking and multiple stacks allowed but no exact
slot/container guarantee. Partial placement is never success. Full inventory
may return false only when PREPARE establishes impossibility before item
creation. InventoryOnly does not intentionally drop overflow; D13's revised G1
requires a no-world-drop planned target-build path within I12's supported-host
boundary, with every returned Item accounted for or subjected to supported
cleanup. Qualification is recorded per target in Player-1F-B. I12 does not
convert observed world delivery into InventoryOnly success.

TakeItem means verified removal of the requested physical quantity from the
same accepted top-level inventory. Insufficient physical quantity may return
false only before removal COMMIT. A hook-returned count alone cannot prove
success; D13 requires matching host result and bounded physical before/after
delta. GiveItem and TakeItem qualify independently; public symmetry is not an
invariant.

`Player:Teleport(Vector3)` is the sole Foundation 1 spatial mutation and
returns no values. It revalidates the exact current D11 Player connection and
owning domain, requires committed execution plus an alive, non-spectating,
non-wounded/non-incapacitated Player, and does not implicitly wake a sleeping
Player. The qualified adapter owns required dismount/parent normalization and
the target Rust build's fall, anti-cheat, movement, network-group, snapshot,
forced-position and client-convergence sequence. The public contract is only
"move this exact Player to this world coordinate". It promises no safe-position
search, terrain/ground projection, collision avoidance, path-trigger traversal,
transactional rollback or client acknowledgement. If host mutation begins and
later work fails, CarbonLuau reports a controlled host-operation failure without
claiming rollback.

Teleport has two mandatory implementation gates: establish the correct adapter
sequence on the exact target Rust/Carbon build, and qualify authenticated-client
convergence at the intended destination. Client qualification covers no
immediate rubber-band, long-distance/network-group transition, fall state,
mount/parent convergence and repeated teleports. Until both gates pass,
Teleport may be implemented and model-qualified but must not be described as
authenticated-client-qualified public behavior. These gates do not block the
read-only Player-1A/1B/1C surface. Player-1C separately requires exact target-
build evidence that direct bounded main/belt/wear inspection satisfies the
physical observation contract; if not, Player-1C is deferred rather than
changing the API meaning.

All new Player operations inherit D11 exact-connection and domain-lifetime
semantics without another proxy type. Same-account reconnect never retargets an
old proxy. After disconnect, `Position`, `Health`, `MaxHealth`, `CountItem`,
`HasItem`, `Teleport`, `GiveItem` and `TakeItem` fail with the controlled stale-
Player model, while an ordinary `Vector3` remains usable. Host access stays
owner-thread-only. Reads are bounded direct observations and avoid intentionally
invoking plugin hooks where possible. Synchronous host/plugin callbacks caused
by Teleport must never recursively enter the active Luau VM; CarbonLuau-owned
resulting work is copied into bounded payloads and admitted later under the
existing scheduler.

Pure `Vector3` work plus `Position`, `Health`, `MaxHealth`, `Items:Exists`,
`CountItem` and `HasItem` are allowed during provisional execution. Teleport is
an irreversible committed-only host mutation under D7/D10; GiveItem and
TakeItem are the same under D13. All three are rejected from root/addon
candidates, first-load modules and provisional dependency chains.
Resource ownership, publication context and deadline remain orthogonal; a
dependency or shared-module call cannot launder provisional context into a
committed mutation. Existing deferred work is the supported way to request one
only after successful publication, and failed candidates never drain it.

Errors preserve simple author semantics: malformed item identity, invalid
Amount or invalid Vector3 are programming errors; unknown canonical items are
false/zero for read APIs, false before COMMIT for TakeItem, and remain a
programming/configuration error for the separately gated GiveItem;
stale Player use is a controlled stale-Player error; ineligible Teleport state
is a controlled operational error; and adapter failure is a controlled host-
operation error. No Rust enum, exception or result wrapper is public. Inventory
mutation additionally preserves D13's exact false/true/error distinction, and
`pcall` remains the ordinary mechanism for handling indeterminate host failure.

Health mutation; richer inventory/container APIs; `GetItems`,
exposed main/belt/wear objects, container/slot-specific giving, item movement,
splitting, dropping, UID removal, nested/backpack mutation, condition/custom-
data/skin/attachment options, multi-item transactions, rollback/undo and cross-
Player transfer; raw Item, `ItemDefinition`, `ItemContainer`, UID/container IDs
or Rust move flags; `BasePlayer`, `BaseEntity` and `UnityEngine.Vector3`; Player
velocity/rotation/CFrame/Transform; explicit wounded/dead APIs; and Vector3
Unit/Dot/Cross remain deferred. No transaction handle or rollback token enters
Luau.

D18 is additive to existing `Player.Name`, `UserId`, `IsConnected`,
`SendMessage` and `HasPermission` behavior. Architecture adoption changes no
package, scripting API, native ABI, provider protocol, package schema or pinned
Luau identity. Player-1A implements and qualifies `Vector3` and Position, while
Player-1B implements and qualifies live read-only Health and MaxHealth under the
existing unreleased identity; their evidence is in
[PlayerInteractionFoundation1A.md](../qualification/PlayerInteraction.md#playerinteractionfoundation1a) and
[PlayerInteractionFoundation1B.md](../qualification/PlayerInteraction.md#playerinteractionfoundation1b). Bounded
read-only item work is implemented by
[PlayerInteractionFoundation1C.md](../qualification/PlayerInteraction.md#playerinteractionfoundation1c), including
the qualified 128-entry main/belt/wear bound. Player-1D implements Teleport and
passes its exact-host/server gate as recorded
in [PlayerInteractionFoundation1D.md](../qualification/PlayerInteraction.md#playerinteractionfoundation1d), while
its authenticated-client gate remains unqualified. Remaining work is routed to
Player-1E for combined lifecycle, stress, documentation and public
qualification closure.
Inventory-M1 adds only the model, gate, bounded plans/snapshots and failure-
injection algorithms with no Rust mutation or public API. Inventory-M2 has
qualified the exact target-build Create/no-drop transfer, returned-resource,
Take, cleanup and work-bound adapters as recorded in
[InventoryMutationM2Validation.md](../InventoryMutationM2Validation.md).
Player-1F-A implements TakeItem using only that qualified adapter as recorded in
[PlayerInteractionFoundation1FA.md](../qualification/PlayerInteraction.md#playerinteractionfoundation1fa), and
Player-1F-B remains assigned to GiveItem;
Player-1F-C performs combined lifecycle/public closure. No inventory production
work is authorized by architecture adoption alone.

**Evidence separation:** [Phase1-Validation.md](../Phase1-Validation.md) owns the scoped execution-core results. [Phase2-Validation.md](../Phase2-Validation.md) owns module/callback/recovery qualification; Phase 1 does not establish their safety. [Phase3-Validation.md](../Phase3-Validation.md) owns first-facade qualification; [Phase4-Validation.md](../history/inventory/Phase4-Validation.md#phase4-validation) records the blocked item investigation, not an implemented item API.

<!-- END RECORD Invariants.md -->

