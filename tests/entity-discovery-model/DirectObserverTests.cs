using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Model = Carbon.Plugins.EntityLifetimeModel;

internal static partial class Program
{
    private static readonly Model.Authority DirectAuthority = new Model.Authority(1, 1, 1);

    private static Model.HostEvidence DirectEvidence(object Identity)
    {
        var Value = (Entity)Identity;
        return new Model.HostEvidence(Value.Alive, true, true, Value.Id, Value.Prefab, Value.Alive ? Value : null);
    }

    private static Model.MembershipCandidate FirstCandidate(Model Model)
    {
        Model.MembershipCursor Cursor;
        Check(Model.BeginCatalogScan(out Cursor), "direct observer scan ready");
        var Scratch = new Model.MembershipCandidate[1];
        int Written;
        Check(Model.InspectCatalog(Cursor, 1, Scratch, out Written) == 1 && Written == 1,
            "direct observer exact candidate");
        return Scratch[0];
    }

    private sealed class WeakTableStorage
    {
        private sealed class Reference
        {
            internal object Owner, Expected;
            internal FieldInfo Field;
        }
        private readonly List<Reference> References = new List<Reference>();
        internal WeakTableStorage(Model Model)
        {
            object Table = typeof(Model).GetField("States", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Model);
            Capture(Table, 0);
            Check(References.Count != 0, "test runtime exposes CWT backing storage for no-growth check");
        }
        private void Capture(object Owner, int Depth)
        {
            foreach (FieldInfo Field in Owner.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                object Value = Field.GetValue(Owner);
                if (Field.FieldType.IsArray)
                    References.Add(new Reference { Owner = Owner, Field = Field, Expected = Value });
                else if (Depth == 0 && Value != null && Value.GetType().FullName.StartsWith(
                    "System.Runtime.CompilerServices.ConditionalWeakTable", StringComparison.Ordinal))
                {
                    References.Add(new Reference { Owner = Owner, Field = Field, Expected = Value });
                    Capture(Value, 1);
                }
            }
        }
        internal void CheckUnchanged()
        {
            foreach (Reference Value in References)
                Check(Object.ReferenceEquals(Value.Field.GetValue(Value.Owner), Value.Expected),
                    "direct catalog operations do not replace/grow CWT backing storage");
        }
    }

    private static void TestDirectCatalogObserver()
    {
        foreach (int InvalidCase in new[] { 0, 1, 2, 3, 4 })
            using (var F = new Fixture(Count: 1))
            {
                Model.MembershipCandidate Candidate = FirstCandidate(F.Model);
                var Storage = new WeakTableStorage(F.Model);
                Entity Value = F.Entities[0];
                if (InvalidCase == 0) Value.Id = 0;
                if (InvalidCase == 1) Value.Prefab = "";
                if (InvalidCase == 2) Value.Prefab = "bad\0prefab";
                if (InvalidCase == 3) Value.Prefab = "\ud800";
                if (InvalidCase == 4) Value.Prefab = new string('x', 513);
                Model.HostEvidence Evidence;
                Check(!F.Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => true, DirectEvidence, out Evidence),
                    "invalid direct ID/prefab rejected");
                Check(!Candidate.EvidenceCaptured && Candidate.State.State == Model.SpawnState.Failed &&
                    !F.Model.IsCatalogCandidateCurrent(Candidate), "invalid evidence sticky-retires the exact catalog epoch");
                Value.Id = 1; Value.Prefab = "assets/example.prefab";
                Check(!F.Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => true, DirectEvidence, out Evidence),
                    "corrected values cannot resurrect retired candidate without new full Spawn");
                CheckTokenStorageEmpty(F.Model); Storage.CheckUnchanged();
            }
        using (var F = new Fixture(Count: 1))
        {
            Model.MembershipCandidate Old = FirstCandidate(F.Model);
            var Storage = new WeakTableStorage(F.Model);
            Model.HostEvidence Evidence;
            Check(!F.Model.TryObserveCatalog(Old, DirectAuthority, Ignore => true, Identity => {
                F.Spawn((Entity)Identity);
                return DirectEvidence(Identity);
            }, out Evidence), "read-reentrant new Spawn rejects old epoch observation");
            Model.MembershipCandidate Replacement = FirstCandidate(F.Model);
            Check(Replacement.Birth != Old.Birth && !Old.EvidenceCaptured && !Replacement.EvidenceCaptured &&
                F.Model.IsCatalogCandidateCurrent(Replacement), "old read cannot latch or retire replacement epoch");
            Check(F.Model.TryObserveCatalog(Replacement, DirectAuthority, Ignore => true, DirectEvidence, out Evidence),
                "replacement epoch still directly observable");
            CheckTokenStorageEmpty(F.Model); Storage.CheckUnchanged();
        }
        using (var F = new Fixture(Count: 1))
        {
            Model.MembershipCandidate Candidate = FirstCandidate(F.Model);
            var Storage = new WeakTableStorage(F.Model);
            bool Authorized = true;
            Model.HostEvidence Evidence;
            Check(!F.Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => Authorized, Identity => {
                Authorized = false; return DirectEvidence(Identity);
            }, out Evidence), "authority loss during evidence read prevents receipt");
            Check(!Candidate.EvidenceCaptured && F.Model.IsCatalogCandidateCurrent(Candidate),
                "domain authority loss does not poison valid shared host membership");
            Authorized = true;
            Check(F.Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => Authorized, DirectEvidence, out Evidence),
                "current authority can subsequently observe unchanged epoch");
            CheckTokenStorageEmpty(F.Model); Storage.CheckUnchanged();
        }
        using (var F = new Fixture(Count: 1))
        {
            Model.MembershipCandidate Candidate = FirstCandidate(F.Model);
            var Storage = new WeakTableStorage(F.Model);
            Model.HostEvidence Evidence;
            Check(!F.Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => true, Identity => {
                F.Model.Dispose(); return DirectEvidence(Identity);
            }, out Evidence), "model disposal during read prevents evidence receipt");
            Check(!Candidate.EvidenceCaptured && !F.Model.IsCatalogCandidateCurrent(Candidate), "disposed model cannot publish sampled epoch");
            CheckTokenStorageEmpty(F.Model); Storage.CheckUnchanged();
        }
        using (var F = new Fixture(Count: 1))
        {
            Model.MembershipCandidate Candidate = FirstCandidate(F.Model);
            var Storage = new WeakTableStorage(F.Model);
            Model.HostEvidence Evidence;
            Check(!F.Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => true,
                Identity => { throw new InvalidOperationException("test evidence fault"); }, out Evidence),
                "evidence exception contained and exact candidate retired");
            Check(!Candidate.EvidenceCaptured && !F.Model.IsCatalogCandidateCurrent(Candidate), "read failure never manufactures receipt");
            CheckTokenStorageEmpty(F.Model); Storage.CheckUnchanged();
        }
        using (var Model = new Model(4))
        {
            Model.BeginQualifiedObservation(); Check(Model.QualifyStartupCompletion(), "weak observer startup");
            Model.MembershipCandidate Candidate;
            WeakReference Identity = CollectibleCandidate(Model, out Candidate);
            var Storage = new WeakTableStorage(Model);
            for (int Attempt = 0; Attempt < 4 && Identity.IsAlive; ++Attempt)
            { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
            Check(!Identity.IsAlive && Candidate.Target == null, "candidate and canonical state do not retain weak host identity");
            int Reads = 0;
            Model.HostEvidence Evidence;
            Check(!Model.TryObserveCatalog(Candidate, DirectAuthority, Ignore => true, Value => {
                Reads++; return DirectEvidence(Value);
            }, out Evidence) && Reads == 0 && !Candidate.EvidenceCaptured, "weak death rejected before evidence read");
            Model.SweepCatalog(1);
            Check(Model.CatalogSlotCount == 0, "bounded sweep releases dead catalog membership");
            CheckTokenStorageEmpty(Model); Storage.CheckUnchanged();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CollectibleCandidate(Model Model, out Model.MembershipCandidate Candidate)
    {
        var Value = new Entity { Id = 1 };
        Check(Model.CompleteSpawn(Model.BeginSpawn(Value), true, true), "collectible full Spawn");
        Candidate = FirstCandidate(Model);
        return new WeakReference(Value);
    }
}
