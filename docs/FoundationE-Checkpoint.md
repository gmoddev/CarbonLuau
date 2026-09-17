# Foundation E checkpoint

Status: superseded by the completed [Foundation E qualification record](FoundationE.md).
This file preserves the pre-worker checkpoint and is not the completion record.

Starting commit: `d9bc2bba7844c0a837d1af80f7e88ee18bdeb5ba`.

## Completed in this checkpoint

- Native callback selection is round-robin across ready domains while preserving FIFO order within each domain.
- Managed facade draining has one bounded global frame budget and rotates across root and addon domains.
- Future-only delayed work uses one delayed wake instead of persistent per-frame polling.
- Native coverage verifies progress for an unrelated domain when another domain is saturated.
- Runtime qualification coverage exercises 0, 1, 10, 50, and 100 addons, memory observations, activation and replacement latency, scheduler progress, the shared heap limit, retained values, and package/parser boundaries.
- Live fixtures cover reachable registration states, stale tokens, replacement, unregister and re-registration, CarbonLuau reload handling, and a 100-addon scale command.
- Packaging can optionally include the Foundation E live fixture.

## Local evidence

The native build and all four native CTest targets passed after the scheduler change. The complete managed runtime suite also passed.

Representative local 100-addon observations were:

- VM allocator: 1,949,081 bytes
- immutable package snapshots: 21,829 bytes
- process RSS: 69,693,440 bytes
- activation latency: 142.987 ms
- replacement latency: 1.416 ms
- service ordinals p50/p95/p99/max: 50/95/99/100
- saturated scheduler test: all ten unrelated callbacks made progress within two service frames
- shared heap: controlled rejection near the 64 MiB limit while the VM and root remained usable
- aggregate package snapshot: exactly 32 MiB accepted and overflow rejected
- forged archive metadata: actual decompressed-byte limit enforcement passed

These preliminary results support retaining the 64 MiB default, but the canonical D2 decision must wait for the required DockerPC qualification.

## Remaining work

1. Finish and run a Windows live Carbon runner covering provider unload/reload, CarbonLuau unload/reload while providers remain loaded, dependency loss/restoration, replacement, optional no-rebind behavior, and teardown with 100 addons.
2. Run clean Windows and Linux worker builds/tests plus the sanitizer matrix on DockerPC and preserve artifacts.
3. Confirm live fixture compilation and behavior in Carbon; adjust only defects demonstrated by qualification.
4. Make the final D2 determination from cross-platform measurements.
5. Assign the addon-capable experimental scripting API identity, keeping package, API, ABI, provider protocol, schema, and Luau identities separate.
6. Update canonical routing, compatibility/status reporting, API docs, README, addon docs, examples, release notes, and Foundation E evidence.
7. Run the full release/packaging/API checks, commit the qualified result, push, and wait for GitHub Actions.

No provider-capability feature, root-to-addon import, package solver, registry, restricted exposure profile, or per-addon heap limit has been started.
