# Player examples

Install one example as the root `scripts/init.luau` at a time. Commands are chat
commands; grant their named permissions through Carbon before testing. Reward
examples are deliberately repeatable test grants, not an economy or entitlement
system. Errors remain ordinary script errors handled by the runtime.

| Example | Entry point |
|---|---|
| Position, Health and MaxHealth | [Player status](../../examples/player-status/init.luau), `/playerstatus` |
| Items, CountItem and HasItem | [Inventory observation](../../examples/player-inventory/init.luau) |
| TakeItem | [Remove scrap](../../examples/player-take-item/init.luau), `/takescrap` |
| GiveItem and InventoryOnly | [Command reward](../../examples/player-give-item/init.luau), `/reward` |
| GUI Activated reward | [GUI reward](../../examples/gui/inventory-reward/init.luau) |
| Separate payment and grant | [Shop demonstration](../../examples/player-shop/init.luau), `/buywood` |

GiveItem/TakeItem return `true` for verified success and `false` only before
mutation begins. An error after mutation begins means state may have changed.
Do not retry blindly. Reads do not reserve inventory, and sequential Take/Give
calls are **not an atomic exchange**. The shop intentionally does not refund the
payment if its later grant fails; it is not a transactional shop implementation.

Cold module initialization cannot mutate, even if called by a committed callback.
A module may return a reward function for later committed calls, or stage
`task.defer` to run after successful publication. Failed initialization discards
that staged work. Existing read-only observations are allowed during initialization.

See [Player](Types/Player.md), [GiveItemBehavior](Types/GiveItemBehavior.md) and
[advanced compatibility limits](Compatibility.md). GUI server-side testing is
not evidence of real-client rendering/click receipt; Teleport's authenticated-
client convergence remains unqualified.
