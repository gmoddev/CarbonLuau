# Gameplay B1: EntitySpawned qualification

Starting source: `2efab56f9a18102931d0ac3bde6d84f050f356c8`, clean `main`.
Verdict: **GAMEPLAY-B1 PASS — ENTITY SPAWN SIGNAL QUALIFIED** within the recorded
exact-host server observation envelope. Windows/Linux native, managed, real-VM,
live, 100-addon, sanitizer and final-source hosted gates PASS.
No package release/tag. Development API remains `0.6.5-experimental`.

Implementation/evidence commit: `df227a1d9c124d333d61052403a7c2f971724501`.
Supplemental live recursive-Spawn proof and public documentation, and final
tested source: `92050ac9ce5f8f6fc1a2587968adaa83f3f5bb04`.
The closing evidence-only commit changes no production, bootstrap, native,
metadata, test, runner or identity inputs from that tested source.

## Final-source hosted qualification

All final runs below tested `92050ac9ce5f8f6fc1a2587968adaa83f3f5bb04`:

- [CarbonLuau validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37887345818):
  PASS, Windows and Ubuntu full native/runtime/worker/persistence, deterministic
  packaging and clean installation, plus hosted ASan/UBSan/leak coverage.
- [Full tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/37887345798):
  PASS, Windows, Ubuntu and macOS tooling; no extra Rust-host support claim.
- [Documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/37887345808): PASS.

The implementation-source [baseline contracts](https://github.com/gmoddev/CarbonLuau/actions/runs/37886712634)
and [full validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37886712736)
also PASS at `df227a1`; supplemental changes do not modify baseline contract
inputs or production code. Commits use the configured GitHub no-reply author
and committer identity and are pushed to `main`. No qualification server,
compiler or storage worker remains running; unrelated infrastructure is preserved.

## Public observation contract

The only new public member is `Workspace.EntitySpawned`, using existing
Signal:Connect and Connection:Disconnect. It delivers a domain-bound live Entity
only from D20's completed outermost virtual Spawn, after catalog enrollment and
startup qualification. No early hook, startup enumeration or ID lookup invents
event history. No new Harmony patch or Entity identity model is introduced.

Per-delivery retention is a canonical EntityLifetimeModel.Binding whose record
holds only a weak Rust identity. Its exact token/epoch/observation generation,
network-ID consistency and original committed domain publication are validated
before callback entry through existing Entity read validation. A reused ID or
new epoch cannot receive an old callback. Event-created proxies retain normal
D20 live properties/equality and domain-bound validity; subsequent races can
raise controlled errors.

"No ID re-resolution" does not remove D20's keyed reciprocal-occupancy check:
the existing read validator may verify that the captured ID still names the
original object. It must not call public lookup to select another lifetime.

Player and Entity streams share Gameplay A's 128 captures/frame, 128 deliveries/
frame, 32/domain/frame, 4096 visits/frame, 512 global held reservations, 2048-byte
payload and 2-MiB double-copy accounting. Existing shared listener/queue limits
remain. Entity payloads allow only canonical tokens/IDs and <=512 UTF-8 prefab;
the existing private native reservation/acknowledgement path releases on every
consumption, cancellation, rejection, error, retirement or VM destruction.
No separate scheduler, historical replay, retry queue or new knobs.

Focused real-VM tests PASS both systems: exact completion, no ID re-resolution at
delivery, rapid Kill, same-ID/new object and same-object/new epoch, failed Spawn,
failed cold/nested publication, idempotent cancellation, failed/successful root
replacement, callback throw/yield/VM-fatal timeout and resource teardown.
100-addon shared-VM tests PASS: saturated root plus 100 single-listener addons,
25 frames, 512 producer burst, 10 replacements, provider/native-held cancellation,
all domains progressed, peak VM 8,234,024 bytes and zero final holds/bytes/VMs.
Recursive epochs and unobserved startup cannot manufacture completion in the
model. Mixed Player/Entity streams share producer limits; commands/deferred
callbacks progress beside Entity saturation. Required dependency replacement,
loss/restoration and optional no-hot-rebind are exercised directly with Entity
listeners in the real shared VM, including already-native-queued cancellation.

Live controlled Windows/Linux Carbon PASS uses the exact Gameplay A/Entity-1A
Rust build 25653776, protocol 2634.289.1, Windows Carbon 2.0.262.0/8a81d70, Linux
Carbon 2.0.261.0/c74c4ca, Harmony 2.4.2.0 binary/hook tuples. Both run actual
wooden-box Spawn/Kill, the known failed same-object retry, 512 actual Spawns,
original source->managed reservation->native deferred Lua, Entity fields/equality,
early-hook exclusion, root replacement/no replay and unchanged 24 D20 stamps.

Final live package SHA256 `85fba817154afe5e27871b707d6c7a94e2ab0e7528b5d91ccec0ca3eea173ebd`.
Windows receipt: `D:/Sandbox/Codex/Entity1AStartup/evidence/entityspawn-20261009-010820-340/server.log`,
SHA256 `5ea64b2ac183ce8fa189a6d6818aefa72fa3b65a8da93427fd32b6ce511c788c`.
Linux receipt: `/root/codex/world-movement-20261003/evidence/entityspawn-20261009-010816-1539917/server.log`,
SHA256 `1fcf912f4729d5284b4176ef1e2d5dfbec4f9d5d8acb51bb0b95439ddb91cffa`.
Both prove no new live entities, zero reservations, normal shutdown and restoration
of prior package/native/compiler/hooks/config plus 20 database/world files.
No test server is left running. Authenticated-client behavior is not claimed.

Fresh native artifacts: Windows
`1d6ffaf0a990e1ec70b114a9f1c6bc382541a639208c1037fcca9d56086abf37`,
Linux `d160d8498c4d81692a236c93c50b94306bb42114c6e688397f14b7a2870f25f8`.
Affected Linux ASan/UBSan/leak detection RuntimeAllocationFaults and Discovery
PASS, 15.41/0.76 seconds, without suppressions. Full tooling/17 preview goldens and
generated richer Entity callback definitions PASS. ABI 1.5, provider 1.2, schema 1,
package 0.5.0 and Luau pin remain unchanged.

## Evidence matrix and limits

| Gate | Result and attribution |
|---|---|
| Windows exact Carbon | PASS final live fixture, 1,019 startup keyed completions; early hook has zero pending event, full return has one; actual Spawn/Kill/retry, 512-Spawn burst and one-shot actual recursive Spawn |
| Linux exact Carbon | PASS final live fixture, 1,005 startup keyed completions; same public end-to-end matrix |
| Windows native | PASS all 23 CTests, 148.91 s; current bridge/bootstrap and Entity codec tests |
| Linux native | PASS all 24 CTests, 117.61 s |
| Windows/Linux managed | PASS full runtime suites, including Gameplay A, Player, GUI, commands, discovery, persistence, addons A-F and existing execution/module regression paths |
| Windows/Linux focused real VM | PASS final B1 lifetime, publication, dependency, mixed-stream and 100-addon tests; host objects/transitions are explicitly synthetic |
| Linux ASan/UBSan/leaks | PASS affected RuntimeAllocationFaults/DiscoveryFacade, including malformed Entity payloads; no suppressions |
| Tooling | PASS full tooling suite and 17 preview goldens; generated metadata/definitions/docs reproducible |
| Pinned LSP 1.70.0 | PASS strict positive Entity inference; wrong Player callback, read-only Id assignment and unknown Cause field all rejected |
| API/architecture/docs/identity/package | PASS local checks and deterministic production packaging; test fixtures excluded |
| Hosted final source | PASS all final runs linked above; separate from live host proof |

The 24-method observer/ordering proof and exact source/IL identities are reused
from [Entity-1A](WorldEntityFoundation1A-Validation.md), not inferred from hook
names. B1 adds one call after `EntityLifetimes.CompleteSpawn` succeeds in
`ExitEntityFinalizer`; it adds no target, prefix, postfix, finalizer or transpiler.
Both current live runs verify all original patch-stamp objects remain unchanged.
Selected exact binary hashes:

| Binary | Windows SHA256 | Linux SHA256 |
|---|---|---|
| Rust Assembly-CSharp | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| Carbon core | `b8873f74343de09c49ea285aa981b1b1e3a53d7f193a7d6c38b3de4bd467abe4` | `44d0e88a7c8c9c45f8a94a67896cb7a8da4f0f4ac73e4bdc85fba1d40f5584f4` |
| Harmony | `77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d` | same |

Windows B1 uses the qualified older paired hooks (Community
`1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3`,
Oxide `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d`),
not the worker's unrelated installed drift. The runner restores that installed
state after each disposable run. Linux hooks are the pinned Entity-1A tuple.

Historical exact-host negative first-install hotload, same-process observer
unload/reload, rejected self-updated host and failed outer-tail/network paths
remain [Entity-1A evidence](WorldEntityFoundation1A-Validation.md#live-qualification-and-negative-evidence).
Their unchanged source gate is reused, not claimed as new B1 live runs.
B1 additionally performs a current exact-host negative probe on both systems:
on a fresh actual Unity frame it queues a real successful Spawn, then injects
one recursive call to the same actor's actual virtual Spawn from the existing
early Carbon hook. The normal D20 chain checker reports `Spawn nested or
base-chain mismatch`; no observer patch is added. The source becomes unavailable,
the old queued callback is suppressed, and new EntitySpawned registration and
keyed lookup reject. Actors are killed, reservations converge to zero and the
disposable process quits. This is injected **real host execution**, not evidence
that Rust spontaneously recurses in normal operation. Model/real-VM tests also
cover unavailable registration and recursive/stale admission.
Natural pooled reuse and successful reuse of
an already-spawned Rust object were not observed; same-ID/new-object and
same-object/new-epoch suppression are controlled model cases, not live claims.
Provider unload/dependency reconstruction and callback faults are real registry,
native queue and VM tests with synthetic host transitions, not authenticated
client or external plugin traces. No assumption about Entity death semantics
follows from this evidence.

Shared listener bounds are 128/signal and 256/domain; pending event queue remains
256/domain. Global 128/frame captures/deliveries and 32/domain deliveries are not
multiplied by addon count. Reservations retain at most 512 bounded binding records
with weak host targets, in addition to the fixed transport charge; Lua callback
roots/results remain inside the existing 64-MiB shared VM allocator. The measured
8,234,024-byte peak does not justify raising that default. Stress establishes
progress and bounded service, not a callback latency SLA or a whole-process heap
limit. Native rejection, stale admission and overload are counted without replay.

Full regression receipts are `b1-native-full.log` and `b1-managed-full.log` under
`C:/Sandbox/Codex/GameplayA-20261008` and `/root/codex/gameplay-a20261008`.
Focused final Windows output is `b1-managed-focused.log`; Linux focused output
was captured by the invoking task. LSP scripts/receipts are in the Windows
`SignalTypeEvidence` directory. Generated definitions SHA256 is
`18328f22afdfaf233757b3a7d0957626fcacdfc5099352012be47c846d79b212`.

The initial private worker/native-codec fixture failures were test configuration:
missing old dependency-test source in the Windows cache, incorrect SQLite mount,
and a Player assertion in an Entity codec fixture. Real-VM tests exposed a Lua
fixture string-concatenation typo. First live compile exposed a checked long-to-
ulong host-ID conversion in the production adapter; fixed and both hosts rerun.
Final extra tests also corrected initialization-only command registration and
the existing registry state's spelling (`Blocked`), without production changes.
One Windows full regression launch used the SSH home directory; rerunning from
the extracted source root passed the existing persistence example gate.
No existing lifetime/publication/resource invariant was weakened to obtain PASS.

B2 handoff: EntityDied/EntityDestroyed require separately qualified actual
transition/retirement evidence. B1's completion or private epoch retirement is
not death/destruction proof. No damage,inventory,loot/policy or B2 work began.
