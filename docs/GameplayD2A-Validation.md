# Gameplay D2A — private barrel loot replacement qualification

Status: **GAMEPLAY-D2A PASS — PRIVATE STRICT REPLACE QUALIFIED**.
Starting main: `35701d5c7986f863550c2d92300d48fa2793d44d`.

The supplied [D0 research](GameplayD0Architecture-Research.md) and preserved
[C/D research](GameplayCDArchitecture-Research.md) are evidence, not implemented
Loot APIs. The authorized phase first proves one exact prefab on the qualified
Windows/Linux hosts, then conditionally implements private D1/D2A only.
`Enum.DropMode` remains the future naming direction. No public Loot service,
enum, C2/B2 work, version change, tag or release is authorized here.

## Exact-host inspection — newly measured

Rust build 25653776 / protocol 2634.289.1 / revision 166494, as routed by B3;
current worker assembly hashes were rechecked against that qualification.
Windows Carbon 2.0.262.0 / 8a81d70 and Linux Carbon 2.0.261.0 / c74c4ca remain
the qualified tuples, not an unrestricted future-host claim.

| Input | Windows SHA256 | Linux SHA256 |
|---|---|---|
| Assembly-CSharp | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| Carbon.dll | `b8873f74343de09c49ea285aa981b1b1e3a53d7f193a7d6c38b3de4bd467abe4` | `44d0e88a7c8c9c45f8a94a67896cb7a8da4f0f4ac73e4bdc85fba1d40f5584f4` |

Backing Rust MVIDs: Windows `c1c11bd1-baa1-4d85-b64e-1d4a4664d62e`;
Linux `616082a0-36f4-4680-a1ab-efc5766796cc`.
Read-only reproducible inspection: `tools/Research-BarrelLootHost.ps1`.
Raw proprietary IL remains in the external task receipts, not public source.

New backing IL agrees across both platforms on the candidate destruction chain:

1. `LootContainer.OnDied` (`0x06005773`) calls `StorageContainer.OnDied`.
2. Storage's virtual `DropItems` dispatch reaches `LootContainer.DropItems`
   (`0x06005772`), then the Storage wrapper (`0x06003700`).
3. Static `StorageContainer.DropItems(IItemContainerEntity, BaseEntity)`
   (`0x06003701`) contains the interface bonus call at `IL_0053`, generic
   `DropUtil.DropItems` at `IL_005f`, and alternative bag/container drop at
   `IL_0093`. This common payout cut is qualified by the private adapter and
   physical receipts below, not by the method name alone.
4. The later inherited OnDied tail performs normal world-removal work; suppressing
   the payout method must not skip that tail or treat attempted Kill as success.

## Negative evidence and scope checks

- `OnContainerDropItems` alone is incomplete: bonus scrap runs earlier and an
  alternative container/bag branch does not reach the generic utility hook.
- `OnBonusItemDrop` follows creation and modifier accounting; its veto is not a
  resource-safe precreation suppression primitive.
- `GenerateScrap` (`0x0600576c`) can fall back from failed inventory placement
  (`IL_0037`) to `Item.Drop` (`IL_0059`) during fill. This separate generation-time
  leak is not magically suppressed at death. Strict Replace is a qualified
  destruction-payout policy, not retroactive reclamation of earlier world Items.
- `TryDropCorpse` runs before OnDied; Decay's debris tail follows the Storage
  payout. Exact prefab/component/asset settings must establish whether either
  can carry extra item payouts. A plain type name alone is insufficient.
- `Item.CreateWorldObject` initializes the WorldItem with the Item before Spawn,
  and repeats the reverse world association afterward. Follow-up inspection of
  `WorldItem.InitializeItem` corrected the initial assumption that no reverse
  association is set before Spawn: that method sets it at `IL_005d`, after first
  assigning the Item and attaching its dirty callback. An earlier initialization
  failure still cannot be diagnosed solely from a null reverse association.
  The private candidate therefore captures the genuine CreateEntity return
  before initialization, not a fabricated reference or query-time world scan.
- A vetoed Kill may follow a completed payout. Verification must keep physical
  payout and proven original-entity retirement separate, preserve partial effects,
  and never replay or claim successful complete replacement.

## Selected private architecture and numeric envelope

Only the exact plain LootContainer prefab `966676416` /
`assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab` is supported.
The actual prefab has Transform, Spawnable and LootContainer root components,
`scrapAmount=2`, `dropsLoot=true`, no deployable corpse, and an empty debris GUID.
Its already-populated InitShared component cache must be present and empty;
the adapter never calls the cold recursive Components getter. Modified settings,
missing original D20/B3 authority or unsupported topology make an occurrence
ineligible before COMMIT. Other barrel families are not inferred equivalent.

The three guarded AutoPatch targets are:

| Target | Purpose | Loaded pin |
|---|---|---|
| LootContainer.OnDied, `0x06005773`, 83 bytes, no EH | Capture one immutable committed rule/plan; final normal-return/B3 certification | Windows `a6851b9116507fcb7e054118e476e0cac8b6a51d39cd10e0824b19e68d0694af`; Linux `993ae54aa5e6c11444abee185f2fb90bd3c5be17d883b3357d88150bc0ad7329` |
| StorageContainer.DropItems static overload, `0x06003701`, 160 bytes, no EH | Suppress the complete stored/bonus/bag payout body inside that exact original source frame only | `14dbfbcb12139d86b2fd759c98b73020282e2b1460f1244769c782cffeddc0d0` |
| Item.CreateWorldObject, `0x06006a37`, 121 bytes, no EH | Capture the genuine CreateEntity return before item initialization or Spawn | `cd31acc55c0044763b9757597bcbe8d12fe5463254c743975466bdcb3bad2c08` |

The full Rust/Carbon/Harmony tuple stays pinned. OnDied's resolved opcode/member/
branch/EH contract matches both backing assemblies and loaded Windows evidence:
canonical SHA256 `480c2b0fa553f7aec908e18b142e463c6b90ecb955101153d3cba2e9cd513c43`,
31 instructions. Raw operand-token layouts differ by OS; no byte mismatch was
accepted merely because a hook name looked correct. The wrong Windows pin's
fail-closed receipt is preserved. Factory transpiling requires exactly one
CreateEntity call followed by the pinned local store, preserving its stack and
all original control flow. Three bounded topology checks reuse the existing
zero-wait Harmony lock order; no broad game/Unity patch is introduced.

Rules use existing inactive root/addon sessions and nested publication checkpoints.
Conflict preflight precedes native candidate commit and managed registrar publication.
Only the exact outgoing owner may replace its rule. Failed candidates/registrars
leave prior ownership unchanged; clearing/retirement removes future selection.
One descriptor is selected once for an admitted synchronous occurrence. Domain
publication changes do not switch or replay an already-committed physical batch.
This private phase has no Luau declaration binding or argument conversion to claim.

`LootRulePolicy` owns the deliberately narrow bounds: one global/domain rule for
this one prefab; eight copied immutable entries; only scrap and metal.fragments;
integer inclusive Min/Max 0..1000; 8000 maximum total quantity; eight maximum
created worlds/occurrence; eight admitted occurrences/actual Unity frame; one
in-flight policy frame. Maximum created worlds per frame is therefore 64.
Lookup visits at most the existing 129 root/addon sessions, not the world.
Position reuses the qualified maximum-65-record no-wait TRS reader. The limits
are private tuning policy, not author-visible public Loot guarantees. Max-size
live tests and the exact producer boundary qualify this initial envelope.

Before COMMIT, source/authority/resource rejection leaves vanilla untouched and
is diagnostic, not reported applied. Suppression is the COMMIT boundary. After
it, failure never falls back to vanilla or automatically replays. Successful
verification requires all planned exact returned Item UIDs/definitions/amounts,
reciprocal original world ownership, captured completed D20 epochs/IDs/prefabs,
and B3 original-source removal certification after the normal outer method.
Vetoed/incomplete death is partial even when custom Items already exist.

Completed host-owned world payouts survive later Drop/quantity failures; they
are not deleted as compensation. Failed original Spawn resources use the existing
qualified BeginSpawn attempt identity for cleanup provenance, never for admission.
Same-object new Spawn, ID/UID reuse, changed ownership or continuity loss rejects
old cleanup authority. Kill is followed by identity/terminal-state verification;
vetoed/unverified cleanup is counted, never claimed successful. Inaccessible
allocations from APIs that throw before returning remain D13's host-failure domain.
Arrays/references are released on finalizer exit; no history/world scan/retry queue
or strong host retention is added. Operator diagnostics are bounded aggregate counts.

## Measured tests and preserved negatives

Prototype physical receipts, package SHA256
`3617ddffed343dd53aebb12916c56634bb73bfd16156cc5cea7f16e799c54502`:

- Windows `D:\Sandbox\Codex\Entity1AStartup\evidence\loot-d2a-20261011-013051-534\server.log`,
  SHA256 `adc0f827d032ff26ce24eafc774bc6af1e5856f040a44840517fcd8afdfd4746`.
- Linux `/root/codex/world-movement-20261003/evidence/loot-d2a-20261011-013051-1676245/server.log`,
  SHA256 `bf668dc2debd5c780f6be04c333d2162004b509ca1c29b7ec0a7eddfac1543f3`.

Both measured actual vanilla contents, zero/custom replacement, nearby independent
barrels, modifier-driven extra scrap (baseline 0, boosted 2), vetoed retirement,
and fixed call-seam failures with ownership-ledger cleanup. Call-seam fault tests
are not labeled actual internal engine faults. Subsequent production-adapter live
qualification adds actual late Spawn failure, root/addon publication/provider
retirement, fatal VM reconstruction, maximum entries/amount and frame exhaustion,
plus actual trusted Item.amount mutation classified partial without compensation.

Private Windows/Mono Linux model tests PASS: immutable copies, controlled validation,
nested commit/rollback, active foreign-owner rejection, exact replacement conflicts,
registrar failure, unavailable source, 100-addon bounded selection, depth limits and
teardown. Windows full real-VM runtime regressions PASS against updated managed code
and unchanged qualified native binaries. Dedicated cleanup-epoch model checks cover
failed original, new same-object epoch, another model/host, and continuity loss.

Preserved negative receipts include the initial unassigned fixture-position compile
error; an incorrect positive-baseline bonus assertion (actual baseline is zero);
and fixture double-removal of zero-UID pooled Items because IsValid checks only
removeTime. Corrected fixture cleanup uses UID identity and never removes pooled
records again. Another fixture failed to track vanilla worlds after rule retirement;
it now follows only its original stored Items. None of these failed runs is PASS.
The wrong Windows loaded OnDied byte pin remains fail-closed evidence; resolved IL
comparison established the exact OS-specific correction rather than relaxing the guard.

## Final-source qualification

Final live fixture package SHA256:
`e278e326f6f158ee192cd017c498130a0750c4fc319d26b0bcba213e69439b2b`.

| Final-source live receipt | Path | SHA256 |
|---|---|---|
| Windows | `D:\Sandbox\Codex\Entity1AStartup\evidence\loot-d2a-20261011-030513-543\server.log` | `41db54fb05604fd13bdbbd42f697600fc33b707e23180a6592c23b7482db8247` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261011-030512-1693018/server.log` | `0d730f1a319e0faa83a78fa371eab77e08c329f956b9f6e96808733b5708a589` |

Both PASS the corrected private adapter: root/addon publication, failed/successful
replacement, provider retirement, actual fatal-VM reconstruction, empty and
maximum-size descriptor, original B1/B3 callback availability, actual-frame
producer admission/exhaustion, actual returned-Item mutation, actual boosted
bonus suppression, and ItemManager.Create null/throw dispatch. The late-Spawn
failure is genuinely executed through IOnSendNetworkUpdate; failed original
world/Item cleanup succeeds without admitting its failed epoch. Create dispatch
faults do not claim hidden internal-allocation cleanup. No authenticated client,
PowerLoss/Cloud/persistence rewrite or public Loot binding claim follows.

Windows and Mono Linux full real-VM managed regression runs both PASS after the
borrowed-handle correction below, including the original 2000-read gate, 100
atomic replacements, 200 failed candidates, publication/provider/package/GUI/
Player inventory and recovery coverage. Receipts:
`D:\Sandbox\Codex\LootD2A\full-runtime-windows-completion.log` and
`/root/codex/loot-d2a-20261011/full-runtime-linux-completion.log`.
New cleanup-epoch model PASS: 17281 checks. Native sources/bootstrap/metadata/
generated definitions/release identities are unchanged. Local API, architecture,
contract/drift and deterministic packaging checks PASS.

Independent read-only reviews confirmed the common-cut coverage and backing
semantic parity, and identified the required fixed world witness/epoch and
post-Kill UID revalidation. These are incorporated. Source-body and fixture
negatives remain evidence, not rewritten as successful first attempts.

### Broader regression correctness fix: borrowed Mono completion handles

The initial Linux full-runtime regression retained at
`/root/codex/loot-d2a-20261011/full-runtime-linux.log` failed with an unhandled
ObjectDisposedException in Mono Task completion, after persistence operation
tests had run. StorageProcess manually closed an APM Task's AsyncWaitHandle
after EndRead/EndWrite. [Pinned Mono Task completion source](https://github.com/mono/mono/blob/mono-6.8.0.105/mcs/class/referencesource/mscorlib/system/threading/Tasks/Task.cs)
publishes completed state before signaling;
[ManualResetEventSlim](https://github.com/mono/mono/blob/mono-6.8.0.105/mcs/class/referencesource/mscorlib/system/threading/ManualResetEventSlim.cs)
owns coordinated event disposal. Closing the exposed underlying handle bypassed
that coordination and could race the background signal. Windows/earlier CI PASS
did not rule out this timing defect.

The smallest transport fix removes manual borrowed-handle closure, preserving
matching EndRead/EndWrite and existing process reap/worker-generation ownership.
Only the detached transport worker waits for completion; stopping and the original
monotonic absolute request deadline are unchanged. Teardown still has its bounded
1000-ms completion allowance and retains ownership if unfinished. Initial sleep
polling passed safety tests but failed the existing 2000-read stress duration;
1-ms sleeping still suffered Windows timer granularity. The final finite Task.Wait
uses framework-managed signaling; non-Task APM results use a borrowed wait handle
without closing it. The 10-ms slice is a maximum wait, not forced per-stage delay.
No timer policy or acceptance deadline is changed.
Codec, SQLite/PERSIST+EXTRA, quotas, corruption, no-replay, worker protocol and
public semantics are untouched. `--storageasync` tests completion/expiry/stopping/
cleanup, fault observation, task kernel-handle avoidance and borrowed APM ownership.
Final full regressions and corrected hosted CI pass this correction; no failed
run is relabeled PASS.

Selected prefab is only
`assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab`.
Initial qualification-branch run 38120398304 compiled and passed native/model/C1
stages but failed the isolated PersistenceAdapterTests build because its Carbon
stubs lacked InitializeLootSource, StopLootSource and LootStatus. The no-op test
stubs were corrected; production code did not change. The fail-fast cancelled
Windows job is not a PASS. Tooling run 38120398301 passed all three platforms.
Test-only runners restore prior installed file hashes and own their isolated
localhost server processes. No authenticated-client outcome is claimed.

### Hosted closure and revision attribution

Implementation: `c06bc234ff461ec2d089ce4b48943b4fdd6da3af`.
Isolated adapter-test stub correction and final tested source:
`e636fe8a04db4f61f6df50143f4d553d7bacaf2c`.

- [Final-source core validation 38120908512](https://github.com/gmoddev/CarbonLuau/actions/runs/38120908512):
  PASS for Windows x64 and Linux x64 native/managed/runtime/loader tests,
  private loot publication and async completion tests, Entity lifetime/position/
  discovery models, C1 and public discovery bindings, persistence worker/lost-ack/
  adapter/combined-scale regressions, deterministic packaging, clean extraction/
  installation and import audit. ASan, UBSan, leak and lifecycle/fault tests PASS.
- [Final-source tooling 38120908504](https://github.com/gmoddev/CarbonLuau/actions/runs/38120908504):
  PASS on Windows, Linux and macOS tooling; macOS is not a server-runtime claim.

The closure commit changes evidence only. It reuses the tested source above under
Compatibility's documentation/evidence-only closure policy; no untested production
change is included. Final main publication follows these successful source gates.
All applicable private D2A gates are closed within the exact-host, one-prefab and
physical-failure envelope recorded here. Initial failures remain historical.
Task servers exited and prior installed package/native/compiler/hook/config hashes
were restored. Evidence/cache artifacts remain isolated outside release contents;
no task listener or sustained qualification process remains.

Package `0.5.0`, scripting API `0.6.5-experimental`, native ABI `1.5`, provider
protocol `1.2`, package schema `1` and the Luau pin are unchanged. No public Loot
binding, enum, API metadata or generated definition was added. No release/tag,
Gameplay C2 or B2 work follows from this PASS.

### Exact D3 handoff

Separately scope the smallest public declaration binding, metadata/tooling and
author documentation around the preferred positional
`Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)` shape. D3 must qualify
actual Luau argument conversion, publication, controlled errors and lifetime
behavior; this private PASS does not claim those bindings exist. Start with only
the qualified prefab, item identities and bounds above. Before-COMMIT rejection
leaves vanilla; after-COMMIT partial failures never fall back, replay or delete
accepted world payouts as compensation. Frame exhaustion remains ineligibility,
not a promise that every matching barrel in an unlimited burst is replaced.
Other prefabs, fill-only replacement, original-item collection, inventory Signals
and unrelated gameplay surfaces require separate qualification. D3 does not
authorize a release or a global enum redesign.
