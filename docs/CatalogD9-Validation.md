# CATALOG-D9 — Item and Prefab catalog qualification

Date: 2026-10-15. Starting `main`:
`27c46d7a5f2493014878d239f7d3a7bb6689cd07` (Gameplay D8).
Status: implementation, offline gates and exact Windows/Linux integration PASS;
final-source hosted CI pending closure.
This record does not authorize a release or tag.

The user explicitly selected development scripting API **0.7.5-experimental**.
Package `0.7.0`, immutable published `v0.7.0`, ABI `1.5`, provider `1.2`, schema
`1` and the Luau pin are unchanged. New declarations have SinceApi 0.7.5;
historical introduction versions and release identities are retained.
[D12](invariants/Runtime.md#d12-current-catalog-identity-assignment),
[runtime invariants](invariants/Runtime.md), [D19](invariants/Tooling.md),
[D20](invariants/WorldEntity.md) and [D23](invariants/Gameplay.md) remain owners
of their existing contracts. [Public reference](api/Catalogs.md).

## D9-A — finite authoritative data

Implement the attached architecture design, SHA256
`b8ad7056fe9824eaba45e0484837452dc447e5e7dd86e3e458593a99cf4788b4`.
Its original research-only scope is superseded by the user's implementation
authorization. No qualified Loot infrastructure is redesigned.

Exactly **221 flat Item fields**, **one Prefab.Loot table** and **two prefab
fields** are generated. Values are ordinary canonical strings:

```luau
assert(Item.Scrap == "scrap")
assert(Item.MetalFragments == "metal.fragments")
assert(Item.Wood == "wood")
local Loot = game:GetService("Loot")
Loot:RegisterDropRule(Prefab.Loot.Barrel2, Enum.DropMode.Replace, {
    {Item = Item.Scrap, Min = 5, Max = 12},
    {Item = "wood", Min = 50, Max = 100},
})
```

| Constant | Exact canonical prefab | Numeric evidence ID |
|---|---|---:|
| Prefab.Loot.Barrel1 | `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab` | 966676416 |
| Prefab.Loot.Barrel2 | `assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab` | 555882409 |

[The unchanged D8 manifest](../api/loot-items.pinned.json) supplies every item
name, ID, vanilla stack limit, family and host pin. Its raw SHA256 is
`a0c7a40dfce177a8bcbf841316dc51dd5a366b6304522e86f030e539d5fead15`;
its canonical digest is
`37688a539718d8f80879b66bc88e9594bf90c37cf4d23f7425ac6813f45a47e8`.
The [public item table](api/LootItems.md) and [D8 ledger](GameplayD8-Validation.md)
retain operation-specific physical limits and negative evidence.

The two-row [prefab manifest](../api/loot-prefabs.pinned.json) uses the design's
transitional source arrangement: the existing D6 managed constants,
TryPrefabId body and bootstrap predicate remain independently authoritative.
Generation audits exact two-way equality; it changes none of those admission
rules. [Symbol algorithm 1 and compatibility snapshot](../generated/catalog-symbols.json)
have zero collisions. Numeric-leading names receive `N`; separators `.`, `_`,
`-` delimit PascalCase segments. There are no categories, runtime lookups,
aliases, unqualified definitions or new host hooks.

`Generate-Catalog.py --check` is side-effect-free and checks exact LF UTF-8
bytes. Its generated native/Core rows and symbol snapshot share fingerprint
`419eeabd336aab199aa23a4537976ee404d838664928dabda77ddcecb538c528`.
Strict tests reject duplicate/unknown JSON fields, wrong types, excessive
bytes/rows, noncanonical names, IDs, capabilities, unsorted/duplicate data,
collisions, source and host-pin drift, BOM/CRLF/extra-byte output drift, and
unreviewed symbol removal/retargeting. Compatibility errors carry a bounded,
deterministic old/new diff. Windows/Linux generation agrees byte-for-byte.
The existing D8 512-definition/64-KiB manifest/32-KiB projection bounds remain;
the D9 prefab input is exactly two rows and at most 4096 bytes, generated blocks
at most 32 KiB, and compatibility snapshot at most 128 KiB.

## D9-B — shared VM ownership and failure

Protected native VM initialization constructs exactly three pre-sized ordinary
tables: Item, Prefab and Prefab.Loot. It freezes the nested table before its
parent and stores two protected outer references on the owning VM. Domain
binding installation reuses those references. Reads do constant-time table
lookup with no host call. There is no exported ABI, scheduler or host-object
surface addition.

Native tests check all 221 exact values and both paths, string type/equality,
table counts, assignment/rawset/metatable rejection, private environment
shadowing, entry/module identity, cross-domain isolation, 100 addons and exact
callback delivery, ten healthy root replacements and three VM generations.
Healthy root/addon replacement retains the catalogs; failed candidates cannot
modify them. Fatal recovery recreates equivalent catalogs before republishing
domains. Complete unload frees the VM before native-library unload.

An independently compiled installer fingerprint is checked during protected VM
setup. The injected mismatch returns controlled INTERNAL_ERROR without
publishing a VM or partial namespace. Every allocator failure prefix through
the first successful setup is exercised: **28 actual allocation positions**,
with zero live native bytes after each teardown. Luau allocates pages; 221
fields do not imply 221 allocator calls. No heap/worker ceiling is raised.

The paired workload compares the immutable D8 native bundle against the D9
binary using identical current managed code, root source and 100-addon source.
Both platforms report the following deltas:

| Workload sample | D9 minus D8 VM bytes |
|---|---:|
| Root only; 1, 10 and 100 addons | 24,552 each |
| 50 addons | 8,192 |
| Healthy root replacement; addon replacement | 24,552 each |
| Fatal reconstruction; provider unload | 24,552 each |

The 50-addon sample reflects allocator/GC page boundaries; it is not a per-addon
catalog copy or a promise of constant total allocator delta at every instant.
Native instrumentation charges 30,696 bytes during catalog construction and
266,864 bytes for initialized VM state, with the same catalog charge at
1/10/50/100 addons and all three generations. The representative managed
100-addon sample is 8,625,384 bytes, below the unchanged 67,108,864-byte cap.
Startup samples include compilation/cache/JIT effects and establish no speed
guarantee. Exact paired timings, binary hashes, teardown and unmap evidence are
retained in the receipts. Both paired runs deterministically unmap/remove
their temporary native files. Live-provider retirement and full native teardown
are separate checks.

## D9-C — metadata, preview and actual editor

The existing Core generator emits Global Item/Prefab, readonly literal fields,
and the structural readonly PrefabLoot record. Its narrowly scoped formatting
rule admits readonly Global properties targeting declared records and omits
the new unusable empty namespace type shells. Existing Enum formatting and
method signatures remain unchanged. Metadata audits exact native rows,
fingerprints and global installation lines in both directions.

The official generator produces API JSON, `.d.luau`, documentation keys and
tooling metadata. Pack integrity covers the symbol map; `CatalogSha256` agrees
with runtime data. Every catalog read is Pure in the real preview VM, including
all 221 strings, nested immutability and absence of fake host calls. Loot and
inventory operations retain their existing preview classes and string types.
Item remains distinct from Items and ImageSource.Item.

Actual **VS Code 1.95.3**, build
`07b4ff1883f94da91f6d698744fc7c3638b59720`, runs the official extension at
`a4ca6d635819f95b249d17a8a978906b6f188485` on a headless Xvfb display.
Workspace Trust is granted through the actual UI. Word-based editor suggestions
are disabled to measure the language provider. Valid snapshots request
completion immediately after each dot, before existing member text. Root entry,
root imported module, addon entry and addon imported module each return exactly
221 Item fields, `Loot`, and `Barrel1`/`Barrel2`, with canonical string/path and
capability caveats in both metadata and inferred hover. Unsupported names are
absent. A hostile workspace `.config.luau` remains excluded from execution.
Markdown escapes are decoded only for comparing displayed canonical path text;
raw hover values are retained. The same pinned editor test is included in CI.

Generated-pack black-box tests pass: eight host tests, six supervised-analysis
tests, seventeen preview goldens, every catalog constant, and hostile preview
worker crash/hang/memory/output/protocol/cancellation/recovery checks. The
unchanged hostile fixture binary and protocol are source-compared with D8;
all production pack executables are D9 builds. No external repository is edited.

## D9-D — integration, regressions and packaging

Windows native CTest passes 23/23; Linux passes 25/25 with the inherited
preview-enabled cache and Linux-only ELF checks. The final fingerprint-guard
native fault checks pass on both hosts. Final Linux ASan/UBSan/leak CTest passes
24/24 in 287.39 seconds. Full Windows/Linux managed and real-VM suites pass,
including Loot D3–D8 declaration/publication/ownership/resource regressions,
gameplay, C4 inventory, GUI, modules, persistence, 100-addon fairness, shared-heap
exhaustion, 100 atomic replacements and 200 failed candidates. These model/VM
tests are not presented as new physical payout observations.

Both platform bundles pass two-generation deterministic packaging and exact
clean extraction/provenance checks. Actual clean installations load/unload the
native runtime, compile all **75** bundled Luau bodies, execute GUI/Player
fixtures and converge to zero live VMs. Windows imports reject dynamic
host-selected MSVC CRT dependencies. API/schema/architecture/document checks
preserve all 97 historical retained spans and all existing invariant owners.

The private CatalogD9 fixture is excluded from production packages. On actual
Carbon it checks startup, root/addon/imported-module visibility and immutability,
100 addons across four providers, exact callback convergence, failed and healthy
addon replacement, healthy root replacement, fatal VM recovery, provider unload,
and the unchanged 64-MiB limit. Persistence fixtures additionally exercise
root/addon isolation, host unload/reload, real process restart and native unmap.

| Host | Exact qualification tuple | Current integration result |
|---|---|---|
| Windows x64 | Rust 25653776, Assembly SHA `bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2`; Carbon 2.0.262.0 / 8a81d70; historical qualified hooks | PASS catalog matrix, namespace process restart, separate Query2D/host reload, restoration and zero workers |
| Linux x64 | Rust 25653776, Assembly SHA `cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450`; Carbon 2.0.261.0 / c74c4ca | PASS catalog matrix, Query2D/host reload/process restart, native unmap and exact restoration of 298 mutable prior files |

The Windows startup observer and lifecycle source are qualified with the exact
historical hook tuple. Installed newer hooks are backed up and restored with
their config, bytes, ACL and timestamps. No permanent host update policy changes.
Linux borrowed fixtures snapshot/restore complete mutable carbon/server trees,
hashes, owners and modes, and verify no owned processes before restoration.

Physical evidence is reused only from [D8](GameplayD8-Validation.md), whose
adapter source SHA remains
`08ddc4454242e1af09ecc30fb948cf8a1f264a89111023d239a808e1dd494004`.
The D8 item manifest, D6 allowlist and all Loot/PlayerInventory/Workspace/Items
validators are unchanged. Constant/literal parity is exercised through actual
VM Loot publication, PlayerInventory observation and persistence round-trip.
No physical item/drop research is repeated or advertised as new D9 evidence.

D6 retains two rules globally/per domain, one owner per qualified prefab,
eight entries/rule, integer amounts 0..1000, sum(Max)<=8000/rule, eight
worlds/occurrence, eight admitted occurrences/actual frame globally and one
in-flight policy frame. Existing staged-session/descriptor/table bounds, D13
PREPARE/COMMIT/VERIFY, D20/D23 lifetime/execution and I12 trusted-host
interference remain. Pre-COMMIT rejection preserves vanilla; post-COMMIT partial
failure has no fallback, rollback, replay or compensation. Stack behavior and
operation-specific capabilities remain exactly those recorded by D8.

## Retained negatives and qualification limits

No assertion, deadline, amount limit or resource ceiling is weakened for PASS.
Retain failed transcripts alongside successful receipts:

- The first native test assumed more than 221 allocator calls. Luau's page
  allocator disproved that assumption; exhaustive actual allocation-prefix
  coverage replaces the false call-count premise.
- Initial metadata synchronization lacked the new Core fingerprint property;
  the worker archive was corrected. Historical SinceApi assertions were scoped
  separately from the current API identity. An existing Enum shell was restored.
- A D12 paragraph first entered a protected historical span; the coverage gate
  rejected it. The new assignment now sits outside all retained spans.
- The first Windows managed wrapper used its SSH home as cwd. Linux persistence
  initially used an unsuitable overlay/tmp path. Correct cwd and the existing
  qualified persistence test-root override restored unchanged assertions.
- A duplicate sanitizer cache exhausted available disk before tests ran; the
  incomplete task-only copy was removed. Reused caches require the existing
  normal-cache SQLite source mount. Passed D9 scratch is hashed before cleanup;
  prior release artifacts and unrelated caches/workloads remain intact.
- Editor preparation first failed on duplicate npm config, then an executable
  on a noexec tmpfs, then missing XKB paths. Subsequent runs preserved real
  failures: incomplete source excluded by D19, a root module outside `modules/`,
  and raw Markdown punctuation escaping. Valid admitted sources, canonical
  module layout and displayed-text comparison correct the fixtures. Exact
  221/1/2 completion assertions and all original deadlines remain.
- The initial Windows live runner read synchronous startup logging before the
  final correlated RCON command response. It now selects the expected response
  within the same existing deadline. A combined Query2D/namespace-restart run
  failed the namespace fixture's fixed operation counters; the supplements pass
  separately with the original counters. Two external PowerShell argument
  wrapper attempts failed before host startup; structured parameter splatting
  replaced that wrapper. Host restoration remains independently verified.
- The first Linux persistence clone passed catalog integration on Carbon
  2.0.262, outside the requested pinned 2.0.261 tuple. It is supplemental only.
  The first exact pinned-host container could not resolve pre-existing absolute
  Bundle symlinks and was stopped after explicit asset-load failure; all 298
  mutable prior files were restored. The next fixture mounts that existing
  immutable Bundle target read-only. No server files are rewritten to fix it.

Raw logs, input hashes, binary identities, memory samples and restoration
receipts are indexed by the [evidence manifest](qualification/CatalogD9-Evidence.json).
Final-source hosted CI and synchronized-main closure are recorded below when
complete. No macOS server, authenticated client, new physical capability,
universal stack-plugin compatibility, power-loss or new release claim follows.
The next recommended phase is bounded catalog search/hover/quick-fix ergonomics
using the same static metadata; new content requires its own operation-specific
host qualification.
