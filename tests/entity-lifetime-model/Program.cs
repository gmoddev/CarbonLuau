using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Model = Carbon.Plugins.EntityLifetimeModel;

internal static class Program
{
    private static int Checks;
    private static void Check(bool Condition, string Label)
    {
        Checks++;
        if (!Condition) throw new Exception(Label);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Model Directory = new Model();
        internal readonly object Entity = new object();
        internal object Occupant;
        internal ulong Id = 41;
        internal string Prefab = "assets/prefabs/example.prefab";
        internal bool Alive = true, FullySpawned = true, NetworkPresent = true, AuthorityActive = true;
        internal readonly Model.Authority Authority = new Model.Authority(1, 2, 3);

        internal Fixture(bool StartupComplete = true)
        {
            Occupant = Entity;
            Directory.BeginQualifiedObservation();
            if (StartupComplete) Check(Directory.QualifyStartupCompletion(), "startup completion qualified");
        }
        internal Model.HostEvidence Read(object Identity)
        { return new Model.HostEvidence(Alive, FullySpawned, NetworkPresent, Id, Prefab, Occupant); }
        internal bool Current(Model.Authority Value)
        { return AuthorityActive && Value.VmGeneration == 1 && Value.DomainLifetime == 2 && Value.PublicationLifetime == 3; }
        internal Model.SpawnAttempt Begin() { return Directory.BeginSpawn(Entity); }
        internal Model.Binding Admit()
        {
            Model.Binding Value;
            Check(Directory.TryAdmit(Entity, Authority, Current, Read, out Value), "admit");
            return Value;
        }
        internal Model.Binding CompleteAndAdmit()
        {
            Model.SpawnAttempt Attempt = Begin();
            Check(Attempt != null && Directory.CompleteSpawn(Attempt, true, true), "qualified Spawn completion");
            return Admit();
        }
        public void Dispose() { Directory.Dispose(); }
    }

    private static void TestObservationAndCompletion()
    {
        using (Fixture F = new Fixture())
        {
            Model.Binding Missing;
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing), "no implicit hotload baseline");
            Model.SpawnAttempt First = F.Begin();
            Check(First != null && First.Epoch == 1, "first outer epoch");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing), "pending is not admissible");
            Check(!F.Directory.CompleteSpawn(First, true, false), "skipped original fails closed");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing), "skipped remains inadmissible");
            Model.SpawnAttempt Second = F.Begin();
            Check(Second.Epoch == 2 && !F.Directory.CompleteSpawn(Second, false, true), "thrown full call fails closed");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing), "failed Spawn remains inadmissible despite keyed snapshot");
            Model.Binding Good = F.CompleteAndAdmit();
            Check(Good.Record.Epoch == 3 && Good.Record.Token == 1, "successful full-call token");
            Model.Binding Same = F.Admit();
            Check(ReferenceEquals(Good.Record, Same.Record), "one record for same object/epoch");
            Check(Model.SameLifetime(Good, Same), "same host lifetime across bindings");
            Check(F.Directory.Validate(Good, F.Current, F.Read), "current record valid");
            F.Occupant = null;
            F.Occupant = F.Entity;
            Check(F.Directory.Validate(Good, F.Current, F.Read), "unobserved same-object registry churn does not retarget");
            F.Occupant = null;
            Check(!F.Directory.Validate(Good, F.Current, F.Read), "observed registry loss stales record");
            F.Occupant = F.Entity;
            Check(!F.Directory.Validate(Good, F.Current, F.Read) && !F.Directory.Validate(Same, F.Current, F.Read), "retirement is sticky");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing), "stale record cannot be re-admitted in same epoch");
        }
    }

    private static void TestAbaAndEvidence()
    {
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            Model.SpawnAttempt Retry = F.Begin();
            Check(Old.Record.Retired && !F.Directory.Validate(Old, F.Current, F.Read), "outer entry fences old token immediately");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Model.Binding Pending), "same object remains pending");
            Check(!F.Directory.CompleteSpawn(Retry, false, true), "failed retry");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Pending), "failed retry leaves fully-spawned keyed object inadmissible");
            Model.Binding New = F.CompleteAndAdmit();
            Check(New.Record.Token > Old.Record.Token && New.Record.Epoch > Old.Record.Epoch, "same object/ID/prefab receives new identity");
            Check(!F.Directory.CompleteSpawn(Retry, true, true), "late completion cannot publish old attempt");
            F.Id = 42;
            Check(!F.Directory.Validate(New, F.Current, F.Read), "network ID change retires");
            Model.Binding AfterId = F.CompleteAndAdmit();
            F.Prefab = "assets/prefabs/replacement.prefab";
            Check(!F.Directory.Validate(AfterId, F.Current, F.Read), "prefab change retires");
            Model.Binding AfterPrefab = F.CompleteAndAdmit();
            F.Occupant = new object();
            Check(!F.Directory.Validate(AfterPrefab, F.Current, F.Read), "different object with same ID retires");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Value = F.CompleteAndAdmit();
            F.Alive = false;
            Check(!F.Directory.Validate(Value, F.Current, F.Read), "destroyed or Unity-invalid evidence retires");
        }
    }

    private static void TestStartupCompletionAndAuthority()
    {
        using (Fixture F = new Fixture(false))
        {
            Model.SpawnAttempt Restored = F.Begin();
            Check(Restored != null && F.Directory.CompleteSpawn(Restored, true, true),
                "restored entity has observed completed Spawn epoch");
            Check(F.Directory.HasCompletedObservation(F.Entity),
                "startup reconciliation sees completed restored entity");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read,
                out Model.Binding Missing), "restored entity waits for world-load completion");
            Check(F.Directory.QualifyStartupCompletion(), "world-load completion qualified");
            Model.Binding RestoredBinding = F.Admit();
            Check(RestoredBinding.Record.Epoch == 1 && F.Directory.Validate(RestoredBinding, F.Current, F.Read),
                "observed completed restored entity admits after world-load marker");
            object Unseen = new object();
            Check(!F.Directory.HasCompletedObservation(Unseen),
                "startup reconciliation rejects unseen registry occupant");
            F.Occupant = Unseen;
            Check(!F.Directory.TryAdmit(Unseen, F.Authority, F.Current, F.Read, out Missing),
                "unseen fully-spawned keyed object cannot admit after world-load marker");
            F.Occupant = F.Entity;
            Model.SpawnAttempt Later = F.Begin();
            Check(!F.Directory.HasCompletedObservation(F.Entity),
                "pending later Spawn is not completed startup evidence");
            Check(!F.Directory.Validate(RestoredBinding, F.Current, F.Read), "restored lifetime retired by later Spawn start");
            Check(F.Directory.CompleteSpawn(Later, true, true), "later completed Spawn");
            Model.Binding Current = F.Admit();
            F.AuthorityActive = false;
            Check(!F.Directory.Validate(Current, F.Current, F.Read), "domain/VM/publication predicate stales binding");
            F.AuthorityActive = true;
            Check(!F.Directory.Validate(Current, F.Current, F.Read), "old binding never revives");
            Model.Binding OtherDomain = F.Admit();
            Check(ReferenceEquals(OtherDomain.Record, Current.Record), "authority retirement does not retire shared host lifetime");
            F.Directory.BreakObserverContinuity();
            Check(!F.Directory.Validate(OtherDomain, F.Current, F.Read), "observer gap stales all prior records");
            Check(OtherDomain.Record.Retired, "observer-gap retirement latches on validation");
            F.Directory.BeginQualifiedObservation();
            Check(!F.Directory.QualifyStartupCompletion(), "observer gap cannot be requalified in the same host");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing),
                "observer restart does not invent an epoch");
            Model.SpawnAttempt PostGap = F.Begin();
            Check(PostGap == null || !F.Directory.CompleteSpawn(PostGap, true, true),
                "post-gap Spawn cannot restore this host baseline");
            F.Directory.Dispose();
            Check(!F.Directory.Validate(OtherDomain, F.Current, F.Read), "host retirement keeps old record stale");
        }
        using (Fixture F = new Fixture())
        {
            Check(!F.Directory.TryAdmit(F.Entity, new Model.Authority(1, 0, 3), F.Current, F.Read,
                out Model.Binding Missing), "incomplete authority cannot admit");
            Model.SpawnAttempt First = F.Begin();
            Model.SpawnAttempt Nested = F.Begin();
            Check(Nested == null && !F.Directory.CompleteSpawn(First, true, true), "nested same-object attempt poisons admission");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing), "poisoned object cannot admit");
        }
        using (Fixture F = new Fixture(false))
        {
            Model.SpawnAttempt Failed = F.Begin();
            Check(!F.Directory.CompleteSpawn(Failed, false, true), "restoration Spawn failed");
            Check(F.Directory.QualifyStartupCompletion(), "world-load marker after failed Spawn");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read,
                out Model.Binding Missing), "failed restored entity stays inadmissible despite valid present state");
        }
    }

    private static void TestBoundsAndOwnership()
    {
        using (Fixture F = new Fixture())
        {
            Model.Binding Value = F.CompleteAndAdmit();
            F.Prefab = new string('a', 513);
            Check(!F.Directory.Validate(Value, F.Current, F.Read), "overlong prefab retires");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Value = F.CompleteAndAdmit();
            F.NetworkPresent = false;
            Check(!F.Directory.Validate(Value, F.Current, F.Read), "missing network object retires");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Value = F.CompleteAndAdmit();
            F.FullySpawned = false;
            Check(!F.Directory.Validate(Value, F.Current, F.Read), "not fully spawned retires");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Value = F.CompleteAndAdmit();
            F.Id = 0;
            Check(!F.Directory.Validate(Value, F.Current, F.Read), "zero network ID retires");
        }
        using (Fixture F = new Fixture())
        {
            F.Prefab = "invalid\ud800";
            Model.SpawnAttempt Attempt = F.Begin();
            Check(F.Directory.CompleteSpawn(Attempt, true, true), "host completion independent of bad prefab");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read,
                out Model.Binding Missing), "malformed UTF-8 prefab rejected");
        }
        using (Fixture F = new Fixture())
        {
            typeof(Model).GetField("NextToken", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(F.Directory, ulong.MaxValue);
            Model.SpawnAttempt Attempt = F.Begin();
            Check(F.Directory.CompleteSpawn(Attempt, true, true), "epoch before token exhaustion");
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read,
                out Model.Binding Missing), "token overflow fails closed");
        }
        using (Fixture F = new Fixture())
        {
            Model.SpawnAttempt Attempt = F.Begin();
            Attempt.State.Epoch = ulong.MaxValue;
            Check(F.Begin() == null, "epoch overflow fails closed");
            Check(!F.Directory.CompleteSpawn(Attempt, true, true), "overflowed prior attempt stays invalid");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            typeof(Model).GetField("ObservationGeneration", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(F.Directory, ulong.MaxValue);
            F.Directory.BreakObserverContinuity();
            Check(!F.Directory.Validate(Old, F.Current, F.Read),
                "observation-generation overflow retires old lifetime");
            F.Directory.BeginQualifiedObservation();
            Check(!F.Directory.QualifyStartupCompletion(),
                "observation-generation overflow cannot requalify host");
        }
        using (Fixture F = new Fixture())
        {
            bool Rejected = false;
            Thread Other = new Thread(() => { try { F.Begin(); } catch (InvalidOperationException) { Rejected = true; } });
            Other.Start(); Other.Join();
            Check(Rejected, "off-owner-thread fence rejected");
        }
        using (Fixture F = new Fixture())
        {
            Model.SpawnAttempt Attempt = F.Begin();
            CheckWrongThread(() => F.Directory.CompleteSpawn(Attempt, true, true), "completion owner thread");
            Check(F.Directory.CompleteSpawn(Attempt, true, true), "owner thread still completes after rejected call");
            CheckWrongThread(() => { Model.Binding Ignored; F.Directory.TryAdmit(F.Entity,
                F.Authority, F.Current, F.Read, out Ignored); }, "admission owner thread");
            Model.Binding Value = F.Admit();
            CheckWrongThread(() => F.Directory.Validate(Value, F.Current, F.Read), "validation owner thread");
            CheckWrongThread(() => F.Directory.BreakObserverContinuity(), "observer-gap owner thread");
            CheckWrongThread(() => F.Directory.Dispose(), "dispose owner thread");
            Check(F.Directory.Validate(Value, F.Current, F.Read), "rejected off-thread operations leave lifetime intact");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            F.Directory.BreakObserverContinuity();
            F.Directory.BeginQualifiedObservation();
            Check(!F.Directory.QualifyStartupCompletion(), "completion marker cannot repair observer gap");
            Check(!F.Directory.Validate(Old, F.Current, F.Read), "observer gap invalidates admitted token");
            using (Fixture Other = new Fixture())
            {
                Model.Binding OtherValue = Other.CompleteAndAdmit();
                Check(!Model.SameLifetime(Old, OtherValue), "token collision across hosts is not lifetime equality");
                Check(!Other.Directory.Validate(Old, Other.Current, Other.Read), "foreign model cannot validate binding");
            }
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Value = F.CompleteAndAdmit();
            Check(!F.Directory.TryAdmit(F.Entity, new Model.Authority(1, 2, 4), F.Current, F.Read,
                out Model.Binding Missing), "publication authority required at admission");
            Check(F.Directory.Validate(Value, F.Current, F.Read), "failed foreign admission does not retire host record");
        }
    }

    private static void CheckWrongThread(Action Operation, string Label)
    {
        bool Rejected = false;
        Thread Other = new Thread(() => { try { Operation(); } catch (InvalidOperationException) { Rejected = true; } });
        Other.Start(); Other.Join();
        Check(Rejected, Label);
    }

    private static void TestReentrantEvidence()
    {
        using (Fixture F = new Fixture())
        {
            Model.SpawnAttempt Initial = F.Begin();
            Check(F.Directory.CompleteSpawn(Initial, true, true), "initial completed for reentry");
            bool Admitted = F.Directory.TryAdmit(F.Entity, F.Authority, F.Current,
                Identity => { F.Begin(); return F.Read(Identity); }, out Model.Binding Missing);
            Check(!Admitted && Missing == null, "Spawn during evidence read cannot publish old epoch");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            Check(!F.Directory.Validate(Old, F.Current,
                Identity => { F.Begin(); return F.Read(Identity); }), "Spawn during validation cannot validate old epoch");
            Check(Old.Record.Retired, "reentrant Spawn immediately retires old record");
        }
        using (Fixture F = new Fixture())
        {
            Model.SpawnAttempt Initial = F.Begin();
            Check(F.Directory.CompleteSpawn(Initial, true, true), "completed for authority reentry");
            int Calls = 0;
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority,
                Value => { if (++Calls == 2) F.Begin(); return true; }, F.Read, out Model.Binding Missing),
                "authority reentry cannot publish old epoch");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            int Calls = 0;
            Check(!F.Directory.Validate(Old,
                Value => { if (++Calls == 2) F.Begin(); return true; }, F.Read),
                "authority reentry cannot validate old epoch");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            Check(!F.Directory.Validate(Old, F.Current,
                Identity => { F.Directory.Retire(Old); return F.Read(Identity); }),
                "retirement during evidence read cannot validate binding");
        }
        using (Fixture F = new Fixture())
        {
            Model.Binding Old = F.CompleteAndAdmit();
            int Calls = 0;
            Check(!F.Directory.Validate(Old,
                Value => { if (++Calls == 2) F.Directory.Retire(Old); return true; }, F.Read),
                "retirement during authority recheck cannot validate binding");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Tuple<WeakReference, Model, Model.Binding> MakeWeakFixture()
    {
        Model Directory = new Model();
        object Entity = new object();
        Directory.BeginQualifiedObservation();
        Check(Directory.QualifyStartupCompletion(), "weak fixture startup completion");
        Model.SpawnAttempt Attempt = Directory.BeginSpawn(Entity);
        Check(Directory.CompleteSpawn(Attempt, true, true), "weak fixture completed");
        Model.Binding Value;
        Check(Directory.TryAdmit(Entity, new Model.Authority(1, 2, 3), _ => true,
            Identity => new Model.HostEvidence(true, true, true, 1, "prefab", Identity), out Value), "weak fixture admitted");
        return Tuple.Create(new WeakReference(Entity), Directory, Value);
    }

    private static void TestWeakIdentity()
    {
        Tuple<WeakReference, Model, Model.Binding> Fixture = MakeWeakFixture();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Check(!Fixture.Item1.IsAlive && Fixture.Item3.Record.Target == null,
            "retained record and ConditionalWeakTable do not retain host object");
        Check(!Fixture.Item2.Validate(Fixture.Item3, _ => true,
            _ => new Model.HostEvidence(true, true, true, 1, "prefab", null)), "collected object cannot validate");
        Fixture.Item2.Dispose();
    }

    private static int Main()
    {
        try
        {
            TestObservationAndCompletion();
            TestAbaAndEvidence();
            TestStartupCompletionAndAuthority();
            TestBoundsAndOwnership();
            TestReentrantEvidence();
            TestWeakIdentity();
            Console.WriteLine("[CarbonLuau:EntityLifetimeModel] PASS (" + Checks + " checks)");
            return 0;
        }
        catch (Exception Error)
        {
            Console.Error.WriteLine("[CarbonLuau:EntityLifetimeModel] FAIL: " + Error);
            return 1;
        }
    }
}
