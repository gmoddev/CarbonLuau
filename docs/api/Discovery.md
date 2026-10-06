# Entity discovery

Discovery-2B contract for development scripting API `0.6.0-experimental`.
Public binding and runtime qualification are recorded as PASS in the
[Foundation 2B record](../WorldEntityFoundation2B.md). The latest published
package remains `0.5.0` and does not include Workspace or Entity. This guide
does not announce a package release or final Foundation 2 release closure.

Obtain the service with `game:GetService("Workspace")`. The existing
[exact lookup reference](Services/Workspace.md) owns
`Workspace:GetEntityById`; that method remains a keyed lookup without a world
scan. Discovery adds one separately admitted asynchronous read:

```luau
Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number,
    Callback: ({Entity}?, string?) -> (),
    Options: { Prefab: string?, Limit: number? }?) -> ()
```

The callback is mandatory and third; optional Options is last. The plain-table
alias is documented as [EntityDiscoveryOptions](Types/EntityDiscoveryOptions.md),
matching the bootstrap annotation's name. Submission returns no value and does
not yield. Completion enters the callback later.

| Argument | Contract |
|---|---|
| `Position` | Immutable CarbonLuau [Vector3](Types/Vector3.md) in world coordinates; all components finite. |
| `Radius` | Finite number greater than or equal to zero. |
| `Callback` | Required function receiving the result array or an error. |
| `Options.Prefab` | Optional exact full canonical prefab string: nonempty valid UTF-8, at most 512 bytes, no NUL. |
| `Options.Limit` | Optional integer from 1 through 256; defaults to 256. |

Omitting Options or either optional field uses its default: no prefab filter,
and Limit 256. Options accepts only Prefab and Limit and cannot have a metatable.
Prefab matching uses the full string exactly, including case;
short names, wildcard matching, normalization and resource/path loading are not
part of this operation. The geometric test includes roots on the sphere boundary.
Radius zero matches only a root observed exactly at Position. Limit is a
whole-query result ceiling: a further match fails the query instead of returning
the first Limit results. It does not request nearest-N or stop the scan early.

## Submission and completion

Submission requires an existing committed operation and no active publication
scope. Entry/candidate initialization and first execution of a cold module cannot
submit, even through `pcall`, nested calls or a foreign committed Workspace
facade. A cold module may export a closure that submits when invoked later from
eligible committed execution. Sharing a service or closure does not transfer
its owner authority or make provisional execution committed.

Invalid arguments, stale authority and failed admission raise controlled
synchronous Luau errors. Catch them around submission with `pcall`; rejection
does not call Callback. Acceptance is not a successful query result.

| Later outcome | Callback arguments |
|---|---|
| Success, including no matches | Dense `{Entity}` array (possibly empty), `nil` error. |
| Whole-query failure | `nil` array, controlled error string. |
| Original owner/VM/session retirement | Silent discard; no callback into replacement code. |

Every retained result is validated as the same exact completed-Spawn Entity
lifetime immediately at callback admission. One stale result, catalog/observer
loss, unsafe or invalid position observation, excess work/results or expiry
fails the whole query; there is no partial array or successful omission of a
failed candidate. Error strings expose controlled categories, not internal
assembly, patch or native exception details; do not parse undocumented messages.

Callback execution is a fresh bounded owner-thread admission with the original
host/session/VM/domain/publication authority checked before entry. It does not
resume submission or extend that operation's deadline. Host-driven recursive
Luau entry is prohibited. Delivery is at most once; callback failure or timeout
does not replay the callback or traversal. Later accesses to a returned
[Entity](Types/Entity.md) still validate its live lifetime and may raise controlled
errors if it has since retired.

## What the query observes

The source is the fixed completed-Spawn catalog from
[Foundation 2A](../WorldEntityFoundation2A.md), rather than a network-group
subset or a mutable host-registry iterator. Acceptance captures the catalog
extent and upper birth watermark. Later births are excluded, including births
that reuse an older physical slot. Holes and reused slots count toward work.
Entities retired before encounter are ineligible.

Root world positions are sampled when each candidate is encountered, across
multiple owner-thread turns. They are not an atomic snapshot at submission or
completion. Movement after a candidate's encounter does not turn its sample into
a completion-time pose; reading `Entity.Position` in Callback is a new live read.
Results preserve physical slot encounter order, not distance order. A successful
array contains all matches within that admitted cohort and observation contract,
subject to the whole-query ceiling; it is not a claim about one instant's world.

Trusted external Rust/Unity/Carbon/Oxide/physics/job writers may race observations
under [I12](../Invariants.md#i12--trusted-in-process-host-interference).
Exact lifetime, storage safety, bounded work and CarbonLuau owner-thread ordering
remain mandatory. This boundary does not permit unsupported structural writes,
unsafe native reads or failed lifetime validation.

| Resource | Bound |
|---|---:|
| Catalog / total raw slots per request | 262,144 |
| Pending requests, including undelivered completion | 8 global / 2 per domain lifetime |
| Shared scan/delivery units per frame | 1,024 |
| Raw slots inspected per frame, shared across queries | 1,024 |
| Results per request | Limit, at most 256 |
| Deliveries per frame | At most 2, within the shared unit budget |
| Absolute deadline, acceptance through completion admission | 120 seconds |

The deadline includes queue delay. Host pauses do not reset it: expiry is
processed before further work or successful delivery when owner-thread service
resumes. A controlled timeout callback may arrive later if its original authority
remains valid; the deadline is not a callback wall-time guarantee under host
starvation. No public cursor, cancellation handle, query DSL, ordering option,
nearest sort, lifecycle Signal, Spawn, Destroy or world mutation is added.

## Startup and example

The exact pinned host and continuous initial-startup observer requirements in
[compatibility](Compatibility.md) apply. First installation after world startup
or full CarbonLuau plugin unload/reload requires a server restart with CarbonLuau
in the initial plugin batch. An unqualified world is an error, not an empty
successful query. Ordinary root/addon replacement or VM recovery preserves a
continuous observer but retires old callback and facade authority.

The [runnable discovery example](../../examples/world/discovery/init.luau)
submits from deferred committed execution, supplies an exact full Prefab and
Limit, and handles submission rejection, later error, empty array and results.
Copy its `init.luau` into the configured script root on a development build
containing Discovery-2B, following [installation](../Installation.md). Adjust
the center, radius and full prefab to the world you want to observe. The example
does not create entities or guarantee that this prefab is present.
