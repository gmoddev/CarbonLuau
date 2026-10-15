# Runtime and editor development readiness

Date: 2026-10-15. Status: candidate qualification in progress. No release verdict
is inferred from the earlier runtime-only CATALOG-D9 PASS. This assessment covers
both repositories and preserves the unresolved inventory CI failure below.

## Identities and compatibility

| Identity | Candidate |
|---|---|
| Canonical API/tooling dependency | CarbonLuau `fff095f57cc53b5f4acf3d45e3ba033ba499c319` |
| Development scripting API | `0.7.5-experimental` |
| Extension implementation | `75a04af22c5bade39a8513eef6bed00d795e576b` |
| Extension package | `0.0.1`, private candidate, unchanged |
| Tooling pack format/profile | `0.4.0-rc.1`, unchanged; not the scripting API version |
| Runtime package / published tag | `0.7.0` / `v0.7.0`, immutable |
| Native ABI / provider / package schema | `1.5` / `1.2` / `1`, unchanged |
| Tooling protocol / metadata / preview plan | `1` / `1` / `1`, unchanged |
| Language server | `1.70.0`, embedded Luau `a62362a53ddc9c629b0e29378a84abb4534d8b64` |

The extension checks exact packaged source revision, API, payload hashes and
existing execution profiles before starting tools. Its selector offers only the
bundled API; configured/cached old identities fail explicitly. Old candidate
artifacts remain paired with their original runtime. Updating an editor does not
update a server. Published v0.7.0 has no Item/Prefab globals. No new package
identity or publication decision is made by this task.

## Accumulated runtime scope

[D4](GameplayD4-Validation.md) qualified barrel-2 strict destruction-time Replace;
[D5](GameplayD5-Validation.md) added wood;
[D6](GameplayD6-Validation.md) qualified independent per-prefab ownership and
atomic complete-owner publication; [D7](GameplayD7-Validation.md) added stones;
[D8](GameplayD8-Validation.md) qualified a finite 221-item manifest and bounded
stack-modifier admission. [D9](CatalogD9-Validation.md) added 221 Item constants
and Prefab.Loot.Barrel1/Barrel2 as frozen ordinary canonical strings, once per VM
generation. Other validators retain their own capability scope.

Exact supported prefabs remain:

| Full path | ID |
|---|---:|
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab` | 966676416 |
| `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab` | 555882409 |

All item IDs, vanilla stacks and qualified maxima remain in the generated
[item reference](api/LootItems.md). D8's per-definition Windows/Linux physical
receipts, 884 original Item/UID/world/Spawn results per host, stored/bonus/bag
suppression, callback interference and stack matrix are reused. No production
adapter or allowlist changes in this synchronization justify repeating them.
Pre-COMMIT rejection preserves vanilla. Completed verified worlds transfer to
host ownership; post-COMMIT partial failures have no fallback, rollback, replay
or compensation. D20 lifetime and I12 trusted-host limits remain in force.

D6 limits remain two rules globally/one per prefab, two per domain, eight entries
per rule, integer amounts 0..1000, sum(Max) <= 8000, eight worlds per occurrence,
eight admitted occurrences per actual frame and one in-flight policy frame.
Staging/descriptor/table capacities and VM/resource budgets are unchanged.

The current generated API also preserves Workspace/Entity lifetime, gameplay
Signals, PlayerInventory acquisition/mutation/sampled ItemChanged, persistence
and Query, GUI and value APIs. Former Player inventory methods remain removed:
see [inventory migration](api/Inventory-Migration.md). Generation checks derive
all metadata, definitions and docs from canonical production bindings.

## Editor discovery and security

Catalog search consumes generated canonical API metadata after exact pack and
host admission. Standard VS Code Quick Pick matches 223 rows by symbol/canonical
value and displays the canonical capability summary. Selection copies the
constant to the clipboard with a generation guard. It never edits source.
Symbol/literal limits are 128/256 characters, summary limit 4096; transport keeps
the existing 8192-member ceiling. Exact 221/2 shape, readonly literal values and
unique names/values are checked. Rows are cleared on restart/unload.

Reference hover remains static. Trusted completion/type checking/typo diagnostics
remain in the existing supervised pinned Luau LSP. Optional raw-string rewrite
actions are absent: lexical matching cannot prove that Item/Prefab are unshadowed
globals. No runtime registry, extra host hook, server connection, workspace
execution configuration, downloader or second language server is introduced.

Restricted Mode starts no LSP or preview worker. Trust/revocation, snapshots,
containment, queue/heap/deadline/process bounds, offline operation and paint-only
GUI preview retain [D19](ToolingLanguageAnalysisSecurity.md). macOS arm64 stays
static-only; macOS x64 execution/artifact support remains unqualified. Native
process controls are not a portable filesystem/network sandbox claim.

## Inventory CI investigation and negative evidence

The original D8 failure is retained at
[Windows job 116333604423](https://github.com/gmoddev/CarbonLuau/actions/runs/38708449523/job/116333604423),
assertion `old PlayerInventory acquisition still scan/watch free`.
Raw log: `C:/Sandbox/Codex/D8Control20261015/CI-bc73ee9-Windows-failed.log`,
65131 bytes, SHA256
`e6387c375980279a66ae641f292bc26cd66b97cb0f3d202179fd76855aa7f7d9`.

That revision combined Reload.Status == OK, scans == 0 and watches == 0 in one
assertion without recording the individual values. The stack identifies the
first real-VM initialization after the model/fairness measurements; no physical
Carbon host or completed-operation claim was involved. Current inspection
confirms initialization includes protected VM/domain/bootstrap/module setup and
entry execution under the existing 100 ms test callback ceiling. Compile/runtime,
resource/deadline, initialization and observation paths cannot be distinguished
from the retained failure output. No specific cause is established.

The D8 follow-up captured the same single Reload result in failure diagnostics;
six subsequent fresh Windows processes passed but did not explain the negative.
This task additionally logs status, scans, watches, elapsed milliseconds and the
bounded runtime error on every first reload, before the unchanged assertion.
It introduces no retry, warmup, altered deadline or success predicate. Passing
D9/current CI does not retroactively classify the original failure.

**Unresolved release blocker:** obtain an independently explained occurrence or
evidence that identifies and corrects the failure mechanism. It is not justified
to call it scheduling noise, a timeout, compiler startup or an inventory bug from
this log. The next experimental publication is held even if both source matrices
are green. Preserving this explicit unresolved blocker satisfies the audit
requirement; it does not establish readiness to publish.

## Qualification and artifact evidence

Pending final committed-source runs in both repositories. Runtime CI covers
Windows/Linux native/managed/real-VM, sanitizers, allocation/lifecycle/fairness,
inventory/gameplay/persistence regressions, generators/metadata/examples,
packaging and docs. The editor matrix builds pinned canonical tooling and checks
unit/security/real-tooling integration, two fresh pack assemblies, two independent
vsce/prepublish builds and deterministic normalized VSIX contents. Actual Windows
and Linux VS Code tests install each candidate into a clean profile/extensions
directory, then exercise Restricted Mode, Trust grant/reopen/revocation, language
providers, GUI, process recovery, catalog search/completion/hover/typos and cleanup.
macOS arm64 builds/checks/package remain static-only.

Reproducibility means repeat pack assembly from identical qualified compiler
outputs and repeat VSIX builds on each platform; it does not claim cross-platform
Deflate or independent compiler/toolchain byte identity. CI artifacts are private
development candidates, not release/Marketplace/Open VSX publications.

Final commits, run IDs, results and artifact hashes will be recorded after these
gates complete. The recommendation remains HOLD for the inventory blocker.
