namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal static class GuiHostCapabilities
        {
            internal const string TargetCarbonVersion = "2.0.259.0";
            internal const string TargetRustProtocol = "2633.288.1";
            internal const string TargetRustSteamBuild = "25230300";
            internal const string CarbonSourceRevision = "4d1b081eadef99da774e0342899bddcd638e26d2";
            internal const string RustCommunitySourceRevision = "c1aba1600e8cf3dc7087bed99fdc7f8aa174af13";
            internal const string OxideAdapterSourceRevision = "ca69c156382acfbb62804de0710d9555d5a206de";
            internal const bool OrderedCreate = true, UpdateExisting = true, DestroyBeforeCreate = true;
            internal const bool RectAnchorsAndOffsets = true, UpdateParent = true, UpdateSiblingIndex = true;
            internal const bool ButtonRunsServerCommand = true, NeedsCursor = true, ApplicationAcknowledgement = false;
        }
    }
}
