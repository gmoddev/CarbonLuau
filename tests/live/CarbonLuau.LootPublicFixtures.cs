// Qualification-only selector; absent from production packages.
namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        partial void SelectPublicLootQualification(ref bool Public) { Public = true; }
    }
}
