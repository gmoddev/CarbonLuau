# Scripting compatibility and limits

The v0.7.0 package contains the complete current API
`0.7.0-experimental`; see [release qualification](../Release-0.7.0-Validation.md).
Its package identity supersedes earlier phase-local 0.5 development holds,
not historical API introduction versions or published artifacts.

Post-v0.7.0 [Gameplay D4](../GameplayD4-Validation.md) additionally qualifies the
exact autospawn `loot-barrel-2.prefab` for strict Loot Replace on current development
builds. Published v0.7.0 supports only barrel-1; its tag/assets are immutable.
The signature, items, one global/domain rule, shared eight-occurrence frame
budget and no-fallback failure contract are unchanged. No new package/API/ABI
identity or release follows. Other barrel paths/items are not inferred supported.

Post-release [Gameplay D5](../GameplayD5-Validation.md) adds only `wood`, with
the exact pinned definition and ordinary Item/world ownership path. Scrap and
metal.fragments, both D4 prefab paths, one global/domain rule and all existing
entry/amount/frame/retention limits remain. Published v0.7.0 includes neither
wood nor barrel-2. The historical D4/D5 one-global-rule scope remains unchanged.

[Gameplay D6](../GameplayD6-Validation.md) qualifies one active owner per prefab,
two rules globally and per domain. Different domains can own disjoint targets;
candidate sets publish together and omission withdraws only the outgoing owner's
omitted key. Duplicate registration/same-prefab conflicts reject deterministically.
All per-rule and shared physical budgets remain; managed staging is bounded at
three rule-bearing sessions, six descriptors and 390 snapshot/map references.
The one in-flight frame's borrowed rule and validation temporaries are separately
bounded. No new item/prefab/mode or version identity is introduced.

[Gameplay D7](../GameplayD7-Validation.md) additionally qualifies only `stones`
(`-2099697608`, stackable 1000) on both existing targets. The supported names are
scrap, metal.fragments, wood and stones. D6 ownership, atomic whole-owner sets,
retirement and all entry/amount/frame/staging/table limits remain unchanged.
Other definitions remain rejected; published v0.7.0 and version identities are
unchanged. There is no new release, tag, mode, prefab or public initialization API.

[Gameplay D8](../GameplayD8-Validation.md) supersedes that four-name development
list with [221 finite qualified definitions](LootItems.md), including all prior
items. Exact IDs, stack limits and both-host individual receipts are recorded;
registry existence alone does not authorize another item. Max must fit both the
0..1000 declaration bound and live definition stackable field before COMMIT.
Effective inventory/container hook limits are separate; the
[stack modifier matrix](../GameplayD8-Validation.md#stack-modifier-compatibility-matrix)
defines the tested compatibility scope. D6 ownership,
atomic whole-owner publication, all resource bounds and strict partial-failure
semantics remain unchanged. Published v0.7.0 remains immutable.

Earlier published package v0.5.0 uses API `0.5.0-experimental`.
The development API identity is `CarbonLuau 0.7.0-experimental`, status `Experimental`;
the newer Workspace/Entity, gameplay and PlayerInventory surface is not in published v0.5.0.
Scripts inspect `game.ApiName`, `game.ApiVersion`, `game.ApiStatus`; operators use
`carbonluau.status`. This identity is distinct from package `0.7.0`, native ABI
`1.5`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1`, the
pinned Luau revision and the installed Rust/Carbon builds. Published v0.3.0
artifacts remain API `0.3.0-experimental` with native ABI `1.2`.

Persistence is assigned to 0.5.0-experimental and packaged in the v0.5.0
experimental prerelease. [Combined 1C qualification](../PersistenceFoundation1C.md)
records Foundation 1 evidence and platform limits; [combined 2D qualification](../PersistenceFoundation2D.md)
records Query and derived-index closure. ABI 1.5 is required by the additive
completion ingress. [Release mapping](../Release.md)
and [the 1B record](../qualification/Persistence1B.md#persistencefoundation1b) separate identity from evidence.
Existing APIs keep their historical introduction versions and qualification.

C4 explicitly removes the Player HasItem/CountItem/GiveItem/TakeItem methods,
without Player aliases. Existing Inventory HasItem/CountItem names are retained. Use
`Player:GetInventory():HasItem/CountItem/GiveItem/TakeItem` and the existing sampled ItemChanged
Signal. See the [breaking migration guide](Inventory-Migration.md) and
[PlayerInventory](Types/PlayerInventory.md) for the separate signatures.
Acquisition/read/Signal metadata retains its 0.6.5 introduction; new
`PlayerInventory:GiveItem`/`PlayerInventory:TakeItem` methods are introduced at 0.7.0.
GiveItemBehavior retains 0.4.0. Package is now 0.7.0; ABI 1.5, provider 1.2, schema 1 and
Luau stay unchanged. This mapping alone does not certify final-source qualification;
see [release readiness](../Release.md#gameplay-ad-release-readiness).

Read-only `Workspace:GetEntityById` and `Entity.Id`, `Prefab`, `Position`
begin in development API `0.6.0-experimental`. This is an exact-host feature:
Rust build `25653776`, protocol `2634.289.1`, Windows Carbon `2.0.262.0`
(`8a81d70`), Linux Carbon `2.0.261.0` (`c74c4ca`), Harmony `2.4.2.0`,
plus the pinned assembly/hook topology in the [Entity-1A record](../WorldEntityFoundation1A-Validation.md).
First installation after world startup and full CarbonLuau plugin unload/reload
require a server restart before Entity lookup; an unqualified world fails with
a controlled error, not `nil`. Root/addon replacement and fatal VM recovery
retain the continuous host observer while their old facades stale. Host drift
fails closed pending requalification. See the [Workspace reference](Services/Workspace.md).

Discovery-2B's selected `Workspace:GetEntitiesInRadiusAsync` contract targets the
same original `0.6.0-experimental` introduction identity. The mandatory callback is third;
optional [EntityDiscoveryOptions](Types/EntityDiscoveryOptions.md) is last, with
exact full Prefab and integer Limit 1..256 (default 256). Submission is committed-only;
completion is a fresh bounded owner-thread admission with every result validated.
Success includes an empty array; whole-query failure supplies nil array and a
controlled error; retirement silently discards completion. Encounter positions
and accepted birth watermark do not form an atomic world snapshot. See the
[discovery guide](Discovery.md) and [public qualification](../WorldEntityFoundation2B.md).
The [private 2A record](../WorldEntityFoundation2A.md) owns the additional pinned
Unity images; its evidence does not qualify public callback admission. The same
startup/restart requirements apply. The surface is included in published v0.7.0;
ABI `1.5`, provider protocol, schema and Luau pin remain unchanged.
Combined closure and final release-readiness evidence are owned by
[Foundation 2C](../WorldEntityFoundation2C.md).

`Workspace.EntitySpawned` and snapshot-only `Workspace.EntityDestroyed` begin
at API `0.6.5-experimental`, included in v0.7.0 but not v0.5.0. Both inherit the
exact D20 startup/restart requirement and shared Gameplay A event budgets.
[B1 evidence](../GameplayB1-Validation.md) qualifies completed Spawn followed by
original live-lifetime admission. [B3 evidence](../GameplayB3-Validation.md)
records successful original world removal or proven original native destruction,
not attempted Kill. Native-only destruction supplies nil Position and does not
promise Rust bookkeeping cleanup. Source/patch drift fails closed; no historical
replay or new package identity follows.

The separate `Workspace.EntityDied` contract also uses
`0.6.5-experimental`, with a seven-field frozen historical
[EntityDeathContext](Types/EntityDeathContext.md). Full death and successful
removal are different observations. Runtime registration requires its own
qualified source. Original D11 actor attribution is qualified only for the narrow
synchronous RPC melee path on the supported exact hosts, with same-writer-scope
validation at full death completion. Other provenance supplies nil actors;
metadata does not infer actors by UserId. Preview remains
unavailable. Existing event/position-attempt budgets and D20 startup continuity
apply, with no new package, ABI or provider identity.

[Persistence Foundation 2](../PersistenceFoundation2.md) defines structured
Query for the same `0.5.0-experimental` API. Optional field-name hints in
`GetDataStore(Name, Options?)` are the 2B addition and appear in generated
definitions. [2C](../PersistenceFoundation2C.md) adds bounded structured
`DataStore:Query`, result pages and opaque authenticated cursors. There is no
author schema, version or migration requirement. Get/Set/Remove retain their
Foundation 1 contracts; [combined Foundation 2 qualification](../PersistenceFoundation2D.md)
is recorded separately.

The earlier published 0.4.0 release combines the additive addon, GUI Foundation 1 and
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

Player-1A adds immutable `Vector3` and read-only `Player.Position` under the
existing `0.4.0-experimental` identity. Components and vector arithmetic remain
inside the finite System.Single range; Position is one bounded exact-connection
host lookup and one root world-position read. See the
[Player-1A qualification record](../qualification/PlayerInteraction.md#playerinteractionfoundation1a).

Player-1B adds read-only `Player.Health` and `Player.MaxHealth` under that same
identity. Each access performs one exact-connection lookup and one direct live
host read. Finite results are preserved independently without clamping; stale
connections and non-finite host values raise controlled errors. See the
[Player-1B qualification record](../qualification/PlayerInteraction.md#playerinteractionfoundation1b).

Player-1C adds the domain-bound `Items` service with `Exists`, plus bounded
physical inventory reads under the same historical identity. In current API 0.7,
these are `Player:GetInventory():CountItem` and `Player:GetInventory():HasItem`.
The scanner reads only direct main, belt and wear entries, rejects more than 128
entries before returning any answer, and never uses hook-virtualized count APIs.
See the [Player-1C qualification record](../qualification/PlayerInteraction.md#playerinteractionfoundation1c).

Player-1D adds committed-only `Player:Teleport(Vector3)` under the same
identity. It preserves D11 exact-connection identity, sleeping state and the
D10 provisional-effect boundary. The exact-build server adapter is qualified,
but authenticated-client convergence remains unqualified. See the
[Player-1D qualification record](../qualification/PlayerInteraction.md#playerinteractionfoundation1d).

Revised D13 and
[D18](../Invariants.md#d18--player-interaction-foundation-1) approve bounded
read-only item observation implemented by Player-1C, plus the mutations originally
qualified by Player-1F-A/B. C4 exposes them as `Inventory:GiveItem` and `Inventory:TakeItem`.
TakeItem reuses the exact Inventory-M2 adapter; GiveItem reuses the
[requalified supported-host path](../qualification/PlayerInteraction.md#playerinteractionfoundation1fb-validation).
The historical blanket M2 G1 conclusion remains superseded.
`Inventory:GiveItem(ShortName, Amount, Behavior?)` defaults to the sole typed member
`GiveItemBehavior.InventoryOnly`.
`DropRemainder` is future design only, not a reserved or exposed enum value.
Their canonical result contract is `false` only before inventory COMMIT, `true`
only after physical VERIFY, and a controlled error for post-COMMIT uncertainty.
The original Player-1F implementation was additive. C4's removal from Player is an
explicit breaking change at API 0.7, with physical behavior preserved and no
package or native ABI change. Historical Player-1F evidence retains its names
and source attribution; C4 requires separate qualification.

[Player-1F-C](../qualification/PlayerInteraction.md#playerinteractionfoundation1fc) records the correction of a
cold-module mutation bug in `9b27ba5` and combined closure. Provisional outer
admission **or** active first-load publication forbids committed-only mutation,
including nested/public/shared calls. Cached exports called later by committed
execution remain usable. Reads stay allowed; no deadline reset, inventory
reservation, transaction, rollback or exactly-once recovery is implied.

[I12](../Invariants.md#i12--trusted-in-process-host-interference) defines the
trusted in-process interference boundary. Normal vanilla behavior, ordinary
callback acceptance/rejection and CarbonLuau's own planning/verification bugs
are not excluded. Equally privileged plugins can change inventory, stack limits
or item state inside callbacks; no whole-process isolation is promised. Observed
uncertainty cannot become success. Errors after creation may include partial
accepted delivery or failed cleanup; reconcile host state rather than blindly
retrying. Neither timeout nor recovery provides rollback or exactly-once delivery.

| Resource | Bound |
|---|---|
| Connected-player population / snapshot | 1024; larger host population fails closed |
| Entity ID / Prefab snapshot | Canonical 1–20 digit nonzero UInt64 string / 512 UTF-8 bytes, no NUL |
| Workspace lookup | One keyed registry lookup plus constant validation; no world scan or index |
| Discovery-2B: catalog / total raw slots per request | 262,144; qualified on the exact pinned Windows/Linux hosts |
| Discovery-2B contract: pending requests global / domain lifetime | 8 / 2, including completed-undelivered work |
| Discovery-2B contract: shared scan/delivery units / raw slots per frame | 1,024 / 1,024 across all queries |
| Discovery-2B contract: result Limit / absolute deadline | Integer 1..256, default 256 / 120 seconds including completion queue |
| Player name / user ID | 128 UTF-8 bytes / 20 ASCII decimal digits |
| Vector3 components | Finite System.Single range; no clamping |
| Item short name / physical inventory entries | 1..128 lowercase ASCII bytes / 128 direct main+belt+wear entries |
| Inventory HasItem amount / CountItem quantity | Exact positive integer through `2^53 - 1` / checked exact Luau integer |
| Inventory TakeItem amount / verification | Exact integer 1..`Int32.MaxValue`; one bounded fresh scan after COMMIT |
| Inventory GiveItem amount / plan / verification | Required exact integer 1..`Int32.MaxValue`; 128 total main/belt/wear slots/entries, 128 chunks, one fresh VERIFY scan |
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

Persistence bounds, names, error outcomes and the qualified operational disk
budget are owned by [Persistence Foundation 1](../PersistenceFoundation1.md#5-logical-names-and-bounds).
The [author guide](Persistence.md) explains their use; generated type information
does not validate finite numbers, UTF-8, table density, cycles or resource bounds.

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

Gameplay A adds no configuration knobs or per-addon heap limit. Its current
hard internal safety ceilings are 128 observed captures per frame, 128 total
lifecycle callback admissions per frame, 32 per domain per frame, 4,096 bounded
producer/fanout visits per frame, 512 globally held deliveries and 2,048 bytes
per payload. Conservative transport accounting charges two payload copies and
remains at most 2 MiB, including native-held/in-flight work. Existing per-signal
128 and per-domain 256 Player listener limits still apply across the new members.
These bounds do not promise delivery under overload or a wall-clock callback SLA.
Drops are visible in operator status and are never replayed.

PlayerDied and PlayerSpawned were introduced at API `0.6.5-experimental`.
Their exact current Windows/Linux source adapters require the pinned Rust/Carbon
tuple in [Gameplay A evidence](../GameplayFoundationA-Validation.md). Unknown
host/patch drift fails closed. Authenticated Steam/client traces remain
unqualified; constructed exact-host server tests and synthetic VM tests are
explicitly labelled separately. No generic Entity or inventory event bus is exposed.

Gameplay B1 introduced `Workspace.EntitySpawned` at that same 0.6.5 identity.
It reports only qualified future full-Spawn completions, with original-lifetime
revalidation before callback admission and no network-ID retarget or startup
replay. It shares the Player lifecycle producer/fanout/queue/retention envelopes,
not a separate budget. Overload drops are diagnosed and never retried. The D20
continuous-observer, pinned-host and restart requirements remain mandatory.
See [B1 qualification](../GameplayB1-Validation.md) for numeric bounds and
separately labelled live, model, VM, sanitizer and CI evidence.
