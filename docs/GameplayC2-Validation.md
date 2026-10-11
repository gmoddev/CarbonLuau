# Gameplay C2 — private inventory net observation

Status: **GAMEPLAY-C2 PARTIAL — SAMPLED OBSERVATION ONLY**.
Starting main: `e7b230963ba583213fcad8620c7c6306bed3a174`.
This record does not announce public inventory Signals.

Historical C2 disposition is preserved. The subsequent explicitly authorized
[C3 combined qualification](GameplayC3-Validation.md) accepts sampled ItemChanged,
not the rejected completed-operation guarantee. C3 owns current public status and
the publication of the preserved C2 implementation/evidence.

## Contract and limitations

D18 owns physical acceptance, D11 original connections, D13 nontransactional
mutation, and D7/D10/D23 existing publication/deferred delivery. The public
[C1 facade](GameplayC1-Validation.md) is unchanged. Preserve the
[C/D research](GameplayCDArchitecture-Research.md), including rejected raw-hook
and intermediate-transfer interpretations.

The private candidate samples opt-in exact connections on the existing owner-frame
pump. Two accepted main/belt/wear observations produce bounded, ordinally ordered
signed `{Item, Before, After, Delta}` records. Snapshots retain only strings and
integers, never Item/container/ItemDefinition/Unity objects. No hook is treated
as a completed transaction or causal receipt.

Completed same-Player moves/splits/merges yield zero when samples bracket the
complete operation. An intermediate sample can observe temporary loss/restoration.
Equal gain/loss between samples is invisible. Sampling is not an atomic inventory
snapshot under trusted in-process interference and guarantees neither every
operation nor cross-Player atomicity or attribution.

`PhysicalInventoryObservation.Snapshot` shares D18 preflight/acceptance with
Count/Has and resolves canonical names to the exact original ItemDefinition.
It is not a second inventory implementation. Invalid labels, arithmetic overflow,
excess stacks, failed reads or allocation failure invalidate the interval and
require a fresh baseline. Never bridge that interval with a fabricated delta.
Accepted baseline advances even when record retention is exhausted; no rewind,
catch-up or replay.

## Ownership and publication

Private demand binds the defining FacadeSession/publication witness and original
D11 token/UserId. Candidates/open modules do not sample before publication.
Failed scopes reclaim capacity synchronously; owner Clear cancels immediately.
Failed replacement preserves the old committed demand. Newly committed demand
gets no pre-subscription interval. Reconnect needs a new baseline, never retargets.
Private consumption removes a record before current lifetime/owner validation.

C2 has no native event kind, Luau callback root, Signal, metadata or public binding.
`Consume` is private qualification intake, not a scheduler. C3 must separately
implement targeted listeners, public staging, D23 reservations/fanout/native
admission and deferred callback delivery. This candidate does not qualify those
future public mechanisms by proxy.

## Private policy bounds

Policy owner: `InventoryNetObservationPolicy`; production/tests share constants.

| Resource/work | Hard candidate cap |
|---|---:|
| Original Player connections | 64 |
| Owner/publication demands, including staged | 128 |
| Complete scans / actual Unity frame | 4 |
| Direct entries / scan; names / snapshot | 128 |
| Stack inspection allowance / frame | 512, charged before reads against D23 shared 4096 visits |
| Union names / comparison | 256 |
| Pending private records | 256 |
| New records / frame | 32 |
| Reserved scalar snapshot charge / watch | 41,088 bytes |
| Retained/scratch policy envelope | 4 MiB; not process/GC/RSS cap |

64 watches follows the original reconciliation target. Four scans conservatively
use at most one eighth of shared gameplay visits before fanout. Under an
uncontended budget, 64 committed watches are revisited within 16 pumps; this is
not a wall-time SLA. Busy runtime/backpressure/invalid samples can delay or
discard intervals. There are no per-Player timers/unbounded dirty/history queues.
Fixed arrays bound lookup/pruning/cancellation. Fanout rotates across demands;
retained sequence orders records. Shared visit exhaustion stops fanout.

Sorted insertion uses at most 128 squared bounded ordinal comparisons/shifts per
scan (128 ASCII characters/comparison). Difference takes at most 256 merge steps.
PlayerDirectory's existing population bound still applies to host lookup. Reserved
charge covers bounded scalar strings/arrays/control records, with scratch and
actual memory observations reported separately. Counters saturate; no stored
values/keys or unbounded history are logged. Unavailable frame pump rejects demand.

## Host source and negative evidence

[Exact-host research](GameplayC2-HostResearch.md) records 24 selected methods per
platform, direct amount writers, hook classifications, static/publicizer provenance
and its distinction from loaded evidence. Rust build 25653776/protocol 2634.289.1;
Windows Carbon 2.0.262.0/8a81d70, Linux 2.0.261.0/c74c4ca, Harmony 2.4.2.0.
Selected shape parity is not a universal inventory transaction theorem.

Actual loaded IsValid is token `0x06006a4a`, 17 bytes, nonvirtual, no locals/EH/calls;
hash `477480dbc895bfd4d0b69e6660104e5c98b23c6bebd9ae0c01182382da848951`.
IsRemoved token `0x06006a3b`, 14 bytes, hash
`d80e2530bb0ad5c9dd617a1e19ac1370be37b6e7e0c4416b3f653645ca058a38`.
Both have zero prefix/postfix/transpiler/finalizer patches in the controlled live
tuple. The scanner reads parent/info/amount directly. Loaded mutator bodies and
topology are recorded separately; no mutation hook is promoted into a producer.

The live fixture deliberately demonstrates transfer `-80/+80` and split `-30/+30`
intermediate observations, with zero net after full completion. A callback changes
a requested +5 into sampled +12, disproving causal attribution. Direct writes and
balanced +10/-10 show both lasting detection and the sampled blind spot. Debouncing
or a quiet frame is not proof arbitrary plugin-held splits completed.

Crafting and wider weapon/recycler paths have static evidence, not newly exercised
live gameplay/client claims. Controlled item creation/transfer/drop receipt is not
an authenticated pickup RPC. No authenticated client is claimed.

## Qualification receipts

Windows/Linux focused managed/model/real-VM candidate tests PASS, including 10,000
mixed churn cycles, staged/nested rollback, same-account reconnect, root/addon and
provider retirement, failed replacement, fatal recovery, slot reuse, invalid reads,
allocation faults, no replay, 64-watch/128-demand caps, 32-record/256-pending caps,
100-addon sharing, shared-budget saturation and cross-Player fairness. Exact 32
changes and exact 128-inspection-plus-one-record boundaries have regressions.

Independent review found and closed definition-label spoofing, rollback slot
retention, visit-exhaustion fanout, unavailable-pump admission, zero-handle pending
underflow and busy-Player starvation (including exact-limit exhaustion).

Worst-label deterministic managed observation (not Carbon server timing): 64
watches, 128 demands, 128 stacks with 128-byte labels; 80 turns/320 scans.
Windows mean/max 0.270/0.301 ms, reachable managed delta 721,608 bytes; Linux
0.407/0.584 ms, delta 974,656 bytes. Charged scalar/scratch bytes 2,703,488 on
both. These are observations, not execution-time/RSS guarantees.

Native regression: Windows 23/23, Linux 24/24; Linux ASan/UBSan/leak 24/24 PASS.
Native source/binaries are unchanged from B2. New managed source is separately
compiled/tested; sanitizer results are not a claim of managed heap instrumentation.
API/architecture, deterministic package exclusion, metadata generation/drift,
tooling/goldens and tooling contracts PASS. No public metadata/definitions changed.

Final-source full Windows/Linux managed/runtime regressions PASS: Foundations A-G,
Player Interaction, GUI, addon/provider, publication/module, scheduling/recovery,
parser/package and loader paths. Final clean bundle installs PASS on both, including
compilation of 68 existing examples and GUI/Player execution/teardown. Default
packages contain 85 production sources and no C2 fixture/research/database state.

The first live attempt was a fixture compile failure: this target's container
delegate is `Action<Item,bool,BasePlayer>`, not the historical two-argument guess.
Its Windows/Linux logs remain under `inventory-c2-20261011-155228-504` and
`inventory-c2-20261011-155239-1762504`; cleanup restored all installed hashes.
An early managed transfer also preceded test-signature synchronization; its failed
build logs remain, not relabeled PASS. Missing native-source context in the first
tooling staging run was corrected before the successful deterministic check.

Final tested production is the intentionally **uncommitted** working source over
`e7b230963ba583213fcad8620c7c6306bed3a174`, not a new Git SHA. The identical
Windows/Linux qualification cszip (85 production sources plus one isolated fixture)
is SHA256 `1147755b9904ba392d5bfde5ec3571cf46820197344c6e641e3a0302ab83b4e4`.
The final observer source SHA256 is
`7b4b3dc2ff8d6db7549c32a2a22392e81c7f994efe75ac5d3f3deef348880d83`.
Later evidence/routing changes do not change those production bytes.

| Controlled final-source receipt | Windows | Linux |
|---|---|---|
| Server log | `D:\Sandbox\Codex\Entity1AStartup\evidence\inventory-c2-20261011-161035-920\server.log` | `/root/codex/world-movement-20261003/evidence/inventory-c2-20261011-161046-1767238/server.log` |
| Server log SHA256 | `9672c4bacf702b206ba3d771977c0cc50b87753da8c10964cd595f56430b8df2` | `b6b1831761b8a23dacfa3664ef5a65cff55bbe3d3d7ec37d4f80a1aaa882ae5a` |
| Native SHA256 (unchanged B2) | `4c4fb6aef7c95c3a8ae02ce669e9df9ff6ec126f3d0e8f00756c41cf8bbaab32` | `b353d6af130af96ceb6a30365e0f43036d689787ae7f15f28805d307233b2fff` |

Both live fixtures PASS loaded read pins/topology, completed transfer/stack zero
net, Use/Take/Remove, direct amount changes, world drop/receipt, callback interference,
root replacement, reconnect, fatal recovery, fresh baselines and cleanup. Runners
use established loopback-only servers, existing caches and at most two build jobs.
Quit/save/shutdown receipts precede expected Rust process termination; every prior
package/native/compiler/hook/config hash is restored. No qualification server or
listener remains. First-attempt failures and subsequent intermediate receipts are
preserved separately, never substituted for this final source.

Task-owned build roots: `D:\Sandbox\Codex\GameplayC2` and
`/root/codex/gameplay-c2-20261011`. Focused receipts are
`inventory-net-qualified.log`; final full regressions/clean installs use
`full-runtime-qualified.log` and `clean-install-qualified.log`. Native and
sanitizer logs are in the reused B2 native caches. Metadata/goldens receipt is
`/root/codex/gameplay-c2-20261011/tooling-d.log`. Local artifact copies are under
`C:\Users\aiden\AppData\Local\Temp\CarbonLuau-C2-20261011`.

Hosted final-source CI is **not run**: the task permits commit/push on C2 PASS,
but complete net-operation observation did not qualify. Workflow adds a private
candidate test stage for a later authorized publication. No earlier B2 hosted
run is relabeled C2 evidence. Full VSIX/LSP provisioning is unaffected and not
newly claimed; metadata/tooling/preview-golden tests and contracts are exercised.

## C3 handoff and repository disposition

Without a complete terminal fence, recommend separately scoped
`PlayerInventory.ItemChanged` with explicit **sampled Before/After/Delta** semantics,
not stronger operation-level ItemAdded/ItemRemoved guarantees. No C3 implementation
or public documentation/definition is added here.

API stays `0.6.5-experimental`, package `0.5.0`, ABI `1.5`, provider `1.2`, schema
`1`, Luau pin unchanged. No release/tag. Commit/push is withheld unless the C2
PASS gate is met. Pre-existing untracked evidence remains preserved.

At handoff, `main` and `origin/main` remain at the starting SHA; tested private
candidate, tests/checker and research/evidence/routing edits remain intentionally
uncommitted. `.codexlock` and `derived-369547800317500/` are preserved. No new commit
metadata/private email exists. All owned agents, test containers, workers and
disposable servers ended; caches and non-secret receipts remain for continuation.
