using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Run only after the public bindings qualify. Synthetic transitions exercise
// shared-VM resource/lifetime behavior, never the Rust host-source proof gate.
internal static class GameplayScaleTests
{
    private const string UserId = "76561190000999777", RootName = "scaleroot";
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous,Runtime.FacadeSession Next) { } }
    private static void Check(bool Value,string Message)
    { if (!Value) throw new InvalidOperationException("Gameplay scale: "+Message); }
    private static string Entry(string Name,int Listeners=128)
    {
        return "local S=require('state'); S.Calls=0; local P=game:GetService('Players'); " +
            "for Index=1,"+Listeners.ToString(CultureInfo.InvariantCulture)+" do P.PlayerDied:Connect(function(Player,Context) " +
            "assert(Player.UserId=='"+UserId+"' and table.isfrozen(Context)); S.Calls+=1 end) end; " +
            "game:GetService('Commands'):Register('"+Name+"',{},function() print('ScaleCount "+Name+" '..S.Calls) end)";
    }
    private static void Write(ZipArchive Zip,string Name,string Text)
    { using (Stream Stream=Zip.CreateEntry(Name).Open()) { byte[] Bytes=Encoding.UTF8.GetBytes(Text); Stream.Write(Bytes,0,Bytes.Length); } }
    private static byte[] Package(string Name,string Version,int Listeners=128)
    {
        using (var Output=new MemoryStream()) {
            using (var Zip=new ZipArchive(Output,ZipArchiveMode.Create,true)) {
                Write(Zip,"addon.json","{\"schema\":1,\"id\":\""+Name+"\",\"version\":\""+Version+"\"}");
                Write(Zip,"init.luau",Entry(Name,Listeners)); Write(Zip,"state.luau","return {}");
            }
            return Output.ToArray();
        }
    }
    private static string Drain(Runtime.ScriptHost Host)
    {
        var Logs=new StringBuilder();
        for (int Pass=0; Pass<128 && Host.HasWork; ++Pass)
            foreach (Runtime.ExecutionResult Result in Host.Drain()) {
                Check(Result.Status==Runtime.RuntimeStatus.OK,"bounded callback failure: "+Result.Error); Logs.Append(Result.Logs);
            }
        Check(!Host.HasWork,"shared scheduler drain converges"); return Logs.ToString();
    }
    private static void Activate(Runtime.AddonRegistry Registry)
    {
        int Guard=256;
        while (Registry.HasPending && Guard-- >0) Check(Registry.ProcessOne(),"eligible addon activation advances");
        Check(!Registry.HasPending,"bounded addon activation queue converges");
    }
    private static Dictionary<string,long> Counts(Runtime.FacadeWorld World,Runtime.ScriptHost Host)
    {
        foreach (Runtime.FacadeSession Session in World.Sessions())
            foreach (string Name in Session.Commands.Keys) Check(Session.Invoke(Name,UserId,new string[0]),"counter command queues independently of event quota");
        var Result=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach (string Line in Drain(Host).Split(new[] {'\n'},StringSplitOptions.RemoveEmptyEntries)) {
            string[] Fields=Line.Trim().Split(' ');
            Check(Fields.Length==3 && Fields[0]=="ScaleCount","bounded exact counter response");
            Result.Add(Fields[1],Int64.Parse(Fields[2],CultureInfo.InvariantCulture));
        }
        Check(Result.Count==World.Sessions().Count,"one observable counter per active domain"); return Result;
    }
    private static Runtime.FacadeSession Session(Runtime.FacadeWorld World,string Name)
    {
        foreach (Runtime.FacadeSession Value in World.Sessions()) if (Value.Commands.ContainsKey(Name)) return Value;
        throw new InvalidOperationException("Gameplay scale: missing owner "+Name);
    }
    private static string Name(Runtime.FacadeSession Session)
    { foreach (string Value in Session.Commands.Keys) return Value; throw new InvalidOperationException("Gameplay scale: unnamed owner"); }
    private static int BudgetField(Runtime.GameplayEventBudget Budget,string Name)
    { return (int)typeof(Runtime.GameplayEventBudget).GetField(Name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Budget); }
    private static void FlushNative(Runtime.ScriptHost Host,Runtime.FacadeWorld World)
    {
        var Domains=new List<Runtime.RuntimeDomain>();
        Domains.Add((Runtime.RuntimeDomain)typeof(Runtime.ScriptHost).GetField("Current",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Host));
        var Addons=(SortedDictionary<long,Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost).GetField("AddonDomains",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Host);
        Domains.AddRange(Addons.Values);
        foreach (Runtime.RuntimeDomain Domain in Domains) {
            int Guard=16;
            while (Domain.FacadeSession.PendingCount!=0 && Guard-- >0)
                Domain.FacadeSession.Flush(Domain,Stopwatch.StartNew(),20,64);
            Check(Domain.FacadeSession.PendingCount==0,"bounded manual native transfer converges");
        }
    }
    private static void Emit(Runtime.FacadeWorld World,Runtime.PlayerLifetime Player)
    {
        Check(World.GameplayEvents.Capture("died"),"synthetic producer capture accepted");
        World.GameplayEvent("died",Player.Token,Player.UserId,"Scale victim",null);
    }
    private static void Target(Runtime.FacadeWorld World,Runtime.FacadeSession Owner,Runtime.PlayerLifetime Player)
    {
        Check(World.GameplayEvents.Capture("died"),"replacement capture accepted");
        Check(Owner.GameplayEvent(new[] {"died","",Player.Token,Player.UserId,"Scale victim","","","","","","",""}),
            "exact old owner receives bounded targeted capture");
    }
    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var View=new Runtime.PlayerView {Identity=new object(),Connection=new object(),UserId=UserId,Name="Scale victim",Connected=true,
            Send=Text=>{},Permission=Permission=>true};
        var Players=new Runtime.PlayerDirectory(Id=>Id==UserId ? View : null); Runtime.PlayerLifetime Player=Players.Connect(View);
        var World=new Runtime.FacadeWorld(Players,new Registrar()); long Frame=0; World.GameplayEvents.FrameClock=()=>Frame;
        var Budget=World.GameplayEvents;
        Func<Runtime.ScriptSnapshot> Snapshot=()=>{var Value=new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource=Entry(RootName)};
            Value.Modules.Add("state","return {}"); return Value;};
        ulong PeakVm=0; int PeakRetained=0, PeakHeld=0; long MaximumGap=0;
        using (var Host=new Runtime.ScriptHost(Native,new Runtime.RuntimeConfig {MaxCallbackMilliseconds=100,FrameDrainBudgetMilliseconds=20},Snapshot,World)) {
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"saturated root public binding initialization");
            using (var Registry=new Runtime.AddonRegistry(Host,Native.HostLifetimeId)) {
                object[] Providers={new object(),new object(),new object(),new object()};
                var Registrations=new Dictionary<string,string>(StringComparer.Ordinal);
                var Owners=new Dictionary<string,object>(StringComparer.Ordinal);
                for (int Index=0; Index<100; ++Index) {
                    string Id="scale"+Index.ToString("D3",CultureInfo.InvariantCulture); object Provider=Providers[Index/25];
                    string[] Result=Registry.RegisterArchive(Provider,Package(Id,"1.0.0"));
                    Check(Result[0]=="OK","100 addon registration within four provider envelopes");
                    Registrations.Add(Id,Result[1]); Owners.Add(Id,Provider);
                }
                Activate(Registry);
                foreach (var Value in Registrations) Check(Registry.Status(Owners[Value.Key],Value.Value)[2]=="Active","all100 addon domains active");
                Check(Registry.Count==100 && Host.DomainCount==101 && Native.LiveVmCount==1,"one shared VM with root and100 addons");
                Dictionary<string,long> Before=Counts(World,Host); foreach (long Count in Before.Values) Check(Count==0,"no subscription backlog");
                ulong Baseline=Host.VmMemoryBytes; var Progress=new HashSet<string>(StringComparer.Ordinal);
                var LastProgress=new Dictionary<string,long>(StringComparer.Ordinal);
                // 101 saturated domains need26 four-domain fanout turns. The
                // requested first25 frames are measured, followed by one turn
                // to prove the last addon progresses; no latency SLA is implied.
                for (int Pass=1; Pass<=26; ++Pass) {
                    Frame++;
                    for (int Capture=0; Capture<128; ++Capture) Emit(World,Player);
                    Check(!Budget.Capture("died"),"129th real producer capture rejected");
                    Check(BudgetField(Budget,"Captures")==128 && BudgetField(Budget,"Deliveries")==128 && BudgetField(Budget,"Visits")==4096,
                        "actual public-signal burst respects128/128/4096 global work bounds");
                    var Pending=new Dictionary<string,int>(StringComparer.Ordinal); int Total=0;
                    foreach (Runtime.FacadeSession Value in World.Sessions()) {
                        Pending.Add(Name(Value),Value.PendingCount); Total+=Value.PendingCount;
                        Check(Value.PendingCount<=32,"real domain fanout bound32");
                    }
                    Check(Total==128 && Budget.PendingCount==128,"bounded accepted deliveries under intentionally lossy burst");
                    int Retained=Budget.RetainedBytes; FlushNative(Host,World);
                    Check(Host.SchedulerSnapshot.Queued==128 && Budget.PendingCount==128 && Budget.RetainedBytes==Retained,
                        "native queued payloads retain global quota after managed intake empties");
                    PeakHeld=Math.Max(PeakHeld,Budget.PendingCount); PeakRetained=Math.Max(PeakRetained,Budget.RetainedBytes);
                    Drain(Host); Dictionary<string,long> After=Counts(World,Host);
                    foreach (var Value in After) {
                        long Delta=Value.Value-Before[Value.Key]; Check(Delta==Pending[Value.Key] && Delta<=32,"one callback attempt per admitted listener");
                        if (Delta>0) {
                            Progress.Add(Value.Key); long Previous;
                            if (LastProgress.TryGetValue(Value.Key,out Previous)) MaximumGap=Math.Max(MaximumGap,Pass-Previous);
                            LastProgress[Value.Key]=Pass;
                        }
                    }
                    Before=After; PeakVm=Math.Max(PeakVm,Host.VmMemoryBytes);
                    Check(Budget.PendingCount==0 && Budget.RetainedBytes==0 && Host.VmMemoryBytes<=Host.VmMemoryLimitBytes,
                        "each saturation frame consumes reservations and stays inside VM cap");
                    if (Pass==25) Console.WriteLine("[CarbonLuau:GameplayScale] measured_frames=25; progressed="+Progress.Count+
                        "; peak_vm="+PeakVm+"; baseline_vm="+Baseline+"; peak_held="+PeakHeld+"; peak_retained="+PeakRetained+"; "+Budget.Status);
                }
                Check(Progress.Count==101 && MaximumGap<=26 && PeakHeld<=512 && PeakRetained<=2*1024*1024,
                    "root andall100 addons progress within this26-frame saturated fixture");
                Check(Host.VmMemoryBytes<=Baseline+8*1024*1024,"ephemeral contexts converge within bounded8MiB measured drift");
                // The isolation case keeps the root saturated while each of
                //100 unrelated addons owns only one listener. It complements
                // the all-saturated rotation above without changing policy.
                foreach (var Value in Registrations)
                    Check(Registry.ReplaceArchive(Owners[Value.Key],Value.Value,Package(Value.Key,"2.0.0",1))[0]=="OK",
                        "single-listener isolation replacement admitted");
                Activate(Registry); Before=Counts(World,Host);
                foreach (var Value in Before) if (Value.Key!=RootName) Check(Value.Value==0,"single-listener replacements have no replay");
                var SingleProgress=new HashSet<string>(StringComparer.Ordinal);
                var SingleLast=new Dictionary<string,int>(StringComparer.Ordinal); int SingleGap=0;
                for (int Pass=1; Pass<=25; ++Pass) {
                    Frame++; Emit(World,Player); FlushNative(Host,World);
                    Check(Budget.PendingCount==128 && Host.SchedulerSnapshot.Queued==128,"one saturated root cannot exceed global native hold128");
                    Drain(Host); var After=Counts(World,Host); long Total=0;
                    foreach (var Value in After) {
                        long Delta=Value.Value-Before[Value.Key]; Total+=Delta;
                        if (Value.Key==RootName) Check(Delta>=28 && Delta<=32,"root admits only residual bounded fanout");
                        else {
                            Check(Delta==0 || Delta==1,"unrelated single listener receives at most one delivery per capture");
                            if (Delta==1) {
                                SingleProgress.Add(Value.Key); int Previous;
                                if (SingleLast.TryGetValue(Value.Key,out Previous)) SingleGap=Math.Max(SingleGap,Pass-Previous);
                                SingleLast[Value.Key]=Pass;
                            }
                        }
                    }
                    Check(Total==128 && Budget.PendingCount==0 && Budget.RetainedBytes==0,"single-listener isolation consumes exact global128 deliveries");
                    Before=After; PeakVm=Math.Max(PeakVm,Host.VmMemoryBytes);
                }
                Check(SingleProgress.Count==100 && SingleGap<=2,"all100 unrelated single-listener addons progress despite saturated root");
                Console.WriteLine("[CarbonLuau:GameplayScale] one_saturated_root=128; unrelated_single_listener_addons=100; measured_frames=25; progressed="+
                    SingleProgress.Count+"; max_observed_service_gap="+SingleGap+"; vm="+Host.VmMemoryBytes+"; "+Budget.Status);
                Frame++; Emit(World,Player);
                var PendingUnload=new Dictionary<string,int>(StringComparer.Ordinal); object Unloading=null; int Dropped=0;
                foreach (Runtime.FacadeSession Value in World.Sessions()) {
                    string Id=Name(Value); PendingUnload[Id]=Value.PendingCount;
                    if (Unloading==null && Id!=RootName && Value.PendingCount>0) Unloading=Owners[Id];
                }
                Check(Unloading!=null,"provider has queued work at unload boundary");
                foreach (var Value in PendingUnload) if (Value.Key!=RootName && Object.ReferenceEquals(Owners[Value.Key],Unloading)) Dropped+=Value.Value;
                FlushNative(Host,World); int Held=Budget.PendingCount;
                Check(Registry.UnloadProvider(Unloading)==25 && Registry.Count==75 && Budget.PendingCount==Held-Dropped,
                    "provider unload discards exact captured native ownership only");
                Drain(Host); var Surviving=Counts(World,Host);
                foreach (var Value in Surviving) Check(Value.Value-Before[Value.Key]==PendingUnload[Value.Key],"unrelated provider callbacks survive unload");
                string Replacing=null; foreach (var Value in Owners) if (!Object.ReferenceEquals(Value.Value,Unloading)) {Replacing=Value.Key;break;}
                for (int Pass=1; Pass<=10; ++Pass) {
                    Frame++; Runtime.FacadeSession Old=Session(World,Replacing); int Expected=Math.Min(32,Old.GameplayDiedListeners);
                    Target(World,Old,Player); FlushNative(Host,World);
                    Check(Budget.PendingCount==Expected,"old replacement domain owns its exact bounded native callbacks");
                    string[] Result=Registry.ReplaceArchive(Owners[Replacing],Registrations[Replacing],Package(Replacing,"2.0."+Pass));
                    Check(Result[0]=="OK","repeated replacement admitted"); Activate(Registry);
                    Runtime.FacadeSession Next=Session(World,Replacing);
                    Check(Old.Disposed && Next.DomainLifetimeId!=Old.DomainLifetimeId && Budget.PendingCount==0,"replacement retires old captures without retarget");
                    Check(Counts(World,Host)[Replacing]==0,"new domain never replays retired observations");
                }
                foreach (object Provider in Providers) Registry.UnloadProvider(Provider);
                Check(Registry.Count==0 && Host.DomainCount==1 && Budget.PendingCount==0,"all providers retire without affecting root");
                Frame++; Runtime.FacadeSession OldRoot=World.Active; Target(World,OldRoot,Player); FlushNative(Host,World);
                Check(Host.Reload().Status==Runtime.RuntimeStatus.OK && OldRoot.Disposed && Budget.PendingCount==0 && Counts(World,Host)[RootName]==0,
                    "root replacement cancels native captures and publishes a fresh subscription without history");
            }
        }
        Check(Native.LiveVmCount==0 && World.Active==null && World.Gui.LiveRegistryCount==0 && Budget.PendingCount==0 && Budget.RetainedBytes==0,
            "full shared-VM/provider/root teardown converges");
        Check(!Budget.Capture("died"),"unloaded scene produces no replay or subscriber demand");
        Console.WriteLine("[CarbonLuau:GameplayScale] PASS one sharedVM,100 addons, root saturation, bounded work/bytes, progress, provider unload and repeated replacement; synthetic transitions only");
    }
}
