# Addon design review — 2026-09-15

**Verdict: suitable to retain as a research-gated proposal; not implementation-ready
or runtime-qualified.** The shared-VM preference is accurately labeled conditional.
Cancellation is necessary but is not the only unresolved architectural gate.

## Scope and authority

The user requested adding and validating the supplied
[decisions/invariants document](CarbonLuau_Addon_Decisions_and_Invariants.md).
The accompanying conversation is source material, not authority to implement its
Phase A–J plans, patch Luau, adopt every proposed rule, or reopen completed phases.
Its earlier isolated-VM/source-export design is superseded by the attachment's
shared-state preference. The two export models must not be combined implicitly.

The imported D-A/I-A labels identify proposed design requirements. They do not
replace [I1–I11 or D1–D13](Invariants.md). No canonical decision is silently changed
by this review. Promoting the proposal requires explicit decisions in that register,
including revisions to runtime ownership, cancellation, replacement and recovery.
The source document is preserved apart from a repository authority note and
conversion of its plain-text version table to Markdown. No source discussion is
presented as a completed experiment.

## Baseline facts checked

Inspected checkout: `3046390b5ee668f02c20bcde01d10138f5ca7489`, matching the attachment.
[release.json](../release.json) records package `0.3.0`, API
`0.3.0-experimental`, ABI `1.2`, and Luau
`c6b830185af962c82003f86784e2fe036357c830`. The repository calls this an experimental
release candidate; the design is not a new release or API version.

| Claim | Evidence and limit |
|---|---|
| Current timeout retires the VM | [Runtime.cpp](../native/src/Runtime.cpp): Interrupt throws non-std DeadlineExceeded; scheduled/thread resume catches it and calls Retire, which closes the complete lua_State. This does not prove a safe smaller unwind. |
| Protected-call behavior matters | Pinned [ldo.cpp](../native/third_party/luau/VM/src/ldo.cpp), luaD_rawrunprotected, catches lua_exception and std::exception. The project's private cancellation intentionally bypasses those catches. Ordinary Luau error handling is not a substitute for D1. |
| Multiple handles, 32 live slots | Runtime.cpp has an array of 32 VM registry entries. Increasing it is relevant to a fallback/scaling plan, not proof of addon isolation or a task authorized here. |
| Memory categories exist | Pinned [lua.h](../native/third_party/luau/VM/include/lua.h) and [lapi.cpp](../native/third_party/luau/VM/src/lapi.cpp) expose lua_setmemcat/lua_totalbytes. They label/query shared-heap accounting, not addon ownership or quota enforcement. |
| Allocation instrumentation is not an admission hook | [lmem.cpp](../native/third_party/luau/VM/src/lmem.cpp), luaM_new_/luaM_realloc_, allocates and updates accounting before onallocate. Small objects may use allocator pages. The allocator receives context/pointer/sizes, not a per-object addon ID. Rejecting from instrumentation is not a demonstrated safe quota mechanism. |
| Sandbox environments and cached module results exist | [Scripts.inl](../native/src/Scripts.inl) uses sandboxed module threads and caches a Lua reference per module in one Vm. No addon owner switching, export membrane or per-addon cache/lifetime exists yet. |
| Facade coordination is currently single-active | [CarbonLuau.Facade.cs](../src/CarbonLuau/CarbonLuau.Facade.cs), FacadeWorld.Active/Commit, maintains one active session. SendMessage checks that session's committed state. |
| Root replacement currently owns a new runtime | [CarbonLuau.Scripts.cs](../src/CarbonLuau/CarbonLuau.Scripts.cs), ScriptHost.Replace, constructs a candidate RuntimeGeneration and disposes the old generation after publication. A shared VM needs a different domain-level lifetime mechanism. |
| 632,928 allocator bytes is recorded evidence | [Phase5-Validation.md](Phase5-Validation.md) records that controlled-root reading. It is not an addon benchmark or process-memory baseline. Scaling it to 100 yields about 60.36 MiB of that measure only. Likewise 100 × 64 MiB is 6.25 GiB, not a measured addon envelope. |

Upstream [sandbox guidance](https://luau.org/sandbox/) supports separate environments
and cooperative interruption but does not certify CarbonLuau's proposed addon-local
uncatchable cancellation. Its [C API reference](https://luau.org/api/) documents
threads, memory and callbacks; pinned local code remains the build-specific evidence.
The Roblox analogy in the conversation is not an acceptance test for this host.

## Unresolved contracts found during validation

These are review findings, not newly accepted implementation policies. References
are to the attachment's proposal IDs. Resolve affected decisions canonically before
implementing the relevant surface; do not weaken existing contracts to mark PASS.

### V1 — Cancellation must cover the complete cross-addon stack

D-A03/R-A01 is correctly open. For B → A → B callbacks, specify which frames are
aborted, whether B's current callback can finish, how ordinary errors differ from
host cancellation, and which domains become unavailable. Killing A's registrations
does not by itself repair suspended B frames or shared values already mutated.
Fault tests must establish VM consistency, not only that the next callback runs.
No cancellation implementation or impossibility proof was produced here.

### V2 — Export revocation conflicts with unrestricted shared values

D-A18/19/45/46 need a complete export value/operation contract. Checking only
Dependency:Require or a top-level method does not invalidate a previously saved
nested table, returned closure, bound method, coroutine or callback. Define reads,
writes, iteration, equality, metatables, function results and callback arguments,
including identity preservation and cycle handling. Raw tables are not transparently
revocable. A proxy can preserve shared underlying state, but it is not automatically
indistinguishable from a normal module value. Do not promise both without proof.

### V3 — Execution ownership is not automatic closure attribution

D-A07/I-A16 need an enforceable ownership rule for every crossing, including B's
callback called by A, tail calls, metamethods, escaped closures and yields. A wrapper
around one entry method does not observe every call. Define caller versus executing
owner, stack restoration on error/cancellation, and cumulative call-chain budgets:
changing owner must not refresh a deadline indefinitely. Current code has VM-wide
deadline/context and generation-scoped host transport, not this machinery.

### V4 — Provisional code can mutate an active dependency before commit

D-A22/23 and I-A27/I-A50 do not yet establish a transaction for shared mutable
dependencies. A provisional B can call an active A's SetBalance, then fail its own
initialization; A's shared table has already changed. Even a first require could
initialize A's module and publish retained state. Staging B's commands does not
undo those effects. Choose an explicit initialization/import/effect policy before
claiming failed candidates preserve active dependency state.

Owner switching also creates a host-effect risk: provisional B → active A →
SendMessage must not lose B's provisional restriction merely because A is active.
The current single-session D10 check cannot simply be reused unchanged in that
call chain. Preserve execution owner, caller authorization and provisional-effect
context as distinct policy questions; no new effect permission is accepted here.

### V5 — Shared heap lifetime and resource-failure locality remain unproven

R-A02 correctly distinguishes attribution from refusal, but the design must also
define retained cross-domain objects, shared/interned allocations, reallocation,
GC work attribution, category reuse and aggregate exhaustion behavior. Removing
A's registry references need not reclaim values still reachable from optional B.
Specify deterministic logical teardown versus delayed heap reclamation, retention
bounds and what happens to unrelated domains at the global heap cap. A successful
cancellation experiment alone cannot establish memory-failure locality.

### V6 — Root replacement and fallback change ownership semantics

D-A24 must preserve root reload and failed-candidate behavior while addon domains
survive. Existing replacement owns/disposes an entire RuntimeGeneration; calling
that teardown on the proposed shared VM would not preserve unrelated addons.
Design candidate domain staging, root recovery and native-library teardown explicitly.

D-A04's topology-independent public API cannot mean topology-independent semantics
for live shared modules. The attachment's fallback warning correctly requires a
separate module-contract decision. Per-consumer source copies are not a transparent
fallback for shared authoritative state; neither RPC nor hidden copies is approved.

### V7 — Snapshot bindings, retries and protocol details need freezing

D-A37/44 and I-A42 should explicitly settle whether repeated optional lookup returns
nil after an already-bound provider retires, how retained exports fail, and whether
an initially absent dependency can bind later. The earlier source-copy model's
surviving consumer-owned values must not be carried into the new A-owned model.
Define ID reservation release, replacement during stopping, recovery rearming on
dependency restoration and bounded failed-dependent retry triggers.

The attachment leaves ID limits, comparator grammar, package limits and response
transport partly conditional. Its `scriptingApi: "0.3"` family matcher does not
exist in the baseline; D12's exact identity is not such a matcher. Public schema/API
versions and compatibility acceptance need their own decisions before publication.
R-A04 remains open: no current Carbon provider-authentication/lifecycle experiment
was performed here. A passed Plugin object must not be advertised as authenticated
caller provenance or hostile-managed-plugin isolation without supporting evidence.

### V8 — Phase ordering must not expose partially coordinated addons

Section 15 delays initial support until research, while section 34 permits
topology-neutral foundation work. Clarify this distinction in the eventual task.
Phases B–E may build internal data models/test harnesses, but production activation
cannot precede the global scheduler/facade/lifetime protections placed in Phase F.
Until those exist, no addon may obtain an independently full frame budget or publish
commands through the root's single-active-session coordinator.

Owner-thread serialization avoids simultaneous execution, not reentrant calls,
cross-callback ordering hazards or partially mutated shared state. Fairness, event
fanout and bounds remain qualification requirements, not consequences of one VM.

## Validation performed and not performed

Performed: read the complete attachment; compare the baseline identities and the
specific source paths above; review against canonical ownership/compatibility
policy; check Markdown structure, decision/invariant IDs, JSON example syntax,
relative links and unchanged runtime/API scope. The imported version table is
formatted for the repository's Markdown renderer. The author example remains a
future example, not a runnable example for v0.3.0.

Checks passed: all 60 D-A decision IDs and 58 I-A invariant IDs are present once,
all 34 numbered sections are ordered, code fences are balanced, the manifest JSON
parses, Test-Api.ps1 passes documentation/link/version checks, and git diff has no
whitespace errors. Source-preservation comparison confirms only the declared
authority note and table formatting differ from the attachment (apart from line
endings/trailing file whitespace). Runtime/native/scripts/tests/examples and
release.json are unchanged. These structural passes do not resolve V1–V8.

Not performed: addon implementation, VM patches, cancellation/quota experiments,
provider registration, Carbon server runs, scale tests, sanitizers/fuzzing, CI,
deployment, commit or push. Prior Phase 0–5 evidence remains historical evidence
for its original scope and does not qualify addons. Per Compatibility.md this
documentation review does not require rerunning those matrices.

**Next permissible design step:** resolve V1–V8 through scoped decisions/research
with explicit authorization. The document is saved and reviewed; its open gates
are not resolved merely by being written as invariants.
