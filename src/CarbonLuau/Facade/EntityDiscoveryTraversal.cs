using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    // Private, owner-thread catalog traversal only. No host adapter is selected
    // here. Observe and IsAuthorized must themselves be qualified bounded host
    // operations; this class cannot preempt a delegate or establish native safety.
    internal sealed class EntityDiscoveryTraversal : IDisposable
    {
        internal sealed class WorkPolicy
        {
            internal readonly int MaximumQueries, MaximumPerDomain, WorkPerTurn;
            internal readonly int RawSlotsPerTurn, MaximumResults, MaximumTotalRawSlots;
            internal readonly int MaximumDeliveriesPerTurn, MaximumPrefabBytes;
            internal readonly long DeadlineTicks;

            // All limits are injected. Eight is the authorized concurrency ceiling,
            // not a default. One delivery costs 1 + twice its retained count:
            // host validation, then a final callback-free candidate recheck.
            internal WorkPolicy(int MaximumQueries, int MaximumPerDomain, int WorkPerTurn,
                int RawSlotsPerTurn, int MaximumResults, int MaximumTotalRawSlots,
                int MaximumDeliveriesPerTurn, int MaximumPrefabBytes, long DeadlineTicks)
            {
                if (MaximumQueries < 1 || MaximumQueries > 8 || MaximumPerDomain < 1 ||
                    MaximumPerDomain > MaximumQueries || MaximumPerDomain > 2 || MaximumResults < 1 ||
                    WorkPerTurn < 1 || MaximumResults > (WorkPerTurn - 1) / 2 || RawSlotsPerTurn < 1 ||
                    RawSlotsPerTurn > WorkPerTurn || MaximumTotalRawSlots < 1 ||
                    MaximumDeliveriesPerTurn < 1 || MaximumDeliveriesPerTurn > MaximumQueries ||
                    MaximumPrefabBytes < 1 || MaximumPrefabBytes > 512 || DeadlineTicks < 1)
                    throw new ArgumentOutOfRangeException("WorkPolicy");
                this.MaximumQueries = MaximumQueries;
                this.MaximumPerDomain = MaximumPerDomain;
                this.WorkPerTurn = WorkPerTurn;
                this.RawSlotsPerTurn = RawSlotsPerTurn;
                this.MaximumResults = MaximumResults;
                this.MaximumTotalRawSlots = MaximumTotalRawSlots;
                this.MaximumDeliveriesPerTurn = MaximumDeliveriesPerTurn;
                this.MaximumPrefabBytes = MaximumPrefabBytes;
                this.DeadlineTicks = DeadlineTicks;
            }
        }

        internal readonly struct Query
        {
            internal readonly double X, Y, Z, Radius;
            internal readonly string Prefab;
            internal readonly int Limit;
            internal Query(double X, double Y, double Z, double Radius, string Prefab, int Limit)
            { this.X = X; this.Y = Y; this.Z = Z; this.Radius = Radius; this.Prefab = Prefab; this.Limit = Limit; }
        }

        internal readonly struct CandidateObservation
        {
            // Original immutable weak candidate, never a native pointer or strong
            // host object. The producer must observe/revalidate this exact lifetime
            // directly, without acquiring an F1 token or proxy.
            internal readonly EntityLifetimeModel.MembershipCandidate Candidate;
            internal readonly CandidateStatus Status;
            internal readonly ulong Birth, Id;
            internal readonly string Prefab;
            internal readonly double X, Y, Z;
            // Construction is data only, never proof of host validity. The
            // scheduler and the request's host validator must still qualify it.
            internal CandidateObservation(EntityLifetimeModel.MembershipCandidate Candidate, ulong Birth,
                ulong Id, string Prefab, double X, double Y, double Z)
            { Status = CandidateStatus.Observed; this.Candidate = Candidate; this.Birth = Birth; this.Id = Id; this.Prefab = Prefab; this.X = X; this.Y = Y; this.Z = Z; }
            internal CandidateObservation(CandidateStatus Status)
            { this.Status = Status; Candidate = null; Birth = Id = 0; Prefab = null; X = Y = Z = 0; }
        }

        internal enum CandidateStatus { Skip, Observed, Failure }
        internal enum StartStatus { Accepted, Invalid, Unavailable, Unauthorized, Capacity, Reentrant, Exhausted }
        internal enum Outcome { Success, Cancelled, Unauthorized, CatalogLost, Deadline, RawLimit, ResultLimit, ProducerFailure, InvalidObservation, StaleResult }

        internal sealed class Completion
        {
            private readonly CandidateObservation[] Results;
            internal readonly ulong RequestId;
            internal readonly Outcome Status;
            internal readonly int Count, RawSlots;
            internal Completion(ulong RequestId, Outcome Status, CandidateObservation[] Results, int Count, int RawSlots)
            { this.RequestId = RequestId; this.Status = Status; this.Results = Results; this.Count = Count; this.RawSlots = RawSlots; }
            internal CandidateObservation GetResult(int Index)
            {
                if (Index < 0 || Index >= Count) throw new ArgumentOutOfRangeException("Index");
                return Results[Index];
            }
        }

        internal readonly struct TurnWork
        {
            internal readonly int Units, RawSlots, ProducerCalls, Deliveries;
            internal TurnWork(int Units, int RawSlots, int ProducerCalls, int Deliveries)
            { this.Units = Units; this.RawSlots = RawSlots; this.ProducerCalls = ProducerCalls; this.Deliveries = Deliveries; }
        }

        private sealed class Request
        {
            internal ulong Id, AcceptedTurn;
            internal ulong DomainKey;
            internal Query Query;
            internal EntityLifetimeModel.MembershipCursor Cursor;
            internal Func<EntityLifetimeModel.MembershipCandidate, CandidateObservation> Observe;
            internal Func<bool> IsAuthorized;
            internal Func<CandidateObservation, bool> ValidateResult;
            internal Action<Completion> Callback;
            internal CandidateObservation[] Results;
            internal int Count, RawSlots, Slot;
            internal long Deadline;
            internal bool Ready, Cancelled;
            internal Outcome Status;
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly EntityLifetimeModel Model;
        private readonly WorkPolicy Policy;
        private readonly Request[] Requests;
        private readonly Queue<Request> Delivery;
        private readonly EntityLifetimeModel.MembershipCandidate[] Scratch = new EntityLifetimeModel.MembershipCandidate[1];
        private readonly int OwnerThread = Thread.CurrentThread.ManagedThreadId;
        private ulong NextId, Turn;
        private long LastTicks;
        private int NextSlot;
        private bool InTurn, InAdmission, Disposed;
        private Request InFlight;
        internal int ActiveCount { get; private set; }
        internal int CallbackFailures { get; private set; }

        internal EntityDiscoveryTraversal(EntityLifetimeModel Model, WorkPolicy Policy)
        {
            if (Model == null || Policy == null) throw new ArgumentNullException(Model == null ? "Model" : "Policy");
            this.Model = Model; this.Policy = Policy;
            Requests = new Request[Policy.MaximumQueries];
            Delivery = new Queue<Request>(Policy.MaximumQueries);
        }

        private void CheckOwner()
        {
            if (Thread.CurrentThread.ManagedThreadId != OwnerThread)
                throw new InvalidOperationException("entity discovery owner-thread required");
        }

        private static bool Finite(double Value) { return !Double.IsNaN(Value) && !Double.IsInfinity(Value); }
        private bool ValidPrefab(string Value)
        {
            if (String.IsNullOrEmpty(Value) || Value.Length > Policy.MaximumPrefabBytes || Value.IndexOf('\0') >= 0) return false;
            try { return StrictUtf8.GetByteCount(Value) <= Policy.MaximumPrefabBytes; }
            catch (EncoderFallbackException) { return false; }
        }

        private bool Authorized(Request Value)
        {
            try { return Value.IsAuthorized(); }
            catch (Exception) { return false; }
        }

        // Now is an injected monotonic owner-clock value; no clock/native call is
        // hidden in traversal. Rejected admissions never invoke a completion.
        internal StartStatus TryStart(Query Query, ulong DomainKey,
            Func<EntityLifetimeModel.MembershipCandidate, CandidateObservation> Observe,
            Func<bool> IsAuthorized, Func<CandidateObservation, bool> ValidateResult,
            Action<Completion> Callback, long Now, out ulong RequestId)
        {
            CheckOwner(); RequestId = 0;
            if (Disposed) return StartStatus.Unavailable;
            if (InAdmission) return StartStatus.Reentrant;
            if (!Finite(Query.X) || !Finite(Query.Y) || !Finite(Query.Z) || !Finite(Query.Radius) ||
                Query.Radius < 0 || Query.Limit < 1 || Query.Limit > Policy.MaximumResults ||
                (Query.Prefab != null && !ValidPrefab(Query.Prefab)) || DomainKey == 0 ||
                Observe == null || IsAuthorized == null || ValidateResult == null || Callback == null || Now < LastTicks ||
                Now < 0 || Now > Int64.MaxValue - Policy.DeadlineTicks) return StartStatus.Invalid;
            LastTicks = Now;
            if (NextId == UInt64.MaxValue || Turn == UInt64.MaxValue) return StartStatus.Exhausted;
            int DomainCount = 0, Free = -1;
            for (int Index = 0; Index < Requests.Length; ++Index)
            {
                Request Existing = Requests[Index];
                if (Existing == null) { if (Free < 0) Free = Index; continue; }
                if (Existing.DomainKey == DomainKey) DomainCount++;
            }
            if (Free < 0 || DomainCount >= Policy.MaximumPerDomain) return StartStatus.Capacity;
            InAdmission = true;
            try
            {
                var Value = new Request { DomainKey = DomainKey, IsAuthorized = IsAuthorized };
                if (!Authorized(Value)) return StartStatus.Unauthorized;
                if (Disposed) return StartStatus.Unavailable;
                EntityLifetimeModel.MembershipCursor Cursor;
                if (!Model.BeginCatalogScan(out Cursor)) return StartStatus.Unavailable;
                if (!Authorized(Value)) return StartStatus.Unauthorized;
                if (Disposed) return StartStatus.Unavailable;
                if (!Model.IsCatalogScanCurrent(Cursor)) return StartStatus.Unavailable;
                Value.Id = ++NextId; Value.AcceptedTurn = Turn; Value.Query = Query;
                Value.Cursor = Cursor; Value.Observe = Observe; Value.Callback = Callback;
                Value.Results = new CandidateObservation[Query.Limit]; Value.Slot = Free;
                Value.ValidateResult = ValidateResult;
                Value.Deadline = Now + Policy.DeadlineTicks;
                Requests[Free] = Value; ActiveCount++; RequestId = Value.Id;
                return StartStatus.Accepted;
            }
            catch (OutOfMemoryException) { return StartStatus.Capacity; }
            finally { InAdmission = false; }
        }

        internal bool Cancel(ulong RequestId)
        {
            CheckOwner();
            for (int Index = 0; Index < Requests.Length; ++Index)
                if (Requests[Index] != null && Requests[Index].Id == RequestId)
                { Requests[Index].Cancelled = true; return true; }
            return false;
        }

        private Outcome CurrentFailure(Request Value, long Now)
        {
            if (Disposed) return Outcome.Cancelled;
            if (Value.Cancelled) return Outcome.Cancelled;
            if (Now >= Value.Deadline) return Outcome.Deadline;
            bool Current = Authorized(Value);
            if (Value.Cancelled) return Outcome.Cancelled;
            if (!Current) return Outcome.Unauthorized;
            if (!Model.IsCatalogScanCurrent(Value.Cursor)) return Outcome.CatalogLost;
            return Outcome.Success;
        }

        private static void Release(Request Value)
        {
            if (Value == null) return;
            Value.Observe = null; Value.IsAuthorized = null; Value.ValidateResult = null;
            Value.Callback = null; Value.Results = null; Value.Cursor = null; Value.Count = 0;
        }

        // Silent terminal unload. Does not own/dispose Model or revoke completion
        // data already handed to the caller. At most eight requests are detached;
        // arrays are dropped, not walked. Safe even inside a private delegate.
        public void Dispose()
        {
            CheckOwner();
            if (Disposed) return;
            Disposed = true;
            for (int Index = 0; Index < Requests.Length; ++Index)
            { Release(Requests[Index]); Requests[Index] = null; }
            Release(InFlight); InFlight = null;
            Delivery.Clear(); Scratch[0] = null; ActiveCount = 0;
        }

        private bool Canonical(CandidateObservation Value, EntityLifetimeModel.MembershipCandidate Candidate)
        {
            // Model's direct membership check covers the original immutable
            // state/epoch/generation/slot/birth and known sticky retirement.
            // Birth is private catalog identity, not an F1/public token. The
            // qualified direct observer latches evidence on this candidate;
            // construction of an observation cannot manufacture that receipt.
            return ReferenceEquals(Value.Candidate, Candidate) && Model.IsCatalogCandidateCurrent(Candidate) &&
                Candidate.EvidenceCaptured && Value.Birth != 0 && Value.Birth == Candidate.Birth &&
                Value.Id != 0 && Value.Id == Candidate.CapturedId &&
                String.Equals(Value.Prefab, Candidate.CapturedPrefab, StringComparison.Ordinal);
        }

        private static bool Matches(Query Query, CandidateObservation Value)
        {
            if (Query.Prefab != null && !String.Equals(Query.Prefab, Value.Prefab, StringComparison.Ordinal)) return false;
            double X = Math.Abs(Value.X - Query.X), Y = Math.Abs(Value.Y - Query.Y), Z = Math.Abs(Value.Z - Query.Z);
            if (X > Query.Radius || Y > Query.Radius || Z > Query.Radius) return false;
            if (Query.Radius == 0) return X == 0 && Y == 0 && Z == 0;
            // Scale before squaring; finite input subtraction may overflow to
            // infinity, which the component checks above correctly reject.
            X /= Query.Radius; Y /= Query.Radius; Z /= Query.Radius;
            return X * X + Y * Y + Z * Z <= 1;
        }

        private void Finish(Request Value, Outcome Status)
        {
            if (Value.Ready) return;
            Value.Status = Status; Value.Ready = true; Value.Observe = null;
            if (Status != Outcome.Success) { Value.Results = null; Value.Count = 0; }
            Delivery.Enqueue(Value);
        }

        internal TurnWork RunTurn(long Now)
        {
            CheckOwner();
            if (Disposed) return default(TurnWork);
            if (InTurn || InAdmission) return default(TurnWork);
            if (Now < LastTicks || Now < 0) throw new ArgumentOutOfRangeException("Now");
            if (Turn == UInt64.MaxValue) throw new InvalidOperationException("entity discovery turn exhausted");
            LastTicks = Now; Turn++; InTurn = true;
            int Units = 0, Raw = 0, Calls = 0, Delivered = 0;
            try
            {
                // Snapshot the FIFO length: callbacks may admit/cancel work but
                // cannot recursively drain it. Ready requests occupy quotas until
                // detached below. Delivery precedes scanning to avoid starvation.
                int Eligible = Math.Min(Delivery.Count, Policy.MaximumDeliveriesPerTurn);
                for (int Index = 0; Index < Eligible; ++Index)
                {
                    if (Disposed) break;
                    Request Value = Delivery.Peek();
                    int Cost = 1 + 2 * Value.Count;
                    if (Cost > Policy.WorkPerTurn - Units) break;
                    Units += Cost;
                    Delivery.Dequeue(); Requests[Value.Slot] = null; ActiveCount--;
                    InFlight = Value;
                    Outcome Status = CurrentFailure(Value, Now);
                    if (Disposed) break;
                    if (Status == Outcome.Success) Status = Value.Status;
                    if (Status == Outcome.Success)
                    {
                        for (int Match = 0; Match < Value.Count; ++Match)
                        {
                            if (!Canonical(Value.Results[Match], Value.Results[Match].Candidate))
                            { Status = Outcome.StaleResult; break; }
                            bool Valid;
                            try { Valid = Value.ValidateResult(Value.Results[Match]); }
                            catch (Exception) { Valid = false; }
                            if (Disposed) break;
                            if (!Valid) { Status = Outcome.StaleResult; break; }
                        }
                        if (Disposed) break;
                        if (Status == Outcome.Success) Status = CurrentFailure(Value, Now);
                        if (Disposed) break;
                        if (Status == Outcome.Success)
                            for (int Match = 0; Match < Value.Count; ++Match)
                                if (!Canonical(Value.Results[Match], Value.Results[Match].Candidate))
                                { Status = Outcome.StaleResult; break; }
                    }
                    var Result = new Completion(Value.Id, Status, Status == Outcome.Success ? Value.Results : null,
                        Status == Outcome.Success ? Value.Count : 0, Value.RawSlots);
                    Action<Completion> Callback = Value.Callback;
                    Value.Callback = null; Value.Results = null; Value.IsAuthorized = null; Value.ValidateResult = null;
                    InFlight = null;
                    Delivered++;
                    try { Callback(Result); }
                    catch (Exception) { if (CallbackFailures < Int32.MaxValue) CallbackFailures++; }
                }

                while (!Disposed && Units < Policy.WorkPerTurn && Raw < Policy.RawSlotsPerTurn)
                {
                    Request Value = null;
                    for (int Index = 0; Index < Requests.Length; ++Index)
                    {
                        Request Candidate = Requests[NextSlot];
                        NextSlot = (NextSlot + 1) % Requests.Length;
                        if (Candidate != null && !Candidate.Ready && Candidate.AcceptedTurn < Turn)
                        { Value = Candidate; break; }
                    }
                    if (Value == null) break;
                    Units++;
                    Outcome Failure = CurrentFailure(Value, Now);
                    if (Disposed) break;
                    if (Failure != Outcome.Success) { Finish(Value, Failure); continue; }
                    if (Value.Cursor.Complete) { Finish(Value, Outcome.Success); continue; }
                    if (Value.RawSlots >= Policy.MaximumTotalRawSlots) { Finish(Value, Outcome.RawLimit); continue; }
                    int Written;
                    int Inspected = Model.InspectCatalog(Value.Cursor, 1, Scratch, out Written);
                    Raw += Inspected; Value.RawSlots += Inspected;
                    EntityLifetimeModel.MembershipCandidate Encounter = Scratch[0]; Scratch[0] = null;
                    if (!Model.IsCatalogScanCurrent(Value.Cursor)) { Finish(Value, Outcome.CatalogLost); continue; }
                    if (Written == 1 && Model.IsCatalogCandidateCurrent(Encounter))
                    {
                        CandidateObservation Observed;
                        Calls++;
                        try { Observed = Value.Observe(Encounter); }
                        catch (Exception) { Observed = new CandidateObservation(CandidateStatus.Failure); }
                        if (Disposed) break;
                        Failure = CurrentFailure(Value, Now);
                        if (Failure != Outcome.Success) { Finish(Value, Failure); continue; }
                        if (Observed.Status == CandidateStatus.Failure) { Finish(Value, Outcome.ProducerFailure); continue; }
                        if (Observed.Status != CandidateStatus.Skip)
                        {
                            if (Observed.Status != CandidateStatus.Observed || !Canonical(Observed, Encounter) ||
                                !ValidPrefab(Observed.Prefab) || !Finite(Observed.X) || !Finite(Observed.Y) || !Finite(Observed.Z))
                            { Finish(Value, Outcome.InvalidObservation); continue; }
                            if (Matches(Value.Query, Observed))
                            {
                                if (Value.Count == Value.Query.Limit) { Finish(Value, Outcome.ResultLimit); continue; }
                                Value.Results[Value.Count++] = Observed;
                            }
                        }
                    }
                    if (Value.Cursor.Complete) Finish(Value, Outcome.Success);
                    else if (Value.RawSlots == Policy.MaximumTotalRawSlots) Finish(Value, Outcome.RawLimit);
                }
                return new TurnWork(Units, Raw, Calls, Delivered);
            }
            finally { InFlight = null; Scratch[0] = null; InTurn = false; }
        }
    }
}
