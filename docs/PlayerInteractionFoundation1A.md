# Player Interaction Foundation 1A

Status: **PASS within the available qualification envelope**.

Player-1A implements the first D18 slice: immutable `Vector3` and read-only
`Player.Position`. It does not implement health, inventory, Items, Teleport,
GiveItem, TakeItem, Inventory-M work or any Player-1B+ surface.

## Implemented surface

`Vector3.new(X, Y, Z)` creates a project-owned, zero-host-lifetime value. The
value exposes read-only X, Y, Z and Magnitude fields, exact component equality,
addition, subtraction, unary negation, scalar multiplication in either order
and scalar division. `tostring(Value)` returns `Vector3`, following the existing
CarbonLuau immutable-value convention.

Components reject wrong types, NaN, infinity and values outside the finite
System.Single range. Arithmetic rejects nonfinite scalars, division by zero,
vector multiplication/division and results outside that range. Values are not
clamped. Magnitude is the finite Euclidean magnitude computed in Luau's number
domain; construction at the maximum finite component range remains valid.

The implementation reuses the private zero-sized-userdata representation and
weak-key immutable records already used by CarbonLuau value types. No managed or
Unity object crosses into Luau. The common same-VM equality function permits
exact equality across domain-local metatables. Arithmetic creates a fresh value
without adding host authority. A value retained through an ordinary same-VM
reference remains usable after its defining domain retires; full-VM retirement
still destroys VM-local references under the existing recovery model.

`Player.Position` performs host operation 22 through the existing domain-bound
facade callback. Managed code resolves the exact D11 connection token and reads
one host-neutral three-float snapshot. The production Carbon adapter reads
`BasePlayer.transform.position`, which is the root Transform's world-space XYZ
position. Parent or mount state therefore does not change the coordinate space.
The adapter performs no eye offset, scale/axis conversion, terrain projection,
hook call, world scan, scheduling or publication.

Each successful property read returns a new `Vector3`. Position is not retained
or cached in the proxy. Disconnect, observed host invalidity and same-account
reconnect leave the old proxy stale; no Position snapshot becomes readable from
that proxy. A Vector3 already returned before disconnect remains an ordinary
value. Position reads are allowed during candidate and module initialization and
create no CarbonLuau-owned publication resource.

## Qualification

Qualified source revision:
`f8f9037c779f8fd091a24fa8e4eba55b7742bf45`.

The focused real-VM fixtures cover zero, signed, fractional and maximum-range
construction; NaN/infinity/overflow/type rejection; X/Y/Z/Magnitude;
immutability; exact equality; every approved arithmetic operator; unsupported
vector operations; division by zero; result overflow; fresh Position values;
changing host position; invalid host coordinates; provisional entry/module
reads; rejected-candidate isolation; disconnect; permanent stale latching;
same-account reconnect; and retained Vector3 usability after disconnect.

The addon regression creates a Vector3 in a provider module, compares and uses
it in two consumer domains, then proves the retained value remains usable after
the provider lifetime retires while required consumers reconstruct against the
replacement. Existing Foundation A-G, addon/provider, GUI, loader, package and
release checks remain in the same complete runtime suite.

An isolated Ubuntu 24.04 BigVPS container with four CPUs and 8 GiB passed all
five release native CTests, the complete Mono real-native runtime suite, loader
tests, architecture/API/release checks, deterministic packaging and all five
ASan/UBSan/leak CTests. The final-head worker fixture measured 2,000 live
Position reads in 12.04 ms. This is a worker observation, not a public
guarantee.

[Hosted validation](https://github.com/gmoddev/CarbonLuau/actions/runs/35567279475)
passed Windows and Ubuntu native/runtime/loader/package jobs plus the sanitizer
job at the qualified revision. The corresponding hosted fixtures measured
11.12 ms on Windows and 18.24 ms on Ubuntu for 2,000 Position reads. The first
hosted run exposed a test-only 5 ms GUI-2E rich-reload budget after the larger
bootstrap; the evidence revision raises only that recovery fixture to its
existing 20 ms budget. Production deadline defaults and behavior are unchanged.
[Documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/35566639504)
also passed.

DockerPC was unavailable: the configured reverse-tunnel endpoint refused the
connection. No Windows native/local or Windows live Carbon result is claimed.
Hosted Windows CI is separate evidence. Read-only BigVPS inspection found no
RustDedicated/Carbon server, so live Carbon Position behavior was unavailable;
mounted/parented behavior is supported by the direct world-position adapter and
model/source evidence, not mislabeled as a live result. No authenticated client
is required for this server-authoritative property.

## Identities and remaining scope

Player-1A remains additive under the unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and the pinned Luau revision are unchanged. Host operation 22 extends
the existing private callback protocol without changing its exported ABI.

Player.Health and Player.MaxHealth are implemented separately by Player-1B.
Items, CountItem, HasItem, Teleport, GiveItem, TakeItem, Inventory-M1/M2,
Vector3 Unit/Dot/Cross, Velocity, Rotation, CFrame and entity/world APIs remain
unimplemented.
