# World/Entity Foundation 1A — lifetime proof investigation

Canonical closure, 2026-09-23: D20 is now **HOST-PRIMITIVE-GATED / DEFERRED**;
Entity-1A remains **BLOCKED**. This file preserves the initial report as history,
including its then-uncommitted status and unchanged-D20 statement. The subsequent
[investigation and adoption](WorldEntityLifetimeInvestigation.md) supersede that
repository disposition, not the negative evidence. Actual pooled BaseEntity reuse
was not demonstrated; no production implementation is authorized.

Follow-up: [authoritative lifecycle investigation](WorldEntityLifetimeInvestigation.md)
records the expanded host/hook research and separate live-evidence status. The
sections below preserve the initial pre-implementation gate report and its 54-check
run; they are not claims about the later investigation's coverage.

## Verdict

**BLOCKED before runtime implementation**, 2026-09-22. Starting baseline:
`72efcf253f46c5d4499bd7f2b5a69ae4ceb23946` (`main`, fast-forwarded to the fetched
`origin/main`). [D20](Invariants.md#d20--worldentity-foundation-1) remains unchanged.
This record is investigation evidence, not an implemented substrate or permission
to weaken the exact-lifetime contract. Entity-1B has not started.

The required proof is missing for an unobserved **same-object, same-ID,
same-prefab reincarnation**. The currently specified sampled host evidence cannot
distinguish that sequence from a continuously live entity. A private CarbonLuau
token labels the first observation; it does not by itself detect an intervening
host retirement.

This is **not** a demonstrated ordinary-gameplay retargeting bug, nor proof that
no safe adapter can exist. Ordinary fresh network allocation on the inspected
build increases a UInt64 counter. A qualified host incarnation discriminator or
authoritative internal retirement mechanism could close the gap. Neither was
established by this bounded inspection, and no such mechanism has been added.

## Exact-build evidence

Inspected retained Rust build `25353106` assemblies from the earlier
[Player-1F-B qualification](PlayerInteractionFoundation1FB-Validation.md), whose
target was Carbon `2.0.259`. This run did not start Carbon or inspect its runtime
patch ordering. Decompiled proprietary host code remains outside version control.

| Artifact | SHA-256 |
|---|---|
| Windows `Assembly-CSharp.dll` | `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a` |
| Linux `Assembly-CSharp.dll` | `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b` |
| Linux runtime `Facepunch.Network.dll` | `568aa2009cd2798ca0f3278833bcc251e49f29dc0ee5942fc81ec48237d32302` |

[Test-EntityLifetimeEvidence.ps1](../tools/Test-EntityLifetimeEvidence.ps1) reads
assemblies through Mono.Cecil without executing game code. It passed **54
structural/model checks** on BigKVM in a container limited to one CPU and 1 GiB.
The usual `dockerbox` worker timed out; BigKVM was the previously authorized
fallback. No game server, listener or unrelated workload was changed.

The checker compares the selected IL bodies and visibility of 12 methods between
the two `Assembly-CSharp.dll` files, then checks relevant call ordering and network
allocation/pooling evidence. This is not whole-assembly equivalence, runtime-patch
qualification, or Windows execution evidence.

| Inspected path | Relevant observation |
|---|---|
| `BaseNetworkable.EntityRealm.Find(NetworkableId)` | Keyed `TryGetValue`; no world scan is needed for lookup. |
| `EntityRealm.RegisterID` / `UnregisterID` | Current occupancy can be replaced/removed; a sampled occupancy is not a historical incarnation marker. |
| `BaseNetworkable.TerminateOnServer` | Removes registry occupancy before destroying/freeing `net`. |
| `BaseNetworkable.EntityDestroy` / `GameManager.Retire` | Resets state; eligible objects are returned to the prefab pool. |
| `GameManager.Instantiate` | Can obtain an existing object through prefab-pool `Pop`. |
| `BaseNetworkable.SpawnShared` | Clears `IsDestroyed` and registers the object again. The flag is not a permanent lifetime latch. |
| `BaseNetworkable.InitLoad` | Accepts a caller-supplied ID, creates a network object for it and registers the entity. |
| `Network.Server.CreateNetworkable(NetworkableId)` | Gets a pooled network object and assigns the supplied ID; `RegisterUID` maintains the allocation high-water mark. |
| `Network.Server.CreateNetworkable()` / `TakeUID` | Ordinary fresh allocation increases the counter; `ReturnUID` is a no-op. Do not describe ordinary allocation as immediately recycling freed IDs. |
| `Networkable.EnterPool` / `LeavePool` | Clears ID and host fields; `LeavePool` is empty. Capturing this object's reference is not a proven incarnation discriminator either. |

The inspected `BaseNetworkable.creationFrame` is a private frame number, not a
unique incarnation token. Multiple lifetimes in one frame would not be
distinguished. No reflection workaround or new hook contract was adopted.

## What the counterexample proves

The checker contains a small **model**, not a production adapter. Host/VM/domain
and publication remain valid throughout; its oracle `Incarnation` field exists
only in the model, not in Rust.

1. Observe object O, ID X, prefab P, alive, registry[X] = O.
2. O retires between facade accesses. CarbonLuau does not sample the invalid state.
3. O is reused for a new lifetime, with X and P restored and registry[X] = O.
4. All sampled evidence from step 1 matches again, despite a different lifetime.

The model rejects an observed destroyed state, same-object/new-ID reuse and
same-ID/new-object replacement. It deliberately demonstrates that the combined
same-object/same-ID case passes the sampled predicate. A permanent retirement latch
only helps if some qualified mechanism observes the retirement.

The static host paths establish pooling and explicit-ID construction primitives;
they do **not** establish a live, normal-host sequence combining them while a
CarbonLuau facade remains valid. Conversely, normal fresh-allocation behavior is
not sufficient evidence for D20's stronger no-retargeting guarantee: the canonical
design explicitly avoids assuming IDs/wrappers never reuse.

## Resolution needed before implementation

Keep the existing no-retargeting guarantee. Establish one of the following through
a separately resolved host-proof investigation:

- A supported incarnation discriminator that changes on every relevant reuse,
  including same-object/same-ID restoration; or
- A complete, ordered **internal** retirement observation mechanism for observed
  entities, with coverage for pooling, load/restore, vetoed/failed destruction,
  shutdown and hotload. This must not become a public Signal, world index, polling
  loop or gameplay-resource owner; or
- A precisely justified canonical restriction on supported host lifetimes, if the
  user approves that design change. No restriction is adopted by this task.

An ordinary pre-kill notification cannot simply be assumed to prove completed
retirement. The existing design already distinguishes vetoable kill notification
from a qualified lifetime boundary. Whether a suitable Carbon mechanism exists
remains unresolved, rather than claimed impossible.

Do not add a synthetic test-adapter incarnation field and treat it as proof of
production Rust support. Do not mint a new token on every read to avoid the problem;
that would violate repeated-observation/equality requirements.

## Reproduction

Supply retained qualified artifacts and a compatible Mono.Cecil assembly:

```powershell
./tools/Test-EntityLifetimeEvidence.ps1 `
    -WindowsAssembly <windows-Assembly-CSharp.dll> `
    -LinuxAssembly <linux-Assembly-CSharp.dll> `
    -NetworkAssembly <linux-Facepunch.Network.dll> `
    -CecilAssembly <Mono.Cecil.dll>
```

This run used PowerShell in the existing `carbonluau-gui3e:latest` tool image,
retained inputs under `/srv/codex/CarbonLuauPlayer1FB20260921`, and task output
under `/srv/codex/CarbonLuauEntity1A20260922`. The checker prints input hashes/MVIDs
and stops on changed evidence. An initial checker run failed due to spelling the
global `NetworkableId` IL type with a `Network.` prefix; that assertion was
corrected against the inspected type and the final run passed all 54 checks.

## Completion/gate ledger

| Requested report field | Result |
|---|---|
| 1. Verdict | BLOCKED at exact-lifetime proof; no implementation PASS. |
| 2. Starting commit | `72efcf253f46c5d4499bd7f2b5a69ae4ceb23946`. |
| 3. Implementation/evidence commits | None; commit/push gate was not met. Investigation files are uncommitted. |
| 4. Final tested commit | No new qualified commit; only the working-tree evidence checker was run against retained host artifacts. |
| 5–8. Token, host evidence, keyed seam, predicate | No runtime substrate added. Keyed lookup is available; continuous exact lifetime is not proven by sampled evidence. |
| 9–11. Same lifetime, ID reuse, object pooling | Five model assertions; combined unobserved ABA exposes the proof gap. No live churn result claimed. |
| 12. Equality | Unimplemented; canonical host-plus-token rule unchanged. |
| 13–17. Domain, sharing, publication, modules, replacement | Unimplemented for Entity; existing behavior unchanged and no new qualification claimed. |
| 18–20. Fatal recovery, unload/reload, shutdown | Entity-specific gates not run. |
| 21–23. Cleanup, diagnostics, stress | No Entity registry/counters implemented; no stress PASS claimed. |
| 24. Exact-build inspection | 54 structural/model checks passed, including 12 selected Windows/Linux method comparisons. Proof gap remains. |
| 25. Affected regressions | Runtime suites not rerun: no runtime, native, Core, GUI, Player or public metadata changed. |
| 26. Windows | Static assembly comparison only; no new Windows runtime qualification. |
| 27. Linux | Evidence checker executed on Linux; no new live Carbon qualification. |
| 28. Sanitizers | Not run; native code unchanged. |
| 29. CI/docs | Local documentation/diff checks only; no pushed revision or new CI run. |
| 30. Identities | Unchanged: package 0.4.0; API 0.4.0-experimental; ABI 1.4; provider 1.2; schema 1; Luau `c6b830185af962c82003f86784e2fe036357c830`. |
| 31. Entity-1B handoff | Not ready; close the host-incarnation proof and implement/qualify Entity-1A first. |
| 32. Public surface | No Workspace/Entity API, userdata, metadata or autocomplete added; Entity-1B not started. |
| 33. Branch/worktree | `main` at the starting baseline, with investigation-only changes. No commit/push. |
