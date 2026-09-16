# Scripting compatibility and limits

Identity: `CarbonLuau`, API `0.3.0-experimental`, status `Experimental`.
Scripts inspect `game.ApiName`, `game.ApiVersion`, `game.ApiStatus`; operators use
`carbonluau.status`. This identity is distinct from package 0.3.0, the published
v0.3.0 native ABI 1.2, the post-release internal Foundation A ABI 1.3,
the Foundation D development ABI 1.4/provider protocol 1.2, the pinned Luau
revision and the installed Rust/Carbon builds. Foundation D's addon-facing names
are implemented in development source but still have no assigned public scripting
API identity and are not part of `0.3.0-experimental`.

The canonical compatibility policy lives in [Compatibility.md](../Compatibility.md)
and decisions D8/D12. Additive means preserving existing contracts while adding
names/operations. Removing/renaming APIs or changing types, lifetime, failure or
authorization behavior is breaking and requires an explicit version, documented
change and migration decision. Experimental does not permit silent breaking changes.
No automatic version negotiation, long-term deprecation window or Roblox contract
is promised. Consult [current qualification](../Phase3-Validation.md).

D13 defers the unshipped Items/Items:Exists and Player:GiveItem proposal from v0.1.
This is a [roadmap scope revision](../CarbonLuau_FirstVersion_Design.md#31-suggested-implementation-phases),
not removal of an implemented API. Package, scripting API and native ABI identities
are unchanged; no script migration is required for this deferral.

| Resource | Phase 3 bound |
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

NUL is rejected in API strings; malformed UTF-8 is rejected by host transport.
Wrong types are not implicitly coerced. Registration/input failures raise script
errors. Host intake overload drops/rejects new deliveries; it does not allocate
unbounded work. Permission and lifetime rejection before Lua means no user callback.

The native callback queue and managed intake are separately bounded, not one shared
memory cap. Source ingestion, native host-response storage (256 KiB per facade VM),
compiler memory and managed/Carbon resources are outside the Lua VM heap cap.
Deadlines remain cooperative; a bounded host operation can exceed a frame budget.
No multi-tenant, real-time, client-delivery or exactly-once replay guarantee exists.
