# CarbonLuau Player Interaction Foundation 1 — proposed D18 architecture

> **Canonical amendment (2026-09-21):** Revised D13 now accepts implementation-
> gated `Player:GiveItem` and `Player:TakeItem` under bounded
> PREPARE/COMMIT/VERIFY semantics. The original response below remains the
> supporting Foundation 1 record; where it categorically defers those two
> operations or says no Player-1F phase exists, the current D13/D18 text and
> [Inventory Ownership / Failure Reassessment](InventoryOwnershipFailureReassessment.md)
> supersede that conclusion. No inventory mutation is implemented by this note.

**Baseline reviewed:** `main` at `b00bf21b1e1ef11b1524a10e2d094d3934024a7b` (`Complete GUI Foundation 3 qualification`), plus the current `AICONTEXT.md`, `docs/Invariants.md`, Player facade/bootstrap implementation, API docs, D11, and the retained Phase 4/D13 investigation. [Baseline commit](https://github.com/gmoddev/CarbonLuau/commit/b00bf21b1e1ef11b1524a10e2d094d3934024a7b?utm_source=chatgpt.com) [Canonical invariants](https://github.com/gmoddev/CarbonLuau/blob/main/docs/Invariants.md?utm_source=chatgpt.com) [D13 investigation](https://github.com/gmoddev/CarbonLuau/blob/main/docs/Phase4.md?utm_source=chatgpt.com)

This is architecture/research only; no repository modification, implementation, or commit is implied.

One evidence distinction matters throughout: CarbonLuau's qualified worker record still names Carbon `2.0.259.0` and Rust 2633 / Steam `25230300`. I also checked Carbon's **current** generated Rust hook/item metadata as of September 21, 2026. The latter is useful evidence that the relevant behavioral problems still exist, but it is not a substitute for qualifying the exact Rust build CarbonLuau ultimately targets.

---

## 1. TL;DR and exact recommended Foundation 1 surface

I recommend **accepting this exact public surface**:

```lua
-- General value type
Vector3.new(X, Y, Z)

Vector.X
Vector.Y
Vector.Z
Vector.Magnitude

A == B
A + B
A - B
-A
A * Scalar
Scalar * A
A / Scalar

-- Player spatial state
Player.Position
Player:Teleport(Position)

-- Player health observation
Player.Health
Player.MaxHealth

-- Inventory observation
Player:CountItem("scrap")
Player:HasItem("scrap")
Player:HasItem("scrap", 100)

-- Item identity/configuration
local Items = game:GetService("Items")
Items:Exists("scrap")
```

Foundation 1 should **not** include:

```lua
Player.Health = Value
Player:SetHealth(Value)
Player:Heal(Amount)
Player:Damage(Amount)

Player:TakeItem(...)

Player:GiveItem(...)
```

The major conclusions are:

* `Vector3`: **accept**.
* `Player.Position`: **accept**.
* `Player:Teleport(Vector3)`: **accept architecturally**, but public availability requires an authenticated-client convergence qualification gate.
* `Player.Health`, `Player.MaxHealth`: **accept read-only**.
* `Player:CountItem`, `Player:HasItem`: **accept read-only**.
* `Items:Exists`: **accept**. D18 should deliberately reopen only the read-only item-identity portion previously removed from scope by D13.
* All health mutation: **defer**.
* `TakeItem`: **defer**.
* `GiveItem`: **remains deferred under D13**.

That gives authors a meaningful gameplay API without dragging Rust's object model into Luau.

---

# 2. Current Rust/Carbon technical findings

### Position

The current Oxide Rust player library—which is useful corroborating adapter evidence—defines player position as `player.transform.position`. Unity defines `Transform.position` as world-space XYZ, including when an object has a parent. ([GitHub][1])

That supports a clean CarbonLuau abstraction:

```lua
Player.Position
```

as the player's **world-space root position**, not eye position, local mount coordinates, terrain contact position, or a transform object.

### Teleportation is more than assigning a position

Current Oxide's player abstraction does substantially more than setting a transform: it requires an alive/non-spectating player, dismounts, removes parenting, enables server fall protection, moves the player, forces the client position, and restores fall behavior. ([GitHub][1])

Current Rust code visible through Carbon's generated metadata reinforces that there is no universal "`transform.position = destination` and done" model. Rust's portal implementation pauses fly/speed anti-cheat, adds stall protection, unparents the player, performs a teleport, updates the network group, marks the player as receiving a snapshot, and sends an immediate network update.  Carbon itself also has a player relocation path that toggles fall handling around `Teleport` and clears `estimatedVelocity`.

Therefore `Player:Teleport(Position)` is a good **facade**, but CarbonLuau must own the ugly backend sequence.

### Health is not just one mutable number

Current Rust/Carbon metadata shows `BasePlayer.OnHealthChanged` invokes `OnPlayerHealthChange`; normal handling then updates base health-related behavior, life-story hurt/heal statistics, and metabolism dirty state.

Death is another lifecycle operation with item/gesture/wound handling and `OnPlayerDeath`; wounded recovery separately manipulates wounded/incapacitated flags, potentially sets health, informs the active game mode, fires recovery hooks, and refreshes the player collider.

Facepunch also added `maxHealthOverride` to `BaseCombatEntity`, explicitly making maximum health runtime-modifiable rather than necessarily equal to a fixed default. ([Facepunch Commits][2])

So reads are straightforward. A generic writable `Health` property is not.

### Inventory reads and removals have materially different risk

Current Carbon metadata exposes a hookable `PlayerInventory.GetAmount`, and the current `PlayerInventory.Take` path:

1. invokes `OnInventoryItemsTake`;
2. permits an integer hook result to override the operation;
3. otherwise removes sequentially from **main → belt → wear**;
4. carries the remaining amount from one container into the next.

That is strong evidence against pretending `TakeItem` is a clean atomic transaction.

### Item identity remains short-name based

Carbon's current Rust Items API explicitly publishes `ShortName` alongside IDs and metadata—for example `hat.wolf`, `discord.trophy`, and `motorbike_sidecar`.

The CarbonLuau facade should expose the useful identifier—**short name**—without exposing `ItemDefinition` or integer IDs.

---

# 3. `Vector3` design

## Accepted semantics

`Vector3` should be a general CarbonLuau immutable value type using the same project-owned value infrastructure already used for `Vector2`, `UDim`, `Color3`, etc.

```lua
local A = Vector3.new(100, 20, -50)

print(A.X)
print(A.Y)
print(A.Z)
print(A.Magnitude)
```

### Representation

`X`, `Y`, and `Z` are Luau numbers.

Inputs must be:

* numbers;
* finite;
* within finite `System.Single`/Unity coordinate representability.

Do **not** apply the GUI-specific `Vector2` `[-32768, 32768]` limit. This is a world/general-purpose value.

CarbonLuau does not need to force every constructor argument through float32 rounding. The value can retain its Luau numeric components; conversion to Unity `Vector3` is validated at a host boundary. Consequently, a teleport followed by a position read may reflect ordinary Unity single-precision rounding.

### Equality

Exact component value equality:

```lua
Vector3.new(1, 2, 3) == Vector3.new(1, 2, 3) -- true
```

No identity semantics.

### Arithmetic

Foundation 1 should include the operations that are both familiar and clean:

```lua
A + B
A - B
-A

A * 2
2 * A
A / 2
```

`Vector3 * Vector3` and `Vector3 / Vector3` are rejected.

Division by zero errors.

Every arithmetic result must remain finite and representable by the `Vector3` component contract.

### Magnitude

Include:

```lua
Vector.Magnitude
```

It is useful immediately for spatial scripting and requires no additional abstraction.

### Defer

Do not add yet:

```lua
Vector.Unit
Vector:Dot(...)
Vector:Cross(...)
```

Those are natural future additions, but Foundation 1 does not need to recreate the complete Roblox math surface.

---

# 4. `Player.Position` semantics

Accepted signature:

```lua
Player.Position -> Vector3
```

It is a **read-only property**.

## Meaning

`Position` is an immutable snapshot of the player's BasePlayer root `Transform.position` in Rust/Unity world coordinates.

Unity explicitly defines `Transform.position` as world-space position, whereas `localPosition` is parent-relative. ([Unity Documentation][3])

Therefore:

* X maps directly to Unity X.
* Y maps directly to Unity Y/up.
* Z maps directly to Unity Z.
* No axis remapping.
* No scale conversion.
* No wrapping in a `Transform`.
* No eye/head offset.
* No automatic terrain projection.

This is also consistent with the current Oxide player abstraction, which reports `player.transform.position`. ([GitHub][1])

## State behavior

| Player state                                                 | `Position`                                 |
| ------------------------------------------------------------ | ------------------------------------------ |
| Normal connected player                                      | Returns current world position             |
| Mounted                                                      | Returns player's current world position    |
| Parented                                                     | Returns world position, not local position |
| Wounded/incapacitated                                        | Read allowed                               |
| Sleeping but still exact-connected                           | Read allowed                               |
| Dead but still represented by the exact connected BasePlayer | Read allowed                               |
| Disconnected                                                 | Controlled stale-player error              |
| Same account reconnects                                      | Old proxy errors; new proxy is required    |
| Retired owning domain                                        | Controlled stale-domain/facade error       |
| Retired VM                                                   | Old value cannot be used as a host proxy   |

Unlike `Name` and `UserId`, there is **no disconnected Position snapshot fallback**. Once D11 resolution fails, a host-backed world-state read fails.

## Provisional execution

Allowed.

`Position` is an observation, not publication and not an irreversible host effect.

---

# 5. Teleport semantics and failure model

Accepted surface:

```lua
Player:Teleport(Vector3.new(100, 20, -50))
```

No flags and no options object.

## Public contract

Foundation 1 `Teleport` requires:

* the exact Player connection is still valid;
* the owning domain remains valid;
* the admitted operation is committed, not provisional;
* the player is alive;
* the player is not spectating;
* the player is not wounded/incapacitated.

The wounded restriction is deliberate for Foundation 1. Rust treats wounded/crawling/incapacitated transitions as a distinct lifecycle with hooks, flags, health changes, game-mode notification, and collider changes. CarbonLuau should not claim ordinary teleport support for that state until it is separately qualified.

A connected sleeping player may be moved; the teleport does **not** implicitly wake the player.

## Mounted and parented players

These should not become author-facing special cases.

`Teleport` should:

1. dismount the player if mounted;
2. confirm the player is actually dismounted;
3. detach the player from host parenting;
4. then perform relocation.

The mounted vehicle itself is not moved.

If a host/plugin veto prevents the required normalization, fail before deliberate positional relocation where possible.

Current Oxide likewise dismounts and removes parenting before relocation. ([GitHub][1])

## Destination semantics

CarbonLuau validates the `Vector3` before any mutation.

It should **not**:

* silently clamp to the terrain;
* find a safe spawn location;
* raycast to the ground;
* automatically move the destination out of geometry;
* require Y to be terrain height.

Teleport means "move to this world coordinate."

A position in the air is valid if the host accepts it. Normal falling behavior resumes after teleport-specific fall state is reconciled.

CarbonLuau should not publish an arbitrary map-size clamp in D18. World sizes and special maps vary. Invalid/non-representable coordinates are rejected; coordinates outside the meaningful current world may be rejected by the adapter but are never silently transformed into another point.

## Required backend responsibilities

The **exact implementation sequence remains adapter-level**, but the adapter must solve, for the qualified Rust build:

* dismount;
* parenting;
* authoritative player position;
* fly/speed anti-cheat state;
* fall-history state;
* movement/estimated velocity needed to prevent inherited movement artifacts;
* client forced-position delivery;
* network-group changes;
* snapshot/loading handling for long-distance moves if necessary;
* immediate network synchronization where necessary.

Current Rust itself uses additional anti-cheat, parenting, network-group, receiving-snapshot, and immediate-update operations around portal relocation.  Current Oxide uses dismount, unparent, `SetServerFall`, `MovePosition`, and a forced client position. ([GitHub][1])

That is exactly the sort of host complexity CarbonLuau should hide.

## Triggers and collision

Foundation 1 does **not** promise synthetic path traversal.

Moving from A to B does not mean every trigger between A and B is crossed. Destination-side host trigger/collision behavior is whatever the qualified Rust teleport path actually establishes.

## Success

`Teleport` returns **no values**.

Success means the qualified server-side relocation sequence completed without reporting failure.

It does **not** mean:

* the client acknowledged receipt;
* every client frame observed the new position;
* the operation is reversible;
* the teleport will be replayed after recovery.

## Failure after mutation begins

Teleport is not a CarbonLuau transaction.

Once host mutation begins, a later host exception may leave effects such as dismounting, unparenting, or relocation already performed. CarbonLuau must report the operation as failed but must not pretend rollback occurred.

That follows D7/D10 rather than inventing transactional Rust game state.

## Provisional execution

Reject:

```lua
Player:Teleport(...)
```

during candidate/module provisional execution.

The error should direct authors toward deferred work.

This remains valid:

```lua
task.defer(function()
    Player:Teleport(Vector3.new(100, 20, -50))
end)
```

Existing D10 semantics already guarantee candidate deferred work cannot run before successful commit and never drains for a failed candidate.

---

# 6. Health read semantics

Accept:

```lua
Player.Health
Player.MaxHealth
```

Both return Luau numbers and are read-only.

## `Health`

`Health` is the current live host health value at the instant of access.

Do not clamp it to `[0, MaxHealth]`. If a modded host has unusual but valid state, CarbonLuau should report it rather than fabricate a different value.

A non-finite host value is a controlled host-state failure.

## `MaxHealth`

`MaxHealth` is evaluated from the host's current maximum-health semantics at access time.

It is **not** documented as stable or always `100`.

Facepunch added a runtime maximum-health override to `BaseCombatEntity`; the explicit goal was to make `MaxHealth()` reflect overrides server- and client-side. ([Facepunch Commits][2])

Therefore:

```lua
local Max = Player.MaxHealth
```

is a snapshot, not a constant.

## Life states

Reads remain legal for wounded/incapacitated and dead-but-still-live exact Player objects.

Crucially:

> `Health` alone is not CarbonLuau's life-state abstraction.

Rust wounded state has explicit flags and transitions beyond health.

Foundation 1 should not encourage scripts to infer "`Health > 0` means normal living player."

A later player-state surface can expose that intentionally.

## Stale behavior

Unlike `Name` and `UserId`, health has no stale snapshot semantics.

A disconnected/stale Player raises the existing controlled stale-player error.

---

# 7. Health mutation decision

**Defer all health mutation.**

Specifically, none of these enter D18:

```lua
Player.Health = 50
Player:SetHealth(50)
Player:Heal(25)
Player:Damage(25)
```

This is not an ergonomics problem. It is a semantic problem.

A direct health change participates in `OnPlayerHealthChange` and subsequent life-story/metabolism handling.  Damage can lead into wound/death behavior involving `HitInfo`, damage types, dropped active items, wound-vs-death decisions, and player hooks. Wounded recovery is yet another explicit process touching health, state flags, the active game mode, hooks, and collision.

Consequently:

* a writable `Health` property risks presenting host-state assignment as ordinary Roblox-like property mutation when Rust does not behave that simply;
* `SetHealth` would merely give the same problematic operation a method name;
* `Damage` requires a future decision about attacker/source/type and death/wound semantics;
* `Heal` requires a future decision about wounded-state interaction and healing semantics.

The right Foundation 1 answer is **read-only health**.

A future dedicated vitals/damage foundation can expose narrow Roblox-friendly methods once their Rust meaning is resolved.

---

# 8. Inventory observation semantics

Accept:

```lua
Player:CountItem(ShortName)
Player:HasItem(ShortName, Amount?)
```

## What "inventory" means

Foundation 1 inventory is exactly the player's ordinary top-level:

1. main container;
2. belt/hotbar;
3. wear container.

No nested recursion.

No backpack or future/special containers.

This choice tracks Rust's current standard `PlayerInventory.Take` ordering, which explicitly traverses main, then belt, then wear.

A richer future container API can expose additional storage deliberately.

## `CountItem`

```lua
Player:CountItem("scrap") -> number
```

Returns the sum of positive stack amounts for the named item across those three top-level containers.

It counts an item regardless of condition. A broken weapon is still an inventory item.

Exclude host entries that are already invalid/destroying/scheduled for removal or whose reciprocal container ownership no longer describes a valid current top-level stack.

Returned count is an integer-valued Luau number.

## `HasItem`

```lua
Player:HasItem("scrap")       -- equivalent threshold 1
Player:HasItem("scrap", 100)
```

Returns `true` as soon as physical observed quantity reaches the requested threshold.

It does not need to compute the entire total after reaching that threshold.

## Do not route this through hook-virtualized inventory counting

Current Carbon exposes `OnInventoryItemsCount` as an override-capable hook around `PlayerInventory.GetAmount`.

For CarbonLuau, `CountItem` should have a more precise meaning:

> bounded observation of the player's actual top-level inventory state.

Therefore the preferred adapter is a bounded owner-thread traversal of the three host containers, not an invocation of a count API whose answer another plugin can replace arbitrarily.

That gives CarbonLuau deterministic semantics while keeping the Rust container details entirely behind the facade.

## Provisional execution

Both are read-only and allowed provisionally.

---

# 9. Item identity and `Items` service decision

Include:

```lua
local Items = game:GetService("Items")

Items:Exists("wood")
Items:Exists("scrap")
Items:Exists("rifle.ak")
```

## Why include it

`CountItem` and `HasItem` answer questions about one Player.

`Items:Exists` answers a different and useful configuration question:

> Is this a recognized Rust item short name at all?

That is useful for configuration/module validation without finding an arbitrary Player.

The current Carbon reference itself organizes item definitions around `ShortName` and separately exposes internal numeric IDs. ([Carbon Documentation][4])

## Canonical identity contract

The public identifier is the **canonical lowercase Rust short name**.

Rules:

* string only;
* non-empty;
* maximum 128 ASCII bytes;
* no NUL;
* ASCII lowercase form only;
* exact short-name identity;
* no display-name search;
* no fuzzy search;
* no integer ID;
* no `ItemDefinition`.

CarbonLuau does not silently lowercase input.

Thus:

```lua
Items:Exists("scrap") -- valid request
Items:Exists("Scrap") -- programming error: noncanonical identifier
```

A syntactically canonical but nonexistent name returns:

```lua
false
```

Similarly:

```lua
Player:CountItem("does.not.exist") -- 0
Player:HasItem("does.not.exist")   -- false
```

That distinction is useful:

* malformed/noncanonical input → programming error;
* canonical but unknown identity → ordinary absence.

## Relationship to D13

This is an intentional scope change.

D13 currently says the entire former Phase 4 item-convenience surface, including `Items:Exists`, was deferred because no independent read-only product use case had been accepted.

Player Interaction Foundation 1 now **provides that use case**.

D18 should therefore supersede only that D13 **scope decision** for read-only item identity/observation.

It does **not** supersede or weaken D13's ownership analysis for item creation/insertion/removal.

---

# 10. `TakeItem` safety investigation and decision

**Defer `Player:TakeItem`.**

Current Rust/Carbon behavior is enough to reject both obvious contracts for Foundation 1.

## Why boolean all-or-nothing is wrong

This would be misleading:

```lua
Player:TakeItem("scrap", 50) -> boolean
```

Current `PlayerInventory.Take` first allows `OnInventoryItemsTake` to override its returned integer. Without an override, it sequentially removes from main, then belt, then wear.

There is no atomic reservation across those containers.

A later failure does not imply earlier removal did not occur.

## Why `removedAmount` is not clean enough either

This is more honest:

```lua
Player:TakeItem("scrap", 50) -> removedAmount
```

but still not a good Foundation 1 contract.

An external hook may return an integer without performing the default removal at all. Conversely, callbacks and container operations can mutate host state before later failure. The returned host count therefore cannot universally serve as proof of one specific physical inventory delta.

CarbonLuau could try to implement its own removal algorithm, but that immediately means reproducing container mutation, stack splitting, callbacks, item destruction, and cleanup behavior—the same class of host-internal coupling the D13 work showed to be dangerous.

So the design answer is not "use a more complicated result object."

It is:

> **Do not expose removal yet.**

---

# 11. `GiveItem` / D13 status

**`Player:GiveItem` remains deferred under D13.**

Nothing in the current evidence establishes a new supported ownership/abort transaction that invalidates the existing finding.

The retained investigation established the actual blocker:

* item construction can run item-mod initialization before returning ownership to the caller;
* child resources can be created during initialization;
* insertion can publish partial state before later callbacks/bookkeeping finish;
* stacking/splitting/drop paths can partially mutate;
* cleanup itself invokes fallible lifecycle paths;
* no universal supported commit/abort boundary was found.

Current Rust item metadata still shows item definitions using `ItemModEntity`, `ItemModContainer`, etc.; these resource-producing item types have not disappeared.

So D18 must explicitly retain:

> **GiveItem remains deferred under D13.**

No weaker ownership rule, no "best effort" grant, and no silent world-drop semantics.

---

# 12. Exact Player lifetime and stale-proxy behavior

Every new API inherits D11 without modification.

| Situation                       | Required behavior                                                   |
| ------------------------------- | ------------------------------------------------------------------- |
| Exact connected Player          | Resolve and operate                                                 |
| Disconnect occurs after lookup  | Operation re-resolution fails                                       |
| Same Steam/User ID reconnects   | Old proxy never retargets                                           |
| Old proxy retained in a table   | Still bound to old token/identity/connection/domain                 |
| Queued callback holds old proxy | Revalidates when callback actually runs                             |
| Addon/root domain retires       | Host-backed operations through its Player proxy fail                |
| Provider replaces addon         | Old provider/domain proxy remains stale                             |
| Complete VM recovery            | Old VM's proxies disappear; reconstructed domains create fresh ones |
| CarbonLuau unload/reload        | No new plugin instance accepts old tokens                           |

New world-state reads **do not gain Name/UserId-style disconnected snapshots**.

Thus after disconnect:

```lua
Player.Name       -- existing bounded identity snapshot remains legal
Player.UserId     -- existing identity snapshot remains legal
Player.IsConnected -- false

Player.Position    -- error
Player.Health      -- error
Player.MaxHealth   -- error
Player:CountItem(...) -- error
Player:HasItem(...)   -- error
Player:Teleport(...)  -- error
```

`Vector3` is different: it is an ordinary immutable value with no Player/domain ownership.

---

# 13. Threading and reentrancy analysis

All Player Interaction Foundation 1 host access remains owner-thread-only.

## Read paths

`Position`, health reads, and physical inventory traversal should be implemented as direct bounded reads that do not intentionally invoke plugin hooks.

That is particularly important for inventory: calling the hook-overrideable `GetAmount` would unnecessarily turn an observation into a synchronous plugin-callback boundary.

## Teleport

Teleport cannot be assumed callback-free.

Dismounting, parenting, movement, networking, trigger state, and surrounding host systems may invoke Carbon/Rust hooks or plugin behavior synchronously. Current Rust source exposed through Carbon repeatedly invokes `Interface.CallHook` inside these lifecycle paths; the health/wound examples show the general model clearly.

The CarbonLuau rule remains:

> A Rust/Carbon callback caused synchronously by a CarbonLuau operation may never recursively enter the active Luau VM.

CarbonLuau-owned callbacks must be copied into bounded host payloads and admitted later through the existing scheduler.

Other plugins may synchronously mutate Rust state while their hooks execute. CarbonLuau does not claim to transactionally isolate itself from them.

No change to I4 is warranted.

---

# 14. Provisional/publication classification

| Surface                    | Classification             | During provisional candidate/module execution |
| -------------------------- | -------------------------- | --------------------------------------------- |
| `Vector3.new` / arithmetic | Pure Luau value            | **Allowed**                                   |
| `Player.Position`          | Host observation           | **Allowed**                                   |
| `Player.Health`            | Host observation           | **Allowed**                                   |
| `Player.MaxHealth`         | Host observation           | **Allowed**                                   |
| `Items:Exists`             | Host metadata observation  | **Allowed**                                   |
| `Player:CountItem`         | Host observation           | **Allowed**                                   |
| `Player:HasItem`           | Host observation           | **Allowed**                                   |
| `Player:Teleport`          | Irreversible host mutation | **Rejected**                                  |
| Health mutation            | Deferred                   | Not exposed                                   |
| `TakeItem`                 | Deferred                   | Not exposed                                   |
| `GiveItem`                 | D13 deferred               | Not exposed                                   |

Teleport follows the established D10 pattern used for `SendMessage`, except that teleport is clearly game-state mutation.

A dependency call cannot launder it. If provisional domain B calls active domain A and A ultimately attempts to teleport through its own captured facade, the admitted operation remains provisional and the teleport still fails.

---

# 15. Error and return-value conventions

Keep ordinary code simple.

| API                      | Misuse                                               | Ordinary absence/state                                 | Host/stale failure              |
| ------------------------ | ---------------------------------------------------- | ------------------------------------------------------ | ------------------------------- |
| `Vector3.new`            | Error                                                | —                                                      | —                               |
| `Vector3` arithmetic     | Error for wrong operand/divide-zero/nonfinite result | —                                                      | —                               |
| `Position`               | —                                                    | —                                                      | Controlled error                |
| `Health` / `MaxHealth`   | —                                                    | —                                                      | Controlled error                |
| `Items:Exists(name)`     | Malformed/noncanonical name errors                   | Unknown canonical name → `false`                       | Host failure errors             |
| `CountItem(name)`        | Malformed/noncanonical name errors                   | Unknown/name absent → `0`                              | Stale/bound/host failure errors |
| `HasItem(name, amount?)` | Malformed name or invalid amount errors              | Unknown/insufficient → `false`                         | Stale/bound/host failure errors |
| `Teleport(position)`     | Wrong/nonfinite Vector3 errors                       | Ineligible Player state → controlled operational error | Stale/host failure errors       |

No result wrappers:

```lua
{ success = false, reason = ... }
```

No exception objects.

No Rust enums leaked into Luau.

For operations where authors expect races—especially stale Player state—`pcall` remains the normal mechanism.

---

# 16. Bounds and resource model

## Vector3

Canonical:

* finite components;
* each component must be convertible to finite Unity `float`;
* arithmetic result must satisfy the same condition.

No arbitrary ±32K world limit.

No automatic terrain clamp.

## Item names

Canonical:

* 1–128 ASCII bytes;
* lowercase canonical form;
* no NUL.

This is an API safety/identity bound, not a Rust stack-size policy.

## Amount

`HasItem`:

```lua
Amount == nil -> 1
```

Otherwise require an integer:

```text
1 <= Amount <= 2^53 - 1
```

Using Luau's exact-integer range avoids an arbitrary small gameplay limit.

## Count accumulation

Accumulate with checked integer arithmetic and require the public result to remain within Luau's exact integer range.

## Inventory traversal

The traversal **must have a hard inspected-stack/work limit** and fail closed if host/modded inventory exceeds it.

I would not encode a speculative numeric value such as 64 or 256 into D18. The exact default should be selected from inspection of the supported host's actual inventory/container maxima plus a substantial safety margin and recorded in `Compatibility`/`FacadePolicy`.

That is host-version tuning, whereas the canonical invariant is:

> inventory observation is bounded; it never performs unbounded recursive traversal.

Nested containers are excluded, which also keeps this bound straightforward.

## Diagnostics

Continue existing bounded facade diagnostics. No host exception/object text leaks.

---

# 17. Roblox-like semantics versus Rust reality

| CarbonLuau author semantic | Rust/Carbon implementation reality                                        | Intentional difference from Roblox                                     |
| -------------------------- | ------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| `Vector3.new(x,y,z)`       | Adapted to Unity float coordinates at host boundary                       | Smaller Foundation 1 math surface                                      |
| `Player.Position`          | `BasePlayer` root world transform snapshot                                | Not a Roblox Character/PrimaryPart/CFrame hierarchy                    |
| `Player:Teleport(v)`       | Dismount/parent/anti-cheat/fall/client/network handling hidden underneath | Teleport restricted to qualified Player states; no CFrame/rotation yet |
| `Player.Health`            | Live `BasePlayer` combat health                                           | Player has no Humanoid object                                          |
| `Player.MaxHealth`         | Dynamic host `MaxHealth()` semantics                                      | Can be host/mod overridden                                             |
| No health setter           | Rust wound/death/damage lifecycle is not plain property assignment        | Deliberate deviation from Roblox's writable Humanoid health model      |
| `CountItem("scrap")`       | Bounded scan of physical main/belt/wear stacks                            | Rust inventories are not Roblox Instance containers                    |
| `HasItem("scrap", 100)`    | Convenience threshold over physical inventory                             | Rust short names are game-specific                                     |
| `Items:Exists("rifle.ak")` | Rust `ItemDefinition` identity lookup behind facade                       | No Roblox analogue; Rust-specific service                              |
| No `TakeItem` yet          | Rust removal is hookable and sequential/partial                           | Correctness beats superficial symmetry                                 |
| No `GiveItem` yet          | D13 lacks safe ownership/abort boundary                                   | Correctness beats superficial symmetry                                 |

This is the right interpretation of "Roblox-like": familiar **shape**, not fabricated Roblox behavior.

---

# 18. Exact accepted public API matrix

| Public API                           | Return                |   Mutable? | Provisional? |                  D11-bound? |
| ------------------------------------ | --------------------- | ---------: | -----------: | --------------------------: |
| `Vector3.new(X,Y,Z)`                 | `Vector3`             |         No |          Yes |                          No |
| `Vector3.X/Y/Z`                      | number                |         No |          Yes |                          No |
| `Vector3.Magnitude`                  | number                |         No |          Yes |                          No |
| Vector3 equality/arithmetic          | `boolean` / `Vector3` |         No |          Yes |                          No |
| `Player.Position`                    | `Vector3`             |         No |          Yes |                         Yes |
| `Player:Teleport(Position)`          | no values             | Host state |       **No** |                         Yes |
| `Player.Health`                      | number                |         No |          Yes |                         Yes |
| `Player.MaxHealth`                   | number                |         No |          Yes |                         Yes |
| `Player:CountItem(ShortName)`        | integer number        |         No |          Yes |                         Yes |
| `Player:HasItem(ShortName, Amount?)` | boolean               |         No |          Yes |                         Yes |
| `Items:Exists(ShortName)`            | boolean               |         No |          Yes | Domain facade validity only |

That is the complete Foundation 1 scripting surface.

---

# 19. Exact deferred/rejected APIs

**Deferred from Foundation 1:**

```lua
-- Vector/math expansion
Vector3.Unit
Vector3:Dot(...)
Vector3:Cross(...)

-- Position expansion
Player.Velocity
Player.Rotation
Player.CFrame
Player:GetTransform()

-- Health mutation/state
Player.Health = ...
Player:SetHealth(...)
Player:Heal(...)
Player:Damage(...)
Player.IsWounded
Player.IsDead

-- Inventory mutation/richer inspection
Player:TakeItem(...)
Player:GiveItem(...)
Player:GetInventory()
Player:GetItems()
Player.MainInventory
Player.Belt
Player.Wear

-- Host identities
Item
ItemDefinition
ItemId
BasePlayer
BaseEntity
UnityEngine.Vector3
```

`GiveItem` is specifically **D13 deferred**, rather than merely "not in this phase."

---

# 20. Compatibility with the current Player API

The design is additive.

Existing semantics remain untouched:

```lua
Player.Name
Player.UserId
Player.IsConnected
Player:SendMessage(...)
Player:HasPermission(...)
```

D11 remains the identity contract.

There is no reason to introduce another proxy type.

## Existing bootstrap/value architecture

The current bundled facade already has the right mechanism for `Vector3`: immutable project-owned userdata backed by private records and frozen metatables.

`Vector3` should extend that mechanism rather than inventing managed host handles for a value type.

## Private host bridge

The current private host primitive only accepts operation codes through the existing GUI range. New Player reads/mutation will require new private host operation IDs or equivalent internal dispatch.

That is an implementation detail, but it means the likely implementation touches the native private facade bridge. It does **not** inherently require a public native ABI bump: no external ABI export/layout needs to change merely because the bundled managed/native facade protocol gains private operation codes.

It **does** mean final native-source qualification/sanitizer coverage is appropriate.

## Scripting API identity

If this lands while package `0.4.0` / scripting API `0.4.0-experimental` remains unreleased, the changes are additive and can remain under that candidate identity under the existing D12 policy.

If `0.4.0` is released first, version assignment must be revisited at implementation closure rather than silently changing a released experimental contract.

## D13 compatibility

D18 needs one explicit amendment:

> D13's GiveItem ownership finding and mutation deferral remain canonical. D18 supersedes only D13's earlier product-scope exclusion of read-only `Items`/item observation.

That prevents the register from simultaneously saying "`Items` is wholly deferred" and "`Items:Exists` is accepted."

---

# 21. Proposed canonical D18 text

I would put approximately this into `docs/Invariants.md`:

> **D18 — resolved Player Interaction Foundation 1:** CarbonLuau exposes a reusable immutable `Vector3` value with finite host-representable X/Y/Z components, exact value equality, Magnitude, addition/subtraction, unary negation and scalar multiplication/division. Exact-connection Player proxies add read-only live `Position`, `Health`, `MaxHealth`, `CountItem(shortName)` and `HasItem(shortName, amount?)`; `Items:Exists(shortName)` provides bounded item-definition validation. Position is the BasePlayer root world position. Inventory observation is bounded, nonrecursive physical observation of top-level main, belt and wear containers using canonical lowercase Rust short names; no host item/container object or numeric item ID enters Luau. These observations revalidate D11 and are permitted during provisional execution.
>
> `Player:Teleport(Vector3)` is the sole Foundation 1 gameplay mutation. It is an irreversible committed-only host effect under D10: it revalidates the exact Player, requires an alive non-spectating non-wounded/incapacitated player, preserves sleeping state, dismounts/detaches parenting as required, and uses the qualified Rust adapter's anti-cheat/fall/network convergence path. It does not perform safe-position search, terrain clamping or path-trigger simulation, returns no value, provides no client acknowledgement, and is never treated as transactionally rollbackable.
>
> Health mutation, `TakeItem`, richer inventory/container APIs and raw host objects remain deferred. `GiveItem` remains deferred under D13; D18 reopens only read-only item identity/observation and does not weaken D13's item ownership/cleanup requirements. All operations preserve I4/D7/D10/D11 owner-thread, non-reentrancy, publication, deadline, exact-connection and domain-lifetime rules.

That is concise enough for the canonical register while being strong enough that implementation does not have to invent product semantics.

---

# 22. Implementation phases

I would split implementation differently from the tentative sequence in the request, primarily to isolate Teleport's host/client risk.

### Player-1A — `Vector3` + Position

Implement:

```lua
Vector3
Player.Position
```

Qualify immutable values, operators, finite handling, world-space conversion, exact Player lifetime, cross-domain sharing, and provisional reads.

### Player-1B — Health observation

Implement:

```lua
Player.Health
Player.MaxHealth
```

No mutation.

Qualify dead/wounded/sleeping states and dynamic MaxHealth.

### Player-1C — Item identity + inventory observation

Implement:

```lua
Items:Exists(...)
Player:CountItem(...)
Player:HasItem(...)
```

Qualify canonical short names, main/belt/wear semantics, bounded physical traversal, invalid items, large stack/container fixtures, stale Player behavior, and absence of hook-mediated count virtualization.

### Player-1D — Teleport

Implement only the accepted:

```lua
Player:Teleport(Vector3)
```

This phase owns exact target-build host inspection, dismount/parent behavior, anti-cheat/fall handling, networking, stale races, provisional rejection, reentrancy, and real-client convergence.

### Player-1E — Combined lifecycle/public closure

Run replacement/recovery/provider/stress/regression matrices with every accepted Player-1 surface enabled together.

Update API docs, Compatibility, examples, D13/D18 cross-reference, API identity mapping, and release evidence.

There is **no Player-1 TakeItem phase**. That API remains deferred until a later architecture task supplies a defensible contract.

---

# 23. Qualification plan

## Controlled model/managed qualification

Test all new operations against deterministic host adapters for:

* success;
* malformed arguments;
* stale exact identity;
* disconnect between lookup and use;
* same-UserId reconnect;
* foreign-domain retained Player proxy;
* owner-domain retirement;
* root replacement;
* addon replacement;
* provider loss/restoration;
* VM reconstruction;
* CarbonLuau teardown.

## `Vector3`

Test:

* finite boundary values;
* NaN and ±infinity;
* host-float overflow;
* equality;
* negative zero behavior;
* all approved arithmetic;
* scalar errors;
* division by zero;
* arithmetic overflow;
* immutability;
* shared-module passage across domains.

## Position/health

Test against live Rust:

* normal player;
* mounted;
* parented;
* sleeping;
* wounded;
* dead-but-still-host-valid where observable;
* disconnect/reconnect;
* dynamic MaxHealth override if the target host permits it.

No authenticated client is required merely to prove server-observed Position/Health reads.

## Inventory observation

On both target platforms:

* main only;
* belt only;
* wear only;
* distribution across all three;
* multiple stacks;
* condition/broken items;
* unknown canonical short name;
* malformed short name;
* removal-pending/invalid entries;
* nested-container contents remain excluded;
* supported maximum normal inventory;
* deliberately oversized/modded fixture trips the bounded-work gate;
* `HasItem` threshold short-circuits correctly;
* current Carbon inventory-count hook cannot rewrite CarbonLuau's physical count semantics.

## Teleport host tests

Exercise:

* short-distance teleport;
* long-distance/network-group teleport;
* mounted player;
* parented player;
* sleeping player;
* wounded rejection;
* incapacitated rejection;
* dead rejection;
* spectating rejection;
* disconnect immediately before operation;
* disconnect during host sequence where injectable;
* invalid/nonfinite destination;
* in-air destination;
* edge-of-world destination;
* destination inside geometry without CarbonLuau silently relocating it;
* inherited fall state;
* movement/estimated-velocity state;
* repeated teleports;
* reload/recovery immediately following a completed teleport.

## Teleport reentrancy

Install controlled Carbon hooks around relevant host operations and prove:

* they may execute synchronously as host/plugin code;
* they cannot recursively enter Luau;
* any CarbonLuau event they induce is queued;
* queued work cannot bypass D10 provisional restrictions;
* a callback cannot resurrect a stale Player.

## Provisional tests

Every read must succeed when its Player/domain is otherwise valid.

Teleport must fail from:

* root candidate initialization;
* addon candidate initialization;
* first-load module initialization beneath either;
* provisional dependency call chains.

Then demonstrate:

```lua
task.defer(function()
    Player:Teleport(...)
end)
```

runs only after successful publication and does not run for a failed candidate.

## Windows/Linux

The actual implementation target must be inspected and exercised on both supported platforms. Existing old host evidence is not automatically new-version qualification.

## Sanitizers

If, as expected, the private native host-operation gate changes to accommodate additional operation IDs, rerun the relevant native unit/sanitizer coverage.

## Authenticated real-client gate

**Required for Teleport.**

A server-side transform update alone does not prove the public semantic.

The gate should establish, using a real authenticated Rust client:

* client reaches the intended destination;
* no immediate rubber-band to the old position;
* long-distance network-group transition converges;
* no accidental fall damage attributable to stale pre-teleport state;
* mount/parent state converges;
* repeated teleports remain stable.

This client gate applies specifically to Teleport. It should **not** block Position, Health, item identity, or inventory observation.

---

# 24. Genuinely unresolved host-dependent gates

There are only three that should remain after architecture adoption.

### A. Exact target-build Teleport sequence

Current evidence proves that robust relocation involves more than position assignment, but the precise sequence must be established against the actual Rust build CarbonLuau will qualify.

Current Rust's portal path and current Oxide's generic player adapter do not use identical sequences.

This is an adapter qualification problem, **not** an unresolved public-API design question.

### B. Authenticated-client Teleport convergence

This cannot be proven from managed/server state alone.

Until it passes, `Player:Teleport` must not be claimed as qualified public behavior.

Again, the architecture is resolved; the implementation has an acceptance gate.

### C. Exact host-layout verification for deterministic inventory reads

The implementation needs to verify, on the chosen target Rust build, that direct bounded inspection of main/belt/wear can produce the accepted physical-inventory semantic without invoking override hooks or unstable host callbacks.

If host changes make that impossible, **Player-1C is deferred rather than changing its API meaning**.

Nothing else needs to be returned as an architecture question.

---

# 25. Final verdict

## **READY FOR CANONICAL ADOPTION**

D18 can now be adopted as the Player Interaction Foundation 1 architecture.

The coherent Foundation 1 surface is:

```lua
Vector3

Player.Position
Player:Teleport(...)

Player.Health
Player.MaxHealth

Player:CountItem(...)
Player:HasItem(...)

Items:Exists(...)
```

with a deliberately asymmetric mutation policy:

* **Teleport is accepted** because CarbonLuau can hide the complicated but conventional player-relocation procedure behind one author-facing operation, with real-client convergence as a hard implementation gate.
* **Health mutation is deferred** because Rust's damage/wound/death/healing semantics are meaningfully richer than assigning a number.
* **TakeItem is deferred** because current removal semantics are hook-overridable and sequential/partially mutating, so neither boolean atomicity nor an uncomplicated removed-count contract is defensible.
* **GiveItem remains D13-deferred** because no new evidence establishes an every-path ownership/commit/cleanup contract.

The important architectural direction is that CarbonLuau is not becoming a thin Rust binding. A script author sees `Player.Position`, `Player.Health`, `Player:Teleport(...)`, and short-name inventory queries. `BasePlayer`, `PlayerInventory`, `ItemDefinition`, network RPCs, anti-cheat toggles, mount handling, and container lists remain implementation details where they belong.

[1]: https://github.com/OxideMod/Oxide.Rust/blob/develop/src/Libraries/Player.cs "Oxide.Rust/src/Libraries/Player.cs at develop · OxideMod/Oxide.Rust · GitHub"
[2]: https://commits.facepunch.com/r/rust_reboot/main/modding_max_hp?utm_source=chatgpt.com "Facepunch Commits"
[3]: https://docs.unity3d.com/cn/6000.0/ScriptReference/Space.html?utm_source=chatgpt.com "Unity - Scripting API: Space"
[4]: https://carbonmod.gg/references/items/ "Items Reference | Carbon"
