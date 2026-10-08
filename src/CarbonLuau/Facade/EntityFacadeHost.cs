namespace Carbon.Plugins
{
    // The public read surface uses the ordinary bounded facade transport.
    // Rust/Unity types remain confined to the Carbon-specific implementation.
    internal interface IEntityFacadeHost
    {
        string[] Lookup(CarbonLuau.FacadeSession Session, string Id);
        string[] Read(CarbonLuau.FacadeSession Session, string Token, string Publication, string Property);
    }
}
