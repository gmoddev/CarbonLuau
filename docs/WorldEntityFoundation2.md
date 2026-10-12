# World/Entity Foundation 2: bounded asynchronous complete discovery

The completed-Spawn catalog and asynchronous traversal are qualified, with the
public callback API separately qualified and combined closure recorded below.
Current authority is [D20](invariants/WorldEntity.md); public authors use the
[discovery guide](api/Discovery.md), not the historical sparse-grid proposal.

## Current model

```text
qualified completed Spawn -> fixed nonspatial catalog membership
accepted query -> bounded slots each owner-thread turn
candidate encountered -> exact lifetime validation + bounded position observation
bounded filter/results -> fresh callback admission and exact result conversion
```

Membership comes from the lifetime substrate, not lazy proxies, network-group
subsets or mutation-unsafe registry enumeration. Work, retained state, results,
concurrency, total inspection and deadlines have hard limits owned by the 2A/2B
records. Overflow or unsafe/unavailable state is controlled failure, not a
successful incomplete subset. Retirement cancels without replay or retargeting.

Candidates are evaluated when encountered. The query is not an atomic world
snapshot: trusted Rust/Unity/Carbon/Oxide/physics scalar changes can race an
observation. CarbonLuau-owned operations preserve deterministic ordering.
Those races never relax exact lifetime/memory/resource safety or permit arbitrary
unsupported off-thread structural mutation. The qualified bounded no-wait read
does not use timing measurements, catches or post-checks as a substitute for the
supported storage premise.

## Public API and qualification

[Workspace:GetEntitiesInRadiusAsync](api/Services/Workspace.md#getentitiesinradiusasync)
uses a required callback and optional bounded discovery options. Internal catalog
slots, watermarks, generations and lifetime tokens are not public.

| Record | Scope |
|---|---|
| [2A](WorldEntityFoundation2A.md) | Catalog membership, exact-host bounded observation and private traversal policy |
| [2B](WorldEntityFoundation2B.md) | Public arguments/options/errors, callback authority and cancellation |
| [2C](WorldEntityFoundation2C.md) | Combined lifecycle, scale, security, final-source closure and package-readiness handoff |

The same initial-startup/restart requirement as [Foundation 1](WorldEntityFoundation1.md)
applies. Development introduction is `0.6.0-experimental`; published `v0.5.0`
does not contain discovery. [Planned package notes](releases/0.6.0.md) do not
authorize a release or assign package identity by matching the API number.

## Preserved rejected approaches

The [original Foundation 2 research](history/world/WorldEntityFoundation2.md#worldentityfoundation2)
and [world history index](history/README.md#worldentity) preserve movement-index,
Unity-history, guarded-getter, registry-enumerator, catalog and position-alternative
findings. Do not repeat them unless a demonstrated implementation defect changes
their conclusions. They are not active preconditions for the qualified traversal.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="worldentity-foundation-2--bounded-asynchronous-complete-discovery-research"></a>

- [World/Entity Foundation 2 — bounded asynchronous complete discovery research](history/world/WorldEntityFoundation2.md#worldentityfoundation2-worldentity-foundation-2--bounded-asynchronous-complete-discovery-research)

<a id="preserved-pre-pivot-sparse-grid-research"></a>

- [Preserved pre-pivot sparse-grid research](history/world/WorldEntityFoundation2.md#worldentityfoundation2-preserved-pre-pivot-sparse-grid-research)

<a id="authority-and-exact-target"></a>

- [Authority and exact target](history/world/WorldEntityFoundation2.md#worldentityfoundation2-authority-and-exact-target)

<a id="exact-host-primitive-inventory"></a>

- [Exact-host primitive inventory](history/world/WorldEntityFoundation2.md#worldentityfoundation2-exact-host-primitive-inventory)

<a id="why-the-tempting-radius-api-is-not-yet-honest"></a>

- [Why the tempting radius API is not yet honest](history/world/WorldEntityFoundation2.md#worldentityfoundation2-why-the-tempting-radius-api-is-not-yet-honest)

<a id="resource-model-and-measurements"></a>

- [Resource model and measurements](history/world/WorldEntityFoundation2.md#worldentityfoundation2-resource-model-and-measurements)

<a id="conditional-public-semantics-not-adopted"></a>

- [Conditional public semantics, not adopted](history/world/WorldEntityFoundation2.md#worldentityfoundation2-conditional-public-semantics-not-adopted)

<a id="smallest-owned-index-candidate"></a>

- [Smallest owned index candidate](history/world/WorldEntityFoundation2.md#worldentityfoundation2-smallest-owned-index-candidate)

<a id="required-proof-and-next-research-task"></a>

- [Required proof and next research task](history/world/WorldEntityFoundation2.md#worldentityfoundation2-required-proof-and-next-research-task)

<a id="research-validation-and-publication-state"></a>

- [Research validation and publication state](history/world/WorldEntityFoundation2.md#worldentityfoundation2-research-validation-and-publication-state)
