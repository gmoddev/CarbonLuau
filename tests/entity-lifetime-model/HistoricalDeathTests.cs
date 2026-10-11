using System;
using Model = Carbon.Plugins.EntityLifetimeModel;

internal static partial class Program
{
    private static void TestHistoricalDeathWitness()
    {
        using (var Directory = new Model(2)) {
            Directory.BeginQualifiedObservation();
            object Entity = new object();
            var Spawn = Directory.BeginSpawn(Entity);
            Check(Directory.CaptureCompletedWitness(Entity) == null, "pending Spawn cannot certify death identity");
            Directory.CompleteSpawn(Spawn, true, true);
            var Witness = Directory.CaptureCompletedWitness(Entity);
            Check(Witness != null && Directory.IsHistoricalObservationCurrent(Witness), "completed historical identity belongs to exact host observer");
            var Next = Directory.BeginSpawn(Entity);
            Check(!Directory.IsWitnessEpochUnchanged(Witness) && Directory.IsHistoricalObservationCurrent(Witness),
                "superseded epoch cannot validate a live proxy but retains certified historical scalars");
            Directory.CompleteSpawn(Next, false, false);
            Check(Directory.IsHistoricalObservationCurrent(Witness), "later failed Spawn cannot retarget or erase historical identity");
            using (var Other = new Model(2)) {
                Other.BeginQualifiedObservation();
                Check(!Other.IsHistoricalObservationCurrent(Witness), "other host cannot accept historical certificate");
            }
            Directory.BreakObserverContinuity();
            Check(!Directory.IsHistoricalObservationCurrent(Witness), "observer loss discards queued historical certificates");
            Check(!Directory.IsHistoricalObservationCurrent(null), "null certificate fails closed");
        }
        var Stopped = new Model(2);
        Stopped.BeginQualifiedObservation();
        object Original = new object();
        Stopped.CompleteSpawn(Stopped.BeginSpawn(Original), true, true);
        var Old = Stopped.CaptureCompletedWitness(Original);
        Stopped.Dispose();
        Check(!Stopped.IsHistoricalObservationCurrent(Old), "teardown cancels historical authority without host-object lookup");
    }
}
