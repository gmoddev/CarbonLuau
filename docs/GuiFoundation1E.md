# GUI Foundation 1E: secure Activated ingress

Verdict: **PASS for the scoped Foundation 1E implementation and available
qualification**. Authenticated real-client click receipt remains
**UNQUALIFIED**.

Starting commit: `31d0301c62d8a9f1cade08136bd2d269919dcf8a`.
Implementation and qualification commit: `918632a97255887eafc91e09e12287a2caa137c5`.

GUI Foundation 1E implements the one approved client interaction:
`TextButton.Activated`. It adds no other GUI event, class, input surface or
release identity.

## Private command path

Rust CUI receives one CarbonLuau-owned command in this fixed form:

```text
carbonluau.gui.action <32-lowercase-hex-token>
```

The Carbon handler accepts only a connected Player-origin command with exactly
one bounded argument. It verifies that Carbon's command connection is the same
object as the current Player connection, resolves the current Player lifetime,
and then asks the retained GUI world to validate and enqueue the action. It does
not enter Luau, render CUI or expose command construction to scripts.

The command text is assembled exclusively by CarbonLuau's render compiler.
Addon strings, public Commands registrations and GUI properties cannot supply a
client command. Tokens are not exposed through GUI userdata or properties.

## Token authority and lifecycle

Each action token is 128 random bits encoded as 32 lowercase hexadecimal
characters. The string is only an opaque lookup key. Its server record binds:

- VM generation and owner domain lifetime;
- ScreenGui object identity and presentation epoch;
- exact Player identity, connection, connection token and user ID;
- TextButton object identity;
- the owning retained registry and the per-action limiter.

Each Player presentation receives distinct tokens. An accepted full
presentation replacement publishes a fresh token set only after the backend
accepts the replacement. Failed or oversized replacements publish no action
authority. Pure property patches retain the current token. Structural changes,
known uncertainty, full reconciliation, Hide, destruction, disconnect, domain
retirement and VM recovery retire prior tokens and fail closed.

Active registries are bounded per presentation, per domain and globally.
Retired-token diagnostic tombstones are also globally bounded. Token generation
rejects collisions with active, retired and same-candidate identities.

## Ingress and scheduler admission

Before queue admission, CarbonLuau validates canonical token encoding, active
registry membership, the exact current Player connection, live VM/domain,
desired Presentation and epoch, live ScreenGui and TextButton, current ancestry,
effective visibility and live Activated listeners. Per-action and per-Player
token buckets then bound repeated actions.

One activation creates one normal scheduler work item per current listener in
registration order. The complete fanout is packed and capacity-checked before
any item is queued, so insufficient queue capacity rejects the action
atomically. Existing Signal connection bounds remain authoritative. Tokens are
repeatable within an epoch and delivery is not exactly once.

Native operation 9 revalidates the immutable action payload immediately before
scheduled Luau entry. Hide, destruction, reparenting, listener removal,
disconnect, domain retirement or epoch replacement after admission suppresses
the stale callback without a script-visible error.

## Activated semantics and mutation

Scripts continue to use only the existing Signal surface:

```lua
Button.Activated:Connect(function(Player)
    Button.Text = "Done"
end)
```

The callback receives the normal Player proxy for the exact connection that
submitted the valid action. Existing scheduler deadlines, Signal ordering,
Connection ownership and teardown rules apply. Callback mutations update the
retained model normally. They cannot recursively process CUI; GUI-1D performs
the later bounded flush. A callback that destroys its button completes, while
later queued listeners are suppressed by pre-entry revalidation.

## Publication and diagnostics

GUI-1B publication snapshots include presentation epochs, token maps and pending
invalidation state. Provisional Show/Connect work cannot flush or activate a
token. Rollback restores the prior authority exactly. Commit permits normal
post-publication synchronization, and tokens become active only after an
accepted full presentation send.

`carbonluau.status` reports saturating counters for accepted, malformed,
unknown, stale, cross-Player, target-unavailable, rate-limited, queue-full and
pre-entry-stale actions, plus active and bounded retired-token counts. Hostile
requests do not produce per-request logs or script-visible failures.

## Validation status

Focused deterministic tests cover exact Player delivery, ordered listener
fanout, repeat admission, malformed/oversized/forged/cross-Player input, token
rotation and retention, hidden/destroyed targets, disconnect/reconnect,
uncertainty, rate and queue limits, registry bounds, publication rollback and
commit, pre-entry suppression, callback mutation, self-destruction, fatal VM
recovery and domain replacement. A hostile-input fixture submits 10,000 each of
forged, cross-Player, rate-limited and stale requests while checking bounded
registry, queue and diagnostic state.

The Windows real-ABI managed suite passed GUI-1A through 1E model and native
coverage, Foundations A through G and addon regressions, 100 replacement cycles,
publication failures, fatal recovery and loader lifecycle. The production
package contained exactly 33 C# sources, no native binary or live fixture, and
two independent builds produced SHA-256
`7DFD88DF7A0278EAA158316F675D1565A9AE8A91CBFC395BDB5DF311CCC070B0`.

The Linux Release lane passed all five native tests, the complete real-compiler
managed suite, loader, architecture and API checks. ASan, UBSan and leak-enabled
qualification passed the same five native tests. A focused security diff review
reported no findings across all changed production surfaces.

The disposable live Carbon overlay compiled and loaded the production package,
reached runtime generation 2, answered RCON health checks, completed ten plugin
unload/load cycles with the native library unmapped during unload, and recovered
from missing, broken, missing-symbol and wrong-probe native fixtures. The
single-attempt RCON test helper was changed to a bounded retry after a startup
race; the repeated run passed. No production server tree or Pelican workload was
modified.

The existing Foundation G Windows live/local compiler-containment qualification
remains deferred as recorded in [Foundation G](FoundationG.md). A native CTest on
this non-DockerPC host reproduced its unqualified worker-memory-limit case; it
does not change the passing GUI-1E Windows managed evidence or historical
Foundations A through F evidence. Hosted Windows CI remains the native Windows
gate for this change.

Live Carbon qualification proves command compilation/registration, plugin load
and lifecycle only. No authenticated Rust client performed a click, so actual
client command receipt and visible interaction remain unqualified.

## Deferred scope and identities

MouseButton1Click, hover and pointer movement, TextBox/input submission,
ImageButton, arbitrary client commands, client acknowledgement, new GUI classes
and GUI Foundation 1F or later behavior remain deferred. Authenticated-client
visual layout, cursor, reconciliation and click evidence also remain deferred.

No CarbonLuau package version, scripting API version, native ABI, provider
protocol, package schema or pinned Luau revision changed. The GUI scripting
identity remains unassigned and outside `0.4.0-experimental`.
