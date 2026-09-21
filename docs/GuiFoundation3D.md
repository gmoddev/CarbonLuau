# GUI Foundation 3D: Presentation-specific scroll effects

Starting commit: `74ac4075e3d369bc51ca24aa7f3a0b33688d8d61`.

GUI Foundation 3D implements only D17's one-way Presentation scroll-effect
slice. The implementation revision requiring later authenticated-client
qualification is `6543951131f6f4ac2b31a1d11d6a5ad0bcd36595`.

## Public surface

```lua
Scroll:ScrollTo(Player, Vector2.new(0.5, 0.5))
Scroll:ScrollToTop(Player)
Scroll:ScrollToBottom(Player)
```

`ScrollTo` accepts finite normalized coordinates in `0..1`, with `(0, 0)` at
the top left and `(1, 1)` at the bottom right. It transmits only axes enabled by
the retained `ScrollingDirection`. Top and Bottom transmit only vertical intent
and fail synchronously when Y scrolling is unavailable. There is no clamping.

These methods are available only on `ScrollingFrame`. They require the exact
current D11 Player connection and an existing Presentation of the containing
ScreenGui. They do not queue for a future Show. Another viewer is unaffected.

## Effect and backend model

An effect is not retained scroll state. CarbonLuau exposes no `CanvasPosition`,
readback, completion event, acknowledgement or client geometry. The
backend-neutral request carries one Presentation epoch, one projected
ScrollingFrame identity and optional normalized X/Y intent. Only the Rust CUI
backend knows that the host uses `horizontalNormalizedPosition` directly and
the inverse `verticalNormalizedPosition` convention.

Current Carbon and Rust client source expose nullable horizontal and vertical
normalized update fields and apply them independently during CUI updates:

- [Carbon CUI source](https://github.com/CarbonCommunity/Carbon/blob/main/src/Carbon.Components/Carbon.Common/src/Carbon/Components/CUI.cs)
- [Rust Community UI source](https://github.com/Facepunch/Rust.Community/blob/master/CommunityEntity.UI.cs)

## Bounds, publication and synchronization

Pending state is latest-wins per `(Presentation, ScrollingFrame)`, with no
historical queue. Hard bounds are 16 effects per Presentation, 512 per domain
and 4096 globally. Admission beyond a bound fails synchronously before changing
state.

Committed calls stage work for a later existing owner-thread GUI flush.
Provisional calls use the existing GUI publication snapshot. Rollback publishes
nothing. At outer commit, retained GUI state is already authoritative; the
effect resolves the resulting exact Player and Presentation and binds to that
Presentation. A vanished Player, Presentation or ScrollingFrame discards only
the ephemeral effect and does not roll back otherwise valid retained work.

Retained synchronization always runs first. A required rebuild is accepted
before a pending effect is attempted. A failed effect send retains only the
newest intent, requests authoritative reconciliation and retries after that
rebuild. Local acceptance consumes the effect. Later unrelated rebuilds do not
replay it.

Effect sends share the existing global GUI send, byte and time budgets. Domain
and Presentation cursors preserve progress when another effect repeatedly
fails. The flush never enters Luau.

## Lifecycle and diagnostics

Pending effects are discarded on Hide, object/screen destruction, disconnect,
cross-screen reparenting, domain replacement, provider retirement, VM recovery
and CarbonLuau teardown. Same-account reconnect receives a new connection
lifetime and no prior effect. Clone copies only retained ScrollingFrame state.

Bounded internal diagnostics count accepted, coalesced, bound-rejected,
Presentation-discarded, Player-discarded, backend-failed and locally accepted
effect events. CarbonLuau does not report an actual client position.

## Qualification status

Verdict: **PARTIAL**.

| Gate | Result |
|---|---|
| Focused managed model and stress | PASS |
| Embedded Luau/native public surface | PASS on Linux |
| GUI Foundations 1A through 3C regressions | PASS |
| Foundations A through G and Phase 0 through 3 regressions | PASS on Linux |
| Linux release/runtime/package | PASS |
| ASan/UBSan/leak detection | PASS |
| Local Windows managed/static/API/package | PASS |
| Hosted Windows CI | PASS at historical source-equivalent commit `31065a78f9184b164943b6d29d811ff10f6498aa`, retained on `codex/backup-main-pre-squash-20260921` |
| Authenticated-client scroll behavior | DEFERRED / UNQUALIFIED |
| Windows native/local | DEFERRED / UNQUALIFIED |

Authenticated current-client qualification must still establish top, bottom,
left, right, midpoint and XY behavior; exact two-viewer isolation; same-frame
latest-wins; Hide/Show; rebuild ordering; retry; disconnect and stale Player;
provisional commit/rollback; and owner replacement. No authenticated client was
available for this task.

Windows native/local GUI-3D qualification is **DEFERRED / UNQUALIFIED** because
DockerPC remains unavailable. Hosted Windows is separate evidence and does not
replace the required native/local environment.

Hosted Windows, Ubuntu and sanitizer jobs passed in
[validation run 35558447750](https://github.com/gmoddev/CarbonLuau/actions/runs/35558447750).
The documentation deployed successfully in
[documentation run 35558447742](https://github.com/gmoddev/CarbonLuau/actions/runs/35558447742).

## Scope and identities

GUI-3D does not implement readable or retained scroll position, scroll events,
animation, ScrollIntoView, TextBox, GUI-3E or Foundation 4. Package `0.4.0`,
scripting API `0.4.0-experimental`, native ABI `1.4`, provider protocol `1.2`,
package schema `1` and the pinned Luau revision remain unchanged. D17 still
assigns no Foundation 3 release identity.
