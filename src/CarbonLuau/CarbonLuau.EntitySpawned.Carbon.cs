using System;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Runs only after D20's existing complete outer virtual Spawn fence.
        // Startup reconciliation never calls this and creates no event history.
        private void ObserveEntitySpawned(BaseEntity Entity)
        {
            if (Thread.CurrentThread.ManagedThreadId != EntityOwnerThread || Stopping ||
                EntityObserverBroken || !EntityStartupQualified || Entity == null ||
                Gameplay == null || Native == null || Host == null || !Host.Ready) return;
            try {
                if (!Gameplay.GameplayEvents.Capture("entityspawned")) return;
                RequireEntityPatchPath(Entity);
                Gameplay.EntitySpawned(checked((ulong)Native.HostLifetimeId), Session => {
                    FacadeSession.PublicationWitness Witness = Session.CaptureCommittedEntityWitness();
                    var Authority = new EntityLifetimeModel.Authority(checked((ulong)Session.VmGenerationId),
                        checked((ulong)Session.DomainLifetimeId), Witness.Token);
                    EntityLifetimeModel.Binding Binding;
                    return TryAdmitEntity(Entity, Authority, Value => EntityFacadeCurrent(Value, Session, Witness),
                        ReadEntityEvidence, true, out Binding) ? Binding : null;
                });
                RequestDrain();
            } catch (Exception) { Gameplay.GameplayEvents.RejectTransfer(); }
        }
    }
}
