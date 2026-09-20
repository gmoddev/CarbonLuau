# GUI Foundation 1F: lifecycle and runtime closure

Verdict: **PASS for the scoped Foundation 1F implementation and available
qualification**. Authenticated real-client rendering and click evidence remains
**UNQUALIFIED** and is not claimed by this result.

Starting commit: `fcfa609208f4a10dfbdc0b7cd2b18408072f4578`.
Implementation commit: `c5a30f9bebf674d5493232f1d4e0fb146eb59346`.

GUI Foundation 1F closes replacement, fatal recovery, teardown, retry,
diagnostic and leak behavior for the D15 surface implemented by Foundations 1A
through 1E. It adds no public GUI class, event, service or version identity.

## Replacement and ownership

Root and addon candidates retain their GUI state provisionally. A failed
candidate publishes no object, presentation, action token or client effect and
leaves the active lifetime unchanged. A successful candidate commits before the
old lifetime retires; retirement then invalidates old action authority, disposes
its registry and queues bounded best-effort client cleanup. This is an atomic
server-authority transition, not an acknowledged client-side transition.

Controlled tests cover successful and failed root candidates, 100 successful
root replacements, 100 successful addon replacements and 100 failed
GUI-producing candidates. Registry counts return to baseline after every
sequence.

Shared GUI references retain the creating facade's exact domain ownership. A
consumer can mutate, show or connect through an exported owner reference while
the owner remains live. Retiring the consumer leaves the owner's GUI intact.
Retiring or replacing the owner stales every escaped object and Signal reference
held by the consumer. The resulting host connection remains owner-owned, in
accordance with D15, even when its closure was defined by another domain.

## Fatal recovery and teardown

Fatal VM recovery retires the complete old GUI world before VM release. Active
tokens become stale, queued callbacks fail their pre-entry lifetime check,
registries and presentations are disposed, and old client roots receive
best-effort destruction. Reconstruction starts from committed source/package
snapshots and creates fresh GUI authority; it preserves no runtime tree, token,
presentation or closure.

The real native-ABI fixture completed 100 fatal recovery cycles. Each cycle
suppressed old queued work, rejected old references and tokens, reconstructed a
fresh runtime, and returned final registry state to baseline.

Domain/provider retirement and host unload use the same owner-bound disposal
path. Controlled GUI domain-retirement coverage and the retained Foundation B/C
provider lifecycle regressions jointly cover unload, replacement, fresh
registration and stale-identity rejection. A GUI-specific live provider fixture
was not available, so provider GUI behavior is not represented as live Carbon
evidence. Live Carbon did prove ten complete
CarbonLuau unload/reload cycles with GUI state present: each unload unmapped the
native library and ended the compiler worker, and each reload reported a fresh
GUI baseline while the server remained responsive.

## Pending work and backend recovery

Retirement and destruction make obsolete patches, full rebuilds, destroys,
Show/Hide intent, callbacks, full-resynchronization state, blocked
presentations and backend retry state non-executable. Scheduler nodes may remain
until normal bounded draining, but immutable lifetime and presentation gates
prevent entry into retired authority.

Fault-injection coverage includes repeated failed Replace followed by success,
failed Update promoted to a full rebuild, failed Destroy followed by disconnect,
retirement during full resynchronization, screen destruction during retry,
disconnect during retry and Hide/Show around a failed rebuild. Retained state is
authoritative, only the newest revision remains pending, and an accepted later
full replacement converges from that state. No historical render revision queue
is introduced.

## Bounded operator diagnostics

`carbonluau.status`, which remains restricted to Carbon authentication level 2,
now includes one aggregate GUI record covering:

- registries, live objects, ScreenGuis, presentations and GUI Signal
  connections;
- dirty, full-resynchronization, blocked and pending-destroy presentations;
- active and bounded retired action-token counts;
- accepted, rejected and pre-entry-stale action totals;
- backend failures, accepted full rebuilds and accepted patches; and
- GUI resource-limit rejections.

All totals are fixed-cardinality saturating counters or current aggregate
counts. Token values, Player identifiers and per-object labels are never
reported. Diagnostics allocate no unbounded history and are cleared with their
owning GUI world.

## Stress and leak closure

The focused suite completed:

- 100 root replacements, 100 addon replacements and 100 failed candidates;
- 100 fatal VM recoveries;
- 1,000 Show/Hide, 1,000 Clone/Destroy and 1,000 reconnect cycles;
- repeated full-rebuild token rotation and backend failure/recovery;
- dirty-detail overflow, action-token saturation and presentation saturation.

Final assertions cover objects, screens, Signals/connections, presentations,
dirty and retry state, action tokens, callback queues and registered GUI worlds.
All returned to the expected live-root or zero baseline. Resource-limit and
diagnostic counters saturated without wrapping or growing cardinality.

## Qualification evidence

On Windows, the Release build and the full real-native-ABI managed suite passed
GUI Foundations 1A through 1F, Foundations A through G, addon/package/parser and
Phase 0 through 3 regressions. The focused 1E regression and 1F suites also
passed independently.

On Linux, an isolated two-CPU, 8 GiB worker container passed all five native
CTest targets, the net48 managed build and the complete Mono real-compiler
suite. The same five native targets passed ASan with leak detection and
halt-on-error, and UBSan with halt-on-error.

Live qualification used Carbon `2.0.259.0 [2026.09.03.0]` at commit `21063e8`
with Rust protocol `2633.288.1` and the 2026-09-16 server build. Ten root GUI
reloads and ten full plugin unload/reload cycles passed. Every unload removed
the native mapping and compiler process; every reload produced exactly one GUI
registry, two objects, one ScreenGui, no presentation, one GUI connection and
no active action token. No authenticated Rust client participated, so this does
not qualify visual layout, cursor behavior, client reconciliation or click
receipt.

A focused security diff review of the production lifecycle, diagnostics and
live-runner changes completed with no findings. The live runner is a privileged
fixture for a disposable isolated worker and uses loopback-only RCON with an
ephemeral secret.

Foundation G's separate Windows live/local compiler-worker qualification remains
deferred exactly as recorded in [Foundation G](FoundationG.md). Foundation 1F
does not reinterpret that historical qualification status.

## Remaining GUI Foundation 1G work

Foundation 1G is qualification and release-planning work, not authorization for
another public GUI feature. It must provide:

- authenticated current-client evidence for visual layout, cursor behavior,
  private command receipt and visible click callback behavior;
- authenticated-client reconciliation evidence across patches, full rebuilds,
  Hide/Show, disconnect/reconnect and lifecycle replacement;
- live multi-viewer cost and behavior evidence beyond the controlled/model
  measurements;
- current supported Carbon/Rust host-adapter upgrade checks; and
- an explicit package/scripting identity and release decision only after those
  gates close.

Until then the GUI package and scripting API identity remains **UNASSIGNED /
release-planning gated**. Package `0.4.0`, scripting API
`0.4.0-experimental`, native ABI `1.4`, provider protocol
`CarbonLuau.Addons` / `1.2`, package schema `1` and the pinned Luau revision are
unchanged.

## Foundation 1G disposition

Foundation 1G subsequently resolved the release-planning gate by assigning the
complete D15 surface to the still-unreleased package `0.4.0` and scripting API
`0.4.0-experimental`. The authenticated-client items listed above remain
unqualified, but an explicit scope decision made them non-gating for the
experimental identity. This does not retroactively turn Foundation 1F's
server-side evidence into visual, cursor, click-receipt or client-reconciliation
evidence.
