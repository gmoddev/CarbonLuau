// Research-only fixture: requests Carbon's normal Spawn hooks without adding
// Harmony patches. The production Entity observer must accept only the pinned
// Carbon hook pair when this ordinary subscription installs it.
namespace Oxide.Plugins
{
    [Info("CarbonLuau.EntityHookDemandEvidence", "CarbonLuau", "0.1.0")]
    [Description("Entity-1A exact Carbon Spawn-hook demand probe")]
    public sealed class CarbonLuauEntityHookDemandEvidence : RustPlugin
    {
        private int SpawnPrefixes;
        private int SpawnedHooks;

        private void OnEntitySpawn(BaseNetworkable Entity)
        {
            SpawnPrefixes++;
        }

        private void OnEntitySpawned(BaseNetworkable Entity)
        {
            SpawnedHooks++;
        }

        private void OnServerInitialized()
        {
            Puts("[CarbonLuau:EntityHookDemand] READY prefixes=" + SpawnPrefixes +
                " spawned=" + SpawnedHooks);
        }
    }
}
