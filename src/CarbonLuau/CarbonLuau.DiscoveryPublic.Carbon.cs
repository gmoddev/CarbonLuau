using System;
using System.Globalization;
using System.Diagnostics;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private sealed class DiscoveryDelivery
        {
            internal FacadeSession Session;
            internal NativeRuntime Runtime;
            internal FacadeSession.PublicationWitness Publication;
            internal EntityLifetimeModel.Authority Authority;
            internal ulong Route, Traversal;
            internal long Deadline;
            internal EntityDiscoveryTraversal.Completion Completion;
        }
        // Includes requests waiting in native intake: traversal completion does
        // not release the public concurrency or retained-result bound.
        private readonly DiscoveryDelivery[] DiscoveryDeliveries = new DiscoveryDelivery[EntityDiscoveryPolicy.MaximumQueries];

        private sealed class EntityDiscoveryFacadeAdapter : IEntityDiscoveryFacadeHost
        {
            private readonly CarbonLuau Owner;
            internal EntityDiscoveryFacadeAdapter(CarbonLuau Owner) { this.Owner = Owner; }
            public string[] Submit(FacadeSession Session, string[] Fields) { return Owner.SubmitDiscovery(Session, Fields); }
            public string[] Admit(FacadeSession Session, string Route) { return Owner.AdmitDiscovery(Session, Route); }
            public void Release(FacadeSession Session, string Route) { Owner.ReleaseDiscovery(Session, Route); }
            public void Retire(FacadeSession Session) { Owner.RetireDiscovery(Session); }
        }

        private bool DiscoveryDeliveryCurrent(DiscoveryDelivery Value)
        { return !Stopping && ReferenceEquals(Native, Value.Runtime) && Gameplay != null &&
            Value.Session.Active && Gameplay.IsActive(Value.Session) &&
            EntityFacadeCurrent(Value.Authority, Value.Session, Value.Publication); }

        private bool DiscoveryResultsCurrent(DiscoveryDelivery Value)
        { return DiscoveryDeliveryCurrent(Value) && EntityLifetimes != null && EntityLifetimes.CatalogReady &&
            EntityDiscoveryAuthorityCurrent(Value.Session, Value.Publication, Value.Authority, Value.Runtime); }

        private static double DiscoveryNumber(string Text)
        {
            double Value;
            if (Text == null || Text.Length > 64 || !Double.TryParse(Text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out Value) || Double.IsNaN(Value) || Double.IsInfinity(Value))
                throw new FacadeException("InvalidDiscovery");
            return Value;
        }

        private string[] SubmitDiscovery(FacadeSession Session, string[] Fields)
        {
            if (Fields.Length != 7) throw new FacadeException("InvalidDiscovery");
            ulong Route = ParseEntityNumber(Fields[0], "discovery route");
            double X = DiscoveryNumber(Fields[1]), Y = DiscoveryNumber(Fields[2]), Z = DiscoveryNumber(Fields[3]),
                Radius = DiscoveryNumber(Fields[4]);
            int Limit;
            if (Radius < 0 || !Int32.TryParse(Fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out Limit) ||
                Limit < 1 || Limit > EntityDiscoveryPolicy.MaximumResults) throw new FacadeException("InvalidDiscovery");
            string Prefab = Fields[5].Length == 0 ? null : Fields[5];
            if (Prefab != null) FacadePolicy.Text(Prefab, EntityDiscoveryPolicy.MaximumPrefabBytes, "discovery prefab");
            int Free = -1, DomainCount = 0;
            for (int Index = 0; Index < DiscoveryDeliveries.Length; ++Index) {
                DiscoveryDelivery Existing = DiscoveryDeliveries[Index];
                if (Existing == null) { if (Free < 0) Free = Index; continue; }
                if (ReferenceEquals(Existing.Session, Session)) {
                    if (Existing.Route == Route) throw new FacadeException("InvalidDiscovery");
                    DomainCount++;
                }
            }
            if (Free < 0 || DomainCount >= EntityDiscoveryPolicy.MaximumPerDomain)
                throw new FacadeException("DiscoveryCapacity");
            RequireEntityWorld();
            if (EntityDiscovery == null || Session.FacadeVm == 0) throw new FacadeException("DiscoveryUnavailable");
            FacadeSession.PublicationWitness Publication = Session.CapturePublicationWitness();
            var Value = new DiscoveryDelivery { Session = Session, Runtime = Native, Route = Route,
                Publication = Publication, Authority = new EntityLifetimeModel.Authority(
                    checked((ulong)Session.VmGenerationId), checked((ulong)Session.DomainLifetimeId), Publication.Token),
                Deadline = checked(EntityDiscoveryClock + Stopwatch.Frequency * EntityDiscoveryPolicy.DeadlineSeconds) };
            DiscoveryDeliveries[Free] = Value;
            try {
                ulong Traversal;
                EntityDiscoveryTraversal.StartStatus Status = StartEntityDiscovery(Session,
                    new EntityDiscoveryTraversal.Query(X, Y, Z, Radius, Prefab, Limit), Completion => {
                        if (!ReferenceEquals(DiscoveryDeliveries[Free], Value) || !DiscoveryDeliveryCurrent(Value)) return;
                        Value.Completion = Completion;
                        // This is bounded data intake, never recursive Luau entry.
                        try {
                            byte[] Payload = FacadePolicy.Pack(new[] {"discovery", Route.ToString(CultureInfo.InvariantCulture)});
                            RuntimeStatus Intake = Value.Runtime.DomainEvent(Session.FacadeVm, Value.Authority.DomainLifetime, Payload);
                            if (Intake != RuntimeStatus.OK) throw new FacadeException("Discovery completion intake failed");
                            RequestDrain();
                        }
                        catch (Exception) {
                            // An ingress fault cannot establish native ownership.
                            // Retire, never retry a possibly retained completion.
                            ReleaseNative();
                            PrintError("[CarbonLuau:Discovery] Scripting stopped after completion intake failure.");
                        }
                    }, out Traversal, () => DiscoveryDeliveryCurrent(Value));
                if (Status != EntityDiscoveryTraversal.StartStatus.Accepted) {
                    throw new FacadeException(Status == EntityDiscoveryTraversal.StartStatus.Capacity ? "DiscoveryCapacity" :
                        Status == EntityDiscoveryTraversal.StartStatus.Invalid ? "InvalidDiscovery" : "DiscoveryUnavailable");
                }
                Value.Traversal = Traversal;
                return new string[0];
            }
            catch { DiscoveryDeliveries[Free] = null; throw; }
        }

        private static string DiscoveryError(EntityDiscoveryTraversal.Outcome Status)
        {
            switch (Status) {
                case EntityDiscoveryTraversal.Outcome.Success: return "";
                case EntityDiscoveryTraversal.Outcome.Deadline: return "DiscoveryDeadline";
                case EntityDiscoveryTraversal.Outcome.ResultLimit: return "DiscoveryResultLimit";
                case EntityDiscoveryTraversal.Outcome.RawLimit: return "DiscoveryWorkLimit";
                case EntityDiscoveryTraversal.Outcome.Cancelled: return "DiscoveryCancelled";
                case EntityDiscoveryTraversal.Outcome.StaleResult: return "DiscoveryStaleResult";
                case EntityDiscoveryTraversal.Outcome.ProducerFailure:
                case EntityDiscoveryTraversal.Outcome.InvalidObservation: return "DiscoveryReadFailed";
                default: return "DiscoveryUnavailable";
            }
        }

        private string[] AdmitDiscovery(FacadeSession Session, string RouteText)
        {
            ulong Route = ParseEntityNumber(RouteText, "discovery route");
            DiscoveryDelivery Value = null;
            for (int Index = 0; Index < DiscoveryDeliveries.Length; ++Index)
                if (DiscoveryDeliveries[Index] != null && ReferenceEquals(DiscoveryDeliveries[Index].Session, Session) &&
                    DiscoveryDeliveries[Index].Route == Route) {
                    Value = DiscoveryDeliveries[Index]; DiscoveryDeliveries[Index] = null; break;
                }
            if (Value == null || Value.Completion == null || !DiscoveryDeliveryCurrent(Value))
                return new[] {"DiscoveryUnavailable"};
            if (EntityDiscoveryClock >= Value.Deadline) return new[] {"DiscoveryDeadline"};
            string Error = DiscoveryError(Value.Completion.Status);
            if (Error.Length != 0) return new[] {Error};
            if (!DiscoveryResultsCurrent(Value)) return new[] {"DiscoveryUnavailable"};
            int Count = Value.Completion.Count;
            var Fields = new string[1 + Count * 4]; Fields[0] = "";
            Func<EntityLifetimeModel.Authority, bool> Current = Authority => DiscoveryResultsCurrent(Value) &&
                Authority.VmGeneration == Value.Authority.VmGeneration && Authority.DomainLifetime == Value.Authority.DomainLifetime &&
                Authority.PublicationLifetime == Value.Authority.PublicationLifetime;
            // Capture exactly the committed callback publication, never create
            // authority by package ID or resolve a result by its network ID.
            FacadeSession.PublicationWitness Witness = Session.CaptureEntityWitness();
            if (!ReferenceEquals(Witness, Value.Publication)) return new[] {"DiscoveryUnavailable"};
            for (int Index = 0; Index < Count; ++Index) {
                EntityDiscoveryTraversal.CandidateObservation Observation = Value.Completion.GetResult(Index);
                BaseEntity Entity = Observation.Candidate.Target as BaseEntity;
                EntityLifetimeModel.Binding Binding;
                if (ReferenceEquals(Entity, null) || !EntityDiscoveryPatchCurrent(Entity) ||
                    !EntityLifetimes.TryAdmitCatalog(Observation.Candidate, Value.Authority, Current,
                        ReadDiscoveryEntityEvidence, out Binding) || Binding.Record.NetworkId != Observation.Id ||
                    !String.Equals(Binding.Record.Prefab, Observation.Prefab, StringComparison.Ordinal))
                    return new[] {"DiscoveryStaleResult"};
                int Offset = 1 + Index * 4;
                Fields[Offset] = Value.Runtime.HostLifetimeId.ToString(CultureInfo.InvariantCulture);
                Fields[Offset + 1] = Binding.Record.Token.ToString(CultureInfo.InvariantCulture);
                Fields[Offset + 2] = Binding.Record.NetworkId.ToString(CultureInfo.InvariantCulture);
                Fields[Offset + 3] = Witness.Token.ToString(CultureInfo.InvariantCulture);
            }
            // A callback-free final epoch pass precedes returning the complete
            // response. No proxy is returned if any exact result retired.
            for (int Index = 0; Index < Count; ++Index)
                if (!EntityLifetimes.IsCatalogCandidateCurrent(Value.Completion.GetResult(Index).Candidate))
                    return new[] {"DiscoveryStaleResult"};
            if (!DiscoveryResultsCurrent(Value)) return new[] {"DiscoveryUnavailable"};
            if (EntityDiscoveryClock >= Value.Deadline) return new[] {"DiscoveryDeadline"};
            return Fields;
        }

        private void ReleaseDiscovery(FacadeSession Session, string RouteText)
        {
            ulong Route = ParseEntityNumber(RouteText, "discovery route");
            for (int Index = 0; Index < DiscoveryDeliveries.Length; ++Index) {
                DiscoveryDelivery Value = DiscoveryDeliveries[Index];
                if (Value == null || !ReferenceEquals(Value.Session, Session) || Value.Route != Route) continue;
                DiscoveryDeliveries[Index] = null;
                if (EntityDiscovery != null) EntityDiscovery.Cancel(Value.Traversal);
            }
        }

        private void RetireDiscovery(FacadeSession Session)
        {
            for (int Index = 0; Index < DiscoveryDeliveries.Length; ++Index) {
                DiscoveryDelivery Value = DiscoveryDeliveries[Index];
                if (Value == null || (Session != null && !ReferenceEquals(Value.Session, Session))) continue;
                DiscoveryDeliveries[Index] = null;
                if (EntityDiscovery != null) EntityDiscovery.Cancel(Value.Traversal);
            }
        }
    }
}
