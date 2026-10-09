# Workspace

Availability: development scripting API `0.6.0-experimental`, for the exact
qualified Rust/Carbon targets only. The latest published package remains
`0.5.0`; it does **not** contain this API. Obtain the service with
`game:GetService("Workspace")`.

| Method | Result | Behavior |
|---|---|---|
| `Workspace:GetEntityById(Id: string)` | `Entity?` | One keyed lookup of a currently admitted Rust `BaseEntity`; `nil` if a valid ID is absent. |
| `Workspace.EntitySpawned` | Signal `(Entity) -> ()` | Future qualified full outer Spawn completions. Introduced in development API `0.6.5-experimental`. |
| `Workspace.EntityDestroyed` | Signal `(EntityDestroyedContext) -> ()` | Future qualified successful removals/destruction, delivered as immutable original-incarnation snapshots. Development API `0.6.5-experimental`. |
| `Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number, Callback: ({Entity}?, string?) -> (), Options: EntityDiscoveryOptions?)` | `()` | Non-yielding committed-only submission; later callback with the whole result array or a controlled error. Qualified by [Discovery-2B](../../WorldEntityFoundation2B.md). |

## EntitySpawned

```luau
local Workspace = game:GetService("Workspace")
Workspace.EntitySpawned:Connect(function(Entity)
    print(Entity.Id, Entity.Prefab, Entity.Position)
end)
```

The existing Signal/Connection conventions apply. CarbonLuau captures only
after the qualified outermost virtual Spawn returns successfully. Carbon's
early OnEntitySpawned hook is not the event's completion authority.

Each callback receives an ordinary domain-bound exact Entity facade. The original
private lifetime is revalidated at fresh scheduler admission. A destroyed,
re-spawned, registry-replaced or retired entity is suppressed, never looked up
again by network ID to select a replacement. Callback properties remain live
reads and may fail if the world changes after admission; use pcall for races.

There is no startup/incumbent replay, history buffer or catch-up on registration,
root/addon replacement, provider reload or VM recovery. First-install hotload and
full plugin unload/reload retain D20's server-restart requirement. Unknown host or
patch drift fails closed, including registration while the source is unavailable.

Entity events share Gameplay A's global work/queue/byte limits with Player events,
not another independent frame budget. Overload can drop deliveries; diagnostics
count drops without retries. Failed module/candidate connections publish no
listeners; committed Disconnect suppresses queued work. B1 does not add Entity
death, damage, inventory, policy or arbitrary-hook APIs. EntityDestroyed is the
separate snapshot-only observation below, not a live Entity callback.

See [B1 qualification](../../GameplayB1-Validation.md) and
[Signal](../Types/Signal.md). This is development source, not the published v0.5.0.

## EntityDestroyed

```luau
Workspace.EntityDestroyed:Connect(function(Context)
    print(Context.Id, Context.Prefab)
    if Context.Position then print(Context.Position) end
end)
```

Introduced in development API `0.6.5-experimental`. The callback receives a
frozen [EntityDestroyedContext](../Types/EntityDestroyedContext.md), never a
destroyed live Entity proxy. It describes the original qualified incarnation:
a completed Rust-world removal, including a pooling path, or proven native
Entity deletion. Vetoed/attempted Kill, cancellation intent, registry churn,
CarbonLuau facade retirement and garbage collection are not successful
destruction observations.

The independently pinned pool fence observes actual stack admission only after
the original incarnation's registry/network authority has been released. A
still-network-registered object pushed into a pool is not proven world removal.
The fact is captured before later pool callbacks or object reuse; deactivation
and the Rust destroyed flag are not required for that boundary.

Native deletion must first be certified against the original weak object;
it is not inferred from a cancellation notification alone. A bounded original-
watch check through the existing frame pump also covers destruction that produces
no cancellation callback. Collection before proof suppresses notification;
later collection does not invalidate an already certified historical snapshot.
Native deletion can leave Rust bookkeeping ghosts. This event does not promise
registry/network cleanup, successful loot processing or complete host teardown.
A completed Rust-world removal can still be observed if later host cleanup
fails. No event retargets a replacement by network ID.

Id and Prefab are bounded pre-invalidation snapshots. Position is optional:
qualified removal paths may capture a safe root world position before
invalidation; native deletion and direct pool admission supply nil. No stale cache, spawn-time
position or later live read is substituted. Snapshot values remain usable after
the original entity is retired or its object/ID is reused.

Delivery is best-effort through the existing owned Signal scheduler, not inline
in a host hook. Listener/publication/source authority and the completion witness
are validated before entry. Missing completion proof, unavailable source,
retired listener, Disconnect and overload suppress delivery, with bounded
diagnostics and no retries. There is no startup replay or backlog on subscription,
replacement, provider reconstruction or VM recovery. D20's initial-install/full
plugin-reload restart requirements remain unchanged.

Native polling cannot timestamp a deletion between visits. Listeners published
after the last positive live observation are excluded rather than receiving a
possible historical replay. Recently registered listeners may consequently miss
a notification in that ambiguous window. This is best-effort observation, not
an exactly-once delivery or callback-latency promise.

The source bounds are 262,144 watches, 64 MiB of charged snapshot scalar text
(not a total process-heap limit), 16 simultaneous removal-observation frames and
128 original native-watch slots per actual Unity frame. There is no realm scan,
ID reacquisition or independent event scheduler.
Player and Entity streams share 128 captures/frame, 128 deliveries/frame,
32 deliveries/domain/frame, 4,096 visits/frame, 512 held reservations,
2,048-byte payloads and 2 MiB of transport accounting. These are global bounds,
not a fresh budget for each addon or event kind.

See [B3 evidence and qualification scope](../../GameplayB3-Validation.md) and the
[runnable example](../../../examples/world/entity-destroyed/init.luau). This
development API is not included in the published v0.5.0 package. It adds no
EntityDied, killer/damage attribution, loot policy or world-mutation API.

## GetEntityById

`Id` must be a canonical nonzero unsigned-64 decimal string: 1–20 ASCII
digits, no leading zero, sign, whitespace or numeric conversion. A malformed
ID raises a controlled error. `GetEntityById` never scans or enumerates the world.

If CarbonLuau was first installed after this server process had already loaded
its world, or if the CarbonLuau plugin was fully unloaded and reloaded, Entity
lookup fails with a controlled world-unavailable error. **Restart the server**
with CarbonLuau in the initial plugin batch before using this service. A
failed/unqualified observer is not reported as an empty world. Ordinary script,
addon or VM recovery does not itself require a server restart while that
observer remains continuously installed.

```luau
local Workspace = game:GetService("Workspace")
local Entity = Workspace:GetEntityById("123")
if Entity then
    print(Entity.Prefab, Entity.Position)
end
```

An ID is a lookup key in the *current* Rust world, not durable Entity identity;
it may later refer to another object. See [Entity](../Types/Entity.md),
[examples](../World-Examples.md) and the [exact compatibility envelope](../Compatibility.md).

## GetEntitiesInRadiusAsync

```luau
Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number,
    Callback: ({Entity}?, string?) -> (),
    Options: EntityDiscoveryOptions?) -> ()
```

Callback is mandatory and third; optional Options is last. Position is the
existing immutable finite [Vector3](../Types/Vector3.md) in world coordinates;
Radius must be a finite nonnegative number. The root-sphere test includes the
boundary; zero radius requires exact coincidence at encounter.

[EntityDiscoveryOptions](../Types/EntityDiscoveryOptions.md) is a plain table
with optional exact full Prefab and integer Limit 1..256, default 256. Prefab
must be nonempty valid UTF-8, at most 512 bytes, without NUL. Omitting Options
or Prefab applies no prefab filter. Exceeding Limit fails the whole query instead
of returning a truncated array or nearest-N results.

Submission requires an existing committed operation and no active publication
scope. Provisional initialization, first-load cold modules, nested calls,
`pcall` and foreign committed facades cannot launder eligibility. A cold module
may export a closure that submits later from eligible committed execution.
Invalid arguments, stale authority and admission rejection raise controlled
synchronous errors without invoking Callback. Submission returns no value and
does not yield.

Accepted work later invokes Callback with either the entire dense Entity array
and nil error (including an empty array), or nil array and a controlled error
string. Every retained exact Entity lifetime and the original
host/session/VM/domain/publication authority is validated at callback admission.
One invalid result fails the whole query; there is no partial success. Callback
execution is a fresh bounded owner-thread admission, never recursive host-driven
VM entry or a resumption of submission. Callback failure/timeout does not replay
work. Owner/session/VM retirement silently discards completion and cannot target
replacement code. Later Entity property reads still validate live lifetime.

Discovery traverses the completed-Spawn catalog with a birth watermark captured
at acceptance. Later births are excluded; positions are root world observations
at each candidate's encounter, not an atomic submission/completion snapshot.
Results preserve physical slot order, not distance order. Trusted external
writers may race these observations under
[I12](../../Invariants.md#i12--trusted-in-process-host-interference), while exact
lifetime, storage safety, bounded work and CarbonLuau owner-thread ordering
remain mandatory.

The shared bounds are 262,144 catalog/total raw slots per request; 8 pending
requests globally and 2 per domain lifetime, including undelivered completion;
1,024 scan/delivery units and at most 1,024 raw slots per frame across all
queries; at most 256 results and 2 deliveries per frame within that budget;
and a 120-second absolute deadline from acceptance through completion admission.
Queue delay and host pauses do not reset it. Expiry is processed before further
work/success when owner-thread service resumes; no callback wall-time SLA is
promised under starvation.

The initial-startup observer and restart requirements above also apply to
discovery. An unqualified world cannot become an empty successful result. See
the [discovery guide](../Discovery.md) for full semantics, limits and the
[runnable example](../../../examples/world/discovery/init.luau).
This query adds no synchronous world enumeration, query DSL, nearest sorting,
public cursor, Spawn, Destroy or world mutation.
