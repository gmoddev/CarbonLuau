# Player Interaction Foundation 1D

Player-1D implements the D18 spatial mutation:

```lua
Player:Teleport(Vector3.new(100, 20, -50))
```

It returns no values. The implementation revision requiring any later
supplemental authenticated-client qualification is
`74b9b48e9375177bc17e76a4d939956d1076645b`.

## Contract and eligibility

Position must be a CarbonLuau `Vector3`. Components have already passed the
finite `System.Single` envelope and are converted directly to Unity world X/Y/Z
without axis conversion, clamping, terrain projection, collision adjustment or
safe-position search.

Every call revalidates the exact D11 Player object and Network.Connection
lifetime immediately before mutation. The owning root or addon facade must be
active and committed. The Player must be alive, non-spectating,
non-wounded and non-incapacitated. Sleeping is accepted and remains set.
Disconnected Players and old same-account proxies fail and can never retarget a
new connection.

Candidate entrypoints, first-load modules, foreign shared modules and dependency
calls remain provisional under D10 and cannot invoke Teleport. `task.defer`
work runs only after successful publication; a failed candidate publishes no
movement. Retired addon facades fail closed, while a reconstructed consumer can
use the fresh dependency lifetime.

## Exact target adapter

The supported target is Rust Dedicated Server Steam build `25353106`, Carbon
`2.0.259.0`, and `Assembly-CSharp.dll` SHA-256
`22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b`.
The exact assembly was inspected before implementation. Current
`BasePlayer.Teleport(Vector3)` performs only `MovePosition` plus the
`ForcePositionTo` RPC and is therefore not the complete CarbonLuau adapter.

The adapter uses this target-build sequence:

1. pause fly, speed and tick-distance detection for five seconds and apply the
   four-second host stall protection window;
2. if mounted, call the supported lightweight `DismountPlayer(Player, true)`
   path so mount references and callbacks are cleared without host dismount-
   point relocation or its invalid-position kill behavior;
3. detach remaining host parenting with world position preserved and update the
   unparent time;
4. enable server-fall reconciliation, call `MovePosition` with the exact target,
   and clear inherited estimated velocity;
5. send the exact Player `ForcePositionTo` RPC, update the network group, and
   send an immediate network update;
6. clear server-fall state for an awake Player, while leaving it active for a
   sleeping Player and preserving the sleeping flag.

The adapter does not start the portal-specific loading/snapshot handshake. That
path requires client completion state and is not appropriate for a synchronous
API with no client acknowledgement. Held-item state is not modified. Normal
destination trigger and physics behavior belongs to Rust; no traversed path is
synthesized.

## Mutation boundary and verification

The first anti-cheat pause is the earliest irreversible host step. Inspection,
Vector3 validation, D11 validation and eligibility rejection occur before that
boundary. Any exception after mutation begins becomes a bounded controlled
error stating that Player state may have changed. CarbonLuau does not move the
Player back, claim rollback, replay a Teleport during recovery or expose a raw
host exception.

After the sequence, one bounded verification pass confirms the original exact
connection is still current, the root Transform contains the requested
single-precision coordinates, mount and parent references are clear, and the
sleeping flag matches its pre-mutation value. A mismatch is a controlled
post-mutation error. `UpdateNetworkGroup` is part of the sequence; no polling or
client acknowledgement is performed.

Host callbacks may run inside Rust operations, but the existing global native
entry guard rejects recursive Luau VM entry. CarbonLuau-owned queued work is
admitted only after the active entry returns. Teleport remains one synchronous
facade operation under the original admitted-operation deadline and is never
split across Luau turns.

## Qualification

Focused model and real-VM tests cover valid, zero, signed, fractional and
maximum finite destinations; wrong types and invalid Vector3 construction; no
return values; alive, sleeping, dead, wounded, incapacitated and spectating
states; exact mount/parent terminal state; D11 disconnect and same-account
reconnect; root candidate/module rejection; successful and failed deferred
publication; foreign public-module laundering; addon retirement and required
consumer reconstruction; pre-mutation failure; post-mutation failure; exactly
one verification; mismatch; curated diagnostics; recursive-entry rejection;
replacement, fatal recovery and no replay through the existing lifecycle
suites.

The complete local Windows managed/native runtime suite passed, including
Player-1A through Player-1C, Foundations A through G, addon/provider,
publication, recovery, GUI, parser/package and loader coverage. Affected native
ScriptCore, RuntimeCore and load/unload CTests passed. The existing Foundation G
Windows compiler-worker 256 MiB containment check did not qualify on this
workstation and remains part of the separate Windows live/local deferral; Linux
passed it. The complete Linux managed/native suite passed, and all five Linux
ASan/UBSan/leak CTests passed after the private native facade-operation change.
DockerPC Windows live/local qualification was unavailable and is
**DEFERRED / UNQUALIFIED**. Hosted Windows CI is recorded separately and does
not substitute for DockerPC.

Three complete isolated live Carbon cycles passed on the exact target build.
Each used a real server-created BasePlayer and Network.Connection and covered
exact/repeated movement, an in-map network-group-scale move, sleeping state,
wounded/dead rejection, real parent detachment, real chair mount cleanup,
disconnect/reconnect staleness, 100 registration replacements, timeout recovery,
production NextFrame scheduling and native/plugin teardown. These fixtures do
not represent an authenticated game client.

## Authenticated-client status

**IMPLEMENTED / AUTHENTICATED-CLIENT UNQUALIFIED.** No suitable authenticated
current Rust client was available. Revision
`74b9b48e9375177bc17e76a4d939956d1076645b` must later be tested for short and
long moves, network-group transition, immediate rubber-band, moving state,
ordinary collision geometry, fall/fall-damage behavior, sleeping, mount/parent
convergence, repeated moves, disconnect/reconnect, root/addon replacement and
fatal recovery. Server evidence is not described as client convergence.

## Identity and remaining scope

Player-1D is additive under the still-unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and pinned Luau revision
`c6b830185af962c82003f86784e2fe036357c830` are unchanged. Private host
operation 28 extends only the build-embedded facade protocol.

Player-1E, Inventory-M1/M2, GiveItem, TakeItem, health mutation, Velocity,
Rotation, CFrame, entity/world APIs, spawn points, teleport history and safe or
animated teleport remain unimplemented.
