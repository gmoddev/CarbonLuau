# Player Interaction Foundation 1F-B — GiveItem evidence and requalification

Date: 2026-09-21. Starting implementation baseline and investigation source:
`3f6a3196b28e2243dc8802d3b8ff2374196f3c89`, branch `main`.

**Policy verdict: CANONICAL BASELINE READY. GiveItem G1: PENDING REQUALIFICATION,
not PASS.** The investigation initially stopped implementation under the prior
whole-process no-drop interpretation. The user subsequently adopted the general
[I12 interference boundary](Invariants.md#i12--trusted-in-process-host-interference)
and amended D13/D18. That resolves the policy question, not the adapter gate.
No runtime implementation is authorized by this architecture/evidence task.

## Scope and provenance

The original implementation request was `Player:GiveItem(ShortName, Amount)`.
The now-canonical intended shape is
`Player:GiveItem(ShortName, Amount, Behavior?) -> boolean`, defaulting to
`GiveItemBehavior.InventoryOnly`. Neither the method nor enum is exposed yet.
[Inventory-M2](InventoryMutationM2Validation.md), D11/D13/D18, the Player-1C
scanner and [Player-1F-A](PlayerInteractionFoundation1FA.md) remain authoritative.
No public binding, production planner, mutation adapter or API example was added.

After the DockerPC/dockerbox outage, the user explicitly authorized the big VPS,
with the controlling PC as fallback. The configured `BigKVM` SSH alias worked.
A task-owned directory `/srv/codex/CarbonLuauPlayer1FB20260921` holds downloaded
artifacts/tools. Disposable containers used one CPU and at most 2 GiB; no server,
listener or unrelated service was started, restarted or modified.

SteamRE DepotDownloader 3.4.0 retrieved only `Assembly-CSharp.dll` anonymously
from the exact M2 manifests. Both SHA-256 values and MVIDs match M2:

| Target | Depot / manifest | SHA-256 |
|---|---|---|
| Windows | `258551` / `7816298056519226227` | `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a` |
| Linux | `258552` / `3352454092778561960` | `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b` |

ILSpy 8.2.0.7535 and Mono.Cecil were used for read-only inspection. Decompiled
host files remain in ignored/task-external storage, not tracked source. These
are vanilla assemblies; no new Carbon-patched live execution is claimed.

## Exact-path finding

Manual control-flow inspection, supplemented by
[Test-GiveItemNoDropEvidence.ps1](../tools/Test-GiveItemNoDropEvidence.ps1), found:

1. Even with an explicit slot, stacking as selected, `ignoreStackLimit=false`
   and `allowSwap=false`, `Item.MoveToContainer` invokes `Item.CanMoveTo`.
2. `CanMoveTo` calls `ItemContainer.CanAcceptItem`, which invokes the public
   `canAcceptItem` delegate. This occurs **inside** the transfer, after any
   caller-side revalidation.
3. On the empty-slot path, the method subsequently reads `maxStackSize`. A
   positive limit below the current source amount enters `SplitItem`.
4. If the generated split cannot be placed and the original source had no
   container, that branch calls `Drop`. The outer transfer can return true.

The following is a **source-derived counterexample**, not a live observation:

- PREPARE selects an empty main slot for a scrap chunk of two, with a limit
  allowing two. Creation returns an unattached source of two; an immediate
  caller-side guard still sees the valid plan.
- The first acceptance delegate invocation sets the target's `maxStackSize`
  to one and accepts. Ordinary remaining acceptance conditions are satisfied.
- The host now sees a chunk larger than the changed limit and splits one.
- The delegate rejects the nested transfer of the split. Because the original
  source was unattached, the outer branch can drop that split.

The delegate need not call Drop itself. The forbidden fallback is in the exact
adapter path CarbonLuau was instructed to use. A last-moment pre-call check,
same-Player Luau gate, or post-call indeterminate result cannot prevent an
internal callback from changing the subsequently tested limit. This is distinct
from attempting to sandbox a trusted plugin that deliberately drops items on
its own. This finding failed the previous whole-process G1 interpretation.
Under the now-approved I12 boundary it is an external-interference example,
not ordinary supported-host behavior and not a qualified success result.
The original blanket M2 G1 proof remains superseded, rather than restored.

The generated split also is not the original returned Item. Checking only that
original reference for a world entity would not establish absence of a drop.
The physical VERIFY predicate can detect insufficient delivery, but it does not
retroactively make this a qualified inventory-only path.

An additional scope requiring review is `RemoveConflictingSlots`: the explicit
empty-slot path calls it when slot masks are present. It can detach conflicting
items and invoke generic GiveItem or automatic placement. This is a separate
reason to qualify accepted container restrictions rather than infer safety
from `allowSwap=false`. No claim is made that vanilla main/belt/wear always
exercises this branch. If baseline host behavior reaches a prohibited path,
that remains CarbonLuau responsibility; I12 cannot excuse it without evidence
of external premise-invalidating mutation.

## What the checks establish, and what they do not

Historical investigation output on BigKVM, using the exact Windows and Linux
assemblies (the BLOCKED label predates I12 adoption):

```text
[CarbonLuau:InventoryM2] Static adapter evidence passed: 92 checks.
[CarbonLuau:Player1FB] Static exposure confirmed: 51 checks; GiveItem G1 remains BLOCKED pending resolution.
```

The supplement checks pinned hashes, identical selected Windows/Linux IL,
callback/limit/split/drop ordering, the conflict-removal path, and public mutable
acceptance/limit fields. It is a regression record of the relevant structural
facts; linear IL assertions alone are not a complete reachability proof. The
counterexample above additionally relies on manual branch inspection.

The existing M2 live successes remain valid for the cases they exercised:
scrap insertion/merge with stable limits, acceptance rejection, exceptional
insertion, cleanup and Take. Their unchanged checker passing does not extend
those cases to limit-changing callbacks. No production GiveItem implementation
or new real-host interference fixture was run. No new authenticated-client
result is needed or claimed. Carbon 2.0.259 live behavior remains prior M2
evidence, not a fresh result from this investigation.

## Adopted scope and required 1F-B requalification

I12 owns the general external-interference definition and exclusions. D13 owns
the revised InventoryOnly G1 wording and unchanged success/failure predicates;
D18 owns the intended optional Behavior enum. This record supplies the evidence
and test matrix, not a parallel policy. Policy adoption is not implementation
authorization and is not a G1 PASS. The existing TakeItem gate and common
physical scanner remain the intended owners; no second mutation scheduler or
inventory model is introduced.

### Required 1F-B requalification

On the exact supported Rust/Carbon baseline, require all of the following:

| Area | Required evidence |
|---|---|
| Supported callbacks | Vanilla/default acceptance, normal acceptance, normal rejection, and inspecting callbacks that do not mutate relevant state. Rejection is not external interference. |
| Complete bounded planning | Full inventory and insufficient total capacity reject before creation; partial compatible stacks, exact stack completion, empty slots and multi-chunk grants fit the whole amount within the shared 128-entry/chunk envelope. No callback-capable speculative PREPARE. |
| Qualified invocation | Exact target container/slot; no automatic slot, swap, ignoreStackLimit, generic GiveItem or CarbonLuau-selected Drop; no oversized planned chunk under the qualified baseline. Inspect conflict-slot behavior and baseline item initialization as well as acceptance. |
| Returned responsibility | Account immediately for each returned resource; prove accepted, consumed-merge and qualified temporary states; supported cleanup, exceptional insertion and cleanup failure; no abandoned reference or global removal-queue drain. |
| VERIFY and outcomes | One fresh bounded physical scan plus every D13 success predicate, not quantity or host return alone; false only before COMMIT; uncertain COMMIT errors, no rollback or replay. Full PREPARE rejection creates no Item; normal host rejection after creation is indeterminate. |
| Existing protections | D11 exact connection, shared Give/Take gate, root/addon and recursive admission, provisional and shared-module restrictions, deferred publication, replacement/unload/recovery and unchanged TakeItem regressions. |

Requalify the exact host adapter with controlled host fixtures before resuming
production implementation; subsequently qualify the actual production planner,
facade, accounting, lifecycle and stress behavior before claiming 1F-B PASS.
Static checks, old M2 markers or this scope amendment alone cannot satisfy that
gate. Preserve qualified G2-G5 evidence where unchanged and rerun affected
cases. No authenticated client is required for these server-authoritative tests.

Retain the hostile-mutation counterexample and any existing fixtures as
**boundary tests**, separate from supported-host success evidence. Qualify
controlled callbacks changing maxStackSize or Item.amount, and relevant
stack/slot/parent/inventory/world mutations. Record the actual mutation, phase,
host outcome and resource/physical observations; no attribution from an
unexplained failure alone. The existing counterexample is static, not a live
fixture result; live boundary tests remain pending.

If a resulting world-delivery or unqualified/uncertain resource state is
detected, report the existing controlled indeterminate failure and bounded
diagnostics; do not hide the effect, claim rollback, retry automatically, or
count it as InventoryOnly success. Known-invalid premises must not be passed
knowingly into another transfer. Interference with unrelated state permits
success only when every existing operation-specific predicate still proves it.
No whole-process forensic isolation or exhaustive external-mutation detection
is promised.

### Future behavior scope

InventoryOnly is the default and only accepted intended enum member. Full
PREPARE inability to fit returns false before COMMIT; post-COMMIT uncertainty
errors. `DropRemainder` is only a future design name, not reserved or exposed.
D18 requires separate design/qualification before any such member is accepted.
There is no boolean DropIfFull, no generic GiveItem shortcut and no identity bump.

## Completion boundary

- Planner, returned-resource accounting, cleanup, physical VERIFY and shared
  Give/Take gate implementation: not started; no new runtime results claimed.
- GiveItem failure injection, lifecycle, stress, live Carbon, Windows/Linux
  runtime and sanitizer gates: not run because implementation stopped at G1.
- Existing TakeItem/Inventory, addon, GUI and broader runtime evidence is
  preserved. No production source changed; no new regression PASS is implied.
- Documentation/API and architecture consistency checks passed on the adoption
  working tree, as did `git diff --check` and evidence-checker syntax parsing.
  A comparison with the starting commit confirmed D13's operational text
  before the M2 evidence paragraph is unchanged. Diff/surface audits confirmed
  no production, TakeItem source/evidence or release-identity changes, and no
  GiveItem/behavior enum/DropRemainder runtime exposure. These lightweight
  checks are not new runtime, live-host or final-source CI qualification.
- Package `0.4.0`, scripting API `0.4.0-experimental`, native ABI `1.4`, provider
  protocol `1.2`, schema `1` and pinned Luau remain unchanged.
- No DropIfFull, DropRemainder, DropItem or Player-1F-C work began.
- The initial investigation made no commit because implementation gates had
  not passed. The subsequent user request separately authorizes committing
  this reconciled architecture/evidence baseline after consistency checks;
  it does not require or claim runtime G1 PASS. Adoption/evidence are committed
  together; the resulting SHA is reported in the completion response and Git
  history, avoiding a self-referential commit hash in this file.
- No persistent task container/server remains; prior artifact caches remain
  for follow-up under the task-owned directory. This policy continuation runs
  only local lightweight consistency checks, not expensive runtime/server tests.
