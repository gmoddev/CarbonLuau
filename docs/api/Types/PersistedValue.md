# PersistedValue

Since API `0.5.0-experimental`. **Experimental; scoped 1B qualification** is recorded
[separately](../../PersistenceFoundation1B.md). 1C is NOT STARTED; this is not
combined closure or release approval. This generated
recursive type alias describes ordinary values, not a runtime constructor or
userdata:

```luau
export type PersistedValue = boolean | number | string | {PersistedValue} | {[string]: PersistedValue}
```

Runtime validation is stricter than the type system. Numbers must be finite
binary64 and retain exact bits, including negative zero and subnormals; NaN and
infinities reject. Strings must be valid UTF-8 without NUL. Preserve exact IDs as
strings; persistence cannot restore precision already lost by numeric conversion.

Nonempty arrays have exactly keys 1..N and no holes or extra keys. Maps have only
string keys; the empty table represents an empty map. Reject metatables, cycles,
functions, threads, buffers, vectors, userdata and all host facades, including
Player and Entity. Nil is absence only and cannot be stored. Repeated acyclic
references become independent copies, each charged to the limits.

The [canonical value rules](../../PersistenceFoundation1.md#4-value-model-and-exact-snapshot-semantics)
and [bounds](../../PersistenceFoundation1.md#5-logical-names-and-bounds) require
depth at most 16, 1,024 entries per table, 4,096 entries across the expanded tree,
a 64-KiB encoded envelope, strings up to 16 KiB and map keys up to 128 UTF-8 bytes.
Store/key names have separate path/control restrictions. Static acceptance does
not establish compliance with these runtime bounds.

Set snapshots the complete bounded value before acceptance, using raw traversal
without metamethods. Later edits to the original table cannot change that request.
Every successful Get materializes a fresh ordinary value graph; editing it does
not save anything. There are no persistent tables or preserved reference identities.
See the [snapshot example](../../../examples/persistence/snapshot/init.luau).
