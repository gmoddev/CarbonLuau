# Gameplay: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D23 — Gameplay A Player lifecycle Signals

The additive public surface is exactly `Players.PlayerDied(Player,
PlayerDeathContext)` and `Players.PlayerSpawned(Player, PlayerSpawnContext)`,
using existing Signal:Connect/Connection:Disconnect. Added/Removing semantics
remain unchanged. There is no new Events service, hook-name API or scheduler.

PlayerDied observes a completed terminal transition of a tracked eligible human,
once per private qualified life epoch inside its exact D11 connection. It is not
wounded entry or a vetoable OnPlayerDeath notification. The exact full original
BasePlayer.Die must complete normally with its qualified base-death progress;
current flags alone do not establish completion. PlayerSpawned includes eligible
initial activation and subsequent completed full RespawnAt, including a full
alive reset. Wake, revive and sleeper reconnect are not spawns. Skipped, throwing,
nested/unqualified and NPC paths do not manufacture events. The pinned method,
Carbon transformation/hook and bounded topology checks fail closed on drift.
Unavailable source registration raises a controlled error.

Contexts are immutable ordinary tables. Death has only optional Position:
Vector3, Killer: Player and KillerId: string; spawn has only optional Position:
Vector3. Positions are qualified completion-time root world observations, not
damage points, later reads or client poses. Only a qualified direct tracked human
initiator supplies killer fields; missing/environmental/indirect/NPC attribution
is ordinary nil. No HitInfo, raw Rust/Unity object, cause, weapon, damage type or
private life token crosses the event transport. Snapshot values confer no host
capability; captured Player facades keep their defining owner and original D11
connection, never a delivery-time UserId re-resolution.

Host observation captures bounded immutable identity/scalars only and never
recursively enters Luau. Existing scheduler admission gives each callback fresh
VM/domain/deadline authority and pre-entry validation. A disconnected victim,
disconnected listener or retired owner suppresses queued work; a reconnect cannot
inherit it. A later killer disconnect does not retarget its captured proxy or
cancel an otherwise eligible victim observation. No registration, replacement,
provider reconstruction or fatal recovery replays history.

D7/D10 publication includes CarbonLuau-owned callback roots and registrations,
not arbitrary Lua mutations. Committed listener demand remains authoritative
while nested publication is open; provisional connections receive no backlog.
Failures restore existing owned callback roots and discard newly staged ones.
Hard global producer, visit, fanout, queue, payload and retained-reservation
bounds are centralized in GameplayEventPolicy and qualified in the evidence
record. Cancellation retains native-held accounting until consumption/destruction;
refund is exact-owner, at most once. Overload drops are counted, not retried.
Wrong-thread rejection accesses no host, VM, logger or frame collection; bounded
atomic diagnostics are reported later from an existing owner-thread boundary.

[Gameplay A qualification](../GameplayFoundationA-Validation.md) distinguishes
exact-host server traces, synthetic managed/native tests, sanitizers, hosted CI
and authenticated-client evidence. Its experimental identity does not qualify
arbitrary Rust/Carbon builds or client outcomes. Later gameplay/entity/inventory
signals and loot/damage policies require separate scoped work.

The separately authorized B2 addition is owned by D20 and
[GameplayB2-Validation.md](../GameplayB2-Validation.md). It shares this bounded
transport but has its own fourteen-field private shape and frozen seven-field
historical public context. B2's original-action melee attribution qualification
does not redefine A's already-qualified Player context or infer a universal
actor from A's fields. No new provider protocol or native export follows.

Gameplay D2A separately implements a **private-only** one-prefab declarative
loot policy, with qualification status owned by
[GameplayD2A-Validation.md](../GameplayD2A-Validation.md). It introduces no Loot
service, Luau declaration, public enum, Signal or new mutation method. Immutable
bounded rule descriptors use existing D7 inactive-session staging, nested
rollback and exact outgoing-owner replacement; conflicts are preflighted before
native and managed publication. D10 forbids active/foreign facade laundering.
Only a committed exact owner may select a rule for a new occurrence.

On the exact qualified hosts/prefab, a pinned outer OnDied frame and common
StorageContainer payout cut replace the destruction's stored/bonus/bag paths
while leaving normal removal processing intact. A small pinned factory fence
captures genuine temporary world returns before initialization. The existing
D20 BeginSpawn attempt/completion/epoch machinery supplies identity, not another
entity model or raw script capability. Failed epochs may be cleaned only with
unchanged original attempt identity; they are never admitted. Current identity,
UID, reciprocal ownership and B3 removal certification govern verification.

Suppression begins nontransactional COMMIT. No post-COMMIT vanilla fallback,
automatic replay, compensation or rollback claim is allowed. Already-completed
host-owned payouts survive later failure; incomplete or vetoed cleanup is an
aggregate diagnostic, not false success. D13's inaccessible unreturned-host
resource boundary and I12 remain authoritative. Before-COMMIT budget/source
ineligibility leaves vanilla untouched. Numeric private limits and exact targets
are centralized in LootRulePolicy and the qualification record, not new public
guarantees. Future public naming is `Enum.DropMode`; global enum redesign,
collection/fill-only aliases, actor attribution and D3 public exposure remain
separately scoped. Existing Player/C1/Workspace/persistence contracts are unchanged.

Gameplay D3 adds only the separately authorized public declaration
`game:GetService("Loot"):RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)`
over that same qualified adapter; [D3 qualification](../GameplayD3-Validation.md)
owns its implementation/platform status. It returns no value and is
initialization-only: registration stages CarbonLuau-owned policy, not a physical
payout, and successful publication alone makes the copied descriptor selectable.
The exact defining domain/host/VM owns the rule; sharing a service or closure
neither transfers ownership nor launders publication authority. Failed first-load
modules roll back staged rules even when caught. Retired owners cannot register;
healthy same-owner candidate replacement may supplant its outgoing rule, but a
foreign committed owner conflicts deterministically before publication.

`Enum` and `Enum.DropMode` are frozen namespaces. `Replace` is an immutable typed
DropMode userdata, not a number/string/record, dynamically created enum or raw
host object. Existing `GiveItemBehavior` spelling/type/behavior is unchanged.
No DropMode global alias, introspection API or other enum member is introduced.

Exactly three arguments are required. Drops is a plain dense array of zero to
eight plain `{Item, Min, Max}` records with no unknown keys/metatables. Bounded
raw inspection rejects cycles/unsupported values/sparse arrays without invoking
author metamethods. Only copied bounded scalars cross the host boundary, followed
by the existing immutable managed validation. Table inspection cannot become
unbounded: before each iteration a private pinned-VM
guard caps each table's array and hash backing capacities at 64 slots each.
Oversized/preallocated or formerly large tables are a controlled resource error;
use small fresh declarations. This adds no public VM/object introspection.
Later caller table mutations cannot change the rule. Only the D2A-qualified full prefab and `scrap` /
`metal.fragments` are allowed; integer inclusive `0 <= Min <= Max <= 1000`, sum
of Max <=8000, one rule/domain and one global owner remain authoritative.
Empty/all-zero declarations suppress the eligible destruction payout. Producer
bounds remain eight worlds/occurrence, eight occurrences/actual frame and one
in-flight frame; excess or unavailable source is pre-COMMIT ineligibility with
vanilla unchanged, never a claim that an unlimited burst was replaced.
Strict Replace does not reclaim earlier generation-time world drops. D2A's exact
host, source eligibility and nontransactional partial/indeterminate/no-replay/
no-compensation envelope is unchanged. There is no public completion callback,
Signal, conditional breaker policy, additional prefab, fill-only/collection mode,
new scheduler or release/tag authority. D3 stays in `0.6.5-experimental`.

<!-- END RECORD Invariants.md -->

## Gameplay D4 — qualified post-release prefab expansion

The separately authorized [D4 record](../GameplayD4-Validation.md) extends only
the strict Replace allowlist to exact autospawn barrel-2, ID `555882409`, alongside
barrel-1, ID `966676416`. This supersedes the historical one-prefab scope above,
not its publication, ownership, failure or resource contracts. A descriptor
retains its exact declared full prefab. Host admission requires the corresponding
exact name/ID pair and existing qualified plain LootContainer/settings/lifetime
premises. A nonmatching lookup returns no owner or rule.

There is still one global rule and one per domain, not one per prefab. Same-owner
publication can change the target; foreign owners conflict under the retained
global cap. The per-frame budget is shared and never reset by profile replacement.
The three existing adaptation targets, PREPARE/COMMIT/VERIFY, D20 authority,
I4/I12 and strict destruction-time/no-fallback/no-replay/no-compensation behavior
remain unchanged. Natural bag behavior is not claimed; the record distinguishes
its controlled physical branch test. No new mode/item/Signal/attribution, release
or identity change follows. Published v0.7.0 remains immutable and barrel-1 only.

## Gameplay D5 — qualified post-release item expansion

The separately authorized [D5 record](../GameplayD5-Validation.md) owns the
additional `wood` item's exact-host qualification status. Both pinned hosts
prove the name/ID definition pair `wood` / `-151838493`, stackable 1000, ordinary
generic dropped worlds, original UID/reciprocal ownership and supported cleanup.
Its Burnable creation callback sets only the existing scalar fuel default;
XPWhenUsed and ApartmentTax inherit empty creation/world/remove callbacks.
No extra held/associated entity, nested contents, condition, skin or public fuel
configuration is introduced. Unrelated consumption/tax behavior is not qualified.

Only the native declaration and managed item allowlist checks expand. D2A/D4's
three-target adapter, D3's immutable descriptors/staged publication and D13/D20/
D23/I12 authority, failure and lifetime contracts remain unchanged. The declared
Max must still fit the actual definition stack limit before COMMIT; a changed
stack premise is pre-COMMIT ineligibility with vanilla untouched. There is no
splitting into extra worlds or post-COMMIT fallback/replay/compensation.

One global rule and one per domain remain; all existing numerical budgets are
shared unchanged. Independent per-prefab ownership is the separately deferred
[D6 proposal](../design/GameplayD6-PrefabRuleRegistry.md). Other definitions,
prefabs, modes, generic transactions and attribution are outside D5. Published
v0.7.0 tag/assets and package/API/ABI/provider/schema/Luau identities are unchanged.

