using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Model = Carbon.Plugins.EntityLifetimeModel;
using Traversal = Carbon.Plugins.EntityDiscoveryTraversal;

internal static partial class Program
{
    private static int Checks;
    private static void Check(bool Condition, string Label)
    {
        Checks++;
        if (!Condition) throw new Exception(Label);
    }

    private static readonly FieldInfo TokenRecordsField = typeof(Model).GetField("TokenRecords", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo TokenSweepField = typeof(Model).GetField("TokenSweep", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo NextTokenField = typeof(Model).GetField("NextToken", BindingFlags.NonPublic | BindingFlags.Instance);

    private static void CheckTokenStorageEmpty(Model Model)
    {
        Check(((Dictionary<ulong, WeakReference>)TokenRecordsField.GetValue(Model)).Count == 0 &&
            ((Queue<ulong>)TokenSweepField.GetValue(Model)).Count == 0 &&
            (ulong)NextTokenField.GetValue(Model) == 0,
            "untouched catalog observation never allocates/registers an F1 token or grows token storage");
    }

    private sealed class Entity
    {
        internal ulong Id;
        internal string Prefab = "assets/example.prefab";
        internal double X, Y, Z = 0;
        internal bool Alive = true;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Model Model;
        internal readonly Traversal Scheduler;
        internal readonly Traversal.WorkPolicy Policy;
        internal readonly List<Entity> Entities = new List<Entity>();
        internal readonly List<Traversal.Completion> Results = new List<Traversal.Completion>();
        internal readonly List<ulong> Encounters = new List<ulong>();
        internal bool Authority = true;
        internal int ValidationCalls;
        internal long Now;
        private readonly Model.Authority HostAuthority = new Model.Authority(1, 1, 1);

        internal Fixture(int Count = 3, int Raw = 1, int Total = 100, long Deadline = 1000,
            int MaximumResults = 8, int Work = 33, int Deliveries = 8, int Capacity = 32)
        {
            Policy = new Traversal.WorkPolicy(8, 2, Work, Raw, MaximumResults, Total, Deliveries, 64, Deadline);
            Model = new Model(Capacity);
            Model.BeginQualifiedObservation();
            Check(Model.QualifyStartupCompletion(), "startup");
            for (int Index = 0; Index < Count; ++Index) Add();
            Scheduler = new Traversal(Model, Policy);
        }

        internal Entity Add()
        {
            var Value = new Entity { Id = (ulong)Entities.Count + 1 };
            Entities.Add(Value); Spawn(Value); return Value;
        }

        internal void Spawn(Entity Value)
        {
            Model.SpawnAttempt Attempt = Model.BeginSpawn(Value);
            Check(Model.CompleteSpawn(Attempt, true, true), "completed spawn");
            CheckTokenStorageEmpty(Model);
        }

        private Model.HostEvidence Read(object Identity)
        {
            Entity Value = (Entity)Identity;
            return new Model.HostEvidence(Value.Alive, true, true, Value.Id, Value.Prefab, Value.Alive ? Value : null);
        }

        internal Traversal.CandidateObservation Observe(Model.MembershipCandidate Candidate)
        {
            Entity Value = Candidate.Target as Entity;
            Model.HostEvidence Evidence;
            if (Value == null || !Model.TryObserveCatalog(Candidate, HostAuthority, Ignore => Authority, Read, out Evidence))
                return new Traversal.CandidateObservation(Traversal.CandidateStatus.Skip);
            Encounters.Add(Value.Id);
            return new Traversal.CandidateObservation(Candidate, Candidate.Birth,
                Evidence.NetworkId, Evidence.Prefab, Value.X, Value.Y, Value.Z);
        }

        internal bool Validate(Traversal.CandidateObservation Observation)
        {
            ValidationCalls++;
            Model.HostEvidence Evidence;
            return Observation.Candidate != null && Observation.Birth == Observation.Candidate.Birth &&
                Model.TryObserveCatalog(Observation.Candidate, HostAuthority, Ignore => Authority, Read, out Evidence) &&
                Evidence.NetworkId == Observation.Id && String.Equals(Evidence.Prefab, Observation.Prefab, StringComparison.Ordinal);
        }

        internal Traversal.Query Query(int Limit = 8, double Radius = 100, string Prefab = null)
        { return new Traversal.Query(0, 0, 0, Radius, Prefab, Limit); }

        internal ulong Start(ulong Domain = 1, Traversal.Query? Query = null,
            Func<Model.MembershipCandidate, Traversal.CandidateObservation> Observe = null,
            Action<Traversal.Completion> Callback = null,
            Func<Traversal.CandidateObservation, bool> Validate = null)
        {
            ulong Id;
            Check(Scheduler.TryStart(Query ?? this.Query(Policy.MaximumResults), Domain, Observe ?? this.Observe,
                () => Authority, Validate ?? this.Validate, Callback ?? Results.Add, Now, out Id) == Traversal.StartStatus.Accepted,
                "request accepted");
            return Id;
        }

        internal Traversal.TurnWork Tick(long? Time = null)
        {
            Now = Time ?? Now + 1;
            Traversal.TurnWork Work = Scheduler.RunTurn(Now);
            Check(Work.Units <= Policy.WorkPerTurn && Work.RawSlots <= Policy.RawSlotsPerTurn &&
                Work.ProducerCalls <= Work.RawSlots && Work.Deliveries <= Policy.MaximumDeliveriesPerTurn,
                "aggregate per-turn bounds");
            CheckTokenStorageEmpty(Model);
            return Work;
        }

        internal void Drain(int Maximum = 300)
        {
            for (int Index = 0; Scheduler.ActiveCount != 0 && Index < Maximum; ++Index) Tick();
            Check(Scheduler.ActiveCount == 0, "bounded test drain");
        }

        public void Dispose() { Scheduler.Dispose(); Model.Dispose(); }
    }

    private static void TestSuccessAndFiltering()
    {
        using (var F = new Fixture(Raw: 3))
        {
            F.Entities[0].X = 3; F.Entities[0].Y = 4;
            F.Entities[1].X = 5.01;
            F.Entities[2].Prefab = "assets/other.prefab";
            F.Spawn(F.Entities[2]);
            F.Start(Query: F.Query(Radius: 5, Prefab: "assets/example.prefab"));
            F.Drain();
            Check(F.Results.Count == 1 && F.Results[0].Status == Traversal.Outcome.Success &&
                F.Results[0].Count == 1 && F.Results[0].GetResult(0).Id == 1, "inclusive sphere and exact prefab");
            Check(F.Results[0].RawSlots == 3 && F.ValidationCalls == 1, "all raw slots and final host validation");
            F.Entities[0].X = 7;
            Check(F.Results[0].GetResult(0).X == 3, "copied immutable sample survives movement");
            bool Thrown = false;
            try { F.Results[0].GetResult(1); } catch (ArgumentOutOfRangeException) { Thrown = true; }
            Check(Thrown, "completion exposes only count");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(Query: F.Query(Radius: 0)); F.Drain();
            Check(F.Results[0].Count == 1, "zero radius exact point");
            F.Entities[0].X = Double.MaxValue;
            F.Start(Query: new Traversal.Query(-Double.MaxValue, 0, 0, Double.MaxValue, null, 8)); F.Drain();
            Check(F.Results[1].Count == 0, "overflowing finite subtraction correctly outside");
            F.Start(Query: new Traversal.Query(Double.MaxValue, 0, 0, Double.MaxValue, null, 8)); F.Drain();
            Check(F.Results[2].Count == 1, "huge radius does not overflow squared distance");
        }
    }

    private static void TestAdmissionAndAuthority()
    {
        using (var F = new Fixture(Count: 0))
        {
            ulong Id;
            foreach (Traversal.Query Query in new[] {
                new Traversal.Query(Double.NaN, 0, 0, 1, null, 1),
                new Traversal.Query(0, 0, 0, Double.PositiveInfinity, null, 1),
                F.Query(Radius: -1), F.Query(Limit: 0), F.Query(Limit: 9),
                F.Query(Prefab: ""), F.Query(Prefab: "bad\0name"), F.Query(Prefab: "\ud800"),
                F.Query(Prefab: new string('\u00e9', 33)), F.Query(Prefab: new string('x', 65)) })
                Check(F.Scheduler.TryStart(Query, 1, F.Observe, () => true, F.Validate, F.Results.Add, 0, out Id)
                    == Traversal.StartStatus.Invalid && Id == 0, "invalid input before enrollment");
            Check(F.Results.Count == 0 && F.Scheduler.ActiveCount == 0, "rejections have no callbacks/state");
            F.Authority = false;
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => F.Authority, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Unauthorized, "authority admission");
            F.Authority = true;
            F.Start(); F.Start();
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => true, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Capacity, "per-domain cap includes pending empty scans");
            for (ulong Domain = 2; Domain <= 4; ++Domain) { F.Start(Domain); F.Start(Domain); }
            Check(F.Scheduler.ActiveCount == 8, "global ceiling eight");
            Check(F.Scheduler.TryStart(F.Query(), 5, F.Observe, () => true, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Capacity, "global admission cap");
            F.Tick(); F.Authority = false; F.Drain();
            Check(F.Results.Count == 8, "one delivery each admitted request");
            foreach (var Result in F.Results) Check(Result.Status == Traversal.Outcome.Unauthorized && Result.Count == 0,
                "delivery authority checked even empty");
        }
    }

    private static void TestFairnessAndReentrancy()
    {
        using (var F = new Fixture(Count: 4, Raw: 1))
        {
            var Order = new List<int>();
            Func<int, Func<Model.MembershipCandidate, Traversal.CandidateObservation>> Reader = Number => Candidate =>
            { Order.Add(Number); return F.Observe(Candidate); };
            F.Start(1, Observe: Reader(1)); F.Start(2, Observe: Reader(2));
            for (int Index = 0; Index < 8; ++Index) F.Tick();
            for (int Index = 0; Index < Order.Count; ++Index) Check(Order[Index] == Index % 2 + 1, "round robin no raw-cap starvation");
            F.Drain();
            Check(F.Results.Count == 2 && F.Results[0].RequestId < F.Results[1].RequestId, "FIFO ready delivery");
        }
        using (var F = new Fixture(Count: 0))
        {
            int Depth = 0, MaximumDepth = 0, Completed = 0;
            Action<Traversal.Completion> Callback = null;
            Callback = Result =>
            {
                Depth++; MaximumDepth = Math.Max(MaximumDepth, Depth); Completed++;
                Check(F.Scheduler.ActiveCount == 0, "detach before callback permits quota reuse");
                if (Completed < 5) F.Start(Callback: Callback);
                Check(F.Scheduler.RunTurn(F.Now).Units == 0, "nested owner turn suppressed");
                Depth--;
            };
            F.Start(Callback: Callback); F.Drain();
            Check(Completed == 5 && MaximumDepth == 1, "reentrant admission never recursive delivery");
            F.Start(Callback: Result => { throw new Exception("test callback"); });
            F.Start(2); F.Drain();
            Check(F.Scheduler.CallbackFailures == 1 && F.Results.Count == 1, "callback fault contained and next FIFO delivered");
        }
    }

    private static void TestCapsCancellationAndLoss()
    {
        using (var F = new Fixture(Count: 3, Total: 2))
        {
            F.Start(); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.RawLimit && F.Results[0].RawSlots == 2 && F.Results[0].Count == 0,
                "total raw cap discards partial matches");
        }
        using (var F = new Fixture(Count: 3))
        {
            F.Start(Query: F.Query(Limit: 2)); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.ResultLimit && F.Results[0].Count == 0,
                "result overflow not truncated success");
        }
        using (var F = new Fixture(Count: 2, Total: 2))
        {
            F.Start(Query: F.Query(Limit: 2)); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Success && F.Results[0].Count == 2,
                "exact raw/result boundary succeeds");
        }
        using (var F = new Fixture(Deadline: 3))
        {
            F.Start(); F.Tick(); F.Tick(3); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Deadline && F.Results[0].Count == 0, "deadline equality whole failure");
        }
        using (var F = new Fixture(Count: 0, Deadline: 2))
        {
            F.Start(); F.Tick(); F.Tick(2);
            Check(F.Results[0].Status == Traversal.Outcome.Deadline, "deadline covers queued delivery");
        }
        using (var F = new Fixture())
        {
            ulong Id = F.Start(); F.Tick();
            Check(F.Scheduler.Cancel(Id), "cancel active"); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Cancelled && F.Results[0].Count == 0, "cancel drops partial");
            Check(!F.Scheduler.Cancel(Id), "delivered cancel has no effect");
        }
        using (var F = new Fixture(Count: 0))
        {
            F.Start(); F.Tick(); F.Model.BreakObserverContinuity(); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.CatalogLost && F.Results[0].Count == 0, "empty completion continuity check");
        }
        using (var F = new Fixture())
        {
            F.Start(); F.Tick(); F.Model.InvalidateCatalog(); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.CatalogLost, "sticky catalog loss drops partial");
        }
    }

    private static void TestMembershipAndRetirement()
    {
        using (var F = new Fixture(Count: 3))
        {
            F.Start(); F.Tick();
            // Unvisited slot is reused after acceptance. It remains a raw slot,
            // but its new birth must not enter this accepted candidate epoch.
            F.Spawn(F.Entities[1]); F.Add(); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Success && F.Results[0].RawSlots == 3 && F.Results[0].Count == 2,
                "captured extent and birth watermark exclude new enrollment/reused slot");
            Check(F.Results[0].GetResult(0).Id == 1 && F.Results[0].GetResult(1).Id == 3, "no replacement retarget");
        }
        using (var F = new Fixture(Count: 3))
        {
            F.Entities[1].Alive = false;
            F.Start(); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Success && F.Results[0].Count == 2 && F.Results[0].RawSlots == 3,
                "legitimate missing encounter skip");
        }
        using (var F = new Fixture(Count: 2))
        {
            F.Start(); F.Tick(); F.Spawn(F.Entities[0]); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.StaleResult && F.Results[0].Count == 0, "retained match retires before delivery");
            Check(F.ValidationCalls == 0, "known stale result never enters host final validator");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(); F.Tick(); F.Entities[0].Alive = false; F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.StaleResult && F.Results[0].Count == 0,
                "host final keyed/liveness validator required beyond catalog membership");
        }
        using (var F = new Fixture(Count: 3))
        {
            F.Start();
            F.Tick(); F.Tick(); F.Tick(); F.Tick();
            foreach (Traversal.CandidateObservation Observation in new[] {
                F.Results[0].GetResult(0), F.Results[0].GetResult(1), F.Results[0].GetResult(2) })
            {
                ((Entity)Observation.Candidate.Target).Alive = false;
                Check(!F.Validate(Observation), "direct host retirement creates a hole without token admission");
            }
            F.Results.Clear(); F.Encounters.Clear();
            F.Start(); F.Drain();
            Check(F.Results[0].RawSlots == 3 && F.Results[0].Count == 0 && F.Encounters.Count == 0,
                "holes count raw work without producer calls");
        }
        using (var F = new Fixture(Count: 2, Raw: 2))
        {
            F.Start(Validate: Observation => {
                if (Observation.Id == 2) F.Spawn(F.Entities[0]);
                return true;
            }); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.StaleResult, "final callback-free pass detects earlier validation mutation");
        }
    }

    private static void TestProducerFaultsAndOwner()
    {
        using (var F = new Fixture(Count: 1))
        {
            foreach (Func<Model.MembershipCandidate, Traversal.CandidateObservation> Reader in new Func<Model.MembershipCandidate, Traversal.CandidateObservation>[] {
                Candidate => { throw new Exception("producer fault"); },
                Candidate => new Traversal.CandidateObservation(Traversal.CandidateStatus.Failure),
                Candidate => new Traversal.CandidateObservation(Candidate, 0, 1, "assets/example.prefab", 0, 0, 0),
                Candidate => new Traversal.CandidateObservation(Candidate, Candidate.Birth, 1, "assets/example.prefab", Double.NaN, 0, 0),
                Candidate => new Traversal.CandidateObservation((Traversal.CandidateStatus)99) })
            { F.Start(Observe: Reader); F.Drain(); }
            foreach (var Result in F.Results) Check(Result.Status != Traversal.Outcome.Success && Result.Count == 0,
                "malformed/failed producer never partial success");
            F.Start(Validate: Observation => { throw new Exception("validator fault"); }); F.Drain();
            Check(F.Results[5].Status == Traversal.Outcome.StaleResult, "host validator exception contained");
            Exception OwnerError = null;
            var Worker = new Thread(() => { try { F.Scheduler.RunTurn(F.Now); } catch (Exception Error) { OwnerError = Error; } });
            Worker.Start(); Worker.Join();
            Check(OwnerError is InvalidOperationException, "owner-thread enforced");
        }
    }

    private static void TestAggregateStress()
    {
        using (var F = new Fixture(Count: 20, Raw: 5, MaximumResults: 20, Work: 41, Capacity: 24))
        {
            for (ulong Domain = 1; Domain <= 4; ++Domain) { F.Start(Domain); F.Start(Domain); }
            int Raw = 0, Calls = 0;
            while (F.Scheduler.ActiveCount > 0) { var Work = F.Tick(); Raw += Work.RawSlots; Calls += Work.ProducerCalls; }
            Check(Raw == 160 && Calls == 160 && F.Results.Count == 8, "aggregate counts across eight full scans");
            foreach (var Result in F.Results) Check(Result.Status == Traversal.Outcome.Success && Result.Count == 20,
                "full bounded delivery fits policy and no partial success");
        }
        bool Thrown = false;
        try { new Traversal.WorkPolicy(8, 2, 16, 1, 8, 100, 8, 64, 10); }
        catch (ArgumentOutOfRangeException) { Thrown = true; }
        Check(Thrown, "policy requires atomic final validation to fit turn envelope");
    }

    private static void TestCatalogEvidenceAndForgery()
    {
        using (var F = new Fixture(Count: 1))
        {
            Func<Model.MembershipCandidate, Traversal.CandidateObservation>[] Forgers = {
                // Valid-looking metadata without the model observation receipt.
                Candidate => new Traversal.CandidateObservation(Candidate, Candidate.Birth, 1, "assets/example.prefab", 0, 0, 0),
                Candidate => { var Value = F.Observe(Candidate); return new Traversal.CandidateObservation(Candidate,
                    Value.Birth + 1, Value.Id, Value.Prefab, Value.X, Value.Y, Value.Z); },
                Candidate => { var Value = F.Observe(Candidate); return new Traversal.CandidateObservation(Candidate,
                    Value.Birth, Value.Id + 1, Value.Prefab, Value.X, Value.Y, Value.Z); },
                Candidate => { var Value = F.Observe(Candidate); return new Traversal.CandidateObservation(Candidate,
                    Value.Birth, Value.Id, "assets/forged.prefab", Value.X, Value.Y, Value.Z); },
                Candidate => { var Value = F.Observe(Candidate); var Forged = new Model.MembershipCandidate(F.Model,
                    Candidate.Target, Candidate.State, Candidate.Slot, Candidate.Birth);
                    return new Traversal.CandidateObservation(Forged, Value.Birth, Value.Id, Value.Prefab, 0, 0, 0); }
            };
            foreach (var Forge in Forgers)
            {
                F.Start(Observe: Forge); F.Drain();
                Check(F.Results[F.Results.Count - 1].Status == Traversal.Outcome.InvalidObservation &&
                    F.Results[F.Results.Count - 1].Count == 0, "forged/unobserved candidate metadata rejected before result publication");
            }
            Check(F.ValidationCalls == 0, "forged samples never reach delivery validator");
            F.Start(); F.Drain();
            Check(F.Results[F.Results.Count - 1].Status == Traversal.Outcome.Success &&
                F.Results[F.Results.Count - 1].GetResult(0).Birth != 0,
                "valid direct receipt succeeds after malformed producer without retargeting");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(); F.Tick(); F.Entities[0].Id++;
            F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.StaleResult && F.Results[0].Count == 0,
                "same birth cannot follow changed keyed ID at delivery");
            F.Start(); F.Drain();
            Check(F.Results[1].Count == 0 && F.Encounters.Count == 1, "changed keyed evidence sticky retirement prevents re-admission");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(); F.Tick(); F.Entities[0].Prefab = "assets/replaced.prefab";
            F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.StaleResult && F.Results[0].Count == 0,
                "same birth cannot follow changed prefab at delivery");
        }
        using (var F = new Fixture(Count: 1))
        {
            ulong OldBirth = 0;
            F.Start(Observe: Candidate => { OldBirth = Candidate.Birth; return F.Observe(Candidate); });
            F.Drain(); F.Entities[0].Id++; F.Spawn(F.Entities[0]);
            F.Start(); F.Drain();
            Check(F.Results[1].Status == Traversal.Outcome.Success && F.Results[1].GetResult(0).Birth != OldBirth &&
                F.Results[1].GetResult(0).Id == 2, "new full Spawn creates new birth/evidence, never retargets old candidate");
        }
    }

    private static void TestDeliveryOrderingAndDelegateBoundaries()
    {
        using (var F = new Fixture(Count: 0, Deliveries: 1))
        {
            ulong First = F.Start(), Second = F.Start(2), Third = F.Start(3);
            F.Tick();
            Check(F.Results.Count == 0 && F.Scheduler.ActiveCount == 3, "completion always deferred out of scan turn");
            Check(F.Scheduler.Cancel(Second), "cancel ready delivery");
            F.Tick(); F.Tick(); F.Tick();
            Check(F.Results[0].RequestId == First && F.Results[1].RequestId == Second &&
                F.Results[2].RequestId == Third && F.Results[1].Status == Traversal.Outcome.Cancelled,
                "FIFO terminal delivery including cancellation");
        }
        using (var F = new Fixture(Count: 1))
        {
            int Calls = 0;
            ulong Id = 0;
            Id = F.Start(Observe: Candidate => {
                Calls++; F.Scheduler.Cancel(Id);
                Check(F.Scheduler.RunTurn(F.Now).Units == 0, "producer cannot recursively advance cursor");
                return F.Observe(Candidate);
            }); F.Drain();
            Check(Calls == 1 && F.Results[0].Status == Traversal.Outcome.Cancelled, "producer cancellation checked after observation");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(Observe: Candidate => {
                Traversal.CandidateObservation Value = F.Observe(Candidate);
                F.Authority = false; return Value;
            }); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Unauthorized && F.Results[0].Count == 0,
                "producer cannot publish after authority retirement");
        }
        using (var F = new Fixture(Count: 0))
        {
            ulong Id;
            Func<bool> ReentrantAuthority = () => {
                ulong Nested;
                Check(F.Scheduler.TryStart(F.Query(), 2, F.Observe, () => true, F.Validate, F.Results.Add, 0, out Nested)
                    == Traversal.StartStatus.Reentrant && Nested == 0, "authority cannot recursively admit before reservation");
                Check(F.Scheduler.RunTurn(0).Units == 0, "admission cannot recursively run owner turn");
                return true;
            };
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, ReentrantAuthority, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Accepted && Id != 0, "outer admission stable across predicate reentry");
            F.Scheduler.Cancel(Id); F.Drain();
        }
        using (var F = new Fixture(Count: 0))
        {
            F.Model.Dispose();
            ulong Id;
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => true, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Unavailable, "disposed model cannot accept empty traversal");
        }
    }

    private static void TestInterleavedAdmissionAndDeliveryLoss()
    {
        using (var F = new Fixture(Count: 3, Raw: 3))
        {
            int NewCalls = 0;
            bool Started = false;
            F.Start(Observe: Candidate => {
                if (!Started)
                {
                    Started = true;
                    F.Start(2, Observe: NewCandidate => { NewCalls++; return F.Observe(NewCandidate); });
                }
                return F.Observe(Candidate);
            });
            F.Tick();
            Check(NewCalls == 0 && F.Scheduler.ActiveCount == 2, "producer-admitted work waits for later turn");
            F.Drain();
            Check(NewCalls == 3 && F.Results.Count == 2 && F.Results[0].RequestId < F.Results[1].RequestId,
                "producer admission preserves FIFO readiness and samples each epoch once");
        }
        using (var F = new Fixture(Count: 1, Raw: 2))
        {
            F.Start(Callback: Result => { F.Results.Add(Result); F.Model.InvalidateCatalog(); });
            F.Start(2); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Success && F.Results[1].Status == Traversal.Outcome.CatalogLost &&
                F.Results[1].Count == 0, "earlier callback invalidation rejects later ready success");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(Validate: Observation => { F.Authority = false; return true; }); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.Unauthorized && F.Results[0].Count == 0,
                "authority rechecked after final host validation");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(Observe: Candidate => {
                Traversal.CandidateObservation Value = F.Observe(Candidate);
                F.Model.BreakObserverContinuity(); return Value;
            }); F.Drain();
            Check(F.Results[0].Status == Traversal.Outcome.CatalogLost, "producer continuity loss fails whole query immediately");
        }
        using (var F = new Fixture(Count: 0))
        {
            ulong Id;
            Check(F.Scheduler.TryStart(F.Query(), 0, F.Observe, () => true, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Invalid, "zero domain key invalid");
            Check(F.Scheduler.TryStart(F.Query(), 1, null, () => true, F.Validate, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Invalid, "producer mandatory");
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => true, null, F.Results.Add, 0, out Id)
                == Traversal.StartStatus.Invalid, "host final validation mandatory");
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => { throw new Exception("authority fault"); },
                F.Validate, F.Results.Add, 0, out Id) == Traversal.StartStatus.Unauthorized, "authority fault controlled before admission");
            F.Tick(10);
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => true, F.Validate, F.Results.Add, 9, out Id)
                == Traversal.StartStatus.Invalid, "admission clock cannot go backward");
            bool Thrown = false;
            try { F.Scheduler.RunTurn(9); } catch (ArgumentOutOfRangeException) { Thrown = true; }
            Check(Thrown, "owner turn clock cannot go backward");
        }
    }

    private static void TestBudgetGrid()
    {
        for (int Queries = 1; Queries <= 8; ++Queries)
            for (int RawBudget = 1; RawBudget <= 7; ++RawBudget)
                using (var F = new Fixture(Count: 6, Raw: RawBudget, MaximumResults: 6, Work: 13))
                {
                    // Two dead encounters create permanent holes for later scans.
                    F.Entities[1].Alive = F.Entities[4].Alive = false;
                    for (int Index = 0; Index < Queries; ++Index) F.Start((ulong)(Index / 2 + 1));
                    int Raw = 0;
                    int Turns = 0;
                    while (F.Scheduler.ActiveCount != 0 && Turns++ < 200)
                    {
                        Traversal.TurnWork Work = F.Tick(); Raw += Work.RawSlots;
                    }
                    Check(F.Scheduler.ActiveCount == 0 && Raw == Queries * 6 && F.Results.Count == Queries,
                        "budget grid complete raw traversal without duplicates");
                    foreach (Traversal.Completion Result in F.Results)
                        Check(Result.Status == Traversal.Outcome.Success && Result.Count == 4 && Result.RawSlots == 6,
                            "budget grid no skipped eligible member or partial success");
                }
    }

    private static void CheckReleased(Traversal Scheduler, object[] Captured)
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Array Requests = (Array)typeof(Traversal).GetField("Requests", Flags).GetValue(Scheduler);
        foreach (object Value in Requests) Check(Value == null, "unload request slot cleared");
        object Queue = typeof(Traversal).GetField("Delivery", Flags).GetValue(Scheduler);
        Check((int)Queue.GetType().GetProperty("Count").GetValue(Queue) == 0, "unload FIFO cleared");
        Check(typeof(Traversal).GetField("InFlight", Flags).GetValue(Scheduler) == null, "unload in-flight cleared");
        Array Scratch = (Array)typeof(Traversal).GetField("Scratch", Flags).GetValue(Scheduler);
        Check(Scratch.GetValue(0) == null && Scheduler.ActiveCount == 0, "unload scratch and count cleared");
        foreach (object Value in Captured)
        {
            if (Value == null) continue;
            foreach (string Name in new[] { "Callback", "Observe", "IsAuthorized", "ValidateResult", "Results", "Cursor" })
                Check(Value.GetType().GetField(Name, Flags).GetValue(Value) == null, "unload captured request reference cleared " + Name);
        }
    }

    private static object[] CaptureRequests(Traversal Scheduler)
    {
        Array Requests = (Array)typeof(Traversal).GetField("Requests", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Scheduler);
        var Captured = new object[Requests.Length];
        Requests.CopyTo(Captured, 0); return Captured;
    }

    private static void TestSilentUnload()
    {
        using (var F = new Fixture(Count: 3, Raw: 1))
        {
            for (ulong Domain = 1; Domain <= 4; ++Domain) { F.Start(Domain); F.Start(Domain); }
            F.Tick();
            object[] Captured = CaptureRequests(F.Scheduler);
            F.Scheduler.Dispose(); F.Scheduler.Dispose();
            CheckReleased(F.Scheduler, Captured);
            Check(F.Scheduler.RunTurn(F.Now + 1).Units == 0 && F.Results.Count == 0, "unload active scans silent and inert");
            ulong Id;
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => true, F.Validate, F.Results.Add, F.Now, out Id)
                == Traversal.StartStatus.Unavailable && Id == 0, "unload admission rejected");
            Check(F.Model.CatalogReady, "scheduler does not own model disposal");
        }
        using (var F = new Fixture(Count: 1, Raw: 2))
        {
            F.Start(); F.Start(2); F.Tick();
            object[] Captured = CaptureRequests(F.Scheduler);
            F.Scheduler.Dispose(); CheckReleased(F.Scheduler, Captured);
            F.Scheduler.RunTurn(F.Now + 1);
            Check(F.Results.Count == 0, "unload queued successful results emits no callback");
        }
        using (var F = new Fixture(Count: 1))
        {
            object[] Captured = null;
            F.Start(Observe: Candidate => {
                Captured = CaptureRequests(F.Scheduler);
                F.Scheduler.Dispose();
                return new Traversal.CandidateObservation(Traversal.CandidateStatus.Failure);
            });
            F.Tick(); CheckReleased(F.Scheduler, Captured);
            Check(F.Results.Count == 0, "producer disposal stops finishing/requeue");
        }
        using (var F = new Fixture(Count: 1))
        {
            F.Start(Validate: Observation => { F.Scheduler.Dispose(); return true; });
            object[] Captured = CaptureRequests(F.Scheduler);
            F.Tick(); F.Tick(); CheckReleased(F.Scheduler, Captured);
            Check(F.Results.Count == 0, "validator disposal clears detached in-flight request silently");
        }
        using (var F = new Fixture(Count: 1, Raw: 2))
        {
            F.Start(Callback: Result => { F.Results.Add(Result); F.Scheduler.Dispose(); });
            F.Start(2); F.Tick(); F.Tick();
            Check(F.Results.Count == 1 && F.Results[0].Count == 1 && F.Results[0].GetResult(0).Id == 1,
                "callback disposal suppresses later callbacks without revoking handed-off data");
            CheckReleased(F.Scheduler, new object[0]);
        }
        using (var F = new Fixture(Count: 1))
        {
            ulong Id;
            Check(F.Scheduler.TryStart(F.Query(), 1, F.Observe, () => { F.Scheduler.Dispose(); return true; }, F.Validate,
                F.Results.Add, 0, out Id) == Traversal.StartStatus.Unavailable && Id == 0,
                "authority disposal cannot publish partially admitted request");
        }
    }

    private static int Main(string[] Args)
    {
        try
        {
            if (Args.Length == 1 && Args[0] == "--benchmark")
            {
                RunBenchmark();
                return 0;
            }
            if (Args.Length != 0) throw new ArgumentException("Use no arguments or --benchmark.");
            TestSuccessAndFiltering(); TestAdmissionAndAuthority(); TestFairnessAndReentrancy();
            TestCapsCancellationAndLoss(); TestMembershipAndRetirement(); TestProducerFaultsAndOwner(); TestAggregateStress();
            TestDeliveryOrderingAndDelegateBoundaries();
            TestInterleavedAdmissionAndDeliveryLoss(); TestBudgetGrid();
            TestCatalogEvidenceAndForgery();
            TestDirectCatalogObserver();
            TestSilentUnload();
            TestPublicConversion();
            Console.WriteLine("[CarbonLuau:EntityDiscoveryModel] PASS Checks=" + Checks);
            return 0;
        }
        catch (Exception Error)
        {
            Console.Error.WriteLine("[CarbonLuau:EntityDiscoveryModel] FAIL " + Error);
            return 1;
        }
    }
}
