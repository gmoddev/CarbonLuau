# GUI Foundation 3E: qualification and release-candidate closure

## Verdict

**PASS within the available qualification envelope.** GUI Foundation 3E closes
the combined server-side model, publication, lifecycle, scale, compatibility,
documentation and release-candidate gates for the D17 surface. The implemented
Foundation 3 surface is ready for experimental public release in the
still-unreleased package `0.4.0` and scripting API `0.4.0-experimental`.

This verdict does not claim authenticated-client clipping, font rendering or
scroll behavior, and does not claim Windows native/local qualification.

## Qualified surface

The final implemented Foundation 3 surface is exactly:

- deterministic `UIGridLayout` with explicit cell geometry, fill topology,
  alignment, list-or-grid exclusivity and retained Position/Size restoration;
- bounded rectangular `Frame.ClipsDescendants` through private projection state;
- immutable `GuiFont` values and retained, patchable `TextLabel.Font` and
  `TextButton.Font`;
- exact-Player `ScrollingFrame:ScrollTo`, `ScrollToTop` and `ScrollToBottom`
  one-way Presentation effects.

No `CanvasPosition`, readback, client geometry or other new GUI feature was
introduced.

## Combined interaction and shared views

The GUI-3E model suite builds a representative tree containing a clipping Frame,
ScrollingFrame, UIPadding, UIGridLayout, text and image nodes, retained fonts and
interactive buttons. It verifies these combinations together:

- grid plus clipping;
- grid plus scrolling;
- grid plus fonts;
- clipping plus scrolling;
- clipping plus interaction authority;
- fonts plus interaction authority;
- a full combined screen under backend failure and recovery.

One retained tree produces equivalent independent Presentations for two Players.
Grid, clipping and font state remains shared retained authority. Scroll position,
pending scroll intent, action authority, client IDs and Presentation epochs stay
Presentation-local. A scroll effect for one exact Player never targets the other
viewer.

## Ownership, publication and lifecycle

The existing Foundation 2E root and provider lifecycle fixtures now execute
with all Foundation 3 feature families active. They cover foreign-domain grid,
clip and font mutation plus a local exact-Player scroll effect, failed candidate
rollback, successful commit, owner retirement, required consumer reconstruction,
optional no-rebind behavior, provider unload/reload and CarbonLuau teardown.

The combined publication tests establish:

- provisional retained writes provide read-your-writes but no client effect;
- 100 failed combined candidates publish no retained mutation or scroll effect;
- committed retained state is synchronized before the associated one-shot
  Presentation effect;
- loss of a Player or Presentation discards only its effect;
- owner retirement stales escaped host-backed GUI references and action authority;
- immutable GuiFont and ImageSource values remain ordinary lifetime-independent
  values;
- a consumed effect is not replayed by an unrelated later rebuild.

Healthy root/addon replacement preserves the old authority until commit, then
uses fresh domain, object, action and Presentation identities. Fatal recovery
and CarbonLuau unload/reload rebuild fresh GUI authority and return registries,
objects, Presentations, actions, pending effects and queued work to baseline.

## Synchronization and fault convergence

Patchable font changes, grid layout dirties, structural clip changes, scroll
configuration rebuilds and Presentation effects share the established bounded
flush path. Injected update, replacement and scroll failures request an
authoritative rebuild and converge to the newest retained state. There is no
historical geometry or effect log. Rebuild precedes a pending scroll effect, and
successful local acceptance consumes that effect.

## Bounds, scale and stress

No configured limit changed. The tested combined screen contains 41 retained
objects and projects to 252 elements, close to the unchanged 257-element screen
limit. Its serialized authoritative operation is 24,641 bytes. The same screen
passed with 1, 10, 50 and 100 Presentations and left zero dirty or full-resync
backlog after each flush.

Measured Linux managed memory upper bounds for initial Presentation projection
were 94,200 bytes at 1 viewer, 154,560 at 10, 5,201,192 at 50 and 11,350,776 at
100. These are practical measurements, not compatibility guarantees. Work and
memory scaled with actual Presentations and projected elements; no persistent
per-viewer idle loop was introduced.

Stress coverage includes 100 healthy root replacements, 200 failed replacement
candidates, 100 rich fatal recoveries, 100 failed combined publications, 1,000
Clone/Destroy cycles, repeated Show/Hide and reconnect churn, 1,000 same-target
and alternating scroll effects, provider loss/restoration and full teardown.
Existing hard bounds remain 64 arranged grid children, clipping depth 4,
FillDirectionMaxCells 1 through 64, 257 projected elements, 16 pending scroll
effects per Presentation, 512 per domain and 4,096 globally.

## Public examples and documentation

The release bundle contains runnable examples for all requested patterns:

1. inventory grid: `grid`;
2. vertical-fill grid: `grid-vertical`;
3. grid plus UIPadding: `grid-padding`;
4. grid inside ScrollingFrame: `grid-scrolling`;
5. clipping Frame: `clipping`;
6. nested clipping: `nested-clipping`;
7. font selection: `fonts`;
8. font patch/change: `font-patch`;
9. ScrollTo top/bottom and normalized positioning: `scroll-effects`;
10. two-viewer per-Player intent: `per-player-scroll`;
11. combined Foundation 3 screen: `foundation3-combined`.

The API checker verifies every example is present, uses no raw CUI or private
command token, and executes through the pinned compiler/VM during the runtime
suite. The GUI guide and reference distinguish desired server state from
unacknowledged client observation and retain the TextBox deferral prominently.

## Compatibility and identity

Foundation 3 is additive to Foundations 1 and 2. The full regression suite
preserves ordinary Position/Size, UIListLayout, UIPadding, LayoutOrder, ZIndex,
GetChildren attachment order, Clone/Destroy, TextButton/ImageButton Activated,
explicit CanvasSize, client-local scrolling, ImageSource, publication,
replacement/recovery and one-tree/multiple-Presentation behavior.

Foundation 3 remains part of the still-unreleased package `0.4.0` and scripting
API `CarbonLuau 0.4.0-experimental`. Completing an additive surface before the
first 0.4 publication does not justify a 0.5 bump. These identities are unchanged:

| Identity | Value |
|---|---|
| Native ABI | `1.4` |
| Provider protocol | `CarbonLuau.Addons` / `1.2` |
| Package schema | `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

Release scripts create deterministic Windows/Linux bundles, checksums and
provenance with the complete example and public-documentation set. GUI-3E does
not create a tag or GitHub Release.

## Deferred qualification

No authenticated current Rust client was available. These features remain
**IMPLEMENTED / AUTHENTICATED-CLIENT UNQUALIFIED**:

- `ClipsDescendants` visual, transparent-parent, nested and clipped-hit behavior;
- rendering and no-fallback behavior of all four GuiFont assets;
- ScrollTo orientation, midpoint, axis behavior, Player isolation, latest-wins,
  rebuild ordering and retry as observed by a real client.

Later supplements must test these exact implementation revisions, or a
documented source-equivalent descendant:

| Feature | Revision |
|---|---|
| ClipsDescendants | `eb25da029c83b2d1a48c4b21bc86e40858ef4dcc` |
| GuiFont | `74ac4075e3d369bc51ca24aa7f3a0b33688d8d61` |
| ScrollTo methods | `6543951131f6f4ac2b31a1d11d6a5ad0bcd36595` |

Windows native/local GUI-3A through GUI-3E qualification is **DEFERRED /
UNQUALIFIED** because DockerPC is unavailable. Hosted Windows CI is separate
evidence and does not substitute for worker process, IPC, native runtime,
packaging, teardown or live Carbon behavior on the intended Windows worker.
This deferral does not invalidate historical Windows evidence for earlier
foundations.

TextBox and Submitted remain unimplemented under D16's exact-text transport
gate. Foundation 4, automatic sizing, CanvasPosition, arbitrary fonts, URL
images, advanced styling, animations, drag/drop, focus/navigation, client
geometry and arbitrary client scripting remain outside this work.

## Validation record

The closure worktree is based on
`6543951131f6f4ac2b31a1d11d6a5ad0bcd36595`. The final implementation/evidence
revision is recorded by the GUI-3E commit in repository history.

Available validation completed:

- local managed build and focused GUI-3E model suite;
- architecture, API, documentation-link and deterministic package checks;
- Linux release native tests and complete managed/runtime regressions;
- all bundled GUI examples through the pinned compiler and VM;
- deterministic linux-x64 release bundle, checksum and provenance checks;
- ASan, UBSan and leak-detection native tests;
- hosted Windows/Linux CI and documentation deployment at final head.

The last item is recorded by the final workflow URLs in the completion report.
No production Foundation 4 or TextBox work began.
