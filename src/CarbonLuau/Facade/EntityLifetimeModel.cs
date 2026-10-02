using System;
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
            internal ulong Epoch, ObservationGeneration;
            internal SpawnState State;
            internal bool Poisoned;
            internal SpawnAttempt Attempt;
            internal LifetimeRecord Current;
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
        private readonly int OwnerThread = Thread.CurrentThread.ManagedThreadId;
        private ulong NextToken, ObservationGeneration = 1;
        private bool ObserverContinuous, StartupCompletionQualified, StartupWindowOpen = true, Exhausted, Disposed;

        private void CheckOwner()
        {
            if (Thread.CurrentThread.ManagedThreadId != OwnerThread)
                throw new InvalidOperationException("entity lifetime owner-thread required");
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

        internal void BreakObserverContinuity()
        {
            CheckOwner();
            ObserverContinuous = false;
            StartupCompletionQualified = false;
            StartupWindowOpen = false;
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
            if (State.Current != null) State.Current.Retired = true;
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
            return State.State == SpawnState.Completed;
        }

        private static bool ValidEvidence(object Identity, HostEvidence Evidence)
        {
            if (!Evidence.Alive || !Evidence.FullySpawned || !Evidence.HasNetworkObject ||
                Evidence.NetworkId == 0 || !ReferenceEquals(Identity, Evidence.RegistryOccupant) ||
                String.IsNullOrEmpty(Evidence.Prefab) || Evidence.Prefab.IndexOf('\0') >= 0) return false;
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
                { State.State = SpawnState.Failed; if (State.Current != null) State.Current.Retired = true; }
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
                if (State.Current != null) State.Current.Retired = true;
                State.State = SpawnState.Failed;
                return false;
            }
            if (State.Current == null)
            {
                if (NextToken == ulong.MaxValue) { Exhausted = true; State.State = SpawnState.Failed; return false; }
                State.Current = new LifetimeRecord(Identity, ++NextToken, State, Evidence);
            }
            if (State.Current.Retired) return false;
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
            { Record.Retired = true; Value.Retired = true; return false; }
            if (!CurrentAuthority(Value.Authority, IsCurrent))
            { Value.Retired = true; return false; }
            object Identity = Record.Target;
            ObjectState State;
            if (Identity == null || !States.TryGetValue(Identity, out State) ||
                !ReferenceEquals(State.Current, Record) || State.Epoch != Record.Epoch ||
                State.ObservationGeneration != Record.ObservationGeneration ||
                State.State != SpawnState.Completed || ReadEvidence == null)
            { Record.Retired = true; Value.Retired = true; return false; }
            ulong ExpectedEpoch = State.Epoch;
            HostEvidence Evidence;
            try { Evidence = ReadEvidence(Identity); }
            catch (Exception) { Record.Retired = true; Value.Retired = true; return false; }
            bool Authorized = CurrentAuthority(Value.Authority, IsCurrent);
            if (!Authorized || Value.Retired || Disposed || !ObserverContinuous || !StartupCompletionQualified ||
                State.Epoch != ExpectedEpoch ||
                State.ObservationGeneration != ObservationGeneration ||
                !ReferenceEquals(State.Current, Record) || Record.Retired)
            { Value.Retired = true; return false; }
            if (!SameEvidence(Record, Identity, Evidence))
            { Record.Retired = true; Value.Retired = true; return false; }
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
        }
    }
}
