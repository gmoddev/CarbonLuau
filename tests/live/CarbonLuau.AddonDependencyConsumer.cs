// Reference: System.IO.Compression
// EXCLUDED from production packages. Foundation C live dependency fixture only.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    [Info("CarbonLuauAddonDependencyConsumer", "gmoddev", "1.0.0")]
    [Description("Foundation C live required/optional consumer fixture")]
    public sealed class CarbonLuauAddonDependencyConsumer : CarbonPlugin
    {
        [PluginReference] private Plugin CarbonLuau;
        private bool Started, RequiredWasBlocked;
        private int ReferenceAttempts;
        private string RequiredToken, OptionalToken, RequiredDomain, OptionalDomain;

        private void Loaded() { NextFrame(StartFixture); }
        private void OnServerInitialized() { NextFrame(StartFixture); }

        private void StartFixture()
        {
            if (Started) return;
            if (CarbonLuau == null || !CarbonLuau.IsLoaded) {
                if (++ReferenceAttempts >= 600) { PrintError("[CarbonLuau:AddonConsumerLive] FAIL CarbonLuau reference unavailable"); return; }
                timer.Once(0.25f, StartFixture); return;
            }
            Started = true;
            try {
                RequiredToken = Register("qualification.consumer", "required", "clfoundationcrequired");
                OptionalToken = Register("qualification.optional", "optional", "clfoundationcoptional");
                Poll(0);
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonConsumerLive] FAIL " + Error.Message); }
        }

        private string Register(string Id, string Kind, string Command)
        {
            string[] Result = CarbonLuau.Call("CarbonLuau_RegisterAddonArchive", this, Archive(Id, Kind, Command)) as string[];
            Check(Result != null && Result.Length == 9 && Result[0] == "OK", Kind + " consumer registration");
            return Result[1];
        }

        private void Poll(int Attempt)
        {
            try {
                string[] Required = Status(RequiredToken), Optional = Status(OptionalToken);
                if (Required[2] == "Failed" || Optional[2] == "Failed" || Attempt >= 2400)
                    throw new InvalidOperationException("consumer state required=" + Required[2] + " optional=" + Optional[2]);
                if (Optional[2] == "Active") {
                    if (OptionalDomain == null) {
                        OptionalDomain = Optional[7];
                        Puts("[CarbonLuau:AddonConsumerLive] PASS OptionalActive; domain=" + OptionalDomain);
                    } else Check(Optional[7] == OptionalDomain, "optional consumer was silently rebound");
                }
                if (Required[2] == "Blocked") {
                    if (!RequiredWasBlocked && RequiredDomain != null) Puts("[CarbonLuau:AddonConsumerLive] PASS RequiredBlocked");
                    RequiredWasBlocked = true;
                } else if (Required[2] == "Active") {
                    if (RequiredDomain == null) {
                        RequiredDomain = Required[7];
                        Puts("[CarbonLuau:AddonConsumerLive] PASS RequiredActive; domain=" + RequiredDomain);
                    } else if (Required[7] != RequiredDomain) {
                        RequiredDomain = Required[7];
                        Puts("[CarbonLuau:AddonConsumerLive] PASS " + (RequiredWasBlocked ? "RequiredRestored" : "RequiredReinitialized") +
                            "; domain=" + RequiredDomain + "; optional=" + OptionalDomain);
                        RequiredWasBlocked = false;
                    }
                }
                timer.Once(0.25f, () => Poll(Attempt + 1));
            } catch (Exception Error) { PrintError("[CarbonLuau:AddonConsumerLive] FAIL " + Error.Message); }
        }

        private string[] Status(string Token)
        {
            string[] Result = CarbonLuau.Call("CarbonLuau_GetAddonStatus", this, Token) as string[];
            Check(Result != null && Result.Length == 9 && Result[0] == "OK", "consumer status"); return Result;
        }

        [ConsoleCommand("clfoundationc.consumerstatus"), AuthLevel(2)]
        private void StatusCommand(ConsoleSystem.Arg Arg)
        {
            try {
                string[] Required = Status(RequiredToken), Optional = Status(OptionalToken);
                Arg.ReplyWith("required=" + Required[2] + ";requiredDomain=" + Required[7] +
                    ";optional=" + Optional[2] + ";optionalDomain=" + Optional[7]);
            } catch (Exception Error) {
                PrintError("[CarbonLuau:AddonConsumerLive] FAIL status command " + Error.Message);
                Arg.ReplyWith("consumer status failed");
            }
        }

        private static byte[] Archive(string Id, string Kind, string Command)
        {
            string Manifest = "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":" +
                (Kind == "required" ? "[\"qualification.dependency\"]" : "[]") + ",\"optional\":" +
                (Kind == "optional" ? "[\"qualification.dependency\"]" : "[]") + "}}";
            string Source = "game:GetService('Commands'):Register('" + Command + "',{},function() end); return true";
            using (var Output = new MemoryStream()) {
                using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                    Write(Zip, "addon.json", Manifest); Write(Zip, "init.luau", Source);
                }
                return Output.ToArray();
            }
        }

        private static void Write(ZipArchive Zip, string Name, string Text)
        {
            ZipArchiveEntry Entry = Zip.CreateEntry(Name, CompressionLevel.Optimal);
            byte[] Bytes = new UTF8Encoding(false, true).GetBytes(Text);
            using (Stream Stream = Entry.Open()) Stream.Write(Bytes, 0, Bytes.Length);
        }

        private static void Check(bool Condition, string Message) { if (!Condition) throw new InvalidOperationException(Message); }
    }
}
