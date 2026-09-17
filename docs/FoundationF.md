# Foundation F: invariant-owner architecture

Verdict: **PASS**.

Foundation F is a behavior-preserving decomposition of the v0.4.0 candidate.
It assigns implementation owners to the major runtime, lifecycle and security
invariants without changing scripting behavior, the ABI, provider protocol,
package schema, limits or the pinned Luau revision.

Starting commit: `f54d7678aa56bea1ad5104ed31350cd13e409169`.

## Managed ownership

| Component | Primary ownership |
|---|---|
| `Runtime/RuntimeConfig.cs` | Validated runtime policy values and managed ABI value types |
| `Runtime/NativeLibraryLoader.cs` | Explicit platform library loading, probe validation and unload |
| `Runtime/NativeRuntime.cs` | Native ABI binding, host lifetime, owner-thread and reentry checks |
| `Runtime/RuntimeGeneration.cs` | Managed VM handle and generation lifetime |
| `Runtime/RuntimeHost.cs` | Legacy generation replacement and recovery coordination |
| `Scripts/ScriptSnapshot.cs` | Script path confinement and immutable source snapshots |
| `Scripts/NativeScripts.cs` | Domain, module and scheduler ABI adaptation |
| `Scripts/RuntimeDomain.cs` | Managed domain lifetime and stale-domain behavior |
| `Scripts/ScriptHost.cs` | Root admission, recovery and bounded global draining |
| `Facade/PlayerDirectory.cs` | Exact reconnect-safe player identity |
| `Facade/CommandRegistry.cs` | Command publication contract |
| `Facade/FacadeSession.cs` | Domain-bound facade session and transactional host resources |
| `Facade/NativeFacade.cs` | Native host-call bridge binding and callback roots |
| `Addons/AddonPackage.cs` | Package policy, ZIP/JSON parsing and immutable package snapshots |
| `Addons/AddonRegistry.cs` | Provider ownership, registration states and activation coordination |
| `Addons/DependencyGraph.cs` | Exact dependency bindings, loss propagation, cycles and restoration |
| `Addons/AddonActivation.cs` | Transactional addon-domain activation and retirement |

The Carbon plugin remains the composition root. Addons depend on script-domain
and publication behavior, scripts depend on runtime/domain behavior, and facade
objects depend on domain lifetime. No service locator, reflection framework or
new global managed registry was introduced.

## Native ownership

| Component | Primary ownership |
|---|---|
| `Runtime.cpp` | Exported C ABI validation and execution coordination |
| `runtime/RuntimeInternal.hpp` | Private subsystem types and narrow cross-component declarations |
| `runtime/VmRegistry.cpp` | VM handle registry, generation identities and owner-thread lookup |
| `runtime/VmState.cpp` | VM allocation, sandbox initialization, domain teardown and thread cleanup |
| `runtime/Publication.cpp` | Nested cache, callback and facade publication commit/rollback |
| `runtime/Deadline.cpp` | Private uncatchable deadline interrupt |
| `scripts/Compiler.cpp` | Synchronous pinned Luau compiler invocation and compile options |
| `scripts/ModuleLoader.cpp` | Module resolution, visibility, cache, cycles, depth and task queues |
| `facade/FacadeBridge.cpp` | Bootstrap installation, host codec boundary and event admission |

`Runtime.cpp` no longer includes private implementation blobs. The former
`Scripts.inl` and `Facade.inl` implementations are independently compiled
subsystems. The C ABI still holds the same recursive registry lock and preserves
the same serialized owner-thread execution model.

Sandbox initialization remains with VM state because it is part of state
construction and fatal retirement. Domain destruction also remains with VM state
because it releases Luau references owned by that state. These are deliberate
deviations from a one-file-per-concept example, not missing owners.

## Invariant map

| Invariant | Implementation owner |
|---|---|
| VM handle validity and generation identity | `VmRegistry.cpp`, `RuntimeGeneration.cs` |
| Domain lifetime and stale validation | `VmState.cpp`, `RuntimeDomain.cs` |
| Deadline cancellation | `Deadline.cpp` and the execution boundary in `Runtime.cpp` |
| Sandbox initialization | `VmState.cpp` |
| Source compilation | `Compiler.cpp` |
| Module resolution/cache/cycles | `ModuleLoader.cpp` |
| Provisional publication | `Publication.cpp`, `FacadeSession.cs` |
| Script path confinement | `ScriptSnapshot.cs` |
| Provider ownership and registration | `AddonRegistry.cs` |
| Dependency lifetime bindings | `DependencyGraph.cs` |
| Player reconnect identity | `PlayerDirectory.cs` |
| Command collision/publication | `CommandRegistry.cs`, `FacadeSession.cs` |
| Facade codec and validation | `FacadePolicy.cs`, `FacadeBridge.cpp` |

## Remaining concentrations

`Runtime.cpp` still owns source-load, callback-resume and thread-resume ABI
coordination. `FacadeSession.cs` retains listeners, commands and publication
checkpoints because they share one atomic host-resource transaction.
`RuntimeInternal.hpp` exposes the private VM/domain data model to native
subsystems, and the bounded process-wide VM registry and recursive mutex remain
global native state. These are explicit remaining coupling points for review.

This organization is not itself a security guarantee. The guarantee continues
to come from the canonical invariants and qualification tests.

## Compiler boundary and Foundation G

All production `Luau::compile` calls now pass through `scripts/Compiler.cpp`.
The boundary deliberately remains synchronous, uses the existing optimization
and debug options, runs under the existing serialized execution path and returns
the same bytecode representation. No compiler worker, deadline, process,
transport or lock-scope change was introduced. Foundation G can begin at this
owner without first extracting compilation from runtime, module and facade code.

## Identity and scope

Package `0.4.0`, scripting API `CarbonLuau 0.4.0-experimental`, native ABI 1.4,
provider protocol `CarbonLuau.Addons` / 1.2, package schema 1 and Luau revision
`c6b830185af962c82003f86784e2fe036357c830` remain unchanged. Foundation G and
compiler containment were not started.

## Qualification

The clean DockerPC Windows worker passed all four native targets, static MSVC
runtime import checks, the complete managed runtime/addon suite, 100 managed
native load/unload cycles, deterministic packaging, API documentation checks and
the architecture guard. The representative 100-addon result retained the same
1,949,081 VM bytes and service ordinals 50/95/99/100; all unrelated saturated
scheduler consumers progressed in the first service frame. Windows log SHA-256:
`662eaa8d63c4610875231e377741b5b0642c02ab3be73767d9dafb6416da9a36`.

The clean Ubuntu 24.04 container passed all four release native targets, the
complete Mono managed runtime/addon suite, loader/export checks and the same
representative 100-addon fixture. A separate Debug build passed all four targets
with ASan, UBSan and leak detection enabled. Linux log SHA-256:
`e62b14b1093258b794df940541f9cbe31542e181dd7f16db274300bc723516af`.

The release identity, intended package contents and byte-for-byte deterministic
Windows release bundle check passed using the qualified native DLL. No live
Carbon rerun was required because this phase changed no host integration or
runtime semantics; Foundation E's live evidence remains scoped to its tested
commit rather than being relabeled as Foundation F evidence.
