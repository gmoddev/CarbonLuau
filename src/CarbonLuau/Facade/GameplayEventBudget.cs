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
            internal static bool Kind(string Value) { return Value == "died" || Value == "spawned"; }
            internal static bool Identity(string Text, out ulong Value)
            {
                Value = 0;
                return Text != null && Text.Length >= 1 && Text.Length <= 20 && Text[0] != '0' &&
                    UInt64.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value) && Value != 0 &&
                    Text == Value.ToString(CultureInfo.InvariantCulture);
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
            }
            private readonly FacadeWorld World;
            private readonly Dictionary<ulong, Reservation> Held = new Dictionary<ulong, Reservation>(GameplayEventPolicy.Reservations);
            private readonly Dictionary<long, int> DomainDeliveries = new Dictionary<long, int>(129);
            internal Func<long> FrameClock = () => 0;
            private long Frame = Int64.MinValue;
            private int Captures, Deliveries, Visits;
            private ulong Next;
            internal int RetainedBytes { get; private set; }
            internal int PendingCount { get { return Held.Count; } }
            internal ulong Accepted, Released, NoSubscribers, ProducerRejected, FanoutRejected, ScanRejected;
            internal ulong QueueRejected, PayloadRejected, NativeRejected, StaleRejected;

            internal GameplayEventBudget(FacadeWorld World) { this.World = World; }
            private static void Count(ref ulong Value) { if (Value != UInt64.MaxValue) ++Value; }
            private void BeginFrame()
            {
                World.Players.CheckOwner();
                long Current = FrameClock();
                if (Current == Frame) return;
                Frame = Current; Captures = Deliveries = Visits = 0; DomainDeliveries.Clear();
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
            internal ulong Reserve(FacadeSession Owner, string Kind, string Listener, string[] Fields, out byte[] Payload)
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
                    Publication = Owner.RootGameplayPublication, Charge = Charge});
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
                ulong Nonce; Reservation Value;
                if (Fields.Length != 12 || !GameplayEventPolicy.Identity(Fields[5], out Nonce) ||
                    !Held.TryGetValue(Nonce, out Value) || !Object.ReferenceEquals(Value.Owner, Owner) || !Value.Native || Value.Cancelled ||
                    Value.Kind != Fields[0] || Value.Listener != Fields[1] || !Owner.IsGameplayPublicationCurrent(Value.Publication)) {
                    Count(ref StaleRejected); return false;
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
            internal string Status
            {
                get { return "[CarbonLuau:Gameplay] pending=" + PendingCount + "; retained_transport=" + RetainedBytes +
                    "; accepted=" + Accepted + "; released=" + Released + "; no_subscribers=" + NoSubscribers +
                    "; producer_rejected=" + ProducerRejected + "; fanout_rejected=" + FanoutRejected +
                    "; scan_rejected=" + ScanRejected + "; queue_rejected=" + QueueRejected +
                    "; payload_rejected=" + PayloadRejected + "; native_rejected=" + NativeRejected + "; stale_rejected=" + StaleRejected; }
            }
        }
    }
}
