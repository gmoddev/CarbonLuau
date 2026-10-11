# Gameplay C3 — public sampled ItemChanged

Status: **GAMEPLAY-C3 PASS — SAMPLED ITEMCHANGED SIGNAL QUALIFIED**.
Combined C2+C3 sampled qualification is complete within the exact recorded host
and platform envelope. This does not qualify a completed-operation observer or
publish a release/tag.
Starting main: `e7b230963ba583213fcad8620c7c6306bed3a174`, preserving intentionally
uncommitted C2 implementation/research and unrelated files.
Combined C2+C3 implementation/tests revision:
`8d53f132b934f72759a4828e8493cd9e9a652f3b`.
Final production correction revision:
`7ed7e536d6e997541cc6415b2895278108c9db4f`.
The qualification branch's evidence revision is the final hosted candidate;
production bytes are unchanged by subsequent evidence-only closure.

## Surface and authority

`Player:GetInventory().ItemChanged:Connect(function(Context) ... end)` returns the
existing Connection. Context is a frozen ordinary table with exactly Item:string,
Before:number, After:number, Delta:number. Before/After are checked exact nonnegative
integers through `2^53-1`; Delta is their nonzero signed difference.
Source annotations generate metadata/definitions. API `0.6.5-experimental`, package
`0.5.0`, ABI `1.5`, provider `1.2`, schema `1` and Luau pin remain unchanged.

This explicitly adopts the [C2 sampled candidate](GameplayC2-Validation.md), not
the rejected complete-operation contract. Intermediate transfers/splits can appear
as loss/restoration; equal gains/losses between samples can disappear. No individual
pickup/drop/consumption event, actor, atomic snapshot, history replay or exactly-once
delivery is promised. Fresh demand receives a baseline, never previous changes.
Invalid/incomplete snapshots produce no bridging delta. Overflow advances the
accepted baseline or rebaselines, never fabricates compensation or catches up.

C1 acquisition/read/mutation APIs and D13 PREPARE/COMMIT/VERIFY are unchanged.
Signal member acquisition is lightweight and watch-free; Connect creates bounded
original-D11/defining-domain/publication demand. Failed module/candidate staging
reclaims demand and callback roots. Original subscriptions survive caught rollback.
Sharing retains the defining owner; native publication control prevents laundering
through another domain. Delivery revalidates listener, original connection, facade
witness and root/VM authority. Disconnect, provider/domain retirement and VM recovery
discard stale work and release charges, never retarget by UserId or replay.

## Resource/transport bounds

The same C2 scanner supplies 64 watched connections, 128 total staged/committed
demands, four complete scans/actual frame and 128 stacks/names per scan. Source
capture remains 32 records/frame, 256 pending total and a 4-MiB accounting ceiling.
The pending cap combines C2 buffers and managed/native-held delivery reservations.
The conservative 2048-byte delivery charge produces a maximum accounted
3,227,776 bytes, below 4 MiB; it is not a process/GC/RSS guarantee.

Existing D23 budgets remain global/shared: 4096 visits/frame, 128 captures/deliveries,
32 deliveries/domain/frame, 512 reservations, 2048-byte payloads and 2-MiB transport
accounting. Other traffic may reduce inventory service. Sampling/fanout/delivery
rotate across watches, subscribers and sorted domains. Exhaustion preserves the
next watch, even at exact scan/record boundaries. There is no per-Player timer or
independent queue allowance. Source capture never enters Luau; delivery uses the
existing bounded scheduler and callback roots. Code39 exact-owner release keeps
native-held charges live through cancellation until callback consumption/teardown.

Private twelve-field transport carries opaque demand/publication/reservation tokens;
none is exposed in Context. Native validation rejects malformed names, integers,
zero/inconsistent deltas, wrong field counts and invalid identities. No export,
provider protocol or native ABI layout changes. Operator diagnostics are bounded
aggregate counts; no item values, keys or unbounded history.

## Qualification and preserved negatives

Windows/Linux real-VM tests PASS immutable context/exact integer extremes, first
baseline, multiple-subscriber cutoffs, failed/nested/cached/shared publication,
reconnect, replacement/provider retirement, callback errors/fatal recovery,
allocation faults, source/shared quota saturation and 100-addon fairness.
Shared 512 reservation stress uses two domains because each native queue caps at 256.
Additional Windows/Linux real-VM mixed-source receipts PASS simultaneous
ItemChanged, PlayerDied/Spawned and EntitySpawned/Died/Destroyed callbacks through
the common native queue: strict 12/14-field payloads, original disconnected killer
identity (no reconnect lookup), intake ordering and exact refunds. Exhausted common
capture/visit budgets suppress every stream. Synthetic completion certificates
test transport integration, not new Rust death/spawn/destruction host evidence.

Windows native 23/23, Linux native 24/24 and Linux ASan/UBSan/leak 24/24 PASS,
including actual allocator refusal/success and exact reservation cleanup. Full
Windows/Linux managed/runtime regressions PASS Foundations A-G, Player Interaction,
GUI, loader/unload, modules/publication, recovery, providers/packages/parser and
scale/fairness. C2's 10,000-cycle mixed-churn tests remain PASS with the public
binding available. No C2 research or historical failing receipts are discarded.

Metadata/generation/drift, C3 context/Signal source checks, 17 preview goldens,
tooling contracts, API links/architecture and deterministic package checks PASS.
Current clean Windows/Linux bundle installs compile 69 examples and exercise
existing GUI/Player behavior. New observation example and both inventory/context
references are bundled; default cszip contains no fixture/database/probe state.
The full hosted tooling pipeline remains the final VSIX/LSP/provisioning gate.

Independent review found/closed per-watch subscriber starvation and unsorted domain
rotation after root replacement. Native enqueue fault testing required a maximum
canonical item payload/queue-growth boundary to exercise actual allocator refusal;
the original small interned-payload fixture did not prove refusal. The first public
managed test incorrectly expected facade cache identity across publication scopes;
C1 does not promise that identity and same-operation acquisition is tested instead.
These are preserved test/review negatives, not hidden contract relaxations.
First hosted core attempt also caught the optional C2/C3 fixture declarations
living only in the Carbon inventory file, which the persistence adapter's narrow
source project intentionally excludes. The declarations now live beside their
callers in Main, matching the other optional fixture seams. Both platforms rebuild
and test that adapter; the hosted failure is retained, not relabeled PASS. This
changes no public behavior, persistence semantics, ABI or inventory policy.

Windows/Linux live controlled Carbon fixtures PASS actual deferred callbacks,
physical gain/loss/use/drop/receipt, completed main/belt/wear and stack zero net,
intermediate transfer/split negatives, balanced missed changes, callback interference,
subscriber cutoffs, root failures/replacement, same-ID reconnect, VM fatal recovery,
addon replacement/provider unload and cleanup. Controlled connections are not
authenticated clients. C2's Rust 25653776/protocol 2634.289.1, Windows Carbon
2.0.262.0/8a81d70 and Linux 2.0.261.0/c74c4ca tuples remain the exact targets.
Loaded IsValid/IsRemoved hashes/no-call bodies/zero Harmony topology are checked;
there is no newly installed inventory mutation patch.

Current live package SHA256: `ce6be89f6e706bb9f314ad966db5ca707c475a96fb6ddaaf7ba8076f2df6268b`.
Windows native SHA256 `9b8ad787d1c4104b166fc96a1befe5027d6144f53eca65696dc00cb2dbac77dc`;
Linux native SHA256 `8330d56db090d39ab01c15ba591efeb5493dd9666c1b9868b8f7058a129233e9`.
Windows log: `D:\Sandbox\Codex\Entity1AStartup\evidence\inventory-c3-20261011-163837-589\server.log`,
SHA256 `45bc7b8b72a63bb509c8dfd282f8b29d79a25ee4b2e2ecf05242cf66eb6880d8`.
Linux log: `/root/codex/world-movement-20261003/evidence/inventory-c3-20261011-163848-1771159/server.log`,
SHA256 `a63b9e844ce67336fe3244d78f18d69e878332703bf756feaf35120e1b092a5e`.
Runners gracefully quit, verify owned processes exited and restore all installed
package/native/compiler/hooks/config hashes. Reusable isolated caches remain.

Final-source rerun after the narrow adapter correction also PASS on both hosts.
Package SHA256 `0ce9172be4f792dd7b7febf512959ec26c4b9a212617a0e14e4cdd6a164a5cae`;
native/compiler hashes above unchanged. Windows receipt:
`D:\Sandbox\Codex\Entity1AStartup\evidence\inventory-c3-20261011-170507-295\server.log`,
SHA256 `bd3f793d905d5f006cc9262bf84427dc01218c792ab6f53b3bc0a6053151c3b3`.
Linux receipt:
`/root/codex/world-movement-20261003/evidence/inventory-c3-20261011-170517-1775601/server.log`,
SHA256 `ebd2efd4151b904700be59b5cfcad4089a0a76ae6d34c77681227d43005c9b14`.
Earlier live receipts remain evidence for their actual source revisions, not a
substitute for this final-head run. Both final runners verify restored installed
hashes and no remaining qualification server/process/lease.

Remote evidence roots: `D:\Sandbox\Codex\GameplayC3` and
`/root/codex/gameplay-c3-20261011`. `public-b.log`, `c2-regression-b.log`,
`full-runtime-b.log`, `clean-install-b.log` and `live-b.log` identify the respective
runtime/live scopes. Native CTests/fault/sanitizer logs remain in existing isolated
native caches. `tooling-b.log` records generated parity and goldens.

## Handoff

Final production correction `7ed7e536d6e997541cc6415b2895278108c9db4f` passed:

| Hosted gate | Exact receipt |
|---|---|
| Windows/Linux native, managed/real-VM, C2/C3, persistence adapter, full regressions, packaging/clean installs and sanitizers | [Validation 38174832218](https://github.com/gmoddev/CarbonLuau/actions/runs/38174832218): PASS all three jobs |
| Tooling Foundations A/B, API metadata/drift, VSIX/LSP/preview and platform matrix | [Tooling 38174832209](https://github.com/gmoddev/CarbonLuau/actions/runs/38174832209): PASS; macOS tooling is not macOS server qualification |
| Baseline contracts at that exact revision | [Contracts 38175074288](https://github.com/gmoddev/CarbonLuau/actions/runs/38175074288): PASS |

The first hosted failure is [901d validation](https://github.com/gmoddev/CarbonLuau/actions/runs/38174376803),
with the narrow adapter compile finding above. It is preserved as failure history,
not counted as PASS. Closing publication changes this evidence record only;
production/fixture/API bytes remain those of the final tested correction revision.
All new commits use configured GitHub no-reply author/committer metadata. No test
DBs/journals, vendor assemblies, secrets or coordination files are committed.
Pre-existing `.codexlock` and `derived-369547800317500/` remain user-owned/untracked.

No ItemAdded/ItemRemoved, global Inventory service, new scanner or mutation method,
Gameplay D extension, release/tag or identity bump. Remaining release readiness is
separately scoped package/release planning, not a completed-operation upgrade.
C2 negative research remains historical and unchanged by sampled public acceptance.
