// EXCLUDED from production packages. Live Carbon provider lifecycle fixture only.
using System;
using System.Text;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    [Info("CarbonLuauAddonProvider", "gmoddev", "1.0.0")]
    [Description("Addon provider lifecycle regression fixture")]
    public sealed class CarbonLuauAddonProvider : CarbonPlugin
    {
        [PluginReference] private Plugin CarbonLuau;
        private bool Started;
        private int ReferenceAttempts;
        private string Token;

        private void Loaded() { NextFrame(StartFixture); }
        private void OnServerInitialized() { NextFrame(StartFixture); }

        private void StartFixture()
        {
            if (Started) return;
            if (CarbonLuau == null || !CarbonLuau.IsLoaded) {
                if (++ReferenceAttempts >= 600) { PrintError("[CarbonLuau:AddonLive] FAIL CarbonLuau reference unavailable"); return; }
                timer.Once(0.25f, StartFixture); return;
            }
            Started = true;
            try {
                string[] Protocol = CarbonLuau.Call("CarbonLuau_AddonProtocol") as string[];
                Check(Protocol != null && Protocol.Length == 4 && Protocol[0] == "OK" &&
                    Protocol[1] == "CarbonLuau.Addons" && Protocol[2] == "1.2" && Protocol[3].Contains("dependencies") &&
                    Protocol[3].Contains("public-modules"), "protocol query");
                byte[] Source = new UTF8Encoding(false, true).GetBytes(
                    "game:GetService('Commands'):Register('clfoundationb',{},function() end); " +
                    "task.delay(86400,function() error('provider teardown failed') end); return true");
                string[] Result = CarbonLuau.Call("CarbonLuau_RegisterAddonSource", this,
                    "qualification.provider", "1.0.0", Source) as string[];
                Check(Result != null && Result.Length == 9 && Result[0] == "OK", "source registration");
                Token = Result[1]; PollStatus(0);
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonLive] FAIL " + Error.Message); }
        }

        private void PollStatus(int Attempt)
        {
            try {
                string[] Status = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, Token) as string[];
                Check(Status != null && Status.Length == 9 && Status[0] == "OK", "status response");
                if (Status[2] == "Active") {
                    Puts("[CarbonLuau:AddonLive] PASS Active; token=" + Token + "; domain=" + Status[7]);
                    return;
                }
                if (Status[2] == "Failed" || Attempt >= 600) throw new InvalidOperationException("activation ended in " + Status[2] + ": " + Status[5]);
                timer.Once(0.25f, () => PollStatus(Attempt + 1));
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonLive] FAIL " + Error.Message); }
        }

        private static void Check(bool Condition, string Message)
        { if (!Condition) throw new InvalidOperationException(Message); }

        private void Unload()
        { Puts("[CarbonLuau:AddonLive] provider Unload reached; CarbonLuau must retire token " + (Token ?? "none")); }
    }
}
