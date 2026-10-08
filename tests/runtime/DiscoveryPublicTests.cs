using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class DiscoveryPublicTests
{
    [DllImport("libc", SetLastError = true)] private static extern int chmod(string Path, int Mode);
    private static void Check(bool Good, string Message) { if (!Good) throw new Exception(Message); }
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private sealed class Entry
    {
        internal Runtime.FacadeSession Session;
        internal string Route;
        internal string[] Fields;
        internal ulong VmEpoch;
        internal bool Ready;
    }
    // The production facade and real VM run against a controlled bounded host
    // transport. Catalog/lifetime qualification belongs to the managed model
    // and live fixtures; this test controls the readiness-to-admission race.
    private sealed class DiscoveryHost : Carbon.Plugins.IEntityDiscoveryFacadeHost, Carbon.Plugins.IEntityFacadeHost
    {
        internal readonly List<Entry> History = new List<Entry>();
        internal readonly List<Entry> Held = new List<Entry>();
        internal readonly Runtime.NativeRuntime Native;
        internal int Fetches, Releases, Lookups, Reads, ResultCount = 2;
        internal string Error;
        internal DiscoveryHost(Runtime.NativeRuntime Native) { this.Native = Native; }
        public string[] Submit(Runtime.FacadeSession Session, string[] Fields)
        {
            Check(Session.Active && !Session.Disposed && Session.FacadeVm != 0 && Fields.Length == 7,
                "production submit session/VM and exact fields");
            Check(Held.Count < 8, "managed global bound");
            int Count = 0; foreach (Entry Value in Held) if (Value.Session == Session) Count++;
            Check(Count < 2, "managed session bound");
            for (int Index = 1; Index <= 4; Index++) {
                double Value;
                Check(Fields[Index].Length <= 32 && Double.TryParse(Fields[Index], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out Value) && !Double.IsNaN(Value) && !Double.IsInfinity(Value),
                    "invariant bounded numeric snapshot");
            }
            var Entry = new Entry {Session = Session, Route = Fields[0], Fields = (string[])Fields.Clone(), VmEpoch = Session.FacadeVm};
            Held.Add(Entry); History.Add(Entry); return new string[0];
        }
        public string[] Admit(Runtime.FacadeSession Session, string Route)
        {
            Entry Entry = Find(Session, Route);
            Check(Entry != null && Entry.Ready && Session.Active && !Session.Disposed,
                "actual managed admission has exact live owner/readiness");
            Fetches++;
            if (Error != null) return new[] {Error};
            var Fields = new List<string> {""};
            var Witness = Session.CaptureEntityWitness();
            for (int Index = 0; Index < ResultCount; Index++) {
                Fields.Add("77"); Fields.Add((41 + Index).ToString(CultureInfo.InvariantCulture));
                Fields.Add((1 + Index).ToString(CultureInfo.InvariantCulture));
                Fields.Add(Witness.Token.ToString(CultureInfo.InvariantCulture));
            }
            return Fields.ToArray();
        }
        private Entry Find(Runtime.FacadeSession Session, string Route)
        { foreach (Entry Entry in Held) if (Entry.Session == Session && Entry.Route == Route) return Entry; return null; }
        public void Release(Runtime.FacadeSession Session, string Route)
        { Entry Entry = Find(Session, Route); if (Entry != null) { Held.Remove(Entry); Releases++; } }
        public void Retire(Runtime.FacadeSession Session)
        { for (int Index = Held.Count - 1; Index >= 0; Index--) if (Held[Index].Session == Session) { Held.RemoveAt(Index); Releases++; } }
        internal Runtime.RuntimeStatus Ready(int Index)
        {
            Entry Entry = History[Index];
            var Result = Native.DomainEvent(Entry.VmEpoch, (ulong)Entry.Session.DomainLifetimeId,
                Encoding.UTF8.GetBytes("discovery\0" + Entry.Route + "\0"));
            if (Result == Runtime.RuntimeStatus.OK) Entry.Ready = true;
            return Result;
        }
        public string[] Lookup(Runtime.FacadeSession Session, string Id)
        {
            Lookups++; var Witness = Session.CaptureEntityWitness();
            return new[] {"77", "41", "1", Witness.Token.ToString(CultureInfo.InvariantCulture)};
        }
        public string[] Read(Runtime.FacadeSession Session, string Token, string Publication, string Property)
        {
            Reads++;
            ulong Witness = UInt64.Parse(Publication, CultureInfo.InvariantCulture);
            if (Session.Disposed || Session.FindEntityWitness(Witness) == null)
                throw new Runtime.FacadeException("stale Entity reference");
            if (Property == "Id") return new[] {(Int32.Parse(Token, CultureInfo.InvariantCulture) - 40).ToString(CultureInfo.InvariantCulture)};
            if (Property == "Prefab") return new[] {"assets/fixture.prefab"};
            if (Property == "Position") return new[] {"1.25", "-2.5", "3.75"};
            throw new Runtime.FacadeException("unknown Entity property");
        }
    }
    private static byte[] Package(string Manifest, string Init, string Api = null)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Action<string,string> Add = (Name, Source) => {
                    using (var Writer = new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Source);
                };
                Add("addon.json", Manifest); Add("init.luau", Init); if (Api != null) Add("api.luau", Api);
            }
            return Output.ToArray();
        }
    }
    private static void Execute(Runtime.ScriptHost Host, string Source)
    {
        var Result = Host.Execute("discovery.public", Source);
        Check(Result.Status == Runtime.RuntimeStatus.OK, "real VM public discovery: " + Result.Error);
    }
    private static void Drain(Runtime.ScriptHost Host)
    {
        for (int Index = 0; Index < 8 && Host.HasReadyWork; Index++)
            foreach (var Result in Host.Drain()) Check(Result.Status == Runtime.RuntimeStatus.OK, "discovery callback: " + Result.Error);
        Check(!Host.HasReadyWork, "bounded drain finished");
    }
    private static void Process(Runtime.AddonRegistry Registry)
    {
        for (int Index = 0; Index < 64 && Registry.HasPending; ++Index) Registry.ProcessOne();
        Check(!Registry.HasPending, "closure graph work converged");
    }
    private static Runtime.FacadeSession Session(Runtime.FacadeWorld World, string[] Status)
    {
        Check(Status[0] == "OK" && Status[2] == "Active", "closure addon active: " + String.Join("|", Status));
        foreach (var Value in World.Sessions())
            if (Value.DomainLifetimeId.ToString(CultureInfo.InvariantCulture) == Status[7]) return Value;
        throw new Exception("closure exact addon session missing");
    }
    private static void ExecuteDomain(Runtime.NativeRuntime Native, Runtime.FacadeSession Session, string Source)
    {
        var Result = Native.DomainExecute(Session.FacadeVm, (ulong)Session.DomainLifetimeId, "discovery.closure", Source, 100);
        Check(Result.Status == Runtime.RuntimeStatus.OK, "closure domain execution: " + Result.Error);
    }
    private static byte[] ClosurePackage(string Id, string Kind, string Source, bool Export = false)
    {
        string Dependencies = Kind == null ? "" : ",\"dependencies\":{\"required\":[" +
            (Kind == "required" ? "\"closureowner\"" : "") + "],\"optional\":[" +
            (Kind == "optional" ? "\"closureowner\"" : "") + "]}";
        return Package("{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" + Dependencies +
            (Export ? ",\"publicModules\":[\"api\"]" : "") + "}", Source,
            Export ? "local W=game:GetService('Workspace'); return {Read=function() return 23 end," +
                "Borrowed=function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end}" : null);
    }
    private static void RunAddonClosure(Runtime.ScriptHost Host, Runtime.NativeRuntime Native,
        Runtime.FacadeWorld World, DiscoveryHost Discovery)
    {
        const string Consumer = "local A=require('@closureowner/api'); assert(A.Read()==23); " +
            "assert(not pcall(A.Borrowed)); task.defer(function() assert(A.Read()==23 and not pcall(A.Borrowed)) end)";
        const string Query = "local W=game:GetService('Workspace'); for I=1,2 do " +
            "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,R) assert(E and #E==2 and R==nil) end) end";
        using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
            object OwnerProvider = new object(), Consumers = new object();
            string[] Owner = Registry.RegisterArchive(OwnerProvider, ClosurePackage("closureowner", null, "return", true));
            Process(Registry);
            string[] Required = Registry.RegisterArchive(Consumers, ClosurePackage("closurerequired", "required", Consumer));
            string[] Optional = Registry.RegisterArchive(Consumers, ClosurePackage("closureoptional", "optional", Consumer));
            Process(Registry); Drain(Host);
            var RequiredSession = Session(World, Registry.Status(Consumers, Required[1]));
            var OptionalSession = Session(World, Registry.Status(Consumers, Optional[1]));
            Check(Discovery.Held.Count == 0, "public cold/provisional/foreign facade calls publish no query");
            int First = Discovery.History.Count;
            ExecuteDomain(Native, RequiredSession, Query); ExecuteDomain(Native, OptionalSession, Query);
            Check(Discovery.Held.Count == 4 && Discovery.Ready(First) == Runtime.RuntimeStatus.OK &&
                Discovery.Ready(First + 2) == Runtime.RuntimeStatus.OK, "two addon scanning and ready captures");
            Check(Registry.ReplaceArchive(Consumers, Required[1], ClosurePackage("closurerequired", "required",
                Consumer + "; error('failed candidate')"))[0] == "OK", "failed replacement accepted for qualification");
            Process(Registry);
            Check(ReferenceEquals(RequiredSession, Session(World, Registry.Status(Consumers, Required[1]))) &&
                Discovery.Held.Count == 4, "failed addon replacement preserves all old query captures");
            Check(Registry.ReplaceArchive(Consumers, Required[1], ClosurePackage("closurerequired", "required", Consumer))[0] == "OK",
                "successful addon replacement accepted");
            Process(Registry); Drain(Host);
            Check(RequiredSession.Disposed && Discovery.Held.Count == 1 &&
                Discovery.Ready(First) == Runtime.RuntimeStatus.INVALID_ARGUMENT, "successful addon replacement discards its ready/scanning work only");
            Check(Discovery.Ready(First + 3) == Runtime.RuntimeStatus.OK, "unrelated optional scanning request survives"); Drain(Host);
            RequiredSession = Session(World, Registry.Status(Consumers, Required[1]));
            First = Discovery.History.Count;
            ExecuteDomain(Native, RequiredSession, Query); ExecuteDomain(Native, OptionalSession, Query);
            Check(Discovery.Ready(First) == Runtime.RuntimeStatus.OK && Discovery.Ready(First + 2) == Runtime.RuntimeStatus.OK,
                "dependency replacement barrier");
            Check(Registry.ReplaceArchive(OwnerProvider, Owner[1], ClosurePackage("closureowner", null, "return", true))[0] == "OK",
                "same-provider dependency replacement accepted");
            Process(Registry); Drain(Host);
            Check(RequiredSession.Disposed && !ReferenceEquals(RequiredSession, Session(World, Registry.Status(Consumers, Required[1]))) &&
                ReferenceEquals(OptionalSession, Session(World, Registry.Status(Consumers, Optional[1]))) &&
                Registry.BindingStatus(Consumers, Optional[1], "closureowner")[1] == "stale" && Discovery.Held.Count == 1,
                "required reconstructs; optional exact binding stays stale while its own query survives");
            ExecuteDomain(Native, OptionalSession, "assert(not addon:IsDependencyAvailable('closureowner')); " +
                "assert(not pcall(function() require('@closureowner/api') end))");
            Check(Discovery.Ready(First + 3) == Runtime.RuntimeStatus.OK, "optional completion remains its own authority"); Drain(Host);
            RequiredSession = Session(World, Registry.Status(Consumers, Required[1]));
            First = Discovery.History.Count; ExecuteDomain(Native, RequiredSession, Query);
            Check(Discovery.Ready(First) == Runtime.RuntimeStatus.OK, "provider loss with ready and scanning work");
            Check(Registry.UnloadProvider(OwnerProvider) == 1 && RequiredSession.Disposed && Discovery.Held.Count == 0 &&
                Registry.Status(Consumers, Required[1])[2] == "Blocked", "required dependency loss retires pending query authority");
            Check(Registry.Status(OwnerProvider, Owner[1])[0] == "ERROR", "old provider token rejects after unload");
            Owner = Registry.RegisterArchive(OwnerProvider, ClosurePackage("closureowner", null, "return", true));
            Process(Registry); Drain(Host);
            RequiredSession = Session(World, Registry.Status(Consumers, Required[1]));
            ExecuteDomain(Native, RequiredSession, "assert(addon:IsDependencyAvailable('closureowner')); " +
                "assert(require('@closureowner/api').Read()==23)");
            Check(Registry.BindingStatus(Consumers, Optional[1], "closureowner")[1] == "stale",
                "explicit provider restoration cannot hot-rebind optional consumer");
            for (int Cycle = 0; Cycle < 8; ++Cycle) {
                First = Discovery.History.Count; ExecuteDomain(Native, RequiredSession, Query);
                Check(Discovery.Ready(First) == Runtime.RuntimeStatus.OK, "repeated replacement ready barrier");
                Check(Registry.ReplaceArchive(Consumers, Required[1], ClosurePackage("closurerequired", "required", Consumer))[0] == "OK",
                    "repeated replacement accepted"); Process(Registry); Drain(Host);
                Check(RequiredSession.Disposed && Discovery.Held.Count == 0 &&
                    Discovery.Ready(First) == Runtime.RuntimeStatus.INVALID_ARGUMENT, "repeated retirement releases all captures without replay");
                RequiredSession = Session(World, Registry.Status(Consumers, Required[1]));
            }
            First = Discovery.History.Count; ExecuteDomain(Native, RequiredSession, Query);
            Check(Discovery.Ready(First) == Runtime.RuntimeStatus.OK && Registry.UnloadProvider(Consumers) == 2 &&
                Discovery.Held.Count == 0, "consumer provider unload clears ready/scanning work");
            Drain(Host); Check(Discovery.Ready(First) == Runtime.RuntimeStatus.INVALID_ARGUMENT, "late provider readiness rejects");
        }
        Check(Discovery.Held.Count == 0 && Host.DomainCount == 1 && Host.SchedulerSnapshot.Queued == 0,
            "combined addon/provider closure converges to root-only baseline");
        Console.WriteLine("[CarbonLuau:DiscoveryClosure] PASS failed/successful/repeated addon replacement, exact shared modules, " +
            "required loss/restoration, optional no-rebind, provider unload and zero retained captures");
    }
    private static void RunExample(Runtime.ScriptHost Host, DiscoveryHost Discovery)
    {
        int Start = Discovery.History.Count, Fetches = Discovery.Fetches;
        string PathValue = Path.Combine(Environment.CurrentDirectory, "examples", "world", "discovery", "init.luau");
        Execute(Host, File.ReadAllText(PathValue));
        Check(Discovery.History.Count == Start, "example defers submission through native scheduling");
        Drain(Host);
        Check(Discovery.History.Count == Start + 1 && Discovery.Held.Count == 1 &&
            Discovery.History[Start].Fields[4] == "25" && Discovery.History[Start].Fields[6] == "32" &&
            Discovery.History[Start].Fields[5] == "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
            "shipped example submits exact bounded snapshot through production binding");
        Check(Discovery.Ready(Start) == Runtime.RuntimeStatus.OK, "example completion ready");
        var Results = Host.Drain();
        var Logs = new StringBuilder();
        foreach (var Result in Results) {
            Check(Result.Status == Runtime.RuntimeStatus.OK, "example callback: " + Result.Error);
            Logs.Append(Result.Logs);
        }
        Check(Discovery.Fetches == Fetches + 1 && Discovery.Held.Count == 0 && Host.SchedulerSnapshot.Queued == 0 &&
            Logs.ToString().Contains("[CarbonLuau:Discovery] Matching entities") &&
            Logs.ToString().Contains("[CarbonLuau:Discovery] Result"),
            "actual example callback and exact Entity property bindings execute");
        Console.WriteLine("[CarbonLuau:DiscoveryExample] PASS pinned compiler/VM and controlled host binding; no live position evidence");
    }
    private static void RunStress(Runtime.ScriptHost Host, DiscoveryHost Discovery)
    {
        const int Cycles = 1000, Batch = 64;
        var Routes = new HashSet<Tuple<ulong,ulong>>();
        var LastRoutes = new Dictionary<ulong,ulong>();
        foreach (Entry Entry in Discovery.History) {
            ulong Route = UInt64.Parse(Entry.Route, CultureInfo.InvariantCulture);
            Check(Routes.Add(Tuple.Create(Entry.VmEpoch, Route)), "existing route is unique within VM epoch");
            LastRoutes[Entry.VmEpoch] = Route;
        }
        Execute(Host, "require('state').StressCount=0");
        int Fetches = Discovery.Fetches, Releases = Discovery.Releases, Lookups = Discovery.Lookups;
        ulong Minimum = UInt64.MaxValue, Maximum = 0;
        var Watch = Stopwatch.StartNew();
        for (int Cycle = 0; Cycle < Cycles; Cycle++) {
            int Index = Discovery.History.Count;
            Execute(Host, "local S=require('state'); game:GetService('Workspace'):GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,Err) " +
                "assert(E and not Err and #E==2); S.StressCount+=1 end)");
            Check(Discovery.History.Count == Index + 1 && Discovery.Held.Count == 1, "stress holds exactly one accepted capture");
            Entry Entry = Discovery.History[Index];
            ulong Route = UInt64.Parse(Entry.Route, CultureInfo.InvariantCulture), Previous;
            Check(Route != 0 && Routes.Add(Tuple.Create(Entry.VmEpoch, Route)) &&
                (!LastRoutes.TryGetValue(Entry.VmEpoch, out Previous) || Route > Previous),
                "stress route never reused or rewound within exact VM epoch");
            LastRoutes[Entry.VmEpoch] = Route;
            Check(Discovery.Ready(Index) == Runtime.RuntimeStatus.OK, "stress readiness accepted once");
            Drain(Host);
            Check(Discovery.Held.Count == 0, "stress callback releases host capture");
            if ((Cycle + 1) % Batch == 0 || Cycle + 1 == Cycles) {
                Check(Host.Ready && Host.SchedulerSnapshot.Queued == 0 && Discovery.Held.Count == 0,
                    "each bounded stress batch converges to zero native/host queued captures");
                Check(Discovery.Ready(Index) == Runtime.RuntimeStatus.INVALID_ARGUMENT, "completed stress route cannot replay");
                ulong Bytes = Host.VmMemoryBytes; Minimum = Math.Min(Minimum, Bytes); Maximum = Math.Max(Maximum, Bytes);
            }
        }
        Execute(Host, "assert(require('state').StressCount==1000)");
        Check(Discovery.Fetches == Fetches + Cycles && Discovery.Releases == Releases + Cycles &&
            Discovery.Lookups == Lookups && Discovery.Held.Count == 0 && Host.SchedulerSnapshot.Queued == 0,
            "all 1000 public cycles admitted/released once without keyed conversion lookup");
        Console.WriteLine("[CarbonLuau:DiscoveryPublicStress] PASS controlled host; Cycles=" + Cycles +
            "; Batch=" + Batch + "; Held=0; NativeQueued=0; RouteReuse=0; NativeHeapObserved=" + Minimum + ".." + Maximum +
            "; ElapsedMs=" + Watch.ElapsedMilliseconds + "; no host position/catalog simulation qualification");
    }
    internal static void Run(string Library, string Compiler)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauDiscovery2B-" + Guid.NewGuid().ToString("N"));
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(Library, Target);
            string CompilerTarget = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, CompilerTarget); if (Rid == "linux-x64") Check(chmod(CompilerTarget, 493) == 0, "compiler executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) {
                Check(Native.AbiVersion == 0x00010005, "discovery preserves ABI 1.5");
                var Discovery = new DiscoveryHost(Native);
                var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
                World.Discovery = Discovery; World.Entities = Discovery;
                string Source = "local S=require('state'); S.W=game:GetService('Workspace'); " +
                    "assert(not pcall(function() S.W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end))";
                Func<Runtime.ScriptSnapshot> Snapshot = () => {
                    var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
                    Value.Modules.Add("state", "return {}");
                    Value.Modules.Add("cold", "local W=game:GetService('Workspace'); " +
                        "assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end)); " +
                        "return function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end");
                    return Value;
                };
                using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {
                    MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 100, MaxQueuedCallbacks = 2}, Snapshot, World)) {
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Discovery.Held.Count == 0,
                        "provisional startup cannot submit");
                    Execute(Host, "local S=require('state'); local W=S.W; local Hits=0; " +
                        "for _,O in {{Unknown=1},{Limit=0},{Limit=257},{Limit=1.5},{Limit='1'},{Prefab=''},{Prefab='a\\0b'}," +
                        "{Prefab=string.rep('a',513)},{Prefab=string.char(255)},setmetatable({},{__index=function() Hits+=1 end})," +
                        "setmetatable({Limit=2},{__metatable=false})} do " +
                        "assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end,O) end)) end; " +
                        "assert(not pcall(function() W:GetEntitiesInRadiusAsync({X=0,Y=0,Z=0},1,function() end) end)); " +
                        "assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),-1,function() end) end)); assert(Hits==0); " +
                        "local O={Prefab='assets/é.prefab',Limit=256}; " +
                        "assert(select('#',W:GetEntitiesInRadiusAsync(Vector3.new(1.25,-2.5,3.75),4,function(E,Err) " +
                        "assert(E and not Err and #E==2 and E[1].Id=='1' and E[2].Id=='2'); " +
                        "assert(E[1].Position==Vector3.new(1.25,-2.5,3.75)); S.E=E[1]; S.Done=true end,O))==0); " +
                        "O.Prefab='changed'; O.Limit=1; assert(not S.Done)");
                    Check(Discovery.History.Count == 1 && Discovery.History[0].Fields[5] == "assets/é.prefab" &&
                        Discovery.History[0].Fields[6] == "256" && Discovery.Fetches == 0, "immutable snapshot contract");
                    Execute(Host, "task.defer(function() end); task.defer(function() end)");
                    Check(Discovery.Ready(0) == Runtime.RuntimeStatus.OK && Discovery.Ready(0) == Runtime.RuntimeStatus.INVALID_ARGUMENT,
                        "fixed readiness survives ordinary saturation; duplicate rejected");
                    Check(Host.Ready && Host.HasWork && Host.HasReadyWork && Discovery.Fetches == 0,
                        "ready reservation visible through ScriptHost");
                    Drain(Host); Execute(Host, "assert(require('state').Done)");
                    Check(Discovery.Fetches == 1 && Discovery.Lookups == 0 && Discovery.Held.Count == 0 && Discovery.Releases == 1,
                        "actual admission fetch, exact factory without retargeting, release once");
                    int Reads = Discovery.Reads;
                    Execute(Host, "local S=require('state'); assert(S.E==S.W:GetEntityById('1'))");
                    Check(Discovery.Lookups == 1 && Discovery.Reads == Reads,
                        "exact public equality with one explicit keyed lookup, no host property access");
                    Execute(Host, "local S=require('state'); S.Done=nil; S.W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,Err) " +
                        "assert(E==nil and Err=='DiscoveryStaleResult'); S.Stale=true end)");
                    Check(Discovery.Ready(1) == Runtime.RuntimeStatus.OK, "queued stale fixture ready");
                    Discovery.Error = "DiscoveryStaleResult"; Drain(Host); Discovery.Error = null;
                    Execute(Host, "assert(require('state').Stale and not require('state').Done)");
                    Execute(Host, "require('cold')"); Check(Discovery.History.Count == 2, "first-load module cannot launder submission");
                    Execute(Host, "require('cold')()"); Check(Discovery.Held.Count == 1, "cached own-domain export can submit later");
                    Check(Discovery.Ready(2) == Runtime.RuntimeStatus.OK, "cached export ready"); Drain(Host);
                    Execute(Host, "local S=require('state'); for I=1,2 do S.W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() error('retired callback') end) end; " +
                        "assert(not pcall(function() S.W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end))");
                    Check(Discovery.Held.Count == 2 && Discovery.History.Count == 5, "two reservations remain held through traversal/readiness");
                    Check(Discovery.Ready(3) == Runtime.RuntimeStatus.OK, "queued retirement fixture");
                    int Before = Discovery.Fetches;
                    Source = "error('failed candidate')";
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Discovery.Held.Count == 2,
                        "failed candidate preserves committed captures");
                    Source = "local S=require('state'); S.W=game:GetService('Workspace')";
                    Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Discovery.Held.Count == 0,
                        "successful replacement cancels traversal/queued captures");
                    Drain(Host); Check(Discovery.Fetches == Before && Discovery.Ready(3) == Runtime.RuntimeStatus.INVALID_ARGUMENT,
                        "late old-domain readiness rejected without replay");
                    using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                        object Provider = new object();
                        string[] Owner = Registry.RegisterArchive(Provider, Package(
                            "{\"schema\":1,\"id\":\"discoverowner\",\"version\":\"1.0.0\",\"publicModules\":[\"api\"]}",
                            "return true", "local W=game:GetService('Workspace'); return function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end"));
                        Check(Owner[0] == "OK" && Registry.ProcessOne() && Registry.Status(Provider, Owner[1])[2] == "Active", "provider activates");
                        int Accepted = Discovery.History.Count;
                        string[] Consumer = Registry.RegisterArchive(Provider, Package(
                            "{\"schema\":1,\"id\":\"discoverconsumer\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":[\"discoverowner\"],\"optional\":[]}}",
                            "local A=require('@discoverowner/api'); assert(not pcall(A)); task.defer(function() assert(not pcall(A)); " +
                            "game:GetService('Workspace'):GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end)"));
                        Check(Consumer[0] == "OK" && Registry.ProcessOne() && Registry.Status(Provider, Consumer[1])[2] == "Active" &&
                            Discovery.History.Count == Accepted, "public/provisional foreign owner denied");
                        Drain(Host); Check(Discovery.History.Count == Accepted + 1 && Discovery.Held.Count == 1,
                            "cached borrowed foreign facade denied; consumer facade admitted");
                        Check(Discovery.Ready(Accepted) == Runtime.RuntimeStatus.OK, "consumer ready");
                        Drain(Host); Check(Discovery.Held.Count == 0, "consumer release");
                        Entry ConsumerEntry = Discovery.History[Accepted];
                        int PendingStart = Discovery.History.Count;
                        var Pending = Native.DomainExecute(ConsumerEntry.Session.FacadeVm,
                            (ulong)ConsumerEntry.Session.DomainLifetimeId, "discovery.addon-retirement",
                            "local W=game:GetService('Workspace'); for I=1,2 do W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() error('retired addon callback') end) end", 100);
                        Check(Pending.Status == Runtime.RuntimeStatus.OK && Discovery.Held.Count == 2 &&
                            Discovery.Ready(PendingStart) == Runtime.RuntimeStatus.OK, "addon holds ready and traversal callbacks");
                        int PriorFetches = Discovery.Fetches;
                        Check(Registry.UnloadProvider(Provider) == 2 && Discovery.Held.Count == 0,
                            "provider unload releases both pending addon captures");
                        Drain(Host);
                        Check(Discovery.Fetches == PriorFetches && Discovery.Ready(PendingStart) == Runtime.RuntimeStatus.INVALID_ARGUMENT,
                            "retired addon readiness/callback cannot replay");
                    }
                    RunAddonClosure(Host, Native, World, Discovery);
                    RunExample(Host, Discovery);
                    RunStress(Host, Discovery);
                    Execute(Host, "game:GetService('Workspace'):GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() error('ordinary discovery callback failure') end)");
                    int FailedCallback = Discovery.History.Count - 1;
                    Check(Discovery.Ready(FailedCallback) == Runtime.RuntimeStatus.OK, "ordinary callback error ready");
                    var FailedResults = Host.Drain();
                    Check(FailedResults.Count == 1 && FailedResults[0].Status == Runtime.RuntimeStatus.RUNTIME_ERROR &&
                        Host.Ready && Discovery.Held.Count == 0, "ordinary callback error releases capture and preserves healthy VM");
                    Drain(Host);
                    Check(Discovery.Ready(FailedCallback) == Runtime.RuntimeStatus.INVALID_ARGUMENT, "failed callback cannot replay");
                    Execute(Host, "game:GetService('Workspace'):GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() error('VM retired') end)");
                    int Last = Discovery.History.Count - 1; Check(Discovery.Ready(Last) == Runtime.RuntimeStatus.OK, "fatal queued fixture");
                    Before = Discovery.Fetches;
                    Check(Host.Execute("discovery.timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT,
                        "fatal VM recovery");
                    Check(Discovery.Held.Count == 0 && Discovery.Fetches == Before, "fatal retirement releases without replay");
                    Drain(Host);
                }
                Check(Discovery.Held.Count == 0 && Native.LiveVmCount == 0, "deterministic public teardown");
            }
            Console.WriteLine("[CarbonLuau:DiscoveryPublic] PASS real VM arguments, snapshots, reserved readiness, exact Entity factory, queued staleness, publication/laundering and retirement");
        } finally { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
