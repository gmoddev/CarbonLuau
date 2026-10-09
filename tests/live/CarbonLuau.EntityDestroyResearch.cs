// Research-only package on the existing disposable host. No public B3 API.
// Uses the already-declared deferred fixture seam, never ships in production.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string DestroyResearchPrefix="[CarbonLuau:EntityDestroyResearch] ";
        private const string DestroyResearchPrefab="assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private sealed class DestroyResearchCase
        {
            internal string Label,Id,Prefab;
            internal ulong Token,Epoch;
            internal WeakReference Actor;
            internal BaseEntity CleanupActor;
            internal GameObject CleanupObject;
            internal CancellationTokenRegistration Registration;
            internal int Cancellations,OffThread;
            internal string CallbackFailure;
        }
        private readonly List<DestroyResearchCase> DestroyResearchCases=new List<DestroyResearchCase>(16);
        private BaseNetworkable DestroyResearchWatched;
        private bool DestroyResearchVeto;
        private int DestroyResearchKillHooks,DestroyResearchOwner;
        // Existing 1/1 Carbon Spawn hook demand preserves D20's qualified tuple.
        private void OnEntitySpawn(BaseNetworkable Entity){}
        private void OnEntitySpawned(BaseNetworkable Entity){}
        private object OnEntityKill(BaseNetworkable Entity)
        {
            if(!ReferenceEquals(Entity,DestroyResearchWatched))return null;
            DestroyResearchKillHooks++;
            Puts(DestroyResearchPrefix+"KILL_HOOK veto="+DestroyResearchVeto+" destroyed="+Entity.IsDestroyed+
                " occupied="+(Entity.net!=null&&ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID),Entity)));
            return DestroyResearchVeto?(object)true:null;
        }
        partial void RunEntitySpawnFixtures()
        {
            NextTick(()=>{
                try{DestroyResearchStart();}
                catch(Exception Error){DestroyResearchFinish(Error);}
            });
        }
        private static void DestroyResearchCheck(bool Condition,string Message)
        {if(!Condition)throw new InvalidOperationException(Message);}
        private DestroyResearchCase DestroyResearchCreate(string Label)
        {
            DestroyResearchCheck(DestroyResearchCases.Count<16,"actor cap");
            var Entity=GameManager.server.CreateEntity(DestroyResearchPrefab,new Vector3(0,100,0),Quaternion.identity,true);
            DestroyResearchCheck(Entity!=null,"prefab missing"); Entity.enableSaving=false;
            var Case=new DestroyResearchCase{Label=Label,Actor=new WeakReference(Entity),CleanupActor=Entity,CleanupObject=Entity.gameObject};
            DestroyResearchCases.Add(Case); Entity.Spawn();
            var Session=Gameplay.Active; var Witness=Session.CaptureCommittedEntityWitness();
            EntityLifetimeModel.Binding Binding;
            var Authority=new EntityLifetimeModel.Authority((ulong)Session.VmGenerationId,(ulong)Session.DomainLifetimeId,Witness.Token);
            DestroyResearchCheck(TryAdmitEntity(Entity,Authority,Value=>EntityFacadeCurrent(Value,Session,Witness),ReadEntityEvidence,false,out Binding),"qualified original epoch");
            Case.Id=Binding.Record.NetworkId.ToString(CultureInfo.InvariantCulture); Case.Prefab=Binding.Record.Prefab;
            Case.Token=Binding.Record.Token; Case.Epoch=Binding.Record.Epoch;
            Case.Registration=Entity.destroyCancellationToken.Register(()=>DestroyResearchCancelled(Case));
            Puts(DestroyResearchPrefix+"ARM label="+Label+" id="+Case.Id+" token="+Case.Token+" epoch="+Case.Epoch+
                " active="+Entity.gameObject.activeInHierarchy+" poolable="+(Entity.GetComponent<Poolable>()!=null));
            return Case;
        }
        private void DestroyResearchCancelled(DestroyResearchCase Case)
        {
            Interlocked.Increment(ref Case.Cancellations);
            bool Owner=Thread.CurrentThread.ManagedThreadId==DestroyResearchOwner;
            if(!Owner){Interlocked.Increment(ref Case.OffThread);return;} // No host/VM/logging access off thread.
            try{
            var Entity=Case.Actor.Target as BaseEntity;
            bool Clr=ReferenceEquals(Entity,null),Unity=Entity==null;
            string Detail="";
            if(!Clr)Detail=" destroyed="+Entity.IsDestroyed+" net="+(Entity.net!=null);
            if(!Unity)Detail+=" active="+Entity.gameObject.activeInHierarchy;
            Puts(DestroyResearchPrefix+"CANCEL label="+Case.Label+" owner="+Owner+" clr_null="+Clr+" unity_null="+Unity+
                " id="+Case.Id+" token="+Case.Token+Detail);
            }catch(Exception Error){Case.CallbackFailure=Error.GetType().Name;}
        }
        private static void DestroyResearchInvoke(BaseEntity Entity,string Method)
        {
            typeof(BaseNetworkable).GetMethod(Method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
                null,Type.EmptyTypes,null).Invoke(Entity,null);
        }
        private void DestroyResearchTrace(DestroyResearchCase Case,string Stage)
        {
            var Entity=Case.CleanupActor;
            bool Clr=ReferenceEquals(Entity,null),Unity=Entity==null;
            bool Occupied=ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(UInt64.Parse(Case.Id))),Entity);
            string Detail=!Clr?" destroyed="+Entity.IsDestroyed+" net="+(Entity.net!=null):"";
            if(!Unity)Detail+=" active="+Entity.gameObject.activeInHierarchy;
            Puts(DestroyResearchPrefix+"STATE label="+Case.Label+" stage="+Stage+" clr_null="+Clr+" unity_null="+Unity+
                " occupied="+Occupied+" cancelled="+Case.Cancellations+Detail);
        }
        private void DestroyResearchStart()
        {
            DestroyResearchOwner=Thread.CurrentThread.ManagedThreadId;
            foreach(string Name in new[]{"Kill","TerminateOnServer"}){
                var Method=typeof(BaseNetworkable).GetMethod(Name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                byte[] Bytes=Method.GetMethodBody().GetILAsByteArray();
                using(var Hash=System.Security.Cryptography.SHA256.Create())
                    Puts(DestroyResearchPrefix+"LIVE_BODY name="+Name+" size="+Bytes.Length+" sha256="+
                        BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant());
            }
            DestroyResearchCheck(EntityStartupQualified&&!EntityObserverBroken&&VerifyAllEntityPatches()&&Host.Ready&&BasePlayer.activePlayerList.Count==0,"isolated qualified host");
            var Veto=DestroyResearchCreate("veto"); DestroyResearchWatched=Veto.CleanupActor; DestroyResearchVeto=true;
            Veto.CleanupActor.Kill(); DestroyResearchTrace(Veto,"veto-return");
            DestroyResearchCheck(Veto.Cancellations==0&&!Veto.CleanupActor.IsDestroyed&&DestroyResearchKillHooks==1,"veto is not destruction");
            DestroyResearchVeto=false; Veto.CleanupActor.Kill(); DestroyResearchTrace(Veto,"kill-return");
            var Term=DestroyResearchCreate("terminate"); DestroyResearchInvoke(Term.CleanupActor,"TerminateOnServer");
            DestroyResearchTrace(Term,"terminate-return");
            DestroyResearchCheck(Term.CleanupActor.net==null&&!Term.CleanupActor.gameObject.activeInHierarchy,"termination world removal");
            DestroyResearchInvoke(Term.CleanupActor,"EntityDestroy"); DestroyResearchTrace(Term,"entitydestroy-return");
            var Churn=DestroyResearchCreate("churn"); BaseNetworkable.serverEntities.UnregisterID(Churn.CleanupActor);
            DestroyResearchTrace(Churn,"unregistered"); BaseNetworkable.serverEntities.RegisterID(Churn.CleanupActor);
            DestroyResearchCheck(Churn.Cancellations==0,"registry churn not native destruction");
            DestroyResearchTrace(Churn,"reinserted");
            var Immediate=DestroyResearchCreate("immediate"); UnityEngine.Object.DestroyImmediate(Immediate.CleanupObject);
            DestroyResearchTrace(Immediate,"destroyimmediate-return");
            DestroyResearchCheck(Immediate.Cancellations==1&&Immediate.CleanupObject==null,"immediate native cancellation");
            var Deferred=DestroyResearchCreate("deferred"); UnityEngine.Object.Destroy(Deferred.CleanupObject);
            DestroyResearchTrace(Deferred,"destroy-requested");
            var Inactive=DestroyResearchCreate("inactive"); Inactive.CleanupObject.SetActive(false);
            DestroyResearchCheck(Inactive.Cancellations==0,"deactivation not destruction");
            UnityEngine.Object.Destroy(Inactive.CleanupObject); DestroyResearchTrace(Inactive,"inactive-destroy-requested");
            var Component=DestroyResearchCreate("component"); UnityEngine.Object.Destroy(Component.CleanupActor);
            DestroyResearchTrace(Component,"component-destroy-requested");
            var Delayed=DestroyResearchCreate("delayed"); UnityEngine.Object.Destroy(Delayed.CleanupObject,0.2f);
            DestroyResearchTrace(Delayed,"delayed-requested");
            timer.Once(0.5f,()=>{
                try{
                    foreach(var Case in DestroyResearchCases){DestroyResearchTrace(Case,"later");
                        DestroyResearchCheck(Case.OffThread==0&&Case.CallbackFailure==null,"owner-thread callback safety: "+Case.Label);
                        if(Case.Label!="churn")DestroyResearchCheck(Case.Cancellations==1,"single native completion: "+Case.Label);}
                    DestroyResearchCheck(VerifyAllEntityPatches()&&!EntityObserverBroken,"D20 topology/continuity preserved");
                    Puts(DestroyResearchPrefix+"PASS actual Kill/veto, termination, registry churn, immediate/deferred/delayed/inactive/component native cancellation; research only");
                    DestroyResearchFinish(null);
                }catch(Exception Error){DestroyResearchFinish(Error);}
            });
        }
        private void DestroyResearchFinish(Exception Failure)
        {
            if(Failure!=null)PrintError(DestroyResearchPrefix+"FAIL "+Failure);
            DestroyResearchVeto=false; DestroyResearchWatched=null;
            try{
                foreach(var Case in DestroyResearchCases){
                    Case.Registration.Dispose(); var Entity=Case.CleanupActor;
                    if(Entity!=null&&!Entity.IsDestroyed){Entity.EnableSaving(false);Entity.Kill();}
                    if(!ReferenceEquals(Entity,null)&&Entity.net!=null){
                        BaseNetworkable.serverEntities.UnregisterID(Entity); Network.Net.sv.DestroyNetworkable(ref Entity.net);
                    }
                    if(Case.CleanupObject!=null)UnityEngine.Object.DestroyImmediate(Case.CleanupObject);
                    DestroyResearchCheck(ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(UInt64.Parse(Case.Id))),null),"owned registry cleanup");
                }
                DestroyResearchCases.Clear();
                Puts(DestroyResearchPrefix+"CLEANUP_PASS no owned registry/network/game-object leftovers");
            }catch(Exception Error){PrintError(DestroyResearchPrefix+"CLEANUP_FAIL "+Error);}
            ConsoleSystem.Run(ConsoleSystem.Option.Server,"quit");
        }
    }
}
