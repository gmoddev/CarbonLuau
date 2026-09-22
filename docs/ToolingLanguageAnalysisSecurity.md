# D19 language-analysis security amendment

Decision: **READY TO RESUME TOOLING FOUNDATION A** under the requirements below.
Accepted for architecture by the 2026-09-21 resolution task. This is permission
to implement and qualify the amended design, not a passing implementation gate.
The preserved extension's unconditional LSP startup block must remain until its
replacement controls pass. This document owns the architecture; subsequent
[Foundation A implementation evidence](ToolingFoundationACompletion.md) owns
platform qualification. macOS remains static-only by the user's explicit
decision when no macOS runner was available; no executable-analysis exception
or reduced security profile is implied.

## 1. Decision in brief

Select a **Workspace Trust-gated, tooling-supervised language-analysis process**.
Restricted Mode gets parse-only syntax and canonical metadata/package/project
diagnostics; it never starts luau-lsp. Trusted workspaces automatically get full
CarbonLuau language analysis after pack/platform qualification, including Luau
type functions, outside the extension host and static coordinator.

Analyze bounded, materialized snapshots using CarbonLuau-owned JSON configuration.
Exclude workspace `.config.luau`, `.luaurc`, `.robloxrc`, LSP settings and arbitrary
plugins in both trust modes. Preserve source text, including type functions;
only the canonical require-analysis adapter may edit analysis inputs. Never
silently erase type functions or substitute `any` and claim successful checking.

This is a hybrid of restricted-mode non-execution, configuration admission,
process supervision and explicit Workspace Trust. It does **not** promise a
portable OS sandbox. A snapshot and child process reduce exposure but do not
remove the native process's OS authority. Trust is necessary precisely because
the language VM/compiler and C++ server remain part of the trusted computing base.

## 2. Exact upstream scope and execution surfaces

Inspected released luau-lsp **1.70.0**, source
`876d84b8caf4454c8df806ddc9eee0565228d7b6`, embedded Luau
`a62362a53ddc9c629b0e29378a84abb4534d8b64`. GitHub's latest-release endpoint
returned 1.70.0, published 2026-09-20, at investigation time. Upstream main was
`fcf6d84e1f6d0f37c778ace50db84ce2614b554d`; its resolver, CLI and configuration
header matched the release byte-for-byte after line-ending normalization. This
is not qualification of main or a promise about future releases.

| Surface | Observed behavior in this pin | Security classification |
|---|---|---|
| Parsing | Upstream AST construction; ordinary runtime bodies are not evaluated | Native data processing, still subject to parser/resource defects |
| Ordinary typechecking | Solver, import resolution, type graph/lint work; does not run an ordinary `error(...)` statement | Native computation; may trigger the following executable stages |
| `.config.luau` | Resolver recursively visits ancestors, compiles source, resumes a sandboxed Luau VM and extracts a returned table | Workspace-provided executable configuration |
| `.luaurc` / `.robloxrc` | JSON/config parsing and alias configuration, not Luau evaluation | Data with authority-bearing paths/analysis choices |
| User type functions | Compiler builds type-function code; solver reduction calls it using serialized type values | Workspace-provided executable analysis in a dedicated Luau environment |
| Source-transform plugins | Configured plugin paths are read, compiled and run; `transformSource` is called on analyzed text | Executable code; only the pack-owned adapter is allowed |
| Bytecode/assembly inspection | Source compilation and bytecode loading/codegen inspection; inspected path does not resume ordinary script bodies | Extra native compiler surface, unnecessary and not exposed by this policy |
| Native filesystem | Server reads modules, ancestors/configs, definitions/docs and plugin paths | Ordinary process file access; not restricted by `plugins.fileSystem.enabled` |
| Native network/process | Optional Sentry crash reporting; POSIX local socket transport; native process retains normal OS capabilities | Disable optional integrations; process separation is not capability confinement |
| Upstream VS Code extension | Includes downloads/flag synchronization, Rojo/custom generator process launch and Roblox companion HTTP behavior | Not imported: CarbonLuau uses its own client and only the pinned server binary |

Source owners: [resolver][resolver], [CLI and optional crash reporting][main],
[plugin runtime][plugin], [plugin host API][plugin-api], [client settings][settings],
[bytecode operation][bytecode], [upstream editor integration][editor]. Inspection
found no general shell/process/network API exposed to the three Luau VMs. That
is an API-surface finding, not a proof against native vulnerabilities or all
possible server behavior.

## 3. Executable configuration

`readConfigRec` walks parents before reading the current directory. Providing
`--base-luaurc` supplies defaults; it does **not** suppress this walk or executable
configuration. A local `.luaurc` also does not prevent a colocated `.config.luau`
from running. Workspace root/cwd is not a configuration boundary. This matters
for home-directory configurations, nested projects and materialized snapshots.

The embedded [configuration evaluator][config-vm] uses `luaL_newstate`, safe
standard libraries and `luaL_sandbox`. It receives no added require, file,
process or network binding. The Windows/Linux probes observe nil for `io`,
`require`, `os.execute`, `os.getenv`, `loadfile`, and `package`. Its returned
aliases can still influence subsequent **native** file resolution. Default
allocation is not a configuration heap quota. Interrupts consult supplied
deadline/cancellation state, which is not an independent hard process deadline.
An infinite configuration required the test harness to kill the CLI at two seconds.

Thus this is executable sandboxed Luau with denial-of-service and native-sandbox
risk, not demonstrated arbitrary shell execution. CarbonLuau excludes it even
after trust because its own API configuration does not need executable input.

## 4. Type functions

The new solver evaluates user-defined type functions when reducing applicable
type-function applications, including types encountered through dependencies
and editor analysis. Declaration registration also compiles/loads the generated
function definitions. Do not assume only a manual diagnostics command reaches
evaluation, or that nonstrict mode reliably prevents it.

The [runtime environment][type-vm] exposes math, table, string, bit32, utf8,
buffer, restricted base functions and the `types` API. It does not expose the
ordinary program's runtime objects, file IO, require, OS, process or network
bindings. Globals/library tables are sandboxed; individual calls use private
environments. Behavior of pcall/xpcall and structured errors depends on pinned
flags. The [call path][type-call] installs interrupt checks for frontend deadline
and cancellation. The LSP's new-solver check paths do not supply a universal
hard module deadline. Windows/Linux infinite-call probes required external kill.

`DebugLuauTypeFunctionRuntimeHeapLimit` is an integer byte limit and defaults to
**0 (off)** in this pin. An explicit 1 MiB limit rejected a 2 MiB buffer fixture
with `not enough memory` on both platforms. This limit covers VM allocations,
not all native type arenas, parser/compiler buffers or the process. The approved
analysis profile must set it to **67108864 (64 MiB)** and qualify that exact flag
on every shipped binary. Treat it as a pinned implementation control, not a
stable upstream security API. Whole-process supervision remains mandatory.

Ordinary typechecking and type-function evaluation cannot be separated by
pretending the latter is passive. Nevertheless, type functions are not equivalent
to arbitrary native programs: their exposed capabilities are substantially narrower.

## 5. Option A: reliable execution-disable mode

**Not available in the inspected released pin.** No supported complete control
was found in CLI options, client settings, resolver or type-function call path.
`plugins.enabled=false` disables plugins only. `plugins.fileSystem.enabled=false`
disables plugin filesystem helpers only. `--base-luaurc` does not disable config
evaluation. `--no-flags-enabled` controls feature defaults, not code admission.
Disabling `LuauSolverV2` makes the tested type-function syntax unsupported and
breaks the intended new-solver/generated-definition pairing; configuration still
executes. It is not the CarbonLuau non-execution profile. The existing generated
readonly/extern/operator definitions and Create/GetService corpus must be
qualified with the chosen new solver, not silently downgraded.

Unknown flags may only produce a warning. Qualification must verify effective
controls and negative fixtures, not infer success from process exit or an accepted
argument. A future supported no-evaluation upstream mode would require a new
policy/pack qualification; its possible existence is not a dependency of this design.

## 6. Option B and CarbonLuau configuration

**Use input admission for configuration; do not use source sanitization as a
general non-execution boundary.** Tooling owns a data-only JSON `.luaurc`, server
settings, generated definitions/docs and their exact digests. No workspace
configuration is merged into launch arguments, settings responses, flags,
definition paths or plugin paths, even when trusted.

The qualification launch profile is `luau-lsp lsp --stdio` with fixed
`--flag:LuauSolverV2=true`,
`--flag:DebugLuauTypeFunctionRuntimeHeapLimit=67108864`,
`--base-luaurc=<owned-json>`, `--settings=<owned-server-json>`,
`--definitions:@carbonluau=<pack-definition>` and `--docs=<pack-docs>` arguments.
Required owned settings are `platform.type=standard`, `sourcemap.enabled=false`,
`index.enabled=false`, `diagnostics.workspace=false`,
`diagnostics.includeDependents=false`, `completion.imports.enabled=false`,
`require.useOriginalRequireByStringSemantics=true`, `plugins.enabled=true`,
`plugins.paths=[<verified-adapter>]`, `plugins.timeoutMs=1000`,
`plugins.fileSystem.enabled=false`, `fflags.enableNewSolver=true`,
`fflags.sync=false`, and pinned
`fflags.override` values consistent with the CLI. Disable Roblox types; supply
only pack-owned definitions/docs. The supervisor owns every configuration reply
and change notification, including initialization-option flags. Pin the release's
remaining effective feature flags;
do not accept workspace overrides or silently adopt upstream flag defaults on
upgrade. These settings enable the **trusted** profile; they are not a claimed
upstream non-execution mode.

Snapshot admission excludes every workspace `.config.luau`, `.luaurc`,
`.robloxrc`, `.vscode` configuration, binary/bytecode and plugin configuration.
If `.config.luau` is itself opened, it can receive safe syntax diagnostics but is
not sent to luau-lsp as an analysis document. Recognize names according to the
actual platform's case/path rules and reject symlink/reparse escapes.

The resolver's ancestor behavior requires an additional launch gate: enumerate
the snapshot directory's entire resolved ancestor chain to the filesystem root,
reject unexpected Luau config files, and watch/recheck that chain before admitting
new analysis. Stop/reap analysis if an unexpected config appears. Only generated
snapshot JSON config is admitted. Never delete or edit a user's ancestor config;
report the local environment conflict and leave static tooling available.
Private task directories and non-symlink ancestry protect against workspace data
placing these files. A hostile concurrent same-user process mutating arbitrary
private/ancestor directories is outside this trust boundary; a scan is not a
race-free OS filesystem sandbox.

Do not strip type functions, rewrite their bodies or replace their results.
Source hashes, positions and normal type errors must remain meaningful. A
sanitized config alone cannot make arbitrary source non-executing. This is why
Restricted Mode never starts the new-solver LSP, even over a sanitized snapshot.

Ordinary Luau syntax and type functions remain interoperable in trusted mode.
Workspace alias/configuration customization is deliberately not honored by this
CarbonLuau analysis session. Explain ignored configuration once in status/Output;
canonical package imports remain authoritative. This task does not create a
generic Luau configuration interpreter or change runtime require semantics.

## 7. Isolation and the five distinct guarantees

| Boundary | Selected guarantee | Explicit limit |
|---|---|---|
| Extension host | No workspace Lua VM, config/module import, eval or typechecker execution inside it | Bounded editor/proxy data still needs validation; a native exploit is not proven contained |
| Tooling process | Static coordinator and language process are separate; supervisor can kill/reap LSP independently | Same-user child processes normally retain file/process/network authority |
| Luau VM | Pinned limited libraries and type heap control; only trusted pack transform host bindings | Native implementation and compiler bugs remain; VM heap is not process memory |
| OS sandbox | No portable filesystem/network sandbox claim in this profile | Jobs, rlimits, process groups and snapshots are not such a sandbox |
| Workspace Trust | Required before any workspace content enters executable analysis; trust checked in code | Consent/policy, not an OS restriction or proof the content is harmless |

This satisfies the required property by withholding executable analysis from
untrusted workspaces, preserving VM capability restrictions in trusted analysis,
and supervising the executable component. A user simply opening an untrusted
folder never grants executable-analysis authority. Already trusted folders follow
VS Code's existing trust decision. We do not silently mark folders trusted.
The [VS Code trust guide][trust] explicitly discusses language extensions executing
workspace code and supports limited functionality plus runtime trust checks.

## 8. Exact Restricted Mode behavior

Keep `untrustedWorkspaces.supported = limited`. Offer packaged syntax grammar,
parse-only syntax diagnostics through the Ast-only host, canonical API reference/
documentation lookup, manifest/package/path checks, project discovery and static
dependency diagnostics. Do not promise type-inferred hover/completion/signature
help here; metadata lookup is not arbitrary-expression inference.

No LSP process, transform VM, config VM or type-function evaluation; no external
development folder mappings, downloads, tasks or preview. Workspace-controlled
tool paths and settings cannot bypass this. A failed trust check leaves the safe
host active. Calling a hidden command must not start analysis.

## 9. Exact trusted behavior and trust transitions

After `workspace.isTrusted` and pack/platform admission pass, normal language
features start automatically over the admitted snapshot: type diagnostics,
hover/completion/signatures and navigation, including evaluated type functions.
No extra repeated consent dialog is required. Workspace executable config and
arbitrary plugins remain excluded. Trust does not authorize preview or publication
under this task, disable quotas, or select executable paths.

On `onDidGrantWorkspaceTrust`, invalidate old revisions, capture fresh input and
start a fresh supervised session. Check trust again immediately before launch
and every new snapshot/request dispatch. VS Code exposes a grant event, not a
general symmetric revocation event; do not invent one. On a trust-changing
reload/extension shutdown, terminate the supervisor/session; on activation,
start from current trust state. Parent-EOF/death cleanup must work even if
deactivation does not. Multi-root additions or removed folders invalidate trust
epoch and admitted URIs; stop when trust is false. Test the actual supported
VS Code reload/revocation behavior, not only a mocked boolean.

## 10. Selected process model

```text
VS Code extension (bounded data client; checks Workspace Trust)
  -> carbonluau-tooling supervisor/proxy (no workspace VM)
       -> pinned luau-lsp process (trusted-workspace analysis only)
            -> private, admitted workspace snapshot + pack-owned artifacts
```

One supervised, reusable but disposable LSP session per workspace/trust epoch,
with at most one analysis process per extension workspace. Keep it while healthy
for interactive latency; discard it on timeout, protocol violation, trust/pack
change or crash. This is a tooling-pack managed child behind a bounded proxy,
not a direct unrestricted `LanguageClient` child and not a preview worker.

Use absolute verified executable paths, stdio only, no shell, hidden Windows
startup/error UI, sanitized environment and private cwd. Remove inherited
credential/proxy/dynamic-loader injection variables; allow only required runtime
environment values. The parent sends no server assemblies, user executable or
native module path. No Rojo/custom generator, companion server, Sentry flag,
flag sync, telemetry or editing-time downloads.

The proxy has an allowlist of required LSP methods and capabilities. Reject
execute-command, arbitrary server-triggered process/file access, unscoped edits,
dynamic features outside the profile and command-enabled Markdown. Validate
bounded ranges, URIs and edits against current snapshot identities before
mapping to editor files; never treat LSP output as an instruction to open arbitrary
paths/URLs or execute a command. Keep normal user-initiated code actions only
after equivalent scoped edit validation. Protocol errors fail the session closed.

## 11. Filesystem and snapshot model

The canonical host admits immutable logical folders/files and dependency maps,
including unsaved buffers. The supervisor materializes only bounded source and
generated configuration under a private session directory outside the workspace.
No symlinks, reparse points, hardlinks to user source, environment expansion,
absolute/UNC/device paths or archive extraction supplied by workspace data.
Use opaque folder IDs and bidirectional URI maps; editor root URIs, filenames,
watch notifications and LSP file requests are rewritten/validated as data.
Snapshots include only admitted workspace roots; external mappings stay deferred.

The LSP sees snapshot paths and packaged definitions/docs, not user workspace
root paths. Canonical invalid import targets must not fall through to arbitrary
upstream absolute/parent/alias resolution. The adapter must give them a bounded
unresolvable snapshot target plus the canonical diagnostic, or withhold affected
language analysis explicitly. Unknown/dynamic require forms must not gain new
resolution privileges. Ordinary source and type-function bodies stay intact.

Snapshot publication is atomic per revision; don't rewrite files underneath an
in-flight check. Coalesce pending edits, retire old requests, and restart when
a consistent session update cannot be established. Cleanup only known session
directories after reaping. Results from old snapshots/trust epochs are discarded.

This is a bounded **input/access policy**, not OS confinement: a compromised
native server can still attempt to read outside the snapshot or create sockets.
The same-user filesystem and runtime libraries remain accessible to its OS token.
No network APIs are exposed by the selected Luau profile, and optional native
network features are disabled, but network denial at the OS level is not claimed.

## 12. Deadline, memory and recovery policy

These are language-analysis limits, independent of the preview defaults:

- 30 seconds for process initialization; 15 seconds for each admitted analysis
  batch/request, measured by the supervisor's monotonic clock. New edits/logs/
  progress messages never extend the oldest deadline. Pull diagnostics or an
  explicit bounded completion barrier must cover document-triggered work too;
  background indexing/dependent scans are disabled. No unbounded notification work.
- Cancellation gets at most 250 ms, then process/tree kill and reap. Deadlines
  cover parsing/config/VM/native checking; cooperative interrupts are supplemental.
  A responsive heartbeat alone does not prove other analysis threads are healthy.
- Set the pinned type-function heap flag to 64 MiB and plugin timeout to 1000 ms;
  plugin VM's pin has a 64 MiB allocator. Bound source graph to the canonical
  static-host admission envelope (currently 32 folders, 2048 files, 6 MiB source),
  with a separate 32 MiB ceiling for generated transform data.
- Proxy framing: at most 8 MiB body, 4096-byte header, depth 32; at most 16
  outstanding editor requests and one queued replacement snapshot. Cancel or
  reject excess work; don't buffer it indefinitely. Bound stderr/operational
  output to 64 KiB per session and diagnostic publication to 256 entries.
- Windows: launch suspended, assign a non-breakaway kill-on-close Job before
  resume; 1 GiB process commit limit and active-process limit of one for the LSP.
  Failure to install required controls leaves static mode available.
- Linux: native launch shim sets hard/soft `RLIMIT_AS` to 2 GiB before exec,
  creates an owned process group and ensures parent-loss termination. Use a
  1 GiB RSS kill threshold as an additional monitor. Address-space and RSS are
  different quantities; this does not promise a 1 GiB hard resident-memory cap.
- macOS: supervised process group and a 1 GiB resident-memory kill threshold,
  sampled at most every 100 ms. This is a **soft threshold with overshoot**, not
  a hard resident-memory ceiling. Do not claim `RLIMIT_AS` supplies a tested macOS
  hard cap. The 64 MiB type VM cap and hard wall deadline still apply. If memory
  monitoring/termination cannot be qualified, ship static-only on that target.
- No automatic replay after an input-driven timeout, memory limit or protocol
  failure. Keep static diagnostics and offer Restart Tooling. One automatic
  restart after an unexpected crash per five minutes; a second failure latches
  static-only until explicit restart or workspace reload. A changed snapshot may
  be retried only through the bounded recovery policy, never a tight crash loop.

These are initial versioned policy values to qualify, not benchmarks already
established. Changing them requires a new policy/pack revision and affected tests;
workspace settings cannot relax them. macOS's soft process-memory monitoring is
an explicitly accepted limitation for trusted analysis, not a portable hard cap.

## 13. Transform trust policy

Only a pack-owned, reviewed and digest-pinned transform template may execute,
inside the supervised LSP's plugin VM. Generated mapping/source strings are
untrusted **data**, encoded without evaluation/interpolation injection. Preserve
the partial implementation's exact-source/hash guard and full-byte escaping.
Workspace files cannot select, replace or append plugins, flags or definitions;
plugin filesystem access remains false. Store generated adapter input outside
the workspace; fail if identity/integrity checks fail.

Keep canonical resolution and map generation in Core/tooling. The thin plugin
remains in LSP for upstream source mapping; moving all transformations into the
host would add another mapping implementation without removing type-function
execution. Therefore do not move it solely to claim non-execution. The template
is trusted code; the source it transforms is not.

## 14. CarbonLuau require-analysis impact

Keep canonical local/module/package visibility and exact declared sibling-ID
mapping. `require("local/module")`, `@addon` and public exports map to admitted
snapshot targets. Preserve original require-by-string semantics and reject
ambiguous/private/undeclared/escaping targets through canonical diagnostics.
Add snapshot URI/source-map tests for all LSP features, UTF-8/UTF-16 positions,
Windows casing and init-module behavior. Do not let workspace config aliases
override canonical maps. The compiler/runtime/module loader are unchanged;
LSP acceptance is never evidence of runtime compiler acceptance.

## 15. Pack and version policy

Add `AnalysisSecurityPolicyVersion = 1`, an explicit `AnalysisProfile` of
`TrustedSnapshotAnalysis`, and exact per-platform `AnalysisContainmentProfile`
identity, including whether process memory is hard-limited or monitored. Pin
source commit, binary digest, embedded Luau commit, flags/effective controls,
config digest, transform revision, protocol/proxy revision and qualification
evidence. Unknown/missing policy identities fail closed for language features;
they must not disable independently compatible static validation.

Candidate 1.70.0 can be qualified against this amended boundary, not labeled
non-executing. Its historical `RejectedNonExecutionGate` result remains true
under the old rule. Do not edit the existing development manifest to claim PASS
now. No runtime API/ABI/provider/package identity changes are needed.

## 16. Disposition of preserved Foundation A work

| Work | Decision |
|---|---|
| Core extraction, canonical package/path/native policy sharing | Keep; reconcile current main and complete original runtime/deployment gates |
| Catalog, definitions/docs, drift/determinism checks | Keep; refresh GiveItem from current main and retain new-solver corpus |
| Static host, Ast-only parser, bounded snapshots/protocol | Keep; separate static capability from executable-analysis policy |
| Extension diagnostics, selection commands, coalescing | Keep; split mode/status and qualify real extension-host behavior |
| `Language.ts` direct LSP launch | Replace with supervised proxy/session; retain useful fixed settings/arguments |
| `Pack.ts` unconditional startup block | Keep until new profile is implemented and qualified; never bypass with one boolean |
| Trust assumptions in activation | Amend: current `isTrusted` text is insufficient; gate all executable admission and transitions |
| Require adapter | Retain canonical map and escape/hash guards; amend for snapshot URIs, invalid-target handling and full position mapping |
| Pack provenance/platform declarations | Amend to carry policy/containment identities and real qualifications |
| Universal "language analysis is non-executing" premise | Discard; no useful Core/runtime implementation needs to be discarded |

Inspected partial runtime worktree: `CarbonLuau-tooling-a`, branch
`codex/tooling-foundation-a`, HEAD `03b6d68346e01747e3b9250f1f4c4668ae3d8383`,
dirty uncommitted implementation and `ToolingFoundationAValidation.md` evidence.
Extension: `carbonluau-vscode-a`, same branch name, HEAD
`85236a0b66b680bda46d94f0a3ed038253a193a7`, dirty uncommitted implementation.
Neither is resumed/committed by this architecture task. This decision branch
starts from runtime main `9b27ba5625c86eb32d9168effa2c6e72d8866576` and reconciles
baseline `33f9c75` while preserving implemented GiveItem.

## 17. Exact canonical amendment

D19 and the baseline now distinguish `StaticInspection`, `ExecutableAnalysis`
and later `PreviewExecution`. StaticInspection may run in Restricted Mode using
the Ast-only host. ExecutableAnalysis is trusted-workspace-only under this
supervised snapshot policy; it may evaluate type functions and the pack-owned
transform, never workspace configuration/plugins or ordinary runtime scripts.
PreviewExecution keeps its existing fresh-worker, trust and containment gates.
None may execute workspace-controlled code in extension host, WebView or static
coordinator. Workspace Trust never selects tool paths or relaxes worker limits.

This narrowly replaces the blanket Foundation A prohibition on all Luau
evaluation; it does not weaken Restricted Mode or silently introduce preview.
This file owns the exact executable-analysis policy under D19. Earlier reports
remain historical evidence of the old stop condition.

## 18. Implementation handoff

Resume Foundation A only in its isolated worktrees. First reconcile latest main
and this documentation branch, preserving runtime/GiveItem behavior. Implement
snapshot materialization/admission and ancestry gate; supervisor/proxy with
platform resource/reaping controls; pack policy negotiation; trust state machine;
scoped LSP response mapping; snapshot-aware require adapter and UX. Retain the
startup block until the selected platform's gates pass. Complete original A
generation/runtime/package/source-deployment/LSP/E2E gates as well as section 20.
No Foundation B feature is needed or authorized by this decision.

## 19. Platform containment assessment (option C)

Full native containment is possible only with explicit platform work:

| Platform | Resource/lifetime control chosen | Stronger OS sandbox evaluated, not selected/claimed |
|---|---|---|
| Windows x64 | Job Object commit/child/lifetime control + deadlines | AppContainer/LPAC with explicit file capabilities and denied network could restrict authority, but requires a native launch/ACL/capability design and qualification |
| Linux x64 | rlimit/native shim/process group + RSS monitoring/deadlines | Namespaces, seccomp and Landlock can constrain access; kernel/LSM/user-namespace availability and cgroup delegation vary. Landlock is not a universal all-network/process filter |
| macOS x64/arm64 | Process group, deadline, RSS monitoring; signed/quarantine-qualified artifacts | App Sandbox needs a correctly signed/entitled helper/broker distribution. A generic Node/.NET spawn does not acquire it; deprecated sandbox-exec is not the product's portability strategy |

Sources: [Windows Jobs][jobs] explicitly separate security limits from job
limits; [AppContainer][appcontainer] addresses resource isolation; Linux
[Landlock][landlock], [seccomp][seccomp] and [cgroup v2][cgroups] have distinct
roles; Apple's [App Sandbox][apple] and [sandbox-exec discussion][apple-forum]
do not provide a portable extension API. No admin-installed container, VM or
host policy change is required for the selected trusted-only profile. An OS
sandbox would be defense in depth, not a reason to enable execution in
Restricted Mode without a separately amended and qualified policy.

## 20. Security qualification matrix

`Observed` below is upstream evidence, **not** an implemented supervisor PASS.
All amended extension/supervisor tests are pending. Tests must be headless and
use task-owned adversarial fixtures with independent harness limits.

| Gate | Required outcome | Windows x64 | Linux x64 | macOS x64/arm64 |
|---|---|---|---|---|
| Ordinary runtime body | `error` body never runs during checking | Observed | Observed | Not run |
| Config with owned base config | Demonstrate base does not disable execution | Observed | Observed | Not run |
| Config/type VM capabilities | No file/process/require/network host API injection | Selected globals observed + source audit | Same | Source audit only |
| Infinite config/type function | External timeout required; no reliance on cooperative default | Both killed by harness at 2 s | Same | Not run |
| Type heap flag | 1 MiB test cap rejects 2 MiB allocation | Observed | Observed | Not run |
| Final 64 MiB/type arena/process stress | VM errors or supervised bounded kill; editor responsive | Pending | Pending | Pending; measure overshoot |
| Malicious/nested/ancestor config | Workspace configs never materialized/evaluated; unexpected snapshot ancestor blocks/stops LSP | Pending | Pending | Pending |
| Configuration replacement/re-enable | Workspace settings/flags/base path/definition path cannot override pack profile | Pending | Pending | Pending |
| Arbitrary plugin/transform replacement | No workspace plugin execution; reject tampered pack/template/mapping; injection corpus | Pending | Pending | Pending |
| Source/path/protocol bounds | Reject malformed/oversized input before unbounded allocation; no traversal/UNC/symlink fallback | Pending | Pending | Pending |
| LSP output attacks | Bound frame/log/diagnostics; reject out-of-snapshot URI, command links, unsolicited edits | Pending | Pending | Pending |
| Crash/hang/restart | Deadline/tree reap, one-restart circuit breaker, stale result rejection; no modal UI | Pending | Pending | Pending |
| Incompatible pack/control failure | Static-only, no failed-policy language launch | Pending | Pending | Pending |
| Untrusted activation/commands | Zero LSP/config/type/plugin execution, useful safe features remain | Pending real E2E | Pending real E2E | Pending real E2E |
| Grant/revoke/reload/multi-root | Fresh trusted session; no orphan/replay after revoked trust or parent death | Pending | Pending | Pending |
| Responsiveness | Inject CPU loop/blocked LSP; extension commands/typing remain responsive during watchdog interval | Pending | Pending | Pending |
| Network/process integration | No Sentry/companion/download/Rojo/subprocess activity in qualified profile; do not infer OS denial | Pending | Pending | Pending |
| Feature fidelity | Definitions, overloads, type functions, requires and every URI/range map preserve results | Pending amended profile | Pending amended profile | Pending |

The eight architecture probes used exact upstream Windows/Linux assets, recorded
in [evidence](ToolingLanguageAnalysisSecurityEvidence.md). Linux probes ran inside
a task test container with network disabled, 1 CPU and 512 MiB; those harness
controls are **not** the shipped extension's sandbox. No macOS host was available
for this investigation. Shipping each target requires its pending gates; static
functionality can be qualified and shipped separately from language analysis.

## 21. UX

Restricted status: **"CarbonLuau: static checks — trust this workspace for Luau
type analysis."** Explanation in Output/settings: "Luau type analysis can run
type functions in a separate supervised process. Workspace Luau configuration
and custom plugins are not used." Link to VS Code's normal Manage Workspace
Trust UI; don't create a separate trust override or automatic trust prompt.

Trusted status shows the selected API and language-analysis readiness. Explain
ignored executable configuration once per workspace/session, not per file or
validation. A timeout/crash yields one bounded status/Output message and a
Restart Tooling action; retain static diagnostics. Distinguish incomplete analysis
from clean results. No repeated toast, modal error or focus stealing.

## 22. Remaining questions

No upstream answer is needed to choose or implement this architecture. Remaining
work is qualification, not an unresolved choice among A-D. The exact binary's
experimental heap/transform behavior and macOS monitor/signing/quarantine behavior
must be tested before that platform's language profile ships. A future upstream
supported no-evaluation mode or maintained cross-platform sandbox could justify
a new policy revision; neither is assumed or required now.

## 23. Verdict

**READY TO RESUME TOOLING FOUNDATION A.** The architecture contradiction is
resolved by explicit trust classes and the selected supervised snapshot policy.
Foundation A itself remains partial and unqualified. Keep language startup
disabled until this policy is implemented and its target-platform gates pass.

[resolver]: https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/WorkspaceFileResolver.cpp
[main]: https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/main.cpp
[plugin]: https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/Plugin/PluginRuntime.cpp
[plugin-api]: https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/Plugin/PluginLuaApi.cpp
[settings]: https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/include/LSP/ClientConfiguration.hpp
[bytecode]: https://github.com/JohnnyMorganz/luau-lsp/blob/876d84b8caf4454c8df806ddc9eee0565228d7b6/src/operations/Bytecode.cpp
[editor]: https://github.com/JohnnyMorganz/luau-lsp/tree/876d84b8caf4454c8df806ddc9eee0565228d7b6/editors/code/src
[config-vm]: https://github.com/luau-lang/luau/blob/a62362a53ddc9c629b0e29378a84abb4534d8b64/Config/src/LuauConfig.cpp
[type-vm]: https://github.com/luau-lang/luau/blob/a62362a53ddc9c629b0e29378a84abb4534d8b64/Analysis/src/TypeFunctionRuntime.cpp
[type-call]: https://github.com/luau-lang/luau/blob/a62362a53ddc9c629b0e29378a84abb4534d8b64/Analysis/src/UserDefinedTypeFunction.cpp
[trust]: https://code.visualstudio.com/api/extension-guides/workspace-trust
[jobs]: https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects
[appcontainer]: https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation
[landlock]: https://docs.kernel.org/userspace-api/landlock.html
[seccomp]: https://docs.kernel.org/userspace-api/seccomp_filter.html
[cgroups]: https://docs.kernel.org/admin-guide/cgroup-v2.html
[apple]: https://developer.apple.com/documentation/security/protecting-user-data-with-app-sandbox
[apple-forum]: https://developer.apple.com/forums/thread/661939
