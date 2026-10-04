using System;
using System.Diagnostics;
using System.Globalization;
using Model = Carbon.Plugins.EntityLifetimeModel;
using Traversal = Carbon.Plugins.EntityDiscoveryTraversal;

internal static partial class Program
{
    private const int BenchmarkSlots = 262144;
    private const int BenchmarkQueries = 8;
    private const int BenchmarkResults = 256;

    // Opt-in measurement of the linked managed implementation, not a host
    // producer, Unity borrow, Mono measurement, or a hard wall-clock bound.
    private static void RunBenchmark()
    {
        Console.WriteLine("[CarbonLuau:EntityDiscoveryBenchmark] NonHost=True Runtime=" + Environment.Version +
            " OS=" + Environment.OSVersion + " PointerBits=" + (IntPtr.Size * 8) +
            " Slots=" + BenchmarkSlots + " Queries=" + BenchmarkQueries +
            " WorkPerTurn=1024 RawPerTurn=1024 Results=256 Deliveries=2");
        var Preparation = Stopwatch.StartNew();
        var Entities = new Entity[BenchmarkSlots];
        using (var Model = new Model(BenchmarkSlots))
        {
            Model.BeginQualifiedObservation();
            Check(Model.QualifyStartupCompletion(), "benchmark startup");
            for (int Index = 0; Index < Entities.Length; ++Index)
            {
                var Value = new Entity { Id = (ulong)Index + 1, X = Index < BenchmarkResults ? 0 : 2 };
                Entities[Index] = Value;
                Check(Model.CompleteSpawn(Model.BeginSpawn(Value), true, true), "benchmark spawn");
            }
            CheckTokenStorageEmpty(Model);
            Check(Model.CatalogSlotCount == BenchmarkSlots && Model.CatalogReady, "benchmark populated catalog");
            Preparation.Stop();
            Console.WriteLine("[CarbonLuau:EntityDiscoveryBenchmark] PreparationMs=" + BenchmarkNumber(Preparation.Elapsed.TotalMilliseconds));
            RunBenchmarkLane(Model, false);
            RunBenchmarkLane(Model, true);
            // Catalog identities are deliberately strong for both lanes: GC must
            // not silently turn this full-catalog measurement into a hole scan.
            GC.KeepAlive(Entities);
        }
        Console.WriteLine("[CarbonLuau:EntityDiscoveryBenchmark] PASS Checks=" + Checks);
    }

    private static string BenchmarkNumber(double Value)
    { return Value.ToString("F3", CultureInfo.InvariantCulture); }

    private static void RunBenchmarkLane(Model Model, bool Observed)
    {
        var Policy = new Traversal.WorkPolicy(8, 2, 1024, 1024, 256, BenchmarkSlots, 2, 512,
            checked(Stopwatch.Frequency * 120));
        var Authority = new Model.Authority(1, 1, 1);
        Func<Model.Authority, bool> IsCurrent = Ignore => true;
        Func<object, Model.HostEvidence> Read = Identity => {
            var Value = (Entity)Identity;
            return new Model.HostEvidence(Value.Alive, true, true, Value.Id, Value.Prefab, Value.Alive ? Value : null);
        };
        var Counts = new int[BenchmarkQueries];
        var WeakStorage = new WeakTableStorage(Model);
        var Previous = new int[BenchmarkQueries];
        var Timings = new long[4096];
        int Turns = 0, Delivered = 0, MaxUnits = 0, MaxRaw = 0, MaxCalls = 0, MaxDeliveries = 0, MaxSpread = 0;
        long TotalUnits = 0, TotalRaw = 0, TotalCalls = 0, TotalTicks = 0, Allocated = 0, MaxAllocated = 0;
        long ScanWorst = 0, DeliveryWorst = 0;
        int Generation0 = GC.CollectionCount(0), Generation1 = GC.CollectionCount(1), Generation2 = GC.CollectionCount(2);
        using (var Scheduler = new Traversal(Model, Policy))
        {
            long Origin = Stopwatch.GetTimestamp();
            for (int Index = 0; Index < BenchmarkQueries; ++Index)
            {
                int QueryIndex = Index;
                ulong RequestId;
                Check(Scheduler.TryStart(new Traversal.Query(0, 0, 0, 0, null, BenchmarkResults),
                    (ulong)(Index / 2) + 1, Candidate => {
                        Counts[QueryIndex]++;
                        if (!Observed) return new Traversal.CandidateObservation(Traversal.CandidateStatus.Skip);
                        var Value = (Entity)Candidate.Target;
                        Model.HostEvidence Evidence;
                        if (!Model.TryObserveCatalog(Candidate, Authority, IsCurrent, Read, out Evidence))
                            return new Traversal.CandidateObservation(Traversal.CandidateStatus.Failure);
                        return new Traversal.CandidateObservation(Candidate, Candidate.Birth,
                            Evidence.NetworkId, Evidence.Prefab, Value.X, Value.Y, Value.Z);
                    }, () => true, Observation => {
                        Model.HostEvidence Evidence;
                        return Model.TryObserveCatalog(Observation.Candidate, Authority, IsCurrent, Read, out Evidence) &&
                            Observation.Birth == Observation.Candidate.Birth && Observation.Id == Evidence.NetworkId &&
                            String.Equals(Observation.Prefab, Evidence.Prefab, StringComparison.Ordinal);
                    }, Result => {
                        Check(QueryIndex == Delivered, "benchmark FIFO delivery");
                        Check(Result.Status == Traversal.Outcome.Success && Result.RawSlots == BenchmarkSlots &&
                            Result.Count == (Observed ? BenchmarkResults : 0), "benchmark complete result");
                        if (Observed)
                            for (int Match = 0; Match < Result.Count; ++Match)
                                Check(Result.GetResult(Match).Id == (ulong)Match + 1, "benchmark original result identity");
                        Delivered++;
                    }, Stopwatch.GetTimestamp() - Origin, out RequestId) == Traversal.StartStatus.Accepted,
                    "benchmark query admission");
            }
            long RunStart = Stopwatch.GetTimestamp();
            while (Scheduler.ActiveCount != 0)
            {
                Check(Turns < Timings.Length, "benchmark finite turn count");
                long Now = Stopwatch.GetTimestamp() - Origin;
                long AllocationBefore = GC.GetAllocatedBytesForCurrentThread();
                long Start = Stopwatch.GetTimestamp();
                Traversal.TurnWork Work = Scheduler.RunTurn(Now);
                long Elapsed = Stopwatch.GetTimestamp() - Start;
                long TurnAllocated = GC.GetAllocatedBytesForCurrentThread() - AllocationBefore;
                CheckTokenStorageEmpty(Model);
                WeakStorage.CheckUnchanged();
                Timings[Turns++] = Elapsed;
                TotalTicks += Elapsed; Allocated += TurnAllocated;
                MaxAllocated = Math.Max(MaxAllocated, TurnAllocated);
                if (Work.RawSlots != 0) ScanWorst = Math.Max(ScanWorst, Elapsed);
                if (Work.Deliveries != 0) DeliveryWorst = Math.Max(DeliveryWorst, Elapsed);
                Check(Work.Units <= Policy.WorkPerTurn && Work.RawSlots <= Policy.RawSlotsPerTurn &&
                    Work.ProducerCalls == Work.RawSlots && Work.Deliveries <= Policy.MaximumDeliveriesPerTurn,
                    "benchmark aggregate bounds and populated encounters");
                MaxUnits = Math.Max(MaxUnits, Work.Units); MaxRaw = Math.Max(MaxRaw, Work.RawSlots);
                MaxCalls = Math.Max(MaxCalls, Work.ProducerCalls); MaxDeliveries = Math.Max(MaxDeliveries, Work.Deliveries);
                TotalUnits += Work.Units; TotalRaw += Work.RawSlots; TotalCalls += Work.ProducerCalls;
                int Minimum = Int32.MaxValue, Maximum = 0;
                for (int Index = 0; Index < Counts.Length; ++Index)
                {
                    int Delta = Counts[Index] - Previous[Index];
                    Minimum = Math.Min(Minimum, Delta); Maximum = Math.Max(Maximum, Delta);
                    Previous[Index] = Counts[Index];
                }
                MaxSpread = Math.Max(MaxSpread, Maximum - Minimum);
                Check(Maximum - Minimum <= 1, "benchmark fair per-query work");
            }
            double WallMs = (Stopwatch.GetTimestamp() - RunStart) * 1000.0 / Stopwatch.Frequency;
            Check(Delivered == BenchmarkQueries && Scheduler.CallbackFailures == 0, "benchmark all callbacks succeeded");
            for (int Index = 0; Index < Counts.Length; ++Index)
                Check(Counts[Index] == BenchmarkSlots, "benchmark full per-query scan");
            Check(TotalRaw == (long)BenchmarkSlots * BenchmarkQueries && TotalCalls == TotalRaw,
                "benchmark exact total raw work");
            Check(TotalUnits == TotalRaw + BenchmarkQueries * (1L + 2 * (Observed ? BenchmarkResults : 0)),
                "benchmark exact total work units");
            long First = Timings[0], WarmWorst = 0;
            for (int Index = 1; Index < Turns; ++Index) WarmWorst = Math.Max(WarmWorst, Timings[Index]);
            Array.Sort(Timings, 0, Turns);
            double TickMs = 1000.0 / Stopwatch.Frequency;
            string Prefix = "[CarbonLuau:EntityDiscoveryBenchmark] Lane=" + (Observed ? "Observed256" : "Skip");
            Console.WriteLine(Prefix + " Turns=" + Turns + " TotalUnits=" + TotalUnits + " Raw=" + TotalRaw +
                " ProducerCalls=" + TotalCalls + " PerQuery=" + BenchmarkSlots + " FairDeltaSpread=" + MaxSpread +
                " MaxTurnUnits=" + MaxUnits + " MaxTurnRaw=" + MaxRaw + " MaxTurnCalls=" + MaxCalls +
                " MaxTurnDeliveries=" + MaxDeliveries);
            Console.WriteLine(Prefix + " FirstTurnMs=" + BenchmarkNumber(First * TickMs) +
                " MedianTurnMs=" + BenchmarkNumber(Timings[Turns / 2] * TickMs) +
                " P95TurnMs=" + BenchmarkNumber(Timings[(Turns - 1) * 95 / 100] * TickMs) +
                " WorstTurnMs=" + BenchmarkNumber(Timings[Turns - 1] * TickMs) +
                " AfterFirstWorstMs=" + BenchmarkNumber(WarmWorst * TickMs) +
                " ScanWorstMs=" + BenchmarkNumber(ScanWorst * TickMs) +
                " DeliveryWorstMs=" + BenchmarkNumber(DeliveryWorst * TickMs) +
                " SumTurnMs=" + BenchmarkNumber(TotalTicks * TickMs) + " LoopWallMs=" + BenchmarkNumber(WallMs));
            Console.WriteLine(Prefix + " TurnAllocatedBytes=" + Allocated + " MaxTurnAllocatedBytes=" + MaxAllocated +
                " GC0=" + (GC.CollectionCount(0) - Generation0) + " GC1=" + (GC.CollectionCount(1) - Generation1) +
                " GC2=" + (GC.CollectionCount(2) - Generation2) +
                " F1TokenRecords=0 F1TokenSweep=0 F1NextToken=0" +
                " Note=FirstTurnMayIncludeJit;ObservedLaneFollowsSkip;" +
                (Observed ? "DirectManagedObservationAndValidationMeasured;" : "TrivialSkipProducer;") + "NativeReaderNotMeasured");
        }
    }
}
