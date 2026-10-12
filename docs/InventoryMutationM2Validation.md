# Inventory-M2: retained exact-build evidence

The original G1 PASS conclusion is superseded and is not restored by this page.
G2–G5 evidence remains part of the inventory qualification basis. The complete
[original record](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation)
preserves its prior G1 argument, raw fixtures and adoption chronology.

[I12/D13](invariants/Runtime.md) and [D18](invariants/Player.md) remain canonical.
Supported-host GiveItem requalification and TakeItem implementation are in the
[Player ledger](qualification/PlayerInteraction.md); [C4](GameplayC4-Validation.md)
owns the current PlayerInventory migration. This page changes no contract or pin.

## Retained scope

These observations are for Rust build `25353106` and Carbon `2.0.259` on the
original qualified Windows/Linux assemblies, not universal future-host proof.
The [original provenance table](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation-scope-and-provenance)
retains depot manifests, assembly hashes/MVIDs and the independent static/live split.
Requalification after the callback-to-split/drop finding is separate from this data.

The G2–G5 sections below retain the original wording and evidence. Historical
fixture G1 markers remain explicitly historical; they do not qualify a current
no-drop adapter under arbitrary external mutation.

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
TakeItem remains independently qualified. GiveItem's revised
Player-1F-B requalification and implementation are linked above. PREPARE/COMMIT/VERIFY,
false/true/indeterminate and nontransactional host semantics are unchanged.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="inventory-m2-exact-build-adapter-validation"></a>

- [Inventory-M2 exact-build adapter validation](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation-inventory-m2-exact-build-adapter-validation)

<a id="scope-and-provenance"></a>

- [Scope and provenance](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation-scope-and-provenance)

<a id="static-evidence"></a>

- [Static evidence](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation-static-evidence)

<a id="historical-adapter-evidence-and-current-qualification-limits"></a>

- [Historical adapter evidence and current qualification limits](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation-historical-adapter-evidence-and-current-qualification-limits)

<a id="g1--historical-pass-superseded-later-requalification-recorded-separately"></a>

- [G1 — historical PASS superseded; later requalification recorded separately](history/inventory/InventoryMutationM2Validation.md#inventorymutationm2validation-g1--historical-pass-superseded-later-requalification-recorded-separately)
