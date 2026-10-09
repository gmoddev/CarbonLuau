using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal static class GameplayEventPolicy
        {
            internal const int CapturesPerFrame = 128, DeliveriesPerFrame = 128, DeliveriesPerDomain = 32;
            internal const int VisitsPerFrame = 4096, Reservations = 512, PayloadBytes = 2048;
            internal const int RetainedTransportBytes = Reservations * PayloadBytes * 2;
            internal static bool Kind(string Value) { return Value == "died" || Value == "spawned" || Value == "entityspawned" || Value == "entitydestroyed"; }
            internal static bool Identity(string Text, out ulong Value)
            {
                Value = 0;
                return Text != null && Text.Length >= 1 && Text.Length <= 20 && Text[0] != '0' &&
                    UInt64.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value) && Value != 0 &&
                    Text == Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        // A separately qualified source supplies the original incarnation's
        // immutable scalars and bounded weak/scalar completion witness. This is
        // not a live Entity binding and never authorizes post-removal host reads.
        internal sealed class GameplayEntityDestroyedObservation
        {
            internal readonly string Id, Prefab;
            internal readonly ulong EpochIdentity;
            internal readonly PlayerPosition? Position;
            internal readonly Func<bool> ValidateCompletion;
            internal readonly ulong PublicationCutoff;
            internal readonly string EpochText, X, Y, Z;
            internal GameplayEntityDestroyedObservation(string Id, string Prefab, ulong EpochIdentity,
                PlayerPosition? Position, Func<bool> ValidateCompletion, ulong PublicationCutoff = UInt64.MaxValue)
            {
                ulong Parsed;
                if (!GameplayEventPolicy.Identity(Id, out Parsed) || EpochIdentity == 0 || ValidateCompletion == null)
                    throw new FacadeException("invalid entity destruction observation");
                FacadePolicy.Text(Prefab, 512, "entity prefab");
                if (Prefab.Length == 0) throw new FacadeException("invalid entity prefab");
                this.Id = Id; this.Prefab = Prefab; this.EpochIdentity = EpochIdentity;
                this.Position = Position; this.ValidateCompletion = ValidateCompletion;
                this.PublicationCutoff = PublicationCutoff;
                EpochText = EpochIdentity.ToString(CultureInfo.InvariantCulture);
                X = Position.HasValue ? Coordinate(Position.Value.X) : "";
                Y = Position.HasValue ? Coordinate(Position.Value.Y) : "";
                Z = Position.HasValue ? Coordinate(Position.Value.Z) : "";
            }
            private static string Coordinate(float Value)
            {
                if (Single.IsNaN(Value) || Single.IsInfinity(Value)) throw new FacadeException("invalid gameplay position");
                return Value == 0 ? "0" : ((double)Value).ToString("R", CultureInfo.InvariantCulture);
            }
            internal bool Matches(string[] Fields)
            {
                return Fields.Length == 12 && Fields[2] == EpochText && Fields[3] == Id && Fields[4] == Prefab &&
                    Fields[6] == X && Fields[7] == Y && Fields[8] == Z &&
                    Fields[9] == "" && Fields[10] == "" && Fields[11] == "";
            }
        }

        // Accounting only. Pending and native scheduler queues remain their
        // existing owners. A reservation survives transfer and callback entry.
        internal sealed class GameplayEventBudget
        {
            private sealed class Reservation
            {
                internal FacadeSession Owner;
                internal string Kind, Listener;
                internal FacadeSession.PublicationWitness Publication;
                internal int Charge;
                internal bool Native, Cancelled;
                // Strong canonical record, weak Rust identity. No host object
                // or network-ID lookup is retained as event authority.
                internal EntityLifetimeModel.Binding Entity;
                internal string EntityHost;
                internal GameplayEntityDestroyedObservation Destroyed;
            }
            private readonly FacadeWorld World;
            private readonly Dictionary<ulong, Reservation> Held = new Dictionary<ulong, Reservation>(GameplayEventPolicy.Reservations);
            private readonly Dictionary<long, int> DomainDeliveries = new Dictionary<long, int>(129);
            internal Func<long> FrameClock = () => 0;
            private long Frame = Int64.MinValue;
            private int Captures, Deliveries, Visits, DestroyPositions;
            private ulong Next;
            internal int RetainedBytes { get; private set; }
            internal int PendingCount { get { return Held.Count; } }
            internal ulong Accepted, Released, NoSubscribers, ProducerRejected, FanoutRejected, ScanRejected;
            internal ulong QueueRejected, PayloadRejected, NativeRejected, StaleRejected;
            internal ulong DestroyPositionAccepted, DestroyPositionRejected;

            internal GameplayEventBudget(FacadeWorld World) { this.World = World; }
            private static void Count(ref ulong Value) { if (Value != UInt64.MaxValue) ++Value; }
            private void BeginFrame()
            {
                World.Players.CheckOwner();
                long Current = FrameClock();
                if (Current == Frame) return;
                Frame = Current; Captures = Deliveries = Visits = DestroyPositions = 0; DomainDeliveries.Clear();
            }
            // Optional pre-removal TRS snapshots must also be bounded when Kill
            // is vetoed or no event can be admitted. Exhaustion means nil
            // Position, not an invented coordinate or consumed event capture.
            internal bool DestroyPositionSnapshot()
            {
                BeginFrame();
                if (!World.HasGameplayDemand("entitydestroyed")) return false;
                if (DestroyPositions >= GameplayEventPolicy.CapturesPerFrame) {
                    Count(ref DestroyPositionRejected); return false;
                }
                DestroyPositions++; Count(ref DestroyPositionAccepted); return true;
            }
            internal bool Capture(string Kind)
            {
                BeginFrame();
                if (!GameplayEventPolicy.Kind(Kind)) { Count(ref PayloadRejected); return false; }
                if (!World.HasGameplayDemand(Kind)) { Count(ref NoSubscribers); return false; }
                if (Captures >= GameplayEventPolicy.CapturesPerFrame) { Count(ref ProducerRejected); return false; }
                Captures++; return true;
            }
            internal bool Visit()
            {
                BeginFrame();
                if (Visits >= GameplayEventPolicy.VisitsPerFrame) { Count(ref ScanRejected); return false; }
                Visits++; return true;
            }
            internal bool Delivery(FacadeSession Owner)
            {
                BeginFrame(); int CountValue;
                DomainDeliveries.TryGetValue(Owner.DomainLifetimeId, out CountValue);
                if (Deliveries >= GameplayEventPolicy.DeliveriesPerFrame || CountValue >= GameplayEventPolicy.DeliveriesPerDomain) {
                    Count(ref FanoutRejected); return false;
                }
                Deliveries++; DomainDeliveries[Owner.DomainLifetimeId] = CountValue + 1; return true;
            }
            internal ulong Reserve(FacadeSession Owner, string Kind, string Listener, string[] Fields, out byte[] Payload,
                EntityLifetimeModel.Binding Entity = null, GameplayEntityDestroyedObservation Destroyed = null)
            {
                Payload = null;
                if (Held.Count >= GameplayEventPolicy.Reservations || Next == UInt64.MaxValue) { Count(ref QueueRejected); return 0; }
                ulong Nonce = ++Next;
                Fields[5] = Nonce.ToString(CultureInfo.InvariantCulture);
                try { Payload = FacadePolicy.Pack(Fields); }
                catch { Count(ref PayloadRejected); return 0; }
                int Charge = Payload.Length * 2;
                if (Payload.Length > GameplayEventPolicy.PayloadBytes || RetainedBytes > GameplayEventPolicy.RetainedTransportBytes - Charge) {
                    Payload = null; Count(ref PayloadRejected); return 0;
                }
                Held.Add(Nonce, new Reservation {Owner = Owner, Kind = Kind, Listener = Listener,
                    Publication = Owner.RootGameplayPublication, Charge = Charge, Entity = Entity, Destroyed = Destroyed,
                    EntityHost = Kind == "entityspawned" ? Fields[6] : null});
                RetainedBytes += Charge; Count(ref Accepted); return Nonce;
            }
            internal bool ToNative(FacadeSession Owner, ulong Nonce)
            {
                Reservation Value;
                if (!Held.TryGetValue(Nonce, out Value) || !Object.ReferenceEquals(Value.Owner, Owner) || Value.Native) return false;
                Value.Native = true; return true;
            }
            internal bool Validate(FacadeSession Owner, string[] Fields)
            {
                World.Players.CheckOwner();
                ulong Nonce; Reservation Value;
                if (Fields.Length != 12 || !GameplayEventPolicy.Identity(Fields[5], out Nonce) ||
                    !Held.TryGetValue(Nonce, out Value) || !Object.ReferenceEquals(Value.Owner, Owner) || !Value.Native || Value.Cancelled ||
                    Value.Kind != Fields[0] || Value.Listener != Fields[1] || !Owner.IsGameplayPublicationCurrent(Value.Publication)) {
                    Count(ref StaleRejected); return false;
                }
                if (Fields[0] == "entityspawned" && (Value.Entity == null || Value.Entity.Retired || Value.Entity.Record.Retired ||
                    Fields[2] != Value.Entity.Record.Token.ToString(CultureInfo.InvariantCulture) ||
                    Fields[3] != Value.Entity.Record.NetworkId.ToString(CultureInfo.InvariantCulture) ||
                    Fields[4] != Value.Entity.Record.Prefab ||
                    Fields[6] != Value.EntityHost ||
                    Fields[7] != Value.Publication.Token.ToString(CultureInfo.InvariantCulture))) {
                    Count(ref StaleRejected); return false;
                }
                if (Fields[0] == "entitydestroyed") {
                    try {
                        if (Value.Destroyed == null || !Value.Destroyed.Matches(Fields) ||
                            World.GameplayAvailable == null || !World.GameplayAvailable("entitydestroyed") ||
                            !Value.Destroyed.ValidateCompletion()) { Count(ref StaleRejected); return false; }
                    } catch (Exception) { Count(ref StaleRejected); return false; }
                }
                return true;
            }
            internal bool Release(FacadeSession Owner, ulong Nonce, bool Rejected = false)
            {
                World.Players.CheckOwner(); Reservation Value;
                if (!Held.TryGetValue(Nonce, out Value) || !Object.ReferenceEquals(Value.Owner, Owner)) return false;
                Held.Remove(Nonce); RetainedBytes -= Value.Charge; Count(ref Released);
                if (Rejected) Count(ref NativeRejected); return true;
            }
            internal void Cancel(FacadeSession Owner)
            {
                World.Players.CheckOwner();
                foreach (Reservation Value in Held.Values) if (Object.ReferenceEquals(Value.Owner, Owner)) Value.Cancelled = true;
            }
            internal void RejectQueue() { Count(ref QueueRejected); }
            internal void RejectTransfer() { Count(ref StaleRejected); }
            internal string Status
            {
                get { return "[CarbonLuau:Gameplay] pending=" + PendingCount + "; retained_transport=" + RetainedBytes +
                    "; accepted=" + Accepted + "; released=" + Released + "; no_subscribers=" + NoSubscribers +
                    "; producer_rejected=" + ProducerRejected + "; fanout_rejected=" + FanoutRejected +
                    "; scan_rejected=" + ScanRejected + "; queue_rejected=" + QueueRejected +
                    "; payload_rejected=" + PayloadRejected + "; native_rejected=" + NativeRejected + "; stale_rejected=" + StaleRejected +
                    "; destroy_position_accepted=" + DestroyPositionAccepted + "; destroy_position_rejected=" + DestroyPositionRejected; }
            }
        }
    }
}
