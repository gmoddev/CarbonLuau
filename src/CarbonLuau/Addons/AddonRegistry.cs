// Reference: System.IO.Compression
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class AddonDomainBinding
        {
            public string Id; public RuntimeDomain Target;
        }

        public sealed partial class AddonRegistry : IDisposable
        {
            private sealed class ReferenceComparer : IEqualityComparer<object>
            {
                public static readonly ReferenceComparer Instance = new ReferenceComparer();
                public new bool Equals(object Left, object Right) { return Object.ReferenceEquals(Left, Right); }
                public int GetHashCode(object Value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Value); }
            }
            private sealed class Registration
            {
                public object Provider; public string Token; public AddonPackageSnapshot Snapshot, PendingSnapshot;
                public AddonRegistrationState State; public RuntimeDomain Domain; public string Failure;
                public readonly Dictionary<string, DependencyBinding> Bindings = new Dictionary<string, DependencyBinding>(StringComparer.Ordinal);
                public bool RestorationPending;
            }
            private sealed class DependencyBinding
            {
                public string Id; public bool Optional; public Registration Target;
                public long VmGenerationId, DomainLifetimeId;
            }
            private readonly ScriptHost Host;
            private readonly long HostLifetimeId;
            private readonly int OwnerThread = Thread.CurrentThread.ManagedThreadId;
            private readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
            private readonly Dictionary<string, Registration> Ids = new Dictionary<string, Registration>(StringComparer.Ordinal);
            private readonly Dictionary<object, int> ProviderCounts = new Dictionary<object, int>(ReferenceComparer.Instance);
            private readonly Dictionary<string, object> Tombstones = new Dictionary<string, object>(StringComparer.Ordinal);
            private readonly Queue<string> TombstoneOrder = new Queue<string>();
            private long NextToken;
            private int AggregateSnapshotBytes;
            private bool Disposed;
            public AddonRegistry(ScriptHost Host, long HostLifetimeId) { this.Host = Host; this.HostLifetimeId = HostLifetimeId; }
            public bool HasPending { get { CheckOwner(); if (Disposed) return false; RefreshGraph(); return FindNext() != null; } }
            public int Count { get { return Registrations.Count; } }
            internal int SnapshotBytes { get { return AggregateSnapshotBytes; } }
            public string[] RegisterArchive(object Provider, byte[] Archive)
            { try { return Register(Provider, AddonPackageSnapshot.FromArchive(Archive)); } catch (Exception Error) { return ErrorResponse(Error); } }
            public string[] RegisterSource(object Provider, string Id, string Version, byte[] Source)
            { try { return Register(Provider, AddonPackageSnapshot.FromSource(Id, Version, Source)); } catch (Exception Error) { return ErrorResponse(Error); } }
            private string[] Register(object Provider, AddonPackageSnapshot Snapshot)
            {
                CheckOwner(); CheckLive(); if (Provider == null) throw new InvalidOperationException("provider object is required");
                if (Registrations.Count >= AddonPolicy.MaxRegistrations) throw new InvalidOperationException("registration limit reached");
                int ProviderCount; ProviderCounts.TryGetValue(Provider, out ProviderCount);
                if (ProviderCount >= AddonPolicy.MaxRegistrationsPerProvider) throw new InvalidOperationException("provider registration limit reached");
                if (Ids.ContainsKey(Snapshot.Id)) throw new InvalidOperationException("addon id is already reserved");
                if (AggregateSnapshotBytes > AddonPolicy.MaxAggregateSnapshotBytes - Snapshot.SourceBytes)
                    throw new InvalidOperationException("aggregate package snapshots exceed 32 MiB");
                if (NextToken == Int64.MaxValue) throw new InvalidOperationException("registration token space exhausted");
                string Token = HostLifetimeId.ToString("x16", CultureInfo.InvariantCulture) + "-" + (++NextToken).ToString("x16", CultureInfo.InvariantCulture);
                var Value = new Registration {Provider = Provider, Token = Token, Snapshot = Snapshot,
                    State = AddonRegistrationState.Registered, Failure = ""};
                Registrations.Add(Token, Value); Ids.Add(Snapshot.Id, Value); ProviderCounts[Provider] = ProviderCount + 1;
                AggregateSnapshotBytes += Snapshot.SourceBytes;
                RefreshGraph();
                return Response(Value);
            }
            public string[] Status(object Provider, string Token)
            {
                try { CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token); RefreshGraph(); return Response(Value); }
                catch (Exception Error) { return ErrorResponse(Error); }
            }
            public string[] ReplaceArchive(object Provider, string Token, byte[] Archive)
            { try { return Replace(Provider, Token, AddonPackageSnapshot.FromArchive(Archive)); } catch (Exception Error) { return ErrorResponse(Error); } }
            public string[] ReplaceSource(object Provider, string Token, string Version, byte[] Source)
            {
                try {
                    CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token);
                    return Replace(Provider, Token, AddonPackageSnapshot.FromSource(Value.Snapshot.Id, Version, Source));
                } catch (Exception Error) { return ErrorResponse(Error); }
            }
            private string[] Replace(object Provider, string Token, AddonPackageSnapshot Snapshot)
            {
                CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token);
                if (Value.State == AddonRegistrationState.Stopping) throw new InvalidOperationException("registration is stopping");
                if (!String.Equals(Value.Snapshot.Id, Snapshot.Id, StringComparison.Ordinal)) throw new InvalidOperationException("replacement id must match reserved id");
                if (Value.PendingSnapshot != null || Value.State == AddonRegistrationState.Initializing) throw new InvalidOperationException("replacement is already pending");
                if (AggregateSnapshotBytes > AddonPolicy.MaxAggregateSnapshotBytes - Snapshot.SourceBytes)
                    throw new InvalidOperationException("aggregate package snapshots exceed 32 MiB");
                AggregateSnapshotBytes += Snapshot.SourceBytes;
                Value.PendingSnapshot = Snapshot; Value.Failure = ""; Value.RestorationPending = Value.Domain == null;
                if (Value.Domain == null) Value.State = AddonRegistrationState.Blocked;
                RefreshGraph(); return Response(Value);
            }
            public string[] Unregister(object Provider, string Token)
            {
                try {
                    CheckOwner(); CheckLive(); Registration Value;
                    if (!Registrations.TryGetValue(Token ?? "", out Value)) {
                        object Owner;
                        if (Token != null && Tombstones.TryGetValue(Token, out Owner) && Object.ReferenceEquals(Owner, Provider))
                            return new[] {"OK", Token, "Stopping", "", "", "", "", "", ""};
                        throw new InvalidOperationException("stale registration token");
                    }
                    if (!Object.ReferenceEquals(Value.Provider, Provider)) throw new InvalidOperationException("registration owner mismatch");
                    Stop(Value, true); return new[] {"OK", Token, "Stopping", Value.Snapshot.Id, Value.Snapshot.Version, "", Value.Snapshot.Hash, "", ""};
                } catch (Exception Error) { return ErrorResponse(Error); }
            }
            public int UnloadProvider(object Provider)
            {
                CheckOwner(); if (Disposed || Provider == null) return 0;
                var Owned = new List<Registration>();
                foreach (Registration Value in Registrations.Values) if (Object.ReferenceEquals(Value.Provider, Provider)) Owned.Add(Value);
                Owned.Sort((Left, Right) => StringComparer.Ordinal.Compare(Left.Snapshot.Id, Right.Snapshot.Id));
                foreach (Registration Value in Owned) Stop(Value, false);
                RefreshGraph();
                return Owned.Count;
            }
            public bool ProcessOne()
            {
                CheckOwner(); if (Disposed) return false; RefreshGraph();
                if (!Host.Ready || Host.Busy) return false;
                Registration Value = FindNext();
                if (Value == null) return false;
                AddonPackageSnapshot CandidateSnapshot = Value.PendingSnapshot ?? Value.Snapshot;
                bool PendingReplacement = Value.PendingSnapshot != null;
                RuntimeDomain Previous = Value.Domain; bool Replacing = Previous != null && Previous.Alive;
                if (HasRequiredCycle(Value, CandidateSnapshot)) {
                    if (PendingReplacement) {
                        AggregateSnapshotBytes -= CandidateSnapshot.SourceBytes; Value.PendingSnapshot = null;
                        Value.Failure = "replacement blocked: required dependency cycle/SCC";
                        Value.State = Replacing ? AddonRegistrationState.Active : AddonRegistrationState.Blocked;
                    }
                    RefreshGraph(); return true;
                }
                string Missing = MissingRequired(CandidateSnapshot);
                if (Missing != null) { RefreshGraph(); return false; }
                Dictionary<string, DependencyBinding> CandidateBindings = BuildBindings(CandidateSnapshot);
                Value.State = AddonRegistrationState.Initializing; Value.Failure = "";
                AddonActivation Activation = Host.ActivateAddon(CandidateSnapshot, Previous, RuntimeBindings(CandidateBindings));
                if (!Registrations.ContainsKey(Value.Token) || Value.State == AddonRegistrationState.Stopping) {
                    if (Activation.Domain != null) Host.RetireAddon(Activation.Domain); return true;
                }
                if (Activation.Result.Status == RuntimeStatus.OK && Activation.Domain != null) {
                    if (!BindingsCurrent(CandidateBindings) || Activation.Domain.VmGenerationId != Host.VmGenerationId) {
                        Host.RetireAddon(Activation.Domain);
                        throw new InvalidOperationException("dependency binding changed during serialized addon activation");
                    }
                    if (PendingReplacement) AggregateSnapshotBytes -= Value.Snapshot.SourceBytes;
                    Value.Snapshot = CandidateSnapshot; Value.PendingSnapshot = null; Value.Domain = Activation.Domain;
                    Value.Bindings.Clear(); foreach (var Binding in CandidateBindings) Value.Bindings.Add(Binding.Key, Binding.Value);
                    Value.State = AddonRegistrationState.Active; Value.RestorationPending = false; Value.Failure = ActiveBindingDiagnostic(Value);
                } else {
                    if (PendingReplacement) AggregateSnapshotBytes -= CandidateSnapshot.SourceBytes;
                    Value.PendingSnapshot = null;
                    Value.Failure = AddonPolicy.Diagnostic((Replacing ? "replacement failed: " : Value.RestorationPending ? "restoration failed: " : "initialization failed: ") +
                        Activation.Result.Status + (String.IsNullOrEmpty(Activation.Result.Error) ? "" : " " + Activation.Result.Error));
                    Value.RestorationPending = false;
                    if (Replacing && Previous.Alive) { Value.Domain = Previous; Value.State = AddonRegistrationState.Active; }
                    else { Value.Domain = null; Value.State = AddonRegistrationState.Failed; }
                }
                RefreshGraph();
                return true;
            }
            private Registration FindOwned(object Provider, string Token)
            {
                Registration Value;
                if (Provider == null || Token == null || !Registrations.TryGetValue(Token, out Value)) throw new InvalidOperationException("stale registration token");
                if (!Object.ReferenceEquals(Value.Provider, Provider)) throw new InvalidOperationException("registration owner mismatch");
                return Value;
            }
            private void Stop(Registration Value, bool Refresh)
            {
                Value.State = AddonRegistrationState.Stopping;
                if (Value.Domain != null) { Host.RetireAddon(Value.Domain); Value.Domain = null; }
                Registrations.Remove(Value.Token); Ids.Remove(Value.Snapshot.Id);
                int Count = ProviderCounts[Value.Provider] - 1;
                if (Count == 0) ProviderCounts.Remove(Value.Provider); else ProviderCounts[Value.Provider] = Count;
                AggregateSnapshotBytes -= Value.Snapshot.SourceBytes;
                if (Value.PendingSnapshot != null) AggregateSnapshotBytes -= Value.PendingSnapshot.SourceBytes;
                AddTombstone(Value.Token, Value.Provider);
                Value.PendingSnapshot = null; Value.Bindings.Clear();
                if (Refresh) RefreshGraph();
            }
            private void AddTombstone(string Token, object Provider)
            {
                Tombstones[Token] = Provider; TombstoneOrder.Enqueue(Token);
                while (TombstoneOrder.Count > AddonPolicy.MaxTombstones) Tombstones.Remove(TombstoneOrder.Dequeue());
            }
            private string[] Response(Registration Value)
            {
                return new[] {"OK", Value.Token, Value.State.ToString(), Value.Snapshot.Id, Value.Snapshot.Version,
                    Value.Failure ?? "", Value.Snapshot.Hash, Value.Domain == null ? "" : Value.Domain.DomainLifetimeId.ToString(CultureInfo.InvariantCulture),
                    Value.PendingSnapshot == null ? "" : Value.PendingSnapshot.Version};
            }
            private static string[] ErrorResponse(Exception Error)
            { return new[] {"ERROR", "", "", "", "", AddonPolicy.Diagnostic(Error is InvalidOperationException ? Error.Message : "provider operation failed"), "", "", ""}; }
            private void CheckOwner() { if (Thread.CurrentThread.ManagedThreadId != OwnerThread) throw new InvalidOperationException("addon registry owner-thread required"); }
            private void CheckLive() { if (Disposed) throw new InvalidOperationException("stale CarbonLuau host lifetime"); }
            public void Dispose()
            {
                CheckOwner(); if (Disposed) return;
                var Values = new List<Registration>(Registrations.Values);
                Values.Sort((Left, Right) => StringComparer.Ordinal.Compare(Left.Snapshot.Id, Right.Snapshot.Id));
                foreach (Registration Value in Values) Stop(Value, false);
                Disposed = true; Tombstones.Clear(); TombstoneOrder.Clear(); ProviderCounts.Clear(); Ids.Clear();
            }
        }
    }
}


