using System;
using System.Globalization;
using System.Threading;

namespace Carbon.Plugins
{
    // Entity-1B's bounded, read-only host adapter. Public script objects remain
    // in the trusted bootstrap; this layer never exports Rust/Unity references.
    public partial class CarbonLuau
    {
        partial void BindEntityFacade() { Gameplay.Entities = new EntityFacadeAdapter(this); }

        private sealed class EntityFacadeAdapter : IEntityFacadeHost
        {
            private readonly CarbonLuau Owner;
            internal EntityFacadeAdapter(CarbonLuau Owner) { this.Owner = Owner; }
            public string[] Lookup(FacadeSession Session, string Id) { return Owner.LookupEntity(Session, Id); }
            public string[] Read(FacadeSession Session, string Token, string Publication, string Property)
            { return Owner.ReadEntity(Session, Token, Publication, Property); }
        }

        // Startup reconciliation supplies one already-proven completed entity.
        // Warm the bounded managed lookup/read path outside any Luau deadline;
        // the temporary session publishes no script-visible proxy or event.
        private void WarmEntityReadPath(BaseEntity Representative)
        {
            var Session = new FacadeSession(Gameplay, 1, 1, 1);
            try {
                string Id = Representative.net.ID.Value.ToString(CultureInfo.InvariantCulture);
                string[] Fields = LookupEntity(Session, Id);
                if (Fields.Length != 4 || ReadEntity(Session, Fields[1], Fields[3], "Id").Length != 1 ||
                    ReadEntity(Session, Fields[1], Fields[3], "Prefab").Length != 1)
                    BreakEntityObserver("read-only Entity startup warmup failed");
            }
            catch (Exception) { BreakEntityObserver("read-only Entity startup warmup failed"); }
            finally { Session.Disposed = true; Session.Clear(); }
        }

        private static ulong ParseEntityNumber(string Value, string Label)
        {
            ulong Parsed;
            if (String.IsNullOrEmpty(Value) || Value.Length > 20 || Value[0] == '0' ||
                !ulong.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out Parsed) ||
                Parsed == 0 || !String.Equals(Value, Parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                throw new FacadeException("invalid " + Label);
            return Parsed;
        }

        private void RequireEntityWorld()
        {
            if (Stopping || Native == null || Gameplay == null || EntityLifetimes == null ||
                Thread.CurrentThread.ManagedThreadId != EntityOwnerThread ||
                EntityObserverBroken || !EntityHostPinned || !EntityStartupQualified)
                throw new FacadeException("Entity world is unavailable");
        }

        private void RequireEntityPatchPath(BaseEntity Entity)
        {
            bool Current;
            try { Current = EntityReadPatchPathCurrent(Entity); }
            catch (Exception) { Current = false; }
            if (!Current) {
                BreakEntityObserver("read-only Entity Spawn patch record changed or effective Spawn is not pinned");
                throw new FacadeException("Entity world is unavailable");
            }
        }

        private string[] LookupEntity(FacadeSession Session, string Id)
        {
            ulong Parsed = ParseEntityNumber(Id, "Entity ID");
            RequireEntityWorld();
            BaseNetworkable Occupant;
            try { Occupant = BaseNetworkable.serverEntities.Find(new NetworkableId(Parsed)); }
            catch (Exception) {
                BreakEntityObserver("read-only Entity keyed registry lookup failed");
                throw new FacadeException("Entity world is unavailable");
            }
            BaseEntity Entity = Occupant as BaseEntity;
            if (Entity == null) return new string[0];
            RequireEntityPatchPath(Entity);
            EntityFacadeBinding Binding;
            // Admission uses the exact occupant returned by the single keyed
            // lookup. Subsequent operations use fresh registry evidence.
            if (!TryAdmitEntity(Entity, Session, Identity => {
                BaseEntity Current = Identity as BaseEntity;
                if (Current == null || Current.net == null)
                    return new EntityLifetimeModel.HostEvidence(false, false, false, 0, null, null);
                return new EntityLifetimeModel.HostEvidence(!Current.IsDestroyed,
                    Current.IsFullySpawned(), true, Current.net.ID.Value, Current.PrefabName, Occupant);
            }, true, out Binding)) {
                if (EntityObserverBroken) throw new FacadeException("Entity world is unavailable");
                throw new FacadeException("Entity is not admissible");
            }
            return new[] {
                Native.HostLifetimeId.ToString(CultureInfo.InvariantCulture),
                Binding.Lifetime.Record.Token.ToString(CultureInfo.InvariantCulture),
                Binding.Lifetime.Record.NetworkId.ToString(CultureInfo.InvariantCulture),
                Binding.Publication.Token.ToString(CultureInfo.InvariantCulture)
            };
        }

        private string[] ReadEntity(FacadeSession Session, string Token, string Publication, string Property)
        {
            ulong ParsedToken = ParseEntityNumber(Token, "Entity token");
            ulong ParsedPublication = ParseEntityNumber(Publication, "Entity publication");
            RequireEntityWorld();
            FacadeSession.PublicationWitness Witness = Session.FindEntityWitness(ParsedPublication);
            if (Witness == null || Session.Disposed || Session.VmGenerationId <= 0 || Session.DomainLifetimeId <= 0)
                throw new FacadeException("stale Entity reference");
            var Authority = new EntityLifetimeModel.Authority(checked((ulong)Session.VmGenerationId),
                checked((ulong)Session.DomainLifetimeId), Witness.Token);
            EntityLifetimeModel.Binding Binding;
            if (!EntityLifetimes.TryBindToken(ParsedToken, Authority,
                Value => EntityFacadeCurrent(Value, Session, Witness), ReadEntityEvidence, out Binding))
                throw new FacadeException("stale Entity reference");
            BaseEntity Entity = Binding.Record.Target as BaseEntity;
            if (Entity == null) throw new FacadeException("stale Entity reference");
            RequireEntityPatchPath(Entity);
            if (Property == "Id") return new[] { Binding.Record.NetworkId.ToString(CultureInfo.InvariantCulture) };
            if (Property == "Prefab") return new[] { Binding.Record.Prefab };
            if (Property == "Position") {
                UnityEngine.Vector3 Position;
                try { Position = Entity.transform.position; }
                catch (Exception) { throw new FacadeException("Entity position is invalid"); }
                if (Single.IsNaN(Position.x) || Single.IsInfinity(Position.x) ||
                    Single.IsNaN(Position.y) || Single.IsInfinity(Position.y) ||
                    Single.IsNaN(Position.z) || Single.IsInfinity(Position.z))
                    throw new FacadeException("Entity position is invalid");
                return new[] { ((double)Position.x).ToString("R", CultureInfo.InvariantCulture),
                    ((double)Position.y).ToString("R", CultureInfo.InvariantCulture),
                    ((double)Position.z).ToString("R", CultureInfo.InvariantCulture) };
            }
            throw new FacadeException("unknown Entity field");
        }
    }
}
