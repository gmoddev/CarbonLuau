# Gameplay D6 — bounded per-prefab Loot registry qualification

Status: **LOCAL IMPLEMENTATION/QUALIFICATION PASS; HOSTED CI PENDING**.
Starting main: `1f334addad235454fb2499cec00063e2b7d70b8e`.
Date: 2026-10-13. [D2A](GameplayD2A-Validation.md),
[D3](GameplayD3-Validation.md), [D4](GameplayD4-Validation.md),
[D5](GameplayD5-Validation.md), [D23](invariants/Gameplay.md),
[D20](invariants/WorldEntity.md) and D7/D10/D13/I12 remain authoritative.
This implements the separately authorized [D6 proposal](design/GameplayD6-PrefabRuleRegistry.md)
without repeating qualified prefab/item/interception research.

## Registry and publication contract

The API is unchanged: `Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)`.
Only the existing exact autospawn barrel-1/barrel-2 paths (IDs 966676416 /
555882409) and scrap/metal.fragments/wood definitions are supported. Registration
remains copied, initialization-only and staged; it is not a payout receipt.

One active owner per prefab, at most two active rules globally, and at most two
distinct rules per domain. One root/addon can declare both, or disjoint targets
can belong to different domains. A repeated same-prefab declaration rejects
deterministically before definition lookup, including identical entries. It does
not replace the earlier declaration. Foreign ownership conflicts only on an
overlapping key. Empty/all-zero descriptors still own their target.

Successful root/addon generation replacement publishes its **complete set**
together. Redeclare every rule to retain, omit a rule to withdraw it. Failed
candidate/native/managed publication leaves the previous committed set; there
is no half-published descriptor pair. Caught module failure restores the earlier
staged set under D7, not arbitrary Lua memory. A caught rejected declaration
leaves earlier valid declarations staged; only the successfully initialized
complete set is published. Fatal VM retirement invalidates every old owner and
reconstruction declares committed source without physical replay.

Preflight inspects the entire candidate mask and every committed owner, excluding
only the exact outgoing session reference. Ordinal two-key lookup selects one
immutable descriptor and exact defining owner. Invalid/key-mismatched/equivocal
state fails closed instead of choosing an enumeration-order winner. Retirement
withdraws only exact owned keys; another owner's target remains published.
There is no new global registry object, policy scheduler, hook topology, dynamic
prefab discovery, mode, callback or raw host object in the public API.

## Hard combined retention and work bounds

Existing limits remain eight entries per rule; integer amounts 0..1000;
sum(Max)<=8000 **per rule**; eight worlds per occurrence; eight admitted
occurrences per actual Unity frame **globally**; one in-flight frame globally;
129 inspected sessions; 64 array / 64 hash backing slots per declaration table;
24 scalar descriptor fields / 1024 UTF-8 transport bytes per call. A second
rule does not create a second producer budget or reset use on publication.
Worst-case admitted work remains 64 worlds / 64000 units per frame, not twice
that because there are two targets. Successful payouts still require the
existing physical verifier and certified original source removal.

The managed host serializes one prepared root/addon candidate through its
existing Busy admission. The new private accounting additionally enforces at
most **three rule-bearing facade sessions**, including committed, candidate
and outgoing lifetimes. A fourth first declaration rejects before item lookup.
Reservation acquisition is repeated after trusted callbacks; failed empty
staging, rollback to empty and Clear/retirement release it exactly once. Rule-free
sessions do not consume this reservation and still use the existing session cap.

Each charged session has at most two distinct immutable descriptors. Registration
only adds a previously absent key; checkpoint maps are subsets of the current
set. Rollback restores a subset and pops descendants. Therefore the existing
64 publication levels share at most two descriptor identities per session,
not 128 descriptor copies. The hard owned maximum is **six descriptors / 48
entries**, at most **195 charged-session maps / 390 dictionary entries and descriptor references**
including current maps. FirstPublication aliases the oldest checkpoint; it is
not another copy. Dictionary keys share the same bounded prefab strings.

Retained descriptor scalar strings are at most
`6 * (67 prefab characters + 8 * 15 item characters) = 1122 UTF-16 code units`,
plus 96 Int32 amount fields (384 bytes). At most six entry arrays, six descriptor
objects, 54 scalar string objects, 195 charged-session map objects and 390 bounded
map nodes are reachable through the rule-bearing registry/checkpoint graph;
comparer objects are shared. Existing rule-free facades also have empty maps,
bounded separately by the unchanged domain/publication lifetime limits below.
These are hard object/payload accounting bounds, not a claim about CLR allocator
overhead or whole-process RSS. The VM heap remains separately accounted globally.

For completeness, the unchanged 128-addon cap, one current root and one Busy
candidate bound current/candidate facade graphs to 130. Including rule-free
empty maps, these graphs retain at most `130 * (1 + 64) = 8450` current/checkpoint
map objects; the 195 charged-session maps are a subset, with at most 390 nonempty
dictionary entries across all these graphs. An empty current rule map implies
empty surviving checkpoints under the same add-only/rollback-subset invariant.
One borrowed retired physical owner after completed Clear may separately retain
one empty current map, but no checkpoint descriptors. One attempted checkpoint
copy is a bounded transient. These are reachable owned graph bounds rather than
cumulative allocations retained by GC or arbitrary external managed references.

The existing one physical frame can separately borrow **one** descriptor after
its owner retires. One serialized validation may temporarily retain one new
descriptor and bounded input/copy/transport arrays. Thus owned descriptors,
one physical borrow and one validation descriptor total at most eight descriptor
identities / 64 entries at an operation boundary; temporary arrays and one
attempted checkpoint copy are separately bounded by the same entry/depth limits.
No unreturned host allocation, GC timing, rollback or process-memory cap is inferred.
The transient validation bound describes the qualified production path: existing
Busy admission serializes VM declarations, ReadItemDefinition performs exact
ItemManager lookup, and the source gate checks pinned observer/patch state.
Arbitrary recursion injected by another equally privileged managed component
does not acquire a registry-wide process-memory guarantee under I12.

## Physical source and preliminary receipts

Pinned host tuple is unchanged: Rust 25653776 / protocol 2634.289.1 / revision
166494, Windows Carbon 2.0.262.0 (`8a81d70`), Linux 2.0.261.0 (`c74c4ca`),
Harmony 2.4.2.0 and the D5 backing hashes. The three production interception
targets, D20 lifetime observer, physical Item/world verifier and cleanup code
are untouched. Native executable bootstrap is byte-identical; only its canonical
API annotation summary changes. API/package/ABI/provider/schema/Luau identities
and published v0.7.0 tag/assets remain unchanged; there is no release or tag.

Initial exact-admission-prefab fixture package SHA256:
`7ae8b014f87c9dddbcd8f1b6bbd52a04635f876c1fffd965626e9e34d9986e09`.

| Host | Actual D6/B2 receipt before the final exact-retirement guard | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261013-070617-857/server.log` | `220ae768aa872fee1b03ae1c47f32de38c82fbac9dcd12ed81e316771a69adf6` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261013-070746-1890447/server.log` | `b2d6f8422d9553d37e035638573be68a0a06855d8ae86868263c15061ee613ba` |

These PASS simultaneous same-root and independent root/addon rules, interleaved
destruction, full-set replacement/rollback/withdrawal/conflicts, shared-frame
exhaustion across both targets, actual stored/bonus/controlled bag suppression,
original Item/UID/world reciprocity, source veto, incomplete Spawn, partial
creation and no fallback/replay/compensation. The changed rule owner never
changes physical ownership: accepted worlds remain host-owned. Final combined
B1/B2/B3/RPC/terminal controls PASS and temporary/registry resources converge.
Natural bag configuration is not claimed. Final-source reruns follow the small
common exact-owner correction below; these receipts are not relabeled final.

## Final guarded physical matrix — both hosts PASS

These runs include both owner/identity corrections below and the strengthened
separate-owner shared-frame exhaustion case. The current FacadeSession source
SHA256 is `8f2b27ad2e3f8676c191fe0ef57608ef96ff1548f8093ae6554a6823591cdefd`.

| Host | Final actual D6/D5/D4/B2 receipt | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261013-073129-855/server.log` | `e09acf4412b15a4494cc39bcb519ef9bf837d793566701f08eb427b71f550374` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261013-073212-1895381/server.log` | `6e4e00e8673fc0653d2fd5124d521f09db6b40677bc50704b6511bbe3598ef3c` |

Windows fixture package SHA256 is
`c347dbc36971cb7338f997ee301c874e3224ac7aac128ae0dbd0273fc1316ff1`;
Linux package is `469b0614daf813df23c3fe85fb6bf4cf7c932e0fa6f0c4e5562a9dfd77ac202d`.
Packages contain current guarded production sources and test-only controls;
ordinary production packages exclude all fixtures and fault seams.
All 95 ZIP entry payloads are byte-for-byte identical across these host packages;
there are zero raw or normalized-content differences. The parity report is
`/root/codex/gameplay-d6-20261013/guard2-package-parity.json`, SHA256
`42c86eea29841ae895bc6e9779760be47f7f58451fcadf0f00790698de1c9ab2`.
The outer archive digests differ; cross-platform archive byte identity is not
claimed. Repeat determinism is tested separately within each build environment.

Both actual hosts PASS two simultaneous root-owned rules, separate root/addon
owners, whole-set failure/replacement/omission, conflicts and duplicates, caught
module rollback, provider unload, fresh VM reconstruction and exact defining
owner selection. Same-root and separate-owner cases each consume the unchanged
global eight-occurrence actual-frame budget, then replace an owner while use
remains eight and verify that excess occurrences on both targets preserve vanilla.
Neither publication nor owner changes replenish the budget.

Interleaved sources retain their original lifetime and stored-item identity.
Stored, bonus and controlled bag payouts are suppressed at the existing common
cut; planned original Item/UID/world reciprocity and completed Spawn witnesses
are verified per occurrence. Ineligible PREPARE leaves vanilla; after COMMIT,
partial creation/Spawn/source-veto/amount uncertainty produces the existing
partial or indeterminate result without fallback, replay or compensation.
Accepted original worlds remain host-owned. Final cleanup converges, old host
disposal and native release leave zero retained rule sessions, and prior installed
package/native/compiler/hooks/config hashes are restored after owned process exit
(Windows -1; Linux 137). Natural bag selection and authenticated-client behavior
are not claimed.

## Exact-owner defect and preserved evidence

Targeted D6 source review found an inherited managed seam: Retire checked only
whether an addon numeric DomainLifetimeId existed; the exact outgoing-owner check
in CommitAddon was conditional on the incoming candidate having Loot rules.
A synthetic stale session with the same numeric ID could therefore withdraw a
different active session through this seam. Native IDs are not reused on the
qualified ordinary path and no live exploit/interference is claimed.

The new managed regression reproduced that reference mismatch before correction:
`D:/Sandbox/Codex/GameplayD6/lootpolicy-stale-retirement-before-fix.log`, SHA256
`8c20353ebd92d36a4db63d44dda51744e22c43921d9b91410be9e60584c86bf7`.
Retire now removes an addon only when its stored reference equals the requested
session. CommitAddon validates every nonnull outgoing reference, including
withdrawal and rule-free replacement. A stale candidate may clear itself without
retargeting another owner. This is the smallest D6 lifetime correction, not a
generic coordinator or a new host authority model. Relevant final managed/live
regressions must pass on this corrected source.

Independent review also found that CommitAddon's duplicate incoming domain-ID
check occurred after registrar publication and outgoing-owner cleanup. A
synthetic inactive candidate sharing a disjoint foreign owner's numeric ID
could pass prefab preflight, then reject after disturbing the outgoing owner.
The existing check now runs before publication or cleanup, excluding only the
exact outgoing reference. The new model checks unchanged registrar calls,
both committed descriptor/owner references, candidate staging and three charged
sessions on rejection, then verifies exact same-outgoing-ID model replacement.
Native monotonic IDs exclude this alias path in ordinary live operation;
neither a live exploit nor external interference is inferred. Its original
negative log `D:/Sandbox/Codex/GameplayD6/lootpolicy-candidate-id-before-fix.log`
has SHA256 `871a991df4a2e03ea96218a1d3a4e505c4923be4c2a2a6feb2c786af1a76aa09`.
Both corrected alias regressions PASS on Windows; final Linux/physical receipts
are attributed separately below.

Earlier D5 fixture and hosted CI negatives remain intact in the D5 receipt and
immutable CI attempt logs. No deadline, callback assertion or resource limit is
relaxed to obtain D6 qualification.

The first final-source Linux Persistence-2D restart run also remains negative:
scheduled-1 and scheduled-3 exceeded the unchanged 3-ms callback deadline and
retired the VM. RootRead subsequently passed, but provider-lifecycle assertions
failed after that retirement. Receipt
`/root/codex/gameplay-d6-20261013/linux-live-2d-20261013-113933/` has server-2 log
SHA256 `d6e7547681438ffb42f0ef3cbb5413e7f73a1c2ffef27c05aaee0a9d64fb3ca2`
and failure log `efeeba0cb72d5c7b6ca6b25f9b299cabc4c00931600deac3a37fdb953f8d1105`.
The existing 3-ms callback / 5-ms frame budgets are unchanged. ASan was running
concurrently, but this does not establish an interference cause. The clone and
all owned processes were restored/stopped; an identical isolated rerun is
attributed separately, with no assertion or deadline relaxation.

## Native, managed and affected regression receipts

Qualification work roots are `D:/Sandbox/Codex/GameplayD6` (existing approved
Windows D: override) and `/root/codex/gameplay-d6-20261013`. Sustained builds use
two-way parallelism and existing dependency/toolchain caches; container builds
use two CPUs / two GiB and the established isolated-server four-GiB allowance.
No host security, timers, unrelated workloads or toolchains are changed.

Native Release suites PASS Windows 23/23 (98.76s), Linux 24/24 (116.48s).
Native C++ and generated executable bootstrap inputs are unchanged by D6;
the bootstrap annotation changes only the generated public summary. Focused
policy/public real-VM suites PASS both platforms, including both numeric-alias
controls, full two-key publication/rollback, maximum copied descriptors, real
allocation exhaustion and fresh VM recovery. The 100-addon test has two distinct
Loot owners among 101 domains in one VM; other callbacks cannot declare after
initialization, recovery reconstructs both fresh owners and provider unload
converges to zero. No VM allocation cap or test assertion is relaxed.

Final Windows full real-VM suite PASS (123.60s), including 100 atomic replacements
and 200 failed candidates. Linux final full real-VM and Persistence-1C lifecycle/
scale suite PASS, including 12 reopen cycles. Actual Carbon regressions are
separate from these model/real-VM checks:

| Linux final guarded live regression | Receipt under `/root/codex/world-movement-20261003/evidence/` | Log SHA256 |
|---|---|---|
| Standalone D5/B2 | `loot-d2a-20261013-073359-1895689/server.log` | `a34fce938795fb84fa95f7a5de8243979da69351e6cc789a982d6736fb23a4c1` |
| C3/C4 | `inventory-c3-20261013-073526-1896030/server.log` | `c02eef85e07ad87ffe7e550cad3774c1aa752be1108a5211739a2ad9b43599b1` |
| Gameplay A | `gameplay-host-20261013-073619-1896269/server.log` | `03308766c1534c79a1897bc1defa6c1faf34c60ab97016191090017a670e5de7` |

These live runners restore installed-file hashes after owned process exit 137;
Gameplay A additionally restores 20 database/world files. The final combined
D6 matrix above also contains the inherited complete D3/D4/D5 and B1/B2/B3
physical/callback/terminal controls. Remaining GUI/restart/tooling/package and
sanitizer receipts follow when complete.

| Windows final guarded regression | Receipt under `D:/Sandbox/Codex/` | SHA256 |
|---|---|---|
| Full real-VM | `GameplayD6/fullruntime-windows.log` | `22a0b1badb7a52e06596d4ddc83c6593a083ea73e38cb4d27b3c4e188618544b` |
| Persistence-1C (313.49s) | `GameplayD6/persistence1c-windows.log` | `857adae23e95b6236e7fa1be899da49d558b4a173e80387b9e27c1364cae750a` |
| Gameplay A | `Entity1AStartup/evidence/gameplay-host-20261013-073537-140/server.log` | `c6168e6ff9b0785caeb2659e49e97e734d16e8688384821a85bb357fddfe8400` |
| C3/C4 | `Entity1AStartup/evidence/inventory-c3-20261013-073938-979/server.log` | `fda4052e389cb48386a3ba39660e1cb516f3b45f555f72f47c7713eb07d15052` |

Windows namespace-only actual restart receipt is
`D:/Sandbox/Codex/GameplayD6/PersistenceEvidence/persistence1b-20261013-073949-0be0036f/`:
server log `f32ee35b88436f68c9fdad90eb883dee7fd9a80fdb5b45796f9882dc93babf55`,
restart log `dfda08baa3f1207187b51446ce9412bccb429d14ae300df600f84d16daa22774`,
restart proof `e5665fb0d59b729baa37276c6bfb5411c1eb4bf4de2ccdad70e1d6d9e9336f00`.
The first process seeds 8/4/2; the second proves read-only 3/0/0. Both owned
processes exit -1, helpers return to zero and FinalNativeUnmapped=True. Legacy
trees are restored and backups/runner leases are absent. Final Windows actual
D6/A/C3 and persistence tests leave no Rust, RuntimeTests, compiler or storage
process. Gameplay A restores all 20 original database/world files.

Windows repeated development-bundle SHA256 is
`a9de28e4fddbf6f6bacbc600a173205dace1c80e242ebb10b233f7e23b7d3828`.
Clean installation and all 72 extracted examples/worker checks PASS. This is
local deterministic development packaging, not replacement of published assets.

Windows native library/compiler/storage SHA256 respectively:
`95b5f7b45546fb565b193586c4ae0199150cfe12be0eb7b4c9516ec05d7175d3`,
`16718cd80cc3e72e0b74a1d16d79514ef8967e156c56ad099cd20643bcca4de7`,
`d54edbcef5464af9da81f1cebf13ef31bd25a8e3a9198e753a6f201d59327372`.
Additional Windows work-root receipt digests are native suite
`ca9a333141a4a76befb8ce6e269b0541dee892908086999fd02b34f9fd09e31e`,
100-cycle loader `34402d0f03f44d166594de8fe93f52b60841bf37c406776887cd281c0d335ddf`,
private worker/lost-ack/derived suite
`2a9fe2612472fcc35892556365823f09ff365ef7ec7dcf97203e53b5b027f456`,
clean installation/72 examples
`70c41a63e1d0188e9d1b30dfb37fe67fbdf798239aa62c45f9ac53215c5abb2c`,
and extracted storage qualification
`b879d4b1e7b11190e1e27136e4da12ecfdf1e8f97996ee796fb8b907de44bf96`.

Linux actual GUI regression passes ten root reloads and ten Carbon unload/reloads,
exact GUI counts, native unmap and compiler exit. Evidence root is
`/root/codex/gameplay-d6-20261013/gui-borrow-20261013-073718/`; runner receipt
SHA256 `03f0e6e99a81af0268750bd4a651e6c7d36add824a39fa42fbfc93cec8446f13`,
cleanup `2fdd77ff2d81f485debb12e7b947c03adfe86bd9c3c617e3c81fe4f84ab7ba52`.
The borrowed existing clone restores all 183 file hashes and UID/GID/modes.
This is server GUI lifecycle proof, not an authenticated-client rendering claim.

Linux ASan/UBSan/leak/lifecycle/allocation-fault native suite PASS 24/24 (285.31s),
work-root log SHA256
`fead8e44f91c84d56d3ed9a7ffdee1b7e3f8592caeb4d5918f8384b157c6edc3`.
Final generation/type/drift/schema/17 preview goldens PASS; tooling log
`1c4d4989f2effe3cd92b4a407b97ebe99fbde19eeb9703951ebce8763bd607e6`.
Repeated Linux development bundles are byte-identical, SHA256
`abee73b34c986e8abd64e5947702d023ed2d074597f004397be404c2ecf4ca73`;
clean installation/72 extracted examples/worker checks PASS, bundle receipt
`3903cf93638994113fa71d62cac23d7b1655104ee563e148ffea53fee3e7ee7d`.
No development artifact replaces a published asset.

The identical isolated Linux Persistence-2D rerun PASS is
`/root/codex/gameplay-d6-20261013/linux-live-2d-20261013-114726/receipt.json`,
SHA256 `0c75649a78f35a6ee6d315252773d54d6516bf33720b3075e6e5edfe095d4801`.
It proves Query/reload/restart, two acknowledged -9 exits, three restart reads
and zero mutations/helpers. Allocated 118784 bytes is an observation, not a
universal disk budget. The same source/package and 3-ms callback / 5-ms frame
settings are used; the first failed run remains preserved without inferred cause.
All 183 prior borrowed-clone file hashes and UID/GID/modes restore; cleanup digest
is `2fdd77ff2d81f485debb12e7b947c03adfe86bd9c3c617e3c81fe4f84ab7ba52`.
No Rust process or task container remains after final Linux tests.

## Hosted closure and source attribution

All local final guarded-source native/managed/real-VM, 100-addon fairness, live
D6 and Gameplay A–D/C4, GUI/persistence, metadata/type/drift/preview, 72 examples,
sanitizers, deterministic development packaging and clean installation gates
above are complete. Hosted core/tooling/contracts/documentation workflows will
be attributed to the exact committed source after they complete. No pending
check counts as PASS; no authenticated-client, macOS-server,
Shockbyte full-runtime, arbitrary host-upgrade or power-loss claim follows.

Published v0.7.0 tag object `7c7d36f648a5506efd07c1b6d6bcf4b94d3f7d2b` and all
six uploaded asset digests are rechecked unchanged. No release/tag, version bump,
host binary, raw IL, private evidence log, database, journal or credential is
committed or shipped. Pre-existing `.codexlock` and `derived-369547800317500/`
remain untracked and untouched.

Next recommended Loot phase is a separately scoped single additional simple
definition (for example stones), with its own exact mod/creation/world/cleanup
proof. No item, prefab, mode, attribution or later API expansion begins here.
