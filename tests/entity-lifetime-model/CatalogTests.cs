using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Model = Carbon.Plugins.EntityLifetimeModel;

internal static partial class Program
{
    private static Model.SpawnAttempt CompleteEpoch(Model Directory, object Identity)
    {
        Model.SpawnAttempt Attempt = Directory.BeginSpawn(Identity);
        Check(Attempt != null && Directory.CompleteSpawn(Attempt, true, true), "catalog completed epoch");
        return Attempt;
    }

    private static Model.MembershipCursor BeginScan(Model Directory)
    {
        Model.MembershipCursor Cursor;
        Check(Directory.BeginCatalogScan(out Cursor), "catalog scan accepted");
        return Cursor;
    }

    private static Model.MembershipCandidate[] ScanAll(Model Directory, int Budget)
    {
        Model.MembershipCursor Cursor = BeginScan(Directory);
        var Scratch = new Model.MembershipCandidate[Budget];
        var Results = new List<Model.MembershipCandidate>();
        while (!Cursor.Complete)
        {
            int Written;
            int Inspected = Directory.InspectCatalog(Cursor, Budget, Scratch, out Written);
            Check(Inspected > 0 && Inspected <= Budget, "scan respects raw-slot budget");
            for (int Index = 0; Index < Written; ++Index) Results.Add(Scratch[Index]);
        }
        Check(Directory.IsCatalogScanCurrent(Cursor), "completed scan still current");
        return Results.ToArray();
    }

    private static void TestCatalogEnrollment()
    {
        using (var Directory = new Model())
        {
            Directory.BeginQualifiedObservation();
            CompleteEpoch(Directory, new object());
            Check(Directory.QualifyStartupCompletion(), "default F1 startup");
            Check(!Directory.CatalogComplete && !Directory.CatalogReady && Directory.CatalogSlotCount == 0,
                "default model allocates no catalog");
            Model.MembershipCursor Missing;
            Check(!Directory.BeginCatalogScan(out Missing), "default discovery unavailable");
        }
        using (var Directory = new Model(4))
        {
            object A = new object();
            Directory.BeginQualifiedObservation();
            Model.SpawnAttempt First = Directory.BeginSpawn(A);
            Check(!Directory.HasCatalogObservation(A), "pending has no membership");
            Check(Directory.CompleteSpawn(First, true, true), "startup completed epoch enrolls");
            Check(Directory.HasCatalogObservation(A) && Directory.CatalogSlotCount == 1 && !Directory.CatalogReady,
                "untouched startup enrollment before readiness");
            Check(!Directory.CompleteSpawn(First, true, true) && Directory.CatalogSlotCount == 1,
                "duplicate completion cannot enroll twice");
            var Tokens = (System.Collections.IDictionary)typeof(Model).GetField("TokenRecords",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Directory);
            Check(Tokens.Count == 0, "membership is independent of lazy token table");
            Check(Directory.QualifyStartupCompletion(), "catalog startup readiness");
            Model.MembershipCandidate Old = ScanAll(Directory, 1)[0];
            Model.SpawnAttempt Failed = Directory.BeginSpawn(A);
            Check(!Directory.IsCatalogCandidateCurrent(Old) && Directory.CatalogSlotCount == 0,
                "BeginSpawn synchronously fences unadmitted epoch");
            Check(!Directory.CompleteSpawn(Failed, false, true), "throw cannot enroll");
            Model.SpawnAttempt Skipped = Directory.BeginSpawn(A);
            Check(!Directory.CompleteSpawn(Skipped, true, false), "skipped cannot enroll");
            Check(ScanAll(Directory, 1).Length == 0, "failed keyed-looking epochs absent");
            CompleteEpoch(Directory, A);
            Model.MembershipCandidate Fresh = ScanAll(Directory, 1)[0];
            Check(Fresh.Birth > Old.Birth && Fresh.Epoch > Old.Epoch && Fresh.Slot == Old.Slot,
                "same object epoch ABA has fresh birth in reused slot");
            Check(ReferenceEquals(Old.Target, A) && Old.Epoch == 1 && !Directory.IsCatalogCandidateCurrent(Old),
                "queued candidate immutable across respawn");
            Check(!Directory.CompleteSpawn(Failed, true, true), "late completion stays stale");
            object Nested = new object();
            Model.SpawnAttempt Outer = Directory.BeginSpawn(Nested);
            Check(Directory.BeginSpawn(Nested) == null && !Directory.CompleteSpawn(Outer, true, true) &&
                !Directory.HasCatalogObservation(Nested), "nested same-object poison never enrolls");
        }
    }

    private static void TestCatalogRetirement()
    {
        using (Fixture F = new Fixture(true, 4))
        {
            CompleteEpoch(F.Directory, F.Entity);
            Model.MembershipCandidate Candidate = ScanAll(F.Directory, 1)[0];
            F.Occupant = null;
            F.NetworkPresent = false;
            F.FullySpawned = false;
            Check(F.Directory.SweepCatalog(1) == 1 && F.Directory.IsCatalogCandidateCurrent(Candidate),
                "weak-only sweep preserves unadmitted transient host ineligibility");
            F.Occupant = F.Entity;
            F.NetworkPresent = F.FullySpawned = true;
            Model.Binding Binding = F.Admit();
            F.Directory.Retire(Binding);
            Check(F.Directory.HasCatalogObservation(F.Entity) && !Binding.Record.Retired,
                "binding-only retirement preserves host membership");
            Model.Binding Another = F.Admit();
            F.AuthorityActive = false;
            Check(!F.Directory.Validate(Another, F.Current, F.Read) && !Another.Record.Retired &&
                F.Directory.IsCatalogCandidateCurrent(Candidate), "authority loss preserves membership");
            F.AuthorityActive = true;
            Model.Binding Live = F.Admit();
            F.Occupant = null;
            Check(!F.Directory.Validate(Live, F.Current, F.Read) && F.Directory.CatalogSlotCount == 0,
                "observed admitted occupancy loss frees exact slot");
            Model.ObjectState State;
            var States = (System.Runtime.CompilerServices.ConditionalWeakTable<object, Model.ObjectState>)typeof(Model)
                .GetField("States", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(F.Directory);
            Check(States.TryGetValue(F.Entity, out State) && ReferenceEquals(State.Current, Live.Record) && Live.Record.Retired,
                "retired record remains the state's sticky tombstone");
            F.Occupant = F.Entity;
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Model.Binding Missing) &&
                !F.Directory.IsCatalogCandidateCurrent(Candidate), "same-epoch tombstone prevents remint after slot removal");
            Model.Binding New = F.CompleteAndAdmit();
            Check(New.Record.Token > Live.Record.Token && F.Directory.HasCatalogObservation(F.Entity),
                "new Spawn reacquires membership and token");
            F.Prefab = "changed";
            Check(!F.Directory.Validate(New, F.Current, F.Read) && F.Directory.CatalogSlotCount == 0,
                "prefab mismatch propagates sticky retirement");
        }
        using (Fixture F = new Fixture(true, 2))
        {
            CompleteEpoch(F.Directory, F.Entity);
            F.Occupant = null;
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Model.Binding Missing) &&
                F.Directory.CatalogSlotCount == 0, "actual failed unadmitted admission releases slot");
            F.Occupant = F.Entity;
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, F.Read, out Missing),
                "failed unadmitted epoch cannot retry without Spawn");
            CompleteEpoch(F.Directory, F.Entity);
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current,
                Identity => { throw new Exception("fixture evidence"); }, out Missing) &&
                F.Directory.CatalogSlotCount == 0, "unadmitted evidence exception releases failed epoch");
        }
    }

    private static void TestCatalogReentrantRetirement()
    {
        using (Fixture F = new Fixture(true, 1))
        {
            Model.Binding Old = F.CompleteAndAdmit();
            Model.MembershipCandidate Queued = ScanAll(F.Directory, 1)[0];
            Model.Binding Fresh = null;
            Check(!F.Directory.Validate(Old, F.Current, Identity => {
                Fresh = F.CompleteAndAdmit();
                throw new Exception("old read throws after new successful Spawn");
            }), "old-record reentrant exception rejected");
            Check(Old.Record.Retired && F.Directory.Validate(Fresh, F.Current, F.Read) &&
                F.Directory.CatalogSlotCount == 1 && F.Directory.HasCatalogObservation(F.Entity),
                "old retirement cannot release new same-object same-slot epoch");
            Check(!F.Directory.IsCatalogCandidateCurrent(Queued) && ReferenceEquals(Queued.Target, F.Entity),
                "old queued candidate stays bound to old epoch");
            Model.Binding Before = Fresh;
            Check(!F.Directory.TryAdmit(F.Entity, F.Authority, F.Current, Identity => {
                Fresh = F.CompleteAndAdmit();
                throw new Exception("old admission throws after respawn");
            }, out Model.Binding Missing) && F.Directory.Validate(Fresh, F.Current, F.Read) &&
                F.Directory.CatalogSlotCount == 1 && Before.Record.Retired,
                "reentrant admission catch cannot fail new epoch");
        }
    }

    private static void TestCatalogOverflowAndContinuity()
    {
        using (Fixture F = new Fixture(true, 1))
        {
            Model.Binding First = F.CompleteAndAdmit();
            Model.MembershipCursor Pending = BeginScan(F.Directory);
            object Lost = new object();
            CompleteEpoch(F.Directory, Lost);
            Check(!F.Directory.CatalogComplete && !F.Directory.CatalogReady &&
                F.Directory.Validate(First, F.Current, F.Read), "capacity loss disables only discovery");
            Check(F.Directory.TryAdmit(Lost, F.Authority, F.Current,
                Identity => new Model.HostEvidence(true, true, true, 99, "prefab", Identity), out Model.Binding Point),
                "F1 lookup remains available for unenrolled overflow epoch");
            int Written;
            Check(F.Directory.InspectCatalog(Pending, 1, new Model.MembershipCandidate[1], out Written) == 0 &&
                Pending.Invalid, "overflow invalidates accepted scan");
            F.Occupant = null;
            Check(!F.Directory.Validate(First, F.Current, F.Read), "retirement frees overflowed catalog slot");
            Check(!F.Directory.CatalogComplete && F.Directory.CatalogSlotCount == 0,
                "free slot cannot repair lost enrollment");
        }
        using (Fixture F = new Fixture(false, 1))
        {
            CompleteEpoch(F.Directory, F.Entity);
            CompleteEpoch(F.Directory, new object());
            Check(F.Directory.QualifyStartupCompletion() && !F.Directory.CatalogReady,
                "startup overflow preserves F1 readiness but blocks discovery");
        }
        using (Fixture F = new Fixture(true, 1))
        {
            Model.Binding First = F.CompleteAndAdmit();
            typeof(Model).GetField("CatalogSequence", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(F.Directory, ulong.MaxValue);
            Model.Binding Next = F.CompleteAndAdmit();
            Check(!F.Directory.CatalogComplete && F.Directory.Validate(Next, F.Current, F.Read) && First.Record.Retired,
                "birth overflow cannot wrap or exhaust F1 tokens");
        }
        using (Fixture F = new Fixture(true, 1))
        {
            Model.Binding First = F.CompleteAndAdmit();
            F.Directory.InvalidateCatalog();
            Check(!F.Directory.CatalogReady && F.Directory.Validate(First, F.Current, F.Read),
                "explicit catalog fault leaves F1 intact");
        }
        using (Fixture F = new Fixture(true, 2))
        {
            Model.Binding First = F.CompleteAndAdmit();
            // Fault the bounded free pool to exercise real enrollment exception
            // containment without allocation pressure or a production test hook.
            int[] Free = (int[])typeof(Model).GetField("CatalogFree", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(F.Directory);
            Free[0] = int.MaxValue;
            object Lost = new object();
            CompleteEpoch(F.Directory, Lost);
            Check(!F.Directory.CatalogComplete && F.Directory.Validate(First, F.Current, F.Read) &&
                F.Directory.TryAdmit(Lost, F.Authority, F.Current,
                    Identity => new Model.HostEvidence(true, true, true, 99, "prefab", Identity), out Model.Binding Point),
                "enrollment exception latches discovery loss and preserves F1");
            GC.KeepAlive(Lost);
        }
        using (var Directory = new Model(1))
        {
            Directory.BeginQualifiedObservation();
            Check(Directory.QualifyStartupCompletion(), "empty catalog startup");
            Model.MembershipCursor Empty = BeginScan(Directory);
            Check(Empty.Complete && Directory.IsCatalogScanCurrent(Empty), "empty cursor captures qualified host");
            Directory.InvalidateCatalog();
            Check(!Directory.IsCatalogScanCurrent(Empty), "empty completion cannot bypass catalog fault");
        }
        using (Fixture F = new Fixture(true, 1))
        {
            CompleteEpoch(F.Directory, F.Entity);
            Model.MembershipCandidate Candidate = ScanAll(F.Directory, 1)[0];
            Model.MembershipCursor Pending = BeginScan(F.Directory);
            F.Directory.BreakObserverContinuity();
            Check(!F.Directory.IsCatalogScanCurrent(Pending) && !F.Directory.IsCatalogCandidateCurrent(Candidate),
                "observer gap invalidates cursor and candidates immediately");
            F.Directory.BeginQualifiedObservation();
            Check(!F.Directory.QualifyStartupCompletion() && !F.Directory.CatalogReady &&
                F.Directory.SweepCatalog(1) == 1 && F.Directory.CatalogSlotCount == 0,
                "gap cannot requalify and weak sweep reclaims old generation");
            using (var Restart = new Model(1))
            {
                Restart.BeginQualifiedObservation();
                CompleteEpoch(Restart, F.Entity);
                Check(Restart.QualifyStartupCompletion() && Restart.CatalogReady &&
                    !Restart.IsCatalogCandidateCurrent(Candidate) && !Restart.IsCatalogScanCurrent(Pending),
                    "fresh host model qualifies without accepting previous host candidates");
                F.Directory.Dispose();
                Check(!F.Directory.CatalogReady, "dispose keeps discovery unavailable");
            }
        }
    }

    private static void TestCatalogSlotsAndWatermark()
    {
        // Production-linked counterpart of the finite cursor/removal proof.
        for (int Capacity = 1; Capacity <= 5; ++Capacity)
        for (int Mask = 0; Mask < (1 << Capacity); ++Mask)
        for (int Cut = 0; Cut <= Capacity; ++Cut)
        using (var Directory = new Model(Capacity))
        {
            Directory.BeginQualifiedObservation();
            var Initial = new object[Capacity];
            for (int Index = 0; Index < Capacity; ++Index) { Initial[Index] = new object(); CompleteEpoch(Directory, Initial[Index]); }
            Check(Directory.QualifyStartupCompletion(), "finite catalog startup");
            Model.MembershipCursor Cursor = BeginScan(Directory);
            var Scratch = new Model.MembershipCandidate[Capacity];
            int Written;
            Check(Directory.InspectCatalog(Cursor, Cut, Scratch, out Written) == Cut && Written == Cut,
                "finite initial cursor budget");
            var Queued = new Model.MembershipCandidate[Written];
            Array.Copy(Scratch, Queued, Written);
            var New = new List<object>();
            for (int Index = 0; Index < Capacity; ++Index)
                if ((Mask & (1 << Index)) != 0)
                {
                    Directory.BeginSpawn(Initial[Index]);
                    object Identity = new object(); New.Add(Identity); CompleteEpoch(Directory, Identity);
                }
            for (int Index = Cut; Index < Capacity; ++Index)
            {
                Check(Directory.InspectCatalog(Cursor, 1, Scratch, out Written) == 1,
                    "raw slot costs one even when hole or post-watermark birth");
                bool Survives = (Mask & (1 << Index)) == 0;
                Check(Written == (Survives ? 1 : 0) && (!Survives || ReferenceEquals(Scratch[0].Target, Initial[Index])),
                    "surviving cohort visited once, reused slot excluded");
            }
            Check(Cursor.Complete && Directory.IsCatalogScanCurrent(Cursor), "finite cursor reaches captured extent");
            for (int Index = 0; Index < Queued.Length; ++Index)
                Check(ReferenceEquals(Queued[Index].Target, Initial[Index]) &&
                    Directory.IsCatalogCandidateCurrent(Queued[Index]) == ((Mask & (1 << Index)) == 0),
                    "immutable queued weak holder never retargets slot replacement");
            Check(Directory.CatalogComplete && Directory.CatalogSlotCount == Capacity, "finite fixed pool conserved");
            GC.KeepAlive(Initial); GC.KeepAlive(New);
        }
        using (var Directory = new Model(3))
        {
            Directory.BeginQualifiedObservation();
            object A = new object(), B = new object();
            CompleteEpoch(Directory, A); CompleteEpoch(Directory, B);
            Check(Directory.QualifyStartupCompletion(), "holes startup");
            Model.MembershipCursor Cursor = BeginScan(Directory);
            Directory.BeginSpawn(A);
            var Scratch = new Model.MembershipCandidate[3];
            int Written;
            Check(Directory.InspectCatalog(Cursor, 0, Scratch, out Written) == 0 && Cursor.Position == 0,
                "zero budget inspects nothing");
            Check(Directory.InspectCatalog(Cursor, 1, Scratch, out Written) == 1 && Written == 0,
                "empty hole consumes raw budget");
            object C = new object(); CompleteEpoch(Directory, C);
            object D = new object(); CompleteEpoch(Directory, D);
            Check(Directory.InspectCatalog(Cursor, 3, Scratch, out Written) == 1 && Written == 1 &&
                ReferenceEquals(Scratch[0].Target, B) && Cursor.Complete,
                "post-acceptance extent growth cannot enter old cursor");
            GC.KeepAlive(A); GC.KeepAlive(B); GC.KeepAlive(C); GC.KeepAlive(D);
        }
        using (Fixture F = new Fixture(true, 1))
        {
            Model.Binding Old = F.CompleteAndAdmit();
            Model.MembershipCandidate Queued = ScanAll(F.Directory, 1)[0];
            object Replacement = new object();
            F.Occupant = Replacement;
            Check(!F.Directory.Validate(Old, F.Current, F.Read), "different object same ID retires old record");
            CompleteEpoch(F.Directory, Replacement);
            Check(F.Directory.TryAdmit(Replacement, F.Authority, F.Current, F.Read, out Model.Binding New),
                "different object same ID and prefab admits its own lifetime");
            Model.MembershipCandidate Fresh = ScanAll(F.Directory, 1)[0];
            Check(Fresh.Slot == Queued.Slot && Fresh.Birth > Queued.Birth &&
                Fresh.Epoch == Queued.Epoch && New.Record.NetworkId == Old.Record.NetworkId &&
                New.Record.Token != Old.Record.Token && ReferenceEquals(Queued.Target, F.Entity) &&
                !F.Directory.IsCatalogCandidateCurrent(Queued),
                "same ID prefab epoch and slot cannot retarget immutable queued candidate");
            GC.KeepAlive(Replacement);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Model.MembershipCandidate PopulateWeakCatalog(Model Directory, int Count)
    {
        Model.MembershipCandidate Last = null;
        for (int Index = 0; Index < Count; ++Index)
        {
            object Identity = new object(); CompleteEpoch(Directory, Identity);
            Model.MembershipCandidate[] Seen = ScanAll(Directory, Count);
            Last = Seen[Seen.Length - 1];
            GC.KeepAlive(Identity);
        }
        return Last;
    }

    private static void TestCatalogWeakConvergence()
    {
        using (var Directory = new Model(32))
        {
            Directory.BeginQualifiedObservation();
            Check(Directory.QualifyStartupCompletion(), "weak catalog startup");
            Model.MembershipCandidate Queued = PopulateWeakCatalog(Directory, 32);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Check(Queued.Target == null, "catalog and queued candidate do not retain host object");
            for (int Pass = 0; Pass < 32; ++Pass)
                Check(Directory.SweepCatalog(1) == 1, "weak sweep has hard raw-slot budget");
            Check(Directory.CatalogSlotCount == 0 && Directory.CatalogComplete,
                "weak cleanup converges without losing completeness");
            object Live = new object(); CompleteEpoch(Directory, Live);
            Check(Directory.HasCatalogObservation(Live) && !Directory.IsCatalogCandidateCurrent(Queued),
                "weak reclaimed slot reuse cannot revive old queued candidate");
            CheckWrongThread(() => Directory.SweepCatalog(1), "catalog sweep owner thread");
            CheckWrongThread(() => { Model.MembershipCursor Ignored; Directory.BeginCatalogScan(out Ignored); },
                "catalog scan owner thread");
            Check(Directory.HasCatalogObservation(Live), "rejected wrong-thread calls preserve membership");
            GC.KeepAlive(Live);
        }
    }

    private static void TestMembershipCatalog()
    {
        TestCatalogEnrollment();
        TestCatalogRetirement();
        TestCatalogReentrantRetirement();
        TestCatalogOverflowAndContinuity();
        TestCatalogSlotsAndWatermark();
        TestCatalogWeakConvergence();
        TestCatalogArgumentBounds();
        TestCatalogConstructorAndDisposal();
        TestCatalogCapturedState();
    }

    private static void TestCatalogCapturedState()
    {
        using (Fixture F = new Fixture(true, 1))
        {
            Model.Binding Binding = F.CompleteAndAdmit();
            Model.MembershipCandidate Old = ScanAll(F.Directory, 1)[0];
            var States = (ConditionalWeakTable<object, Model.ObjectState>)typeof(Model)
                .GetField("States", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(F.Directory);
            Model.ObjectState Captured;
            Check(States.TryGetValue(F.Entity, out Captured) && ReferenceEquals(Old.State, Captured) &&
                ReferenceEquals(Old.State.Current, Binding.Record), "candidate captures original canonical ObjectState");
            Check(typeof(Model.MembershipCandidate).GetField("State", BindingFlags.Instance | BindingFlags.NonPublic).IsInitOnly,
                "candidate state reference cannot retarget");

            // Populate the CWT independently of catalog capacity. These failed
            // and pending objects must have no influence on raw-slot work.
            var Unenrolled = new object[4096];
            for (int Index = 0; Index < Unenrolled.Length; ++Index)
            {
                Unenrolled[Index] = new object();
                Model.SpawnAttempt Attempt = F.Directory.BeginSpawn(Unenrolled[Index]);
                if ((Index & 1) == 0)
                    Check(!F.Directory.CompleteSpawn(Attempt, false, true), "unrelated failed epoch outside fixed pool");
            }
            Model.MembershipCursor Cursor = BeginScan(F.Directory);
            var Scratch = new Model.MembershipCandidate[1];
            int Written;
            Check(F.Directory.InspectCatalog(Cursor, 1, Scratch, out Written) == 1 && Written == 1 &&
                ReferenceEquals(Scratch[0], Old) && F.Directory.SweepCatalog(1) == 1,
                "one raw slot opportunity independent of larger CWT population");

            CompleteEpoch(F.Directory, F.Entity);
            Model.MembershipCandidate Respawned = ScanAll(F.Directory, 1)[0];
            Check(ReferenceEquals(Respawned.State, Captured) && ReferenceEquals(Old.State, Captured) &&
                Respawned.Epoch > Old.Epoch && !F.Directory.IsCatalogCandidateCurrent(Old),
                "same-object reuse mutates captured state but never old candidate epoch");
            F.Directory.BeginSpawn(F.Entity);
            object Replacement = new object(); CompleteEpoch(F.Directory, Replacement);
            Model.MembershipCandidate Fresh = ScanAll(F.Directory, 1)[0];
            Check(Fresh.Slot == Old.Slot && !ReferenceEquals(Fresh.State, Captured) &&
                ReferenceEquals(Old.State, Captured) && ReferenceEquals(Old.Target, F.Entity) &&
                !F.Directory.IsCatalogCandidateCurrent(Old), "new object and CWT state cannot retarget reused slot candidate");
            GC.KeepAlive(Unenrolled); GC.KeepAlive(Replacement);
        }
        using (var Directory = new Model(1))
        {
            Directory.BeginQualifiedObservation();
            Check(Directory.QualifyStartupCompletion(), "captured-state GC startup");
            Model.MembershipCandidate Old = MakeCapturedStateFixture(Directory);
            Model.ObjectState Captured = Old.State;
            Check(Captured.Current != null && !Captured.Current.Retired, "GC fixture captures admitted lifetime state");
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Check(Old.Target == null && Captured.Current.Target == null && !Directory.IsCatalogCandidateCurrent(Old),
                "retained candidate state and record do not keep host identity alive");
            Check(Directory.SweepCatalog(1) == 1 && Directory.CatalogSlotCount == 0 &&
                Captured.CatalogSlot == -1 && Captured.CatalogBirth == 0,
                "direct-state release clears collected object's link without a CWT lookup");
            object Replacement = new object(); CompleteEpoch(Directory, Replacement);
            Model.MembershipCandidate Fresh = ScanAll(Directory, 1)[0];
            Check(Fresh.Slot == Old.Slot && Fresh.Epoch == Old.Epoch && Fresh.Birth > Old.Birth &&
                !ReferenceEquals(Fresh.State, Captured) && ReferenceEquals(Old.State, Captured) &&
                Old.Target == null && !Directory.IsCatalogCandidateCurrent(Old),
                "post-GC same-slot same-epoch new state cannot revive or retarget old candidate");
            GC.KeepAlive(Replacement); GC.KeepAlive(Captured);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Model.MembershipCandidate MakeCapturedStateFixture(Model Directory)
    {
        object Identity = new object(); CompleteEpoch(Directory, Identity);
        Check(Directory.TryAdmit(Identity, new Model.Authority(1, 2, 3), _ => true,
            Value => new Model.HostEvidence(true, true, true, 1, "prefab", Value), out Model.Binding Binding),
            "captured-state fixture canonical admission");
        Model.MembershipCandidate Candidate = ScanAll(Directory, 1)[0];
        GC.KeepAlive(Identity);
        return Candidate;
    }

    private static void TestCatalogConstructorAndDisposal()
    {
        const BindingFlags Constructors = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        ConstructorInfo Empty = typeof(Model).GetConstructor(Constructors, null, Type.EmptyTypes, null);
        ConstructorInfo Sized = typeof(Model).GetConstructor(Constructors, null, new[] { typeof(int) }, null);
        Check(Empty != null && Empty.IsAssembly && Sized != null && !Sized.GetParameters()[0].IsOptional,
            "actual internal CLR parameterless constructor and nonoptional capacity constructor");
        using (var Reflected = (Model)Activator.CreateInstance(typeof(Model), true))
            Check(!Reflected.CatalogComplete && Reflected.CatalogSlotCount == 0,
                "nonpublic parameterless Activator creation preserves no-catalog default");

        using (Fixture F = new Fixture(true, 3))
        {
            Model.Binding Retained = F.CompleteAndAdmit();
            object Other = new object(); CompleteEpoch(F.Directory, Other);
            Model.MembershipCandidate Queued = ScanAll(F.Directory, 1)[0];
            Model.MembershipCursor Cursor = BeginScan(F.Directory);
            Check(F.Directory.SweepCatalog(1) == 1 && F.Directory.CatalogSlotCount == 2,
                "nonempty catalog and advanced maintenance cursor before disposal");
            F.Directory.Dispose();
            Model Owner = Retained.Owner;
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
            Check(typeof(Model).GetField("CatalogSlots", Fields).GetValue(Owner) == null &&
                typeof(Model).GetField("CatalogFree", Fields).GetValue(Owner) == null,
                "old binding owner retains neither catalog array nor free pool after Dispose");
            foreach (string Name in new[] { "CatalogFreeCount", "CatalogExtent", "CatalogSweepPosition" })
                Check((int)typeof(Model).GetField(Name, Fields).GetValue(Owner) == 0,
                    "disposed slot counter reset: " + Name);
            Check(!Owner.IsCatalogCandidateCurrent(Queued) && !Owner.IsCatalogScanCurrent(Cursor) &&
                !Owner.Validate(Retained, F.Current, F.Read), "old candidate cursor and binding stay invalid after pool detach");
            int Written;
            Check(Owner.InspectCatalog(Cursor, 2, new Model.MembershipCandidate[2], out Written) == 0 &&
                Written == 0 && Cursor.Invalid && Owner.SweepCatalog(3) == 0 && Owner.CatalogSlotCount == 0,
                "disposed scanning and sweeping never access detached pools");
            Owner.Dispose();
            Check(!Owner.CatalogReady && !Owner.HasCatalogObservation(F.Entity) &&
                typeof(Model).GetField("CatalogSlots", Fields).GetValue(Owner) == null,
                "repeated Dispose does not recreate pools or membership");
            GC.KeepAlive(Other); GC.KeepAlive(Queued); GC.KeepAlive(Cursor); GC.KeepAlive(Retained);
        }
    }

    private static void RejectCatalogArgument(Action Action, string Label)
    {
        bool Rejected = false;
        try { Action(); }
        catch (ArgumentException) { Rejected = true; }
        Check(Rejected, Label);
    }

    private static void TestCatalogArgumentBounds()
    {
        RejectCatalogArgument(() => new Model(-1), "negative catalog capacity rejected");
        using (var Directory = new Model(2))
        using (var Other = new Model(2))
        {
            Directory.BeginQualifiedObservation(); Other.BeginQualifiedObservation();
            object A = new object(); CompleteEpoch(Directory, A);
            Check(Directory.QualifyStartupCompletion() && Other.QualifyStartupCompletion(), "argument fixtures ready");
            Model.MembershipCursor Cursor = BeginScan(Directory);
            var Scratch = new Model.MembershipCandidate[2];
            RejectCatalogArgument(() => Directory.SweepCatalog(-1), "negative sweep budget rejected");
            RejectCatalogArgument(() => { int Written; Directory.InspectCatalog(Cursor, -1, Scratch, out Written); },
                "negative scan budget rejected");
            RejectCatalogArgument(() => { int Written; Directory.InspectCatalog(Cursor, 1, new Model.MembershipCandidate[0], out Written); },
                "undersized caller scratch rejected before progress");
            RejectCatalogArgument(() => { int Written; Other.InspectCatalog(Cursor, 1, Scratch, out Written); },
                "foreign host cursor rejected");
            Check(Cursor.Position == 0 && !Cursor.Invalid && Directory.HasCatalogObservation(A),
                "argument rejection neither advances cursor nor loses membership");
            int Count;
            Check(Directory.InspectCatalog(Cursor, int.MaxValue, Scratch, out Count) == 1 && Count == 1 && Cursor.Complete,
                "oversized explicit budget remains bounded by captured extent");
            Check(Directory.SweepCatalog(int.MaxValue) == 1, "maintenance never scans past high-water extent");
            GC.KeepAlive(A);
        }
    }
}
