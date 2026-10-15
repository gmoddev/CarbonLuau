# CarbonLuau compatibility and validation policy

This document owns compatibility promises, version identities and evidence requirements. Runtime rules are in [Invariants.md](Invariants.md); task workflow is in [AICONTEXT.md](../AICONTEXT.md). It records a narrow supported target direction, not universal compatibility.

## Support dimensions

The current [v0.7.0 release receipt](Release-0.7.0-Validation.md) qualifies Rust
build 25653776 / revision 166494 / protocol 2634.289.1, Windows Carbon
2.0.262.0 (`8a81d70`) and Linux Carbon 2.0.261.0 (`c74c4ca`), Harmony 2.4.2.0
within the recorded assembly/topology and startup-continuity envelope. The
foundational targets below retain their original evidence depth; they are not
an unrestricted host-version promise. Post-release offline research does not
expand that envelope.

| Dimension | Current commitment and evidence limit |
|---|---|
| Operating system | Windows and glibc-based Linux are the v0.1 targets. Worker Windows and Ubuntu 24.04 Docker passed Phase 0; this does not qualify every Windows release/Linux distribution or define a minimum glibc version. |
| Architecture | x64 process and native binaries only. ARM64, x86, macOS and other targets are unqualified and rejected by Phase 0. |
| Managed runtime | Keep deployed code compatible with the proven .NET Framework/Mono-style Carbon runtime. The `net48` loader test passed on Windows and Mono 6.8. A .NET SDK used for tooling is not the server runtime. |
| Carbon | CarbonPlugin and source `.cszip` are the integration/package model. Earlier worker evidence is Carbon 2.0.259.0; Persistence-2D live Linux used Carbon 2.0.261.0 and Windows used the recorded current archive. No unrestricted future Carbon-version guarantee. |
| Rust server | The loader baseline used Rust 2633 / Steam build 25230300; Persistence-2D live qualification used build 25653776. New server builds need affected compatibility checks, not assumptions based on the same game name. |
| Luau | Vendored commit is recorded in [LUAU_REVISION.txt](../native/third_party/LUAU_REVISION.txt). Phase 1 links compiler/VM; its platform and containment evidence is separate in [Phase1-Validation.md](Phase1-Validation.md). |
| Hosting provider | The user's Shockbyte Linux server passed only the historical Phase 0 load/probe/unload/reload gate based on user-provided logs. Phase 1-5 full-runtime behavior remains unqualified there. By user decision on 2026-09-15, Shockbyte full-runtime qualification is deferred and is not a Phase 5/v0.1 gate; this is not a support claim, and requalification is required before advertising Shockbyte as qualified or supported. Other servers/plans and provider policy changes remain unqualified. |

Dates, source/artifact hashes, runtime paths, CI links and the different depths of worker versus Shockbyte testing are owned by [Phase0-Validation.md](Phase0-Validation.md). PROVEN describes the tested native-load gate, not the safety of the future VM or all features in the design.

Carbon currently documents targeting .NET Framework 4.8 with `Carbon.targets`. Do not introduce modern-.NET-only APIs or netstandard2.1-only dependencies into the plugin without proving support on the actual runtime and documenting an accepted compatibility change. Keep project-owned native code and managed runtime aligned. [Carbon project setup](https://carbonmod.gg/devs/creating-your-project). Source packaging follows [Carbon's ZIP documentation](https://carbonmod.gg/devs/features/zip-script-packages).

## Version identities

The current release mapping is package `0.7.0` / `v0.7.0`,
containing the complete `0.7.0-experimental` API. ABI `1.5`, provider `1.2`,
schema `1` and Luau are unchanged. [Release qualification](Release-0.7.0-Validation.md)
owns final-source/publication gates; the phase assignments below remain chronology,
not current package holds. See [release notes](releases/0.7.0.md).

Gameplay C4's user-authorized removal of Player:HasItem, CountItem, GiveItem and
TakeItem is breaking and advances development API to `0.7.0-experimental`.
Acquire `Player:GetInventory()` and use those methods on PlayerInventory instead;
there are no retained aliases. Existing read/Signal introductions stay at 0.6.5;
facade GiveItem/TakeItem are introduced at 0.7.0 and included in package/tag
0.7.0/v0.7.0. ABI 1.5, provider 1.2, schema 1 and Luau are unchanged. [C4 qualification](GameplayC4-Validation.md)
owns the current platform/lifecycle and release-readiness disposition; historical
C1–C3 evidence describes its actual earlier API, not current legacy availability.

Developer tooling is separate from the production server support table above.
[ToolingBaseline.md](ToolingBaseline.md) owns its independent extension, protocol,
pack, metadata, preview-plan, tooling-native and LSP identities, developer-platform
targets and qualification requirements. In particular, macOS tooling does not
imply macOS CarbonLuau server support. Baseline adoption changes none of the
runtime identities below. For a documentation/bootstrap-only tooling change,
run document/contract checks, extension build/static/manifest checks and a
deterministic server-package exclusion check. Full runtime regressions become
mandatory when shared production code is actually extracted; see
[ToolingFoundationA.md](ToolingFoundationA.md).

Keep these identities conceptually separate and record those relevant to a result: Carbon build, Rust server build, pinned Luau commit/build options, CarbonLuau package version, project-owned native ABI compatibility, and CarbonLuau public scripting API compatibility.

Gameplay A's additive development surface uses the user-selected
`0.6.5-experimental` scripting identity. PlayerDied/PlayerSpawned and their
immutable context records retain that introduction version; historical Entity
introductions remain `0.6.0-experimental`. Package/tag remain `0.5.0`/`v0.5.0`,
ABI `1.5`, provider `1.2`, schema `1` and Luau unchanged. No release/tag follows
from this assignment. [Gameplay A evidence](GameplayFoundationA-Validation.md)
separates qualified exact server observations, synthetic transport fixtures,
hosted tests and still-unqualified authenticated-client behavior.

Gameplay B1 introduces only `Workspace.EntitySpawned` under the same
`0.6.5-experimental` development API. [B1 qualification](GameplayB1-Validation.md)
records exact Windows/Linux completed-Spawn evidence, original-lifetime
admission, shared global Player/Entity event limits, regressions and host
topology/restart requirements. Earlier Entity members keep their 0.6.0
introduction. No package/ABI/provider/schema/Luau identity changes or release/tag
follow. Live server evidence is separate from synthetic lifecycle/stress tests
and does not claim authenticated-client behavior.

Gameplay B3 adds `Workspace.EntityDestroyed` and immutable `EntityDestroyedContext`
under the same `0.6.5-experimental` identity. [B3 evidence](GameplayB3-Validation.md)
separates successful original world removal and proven pool admission from native
deletion with potentially surviving Rust bookkeeping, and records nil native-only/
direct-pool Position, exact epoch proof, the separate three-target removal/pool
topology and unchanged 24-target Spawn
topology. Native cancellation alone, pre-proof weak collection and attempted Kill
are not completion. The native complement observes at most 128 original weak
watch slots per actual Unity frame through existing frame intake; it does not
scan the realm or resolve IDs. Last-live publication cutoffs prevent replay and
may conservatively suppress ambiguous recent-registration windows. B3 adds
bounded weak observation state: 262,144 watches, 64-MiB UTF-16 snapshot text
accounting and 16 removal frames. That text charge is not a total
managed-heap or process-RSS guarantee. Existing Player/Entity event budgets remain
shared, not per addon. The evidence record owns current completion gates;
historical A/B1 evidence is preserved, not substituted. No package/ABI/provider/
schema/Luau identity change or release/tag follows.

Gameplay B2 adds `Workspace.EntityDied` and the frozen EntityDeathContext under
`0.6.5-experimental`. [B2 evidence](GameplayB2-Validation.md) owns its final gate
status and distinguishes all-type structural/loaded-topology coverage from
representative physical deaths and synthetic VM/lifecycle tests. The source
observes qualified full terminal completion in pinned combat/resource families;
ordinary removal is not death. Historical Id/Prefab/optional scalar snapshots
never reacquire the current ID occupant. Actor authority is source-specific
original RPC melee only; unproved NPC/environment/indirect attribution is nil.
Qualified episode resets do not change D20 incarnation or D11 connection rules.
Eight weak attempt-topology stamps and sixteen frames bound private synchronous
bookkeeping; all established event budgets remain shared. No authenticated-client
claim, generic hook, additional mutation, package/ABI/provider/schema/Luau change
or release/tag follows from this phase. Remaining source/qualification gates are
reported explicitly in the evidence record, not inferred from generated types.

Phase 0 package `0.0.1`, Phase 1 package `0.1.0`, native ABI `1.0`, and the probe magic are not scripting API versions. Native ABI major mismatch is rejected before runtime binding; layouts and ownership are specified in [Phase1.md](Phase1.md#native-boundary-and-ownership). A package bump does not automatically mean a script break. Phase 3 introduces package `0.3.0`, additive native ABI `1.2`, and the separate scripting identity `CarbonLuau` / `0.3.0-experimental` / `Experimental`, inspectable through read-only game fields and operator status. D8/D12 own the minimum policy; [public compatibility](api/Compatibility.md) documents it. Additive changes preserve existing contracts; removing/renaming an API or changing its types, lifetime, failures or authorization is breaking and requires an explicit version, documentation and migration decision. A larger deprecation/negotiation framework remains deferred.

The published `v0.3.0` release maps package `0.3.0`, scripting API
`0.3.0-experimental` and native ABI `1.2`. The roadmap's “v0.1” label is a scope
name, not another semantic version.

Post-release Foundation A source adds internal domain exports as additive native
ABI `1.3`. This does not alter the already-published v0.3.0/ABI 1.2 artifacts or
assign a new scripting API identity. A future release must deliberately map its
package, native ABI and still-pending addon scripting/protocol identities.

The qualified v0.4.0 candidate assigns the additive addon and GUI-capable identity
`CarbonLuau 0.4.0-experimental`. It maps package `0.4.0`, native ABI `1.4`,
provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and pinned Luau
revision `c6b830185af962c82003f86784e2fe036357c830`. The identities remain separate,
and v0.3.0 artifacts are unchanged. The machine-readable mapping is
[release.json](../release.json), with build and provenance instructions in the
[release guide](Release.md).

GUI Foundation 1 is implemented through Foundation 1F and publicly closed by
Foundation 1G. It is included in package `0.4.0` and scripting API
`0.4.0-experimental`. No 0.5.0 bump is warranted because 0.4.0 has not been
released and GUI is additive to that first public addon candidate. Native ABI
`1.4`, provider protocol `CarbonLuau.Addons` / `1.2`, package schema `1` and the
pinned Luau revision are unchanged. Authenticated-client visual layout, cursor,
actual click receipt and client reconciliation remain explicitly unqualified;
that evidence is no longer a gate for the experimental identity.

GUI Foundation 2A through 2C implement deterministic layout, typed images and
retained scrolling. [Foundation 2E](qualification/Gui2.md#guifoundation2e) qualifies their
lifecycle, shared-view, cross-domain, replacement, recovery, provider and
failure behavior. [Foundation 2F](qualification/Gui2.md#guifoundation2f) closes public documentation,
examples, compatibility, bounded scale and release preparation and assigns this
implemented subset to the existing package `0.4.0` and scripting API
`0.4.0-experimental`. No 0.5.0 identity is needed because 0.4.0 remains
unreleased and the subset is additive. `TextBox`
is **DEFERRED / NOT IMPLEMENTED**. Rust Dedicated Server app `258550`, build
`25353106`, was inspected for GUI-2D and trims the complete command and
`ConsoleSystem.Arg.FullString`; trailing and whitespace-only input cannot meet
D16's exact-preservation contract. Parsed argument reconstruction is not an
acceptable substitute. This finding is build-specific and may be revisited if
the host later exposes a bounded opaque text-preserving UI-input payload.

Public behavior changes need deliberate compatibility review, documentation and behavioral tests. Prefer adapting to host changes beneath the facade. If an accepted public behavior cannot be preserved, state the break and migration decision explicitly; do not silently expose new host internals to compensate.

Persistence Foundation 1 was explicitly assigned scripting API
`0.5.0-experimental` on 2026-09-23; it is not part of historical 0.4 availability.
[D12](Invariants.md#d12--scripting-and-protocol-identity) owns the decision and
[Release.md](Release.md) the independent v0.5.0 package/tag mapping. Existing
catalog entries retain historical SinceApi values. Persistence metadata follows
actual bindings and is Experimental; preview remains unavailable. Foundation 1
implementation evidence remains in [1B validation](qualification/Persistence1B.md#persistencefoundation1b-validation),
with combined closure and Windows/Linux live limits in [1C](PersistenceFoundation1C.md).
The v0.5.0 prerelease qualification is recorded separately and does not imply
stable production readiness.
The additive reserved completion export requires native ABI 1.5; no provider
protocol, addon schema or Luau change is implied.

Persistence Foundation 2 architecture is resolved by [D22](Invariants.md#d22--persistence-foundation-2--bounded-derived-indexes-and-query)
and [PersistenceFoundation2.md](PersistenceFoundation2.md). Its implemented Query uses
required `Field`, structured `Equals` or inclusive `Min`/`Max`, automatic
preparation and optional `Indexes` string-list hints. Authors manage no schemas,
versions, migrations or index lifecycle. It joins the same published experimental
`0.5.0-experimental` scripting identity. The [final correction audit](history/persistence/PersistenceFoundation2-Validation.md#persistencefoundation2-validation)
is architecture evidence. The 2B optional hint binding and metadata are
[qualified](PersistenceFoundation2B.md); public Query bindings and generated
metadata are [qualified by 2C](PersistenceFoundation2C.md). Combined closure is
recorded separately as PASS in [2D](PersistenceFoundation2D.md). The v0.5.0
package release remains experimental and does not add preview storage behavior.

## Dependency and host upgrades

Luau upgrades must be explicit scoped changes. Preserve its pin and MIT/Lua attribution; review relevant upstream API, sandbox, compiler, bytecode and behavior changes. Validate affected builds and runtime failure paths against the candidate pin before claiming support. Do not edit the vendor tree or update dependencies incidentally while implementing another feature. Internal compiler/VM bytecode compatibility is tied to the selected pin; external untrusted bytecode remains excluded by I7.

For Rust/Carbon updates, record candidate versions, compile against the actual references where appropriate, then run the canonical smoke fixture and affected adaptation tests. Requalify target-host behavior when the native loader, platform dependencies, sandbox or host policy could change the outcome. Preserve old-version evidence as historical evidence, not proof for the new version. Setup scripts fetch mutable upstream releases: record what was actually installed each run.

## Validation selection

Gameplay D2A is private-only one-prefab destruction-payout adaptation, routed
through [its qualification record](GameplayD2A-Validation.md). Existing D20/B3
initial-startup continuity, exact Windows/Linux target and requalification gates
apply. It introduces no Loot service, public DropMode enum, public metadata or
release identity. Logical publication and nontransactional physical payout are
separate; the record distinguishes pre-COMMIT ineligibility, verified outcomes
and post-COMMIT partial/unknown state without replay or compensating deletion.

Gameplay D3 exposes that same qualified adapter through initialization-only
`Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)` in development
API `0.6.5-experimental`; [D3 evidence](GameplayD3-Validation.md) owns final public
Windows/Linux, real-VM, metadata, tooling, packaging and hosted CI status. D2A's
source eligibility/initial-startup continuity and strict destruction-only failure
contract remain unchanged. Public declaration acceptance is not a physical payout
success result. No new prefab, inventory Signal, release/tag or ABI advancement
is implied; `GiveItemBehavior` retains its existing contract.

Post-release [Gameplay D4](GameplayD4-Validation.md) qualifies the second exact
autospawn barrel on the same pinned Windows/Linux hosts, with unchanged three-target
interception, publication, item ownership and numerical limits. Exact path/ID pairs
select immutable descriptors; one global rule remains shared across both profiles.
Natural individual payouts and a controlled alternate bag branch are distinguished.
The current development signature and identities remain unchanged; published
v0.7.0 still supports only barrel-1. No additional item, mode or prefab is implied.

[Gameplay D5](GameplayD5-Validation.md) separately expands only the replacement
item allowlist to wood on those same pinned Windows/Linux hosts. The record owns
physical identity/mod/creation/world/cleanup proof, actual public declarations,
failure/recovery/regressions and final-source closure. It changes neither Loot
signature nor prefab support, ownership cardinality, numerical budgets or modes.
Published v0.7.0 remains scrap/metal.fragments and barrel-1 only; no new release.

Gameplay C1 adds Player:GetInventory and read-only PlayerInventory CountItem/HasItem
to development API 0.6.5-experimental, not published package 0.5.0. Existing D18
physical rules, exact D11 connection and domain/publication lifetime remain;
[C1 evidence](GameplayC1-Validation.md) separates synthetic real-VM tests,
controlled live Carbon connections and authenticated-client claims. Inventory
Signals/loot policies are not implied. No native ABI/provider/schema/pin change.

[Gameplay C2](GameplayC2-Validation.md) is a private sampled inventory observation
candidate, not public ItemAdded/ItemRemoved qualification. Its exact host,
resource/lifecycle receipts and intermediate-state counterexamples are separate
from the unchanged C1 read-only support claim. C3 remains separately scoped.

[Gameplay C3](GameplayC3-Validation.md) adds sampled PlayerInventory.ItemChanged in
`0.6.5-experimental`, with immutable Before/After/Delta observations and no
completed-operation, every-action, attribution or history guarantee. C2's exact
host read/scanner rules and shared D23 quotas remain. Package/ABI/provider/schema/
Luau identities are unchanged; a release/tag is not authorized.

Use the narrowest levels that exercise the changed behavior, adding broader tests when the boundary requires them:

| Change | Relevant evidence |
|---|---|
| Documentation/policy | Check links, rule ownership, contradictions, diff scope and accuracy of existing evidence; no live-server rebuild required. |
| Native ABI/runtime | Native unit/ABI and negative-path tests on affected platforms, managed interop tests, then live Carbon for changed integration/lifecycle behavior. |
| Managed validation/configuration | Focused tests of accepted/rejected inputs; live Carbon when host APIs or lifecycle are affected. |
| Script sandbox/limits/reload | Adversarial failure and post-failure recovery tests as well as happy paths; integration/live fixture when claiming server behavior. |
| Host/platform/dependency upgrade | Candidate build identity, canonical smoke and affected behavior tests; actual provider validation for provider-specific claims. |

Revalidate final source when a subsequent implementation edit invalidates a result. Preserve evidence for unaffected code; do not rerun the entire matrix for a documentation fix. Compile-only CI does not prove live Carbon behavior. Label user-supplied logs, direct process inspection, CI and assumptions separately. A phase verdict states the tested scope, limitations and any blocked acceptance items; do not weaken a criterion to obtain PASS.

## Detailed validation criteria

The [qualification policy](qualification/Policy.md) is the delegated normative
evidence contract for the phases below. This page keeps support, identities,
upgrade policy and change-based test selection; historical results remain dated.

<a id="canonical-phase-0-fixture"></a>

- [Canonical Phase 0 fixture](qualification/Policy.md#canonical-phase-0-fixture)

<a id="phase-1-evidence-contract"></a>

- [Phase 1 evidence contract](qualification/Policy.md#phase-1-evidence-contract)

<a id="phase-2-and-phase-3-evidence-contracts"></a>

- [Phase 2 and Phase 3 evidence contracts](qualification/Policy.md#phase-2-and-phase-3-evidence-contracts)

<a id="foundation-a-evidence-contract"></a>

- [Foundation A evidence contract](qualification/Policy.md#foundation-a-evidence-contract)

<a id="foundation-b-evidence-contract"></a>

- [Foundation B evidence contract](qualification/Policy.md#foundation-b-evidence-contract)

<a id="foundation-c-evidence-contract"></a>

- [Foundation C evidence contract](qualification/Policy.md#foundation-c-evidence-contract)

<a id="foundation-d-evidence-contract"></a>

- [Foundation D evidence contract](qualification/Policy.md#foundation-d-evidence-contract)

<a id="foundation-e-evidence-contract"></a>

- [Foundation E evidence contract](qualification/Policy.md#foundation-e-evidence-contract)

<a id="foundation-f-evidence-contract"></a>

- [Foundation F evidence contract](qualification/Policy.md#foundation-f-evidence-contract)

<a id="foundation-g-evidence-contract"></a>

- [Foundation G evidence contract](qualification/Policy.md#foundation-g-evidence-contract)

<a id="gui-foundation-1-architecture-gate"></a>

- [GUI Foundation 1 architecture gate](qualification/Policy.md#gui-foundation-1-architecture-gate)

<a id="gui-foundation-1a-evidence-contract"></a>

- [GUI Foundation 1A evidence contract](qualification/Policy.md#gui-foundation-1a-evidence-contract)

<a id="gui-foundation-1b-evidence-contract"></a>

- [GUI Foundation 1B evidence contract](qualification/Policy.md#gui-foundation-1b-evidence-contract)

<a id="gui-foundation-1c-evidence-contract"></a>

- [GUI Foundation 1C evidence contract](qualification/Policy.md#gui-foundation-1c-evidence-contract)

<a id="gui-foundation-1d-evidence-contract"></a>

- [GUI Foundation 1D evidence contract](qualification/Policy.md#gui-foundation-1d-evidence-contract)

<a id="gui-foundation-1e-evidence-contract"></a>

- [GUI Foundation 1E evidence contract](qualification/Policy.md#gui-foundation-1e-evidence-contract)

<a id="gui-foundation-1f-evidence-contract"></a>

- [GUI Foundation 1F evidence contract](qualification/Policy.md#gui-foundation-1f-evidence-contract)

<a id="gui-foundation-1g-evidence-contract"></a>

- [GUI Foundation 1G evidence contract](qualification/Policy.md#gui-foundation-1g-evidence-contract)

<a id="gui-foundation-2-architecture-gate"></a>

- [GUI Foundation 2 architecture gate](qualification/Policy.md#gui-foundation-2-architecture-gate)

<a id="gui-foundation-3-architecture-gate"></a>

- [GUI Foundation 3 architecture gate](qualification/Policy.md#gui-foundation-3-architecture-gate)

<a id="player-interaction-foundation-1-architecture-gate"></a>

- [Player Interaction Foundation 1 architecture gate](qualification/Policy.md#player-interaction-foundation-1-architecture-gate)

<a id="worldentity-foundation-1-read-only-runtime-qualification"></a>

- [World/Entity Foundation 1 read-only runtime qualification](qualification/Policy.md#worldentity-foundation-1-read-only-runtime-qualification)

<a id="persistence-foundation-1-qualification"></a>

- [Persistence Foundation 1 qualification](qualification/Policy.md#persistence-foundation-1-qualification)

<a id="persistence-foundation-2-architecture-gate"></a>

- [Persistence Foundation 2 architecture gate](qualification/Policy.md#persistence-foundation-2-architecture-gate)

<a id="phase-0-consistency-review"></a>

- [Phase 0 consistency review](qualification/Policy.md#phase-0-consistency-review)

The current post-release [Gameplay D6 qualification](GameplayD6-Validation.md)
extends the two qualified barrel targets to one exact owner per prefab, with
two rules globally and per domain. Whole owner sets publish atomically, and both
targets share the existing physical producer budgets. D4/D5 receipts retain their
historical single-rule scope; published v0.7.0 remains immutable.

[Gameplay D7](GameplayD7-Validation.md) additionally qualifies only the simple
`stones` definition on the two existing targets. It preserves the D6 registry,
all shared resource/ownership/failure contracts and immutable published assets.

[Gameplay D8](GameplayD8-Validation.md) expands development support to the
[finite 221-item list](api/LootItems.md), with per-definition Windows/Linux
physical/public receipts and a complete discovery/exclusion ledger. Both barrel
prefabs, D6 whole-owner sets and numerical budgets remain unchanged. This
qualifies creation/drop defaults and exact cleanup, without expanding future
use/equip APIs or unrestricted definition support. No release/tag follows.
