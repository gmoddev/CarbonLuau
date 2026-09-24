using System;
using System.Diagnostics;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    private static void ValueBounds(Fixture F)
    {
        F.Execute(Store + @"
local Huge={string.rep('x',16300),string.rep('y',16300),string.rep('z',16300),string.rep('w',16300)}
local Returned=false
assert(select('#',S:SetAsync('near64k',Huge,function(V,E)
 assert(Returned and V==true and E==nil)
 S:GetAsync('near64k',function(R,E2)
  assert(E2==nil and #R==4 and #R[1]==16300 and string.sub(R[1],1,1)=='x')
  print('near64k-done')
 end)
end))==0)
Returned=true; Huge[1]='changed'
local TooBig={string.rep('x',16384),string.rep('y',16384),string.rep('z',16384),string.rep('w',16384)}
assert(not pcall(function() S:SetAsync('too-big',TooBig,function() error('rejected callback') end) end))
local Leaf={} for I=1,1024 do Leaf[I]=true end
assert(not pcall(function() S:SetAsync('expanded',{Leaf,Leaf,Leaf,Leaf},function() error('rejected callback') end) end))
local Depth={} local Tail=Depth for I=2,16 do Tail.Next={} Tail=Tail.Next end
S:SetAsync('depth16',Depth,function(V,E) assert(V==true and E==nil); print('depth16-done') end)
local D=game:GetService('DataStoreService')
assert(not pcall(function() D:GetDataStore(string.rep('é',33)) end))
assert(not pcall(function() S:GetAsync(string.rep('é',65),function() end) end))
D:GetDataStore(string.rep('é',32)):SetAsync(string.rep('é',64),{['']='empty',['a/b:c']='map',Control='\t\n'},function(V,E)
 assert(V==true and E==nil); print('utf8-boundary-done')
end)
");
        Check(F.Queue.PendingCount == 3, "only valid snapshot/name boundaries admitted");
        F.Until("near64k-done"); F.Until("depth16-done"); F.Until("utf8-boundary-done"); F.Refill();
    }

    private static void Publication(Fixture F)
    {
        F.Modules["leak"] = @"
local State=require('state')
State.Leaked=game:GetService('DataStoreService'):GetDataStore('failed-scope')
task.defer(function() State.Leaked:SetAsync('x',true,function() error('failed scope callback') end) end)
error('intentional cold failure')";
        F.Reload();
        long Sent = F.Worker.RequestsSent;
        F.Execute(@"
local State=require('state')
assert(not pcall(require,'leak'))
assert(State.Leaked~=nil)
assert(not pcall(function() State.Leaked:GetAsync('x',function() error('stale facade') end) end))
");
        F.Tick();
        Check(F.Queue.PendingCount == 0 && F.Worker.RequestsSent == Sent, "failed module leaked facade and deferred request discarded");

        F.Modules["committed"] = Store + "return S";
        F.Modules["cold-operation"] = @"
local S=require('committed')
for _,M in {'GetAsync','SetAsync','RemoveAsync'} do
 local function Attempt() if M=='SetAsync' then S[M](S,'no-io',true,function() end) else S[M](S,'no-io',function() end) end end
 assert(not pcall(Attempt))
 local OK=coroutine.resume(coroutine.create(function() assert(not pcall(Attempt)) end)); assert(OK)
end
return true";
        F.Reload(); F.Execute("require('committed'); assert(require('cold-operation'))");
        Check(F.Queue.PendingCount == 0 && F.Worker.RequestsSent == Sent, "cached facade/coroutine cannot launder cold publication");
        long Generation = F.Host.Generation;
        F.Source = Store + @"
task.defer(function() S:SetAsync('candidate-leak',true,function() error('candidate callback') end) end)
error('intentional rejected candidate')";
        var Rejected = F.Host.Reload();
        Check(Rejected.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && F.Host.Generation == Generation, "ordinary failed candidate preserves active root");
        F.Tick();
        Check(F.Queue.PendingCount == 0 && F.Worker.RequestsSent == Sent, "failed candidate dispatches nothing");
        F.Source = "return true";
        F.Execute(Store + "S:GetAsync('candidate-leak',function(V,E) assert(V==nil and E==nil); print('publication-done') end)");
        F.Until("publication-done");
    }

    private static void CallbackFailures(Fixture F)
    {
        F.Refill();
        var Original = F.Host.Execute("persistence.original-error", Store +
            "S:SetAsync('original-error','kept',function(V,E) assert(V==true and E==nil); print('original-error-done') end); error('caller failed after acceptance')");
        Check(Original.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && F.Queue.PendingCount == 1, "ordinary originating error preserves accepted request");
        F.Until("original-error-done");

        F.Execute(Store + "S:SetAsync('callback-error','kept',function(V,E) assert(V==true and E==nil); error('intentional storage callback failure') end)");
        bool SawError = false;
        WaitFor(() => SawError, () => {
            F.Tick(false);
            foreach (var Result in F.Host.Drain()) {
                F.Logs.Append(Result.Logs);
                if (Result.Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Result.Error.Contains("intentional storage callback failure")) SawError = true;
                else Check(Result.Status == Runtime.RuntimeStatus.OK, "unexpected callback failure: " + Result.Error);
            }
        }, "ordinary callback error reported separately");
        Check(F.Host.Ready && F.Queue.PendingCount == 0, "callback error releases reservation, preserves VM");
        F.Execute(Store + "S:GetAsync('callback-error',function(V,E) assert(V=='kept' and E==nil); print('callback-error-durable') end)");
        F.Until("callback-error-durable"); F.Refill();

        // Both replies reach native before drain. The first callback's fatal
        // deadline retires the second callback and every old VM domain.
        long PreviousVm = F.Host.VmGenerationId;
        F.Execute(Store + @"
S:SetAsync('callback-timeout','durable',function(V,E) assert(V==true and E==nil); while true do end end)
S:GetAsync('absent',function() print('FORBIDDEN-FATAL-CALLBACK') end)");
        WaitFor(() => {
            int Count=0; foreach (var R in Reservations(F.Queue)) if (R!=null && R.HandedOff) ++Count;
            return Count==2;
        }, () => F.Tick(false), "two callbacks retained before fatal drain");
        bool TimedOut = false;
        foreach (var Result in F.Host.Drain()) {
            F.Logs.Append(Result.Logs);
            if (Result.Status == Runtime.RuntimeStatus.TIMEOUT) TimedOut = true;
            else Check(Result.Status == Runtime.RuntimeStatus.OK, "fatal recovery result: " + Result.Error);
        }
        Check(TimedOut && F.Host.Ready && F.Host.VmGenerationId != PreviousVm && F.Host.Recoveries == 1, "storage callback has VM-fatal budget and recovers");
        F.Tick();
        Check(F.Queue.PendingCount == 0 && !F.Logs.ToString().Contains("FORBIDDEN-FATAL-CALLBACK"), "fatal recovery discards old callback");
        F.Execute(Store + "S:GetAsync('callback-timeout',function(V,E) assert(V=='durable' and E==nil); print('fatal-durable') end)");
        F.Until("fatal-durable"); F.Refill();
        Console.WriteLine("[CarbonLuau:Persistence1B] publication, ordinary origin/callback failure and fatal callback recovery PASS");
    }

    private static void CrossDomain(Fixture F)
    {
        object Provider = new object(), Consumer = new object();
        string Api = @"
local D=game:GetService('DataStoreService'); local S=D:GetDataStore('Private')
return {Service=D,Store=S,Acquire=function() return game:GetService('DataStoreService') end,
 Read=function() S:GetAsync('key',function() error('foreign callback') end) end,
 Schedule=function() task.defer(function() S:GetAsync('key',function(V,E) assert(V==nil and E==nil); print('owner-admission-done') end) end) end}";
        Check(F.Addons.RegisterArchive(Provider, Package("persist.owner", "require('api')", Api))[0] == "OK" && F.Addons.ProcessOne(), "owner API package active");
        string Source = @"
local API=require('@persist.owner')
task.defer(function()
 local function Foreign(F) local OK,E=pcall(F); assert(not OK and string.find(tostring(E),'ForeignDataStore',1,true)) end
 Foreign(function() API.Service:GetDataStore('Private') end)
 Foreign(function() API.Store:GetAsync('key',function() error('foreign') end) end)
 Foreign(function() API.Store:SetAsync('key',true,function() error('foreign') end) end)
 Foreign(function() API.Store:RemoveAsync('key',function() error('foreign') end) end)
 Foreign(API.Read); Foreign(API.Acquire)
 API.Schedule()
 print('foreign-rejected')
end)";
        Check(F.Addons.RegisterArchive(Consumer, Package("persist.consumer", Source, null,
            "{\"required\":[\"persist.owner\"],\"optional\":[]}"))[0] == "OK" && F.Addons.ProcessOne(), "consumer API package active");
        long Sent = F.Worker.RequestsSent;
        F.Until("foreign-rejected"); F.Until("owner-admission-done");
        Check(F.Worker.RequestsSent == Sent + 1, "foreign attempts submit zero; scheduled owner gets fresh admission");
        object Watcher = new object();
        Check(F.Addons.RegisterArchive(Watcher, Package("persist.watcher", @"
local API=require('@persist.owner')
task.defer(function()
 local OK,E=pcall(API.Read); assert(not OK and string.find(tostring(E),'StaleDataStore',1,true))
 local OK2,E2=pcall(function() API.Service:GetDataStore('Private') end)
 assert(not OK2 and string.find(tostring(E2),'StaleDataStore',1,true))
 print('foreign-retired')
end)", null, "{\"required\":[],\"optional\":[\"persist.owner\"]}"))[0] == "OK" && F.Addons.ProcessOne(), "optional observer retains old facade");
        Check(F.Addons.UnloadProvider(Consumer) == 1 && F.Addons.UnloadProvider(Provider) == 1, "cross-domain fixtures retired");
        F.Until("foreign-retired");
        Check(F.Addons.UnloadProvider(Watcher) == 1, "observer retired");

        object Retiring = new object();
        Source = Store + "task.defer(function() S:SetAsync('provider-retired','kept',function() print('FORBIDDEN-PROVIDER-CALLBACK') end) end)";
        Check(F.Addons.RegisterSource(Retiring, "provider.retired", "1.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "retiring writer active");
        WaitFor(() => HandedOff(F) == 1, () => {
            // Drain only the initializer, never the completed storage callback.
            if (F.Queue.PendingCount == 0) F.DrainChecked();
            F.Tick(false);
        }, "provider write completed before unload");
        Check(F.Addons.UnloadProvider(Retiring) == 1 && F.Queue.PendingCount == 0, "provider retirement releases ready callback");
        Source = Store + "task.defer(function() S:GetAsync('provider-retired',function(V,E) assert(V=='kept' and E==nil); print('provider-reassigned') end) end)";
        object Reassigned = new object();
        Check(F.Addons.RegisterSource(Reassigned, "provider.retired", "2.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "fresh provider resumes same durable namespace");
        F.Until("provider-reassigned");
        Check(!F.Logs.ToString().Contains("FORBIDDEN-PROVIDER-CALLBACK") && F.Addons.UnloadProvider(Reassigned) == 1, "old provider callback never executes");
        Console.WriteLine("[CarbonLuau:Persistence1B] foreign facade/service/exported closure rejection and owner callback admission PASS");
    }

    private static void Stress(Fixture F)
    {
        var Watch = Stopwatch.StartNew();
        object Provider = new object();
        // Ten namespaces pace accepted requests below 20/s each and 200/s total.
        // Exactly 2000 real-worker reads must complete without overload retries.
        for (int Index = 0; Index < 10; ++Index) {
            string Source = @"
local S=game:GetService('DataStoreService'):GetDataStore('ReadStress')
local Sent,Done=0,0
local function Submit()
 Sent+=1
 S:GetAsync('absent',function(V,E)
  assert(V==nil and E==nil); Done+=1
  if Done==200 then print('stress-'..addon.Id) else task.delay(0.06,Submit) end
 end)
end
task.defer(Submit)";
            Check(F.Addons.RegisterSource(Provider, "stress" + Index, "1.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "stress addon active");
        }
        WaitFor(() => F.Queue.Completed[0] == 2000 && F.Queue.PendingCount == 0, () => F.Tick(), "2000 paced reads complete", 60000);
        for (int Index = 0; Index < 10; ++Index)
            Check(F.Logs.ToString().Contains("stress-stress" + Index + "\n"), "each namespace completes 200 reads");
        Check(F.Worker.RequestsSent == 2000 && F.Queue.RateRejected == 0 && F.Queue.QueueRejected == 0,
            "2000 reads complete within legitimate namespace/global rates: " + F.Status);
        Check(F.Addons.UnloadProvider(Provider) == 10, "stress addon teardown");
        Console.WriteLine("[CarbonLuau:Persistence1B] 2000 production-worker reads across 10 namespaces PASS; elapsed_ms=" + Watch.ElapsedMilliseconds);
    }

    private static void StoreQuota(Fixture F)
    {
        object Provider = new object();
        string Source = @"
local D=game:GetService('DataStoreService'); local I=0
local function Next()
 I+=1
 D:GetDataStore('S'..I):SetAsync('K',true,function(V,E)
  assert(V==true and E==nil)
  if I==64 then print('quota-filled') else task.delay(0.22,Next) end
 end)
end
task.defer(Next)";
        Check(F.Addons.RegisterSource(Provider, "quota", "1.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "quota fixture active");
        F.Until("quota-filled", 30000);
        Check(F.Addons.UnloadProvider(Provider) == 1, "quota domain retired");
        Source = @"
local D=game:GetService('DataStoreService'); local New=D:GetDataStore('New')
task.defer(function()
 New:SetAsync('K',true,function(V,E)
  assert(V==nil and E=='QuotaExceeded')
  task.delay(0.22,function() D:GetDataStore('S1'):RemoveAsync('K',function(V2,E2)
   assert(V2==true and E2==nil)
   task.delay(0.22,function() New:SetAsync('K',true,function(V3,E3) assert(V3==true and E3==nil); print('quota-released') end) end)
  end) end)
 end)
end)";
        Check(F.Addons.RegisterSource(new object(), "quota", "2.0.0", Encoding.UTF8.GetBytes(Source))[0] == "OK" && F.Addons.ProcessOne(), "reassigned namespace retains actual store quota");
        F.Until("quota-released");
        Check(F.Queue.QuotaRejected == 1 && F.Queue.PendingCount == 0, "real QuotaExceeded and committed remove release");
        Console.WriteLine("[CarbonLuau:Persistence1B] representative real 64-store durable quota/reassignment/remove release PASS");
    }
}
