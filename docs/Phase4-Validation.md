# Phase 4 investigation — 2026-09-14

**Current disposition: D13 resolved/deferred for v0.1 by user approval on
2026-09-14.** All item conveniences, including Items:Exists, are out of v0.1 scope.
The documentation-only closure is recorded below; the investigation is preserved.

**Historical investigation verdict: BLOCKED.** The approved construction-adapter
follow-up closes the outer factory reference gap in principle, but has not found
a supported every-path cleanup/commit strategy, even for amount=1/free-slot/no-stack.
No Phase 4 runtime/public API was implemented or claimed qualified.
See [investigation](Phase4.md#ownership-gate-d13) and the canonical
[decision register](Invariants.md#decision-register).

## Revision and versions

| Field | State |
|---|---|
| Inspected branch / starting HEAD | main, `4e5530b2a09f55edab70d80352f2868bb348c03e` |
| Qualified Phase 3 implementation | `f50d4d80ad4746dfe22c214c6316930d8b2749a8` |
| Phase 4 implementation commit | None; no runtime/API implementation |
| Investigation / closure evidence | Preserved in the D13 closure commit adding this record and the checker; its commit SHA is the Phase 5 starting baseline, not a hardening result |
| Package | Unchanged 0.3.0 |
| Scripting API | Unchanged CarbonLuau 0.3.0-experimental |
| Native ABI | Unchanged 1.2; no exports/layout/private-operation changes |
| Luau | Unchanged `c6b830185af962c82003f86784e2fe036357c830` |
| Rust / Carbon baseline | Retained Rust 2633, Steam 25230300 / Carbon 2.0.259.0 |

## Direct host evidence

Read-only Mono.Cecil inspection ran through SSH on dockerbox/HostPC using its
already installed `server-win/carbon/managed/lib/Mono.Cecil.dll`. Host assembly
code was inspected, not modified or executed for item operations.

| Assembly | SHA-256 | MVID |
|---|---|---|
| Windows Assembly-CSharp.dll | `543c0eb569ec6b3889de9aa23f61cd5af7ae5a8f0d07c2d6f1e9fb94b3d49d09` | `ebea0422-a2e5-4093-b314-efd859e6f8a0` |
| Linux Assembly-CSharp.dll | `1775aec5e28c3ab1701c1a7c27d141e5ed58f2a429e47510145c54938669d7f9` | `240a3cc6-1be0-4099-a0e5-78a2908f6e24` |
| Windows Facepunch.Network.dll | `dbc8273b85a9ac737aa8287fe97d0b5ed723b42d902429a28ef6be433f582236` | `1b92e3fd-85d3-41d3-92f8-795469860de4` |

Windows source: `C:\Sandbox\Codex\Builds\CarbonLuau\server-win\RustDedicated_Data\Managed\Assembly-CSharp.dll`.
Linux source: `/work/server-linux/RustDedicated_Data/Managed/Assembly-CSharp.dll`
in the existing stopped `codex-carbonluau-linux` container, copied for inspection
to `C:\Sandbox\Codex\Artifacts\CarbonLuau\phase4-inspection-linux.dll`.
No proprietary assembly or IL dump is added to the repository.

Compared 26 selected method bodies across ItemManager, Item, ItemContainer,
PlayerInventory, BasePlayer and ItemModContainer; all compared IL instruction
sequences were identical despite different whole-file hashes/MVIDs.
That initial ad-hoc inspection is separate from the reproducible follow-up below;
the sets overlap and must not be added together as a unique method count.

Relevant observations:

- String lookup uses OrdinalIgnoreCase short-name dictionary semantics.
- BasePlayer.GiveItem's failure branch calls Item.Drop.
- MoveToContainer mutates existing stacks before recursive remainder handling;
  its container-max-stack split branch also has a world-drop fallback.
- Factory Create has zero exception handlers and calls Initialize before returning
  the newly allocated/pool Item. Initialize obtains a UID and invokes item mods.
- Remove invokes item-mod cleanup callbacks before removal scheduling. DoRemoves
  later performs DoRemove and pooling. No safe universal exception-cleanup wrapper
  has been established.

The current [Carbon hook reference](https://carbonmod.gg/references/hooks/)
corroborates item acceptance/stack/container hook surfaces; it does not replace
the inspected build-specific method evidence or prove active subscriptions.
No old Oxide example was used as the item ownership contract.

## Approved D13 adapter follow-up

Read-only inspection on dockerbox extended to constructor/pool reset, UID service,
mod-owned entity creation, container slot callbacks, split and cleanup internals.
The inspected base-assembly methods include non-public lifecycle methods; they
were read as metadata, not invoked through reflection. There was no host patching
or production/plugin configuration change.

New findings and limits:

- Public Item constructor has no item callback and public Initialize can be called
  after retaining the outer reference. Required factory fields and initialization
  order are recorded in [Phase4.md](Phase4.md#approved-adapter-investigation-result).
- ItemModEntity.CreateEntity calls Spawn before SetHeldEntity. Spawn has virtual
  initialization calls and no compensating exception handler; outer Item ownership
  alone cannot establish ownership of every partly created child resource.
- Container Insert links before FindPosition/slot-reservation and add callbacks;
  Remove invokes a pre-remove callback before unlinking. The static paired-failure
  trace defeats amount=1/free-slot as an all-path solution. No injection was run.
- Pool reset is not full destruction. Mod cleanup, container removal and entity
  Kill are callback-fallible; DoRemoves dequeues before final cleanup. No supported
  universal abort or transaction receipt was established in the inspected paths.
- Windows Network.Server.TakeUID is monotonic and ReturnUID is a no-op. The UID
  ceiling branch stops/sleeps rather than providing an adapter error. Linux's
  Network dependency was not inspected; no UID ceiling was exercised.
- Additional Windows-only inspection of public CreateWorldObject confirms generic
  world-prefab creation, WorldItem initialization, optional parenting and Spawn
  before SetWorldEntity, with no local exception handler. It is not a cleanup
  alternative; this method is outside the 32-method comparison below.

### Repeatable structural check

[Test-ItemOwnershipEvidence.ps1](../tools/Test-ItemOwnershipEvidence.ps1) uses the
existing Mono.Cecil reader; it never executes Rust assembly code, grants items,
injects callbacks or writes an assembly. It compares 32 explicitly selected
Windows/Linux method instruction bodies and visibility, checks callback/mutation
ordering and selected absence of local exception handlers, and records hashes.
The method set is in the script. It does not compare the entire dependency graph,
Carbon's runtime patches, all exception-handler metadata, or active subscriptions.

Run on dockerbox on 2026-09-14 (PowerShell 5.1.26100.9168, about four seconds, exit 0):

```powershell
& C:\Sandbox\Codex\Workspaces\CarbonLuau\tools\Test-ItemOwnershipEvidence.ps1 `
  -WindowsAssembly C:\Sandbox\Codex\Builds\CarbonLuau\server-win\RustDedicated_Data\Managed\Assembly-CSharp.dll `
  -LinuxAssembly C:\Sandbox\Codex\Artifacts\CarbonLuau\phase4-inspection-linux.dll `
  -NetworkAssembly C:\Sandbox\Codex\Builds\CarbonLuau\server-win\RustDedicated_Data\Managed\Facepunch.Network.dll `
  -CecilAssembly C:\Sandbox\Codex\Builds\CarbonLuau\server-win\carbon\managed\lib\Mono.Cecil.dll
```

Result: **106 structural assertions passed; 32 selected method bodies/visibility
matched. D13 remains BLOCKED.** This is repeatable static evidence, not an adapter
test, a live Linux run or proof that ordinary creation leaks. The counterexamples
are conditional on exceptions/mutations at the documented callback boundaries;
their actual frequency was not measured. No viable candidate emerged to justify
a runtime prototype or live grant/failure experiment. The checker is test tooling
only, not included in the shipped scripting surface.

## Historical investigation validation status

| Originally requested Phase 4 evidence (not outstanding v0.1 requirements after deferral) | Result |
|---|---|
| Windows / Linux item API investigation | Completed static inspection, as scoped above |
| D13 repeatable structural checker | PASS, 106 assertions; 32 selected cross-platform method bodies/visibility agree |
| Deterministic adapter/fake failure tests | Not run; no viable adapter/prototype; static ordering assertions are not failure injection |
| Exists/amount/default/exact-case runtime tests | Not run; no Phase 4 API |
| Grant/stale/reconnect/provisional/deferred/permission tests | Not run |
| Creation/transfer failure injection and ownership stress | Not run; blocked ownership strategy |
| Live item state / full inventory / overflow | Not run; static transfer behavior is not live qualification |
| Phase 4 native sanitizers/allocation faults | Not run; native source unchanged |
| Phase 0–3 regressions | Prior final-source evidence remains valid; runtime unchanged |
| Phase 4 CI | Not run; no implementation commit |
| Public API docs / starter-kit example | Not added as implemented; no actual target item-state proof yet |
| Shockbyte Phase 4 | Not deployed or qualified |
| Phase 5 | Not started |

This records unprotected factory, nested-resource and partial-container failure
paths, not reproduced normal-host leaks or a claim that every host callback throws.
The unresolved issue is meeting the requested guarantee without excluding them.
No numeric limits, partial-grant/no-drop contract or new API version is accepted
by these investigation notes.

No build, test server or container was started in this investigation. At initial
inspection the task container was stopped and unrelated drycreek-bot/directus-db
were running; no container operation was needed in the adapter follow-up.
The Linux assembly inspection copy persists in the task artifact directory, and
the checker copy in the worker's CarbonLuau tools directory. Existing caches,
servers and credentials were not changed. No persistent process was started.

Local handoff checks cover PowerShell syntax, Test-Api.ps1, relative documentation
links (48 checked), git diff whitespace and scope; all passed. Runtime/native/vendor/public API sources
remain unchanged, so Compatibility.md does not require rerunning the Phase 0–3
matrix or sanitizers. No new CI or Shockbyte qualification is claimed.

At investigation handoff, five files were uncommitted: AICONTEXT.md,
docs/Invariants.md, docs/Phase4.md, docs/Phase4-Validation.md and
tools/Test-ItemOwnershipEvidence.ps1. Main was still at the recorded Phase 3 HEAD.
No commit/push had been made, and deferral was only a recommendation; the roadmap
was unchanged pending a product decision. No Phase 5 work had started.

## Approved documentation-only closure — 2026-09-14

The user approved D13 deferral and chose to defer the entire Phase 4 item
convenience surface from v0.1, including Items/Items:Exists. The ownership rules
remain unchanged. D13 is resolved as a scope decision, not as proof of safe item
construction/transfer/cleanup. Reopening conditions remain in the canonical
decision register and the retained investigation.

The canonical design now removes item conveniences from the required service/
proxy lists, configuration candidates and v0.1 definition of done. Section 31
records Phase 4 DEFERRED and Phase 5 READY for a separate task. Contributor and
public-facing docs agree; no new API, implementation, example or version bump is
introduced. The static checker is preserved unchanged from the recorded worker
run, including its historical BLOCKED diagnostic.

Closure validation is documentation/policy consistency only: 162 relative links,
roadmap/decision/evidence agreement, public API documentation/version checks,
PowerShell syntax and final diff scope/whitespace all passed. No worker build, live server,
adapter test, sanitizer, soak or Phase 0–3 rerun is requested for this closure.
Existing push-triggered CI may run automatically; this record makes no new CI or
host-qualification claim.

The closure commit on main includes the previously uncommitted investigation and
checker together with the approved roadmap/documentation changes. Identify that
baseline with `git log -1 --format=%H --diff-filter=A -- docs/Phase4-Validation.md`;
the full SHA is also reported at handoff after push verification. A self-referential
commit hash is intentionally not embedded in this file. Phase 5 is not started.
