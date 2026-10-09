using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Actual shared VM/registry/public Signals with synthetic qualified snapshots.
// Required/optional binding and ResourceOwner remain existing D14/D10 semantics.
internal static class GameplayDependencySignalTests
{
    private const string UserId="76561190000999666", Dependency="lifecycleprovider";
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous,Runtime.FacadeSession Next) { } }
    private static void Check(bool Value,string Message)
    { if (!Value) throw new InvalidOperationException("Gameplay dependency: "+Message); }
    private static void Write(ZipArchive Zip,string Name,string Text)
    { using (Stream Stream=Zip.CreateEntry(Name).Open()) { byte[] Bytes=Encoding.UTF8.GetBytes(Text); Stream.Write(Bytes,0,Bytes.Length); } }
    private static byte[] Archive(string Manifest,string Source,params string[] Modules)
    {
        using (var Output=new MemoryStream()) {
            using (var Zip=new ZipArchive(Output,ZipArchiveMode.Create,true)) {
                Write(Zip,"addon.json",Manifest); Write(Zip,"init.luau",Source);
                for (int Index=0; Index<Modules.Length; Index+=2) Write(Zip,Modules[Index]+".luau",Modules[Index+1]);
            }
            return Output.ToArray();
        }
    }
    private static string Manifest(string Id,bool Optional)
    { return "{\"schema\":1,\"id\":\""+Id+"\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":["+
        (Optional ? "" : "\""+Dependency+"\"")+"],\"optional\":["+(Optional ? "\""+Dependency+"\"" : "")+"]}}"; }
    private static string OwnSignals(string Label,string Marker)
    {
        return "local P=game:GetService('Players'); P.PlayerDied:Connect(function(_,C) assert(table.isfrozen(C)); print('"+Label+" died '.."+Marker+") end); "+
            "P.PlayerSpawned:Connect(function(_,C) assert(table.isfrozen(C)); print('"+Label+" spawned '.."+Marker+") end); ";
    }
    private static byte[] Provider(string Marker,string Version)
    {
        string Api="local Api={Marker='"+Marker+"',Players=game:GetService('Players')}; "+
            "function Api.ConnectForeign() Api.Players.PlayerDied:Connect(function() print('foreign died '..Api.Marker) end); "+
            "Api.Players.PlayerSpawned:Connect(function() print('foreign spawned '..Api.Marker) end) end; return Api";
        string Cold=OwnSignals("LEAK cold","'"+Marker+"'")+"error('cold foreign failure')";
        string Nested="assert(not pcall(require,'cold')); "+OwnSignals("LEAK nested","'"+Marker+"'")+"error('outer foreign failure')";
        return Archive("{\"schema\":1,\"id\":\""+Dependency+"\",\"version\":\""+Version+"\",\"main\":\"api\",\"publicModules\":[\"cold\",\"nested\"]}",
            "local Api=require('api'); game:GetService('Commands'):Register('providerstate',{},function() print('Provider '..Api.Marker) end)",
            "api",Api,"cold",Cold,"nested",Nested);
    }
    private static byte[] Consumer(string Id,string Label,bool Optional,bool Foreign=false)
    {
        string Source="local Api=require('@"+Dependency+"'); local Marker=Api.Marker; ";
        Source+=Foreign ? "Api.ConnectForeign(); " : OwnSignals(Label,"Marker");
        Source+="game:GetService('Commands'):Register('"+Label+"state',{},function() print('Binding "+Label+" '..Marker..' '..tostring(addon:IsDependencyAvailable('"+Dependency+"'))) end)";
        return Archive(Manifest(Id,Optional),Source);
    }
    private static void Process(Runtime.AddonRegistry Registry)
    {
        int Guard=32;
        while (Registry.HasPending && Guard-- >0) Check(Registry.ProcessOne(),"eligible graph activation advances");
        Check(!Registry.HasPending,"bounded dependency graph converges");
    }
    private static string Register(Runtime.AddonRegistry Registry,object Owner,byte[] Package,string State="Active")
    {
        string[] Result=Registry.RegisterArchive(Owner,Package); Check(Result[0]=="OK","registration accepted");
        Process(Registry); Check(Registry.Status(Owner,Result[1])[2]==State,"registration reaches "+State); return Result[1];
    }
    private static Runtime.FacadeSession Session(Runtime.FacadeWorld World,string Command)
    {
        foreach (Runtime.FacadeSession Value in World.Sessions()) if (Value.Commands.ContainsKey(Command)) return Value;
        throw new InvalidOperationException("Gameplay dependency: owner absent "+Command);
    }
    private static string Drain(Runtime.ScriptHost Host,Runtime.FacadeWorld World)
    {
        var Text=new StringBuilder();
        for (int Pass=0; Pass<128 && Host.HasWork; ++Pass)
            foreach (Runtime.ExecutionResult Result in Host.Drain()) {
                Check(Result.Status==Runtime.RuntimeStatus.OK,"callback error: "+Result.Error); Text.Append(Result.Logs);
            }
        Check(!Host.HasWork && World.GameplayEvents.PendingCount==0 && World.GameplayEvents.RetainedBytes==0,"owned queued work converges");
        return Text.ToString();
    }
    private static void Lines(string Actual,params string[] Expected)
    {
        var Values=new List<string>(Actual.Split(new[] {'\n'},StringSplitOptions.RemoveEmptyEntries));
        Values.Sort(StringComparer.Ordinal); var Wanted=new List<string>(Expected); Wanted.Sort(StringComparer.Ordinal);
        Check(String.Join("|",Values)==String.Join("|",Wanted),"exact delivered owners/markers: "+Actual.Trim());
    }
    private static void Both(Runtime.FacadeWorld World,Runtime.PlayerLifetime Player)
    {
        foreach (string Kind in new[] {"died","spawned"}) {
            Check(World.GameplayEvents.Capture(Kind),"synthetic capture admitted");
            World.GameplayEvent(Kind,Player.Token,Player.UserId,Player.Name,null);
        }
    }
    private static void NativeQueue(Runtime.ScriptHost Host,Runtime.FacadeWorld World)
    {
        var Values=new List<Runtime.RuntimeDomain> {(Runtime.RuntimeDomain)typeof(Runtime.ScriptHost).GetField("Current",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Host)};
        Values.AddRange(((SortedDictionary<long,Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost).GetField("AddonDomains",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Host)).Values);
        foreach (Runtime.RuntimeDomain Value in Values) Value.FacadeSession.Flush(Value,Stopwatch.StartNew(),20);
        Check(Host.SchedulerSnapshot.Queued==(ulong)World.GameplayEvents.PendingCount,"lifecycle reservation survives actual native transfer");
    }
    private static void Binding(Runtime.FacadeWorld World,Runtime.ScriptHost Host,string Label,string Marker,bool Available)
    {
        Check(Session(World,Label+"state").Invoke(Label+"state",UserId,new string[0]),"binding observation command");
        Lines(Drain(Host,World),"Binding "+Label+" "+Marker+" "+(Available ? "true" : "false"));
    }
    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var View=new Runtime.PlayerView {Identity=new object(),Connection=new object(),UserId=UserId,Name="Dependency victim",Connected=true,
            Send=Value=>{},Permission=Value=>true};
        var Players=new Runtime.PlayerDirectory(Id=>Id==UserId ? View : null); Runtime.PlayerLifetime Player=Players.Connect(View);
        var World=new Runtime.FacadeWorld(Players,new Registrar()); long Frame=0; World.GameplayEvents.FrameClock=()=>Frame;
        using (var Host=new Runtime.ScriptHost(Native,new Runtime.RuntimeConfig {MaxCallbackMilliseconds=100,FrameDrainBudgetMilliseconds=20},
            ()=>new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource=OwnSignals("root","'root'")},World)) {
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"independent root subscriptions");
            using (var Registry=new Runtime.AddonRegistry(Host,Native.HostLifetimeId)) {
                object ProviderOwner=new object(),Consumers=new object(),ForeignOwner=new object(),FailureOwner=new object();
                string ProviderToken=Register(Registry,ProviderOwner,Provider("A1","1.0.0"));
                string Required=Register(Registry,Consumers,Consumer("liferequired","required",false));
                string Optional=Register(Registry,Consumers,Consumer("lifeoptional","optional",true));
                Runtime.FacadeSession OptionalOwner=Session(World,"optionalstate");
                string Failed=Register(Registry,FailureOwner,Archive(Manifest("lifefailed",false),
                    "assert(not pcall(require,'@"+Dependency+"/nested')); "+OwnSignals("LEAK candidate","'caller'")+"error('failed addon')"),"Failed");
                Check(Session(World,"providerstate").ListenerCount==0,"foreign cold/outer rollback publishes no provider-owned roots");
                string Foreign=Register(Registry,ForeignOwner,Consumer("lifeforeign","foreign",true,true));
                Check(Session(World,"providerstate").ListenerCount==2 && Session(World,"foreignstate").ListenerCount==0,
                    "captured foreign facade remains provider ResourceOwner, never caller-owned");
                Binding(World,Host,"required","A1",true); Binding(World,Host,"optional","A1",true);
                Frame++; Both(World,Player);
                Lines(Drain(Host,World),"root died root","root spawned root","required died A1","required spawned A1",
                    "optional died A1","optional spawned A1","foreign died A1","foreign spawned A1");
                Runtime.FacadeSession RequiredOld=Session(World,"requiredstate"),ProviderOld=Session(World,"providerstate");
                Frame++; Both(World,Player); NativeQueue(Host,World); Check(World.GameplayEvents.PendingCount==8,"all four original owners queued");
                Check(Registry.ReplaceArchive(ProviderOwner,ProviderToken,Provider("A2","2.0.0"))[0]=="OK","provider replacement accepted"); Process(Registry);
                Check(RequiredOld.Disposed && ProviderOld.Disposed && Session(World,"requiredstate").DomainLifetimeId!=RequiredOld.DomainLifetimeId,
                    "required consumer reconstructs after exact provider binding replacement");
                Check(Object.ReferenceEquals(OptionalOwner,Session(World,"optionalstate")) && World.GameplayEvents.PendingCount==4,
                    "optional owns surviving listeners while required/provider old native work is cancelled");
                Lines(Drain(Host,World),"root died root","root spawned root","optional died A1","optional spawned A1");
                Binding(World,Host,"required","A2",true); Binding(World,Host,"optional","A1",false);
                Check(Registry.BindingStatus(Consumers,Optional,Dependency)[1]=="stale","optional binding does not hot-rebind to replacement");
                Check(Session(World,"providerstate").ListenerCount==0 && Session(World,"foreignstate").ListenerCount==0,
                    "retired foreign-facade listeners do not move to holder or new provider");
                Frame++; Both(World,Player);
                Lines(Drain(Host,World),"root died root","root spawned root","required died A2","required spawned A2","optional died A1","optional spawned A1");
                Runtime.FacadeSession RequiredBeforeLoss=Session(World,"requiredstate");
                Frame++; Both(World,Player); NativeQueue(Host,World);
                Check(Registry.UnloadProvider(ProviderOwner)==1 && Registry.Status(Consumers,Required)[2]=="Blocked" && RequiredBeforeLoss.Disposed,
                    "required provider loss retires consumer and queued old authority");
                Lines(Drain(Host,World),"root died root","root spawned root","optional died A1","optional spawned A1");
                ProviderToken=Register(Registry,ProviderOwner,Provider("A2","2.0.1"));
                Check(Registry.Status(Consumers,Required)[2]=="Active" && Session(World,"requiredstate").DomainLifetimeId!=RequiredBeforeLoss.DomainLifetimeId,
                    "required restoration activates a fresh consumer from immutable snapshot");
                Binding(World,Host,"required","A2",true); Binding(World,Host,"optional","A1",false);
                Check(Object.ReferenceEquals(OptionalOwner,Session(World,"optionalstate")),"restoration preserves optional domain identity and pure A1 value");
                Frame++; Both(World,Player);
                Lines(Drain(Host,World),"root died root","root spawned root","required died A2","required spawned A2","optional died A1","optional spawned A1");
                Frame++; Both(World,Player); NativeQueue(Host,World); long Generation=Host.VmGenerationId;
                Runtime.ExecutionResult Fatal=Host.Execute("gameplay.dependencies.timeout","while true do end");
                Check(Fatal.Status==Runtime.RuntimeStatus.TIMEOUT && Host.Ready && Host.VmGenerationId!=Generation && Host.Recoveries==1 &&
                    World.GameplayEvents.PendingCount==0,"VM-fatal reconstruction discards captured old-generation work");
                Process(Registry); Check(Registry.Status(Consumers,Required)[2]=="Active" && Registry.Status(Consumers,Optional)[2]=="Active",
                    "real registry restores eligible domains in fresh VM");
                Check(OptionalOwner.Disposed && Session(World,"optionalstate").DomainLifetimeId!=OptionalOwner.DomainLifetimeId,
                    "new VM creates fresh optional lifetime rather than hot-rebinding old one");
                Check(Drain(Host,World)=="","reconstruction does not replay queued Died/Spawned observations");
                Binding(World,Host,"required","A2",true); Binding(World,Host,"optional","A2",true);
                Check(Registry.Status(FailureOwner,Failed)[2]=="Failed","failed candidate remains failed instead of leaking/restoring listeners");
                Check(Session(World,"providerstate").ListenerCount==2 && Session(World,"foreignstate").ListenerCount==0,
                    "reconstructed foreign facade still owns listeners in new provider domain");
                Frame++; Both(World,Player);
                Lines(Drain(Host,World),"root died root","root spawned root","required died A2","required spawned A2",
                    "optional died A2","optional spawned A2","foreign died A2","foreign spawned A2");
            }
        }
        Check(Native.LiveVmCount==0 && World.Active==null && World.GameplayEvents.PendingCount==0 && World.GameplayEvents.RetainedBytes==0,
            "dependency fixture releases complete VM/provider/queued ownership");
        Console.WriteLine("[CarbonLuau:GameplayDependencySignals] PASS required replacement/loss/restoration, optional exact no-hotbind, provider ResourceOwner, failed foreign publication and fresh-VM no-replay; synthetic snapshots only");
    }
}
