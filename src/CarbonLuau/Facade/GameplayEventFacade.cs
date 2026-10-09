using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class FacadeWorld
        {
            internal readonly GameplayEventBudget GameplayEvents;
            // Carbon replaces this with its exact qualified source predicate.
            // Carbon-independent fixtures supply synthetic host transitions.
            internal Func<string, bool> GameplayAvailable = Kind => true;
            private bool GameplayDemandDirty = true;
            private int GameplayDiedDemand, GameplaySpawnedDemand, GameplayEntitySpawnedDemand;
            private long GameplayFanoutCursor;
            internal void InvalidateGameplayDemand() { GameplayDemandDirty = true; }
            internal bool HasGameplayDemand(string Kind)
            {
                Players.CheckOwner();
                if (GameplayDemandDirty) {
                    GameplayDiedDemand = GameplaySpawnedDemand = GameplayEntitySpawnedDemand = 0;
                    if (Active != null && Active.Active && !Active.Disposed) {
                        if (!GameplayEvents.Visit()) return false;
                        GameplayDiedDemand += Active.GameplayDiedListeners;
                        GameplaySpawnedDemand += Active.GameplaySpawnedListeners;
                        GameplayEntitySpawnedDemand += Active.GameplayEntitySpawnedListeners;
                    }
                    foreach (FacadeSession Session in Addons.Values) if (Session.Active && !Session.Disposed) {
                        if (!GameplayEvents.Visit()) return false;
                        GameplayDiedDemand += Session.GameplayDiedListeners;
                        GameplaySpawnedDemand += Session.GameplaySpawnedListeners;
                        GameplayEntitySpawnedDemand += Session.GameplayEntitySpawnedListeners;
                    }
                    GameplayDemandDirty = false;
                }
                return Kind == "died" ? GameplayDiedDemand != 0 : Kind == "spawned" ? GameplaySpawnedDemand != 0 :
                    Kind == "entityspawned" && GameplayEntitySpawnedDemand != 0;
            }
            // Snapshot data only; no host object or callback is retained here.
            internal void GameplayEvent(string Kind, string Token, string UserId, string Name,
                PlayerPosition? Position, string KillerToken = null, string KillerId = null, string KillerName = null)
            {
                Players.CheckOwner();
                if (Kind != "died" && Kind != "spawned") throw new FacadeException("unknown player gameplay event");
                FacadePolicy.UserId(UserId); FacadePolicy.Text(Name, 128, "player name");
                ulong Parsed;
                if (!GameplayEventPolicy.Identity(Token, out Parsed))
                    throw new FacadeException("invalid player lifetime");
                if (KillerToken != null) {
                    FacadePolicy.UserId(KillerId); FacadePolicy.Text(KillerName, 128, "killer name");
                    if (Kind != "died" || !GameplayEventPolicy.Identity(KillerToken, out Parsed))
                        throw new FacadeException("invalid killer lifetime");
                } else if (KillerId != null || KillerName != null) throw new FacadeException("incomplete killer snapshot");
                var Fields = new[] {Kind, "", Token, UserId, Name, "", "", "", "", KillerToken ?? "", KillerId ?? "", KillerName ?? ""};
                if (Position.HasValue) {
                    PlayerPosition Value = Position.Value;
                    Fields[6] = GameplayCoordinate(Value.X); Fields[7] = GameplayCoordinate(Value.Y); Fields[8] = GameplayCoordinate(Value.Z);
                }
                FanoutGameplay(Fields, null);
            }
            internal void EntitySpawned(ulong HostIdentity, Func<FacadeSession, EntityLifetimeModel.Binding> Capture)
            {
                Players.CheckOwner();
                if (HostIdentity == 0 || Capture == null) throw new FacadeException("invalid entity event source");
                FanoutGameplay(new[] {"entityspawned", "", "", "", "", "", HostIdentity.ToString(CultureInfo.InvariantCulture),
                    "", "", "", "", ""}, Capture);
            }
            private void FanoutGameplay(string[] Fields, Func<FacadeSession, EntityLifetimeModel.Binding> Capture)
            {
                List<FacadeSession> Values = Sessions();
                Values.Sort((Left, Right) => Left.DomainLifetimeId.CompareTo(Right.DomainLifetimeId));
                int Start = Values.FindIndex(Value => Value.DomainLifetimeId > GameplayFanoutCursor);
                if (Start < 0) Start = 0;
                for (int Index = 0; Index < Values.Count; ++Index) {
                    if (!GameplayEvents.Visit()) break;
                    FacadeSession Session = Values[(Start + Index) % Values.Count];
                    if (Session.GameplayEvent(Fields, Capture)) GameplayFanoutCursor = Session.DomainLifetimeId;
                }
            }
            private static string GameplayCoordinate(float Value)
            {
                if (Single.IsNaN(Value) || Single.IsInfinity(Value)) throw new FacadeException("invalid gameplay position");
                return Value == 0 ? "0" : Value.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        public sealed partial class FacadeSession
        {
            internal int GameplayDiedListeners { get; private set; }
            internal int GameplaySpawnedListeners { get; private set; }
            internal int GameplayEntitySpawnedListeners { get; private set; }
            internal PublicationWitness RootGameplayPublication { get { return RootPublication; } }
            internal bool IsGameplayPublicationCurrent(PublicationWitness Witness)
            { return !Disposed && Active && World.IsActive(this) && Object.ReferenceEquals(Witness, RootPublication) && !Witness.Retired; }
            private void RefreshGameplayListeners()
            {
                GameplayDiedListeners = GameplaySpawnedListeners = GameplayEntitySpawnedListeners = 0;
                var Published = FirstPublication == null ? Listeners : FirstPublication.Listeners;
                foreach (string Kind in Published.Values) {
                    if (Kind == "died") GameplayDiedListeners++;
                    else if (Kind == "spawned") GameplaySpawnedListeners++;
                    else if (Kind == "entityspawned") GameplayEntitySpawnedListeners++;
                }
                World.InvalidateGameplayDemand();
            }
            private void ClearPendingGameplay()
            {
                while (Pending.Count != 0) {
                    FacadePendingEvent Value = Pending.Dequeue();
                    if (Value.GameplayReservation != 0) World.GameplayEvents.Release(this, Value.GameplayReservation);
                }
            }
            internal bool GameplayEvent(string[] Fields, Func<FacadeSession, EntityLifetimeModel.Binding> Capture = null)
            {
                if (!Active || Disposed || !World.IsActive(this) ||
                    (Fields[0] == "died" ? GameplayDiedListeners : Fields[0] == "spawned" ? GameplaySpawnedListeners : GameplayEntitySpawnedListeners) == 0) return false;
                bool Admitted = false;
                EntityLifetimeModel.Binding Entity = null;
                var Published = FirstPublication == null ? Listeners : FirstPublication.Listeners;
                foreach (var Listener in Published) {
                    if (!World.GameplayEvents.Visit()) break;
                    if (Listener.Value != Fields[0]) continue;
                    if (Pending.Count >= Capacity) { World.GameplayEvents.RejectQueue(); break; }
                    if (!World.GameplayEvents.Delivery(this)) break;
                    if (Fields[0] == "entityspawned" && Entity == null) {
                        Entity = Capture == null ? null : Capture(this);
                        if (Entity == null) { World.GameplayEvents.RejectTransfer(); break; }
                        Fields[2] = Entity.Record.Token.ToString(CultureInfo.InvariantCulture);
                        Fields[3] = Entity.Record.NetworkId.ToString(CultureInfo.InvariantCulture);
                        Fields[4] = Entity.Record.Prefab;
                        Fields[7] = RootGameplayPublication.Token.ToString(CultureInfo.InvariantCulture);
                    }
                    string IdValue = Listener.Key.ToString(CultureInfo.InvariantCulture);
                    Fields[1] = IdValue; byte[] Payload;
                    ulong Nonce = World.GameplayEvents.Reserve(this, Fields[0], IdValue, Fields, out Payload, Entity);
                    if (Nonce == 0) break;
                    try { Pending.Enqueue(new FacadePendingEvent(Payload, Nonce)); Admitted = true; }
                    catch { World.GameplayEvents.Release(this, Nonce, true); throw; }
                }
                return Admitted;
            }
        }
    }
}
