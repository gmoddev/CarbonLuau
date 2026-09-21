using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class PlayerInteractionFoundation1FATests
{
    private sealed class Fixture
    {
        internal readonly object Definition = new object();
        internal readonly object OtherDefinition = new object();
        internal readonly object MainId = new object(), BeltId = new object(), WearId = new object();
        internal readonly List<Runtime.PhysicalInventoryStack> Main = new List<Runtime.PhysicalInventoryStack>();
        internal readonly List<Runtime.PhysicalInventoryStack> Belt = new List<Runtime.PhysicalInventoryStack>();
        internal readonly List<Runtime.PhysicalInventoryStack> Wear = new List<Runtime.PhysicalInventoryStack>();
        internal bool Current = true, ThrowDuringCommit, AddMatchingDuringCommit, MutateUnrelated;
        internal int HostAdjustment, ExtraPhysicalDelta;

        internal Runtime.PhysicalInventorySource Source()
        {
            return new Runtime.PhysicalInventorySource {
                Main = Container(MainId, Main, 64), Belt = Container(BeltId, Belt, 32), Wear = Container(WearId, Wear, 32)};
        }
        internal Runtime.PlayerView View()
        {
            return new Runtime.PlayerView {Identity = this, Connection = this, Connected = true,
                Inventory = Source, TakeInventory = Take};
        }
        internal void Add(List<Runtime.PhysicalInventoryStack> Target, object Parent, int Amount)
        { Target.Add(new Runtime.PhysicalInventoryStack(Parent, Definition, Amount, true)); }
        internal int Take(object Expected, int Amount)
        {
            if (ThrowDuringCommit) throw new InvalidOperationException("controlled host failure");
            int Remaining = Amount + ExtraPhysicalDelta;
            Remove(Main, MainId, ref Remaining); Remove(Belt, BeltId, ref Remaining); Remove(Wear, WearId, ref Remaining);
            if (AddMatchingDuringCommit)
                Main.Add(new Runtime.PhysicalInventoryStack(MainId, Definition, 1, true));
            if (MutateUnrelated)
                Main.Add(new Runtime.PhysicalInventoryStack(MainId, OtherDefinition, 17, true));
            return Amount + HostAdjustment;
        }
        private void Remove(List<Runtime.PhysicalInventoryStack> Values, object Parent, ref int Remaining)
        {
            for (int Index = 0; Index < Values.Count && Remaining > 0; ++Index) {
                Runtime.PhysicalInventoryStack Value = Values[Index];
                if (!Value.Valid || !Object.ReferenceEquals(Value.Parent, Parent) ||
                    !Object.ReferenceEquals(Value.Definition, Definition) || Value.Amount <= 0) continue;
                int Used = (int)Math.Min(Value.Amount, Remaining);
                Remaining -= Used;
                long Left = Value.Amount - Used;
                Values[Index] = new Runtime.PhysicalInventoryStack(Parent, Definition, Left, Left > 0);
            }
        }
        private static Runtime.PhysicalInventoryContainer Container(object Identity,
            List<Runtime.PhysicalInventoryStack> Values, int Capacity)
        {
            return new Runtime.PhysicalInventoryContainer {Identity = Identity, StackCount = Values.Count,
                Capacity = Capacity, Read = Index => Values[Index]};
        }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Player-1F-A: " + Message); }
    private static string Reject(Action Action)
    {
        try { Action(); }
        catch (InvalidOperationException Error) { return Error.Message; }
        throw new Exception("Player-1F-A: expected rejection");
    }
    private static bool Execute(Runtime.PlayerTakeItemOperation Operation, Fixture Value, string Token, object Definition, int Amount)
    { return Operation.Execute(Token, () => Value.Current ? Value.View() : null, Definition, Amount); }

    public static void RunModel()
    {
        var Operation = new Runtime.PlayerTakeItemOperation();
        var Empty = new Fixture();
        Check(!Execute(Operation, Empty, "empty", Empty.Definition, 1), "empty inventory rejects before commit");
        Check(!Execute(Operation, Empty, "unknown", null, 1), "unknown canonical item rejects before commit");

        var Split = new Fixture(); Split.Add(Split.Main, Split.MainId, 2); Split.Add(Split.Belt, Split.BeltId, 3); Split.Add(Split.Wear, Split.WearId, 5);
        Check(Execute(Operation, Split, "split", Split.Definition, 6), "split main/belt/wear quantity verifies");
        Check(Runtime.PhysicalInventoryObservation.CountForMutation(Split.Source(), Split.Definition) == 4,
            "partial, full, multiple-stack and multiple-container removal has exact delta");
        Check(Execute(Operation, Split, "exact", Split.Definition, 4), "exact available quantity verifies");

        var Maximum = new Fixture();
        for (int Index = 0; Index < 128; ++Index) Maximum.Add(Index < 64 ? Maximum.Main : Index < 96 ? Maximum.Belt : Maximum.Wear,
            Index < 64 ? Maximum.MainId : Index < 96 ? Maximum.BeltId : Maximum.WearId, 1);
        Check(Execute(Operation, Maximum, "maximum", Maximum.Definition, 128), "exact 128-entry capacity envelope succeeds");
        var Oversized = new Fixture();
        var OversizedSource = Oversized.Source(); OversizedSource.Main.Capacity = 65;
        OversizedSource.Belt.Capacity = 32; OversizedSource.Wear.Capacity = 32;
        var OversizedView = Oversized.View(); OversizedView.Inventory = () => OversizedSource;
        Reject(() => Operation.Execute("oversized", () => OversizedView, Oversized.Definition, 1));

        var HostMismatch = new Fixture(); HostMismatch.Add(HostMismatch.Main, HostMismatch.MainId, 3); HostMismatch.HostAdjustment = -1;
        Check(Reject(() => Execute(Operation, HostMismatch, "host-mismatch", HostMismatch.Definition, 1)).Contains("may have changed"),
            "host-return mismatch is indeterminate");
        var DeltaMismatch = new Fixture(); DeltaMismatch.Add(DeltaMismatch.Main, DeltaMismatch.MainId, 3); DeltaMismatch.ExtraPhysicalDelta = 1;
        Check(Reject(() => Execute(Operation, DeltaMismatch, "delta-mismatch", DeltaMismatch.Definition, 1)).Contains("may have changed"),
            "physical-delta mismatch is indeterminate");
        var BothMismatch = new Fixture(); BothMismatch.Add(BothMismatch.Main, BothMismatch.MainId, 4);
        BothMismatch.HostAdjustment = 1; BothMismatch.ExtraPhysicalDelta = 1;
        Check(Reject(() => Execute(Operation, BothMismatch, "both-mismatch", BothMismatch.Definition, 1)).Contains("may have changed"),
            "combined mismatch is indeterminate");
        var HostFailure = new Fixture(); HostFailure.Add(HostFailure.Main, HostFailure.MainId, 1); HostFailure.ThrowDuringCommit = true;
        Check(Reject(() => Execute(Operation, HostFailure, "host-failure", HostFailure.Definition, 1)).Contains("may have changed"),
            "host exception is indeterminate");
        var MatchingAddition = new Fixture(); MatchingAddition.Add(MatchingAddition.Main, MatchingAddition.MainId, 2);
        MatchingAddition.AddMatchingDuringCommit = true;
        Check(Reject(() => Execute(Operation, MatchingAddition, "matching-addition", MatchingAddition.Definition, 1)).Contains("may have changed"),
            "matching-item addition makes the physical delta indeterminate");
        var UnrelatedMutation = new Fixture(); UnrelatedMutation.Add(UnrelatedMutation.Main, UnrelatedMutation.MainId, 2);
        UnrelatedMutation.MutateUnrelated = true;
        Check(Execute(Operation, UnrelatedMutation, "unrelated-mutation", UnrelatedMutation.Definition, 1),
            "unrelated item mutation does not change the requested-item verification delta");

        var BeforeCommitDisconnect = new Fixture(); BeforeCommitDisconnect.Add(BeforeCommitDisconnect.Main, BeforeCommitDisconnect.MainId, 1);
        int Resolves = 0;
        string BeforeCommit = Reject(() => Operation.Execute("before-disconnect", () => ++Resolves == 1 ? BeforeCommitDisconnect.View() : null,
            BeforeCommitDisconnect.Definition, 1));
        Check(BeforeCommit.Contains("no longer connected") && Runtime.PhysicalInventoryObservation.CountForMutation(
            BeforeCommitDisconnect.Source(), BeforeCommitDisconnect.Definition) == 1, "disconnect before commit is stale and mutation-free");
        var AfterCommitDisconnect = new Fixture(); AfterCommitDisconnect.Add(AfterCommitDisconnect.Main, AfterCommitDisconnect.MainId, 1);
        Resolves = 0;
        Check(Reject(() => Operation.Execute("after-disconnect", () => ++Resolves <= 2 ? AfterCommitDisconnect.View() : null,
            AfterCommitDisconnect.Definition, 1)).Contains("may have changed"), "disconnect during verify is indeterminate");

        var Recursive = new Fixture(); Recursive.Add(Recursive.Main, Recursive.MainId, 2);
        Runtime.PlayerView RecursiveView = Recursive.View();
        RecursiveView.TakeInventory = (Definition, Amount) => {
            Check(Reject(() => Operation.Execute("recursive", () => RecursiveView, Definition, 1)).Contains("already in progress"),
                "recursive same-player mutation rejected");
            return Recursive.Take(Definition, Amount);
        };
        Check(Operation.Execute("recursive", () => RecursiveView, Recursive.Definition, 1), "outer recursive fixture still verifies");

        var First = new Fixture(); First.Add(First.Main, First.MainId, 1);
        var Second = new Fixture(); Second.Add(Second.Main, Second.MainId, 1);
        Runtime.PlayerView FirstView = First.View();
        FirstView.TakeInventory = (Definition, Amount) => {
            Check(Execute(Operation, Second, "different-b", Second.Definition, 1), "different exact Player may proceed independently");
            return First.Take(Definition, Amount);
        };
        Check(Operation.Execute("different-a", () => FirstView, First.Definition, 1), "first exact Player verifies around independent second Player");

        var Watch = Stopwatch.StartNew();
        for (int Index = 0; Index < 1000; ++Index) {
            var Value = new Fixture(); Value.Add(Value.Main, Value.MainId, 1);
            Check(Execute(Operation, Value, "success-" + Index, Value.Definition, 1), "successful stress mutation");
        }
        for (int Index = 0; Index < 1000; ++Index)
            Check(!Execute(Operation, Empty, "reject-" + Index, Empty.Definition, 1), "PREPARE rejection stress");
        for (int Index = 0; Index < 1000; ++Index) {
            var Value = new Fixture(); Value.Add(Value.Main, Value.MainId, 2); Value.HostAdjustment = -1;
            Reject(() => Execute(Operation, Value, "mismatch-" + Index, Value.Definition, 1));
        }
        Watch.Stop();
        Check(Operation.BusyCount == 0 && Operation.Diagnostics.Attempts >= 3015 &&
            Operation.Diagnostics.PrepareRejected >= 1002 && Operation.Diagnostics.Verified >= 1006 &&
            Operation.Diagnostics.HostAmountMismatches >= 1002 && Operation.Diagnostics.PhysicalDeltaMismatches >= 2 &&
            Operation.Diagnostics.Indeterminate >= 1005 && Operation.Diagnostics.GateBusyRejected == 1,
            "bounded diagnostics and mutation gate return to baseline");
        Console.WriteLine("[CarbonLuau:Player1FAModel] PASS PREPARE/COMMIT/VERIFY, exact-player gate, bounds and 3000-operation stress in " +
            Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
    }
}
