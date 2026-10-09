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
exist. [Foundation 2C](../WorldEntityFoundation2C.md) owns combined qualification
and release readiness for these four world examples.

The [EntitySpawned example](../../examples/world/entity-spawned/init.luau)
requires development API `0.6.5-experimental`. It observes future completed
Spawns through the existing Signal path and uses `pcall` for live Entity reads
that can race later host changes. It does not create entities or replay the
existing world. [Gameplay B1](../GameplayB1-Validation.md) owns its distinct
qualification; the frozen 0.6.0 evidence does not qualify this new Signal.
