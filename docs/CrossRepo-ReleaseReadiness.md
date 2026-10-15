# Runtime and editor development readiness

Date: 2026-10-15. Status: runtime/editor implementation qualified; experimental
publication held for the unresolved inventory CI failure below. No release
verdict is inferred from the earlier runtime-only CATALOG-D9 PASS. This assessment
covers both repositories; final documentation commits require their own CI before
delivery.

## Identities and compatibility

| Identity | Candidate |
|---|---|
| Canonical API/tooling dependency | CarbonLuau `fff095f57cc53b5f4acf3d45e3ba033ba499c319` |
| Development scripting API | `0.7.5-experimental` |
| Runtime audit/diagnostic implementation | `18104098b6fa6b8298787eddff83873dec44a29b` |
| Extension implementation | `c2d3ac9ad27af3c7fe97447ea73bd1ab91d4649d` |
| Final editor source pinned by runtime CI | `838ed27c6a627a5e4030f9e64f8744c590842a97` |
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

Unchanged D9 memory/lifecycle evidence at the qualified canonical dependency:
30,696 bytes for native catalog construction, two retained VM references, and
24,552-byte typical paired D8/D9 VM delta (allocator-page effects are recorded in
D9). The representative 100-addon managed fixture used 8,625,384 bytes under the
existing 64-MiB heap. All 28 actual initialization-allocation failure prefixes
clean up to zero bytes. Healthy replacement shares the frozen tables; recovery
constructs a fresh generation. This synchronization changes none of those paths;
current native/VM regressions recheck the same bounds, not a new memory allowance.

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

Runtime implementation 18104098b6fa6b8298787eddff83873dec44a29b passes all four
hosted workflows:
[runtime and sanitizers](https://github.com/gmoddev/CarbonLuau/actions/runs/38813149842),
[tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/38813149926),
[baseline contracts](https://github.com/gmoddev/CarbonLuau/actions/runs/38813149928)
and [documentation](https://github.com/gmoddev/CarbonLuau/actions/runs/38813149861).
Windows has 23 native tests, Linux 24, and ASan/UBSan/leak checking 24, all PASS.
Managed/real-VM, allocation faults, 100-addon fairness, GUI, inventory, gameplay,
persistence, tooling/generators/metadata, deterministic packaging and clean
installation pass. Each clean Windows/Linux bundle compiles all 75 examples and
executes the GUI/Player fixtures before teardown. The new inventory diagnostic
records OK/zero scans/zero watches, Windows 60.423 ms and Linux 17.243 ms; these
whole-reload observations do not identify the historical failure's mechanism.

Editor implementation c2d3ac9ad27af3c7fe97447ea73bd1ab91d4649d passes
[its complete matrix](https://github.com/gmoddev/carbonluau-vscode/actions/runs/38814564532).
Windows/Linux each pass all 48 unit/security/real-tooling checks, zero skips.
macOS arm64 passes 35 with 13 deliberately unsupported execution-profile checks
skipped. Each platform repeats canonical pack assembly in a fresh directory and
runs two independent vsce/prepublish builds before deterministic ZIP normalization.
All payloads are identical within each repeat pair.

Actual Windows/Linux VS Code 1.95.3 tests install the candidate into a clean
profile/extensions directory, then pass Restricted Mode, Trust grant/reopen/
revocation, language providers, GUI, process recovery and cleanup. Catalog
receipts prove exact canonical and symbol queries, copied constants, unchanged
source, 221/2 completion, literal/capability hover, misspelled-field diagnostics,
picker cancellation and old-API rejection/recovery. The shipped new example
typechecks. Runtime CI independently verifies all 221/2 completions in root entry,
root module, addon entry and addon module. No optional raw-string rewrite action
is claimed. macOS stays static-only with no actual editor installation or
executable analysis claim.

The [runtime evidence ledger](qualification/CrossRepo-ReleaseReadiness-Evidence.json)
records exact source dependencies, all four workflow/job identities, six runtime
candidate artifact hashes, canonical source hashes, seven raw receipt hashes and
immutable published-release identities. The editor's
[evidence ledger](https://github.com/gmoddev/carbonluau-vscode/blob/838ed27c6a627a5e4030f9e64f8744c590842a97/docs/qualification/ReleaseReadiness-Evidence.json)
records all three implementation VSIX hashes/build IDs and 41 raw receipt hashes.
Every canonical API metadata, tooling metadata, definition, doc and symbol file
matches the qualified authoritative runtime bytes; every packaged payload hash
is verified. The [editor report](https://github.com/gmoddev/carbonluau-vscode/blob/838ed27c6a627a5e4030f9e64f8744c590842a97/docs/ReleaseReadiness.md)
also retains both failed UI-driver attempts and the isolated successful Linux
development fixture. Neither driver correction changes an assertion or deadline.

Reproducibility means repeat pack assembly from identical qualified compiler
outputs and repeat VSIX builds on each platform; it does not claim cross-platform
Deflate or independent compiler/toolchain byte identity. CI artifacts are
development candidates, not release/Marketplace/Open VSX publications.

This evidence closure changes documentation and the exact editor test pin only.
The runtime source containing this report and editor
838ed27c6a627a5e4030f9e64f8744c590842a97 must both receive their own exact-source
CI before delivery. Final delivery records those tested SHAs and immutable run
links; match workflow headSha rather than an advanced main badge. Implementation
artifact hashes above belong to their stated source, and final closure artifacts
have their own provenance/checksums.

Published tag object 7c7d36f648a5506efd07c1b6d6bcf4b94d3f7d2b still peels to
dabb872094f74e60d75de099379d198fb63a6bf8. Release 410150414 and all six asset
IDs, digests and update times equal the retained published baseline. CI candidate
bundles bearing the old package filename are not those immutable published assets.
No release/tag, Marketplace/Open VSX or public VSIX publication occurs here, and
no next package version is selected. No Rust server is changed by this task;
D8/D9 exact pinned Windows Carbon 2.0.262/8a81d70 and Linux Carbon 2.0.261/c74c4ca
physical evidence and restored-server receipts are retained. The temporary Linux
editor container exits zero and is removed; all eight protected containers remain
unchanged. The focused Windows fixture leaves no owned runtime/compiler process.

Recommendation: **HOLD the next experimental publication** until the historical
inventory initialization failure is explained with evidence and any necessary
fix is qualified. Then assign the candidate package identities explicitly under
D12 and complete the existing publisher/licensing/attestation/platform
distribution gates before separately authorized publication. No additional
gameplay investigation or Loot expansion is needed to resolve this audit blocker.

## Retained synchronization attempt

The first runtime candidate `17ba1ccb9c908a13c4fdec6fe5d314c99ddf6f11` failed
its Linux catalog editor test in
[job 116688561569](https://github.com/gmoddev/CarbonLuau/actions/runs/38812393517/job/116688561569).
The newly strict extension correctly rejected a runtime candidate pack whose
SemanticRevision differed from its packaged fff095f source pin; completion was
empty and the diagnostic was `Incompatible CarbonLuau tooling pack.` The test
fixture now pins its copied editor to the actual current runtime candidate,
asserting exact pack revision and API first. It does not relabel the pack or
change the extension repository's qualified shipping dependency. All completion
assertions, admission checks and deadlines remain intact.
Raw job log is retained at
`C:/Sandbox/Codex/CrossRepoReadiness20261015/Runtime-Tooling-Linux-negative.log`,
SHA256 `890c9567be575a8dc1e74cfe2c79c31edb03ac451030b9251d0e204253e70562`.
The original result.json/vscode.log are retained in that directory's
Runtime-Editor-Negative subdirectory and the job's catalog-d9-editor artifact.
