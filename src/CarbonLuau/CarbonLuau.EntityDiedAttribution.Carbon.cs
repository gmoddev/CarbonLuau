using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Separate from the 67 death-completion targets. Reproduced processed pins;
        // Both exact-host actual RPC receipts qualified this recipe (B2 record).
        // Deployment approval is pinned source policy, never a fixture switch.
        private const bool EntityMeleeDeploymentQualified = true;
        private static readonly string[] EntityMeleePinRows = {
            "0600179e|3080|2e4852bd95fb79197e3b697582a11cf66438b59ddbd55d1e47d53defae1ef793|544a8d880f416b3f33c37071dce5c16036728e7b6d5e76c3bcebfb99127a8427|3029:9:3038:12:Finally,50:3009:3059:10:Finally,37:3032:3069:10:Finally",
            "06001795|187|73820ceb100d4a0ff2179290b440af3245f8de8a664d0b265d37ec60035d6621|73820ceb100d4a0ff2179290b440af3245f8de8a664d0b265d37ec60035d6621|34:14:48:10:Finally,87:9:96:10:Finally,155:9:164:10:Finally"
        };
        private static readonly MethodInfo EntityMeleeRpcMethod = ResolveGameplayMethod(typeof(BaseMelee), "PlayerAttack", 0x0600179e, typeof(BaseEntity.RPCMessage).FullName);
        private static readonly MethodInfo EntityMeleeSharedMethod = ResolveGameplayMethod(typeof(BaseMelee), "DoAttackShared", 0x06001795, "HitInfo");
        private static readonly MethodInfo EntityMeleeAttackedMethod = typeof(BaseEntity).GetMethod("OnAttacked", new[] {typeof(HitInfo)});
        private static bool EntityMeleeRpcMarker, EntityMeleeApplyMarker;
        private const int EntityMeleeFrameLimit = 16, EntityMeleeCaptureLimit = 128;
        private int EntityMeleeThread, EntityMeleeTurn = -1, EntityMeleeCaptures, EntityMeleeOffThread;
        private bool EntityMeleeInitialized, EntityMeleeStopped;
        private readonly List<EntityMeleeFrame> EntityMeleeFrames = new List<EntityMeleeFrame>(EntityMeleeFrameLimit);
        private readonly HashSet<Type> EntityMeleeTypes = new HashSet<Type>();
        private readonly byte[][] EntityMeleeStamps = new byte[2][];

        private sealed class EntityMeleeFrame
        {
            internal CarbonLuau Owner;
            internal BaseMelee Weapon;
            internal BasePlayer Actor;
            internal Network.Connection Connection;
            internal PlayerLifetime Lifetime;
            internal HitInfo Hit;
            internal BaseEntity Victim;
            internal EntityLifetimeModel.MembershipCandidate Witness;
            internal ulong Birth;
            internal bool Dispatch, SharedEntered, Applying, Poisoned, Exited;
            internal void Clear()
            {
                Owner=null; Weapon=null; Actor=null; Connection=null; Lifetime=null;
                Hit=null; Victim=null; Witness=null; Birth=0; Dispatch=SharedEntered=Applying=false;
            }
        }
        private static bool EntityMeleePinned(MethodInfo Method, int Index)
        {
            try {
                if (Method == null || !EntityDeathModulePinned || !ReferenceEquals(Method.Module, typeof(BaseEntity).Module)) return false;
                string[] Row=EntityMeleePinRows[Index].Split('|');
                MethodBody Body=Method.GetMethodBody(); byte[] Bytes=Body.GetILAsByteArray();
                if (Method.MetadataToken != Int32.Parse(Row[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture) ||
                    Bytes.Length != Int32.Parse(Row[1], CultureInfo.InvariantCulture)) return false;
                var EH=new List<string>();
                foreach(var Clause in Body.ExceptionHandlingClauses)
                    EH.Add(Clause.TryOffset+":"+Clause.TryLength+":"+Clause.HandlerOffset+":"+Clause.HandlerLength+":"+Clause.Flags);
                if (String.Join(",",EH) != Row[4]) return false;
                using(var Hash=SHA256.Create()) return BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant()==Row[Environment.OSVersion.Platform==PlatformID.Unix?3:2];
            } catch(Exception) { return false; }
        }
        private void InitializeEntityMeleeSource()
        {
            StopEntityMeleeSource(); EntityMeleeThread=Thread.CurrentThread.ManagedThreadId;
            EntityMeleeStopped=false; EntityMeleeOffThread=0;
            try {
                if (!VerifyGameplayHost() || !EntityMeleePinned(EntityMeleeRpcMethod,0) || !EntityMeleePinned(EntityMeleeSharedMethod,1) ||
                    EntityMeleeAttackedMethod==null || !EntityMeleeRpcMarker || !EntityMeleeApplyMarker) return;
                Type[] Types=typeof(BaseMelee).Assembly.GetTypes(); if(Types.Length>8192)return;
                foreach(Type Type in Types) if(typeof(BaseMelee).IsAssignableFrom(Type) &&
                    SameEntityMethod(Type.GetMethod("DoAttackShared",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(HitInfo)},null),EntityMeleeSharedMethod)) EntityMeleeTypes.Add(Type);
                EntityMeleeInitialized=EntityMeleeTypes.Count!=0 && EntityMeleeTopology();
            } catch(Exception) { EntityMeleeInitialized=false; }
        }
        private void StopEntityMeleeSource()
        {
            EntityMeleeStopped=true; EntityMeleeInitialized=false;
            // Shutdown is owner-thread. An off-thread caller only invalidates.
            if(EntityMeleeThread!=0 && Thread.CurrentThread.ManagedThreadId!=EntityMeleeThread){Interlocked.Exchange(ref EntityMeleeOffThread,1);return;}
            foreach(var Frame in EntityMeleeFrames){Frame.Poisoned=true;Frame.Clear();}
            EntityMeleeFrames.Clear();EntityMeleeTypes.Clear();EntityMeleeStamps[0]=EntityMeleeStamps[1]=null;
        }
        private bool EntityMeleeCandidateAvailable()
        {
            if(Thread.CurrentThread.ManagedThreadId!=EntityMeleeThread){Interlocked.Exchange(ref EntityMeleeOffThread,1);return false;}
            try { return EntityMeleeInitialized && !EntityMeleeStopped && EntityMeleeOffThread==0 && !Stopping && EntityDiedAvailable() && EntityMeleeTopology(); }
            catch(Exception){EntityMeleeInitialized=false;return false;}
        }
        private bool EntityMeleeAvailable(){return EntityMeleeCandidateAvailable() && EntityMeleeDeploymentQualified;}
        private bool EntityMeleeTopology()
        {
            IDictionary State=GameplayPatchState; object Locker=GameplayPatchLock;
            if(State==null||Locker==null||!Monitor.TryEnter(Locker,0))return false;
            try {
                if(!Monitor.TryEnter(State,0))return false;
                try {
                    if(State.Count>GameplayMaximumPatchRecords || !ReferenceEquals(State,GameplayPatchStateField.GetValue(null)) || !ReferenceEquals(Locker,GameplayPatchLockField.GetValue(null)))return false;
                    for(int Index=0;Index<2;Index++) {
                        var Target=Index==0?EntityMeleeRpcMethod:EntityMeleeSharedMethod;
                        byte[] Stamp=State[Target] as byte[];
                        if(Stamp==null||Stamp.Length>GameplayMaximumPatchBytes)return false;
                        if(ReferenceEquals(Stamp,EntityMeleeStamps[Index]))continue;
                        var Patches=Harmony.GetPatchInfo(Target); Type Own=Index==0?typeof(EntityMeleeRpcPatch):typeof(EntityMeleeApplyPatch);
                        if(Patches==null||Patches.InnerPrefixes.Count!=0||Patches.InnerPostfixes.Count!=0||Patches.Transpilers.Count!=1||
                            !OwnGameplayPatch(Patches.Transpilers[0],Own,"Transpiler")||Patches.Transpilers[0].priority!=Priority.Last)return false;
                        int Count=1;
                        if(Patches.Prefixes.Count!=Count||Patches.Postfixes.Count!=Count||Patches.Finalizers.Count!=Count)return false;
                        if(!OwnGameplayPatch(Patches.Prefixes[0],Own,"Prefix")||Patches.Prefixes[0].priority!=Priority.First||
                            !OwnGameplayPatch(Patches.Postfixes[0],Own,"Postfix")||Patches.Postfixes[0].priority!=Priority.Last||
                            !OwnGameplayPatch(Patches.Finalizers[0],Own,"Finalizer")||Patches.Finalizers[0].priority!=Priority.Last)return false;
                        EntityMeleeStamps[Index]=Stamp;
                    }
                    return true;
                } finally{Monitor.Exit(State);}
            } finally{Monitor.Exit(Locker);}
        }
        private EntityMeleeFrame EnterEntityMelee(BaseMelee Weapon, BaseEntity.RPCMessage Message)
        {
            if(!EntityMeleeCandidateAvailable())return null;
            int Turn=Time.frameCount; if(Turn!=EntityMeleeTurn){EntityMeleeTurn=Turn;EntityMeleeCaptures=0;}
            // Bound even rejected RPC captures; no entity/projectile/account history.
            if(EntityMeleeCaptures>=EntityMeleeCaptureLimit||EntityMeleeFrames.Count>=EntityMeleeFrameLimit){foreach(var Pending in EntityMeleeFrames)Pending.Poisoned=true;return null;}
            EntityMeleeCaptures++;
            var Frame=new EntityMeleeFrame{Owner=this,Weapon=Weapon,Actor=Message.player,Connection=Message.connection};
            // Any nested RPC poisons both scopes rather than laundering a caller.
            foreach(var Pending in EntityMeleeFrames){Pending.Poisoned=true;Frame.Poisoned=true;}
            if(Weapon==null||!EntityMeleeTypes.Contains(Weapon.GetType())||Message.connection==null||
                !ReferenceEquals(Message.connection.player,Message.player))Frame.Poisoned=true;
            else {
                Frame.Lifetime=CurrentGameplayPlayer(Message.player);
                if(Frame.Lifetime==null||!ReferenceEquals(Frame.Lifetime.Connection,Message.connection))Frame.Poisoned=true;
            }
            EntityMeleeFrames.Add(Frame);return Frame;
        }
        private void ExitEntityMelee(EntityMeleeFrame Frame, Exception Error)
        {
            if(Frame==null||Frame.Exited)return; Frame.Exited=true;
            if(Thread.CurrentThread.ManagedThreadId!=EntityMeleeThread){Interlocked.Exchange(ref EntityMeleeOffThread,1);return;}
            if(Error!=null||EntityMeleeFrames.Count==0||!ReferenceEquals(EntityMeleeFrames[EntityMeleeFrames.Count-1],Frame))Frame.Poisoned=true;
            EntityMeleeFrames.Remove(Frame);Frame.Clear();
        }
        private static void EntityMeleeDispatch(BaseMelee Weapon, HitInfo Hit)
        {
            var Owner=ActiveEntityObserver; EntityMeleeFrame Frame=null;
            try {
                if(Owner!=null&&Owner.EntityMeleeCandidateAvailable()&&Owner.EntityMeleeFrames.Count!=0) {
                    Frame=Owner.EntityMeleeFrames[Owner.EntityMeleeFrames.Count-1];
                    if(Frame.Poisoned||Frame.Dispatch||!ReferenceEquals(Frame.Weapon,Weapon)||Hit==null){Frame.Poisoned=true;Frame=null;}
                    else {Frame.Hit=Hit;Frame.Dispatch=true;}
                }
            } catch(Exception){if(Frame!=null)Frame.Poisoned=true;Frame=null;}
            try{Weapon.DoAttackShared(Hit);} // Preserve actual virtual dispatch and its exception.
            finally{if(Frame!=null){Frame.Dispatch=Frame.Applying=false;Frame.Hit=null;Frame.Victim=null;Frame.Witness=null;}}
        }
        private static void EntityMeleeApply(BaseEntity Victim, HitInfo Hit)
        {
            var Owner=ActiveEntityObserver;EntityMeleeFrame Frame=null;
            try {
                if(Owner!=null&&Owner.EntityMeleeCandidateAvailable()&&Owner.EntityMeleeFrames.Count!=0) {
                    Frame=Owner.EntityMeleeFrames[Owner.EntityMeleeFrames.Count-1];DestroyWatch Watch;
                    if(Frame.Poisoned||!Frame.Dispatch||!Frame.SharedEntered||Frame.Applying||!ReferenceEquals(Frame.Hit,Hit)||
                        !ReferenceEquals(Hit.HitEntity,Victim)||!Owner.EntityMeleeHitMatches(Frame,Hit)||
                        !Owner.DestroyWatches.TryGetValue(Victim,out Watch)||Watch.Closed||!Owner.EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness)) {
                        Frame.Poisoned=true;Frame=null;
                    } else {Frame.Victim=Victim;Frame.Witness=Watch.Witness;Frame.Birth=Watch.Witness.Birth;Frame.Applying=true;}
                }
            } catch(Exception){if(Frame!=null)Frame.Poisoned=true;Frame=null;}
            try{Victim.OnAttacked(Hit);} // Armed only for this exact original application.
            finally{if(Frame!=null){Frame.Applying=false;Frame.Victim=null;Frame.Witness=null;Frame.Birth=0;}}
        }
        private bool EntityMeleeHitMatches(EntityMeleeFrame Frame, HitInfo Hit)
        {
            if(Frame.Lifetime==null||CurrentGameplayPlayer(Frame.Actor,Frame.Lifetime)==null||
                !ReferenceEquals(Hit.Initiator,Frame.Actor)||!ReferenceEquals(Hit.Predicted,Frame.Connection)||
                !ReferenceEquals(Hit.Weapon,Frame.Weapon)||!ReferenceEquals(Hit.WeaponPrefab,Frame.Weapon)||
                Hit.ProjectileID!=0||!ReferenceEquals(Hit.ProjectilePrefab,null))return false;
            float[] Weights=Hit.damageTypes==null?null:Hit.damageTypes.types;
            if(Weights==null||Weights.Length!=28)return false; bool Positive=false;
            for(int Index=0;Index<28;Index++) {
                float Value=Weights[Index];
                if(Single.IsNaN(Value)||Single.IsInfinity(Value)||Value<0||Value>0&&(Index!=10&&Index!=11&&Index!=15))return false;
                Positive|=Value>0;
            }
            return Positive;
        }
        private bool TryEntityMeleeKiller(BaseEntity Victim, HitInfo Hit, out string Token, out string Id, out string Name)
        {
            Token=Id=Name=null;
            try {
                if(!EntityMeleeCandidateAvailable()||Hit==null||EntityMeleeFrames.Count==0||EntityDeathFrames.Count==0)return false;
                var Frame=EntityMeleeFrames[EntityMeleeFrames.Count-1];var Death=EntityDeathFrames[EntityDeathFrames.Count-1];
                if(Frame.Poisoned||!Frame.Applying||!ReferenceEquals(Frame.Hit,Hit)||!ReferenceEquals(Frame.Victim,Victim)||
                    !ReferenceEquals(Death.Entity,Victim)||Death.Poisoned||Death.Watch==null||
                    !ReferenceEquals(Death.Watch.Witness,Frame.Witness)||Frame.Birth!=Frame.Witness.Birth||
                    !EntityLifetimes.IsWitnessEpochUnchanged(Frame.Witness)||!EntityMeleeHitMatches(Frame,Hit))return false;
                // Fixture receipt observes candidate scalars only. No Lua, queue or
                // production metadata is admitted before actual fixture qualification.
                ObserveEntityMeleeSnapshotFixture(Victim,Frame.Lifetime.Token,Frame.Lifetime.UserId);
                if(Frame.Poisoned||!Frame.Applying||!EntityMeleeHitMatches(Frame,Hit))return false;
                if(!EntityMeleeDeploymentQualified)return false;
                Token=Frame.Lifetime.Token;Id=Frame.Lifetime.UserId;Name=Frame.Lifetime.Name;return true;
            } catch(Exception){Token=Id=Name=null;return false;}
        }
        partial void ObserveEntityMeleeSnapshotFixture(BaseEntity Victim, string Token, string Id);
        partial void ObserveEntityMeleeTerminalFixture(GameplayEntityDiedObservation Observation);

        // Main calls this immediately before producing its immutable observation.
        // No later directory lookup: a legitimately captured actor may disconnect.
        private bool EntityMeleeCompletionMatches(BaseEntity Victim,HitInfo Hit,string Token)
        {
            try {
                if(!EntityMeleeAvailable()||Hit==null||EntityMeleeFrames.Count==0||EntityDeathFrames.Count==0)return false;
                var Frame=EntityMeleeFrames[EntityMeleeFrames.Count-1];var Death=EntityDeathFrames[EntityDeathFrames.Count-1];
                if(Frame.Poisoned||!Frame.Applying||!ReferenceEquals(Frame.Hit,Hit)||!ReferenceEquals(Frame.Victim,Victim)||
                    Frame.Lifetime==null||Frame.Lifetime.Token!=Token||!ReferenceEquals(Hit.Initiator,Frame.Actor)||
                    !ReferenceEquals(Hit.Predicted,Frame.Connection)||!ReferenceEquals(Hit.Weapon,Frame.Weapon)||
                    !ReferenceEquals(Hit.WeaponPrefab,Frame.Weapon)||!ReferenceEquals(Hit.HitEntity,Victim)||
                    !ReferenceEquals(Death.Entity,Victim)||Death.Watch==null||!ReferenceEquals(Death.Watch.Witness,Frame.Witness)||
                    Frame.Witness==null||Frame.Birth!=Frame.Witness.Birth||!EntityLifetimes.IsWitnessEpochUnchanged(Frame.Witness))return false;
                var Values=Hit.damageTypes==null?null:Hit.damageTypes.types;
                if(Values==null||Values.Length!=28||Death.Weights==null||!ReferenceEquals(Values,Death.DamageArray))return false;
                for(int Index=0;Index<28;Index++)if(Values[Index]!=Death.Weights[Index])return false;
                return Hit.ProjectileID==0&&ReferenceEquals(Hit.ProjectilePrefab,null);
            } catch(Exception){return false;}
        }

        private static IEnumerable<CodeInstruction> EntityMeleeRewrite(IEnumerable<CodeInstruction> Input, MethodInfo Target, MethodInfo Replacement, int Limit, bool Rpc)
        {
            var Result=new List<CodeInstruction>();int Matches=0;
            foreach(var Instruction in Input) {
                if(Result.Count>=Limit)throw new InvalidOperationException("bounded melee source IL");
                if(Instruction.opcode==OpCodes.Callvirt&&SameEntityMethod(Instruction.operand as MethodBase,Target)) {
                    if(Instruction.blocks.Count!=0)throw new InvalidOperationException("melee source call block drift");
                    var Call=new CodeInstruction(OpCodes.Call,Replacement);Call.labels.AddRange(Instruction.labels);Result.Add(Call);Matches++;
                } else Result.Add(Instruction);
            }
            if(Matches!=1)throw new InvalidOperationException("melee source call count drift");
            if(Rpc)EntityMeleeRpcMarker=true;else EntityMeleeApplyMarker=true;return Result;
        }
        [AutoPatch(IsRequired=true),HarmonyPatch]
        private static class EntityMeleeRpcPatch
        {
            [HarmonyPrepare] private static bool Prepare(){return EntityMeleePinned(EntityMeleeRpcMethod,0);}
            [HarmonyTargetMethods] private static IEnumerable<MethodBase> Targets(){if(EntityMeleePinned(EntityMeleeRpcMethod,0))yield return EntityMeleeRpcMethod;}
            [HarmonyTranspiler,HarmonyPriority(Priority.Last)] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Input)
            {return EntityMeleeRewrite(Input,EntityMeleeSharedMethod,typeof(CarbonLuau).GetMethod("EntityMeleeDispatch",BindingFlags.Static|BindingFlags.NonPublic),1200,true);}
            [HarmonyPrefix,HarmonyPriority(Priority.First)] private static void Prefix(BaseMelee __instance,BaseEntity.RPCMessage __0,out EntityMeleeFrame __state)
            {__state=null;var Owner=ActiveEntityObserver;if(Owner==null)return;try{__state=Owner.EnterEntityMelee(__instance,__0);}catch(Exception){Owner.EntityMeleeInitialized=false;}}
            [HarmonyPostfix,HarmonyPriority(Priority.Last)] private static void Postfix(bool __runOriginal,EntityMeleeFrame __state){if(__state!=null&&!__runOriginal)__state.Poisoned=true;}
            [HarmonyFinalizer,HarmonyPriority(Priority.Last)] private static void Finalizer(Exception __exception,EntityMeleeFrame __state){if(__state!=null&&__state.Owner!=null)__state.Owner.ExitEntityMelee(__state,__exception);}
        }
        [AutoPatch(IsRequired=true),HarmonyPatch]
        private static class EntityMeleeApplyPatch
        {
            [HarmonyPrepare] private static bool Prepare(){return EntityMeleePinned(EntityMeleeSharedMethod,1);}
            [HarmonyTargetMethods] private static IEnumerable<MethodBase> Targets(){if(EntityMeleePinned(EntityMeleeSharedMethod,1))yield return EntityMeleeSharedMethod;}
            [HarmonyTranspiler,HarmonyPriority(Priority.Last)] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Input)
            {return EntityMeleeRewrite(Input,EntityMeleeAttackedMethod,typeof(CarbonLuau).GetMethod("EntityMeleeApply",BindingFlags.Static|BindingFlags.NonPublic),80,false);}
            [HarmonyPrefix,HarmonyPriority(Priority.First)] private static void Prefix(BaseMelee __instance,HitInfo __0,out EntityMeleeFrame __state)
            {
                __state=null;var Owner=ActiveEntityObserver;if(Owner==null||!Owner.EntityMeleeCandidateAvailable()||Owner.EntityMeleeFrames.Count==0)return;
                var Frame=Owner.EntityMeleeFrames[Owner.EntityMeleeFrames.Count-1];
                if(Frame.Poisoned||!Frame.Dispatch||Frame.SharedEntered||!ReferenceEquals(Frame.Weapon,__instance)||!ReferenceEquals(Frame.Hit,__0)){Frame.Poisoned=true;return;}
                Frame.SharedEntered=true;__state=Frame;
            }
            [HarmonyPostfix,HarmonyPriority(Priority.Last)] private static void Postfix(bool __runOriginal,EntityMeleeFrame __state){if(__state!=null&&!__runOriginal)__state.Poisoned=true;}
            [HarmonyFinalizer,HarmonyPriority(Priority.Last)] private static void Finalizer(Exception __exception,EntityMeleeFrame __state)
            {if(__state!=null){if(__exception!=null)__state.Poisoned=true;__state.SharedEntered=false;}}
        }
    }
}
