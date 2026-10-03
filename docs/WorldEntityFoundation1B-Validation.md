# Entity-1B — exact keyed read-only Workspace/Entity runtime

**Verdict: runtime PASS on the pinned Windows/Linux host targets; Entity-1C
combined/public closure remains open.** Starting source was
`49dc0b4f348f6cda1737f5f562c2938a62423c3f` (`origin/main`). This phase
does not assign a scripting API/release identity or generate public definitions.
It consumes, without weakening, the [Entity-1A qualified lifetime substrate](WorldEntityFoundation1A-Validation.md).

## Implemented boundary

The only new Luau runtime surface is `game:GetService("Workspace")`,
`Workspace:GetEntityById(Id: string) -> Entity?`, and read-only `Entity.Id`,
`Entity.Prefab`, `Entity.Position`. Equality compares the private CarbonLuau host
identity plus lifetime token, not wrapper, domain or network ID, and requires no
host call. No `IsValid`, enumeration, query, lifecycle Signal, Spawn, Destroy,
position write, host object or reflection path was added. No API metadata or
author-facing release documentation was generated in 1B.

`Workspace` is domain-bound and may be acquired before Entity baseline readiness.
Lookup then fails controlledly until the 1A observer has a qualified continuous
startup baseline; it never reports an unqualified world as empty. Input must be
canonical decimal UInt64 text in the range 1..18446744073709551615, at most 20
ASCII digits, without leading zero. Valid absent or non-`BaseEntity` occupancy
returns `nil`. The adapter performs one `serverEntities.Find(NetworkableId)`;
the exact-target structural checker follows `Find` through `ListDictionary`
and `Dictionary.TryGetValue` on both platform assemblies and fails closed on
drift. It neither iterates the registry nor builds a world index.

Admission and every property read use the 1A exact weak-object, completed
SpawnEpoch, host-local EntityLifetimeToken, captured ID/prefab, keyed-occupant
and owner-thread predicate. Proxies retain only opaque CarbonLuau-owned strings;
the managed token directory holds weak records and incrementally sweeps dead
entries. Per-session publication witnesses use D7/D10 commit/rollback and
retirement; a failed publication cannot revive an escaped proxy. Stale property
access is a controlled error even for captured `Id`/`Prefab`, while equality
remains available without host access. An old token never rebinds to an ID reuse,
new Spawn epoch, replacement domain or reconstructed VM.

`Prefab` is the full host `PrefabName`, bounded at 512 UTF-8 bytes with no NUL;
it is not a display name or short prefab. `Position` is a freshly read root
`Transform.position` converted from finite Unity single coordinates to D18's
immutable `Vector3`, with no axis/unit remap or local-position fallback.
Malformed/over-bound host state and nonfinite coordinates fail controlledly.
Lookup and reads do not call Spawn, Kill, network publication, registry or
Transform mutators. The only changes are bounded private facade bookkeeping.

## Exact target and bounded host gate

The qualified envelope is Rust build `25653776`, protocol `2634.289.1`,
Linux Carbon `2.0.261.0` / `c74c4ca`, Windows Carbon `2.0.262.0` /
`8a81d70`, and Harmony `2.4.2.0`; exact assembly/hook hashes are in the
[1A record](WorldEntityFoundation1A-Validation.md). The added Entity-1B
checker pins Windows/Linux `Facepunch.System.dll` keyed-lookup IL and the
shipped Harmony shared patch-record replacement semantics. The existing full
24-method Spawn topology is checked at startup. On each public operation a
constant-size snapshot check of the relevant Harmony patch-record identities
detects any post-start patch/unpatch and fails closed. The pinned Harmony
`UpdatePatchInfo` replaces the serialized `byte[]` under the same dictionary
lock; the checker verifies that source/IL contract. A startup-only bounded
read-path warmup happens outside Luau's 3 ms deadline; no script-visible proxy
or event is published by it. In the live qualification the first public lookup
remained below that deadline on both hosts.

## Qualification evidence

- Exact-target extended structural checker: 93 additional Entity-read checks,
  including both `Find` paths and Harmony patch-record replacement; existing
  1A structural gate retained.
- Lifetime model: 2,173 checks including cross-domain same-token identity,
  same-object/new-epoch ABA, ID reuse, sticky retirement, token binding,
  weak-reference churn and host retirement.
- Managed publication witness and real pinned compiler/native VM tests passed
  on Windows and Linux. They cover service acquisition, cold-module lookup,
  canonical/malformed IDs, unknown ID, property values, equality without host
  reads (including across two addon domains through a public module), stale
  properties and replacement authority.
- Full Windows managed/runtime regression passed, including persistence,
  addons/providers, GUI, Player and foundation scale tests. Architecture/API
  checks passed. Native targeted runtime/facade tests passed on both platforms;
  Linux ASan/UBSan passed all 23 native CTest cases. The production package
  audit found 65 expected C# sources and no test/live fixture.
- Disposable Windows Carbon log
  `D:\Sandbox\Codex\Entity1AStartup\evidence\adapter-clean-20261002-212435\server.log`:
  1,744 qualified startup completions; pre-existing keyed lookup, full prefab,
  parented world-space position, invalid IDs, immutable fields, Kill/stale
  callback and 1A private lifecycle fixture passed. First public lookup wall
  observation was 0.351 ms.
- Disposable Linux Carbon log
  `/root/codex/entity1a-25653776/adapter-check/evidence/entity-private-20261002-212436/server.log`:
  1,732 qualified startup completions; the same read fixture and 1A private
  lifecycle fixture passed. First public lookup wall observation was 1.854 ms.
- A qualification-only scan of the keyed, fully spawned prefab population
  observed 1,744 Windows / 1,732 Linux entries, maximum full PrefabName length
  94 UTF-8 bytes on both. Public equality of sampled full prefab values was
  checked for available world, deployable and NPC categories. The fixture also
  spawned and parented a deployable to distinguish world from local position.
  No authenticated player or building block was present in these runs; no such
  live observation is claimed.

The production registry path performs no population scan; the above sweep is
test-only. Exact non-`BaseEntity` keyed occupancy is covered by the cast/nil
path but was not produced by the live world. Unqualified first-install/hotload,
full unload/reload continuity loss and shutdown remain fail-closed under the
qualified 1A observer; 1B's fake-host real VM test separately checks the
unready public lookup error. Broad provider/root/addon combined lifecycle and
identity-table scale closure remain Entity-1C, not falsely included here.
Both disposable servers were stopped after qualification; their original
CarbonLuau package/native/compiler files were restored from task-owned backups
and hash-checked. The three temporary Linux asset links and two copied manifest
files used to complete its disposable Rust installation were removed; the
source asset installation and evidence logs were preserved.

## Scope and handoff

No World/Entity collection discovery, spatial/prefab query, Signal,
Spawn/Destroy, `IsValid`, specialized capability, API metadata or release/tag
was added. Package/API/native ABI/provider/package-schema/Luau identities are
unchanged. The existing `0.5.0-experimental` scripting API assignment is for
Persistence, **not** Entity. Entity-1C must run the combined cross-domain,
provider, reload/recovery, startup/shutdown, resource-scale and public
metadata/docs/release-identity closure before author-facing publication.
Historical registry-continuity, failed-Spawn and early base-completion negative
evidence remains preserved in the linked 1A and investigation records.
