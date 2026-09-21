using System;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class PlayerInteractionFoundation1DTests
{
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Player-1D: " + Message); }

    private static string Reject(Action Action)
    {
        try { Action(); }
        catch (InvalidOperationException Error) { return Error.Message; }
        throw new Exception("Player-1D: expected rejection");
    }

    public static void RunModel()
    {
        var State = new Runtime.PlayerTeleportState {Current = true, Alive = true};
        Runtime.PlayerPosition Moved = default(Runtime.PlayerPosition);
        int Inspections = 0, Mutations = 0, Verifications = 0;
        var Operation = new Runtime.PlayerTeleportOperation(
            () => { Inspections++; return State; },
            (Destination, Before) => { Mutations++; Moved = Destination; State.Mounted = false; State.Parented = false; },
            (Destination, Before) => { Verifications++; return State.Current && !State.Mounted && !State.Parented &&
                State.Sleeping == Before.Sleeping && Moved.X == Destination.X && Moved.Y == Destination.Y && Moved.Z == Destination.Z; });

        foreach (var Destination in new[] {
            new Runtime.PlayerPosition(0f, 0f, 0f),
            new Runtime.PlayerPosition(12.5f, -24.25f, 0.125f),
            new Runtime.PlayerPosition(Single.MaxValue, -Single.MaxValue, Single.Epsilon)
        }) Operation.Execute(Destination);
        Check(Inspections == 3 && Mutations == 3 && Verifications == 3 && Moved.X == Single.MaxValue,
            "valid exact coordinates mutate and verify once per call");

        State.Sleeping = true; State.Mounted = true; State.Parented = true;
        Operation.Execute(new Runtime.PlayerPosition(-4f, 5f, 6f));
        Check(State.Sleeping && !State.Mounted && !State.Parented, "sleeping state is preserved while mount and parent are normalized");

        foreach (var Case in new[] {
            new Runtime.PlayerTeleportState {Current = false, Alive = true},
            new Runtime.PlayerTeleportState {Current = true, Alive = false},
            new Runtime.PlayerTeleportState {Current = true, Alive = true, Spectating = true},
            new Runtime.PlayerTeleportState {Current = true, Alive = true, Wounded = true},
            new Runtime.PlayerTeleportState {Current = true, Alive = true, Incapacitated = true}
        }) {
            State = Case; int Before = Mutations;
            Reject(() => Operation.Execute(new Runtime.PlayerPosition(1f, 2f, 3f)));
            Check(Mutations == Before, "ineligible state fails before mutation");
        }

        int Partial = 0;
        var FailingMutation = new Runtime.PlayerTeleportOperation(
            () => new Runtime.PlayerTeleportState {Current = true, Alive = true},
            (Destination, Before) => { Partial++; throw new InvalidOperationException("private host detail"); },
            (Destination, Before) => { throw new Exception("must not verify"); });
        string Failure = Reject(() => FailingMutation.Execute(new Runtime.PlayerPosition(1f, 2f, 3f)));
        Check(Partial == 1 && Failure.Contains("after host mutation began") && !Failure.Contains("private host detail"),
            "post-boundary host failure is curated and not rolled back");

        int VerifyCalls = 0;
        var Mismatch = new Runtime.PlayerTeleportOperation(
            () => new Runtime.PlayerTeleportState {Current = true, Alive = true},
            (Destination, Before) => { },
            (Destination, Before) => { VerifyCalls++; return false; });
        Reject(() => Mismatch.Execute(new Runtime.PlayerPosition(1f, 2f, 3f)));
        Check(VerifyCalls == 1, "server verification is bounded to one attempt");

        int PreMutation = 0;
        var InspectionFailure = new Runtime.PlayerTeleportOperation(
            () => { throw new InvalidOperationException("private inspection detail"); },
            (Destination, Before) => { PreMutation++; },
            (Destination, Before) => true);
        string Inspection = Reject(() => InspectionFailure.Execute(new Runtime.PlayerPosition(1f, 2f, 3f)));
        Check(PreMutation == 0 && Inspection.Contains("before mutation") && !Inspection.Contains("private inspection detail"),
            "inspection failure has no mutation and no private detail");

        Console.WriteLine("[CarbonLuau:Player1DModel] PASS eligibility, exact coordinates, sleep preservation, normalization, mutation boundary and bounded verification");
    }
}
