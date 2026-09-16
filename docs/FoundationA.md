# Foundation A — shared-VM multi-domain runtime foundations

Foundation A implements internal lifetime, admission and publication primitives for
the canonical addon architecture in [Invariants.md](Invariants.md). It does not
expose addon registration, package transport, dependency resolution, public-module
imports or an addon scripting API. The current public scripting identity remains
`0.3.0-experimental`.

## Lifetime model

The managed/native ownership chain is now explicit:

```text
NativeRuntime / CarbonLuau host lifetime
└── RuntimeGeneration / one native VM generation
    ├── RuntimeDomain / operator-root lifetime R1
    ├── RuntimeDomain / operator-root candidate R2
    └── future addon-capable domains
```

`NativeRuntime.HostLifetimeId`, native `VmGenerationId` and native domain handles
are distinct monotonically allocated identities. A domain handle is also its
`DomainLifetimeId`; it is never reused within the native-library lifetime. Retired
domain records remain bounded tombstones while the VM lives so a closure retaining
a domain-bound native upvalue fails as stale instead of dereferencing freed memory.

A healthy operator reload creates and initializes a provisional root domain inside
the current VM. Ordinary failure destroys only that candidate. Successful commit
publishes it at the owner-thread boundary and retires the old root. A timeout or
integrity failure still retires the whole VM; D9 reconstruction creates a new VM
generation and then a new root domain.

## Native representation

Native ABI 1.3 adds internal domain operations while retaining ABI 1.0–1.2 exports
as compatibility wrappers for historical fixtures:

- `cl_vm_generation`
- `cl_domain_create` / `cl_domain_destroy` / `cl_domain_commit`
- `cl_domain_module` / `cl_domain_load_source`
- `cl_domain_facade` / `cl_domain_event`

One VM owns a bounded set of domain records. Each live domain owns its source/module
map, loading stack, committed and provisional callback queues, module-cache refs,
facade refs and host callback. The semantic cache key is therefore the containing
VM generation, defining domain handle and logical module path. Entry chunks and
first module executions each use a fresh `luaL_sandboxthread` environment populated
with domain-bound `require`, `task` and facade values. The protected base libraries
remain VM-wide.

The VM owns one `AdmissionContext` during every Luau resume. It records the admitted
domain, operation identity and provisional state; the existing monotonic deadline
continues to cover the complete synchronous call chain. The managed `InsideNative`
guard and native admission checks reject host-driven recursive execution. The
registry mutex is recursive only so a rejected reverse-call can return a controlled
status rather than deadlock; it does not authorize nested VM entry.

## Publication model

Every first module execution creates a nested native `PublicationScope`.

- Cache refs and native task callbacks are staged in that scope.
- A failed scope unreferences its cache candidates and task threads.
- A successful nested scope merges into its parent.
- A successful scope in an active operation publishes immediately.
- A successful scope under a provisional candidate merges into that candidate;
  domain commit publishes cache refs and callbacks together.
- Candidate destruction discards all remaining provisional state.

Domain-bound host calls that create or remove registrations open a matching managed
facade checkpoint. Nested commit merges the checkpoint; rollback removes commands
and listeners added by the failed module attempt. This is deliberately limited to
CarbonLuau-owned publication. Plain Luau tables, globals and leaked references are
not rolled back.

The module value contract is unchanged: first result only, `nil`/no result becomes
`true`, extra results are ignored, successful values are cached by reference,
failures retry, cycles are controlled, depth is 32, initialization cannot yield,
module globals are private and closures retain their defining environment. Ordinary
errors remain catchable; private deadline cancellation remains uncatchable and
VM-fatal.

## Qualification fixtures

The native `ScriptCore` fixture now proves:

- two independently owned domains coexist in one VM;
- equal logical module paths have distinct domain cache entries;
- provisional module cache/task state is invisible before commit and publishes
  together at commit;
- retiring one domain removes only its cache/queue while the other continues;
- stale domain entry is rejected;
- a caught failed module publishes no cache/task and a retry executes again.

Managed runtime/facade fixtures prove:

- CarbonLuau host, VM generation and root-domain identities are distinct;
- ordinary failed and successful root replacement keep the VM generation;
- fatal recovery changes the VM generation;
- caught failed module initialization can register a command and queue a task yet
  leaves neither resource nor cache entry eligible for candidate commit;
- the same failed module retries twice in one successful candidate;
- historical module, scheduler, facade, recovery and unload behavior remains green.

Windows qualification uses the native CTest suite and the real net48 managed/native
interop executable. Linux qualification uses the same sources/tests in the existing
Ubuntu container and sanitizer jobs. These tests qualify Foundation A primitives,
not public addons, provider lifecycle, package parsing, dependency graphs, scale
policy or an addon API version.

## Deliberately deferred

Foundation A does not implement D5 package-qualified imports, D14 registration or
provider protocols, dependency graph transitions, addon metadata, public module
visibility, provider archives, addon scheduler fairness/scale policy, operator-root
consumption of addons, or public addon documentation. Those remain gated by the
canonical compatibility and qualification work.
