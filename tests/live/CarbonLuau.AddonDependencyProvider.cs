// EXCLUDED from production packages. Foundation C live dependency fixture only.
using System;
using System.Text;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    [Info("CarbonLuauAddonDependencyProvider", "gmoddev", "1.0.0")]
    [Description("Foundation C live dependency provider fixture")]
    public sealed class CarbonLuauAddonDependencyProvider : CarbonPlugin
    {
        [PluginReference] private Plugin CarbonLuau;
        private bool Started;
        private int ReferenceAttempts;
        private string Token, Domain;

        private void Loaded() { NextFrame(StartFixture); }
        private void OnServerInitialized() { NextFrame(StartFixture); }

        private void StartFixture()
        {
            if (Started) return;
            if (CarbonLuau == null || !CarbonLuau.IsLoaded) {
                if (++ReferenceAttempts >= 600) { PrintError("[CarbonLuau:AddonDependencyLive] FAIL CarbonLuau reference unavailable"); return; }
                timer.Once(0.25f, StartFixture); return;
            }
            Started = true;
            try {
                string[] Result = CarbonLuau.Call("CarbonLuau_RegisterAddonSource", this,
                    "qualification.dependency", "1.0.0", Bytes("return true")) as string[];
                Check(Result != null && Result.Length == 9 && Result[0] == "OK", "dependency registration");
                Token = Result[1]; PollActive(false, 0);
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonDependencyLive] FAIL " + Error.Message); }
        }

        [ConsoleCommand("clfoundationc.replace"), AuthLevel(2)]
        private void ReplaceCommand(ConsoleSystem.Arg Arg)
        {
            try {
                Check(Token != null && Domain != null, "dependency is not active");
                string[] Result = CarbonLuau.Call("CarbonLuau_ReplaceAddonSource", this, Token,
                    "2.0.0", Bytes("return true")) as string[];
                Check(Result != null && Result.Length == 9 && Result[0] == "OK", "dependency replacement");
                PollActive(true, 0); Arg.ReplyWith("CarbonLuau Foundation C dependency replacement queued");
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonDependencyLive] FAIL " + Error.Message); }
        }

        private void PollActive(bool Replacement, int Attempt)
        {
            try {
                string[] Status = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, Token) as string[];
                Check(Status != null && Status.Length == 9 && Status[0] == "OK", "dependency status");
                if (Status[2] == "Active" && (!Replacement || Status[7] != Domain)) {
                    Domain = Status[7];
                    Puts("[CarbonLuau:AddonDependencyLive] PASS " + (Replacement ? "Replaced" : "Active") + "; domain=" + Domain);
                    return;
                }
                if (Status[2] == "Failed" || Attempt >= 600) throw new InvalidOperationException("dependency activation ended in " + Status[2] + ": " + Status[5]);
                timer.Once(0.25f, () => PollActive(Replacement, Attempt + 1));
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonDependencyLive] FAIL " + Error.Message); }
        }

        private static byte[] Bytes(string Source) { return new UTF8Encoding(false, true).GetBytes(Source); }
        private static void Check(bool Condition, string Message) { if (!Condition) throw new InvalidOperationException(Message); }
        private void Unload() { Puts("[CarbonLuau:AddonDependencyLive] provider Unload reached"); }
    }
}
