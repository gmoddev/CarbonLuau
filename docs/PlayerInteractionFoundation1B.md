# Player Interaction Foundation 1B

Status: **PASS within the available qualification envelope**.

Player-1B implements the D18 read-only health slice:
`Player.Health` and `Player.MaxHealth`. It does not implement health mutation,
inventory observation or mutation, Items, Teleport, entity access or any
Player-1C+ surface.

## Implemented surface

Each property access resolves the proxy's exact D11 connection lifetime and
performs one direct owner-thread host read. `Health` invokes
`BasePlayer.Health()` and `MaxHealth` invokes `BasePlayer.MaxHealth()` through
the existing domain-bound facade. Results are transported as invariant
round-trip finite System.Single values and become Luau numbers. No host object
crosses the boundary.

The two values are independent. CarbonLuau does not clamp Health to MaxHealth,
does not replace unusual finite values and does not assume MaxHealth is 100.
NaN or infinity from the host raises a controlled error. No result is cached in
the Player proxy, so later reads observe later host state. Both properties are
read-only through the existing frozen Player proxy.

Reads are allowed during provisional entrypoint and module execution because
they are bounded observations that create no CarbonLuau resource and perform no
host mutation. A Player proxy shared through an addon module remains bound to
the defining domain lifetime. Retirement makes subsequent host-backed reads
fail closed; a replacement domain receives a new facade and does not retarget
the old value.

Disconnect, observed host invalidity and same-account reconnect leave the old
proxy stale. Unlike Name and UserId, Health and MaxHealth retain no disconnect
snapshot. Normal, sleeping, wounded and dead-but-still-host-valid exact Players
remain readable because the adapter adds no gameplay-state eligibility gate.

## Exact target source evidence

The production target is Rust Dedicated Server Steam build `25353106` with
Carbon `2.0.259.0`. The exact target `Assembly-CSharp.dll` was downloaded and
decompiled for this task. `BaseCombatEntity.Health()` returns `_health`
directly. Its base `MaxHealth()` honors a positive `maxHealthOverride`; the
`BasePlayer` override otherwise computes the current `_maxHealth` multiplied by
the current `Max_Health` modifier. Neither getter invokes a plugin hook or gates
on sleeping, wounded or dead state. This establishes the dynamic maximum-health
adapter and rules out a hard-coded 100.

## Qualification

The focused real-VM fixtures cover zero, positive, negative and fractional
finite values; changing reads; Health above MaxHealth; non-100 and changing
MaxHealth; no clamping; NaN/infinity rejection; immutability; provisional
entrypoint and module reads; rejected-candidate isolation; normal, sleeping,
wounded and dead-but-host-valid model states; disconnect; permanent stale
latching; same-account reconnect; and repeated read overhead.

The addon regression exports an exact Player proxy from a public module, reads
both properties from optional and required consumer domains, rejects reads
after the defining provider lifetime retires and verifies that a reconstructed
required consumer reads through the replacement domain rather than retargeting
the old proxy.

An isolated Ubuntu 24.04 BigVPS worker with four CPUs and 8 GiB passed all five
release native CTests, the complete Mono real-native runtime suite, loader
tests, architecture/API/release checks, deterministic packaging and all five
ASan/UBSan/leak CTests. A repeated final runtime pass measured 2,000 live
Health/MaxHealth read pairs in 13.82 ms. This is a worker observation, not a
public performance guarantee.

The exact Rust build `25353106` and Carbon `2.0.259.0` were then exercised in an
isolated live server with a controlled real `BasePlayer` and
`Network.Connection`. Three complete plugin cycles passed direct fractional
Health, dynamic and fractional MaxHealth, Health above MaxHealth, sleeping,
wounded, explicit dead-but-host-valid LifeState, disconnect staleness,
same-account reconnect, 100 replacements and native unload. The runner exited
successfully and makes no authenticated-client claim. Its 100 ms callback
deadline is test-only; production configuration and defaults are unchanged.

DockerPC was unavailable, so no Windows native/local or Windows live result is
claimed. Hosted Windows CI is separate evidence and is not a substitute for
that unavailable local environment.

## Identities and remaining scope

Player-1B remains additive under the unreleased package `0.4.0` and scripting
API `0.4.0-experimental`. Native ABI `1.4`, provider protocol `1.2`, package
schema `1` and the pinned Luau revision are unchanged. Private host operations
23 and 24 extend the existing callback protocol without changing its exported
ABI.

Health mutation, Items, CountItem, HasItem, Teleport, GiveItem, TakeItem,
Inventory-M1/M2, explicit wounded/dead APIs and entity/world APIs remain
unimplemented.
