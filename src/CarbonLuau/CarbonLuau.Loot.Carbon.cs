using System;
using System.Collections;
using System.Collections.Generic;
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
        // Private D2A only: no service, script operation, callback or public enum.
        private static readonly MethodInfo LootDeathTarget = ResolveGameplayMethod(typeof(LootContainer), "OnDied", 0x06005773, "HitInfo");
        private static readonly MethodInfo LootPayoutTarget = typeof(StorageContainer).GetMethod("DropItems",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            new[] { typeof(IItemContainerEntity), typeof(BaseEntity) }, null);
        private static readonly MethodInfo LootWorldTarget = ResolveGameplayMethod(typeof(Item), "CreateWorldObject", 0x06006a37,
            "UnityEngine.Vector3", "UnityEngine.Quaternion", "BaseEntity", "System.UInt32");
        private static readonly MethodInfo[] LootTargets = { LootDeathTarget, LootPayoutTarget, LootWorldTarget };
        private static readonly FieldInfo LootComponentsField = typeof(BaseEntity).GetField("_components",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        // Same independently resolved opcode/member/EH contract; Carbon's
        // publicized Windows operand tokens differ from the Linux token layout.
        private static string LootDeathHash { get { return Environment.OSVersion.Platform == PlatformID.Unix ?
            "993ae54aa5e6c11444abee185f2fb90bd3c5be17d883b3357d88150bc0ad7329" :
            "a6851b9116507fcb7e054118e476e0cac8b6a51d39cd10e0824b19e68d0694af"; } }
        private readonly byte[][] LootPatchStamps = new byte[3][];
        private bool LootArmed;
        private static bool LootWorldMarker;
        private int LootThread, LootFrameNumber = -1, LootFrameUsed;
        private LootPayoutFrame LootCurrent;
        private long LootSelected, LootVerified, LootPartial, LootRejected, LootCleanupFailed, LootOffThread;

        private sealed class LootPayoutFrame
        {
            internal CarbonLuau Owner;
            internal LootDropRule Rule;
            internal FacadeSession Domain;
            internal LootContainer Source;
            internal EntityFacadeBinding Binding;
            internal DestroyWatch Removal;
            internal ItemContainer Container;
            internal Vector3 Position;
            internal readonly int[] Amounts = new int[LootRulePolicy.Entries];
            internal readonly ItemDefinition[] Definitions = new ItemDefinition[LootRulePolicy.Entries];
            internal readonly Item[] Items = new Item[LootRulePolicy.Entries];
            internal readonly ulong[] Uids = new ulong[LootRulePolicy.Entries];
            internal readonly BaseEntity[] Worlds = new BaseEntity[LootRulePolicy.Entries];
            internal readonly EntityLifetimeModel.MembershipCandidate[] WorldWitnesses = new EntityLifetimeModel.MembershipCandidate[LootRulePolicy.Entries];
            internal readonly ulong[] WorldIds = new ulong[LootRulePolicy.Entries];
            internal readonly string[] WorldPrefabs = new string[LootRulePolicy.Entries];
            internal readonly EntityLifetimeModel.SpawnAttempt[] WorldAttempts = new EntityLifetimeModel.SpawnAttempt[LootRulePolicy.Entries];
            internal readonly bool[] Accepted = new bool[LootRulePolicy.Entries];
            internal int Index = -1, Created;
            internal bool Committed, PayoutComplete, Postfix, Original, Exited, Poisoned;
        }

        private static void LootCount(ref long Value) { if (Value != Int64.MaxValue) Value++; }
        private string LootStatus { get { return "[CarbonLuau:Loot] private_only=true; pinned=" + LootArmed +
            "; factory_fence=" + LootWorldMarker + "; selected=" + LootSelected + "; verified=" + LootVerified +
            "; partial=" + LootPartial + "; rejected=" + LootRejected + "; cleanup_failed=" + LootCleanupFailed +
            "; off_thread=" + Interlocked.Read(ref LootOffThread); } }
        private void StopLootSource()
        {
            LootArmed = false;
            if (Gameplay != null) Gameplay.LootAvailable = null;
        }
        private static bool LootMethodPinned(MethodInfo Method, int Token, int Size, string HashText)
        {
            try {
                bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
                if (Method == null || Method.MetadataToken != Token || Method.IsAbstract ||
                    !HasHash(Method.Module.Assembly, Linux ? LinuxRustHash : WindowsRustHash)) return false;
                MethodBody Body = Method.GetMethodBody();
                if (Body == null || Body.ExceptionHandlingClauses.Count != 0) return false;
                byte[] Bytes = Body.GetILAsByteArray();
                if (Bytes.Length != Size) return false;
                using (var Hash = SHA256.Create()) return BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant() == HashText;
            } catch (Exception) { return false; }
        }
        private void InitializeLootSource()
        {
            LootThread = Thread.CurrentThread.ManagedThreadId;
            LootArmed = EntityHostPinned && LootComponentsField != null &&
                LootComponentsField.FieldType == typeof(List<EntityComponentBase>) &&
                LootMethodPinned(LootDeathTarget, 0x06005773, 83, LootDeathHash) &&
                LootMethodPinned(LootPayoutTarget, 0x06003701, 160, "14dbfbcb12139d86b2fd759c98b73020282e2b1460f1244769c782cffeddc0d0") &&
                LootMethodPinned(LootWorldTarget, 0x06006a37, 121, "cd31acc55c0044763b9757597bcbe8d12fe5463254c743975466bdcb3bad2c08");
            if (Gameplay != null) Gameplay.LootAvailable = LootAvailable;
        }
        private bool LootAvailable()
        {
            if (!LootArmed || !LootWorldMarker || Stopping || Thread.CurrentThread.ManagedThreadId != LootThread ||
                EntityObserverBroken || !EntityStartupQualified || EntityPositionReader == null || !EntityPositionReader.Available) return false;
            return LootTopology() && EntityDestroyedAvailable();
        }
        private bool LootTopology()
        {
            IDictionary State = GameplayPatchState; object Locker = GameplayPatchLock;
            if (State == null || Locker == null || !Monitor.TryEnter(Locker, 0)) return false;
            try {
                if (!Monitor.TryEnter(State, 0)) return false;
                try {
                    if (State.Count > GameplayMaximumPatchRecords || !ReferenceEquals(GameplayPatchStateField.GetValue(null), State) ||
                        !ReferenceEquals(GameplayPatchLockField.GetValue(null), Locker)) return false;
                    MethodInfo[] Targets = LootTargets;
                    for (int Index = 0; Index < Targets.Length; Index++) {
                        object Record = State[Targets[Index]]; byte[] Stamp = Record as byte[];
                        if (Stamp == null || Stamp.Length > GameplayMaximumPatchBytes) return false;
                        if (ReferenceEquals(Stamp, LootPatchStamps[Index])) continue;
                        if (Index < 2) {
                            if (!VerifyGameplayMethodTopology(Targets[Index], Index == 0 ? typeof(LootDeathPatch) : typeof(LootPayoutPatch), null, false)) return false;
                        } else {
                            HarmonyLib.Patches Patches = Harmony.GetPatchInfo(Targets[Index]);
                            if (Patches == null || Patches.Prefixes.Count != 0 || Patches.Postfixes.Count != 0 ||
                                Patches.Finalizers.Count != 0 || Patches.InnerPrefixes.Count != 0 || Patches.InnerPostfixes.Count != 0 ||
                                Patches.Transpilers.Count != 1 || !OwnGameplayPatch(Patches.Transpilers[0], typeof(LootWorldPatch), "Transpiler") ||
                                Patches.Transpilers[0].priority != Priority.Last) return false;
                        }
                        LootPatchStamps[Index] = Stamp;
                    }
                    return true;
                } finally { Monitor.Exit(State); }
            } finally { Monitor.Exit(Locker); }
        }
        private LootPayoutFrame EnterLoot(LootContainer Source)
        {
            if (Thread.CurrentThread.ManagedThreadId != LootThread) {
                long Before = Interlocked.Read(ref LootOffThread);
                if (Before != Int64.MaxValue) Interlocked.CompareExchange(ref LootOffThread, Before + 1, Before);
                return null;
            }
            if (LootCurrent != null) { LootCurrent.Poisoned = true; LootCount(ref LootRejected); return null; }
            if (!LootAvailable() || Gameplay == null) return null;
            FacadeSession Domain; LootDropRule Rule;
            if (Source == null || Source.GetType() != typeof(LootContainer) || Source.PrefabName != LootRulePolicy.Prefab ||
                Source.prefabID != 966676416 || !Gameplay.TryLootRule(LootRulePolicy.Prefab, out Domain, out Rule)) return null;
            // Fixed prefab settings exclude pre-OnDied corpse and post-payout debris.
            // Altered/trusted plugin settings make this occurrence ineligible.
            // InitShared populated the pinned cache before completed Spawn. Never
            // invoke Components here: its cold branch recursively scans children.
            var Components = LootComponentsField.GetValue(Source) as List<EntityComponentBase>;
            if (Source.spawnDeployableCorpseOnDeath || Source.debrisPrefab == null ||
                !String.IsNullOrEmpty(Source.debrisPrefab.guid) || Components == null || Components.Count != 0 ||
                Source.inventory == null || Source.inventory.itemList == null || Source.inventory.itemList.Count > FacadePolicy.InventoryStacks ||
                !ReferenceEquals(Source.inventory.entityOwner, Source) || !ConVar.Server.dropitems) { LootCount(ref LootRejected); return null; }
            DestroyWatch Removal; EntityFacadeBinding Binding;
            EntityPositionComposition.Position Position;
            if (!DestroyWatches.TryGetValue(Source, out Removal) || Removal.Closed || Removal.Certified ||
                !TryAdmitEntity(Source, Domain, out Binding) || !EntityPositionReader.TryObserve(Source, out Position) ||
                Position.X == 0 && Position.Y == 0 && Position.Z == 0) { LootCount(ref LootRejected); return null; }
            int Frame = Time.frameCount;
            if (Frame != LootFrameNumber) { LootFrameNumber = Frame; LootFrameUsed = 0; }
            if (LootFrameUsed >= LootRulePolicy.PerFrame) { LootCount(ref LootRejected); return null; }
            var Result = new LootPayoutFrame { Owner = this, Rule = Rule, Domain = Domain, Source = Source,
                Binding = Binding, Removal = Removal, Container = Source.inventory, Position = new Vector3(Position.X, Position.Y, Position.Z) };
            for (int Index = 0; Index < Rule.Count; Index++) {
                LootDropEntry Entry = Rule.Entry(Index);
                ItemDefinition Definition = ItemManager.FindItemDefinition(Entry.Item);
                if (Definition == null || Definition.stackable < Entry.Max || Definition.itemMods == null || Definition.itemMods.Length > 8) return null;
                Result.Definitions[Index] = Definition;
                Result.Amounts[Index] = UnityEngine.Random.Range(Entry.Min, checked(Entry.Max + 1));
            }
            LootFrameUsed++; LootCount(ref LootSelected); LootCurrent = Result; return Result;
        }
        private bool ReplaceLoot(IItemContainerEntity Container)
        {
            LootPayoutFrame Frame = LootCurrent;
            if (Frame == null || !ReferenceEquals(Frame.Source, Container)) return true;
            if (Frame.Committed) { Frame.Poisoned = true; return false; }
            if (Frame.Poisoned || !LootAvailable() || !ValidateEntity(Frame.Binding) ||
                !ReferenceEquals(Frame.Source.inventory, Frame.Container)) return true;
            // Suppression itself begins COMMIT. Never fall back after this point.
            Frame.Committed = true;
            try {
                for (int Index = 0; Index < Frame.Rule.Count; Index++) {
                    int Amount = Frame.Amounts[Index]; if (Amount == 0) continue;
                    // The selected immutable policy may finish after publication
                    // changes, but original physical Entity identity never relaxes.
                    if (Frame.Poisoned || !EntityLifetimes.Validate(Frame.Binding.Lifetime, Authority => true, ReadEntityEvidence))
                        throw new InvalidOperationException("loot source changed");
                    Frame.Index = Index;
                    if (Frame.Definitions[Index] == null || Frame.Definitions[Index].stackable < Amount ||
                        Frame.Definitions[Index].itemMods == null || Frame.Definitions[Index].itemMods.Length > 8)
                        throw new InvalidOperationException("loot definition premises changed");
                    Item Value = ItemManager.Create(Frame.Definitions[Index], Amount, 0);
                    Frame.Items[Index] = Value; Frame.Created++;
                    if (Value == null) throw new InvalidOperationException("loot creation failed");
                    Frame.Uids[Index] = Value.uid.Value;
                    if (!Value.IsValid() || Value.uid.Value == 0 || Value.info == null || Value.amount != Amount ||
                        !ReferenceEquals(Value.info, Frame.Definitions[Index]) || Value.info.shortname != Frame.Rule.Entry(Index).Item ||
                        Value.parent != null || Value.GetWorldEntity() != null)
                        throw new InvalidOperationException("loot creation premises changed");
                    BaseEntity World = Value.Drop(Frame.Position, Vector3.zero);
                    if (!ReferenceEquals(World, Frame.Worlds[Index]) || !VerifyLootWorld(Frame, Index)) throw new InvalidOperationException("loot drop unverified");
                    Frame.Accepted[Index] = true;
                }
                Frame.PayoutComplete = !Frame.Poisoned;
            } catch (Exception) { Frame.Poisoned = true; }
            finally { Frame.Index = -1; }
            return false;
        }
        private bool LootWorldOwned(LootPayoutFrame Frame, int Index)
        {
            Item Value = Frame.Items[Index]; WorldItem World = Frame.Worlds[Index] as WorldItem;
            if (Value == null || World == null || World.IsDestroyed || World.net == null || !World.IsFullySpawned() ||
                World.net.ID.Value == 0 ||
                Value.uid.Value != Frame.Uids[Index] || Value.parent != null ||
                !ReferenceEquals(World.item, Value) || !ReferenceEquals(Value.GetWorldEntity(), World) ||
                !ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World)) return false;
            var Witness = Frame.WorldWitnesses[Index];
            if (Witness == null) {
                Witness = EntityLifetimes.CaptureCompletedWitness(World);
                if (Witness == null) return false;
                Frame.WorldWitnesses[Index] = Witness;
                Frame.WorldIds[Index] = World.net.ID.Value;
                Frame.WorldPrefabs[Index] = World.PrefabName;
            }
            return EntityLifetimes.IsWitnessEpochUnchanged(Witness) &&
                ReferenceEquals(Witness.Target, World) && World.net.ID.Value == Frame.WorldIds[Index] &&
                World.PrefabName == Frame.WorldPrefabs[Index];
        }
        private bool VerifyLootWorld(LootPayoutFrame Frame, int Index)
        {
            Item Value = Frame.Items[Index];
            return LootWorldOwned(Frame, Index) && Value.IsValid() && Value.amount == Frame.Amounts[Index] &&
                Value.info != null && ReferenceEquals(Value.info, Frame.Definitions[Index]) && Value.info.shortname == Frame.Rule.Entry(Index).Item;
        }
        private static void CaptureLootWorld(BaseEntity World, Item Value)
        {
            CarbonLuau Owner = ActiveEntityObserver;
            if (Owner == null || Thread.CurrentThread.ManagedThreadId != Owner.LootThread) return;
            LootPayoutFrame Frame = Owner.LootCurrent;
            if (Frame == null || !Frame.Committed || Frame.Index < 0 || !ReferenceEquals(Frame.Items[Frame.Index], Value)) return;
            if (Value.uid.Value != Frame.Uids[Frame.Index]) { Frame.Poisoned = true; return; }
            // Genuine CreateEntity return, captured before InitializeItem/Spawn.
            Frame.Worlds[Frame.Index] = World;
            Owner.LootFixtureWorldCaptured(World, Value);
        }
        private void ObserveLootWorldSpawnAttempt(BaseEntity World, EntityLifetimeModel.SpawnAttempt Attempt)
        {
            LootPayoutFrame Frame = LootCurrent;
            if (Frame == null || Frame.Index < 0 || Attempt == null || ReferenceEquals(World, null) ||
                !ReferenceEquals(Frame.Worlds[Frame.Index], World)) return;
            if (Frame.WorldAttempts[Frame.Index] != null) { Frame.Poisoned = true; return; }
            Frame.WorldAttempts[Frame.Index] = Attempt;
        }
        private void ExitLoot(LootPayoutFrame Frame, Exception Error)
        {
            if (Frame == null || Frame.Exited) return;
            Frame.Exited = true;
            try {
                bool Success = false;
                try {
                    Success = Frame.Committed && Frame.PayoutComplete && !Frame.Poisoned && Frame.Postfix && Frame.Original &&
                        Error == null && LootAvailable() && Frame.Removal.Certified &&
                        EntityLifetimes.IsWitnessEpochUnchanged(Frame.Removal.Witness);
                    // Reverify every accepted physical owner at the final observation.
                    if (Success) for (int Index = 0; Index < Frame.Rule.Count; Index++)
                        if (Frame.Amounts[Index] != 0 && !VerifyLootWorld(Frame, Index)) { Success = false; break; }
                } catch (Exception) { Success = false; }
                if (Success) LootCount(ref LootVerified);
                else if (Frame.Committed) LootCount(ref LootPartial);
                for (int Index = 0; Index < Frame.Rule.Count; Index++) {
                    if (Frame.Accepted[Index]) continue; // Never compensate/rollback an accepted world payout.
                    Item Value = Frame.Items[Index]; if (Value == null) continue;
                    try {
                        if (Value.uid.Value == 0 || Value.uid.Value != Frame.Uids[Index] || Value.info == null) throw new InvalidOperationException("loot cleanup identity changed");
                        // Host ownership can complete before Drop returns, or
                        // survive an external quantity change. Neither permits
                        // compensating deletion of an accepted world payout.
                        if (LootWorldOwned(Frame, Index)) { Frame.Accepted[Index] = true; continue; }
                        BaseEntity World = Frame.Worlds[Index];
                        if (World != null) {
                            var Attempt = Frame.WorldAttempts[Index];
                            if (Attempt != null && !EntityLifetimes.IsAttemptEpochUnchanged(Attempt))
                                throw new InvalidOperationException("loot cleanup Spawn incarnation changed");
                            // Never delete a completed different/unknown owner.
                            if (EntityLifetimes.HasCompletedObservation(World))
                                throw new InvalidOperationException("completed loot world owner is not verified");
                            if (World is WorldItem && (((WorldItem)World).item == null || ReferenceEquals(((WorldItem)World).item, Value))) {
                                if (!World.IsDestroyed) World.Kill();
                                if (World.net != null) throw new InvalidOperationException("loot cleanup vetoed");
                            } else throw new InvalidOperationException("loot world owner changed");
                        }
                        if (Value.uid.Value == 0) continue;
                        if (Value.uid.Value != Frame.Uids[Index] || !ReferenceEquals(Value.info, Frame.Definitions[Index]))
                            throw new InvalidOperationException("loot cleanup Item incarnation changed");
                        if (Frame.WorldAttempts[Index] != null && !EntityLifetimes.IsAttemptEpochUnchanged(Frame.WorldAttempts[Index]))
                            throw new InvalidOperationException("loot cleanup world incarnation changed during Kill");
                        if (Value.IsRemoved()) continue;
                        if (Value.parent != null || Value.GetWorldEntity() != null) throw new InvalidOperationException("loot owner changed");
                        Value.Remove(0);
                        if (!Value.IsRemoved() || Value.amount != 0) throw new InvalidOperationException("loot removal unverified");
                    } catch (Exception) { LootCount(ref LootCleanupFailed); }
                }
                LootFixtureCompleted(Frame, Success);
            } finally {
                Array.Clear(Frame.Items, 0, Frame.Items.Length); Array.Clear(Frame.Worlds, 0, Frame.Worlds.Length);
                Array.Clear(Frame.WorldWitnesses, 0, Frame.WorldWitnesses.Length); Array.Clear(Frame.WorldPrefabs, 0, Frame.WorldPrefabs.Length);
                Array.Clear(Frame.WorldAttempts, 0, Frame.WorldAttempts.Length);
                Array.Clear(Frame.Definitions, 0, Frame.Definitions.Length);
                Frame.Source = null; Frame.Container = null; Frame.Domain = null; Frame.Rule = null; Frame.Binding = null; Frame.Removal = null; Frame.Owner = null;
                if (ReferenceEquals(LootCurrent, Frame)) LootCurrent = null;
            }
        }

        // Erased from production packages; owned live fault/receipt seams only.
        partial void LootFixtureWorldCaptured(BaseEntity World, Item Value);
        partial void LootFixtureCompleted(LootPayoutFrame Frame, bool Success);

        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class LootDeathPatch
        {
            private static bool Prepare() { return LootMethodPinned(LootDeathTarget, 0x06005773, 83, LootDeathHash); }
            private static MethodBase TargetMethod() { return LootDeathTarget; }
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(LootContainer __instance, out LootPayoutFrame __state)
            { __state = null; var Owner = ActiveEntityObserver; if (Owner == null) return; try { __state = Owner.EnterLoot(__instance); } catch (Exception) { LootCount(ref Owner.LootRejected); } }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, LootPayoutFrame __state)
            { if (__state != null) { __state.Postfix = true; __state.Original = __runOriginal; } }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, LootPayoutFrame __state)
            { if (__state != null && __state.Owner != null) __state.Owner.ExitLoot(__state, __exception); }
        }
        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class LootPayoutPatch
        {
            private static bool Prepare() { return LootMethodPinned(LootPayoutTarget, 0x06003701, 160, "14dbfbcb12139d86b2fd759c98b73020282e2b1460f1244769c782cffeddc0d0"); }
            private static MethodBase TargetMethod() { return LootPayoutTarget; }
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static bool Prefix(IItemContainerEntity __0)
            {
                var Owner = ActiveEntityObserver;
                if (Owner == null || Thread.CurrentThread.ManagedThreadId != Owner.LootThread) return true;
                try { return Owner.ReplaceLoot(__0); }
                catch (Exception) {
                    var Frame = Owner.LootCurrent;
                    if (Frame != null && Frame.Committed) { Frame.Poisoned = true; return false; }
                    LootCount(ref Owner.LootRejected); return true;
                }
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)] private static void Postfix() { }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last)] private static void Finalizer() { }
        }
        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class LootWorldPatch
        {
            private static bool Prepare() { return LootMethodPinned(LootWorldTarget, 0x06006a37, 121, "cd31acc55c0044763b9757597bcbe8d12fe5463254c743975466bdcb3bad2c08"); }
            private static MethodBase TargetMethod() { return LootWorldTarget; }
            [HarmonyTranspiler, HarmonyPriority(Priority.Last)]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions)
            {
                var Source = new List<CodeInstruction>(Instructions); int Location = -1;
                for (int Index = 0; Index + 1 < Source.Count; Index++) {
                    MethodInfo Method = Source[Index].operand as MethodInfo;
                    if (Source[Index].opcode == OpCodes.Callvirt && Method != null && Method.DeclaringType == typeof(GameManager) && Method.Name == "CreateEntity" &&
                        Source[Index + 1].opcode == OpCodes.Stloc_0) {
                        if (Location != -1) return Source; Location = Index + 1;
                    }
                }
                if (Location < 0 || !LootMethodPinned(LootWorldTarget, 0x06006a37, 121, "cd31acc55c0044763b9757597bcbe8d12fe5463254c743975466bdcb3bad2c08")) return Source;
                MethodInfo Capture = typeof(CarbonLuau).GetMethod("CaptureLootWorld", BindingFlags.Static | BindingFlags.NonPublic);
                Source.InsertRange(Location + 1, new[] { new CodeInstruction(OpCodes.Ldloc_0), new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, Capture) });
                LootWorldMarker = true;
                return Source;
            }
        }
    }
}
