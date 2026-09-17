# Addon provider protocol

Availability: CarbonLuau package `0.4.0`, provider protocol
`CarbonLuau.Addons` / `1.2`, package schema `1`.

A loaded Carbon plugin registers immutable addon bytes through Carbon's plugin
call mechanism. The concrete provider plugin object owns every token it receives.
Tokens are not authentication credentials and are never exposed to Luau.

| Hook | Arguments after the provider object | Purpose |
|---|---|---|
| `CarbonLuau_AddonProtocol` | none; no provider required | Returns protocol name, version and capabilities |
| `CarbonLuau_RegisterAddonArchive` | archive bytes | Copies, validates and reserves a schema-1 package |
| `CarbonLuau_RegisterAddonSource` | ID, version, UTF-8 source bytes | Registers one `init.luau` package |
| `CarbonLuau_GetAddonStatus` | token | Reads an owned registration |
| `CarbonLuau_ReplaceAddonArchive` | token, archive bytes | Stages a same-ID replacement |
| `CarbonLuau_ReplaceAddonSource` | token, version, source bytes | Stages a single-source replacement |
| `CarbonLuau_UnregisterAddon` | token | Retires an owned registration |

Mutation and status calls return a nine-string array:

```text
result, token, state, id, version, diagnostic, snapshotSha256,
domainLifetimeId, pendingVersion
```

`result` is `OK` or `ERROR`. Registration and replacement are asynchronous;
providers poll status until `Active`, `Blocked` or `Failed`. The state names are
`Registered`, `Blocked`, `Initializing`, `Active`, `Failed` and `Stopping`.
Diagnostics are bounded and safe to log, but should not be parsed as stable IDs.

Provider unload retires all registrations owned by that exact plugin instance.
CarbonLuau unload invalidates all tokens. If the provider remains loaded when
CarbonLuau returns, it must query the protocol and explicitly register fresh
snapshots; an old token always fails. Required dependency loss retires and blocks
affected consumers. Optional consumers stay active and do not silently rebind.

Protocol 1.2 transports packages and lifecycle only. It does not let providers
inject arbitrary C# objects or capabilities into Luau. There is no persistent
registration across CarbonLuau reload, remote registry, package download or
version solver.
