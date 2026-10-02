# Entity-1A early startup observer investigation

Historical research stage, 2026-10-02; final Entity-1A closure is in
[WorldEntityFoundation1A-Validation.md](WorldEntityFoundation1A-Validation.md).
The open-gate statements below record the point-in-time investigation before
the private adapter and final platform matrix passed. This extends the preserved
[completion-fence investigation](WorldEntityCompletionFenceInvestigation.md).
D20 remains **HOST-PRIMITIVE-GATED** until the full private Entity-1A substrate,
failure matrix, and platform qualification close. This record adds no public
World/Entity API and makes no release-identity claim.

## Exact target and startup gate

The inspected Linux target is Rust dedicated-server build `25653776`, protocol
`2634.289.1`, server log revision `166494`, Carbon `2.0.261.0` at source commit
`c74c4ca8d0f7a9c8e8a431b077c0b3010f476e44`, and shipped Harmony
`2.4.2.0`. The Linux Rust `Assembly-CSharp.dll` hash is
`cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`.
The installed `Carbon.Bootstrap.dll` and `Carbon.dll` SHA-256 values are,
respectively, `624d324b2e20b7d4f110ac0d1118677f6c7b3a7963e6da6a3cb44122955eb677`
and `44d0e88a7c8c9c45f8a94a67896cb7a8da4f0f4ac73e4bdc85fba1d40f5584f4`.
The separately inspected Windows Rust `Assembly-CSharp.dll` SHA-256 is
`bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`.
A disposable Windows server run identified the same Rust build `25653776`
and protocol `2634.289.1`, but **Carbon `2.0.262.0` at commit `8a81d70`**.
This is a distinct Carbon target from the Linux `2.0.261.0` binary. The
Windows Carbon generated Spawn-transpiler type has identifier
`3dfbe84a8fb1482ea238d43e7f12aece`, not the Linux identifier; each target
requires its own exact allowlist.
A final-source
Windows/Linux structural comparison of the exact selected IL and hook prefix
passed 1,222 checks with `-SpawnEpochProof -CompletionResearch`; this is not
a Windows live AutoPatch/startup run.
The extended `-StartupObserverProof` route then passed **1,242** checks,
including matching Windows/Linux `ServerMgr.Initialize` ordering, Carbon
`FileSystem_WarmupHalt` gate IL, and the installed Carbon Spawn-transpiler
method/iterator IL. It still cannot substitute for live patch installation
and observer-continuity tests.

The Windows disposable host later updated its `Carbon.Hooks.Oxide.dll` component
while exercising a normal hook subscriber. Carbon's inspected `HooksUpdater`
downloads hook components from its CDN; the timing does **not** prove the
subscriber itself generated the new DLL. The new installed Oxide hash is
`0b668e1f4819ef3455c70781428c0afdd02601d2398eef71bd28428a8a073afa`,
MVID `eaf4dad6-6dd5-4f38-a5af-d61fab1780bc`, with the Windows generated
Spawn-transpiler type identifier `3dfbe84a8fb1482ea238d43e7f12aece`.
Carbon's `OnEntitySpawn` **prefix** belongs to
`Carbon.Hooks.Community.dll`; its generated `OnEntitySpawned` **transpiler**
belongs to the separately versioned Oxide hook component. The updated Windows
Community component is hash
`4364a8782fd012dd8cfa8acf1d778c1e6bee9bf6bbe0efb801deba685778f50e`,
MVID `e7a46446-eb51-426d-908e-59aa9cdb4677`. Its six-instruction prefix
body exactly matches the prior pinned Windows Community version. The checker now
accepts an explicit updated-Windows-Oxide input and confirms that its
12-instruction transpiler and 185-instruction iterator bodies exactly match
the previously qualified bodies after normalizing only the generated type
identifier. With both updated Windows hook-component inputs it passes
**1,249** checks. A fixed whole-file pin from the earlier copies alone would
incorrectly reject this Carbon-supported component update; any newly admitted
binary variant still needs exact structural and live qualification.

In that Carbon source, `CommunityInternal.Initialize` starts plugin processing
before the world path; `ScriptLoader.LoadAll` prepares the initial batch.
`FileSystem_WarmupHalt` withholds Rust's filesystem warmup until both Carbon
hooks are ready and `ModLoader.IsBatchComplete` is true. The shipped bootstrap
assembly was independently decompiled and contains that wait. Rust
`ServerMgr.Initialize` then performs `SaveRestore.Load` or map-entity Spawn;
Carbon's `OnServerInitialized` follows through `ServerMgr.OpenConnection`.
An initial-batch plugin's default `AfterPluginInit` AutoPatch is applied before
its `Loaded` callback and before that batch completes. **Batch completion is
not patch-success evidence:** failed compilation or zero/partial patching must
leave Entity admission unavailable.

This is a qualified *conditional ordering candidate* for a plugin already in
the initial Carbon batch, not a claim that arbitrary first-time hotload onto a
running server has historical Spawn evidence. First-time hotload must fail
closed and require a fresh, successfully observed startup. Full plugin unload
removes AutoPatch and creates an observer gap; a later hot reload cannot
silently regain the prior baseline.

## Isolated Linux observations

The task-owned disposable server copy is under
`/root/codex/entity1a-25653776/startup-probe/server-linux` on BigKVM; the
source copy under `/root/codex/carbonluau-2d/live-server` was not modified.
Runs used a two-CPU, 6-GiB, `--network none` container with loopback server
binds and no published ports. The
[startup fixture](../tests/live/CarbonLuau.EntityStartupEvidence.cs) and
[Linux runner](../tools/Test-EntityStartupLinux.py) are research-only, not
production Entity code. The separate
[Windows runner](../tools/Test-EntityStartupWindows.ps1) operates only on the
task-owned `D:\Sandbox\Codex\Entity1AStartup` copy, uses hidden/headless
loopback server startup, and stops only its own process.

- Initial hook-only run: fixture `Init` and `Loaded` preceded its first
  `OnEntitySpawn`, which occurred during map entity creation on thread 1;
  `OnServerInitialized` saw 1,732 prefix/spawned callbacks.
- A second run installed a required `BaseNetworkable.Spawn` AutoPatch during
  `AfterPluginInit`, before `Loaded` and before the first Spawn. It observed
  1,726 Carbon prefixes and 1,726 matching patch prefixes by server
  initialization, then saved 948 entities. The save completed before the
  runner stopped the disposable server.
- The next boot read that save: the log records 948 entities spawned from save
  and all **24** relevant method bodies AutoPatched before restoration. At
  `OnServerInitialized`, the research patch observed 989 Carbon prefixes,
  989 matching base patch prefixes, 989 outer completions, zero pending
  failures, and 24 patch targets. This demonstrates the restored entities in
  this fixture crossed the installed fence. It does not establish all supported
  restoration variants, Windows equivalence, or full lifetime correctness.
- A stricter method-chain fixture initially rejected 979/988 calls because its
  `MethodInfo.Equals` comparison did not reliably identify the same definition
  when reflection supplied a different reflected type. It did not establish a
  host Spawn failure. The corrected definition comparison uses module MVID and
  metadata token. A repeat saved-world boot observed a `ServerMgr.Initialize`
  prefix on owner thread 1 with 24 installed target methods and an empty
  registry before restoration; 987 Carbon Spawn prefixes matched 987 base
  patch prefixes, 987 full completions, and zero bad-chain, missing-postfix,
  exception, or skipped-original counters. Three subsequent fixture Spawns
  produced: normal completion; a network-update-tail exception with a keyed
  entity but **no** completion; and a `BaseEntity` outer-tail exception with
  **no** completion. This validates those failure paths for the research
  fixture, not yet the production lifetime adapter.
- In a separate first-install hotload run, the disposable server reached
  `Server startup complete` **without** the fixture, then loaded it into the
  running world. The `ServerMgr.Initialize` prefix had not run for that plugin;
  the fixture reported `HOTLOAD_UNQUALIFIED initializePrefix=missing` and did
  not claim a baseline. This supports restart-required, fail-closed first
  installation. It does not prove full plugin unload/reload teardown yet.
- On the task-owned Windows worker copy under
  `D:\Sandbox\Codex\Entity1AStartup\server`, an initial-batch fixture likewise
  installed all 24 Spawn patches before restoration/map spawning. The
  `ServerMgr.Initialize` prefix ran once on thread 1 with an empty registry;
  `OnServerInitialized` saw 1,741 matched Spawn prefixes and full completions,
  zero pending/bad-chain/exception/skip counts. Normal, network-tail and
  outer-tail fixture cases matched the Linux outcomes; the two thrown tails
  were not completed. The fixture saved successfully, then the bounded runner
  stopped its own process. This is **Windows live research evidence**, not a
  production adapter or D20 qualification.
- A second Windows boot read the task-owned save: the log reports 943 entities
  spawned from save (plus 32 map entities) after the single qualified
  `ServerMgr.Initialize` prefix. At `OnServerInitialized`, 983 Carbon prefixes
  matched 983 full research completions with no pending/bad-chain/exception
  counters. The three injected post-start cases again produced only the normal
  completion. This is actual Windows restoration observation; it does not
  substitute for production-adapter lifecycle qualification.
- First-install Windows hotload was also exercised: the server reached
  `Server startup complete` before the fixture was copied into the disposable
  plugin directory. Its `Init` saw 1,057 already registered entities and its
  `ServerMgr.Initialize` prefix count stayed zero; it reported
  `HOTLOAD_UNQUALIFIED` and did not claim history. The bounded runner stopped
  its own server process. A restart is required before Entity admission.
- A later Windows same-object retry probe (`startup-20261002-164835`) again
  observed 983 matched restored/map Spawn completions and passed the normal,
  network-tail, and outer-tail cases. Retrying each failed Spawn after removing
  the injected throwing component reached a new observed epoch (`2`), but the
  host rejected both retries with `ArgumentException`; neither epoch was
  marked complete. This is a fail-closed retry result, not evidence that
  same-object *successful* retry is supported by this host fixture.

The exact logs remain outside Git at:

```text
/root/codex/entity1a-25653776/startup-probe/evidence/startup-20261002-193033/server.log
/root/codex/entity1a-25653776/startup-probe/evidence/startup-20261002-193825/server.log
/root/codex/entity1a-25653776/startup-probe/evidence/startup-20261002-194341/server.log
/root/codex/entity1a-25653776/startup-probe/evidence/startup-20261002-200207/server.log
/root/codex/entity1a-25653776/startup-probe/evidence/hotload-20261002-200709/server.log
/root/codex/entity1a-25653776/startup-probe/evidence/startup-20261002-201804/server.log
D:\Sandbox\Codex\Entity1AStartup\evidence\startup-20261002-163121\server.log
D:\Sandbox\Codex\Entity1AStartup\evidence\startup-20261002-163356\server.log
D:\Sandbox\Codex\Entity1AStartup\evidence\hotload-20261002-163559\server.log
D:\Sandbox\Codex\Entity1AStartup\evidence\startup-20261002-164835\server.log
```

The first run's offline map-upload errors delayed server initialization;
those network errors are not Spawn-lifetime failures. The fixtures did not
contact an authenticated player or expose a public Entity surface.

## Private model and patch-topology review

The private, host-agnostic [lifetime model](../src/CarbonLuau/Facade/EntityLifetimeModel.cs)
now has 154 passing deterministic checks. It creates an epoch only from an
observed full-Spawn entry; the world-load marker cannot manufacture history for
an unseen keyed object. A failed restored Spawn remains inadmissible even if
current flags and registry occupancy look valid. The model uses weak host-object
references, sticky token retirement, and an owner-thread check. After an
observer gap, the same host instance cannot requalify its startup baseline;
a new process/startup observer is required. This model is not yet wired to
Carbon or a public facade. Independent review found and the model fixed a
reentrant validation edge: a binding retired during evidence or authority
recheck must fail the same validation, not only the next one. The model tests
cover both callback timings.

Independent Harmony review found a material remaining gate: a foreign
finalizer ordered after the research fixture's `Priority.Last` finalizer can
throw after that fixture marks an attempt completed. Harmony priority does not
by itself establish last-finalizer position. Production admission therefore
needs exact effective patch-topology qualification, fail-closed drift behavior,
and competing-patch tests. The research fixture now logs installed prefix,
postfix, finalizer and transpiler identities for every target. A separate
saved-world Linux run enumerated all 24 targets: the 23 `BaseEntity` override
bodies each had only the research observer's prefix/postfix/finalizer, while
`BaseNetworkable.Spawn` additionally had Carbon's `OnEntitySpawn` prefix and
its generated `Entity_BaseNetworkable_7cc7a8d1f2b14faf96c00d917542524f`
transpiler. No foreign finalizer was installed in this test environment. The
existing Carbon transpiler must be *exactly qualified and allowlisted*, not
blanket-rejected; any additional or changed transpiler remains a fail-closed
topology change. The run again passed normal, network-tail and outer-tail
cases. This finding is not hidden by I12 or treated as a PASS.
The pinned `Carbon.Hooks.Oxide.dll` is SHA-256
`b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764`
on Linux and `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d`
on Windows. The named transpiler method and its generated iterator `MoveNext`
have matching selected IL across those assemblies. Its inspected iterator
inserts the `OnEntitySpawned` hook call at an exact instruction index and
otherwise yields the existing instructions, including the original tail;
the fixture's injected tail failures still prevented full completion.

## Open qualification before D20 adoption

The research patch currently tests a candidate full-virtual-call prefix,
postfix and finalizer. Full correctness still requires fault results for
network-update and outer-`BaseEntity` tails, `__runOriginal` skip handling,
unexpected nested calls, foreign patch topology, observed completion after
all relevant finalizers, weak identity and sticky token retirement, first
hotload/reload continuity, bounded resource behavior, Windows/Linux final-source
matrices, and private domain/VM/publication integration. No research-only
counter should be mistaken for a production Entity lifetime token.
