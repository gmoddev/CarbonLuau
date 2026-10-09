# Player lifecycle example

Use development API `0.6.5-experimental` on a qualified Rust/Carbon host. This
example is not compatible with the published v0.5.0 surface.

Install `init.luau` as your operator entrypoint only after preserving existing
scripts, and keep a `modules` directory beside it. It prints completed initial
spawns/respawns and terminal deaths. It does not replay events for players who
were already present, run inline in a host hook, or treat wounded state as death.

Context values are immutable snapshots. Missing position/killer observations are
normal; no client acknowledgement or indirect kill attribution is promised.
See the [Players reference](../../docs/api/Services/Players.md).
