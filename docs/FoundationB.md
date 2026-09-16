# Foundation B - experimental provider/package registration

Foundation B implements the narrow D14 package/provider lifecycle on top of the
qualified shared-VM primitives in [Foundation A](FoundationA.md). It is an
experimental managed Carbon-plugin integration, not a completed public addon
system. The public Luau scripting identity remains `CarbonLuau
0.3.0-experimental`; the provider protocol has its own identity,
`CarbonLuau.Addons` / `1.0`.

## Provider protocol

A loaded Carbon plugin obtains the CarbonLuau plugin through the supported
`[PluginReference]` mechanism and calls these named hooks, passing its concrete
plugin object as the first argument on every stateful operation:

| Hook | Arguments after provider | Purpose |
| --- | --- | --- |
| `CarbonLuau_AddonProtocol` | none; no provider required | Query protocol name, version and availability |
| `CarbonLuau_RegisterAddonArchive` | `.claddon` bytes | Copy, parse and reserve an archive registration |
| `CarbonLuau_RegisterAddonSource` | ID, version, UTF-8 source bytes | Register one `init.luau` source through the same snapshot lifecycle |
| `CarbonLuau_GetAddonStatus` | token | Read owned registration status |
| `CarbonLuau_ReplaceAddonArchive` | token, `.claddon` bytes | Prepare a same-ID replacement |
| `CarbonLuau_ReplaceAddonSource` | token, version, UTF-8 source bytes | Prepare a single-source replacement |
| `CarbonLuau_UnregisterAddon` | token | Retire and release an owned registration |

The protocol returns only strings, string arrays and byte-array inputs. It does not
expose runtime/domain objects to providers or provider objects to Luau. Stateful
responses are a nine-element string array:

```text
result, token, state, id, version, diagnostic, snapshotSha256,
domainLifetimeId, pendingVersion
```

`result` is `OK` or `ERROR`. Diagnostics are sanitized and limited to 1024
characters. Tokens combine the CarbonLuau host lifetime with a monotonic sequence;
they are usable only by the same concrete provider plugin instance. Carbon's
`OnPluginUnloaded(Plugin)` lifecycle hook retires all registrations owned by that
instance before its identity is discarded. This is lifecycle ownership, not a
hostile managed-plugin security boundary.

## Package subset

A `.claddon` is a standard ZIP archive held and read in memory. It is never mounted
or extracted. It contains exactly one `addon.json`, one `init.luau`, and optionally
additional `.luau` modules. Nested archives and all other file types are rejected.
Foundation B accepts this strict manifest subset:

```json
{
  "schema": 1,
  "id": "creator.addon",
  "version": "1.0.0",
  "dependencies": {
    "required": ["creator.required"],
    "optional": ["creator.optional"]
  }
}
```

`dependencies` is optional. Unknown or duplicate fields, duplicate dependencies,
self-dependencies and noncanonical values are rejected. IDs have one or two
lowercase ASCII segments of 1-32 characters, joined by one dot; each segment starts
and ends alphanumeric and may contain internal `_` or `-`. The total is at most 65
characters. `carbonluau` and `carbonluau.*` are reserved. Versions are canonical
numeric `MAJOR.MINOR.PATCH` values.

Archive names use `/`, are relative, contain no empty, dot or traversal segments,
and are at most 127 characters and 32 segments. Duplicate normalized paths,
directories, links/special entries, encryption, unsupported compression, ZIP64,
multi-disk ZIP, malformed UTF-8/JSON and nested archives are rejected. Normal ZIP
handling remains `System.IO.Compression`; an explicit bounded header preflight is
used because that API does not expose encryption/compression flags needed for the
required rejection policy.

Accepted input bytes are copied before registration returns, decoded into a private
immutable source snapshot, and identified by SHA-256. Single-source registration is
normalized to the same representation with an implicit `init.luau`, schema 1 and no
dependencies.

## Foundation B qualification limits

These are conservative Foundation B values, not promises for later package policy:

| Resource | Limit |
| --- | ---: |
| Input archive | 4 MiB |
| Cumulative expanded archive data | 8 MiB |
| Manifest or one source | 64 KiB |
| Aggregate source per package | 4 MiB |
| Modules besides `init.luau` | 256 |
| ZIP entries | 512 |
| Dependencies | 32 |
| Live registrations / one provider | 128 / 32 |
| Aggregate live and pending snapshot source | 32 MiB |
| Retained idempotence tombstones | 1024 |

## Lifecycle and publication

Registration reserves its stable ID immediately. Dependency-free registrations move
from `Registered` to `Initializing`, then `Active` or `Failed`. A manifest declaring
any dependency is accepted as `Blocked` with an explicit diagnostic; Foundation B
does not execute it or infer partial dependency semantics.

Activation creates a Foundation A domain in the current shared VM. Entrypoint,
module-cache, task, command and listener publication uses the existing candidate
transaction. Compile/init failure publishes nothing. Caught ordinary module failure
retains the qualified retry/no-leak behavior.

Same-provider replacement retains the token and ID reservation. The old domain and
resources remain active while the candidate initializes. Success atomically
publishes the new domain and retires the old one; ordinary candidate failure drops
the candidate and preserves the old active domain. Dependency-bearing replacement is
rejected until dependency resolution exists.

Unregister transitions through `Stopping`, tears down the domain/resources, then
releases the ID. Repeating unregister with the same provider/token is idempotent
while its bounded tombstone remains; foreign and stale tokens fail. Provider unload
does the same for every owned registration in a deterministic owner-thread pass.
`Initializing` is synchronous and therefore not externally observable or
unload-interruptible on the owner thread.

## Qualification evidence

Foundation B was qualified on 2026-09-16 from baseline
`333e8c9010aea47c83915ac5c82e46dbf40321d5`:

- Windows 11/MSVC 19.44: all four native CTest fixtures, import policy, managed
  loader tests, real native/managed runtime tests, package checks and API checks
  passed.
- Ubuntu 24.04/GCC 13.3: all four native fixtures, managed loader/runtime tests and
  exported-probe check passed.
- Ubuntu ASan/UBSan with leak detection: all four native fixtures passed.
- The addon fixture covered strict archive/manifest/path rejection, immutable
  archive and source copies, IDs/ownership/stale tokens, `Registered`, `Blocked`,
  `Active` and `Failed`, compile/runtime/module failure rollback, atomic successful
  and failed replacement, provider-wide teardown, 100 register/activate/unregister
  cycles, 100 replacements and return to one root domain/zero VMs at teardown.
- Live Carbon 2.0.259.0 on an isolated Windows Rust server compiled the production
  package using Carbon's supported assembly-reference directive. A separate provider
  reached `Active`; real provider unload retired its domain, long-delay task,
  command and ID reservation; reloading that provider reclaimed the same stable ID
  and reached `Active` again.

The live check also established why the package source carries
`// Reference: System.IO.Compression`: Carbon's plugin compilation set does not
reference that forwarded framework assembly by default. The directive is handled by
Carbon's supported script loader and avoids reflection or an environment-specific
ZIP implementation.

## Deliberately deferred

Foundation B does not implement `require("@addon")`, dependency graphs or binding,
public module exports, `addon:IsDependencyAvailable`, root-to-addon imports,
provider C# capabilities, restricted exposure, multiple versions/instances,
downloads/registries, version solving, or any Foundation C work. It assigns no new
addon-capable Luau scripting API version.
