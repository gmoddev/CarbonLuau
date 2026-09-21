# Scripting compatibility and limits

Identity: `CarbonLuau`, API `0.4.0-experimental`, status `Experimental`.
Scripts inspect `game.ApiName`, `game.ApiVersion`, `game.ApiStatus`; operators use
`carbonluau.status`. This identity is distinct from package `0.4.0`, native ABI
`1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1`, the
pinned Luau revision and the installed Rust/Carbon builds. Published v0.3.0
artifacts remain API `0.3.0-experimental` with native ABI `1.2`.

The unreleased 0.4.0 candidate combines the additive addon, GUI Foundation 1 and
implemented GUI Foundation 2 layout/image/scrolling surfaces under this one
experimental identity. Authenticated-client visual,
cursor, scrolling, clipping, image-load, click-receipt and reconciliation behavior remains unqualified and is not
implied by API availability.

Foundation 2 deterministic layout, typed images and retained scrolling are
qualified for experimental public release by Foundation 2F. `TextBox` is not
implemented and `Submitted` is not implemented because the inspected Rust InputField command
path trims submitted text before CarbonLuau receives it. The exact D16 text
contract was not weakened into console-argument semantics.

The canonical compatibility policy lives in [Compatibility.md](../Compatibility.md)
and decisions D8/D12. Additive means preserving existing contracts while adding
names/operations. Removing/renaming APIs or changing types, lifetime, failure or
authorization behavior is breaking and requires an explicit version, documented
change and migration decision. Experimental does not permit silent breaking changes.
No automatic version negotiation, long-term deprecation window or Roblox contract
is promised. Consult [current qualification](../Phase3-Validation.md).

D13 continues to defer unshipped `Player:GiveItem`, `TakeItem` and item mutation.
[D18](../Invariants.md#d18--player-interaction-foundation-1) now approves bounded
read-only item identity/inventory observation for future scoped implementation,
but none of that Player Interaction Foundation 1 surface is currently shipped.
This policy change removes no implemented API and changes no package, scripting
API or native ABI identity.

| Resource | Bound |
|---|---|
| Connected-player population / snapshot | 1024; larger host population fails closed |
| Player name / user ID | 128 UTF-8 bytes / 20 ASCII decimal digits |
| Listeners | 128 per signal, 256 per generation |
| Commands | 64 per generation; initialization-only |
| Command / permission name | 32 / 128 ASCII bytes |
| Description / message | 256 / 1024 UTF-8 bytes |
| Command arguments | 16; 512 bytes each, 4096 bytes total |
| Pending managed event deliveries | min(configured queue capacity, 256), at most 16 KiB each |
| Native ready/delayed callbacks | Existing configured maximum, up to 4096; shared by tasks and admitted facade work |
| Event ingestion per frame | Up to 64, within the existing frame-drain stopwatch |
| New permission metadata | 256 names per plugin lifetime |
| Host command-registry inspection | 16384 existing total chat/client-console/RCON entries |
| Addon archive / expanded bytes | 4 MiB / 8 MiB |
| Addon manifest / each source / package source total | 64 KiB / 64 KiB / 4 MiB |
| Addon modules / archive entries / dependencies | 256 / 512 / 32 |
| Addon registrations / one provider | 128 / 32 |
| Aggregate immutable addon snapshots | 32 MiB |
| Compiler request / response / wall time | 64 KiB / 1 MiB / 1 second |
| GUI objects per ScreenGui / depth / children | 128 / 16 / 64 |
| GUI objects / ScreenGuis per domain; GUI objects global | 1,024 / 32; 8,192 |
| Screens per Player connection / viewers per ScreenGui | 16 / 256 |
| GUI presentations per domain / global | 512 / 4,096 |
| Interactive buttons per ScreenGui / Activated listeners per button | 64 / 8 |
| Authoritative projected elements per ScreenGui | 257 |
| ScrollingFrame projected-element charge | 7 |
| Effective Frame/ScrollingFrame clip depth | 4 |
| Explicit clipping Frame projected-element charge | 1 additional private element |
| Image sprite / decimal identifier | 256 UTF-8 bytes / 20 ASCII digits |
| GUI Signal connections per domain | 256 |
| GUI Name / Text / aggregate screen text | 64 B / 2,048 B / 32 KiB UTF-8 |
| GUI clone objects / depth | 128 / 16 |
| GUI interactions per Player / per action | 20/s burst 20 / 8/s burst 8 |

NUL is rejected in API strings; malformed UTF-8 is rejected by host transport.
Wrong types are not implicitly coerced. Registration/input failures raise script
errors. Host intake overload drops/rejects new deliveries; it does not allocate
unbounded work. Permission and lifetime rejection before Lua means no user callback.

The native callback queue and managed intake are separately bounded, not one shared
memory cap. Source ingestion, native host-response storage (256 KiB per facade VM),
compiler memory and managed/Carbon resources are outside the Lua VM heap cap.
The compiler runs in a killable sibling process with a production 256 MiB process
limit; timeout or invalid protocol response discards the worker and starts clean
on the next compilation request.
Deadlines remain cooperative; a bounded host operation can exceed a frame budget.
No multi-tenant, real-time, client-delivery or exactly-once replay guarantee exists.

GUI serialization size, flush CPU/byte/send budgets and full-reconciliation
checkpoints are internal bounded tuning values rather than public scripting API
promises. See the [GUI guide](Gui.md) for observable semantics and unsupported
features.
