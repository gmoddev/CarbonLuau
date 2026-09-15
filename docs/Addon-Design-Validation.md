# Addon design review — revised 2026-09-15

**Verdict: the AI review supports a substantially simpler shared-VM proposal.
Documentation validation only; not canonical adoption or implementation readiness.**

The [revised proposal](CarbonLuau_Addon_Decisions_and_Invariants.md) removes
requirements for addon-local cancellation, deep export revocation, per-closure
budget switching and rollback of shared Luau state. It retains concrete ownership,
provisional-effect and integration gates. No runtime, vendor, public API, release
identity or canonical I1–I11/D1–D13 changes were made.

## Provenance and scope

Reviewed the entire user-supplied AI review attachment
`0c6fb42d-2855-4111-be23-ec3d73f7d35a/pasted-text.txt` against checkout
`3b086f43db3eb8e700513c8836d794603b295c9b`.
Runtime/release baseline remains `3046390b5ee668f02c20bcde01d10138f5ca7489`:
package `0.3.0`, API `0.3.0-experimental`, native ABI `1.2`,
Luau `c6b830185af962c82003f86784e2fe036357c830`.

The [original imported proposal](https://github.com/gmoddev/CarbonLuau/blob/3b086f43db3eb8e700513c8836d794603b295c9b/docs/CarbonLuau_Addon_Decisions_and_Invariants.md)
and [original V1–V8 review](https://github.com/gmoddev/CarbonLuau/blob/3b086f43db3eb8e700513c8836d794603b295c9b/docs/Addon-Design-Validation.md)
are preserved in immutable history. This revision intentionally consolidates their
repeated sections and supersedes their D-A/I-A proposal labels, rather than
silently giving those labels new meanings. The original attachment is no longer
claimed to be textually unchanged. The AI review is evaluated source material,
not authority to implement its suggestions.

## AI recommendation disposition

| Recommendation | Disposition and reason |
|---|---|
| One VM, ordinary errors local, fatal timeout global | Adopt in proposal. Matches current VM retirement scope; does not promise isolation or safe addon-local cancellation. |
| Remove automatic per-addon-VM fallback | Adopt. Source copies/RPC would change canonical shared-state semantics. |
| Plain shared values, no export membrane | Adopt. New imports and host handles become stale; escaped plain tables/closures can survive. |
| Budget follows scheduled operation | Adopt. Entire synchronous call chain consumes original deadline; provenance and host lifetime remain separate. |
| Transactional publication, not shared state | Adopt. Explicitly document candidate mutations/cache effects that survive ordinary failure and global effects of candidate timeout. |
| Global heap cap; diagnostic attribution | Adopt. Healthy-VM allocation errors need not always retire the VM, but unrelated domains have no hard memory isolation. |
| Package-aware require aliases | Adopt as proposed syntax, not current support or full Luau filesystem-resolver parity. |
| Local ./ and ../ paths | Do not adopt. Existing D5/custom resolver prohibits them; package-root-relative logical paths remain adequate. |
| Tiny id/version-only manifest; public/ implies visibility | Modify. Keep schema, explicit exports and ID-only required/optional lists for defensive validation, preflight and stable privacy. Fixed entrypoint and optional declared main reduce metadata. |
| Runtime-discovered dependencies | Do not adopt as lifecycle authority. Conditional/caught imports do not reliably describe required startup/loss/retry behavior. |
| Defer version ranges | Adopt as a scope tradeoff. One active version still benefits from compatibility checks; removing ranges deliberately leaves version compatibility to operators. |
| Immutable snapshot primary; ZIP transport | Adopt. Single-file registration must normalize to the same model. |
| Keep provider ownership/fair scheduling/readiness gate | Retain. These require coordinated host implementation and real evidence, not merely one VM. |

This is a design recommendation, not a claim that every simplification is cost-free.
Ordinary shared values mean shared mutable state can be damaged by an error, old
code/data may remain reachable, and a fatal callback can interrupt every domain.

## Evidence and factual corrections

| Source inspected | What it establishes |
|---|---|
| [Runtime.cpp](../native/src/Runtime.cpp), Interrupt/Retire/resume catches | Private non-std deadline cancellation retires the complete VM. One VM's allocator tracks Used/Limit; the registry has 32 VM slots, not 32 addon slots. |
| [ldo.cpp](../native/third_party/luau/VM/src/ldo.cpp), luaD_rawrunprotected | Lua/std exception handling is distinct from the current private cancellation. An ordinary catchable Lua error is not a qualified substitute. |
| [Scripts.inl](../native/src/Scripts.inl), ModuleName/RequireModule | Canonical logical paths reject @ and dot-relative forms today. Loaded modules return their cached Lua reference; private sandbox threads and controlled cycle errors already exist. |
| [CarbonLuau.Scripts.cs](../src/CarbonLuau/CarbonLuau.Scripts.cs), Replace/Recover | Current replacement creates a separate RuntimeGeneration; current D9 recovery is bounded. Neither implements shared-VM domain replacement/reconstruction. |
| [CarbonLuau.Facade.cs](../src/CarbonLuau/CarbonLuau.Facade.cs), FacadeWorld.Active/Commit/SendMessage | Single-active-session guard is insufficient for provisional B calling active A's captured facade. Operation context and multi-domain coordination are still needed. |
| Pinned [lua.h](../native/third_party/luau/VM/include/lua.h), [lapi.cpp](../native/third_party/luau/VM/src/lapi.cpp), [lmem.cpp](../native/third_party/luau/VM/src/lmem.cpp) | Categories/accounting are not hard addon quotas. Allocator input has no addon identity; allocation instrumentation follows allocation/accounting. |
| [Phase5-Validation.md](Phase5-Validation.md) | 632,928 allocator bytes describes a controlled root workload, not addon/process memory or scaling qualification. |

The [Luau alias RFC](https://rfcs.luau.org/require-by-string-aliases.html) supports
`@name` and subpath syntax and separates aliases from versioning. It describes
case-insensitive aliases and filesystem configuration, not CarbonLuau's custom
resolver or its lowercase-only identity contract. Adopting syntax does not adopt
those different resolution semantics.

The official [Roblox ModuleScript source documentation](https://github.com/Roblox/creator-docs/blob/main/content/en-us/reference/engine/classes/ModuleScript.yaml)
supports cached identical return values within an environment. It explicitly says
cyclic imports hang rather than generate errors. The AI review's claim that Roblox
rejects recursive cycles with errors is incorrect. CarbonLuau should retain its own
controlled cycle errors and non-yielding module-load contract, not imitate that
behavior or claim full Roblox parity.

[Luau sandbox guidance](https://luau.org/sandbox/) distinguishes environment
separation from guaranteed VM isolation and describes cooperative interruption.
It does not prove addon-local cancellation or hard preemption of C#/native calls.
Trusted installation does not eliminate bounds, reentrancy hazards or host checks.

The comparison to package managers is design opinion, not evidence that explicit
dependency/export metadata is unnecessary. No Wally behavior is needed to justify
CarbonLuau's retained preflight and visibility requirements.

## Disposition of the original findings

| Finding | Current resolution or remaining gate |
|---|---|
| V1 — cross-addon cancellation | Remove local-cancellation requirement. Fatal cancellation retires the whole shared VM/call stack. Global reconstruction still needs G3 evidence. |
| V2 — export revocation | Remove deep revocation. Ordinary escaped values survive; new imports and host-backed lifetimes remain enforceable (G1). |
| V3 — closure ownership | Remove dynamic CPU-owner switching. One admitted call-chain budget; cross-domain resource admission/provisional context remains G2. |
| V4 — candidate transaction | Narrow to CarbonLuau publication. Shared table/cache mutations are explicitly not rolled back. Host-effect laundering still must be prevented (G2). |
| V5 — heap locality | Accept shared hard heap boundary and delayed reclamation. Aggregate limits and memory-failure/stress evidence remain G4/G5. |
| V6 — root replacement/fallback | Remove topology fallback and fatal root independence. Healthy root domain replacement differs from current D7; G3 requires canonical migration and qualification. |
| V7 — bindings/retries/protocol | Remove ranges; retain explicit lifetime-bound dependencies, bounded restoration and no implicit retry of Failed registrations. Exact protocol and public version negotiation remain G4. |
| V8 — phase ordering | Retain. No production addon activation before global scheduler/facade/lifecycle coordination and required qualification. |

The old “all V1–V8 must be solved while preserving every original guarantee”
interpretation is superseded. Some guarantees were deliberately removed from the
proposal; the remaining gates are not solved by calling the design simpler.

## Canonical adoption boundary

Before implementation, record approved changes in [Invariants.md](Invariants.md),
especially D5 import syntax, D7 candidate/domain preservation, D9 global recovery
and D10 operation-wide provisional enforcement; D12 governs version/migration.
Shared-VM generation-wide failure is compatible with I9's existing caution, but
does not automatically approve every addon lifecycle rule.

G2 still needs an explicit ownership decision for tasks/listeners/commands created
through foreign captured facades or lazy module initialization. G3 needs the
operator rearm action and safe root/domain migration. G4 needs exact provider
transport, compatibility negotiation and aggregate limits. These are genuine
remaining design/integration decisions, not a mandate for revocation membranes,
version solvers or a Luau cancellation fork.

## Validation scope

Documentation/policy validation selected through [Compatibility.md](Compatibility.md):
review source evidence, rule ownership, examples, links, balanced fences, JSON,
diff scope and `tools/Test-Api.ps1`. Checks passed: API version/reference/example
presence and repository relative links; changed-document relative links, balanced
code fences and manifest JSON parsing; `git diff --check`. Diff scope is exactly
the proposal, this review and AICONTEXT routing. Markdown uses proposed examples
only; no addon API is advertised as implemented in the public reference.

No runtime tests, server runs, cancellation experiments, quota experiments,
sanitizers, provider lifecycle experiments, CI, deployment, commit or push were
performed for this revision. Existing Phase 0–5 evidence remains valid for its
recorded unchanged implementation, not for addons.
