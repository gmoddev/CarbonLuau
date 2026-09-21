# Player-1F-B implementation qualification

Date: 2026-09-21. Starting commit: `03b6d68346e01747e3b9250f1f4c4668ae3d8383`,
branch `main`. Scope: GiveItem InventoryOnly only; no Player-1F-C closure.

**Verdict: PASS for the recorded supported-host/server-authoritative scope.**
Windows live Carbon, authenticated-client receipt/UI and Shockbyte remain
unqualified; the final-source CI result is reported separately after push.

## Source and identity

The implementation and evidence are committed together after the local applicable
gates. Git history identifies that commit; no self-referential SHA is embedded.
Final-source CI is reported separately after publication and is not inferred from
local tests. The [implementation record](PlayerInteractionFoundation1FB.md) routes
to canonical I12/D13/D18 and public docs.

Unchanged identities: package `0.4.0`, API `0.4.0-experimental`, native ABI `1.4`,
provider protocol `CarbonLuau.Addons` / `1.2`, schema `1`, Luau
`c6b830185af962c82003f86784e2fe036357c830`. A private host operation and bootstrap
value are added, but no C ABI signature/layout/export changes.

## Target and upstream evidence

Rust Dedicated Server app `258550`, build `25353106`; Carbon `2.0.259`,
protocol `2026.09.03.0`, revision `21063e8490adf412101bcc7d1cfe9d6280f61e80`.
Downloaded exact depots: Linux `258552/3352454092778561960`, Windows
`258551/7816298056519226227`, shared `258554/4408100835840826754`.
Vanilla Assembly-CSharp SHA-256:

- Linux: `22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.
- Windows: `87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a`.

Live Carbon.Hooks.Oxide.dll SHA-256:
`028d8ee801937d97447a71a459684281cd44653b59416fd6968b63fe097d33c0`.
Selected identical Windows/Linux IL, public callback fields, constructor/removal
behavior, CanStack, CanMoveTo, MoveToContainer and conflict paths were inspected
using ILSpy/Mono.Cecil against these exact assemblies. The existing M2 structural
checker and the 1F-B callback/drop exposure checker supplement manual control-flow
review. Decompiled proprietary source is not committed.

Exact mutation APIs: `ItemManager.Create(ItemDefinition, Amount, 0)`, then
`Item.MoveToContainer(Target, Chunk.Slot, AllowStack, false, Player, false)`;
qualified temporary cleanup uses `Item.Remove(0)`. Lookup remains the existing
canonical exact-short-name wrapper around `ItemManager.FindItemDefinition`.
No callback-capable acceptance call occurs in PREPARE. Guards exclude baseline
`RemoveConflictingSlots`, `CanEquipItem` conflicting belt relocation, and
`WearItemCheck` conflicting clothing relocation paths. Real returned items are
checked again after creation; actual CanStack is used before merging.

## Results and reproduction

Worker: user-authorized BigKVM, Ubuntu 24, task root
`/srv/codex/CarbonLuauPlayer1FB20260921`, image `carbonluau-gui3e:latest`.
Disposable build/test containers use 1–2 CPUs, 2–3 GiB; live server 2 CPUs/6 GiB.
No ports are published, no real client is connected, and unrelated workloads are
untouched. Local Windows fallback uses MSVC 19.40, x64, one compile job.

| Gate | Evidence |
|---|---|
| Initial supported-host adapter requalification | `CarbonLuau.GiveItemG1Evidence.cs`: default/no callback/accept/inspection/reject, exact-slot main/belt/wear, merge, chunks, cleanup, acceptance and post-insertion exceptions. Ran before production implementation. |
| Model / planning | `PlayerInteractionFoundation1FBTests`: empty/full/insufficient, partial stacks, merge+empty, exact 128 chunks, one-over rejection, no Create on false, unknown input, identity and resource state, one fresh VERIFY. |
| Failure injection | Creation exception/null, transfer false/throw, accepted-then-throw/false, cleanup throw, VERIFY error/stale, world/inconsistent state, UID change. Every ambiguous COMMIT errors; accepted state is not rolled back. |
| Stress | 1,000 each successful merge, complete PREPARE rejection, callback rejection cleanup and transfer-failure cleanup: 4,000 model operations; gate and tracked references return to zero. |
| Native public API | Default/explicit typed behavior, invalid types/numeric forms, unknown/oversized name, provisional rejection, deferred grant, failed candidate, reconnect, root replacement, script error after grant, timeout/recovery without replay and teardown. |
| Command authorization | Denied grant command never enters Luau or creates an Item; authorized command grants. No method-specific permission requirement is added. |
| Addons | Shared-module provisional laundering rejection, foreign-domain enum, deferred grant, failed/successful replacement, root/addon Give/Take gate, provider unload and registration after unload. |
| Linux runtime/regressions | Full net48/Mono runtime suite passed: TakeItem, Player observations/Teleport, GUI, package/parser, addon/provider, scheduler/loader, Foundations A–G affected suites. Release CTest 5/5. |
| Windows available runtime | Full net48/native runtime suite and CTest 5/5 passed locally. Windows live Carbon not run; exact Windows assembly evidence retained, not equivalent to Windows live qualification. |
| Sanitizers / allocation faults | Linux instrumented runtime and pinned Luau: ASan/UBSan/leak detection, CTest 5/5 including RuntimeAllocationFaults and compiler containment. |
| Production live host | `CarbonLuau.Player1FBFixtures.cs` through actual `.cszip` plus native bridge: main/belt/wear, compatible merge/multi-chunk, full PREPARE rejection, normal callback rejection/exception cleanup, physical VERIFY, 300 repeated grant/Take/rejection operations, recursive Give/Take, API/reconnect, provisional and failed candidate. Two plugin cycles; `/proc` confirms native unmapping after each unload. |
| Structural/API/docs/packaging | `Test-GiveItemSafety.ps1` wired into architecture checks; exact invocation, no generic/auto/swap/ignore/drop, bounded plan, combined VERIFY, shared gate and typed behavior. Existing package/API/release checks apply. |

The live harness is [Test-Player1FBLinux.py](../tools/Test-Player1FBLinux.py), using
`tools/package.ps1 -IncludePlayer1FBFixtures` only for the isolated fixture
package. Production packaging omits both live fixture files. Build native with
the normal CMake Release/ASan configurations; run `tests/runtime/RuntimeTests.csproj`
against the resulting library/compiler and the usual wrong-ABI/legacy fixtures.
No new external test service or secret is required. RCon credentials are ephemeral
in-memory fixture values and not committed or printed.

Preserve valid historical M1 design/model guidance, M2 G2–G5 and unaffected phase
evidence. There is no standalone Inventory-M1 test executable in this baseline;
the new deterministic GiveItem model and existing TakeItem/scanner tests exercise
the relevant current contracts. New PASS markers do not claim to rerun historical
client-observed gates.

## I12 boundary demonstrations — separate from supported-host success

The live G1 fixture changed maxStackSize during acceptance and reproduced the
source-derived internal split/drop exposure despite an outer `true`; changed
Item.amount produced insufficient physical delivery; changed target occupancy
produced transfer failure. The production fixture exercises limit, amount and
slot interference and requires indeterminate errors, not successful grants.
These are explicitly injected trusted-plugin mutations, not inferred blame for
unexplained failures. Ordinary acceptance/rejection remains supported-host scope.

The first production harness attempt lacked the required empty modules folder;
the second raced plugin-copy reload against its initial readiness marker. Neither
is a qualifying run. The corrected harness creates the folder and stages the
package before startup; subsequent complete runs include native unload checks.
Logs/artifacts remain in the task worker's `evidence/` and `build/` directories.

Final live source/package run: `evidence/live-production-20260921-122558/server.log`,
container `codex-carbonluau-1fb-host-reviewed`, exit 0, two complete fixture cycles
plus two native-unmap checks. Initial adapter run:
`evidence/live-g1-20260921-115456/server.log`. Model/native final output:
`codex-carbonluau-1fb-review-final`; Windows output:
`dist/player1fb/windows-runtime-final.log`. That Linux container's tests passed
but its last release-packaging check initially lacked Git metadata in the archive
workspace; fetching the exact starting Git baseline (without replacing source)
and rerunning `Test-Release.ps1` passed. This was a harness/provenance prerequisite,
not a runtime result. Final review also added the nonpositive-stack regression
and capped merge chunks at the observed stack limit; managed/live tests were
rerun afterward. Native source did not change after its sanitizer/native gates.

Tested artifact SHA-256 values (fixture package is **not** a release artifact):

| Artifact | SHA-256 |
|---|---|
| Linux native | `b01494c3af2affbfffb1fa2f2552f45c1d83f25b7b79841d2c2319881eff29bd` |
| Linux compiler | `5b82c00c70d6d6ea0b38557d6f462067c5fd25d0b02a7062c1f3aa3884d39132` |
| Windows native | `08432a5cad3ef08c60f2a7b632a574fd4acfc5fd6f6c284d61856ffd5b7dd51e` |
| Windows compiler | `5f4977f9805980b0aa00f2341221e4c620a56b69a1215f2b636acef8ff133f8b` |
| Live fixture cszip | `fd8e344f1cba0625963da7e44f22883747db01967ff8cffff71a97a97126e1c5` |

Exact-build static reruns: Inventory-M2 92 checks; callback/drop exposure 51
checks. Windows/Linux loader suites each passed 100 native load/unload cycles.
Windows import checks passed with no host-selected dynamic MSVC CRT dependency.
Package/API/architecture and release identity/determinism checks passed; production
package contains 40 C# sources and no live fixtures or native binaries. All
task-owned servers/containers were stopped at completion; artifact caches remain.

## Limits and verdict boundary

No exhaustive item-definition/mod combination or arbitrary trusted-C# behavior
is qualified. Default stack compatibility is deliberately conservative; a safe
complete plan may be unavailable even with apparent free space. Ordinary creation
or transfer failure is controlled indeterminate; callback-side allocations that
never return a handle cannot be claimed cleaned by CarbonLuau. Cleanup failure
is counted and reported as uncertainty, not silently called successful cleanup.
No retained host references, retry queue or mutation history outlives the call.
There is no hard deadline preemption of Rust/C# callbacks.

No authenticated client receipt/UI, Windows live Carbon, Shockbyte qualification,
DropRemainder, DropIfFull, raw item/container surface, rollback, arbitrary slot
targeting, nested/cross-player transfer or Player-1F-C closure is claimed.
