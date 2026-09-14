# Phase 3 — first gameplay facade

Package 0.3.0; scripting API `CarbonLuau` / `0.3.0-experimental`; native ABI 1.2.
Pinned Luau is unchanged. [Invariants](Invariants.md) owns rules and decisions;
[API reference](api/README.md) owns author-facing signatures/semantics/limits;
[Phase3-Validation](Phase3-Validation.md) owns actual results. Implementation does
not by itself establish PASS. No Phase 4 capability is included.

## Facade and identity

`scripts/bootstrap.luau` builds game, Players, Commands, Player, Signal, Connection
and CommandContext facades. CMake embeds this trusted source into a generated native
header; no extra operator-editable bootstrap file is deployed. The private host
primitive is a bootstrap argument, not a script global. Frozen tables and private
weak maps prevent forged proxy records; the managed boundary remains authoritative.
Existing module environments/sandbox/standard-library allowlist remain unchanged.

D11 lifetime tokens are monotonic unsigned integers encoded as private decimal
strings in bounded transport. A token is scoped to its owning plugin instance;
session identity and generation checks prevent old-instance callbacks entering a
new instance. Lua only receives facades, not those host records. Managed records
bind exact BasePlayer identity, exact Network.Connection identity and user ID.
Resolve calls the actual host's active-player lookup again and checks all three.
No same-account retargeting is possible after disconnect. The active directory is
bounded to 1024; snapshots sort ordinal UserId. Display-name snapshots are at most
128 UTF-8 bytes; malformed/oversized host names fail closed.

OnServerInitialized seeds existing connections without synthesizing events. Future
OnPlayerConnected/OnPlayerDisconnected callbacks verify the captured owner thread
before touching state. Every connection event assigns a new lifetime; observed
invalidation stays latched even if a host object is reused. Connection active/connected
flags and the connection's account ID are also checked. Removal invalidates the directory before event admission. Lua Name and
UserId remain snapshot identity after removal; mutations/permission queries fail.

## Transactions and permissions

D10 was explicitly approved on 2026-09-14 after initial preparation paused:
SendMessage raises while provisional. Entry/module initialization during normal
load, reload and automatic recovery obeys the same rule. Deferred startup delivery
is not admitted to execution until commit. Already-committed message delivery is
not rolled back or deduplicated on later error/recovery.

Signals and command definitions stage in FacadeSession. Commands:Register is
initialization-only and returns no handle; generation teardown owns removal.
Native bootstrap references and staged host definitions are discarded together on
failure. A failed host publication also preserves the old active state.

The Carbon adapter isolates SDK coupling behind ICommandRegistrar. Its convenience
AddChatCommand API returns void and does not implement a multi-name transaction.
The SDK adapter prepares fresh chat command objects and a replacement chat list,
preserves foreign entries, rejects collisions across all three command factories,
then performs one list-reference assignment. No host callback, permission hook,
allocation or yield follows that assignment in Publish. Execution is serialized on
the server thread. Old delegates capture the old FacadeSession; they do not consult
a mutable router that could redirect a late selection to the new generation.

Carbon permissions are checked before managed queue admission and immediately
before Lua entry. No permission means public to connected players; no extra admin
bypass is introduced. Server/RCON callers are rejected. Parsed arguments are
validated and copied immediately; the host's mutable array never reaches Lua.

Missing permission metadata is registered only after commit, once per generation,
and bounded to 256 newly encountered names per plugin lifetime. Existing permissions
can be referenced without ownership takeover. Failure logs a bounded diagnostic and
does not bypass authorization or retroactively reject a committed generation.
Metadata can outlive a script generation until plugin unload; Carbon owns stored
grant persistence. No Lua permission mutation exists.

Adapter source inspection used Carbon revision
`4d1b081eadef99da774e0342899bddcd638e26d2`:
[Command library](https://github.com/CarbonCommunity/Carbon/blob/4d1b081eadef99da774e0342899bddcd638e26d2/src/Carbon.Components/Carbon.Common/src/Oxide/Command.cs),
[manager](https://github.com/CarbonCommunity/Carbon/blob/4d1b081eadef99da774e0342899bddcd638e26d2/src/Carbon.Components/Carbon.Bootstrap/src/Components/CommandManager.cs),
[SDK contract](https://github.com/CarbonCommunity/Carbon/blob/4d1b081eadef99da774e0342899bddcd638e26d2/src/Carbon.Components/Carbon.SDK/src/Commands/Contracts/ICommandManager.cs).
Retained Rust metadata confirms FindByID uses the active-player dictionary and
ChatMessage routes through a fixed `chat.add` system-chat operation. No arbitrary
console command string is exposed. Inspection is separate from live qualification.

## Scheduling and ABI

Managed intake stores bounded immutable field payloads, not BasePlayer references.
It holds at most min(MaxQueuedCallbacks, 256) deliveries, 16 KiB each. Drain admits
up to 64 per frame into the existing native queue within the same Stopwatch budget;
it then consumes eligible callbacks under the unchanged 256-attempt/per-frame and
per-callback limits. The native queue cap remains shared with task callbacks. These
are separate bounded queues, not a single combined cap. Overload rejects new work.

Each listener is a distinct work item, in registration order; disconnect checks
suppress queued deliveries. No event hook executes Lua directly. Callback errors
remain isolated while the VM is healthy. Whole-VM timeout and D9's one-reconstruction
allowance are unchanged. Cancelled intake and native work are included in counters.
Status adds scripting identity plus pending/listener/command counts.

ABI 1.2 adds cl_vm_facade and cl_vm_event without changing prior POD layouts.
The cdecl host callback borrows request/response storage for the synchronous call
only. Context is uint64 generation, never a managed pointer or GCHandle. Requests
and events are at most 16384 bytes; the native-owned response buffer is 262144 bytes
per facade VM. Fields are NUL-delimited UTF-8 with explicit byte lengths. Native
parsing returns Lua-owned strings/tables; neither side retains borrowed pointers.

| Private host operation | Purpose |
|---|---|
| 0 | Bounded interop/metadata setup before script deadlines; no host publication or messaging |
| 1 / 2 / 3 | Player snapshot / account lookup / lifetime resolution |
| 4 / 5 | Message / permission query |
| 6 / 7 / 8 | Listener registration / disconnect / provisional command definition |
| 9 | Generation/listener/connection/permission gate immediately before Lua callback entry |

Operations 0 and 9 cannot be requested by the private Lua primitive. Bootstrap
itself is not a source of authority. Managed callbacks catch every exception and
return failure; native code raises a Lua error only after managed frames return.
Only curated project validation messages cross to scripts, not host exceptions.

NativeRuntime roots each FacadeSession/delegate until native VM destruction
succeeds, including partial initialization. Reverse callbacks may not reenter a
VM operation: the managed native-entry guard rejects attempts before taking the
native registry mutex. Hooks may enqueue bounded data while Lua is active, but
never enter the native queue recursively. Nested unload requests stop further work
and defer destruction until the current outer execution returns.

Cold managed interop preparation is bounded host setup outside user execution.
It exercises codecs and local metadata operations, then clears scratch definitions;
it never sends a message or publishes a command. This avoids charging initial
bridge/JIT setup to the first 3 ms user callback. It does not make host calls hard
preemptible or exclude ordinary callback host work from the deadline.

## Validation and limits

The managed regression suite exercises the actual native library, fake host views
for deterministic lifetime/permission/transaction tests, shipped examples, and
bounded stress. Native allocation fault tests cover installation, facade entry,
event admission/callbacks and timeout retirement under sanitizer instrumentation.
Test-Api.ps1 checks versions, service/type references and relative links. Ordinary
CI does not run Rust servers.

Worker live fixtures are test-only cszip additions. They create real Rust
BasePlayer/Network.Connection objects on an isolated zero-client server, exercise
the production adapter and actual Carbon command/permission APIs, remove fixture
objects and verify host/native teardown. They are not automated client connections
and do not prove client message receipt. Fixtures use NextFrame for the controlled
test root and separately verify the production drain.

Limits, experimental compatibility, unavailable services and deferred capabilities
are in the [public reference](api/Compatibility.md). Host-registry inspection bounds
reject new publication, not cleanup of already-owned commands. VM caps exclude
compiler, managed/native containers and host memory; compilation, setup, source
snapshots and host registration are bounded but not hard-preemptible. Provider
qualification is separate.
