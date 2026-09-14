# CarbonLuau — First-Version Architecture and Codex Implementation Brief

**Status:** implementation design for v0.1 prototype  
**Target:** Carbon-modded Rust Dedicated Server  
**Primary deployment:** Linux x64 managed Rust host; Windows x64 local development  
**Language boundary:** Carbon/C# host ↔ native Luau VM  
**Scope:** server-side Luau only

---

## 1. Objective

Implement a Carbon-only plugin that embeds the open-source **Luau** VM into a Rust dedicated server and allows server owners to write server gameplay/admin logic in `.luau` files.

The first version should prove that this architecture is viable and safe enough to iterate on. It is **not** intended to expose the entire Rust, Carbon, Oxide, Unity, or Roblox API surface.

The end-state of v0.1 should support code like:

```lua
local Players = game:GetService("Players")
local Commands = game:GetService("Commands")
local Server = game:GetService("Server")

Players.PlayerAdded:Connect(function(player)
    print(`{player.Name} joined from Luau`)
    player:ChatMessage("This message came from Luau.")
end)

Commands:Register("luautest", {
    permission = "carbonluau.test"
}, function(ctx, args)
    if ctx.Player then
        ctx.Player:ChatMessage(`Luau is alive, {ctx.Player.Name}.`)
    else
        Server:Log("luautest executed from server console")
    end
end)
```

The plugin must remain robust if a script:

- throws an error;
- enters an infinite loop;
- allocates excessive Luau memory;
- retains a player object after disconnect;
- attempts `require("../../...")` path traversal;
- registers a conflicting command;
- is reloaded repeatedly.

---

## 2. Research conclusions that constrain the design

### 2.1 Carbon is suitable for a modular Carbon-only host

Carbon supports `CarbonPlugin` as a Carbon-specific plugin base class. It also supports `.cszip` packages containing a plugin split across multiple C# partial-class source files. Production packages are placed in `carbon/plugins`; the unpacked `cszip_dev` form is explicitly debug-build-only.

This makes `.cszip` the preferred packaging format for the managed plugin layer.

### 2.2 Carbon plugin development currently targets .NET Framework 4.8

Carbon's current project setup documentation instructs plugin developers to target **.NET Framework 4.8** and import `Carbon.targets` from a local Carbon development server.

This matters for Luau bindings: modern `luau-dotnet`/NuLua packages target .NET Standard 2.1 or newer. .NET Framework 4.8 supports .NET Standard 2.0, not 2.1. Therefore the MVP must **not depend on NuLua or another netstandard2.1-only package**.

Use a small native bridge/P/Invoke layer instead.

### 2.3 Carbon already provides the host-side primitives needed for an MVP

Carbon provides:

- hooks such as server initialization and player lifecycle hooks;
- chat/console/universal commands with permission/auth-level support;
- timers plus `NextFrame`/`NextTick`;
- JSON plugin configuration;
- profiling of plugin assemblies and hooks.

The Luau layer should use these primitives rather than creating its own threads or bypassing Carbon's lifecycle.

### 2.4 Luau is designed to be embedded and provides the safety primitives we need

Official Luau exposes a C API, separate compiler and VM components, coroutine/thread APIs, sandbox helpers, allocator customization, and an interrupt callback.

For a sandboxed embedder Luau specifically recommends:

- `luaL_sandbox` for the shared global state;
- `luaL_sandboxthread` for individual script execution threads;
- an interrupt callback to terminate runaway execution;
- allocator/memory tracking to limit VM memory;
- explicit host-controlled API exposure.

These features are first-version requirements, not optional hardening.

---

## 3. Non-goals for v0.1

Do **not** implement the following in the first version:

- client-side Luau;
- Roblox API compatibility;
- generic access to arbitrary Carbon hooks by string;
- generic access to arbitrary C#/.NET objects;
- reflection;
- Harmony patching from Luau;
- arbitrary filesystem access;
- arbitrary network/HTTP access;
- process execution;
- direct Unity object exposure;
- raw `BasePlayer`, `BaseEntity`, `Item`, `GameObject`, or Carbon object references inside Luau;
- persistent DataStore API;
- cross-server RPC;
- async HTTP promises;
- debugger/profiler integration beyond logging and Carbon's existing profiler;
- Luau native codegen/JIT;
- Oxide compatibility as a design requirement.

Keep the boundary intentionally narrow.

---

## 4. Top-level architecture

```text
Rust Dedicated Server
        │
      Carbon
        │
        ▼
CarbonLuau.cszip
  ├── lifecycle
  ├── config
  ├── Rust/Carbon hook bridge
  ├── command bridge
  ├── timer/scheduler integration
  ├── safe object proxies
  └── native runtime loader
        │
        │ P/Invoke / stable C ABI
        ▼
carbonluau_native
  ├── Luau.Compiler
  ├── Luau.VM
  ├── VM allocation accounting
  ├── sandbox setup
  ├── source compilation
  ├── coroutine execution
  ├── execution deadline interrupt
  └── C# host-dispatch callback
        │
        ▼
Server-owned .luau scripts
```

### Core trust rule

**Luau scripts never receive raw managed/native game objects.**

They receive immutable/simple value data plus proxy objects backed by stable identifiers. Every operation crosses a validated host-call boundary.

---

## 5. Repository layout

Codex should create a repository approximately like:

```text
CarbonLuau/
├── README.md
├── LICENSE
├── THIRD_PARTY_NOTICES.md
├── docs/
│   ├── architecture.md
│   ├── scripting-api.md
│   └── deployment.md
├── src/
│   └── CarbonLuau/
│       ├── CarbonLuau.Main.cs
│       ├── CarbonLuau.Config.cs
│       ├── CarbonLuau.Lifecycle.cs
│       ├── CarbonLuau.Native.cs
│       ├── CarbonLuau.Runtime.cs
│       ├── CarbonLuau.Scheduler.cs
│       ├── CarbonLuau.Hooks.cs
│       ├── CarbonLuau.Commands.cs
│       ├── CarbonLuau.Players.cs
│       ├── CarbonLuau.Items.cs
│       ├── CarbonLuau.Permissions.cs
│       ├── CarbonLuau.Diagnostics.cs
│       └── Bootstrap.generated.cs
├── native/
│   ├── CMakeLists.txt
│   ├── include/
│   │   └── carbonluau.h
│   ├── src/
│   │   └── carbonluau.cpp
│   └── third_party/
│       └── luau/              # pinned submodule or pinned source dependency
├── scripts/
│   └── bootstrap.luau         # source for generated Bootstrap.generated.cs
├── examples/
│   ├── hello.luau
│   ├── join-message.luau
│   └── command.luau
├── tests/
│   ├── native/
│   └── integration/
└── tools/
    ├── build-native.ps1
    ├── build-native.sh
    ├── generate-bootstrap.ps1
    └── package.ps1
```

Do not place all managed code into one C# file. Carbon's `.cszip` format exists specifically to support multi-file partial plugins.

---

## 6. Deployment layout

Production server:

```text
carbon/
├── plugins/
│   └── CarbonLuau.cszip
├── configs/
│   └── CarbonLuau.json
└── data/
    └── CarbonLuau/
        ├── native/
        │   ├── linux-x64/
        │   │   └── libcarbonluau_native.so
        │   └── win-x64/
        │       └── carbonluau_native.dll
        └── scripts/
            ├── init.luau
            └── modules/
                └── Example.luau
```

The native library is deliberately **outside** the `.cszip`. Carbon documents `.cszip` as a source package; do not assume arbitrary native payloads inside the archive will be extracted or loadable.

---

## 7. Phase 0 — deployment feasibility gate

Before implementing the full scripting API, Codex must prove native loading on the actual target environment.

### Phase 0 objective

Create the smallest possible Carbon plugin plus native shared library:

```c
int carbonluau_probe(void) { return 0x4C554155; }
```

The Carbon plugin must:

1. resolve an absolute path under `carbon/data/CarbonLuau/native/<rid>/`;
2. load the library;
3. call `carbonluau_probe()`;
4. print a success message;
5. unload cleanly.

### Required targets

- Windows x64 local Carbon development server;
- Linux x64 target server/host.

### Native loader requirement

Do **not** depend solely on default `DllImport` search behavior.

Implement an explicit absolute-path loader:

- Windows: `LoadLibraryW` + `GetProcAddress`;
- Linux: `dlopen` + `dlsym`;
- bind exported functions to C# delegates.

This avoids assumptions about the Rust server's current directory and Mono native-library search path.

### Stop condition

If the managed host blocks loading custom native libraries, stop and document that constraint before building the remaining runtime. Do not attempt to work around host policy.

---

## 8. Native bridge design

The native bridge should expose a **small project-owned C ABI**, not Luau's entire C API directly to C#.

Rationale:

- keeps C++ exceptions and Luau implementation details below the ABI;
- reduces P/Invoke surface;
- prevents C# from accidentally corrupting the Luau stack;
- allows Luau to be upgraded behind a stable CarbonLuau ABI;
- centralizes allocator, sandbox, interrupt, and traceback behavior.

### Required ABI characteristics

- `extern "C"` exports;
- C calling convention;
- opaque handles only;
- explicit ownership of every returned allocation;
- no STL objects across ABI;
- no C++ exceptions crossing ABI;
- every function returns a status code and optionally fills an error buffer/result struct.

### Suggested ABI

Names may change, semantics may not.

```c
typedef struct cl_vm cl_vm;
typedef struct cl_thread cl_thread;
typedef uint64_t cl_callback_id;

typedef enum cl_status {
    CL_OK = 0,
    CL_YIELDED = 1,
    CL_RUNTIME_ERROR = 2,
    CL_COMPILE_ERROR = 3,
    CL_MEMORY_LIMIT = 4,
    CL_TIMEOUT = 5,
    CL_INVALID_ARGUMENT = 6,
    CL_INTERNAL_ERROR = 7
} cl_status;

typedef struct cl_vm_config {
    uint64_t memory_limit_bytes;
    uint32_t optimization_level;
    uint32_t debug_level;
} cl_vm_config;

typedef struct cl_exec_limits {
    uint64_t deadline_monotonic_ns;
} cl_exec_limits;

cl_status cl_vm_create(const cl_vm_config* config, cl_vm** out_vm);
void      cl_vm_destroy(cl_vm* vm);

cl_status cl_vm_load_source(
    cl_vm* vm,
    const char* chunk_name,
    const char* source,
    size_t source_len,
    cl_thread** out_thread);

cl_status cl_thread_resume(
    cl_thread* thread,
    const cl_exec_limits* limits,
    /* argument/result transport */);

void      cl_thread_destroy(cl_thread* thread);

uint64_t  cl_vm_memory_bytes(const cl_vm* vm);
const char* cl_last_error(const cl_vm* vm);
```

The exact argument/result transport should be the smallest mechanism needed by the bootstrap/host-call interface. Avoid designing a general serializer in v0.1.

---

## 9. Luau VM setup

### 9.1 VM composition

Link at minimum:

- `Luau.VM`
- `Luau.Compiler`

Do not enable native code generation for v0.1.

### 9.2 State model

Use:

- one Luau **global VM state** per CarbonLuau runtime;
- one sandboxed Luau thread/environment per root script callback execution;
- module caching scoped to the VM generation.

A plugin/script reload creates an entirely new VM generation.

### 9.3 Libraries

Expose safe standard libraries needed for ordinary Luau code:

- base;
- math;
- string;
- table;
- coroutine;
- bit32;
- utf8;
- buffer;
- vector if straightforward.

Do **not** expose host filesystem, process, environment, or debug facilities.

### 9.4 Sandboxing

At VM creation:

1. open only the intended standard libraries;
2. install the host bootstrap primitives;
3. make shared library/global tables immutable where appropriate;
4. call `luaL_sandbox` on the global state;
5. call `luaL_sandboxthread` for each script execution environment.

### 9.5 Memory cap

Use a custom Luau allocator and track total VM allocation bytes.

Default:

```text
MaxVmMemoryMiB = 64
```

Configurable range:

```text
16 MiB .. 256 MiB
```

A request above the cap must fail allocation and surface a controlled script/runtime error. It must not intentionally kill the Rust process.

### 9.6 Execution watchdog

Install Luau's VM interrupt callback.

Every resume gets a monotonic deadline. The interrupt callback checks the deadline at Luau safepoints and terminates the execution with a controlled timeout error once exceeded.

Defaults:

```text
MaxCallbackMilliseconds = 3
MaxDrainMillisecondsPerFrame = 5
```

The first version may expose these in config, but clamp them to conservative upper bounds.

Important: the watchdog cannot interrupt a long-running C#/native host function while that function itself is executing. Therefore all exposed host calls must be intentionally short and non-blocking.

---

## 10. Managed runtime lifecycle

### `Loaded()`

- load and validate CarbonLuau config;
- determine platform/RID;
- load native library;
- bind ABI;
- do not execute game scripts yet if the Rust server is not initialized.

### `OnServerInitialized()`

- create VM generation;
- install bootstrap API;
- discover configured root scripts;
- compile all scripts;
- if compile succeeds, start runtime;
- emit `ServerStarted` event;
- expose already-online players through `Players:GetPlayers()`.

### `Unload()`

Order matters:

1. stop accepting/enqueuing new Carbon events;
2. unregister script-created commands;
3. cancel Carbon timers owned by the runtime;
4. invalidate all proxy handles;
5. dispose callback/function references;
6. destroy Luau threads;
7. destroy VM;
8. unload native library last.

Unload must be idempotent.

---

## 11. Script discovery and module loading

### Root directory

Default:

```text
carbon/data/CarbonLuau/scripts
```

Configurable only to a path **under** `carbon/data/CarbonLuau/` in v0.1.

### Root scripts

Config example:

```json
{
  "Enabled": true,
  "RootScripts": [
    "init.luau"
  ],
  "MaxVmMemoryMiB": 64,
  "MaxCallbackMilliseconds": 3,
  "MaxDrainMillisecondsPerFrame": 5,
  "LogScriptTracebacks": true
}
```

### `require`

Implement host-owned module resolution.

Allowed examples:

```lua
local util = require("modules/Util")
local kits = require("modules/Kits.luau")
```

Rules:

- only `.luau` files under the script root;
- normalize path before access;
- reject absolute paths;
- reject `..` traversal escaping the root;
- cache successful module results once per VM generation;
- detect and report module cycles;
- include module path in compile/runtime tracebacks.

Do not expose arbitrary filesystem reads.

---

## 12. Bootstrap API architecture

Do not bind dozens of independent C# methods directly into Luau for v0.1.

Expose a minimal native host primitive and build the ergonomic API in a bundled Luau bootstrap layer.

Conceptually:

```lua
__hostcall(operation: string, ...): ...
```

The bootstrap constructs:

```text
game
├── GetService(name)
├── Players
├── Commands
├── Permissions
├── Items
└── Server
```

The bootstrap source should live in `scripts/bootstrap.luau` in the repository but be generated into `Bootstrap.generated.cs` at build time so production deployment does not require an extra bootstrap file.

The generated source is internal implementation data, not server-owner editable content.

---

## 13. First-version Luau API

### 13.1 `game`

```lua
game:GetService(name: string)
```

Required services:

- `Players`
- `Commands`
- `Permissions`
- `Items`
- `Server`

Unknown service names throw a clear Luau error.

This API is intentionally Roblox-like ergonomically but is **not Roblox API compatible**.

---

## 14. Signal/event primitive

Bootstrap must provide an internal `Signal` implementation:

```lua
local connection = Signal:Connect(function(...)
end)

connection:Disconnect()
```

Required behavior:

- callbacks run independently;
- one callback error does not prevent remaining callbacks;
- disconnect during dispatch is safe;
- connection cleanup occurs on runtime teardown;
- no callback may execute after VM generation invalidation.

Do not expose a generic `CarbonHook:Connect("HookName")` API in v0.1.

---

## 15. Players service

### API

```lua
local Players = game:GetService("Players")

Players.PlayerAdded
Players.PlayerRemoving

Players:GetPlayers() -> { Player }
Players:GetPlayerByUserId(userId: string) -> Player?
```

### Player proxy

```lua
player.Name: string
player.UserId: string
player.IsConnected: boolean

player:ChatMessage(message: string)
player:HasPermission(permission: string) -> boolean
player:GiveItem(shortName: string, amount: number?) -> boolean, string?
```

### Identity rule

Expose Rust/Steam user IDs as **strings**, not Luau numbers.

SteamID64 values exceed JavaScript-style 53-bit integer precision and should not be routed through floating-point APIs.

### Lifetime rule

A Luau `Player` proxy must not hold a raw `BasePlayer*`/managed object reference.

Store a stable user ID. Every host call resolves the current live player from Carbon/Rust state. After disconnect:

```lua
player.IsConnected == false
```

Mutating methods return failure or throw a controlled API error.

---

## 16. Permissions service

```lua
local Permissions = game:GetService("Permissions")

Permissions:Register("myplugin.use")
Permissions:Has(player, "myplugin.use") -> boolean
```

Rules:

- validate permission name length/format;
- register through Carbon/Oxide-compatible permission system;
- repeated registration is idempotent;
- permission checking must use the host system, not a Luau-side cache.

---

## 17. Commands service

### API

```lua
local Commands = game:GetService("Commands")

local handle = Commands:Register("healme", {
    permission = "myplugin.heal",
    authLevel = 0,
    allowChat = true,
    allowConsole = true,
    cooldownMs = 1000,
}, function(ctx, args)
    -- ctx.Player is nil for server/RCON contexts where applicable
end)

handle:Unregister()
```

### Host integration

Prefer Carbon/Oxide-compatible public command registration APIs where available.

Do not couple the first version directly to undocumented internal command manager types unless no stable public API exists. If an internal Carbon API is required, isolate it behind `ICommandRegistrar` so it can be replaced without touching the Luau API.

### Validation

Reject:

- empty names;
- names above configured length;
- whitespace/control characters;
- command conflicts with existing runtime-owned commands;
- protected Carbon/admin namespaces if appropriate.

Script command callbacks should be queued through the Luau scheduler, not executed reentrantly inside Carbon's command dispatch stack.

---

## 18. Items service

Keep v0.1 deliberately small.

```lua
local Items = game:GetService("Items")

Items:Exists(shortName: string) -> boolean
```

Player convenience:

```lua
player:GiveItem(shortName: string, amount: number?) -> boolean, string?
```

Validation:

- resolve item by Rust short name;
- reject unknown item names;
- amount must be an integer;
- default amount = 1;
- clamp amount to a conservative configurable maximum;
- return a useful failure message instead of throwing host exceptions through the ABI.

Do not expose raw `Item` or `ItemContainer` objects yet.

---

## 19. Server service

```lua
local Server = game:GetService("Server")

Server:Log(message: string)
Server:Warn(message: string)
Server:Broadcast(message: string)
Server:GetPlayerCount() -> number
```

`print`, `warn`, and unhandled script errors should be tagged with script/module identity in Carbon logs.

Example:

```text
[CarbonLuau][init.luau] player connected: ExampleUser
```

Do not expose arbitrary server console execution in v0.1.

---

## 20. Task/scheduler API

### Required in v0.1

```lua
task.defer(callback, ...)
task.delay(seconds, callback, ...)
task.spawn(callback, ...)
```

Semantics:

- `defer`: next Carbon frame;
- `delay`: Carbon timer, then scheduler queue;
- `spawn`: scheduler queue as soon as possible, never parallel OS-thread execution.

### Deferred to v0.2 unless trivial

```lua
task.wait(seconds?)
```

`task.wait` requires robust yielded-coroutine ownership/resumption. Do not fake it with blocking sleeps. If implemented, it must use `lua_yield`/`lua_resume` and Carbon timers.

---

## 21. Carbon hook bridge

Subscribe only to the hooks needed by the documented API.

Minimum:

```text
OnServerInitialized
OnPlayerConnected
OnUserDisconnected / appropriate disconnect hook
```

Optional if needed for a richer example:

```text
OnPlayerRespawned
```

### Dispatch rule

Carbon hook callbacks should perform only cheap capture/validation, then enqueue a Luau event for execution through the scheduler.

Do not execute arbitrary Luau directly reentrantly from a deep Rust/Carbon hook stack.

Use `NextFrame`/`NextTick` to drain queued work; Carbon documents those as equivalent and provides them specifically for next-frame execution.

---

## 22. Scheduler

### Requirements

The scheduler owns:

- pending Carbon events;
- command callbacks;
- deferred tasks;
- delayed tasks after their Carbon timer fires;
- callback runtime budgeting;
- VM-generation validation.

### Drain algorithm

Pseudo-code:

```text
On enqueue:
    queue item
    if no drain scheduled:
        schedule NextFrame(Drain)

Drain:
    drainScheduled = false
    deadline = now + MaxDrainMillisecondsPerFrame

    while queue not empty and now < deadline:
        item = dequeue
        run item with MaxCallbackMilliseconds deadline
        isolate/log failure

    if queue not empty:
        schedule NextFrame(Drain)
```

This protects the Rust tick from a burst of Luau work.

### No parallel VM access

All Luau VM interaction must occur on the Rust/Carbon main thread in v0.1.

No worker thread may enter the VM.

---

## 23. Reload behavior

Required admin commands:

```text
carbonluau.status
carbonluau.reload
carbonluau.memory
```

Use Carbon command attributes for these fixed host commands and require owner/admin authorization.

### Atomic reload

`carbonluau.reload` should:

1. create a **new** VM generation;
2. install bootstrap;
3. compile/load every configured root script;
4. if any script fails, destroy the new VM and keep the old generation running;
5. if all succeed, stop intake to old generation;
6. unregister old runtime commands/timers;
7. swap to new generation;
8. destroy the old VM.

No state preservation is required in v0.1.

This gives server owners safe edit/test cycles without taking the server down because of a syntax error.

---

## 24. Diagnostics

Every script failure should include:

- root script/module name;
- callback/event type;
- Luau error text;
- Luau traceback where available;
- whether failure was compile/runtime/memory/timeout;
- VM generation ID.

Example:

```text
[CarbonLuau][gen=8][init.luau][PlayerAdded]
Runtime error: attempt to index nil with 'Foo'
init.luau:17 function onPlayerAdded
bootstrap:...
```

Rate-limit repeated identical errors to avoid log flooding.

Suggested policy:

```text
same script + callback + error hash:
    log first 5 in 60 seconds
    then emit one suppression message
```

---

## 25. Configuration

Suggested initial config:

```json
{
  "Enabled": true,
  "RootScripts": [
    "init.luau"
  ],
  "MaxVmMemoryMiB": 64,
  "MaxCallbackMilliseconds": 3,
  "MaxDrainMillisecondsPerFrame": 5,
  "MaxQueuedCallbacks": 4096,
  "MaxGiveItemAmount": 10000,
  "LogScriptTracebacks": true,
  "EnablePlayerService": true,
  "EnableCommandsService": true,
  "EnableItemsService": true
}
```

Validate config aggressively and fail to safe defaults.

If queue length reaches `MaxQueuedCallbacks`, reject/drop new non-critical script work with a throttled warning rather than allowing unbounded memory growth.

---

## 26. Security invariants

These are hard requirements.

### S1 — no raw host object exposure

Luau never obtains managed pointers, `IntPtr`s, GCHandles, Unity objects, or Carbon/Rust objects.

### S2 — no arbitrary host reflection

No `System.Type`, reflection, dynamic assembly loading, or generic property access API is exposed.

### S3 — no arbitrary file/network/process access

Scripts can only load `.luau` modules through the controlled resolver under the script root.

### S4 — bounded VM memory

Luau allocation is capped by the host allocator.

### S5 — bounded execution

Every callback/resume has an execution deadline enforced by Luau's interrupt callback.

### S6 — bounded host work

Host APIs called from Luau must not perform unbounded loops, blocking file I/O, synchronous network I/O, or sleeps.

### S7 — main-thread game access

Rust/Unity game state is touched only on the Carbon server main thread in v0.1.

### S8 — stale proxies fail local

A disconnected player proxy cannot mutate a newly connected player or another entity due to stale object references.

### S9 — module resolution stays inside root

Canonicalized `require` paths cannot escape the configured script root.

### S10 — reload cannot partially replace runtime

A failed compile/load leaves the currently running generation untouched.

---

## 27. Failure behavior

| Failure | Required behavior |
|---|---|
| Native library missing | plugin logs exact expected path and disables itself cleanly |
| Unsupported architecture | plugin logs supported RIDs and disables itself |
| Luau compile error | current runtime remains intact during reload |
| Callback runtime error | log callback error; continue other callbacks |
| Infinite loop | interrupt at deadline; server remains responsive |
| VM memory limit | fail script allocation/callback; do not intentionally terminate server |
| Queue overflow | drop/reject bounded work and warn with rate limiting |
| Unknown item | return false + message |
| Disconnected player mutation | return false/API error |
| Command collision | registration fails locally; existing command remains |
| Plugin unload during pending timer | callback is canceled/invalidated by generation token |

---

## 28. Performance requirements

Use Carbon's built-in plugin/hook profiler for integration measurements.

MVP targets on an otherwise idle server:

```text
Idle CarbonLuau tick overhead:
    effectively zero when queue is empty; no per-frame VM work

No-op event dispatch:
    p95 < 0.25 ms per callback on normal server hardware

Scheduler drain:
    must obey MaxDrainMillisecondsPerFrame

Memory:
    stable after repeated connect/disconnect and 100 reload cycles
```

Do not optimize microbenchmarks at the expense of correctness in v0.1. Primary acceptance is bounded behavior and no server instability.

---

## 29. Tests Codex must implement

### Native unit tests

1. create/destroy VM repeatedly;
2. compile and execute `return 1 + 2`;
3. syntax error returns `CL_COMPILE_ERROR`;
4. runtime error returns `CL_RUNTIME_ERROR` with traceback;
5. `while true do end` is interrupted;
6. allocation bomb reaches configured memory cap;
7. sandbox cannot access prohibited libraries;
8. host callback can exchange supported primitive values;
9. destroy after timeout/error is clean;
10. 1,000 VM create/destroy cycles under ASan/UBSan on Linux where practical.

### Managed unit/integration tests

1. config validation/clamping;
2. platform/RID native-path resolution;
3. module path traversal rejection;
4. command-name validation;
5. generation-token invalidation;
6. player proxy after disconnect;
7. queue overflow behavior;
8. reload with one syntax-error script preserves old runtime;
9. reload success removes old commands and installs new commands exactly once;
10. unload with outstanding delayed tasks is safe.

### Live Carbon acceptance fixture

Use these scripts:

#### `join-message.luau`

```lua
local Players = game:GetService("Players")

Players.PlayerAdded:Connect(function(player)
    player:ChatMessage("Luau bridge OK")
end)
```

#### `command.luau`

```lua
local Commands = game:GetService("Commands")

Commands:Register("luautest", {}, function(ctx)
    if ctx.Player then
        ctx.Player:ChatMessage("Luau command OK")
    end
end)
```

#### `timeout.luau`

```lua
local Commands = game:GetService("Commands")

Commands:Register("luauhang", {}, function()
    while true do end
end)
```

Acceptance:

- `/luautest` works;
- `/luauhang` times out and logs an error;
- Rust server continues ticking/responding;
- a subsequent `/luautest` still works.

---

## 30. Build and CI

### Native CI

Build/test:

- Windows x64, MSVC;
- Linux x64, Clang or GCC;
- Linux ASan/UBSan native test job.

Pin Luau to an explicit commit/tag. Do not track `master` implicitly in production builds.

### Managed CI

Compile against Carbon development references where licensing/distribution allows. If Carbon/Rust assemblies cannot be redistributed in CI artifacts, document how CI retrieves or supplies them.

### Packaging

`package.ps1` should produce:

```text
dist/
├── CarbonLuau.cszip
├── native/
│   ├── linux-x64/libcarbonluau_native.so
│   └── win-x64/carbonluau_native.dll
└── examples/
```

The `.cszip` must contain only the C# partial source files required by Carbon.

---

## 31. Suggested implementation phases

### Phase 0 — native-load proof

- Carbon plugin loads native probe library on Windows/Linux.
- Stop if target host blocks native libraries.

### Phase 1 — Luau execution core

- VM create/destroy;
- compile/run source;
- logging;
- sandbox;
- memory cap;
- timeout interrupt;
- `carbonluau.status` and `carbonluau.reload`.

No player APIs yet.

### Phase 2 — bootstrap and event bridge

- bundled bootstrap;
- `game:GetService`;
- `Signal`;
- scheduler queue;
- player connect/disconnect events;
- player proxy and chat message.

### Phase 3 — commands and permissions

- dynamic script-defined commands;
- permission registration/checks;
- command cleanup on reload;
- command collision handling.

### Phase 4 — item conveniences and delayed tasks

- `player:GiveItem`;
- `Items:Exists`;
- `task.defer` / `task.spawn` / `task.delay`.

### Phase 5 — hardening and live-host validation

- 100 reload soak;
- connect/disconnect soak;
- memory pressure fixture;
- timeout fixture;
- Carbon profiler validation;
- actual managed-host deployment.

Stop v0.1 here.

---

## 32. Explicitly deferred follow-up roadmap

After v0.1 is stable, consider:

- `task.wait` with resumable scheduler;
- local JSON/DataStore service;
- typed entities and entity events;
- inventory/container proxies;
- damage/combat hooks;
- CUI abstractions;
- HTTP service with allowlists and async completion;
- per-script capabilities;
- script manifests;
- richer command argument parsing;
- hot reload with optional state handoff;
- generated Luau type definitions for editor tooling;
- `luau-analyze` integration;
- source maps/diagnostic line mapping;
- debugger protocol;
- generic hook adapter generated from a curated schema;
- optional module/plugin ecosystem.

Do not expose all ~Carbon/Rust hooks generically until object lifetime, marshalling, return-value overrides, and hook-specific semantics are designed explicitly.

---

## 33. Codex implementation rules

1. **Research the installed/current Carbon API before choosing an internal command registration API.** Prefer documented/public APIs.
2. Do not make source edits to Carbon itself.
3. Do not patch Rust methods with Harmony for v0.1.
4. Keep all unsafe/native code in the native bridge or one narrowly scoped managed native-loader file.
5. No raw native pointers may escape into script-visible state.
6. No script callback may run without an execution deadline.
7. No VM may be created without a memory cap.
8. All VM interaction occurs on the Carbon/Rust main thread.
9. Every resource registered by scripts must be owned by a VM generation and automatically cleaned up on reload/unload.
10. Prefer fail-local behavior to server-wide failure.
11. Add tests before broadening the API.
12. Do not implement deferred features merely because they are easy to expose.

---

## 34. Definition of done for v0.1

The first version is complete when all of the following are true:

- Carbon loads the plugin from `.cszip`;
- Windows x64 and Linux x64 native Luau libraries load by absolute path;
- server owner can place `init.luau` under the configured script directory;
- Luau source compiles and executes;
- sandbox blocks prohibited host access;
- memory cap works;
- infinite loops are interrupted;
- player connect/disconnect signals work;
- player chat messaging works;
- script-defined commands work;
- Carbon permissions can gate those commands;
- simple Rust item grants work;
- task defer/delay/spawn work;
- script errors are isolated with useful tracebacks;
- atomic reload works;
- unload/reload does not leave duplicate commands, timers, callbacks, or native handles;
- repeated reload/connect/disconnect testing does not produce unbounded memory growth;
- Carbon profiler shows negligible idle overhead and bounded dispatch cost;
- deployment succeeds on the intended hosted Rust server.

---

## 35. Reference material

Official/current references used for this design:

1. Carbon — ZIP Scripts & Packages  
   https://carbonmod.gg/devs/features/zip-script-packages

2. Carbon — Creating Your First Plugin  
   https://carbonmod.gg/devs/creating-your-first-plugin

3. Carbon — Creating Your Project (.NET Framework 4.8 / Carbon.targets)  
   https://carbonmod.gg/devs/creating-your-project

4. Carbon — Commands  
   https://carbonmod.gg/devs/features/commands

5. Carbon — Timers / NextFrame / NextTick  
   https://carbonmod.gg/devs/features/timers

6. Carbon — Hooks Reference  
   https://carbonmod.gg/references/hooks/

7. Carbon — Profiler  
   https://carbonmod.gg/devs/features/mono-profiler

8. Carbon — Extensions  
   https://carbonmod.gg/devs/features/extensions

9. Luau — C API  
   https://luau.org/api/

10. Luau — Sandboxed VM embedding  
    https://luau.org/sandbox/

11. Luau — GitHub / embedding/build notes  
    https://github.com/luau-lang/luau

12. Microsoft — .NET Standard implementation support  
    https://learn.microsoft.com/en-us/dotnet/standard/whats-new/whats-new-in-dotnet-standard

13. Shockbyte — Carbon installation on Rust  
    https://shockbyte.com/help/knowledgebase/articles/how-to-install-carbon-on-your-rust-server

---

## 36. Architectural summary

The first version should be deliberately boring at the trust boundary:

```text
Carbon/Rust objects
      │
      │ validated, short host operations
      ▼
C# facade
      │
      │ small project-owned C ABI
      ▼
Luau VM
      │
      │ sandboxed bootstrap/proxies
      ▼
server .luau scripts
```

Do not expose the Carbon object model and hope Luau behaves. Build a small, stable server scripting platform on top of Carbon and expand it only after lifecycle, resource bounds, reload, and object validity are proven.
