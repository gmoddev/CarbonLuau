# GUI Foundation 3C: retained fonts

Starting commit: `d6cc7f6b80777b32bf576638e63cccfa0b177210`.

GUI Foundation 3C implements only D17's immutable `GuiFont` and retained text
font slice. The exact implementation revision for later authenticated-client
font qualification is `de02135a4ce8167a5bdf40e0f5ae2c41342f8864`.

## Implemented surface

The Luau facade exposes four immutable singleton-like values:

```lua
GuiFont.RobotoCondensedRegular
GuiFont.RobotoCondensedBold
GuiFont.DroidSansMono
GuiFont.PermanentMarker
```

There is no constructor, string-to-font conversion, arbitrary host font name,
path or filesystem capability. Values compare by canonical identity, carry no host
resource and remain ordinary immutable values after a source GUI object or
domain retires.

`TextLabel.Font` and `TextButton.Font` are retained, writable `GuiFont`
properties. Both default to `RobotoCondensedRegular`, so existing scripts keep
the previous default rendering selection. Other GUI classes do not expose
`Font`.

## Projection and synchronization

The backend-neutral render plan carries `GuiFontIdentity`, never a host path.
The Rust CUI backend owns the explicit mapping to the four current Carbon font
asset identifiers. Initial projection includes the retained identity, and Font
changes use the existing bounded text-component patch path. Repeated writes
before a flush coalesce to the newest value. A failed patch enters the existing
full-resynchronization path and converges on retained authority.

Font mutations use the established GUI publication journal. Provisional writes
provide read-your-writes without client output; rollback restores the previous
identity and commit publishes the newest retained state. Clone copies Font.
Destroy, root/addon replacement, fatal VM recovery, provider retirement and
CarbonLuau teardown retain their established object-lifetime behavior without
making escaped GuiFont values stale.

## Qualification status

Verdict: **PARTIAL**.

Available implementation/model gates cover:

- all four values, immutability, equality and distinctness;
- absence of a constructor, arbitrary string conversion and public host paths;
- TextLabel/TextButton defaults, surface restriction and type enforcement;
- deterministic render identities and exact private backend mapping;
- initial non-default projection, patching, coalescing, multi-viewer updates and
  backend-failure reconciliation;
- provisional commit/rollback, Clone, Destroy, replacement, VM recovery,
  provider-style retirement and full teardown;
- complete available GUI, addon, provider, publication and recovery regressions.

Authenticated current-client rendering is **DEFERRED / UNQUALIFIED**. No
authenticated client was available for this task. A later supplement must test
all four fonts for successful rendering without missing-font fallback, changes
between fonts on TextLabel and TextButton, multi-viewer updates, replacement
and recovery. If any value is unavailable, remove that public member rather
than substitute another font.

Windows native/local GUI-3C qualification is **DEFERRED / UNQUALIFIED** because
DockerPC is unavailable. Hosted Windows results are separate evidence and do
not replace process creation, native runtime, deployment or live/local checks
on the required Windows worker.

## Evidence

| Gate | Result |
|---|---|
| Focused managed model | PASS |
| Embedded Luau/native public surface | PASS on Linux |
| GUI Foundations 1A through 3B regressions | PASS on Linux |
| Foundations A through G and Phase 0 through 3 regressions | PASS on Linux |
| Linux release/runtime/package | PASS |
| ASan/UBSan/leak detection | PASS, 5/5 native suites |
| Local Windows managed/static/API/package | PASS |
| Hosted Windows CI | [PASS on attempt 2](https://github.com/gmoddev/CarbonLuau/actions/runs/35555915196) |
| Authenticated-client fonts | DEFERRED / UNQUALIFIED |
| Windows native/local | DEFERRED / UNQUALIFIED |

## Deferred scope and identities

GUI-3C does not implement `ScrollTo*`, TextBox, FontFace, arbitrary fonts,
styling, outlines, rich text or any GUI-3D+ feature. Package `0.4.0`, scripting
API `0.4.0-experimental`, native ABI `1.4`, provider protocol `1.2`, package
schema `1` and the pinned Luau revision are unchanged. D17 still assigns no
Foundation 3 release identity.
