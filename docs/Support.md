# Compatibility and support

Package 0.3.0 targets API 0.3.0-experimental. ABI, provider, addon
schema and Luau identities are recorded independently in release.json.
Published historical releases keep their own source, API and host envelope.
Current examples do not retroactively extend old binaries.

## Evidence scope

Development research, host logs, internal qualification receipts and operational
machine paths are deliberately absent from this public snapshot. Links to those
records point here; they do not claim that the internal receipt is present.
Public tests establish their own stated native/managed/synthetic scope, not a
fresh live Carbon result. Changing adapters or hosts requires affected evidence.

The current qualified gameplay tuple is Rust build 25653776, Windows Carbon
2.0.262.0/8a81d70 and Linux Carbon 2.0.261.0/c74c4ca; definition assembly/MVID
pins are in api/loot-items.pinned.json. No unrestricted future-host claim follows.
The World observer must be installed at initial startup. First hotload/full plugin
unload requires server restart to restore lifetime continuity.

Host-specific provider restrictions remain: historical Shockbyte evidence covers
load/probe only, not full runtime operation. macOS is not a server target. The
official editor executes qualified analysis/preview only on Windows/Linux;
macOS arm64 remains static-only, without a signing/quarantine distribution claim.

Authenticated-client GUI/Teleport convergence remains unqualified. TextBox is not
implemented. Preview approximates client rendering; it proves no authenticated
click, client delivery or gameplay mutation.

## Gameplay and persistence boundaries

See the [API reference](api/README.md) for version-specific inventory migration.
The finite manifests define supported Loot definitions and prefab strings.
Per-prefab independent ownership, atomic whole-owner publication, exact Item/UID/
world reciprocity and strict destruction-time suppression remain mandatory.
No breaker attribution, generic host hook or unrestricted item definition exists.

Stack-plugin compatibility is limited to the qualified runtime stackable checks:
PREPARE must admit declared Max, and the pre-suppression guard must still hold.
Container/hook stacking rules are not physical Drop normalizers. Trusted
in-process interference is not transactionally isolated. After COMMIT partial
failures do not trigger vanilla fallback, rollback, replay or compensation.

Persistence uses the qualified bounded worker and DELETE/EXTRA policy. Logical
quota does not reserve physical disk capacity; process-crash tests are not proof
against power loss. Storage failure and recovery preserve the documented error,
ownership and resource contracts rather than claiming unconditional durability.
