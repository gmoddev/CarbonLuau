# Player examples

Inventory examples target development API `0.7.0-experimental` and use
`Player:GetInventory()`; they do not run against the published v0.5.0 inventory
surface. See the [migration guide](Inventory-Migration.md).

Install one example as the root `scripts/init.luau` at a time. Commands are chat
commands; grant their named permissions through Carbon before testing. Reward
examples are deliberately repeatable test grants, not an economy or entitlement
system. Errors remain ordinary script errors handled by the runtime.

| Example | Entry point |
|---|---|
| Position, Health and MaxHealth | [Player status](../../examples/player-status/init.luau), `/playerstatus` |
| Items, Inventory CountItem and HasItem | [Inventory observation](../../examples/player-inventory/init.luau) |
| Inventory TakeItem | [Remove scrap](../../examples/player-take-item/init.luau), `/takescrap` |
| Inventory GiveItem and InventoryOnly | [Command reward](../../examples/player-give-item/init.luau), `/reward` |
| GUI Activated reward | [GUI reward](../../examples/gui/inventory-reward/init.luau) |
| Separate payment and grant | [Shop demonstration](../../examples/player-shop/init.luau), `/buywood` |

Inventory GiveItem/TakeItem return `true` for verified success and `false` only before
mutation begins. An error after mutation begins means state may have changed.
Do not retry blindly. Reads do not reserve inventory, and sequential TakeItem/GiveItem
calls are **not an atomic exchange**. The shop intentionally does not refund the
payment if its later grant fails; it is not a transactional shop implementation.

Cold module initialization cannot mutate, even if called by a committed callback.
A module may return a reward function for later committed calls, or stage
`task.defer` to run after successful publication. Failed initialization discards
that staged work. Existing read-only observations are allowed during initialization.

See [Player](Types/Player.md), [PlayerInventory](Types/PlayerInventory.md),
[GiveItemBehavior](Types/GiveItemBehavior.md) and
[advanced compatibility limits](Compatibility.md). GUI server-side testing is
not evidence of real-client rendering/click receipt; Teleport's authenticated-
client convergence remains unqualified.
