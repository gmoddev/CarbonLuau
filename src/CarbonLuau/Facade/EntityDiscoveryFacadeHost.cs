namespace Carbon.Plugins
{
    // Private transport only. Callback routes and catalog observations never
    // become public values. Native owns callback captures; the host owns results.
    internal interface IEntityDiscoveryFacadeHost
    {
        string[] Submit(CarbonLuau.FacadeSession Session, string[] Fields);
        string[] Admit(CarbonLuau.FacadeSession Session, string Route);
        void Release(CarbonLuau.FacadeSession Session, string Route);
        void Retire(CarbonLuau.FacadeSession Session);
    }
}
