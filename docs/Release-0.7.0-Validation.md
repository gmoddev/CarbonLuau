# v0.7.0 experimental release qualification

Release scope: the complete current `0.7.0-experimental` scripting API, including
the breaking C4 PlayerInventory migration. This is not the older World-only
0.6 planning candidate. Starting source: `eba0caad2952b058c250063b395f1038a4ccca17`.

## Source and publication authority

Identity preparation: `baaaaeeb7da63c2fe9574f89afb168e079b99de9`.
Combined persistence/gameplay regression: `8fa46f0548d7b2aad9898b928c06099c56210cff`.
GUI lifecycle-soak correction: `5ab0669fa799a5e2898eee36add591a2cf85ba99`.
Restart-host pin: `1ab9874303cea0a43b4f2351e6116a0322fbdc5d`.
Deferred Loot-fixture correction and tested candidate:
`433ae0d6f3196d36f2cc089be7d5a5db46324313`.

No runtime behavior was redesigned. Production changes are only the managed
package identity and native CMake package version. API introduction versions,
runtime bounds, host pins and implementation semantics are unchanged.
The final packaging/documentation commit extends the checked archive contents
to every current public API reference and the canonical metadata/definitions.
Neither editor executables nor VSIX artifacts are included.

The **final release revision** is the annotated `v0.7.0` tag's peeled commit,
also recorded as `sourceRevision` in both provenance sidecars. This document
cannot contain its own Git commit hash. Before tag/publication, require clean
source, synchronized main, successful final-commit Windows/Linux/sanitizer,
tooling and documentation workflows, final deterministic bundles and clean
installs. Candidate CI below is evidence for its recorded revision, not an
automatic substitute for those final-commit gates. Publication and downloaded
asset hashes are recorded by the GitHub prerelease and task completion report.

| Identity | Value |
|---|---|
| Package/tag | `0.7.0` / annotated `v0.7.0`, GitHub prerelease only |
| Scripting API | `0.7.0-experimental` |
| Native ABI | `1.5` |
| Provider protocol / addon schema | `1.2` / `1` |
| Luau | `c6b830185af962c82003f86784e2fe036357c830` |
| SQLite | `3.53.4`, unchanged qualified source/compile profile |

## Exact live-host scope

Rust build `25653776`, revision `166494`, protocol `2634.289.1`.
Windows Carbon `2.0.262.0` / `8a81d70`; Linux Carbon `2.0.261.0` / `c74c4ca`;
Harmony `2.4.2.0`. Assembly-CSharp SHA256:

- Windows: `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`.
- Linux: `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`.

World/gameplay runs installed the already-qualified hook tuple temporarily,
disabled the isolated host updater and restored the previous hashes afterward.
Unsupported hook/build drift is not qualified by this release. Initial-startup
observer continuity remains mandatory; first hotload/full plugin unload requires
restart for World. No vendor assemblies enter the release bundles.

Windows build: MSVC `19.44.35228.0`, Visual Studio 2022, x64 Release, static CRT,
parallelism two. Linux build: GNU `13.3.0`, x64 Release, bounded two-CPU/4-GiB
build/test containers on BigKVM ext4. The pinned SQLite source cache was hash
verified by CMake. Final-source rebuild/package checks reuse only unchanged
compiled inputs; documentation is not native source. Native payload SHA256:

- Windows runtime: `f52e1a3be3fa81d3c788dc4cfb882940f7be3d5c198de4306bada7b153865609`.
- Windows compiler: `2462b65e7e3e0a110f315ffa2e426ed033c95f565c884bf6ea26d93d0e2c67c1`.
- Windows storage: `70fee873cb7bc9650b3c8e9441d7dfff207dc477eae47e25acbe7f6d30c6036d`.
- Linux runtime: `679099bf4e83b0386f9d0b5ceba6b24df7a8fab8928ec580b7d3ed7bd2907be2`.
- Linux compiler: `884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92`.
- Linux storage: `ccff9dbb308cabaee8d1ddd4fb9ca2b86f3f3b8fca646bf8709490058d88cc54`.

## Newly executed combined evidence

All rows below passed in this release task. Controlled BasePlayer connections
are actual server objects/physical inventories, not authenticated Rust clients.
Event stress captures remain synthetic where the fixture explicitly says so.

| Boundary | Windows/Linux result |
|---|---|
| Give/Take plus sampled ItemChanged | Real public calls, physical gain/loss, split/merge/transfers, immutable context, initial baseline/no history; preserved balanced/intermediate negative observations |
| Player death/spawn | Original connection, real initial/full respawn, terminal death, veto, wounded/revive, reconnect, NPC exclusion, deferred public delivery and cleanup |
| Entity death/removal plus Loot | Exact loaded 67 body pins/467-type topology, original RPC melee, nil unproved attribution, death/removal distinction, B1/B3 callbacks, strict stored/bonus/bag payout suppression, range/zero/budgets and partial failures |
| Discovery | 31 live public cases each: watermarks/churn, encounter movement, exact lifetime/ID-reuse rejection, 256-result boundary, domain/provider replacement, publication, fatal recovery, deadline and callback discard |
| Persistence integration | Production package, real worker, root/addon Get/Set/Remove, structured Query, reload, native unmap and stopped helpers |
| Actual Rust restart | Windows Root/A/B same-name store/key isolation: 8/4/2 Get/Set/Remove before restart, 3/0/0 after; Linux equivalent plus Query restart, no mutation replay |
| Persistence alongside saturated gameplay | Six real-worker Set/Get/Remove chains under public PlayerDied saturation, positive event progress, overload counted, no duplicate completion, zero pending/retained reservations |
| Shared fairness/lifecycle | Final candidate full managed/real-VM regression covers 100 actual addon domains with synthetic event producers, mixed A/B/C traffic, failed/root/addon/provider publication, callback faults/recovery and resource convergence |

Local native suites: Windows **23/23**, 164.20 seconds; Linux **24/24**,
124.11 seconds. This includes ABI/load/unload, compiler containment,
allocation faults, persistence crash/corruption/WAL/capacity/derived/Query and
discovery ingress. Windows import audits reject dynamic MSVC CRT dependencies.
Linux ASan/UBSan/leak coverage passed in the candidate hosted sanitizer lane.

Metadata regeneration produced no changes; catalog/definitions retain the exact
five PlayerInventory members and omit all four removed Player methods.
Tooling tests, negative drift and 17 preview goldens passed. Preview remains
offline and does not simulate persistence/gameplay. Complete-reference candidate
bundles passed repeated deterministic packaging, provenance/payload checks,
clean extraction and real-VM compilation of **69 examples**, GUI/Player execution,
C4 admission and teardown. Final committed bundles repeat these gates.

Candidate hosted receipts at `433ae0d6f3196d36f2cc089be7d5a5db46324313`:

- [Windows/Linux/native/managed/real-VM/sanitizers and bundles](https://github.com/gmoddev/CarbonLuau/actions/runs/38198881535): all three jobs PASS.
- [Tooling Windows/Linux/macOS](https://github.com/gmoddev/CarbonLuau/actions/runs/38198881511): PASS; macOS tooling is not macOS server support.
- [Baseline/document coverage](https://github.com/gmoddev/CarbonLuau/actions/runs/38197497154): PASS at the identity preparation source; final documentation checks are required separately.

## Live receipts and preservation

Windows evidence root: `D:\Sandbox\Codex\Entity1AStartup\evidence`.
Linux evidence root: `/root/codex/world-movement-20261003/evidence`.
Each named run contains `server.log`; runners record input/log hashes and
verify owned process exit plus restoration of package/native/compiler/hooks/config.

| Run | Windows | Linux |
|---|---|---|
| Sampled inventory | `inventory-c3-20261011-230418-734` | `inventory-c3-20261011-230746-1803861` |
| Loot/death final fixture | `loot-d2a-20261011-232544-223` | `loot-d2a-20261011-231341-1804643` |
| Player lifecycle | `gameplay-host-20261011-230830-453` | `gameplay-host-20261011-231511-1805127` |
| Discovery | `discovery-public-20261011-231110-469` | `discovery-public-20261011-231811-1805719` |

Windows production persistence receipts are under
`D:\Sandbox\Codex\Release070\Persistence`: Query/reload run
`persistence1b-20261011-231428-dcd58270`, and actual restart run
`persistence1b-20261011-231821-d8dd35bc`. The latter records two distinct process
starts, acknowledged save/shutdown, exit `-1`, zero helpers and final native unmap.
The current Windows quit path was re-inspected using shipped Mono.Cecil:
ConVar.Global.quit calls Shutdown at IL_003c and Process.Kill at IL_0061;
System.Process.Kill passes `-1` to TerminateProcess. Both assembly hashes are
now pinned by the existing restart runner; no generic exit-code waiver was added.

Linux production persistence receipt:
`/root/codex/release-070-20261011/persistence-evidence/linux-live-2d-20261012-031607`.
It records two acknowledged quit/save exits `-9`, zero helpers, restart reads
and Query, and 118,784 allocated bytes after stop (observation, not a hard bound).
The loopback-only isolated server copy and evidence are retained; it is stopped.

Preserved negatives/corrections:

- First tooling CI still asserted the old development package hold at 0.5.
  The authorized 0.7 release mapping corrected that test; introduction checks remain.
- First Windows GUI soak exceeded its 5ms reconstruction fixture deadline.
  The lifecycle soak now uses the existing 100ms clamp while still requiring all
  100 infinite-loop cancellations and old-authority rejection. Production limits
  and separate short-deadline tests are unchanged.
- Initial combined-test harness tried to share the native staging directory with
  storage and then exceeded the canonical submission rate. Isolated storage and
  normal rate refill corrected the harness, not production contracts.
- Linux Loot run `loot-d2a-20261011-230906-1804110` checked a deferred B3 callback
  too early. The fixture now waits at most 16 actual frames, pays once and retains
  the same callback assertions. The corrected fixture passed on both platforms.
- Superseded intermediate CI runs were cancelled after newer task-source checks
  started; they are not counted as PASS. Historical investigations remain intact.

## Release limits and artifact discipline

The [release notes](releases/0.7.0.md), [compatibility](api/Compatibility.md) and
[migration](api/Inventory-Migration.md) retain exact-host/startup continuity,
encounter-time discovery, sampled inventory, narrow original-connection killer
attribution, and nontransactional partial Loot outcomes. No universal ordering
across Entity Signals, replay, rollback or client-delivery promise is added.
Authenticated-client GUI/Teleport, Shockbyte full-runtime, macOS server, physical
power loss, TextBox and broader/deferred gameplay remain unqualified/unimplemented.

Publish only the qualified `CarbonLuau-v0.7.0-{win-x64,linux-x64}.zip`, matching
`.sha256` and `.provenance.json`. ZIPs must contain no fixture DB/journal, host
assembly, probe, credentials, runtime log, tooling executable or VSIX.
Final provenance must say `committed`, record the final tag commit and match
the tested payload hashes above. Verify public downloads against the tested ZIP
and sidecar hashes. Ordinary forward commits only; existing tags/assets, archive
branches and unrelated untracked files remain unchanged. No qualification server,
worker or build/test container remains after task cleanup.
