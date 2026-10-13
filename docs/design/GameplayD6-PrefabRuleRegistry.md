# Gameplay D6 — bounded per-prefab Loot registry proposal

Scope note: this is the preserved D5-era proposal, not current registry status.
The separately authorized [D6 qualification](../GameplayD6-Validation.md) owns
the implementation and exact source/host evidence; original deferred findings
below retain their historical scope.

Status: **DEFERRED; NOT IMPLEMENTED OR QUALIFIED**.
This is the optional registry investigation requested with Gameplay D5. D5 retains
one global rule and one rule per domain; item expansion does not require a registry
change. [D23](../invariants/Gameplay.md), [D7/D10](../invariants/Runtime.md),
[D4 qualification](../GameplayD4-Validation.md) and the public
[Loot contract](../api/Services/Loot.md) remain authoritative.

## Why this is a separate phase

The current `LootRulePolicy.GlobalRules` and `DomainRules` are both one.
`FacadeWorld.PreflightLootRules` excludes only the exact outgoing owner and
rejects a foreign rule even for a disjoint prefab. `PublishedLootRule` returns
one immutable descriptor, while `TryLootRule` inspects at most 129 sessions and
returns no owner/rule for a nonmatching target. These deliberate checks implement
the qualified global capacity; changing only the conflict check would leave
selection and staged publication inconsistent.

A per-prefab registry must independently qualify a bounded set of declarations,
nested publication checkpoints, root/addon candidate replacement, foreign-facade
authority, provider/required-dependency retirement and fatal reconstruction.
Allowing both rules in one domain would also change the current domain limit.
Those logical changes materially expand D5's item-definition qualification and
are deferred rather than mixed into its physical payout evidence.

## Smallest candidate for later qualification

Use only the two already-qualified exact full prefab paths. A finite registry
could allow one committed owner per prefab, with a total capacity of two and an
explicitly chosen domain capacity. Preserve ordinal keys and immutable copied
descriptors. No generic policy engine, callback, new prefab or mode is proposed.

Preflight the complete candidate set before native or managed publication.
Exclude only the candidate's exact outgoing owner; reject overlaps with any
other committed owner deterministically. A malformed/conflicting replacement
must leave all prior CarbonLuau-owned rules published and expose no partial set.
Do not claim rollback of arbitrary Luau or physical host effects. A selected
occurrence captures one descriptor and its exact owner at admission.

Keep the existing eight-entry/eight-world, amount/total, eight-occurrence actual
frame, one-in-flight and session inspection bounds shared across both prefabs.
Profile changes and owner retirement must never reset producer use. Registry
cardinality changes require their own measured retention/publication budget;
neither per-prefab nor per-domain multiplication is justified by this proposal.

## Required evidence

Test same-prefab conflicts, disjoint owners, candidate sets with one conflicting
entry, caught nested-module failure, failed native/managed publication, root and
addon replacements, provider/dependency unload, stale services and VM recovery.
Prove no provisional visibility, stale-owner selection or historical replay.
On both pinned live Carbon hosts, exercise concurrent barrel-1/barrel-2 strict
replacement under independent owners, source/Spawn/creation failure, and shared
frame exhaustion across profile/owner changes. Reuse the qualified physical
adapter and verifier without new host hooks. Complete affected real-VM,
sanitizer, tooling, package and final-source CI gates before changing public
limits or claiming coexistence of independent rules.

No registry implementation, numeric-limit amendment or new release follows from
this document. D5's recommended next phase is this separately scoped D6.
