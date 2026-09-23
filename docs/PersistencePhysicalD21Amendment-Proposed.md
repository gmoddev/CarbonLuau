# D21 physical-allocation amendment — ADOPTED WITH QUALIFICATION CONDITION

Explicitly approved by the user and adopted on **2026-09-23**. Supporting
[investigation](PersistencePhysicalAllocationInvestigation.md) found no 1,280-MiB
breach, but did not establish a strict physical allocation enforcement mechanism.
This proposal changes a resource guarantee; it is not an editorial clarification
or a statement that the old gate passed. The replacement text below is now adopted
in [D21](Invariants.md#d21--persistence-foundation-1) and
[PersistenceFoundation1.md](PersistenceFoundation1.md); the proposal filename is
retained for historical links.

**Adoption condition:** the 1,280 MiB figure is an operational safety budget,
qualification target and diagnostic threshold, not a hard physical invariant.
Hard 16-MiB namespace / 256-MiB global logical quotas and SQLite page/file-length
bounds remain. No breach observed is empirical evidence, not a theorem. Final
Persistence-1A qualification is in progress; approval is not PASS. The WAL startup
rejection fix is mandatory, including page-1 restoration through hot-journal
recovery, before unsupported conversion or WAL/SHM creation. Preserve supported
hot-journal recovery and close the investigation's scoped filesystem-profile
follow-up. D21 is resolved; this is final qualification, not another architecture
investigation. Historical negative evidence remains intact; do not begin 1B.

## Exact replacement: D21 retained-journal/allocation sentences

Replace the sentence beginning “Retained journals count toward the existing
physical directory ceiling” with:

> Retained journals count toward persistence resource accounting and must not be
> manually deleted or zeroed during startup or shutdown. CarbonLuau enforces hard
> logical quotas and qualified backend file-byte extent bounds. The 1,280 MiB
> allocated-file budget is a filesystem-qualified operational envelope checked at
> startup, before dispatching a database operation and after its completion; it
> is not an application-enforced never-exceeded physical disk quota. Measurements
> cannot guarantee that an in-flight filesystem operation never temporarily
> exceeds that envelope. On an observed budget violation, admit no further work
> and preserve storage; a mutation whose commit outcome is uncertain remains
> Indeterminate and is never replayed. Filesystem/device metadata, reservations,
> allocation units and storage-stack overhead remain filesystem-dependent, not
> bounded by a promise about database EOF. Foundation 1 does not claim an exact
> filesystem-wide or host-physical-footprint quota.

## Exact replacement: PersistenceFoundation1 section 5 table row

Replace the “Persistence directory allocation budget” row with:

| Resource | Ceiling/accounting |
|---|---|
| Persistence allocated-file operational budget | 1,280 MiB, observed at startup/pre-operation/post-operation on a qualified local filesystem; includes all CarbonLuau-owned persistence files and retained journals. Not a never-exceeded in-flight physical disk quota; section 6 defines measurement scope and backend extent limits. |

Keep the 16-MiB namespace, 256-MiB global, 512-MiB database, 8-MiB diagnostic,
256-MiB worker and every other accepted count/rate/value limit unchanged.

## Exact replacement: section 6 final physical-bound paragraph

Replace the paragraph beginning “Database page ceiling is not by itself a
journal/disk quota” through “External files/backups are operator-owned and cannot
be bounded by this service” with:

> Database page ceiling is not by itself an allocated-disk quota. With the pinned
> qualified SQLite, fixed 4,096-byte pages, max_page_count=131072, PERSIST/EXTRA,
> journal_size_limit=-1, cache_spill=OFF, auto_vacuum=NONE, temp_store=MEMORY,
> mmap=0 and no chunk-size override, bound database EOF to 536,870,912 bytes and
> retained rollback-journal EOF to 537,985,024 bytes. Qualify startup, hot-journal
> recovery, statement failure and fixed one-key transactions against these bounds;
> do not generalize the normal-workload proof to incompatible existing formats.
> Reject unsupported WAL/auxiliary/conversion paths before using them. Preserve
> supported SQLite hot-journal recovery rather than implementing manual repair.
>
> Count the database, retained/active journal, ownership metadata and any future
> bounded diagnostic or temporary files owned by persistence. Unexpected files
> remain a startup failure; temporary paths cannot evade accounting by residing
> elsewhere. Foundation 1 creates no disk-backed statement/sort temporary files,
> copy/rebuild files or VACUUM images. Adding such a path requires a new bound and
> qualification. Retained/deleted-open files must not be omitted if a future
> supported path creates them. No log rotation overlap is implicitly exempt.
>
> Measure ordinary file allocation using Windows FileStandardInfo.AllocationSize
> or Linux st_blocks multiplied by 512, recording the exact filesystem profile.
> These platform measurements are not identical accounting definitions and do
> not include a portable, complete share of filesystem metadata, filesystem
> journals, snapshots or virtual-disk backing overhead. Do not advertise their
> sum as an exact physical device footprint. Keep checked arithmetic and bounded
> file enumeration. Check allocation and file-byte bounds before opening existing
> storage, before each operation and after it completes. Refuse further admission
> on a failed check and preserve the files; do not delete, compact or truncate
> valid storage merely to regain budget. A post-commit failure does not roll back
> the mutation and must preserve the established Indeterminate/no-replay policy.
>
> The 1,280 MiB number is an operational allocation budget on qualified local
> Windows NTFS and Linux ext4 profiles, not a strict physical quota enforced inside
> filesystem calls. Qualification must record allocation geometry and relevant
> filesystem/mount features, normal and maximum-size workloads, sparse/preallocated
> input handling, journal high-water, page exhaustion, recovery and observed
> transient allocation at available instrumentation boundaries. A filesystem name
> alone is insufficient qualification. Network/FUSE/cloud-synchronized storage,
> unqualified copy-on-write/snapshot/deduplication/compression profiles and ext4
> bigalloc profiles remain unsupported until separately qualified. Stock SQLite
> must not request unqualified preallocation. No administrator-configured quota,
> dedicated volume, custom VFS or external flush layer is a default requirement.
>
> Logical quotas and qualified backend byte-extent bounds remain hard limits.
> Whether actual allocation stays within the operational envelope between checks
> is conditional on the qualified filesystem's behavior; observations are not a
> universal mathematical proof. If a deployment requires a never-exceeded physical
> quota, Foundation 1 does not satisfy that requirement without a separately
> designed and qualified filesystem/storage enforcement mechanism. External files
> and backups remain operator-owned; independent disk exhaustion can cause a
> controlled storage failure. External interference does not excuse CarbonLuau's
> own allocation or accounting errors.

## Adopted dependent gate wording

Replace section 11's “global disk ceiling” 1A gate with “hard logical/backend
byte-extent bounds and qualified allocated-file operational-budget checks”. In
D21's current qualification gate, replace “physical bounds” with “hard backend
byte-extent bounds and qualified allocated-file operational-budget checks”. Keep
all other startup/recovery/codec/worker/failure/platform gates. Update the section
13 quota summary and closing stop condition to refer to this section 6 contract,
not to an unconditional physical cap. Link the investigation from the adoption
record without deleting the original negative evidence.

## Protection retained, protection lost and adoption conditions

Retained: hard 16/256-MiB logical quotas, atomic key/quota transactions, fixed
backend byte extents, bounded process/memory/queues, exclusive ownership,
PERSIST/EXTRA ordering, controlled FULL, corruption preservation and no replay.

Lost: the claim that **every** admitted write is prevented from ever allocating
more than 1,280 MiB physically. Transient allocation can only be qualified/observed
under filesystem assumptions, and a later detected excess cannot be undone.
This is an explicit weakening of that one resource guarantee, not a substitute
proof and not a weaker durability acknowledgement.

This distinction was explicitly approved and is now documented for ordinary
Windows/Linux default deployment, conditional on qualification. The tested
NTFS/ext4 profiles still need the scoped startup/profile follow-up in the
investigation; approval alone does not mark 1A PASS. A deployment requiring strict
physical containment is not satisfied by this amendment. Do not publish
Persistence-1A under the original claim.

This documentation adoption makes no runtime/API/version changes. Backend fixes
and tests remain separately scoped work. Do not begin 1B before 1A qualifies.
