using System;
using System.Collections.Generic;
using System.Reflection;
using Model = Carbon.Plugins.EntityLifetimeModel;

internal static partial class Program
{
    private static void TestPublicConversion()
    {
        var Authority = new Model.Authority(1, 2, 3);
        bool Current = true;
        Func<Model.Authority, bool> Authorized = Value => Current && Value.DomainLifetime == 2;
        var Values = new List<Entity>();
        Func<object, Model.HostEvidence> Read = Identity => {
            Entity Value = (Entity)Identity;
            return new Model.HostEvidence(Value.Alive, true, true, Value.Id, Value.Prefab, Value.Alive ? Value : null);
        };
        using (var Directory = new Model(4)) {
            Directory.BeginQualifiedObservation(); Check(Directory.QualifyStartupCompletion(), "conversion startup");
            for (int Index = 0; Index < 4; ++Index) {
                var Value = new Entity { Id = (ulong)Index + 1 }; Values.Add(Value);
                Check(Directory.CompleteSpawn(Directory.BeginSpawn(Value), true, true), "conversion Spawn");
            }
            Model.MembershipCursor Cursor;
            Check(Directory.BeginCatalogScan(out Cursor), "conversion cursor");
            var Candidates = new Model.MembershipCandidate[4]; int Written;
            Check(Directory.InspectCatalog(Cursor, 4, Candidates, out Written) == 4 && Written == 4, "all exact candidates");
            CheckTokenStorageEmpty(Directory);
            var Bindings = new Model.Binding[4];
            for (int Index = 0; Index < 4; ++Index)
                Check(Directory.TryAdmitCatalog(Candidates[Index], Authority, Authorized, Read, out Bindings[Index]),
                    "bounded direct public conversion");
            for (int Index = 0; Index < 4; ++Index) {
                Model.Binding Again;
                Check(Directory.TryAdmitCatalog(Candidates[Index], Authority, Authorized, Read, out Again) &&
                    Model.SameLifetime(Bindings[Index], Again), "conversion uses F1 exact identity");
            }
            Check(((Dictionary<ulong, WeakReference>)TokenRecordsField.GetValue(Directory)).Count == 4,
                "only returned matches acquire bounded tokens");
            Current = false; Model.Binding Failed;
            Check(!Directory.TryAdmitCatalog(Candidates[0], Authority, Authorized, Read, out Failed), "replacement authority denied");
            Current = true;
            Values[0].Id = 8;
            Check(!Directory.TryAdmitCatalog(Candidates[0], Authority, Authorized, Read, out Failed) && Bindings[0].Record.Retired,
                "changed ID at admission permanently retires old lifetime");
            Values[0].Id = 1;
            Check(!Directory.TryAdmitCatalog(Candidates[0], Authority, Authorized, Read, out Failed), "ID restoration cannot revive result");
            Check(Directory.CompleteSpawn(Directory.BeginSpawn(Values[0]), true, true), "same-object new epoch");
            Model.MembershipCursor NewCursor;
            Check(Directory.BeginCatalogScan(out NewCursor), "new epoch cursor");
            var NewCandidates = new Model.MembershipCandidate[4];
            Directory.InspectCatalog(NewCursor, 4, NewCandidates, out Written);
            foreach (var Candidate in NewCandidates) if (Candidate != null && ReferenceEquals(Candidate.Target, Values[0])) {
                Model.Binding Fresh;
                Check(Directory.TryAdmitCatalog(Candidate, Authority, Authorized, Read, out Fresh) &&
                    !Model.SameLifetime(Fresh, Bindings[0]), "fresh token does not retarget stale result");
            }
            Check(!Directory.TryAdmitCatalog(Candidates[0], Authority, Authorized, Read, out Failed), "reused slot never retargets old candidate");
            Values[1].Alive = false;
            Check(!Directory.TryAdmitCatalog(Candidates[1], Authority, Authorized, Read, out Failed), "death before public callback fails closed");
            Directory.BreakObserverContinuity();
            Check(!Directory.TryAdmitCatalog(Candidates[2], Authority, Authorized, Read, out Failed), "continuity lost before admission");
        }
        using (var Directory = new Model(1)) {
            Directory.BeginQualifiedObservation(); Check(Directory.QualifyStartupCompletion(), "conversion reentry startup");
            var Value = new Entity {Id = 1};
            Check(Directory.CompleteSpawn(Directory.BeginSpawn(Value), true, true), "conversion reentry Spawn");
            Model.MembershipCursor Cursor; Directory.BeginCatalogScan(out Cursor);
            var Candidates = new Model.MembershipCandidate[1]; int Written;
            Directory.InspectCatalog(Cursor, 1, Candidates, out Written);
            int ChecksAtAuthority = 0; Model.Binding Failed;
            Check(!Directory.TryAdmitCatalog(Candidates[0], Authority, Ignore => {
                if (++ChecksAtAuthority == 3) Directory.BeginSpawn(Value);
                return true;
            }, Read, out Failed) && Failed == null && ChecksAtAuthority == 3,
                "final authority-check lifecycle reentry cannot admit retired candidate");
        }
    }
}
