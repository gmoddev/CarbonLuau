# Language-analysis architecture investigation evidence

2026-09-21; supports the [D19 decision](ToolingLanguageAnalysisSecurity.md).
This is source inspection and controlled upstream probing. It does not implement
or qualify the future CarbonLuau supervisor, trust state machine or snapshot gate.

## Inputs and source inspection

- Canonical isolated baseline: `33f9c75c62759cc093e06477b25b2fe05f0f03e5`.
- Read the uncommitted `docs/ToolingFoundationAValidation.md` in the preserved
  `CarbonLuau-tooling-a` worktree. Its old-rule BLOCKED result is retained there;
  it is not rewritten to claim the amendment is implemented.
- Inspected partial extension `Language.ts`, `Pack.ts`, `Extension.ts`,
  `ToolingClient.ts`, snapshots/adapter and tests in `carbonluau-vscode-a`.
- Architecture branch begins at current runtime main
  `9b27ba5625c86eb32d9168effa2c6e72d8866576` and applies the baseline as a
  prerequisite. Its single decision-table conflict was resolved by retaining
  main's D18 GiveItem InventoryOnly status and adding D19. No runtime source
  changes or partial implementation were copied into this branch.
- LSP release 1.70.0 resolves to
  `876d84b8caf4454c8df806ddc9eee0565228d7b6`; embedded Luau
  `a62362a53ddc9c629b0e29378a84abb4534d8b64`. Official source tarballs inspected
  locally, including Config/Analysis VM entry points, interrupt and allocator
  paths, plugin API/runtime, native IO/transport/crash reporting, and the separate
  upstream editor's process/network integrations. Stable source links are in
  the decision. This is a bounded architecture audit, not an exhaustive native
  memory-safety audit.
- GitHub latest-release API returned 1.70.0 (2026-09-20T14:13:43Z).
  Current main `fcf6d84e1f6d0f37c778ace50db84ce2614b554d` matched pinned
  `WorkspaceFileResolver.cpp`, `main.cpp` and `ClientConfiguration.hpp` after
  line-ending normalization. No upstream version was changed.

| Official release archive | Verified SHA-256 |
|---|---|
| luau-lsp-win64.zip | `26e6d32069cb5dd74f06bbdc2f3033d3ba66da76d3dbdfc3dcc2b085ddaa274a` |
| luau-lsp-linux-x86_64.zip | `4ff08890ea0d4b6d9de25fdff1a4c87e0dc9f2e45d782c894e83475e51d55813` |

## Reproduction

Run [ProbeAnalysisBoundary.py](evidence/ProbeAnalysisBoundary.py) with the absolute
path to the corresponding extracted official `luau-lsp` executable. Python uses
task-owned temporary fixtures, captured output, headless Windows spawn/error mode
and a two-second subprocess timeout. Timeout kills and waits for the child.
No user repository scripts/configuration are executed. Memory probing requests
only a 2 MiB buffer under an explicit 1 MiB type-VM cap; there is no uncontrolled
memory-exhaustion stress on the developer machine.

The source-identified paths are shared between CLI and language-server analysis.
These **CLI** probes establish behavior/capability examples, not complete LSP
request/cancellation or VS Code lifecycle behavior. Final qualification must
exercise the actual proxied LSP and real extension host.

| Probe | Windows and Linux observation |
|---|---|
| Ordinary body `error(...)` | Exit 0, sentinel not executed |
| `.config.luau` error with owned `--base-luaurc` | Exit 1, config sentinel executed |
| Config capabilities | `io`, `require`, `os.execute`, `os.getenv`, `loadfile`, `package` all nil |
| Infinite config | Harness killed after 2 s |
| Strict type-function capability probe | Runtime sentinel: `io`, `require`, `os`, `loadfile`, `package` all nil; ordinary unknown-global diagnostics also emitted |
| Infinite type function | Harness killed after 2 s |
| Type memory cap | Exit 1, runtime `not enough memory` at 2 MiB allocation with 1 MiB cap |
| Old solver | Exit 1, unsupported type-function syntax/unknown type; not a useful full non-execution profile |

Raw normalized results: [Windows](evidence/AnalysisBoundaryWindows.json),
[Linux](evidence/AnalysisBoundaryLinux.json). Successful probes completed in
well under one second; loops were bounded by the harness. An earlier exploratory
nonstrict fixture hid some type errors, so final capability/memory fixtures use
`--!strict`; absence of a diagnostic is not evidence of non-execution.

Linux used the existing task image `codex-carbonluau-tooling-a:net10` on
`dockerbox`, in a disposable container with `--network none --cpus 1 --memory 512m`.
Transferred only the probe and checksum-verified release archive under
`C:\Sandbox\Codex\Artifacts\CarbonLuauToolingA-20260921`. Existing unrelated
containers were left running. No source build, host installation, global setting
or original checkout modification was needed. macOS execution was unavailable;
the decision's macOS controls remain implementation/qualification requirements.

## Decision validation and repository scope

Review the amendment against D19, baseline security/Trust/pack sections and
Foundation A routing. Required checks are documentation link/whitespace/conflict
checks, baseline contract fixtures and existing API/architecture checks. No
runtime regression rerun is needed for this documentation-only amendment.
Executed contract fixtures, Test-Api (including GiveItem structural checks) and
Test-Architecture all passed on the architecture worktree. Whitespace and
relative-document-link checks passed; no conflict markers remained.
Check the final change list against current main to ensure baseline documents/
schema fixtures and architecture/evidence are the only additions. The extension
receives a documentation routing update only, referencing the exact runtime
decision commit. No Foundation A executable changes are committed or pushed.

Architectural consistency means the decision can be implemented without claiming
portable OS isolation: untrusted execution is withheld, trusted type analysis is
explicit, configurations are excluded, and platform limitations are labeled.
The pending security matrix is deliberately not converted into passing evidence.
