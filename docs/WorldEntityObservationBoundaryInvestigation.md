# Foundation 2 — trusted observation races and bounded access

## Current disposition — 2026-10-04

[Foundation 2A](WorldEntityFoundation2A.md) owns the current implementation,
supported-host qualification, numeric policy and final closure gates. The current
qualified read conclusion is scoped to the pinned host's supported owner-thread
structural-storage contract: an admitted completed-Spawn entity supplies its
incarnation's cached root handle; an uninterrupted no-engine-entry borrow copies
capacity-checked local TRS and actual parent indices into fixed managed scratch.
At most 65 records (leaf plus 64 ancestors) are composed into a finite root-world
position. Invalid data or excess depth fails the whole query, never a subset.
Trusted external scalar/job writes may race the encounter observation; arbitrary
unsupported off-thread structural mutation is not part of this qualification.
Post-checks, catches, hash pins and elapsed timings are not replacement leases.

Intermediate measured Linux live result: fixed MonoBehaviour owner-thread pump
**PASS**, receipt `discovery-20261004-051018`, log SHA-256
`f147343edab2e4f9981398c810bb0fe591bb2ed85b786eabd17e0f6f6d46649e`.
Windows subsequently passed; final corrected-source receipts are consolidated in
Foundation 2A. Final PASS remains conditional on its final gates; this routing
note does not certify them or change
the existing public Position semantics.

## Preserved investigation record (historical scope)

All findings, negative receipts and probe limits below remain unchanged. Their
then-current unqualified-storage conclusions describe the earlier investigation,
not a rejection of the later supported-host borrow conclusion above. Neither the
relaxed observation boundary nor an access-mapping PASS alone qualified that read.

Continuation: 2026-10-04, starting HEAD
`f36ff7c83a9e273816f7b726fe3062618b430539`. All intentionally uncommitted
Foundation 2 implementation/research and negative receipts are preserved.

## Adopted consistency boundary

[I12](Invariants.md#i12--trusted-in-process-host-interference) now separates
hard safety/lifetime and resource containment, CarbonLuau-owned owner-thread
ordering, and observation races from trusted external in-process mutation.
[D20](Invariants.md#d20--worldentity-foundation-1) retains exact completed-Spawn
admission and non-retargeting authority. No Entity-1A gate is relaxed.

Discovery is not an atomic world snapshot. A candidate is considered once at its
watermarked catalog encounter. External Rust/Unity/Carbon/Oxide/physics/jobs may
change the observed state during or between encounters; CarbonLuau need not
serialize every external writer. CarbonLuau-originated effects and traversal
turns still use the same owner-thread/admission ordering. A copied encounter
observation can remain the filter input after an external move. This does not
make an arbitrarily old cache a new observation, or authorize invalid native
memory access. Any successful result still needs exact lifetime authority.

The [catalog](WorldEntityCatalogInvestigation.md) and its 17,275 Windows/Linux
model checks are inherited unchanged. The [movement](WorldEntityDiscoveryMovementInvestigation.md),
[guarded-getter](WorldEntityPositionOrderingInvestigation.md) and
[position-alternatives](WorldEntityPositionAlternativesInvestigation.md) receipts
remain history. No movement inventory, native history, generic catalog design or
guard/publisher proof was rerun in this continuation.

## New narrow candidate: bounded encounter-time scalar composition

The strongest newly relevant candidate is a read-only exact-host adapter which
copies local TRS and actual Transform parent indices, validates each index before
use, and composes root-world position with a fixed ancestor limit. It would never
call Position, hierarchyCount, Complete, Schedule, or a waiting getter inside
the borrow. Supported scalar writes can change the observation; those changes
need not force writer serialization. Each copied value and result remains
CarbonLuau-owned, with no retained native pointer or strong world ownership.

The arithmetic kernel is O(D+1), with fixed retained scratch, for a chosen
ancestor limit D. Excess depth, malformed termination/index or nonfinite output
must fail the **whole query**, not silently omit the candidate. D bounds the
number of records inspected, not the physical index of a record. Position,
rotation and scale are all needed; BaseEntity parent references alone omit
non-Entity/bone Transform ancestry.

That is a conditional algorithm, not a qualified producer. It needs a justified
structural-storage borrow: the live descriptor, TRS allocation and parent array
must remain valid throughout the uninterrupted copy. A value race and an
allocation-reclamation race are different. Freshly checking an index or object
after a read cannot repair earlier access to reclaimed storage. Merely holding
the managed wrapper does not create an additional Unity storage lease.

## New exact-host access mapping

The isolated `tests/live/CarbonLuau.WorldPositionAccessResearch.cs` maps selected
Mono internal calls; it **does not invoke** a Transform getter/mutator, create
entities, add a world scan, or install a native patch. Existing task-owned runners
accept `Access` in addition to their preserved default `Bound` and optional
`Phase`. Both hosts mapped the same ten method names and exited/cleaned up.
`OBSERVATION_PASS ... NOT_HARD_BOUND NOT_STORAGE_LEASE` certifies mapping only.

The reused exact Rust/Unity images are identified in the prior receipts: Rust
`25653776`, protocol `2634.289.1`, Unity `6000.3.15x1-13 (a91cf34396ee)`;
UnityPlayer Windows SHA-256
`6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a`,
Linux `ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1`.
The preserved Windows Carbon hook-pin refusal is not repaired or reclassified:
these are Rust/Unity access observations, **not** Windows Entity-envelope PASS.

Addresses are Windows image RVAs / Linux ELF virtual addresses, not call targets
for production. Read-only decoding uses the existing correct PE/ELF mapper.

| Wrapper | Windows | Linux |
|---|---|---|
| Transform SetParent | `0xd01e0` | `0x802590` |
| Handle SetParent | `0xd1bf0` | `0x802d50` |
| Handle IsValid | `0xd1b30` | `0x802d20` |
| Handle TryGetParent | `0xd1b80` | `0x802d40` |
| TransformAccess GetLocalPosition | `0xd2c30` | `0x803ca0` |
| TransformAccess SetLocalPosition | `0xd2c70` | `0x803ce0` |
| TransformAccess GetLocalRotation | `0xd2cb0` | `0x803d90` |
| TransformAccess GetLocalScale | `0xd2d00` | `0x803e90` |
| Object Destroy | `0xc7040` | `0x7fe620` |
| Object DestroyImmediate | `0xc7160` | `0x7fe670` |

Windows `GetLocalPosition 0xd2c30..0xd2c61` and Linux
`0x803ca0..0x803cd1` load existing TRS fields at `48 * index`, through the
hierarchy TRS pointer at `H+0x18`. Neither body waits, allocates, checks index
bounds, nor establishes a lease. Both position reads are multiple native loads,
not a promised atomic Vector3 snapshot. Exact Windows metadata marks the
TransformAccess local getter/setter `IsThreadSafe=True`; the job-access surface
has no SetParent/capacity/destruction operation. This supports separating
value writes from topology operations, not forging an access descriptor.

Independent exact-Linux review adds:

- Local setter `0x803ce0..0x803d86` writes the existing position lane and dirty
  masks. Its decoded body has no calls, allocation, descriptor replacement or
  parent-index writes. Its descendant-mask loop is writer work, not reader work.
- Capacity replacement `0xd174c0..0xd17730` waits recorded work, allocates/copies
  replacement storage, updates descriptors and frees old hierarchy storage at
  `0xd17633` through `0xd2b080`.
- Reparent `0xd17b20..0xd18c00` waits recorded dependencies before structural
  removal/allocation/copying, including free at `0xd1877f`.
- Destruction helper `0xd2b080..0xd2b0d0` waits, unregisters and frees. A raw
  reader not enrolled in that access protocol does not acquire a lease thereby.
- `H+0x10` is allocated capacity; `H+0x20` is the four-byte-stride parent array.
  `H+0x28` is a subtree-count array, not a general interchangeable slot bound.

The mapped SetParent wrappers themselves do not reject a wrong thread:
Windows Transform forwards to `0x664330`, Handle to `0x66bb70`; Linux
Transform forwards to `0xd2ef80`, Handle to `0xd17b20`. This does **not** prove
supported off-main reparenting. Unity documents main-thread restrictions and
warns release builds need not enforce them.
[Unity threading guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/async-awaitable-continuations.html).
The [documented TransformAccess surface](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Jobs.TransformAccess.html)
corroborates value access, not arbitrary hierarchy lifetime ownership.

Current public Unity C# source also describes unsafe access as relying on an
already-granted external safety premise and updates access descriptors on
reparenting. This is context, **not** exact-custom-build proof. The pinned
CoreModule has no `UnsafeTransformAccess` or `TransformHierarchy` managed type;
new master-only methods cannot be silently used here.
[Unity access reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Transform/ScriptBindings/TransformAccessArray.bindings.cs).

## Alternatives under the new boundary

| Avenue | Disposition without repeating prior inventories |
|---|---|
| Private managed copy of this encounter's world observation | Safe bounded consumer after a qualified producer. Subsequent external movement does not invalidate the copied value as an observation. |
| Spawn, save/network, player/physics or previous-operation caches | The relaxed boundary does not add missing all-Entity coverage, convert local to world coordinates, or establish a new encounter sample. |
| Fresh local TRS plus actual parent indices, fixed-depth composition | Promising newly relevant no-wait producer; storage lifetime and observation semantics must be qualified independently of arithmetic. |
| Existing public getters or moving them into a refresh callback | The native dependency wait/uncapped work remains regardless of transactional isolation. |
| Asynchronous job snapshot | The prior concrete binding/scheduling/helping costs remain resource terms, not freshness objections. Accepting external races does not bound them. |
| Engine-supported finite read-only borrow/copy helper | Can make the narrow missing access premise explicit without a spatial index. No such bounded helper is currently qualified on this custom host. |

Neither an arbitrary worker-thread Unity getter nor catch/timeout/recheck around
an unsafe native read is adopted. Bypassing waits is not itself a memory proof.
A bounded result count does not change these obligations.

## Live receipts and preservation

Fixture SHA-256 on both hosts:
`3bb3756502b67ee80dc9b37d66b39fae116271a9b66da15f89a214dfb22333cc`.

| Host | Task-owned receipt | Log SHA-256 |
|---|---|---|
| Windows | `D:\Sandbox\Codex\Entity1AStartup\evidence\position-access-20261004-041742\server.log` | `9a85e8c8d87f525016503d2d9504cdb927079d6cd16067831909c6350bc9aa27` |
| Linux | `/root/codex/world-movement-20261003/evidence/position-access-20261004-041753/server.log` | `9254a6b86a72831383a7c077a18b6283947996625d3bc56456213751d205e33a` |

Both runs used the existing disposable servers with localhost-only ports
28335/28337, reused caches/maps and no Windows security-policy change. Final
inspection found no RustDedicated process, port listener or installed Access
fixture on either host. Evidence, reusable copies and unrelated data remain.

## Disposition

The consistency boundary is adopted; a raw hierarchy borrow is not thereby
qualified. No supported concurrent structural-reclamation counterexample was
found, and complete traversal is not disproven. The remaining question is
whether the narrow direct-copy adapter can reuse a justified supported-host
structural-affinity premise, or needs an engine-provided storage borrow. Prior
Foundation 1 public-getter tests are not automatically qualification of bypassing
that getter's access protocol. Numeric traversal defaults, production catalog
activation, callback machinery and public discovery remain unassigned pending
that access decision/qualification.

No new production code, metadata, native ABI or identity change is introduced by
this continuation. No release/tag, commit or push follows from mapping success.
The existing uncommitted catalog implementation/tests and all negative history
remain preserved. Architecture/API/link/release-layout checks are separate from
Windows/Linux runtime qualification; new native sanitizer or hosted-CI PASS is
not claimed by an isolated read-only mapping fixture.
