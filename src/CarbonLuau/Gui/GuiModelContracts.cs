using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal struct GuiObjectIdentity : IEquatable<GuiObjectIdentity>
        {
            internal readonly ulong VmGenerationId, DomainLifetimeId, GuiObjectId;
            internal GuiObjectIdentity(ulong VmGenerationId, ulong DomainLifetimeId, ulong GuiObjectId)
            {
                if (VmGenerationId == 0 || DomainLifetimeId == 0 || GuiObjectId == 0)
                    throw new InvalidOperationException("GUI object identity components must be nonzero");
                this.VmGenerationId = VmGenerationId; this.DomainLifetimeId = DomainLifetimeId; this.GuiObjectId = GuiObjectId;
            }
            public bool Equals(GuiObjectIdentity Other)
            { return VmGenerationId == Other.VmGenerationId && DomainLifetimeId == Other.DomainLifetimeId && GuiObjectId == Other.GuiObjectId; }
            public override bool Equals(object Value) { return Value is GuiObjectIdentity && Equals((GuiObjectIdentity)Value); }
            public override int GetHashCode()
            { unchecked { return ((int)VmGenerationId * 397) ^ ((int)DomainLifetimeId * 31) ^ (int)GuiObjectId; } }
        }

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
