using System;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private AddonRegistry Addons;
        private bool AddonFrameScheduled;

        private string[] CarbonLuau_AddonProtocol()
        {
            return new[] {"OK", AddonPolicy.ProtocolName, AddonPolicy.ProtocolVersion,
                Addons == null ? "unavailable" : "archive,source,status,replace,unregister,dependencies"};
        }
        private string[] CarbonLuau_RegisterAddonArchive(Plugin Provider, byte[] Archive)
        {
            string[] Error = ValidateProvider(Provider); if (Error != null) return Error;
            string[] Result = Addons.RegisterArchive(Provider, Archive); QueueAddonWork(); return Result;
        }
        private string[] CarbonLuau_RegisterAddonSource(Plugin Provider, string Id, string Version, byte[] Source)
        {
            string[] Error = ValidateProvider(Provider); if (Error != null) return Error;
            string[] Result = Addons.RegisterSource(Provider, Id, Version, Source); QueueAddonWork(); return Result;
        }
        private string[] CarbonLuau_GetAddonStatus(Plugin Provider, string Token)
        {
            string[] Error = ValidateProvider(Provider); return Error ?? Addons.Status(Provider, Token);
        }
        private string[] CarbonLuau_ReplaceAddonArchive(Plugin Provider, string Token, byte[] Archive)
        {
            string[] Error = ValidateProvider(Provider); if (Error != null) return Error;
            string[] Result = Addons.ReplaceArchive(Provider, Token, Archive); QueueAddonWork(); return Result;
        }
        private string[] CarbonLuau_ReplaceAddonSource(Plugin Provider, string Token, string Version, byte[] Source)
        {
            string[] Error = ValidateProvider(Provider); if (Error != null) return Error;
            string[] Result = Addons.ReplaceSource(Provider, Token, Version, Source); QueueAddonWork(); return Result;
        }
        private string[] CarbonLuau_UnregisterAddon(Plugin Provider, string Token)
        {
            string[] Error = ValidateProvider(Provider); if (Error != null) return Error;
            string[] Result = Addons.Unregister(Provider, Token); RegisterActivePermissions(); RequestDrain(); return Result;
        }
        private string[] ValidateProvider(Plugin Provider)
        {
            if (Stopping || Addons == null || Host == null) return ProviderError("CarbonLuau addon host is unavailable");
            if (Provider == null || Object.ReferenceEquals(Provider, this) || !Provider.IsLoaded) return ProviderError("loaded provider plugin object is required");
            return null;
        }
        private static string[] ProviderError(string Message)
        { return new[] {"ERROR", "", "", "", "", AddonPolicy.Diagnostic(Message), "", "", ""}; }

        private void OnPluginUnloaded(Plugin Provider)
        {
            if (Provider == null || Object.ReferenceEquals(Provider, this) || Addons == null) return;
            try {
                int Removed = Addons.UnloadProvider(Provider);
                if (Removed != 0) Puts("[CarbonLuau:Addons] Retired " + Removed + " registration(s) for unloaded provider " + Provider.Name + ".");
                RegisterActivePermissions(); RequestDrain();
            } catch (Exception Error) { PrintError("[CarbonLuau:Addons] Provider unload teardown failed: " + AddonPolicy.Diagnostic(Error.Message)); }
        }
        private void QueueAddonWork()
        {
            if (Stopping || !Initialized || Addons == null || Host == null || !Host.Ready || AddonFrameScheduled || !Addons.HasPending) return;
            AddonFrameScheduled = true;
            NextFrame(() => {
                AddonFrameScheduled = false;
                if (Stopping || Addons == null) return;
                try {
                    Addons.ProcessOne(); RegisterActivePermissions(); RequestDrain();
                } catch (Exception Error) { PrintError("[CarbonLuau:Addons] Activation processing failed: " + AddonPolicy.Diagnostic(Error.Message)); }
                if (Addons != null && Addons.HasPending) QueueAddonWork();
            });
        }
    }
}
