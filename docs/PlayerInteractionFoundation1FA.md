# Player Interaction Foundation 1F-A

## Verdict

`Player:TakeItem(ShortName, Amount) -> boolean` is implemented using the
revised D13 PREPARE, COMMIT and VERIFY model. It is limited to the exact Rust
build `25353106` and Carbon `2.0.259` adapter qualified by Inventory-M2.
The implementation revision is
`82e940c55287d02830d8f3ed3a4f5142389db164`.

This record does not qualify or implement `Player:GiveItem`, generic inventory
objects, rollback, multi-item transactions, Player-1F-B or Player-1F-C.

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

## Identity and remaining scope

This additive method keeps package `0.4.0`, scripting API
`0.4.0-experimental`, native ABI `1.4`, provider protocol `1.0`, package schema
`1` and Luau revision `c6b830185af962c82003f86784e2fe036357c830` unchanged.
`GiveItem`, Player-1F-B and Player-1F-C remain deferred.
