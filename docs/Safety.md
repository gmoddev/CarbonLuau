# Safety and ownership

Carbon owns the Rust host. CarbonLuau owns only its runtime resources and
registrations. Scripts receive validated identity/lifetime proxies and simple
values, never raw Rust, Unity, Carbon, managed or native objects.

All VM and host-state access belongs to the serialized server owner thread.
Host-driven recursive VM entry is prohibited; reentrant host paths admit bounded
work for a later turn. Compilation occurs in the isolated, bounded pinned worker.
User bytecode, arbitrary filesystem/network/process access and generic host
reflection/hooks are not scripting capabilities.

The project C ABI owns transport and exception containment. No C++/managed/Luau
error crosses an unprotected foreign boundary. Explicit absolute native loading
does not sandbox administrator-installed native code.

VM heap, compiler process memory, work queues, descriptor retention, actual-frame
admission and deadlines are independently bounded. A VM heap limit is not a
whole-server memory cap. Cooperative deadlines cannot preempt arbitrary host work.

Domain publication/replacement is staged and atomic at its documented boundary.
Ordinary rejected candidates retain published owners. Fatal integrity failures
retire the whole shared VM generation; no old domain survives by promise. Domain,
Player-connection and exact Entity lifetime authority never retargets by reused ID.
Teardown disconnects registrations, cancels queues and releases references before
native unloading, including partial initialization and allocation-fault paths.

Inventory operations preserve PREPARE/COMMIT/VERIFY. Pre-COMMIT rejection preserves
the documented prior behavior; post-COMMIT uncertainty is a controlled partial or
indeterminate result, without compensation/replay. Equally privileged plugins can
interfere with host state; this does not excuse unsafe reads, wrong adapters,
unbounded scans or false success. Host observations are not atomic world snapshots.

The [public API](api/README.md) owns operation-specific amounts, admission,
capabilities and failure semantics. Technical safety comments in implementation
and vendored attribution remain byte-identical to the approved source.
