using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {

        internal struct GuiMutationScope
        {
            internal readonly ulong ResourceOwnerDomainLifetimeId, PublicationContextId;
            internal readonly bool Provisional;
            internal GuiMutationScope(ulong ResourceOwnerDomainLifetimeId, ulong PublicationContextId, bool Provisional)
            {
                if (ResourceOwnerDomainLifetimeId == 0 || PublicationContextId == 0)
                    throw new InvalidOperationException("GUI mutation scope identities must be nonzero");
                this.ResourceOwnerDomainLifetimeId = ResourceOwnerDomainLifetimeId;
                this.PublicationContextId = PublicationContextId; this.Provisional = Provisional;
            }
        }

        internal interface IGuiRetainedRegistry
        {
            bool IsDomainLive(ulong VmGenerationId, ulong DomainLifetimeId);
            bool TryGetClass(GuiObjectIdentity Identity, out GuiClassId ClassId);
        }
    }
}
