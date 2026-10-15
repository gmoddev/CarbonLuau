using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Register exclusively through the shipped bootstrap and pinned compiler/VM.
// Private descriptors are the observation seam, not a substitute registration
// path or proof of physical Rust payouts (qualified separately by D2A).
internal static class LootPublicTests
{
    private const string Setup = "local S=require('state'); local Loot=game:GetService('Loot'); local Mode=Enum.DropMode.Replace; ";
    private const string Prefab = "'assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab'";
    private const string Barrel2 = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab";
    private const string Prefab2 = "'" + Barrel2 + "'";
    private const string Entry = "{Item='scrap',Min=1,Max=2}";
    private const string Register = "Loot:RegisterDropRule(" + Prefab + ",Mode,{" + Entry + "}); ";
    [DllImport("libc")] private static extern int chmod(string Path, int Mode);

    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        internal int Calls;
        internal bool Fail;
        internal Action ObservePublication;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        {
            Calls++;
            if (ObservePublication != null) ObservePublication();
            if (Fail && Next != null) throw new InvalidOperationException("loot fixture registrar failure");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Registrar Registrar = new Registrar();
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.ScriptHost Host;
        internal readonly Dictionary<string, string> Modules = new Dictionary<string, string>(StringComparer.Ordinal);
        internal bool SourceAvailable = true, MissingItem;
        internal int Lookups;
        internal string Source = "return true";
        internal Action ObserveLookup;

        internal Fixture(Runtime.NativeRuntime Native)
        {
            World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), Registrar,
                new Runtime.ItemDirectory(Name => {
                    Lookups++; if (ObserveLookup != null) ObserveLookup();
                    return !MissingItem && LootItemManifestTests.Contains(Name) ? new object() : null;
                }));
            World.LootAvailable = () => SourceAvailable;
            Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {
                MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20
            }, Snapshot, World);
        }
        private Runtime.ScriptSnapshot Snapshot()
        {
            var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
            Value.Modules.Add("state", "return {}");
            foreach (var Module in Modules) Value.Modules.Add(Module.Key, Module.Value);
            return Value;
        }
        internal Runtime.ExecutionResult Reload(string Code)
        { Source = Code; return Host.Reload(); }
        internal void Load(string Code)
        {
            Runtime.ExecutionResult Result = Reload(Code);
            Check(Result.Status == Runtime.RuntimeStatus.OK, "root publication: " + Result.Error);
        }
        internal void Execute(string Code)
        {
            Runtime.ExecutionResult Result = Host.Execute("loot.public", Code);
            Check(Result.Status == Runtime.RuntimeStatus.OK, "real VM execution: " + Result.Error + " / " + Code);
        }
        internal Runtime.LootDropRule Selected(out Runtime.FacadeSession Owner)
        { return Selected(Runtime.LootRulePolicy.Prefab, out Owner); }
        internal Runtime.LootDropRule Selected(string Prefab, out Runtime.FacadeSession Owner)
        {
            Runtime.LootDropRule Rule;
            Check(World.TryLootRule(Prefab, out Owner, out Rule) && Owner != null && Rule != null,
                "committed public declaration selected");
            Check(String.Equals(Rule.Prefab, Prefab, StringComparison.Ordinal), "public descriptor retains actual prefab");
            return Rule;
        }
        internal Runtime.LootDropRule Selected(Runtime.FacadeSession Expected)
        {
            Runtime.FacadeSession Owner; Runtime.LootDropRule Rule = Selected(out Owner);
            Check(Object.ReferenceEquals(Owner, Expected), "exact public declaration owner"); return Rule;
        }
        internal void None()
        {
            Runtime.FacadeSession Owner; Runtime.LootDropRule Rule;
            Check(!World.TryLootRule(Runtime.LootRulePolicy.Prefab, out Owner, out Rule) && Owner == null && Rule == null,
                "no rule effect or retained selection outputs");
            Check(!World.TryLootRule(Barrel2, out Owner, out Rule) && Owner == null && Rule == null,
                "no barrel-2 rule effect or retained selection outputs");
        }
        public void Dispose()
        {
            Host.Dispose(); None();
            Check(World.LootRetainedSessionCount == 0, "all committed/staged Loot retention released");
            Check(World.Sessions().Count == 0 && !World.HasWork && World.Gui.LiveRegistryCount == 0,
                "public fixture teardown clears domain state");
        }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new InvalidOperationException("Loot public: " + Message); }
    private static string Declaration(int Amount)
    { return Declaration(Prefab, Amount); }
    private static string Declaration(string Prefab, int Amount)
    {
        string Number = Amount.ToString(CultureInfo.InvariantCulture);
        return Setup + "Loot:RegisterDropRule(" + Prefab + ",Mode,{{Item='scrap',Min=" + Number + ",Max=" + Number + "}}); ";
    }
    private static void EntryIs(Runtime.LootDropRule Rule, int Index, string Item, int Min, int Max)
    {
        Runtime.LootDropEntry Value = Rule.Entry(Index);
        Check(Value.Item == Item && Value.Min == Min && Value.Max == Max, "exact copied entry " + Index);
    }
    private static string Drain(Runtime.ScriptHost Host)
    {
        var Logs = new StringBuilder();
        for (int Pass = 0; Pass < 128 && Host.HasWork; Pass++)
            foreach (Runtime.ExecutionResult Result in Host.Drain()) {
                Check(Result.Status == Runtime.RuntimeStatus.OK, "callback: " + Result.Error); Logs.Append(Result.Logs);
            }
        Check(!Host.HasWork, "bounded callback drain converges"); return Logs.ToString();
    }

    internal static void Run(string Library, string Compiler)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauLootPublic-" + Guid.NewGuid().ToString("N"));
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(Library, Target);
            string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, Worker);
            if (Rid == "linux-x64") Check(chmod(Worker, 493) == 0, "compiler executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) RunNative(Native);
        } finally { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        CatalogDeclarations(Native);
        ValidDeclarations(Native); InvalidDeclarations(Native); BackingCapacity(Native); Publication(Native);
        Availability(Native); RootReplacement(Native); AddonOwnership(Native); ScaleAndRecovery(Native);
        ProfileDeclarations(Native); ProfilePublication(Native); ProfileRootReplacement(Native); ProfileAddonOwnership(Native);
        ItemDeclarations(Native);
        RegistryDeclarations(Native);
        AdditionalItemDeclarations(Native);
        BulkItemDeclarations(Native);
        Check(Native.LiveVmCount == 0, "all real VM fixtures released");
        Console.WriteLine("[CarbonLuau:LootPublic] PASS pinned VM declarations, raw-table validation, enum identity, publication, conflicts, lifecycle, recovery and 100 addons; synthetic host only");
    }

    private static void CatalogDeclarations(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Load("local Loot=game:GetService('Loot'); assert(type(Item.Scrap)=='string'); " +
                "Loot:RegisterDropRule(Prefab.Loot.Barrel1,Enum.DropMode.Replace,{{Item=Item.Scrap,Min=5,Max=12},{Item='wood',Min=50,Max=100}}); " +
                "Loot:RegisterDropRule(Prefab.Loot.Barrel2,Enum.DropMode.Replace,{{Item=Item.MetalFragments,Min=10,Max=25}})");
            var First = Value.Selected(Value.World.Active);
            var Second = ProfileSelected(Value, Barrel2, Value.World.Active);
            EntryIs(First, 0, "scrap", 5, 12); EntryIs(First, 1, "wood", 50, 100);
            EntryIs(Second, 0, "metal.fragments", 10, 25);
            Value.Load("local Loot=game:GetService('Loot'); " +
                "Loot:RegisterDropRule('assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab',Enum.DropMode.Replace,{{Item='scrap',Min=5,Max=12},{Item=Item.Wood,Min=50,Max=100}}); " +
                "Loot:RegisterDropRule('assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab',Enum.DropMode.Replace,{{Item='metal.fragments',Min=10,Max=25}})");
            First = Value.Selected(Value.World.Active); Second = ProfileSelected(Value, Barrel2, Value.World.Active);
            EntryIs(First, 0, "scrap", 5, 12); EntryIs(First, 1, "wood", 50, 100); EntryIs(Second, 0, "metal.fragments", 10, 25);
            Console.WriteLine("[CarbonLuau:CatalogLoot] PASS constant/literal parity through unchanged two-prefab public declaration and publication");
        }
    }

    private static Runtime.LootDropRule ProfileSelected(Fixture Value, string Prefab, Runtime.FacadeSession Expected)
    {
        Runtime.FacadeSession Owner; Runtime.LootDropRule Rule = Value.Selected(Prefab, out Owner);
        Check(Object.ReferenceEquals(Owner, Expected), "profile selects exact committed owner"); return Rule;
    }
    private static void Nonmatching(Fixture Value, string Prefab, Runtime.FacadeSession Incumbent, Runtime.LootDropRule Snapshot)
    {
        Runtime.FacadeSession Owner = Incumbent; Runtime.LootDropRule Rule = Snapshot;
        Check(!Value.World.TryLootRule(Prefab, out Owner, out Rule) && Owner == null && Rule == null,
            "nonmatching public profile clears both selection outputs");
    }
    private static string DualDeclaration(int First, int Second)
    { return Declaration(First) + Declaration(Prefab2, Second); }

    private static void RegistryDeclarations(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Modules["first"] = Declaration(5) + "return true";
            Value.Modules["second"] = Declaration(Prefab2, 50) + "return true";
            Value.Modules["badsecond"] = "require('second'); error('failed second parent')";
            Value.Load("require('first'); assert(not pcall(require,'badsecond')); require('second')");
            var Old = Value.World.Active;
            var First = ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Old);
            var Second = ProfileSelected(Value, Barrel2, Old);
            EntryIs(First, 0, "scrap", 5, 5); EntryIs(Second, 0, "scrap", 50, 50);
            Check(Value.World.LootRetainedSessionCount == 1, "two committed rules share one exact owner reservation");
            int Lookups = Value.Lookups;
            Value.Execute(Setup + "for _,P in {" + Prefab + "," + Prefab2 + "} do " +
                "assert(not pcall(function() Loot:RegisterDropRule(P,Mode,{}) end)) end");
            Check(Value.Lookups == Lookups, "committed dual owner cannot redeclare either target");
            Value.ObserveLookup = () => {
                Check(Object.ReferenceEquals(ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Old), First) &&
                    Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Old), Second), "whole old set remains visible during candidate initialization");
            };
            var Failed = Value.Reload(DualDeclaration(8, 80) + "error('failed dual root')");
            Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Old &&
                Value.World.LootRetainedSessionCount == 1, "failed dual candidate releases staging and preserves both old rules");
            Value.Registrar.Fail = true; Failed = Value.Reload(DualDeclaration(9, 90)); Value.Registrar.Fail = false;
            Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Old &&
                Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Old), Second), "failed registrar cannot publish half a dual set");
            Value.Load(DualDeclaration(7, 70)); Value.ObserveLookup = null;
            var Next = Value.World.Active;
            Check(Old.Disposed && !Old.HasLootRule && Value.World.LootRetainedSessionCount == 1,
                "healthy whole-set replacement retires only exact outgoing owner");
            EntryIs(ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Next), 0, "scrap", 7, 7);
            EntryIs(ProfileSelected(Value, Barrel2, Next), 0, "scrap", 70, 70);
            Failed = Value.Reload(DualDeclaration(8, 80) + "local T=table.create(10000000,true)");
            Check(Failed.Status != Runtime.RuntimeStatus.OK, "real VM allocation exhaustion cannot publish a candidate rule set");
            if (Failed.Retired) {
                Check(Next.Disposed && Value.World.LootRetainedSessionCount == 0, "fatal allocation failure retires all old and staged authority");
                Value.Load(DualDeclaration(7, 70));
            } else {
                Check(Value.World.Active == Next && Value.World.LootRetainedSessionCount == 1,
                    "contained allocation failure preserves both exact committed rules and releases candidate retention");
                EntryIs(ProfileSelected(Value, Barrel2, Next), 0, "scrap", 70, 70);
            }
            Value.Load(Setup + "local A=table.create(8); local B=table.create(8); for Index=1,8 do " +
                "A[Index]={Item='scrap',Min=1000,Max=1000}; B[Index]={Item='wood',Min=1000,Max=1000} end; " +
                "Loot:RegisterDropRule(" + Prefab + ",Mode,A); Loot:RegisterDropRule(" + Prefab2 + ",Mode,B); table.clear(A); table.clear(B)");
            var MaximumFirst = ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Value.World.Active);
            var MaximumSecond = ProfileSelected(Value, Barrel2, Value.World.Active);
            Check(MaximumFirst.Count == 8 && MaximumSecond.Count == 8 && MaximumFirst.MaximumTotal == 8000 && MaximumSecond.MaximumTotal == 8000,
                "two declarations retain independent per-rule limits, not a larger physical occurrence budget");
            Value.Load(Declaration(6));
            Nonmatching(Value, Barrel2, Value.World.Active, Value.Selected(Value.World.Active));
            Check(First.Entry(0).Max == 5 && Second.Entry(0).Max == 50, "omission withdraws a key without retargeting old descriptors");
            using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
                object Provider = new object(), OtherProvider = new object();
                string Token = Add(Registry, Provider, Package("registryowner", Declaration(Prefab2, 100)), "Active");
                var Root = Value.World.Active; var RootRule = Value.Selected(Root);
                Runtime.FacadeSession Addon; var AddonRule = Value.Selected(Barrel2, out Addon);
                var Conflict = Value.Reload(DualDeclaration(10, 200));
                Check(Conflict.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Root &&
                    Object.ReferenceEquals(Value.Selected(Root), RootRule) &&
                    Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Addon), AddonRule), "one conflicting key rejects entire root set before publication");
                int Calls = Value.Registrar.Calls;
                Add(Registry, OtherProvider, Package("registryforeign", DualDeclaration(11, 210)), "Failed");
                Check(Value.Registrar.Calls == Calls && Value.World.LootRetainedSessionCount == 2,
                    "foreign dual candidate conflict leaves both owners and no staging reservation");
                Value.Load(Declaration(12)); EntryIs(ProfileSelected(Value, Barrel2, Addon), 0, "scrap", 100, 100);
                Replace(Registry, Provider, Token, Package("registryowner", Declaration(Prefab2, 120), "2.0.0"));
                Runtime.FacadeSession Replacement; var Replaced = Value.Selected(Barrel2, out Replacement);
                Check(Addon.Disposed && !Addon.HasLootRule && Replacement != Addon, "addon replacement retires its own key only");
                EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 12, 12); EntryIs(Replaced, 0, "scrap", 120, 120);
                Check(Registry.UnloadProvider(Provider) == 1 && Replacement.Disposed, "provider unload withdraws exact addon owner");
                EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 12, 12);
                Nonmatching(Value, Barrel2, Value.World.Active, Value.Selected(Value.World.Active));
                Registry.UnloadProvider(OtherProvider);
            }
            Value.Load(DualDeclaration(13, 130)); var BeforeRecovery = Value.World.Active; long Vm = Value.Host.VmGenerationId;
            var Fatal = Value.Host.Execute("loot.registry.timeout", "while true do end");
            Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && BeforeRecovery.Disposed && Value.Host.VmGenerationId != Vm,
                "fatal recovery retires entire dual owner without preserving stale authority");
            EntryIs(ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Value.World.Active), 0, "scrap", 13, 13);
            EntryIs(ProfileSelected(Value, Barrel2, Value.World.Active), 0, "scrap", 130, 130);
            Check(Value.World.LootRetainedSessionCount == 1 && Drain(Value.Host) == "", "dual reconstruction has one fresh owner and no payout replay");
        }
    }

    private static string BulkDeclaration(string Profile, string Name, int Amount, int Count = 1)
    {
        var Entries = new string[Count];
        string Entry = "{Item='" + Name + "',Min=" + Amount.ToString(CultureInfo.InvariantCulture) +
            ",Max=" + Amount.ToString(CultureInfo.InvariantCulture) + "}";
        for (int Index = 0; Index < Count; Index++) Entries[Index] = Entry;
        return Setup + "local Drops={" + String.Join(",", Entries) + "}; Loot:RegisterDropRule(" + Profile +
            ",Mode,Drops); if Drops[1] then Drops[1].Item='carbonluau.d8.invalid'; Drops[1].Max=0 end; table.clear(Drops); ";
    }

    private static void BulkItemDeclarations(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            foreach (var Definition in LootItemManifestTests.Definitions) {
                int Maximum = Math.Min(1000, Definition.Stack);
                foreach (string Profile in new[] { Prefab, Prefab2 }) {
                    string Path = Profile == Prefab ? Runtime.LootRulePolicy.Prefab : Barrel2;
                    foreach (int Amount in new[] { 0, 1, Maximum }) {
                        Value.Load(BulkDeclaration(Profile, Definition.Name, Amount));
                        Runtime.FacadeSession Owner;
                        var Rule = Value.Selected(Path, out Owner);
                        Check(Rule.Count == 1 && Rule.Entry(0).Item == Definition.Name && Rule.Entry(0).Min == Amount &&
                            Rule.Entry(0).Max == Amount && Rule.MaximumTotal == Amount,
                            "D8 every manifest name public zero/min/capped maximum copied scalars on both prefabs");
                    }
                    Value.Load(BulkDeclaration(Profile, Definition.Name, Maximum, 8));
                    Runtime.FacadeSession PreviousOwner;
                    var Previous = Value.Selected(Path, out PreviousOwner);
                    Check(Previous.Count == 8 && Previous.MaximumTotal == Maximum * 8 && Previous.Entry(7).Item == Definition.Name,
                        "D8 every manifest name eight repeated entries retain exact copied maximum");
                    int Lookups = Value.Lookups;
                    foreach (string Invalid in new[] {
                        BulkDeclaration(Profile, Definition.Name, 1001),
                        BulkDeclaration(Profile, Definition.Name, 1, 9),
                        BulkDeclaration(Profile, Definition.Name.ToUpperInvariant(), 1),
                        BulkDeclaration(Profile, "carbonluau.d8.invalid", 1) }) {
                        var Failed = Value.Reload(Invalid);
                        Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == PreviousOwner &&
                            Object.ReferenceEquals(Value.Selected(Path, out PreviousOwner), Previous) && Value.Lookups == Lookups &&
                            Value.World.LootRetainedSessionCount == 1,
                            "D8 every-name malformed declaration preserves exact incumbent before lookup without retention charge");
                    }
                    Value.MissingItem = true;
                    var Missing = Value.Reload(BulkDeclaration(Profile, Definition.Name, 1)); Value.MissingItem = false;
                    Check(Missing.Status != Runtime.RuntimeStatus.OK && Object.ReferenceEquals(Value.Selected(Path, out PreviousOwner), Previous) &&
                        Value.World.LootRetainedSessionCount == 1,
                        "D8 every-name unavailable definition preserves incumbent and charge");
                }
            }
            for (int Start = 0; Start < LootItemManifestTests.Definitions.Length; Start += 8) {
                int Count = Math.Min(8, LootItemManifestTests.Definitions.Length - Start);
                var Entries = new string[Count];
                for (int Index = 0; Index < Count; Index++)
                    Entries[Index] = "{Item='" + LootItemManifestTests.Definitions[Start + Index].Name + "',Min=1,Max=1}";
                Value.Load(Setup + "Loot:RegisterDropRule(" + Prefab + ",Mode,{" + String.Join(",", Entries) + "}); " +
                    "Loot:RegisterDropRule(" + Prefab2 + ",Mode,{" + String.Join(",", Entries) + "});");
                Runtime.FacadeSession Owner;
                var First = Value.Selected(Runtime.LootRulePolicy.Prefab, out Owner);
                var Second = ProfileSelected(Value, Barrel2, Owner);
                Check(First.Count == Count && Second.Count == Count && Value.World.LootRetainedSessionCount == 1,
                    "D8 mixed finite manifest batches atomically publish both independent prefab descriptors");
                for (int Index = 0; Index < Count; Index++)
                    Check(First.Entry(Index).Item == LootItemManifestTests.Definitions[Start + Index].Name &&
                        Second.Entry(Index).Item == First.Entry(Index).Item && First.Entry(Index).Max == 1,
                        "D8 mixed manifest entries preserve names and order");
            }
        }
        Console.WriteLine("[CarbonLuau:LootPublicTest] PASS D8 all finite manifest names on real VM: copied zero/min/max/repeated/mixed, rejection and atomic two-prefab publication");
    }

    private static string StonesDeclaration(string Profile, int Amount)
    {
        string Number = Amount.ToString(CultureInfo.InvariantCulture);
        return Setup + "Loot:RegisterDropRule(" + Profile + ",Mode,{{Item='stones',Min=" + Number + ",Max=" + Number + "}}); ";
    }
    private static void AdditionalItemDeclarations(Runtime.NativeRuntime Native)
    {
        foreach (string Profile in new[] { Prefab, Prefab2 })
        using (var Value = new Fixture(Native)) {
            string Name = Profile == Prefab ? Runtime.LootRulePolicy.Prefab : Barrel2;
            Value.ObserveLookup = Value.None;
            Value.Load(Setup + "local Drops={{Item='stones',Min=50,Max=250},{Item='wood',Min=50,Max=100}," +
                "{Item='scrap',Min=5,Max=12},{Item='metal.fragments',Min=10,Max=25}}; " +
                "Loot:RegisterDropRule(" + Profile + ",Mode,Drops); Drops[1].Item='wood'; table.clear(Drops)");
            var Owner = Value.World.Active; var Snapshot = ProfileSelected(Value, Name, Owner);
            EntryIs(Snapshot, 0, "stones", 50, 250); EntryIs(Snapshot, 1, "wood", 50, 100);
            EntryIs(Snapshot, 2, "scrap", 5, 12); EntryIs(Snapshot, 3, "metal.fragments", 10, 25);
            Check(Snapshot.Count == 4 && Snapshot.MaximumTotal == 387 && Value.Lookups == 4,
                "D7 mixed four-definition copied public descriptor on each qualified profile");
            Value.ObserveLookup = null;
            foreach (int Amount in new[] {0, 1, 1000}) {
                Value.Load(StonesDeclaration(Profile, Amount));
                EntryIs(ProfileSelected(Value, Name, Value.World.Active), 0, "stones", Amount, Amount);
            }
            Value.Load(Setup + "local Record=table.freeze({Item='stones',Min=0,Max=1000}); " +
                "Loot:RegisterDropRule(" + Profile + ",Mode,{Record,Record,Record,Record,Record,Record,Record,Record})");
            Owner = Value.World.Active; Snapshot = ProfileSelected(Value, Name, Owner);
            Check(Snapshot.Count == 8 && Snapshot.MaximumTotal == 8000, "D7 maximum public descriptor preserves existing budgets");
            for (int Index = 0; Index < 8; Index++) EntryIs(Snapshot, Index, "stones", 0, 1000);
            foreach (string Bad in new[] {
                "{{Item='stones',Min=0,Max=1001}}",
                "(function() local T={}; for Index=1,9 do T[Index]={Item='stones',Min=0,Max=0} end; return T end)()"}) {
                int Lookups = Value.Lookups;
                var Failed = Value.Reload(Setup + "Loot:RegisterDropRule(" + Profile + ",Mode," + Bad + ")");
                Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.Lookups == Lookups &&
                    Object.ReferenceEquals(ProfileSelected(Value, Name, Owner), Snapshot) && Value.World.LootRetainedSessionCount == 1,
                    "D7 invalid amount/cardinality rejects before lookup and preserves incumbent");
            }
            Value.MissingItem = true;
            var Missing = Value.Reload(StonesDeclaration(Profile, 1)); Value.MissingItem = false;
            Check(Missing.Status != Runtime.RuntimeStatus.OK && Object.ReferenceEquals(ProfileSelected(Value, Name, Owner), Snapshot),
                "D7 missing definition does not replace the current exact owner");
        }
        using (var Value = new Fixture(Native)) {
            string Both = StonesDeclaration(Prefab, 50) + StonesDeclaration(Prefab2, 250);
            Value.Load(Both); var Root = Value.World.Active;
            var First = ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Root);
            var Second = ProfileSelected(Value, Barrel2, Root);
            Value.Registrar.Fail = true;
            var Failed = Value.Reload(StonesDeclaration(Prefab, 60) + StonesDeclaration(Prefab2, 260)); Value.Registrar.Fail = false;
            Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Root &&
                Object.ReferenceEquals(ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Root), First) &&
                Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Root), Second) && Value.World.LootRetainedSessionCount == 1,
                "D7 failed whole-owner publication preserves both exact committed rules");
            Failed = Value.Reload(StonesDeclaration(Prefab, 60) + StonesDeclaration(Prefab2, 260) + "local T=table.create(10000000,true)");
            Check(Failed.Status != Runtime.RuntimeStatus.OK, "D7 real VM allocation exhaustion cannot publish candidate descriptors");
            if (Failed.Retired) {
                Check(Root.Disposed && Value.World.LootRetainedSessionCount == 0, "D7 fatal allocation retirement clears both old and staged owners");
                Value.None();
            } else Check(Value.World.Active == Root && Object.ReferenceEquals(ProfileSelected(Value, Runtime.LootRulePolicy.Prefab, Root), First) &&
                Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Root), Second) && Value.World.LootRetainedSessionCount == 1,
                "D7 contained allocation error preserves complete prior owner set");
            Value.Load(StonesDeclaration(Prefab, 50));
            using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
                object Provider = new object(), OtherProvider = new object();
                string Token = Add(Registry, Provider, Package("stonesowner", StonesDeclaration(Prefab2, 250)), "Active");
                Root = Value.World.Active; First = Value.Selected(Root);
                Runtime.FacadeSession Addon; Second = Value.Selected(Barrel2, out Addon);
                Failed = Value.Reload(Both);
                Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Root &&
                    Object.ReferenceEquals(Value.Selected(Root), First) && Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Addon), Second),
                    "D7 overlapping dual candidate preserves independent owners");
                Add(Registry, OtherProvider, Package("stonesforeign", Both), "Failed");
                Replace(Registry, Provider, Token, Package("stonesowner", StonesDeclaration(Prefab2, 260) + "error('D7 failed replacement')", "2.0.0"));
                Check(Status(Registry, Provider, Token, "Active")[5].Contains("replacement failed") &&
                    Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Addon), Second), "D7 failed addon replacement preserves incumbent");
                Value.Load(StonesDeclaration(Prefab, 60));
                Check(Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Addon), Second), "D7 root replacement does not retarget foreign slot");
                Replace(Registry, Provider, Token, Package("stonesowner", StonesDeclaration(Prefab2, 260), "3.0.0"));
                Runtime.FacadeSession Replacement; var Current = Value.Selected(Barrel2, out Replacement);
                Check(Addon.Disposed && Replacement != Addon, "D7 addon replacement retires only its original owner");
                EntryIs(Value.Selected(Value.World.Active), 0, "stones", 60, 60); EntryIs(Current, 0, "stones", 260, 260);
                Check(Registry.UnloadProvider(Provider) == 1 && Replacement.Disposed, "D7 provider unload withdraws exact addon key");
                Nonmatching(Value, Barrel2, Value.World.Active, Value.Selected(Value.World.Active));
                Registry.UnloadProvider(OtherProvider);
            }
            Value.Load(Both); Root = Value.World.Active; long Vm = Value.Host.VmGenerationId;
            var Fatal = Value.Host.Execute("loot.stones.timeout", "while true do end");
            Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && Root.Disposed && Value.Host.VmGenerationId != Vm &&
                Value.World.LootRetainedSessionCount == 1 && Drain(Value.Host) == "", "D7 fatal VM recovery reconstructs fresh dual owner without replay");
            EntryIs(Value.Selected(Value.World.Active), 0, "stones", 50, 50);
            EntryIs(ProfileSelected(Value, Barrel2, Value.World.Active), 0, "stones", 250, 250);
        }
    }
    private static void ItemDeclarations(Runtime.NativeRuntime Native)
    {
        foreach (string Profile in new[] { Prefab, Prefab2 })
        using (var Value = new Fixture(Native)) {
            string Name = Profile == Prefab ? Runtime.LootRulePolicy.Prefab : Barrel2;
            Value.ObserveLookup = Value.None;
            Value.Load(Setup + "local Drops={{Item='wood',Min=50,Max=100},{Item='scrap',Min=5,Max=12}," +
                "{Item='metal.fragments',Min=10,Max=25}}; S.Drops=Drops; " +
                "Loot:RegisterDropRule(" + Profile + ",Mode,Drops); Drops[1].Item='stones'; table.clear(Drops)");
            var Owner = Value.World.Active; var Snapshot = ProfileSelected(Value, Name, Owner);
            EntryIs(Snapshot, 0, "wood", 50, 100); EntryIs(Snapshot, 1, "scrap", 5, 12);
            EntryIs(Snapshot, 2, "metal.fragments", 10, 25);
            Check(Snapshot.Count == 3 && Snapshot.MaximumTotal == 137 && Value.Lookups == 3,
                "mixed qualified items copied before publication on each exact profile");
            Value.ObserveLookup = null;
            var Failed = Value.Reload(Setup + "Loot:RegisterDropRule(" + Profile +
                ",Mode,{{Item='wood',Min=1,Max=1000}}); error('failed wood replacement')");
            Check(Failed.Status != Runtime.RuntimeStatus.OK && !Owner.Disposed &&
                Object.ReferenceEquals(ProfileSelected(Value, Name, Owner), Snapshot),
                "failed wood candidate preserves the original immutable rule");
            Value.Load(Setup + "local Record=table.freeze({Item='wood',Min=0,Max=1000}); " +
                "Loot:RegisterDropRule(" + Profile + ",Mode,{Record,Record,Record,Record,Record,Record,Record,Record})");
            var Maximum = ProfileSelected(Value, Name, Value.World.Active);
            Check(Maximum.Count == 8 && Maximum.MaximumTotal == 8000, "wood retains global entry/amount budgets");
            for (int Index = 0; Index < Maximum.Count; Index++) EntryIs(Maximum, Index, "wood", 0, 1000);
            Value.Load(Setup + "Loot:RegisterDropRule(" + Profile + ",Mode,{{Item='wood',Min=0,Max=0}})");
            EntryIs(ProfileSelected(Value, Name, Value.World.Active), 0, "wood", 0, 0);
            Value.MissingItem = true;
            var Missing = Value.Reload(Setup + "Loot:RegisterDropRule(" + Profile + ",Mode,{{Item='wood',Min=1,Max=1}})");
            Check(Missing.Status != Runtime.RuntimeStatus.OK, "missing wood definition cannot publish");
            EntryIs(ProfileSelected(Value, Name, Value.World.Active), 0, "wood", 0, 0);
        }
    }

    private static void ProfileDeclarations(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            var Code = new StringBuilder(Setup);
            foreach (string Name in new[] {
                "'loot-barrel-2'", "'loot-barrel-2.prefab'", "string.upper(" + Prefab2 + ")", "' '.." + Prefab2,
                Prefab2 + "..' '", Prefab2 + "..'extra'", Prefab2 + "..'\\0'", "string.rep('x',1025)",
                "string.gsub(" + Prefab2 + ", '/', string.char(92))",
                "'assets/bundled/prefabs/autospawn/resource/loot/../loot/loot-barrel-2.prefab'",
                "'assets/bundled/prefabs/radtown/loot_barrel_1.prefab'",
                "'assets/bundled/prefabs/radtown/loot_barrel_2.prefab'",
                "'assets/bundled/prefabs/radtown/oil_barrel.prefab'",
                "'assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-3.prefab'" })
                Code.Append("assert(not pcall(function() Loot:RegisterDropRule(").Append(Name).Append(",Mode,{").Append(Entry).Append("}) end)); ");
            foreach (string Drops in new[] {
                "{" + Entry + ",{Item='carbonluau.d8.invalid',Min=0,Max=1}}",
                "{" + Entry + ",{Item='Scrap',Min=0,Max=1}}",
                "{" + Entry + ",{Item='scrap\\0',Min=0,Max=1}}",
                "{" + Entry + ",{Item='scráp',Min=0,Max=1}}",
                "{" + Entry + ",{Item='metal.fragments',Min=2,Max=1}}",
                "{" + Entry + ",{Item='metal.fragments',Min=0,Max=1001}}" })
                Code.Append("assert(not pcall(function() Loot:RegisterDropRule(").Append(Prefab2).Append(",Mode,").Append(Drops).Append(") end)); ");
            Value.Load(Code.ToString()); Value.None();
            Check(Value.Lookups == 0 && !Value.World.Active.HasLootRule && !Value.Host.HasWork,
                "barrel-2 malformed and unsupported strings reject before any definition resolver");
            Value.ObserveLookup = Value.None;
            Value.Load(Setup + "local Drops={{Item='metal.fragments',Min=3,Max=7}}; S.Drops=Drops; " +
                "assert(select('#',Loot:RegisterDropRule(" + Prefab2 + ",Mode,Drops))==0); " +
                "assert(not pcall(function() Loot:RegisterDropRule(" + Prefab2 + ",Mode,{" + Entry + "}) end)); Drops[1].Item='wood'; Drops[1].Max=99; table.clear(Drops)");
            Runtime.FacadeSession Owner = Value.World.Active; Runtime.LootDropRule Snapshot = ProfileSelected(Value, Barrel2, Owner);
            Check(Value.Lookups == 1 && Snapshot.Count == 1 && Snapshot.MaximumTotal == 7,
                "barrel-2 copies its declaration and same-prefab duplicate rejects before lookup");
            EntryIs(Snapshot, 0, "metal.fragments", 3, 7);
            foreach (string Name in new[] { Runtime.LootRulePolicy.Prefab, Barrel2.ToUpperInvariant(), Barrel2 + "extra", null })
                Nonmatching(Value, Name, Owner, Snapshot);
            Value.ObserveLookup = null;
            Value.Execute("table.clear(require('state').Drops)"); EntryIs(Snapshot, 0, "metal.fragments", 3, 7);
            Value.Load(Setup + "Loot:RegisterDropRule(" + Prefab2 + ",Mode,{})");
            Check(ProfileSelected(Value, Barrel2, Value.World.Active).Count == 0, "barrel-2 empty replacement remains a rule");
            Check(Snapshot.Prefab == Barrel2, "captured barrel-2 descriptor keeps its actual prefab after replacement");
        }
    }
    private static void ProfilePublication(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Modules.Add("profilefailed", Declaration(Prefab2, 4) + "error('failed barrel-2 module')");
            Value.Modules.Add("profileinner", Declaration(Prefab2, 5) + "return true");
            Value.Modules.Add("profileouter", "assert(require('profileinner')); error('failed profile parent')");
            Value.Load(Setup + "assert(not pcall(require,'profilefailed')); assert(not pcall(require,'profileouter')); " + Register);
            var Owner = Value.World.Active; var Snapshot = Value.Selected(Owner);
            Nonmatching(Value, Barrel2, Owner, Snapshot);
            int Before = Value.Lookups;
            Value.Execute(Setup + "assert(not pcall(function() " + Declaration(Prefab2, 6) + "end)); " +
                "assert(not pcall(require,'profilefailed')); task.defer(function() assert(not pcall(function() " +
                Declaration(Prefab2, 6) + "end)) end)");
            Check(Drain(Value.Host) == "" && Value.Lookups == Before && Object.ReferenceEquals(Value.Selected(Owner), Snapshot),
                "active direct, cold module and deferred barrel-2 calls preserve barrel-1 publication");
            Value.Load(Setup + "assert(not pcall(require,'profilefailed')); " + Declaration(Prefab2, 7));
            ProfileSelected(Value, Barrel2, Value.World.Active);
            Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Value.World.Active, ProfileSelected(Value, Barrel2, Value.World.Active));
        }
    }
    private static void ProfileRootReplacement(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Load(Declaration(2)); var Previous = Value.World.Active; var Original = Value.Selected(Previous);
            long Vm = Value.Host.VmGenerationId;
            Value.ObserveLookup = () => {
                Check(Object.ReferenceEquals(Value.Selected(Previous), Original), "cross-profile candidate remains invisible while prepared");
                Nonmatching(Value, Barrel2, Previous, Original);
            };
            Value.Registrar.ObservePublication = Value.ObserveLookup;
            Runtime.ExecutionResult Failed = Value.Reload(Declaration(Prefab2, 9) +
                "task.defer(function() print('must never run') end); error('failed profile candidate')");
            Check(Failed.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Value.World.Active == Previous &&
                !Previous.Disposed && Drain(Value.Host) == "", "failed cross-profile candidate preserves old lifetime and discards work");
            Value.Registrar.Fail = true; Failed = Value.Reload(Declaration(Prefab2, 8));
            Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Previous &&
                Object.ReferenceEquals(Value.Selected(Previous), Original), "failed cross-profile registrar preserves exact old descriptor");
            Nonmatching(Value, Barrel2, Previous, Original);
            Value.Registrar.Fail = false; Value.Load(Declaration(Prefab2, 8));
            Value.ObserveLookup = null; Value.Registrar.ObservePublication = null;
            var Next = Value.World.Active; var Second = ProfileSelected(Value, Barrel2, Next);
            Check(Previous.Disposed && !Previous.HasLootRule && Value.Host.VmGenerationId == Vm,
                "healthy profile switch retires outgoing root inside same VM");
            Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Next, Second); EntryIs(Second, 0, "scrap", 8, 8);
            Value.ObserveLookup = () => {
                Check(Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Next), Second), "reverse profile switch retains incumbent until publication");
                Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Next, Second);
            };
            Value.Load(Declaration(3)); Value.ObserveLookup = null;
            Nonmatching(Value, Barrel2, Value.World.Active, Value.Selected(Value.World.Active));
            Check(Next.Disposed && Original.Prefab == Runtime.LootRulePolicy.Prefab && Second.Prefab == Barrel2,
                "switching back never retargets captured descriptors");
            Value.Load(Declaration(Prefab2, 5)); var BeforeRecovery = Value.World.Active;
            Runtime.ExecutionResult Fatal = Value.Host.Execute("loot.profile.timeout", "while true do end");
            Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && Value.Host.Ready && BeforeRecovery.Disposed &&
                !BeforeRecovery.HasLootRule && Value.Host.VmGenerationId != Vm && Value.Host.Recoveries == 1,
                "fatal recovery retires barrel-2 authority and reconstructs committed source once");
            var Reconstructed = ProfileSelected(Value, Barrel2, Value.World.Active); EntryIs(Reconstructed, 0, "scrap", 5, 5);
            Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Value.World.Active, Reconstructed);
            Check(Drain(Value.Host) == "", "profile recovery creates no callback replay");
        }
    }
    private static void ProfileAddonOwnership(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Load(Declaration(2));
            using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
                object Provider = new object(), OtherProvider = new object();
                var Root = Value.World.Active; var RootRule = Value.Selected(Root); int Calls = Value.Registrar.Calls;
                string Other = Add(Registry, OtherProvider, Package("lootprofiledisjoint", Declaration(Prefab2, 3)), "Active");
                Runtime.FacadeSession Disjoint; var DisjointRule = Value.Selected(Barrel2, out Disjoint);
                Check(Value.Host.DomainCount == 2 && Disjoint != Root && DisjointRule.Entry(0).Max == 3,
                    "different-prefab root/addon ownership is independent");
                Calls = Value.Registrar.Calls;
                string Collision = Add(Registry, OtherProvider, Package("lootprofilecollision", Declaration(3)), "Failed");
                Check(Value.Registrar.Calls == Calls && Value.Host.DomainCount == 2 && Object.ReferenceEquals(Value.Selected(Root), RootRule) &&
                    Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Disjoint), DisjointRule),
                    "same-prefab foreign addon conflicts before publication without disturbing either owner");
                Check(Registry.Unregister(OtherProvider, Collision)[0] == "OK", "profile collision registration removed");
                Check(Registry.Unregister(OtherProvider, Other)[0] == "OK" && Disjoint.Disposed,
                    "disjoint owner withdrawal leaves root rule intact");
                Nonmatching(Value, Barrel2, Root, RootRule);
                Value.Load("return true"); Value.None();
                string Token = Add(Registry, Provider, Package("lootprofileowner", Declaration(Prefab2, 4)), "Active");
                Runtime.FacadeSession Owner; var OldRule = Value.Selected(Barrel2, out Owner); Calls = Value.Registrar.Calls;
                Add(Registry, OtherProvider, Package("lootprofileforeign", Declaration(Prefab2, 6)), "Failed");
                Runtime.ExecutionResult Conflict = Value.Reload(Declaration(Prefab2, 7));
                Check(Conflict.Status != Runtime.RuntimeStatus.OK && Value.Registrar.Calls == Calls &&
                    Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Owner), OldRule), "same-prefab addon/addon and addon/root conflicts preserve incumbent");
                Value.Source = "return true";
                Replace(Registry, Provider, Token, Package("lootprofileowner", Declaration(9) + "error('failed profile addon')", "2.0.0"));
                Check(Status(Registry, Provider, Token, "Active")[5].Contains("replacement failed") && !Owner.Disposed &&
                    Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Owner), OldRule), "failed addon profile replacement preserves old plan");
                Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Owner, OldRule);
                Value.ObserveLookup = () => {
                    Check(Object.ReferenceEquals(ProfileSelected(Value, Barrel2, Owner), OldRule), "addon profile candidate stays invisible during preparation");
                    Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Owner, OldRule);
                };
                Replace(Registry, Provider, Token, Package("lootprofileowner", Declaration(8), "3.0.0")); Value.ObserveLookup = null;
                Runtime.FacadeSession Next; var NextRule = Value.Selected(out Next);
                Check(Owner.Disposed && !Owner.HasLootRule && Next != Owner, "exact addon owner may publish a different profile");
                EntryIs(NextRule, 0, "scrap", 8, 8); Nonmatching(Value, Barrel2, Next, NextRule);
                Check(OldRule.Prefab == Barrel2 && OldRule.Entry(0).Max == 4, "retired addon descriptor retains barrel-2 identity");
                Check(Registry.UnloadProvider(Provider) == 1 && Next.Disposed, "provider unload removes switched profile authority");
                Check(Registry.UnloadProvider(OtherProvider) == 1, "failed foreign profile registration released"); Value.None();
            }
        }
    }

    private static void ValidDeclarations(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.ObserveLookup = Value.None;
            Value.Load(Setup + "local Drops={" + Entry + ",{Item='metal.fragments',Min=0,Max=1000}}; S.Drops=Drops; " +
                "assert(select('#',Loot:RegisterDropRule(" + Prefab + ",Mode,Drops))==0); " +
                "Drops[1].Item='wood'; Drops[1].Min=99; Drops[1].Max=99; Drops[2]={}; Drops[3]=Drops; " +
                "assert(Drops[1].Item=='wood')");
            Runtime.LootDropRule Copied = Value.Selected(Value.World.Active);
            Check(Copied.Count == 2 && Copied.MaximumTotal == 1002 && Value.Lookups == 2, "two-entry inclusive declaration");
            EntryIs(Copied, 0, "scrap", 1, 2); EntryIs(Copied, 1, "metal.fragments", 0, 1000);
            Value.Execute("local S=require('state'); table.clear(S.Drops); S.Drops[1]=false");
            EntryIs(Copied, 0, "scrap", 1, 2); EntryIs(Copied, 1, "metal.fragments", 0, 1000);
            Value.ObserveLookup = null;
            Value.Load(Setup + "Loot:RegisterDropRule(" + Prefab + ",Mode,{})");
            Runtime.LootDropRule Empty = Value.Selected(Value.World.Active);
            Check(Empty.Count == 0 && Empty.MaximumTotal == 0, "empty array is an effective replacement declaration");
            Value.Load(Declaration(0)); EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 0, 0);
            Value.Load(Setup + "local Drops=table.create(8); for Index=1,8 do Drops[Index]={Item=if Index%2==0 then 'metal.fragments' else 'scrap',Min=0,Max=1000} end; " +
                "Loot:RegisterDropRule(" + Prefab + ",Mode,Drops); table.clear(Drops)");
            Runtime.LootDropRule Maximum = Value.Selected(Value.World.Active);
            Check(Maximum.Count == 8 && Maximum.MaximumTotal == 8000, "maximum descriptor and total");
            for (int Index = 0; Index < 8; Index++) EntryIs(Maximum, Index, Index % 2 == 0 ? "scrap" : "metal.fragments", 0, 1000);
            Value.Load(Setup + "local Record=table.freeze({Item='metal.fragments',Min=1000,Max=1000}); " +
                "Loot:RegisterDropRule(" + Prefab + ",Mode,table.freeze({Record,Record}))");
            Runtime.LootDropRule Repeated = Value.Selected(Value.World.Active);
            Check(Repeated.Count == 2 && Repeated.MaximumTotal == 2000, "frozen plain tables and repeated acyclic records");
            EntryIs(Repeated, 0, "metal.fragments", 1000, 1000); EntryIs(Repeated, 1, "metal.fragments", 1000, 1000);
            Value.Load(Setup + "local Record=table.create(64); Record.Item='scrap'; Record.Min=0; Record.Max=1; " +
                "local Drops=table.create(64); Drops[1]=table.freeze(Record); " +
                "Loot:RegisterDropRule(" + Prefab + ",Mode,table.freeze(Drops))");
            EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 0, 1);
            Value.Load(Setup + "Loot:RegisterDropRule(" + Prefab + ",Mode,table.freeze(table.create(64)))");
            Check(Value.Selected(Value.World.Active).Count == 0, "inclusive 64-slot backing accepts an empty plain frozen array");
            Value.Execute(EnumChecks());
        }
    }

    private static string EnumChecks()
    {
        return "assert(type(Enum)=='table' and table.isfrozen(Enum)); assert(table.isfrozen(Enum.DropMode)); " +
            "local Mode=Enum.DropMode.Replace; assert(type(Mode)=='userdata' and tostring(Mode)=='DropMode.Replace'); " +
            "assert(getmetatable(Mode)=='CarbonLuau.DropMode' and Mode==Enum.DropMode.Replace); " +
            "assert(not pcall(function() Mode.X=1 end)); assert(not pcall(function() return Mode.X end)); " +
            "assert(not pcall(function() setmetatable(Mode,{}) end)); " +
            "assert(not pcall(function() Enum.DropMode.Replace=false end)); assert(not pcall(function() Enum.DropMode={} end)); " +
            "assert(not pcall(function() rawset(Enum,'DropMode',{}) end)); assert(not pcall(function() rawset(Enum.DropMode,'Replace',{}) end)); " +
            "assert(not pcall(function() setmetatable(Enum,{}) end)); assert(Enum.DropMode.Append==nil); " +
            "assert(DropMode==nil); assert(type(GiveItemBehavior.InventoryOnly)=='userdata'); " +
            "assert(tostring(GiveItemBehavior.InventoryOnly)=='GiveItemBehavior.InventoryOnly'); " +
            "assert(getmetatable(GiveItemBehavior.InventoryOnly)=='CarbonLuau.GiveItemBehavior'); " +
            "assert(GiveItemBehavior.DropRemainder==nil and table.isfrozen(GiveItemBehavior)); " +
            "assert(Mode~=GiveItemBehavior.InventoryOnly); assert(not pcall(function() GiveItemBehavior.InventoryOnly.X=1 end)); " +
            "assert(table.isfrozen(game:GetService('Loot')) and game:GetService('Loot').DropMode==nil)";
    }

    private static void InvalidDeclarations(Runtime.NativeRuntime Native)
    {
        var Calls = new List<string>();
        Action<string> Drops = Text => Calls.Add("Loot:RegisterDropRule(" + Prefab + ",Mode," + Text + ")");
        foreach (string Text in new[] {"nil", "false", "1", "'drops'", "function() S.Touched+=1 end", "Mode", "Vector3.new(1,2,3)",
            "{" + Entry + ",false}", "{{Item='scrap',Min=0}}", "{{Item='scrap',Max=1}}", "{{Min=0,Max=1}}", "{{}}",
            "{'scrap'}", "{Mode}", "{function() S.Touched+=1 end}", "{true}", "{42}", "{Cycle}", "Cycle",
            "{[2]=" + Entry + "}", "{[1]=" + Entry + ",[3]=" + Entry + "}", "{[0]=" + Entry + "}",
            "{[-1]=" + Entry + "}", "{[1.5]=" + Entry + "}", "{[math.huge]=" + Entry + "}",
            "{[1000000]=" + Entry + "}", "{['1']=" + Entry + "}", "{Extra=true}", "{[Mode]=" + Entry + "}",
            "{" + Entry + ",Extra=true}", "setmetatable({},{__index=Touch,__len=Touch,__iter=Touch})",
            "setmetatable({" + Entry + "},{__metatable=false,__index=Touch,__len=Touch,__iter=Touch})",
            "{setmetatable({Item='scrap',Min=0,Max=1},{__metatable='sealed',__index=Touch,__iter=Touch})}",
            "{setmetatable({},{__index=function() S.Touched+=1; return 1 end})}",
            "{{Item='scrap',Min=0,Max=1,Extra=true}}", "{{Item='scrap',Min=0,Max=1,[1]=true}}",
            "{{item='scrap',Min=0,Max=1}}", "{{Item='scrap',Min=0,Max=1,Callback=Touch}}",
            "{{Item='scrap',Min=0,Max=1,Self=Cycle}}",
            "(function() local T={}; for Index=1,9 do T[Index]={Item='scrap',Min=0,Max=0} end; return T end)()"}) Drops(Text);
        foreach (string Item in new[] {"'carbonluau.d8.invalid'", "'Stones'", "'stones.extra'", "' stones'", "'stones\\0'", "'Wood'", "'wood.extra'", "'wood\\0'", "'Scrap'", "' metal.fragments'", "'scrap\\0'", "'scráp'", "''", "string.rep('x',129)",
            "42", "true", "{}", "Mode", "Touch"}) Drops("{{Item=" + Item + ",Min=0,Max=1}}");
        foreach (string Number in new[] {"nil", "'1'", "true", "{}", "Touch", "Mode", "-1", "1001", "0.5", "math.huge", "-math.huge", "0/0", "9007199254740992"}) {
            Drops("{{Item='scrap',Min=" + Number + ",Max=1000}}");
            Drops("{{Item='scrap',Min=0,Max=" + Number + "}}");
        }
        Drops("{{Item='scrap',Min=2,Max=1}}");
        foreach (string Mode in new[] {"nil", "'Replace'", "'DropMode.Replace'", "0", "1", "true", "{}", "Enum.DropMode", "GiveItemBehavior.InventoryOnly",
            "Vector3.new(1,2,3)", "setmetatable({},{__tostring=Touch,__metatable='CarbonLuau.DropMode'})"})
            Calls.Add("Loot:RegisterDropRule(" + Prefab + "," + Mode + ",{})");
        foreach (string Name in new[] {"nil", "1", "true", "{}", "Touch", "''", "'loot-barrel-1'", Prefab + "..'extra'", "string.upper(" + Prefab + ")", Prefab + "..'\\0'"})
            Calls.Add("Loot:RegisterDropRule(" + Name + ",Mode,{})");
        Calls.AddRange(new[] {"Loot:RegisterDropRule()", "Loot:RegisterDropRule(" + Prefab + ",Mode)",
            "Loot:RegisterDropRule(" + Prefab + ",Mode,{},true)", "Loot:RegisterDropRule(" + Prefab + ",Mode,{},nil)",
            "Loot.RegisterDropRule({}," + Prefab + ",Mode,{})", "Loot.RegisterDropRule(" + Prefab + ",Mode,{})"});
        var Code = new StringBuilder(Setup + "S.Touched=0; S.Rejections=0; local Cycle={}; Cycle[1]=Cycle; " +
            "local function Touch() S.Touched+=1; error('metamethod or callback executed') end; " +
            "local function RejectCall(Callback,Name) local Ok,Message=pcall(Callback); " +
            "assert(not Ok and type(Message)=='string' and #Message>0,Name); S.Rejections+=1 end; ");
        for (int Index = 0; Index < Calls.Count; Index++)
            Code.Append("RejectCall(function() ").Append(Calls[Index]).Append(" end,'case ").Append(Index).Append("'); ");
        Code.Append("assert(S.Touched==0 and S.Rejections==").Append(Calls.Count).Append(")");
        using (var Value = new Fixture(Native)) {
            Value.Load(Code.ToString()); Value.None();
            Check(Value.Lookups == 0 && !Value.World.Active.HasLootRule && !Value.Host.HasWork &&
                Value.World.Active.ListenerCount == 0 && Value.World.Active.Commands.Count == 0,
                "all malformed calls fail before host resolution, staging, subscriptions or deferred effects");
            Value.Execute("assert(require('state').Touched==0)");
            // A rejected call does not poison the inactive session's one-rule slot.
            Value.Load(Setup + "assert(not pcall(function() Loot:RegisterDropRule(" + Prefab + ",Mode,{{Item='carbonluau.d8.invalid',Min=0,Max=1}}) end)); " + Register);
            EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 1, 2);
        }
    }

    private static void BackingCapacity(Runtime.NativeRuntime Native)
    {
        // Pinned ltable.cpp rehash/resize can shrink an empty preallocation on
        // string-key insertion. Fill the array before creating record fields;
        // deleting existing array entries and luaH_clear retain that storage.
        // Cleared hash nodes also retain capacity/free slots for reinsertion.
        // The resulting visible shapes are otherwise valid declarations.
        string[] Inputs = {
            "table.create(100000)",
            "table.create(65)",
            "table.freeze(table.create(100000))",
            "(function() local Drops=table.create(100000); Drops[1]=" + Entry + "; return Drops end)()",
            "(function() local Record=table.create(100000,true); Record.Item='scrap'; Record.Min=0; Record.Max=1; " +
                "for Index=1,100000 do Record[Index]=nil end; return {Record} end)()",
            "(function() local Record=table.create(100000,true); Record.Item='scrap'; Record.Min=0; Record.Max=1; table.clear(Record); " +
                "Record.Item='scrap'; Record.Min=0; Record.Max=1; return {table.freeze(Record)} end)()",
            "(function() local Drops={}; for Index=1,128 do Drops['Extra'..Index]=true end; table.clear(Drops); return Drops end)()",
            "(function() local Drops={" + Entry + "}; for Index=1,128 do Drops['Extra'..Index]=true end; " +
                "for Index=1,128 do Drops['Extra'..Index]=nil end; return Drops end)()",
            "(function() local Record=" + Entry + "; for Index=1,128 do Record['Extra'..Index]=true end; " +
                "for Index=1,128 do Record['Extra'..Index]=nil end; return {Record} end)()",
            "(function() local Record={}; for Index=1,128 do Record['Extra'..Index]=true end; table.clear(Record); " +
                "Record.Item='scrap'; Record.Min=0; Record.Max=1; return {table.freeze(Record)} end)()"
        };
        var Code = new StringBuilder(Setup + "S.Rejections=0; local function RejectCapacity(Drops,Name) " +
            "local Ok,Message=pcall(function() Loot:RegisterDropRule(" + Prefab + ",Mode,Drops) end); " +
            "assert(not Ok and type(Message)=='string' and string.find(Message,'table capacity exceeds limit',1,true),Name); " +
            "S.Rejections+=1 end; ");
        for (int Index = 0; Index < Inputs.Length; Index++)
            Code.Append("RejectCapacity(").Append(Inputs[Index]).Append(",'capacity case ").Append(Index).Append("'); ");
        Code.Append("assert(S.Rejections==").Append(Inputs.Length).Append(")");
        using (var Value = new Fixture(Native)) {
            Value.Load(Code.ToString()); Value.None();
            Check(Value.Lookups == 0 && !Value.World.Active.HasLootRule && !Value.Host.HasWork,
                "oversized empty arrays, entry records and cleared/shrunken hash backing reject before any resolver or staging effect");
            // Original failed cases assumed allocation history was preserved
            // through rehash. This actual shrink is legal: guard current backing.
            Value.Load(Setup + "local Record=table.create(100000); Record.Item='scrap'; Record.Min=0; Record.Max=1; " +
                "Loot:RegisterDropRule(" + Prefab + ",Mode,{Record})");
            EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 0, 1);
            Check(Value.Lookups == 1, "empty preallocation shrinks on string insertion and remains a valid declaration");
            Value.Load(Setup + "local Drops=table.create(8); for Index=1,8 do Drops[Index]=table.freeze({Item='scrap',Min=0,Max=1}) end; " +
                "Loot:RegisterDropRule(" + Prefab + ",Mode,table.freeze(Drops))");
            Runtime.LootDropRule Rule = Value.Selected(Value.World.Active);
            Check(Rule.Count == 8 && Rule.MaximumTotal == 8 && Value.Lookups == 9,
                "normal eight-slot dense frozen array remains accepted after controlled capacity rejection");
            for (int Index = 0; Index < 8; Index++) EntryIs(Rule, Index, "scrap", 0, 1);
        }
    }

    private static void Publication(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Modules.Add("failed", Setup + "S.Failures=(S.Failures or 0)+1; " + Register + "error('failed module')");
            Value.Modules.Add("inner", Setup + Register + "return true");
            Value.Modules.Add("outer", "assert(require('inner')); error('failed parent')");
            Value.Modules.Add("caught", "assert(not pcall(require,'failed')); return true");
            Value.Modules.Add("success", Setup + Register + "return Enum.DropMode.Replace");
            Value.Load(Setup + "assert(not pcall(require,'failed')); assert(not pcall(require,'failed')); " +
                "assert(require('caught')); assert(S.Failures==3); assert(not pcall(require,'outer')); " +
                "assert(not pcall(require,'outer')); assert(require('success')==Mode)");
            Runtime.LootDropRule Rule = Value.Selected(Value.World.Active);
            EntryIs(Rule, 0, "scrap", 1, 2);
            Check(Value.Lookups == 6, "failed module cache and successful inner resources roll back before fallback registration");
            int Before = Value.Lookups;
            Value.Execute(Setup + "assert(not pcall(function() " + Register + "end)); " +
                "assert(not pcall(require,'failed')); assert(S.Failures==4); " +
                "task.defer(function() assert(not pcall(function() " + Register + "end)); print('[CarbonLuau:LootPublicCallback] rejected') end)");
            Check(Drain(Value.Host) == "[CarbonLuau:LootPublicCallback] rejected\n" && Value.Lookups == Before,
                "active direct, cold-module and callback calls cannot stage a declaration");
            Check(Object.ReferenceEquals(Value.Selected(Value.World.Active), Rule), "active rejection keeps original snapshot");
            Runtime.FacadeSession Previous = Value.World.Active;
            Runtime.ExecutionResult Failed = Value.Reload(Declaration(9) + "task.defer(function() print('must never run') end); error('failed root')");
            Check(Failed.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Failed.Logs == "" && Value.World.Active == Previous &&
                !Previous.Disposed && Drain(Value.Host) == "", "failed candidate discards declaration and callbacks");
            Check(Object.ReferenceEquals(Value.Selected(Previous), Rule), "failed root preserves exact committed rule");
            Value.Load(Declaration(3) + "assert(not pcall(function() " + Register + "end))");
            EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 3, 3);
        }
    }

    private static void Availability(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.SourceAvailable = false;
            Value.Load("return true"); Value.None();
            foreach (Func<bool> Gate in new Func<bool>[] {null, () => false, () => { throw new InvalidOperationException("source fault"); }}) {
                Value.World.LootAvailable = Gate; int Before = Value.Lookups;
                Value.Load(Setup + "assert(not pcall(function() " + Register + "end))"); Value.None();
                Check(Value.Lookups == Before && !Value.World.Active.HasLootRule, "source rejection before definition resolution");
            }
            Value.World.LootAvailable = () => Value.SourceAvailable; Value.SourceAvailable = true; Value.MissingItem = true;
            Value.Load(Setup + "assert(not pcall(function() " + Register + "end)); " +
                "assert(not pcall(function() Loot:RegisterDropRule(" + Prefab + ",Mode,{{Item='metal.fragments',Min=0,Max=1}}) end))");
            Value.None(); Check(!Value.World.Active.HasLootRule, "missing host definitions stage no rule"); Value.MissingItem = false;
            Value.ObserveLookup = () => Value.SourceAvailable = false;
            Value.Load(Setup + "assert(not pcall(function() " + Register + "end))"); Value.None();
            Check(!Value.World.Active.HasLootRule, "source revalidation after trusted resolver callback");
            Value.ObserveLookup = null; Value.SourceAvailable = true;
            Value.Load(Declaration(4)); Runtime.LootDropRule Rule = Value.Selected(Value.World.Active);
            Value.SourceAvailable = false; Value.None(); Value.SourceAvailable = true;
            Check(Object.ReferenceEquals(Value.Selected(Value.World.Active), Rule), "source restoration selects same immutable committed descriptor");
            Runtime.FacadeSession Previous = Value.World.Active; int SourceChecks = 0, Calls = Value.Registrar.Calls;
            // Staging checks twice; the third check is commit preflight. Loss
            // there must leave the outgoing committed owner untouched.
            Value.World.LootAvailable = () => ++SourceChecks <= 2;
            Runtime.ExecutionResult Lost = Value.Reload(Declaration(7));
            Check(Lost.Status != Runtime.RuntimeStatus.OK && SourceChecks == 3 && Value.World.Active == Previous &&
                !Previous.Disposed && Value.Registrar.Calls == Calls, "source loss at commit rejects before publication");
            Value.World.LootAvailable = () => true;
            Check(Object.ReferenceEquals(Value.Selected(Previous), Rule), "failed source preflight preserves incumbent descriptor");
        }
    }

    private static void RootReplacement(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Load(Declaration(2)); Runtime.FacadeSession Previous = Value.World.Active;
            Runtime.LootDropRule Rule = Value.Selected(Previous); long Vm = Value.Host.VmGenerationId;
            Value.ObserveLookup = () => Check(Object.ReferenceEquals(Value.Selected(Previous), Rule), "incoming root remains invisible during preparation");
            Value.Registrar.Fail = true;
            Runtime.ExecutionResult Failed = Value.Reload(Declaration(8));
            Check(Failed.Status != Runtime.RuntimeStatus.OK && Value.World.Active == Previous && !Previous.Disposed &&
                Object.ReferenceEquals(Value.Selected(Previous), Rule), "failed registrar publication preserves prior owner");
            Value.Registrar.Fail = false; Value.Load(Declaration(8)); Value.ObserveLookup = null;
            Check(Previous.Disposed && !Previous.HasLootRule && Value.Host.VmGenerationId == Vm,
                "healthy root replacement retires old authority within shared VM");
            EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 8, 8);
            EntryIs(Rule, 0, "scrap", 2, 2); // Previously captured occurrence plan is immutable.
            Value.SourceAvailable = false; Value.Load("return true"); Value.SourceAvailable = true; Value.None();
            Check(!Value.World.Active.HasLootRule, "rule-free replacement removes rule despite unavailable source");
            Value.Load(Declaration(5));
            Runtime.FacadeSession BeforeRecovery = Value.World.Active;
            Runtime.ExecutionResult Fatal = Value.Host.Execute("loot.timeout", "while true do end");
            Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && Value.Host.Ready && BeforeRecovery.Disposed &&
                !BeforeRecovery.HasLootRule && Value.Host.VmGenerationId != Vm, "fatal execution retires old VM rule authority");
            EntryIs(Value.Selected(Value.World.Active), 0, "scrap", 5, 5);
            Check(Value.Host.Recoveries == 1 && Drain(Value.Host) == "", "one reconstruction without callback replay");
        }
    }

    private static byte[] Package(string Id, string Source, string Version = "1.0.0", string Api = null, string Required = null)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Action<string, string> Write = (Name, Text) => {
                    using (var Writer = new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Text);
                };
                Write("addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"" + Version + "\"" +
                    (Api == null ? "" : ",\"publicModules\":[\"api\"]") +
                    (Required == null ? "" : ",\"dependencies\":{\"required\":[\"" + Required + "\"],\"optional\":[]}") + "}");
                Write("init.luau", Source); Write("state.luau", "return {}");
                if (Api != null) Write("api.luau", Api);
            }
            return Output.ToArray();
        }
    }
    private static void Activate(Runtime.AddonRegistry Registry)
    {
        int Guard = 256;
        while (Registry.HasPending && Guard-- > 0) Check(Registry.ProcessOne(), "eligible addon activation advances");
        Check(!Registry.HasPending, "bounded addon activation converges");
    }
    private static string Add(Runtime.AddonRegistry Registry, object Provider, byte[] Archive, string Expected)
    {
        string[] Result = Registry.RegisterArchive(Provider, Archive);
        Check(Result[0] == "OK", "addon registration: " + String.Join("|", Result));
        Activate(Registry); Status(Registry, Provider, Result[1], Expected); return Result[1];
    }
    private static string[] Status(Runtime.AddonRegistry Registry, object Provider, string Token, string Expected)
    {
        string[] Result = Registry.Status(Provider, Token);
        Check(Result[0] == "OK" && Result[2] == Expected, "addon state: " + String.Join("|", Result)); return Result;
    }
    private static void Replace(Runtime.AddonRegistry Registry, object Provider, string Token, byte[] Archive)
    {
        string[] Result = Registry.ReplaceArchive(Provider, Token, Archive);
        Check(Result[0] == "OK", "addon replacement: " + String.Join("|", Result)); Activate(Registry);
    }

    private static void AddonOwnership(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Load(Declaration(2));
            using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
                object Provider = new object(), OtherProvider = new object();
                Runtime.FacadeSession Root = Value.World.Active; Runtime.LootDropRule RootRule = Value.Selected(Root);
                int Calls = Value.Registrar.Calls;
                string Collision = Add(Registry, Provider, Package("lootcollision", Declaration(3)), "Failed");
                Check(Value.Registrar.Calls == Calls && Value.Host.DomainCount == 1 && Object.ReferenceEquals(Value.Selected(Root), RootRule),
                    "root/addon conflict rejected before registrar/native publication");
                Check(Registry.Unregister(Provider, Collision)[0] == "OK", "failed collision registration removed");
                Value.Load("return true"); Value.None();
                string OwnerToken = Add(Registry, Provider, Package("lootowner", Declaration(4)), "Active");
                Runtime.FacadeSession Owner; Runtime.LootDropRule OldRule = Value.Selected(out Owner);
                Calls = Value.Registrar.Calls;
                string Other = Add(Registry, OtherProvider, Package("lootother", Declaration(6)), "Failed");
                Runtime.ExecutionResult RootConflict = Value.Reload(Declaration(7));
                Check(RootConflict.Status != Runtime.RuntimeStatus.OK && Value.Registrar.Calls == Calls &&
                    Object.ReferenceEquals(Value.Selected(Owner), OldRule), "addon/addon and addon/root conflicts leave incumbent intact");
                Value.Source = "return true";
                Replace(Registry, Provider, OwnerToken, Package("lootowner", Declaration(9) + "error('failed addon')", "2.0.0"));
                Check(Status(Registry, Provider, OwnerToken, "Active")[5].Contains("replacement failed") &&
                    !Owner.Disposed && Object.ReferenceEquals(Value.Selected(Owner), OldRule), "failed addon replacement retains exact old plan");
                Value.ObserveLookup = () => Check(Object.ReferenceEquals(Value.Selected(Owner), OldRule), "replacement staging does not switch incumbent");
                Replace(Registry, Provider, OwnerToken, Package("lootowner", Declaration(8), "3.0.0")); Value.ObserveLookup = null;
                Status(Registry, Provider, OwnerToken, "Active");
                Runtime.FacadeSession Next; Runtime.LootDropRule NextRule = Value.Selected(out Next);
                Check(Owner.Disposed && !Owner.HasLootRule && Next != Owner, "same addon outgoing owner can replace its rule");
                EntryIs(NextRule, 0, "scrap", 8, 8); EntryIs(OldRule, 0, "scrap", 4, 4);
                Check(Registry.UnloadProvider(Provider) == 1 && Next.Disposed && !Next.HasLootRule, "provider retirement removes selection authority"); Value.None();
                Replace(Registry, OtherProvider, Other, Package("lootother", Declaration(6), "2.0.0"));
                Status(Registry, OtherProvider, Other, "Active"); Runtime.FacadeSession Reclaimed;
                EntryIs(Value.Selected(out Reclaimed), 0, "scrap", 6, 6);
                Check(Registry.UnloadProvider(OtherProvider) == 1 && Reclaimed.Disposed, "conflicted registration can claim released prefab"); Value.None();

                // A foreign active facade cannot launder declaration authority
                // into a consumer's provisional scope; explicit consumer ownership can.
                string Api = "local Loot=game:GetService('Loot'); local Saved; local Calls=0; return {Mode=Enum.DropMode.Replace,Give=GiveItemBehavior.InventoryOnly," +
                    "Declare=function() Loot:RegisterDropRule(" + Prefab + ",Enum.DropMode.Replace,{}) end," +
                    "DeclareFor=function(Target,Mode) Calls+=1; Saved=Target; Target:RegisterDropRule(" + Prefab + ",Mode,{}) end," +
                    "DeclareSaved=function() Saved:RegisterDropRule(" + Prefab + ",Enum.DropMode.Replace,{}) end," +
                    "Count=function() return Calls end}";
                Add(Registry, Provider, Package("lootenums", "assert(require('api'))", Api: Api), "Active");
                int Before = Value.Lookups;
                Add(Registry, OtherProvider, Package("lootconsumer", "local Api=require('@lootenums/api'); " + EnumChecks() +
                    "; assert(Api.Mode==Enum.DropMode.Replace and Enum.DropMode.Replace==Api.Mode); " +
                    "assert(Api.Give==GiveItemBehavior.InventoryOnly); assert(not pcall(Api.Declare)); " +
                    "Api.DeclareFor(game:GetService('Loot'),Api.Mode)", Required: "lootenums"), "Active");
                Runtime.FacadeSession Consumer; Runtime.LootDropRule Empty = Value.Selected(out Consumer);
                Check(Empty.Count == 0 && Value.Lookups == Before && Registry.Count == 2, "cross-domain enum accepted with explicit consumer facade");
                Check(Registry.UnloadProvider(OtherProvider) == 1 && Consumer.Disposed, "consumer rule retires independently"); Value.None();
                Add(Registry, OtherProvider, Package("lootfailedconsumer", "local Api=require('@lootenums/api'); " +
                    "assert(not pcall(Api.DeclareSaved)); assert(Api.Count()==1); " +
                    "Api.DeclareFor(game:GetService('Loot'),Api.Mode); error('failed foreign parent')", Required: "lootenums"), "Failed");
                Value.None();
                Add(Registry, OtherProvider, Package("lootretiredconsumer", "local Api=require('@lootenums/api'); " +
                    "assert(Api.Count()==2); assert(not pcall(Api.DeclareSaved)); " +
                    "Api.DeclareFor(game:GetService('Loot'),Api.Mode)", Required: "lootenums"), "Active");
                Runtime.FacadeSession LastConsumer;
                Check(Value.Selected(out LastConsumer).Count == 0 && Value.Lookups == Before,
                    "retired and failed-candidate facades reject; ordinary foreign Lua mutations persist under D7");
                Check(Registry.UnloadProvider(OtherProvider) == 2 && LastConsumer.Disposed, "failed and active consumer registrations released"); Value.None();
                Check(Registry.UnloadProvider(Provider) == 1, "enum provider released");
            }
        }
    }

    private static void ScaleAndRecovery(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Load("return true");
            using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
                object[] Providers = {new object(), new object(), new object(), new object()};
                var Tokens = new List<string>();
                for (int Index = 0; Index < 100; Index++) {
                    string Id = "lootstress" + Index.ToString("D3", CultureInfo.InvariantCulture);
                    string Source = Index == 0 ? StonesDeclaration(Prefab, 3) : Index == 1 ? StonesDeclaration(Prefab2, 4) : EnumChecks() +
                        "; task.defer(function() local Loot=game:GetService('Loot'); assert(not pcall(function() Loot:RegisterDropRule(" +
                        Prefab + ",Enum.DropMode.Replace,{}) end)) end)";
                    string[] Result = Registry.RegisterArchive(Providers[Index / 25], Package(Id, Source));
                    Check(Result[0] == "OK", "100 real addon registrations within provider envelope"); Tokens.Add(Result[1]);
                }
                Activate(Registry);
                for (int Index = 0; Index < 100; Index++) Status(Registry, Providers[Index / 25], Tokens[Index], "Active");
                Check(Registry.Count == 100 && Value.Host.DomainCount == 101 && Native.LiveVmCount == 1 && Value.World.Sessions().Count == 101,
                    "one actual VM contains root plus 100 addon domains");
                Runtime.FacadeSession Owner; Runtime.LootDropRule Rule = Value.Selected(out Owner);
                Runtime.FacadeSession SecondOwner; var SecondRule = Value.Selected(Barrel2, out SecondOwner);
                Check(Owner != SecondOwner, "100-addon population retains independent exact owners for the two prefabs");
                Check(Drain(Value.Host) == "", "98 addon callbacks reject post-publication declarations without effects");
                for (int Index = 0; Index < 100; Index++)
                    Check(Object.ReferenceEquals(Value.Selected(Owner), Rule), "bounded selection retains one snapshot across real addon population");
                int Rules = 0; foreach (Runtime.FacadeSession Session in Value.World.Sessions()) if (Session.HasLootRule) Rules++;
                Check(Rules == 2 && Value.World.LootRetainedSessionCount == 2 && Value.Host.VmMemoryBytes <= Value.Host.VmMemoryLimitBytes,
                    "two committed owners retain shared VM memory and global publication bounds");
                long Vm = Value.Host.VmGenerationId;
                Runtime.ExecutionResult Fatal = Value.Host.Execute("loot.scale.timeout", "while true do end");
                Check(Fatal.Status == Runtime.RuntimeStatus.TIMEOUT && Value.Host.Ready && Value.Host.VmGenerationId != Vm &&
                    Owner.Disposed && !Owner.HasLootRule, "fatal shared VM retirement clears addon rule before reconstruction");
                Value.None(); Activate(Registry);
                for (int Index = 0; Index < 100; Index++) Status(Registry, Providers[Index / 25], Tokens[Index], "Active");
                Runtime.FacadeSession Reconstructed; Runtime.LootDropRule ReconstructedRule = Value.Selected(out Reconstructed);
                Runtime.FacadeSession SecondReconstructed; var SecondReconstructedRule = Value.Selected(Barrel2, out SecondReconstructed);
                Check(Reconstructed != Owner && ReconstructedRule != Rule && Value.Host.DomainCount == 101 && Native.LiveVmCount == 1,
                    "all 100 live registrations reconstruct into fresh exact authority");
                EntryIs(ReconstructedRule, 0, "stones", 3, 3); EntryIs(Rule, 0, "stones", 3, 3); Drain(Value.Host);
                Check(SecondReconstructed != SecondOwner && SecondReconstructedRule != SecondRule && SecondOwner.Disposed,
                    "second prefab reconstructs into its own fresh exact domain");
                EntryIs(SecondReconstructedRule, 0, "stones", 4, 4);
                Check(Value.Host.Recoveries == 1, "addon reconstruction never rearms global recovery allowance");
                foreach (object Provider in Providers) Check(Registry.UnloadProvider(Provider) == 25, "each provider retires 25 actual addons");
                Value.None(); Check(Registry.Count == 0 && Value.Host.DomainCount == 1 && !Value.Host.HasWork,
                    "100-addon teardown leaves only root with no rule or callback work");
            }
        }
    }
}
