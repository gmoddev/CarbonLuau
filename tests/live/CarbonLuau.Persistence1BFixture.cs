// EXCLUDED from production packages. Install beside the unmodified production
// CarbonLuau.cszip only in Test-Persistence1BLiveWindows.ps1's isolated data tree.
using System;
using System.Text;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    [Info("CarbonLuauPersistence1BFixture", "gmoddev", "1.0.0")]
    [Description("Bounded representative Persistence-1B live Carbon qualification")]
    public sealed class CarbonLuauPersistence1BFixture : CarbonPlugin
    {
        [PluginReference] private Plugin CarbonLuau;
        private string Token, Domain;
        private int Epoch;
        private sealed class PairRegistration { internal string Token; }
        private PairRegistration PairA, PairB;

        [ConsoleCommand("clpersistence1b.abseed"), AuthLevel(2)]
        private void SeedPair(ConsoleSystem.Arg Arg)
        { Run(Arg, () => RegisterPair(true)); }

        [ConsoleCommand("clpersistence1b.abrestartread"), AuthLevel(2)]
        private void ReadPairAfterRestart(ConsoleSystem.Arg Arg)
        { Run(Arg, () => RegisterPair(false)); }

        private void RegisterPair(bool SeedValue)
        {
            Check(PairA == null && PairB == null, "fresh pair registration required");
            int ExpectedEpoch = ++Epoch;
            PairA = RegisterPairMember("qualification.persistence1b-a", "AddonA", SeedValue,
                SeedValue ? "AddonASeed" : "AddonAReadServerRestart", ExpectedEpoch);
            PairB = RegisterPairMember("qualification.persistence1b-b", "AddonB", SeedValue,
                SeedValue ? "AddonBSeed" : "AddonBReadServerRestart", ExpectedEpoch);
        }

        private PairRegistration RegisterPairMember(string PackageId, string ValueName, bool SeedValue,
            string Marker, int ExpectedEpoch)
        {
            string[] Result = CarbonLuau.Call("CarbonLuau_RegisterAddonSource", this,
                PackageId, "1.0.0", Source(SeedValue, Marker, ValueName)) as string[];
            Check(Result != null && Result.Length == 9 && Result[0] == "OK", "pair registration accepted");
            var State = new PairRegistration { Token = Result[1] };
            PollPair(State, ExpectedEpoch, 0, Marker);
            return State;
        }

        private void PollPair(PairRegistration State, int ExpectedEpoch, int Attempt, string Marker)
        {
            if (ExpectedEpoch != Epoch) return;
            try {
                Check(CarbonLuau != null && CarbonLuau.IsLoaded, "pair host available");
                string[] Status = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, State.Token) as string[];
                Check(Status != null && Status.Length == 9 && Status[0] == "OK", "pair status shape");
                if (Status[2] == "Active") {
                    Check(!string.IsNullOrEmpty(Status[7]), "pair active domain");
                    Puts("[CarbonLuau:Persistence1BLive] PASS Active " + Marker);
                    return;
                }
                Check(Status[2] != "Failed" && Attempt < 120, "bounded pair activation");
                timer.Once(0.25f, () => PollPair(State, ExpectedEpoch, Attempt + 1, Marker));
            } catch (Exception) { Fail(); }
        }

        [ConsoleCommand("clpersistence1b.seed"), AuthLevel(2)]
        private void Seed(ConsoleSystem.Arg Arg)
        {
            Run(Arg, () => {
                Check(Token == null, "seed once");
                Register(true, "AddonSeed");
            });
        }

        [ConsoleCommand("clpersistence1b.replace"), AuthLevel(2)]
        private void Replace(ConsoleSystem.Arg Arg)
        {
            Run(Arg, () => {
                Check(Token != null && Domain != null, "seed before replace");
                string Previous = Domain;
                string[] Result = CarbonLuau.Call("CarbonLuau_ReplaceAddonSource", this, Token,
                    "2.0.0", Source(false, "AddonReadReplacement")) as string[];
                Check(Result != null && Result.Length == 9 && Result[0] == "OK", "replace accepted");
                Poll(++Epoch, 0, Previous, "Replacement");
            });
        }

        [ConsoleCommand("clpersistence1b.reregister"), AuthLevel(2)]
        private void Reregister(ConsoleSystem.Arg Arg)
        {
            Run(Arg, () => {
                Check(Token != null, "previous host token exists");
                string[] Stale = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, Token) as string[];
                Check(Stale != null && Stale[0] == "ERROR", "old host token rejected");
                Puts("[CarbonLuau:Persistence1BLive] PASS StaleAddonToken");
                Register(false, "AddonReadHostReload");
            });
        }

        private void Register(bool SeedValue, string Marker)
        {
            string[] Result = CarbonLuau.Call("CarbonLuau_RegisterAddonSource", this,
                "qualification.persistence1b", "1.0.0", Source(SeedValue, Marker)) as string[];
            Check(Result != null && Result.Length == 9 && Result[0] == "OK", "registration accepted");
            Token = Result[1];
            Poll(++Epoch, 0, null, Marker);
        }

        private void Poll(int ExpectedEpoch, int Attempt, string Previous, string Marker)
        {
            if (ExpectedEpoch != Epoch) return;
            try {
                Check(CarbonLuau != null && CarbonLuau.IsLoaded, "host available during activation");
                string[] Status = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, Token) as string[];
                Check(Status != null && Status.Length == 9 && Status[0] == "OK", "status shape");
                if (Status[2] == "Active" && Status[7] != Previous) {
                    Check(!string.IsNullOrEmpty(Status[7]), "active domain");
                    Domain = Status[7];
                    Puts("[CarbonLuau:Persistence1BLive] PASS Active " + Marker);
                    return;
                }
                Check(Status[2] != "Failed" && Attempt < 120, "bounded activation");
                timer.Once(0.25f, () => Poll(ExpectedEpoch, Attempt + 1, Previous, Marker));
            } catch (Exception) { Fail(); }
        }

        private void Run(ConsoleSystem.Arg Arg, Action Action)
        {
            try {
                Check(CarbonLuau != null && CarbonLuau.IsLoaded, "host loaded");
                Action();
                Arg.ReplyWith("[CarbonLuau:Persistence1BLive] accepted");
            } catch (Exception) {
                Fail();
                Arg.ReplyWith("[CarbonLuau:Persistence1BLive] FAIL");
            }
        }

        private static byte[] Source(bool SeedValue, string Marker, string ValueName = "Addon")
        {
            // Same store/key as Root, deliberately different value. No caller-
            // chosen namespace and no privileged persistence/backend access.
            string Read = @"
S:GetAsync('Retained', function(Value, ErrorCode)
    assert(ErrorCode == nil and Value == '__VALUE__', '[CarbonLuau:Persistence1BLive] FAIL addon read')
    print('[CarbonLuau:Persistence1BLive] PASS " + Marker + @"')
end)";
            string Body = SeedValue ? @"
S:GetAsync('Retained', function(Value, ErrorCode)
    assert(ErrorCode == nil and Value == nil, '[CarbonLuau:Persistence1BLive] FAIL namespace isolation')
    S:SetAsync('Retained', '__VALUE__', function(Saved, SaveError)
        assert(Saved == true and SaveError == nil, '[CarbonLuau:Persistence1BLive] FAIL addon set')
        " + Read + @"
    end)
end)" : Read;
            return new UTF8Encoding(false, true).GetBytes(
                "local S=game:GetService('DataStoreService'):GetDataStore('Persistence1BLive')\n" +
                "task.defer(function()\n" + Body.Replace("__VALUE__", ValueName) + "\nend)\nreturn true");
        }

        private void OnPluginLoaded(Plugin Plugin)
        { if (Plugin != null && Plugin.Name == "CarbonLuau") CarbonLuau = Plugin; }

        private void OnPluginUnloaded(Plugin Plugin)
        {
            if (Plugin == null || Plugin.Name != "CarbonLuau") return;
            ++Epoch; CarbonLuau = null; Domain = null;
            Puts("[CarbonLuau:Persistence1BLive] PASS ObservedHostUnload");
        }

        private void Unload() { ++Epoch; }
        private static void Check(bool Condition, string Message)
        { if (!Condition) throw new InvalidOperationException(Message); }
        private void Fail()
        { PrintError("[CarbonLuau:Persistence1BLive] FAIL provider lifecycle; inspect bounded host status"); }
    }
}
