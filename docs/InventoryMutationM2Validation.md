# Inventory-M2 exact-build adapter validation

**Date:** 2026-09-21  
**Current disposition:** **Historical G1 PASS conclusion SUPERSEDED;
G1 PENDING REQUALIFICATION under I12. G2-G5 evidence retained.**
The original phase reported G1-G5 PASS before either public mutation existed.
TakeItem was subsequently implemented and qualified by Player-1F-A and remains
unchanged. GiveItem is still unimplemented; this record changes no identity.

**Player-1F-B follow-up and policy adoption (2026-09-21):** Original M2 G1
proof was incomplete: mutable acceptance occurs before a split/drop decision.
The later investigation failed G1 under the previous whole-process guarantee;
CarbonLuau cannot isolate arbitrary trusted mutation across that callback.
[I12](Invariants.md#i12--trusted-in-process-host-interference) now places that
external mutation outside semantic isolation, not outside result verification.
This does not restore the old PASS. The explicit-slot/no-swap/no-ignoreStackLimit
adapter must be requalified under the supported-host model and
[Player-1F-B gate](PlayerInteractionFoundation1FB.md#required-1f-b-requalification)
before implementation resumes. Historical observations below are preserved.

## Scope and provenance

The vanilla Rust Dedicated Server was downloaded anonymously from Steam app
`258550`, public branch, following Facepunch's
[server installation documentation](https://wiki.facepunch.com/rust/Creating-a-server).
SteamRE DepotDownloader `3.4.0` was used after SteamCMD's bootstrap CDN returned
HTTP 403 in this environment. The resulting target is the already selected
Steam build `25353106`.

| Artifact | Depot / manifest | SHA-256 | MVID |
|---|---|---|---|
| Windows `Assembly-CSharp.dll` | `258551` / `7816298056519226227` | `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a` | `9d6ca77d-5a88-476c-8795-38b08e1c03a0` |
| Linux `Assembly-CSharp.dll` | `258552` / `3352454092778561960` | `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b` | `fb72291d-03c0-4d66-834b-1439625b5c80` |
| shared content | `258554` / `4408100835840826754` | n/a | n/a |

The Linux hash is identical to the target already recorded by
[PlayerInteractionFoundation1C.md](PlayerInteractionFoundation1C.md). Carbon
Windows/Linux release `2.0.259`, protocol `2026.09.03.0`, commit
`21063e8490adf412101bcc7d1cfe9d6280f61e80`, was downloaded from the official
[Carbon production release](https://github.com/CarbonCommunity/Carbon/releases/tag/production_build).

ILSpy command-line `8.2.0.7535` was used only in the external build directory.
No decompiled Facepunch source, vendor assembly or Carbon package is stored in
the repository. This record contains independently written semantic findings
and reproducible structural assertions only.

## Static evidence

The historical D13 checker still passes unchanged:

```text
[CarbonLuau:D13] Identical selected Windows/Linux bodies and visibility: 32
[CarbonLuau:D13] Structural checks passed: 106.
```

The additive [Test-InventoryMutationAdapterEvidence.ps1](../tools/Test-InventoryMutationAdapterEvidence.ps1)
compares the mutation adapter surface across the exact Windows/Linux assemblies
and checks the required signatures, observable state, control-flow ordering,
cleanup route and capacities:

```text
[CarbonLuau:InventoryM2] Static adapter evidence passed: 92 checks.
```

The selected Windows/Linux method bodies and visibility are identical for
`ItemManager.Create`, `Item.MoveToContainer`, `Item.SetParent`, `Item.Remove`,
`Item.IsRemoved`, `Item.IsValid`, `Item.GetWorldEntity`,
`ItemContainer.Insert`, `ItemContainer.Take`, `PlayerInventory.ServerInit` and
`PlayerInventory.Take`.

## Historical adapter evidence and current qualification limits

### G1 — historical PASS superseded; requalification pending

The general `PlayerInventory.GiveItem`, `BasePlayer.GiveItem` and unconstrained
`Item.MoveToContainer` paths are not qualified. `MoveToContainer` still has an
oversized-container-stack split branch that may call `Drop`.

The originally proposed qualified path was narrower:

1. PREPARE scans only `containerMain`, `containerBelt` and `containerWear`.
2. PREPARE selects every exact target slot and chunk amount without invoking
   `CanAcceptItem` or another callback-capable host method.
3. A merge chunk is no larger than the observed free space in its exact target
   stack. An empty-slot chunk is no larger than the item stack limit and any
   positive target `maxStackSize`.
4. COMMIT creates one planned chunk and calls
   `MoveToContainer(Container, Slot, AllowStack, false, Player, false)`.
5. The adapter never passes target slot `-1`, never enables
   `ignoreStackLimit`, never enables swap and never supplies an amount that can
   enter the oversized split/drop branch under the qualified baseline. The
   original argument did not account for a callback changing this premise.

For an exact compatible stack, the inspected path adds the bounded amount,
decrements the source, migrates ownership, removes the exhausted source and
returns `true`. For a planned empty slot, it checks acceptance, detaches any old
world/container state and assigns the selected parent. Neither qualified branch
calls `Drop` when its premises hold. That conditional observation is not the
complete revised G1 proof. A host `false` or exception after creation is an indeterminate
post-COMMIT outcome, not ordinary `false`.

### G2 — returned-Item observability: PASS

The exact build exposes enough state for the adapter to classify each returned
Item while retaining its reference:

| State | Required observations |
|---|---|
| Accepted inventory | `parent` is one accepted container; that container's `itemList` contains the same reference; position is in bounds; `IsValid()`; no world entity |
| Consumed merge source | the exact transfer returned `true`; `IsRemoved()`; amount `0`; no parent; no world entity |
| Temporary responsibility | `IsValid()`; no parent; no world entity; positive planned amount |
| Unexpected / indeterminate | every other state, including another container or a world entity |

`ItemContainer.Insert` mutates `itemList` and `Item.parent` before later
callbacks. Consequently a callback exception can leave a physically accepted
Item; VERIFY must inspect state instead of assuming an exception means no
placement.

### G3 — Take verification: PASS

`PlayerInventory.Take(null, itemId, amount)` traverses main, then belt, then
wear and returns the accumulated removed amount. With `collect == null`,
partial stacks use `UseItem`; complete stacks are scheduled for removal and the
host drains its removal queue before returning.

The qualified success predicate remains:

```text
hostReturnedAmount == requestedAmount
AND
physicalBefore - physicalAfter == requestedAmount
```

PREPARE must first prove sufficient bounded physical quantity. Any exception,
host mismatch or physical-delta mismatch after COMMIT is indeterminate.

### G4 — returned unattached cleanup: PASS

The supported request is `Item.Remove(0)`. If it returns normally, the exact
observable state is `IsRemoved() == true`, amount `0`, position `-1`, and the
Item is scheduled in `ItemManager`'s removal queue. For the qualified unattached
case, parent and world entity remain null. Production adapter code must not call
the global `ItemManager.DoRemoves` merely to accelerate cleanup; normal host
heartbeat owns queue draining.

An exception before `Remove` schedules the Item remains an indeterminate host
failure. D13 does not claim universal cleanup against that host-internal case.

### G5 — bounded work: PASS

Vanilla `PlayerInventory.ServerInit` creates capacities main `24`, belt `6` and
wear `8`, total `38`. CarbonLuau's already-qualified modded envelope remains
`128` top-level entries/slots across the accepted containers.

Inventory mutation must reject during PREPARE if any capacity is negative, the
sum of capacities exceeds `128`, an item list exceeds its capacity, or the
bounded scan/plan would exceed `128` entries or transfer chunks. It must not
scan nested containers or backpacks. These checks keep both planning and VERIFY
bounded even when another mod changes vanilla capacities.

## Live Carbon evidence

[CarbonLuau.InventoryMutationM2Evidence.cs](../tests/live/CarbonLuau.InventoryMutationM2Evidence.cs)
ran on an isolated Windows server using the exact Rust build and Carbon
`2.0.259`. It created a controlled real `BasePlayer` and exercised:

- explicit empty-slot transfer with swap disabled;
- exact compatible-stack merge and consumed-source classification;
- callback-free physical before/after counting;
- controlled acceptance rejection, temporary responsibility and cleanup;
- an injected post-insertion delegate exception, proving accepted state remains
  observable after exceptional control flow;
- `PlayerInventory.Take(null, ...)` with matching host and physical deltas;
- the exact vanilla capacity values.

Historical observed result (the G1 marker describes those fixture cases,
not current G1 qualification):

```text
[CarbonLuau:InventoryM2] PASS G1 explicit-slot/no-swap path produced no world entity or drop
[CarbonLuau:InventoryM2] PASS G2 accepted, consumed, temporary-responsibility and exceptional accepted states observable
[CarbonLuau:InventoryM2] PASS G3 Take returned amount matched bounded physical delta
[CarbonLuau:InventoryM2] PASS G4 Remove cleanup reached scheduled/zeroed/unattached terminal state
[CarbonLuau:InventoryM2] PASS G5 vanilla capacities main=24 belt=6 wear=8 total=38
[CarbonLuau:InventoryM2] LIVE CARBON PASS
```

The fixture is excluded from production packaging. The server exited through a
graceful RCON `quit`; no authenticated client was required or used.

## Conclusion

The original blanket G1-G5 PASS conclusion is explicitly superseded. G2-G5
observations on Rust build `25353106` plus Carbon `2.0.259` and the historical
G1 fixture outcomes remain evidence, not a new supported-host G1 PASS.
TakeItem remains independently qualified. GiveItem requires the revised
Player-1F-B requalification gate before implementation. PREPARE/COMMIT/VERIFY,
false/true/indeterminate and nontransactional host semantics are unchanged.
