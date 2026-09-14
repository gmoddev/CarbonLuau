# Phase 3 validation — 2026-09-14

**Verdict: PARTIAL pending publication and final-source CI.** Worker Windows/Linux
qualification is complete. Controlled real-host integration is not authenticated
client or provider qualification. No Phase 4 capability is included.

## Source and release identity

- Starting qualified main: `10c31c21a436dea7cdc506fe842b9a765d697019`.
- Implementation commit: pending publication of this tested change.
- Final evidence commit: pending CI follow-up; resolve through this file's Git history.
- Package **0.3.0**; scripting API **CarbonLuau 0.3.0-experimental**, Experimental.
- Native ABI: additive **1.2** (`0x00010002`); prior layouts/probe unchanged.
- Luau: `c6b830185af962c82003f86784e2fe036357c830`; vendor unchanged.
- User approved D10 on 2026-09-14 before implementation resumed. D11/D12 own
  the selected lifetime and minimum public API compatibility contracts.

Local and worker runtime/example sources match aggregate SHA-256:
`6f3df1e8370c35964dc88551579287abb8d1ab73e954763dae3d2f793b80d53d`.

Algorithm: normalize CRLF to LF, hash each UTF-8 file, then hash rows
`relative-path lowercase-sha256` joined by LF without a trailing newline. Ordered
files: native/CMakeLists.txt, native/include/carbonluau_native.h,
native/src/probe.cpp, native/src/Runtime.cpp, native/src/Scripts.inl,
native/src/Facade.inl, native/src/Bootstrap.h.in,
native/third_party/LUAU_REVISION.txt, scripts/bootstrap.luau,
src/CarbonLuau/CarbonLuau.Main.cs, src/CarbonLuau/CarbonLuau.Native.cs,
src/CarbonLuau/CarbonLuau.Runtime.cs, src/CarbonLuau/CarbonLuau.Scripts.cs,
src/CarbonLuau/CarbonLuau.Facade.cs, src/CarbonLuau/CarbonLuau.Carbon.cs,
examples/scripts/init.luau, examples/scripts/modules/message.luau,
examples/player-events/init.luau, examples/hello-command/init.luau.
Tests and docs are additionally identified by the implementation commit.

| Production artifact | SHA-256 |
|---|---|
| CarbonLuau.cszip | `98e3236b5be9a0d67d65e105d3a0ea5019222c385161236bff55b289bb9b71dd` |
| carbonluau_native.dll | `53998557a464b844d2ac9a473c88a5298ecf85bb5c56e683c6138dfb253aa063` |
| libcarbonluau_native.so | `de15abe4129db3da05179995c29a1e80e0600685ddcc406d9e6b4874adc63aaf` |

Package check passed: six C# sources, no native binaries or live-fixture source
inside the zip. Local deliverables are in ignored `dist/phase3`, including native
platform directories and examples. Later packaging timestamps can change the zip
hash without changing source. No credentials or server logs are committed.

## Environments

All sustained builds/tests/servers ran on `dockerbox` (HostPC), not the controlling
PC. At most four native jobs and two managed build jobs were used; Linux container
limits were four CPUs/10 GiB. Incremental/vendor caches were retained. Servers ran
sequentially/headlessly with loopback RCON, world 1000, seed 13579, zero real clients.

| Component | Windows x64 | Linux x64 |
|---|---|---|
| OS | Windows 11 Pro 10.0.26200 | Ubuntu 24.04.4 LTS container |
| Compiler | MSVC 19.44.35228 / VS2022 | GCC 13.3.0 |
| Managed tests | .NET SDK 9.0.317, net48 | Same assembly on Mono 6.8.0.105 |
| Carbon | 2.0.259.0 | 2.0.259.0 |
| Rust | 2633, Steam build 25230300 | 2633, retained installed build 25230300 |
| Live limits | 64 MiB VM, 3 ms callback, 5 ms frame | Same |

Windows private static CRT/import guard passed. The adapter compiled and ran
inside actual servers. Upstream SDK inspection and adaptation are in [Phase3.md](Phase3.md).

## Unit, interop and regression results

| Validation | Windows | Linux |
|---|---|---|
| Native CTest | PASS 4/4, 1.29 s | PASS 4/4, 1.50 s |
| Native load/unload | PASS 100 cycles | PASS 100 cycles |
| Original managed loader/path/failure matrix | PASS 100 cycles | PASS 100 cycles |
| Phase 1 native lifecycle/containment | PASS existing 1000 lifecycles/failure tests | PASS same |
| Phase 1 managed transactions | PASS 100 replacements, 200 failed candidates | PASS same |
| Phase 2 modules, confinement, scheduler, D9 | PASS all regressions | PASS all regressions |
| Phase 2 managed queue/reload stress | PASS 100 × 1000 queued/cancelled callbacks | PASS same |
| Services/version, unknown service/types, forbidden globals | PASS | PASS |
| Player strings/snapshots, message/query bounds, forged receiver | PASS | PASS |
| Disconnect/reconnect and permanently latched invalidity | PASS | PASS |
| Signals: ordering, disconnect/idempotence, errors, removal snapshot | PASS | PASS |
| D10 provisional messages/failed-candidate deferred work | PASS | PASS |
| A → failed B → committed C; publication failure preserves A | PASS | PASS |
| Permissions/revocation, queued disconnect, late selected command | PASS before Lua | PASS before Lua |
| Context/argument immutability, command errors and input bounds | PASS | PASS |
| Ownership/overload, off-thread intake, native callback reentry | PASS | PASS |
| Nested stop blocks another callback; outer teardown | PASS | PASS |
| Signal timeout → one reconstruction → unavailable | PASS at 3 ms | PASS at 3 ms |
| Both shipped examples | PASS real compiler/VM load | PASS real compiler/VM load |
| API/version/services/types/relative-link check | PASS | Ordinary CI pending |

Final managed build: zero warnings/errors. One earlier manual loader invocation
used runtime wrong-ABI fixtures and failed its expected wrong-probe assertion;
both platforms then passed with the correct WrongProbe/MissingSymbol inputs.
CI already uses those correct inputs.

Phase 3 stress per managed platform: **100 command-generation replacements, 2000
same-user reconnect cycles with old/new resolutions, 2000 signal connect/disconnect
cycles and 2000 compiled-Lua lookup/snapshot pairs**. Intake saturation admits 256
of 1000 deliveries and rejects 744; replacement cancels them. Final native live-VM
count is zero. Phase 2 samples remain 252528 VM bytes across 100 replacements.

## Native allocation and sanitizers

Linux ASan + UBSan with leak detection passed **4/4 in 13.54 s**, without
suppressions or Phase-3-owned findings. Native source did not change afterward;
later changes affected managed lifetime validation, tests and documentation only.

Fault tests cover 256 VM-init, 64 source-load, 768 Phase 2 and **1024 new facade
allocation positions**. Facade coverage includes bootstrap installation,
proxy/signal/command creation, event admission and callbacks. Every tested teardown
returns tracked allocator bytes to zero. Explicit facade timeout retires the VM,
rejects a late event and releases tracked bytes. The bridge and Luau dependencies
are instrumented; Carbon/Mono are not sanitizer-qualified.

## Actual Carbon/Rust integration

The test-only package creates a real BasePlayer entity and Network.Connection,
uses production player hooks/directory and actual Carbon command/permission APIs.
It does not create an authenticated transport session. The message assertion
proves the bounded BasePlayer.ChatMessage path returned, **not client receipt**.

Each successful cycle establishes:

1. Package/API/ABI readiness, future PlayerAdded identity/message routing.
2. Real command registration and host permission denial/grant.
3. A survives failed B; C commits; selected A cannot enter Lua.
4. Foreign-command collision rejection preserves the active command.
5. Same BasePlayer/account with a new connection cannot revive the old proxy.
6. D10, listener-error isolation and command timeout/D9 reconstruction.
7. 100 command replacements and production NextFrame event drain.
8. Owned chat removal and native unload, checked in process modules/maps.
9. Server responsiveness, fixture entity/permission cleanup, plugin unload/reload.

Windows final log:
`C:\Sandbox\Codex\Artifacts\CarbonLuau\phase3-server-win-final2.log`.
**Three cycles / 300 replacements passed**. Runner exit 0; server launcher exit 1
after requested quit. Library absence was checked before quit.

Linux logs in the same worker artifact directory:
`phase3-server-linux-20260914-191131.log` and
`phase3-server-linux-20260914-191256.log`.
**Two fresh processes, three cycles each / 600 replacements passed**, both runners
exit 0. The server child returned 137 after requested quit. Neither runner used
its termination fallback; process-map absence/host cleanup were checked before
shutdown. These codes are recorded, not described as exit-code-0 Rust shutdown.
Container memory events were all zero, including oom/oom_kill; no Rust process
remained at final inspection. This does not identify the server's reason for 137.

Earlier Linux log `phase3-server-linux-20260914-190947.log` failed its first
authorized-command output assertion and then showed generation reconstruction.
The original fixture hid callback failures, so the precise cause was not captured;
a deadline retirement is an inference, not a proven root cause. A diagnostic-only
fixture edit now prints callback status/errors. The unchanged production runtime
and unchanged 3 ms/5 ms limits passed both fresh repeat runs above. This observation
remains a limitation: not every callback is guaranteed to meet a 3 ms wall-clock
deadline under all cold-start, GC or host-scheduling conditions.

Preparation also caught a protected-metatable freeze-order error and an unset
UserIDString cache in the controlled entity fixture. Both were corrected before
final-source qualification; those earlier runs are not counted as PASS evidence.

## Basic performance observations

Worker observations, not hard gates. Managed tests use controlled host views and
exclude real Rust network latency. Setup/host calls are not hard-preemptible.

| Measurement | Windows | Linux |
|---|---|---|
| 100 replacements + 2000 reconnect checks + command dispatches | 46.05 ms | 37.96 ms |
| Event admission/drain, 1 listener | 0.027 ms | 0.034 ms |
| Event admission/drain, 10 listeners | 0.103 ms | 0.116 ms |
| Event admission/drain, 100 listeners | 0.962 ms | 0.919 ms |
| 2000 Lua lookup/snapshot pairs including chunk compile/setup | 5.89 ms | 11.19 ms |

Command dispatch and registration transition are included in the combined stress
measurement, not claimed as isolated overhead. No production throughput/soak claim.

## Public surface and documentation

Implemented: game:GetService and ApiName/ApiVersion/ApiStatus; Players GetPlayers,
GetPlayerByUserId, PlayerAdded/PlayerRemoving; Player Name/UserId/IsConnected,
SendMessage/HasPermission; Signal:Connect, Connection:Disconnect;
Commands:Register and CommandContext Player/Name/Arguments.

D11 binds a monotonic per-plugin token to exact player/connection identities and
string account ID, with permanently latched invalidity. Signals deliver future
events in registration order through the bounded scheduler; no synthetic reload
joins. Candidate registrations stage locally; command publication uses one prepared
list assignment. Failure preserves active state. Permissions gate admission and
Lua entry; no configured permission means public to connected players.

Reference files: api/README.md, Globals.md, Compatibility.md, Services/Players.md,
Services/Commands.md, Types/Player.md, Types/CommandContext.md, Types/Signal.md and
Types/Connection.md. README links installation, scripts, reference and both examples.
Types/errors/permissions/lifetime/reload/limits are documented separately from
deferred APIs. [API index](api/README.md); [implemented contract](Phase3.md).

## CI, limitations and handoff

CI publication/result: pending. The Phase 0-through-3 workflow builds Windows and
Ubuntu, runs native/managed regressions and examples, package/API checks and Linux
sanitizers. It does not launch Rust servers. Final evidence follows green CI.

The API is experimental. Commands are initialization-only, chat/player-only and
generation-owned; individual command removal/console callers are unsupported.
Permission metadata is bounded separately and may outlive generations. Carbon SDK
list adaptation requires requalification on relevant host upgrades. VM caps exclude
compiler/managed/host memory; queues reject overload; deadlines are cooperative;
messages have no exactly-once replay guarantee. The earlier Linux failure is
explicitly retained above. No long-duration leak-free or production-soak claim.

**Shockbyte Phase 3 was not deployed or qualified.** Phase 0 provider native-load
evidence does not qualify this facade. Real-client session/receipt, other host
builds and broad production load remain unqualified. Inventory/entities/health,
admin mutation, UI, networking/filesystem, arbitrary hooks, reflection, Roblox
hierarchy and task.wait remain deferred. **Phase 4 was not started.**

Worker sources/builds/artifacts remain under `C:\Sandbox\Codex`, with reusable
caches retained. Both task servers exited; `codex-carbonluau-linux` was stopped.
Unrelated `drycreek-bot` and `directus-db` remained running (seven days uptime).
No persistent task server is left running and no shared Docker cleanup was used.
