using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Managed-only adaptation records. No host reference crosses the facade ABI.
        public sealed class InventoryPlacementChunk
        {
            public readonly PhysicalInventoryContainer Container;
            public readonly int Slot, Amount;
            public readonly object MergeTarget;
            public InventoryPlacementChunk(PhysicalInventoryContainer Container, int Slot, int Amount, object MergeTarget)
            { this.Container = Container; this.Slot = Slot; this.Amount = Amount; this.MergeTarget = MergeTarget; }
        }
        public enum InventoryResourceState { Unexpected, Temporary, Accepted, Consumed }
        public struct InventoryResourceObservation
        {
            public InventoryResourceState State;
            public ulong Identity;
            public int Amount;
        }
        public interface IInventoryGrantHost
        {
            // PREPARE: direct observations only, no acceptance callbacks.
            int StackLimit(object Definition, PhysicalInventoryContainer Container);
            int MergeSpace(object Definition, PhysicalInventoryStack Stack, PhysicalInventoryContainer Container);
            bool CanPlace(object Definition, PhysicalInventoryContainer Container, int Slot);
            object Create(object Definition, int Amount);
            bool Transfer(object Item, object Definition, InventoryPlacementChunk Chunk);
            InventoryResourceObservation Observe(object Item);
            void Cleanup(object Item);
        }

        internal sealed class InventoryPlacementPlan
        {
            internal readonly List<InventoryPlacementChunk> Chunks = new List<InventoryPlacementChunk>(FacadePolicy.InventoryStacks);
            internal long Before;
            internal static InventoryPlacementPlan Prepare(PhysicalInventorySource Source, IInventoryGrantHost Host, object Definition, int Amount)
            {
                PhysicalInventoryObservation.ValidateMutationCapacity(Source);
                var Result = new InventoryPlacementPlan();
                // Freeze the same Player-1C records; counting and planning share this snapshot.
                var Containers = new[] { Source.Main, Source.Belt, Source.Wear };
                var Copies = new PhysicalInventoryContainer[3];
                var Slots = new PhysicalInventoryStack[3][];
                for (int C = 0; C < Containers.Length; ++C) {
                    PhysicalInventoryContainer Container = Containers[C];
                    if (Container.Identity == null || Container.Read == null) throw new FacadeException("Player inventory state is invalid");
                    for (int Prior = 0; Prior < C; ++Prior)
                        if (Object.ReferenceEquals(Container.Identity, Containers[Prior].Identity)) throw new FacadeException("Player inventory containers overlap");
                    var Entries = new PhysicalInventoryStack[Container.StackCount];
                    var Occupied = new PhysicalInventoryStack[Container.Capacity];
                    for (int Index = 0; Index < Entries.Length; ++Index) {
                        PhysicalInventoryStack Stack = Container.Read(Index); Entries[Index] = Stack;
                        if (Stack.Identity == null || Stack.Position < 0 || Stack.Position >= Container.Capacity ||
                            Occupied[Stack.Position].Identity != null || !Object.ReferenceEquals(Stack.Parent, Container.Identity))
                            throw new FacadeException("Player inventory slot state is invalid");
                        Occupied[Stack.Position] = Stack;
                    }
                    Slots[C] = Occupied;
                    Copies[C] = new PhysicalInventoryContainer { Identity = Container.Identity, Capacity = Container.Capacity,
                        StackCount = Entries.Length, Read = Index => Entries[Index] };
                }
                Result.Before = PhysicalInventoryObservation.CountForMutation(new PhysicalInventorySource {
                    Main = Copies[0], Belt = Copies[1], Wear = Copies[2] }, Definition);
                int Remaining = Amount;
                // All compatible merges first, then empty slots; stable main/belt/wear + slot order.
                for (int Pass = 0; Pass < 2 && Remaining > 0; ++Pass)
                    for (int C = 0; C < Containers.Length && Remaining > 0; ++C)
                        for (int Slot = 0; Slot < Slots[C].Length && Remaining > 0; ++Slot) {
                            PhysicalInventoryStack Stack = Slots[C][Slot];
                            bool Merge = Stack.Identity != null;
                            if ((Pass == 0) != Merge || !Host.CanPlace(Definition, Containers[C], Slot)) continue;
                            if (Merge && (!Stack.Valid || Stack.Amount <= 0 || !Object.ReferenceEquals(Stack.Definition, Definition))) continue;
                            int Limit = Host.StackLimit(Definition, Containers[C]);
                            int Space = Merge ? Math.Min(Limit, Host.MergeSpace(Definition, Stack, Containers[C])) : Limit;
                            if (Space <= 0) continue;
                            int Chunk = Math.Min(Space, Remaining);
                            if (Result.Chunks.Count >= FacadePolicy.InventoryStacks) throw new FacadeException("inventory plan exceeds 128-chunk bound");
                            Result.Chunks.Add(new InventoryPlacementChunk(Containers[C], Slot, Chunk, Stack.Identity));
                            Remaining -= Chunk;
                        }
                return Remaining == 0 ? Result : null;
            }
        }

        internal sealed class PlayerGiveItemOperation
        {
            private const string Failure = "GiveItem host failure after commit began; inventory state may have changed";
            private readonly InventoryMutationGate Gate;
            internal readonly InventoryMutationDiagnostics Diagnostics = new InventoryMutationDiagnostics();
            internal ulong CleanupFailures, UnaccountedResources;
            internal int TrackedResources { get; private set; }
            internal int BusyCount { get { return Gate.Count; } }
            internal PlayerGiveItemOperation(InventoryMutationGate Gate) { this.Gate = Gate; }
            internal bool Execute(string Token, Func<PlayerView> Resolve, object Definition, int Amount)
            {
                InventoryMutationDiagnostics.Increment(ref Diagnostics.Attempts);
                if (String.IsNullOrEmpty(Token) || Resolve == null || Amount < 1) throw new FacadeException("invalid GiveItem operation");
                if (!Gate.TryEnter(Token)) {
                    InventoryMutationDiagnostics.Increment(ref Diagnostics.GateBusyRejected);
                    throw new FacadeException("Player inventory mutation is already in progress");
                }
                object[] Returned = null;
                ulong[] Identities = null;
                bool[] Transferred = null;
                IInventoryGrantHost Host = null;
                int Created = 0;
                bool Verified = false;
                try {
                    PlayerView View = Resolve();
                    if (View == null) throw new FacadeException("Player is no longer connected");
                    if (Definition == null) throw new FacadeException("unknown item short name");
                    Host = View.GiveInventory;
                    if (Host == null || View.Inventory == null) throw new FacadeException("Player inventory mutation is unavailable");
                    InventoryPlacementPlan Plan = InventoryPlacementPlan.Prepare(View.Inventory(), Host, Definition, Amount);
                    if (Plan == null) { InventoryMutationDiagnostics.Increment(ref Diagnostics.PrepareRejected); return false; }
                    Returned = new object[Plan.Chunks.Count]; Identities = new ulong[Returned.Length]; Transferred = new bool[Returned.Length];
                    if (Resolve() == null) throw new FacadeException("Player is no longer connected");
                    // COMMIT: no false result is possible beyond this first Create boundary.
                    try {
                        for (int Index = 0; Index < Plan.Chunks.Count; ++Index) {
                            InventoryPlacementChunk Chunk = Plan.Chunks[Index];
                            object Value = Host.Create(Definition, Chunk.Amount);
                            Returned[Index] = Value; Created = Index + 1;
                            if (Value == null) throw new InvalidOperationException();
                            TrackedResources++;
                            InventoryResourceObservation State = Host.Observe(Value);
                            Identities[Index] = State.Identity;
                            if (State.State != InventoryResourceState.Temporary || State.Amount != Chunk.Amount || State.Identity == 0 || Resolve() == null)
                                throw new InvalidOperationException();
                            Transferred[Index] = Host.Transfer(Value, Definition, Chunk);
                            State = Host.Observe(Value);
                            if (!Transferred[Index] || !Terminal(State, Identities[Index])) throw new InvalidOperationException();
                        }
                        PlayerView Current = Resolve();
                        if (Current == null || Current.Inventory == null) throw new InvalidOperationException();
                        long After = PhysicalInventoryObservation.CountForMutation(Current.Inventory(), Definition);
                        for (int Index = 0; Index < Created; ++Index)
                            if (!Transferred[Index] || !Terminal(Host.Observe(Returned[Index]), Identities[Index])) throw new InvalidOperationException();
                        if (After < checked(Plan.Before + Amount)) {
                            InventoryMutationDiagnostics.Increment(ref Diagnostics.PhysicalDeltaMismatches);
                            throw new InvalidOperationException();
                        }
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.Verified);
                        Verified = true;
                        return true;
                    }
                    catch {
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.HostExceptions);
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.Indeterminate);
                        throw new FacadeException(Failure);
                    }
                }
                finally {
                    // Classification on exceptional insertion is mandatory. Never remove accepted items.
                    for (int Index = 0; Index < Created; ++Index) {
                        object Value = Returned[Index];
                        if (Value == null) continue;
                        try {
                            // VERIFY already classified every resource. Do not run additional host
                            // observations after reporting success; simply release borrowed references.
                            if (Verified) continue;
                            InventoryResourceObservation State = Host.Observe(Value);
                            bool Same = Identities[Index] != 0 && State.Identity == Identities[Index];
                            if ((Same || Identities[Index] == 0) && State.State == InventoryResourceState.Temporary) {
                                Host.Cleanup(Value);
                                State = Host.Observe(Value);
                                if (State.State != InventoryResourceState.Consumed || (Same && State.Identity != Identities[Index]))
                                    InventoryMutationDiagnostics.Increment(ref CleanupFailures);
                            } else if (!Same || !Terminal(State, Identities[Index]) ||
                                (State.State == InventoryResourceState.Consumed && !Transferred[Index]))
                                InventoryMutationDiagnostics.Increment(ref UnaccountedResources);
                        }
                        catch { InventoryMutationDiagnostics.Increment(ref CleanupFailures); }
                        finally { Returned[Index] = null; TrackedResources--; }
                    }
                    Gate.Exit(Token);
                }
            }
            private static bool Terminal(InventoryResourceObservation State, ulong Identity)
            { return State.Identity == Identity && (State.State == InventoryResourceState.Accepted || State.State == InventoryResourceState.Consumed); }
        }
    }
}
