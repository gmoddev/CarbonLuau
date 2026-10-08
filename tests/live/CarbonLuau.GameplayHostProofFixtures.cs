// A0 test-package-only fixture. Never include in a production package.
// Uses task-owned constructed connections, not Steam authentication or clients.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Object = System.Object;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string GameplayProofPrefix = "[CarbonLuau:GameplayHostProof] ";
        private const ulong GameplayProofFirstId = 76561198000009001;
        private readonly List<BasePlayer> GameplayProofPlayers = new List<BasePlayer>(4);
        private readonly Dictionary<BasePlayer, string> GameplayProofOwnedIds = new Dictionary<BasePlayer, string>();
        private readonly Dictionary<string, Network.Connection> GameplayProofConnections =
            new Dictionary<string, Network.Connection>(StringComparer.Ordinal);
        private readonly List<GameplayHostObservation> GameplayProofObservations = new List<GameplayHostObservation>(128);
        private int GameplayProofOwner, GameplayProofBeforeOperation, GameplayProofRespawnHooks;
        private string GameplayProofOperation;
        private bool GameplayProofRunning, GameplayProofEarlyFailure, GameplayProofDeathVeto, GameplayProofSpawnOverride;
        private bool GameplayProofNestedDeath, GameplayProofNestedGuard;
        private static BasePlayer GameplayProofThrowPlayer;

        partial void RunGameplayHostProofFixtures()
        {
            NextTick(() => {
                try { ExecuteGameplayHostProofFixtures(); }
                catch (Exception Error) {
                    PrintError(GameplayProofPrefix + "FAIL " + Error.GetType().Name + ": " + Error.Message);
                }
                finally { ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"); }
            });
        }

        private static void RequireGameplayProof(bool Condition, string Reason)
        { if (!Condition) throw new InvalidOperationException(Reason); }

        private static object InvokeGameplayProofServer(string Name, params object[] Arguments)
        {
            MethodInfo Method = typeof(ServerMgr).GetMethod(Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            RequireGameplayProof(Method != null, "pinned ServerMgr method missing: " + Name);
            try { return Method.Invoke(ServerMgr.Instance, Arguments); }
            catch (TargetInvocationException Error) {
                throw new InvalidOperationException("actual ServerMgr." + Name + " failed", Error.InnerException ?? Error);
            }
        }

        private Network.Connection GameplayProofConnection(ulong Id)
        {
            RequireGameplayProof(GameplayProofConnections.Count < 4 ||
                GameplayProofConnections.ContainsKey(Id.ToString(CultureInfo.InvariantCulture)), "fixture connection bound");
            var Connection = new Network.Connection {
                userid = Id, username = "CarbonLuau A0 constructed connection",
                connected = true, active = true, state = (Network.Connection.State)4
            };
            GameplayProofConnections[Id.ToString(CultureInfo.InvariantCulture)] = Connection;
            return Connection;
        }

        // This hook records a fixed-count fixture object before PlayerInit, so a
        // failed actual initial path can still clean up its own created object.
        private object OnPlayerSpawn(BasePlayer Player, Network.Connection Connection)
        {
            if (!GameplayProofRunning || Connection == null ||
                !GameplayProofConnections.ContainsKey(Connection.userid.ToString(CultureInfo.InvariantCulture))) return null;
            if (Player != null && !GameplayProofPlayers.Contains(Player)) {
                if (GameplayProofPlayers.Count >= 4) { GameplayProofEarlyFailure = true; return true; }
                GameplayProofPlayers.Add(Player);
                Player.enableSaving = false;
                GameplayProofOwnedIds[Player] = Connection.userid.ToString(CultureInfo.InvariantCulture);
            }
            Puts(GameplayProofPrefix + "EARLY operation=" + GameplayProofOperation +
                " source=OnPlayerSpawn fullySpawned=" + (Player != null && Player.IsFullySpawned()) +
                " connected=" + (Player != null && Player.IsConnected) +
                " observations=" + GameplayProofObservations.Count);
            if (GameplayProofObservations.Count != GameplayProofBeforeOperation) GameplayProofEarlyFailure = true;
            return GameplayProofSpawnOverride ? (object)true : null;
        }

        private void OnPlayerRespawned(BasePlayer Player)
        {
            if (!GameplayProofRunning || Player == null ||
                !GameplayProofConnections.ContainsKey(Player.UserIDString)) return;
            GameplayProofRespawnHooks++;
            PlayerLifetime Lifetime = Gameplay.Players.Find(Player.UserIDString);
            bool Exact = Lifetime != null && Object.ReferenceEquals(Lifetime.Identity, Player) &&
                Object.ReferenceEquals(Lifetime.Connection, Player.Connection);
            bool NoCompletion = GameplayProofObservations.Count == GameplayProofBeforeOperation;
            if (!Exact || !NoCompletion) GameplayProofEarlyFailure = true;
            Puts(GameplayProofPrefix + "EARLY operation=" + GameplayProofOperation +
                " source=OnPlayerRespawned token=" + (Lifetime == null ? "nil" : Lifetime.Token) +
                " exactConnection=" + Exact + " completionNotYetCaptured=" + NoCompletion);
        }

        private object OnPlayerDeath(BasePlayer Player, HitInfo Hit)
        {
            if (GameplayProofRunning && GameplayProofNestedDeath && !GameplayProofNestedGuard && Player != null &&
                GameplayProofConnections.ContainsKey(Player.UserIDString)) {
                GameplayProofNestedGuard = true;
                try { Player.Die(GameplayProofHit(Player, Rust.DamageType.Suicide)); }
                finally { GameplayProofNestedGuard = false; }
                return true;
            }
            return GameplayProofRunning && GameplayProofDeathVeto && Player != null &&
                GameplayProofConnections.ContainsKey(Player.UserIDString) ? (object)true : null;
        }

        private void ReceiveGameplayProofObservation(GameplayHostObservation Observation)
        {
            if (!GameplayProofRunning || Observation == null ||
                !GameplayProofConnections.ContainsKey(Observation.UserId)) return;
            if (Thread.CurrentThread.ManagedThreadId != GameplayProofOwner ||
                GameplayProofObservations.Count >= 128) { GameplayProofEarlyFailure = true; return; }
            Network.Connection Connection = GameplayProofConnections[Observation.UserId];
            PlayerLifetime Lifetime = Gameplay.Players.Find(Observation.UserId);
            bool Exact = Lifetime != null && Lifetime.Token == Observation.PlayerToken &&
                Object.ReferenceEquals(Lifetime.Connection, Connection) &&
                Object.ReferenceEquals(Lifetime.Identity, Connection.player);
            if (!Exact) GameplayProofEarlyFailure = true;
            GameplayProofObservations.Add(Observation);
            Puts(GameplayProofPrefix + "CAPTURE operation=" + GameplayProofOperation +
                " sequence=" + GameplayProofObservations.Count + " kind=" + Observation.Kind +
                " token=" + Observation.PlayerToken + " userId=" + Observation.UserId +
                " exactConnection=" + Exact + " position=" + (Observation.Position.HasValue ? "sampled" : "nil") +
                " killerToken=" + (Observation.KillerToken ?? "nil") + " killerId=" + (Observation.KillerId ?? "nil"));
        }

        private void GameplayProofStep(string Name, Action Operation, int Spawns, int Deaths)
        {
            GameplayProofOperation = Name;
            GameplayProofBeforeOperation = GameplayProofObservations.Count;
            Operation();
            int ActualSpawns = 0, ActualDeaths = 0;
            for (int Index = GameplayProofBeforeOperation; Index < GameplayProofObservations.Count; Index++) {
                if (GameplayProofObservations[Index].Kind == "spawned") ActualSpawns++;
                else if (GameplayProofObservations[Index].Kind == "died") ActualDeaths++;
                else throw new InvalidOperationException("unknown private observation kind");
            }
            RequireGameplayProof(!GameplayProofEarlyFailure && ActualSpawns == Spawns && ActualDeaths == Deaths,
                Name + " expected spawns=" + Spawns + " deaths=" + Deaths +
                " actual spawns=" + ActualSpawns + " deaths=" + ActualDeaths +
                " earlyFailure=" + GameplayProofEarlyFailure);
            Puts(GameplayProofPrefix + "STEP_PASS " + Name + " spawns=" + ActualSpawns + " deaths=" + ActualDeaths);
        }

        private static HitInfo GameplayProofHit(BaseEntity Initiator, Rust.DamageType Kind)
        {
            var Hit = new HitInfo { Initiator = Initiator };
            Hit.damageTypes.Add(Kind, 1000);
            return Hit;
        }

        private void ExecuteGameplayHostProofFixtures()
        {
            RequireGameplayProof(Host != null && Host.Ready && Initialized && Gameplay != null &&
                BasePlayer.activePlayerList.Count == 0, "isolated zero-client ready server required");
            RequireGameplayProof(EntityStartupQualified && !EntityObserverBroken && VerifyAllEntityPatches(),
                "D20 qualified startup observer required");
            WriteGameplayLiveIlProof();
            RequireGameplayProof(GameplayDeathQualified && GameplaySpawnQualified,
                "private exact-host gameplay observers unavailable");
            var EntityStamps = new Dictionary<MethodBase, byte[]>(EntityReadPatchStamps);
            Action<GameplayHostObservation> SavedReceiver = GameplayObservationReceiver;
            Func<string, bool> SavedAdmission = GameplayCaptureAdmission;
            BasePlayer Player = null, Killer = null;
            GameplayProofOwner = Thread.CurrentThread.ManagedThreadId;
            GameplayProofRunning = true;
            GameplayObservationReceiver = ReceiveGameplayProofObservation;
            GameplayCaptureAdmission = Kind => GameplayProofRunning && GameplayProofObservations.Count < 128;
            try {
                Puts(GameplayProofPrefix + "SCOPE constructed Network.Connection; no Steam/authenticated-client receipt; bounded actors=4 records=128");
                GameplayProofStep("actual-initial-activation", () => {
                    Network.Connection Connection = GameplayProofConnection(GameplayProofFirstId);
                    Player = InvokeGameplayProofServer("SpawnNewPlayer", Connection) as BasePlayer;
                    RequireGameplayProof(Player != null && Player.IsAlive() &&
                        Object.ReferenceEquals(Connection.player, Player), "actual initial activation postcondition");
                }, 1, 0);
                RequireGameplayProof(GameplayProofRespawnHooks == 1, "actual initial Carbon respawn callback missing");
                PlayerLifetime Initial = Gameplay.Players.Find(Player.UserIDString);
                RequireGameplayProof(Initial != null, "actual initial D11 token missing");
                GameplayProofStep("sleeping-entry-without-spawn", () => Player.StartSleeping(), 0, 0);
                GameplayProofStep("sleeping-player-wakes", () => Player.EndSleeping(), 0, 0);
                GameplayProofStep("awake-null-environment-death", () => Player.Die(null), 0, 1);
                RequireGameplayProof(Player.IsDead(), "null-HitInfo death did not complete");
                GameplayProofStep("repeated-dead-die", () => Player.Die(null), 0, 0);
                GameplayProofStep("actual-respawn", () => Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                GameplayProofStep("already-alive-full-respawn", () => Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                GameplayProofDeathVeto = true;
                GameplayProofStep("real-OnPlayerDeath-veto", () => Player.Die(null), 0, 0);
                RequireGameplayProof(Player.IsAlive(), "death veto failed to preserve alive state");
                GameplayProofDeathVeto = false;
                GameplayProofStep("suicide-final-death", () => Player.Die(GameplayProofHit(Player, Rust.DamageType.Suicide)), 0, 1);
                GameplayProofStep("respawn-before-wound", () => Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                Player.EndSleeping();
                GameplayProofStep("actual-wounded-entry", () => Player.BecomeWounded(GameplayProofHit(null, Rust.DamageType.Bullet)), 0, 0);
                RequireGameplayProof(Player.IsWounded(), "actual wounded entry missing");
                GameplayProofStep("actual-wounded-revive", () => Player.StopWounded(null), 0, 0);
                RequireGameplayProof(Player.IsAlive() && !Player.IsWounded(), "actual wounded recovery failed");
                GameplayProofStep("wounded-again", () => Player.BecomeWounded(GameplayProofHit(null, Rust.DamageType.Bullet)), 0, 0);
                GameplayProofStep("wounded-terminal-null-death", () => Player.Die(null), 0, 1);
                GameplayProofStep("respawn-before-reconnect", () => Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                Network.Connection OldConnection = Player.Connection;
                GameplayProofStep("actual-server-disconnect", () => {
                    InvokeGameplayProofServer("OnDisconnected", "Gameplay A0 fixture", OldConnection);
                    OldConnection.connected = false; OldConnection.active = false;
                }, 0, 0);
                RequireGameplayProof(Gameplay.Players.Find(Initial.UserId) == null && Player.IsSleeping(),
                    "disconnect did not retire original connection and preserve sleeper");
                GameplayProofStep("actual-sleeper-reconnect", () => {
                    Network.Connection NewConnection = GameplayProofConnection(GameplayProofFirstId);
                    BasePlayer Reconnected = InvokeGameplayProofServer("SpawnPlayerSleeping", NewConnection) as BasePlayer;
                    RequireGameplayProof(Object.ReferenceEquals(Reconnected, Player) &&
                        Object.ReferenceEquals(Player.Connection, NewConnection), "same-object sleeper reconnect missing");
                    PlayerLifetime Fresh = Gameplay.Players.Find(Player.UserIDString);
                    RequireGameplayProof(Fresh != null && Fresh.Token != Initial.Token &&
                        Object.ReferenceEquals(Fresh.Connection, NewConnection), "reconnect did not create exact new D11 token");
                }, 0, 0);
                GameplayProofStep("reconnected-survivor-wakes", () => Player.EndSleeping(), 0, 0);
                GameplayProofStep("second-actual-initial-player", () => {
                    Killer = InvokeGameplayProofServer("SpawnNewPlayer", GameplayProofConnection(GameplayProofFirstId + 1)) as BasePlayer;
                    RequireGameplayProof(Killer != null, "second controlled player missing");
                }, 1, 0);
                Player.StartSleeping();
                GameplayProofStep("direct-player-initiator-final-death", () => Player.Die(GameplayProofHit(Killer, Rust.DamageType.Bullet)), 0, 1);
                GameplayHostObservation PvP = GameplayProofObservations[GameplayProofObservations.Count - 1];
                PlayerLifetime KillerLifetime = Gameplay.Players.Find(Killer.UserIDString);
                RequireGameplayProof(KillerLifetime != null && PvP.KillerToken == KillerLifetime.Token &&
                    PvP.KillerId == KillerLifetime.UserId, "exact direct initiator attribution missing");
                GameplayProofSpawnOverride = true;
                GameplayProofStep("real-OnPlayerSpawn-early-override", () => {
                    BasePlayer Uninitialized = InvokeGameplayProofServer("SpawnNewPlayer", GameplayProofConnection(GameplayProofFirstId + 2)) as BasePlayer;
                    RequireGameplayProof(Uninitialized != null && !Uninitialized.IsFullySpawned() &&
                        !Uninitialized.IsConnected, "early spawn override did not stop initialization");
                }, 0, 0);
                GameplayProofSpawnOverride = false;
                GameplayProofStep("real-game-mode-respawn-refusal", () => GameplayProofGameModeRefusal(Killer), 0, 0);
                GameplayProofStep("constructed-connected-NPC-exclusion", GameplayProofNpcExclusion, 0, 0);
                GameplayProofStep("indirect-nonplayer-owner-not-a-killer", () => GameplayProofIndirectDeath(Killer), 0, 1);
                GameplayProofStep("respawn-before-throwing-tail", () => Killer.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                GameplayProofStep("actual-RespawnAt-throwing-mission-tail", () => GameplayProofThrowingTail(Killer), 0, 0);
                GameplayProofStep("successful-respawn-after-throw", () => Killer.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                GameplayProofNestedDeath = true;
                long BeforeNested = GameplayLifecycleNested;
                GameplayProofStep("real-nested-OnPlayerDeath-poisons-capture", () => Killer.Die(null), 0, 0);
                GameplayProofNestedDeath = false;
                RequireGameplayProof(Killer.IsDead() && GameplayLifecycleNested > BeforeNested, "nested actual death was not accounted");
                GameplayProofStep("respawn-after-nested-death", () => Killer.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                GameplayProofStep("foreign-original-skip-fails-spawn-only", () => GameplayProofForeignSkip(Killer), 0, 0);
                Unsubscribe(nameof(OnPlayerRespawned));
                AssertGameplayProofEntityStamps(EntityStamps, "optional-respawn-hook-unsubscribed");
                int BeforeDemand = GameplayProofRespawnHooks;
                GameplayProofStep("respawn-with-fixture-hook-unsubscribed", () => Killer.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                RequireGameplayProof(GameplayProofRespawnHooks == BeforeDemand, "unsubscribed fixture hook still received dispatch");
                Subscribe(nameof(OnPlayerRespawned));
                AssertGameplayProofEntityStamps(EntityStamps, "optional-respawn-hook-resubscribed");
                GameplayProofStep("respawn-with-fixture-hook-resubscribed", () => Killer.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                RequireGameplayProof(GameplayProofRespawnHooks == BeforeDemand + 1, "resubscribed fixture hook dispatch missing");
                AssertGameplayProofEntityStamps(EntityStamps, "end");
                Puts(GameplayProofPrefix + "BASELINE_PASS actual initial+respawn, terminal death, veto, wound/revive, survivor reconnect, attribution, early override, game-mode refusal, connected NPC exclusion, D20 unchanged; position=" +
                    (PvP.Position.HasValue ? "sampled" : "DEFERRED") + "; indirect,skip,throw,nesting checked; remaining=off-thread,authenticated clients,saturation,public transport");
            }
            finally {
                GameplayProofDeathVeto = false; GameplayProofSpawnOverride = false;
                GameplayProofNestedDeath = false; GameplayProofNestedGuard = false;
                GameplayProofRunning = false;
                GameplayObservationReceiver = SavedReceiver; GameplayCaptureAdmission = SavedAdmission;
                for (int Index = GameplayProofPlayers.Count - 1; Index >= 0; Index--) {
                    BasePlayer Owned = GameplayProofPlayers[Index];
                    if (Object.ReferenceEquals(Owned, null)) continue;
                    try {
                        if (!String.IsNullOrEmpty(Owned.UserIDString)) OnPlayerDisconnected(Owned, "Gameplay A0 owned cleanup");
                        string OwnedId;
                        if (GameplayProofOwnedIds.TryGetValue(Owned, out OwnedId)) {
                            PlayerLifetime Retired = Gameplay.Players.Disconnect(OwnedId, Owned);
                            Gameplay.DisconnectGui(Retired); GameplayLifecycleDisconnected(Retired);
                        }
                        if (Owned.net != null) Owned.net.connection = null;
                        ulong NumericId;
                        if (!UInt64.TryParse(OwnedId, NumberStyles.None, CultureInfo.InvariantCulture, out NumericId)) continue;
                        BasePlayer Found;
                        if (BasePlayer.activePlayerLookup.TryGetValue(NumericId, out Found) &&
                            Object.ReferenceEquals(Found, Owned)) BasePlayer.activePlayerLookup.Remove(NumericId);
                        BasePlayer.activePlayerList.Remove(Owned);
                        if (BasePlayer.sleepingPlayerLookup.TryGetValue(NumericId, out Found) &&
                            Object.ReferenceEquals(Found, Owned)) BasePlayer.sleepingPlayerLookup.Remove(NumericId);
                        BasePlayer.sleepingPlayerList.Remove(Owned);
                        if (!Owned.IsDestroyed) Owned.Kill();
                    } catch (Exception Error) { PrintError(GameplayProofPrefix + "CLEANUP_FAIL " + Error.GetType().Name); }
                }
                GameplayProofPlayers.Clear(); GameplayProofOwnedIds.Clear(); GameplayProofConnections.Clear(); GameplayProofObservations.Clear();
                Puts(GameplayProofPrefix + "CLEANUP owned players retired; private receivers restored");
            }
        }

        private void GameplayProofGameModeRefusal(BasePlayer Player)
        {
            FieldInfo Active = typeof(BaseGameMode).GetField("svActiveGameMode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo Resetting = typeof(BaseGameMode).GetField("isResetting",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo RespawnId = typeof(BasePlayer).GetField("respawnId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            RequireGameplayProof(Active != null && Resetting != null && RespawnId != null,
                "pinned game-mode refusal fields missing");
            object SavedActive = Active.GetValue(null), SavedResetting = Resetting.GetValue(null);
            string Before = (string)RespawnId.GetValue(Player);
            GameObject Owned = new GameObject("CarbonLuau A0 temporary refusal mode");
            try {
                BaseGameMode Mode = Owned.AddComponent<BaseGameMode>();
                Mode.SetFlag((BaseEntity.Flags)256, true, false);
                Mode.SetFlag((BaseEntity.Flags)512, false, false);
                Active.SetValue(null, Mode); Resetting.SetValue(null, false);
                RequireGameplayProof(!Mode.CanPlayerRespawn(Player), "actual pinned game mode did not refuse respawn");
                Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null);
                RequireGameplayProof(Player.IsAlive() && (string)RespawnId.GetValue(Player) == Before,
                    "game-mode refusal progressed respawn while entry was already alive");
            }
            finally {
                Active.SetValue(null, SavedActive); Resetting.SetValue(null, SavedResetting);
                UnityEngine.Object.DestroyImmediate(Owned);
            }
        }

        private void GameplayProofNpcExclusion()
        {
            const ulong Id = GameplayProofFirstId + 3;
            BasePlayer Npc = GameManager.server.CreateEntity(
                "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab",
                new Vector3(0, 50, 0), Quaternion.identity, true) as BasePlayer;
            RequireGameplayProof(Npc != null, "pinned scientist prefab unavailable");
            RequireGameplayProof(GameplayProofPlayers.Count < 4, "fixture actor bound");
            GameplayProofPlayers.Add(Npc);
            GameplayProofOwnedIds[Npc] = Id.ToString(CultureInfo.InvariantCulture);
            RequireGameplayProof(Npc.IsNpc, "scientist prefab did not produce an NPC Player");
            Npc.userID = Id; Npc.UserIDString = Id.ToString(CultureInfo.InvariantCulture);
            Npc.displayName = "CarbonLuau A0 connected NPC construction";
            Npc.Spawn();
            Network.Connection Connection = GameplayProofConnection(Id);
            Connection.player = Npc; Npc.net.connection = Connection;
            BasePlayer.activePlayerLookup[Id] = Npc; BasePlayer.activePlayerList.Add(Npc);
            // This NPC negative deliberately constructs a tracked connection. It
            // does not substitute for the actual human initial-spawn proof.
            OnPlayerConnected(Npc);
            RequireGameplayProof(Gameplay.Players.Find(Npc.UserIDString) != null, "NPC negative lacks constructed D11 connection");
            Npc.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null);
            Npc.Die(null);
        }

        private void GameplayProofIndirectDeath(BasePlayer Victim)
        {
            BaseEntity Owned = GameManager.server.CreateEntity(
                "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
                new Vector3(0, 50, 0), Quaternion.identity, true);
            RequireGameplayProof(Owned != null, "indirect initiator fixture prefab missing");
            try {
                Owned.OwnerID = GameplayProofFirstId;
                Owned.Spawn();
                Victim.Die(GameplayProofHit(Owned, Rust.DamageType.Explosion));
                GameplayHostObservation Observation = GameplayProofObservations[GameplayProofObservations.Count - 1];
                RequireGameplayProof(Observation.Kind == "died" && Observation.UserId == Victim.UserIDString &&
                    Observation.KillerToken == null && Observation.KillerId == null,
                    "non-player ownership was converted to unsupported killer attribution");
            }
            finally { if (Owned != null && !Owned.IsDestroyed) Owned.Kill(); }
        }

        private static void GameplayProofThrowMissionPrefix(BasePlayer __instance)
        {
            if (Object.ReferenceEquals(__instance, GameplayProofThrowPlayer))
                throw new InvalidOperationException("CarbonLuau A0 mission-tail exception");
        }

        private void GameplayProofThrowingTail(BasePlayer Player)
        {
            MethodInfo Target = typeof(BasePlayer).GetMethod("ProcessMissionEvent",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo Prefix = typeof(CarbonLuau).GetMethod(nameof(GameplayProofThrowMissionPrefix),
                BindingFlags.NonPublic | BindingFlags.Static);
            RequireGameplayProof(Target != null && Prefix != null, "pinned mission tail fixture methods missing");
            var Patch = new Harmony("CarbonLuau.GameplayA0.MissionTailFixture");
            GameplayProofThrowPlayer = Player;
            try {
                Patch.Patch(Target, prefix: new HarmonyMethod(Prefix) { priority = Priority.First });
                bool Threw = false;
                try { Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null); }
                catch (InvalidOperationException Error) { Threw = Error.Message == "CarbonLuau A0 mission-tail exception"; }
                RequireGameplayProof(Threw && GameplaySpawnQualified, "throwing tail did not reject capture under unchanged source topology");
            }
            finally {
                GameplayProofThrowPlayer = null;
                Patch.Unpatch(Target, HarmonyPatchType.All, Patch.Id);
            }
        }

        private static bool GameplayProofSkipPrefix() { return false; }

        private void GameplayProofForeignSkip(BasePlayer Player)
        {
            MethodInfo Target = typeof(BasePlayer).GetMethod("RespawnAt",
                new[] { typeof(Vector3), typeof(Quaternion), typeof(BaseEntity) });
            MethodInfo Prefix = typeof(CarbonLuau).GetMethod(nameof(GameplayProofSkipPrefix), BindingFlags.NonPublic | BindingFlags.Static);
            var Patch = new Harmony("CarbonLuau.GameplayA0.OriginalSkipFixture");
            try {
                Patch.Patch(Target, prefix: new HarmonyMethod(Prefix) { priority = Priority.Last });
                Player.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null);
                RequireGameplayProof(!GameplaySpawnQualified && GameplayDeathQualified,
                    "foreign skip patch did not fail only the affected spawn capability");
            }
            finally {
                Patch.Unpatch(Target, HarmonyPatchType.All, Patch.Id);
                StopGameplayLifecycle(); InitializeGameplayLifecycle();
                GameplayObservationReceiver = ReceiveGameplayProofObservation;
                GameplayCaptureAdmission = Kind => GameplayProofRunning && GameplayProofObservations.Count < 128;
                RequireGameplayProof(GameplayDeathQualified && GameplaySpawnQualified,
                    "A0 exact topology reinitialization after removing fixture patch failed");
            }
        }

        private void AssertGameplayProofEntityStamps(Dictionary<MethodBase, byte[]> Expected, string Stage)
        {
            RequireGameplayProof(EntityStartupQualified && !EntityObserverBroken && VerifyAllEntityPatches() &&
                EntityReadPatchStamps.Count == Expected.Count, "D20 availability changed at " + Stage);
            foreach (var Pair in Expected) {
                byte[] Current;
                RequireGameplayProof(EntityReadPatchStamps.TryGetValue(Pair.Key, out Current) &&
                    Object.ReferenceEquals(Current, Pair.Value), "D20 Spawn patch record changed at " + Stage);
            }
            Puts(GameplayProofPrefix + "D20_PASS stage=" + Stage + " methods=" + Expected.Count);
        }
    }
}
