using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
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
        // Completion, not an OnEntityDeath notification or a new identity model.
        private const int EntityDeathFramesLimit = 16, EntityDeathTypeLimit = 8192, EntityDeathCriticalLimit = 8;
        private static readonly MethodInfo[] EntityDeathTargets = {
            ResolveGameplayMethod(typeof(BaseCombatEntity), "Die", 0x0600149b, "HitInfo"),
            ResolveGameplayMethod(typeof(BaseCombatEntity), "DieInstantly", 0x0600149c),
            ResolveGameplayMethod(typeof(BaseVehicleModule), "Die", 0x06001e7e, "HitInfo"),
            ResolveGameplayMethod(typeof(BuildingPrivlidge), "Die", 0x060020fb, "HitInfo"),
            ResolveGameplayMethod(typeof(SamSite), "Die", 0x060033cc, "HitInfo"),
            ResolveGameplayMethod(typeof(ResourceEntity), "OnDied", 0x06005a9e, "HitInfo"),
            ResolveGameplayMethod(typeof(MonumentBlocker), "OnDied", 0x06002d19, "HitInfo"),
            ResolveGameplayMethod(typeof(OreResourceEntity), "OnDied", 0x06002e14, "HitInfo"),
            ResolveGameplayMethod(typeof(TreeEntity), "OnDied", 0x060038fb, "HitInfo"),
            EntityDeathNamedMethod(typeof(TrainCar), "DoRepair"),
            EntityDeathNamedMethod(typeof(SamSite), "SelfHeal")
        };
        private const string EntityDeathResourceHook = "Carbon.Hooks.Category_Entity+Entity_ResourceEntity+Entity_ResourceEntity_e29a6af5261444aa96d48c13fde8cc9d";
        private static readonly FieldInfo EntityDeathResourceKilled = typeof(ResourceEntity).GetField("isKilled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo EntityDeathPrefab = typeof(BaseNetworkable).GetField("_prefabName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly string[] EntityDeathDamageNames = {"Generic","Hunger","Thirst","Cold","Drowned","Heat","Bleeding","Poison","Suicide","Bullet","Slash","Blunt","Fall","Radiation","Bite","Stab","Explosion","RadiationExposure","ColdExposure","Decay","ElectricShock","Arrow","AntiVehicle","Collision","Fun_Water","BeeSting","Paintball","Cannon"};
        private static bool EntityDeathMarkersValid;
        private static readonly bool EntityDeathModulePinned = HasHash(typeof(BaseEntity).Assembly,
            Environment.OSVersion.Platform == PlatformID.Unix ? LinuxRustHash : WindowsRustHash);
        private volatile bool EntityDeathArmed, EntityDeathStopped, EntityDeathOffThread;
        private int EntityDeathThread;
        private long EntityDeathCompleted, EntityDeathRejected, EntityDeathNested, EntityDeathMetadataDropped;
        private string EntityDeathFailure;
        private readonly List<EntityDeathFrame> EntityDeathFrames = new List<EntityDeathFrame>(EntityDeathFramesLimit);
        private readonly Dictionary<MethodBase, byte[]> EntityDeathPatchStamps = new Dictionary<MethodBase, byte[]>();
        private readonly Dictionary<Type, MethodInfo[]> EntityDeathTypeMethods = new Dictionary<Type, MethodInfo[]>();
        private readonly Dictionary<Type, MethodInfo> EntityDeathEffective = new Dictionary<Type, MethodInfo>();

        private sealed class EntityDeathCertificate
        {
            internal readonly WeakReference Observer;
            internal readonly ulong Birth;
            internal readonly EntityLifetimeModel.MembershipCandidate Witness;
            internal bool Certified;
            internal EntityDeathCertificate(CarbonLuau Observer, EntityLifetimeModel.MembershipCandidate Witness)
            { this.Observer = new WeakReference(Observer); this.Witness = Witness; Birth = Witness.Birth; }
            internal bool Validate()
            {
                var Owner = Observer.Target as CarbonLuau;
                return Certified && Birth != 0 && Owner != null && Owner.EntityDiedAvailable() &&
                    Owner.EntityLifetimes.IsHistoricalObservationCurrent(Witness);
            }
        }
        private sealed class EntityDeathFrame
        {
            internal CarbonLuau Owner;
            internal BaseEntity Entity;
            internal DestroyWatch Watch;
            internal EntityDeathCertificate Certificate;
            internal MethodBase Method;
            internal MethodInfo[] CriticalMethods;
            internal WeakReference[] TopologyStamps;
            internal HitInfo Hit;
            internal BaseEntity Initiator, WeaponPrefab;
            internal string KillerToken, KillerId, KillerName, DamageType, Weapon;
            internal float[] Weights;
            internal object DamageArray;
            internal PlayerPosition? Position;
            internal bool Marked, Postfix, Original, Poisoned, Exited, Reset;
            internal string PlayerToken;
            internal string EntryRespawnId;
            internal ulong PlayerEpoch;
        }
        private static MethodInfo EntityDeathNamedMethod(Type Type, string Name)
        {
            MethodInfo Result = null;
            foreach (MethodInfo Method in Type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (Method.Name == Name) { if (Result != null) return null; Result = Method; }
            return Result;
        }
        private static bool EntityDeathPinned(MethodInfo Method)
        {
            try {
                bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
                if (Method == null || Method.IsStatic || Method.IsAbstract || !EntityDeathModulePinned ||
                    !ReferenceEquals(Method.Module, typeof(BaseEntity).Module)) return false;
                foreach (string Row in EntityDeathPinRows) {
                    string[] Parts = Row.Split('|');
                    if (Parts.Length != 5 || Method.MetadataToken != Int32.Parse(Parts[0],
                        Parts[0].StartsWith("06", StringComparison.Ordinal) ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture)) continue;
                    MethodBody Body = Method.GetMethodBody(); byte[] Bytes = Body.GetILAsByteArray();
                    if (Bytes.Length != Int32.Parse(Parts[1], CultureInfo.InvariantCulture)) return false;
                    var EH = new List<string>();
                    foreach (var Clause in Body.ExceptionHandlingClauses)
                        EH.Add(Clause.TryOffset+":"+Clause.TryLength+":"+Clause.HandlerOffset+":"+Clause.HandlerLength+":"+Clause.Flags);
                    if (String.Join(",", EH) != Parts[4]) return false;
                    using (var Hash = SHA256.Create()) return BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant() == Parts[Linux ? 3 : 2];
                }
            } catch (Exception) { }
            return false;
        }
        private void InitializeEntityDiedSource()
        {
            EntityDeathThread = Thread.CurrentThread.ManagedThreadId;
            try {
                if (!VerifyGameplayHost() || EntityDeathResourceKilled == null || EntityDeathResourceKilled.FieldType != typeof(bool) ||
                    EntityDeathPrefab == null || EntityDeathPrefab.FieldType != typeof(string)) return;
                foreach (var Method in EntityDeathTargets) if (!EntityDeathPinned(Method)) {
                    EntityDeathFailure = "target pin " + (Method == null ? "missing target" : Method.DeclaringType.Name+"."+Method.Name); return;
                }
                Type[] Types = typeof(BaseEntity).Assembly.GetTypes();
                if (Types.Length > EntityDeathTypeLimit) return;
                var Verified = new HashSet<MethodInfo>();
                foreach (var Target in EntityDeathTargets) Verified.Add(Target);
                int Die = 0, OnDied = 0;
                foreach (Type Type in Types) {
                    if (!typeof(BaseCombatEntity).IsAssignableFrom(Type) && !typeof(ResourceEntity).IsAssignableFrom(Type)) continue;
                    var Methods = new List<MethodInfo>(); Type At = Type; int Depth = 0;
                    while (At != null && typeof(BaseEntity).IsAssignableFrom(At)) {
                        if (++Depth > 16) return;
                        foreach (MethodInfo Method in At.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                            if (Method.Name != "Die" && Method.Name != "OnDied") continue;
                            var Args = Method.GetParameters();
                            if (Method.ReturnType != typeof(void) || Args.Length != 1 || Args[0].ParameterType != typeof(HitInfo)) continue;
                            if (!Verified.Contains(Method)) { if (!EntityDeathPinned(Method)) return; Verified.Add(Method); }
                            if (!Methods.Contains(Method)) Methods.Add(Method);
                            if (At == Type && typeof(BaseCombatEntity).IsAssignableFrom(Type)) { if (Method.Name == "Die") Die++; else OnDied++; }
                        }
                        At = At.BaseType;
                    }
                    EntityDeathTypeMethods.Add(Type, Methods.ToArray());
                    EntityDeathEffective.Add(Type, Type.GetMethod(typeof(BaseCombatEntity).IsAssignableFrom(Type) ? "Die" : "OnDied",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] {typeof(HitInfo)}, null));
                }
                if (Die != 5 || OnDied != 55) return;
                EntityDeathArmed = true;
            } catch (Exception) { EntityDeathFailure = "cold host/type qualification"; }
        }
        private void StopEntityDiedSource()
        {
            EntityDeathStopped = true; EntityDeathArmed = false;
            if (EntityDeathThread != 0 && Thread.CurrentThread.ManagedThreadId != EntityDeathThread) { EntityDeathOffThread = true; return; }
            foreach (var Frame in EntityDeathFrames) { Frame.Poisoned = true; ClearEntityDeathFrame(Frame); }
            EntityDeathFrames.Clear();
            EntityDeathPatchStamps.Clear(); EntityDeathTypeMethods.Clear(); EntityDeathEffective.Clear();
        }
        private static void ClearEntityDeathFrame(EntityDeathFrame Frame)
        {
            Frame.Entity=null; Frame.Hit=null; Frame.Initiator=null; Frame.WeaponPrefab=null;
            Frame.Weights=null; Frame.DamageArray=null; Frame.Watch=null; Frame.Owner=null;
            Frame.Certificate=null;
            Frame.CriticalMethods=null;
            Frame.TopologyStamps=null;
        }
        private string EntityDiedStatus { get { return "[CarbonLuau:EntityDied] armed="+EntityDeathArmed+"; marker="+EntityDeathMarkersValid+
            "; completed="+EntityDeathCompleted+"; rejected="+EntityDeathRejected+"; nested="+EntityDeathNested+
            "; metadata_dropped="+EntityDeathMetadataDropped+"; failure="+(EntityDeathFailure??"none"); } }
        private bool EntityDiedAvailable()
        {
            if (Thread.CurrentThread.ManagedThreadId != EntityDeathThread) { EntityDeathOffThread = true; EntityDeathArmed = false; return false; }
            if (!EntityDeathArmed || !EntityDeathMarkersValid || EntityDeathStopped || EntityDeathOffThread || Stopping ||
                EntityObserverBroken || !EntityStartupQualified || !DestroySourceArmed || !DestroySourceQualified ||
                DestroySourceStopped || DestroyOffThread) return false;
            return EntityDeathTopology(EntityDeathTargets);
        }
        private bool EntityDeathTopology(IEnumerable<MethodInfo> Targets, EntityDeathFrame Attempt = null, bool Capture = false)
        {
            var State = GameplayPatchState; var Locker = GameplayPatchLock;
            if (State == null || Locker == null || !Monitor.TryEnter(Locker, 0)) return false;
            try {
                if (!Monitor.TryEnter(State, 0)) return false;
                try {
                    if (State.Count > GameplayMaximumPatchRecords || !ReferenceEquals(State, GameplayPatchStateField.GetValue(null)) ||
                        !ReferenceEquals(Locker, GameplayPatchLockField.GetValue(null))) return false;
                    int Index=0;
                    foreach (MethodInfo Target in Targets) {
                        object Record=State[Target];
                        byte[] Stamp = Record as byte[], Previous;
                        if (Record != null && Stamp == null) return false;
                        if (Stamp != null && Stamp.Length > GameplayMaximumPatchBytes) return false;
                        if (Attempt != null) {
                            if (Index >= EntityDeathCriticalLimit || Attempt.TopologyStamps == null || Index >= Attempt.TopologyStamps.Length) return false;
                            if (Capture) Attempt.TopologyStamps[Index]=Stamp==null?null:new WeakReference(Stamp);
                            else {
                                var Expected=Attempt.TopologyStamps[Index];
                                if (Expected==null ? Stamp!=null : Expected.Target==null || !ReferenceEquals(Expected.Target,Stamp)) return false;
                            }
                            Index++;
                        }
                        if (EntityDeathPatchStamps.TryGetValue(Target, out Previous) && ReferenceEquals(Stamp, Previous)) continue;
                        bool Own = Array.IndexOf(EntityDeathTargets, Target) >= 0;
                        bool Player = SameEntityMethod(Target, GameplayDieMethod);
                        bool Respawn = SameEntityMethod(Target, GameplayRespawnMethod);
                        bool Loot = SameEntityMethod(Target, LootDeathTarget);
                        string Hook = SameEntityMethod(Target, GameplayBaseDieMethod) ? GameplayEntityDeathHook :
                            Target.DeclaringType == typeof(ResourceEntity) && Target.Name == "OnDied" ? EntityDeathResourceHook :
                            Player ? GameplayDeathHook : Respawn ? GameplayRespawnHook : null;
                        if (!VerifyGameplayMethodTopology(Target, Own ? typeof(EntityDiedPatch) : Player ? typeof(GameplayDeathPatch) : Respawn ? typeof(GameplaySpawnPatch) : Loot ? typeof(LootDeathPatch) : null,
                            Hook, Own || Player)) return false;
                        EntityDeathPatchStamps[Target] = Stamp;
                    }
                    return true;
                } finally { Monitor.Exit(State); }
            } finally { Monitor.Exit(Locker); }
        }
        private EntityDeathFrame EnterEntityDeath(BaseEntity Entity, MethodBase Method, string PlayerToken = null, ulong PlayerEpoch = 0)
        {
            if (!EntityDiedAvailable() || Entity == null || Gameplay == null || Host == null || !Host.Ready) return null;
            ObserveEntityDeathCandidateFixture(Entity, Method);
            MethodInfo[] Methods;
            if (!EntityDeathTypeMethods.TryGetValue(Entity.GetType(), out Methods) || !EntityDeathTopology(Methods)) { LootCount(ref EntityDeathRejected); return null; }
            bool Reset = Method.Name == "DoRepair" || Method.Name == "SelfHeal" || SameEntityMethod(Method, GameplayRespawnMethod);
            bool Instant = Method.Name == "DieInstantly";
            bool Combat = Entity is BaseCombatEntity;
            if (!Reset && !Instant) {
                var Effective = EntityDeathEffective[Entity.GetType()];
                if (!SameEntityMethod(Effective, Method)) return null; // Ancestor call belongs to its existing outer frame.
            }
            var BCE = Entity as BaseCombatEntity;
            bool Dead = Combat ? BCE.lifestate == BaseCombatEntity.LifeState.Dead : (bool)EntityDeathResourceKilled.GetValue(Entity);
            if (Reset ? !Dead && !SameEntityMethod(Method, GameplayRespawnMethod) : Dead) return null;
            DestroyWatch Watch;
            if (!DestroyWatches.TryGetValue(Entity, out Watch) || Watch.Closed || !EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness) ||
                Entity.net == null || Entity.net.ID.Value.ToString(CultureInfo.InvariantCulture) != Watch.Id ||
                !Entity.IsFullySpawned() || !ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity)) return null;
            if (!Reset && (PlayerToken == null ? Watch.DeathSeen : Watch.DeathPlayerToken == PlayerToken && Watch.DeathPlayerEpoch == PlayerEpoch)) return null;
            if (EntityDeathFrames.Count >= EntityDeathFramesLimit) {
                foreach (var Pending in EntityDeathFrames) Pending.Poisoned = true;
                LootCount(ref EntityDeathRejected); return null;
            }
            var Critical = new List<MethodInfo>(Methods);
            bool ContainsMethod=false;
            foreach (var Target in Critical) if (SameEntityMethod(Target, Method)) ContainsMethod=true;
            if (!ContainsMethod) Critical.Add(Method as MethodInfo);
            if (Critical.Count > EntityDeathCriticalLimit || Critical.Contains(null)) return null;
            var Frame = new EntityDeathFrame {Owner=this, Entity=Entity, Watch=Watch, Method=Method, CriticalMethods=Critical.ToArray(),
                TopologyStamps=new WeakReference[Critical.Count], Reset=Reset, PlayerToken=PlayerToken, PlayerEpoch=PlayerEpoch};
            if (!EntityDeathTopology(Frame.CriticalMethods, Frame, true)) { ClearEntityDeathFrame(Frame); return null; }
            foreach (var Pending in EntityDeathFrames) if (ReferenceEquals(Pending.Entity, Entity)) {
                Pending.Poisoned = Frame.Poisoned = true; LootCount(ref EntityDeathNested);
            }
            if (!Reset && Gameplay.GameplayEvents.DeathPositionSnapshot() && EntityPositionReader != null && EntityPositionReader.Available && EntityDiscoveryPatchCurrent(Entity)) {
                EntityPositionComposition.Position Position;
                if (EntityPositionReader.TryObserve(Entity, out Position) && EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness))
                    Frame.Position = new PlayerPosition(Position.X, Position.Y, Position.Z);
            }
            EntityDeathFrames.Add(Frame); ObserveEntityDeathAttemptFixture(Frame,"entry",null); return Frame;
        }
        private EntityDeathFrame EnterEntityPlayerRespawn(BasePlayer Player)
        {
            if (Thread.CurrentThread.ManagedThreadId != EntityDeathThread) { EntityDeathOffThread=true; EntityDeathArmed=false; return null; }
            bool Busy;
            if (Player == null || !VerifyGameplayTopology(false, out Busy)) return null;
            string RespawnId = Player.respawnId;
            if (RespawnId != null && RespawnId.Length > 32) return null;
            var Frame = EnterEntityDeath(Player, GameplayRespawnMethod);
            if (Frame != null) Frame.EntryRespawnId = RespawnId;
            return Frame;
        }
        private static void MarkEntityDeath(BaseEntity Entity, HitInfo Hit)
        {
            var Owner = ActiveEntityObserver;
            EntityDeathFrame Frame = null;
            try {
                if (Owner == null || !Owner.EntityDiedAvailable() || Owner.EntityDeathFrames.Count == 0) return;
                Frame = Owner.EntityDeathFrames[Owner.EntityDeathFrames.Count - 1];
                if (!ReferenceEquals(Frame.Entity, Entity) || Frame.Reset || Frame.Poisoned || Frame.Marked) return;
                Frame.Marked = true; Frame.Hit = Hit;
                Owner.ObserveEntityDeathAttemptFixture(Frame,"marker",null);
                Owner.SnapshotEntityDeathHit(Frame, Hit);
            } catch (Exception) {
                // Observer instrumentation never changes host death semantics.
                // Unknown optional metadata does not turn a completed death into
                // a fabricated actor/cause or escape through the injected marker.
                if (Frame != null) {
                    Frame.KillerToken=Frame.KillerId=Frame.KillerName=Frame.DamageType=Frame.Weapon=null;
                    Frame.Initiator=Frame.WeaponPrefab=null; Frame.Weights=null; Frame.DamageArray=null;
                }
                if (Owner != null) LootCount(ref Owner.EntityDeathMetadataDropped);
            }
        }
        private void SnapshotEntityDeathHit(EntityDeathFrame Frame, HitInfo Hit)
        {
            if (Hit == null) return;
            Frame.Initiator = Hit.Initiator; Frame.WeaponPrefab = Hit.WeaponPrefab;
            // Direct field type is not original-action provenance: vanilla may
            // already have resolved an indirect owner by account ID. Keep nil
            // until the separately qualified synchronous writer fence proves it.
            TryEntityMeleeKiller(Frame.Entity, Hit, out Frame.KillerToken, out Frame.KillerId, out Frame.KillerName);
            if (Hit.damageTypes != null && Hit.damageTypes.types != null && Hit.damageTypes.types.Length == 28) {
                var Values = Hit.damageTypes.types; Frame.DamageArray = Values; Frame.Weights = (float[])Values.Clone();
                float Best = 0; int Selected = -1;
                for (int Index = 0; Index < 28; Index++) {
                    float Value = Values[Index];
                    if (Single.IsNaN(Value) || Single.IsInfinity(Value) || Value < 0) { Selected=-1; break; }
                    if (Value > 0 && Value >= Best) { Best=Value; Selected=Index; }
                }
                if (Selected >= 0) Frame.DamageType = EntityDeathDamageNames[Selected];
            }
            if (!ReferenceEquals(Hit.WeaponPrefab, null)) {
                string Name = EntityDeathPrefab.GetValue(Hit.WeaponPrefab) as string;
                if (Name != null && Name.Length <= 512 && Name.StartsWith("assets/", StringComparison.Ordinal) && Name.EndsWith(".prefab", StringComparison.Ordinal)) {
                    bool Valid = Name.Length != 0;
                    foreach (char Value in Name) if (Value < 32 || Value == 127 || Value == '\\' || Char.IsUpper(Value)) Valid=false;
                    if (Valid && FacadePolicy.Utf8.GetByteCount(Name) <= 512) Frame.Weapon = Name;
                }
            }
        }
        private void CompleteEntityDeath(EntityDeathFrame Frame, Exception Error)
        {
            if (Thread.CurrentThread.ManagedThreadId != EntityDeathThread) { EntityDeathOffThread=true; EntityDeathArmed=false; return; }
            if (Frame == null || Frame.Exited) return;
            Frame.Exited = true;
            try {
                ObserveEntityDeathAttemptFixture(Frame,"completion",Error);
                if (!EntityDiedAvailable() || Frame.Poisoned || Error != null || !Frame.Postfix || !Frame.Original ||
                    Frame.CriticalMethods == null || !EntityDeathTopology(Frame.CriticalMethods, Frame) ||
                    EntityDeathFrames.Count == 0 || !ReferenceEquals(EntityDeathFrames[EntityDeathFrames.Count-1], Frame) ||
                    !EntityLifetimes.IsWitnessEpochUnchanged(Frame.Watch.Witness)) return;
                var BCE = Frame.Entity as BaseCombatEntity;
                bool Dead = !ReferenceEquals(BCE, null) ? BCE.lifestate == BaseCombatEntity.LifeState.Dead : (bool)EntityDeathResourceKilled.GetValue(Frame.Entity);
                if (Frame.Reset) {
                    if (SameEntityMethod(Frame.Method, GameplayRespawnMethod)) {
                        bool Busy; var Player = Frame.Entity as BasePlayer;
                        if (Player == null || !VerifyGameplayTopology(false, out Busy) || Player.IsWounded() ||
                            String.IsNullOrEmpty(Player.respawnId) || Player.respawnId.Length != 32 ||
                            String.Equals(Frame.EntryRespawnId, Player.respawnId, StringComparison.Ordinal) ||
                            Single.IsNaN(Player.health) || Single.IsInfinity(Player.health) || Player.health <= 0 ||
                            Player.net == null || Player.net.ID.Value.ToString(CultureInfo.InvariantCulture) != Frame.Watch.Id ||
                            !Player.IsFullySpawned() || !ReferenceEquals(BaseNetworkable.serverEntities.Find(Player.net.ID), Player)) return;
                    }
                    if (!Dead && !Frame.Watch.Closed) { Frame.Watch.DeathSeen=false; Frame.Watch.DeathPlayerToken=null; Frame.Watch.DeathPlayerEpoch=0; }
                    return;
                }
                if (!Frame.Marked || (!Dead && !Frame.Watch.Certified)) return;
                Frame.Watch.DeathSeen = true; Frame.Watch.DeathPlayerToken = Frame.PlayerToken; Frame.Watch.DeathPlayerEpoch = Frame.PlayerEpoch;
                LootCount(ref EntityDeathCompleted);
                // An actor snapshot is not a last-attacker/owner lookup. Mutation
                // or reuse of the death-associated hit makes its metadata unknown.
                if (Frame.Hit != null) {
                    if (!ReferenceEquals(Frame.Hit.Initiator, Frame.Initiator) || (Frame.KillerToken != null &&
                        !EntityMeleeCompletionMatches(Frame.Entity, Frame.Hit, Frame.KillerToken))) Frame.KillerToken=Frame.KillerId=Frame.KillerName=null;
                    if (!ReferenceEquals(Frame.Hit.WeaponPrefab, Frame.WeaponPrefab)) Frame.Weapon=null;
                    var Values = Frame.Hit.damageTypes == null ? null : Frame.Hit.damageTypes.types;
                    if (!ReferenceEquals(Values, Frame.DamageArray) || Frame.Weights == null) Frame.DamageType=null;
                    else for (int Index=0; Index<28; Index++) if (Values[Index] != Frame.Weights[Index]) { Frame.DamageType=null; break; }
                }
                var Certificate = new EntityDeathCertificate(this, Frame.Watch.Witness) {Certified=true};
                var Observation = new GameplayEntityDiedObservation(Frame.Watch.Id, Frame.Watch.Prefab, Frame.Watch.EpochIdentity,
                    Frame.Position, Certificate.Validate, Frame.KillerToken, Frame.KillerId, Frame.KillerName, Frame.DamageType, Frame.Weapon);
                ObserveEntityDeathQualification(Observation);
                ObserveEntityMeleeTerminalFixture(Observation);
                if (Gameplay != null && Host != null && Host.Ready && Gameplay.GameplayEvents.Capture("entitydied")) {
                    Gameplay.EntityDied(Observation); RequestDrain();
                }
            } catch (Exception) { LootCount(ref EntityDeathRejected); }
            finally {
                EntityDeathFrames.Remove(Frame); ClearEntityDeathFrame(Frame);
            }
        }
        partial void ObserveEntityDeathQualification(GameplayEntityDiedObservation Observation);
        partial void ObserveEntityDeathAttemptFixture(EntityDeathFrame Frame, string Stage, Exception Error);
        partial void ObserveEntityDeathCandidateFixture(BaseEntity Entity, MethodBase Method);

        [AutoPatch(IsRequired=true), HarmonyPatch]
        private static class EntityDiedPatch
        {
            [HarmonyPrepare] private static bool Prepare()
            { foreach (var Method in EntityDeathTargets) if (!EntityDeathPinned(Method)) return false; return true; }
            [HarmonyTargetMethods] private static IEnumerable<MethodBase> TargetMethods()
            { foreach (var Method in EntityDeathTargets) if (EntityDeathPinned(Method)) yield return Method; }
            [HarmonyTranspiler, HarmonyPriority(Priority.Last)]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Input, MethodBase __originalMethod)
            {
                var Source = new List<CodeInstruction>(Input);
                if (Source.Count > 4096) throw new InvalidOperationException("bounded death IL qualification");
                var Result = new List<CodeInstruction>(Source.Count+4); int Markers=0;
                var Marker = typeof(CarbonLuau).GetMethod("MarkEntityDeath", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (var Instruction in Source) {
                    Result.Add(Instruction);
                    FieldInfo Field = Instruction.operand as FieldInfo;
                    if (Instruction.opcode != OpCodes.Stfld || Field == null ||
                        !((Field.DeclaringType==typeof(BaseCombatEntity) && Field.Name=="lifestate") ||
                          (Field.DeclaringType==typeof(ResourceEntity) && Field.Name=="isKilled"))) continue;
                    Result.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    Result.Add(new CodeInstruction(__originalMethod.GetParameters().Length==1 && __originalMethod.GetParameters()[0].ParameterType==typeof(HitInfo) ? OpCodes.Ldarg_1 : OpCodes.Ldnull));
                    Result.Add(new CodeInstruction(OpCodes.Call, Marker)); Markers++;
                }
                if (SameEntityMethod(__originalMethod, GameplayBaseDieMethod)) EntityDeathMarkersValid=Markers==1;
                return Result;
            }
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(BaseEntity __instance, MethodBase __originalMethod, out EntityDeathFrame __state)
            {
                __state=null; var Owner=ActiveEntityObserver;
                if (Owner==null) return;
                try { __state=Owner.EnterEntityDeath(__instance,__originalMethod); }
                catch(Exception){LootCount(ref Owner.EntityDeathRejected);}
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, EntityDeathFrame __state)
            { if(__state!=null){__state.Postfix=true;__state.Original=__runOriginal;} }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, EntityDeathFrame __state)
            { if(__state!=null&&__state.Owner!=null)__state.Owner.CompleteEntityDeath(__state,__exception); }
        }
    }
}
