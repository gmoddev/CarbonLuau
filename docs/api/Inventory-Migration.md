# PlayerInventory migration (development API 0.7)

Development API `0.7.0-experimental` makes an explicit breaking inventory change
before the next package release. Player owns acquisition; PlayerInventory owns
reads, mutations and observation. The last published package remains `0.5.0` with
API `0.5.0-experimental`. Updated examples require the development runtime.

## Removed methods and replacements

The legacy methods are deprecated in this migration record and removed from the
current Player public surface. There is no compatibility/deprecation grace alias.

| Removed call | Current call |
|---|---|
| `Player:CountItem(ShortName)` | `Player:GetInventory():CountItem(ShortName)` |
| `Player:HasItem(ShortName, Amount?)` | `Player:GetInventory():HasItem(ShortName, Amount?)` |
| `Player:GiveItem(ShortName, Amount, Behavior?)` | `Player:GetInventory():GiveItem(ShortName, Amount, Behavior?)` |
| `Player:TakeItem(ShortName, Amount)` | `Player:GetInventory():TakeItem(ShortName, Amount)` |

Existing C1/C3 `Inventory:CountItem` and `Inventory:HasItem` names are retained.
The final Inventory surface is HasItem, CountItem, GiveItem, TakeItem and
ItemChanged. The four removed Player methods have no compatibility aliases;
no shortened Inventory method names are introduced.
RemoveItem was not an existing public method to migrate. GetItem/GetItems,
mutable Item objects and a global Inventory service are not introduced.

```luau
local Inventory = Player:GetInventory()
local Scrap = Inventory:CountItem("scrap")
local CanPay = Inventory:HasItem("scrap", 100)

-- Execute mutations only from committed callbacks or post-publication deferred work.
local CallOk, Granted = pcall(function()
    return Inventory:GiveItem("scrap", 10, GiveItemBehavior.InventoryOnly)
end)
if not CallOk then
    print("[Inventory:Migration] Call failed; reconcile host state before retrying", Granted)
elseif not Granted then
    print("[Inventory:Migration] No grant began")
end

local Connection = Inventory.ItemChanged:Connect(function(Context)
    print("[Inventory:Sample]", Context.Item, Context.Before, Context.After, Context.Delta)
end)
-- Connection:Disconnect() when no longer needed.
```

## Preserved behavior

HasItem defaults omitted/nil Amount to 1 and accepts exact integers 1..`2^53 - 1`.
GiveItem/TakeItem require exact integer Amount 1..`Int32.MaxValue`. GiveItem defaults omitted/nil
Behavior to the unchanged typed `GiveItemBehavior.InventoryOnly`; no other grant
mode or boolean DropIfFull is accepted. Canonical unknown names return 0/false for
CountItem/HasItem and false before COMMIT for TakeItem, but raise a configuration error for GiveItem.

Reads reuse the bounded direct main/belt/wear scanner. Mutation uses the same D13
PREPARE/COMMIT/VERIFY backend and one original-connection gate across all domains.
False guarantees no inventory COMMIT began; true requires physical verification;
an uncertain failure after COMMIT raises a controlled error and may leave changes.
No intentional world-drop fallback, rollback, automatic retry or transaction is
added. Separate payment and grant remain separate irreversible operations.

Facades retain the original D11 connection, defining domain, publication witness
and VM authority. Reconnect by the same UserId never retargets an old facade.
Provisional initialization/cold modules cannot mutate; failed publication discards
staged work. Replacement, provider unload and recovery invalidate old authority.

ItemChanged keeps C3 sampled Before/After/Delta semantics. Intermediate transfers
can appear and balanced changes between samples can be missed. Mutation does not
force immediate delivery or identify a cause. Callbacks remain deferred and bounded.
See [PlayerInventory](Types/PlayerInventory.md) for full signatures, limits and errors.

## Versions and qualification

The development API advances from `0.6.5-experimental` to `0.7.0-experimental`
because removing the Player methods is breaking under D12. Existing declarations retain
their recorded SinceApi introductions, including GetInventory, CountItem/HasItem and
ItemChanged at `0.6.5-experimental`, and GiveItemBehavior at `0.4.0-experimental`.
New `PlayerInventory:GiveItem` and `PlayerInventory:TakeItem` methods are introduced at
`0.7.0-experimental`. Introduction metadata is not a promise that removed spellings
remain callable or that qualification applies to every later revision.

Development package/release/tag remain `0.5.0`/`v0.5.0`, ABI `1.5`, provider `1.2`,
addon schema `1` and the pinned Luau revision unchanged. No C ABI boundary change
is inferred from this API migration. Development dry-run artifacts must never
replace published v0.5.0 artifacts. The [published release notes](../releases/0.5.0.md)
and historical qualification records retain their original names and evidence.

[C4 qualification](../GameplayC4-Validation.md) records the completed migration gates.
[Release readiness](../Release.md#gameplay-ad-release-readiness) distinguishes prior
Gameplay A–D qualification from the C4 final-source runtime/metadata/generated
definition absence, Windows/Linux real-VM/live, regressions/sanitizers, bundled
examples, tooling, packaging, clean-install and hosted CI receipts. Qualification
does not authorize publication; release-identity/artifact preparation is separate.
