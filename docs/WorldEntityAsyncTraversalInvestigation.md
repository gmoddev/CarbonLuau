# World/Entity Foundation 2 — bounded asynchronous complete traversal

## Current disposition — 2026-10-04

[Foundation 2A](WorldEntityFoundation2A.md) owns current private complete-traversal
implementation, bounded policy, owner-thread pumping and final qualification.
Its selected position producer is the pinned supported-host owner-thread direct
TRS borrow: actual ancestry, at most 65 records, finite managed composition and
whole-query failure on invalid data or excess depth. Trusted external scalar/job
observation races do not require an atomic snapshot or universal writer fence;
exact admission, structural-storage lifetime and hard work bounds remain required.
The latest Linux fixed-pump live PASS and still-pending Windows status are routed
through the [observation-boundary disposition](WorldEntityObservationBoundaryInvestigation.md#current-disposition--2026-10-04).
Final Foundation 2A PASS is contingent on its final gates; no public API or change
to existing public Position semantics is implied by this note.

## Preserved investigation record (historical scope)

All proposals, negative evidence and probe results below are preserved. Their
"current", "NEEDS REVIEW" and missing-read-primitive statements describe their
original research stages, not the disposition routed above.

**Current continuation:** [trusted observation races and bounded access](WorldEntityObservationBoundaryInvestigation.md)
separates I12's observation boundary from hard lifetime/memory safety and bounded
reads. It does not reopen catalog/collection design. Older snapshot-phase and
writer-serialization requirements below retain their historical scope; they are
not mandatory freshness gates under the revised boundary.

**Latest direction:** the user selected a bounded nonspatial **lifetime-fed
catalog**, not guarded registry enumeration. [The catalog investigation](WorldEntityCatalogInvestigation.md)
now owns the active fixed-slot/birth-watermark proof, Foundation 1 integration
findings and narrow candidate-read gate. The direct traversal and ordered-tree
alternatives below are preserved earlier proposals, no longer the preferred plan.

**FOUNDATION 2 ASYNC TRAVERSAL NEEDS REVIEW — no qualified universal snapshot
phase or bounded Transform-job admission/read primitive is established.** The catalog
record's 2026-10-04 continuation adds opt-in direct real-model integration and
Windows/Linux tests; production activation remains gated. Historical collection
findings below are preserved, not reopened. The latest
[position-alternatives continuation](WorldEntityPositionAlternativesInvestigation.md)
owns cached-source, phase and snapshot-job evidence; guarded-getter serialization
is a preserved earlier route, not the only architecture now under consideration.

Research date: 2026-10-03. Starting checkout:
`f36ff7c83a9e273816f7b726fe3062618b430539`, with the intentionally
uncommitted Foundation 2 research preserved. This record pivots the active
research direction; it does not qualify or implement Discovery-2A.

## Decision and scope

The user rejects network-group subsets and now prefers a complete asynchronous
world traversal over a continuously fresh spatial index. The old sparse-grid
and [writer-side movement investigation](WorldEntityDiscoveryMovementInvestigation.md)
remain negative evidence, not prerequisites to be repeated. No movement
notification is necessary when each candidate's current root Position is read
when encountered. No atomic world snapshot is promised.

[D20](Invariants.md#d20--worldentity-foundation-1) retains Foundation 1's exact
lifetime and point-lookup rules. The amended future-discovery work rule permits
total O(world population) work only when spread across bounded owner-thread
turns, with hard aggregate work, retained memory, result, total-inspection and
deadline limits. A result cap alone remains insufficient. No synchronous
whole-world query, generic query engine, public cursor, Signals, Spawn, Destroy,
prefab index or native Unity patch is authorized.

## Exact collection evidence

The inspected Foundation 1 target remains Rust build `25653776`, protocol
`2634.289.1`, revision `166494`; Windows Carbon `2.0.262.0` / `8a81d70`, Linux
Carbon `2.0.261.0` / `c74c4ca`, Harmony `2.4.2.0`. This is read-only assembly
research, not a new live host qualification or support expansion.

| Assembly | Windows SHA-256 | Linux SHA-256 |
|---|---|---|
| `Assembly-CSharp.dll` | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| `Facepunch.System.dll` | `452a1f0646ef6656951601c8b7bf4a351e6b2b31962b47e8defd8acf7f144a1f` | `cf42f5749758ac553102684fb7657a4f5f039e2d5f574a83567b9b9059cf5695` |

`Facepunch.System.dll` MVIDs are Windows
`d694f505-4957-4785-993e-8017a63d587c` and Linux
`7d561100-4aa3-48e7-9821-3be6bbae6a1c`.
The existing read-only `tools/Research-WorldDiscoveryHost.ps1` inspected the
following exact bodies. It is not a runtime compatibility checker.

| Body/token | Observation |
|---|---|
| `EntityRealm.GetEnumerator`, `0x060051e7` | Enumerates the live `ListDictionary.Values` `BufferList`; not an immutable snapshot. |
| `EntityRealm.RegisterID`, `0x060051e2` | Adds a missing ID, or replaces the value for an existing ID without changing count. |
| `EntityRealm.UnregisterID`, `0x060051e3` | Calls `ListDictionary.Remove`. |
| `EntityRealm.Clear`, `0x060051ea` | Clears the collection. |
| `ListDictionary.Remove(int, key)`, `0x0600005c` | Swaps the last key/value into the removed index and updates the index maps. |
| `BufferList.RemoveUnordered(int)`, `0x06000018` | Copies last element into removed slot, clears last slot, decrements count. |
| `BufferList.Enumerator.MoveNext`, `0x0600020a` | Increments an integer index and compares against live count. No mutation version. |

Both platform bodies exhibit the same relevant removal/enumeration mechanics.
`EntityRealm.entityList` is private. An exact-field reference inventory found
eight instructions in eight methods: Count, constructor, Contains, Find,
RegisterID, UnregisterID, GetEnumerator and Clear. Only the constructor writes
that field. A separate exact `serverEntities` field inventory found 153
instructions in 142 methods on each platform; the sole inspected Rust-assembly
assignment is `BaseNetworkable..cctor`, token `0x060051d5`. The field is public,
not immutable. These inventories narrow the candidate adapter; they do not
prove absence of aliases, reflection, other assemblies, patches or inlined calls.

Reproduction uses `-TypePattern '.*'` with anchored
`-ReferencePattern '^HiddenValue.*BaseNetworkable/EntityRealm::entityList$'`
or `'^BaseNetworkable/EntityRealm BaseNetworkable::serverEntities$'`.
Unanchored field-name searches can include unrelated fields or branch-target
text and are not used as exact-field closure evidence.

### Why a naked cursor fails

With values `[A, B, C]`, inspect A, then remove A. Swap removal produces
`[C, B]`. Resuming at index 1 inspects B and permanently misses continuously
present C. Remove A and append D instead produces `[C, B, D]`: count is
unchanged, so count comparison also misses the discontinuity. Existing-ID
replacement is another count-neutral change.

Holding the iterator across turns does not repair this. Restarting enumeration
and skipping the first K values rescans work and still does not identify stable
membership. Copying the world synchronously or retaining arbitrary historical
buffers is not the bounded solution. Numeric network-ID seeking also lacks a
dense monotonic domain: Foundation 1 explicitly supports explicit-ID restoration.

## Earlier candidate: guarded direct traversal (superseded preference)

The earlier proposal preferred a private optimistic cursor over the authoritative live values, with a
host-lifetime-local membership generation. **Any relevant membership mutation
invalidates the entire query; there is no automatic restart.** A controlled
`WorldChanged` failure is honest, not a successful subset. Ordinary movement
does not invalidate membership and requires no notification mechanism.

Candidate bookkeeping contains exact host/realm identity, weak collection
identity, captured generation, initial count, next array index, total inspected
count, deadline, callback authority and at most R weak result identities. It
must not retain a full snapshot or host objects through an old iterator.
Bounded direct access to the private values requires an exact-host adapter;
public `IEnumerable` alone provides no seek primitive. Obtain one short-lived
strong reference for the current slice only. Count/accessor/collection identity
must be checked before indexing; the raw BufferList indexer does not certify
logical count. No reflection capability is exposed to scripts.

The proposed fence increments generation **before** every relevant RegisterID,
UnregisterID and Clear attempt. Conservative invalidation for no-op, skipped or
failed calls is acceptable. Counter wrap disables discovery rather than creating
ABA. Exact realm/collection replacement invalidates pending queries. Adapter
qualification must also exclude admission or inspection inside an unfinished
mutator. A prefix stamp alone is insufficient if a query can start after that
stamp but before mutation finishes. Either prove such reentrancy impossible or
maintain a bounded active-mutation depth with exact cleanup on normal, skipped,
throwing and nested exits; nonzero depth forbids admission, slices and delivery.
An unmatched exit disables discovery, never silently clears the guard.
Adapter installation/order, inlining, alias exposure, all supported mutation paths and
owner-thread serialization must be structurally and live-qualified. A detected
off-thread path, observer gap or patch drift disables discovery; merely detecting
an off-thread mutation after a racing read is not a synchronization proof.
The existing Spawn observer does not already supply this membership generation.
No new patch is installed by this investigation.

Each slice validates lifetime/authority/deadline/source generation, reads at most
B raw entries, and validates generation after potentially reentrant host calls
and before using another slot or publishing. Non-Entity, invalid, PENDING and
nonmatching entries all count toward inspected work. Successful stable traversal
visits all N initial slots exactly once without a seen-world set. Matching
results deduplicate by exact Entity lifetime token using at most R slots; a
possible registry alias must not produce duplicate public results. Generation
alone is not an Entity lifetime or a Spawn-completion witness.

Result deduplication alone does **not** establish once-only encounter evaluation:
an alias visited later could match after the first slot did not. The private
adapter must expose the parallel slot key as well as its value. Evaluate only
the unique slot whose key equals that object's captured current nonzero ID and
whose keyed lookup returns that exact object. The unique dictionary key and D20
admission predicate ensure every admissible object has that canonical slot.
Other aliases consume raw inspection budget but never trigger a second Position
evaluation. Parallel key/value integrity, equal lengths and this canonical-slot
rule are explicit adapter gates; no O(N) seen-lifetime set is hidden in O(QR).

### Encounter-time completeness

Success certifies evaluation of the entire unchanged registry membership, not
one network group, collider set or chosen prefix. For each candidate, reuse D20
exact admission and current keyed occupancy, read current Position and optional
exact Prefab, then revalidate exact lifetime across that read. Unavailable world
authority or unbounded host work is a controlled failure, not an empty result.

Eligibility and position are evaluated when encountered. An entity moved into
the sphere before its encounter is included; an entity moving after its encounter
is not inspected again. A failed/PENDING incarnation at encounter is ineligible
even if it later completes. No success promises all entities matching at start
or finish, much less every entity ever transiently matching during the interval.
This temporal distinction must be prominent in eventual author documentation.

Before completion entry, revalidate every retained exact lifetime and the query
authority. If a retained result no longer validates, fail the whole query rather
than silently dropping it or resolving its current ID occupant. Do not re-run
radius filtering at delivery: that would change encounter semantics without
revisiting nonmatches. Property reads after delivery retain ordinary D20 staleness
and current-Position semantics. Recheck source generation at final admission;
publication/entry must be serialized without a validation-to-entry gap.

Registry churn can starve this candidate of successful results. The deadline
forbids late success; expiry is processed at the next bounded owner-thread
opportunity, not during an owner-thread stall. Ordinary-world cancellation
rate and usefulness are **not measured**. Bounded failure is not a claim that this
strategy provides useful discovery on a continuously changing busy server.

## Earlier ordered-catalog alternative (fixed slots now preferred)

If controlled cancellation makes the smallest candidate inadequate, investigate
a **nonspatial** CarbonLuau-owned bounded membership catalog. Maintain weak
records in an ordered balanced tree keyed by monotonic registration sequence;
each query captures an upper sequence watermark and remembers its last sequence.
Successor lookup is bounded by tree height, not a sort or world scan per turn.
Removal deletes the node; a cursor is numeric, not a retained node pointer.
No unbounded tombstone/history log is needed. Re-registration gets a new membership
sequence even when D20's Entity lifetime itself has not changed.

Every continuously registered member of the initial cohort is encountered once;
removed-before-encounter members are omitted, new registrations after the watermark
are excluded. This is an explicit membership-cohort contract, not an atomic
snapshot or discovery of all arrivals during execution. Replacement at an
existing ID must remove the old membership and add a new sequence. Clear,
overflow and observer gaps fail closed. Initial catalog seeding must itself be
bounded and gap-free with subsequent registration; queries cannot run over an
incomplete bootstrap. Foundation 1's startup observer remains required.

This costs shared O(N) bounded membership memory and O(log N) mutation work,
versus the optimistic cursor's O(QR) query memory and constant generation mark.
It avoids the movement problem but still needs a qualified membership adapter,
hard catalog capacity, allocator/maintenance bounds and owner-thread lifecycle
proof. The finite sequence example below establishes only cursor algebra.
Do not quietly replace the optimistic contract with this cohort contract.

## Hard resource and scheduling contract to qualify

Use a single shared policy owner, not new mutable counters per facade:

- Q: host/global and per-domain pending query admission ceilings, including
  completed-but-undelivered callbacks. Reject before retaining unbounded state.
- B: maximum raw inspected entries per query opportunity; G: aggregate raw entry
  budget per host turn. Q times B must not bypass G. Charge setup, cursor lookup,
  validation, filtering, result revalidation, marshaling and cleanup separately.
- T: maximum total raw inspections per request. Count N above T rejects before
  traversal. No hidden retry/rescan resets this bound.
- R: hard result cap. The R+1st distinct match fails the whole query, with no
  truncation or partial success. Deduplication/sorting cannot grow beyond R.
- D: absolute monotonic deadline from acceptance through completion admission,
  including scheduling/queue delay; no success may enter after D. Expiry is marked
  on the next owner-thread opportunity, with bounded cleanup thereafter, not a
  hard wall-clock reclamation guarantee during a stalled host. A controlled
  timeout callback may enter later if its original authority is still valid.
  No slice resets it. Owner retirement discards
  callbacks rather than reporting into a replacement VM/domain.
- Memory: O(Q) cursor/authority state plus O(QR) weak result/dedup slots and one
  bounded completion construction. Release on rejection, expiry, error,
  retirement or shutdown. Cleanup and cancellation also need per-turn budgets.

Ready queries receive persistent round-robin opportunities across turns. New
queries cannot repeatedly jump ahead; per-domain admission/fair scheduling prevents
one addon monopolizing all global slots. Traversal host work and completion Luau
callbacks have distinct existing scheduler/deadline accounting. No disk/worker
thread is necessary: every Unity/Rust inspection stays on the owner thread.
Neither acceptance nor a yielded slice is completion. Callback errors/timeouts
do not replay the traversal. Terminal delivery is at most once.

**A raw-entry cap does not yet prove I8.** Candidate validation includes current
registry lookup, qualified Spawn-patch authority and Unity Position reads.
Existing Foundation 1 point-read evidence is preserved, but repeated reads need
an independently defensible per-candidate host-work/wait bound. An elapsed-time
check between candidates cannot interrupt a long native read. The prior research
already records ancestor/job-wait issues; no fresh movement-seam investigation
is needed. A host topology/synchronization bound or another qualified bounded
read mechanism must close this before claiming hard turn-work qualification.
Numeric B/G/Q/T/R/D policy is **not selected** from synthetic model constants.

## Small public proposal, not an implemented contract

```luau
Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number,
    Callback: ({Entity}?, string?) -> (),
    Options: { Prefab: string? }?) -> ()
```

One required callback, optional exact full Prefab filter, no Query DSL, Limit,
ordering, nearest-N, public generation/cursor, cancellation object or generic
enumerator. Validate finite center/radius and bounded option strings/types before
admission; no host path lookup. Geometry arithmetic must avoid overflow and use
one exact documented root-sphere comparison. Success is array plus nil error,
including an empty array; failure is nil array plus controlled error. Synchronous
argument/stale/admission errors use existing controlled conventions. Internal
assembly/patch/Unity exception details never become script error contracts.

Recommend committed-only submission using existing current-operation publication
eligibility, since this retains a future callback. A cold module may later export
a closure that submits from committed execution; it cannot launder a provisional
submission through foreign Workspace values. This is a proposed additive query
rule, not a change to Foundation 1's provisional read-only point lookup. Completion
must reuse existing exact VM/domain/callback owner-thread admission, never resume
the original operation, extend its deadline, or target a package-ID replacement.
Numeric policy and final callback shape require canonical closure before metadata.

## Evidence and exact remaining gates

`tools/Research-WorldTraversalModel.py` ran on the controlling Windows Python
runtime and `linuxbox` Python via stdin; no server, native patch or remote fixture
was installed. It reproduced naked-cursor skips and count-neutral churn, then
passed 144 mutation cases and 36 stable traversal cases. Additional assertions
cover replacement/Clear/addition, mutation during inspection, encounter-time
movement, canonical alias-slot evaluation, exclusion of active/nested mutation
windows, result/work limits, expiry including queued completion, retirement
before/after queueing, bounded exact-result deduplication, at-most-once successful delivery and three global budgets
with round-robin cursor advancement. A membership-watermark example passed.
These are finite synthetic tests, not exhaustive host correctness, memory/weak
retention, callback error delivery or live fairness qualification.

Model source SHA-256 after these runs:
`1b188dd436588389c8f670e6ae525cfb1ca2dfa82048fe6d31a2a27dc5b3970d`.
`Test-Api.ps1` (including relative links), `Test-Architecture.ps1` (including
GiveItem safety), `Test-Release.ps1` (identity/intended contents/deterministic
packaging) and `git diff --check` passed on the amended research checkout.
These repository checks are not new executable-package, live-discovery,
sanitizer or hosted-CI qualification. Production sources and public definitions
are unchanged, so no new production platform PASS is asserted.

Independent read-only review found three substantive specification gaps:
result-only deduplication permitted repeated alias encounters; prefix-only
generation allowed a possible unfinished-mutation admission window; and deadline
wording implied reclamation during a stalled owner thread. The canonical-slot
rule, active-mutation exclusion and completion-cutoff/next-opportunity expiry
wording above address those findings. Added finite model regressions passed on
both platforms. They do not qualify the corresponding real host adapter.

To close this research baseline, qualify the smallest exact-host generation and
bounded cursor adapter, then establish per-candidate host-work bounds and measured
policy. Test normal churn usefulness; if cancellation is inadequate, qualify the
catalog alternative with explicit cohort semantics instead. New live Windows/Linux
traversal, dense/resource, namespace/facade authority, VM/callback and sanitizer
evidence are **not measured**. No implementation prerequisite is satisfied merely
by the model. This is not a proof that bounded complete traversal is nonviable.

The next task is confined to the managed membership/continuation seam and bounded
candidate reads—not native movement interception, a new grid or broad Unity
patches. Existing research, fixtures and historical negative findings remain.
No production runtime, API metadata, release identity, commit or push is introduced
by this record.
