# CarbonLuau scripting API

The current development scripting identity is **experimental** `CarbonLuau
0.5.0-experimental`. Persistence-1B is **implemented and qualified within recorded
scope**; see [persistence](Persistence.md) and its [1B record](../PersistenceFoundation1B.md).
1C is NOT STARTED: combined closure, final-source CI and release approval remain separate.
The earlier gameplay/addon/GUI/Player surfaces retain their introduction versions
through 0.4.0-experimental. Their recorded Windows/Linux, sanitizer and Carbon
evidence, including [Foundation E](../FoundationE.md), does not qualify persistence.
This is server-side Luau, not Roblox API compatibility.

| Implemented surface | Reference |
|---|---|
| `game` and service discovery | [Globals](Globals.md) |
| Players, connected-player snapshots, join/leave events | [Players](Services/Players.md) |
| Player-issued chat commands | [Commands](Services/Commands.md) |
| Player identity, live position/health observation, messaging and permission query | [Player](Types/Player.md) |
| Rust item identity, bounded physical inventory observation and verified TakeItem/GiveItem | [Items](Services/Items.md), [Player](Types/Player.md), [GiveItemBehavior](Types/GiveItemBehavior.md) |
| Immutable world-coordinate values | [Vector3](Types/Vector3.md) |
| Command payload | [CommandContext](Types/CommandContext.md) |
| Event subscription | [Signal](Types/Signal.md), [Connection](Types/Connection.md) |
| Versions and limits | [Compatibility](Compatibility.md) |
| Player status, inventory, rewards and nontransactional shop examples | [Player examples](Player-Examples.md) |
| Existing `require` and `task.spawn/defer/delay` | [Phase 2 script contract](../Phase2.md) |
| Addon manifests, dependencies and package-qualified imports | [Addon composition](Addons.md) |
| Carbon provider registration protocol | [Addon providers](Addon-Providers.md) |
| Server-driven retained GUI with qualified Foundation 2A layout, 2B typed images and 2C scrolling source | [GUI guide](Gui.md), [GUI reference](Gui-Reference.md) |
| Experimental development persistence (0.5, scoped 1B qualification; 1C NOT STARTED) | [Guide and six examples](Persistence.md), [DataStoreService](Services/DataStoreService.md), [DataStore](Types/DataStore.md), [PersistedValue](Types/PersistedValue.md) |

Players and Commands are available beginning with API `0.3.0-experimental`.
Addon composition and GUI are available beginning with
`0.4.0-experimental`. They are implemented but experimental; see the evidence
records before deploying.
Methods use colon syntax. Services, proxies and command contexts cannot be edited.
API misuse and failed player operations raise controlled Luau errors; catch with
`pcall` when appropriate. Lookup absence returns nil; `IsConnected` returns false.
No API returns a host object or transfers ownership of a Rust player to Lua.

Successful healthy reload replaces the operator-root domain inside the current VM.
Failed candidate initialization preserves the old root, including its listeners,
commands and queued callbacks.
Callbacks run later through a bounded main-thread scheduler, not inline in hooks.
Timeout may retire the entire generation under the existing one-attempt recovery
policy. Previously delivered messages are not undone or deduplicated on recovery.

```lua
local Players = game:GetService("Players")
Players.PlayerAdded:Connect(function(Player)
    Player:SendMessage("Hello from CarbonLuau")
end)
```

Player-1A adds immutable `Vector3` and the live read-only `Player.Position`;
Player-1B adds live read-only `Player.Health` and `Player.MaxHealth`; Player-1C
adds `Items:Exists`, `Player:CountItem` and `Player:HasItem` under the same
identity; Player-1D adds committed-only `Player:Teleport(Vector3)` and
Player-1F-A adds committed-only verified `Player:TakeItem`; Player-1F-B adds
`Player:GiveItem(ShortName, Amount, Behavior?)`, defaulting to typed InventoryOnly. See the
[Player-1A](../PlayerInteractionFoundation1A.md) and
[Player-1B](../PlayerInteractionFoundation1B.md) and
[Player-1C](../PlayerInteractionFoundation1C.md) and
[Player-1D](../PlayerInteractionFoundation1D.md) and
[Player-1F-A](../PlayerInteractionFoundation1FA.md) and
[Player-1F-B](../PlayerInteractionFoundation1FB-Validation.md) and
[Player-1F-C combined closure](../PlayerInteractionFoundation1FC.md) qualification records. Not implemented in
the current scripting surface: raw inventory objects, entities, health mutation,
moderation/admin mutation, networking, HTTP, filesystem APIs,
arbitrary hooks/console execution, reflection, Roblox hierarchy/replication and
`task.wait`. Historical Phase 4 was deferred; the implemented Player-1C/1F
surface is qualified separately under revised D13 and
[D18](../Invariants.md#d18--player-interaction-foundation-1).
Teleport is available with exact-build server qualification and an explicit
authenticated-client deferral. TakeItem is available with exact-build
PREPARE/COMMIT/VERIFY qualification; GiveItem uses a bounded complete placement plan
and the supported-host InventoryOnly adapter.
Revised D13 requires PREPARE/COMMIT/VERIFY and exact-Player serialization for inventory mutation;
richer inventory mutation and raw host objects remain deferred.

Addon packages use exact dependency bindings, explicit exports and the readonly
`addon` context. They are public experimental behavior beginning with
`0.4.0-experimental`; they are not present in published v0.3.0 artifacts.

GUI Foundation 1 provides `game:GetService("Gui")`, ScreenGui, Frame and text
controls with retained properties, immutable values, per-Player Show/Hide and
secure Activated events. Foundation 2 adds release-qualified layout helpers,
typed ImageSource, ImageLabel, ImageButton and retained ScrollingFrame
configuration with client-local scroll position under the same
`0.4.0-experimental` identity. TextBox is not implemented. Advanced styling,
hover/focus and raw CUI remain unsupported. Authenticated-client visual, cursor, image-load,
click-receipt and reconciliation observations remain unqualified even though
the experimental source surface is available.

Foundation 3 adds deterministic `UIGridLayout`, bounded
`Frame.ClipsDescendants`, four project-owned `GuiFont` values, retained text
fonts and exact-Player one-way scroll effects under the same still-unreleased
`0.4.0-experimental` identity. Their server-side model, publication, lifecycle
and scale behavior is qualified. Authenticated-client clipping, font rendering
and actual scroll behavior remains explicitly unqualified.
