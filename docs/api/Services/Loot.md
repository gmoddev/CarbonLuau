# Loot

Development scripting API `0.6.5-experimental`; not included in published
package v0.5.0. Obtain the frozen service with `game:GetService("Loot")`.

```luau
Loot:RegisterDropRule(Prefab: string, Mode: DropMode, Drops: {LootDropEntry}): ()
```

Call during initialization of the owning root or addon. Exactly three arguments
are required, with [Enum.DropMode.Replace](../Types/DropMode.md) as Mode.
The declaration copies its inputs and stages a rule until initialization and
publication succeed. It returns no values and performs no immediate payout.
Callbacks and already-active modules cannot register rules. Invalid input,
wrong receivers and unavailable gates detected during the call raise controlled
Luau errors synchronously. Publication rechecks availability and ownership;
a conflict can reject the candidate after the declaration returns, preserving
the prior owner. `pcall` around registration is not proof of publication or payout.

Only this exact prefab is supported:

```text
assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab
```

Drops is a plain dense array of zero through eight
[LootDropEntry](../Types/LootDropEntry.md) records. An empty array requests no
custom payout. Each record has exactly Item, Min and Max; Item is `scrap` or
`metal.fragments`. Min and Max are finite integers with
`0 <= Min <= Max <= 1000`; the sum of Max across entries cannot exceed 8000.
Arrays and entries cannot have metatables, holes or extra keys. Later changes
to the input tables do not change the copied rule.

Use fresh small table literals for Drops and its entries. Oversized preallocated
tables raise a controlled Luau resource error, even when empty or cleared back
to a few keys. The current implementation checks table storage before iteration:
each input table may have at most 64 allocated array slots and 64 allocated hash
slots. These storage limits do not increase the logical limit of eight drop
entries or the three fields in each entry. Create a fresh small table instead
of reusing a large preallocated table.

One rule is allowed globally across the root and all addons, with at most one
per domain. A successful replacement can replace its exact outgoing owner's
rule. A failed candidate preserves the prior rule. Retirement removes the
owner's rule from future selection; registration and reload do not replay
earlier destruction events.

Replace applies only to eligible, qualified destruction-time stored, bonus and
bag payouts. It does not change earlier fill/generation or reclaim items
already dropped during fill. The normal destruction/removal path still runs.
Each admitted event can create at most eight world payouts, with at most eight
events per actual server frame and one event in flight. Frame exhaustion makes
an occurrence ineligible; replacement of every barrel in a burst is not promised.

Before commit, an ineligible source, unavailable authority or exhausted budget
leaves vanilla behavior. Suppression starts physical commit. After commit, a
failure can leave a partial payout: there is no vanilla fallback, automatic
replay or compensation, and accepted world payouts survive later failures.
Successful declaration is not a payout-success receipt or a rollback guarantee.

Use only an eligible, qualified Rust/Carbon host with the startup observers and
loot source gates available. First-time hotload or full plugin unload/reload
requires a server restart to restore startup observation authority. Source drift
fails closed; a declaration can fail if the source gate is unavailable.
The [D2A qualification record](../../Support.md#evidence-scope) describes the
qualified physical adapter and its exact host limits; private qualification does
not by itself prove public binding qualification.

See the [initialization example](../../../examples/scripts/loot-replace.luau)
included in the package's scripts directory; use it as the root `init.luau`
on the qualified development host.
See also the [Enum namespace](../Types/Enum.md).
