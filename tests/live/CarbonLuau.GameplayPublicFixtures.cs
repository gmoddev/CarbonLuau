// Test-package-only extension of the actual-host A0 fixture. Never ship.
using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = System.Object;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string GameplayPublicPrefix = "[CarbonLuau:GameplayPublic] ";

        partial void RunGameplayPublicFixtures(BasePlayer Actor, Action<GameplayHostObservation> ProductionReceiver,
            Func<string, bool> ProductionAdmission)
        {
            RequireGameplayProof(ProductionReceiver != null && ProductionAdmission != null &&
                GameplayDeathQualified && GameplaySpawnQualified && !Host.Busy, "production lifecycle adapter unavailable");
            // A0 can leave legitimate legacy PlayerAdded/Removing work queued.
            // Drain it before the new listeners exist; never call it GP replay.
            string BeforeRegistration = GameplayPublicDrain(true);
            RequireGameplayProof(BeforeRegistration == "", "unexpected public fixture markers before listener registration: " + BeforeRegistration);
            int BeforeListeners = Gameplay.Active.ListenerCount;
            long Generation = Host.VmGenerationId;
            Action<GameplayHostObservation> SpyReceiver = GameplayObservationReceiver;
            Func<string, bool> SpyAdmission = GameplayCaptureAdmission;
            string ActorId = Actor.UserIDString;
            string InitialId = (GameplayProofFirstId + 2).ToString(CultureInfo.InvariantCulture);
            GameplayCaptureAdmission = ProductionAdmission;
            GameplayObservationReceiver = Observation => {
                ReceiveGameplayProofObservation(Observation);
                ProductionReceiver(Observation);
            };
            try {
                GameplayPublicExecute("subscribe", "local S={Died=0,Spawned=0}; local P=game:GetService('Players'); " +
                    "local function Context(C) assert(table.isfrozen(C)); assert(C.Cause==nil and C.Weapon==nil); " +
                    "assert(not pcall(function() C.Position=nil end)); if C.Position then " +
                    "assert(C.Position.X==C.Position.X and C.Position.Y==C.Position.Y and C.Position.Z==C.Position.Z); " +
                    "assert(not pcall(function() C.Position.X=99 end)) end end; " +
                    "S.Death=P.PlayerDied:Connect(function(Player,C) Context(C); S.Died+=1; " +
                    "assert(Player.UserId=='" + ActorId + "' and Player.IsConnected); " +
                    "assert(C.Killer==nil and C.KillerId==nil); if S.Died==3 then " +
                    "assert(S.BeforeReconnect and S.BeforeReconnect~=Player and not S.BeforeReconnect.IsConnected); " +
                    "assert(not pcall(function() return S.BeforeReconnect.Position end)) end; " +
                    "print('PUBLIC_DIED|'..S.Died..'|'..Player.UserId) end); " +
                    "S.Spawn=P.PlayerSpawned:Connect(function(Player,C) Context(C); S.Spawned+=1; " +
                    "assert(Player.IsConnected and C.Killer==nil and C.KillerId==nil); " +
                    "if S.Spawned==1 then assert(Player.UserId=='" + InitialId + "') " +
                    "else assert(Player.UserId=='" + ActorId + "'); S.BeforeReconnect=Player end; " +
                    "print('PUBLIC_SPAWNED|'..S.Spawned..'|'..Player.UserId); " +
                    "if S.Spawned==4 then assert(S.Died==3); S.Death:Disconnect(); S.Death:Disconnect(); " +
                    "S.Spawn:Disconnect(); print('PUBLIC_LISTENERS_DISCONNECTED') end end)");
                RequireGameplayProof(Gameplay.Active.ListenerCount == BeforeListeners + 2, "committed public listeners missing");
                string Registration = GameplayPublicDrain(true);
                RequireGameplayProof(Registration == "", "public registration replayed historical lifecycle state: " + Registration);

                GameplayPublicOperation("actual-initial", () => {
                    BasePlayer Initial = InvokeGameplayProofServer("SpawnNewPlayer", GameplayProofConnection(GameplayProofFirstId + 2)) as BasePlayer;
                    RequireGameplayProof(Initial != null && Initial.IsAlive(), "public actual initial activation missing");
                }, 1, 0);
                RequireGameplayProof(GameplayPublicDrain() == "PUBLIC_SPAWNED|1|" + InitialId + "\n", "public initial spawn callback missing");

                GameplayPublicOperation("terminal-death", () => Actor.Die(null), 0, 1);
                RequireGameplayProof(GameplayPublicDrain() == "PUBLIC_DIED|1|" + ActorId + "\n", "public final death callback missing");
                GameplayPublicOperation("respawn", () => Actor.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                RequireGameplayProof(GameplayPublicDrain() == "PUBLIC_SPAWNED|2|" + ActorId + "\n", "public respawn callback missing");

                // This earlier native task executes before the newly captured
                // death payload. Disconnect is the public Connection operation.
                GameplayPublicExecute("queued-disconnect", "local C=game:GetService('Players').PlayerDied:Connect(function() " +
                    "error('cancelled public listener ran') end); task.defer(function() C:Disconnect(); C:Disconnect(); " +
                    "print('PUBLIC_QUEUED_DISCONNECT') end)");
                GameplayPublicOperation("death-with-queued-listener-cancellation", () => Actor.Die(null), 0, 1);
                string Cancelled = GameplayPublicDrain();
                RequireGameplayProof(Cancelled == "PUBLIC_QUEUED_DISCONNECT\nPUBLIC_DIED|2|" + ActorId + "\n",
                    "public queued listener disconnect did not suppress its payload: " + Cancelled);
                GameplayPublicOperation("respawn-before-reconnect", () => Actor.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                RequireGameplayProof(GameplayPublicDrain() == "PUBLIC_SPAWNED|3|" + ActorId + "\n", "public spawn before reconnect missing");

                PlayerLifetime Previous = Gameplay.Players.Find(ActorId);
                GameplayPublicOperation("capture-spawn-before-victim-disconnect", () => Actor.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                GameplayProofStep("public.actual-disconnect-before-admission", () => {
                    Network.Connection Connection = Actor.Connection;
                    InvokeGameplayProofServer("OnDisconnected", "public queued-lifetime fixture", Connection);
                    Connection.connected = false; Connection.active = false;
                }, 0, 0);
                GameplayProofStep("public.actual-sleeper-reconnect-before-admission", () => {
                    Network.Connection Connection = GameplayProofConnection(Actor.userID);
                    BasePlayer Reconnected = InvokeGameplayProofServer("SpawnPlayerSleeping", Connection) as BasePlayer;
                    PlayerLifetime Fresh = Gameplay.Players.Find(ActorId);
                    RequireGameplayProof(Object.ReferenceEquals(Reconnected, Actor) && Fresh != null &&
                        Previous != null && Fresh.Token != Previous.Token, "public reconnect did not replace exact connection lifetime");
                }, 0, 0);
                RequireGameplayProof(GameplayPublicDrain() == "", "queued old-victim payload retargeted after reconnect");
                GameplayPublicOperation("new-exact-connection-death", () => Actor.Die(null), 0, 1);
                RequireGameplayProof(GameplayPublicDrain() == "PUBLIC_DIED|3|" + ActorId + "\n", "new connection callback/stale facade proof missing");
                GameplayPublicOperation("final-respawn-and-listener-retirement", () => Actor.RespawnAt(new Vector3(0, 50, 0), Quaternion.identity, null), 1, 0);
                RequireGameplayProof(GameplayPublicDrain() == "PUBLIC_SPAWNED|4|" + ActorId + "\nPUBLIC_LISTENERS_DISCONNECTED\n",
                    "public final spawn/listener retirement missing");
                RequireGameplayProof(Gameplay.Active.ListenerCount == BeforeListeners && Host.VmGenerationId == Generation &&
                    Gameplay.GameplayEvents.PendingCount == 0 && Gameplay.GameplayEvents.RetainedBytes == 0,
                    "public phase changed VM lifetime or retained listener/payload state");
                Puts(GameplayPublicPrefix + "PASS production admission/receiver -> managed reservation -> native dispatcher -> deferred real-Luau initial/death/respawn; immutable contexts, queued disconnect, exact reconnect, no synchronous VM entry");
            }
            finally { GameplayObservationReceiver = SpyReceiver; GameplayCaptureAdmission = SpyAdmission; }
        }

        private void GameplayPublicExecute(string Name, string Source)
        {
            ExecutionResult Result = Host.Execute("gameplay.public." + Name, Source);
            RequireGameplayProof(Result.Status == RuntimeStatus.OK, "public script " + Name + ": " + Result.Error);
            RequireGameplayProof(String.IsNullOrEmpty(Result.Logs), "public setup synchronously emitted callback output");
        }

        private void GameplayPublicOperation(string Name, Action Operation, int Spawns, int Deaths)
        {
            ulong Before = Host.Attempted;
            GameplayProofStep("public." + Name, Operation, Spawns, Deaths);
            RequireGameplayProof(Host.Attempted == Before && !Host.Busy && Gameplay.GameplayEvents.PendingCount > 0,
                "public host capture entered the VM synchronously or failed production queue admission");
        }

        private string GameplayPublicDrain(bool Diagnose = false)
        {
            var Output = new StringBuilder();
            for (int Turn = 0; Turn < 64 && Host.HasReadyWork; Turn++) {
                foreach (ExecutionResult Result in Host.Drain()) {
                    RequireGameplayProof(Result.Status == RuntimeStatus.OK, "public callback: " + Result.Error);
                    RequireGameplayProof(Output.Length + Result.Logs.Length <= 16384, "public fixture output bound");
                    Output.Append(Result.Logs);
                }
            }
            RequireGameplayProof(!Host.HasReadyWork && Gameplay.GameplayEvents.PendingCount == 0 &&
                Gameplay.GameplayEvents.RetainedBytes == 0, "bounded public drain did not converge");
            string All = Output.ToString().Replace("\r\n", "\n");
            var Public = new StringBuilder();
            foreach (string Line in All.Split('\n'))
                if (Line.StartsWith("PUBLIC_", StringComparison.Ordinal)) Public.Append(Line).Append('\n');
            if (Diagnose && All.Length != 0) {
                string Detail = All.Length <= 2048 ? All : All.Substring(0, 2048);
                Puts(GameplayPublicPrefix + "DRAIN_DIAGNOSTIC bytes=" + All.Length +
                    " publicBytes=" + Public.Length + " output=" + Detail);
            }
            return Public.ToString();
        }
    }
}
