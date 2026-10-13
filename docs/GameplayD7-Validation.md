# Gameplay D7 — additional Loot item qualification

Status: **LOCAL QUALIFICATION PASS; FINAL-SOURCE HOSTED CI PENDING**.
Starting main: `ee944d206e0e7af40e25333716054e8a16590ad2`.
Date: 2026-10-13. [D2A](GameplayD2A-Validation.md),
[D3](GameplayD3-Validation.md), [D4](GameplayD4-Validation.md),
[D5](GameplayD5-Validation.md), [D6](GameplayD6-Validation.md),
[D23](invariants/Gameplay.md), [D20](invariants/WorldEntity.md) and
D7/D10/D13/I4/I8/I9/I10/I12 remain authoritative.

## Scope and opening gate

The preferred sole candidate is `stones`. It is not an authorized replacement
definition until the exact pinned Windows/Linux hosts independently prove its
canonical name/ID reference identity, actual stack limit, initialization/mod
premises, fresh original Item state, Drop/world creation, completed Spawn
witness, original UID/reciprocal Item/world ownership and exact cleanup.
The initial probe is test-only and uses the existing bounded D5 helpers while
both production allowlist checks continue to reject stones. No public capability
is inferred from an inspection, model, guessed numeric ID or planned test.

After both physical probes pass, extend only the existing native declaration
and managed validation item checks. Reuse the qualified adapter and D6 registry.
Existing scrap/metal.fragments/wood and both exact autospawn barrel prefabs stay
supported. No new item is chosen if stones fits; if it fails, preserve the
negative evidence and investigate a separately bounded simple alternative.

Both closed-allowlist probes PASS using test package SHA256
`6fe12d396710c9a241ae2120f56869bb4d95d4ab99b4ce02dc08f6ccf5b99878`.

| Host | Actual candidate receipt | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261013-165830-694/server.log` | `f0a10e751ec1be5c8db28ccd608a86d56eaee34275bb79e376077b750037420e` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261013-165908-1916586/server.log` | `c0d2073aa9efe6e201074963ede8a961d04c10819e9ec0b213c57ef2cfbdb940` |

Both measure canonical `stones` / `-2099697608`, identical name/ID lookup
references, stackable 1000 and disabled condition. Definition roots are Transform,
ItemDefinition, ItemModXPWhenUsed and ItemModApartmentTax. Both mods inherit
the six exact ret-only ItemMod creation/world/removal/change callbacks; no
special initialization is required. Fresh amounts 1/50/250/1000 have skin,
condition/maxCondition and fuel zero, no contents/instanceData/held entity.
Each original Item drops into the ordinary generic DroppedItem world, preserving
its UID and both reciprocal references with no container parent. The exact keyed
world and original completed-Spawn epoch are proved before cleanup; all ledgers
return to zero and original world/Item removal is verified. The loaded method
reflection bodies are diagnostic identity, not mislabeled Harmony replacements.

Windows exits -1; Linux exits 137. Both runners restore the prior installed
package/native/compiler/hooks/config hashes and stop their owned processes.
These receipts authorize only the two narrow allowlist checks; they do not
count as final public binding, failure, publication or regression qualification.

## Final public physical matrix — both hosts PASS

Corrected D7/D6/D5/D4/B2 package SHA256:
`c372a2634173214f722ef737a5e09612228626d0d516e7112735b28e6c134d49`.
Both runs use the identical corrected test package and the actual new native
bootstrap; tables enter through the public service, not substituted descriptors.

| Host | Final actual public receipt | Log SHA256 |
|---|---|---|
| Windows | `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261013-171416-257/server.log` | `fc3fbd96afdb9fa625fea38e5c3847a616c8ec23dd475d098f6e794c03e69aae` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261013-171752-1926279/server.log` | `e8072fc4e80fa0bfeeadaa420b0bf84458e80ceed24af7e603347c16c114c485` |

Both PASS stones minimum=1, zero-only, mixed zero entries, inclusive range
50..250, maximum=1000, eight distinct 1000-unit worlds and mixed entries with
scrap/metal.fragments/wood on each exact prefab. All successful worlds preserve
the original Item/UID, planned definition reference and exact amount, reciprocal
world links, keyed original object and completed Spawn witness. Range tests
prove inclusion, not a distribution. Stackable=999 against declared Max=1000
rejects before COMMIT and preserves two exact original vanilla worlds; restore
the actual definition in finally. Amount1001, nine entries, unsupported names
and repeated prefab declarations reject without changing committed owners.

Stored, real modifier-driven bonus and controlled alternate bag payouts are
suppressed by the existing common cut. Natural bag selection is not claimed.
New stones cases exercise source Kill veto, failed/incomplete actual Spawn,
second Create null/throw and returned-amount interference. Accepted first worlds
survive later failure, cleanup uses original references/UIDs, and no unreturned
internal allocation cleanup, fallback, replay or compensation is inferred.

Both hosts PASS same-root and separate root/addon ownership, overlapping
whole-set conflicts, caught module rollback, failed/successful independent
replacement, provider unload, fresh fatal VM recovery and no physical replay.
Interleaved sources exhaust the unchanged global eight-occurrence actual-frame
budget; replacement does not reset use, and both excess targets preserve vanilla.
Final inherited D3–D6/B1/B2/B3/RPC/terminal controls PASS. Cleanup_failed=0 and
final retained rule sessions/temporary resources converge to zero. Windows exits
-1; Linux exits137; prior installed hashes restore and owned processes stop.

| Supported canonical definition | Pinned numeric ID | Stack limit |
|---|---:|---:|
| scrap | -932201673 | 1000 |
| metal.fragments | 69511070 | 1000 |
| wood | -151838493 | 1000 |
| stones | -2099697608 | 1000 |

The existing exact prefab suffixes under
`assets/bundled/prefabs/autospawn/resource/loot/` remain
`loot-barrel-1.prefab` (966676416) and `loot-barrel-2.prefab` (555882409).
No additional item, prefab, mode or unrestricted definition support is enabled.

## Unchanged publication, ownership and resource envelope

The API remains `Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)`.
One exact active owner per qualified prefab, two active rules globally and two
distinct rules per domain. Whole owner sets publish together; failed publication
preserves previous committed sets, omission withdraws only that owner's key,
and retirement requires the exact outgoing owner reference. Fatal VM retirement
invalidates all old owners and reconstructs declarations without payout replay.

Keep eight entries per rule, integer amounts 0..1000, sum(Max)<=8000 per rule,
eight worlds per occurrence, eight admitted occurrences per actual Unity frame
globally, one in-flight frame, 129 inspected sessions, 64 array/64 hash table
backing slots and 24 scalar fields/1024 UTF-8 transport bytes per declaration.
D6's three charged sessions, six owned descriptors/48 entries, 195 charged maps/
390 descriptor references, 64 publication levels and separately bounded empty,
borrowed and transient graph accounting remain unchanged. Adding a shorter item
name cannot increase the qualified maximum retained string length.

PREPARE rejection preserves vanilla. Suppression begins nontransactional COMMIT;
partial/indeterminate failures never trigger fallback, rollback, replay or
compensation. Accepted original worlds remain host-owned. Existing three
interception targets, lifetime observers, schedulers and execution boundaries
remain untouched; no synchronous Luau callback or heuristic attribution is added.

## Qualification and source attribution

Pinned hosts remain Rust 25653776 / protocol 2634.289.1 / revision 166494,
Windows Carbon 2.0.262.0 (`8a81d70`), Linux Carbon 2.0.261.0 (`c74c4ca`)
and Harmony 2.4.2.0 with the D5/D6 backing hashes. ItemMod callback reflection
MVID is `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e`; each inherited one-byte ret
body hashes to `684888c0ebb17f374298b65ee2807526c066094c701bcc7ebbe1c1095f494fc1`.
The executable changes are only the two existing item-name admission checks;
bootstrap is rebuilt because its executable body changes. API annotations and
generated documentation change without changing signatures or version identities.

Require separate physical candidate receipts and final public matrices on both
pinned hosts. Final public tests cover zero/minimum/inclusive range/maximum,
eight repeated entries, all supported mixed entries, both independent prefab
owners, shared frame exhaustion, stored/bonus/controlled bag suppression, source
veto/incomplete Spawn/creation faults, publication/provider/root/addon/recovery
and cleanup. Run D3–D6/gameplay/GUI/inventory/persistence regressions, native/
managed/real-VM tests, allocation faults/sanitizers, 100-addon fairness, tooling/
metadata/types/preview, all examples, deterministic development packaging,
clean installation and exact final-source hosted CI.

Record every failed attempt separately without weakening assertions or deadlines.
No pending test counts as PASS. Published v0.7.0 tag/assets and package/API/ABI/
provider/schema/Luau identities stay immutable; no release or tag is created.

## Preserved negative evidence

The first Windows public fixture package was
`92231e42471db4559eaa510042733888560f0fd9439057b6364600baea130fa0`.
Receipt `D:/Sandbox/Codex/Entity1AStartup/evidence/loot-d2a-20261013-170731-168/server.log`,
SHA256 `799b15df47fb8db09515bf61a229a197682704df89034e8cf5f64268dca52e37`,
passes both profiles' mixed-four, all-zero, inclusive range, maximum and eight
1000-unit entry rows, but is not a complete public PASS. The stack999 case was
the first death in a fresh actual frame. Existing LootTryBegin normalizes its
frame counter before rejecting the changed stack premise; the fixture's
unchanged-frame assertion assumed a prior eligible payout. It failed before
holding the exact original vanilla worlds, then strict cleanup refused unowned
Items and masked the first cause. No post-COMMIT receipt is claimed for that row.

The test-only correction establishes the existing D5 frame premise with a real
eligible payout first, and creates the source before the controlled definition
mutation. Its required occurrence count stays within the unchanged global
budget. Production accounting, rejection/ownership assertions, deadlines and
cleanup authority are unchanged. The known-failing package is not repeated on
Linux or relabeled as success; corrected receipts are recorded separately.

Two earlier Windows transfer/test-alignment errors remain separately recorded:
managed source stayed closed after a wrong transfer path while the test/bootstrap
opened stones, and baseline public tests still expected stones rejection after
native admission opened. PS5 native-stderr handling left their redirected logs
empty; only the captured tool diagnostics and explicit transcript-derived
receipts describe their causes. No complete native trace is inferred from an
empty log, and no production defect is inferred from mixed-source execution.

The explicit transcript-derived Windows staging receipt is
`D:/Sandbox/Codex/GameplayD7/ExpectedStagingFailures.json`, SHA256
`aaa22cc31573762b856437f042edbeb8421fb177c217a3fd7f1f3b80e4f6590c`.

## Completed local and hosted regressions

Windows native Release tests PASS 23/23 in 164.66 seconds; Linux Release PASS
24/24 in 166.17 seconds. Linux ASan/UBSan/leak detection PASS 24/24 in 324.34
seconds. Private transport/lost-ack/worker/parser supervision and actual adapter
teardown pass on both platforms. Focused policy/public real-VM bindings pass,
including both-prefab stones declarations, zero/one/1000 amounts, eight repeated
entries, four-item copied inputs, malformed/missing-definition rejection,
registrar allocation failure, real-VM allocation exhaustion, exact incumbent
preservation or fatal retirement, independent owner replacement/unload and fresh
recovery without replay. The 100-addon shared-VM fairness/heap/lifecycle cases
pass; full runtime preserves 100 atomic replacements and 200 failed replacements.

| Linux task-owned log under `/root/codex/gameplay-d7-20261013/` | SHA256 |
|---|---|
| `native-linux.log` | `43c0d9c525bf02ada21b4309e4c91c2fd0cdc1ed983111f78cc7a6ff03c0e929` |
| `asan-linux.log` | `98c55e62cd38eeab1c7bd144fe8b672ca95ddc6cef97cebe8205dac98b040a9c` |
| `private-linux.log` | `928192fad6ff618db72a3858dbe68c3d5d449b225137e12dc61ab32baaa6f449` |
| `runtime-linux.log` | `81fd8e869358e061bc9fc0ca13c3355aebdad987169cc7b88b4f1627b2b78583` |

| Windows task-owned log under `D:/Sandbox/Codex/GameplayD7/` | SHA256 |
|---|---|
| `native-windows.log` | `d79abe3e887f2709c575b365551307d3689d2a1b4e17e89b57c256bb6cec7433` |
| `lootpolicy-windows.log` | `dbd0a213458e8aa310d4f905360b8f31f930f70b717dbbc74cf9151cb82bc329` |
| `lootpublic-windows.log` | `00ecb3f9ee6bbbcd7b391046ad3c95127e520c096433216c6d2125304a84c32d` |
| `full-runtime-windows.log` | `c9408b4c7b9b69ed90bad52f65709c8696c821aac951674f81973e706af5fab3` |
| `loader-windows.log` | `c1c7477e8ac3662f86be80e529f028453e501f73688e703b6b24c989ef57bdad` |
| `managed-persistence-windows.log` | `fc6311edc7bb8af4e1a715c4843df1158a4301f143e440f69f62d34c0e474b63` |

Final native SHA256 Windows
`e1a57f5e1443152e508675ccbca6259cb605bd026bd0f5f7b3cbc9291b9d3fa1`,
Linux `97fd452a1fe590b7b28971decb60b67765c3508cbab0a6062bdc90c3a5a5ba3c`.
Windows compiler/storage respectively
`bcb3debe4c926db4e830a52304177d3ab43e8949278e3a43bf1d18699a5aa54b` /
`45d90f84d6f6776090bda01baee34c2b043f6e8839432fee3b87e1f1c135b8cb`.
Linux compiler remains
`884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92`.
Linux storage remains
`ccff9dbb308cabaee8d1ddd4fb9ca2b86f3f3b8fca646bf8709490058d88cc54`.

Separate actual Linux D6+B2 receipt
`/root/codex/world-movement-20261003/evidence/loot-d2a-20261013-172114-1926953/server.log`
SHA256 `3b8db7b29dab5db7029e0c1e8bac553740b8f822f2d4701a10805bee187a4155`;
D5+B2 receipt `loot-d2a-20261013-172311-1927162/server.log` in the same evidence
root SHA256 `9b43c9d18be2674162e707113e927380e670238612c3984daefe88875baccfe4`.
Both PASS without modifying default assertions and restore installed inputs.

Separate actual Linux C3/C4 receipt
`/root/codex/world-movement-20261003/evidence/inventory-c3-20261013-172444-1927348/server.log`,
SHA256 `078154339fc9e8fa0e896e0af3522ccc76408fcfbc146bd5d1ea95e331dce2f9`,
and Gameplay A receipt `gameplay-host-20261013-172542-1927674/server.log` in
the same evidence root, SHA256
`9f97bd7b17bce67238e2b0ea5c4915153a64aeb824c1c1b54bfe09f670b7987e`, PASS.
All twenty prior database/companion/world saves and installed inputs restore.

Actual Windows Gameplay A receipt
`D:/Sandbox/Codex/Entity1AStartup/evidence/gameplay-host-20261013-171740-123/server.log`,
SHA256 `438dae305f1fe563fc0ea3aa9d8744504daefa5ce75779ed96308ad0276e0757`,
and C3/C4 receipt `inventory-c3-20261013-171846-005/server.log` in the same
evidence root, SHA256
`3977e891cb58aaa29bc25c4ad6312af24dd8934f43c137489b58007b33fc3185`, PASS.
Gameplay A cleans ten actors/corpses and restores twenty original database/world
files. Both stop the owned server and restore installed inputs.

Windows namespace-only actual restart PASS in
`D:/Sandbox/Codex/GameplayD7/PersistenceEvidence/persistence1b-20261013-172058-4f95a0ad/`:
`server.log` SHA256 `c3406c20443f85e4a187517f813b8b99a7f7cabd520c6803f8b0d74ec8dcc667`,
`server-restart.log` SHA256 `077daf0f632321752e0ebaa719e8df4af45070c5e6a28571a59ae92a5b91faa5`,
`restart-proof.json` SHA256 `29f66f5b40a3e59c0a002f377083cebc405eef0d0ca1c5c2119049df8762ece9`.
Seed 8/4/2 survives an actual restart as read-only 3/0/0; both exits -1, zero
helpers, native unmapped and prior legacy trees restored.

Local document validation PASS 239 documents / 3696 links / 97 retained spans
and I1–I12 coverage. API, architecture, contract positive/negative fixtures,
17 preview-plan schema goldens, offline published-release negative fixtures and
relocated/offline release navigation checks PASS. Generated type definitions
remain byte-identical; SDK10 regenerates only the Loot summary in API/docs
metadata. Actual preview execution, final packaging and CI are recorded below
when completed; these schema checks alone do not qualify the preview worker.

GUI attribution preserves D6's envelope: Windows final real-VM retained GUI,
projection/compiler, reload/teardown/recovery and clean-install cases; Linux
actual Carbon GUI fixture. Windows real-VM receipts are not mislabeled Carbon
GUI or authenticated-client rendering evidence. The local Windows SDK9 cannot
execute the net10 tooling worker; exact hosted Windows tooling supplies that
platform's worker execution without installing a toolchain or retargeting it.

Linux actual GUI PASS ten healthy root replacements and ten Carbon
unload/reloads; native unmaps and compiler exits on each unload. Task receipt
`/root/codex/gameplay-d7-20261013/gui-borrow-20261013-172730/runner.log`, SHA256
`a2d278e9fe00e3fe0132c4813436bcc4a06ed8628154679e644edc3a8eba552b`.
Its cleanup receipt SHA256
`2fdd77ff2d81f485debb12e7b947c03adfe86bd9c3c617e3c81fe4f84ab7ba52`
confirms all 183 original files' hashes, UID/GID/modes and zero owned processes.
Linux SDK10 generation/drift/17 production goldens receipt
`/root/codex/gameplay-d7-20261013/generate-linux.log`, SHA256
`3af22bbb121b6632e0e2d0f1d0bd530fac6693d54317908b31a21081e40db7d8`.

Linux Persistence2D actual two-server restart PASS, receipt
`/root/codex/gameplay-d7-20261013/linux-live-2d-20261013-213045/receipt.json`,
SHA256 `6781c1b32e7d448ba24fe4cfc9ba940221da6ff3667019754f4dd479baf14527`.
Three restart reads, zero restart mutations, Query2D true, both quit/save
acknowledged, zero surviving helpers and 118784 allocated bytes after stop.
Both servers exit -9 after the qualified quit/save sequence. Live cleanup SHA256
`ff062fb6f78d73b25d7d9eebb7a4a3f2ea5f66863354fbf5d21b2c5646d3df65`;
borrowed clone restores the same exact 183-file hash/UID/GID/mode manifest.
Its deterministic development bundle and clean installation compile all
73 examples with zero errors; the unchanged bounded wrapper exits 0 in 8.94
seconds. Packaging and worker receipts are completed below with final hashes.

Windows Persistence1C PASS 303.91 seconds with twelve full host/worker reopen
cycles. `D:/Sandbox/Codex/GameplayD7/persistence1c-windows.log` SHA256
`dff73fb7ee1d346058da9e937622f959d90aadc37ad439efb04d598d07bf0350`.
Repeated final 73-example development bundles are byte-identical, SHA256
`1f2c4a08670f18b658b6d56a74812db1bbdccaca7a5ecd476c1b5e5f6a210e13`.
Clean-install 73 examples and actual native GUI/Player/C4 teardown PASS, log
SHA256 `c7710589586c9ecd1f1f10e617ffc244930a092c17816cefd90e7a27299bb20d`.
Extracted private-worker/import/ownership/failure checks PASS, log SHA256
`1466d2c751db7540d25c0f1c6cf7f24e2442a58a735e3ff9b331661d73b4b078`.
These installation checks run the native/managed runtime, not a live Carbon GUI.

Independent final read-only diff audit PASS: exactly four admitted names;
production constants, D6 publication/FacadeSession/adapter and existing lifetime
observers remain unchanged. Existing LootFixtureVanilla and LootFixturePayout
predicates are byte-equivalent after newline normalization. New cases append
to the inherited matrix and reuse exact UID/reference/epoch ownership helpers.
No interception target, producer cap, callback budget or runner deadline is
added or relaxed. Old unknown-item cases use a still-unsupported definition;
strict case/NUL/1001/ninth-entry and incumbent-preservation checks remain.

Windows final source/restoration manifest
`D:/Sandbox/Codex/GameplayD7/Windows-Qualification-Manifest.json`, SHA256
`634705a1382053485d8dd808368f43aa0f3012ce55aacde901900191072ebcc1`,
records all logs/binaries/source inputs, the limitations and preserved negatives.
Final owned processes=0, runner locks=0, namespace backup entries=0; installed
inputs and prior legacy trees restore. Runtime/test input hashes match the final
shared checkout; evidence-only ledger updates do not change executable inputs.

Linux final deterministic bundle SHA256
`f2dbc6cc85710641e70d96ef64026987336a331a6b3773354fd30f1a9cfda520`.
Clean-install all 73 examples and extracted private worker PASS; receipt
`/root/codex/gameplay-d7-20261013/bundle-linux.log`, SHA256
`96ec3b288dc7e8195148f69a08aee74762a78dc0b41e71a5887f16ade241bc6d`.
Legitimate pinned Linux tooling pack and actual preview black-box PASS the new
stones UnsupportedPreviewApi case, 17 repeated goldens and failure/recovery;
`tooling-provision-linux.log` in the same task root, SHA256
`ccdb441dda1fc5f750f9795e9b39e69affd3ea46be45799b24a03e710ce6b193`.
This is actual worker execution, separately from the schema checks.

Separate supervisor negative is preserved: the task-only Docker container lacked
an init reaper, and its unchanged orphan assertion failed after killing the hang
worker. `tooling-preview-supervisor-linux.log` SHA256
`e1f57755a5934ae7966caef9756d265e7776a4ec42591249a9fa1d88b072a3f5`.
An unchanged no-init reproduction preserves log SHA256
`05433f7e928ee28582d61795f5c453b9077dde070c977fdc007e1a7bb9a11170`;
`tooling-supervisor-noinit-process-states.json` in the Linux task root, SHA256
`3afa10eba7525ee053668b8cd02e6fcfa94bd4d345db4d3d69c196db1df69bef`,
proves PreviewFixture PID98 first sleeping under launcher97, then zombie state Z
with PPid1 for thirteen snapshots before the unchanged orphan assertion fails.
The owned stopped container is removed; no live worker remains. The qualified
retry uses only a task-container `--init` reaper; product source, payload,
assertions/deadlines and host configuration remain unchanged. Its outcome is
recorded separately below, not substituted for this negative.

Unchanged supervisor retry PASS with the task-only init reaper: eleven hostile
modes, external time/memory bounds, protocol, cancellation, EOF shutdown and
real-preview recovery; zero orphan assertion failures, persisted exit 0 in
7.85 seconds. Receipt `tooling-supervisor-init-linux.log` in the Linux task root.
All requested local Linux gates now PASS. Final-source hosted CI remains required.

Recommended next phase: separately qualify one additional simple vanilla
definition, such as sulfur, through the same bounded adapter and both exact
hosts. This phase does not authorize it or expand the registry.

Read-only GitHub release inspection confirms published v0.7.0 tag object
`7c7d36f648a5506efd07c1b6d6bcf4b94d3f7d2b` and all six asset digests unchanged
from D6. No release, tag or unnecessary version-identity change is made.
