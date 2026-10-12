# Gameplay D3 — public Loot Replace qualification

Status: **GAMEPLAY-D3 PASS — PUBLIC LOOT REPLACE QUALIFIED**.
Starting main: `77a5fcf4288192dd00ec7e5d2a64250c25bd524f`.

This phase adds only the public binding over the qualified
[D2A substrate](GameplayD2A-Validation.md). [D23](Invariants.md#d23--gameplay-a-player-lifecycle-signals)
owns semantics; [D0](history/gameplay/GameplayD0Architecture-Research.md#gameplayd0architecture-research) remains historical
research. No settled host-source investigation is repeated or weakened.

## Public contract and bounded conversion

`game:GetService("Loot") -> Loot` and
`Loot:RegisterDropRule(Prefab: string, Mode: DropMode, Drops: {LootDropEntry}) -> ()`.
Mode is only `Enum.DropMode.Replace`. Enum namespaces are frozen and the sealed
typed userdata uses the existing shared-value equality convention, including
cross-domain values. `GiveItemBehavior.InventoryOnly` is unchanged.

Exactly three arguments, no optional fields/default amounts/callbacks. A plain
dense zero-to-eight element array contains only plain Item/Min/Max records.
Raw iteration stops at the ninth array key or fourth record key. No metatable
or recursive graph traversal/user code is invoked. A private constant-work
CheckLootTable guard first limits array backing capacity to 64 slots and hash
capacity to 64 slots. Nine tables therefore expose at most 1152 backing slots
to raw iteration, including empty preallocated slots; huge empty/sparse histories
cannot evade the work bound. The guard reads only pinned LuauTable scalar
capacities via lua_topointer on a rooted table argument under serialized owner
execution; no allocation/callback/raw pointer crosses to managed code or scripts.
Strings are exact allowlisted
prefab/items; numbers are finite integers 0..1000 with Min <= Max, sum(Max)<=8000.
Only scalar strings (maximum 24 fields/1024 descriptor bytes) cross private
operation 42; managed parsing and D2A immutable validation independently enforce
the limits. The source Lua graph is neither retained nor frozen in place.

One rule/domain and one global owner. Existing nested D7 publication checkpoints
enlist operation 42, including foreign facade calls. Active-session registration
is rejected; candidate/root/addon initialization stages only. Failed module or
candidate publication discards the rule. Same outgoing owner may replace; foreign
owners conflict before native/managed publication. Retirement/provider unload/
VM recovery removes old authority; reconstruction declares from committed source,
not replay of physical effects.

## Physical contract and identities

Only `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab`
(966676416), scrap and metal.fragments. Eight worlds/occurrence, eight admitted
occurrences/actual Unity frame, one in-flight frame. D2A source/host/topology/
initial-startup qualification remains mandatory. Other configurations or exhausted
producer budget leave vanilla before COMMIT. Suppression starts nontransactional
COMMIT; later partial/indeterminate failure has no vanilla fallback, automatic
replay, compensation or false complete-success report. Accepted host-owned worlds
are not deleted as compensation. Earlier generation-time drops are not reclaimed.

Exact tuple inherited and rechecked for live tests: Rust 25653776, protocol
2634.289.1, revision 166494; Windows Carbon 2.0.262.0/8a81d70; Linux
2.0.261.0/c74c4ca; Harmony 2.4.2.0. D2A owns assembly/method/source pins.
Package 0.5.0, API 0.6.5-experimental, ABI 1.5, provider 1.2, schema 1 and
Luau pin remain unchanged. Private opcode addition changes no exported C ABI.

## Qualification receipts

Final Windows/Linux public real-VM and live physical receipts are recorded below.
Hosted sanitizer, tooling/preview, packaging/clean-install and final-source CI
all PASS at the implementation revision below. No local model result alone qualifies physical
Rust ownership. Final revision attribution follows those gates, not D2A PASS.

Final capacity-guarded live package:
`2b343cc0fd9913e1c30daeb292f5b5e2f334013a53fd42dfd926173ceacf39f6`.
The D3 selector disables private constructor staging. Root/addon and recovery
source snapshots call the actual public declaration and mutate/clear the original
Lua table afterward. Physical outcomes therefore exercise public conversion,
not a managed descriptor substituted into the rule registry.

| Newly measured final live source | Receipt | Log SHA256 | Native SHA256 |
|---|---|---|---|
| Windows | `D:\Sandbox\Codex\Entity1AStartup\evidence\loot-d2a-20261011-050000-612\server.log` | `6ee1ef8e6d1ec6731dc2d4843449bb4e9295a11ef3ed6f1dffc8d7cd93a7d8d9` | `f6d3bbf9b7aefc37c8a1309ddc4fde504a98e2dfab8146b2505faa223854705b` |
| Linux | `/root/codex/world-movement-20261003/evidence/loot-d2a-20261011-050010-1715062/server.log` | `2aa818568387c6b7273382305f4c373f7f02033f3cc50f0ef46c5439763497ea` | `463166f9055e35ad4b542ae7dcf6bb115527a19b016b1359fb54e56c859a797f` |

Both PASS public declarations/snapshot independence, min/constant/max/empty and
inclusive ranges, root failed/successful replacement, addon ownership/provider
unload, fatal VM reconstruction, real Scrap_Yield bonus suppression, source Kill
veto, real late Spawn exception, ItemManager.Create null/throw dispatch, actual
Item.amount interference, eight-world/eight-frame admission boundary and existing
B1/B3 callbacks. Final sampled inclusive range was Windows scrap 12 / fragments 22,
Linux scrap 9 / fragments 20. These are observations, not RNG distribution proof.
Stored/bonus/bag suppression coverage retains the unchanged D2A common-cut source
proof; no extra prefab or unsupported component configuration is inferred.
Cleanup failed counters remained zero for the qualified failed-Spawn cases;
already accepted worlds survived later partial failure and were released only by
the fixture's exact ownership ledger afterward, not production compensation.
No authenticated-client or unreturned internal allocation cleanup is claimed.

Both runners restored package/native/compiler/hooks/config hashes and stopped
their owned isolated server. Qualified host quit ends via Process.Kill (Windows
exit -1, Linux 137); this is the recorded shutdown path, not silent task leakage.
Compiler pins remain Windows `887a0f66d2a86257b729833920fd0205c380e8b55836ac9a67a43ad31cf89390`
and Linux `884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92`.

Windows/Linux corrected full real-VM regressions PASS in
`D:\Sandbox\Codex\LootD3\full-runtime-final.log` and
`/root/codex/loot-d3-20261011/full-runtime-final.log`: original persistence bounds,
Player Give/Take/C1, GUI, modules, publication, callbacks, providers, 100-addon
scale, 100 atomic replacements, 200 failed candidates and recovery. Final
capacity-guarded native smoke/fault fixtures PASS 5/5 per platform; final hosted
CI additionally repeats all native/managed and sanitizer/packaging gates.
Windows reused D2A's authorized D: sandbox; Linux used the established BigKVM
worker with an ext4-backed persistence fixture root. Builds used parallelism two
and retained pinned vendor/toolchain caches; no OS/security/timer settings changed.

Final public VM tests PASS on Windows and Mono/Linux in `LootD3/public-vm-final.log`
and `/root/codex/loot-d3-20261011/public-vm-final.log`. They cover exact arguments,
scalar types/NaN/infinity, huge/sparse/cyclic/metatable tables, 64-slot backing
boundary and rejection before resolver effects, empty/frozen/repeated records,
enum immutability/type/equality across modules/domains, committed declaration
rejection, nested/caught failed modules, stale/foreign service laundering, failed
root/addon/registrar replacement, source loss, provider/required dependency
lifecycle, fatal recovery and 100 actual addon domains in one pinned VM.
The 100-addon fixture verifies exactly one rule, 99 rejected committed callbacks,
fresh reconstruction authority, and 25-addon retirement per four providers with
zero remaining rule/callback work. These are synthetic host-state VM tests,
separate from the actual Rust physical receipts above.

Metadata comes only from production annotations and the existing authoritative
generator: exact service signature, nominal DropMode, nested Enum namespace,
entry alias, docs keys and SinceApi 0.6.5-experimental. Generated definitions have
no standalone DropMode global; GiveItemBehavior remains identical. Focused
generator/negative drift tests and local API/architecture/schema/release checks
PASS. Preview registration remains controlled UnsupportedPreviewApi, with no
mock payout/backend; final hosted tooling executes its explicit regression.

Independent source reviews of publication authority and pinned table storage
found no outstanding issue after the capacity correction. The scalar guard uses
the pinned VM's collectable-table layout only, under its existing owner thread;
no external object storage, user callback or pointer crosses the boundary.

### Preserved negative findings

The initial annotated bootstrap reached 68,603 bytes and exceeded the unchanged
64-KiB compiler request ceiling. Initial native facade smoke failed on both OSes.
Build embedding now omits metadata comments while preserving line breaks, with
an explicit executable-source ceiling check; tooling still reads all annotations.
The corrected five native facade/runtime/fault fixtures pass on both platforms.
No compiler protocol/deadline/heap/resource limit was increased.

Independent conversion review identified that a key-count cap alone does not
bound next over oversized empty table backing. The new capacity guard closes
that before any iteration, with adversarial real-VM regression tests. This is a
binding resource fix, not a redesign of the qualified D2A payout system.
The first oversized-record fixture incorrectly assumed that inserting string
keys into an empty preallocated array retains its capacity. Exact pinned rehash
can shrink that empty array; the test was corrected to fill before adding fields,
then clear/delete without forcing that rehash. Both initial case-4 failures
remain `public-vm-capacity.log`; corrected `public-vm-final.log` PASS is distinct.

An initial broader-regression invocation swapped WrongAbi and LegacyProbe
arguments on Windows; Linux initially placed persistence fixtures on unsupported
container overlayfs. Those runs are not PASS. The canonical fixture order and
existing qualified host-mounted persistence test root were used for corrected
Windows/Linux full regression PASS; no production persistence behavior changed.
The Windows .NET 9 SDK cannot build the existing .NET 10 tooling target; generation
uses the pre-existing Linux SDK 10 image instead, with no toolchain installation.

Production packages exclude qualification selectors/fault fixtures and stored
test state. Established task workers/caches are reused with bounded parallelism;
isolated server runners restore installed hashes and gracefully stop owned hosts.

## Next phase boundary

D3 introduces no ReplaceContents, original-item collection, conditional breaker
rules, DropResolved, C2/B2, extra prefab or release/tag. Any extension requires
separate scoped architecture/qualification; no later implementation begins here.

## Final-source hosted closure

Implementation and final tested source:
`0dd4dc7cb8b33a153d4016ac30a82e238b6ad21a`.

- [Core validation 38127303161](https://github.com/gmoddev/CarbonLuau/actions/runs/38127303161):
  PASS Windows x64, Linux x64 and sanitizers. All native tests, bounded storage
  worker/lost-ack, private loot parsing/publication, public Loot VM, Entity lifetime/
  discovery/position, C1, public discovery, persistence teardown, runtime/loader,
  combined persistence lifecycle/scale and preview-versus-production GUI checks
  pass. Production packaging, clean bundle example compilation/install and private
  worker/import audit pass on both platforms. ASan/UBSan/leak/lifecycle/fault PASS.
- [Tooling Foundations A/B 38127303176](https://github.com/gmoddev/CarbonLuau/actions/runs/38127303176):
  PASS Windows, Linux and macOS tooling. Exact generated metadata/definitions,
  drift/type checks, native analysis, VS Code package contracts and the explicit
  unsupported Loot preview test pass. macOS remains tooling-only, not a server
  runtime claim. No simulated loot backend or public payout event was added.
- [Tooling baseline contracts 38127303193](https://github.com/gmoddev/CarbonLuau/actions/runs/38127303193):
  PASS API/schema/preview baseline contracts.

All applicable D3 gates are closed within this one-prefab exact-host envelope.
The subsequent closure commit changes only this evidence record; under
Compatibility's documentation/evidence-only policy it reuses the source runs
above rather than falsely attributing native/live tests to a documentation edit.
Final main publication follows these successful gates. Source/fixture negative
receipts remain preserved and no failed run is relabeled PASS.

Tracked main is published by fast-forward only, with GitHub no-reply commit
metadata. The pre-existing untracked `.codexlock` and `derived-369547800317500/`
are preserved and excluded from commits/packages. Dedicated test servers exited,
leases cleared and installed hashes restored; no task container/listener/process
remains. Isolated source/build/evidence caches are retained under the established
Windows D: sandbox and BigKVM codex task roots; no unrelated workload was changed.
Package/API/ABI/provider/schema/Luau identities are unchanged and no release/tag
or marketplace publication is created. Next work needs a separate scoped task;
this PASS is not authorization to begin C2/B2 or another loot capability.
