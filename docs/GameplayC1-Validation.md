# Gameplay C1 — PlayerInventory foundation

Status: **GAMEPLAY-C1 PASS — PLAYER INVENTORY FACADE QUALIFIED**.
Starting main: `35bd8c3bb5d041063ba3388d23cc2cd37daa2808`.
Implementation and final tested production revision:
`ae4346973dda918403cd3a61aec9bf48d9ee873f`.
The subsequent closure commit changes evidence only; it does not change the
production source, generated surface, fixture or qualified artifacts.

D18 owns the additive facade; D11 original connection, D7/D10 publication,
D13 mutations and D23 event separation remain unchanged. Supplied
[C/D static research](history/gameplay/GameplayCDArchitecture-Research.md#gameplaycdarchitecture-research) is preserved with the
user's amendment: future drop enum namespace is `Enum.DropMode`, not
`Loot.DropMode`. C1 introduces no Enum/Loot surface or migration of the already
qualified GiveItemBehavior. No Gameplay D or C2 implementation is authorized.

Surface: `Player:GetInventory(): PlayerInventory`,
`PlayerInventory:CountItem(ShortName): number`,
`PlayerInventory:HasItem(ShortName, Amount?): boolean`, default Amount 1.
The sealed userdata captures scalar original token/UserId/publication only.
Weak cache holds one latest facade per Player proxy; there is no managed
inventory registry/watch or raw object. Acquisition resolves the connection and
captures the existing publication witness, without inventory or disk access.
Private host operations 40/41 reuse the existing bounded facade ABI transport;
reads dispatch to unchanged operations 26/27, not a new scanner. No C ABI export,
layout, provider/schema/Luau or release identity change.

Qualification receipts below record real-VM no-scan acquisition, boundary/malformed
inputs, D18 128-stack/exact integer behavior, failed publication, sharing,
connection changes, replacement/recovery, Windows/Linux host probes, sanitizers,
metadata/docs/tooling, packaging and final-source hosted CI. Controlled host
connections must not be reported as authenticated Rust clients. The real physical
host fixture creates/removes only its isolated Player/items and restores packages.

## Local qualification

Windows/Linux real compiler/VM tests PASS: no-scan acquisition; empty, 128-stack,
129-rejection and integer overflow; old read parity; malformed inputs and self;
failed nested publication/candidate; shared owner facade in consumer initialization;
provider teardown; reconnect old-token failure; root retirement and fatal recovery.
One thousand acquisitions/reads create no listeners. Weak cache identity is not
promised across intervening publication scopes.

Native tests cover 48 C1 allocation-failure positions and zero retained allocator
bytes. Windows fault suite and Linux ASan/UBSan/leak checks PASS. Larger bootstrap
changed an existing event fault test's freelist reuse, so maximum bounded prefab
payload now forces an allocator boundary (both refusal/acceptance required).
Linux debug then exposed an old transport defect: EnqueueEvent allocated on a
new Lua thread while lua_cpcall protected only the parent, violating ldo.cpp's
exception-thread assertion. Materialization now occurs on the protected parent,
then a two-value xmove. No vendor patch, suppressed assertion or ABI change.
Failed boundary/SIGILL evidence remains in task logs, not labeled PASS.

## Final-source live evidence

Rust 25653776/protocol 2634.289.1; Windows Carbon 2.0.262.0/8a81d70;
Linux 2.0.261.0/c74c4ca. Existing host pins/support limits remain authoritative.
Controlled real BasePlayer/connection/physical inventory, not authenticated client.
Production GetInventory/read, Give/Take parity, publication, reconnect and
replacement/recovery all PASS on both. Fixture SHA256:
`3a6b8ef90f09d05bb64e4fec0995cb224a84ccd50084404606fddefe813c0bd7`.

| Receipt | Windows | Linux |
|---|---|---|
| Log | `D:\Sandbox\Codex\Entity1AStartup\evidence\inventory-c1-20261010-214847-905\server.log` | `/root/codex/world-movement-20261003/evidence/inventory-c1-20261010-214858-1657833/server.log` |
| Log SHA256 | `a6e1a075b4ebc0c8d97f9617785024a572559c32e08068683d273c4a174e021f` | `5e8124849ad21b50d30c216a620d0d153fec1be9c8da744c40629f3e76c6d826` |
| Native SHA256 | `edcb40683926513041c66995c1260254a70051c41c839092efc9558d5d59255d` | `85b87fd44cf616255404cda14e92a6964cf9aff08d49b5e7357e5f405c3fa2f2` |

Runners restore package/native/compiler/hooks/config hashes and verify process
exit. Localhost-only 28335/28337, no client claim. Windows cache/source/runtime:
`D:\Sandbox\Codex\Discovery2BNative`; Linux `/root/codex/gameplay-a20261008`.
Bounded parallelism two, existing cached containers 3–4 GiB; no installation,
host restart or unrelated workload mutation. Evidence/caches retained.

Metadata/definitions regenerated; .NET10 tooling/goldens and API/architecture/
packaging/release checks PASS. Production package: 78 C# sources, no live fixture.
API 0.6.5-experimental, package 0.5.0, ABI1.5/provider1.2/schema1/Luau unchanged;
no release/tag. Final hosted full regression and clean-install gates PASS.

## Final-source hosted qualification

All runs below tested production revision
`ae4346973dda918403cd3a61aec9bf48d9ee873f` and completed successfully.

| Gate | Result / receipt |
|---|---|
| Windows x64 and Linux x64 native, managed, real-VM, loader and full runtime regressions | [Validation run 38103373635](https://github.com/gmoddev/CarbonLuau/actions/runs/38103373635): PASS on both platforms, including the dedicated PlayerInventory binding stage |
| ASan / UBSan / leak detection / lifecycle and allocation-fault fixtures | Same validation run: PASS |
| Deterministic packaging, bundled examples, clean extraction/install and private storage worker/import audit | Same validation run: PASS on Windows and Linux |
| Metadata, generated definitions and tooling Foundations A/B | [Tooling run 38103373584](https://github.com/gmoddev/CarbonLuau/actions/runs/38103373584): PASS; macOS tooling results are not a macOS server claim |
| Tooling baseline contracts | [Contract run 38103373598](https://github.com/gmoddev/CarbonLuau/actions/runs/38103373598): PASS |
| Documentation build/deployment | [Documentation run 38103373648](https://github.com/gmoddev/CarbonLuau/actions/runs/38103373648): PASS |

Existing Player inventory methods and the D18 scanner are unchanged. C1 adds
no event subscription, mutation, second inventory scanner or public enum.
Controlled live-host evidence above is newly measured; authenticated-client
network/UI delivery remains outside this read-only facade qualification.
All task qualification servers terminated and installed file hashes were restored.
Retained evidence and bounded build caches are not running qualification workloads.

## Scope and next permitted task

C2 handoff: prove completed net quantity observation for the same D11 connection
before exposing Signals. Raw container hooks alone are not completion proof;
reuse D18 validity rules and D23 shared budgets, preserve negative evidence.
Do not infer loot suppression, breaker attribution or future DropMode support.
