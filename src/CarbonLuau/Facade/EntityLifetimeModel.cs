using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    // Private state only. The host adapter must independently qualify the full
    // virtual Spawn boundary and its normal return. A startup marker alone is
    // never evidence that an individual entity completed Spawn.
    internal sealed class EntityLifetimeModel : IDisposable
    {
        internal enum SpawnState { None, Pending, Completed, Failed }

        internal readonly struct Authority
        {
            internal readonly ulong VmGeneration, DomainLifetime, PublicationLifetime;
            internal Authority(ulong VmGeneration, ulong DomainLifetime, ulong PublicationLifetime)
            {
                this.VmGeneration = VmGeneration;
                this.DomainLifetime = DomainLifetime;
                this.PublicationLifetime = PublicationLifetime;
            }
            internal bool IsValid { get { return VmGeneration != 0 && DomainLifetime != 0 && PublicationLifetime != 0; } }
        }

        internal readonly struct HostEvidence
        {
            internal readonly bool Alive, FullySpawned, HasNetworkObject;
            internal readonly ulong NetworkId;
            internal readonly string Prefab;
            internal readonly object RegistryOccupant;
            internal HostEvidence(bool Alive, bool FullySpawned, bool HasNetworkObject,
                ulong NetworkId, string Prefab, object RegistryOccupant)
            {
                this.Alive = Alive;
                this.FullySpawned = FullySpawned;
                this.HasNetworkObject = HasNetworkObject;
                this.NetworkId = NetworkId;
                this.Prefab = Prefab;
                this.RegistryOccupant = RegistryOccupant;
            }
        }

        internal sealed class ObjectState
        {
            // Candidates can retain this state after its weak table key dies.
            // Neither this state nor its attempt/record may strongly own a host.
            internal ulong Epoch, ObservationGeneration;
            internal SpawnState State;
            internal bool Poisoned;
            internal SpawnAttempt Attempt;
            internal LifetimeRecord Current;
            internal int CatalogSlot = -1;
            internal ulong CatalogBirth;
        }

        // This is completed-epoch membership only, never host admission or caller
        // authority. A new activation allocates a new immutable weak holder so
        // queued candidates cannot follow a reused slot to its new occupant.
        internal sealed class MembershipCandidate
        {
            private readonly WeakReference Identity;
            internal readonly EntityLifetimeModel Owner;
            internal readonly ObjectState State;
            internal readonly int Slot;
            internal readonly ulong Epoch, ObservationGeneration, Birth;
            internal bool EvidenceCaptured;
            internal ulong CapturedId;
            internal string CapturedPrefab;
            internal MembershipCandidate(EntityLifetimeModel Owner, object Identity,
                ObjectState State, int Slot, ulong Birth)
            {
                this.Owner = Owner;
                this.State = State;
                this.Identity = new WeakReference(Identity);
                this.Slot = Slot;
                this.Birth = Birth;
                Epoch = State.Epoch;
                ObservationGeneration = State.ObservationGeneration;
            }
            internal object Target { get { return Identity.Target; } }
        }

        internal sealed class MembershipCursor
        {
            internal readonly EntityLifetimeModel Owner;
            internal readonly int End;
            internal readonly ulong UpperBirth, ObservationGeneration;
            internal int Position;
            internal bool Invalid;
            internal MembershipCursor(EntityLifetimeModel Owner, int End, ulong UpperBirth,
                ulong ObservationGeneration)
            {
                this.Owner = Owner;
                this.End = End;
                this.UpperBirth = UpperBirth;
                this.ObservationGeneration = ObservationGeneration;
            }
            // Completion describes cursor progress. The caller must also check
            // IsCatalogScanCurrent before admission, including an empty result.
            internal bool Complete { get { return !Invalid && Position == End; } }
        }

        internal sealed class SpawnAttempt
        {
            private readonly WeakReference Identity;
            internal readonly ObjectState State;
            internal readonly ulong Epoch, ObservationGeneration;
            internal SpawnAttempt(object Identity, ObjectState State)
            {
                this.Identity = new WeakReference(Identity);
                this.State = State;
                Epoch = State.Epoch;
                ObservationGeneration = State.ObservationGeneration;
            }
            internal object Target { get { return Identity.Target; } }
        }

        internal sealed class LifetimeRecord
        {
            private readonly WeakReference Identity;
            internal readonly ulong Token, Epoch, ObservationGeneration, NetworkId;
            internal readonly string Prefab;
            internal bool Retired;
            internal LifetimeRecord(object Identity, ulong Token, ObjectState State, HostEvidence Evidence)
            {
                this.Identity = new WeakReference(Identity);
                this.Token = Token;
                Epoch = State.Epoch;
                ObservationGeneration = State.ObservationGeneration;
                NetworkId = Evidence.NetworkId;
                Prefab = Evidence.Prefab;
            }
            internal object Target { get { return Identity.Target; } }
        }

        internal sealed class Binding
        {
            internal readonly EntityLifetimeModel Owner;
            internal readonly LifetimeRecord Record;
            internal readonly Authority Authority;
            internal bool Retired;
            internal Binding(EntityLifetimeModel Owner, LifetimeRecord Record, Authority Authority)
            { this.Owner = Owner; this.Record = Record; this.Authority = Authority; }
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly ConditionalWeakTable<object, ObjectState> States = new ConditionalWeakTable<object, ObjectState>();
        // Tokens are lookup handles, not owners. The CWT supplies canonical state.
        // Optional/queued candidates retain one fixed-size ObjectState and its
        // current record/attempt metadata, all with weak host identity. There is
        // no epoch-history chain and none of this metadata strongly owns a host.
        // Live-key states are never removed/replaced: direct candidate validation
        // depends on that invariant. A token sweep removes dead/retired token keys.
        private readonly Dictionary<ulong, WeakReference> TokenRecords = new Dictionary<ulong, WeakReference>();
        private readonly Queue<ulong> TokenSweep = new Queue<ulong>();
        private readonly int OwnerThread = Thread.CurrentThread.ManagedThreadId;
        private ulong NextToken, ObservationGeneration = 1;
        private bool ObserverContinuous, StartupCompletionQualified, StartupWindowOpen = true, Exhausted, Disposed;
        private readonly int CatalogCapacity;
        private MembershipCandidate[] CatalogSlots;
        private int[] CatalogFree;
        private int CatalogFreeCount, CatalogExtent, CatalogSweepPosition;
        private ulong CatalogSequence;
        private bool CatalogIncomplete;

        // Opt-in at model creation only. F1-only callers need no catalog;
        // the production discovery adapter selects its shared private policy.
        internal EntityLifetimeModel() : this(0) { }

        internal EntityLifetimeModel(int CatalogCapacity)
        {
            if (CatalogCapacity < 0) throw new ArgumentOutOfRangeException("CatalogCapacity");
            this.CatalogCapacity = CatalogCapacity;
            if (CatalogCapacity == 0) return;
            try
            {
                // Public discovery converts at most 256 matches at admission,
                // never every inspected candidate. Pre-size token storage cold:
                // no population-sized dictionary/queue resize in that callback.
                TokenRecords = new Dictionary<ulong, WeakReference>(CatalogCapacity);
                TokenSweep = new Queue<ulong>(CatalogCapacity);
                var Slots = new MembershipCandidate[CatalogCapacity];
                var Free = new int[CatalogCapacity];
                for (int Index = 0; Index < CatalogCapacity; ++Index) Free[Index] = CatalogCapacity - Index - 1;
                CatalogSlots = Slots;
                CatalogFree = Free;
                CatalogFreeCount = CatalogCapacity;
            }
            catch (Exception) { CatalogIncomplete = true; }
        }

        internal bool CatalogComplete
        {
            get { CheckOwner(); return CatalogCapacity != 0 && !CatalogIncomplete; }
        }

        internal bool CatalogReady
        {
            get { CheckOwner(); return CatalogComplete && ObserverContinuous && StartupCompletionQualified && !Disposed && !Exhausted; }
        }

        internal int CatalogSlotCount
        {
            get { CheckOwner(); return CatalogSlots == null ? 0 : CatalogCapacity - CatalogFreeCount; }
        }

        // Loss is sticky and discovery-only. Neither free capacity nor another
        // startup marker repairs an enrollment loss within this model instance.
        internal void InvalidateCatalog()
        {
            CheckOwner();
            if (CatalogCapacity != 0) CatalogIncomplete = true;
        }

        private bool CatalogObservation(MembershipCandidate Candidate)
        {
            if (Candidate == null || !ReferenceEquals(Candidate.Owner, this) ||
                !ObserverContinuous || Disposed || Exhausted ||
                Candidate.ObservationGeneration != ObservationGeneration) return false;
            object Identity = Candidate.Target;
            ObjectState State = Candidate.State;
            return Identity != null &&
                State.Epoch == Candidate.Epoch && State.ObservationGeneration == Candidate.ObservationGeneration &&
                State.State == SpawnState.Completed && !State.Poisoned &&
                (State.Current == null || !State.Current.Retired) &&
                State.CatalogSlot == Candidate.Slot && State.CatalogBirth == Candidate.Birth &&
                ReferenceEquals(CatalogSlots[Candidate.Slot], Candidate);
        }

        // Reconciliation can verify enrollment before discovery readiness opens.
        // This never reads host flags/registry occupancy or creates a token.
        internal bool HasCatalogObservation(object Identity)
        {
            CheckOwner();
            ObjectState State;
            if (!CatalogComplete || Identity == null || !States.TryGetValue(Identity, out State) ||
                State.CatalogSlot < 0 || State.CatalogSlot >= CatalogCapacity) return false;
            try
            {
                MembershipCandidate Candidate = CatalogSlots[State.CatalogSlot];
                return Candidate != null && ReferenceEquals(Candidate.State, State) &&
                    ReferenceEquals(Candidate.Target, Identity) && CatalogObservation(Candidate);
            }
            catch (Exception) { InvalidateCatalog(); return false; }
        }

        internal bool IsCatalogCandidateCurrent(MembershipCandidate Candidate)
        {
            CheckOwner();
            if (!CatalogReady) return false;
            try { return CatalogObservation(Candidate); }
            catch (Exception) { InvalidateCatalog(); return false; }
        }

        // Discovery is not proxy acquisition. Direct canonical state avoids CWT
        // probes and shared token-table growth/resizing inside a scan quantum.
        internal bool TryObserveCatalog(MembershipCandidate Candidate, Authority Authority,
            Func<Authority, bool> IsCurrent, Func<object, HostEvidence> ReadEvidence,
            out HostEvidence Evidence)
        {
            CheckOwner();
            Evidence = default(HostEvidence);
            if (ReadEvidence == null || !CurrentAuthority(Authority, IsCurrent) ||
                !IsCatalogCandidateCurrent(Candidate)) return false;
            object Identity = Candidate.Target;
            if (Identity == null) return false;
            ObjectState State = Candidate.State;
            bool Read = false;
            try { Evidence = ReadEvidence(Identity); Read = true; }
            catch (Exception) { }
            if (!CurrentAuthority(Authority, IsCurrent) || !IsCatalogCandidateCurrent(Candidate)) return false;
            if (!Read || !ValidEvidence(Identity, Evidence) ||
                (State.Current != null && !SameEvidence(State.Current, Identity, Evidence)) ||
                (Candidate.EvidenceCaptured && (Candidate.CapturedId != Evidence.NetworkId ||
                    !String.Equals(Candidate.CapturedPrefab, Evidence.Prefab, StringComparison.Ordinal)))) {
                State.State = SpawnState.Failed;
                if (State.Current != null) State.Current.Retired = true;
                RemoveCatalogObservation(State);
                Evidence = default(HostEvidence);
                return false;
            }
            if (!Candidate.EvidenceCaptured) {
                Candidate.CapturedId = Evidence.NetworkId;
                Candidate.CapturedPrefab = Evidence.Prefab;
                Candidate.EvidenceCaptured = true;
            }
            return true;
        }

        internal bool BeginCatalogScan(out MembershipCursor Cursor)
        {
            CheckOwner();
            Cursor = null;
            if (!CatalogReady) return false;
            try { Cursor = new MembershipCursor(this, CatalogExtent, CatalogSequence, ObservationGeneration); return true; }
            catch (Exception) { InvalidateCatalog(); return false; }
        }

        internal bool IsCatalogScanCurrent(MembershipCursor Cursor)
        {
            CheckOwner();
            return Cursor != null && ReferenceEquals(Cursor.Owner, this) && !Cursor.Invalid &&
                CatalogReady && Cursor.ObservationGeneration == ObservationGeneration;
        }

        // Maximum counts RAW slots, including holes, stale entries and births
        // above the acceptance watermark. Scratch belongs to the caller; no
        // callback, host access, world walk or per-step allocation occurs here.
        internal int InspectCatalog(MembershipCursor Cursor, int Maximum,
            MembershipCandidate[] Candidates, out int Written)
        {
            CheckOwner();
            Written = 0;
            if (Cursor == null || !ReferenceEquals(Cursor.Owner, this)) throw new ArgumentException("foreign catalog cursor", "Cursor");
            if (Maximum < 0) throw new ArgumentOutOfRangeException("Maximum");
            int Count = Math.Min(Maximum, Cursor.End - Cursor.Position);
            if (Candidates == null || Candidates.Length < Count) throw new ArgumentException("catalog scratch too small", "Candidates");
            if (!IsCatalogScanCurrent(Cursor)) { Cursor.Invalid = true; return 0; }
            int Inspected = 0;
            try
            {
                for (; Inspected < Count;)
                {
                    MembershipCandidate Candidate = CatalogSlots[Cursor.Position++];
                    Inspected++;
                    if (Candidate != null && Candidate.Birth <= Cursor.UpperBirth && CatalogObservation(Candidate))
                        Candidates[Written++] = Candidate;
                }
            }
            catch (Exception) { InvalidateCatalog(); Cursor.Invalid = true; Written = 0; }
            return Inspected;
        }

        private void ReleaseCatalogSlot(MembershipCandidate Candidate)
        {
            if (!ReferenceEquals(CatalogSlots[Candidate.Slot], Candidate) ||
                CatalogFreeCount < 0 || CatalogFreeCount >= CatalogCapacity)
            { InvalidateCatalog(); return; }
            ObjectState State = Candidate.State;
            if (State.CatalogSlot == Candidate.Slot && State.CatalogBirth == Candidate.Birth &&
                State.Epoch == Candidate.Epoch && State.ObservationGeneration == Candidate.ObservationGeneration)
            { State.CatalogSlot = -1; State.CatalogBirth = 0; }
            CatalogSlots[Candidate.Slot] = null;
            CatalogFree[CatalogFreeCount++] = Candidate.Slot;
        }

        private void RemoveCatalogObservation(ObjectState State)
        {
            if (CatalogSlots == null || State.CatalogSlot < 0) return;
            try
            {
                MembershipCandidate Candidate = CatalogSlots[State.CatalogSlot];
                if (Candidate == null || !ReferenceEquals(Candidate.State, State) || Candidate.Birth != State.CatalogBirth ||
                    Candidate.Epoch != State.Epoch || Candidate.ObservationGeneration != State.ObservationGeneration)
                { InvalidateCatalog(); return; }
                ReleaseCatalogSlot(Candidate);
            }
            catch (Exception) { InvalidateCatalog(); }
        }

        private void EnrollCatalog(object Identity, ObjectState State)
        {
            if (!CatalogComplete) return;
            try
            {
                if (State.CatalogSlot >= 0) { if (!HasCatalogObservation(Identity)) InvalidateCatalog(); return; }
                if (CatalogFreeCount <= 0 || CatalogFreeCount > CatalogCapacity || CatalogSequence == ulong.MaxValue)
                { InvalidateCatalog(); return; }
                int Slot = CatalogFree[CatalogFreeCount - 1];
                if (CatalogSlots[Slot] != null) { InvalidateCatalog(); return; }
                ulong Birth = CatalogSequence + 1;
                var Candidate = new MembershipCandidate(this, Identity, State, Slot, Birth);
                CatalogFreeCount--;
                CatalogSequence = Birth;
                CatalogSlots[Slot] = Candidate;
                State.CatalogSlot = Slot;
                State.CatalogBirth = Birth;
                CatalogExtent = Math.Max(CatalogExtent, Slot + 1);
            }
            catch (Exception) { InvalidateCatalog(); }
        }

        // No host-read callback: only weak identity, epoch/state and known sticky
        // record retirement. Temporary unadmitted host ineligibility is retained.
        internal int SweepCatalog(int Maximum)
        {
            CheckOwner();
            if (Maximum < 0) throw new ArgumentOutOfRangeException("Maximum");
            if (CatalogSlots == null) return 0;
            int Count = Math.Min(Maximum, CatalogExtent);
            int Inspected = 0;
            try
            {
                for (; Inspected < Count; ++Inspected)
                {
                    int Slot = CatalogSweepPosition;
                    CatalogSweepPosition = Slot + 1 == CatalogExtent ? 0 : Slot + 1;
                    MembershipCandidate Candidate = CatalogSlots[Slot];
                    if (Candidate != null && !CatalogObservation(Candidate)) ReleaseCatalogSlot(Candidate);
                }
            }
            catch (Exception) { InvalidateCatalog(); return Inspected + 1; }
            return Inspected;
        }

        private void RetireRecord(LifetimeRecord Record)
        {
            Record.Retired = true;
            if (CatalogSlots == null) return;
            object Identity = Record.Target;
            ObjectState State;
            // An old validation can throw AFTER reentrant successful Spawn. Its
            // retirement must never release the new epoch's membership link.
            if (Identity != null && States.TryGetValue(Identity, out State) &&
                ReferenceEquals(State.Current, Record) && State.Epoch == Record.Epoch &&
                State.ObservationGeneration == Record.ObservationGeneration)
                RemoveCatalogObservation(State);
            // Keep State.Current as the sticky tombstone until BeginSpawn.
        }

        private void CheckOwner()
        {
            if (Thread.CurrentThread.ManagedThreadId != OwnerThread)
                throw new InvalidOperationException("entity lifetime owner-thread required");
        }

        private void SweepTokens(int Maximum)
        {
            int Count = Math.Min(Maximum, TokenSweep.Count);
            for (int Index = 0; Index < Count; ++Index)
            {
                ulong Token = TokenSweep.Dequeue();
                WeakReference Reference;
                if (!TokenRecords.TryGetValue(Token, out Reference)) continue;
                LifetimeRecord Record = Reference.Target as LifetimeRecord;
                if (Record == null || Record.Retired || Record.Target == null) TokenRecords.Remove(Token);
                else TokenSweep.Enqueue(Token);
            }
        }

        internal void SweepRetiredTokens(int Maximum)
        {
            CheckOwner();
            if (Maximum < 0 || Maximum > 64) throw new ArgumentOutOfRangeException("Maximum");
            SweepTokens(Maximum);
        }

        internal bool TryBindToken(ulong Token, Authority Authority,
            Func<Authority, bool> IsCurrent, Func<object, HostEvidence> ReadEvidence,
            out Binding Result)
        {
            CheckOwner();
            Result = null;
            if (Token == 0 || Disposed || !CurrentAuthority(Authority, IsCurrent)) return false;
            SweepTokens(4);
            WeakReference Reference;
            if (!TokenRecords.TryGetValue(Token, out Reference)) return false;
            LifetimeRecord Record = Reference.Target as LifetimeRecord;
            if (Record == null || Record.Retired || Record.Token != Token) return false;
            var Candidate = new Binding(this, Record, Authority);
            if (!Validate(Candidate, IsCurrent, ReadEvidence)) return false;
            Result = Candidate;
            return true;
        }

        // This is a claim supplied by a separately qualified observer adapter;
        // object/registry snapshots cannot establish it themselves.
        internal void BeginQualifiedObservation()
        {
            CheckOwner();
            if (!StartupWindowOpen || Disposed || Exhausted) return;
            ObserverContinuous = true;
        }

        // The separately qualified world-load boundary only opens admission for
        // epochs whose full Spawn completion was already observed. It creates no
        // per-object state and cannot authorize an unseen or failed entity.
        internal bool QualifyStartupCompletion()
        {
            CheckOwner();
            if (!StartupWindowOpen || !ObserverContinuous || Disposed || Exhausted) return false;
            StartupCompletionQualified = true;
            StartupWindowOpen = false;
            return true;
        }

        // One-time startup reconciliation may inspect current registry members
        // without admitting them or creating a token. It must never manufacture
        // history for an object whose full Spawn completion was not observed.
        internal bool HasCompletedObservation(object Identity)
        {
            CheckOwner();
            ObjectState State;
            return Identity != null && ObserverContinuous && !Disposed && !Exhausted &&
                States.TryGetValue(Identity, out State) &&
                State.ObservationGeneration == ObservationGeneration &&
                State.State == SpawnState.Completed && !State.Poisoned;
        }

        // Host-observation witness only: no domain admission and no public
        // facade token is manufactured. B3 links pre-removal scalars to the
        // existing completed epoch/catalog birth, including startup epochs.
        internal MembershipCandidate CaptureCompletedWitness(object Identity)
        {
            CheckOwner();
            ObjectState State;
            if (!HasCompletedObservation(Identity) || !HasCatalogObservation(Identity) ||
                !States.TryGetValue(Identity,out State)) return null;
            return CatalogSlots[State.CatalogSlot];
        }
        internal bool IsWitnessEpochUnchanged(MembershipCandidate Witness)
        {
            CheckOwner();
            return Witness!=null&&ReferenceEquals(Witness.Owner,this)&&ObserverContinuous&&!Disposed&&
                Witness.ObservationGeneration==ObservationGeneration&&
                Witness.State.ObservationGeneration==Witness.ObservationGeneration&&Witness.State.Epoch==Witness.Epoch;
        }

        internal void BreakObserverContinuity()
        {
            CheckOwner();
            ObserverContinuous = false;
            StartupCompletionQualified = false;
            StartupWindowOpen = false;
            InvalidateCatalog();
            if (ObservationGeneration == ulong.MaxValue) Exhausted = true;
            else ObservationGeneration++;
            // No table walk: old records carry the previous generation and fail
            // closed at their next operation. Weak keys remain collectible.
        }

        // The adapter calls this synchronously at the OUTER full Spawn entry.
        // A nested same-object entry poisons that object rather than guessing
        // which invocation's later completion is authoritative.
        internal SpawnAttempt BeginSpawn(object Identity)
        {
            CheckOwner();
            if (Identity == null) return null;
            ObjectState State = States.GetValue(Identity, Key => new ObjectState());
            if (State.Current != null) RetireRecord(State.Current);
            RemoveCatalogObservation(State);
            State.Current = null;
            if (State.State == SpawnState.Pending && State.ObservationGeneration == ObservationGeneration)
                State.Poisoned = true;
            State.Attempt = null;
            State.ObservationGeneration = ObservationGeneration;
            if (State.Epoch == ulong.MaxValue) State.Poisoned = true;
            else State.Epoch++;
            if (!ObserverContinuous || Disposed || Exhausted || State.Poisoned)
            { State.State = SpawnState.Failed; return null; }
            State.State = SpawnState.Pending;
            State.Attempt = new SpawnAttempt(Identity, State);
            return State.Attempt;
        }

        // Both flags must come from the qualified full-call adapter. A skipped
        // original, exception, missing postfix, or stale attempt cannot complete.
        internal bool CompleteSpawn(SpawnAttempt Attempt, bool FullCallReturnedNormally, bool OriginalRan)
        {
            CheckOwner();
            if (Attempt == null || Disposed) return false;
            object Identity = Attempt.Target;
            ObjectState State;
            if (Identity == null || !States.TryGetValue(Identity, out State) ||
                !ReferenceEquals(State, Attempt.State) || !ReferenceEquals(State.Attempt, Attempt) ||
                State.Epoch != Attempt.Epoch || State.ObservationGeneration != Attempt.ObservationGeneration ||
                State.ObservationGeneration != ObservationGeneration || State.State != SpawnState.Pending)
                return false;
            State.Attempt = null;
            State.State = ObserverContinuous && !Exhausted && !State.Poisoned &&
                FullCallReturnedNormally && OriginalRan ? SpawnState.Completed : SpawnState.Failed;
            if (State.State == SpawnState.Completed) EnrollCatalog(Identity, State);
            return State.State == SpawnState.Completed;
        }

        private static bool ValidEvidence(object Identity, HostEvidence Evidence)
        {
            if (!Evidence.Alive || !Evidence.FullySpawned || !Evidence.HasNetworkObject ||
                Evidence.NetworkId == 0 || !ReferenceEquals(Identity, Evidence.RegistryOccupant) ||
                String.IsNullOrEmpty(Evidence.Prefab) || Evidence.Prefab.Length > 512 ||
                Evidence.Prefab.IndexOf('\0') >= 0) return false;
            // Every valid <=512-byte UTF-8 string has <=512 UTF-16 code units.
            // Reject larger host strings before either linear string traversal.
            try { return StrictUtf8.GetByteCount(Evidence.Prefab) <= 512; }
            catch (EncoderFallbackException) { return false; }
        }

        private static bool SameEvidence(LifetimeRecord Record, object Identity, HostEvidence Evidence)
        {
            return ValidEvidence(Identity, Evidence) && Evidence.NetworkId == Record.NetworkId &&
                String.Equals(Evidence.Prefab, Record.Prefab, StringComparison.Ordinal);
        }

        private static bool CurrentAuthority(Authority Authority, Func<Authority, bool> IsCurrent)
        {
            if (!Authority.IsValid || IsCurrent == null) return false;
            try { return IsCurrent(Authority); }
            catch (Exception) { return false; }
        }

        internal static bool SameLifetime(Binding Left, Binding Right)
        {
            return Left != null && Right != null && ReferenceEquals(Left.Owner, Right.Owner) &&
                Left.Record.Token == Right.Record.Token;
        }

        internal bool TryAdmit(object Identity, Authority Authority, Func<Authority, bool> IsCurrent,
            Func<object, HostEvidence> ReadEvidence, out Binding Result)
        {
            CheckOwner();
            Result = null;
            SweepTokens(4);
            if (Identity == null || ReadEvidence == null || !ObserverContinuous ||
                !StartupCompletionQualified || Disposed || Exhausted ||
                !CurrentAuthority(Authority, IsCurrent)) return false;
            ObjectState State;
            if (!States.TryGetValue(Identity, out State) ||
                State.ObservationGeneration != ObservationGeneration || State.State != SpawnState.Completed)
                return false;
            ulong ExpectedEpoch = State.Epoch;
            SpawnState ExpectedState = State.State;
            LifetimeRecord ExpectedRecord = State.Current;
            HostEvidence Evidence;
            try { Evidence = ReadEvidence(Identity); }
            catch (Exception)
            {
                if (State.Epoch == ExpectedEpoch && State.State == ExpectedState &&
                    ReferenceEquals(State.Current, ExpectedRecord))
                {
                    State.State = SpawnState.Failed;
                    if (State.Current != null) RetireRecord(State.Current);
                    RemoveCatalogObservation(State);
                }
                return false;
            }
            bool Authorized = CurrentAuthority(Authority, IsCurrent);
            if (!Authorized || Disposed || !ObserverContinuous || !StartupCompletionQualified ||
                State.ObservationGeneration != ObservationGeneration ||
                State.Epoch != ExpectedEpoch || State.State != ExpectedState ||
                !ReferenceEquals(State.Current, ExpectedRecord)) return false;
            if (!ValidEvidence(Identity, Evidence) ||
                (State.Current != null && !SameEvidence(State.Current, Identity, Evidence)))
            {
                if (State.Current != null) RetireRecord(State.Current);
                State.State = SpawnState.Failed;
                RemoveCatalogObservation(State);
                return false;
            }
            if (State.Current == null)
            {
                if (NextToken == ulong.MaxValue)
                { Exhausted = true; State.State = SpawnState.Failed; RemoveCatalogObservation(State); return false; }
                State.Current = new LifetimeRecord(Identity, ++NextToken, State, Evidence);
                TokenRecords.Add(State.Current.Token, new WeakReference(State.Current));
                TokenSweep.Enqueue(State.Current.Token);
            }
            if (State.Current.Retired) return false;
            Result = new Binding(this, State.Current, Authority);
            return true;
        }

        // Exact direct catalog -> public lifetime conversion. No registry
        // enumerator, CWT probe or key-based replacement lookup is involved.
        // Existing tokens retain F1 identity; new tokens have fixed-capacity
        // storage and four bounded weak-token maintenance opportunities.
        internal bool TryAdmitCatalog(MembershipCandidate Candidate, Authority Authority,
            Func<Authority, bool> IsCurrent, Func<object, HostEvidence> ReadEvidence,
            out Binding Result)
        {
            CheckOwner(); Result = null;
            SweepTokens(4);
            HostEvidence Evidence;
            if (!TryObserveCatalog(Candidate, Authority, IsCurrent, ReadEvidence, out Evidence)) return false;
            ObjectState State = Candidate.State;
            object Identity = Candidate.Target;
            if (Identity == null || !IsCatalogCandidateCurrent(Candidate)) return false;
            if (State.Current == null) {
                if (NextToken == ulong.MaxValue || TokenRecords.Count >= CatalogCapacity || TokenSweep.Count >= CatalogCapacity) return false;
                var Record = new LifetimeRecord(Identity, ++NextToken, State, Evidence);
                TokenRecords.Add(Record.Token, new WeakReference(Record));
                TokenSweep.Enqueue(Record.Token);
                State.Current = Record;
            }
            if (State.Current.Retired || State.Current.Epoch != Candidate.Epoch ||
                !SameEvidence(State.Current, Identity, Evidence) || !CurrentAuthority(Authority, IsCurrent)) return false;
            // Even a qualification delegate that changes lifecycle during its
            // final authority check cannot publish the just-retired candidate.
            if (!IsCatalogCandidateCurrent(Candidate) || State.Current == null || State.Current.Retired ||
                State.Current.Epoch != Candidate.Epoch) return false;
            Result = new Binding(this, State.Current, Authority);
            return true;
        }

        internal bool Validate(Binding Value, Func<Authority, bool> IsCurrent,
            Func<object, HostEvidence> ReadEvidence)
        {
            CheckOwner();
            if (Value == null || !ReferenceEquals(Value.Owner, this) || Value.Retired) return false;
            LifetimeRecord Record = Value.Record;
            if (Disposed || !ObserverContinuous || !StartupCompletionQualified || Record.Retired ||
                Record.ObservationGeneration != ObservationGeneration)
            { RetireRecord(Record); Value.Retired = true; return false; }
            if (!CurrentAuthority(Value.Authority, IsCurrent))
            { Value.Retired = true; return false; }
            object Identity = Record.Target;
            ObjectState State;
            if (Identity == null || !States.TryGetValue(Identity, out State) ||
                !ReferenceEquals(State.Current, Record) || State.Epoch != Record.Epoch ||
                State.ObservationGeneration != Record.ObservationGeneration ||
                State.State != SpawnState.Completed || ReadEvidence == null)
            { RetireRecord(Record); Value.Retired = true; return false; }
            ulong ExpectedEpoch = State.Epoch;
            HostEvidence Evidence;
            try { Evidence = ReadEvidence(Identity); }
            catch (Exception) { RetireRecord(Record); Value.Retired = true; return false; }
            bool Authorized = CurrentAuthority(Value.Authority, IsCurrent);
            if (!Authorized || Value.Retired || Disposed || !ObserverContinuous || !StartupCompletionQualified ||
                State.Epoch != ExpectedEpoch ||
                State.ObservationGeneration != ObservationGeneration ||
                !ReferenceEquals(State.Current, Record) || Record.Retired)
            { Value.Retired = true; return false; }
            if (!SameEvidence(Record, Identity, Evidence))
            { RetireRecord(Record); Value.Retired = true; return false; }
            return true;
        }

        internal void Retire(Binding Value)
        {
            CheckOwner();
            if (Value != null && ReferenceEquals(Value.Owner, this)) Value.Retired = true;
        }

        public void Dispose()
        {
            CheckOwner();
            if (Disposed) return;
            Disposed = true;
            BreakObserverContinuity();
            TokenRecords.Clear();
            TokenSweep.Clear();
            // Detach bounded pools in constant work even when an old binding,
            // candidate or cursor still holds this disposed model as its owner.
            CatalogSlots = null;
            CatalogFree = null;
            CatalogFreeCount = CatalogExtent = CatalogSweepPosition = 0;
        }
    }
}
