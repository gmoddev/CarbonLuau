using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class InventoryMutationDiagnostics
        {
            internal ulong Attempts, PrepareRejected, Verified, StaleRejected, GateBusyRejected;
            internal ulong HostExceptions, HostAmountMismatches, PhysicalDeltaMismatches, Indeterminate;
            internal static void Increment(ref ulong Value) { if (Value != UInt64.MaxValue) Value++; }
        }

        internal sealed class InventoryMutationGate
        {
            private readonly HashSet<string> Busy = new HashSet<string>(StringComparer.Ordinal);
            internal bool TryEnter(string ExactConnectionToken)
            { return Busy.Add(ExactConnectionToken); }
            internal void Exit(string ExactConnectionToken)
            {
                if (!Busy.Remove(ExactConnectionToken))
                    throw new InvalidOperationException("inventory mutation gate invariant failed");
            }
            internal int Count { get { return Busy.Count; } }
        }

        internal sealed class PlayerTakeItemOperation
        {
            private const string IndeterminateMessage =
                "TakeItem host failure after commit began; inventory state may have changed";
            private readonly InventoryMutationGate Gate = new InventoryMutationGate();
            internal InventoryMutationGate SharedGate { get { return Gate; } }
            internal readonly InventoryMutationDiagnostics Diagnostics = new InventoryMutationDiagnostics();
            internal int BusyCount { get { return Gate.Count; } }

            internal bool Execute(string ExactConnectionToken, Func<PlayerView> Resolve, object Definition, int Amount)
            {
                InventoryMutationDiagnostics.Increment(ref Diagnostics.Attempts);
                if (String.IsNullOrEmpty(ExactConnectionToken) || Resolve == null)
                    throw new FacadeException("invalid TakeItem operation");
                if (Amount < 1) throw new FacadeException("item amount must be an exact positive integer");
                if (!Gate.TryEnter(ExactConnectionToken)) {
                    InventoryMutationDiagnostics.Increment(ref Diagnostics.GateBusyRejected);
                    throw new FacadeException("Player inventory mutation is already in progress");
                }
                try {
                    PlayerView View = ResolveCurrent(Resolve, false);
                    if (Definition == null) {
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.PrepareRejected);
                        return false;
                    }
                    long Before = Count(View, Definition);
                    if (Before < Amount) {
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.PrepareRejected);
                        return false;
                    }

                    View = ResolveCurrent(Resolve, false);
                    if (View.TakeInventory == null) throw new FacadeException("Player inventory mutation is unavailable");
                    int HostReturned;
                    // COMMIT begins immediately before the exact qualified host call.
                    try { HostReturned = View.TakeInventory(Definition, Amount); }
                    catch {
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.HostExceptions);
                        throw Indeterminate();
                    }

                    long After;
                    try { After = Count(ResolveCurrent(Resolve, true), Definition); }
                    catch (FacadeException) { throw Indeterminate(); }
                    catch {
                        InventoryMutationDiagnostics.Increment(ref Diagnostics.HostExceptions);
                        throw Indeterminate();
                    }

                    long Delta;
                    try { Delta = checked(Before - After); }
                    catch (OverflowException) { throw Indeterminate(); }
                    bool HostMismatch = HostReturned != Amount;
                    bool DeltaMismatch = Delta != Amount;
                    if (HostMismatch) InventoryMutationDiagnostics.Increment(ref Diagnostics.HostAmountMismatches);
                    if (DeltaMismatch) InventoryMutationDiagnostics.Increment(ref Diagnostics.PhysicalDeltaMismatches);
                    if (HostMismatch || DeltaMismatch) throw Indeterminate();
                    InventoryMutationDiagnostics.Increment(ref Diagnostics.Verified);
                    return true;
                }
                finally { Gate.Exit(ExactConnectionToken); }
            }

            private PlayerView ResolveCurrent(Func<PlayerView> Resolve, bool AfterCommit)
            {
                PlayerView View;
                try { View = Resolve(); }
                catch {
                    if (AfterCommit) throw;
                    InventoryMutationDiagnostics.Increment(ref Diagnostics.StaleRejected);
                    throw new FacadeException("Player is no longer connected");
                }
                if (View != null) return View;
                InventoryMutationDiagnostics.Increment(ref Diagnostics.StaleRejected);
                if (AfterCommit) throw new FacadeException(IndeterminateMessage);
                throw new FacadeException("Player is no longer connected");
            }

            private static long Count(PlayerView View, object Definition)
            {
                if (View.Inventory == null) throw new FacadeException("Player inventory is unavailable");
                return PhysicalInventoryObservation.CountForMutation(View.Inventory(), Definition);
            }

            private FacadeException Indeterminate()
            {
                InventoryMutationDiagnostics.Increment(ref Diagnostics.Indeterminate);
                return new FacadeException(IndeterminateMessage);
            }
        }
    }
}
