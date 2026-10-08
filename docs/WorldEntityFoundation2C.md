# World/Entity Foundation 2C: final closure and v0.6.0 readiness

**WORLD/ENTITY FOUNDATION 2 FINAL PASS — v0.6.0 RELEASE READY**

Qualification date: 2026-10-08. Final qualification source:
`4444d25f987a3b86e59148a41485d7c31dc053c0`. All four required source workflows
completed successfully. The evidence-only commit carrying the completed ledger
must also finish its automatic workflows before task handoff; their exact HEAD
and results are checked and reported separately, without treating running work
as PASS.
Starting evidence HEAD: `465dcc167ae2aab899006e9734981764cd008f93`.
Qualified production implementation: `79f0ddb38b8414007761a74bd7472525c9824516`.
The first closure fixture commit is `55a7a34c841e214f38ca08ee8c43c30f158162b8`.
This task changes qualification, bundle contents and documentation, without
changing production runtime source or any public contract.

## Authority and frozen surface

[D20](Invariants.md#d20--worldentity-foundation-1) and
[I12](Invariants.md#i12--trusted-in-process-host-interference) remain canonical.
[Entity-1C](WorldEntityFoundation1C.md), [private 2A](WorldEntityFoundation2A.md)
and [public 2B](WorldEntityFoundation2B.md) retain their original evidence and
limits. This record owns the combined final closure and release handoff.

```luau
Workspace:GetEntitiesInRadiusAsync(Position: Vector3, Radius: number,
    Callback: ({Entity}?, string?) -> (),
    Options: { Prefab: string?, Limit: number? }?) -> ()
```

The callback is third; optional Options is last. Submission does not yield and
returns no value. Radius is finite and nonnegative; zero uses exact encounter
coincidence. Prefab is an exact full nonempty UTF-8 string, at most 512 bytes,
without NUL. Limit is an integer 1..256, default 256. The next matching entity
fails the whole query. There is no truncation, nearest ordering or public
variadic/cursor/cancellation/token/catalog interface. Existing keyed lookup,
Entity equality and live property semantics remain unchanged.

| Resource | Unchanged hard bound |
|---|---:|
| Catalog / total raw slots per request | 262,144 |
| Pending requests, including completed-undelivered | 8 global / 2 per domain |
| Shared scan/delivery work units / raw slots per frame | 1,024 / 1,024 |
| Results | 256 maximum |
| Deliveries per frame | At most 2, within shared work |
| Absolute acceptance-to-admission deadline | 120 seconds |

Two 256-result deliveries cost 1,026 units and cannot both fit one turn. The
deadline never resets at a scan slice, readiness notification or queue delay.
Service resumes by checking expiry before further work/success; there is no
callback wall-time guarantee while the host is stalled.

## Lifecycle, publication and callback closure

`DiscoveryPublicTests` now combines the real pinned compiler/VM with exact
provider registrations and controlled completion transport. Failed addon
replacement preserves old scanning and ready captures. Successful replacement
retires them without entering old callbacks. Dependency replacement reconstructs
required consumers; optional consumers retain their old domain/binding, reject
new imports and do not hot-rebind on explicit provider restoration. Their own
already-admitted queries remain eligible. Provider unload rejects old tokens and
releases scanning/ready captures. Eight repeated replacements return to a clean
root-only baseline.

The expanded live fixture exercises the same graph/publication behavior with
the actual catalog, position adapter, compiler, VM and Carbon facade on both
platforms. It retains ordinary pure module values after dependency retirement,
while captured foreign host-backed Workspace operations fail closed. Cold and
provisional public-module calls admit no query; valid later calls use the owning
consumer's own Workspace. Shared values do not transfer callback authority.

Root failure preserves pending delivery; successful root replacement discards
both scanning and ready work. Fatal VM recovery creates fresh authority without
replay. Retirement during scanning/readiness/admission is covered by the model,
native and public suites. Native callback error/yield/deadline and allocation
fault fixtures release captures; ordinary errors remain catchable, while the
private deadline is VM-fatal. Delivery detaches before callback, validates every
original weak Entity lifetime and current publication/host/session/VM/domain,
then enters a fresh bounded operation. Results are never reacquired by ID.

First-install hotload and full plugin unload/reload still lose observer history
and require restart. This closure preserves the unchanged exact-host
[Entity-1A startup/hotload/reload receipts](WorldEntityFoundation1A-Validation.md#live-qualification-and-negative-evidence)
and [Entity-1C evidence](WorldEntityFoundation1C.md), rather than treating a new
catalog or current registry as recovered history. The new 2C live runs start
fresh server processes, successfully reconcile save-loaded completed epochs,
and qualify public discovery after that startup. Catalog loss is sticky and
produces controlled failure, including an empty result already queued for entry.
Unexpected receiver destruction stops scripting and clears runtime/facade roots.
Shutdown owns no Rust entity destruction; only the fixture's entities are removed.

## Scale, fairness and memory

Both platforms pass 17,275 lifetime-model checks, 3,925 discovery/conversion
checks and 1,667 position-composition checks. The extended maximum-catalog
benchmark passes 299,591 checks per platform. Each lane accepts eight requests
across four domains over 262,144 slots and inspects 2,097,152 raw slots.
Shared work/raw never exceeds 1,024, with zero per-query fairness spread in the
populated lanes. No per-addon independent frame budget is added.

The new maximum-catalog churn lane leaves 128 holes, excludes 129 reused/new
births after acceptance, and delivers eight complete original-cohort results.
A second full-capacity lane retires an already-encountered lifetime and produces
eight whole-query stale failures, zero partial results and zero pending work.
Smaller adversarial models cover ID/epoch/slot reuse, deadlines, cancellation,
weak cleanup, admission/delivery reentry, invalid evidence and catalog loss.

| Managed observed-256 lane | Windows .NET 9.0.19 | Linux .NET 9.0.20 |
|---|---:|---:|
| Turns | 2,056 | 2,056 |
| Median / p95 turn, ms | 0.238 / 0.260 | 0.128 / 0.203 |
| Maximum measured turn, ms | 0.818 | 2.422 |
| Total turn allocations, bytes | 480 | 480 |
| Collections during measured turns | 0 / 0 / 0 | 0 / 0 / 0 |

These are non-host managed measurements, not Mono/native-reader timings or
service guarantees. Existing 2A maximum-depth live observation measurements and
bounded storage premise remain separate. No portable exact managed-object byte
size is claimed. The public controlled-host 1,000-cycle capture stress returns
to Held=0 / native queued=0 / route reuse=0 on both platforms; measured native
heap remains 861,208 bytes before/after. Windows/Linux observations were 83/51
ms for that capture stress, without host position/catalog simulation.

## Final-source live evidence

Exact targets remain Rust build `25653776`, protocol `2634.289.1`, revision
`166494`; Windows Carbon `2.0.262.0` / `8a81d70`, Linux Carbon `2.0.261.0` /
`c74c4ca`, Harmony `2.4.2.0`, and the Unity/assembly/hook hashes in 2A.
The production native/compiler artifacts are identical to 2B.

| Artifact | Windows SHA-256 | Linux SHA-256 |
|---|---|---|
| Native | `83fdfc4bea656a8eb57256bffaf5dc4645a57083c6f9a1f0d588dd6e1f32cae2` | `511593f83202f431bae4f119afabb3cdd8ee992c13b6589fbc3b52287638ce5f` |
| Compiler | `887a0f66d2a86257b729833920fd0205c380e8b55836ac9a67a43ad31cf89390` | `884844c07b3c3f2b7c7e8aabb018399db3a80b0ae91879e877f81f40a1774d92` |
| Isolated live fixture package | `41ddf22df460fa8b82e3e2831437051c45cbbf1e3f51a22f7f97eb518184191b` | `8cd176b27beb44e0f6f0c87cc6c6e4b7de02390b903924aced05017975c2cba0` |
| Server receipt | `f14c1248e59e88817a3934cfbe67743b0d39a1714f150af906bbaefbbc194dac` | `95eeb608e194436563310c904e103efe7333128a507f9188dbf4f52f6714abb2` |

Windows: `D:\Sandbox\Codex\Entity1AStartup\evidence\discovery-public-20261008-031851-022\server.log`.
Linux: `/root/codex/world-movement-20261003/evidence/discovery-public-20261008-031858-1477942/server.log`.
Both **31 assertions PASS**, with 1,021 Windows / 1,004 Linux startup completions.
The runners bind only localhost ports 28335/28337. Fixture packages were produced
by different .NET ZIP implementations; integrity is recorded separately, not
claimed to be a common cross-tool compression hash.

Both repeat empty/one/many, exact Prefab, 256/257, later birth exclusion,
movement before encounter/after observation, death before entry, synthetic ID
reuse, nested later-turn query, root/addon/provider replacement, fatal recovery,
catalog loss, receiver teardown and zero requests/traversals/VMs/owned entities.
No authenticated-player receipt, natural pooling or real 120-second wait is
claimed. Runners accept the pinned quit path's process-kill exit only after
COMPLETE, cleanup, shutdown and save receipts. Prior installed package/native/
compiler/hooks/config hashes are restored; no server/listener remains.

## Regressions and sanitizer scope

Windows and Linux full managed/native-VM runtime regressions PASS, including
modules/publication, addons/providers, scheduler/task/event recovery, Player,
inventory, GUI, persistence/Query worker, examples and deterministic teardown.
Focused Entity-1C and Discovery-2B/2C public tests PASS on both platforms.
Cached affected native suites pass six Windows / seven Linux tests, including
Linux ELF unloadability. Full native/platform/loader/package gates are owned by
the final hosted runs below.

Linux ASan/UBSan with leak detection passes DiscoveryFacade and
RuntimeAllocationFaults. This instruments CarbonLuau/pinned Luau native runtime,
including callback capture/error/yield/deadline/allocation-fault retirement.
Managed model semantics and UnityPlayer/TRS reads are not sanitizer coverage;
Unity is not rebuilt or represented as instrumented.

Evidence directories: `C:\Sandbox\Codex\WorldEntity2C-20261008` on DockerPC and
`/root/codex/worldentity2c-20261008` on BigVPS. Linux task containers use the
existing builder image, two CPUs and 2..3 GiB limits; Docker Desktop was not
started. Full Linux persistence fixtures use the qualified ext4 mount through
the existing test-root override. Earlier cache selections hit absent test
executables, and an initial Linux runtime run rejected overlayfs as designed;
corrected invocations/builds pass. A Windows combined run hit the unchanged
Player deadline while live qualification was also running; the complete rerun
after server exit passes, without changing deadlines or production code.
The first maximum-churn fixture accidentally left a Spawn attempt Pending;
the model correctly rejected a nested retry. Corrected failed full-call
completion creates the intended holes and both final runs pass.

## Security and observation audit

Scoped source review traced native discovery input/captures, the bootstrap Entity
factory, managed facade/publication authority, public delivery, catalog
traversal/conversion, position borrowing, retirement and teardown. No unresolved
actionable production finding remains in these reviewed paths. This is a scoped
closure review, not a claim of exhaustive repository or upstream-engine security.

Malformed Radius/Vector3/Prefab/Options cannot invoke script metamethods, admit
unbounded state or escape a controlled error. Options are copied before intake.
Fixed eight/two captures include undelivered work; routes/VM/domain lifetimes do
not recycle into replacement authority. Shared callbacks cannot launder owner or
publication authority. Complete result conversion is bounded to 256 original
candidates; stale evidence fails the whole array. Deadline expiry and duplicate
readiness cannot reset/replay the operation. No host object, native pointer,
reflection, registry or exception detail is exposed to Luau.

Hard guarantees remain exact lifetime/non-retargeting, native storage safety,
bounded CarbonLuau work/state, serialized owner-thread authority and at-most-once
delivery. Positions are encounter observations; trusted Rust/Unity/Carbon/Oxide
writers may race scalar values under I12. No atomic world snapshot, universal
movement-freshness theorem, unsafe-storage permission or silent partial success
is inferred from I12.

## Metadata, release audit and handoff

The complete `v0.5.0..main` source history and all metadata declarations newly
assigned `0.6.0-experimental` were audited. The new public types are Workspace,
Entity and EntityDiscoveryOptions; the new members are keyed lookup, radius
discovery and Entity Id/Prefab/Position. Existing declarations retain SinceApi.
Runtime/bootstrap, API JSON, generated definitions, tooling drift/schema tests,
author references and examples agree. Preview remains unavailable for world
operations. The three Foundation 1 examples and discovery example execute in the
pinned compiler/VM against controlled hosts; live world evidence is separate.

Closure corrects the omitted discovery changelog entry and adds Discovery.md /
EntityDiscoveryOptions.md to deterministic release bundles and clean-install
checks. [Planned v0.6.0 notes](releases/0.6.0.md) describe the actual additive
surface, support envelope, restart rules, async failures and remaining limits.
[Release.md](Release.md) owns the publication procedure. Current dry-run bundles
retain the existing 0.5.0 filename/manifest and are not v0.5.0 replacement releases.
Both platform bundles pass repeated byte-for-byte packaging and clean native
installation, compile all 63 bundled Luau examples and execute the existing
GUI/Player installation regressions. Corrected discovery documentation/example
presence is now a mandatory bundle and extracted-install assertion. Local
precommit dry-run SHA-256: Windows
`d3c921b495c7d61d246527047f9540a90112b2558729b23487435975398d8db3`,
Linux `77b4539227c9f4343a3052b6e104a4645035745a5e30c451d49f102c50416bb5`.
Those provenance records correctly say uncommitted-working-tree; final hosted
bundles use the exact committed revision and are independently checked.

| Current identity | Value |
|---|---|
| Published package / tag | `0.5.0` / `v0.5.0` |
| Development scripting API | `0.6.0-experimental` |
| Native ABI / provider protocol / package schema | `1.5` / `1.2` / `1` |
| Pinned Luau | `c6b830185af962c82003f86784e2fe036357c830` |

After the final gates pass, the release-publication handoff is: separately
authorize v0.6.0, update the package/tag/build mapping and workflow artifact names,
retain API `0.6.0-experimental` and all other identities, rebuild/test the exact
release revision, verify provenance/checksums and publish its experimental
platform archives with the planned notes. A matching official editor extension
needs its own source-pin/build/qualification; the older extension is not a
matching 0.6 artifact. Its absence does not block the independently packaged
server release and no VSIX is included or published here.

Authenticated-client GUI/Teleport behavior, historical Windows qualification
deferrals, natural Entity pooling and other unqualified hosts remain explicitly
unqualified. TextBox, Entity Signals, Spawn/Destroy, Position writes, specialized
capabilities, general enumeration/query DSL, spatial/movement indexes and public
query cursors/cancellation are deferred. All rejected movement/catalog/getter/
position research remains historical evidence under Foundation 2 routing.
No release, tag, Marketplace artifact or later World/Entity feature is created.

## Final hosted qualification

The expected starting evidence HEAD `465dcc167ae2aab899006e9734981764cd008f93`
completed successfully: [validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37444726975),
[tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/37444727098) and
[documentation](https://github.com/gmoddev/CarbonLuau/actions/runs/37444727174).
The separately path-filtered tooling-baseline workflow had passed the unchanged
contract at production revision 79f0ddb; final closure dispatches it explicitly.

The first closure fixture commit `55a7a34c841e214f38ca08ee8c43c30f158162b8`
also completed [full validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37743063710)
and [tooling](https://github.com/gmoddev/CarbonLuau/actions/runs/37743063712).
Final qualification source `4444d25f987a3b86e59148a41485d7c31dc053c0` adds
maximum-catalog churn, mandatory hosted scale coverage, bundle documentation
correction, extracted-content assertions and release planning. Completed gates:

| Workflow | Completed result |
|---|---|
| [CarbonLuau validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37744910348) | PASS: Windows 23/23 native, Linux 24/24 native, full runtime/worker/loader regressions, maximum catalog, examples, deterministic bundles and clean install |
| Same workflow, sanitizer job | PASS: 24/24 ASan/UBSan/leak lifecycle/fault tests; no new suppression |
| [Tooling Foundations A/B](https://github.com/gmoddev/CarbonLuau/actions/runs/37744910364) | PASS on Windows/Linux/macOS; macOS tooling is not server support |
| [Tooling baseline contracts](https://github.com/gmoddev/CarbonLuau/actions/runs/37744910488) | PASS, explicitly dispatched for this exact HEAD |
| [Documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/37744910354) | PASS |

Hosted generated API JSON equals the canonical checked-in catalog. The actual
generated `.d.luau` contains `EntityDiscoveryOptions = {Prefab: string?, Limit:
number?}` and `GetEntitiesInRadiusAsync(self, Position, Radius, Callback,
Options?)` with the exact public types; there is no variadic public signature.
Hosted committed-source dry-run bundle hashes are Windows
`82ad2721620d9e42a11dbccf160bfbb8bfd744f1c4dfb91f5cbaf6f354a9a47d`
and Linux `4c79fc1b256e4cba2a7109bfcf6ca1471ddf04a196e74ed5b19998acad02d83e`.
They are development qualification artifacts, not a published v0.5 replacement.

Local receipt integrity, relative to the evidence directories above:

| Receipt | SHA-256 |
|---|---|
| Windows `windows-native-final.log` | `1a49cc429858f99cc5e06344c48e9d8c7cabd765e6369c6458e755a00e938128` |
| Windows `windows-runtime-final.log` | `4fb234e7d86994b41693bc5a755faaa9c8dbb3720d34c07becaea52b23964071` |
| Windows `windows-maximum-final.log` | `b08be36a2b718049f4152c8fc67731d280a43254cd7d9416a46606e4506eea67` |
| Windows `windows-package-final.log` | `f85cf7416b8562356f8e81926198e664f4063f2f42e7b002de3f502b54cc9da0` |
| Linux `linux-runtime-complete.log` | `b536c61e6fccf77214c828e7347c878c16fccfd653b9c61238c6530518cec6e6` |
| Linux `linux-maximum-final.log` | `74d567fff92a88242843a4d40a82a30fd7452da664a4c2742d7956617ddac236` |
| Linux `linux-package-final.log` | `3058a3aee9448babc4d657a180d30c99befa1496a301466d57e3b3ddb93e0a05` |

No Foundation 2 gate or separate server-release blocker remains. Publication
still requires the deliberate identity/provenance/release procedure above,
not another discovery design. Historical negative evidence and qualification
deferrals are preserved. All task-owned servers, compiler/storage workers and
containers exited; no fixture port remains open. Reusable evidence/build caches
are retained, with no credentials, binaries or databases committed. The closure
is pushed on `main` with GitHub no-reply author/committer metadata; tracked
worktree cleanliness/synchronization and the final evidence HEAD workflows are
confirmed at task handoff. No v0.6.0 release/tag or later feature was started.
