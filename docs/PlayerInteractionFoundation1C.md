# Player Interaction Foundation 1C

Status: **PASS within the available qualification envelope**.

Player-1C implements the D18 read-only item slice: the domain-bound `Items`
service with `Exists`, and exact-Player `CountItem` and `HasItem` methods backed
by one reusable bounded physical inventory scanner. It does not implement item
or inventory mutation, PREPARE/COMMIT/VERIFY, raw inventory objects, Teleport,
Player-1D+ or Inventory-M.

## Implemented surface

`game:GetService("Items")` returns the frozen service for the calling domain.
`Items:Exists(ShortName)` returns only a boolean and never exposes or retains an
ItemDefinition or other host object. A service retained past its defining domain
lifetime fails closed through the existing facade ownership check.

All three APIs share one short-name validator. The accepted form is 1 through
128 ASCII bytes, contains no NUL or uppercase ASCII, and has no leading or
trailing whitespace. Input is not trimmed or lowercased. Malformed input is a
programming error; a canonical but unknown name produces `false`, `0` or
`false` from Exists, CountItem or HasItem respectively.

`Player:CountItem(ShortName)` returns the checked exact quantity across direct
entries in the exact Player's main, belt and wear containers.
`Player:HasItem(ShortName, Amount?)` uses the same scanner, defaults Amount to
1, and may stop after reaching the threshold. An explicit Amount must be a
finite exact positive Luau integer no greater than `2^53 - 1`; it is never
rounded. Accumulation also fails rather than overflow, clamp or return an
inexact Luau number.

## Physical observation and bound

The low-level `PhysicalInventoryObservation` accepts only three adapter-provided
top-level containers. Before reading an entry it preflights the aggregate direct
entry count. A counted stack must be current, valid, positive, match the exact
resolved ItemDefinition object and point back to the same accepted container
whose direct list contains it. Invalid, removal-pending, zero/negative,
wrong-definition and non-reciprocal entries do not count. Nested contents,
backpacks, plugin storage, external containers, corpses and world items are not
traversed.

The hard envelope is **128 direct entries total across main, belt and wear**.
The exact target initializes normal capacities as 24, 6 and 8, for 38 normal
slots. Rust permits code to alter container capacity or lists, so CarbonLuau
does not trust those capacities as its safety bound. The selected limit is more
than three times the normal total, matches Rust's own 128-entry multi-container
buffer envelope, and still bounds abnormal or modded state. One over the limit
fails before CountItem or an early-success HasItem can return a partial answer.
The algorithm is nonrecursive and O(direct accepted entries), capped at 128.

This scanner is intentionally independent of the public method names. A later
revised-D13 PREPARE/VERIFY implementation can reuse the same definition of
physical membership and quantity without adding mutation behavior here.

## Exact target source and hook independence

The qualified production target is Rust Dedicated Server Steam build
`25353106` with Carbon `2.0.259.0`. Its exact `Assembly-CSharp.dll` SHA-256 is
`22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.
Exact-build inspection established `PlayerInventory.containerMain`,
`containerBelt` and `containerWear`; direct `ItemContainer.itemList`; reciprocal
`Item.parent`; `Item.info`; and `Item.IsValid()`, which rejects the observed
removal-pending state. The adapter reads those members directly.

`ItemManager.FindItemDefinition(string)` is the bounded definition lookup path.
Because the target's backing dictionary compares case-insensitively, the adapter
additionally verifies `ItemDefinition.shortname` with ordinal equality after
central canonical validation. No definition crosses into Luau.

The scanner never calls `PlayerInventory.GetAmount`, Carbon item-count hooks,
`CanAcceptItem`, `OnInventoryItemsTake` or any mutation path. Its result therefore
does not depend on a plugin-supplied virtual count. The live fixture did not need
to install a synthetic hook override because the implementation has no call edge
to a hook-virtualized count API; the exact source path and runtime adapter both
show direct list observation.

## Lifetime, publication and domains

Every Player read first resolves the existing D11 token and exact connection.
Disconnect makes the proxy stale, same-account reconnect never retargets it, and
no quantity snapshot survives disconnect. Inventory is readable for normal,
sleeping, wounded and dead-but-still-host-valid exact Players while their three
accepted containers remain present. A missing/malformed host inventory fails
with a controlled host-state error rather than manufacturing cached state.

Items, CountItem and HasItem are bounded owner-thread observations and may run
during candidate initialization, module initialization and shared-module calls.
They publish no resource or host effect. A failed candidate therefore has
nothing from Player-1C to roll back. Public modules may share ordinary returned
booleans/numbers and exact Player/service references, but those host-backed
facades remain bound to the defining domain and fail after retirement.

## Qualification

Qualified implementation revision:
`0902b0643ce6435c58c81800df142bc3d0fd7a55`.

Focused model and real-VM coverage includes short-name boundaries and wrong
types; known and unknown definitions; empty, main, belt, wear, split and changed
quantities; multiple stacks; invalid/zero/negative/non-reciprocal entries;
nonaccepted state; exact and over-limit traversal; checked accumulation; omitted,
exact, below, above and invalid HasItem thresholds; early success; provisional
entrypoint/module calls; failed candidates; shared-module and foreign-domain
use; domain retirement/replacement; disconnect and same-account reconnect.

The exact Rust/Carbon build was exercised on the isolated Linux server with real
Item instances in main, belt and wear. Three complete load/unload cycles passed
known/unknown lookup, multiple split stacks, changing quantities, normal,
sleeping, wounded and dead-but-host-valid reads, disconnect staleness,
same-account reconnect, 100 command replacements and native unload. No
authenticated client is required or claimed because this contract is
server-authoritative. The production callback deadline remained unchanged; a
100 ms deadline was used only by the expanded controlled-host fixture and was
restored to 3 ms afterward.

The complete Windows managed/native runtime suite and the complete Linux
managed/native runtime suite passed, including Player-1A/1B, D11, Foundations
A-G, addon/provider, publication/recovery, GUI, parser/package and loader
regressions. All five Linux ASan/UBSan/leak CTests passed. Hosted Windows and
Ubuntu results are linked from the completion report after the final revision.

Representative local release-build observations, not public guarantees, were
recorded for 10,000 empty, normal, early-success and 128-entry scans plus 2,000
real-VM CountItem/HasItem pairs. The final values are retained in test output;
the important compatibility property is the fixed 128-entry O(n) envelope.

## Identities and remaining scope

Player-1C is additive under the still-unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` are unchanged. Private host
operations 25 through 27 extend the existing internal facade protocol without
changing its exported ABI.

GiveItem, TakeItem, mutation gates, PREPARE/COMMIT/VERIFY, inventory snapshots
exposed to Luau, Item, ItemDefinition, ItemContainer, GetInventory/GetItems,
slot/container APIs, nested/backpack inventory, item metadata, numeric item IDs,
Teleport, Player-1D+ and Inventory-M1/M2 remain unimplemented.
