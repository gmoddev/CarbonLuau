# Entity-1A exact-target Spawn-epoch probe — gate remains open

Historical negative evidence. The later
[Entity-1A validation](WorldEntityFoundation1A-Validation.md) supersedes this
document's **then-current** D20 gate using full-virtual-call completion and
continuous startup observation; the failed-Spawn counterexample remains valid.

Research result, 2026-10-02. Starting `origin/main` was
`3c0d057c490e88bbcd9ed6d6552a2213ba7783b1` (post-`v0.5.0`). This
record supplements, and does not replace, [D20](Invariants.md#d20--worldentity-foundation-1),
the [Foundation 1A history](WorldEntityFoundation1A.md), or the
[lifetime investigation](WorldEntityLifetimeInvestigation.md). No public or
private production Entity implementation, API metadata, version change, or D20
spawn-epoch amendment was adopted.

## Exact target and positive evidence

- Rust Dedicated Server app `258550`, Steam build `25653776`, protocol
  `2634.289.1`. A separate Rust source revision was not available in the
  inspected deployment metadata.
- Linux Carbon `2.0.261.0 [2026.10.01.0] c74c4ca`. The Windows hook binary was
  inspected separately; a Windows live Entity fixture was not run in this
  investigation.
- Windows `Assembly-CSharp.dll` SHA-256:
  `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`.
- Linux `Assembly-CSharp.dll` SHA-256:
  `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`.
- Windows `Carbon.Hooks.Community.dll` SHA-256:
  `1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3`.
- Linux `Carbon.Hooks.Community.dll` SHA-256:
  `4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343`.
- Windows `Carbon.Hooks.Oxide.dll` SHA-256:
  `71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d`.
- Linux `Carbon.Hooks.Oxide.dll` SHA-256:
  `b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764`.

The extended [structural checker](../tools/Test-EntityLifetimeEvidence.ps1)
completed 1,069 checks on those exact Windows/Linux Rust assemblies and the
installed Carbon hooks. It found only two direct writers of
`BaseNetworkable.isSpawned`: `Spawn` sets it true and `DoServerDestroy` sets it
false. `IsFullySpawned` reads that field. The exact installed Community hook is
a synchronous `OnEntitySpawn` prefix on `BaseNetworkable.Spawn`; the Windows
and Linux prefix bodies match. The checker enumerated the three direct
`InitLoad` callers (save restoration, copy/paste, and Nexus transfer), each
requiring a subsequent Spawn for fully-spawned admission. These are
exact-build structural findings, not a permanent compatibility guarantee.

The disposable Linux live fixture observed `OnEntitySpawn` on the server owner
thread while the fixture entity was not yet fully spawned, followed by
`OnEntitySpawned` after full/keyed state became visible. Explicit-ID `InitLoad`
was keyed before Spawn but not fully spawned. Vetoed Kill did not destroy;
ordinary Kill removed net/registry state; direct unregister/reinsert bypassed
the tested lifecycle hooks. The wooden-box fixture was not naturally poolable,
and same-managed-object reuse was **not observed**.

## Disqualifying completion counterexample

The exact `BaseNetworkable.Spawn` body sets `isSpawned = true`, then runs
`SendNetworkUpdateImmediate` and later work before returning. The installed
`OnEntitySpawned` hook runs after the flag is set but **before** that fallible
tail. `SendNetworkUpdateImmediate` calls
`IOnSendNetworkUpdate.OnSendNetworkUpdate(BaseEntity)` through the host's
ordinary component path.

In the isolated fixture, a fixture-owned `IOnSendNetworkUpdate` component
deliberately threw from that callback. `Spawn` propagated the exception, yet
immediately afterward the exact same entity was:

```text
Spawn threw:                 true
IsFullySpawned():            true
IsDestroyed:                false
net present / ID nonzero:    true
serverEntities[ID] == self:  true
```

The `OnEntitySpawn` prefix did fire; it is not bypassed. The problem is that a
failed/partial Spawn can satisfy every proposed post-Spawn admission predicate
after `OnEntitySpawned` has already fired. A private epoch would prevent an old
proxy from retargeting, but the proposed substrate would still admit the
failed new incarnation. A next-frame check of the same fields cannot establish
whether the call returned successfully. This is a controlled callback-failure
probe, not evidence that normal vanilla spawning routinely fails.

The task's mandatory **failed/partial Spawn => no admission** gate therefore
does not close with the supported prefix and `OnEntitySpawned` hook alone.
CarbonLuau cannot add an invasive Spawn postfix/finalizer detour under D20 merely
to force PASS. D20 remains **HOST-PRIMITIVE-GATED**; its proposed spawn-epoch
amendment was not adopted. Entity-1B is not authorized by this probe.

The exact live log is retained outside the repository in the task-owned
qualification workspace under
`/root/codex/entity1a-25653776/evidence/lifecycle-live-20261002-035400/server.log`.
The repository [fixture](../tests/live/CarbonLuau.EntityLifetimeEvidence.cs)
contains the bounded reproduction. No Rust/Carbon assemblies, server binaries,
or private storage files are committed.
