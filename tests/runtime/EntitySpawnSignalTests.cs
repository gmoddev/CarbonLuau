using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;
using Model = Carbon.Plugins.EntityLifetimeModel;

// Exact EntityLifetimeModel plus the real compiler/VM/event scheduler. These
// synthetic host objects are not evidence for Rust Spawn completion timing.
internal static class EntitySpawnSignalTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    internal sealed class ObjectState
    {
        internal ulong Id;
        internal bool Alive = true;
        internal string Prefab = "assets/fixture.prefab";
    }
    internal sealed class EntityHost : Carbon.Plugins.IEntityFacadeHost
    {
        internal readonly Model Model = new Model();
        internal readonly Dictionary<ulong, ObjectState> Objects = new Dictionary<ulong, ObjectState>();
        internal int Lookups;
        internal bool Ready = true;
        internal EntityHost() { Model.BeginQualifiedObservation(); Model.QualifyStartupCompletion(); }
        private Model.HostEvidence Evidence(object Identity)
        {
            var Value = (ObjectState)Identity; ObjectState Occupant;
            Objects.TryGetValue(Value.Id, out Occupant);
            return new Model.HostEvidence(Value.Alive, true, true, Value.Id, Value.Prefab, Occupant);
        }
        private static bool Current(Runtime.FacadeSession Session, Runtime.FacadeSession.PublicationWitness Witness,
            Model.Authority Value)
        { return !Session.Disposed && Session.IsPublicationWitnessCurrent(Witness) &&
            Value.VmGeneration == (ulong)Session.VmGenerationId && Value.DomainLifetime == (ulong)Session.DomainLifetimeId &&
            Value.PublicationLifetime == Witness.Token; }
        internal Model.Binding Capture(Runtime.FacadeSession Session, ObjectState Value)
        {
            var Witness = Session.CaptureCommittedEntityWitness(); Model.Binding Result;
            return Model.TryAdmit(Value, new Model.Authority((ulong)Session.VmGenerationId,
                (ulong)Session.DomainLifetimeId, Witness.Token), Authority => Current(Session, Witness, Authority), Evidence, out Result) ? Result : null;
        }
        public string[] Lookup(Runtime.FacadeSession Session, string Id)
        {
            Lookups++; ObjectState Value;
            if (!Objects.TryGetValue(UInt64.Parse(Id, CultureInfo.InvariantCulture), out Value)) return new string[0];
            var Witness = Session.CaptureEntityWitness(); Model.Binding Result;
            if (!Model.TryAdmit(Value, new Model.Authority((ulong)Session.VmGenerationId,
                (ulong)Session.DomainLifetimeId, Witness.Token), Authority => Current(Session, Witness, Authority), Evidence, out Result)) return new string[0];
            return new[] {"77", Result.Record.Token.ToString(), Id, Witness.Token.ToString()};
        }
        public string[] Read(Runtime.FacadeSession Session, string Token, string Publication, string Property)
        {
            var Witness = Session.FindEntityWitness(UInt64.Parse(Publication)); Model.Binding Result;
            if (!Ready || Witness == null || !Model.TryBindToken(UInt64.Parse(Token),
                new Model.Authority((ulong)Session.VmGenerationId, (ulong)Session.DomainLifetimeId, Witness.Token),
                Authority => Current(Session, Witness, Authority), Evidence, out Result)) throw new Runtime.FacadeException("stale Entity");
            if (Property == "Id") return new[] {Result.Record.NetworkId.ToString()};
            if (Property == "Prefab") return new[] {Result.Record.Prefab};
            if (Property == "Position") return new[] {"1", "2", "3"};
            throw new Runtime.FacadeException("unknown Entity property");
        }
    }
    private static void Check(bool Value, string Message)
    { if (!Value) throw new Exception("EntitySpawned: " + Message); }
    private const string State = "local S=require('state'); ";
    private const string Entry = State + "S.Count=0; local W=game:GetService('Workspace'); S.Connection=W.EntitySpawned:Connect(function(E) " +
        "S.Count+=1; S.Last=E; assert(E.Prefab=='assets/fixture.prefab' and E.Position==Vector3.new(1,2,3)); print('spawn '..E.Id) end)";
    internal sealed class Fixture : IDisposable
    {
        internal readonly EntityHost Entities = new EntityHost();
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.ScriptHost Host;
        internal readonly Dictionary<string,Runtime.PlayerView> Players = new Dictionary<string,Runtime.PlayerView>();
        internal string Source = Entry;
        internal long Frame;
        internal Fixture(Runtime.NativeRuntime Native)
        {
            World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => Players.ContainsKey(Id) ? Players[Id] : null), new Registrar());
            World.Entities = Entities; World.GameplayEvents.FrameClock = () => Frame;
            World.GameplayAvailable = Kind => Kind != "entityspawned" || Entities.Ready;
            Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds=20,FrameDrainBudgetMilliseconds=20}, Snapshot, World);
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "initial subscriptions");
        }
        private Runtime.ScriptSnapshot Snapshot()
        {
            var Value = new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource=Source};
            Value.Modules.Add("state", "return {}");
            Value.Modules.Add("connectfail", "game:GetService('Workspace').EntitySpawned:Connect(function() error('leak') end); error('fail')");
            Value.Modules.Add("disconnectfail", State+"S.Connection:Disconnect(); error('fail')");
            Value.Modules.Add("nestedfail", "assert(require('connectfail')); return true");
            return Value;
        }
        internal ObjectState Spawn(ulong Id, bool Success = true, ObjectState Existing = null)
        {
            var Value = Existing ?? new ObjectState {Id=Id}; Value.Alive=true; Entities.Objects[Id]=Value;
            var Attempt = Entities.Model.BeginSpawn(Value);
            if (Entities.Model.CompleteSpawn(Attempt, Success, Success)) Emit(Value);
            return Value;
        }
        internal void Emit(ObjectState Value)
        {
            if (World.GameplayEvents.Capture("entityspawned")) World.EntitySpawned(77, Session => Entities.Capture(Session, Value));
        }
        internal void Execute(string Source)
        { var Result=Host.Execute("entityspawn.fixture",Source); Check(Result.Status==Runtime.RuntimeStatus.OK,Result.Error); }
        internal string Drain(bool AllowError = false)
        {
            var Text = new StringBuilder();
            for(int Turn=0;Turn<256&&Host.HasWork;Turn++) foreach(var Result in Host.Drain()) {
                Check(AllowError||Result.Status==Runtime.RuntimeStatus.OK,Result.Error); Text.Append(Result.Logs);
            }
            Check(!Host.HasWork&&World.GameplayEvents.PendingCount==0&&World.GameplayEvents.RetainedBytes==0,"drain/release");
            return Text.ToString();
        }
        public void Dispose()
        { Host.Dispose(); Entities.Model.Dispose(); Check(World.GameplayEvents.PendingCount==0&&World.GameplayEvents.RetainedBytes==0,"teardown"); }
    }
    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        using(var Value = new Fixture(Native)) {
            Check(Value.Drain()=="","registration no replay");
            Value.Spawn(1); Check(Value.Drain()=="spawn 1\n"&&Value.Entities.Lookups==0,"exact completion, no ID lookup at delivery");
            Value.Execute(State+"assert(S.Count==1 and S.Last==game:GetService('Workspace'):GetEntityById('1'))");
            int Lookups=Value.Entities.Lookups;
            Value.Frame++; var Killed=Value.Spawn(2); Killed.Alive=false;
            Check(Value.Drain()==""&&Value.Entities.Lookups==Lookups,"Spawn then Kill suppressed without lookup");
            Value.Frame++; Value.Spawn(3,false); Check(Value.Drain()=="","failed original emits none");
            var Recursive=new ObjectState{Id=30}; Value.Entities.Objects[30]=Recursive;
            var Outer=Value.Entities.Model.BeginSpawn(Recursive);
            var Inner=Value.Entities.Model.BeginSpawn(Recursive);
            Check(Inner==null&&!Value.Entities.Model.CompleteSpawn(Outer,true,true),"recursive epoch poisoned, no guessed completion");
            Value.Emit(Recursive); Check(Value.Drain()=="","recursive attempts never publish live proxy");
            Value.Frame++; var Old=Value.Spawn(4); var Fresh=Value.Spawn(4);
            Check(Value.Drain()=="spawn 4\n"&&Value.Entities.Lookups==Lookups,"same ID new object cannot inherit old event");
            Value.Frame++; Value.Spawn(5); var Reused=Value.Entities.Objects[5]; Value.Spawn(5,true,Reused);
            Check(Value.Drain()=="spawn 5\n","same object/new epoch suppresses old capture");
            Value.Execute("assert(not pcall(require,'connectfail')); assert(not pcall(require,'disconnectfail')); assert(not pcall(require,'nestedfail'))");
            Check(Value.World.Active.ListenerCount==1,"module rollback restores exact callbacks");
            Value.Frame++; Value.Spawn(6); Check(Value.Drain()=="spawn 6\n","callback root restored");
            Value.Frame++; Value.Spawn(7); Value.Execute(State+"S.Connection:Disconnect(); S.Connection:Disconnect()");
            Check(Value.Drain()=="","queued disconnect cancels");
            Value.Entities.Ready=false;
            Value.Execute("assert(not pcall(function() game:GetService('Workspace').EntitySpawned:Connect(function() end) end))");
            Value.Entities.Ready=true;
        }
        using(var Unobserved=new Model()) {
            var Object=new ObjectState{Id=90}; var Attempt=Unobserved.BeginSpawn(Object);
            Check(!Unobserved.CompleteSpawn(Attempt,true,true)&&!Unobserved.QualifyStartupCompletion(),
                "first install/hotload cannot manufacture startup continuity");
        }
        using(var Value = new Fixture(Native)) {
            var Previous=Value.World.Active; Value.Source="game:GetService('Workspace').EntitySpawned:Connect(function() error('leak') end); error('fail')";
            Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.RUNTIME_ERROR&&Value.World.Active==Previous,"failed candidate preserves");
            Value.Spawn(1); Check(Value.Drain()=="spawn 1\n","failed candidate no publication");
            Value.Frame++; Value.Spawn(2); Value.Source=Entry;
            Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.OK&&Previous.Disposed&&Value.Drain()=="","successful replacement cancels old work");
            Value.Frame++; Value.Spawn(3); Check(Value.Drain()=="spawn 3\n","future fresh owner");
        }
        foreach(string Body in new[]{"error('callback')","coroutine.yield()","while true do end"}) using(var Value=new Fixture(Native)) {
            Value.Execute("game:GetService('Workspace').EntitySpawned:Connect(function() "+Body+" end)");
            long Generation=Value.Host.VmGenerationId; Value.Spawn(1); Check(Value.Drain(true).Contains("spawn 1\n"),"unrelated listener progress");
            if(Body=="while true do end") Check(Value.Host.VmGenerationId!=Generation&&Value.Host.Recoveries==1,"private cancellation VM fatal");
        }
        Check(Native.LiveVmCount==0,"no retained VMs");
        RunMixed(Native);
        RunDependencies(Native);
        RunScale(Native);
        Console.WriteLine("[CarbonLuau:EntitySpawned] PASS real VM, exact lifetime/no ID retarget, rapid Kill, rollback, cancellation, replacement, faults and recovery; synthetic host model");
    }
    private static byte[] Package(string Id, string Source = null, string ManifestSuffix = "", string Api = null)
    {
        using(var Output=new MemoryStream()) {
            using(var Zip=new ZipArchive(Output,ZipArchiveMode.Create,true)) {
                var Files=new Dictionary<string,string> {
                    {"addon.json","{\"schema\":1,\"id\":\""+Id+"\",\"version\":\"1.0.0\""+ManifestSuffix+"}"},
                    {"init.luau",Source ?? "game:GetService('Workspace').EntitySpawned:Connect(function(E) assert(E.Position==Vector3.new(1,2,3)); print('B1 '.. '"+Id+"') end)"}};
                if(Api!=null) Files.Add("api.luau",Api);
                foreach(var Pair in Files)
                    using(var Stream=Zip.CreateEntry(Pair.Key).Open()) {
                        byte[] Bytes=Encoding.UTF8.GetBytes(Pair.Value); Stream.Write(Bytes,0,Bytes.Length);
                    }
            }
            return Output.ToArray();
        }
    }
    private static void RunMixed(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) {
            const string Id="76561190000999111";
            var View=new Runtime.PlayerView{Identity=new object(),Connection=new object(),UserId=Id,Name="B1 mixed",Connected=true};
            Value.Players.Add(Id,View); var Player=Value.World.Players.Connect(View);
            Value.Source=Entry+"local P=game:GetService('Players'); P.PlayerDied:Connect(function() print('B1 died') end); "+
                "P.PlayerSpawned:Connect(function() print('B1 player-spawned') end); "+
                "game:GetService('Commands'):Register('b1fair',{},function() print('B1 command') end); task.defer(function() print('B1 deferred') end)";
            Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.OK,"mixed initialization");
            var Entity=Value.Spawn(700);
            for(int Index=1;Index<128;Index++) Value.Emit(Entity);
            Check(!Value.World.GameplayEvents.Capture("died")&&!Value.World.GameplayEvents.Capture("spawned"),"producer limit shared, not per stream");
            Check(Value.World.Active.Invoke("b1fair",Id,new string[0]),"command admission beside producer saturation");
            string Saturated=Value.Drain();
            Check(Saturated.Contains("B1 command\n")&&Saturated.Contains("B1 deferred\n"),"ordinary work progresses beside bounded Entity stream");
            Value.Frame++;
            foreach(string Kind in new[]{"died","spawned"}) {
                Check(Value.World.GameplayEvents.Capture(Kind),"fresh mixed frame capture");
                Value.World.GameplayEvent(Kind,Player.Token,Player.UserId,Player.Name,null);
            }
            Value.Emit(Entity); string Mixed=Value.Drain();
            Check(Mixed.Contains("B1 died\n")&&Mixed.Contains("B1 player-spawned\n")&&Mixed.Contains("spawn 700\n"),"Player and Entity typed dispatch coexist");
        }
        Console.WriteLine("[CarbonLuau:EntitySpawnedMixed] PASS shared producer limits, Player/Entity dispatch and commands/deferred progress");
    }
    private static void RunDependencies(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) using(var Registry=new Runtime.AddonRegistry(Value.Host,Native.HostLifetimeId)) {
            object Provider=new object(),Consumers=new object();
            Action Process=()=>{int Guard=32;while(Registry.HasPending&&Guard-->0)Registry.ProcessOne();Check(!Registry.HasPending,"dependency convergence");};
            Func<string,byte[]> Dependency=Marker=>Package("b1provider", "game:GetService('Workspace').EntitySpawned:Connect(function() print('B1 provider "+Marker+"') end)",
                ",\"main\":\"api\"", "return {Marker='"+Marker+"'}");
            Func<string,bool,byte[]> Consumer=(Name,Optional)=>Package(Name,"local Api=require('@b1provider'); "+
                "game:GetService('Workspace').EntitySpawned:Connect(function(E) assert(E.Id~=nil); print('B1 "+Name+" '..Api.Marker) end)",
                ",\"dependencies\":{\"required\":["+(Optional?"":"\"b1provider\"")+"],\"optional\":["+(Optional?"\"b1provider\"":"")+"]}");
            string Token=Registry.RegisterArchive(Provider,Dependency("A1"))[1]; Process();
            string Required=Registry.RegisterArchive(Consumers,Consumer("b1required",false))[1]; Process();
            Registry.RegisterArchive(Consumers,Consumer("b1optional",true)); Process();
            Value.Spawn(800); FlushNative(Value);
            Registry.ReplaceArchive(Provider,Token,Dependency("A2")); Process();
            string Queued=Value.Drain();
            Check(!Queued.Contains("B1 provider A1")&&!Queued.Contains("B1 b1required A1")&&Queued.Contains("B1 b1optional A1"),"replacement cancels exact provider/required, optional owner persists");
            Value.Frame++; Value.Spawn(801); string Fresh=Value.Drain();
            Check(Fresh.Contains("B1 provider A2")&&Fresh.Contains("B1 b1required A2")&&Fresh.Contains("B1 b1optional A1"),"required reconstruction, optional no hot rebind");
            Registry.UnloadProvider(Provider); Process();
            Check(Registry.Status(Consumers,Required)[2]=="Blocked","required dependency loss");
            Value.Frame++; Value.Spawn(802); string Lost=Value.Drain();
            Check(!Lost.Contains("B1 provider")&&!Lost.Contains("B1 b1required")&&Lost.Contains("B1 b1optional A1"),"provider loss suppresses retired listeners");
            Registry.RegisterArchive(Provider,Dependency("A3")); Process();
            Value.Frame++; Value.Spawn(803); string Restored=Value.Drain();
            Check(Restored.Contains("B1 provider A3")&&Restored.Contains("B1 b1required A3")&&Restored.Contains("B1 b1optional A1"),"restoration only future events, exact optional binding");
            Registry.UnloadProvider(Provider); Registry.UnloadProvider(Consumers);
            Check(Registry.Count==0&&Value.World.GameplayEvents.PendingCount==0,"dependency teardown");
        }
        Console.WriteLine("[CarbonLuau:EntitySpawnedDependencies] PASS exact queued retirement, required reconstruction/loss/restoration, optional no hot rebind and no replay");
    }
    private static void FlushNative(Fixture Value)
    {
        var Domains=new List<Runtime.RuntimeDomain> {
            (Runtime.RuntimeDomain)typeof(Runtime.ScriptHost).GetField("Current",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Value.Host)};
        var Addons=(SortedDictionary<long,Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost)
            .GetField("AddonDomains",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Value.Host);
        Domains.AddRange(Addons.Values);
        foreach(var Domain in Domains) Domain.FacadeSession.Flush(Domain,Stopwatch.StartNew(),20,256);
    }
    private static void RunScale(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) using(var Registry=new Runtime.AddonRegistry(Value.Host,Native.HostLifetimeId)) {
            Value.Execute("for I=1,127 do game:GetService('Workspace').EntitySpawned:Connect(function(E) assert(E.Id~=nil) end) end");
            var Providers=new[]{new object(),new object(),new object(),new object()};
            var Tokens=new List<string>(); var Names=new HashSet<string>();
            for(int Index=0;Index<100;Index++) {
                string Name="b1a"+Index.ToString("D3"); Names.Add(Name);
                var Registration=Registry.RegisterArchive(Providers[Index/25],Package(Name)); Tokens.Add(Registration[1]);
                int Guard=16; while(Registry.HasPending&&Guard-->0) Registry.ProcessOne();
                Check(!Registry.HasPending&&Registry.Status(Providers[Index/25],Registration[1])[2]=="Active","100-addon activation");
            }
            Check(Native.LiveVmCount==1&&Value.Host.DomainCount==101,"one shared VM");
            var Progress=new HashSet<string>(); ulong Peak=Value.Host.VmMemoryBytes;
            for(int Frame=0;Frame<25;Frame++) {
                Value.Frame++; Value.Spawn((ulong)(100+Frame));
                Check(Value.World.GameplayEvents.PendingCount<=128&&Value.World.GameplayEvents.RetainedBytes<=2097152,"shared bounds");
                FlushNative(Value); Check(Value.Host.SchedulerSnapshot.Queued<=128,"shared native admission");
                foreach(string Line in Value.Drain().Split('\n')) if(Line.StartsWith("B1 ",StringComparison.Ordinal)) Progress.Add(Line.Substring(3));
                Peak=Math.Max(Peak,Value.Host.VmMemoryBytes);
            }
            Check(Progress.SetEquals(Names),"all 100 domains progress beside saturated root");
            Value.Frame++; Value.Spawn(200); FlushNative(Value);
            Registry.UnloadProvider(Providers[0]);
            string Remaining=Value.Drain();
            for(int Index=0;Index<25;Index++) Check(!Remaining.Contains("B1 b1a"+Index.ToString("D3")),"unloaded provider native work cancelled");
            for(int Turn=0;Turn<10;Turn++) {
                Value.Frame++; Value.Spawn((ulong)(210+Turn)); FlushNative(Value);
                Registry.ReplaceArchive(Providers[1],Tokens[25],Package("b1a025"));
                int Guard=16; while(Registry.HasPending&&Guard-->0) Registry.ProcessOne();
                Check(!Registry.HasPending&&Registry.Status(Providers[1],Tokens[25])[2]=="Active","repeated replacement");
                Check(!Value.Drain().Contains("B1 b1a025\n"),"old lifetime callback not reassigned to replacement");
            }
            Value.Frame++; var Burst=Value.Spawn(400);
            for(int Index=1;Index<512;Index++) Value.Emit(Burst);
            Check(Value.World.GameplayEvents.PendingCount<=128&&Value.World.GameplayEvents.ProducerRejected>0&&
                Value.World.GameplayEvents.FanoutRejected>0,"producer burst bounded/counts drops");
            Value.Drain();
            foreach(var Provider in Providers) Registry.UnloadProvider(Provider);
            Check(Registry.Count==0&&Value.World.GameplayEvents.PendingCount==0,"provider teardown");
            Console.WriteLine("[CarbonLuau:EntitySpawnedScale] PASS 100 addons, saturated root,25 frames,512 producer burst,10 replacements, exact native cancellation; peak_vm="+Peak);
        }
        Check(Native.LiveVmCount==0,"scale teardown");
    }
}
