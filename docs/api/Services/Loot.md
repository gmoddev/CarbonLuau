# Loot

Introduced at API `0.6.5-experimental`, included in published v0.7.0 but not
v0.5.0. Obtain the frozen service with `game:GetService("Loot")`.

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

Current post-v0.7.0 development builds qualified by
[Gameplay D4](../../GameplayD4-Validation.md) support these exact prefabs:

```text
assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab
assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab
```

Published v0.7.0 assets remain unchanged and support only barrel-1. Barrel-2
requires the qualified D4 build; no new release is announced here. Short names,
case variants and other barrel paths are not accepted.

Drops is a plain dense array of zero through eight
[LootDropEntry](../Types/LootDropEntry.md) records. An empty array requests no
custom payout. Each record has exactly Item, Min and Max; Item is `scrap`,
`metal.fragments` or `wood`. Wood requires the post-v0.7.0
[Gameplay D5 build](../../GameplayD5-Validation.md); published v0.7.0 still
supports only scrap and metal.fragments. The pinned wood definition is
`-151838493`, stackable 1000. The adapter creates one original Item/world per
nonzero entry and does not split oversized amounts or configure fuel, condition,
skins, contents or held entities. Min and Max are finite integers with
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

Current post-release [Gameplay D6 builds](../../GameplayD6-Validation.md) allow
one active owner per qualified prefab, two rules globally and at most two rules
per domain. One root/addon may own both targets, or separate domains may own one
each. Repeated registration of the same prefab by one candidate raises a
controlled error before definition lookup, even when the entries are identical.
A foreign committed owner conflicts only for an overlapping prefab.

Initialization stages the owner's complete set. Successful generation replacement
publishes that set together: redeclare every rule to retain, omit a rule to
withdraw it. Failed argument/module/candidate/publication paths never partly
publish a set; caught module failure restores the earlier staged set. Healthy
failed replacement preserves the prior committed owner set. A caught rejected
declaration leaves earlier valid declarations staged; the complete successfully
initialized set is what publishes. Fatal VM retirement
still invalidates every domain under the existing recovery contract.

Retirement/provider unload withdraws only the retired domain's exact owned keys;
other owners remain. Registration/reconstruction never replays destruction.
An unregistered target remains vanilla. Both targets share the existing producer
budget; replacing rules or switching owners never resets its actual-frame use.
Published v0.7.0 retains its original one-global-rule contract; D4/D5 development
receipts likewise retain their historical single-rule scope.

Registry staging has an additional fixed retention bound: at most three
rule-bearing facade sessions (committed owners plus pending initialization), six
distinct immutable descriptors and 390 map/checkpoint reference slots across
the existing 64 publication levels. Snapshots share descriptors rather than
copying entries. The one physical frame may separately borrow one descriptor;
validation has bounded temporary arrays. These are managed retention bounds,
not a whole-process memory cap. See the D6 record for the full accounting.

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
The [D2A qualification record](../../GameplayD2A-Validation.md) describes the
qualified physical adapter and its exact host limits; private qualification does
not by itself prove public binding qualification.

See the [initialization example](../../../examples/scripts/loot-replace.luau)
included in the package's scripts directory; use it as the root `init.luau`
on the qualified exact host.
For barrel-2, use the separate [D4 example](../../../examples/scripts/loot-replace-barrel-2.luau)
as a standalone root. For both targets in one root, use the
[D6 example](../../../examples/scripts/loot-replace-both-barrels.luau) on a qualified
D6 development build. The ordinary barrels use individual drops;
the alternate bag branch was tested with a controlled exact-source selector
override, not advertised as natural barrel-2 behavior.
See also the [Enum namespace](../Types/Enum.md).
