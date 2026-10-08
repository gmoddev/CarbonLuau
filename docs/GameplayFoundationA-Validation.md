# Gameplay Foundation A: paused implementation checkpoint

Status: **PAUSED / INCOMPLETE**, at the user's request on 2026-10-08.
This is a portable work-in-progress handoff, not a qualified Gameplay A verdict
or a release-ready source revision. Resume from the commit containing this
record, not from an assumed clean `fc71db4` checkout.

Starting source: `fc71db4ddb2053a98ad3fd65811e64b58f75b150` on `main`.
All checkpoint changes belong to Gameplay A. No public `PlayerDied` or
`PlayerSpawned` binding, public context, API annotation, or D23 adoption has been
made. Neither member has passed its A0 live-host gate. No release or tag is
authorized by this checkpoint.

## Exact stopping point

The next step is to finish the durable proof of Carbon's in-memory Rust assembly
transformation, then correct the private observer's exact live-body pins and run
the A0 fixtures. **Do not merely accept the observed hashes or remove the gate.**

The private observer still checks original backing-PE method hashes. Carbon's
`AssemblyCSharp.InjectIPlayer` adds the `BasePlayer.IPlayer` field with Cecil;
`Patch.UpdateBuffer` serializes the modified assembly and `Patch.Load` loads the
processed bytes. This reindexes metadata tokens without changing the intended
instruction/operand semantics. A read-only in-memory reproduction independently
matched all four observed live method hashes on both Windows and Linux.
Windows opcode/resolved-operand parity also passed. The durable helper's new
reproduction mode has been written but **not executed or validated**. Complete
canonical opcode/resolved-operand and exception-handler receipts, exact
`Carbon.Startup` pins and the qualified transform-owner tuple before changing
production acceptance.

The most recent private live run deliberately failed closed at observer
readiness, before the constructed-human transition cases ran. This is negative
evidence, not a successful initial-spawn/death qualification.

Two reviewed defects remain in the new, unexposed managed lifecycle transfer
path, in both `FacadeSession.Flush` overloads:

- `GameplayEvents.ToNative(...) == false` is ignored and the dequeued event is
  still submitted. Discard/count the stale transfer without refunding a nonce
  that is already owned by a native callback.
- An exception from `Runtime.Event` after transfer can leave a dequeued
  reservation charged with no callback to acknowledge it. Establish and test
  exception-safe ownership/refund, including disposed-runtime behavior.

Fix only these ordinary Gameplay A defects and rerun affected tests. This
checkpoint must not be deployed or represented as the finished feature.

## Implementation preserved

- Private exact-host full-method death/respawn observers, terminal death
  base-chain progress marker, normal-completion fences, bounded per-connection
  life state and conservative nested-call rejection. Public capture remains
  unwired; optional position uses existing qualified no-wait Entity observations.
- Death/spawn IL research tools and isolated live fixture runners with backups,
  task-owned server leases, shutdown and restoration checks. The fixture uses
  constructed server-side connections, **not authenticated real clients**.
- Existing Signal callback-root publication correction: private bounded callback
  holder snapshots restore failed module/candidate connect/disconnect/destroy
  operations; teardown clears roots. GUI disconnect no longer retains a poisoned
  local active bit after rollback. Arbitrary Luau mutations are not rolled back.
- Strict private 12-field lifecycle payload validation and existing native event
  scheduler integration. Fixed reservation accounting persists through queue,
  callback, rejection, cancellation, domain retirement and VM teardown. Private
  acknowledgement opcode 39 is not exposed as a script host primitive.
- Managed producer/fanout/accounting policy and committed-listener demand
  tracking. Candidate constants are 128 captures/frame, 128 deliveries/frame,
  32 deliveries/domain/frame, 4,096 visits/frame, 512 held reservations and
  2,048 bytes/payload; conservative double-charged transport ceiling is 2 MiB.
  These bounds and fairness still require final qualification. The real frame
  clock and host receiver/admission callbacks are not connected yet.
- Typed Signal definition generation plus synthetic catalog tests. The existing
  one-Player Signal definitions remain unchanged. Actual pinned LSP parsing and
  inference of the richer `SignalWith` form remain unqualified.

D20's Entity Spawn observer target and patch topology were not redesigned or
edited. No Entity signals, hook bus, second scheduler, policies, inventory
signals, Gameplay B work or release publication began.

## Evidence obtained before pause

| Check | Result and scope |
|---|---|
| Windows/Linux native build | PASS for the tested private transport/publication snapshots |
| `RuntimeAllocationFaults` | PASS, Windows 4.36 s / Linux 3.54 s, including new allocation/refund/teardown tests |
| Real pinned-Luau callback publication tests | PASS Windows and Linux for existing Player/GUI Signals |
| Tooling suite, SDK 10 Linux | PASS, including richer synthetic Signal catalog tests |
| Exact Rust/Carbon static research | Death and activation/respawn method/hook timing inspected on both pinned tuples |
| Private A0 live fixtures | FAILED CLOSED at live-body pin/readiness gate; actual transition cases not reached |
| New managed budget suite | Written; not compiled, invoked or run |
| Final-source full regressions, live public delivery, sanitizers and hosted CI | NOT RUN for this checkpoint |
| Authenticated-client lifecycle behavior | UNQUALIFIED; no mock/constructed connection substitutes for it |

There is **no final tested SHA for the whole checkpoint**. Earlier PASS receipts
apply to their worker snapshots, not to later managed budget/helper additions.
The source commit containing this record is the precise resume revision, not a
qualification claim.

Pause cleanup checks passed: `git diff --check`, PowerShell syntax parsing of
the two research helpers, Windows runner and package script, and effective
Git author/committer no-reply identity. These are checkpoint hygiene checks,
not runtime qualification.

The new callback-root regression is counterevidence to an uncovered publication
case on the frozen v0.6.0 source. Preserve its historical World/Entity results,
but do not assume those frozen results qualify this correction or Gameplay A.
Any publication of that earlier candidate needs an explicit correction/backport
and affected requalification.

## Hosts, caches and receipts

Use the existing `DockerPC` and `BigVPS` SSH aliases with `BatchMode=yes` and a
finite connection timeout. Do not rebuild on the controlling PC or change
unrelated infrastructure. Worker directories are retained for another PC:

- Windows worker: `C:\Sandbox\Codex\GameplayA-20261008`.
- Linux worker: `/root/codex/gameplay-a20261008`.
- Windows isolated server: `D:\Sandbox\Codex\Entity1AStartup\server`.
- Linux isolated server: `/root/codex/world-movement-20261003/server-linux`.
- Windows native cache: worker `native\Release`; Linux cache: worker `release`.
- Worker source snapshots can lag this commit: restage the actual checkout before
  building. Do not overwrite the preserved receipts when restaging.

Both hosts use Rust build `25653776`, protocol `2634.289.1`, revision `166494`.
Windows Carbon is `2.0.262.0` / `8a81d70`; Linux Carbon is `2.0.261.0` / `c74c4ca`.
Harmony is `2.4.2.0`.

| Pinned binary | Windows SHA-256 | Linux SHA-256 |
|---|---|---|
| Rust `Assembly-CSharp.dll` | `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2` | `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450` |
| `Carbon.Common.dll` | `0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a` | `5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb` |
| `Carbon.Hooks.Community.dll` | `1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3` | `4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343` |
| `Carbon.Hooks.Oxide.dll` | `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d` | `b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764` |

The installed Windows hook directory had prior drift. The runner temporarily
uses the saved qualified hooks in `D:\Sandbox\Codex\Entity1AStartup`, then
restores the original installed files. Do not silently bless or leave the drift
replaced. Linux uses its already-pinned installed hook tuple.

Native build receipts: worker `transport-build.log`; existing callback
publication receipts: worker `publication-build.log` and the runtime outputs.
Latest negative A0 logs:

- Windows `D:\Sandbox\Codex\Entity1AStartup\evidence\gameplay-host-20261008-054154-032\server.log`.
- Linux `/root/codex/world-movement-20261003/evidence/gameplay-host-20261008-054155-1486825/server.log`.

That run's fixture package SHA-256 was
`e50f561a4221323c479fc2b31cf1b5b15f262def5ef1ed100bc4d7cb0d3315da`.
The preceding `052354`/`052357` and `053448`/`053449` receipts remain negative
evidence. All three runner pairs stopped their test servers and reported
restoration. Pause-time read-only checks found no Rust server or task build
running. Task caches/evidence are intentionally retained; no infrastructure or
unrelated workload was removed.

## Resume checklist

1. Read `AICONTEXT.md`, current canonical references and this record; verify the
   checkout and any unrelated changes. The [supplied architecture](GameplayEventsArchitecture-Research.md)
   is research, not a competing canonical owner.
2. Run `tools/Research-GameplayDeathHost.ps1 -ReproducePublicizer` with one
   `-AssemblyPath`, `-CommonAssemblyPath` and `-LiveLogPath` per pinned host,
   exact `-CecilPath`, and `-DecoderSource` pointing to
   `src/CarbonLuau/CarbonLuau.GameplayLifecycle.Carbon.cs`. Preserve canonical/EH
   parity and Startup transformation-owner hashes before accepting processed IL.
3. Correct exact qualified private observer pins, stage the actual source and
   package with `tools/package.ps1 -IncludeGameplayHostProofFixtures`. Run the
   Windows/Linux `Test-GameplayHost*` runners only on their isolated servers
   with coordinated artifacts and backups. Review generated corpse/NPC cleanup
   and verify actual initial activation, respawn and all positive/negative cases.
4. Fix and test both managed transfer defects above. Wire
   `GameplayEventBudgetTests.Run()` / `RunNative(NativeRuntime)` into focused
   runtime execution. Restage current managed files before compiling.
5. Only after A0 passes, connect the real frame clock and host capture receiver,
   expose the qualified subset via existing bootstrap Signals/dispatcher, add
   immutable captured contexts, metadata and typed definitions. Preserve exact
   original Player lifetimes and no replay/no recursive entry.
6. Qualify the complete lifecycle/publication/replacement/provider/fatal-recovery,
   resource/fairness/fault cases, existing Player/GUI/command/discovery/persistence
   regressions, exact-host live public delivery, sanitizers, tooling/LSP,
   deterministic packaging and clean install. Do not reuse snapshot PASS as
   final-source PASS.
7. Finalize only the qualified observation contract in canonical policy and
   docs/examples, then final-source hosted Windows/Linux CI, push/synchronize and
   report the required verdict. Stop before Gameplay B or a release/tag.

The authored budget tests expose `Run()` and `RunNative(NativeRuntime)`; they
are intentionally private registration tests and not proof of public context
delivery. The publication suite currently has focused invocation
`RuntimeTests.exe --gameplay-publication <native data root>` and is also wired
into the full suite.

The pinned Windows LSP found for the pending typed-Signal check is
`C:\Sandbox\Codex\Artifacts\CarbonLuauToolingD\extension\tooling\win32-x64\luau-lsp.exe`,
version `1.70.0`, SHA-256
`89e9162cf7ab828ccd48af158a43e680f263586e53b21c0de5732718224940bd`.

## Version/release handoff

The user chose **0.6.5** for Gameplay A on 2026-10-08. On qualified public
implementation, assign development scripting API `0.6.5-experimental` and its
introduction markers; no stable API claim. This choice is recorded but not yet
applied to metadata: current development API remains `0.6.0-experimental`.
Package/tag remain `0.5.0`/`v0.5.0`; ABI `1.5`, provider `1.2`, schema `1` and
Luau `c6b830185af962c82003f86784e2fe036357c830` remain unchanged. A package
publication/version mapping is a separate release task. No release/tag is part
of this pause commit or resumed Gameplay A qualification.
