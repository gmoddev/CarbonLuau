# Private Entity discovery traversal sidecar

Run `dotnet run --project tests/entity-discovery-model/EntityDiscoveryModelTests.csproj --configuration Release`.
The standalone project links the current `EntityLifetimeModel.cs` and the new
`EntityDiscoveryTraversal.cs`; it does not select a native reader, activate a
production catalog, enter Lua, or qualify host delegates.

## Opt-in managed benchmark

Append `-- --benchmark` to the command above. This prepares 262,144 populated,
strongly retained, untouched catalog identities and admits eight queries across four domain
keys. Injected limits are 1,024 units/raw slots per turn, 256 results, two
deliveries, and a 120-second monotonic deadline. The Skip lane measures catalog
traversal with a trivial producer; the Observed256 lane additionally constructs
managed observations, applies canonical validation/geometry, retains 256 matches
per query and exercises maximum-sized deliveries. Both observation and delivery
use the linked model's `TryObserveCatalog` with fake bounded host evidence.
There is no prior F1 admission, Binding/proxy creation, Unity/native reader or
global token lookup. Direct managed observation/validation is timed; real host
evidence and native position reading are not.

Each lane verifies 2,097,152 raw slots/producer calls, complete FIFO delivery,
per-query fairness and aggregate limits. Output separates preparation, first
turn, median/p95/worst turns, scan/delivery worst, measured turn allocations and
GC counts. TokenRecords, TokenSweep and NextToken are asserted zero before
scanning and after every turn, including delivery. CWT backing arrays/container
are checked for unchanged identity outside the timed interval. The test-only
reflection check supports the .NET/Mono backing layouts exposing these fields;
an unrecognized layout fails instead of silently omitting the assertion.
The observed lane follows the skip lane; neither is a rigorously
warmed microbenchmark. Wall-clock samples include JIT, GC and OS interruptions
and are not hard timing guarantees or production Mono/native-reader estimates.
Default invocation still runs only the deterministic model checks.
These include the real direct observer's invalid ID/prefab sticky retirement,
read-reentrant new Spawn, authority loss, disposal, read exception and weak host
death, in addition to forged receipt/birth/ID/prefab/candidate rejection.
Foundation 1 tests are separate and unchanged.

## Integration interface

Construct `EntityDiscoveryTraversal(Model, WorkPolicy)` on the model owner thread.
All policy limits are injected; no production defaults are supplied. The
authorized ceilings are eight admitted requests and two per nonzero DomainKey.
Use a key unique to the owning domain lifetime, not a reusable domain name.

`TryStart(Query, DomainKey, Observe, IsAuthorized, ValidateResult, Callback, Now, out RequestId)`
returns an admission status. Rejections invoke no callback. Query carries finite
world-space center XYZ, finite nonnegative Radius, optional exact Prefab, and
positive Limit. Prefab validation is strict bounded UTF-8, rejecting NUL and
malformed surrogates before acceptance.

`Observe` is `Func<MembershipCandidate, CandidateObservation>`. Return status
Skip for a legitimately ineligible/missing encounter, Failure for an unsafe or
failed read, or construct `CandidateObservation(Candidate, Birth, Id, Prefab, X, Y, Z)`
for Observed. Construction asserts no host qualification. The producer must
observe/revalidate the original candidate's exact lifetime through
`Model.TryObserveCatalog`, without F1 token/proxy acquisition. Birth is the
private immutable `Candidate.Birth`, not a public lookup token. Canonical
validation requires the exact current candidate and its latched valid evidence:
EvidenceCaptured, matching birth/ID and ordinal prefab equality. Construction
alone cannot create that evidence receipt. The scheduler applies the query
predicate to the copied sample. Positions are independent encounter observations,
not an atomic world snapshot and not resampled at delivery.

`IsAuthorized` is `Func<bool>` supplied by the host to cover domain/VM/publication
and host authority. `ValidateResult` is `Func<CandidateObservation, bool>` supplied
by the host to cover current keyed occupancy, captured ID/prefab agreement and exact lifetime authority for each
retained result; catalog membership alone cannot establish those facts. These
delegates must be bounded and qualified, with no Lua entry. Authority and model
continuity are checked again before successful delivery, followed by a
callback-free canonical-candidate pass. No native pointer is stored here.
Scheduler canonical checks use direct catalog state, not CWT probes, shared
token lookup or token sweeping. Foundation 1 lookup/acquisition remains separate.

`RunTurn(Now)` uses an injected monotonic nonnegative clock value in the same
units as DeadlineTicks. Its TurnWork reports units, raw slots, producer calls and
deliveries. `Cancel(RequestId)` marks cancellation. Deadline includes queued
delivery; expiration is inclusive. Ready requests retain quotas until detached
before validation/callback. A terminal delivery contains either all retained
matches or zero results with a failure status, never truncated/partial success.
At most one callback is attempted per accepted request. Callback exceptions are
contained and counted. The host callback must copy/admit bounded delivery data
for later Lua work, not recursively enter Lua. F1 token/proxy acquisition is not
part of this private traversal envelope.

`Dispose()` is idempotent owner-thread silent unload: no pending callbacks,
no subsequent admission/turn work, and O(MaximumQueries) detachment of callbacks,
delegates, result arrays, cursors, scratch and delivery queue. The caller owns the
model. Disposal from a private delegate terminates the current turn; completion
data already delivered is not revoked.

## Work and ordering envelope

One scan/control quantum costs one unit and inspects at most one raw catalog slot.
Holes, stale entries and post-watermark births consume the raw budget. Requests
are serviced round-robin. A delivery reserves `1 + 2 * Count` units: one bounded
host validation per result and one final model check per result, plus fixed
control. Policy requires that a maximum-size delivery fit in one turn. Selection
scans at most eight request slots per quantum; authority calls, bounded strings,
geometry and all host delegates require their own fixed per-unit qualification.
WorkPerTurn, RawSlotsPerTurn, MaximumTotalRawSlots and MaximumDeliveriesPerTurn
are separately enforced. Retained sample storage is at most eight injected
MaximumResults arrays, plus fixed request/queue/scratch state.

Terminal delivery is FIFO by readiness, not by submission when queries finish at
different times. Only completions already queued at turn entry are eligible for
that turn's delivery. Reentrant TryStart from a completion is allowed after the
old request detaches; newly accepted work cannot run until a later turn. Recursive
RunTurn is suppressed. This owner-thread managed envelope is not a theorem about
the time, allocation, memory safety or callbacks of an arbitrary injected host
producer/validator/completion action.
