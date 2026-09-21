# Player

Availability: core proxy in experimental API `0.3.0-experimental`; Position,
Health, MaxHealth, CountItem, HasItem, Teleport and TakeItem are added in `0.4.0-experimental`. Obtained from Players,
Signals or CommandContext; there is no constructor. A frozen proxy represents
one connection in one generation, never a BasePlayer or a transferable host handle.

| Signature / property | Return | Behavior |
|---|---|---|
| `Player.Name` | read-only string | Current bounded host display name while connected; creation-time name snapshot when disconnected. |
| `Player.UserId` | read-only string | Decimal account ID, never a floating-point number. |
| `Player.IsConnected` | read-only boolean | Fresh host resolution and connection identity check. False when the connection is gone/replaced. |
| `Player.Position` | read-only [Vector3](Vector3.md) | Fresh exact-connection root world position. Stale/disconnected access errors; no snapshot is retained. |
| `Player.Health` | read-only number | Fresh exact-connection host health. The finite value is not clamped to MaxHealth. |
| `Player.MaxHealth` | read-only number | Fresh exact-connection current host maximum health, including host overrides/modifiers. It is not assumed to be 100. |
| `Player:CountItem(ShortName: string)` | number | Exact checked physical quantity across top-level main, belt and wear stacks. |
| `Player:HasItem(ShortName: string, Amount: number?)` | boolean | Whether that physical quantity reaches Amount; omitted Amount is 1. |
| `Player:TakeItem(ShortName: string, Amount: number)` | boolean | Removes and verifies the exact physical quantity. Committed execution only. |
| `Player:Teleport(Position: Vector3)` | no values | Moves this exact live Player to the requested raw Rust world coordinate. Committed execution only. |
| `Player:SendMessage(Message: string)` | no values | Sends system chat through Rust `BasePlayer.ChatMessage`; 1024 UTF-8 bytes maximum, no NUL. The command name/channel is host-fixed, not script-controlled. |
| `Player:HasPermission(Permission: string)` | boolean | Fresh Carbon permission query for this live connection; no permission mutation. |

Every operation revalidates identity, including property access. Position,
Health, MaxHealth, CountItem and HasItem are bounded direct observations allowed during provisional
initialization. Sleeping, wounded, dead-but-host-valid and mounted/parented
host-valid players remain readable;
mounted/parented reads use the root Transform's world position. Each read returns
a new immutable value that remains usable after later movement or disconnect.
After disconnect, Name/UserId remain safe snapshot values; Position, Health,
MaxHealth, CountItem, HasItem, TakeItem, Teleport, SendMessage and HasPermission raise
`Player is no longer connected`. Same-account reconnect creates a different proxy.
Generation retirement destroys VM-local script references and rejects late host work.
Once host invalidity is observed, the old token stays invalid even if the host
reuses an object; a newly observed connection receives a new lifetime.

Inventory observation scans only direct entries in the exact Player's top-level
main, belt and wear containers. It ignores nested contents, backpacks, external
storage, world items, corpses and plugin-defined storage. A stack counts only
when it is current, has positive amount, matches the exact item definition, is
not removal-pending, and points back to the accepted container whose list holds
it. The scanner never invokes hook-virtualized inventory count APIs.

ShortName follows the [Items service](../Services/Items.md) rules. A canonical
unknown name returns `0` or `false`. Explicit Amount must be an exact positive
integer no greater than `2^53 - 1`; it is never rounded. At most 128 direct
entries are admitted per observation across all three containers. One over the
bound fails closed before returning a partial answer. Accumulation is checked
and must remain an exact Luau integer.

Malformed receivers, attempted writes, wrong input types, invalid permission names,
oversized/NUL input and host failure raise controlled errors. Unknown properties
return nil. Permission names are 1–128 ASCII bytes, start with a lowercase letter,
contain lowercase letters/digits/underscore/hyphen and nonempty dot-separated
segments, and include a namespace dot. HasPermission itself requires no permission.

SendMessage needs a live player but no additional permission: it cannot grant
items, run arbitrary commands or change administrator state. **It is prohibited
during provisional initialization**, including reload/recovery entrypoints and
modules they require. Use deferred work for startup messages:

```lua
local Players = game:GetService("Players")
task.defer(function()
    local Player = Players:GetPlayers()[1]
    if Player then
        print(Player.Name, Player.UserId, Player.IsConnected)
        print(Player:HasPermission("carbonluau.example.hello"))
        local Ok, Error = pcall(function() Player:SendMessage("Hello") end)
        if not Ok then print(Error) end
    end
end)
```

Failed candidates never deliver their deferred messages. A successful host call
does not acknowledge client receipt; there is no replay deduplication or formatting
sanitization beyond the bounded string contract. `IsConnected` is not a lease:
always allow the later operation to fail. See [compatibility](../Compatibility.md).

Teleport has the same committed-only rule as SendMessage. It requires an alive,
non-spectating, non-wounded and non-incapacitated Player. Sleeping is preserved;
mounted and parented Players are normalized internally. The destination is not
clamped, grounded or made collision-safe. A failure after host mutation starts
means Player state may already have changed and CarbonLuau does not roll it
back. Server-side behavior is qualified; authenticated-client convergence and
rubber-band behavior remain unqualified.

TakeItem is also committed-only. `true` means the host result and one fresh
bounded main/belt/wear scan both prove that the requested quantity was removed.
`false` means PREPARE found an unknown canonical item or insufficient physical
quantity and Rust inventory mutation never began. An error after COMMIT means
inventory may have changed and CarbonLuau could not prove the final result; no
rollback is promised. Amount is required and must be an exact integer from 1
through `Int32.MaxValue`. Use `pcall` when code must handle the indeterminate
host-failure case separately.

```lua
local Removed = Player:TakeItem("scrap", 100)

if Removed then
    print("Removed 100 scrap")
else
    print("Player did not have enough scrap")
end
```

Handle errors separately when needed; do not blindly retry a failed call:

```lua
local CallOk, Removed = pcall(function()
    return Player:TakeItem("scrap", 100)
end)

if not CallOk then
    -- Validation/stale errors can occur before mutation; a host failure after
    -- COMMIT may have changed inventory. Reconcile authoritative state first.
elseif Removed then
    print("Removed 100 scrap")
else
    print("No removal began")
end
```
