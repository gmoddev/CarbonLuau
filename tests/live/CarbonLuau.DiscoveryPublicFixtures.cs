// Isolated Discovery-2B package only. Never include beside the private 2A partial.
// Every public test is source compiled by the installed pinned worker and run in
// the live native VM. CHECK/COMPLETE receipts are emitted only after assertions.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string DiscoveryPublicPrefix = "[CarbonLuau:DiscoveryPublicFixture] ";
        private const string DiscoveryPublicPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private const string DiscoveryPublicPrelude = "local S=require('state'); local W=game:GetService('Workspace'); local P=Vector3.new(0,5000,0); ";
        private readonly List<BaseEntity> DiscoveryPublicEntities = new List<BaseEntity>();
        private readonly List<RuntimeDomain> DiscoveryPublicDomains = new List<RuntimeDomain>();
        private ScriptSnapshot DiscoveryPublicRootSnapshot;
        private int DiscoveryPublicCases;

        partial void RunEntityDiscoveryFixtures()
        {
            NextTick(ExecuteDiscoveryPublicFixtures);
        }

        private static void RequireDiscoveryPublic(bool Condition, string Reason)
        { if (!Condition) throw new InvalidOperationException(Reason); }

        private int DiscoveryPublicPending()
        {
            int Count = 0;
            foreach (DiscoveryDelivery Value in DiscoveryDeliveries) if (Value != null) ++Count;
            return Count;
        }

        private void DiscoveryPublicReceipt(string Case)
        {
            ++DiscoveryPublicCases;
            Puts(DiscoveryPublicPrefix + "CHECK " + Case);
        }

        private BaseEntity DiscoveryPublicBox(float X, float Y = 5000)
        {
            BaseEntity Value = GameManager.server.CreateEntity(DiscoveryPublicPrefab, new Vector3(X, Y, 0));
            RequireDiscoveryPublic(Value != null, "fixture prefab unavailable");
            DiscoveryPublicEntities.Add(Value);
            Value.enableSaving = false;
            Value.Spawn();
            RequireDiscoveryPublic(Value.net != null && EntityLifetimes.HasCatalogObservation(Value),
                "completed Spawn did not enroll task-owned box");
            return Value;
        }

        private static string DiscoveryPublicId(BaseEntity Value)
        { return Value.net.ID.Value.ToString(CultureInfo.InvariantCulture); }

        private void DiscoveryPublicExecute(string Case, string Source)
        {
            ExecutionResult Result = Host.Execute("discovery2b." + Case, DiscoveryPublicPrelude + Source);
            RequireDiscoveryPublic(Result.Status == RuntimeStatus.OK,
                Case + ": " + Result.Status + " " + Result.Error);
        }

        private void DiscoveryPublicReload(string Source)
        {
            DiscoveryPublicRootSnapshot.EntrySource = Source;
            ExecutionResult Result = Host.Reload();
            RequireDiscoveryPublic(Result.Status == RuntimeStatus.OK && Host.Ready, "fixture root load: " + Result.Error);
        }

        // Hold native drain, then use the unchanged bounded traversal turn. The
        // domain_event notification only marks Ready; op38 runs in Host.Drain.
        private void DiscoveryPublicReady()
        {
            for (int Turn = 0; EntityDiscovery.ActiveCount != 0 && Turn < 4096; ++Turn) PumpEntityDiscovery();
            RequireDiscoveryPublic(EntityDiscovery.ActiveCount == 0, "traversal did not settle within bounded fixture turns");
            RequireDiscoveryPublic(EntityDiscoveryMaximumTurnUnits <= 1024 && EntityDiscoveryMaximumTurnRaw <= 1024,
                "production turn exceeded shared work/raw bound");
        }

        private void DiscoveryPublicDrain(bool CallbackError = false)
        {
            bool SawError = false;
            for (int Turn = 0; Host.HasReadyWork && Turn < 512; ++Turn) {
                foreach (ExecutionResult Result in Host.Drain()) {
                    if (CallbackError && Result.Status == RuntimeStatus.RUNTIME_ERROR) SawError = true;
                    else RequireDiscoveryPublic(Result.Status == RuntimeStatus.OK,
                        "native callback: " + Result.Status + " " + Result.Error);
                }
            }
            RequireDiscoveryPublic(!Host.HasReadyWork, "native drain did not settle");
            RequireDiscoveryPublic(!CallbackError || SawError, "expected callback failure was not observed");
        }

        private void DiscoveryPublicBaseline()
        {
            RequireDiscoveryPublic(DiscoveryPublicPending() == 0 && EntityDiscovery.ActiveCount == 0 &&
                Host.SchedulerSnapshot.Queued == 0, "request/result/native ready queue leak");
        }

        private string DiscoveryPublicCallback(string Case, string Assertion)
        {
            return "function(Entities,Error) assert(S['" + Case + "']==nil,'callback replay'); " +
                "S['" + Case + "']=1; " + Assertion + " end";
        }

        private void DiscoveryPublicQuery(string Case, string Position, string Radius, string Options, string Assertion)
        {
            DiscoveryPublicExecute(Case, "local Returned=false; local Count=select('#',W:GetEntitiesInRadiusAsync(" +
                Position + "," + Radius + ",function(Entities,Error) assert(Returned,'inline/yielded completion'); " +
                "assert(S['" + Case + "']==nil,'callback replay'); S['" + Case + "']=1; " + Assertion + " end" +
                (Options == null ? "" : "," + Options) + ")); assert(Count==0); Returned=true; assert(S['" + Case + "']==nil)");
            RequireDiscoveryPublic(DiscoveryPublicPending() == 1 && EntityDiscovery.ActiveCount == 1,
                Case + " did not reserve exactly one public request");
            DiscoveryPublicReady();
            DiscoveryPublicExecute(Case + ".ready", "assert(S['" + Case + "']==nil)");
            RequireDiscoveryPublic(DiscoveryPublicPending() == 1 && Host.SchedulerSnapshot.Queued == 1,
                "Ready notification released retained completion/callback capacity");
            DiscoveryPublicDrain();
            DiscoveryPublicExecute(Case + ".done", "assert(S['" + Case + "']==1)");
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt(Case);
        }

        private void DiscoveryPublicArguments()
        {
            DiscoveryPublicExecute("rejected-arguments", @"
local Called=0; local function Callback() Called+=1 end
local function Reject(Run) assert(not pcall(Run)) end
Reject(function() W:GetEntitiesInRadiusAsync(P,1) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,1,17) end)
Reject(function() W:GetEntitiesInRadiusAsync({},1,Callback) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,-1,Callback) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,0/0,Callback) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,math.huge,Callback) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,'1',Callback) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,1,{},Callback) end)
Reject(function() W:GetEntitiesInRadiusAsync(P,1,Callback,{},true) end)
for _,Options in {false, {Limit=0}, {Limit=257}, {Limit=1.5}, {Limit='1'},
    {Limit=0/0}, {Limit=math.huge}, {Prefab=''}, {Prefab=1}, {Prefab='x\0y'},
    {Prefab=string.rep('x',513)}, {Prefab=string.char(255)}, {Unknown=true},
    {[1]=true}, setmetatable({},{}), setmetatable({Limit=1},{__metatable=false})} do
    Reject(function() W:GetEntitiesInRadiusAsync(P,1,Callback,Options) end)
end
assert(Called==0)
local Touched=0
local Fake=setmetatable({},{__index=function() Touched+=1; error('fake Vector3 read') end})
Reject(function() W:GetEntitiesInRadiusAsync(Fake,1,Callback) end)
assert(Touched==0)
");
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("rejected-arguments-zero-requests");
        }

        private void DiscoveryPublicAuthority()
        {
            DiscoveryPublicReload(DiscoveryPublicPrelude + @"
local function Attempt() W:GetEntitiesInRadiusAsync(P,0,function() error('provisional callback') end) end
assert(not pcall(Attempt)); assert(not pcall(function() Attempt() end))
S.W=W; S.Run=require('cold'); S.ColdAttempted=true
");
            DiscoveryPublicBaseline();
            DiscoveryPublicExecute("cold-closure", "assert(S.ColdAttempted); S.Run(" +
                DiscoveryPublicCallback("cold", "assert(Error==nil and #Entities==0)") + ")");
            DiscoveryPublicReady(); DiscoveryPublicDrain();
            DiscoveryPublicExecute("cold-closure.done", "assert(S.cold==1)");
            DiscoveryPublicBaseline();
            FacadeSession Before = Gameplay.Active;
            ExecutionResult Failed = Host.Reload(DiscoveryPublicPrelude +
                "assert(not pcall(function() W:GetEntitiesInRadiusAsync(P,0,function() end) end)); " +
                "task.defer(function() error('failed candidate callback') end); error('candidate rejected')");
            RequireDiscoveryPublic(Failed.Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Before, Gameplay.Active),
                "failed provisional candidate retired committed root");
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("provisional-cold-nested-zero-requests-committed-export");
        }

        private void DiscoveryPublicConcurrency()
        {
            RuntimeGeneration Vm = (RuntimeGeneration)typeof(ScriptHost).GetField("Vm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Host);
            string Source = "local S=require('state'); local W=game:GetService('Workspace'); task.defer(function() " +
                "for Index=1,2 do W:GetEntitiesInRadiusAsync(Vector3.new(0,6000,0),0,function(E,R) " +
                "assert(R==nil and #E==0 and S[Index]==nil); S[Index]=1 end) end; " +
                "assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,6000,0),0,function() end) end)) end)";
            for (int Index = 0; Index < 3; ++Index) {
                var Snapshot = new ScriptSnapshot {EntryName="init.luau", EntrySource=Source};
                Snapshot.Modules.Add("state", "return {}");
                var Domain = new RuntimeDomain(Native, Vm, new RuntimeConfig {MaxCallbackMilliseconds=100},
                    Snapshot);
                DiscoveryPublicDomains.Add(Domain);
                Domain.Facade(new FacadeSession(Gameplay, Domain.VmGenerationId, Domain.DomainLifetimeId, 4096));
                RequireDiscoveryPublic(Domain.Execute("discovery2b.domain", Source, 100).Status == RuntimeStatus.OK,
                    "concurrency domain initialization failed");
                Domain.Commit(); Gameplay.CommitAddon(null, Domain.FacadeSession);
            }
            DiscoveryPublicDrain(); // Each real domain accepts two before traversal runs.
            DiscoveryPublicExecute("capacity", "for Index=1,2 do W:GetEntitiesInRadiusAsync(P,0," +
                "function(Entities,Error) local Key='capacity'..Index; assert(S[Key]==nil); S[Key]=1; " +
                "assert(Error==nil and #Entities==0) end" +
                ") end; assert(not pcall(function() W:GetEntitiesInRadiusAsync(P,0,function() end) end))");
            RequireDiscoveryPublic(DiscoveryPublicPending() == 8 && EntityDiscovery.ActiveCount == 8,
                "eight global/two domain reservation not enforced");
            var Probe = new RuntimeDomain(Native, Vm, new RuntimeConfig {MaxCallbackMilliseconds=100},
                new ScriptSnapshot {EntryName="init.luau", EntrySource="return"});
            DiscoveryPublicDomains.Add(Probe);
            Probe.Facade(new FacadeSession(Gameplay, Probe.VmGenerationId, Probe.DomainLifetimeId, 4096));
            RequireDiscoveryPublic(Probe.Execute("discovery2b.probe-init", "return", 100).Status == RuntimeStatus.OK,
                "global capacity probe initialization");
            Probe.Commit(); Gameplay.CommitAddon(null, Probe.FacadeSession);
            RequireDiscoveryPublic(Probe.Execute("discovery2b.global-full", "assert(not pcall(function() " +
                "game:GetService('Workspace'):GetEntitiesInRadiusAsync(Vector3.new(0,6000,0),0,function() end) end))", 100).Status == RuntimeStatus.OK,
                "global ninth request was accepted");
            DiscoveryPublicReady();
            RequireDiscoveryPublic(DiscoveryPublicPending() == 8, "Ready requests stopped counting against global capacity");
            DiscoveryPublicExecute("capacity.ready", "assert(not pcall(function() W:GetEntitiesInRadiusAsync(P,0,function() end) end))");
            DiscoveryPublicDrain();
            DiscoveryPublicExecute("capacity.done", "assert(S.capacity1==1 and S.capacity2==1)");
            DiscoveryPublicBaseline();
            foreach (RuntimeDomain Domain in DiscoveryPublicDomains) {
                if (!ReferenceEquals(Domain, Probe)) {
                    RequireDiscoveryPublic(Domain.Execute("discovery2b.domain.done", "local S=require('state'); " +
                        "assert(S[1]==1 and S[2]==1)", 100).Status == RuntimeStatus.OK, "addon callback count/replay failed");
                }
                Gameplay.Retire(Domain.FacadeSession); Domain.Dispose();
            }
            DiscoveryPublicDomains.Clear();
            DiscoveryPublicReceipt("eight-global-two-domain-ready-reservations");
        }

        private void DiscoveryPublicLifetimes(BaseEntity Center)
        {
            string Id = DiscoveryPublicId(Center);
            DiscoveryPublicExecute("retain", "S.Old=W:GetEntityById('" + Id + "'); S.OldId=S.Old.Id");
            DiscoveryPublicExecute("stale-at-entry", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("stale", "assert(Entities==nil and Error=='DiscoveryStaleResult')") + ")");
            DiscoveryPublicReady();
            RequireDiscoveryPublic(DiscoveryPublicPending() == 1, "completion absent at pre-entry barrier");
            Center.Kill(); // Traversal already succeeded, op38 has not yet run.
            BaseEntity Replacement = DiscoveryPublicBox(0);
            NetworkableId Original = Replacement.net.ID;
            try {
                BaseNetworkable.serverEntities.UnregisterID(Replacement);
                Replacement.net.ID = new NetworkableId(UInt64.Parse(Id, CultureInfo.InvariantCulture));
                BaseNetworkable.serverEntities.RegisterID(Replacement);
                DiscoveryPublicDrain();
                DiscoveryPublicExecute("no-retarget", "assert(S.stale==1); local New=W:GetEntityById(S.OldId); " +
                    "assert(New and New~=S.Old and New.Id==S.OldId); assert(S.Old==S.Old); " +
                    "assert(not pcall(function() return S.Old.Id end)); " +
                    "assert(not pcall(function() return S.Old.Prefab end)); " +
                    "assert(not pcall(function() return S.Old.Position end))");
            }
            finally {
                // Only this task's replacement registry key is temporarily reassigned.
                BaseNetworkable.serverEntities.UnregisterID(Replacement);
                Replacement.net.ID = Original;
                BaseNetworkable.serverEntities.RegisterID(Replacement);
                Replacement.Kill();
            }
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("kill-after-traversal-before-op38-synthetic-id-reuse-no-retarget");
            BaseEntity Survivor = DiscoveryPublicBox(0);
            DiscoveryPublicExecute("later-birth", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("birth", "assert(Error==nil and #Entities==1 and Entities[1].Id=='" + DiscoveryPublicId(Survivor) + "')") + ")");
            BaseEntity Later = DiscoveryPublicBox(0);
            DiscoveryPublicReady();
            BaseEntity AfterTraversal = DiscoveryPublicBox(0);
            DiscoveryPublicDrain();
            DiscoveryPublicExecute("birth.done", "assert(S.birth==1)");
            Later.Kill(); AfterTraversal.Kill();
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("birth-watermark-and-spawn-after-traversal");
            DiscoveryPublicExecute("encounter", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("encounter", "assert(Error==nil and #Entities==0)") + ")");
            Survivor.transform.position = new Vector3(0,5005,0); // External owner-thread writer before encounter.
            DiscoveryPublicReady(); DiscoveryPublicDrain();
            DiscoveryPublicExecute("encounter.done", "assert(S.encounter==1)");
            Survivor.transform.position = new Vector3(0,5000,0);
            DiscoveryPublicExecute("observed", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("observed", "assert(Error==nil and #Entities==1 and Entities[1].Position.Y==5005)") + ")");
            DiscoveryPublicReady();
            Survivor.transform.position = new Vector3(0,5005,0); // No resampling the inclusion predicate at entry.
            DiscoveryPublicDrain();
            DiscoveryPublicExecute("observed.done", "assert(S.observed==1)");
            Survivor.Kill();
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("external-movement-before-encounter-after-observation");
        }

        private void DiscoveryPublicRetirement()
        {
            DiscoveryPublicExecute("nested", "W:GetEntitiesInRadiusAsync(P,0,function(Entities,Error) " +
                "assert(Error==nil and #Entities==0 and S.Parent==nil); S.Parent=1; " +
                "W:GetEntitiesInRadiusAsync(P,0," + DiscoveryPublicCallback("nested", "assert(Error==nil and #Entities==0)") +
                "); assert(S.nested==nil) end)");
            DiscoveryPublicReady(); DiscoveryPublicDrain();
            DiscoveryPublicExecute("nested.barrier", "assert(S.Parent==1 and S.nested==nil)");
            RequireDiscoveryPublic(DiscoveryPublicPending()==1 && EntityDiscovery.ActiveCount==1,
                "nested request ran inside the parent callback turn");
            DiscoveryPublicReady(); DiscoveryPublicDrain();
            DiscoveryPublicExecute("nested.done", "assert(S.nested==1)");
            DiscoveryPublicBaseline(); DiscoveryPublicReceipt("nested-callback-fresh-later-turn");
            DiscoveryPublicExecute("failed-replacement", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("preserved", "assert(Error==nil and #Entities==0)") + ")");
            DiscoveryPublicReady();
            FacadeSession Old = Gameplay.Active;
            ExecutionResult Failed = Host.Reload("error('expected candidate failure')");
            RequireDiscoveryPublic(Failed.Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Old, Gameplay.Active) &&
                DiscoveryPublicPending() == 1, "failed root replacement discarded old pending completion");
            DiscoveryPublicDrain(); DiscoveryPublicExecute("preserved.done", "assert(S.preserved==1)");
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("failed-root-preserves-ready-completion");
            foreach (bool Ready in new[] {false,true}) {
                DiscoveryPublicExecute("retire-root", "W:GetEntitiesInRadiusAsync(P,0,function() error('retired callback entered') end)");
                if (Ready) DiscoveryPublicReady();
                Old = Gameplay.Active;
                DiscoveryPublicReload("return");
                RequireDiscoveryPublic(Old.Disposed && !ReferenceEquals(Old, Gameplay.Active), "root authority did not retire");
                DiscoveryPublicReady(); DiscoveryPublicDrain(); DiscoveryPublicBaseline();
            }
            DiscoveryPublicReceipt("successful-root-cancels-scanning-and-ready-work");
            DiscoveryPublicExecute("cancel", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("cancel", "assert(Entities==nil and Error=='DiscoveryCancelled')") + ")");
            foreach (DiscoveryDelivery Value in DiscoveryDeliveries) if (Value != null) EntityDiscovery.Cancel(Value.Traversal);
            DiscoveryPublicReady(); DiscoveryPublicDrain();
            DiscoveryPublicExecute("cancel.done", "assert(S.cancel==1)");
            DiscoveryPublicBaseline(); DiscoveryPublicReceipt("internal-cancel-stable-error");
            DiscoveryPublicExecute("entry-deadline", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("deadline", "assert(Entities==nil and Error=='DiscoveryDeadline')") + ")");
            DiscoveryPublicReady();
            foreach (DiscoveryDelivery Value in DiscoveryDeliveries) if (Value != null) Value.Deadline = EntityDiscoveryClock - 1;
            DiscoveryPublicDrain(); DiscoveryPublicExecute("deadline.done", "assert(S.deadline==1)");
            DiscoveryPublicBaseline(); DiscoveryPublicReceipt("absolute-deadline-at-op38-injected-clock");
            DiscoveryPublicExecute("callback-fault", "W:GetEntitiesInRadiusAsync(P,0,function() " +
                "assert(S.Fault==nil); S.Fault=1; error('expected callback fault') end)");
            DiscoveryPublicReady(); DiscoveryPublicDrain(true); DiscoveryPublicDrain();
            DiscoveryPublicExecute("fault.done", "assert(S.Fault==1)");
            DiscoveryPublicBaseline(); DiscoveryPublicReceipt("callback-fault-no-replay");
            DiscoveryPublicExecute("fatal-ready", "W:GetEntitiesInRadiusAsync(P,0,function() error('old VM completion entered') end)");
            DiscoveryPublicReady();
            Old = Gameplay.Active; long VmGeneration = Host.VmGenerationId;
            ExecutionResult Fatal = Host.Execute("discovery2b.vm-fatal", "while true do end");
            RequireDiscoveryPublic(Fatal.Status == RuntimeStatus.TIMEOUT && Host.Ready && Old.Disposed &&
                Host.VmGenerationId != VmGeneration && !ReferenceEquals(Old, Gameplay.Active), "VM fatal recovery authority failed");
            DiscoveryPublicDrain(); DiscoveryPublicBaseline();
            DiscoveryPublicQuery("fresh-vm", "P", "0", null, "assert(Error==nil and #Entities==0)");
            DiscoveryPublicReceipt("vm-fatal-ready-discard-fresh-authority");
        }

        // Terminal phase: genuine sticky catalog loss, never reflection-clear
        // CatalogIncomplete or manufacture restored production continuity.
        private void DiscoveryPublicCatalogLoss()
        {
            DiscoveryPublicBaseline();
            FacadeSession Session = Gameplay.Active;
            long VmGeneration = Host.VmGenerationId;
            DiscoveryPublicExecute("catalog-empty-ready", "W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("catalogEmpty", "assert(Entities==nil and Error=='DiscoveryUnavailable')") + ")");
            DiscoveryPublicReady();
            DiscoveryDelivery Empty = null;
            foreach (DiscoveryDelivery Value in DiscoveryDeliveries) if (Value != null) Empty=Value;
            RequireDiscoveryPublic(Empty != null && Empty.Completion != null &&
                Empty.Completion.Status == EntityDiscoveryTraversal.Outcome.Success && Empty.Completion.Count == 0 &&
                DiscoveryDeliveryCurrent(Empty) && Host.SchedulerSnapshot.Queued == 1,
                "empty traversal was not successfully Ready before the catalog-loss barrier");
            DiscoveryPublicExecute("catalog-scanning", "assert(S.catalogEmpty==nil); W:GetEntitiesInRadiusAsync(P,0," +
                DiscoveryPublicCallback("catalogScanning", "assert(Entities==nil and Error=='DiscoveryUnavailable')") + ")");
            RequireDiscoveryPublic(DiscoveryPublicPending()==2 && EntityDiscovery.ActiveCount==1,
                "catalog-loss fixture requires one Ready and one untraversed request");
            EntityLifetimes.InvalidateCatalog();
            RequireDiscoveryPublic(!EntityLifetimes.CatalogReady && Host.Ready &&
                ReferenceEquals(Gameplay.Active, Session) && !Session.Disposed && Session.Active &&
                Host.VmGenerationId == VmGeneration && DiscoveryDeliveryCurrent(Empty),
                "catalog loss unexpectedly retired live callback authority");
            // The public completionAuthority keeps a normal failure deliverable,
            // even though traversal/result authority is now unavailable.
            DiscoveryPublicReady();
            RequireDiscoveryPublic(DiscoveryPublicPending()==2 && Host.SchedulerSnapshot.Queued==2,
                "catalog loss silently dropped a live-authority callback");
            DiscoveryPublicExecute("catalog.before-op38", "assert(S.catalogEmpty==nil and S.catalogScanning==nil)");
            DiscoveryPublicDrain();
            DiscoveryPublicExecute("catalog.done", "assert(S.catalogEmpty==1 and S.catalogScanning==1)");
            RequireDiscoveryPublic(Host.Ready && ReferenceEquals(Gameplay.Active, Session) &&
                Host.VmGenerationId==VmGeneration && !EntityLifetimes.CatalogReady,
                "catalog failure retired the VM or restored sticky catalog continuity");
            DiscoveryPublicBaseline();
            DiscoveryPublicReceipt("catalog-loss-empty-ready-before-op38");
            DiscoveryPublicReceipt("catalog-loss-scanning-live-callback-authority");
        }

        private void DiscoveryPublicPumpLoss()
        {
            DiscoveryPublicBaseline();
            NativeRuntime Runtime = Native;
            FacadeSession Session = Gameplay.Active;
            RequireDiscoveryPublic(Host.Ready && Runtime != null && Runtime.LiveVmCount==1 &&
                EntityDiscoveryFramePump != null && ReferenceEquals(EntityDiscoveryFramePump.Owner, this),
                "terminal pump-loss fixture requires an owned receiver and live runtime");
            // Actual engine OnDestroy, with Owner still attached. Catalog loss
            // prevents new queries here; this proves zero-pending teardown only.
            UnityEngine.Object.DestroyImmediate(EntityDiscoveryFramePump.gameObject);
            RequireDiscoveryPublic(Stopping && Host==null && Native==null && Gameplay==null &&
                EntityDiscovery==null && EntityDiscoveryFramePump==null && Session.Disposed &&
                DiscoveryPublicPending()==0 && Runtime.LiveVmCount==0 && Runtime.UnloadError==null,
                "unexpected receiver OnDestroy did not stop/release the runtime");
            foreach (string Name in new[] {"FacadeRoots", "DomainFacadeRoots"}) {
                FieldInfo Field = typeof(NativeRuntime).GetField(Name, BindingFlags.Instance | BindingFlags.NonPublic);
                RequireDiscoveryPublic(Field != null && ((IDictionary)Field.GetValue(Runtime)).Count==0,
                    "unexpected receiver OnDestroy retained native facade callback roots: " + Name);
            }
            DiscoveryPublicReceipt("unexpected-pump-ondestroy-zero-pending-runtime-and-roots-cleared");
        }

        private static byte[] DiscoveryClosurePackage(string Id, string Kind, string Source, bool Export = false)
        {
            string Dependencies = Kind == null ? "" : ",\"dependencies\":{\"required\":[" +
                (Kind == "required" ? "\"closureowner\"" : "") + "],\"optional\":[" +
                (Kind == "optional" ? "\"closureowner\"" : "") + "]}";
            using (var Output = new MemoryStream()) {
                using (var Archive = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                    Action<string, string> Add = (Name, Text) => {
                        using (var Writer = new StreamWriter(Archive.CreateEntry(Name).Open())) Writer.Write(Text);
                    };
                    Add("addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" + Dependencies +
                        (Export ? ",\"publicModules\":[\"api\"]" : "") + "}");
                    Add("init.luau", Source); Add("state.luau", "return {}");
                    if (Export) Add("api.luau", "local W=game:GetService('Workspace'); return {Read=function() return 23 end," +
                        "Borrowed=function() W:GetEntitiesInRadiusAsync(Vector3.new(0,6000,0),0,function() end) end}");
                }
                return Output.ToArray();
            }
        }

        private void DiscoveryClosureProcess(AddonRegistry Registry)
        {
            for (int Index = 0; Index < 64 && Registry.HasPending; ++Index) Registry.ProcessOne();
            RequireDiscoveryPublic(!Registry.HasPending, "closure addon graph did not converge");
        }

        private FacadeSession DiscoveryClosureSession(string[] Status)
        {
            RequireDiscoveryPublic(Status[0] == "OK" && Status[2] == "Active", "closure addon not Active: " + String.Join("|", Status));
            foreach (FacadeSession Value in Gameplay.Sessions())
                if (Value.DomainLifetimeId.ToString(CultureInfo.InvariantCulture) == Status[7]) return Value;
            throw new InvalidOperationException("exact closure addon session missing");
        }

        private void DiscoveryClosureExecute(FacadeSession Session, string Source)
        {
            ExecutionResult Result = Native.DomainExecute(Session.FacadeVm, (ulong)Session.DomainLifetimeId,
                "discovery2c.addon", Source, 100);
            RequireDiscoveryPublic(Result.Status == RuntimeStatus.OK, "closure addon execution: " + Result.Error);
        }

        private void DiscoveryPublicAddonClosure()
        {
            DiscoveryPublicBaseline();
            const string Consumer = "local S=require('state'); S.A=require('@closureowner/api'); " +
                "assert(S.A.Read()==23 and not pcall(S.A.Borrowed)); task.defer(function() " +
                "assert(S.A.Read()==23 and not pcall(S.A.Borrowed)) end)";
            const string Query = "local S=require('state'); local W=game:GetService('Workspace'); " +
                "for I=1,2 do W:GetEntitiesInRadiusAsync(Vector3.new(0,6000,0),0,function(E,R) " +
                "assert(E and #E==0 and R==nil and S.A.Read()==23); S.Count=(S.Count or 0)+1 end) end";
            using (var Registry = new AddonRegistry(Host, Native.HostLifetimeId)) {
                object Provider = new object(), Consumers = new object();
                string[] Owner = Registry.RegisterArchive(Provider, DiscoveryClosurePackage("closureowner", null, "return", true));
                DiscoveryClosureProcess(Registry);
                string[] Required = Registry.RegisterArchive(Consumers, DiscoveryClosurePackage("closurerequired", "required", Consumer));
                string[] Optional = Registry.RegisterArchive(Consumers, DiscoveryClosurePackage("closureoptional", "optional", Consumer));
                DiscoveryClosureProcess(Registry); DiscoveryPublicDrain(); DiscoveryPublicBaseline();
                FacadeSession RequiredSession = DiscoveryClosureSession(Registry.Status(Consumers, Required[1]));
                FacadeSession OptionalSession = DiscoveryClosureSession(Registry.Status(Consumers, Optional[1]));
                DiscoveryClosureExecute(RequiredSession, Query); DiscoveryClosureExecute(OptionalSession, Query);
                DiscoveryPublicReady();
                RequireDiscoveryPublic(DiscoveryPublicPending() == 4, "addon ready captures lost before replacement");
                RequireDiscoveryPublic(Registry.ReplaceArchive(Consumers, Required[1], DiscoveryClosurePackage("closurerequired", "required",
                    Consumer + "; error('failed addon candidate')"))[0] == "OK", "failed addon replacement intake");
                DiscoveryClosureProcess(Registry);
                RequireDiscoveryPublic(ReferenceEquals(RequiredSession, DiscoveryClosureSession(Registry.Status(Consumers, Required[1]))) &&
                    DiscoveryPublicPending() == 4, "failed addon replacement lost committed captures");
                DiscoveryPublicDrain(); DiscoveryPublicBaseline();
                DiscoveryClosureExecute(RequiredSession, "assert(require('state').Count==2)");
                DiscoveryClosureExecute(OptionalSession, "assert(require('state').Count==2)");
                DiscoveryPublicReceipt("failed-addon-preserves-ready-public-completions");
                // One addon is scanning, the other has already completed. Both
                // must release when required-dependency authority retires.
                DiscoveryClosureExecute(RequiredSession, Query); DiscoveryPublicReady();
                DiscoveryClosureExecute(RequiredSession, "assert(not pcall(function() " + Query + " end))");
                DiscoveryClosureExecute(OptionalSession, Query);
                RequireDiscoveryPublic(Registry.ReplaceArchive(Provider, Owner[1], DiscoveryClosurePackage("closureowner", null, "return", true))[0] == "OK",
                    "dependency replacement intake"); DiscoveryClosureProcess(Registry);
                RequireDiscoveryPublic(RequiredSession.Disposed && DiscoveryPublicPending() == 2 &&
                    ReferenceEquals(OptionalSession, DiscoveryClosureSession(Registry.Status(Consumers, Optional[1]))) &&
                    Registry.BindingStatus(Consumers, Optional[1], "closureowner")[1] == "stale", "dependency replacement retargeted old authority");
                DiscoveryPublicReady(); DiscoveryPublicDrain(); DiscoveryPublicBaseline();
                DiscoveryClosureExecute(OptionalSession, "local S=require('state'); assert(S.Count==4 and S.A.Read()==23); " +
                    "assert(not addon:IsDependencyAvailable('closureowner') and not pcall(S.A.Borrowed)); " +
                    "assert(not pcall(function() require('@closureowner/api') end))");
                RequiredSession = DiscoveryClosureSession(Registry.Status(Consumers, Required[1]));
                DiscoveryClosureExecute(RequiredSession, Query); DiscoveryPublicReady();
                RequireDiscoveryPublic(Registry.UnloadProvider(Provider) == 1 && RequiredSession.Disposed &&
                    Registry.Status(Consumers, Required[1])[2] == "Blocked" && DiscoveryPublicPending() == 0,
                    "required provider loss failed to retire query captures");
                RequireDiscoveryPublic(Registry.Status(Provider, Owner[1])[0] == "ERROR", "old provider token did not stale");
                DiscoveryPublicDrain(); DiscoveryPublicReady(); DiscoveryPublicBaseline();
                Owner = Registry.RegisterArchive(Provider, DiscoveryClosurePackage("closureowner", null, "return", true));
                DiscoveryClosureProcess(Registry); DiscoveryPublicDrain();
                RequiredSession = DiscoveryClosureSession(Registry.Status(Consumers, Required[1]));
                RequireDiscoveryPublic(Registry.BindingStatus(Consumers, Optional[1], "closureowner")[1] == "stale", "optional restoration silently rebound");
                DiscoveryClosureExecute(RequiredSession, "assert(addon:IsDependencyAvailable('closureowner'))");
                DiscoveryClosureExecute(RequiredSession, Query); DiscoveryPublicReady(); DiscoveryPublicDrain(); DiscoveryPublicBaseline();
                DiscoveryPublicReceipt("dependency-provider-loss-restoration-optional-no-rebind-retained-pure-value");
                for (int Cycle = 0; Cycle < 4; ++Cycle) {
                    DiscoveryClosureExecute(RequiredSession, Query);
                    if ((Cycle & 1) == 0) DiscoveryPublicReady();
                    RequireDiscoveryPublic(Registry.ReplaceArchive(Consumers, Required[1], DiscoveryClosurePackage("closurerequired", "required", Consumer))[0] == "OK",
                        "repeated addon replacement intake"); DiscoveryClosureProcess(Registry);
                    RequireDiscoveryPublic(RequiredSession.Disposed && DiscoveryPublicPending() == 0, "replacement retained captures");
                    DiscoveryPublicReady(); DiscoveryPublicDrain(); DiscoveryPublicBaseline();
                    RequiredSession = DiscoveryClosureSession(Registry.Status(Consumers, Required[1]));
                }
                DiscoveryClosureExecute(RequiredSession, Query); DiscoveryPublicReady();
                DiscoveryClosureExecute(OptionalSession, Query);
                RequireDiscoveryPublic(Registry.UnloadProvider(Consumers) == 2 && DiscoveryPublicPending() == 0,
                    "consumer provider unload did not clear scanning/ready work");
                DiscoveryPublicReady(); DiscoveryPublicDrain(); DiscoveryPublicBaseline();
                DiscoveryPublicReceipt("repeated-addon-replacement-provider-unload-scanning-ready-no-replay");
            }
            RequireDiscoveryPublic(Host.DomainCount == 1, "closure addon teardown did not return to root only");
            DiscoveryPublicBaseline();
        }

        private void ExecuteDiscoveryPublicFixtures()
        {
            bool Success = false, Held = false;
            bool PreviousDrain = DrainScheduled;
            NativeRuntime FixtureNative = null;
            try {
                RequireDiscoveryPublic(BasePlayer.activePlayerList.Count == 0 && Host != null && Host.Ready &&
                    Native != null && Native.AbiVersion == 0x00010005 && EntityStartupQualified && !EntityObserverBroken &&
                    Gameplay != null && Gameplay.Discovery != null && EntityDiscovery != null &&
                    EntityPositionReader != null && EntityPositionReader.Available && EntityLifetimes.CatalogReady &&
                    EntityDiscoveryFramePump != null, "isolated qualified world/native/public adapter unavailable");
                FixtureNative = Native;
                RequireDiscoveryPublic(EntityDiscoveryPolicy.MaximumQueries == 8 && EntityDiscoveryPolicy.MaximumPerDomain == 2 &&
                    EntityDiscoveryPolicy.MaximumResults == 256 && EntityDiscoveryPolicy.WorkPerTurn == 1024 &&
                    EntityDiscoveryPolicy.DeadlineSeconds == 120 && EntityDiscoveryPolicy.CatalogSlots == 262144,
                    "qualified shared policy changed");
                DrainScheduled = true; EntityDiscoveryFramePump.enabled = false; Held = true;
                // Replace only the disposable server's runtime; disk scripts/config
                // stay intact. Keep the session attached to its live FacadeWorld.
                if (Addons != null) { Addons.Dispose(); Addons = null; }
                Host.Dispose();
                DiscoveryPublicRootSnapshot = new ScriptSnapshot {EntryName="init.luau", EntrySource="return"};
                DiscoveryPublicRootSnapshot.Modules.Add("state", "return {}");
                DiscoveryPublicRootSnapshot.Modules.Add("cold", "local W=game:GetService('Workspace'); " +
                    "assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,5000,0),0,function() end) end)); " +
                    "return function(Callback) W:GetEntitiesInRadiusAsync(Vector3.new(0,5000,0),0,Callback) end");
                Host = new ScriptHost(Native, new RuntimeConfig {MaxCallbackMilliseconds=100, FrameDrainBudgetMilliseconds=20},
                    () => DiscoveryPublicRootSnapshot, Gameplay);
                DiscoveryPublicReload("return");
                Puts(DiscoveryPublicPrefix + "BEGIN real pinned compiler/native; callback admission held until explicit drain");
                DiscoveryPublicArguments(); DiscoveryPublicAuthority(); DiscoveryPublicConcurrency();
                BaseEntity Center = DiscoveryPublicBox(0);
                DiscoveryPublicQuery("one", "P", "0", null,
                    "assert(Error==nil and #Entities==1 and Entities[1].Id=='" + DiscoveryPublicId(Center) + "'); " +
                    "assert(Entities[1]==W:GetEntityById(Entities[1].Id) and Entities[1].Prefab=='" + DiscoveryPublicPrefab + "'); " +
                    "assert(Entities[1].Position==P); assert(not pcall(function() Entities[1].Id='forged' end))");
                DiscoveryPublicQuery("limit-one-success", "P", "0", "{Limit=1}", "assert(Error==nil and #Entities==1)");
                DiscoveryPublicQuery("empty", "Vector3.new(0,6000,0)", "0", "{}", "assert(Error==nil and type(Entities)=='table' and next(Entities)==nil)");
                BaseEntity Boundary = DiscoveryPublicBox(2), Outside = DiscoveryPublicBox(2.01f);
                DiscoveryPublicQuery("boundary-many", "P", "2", "{Prefab='" + DiscoveryPublicPrefab + "',Limit=2}",
                    "assert(Error==nil and #Entities==2); local Ids={}; for Index,E in Entities do " +
                    "assert(type(Index)=='number' and Index>=1 and Index<=2); Ids[E.Id]=true end; " +
                    "assert(Ids['" + DiscoveryPublicId(Center) + "'] and Ids['" + DiscoveryPublicId(Boundary) + "'] and not Ids['" + DiscoveryPublicId(Outside) + "'])");
                DiscoveryPublicQuery("prefab-case-exact", "P", "3", "{Prefab='" + DiscoveryPublicPrefab.ToUpperInvariant() + "'}", "assert(Error==nil and #Entities==0)");
                DiscoveryPublicQuery("prefab-no-prefix", "P", "3", "{Prefab='woodbox_deployed'}", "assert(Error==nil and #Entities==0)");
                DiscoveryPublicQuery("result-limit", "P", "2", "{Limit=1}", "assert(Entities==nil and Error=='DiscoveryResultLimit')");
                DiscoveryPublicExecute("options-snapshot", "local Options={Prefab='" + DiscoveryPublicPrefab + "',Limit=1}; " +
                    "W:GetEntitiesInRadiusAsync(P,2," + DiscoveryPublicCallback("snapshot", "assert(Entities==nil and Error=='DiscoveryResultLimit')") +
                    ",Options); Options.Prefab='changed'; Options.Limit=256");
                DiscoveryPublicReady(); DiscoveryPublicDrain();
                DiscoveryPublicExecute("snapshot.done", "assert(S.snapshot==1)");
                DiscoveryPublicBaseline(); DiscoveryPublicReceipt("immutable-options-snapshot");
                Boundary.Kill(); Outside.Kill();
                DiscoveryPublicLifetimes(Center);
                var Scale = new List<BaseEntity>();
                for (int Index = 0; Index < 256; ++Index) Scale.Add(DiscoveryPublicBox(0));
                DiscoveryPublicQuery("default-256", "P", "0", null,
                    "assert(Error==nil and #Entities==256); local Seen={}; for Index,E in Entities do " +
                    "assert(Index>=1 and Index<=256 and E.Prefab=='" + DiscoveryPublicPrefab + "' and not Seen[E.Id]); Seen[E.Id]=true end");
                DiscoveryPublicQuery("explicit-256", "P", "0", "{Limit=256}", "assert(Error==nil and #Entities==256)");
                Scale.Add(DiscoveryPublicBox(0));
                DiscoveryPublicQuery("default-257-fails", "P", "0", null, "assert(Entities==nil and Error=='DiscoveryResultLimit')");
                foreach (BaseEntity Value in Scale) Value.Kill();
                DiscoveryPublicRetirement();
                DiscoveryPublicAddonClosure();
                DiscoveryPublicCatalogLoss(); // Last query phase: production loss is sticky.
                DiscoveryPublicPumpLoss(); // Terminal engine fault; no VM work may follow.
                Success = true;
            }
            catch (Exception Error) { PrintError(DiscoveryPublicPrefix + "FAIL " + Error.GetType().Name + ": " + Error.Message); }
            finally {
                try {
                    foreach (RuntimeDomain Domain in DiscoveryPublicDomains) {
                        Gameplay.Retire(Domain.FacadeSession); Domain.Dispose();
                    }
                    DiscoveryPublicDomains.Clear();
                    if (Held && Host != null) { Host.Dispose(); Host = null; }
                    // Retirement marks scanning requests cancelled. Service their
                    // bounded detachment before declaring the cleanup baseline.
                    if (Held && EntityDiscovery != null) DiscoveryPublicReady();
                    foreach (BaseEntity Value in DiscoveryPublicEntities) if (Value != null && !Value.IsDestroyed) Value.Kill();
                    DiscoveryPublicEntities.Clear(); DiscoveryPublicRootSnapshot = null;
                    RequireDiscoveryPublic(DiscoveryPublicPending() == 0 && (EntityDiscovery == null || EntityDiscovery.ActiveCount == 0) &&
                        FixtureNative != null && FixtureNative.LiveVmCount == 0, "cleanup retained requests/native VM");
                    Puts(DiscoveryPublicPrefix + "CLEANUP requests=0 traversal=0 vm=0 ownedEntities=0 diskScripts=unchanged");
                }
                catch (Exception Error) { Success=false; PrintError(DiscoveryPublicPrefix + "FAIL cleanup: " + Error.Message); }
                DrainScheduled = PreviousDrain;
                if (Held && EntityDiscoveryFramePump != null) EntityDiscoveryFramePump.enabled = true;
                if (Success) Puts(DiscoveryPublicPrefix + "COMPLETE cases=" + DiscoveryPublicCases);
                timer.Once(1, () => ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"));
            }
        }
    }
}
