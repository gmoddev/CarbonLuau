# GUI Foundation 1D: retained synchronization and reconciliation

Verdict: **PASS for the scoped Foundation 1D implementation**.

Starting commit: `0ecd9337851fc368a67993367d29fb9f17c23c2c`.
Implementation and qualification commit: `31d0301c62d8a9f1cade08136bd2d269919dcf8a`.
Recovery correction and final tested-source commit:
`31d0301c62d8a9f1cade08136bd2d269919dcf8a`.

GUI Foundation 1D connects committed retained mutations to already-shown
Presentations. It adds revisioned dirty state, deterministic Rust CUI property
updates, structural full replacement, bounded owner-thread flushing, persistent
fairness and newest-state failure convergence. It does not add client event
ingress or change the unversioned GUI development surface.

## Revision and dirty model

Each retained ScreenGui owns a monotonically advancing revision. Each
Presentation records the newest revision it has locally issued. Render-affecting
committed mutations advance the ScreenGui revision; Name remains metadata-only
and creates no synchronization work.

Patchable dirty state is a bounded map from immutable GUI object identity to a
set of property descriptors. Repeated writes coalesce in that map and the patch
compiler always reads the newest retained value. There is no historical mutation
queue. Dirty detail remains available until every current Presentation of that
ScreenGui has reached the newest revision.

If the per-domain dirty-object capacity is exhausted, the detailed map is
discarded and the affected ScreenGui is marked for full replacement. The valid
retained mutation is never rejected because synchronization detail is full.
Hidden ScreenGuis retain their newest state without client work; a later Show
starts with a full projection of that state.

## Patch and structural classification

The following properties are patched:

- Position, Size and AnchorPoint;
- Visible;
- BackgroundColor3 and BackgroundTransparency;
- Text, TextColor3, TextTransparency and TextSize;
- TextXAlignment and TextYAlignment.

Combined backend properties are regenerated from the current retained pair. For
example, a BackgroundColor3 change emits the current color and current
BackgroundTransparency-derived alpha. Position, Size or AnchorPoint changes emit
the complete five-field RectTransform derived from the current layout tuple.

Object creation or attachment, Destroy, Parent, cross-ScreenGui reparent and
ZIndex are structural. The affected ScreenGui Presentations receive one full
replacement compiled from final retained state. Cross-root reparent marks both
roots. Destroyed objects cannot leave obsolete property patches behind.

A Visible change that changes whether the tree needs a cursor is promoted to a
full replacement because removing the Foundation 1 root cursor marker is not a
safe partial CUI operation. Visible remains patchable when cursor state is
unchanged.

## Bounded GUI flush

`ScriptHost.Drain` now performs GUI work only after the bounded Luau callback
loop has returned and the VM is no longer entered:

```text
callback/task drain
    -> VM returned and host not busy
    -> one shared bounded GUI flush
```

The GUI phase has the current internal 1 ms target plus the existing global
64-send and 256 KiB serialized-byte limits. These are qualification values, not
public API guarantees. An operation already in flight may finish after the time
target, but no next operation begins after the deadline. Exhausted work remains
dirty for a later host turn.

One `FacadeWorld` budget is shared across root and addon domains. Persistent
domain and Presentation cursors provide deterministic round-robin progress. A
failed Presentation receives at most one attempt in a world flush cycle, so it
cannot consume the full shared send budget. Standalone test drains do not share
the world-cycle attempt namespace.

## Rust CUI updates

`IGuiBackend` measures the actual operation before transport. `RustCuiBackend`
serializes property patches with `update=true`, stable opaque element names and
only the components and fields required by the dirty descriptors. It validates
the final UTF-8 size before calling transport. Text-only updates do not emit
RectTransform or DestroyUI fields.

If a patch cannot fit or cannot be represented safely, the same Presentation is
promoted to a full replacement. If the complete current full projection also
cannot fit, that revision enters a controlled blocked state with no send and no
hot retry. A newer retained revision clears the block and is evaluated from its
newest state.

## Failure and reconciliation

Any failed or unavailable Update makes the Presentation uncertain and requires
a later full replacement. A failed Replace remains pending as a full replacement.
No failed path queues old plans or render revisions. Exact Player connection
resolution is repeated immediately before work; stale presentations are removed.
Destroy and Hide remain authoritative even when best-effort client cleanup fails.

After 32 successful patch batches for a Presentation, its next dirty
synchronization is a full replacement. The threshold is internal and
configurable. It reduces long-lived silent drift without claiming client
acknowledgement.

Show followed by Hide before a flush sends nothing. Hide followed by Show
destroys the retired epoch before sending a new full projection with fresh IDs.
A mutation before first Show is folded into the initial full tree. Publication
rollback restores retained, dirty and Presentation state and therefore produces
no backend operation.

## Modeled performance evidence

The focused model uses one 50-object Foundation 1 ScreenGui and the production
Rust serializer. Representative local Windows results were:

| Viewers | Retained presentation memory | Initial full | Text patch | Position patch | Structural rebuild |
|---:|---:|---:|---:|---:|---:|
| 1 | 31,752 B | 4.060 ms / 15,592 B | 0.107 ms / 138 B | 0.154 ms / 211 B | 4.492 ms / 15,924 B |
| 10 | 34,208 B | 32.831 ms / 155,920 B | 0.546 ms / 1,390 B | 1.036 ms / 2,110 B | 39.831 ms / 159,250 B |
| 50 | 45,120 B | 204.750 ms / 779,600 B | 2.151 ms / 6,950 B | 4.853 ms / 10,550 B | 276.963 ms / 796,250 B |
| 100 | 52,144 B | 292.338 ms / 1,559,200 B | 2.629 ms / 14,000 B | 5.030 ms / 21,100 B | 233.903 ms / 1,592,600 B |
| 256 | 87,328 B | 732.416 ms / 3,991,552 B | 11.293 ms / 35,840 B | 15.884 ms / 54,016 B | 582.932 ms / 4,077,056 B |

The elapsed columns measure complete modeled backlog serialization, not one
bounded host turn. Text and Position workloads converged in one operation per
viewer, with one send per direct model flush. Structural byte totals show why
full replacements must remain globally budgeted across turns. A separate shared
flush fixture stops after one 3 ms in-flight backend send and carries the other
two presentations forward without duplication. The current limits required no
tuning. A 101-presentation same-domain fairness fixture serviced the unrelated
ScreenGui in four bounded turns on the recorded run and always within one full
round-robin pass.

## Validation status

`GuiFoundation1DTests` covers coalescing, every patch class, multiple objects,
metadata suppression, structural cases, Show/Hide ordering, hidden mutation,
dirty overflow, reconciliation checkpoints, exact Rust update JSON, payload and
flush budgets, injected Update/Replace failures, newest-state recovery,
publication rollback/commit, disconnect/teardown, multi-domain and same-domain
fairness, viewer scale and post-Luau native flushing.

Final qualification on 2026-09-19 produced the following results:

- Local Windows .NET Framework Release compilation completed with zero warnings
  and errors. GUI-1A, GUI-1B, GUI-1C and GUI-1D focused model suites passed, as
  did architecture, API, release, deterministic package and diff checks.
- Hosted Windows built every native target and the compiler worker. All five
  native tests, import checks, complete real compiler/VM managed suite, loader
  tests, packaging and release artifact generation passed.
- Hosted Ubuntu 24.04 passed the equivalent native build/tests, complete
  managed/native suite, loader/export checks, packaging and release artifacts.
- The ASan/UBSan/leak lane passed ScriptCore, RuntimeAllocationFaults,
  CompilerContainment, RuntimeCore and NativeLoadUnload with sanitizer halt and
  leak detection enabled.
- The immutable corrected source passed all three lanes in
  [validation run 35478372835](https://github.com/gmoddev/CarbonLuau/actions/runs/35478372835).
  GitHub Pages documentation deployed successfully in
  [run 35478234017](https://github.com/gmoddev/CarbonLuau/actions/runs/35478234017).

The first hosted run exposed a null VM recovery-path dereference in the new
post-callback GUI guard. Commit `31d0301` added the missing live-VM check; the
complete corrected-source matrix above then passed. The failed run is not cited
as qualification evidence.

DockerPC live/local qualification was unavailable. The
DockerPC SSH path reached MiniVPS, but the expected loopback reverse-tunnel
listener on port 2222 was absent. No tunnel, host or unrelated workload was
changed. No live Carbon or authenticated-client result is claimed.

## Deferred scope and identities

Foundation 1D does not implement Activated ingress, action tokens, a private GUI
client command, interaction rate limiting, click callbacks, images, TextBox,
scrolling, layouts, advanced styling or any GUI Foundation 1E behavior.

No package version, scripting API version, native ABI, provider protocol,
package schema or pinned Luau revision changed. The GUI scripting identity
remains unassigned and outside `0.4.0-experimental`. Authenticated-client visual
layout, cursor behavior, reconciliation and click receipt remain unqualified.
