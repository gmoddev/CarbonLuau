# Gameplay C/D supplied static research — preserved source — compatibility routing

This stable compatibility page routes to the complete dated
[record](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research). It is not a second contract or qualification receipt.
For current documentation roles, use the [documentation index](Index.md).

## Original sections

<a id="gameplay-cd-supplied-static-research--preserved-source"></a>

- [Gameplay C/D supplied static research — preserved source](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-gameplay-cd-supplied-static-research--preserved-source)

<a id="carbonluau--gameplay-c--d-static-architecture-research-and-decision-record"></a>

- [CarbonLuau — Gameplay C & D: Static Architecture Research and Decision Record](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-carbonluau--gameplay-c--d-static-architecture-research-and-decision-record)

<a id="0-executive-decisions"></a>

- [0. Executive decisions](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-0-executive-decisions)

<a id="1-repository-baseline-and-invariants-that-must-survive"></a>

- [1. Repository baseline and invariants that must survive](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-1-repository-baseline-and-invariants-that-must-survive)

<a id="11-current-capability-not-a-new-invention"></a>

- [1.1 Current capability, not a new invention](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-11-current-capability-not-a-new-invention)

<a id="12-hard-invariant-map"></a>

- [1.2 Hard invariant map](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-12-hard-invariant-map)

<a id="2-recommended-public-apis"></a>

- [2. Recommended public APIs](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-2-recommended-public-apis)

<a id="21-gameplay-c--player-owned-playerinventory"></a>

- [2.1 Gameplay C — Player-owned `PlayerInventory`](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-21-gameplay-c--player-owned-playerinventory)

<a id="22-gameplay-d--positional-immutable-loot-rules"></a>

- [2.2 Gameplay D — positional, immutable `Loot` rules](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-22-gameplay-d--positional-immutable-loot-rules)

<a id="23-player-break-callbacks-two-genuinely-different-requirements"></a>

- [2.3 Player-break callbacks: two genuinely different requirements](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-23-player-break-callbacks-two-genuinely-different-requirements)

<a id="3-exact-evidence-and-host-source-ledger"></a>

- [3. Exact evidence and host-source ledger](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-3-exact-evidence-and-host-source-ledger)

<a id="31-two-historical-host-tuples-must-not-be-conflated"></a>

- [3.1 Two historical host tuples must not be conflated](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-31-two-historical-host-tuples-must-not-be-conflated)

<a id="32-c-item-paths--evidence-versus-needed-proof"></a>

- [3.2 C item paths — evidence versus needed proof](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-32-c-item-paths--evidence-versus-needed-proof)

<a id="33-d-lootdrop-paths--evidence-versus-needed-proof"></a>

- [3.3 D loot/drop paths — evidence versus needed proof](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-33-d-lootdrop-paths--evidence-versus-needed-proof)

<a id="4-gameplay-c-candidate-architectures-and-disposition"></a>

- [4. Gameplay C: candidate architectures and disposition](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-4-gameplay-c-candidate-architectures-and-disposition)

<a id="c-a-read-only-facade-wrapping-d18--accept-first"></a>

- [C-A. Read-only facade wrapping D18 — **ACCEPT FIRST**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-c-a-read-only-facade-wrapping-d18--accept-first)

<a id="c-b-end-of-operation-verified-net-delta-journal--preferred-only-if-source-provable"></a>

- [C-B. End-of-operation verified net-delta journal — **PREFERRED ONLY IF SOURCE-PROVABLE**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-c-b-end-of-operation-verified-net-delta-journal--preferred-only-if-source-provable)

<a id="c-c-bounded-periodic-reconciliation--simple-honest-but-weaker"></a>

- [C-C. Bounded periodic reconciliation — **SIMPLE, HONEST BUT WEAKER**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-c-c-bounded-periodic-reconciliation--simple-honest-but-weaker)

<a id="c-d-patch-terminal-rust-inventory-mutators--investigate-high-fragility"></a>

- [C-D. Patch terminal Rust inventory mutators — **INVESTIGATE, HIGH FRAGILITY**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-c-d-patch-terminal-rust-inventory-mutators--investigate-high-fragility)

<a id="c-e-only-observe-carbonluau-givetake--safe-limited-alternative-not-a-drop-in"></a>

- [C-E. Only observe CarbonLuau Give/Take — **SAFE LIMITED ALTERNATIVE, NOT A DROP-IN**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-c-e-only-observe-carbonluau-givetake--safe-limited-alternative-not-a-drop-in)

<a id="c-decision-table"></a>

- [C decision table](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-c-decision-table)

<a id="5-gameplay-d-candidate-mechanisms-and-disposition"></a>

- [5. Gameplay D: candidate mechanisms and disposition](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-5-gameplay-d-candidate-mechanisms-and-disposition)

<a id="d-a-replace-at-loot-fill-decision-lootcontainerspawnloot--best-narrow-first-candidate"></a>

- [D-A. Replace at loot-fill decision (`LootContainer.SpawnLoot`) — **BEST NARROW FIRST CANDIDATE**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-a-replace-at-loot-fill-decision-lootcontainerspawnloot--best-narrow-first-candidate)

<a id="d-b-replace-at-destructions-last-complete-payout-decision--ideal-ux-high-host-proof-gate"></a>

- [D-B. Replace at destruction's last complete payout decision — **IDEAL UX; HIGH HOST-PROOF GATE**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-b-replace-at-destructions-last-complete-payout-decision--ideal-ux-high-host-proof-gate)

<a id="d-c-override-droputildropitems-plus-bonus-specific-prehooks--multi-seam-conditionally-viable"></a>

- [D-C. Override `DropUtil.DropItems` plus bonus-specific prehooks — **MULTI-SEAM, CONDITIONALLY VIABLE**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-c-override-droputildropitems-plus-bonus-specific-prehooks--multi-seam-conditionally-viable)

<a id="d-d-use-gameplay-deathdestruction-signals-and-playergiveitem--reject-as-drop-replacement"></a>

- [D-D. Use gameplay death/destruction Signals and `Player:GiveItem` — **REJECT AS DROP REPLACEMENT**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-d-use-gameplay-deathdestruction-signals-and-playergiveitem--reject-as-drop-replacement)

<a id="d-e-mutate-global-rust-loot-tablesdefinitions--reject-for-initial-d"></a>

- [D-E. Mutate global Rust loot tables/definitions — **REJECT FOR INITIAL D**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-e-mutate-global-rust-loot-tablesdefinitions--reject-for-initial-d)

<a id="d-f-arbitrary-dynamic-luau-callback-when-hitbroken--reject-synchronously"></a>

- [D-F. Arbitrary dynamic Luau callback when hit/broken — **REJECT SYNCHRONOUSLY**](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-f-arbitrary-dynamic-luau-callback-when-hitbroken--reject-synchronously)

<a id="d-decision-table"></a>

- [D decision table](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-d-decision-table)

<a id="6-internal-integration-minimal-changes-maximum-reuse"></a>

- [6. Internal integration: minimal changes, maximum reuse](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-6-internal-integration-minimal-changes-maximum-reuse)

<a id="61-filesmodules-to-extend-only-after-phases-are-authorized"></a>

- [6.1 Files/modules to extend only after phases are authorized](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-61-filesmodules-to-extend-only-after-phases-are-authorized)

<a id="62-listener-identity-and-publication-details"></a>

- [6.2 Listener identity and publication details](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-62-listener-identity-and-publication-details)

<a id="7-lifetimes-bounds-timing-and-ordering"></a>

- [7. Lifetimes, bounds, timing and ordering](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-7-lifetimes-bounds-timing-and-ordering)

<a id="71-c-workresource-contract"></a>

- [7.1 C work/resource contract](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-71-c-workresource-contract)

<a id="72-d-ruleresource-contract"></a>

- [7.2 D rule/resource contract](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-72-d-ruleresource-contract)

<a id="73-failure-taxonomy-applies-to-both-with-different-exposure"></a>

- [7.3 Failure taxonomy (applies to both, with different exposure)](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-73-failure-taxonomy-applies-to-both-with-different-exposure)

<a id="8-remaining-static-research-work-exact-questions-and-stop-conditions"></a>

- [8. Remaining static research work: exact questions and stop conditions](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-8-remaining-static-research-work-exact-questions-and-stop-conditions)

<a id="81-c-sourceil-audit-order"></a>

- [8.1 C source/IL audit order](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-81-c-sourceil-audit-order)

<a id="82-d-sourceil-audit-order"></a>

- [8.2 D source/IL audit order](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-82-d-sourceil-audit-order)

<a id="83-proof-record-format-for-each-promoted-source"></a>

- [8.3 Proof-record format for each promoted source](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-83-proof-record-format-for-each-promoted-source)

<a id="9-qualification-test-matrix"></a>

- [9. Qualification test matrix](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-9-qualification-test-matrix)

<a id="91-gameplay-c--controlled-host-and-runtime"></a>

- [9.1 Gameplay C — controlled host and runtime](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-91-gameplay-c--controlled-host-and-runtime)

<a id="92-gameplay-d--controlled-host-and-runtime"></a>

- [9.2 Gameplay D — controlled host and runtime](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-92-gameplay-d--controlled-host-and-runtime)

<a id="10-separate-phased-roadmaps-and-decision-gates"></a>

- [10. Separate phased roadmaps and decision gates](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-10-separate-phased-roadmaps-and-decision-gates)

<a id="gameplay-c"></a>

- [Gameplay C](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-gameplay-c)

<a id="gameplay-d"></a>

- [Gameplay D](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-gameplay-d)

<a id="which-phase-first"></a>

- [Which phase first?](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-which-phase-first)

<a id="11-final-architectural-verdict"></a>

- [11. Final architectural verdict](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-11-final-architectural-verdict)

<a id="12-source-index--pinned-repository-and-public-host-evidence"></a>

- [12. Source index — pinned repository and public host evidence](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research-12-source-index--pinned-repository-and-public-host-evidence)
