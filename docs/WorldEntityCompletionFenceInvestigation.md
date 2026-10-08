# Entity lifetime — successful-Spawn completion-fence investigation

Historical negative evidence. The later
[Entity-1A validation](WorldEntityFoundation1A-Validation.md) supersedes this
document's **then-current** D20 gate using full-virtual-call completion and
continuous startup observation; the base-tail failure remains valid.

Research result, 2026-10-02. Starting `origin/main`/`main`:
`3c0d057c490e88bbcd9ed6d6552a2213ba7783b1`. This supplements the
[Spawn-epoch probe](WorldEntitySpawnEpochProbe.md) and does not supersede its
failed-Spawn observation. D20 remains **HOST-PRIMITIVE-GATED**. No private
production tracker, public World/Entity API, metadata, or version change was
adopted.

## Exact inspected host and evidence strength

The retained disposable-server inputs are Rust dedicated-server build
`25653776`, protocol `2634.289.1`, and Linux Carbon `2.0.261.0
[2026.10.01.0] c74c4ca`. No separate Rust source revision was available.
The Linux `Assembly-CSharp.dll` SHA-256 is
`cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`;
the installed `Carbon.Hooks.Community.dll` is
`4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343`.
The installed `0Harmony.dll` is version `2.4.2.0`, MVID
`b9e6cf65-9433-482b-8860-83cff28d0128`, SHA-256
`77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d`.
The earlier [probe](WorldEntitySpawnEpochProbe.md) records the separately
inspected Windows Rust/Carbon hashes. This follow-up did **not** rerun a
Windows completion-fence checker or live server; the new conclusion is pinned
to the Linux binary, not silently generalized to Windows.

The extended [structural checker](../tools/Test-EntityLifetimeEvidence.ps1),
with the same Linux assembly supplied in both comparison slots for this
Linux-only follow-up, passed **1,080 structural/model assertions** using
`-SpawnEpochProof -CompletionResearch`. That run is not a cross-platform
comparison. It enumerated 25 zero-argument `Spawn` method bodies in the
assembly; that count includes types unrelated to BaseEntity and is not a
promise that all 25 need patching. The earlier 1,069-check Windows/Linux
comparison and 17-check disposable live run remain separate prior evidence.

## Completion control flow and disqualifying outer tail

The exact `BaseNetworkable.Spawn()` body has one normal return, no local
catch/finally, and this relevant order:

```text
Carbon OnEntitySpawn prefix
SpawnShared / network creation / InitShared / ServerInit
network-group and post-group work
isSpawned = true
SendNetworkUpdateImmediate
Invoke(SendGlobalNetworkUpdate, 0)
conditional SendOnSendNetworkUpdate during load
return
```

The installed `OnEntitySpawned` integration occurs before the fallible update
tail. The previous isolated `IOnSendNetworkUpdate` fixture made `Spawn` throw
while the entity remained fully spawned, non-destroyed, nonzero-ID and the
exact keyed occupant. No post-success-only host marker was found in the
inspected base method: the final fallible callback can exit directly, and
the return itself writes no persistent incarnation evidence.

More importantly, a normal return from **that base method is not necessarily
a normal return from the entity's virtual `Spawn` invocation**. The exact
`BaseEntity.Spawn()` calls `BaseNetworkable.Spawn()`, then (on the server)
calls `OnParentSpawningEx.BroadcastOnParentSpawning(GameObject)` before its
own return. The broadcaster directly invokes each
`IOnParentSpawning.OnParentSpawning()` and has no exception handler;
`BaseEntity.Spawn()` has no handler around it either. Thus a component callback
can throw after a base-method success-only postfix would have marked the epoch
`COMPLETED`. The outer call would fail while the base-created fully-spawned,
keyed state can remain. This is exact IL/control-flow evidence of a possible
failure path, **not** a claim that this second path was newly observed live.
Several further derived `Spawn` bodies also do work after their base call,
including `Lift`, `SlotMachine` and `TrainCarUnloadableLoot`. A single
`BaseNetworkable.Spawn` postfix therefore does not prove successful completion
of every supported BaseEntity virtual dispatch path.

The structural checker now fails if the exact base/outer-tail ordering,
no-handler premises, or expected shipped hook metadata change. The installed
Carbon hook metadata contains no `BaseEntity.Spawn` completion patch. The
checker does **not** establish that no possible extension could add one.

## Candidate mechanisms and patch ownership

| Candidate | Classification | Result |
|---|---|
| `OnEntitySpawned` | Supported Carbon/Oxide hook, exact placement is target detail | Too early; prior live counterexample. |
| `BaseNetworkable.Spawn` Harmony postfix | Carbon's [documented AutoPatch mechanism](https://carbonmod.gg/devs/creating-hooks#automatic-patching) could own install/uninstall; exact completion semantics still require live qualification | Not enough: outer `BaseEntity.Spawn`/derived tails can throw afterward. |
| Postfix plus `__runOriginal` | [Harmony documents](https://harmony.pardeike.net/v2/articles/patching-injections.html#__runoriginal) this injection for detecting skipped originals | Necessary for prefix-skip handling, not sufficient for outer virtual-call completion. A plain postfix can also run after a prefix skips the original. |
| Finalizer | [Harmony's execution contract](https://harmony.pardeike.net/v2/articles/execution.html) permits exception observation | Would add exception-path complexity; a base-method finalizer cannot observe a later outer-call exception. No finalizer was installed. |
| Patch every applicable virtual `Spawn` override | Carbon-supported patch *transport*, but CarbonLuau-owned exact-target compatibility adaptation | Potentially covers an outer return, but needs complete override/call-chain, recursion, ordering, hot-reload and patch-interference proof **and an explicit D20 change permitting a Harmony dependency**. Not approved or implemented. |
| Wrapper/call-site interception | Exact-target implementation detail or broad compatibility patch | There is no single proved caller covering normal creation, load/restore, copy and transfer. Not adopted. |
| Persistent post-success host fact | Would be a preferable host primitive | None identified in the inspected base/outer methods. Existing fully-spawned/keyed/ID evidence is present on failed paths. |

[Harmony's execution documentation](https://harmony.pardeike.net/v2/articles/execution.html)
states that postfixes run on normal patched-method flow, including
prefix-skipped originals, but not by default when original/patch code throws.
That supports a *candidate* PENDING-to-COMPLETED transition for one exact
method only; it is not a qualified multi-method Entity lifetime fence.
The shipped Carbon `AutoPatch` facility manages plugin patch/unpatch, whereas
CarbonLuau production currently owns no Harmony/AutoPatch target. Manual
patching is specifically discouraged by [Carbon's hook guide](https://carbonmod.gg/devs/creating-hooks#manual-patching).
Current D20 explicitly rejects a Harmony/detour dependency for this read-only
surface. Carbon's support for AutoPatch is not permission to bypass that rule;
an override-wide patch would require a separate canonical architecture decision.
No patch was installed or tested by this investigation. Patch priority,
third-party prefix skip, transpiler/finalizer interference, recursive Spawn,
reload ordering and owner-thread bookkeeping remain unqualified, not waved
away under I12.

## Pre-existing and restored entities

The prior disposable startup log places plugin loading before that run's
`Spawning World` and `Server startup complete` messages. This is useful
*observed ordering*, not a universal guarantee that CarbonLuau installs a
completion patch before every save/load restoration or every supported server
configuration. CarbonLuau's current `Loaded()` prepares native/facade state,
while its root bootstrap is in `OnServerInitialized`; no Entity completion
patch exists at either point.

Hotloading/reloading CarbonLuau into an already-running world necessarily
encounters entities whose prior Spawn returns were not observed by a newly
installed fence. `InitLoad` can make an entity keyed before Spawn, but that
state alone is not admissible; save/load Spawn success would be observable
only if the fence was installed in time. A later server-initialization/load
notification and a present-state recheck cannot distinguish a completed Spawn
from the recorded failed-yet-fully-spawned/keyed object. No bounded,
nonmutating reconciliation handshake or persistent host success fact was
established. The old lazy hotload baseline must **not** be restored.

**Pre-existing admission result: PREEXISTING-ADMISSION-DEFERRED as a safe
candidate restriction, not adopted public behavior.** Admitting only
directly observed completed epochs would omit arbitrary pre-existing world
entities on plugin hotload; an exact-ID lookup with that omission is a
material product/API choice requiring review. It does not by itself close
the outer-call completion gap above.

## Disposition

**WORLD/ENTITY STILL HOST-PRIMITIVE-GATED.** A base-only successful-return
fence is insufficient on the exact target. No complete supported outer-call
fence or pre-existing-entity baseline has been proved. The conceptual
PENDING/COMPLETED/optional FAILED state machine remains a research hypothesis;
no epoch/token tracker, admission helper, public binding, or D20 spawn-epoch
amendment is present. Failed/PENDING epochs would have to remain inadmissible,
and an old admitted token would still need immediate sticky fencing at the
start of a new attempt. The prior failed-Spawn counterexample and negative
registry/pooling evidence remain intact.

No new live matrix, Windows completion test, sanitizer run or production
regression is claimed, because the candidate failed structurally before
implementation. No commit/push or Entity-1B work is authorized by this result.
The next architecture decision is whether a larger CarbonLuau-owned,
exact-target completion patch across the virtual Spawn chain **and** a
restricted policy for entities predating installation are acceptable, or
whether Entity remains deferred pending a stronger supported host primitive.

## Continued Entity-1A review: AutoPatch transport and hotload baseline

The subsequent task explicitly permitted **using** a narrow Carbon
AutoPatch/Harmony adapter if needed and required support for pre-existing
entities. That permission supersedes the prior categorical Harmony exclusion
for this investigation, but does not by itself establish a safe completion
fence or amend D20. The intentionally
uncommitted investigation state remains preserved.

The same retained Linux target was re-pinned: Rust app `258550`, build
`25653776`, protocol `2634.289.1` (server log revision `166494`), Carbon
`2.0.261.0 [2026.10.01.0] c74c4ca`. The inspected Rust, Community hook and
Harmony hashes are listed above; installed `Carbon.Hooks.Base.dll` is SHA-256
`847eebbb34dc0db7636e3a1c2ee4380d03d0e828e6a55b7eee6ff885c200f8bc`.
The previous Windows assembly/hook comparisons remain inherited evidence, not a
new Windows run of this continuation.

### Completion candidates

The non-Harmony search found no supported per-invocation acknowledgement after
the *full virtual* `Spawn` returns. `OnEntitySpawn` is a prefix;
`OnEntitySpawned`, network publication and the base-method return can all
precede a failing tail. `OnEntityLoaded` is a load notification, and server
initialization is a global readiness notification. No single qualified outer
caller covers Rust creation, restoration, transfer and plugin-created entities.
The exact-host override inventory and patch-coverage analysis must remain a
separate gate before any adapter could be installed.

The research checker now asserts an exact inventory of **23 concrete
`BaseEntity` virtual `Spawn` override bodies**, in addition to the abstract
`BaseNetworkable` body; the 25th zero-argument `Spawn` body is unrelated
`SpawnGroup.Spawn`. Each of the 23 overrides calls its nearest base `Spawn`
body exactly once on the inspected Linux assembly. `CargoShip` and
`JunkPileWater` perform work before that base call; other overrides have
fallible tails, including child-entity Spawn calls. A prospective fence
would therefore need entry and normal-exit coverage across the complete
effective invocation, not just the base body. The updated checker passed
**1,220 structural/model assertions** with the same Linux assembly in both
platform slots. This is Linux target research, **not** a Windows comparison,
installed-patch test, or successful completion theorem.

Carbon's plugin-owned AutoPatch can transport a narrow patch, but patching the
base method alone still fails the outer-tail counterexample. On the shipped
Harmony version a postfix can run after another prefix skipped the original;
`__runOriginal` must be checked. A postfix observes its own place in the
patch chain, not necessarily the final result of later postfixes/finalizers.
AutoPatch installation order is not execution priority. Carbon unpatches
plugin-owned patches on unload, and an unsuccessful unpatch needs fail-closed
handling. These are adapter qualification issues, not reasons to pretend that
Harmony itself supplies historical Spawn-completion evidence.

### Why present-state bootstrap is not a completion proof

The retained live fixture already demonstrated a normal plugin-owned call to
`Spawn` that threw while leaving the object fully spawned, non-destroyed, with
a nonzero ID and exact keyed registry occupancy. The exact `BaseEntity.Spawn`
IL also permits an exception in `BroadcastOnParentSpawning` *after* the base
method and its network-update work complete. These are different failure
points; the second is structural, not a newly observed live result.

The exact `BaseNetworkable.IsFullySpawned()` reads only `isSpawned`, which is
set before those fallible tails. Carbon's installed `IOnServerInitialized`
postfix sets a global initialized flag and dispatches a server event; it
does not record which entity's full Spawn returned. On save restoration,
`SaveRestore.Load` catches exceptions and returns `false`, while
`ServerMgr.Initialize` branches into fallback map spawning rather than using
that flag as a per-entity success record. This observation does **not** claim
that every such failed startup reaches a healthy running server.
Other apparent host registries are also too early: `SpawnShared` registers
the transform before `ServerInit`, while `BaseEntity.ServerInit` adds a
savable entity to `saveList` and the server query index before the later
`isSpawned` write and outer tail. They are not success markers.

First-time CarbonLuau hotload is the decisive case. A newly installed patch
did not observe earlier calls. A prior plugin can catch a failed Spawn and
leave its fully-spawned/keyed object present; the live fixture proves that
state exists at the catch point. The host's present flag, ID, registry,
object and prefab snapshots do not distinguish it from a successful object.
Removing a throwing test component afterward can erase that particular
component clue; this latter step is a supported-host inference, not an
additional live test. No bounded, read-only, authoritative historical
completion marker was identified in the inspected base types or Carbon hooks.
An ordinary earlier-installed plugin postfix that throws after the original
`Spawn` body supplies an even sharper structural ambiguity: the host entity
can have received every original-body state write while the effective patched
invocation still exits by exception. This is a Harmony control-flow
possibility, **not** a newly run live fixture. A patch first installed after
that invocation cannot recover its return outcome from current entity fields.

The investigated baseline approaches therefore remain unresolved:

| Candidate | Limitation |
|---|---|
| Install AutoPatch during initial plugin load | Can observe future calls, but not calls before first-time hotload. |
| Server/world-load-complete event | Global readiness does not certify every current entity's full Spawn result, including later plugin-created objects. |
| Successful `SaveRestore.Load` | At most speaks to that restoration call; it neither labels all later entities nor covers hotload after arbitrary plugin activity. |
| Current-state bootstrap scan/lazy lookup | Rechecks the failed-Spawn predicates and cannot manufacture missed history. |
| Reinvoke Spawn to establish completion | Mutates Rust world/network state and is not a safe read-only baseline handshake. |
| Persistent host-wide completion marker installed before world creation | No such supported marker was found; adding one would change deployment/lifecycle architecture and still require a policy for first install into a running world. |

Consequently neither a private tracker nor the proposed D20 spawn-epoch
amendment can be adopted from this evidence. An observed-only fallback would
violate the stated product requirement. A future safe path needs an
authoritative host-provided historical success fact or an explicit decision
about first-install/reload deployment and baseline eligibility; it cannot be
silently substituted with the `fully spawned + keyed` predicate.
