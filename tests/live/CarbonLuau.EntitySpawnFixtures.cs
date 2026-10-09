// B1 exact-host test package only. Never deploy as a production package.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string EntitySpawnFixturePrefix = "[CarbonLuau:EntitySpawnFixture] ";
        private const string EntitySpawnFixturePrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private readonly List<BaseEntity> EntitySpawnFixtureOwned = new List<BaseEntity>(600);
        private bool EntitySpawnFixtureRunning;
        private int EntitySpawnFixtureEarly;
        private BaseEntity EntitySpawnFixtureRecursive;
        private int EntitySpawnFixtureRecursions;
        // Request only the already-qualified Carbon 1/1 pair. Neither callback
        // supplies completion or lifetime authority to the public Signal.
        private void OnEntitySpawn(BaseNetworkable Entity) { }
        private void OnEntitySpawned(BaseNetworkable Entity)
        {
            if(!EntitySpawnFixtureRunning||!EntitySpawnFixtureOwned.Contains(Entity as BaseEntity))return;
            EntitySpawnFixtureEarly++;
            RequireEntitySpawnFixture(!Host.Busy,"early Carbon hook reentered Lua");
            if(EntitySpawnFixtureEarly==1)
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.PendingCount==0,"early hook manufactured completion");
            if(ReferenceEquals(Entity,EntitySpawnFixtureRecursive)) {
                EntitySpawnFixtureRecursive=null; EntitySpawnFixtureRecursions++;
                try{ ((BaseEntity)Entity).Spawn(); }catch(Exception){}
            }
        }
        partial void RunEntitySpawnFixtures()
        {
            NextTick(()=>{
                try{
                    ExecuteEntitySpawnFixtures();
                    // Fresh actual Unity frame; do not reset or fake the shared
                    // production frame clock after the 512-Spawn saturation.
                    NextTick(()=>{
                        try{ ExecuteEntitySpawnRecursiveFixture(); }
                        catch(Exception Error){ PrintError(EntitySpawnFixturePrefix+"FAIL "+Error); }
                        finally{ ConsoleSystem.Run(ConsoleSystem.Option.Server,"quit"); }
                    });
                }catch(Exception Error){
                    PrintError(EntitySpawnFixturePrefix+"FAIL "+Error);
                    ConsoleSystem.Run(ConsoleSystem.Option.Server,"quit");
                }
            });
        }
        private static void RequireEntitySpawnFixture(bool Value,string Message)
        {if(!Value)throw new InvalidOperationException(Message);}
        private BaseEntity NewEntitySpawnFixture()
        {
            RequireEntitySpawnFixture(EntitySpawnFixtureOwned.Count<600,"fixture object cap");
            var Entity=GameManager.server.CreateEntity(EntitySpawnFixturePrefab,new Vector3(0,100,0),Quaternion.identity,true);
            RequireEntitySpawnFixture(Entity!=null,"prefab unavailable");
            Entity.enableSaving=false; EntitySpawnFixtureOwned.Add(Entity); return Entity;
        }
        private void EntitySpawnFixtureExecute(string Source)
        {
            var Result=Host.Execute("entityspawn.live",Source);
            RequireEntitySpawnFixture(Result.Status==RuntimeStatus.OK,Result.Error);
        }
        private string EntitySpawnFixtureDrain()
        {
            var Text=new StringBuilder();
            for(int Turn=0;Turn<256&&Host.HasReadyWork;Turn++)foreach(var Result in Host.Drain()){
                RequireEntitySpawnFixture(Result.Status==RuntimeStatus.OK,Result.Error);
                RequireEntitySpawnFixture(Text.Length+Result.Logs.Length<=32768,"fixture log cap");Text.Append(Result.Logs);
            }
            RequireEntitySpawnFixture(!Host.HasReadyWork&&Gameplay.GameplayEvents.PendingCount==0&&
                Gameplay.GameplayEvents.RetainedBytes==0,"drain/reservation convergence");
            var Filtered=new StringBuilder();
            foreach(string Line in Text.ToString().Split('\n')) if(Line.StartsWith("B1LIVE|",StringComparison.Ordinal))Filtered.Append(Line).Append('\n');
            return Filtered.ToString();
        }
        private void ExecuteEntitySpawnFixtures()
        {
            RequireEntitySpawnFixture(EntityStartupQualified&&!EntityObserverBroken&&Host!=null&&Host.Ready&&
                BasePlayer.activePlayerList.Count==0&&VerifyAllEntityPatches(),"isolated qualified startup required");
            EntitySpawnFixtureRunning=true;
            int InitialListeners=Gameplay.Active.ListenerCount;
            RequireEntitySpawnFixture(BaseNetworkable.serverEntities.Count<=4096,"isolated realm cap");
            var InitialEntities=new HashSet<BaseNetworkable>();
            foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)InitialEntities.Add(Entity);
            var Stamps=new Dictionary<System.Reflection.MethodBase,byte[]>(EntityReadPatchStamps);
            try{
                EntitySpawnFixtureDrain();
                EntitySpawnFixtureExecute("local W=game:GetService('Workspace'); local N=0; local C; C=W.EntitySpawned:Connect(function(E) " +
                    "N+=1; assert(E.Prefab=='"+EntitySpawnFixturePrefab+"'); local P=E.Position; assert(P.X==P.X); " +
                    "assert(E==W:GetEntityById(E.Id)); print('B1LIVE|'..N..'|'..E.Id) end)");
                RequireEntitySpawnFixture(EntitySpawnFixtureDrain()=="","registration/startup replay");
                var First=NewEntitySpawnFixture(); ulong Before=Host.Attempted; First.Spawn();
                RequireEntitySpawnFixture(Host.Attempted==Before&&Gameplay.GameplayEvents.PendingCount==1&&EntitySpawnFixtureEarly==1,
                    "full completion not queued or synchronous Lua entry");
                string Id=First.net.ID.Value.ToString(CultureInfo.InvariantCulture);
                RequireEntitySpawnFixture(EntitySpawnFixtureDrain()=="B1LIVE|1|"+Id+"\n","exact live entity/fields/identity mismatch");
                var Rapid=NewEntitySpawnFixture(); Rapid.Spawn(); Rapid.Kill();
                RequireEntitySpawnFixture(EntitySpawnFixtureDrain()=="","rapid Spawn-Kill produced stale live proxy");
                // Real failed same-object Spawn must never publish its abandoned
                // epoch; it may invalidate the old instance, not retarget it.
                var Retry=NewEntitySpawnFixture(); Retry.Spawn(); EntitySpawnFixtureDrain();
                try{Retry.Spawn();}catch(Exception){}
                RequireEntitySpawnFixture(EntitySpawnFixtureDrain()=="","failed same-object retry emitted a live event");
                var Queued=NewEntitySpawnFixture(); Queued.Spawn();
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.PendingCount>0,"replacement fixture lacks actual queued capture");
                // Retiring the original root cancels its pending Entity work;
                // a new root sees no old Spawn history.
                var Old=Gameplay.Active; var Reload=Host.Reload();
                RequireEntitySpawnFixture(Reload.Status==RuntimeStatus.OK&&Old.Disposed,"root replacement");
                RequireEntitySpawnFixture(EntitySpawnFixtureDrain()=="","replacement replay");
                EntitySpawnFixtureExecute("game:GetService('Workspace').EntitySpawned:Connect(function(E) assert(E.Id~=nil and E.Position~=nil) end)");
                ulong Accepted=Gameplay.GameplayEvents.Accepted;
                for(int Index=0;Index<512;Index++) NewEntitySpawnFixture().Spawn();
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.PendingCount<=128&&Gameplay.GameplayEvents.RetainedBytes<=2097152&&
                    Gameplay.GameplayEvents.ProducerRejected>0,"high-volume producer/work/byte cap");
                EntitySpawnFixtureDrain();
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.Accepted-Accepted<=128,"global delivery cap");
                RequireEntitySpawnFixture(VerifyAllEntityPatches()&&EntityReadPatchStamps.Count==Stamps.Count,"D20 topology changed");
                foreach(var Pair in Stamps)RequireEntitySpawnFixture(ReferenceEquals(EntityReadPatchStamps[Pair.Key],Pair.Value),"D20 patch stamp changed");
                Puts(EntitySpawnFixturePrefix+"PASS completed outer Spawn, deferred exact Entity fields/equality, early-hook exclusion, rapid Kill, failed retry,512-Spawns bounded, replacement/no replay,D20 unchanged; historical="+InitialListeners);
            }finally{
                EntitySpawnFixtureRunning=false;
                foreach(var Entity in EntitySpawnFixtureOwned)try{
                    if(Entity!=null&&!Entity.IsDestroyed){Entity.EnableSaving(false);Entity.Kill();}
                }catch(Exception Error){PrintError(EntitySpawnFixturePrefix+"CLEANUP_FAIL "+Error.GetType().Name);}
                EntitySpawnFixtureOwned.Clear();
                RequireEntitySpawnFixture(BaseNetworkable.serverEntities.Count<=4096,"cleanup realm cap");
                foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)
                    RequireEntitySpawnFixture(InitialEntities.Contains(Entity),"new live entity survived scoped cleanup");
                Puts(EntitySpawnFixturePrefix+"NO_ORPHANS_PASS");
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.PendingCount==0&&Gameplay.GameplayEvents.RetainedBytes==0,"teardown retained transport");
                Puts(EntitySpawnFixturePrefix+"CLEANUP owned entities retired; reservations zero");
            }
        }
        private void ExecuteEntitySpawnRecursiveFixture()
        {
            RequireEntitySpawnFixture(EntityStartupQualified&&!EntityObserverBroken,"recursive probe needs qualified source");
            EntitySpawnFixtureRunning=true;
            try{
                var Queued=NewEntitySpawnFixture(); Queued.Spawn();
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.PendingCount==1,"negative probe must hold an actual deferred event");
                var Recursive=NewEntitySpawnFixture(); EntitySpawnFixtureRecursive=Recursive;
                try{Recursive.Spawn();}catch(Exception){}
                RequireEntitySpawnFixture(EntitySpawnFixtureRecursions==1&&EntityObserverBroken&&!EntityStartupQualified,
                    "recursive full Spawn did not fail closed");
                RequireEntitySpawnFixture(EntitySpawnFixtureDrain()=="","broken source admitted an old or recursive Entity callback");
                EntitySpawnFixtureExecute("local W=game:GetService('Workspace'); assert(not pcall(function() W.EntitySpawned:Connect(function() end) end)); "+
                    "assert(not pcall(function() W:GetEntityById('"+Queued.net.ID.Value.ToString(CultureInfo.InvariantCulture)+"') end))");
                Puts(EntitySpawnFixturePrefix+"PASS recursive full Spawn rejects source, cancels pending Entity admission and rejects new registration/lookup; injected one-shot recursion through actual Carbon hook");
            }finally{
                EntitySpawnFixtureRunning=false; EntitySpawnFixtureRecursive=null;
                foreach(var Entity in EntitySpawnFixtureOwned)if(Entity!=null&&!Entity.IsDestroyed){Entity.EnableSaving(false);Entity.Kill();}
                foreach(var Entity in EntitySpawnFixtureOwned)
                    RequireEntitySpawnFixture(Entity==null||Entity.IsDestroyed,"recursive probe orphan");
                EntitySpawnFixtureOwned.Clear();
                RequireEntitySpawnFixture(Gameplay.GameplayEvents.PendingCount==0&&Gameplay.GameplayEvents.RetainedBytes==0,"negative probe retained transport");
                Puts(EntitySpawnFixturePrefix+"RECURSIVE_CLEANUP_PASS exact scoped actors killed; reservations zero");
            }
        }
    }
}
