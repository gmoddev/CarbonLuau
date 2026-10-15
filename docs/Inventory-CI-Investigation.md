# Inventory CI failure investigation and status correction

Date: 2026-10-15. Starting main:
`c0152238a7a10941806e06e931ed70eba975daa9`.
Correction: `24599a534cffeeb9c7ce8dac6780a39d94ee0e5c`.
**The historical inventory release HOLD remains.** A reproducible bootstrap
compiler timeout, an execution timeout and a shared managed status-classification
defect are established.
They do not uniquely identify the cause of the original incomplete observation.
The [cross-repository assessment](CrossRepo-ReleaseReadiness.md) remains authoritative
for the separate editor, published identities and other release gates.

## Historical evidence

The original [Windows job 116333604423](https://github.com/gmoddev/CarbonLuau/actions/runs/38708449523/job/116333604423)
failed `old PlayerInventory acquisition still scan/watch free`. That assertion
combined reload success, zero scans and zero watches. It recorded none of the
three values or the reload error. The retained 65,131-byte log remains unchanged,
SHA256 `e6387c375980279a66ae641f292bc26cd66b97cb0f3d202179fd76855aa7f7d9`,
at `C:/Sandbox/Codex/D8Control20261015/CI-bc73ee9-Windows-failed.log`.
The original Linux job passed this step before later cancellation. Later passing
Windows/Linux jobs, including the cross-repository closure, do not explain the
negative. No missing result is reconstructed from its timestamps.

The original source is `bc73ee9077dc69f13ce8359f7edc5ed451143dc4`.
Comparison to starting c0152238 confirms identical Git blobs for NativeRuntime,
ScriptHost, RuntimeDomain, FacadeSession, Runtime.cpp, Compiler.cpp and Deadline.cpp.
The first acquisition source and counter paths are unchanged. The later
ModuleLoader difference installs the qualified D9 catalog globals; its require
compile/resume/deadline path is unchanged. Current scheduling probes therefore
exercise the same relevant mechanisms, while remaining current-source evidence
rather than a claim to have recreated the historical hosted environment.

## Traced operation and boundaries

| Phase | Code and ownership | Applicable deadline |
|---|---|---|
| Model tests and measurements | Separate managed Fixture/World/Player instances, disposed before RealVm | Existing bounded model work |
| Private native deployment | Unique invocation-owned temp directory; native library and compiler copied; fresh NativeRuntime | Outside script execution |
| VM creation | ScriptHost.Replace → RuntimeGeneration → NativeRuntime.Create; protected standard-library/catalog setup | Existing VM allocation cap; no entry admission yet |
| Domain setup | RuntimeDomain, immutable source/module snapshot, fresh FacadeSession | No entry admission yet |
| Facade bootstrap | cl_domain_facade → CompileSource(BootstrapSource), then protected InstallFacade; managed preparation operation 0 | Compiler request has its own fixed 1,000 ms wall deadline, including worker startup; no 100 ms entry timer yet |
| Entry source | cl_domain_load_source → isolated compiler → sandbox/load | Separate fixed compiler request bound |
| Entry execution | cl_thread_resume installs admission and monotonic Deadline immediately before lua_resume | Fixture's unchanged 100 ms execution ceiling |
| First state module | require('state') → fresh module publication, compiler request, sandbox/load/resume, staged result | Same outer entry execution deadline; no reset for synchronous require |
| Acquisition | Players:GetPlayers, exact D11 lookup, operation 40 and publication witness, immutable cached PlayerInventory | Same entry deadline; no inventory scan or subscription |
| Publication | Native candidate commit, facade publication, old owner retirement | Existing D7/D10 ordering |

The whole Reload stopwatch includes all these phases; a value above 100 ms does
not identify an execution timeout. VM interrupts are cooperative and do not
preempt compilation or managed host calls. The compiler remains separately
bounded at 64 KiB source, 1 MiB response, one second and 256 MiB process memory.
The global compiler service serializes requests; compilation releases the VM
registry lock while waiting, without another thread entering the same VM.

Player.Scans is an instance field incremented only by the fixture's inventory
source. The first entry calls neither CountItem/HasItem nor mutation. Its source
does not connect ItemChanged or stage a demand. WatchCount belongs to the new
World.InventorySamples; it is not a static counter. No Samples.Tick, Host.Drain,
asynchronous sampler or scheduler callback runs during this acquisition. The
earlier model measurements use other instances. These facts rule out ordinary
counter carryover and scheduled sampling on the inspected path; they do not
recover unrecorded historical values or prove the absence of every runtime fault.

## Controlled observations and alternatives

Twenty unmodified fresh Windows processes passed before the correction. A
process-local scheduling probe then pinned only itself and its child fixture to
one logical processor, at normal priority, with zero, one, two or four additional
CPU-bound threads sharing that processor. It modified no host policy, compiler
deadline, source, assertion or unrelated process. Its external 30-second bound
and deterministic thread/process teardown limit the experiment.

Five runs each with zero, one and two competing threads passed. Seven four-thread
runs produced two passes and five failures. Four failed reloads recorded
`INTERNAL_ERROR / scans=0 / watches=0`, and error
`domain facade failed: TIMEOUT`. One retained whole-reload observation is
1,533.441 ms. This failure occurred during bootstrap compilation before inventory
entry execution. The compiler starts its one-second request deadline before
worker startup and its bounded transport wait. Delayed scheduling can exhaust
that bound; a bootstrap timeout rejects the candidate. This is an observed
environmental failure of initialization, not evidence of an inventory scan.

The other failed reload recorded TIMEOUT, scans=0, watches=0 and
`monotonic execution deadline exceeded; VM retired`, whole Reload 1,192.478 ms.
It also failed the exact original assertion. The probe changed no script source
or deadline. Thus both bootstrap and execution deadline exhaustion can reach this
assertion under controlled contention. One bootstrap-failure invocation then
encountered UnauthorizedAccessException deleting its copied compiler executable;
its finally cleanup masked the primary assertion. Its recorded Reload observation
and full cleanup exception are retained as separate negative evidence. That
cleanup observation does not establish long-term process leakage or identify the
historical failure, whose visible exception was the acquisition assertion.
The one leftover compiler file was identified by its exact fixture creation
window and qualified binary hash, with no owned process remaining, and moved
into the task's Quarantine directory. Its receipt is retained; this administrative
cleanup does not change or retry the fixture's failed operation.

This intentionally constrained scheduler is not the historical hosted runner.
Its successes and failures prove a mechanism, not historical attribution. The
original job also permits alternative explanations, including entry/module
compile/load failure, execution timeout, native initialization failure, allocation
failure or a runtime assertion. The old log cannot discriminate them. No claim
is made about Defender, disk contention, a particular JIT pause or actual CPU
starvation on that old runner. No production scheduling or timeout change is
justified by this evidence.

## Demonstrated defect and correction

Before the correction, NativeRuntime.Require threw a plain
InvalidOperationException for every checked native failure. ScriptHost.Replace
caught it and assigned INTERNAL_ERROR, losing the native TIMEOUT category.
Addon activation and the shared RuntimeHost initialization used the same pattern.
The diagnostic text retained the native category, but the structured status did
not. This is a shared initialization diagnostic defect under I9.

Require now throws an internal InvalidOperationException subtype carrying the
checked RuntimeStatus. The three initialization catch paths preserve that status;
ordinary managed exceptions retain their previous classification and curated
message behavior. Native code, VM/bootstrap order, host hooks, worker transport,
deadlines, retries and allocation/resource limits are unchanged. In particular,
a compiler timeout does not fabricate an execution-retirement flag, retire a
healthy published domain or become an automatic replay. A fatal VM execution
timeout still retires the complete generation under D10.

The new `--inventory-init` fixture is a separate process from `--inventory-net`.
It never warms up that original fixture. Its real hanging compiler uses the
existing private CompilerFixtureHang executable inside a unique temp deployment.
That regression fails on the old managed source on both platforms:
native TIMEOUT becomes INTERNAL_ERROR with zero scans/watches and zero live VMs.
The corrected source returns TIMEOUT with the same error, bounds and cleanup.

Five cases distinguish bootstrap timeout, entry compile error, module compile
error (existing enclosing RUNTIME_ERROR), ordinary entry error and fatal entry
deadline. Every rejected first initialization has zero scans, watches, demands,
pending records and live VMs. Explicit operator recovery occurs only after the
known fault is removed or the source corrected. Successful acquisition then
remains scan/watch free; sampled baseline/change, failed and successful root
replacement and old-demand retirement are checked. Test-assembly-only one-shot
ABI refusals also verify root setup TIMEOUT, addon setup MEMORY_LIMIT and VM
creation MEMORY_LIMIT. They restore the real delegate immediately and export no
production test hook. Root/addon setup refusals preserve the original owner,
generation and observer demand. The original inventory success predicate remains
required and can still fail under genuine resource starvation.

## Qualification and retained attempts

Windows uses the existing dockerbox native MSVC binaries; Linux uses the existing
qualified D9 binaries on BigKVM in a task-owned container limited to two CPUs,
4 GiB and 512 PIDs. Native inputs are unchanged and identified in the evidence
ledger. Managed assemblies are rebuilt from the modified source. Each platform
passes three fresh initialization fault suites and twenty fresh original
inventory suites after the correction. The original suite includes 10,000 churn
cycles, saturation, allocation faults, real-VM replacement/recovery and teardown.
Every fresh acquisition reports OK, scans=0, watches=0. These passes establish
current behavior, not historical causality.

All raw logs live beneath `C:/Sandbox/Codex/InventoryCi20261015`; the
[evidence ledger](qualification/Inventory-CI-Evidence.json) identifies hashes,
sources, repeated results and hosted qualification. Negative attempts are retained:
the expected old-source regression; the controlled scheduling and cleanup failures; a C#
fixture compile error corrected by passing UTF-8 bytes to FromSource; an initial
Linux read-only NuGet-cache setup rejection; and a Linux incremental-build attempt
that retained the old assembly because the extracted replacement sources were
older than its output. The latter is recorded as a failed test attempt, not a
passing correction. The corrected isolated managed rebuild uses Rebuild; no test
assertion or deadline changes. The first Windows scheduling driver stopped after
PowerShell promoted child stderr; subsequent independent runs retain both streams.

Correction 24599a5 passes [runtime CI](https://github.com/gmoddev/CarbonLuau/actions/runs/38821450187)
and [all three tooling platforms](https://github.com/gmoddev/CarbonLuau/actions/runs/38821450163).
Native counts are Windows 23, Linux 24 and Linux ASan/UBSan/leak checking 24, all
PASS. That matrix covers native/managed/real-VM regressions, allocation
faults, 100-addon fairness and deadlines, gameplay/GUI/inventory/persistence/Loot/
catalog, ASan/UBSan/leak checking, tooling/generation/metadata, deterministic
packaging, clean installation and all 75 bundled examples on each host. Hosted
first reloads are OK/zero scans/zero watches, Windows 49.929 ms and Linux
17.512 ms, measured for the whole Reload. Both hosted failure-isolation suites
pass. Downloaded Windows/Linux bundles have matching sidecars/provenance and
source 24599a5, exact corrected managed payloads, unchanged canonical API/manifest
bytes and no private test seam. The evidence ledger records their six artifact
hashes. Final documentation source receives its own complete CI before delivery;
the final tested SHA and immutable run links are reported with delivery.

No Rust server or protected service is changed by this investigation. Existing
D8/D9 physical Windows/Linux evidence is retained without a new live-host claim.
The test container is removed and the Windows fixtures leave no owned runtime or
compiler process. The editor repository, API/catalog counts, D6 registry, D13/C3
semantics, package/API/ABI identities, published v0.7.0 and its assets remain
unchanged. There is no release, tag or VSIX publication.

## Release disposition

The shared status defect is corrected and has an old-source failing regression.
The separately reproduced bootstrap timeout remains a legitimate bounded
initialization rejection. There is no basis to hide it by retry, warmup, a longer
deadline or an inventory-specific success exception. **The historical release
HOLD remains because the original occurrence is not conclusively identified.**
Clearing it requires an independently observed and explained relevant failure
that resolves the remaining alternatives, or equivalent defensible evidence.
More unexplained green runs alone do not satisfy that gate. No additional Loot
or catalog expansion is recommended as a response to this blocker.
