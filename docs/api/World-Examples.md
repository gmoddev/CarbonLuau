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
