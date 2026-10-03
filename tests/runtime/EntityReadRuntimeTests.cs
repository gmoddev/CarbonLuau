using System;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class EntityReadRuntimeTests
{
    [DllImport("libc", SetLastError = true)] private static extern int chmod(string Path, int Mode);
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class EntityHost : Carbon.Plugins.IEntityFacadeHost
    {
        internal bool Ready, Valid = true;
        internal int Lookups, Reads;
        public string[] Lookup(Runtime.FacadeSession Session, string Id)
        {
            Lookups++;
            if (!Ready) throw new Runtime.FacadeException("Entity world is unavailable");
            ulong Parsed;
            if (!ulong.TryParse(Id, NumberStyles.None, CultureInfo.InvariantCulture, out Parsed) ||
                Parsed == 0 || Id != Parsed.ToString(CultureInfo.InvariantCulture))
                throw new Runtime.FacadeException("invalid Entity ID");
            if (Id != "1" && Id != "2") return new string[0];
            var Witness = Session.CaptureEntityWitness();
            return new[] {"77", Id == "1" ? "41" : "42", Id,
                Witness.Token.ToString(CultureInfo.InvariantCulture)};
        }
        public string[] Read(Runtime.FacadeSession Session, string Token, string Publication, string Property)
        {
            Reads++;
            ulong WitnessToken = ulong.Parse(Publication, CultureInfo.InvariantCulture);
            if (!Valid || Session.FindEntityWitness(WitnessToken) == null)
                throw new Runtime.FacadeException("stale Entity reference");
            if (Property == "Id") return new[] {Token == "41" ? "1" : "2"};
            if (Property == "Prefab") return new[] {"assets/prefabs/fixture.prefab"};
            if (Property == "Position") return new[] {"1.25", "-2.5", "3.75"};
            throw new Runtime.FacadeException("unknown Entity field");
        }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception(Message); }

    private static byte[] AddonPackage(string Manifest, string Init, string Api = null)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Action<string, string> Add = (Name, Source) => {
                    using (var Writer = new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Source);
                };
                Add("addon.json", Manifest);
                Add("init.luau", Init);
                if (Api != null) Add("api.luau", Api);
            }
            return Output.ToArray();
        }
    }

    internal static void Run(string Library, string Compiler)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauEntity1B-" + Guid.NewGuid().ToString("N"));
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform,
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.Copy(Library, Target);
            string CompilerTarget = Path.Combine(Path.GetDirectoryName(Target),
                Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, CompilerTarget);
            if (Rid == "linux-x64") Check(chmod(CompilerTarget, 493) == 0,
                "compiler worker executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) {
                var Entities = new EntityHost();
                var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
                World.Entities = Entities;
                string Source = "return true";
                Func<Runtime.ScriptSnapshot> Snapshot = () => {
                    var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
                    Value.Modules.Add("state", "return {}");
                    Value.Modules.Add("cold", "local W=game:GetService('Workspace'); " +
                        "return {Entity=W:GetEntityById('1')}");
                    return Value;
                };
                using (var Host = new Runtime.ScriptHost(Native,
                    new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100}, Snapshot, World)) {
                    Source = "local W=game:GetService('Workspace'); assert(W==game:GetService('Workspace')); " +
                        "assert(not pcall(function() W:GetEntityById('1') end))";
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "unready lookup controlled");
                    Entities.Ready = true;
                    Source = "local S=require('state'); local W=game:GetService('Workspace'); " +
                        "S.W=W; S.E=W:GetEntityById('1'); " +
                        "assert(S.E and S.E.Id=='1' and S.E.Prefab=='assets/prefabs/fixture.prefab'); " +
                        "assert(S.E.Position==Vector3.new(1.25,-2.5,3.75)); " +
                        "assert(S.E==W:GetEntityById('1') and S.E~=W:GetEntityById('2')); " +
                        "assert(W:GetEntityById('18446744073709551615')==nil); " +
                        "assert(W:GetEntityById('3')==nil)";
                    var Loaded = Host.Reload();
                    Check(Loaded.Status == Runtime.RuntimeStatus.OK, "real Luau read-only bootstrap: " + Loaded.Error);
                    var Cold = Host.Execute("entity1b.cold",
                        "local E=require('cold').Entity; assert(E~=nil and E.Id=='1')");
                    Check(Cold.Status == Runtime.RuntimeStatus.OK,
                        "cold module lookup publishes a readable proxy: " + Cold.Error);
                    var Invalid = Host.Execute("entity1b.ids",
                        "local W=require('state').W; " +
                        "for _,Id in {'0','01','+1','-1',' 1','1 ','1.0','1e0','18446744073709551616','100000000000000000000','１','1\\0'} do " +
                        "assert(not pcall(function() W:GetEntityById(Id) end),Id) end; " +
                        "assert(not pcall(function() W:GetEntityById(1) end)); " +
                        "assert(not pcall(function() W:GetEntityById(nil) end)); " +
                        "assert(not pcall(function() require('state').E.Id='forged' end)); " +
                        "assert(not pcall(function() return require('state').E.Unknown end))");
                    Check(Invalid.Status == Runtime.RuntimeStatus.OK, "ID and read-only validation: " + Invalid.Error);
                    int BeforeReads = Entities.Reads;
                    var Equality = Host.Execute("entity1b.equality",
                        "local S=require('state'); assert(S.E==S.W:GetEntityById('1')); " +
                        "assert(S.E~=S.W:GetEntityById('2'))");
                    Check(Equality.Status == Runtime.RuntimeStatus.OK && Entities.Reads == BeforeReads,
                        "equality is token-based and host-read-free: " + Equality.Error);
                    using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                        object Provider = new object();
                        string[] Owner = Registry.RegisterArchive(Provider, AddonPackage(
                            "{\"schema\":1,\"id\":\"entityowner\",\"version\":\"1.0.0\",\"publicModules\":[\"api\"]}",
                            "return true", "return game:GetService('Workspace'):GetEntityById('1')"));
                        Check(Owner[0] == "OK" && Registry.ProcessOne() &&
                            Registry.Status(Provider, Owner[1])[2] == "Active", "Entity owner addon activates");
                        string[] Consumer = Registry.RegisterArchive(Provider, AddonPackage(
                            "{\"schema\":1,\"id\":\"entityconsumer\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":[\"entityowner\"],\"optional\":[]}}",
                            "local Foreign=require('@entityowner/api'); " +
                            "local Own=game:GetService('Workspace'):GetEntityById('1'); " +
                            "assert(Foreign==Own and Foreign.Id=='1' and Own.Id=='1')"));
                        Check(Consumer[0] == "OK" && Registry.ProcessOne() &&
                            Registry.Status(Provider, Consumer[1])[2] == "Active",
                            "cross-domain Entity equality and owner-bound reads");
                    }
                    Entities.Valid = false;
                    var Stale = Host.Execute("entity1b.stale",
                        "local E=require('state').E; assert(E==E); " +
                        "assert(not pcall(function() return E.Id end)); " +
                        "assert(not pcall(function() return E.Prefab end)); " +
                        "assert(not pcall(function() return E.Position end))");
                    Check(Stale.Status == Runtime.RuntimeStatus.OK, "stale properties fail closed: " + Stale.Error);
                    Entities.Valid = true;
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK,
                        "replacement obtains fresh Workspace and Entity facade");
                }
            }
            Console.WriteLine("[CarbonLuau:EntityRead] PASS native bootstrap, exact ID, cold module, cross-domain equality, stale reads");
        }
        finally {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
