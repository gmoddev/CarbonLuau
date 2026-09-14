# CarbonLuau AI and contributor policy

This document owns contribution workflow and prompt construction. It applies to CarbonLuau only. Phase 0's accepted source baseline is `a88f2eb`; read the current checkout and [validation record](docs/Phase0-Validation.md) before relying on that baseline. Phase 1 is cleared to begin but remains unimplemented at this policy baseline.

## Authority and reading order

| Question | Canonical owner |
|---|---|
| How to scope work and construct prompts | This document |
| Product purpose, architecture, trust, ownership, ABI, threading, lifecycle and resource rules | [Invariants.md](docs/Invariants.md) |
| Provisional/open/deferred decisions and implementation gates | [Decision register in Invariants.md](docs/Invariants.md#decision-register) |
| Supported environments, API/version policy and required validation | [Compatibility.md](docs/Compatibility.md) |
| Phase scope, planned API examples and initial configuration candidates | [First-version design](docs/CarbonLuau_FirstVersion_Design.md), especially sections 3 and 31 |
| What Phase 0 actually proved | [Phase0-Validation.md](docs/Phase0-Validation.md) |
| Phase 0 build/deployment commands | [Phase0.md](docs/Phase0.md) |

Read this policy, Invariants and Compatibility before implementation; then read the current phase evidence and only the design sections and source needed for the task. The original design remains the accepted phase plan, not a claim that its future features exist. Runtime invariants apply when their owning feature is implemented. Do not implement a later feature simply to satisfy its future invariant now.

If documents, code, a task request or upstream evidence conflict, identify the conflict and its rule owner. Preserve accepted decisions while investigating. Stop the affected implementation if it would require silently weakening a rule or making an unsupported architectural decision; report the evidence and proposed resolution. Do not expand documentation to manufacture certainty. An explicit user-approved design change must also update its canonical document.

## Working rules

- Identify whether the request is investigation, design, implementation, review or validation. Stay within its modification authority and current phase; keep unrelated refactors out.
- Modify only CarbonLuau unless explicitly authorized otherwise. Do not edit Carbon, Rust, Gargantuan, or casually change vendored Luau. Read upstream sources to resolve assumptions; prefer documented/public adaptation APIs.
- Use PascalCase for project-owned identifiers by default. Preserve required OS/upstream names and accepted public API spellings; `self = setmetatable(...)` is the stated local exception. Use `GetFolder`/`GetObj`, not `GetOrCreateFolder`/`GetOrCreateObj`.
- Use `[System:SubSystem]` logging, e.g. `[CarbonLuau:Native]`, with concise relevant diagnostics. Prevent task processes from showing error dialogs or stealing focus; use headless/hidden server and helper launches.
- Preserve user changes and concurrent work. Use `apply_patch` and the available CodexLock workflow; do not bypass another task's claims.
- Run native builds and test servers on `dockerbox` through the authorized Windows profile connection. Keep worker host files under `C:\Sandbox\Codex`, bound resource use, reuse caches, and leave unrelated workloads alone. Use Linux containers for Linux tests. Do not silently move sustained builds or servers to the controlling PC.
- Keep credentials in their existing profile helpers, out of code/logs/artifacts. Deployment and destructive fixture tests have different scopes: production uploads never authorize replacing live libraries with failure fixtures or modifying host policy.
- Select validation through Compatibility.md. Preserve valid previous evidence; rerun affected checks after implementation changes. Report source revision, results, environment, remaining uncertainty and any persistent task processes.
- Add durable accepted rules to their canonical owner and link them from task notes. Do not duplicate them across prompts or create parallel policy documents.

## Prompt construction

A task prompt supplies the delta, not the architecture. Specify objective, task kind, permitted repository/paths, current revision/evidence, exclusions and stop conditions. Route to this policy and canonical phase criteria; use the smallest authoritative context. State prior evidence with its limits, prescribe order only where dependencies or test attribution require it, and do not optimize for a desired verdict. Unresolved choices belong in the decision register, not hidden prompt assumptions.

```text
Task: <phase and concrete outcome>; mode: <design/implementation/review/validation>.
Scope: CarbonLuau at <revision>; permitted changes: <paths/components>.
Read AICONTEXT.md and its canonical architecture, compatibility and phase references.
Established evidence: <link and applicable limits>.
Change: <task-specific delta>; exclude <later phases/unrelated work>.
Validate: <canonical fixture/criteria links plus only genuinely new requirements>.
Stop and report if <task-specific blocker> or an accepted invariant must change.
Report changes, final-source evidence, unresolved decisions and verdict.
```

For a Phase 1 prompt, route to the design's Phase 1 scope and the [Phase 1 evidence contract](docs/Compatibility.md#phase-1-evidence-contract). Do not request a complete scheduler, gameplay bindings, or the rest of v0.1 as part of the execution core.
