using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Shared C2 sampled substrate. C3 owns public bindings/deferred transport;
        // this source never enters Luau or attributes completed inventory operations.
        internal static class InventoryNetObservationPolicy
        {
            internal const int Players = 64, Demands = 128, ScansPerFrame = 4;
            internal const int Pending = 256, RecordsPerFrame = 32;
            internal const int SnapshotCharge = 128 * 320 + 128;
            internal const int RetainedChargeCeiling = 4 * 1024 * 1024;
        }
        internal sealed class InventoryNetObservation : IDisposable
        {
            private sealed class Watch
            {
                internal string Token, UserId;
                internal InventoryQuantitySnapshot Baseline;
                internal int FanoutCursor;
            }
            private sealed class Demand
            {
                internal ulong Id;
                internal int Slot;
                internal FacadeSession Owner;
                internal FacadeSession.PublicationWitness Publication;
                internal bool Ready;
                internal Func<bool> Eligible;
            }
            private struct PendingRecord
            {
                internal ulong Demand, Sequence;
                internal InventoryQuantityChange Change;
            }
            private readonly FacadeWorld World;
            private readonly Watch[] Watches = new Watch[InventoryNetObservationPolicy.Players];
            private readonly Demand[] Demands = new Demand[InventoryNetObservationPolicy.Demands];
            private readonly PendingRecord[] Pending = new PendingRecord[InventoryNetObservationPolicy.Pending];
            private ulong NextDemand, NextSequence;
            private int Cursor;
            private long Frame = Int64.MinValue;
            private bool Closed;
            // Bounded qualification seam; null in production. Not a host callback.
            internal Func<bool> AllocationAdmission;
            internal Func<bool> PumpAvailable;
            internal ulong Scans, InvalidSnapshots, Dropped, Baselines;
            internal int WatchCount { get; private set; }
            internal int DemandCount { get; private set; }
            private int BufferedCount, DeliveryCount;
            internal int PendingCount { get { return BufferedCount + DeliveryCount; } }
            internal int LastScans { get; private set; }
            internal string Status { get { return "; inventory_watches=" + WatchCount + "; inventory_demands=" + DemandCount +
                "; inventory_pending=" + PendingCount + "; inventory_charge=" + ChargedBytes + "; inventory_scans=" + Scans +
                "; inventory_invalid=" + InvalidSnapshots + "; inventory_dropped=" + Dropped; } }
            internal int ChargedBytes { get { return WatchCount * InventoryNetObservationPolicy.SnapshotCharge +
                DemandCount * 256 + PendingCount * 2048 + InventoryNetObservationPolicy.SnapshotCharge; } }
            internal InventoryNetObservation(FacadeWorld World) { this.World = World; }
            private static void Count(ref ulong Value) { if (Value != UInt64.MaxValue) Value++; }
            private static void Count(ref ulong Value, int Amount)
            {
                ulong Increase = checked((ulong)Amount);
                Value = Value > UInt64.MaxValue - Increase ? UInt64.MaxValue : Value + Increase;
            }
            private void Check()
            { World.Players.CheckOwner(); if (Closed) throw new FacadeException("inventory observation retired"); }
            private void Allocate()
            { if (AllocationAdmission != null && !AllocationAdmission()) throw new OutOfMemoryException("inventory qualification allocation"); }

            internal ulong Stage(FacadeSession Owner, string Token, string UserId, Func<bool> Eligible = null)
            {
                Check();
                if (PumpAvailable != null && !PumpAvailable()) throw new FacadeException("inventory observation pump unavailable");
                PruneRetired();
                if (Owner == null || Owner.Disposed || Owner.InventoryWorld != World ||
                    World.Players.Resolve(Token, UserId) == null) throw new FacadeException("stale inventory observation owner");
                if (World.Items.Describe == null) throw new FacadeException("inventory observation unavailable");
                if (NextDemand == UInt64.MaxValue) throw new FacadeException("inventory observation identity exhausted");
                int Free = -1, Slot = -1, Empty = -1;
                for (int Index = 0; Index < Demands.Length; Index++) if (Demands[Index] == null && Free < 0) Free = Index;
                for (int Index = 0; Index < Watches.Length; Index++) {
                    Watch Value = Watches[Index];
                    if (Value == null) { if (Empty < 0) Empty = Index; }
                    else if (Value.Token == Token && Value.UserId == UserId) Slot = Index;
                }
                if (Free < 0 || (Slot < 0 && Empty < 0)) throw new FacadeException("inventory observation capacity exceeded");
                Allocate();
                var ValueDemand = new Demand { Id = NextDemand + 1, Owner = Owner,
                    Publication = Owner.CapturePublicationWitness(), Slot = Slot < 0 ? Empty : Slot, Eligible = Eligible };
                Watch NewWatch = Slot < 0 ? new Watch { Token = Token, UserId = UserId } : null;
                // All allocations/preflight precede publication into fixed storage.
                if (NewWatch != null) { Watches[Empty] = NewWatch; WatchCount++; }
                Demands[Free] = ValueDemand; DemandCount++; NextDemand++;
                return ValueDemand.Id;
            }
            private bool Current(Demand Value)
            { return Value.Owner.IsPublicationWitnessCurrent(Value.Publication); }
            private bool Published(Demand Value)
            { return Value.Owner.InventoryPublicationPublished(Value.Publication) && (Value.Eligible == null || Value.Eligible()); }
            private void Remove(int Index)
            {
                Demand Value = Demands[Index]; if (Value == null) return;
                Demands[Index] = null; DemandCount--;
                for (int Next = 0; Next < Pending.Length; Next++) if (Pending[Next].Demand == Value.Id) {
                    Pending[Next] = default(PendingRecord); BufferedCount--;
                }
                for (int Next = 0; Next < Demands.Length; Next++)
                    if (Demands[Next] != null && Demands[Next].Slot == Value.Slot) return;
                Watches[Value.Slot] = null; WatchCount--;
            }
            internal void Cancel(ulong Id)
            { Check(); for (int Index = 0; Index < Demands.Length; Index++) if (Demands[Index] != null && Demands[Index].Id == Id) Remove(Index); }
            internal void Retire(FacadeSession Owner)
            {
                World.Players.CheckOwner();
                if (Closed) return;
                for (int Index = 0; Index < Demands.Length; Index++)
                    if (Demands[Index] != null && ReferenceEquals(Demands[Index].Owner, Owner)) Remove(Index);
            }
            internal void PruneRetired()
            {
                World.Players.CheckOwner(); if (Closed) return;
                for (int Index = 0; Index < Demands.Length; Index++)
                    if (Demands[Index] != null && !Current(Demands[Index])) Remove(Index);
            }
            // One existing owner-frame pump, never a timer per Player or Luau entry.
            internal void Tick(long CurrentFrame)
            {
                Check(); if (Frame == CurrentFrame) return;
                Frame = CurrentFrame; LastScans = 0; int Produced = 0;
                PruneRetired();
                for (int Visit = 0; Visit < Watches.Length && LastScans < InventoryNetObservationPolicy.ScansPerFrame; Visit++) {
                    int Slot = Cursor; Cursor = (Cursor + 1) % Watches.Length;
                    Watch Value = Watches[Slot]; if (Value == null) continue;
                    bool Active = false;
                    for (int Index = 0; Index < Demands.Length; Index++) if (Demands[Index] != null &&
                        Demands[Index].Slot == Slot && Published(Demands[Index])) Active = true;
                    if (!Active) continue;
                    // Charge the complete D18 inspection before any host lookup/read.
                    if (!World.GameplayEvents.InventoryInspection()) { Cursor = Slot; break; }
                    LastScans++; Count(ref Scans);
                    try {
                        PlayerView Player = World.Players.Resolve(Value.Token, Value.UserId);
                        if (Player == null) {
                            for (int Index = 0; Index < Demands.Length; Index++)
                                if (Demands[Index] != null && Demands[Index].Slot == Slot) Remove(Index);
                            continue;
                        }
                        Allocate();
                        InventoryQuantitySnapshot After = PhysicalInventoryObservation.Snapshot(Player.Inventory(), World.Items.Describe, World.Items.Resolve);
                        if (World.Players.Resolve(Value.Token, Value.UserId) == null) throw new FacadeException("stale inventory sample");
                        InventoryQuantitySnapshot Before = Value.Baseline;
                        Value.Baseline = After; // Always advance; queue loss is never replayed.
                        if (Before == null) {
                            Count(ref Baselines);
                            for (int Index = 0; Index < Demands.Length; Index++) if (Demands[Index] != null &&
                                Demands[Index].Slot == Slot && Published(Demands[Index])) Demands[Index].Ready = true;
                            continue;
                        }
                        Allocate();
                        InventoryQuantityChange[] Changes = InventoryQuantitySnapshot.Difference(Before, After);
                        // Establish new-demand cutoffs even when the shared work
                        // budget stops record fanout below.
                        var Eligible = new bool[Demands.Length];
                        int EligibleCount = 0;
                        for (int Index = 0; Index < Demands.Length; Index++) {
                            Demand Target = Demands[Index];
                            if (Target == null || Target.Slot != Slot || !Published(Target)) continue;
                            Eligible[Index] = Target.Ready;
                            if (Target.Ready) EligibleCount++;
                            Target.Ready = true;
                        }
                        int Start = Value.FanoutCursor;
                        bool Exhausted = false;
                        int Attempted = 0;
                        for (int VisitDemand = 0; VisitDemand < Demands.Length; VisitDemand++) {
                            int Index = (Start + VisitDemand) % Demands.Length;
                            Demand Target = Demands[Index];
                            if (!Eligible[Index]) continue;
                            foreach (InventoryQuantityChange Change in Changes) {
                                if (Produced >= InventoryNetObservationPolicy.RecordsPerFrame || PendingCount == Pending.Length || NextSequence == UInt64.MaxValue) {
                                    Exhausted = true; break;
                                }
                                Attempted++;
                                if (!World.GameplayEvents.Visit()) { Count(ref Dropped); Exhausted = true; break; }
                                Allocate();
                                for (int Free = 0; Free < Pending.Length; Free++) if (Pending[Free].Demand == 0) {
                                    Pending[Free] = new PendingRecord { Demand = Target.Id, Sequence = ++NextSequence, Change = Change };
                                    BufferedCount++; Produced++; Value.FanoutCursor = (Index + 1) % Demands.Length; break;
                                }
                                if (Produced >= InventoryNetObservationPolicy.RecordsPerFrame || PendingCount == Pending.Length) {
                                    Exhausted = true; break;
                                }
                            }
                            if (Exhausted) break;
                        }
                        if (Exhausted) {
                            Count(ref Dropped, EligibleCount * Changes.Length - Attempted);
                            // Leave Cursor immediately after the budget-consuming
                            // watch. Completing an empty-slot sweep would restore
                            // the same first busy Player and starve later watches.
                            return;
                        }
                    } catch (Exception) {
                        // No fabricated loss/gain bridges an invalid/failed interval.
                        Value.Baseline = null; Count(ref InvalidSnapshots);
                    }
                }
            }
            // Private consumption only. C3 must add D23 deferred callback transport,
            // public staging/cutoffs and fresh native admission separately.
            internal bool Consume(ulong Id, out InventoryQuantityChange Change, bool RetainDelivery = false)
            {
                Check(); Change = default(InventoryQuantityChange);
                if (Id == 0) return false;
                Demand Value = null;
                for (int Index = 0; Index < Demands.Length; Index++) if (Demands[Index] != null && Demands[Index].Id == Id) Value = Demands[Index];
                int First = -1;
                for (int Index = 0; Index < Pending.Length; Index++) if (Pending[Index].Demand == Id &&
                    (First < 0 || Pending[Index].Sequence < Pending[First].Sequence)) First = Index;
                if (First < 0) return false;
                PendingRecord Record = Pending[First]; Pending[First] = default(PendingRecord); BufferedCount--;
                if (Value == null || !Published(Value)) return false;
                Watch WatchValue = Watches[Value.Slot];
                if (WatchValue == null || World.Players.Resolve(WatchValue.Token, WatchValue.UserId) == null) return false;
                if (RetainDelivery) DeliveryCount++;
                Change = Record.Change; return true;
            }
            internal void ReleaseDelivery()
            {
                World.Players.CheckOwner();
                if (DeliveryCount <= 0) throw new FacadeException("invalid inventory observation release");
                DeliveryCount--;
            }
            public void Dispose()
            {
                World.Players.CheckOwner(); if (Closed) return; Closed = true;
                Array.Clear(Watches, 0, Watches.Length); Array.Clear(Demands, 0, Demands.Length); Array.Clear(Pending, 0, Pending.Length);
                WatchCount = DemandCount = BufferedCount = 0; AllocationAdmission = null;
                PumpAvailable = null;
            }
        }
        public sealed partial class FacadeSession
        {
            internal FacadeWorld InventoryWorld { get { return World; } }
            internal bool InventoryPublicationPublished(PublicationWitness Witness)
            {
                if (!Active || !World.IsActive(this) || !IsPublicationWitnessCurrent(Witness)) return false;
                for (PublicationWitness Current = Witness; Current != null; Current = Current.Parent)
                    foreach (PublicationCheckpoint Checkpoint in Publications)
                        if (ReferenceEquals(Checkpoint.Witness, Current)) return false;
                return true;
            }
        }
    }
}
