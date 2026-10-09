// B3 production-source public qualification on the disposable pinned hosts only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string EntityDestroyedFixturePrefix="[CarbonLuau:EntityDestroyedFixture] ";
        private const string EntityDestroyedFixturePrefab="assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private readonly List<BaseEntity> EntityDestroyedFixtureOwned=new List<BaseEntity>(600);
        private readonly Dictionary<BaseEntity,NetworkableId> EntityDestroyedFixtureIds=new Dictionary<BaseEntity,NetworkableId>();
        private readonly Dictionary<BaseEntity,GameObject> EntityDestroyedFixtureObjects=new Dictionary<BaseEntity,GameObject>();
        private HashSet<BaseNetworkable> EntityDestroyedFixtureInitial;
        private Dictionary<MethodBase,byte[]> EntityDestroyedFixtureStamps;
        private BaseNetworkable EntityDestroyedFixtureWatched;
        private bool EntityDestroyedFixtureVeto,EntityDestroyedFixtureNested;
        private int EntityDestroyedFixtureHooks,EntityDestroyedFixtureRecursions,EntityDestroyedFixtureBaseline;
        private Harmony EntityDestroyedFixtureHarmony;
        private static BaseNetworkable EntityDestroyedFixtureResetActor;
        private static Poolable EntityDestroyedFixturePoolTarget;
        private void OnEntitySpawn(BaseNetworkable Entity){}
        private void OnEntitySpawned(BaseNetworkable Entity){}
        private object OnEntityKill(BaseNetworkable Entity)
        {
            if(!ReferenceEquals(Entity,EntityDestroyedFixtureWatched))return null;
            EntityDestroyedFixtureHooks++;
            EntityDestroyedFixtureCheck(!Host.Busy,"Kill hook recursively entered Luau");
            if(EntityDestroyedFixtureNested){
                EntityDestroyedFixtureNested=false;EntityDestroyedFixtureRecursions++;Entity.Kill();
            }
            return EntityDestroyedFixtureVeto?(object)true:null;
        }
        partial void RunEntitySpawnFixtures()
        {
            EntityDestroyedFixturePoolPinEvidence();
            string Mode=Environment.GetEnvironmentVariable("CARBONLUAU_B3_FIXTURE_MODE");
            if(Mode=="real-pool"){
                NextTick(()=>EntityDestroyedFixtureStage(()=>{EntityDestroyedFixtureRealPool();EntityDestroyedFixtureFinish(null);}));return;
            }
            if(Mode=="no-listener-poll"){
                NextTick(()=>EntityDestroyedFixtureStage(EntityDestroyedFixtureNoListenerPoll));return;
            }
            if(!String.IsNullOrEmpty(Mode)&&Mode!="normal"){
                NextTick(()=>EntityDestroyedFixtureStage(()=>{EntityDestroyedFixtureFault(Mode);EntityDestroyedFixtureFinish(null);}));return;
            }
            NextTick(()=>EntityDestroyedFixtureStage(()=>{
                EntityDestroyedFixtureBasic();
                NextTick(()=>EntityDestroyedFixtureStage(()=>{
                    EntityDestroyedFixtureNative(()=>NextTick(()=>EntityDestroyedFixtureStage(()=>{
                        EntityDestroyedFixtureStress();
                        NextTick(()=>EntityDestroyedFixtureStage(()=>{
                            EntityDestroyedFixtureDrift();EntityDestroyedFixtureFinish(null);
                        }));
                    })));
                }));
            }));
        }
        private void EntityDestroyedFixturePoolPinEvidence()
        {
            try{
                bool Linux=Environment.OSVersion.Platform==PlatformID.Unix;
                MethodInfo Method=PoolDestroyMethod??typeof(PrefabPool).GetMethod("Push",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
                    null,new[]{typeof(Poolable)},null);
                EntityDestroyedFixtureCheck(Method!=null,"diagnostic actual Pool.Push method missing");
                MethodBody Body=Method.GetMethodBody();byte[] Bytes=Body==null?new byte[0]:Body.GetILAsByteArray();string Sha;
                using(var Hash=SHA256.Create())Sha=BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant();
                var Parameters=Method.GetParameters();
                Puts(EntityDestroyedFixturePrefix+"POOL_LOADED_PIN method="+Method.DeclaringType.FullName+"."+Method.Name+
                    " resolved="+(PoolDestroyMethod!=null)+" token="+Method.MetadataToken.ToString("X8",CultureInfo.InvariantCulture)+
                    " il_length="+Bytes.Length+" eh="+(Body==null?-1:Body.ExceptionHandlingClauses.Count)+" sha256="+Sha+
                    " mvid="+Method.Module.ModuleVersionId.ToString("D")+" parameter="+(Parameters.Length==1?Parameters[0].ParameterType.FullName:"invalid")+
                    " asm_hash="+HasHash(Method.Module.Assembly,Linux?LinuxRustHash:WindowsRustHash)+
                    " pinned="+PoolDestroyPinned()+" marker="+PoolDestroyMarkerValid+" il_hex="+BitConverter.ToString(Bytes));
            }catch(Exception Error){PrintError(EntityDestroyedFixturePrefix+"POOL_PIN_DIAGNOSTIC_FAIL "+Error.GetType().FullName+" "+Error.Message);}
        }
        private void EntityDestroyedFixtureStage(Action Action)
        {try{Action();}catch(Exception Error){EntityDestroyedFixtureFinish(Error);}}
        private static void EntityDestroyedFixtureCheck(bool Value,string Message)
        {if(!Value)throw new InvalidOperationException(Message);}
        private BaseEntity NewEntityDestroyedFixture(bool Spawn=true,bool Active=true)
        {
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureOwned.Count<600,"owned actor cap");
            var Entity=GameManager.server.CreateEntity(EntityDestroyedFixturePrefab,new Vector3(0,100,0),Quaternion.identity,Active);
            EntityDestroyedFixtureCheck(Entity!=null,"fixture prefab missing");
            Entity.enableSaving=false;EntityDestroyedFixtureOwned.Add(Entity);EntityDestroyedFixtureObjects[Entity]=Entity.gameObject;
            if(Spawn){Entity.Spawn();EntityDestroyedFixtureIds[Entity]=Entity.net.ID;}
            return Entity;
        }
        private void EntityDestroyedFixtureExecute(string Source)
        {
            var Result=Host.Execute("entitydestroyed.live",Source);
            EntityDestroyedFixtureCheck(Result.Status==RuntimeStatus.OK,Result.Error);
        }
        private void EntityDestroyedFixtureSubscribe()
        {
            EntityDestroyedFixtureExecute("local W=game:GetService('Workspace'); W.EntityDestroyed:Connect(function(C) "+
                "assert(type(C)=='table' and type(C.Id)=='string' and C.Prefab=='"+EntityDestroyedFixturePrefab+"'); "+
                "assert(not pcall(function() C.Id='changed' end)); assert(C.IsAlive==nil and C.Token==nil); "+
                "if C.Position~=nil then assert(C.Position.X==C.Position.X) end; "+
                "print('B3LIVE|'..C.Id..'|'..(C.Position==nil and '0' or '1')) end)");
        }
        private string EntityDestroyedFixtureDrain()
        {
            var Text=new StringBuilder();
            for(int Turn=0;Turn<256&&Host.HasReadyWork;Turn++)foreach(var Result in Host.Drain()){
                EntityDestroyedFixtureCheck(Result.Status==RuntimeStatus.OK,Result.Error);
                EntityDestroyedFixtureCheck(Text.Length+Result.Logs.Length<=32768,"fixture log byte cap");Text.Append(Result.Logs);
            }
            EntityDestroyedFixtureCheck(!Host.HasReadyWork&&Gameplay.GameplayEvents.PendingCount==0&&
                Gameplay.GameplayEvents.RetainedBytes==0,"pending transport convergence");
            var Filtered=new StringBuilder();
            foreach(string Line in Text.ToString().Split('\n'))if(Line.StartsWith("B3LIVE|",StringComparison.Ordinal))Filtered.Append(Line).Append('\n');
            // Preserve actual public callback receipts even when a private drain
            // consumes them before the ordinary Carbon logging adapter.
            foreach(string Line in Filtered.ToString().Split('\n'))if(Line.Length!=0)Puts(EntityDestroyedFixturePrefix+Line);
            return Filtered.ToString();
        }
        private static int EntityDestroyedFixtureLines(string Value)
        {int Count=0;foreach(char Character in Value)if(Character=='\n')Count++;return Count;}
        private static void EntityDestroyedFixtureInvoke(BaseEntity Entity,string Name)
        {typeof(BaseNetworkable).GetMethod(Name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null).Invoke(Entity,null);}
        private void EntityDestroyedFixtureKill(BaseEntity Entity)
        {
            ulong Before=Host.Attempted;Entity.Kill();
            EntityDestroyedFixtureCheck(Host.Attempted==Before,"synchronous host Kill entered Luau");
        }
        private void EntityDestroyedFixtureBasic()
        {
            EntityDestroyedFixtureCheck(EntityStartupQualified&&!EntityObserverBroken&&DestroySourceQualified&&EntityDestroyedAvailable()&&
                Host.Ready&&BasePlayer.activePlayerList.Count==0&&BaseNetworkable.serverEntities.Count<=4096&&VerifyAllEntityPatches(),
                "isolated qualified B3/D20 host required");
            EntityDestroyedFixtureInitial=new HashSet<BaseNetworkable>();
            foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)EntityDestroyedFixtureInitial.Add(Entity);
            EntityDestroyedFixtureStamps=new Dictionary<MethodBase,byte[]>(EntityReadPatchStamps);
            EntityDestroyedFixtureBaseline=DestroyHeldWatches.Count;
            EntityDestroyedFixtureDrain();
            EntityDestroyedFixtureNoListenerAndPool();
            EntityDestroyedFixtureSubscribe();
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","registration replayed startup destruction");
            var Veto=NewEntityDestroyedFixture();EntityDestroyedFixtureWatched=Veto;EntityDestroyedFixtureVeto=true;
            long Certified=DestroyCertified;ulong PositionReads=Gameplay.GameplayEvents.DestroyPositionAccepted;
            for(int Attempt=0;Attempt<512;Attempt++)EntityDestroyedFixtureKill(Veto);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureHooks==512&&!Veto.IsDestroyed&&DestroyCertified==Certified&&
                Gameplay.GameplayEvents.PendingCount==0,"veto became successful destruction");
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.DestroyPositionAccepted-PositionReads<=128&&
                Gameplay.GameplayEvents.DestroyPositionRejected>0,"512 veto attempts exceeded bounded optional TRS work");
            EntityDestroyedFixtureVeto=false;EntityDestroyedFixtureKill(Veto);
            EntityDestroyedFixtureCheck(Veto.IsDestroyed&&Veto.net==null&&Gameplay.GameplayEvents.PendingCount==1,"normal Kill completion missing");
            string Id=EntityDestroyedFixtureIds[Veto].Value.ToString(CultureInfo.InvariantCulture);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain().StartsWith("B3LIVE|"+Id+"|",StringComparison.Ordinal),"retired snapshot was not delivered");
            var Term=NewEntityDestroyedFixture();EntityDestroyedFixtureInvoke(Term,"TerminateOnServer");
            EntityDestroyedFixtureCheck(!Term.IsDestroyed&&Term.net==null&&!Term.gameObject.activeInHierarchy&&
                Gameplay.GameplayEvents.PendingCount==1,"successful alternate Terminate not observed");
            EntityDestroyedFixtureInvoke(Term,"EntityDestroy");
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1,"alternate removal duplicated/lost");
            EntityDestroyedFixtureResetFailure();
            var Nested=NewEntityDestroyedFixture();EntityDestroyedFixtureWatched=Nested;EntityDestroyedFixtureNested=true;
            EntityDestroyedFixtureKill(Nested);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureRecursions==1&&EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1,"nested actual Kill duplicated/lost");
            EntityDestroyedFixtureWatched=null;
            var Churn=NewEntityDestroyedFixture();var ChurnId=EntityDestroyedFixtureIds[Churn];
            BaseNetworkable.serverEntities.UnregisterID(Churn);BaseNetworkable.serverEntities.RegisterID(Churn);
            EntityDestroyedFixtureCheck(ReferenceEquals(BaseNetworkable.serverEntities.Find(ChurnId),Churn)&&
                Gameplay.GameplayEvents.PendingCount==0,"registry churn manufactured physical removal");
            EntityDestroyedFixtureExecute("local C=game:GetService('Workspace').EntityDestroyed:Connect(function() error('disconnected listener delivered') end); C:Disconnect(); C:Disconnect()");
            EntityDestroyedFixtureKill(Churn);EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1,"disconnect/churn semantics");
            var Rapid=NewEntityDestroyedFixture();EntityDestroyedFixtureKill(Rapid);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1,"rapid Spawn-Kill lost terminal snapshot");
            var Old=NewEntityDestroyedFixture();var OldId=EntityDestroyedFixtureIds[Old];DestroyWatch OldWatch;
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(Old,out OldWatch),"old lifetime lacks watch");
            ulong OldEpoch=OldWatch.EpochIdentity;EntityDestroyedFixtureKill(Old);
            var New=NewEntityDestroyedFixture(false);New.InitLoad(OldId);New.Spawn();EntityDestroyedFixtureIds[New]=New.net.ID;
            DestroyWatch NewWatch;
            EntityDestroyedFixtureCheck(New.net.ID.Value==OldId.Value&&DestroyWatches.TryGetValue(New,out NewWatch)&&
                NewWatch.EpochIdentity!=OldEpoch,"same-ID replacement lacks distinct qualified incarnation");
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain().StartsWith("B3LIVE|"+OldId.Value.ToString(CultureInfo.InvariantCulture)+"|",StringComparison.Ordinal),
                "old removal snapshot resolved new ID lifetime");
            EntityDestroyedFixtureKill(New);EntityDestroyedFixtureDrain();
            var Queued=NewEntityDestroyedFixture();EntityDestroyedFixtureKill(Queued);
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==1,"replacement needs queued public event");
            var Previous=Gameplay.Active;var Reload=Host.Reload();
            EntityDestroyedFixtureCheck(Reload.Status==RuntimeStatus.OK&&Previous.Disposed&&EntityDestroyedFixtureDrain()=="","root replacement replay/cancellation");
            EntityDestroyedFixtureSubscribe();EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","new root historical replay");
            Puts(EntityDestroyedFixturePrefix+"PASS basic veto,completed Kill,alternate Terminate,nested dedup,registry churn,Disconnect,rapid Spawn-Kill,same-ID new lifetime,root replacement/cancellation,no replay");
        }
        private void EntityDestroyedFixtureNoListenerAndPool()
        {
            EntityDestroyedFixtureCheck(!Gameplay.HasGameplayDemand("entitydestroyed"),"no-listener qualification needs zero B3 demand");
            ulong Accepted=Gameplay.GameplayEvents.Accepted,Positions=Gameplay.GameplayEvents.DestroyPositionAccepted;
            int Watches=DestroyHeldWatches.Count,Bytes=DestroySnapshotBytes;
            for(int Index=0;Index<64;Index++){var Entity=NewEntityDestroyedFixture();EntityDestroyedFixtureKill(Entity);}
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.Accepted==Accepted&&Gameplay.GameplayEvents.PendingCount==0&&
                Gameplay.GameplayEvents.RetainedBytes==0&&DestroyHeldWatches.Count==Watches&&DestroySnapshotBytes==Bytes,
                "no-listener completion leaked watch/snapshot/event memory");
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.DestroyPositionAccepted==Positions,"no-listener Kill performed optional TRS observations");
            int Observed=0,Inspected=0;
            foreach(BaseNetworkable Networkable in BaseNetworkable.serverEntities){
                EntityDestroyedFixtureCheck(++Inspected<=4096,"pool scan work cap");
                var Entity=Networkable as BaseEntity;
                if(Entity!=null&&Entity.GetComponent<Poolable>()!=null&&Entity.gameObject.SupportsPooling())Observed++;
            }
            // Existing ordinary server preprocessing strips client Poolable
            // components. Never attach one or corrupt pool fields to force ABA.
            var First=NewEntityDestroyedFixture();bool Supports=First.GetComponent<Poolable>()!=null&&First.gameObject.SupportsPooling();
            DestroyWatch FirstWatch;EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(First,out FirstWatch),"pool probe original watch");
            ulong Epoch=FirstWatch.EpochIdentity;int Instance=First.GetInstanceID();EntityDestroyedFixtureKill(First);
            var Second=NewEntityDestroyedFixture();bool Reused=ReferenceEquals(First,Second);
            if(Reused)EntityDestroyedFixtureCheck(Second.GetInstanceID()==Instance&&DestroyWatches.TryGetValue(Second,out var Current)&&
                Current!=null&&!Current.Closed&&Current.EpochIdentity!=Epoch,"naturally pooled incarnation lacked fresh cancellation watch");
            EntityDestroyedFixtureKill(Second);
            EntityDestroyedFixtureCheck(DestroyHeldWatches.Count==Watches&&DestroySnapshotBytes==Bytes,"pool probe watch convergence");
            Puts(EntityDestroyedFixturePrefix+"NO_LISTENER_PASS 64 completions plus pool probe,zero events/reservations,watch/bytes converge");
            Puts(EntityDestroyedFixturePrefix+"POOL_EVIDENCE realm_inspected="+Inspected+" real_poolable_entities="+Observed+
                " owned_pool_supported="+Supports+" same_object_reuse="+(Reused?"OBSERVED":"NOT_OBSERVED")+
                "; no fabricated components or pool fields");
            long Visits=DestroyPollVisits,Began=System.Diagnostics.Stopwatch.GetTimestamp();int Frame=UnityEngine.Time.frameCount;
            for(int Repeat=0;Repeat<1000;Repeat++)RunEntityDestroyedFrame();
            EntityDestroyedFixtureCheck(UnityEngine.Time.frameCount==Frame&&DestroyPollVisits-Visits<=128&&DestroyMaximumPollVisits<=128,
                "same-frame repeated intake exceeded one global watch sweep allowance");
            Puts(EntityDestroyedFixturePrefix+"SAME_FRAME_POLL_PASS held="+DestroyHeldWatches.Count+" repeats=1000 visits="+(DestroyPollVisits-Visits)+
                " total_ms="+(1000.0*(System.Diagnostics.Stopwatch.GetTimestamp()-Began)/System.Diagnostics.Stopwatch.Frequency).ToString("R",CultureInfo.InvariantCulture)+
                "; no frame clock reset or synthetic catch-up");
        }
        private static void EntityDestroyedFixtureThrowReset(BaseNetworkable __instance)
        {if(ReferenceEquals(__instance,EntityDestroyedFixtureResetActor))throw new InvalidOperationException("B3 injected owned ResetState tail failure");}
        private void EntityDestroyedFixtureResetFailure()
        {
            var Actor=NewEntityDestroyedFixture();EntityDestroyedFixtureResetActor=Actor;
            var Reset=Actor.GetType().GetMethod("ResetState",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
            EntityDestroyedFixtureCheck(Reset!=null,"exact owned virtual ResetState body missing");
            Reset=Reset.DeclaringType.GetMethod("ResetState",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,
                null,Type.EmptyTypes,null);
            EntityDestroyedFixtureCheck(Reset!=null,"declared owned virtual ResetState body missing");
            var Patch=new Harmony("CarbonLuau.Test.EntityDestroyed.ResetTail");bool Threw=false;
            try{
                Patch.Patch(Reset,prefix:new HarmonyMethod(typeof(CarbonLuau).GetMethod("EntityDestroyedFixtureThrowReset",BindingFlags.Static|BindingFlags.NonPublic)));
                try{Actor.Kill();}catch(InvalidOperationException Error){Threw=Error.Message=="B3 injected owned ResetState tail failure";}
                EntityDestroyedFixtureCheck(Threw&&Actor.IsDestroyed&&Actor.net==null&&!Actor.gameObject.activeInHierarchy&&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(EntityDestroyedFixtureIds[Actor]),null)&&
                    Gameplay.GameplayEvents.PendingCount==1,"later ResetState failure negated completed Terminate proof");
            }finally{EntityDestroyedFixtureResetActor=null;Patch.UnpatchAll(Patch.Id);}
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1&&EntityDestroyedAvailable()&&VerifyAllEntityPatches(),
                "completed removal lost/duplicated after injected later reset failure");
            UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","later physical cleanup replayed already-certified removal");
            Puts(EntityDestroyedFixturePrefix+"RESET_TAIL_PASS actual completed Terminate survives exact owned injected later ResetState failure,one snapshot,no physical-cleanup replay");
        }
        private void EntityDestroyedFixtureRemoveNetwork(BaseEntity Entity)
        {
            if(!ReferenceEquals(Entity,null)&&Entity.net!=null){
                BaseNetworkable.serverEntities.UnregisterID(Entity);Network.Net.sv.DestroyNetworkable(ref Entity.net);
            }
        }
        private DestroyWatch EntityDestroyedFixtureNativeWatch(BaseEntity Actor,List<DestroyWatch> Watches)
        {
            DestroyWatch Watch;
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(Actor,out Watch)&&!Watch.Closed,"original native case lacks active watch");
            Watches.Add(Watch);
            Puts(EntityDestroyedFixturePrefix+"EXPECT_NATIVE|"+EntityDestroyedFixtureIds[Actor].Value.ToString(CultureInfo.InvariantCulture));
            return Watch;
        }
        private void EntityDestroyedFixtureNative(Action Continue)
        {
            var Watches=new List<DestroyWatch>(8);
            var IntentKill=NewEntityDestroyedFixture();DestroyWatch IntentWatch;
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(IntentKill,out IntentWatch),"intent probe watch missing");
            DestroyNativeCancelled(IntentWatch);
            EntityDestroyedFixtureCheck(!IntentWatch.Closed&&Gameplay.GameplayEvents.PendingCount==0,"cancellation intent published/released live original");
            EntityDestroyedFixtureKill(IntentKill);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1,"intent notification prevented later actual Kill");
            var IntentNative=NewEntityDestroyedFixture();var IntentNativeWatch=EntityDestroyedFixtureNativeWatch(IntentNative,Watches);
            DestroyNativeCancelled(IntentNativeWatch);
            EntityDestroyedFixtureCheck(!IntentNativeWatch.Closed&&Gameplay.GameplayEvents.PendingCount==0,"intent-only notification manufactured native completion");
            UnityEngine.Object.DestroyImmediate(IntentNative.gameObject);
            var Immediate=NewEntityDestroyedFixture();EntityDestroyedFixtureNativeWatch(Immediate,Watches);
            EntityDestroyedFixtureRemoveNetwork(Immediate);ulong Before=Host.Attempted;
            UnityEngine.Object.DestroyImmediate(Immediate.gameObject);
            EntityDestroyedFixtureCheck(Host.Attempted==Before&&Immediate==null,"immediate native deletion/synchronous entry");
            var Partial=NewEntityDestroyedFixture();EntityDestroyedFixtureNativeWatch(Partial,Watches);
            UnityEngine.Object.DestroyImmediate(Partial.gameObject);
            EntityDestroyedFixtureCheck(Partial==null&&!ReferenceEquals(Partial,null)&&Partial.net!=null&&
                ReferenceEquals(BaseNetworkable.serverEntities.Find(EntityDestroyedFixtureIds[Partial]),Partial),"raw original bookkeeping ghost missing");
            var Inactive=NewEntityDestroyedFixture();EntityDestroyedFixtureNativeWatch(Inactive,Watches);
            Inactive.gameObject.SetActive(false);
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==0,"deactivation became completed destruction");
            UnityEngine.Object.DestroyImmediate(Inactive.gameObject);
            var Component=NewEntityDestroyedFixture();EntityDestroyedFixtureNativeWatch(Component,Watches);
            UnityEngine.Object.DestroyImmediate(Component);
            EntityDestroyedFixtureCheck(Component==null&&EntityDestroyedFixtureObjects[Component]!=null,"component-only native deletion missing");
            var Never=NewEntityDestroyedFixture(false,false);bool InitialActive=Never.gameObject.activeSelf;string NeverFailure="none";
            try{Never.Spawn();}catch(Exception Error){NeverFailure=Error.GetType().Name;}
            if(Never.net!=null)EntityDestroyedFixtureIds[Never]=Never.net.ID;
            DestroyWatch NeverWatch;bool Admitted=DestroyWatches.TryGetValue(Never,out NeverWatch)&&!NeverWatch.Closed;
            bool SpawnActive=Never.gameObject.activeSelf;
            if(Admitted)EntityDestroyedFixtureNativeWatch(Never,Watches);
            UnityEngine.Object.DestroyImmediate(Never.gameObject);
            var Deferred=NewEntityDestroyedFixture();EntityDestroyedFixtureNativeWatch(Deferred,Watches);
            EntityDestroyedFixtureRemoveNetwork(Deferred);Before=Host.Attempted;UnityEngine.Object.Destroy(Deferred.gameObject);
            EntityDestroyedFixtureCheck(Host.Attempted==Before&&Gameplay.GameplayEvents.PendingCount==0,"native intent entered Luau or prematurely published completion");
            NetworkableId WeakId;DestroyWatch WeakWatch;var Weak=EntityDestroyedFixtureWeakNative(out WeakId,out WeakWatch);
            long RejectedBefore=DestroyRejected;
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            bool Collected=!Weak.IsAlive;
            Watches.Add(WeakWatch);
            EntityDestroyedFixtureWaitReleased(Watches,0,()=>{
                foreach(var Watch in Watches)if(!ReferenceEquals(Watch,WeakWatch))
                    EntityDestroyedFixtureCheck(Watch.Certified,"released native case lacks exact original completion fact");
                bool Missing=!Weak.IsAlive;
                if(!WeakWatch.Certified)EntityDestroyedFixtureCheck(Missing&&DestroyRejected>RejectedBefore,"uncertified original was not an accounted weak-proof drop");
                else Puts(EntityDestroyedFixturePrefix+"EXPECT_NATIVE|"+WeakId.Value.ToString(CultureInfo.InvariantCulture));
                EntityDestroyedFixtureDrain();
                foreach(var Actor in new[]{IntentNative,Partial,Inactive,Component,Never})EntityDestroyedFixtureRemoveNetwork(Actor);
                EntityDestroyedFixtureCheck(Deferred==null,"deferred actual native deletion incomplete");
                Puts(EntityDestroyedFixturePrefix+"INTENT_PASS injected cancellation intent alone emits nothing,keeps watch,later actual Kill/native deletion qualified");
                Puts(EntityDestroyedFixturePrefix+"INACTIVE_COMPONENT_PASS active_then_disabled and component deletion; startActive_false initial_active="+InitialActive+
                    " after_spawn_active="+SpawnActive+" qualified_watch="+Admitted+" spawn_failure="+NeverFailure+"; actual Update proof,no fabricated activation");
                Puts(EntityDestroyedFixturePrefix+"WEAK_GC_EVIDENCE original_collected="+((Collected||Missing)?"OBSERVED":"NOT_OBSERVED")+
                    " native_certified="+WeakWatch.Certified+
                    " proof_rejected_delta="+(DestroyRejected-RejectedBefore)+"; actual forced GC before certification,no fabricated weak target");
                Puts(EntityDestroyedFixturePrefix+"PASS native completed alternate and raw destruction with bookkeeping ghosts,exact fresh original invalidity,nil Position,no cleanup claim");
                Puts(EntityDestroyedFixturePrefix+"POLL_SERVICE_PASS turns="+DestroyPollTurns+" visits="+DestroyPollVisits+" max_visits="+DestroyMaximumPollVisits+
                    " max_turn_ms="+(1000.0*DestroyMaximumPollTicks/System.Diagnostics.Stopwatch.Frequency).ToString("R",CultureInfo.InvariantCulture)+
                    " native_polled="+DestroyPolled+"; fixed global128/Unityframe,not peraddon,no public timing SLA");
                Continue();
            });
        }
        private void EntityDestroyedFixtureWaitReleased(List<DestroyWatch> Watches,int Turn,Action Complete)
        {
            EntityDestroyedFixtureCheck(DestroyMaximumPollVisits<=128,"actual Update exceeded global 128-slot bound");
            bool Closed=true;foreach(var Watch in Watches)if(!Watch.Closed)Closed=false;
            if(Closed){Complete();return;}
            EntityDestroyedFixtureCheck(Turn<256,"isolated original watch failed bounded fixture progress");
            NextTick(()=>EntityDestroyedFixtureStage(()=>EntityDestroyedFixtureWaitReleased(Watches,Turn+1,Complete)));
        }
        private void EntityDestroyedFixtureNoListenerPoll()
        {
            EntityDestroyedFixtureCheck(EntityStartupQualified&&!EntityObserverBroken&&EntityDestroyedAvailable()&&Host.Ready&&
                BasePlayer.activePlayerList.Count==0&&BaseNetworkable.serverEntities.Count<=4096&&!Gameplay.HasGameplayDemand("entitydestroyed"),
                "isolated fresh no-demand poll source");
            EntityDestroyedFixtureInitial=new HashSet<BaseNetworkable>();
            foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)EntityDestroyedFixtureInitial.Add(Entity);
            EntityDestroyedFixtureBaseline=DestroyHeldWatches.Count;
            ulong Accepted=Gameplay.GameplayEvents.Accepted;int Bytes=DestroySnapshotBytes;
            var Actor=NewEntityDestroyedFixture(false,false);
            EntityDestroyedFixtureCheck(!Actor.gameObject.activeSelf,"no-demand probe not initially inactive");
            Actor.Spawn();EntityDestroyedFixtureIds[Actor]=Actor.net.ID;DestroyWatch Watch=null;
            EntityDestroyedFixtureCheck(!Actor.gameObject.activeSelf&&DestroyWatches.TryGetValue(Actor,out Watch),"inactive completed epoch not watched");
            UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            // Registration after physical destruction but before polling must
            // not turn the future-only Signal into delayed historical replay.
            EntityDestroyedFixtureSubscribe();
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","late registration replayed native intent");
            EntityDestroyedFixtureWaitReleased(new List<DestroyWatch>{Watch},0,()=>{
                EntityDestroyedFixtureCheck(Watch.Certified&&Gameplay.GameplayEvents.Accepted==Accepted&&Gameplay.GameplayEvents.PendingCount==0&&
                    Gameplay.GameplayEvents.RetainedBytes==0&&DestroyHeldWatches.Count<=EntityDestroyedFixtureBaseline&&DestroySnapshotBytes<=Bytes,
                    "pre-subscription destruction replayed or retained watch memory");
                EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","later subscriber replayed polled historical destruction");
                EntityDestroyedFixtureRemoveNetwork(Actor);
                Puts(EntityDestroyedFixturePrefix+"NO_LISTENER_POLL_PASS never-active original certified,watch/text refund,pre-poll lateConnect zero replay; "+EntityDestroyedStatus);
                EntityDestroyedFixtureFinish(null);
            });
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference EntityDestroyedFixtureWeakNative(out NetworkableId Id,out DestroyWatch Watch)
        {
            var Actor=NewEntityDestroyedFixture();Id=EntityDestroyedFixtureIds[Actor];
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(Actor,out Watch),"weak-GC original watch");
            EntityDestroyedFixtureRemoveNetwork(Actor);UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            var Weak=new WeakReference(Actor);
            EntityDestroyedFixtureOwned.Remove(Actor);EntityDestroyedFixtureIds.Remove(Actor);EntityDestroyedFixtureObjects.Remove(Actor);
            EntityDestroyedFixtureCheck(ReferenceEquals(BaseNetworkable.serverEntities.Find(Id),null),"weak-GC actor registry cleanup");
            return Weak;
        }
        private void EntityDestroyedFixtureFault(string Mode)
        {
            EntityDestroyedFixtureCheck(Mode=="watch-bytes"||Mode=="watch-count"||Mode=="depth"||Mode=="offthread"||Mode=="teardown",
                "unknown selected fault mode");
            EntityDestroyedFixtureCheck(EntityStartupQualified&&!EntityObserverBroken&&EntityDestroyedAvailable()&&Host.Ready&&
                BasePlayer.activePlayerList.Count==0&&BaseNetworkable.serverEntities.Count<=4096&&VerifyAllEntityPatches(),"isolated fresh fault source");
            EntityDestroyedFixtureInitial=new HashSet<BaseNetworkable>();
            foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)EntityDestroyedFixtureInitial.Add(Entity);
            EntityDestroyedFixtureBaseline=DestroyHeldWatches.Count;
            EntityDestroyedFixtureSubscribe();var Queued=NewEntityDestroyedFixture();EntityDestroyedFixtureKill(Queued);
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==1,"fault gate needs real queued snapshot");
            var Target=NewEntityDestroyedFixture();DestroyWatch Watch;
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(Target,out Watch)&&Watch!=null&&!Watch.Closed,"fault target watch");
            int SavedBytes=DestroySnapshotBytes,SavedDepth=DestroyDepth;var Injected=new List<int>();
            try{
                if(Mode=="watch-bytes"){
                    DestroySnapshotBytes=DestroySnapshotLimit;
                    NewEntityDestroyedFixture();
                }else if(Mode=="watch-count"){
                    // Bounded predicate injection, not 262144 physical entities
                    // or proof of maximum-host heap consumption.
                    int Missing=DestroyWatchLimit-DestroyHeldWatches.Count;
                    for(int Index=0;Index<Missing;Index++){int Key=-Index-1;DestroyHeldWatches.Add(Key,Watch);Injected.Add(Key);}
                    NewEntityDestroyedFixture();
                }else if(Mode=="depth"){
                    DestroyDepth=DestroyFrameLimit;EntityDestroyedFixtureKill(Target);
                }else if(Mode=="offthread"){
                    var Thread=new System.Threading.Thread(()=>DestroyNativeCancelled(Watch)){IsBackground=true};
                    Thread.Start();EntityDestroyedFixtureCheck(Thread.Join(5000),"bounded injected callback worker join");
                    EntityDestroyedFixtureCheck(DestroyOffThread,"off-thread callback did not fail closed");
                }else{
                    StopEntityDestroyedSource();
                    EntityDestroyedFixtureCheck(DestroyHeldWatches.Count==0&&DestroySnapshotBytes==0,"full source teardown retained registrations");
                }
            }finally{
                foreach(int Key in Injected)DestroyHeldWatches.Remove(Key);
                if(Mode=="watch-bytes")DestroySnapshotBytes=SavedBytes;
                DestroyDepth=SavedDepth;
                // Restore injected accounting only. Source continuity is never
                // reset or requalified after a sticky failure.
            }
            EntityDestroyedFixtureCheck(!EntityDestroyedAvailable()&&!DestroySourceQualified&&VerifyAllEntityPatches()&&!EntityObserverBroken,
                "fault did not isolate B3 source from qualified D20");
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","source fault admitted queued terminal snapshot");
            EntityDestroyedFixtureExecute("local W=game:GetService('Workspace'); assert(not pcall(function() W.EntityDestroyed:Connect(function() end) end)); "+
                "W.EntitySpawned:Connect(function(E) assert(E.Id~=nil and E.Position~=nil) end)");
            var Live=NewEntityDestroyedFixture();EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==1,"B3 fault broke B1 completion");
            EntityDestroyedFixtureDrain();EntityDestroyedFixtureKill(Live);EntityDestroyedFixtureDrain();
            Puts(EntityDestroyedFixturePrefix+"FAULT_PASS mode="+Mode+" B3 sticky fail-closed,queued cancellation,new registration rejection,B1/D20 intact; injected="+
                (Mode!="teardown")+" held_watches="+DestroyHeldWatches.Count+" snapshot_bytes="+DestroySnapshotBytes);
        }
        private Poolable EntityDestroyedFixtureConfigurePool(BaseEntity Actor)
        {
            var Pool=Actor.gameObject.GetComponent<Poolable>();
            bool Natural=Pool!=null;
            if(Pool==null)Pool=Actor.gameObject.AddComponent<Poolable>();
            Pool.Initialize(StringPool.Get(Actor.PrefabName));
            EntityDestroyedFixtureCheck(Actor.gameObject.SupportsPooling(),"real host Poolable public initialization failed");
            Puts(EntityDestroyedFixturePrefix+"CONTROLLED_POOL_CONFIG natural_component="+Natural+" real_host_type="+Pool.GetType().FullName+
                " public_Initialize=true mode="+ConVar.Pool.mode+"; plugin-installed genuine host component,no forged pool fields/success");
            return Pool;
        }
        private void EntityDestroyedFixturePreparePoolRetire(BaseEntity Actor)
        {
            EntityDestroyedFixtureInvoke(Actor,"DoEntityDestroy");
            EntityDestroyedFixtureRemoveNetwork(Actor);
            var Reset=Actor.GetType().GetMethod("ResetState",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
            Reset.Invoke(Actor,null);
            Actor.gameObject.SetActive(false);
            EntityDestroyedFixtureCheck(Actor!=null&&Actor.IsDestroyed&&Actor.net==null&&
                ReferenceEquals(BaseNetworkable.serverEntities.Find(EntityDestroyedFixtureIds[Actor]),null),"controlled no-Term world authority cleanup");
        }
        private static void EntityDestroyedFixtureFailPoolTail(Poolable __instance)
        {if(ReferenceEquals(__instance,EntityDestroyedFixturePoolTarget))throw new InvalidOperationException("B3 injected actual EnterPool tail failure");}
        private void EntityDestroyedFixtureRealPool()
        {
            EntityDestroyedFixtureCheck(EntityStartupQualified&&!EntityObserverBroken&&EntityDestroyedAvailable()&&Host.Ready&&
                BasePlayer.activePlayerList.Count==0&&BaseNetworkable.serverEntities.Count<=4096&&ConVar.Pool.enabled&&
                LevelManager.isLoaded&&!Rust.Application.isUnloadingWorld,"fresh qualified host with actual pooling enabled required");
            EntityDestroyedFixtureInitial=new HashSet<BaseNetworkable>();
            foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)EntityDestroyedFixtureInitial.Add(Entity);
            EntityDestroyedFixtureBaseline=DestroyHeldWatches.Count;
            var Stamps=new Dictionary<MethodBase,byte[]>(EntityReadPatchStamps);
            var Push=typeof(PrefabPool).GetMethod("Push",BindingFlags.Instance|BindingFlags.Public,null,new[]{typeof(Poolable)},null);
            var Patches=Harmony.GetPatchInfo(Push);bool Installed=false;
            if(Patches!=null)foreach(var Patch in Patches.Transpilers)
                if(Patch.PatchMethod.DeclaringType!=null&&Patch.PatchMethod.DeclaringType.DeclaringType==typeof(CarbonLuau))Installed=true;
            EntityDestroyedFixtureCheck(Installed,"pinned actual Stack.Push completion observer not installed");
            EntityDestroyedFixtureSubscribe();var Actor=NewEntityDestroyedFixture();
            EntityDestroyedFixtureConfigurePool(Actor);DestroyWatch OldWatch;
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(Actor,out OldWatch),"original controlled pool watch");
            ulong Epoch=OldWatch.EpochIdentity;var OldId=EntityDestroyedFixtureIds[Actor];int Instance=Actor.GetInstanceID();
            EntityDestroyedFixturePreparePoolRetire(Actor);
            ulong Before=Host.Attempted;GameManager.server.Retire(Actor.gameObject);
            EntityDestroyedFixtureCheck(Host.Attempted==Before&&Actor!=null&&Actor.net==null&&OldWatch.Closed&&OldWatch.Certified&&
                Gameplay.GameplayEvents.PendingCount==1,"actual successful pool admission not certified without Term/native destruction");
            bool ActiveAfterPool=Actor.gameObject.activeInHierarchy;
            var Reused=NewEntityDestroyedFixture(false);
            EntityDestroyedFixtureCheck(ReferenceEquals(Actor,Reused)&&Reused.GetInstanceID()==Instance,"actual host Pool.Pop did not return exact original object");
            Reused.Spawn();EntityDestroyedFixtureIds[Reused]=Reused.net.ID;DestroyWatch Fresh;
            EntityDestroyedFixtureCheck(DestroyWatches.TryGetValue(Reused,out Fresh)&&Fresh.EpochIdentity!=Epoch&&!Fresh.Closed,
                "actual same-object pool reuse lacks new completed epoch/watch");
            string Receipt=EntityDestroyedFixtureDrain();
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(Receipt)==1&&Receipt.StartsWith("B3LIVE|"+OldId.Value.ToString(CultureInfo.InvariantCulture)+"|",StringComparison.Ordinal),
                "old pool snapshot was lost/duplicated/retargeted to reused incarnation");
            EntityDestroyedFixtureKill(Reused);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(EntityDestroyedFixtureDrain())==1,"Term plus pool observer double-published current epoch");
            var Cleanup=NewEntityDestroyedFixture(false);
            EntityDestroyedFixtureCheck(ReferenceEquals(Cleanup,Actor),"exact owned pooled cleanup Pop mismatch");
            UnityEngine.Object.DestroyImmediate(Cleanup.GetComponent<Poolable>());UnityEngine.Object.DestroyImmediate(Cleanup.gameObject);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","physical disposal replayed certified pool epoch");
            var Failure=NewEntityDestroyedFixture();var FailurePool=EntityDestroyedFixtureConfigurePool(Failure);
            var FailureId=EntityDestroyedFixtureIds[Failure];EntityDestroyedFixturePreparePoolRetire(Failure);
            var Tail=new Harmony("CarbonLuau.Test.EntityDestroyed.PoolTail");bool Threw=false;
            EntityDestroyedFixturePoolTarget=FailurePool;
            try{
                Tail.Patch(typeof(Poolable).GetMethod("EnterPool",BindingFlags.Instance|BindingFlags.Public,null,new[]{typeof(GameObject)},null),
                    prefix:new HarmonyMethod(typeof(CarbonLuau).GetMethod("EntityDestroyedFixtureFailPoolTail",BindingFlags.Static|BindingFlags.NonPublic)));
                try{GameManager.server.Retire(Failure.gameObject);}catch(InvalidOperationException Error){Threw=Error.Message=="B3 injected actual EnterPool tail failure";}
                EntityDestroyedFixtureCheck(Threw&&Failure!=null&&Failure.net==null&&Gameplay.GameplayEvents.PendingCount==1,
                    "late EnterPool failure lost actual Stack.Push completion");
            }finally{EntityDestroyedFixturePoolTarget=null;Tail.UnpatchAll(Tail.Id);}
            Receipt=EntityDestroyedFixtureDrain();
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureLines(Receipt)==1&&Receipt.StartsWith("B3LIVE|"+FailureId.Value.ToString(CultureInfo.InvariantCulture)+"|",StringComparison.Ordinal),
                "actual pool commit before failed tail lacked exact snapshot");
            var FailureCleanup=NewEntityDestroyedFixture(false);
            EntityDestroyedFixtureCheck(ReferenceEquals(FailureCleanup,Failure),"failed-tail committed Stack did not return original on actual Pop");
            UnityEngine.Object.DestroyImmediate(FailureCleanup.GetComponent<Poolable>());UnityEngine.Object.DestroyImmediate(FailureCleanup.gameObject);
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()==""&&VerifyAllEntityPatches()&&!EntityObserverBroken&&EntityDestroyedAvailable(),
                "pool cleanup replay/observer continuity failure");
            foreach(var Pair in Stamps)EntityDestroyedFixtureCheck(ReferenceEquals(EntityReadPatchStamps[Pair.Key],Pair.Value),"pool observer changed D20 patch stamp");
            Puts(EntityDestroyedFixturePrefix+"REAL_POOL_PASS actual public host Poolable.Initialize,GameManager.Retire,Stack.Push+Pop,same managed object/UnityID,new D20epoch,old queued snapshot,no retarget/dedup; active_after_pool="+
                ActiveAfterPool+"; actual pool commit survives injected EnterPool failure; scoped LIFO cleanup,D20 24-target stamps unchanged; controlled plugin-driven,not natural vanilla");
        }
        private void EntityDestroyedFixtureStress()
        {
            ulong Accepted=Gameplay.GameplayEvents.Accepted,Positions=Gameplay.GameplayEvents.DestroyPositionAccepted;long Certified=DestroyCertified;
            for(int Index=0;Index<512;Index++){var Entity=NewEntityDestroyedFixture();EntityDestroyedFixtureKill(Entity);}
            EntityDestroyedFixtureCheck(DestroyCertified-Certified==512&&Gameplay.GameplayEvents.PendingCount<=128&&
                Gameplay.GameplayEvents.RetainedBytes<=2097152&&Gameplay.GameplayEvents.ProducerRejected>0&&
                DestroyHeldWatches.Count<=EntityDestroyedFixtureBaseline,"512-destruction resource/producer/watch convergence");
            EntityDestroyedFixtureDrain();
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.Accepted-Accepted<=128&&VerifyAllEntityPatches()&&
                EntityReadPatchStamps.Count==EntityDestroyedFixtureStamps.Count,"global delivery/D20 topology");
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.DestroyPositionAccepted-Positions<=128,"512 successful Kills exceeded optional TRS frame bound");
            foreach(var Pair in EntityDestroyedFixtureStamps)
                EntityDestroyedFixtureCheck(ReferenceEquals(EntityReadPatchStamps[Pair.Key],Pair.Value),"D20 patch stamp changed");
            Puts(EntityDestroyedFixturePrefix+"PASS public destruction snapshots,512-destruction burst,bounded shared128/32/512/2MiB transport,watch release,D20 24-target topology unchanged");
        }
        private static void EntityDestroyedFixtureForeignPrefix(){}
        private void EntityDestroyedFixtureDrift()
        {
            var Queued=NewEntityDestroyedFixture();EntityDestroyedFixtureKill(Queued);
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==1,"drift negative must hold actual queued snapshot");
            EntityDestroyedFixtureHarmony=new Harmony("CarbonLuau.Test.EntityDestroyed.ForeignTerm");
            EntityDestroyedFixtureHarmony.Patch(DestroyTermMethod,prefix:new HarmonyMethod(typeof(CarbonLuau).GetMethod(
                "EntityDestroyedFixtureForeignPrefix",BindingFlags.Static|BindingFlags.NonPublic)));
            EntityDestroyedFixtureCheck(!EntityDestroyedAvailable()&&!DestroySourceQualified&&VerifyAllEntityPatches()&&!EntityObserverBroken,
                "removal patch drift did not isolate/fail closed B3");
            EntityDestroyedFixtureCheck(EntityDestroyedFixtureDrain()=="","unsupported removal topology delivered queued terminal event");
            EntityDestroyedFixtureExecute("local W=game:GetService('Workspace'); assert(not pcall(function() W.EntityDestroyed:Connect(function() end) end)); "+
                "W.EntitySpawned:Connect(function(E) assert(E.Prefab=='"+EntityDestroyedFixturePrefab+"'); print('B1UNCHANGED|'..E.Id) end)");
            var Live=NewEntityDestroyedFixture();
            EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==1,"B3 drift suppressed independent B1 completion");
            EntityDestroyedFixtureExecute("local E=game:GetService('Workspace'):GetEntityById('"+Live.net.ID.Value.ToString(CultureInfo.InvariantCulture)+"'); assert(E~=nil and E.Position~=nil)");
            EntityDestroyedFixtureDrain();EntityDestroyedFixtureKill(Live);EntityDestroyedFixtureDrain();
            EntityDestroyedFixtureHarmony.UnpatchAll(EntityDestroyedFixtureHarmony.Id);EntityDestroyedFixtureHarmony=null;
            Puts(EntityDestroyedFixturePrefix+"DRIFT_PASS B3 rejects queued/new work; B1/keyed D20 remain qualified; task-owned foreign prefix removed");
        }
        private void EntityDestroyedFixtureFinish(Exception Failure)
        {
            if(Failure!=null)PrintError(EntityDestroyedFixturePrefix+"FAIL "+Failure);
            EntityDestroyedFixtureVeto=EntityDestroyedFixtureNested=false;EntityDestroyedFixtureWatched=null;
            try{
                if(EntityDestroyedFixtureHarmony!=null){EntityDestroyedFixtureHarmony.UnpatchAll(EntityDestroyedFixtureHarmony.Id);EntityDestroyedFixtureHarmony=null;}
                foreach(var Entity in EntityDestroyedFixtureOwned){
                    if(Entity!=null&&!Entity.IsDestroyed){Entity.EnableSaving(false);Entity.Kill();}
                    EntityDestroyedFixtureRemoveNetwork(Entity);
                }
                foreach(var Object in EntityDestroyedFixtureObjects.Values)if(Object!=null)UnityEngine.Object.DestroyImmediate(Object);
                EntityDestroyedFixtureDrain();
                foreach(var Id in EntityDestroyedFixtureIds.Values)
                    EntityDestroyedFixtureCheck(ReferenceEquals(BaseNetworkable.serverEntities.Find(Id),null),"owned raw registry orphan");
                EntityDestroyedFixtureCheck(BaseNetworkable.serverEntities.Count<=4096,"cleanup realm cap");
                if(EntityDestroyedFixtureInitial!=null)foreach(BaseNetworkable Entity in BaseNetworkable.serverEntities)
                    EntityDestroyedFixtureCheck(EntityDestroyedFixtureInitial.Contains(Entity),"new live actor survived cleanup");
                EntityDestroyedFixtureOwned.Clear();EntityDestroyedFixtureIds.Clear();EntityDestroyedFixtureObjects.Clear();
                EntityDestroyedFixtureCheck(Gameplay.GameplayEvents.PendingCount==0&&Gameplay.GameplayEvents.RetainedBytes==0,"cleanup transport convergence");
                StopEntityDestroyedSource();
                EntityDestroyedFixtureCheck(DestroyHeldWatches.Count==0&&DestroySnapshotBytes==0&&DestroyPollCount==0,
                    "full observer teardown retained registrations/snapshot slots");
                for(int Slot=0;Slot<DestroyNextSlot;Slot++)EntityDestroyedFixtureCheck(DestroyPollSlots[Slot]==null,"teardown retained poll slot");
                long Visits=DestroyPollVisits,Turns=DestroyPollTurns,Began=System.Diagnostics.Stopwatch.GetTimestamp();
                for(int Repeat=0;Repeat<1000;Repeat++)RunEntityDestroyedFrame();
                EntityDestroyedFixtureCheck(DestroyPollVisits==Visits&&DestroyPollTurns==Turns,"zero-held source performed persistent sweep work");
                Puts(EntityDestroyedFixturePrefix+"ZERO_HELD_PASS full source stop,count/slots/bytes zero,1000 idle calls zero visits/turns; total_ms="+
                    (1000.0*(System.Diagnostics.Stopwatch.GetTimestamp()-Began)/System.Diagnostics.Stopwatch.Frequency).ToString("R",CultureInfo.InvariantCulture));
                Puts(EntityDestroyedFixturePrefix+"NO_ORPHANS_PASS exact owned network/registry/GameObject cleanup");
                Puts(EntityDestroyedFixturePrefix+"CLEANUP_PASS owned actors retired,watch count converged,reservations/bytes zero");
            }catch(Exception Error){PrintError(EntityDestroyedFixturePrefix+"CLEANUP_FAIL "+Error);}
            ConsoleSystem.Run(ConsoleSystem.Option.Server,"quit");
        }
    }
}
