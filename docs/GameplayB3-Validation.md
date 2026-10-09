# Gameplay B3: EntityDestroyed qualification

Starting source: `d3e464ad4405f791182bbc6e5bdb5a2924ce0b57`, clean `main`.
Status: Windows/Linux live fixtures, full native/managed regressions,
sanitizers, final tooling/LSP and deterministic production packaging pass. A later exact-host
never-active native-deletion negative exposed missing cancellation coverage.
The corrected bounded original-wrapper complement passes current Windows/Linux
live probes, including the previously failing never-active case. A separately
pinned pool-admission fence covers world retirement without Unity invalidity and
passes controlled real-pool Windows/Linux qualification. All eight final
three-source host modes pass on both platforms, including off-thread rejection
and teardown. Final-source hosted CI/documentation and commit synchronization
remain pending. Dedicated B3 allocation-fault reruns and refreshed full
managed suites pass. Final production clean-bundle execution passes on both hosts.
This is not a completed overall PASS record yet. No release or tag is created.
Development API remains `0.6.5-experimental`; package remains `0.5.0`.

## Public contract and coverage

The only new member is `Workspace.EntityDestroyed:Connect(function(Context)
end)`. Its ordinary frozen `EntityDestroyedContext` contains `Id: string`,
`Prefab: string`, and optional `Position: Vector3`. It contains no live Entity
proxy, raw host object, token, epoch, cause or killer. Id is an original-lifetime
correlation snapshot, never authority to select a replacement by network ID.
Already-returned strings and Vector3 values remain usable after host retirement.

The implemented collective source combines successful world removal, completed
pool admission, native cancellation prompts and bounded native-validity observation:

- Successful original `TerminateOnServer` completion, with the original registry
  occupancy removed, original network authority released and GameObject
  deactivated. This is successful Rust-world removal even if later reset/pool
  cleanup fails. A Kill attempt, veto, destroyed flag or private D20 retirement
  alone cannot publish it. Normal admin, combat, decay, stability and plugin Kill
  paths reach this source; Carbon's construction-cancellation path also reaches
  it without Kill or `DoEntityDestroy`.
- A separately hash-pinned `PrefabPool.Push(Poolable)` fence runs immediately
  after its single actual `Stack<Poolable>.Push` returns and before `EnterPool`.
  It certifies only a previously watched original epoch whose network authority
  is released and whose original registry occupancy is absent. The original weak
  GameObject mapping is correlation, not a fresh ID lookup. No `IsDestroyed` or
  deactivation assumption is used: pooling mode 2 can remain active. A later
  `EnterPool` throw or same-object reuse does not revoke this historical removal
  fact. Direct pooling of a still-network-registered object is host misuse, not
  proven successful world removal, and publishes no event. Controlled actual
  host-pool admission/reuse passes on Windows/Linux; natural vanilla pooling is
  separately NOT OBSERVED.
- The original MonoBehaviour's native destruction cancellation is only a prompt.
  It retains the watch rather than certifying success or losing later Terminate
  coverage. Cancellation cannot prove deletion and is absent on the pinned host
  for some never-active eligible components.
- A fixed-slot, owner-thread complement visits at most 128 original weak watch
  slots per actual Unity frame through the existing discovery frame pump. It
  certifies native removal only after observing the original managed wrapper as
  Unity-invalid with unchanged D20 epoch. It never resolves an ID, traverses the
  realm, strongly retains wrappers, adds a scheduler or changes activation.
  Weak collection before proof is a counted drop, not destruction. Once certified,
  the historical scalar fact survives subsequent collection/reuse; fresh callback
  admission validates listener/domain/source authority, not a live Entity proxy.
  Native deletion may leave Rust bookkeeping ghosts and makes no cleanup guarantee.

The Terminate source may include a bounded pre-removal Position observed before
Kill/Terminate invalidation through the qualified position reader. Native-only
destruction and direct pool admission supply no Position: no unqualified cached
position is invented.
The event promises original-incarnation removal, not a cause, physical allocation
reclamation, pool completion, client delivery or successful Rust-subsystem cleanup.

Only qualified completed D20 Spawn epochs are watched. Startup reconciliation
produces no historical event, and registration, root/addon replacement, provider
reconstruction and VM recovery produce no replay. Deduplication uses the original
epoch, not UserId, flags or network ID. Terminal snapshots may be delivered after
retirement but never become live proxies. Native polling cannot timestamp deletion
between visits. Its fanout uses the last positive live-observation publication
cutoff: listeners published afterward cannot receive historical destruction.
Ambiguous recent-registration windows may conservatively drop notification;
delivery is best-effort, not a completeness or latency guarantee. Pure churn,
deactivation, failed/vetoed Kill and CarbonLuau unload do not manufacture events.
Shutdown stops capture and suppresses pending work instead of synthesizing
destruction for surviving entities.

## Host proof and preserved counterexamples

The exact tuple remains Rust build `25653776`, protocol `2634.289.1`, Windows
Carbon `2.0.262.0` / `8a81d70`, Linux Carbon `2.0.261.0` / `c74c4ca`, Harmony
`2.4.2.0`. [Entity-1A](WorldEntityFoundation1A-Validation.md),
[Gameplay A](GameplayFoundationA-Validation.md) and [B1](GameplayB1-Validation.md)
retain their own historical evidence; they do not substitute for B3 qualification.

| Binary | Windows SHA256 | Linux SHA256 |
|---|---|---|
| Rust Assembly-CSharp | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| UnityEngine.CoreModule | `93b0e7e8e1b9a82f34d09740c45be2cbd7c13a82683a192af2858831b63ce45a` | `ada97d7037c7c928d8d432f734115d82a3d805a2da628f481901da5d165795e2` |
| Facepunch.Network | `ca0181b2458ebeaa59f4e9f9aaf09e73cdc3a808ccd26b8cfd503ab30c466fc7` | `fbeae46ee305b7079435a05d4175c50e9ca61c3c8bafbe6d09df915ea3030c25` |
| UnityPlayer | `6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a` | `ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1` |

Current Windows/Linux source IL agrees on these boundaries:

| Method | Token and relevant ordering |
|---|---|
| BaseNetworkable.Kill | `0x06005177`: virtual OnKilled `IL_004e`, DoEntityDestroy `IL_0054`, TerminateOnServer `IL_0061`, EntityDestroy `IL_0067` |
| TerminateOnServer | `0x0600517a`: realm unregister `IL_0015`, network release `IL_0025`, deactivate `IL_0037`, return `IL_003c` |
| EntityDestroy | `0x060051b4`: virtual ResetState `IL_000e`, GameManager.Retire `IL_001f` |
| GameManager.Retire | `0x06006458`: pool Push `IL_005b` or deferred Unity Destroy `IL_0063`; a still-valid entity warning does not stop retirement |
| PrefabPool.Push(Poolable) | `0x06008344`: 41-byte body, no exception handlers; exactly one `Stack<Poolable>.Push` fence before `EnterPool` |

Complete managed call-reference inspection finds 383 Kill callsites per platform.
Direct TerminateOnServer/EntityDestroy references in Rust are in Kill; Carbon's
`OnConstructionPlace` transpiler also emits DoServerDestroy, TerminateOnServer
and EntityDestroy. Method `0x06000737`, 1,056-byte body, has identical IL-text hash
`d1ab041eb359fbafaee7d3346dc6d823e855bbbab4d7e013cfadf58e249cfccf`.
Thus `IsDestroyed` is not universal completion authority.

The separately pinned live raw Kill hashes are Windows
`9153173a2977a37f995b06465a5b9a3de598a7a48e4868be0cb71f1bcfeb6558`, Linux
`ed1ebec87800e322c04e9810b72551bed60e6e96e9afa245e6441d0f97234e05`;
TerminateOnServer is `4450e9c486b61e2a4474b72a7201ae7dbcc91d032c2daf6ef025bf8dee7b8333`.
Carbon publicizer transformations are not mistaken for unsupported drift.

B3 adds a separate three-target Kill/Terminate/PrefabPool.Push patch table, not
changes to D20's 24 Spawn methods, patch order or stamp objects. Kill captures
pre-removal Position; verified Terminate completion certifies that source.
Kill/Terminate use first prefix, last postfix/finalizer, original-run/error checks
and bounded nesting. Pool admission uses a narrowly pinned insertion fence and
shares the same bounded removal nesting; its finalizer releases temporary host
references even when later host work throws. The backing assembly pool body SHA256
is `2e189be7a82833c4a85098a75c9902448619f907ac1cd822ffe9545956ac0be1`.
The loaded Carbon-publicized body, measured identically on Windows/Linux and used
by production's live pin, is
`cd2bcf544bc4e38c1969ea954f7f00ee3e8d098b3980695a178fa8e40be851d9`.
Its token, 41-byte length and zero exception-handler count are unchanged; operand
tokens differ. The first backing-body-only guard correctly failed closed on the
loaded body. Initial negative boots Windows `044948-911`/Linux `044956-1571956`
and diagnostic boots Windows `045358-443`/Linux `045406-1572330` are preserved.
Native
cancellation uses the pinned existing host primitive only as a prompt. The
complement uses original native invalidity in bounded existing frame intake.
Removal/body/binary drift,
off-thread cancellation, observation quotas or lost continuity fail B3 closed.
B3-only patch drift need not invalidate independently unchanged keyed D20 or B1.
First install after world startup and full plugin unload/reload still need D20's
restart; B3 cannot repair an observer gap.

[Historical lifetime negatives](WorldEntityLifetimeInvestigation.md), the
[Gameplay research](GameplayEventsArchitecture-Research.md), vetoable OnEntityKill
and specialized Bradley/CH47 OnEntityDestroy findings remain intact. The collective
observer supplies qualified success boundaries; those hooks do not become
completion authority. Actual natural same-object pooled reuse is not newly
claimed. Controlled actual live pool admission/reuse qualification passes,
separately from controlled model same-object/epoch tests. No EntityDied,
generic hook bus or B2 authority follows.

Full IL remains outside Git in
`C:/Sandbox/Codex/GameplayA-20261008/HostEvidence`:

| Research artifact | SHA256 |
|---|---|
| B3Kill-MainBodies.txt | `26b151b3e92201e69b506644ec328c7da62ef5e823c44b86855a397d090c180b` |
| B3Kill-DirectCallers.txt | `252f6ad781880db16f46f6c3ae07bbf5e847990f6218d47dfaf77570d9c802f1` |
| B3Kill-HookTopology.txt | `254adf1657738c49b07020f51c0c6ddf46da5ada10642c64040e83b2378d2aa9` |
| B3Kill-HookAlternateCallers.txt | `5cdb13b1de7f5035bd9c453c0f1222182b41a161f4deb27926e97d8878d07272` |
| B3Kill-KillCallers.txt | `d6cc11389b6c622bfd953a98335269435c2e3da1776d28055a0c354ffa6152e6` |

## Resource, publication and lifecycle policy

D7/D10 listener staging, rollback, exact owner/domain admission and existing
Signal/Connection cancellation are reused. No hook enters Luau recursively.
Snapshots use existing deferred scheduling and native private reservations.
Failed candidates publish no listeners; replacement/provider retirement, fatal
recovery and shutdown cancel delivery and release reservations.

| Bound | Scope |
|---|---|
| 262,144 active watches | Global host observation envelope, not per addon |
| 64 MiB snapshot text charge | UTF-16 prefab plus 20-character ID charge; not total managed heap/RSS |
| 16 nested removal frames | Global owner-thread depth; continuity fails closed when exceeded |
| 128 optional pre-removal Position reads per frame | Requires committed listener demand; separate bounded producer work, excess yields nil Position without suppressing otherwise valid removal |
| 128 native-watch slots per actual Unity frame | Existing frame pump; no realm scan, ID lookup or per-addon budget; zero held watches take constant work |
| 128 captures / 128 deliveries / 4,096 visits per frame | Shared with Gameplay A/B1, not multiplied by addon count |
| 32 deliveries per domain/frame | Existing fairness ceiling |
| 128 listeners/signal, 256 listeners/domain, 256 queued events/domain | Existing shared policy |
| 512 reservations | Shared existing managed/native ownership/acknowledgement path |
| 2,048 payload bytes, 512 UTF-8 prefab bytes | Strict twelve-field codec; all-or-none finite host-coordinate XYZ |
| 2 MiB retained transport accounting | Existing two-copy envelope, not total runtime memory |

Watches retain weak owner/entity identity and immutable bounded scalar/text state.
Native completion never scans the world or selects an entity by ID. Watches release
on proven removal, later Spawn, rejected pre-proof weak identity or teardown;
cancellation intent alone does not release coverage. Weak collection is not proof
but does not revoke an already certified historical scalar fact. Overload/rejection
is diagnosed without retries/replay. Failed source registration/qualification
continues bounded stale-watch cleanup, at most 128 slots per actual frame, while
admitting no new watch growth or replay. Lua contexts and
callbacks remain within the unchanged shared 64-MiB VM allocator. No per-addon heap
limit or independent frame budget is added. Held watch-slot storage is presized
at startup instead of resizing during callbacks. Native cancellation registration
and weak-table costs are empirically qualified, not a wall-time theorem.

## Preliminary exact-host evidence and correction

Preliminary live fixture package SHA256, before the native coverage correction:
`692e15a86a17b92a181ca476d95791c725ab202860b04ca55237535bb3f2c5b1`.

| Gate | Current result |
|---|---|
| Windows exact Carbon preliminary positives | PASS public end-to-end fixture, 1,020 startup watches; later never-active negative requires corrected-source requalification |
| Linux exact Carbon preliminary positives | PASS public end-to-end fixture, 1,003 startup watches; same later negative |
| Windows/Linux focused native | PASS strict codec, immutable real callback context, cancellation, shared quotas and allocation-fault ownership |
| Windows/Linux focused managed/real VM | PASS snapshot/publication/replacement/provider/recovery/100-addon tests; host transitions explicitly synthetic |
| Full Windows native | PASS 23/23 CTests, 146.29 s |
| Full Linux native | PASS 24/24 CTests, 119.46 s |
| Full Windows/Linux managed | PASS refreshed runtime regressions, including publication cutoffs/current activation boundary, A/B1/B3, addons, GUI, persistence and Player; focused budget/100-addon real-VM tests also pass |
| Linux ASan/UBSan/leaks | PASS all 24/24 CTests, 172.87 s; instrumented native runtime and pinned Luau, not Unity/Carbon |
| Generated metadata/full tooling | PASS SDK10 check-generated and full tooling; local API/architecture checks pass |
| Pinned Windows LSP | PASS valid generated-definition EntityDestroyed callback, negative read-only Id, absent Killer, mandatory assignment from optional Vector3 and wrong Entity callback type |
| Production packaging | PASS final deterministic package and Test-Release; 78 production C# files, no fixtures/native binaries and no identity change |
| Final clean bundle | PASS Windows/Linux actual load, all 65 example compilations, GUI/Player execution and teardown; candidate archive, not a release |
| Dedicated B3 64-point context/Vector3 allocation faults | PASS Windows 4.52 s, Linux 3.60 s, Linux sanitizer 15.91 s; exact reservation refund/retirement convergence |
| Corrected Windows/Linux live complement | PASS never-active, immediate/deferred/component/inactive and bookkeeping-ghost deletion, original IDs and nil native Position |
| Final cold-boot no-replay | PASS Windows/Linux; receipts below |
| Controlled actual Windows/Linux pool admission/reuse | PASS public Poolable.Initialize, GameManager.Retire, actual stack Push/Pop, same wrapper/new epoch and injected EnterPool throw |
| Final three-source normal/no-replay/watch-byte/watch-count/depth | PASS fresh Windows/Linux host runs; exact receipts below |
| Final off-thread source rejection | PASS Windows/Linux injected owner-thread violation fails the source closed |
| Final full source teardown | PASS Windows/Linux zero held watches, slots/text/pending accounting; disposable hosts stopped and prior D20 state restored |
| Final-source hosted CI/documentation | Pending source commit and final runs |

Both preliminary live fixtures exercise actual vetoed/completed Kill, direct alternate
Terminate without IsDestroyed, recursive same-entity Kill, registry churn,
Disconnect, rapid Spawn/Kill, same-ID replacement, delivery after retirement,
root replacement and no replay. Immediate/deferred native deletion includes a
destroyed original wrapper with surviving Rust bookkeeping; native-only snapshots
have nil Position. The 512-destruction burst certifies all actual removals while
shared producer limits drop excess work. Watches, pending reservations and bytes
converge. All original D20 stamp objects stay unchanged.

A task-owned foreign Terminate prefix makes B3 reject queued/new work while B1
and keyed D20 reads remain qualified. It is then removed. Exact task-owned entity,
network, registry and GameObject cleanup passes. Prior package/native/compiler/
hooks/config plus bounded world/database files are restored after normal disposable
host shutdown. No live proof is attributed to mocks or authenticated clients.

Windows receipt:
`D:/Sandbox/Codex/Entity1AStartup/evidence/entitydestroyed-20261009-032735-137/server.log`,
SHA256 `d7bafdaae4cff771e8da0f21305984ccdaa1d3230e24bb5f8af77ba168ecd101`.
Linux receipt:
`/root/codex/world-movement-20261003/evidence/entitydestroyed-20261009-032732-1552667/server.log`,
SHA256 `54eae941c30910e9d609eb5b1947a074d1b406bec559e83238738a8d3a43355b`.
Preliminary native runtime SHA256 is Windows
`38179274d8f0eb2d9ac1ca492014fab2e6dee17047ac2ab3a0885b7b7418c06b`, Linux
`8bd67dff50b6f944f47dd2c593a053137a0e962968ae7b5681fc3fd8945b8035`.
100-addon real-VM peak allocation is 7,985,480 bytes; final retention converges.
That measurement is not a latency SLA or justification to raise the VM default.

Refreshed Windows/Linux real-VM budget tests exercise 512 optional Position
attempts in one frame: 128 accepted and 384 rejected, with new-frame reset and
no-listener-demand probes. Position-budget rejection leaves otherwise valid
removal eligible with nil Position. These are controlled model/real-VM tests,
not evidence that Unity getters have a hard timing bound.

Actual pinned Windows LSP `1.70.0` SHA256 is
`89e9162cf7ab828ccd48af158a43e680f263586e53b21c0de5732718224940bd`.
Generated-definition positive analysis exits 0; the four negative cases exit 1.
Pre-complement deterministic production `.cszip` SHA256 is
`245e5961700b3ad5d35e598830cbb70605527da77f9175636e78ead4960b6eeb`.
Research/live fixture code is excluded from that package. The native complement
and pool fence change production source; this earlier hash is superseded by the
final production package below. No release archive or tag is published.

The subsequent exact-host never-active negative is preserved, not hidden by the
positive matrix. A qualified full Spawn followed by raw physical destruction of
an eligible never-active component produced no cancellation notification on
Windows or Linux. The initial token-only native source therefore lacked coverage.
Receipts are Windows
`D:/Sandbox/Codex/Entity1AStartup/evidence/entitydestroyed-20261009-035348-556/server.log`
SHA256 `0dfe6415998c3a11fc9a31593f53ec136e24b71d9ba82f230fc9f518004dda83`,
and Linux
`/root/codex/world-movement-20261003/evidence/entitydestroyed-20261009-035356-1564909/server.log`,
SHA256 `782aae57b42249b57beb27b28e16078caade60043c91ef686f574d6af597d97b`.
The correction adds only the bounded original-watch complement described above,
not forced activation, new public semantics, general Unity instrumentation or
registry polling. Corrected final-source evidence must close this negative gate
before an overall PASS is recorded.

### Corrected original-watch complement live evidence

The corrected fixture package SHA256 is
`024c2f4d06f6963a6fddea2112c0e93ddb12e03dbbb80ef0294f1f305cd56e20`.
Windows receipt:
`D:/Sandbox/Codex/Entity1AStartup/evidence/entitydestroyed-20261009-041644-834/server.log`,
SHA256 `8ddbf02be864ce44b9954ba78e72b7660bafb53010c0833fadaf5be35b74900e`,
1,021 startup watches. Linux receipt:
`/root/codex/world-movement-20261003/evidence/entitydestroyed-20261009-041652-1567331/server.log`,
SHA256 `df2af98572f0ee3be4de3c18a54301224609412f7ca91e9e7f9127489b0fec8b`,
1,003 startup watches.

These corrected runs verify eight exact original native-deletion IDs with nil
Position, including a fully qualified Spawn whose GameObject is inactive before
and after Spawn and never becomes active. Native physical deletion now produces
the missing event without a cancellation callback. Injected cancellation intent
on a still-live watch cannot release coverage; subsequent Kill/native deletion
remains observed. The 512-Kill burst, veto-bound Position work, cleanup and B3
foreign-patch rejection with unchanged B1/D20 all pass.

Bounded intake observations: Windows 9 turns/1,152 visited slots, maximum 128
visits/turn and measured maximum 0.34 ms; Linux 8 turns/1,024 slots, maximum 128
and 0.4517 ms. These are measured controlled-host results, not timing guarantees.
One thousand same-frame calls cannot exceed 128 visits. After source stop,
zero held watches/slots/text bytes and 1,000 calls produce zero visits/turns.
Actual weak-wrapper collection remains NOT OBSERVED in this live run; a native
fact is certified before further collection testing. Synthetic tests own weak-
collection branch evidence. Natural pooling remains NOT OBSERVED, not PASS.

Corrected native runtime SHA256: Windows
`d62bc1047ff0086806b946be4bdceb7a3cfc40df9612b3e619ead76cb651fb57`, Linux
`9a21691adabdae8672e9185d2ee6f23926a8b35ddc4c8cf980805d8c555be7d1`.
Manual/plugin retirement that unregisters/releases network authority and returns
a still-valid wrapper directly to a pool without Terminate exposed another gap:
native invalidity alone does not cover pool admission. The new third observer
certifies actual stack admission with original registry/network release, before
callbacks or reuse. Controlled public `Poolable.Initialize`/`GameManager.Retire`
and actual `Pop` same-object/new-epoch evidence passes separately below; natural
pooling remains NOT OBSERVED. Earlier positive matrices alone did not prove this
new fence or universal coverage.

### Controlled actual pool admission/reuse

The corrected loaded-body pool-pin fixture package SHA256 is
`82461c597b20d79186268a5c838da610380530b10337c3bd3f052c2f24a42949`.
Windows receipt:
`D:/Sandbox/Codex/Entity1AStartup/evidence/entitydestroyed-20261009-045627-763/server.log`,
SHA256 `1d0fc989c8f69947e5e57888fd94cc4e3a5159389ced72ee5ef00c1b871978a5`,
1,019 startup watches. Linux receipt:
`/root/codex/world-movement-20261003/evidence/entitydestroyed-20261009-045633-1572673/server.log`,
SHA256 `51c69d1fd895c87922e5f7ec4284de38461266db7865e856033bc17bf135d47e`,
1,004 startup watches.

These are controlled real-host paths, not fabricated pool success or mocks: a
task-owned plugin installs a genuine component using public `Poolable.Initialize`,
then `GameManager.Retire` performs actual stack admission and `Pop` returns the
same CLR wrapper and Unity instance ID. A later completed Spawn gives it a new
D20 epoch and fresh watch. The queued old snapshot retains its old Id and never
retargets; subsequent Kill/retirement yields no duplicate old-incarnation event.
Pooling mode 2 keeps the GameObject active, confirming that inactivity cannot be
required. Actual stack commitment remains a certified historical fact when an
injected later `EnterPool` callback throws. The actual `Pop` uses the original host
method. Scoped LIFO cleanup converges watch/poll/transport accounting to zero.
All 24 original Spawn stamps remain unchanged; B3 has three separate targets.
Natural unmodified-world pooling remains NOT OBSERVED, not this controlled PASS.

### Preserved intermediate cold-boot checks

No-replay cold boots pass with Windows receipt
`D:/Sandbox/Codex/Entity1AStartup/evidence/entitydestroyed-20261009-042325-676/server.log`,
SHA256 `572b5bf6170db21bce4099f6cc2f2843f7b2dd8de4c27b2d1133158297a7736c`,
and Linux receipt
`/root/codex/world-movement-20261003/evidence/entitydestroyed-20261009-042335-1567896/server.log`,
SHA256 `ddbc9fdea716854baa3ddd74cedcc98cdd4dbd71504536e10ae7208e08ef29ac`.
The subsequent watch/text-budget probes reached `FAULT_PASS` but their harness
asserted zero held watches before source stop. That fixture assertion was fixed
to stop before checking zero; the negative receipts at Windows `042421-450` and
Linux `042427-1568476` remain preserved and do not constitute final gate PASS.

### Final three-source host matrix

All eight fresh modes pass on Windows and Linux with the same fixture package
`82461c597b20d79186268a5c838da610380530b10337c3bd3f052c2f24a42949`,
three qualified B3 targets and all 24 D20 Spawn targets unchanged. Each table
suffix identifies `server.log` beneath the platform's evidence directory:

- Windows: `D:/Sandbox/Codex/Entity1AStartup/evidence/entitydestroyed-20261009-`
- Linux: `/root/codex/world-movement-20261003/evidence/entitydestroyed-20261009-`

| Mode | Windows suffix / server.log SHA256 | Linux suffix / server.log SHA256 |
|---|---|---|
| Normal | `045814-863` / `5b603c05ed06907fabd3615e3c66c3f10592a6189dfcad31a6f0d632a2e3c3b9` | `045822-1572863` / `48885547c069ecc6d479a5f513c9589c934e34516185061c850e9b5febc7e86d` |
| No replay | `045924-753` / `fef897c0ea69cab973cce8012996ed4214624fddea93504a10ee01360aa1fc49` | `045923-1572997` / `fb3b1587e440bedfa04d13f8a36f60c0a1aa4dc642b73ebf84077ecc1eae4722` |
| Watch text budget | `050021-413` / `3b424bccade9d4ef7571e6ec46ba21b24ee87270d4d0f60d7c336dea49dd40fe` | `050014-1573193` / `4c9520f0d4cf7122ca4fe4b668f19d70ced261d59beec6d3e3c3d94d7cf6487c` |
| Watch count | `050118-063` / `d2fc691066ee76ad8c657ede394c0a7df756a31cb17070e32d2a03bb748a51ae` | `050104-1573318` / `0d8d2fdb491989d971416ce071ecd5a9b9512ff063f68c2ba7be474b741a6569` |
| Removal depth | `050215-773` / `2442a2ecc536c9aa638a243c71efa24082e9a00ef2af33902242dcc5d6dbe36d` | `050155-1573481` / `e4396b2aa28d23527a437f267ec6d864d74f7e929ce55254b35ea24cfcd1c8a4` |
| Off-thread rejection | `050313-374` / `61629fbeb2b49e68ee90b7cc9147fd41ef1f1e340387acaf277412aebf6b5e9c` | `050246-1573795` / `9c6d356e84af9c0c0b553bb82cbf412740e1b3fce2f76ff83bd718bc15ac1956` |
| Teardown | `050411-096` / `7d417e40a586cf05de9b46d98859d9d326ae74a7462795685c26768f0071d4f6` | `050336-1574189` / `d77277b893701504e4e8471e689ab88d7767d98152316358de808eb1e2521b58` |
| Actual controlled pool | `045627-763` / `1d0fc989c8f69947e5e57888fd94cc4e3a5159389ced72ee5ef00c1b871978a5` | `045633-1572673` / `51c69d1fd895c87922e5f7ec4284de38461266db7865e856033bc17bf135d47e` |

Final normal polling measures Windows 9 turns/1,152 visits, maximum 128 per
turn and 0.3132 ms; Linux 8 turns/1,024 visits, maximum 128 and 0.4931 ms.
These are controlled empirical timings, not public compatibility guarantees.
Source stop yields zero watch count, slot count and snapshot-text charge in all
eight modes. Task-owned D20 state is restored after every run; no disposable
server remains running. Injected quota/owner-thread/depth fault gates demonstrate
fail-closed behavior and bounded cleanup, not maximum physical heap/RSS capacity
qualification. The 64-MiB bound charges retained text only.

The qualified real-pool fence covers manual RetireCell/RetireAllChildren-style
admission when original network authority is released and original registry
occupancy is absent. Pushing a still-network-registered object is misuse and does
not prove world removal or generate a destruction event. Only qualified original
epochs are eligible; no global Entity scan or retrospective backlog is added.

The B3 codec boundary fixture found short Single round-trip text could exceed the
exact Luau Vector3 component bound. B3 emits binary32-as-binary64 decimals and
rejects incompatible snapshot decimals before allocation. Existing Gameplay A/B1
parsing and Vector3 semantics are preserved, not weakened.

### Final production packaging and clean install

The final production `.cszip` SHA256 is
`223c887a2839f62cf66f26f39f7e22def67441d8ad60501c6043530c13edf22d`.
Two builds are byte-identical. Its 78 C# sources contain no test/live fixtures or
native binaries. Final Test-Release, API/architecture checks, SDK10 generated
metadata verification and full tooling pass without an identity change.

Windows DLL import/dependency checks pass for the runtime, compiler worker and
storage worker. Actual Windows/Linux clean bundles load, compile all 65 examples,
execute GUI/Player examples and tear down successfully. The worker archives record
uncommitted candidate provenance honestly; these are clean-install qualification
artifacts, not a published release or a tag. Earlier packaging receipts are
historical, not the final source package.

## Identity and handoff

API `0.6.5-experimental`, ABI `1.5`, provider `1.2`, schema `1`, package `0.5.0`
and pinned Luau remain unchanged. EntityDestroyed/context have the 0.6.5 development
introduction and are not in published v0.5.0. The frozen v0.6.0 candidate's evidence
does not qualify this feature. No release/tag, damage/killer/inventory API, loot
policy or generic hook is authorized.

Implementation/evidence commit, final tested SHA and hosted URLs will be recorded
after required gates pass. B2 remains a separate EntityDied task: cause-independent
removal proves neither a death transition nor killer attribution.
