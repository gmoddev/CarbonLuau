# Entity-1C — combined public World/Entity Foundation 1 closure

Status: **ENTITY-1C PASS — WORLD/ENTITY FOUNDATION 1 PUBLICLY QUALIFIED**
for the exact host envelope below. Entity-1A and Entity-1B are PASS. This
record does not create a package release or tag. Starting source:
`cee1df0583ab92b139a84bff37df76a0f51c3803` on `origin/main`;
implementation/qualification source: `69049c739cce8984e375bd582968de45f4786705`.

## Qualified inputs and exact host envelope

[Entity-1A](WorldEntityFoundation1A-Validation.md) qualifies the private
startup-observed full-Spawn epoch and non-retargeting lifetime substrate;
[Entity-1B](WorldEntityFoundation1B-Validation.md) qualifies the one-keyed-lookup
read-only runtime. Their exact assembly/hook hashes, 24-method patch topology,
negative tests and support limits remain authoritative. Entity-1C does not
change the production Entity adapter or native ABI.

The tested host envelope is Rust dedicated build `25653776`, protocol
`2634.289.1`, Windows Carbon `2.0.262.0` / `8a81d70`, Linux Carbon
`2.0.261.0` / `c74c4ca`, and Harmony `2.4.2.0`. Initial plugin installation
must precede world restoration. First installation into an already-running
server, or a full CarbonLuau plugin unload/reload, leaves the observer
unqualified until server restart. Ordinary root/addon replacement and fatal
Luau VM recovery do not remove the continuously installed observer. Host drift
fails closed. No arbitrary newer Rust/Carbon build is implied.

The historical [registry-continuity](WorldEntityLifetimeInvestigation.md),
[failed-Spawn](WorldEntitySpawnEpochProbe.md), and
[early base-completion](WorldEntityCompletionFenceInvestigation.md) failures
remain negative evidence, not rewritten as successes. Naturally pooled
same-managed-object reuse and authenticated player/building-block behavior were
not observed in the disposable host fixtures.

## Public development surface and identities

Only `game:GetService("Workspace")`,
`Workspace:GetEntityById(Id: string) -> Entity?`, and read-only `Entity.Id:
string`, `Entity.Prefab: string`, `Entity.Position: Vector3` are introduced.
Entity equality uses the existing exact-lifetime userdata comparison, with no
new method. Metadata, generated `.d.luau`, public reference pages and examples
agree on this surface. The development scripting API is
`0.6.0-experimental`; published package `0.5.0` and its historical scripting
API are unchanged. Native ABI `1.5`, provider protocol `1.2`, package schema
`1`, and Luau pin `c6b830185af962c82003f86784e2fe036357c830` are
unchanged. No package `0.6.0`, release, tag or editor publication is made.

## Combined lifecycle and authority

- Real pinned compiler/VM tests on Windows and Linux exercise Workspace
  acquisition, exact string IDs, read-only fields, world-space Vector3,
  equality, cold module lookup, failed and successful root candidates, failed
  and successful addon-owner replacement, required dependent reconstruction,
  cross-domain public-module sharing, provider unload, stale old witnesses,
  fatal VM timeout/reconstruction and fresh lookup. A failed candidate does
  not retire committed authority; a successful replacement does. Another
  domain's fresh proxy continues to use its own authority. Both proxies may
  name the same underlying host token without transferring ResourceOwner.
- Existing addon/provider regression tests cover required loss/restoration,
  optional stale bindings, stable package identity and provider unload/reload;
  Entity-1C's focused test adds a real shared Entity value to the required
  dependency/replacement path. These are fake-host/VM composition tests, not
  claims that an external Rust entity was replaced live. D4 keeps the operator
  root outside the addon dependency graph: root-to-addon public-module imports
  and addon-to-root imports are not permitted paths in Foundation 1. The
  focused test confirms a root import of an addon public module is rejected;
  it does not claim nonexistent root↔addon module transfer.
- The exact-host private live fixture on both platforms exercises pre-existing
  lookup, new Spawn, sticky retirement, failed same-object retry, registry
  churn, root replacement and VM recovery. The public live fixture exercises
  keyed lookup, full Prefab, parented world-space Position, immutable fields
  and stale reads. A 1C Windows first-install hotload of the final candidate
  into an already-started disposable world logged `observer was not installed
  before world startup; server restart required` and admitted no world. Existing
  1A evidence establishes full observer-gap fail-closed behavior; the 1C real-VM
  fake host checks that an unavailable world raises a controlled error rather
  than returning `nil`.
- Host-process restart creates a new observer and lifetime space. CarbonLuau
  persists no Entity token, does not force Rust saves and does not treat a
  reused numeric ID as durable identity. The startup restoration evidence is
  the exact-host pre-existing world fixture; no old process proxy is claimed
  to be executable after process exit. Shutdown retires facade authority and
  does not walk or destroy Rust entities; there are no Entity Signals or
  host-driven Luau callbacks in Foundation 1.

## Scale, boundedness and resource observations

The deterministic lifetime model passes 9,005 checks, including 128 live
observed identities, independent cross-domain bindings sharing each token,
4,096 churned weak lifetimes, repeated validation, monotonic token identity
and incremental weak-record cleanup converging to at most one JIT-retained
test-local record. Production lookup remains one keyed `serverEntities.Find`
plus constant validation, not a world scan. A qualification-only fixture
looked up 128 distinct live entities on each host; it is not a production
index or enumeration API. The existing full regression also tests 100 addon
domains and bounded scheduler/resource behavior. Observed times below are
measurements, not public latency guarantees.

| Exact-host disposable run | Startup completed epochs | First public lookup | 128 distinct keyed lookups | Public fixture |
|---|---:|---:|---:|---|
| Windows clean-source build, `adapter-clean-20261002-231230` | 1,753 | 0.392 ms | 12.101 ms | PASS |
| Linux clean-source build, `entity-private-20261002-231020` | 1,752 | 0.892 ms | 10.551 ms | PASS |

Windows first-install hotload evidence:
`D:\Sandbox\Codex\Entity1AStartup\evidence\adapter-hotload-20261002-232909\server.log`.

The live prefab scan was test-only: maximum observed full name was 94 UTF-8
bytes, below the 512-byte design ceiling. Live world/deployable/NPC categories
were observed; no authenticated player/building block claim is made. Both
disposable servers were stopped. Their original package/native files were
restored, and the temporary Linux asset links/manifests were removed.

## Public-contract and security audit

The managed bridge parses a canonical UInt64 string, performs one keyed
registry lookup, requires exact-host patch continuity and the qualified
startup baseline, then validates exact object/epoch/ID/prefab and current
VM/domain/publication authority on each property read. Unknown valid IDs
return `nil`; malformed IDs and unqualified worlds fail controlledly.
Lookup never accepts Luau numbers for IDs, never exposes raw BaseEntity,
Unity/.NET objects or reflection, and never scans the host registry.
`Prefab` is bounded and not interpreted as filesystem authority; nonfinite
Position fails controlledly. Equality is host-read-free, but does not make a
stale proxy usable. Provisional publication rollback and replacement leave
escaped old proxies stale; no mutation authority is delegated by sharing.
I12's trusted in-process interference boundary remains unchanged.

Author-facing references are [Workspace](api/Services/Workspace.md),
[Entity](api/Types/Entity.md), [world examples](api/World-Examples.md), and
[compatibility](api/Compatibility.md). The three runnable examples cover exact
lookup, repeated-lookup equality and safe string IDs, and passed through the
pinned real compiler/VM on Windows and Linux. Tooling metadata/drift checks
and generated definitions include only production bindings; no world preview
simulation was introduced.

## Qualification and release-readiness ledger

- Windows clean-source native build, all 22 native CTest cases and full
  managed/runtime regression: PASS, including Player, GUI, persistence,
  addons/providers, publication, scheduler, recovery and 100-addon scale.
  Focused Entity real-VM test: PASS.
- Linux clean-source native build, all 23 native CTest cases, full
  managed/runtime regression and focused Entity real-VM test: PASS.
- API metadata generation, generated drift, tooling tests, architecture check,
  API navigation/link check and release-content policy check: PASS locally.
- Native code beyond the embedded API-version/bootstrap text is unchanged;
  hosted sanitizers passed against the implementation source. They validate
  the existing native substrate, not managed Entity lifetime logic.
- Windows and Linux dry-run release bundles were byte-for-byte deterministic
  across two builds per platform. Windows clean extraction contained the
  expected 65-source production package, docs and examples. Linux clean
  extraction exposed a ZIP creator-OS defect: POSIX `0755` attributes alone
  were ignored by Linux `unzip` when the entry was marked DOS-origin. The
  packaging correction now marks only the compiler/storage worker entries as
  Unix-origin; a fresh Linux extraction verified both remain executable and
  contains the expected public docs/examples. Neither bundle was published.
- Hosted [CarbonLuau validation](https://github.com/gmoddev/CarbonLuau/actions/runs/37093495173)
  passed on Windows, Linux and sanitizers against implementation revision
  `69049c739cce8984e375bd582968de45f4786705`. Hosted
  [Tooling Foundations A and B](https://github.com/gmoddev/CarbonLuau/actions/runs/37093495190)
  and [tooling baseline contracts](https://github.com/gmoddev/CarbonLuau/actions/runs/37093495246)
  also passed. Final evidence-only commit CI and served-docs deployment are
  checked separately after publication; they do not change the runtime proof.

No `IsValid`, enumeration, prefab/spatial query, Signals, Spawn, Destroy,
Position write, raw host object or other later World/Entity foundation was
implemented. Later collection/event/mutation work requires separate design
and authorization. A future package `0.6.0` release pass remains separate.
