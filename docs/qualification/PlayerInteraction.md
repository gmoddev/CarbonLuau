# PlayerInteraction: qualification ledger

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../Invariants.md).

## Records

- [PlayerInteractionFoundation1A.md](#playerinteractionfoundation1a)
- [PlayerInteractionFoundation1B.md](#playerinteractionfoundation1b)
- [PlayerInteractionFoundation1C.md](#playerinteractionfoundation1c)
- [PlayerInteractionFoundation1D.md](#playerinteractionfoundation1d)
- [PlayerInteractionFoundation1FA.md](#playerinteractionfoundation1fa)
- [PlayerInteractionFoundation1FB.md](#playerinteractionfoundation1fb)
- [PlayerInteractionFoundation1FB-Validation.md](#playerinteractionfoundation1fb-validation)
- [PlayerInteractionFoundation1FC.md](#playerinteractionfoundation1fc)

<!-- BEGIN RECORD PlayerInteractionFoundation1A.md -->

<a id="playerinteractionfoundation1a"></a>

<a id="playerinteractionfoundation1a-player-interaction-foundation-1a"></a>

# Player Interaction Foundation 1A

Status: **PASS within the available qualification envelope**.

Player-1A implements the first D18 slice: immutable `Vector3` and read-only
`Player.Position`. It does not implement health, inventory, Items, Teleport,
GiveItem, TakeItem, Inventory-M work or any Player-1B+ surface.

<a id="playerinteractionfoundation1a-implemented-surface"></a>

## Implemented surface

`Vector3.new(X, Y, Z)` creates a project-owned, zero-host-lifetime value. The
value exposes read-only X, Y, Z and Magnitude fields, exact component equality,
addition, subtraction, unary negation, scalar multiplication in either order
and scalar division. `tostring(Value)` returns `Vector3`, following the existing
CarbonLuau immutable-value convention.

Components reject wrong types, NaN, infinity and values outside the finite
System.Single range. Arithmetic rejects nonfinite scalars, division by zero,
vector multiplication/division and results outside that range. Values are not
clamped. Magnitude is the finite Euclidean magnitude computed in Luau's number
domain; construction at the maximum finite component range remains valid.

The implementation reuses the private zero-sized-userdata representation and
weak-key immutable records already used by CarbonLuau value types. No managed or
Unity object crosses into Luau. The common same-VM equality function permits
exact equality across domain-local metatables. Arithmetic creates a fresh value
without adding host authority. A value retained through an ordinary same-VM
reference remains usable after its defining domain retires; full-VM retirement
still destroys VM-local references under the existing recovery model.

`Player.Position` performs host operation 22 through the existing domain-bound
facade callback. Managed code resolves the exact D11 connection token and reads
one host-neutral three-float snapshot. The production Carbon adapter reads
`BasePlayer.transform.position`, which is the root Transform's world-space XYZ
position. Parent or mount state therefore does not change the coordinate space.
The adapter performs no eye offset, scale/axis conversion, terrain projection,
hook call, world scan, scheduling or publication.

Each successful property read returns a new `Vector3`. Position is not retained
or cached in the proxy. Disconnect, observed host invalidity and same-account
reconnect leave the old proxy stale; no Position snapshot becomes readable from
that proxy. A Vector3 already returned before disconnect remains an ordinary
value. Position reads are allowed during candidate and module initialization and
create no CarbonLuau-owned publication resource.

<a id="playerinteractionfoundation1a-qualification"></a>

## Qualification

Qualified source revision:
`f8f9037c779f8fd091a24fa8e4eba55b7742bf45`.

The focused real-VM fixtures cover zero, signed, fractional and maximum-range
construction; NaN/infinity/overflow/type rejection; X/Y/Z/Magnitude;
immutability; exact equality; every approved arithmetic operator; unsupported
vector operations; division by zero; result overflow; fresh Position values;
changing host position; invalid host coordinates; provisional entry/module
reads; rejected-candidate isolation; disconnect; permanent stale latching;
same-account reconnect; and retained Vector3 usability after disconnect.

The addon regression creates a Vector3 in a provider module, compares and uses
it in two consumer domains, then proves the retained value remains usable after
the provider lifetime retires while required consumers reconstruct against the
replacement. Existing Foundation A-G, addon/provider, GUI, loader, package and
release checks remain in the same complete runtime suite.

An isolated Ubuntu 24.04 BigVPS container with four CPUs and 8 GiB passed all
five release native CTests, the complete Mono real-native runtime suite, loader
tests, architecture/API/release checks, deterministic packaging and all five
ASan/UBSan/leak CTests. The final-head worker fixture measured 2,000 live
Position reads in 12.04 ms. This is a worker observation, not a public
guarantee.

[Hosted validation](https://github.com/gmoddev/CarbonLuau/actions/runs/35567279475)
passed Windows and Ubuntu native/runtime/loader/package jobs plus the sanitizer
job at the qualified revision. The corresponding hosted fixtures measured
11.12 ms on Windows and 18.24 ms on Ubuntu for 2,000 Position reads. The first
hosted run exposed a test-only 5 ms GUI-2E rich-reload budget after the larger
bootstrap; the evidence revision raises only that recovery fixture to its
existing 20 ms budget. Production deadline defaults and behavior are unchanged.
[Documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/35566639504)
also passed.

DockerPC was unavailable: the configured reverse-tunnel endpoint refused the
connection. No Windows native/local or Windows live Carbon result is claimed.
Hosted Windows CI is separate evidence. Read-only BigVPS inspection found no
RustDedicated/Carbon server, so live Carbon Position behavior was unavailable;
mounted/parented behavior is supported by the direct world-position adapter and
model/source evidence, not mislabeled as a live result. No authenticated client
is required for this server-authoritative property.

<a id="playerinteractionfoundation1a-identities-and-remaining-scope"></a>

## Identities and remaining scope

Player-1A remains additive under the unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and the pinned Luau revision are unchanged. Host operation 22 extends
the existing private callback protocol without changing its exported ABI.

Player.Health and Player.MaxHealth are implemented separately by Player-1B.
Items, CountItem, HasItem, Teleport, GiveItem, TakeItem, Inventory-M1/M2,
Vector3 Unit/Dot/Cross, Velocity, Rotation, CFrame and entity/world APIs remain
unimplemented.

<!-- END RECORD PlayerInteractionFoundation1A.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1B.md -->

<a id="playerinteractionfoundation1b"></a>

<a id="playerinteractionfoundation1b-player-interaction-foundation-1b"></a>

# Player Interaction Foundation 1B

Status: **PASS within the available qualification envelope**.

Player-1B implements the D18 read-only health slice:
`Player.Health` and `Player.MaxHealth`. It does not implement health mutation,
inventory observation or mutation, Items, Teleport, entity access or any
Player-1C+ surface.

<a id="playerinteractionfoundation1b-implemented-surface"></a>

## Implemented surface

Each property access resolves the proxy's exact D11 connection lifetime and
performs one direct owner-thread host read. `Health` invokes
`BasePlayer.Health()` and `MaxHealth` invokes `BasePlayer.MaxHealth()` through
the existing domain-bound facade. Results are transported as invariant
round-trip finite System.Single values and become Luau numbers. No host object
crosses the boundary.

The two values are independent. CarbonLuau does not clamp Health to MaxHealth,
does not replace unusual finite values and does not assume MaxHealth is 100.
NaN or infinity from the host raises a controlled error. No result is cached in
the Player proxy, so later reads observe later host state. Both properties are
read-only through the existing frozen Player proxy.

Reads are allowed during provisional entrypoint and module execution because
they are bounded observations that create no CarbonLuau resource and perform no
host mutation. A Player proxy shared through an addon module remains bound to
the defining domain lifetime. Retirement makes subsequent host-backed reads
fail closed; a replacement domain receives a new facade and does not retarget
the old value.

Disconnect, observed host invalidity and same-account reconnect leave the old
proxy stale. Unlike Name and UserId, Health and MaxHealth retain no disconnect
snapshot. Normal, sleeping, wounded and dead-but-still-host-valid exact Players
remain readable because the adapter adds no gameplay-state eligibility gate.

<a id="playerinteractionfoundation1b-exact-target-source-evidence"></a>

## Exact target source evidence

The production target is Rust Dedicated Server Steam build `25353106` with
Carbon `2.0.259.0`. The exact target `Assembly-CSharp.dll` was downloaded and
decompiled for this task. `BaseCombatEntity.Health()` returns `_health`
directly. Its base `MaxHealth()` honors a positive `maxHealthOverride`; the
`BasePlayer` override otherwise computes the current `_maxHealth` multiplied by
the current `Max_Health` modifier. Neither getter invokes a plugin hook or gates
on sleeping, wounded or dead state. This establishes the dynamic maximum-health
adapter and rules out a hard-coded 100.

<a id="playerinteractionfoundation1b-qualification"></a>

## Qualification

Qualified implementation revision:
`ae7f6856fae92e03c2ddf0a686cf7e2a3670ce81`.

The focused real-VM fixtures cover zero, positive, negative and fractional
finite values; changing reads; Health above MaxHealth; non-100 and changing
MaxHealth; no clamping; NaN/infinity rejection; immutability; provisional
entrypoint and module reads; rejected-candidate isolation; normal, sleeping,
wounded and dead-but-host-valid model states; disconnect; permanent stale
latching; same-account reconnect; and repeated read overhead.

The addon regression exports an exact Player proxy from a public module, reads
both properties from optional and required consumer domains, rejects reads
after the defining provider lifetime retires and verifies that a reconstructed
required consumer reads through the replacement domain rather than retargeting
the old proxy.

An isolated Ubuntu 24.04 BigVPS worker with four CPUs and 8 GiB passed all five
release native CTests, the complete Mono real-native runtime suite, loader
tests, architecture/API/release checks, deterministic packaging and all five
ASan/UBSan/leak CTests. A repeated final runtime pass measured 2,000 live
Health/MaxHealth read pairs in 13.82 ms. This is a worker observation, not a
public performance guarantee.

The exact Rust build `25353106` and Carbon `2.0.259.0` were then exercised in an
isolated live server with a controlled real `BasePlayer` and
`Network.Connection`. Three complete plugin cycles passed direct fractional
Health, dynamic and fractional MaxHealth, Health above MaxHealth, sleeping,
wounded, explicit dead-but-host-valid LifeState, disconnect staleness,
same-account reconnect, 100 replacements and native unload. The runner exited
successfully and makes no authenticated-client claim. Its 100 ms callback
deadline is test-only; production configuration and defaults are unchanged.

DockerPC was unavailable, so no Windows native/local or Windows live result is
claimed. Hosted Windows CI is separate evidence and is not a substitute for
that unavailable local environment. The
[hosted validation run](https://github.com/gmoddev/CarbonLuau/actions/runs/35570576645)
passed Windows and Ubuntu native/runtime/loader/package jobs plus the sanitizer
job at the qualified revision. The
[documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/35570576640)
also passed.

<a id="playerinteractionfoundation1b-identities-and-remaining-scope"></a>

## Identities and remaining scope

Player-1B remains additive under the unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and the pinned Luau revision are unchanged. Private host operations
23 and 24 extend the existing callback protocol without changing its exported
ABI.

Health mutation, Items, CountItem, HasItem, Teleport, GiveItem, TakeItem,
Inventory-M1/M2, explicit wounded/dead APIs and entity/world APIs remain
unimplemented.

<!-- END RECORD PlayerInteractionFoundation1B.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1C.md -->

<a id="playerinteractionfoundation1c"></a>

<a id="playerinteractionfoundation1c-player-interaction-foundation-1c"></a>

# Player Interaction Foundation 1C

Status: **PASS within the available qualification envelope**.

Player-1C implements the D18 read-only item slice: the domain-bound `Items`
service with `Exists`, and exact-Player `CountItem` and `HasItem` methods backed
by one reusable bounded physical inventory scanner. It does not implement item
or inventory mutation, PREPARE/COMMIT/VERIFY, raw inventory objects, Teleport,
Player-1D+ or Inventory-M.

<a id="playerinteractionfoundation1c-implemented-surface"></a>

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

<a id="playerinteractionfoundation1c-physical-observation-and-bound"></a>

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

<a id="playerinteractionfoundation1c-exact-target-source-and-hook-independence"></a>

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

<a id="playerinteractionfoundation1c-lifetime-publication-and-domains"></a>

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

<a id="playerinteractionfoundation1c-qualification"></a>

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

<a id="playerinteractionfoundation1c-identities-and-remaining-scope"></a>

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

<!-- END RECORD PlayerInteractionFoundation1C.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1D.md -->

<a id="playerinteractionfoundation1d"></a>

<a id="playerinteractionfoundation1d-player-interaction-foundation-1d"></a>

# Player Interaction Foundation 1D

Player-1D implements the D18 spatial mutation:

```lua
Player:Teleport(Vector3.new(100, 20, -50))
```

It returns no values. The implementation revision requiring any later
supplemental authenticated-client qualification is
`74b9b48e9375177bc17e76a4d939956d1076645b`.

<a id="playerinteractionfoundation1d-contract-and-eligibility"></a>

## Contract and eligibility

Position must be a CarbonLuau `Vector3`. Components have already passed the
finite `System.Single` envelope and are converted directly to Unity world X/Y/Z
without axis conversion, clamping, terrain projection, collision adjustment or
safe-position search.

Every call revalidates the exact D11 Player object and Network.Connection
lifetime immediately before mutation. The owning root or addon facade must be
active and committed. The Player must be alive, non-spectating,
non-wounded and non-incapacitated. Sleeping is accepted and remains set.
Disconnected Players and old same-account proxies fail and can never retarget a
new connection.

Candidate entrypoints, first-load modules, foreign shared modules and dependency
calls remain provisional under D10 and cannot invoke Teleport. `task.defer`
work runs only after successful publication; a failed candidate publishes no
movement. Retired addon facades fail closed, while a reconstructed consumer can
use the fresh dependency lifetime.

<a id="playerinteractionfoundation1d-exact-target-adapter"></a>

## Exact target adapter

The supported target is Rust Dedicated Server Steam build `25353106`, Carbon
`2.0.259.0`, and `Assembly-CSharp.dll` SHA-256
`22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.
The exact assembly was inspected before implementation. Current
`BasePlayer.Teleport(Vector3)` performs only `MovePosition` plus the
`ForcePositionTo` RPC and is therefore not the complete CarbonLuau adapter.

The adapter uses this target-build sequence:

1. pause fly, speed and tick-distance detection for five seconds and apply the
   four-second host stall protection window;
2. if mounted, call the supported lightweight `DismountPlayer(Player, true)`
   path so mount references and callbacks are cleared without host dismount-
   point relocation or its invalid-position kill behavior;
3. detach remaining host parenting with world position preserved and update the
   unparent time;
4. enable server-fall reconciliation, call `MovePosition` with the exact target,
   and clear inherited estimated velocity;
5. send the exact Player `ForcePositionTo` RPC, update the network group, and
   send an immediate network update;
6. clear server-fall state for an awake Player, while leaving it active for a
   sleeping Player and preserving the sleeping flag.

The adapter does not start the portal-specific loading/snapshot handshake. That
path requires client completion state and is not appropriate for a synchronous
API with no client acknowledgement. Held-item state is not modified. Normal
destination trigger and physics behavior belongs to Rust; no traversed path is
synthesized.

<a id="playerinteractionfoundation1d-mutation-boundary-and-verification"></a>

## Mutation boundary and verification

The first anti-cheat pause is the earliest irreversible host step. Inspection,
Vector3 validation, D11 validation and eligibility rejection occur before that
boundary. Any exception after mutation begins becomes a bounded controlled
error stating that Player state may have changed. CarbonLuau does not move the
Player back, claim rollback, replay a Teleport during recovery or expose a raw
host exception.

After the sequence, one bounded verification pass confirms the original exact
connection is still current, the root Transform contains the requested
single-precision coordinates, mount and parent references are clear, and the
sleeping flag matches its pre-mutation value. A mismatch is a controlled
post-mutation error. `UpdateNetworkGroup` is part of the sequence; no polling or
client acknowledgement is performed.

Host callbacks may run inside Rust operations, but the existing global native
entry guard rejects recursive Luau VM entry. CarbonLuau-owned queued work is
admitted only after the active entry returns. Teleport remains one synchronous
facade operation under the original admitted-operation deadline and is never
split across Luau turns.

<a id="playerinteractionfoundation1d-qualification"></a>

## Qualification

Focused model and real-VM tests cover valid, zero, signed, fractional and
maximum finite destinations; wrong types and invalid Vector3 construction; no
return values; alive, sleeping, dead, wounded, incapacitated and spectating
states; exact mount/parent terminal state; D11 disconnect and same-account
reconnect; root candidate/module rejection; successful and failed deferred
publication; foreign public-module laundering; addon retirement and required
consumer reconstruction; pre-mutation failure; post-mutation failure; exactly
one verification; mismatch; curated diagnostics; recursive-entry rejection;
replacement, fatal recovery and no replay through the existing lifecycle
suites.

The complete local Windows managed/native runtime suite passed, including
Player-1A through Player-1C, Foundations A through G, addon/provider,
publication, recovery, GUI, parser/package and loader coverage. Affected native
ScriptCore, RuntimeCore and load/unload CTests passed. The existing Foundation G
Windows compiler-worker 256 MiB containment check did not qualify on this
workstation and remains part of the separate Windows live/local deferral; Linux
passed it. The complete Linux managed/native suite passed, and all five Linux
ASan/UBSan/leak CTests passed after the private native facade-operation change.
DockerPC Windows live/local qualification was unavailable and is
**DEFERRED / UNQUALIFIED**. Hosted Windows CI is recorded separately and does
not substitute for DockerPC.

Three complete isolated live Carbon cycles passed on the exact target build.
Each used a real server-created BasePlayer and Network.Connection and covered
exact/repeated movement, an in-map network-group-scale move, sleeping state,
wounded/dead rejection, real parent detachment, real chair mount cleanup,
disconnect/reconnect staleness, 100 registration replacements, timeout recovery,
production NextFrame scheduling and native/plugin teardown. These fixtures do
not represent an authenticated game client.

<a id="playerinteractionfoundation1d-authenticated-client-status"></a>

## Authenticated-client status

**IMPLEMENTED / AUTHENTICATED-CLIENT UNQUALIFIED.** No suitable authenticated
current Rust client was available. Revision
`74b9b48e9375177bc17e76a4d939956d1076645b` must later be tested for short and
long moves, network-group transition, immediate rubber-band, moving state,
ordinary collision geometry, fall/fall-damage behavior, sleeping, mount/parent
convergence, repeated moves, disconnect/reconnect, root/addon replacement and
fatal recovery. Server evidence is not described as client convergence.

<a id="playerinteractionfoundation1d-identity-and-remaining-scope"></a>

## Identity and remaining scope

Player-1D is additive under the still-unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` are unchanged. Private host
operation 28 extends only the build-embedded facade protocol.

Player-1E, Inventory-M1/M2, GiveItem, TakeItem, health mutation, Velocity,
Rotation, CFrame, entity/world APIs, spawn points, teleport history and safe or
animated teleport remain unimplemented.

<!-- END RECORD PlayerInteractionFoundation1D.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1FA.md -->

<a id="playerinteractionfoundation1fa"></a>

<a id="playerinteractionfoundation1fa-player-interaction-foundation-1f-a"></a>

# Player Interaction Foundation 1F-A

<a id="playerinteractionfoundation1fa-verdict"></a>

## Verdict

`Player:TakeItem(ShortName, Amount) -> boolean` is implemented using the
revised D13 PREPARE, COMMIT and VERIFY model. It is limited to the exact Rust
build `25353106` and Carbon `2.0.259` adapter qualified by Inventory-M2.
The implementation revision is
`82e940c55287d02830d8f3ed3a4f5142389db164`.

This record does not qualify or implement `Player:GiveItem`, generic inventory
objects, rollback, multi-item transactions, Player-1F-B or Player-1F-C.

<a id="playerinteractionfoundation1fa-public-contract"></a>

## Public contract

`ShortName` uses the D18 canonical 1 through 128 byte lowercase ASCII item
identity. `Amount` is required and must be an exact integer from 1 through
`Int32.MaxValue`.

- `true` means the host returned the requested amount and the fresh physical
  inventory scan proved that exact requested amount disappeared.
- `false` means PREPARE proved the operation could not proceed and COMMIT did
  not begin. This includes insufficient quantity and a canonical unknown item.
- an error after COMMIT means inventory may have changed and CarbonLuau could
  not establish the final result. No rollback is promised.

Authors that need to handle the indeterminate case separately can use `pcall`:

```lua
local CallOk, Removed = pcall(function()
    return Player:TakeItem("scrap", 100)
end)

if not CallOk then
    -- The mutation may have started. Re-read authoritative state before acting.
elseif Removed then
    print("Removed 100 scrap")
else
    print("Player did not have enough scrap")
end
```

<a id="playerinteractionfoundation1fa-prepare-commit-and-verify"></a>

## PREPARE, COMMIT and VERIFY

PREPARE revalidates the exact D11 connection, resolves the canonical item
definition and uses `PhysicalInventoryObservation` to count only valid physical
stacks in main, belt and wear. The scan is nonrecursive and enforces the 128
direct-entry/capacity envelope. Unknown items and insufficient quantities return
`false` without calling a mutation helper.

Immediately before COMMIT the exact connection is revalidated. COMMIT is the
qualified call:

```csharp
Player.inventory.Take(null, ItemDefinition.itemid, Amount)
```

After it returns, VERIFY resolves the exact connection again and performs one
fresh bounded physical scan. Success requires both:

```text
hostReturnedAmount == requestedAmount
Q0 - Q1 == requestedAmount
```

Checked arithmetic is used. A host exception, connection loss, host amount
mismatch or physical delta mismatch after COMMIT is a controlled indeterminate
error. CarbonLuau does not poll, replay or attempt rollback.

<a id="playerinteractionfoundation1fa-mutation-gate-and-publication"></a>

## Mutation gate and publication

The root domain and every addon share one owner-thread mutation gate keyed by
the exact D11 connection token. The gate has no waiting queue. Recursive work
for the same exact Player is rejected, different Players remain independent,
and every exit path releases the gate in `finally`.

TakeItem is committed-only. Candidate initialization, provisional modules and
cross-addon shared-module calls cannot invoke it. A dependency cannot launder
provisional admission into committed execution. `task.defer` published by a
successful candidate is the supported startup pattern. Failed candidates
publish no deferred mutation.

<a id="playerinteractionfoundation1fa-interference-and-diagnostics"></a>

## Interference and diagnostics

The gate serializes only CarbonLuau-originated mutations. Model fixtures cover
altered host return amounts, extra matching-item removal, matching-item
addition, unrelated-item mutation and callback exceptions. Only the exact host
and physical predicates return `true`; every ambiguous post-COMMIT result
errors. Unrelated-item mutation does not invalidate an otherwise exact delta
for the requested definition.

Saturating internal counters cover attempts, PREPARE rejections, verified
success, stale rejection, gate-busy rejection, host exceptions, host amount
mismatches, physical delta mismatches and indeterminate outcomes. The native
domain rejection counter records provisional admission rejection. No host Item
or ItemContainer enters Luau and no mutation history queue exists.

<a id="playerinteractionfoundation1fa-qualification"></a>

## Qualification

The focused model covers empty, unknown, split, partial and full stacks; all
three accepted containers; the exact 128-entry boundary; over-bound rejection;
stale transitions; same-account replacement; recursive and cross-Player gate
behavior; and all interference outcomes. It also performs 1,000 successful
mutations, 1,000 PREPARE rejections and 1,000 verification-mismatch injections.

The managed native suite covers root and addon calls, shared-module provisional
laundering rejection, deferred publication, failed-candidate rollback,
dependency replacement, stale proxies, VM recovery, provider/addon/GUI/package
regressions and shipped examples. Exact-build live Carbon qualification covers
partial and full stacks, multiple stacks, main/belt/wear traversal, insufficient
quantity, canonical unknown identity, repeated calls, reconnect lifetime and
native teardown.

The final local Windows managed/native suite passed, including the 3,000-case
focused stress loop, all prior Player slices, addon/provider, GUI, package,
scheduler and lifecycle regressions. Affected Windows native ScriptCore,
RuntimeAllocationFaults, RuntimeCore and NativeLoadUnload CTests passed. The
pre-existing local Foundation G compiler-worker 256 MiB containment check did
not qualify on this workstation; Linux passed it.

The complete Linux native and managed suite passed. All five release CTests
passed, including compiler containment, and the ASan/UBSan/leak build passed all
five CTests. Linux timing for the 3,000 focused operations was 3.20 ms; this is
an observation, not a compatibility guarantee.

Three isolated exact-build live Carbon cycles passed. Every cycle logged the
TakeItem PREPARE/COMMIT/VERIFY marker and then unloaded the plugin with the
native library unmapped. The final isolated Rust process was cgroup-killed
during server shutdown after all three plugin teardown assertions had passed;
this is not represented as graceful server-exit evidence.

DockerPC Windows live/native qualification is unavailable for this task and is
not represented as completed by hosted Windows CI. Hosted CI and documentation
deployment are recorded at the final tested revision in the completion report.

<a id="playerinteractionfoundation1fa-identity-and-remaining-scope"></a>

## Identity and remaining scope

This additive method keeps package `0.4.0`, scripting API
`0.4.0-experimental`, native ABI `1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema
`1` and Luau revision `c6b830185af962c82003f86784e2fe036357c830` unchanged.
`GiveItem`, Player-1F-B and Player-1F-C remain deferred.

<a id="playerinteractionfoundation1fa-continuation-audit--2026-09-21"></a>

## Continuation audit — 2026-09-21

Resumed from evidence commit `7111f494b5c1c27b299dcfdf0ff698e9f1967b8c`;
the runtime implementation was already present in `82e940c55287d02830d8f3ed3a4f5142389db164`.
Source review reconfirmed the qualified host call, exact-connection resolution
before PREPARE/COMMIT and at VERIFY, the shared FacadeWorld-owned gate,
publication-context rejection in the native bridge, and both success predicates.
No production source, native/vendor code or version identity changed in this
continuation. The provider version above corrects a documentation typo from
`1.0` to the existing `CarbonLuau.Addons` / `1.2` in release.json and D12.

Independently retrieved final-baseline CI status and logs:

- [Windows, Linux and sanitizer validation](https://github.com/gmoddev/CarbonLuau/actions/runs/35585100724)
  passed at `7111f494b5c1c27b299dcfdf0ff698e9f1967b8c`. Each of the Windows,
  Linux and ASan/UBSan/leak native jobs passed all five CTests. Both managed
  runtime jobs passed the Player, addon/provider, GUI, package, scheduler and
  lifecycle suites. The focused 3,000-operation model reported 11.11 ms on
  hosted Windows and 4.11 ms on hosted Linux; these are observations only.
- [Documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/35585100787)
  passed at that same revision. Hosted tests are not live Rust/Carbon evidence.

Extended the existing focused model with maximum-amount successful removal,
pre-COMMIT stale/rejection paths, exceptions after actual mutation, VERIFY host
exceptions, over-bound VERIFY state, post-COMMIT stale state, exact scan counts
and same-token gate reuse after each outcome. The new boundary cases and the
original 3,000-operation stress passed in a small local managed-only check:
PowerShell 7 Add-Type compiled the production FacadePolicy, PlayerDirectory,
PlayerTeleportOperation, InventoryObservation and InventoryMutation sources
with PlayerInteractionFoundation1FATests, then invoked RunModel. No native
build or server was started locally. This check does not substitute for the
.NET Framework/native integration matrix. API/architecture checks and
`git diff --check` also passed. The public Player reference now includes the
separate pcall example without implying rollback or safe automatic retry.

Fresh worker/live runs were unavailable: `DockerPC` did not resolve from this
Windows profile, and the configured `dockerbox` alias timed out on SSH port 22.
No host configuration was changed or unrelated worker was substituted. The
three exact-build live cycles above remain previously recorded evidence, not
newly reproduced live results. No new real-client or graceful server-exit claim
is made. GiveItem and Player-1F-B/C remain outside this task.

<!-- END RECORD PlayerInteractionFoundation1FA.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1FB.md -->

<a id="playerinteractionfoundation1fb"></a>

<a id="playerinteractionfoundation1fb-player-interaction-foundation-1f-b--giveitem-implementation-and-evidence"></a>

# Player Interaction Foundation 1F-B — GiveItem implementation and evidence

<a id="playerinteractionfoundation1fb-current-implementation"></a>

## Current implementation

The separately authorized implementation starting at `03b6d68346e01747e3b9250f1f4c4668ae3d8383`
adds `Player:GiveItem(ShortName, Amount, Behavior?) -> boolean`, with only typed
`GiveItemBehavior.InventoryOnly` (the default). The current implementation,
qualification scope, limitations and reproduction record are in
[Player-1F-B validation](#playerinteractionfoundation1fb-validation).
Author semantics are in [Player](../api/Types/PlayerInventory.md#giveitem) and
[GiveItemBehavior](../api/Types/GiveItemBehavior.md). I12/D13/D18 remain canonical;
no change to their operational semantics is made.

Implementation owners:

- `Facade/InventoryGrant.cs`: complete bounded plan, returned-resource accounting,
  cleanup and combined VERIFY. It consumes the Player-1C physical records, not a
  separate inventory model. Q0 uses the frozen planning snapshot; Q1 uses one
  fresh bounded scan. Arrays/list capacity never exceed 128 chunks.
- `CarbonLuau.Inventory.Carbon.cs`: exact-build host adaptation, conservative
  callback-free placement guards and explicit-slot transfer. Default unmodified
  merge candidates only; actual returned Item compatibility is revalidated before
  transfer. Wear/belt conflict relocation is excluded with readonly compatibility
  guards; one clothing/restriction-bearing chunk per eligible container avoids
  conflicts between planned chunks. Slot-mask containers and backpack/parachute
  wear are not selected. No generic transfer, automatic slot or Drop fallback.
- `FacadeWorld`: GiveItem shares TakeItem's existing exact-connection gate.
- Build-embedded bootstrap and native facade ingress: typed enum, scalar/string
  transport, and provisional admission rejection even through shared modules.

Each returned Item is tracked before observation/transfer. Qualified accepted
items remain Rust-owned even after an insertion callback throws. Consumed merge
sources are accounted by transfer success plus UID/state. Only a still-valid,
unattached, non-world temporary is eligible for `Remove(0)` cleanup; no global
removal drain is used in production. Missing returns, changed identities, world
state, unqualified resource states or failed cleanup produce an indeterminate
error, never success or post-COMMIT false. Cleanup failures/unaccounted resources
have saturating counters; transient borrowed references are released on exit.
Zero retained references does not falsely claim a failed cleanup succeeded.

No item-specific Player identity, capability policy, lock, queue, polling,
rollback, retries, raw item facade or ABI change was introduced. Amount remains
required and bounded by D18; planning may reject a valid large amount before
creation when the complete safe plan cannot fit. Ordinary host rejection after
creation remains an error. Main/belt/wear capacities and entry counts are checked
before planning; bounded readonly compatibility checks may revisit those entries
for candidate slots, never traverse nested/external inventories.

<a id="playerinteractionfoundation1fb-historical-investigation-and-policy-adoption"></a>

## Historical investigation and policy adoption

The remainder records the earlier stopped investigation and policy-only adoption.
Statements below such as “unimplemented”, “pending” and “not run” describe those
historical tasks, not the later implementation above. Their callback/drop finding
and the supersession of the original blanket M2 G1 claim remain valid.

Date: 2026-09-21. Starting implementation baseline and investigation source:
`3f6a3196b28e2243dc8802d3b8ff2374196f3c89`, branch `main`.

**Policy verdict: CANONICAL BASELINE READY. GiveItem G1: PENDING REQUALIFICATION,
not PASS.** The investigation initially stopped implementation under the prior
whole-process no-drop interpretation. The user subsequently adopted the general
[I12 interference boundary](../Invariants.md#i12--trusted-in-process-host-interference)
and amended D13/D18. That resolves the policy question, not the adapter gate.
No runtime implementation is authorized by this architecture/evidence task.

<a id="playerinteractionfoundation1fb-scope-and-provenance"></a>

## Scope and provenance

The original implementation request was `Player:GiveItem(ShortName, Amount)`.
The now-canonical intended shape is
`Player:GiveItem(ShortName, Amount, Behavior?) -> boolean`, defaulting to
`GiveItemBehavior.InventoryOnly`. Neither the method nor enum is exposed yet.
[Inventory-M2](../InventoryMutationM2Validation.md), D11/D13/D18, the Player-1C
scanner and [Player-1F-A](#playerinteractionfoundation1fa) remain authoritative.
No public binding, production planner, mutation adapter or API example was added.

After the DockerPC/dockerbox outage, the user explicitly authorized the big VPS,
with the controlling PC as fallback. The configured `BigKVM` SSH alias worked.
A task-owned directory `/srv/codex/CarbonLuauPlayer1FB20260921` holds downloaded
artifacts/tools. Disposable containers used one CPU and at most 2 GiB; no server,
listener or unrelated service was started, restarted or modified.

SteamRE DepotDownloader 3.4.0 retrieved only `Assembly-CSharp.dll` anonymously
from the exact M2 manifests. Both SHA-256 values and MVIDs match M2:

| Target | Depot / manifest | SHA-256 |
|---|---|---|
| Windows | `258551` / `7816298056519226227` | `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a` |
| Linux | `258552` / `3352454092778561960` | `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b` |

ILSpy 8.2.0.7535 and Mono.Cecil were used for read-only inspection. Decompiled
host files remain in ignored/task-external storage, not tracked source. These
are vanilla assemblies; no new Carbon-patched live execution is claimed.

<a id="playerinteractionfoundation1fb-exact-path-finding"></a>

## Exact-path finding

Manual control-flow inspection, supplemented by
[Test-GiveItemNoDropEvidence.ps1](../../tools/Test-GiveItemNoDropEvidence.ps1), found:

1. Even with an explicit slot, stacking as selected, `ignoreStackLimit=false`
   and `allowSwap=false`, `Item.MoveToContainer` invokes `Item.CanMoveTo`.
2. `CanMoveTo` calls `ItemContainer.CanAcceptItem`, which invokes the public
   `canAcceptItem` delegate. This occurs **inside** the transfer, after any
   caller-side revalidation.
3. On the empty-slot path, the method subsequently reads `maxStackSize`. A
   positive limit below the current source amount enters `SplitItem`.
4. If the generated split cannot be placed and the original source had no
   container, that branch calls `Drop`. The outer transfer can return true.

The following is a **source-derived counterexample**, not a live observation:

- PREPARE selects an empty main slot for a scrap chunk of two, with a limit
  allowing two. Creation returns an unattached source of two; an immediate
  caller-side guard still sees the valid plan.
- The first acceptance delegate invocation sets the target's `maxStackSize`
  to one and accepts. Ordinary remaining acceptance conditions are satisfied.
- The host now sees a chunk larger than the changed limit and splits one.
- The delegate rejects the nested transfer of the split. Because the original
  source was unattached, the outer branch can drop that split.

The delegate need not call Drop itself. The forbidden fallback is in the exact
adapter path CarbonLuau was instructed to use. A last-moment pre-call check,
same-Player Luau gate, or post-call indeterminate result cannot prevent an
internal callback from changing the subsequently tested limit. This is distinct
from attempting to sandbox a trusted plugin that deliberately drops items on
its own. This finding failed the previous whole-process G1 interpretation.
Under the now-approved I12 boundary it is an external-interference example,
not ordinary supported-host behavior and not a qualified success result.
The original blanket M2 G1 proof remains superseded, rather than restored.

The generated split also is not the original returned Item. Checking only that
original reference for a world entity would not establish absence of a drop.
The physical VERIFY predicate can detect insufficient delivery, but it does not
retroactively make this a qualified inventory-only path.

An additional scope requiring review is `RemoveConflictingSlots`: the explicit
empty-slot path calls it when slot masks are present. It can detach conflicting
items and invoke generic GiveItem or automatic placement. This is a separate
reason to qualify accepted container restrictions rather than infer safety
from `allowSwap=false`. No claim is made that vanilla main/belt/wear always
exercises this branch. If baseline host behavior reaches a prohibited path,
that remains CarbonLuau responsibility; I12 cannot excuse it without evidence
of external premise-invalidating mutation.

<a id="playerinteractionfoundation1fb-what-the-checks-establish-and-what-they-do-not"></a>

## What the checks establish, and what they do not

Historical investigation output on BigKVM, using the exact Windows and Linux
assemblies (the BLOCKED label predates I12 adoption):

```text
[CarbonLuau:InventoryM2] Static adapter evidence passed: 92 checks.
[CarbonLuau:Player1FB] Static exposure confirmed: 51 checks; GiveItem G1 remains BLOCKED pending resolution.
```

The supplement checks pinned hashes, identical selected Windows/Linux IL,
callback/limit/split/drop ordering, the conflict-removal path, and public mutable
acceptance/limit fields. It is a regression record of the relevant structural
facts; linear IL assertions alone are not a complete reachability proof. The
counterexample above additionally relies on manual branch inspection.

The existing M2 live successes remain valid for the cases they exercised:
scrap insertion/merge with stable limits, acceptance rejection, exceptional
insertion, cleanup and Take. Their unchanged checker passing does not extend
those cases to limit-changing callbacks. No production GiveItem implementation
or new real-host interference fixture was run. No new authenticated-client
result is needed or claimed. Carbon 2.0.259 live behavior remains prior M2
evidence, not a fresh result from this investigation.

<a id="playerinteractionfoundation1fb-adopted-scope-and-required-1f-b-requalification"></a>

## Adopted scope and required 1F-B requalification

I12 owns the general external-interference definition and exclusions. D13 owns
the revised InventoryOnly G1 wording and unchanged success/failure predicates;
D18 owns the intended optional Behavior enum. This record supplies the evidence
and test matrix, not a parallel policy. Policy adoption is not implementation
authorization and is not a G1 PASS. The existing TakeItem gate and common
physical scanner remain the intended owners; no second mutation scheduler or
inventory model is introduced.

<a id="playerinteractionfoundation1fb-required-1f-b-requalification"></a>

### Required 1F-B requalification

On the exact supported Rust/Carbon baseline, require all of the following:

| Area | Required evidence |
|---|---|
| Supported callbacks | Vanilla/default acceptance, normal acceptance, normal rejection, and inspecting callbacks that do not mutate relevant state. Rejection is not external interference. |
| Complete bounded planning | Full inventory and insufficient total capacity reject before creation; partial compatible stacks, exact stack completion, empty slots and multi-chunk grants fit the whole amount within the shared 128-entry/chunk envelope. No callback-capable speculative PREPARE. |
| Qualified invocation | Exact target container/slot; no automatic slot, swap, ignoreStackLimit, generic GiveItem or CarbonLuau-selected Drop; no oversized planned chunk under the qualified baseline. Inspect conflict-slot behavior and baseline item initialization as well as acceptance. |
| Returned responsibility | Account immediately for each returned resource; prove accepted, consumed-merge and qualified temporary states; supported cleanup, exceptional insertion and cleanup failure; no abandoned reference or global removal-queue drain. |
| VERIFY and outcomes | One fresh bounded physical scan plus every D13 success predicate, not quantity or host return alone; false only before COMMIT; uncertain COMMIT errors, no rollback or replay. Full PREPARE rejection creates no Item; normal host rejection after creation is indeterminate. |
| Existing protections | D11 exact connection, shared Give/Take gate, root/addon and recursive admission, provisional and shared-module restrictions, deferred publication, replacement/unload/recovery and unchanged TakeItem regressions. |

Requalify the exact host adapter with controlled host fixtures before resuming
production implementation; subsequently qualify the actual production planner,
facade, accounting, lifecycle and stress behavior before claiming 1F-B PASS.
Static checks, old M2 markers or this scope amendment alone cannot satisfy that
gate. Preserve qualified G2-G5 evidence where unchanged and rerun affected
cases. No authenticated client is required for these server-authoritative tests.

Retain the hostile-mutation counterexample and any existing fixtures as
**boundary tests**, separate from supported-host success evidence. Qualify
controlled callbacks changing maxStackSize or Item.amount, and relevant
stack/slot/parent/inventory/world mutations. Record the actual mutation, phase,
host outcome and resource/physical observations; no attribution from an
unexplained failure alone. The existing counterexample is static, not a live
fixture result; live boundary tests remain pending.

If a resulting world-delivery or unqualified/uncertain resource state is
detected, report the existing controlled indeterminate failure and bounded
diagnostics; do not hide the effect, claim rollback, retry automatically, or
count it as InventoryOnly success. Known-invalid premises must not be passed
knowingly into another transfer. Interference with unrelated state permits
success only when every existing operation-specific predicate still proves it.
No whole-process forensic isolation or exhaustive external-mutation detection
is promised.

<a id="playerinteractionfoundation1fb-future-behavior-scope"></a>

### Future behavior scope

InventoryOnly is the default and only accepted intended enum member. Full
PREPARE inability to fit returns false before COMMIT; post-COMMIT uncertainty
errors. `DropRemainder` is only a future design name, not reserved or exposed.
D18 requires separate design/qualification before any such member is accepted.
There is no boolean DropIfFull, no generic GiveItem shortcut and no identity bump.

<a id="playerinteractionfoundation1fb-completion-boundary"></a>

## Completion boundary

- Planner, returned-resource accounting, cleanup, physical VERIFY and shared
  Give/Take gate implementation: not started; no new runtime results claimed.
- GiveItem failure injection, lifecycle, stress, live Carbon, Windows/Linux
  runtime and sanitizer gates: not run because implementation stopped at G1.
- Existing TakeItem/Inventory, addon, GUI and broader runtime evidence is
  preserved. No production source changed; no new regression PASS is implied.
- Documentation/API and architecture consistency checks passed on the adoption
  working tree, as did `git diff --check` and evidence-checker syntax parsing.
  A comparison with the starting commit confirmed D13's operational text
  before the M2 evidence paragraph is unchanged. Diff/surface audits confirmed
  no production, TakeItem source/evidence or release-identity changes, and no
  GiveItem/behavior enum/DropRemainder runtime exposure. These lightweight
  checks are not new runtime, live-host or final-source CI qualification.
- Package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider
  protocol `1.2`, schema `1` and pinned Luau remain unchanged.
- No DropIfFull, DropRemainder, DropItem or Player-1F-C work began.
- The initial investigation made no commit because implementation gates had
  not passed. The subsequent user request separately authorizes committing
  this reconciled architecture/evidence baseline after consistency checks;
  it does not require or claim runtime G1 PASS. Adoption/evidence are committed
  together; the resulting SHA is reported in the completion response and Git
  history, avoiding a self-referential commit hash in this file.
- No persistent task container/server remains; prior artifact caches remain
  for follow-up under the task-owned directory. This policy continuation runs
  only local lightweight consistency checks, not expensive runtime/server tests.

<!-- END RECORD PlayerInteractionFoundation1FB.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1FB-Validation.md -->

<a id="playerinteractionfoundation1fb-validation"></a>

<a id="playerinteractionfoundation1fb-validation-player-1f-b-implementation-qualification"></a>

# Player-1F-B implementation qualification

Date: 2026-09-21. Starting commit: `03b6d68346e01747e3b9250f1f4c4668ae3d8383`,
branch `main`. Scope: GiveItem InventoryOnly only; no Player-1F-C closure.

**Verdict: PASS for the recorded supported-host/server-authoritative scope.**
Windows live Carbon, authenticated-client receipt/UI and Shockbyte remain
unqualified; the final-source CI result is reported separately after push.

<a id="playerinteractionfoundation1fb-validation-source-and-identity"></a>

## Source and identity

The implementation and evidence are committed together after the local applicable
gates. Git history identifies that commit; no self-referential SHA is embedded.
Final-source CI is reported separately after publication and is not inferred from
local tests. The [implementation record](#playerinteractionfoundation1fb) routes
to canonical I12/D13/D18 and public docs.

Unchanged identities: package `0.4.0`, API `0.4.0-experimental`, native ABI `1.4`,
provider protocol `CarbonLuau.Addons` / `1.2`, schema `1`, Luau
`c6b830185af962c82003f86784e2fe036357c830`. A private host operation and bootstrap
value are added, but no C ABI signature/layout/export changes.

<a id="playerinteractionfoundation1fb-validation-target-and-upstream-evidence"></a>

## Target and upstream evidence

Rust Dedicated Server app `258550`, build `25353106`; Carbon `2.0.259`,
protocol `2026.09.03.0`, revision `21063e8490adf412101bcc7d1cfe9d6280f61e80`.
Downloaded exact depots: Linux `258552/3352454092778561960`, Windows
`258551/7816298056519226227`, shared `258554/4408100835840826754`.
Vanilla Assembly-CSharp SHA-256:

- Linux: `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.
- Windows: `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a`.

Live Carbon.Hooks.Oxide.dll SHA-256:
`028d8ee801937d97447a71a459684281cd44653b59416fd6968b63fe097d33c0`.
Selected identical Windows/Linux IL, public callback fields, constructor/removal
behavior, CanStack, CanMoveTo, MoveToContainer and conflict paths were inspected
using ILSpy/Mono.Cecil against these exact assemblies. The existing M2 structural
checker and the 1F-B callback/drop exposure checker supplement manual control-flow
review. Decompiled proprietary source is not committed.

Exact mutation APIs: `ItemManager.Create(ItemDefinition, Amount, 0)`, then
`Item.MoveToContainer(Target, Chunk.Slot, AllowStack, false, Player, false)`;
qualified temporary cleanup uses `Item.Remove(0)`. Lookup remains the existing
canonical exact-short-name wrapper around `ItemManager.FindItemDefinition`.
No callback-capable acceptance call occurs in PREPARE. Guards exclude baseline
`RemoveConflictingSlots`, `CanEquipItem` conflicting belt relocation, and
`WearItemCheck` conflicting clothing relocation paths. Real returned items are
checked again after creation; actual CanStack is used before merging.

<a id="playerinteractionfoundation1fb-validation-results-and-reproduction"></a>

## Results and reproduction

Worker: user-authorized BigKVM, Ubuntu 24, task root
`/srv/codex/CarbonLuauPlayer1FB20260921`, image `carbonluau-gui3e:latest`.
Disposable build/test containers use 1–2 CPUs, 2–3 GiB; live server 2 CPUs/6 GiB.
No ports are published, no real client is connected, and unrelated workloads are
untouched. Local Windows fallback uses MSVC 19.40, x64, one compile job.

| Gate | Evidence |
|---|---|
| Initial supported-host adapter requalification | `CarbonLuau.GiveItemG1Evidence.cs`: default/no callback/accept/inspection/reject, exact-slot main/belt/wear, merge, chunks, cleanup, acceptance and post-insertion exceptions. Ran before production implementation. |
| Model / planning | `PlayerInteractionFoundation1FBTests`: empty/full/insufficient, partial stacks, merge+empty, exact 128 chunks, one-over rejection, no Create on false, unknown input, identity and resource state, one fresh VERIFY. |
| Failure injection | Creation exception/null, transfer false/throw, accepted-then-throw/false, cleanup throw, VERIFY error/stale, world/inconsistent state, UID change. Every ambiguous COMMIT errors; accepted state is not rolled back. |
| Stress | 1,000 each successful merge, complete PREPARE rejection, callback rejection cleanup and transfer-failure cleanup: 4,000 model operations; gate and tracked references return to zero. |
| Native public API | Default/explicit typed behavior, invalid types/numeric forms, unknown/oversized name, provisional rejection, deferred grant, failed candidate, reconnect, root replacement, script error after grant, timeout/recovery without replay and teardown. |
| Command authorization | Denied grant command never enters Luau or creates an Item; authorized command grants. No method-specific permission requirement is added. |
| Addons | Shared-module provisional laundering rejection, foreign-domain enum, deferred grant, failed/successful replacement, root/addon Give/Take gate, provider unload and registration after unload. |
| Linux runtime/regressions | Full net48/Mono runtime suite passed: TakeItem, Player observations/Teleport, GUI, package/parser, addon/provider, scheduler/loader, Foundations A–G affected suites. Release CTest 5/5. |
| Windows available runtime | Full net48/native runtime suite and CTest 5/5 passed locally. Windows live Carbon not run; exact Windows assembly evidence retained, not equivalent to Windows live qualification. |
| Sanitizers / allocation faults | Linux instrumented runtime and pinned Luau: ASan/UBSan/leak detection, CTest 5/5 including RuntimeAllocationFaults and compiler containment. |
| Production live host | `CarbonLuau.Player1FBFixtures.cs` through actual `.cszip` plus native bridge: main/belt/wear, compatible merge/multi-chunk, full PREPARE rejection, normal callback rejection/exception cleanup, physical VERIFY, 300 repeated grant/Take/rejection operations, recursive Give/Take, API/reconnect, provisional and failed candidate. Two plugin cycles; `/proc` confirms native unmapping after each unload. |
| Structural/API/docs/packaging | `Test-GiveItemSafety.ps1` wired into architecture checks; exact invocation, no generic/auto/swap/ignore/drop, bounded plan, combined VERIFY, shared gate and typed behavior. Existing package/API/release checks apply. |

The live harness is [Test-Player1FBLinux.py](../../tools/Test-Player1FBLinux.py), using
`tools/package.ps1 -IncludePlayer1FBFixtures` only for the isolated fixture
package. Production packaging omits both live fixture files. Build native with
the normal CMake Release/ASan configurations; run `tests/runtime/RuntimeTests.csproj`
against the resulting library/compiler and the usual wrong-ABI/legacy fixtures.
No new external test service or secret is required. RCon credentials are ephemeral
in-memory fixture values and not committed or printed.

Preserve valid historical M1 design/model guidance, M2 G2–G5 and unaffected phase
evidence. There is no standalone Inventory-M1 test executable in this baseline;
the new deterministic GiveItem model and existing TakeItem/scanner tests exercise
the relevant current contracts. New PASS markers do not claim to rerun historical
client-observed gates.

<a id="playerinteractionfoundation1fb-validation-i12-boundary-demonstrations--separate-from-supported-host-success"></a>

## I12 boundary demonstrations — separate from supported-host success

The live G1 fixture changed maxStackSize during acceptance and reproduced the
source-derived internal split/drop exposure despite an outer `true`; changed
Item.amount produced insufficient physical delivery; changed target occupancy
produced transfer failure. The production fixture exercises limit, amount and
slot interference and requires indeterminate errors, not successful grants.
These are explicitly injected trusted-plugin mutations, not inferred blame for
unexplained failures. Ordinary acceptance/rejection remains supported-host scope.

The first production harness attempt lacked the required empty modules folder;
the second raced plugin-copy reload against its initial readiness marker. Neither
is a qualifying run. The corrected harness creates the folder and stages the
package before startup; subsequent complete runs include native unload checks.
Logs/artifacts remain in the task worker's `evidence/` and `build/` directories.

Final live source/package run: `evidence/live-production-20260921-122558/server.log`,
container `codex-carbonluau-1fb-host-reviewed`, exit 0, two complete fixture cycles
plus two native-unmap checks. Initial adapter run:
`evidence/live-g1-20260921-115456/server.log`. Model/native final output:
`codex-carbonluau-1fb-review-final`; Windows output:
`dist/player1fb/windows-runtime-final.log`. That Linux container's tests passed
but its last release-packaging check initially lacked Git metadata in the archive
workspace; fetching the exact starting Git baseline (without replacing source)
and rerunning `Test-Release.ps1` passed. This was a harness/provenance prerequisite,
not a runtime result. Final review also added the nonpositive-stack regression
and capped merge chunks at the observed stack limit; managed/live tests were
rerun afterward. Native source did not change after its sanitizer/native gates.

Tested artifact SHA-256 values (fixture package is **not** a release artifact):

| Artifact | SHA-256 |
|---|---|
| Linux native | `b01494c3af2affbfffb1fa2f2552f45c1d83f25b7b79841d2c2319881eff29bd` |
| Linux compiler | `5b82c00c70d6d6ea0b38557d6f462067c5fd25d0b02a7062c1f3aa3884d39132` |
| Windows native | `08432a5cad3ef08c60f2a7b632a574fd4acfc5fd6f6c284d61856ffd5b7dd51e` |
| Windows compiler | `5f4977f9805980b0aa00f2341221e4c620a56b69a1215f2b636acef8ff133f8b` |
| Live fixture cszip | `fd8e344f1cba0625963da7e44f22883747db01967ff8cffff71a97a97126e1c5` |

Exact-build static reruns: Inventory-M2 92 checks; callback/drop exposure 51
checks. Windows/Linux loader suites each passed 100 native load/unload cycles.
Windows import checks passed with no host-selected dynamic MSVC CRT dependency.
Package/API/architecture and release identity/determinism checks passed; production
package contains 40 C# sources and no live fixtures or native binaries. All
task-owned servers/containers were stopped at completion; artifact caches remain.

<a id="playerinteractionfoundation1fb-validation-limits-and-verdict-boundary"></a>

## Limits and verdict boundary

No exhaustive item-definition/mod combination or arbitrary trusted-C# behavior
is qualified. Default stack compatibility is deliberately conservative; a safe
complete plan may be unavailable even with apparent free space. Ordinary creation
or transfer failure is controlled indeterminate; callback-side allocations that
never return a handle cannot be claimed cleaned by CarbonLuau. Cleanup failure
is counted and reported as uncertainty, not silently called successful cleanup.
No retained host references, retry queue or mutation history outlives the call.
There is no hard deadline preemption of Rust/C# callbacks.

No authenticated client receipt/UI, Windows live Carbon, Shockbyte qualification,
DropRemainder, DropIfFull, raw item/container surface, rollback, arbitrary slot
targeting, nested/cross-player transfer or Player-1F-C closure is claimed.

<!-- END RECORD PlayerInteractionFoundation1FB-Validation.md -->

<!-- BEGIN RECORD PlayerInteractionFoundation1FC.md -->

<a id="playerinteractionfoundation1fc"></a>

<a id="playerinteractionfoundation1fc-player-interaction-foundation-1f-c--correction-and-combined-closure"></a>

# Player Interaction Foundation 1F-C — correction and combined closure

**Current verdict: PASS for the recorded server-authoritative experimental
scope.** The D18 cold-module defect is corrected; Windows/Linux focused,
combined, native/runtime, loader, package and Linux sanitizer/live gates passed.
Final-source CI and documentation deployment are reported after publication,
not inferred from worker runs. No tag or GitHub Release is created.

The initial stopped investigation is preserved below, explicitly describing
the unfixed `9b27ba5` baseline. The correction and resumed results follow it.

<a id="playerinteractionfoundation1fc-historical-stopped-investigation--before-correction"></a>

## Historical stopped investigation — before correction

Date: 2026-09-21. Production revision during this initial investigation:
`9b27ba5625c86eb32d9168effa2c6e72d8866576`, branch `main`.

**Historical verdict: BLOCKED / FAIL — D18 first-load module mutation restriction.**
No production fix, architecture change, release/tag, commit or push was made.
The local test/evidence changes are uncommitted. This is not Foundation 1 closure
or a new release-readiness claim. Existing qualification records remain historical
evidence for their tested paths; they do not establish the missing gate below.

<a id="playerinteractionfoundation1fc-reproduced-stop-condition"></a>

### Reproduced stop condition

[D18](../Invariants.md#d18--player-interaction-foundation-1) prohibits Teleport,
GiveItem and TakeItem from first-load modules, as well as provisional candidates
and dependency chains. A cold `require` inside an already committed operation
does not currently enforce that restriction.

The focused regression starts a real compiled/native Luau runtime with the
production managed facade and deterministic host adapters. Root candidate calls
are correctly rejected with zero mutations. After commit, it separately loads
three previously unloaded modules. Each module invokes one mutation and then
raises an ordinary error. The outer committed operation catches the module error.

Observed on both Windows x64 and Linux x64:

| Failed module | Cumulative Create calls | Cumulative Take calls | Cumulative Teleports | Physical scrap |
|---|---:|---:|---:|---:|
| Initial state | 0 | 0 | 0 | 5 |
| Give 2, then error | 1 | 0 | 0 | 7 |
| Take 1, then error | 1 | 1 | 0 | 6 |
| Teleport, then error | 1 | 1 | 1 | 6 |

Expected mutation counts are all zero. The test intentionally fails instead of
accepting these effects. It confirms gate/reference release and native VM teardown
before reporting failure. No external trusted-plugin mutation is involved; I12
cannot excuse this CarbonLuau publication-context error. Individual Give/Take
physical verification succeeds, but the calls should never reach COMMIT here.

<a id="playerinteractionfoundation1fc-source-explanation-at-the-failing-revision"></a>

### Source explanation at the failing revision

- [HostPrimitive](../../native/src/facade/FacadeBridge.cpp) checks only
  `Runtime.Admission->Provisional` for mutation opcodes 28, 29 and 30.
- [ModuleLoader](../../native/src/scripts/ModuleLoader.cpp) creates a nested
  `PublicationScope` for cold module initialization.
- [PublicationScope](../../native/src/runtime/RuntimeInternal.hpp) changes
  `Runtime.Publication`, not the outer admission's provisional flag.
- The [managed facade](../../src/CarbonLuau/Facade/FacadeSession.cs) sees an active
  owning session, so its committed-domain checks do not reject these calls.

The affected implementation is stopped under the user's explicit D18 contradiction
condition. The proposed follow-up is to enforce the existing mutation restriction
for first-load publication scopes, without changing ownership or resetting the
admitted deadline, then qualify nested/shared-module paths and resume closure.
No new semantics or compensating rollback are proposed.

<a id="playerinteractionfoundation1fc-tests-and-reproduction-before-correction"></a>

### Tests and reproduction before correction

[PlayerInteractionFoundation1FCTests](../../tests/runtime/PlayerInteractionFoundation1FCTests.cs)
is wired into the runtime suite. The focused entry points are:

```text
RuntimeTests.exe --player1fc-only
RuntimeTests.exe --player1fc-module-boundary <Carbon-data-directory>
```

On Linux run the executable through Mono. The data directory must contain the
matching native library and compiler under `CarbonLuau/native/<rid>/` using the
normal loader layout. Build `tests/runtime/RuntimeTests.csproj` in Release.
The module-boundary command currently exits 1 on both platforms, as required by
the failing contract assertion. It uses synthetic host adapters, not a Rust
server or authenticated client.

The combined model passed on Windows and Linux: all four recursive Give/Take
pairs, different-Player independence, shared-gate release, separate-definition
shop rejection/error with no payment rollback, non-reserving reads and 9,000
repeated success/rejection/programming/stale/post-COMMIT calls. Saturating
diagnostics and zero gate/tracked-reference baselines were checked. This does
not establish the unfinished addon/lifecycle/live closure matrix.

The Windows full managed/native suite passed earlier Player/model/GUI checks but
stopped at the new cold-module assertion. Later suite sections did not run to
completion. No final full-suite PASS is claimed.

<a id="playerinteractionfoundation1fc-original-environment-and-artifacts"></a>

### Original environment and artifacts

Production native/compiler binaries are the unchanged qualified 1F-B artifacts.
Native SHA-256:

- Windows: `08432a5cad3ef08c60f2a7b632a574fd4acfc5fd6f6c284d61856ffd5b7dd51e`.
- Linux: `b01494c3af2affbfffb1fa2f2552f45c1d83f25b7b79841d2c2319881eff29bd`.

Windows uses the local net48 runtime; focused log:
`dist/player1fc/windows-module-boundary.log`.
Linux uses user-authorized BigKVM, image `carbonluau-gui3e:latest`, one CPU/2 GiB,
task directory `/srv/codex/CarbonLuauPlayer1FC20260921`, exited container
`codex-carbonluau-1fc-module-boundary` (exit 1). Its Docker log holds the model
PASS and boundary failure. The native cache was mounted read-only. No ports were
published, no game server was started, and unrelated workloads were untouched.

<a id="playerinteractionfoundation1fc-closure-still-pending-at-the-initial-stop"></a>

### Closure still pending at the initial stop

No final-head CI/docs deployment, new live Carbon validation, release packaging,
checksums/provenance package, sanitizer rerun, complete API/example audit,
root/addon/provider/recovery stress closure or experimental readiness verdict is
claimed. These remain pending after correction and revalidation. The full
completion report cannot truthfully report PASS while the mandatory gate fails.

Package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider
`CarbonLuau.Addons` / `1.2`, schema `1` and Luau
`c6b830185af962c82003f86784e2fe036357c830` are unchanged. No gameplay API was added.
Teleport authenticated-client convergence, Windows live Carbon, historical
Foundation G platform limitations and Shockbyte/full-host qualification remain
as recorded in their owning evidence, not silently promoted by these tests.
DropRemainder, transactions/rollback, raw inventory/container access, health
mutation and other deferred Player/entity APIs remain unimplemented.

<a id="playerinteractionfoundation1fc-correction--2026-09-21"></a>

## Correction — 2026-09-21

Starting commit: `9b27ba5625c86eb32d9168effa2c6e72d8866576`. The correction,
regressions, examples and this evidence are committed together after applicable
worker gates. Git history identifies that commit; this file cannot embed its
own SHA. Final-source CI identifies the exact tested revision after push.

Only three production native files change: the internal declaration in
`RuntimeInternal.hpp`, `CanMutateHost` in `Publication.cpp`, and its use by
`HostPrimitive` in `FacadeBridge.cpp`. The single eligibility predicate requires
an existing nonprovisional admission **and no active publication scope**.
It governs opcodes 28/29/30 (Teleport/Take/Give), plus opcode 4 (SendMessage),
which already belongs to the same committed-only effect class. There is no
new managed rule, transport, field, C ABI or public API.

The current publication scope belongs to the VM execution chain, not the
lexical owner of a closure or captured facade. Existing RAII nesting/restoration
therefore carries the restriction through local/nested/public/transitive/shared
calls and restores it on success, ordinary errors and fatal unwind. Cached
exports called later in committed execution are not permanently provisional.
No admission creation, clock/deadline assignment, module loader, cache,
protected-call, scheduler or ownership implementation changes. A cold module
still inherits the original admitted deadline; the uncatchable timeout test
retires the VM through the existing recovery path.

<a id="playerinteractionfoundation1fc-focused-result-after-correction"></a>

### Focused result after correction

Windows x64 and Linux x64 both report the following cumulative counters for the
preserved three-module regression. Each error is caught by the outer operation:

| Cold module | Create | Take | Teleport | Physical scrap |
|---|---:|---:|---:|---:|
| Give | 0 | 0 | 0 | 5 |
| Take | 0 | 0 | 0 | 5 |
| Teleport | 0 | 0 | 0 | 5 |

The mutation itself now errors before entering the managed host operation,
instead of merely reaching the later explicit module error. I12 is not involved.
The live Linux supplement separately proves unchanged server position, physical
quantity, and Give/Take host-attempt counters through the production package.

<a id="playerinteractionfoundation1fc-resumed-qualification"></a>

## Resumed qualification

| Gate | Result and evidence |
|---|---|
| Cold execution | PASS: root local/nested modules; command, GUI Activated and deferred committed callbacks; public/transitive/shared addon modules and foreign captured Player facade. No cold mutation reaches host COMMIT. |
| Reads/cache/restoration | PASS: Position, Health, MaxHealth, Items.Exists, CountItem, HasItem and Vector3 during initialization. Same cached function identity subsequently grants/takes/teleports. Ordinary failed modules remain catchable, retry twice, publish no command or deferred mutation; nested restoration permits later committed work. |
| Shared mutation gate | PASS: all four recursive Give/Take pairings, other-Player independence, true/false/stale/programming/post-COMMIT release. Existing addon test holds the exact shared world gate while root/addon deferred Give/Take attempt entry, without recursive VM entry. |
| D11 | PASS: 20 disconnect/reconnect cycles retain a non-nil actual old Player proxy and check stale errors for reads and all three D18 mutations; no retarget or host effect. |
| Root/addon lifecycle | PASS: failed candidate preserves active authority and emits zero mutation; successful replacement drops stale queued GUI work. Required dependency reconstruction gets fresh authority; optional binding remains stale and captured old exports cannot mutate the replacement. Provider unload/re-registration returns gate/reference baselines with no replay. |
| Recovery | PASS: 10 explicit reload/rearmed fatal cold-module timeouts after representative reads and Give/Take/Teleport; effects already performed stay performed, queued work is discarded, reconstructed authority works. No replay or exactly-once claim. |
| Result-contract audit | PASS: both inventory operations return false only from PREPARE; true follows operation-specific physical VERIFY. COMMIT failures are errors; `finally` releases gates. Programming/stale errors may also occur before COMMIT. Source audit plus existing 1F-A/B fault cases; no production inventory adapter changes. |
| Physical scanner | PASS: existing Player-1C and 1F-A/B suites cover authoritative direct reciprocal main/belt/wear membership, exact definition, no recursion, 128 bound and no partial overflow answer. Count/Has do not reserve subsequent mutations. |
| I12 | PASS within its recorded boundary: explicit stack-limit, amount and slot mutation fixtures remain separately labeled external interference, not supported-host success. Take matching-item interference is indeterminate; unrelated-item mutation still permits verified removal. Ordinary accept/reject/exception behavior remains CarbonLuau responsibility. |
| Returned resources | PASS: rerun 1F-B creation/null/transfer/accepted-then-throw/VERIFY/stale/UID/cleanup-failure cases. Accepted/consumed/qualified-cleanup outcomes or explicit indeterminate errors; no retained references, double cleanup or rollback. Unreturned callback allocations are not claimed cleaned. |
| Workflows/examples | PASS: separate-definition shop payment followed by grant false/error does not refund; later explicit grant works. Command permission denial never enters Luau, authorized command/GUI/deferred rewards work. Six bundled Player examples execute through the real compiler/VM, using deterministic adapters and synthetic GUI ingress. |
| Stress | PASS: 9,000 combined model calls plus rerun 4,000 1F-B stress operations and 100 root/addon contention rounds (400 rejected calls); bounded/saturating counters, zero busy gates/tracked references. Native 1,000 VM cycles and 1,000 script generation cycles; managed 100 atomic replacements/200 failed candidates; existing 0/1/10/50/100-addon and GUI scale/fairness suites. |

<a id="playerinteractionfoundation1fc-platform-and-live-scope"></a>

### Platform and live scope

- **Windows:** MSVC 19.40 x64 Release, full net48/native suite on `dockerbox`
  (`HostPC`), task root `C:\Sandbox\Codex\Workspaces\CarbonLuau1FC`.
  All five native test executables passed, including allocation faults and
  compiler 256 MiB containment/recovery. Final managed suite includes new
  examples. Workstation loader 100 cycles, native/compiler import audits and
  deterministic Windows packaging also passed. Native code used the existing
  one-job incremental cache; worker tests were serial. Crash fixtures inherited
  process-local Windows error-dialog suppression, restored on completion.
- **Linux:** BigKVM, Ubuntu 24, image `carbonluau-gui3e:latest`, existing
  `/srv/codex/CarbonLuauPlayer1FB20260921` cache. Two build jobs/2 CPUs/3 GiB;
  full Mono runtime, loader 100 cycles, Release CTest **5/5** and instrumented
  Luau/bridge ASan + UBSan + leak/allocation-fault CTest **5/5** passed.
  `codex-carbonluau-1fc-regression`, `codex-carbonluau-1fc-final` and final
  contention/documentation rerun `codex-carbonluau-1fc-closure` exited 0.
  Final runtime includes Foundations A–G affected tests, Player-1A–1D/1F-A/B,
  inventory models, GUI, modules/parser, providers, scheduler and examples.
- **Live Linux:** Rust app `258550`, build `25353106`; Carbon `2.0.259`, protocol
  `2026.09.03.0`, revision `21063e8490adf412101bcc7d1cfe9d6280f61e80`.
  Exact assembly evidence and unchanged adapter calls are routed by
  [1F-B validation](#playerinteractionfoundation1fb-validation). The amended
  `CarbonLuau.Player1FBFixtures.cs` and `Test-Player1FBLinux.py` passed twice
  through actual Carbon/Rust: Give/Take, merge/empty/full, ordinary callbacks,
  300 repeated operations per cycle, explicit I12 fixtures, reconnect,
  provisional/failed reload, and the new zero-COMMIT cold-module supplement.
  `/proc` confirmed native unmapping after each unload. Container
  `codex-carbonluau-1fc-live` exited 0; log
  `evidence/live-production-20260921-205306/server.log`. Two CPUs/6 GiB,
  no published ports, synthetic fixture Player, **no authenticated client**.

Exact Windows/Linux host-assembly static evidence was also rerun read-only:
Inventory-M2 **92 checks**, callback/drop exposure **51 checks**. The exposure
checker confirms the historical hazard, not G1 safety; supported-host safety
is established only within the separately recorded adapter/live scope.

Task-owned test containers/servers exited; unrelated services were untouched.
Incremental caches and diagnostic logs remain. The live rerun uses final
production/runtime/fixture source; subsequent changes only add examples,
test coverage and documentation. Unaffected prior live host evidence is retained,
not relabeled as newly executed.

<a id="playerinteractionfoundation1fc-release-facing-audit"></a>

### Release-facing audit

API, architecture/GiveItem-safety, relative links, package contents and
deterministic platform-bundle checks passed. Production `.cszip` excludes live
fixtures. Public docs/navigation/release notes route to this correction and the
new Player examples; the bundle content audit requires all six Player examples.
Clean-commit bundles/checksums/provenance are generated after commit, and hosted
CI independently rebuilds both platforms at that revision. No tag/release is
created by this qualification task.

Corrected tested native SHA-256:

| Platform | SHA-256 |
|---|---|
| Windows | `89f54153df9021e3add9603133cb0f2d12d8cb80e2bdd59eba1143c0099f60a6` |
| Linux | `34e640d594b49c3806740a2ec441523ade9e2097061c9deb5d0f5d11938a468d` |

Compiler source/binaries, pinned Luau and all six version identities above are
unchanged. No new gameplay API, raw host object, drop behavior, transaction or
health/entity mutation was introduced. Canonical I12/D11/D13/D18 were not weakened.

<a id="playerinteractionfoundation1fc-readiness-and-remaining-limitations"></a>

## Readiness and remaining limitations

The implemented Player Interaction Foundation 1 surface is qualified for the
existing unreleased **0.4.0-experimental server-authoritative candidate**, within
the target-build and I12 boundary. This closes the combined lifecycle/public
qualification planned as Player-1E together with inventory Player-1F-C; it does
not authorize deferred gameplay work.

Windows live Carbon was not rerun or newly qualified. The new Windows worker
native/managed/containment PASS does not erase Foundation G's historical
live/local deferral. Teleport authenticated-client convergence, GUI receipt/UI,
exhaustive item/mod combinations and Shockbyte full-runtime qualification remain
unqualified. Existing Phase 0 Shockbyte evidence is unchanged. No operating-system
isolation from hostile installed C#, rollback, automatic retry, hard preemption
of host callbacks or exactly-once delivery is promised.

<!-- END RECORD PlayerInteractionFoundation1FC.md -->

