// Reference: System.IO.Compression
// EXCLUDED from production packages. Foundation C/D live dependency fixture only.
using System;
using System.IO;
using System.IO.Compression;
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
        private string Token, Domain, StaleToken;

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
                if (StaleToken != null) {
                    string[] Stale = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, StaleToken) as string[];
                    Check(Stale != null && Stale[0] == "ERROR", "old dependency token must be stale");
                    Puts("[CarbonLuau:AddonDependencyLive] PASS stale token rejected after CarbonLuau reload"); StaleToken = null;
                }
                string[] Result = CarbonLuau.Call("CarbonLuau_RegisterAddonArchive", this, Archive("1.0.0", "1")) as string[];
                Check(Result != null && Result.Length == 9 && Result[0] == "OK", "dependency registration");
                Token = Result[1]; PollActive(false, 0);
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonDependencyLive] FAIL " + Error.Message); }
        }

        [ConsoleCommand("clfoundationc.replace"), AuthLevel(2)]
        private void ReplaceCommand(ConsoleSystem.Arg Arg)
        {
            try {
                Check(Token != null && Domain != null, "dependency is not active");
                string[] Result = CarbonLuau.Call("CarbonLuau_ReplaceAddonArchive", this, Token, Archive("2.0.0", "2")) as string[];
                Check(Result != null && Result.Length == 9 && Result[0] == "OK", "dependency replacement");
                PollActive(true, 0); Arg.ReplyWith("CarbonLuau Foundation C dependency replacement queued");
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonDependencyLive] FAIL " + Error.Message); }
        }

        private void PollActive(bool Replacement, int Attempt)
        {
            if (!Started || CarbonLuau == null || !CarbonLuau.IsLoaded) return;
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

        private static byte[] Archive(string Version, string Generation)
        {
            string Manifest = "{\"schema\":1,\"id\":\"qualification.dependency\",\"version\":\"" + Version + "\",\"main\":\"api\"}";
            using (var Output = new MemoryStream()) {
                using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                    Write(Zip, "addon.json", Manifest); Write(Zip, "init.luau", "assert(addon.Id=='qualification.dependency'); return true");
                    Write(Zip, "api.luau", "return {Generation='" + Generation + "'}");
                }
                return Output.ToArray();
            }
        }
        private static void Write(ZipArchive Zip, string Name, string Text)
        {
            ZipArchiveEntry Entry = Zip.CreateEntry(Name, CompressionLevel.Optimal); byte[] Bytes = new UTF8Encoding(false, true).GetBytes(Text);
            using (Stream Stream = Entry.Open()) Stream.Write(Bytes, 0, Bytes.Length);
        }
        private static void Check(bool Condition, string Message) { if (!Condition) throw new InvalidOperationException(Message); }
        private void OnPluginUnloaded(Plugin Plugin)
        {
            if (Plugin == null || Plugin.Name != "CarbonLuau") return;
            StaleToken = Token; Started = false; CarbonLuau = null;
            Puts("[CarbonLuau:AddonDependencyLive] observed CarbonLuau unload");
        }
        private void OnPluginLoaded(Plugin Plugin)
        {
            if (Plugin == null || Plugin.Name != "CarbonLuau") return;
            CarbonLuau = Plugin; ReferenceAttempts = 0; NextFrame(StartFixture);
        }
        private void Unload() { Puts("[CarbonLuau:AddonDependencyLive] provider Unload reached"); }
    }
}
