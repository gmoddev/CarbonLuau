// Qualification-only selector; absent from production packages.
namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // D3 remains barrel-1. MAIN's test-only D4 package implements this
        // erased selector; absence is the established D3 default.
        partial void SelectBarrel2Qualifier(ref bool Barrel2);
        partial void SelectPublicLootQualification(ref bool Public)
        {
            Public = true;
            SelectBarrel2Qualifier(ref LootFixtureBarrel2);
        }
    }
}
