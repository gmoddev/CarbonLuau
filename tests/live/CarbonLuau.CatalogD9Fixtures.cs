// EXCLUDED from production packages. Controlled CATALOG-D9 runtime qualification.
// Reference: System.IO.Compression
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string CatalogD9Assertions =
            "assert(game.ApiVersion=='0.7.5-experimental'); assert(type(Item)=='table' and type(Prefab)=='table'); " +
            "assert(Item.Scrap=='scrap' and Item.MetalFragments=='metal.fragments' and Item.Wood=='wood'); " +
            "assert(type(Item.Scrap)=='string' and tostring(Item.Scrap)=='scrap'); " +
            "assert(Prefab.Loot.Barrel1=='assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab'); " +
            "assert(Prefab.Loot.Barrel2=='assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab'); " +
            "local N=0; for K,V in Item do assert(type(K)=='string' and type(V)=='string'); N+=1 end; assert(N==221); " +
            "local P=0; for K in Prefab do assert(K=='Loot'); P+=1 end; assert(P==1); local L=0; for K,V in Prefab.Loot do assert(type(V)=='string' and (K=='Barrel1' or K=='Barrel2')); L+=1 end; assert(L==2); " +
            "assert(Item.scrap==nil and Item.Revolver==nil and Item.Get==nil and Prefab.Entity==nil and Prefab.Barrel1==nil); " +
            "for _,F in {function() Item.Scrap='bad' end,function() rawset(Item,'Scrap','bad') end," +
            "function() Prefab.Loot.Barrel1='bad' end,function() rawset(Prefab.Loot,'Barrel2','bad') end," +
            "function() Prefab.Loot={} end} do assert(not pcall(F)) end; " +
            "assert(game:GetService('Items'):Exists(Item.Scrap)==game:GetService('Items'):Exists('scrap')); " +
            "assert(type(ImageSource.Item)=='function' and game:GetService('Items')~=Item); ";

        private static void CatalogD9Check(bool Value, string Message)
        { if (!Value) throw new InvalidOperationException("CATALOG-D9 " + Message); }

        private static byte[] CatalogD9Archive(string Id)
        {
            using (var Output = new MemoryStream()) {
                using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                    string Source = CatalogD9Assertions +
                        "local M=require('catalog'); assert(M.Item==Item and M.Prefab==Prefab); local C=Item; " +
                        "task.defer(function() assert(C.Scrap=='scrap' and M.Prefab.Loot.Barrel2~=nil); print('CatalogD9Callback '..addon.Id) end); " +
                        "Item={Scrap='shadow'}; assert(Item.Scrap=='shadow' and M.Item.Scrap=='scrap'); return M";
                    var Rows = new Dictionary<string, string> {
                        {"addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"}"},
                        {"init.luau", Source}, {"catalog.luau", "return {Item=Item,Prefab=Prefab}"}
                    };
                    foreach (var Row in Rows) {
                        using (Stream Stream = Zip.CreateEntry(Row.Key).Open()) {
                            byte[] Bytes = Encoding.UTF8.GetBytes(Row.Value); Stream.Write(Bytes, 0, Bytes.Length);
                        }
                    }
                }
                return Output.ToArray();
            }
        }

        [ConsoleCommand("carbonluau.catalogd9"), AuthLevel(2)]
        private void CatalogD9Qualification(ConsoleSystem.Arg Arg)
        {
            var Providers = new object[] {new object(), new object(), new object(), new object()};
            var Tokens = new List<string>();
            try {
                CatalogD9Check(Host != null && Host.Ready && Addons != null && Addons.Count == 0,
                    "requires isolated healthy root without existing addons");
                var Timer = Stopwatch.StartNew();
                int InitialDomains = (int)Host.DomainCount;
                ulong InitialBytes = Host.VmMemoryBytes;
                long Generation = Host.VmGenerationId;
                CatalogD9Check(Host.Execute("catalog.root", CatalogD9Assertions).Status == RuntimeStatus.OK, "root visibility/immutability");
                for (int Index = 0; Index < 100; ++Index) {
                    string Id = "catalogd9" + Index;
                    string[] Registered = Addons.RegisterArchive(Providers[Index / 32], CatalogD9Archive(Id));
                    CatalogD9Check(Registered[0] == "OK" && Addons.ProcessOne(), "addon registration " + Id);
                    Tokens.Add(Registered[1]);
                    CatalogD9Check(Addons.Status(Providers[Index / 32], Registered[1])[2] == "Active", "addon active " + Id);
                    if (Index == 0 || Index == 9 || Index == 49 || Index == 99)
                        Puts("[CarbonLuau:CatalogD9] Addons=" + (Index + 1) + " VmBytes=" + Host.VmMemoryBytes + " Limit=" + Host.VmMemoryLimitBytes);
                }
                CatalogD9Check(Host.DomainCount == (ulong)(InitialDomains + 100) && Host.VmMemoryLimitBytes == 64UL * 1024 * 1024,
                    "unchanged shared VM/domain bounds");
                var Delivered = new HashSet<string>(StringComparer.Ordinal);
                for (int Pass = 0; Pass < 128 && Host.HasWork; ++Pass)
                    foreach (ExecutionResult Result in Host.Drain()) {
                        CatalogD9Check(Result.Status == RuntimeStatus.OK, "callback failure " + Result.Error);
                        foreach (string Line in (Result.Logs ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)) {
                            CatalogD9Check(Line.StartsWith("CatalogD9Callback ", StringComparison.Ordinal) && Delivered.Add(Line.Trim()), "exact callback delivery");
                        }
                    }
                CatalogD9Check(Delivered.Count == 100 && !Host.HasWork, "100-addon fairness convergence");
                string Before = Addons.Status(Providers[0], Tokens[0])[7];
                CatalogD9Check(Addons.ReplaceSource(Providers[0], Tokens[0], "2.0.0", Encoding.UTF8.GetBytes("rawset(Item,'Scrap','bad')"))[0] == "OK" && Addons.ProcessOne(), "failed replacement admitted");
                CatalogD9Check(Addons.Status(Providers[0], Tokens[0])[2] == "Active" && Addons.Status(Providers[0], Tokens[0])[7] == Before,
                    "failed candidate preserves published addon");
                CatalogD9Check(Addons.ReplaceSource(Providers[0], Tokens[0], "3.0.0", Encoding.UTF8.GetBytes(CatalogD9Assertions))[0] == "OK" && Addons.ProcessOne(), "healthy replacement");
                CatalogD9Check(Addons.Status(Providers[0], Tokens[0])[7] != Before && Host.VmGenerationId == Generation, "addon replacement retains shared VM");
                CatalogD9Check(Host.Reload().Status == RuntimeStatus.OK && Host.VmGenerationId == Generation,
                    "healthy root replacement retains catalogs/VM");
                CatalogD9Check(Host.Execute("catalog.root.after", CatalogD9Assertions).Status == RuntimeStatus.OK, "root after shadowing/replacement");
                ExecutionResult Fatal = Host.Execute("catalog.recovery", "while true do end");
                CatalogD9Check(Fatal.Status == RuntimeStatus.TIMEOUT && Host.Ready && Host.VmGenerationId != Generation, "fatal reconstruction");
                for (int Pass = 0; Pass < 128 && Addons.HasPending; ++Pass) CatalogD9Check(Addons.ProcessOne(), "recovery activation advances");
                for (int Index = 0; Index < 100; ++Index)
                    CatalogD9Check(Addons.Status(Providers[Index / 32], Tokens[Index])[2] == "Active", "recovered addon " + Index);
                CatalogD9Check(Host.Execute("catalog.recovered", CatalogD9Assertions).Status == RuntimeStatus.OK, "new generation complete catalog");
                foreach (object Provider in Providers) Addons.UnloadProvider(Provider);
                CatalogD9Check(Addons.Count == 0 && Host.DomainCount == (ulong)InitialDomains && Host.Ready,
                    "provider unload returns domain baseline");
                Puts("[CarbonLuau:CatalogD9] PASS root/addon/module visibility, 100-addon fairness, failed/healthy replacement, VM recovery and provider unload; initial/finalVmBytes=" +
                    InitialBytes + "/" + Host.VmMemoryBytes + "; milliseconds=" + Timer.Elapsed.TotalMilliseconds);
                Arg.ReplyWith("CarbonLuau CATALOG-D9 PASS");
            } catch (Exception Error) {
                PrintError("[CarbonLuau:CatalogD9] FAIL " + Error); Arg.ReplyWith("CarbonLuau CATALOG-D9 FAIL");
            } finally {
                if (Addons != null) foreach (object Provider in Providers) Addons.UnloadProvider(Provider);
            }
        }
    }
}
