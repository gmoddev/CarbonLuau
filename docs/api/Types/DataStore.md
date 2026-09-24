# DataStore

Since API `0.5.0-experimental`. **Experimental; 1B implemented and qualified within
[recorded scope](../../PersistenceFoundation1B.md).**
[1C](../../PersistenceFoundation1C.md) owns combined qualification; package release
approval remains separate. Available in development source
containing the production bindings. Obtain this sealed,
private facade from [DataStoreService](../Services/DataStoreService.md).
No fields, constructor, Close, request handle or cancellation API is exposed.

```luau
DataStore:GetAsync(Key: string, Callback: (PersistedValue?, string?) -> ()) -> ()
DataStore:SetAsync(Key: string, Value: PersistedValue, Callback: (boolean?, string?) -> ()) -> ()
DataStore:RemoveAsync(Key: string, Callback: (boolean?, string?) -> ()) -> ()
```

These are signature notation. All three methods are **non-yielding** submissions
with required callbacks and no immediate return values. Immediate return means
acceptance only, never successful storage. No Promise, wait/await, synchronous
alias or fire-and-forget write is provided.

| Completion | Callback arguments |
|---|---|
| Get found | fresh value, nil |
| Get absent | nil, nil |
| Set durably committed | true, nil |
| Remove committed, key existed | true, nil |
| Remove absent | false, nil |
| Accepted operational failure | nil, ErrorCode |

`false` is a stored value; test `Value == nil` for absence. Set with nil rejects;
use Remove to delete. No missing Get/Remove creates durable rows. The complete
callback/error contract is owned by [the design](../../PersistenceFoundation1.md#completion-and-errors).

Keys are exact, case-sensitive strings of 1..128 UTF-8 bytes with the
[store-name character restrictions](../Services/DataStoreService.md); no
number-to-string coercion occurs. `Player.UserId` is already
a string; `tostring(Player.UserId)` preserves it. Do not convert IDs through a
number. Values follow [PersistedValue](PersistedValue.md).

Async reads and writes require an existing committed admission **and no active
publication scope**. Candidate initialization and first-load modules cannot
submit, even through pcall, dependencies or previously captured facades. Use
`task.defer` to submit after successful publication. Failed candidates/modules
discard that deferred work. Acquisition itself is disk-free and may be provisional.

Submission rejects malformed arguments, invalid values, missing/nonfunction
callbacks, foreign/stale facades, provisional execution, exhausted queue/rate
capacity and service-not-ready synchronously. A `pcall` around submission catches
these failures; rejected calls owe no callback. It does not catch errors thrown
later by a callback. Callback errors cannot roll back accepted storage; a callback
deadline violation retains the existing VM-fatal policy.

Accepted failures report `QuotaExceeded`, `StorageUnavailable`, `StorageBusy`,
`StorageFull`, `StorageCorrupt`, `FormatUnsupported`, `DeadlineExceeded`,
`StorageError` or `Indeterminate`. Never treat an error as absence and overwrite a
default. Indeterminate means a dispatched mutation may have committed. Do not
blindly retry; no automatic replay, rollback, exactly-once effect or delivery is
promised. Corrupt/incompatible storage fails closed and is preserved for operator
recovery. Other mutation failures require affirmative evidence of no commit.

Set snapshots before acceptance. Completion runs later on the owner thread as a
new bounded admission; it never resumes the original frame or resets its deadline.
Per-namespace acceptance FIFO includes different stores. Pending capacity includes
executing and completed-undelivered work: 8 per namespace, 128 globally. Requests
have five seconds from acceptance to completion intake; callback delivery may be
later under scheduler fairness. Domain/VM retirement suppresses delivery, but a
dispatched write may still commit. New work is fenced behind its outcome/recovery.

Success follows the checked durable commit and validated worker response under
[the qualified storage assumptions](../../PersistenceFoundation1.md#7-durability-atomicity-and-uncertainty).
It is not an arbitrary hardware power-loss guarantee. Get-then-Set is not an atomic
increment; different keys are not a transaction. [Full bounds](../../PersistenceFoundation1.md#5-logical-names-and-bounds)
remain canonical. See [six examples](../Persistence.md) and [qualification](../../PersistenceFoundation1B.md).
