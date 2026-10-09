using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Additive observation only. D20 owns epochs and its 24 Spawn targets.
        private const int DestroyWatchLimit=262144,DestroySnapshotLimit=67108864,DestroyFrameLimit=16;
        private static readonly MethodInfo DestroyKillMethod=ResolveGameplayMethod(typeof(BaseNetworkable),"Kill",0x06005177,
            "BaseNetworkable+DestroyMode","System.Boolean");
        private static readonly MethodInfo DestroyTermMethod=ResolveGameplayMethod(typeof(BaseNetworkable),"TerminateOnServer",0x0600517a);
        private static readonly MethodInfo[] DestroyTargets={DestroyKillMethod,DestroyTermMethod,
            ResolveGameplayMethod(typeof(PrefabPool),"Push",0x06008344,typeof(Poolable).FullName)};
        private const string DestroyKillHook="Carbon.Hooks.Category_Entity+Entity_BaseNetworkable+Entity_BaseNetworkable_d4af8c75a9a747378cbaf881ce6499bd";
        private readonly Dictionary<MethodBase,byte[]> DestroyPatchStamps=new Dictionary<MethodBase,byte[]>();
        private ConditionalWeakTable<object,DestroyWatch> DestroyWatches=new ConditionalWeakTable<object,DestroyWatch>();
        // Cold capacity reservation avoids population-sized collection resizing
        // in an individual Spawn/removal callback. Runtime CTS/CWT costs remain
        // separately qualified host behavior, not a callback-time guarantee.
        private readonly Dictionary<int,DestroyWatch> DestroyHeldWatches=new Dictionary<int,DestroyWatch>(DestroyWatchLimit);
        private readonly Stack<int> DestroyFreeSlots=new Stack<int>(DestroyWatchLimit);
        // Native cancellation misses never-active components on this host.
        // A bounded original-wrapper complement avoids relying on activation
        // classifications; it never scans the registry or reacquires by ID.
        private readonly DestroyWatch[] DestroyPollSlots=new DestroyWatch[DestroyWatchLimit];
        private int DestroyPollCount,DestroyPollCursor;
        private long DestroyPollFrame=Int64.MinValue;
        private int DestroyNextSlot,DestroySnapshotBytes,DestroyOwnerThread,DestroyDepth;
        private volatile bool DestroySourceArmed,DestroySourceQualified,DestroySourceStopped,DestroyOffThread;
        private string DestroySourceFailure;
        private long DestroyCertified,DestroyNativePending,DestroyRejected,DestroyPollVisits,DestroyPolled,DestroyPollTurns,DestroyMaximumPollTicks;
        private int DestroyMaximumPollVisits;

        private sealed class DestroyWatch
        {
            internal readonly WeakReference Owner,Entity;
            internal readonly EntityLifetimeModel.MembershipCandidate Witness;
            internal readonly string Id,Prefab;
            internal readonly ulong EpochIdentity;
            internal readonly int Slot,Charge,Thread;
            internal CancellationTokenRegistration Registration;
            internal bool Closed,Published,Certified;
            internal bool NativeNotified;
            internal ulong LastLivePublication;
            internal PlayerPosition? KillPosition;
            internal DestroyWatch(CarbonLuau Owner,BaseEntity Entity,EntityLifetimeModel.MembershipCandidate Witness,int Slot,int Charge)
            {
                this.Owner=new WeakReference(Owner);this.Entity=new WeakReference(Entity);this.Witness=Witness;
                Id=Entity.net.ID.Value.ToString(CultureInfo.InvariantCulture);Prefab=Entity.PrefabName;
                EpochIdentity=Witness.Birth;this.Slot=Slot;this.Charge=Charge;Thread=Owner.DestroyOwnerThread;
                LastLivePublication=Owner.Gameplay==null?0:Owner.Gameplay.DestroyedListenerVersion;
            }
        }
        private sealed class DestroyFrame
        {
            internal CarbonLuau Owner;
            internal DestroyWatch Watch;
            internal BaseEntity Entity;
            internal bool Term,Postfix,Original,Exited;
        }
        private static bool DestroyTargetPinned(MethodInfo Method)
        {
            try{
                bool Linux=Environment.OSVersion.Platform==PlatformID.Unix;
                if(Method==null||Method.IsStatic||Method.IsAbstract||Method.ReturnType!=typeof(void)||
                    !HasHash(Method.Module.Assembly,Linux?LinuxRustHash:WindowsRustHash)||
                    Method.Module.ModuleVersionId.ToString("D")!=(Linux?"616082a0-36f4-4680-a1ab-efc5766796cc":"c1c11bd1-baa1-4d85-b64e-1d4a4664d62e"))return false;
                MethodBody Body=Method.GetMethodBody();
                if(Body==null||Body.ExceptionHandlingClauses.Count!=0)return false;
                byte[] Bytes=Body.GetILAsByteArray();
                bool Kill=SameEntityMethod(Method,DestroyKillMethod);
                if(Bytes.Length!=(Kill?109:61))return false;
                string Expected=Kill?(Linux?"ed1ebec87800e322c04e9810b72551bed60e6e96e9afa245e6441d0f97234e05":
                    "9153173a2977a37f995b06465a5b9a3de598a7a48e4868be0cb71f1bcfeb6558"):
                    "4450e9c486b61e2a4474b72a7201ae7dbcc91d032c2daf6ef025bf8dee7b8333";
                using(var Hash=SHA256.Create())return BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant()==Expected;
            }catch(Exception){return false;}
        }
        private void InitializeEntityDestroyedSource()
        {
            DestroyOwnerThread=Thread.CurrentThread.ManagedThreadId;
            try{
                bool Linux=Environment.OSVersion.Platform==PlatformID.Unix;
                Assembly Core=typeof(MonoBehaviour).Assembly;
                if(!EntityHostPinned||!DestroyTargetPinned(DestroyKillMethod)||!DestroyTargetPinned(DestroyTermMethod)||!PoolDestroyPinned()||
                    !HasFileHash(Path.Combine(Path.GetDirectoryName(Application.dataPath),Linux?"UnityPlayer.so":"UnityPlayer.dll"),
                        Linux?"ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1":
                        "6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a")||
                    !HasHash(LoadedEntityAssembly("Facepunch.Network"),
                        Linux?"fbeae46ee305b7079435a05d4175c50e9ca61c3c8bafbe6d09df915ea3030c25":
                        "ca0181b2458ebeaa59f4e9f9aaf09e73cdc3a808ccd26b8cfd503ab30c466fc7")||
                    !HasHash(Core,Linux?"ada97d7037c7c928d8d432f734115d82a3d805a2da628f481901da5d165795e2":
                        "93b0e7e8e1b9a82f34d09740c45be2cbd7c13a82683a192af2858831b63ce45a")||
                    Core.ManifestModule.ModuleVersionId.ToString("D")!=(Linux?"7ef4b7e3-9e5e-4212-acad-bf6d2e50d7fa":"dce824bf-6f64-481d-908e-6fe02c35a666")){
                    RejectEntityDestroyedSource("host/body/native-cancellation pin");return;
                }
                DestroySourceArmed=true;
            }catch(Exception){RejectEntityDestroyedSource("observer initialization");}
        }
        private void RejectEntityDestroyedSource(string Reason)
        {
            DestroySourceQualified=DestroySourceArmed=false;
            if(DestroySourceFailure==null)DestroySourceFailure=Reason;
            if(Thread.CurrentThread.ManagedThreadId==DestroyOwnerThread)
                try{PrintWarning("[CarbonLuau:EntityDestroyed] Source unavailable: "+DestroySourceFailure);}catch(Exception){}
        }
        private bool VerifyDestroyTopology(out bool Busy)
        {
            Busy=false;IDictionary State=GameplayPatchState;object Locker=GameplayPatchLock;
            if(State==null||Locker==null)return false;
            if(!Monitor.TryEnter(Locker,0)){Busy=true;return false;}
            try{
                if(!Monitor.TryEnter(State,0)){Busy=true;return false;}
                try{
                    if(State.Count>GameplayMaximumPatchRecords||!ReferenceEquals(GameplayPatchStateField.GetValue(null),State)||
                        !ReferenceEquals(GameplayPatchLockField.GetValue(null),Locker))return false;
                    foreach(MethodInfo Target in DestroyTargets){
                        object Record=State[Target];byte[] Stamp=Record as byte[],Expected;
                        if((Record!=null&&Stamp==null)||(Stamp!=null&&Stamp.Length>GameplayMaximumPatchBytes))return false;
                        if(DestroyPatchStamps.TryGetValue(Target,out Expected)&&ReferenceEquals(Stamp,Expected))continue;
                        bool Pool=SameEntityMethod(Target,PoolDestroyMethod);
                        if(Pool&&!PoolDestroyMarkerValid)return false;
                        if(!VerifyGameplayMethodTopology(Target,Pool?typeof(EntityDestroyPoolPatch):
                            Target==DestroyKillMethod?typeof(EntityDestroyKillPatch):typeof(EntityDestroyTermPatch),
                            Target==DestroyKillMethod?DestroyKillHook:null,Pool))return false;
                        DestroyPatchStamps[Target]=Stamp;
                    }
                    return true;
                }finally{Monitor.Exit(State);}
            }finally{Monitor.Exit(Locker);}
        }
        private bool EntityDestroyedAvailable()
        {
            if(!DestroySourceQualified||DestroySourceStopped||DestroyOffThread||!EntityStartupQualified||EntityObserverBroken)return false;
            if(Gameplay!=null&&Gameplay.DestroyedListenerVersion==UInt64.MaxValue){
                RejectEntityDestroyedSource("listener publication sequence exhausted");return false;
            }
            if(EntityDiscoveryFramePump==null||!ReferenceEquals(EntityDiscoveryFramePump.Owner,this)){
                RejectEntityDestroyedSource("bounded owner-frame observer unavailable");return false;
            }
            bool Busy;
            if(VerifyDestroyTopology(out Busy))return true;
            RejectEntityDestroyedSource(Busy?"removal topology validation busy; continuity lost":"removal patch topology drift");
            return false;
        }
        private void QualifyEntityDestroyedSource()
        {
            if(!DestroySourceArmed||!EntityStartupQualified||EntityObserverBroken)return;
            if(EntityDiscoveryFramePump==null||!ReferenceEquals(EntityDiscoveryFramePump.Owner,this)){
                RejectEntityDestroyedSource("bounded owner-frame observer installation");return;
            }
            bool Busy;
            if(!VerifyDestroyTopology(out Busy)){RejectEntityDestroyedSource("removal patch installation");return;}
            DestroySourceQualified=true;
            Puts("[CarbonLuau:EntityDestroyed] Source qualified; watches="+DestroyHeldWatches.Count+" snapshot_bytes="+DestroySnapshotBytes);
        }
        private void ArmEntityDestroyWatch(BaseEntity Entity)
        {
            if(!DestroySourceArmed||DestroySourceStopped||EntityObserverBroken||Entity==null||Thread.CurrentThread.ManagedThreadId!=DestroyOwnerThread)return;
            DestroyWatch Installing=null;
            try{
                DestroyWatch Previous;
                if(DestroyWatches.TryGetValue(Entity,out Previous))ReleaseDestroyWatch(Previous);
                var Witness=EntityLifetimes.CaptureCompletedWitness(Entity);
                if(Witness==null){
                    if(EntityLifetimes.HasCompletedObservation(Entity))RejectEntityDestroyedSource("completed epoch lacks bounded observation witness");
                    return;
                }
                if(Entity.net==null||Entity.net.ID.Value==0||Entity.IsDestroyed||!Entity.IsFullySpawned()||
                    !ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID),Entity))return;
                string Prefab=Entity.PrefabName;
                if(String.IsNullOrEmpty(Prefab)||Prefab.Length>512||Prefab.IndexOf('\0')>=0||new UTF8Encoding(false,true).GetByteCount(Prefab)>512)return;
                int Charge=2*(Prefab.Length+20);
                if(DestroyHeldWatches.Count>=DestroyWatchLimit||DestroySnapshotBytes>DestroySnapshotLimit-Charge){
                    RejectEntityDestroyedSource("watch/snapshot quota");return;
                }
                int Slot=DestroyFreeSlots.Count!=0?DestroyFreeSlots.Pop():DestroyNextSlot++;
                var Watch=new DestroyWatch(this,Entity,Witness,Slot,Charge);Installing=Watch;
                DestroyHeldWatches.Add(Slot,Watch);DestroySnapshotBytes+=Charge;DestroyWatches.Add(Entity,Watch);
                DestroyPollSlots[Slot]=Watch;DestroyPollCount++;
                ArmEntityPoolWatch(Watch,Entity.gameObject);
                if(!DestroySourceArmed){ReleaseDestroyWatch(Watch);return;}
                if(ExecutionContext.IsFlowSuppressed())Watch.Registration=Entity.destroyCancellationToken.Register(DestroyNativeCancelled,Watch,false);
                else using(ExecutionContext.SuppressFlow())Watch.Registration=Entity.destroyCancellationToken.Register(DestroyNativeCancelled,Watch,false);
                if(Watch.Closed||Watch.NativeNotified){Watch.Registration.Dispose();Watch.Registration=default(CancellationTokenRegistration);}
            }catch(Exception){
                if(Installing!=null)ReleaseDestroyWatch(Installing);
                RejectEntityDestroyedSource("native watch registration");
            }
        }
        private void ReleaseDestroyWatch(DestroyWatch Watch)
        {
            if(Watch==null||Watch.Closed)return;
            Watch.Closed=true;
            try{Watch.Registration.Dispose();}
            finally{
                Watch.Registration=default(CancellationTokenRegistration);
                try{ReleaseEntityPoolWatch(Watch);}
                finally{
                    if(ReferenceEquals(DestroyPollSlots[Watch.Slot],Watch)){DestroyPollSlots[Watch.Slot]=null;DestroyPollCount--;}
                    if(DestroyHeldWatches.Remove(Watch.Slot)){DestroySnapshotBytes-=Watch.Charge;DestroyFreeSlots.Push(Watch.Slot);}
                    object Entity=Watch.Entity.Target;
                    if(Entity!=null){DestroyWatch Current;if(DestroyWatches.TryGetValue(Entity,out Current)&&ReferenceEquals(Current,Watch))DestroyWatches.Remove(Entity);}
                }
            }
        }
        private void RetireEntityDestroyWatchAtSpawn(object Entity)
        {
            DestroyWatch Watch;
            if(Entity!=null&&DestroyWatches.TryGetValue(Entity,out Watch))ReleaseDestroyWatch(Watch);
        }
        private static void DestroyNativeCancelled(object State)
        {
            var Watch=State as DestroyWatch;if(Watch==null)return;
            if(Thread.CurrentThread.ManagedThreadId!=Watch.Thread){
                var OffOwner=Watch.Owner.Target as CarbonLuau;
                if(OffOwner!=null){OffOwner.DestroyOffThread=true;OffOwner.DestroySourceQualified=false;}return;
            }
            var Owner=Watch.Owner.Target as CarbonLuau;
            if(Owner==null)return;
            try{Owner.NativeEntityDestroy(Watch);}catch(Exception){Owner.DestroyRejected++;}
        }
        private void NativeEntityDestroy(DestroyWatch Watch)
        {
            if(Watch.Closed||Watch.NativeNotified)return;
            // Cancellation is only a prompt. Keep both Term and fixed-slot
            // coverage until an original invalid wrapper is actually observed.
            Watch.NativeNotified=true;DestroyNativePending++;
            Watch.Registration.Dispose();Watch.Registration=default(CancellationTokenRegistration);
        }
        private void CompleteNativeEntityDestroy(DestroyWatch Watch)
        {
            if(Watch.Closed||!Watch.Certified)return;
            try{
                if(EntityDestroyedAvailable()&&EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness)&&
                    Gameplay!=null&&Host!=null&&Host.Ready&&Gameplay.GameplayEvents.Capture("entitydestroyed")){
                    Watch.Published=true;
                    Gameplay.EntityDestroyed(new GameplayEntityDestroyedObservation(Watch.Id,Watch.Prefab,Watch.EpochIdentity,null,
                        ()=>ValidateNativeDestroy(Watch),Watch.LastLivePublication));
                    RequestDrain();
                }
            }finally{ReleaseDestroyWatch(Watch);}
        }
        private bool ValidateNativeDestroy(DestroyWatch Watch)
        {
            if(Thread.CurrentThread.ManagedThreadId!=DestroyOwnerThread||!EntityDestroyedAvailable())return false;
            if(Watch.Certified)return true;
            if(!EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness))return false;
            var Entity=Watch.Entity.Target as BaseEntity;
            // Weak collection is not destruction proof. A native cancellation
            // does not certify its still-live wrapper; validate original only.
            if(ReferenceEquals(Entity,null)||Entity!=null)return false;
            return true;
        }
        private void RunEntityDestroyedFrame()
        {
            if(Stopping||DestroyPollCount==0||DestroySourceStopped||
                (Host!=null&&Host.Busy))return;
            if(Thread.CurrentThread.ManagedThreadId!=DestroyOwnerThread){DestroyOffThread=true;DestroySourceQualified=false;return;}
            long Frame=UnityEngine.Time.frameCount;
            if(DestroyPollFrame==Frame)return;
            DestroyPollFrame=Frame;
            long Began=System.Diagnostics.Stopwatch.GetTimestamp();int Inspected=0;
            try{
                bool Current=DestroySourceQualified&&EntityDestroyedAvailable();
                int End=DestroyNextSlot;
                for(int Visits=0;Visits<GameplayEventPolicy.CapturesPerFrame&&DestroyPollCount!=0&&End!=0;Visits++){
                    if(DestroyPollCursor>=End)DestroyPollCursor=0;
                    var Watch=DestroyPollSlots[DestroyPollCursor++];DestroyPollVisits++;Inspected++;
                    if(Watch==null||Watch.Closed)continue;
                    // Sticky source loss admits no new observations. Reuse the
                    // bounded owner-frame maintenance path to retire watches,
                    // rather than retain dead registrations until plugin unload.
                    if(!Current){ReleaseDestroyWatch(Watch);continue;}
                    var Entity=Watch.Entity.Target as BaseEntity;
                    if(ReferenceEquals(Entity,null)||!EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness)){
                        DestroyRejected++;ReleaseDestroyWatch(Watch);continue;
                    }
                    if(Entity!=null){Watch.LastLivePublication=Gameplay==null?0:Gameplay.DestroyedListenerVersion;continue;}
                    // Original native invalidity is completion, not registry
                    // retirement or a guessed identity. Normal event budgets,
                    // deferred delivery and fresh proof validation still apply.
                    Watch.Certified=true;DestroyPolled++;CompleteNativeEntityDestroy(Watch);
                }
            }catch(Exception){RejectEntityDestroyedSource("bounded native-completion intake");}
            finally{
                DestroyPollTurns++;DestroyMaximumPollVisits=Math.Max(DestroyMaximumPollVisits,Inspected);
                DestroyMaximumPollTicks=Math.Max(DestroyMaximumPollTicks,System.Diagnostics.Stopwatch.GetTimestamp()-Began);
            }
        }
        private string EntityDestroyedStatus {get{return "[CarbonLuau:EntityDestroyed] qualified="+DestroySourceQualified+
            "; watches="+DestroyHeldWatches.Count+"; snapshot_text="+DestroySnapshotBytes+"; certified="+DestroyCertified+
            "; native_pending="+DestroyNativePending+"; native_polled="+DestroyPolled+"; proof_rejected="+DestroyRejected+
            "; poll_turns="+DestroyPollTurns+"; poll_visits="+DestroyPollVisits+"; max_visits="+DestroyMaximumPollVisits+
            "; max_poll_ticks="+DestroyMaximumPollTicks+"; source_failure="+(DestroySourceFailure??"none");}}
        private DestroyFrame EnterEntityDestroy(BaseNetworkable Networkable,bool Term)
        {
            if(Thread.CurrentThread.ManagedThreadId!=DestroyOwnerThread){DestroyOffThread=true;DestroySourceQualified=false;return null;}
            if(!EntityDestroyedAvailable()||Networkable==null)return null;
            if(DestroyDepth>=DestroyFrameLimit){DestroyRejected++;RejectEntityDestroyedSource("nested removal frame quota; continuity lost");return null;}
            BaseEntity Entity=Networkable as BaseEntity;DestroyWatch Watch;
            if(Entity==null||!DestroyWatches.TryGetValue(Entity,out Watch)||Watch.Closed||Watch.Certified||
                !EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness)||Entity.net==null||
                Entity.net.ID.Value.ToString(CultureInfo.InvariantCulture)!=Watch.Id||Entity.PrefabName!=Watch.Prefab||
                !ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID),Entity))return null;
            if(!Term||!Entity.IsDestroyed){
                Watch.KillPosition=null;
                EntityPositionComposition.Position Position;
                if(Gameplay!=null&&Host!=null&&Host.Ready&&Gameplay.GameplayEvents.DestroyPositionSnapshot()&&
                    EntityPositionReader!=null&&EntityPositionReader.Available&&EntityDiscoveryPatchCurrent(Entity)&&
                    EntityPositionReader.TryObserve(Entity,out Position)&&EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness))
                    Watch.KillPosition=new PlayerPosition(Position.X,Position.Y,Position.Z);
                else Watch.KillPosition=null;
            }
            var Frame=new DestroyFrame{Owner=this,Watch=Watch,Entity=Entity,Term=Term};
            DestroyDepth++;
            return Frame;
        }
        private void ExitEntityDestroy(DestroyFrame Frame,Exception Error)
        {
            if(Frame==null||Frame.Exited)return;Frame.Exited=true;
            try{
                var Watch=Frame.Watch;
                if(!Frame.Term||Error!=null||!Frame.Postfix||!Frame.Original||Watch.Closed||Watch.Certified||
                    !EntityDestroyedAvailable()||!EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness))return;
                var Entity=Frame.Entity;
                if(Entity==null||Entity.net!=null||Entity.gameObject.activeInHierarchy||
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(UInt64.Parse(Watch.Id,CultureInfo.InvariantCulture))),Entity))return;
                Watch.Certified=true;DestroyCertified++;
                if(Gameplay!=null&&Host!=null&&Host.Ready&&Gameplay.GameplayEvents.Capture("entitydestroyed")){
                    Watch.Published=true;
                    Gameplay.EntityDestroyed(new GameplayEntityDestroyedObservation(Watch.Id,Watch.Prefab,Watch.EpochIdentity,Watch.KillPosition,
                        ()=>EntityDestroyedAvailable()&&Watch.Certified));
                    RequestDrain();
                }
                ReleaseDestroyWatch(Watch);
            }catch(Exception){DestroyRejected++;}
            finally{DestroyDepth--;Frame.Entity=null;Frame.Watch=null;Frame.Owner=null;}
        }
        private void StopEntityDestroyedSource()
        {
            DestroySourceStopped=true;DestroySourceQualified=DestroySourceArmed=false;
            foreach(var Watch in new List<DestroyWatch>(DestroyHeldWatches.Values))ReleaseDestroyWatch(Watch);
            StopEntityPoolWatches();
            DestroyWatches=new ConditionalWeakTable<object,DestroyWatch>();DestroyPatchStamps.Clear();
        }

        [AutoPatch(IsRequired=true),HarmonyPatch]
        private static class EntityDestroyKillPatch
        {
            [HarmonyTargetMethods]private static IEnumerable<MethodBase> TargetMethods(){return DestroyTargetPinned(DestroyKillMethod)?new MethodBase[]{DestroyKillMethod}:new MethodBase[0];}
            [HarmonyPrefix,HarmonyPriority(Priority.First)]private static void Prefix(BaseNetworkable __instance,out DestroyFrame __state){
                __state=null;var Owner=ActiveEntityObserver;if(Owner==null)return;
                try{__state=Owner.EnterEntityDestroy(__instance,false);}catch(Exception){Owner.DestroyRejected++;}}
            [HarmonyPostfix,HarmonyPriority(Priority.Last)]private static void Postfix(bool __runOriginal,DestroyFrame __state){if(__state!=null){__state.Postfix=true;__state.Original=__runOriginal;}}
            [HarmonyFinalizer,HarmonyPriority(Priority.Last)]private static void Finalizer(Exception __exception,DestroyFrame __state){if(__state!=null)__state.Owner.ExitEntityDestroy(__state,__exception);}
        }
        [AutoPatch(IsRequired=true),HarmonyPatch]
        private static class EntityDestroyTermPatch
        {
            [HarmonyTargetMethods]private static IEnumerable<MethodBase> TargetMethods(){return DestroyTargetPinned(DestroyTermMethod)?new MethodBase[]{DestroyTermMethod}:new MethodBase[0];}
            [HarmonyPrefix,HarmonyPriority(Priority.First)]private static void Prefix(BaseNetworkable __instance,out DestroyFrame __state){
                __state=null;var Owner=ActiveEntityObserver;if(Owner==null)return;
                try{__state=Owner.EnterEntityDestroy(__instance,true);}catch(Exception){Owner.DestroyRejected++;}}
            [HarmonyPostfix,HarmonyPriority(Priority.Last)]private static void Postfix(bool __runOriginal,DestroyFrame __state){if(__state!=null){__state.Postfix=true;__state.Original=__runOriginal;}}
            [HarmonyFinalizer,HarmonyPriority(Priority.Last)]private static void Finalizer(Exception __exception,DestroyFrame __state){if(__state!=null)__state.Owner.ExitEntityDestroy(__state,__exception);}
        }
    }
}
