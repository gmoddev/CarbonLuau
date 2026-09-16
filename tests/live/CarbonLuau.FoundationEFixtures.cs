// Reference: System.IO.Compression
// EXCLUDED from production packages. Foundation E scale/lifecycle fixture only.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private readonly List<object> FoundationEProviders = new List<object>();

        [ConsoleCommand("carbonluau.foundationestates"), AuthLevel(2)]
        private void FoundationEStates(ConsoleSystem.Arg Arg)
        {
            try {
                CheckFoundationE(Addons != null && Host != null && Host.Ready, "runtime unavailable");
                int Baseline = Addons.Count; object Provider = new object();
                string[] Active = Addons.RegisterSource(Provider, "liveactive", "1.0.0", FoundationEBytes("return true"));
                CheckFoundationE(Active[0] == "OK" && Addons.ProcessOne(), "Active setup");
                string[] Failed = Addons.RegisterSource(Provider, "livefailed", "1.0.0", FoundationEBytes("error('expected')"));
                CheckFoundationE(Failed[0] == "OK" && Addons.ProcessOne(), "Failed setup");
                string[] Blocked = Addons.RegisterArchive(Provider, FoundationEArchive("liveblocked", "1.0.0", "missingdep", "return true"));
                string[] Registered = Addons.RegisterSource(Provider, "liveregistered", "1.0.0", FoundationEBytes("return true"));
                CheckFoundationE(Blocked[0] == "OK" && Addons.Status(Provider, Blocked[1])[2] == "Blocked", "Blocked setup");
                CheckFoundationE(Registered[0] == "OK" && Addons.Status(Provider, Registered[1])[2] == "Registered", "Registered setup");
                CheckFoundationE(Addons.Status(Provider, Active[1])[2] == "Active" && Addons.Status(Provider, Failed[1])[2] == "Failed",
                    "reachable states established");
                CheckFoundationE(Addons.UnloadProvider(Provider) == 4 && Addons.Count == Baseline, "provider unload from every reachable state");
                CheckFoundationE(Addons.Status(Provider, Active[1])[0] == "ERROR", "unloaded token is stale");

                object ReplacementProvider = new object();
                string[] Replacement = Addons.RegisterSource(ReplacementProvider, "livereplace", "1.0.0", FoundationEBytes("return 1"));
                CheckFoundationE(Replacement[0] == "OK" && Addons.ProcessOne(), "replacement setup");
                string FirstDomain = Addons.Status(ReplacementProvider, Replacement[1])[7];
                CheckFoundationE(Addons.ReplaceSource(ReplacementProvider, Replacement[1], "2.0.0", FoundationEBytes("return 2"))[0] == "OK" &&
                    Addons.ProcessOne(), "same-provider replacement");
                CheckFoundationE(Addons.Status(ReplacementProvider, Replacement[1])[7] != FirstDomain, "replacement lifetime changed");
                CheckFoundationE(Addons.Unregister(ReplacementProvider, Replacement[1])[0] == "OK" &&
                    Addons.Status(ReplacementProvider, Replacement[1])[0] == "ERROR" &&
                    Addons.Unregister(ReplacementProvider, Replacement[1])[0] == "OK", "unregister and stale/idempotent token behavior");
                string[] Reregistered = Addons.RegisterSource(ReplacementProvider, "livereplace", "3.0.0", FoundationEBytes("return 3"));
                CheckFoundationE(Reregistered[0] == "OK" && Addons.ProcessOne() &&
                    Addons.Status(ReplacementProvider, Reregistered[1])[2] == "Active", "explicit provider re-registration");
                Addons.UnloadProvider(ReplacementProvider);
                Puts("[CarbonLuau:FoundationELive] PASS reachable states, unload, stale token, replacement, unregister, re-registration");
                Arg.ReplyWith("CarbonLuau Foundation E lifecycle states PASS");
            } catch (Exception Error) { FoundationEFailure(Arg, Error); }
        }

        [ConsoleCommand("carbonluau.foundationescale"), AuthLevel(2)]
        private void FoundationEScale(ConsoleSystem.Arg Arg)
        {
            try {
                CheckFoundationE(Addons != null && Host != null && Host.Ready, "scale fixture requires a healthy addon registry");
                int BaselineRegistrations = Addons.Count; ulong BaselineDomains = Host.DomainCount;
                CheckFoundationE(BaselineRegistrations <= AddonPolicy.MaxRegistrations - 100, "scale fixture requires capacity for 100 addons");
                long ManagedBefore = GC.GetTotalMemory(true); Process Current = Process.GetCurrentProcess(); Current.Refresh();
                long RssBefore = Current.WorkingSet64; var Activation = Stopwatch.StartNew();
                for (int ProviderIndex = 0; ProviderIndex < 4; ++ProviderIndex) FoundationEProviders.Add(new object());
                for (int Index = 0; Index < 100; ++Index) {
                    string Id = "livescale" + Index.ToString("D3", CultureInfo.InvariantCulture);
                    string Source = "local State={Payload=string.rep('x',1024)}; task.delay(86400,function() return State end); return true";
                    string[] Result = Addons.RegisterSource(FoundationEProviders[Index / AddonPolicy.MaxRegistrationsPerProvider],
                        Id, "1.0.0", FoundationEBytes(Source));
                    CheckFoundationE(Result[0] == "OK" && Addons.ProcessOne() &&
                        Addons.Status(FoundationEProviders[Index / AddonPolicy.MaxRegistrationsPerProvider], Result[1])[2] == "Active",
                        "scale activation " + Index);
                }
                Activation.Stop(); RequestDrain(); Current.Refresh();
                CheckFoundationE(Host.DomainCount == BaselineDomains + 100 && Addons.Count == BaselineRegistrations + 100,
                    "100 additional addon domains in the shared VM");
                CheckFoundationE(Host.VmMemoryBytes < Host.VmMemoryLimitBytes && Host.VmMemoryLimitBytes == 64UL * 1024 * 1024,
                    "64 MiB default headroom");
                CheckFoundationE(Host.HasWork && !Host.HasReadyWork && DrainWakeDueNs != 0 && !DrainScheduled,
                    "future-only addon work uses one delayed wake instead of persistent frame polling");
                Puts("[CarbonLuau:FoundationELive] PASS scale100 vm/snapshot/managed/rss=" + Host.VmMemoryBytes + "/" +
                    Addons.SnapshotBytes + "/" + GC.GetTotalMemory(true) + "/" + Current.WorkingSet64 +
                    "; deltas=" + (GC.GetTotalMemory(false) - ManagedBefore) + "/" + (Current.WorkingSet64 - RssBefore) +
                    "; activationMs=" + Activation.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                    "; delayedWakeNs=" + DrainWakeDueNs);
                Arg.ReplyWith("CarbonLuau Foundation E scale100 PASS");
            } catch (Exception Error) { FoundationEFailure(Arg, Error); }
        }

        private static byte[] FoundationEArchive(string Id, string Version, string Required, string Source)
        {
            string Manifest = "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"" + Version +
                "\",\"dependencies\":{\"required\":[\"" + Required + "\"],\"optional\":[]}}";
            using (var Output = new MemoryStream()) {
                using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                    FoundationEWrite(Zip, "addon.json", Manifest); FoundationEWrite(Zip, "init.luau", Source);
                }
                return Output.ToArray();
            }
        }
        private static void FoundationEWrite(ZipArchive Zip, string Name, string Text)
        {
            ZipArchiveEntry Entry = Zip.CreateEntry(Name, CompressionLevel.Optimal); byte[] Value = FoundationEBytes(Text);
            using (Stream Stream = Entry.Open()) Stream.Write(Value, 0, Value.Length);
        }
        private static byte[] FoundationEBytes(string Text) { return new UTF8Encoding(false, true).GetBytes(Text); }
        private static void CheckFoundationE(bool Condition, string Message)
        { if (!Condition) throw new InvalidOperationException(Message); }
        private void FoundationEFailure(ConsoleSystem.Arg Arg, Exception Error)
        {
            PrintError("[CarbonLuau:FoundationELive] FAIL " + Error); Arg.ReplyWith("CarbonLuau Foundation E FAIL");
        }
    }
}
