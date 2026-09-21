# GUI Foundation 2B: typed images

Starting commit: `e6e925ff513c499f99898695ef4fc1782706a9e7`.

GUI Foundation 2B implements only D16's typed-image slice. It adds the
immutable `ImageSource` value, retained `ImageLabel` and `ImageButton` classes,
image projection, bounded projection-cost accounting and `ImageButton.Activated`
through the existing GUI-1E ingress. ScrollingFrame, TextBox, submitted text and
all GUI-2C+ behavior remain deferred.

## Public retained model

`ImageSource.None()`, `Sprite(Name)`, `Png(Id)`, `Item(ItemId, SkinId?)` and
`SteamAvatar(UserId)` create immutable, value-comparable userdata. The value has
no domain or host lifetime and can remain ordinary Luau state after a creating
domain retires. Assigning it to an image object creates no new capability.

Sprite keys contain 1 through 256 UTF-8 bytes and use only ASCII letters,
digits, underscore, hyphen, dot and slash in canonical non-traversing segments.
PNG IDs are canonical decimal strings of at most 20 digits. Item IDs are signed
32-bit integers; optional skin IDs and Steam user IDs are canonical unsigned
64-bit decimal strings supplied as strings so Luau number precision cannot
alter them. URLs, paths, Carbon image-database names and arbitrary media
metadata are not accepted.

`ImageLabel` and `ImageButton` are ordinary child-capable GuiObjects. Both add
`Image`, `ImageColor3` and `ImageTransparency`; their defaults are
`ImageSource.None()`, white and zero. Their default size is 100 by 100 and
their background is transparent. One retained source is shared by every
Presentation of the same object. Client cache or asset-load success is neither
retained state nor acknowledged to Luau.

## Backend mapping and synchronization

The backend-neutral plan carries a typed image source. Rust CUI projection maps
Sprite to `sprite`, Png to `png`, Item to signed `itemid` plus optional unsigned
`skinid`, and SteamAvatar to a RawImage `steamid`. None emits no graphic
component. The adapter never writes `url`, performs a download or resolves a
server filesystem path.

Every image object projects a retained container and an image-content element.
ImageButton adds a transparent interaction overlay. ImageLabel therefore costs
two projected elements and ImageButton three. The ScreenGui root and every
existing class also have an explicit worst-case projection cost. Creation and
reparenting reject an attachment atomically when the resulting complete screen
would exceed the configured full-presentation element envelope. The default
envelope remains the existing 257-element render-operation bound; serializer
and payload limits were not raised.

ImageColor3 and ImageTransparency use bounded image patches. Image is
structural and requests whole-presentation reconciliation. A failed patch uses
the existing uncertainty path and later full replacement. The retained value
remains authoritative across backend failure; an unavailable client asset does
not roll it back or create a false acknowledgement.

## Interaction, publication and lifetime

ImageButton uses the exact existing Activated Signal, connection limits,
presentation-specific opaque token, exact Player connection binding, epoch,
VM/domain/ScreenGui/object validation, rate limits, queue admission and
pre-entry revalidation. No second command or transport exists. Image color
patches preserve current action authority; structural source replacement,
Hide, Destroy, replacement and recovery invalidate it under the established
D15 rules.

Image state, connections and Show intent participate in the existing GUI
publication journal. Provisional work cannot flush or create a usable token.
Rollback exposes no client effect, while commit makes one atomic retained state
eligible for a later bounded flush. Clone copies source/color/transparency into
new object identities but copies no listeners, viewers, token or Presentation
state. Destroy and domain retirement stale host-backed objects without revoking
ordinary ImageSource values.

## Qualification

The focused managed suite covers every constructor and source kind; identifier,
range and traversal rejection; defaults; retained readback; value equality;
clone/destroy/stale behavior; shared multi-viewer state; deterministic render
plans; exact Rust CUI fields; absence of URL/filesystem projection; image color
and transparency patches; structural source reconciliation; backend failure;
ImageButton valid and cross-Player action paths; action rotation and Destroy;
publication commit/rollback; and exact projection boundary/overflow behavior.

The native suite exercises constructors, equality, ordinary catchable errors,
retained assignment, both image classes and scheduler-gated ImageButton
callbacks through the real compiler and VM. The full runtime suite retains GUI
Foundations 1A through 1G, GUI-2A, Foundations A through G and Phase 0 through 3
coverage.

Qualification of the implementation worktree completed on 2026-09-20:

- Windows x64 built the native library and compiler worker, passed the complete
  managed/native runtime suite, passed API and architecture audits, passed both
  production import checks, and produced a deterministic release bundle. The
  previously recorded Foundation G Windows worker-memory-limit case remains
  deferred and is not claimed as qualified by GUI-2B.
- A bounded BigVPS Linux container passed all five native CTest suites and the
  complete managed/native runtime suite. A separate ASan/UBSan/leak build
  passed all five native suites with leak detection enabled.
- Windows and Linux package checks found 34 production C# sources, excluded
  native/live fixtures from the plugin package, and reproduced the release
  bundle byte for byte. The image example is included in both platform release
  bundles.
- DockerPC was unavailable and no RustDedicated/Carbon test server was active
  on BigVPS, so live Carbon and authenticated-client visual qualification were
  not available. This does not replace or invalidate earlier live evidence.

The final hosted Windows/Linux and sanitizer CI result is recorded by the
workflow attached to the implementation commit.

Authenticated-client visual loading, tint/transparency appearance and click
receipt remain unqualified and non-gating. The controlled host mapping and
serialized payload are qualified; no claim is made that a particular asset was
available or visibly rendered for an authenticated client.

## Identity and remaining scope

This phase changes no package, scripting API, native ABI, provider protocol,
package schema or pinned Luau identity. Foundation 2 still has no release/API
identity. ScrollingFrame, CanvasSize, TextBox, typed text ingress, Submitted,
grid/automatic layout, arbitrary URL images, Carbon image-database integration,
advanced styling and GUI-2C+ work were not implemented.
