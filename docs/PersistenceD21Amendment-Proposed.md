# Proposed D21 durability amendment — NOT ADOPTED

For explicit user review, 2026-09-23. Supporting
[investigation](PersistenceDurabilityInvestigation.md). No production or canonical
behavior is changed by this proposal. The decision remains owned by
[D21](Invariants.md#d21--persistence-foundation-1), not this file.

## Proposed D21 replacement

Replace the D21 paragraph beginning "Select one private pinned SQLite database"
and ending "never defaults/overwrites" with exactly:

> Select one private pinned SQLite database in a supervised storage helper, with
> fixed parameterized operations, rollback PERSIST journaling and EXTRA
> synchronization verified against qualified local Windows/Linux VFS/filesystem
> behavior. Each connection must configure and verify these settings before Ready.
> Commit uses SQLite's retained-journal invalidation and synchronization, not
> journal-file deletion. Do not use Carbon/Oxide direct JSON writes or its generic
> SQLite queue as evidence of this contract. One transaction mutates a key and
> durable quota accounting atomically. Success follows checked SQLite COMMIT after
> its ordered journal, database and journal-commit-marker synchronization, and a
> validated worker result; it never means queue or memory acceptance. Successfully
> acknowledged transactions survive ordinary worker/server-process crashes when
> reopening the same intact storage under the qualified filesystem/device
> assumptions. OS-crash and power-loss durability are conditional on the complete
> OS/VFS/filesystem/virtualization/device stack honoring synchronization, ordering
> and namespace recovery; do not claim tested physical power-loss survival without
> such testing. Hardware/OS dishonesty and catastrophic or external storage damage
> are not covered. Uncertain post-dispatch mutations report Indeterminate, never
> rollback/exactly-once/retry. Corruption or unsupported format fails closed and
> preserves storage, including journals, for operator recovery, never defaults or
> overwrites it. Retained journals count toward the existing physical directory
> ceiling and must not be manually deleted or zeroed during startup or shutdown.

Upon approval, replace D21's current qualification-gate note with:

> The historical DELETE/EXTRA Windows directory-sync proof gap remains recorded
> in PersistenceFoundation1A.md. PersistenceDurabilityInvestigation.md establishes
> the built-in PERSIST/EXTRA path for implementation, not production qualification.
> Persistence-1A must still prove startup/recovery, codec, quotas, physical bounds,
> worker containment, failure handling and platform integration before PASS.

Update the decision-register row to "resolved Persistence Foundation 1
architecture; PERSIST durability amendment approved; unimplemented" and route
to that investigation. Do not mark implementation PASS.

## Required coordinated edits in PersistenceFoundation1.md upon approval

1. Section 6 fixed-storage settings: replace `journal_mode=DELETE` with
   `journal_mode=PERSIST`; retain `synchronous=EXTRA`. Add exactly:

   > Configure PERSIST/EXTRA on every connection; do not rely on the mode being
   > remembered across reopen. Retain the journal with journal_size_limit=-1;
   > do not set it to zero or use cleanup to change the commit mechanism.
   > SQLite owns journal contents and hot-journal recovery. A retained
   > store.sqlite3-journal is expected storage, not a disposable stale file.
   > Initial directory/database/journal creation and recovery must be qualified
   > before Ready. Disable cache spill for the fixed single-key workload and
   > qualify its memory and physical-journal bound. No custom VFS or external
   > post-commit file/volume-flush layer is part of Foundation 1.

2. In section 6's physical-budget paragraph, replace "worst-case DELETE journal
   growth" with "worst-case active and retained PERSIST journal allocation".
   Keep the 512 MiB database / 1,280 MiB directory / 8 MiB diagnostics ceilings
   unchanged. Preserve the explicit stop condition if they cannot be proved.
   `journal_size_limit` is still not an active-journal cap.

3. Section 7 success paragraph: replace with exactly:

   > For Set/Remove, success means the transaction containing the key mutation
   > and its quota accounting completed SQLite COMMIT successfully after the
   > required journal, database and retained-journal commit-marker synchronization,
   > and CarbonLuau validated its worker response before reporting completion.
   > There is no success at enqueue, memory update or database flush alone.
   > The PERSIST commit marker is the synchronized invalid journal header.
   > Remove's true/false is determined inside that same transaction. No public
   > multikey transaction is introduced.

4. Replace section 7's explanation that EXTRA supplies DELETE directory sync:

   > PERSIST commits by invalidating and synchronizing the retained journal
   > after database synchronization. FULL and EXTRA have the same ordinary
   > PERSIST commit sequence in the qualified pin; EXTRA remains the configured
   > policy. This avoids relying on per-commit journal deletion durability.
   > It does not eliminate initial file/directory creation, recovery, locking,
   > filesystem/device or virtualization assumptions. Process-crash recovery
   > evidence is distinct from OS-crash or physical power-loss qualification.

5. Keep the event table's process-crash promise. Replace its OS-crash/power-loss
   row with:

   > Intended durability is conditional on the qualified complete storage stack
   > honoring synchronization, ordering and namespace recovery. No tested OS-crash
   > or physical power-loss claim follows from process-kill tests.

6. Update implementation-gate/decision-index references to the journal mode
   consistently. Preserve all other public API, namespace, codec, quota,
   admission/publication, worker, no-replay, corruption and lifecycle rules.
   Link this investigation from Compatibility/AICONTEXT; preserve the prior
   negative evidence as history. Assign no new release/API/ABI identity.

## Approval boundary

Approval adopts only the changes above and permits resuming separately requested
Persistence-1A work. It does not approve 1B, widen supported filesystems, waive
crash/physical-bound qualification or claim unconditional power-loss durability.
