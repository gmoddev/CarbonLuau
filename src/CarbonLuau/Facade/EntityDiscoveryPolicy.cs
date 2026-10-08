namespace Carbon.Plugins
{
    // Shared traversal/public-discovery resource policy; no package release.
    internal static class EntityDiscoveryPolicy
    {
        internal const int CatalogSlots = 262144; // Existing qualified startup registry cap.
        internal const int MaximumQueries = 8;
        internal const int MaximumPerDomain = 2;
        internal const int WorkPerTurn = 1024;
        internal const int RawSlotsPerTurn = 1024;
        internal const int MaximumResults = 256;
        internal const int MaximumDeliveriesPerTurn = 2;
        internal const int MaximumPrefabBytes = 512; // D20, unchanged.
        internal const int CatalogSweepPerTurn = 64;
        internal const int DeadlineSeconds = 120;
        internal const int MaximumPatchRecords = 8192;
        internal const int MaximumEntityTypes = 4096;
    }
}
