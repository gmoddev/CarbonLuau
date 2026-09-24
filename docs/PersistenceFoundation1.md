# CarbonLuau Persistence Foundation 1

Status: **CANONICAL DESIGN BASELINE — PRIVATE 1A PASS; PUBLIC 1B IMPLEMENTED / QUALIFIED WITHIN RECORDED SCOPE; 1C NOT STARTED**,
2026-09-23. [Private 1A closure](PersistenceFoundation1A.md) records the qualified
startup/profile conditions of the approved physical-budget amendment. The later
user decision assigns Persistence Foundation 1 to API `0.5.0-experimental` under
[D12](Invariants.md#d12--scripting-and-protocol-identity), not retroactively to 0.4.
[Persistence-1B](PersistenceFoundation1B.md) records implementation, scoped local
qualification, implementation-source CI and remaining limits. Experimental
development availability is not overall Foundation 1 closure or release approval.

[D21](Invariants.md#d21--persistence-foundation-1) owns the architecture. This
document owns its detailed signatures, bounds, storage contract and phased gates;
the [validation record](PersistenceFoundation1-Validation.md) separates inspected
source evidence from proposed policy. Examples require the 0.5 development
bindings; historical 0.4 API artifacts do not provide persistence.

Persistence is the next runtime foundation; separately scoped implementation and
final qualification proceed under resolved D21, not another architecture investigation.
It does not depend on reopening D20: World/Entity remains
HOST-PRIMITIVE-GATED / DEFERRED, Entity-1A BLOCKED. Persist application identifiers
such as string Player.UserId, never Entity.Id as evidence of persistent identity.

## 1. Product and public contract

Local server storage for addon-owned configuration, progression, cooldowns, homes,
kits, currency and preferences. This is not Roblox cloud storage: no cross-server
replication, Roblox request budgets, user sessions or distributed locks.

The service is `DataStoreService`; the facade type is `DataStore`.

```luau
game:GetService("DataStoreService") -> DataStoreService
DataStoreService:GetDataStore(StoreName: string) -> DataStore

DataStore:GetAsync(Key: string, Callback: (Value: PersistedValue?, ErrorCode: string?) -> ()) -> ()
DataStore:SetAsync(Key: string, Value: PersistedValue, Callback: (Saved: boolean?, ErrorCode: string?) -> ()) -> ()
DataStore:RemoveAsync(Key: string, Callback: (Removed: boolean?, ErrorCode: string?) -> ()) -> ()
```

These signatures use contract notation. Development type definitions are
[generated from the production bindings](../generated/carbonluau.d.luau);
their availability is not runtime qualification.
**Async means callback-based, not yielding.** Each call validates, snapshots and
admits bounded work, then returns no values immediately. That return means only
request acceptance, never storage success. Callback is required; no fire-and-forget
write. Success is reported only by the later callback. No synchronous aliases,
Promise, wait/await, request handle or cancellation API in Foundation 1.

`GetDataStore` is synchronous, disk-free facade acquisition, not database/store
creation. Store identity is `(PersistentNamespace, exact StoreName bytes)`; repeated
acquisition uses that identity. Reference equality of facade objects is not public
store identity. Store facades have no Name/Path/Connection fields or Close method.

### Completion and errors

| Operation/outcome | Callback arguments |
|---|---|
| Get found | fresh decoded value, nil |
| Get absent, including never-written store | nil, nil |
| Set committed | true, nil |
| Remove committed and key existed | true, nil |
| Remove absent key | false, nil |
| Operational failure | nil, stable ErrorCode |

`false` is a valid stored value; nil is only absence. There is no default argument.
`SetAsync(Key, nil, ...)` is a programming error; use RemoveAsync. A missing Get or
Remove does not create namespace/store rows. A store is materialized only by a
successful first Set. Empty store/namespace accounting rows are removed in the
same transaction as their last key; retained data, not mere facade acquisition,
consumes persistent namespace/store counts.

Malformed arguments, invalid value, stale/foreign facade, provisional context,
missing/nonfunction callback, queue/rate exhaustion or service-not-ready reject
synchronously with a controlled API error **before acceptance**. No callback is
owed for a rejected call. `pcall` can catch submission errors; it does not catch an
error later raised inside Callback. Ordinary callback errors use I9 and do not
roll back storage. A callback timeout retains D1's VM-fatal behavior.

Accepted failures use `QuotaExceeded`, `StorageUnavailable`, `StorageBusy`,
`StorageFull`, `StorageCorrupt`, `FormatUnsupported`, `DeadlineExceeded`,
`StorageError` or `Indeterminate`. Codes are bounded ASCII, not raw exception text,
SQL, filesystem paths or persisted content. More detail belongs in bounded host
diagnostics. `Indeterminate` means a dispatched mutation may have committed;
other mutation failures require affirmative evidence that no commit occurred.

## 2. Namespace authority and durable identity

The persistent namespace is a host-derived tagged identity:

- operator root: `(Root)` for this CarbonLuau installation/data directory;
- addon: `(Addon, canonical stable PackageId)` under D14's existing ID grammar.

No namespace argument, provider string, alternate root, scope suffix or override
is script-supplied. Tags prevent root/addon collisions. Addon version, provider
Plugin object, domain token and VM generation are **not** durable namespace keys.
Changing package ID selects different data; it never migrates automatically.
Installing another trusted provider using the same stable ID after unregistering
the old one intentionally resumes that ID's data. Namespace is not author
authentication. Operators must treat reassignment of an ID as data-access approval;
malicious C#/administrator access is outside the script boundary.

Facades remain bound to their original host/VM/domain and D7 publication scope.
Replacements, provider unload, fatal VM recovery, CarbonLuau reload and restart
invalidate old facades and queued callbacks, **not stored records**. New generations
reacquire fresh facades for the same namespace. No script-triggered namespace wipe
or automatic garbage collection on uninstall/version change exists.

Persistence is deliberately private. At service acquisition and every store call,
the current admitted operation's domain must equal the facade's ResourceOwner;
check this in the native/host authority boundary, not an overridable Luau wrapper.
Passing A's facade to B does not retarget it to B and does not authorize B to use A's
namespace: reject `ForeignDataStore`. D10 is unchanged: invoking A's exported
closure from B does not switch B's admission to A. Such a closure cannot access
A's private store synchronously on B's behalf. An A-owned scheduled callback may
use A's own store under a fresh A admission. Existing public modules may share
ordinary returned/cached application data; Foundation 1 adds no shared-store grant,
cross-addon storage broker, RPC or implicit authority transfer. This privacy check
is specific to persistence, not a change to foreign-use rules of Player/GUI.

## 3. Publication policy

Disk-free GetDataStore acquisition is permitted during candidate/cold-module
initialization and participates in existing facade publication: a failed scope
stales a newly acquired facade, including one leaked through ordinary Lua memory.

All three async operations require the existing authoritative predicate:
**an existing nonprovisional admission and no active publication scope**.
Set/Remove are irreversible effects; no journal, rollback or failed-candidate write.
Get is read-only but is also committed-only in Foundation 1: a private async read
must not outlive a provisional module or require yielding initialization. This is
a deliberate conservative restriction, not a claim that a read mutates storage.
Provisionally scheduled `task.defer` work may perform I/O only after its publication
commits; failed candidates/modules never dispatch those requests. A dependency,
pcall, cached facade or nested coroutine cannot launder this restriction.

Configuration reads occur after activation; authors gate their own gameplay logic
until the completion arrives. Persistence does not suspend activation or fabricate
defaults on storage failure. No new provisional transaction model is introduced.

## 4. Value model and exact snapshot semantics

`PersistedValue` is boolean, finite Luau number, valid UTF-8 string, dense array
or string-keyed map recursively containing those values. No stored null/nil token.

- A nonempty array has exactly integer keys 1..N with no holes or extra keys.
- A map has only UTF-8 string keys; mixed/nonnumeric-array keys are rejected.
- An empty table canonically represents an empty map; no separate empty-array API.
- No metatable, cycle, userdata, function, thread, buffer, vector, Player, Entity,
  Signal, Connection, host facade, pointer or object serialization.
- Repeated acyclic references are encoded as independent value copies; every copy
  consumes bounds. Only the active ancestor path detects cycles. Object aliasing
  and identity do not survive storage.
- Snapshot uses raw traversal, invokes no metamethod, stops immediately at a bound,
  and checks the existing monotonic execution deadline during traversal/encoding.
  Reject oversized numeric indices before any dense-array walk; never iterate to
  a caller's huge maximum index to discover a hole.

Set captures the entire bounded snapshot **before accepting** the request. Later
caller mutation cannot change it. Get validates bytes and creates a fresh ordinary
Luau graph for each successful completion. Editing it does not save. Neither worker
nor managed host retains script table references as persisted state. Callback
references are separate VM-owned scheduling resources, never worker payloads.

Numbers use an explicit IEEE-754 binary64 bit encoding, not JSON decimal conversion
or SQLite REAL coercion. Every finite bit pattern, including subnormals and negative
zero, round-trips exactly; NaN and both infinities are rejected on encode and decode.
Integers beyond precise double representation are not repaired; store exact IDs as
strings. No string/number, date, enum or integer-width coercion.

## 5. Logical names and bounds

Store names: 1..64 UTF-8 bytes. Keys: 1..128 UTF-8 bytes. Both are case-sensitive
exact byte identities, no Unicode normalization, trimming, lowercasing or fuzzy
comparison. Reject malformed UTF-8, NUL, ASCII controls (U+0001..001F and U+007F),
`/`, `\`, `:`, and complete names `.` or `..`. This rejects ordinary paths without
turning names into filenames. No value-to-string coercion. SQLite binds validated
names/keys as BLOBs so its text collations cannot change identity.

Map property names have the value-string rules, not store/key path restrictions:
0..128 UTF-8 bytes, no NUL; duplicate encoded keys are invalid on decode. Ordinary
value strings allow 0..16 KiB valid UTF-8 bytes, no NUL. Text is not executable.

All numbers below are **accepted Foundation 1 design limits**, except the allocated-file
operational safety budget, which is a qualification target and diagnostic threshold
under section 6, not a hard physical invariant. They are not measured performance/
support claims. Implementation may lower them with a documented D21 amendment before
public release, never silently broaden them.

| Resource | Ceiling/accounting |
|---|---|
| Container nesting | 16, root container depth 1 |
| Entries | 1,024 in one table; 4,096 across the expanded value tree |
| Encoded value envelope | 64 KiB including header/checksum |
| Stores | 64 nonempty durable stores and 64 distinct acquired names per domain |
| Keys | 10,000 per namespace; 100,000 globally |
| Namespace logical bytes | 16 MiB of stored name/key/envelope bytes |
| Global logical bytes | 256 MiB across at most 256 nonempty namespaces |
| Pending requests | 8 per namespace; 128 globally, including executing and completed-undelivered |
| Request/response IPC frame | 68 KiB each; lengths checked before allocation |
| Retained transport bytes | 18 MiB globally, request + reserved result buffers |
| Request rate | namespace 20/s, burst 32; global 200/s, burst 256 |
| Mutation rate (also consumes request tokens) | namespace 5/s, burst 8; global 50/s, burst 64 |
| Completion intake per owner-thread tick | at most 8, with at most 1 MiB transport work |
| Async request lifetime | 5 seconds from acceptance, no extension at dispatch |
| Worker | one process, one outstanding command, 256 MiB process limit |
| Database | 4 KiB pages, 131,072-page ceiling = 512 MiB |
| Persistence allocated-file operational budget | 1,280 MiB, observed at startup/pre-operation/post-operation on a qualified local filesystem; includes all CarbonLuau-owned persistence files and retained journals. Not a never-exceeded in-flight physical disk quota; section 6 defines measurement scope and backend extent limits. |
| Diagnostic retention | at most 8 MiB total, rotating; no stored values |

Logical bytes charge each row its store-name + key + full value-envelope bytes;
repeated store names count repeatedly. Namespace ID overhead and indexes are covered
by count/backend byte-extent limits and section 6 allocation accounting. Updates
subtract old row charge and add new in the same
transaction. Removes release quota only at commit. Quotas survive reload/restart
through durable accounting; validate counters on worker startup against bounded
tables. Check all length/count arithmetic for overflow before allocation.

Rate buckets belong to persistent namespace within the loaded host, so replacing a
domain cannot reset its allowance. Retain at most 256 active/nonempty namespace
bucket records plus the existing bounded active-domain set; evict empty inactive
ones. Operator/plugin restart resets rate counters, not quotas. Reject at admission
instead of waiting on a mutex. Completion capacity is reserved on acceptance; retain
that reservation until callback delivery/discard so saturation cannot allocate an
unbounded result backlog. A domain cannot cache unlimited distinct facade names.
Reserved completion capacity must include native callback intake, not just managed
bytes; an ordinary task flood cannot evict an accepted storage completion. Fatal
VM retirement or allocation failure still follows I9/I10 and can prevent callback
delivery; neither condition authorizes replay or a claim that a write did not commit.

## 6. Backend selection and evidence

**Choose private SQLite in a shipped CarbonLuau storage worker process**, not
Carbon's generic SQLite wrapper. It is a local key/value backend; scripts never see
SQL, connections, schema, transactions or filenames. Pin an audited SQLite source
revision/build in Persistence-1A and record its OS/VFS qualification. No SQLite
dependency, binary or version is added by this design task.

| Candidate | Decision |
|---|---|
| Carbon/Oxide data-file APIs | Familiar JSON convention, but inspected Save/WriteObject write the target directly and ReadObject may create a missing file. No qualified durable atomic per-key contract; do not wrap as success. |
| Carbon SQLite library | Existing Mono.Data.Sqlite integration demonstrates host availability, not our bounds. Inspected queue has no admission cap and Shutdown joins the worker; it does not expose the required structured durable-result contract. Do not adopt that wrapper. |
| CarbonLuau atomic files | Feasible in principle, but requires project-owned crash recovery, atomic deletion, parent-directory sync, per-platform replace behavior and transactional quota accounting. Not merely WriteAllText plus rename. |
| Private SQLite | Supplies established journaling/transaction machinery for a key update plus quota bookkeeping, avoiding a second project-owned journal/recovery engine. Select the smallest fixed-query subset. |
| Other embedded/distributed store | No need established; deferred. |

Current upstream source, inspected 2026-09-23 (not exact-binary qualification):
[Carbon DynamicConfigFile](https://github.com/CarbonCommunity/Carbon/blob/08bcfd854b6692e4566a859d050eb1ccd8f9dc9e/src/Carbon.Components/Carbon.Common/src/Oxide/Configuration/DynamicConfigFile.cs),
[DataFileSystem](https://github.com/CarbonCommunity/Carbon/blob/08bcfd854b6692e4566a859d050eb1ccd8f9dc9e/src/Carbon.Components/Carbon.Common/src/Oxide/Configuration/DataFileSystem.cs),
[Carbon SQLite](https://github.com/CarbonCommunity/Carbon/blob/08bcfd854b6692e4566a859d050eb1ccd8f9dc9e/src/Carbon.Components/Carbon.Common/src/Oxide/SQLite/SQLite.cs),
[Oxide DynamicConfigFile](https://github.com/OxideMod/Oxide.Core/blob/b5001d6b3f82da0227448fe2a80e1316e8b8f2b6/src/Configuration/DynamicConfigFile.cs).
In both DynamicConfigFile implementations, `sync` refreshes the in-memory dictionary;
it is not an fsync request. These APIs remain useful to other plugins; this decision
only declines to infer CarbonLuau's stronger contract from them.

.NET provides [Flush(true)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=netframework-4.8.1)
and [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=netframework-4.8.1),
but their existence is not qualification of crash-safe directory metadata and
quota updates across Windows and Mono/Linux. Keep the plugin net48/C# 7.3 compatible;
do not introduce a modern-.NET database dependency into the game process. A native
helper with a bounded protocol avoids that binding, similar in containment purpose
to Foundation G, **but asynchronous rather than its synchronous compile wait**.

### Fixed storage layout

One database at a host-owned fixed child under Carbon's data directory:
`CarbonLuau/persistence/store.sqlite3`. No script string is part of a filesystem
path; `(NamespaceTag, PackageIdBytes, StoreNameBytes, KeyBytes)` are bound fields.
No raw-name paths or ambiguous string concatenation/hashing collisions. The worker
validates the canonical host-selected directory and rejects symlink/reparse escape
and unexpected files. No file URI, network share, arbitrary path or connection
string ingress. Dedicated local Windows/glibc Linux filesystems only initially;
network/FUSE/cloud-synchronized storage remains unqualified.

One worker/connection owns this database. A fail-fast exclusive ownership lock
prevents another CarbonLuau host/worker using the same directory. No retrying lock
loop and no simultaneous replacement worker. These are worker-side OS locks, never
locks held or awaited by a Luau callback. Other tools/plugins must not edit the
database while online. One database makes corruption a storage-service-wide
availability risk; namespace isolation is logical, not separate crash isolation.

Use rollback `journal_mode=PERSIST`, `synchronous=EXTRA`, fixed page size above,
`max_page_count`, bounded cache, `temp_store=MEMORY`, mmap disabled, no automatic
VACUUM, no ATTACH, extension loading, dynamic schema or user SQL. Verify effective
settings, do not assume a PRAGMA was accepted. Fixed parameterized operations only.
Disable trusted schema/application-defined execution and enforce worker memory/SQL
limits. Startup checks known schema, format, bounds and database integrity before
Ready. Interrupted journal recovery is performed by SQLite, never manual deletion
of a hot journal. No WAL/checkpoint subsystem is needed for one serial worker.

Configure PERSIST/EXTRA on every connection; do not rely on the mode being
remembered across reopen. Retain the journal with journal_size_limit=-1;
do not set it to zero or use cleanup to change the commit mechanism.
SQLite owns journal contents and hot-journal recovery. A retained
store.sqlite3-journal is expected storage, not a disposable stale file.
Initial directory/database/journal creation and recovery must be qualified
before Ready. Disable cache spill for the fixed single-key workload and
qualify its memory and journal byte-extent bounds. No custom VFS or external
post-commit file/volume-flush layer is part of Foundation 1.

Database page ceiling is not by itself an allocated-disk quota. With the pinned
qualified SQLite, fixed 4,096-byte pages, max_page_count=131072, PERSIST/EXTRA,
journal_size_limit=-1, cache_spill=OFF, auto_vacuum=NONE, temp_store=MEMORY,
mmap=0 and no chunk-size override, bound database EOF to 536,870,912 bytes and
retained rollback-journal EOF to 537,985,024 bytes. Qualify startup, hot-journal
recovery, statement failure and fixed one-key transactions against these bounds;
do not generalize the normal-workload proof to incompatible existing formats.
Reject unsupported WAL/auxiliary/conversion paths before using them. Preserve
supported SQLite hot-journal recovery rather than implementing manual repair.

Count the database, retained/active journal, ownership metadata and any future
bounded diagnostic or temporary files owned by persistence. Unexpected files
remain a startup failure; temporary paths cannot evade accounting by residing
elsewhere. Foundation 1 creates no disk-backed statement/sort temporary files,
copy/rebuild files or VACUUM images. Adding such a path requires a new bound and
qualification. Retained/deleted-open files must not be omitted if a future
supported path creates them. No log rotation overlap is implicitly exempt.

Measure ordinary file allocation using Windows FileStandardInfo.AllocationSize
or Linux st_blocks multiplied by 512, recording the exact filesystem profile.
These platform measurements are not identical accounting definitions and do
not include a portable, complete share of filesystem metadata, filesystem
journals, snapshots or virtual-disk backing overhead. Do not advertise their
sum as an exact physical device footprint. Keep checked arithmetic and bounded
file enumeration. Check allocation and file-byte bounds before opening existing
storage, before each operation and after it completes. Refuse further admission
on a failed check and preserve the files; do not delete, compact or truncate
valid storage merely to regain budget. A post-commit failure does not roll back
the mutation and must preserve the established Indeterminate/no-replay policy.

The 1,280 MiB number is an operational allocation budget on qualified local
Windows NTFS and Linux ext4 profiles, not a strict physical quota enforced inside
filesystem calls. Qualification must record allocation geometry and relevant
filesystem/mount features, normal and maximum-size workloads, sparse/preallocated
input handling, journal high-water, page exhaustion, recovery and observed
transient allocation at available instrumentation boundaries. A filesystem name
alone is insufficient qualification. Network/FUSE/cloud-synchronized storage,
unqualified copy-on-write/snapshot/deduplication/compression profiles and ext4
bigalloc profiles remain unsupported until separately qualified. Stock SQLite
must not request unqualified preallocation. No administrator-configured quota,
dedicated volume, custom VFS or external flush layer is a default requirement.

Logical quotas and qualified backend byte-extent bounds remain hard limits.
Whether actual allocation stays within the operational envelope between checks
is conditional on the qualified filesystem's behavior; observations are not a
universal mathematical proof. If a deployment requires a never-exceeded physical
quota, Foundation 1 does not satisfy that requirement without a separately
designed and qualified filesystem/storage enforcement mechanism. External files
and backups remain operator-owned; independent disk exhaustion can cause a
controlled storage failure. External interference does not excuse CarbonLuau's
own allocation or accounting errors.

The [physical-allocation investigation](PersistencePhysicalAllocationInvestigation.md)
observed no 1,280 MiB breach; this is empirical evidence, not a theorem or 1A PASS.
Its historical enforcement/proof gap remains preserved. The
[2026-09-23 adoption](PersistencePhysicalD21Amendment-Proposed.md) changes that
resource guarantee, conditional on final qualification. The WAL startup rejection
fix remains mandatory: reject unsupported WAL mode before conversion or WAL/SHM
creation, including page-1 restoration through hot-journal recovery. A main-header
check alone does not close that gate; preserve normal supported recovery and
qualify incompatible geometry, auto-vacuum and crafted journal/header combinations.

[Persistence-1A](PersistenceFoundation1A.md) now records the qualified private
implementation, including that startup/recovery correction and the measured
filesystem profiles. This does not turn the operational budget into a hard
physical invariant or qualify public API/Carbon integration.

## 7. Durability, atomicity and uncertainty

For Set/Remove, success means the transaction containing the key mutation
and its quota accounting completed SQLite COMMIT successfully after the
required journal, database and retained-journal commit-marker synchronization,
and CarbonLuau validated its worker response before reporting completion.
There is no success at enqueue, memory update or database flush alone.
The PERSIST commit marker is the synchronized invalid journal header.
Remove's true/false is determined inside that same transaction. No public
multikey transaction is introduced.

Selected basis: SQLite's [atomic commit design](https://sqlite.org/atomiccommit.html),
[EXTRA synchronization](https://sqlite.org/pragma.html#pragma_synchronous), and
[transaction failure semantics](https://sqlite.org/lang_transaction.html).
PERSIST commits by invalidating and synchronizing the retained journal
after database synchronization. FULL and EXTRA have the same ordinary
PERSIST commit sequence in the qualified pin; EXTRA remains the configured
policy. This avoids relying on per-commit journal deletion durability.
It does not eliminate initial file/directory creation, recovery, locking,
filesystem/device or virtualization assumptions. Process-crash recovery
evidence is distinct from OS-crash or physical power-loss qualification.

The [2026-09-23 approved amendment](PersistenceD21Amendment-Proposed.md) adopts
the [investigation's](PersistenceDurabilityInvestigation.md) implementation path,
not production qualification. The original DELETE/EXTRA proof gap remains
historical evidence, not observed NTFS data loss. Lying disks, hardware failure,
external editing and unsupported mounts are not covered. No universal
hardware/power-loss guarantee or backup is promised.

| Event | Contract |
|---|---|
| Graceful unload/shutdown | Stop intake, invalidate callbacks, cancel undispatched work. Previously acknowledged commits survive; no final save is required. |
| Domain/VM retirement during dispatched write | It may commit. Drop the stale callback; never roll back or replay it. Fence subsequent work until its outcome/recovery is settled. |
| Crash before commit | SQLite recovers to a complete prior state under qualified assumptions. No success was reported. |
| Commit then crash/lost response | New value may be durable without any callback. Outcome is indeterminate to the caller. |
| Server/process crash after success | Acknowledged commit is recovered under the selected backend contract, not dependent on Luau state. |
| OS crash/power loss | Intended durability is conditional on the qualified complete storage stack honoring synchronization, ordering and namespace recovery. No tested OS-crash or physical power-loss claim follows from process-kill tests. |
| Disk full, write/sync/delete failure | No success. Definite StorageFull/StorageError only after proven rollback/no commit; otherwise Indeterminate and service recovery before more work. |
| Partial physical write | Journal recovery protects transaction boundaries within backend assumptions; corruption triggers fail-closed behavior, not a partial decoded value. |
| Rename failure | No application-level rename is in normal Set/Remove. Worker initialization/maintenance failure never becomes success; no fallback copy-overwrite. |

The dispatcher never automatically retries a mutation after dispatch. A timeout,
worker crash, lost/malformed response or unexpected exit after dispatch is
`Indeterminate` unless absence of commit is proved. Read failure never means nil
absence. Queue expiry **before dispatch** is `DeadlineExceeded` and cannot mutate.
The service admits at most one callback for a completed request, but does not
promise exactly-once effect or delivery across retirement/crash. Authors must not
blindly retry an increment after uncertainty. Get-then-Set is **not** atomic; later
serialized Set wins and logical lost updates are possible. This is not a financial
ledger or distributed currency transaction system.

Corruption, checksum mismatch, illegal value/schema, incompatible format, invalid
quota metadata or unexpected database structure produces StorageCorrupt or
FormatUnsupported. Disable the storage service and preserve database plus journals
in place for operator recovery; do not rename/delete/reset/default/overwrite them
automatically. Known corruption is never treated as absent. SQLite may perform its
normal crash recovery before discovering corruption; no forensic byte-preservation
claim is made for that recovery. Operators recover offline from a coherent backup;
live-copying only the database while its journal is active is not a backup recipe.

## 8. Serialization, scheduling and lifecycle

Foundation 1 chooses **non-yielding asynchronous callbacks** because current
`cl_vm_callback` rejects LUA_YIELD and D4 forbids yielding first-load modules.
No suspended Luau frame, yield-through-host boundary or continuation-resume design
is required. The original callback finishes normally; the completion is a separate
later admission, analogous to existing task/event work, not a resumption.

1. Owner thread validates authority, publication, rate/queue reservations and bounded
   raw snapshot. Native retains the callback in its exact owner domain. It assigns
   an opaque request nonce with host/VM/domain epochs, never a worker-side function.
2. A per-namespace FIFO holds immutable records. On later safe owner turns, a rotating
   namespace cursor offers one request per nonempty namespace before a second turn.
   The dispatcher revalidates lifetime and hands one record to the supervisor.
3. Background pipe I/O and a separate supervised storage process perform all open,
   read, decode validation, SQL, write, flush, lock, recovery and close work. No VM,
   Rust/Unity object, managed delegate or native VM pointer crosses to that process.
4. A bounded completion mailbox is drained only by the existing verified owner-thread
   tick. Worker code never calls Carbon NextTick or Luau. Validate protocol version,
   lengths, nonce and epochs; reject duplicate/late/foreign replies. Decode into Luau
   only on owner-thread completion admission, with deadline/allocation checks.
5. Schedule the completion for a later drain cutoff, with fair per-domain scheduling
   and the existing global frame budget. Never enter recursively or run same-drain
   newly-created callback work. Release all reservations on delivery or discard.

Within a namespace, dispatch and completion scheduling preserve acceptance FIFO,
including distinct stores. Different namespaces interleave fairly; no global
cross-namespace ordering promise. One worker transaction at a time gives a clear
database linearization order. A Get after an acknowledged Set observes that value
unless a later write intervenes. A request accepted after an earlier same-namespace
request does not overtake it; cancellation of undispatched old work is explicit.

There is no OS wait, pipe write/read, process wait, database open, fsync or worker
launch inside Luau/owner-thread execution. Submission uses preallocated bounded
mailboxes with nonblocking admission; no blocking mutex. Supervisor owns launch,
watchdog, I/O and process reaping. Completion materialization and callbacks consume
the usual frame/deadline/memory budgets. Failure to prove these bounds blocks 1B.

The original admitted operation's deadline never resets at submission, dependency
crossing, snapshotting or rejection. Each request separately carries an absolute
five-second monotonic deadline from acceptance, including queue/IPC/worker time;
dispatch/restart cannot extend it. Its result must enter the bounded completion
mailbox before that deadline. Scheduled callback delivery may occur later under
frame fairness; no real-time delivery guarantee. A completion is a **new** admitted
operation with the normal callback budget, not an extension of the finished caller.
Its callback and value materialization share that one budget. No code after a timed-
out original admission is resumed. Storage timeout does not itself retire a healthy
VM; ordinary callback deadline cancellation still does.

### Teardown and worker failure

Retirement cancels undispatched requests and releases old native callback references.
Already-dispatched requests own no VM references in the worker and can finish
durably; their callbacks are suppressed once host/VM/domain is stale. New-generation
work for that namespace cannot pass the dispatched old transaction: wait for its
terminal response, or worker death followed by SQLite recovery. Do not cancel an
old transaction by merely dropping a response while admitting its successor.

CarbonLuau unload stops supervisor intake and closes host-facing delivery first.
The worker has bounded graceful shutdown (up to one second off the owner thread),
then kill/reap. OS parent-death/job containment is required so plugin/server failure
does not orphan a writer. No background thread may call an unloaded native library;
the supervisor uses only managed transport/process resources after detachment, and
all native callback refs are released before native unload. No new worker starts
until the old one is confirmed dead and the exclusive directory lock is obtainable.
If OS I/O prevents confirmed termination, persistence remains unavailable; do not
accumulate replacement workers. Server scripting can remain operational without
storage. One worker restart attempt follows a fault; further failure stays disabled
until explicit operator action. Startup integrity/recovery has a separate 30-second
supervised readiness deadline; calls while not Ready reject, never block.

## 9. Internal format and security boundary

Storage format 1 is independent of package, scripting API, native ABI, provider
protocol, addon schema and SQLite version. It has a fixed database application
marker, schema version and one fixed record/quota schema; no automatic upgrade or
downgrade. Exact SQL layout/protocol magic is internal Persistence-1A work, not a
new public option. Unknown format stops without conversion. Authors may store
their own `{ Version = 1, ... }` payloads; no migration framework in Foundation 1.

Value envelope: project magic, format version, bounded payload length, payload,
SHA-256 checksum binding the length-delimited namespace/store/key plus envelope
header/payload. Payload uses explicit tags for boolean/binary64/string/array/map,
fixed-endian lengths/counts, UTF-8 bytes and lexicographically byte-sorted map keys.
No JSON polymorphism, type-name instantiation, arbitrary serializer plugins or
host deserialization. Reject trailing bytes, duplicate keys, unknown tags,
noncanonical lengths/order and all bounds violations before constructing large
objects. The checksum detects accidental corruption, **not authentication** against
the operator or trusted C# code. No encrypted-at-rest or secrets-store claim.

SQLite treats stored values as BLOBs, not SQL or REAL values. Validate untrusted
database schema/settings and use defensive configuration, no extensions, trusted
schema disabled, fixed prepared statements, reduced limits and process containment.
See SQLite's [defensive guidance](https://sqlite.org/security.html),
[size limits](https://sqlite.org/limits.html) and
[runtime limits](https://sqlite.org/c3ref/limit.html).
Native corruption remains a risk requiring patching/fuzzing; a helper is not a
portable hostile-code OS sandbox. No worker environment credentials or arbitrary
inherited handles are needed. Scripts gain only the named persistence capability,
not filesystem/network/SQL/process/reflection or player-based permissions.

## 10. Contract examples (0.5 development API; unqualified)

Read on player admission; the ID is already a string. A retained Player may be stale
by completion, so this example only prints its captured ordinary name.

```luau
local DataStoreService = game:GetService("DataStoreService")
local Players = game:GetService("Players")
local Coins = DataStoreService:GetDataStore("Coins")

Players.PlayerAdded:Connect(function(Player)
    local Name = Player.Name
    Coins:GetAsync(Player.UserId, function(Value, ErrorCode)
        if ErrorCode then
            print("[Coins:Storage] Read failed", ErrorCode)
            return
        end
        if Value == nil then Value = 0 end
        print("[Coins:Balance]", Name, Value)
    end)
end)
```

A fixed-value write after commit, not an unsafe Get/Set increment:

```luau
local DataStoreService = game:GetService("DataStoreService")
local Settings = DataStoreService:GetDataStore("Settings")

task.defer(function()
    Settings:SetAsync("Welcome", { Version = 1, Enabled = true }, function(Saved, ErrorCode)
        if ErrorCode then
            print("[Settings:Storage] Save failed or uncertain", ErrorCode)
            return
        end
        print("[Settings:Storage] Durable save completed", Saved)
    end)
end)
```

For failure-aware submission under overload, wrap the `GetAsync`/`SetAsync` call
itself in pcall as well; its failure means no request was accepted, not an async
storage result. Never convert StorageCorrupt/StorageUnavailable into a default and
overwrite. Get/Set races need addon-owned serialized application logic; no hidden
atomic Update guarantee. Persistence does not observe existing players
synthetically or claim any real-client result.

## 11. Implementation phases and validation gates

### Persistence-1A — private backend, codec and namespace substrate

No public API. Pin/package the SQLite helper for Windows x64 and glibc Linux x64;
record provenance/license; implement bounded protocol, defensive schema checks,
codec, quotas, private namespace mapping and nonblocking supervision. Core may own
pure value/name/quota descriptors; Carbon integration and process ownership stay
in their existing layers. Introduce native ABI changes only if actual exports/layout
change; never pass VM/host objects. Gate on deterministic tests plus helper crash
tests, exact binary64 fixtures, unsupported values, corrupt input, file/path escape,
disk-full/short-write/flush failures, directory lock, journal recovery, hard
logical/backend byte-extent bounds and qualified allocated-file operational-budget
checks, hung-worker containment and no native-library-after-unload access. Final
qualification must close section 6's mandatory WAL startup rejection fix and
filesystem-profile follow-up before 1A PASS or the 1B handoff.

**Implemented/qualified:** [PersistenceFoundation1A.md](PersistenceFoundation1A.md)
records the private substrate PASS and exact 1B handoff. The later phases below
are not implemented by that closure.

### Persistence-1B — DataStoreService and completion admission

**Implemented and qualified within recorded scope:** see
[the 1B record](PersistenceFoundation1B.md). The criteria below define that slice,
not combined 1C closure or release approval.

Add only the signatures above; no yielding support. Qualify ResourceOwner/admission
privacy, publication/stale-facade checks, authoritative committed-only predicate,
pre-accept snapshots, callback retention/capacity, reply validation, fair FIFO intake,
bounded fresh decoding, no recursive VM entry, original deadlines and new callback
budgets. Cover foreign exported closures, leaked facades, cold first-load modules,
caught errors, failed candidates, deferred success, original callback errors after
acceptance, stale domains/VMs and queue floods. Public metadata/docs cannot claim
available support before this slice is implemented and its applicable gates pass.

### Persistence-1C — combined qualification and public closure

**NOT STARTED.** This separately scoped phase is not closed by 1B's local gates.

Prove repeated replacement/provider unload/VM recovery/CarbonLuau reload/restart,
in-flight writer fencing, lost-ack uncertainty, no replay, no quota-reset loopholes,
namespace reassignment, no per-request memory leaks and no active-I/O orphan workers.
Crash-inject before/during/after COMMIT and before response delivery; separately
test process crash, simulated VFS faults and actual qualified filesystem behavior.
Do not call process-kill testing proof of physical power-loss survival. Stress at
limits, concurrent namespace fairness, corrupted rows/schema/journal, incompatible
formats and physical file limits. Windows/Linux required; affected native sanitizer,
allocation-fault and existing runtime/publication regressions selected through
Compatibility. Record remaining host/filesystem uncertainty and final-source CI.
No live Rust entity/client mutation is needed to prove local storage; actual Carbon
integration/lifecycle still needs both target platforms before support claims.

### Tooling and release seam

Represent service/type/signatures, callback shapes, private ownership, errors,
effect classifications and numeric limits in the existing D19 authoritative API
catalog during implementation; generate public definitions/docs from it. Runtime
validation remains authoritative. No VS Code/preview storage, mock database,
editor filesystem access, tooling worker or extension modification in this task.

The 2026-09-23 user decision assigns Persistence Foundation 1 to
**0.5.0-experimental**, independently of Entity. Package **0.5.0** is the intended
future release, not an authorized package bump or publication. [D12](Invariants.md#d12--scripting-and-protocol-identity)
and [Release.md](Release.md) own the separate development identity mapping.
Public qualification remains mandatory. Existing APIs keep their historical
introduction versions; persistence receives explicit SinceApi 0.5, unavailable
preview and work-in-progress qualification until its applicable gates close.

## 12. Explicitly deferred

Update/transform, compare-and-swap, increment, multi-key transactions, shared or
cross-addon stores, queries, ordered stores, List/Keys/enumeration, TTL, replication,
cloud/remote/HTTP databases, SQL/files/connection strings, automatic persistence,
live persistent tables, Player/Entity serialization, migrations, script backup/
restore/import/export, yielding/Promises and editor/mock persistence are deferred.

**Update is excluded:** running Luau under a database lock breaks I4/I8; optimistic
retry would introduce callback replay/effect rules. Foundation 1 does neither.
The database's internal transaction support does not authorize a public transaction
or transformation callback.

## 13. Resolved decision index

1. Service: DataStoreService.
2. Store: private domain-bound DataStore; acquisition is disk-free.
3. Methods: GetDataStore; GetAsync, SetAsync, RemoveAsync with required callbacks.
4. Async: non-yielding admission, separate later completion; no sync aliases.
5. Values: bounded JSON-like booleans, finite doubles, UTF-8 strings, arrays/maps;
   nil denotes absence only.
6. Tables: pre-accept snapshot and fresh Get copies, no persistent object identity.
7. Numbers: exact finite binary64 bits, including negative zero; no coercion.
8. Keys: exact case-sensitive UTF-8 1..128 bytes under section 5.
9. Stores: exact case-sensitive UTF-8 1..64 bytes under section 5.
10. Namespace: Root or Addon/stable PackageId, host-selected; same admission owner.
11. Replacement: durable records survive; old facades/callbacks stale, no retargeting.
12. Provisional reads: async Get rejected; disk-free facade acquisition allowed.
13. Provisional writes: reject via authoritative predicate; defer only after commit.
14. Backend: private pinned SQLite worker, fixed statements and one database.
15. Durability: PERSIST/EXTRA synchronized retained-journal commit marker plus validated worker result, subject to the process/OS/power-loss distinctions in section 7.
16. Atomicity: one key plus accounting per transaction, no public multikey operation.
17. Crash: recover old/new complete state; lost response can be indeterminate.
18. Corruption: controlled error, disable service, preserve files, no automatic reset.
19. Concurrency: per-namespace FIFO, fair dispatcher, one serial worker transaction.
20. I/O: supervised process and bounded async mailboxes, no owner-thread disk waits.
21. Deadlines: original Luau admission unchanged; fixed request deadline; completion
    is a new bounded admission, not a resumed/extended old one.
22. Update: deferred; no replayable transforms/locks across Luau.
23. Cross-addon: no shared stores or foreign facade use; ordinary data sharing only.
24. Quotas: section 5 fixes value, count, logical-byte, queue, rate and worker limits;
    section 6 keeps hard backend byte-extent bounds and defines the 1,280 MiB
    operational safety budget, qualification target and diagnostic threshold.
25. Format: version 1 typed/checksummed envelope and fixed database schema;
    author-managed application version fields.
26. Security: allowlisted storage, no paths/SQL/raw objects, bounded untrusted decode.
27. Phases: 1A substrate, 1B API/admission, 1C lifecycle/crash/quota/public closure.
28. Tooling: implementation-owned declarations under D19; no preview persistence.
29. Deferred APIs: section 12, not implicit follow-on authorization.
30. Version: assigned 0.5.0-experimental by the later user decision; intended future
    package 0.5.0, without a package bump, publication or qualification claim.

No unresolved ordinary API choice remains. Implementation qualification is still
required: stop if the selected VFS cannot meet durability, decoder/supervisor cannot
be bounded, mailbox/scheduler cannot preserve I4/D10, section 6's hard backend
byte-extent bounds or qualified operational-budget checks fail,
or namespace authority requires raw paths. Do not silently weaken this contract.
