# Read-only world examples

These examples target development scripting API `0.6.0-experimental`; the
published v0.5.0 package does not include Workspace or Entity. The server must
meet the [exact compatibility and startup requirements](Compatibility.md).

Run the [exact lookup](../../examples/world/exact-lookup/init.luau),
[equality](../../examples/world/equality/init.luau) and
[string-ID](../../examples/world/string-id/init.luau) examples from their
respective directories. A valid absent ID returns `nil`; a malformed ID raises
an error. An unqualified first-install or full-plugin-reload world raises an
operational error, not `nil`.

These examples only read current Rust world state. They do not Spawn, Destroy,
enumerate, subscribe to entity events or persist `Entity.Id` as durable identity.

For bounded asynchronous radius discovery, run the
[discovery example](../../examples/world/discovery/init.luau) and follow the
[discovery guide](Discovery.md). It submits from deferred committed execution,
uses an exact full Prefab and Limit, and handles both submission rejection and
later callback failure. It does not create entities or assume matching entities
exist. [Foundation 2C](../Support.md#evidence-scope) owns combined qualification
and release readiness for these four world examples.

The [EntitySpawned example](../../examples/world/entity-spawned/init.luau)
requires development API `0.6.5-experimental`. It observes future completed
Spawns through the existing Signal path and uses `pcall` for live Entity reads
that can race later host changes. It does not create entities or replay the
existing world. [Gameplay B1](../Support.md#evidence-scope) owns its distinct
qualification; the frozen 0.6.0 evidence does not qualify this new Signal.

The [EntityDestroyed example](../../examples/world/entity-destroyed/init.luau)
also requires `0.6.5-experimental`. Its callback reads an immutable original-
incarnation snapshot, not a destroyed live Entity. Position may be nil, and Id
must not be treated as durable lifetime authority. It observes successful removal
without causing destruction. [Gameplay B3](../Support.md#evidence-scope) owns its
separate host/transport qualification and current completion gates.

The [EntityDied example](../../examples/world/entity-died/init.luau) targets the
development `0.6.5-experimental` contract and handles unavailable-source
registration. It observes a full-death snapshot rather than causing death or
removal. Frozen Id/Prefab can describe an already retired original incarnation;
all five additional fields are optional. It guards Position, captured actor and
damage/weapon labels and never resolves a network ID or UserId to select a new
object. Actor fields are supplied only by the qualified original synchronous
RPC melee source on the supported exact hosts; other or unknown provenance
leaves them nil. Tooling preview does not simulate it.
