# Installation

CarbonLuau is an experimental server-side Luau runtime for Carbon-modded Rust
servers. Use the release bundle matching the server process: Windows x64 or
glibc Linux x64. ARM, x86, macOS and non-glibc Linux are not qualified.

## Requirements

- A Carbon-modded Rust server.
- An x64 server process.
- A matching `win-x64` or `linux-x64` CarbonLuau release bundle.
- File and console/RCON access sufficient to install a Carbon plugin and run
  administrator commands.

Current Player/GUI host qualification used Carbon `2.0.259` and Rust Steam build
`25353106`; the earlier loader baseline used Rust `2633` / build `25230300`.
Other versions are not automatically supported; review the
[compatibility policy](https://gmoddev.github.io/CarbonLuau/#/Compatibility)
before deploying.

## Production layout

For the separate developer extension, install the matching platform VSIX using
VS Code's **Extensions: Install from VSIX**. See the
[official tooling setup](https://github.com/gmoddev/carbonluau-vscode#install).
The extension contains its tooling pack; no Carbon installation or source checkout
is required. Windows/Linux x64 support trusted language analysis and GUI preview;
macOS arm64 is static-only. Restricted Mode retains static project diagnostics.

Install only the native runtime, compiler worker and private storage worker matching the server platform:

```text
carbon/
|-- plugins/
|   `-- CarbonLuau.cszip
`-- data/
    `-- CarbonLuau/
        |-- native/
        |   `-- win-x64/
        |       |-- carbonluau_native.dll
        |       |-- carbonluau_compiler.exe
        |       `-- carbonluau_storage.exe
        `-- scripts/
            |-- init.luau
            `-- modules/
```

For Linux, replace the `win-x64` subtree with:

```text
native/
`-- linux-x64/
    |-- libcarbonluau_native.so
    |-- carbonluau_compiler
    `-- carbonluau_storage
```

Do not mix Windows and Linux files, and never install files from a test fixture
package. The plugin loads one normalized, platform-specific runtime path;
that runtime launches only its sibling compiler worker. A separate managed
supervisor launches the private storage worker off the game thread. The development
API `0.5.0-experimental` adds [DataStoreService](api/Services/DataStoreService.md);
historical 0.4 artifacts do not include it. See the [combined qualification](PersistenceFoundation1C.md)
for the actual platform envelope. Storage failure does not disable scripting.

The private worker owns `carbon/data/CarbonLuau/persistence/store.sqlite3` and
its retained rollback journal. Use the qualified local NTFS/ext4 storage profile;
network shares, cloud-synchronized folders and unqualified filesystem features
are unsupported. Do not open or modify the database with other tools while the
worker is running, enable WAL, or delete/zero the journal. Unsupported WAL formats
are rejected; CarbonLuau does not silently convert them. Preserve failed storage
for offline operator recovery rather than deleting it to make startup succeed.

Logical quotas (16 MiB/namespace, 256 MiB/global) and SQLite page/file-length
limits are hard bounds. The 1,280 MiB allocated-file figure is an operational
budget/qualification target, **not** a strict physical allocation guarantee.
See [Persistence-1A evidence and limits](https://github.com/gmoddev/CarbonLuau/blob/main/docs/PersistenceFoundation1A.md).

## Install and start

1. Stop the Rust server or follow your normal safe Carbon plugin-maintenance
   procedure.
2. Copy `CarbonLuau.cszip` to `carbon/plugins/CarbonLuau.cszip`.
3. Copy the matching native runtime and both workers to the exact platform path above. On Linux, make the extracted workers executable:

   ```bash
   chmod 0755 carbon/data/CarbonLuau/native/linux-x64/carbonluau_compiler
   chmod 0755 carbon/data/CarbonLuau/native/linux-x64/carbonluau_storage
   ```
4. Create `carbon/data/CarbonLuau/scripts/modules`, even if it is initially empty.
5. Create `carbon/data/CarbonLuau/scripts/init.luau` or copy one of the bundled
   examples deliberately. Do not overwrite existing scripts without a backup.
6. Start the server. CarbonLuau creates its default configuration and loads the
   entry script during server initialization.
7. Run `carbonluau.status` from the server console or authenticated RCON. A ready
   runtime reports its generation, API identity, native ABI, Luau revision and
   bounded-resource counters.

After editing scripts, run `carbonluau.reload`. A successful candidate atomically
replaces the operator-root domain in the healthy shared VM. A compile or
initialization failure preserves the previous working root.

## Addon packages

The v0.4.0 candidate accepts schema-1 addon snapshots from a loaded Carbon
provider plugin through `CarbonLuau.Addons` protocol 1.2. CarbonLuau does not scan
a directory for addon archives, so copying a `.claddon` file into the server is
not enough. Install and configure the provider that owns those packages, then use
`carbonluau.status` and the provider's own diagnostics to confirm activation.

Package authors can start from the bundled `examples/addons/economy` and
`examples/addons/shop` layouts. See [addon composition](api/Addons.md) and the
[provider protocol](api/Addon-Providers.md). CarbonLuau unload invalidates every
provider token; a provider that remains loaded must register again after reload.

## GUI scripts

The v0.4.0 candidate includes the experimental server-driven GUI surface. A
minimal entrypoint can create one retained tree and show it to current and future
Players:

```lua
local Gui = game:GetService("Gui")
local Players = game:GetService("Players")

local Screen = Gui:Create("ScreenGui")
local Label = Screen:Create("TextLabel")
Label.Text = "Hello from CarbonLuau"

for _, Player in Players:GetPlayers() do
    Screen:Show(Player)
end

Players.PlayerAdded:Connect(function(Player)
    Screen:Show(Player)
end)
```

The release bundle includes hello, shared-tree, per-player Clone, Activated,
deterministic list/grid layout, padding, typed image, retained scrolling,
clipping, font, scroll-effect and combined examples under `examples/gui`, plus
owner/consumer addon examples under `examples/addons`. See
the [GUI guide](api/Gui.md) before deployment. `TextBox` is not implemented.
Show/Hide represent desired server state; authenticated-client visual
correctness, image loading, clipping, scrolling, actual click receipt and
reconciliation remain unqualified.

## Configuration

Carbon writes the plugin configuration in its normal configuration directory.
The defaults are:

```json
{
  "Enabled": true,
  "MaxVmMemoryMiB": 64,
  "MaxCallbackMilliseconds": 3,
  "ScriptRoot": "scripts",
  "EntryScript": "init.luau",
  "ModuleRoot": "modules",
  "FrameDrainBudgetMilliseconds": 5,
  "MaxQueuedCallbacks": 4096
}
```

Memory is clamped to 16–256 MiB per VM, callback deadlines to 1–100 ms, frame
drain budget to 1–20 ms and queued callbacks to 1–4096. Paths are confined under
`carbon/data/CarbonLuau`; absolute paths, traversal and reparse/symlink escapes are
rejected.

## Diagnostics

- `carbonluau.status`: current availability, generation, identities and bounded
  scheduler/addon/GUI counters.
- `carbonluau.reload`: compile and initialize a new candidate generation.
- Carbon server log: messages beginning with `[CarbonLuau:Config]`,
  `[CarbonLuau:Native]`, `[CarbonLuau:Runtime]` or `[CarbonLuau:Scheduler]`.
- `native library missing`: verify the exact platform directory and filename.
- `compiler worker could not start`: verify the matching compiler worker is beside the native runtime and executable on Linux.
- `ABI/platform/library`: verify that the archive matches the server OS/x64
  process and was not mixed with files from another version.
- `COMPILE_ERROR` or `RUNTIME_ERROR`: inspect the logged chunk/error and fix the
  Luau source; a failed candidate does not replace the active generation.
- `TIMEOUT` or `MEMORY_LIMIT`: the configured safety boundary stopped execution;
  inspect status before reloading rather than raising limits blindly.
- `Blocked` addon: one or more required dependency IDs are not active; inspect
  the provider status response and dependency registration order.

Continue with the [quick start](https://gmoddev.github.io/CarbonLuau/#/README?id=quick-start),
[public API](https://gmoddev.github.io/CarbonLuau/#/api/README) and
[known compatibility limits](https://gmoddev.github.io/CarbonLuau/#/api/Compatibility).
