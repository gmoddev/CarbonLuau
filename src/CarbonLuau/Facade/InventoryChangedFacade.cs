using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class InventoryChangedObservation
        {
            internal readonly FacadeSession Owner;
            internal readonly ulong Listener, Demand;
            internal readonly string Token, UserId, FacadePublication;
            internal readonly InventoryQuantityChange Change;
            internal InventoryChangedObservation(FacadeSession Owner, ulong Listener, ulong Demand, string Token,
                string UserId, string FacadePublication, InventoryQuantityChange Change)
            { this.Owner = Owner; this.Listener = Listener; this.Demand = Demand; this.Token = Token;
                this.UserId = UserId; this.FacadePublication = FacadePublication; this.Change = Change; }
            internal bool Matches(string[] Fields)
            {
                return Fields.Length == 12 && Fields[2] == Token && Fields[3] == UserId && Fields[4] == Change.Item &&
                    Fields[6] == Change.Before.ToString(CultureInfo.InvariantCulture) &&
                    Fields[7] == Change.After.ToString(CultureInfo.InvariantCulture) &&
                    Fields[8] == Change.Delta.ToString(CultureInfo.InvariantCulture) &&
                    Fields[9] == Demand.ToString(CultureInfo.InvariantCulture) && Fields[10] == FacadePublication && Fields[11] == "";
            }
        }
        public sealed partial class FacadeWorld
        {
            private long InventoryDeliveryCursor;
            private long InventoryDeliveryFrame = Int64.MinValue;
            internal void FlushInventoryChanges()
            {
                Players.CheckOwner();
                long Frame = GameplayEvents.FrameClock();
                if (Frame == InventoryDeliveryFrame) return;
                InventoryDeliveryFrame = Frame;
                var Values = Sessions();
                Values.Sort((Left, Right) => Left.DomainLifetimeId.CompareTo(Right.DomainLifetimeId));
                int Start = Values.FindIndex(Value => Value.DomainLifetimeId > InventoryDeliveryCursor);
                if (Start < 0) Start = 0;
                int Taken = 0;
                for (int Offset = 0; Offset < Values.Count && Taken < InventoryNetObservationPolicy.RecordsPerFrame; Offset++) {
                    if (!GameplayEvents.Visit()) break;
                    FacadeSession Value = Values[(Start + Offset) % Values.Count];
                    int Used = Value.FlushInventoryChanges(InventoryNetObservationPolicy.RecordsPerFrame - Taken);
                    Taken += Used;
                    if (Used != 0) InventoryDeliveryCursor = Value.DomainLifetimeId;
                }
            }
        }
        public sealed partial class FacadeSession
        {
            private sealed class InventoryListener
            {
                internal ulong Id, Demand;
                internal string Token, UserId, FacadePublication;
            }
            private readonly SortedDictionary<ulong, InventoryListener> InventoryListeners = new SortedDictionary<ulong, InventoryListener>();
            private ulong InventoryListenerCursor;
            private bool InventoryListenerPublished(InventoryListener Value)
            {
                string Kind;
                var Published = FirstPublication == null ? Listeners : FirstPublication.Listeners;
                ulong Publication;
                return !Disposed && Active && World.IsActive(this) &&
                    Published.TryGetValue(Value.Id, out Kind) && Kind == "itemchanged" &&
                    GameplayEventPolicy.Identity(Value.FacadePublication, out Publication) && FindEntityWitness(Publication) != null;
            }
            internal bool ValidateInventoryChanged(InventoryChangedObservation Value)
            {
                InventoryListener Listener;
                return Value != null && ReferenceEquals(Value.Owner, this) && InventoryListeners.TryGetValue(Value.Listener, out Listener) &&
                    Listener.Demand == Value.Demand && Listener.Token == Value.Token && Listener.UserId == Value.UserId &&
                    Listener.FacadePublication == Value.FacadePublication && InventoryListenerPublished(Listener) &&
                    World.Players.Resolve(Value.Token, Value.UserId) != null;
            }
            private string[] RegisterInventoryListener(string[] Fields)
            {
                ulong Publication;
                if (Fields.Length == 4) FacadePolicy.UserId(Fields[2]);
                if (Fields.Length != 4 || !GameplayEventPolicy.Identity(Fields[1], out Publication) ||
                    !GameplayEventPolicy.Identity(Fields[3], out Publication) || FindEntityWitness(Publication) == null ||
                    World.Players.Resolve(Fields[1], Fields[2]) == null)
                    throw new FacadeException("stale PlayerInventory signal");
                if (Listeners.Count >= FacadePolicy.Listeners || InventoryListeners.Count >= InventoryNetObservationPolicy.Demands)
                    throw new FacadeException("inventory signal listener limit reached");
                ulong IdValue = UInt64.Parse(Id(), CultureInfo.InvariantCulture);
                var Listener = new InventoryListener { Id = IdValue, Token = Fields[1], UserId = Fields[2], FacadePublication = Fields[3] };
                ulong Demand = World.InventorySamples.Stage(this, Listener.Token, Listener.UserId, () => InventoryListenerPublished(Listener));
                Listener.Demand = Demand;
                try { InventoryListeners.Add(IdValue, Listener); Listeners.Add(IdValue, "itemchanged"); }
                catch { InventoryListeners.Remove(IdValue); Listeners.Remove(IdValue); World.InventorySamples.Cancel(Demand); throw; }
                RefreshGameplayListeners(); return new[] {IdValue.ToString(CultureInfo.InvariantCulture)};
            }
            private void PruneInventoryListeners()
            {
                var Removed = new List<ulong>();
                foreach (var Entry in InventoryListeners) {
                    bool Keep = Listeners.ContainsKey(Entry.Key);
                    foreach (PublicationCheckpoint Checkpoint in Publications)
                        if (Checkpoint.Listeners.ContainsKey(Entry.Key)) Keep = true;
                    if (!Keep) { World.InventorySamples.Cancel(Entry.Value.Demand); Removed.Add(Entry.Key); }
                }
                foreach (ulong IdValue in Removed) InventoryListeners.Remove(IdValue);
            }
            internal int FlushInventoryChanges(int Maximum)
            {
                if (!Active || Disposed || !World.IsActive(this)) return 0;
                PruneInventoryListeners();
                var Values = new List<InventoryListener>(InventoryListeners.Values);
                int Start = Values.FindIndex(Value => Value.Id > InventoryListenerCursor);
                if (Start < 0) Start = 0;
                int Taken = 0;
                for (int Offset = 0; Offset < Values.Count && Taken < Maximum; Offset++) {
                    if (!World.GameplayEvents.Visit()) break;
                    InventoryListener Listener = Values[(Start + Offset) % Values.Count];
                    if (!InventoryListenerPublished(Listener)) continue;
                    while (Taken < Maximum) {
                        if (!World.GameplayEvents.Visit()) return Taken;
                        InventoryQuantityChange Change;
                        if (!World.InventorySamples.Consume(Listener.Demand, out Change, true)) break;
                        Taken++; InventoryListenerCursor = Listener.Id;
                        bool Retained = false;
                        try {
                            if (Pending.Count >= Capacity) { World.GameplayEvents.RejectQueue(); continue; }
                            if (!World.GameplayEvents.Capture("itemchanged") || !World.GameplayEvents.Delivery(this)) continue;
                            var Observation = new InventoryChangedObservation(this, Listener.Id, Listener.Demand, Listener.Token,
                                Listener.UserId, Listener.FacadePublication, Change);
                            string[] Fields = {"itemchanged", Listener.Id.ToString(CultureInfo.InvariantCulture), Listener.Token, Listener.UserId,
                                Change.Item, "", Change.Before.ToString(CultureInfo.InvariantCulture), Change.After.ToString(CultureInfo.InvariantCulture),
                                Change.Delta.ToString(CultureInfo.InvariantCulture), Listener.Demand.ToString(CultureInfo.InvariantCulture), Listener.FacadePublication, ""};
                            byte[] Payload;
                            ulong Nonce = World.GameplayEvents.Reserve(this, "itemchanged", Fields[1], Fields, out Payload, Inventory: Observation);
                            if (Nonce == 0) continue;
                            Retained = true;
                            try { Pending.Enqueue(new FacadePendingEvent(Payload, Nonce)); }
                            catch { World.GameplayEvents.Release(this, Nonce, true); throw; }
                        } finally { if (!Retained) World.InventorySamples.ReleaseDelivery(); }
                    }
                }
                return Taken;
            }
        }
    }
}
