# World/Entity Foundation 1: current contract and qualification

Read-only exact lookup and Entity observations are implemented in development
API `0.6.0-experimental`; they are not in published package `v0.5.0`.
[D20](invariants/WorldEntity.md#d20--worldentity-foundation-1) is the canonical
lifetime/authority contract. This page routes that contract and evidence rather
than reissuing the superseded registry-continuity design.

## Public surface

```lua
local Workspace = game:GetService("Workspace")
local Entity = Workspace:GetEntityById(Id)
if Entity then
    print(Entity.Id, Entity.Prefab, Entity.Position)
end
```

Use the [Workspace](api/Services/Workspace.md) and [Entity](api/Types/Entity.md)
references for exact signatures, errors and identity behavior. There is no raw
BaseEntity/Transform, generic Instance hierarchy, public Spawn/Destroy or
implicit durable identity. Persistence stores application identifiers, not
Entity lifetime authority.

## Qualified lifetime and deployment

The full effective Spawn observer must be installed before world restoration.
Beginning another qualified Spawn fences the prior epoch; failed/PENDING attempts
are not admissible. Successful startup reconciliation and continuous observation,
not merely current fully-spawned/keyed flags, authorize pre-existing entities.
Admission and every operation revalidate exact object/epoch/registry/prefab and
host/VM/domain/publication authority. Old handles never rebind or revive.

First-time hotload after world startup or full plugin unload/reload requires
server restart before lookup. Root/addon replacement and VM recovery retain the
qualified host observer while their old facade authority retires. Host/patch drift
fails closed pending exact-host requalification. Registry churn is not by itself
another incarnation; observed occupancy loss may conservatively retire a handle.
Do not manufacture natural pooling evidence: the records distinguish controlled
cases from same-object reuse that was not naturally observed.

## Qualification routing

| Record | Scope |
|---|---|
| [1A](WorldEntityFoundation1A-Validation.md) | Exact-host startup/completion fence and private lifetime substrate |
| [1B](WorldEntityFoundation1B-Validation.md) | Real keyed lookup/read-only VM binding and stale authority |
| [1C](WorldEntityFoundation1C.md) | Combined lifecycle, public metadata/docs and development identity |
| [Foundation 2](WorldEntityFoundation2.md) | Separately qualified asynchronous complete discovery |
| [Gameplay B1](GameplayB1-Validation.md), [B2](GameplayB2-Validation.md), [B3](GameplayB3-Validation.md) | Separate completed-Spawn, death and removal Signal proofs |

These receipts retain their actual tested revisions, Windows/Linux host tuples
and client limits. Readiness does not create a package release or authorize later
world mutation/discovery/event phases.

## Original research

The [complete original design](history/world/WorldEntityFoundation1.md#worldentityfoundation1)
and [historical investigations](history/README.md#worldentity) retain rejected
registry-continuity, Spawn-prefix and base-only completion assumptions. They are
evidence of why the current fence is necessary, not current open implementation gates.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="carbonluau-worldentity-foundation-1"></a>

- [CarbonLuau World/Entity Foundation 1](history/world/WorldEntityFoundation1.md#worldentityfoundation1-carbonluau-worldentity-foundation-1)

<a id="1-baseline-and-evidence-classification"></a>

- [1. Baseline and evidence classification](history/world/WorldEntityFoundation1.md#worldentityfoundation1-1-baseline-and-evidence-classification)

<a id="exactcurrent-carbonluau-evidence"></a>

- [Exact/current CarbonLuau evidence](history/world/WorldEntityFoundation1.md#worldentityfoundation1-exactcurrent-carbonluau-evidence)

<a id="current-carbon-evidence"></a>

- [Current Carbon evidence](history/world/WorldEntityFoundation1.md#worldentityfoundation1-current-carbon-evidence)

<a id="current-rustoxide-generated-evidence"></a>

- [Current Rust/Oxide generated evidence](history/world/WorldEntityFoundation1.md#worldentityfoundation1-current-rustoxide-generated-evidence)

<a id="networkableid-representation-evidence"></a>

- [NetworkableId representation evidence](history/world/WorldEntityFoundation1.md#worldentityfoundation1-networkableid-representation-evidence)

<a id="position-and-entity-count-evidence"></a>

- [Position and entity-count evidence](history/world/WorldEntityFoundation1.md#worldentityfoundation1-position-and-entity-count-evidence)

<a id="2-host-model-adopted-by-carbonluau"></a>

- [2. Host model adopted by CarbonLuau](history/world/WorldEntityFoundation1.md#worldentityfoundation1-2-host-model-adopted-by-carbonluau)

<a id="basenetworkable-versus-baseentity"></a>

- [BaseNetworkable versus BaseEntity](history/world/WorldEntityFoundation1.md#worldentityfoundation1-basenetworkable-versus-baseentity)

<a id="registry"></a>

- [Registry](history/world/WorldEntityFoundation1.md#worldentityfoundation1-registry)

<a id="world-readiness"></a>

- [World readiness](history/world/WorldEntityFoundation1.md#worldentityfoundation1-world-readiness)

<a id="3-foundation-1-public-surface"></a>

- [3. Foundation 1 public surface](history/world/WorldEntityFoundation1.md#worldentityfoundation1-3-foundation-1-public-surface)

<a id="workspace"></a>

- [Workspace](history/world/WorldEntityFoundation1.md#worldentityfoundation1-workspace)

<a id="getentitybyid"></a>

- [GetEntityById](history/world/WorldEntityFoundation1.md#worldentityfoundation1-getentitybyid)

<a id="entityid"></a>

- [Entity.Id](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entityid)

<a id="entityprefab"></a>

- [Entity.Prefab](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entityprefab)

<a id="entityposition"></a>

- [Entity.Position](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entityposition)

<a id="deliberately-absent-generic-properties"></a>

- [Deliberately absent generic properties](history/world/WorldEntityFoundation1.md#worldentityfoundation1-deliberately-absent-generic-properties)

<a id="4-exact-host-lifetime-identity"></a>

- [4. Exact host-lifetime identity](history/world/WorldEntityFoundation1.md#worldentityfoundation1-4-exact-host-lifetime-identity)

<a id="validation-predicate"></a>

- [Validation predicate](history/world/WorldEntityFoundation1.md#worldentityfoundation1-validation-predicate)

<a id="poolingreuse"></a>

- [Pooling/reuse](history/world/WorldEntityFoundation1.md#worldentityfoundation1-poolingreuse)

<a id="entity-equality"></a>

- [Entity equality](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entity-equality)

<a id="5-facadepublication-lifetime"></a>

- [5. Facade/publication lifetime](history/world/WorldEntityFoundation1.md#worldentityfoundation1-5-facadepublication-lifetime)

<a id="candidate-and-module-publication"></a>

- [Candidate and module publication](history/world/WorldEntityFoundation1.md#worldentityfoundation1-candidate-and-module-publication)

<a id="6-cross-domain-sharing-and-replacement"></a>

- [6. Cross-domain sharing and replacement](history/world/WorldEntityFoundation1.md#worldentityfoundation1-6-cross-domain-sharing-and-replacement)

<a id="7-fatal-recovery-reload-and-server-lifecycle"></a>

- [7. Fatal recovery, reload and server lifecycle](history/world/WorldEntityFoundation1.md#worldentityfoundation1-7-fatal-recovery-reload-and-server-lifecycle)

<a id="vm-fatal-recovery"></a>

- [VM-fatal recovery](history/world/WorldEntityFoundation1.md#worldentityfoundation1-vm-fatal-recovery)

<a id="carbonluau-reload"></a>

- [CarbonLuau reload](history/world/WorldEntityFoundation1.md#worldentityfoundation1-carbonluau-reload)

<a id="server-startuphotload"></a>

- [Server startup/hotload](history/world/WorldEntityFoundation1.md#worldentityfoundation1-server-startuphotload)

<a id="shutdown"></a>

- [Shutdown](history/world/WorldEntityFoundation1.md#worldentityfoundation1-shutdown)

<a id="saveload-and-restart"></a>

- [Save/load and restart](history/world/WorldEntityFoundation1.md#worldentityfoundation1-saveload-and-restart)

<a id="8-ownership-model"></a>

- [8. Ownership model](history/world/WorldEntityFoundation1.md#worldentityfoundation1-8-ownership-model)

<a id="host-owned-entities"></a>

- [Host-owned entities](history/world/WorldEntityFoundation1.md#worldentityfoundation1-host-owned-entities)

<a id="future-carbonluau-created-entities"></a>

- [Future CarbonLuau-created entities](history/world/WorldEntityFoundation1.md#worldentityfoundation1-future-carbonluau-created-entities)

<a id="9-discoveryquery-model"></a>

- [9. Discovery/query model](history/world/WorldEntityFoundation1.md#worldentityfoundation1-9-discoveryquery-model)

<a id="why-enumeration-is-deferred"></a>

- [Why enumeration is deferred](history/world/WorldEntityFoundation1.md#worldentityfoundation1-why-enumeration-is-deferred)

<a id="why-spatial-queries-are-deferred"></a>

- [Why spatial queries are deferred](history/world/WorldEntityFoundation1.md#worldentityfoundation1-why-spatial-queries-are-deferred)

<a id="10-lifecycle-signals"></a>

- [10. Lifecycle Signals](history/world/WorldEntityFoundation1.md#worldentityfoundation1-10-lifecycle-signals)

<a id="11-prefab-identity-and-future-prefab-input"></a>

- [11. Prefab identity and future prefab input](history/world/WorldEntityFoundation1.md#worldentityfoundation1-11-prefab-identity-and-future-prefab-input)

<a id="12-spawn-research-and-decision"></a>

- [12. Spawn research and decision](history/world/WorldEntityFoundation1.md#worldentityfoundation1-12-spawn-research-and-decision)

<a id="13-destroy-research-and-decision"></a>

- [13. Destroy research and decision](history/world/WorldEntityFoundation1.md#worldentityfoundation1-13-destroy-research-and-decision)

<a id="14-publication-and-irreversible-mutation-policy"></a>

- [14. Publication and irreversible mutation policy](history/world/WorldEntityFoundation1.md#worldentityfoundation1-14-publication-and-irreversible-mutation-policy)

<a id="15-i12-and-reentrancy"></a>

- [15. I12 and reentrancy](history/world/WorldEntityFoundation1.md#worldentityfoundation1-15-i12-and-reentrancy)

<a id="16-error-model"></a>

- [16. Error model](history/world/WorldEntityFoundation1.md#worldentityfoundation1-16-error-model)

<a id="17-hard-safety-bounds"></a>

- [17. Hard safety bounds](history/world/WorldEntityFoundation1.md#worldentityfoundation1-17-hard-safety-bounds)

<a id="public-input"></a>

- [Public input](history/world/WorldEntityFoundation1.md#worldentityfoundation1-public-input)

<a id="host-work"></a>

- [Host work](history/world/WorldEntityFoundation1.md#worldentityfoundation1-host-work)

<a id="prefab-snapshot-bound"></a>

- [Prefab snapshot bound](history/world/WorldEntityFoundation1.md#worldentityfoundation1-prefab-snapshot-bound)

<a id="identity-bookkeeping"></a>

- [Identity bookkeeping](history/world/WorldEntityFoundation1.md#worldentityfoundation1-identity-bookkeeping)

<a id="18-performance-model"></a>

- [18. Performance model](history/world/WorldEntityFoundation1.md#worldentityfoundation1-18-performance-model)

<a id="19-future-specialized-capability-seam"></a>

- [19. Future specialized-capability seam](history/world/WorldEntityFoundation1.md#worldentityfoundation1-19-future-specialized-capability-seam)

<a id="20-implementation-routing"></a>

- [20. Implementation routing](history/world/WorldEntityFoundation1.md#worldentityfoundation1-20-implementation-routing)

<a id="entity-1a--exact-identitylifetime-substrate"></a>

- [Entity-1A — exact identity/lifetime substrate](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entity-1a--exact-identitylifetime-substrate)

<a id="entity-1b--read-only-workspaceentity-surface"></a>

- [Entity-1B — read-only Workspace/Entity surface](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entity-1b--read-only-workspaceentity-surface)

<a id="entity-1c--combined-lifecyclescalepublic-closure"></a>

- [Entity-1C — combined lifecycle/scale/public closure](history/world/WorldEntityFoundation1.md#worldentityfoundation1-entity-1c--combined-lifecyclescalepublic-closure)

<a id="later-foundations"></a>

- [Later foundations](history/world/WorldEntityFoundation1.md#worldentityfoundation1-later-foundations)

<a id="21-version-planning"></a>

- [21. Version planning](history/world/WorldEntityFoundation1.md#worldentityfoundation1-21-version-planning)

<a id="22-explicitly-deferred"></a>

- [22. Explicitly deferred](history/world/WorldEntityFoundation1.md#worldentityfoundation1-22-explicitly-deferred)

<a id="23-resolved-decision-matrix"></a>

- [23. Resolved decision matrix](history/world/WorldEntityFoundation1.md#worldentityfoundation1-23-resolved-decision-matrix)

<a id="24-stop-conditions-retained-for-implementation"></a>

- [24. Stop conditions retained for implementation](history/world/WorldEntityFoundation1.md#worldentityfoundation1-24-stop-conditions-retained-for-implementation)

<a id="25-validation-requirements-and-qualification-routing"></a>

- [25. Validation requirements and qualification routing](history/world/WorldEntityFoundation1.md#worldentityfoundation1-25-validation-requirements-and-qualification-routing)
