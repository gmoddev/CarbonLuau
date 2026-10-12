# Persistence: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D21 — Persistence Foundation 1

Persistence is the next runtime foundation; architecture is resolved and the
private Persistence-1A substrate is qualified within the scope recorded in
[PersistenceFoundation1A.md](../PersistenceFoundation1A.md). Separately authorized
[1B](../qualification/Persistence1B.md#persistencefoundation1b) is implemented and qualified within recorded
scope; [1C](../PersistenceFoundation1C.md) owns combined closure and its exact final
verdict without changing D21 semantics. D12 assigns
this experimental development surface `0.5.0-experimental`, not retroactive
0.4 availability. D20's Entity-1A/1B runtime gates are closed, and Entity-1C
public development assignment is independent of Persistence. The exact
surface is `game:GetService("DataStoreService")`, synchronous disk-free
`GetDataStore(StoreName)`, and `DataStore:GetAsync(Key, Callback)`,
`SetAsync(Key, Value, Callback)`, `RemoveAsync(Key, Callback)`. Async means
non-yielding bounded submission and a separate later owner-thread callback,
not a suspended Luau frame. Immediate return acknowledges acceptance only.
[PersistenceFoundation1.md](../PersistenceFoundation1.md) owns the detailed
callback results/errors, logical names, value format, hard design limits and the
qualified allocated-file operational safety budget.

Persistent namespaces are host-selected Root or Addon/canonical stable PackageId,
not provider/version/domain/VM identity. Data survives replacement, retirement,
reload and restart; facade/callback authority does not. Reusing a stable package ID
intentionally resumes its data under the installed-provider trust model. Store
calls require current admitted domain == facade ResourceOwner. Sharing a facade
or invoking a foreign exported closure does not transfer private namespace
authority or switch D10's admission. No cross-addon shared store is approved.

GetDataStore may participate in provisional facade publication without I/O.
All async operations, including reads, require an existing nonprovisional admission
and **no active publication scope**, reusing the authoritative mutation predicate.
Writes are irreversible, never journaled as candidate publication or replayed.
Reads are conservatively committed-only because Foundation 1 introduces no
asynchronous initialization or yielding module model. Deferred work may submit
only after commit; failed candidates/modules never dispatch persistence work.

Values are bounded snapshots of booleans, finite binary64 numbers, UTF-8 strings,
dense arrays and string-keyed maps. Nil means absence, not a stored value. Reject
cycles, metatables, functions, threads, userdata and host objects. Preserve finite
double bits without coercion; Get returns fresh ordinary values. Snapshot/decoding
must check byte/depth/count/allocation/deadline bounds and execute no metamethods.
Names/keys are exact validated logical identifiers, never filesystem paths.

Select one private pinned SQLite database in a supervised storage helper, with
fixed parameterized operations, rollback PERSIST journaling and EXTRA
synchronization verified against qualified local Windows/Linux VFS/filesystem
behavior. Each connection must configure and verify these settings before Ready.
For the additive Foundation 2 private derived substrate, primary Ready still
requires the D21 SQLite/file/profile/schema/integrity, primary-envelope and
quota proofs. Full derived graph, representation and cleanup work is separate:
unverified derived generations are unavailable for Query and cannot defer
otherwise healthy Foundation 1 readiness. Physical database corruption or a
failed authoritative-primary proof remains D21-wide and fails closed.
Commit uses SQLite's retained-journal invalidation and synchronization, not
journal-file deletion. Do not use Carbon/Oxide direct JSON writes or its generic
SQLite queue as evidence of this contract. One transaction mutates a key and
durable quota accounting atomically. Success follows checked SQLite COMMIT after
its ordered journal, database and journal-commit-marker synchronization, and a
validated worker result; it never means queue or memory acceptance. Successfully
acknowledged transactions survive ordinary worker/server-process crashes when
reopening the same intact storage under the qualified filesystem/device
assumptions. OS-crash and power-loss durability are conditional on the complete
OS/VFS/filesystem/virtualization/device stack honoring synchronization, ordering
and namespace recovery; do not claim tested physical power-loss survival without
such testing. Hardware/OS dishonesty and catastrophic or external storage damage
are not covered. Uncertain post-dispatch mutations report Indeterminate, never
rollback/exactly-once/retry. Corruption or unsupported format fails closed and
preserves storage, including journals, for operator recovery, never defaults or
overwrites it. Retained journals count toward persistence resource accounting and
must not be manually deleted or zeroed during startup or shutdown. CarbonLuau
enforces hard logical quotas and qualified backend file-byte extent bounds.
Logical quota is an author-visible admission ceiling, not a reservation of
database or journal space: unused logical quota does not guarantee that an
otherwise valid mutation is physically admissible. CarbonLuau must not
knowingly exceed its canonical database/page or retained-journal byte extents
to satisfy unused logical quota. A proven pre-COMMIT extent/capacity failure
is a controlled StorageFull with no mutation; an uncertain outcome after COMMIT
begins remains Indeterminate and is never replayed. The physical ceilings are
independent private resource limits, not a reduction of the 16-MiB namespace
or 256-MiB global logical quotas. Qualified Foundation 1's 512-MiB database,
corresponding journal limit and 1,280-MiB operational budget remain its
historical baseline. The additive private Foundation 2A substrate uses a
262,144-page/1,073,741,824-byte database EOF ceiling, a
1,075,904,512-byte retained-journal EOF ceiling and a 2,560-MiB
filesystem-allocation operational budget. These larger private limits do not
change the Foundation 1 public API or logical quotas. The allocated-file budget
is a filesystem-qualified operational envelope
checked at startup, before dispatching a database operation and after its
completion; it is not an application-enforced never-exceeded physical disk quota.
Measurements cannot guarantee that an in-flight filesystem operation never
temporarily exceeds that envelope. On an observed budget violation, admit no
further work and preserve storage; a mutation whose commit outcome is uncertain
remains Indeterminate and is never replayed. Filesystem/device metadata,
reservations, allocation units and storage-stack overhead remain
filesystem-dependent, not bounded by a promise about database EOF. Foundation 1
does not claim an exact filesystem-wide or host-physical-footprint quota.

**Current qualification gate:** the historical DELETE/EXTRA Windows directory-sync
proof gap remains recorded in [PersistenceFoundation1A.md](../PersistenceFoundation1A.md).
[PersistenceDurabilityInvestigation.md](../history/persistence/Durability.md#persistencedurabilityinvestigation)
establishes the built-in PERSIST/EXTRA path for implementation, not production
qualification. The subsequent [private 1A closure](../PersistenceFoundation1A.md)
records startup/recovery, codec, quotas, backend extent/allocation-budget checks,
worker containment, failure handling and platform evidence within its stated
scope; it is not public-service qualification. The [durability amendment](../history/persistence/Durability.md#persistenced21amendment-proposed) was
explicitly approved on 2026-09-23; this changes no runtime or release identity.
The [physical-budget amendment](../history/persistence/PhysicalAllocation.md#persistencephysicald21amendment-proposed) was
also explicitly approved and adopted on 2026-09-23, conditional on scoped final
qualification. The 1,280 MiB figure is an operational safety budget,
qualification target and diagnostic threshold, not a hard physical invariant.
The [investigation](../history/persistence/PhysicalAllocation.md#persistencephysicalallocationinvestigation) observed no
breach; that is empirical evidence, not a theorem. The WAL startup rejection fix
is mandatory, including page-1 restoration through hot-journal recovery, before
unsupported conversion or WAL/SHM creation. Preserve supported hot-journal
recovery. [1A](../PersistenceFoundation1A.md) closes this fix and filesystem-profile
qualification under resolved D21; the later explicit 1B authorization is recorded
in [PersistenceFoundation1B.md](../qualification/Persistence1B.md#persistencefoundation1b).

One serial worker receives bounded bytes/scalars only. Per-namespace FIFO and fair
global dispatch, reserved completion capacity, rate/count/byte/disk limits and a
fixed request deadline bound work. Background I/O never touches Luau or host game
objects. Owner-thread intake revalidates host/VM/domain epochs and admits completion
later through existing fair, non-reentrant scheduling. No owner-thread file/process
wait or blocking OS mutex. Original admitted deadlines are unchanged; the later
callback is a new bounded admission, never a resumed timed-out operation.
Retirement drops undispatched work/stale callbacks; a dispatched write may commit.
Fence replacement work behind that transaction or confirmed worker death/recovery;
never accumulate stuck replacement workers or call native code after unload.

Foundation 1 excludes Update/transform, multikey transactions, key enumeration,
queries, TTL, shared/cloud/remote stores, arbitrary files/SQL, automatic object
persistence and Player/Entity serialization. Use existing D19 metadata ownership
when implemented, with no editor/preview storage backend. Persistence-1A is private backend/
codec/namespace supervision; 1B is public facade/admission; 1C is crash/lifecycle/
quota/platform/public closure. The 2026-09-23 user decision assigns persistence
to `0.5.0-experimental` under D12 independently of Entity. Original design adoption
and private 1A changed no identity; this later assignment does not establish PASS.

<!-- END RECORD Invariants.md -->

<!-- BEGIN RECORD Invariants.md -->

#### D22 — Persistence Foundation 2 — bounded derived indexes and Query

[PersistenceFoundation2.md](../PersistenceFoundation2.md) is the detailed authority.
D21 remains authoritative for durability, namespace ownership, primary persisted values,
publication, worker ownership, foreground queue/fairness, callback lifetime and primary-data
quotas.

Foundation 2 keeps the author model small:

1. Foundation 1 primary values remain authoritative.
2. Query uses CarbonLuau-owned disposable/reconstructable derived indexes.
3. Authors manage no persistence schemas, public versions, migrations or index lifecycle.
4. Optional GetDataStore(Name, { Indexes = { ... } }) string-list hints only request proactive
   bounded preparation/retention; Query never requires a prior hint.
5. Query requests are structured Luau data: required Field, optional Type, Equals, Min, Max,
   Direction, Limit and Cursor. Equals is exclusive with Min/Max; Min/Max are inclusive;
   booleans support equality only.
6. Public Query never falls back to scanning authoritative primary records.
7. Preparation/repair is bounded, resumable and online; ordinary Get/Set/Remove continue where
   D21 permits.
8. Foreground writes maintain building and active derived state through the single serialized
   worker.
9. An ACTIVE generation is usable only when its durable ACTIVE state and exact
   current-process verification/admission both hold. Worker restart clears all
   derived admission. After a successful Set/Remove, affected *admitted* ACTIVE
   state is transactionally correct or made unavailable before commit; durable
   ACTIVE metadata alone never authorizes Query. An unverified generation may
   retain its durable row while unavailable and must be reverified against the
   current authoritative primary state or withdrawn before readmission.
   A recoverably failed bounded proof turn suspends derived admission and
   background maintenance for that worker lifetime; it does not restart an
   otherwise healthy authoritative primary worker. Restart begins a fresh
   proof, never a reused process-local admission.
10. Query result size and execution work are hard-bounded.
11. Pagination uses opaque bounded keyset cursors; each call sees its own current snapshot, not
    a frozen multi-page snapshot.
12. D21 remains authoritative for persistence durability, namespace ownership, publication,
    worker ownership and callback lifetime.

The separate derived logical pool also provides a maximum, not a promise that
every state below it fits the private SQLite file extents. Controlled physical
capacity rejection does not authorize a stale ACTIVE index, a false primary
success, or silent Query fallback to primary scanning. The previous universal
logical-to-physical fit implication is superseded; the detailed Foundation 2
record owns the corrected admission and withdrawal rules. Candidate B/C split
investigations remain historical evidence, not the active implementation route.

The detailed record owns numeric limits, preparation admission, type selection,
cursor safety and implementation gates. Its [final correction audit](../history/persistence/PersistenceFoundation2-Validation.md#persistencefoundation2-validation)
separates architecture readiness from runtime qualification.

Foundation 2 joins the still-unpublished 0.5.0-experimental scripting identity. This decision
changes no package/tag/release. Implementation remains routed through Persistence-2A private
derived substrate, 2B automatic demand/hints, 2C structured Query and 2D combined closure.
Private Persistence-2A derived storage and process-local admission are implemented
under [2A qualification](../PersistenceFoundation2A.md). [2B](../PersistenceFoundation2B.md)
qualifies automatic demand and optional hints; [2C](../PersistenceFoundation2C.md)
implements public structured Query. Combined Foundation 2 closure remains 2D.

<!-- END RECORD Invariants.md -->

