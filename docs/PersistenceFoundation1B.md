# Persistence Foundation 1B — facade, admission and metadata

Status: **PASS — Persistence-1B within the recorded qualification scope**, 2026-09-23.
Starting checkout: `4f665a3c4d801c951227a2557ad0019dfbdb6856`.
[Validation](PersistenceFoundation1B-Validation.md) records the tested source,
platform results, source-qualified hosted CI and explicit limits. The actual
Windows server restart supplement passes. Persistence-1C remains NOT STARTED.

[D21](Invariants.md#d21--persistence-foundation-1) and the
[detailed contract](PersistenceFoundation1.md) own signatures, bounds, authority,
publication, snapshot, callback, error and durability rules. The
[private 1A closure](PersistenceFoundation1A.md) is inherited evidence within its
scope, not proof of the new public API or Carbon integration.

## Authorized scope and identity

1B adds only DataStoreService/GetDataStore and callback-based DataStore GetAsync,
SetAsync and RemoveAsync. Production native/bootstrap and managed admission paths
implement that surface. Docs/tooling work follows concrete bindings; it does not
create a storage preview backend or qualify the runtime by generating definitions.

The user explicitly resolved the release-planning stop on 2026-09-23:
Persistence Foundation 1 is assigned API `0.5.0-experimental`, not retroactive
0.4. [D12](Invariants.md#d12--scripting-and-protocol-identity) owns that decision.
Existing declarations retain earlier SinceApi values. The catalog validator's
SinceApi <= current rule remains intact. Recursive PersistedValue typing is a
metadata representation of the accepted value contract, not another runtime API.

Release.md and Test-Release.ps1 were audited: the package must match releaseVersion
and its tag, but the scripting API need not share their version. Therefore
releaseVersion/packageVersion/tag remain 0.4.0/v0.4.0; only apiVersion advances to
0.5.0-experimental. Package 0.5.0 is the intended future release identity. These
development artifacts are not authorization to tag or publish. The new reserved
`cl_domain_storage_completion` export independently requires native ABI 1.5;
provider 1.2, addon schema 1 and the Luau pin are unchanged. See [Release.md](Release.md).

## Implementation map

The canonical contract remains D21 and PersistenceFoundation1, not this record.
The implementation connects their existing owners as follows:

| Boundary | Implementation |
|---|---|
| Domain-bound acquisition, raw bounded snapshot conversion, callback roots, exact-lifetime completion, fresh decoding | `native/src/facade/PersistenceFacade.cpp` |
| Publication discard and VM/domain teardown | Existing Publication/VmState paths, with persistence retirement hooks |
| Owner-thread private byte/scalar transport | `Persistence/StorageFacade.cs`, `Persistence/NativeStorage.cs` |
| Accepted request/completion reservations and private backend admission | Existing `StorageQueue`, extended to retain bounded reservations through callback handoff |
| Ordinary scheduled callback admission and recovery | Existing NativeRuntime/ScriptHost/RuntimeDomain paths |
| Worker intake and plugin shutdown | Existing `CarbonLuau.Persistence.cs` / Main lifecycle |

Submission uses private facade operation 31. Its bounded frame carries only
project-owned scalar identities, UTF-8 names and the qualified codec envelope;
the managed binding checks the exact VM/domain/package namespace. Operation 32
releases a callback reservation using an allocation-free fixed frame, including
fatal cleanup. Neither operation exposes a public script opcode or namespace
selector. Once backend acceptance succeeds, the submission return path requires
no allocating response construction.

The ABI 1.5 completion export stages a bounded result against an existing native
reservation. It does not execute Luau. The ordinary owner-thread drain validates
the exact surviving authority and constructs Get values under the fresh callback
deadline. Retirement destroys callback roots without retargeting replacements;
the storage supervisor still settles the backend result. Completed-but-undelivered
results remain counted against 1A's admission limits. No parallel Luau accounting,
worker access to Luau objects, retry loop or second publication predicate was added.

The production adapter now releases the entire native host on an unexpected
persistence-intake/scheduled-drain exception. Stopping only its worker could leave
ready callback roots and a native VM alive. Tests execute those actual adapter
catch paths with narrowly injected Carbon glue failures.

Linux qualification also exposed a real unload defect: GCC emitted a GNU-unique
`std::make_shared` tag, causing the dynamic loader to pin the shared object after
`dlclose`. GNU builds of the production/preview bridge now use the private
`-fno-gnu-unique` option. An ELF dynamic-symbol test prevents recurrence; this is
not a Luau, storage, deployment or public API contract change.

## Docs and tooling integration

Production bindings and implementation-owned annotations are in
[bootstrap](../scripts/bootstrap.luau) and
[PersistenceFacade.cpp](../native/src/facade/PersistenceFacade.cpp). The existing
Core exporter consumes them along with ModuleLoader declarations and shared GUI
descriptors. The [catalog](../api/carbonluau-api.json),
[definitions](../generated/carbonluau.d.luau), LSP documentation and tooling
metadata are generated from those sources; no Markdown or runtime reflection
supplies signatures. Recursive alias representation is a narrow metadata-schema-1
extension, not a new runtime capability. Qualified persistence declarations are
Experimental and explicitly introduced in scripting API 0.5.0-experimental.

Existing introduction versions are frozen at their explicit earlier versions or
the prior 0.4 default; new persistence declarations explicitly name 0.5. Native
registration/annotation checks cover both directions, with negative drift fixtures.
Bounds stay in their existing production owners and D21; no private 1A extraction,
new accounting policy or editor database was introduced to populate metadata.

[Public docs and six examples](api/Persistence.md) cover Get, Set, Remove,
string Player keys, snapshots and failures. They report success only after the
appropriate callback. Preview rejects service acquisition after its normal native
authority check with the existing unsupported-host marker, mapped to
UnsupportedPreviewApi; no fake success or backend is provided. Real worker failure,
supervisor recovery and recursive-value LSP checks run on both platforms.

Development bundle construction includes the guide and three reference pages at
their repository-relative paths, alongside the six automatically included public
examples. Bundle checks require them and continue to exclude database fixtures.
This changes packaging contents, not package version or publication authority.

The validation record distinguishes generation/static checks, real pinned VM
tests, language-server/preview execution and actual Carbon observations. Metadata
status changes require regeneration and `--check-generated`; they cannot qualify
a runtime by themselves.

## Qualification handoff

1B qualification covers its public API/admission slice, including representative
runtime, worker, reload and fault cases. It is not overall Persistence Foundation
1 closure. Persistence-1C is **NOT STARTED** and remains the next separately scoped
work: the extended crash/lifecycle, quota/scale, long-run resource, final platform
and public-readiness matrix assigned by the canonical routing.
The phase matrix remains in [the design](PersistenceFoundation1.md#11-implementation-phases-and-validation-gates)
and [Compatibility](Compatibility.md#persistence-foundation-1-qualification).
No OS-crash, physical power-loss, Shockbyte or authenticated-client claim follows.

## Internal future query compatibility note

Queries remain deferred. The inspected private [backend](../native/src/persistence/Backend.cpp)
keys Records by `(Namespace, Store, Key)` WITHOUT ROWID, with Quotas/Totals
accounting and checked application_id/user_version. Set/Remove update the value
and quota state in the same BEGIN IMMEDIATE/COMMIT transaction. No public file
layout or row identifier is exposed. This leaves compatibility potential for a
separately designed and qualified migration adding per-store metadata/index
maintenance within that transaction, without changing current key/value behavior.
It is not implemented Query support or a compatibility proof for a future design.
Strict current schema/version checks reject unknown future formats instead of
silently accepting them; no automatic migration is implemented or required here.
Any later design must preserve private namespace authority, bounds and completion
semantics. This note reserves no Query name, syntax, parameters or result type.
