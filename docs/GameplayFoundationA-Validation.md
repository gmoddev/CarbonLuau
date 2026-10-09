# Gameplay Foundation A: Player lifecycle qualification

Verdict: **GAMEPLAY-A PASS — PLAYER LIFECYCLE SIGNALS QUALIFIED** within the
recorded exact-host server observation envelope. Local implementation, live
public dispatch, native/managed, dependency/resource, tooling, clean install and
final implementation-source hosted CI PASS. Authenticated-client observations
remain explicitly UNQUALIFIED. No release or tag.

Original qualified starting source: `fc71db4ddb2053a98ad3fd65811e64b58f75b150`.
The user-requested portable pause checkpoint was
`22b6ca74d50632fe2459107093cb48399ab01a57`; the resumed checkout was clean.
All subsequent changes are Gameplay A. Final tested implementation source:
`5c96fa1aa527535f999b509db00a6e8e6c2d11d3`. The closing evidence commit changes
only this record and does not alter its tested runtime/tooling inputs.

## Final implementation-source hosted CI

All runs below tested `5c96fa1aa527535f999b509db00a6e8e6c2d11d3`:

- [CarbonLuau validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37881419445):
  PASS, Windows and Ubuntu full native/runtime/worker/persistence/packaging lanes
  plus hosted ASan/UBSan/leak fixtures.
- [Full tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/37881419415):
  PASS, Windows, Ubuntu and macOS tooling lanes. Tooling platforms do not imply
  additional Rust server support.
- [Tooling baseline contracts](https://github.com/gmoddev/CarbonLuau/actions/runs/37881419470): PASS.
- [Hosted documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/37881419420): PASS.

Implementation/evidence was committed and pushed to `main` using the configured
GitHub no-reply author and committer identity. The tracked worktree was clean
and remote HEAD matched at implementation-source closure. Test servers and
qualification containers were stopped; caches/receipts remain recoverable.

[D23](Invariants.md#d23--gameplay-a-player-lifecycle-signals) owns the resolved
observation contract. The [supplied architecture](GameplayEventsArchitecture-Research.md)
remains supporting research; later events/policies are not adopted wholesale.

## Implemented public surface

```luau
local Players = game:GetService("Players")
Players.PlayerDied:Connect(function(Player, Context) end)
Players.PlayerSpawned:Connect(function(Player, Context) end)
```

The existing Signal/Connection implementation, resource ownership and scheduler
are reused. Added/Removing, command and GUI dispatch remain unchanged.
Death context is a frozen table containing only optional Position: Vector3,
Killer: Player, KillerId: string. Spawn context has only optional Position:
Vector3. No constructor, raw HitInfo/Unity/Rust object, cause, weapon, damage
type, public life epoch, generic hook string or new Events service is exposed.

Position is a qualified completion-time root world observation, not a later
read or client pose. Direct tracked non-NPC BasePlayer initiators supply exact
captured D11 killer lifetimes. Environmental/null, indirect owner, NPC and
unavailable provenance give nil, without a virtual initiator-owner walk or
delivery-time UserId re-resolution.

Queued delivery revalidates its original domain/listener/publication reservation
and victim connection. Disconnect/reconnect never transfers an old event to the
new connection. Killer disconnect does not cancel an otherwise valid victim
observation; its old facade remains old. Registration, replacement and recovery
do not synthesize history.

## Exact-host A0 proof

Both hosts use Rust build `25653776`, protocol `2634.289.1`, revision `166494`,
Harmony `2.4.2.0`. Windows Carbon: `2.0.262.0 / 8a81d70`; Linux:
`2.0.261.0 / c74c4ca`. There is no unrestricted future-build support claim.

| Binary | Windows SHA-256 | Linux SHA-256 |
|---|---|---|
| Assembly-CSharp.dll | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| Carbon.Common.dll | `0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a` | `5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb` |
| Carbon.Startup.dll | `b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28` | `7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0` |
| Carbon.Hooks.Community.dll | `1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3` | `4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343` |
| Carbon.Hooks.Oxide.dll | `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d` | `b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764` |

Existing exact Carbon/Harmony/assembly MVID and hook hashes are also checked;
D20's observer source and 24-target topology were not changed.

Death proof: BasePlayer.Die `0x06001AEA` (210 bytes) returns on dead/wounded
paths before the OnPlayerDeath injection at IL00B8. That hook is vetoable and
precedes base Die IL00C0; it is not the event seam. BaseCombatEntity.Die
`0x0600149B` (337 bytes) sets health/lifestate before OnEntityDeath IL0028,
which still precedes the complete OnDied tail. BasePlayer.OnDied
`0x06001ABD` is 2,861 bytes. Full normal original completion plus the unique
base-death return marker and final terminal state establishes the narrow event.
Same-player nesting poisons capture; a skipped/throwing original publishes none.

Spawn proof: ServerMgr.SpawnNewPlayer `0x06007A86` creates the initial dead
Player. PlayerInit `0x06001A9D` binds Connection.player and the exact Carbon
IOnPlayerConnected/D11 path before initial Respawn. Respawn `0x06001ABF`
calls RespawnAt `0x06001ABE` (618 bytes), whose exact three parameters are
Vector3, Quaternion and BaseEntity. Its game-mode early refusal returns before
progress; a successful path advances respawnId and establishes eligible alive
state. OnPlayerRespawned IL024F still precedes mission/injury work and full
return IL0269. The event therefore observes full normal original completion,
not hook notification or a dead-to-alive guess. Full alive resets are included.

Pinned Carbon.Startup adds BasePlayer.IPlayer and Cecil-serializes Rust into a
processed in-memory assembly, reindexing metadata operands. The first PE-only
gate correctly failed closed. [Normalized proof receipt](evidence/GameplayA-PublicizerReproduction.txt)
independently reproduces all eight Windows/Linux method comparisons: original
opcode/resolved-operand/EH parity and actual live raw/canonical/EH matches.
Original receipt SHA-256:
`d8aab1b60a4f2a32cee0fe091d40107e1645b6566e9c0bbf138690205431f598`.
The corrected gate separately pins backing files, transformation owner, exact
injected field, processed body hashes and exception-handler shapes.

Topology capture is bounded and zero-wait on both Harmony locks. Unchanged
serialized patch-record identity avoids repeated deserialization; drift disables
the affected source. Wrong-thread guards touch no host, VM, logger or frame
collection; atomic invalidation/diagnostics are drained on existing owner paths.

## Live server qualification

These are actual Rust/Carbon server calls with constructed Network.Connection
objects, not mocks or authenticated Steam clients. The isolated actor/account
caps are five/four, scalar records 128, corpse tracking 32, and test-only cleanup
realm inspection 4,096. This inspection is not a production world scan.

PASS on both hosts: actual initial activation, normal death/respawn, alive full
reset, vetoed/repeated death, wound/revive, wounded final death, suicide,
direct human attribution, indirect nil attribution, sleeper reconnect with fresh
D11 identity, NPC exclusion, early activation override, game-mode refusal,
throwing full-method tail, nested death, off-thread null admission without
Rust/Unity access, foreign original skip, optional-hook demand and D20 stamps.

Public integration restores the original production admission/receiver, with an
independent trace spy. Real host operations enqueue bounded managed/native work;
Host.Attempted does not advance synchronously. Later Host.Drain delivers actual
public Luau callbacks. Frozen contexts, real initial/death/respawn, queued
Connection:Disconnect, disconnect-before-admission, reused account/fresh proxy,
self-disconnect and zero retained reservation bytes PASS on both hosts.

Final combined fixture package:
`37ab27577b4494b6220a6b4e92d8bc2e131e7398b22096304ad380f05373e07f`.

| Evidence | Receipt | SHA-256 |
|---|---|---|
| Windows live | D:/Sandbox/Codex/Entity1AStartup/evidence/gameplay-host-20261008-234200-697/server.log | `cc8d9066caf9fe15b7653119c364d0a2630fa03ec59de37c9a2a4f1113e0566f` |
| Linux live | /root/codex/world-movement-20261003/evidence/gameplay-host-20261008-234158-1529049/server.log | `be3c496282e4e29b652a83b747cd2a79081c8a4c9c02f51e5089cdbd85444fc8` |

Both verify no new live entities, normal shutdown and exact restoration of 20
database/companion/world-save/navmesh files plus installed package/native/compiler/
hooks/config. Windows uses the saved qualified hook tuple temporarily; prior
installed drift is restored, not silently blessed. No test server remains running.

## Corrections and preserved negative evidence

- Lua-owned callback roots previously escaped connect/disconnect/destroy rollback.
  The bounded private holder now snapshots/restores under existing publication;
  retired domains clear it. GUI disconnect no longer retains a poisoned local
  active flag. Ordinary user table/global mutations are not rolled back.
- Managed intake ignored failed ToNative transfer and could leak a dequeued
  reservation on Runtime.Event exceptions. Exact transfer/refund tests now PASS
  for both flush paths, disposed-domain/runtime and native rejection.
- Off-thread rejection previously logged and touched ordinary frame state.
  Owner guards and bounded atomic diagnostics correct that production defect.
- Fixture OnEntitySpawned alone created an unqualified Carbon 0/1 pair. Requesting
  the existing known 1/1 hook pair fixes the fixture without changing D20.
- Fixture reflection initially selected an ambiguous ProcessMissionEvent
  overload. Exact target signature and full-tail throw tests now PASS.
- Killing an uninitialized vetoed-spawn prefab can fail after logical retirement
  but before Unity disposal. Only its verified original task GameObject is
  discarded, with a warning accepted only after physical invalidation and the
  no-orphans check. No fake initialization or production destruction API exists.
- Corpse Kill can create bags, and a late enableSaving field write leaves save
  list entries. Exact tracked corpses use EnableSaving(false)/blockBagDrop.
  The earlier Linux actor runs increased the disposable save count; all versions
  were backed up and the proven pre-actor 22:27 save restored before final runs.
  Backup: /root/codex/gameplay-a20261008/world-before-orphan-cleanup-20261008.
  Restored active SHA: `d5d02a37c5ac99f7b71c4f151d1c2b9cd34472442221874662faa0e51ba1c785`.
- Public fixture counter signedness and unrelated legacy callback output caused
  false failures. All callback statuses/bounds still validate; only exact
  fixture PUBLIC_ markers drive expected-output/no-replay assertions.
- Current-version assertions were stale after the selected 0.6.5 assignment.
  Historical SinceApi declarations were preserved.
- Linux full regression initially used overlayfs /tmp, rejected by the existing
  storage policy. The existing ext4-backed test-root override fixes the harness;
  no persistence policy or budget was weakened.

The callback-root counterexample affects the frozen v0.6.0 candidate's uncovered
publication case. Preserve its historical World/Entity evidence, but backport/
requalify the correction before publishing that older candidate. It does not
qualify Gameplay A or invalidate unrelated historical Windows evidence.

## Runtime, resources and tooling

| Gate | Result |
|---|---|
| Windows full native CTest | 23/23 PASS, 148.12 s |
| Linux full native CTest | 24/24 PASS, 120.74 s |
| Linux ASan/UBSan/leak detection | Affected allocation/publication/event and Discovery tests 2/2 PASS, 19.11 s; no suppression |
| Windows/Linux full managed runtime | PASS, including Foundations A-G, GUI/Player/module, persistence and heap/fairness regressions |
| Publication/budget/public Signal fixtures | PASS both platforms |
| Required/optional dependency-specific Signal fixture | PASS Windows/Linux; required A1/A2 reconstruction, surviving optional A1 no-hotbind, exact foreign owner, cold/nested rollback and fresh-VM no replay |
| Windows adapter teardown | Both actual scheduler failure paths PASS with current stubs/workers |
| Windows deterministic packaging/clean install | PASS; 64 examples compiled, real GUI/Player execution and teardown |
| Linux deterministic packaging/clean install | PASS; 64 examples compiled, real GUI/Player execution and zero-VM teardown |
| API/architecture/identity/docs checks | PASS |
| Full tooling and actual generated declarations | PASS; 17 preview goldens/cleanup; no preview lifecycle simulation |
| Pinned LSP 1.70.0 | Actual generated signatures/inference PASS; wrong callback, mutation and unknown field rejected |
| Final implementation-source hosted CI/docs deployment | PASS; exact runs linked above |

Current fresh native hashes: Windows
`0ddcab77db85f0ccaad1d9cd1f96e1034514d3eadaf98aad0ae6a34c4c3b8f08`;
Linux `082e29bf1d155aa40c56269caae3c868d599b140bbe24638d5bea22192040a57`.
Embedded bootstrap SHA:
`bc7140d248204d445a5f75e4c1821c12b1f17973d0dbff70dd51e411ae2e3f90`.

Native receipts: Windows worker NativeQualification-20261008/windows-ctest.log;
Linux native-qualification-20261008/linux-ctest.log and linux-sanitize-ctest.log.
Managed/scale receipts: worker gameplay-scale-{windows,linux}-public.log,
managed-full-linux-public.log and Windows
NativeQualification-20261008/windows-full-managed-current-api.log.

Hard current policy: 128 captures/frame, 128 lifecycle deliveries/frame,
32/domain/frame, 4,096 visits/frame, 512 held reservations, 2,048 bytes/payload,
2-MiB conservative double-copy transport accounting. Existing 128/signal and
256/domain Player listener limits remain unchanged. Private acknowledgement 39
is inaccessible through the script host primitive; native-held work is charged
until consumption/destruction, even after managed cancellation. Malformed UTF-8,
truncated/oversized/unknown-kind/invalid-scalar/duplicate/stale payloads and
allocation failures are exercised. No replay, hidden retry or new scheduler.

Both platforms measured identical 100-addon shared-VM results: baseline
8,286,048 bytes, peak 8,924,200, drift 638,152; peak held 128 and charged transport
13,256 bytes. All 101 saturated domains progress by turn 26. Saturated root plus
100 single-listener addons: all progress over 25 frames, max observed gap one
frame, VM 7,867,256 bytes, accepted/released 6,528/6,528, final pending/bytes zero.
Provider unload, ten replacements, root replacement and full teardown PASS.
These are synthetic transitions and equivalent service-gap observations, not
isolated host latency percentiles or a callback SLA. Existing 64-MiB default
remains appropriate within this measured envelope; no D2/default increase.

Generated artifacts are implementation-owned JSON/definitions/docs/limits.
The actual output and full tooling suite use API 0.6.5 while retaining older
SinceApi values. LSP 1.70.0 SHA
`89e9162cf7ab828ccd48af158a43e680f263586e53b21c0de5732718224940bd`;
generated definitions SHA
`5df89901a355d10c22eec9034a535a1413a391cd354d6347f0aa81c148ad8517`.
Synthetic and actual positive/negative scripts are retained in worker
SignalTypeEvidence; generated artifacts are reproducible, not hand-authored.

## Scope, identities and next phase

The user selected development API `0.6.5-experimental`. Package/tag remain
`0.5.0/v0.5.0`, ABI 1.5, provider 1.2, schema 1, Luau
`c6b830185af962c82003f86784e2fe036357c830`. No stable API or new publication.
A future Gameplay A package should follow the selected 0.6.5 scope and receive
its own final-source artifact qualification; this task creates no release/tag.

Authenticated Steam/client join, network receipt and client-observed behavior
remain UNQUALIFIED. Constructed exact-host server traces do not claim otherwise.
No Entity/inventory/loot signals, policies, generic hooks, broad Unity
instrumentation, world mutation surface or Gameplay B work began.

Workers/evidence remain under C:/Sandbox/Codex/GameplayA-20261008 and
/root/codex/gameplay-a20261008. Use DockerPC/BigVPS aliases and bounded existing
worker policy; preserve evidence/unrelated infrastructure. Next implementation
phase is Gameplay B only after a separate scoped request; no automatic follow-on.
