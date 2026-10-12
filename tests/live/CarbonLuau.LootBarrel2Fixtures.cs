// TEST ONLY: explicit D4 selection; never packed in production.
namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        partial void SelectBarrel2Qualifier(ref bool Barrel2) { Barrel2 = true; }
    }
}
