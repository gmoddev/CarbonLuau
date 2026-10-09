using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Real compiler/VM, publication and scheduler, but deliberately synthetic
// completion witnesses. These tests do not qualify a Rust destruction seam.
internal static class EntityDestroyedSignalTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    internal sealed class Proof
    {
        internal bool Removed = true, Alive = true, Throws;
        internal int Checks;
        internal bool Validate()
        { Checks++; if (Throws) throw new InvalidOperationException("synthetic completion failure"); return Removed; }
    }
    private static void Check(bool Value, string Message)
    { if (!Value) throw new Exception("EntityDestroyed: " + Message); }
    private const string State = "local S=require('state'); ";
    private const string Entry = State + "S.Count=0; local W=game:GetService('Workspace'); " +
        "S.Connection=W.EntityDestroyed:Connect(function(C) " +
        "assert(type(C)=='table' and table.isfrozen(C)); assert(C.Id~=nil and C.Prefab=='assets/fixture.prefab'); " +
        "assert(C.EpochIdentity==nil and C.Token==nil and C.Entity==nil); " +
        "assert(not pcall(function() C.Id='replacement' end)); " +
        "assert(C.Position==nil or C.Position==Vector3.new(1,2,3)); " +
        "S.Count+=1; S.Last=C; print('destroy '..C.Id) end)";
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.ScriptHost Host;
        internal string Source = Entry;
        internal long Frame;
        internal bool Ready = true;
        internal int PlayerLookups;
        internal Fixture(Runtime.NativeRuntime Native)
        {
            World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => { PlayerLookups++; return null; }), new Registrar());
            // Deliberately no Entity host: snapshot delivery must not require
            // a live entity, create a facade or resolve a Player by its Id.
            World.GameplayEvents.FrameClock = () => Frame;
            World.GameplayAvailable = Kind => Kind != "entitydestroyed" || Ready;
            Host = new Runtime.ScriptHost(Native,
                new Runtime.RuntimeConfig {MaxCallbackMilliseconds=20,FrameDrainBudgetMilliseconds=20}, Snapshot, World);
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"initial subscription");
        }
        private Runtime.ScriptSnapshot Snapshot()
        {
            var Value=new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource=Source};
            Value.Modules.Add("state","return {}");
            Value.Modules.Add("connectfail","game:GetService('Workspace').EntityDestroyed:Connect(function() error('leak') end); error('fail')");
            Value.Modules.Add("disconnectfail",State+"S.Connection:Disconnect(); error('fail')");
            Value.Modules.Add("nestedfail","assert(require('connectfail')); return true");
            return Value;
        }
        internal Runtime.GameplayEntityDestroyedObservation Observation(ulong Id, ulong Epoch, Proof Proof, bool Position = true)
        {
            return new Runtime.GameplayEntityDestroyedObservation(Id.ToString(CultureInfo.InvariantCulture),
                "assets/fixture.prefab",Epoch,Position ? (Runtime.PlayerPosition?)new Runtime.PlayerPosition(1,2,3) : null,Proof.Validate);
        }
        internal void Emit(Runtime.GameplayEntityDestroyedObservation Observation)
        { if (World.GameplayEvents.Capture("entitydestroyed")) World.EntityDestroyed(Observation); }
        internal void Execute(string Source)
        { var Result=Host.Execute("entitydestroyed.fixture",Source); Check(Result.Status==Runtime.RuntimeStatus.OK,Result.Error); }
        internal string Drain(bool AllowError = false)
        {
            var Text=new StringBuilder();
            for(int Turn=0;Turn<256&&Host.HasWork;Turn++) foreach(var Result in Host.Drain()) {
                Check(AllowError||Result.Status==Runtime.RuntimeStatus.OK,Result.Error); Text.Append(Result.Logs);
            }
            Check(!Host.HasWork&&World.GameplayEvents.PendingCount==0&&World.GameplayEvents.RetainedBytes==0,"exact consumption/refund");
            Check(PlayerLookups==0,"snapshot delivery never resolves a Player");
            return Text.ToString();
        }
        public void Dispose()
        { Host.Dispose(); Check(World.GameplayEvents.PendingCount==0&&World.GameplayEvents.RetainedBytes==0,"teardown refunds"); }
    }
    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) {
            Check(Value.Drain()=="","registration does not replay history");
            ulong PositionAccepted=Value.World.GameplayEvents.DestroyPositionAccepted;
            for(int Index=0;Index<512;Index++)
                Check(Value.World.GameplayEvents.DestroyPositionSnapshot()==(Index<128),"optional removal snapshot work bounded independently of veto/transport");
            Check(Value.World.GameplayEvents.DestroyPositionAccepted-PositionAccepted==128&&
                Value.World.GameplayEvents.DestroyPositionRejected==384,"bounded optional snapshot diagnostics");
            Check(Value.World.GameplayEvents.Capture("entitydestroyed"),"vetoed optional observations do not consume event captures");
            Value.Frame++;
            Check(Value.World.GameplayEvents.DestroyPositionSnapshot(),"new frame restores optional snapshot allowance");
            Value.Execute(State+"S.Connection:Disconnect()");
            PositionAccepted=Value.World.GameplayEvents.DestroyPositionAccepted;
            for(int Index=0;Index<512;Index++)Check(!Value.World.GameplayEvents.DestroyPositionSnapshot(),"no subscriber performs no optional host snapshot");
            Check(Value.World.GameplayEvents.DestroyPositionAccepted==PositionAccepted,"no-demand optional work charge remains zero");
            Value.Source=Entry;Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.OK,"restore initial subscription after budget probe");
            Value.Frame++;
            var HistoricalProof=new Proof();
            ulong StaleBefore=Value.World.GameplayEvents.StaleRejected;
            Value.Emit(new Runtime.GameplayEntityDestroyedObservation("900","assets/fixture.prefab",900,null,
                HistoricalProof.Validate,0));
            Check(Value.Drain()==""&&Value.World.GameplayEvents.StaleRejected>StaleBefore&&HistoricalProof.Checks==0,
                "polled removal cannot replay to listeners published after last positive live proof");
            Value.Frame++;
            ulong LiveCutoff=Value.World.DestroyedListenerVersion;
            Value.Emit(new Runtime.GameplayEntityDestroyedObservation("901","assets/fixture.prefab",901,null,
                new Proof().Validate,LiveCutoff));
            Check(Value.Drain()=="destroy 901\n","polled completion reaches already-published listeners");
            Value.Source=Entry;Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.OK,"reset counters after polling publication probes");
            Value.Frame++;
            var Retired=new Proof(); var Snapshot=Value.Observation(1,1,Retired);
            Value.Emit(Snapshot); Retired.Alive=false;
            Check(Value.Drain()=="destroy 1\n"&&Retired.Checks>0,"original snapshot survives actual retirement without live facade");
            Value.Execute(State+"assert(S.Count==1 and S.Last.Id=='1' and S.Last.Position==Vector3.new(1,2,3))");
            Value.Frame++; Value.Emit(Value.Observation(2,2,new Proof(),false));
            Check(Value.Drain()=="destroy 2\n","optional Position omitted without invented coordinates");
            Value.Execute(State+"assert(S.Last.Position==nil)");
            Value.Frame++; var Veto=new Proof{Removed=false}; Value.Emit(Value.Observation(3,3,Veto));
            Check(Value.Drain()==""&&Veto.Checks>0,"unproven or vetoed completion suppressed");
            Value.Frame++; Value.Emit(Value.Observation(4,4,new Proof{Throws=true}));
            Check(Value.Drain()=="","throwing host proof rejects without entering callback");
            Value.Frame++; Value.Emit(Value.Observation(5,5,new Proof())); Value.Ready=false;
            Check(Value.Drain()=="","source drift suppresses queued destruction");
            Value.Execute("assert(not pcall(function() game:GetService('Workspace').EntityDestroyed:Connect(function() end) end))");
            Value.Ready=true;
            Value.Frame++; Value.Emit(Value.Observation(6,6,new Proof{Alive=false}));
            Value.Emit(Value.Observation(6,7,new Proof{Alive=false}));
            Check(Value.Drain()=="destroy 6\ndestroy 6\n","two original lifetime snapshots may share a lookup Id without retargeting");
            Value.Execute("assert(not pcall(require,'connectfail')); assert(not pcall(require,'disconnectfail')); assert(not pcall(require,'nestedfail'))");
            Check(Value.World.Active.ListenerCount==1&&Value.World.Active.GameplayEntityDestroyedListeners==1,"failed modules restore listener and demand");
            Value.Frame++; Value.Emit(Value.Observation(7,8,new Proof()));
            Check(Value.Drain()=="destroy 7\n","restored callback still works");
            Value.Frame++; Value.Emit(Value.Observation(8,9,new Proof()));
            Value.Execute(State+"S.Connection:Disconnect(); S.Connection:Disconnect()");
            Check(Value.Drain()==""&&!Value.World.GameplayEvents.Capture("entitydestroyed"),"disconnect cancels pending and invalidates demand");
        }
        using(var Value=new Fixture(Native)) {
            var Previous=Value.World.Active;
            Value.Source="game:GetService('Workspace').EntityDestroyed:Connect(function() error('leak') end); error('fail')";
            Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.RUNTIME_ERROR&&Value.World.Active==Previous,"failed root candidate preserves old owner");
            Value.Emit(Value.Observation(10,10,new Proof())); Check(Value.Drain()=="destroy 10\n","failed candidate leaves no callback");
            Value.Frame++; Value.Emit(Value.Observation(11,11,new Proof())); FlushNative(Value);
            Value.Source=Entry; Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.OK&&Previous.Disposed,"successful root replacement");
            Check(Value.Drain()=="","native-queued old callbacks cancel without replay");
            Value.Frame++; Value.Emit(Value.Observation(12,12,new Proof())); Check(Value.Drain()=="destroy 12\n","new owner receives only future destruction");
        }
        foreach(string Body in new[]{"error('callback')","coroutine.yield()","while true do end"}) using(var Value=new Fixture(Native)) {
            Value.Execute("game:GetService('Workspace').EntityDestroyed:Connect(function() "+Body+" end)");
            long Generation=Value.Host.VmGenerationId; Value.Emit(Value.Observation(20,20,new Proof()));
            Check(Value.Drain(true).Contains("destroy 20\n"),"callback faults do not erase independent progress");
            if(Body=="while true do end") {
                Check(Value.Host.VmGenerationId!=Generation&&Value.Host.Recoveries==1,"private deadline remains VM-fatal");
                Value.Frame++; Value.Emit(Value.Observation(21,21,new Proof()));
                Check(Value.Drain()=="destroy 21\n","recovered root sees future observations only");
            }
        }
        RunPayloadValidation(Native);
        RunMixed(Native);
        RunDependencies(Native);
        RunScale(Native);
        Check(Native.LiveVmCount==0,"no retained VMs");
        Console.WriteLine("[CarbonLuau:EntityDestroyed] PASS real VM snapshot-only immutable context, original-lifetime delivery, proof rejection, publication, cancellation, replacement, faults/recovery; synthetic completion witnesses");
    }
    private static void RunPayloadValidation(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) {
            var Observation=Value.Observation(50,50,new Proof());
            string[] Fields={"entitydestroyed","1","50","50","assets/fixture.prefab","","1","2","3","","",""};
            byte[] Payload;
            ulong Nonce=Value.World.GameplayEvents.Reserve(Value.World.Active,"entitydestroyed","1",Fields,out Payload,null,Observation);
            Check(Nonce!=0&&Value.World.GameplayEvents.ToNative(Value.World.Active,Nonce),"model reservation setup");
            Check(Value.World.GameplayEvents.Validate(Value.World.Active,Fields),"captured fields accepted");
            foreach(int Index in new[]{2,3,4,6,7,8,9,10,11}) {
                string Saved=Fields[Index]; Fields[Index]="tampered";
                Check(!Value.World.GameplayEvents.Validate(Value.World.Active,Fields),"snapshot field mismatch fails closed"); Fields[Index]=Saved;
            }
            Value.World.GameplayEvents.Release(Value.World.Active,Nonce);
            foreach(string Id in new[]{"0","01","-1","18446744073709551616"}) {
                bool Rejected=false; try {new Runtime.GameplayEntityDestroyedObservation(Id,"assets/fixture.prefab",1,null,()=>true);} catch(Runtime.FacadeException){Rejected=true;}
                Check(Rejected,"noncanonical or oversized Id rejected");
            }
            foreach(string Prefab in new[]{"",new string('x',513),"nul\0prefab"}) {
                bool Rejected=false; try {new Runtime.GameplayEntityDestroyedObservation("1",Prefab,1,null,()=>true);} catch(Runtime.FacadeException){Rejected=true;}
                Check(Rejected,"malformed snapshot prefab rejected");
            }
        }
    }
    private static byte[] Package(string Id, string Source = null, string ManifestSuffix = "", string Api = null)
    {
        using(var Output=new MemoryStream()) {
            using(var Zip=new ZipArchive(Output,ZipArchiveMode.Create,true)) {
                var Files=new Dictionary<string,string>{
                    {"addon.json","{\"schema\":1,\"id\":\""+Id+"\",\"version\":\"1.0.0\""+ManifestSuffix+"}"},
                    {"init.luau",Source??"game:GetService('Workspace').EntityDestroyed:Connect(function(C) assert(table.isfrozen(C)); print('B3 "+Id+"') end)"}};
                if(Api!=null)Files.Add("api.luau",Api);
                foreach(var Pair in Files) using(var Stream=Zip.CreateEntry(Pair.Key).Open()) {
                    byte[] Bytes=Encoding.UTF8.GetBytes(Pair.Value); Stream.Write(Bytes,0,Bytes.Length);
                }
            }
            return Output.ToArray();
        }
    }
    private static void RunMixed(Runtime.NativeRuntime Native)
    {
        using(var Value=new EntitySpawnSignalTests.Fixture(Native)) {
            const string Id="76561190000999333";
            var View=new Runtime.PlayerView{Identity=new object(),Connection=new object(),UserId=Id,Name="B3 mixed",Connected=true};
            Value.Players.Add(Id,View); var Player=Value.World.Players.Connect(View);
            Value.Source="local W=game:GetService('Workspace'); local P=game:GetService('Players'); "+
                "W.EntitySpawned:Connect(function(E) print('B3 spawn') end); "+
                "W.EntityDestroyed:Connect(function(C) assert(table.isfrozen(C)); print('B3 removed') end); "+
                "P.PlayerDied:Connect(function() print('B3 died') end); P.PlayerSpawned:Connect(function() print('B3 player-spawned') end); "+
                "game:GetService('Commands'):Register('b3fair',{},function() print('B3 command') end); "+
                "task.defer(function() print('B3 deferred') end)";
            Check(Value.Host.Reload().Status==Runtime.RuntimeStatus.OK,"mixed source activation");
            var Observation=new Runtime.GameplayEntityDestroyedObservation("700","assets/fixture.prefab",700,null,()=>true);
            for(int Index=0;Index<128;Index++) {
                Check(Value.World.GameplayEvents.Capture("entitydestroyed"),"bounded mixed producer intake");
                Value.World.EntityDestroyed(Observation);
            }
            foreach(string Kind in new[]{"died","spawned","entityspawned"})
                Check(!Value.World.GameplayEvents.Capture(Kind),"global producer ceiling shared across four streams");
            Check(Value.World.Active.Invoke("b3fair",Id,new string[0]),"command admission beside saturated destruction");
            string Saturated=Value.Drain();
            Check(Saturated.Contains("B3 command\n")&&Saturated.Contains("B3 deferred\n"),"commands/deferred work progress beside destruction saturation");
            Value.Frame++;
            foreach(string Kind in new[]{"died","spawned"}) {
                Check(Value.World.GameplayEvents.Capture(Kind),"fresh mixed frame");
                Value.World.GameplayEvent(Kind,Player.Token,Player.UserId,Player.Name,null);
            }
            Value.Spawn(701);
            Check(Value.World.GameplayEvents.Capture("entitydestroyed"),"fresh destruction capture"); Value.World.EntityDestroyed(Observation);
            string Mixed=Value.Drain();
            Check(Mixed.Contains("B3 died\n")&&Mixed.Contains("B3 player-spawned\n")&&Mixed.Contains("B3 spawn\n")&&
                Mixed.Contains("B3 removed\n"),"all typed streams coexist");
        }
        Console.WriteLine("[CarbonLuau:EntityDestroyedMixed] PASS shared four-stream producer limits and command/deferred progress");
    }
    private static void RunDependencies(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) using(var Registry=new Runtime.AddonRegistry(Value.Host,Native.HostLifetimeId)) {
            object Provider=new object(), Consumers=new object();
            Action Process=()=>{int Guard=32;while(Registry.HasPending&&Guard-->0)Registry.ProcessOne();Check(!Registry.HasPending,"dependency convergence");};
            Func<string,byte[]> Dependency=Marker=>Package("b3provider","game:GetService('Workspace').EntityDestroyed:Connect(function() print('B3 provider "+Marker+"') end)",
                ",\"main\":\"api\"","return {Marker='"+Marker+"'}");
            Func<string,bool,byte[]> Consumer=(Name,Optional)=>Package(Name,"local Api=require('@b3provider'); "+
                "game:GetService('Workspace').EntityDestroyed:Connect(function(C) assert(C.Id~=nil); print('B3 "+Name+" '..Api.Marker) end)",
                ",\"dependencies\":{\"required\":["+(Optional?"":"\"b3provider\"")+"],\"optional\":["+(Optional?"\"b3provider\"":"")+"]}");
            string Token=Registry.RegisterArchive(Provider,Dependency("A1"))[1]; Process();
            string Required=Registry.RegisterArchive(Consumers,Consumer("b3required",false))[1]; Process();
            Registry.RegisterArchive(Consumers,Consumer("b3optional",true)); Process();
            Value.Emit(Value.Observation(800,800,new Proof())); FlushNative(Value);
            Registry.ReplaceArchive(Provider,Token,Dependency("A2")); Process();
            string Queued=Value.Drain();
            Check(!Queued.Contains("B3 provider A1")&&!Queued.Contains("B3 b3required A1")&&Queued.Contains("B3 b3optional A1"),"exact provider/required replacement cancellation, optional persistence");
            Value.Frame++; Value.Emit(Value.Observation(801,801,new Proof())); string Fresh=Value.Drain();
            Check(Fresh.Contains("B3 provider A2")&&Fresh.Contains("B3 b3required A2")&&Fresh.Contains("B3 b3optional A1"),"required reconstructs, optional does not rebind");
            Registry.UnloadProvider(Provider); Process();
            Check(Registry.Status(Consumers,Required)[2]=="Blocked","required dependency loss");
            Value.Frame++; Value.Emit(Value.Observation(802,802,new Proof())); string Lost=Value.Drain();
            Check(!Lost.Contains("B3 provider")&&!Lost.Contains("B3 b3required")&&Lost.Contains("B3 b3optional A1"),"retired owners suppressed after provider loss");
            Registry.RegisterArchive(Provider,Dependency("A3")); Process();
            Value.Frame++; Value.Emit(Value.Observation(803,803,new Proof())); string Restored=Value.Drain();
            Check(Restored.Contains("B3 provider A3")&&Restored.Contains("B3 b3required A3")&&Restored.Contains("B3 b3optional A1"),"restoration receives future events only");
            Registry.UnloadProvider(Provider); Registry.UnloadProvider(Consumers);
            Check(Registry.Count==0&&Value.World.GameplayEvents.PendingCount==0,"dependency teardown");
        }
        Console.WriteLine("[CarbonLuau:EntityDestroyedDependencies] PASS queued exact-owner retirement, required reconstruction/loss/restoration, optional no hot rebind and no replay");
    }
    private static void FlushNative(Fixture Value)
    {
        var Domains=new List<Runtime.RuntimeDomain>{(Runtime.RuntimeDomain)typeof(Runtime.ScriptHost)
            .GetField("Current",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Value.Host)};
        Domains.AddRange(((SortedDictionary<long,Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost)
            .GetField("AddonDomains",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Value.Host)).Values);
        foreach(var Domain in Domains) Domain.FacadeSession.Flush(Domain,Stopwatch.StartNew(),20,256);
    }
    private static void RunScale(Runtime.NativeRuntime Native)
    {
        using(var Value=new Fixture(Native)) using(var Registry=new Runtime.AddonRegistry(Value.Host,Native.HostLifetimeId)) {
            Value.Execute("for I=1,127 do game:GetService('Workspace').EntityDestroyed:Connect(function(C) assert(C.Id~=nil) end) end");
            var Providers=new[]{new object(),new object(),new object(),new object()};
            var Names=new HashSet<string>(); var Tokens=new List<string>();
            for(int Index=0;Index<100;Index++) {
                string Name="b3a"+Index.ToString("D3"); Names.Add(Name);
                var Registration=Registry.RegisterArchive(Providers[Index/25],Package(Name)); Tokens.Add(Registration[1]);
                int Guard=16; while(Registry.HasPending&&Guard-->0)Registry.ProcessOne();
                Check(!Registry.HasPending&&Registry.Status(Providers[Index/25],Registration[1])[2]=="Active","100-addon activation");
            }
            Check(Native.LiveVmCount==1&&Value.Host.DomainCount==101,"shared VM");
            var Progress=new HashSet<string>(); ulong Peak=Value.Host.VmMemoryBytes;
            for(int Frame=0;Frame<25;Frame++) {
                Value.Frame++; Value.Emit(Value.Observation((ulong)(100+Frame),(ulong)(100+Frame),new Proof{Alive=false}));
                Check(Value.World.GameplayEvents.PendingCount<=128&&Value.World.GameplayEvents.RetainedBytes<=2097152,"global bounds shared across all listeners");
                FlushNative(Value); Check(Value.Host.SchedulerSnapshot.Queued<=128,"native admission bound");
                foreach(string Line in Value.Drain().Split('\n')) if(Line.StartsWith("B3 ",StringComparison.Ordinal))Progress.Add(Line.Substring(3));
                Peak=Math.Max(Peak,Value.Host.VmMemoryBytes);
            }
            Check(Progress.SetEquals(Names),"all 100 addons progress beside saturated root");
            Value.Frame++; Value.Emit(Value.Observation(200,200,new Proof())); FlushNative(Value);
            Registry.UnloadProvider(Providers[0]); string Remaining=Value.Drain();
            for(int Index=0;Index<25;Index++)Check(!Remaining.Contains("B3 b3a"+Index.ToString("D3")),"provider unload cancels exact native-queued snapshots");
            for(int Turn=0;Turn<10;Turn++) {
                Value.Frame++; Value.Emit(Value.Observation((ulong)(210+Turn),(ulong)(210+Turn),new Proof())); FlushNative(Value);
                Registry.ReplaceArchive(Providers[1],Tokens[25],Package("b3a025"));
                int Guard=16; while(Registry.HasPending&&Guard-->0)Registry.ProcessOne();
                Check(!Registry.HasPending&&Registry.Status(Providers[1],Tokens[25])[2]=="Active","repeated replacement");
                Check(!Value.Drain().Contains("B3 b3a025\n"),"replacement never inherits queued old snapshot");
            }
            Value.Frame++; var Burst=Value.Observation(400,400,new Proof());
            for(int Index=0;Index<512;Index++)Value.Emit(Burst);
            Check(Value.World.GameplayEvents.PendingCount<=128&&Value.World.GameplayEvents.ProducerRejected>0&&
                Value.World.GameplayEvents.FanoutRejected>0,"producer burst drops counted and bounded");
            Value.Drain(); foreach(var Provider in Providers)Registry.UnloadProvider(Provider);
            Check(Registry.Count==0&&Value.World.GameplayEvents.PendingCount==0,"provider teardown");
            Console.WriteLine("[CarbonLuau:EntityDestroyedScale] PASS 100 addons, saturated root,25 frames,512 producer burst,10 replacements, provider/native cancellation; peak_vm="+Peak);
        }
    }
}
