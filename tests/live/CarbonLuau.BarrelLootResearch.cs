// TEST ONLY. Include alone with the parent's erased RunBarrelLootResearch seam.
// Fresh disposable pinned server, before any external clients; never a D2 verdict.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string BarrelResearchMarker = "[CarbonLuau:BarrelLootResearch] ";
        private const string BarrelResearchPrefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab";
        private string BarrelResearchSelectedPrefab;
        private const int BarrelResearchLimit = 64, BarrelResearchItemLimit = 16;
        private static readonly Vector3 BarrelResearchPosition = new Vector3(37, 150, 41);
        private static CarbonLuau BarrelResearchOwner;
        private readonly BarrelResearchResource[] BarrelResearchLedger = new BarrelResearchResource[BarrelResearchLimit];
        private readonly BarrelResearchWorld[] BarrelResearchTrace = new BarrelResearchWorld[128];
        private readonly MethodInfo[] BarrelResearchMethods = new MethodInfo[16];
        private readonly BarrelResearchCase[] BarrelResearchCases = new BarrelResearchCase[2];
        private int BarrelResearchLedgerCount, BarrelResearchTraceCount, BarrelResearchMethodCount, BarrelResearchThread;
        private int BarrelResearchDropDepth, BarrelResearchBonusDepth, BarrelResearchOffThread;
        private Harmony BarrelResearchHarmony;
        private BarrelResearchCase BarrelResearchCurrent;
        private BasePlayer BarrelResearchActor;
        private string BarrelResearchPatchFailure;
        private bool BarrelResearchVeto, BarrelResearchCleaning;

        private enum BarrelResearchMode { Vanilla, Replace, Zero }
        private enum BarrelResearchFault { None, CreateNull, CreateThrow, DropNull, DropThrow }
        private struct BarrelResearchResource
        {
            internal Item Item;
            internal ulong ItemUid;
            internal BaseEntity Entity;
            internal ulong Id;
            internal EntityLifetimeModel.MembershipCandidate Witness;
            internal bool OwnershipUnknown;
        }
        private struct BarrelResearchWorld
        {
            internal BarrelResearchCase Source;
            internal Item Item;
            internal BaseEntity Entity;
            internal ItemContainer ParentAtCreate;
            internal bool Bonus;
            internal int Amount;
            internal string Name;
        }
        private sealed class BarrelResearchCase
        {
            internal string Label;
            internal string Prefab;
            internal LootContainer Source;
            internal ItemContainer Container;
            internal string ContainerUid;
            internal EntityFacadeBinding Binding;
            internal ulong Id;
            internal Vector3 Position;
            internal BarrelResearchMode Mode;
            internal BarrelResearchFault Fault;
            internal readonly Item[] Stored = new Item[BarrelResearchItemLimit];
            internal readonly ulong[] StoredUids = new ulong[BarrelResearchItemLimit];
            internal readonly int[] StoredAmounts = new int[BarrelResearchItemLimit];
            internal readonly Item[] Replacement = new Item[2];
            internal int StoredCount, DropCalls, OriginalCalls, BonusCalls, KillHooks, ReplacementCalls, Injected;
            internal bool ExactBonusActor;
        }

        // Demand the existing qualified Carbon Spawn hook pair, as other private
        // Entity research packages do. Do not pack this alongside other hook fixtures.
        private void OnEntitySpawn(BaseNetworkable Entity) { }
        private void OnEntitySpawned(BaseNetworkable Entity) { }
        private object OnEntityKill(BaseNetworkable Entity)
        {
            object BoundaryVeto = null;
            ObserveEntityDeathBoundaryKill(Entity, ref BoundaryVeto);
            if (BoundaryVeto != null) return BoundaryVeto;
            var Current = BarrelResearchCurrent;
            if (Current == null || !ReferenceEquals(Entity, Current.Source) || BarrelResearchCleaning) return null;
            if (!BarrelResearchOnOwner()) return null;
            Current.KillHooks++;
            return BarrelResearchVeto ? (object)true : null;
        }
        partial void ObserveEntityDeathBoundaryKill(BaseNetworkable Entity, ref object Veto);

        partial void RunBarrelLootResearch()
        {
            bool Qualification = false;
            SelectLootQualification(ref Qualification);
            if (Qualification) RunLootQualification();
            else NextTick(BarrelResearchRun);
        }
        partial void SelectLootQualification(ref bool Qualification);
        partial void RunLootQualification();
        private static void BarrelResearchCheck(bool Condition, string Message)
        { if (!Condition) throw new InvalidOperationException(Message); }

        private bool BarrelResearchOnOwner()
        {
            if (Thread.CurrentThread.ManagedThreadId != BarrelResearchThread) {
                Interlocked.Increment(ref BarrelResearchOffThread); return false;
            }
            if (Host == null || Host.Busy) { BarrelResearchPatchFailure = "host callback during VM entry"; return false; }
            return true;
        }
        private void BarrelResearchGuard()
        {
            BarrelResearchCheck(BarrelResearchOnOwner(), "owner thread / no VM entry");
            BarrelResearchCheck(BarrelResearchOffThread == 0 && BarrelResearchPatchFailure == null,
                "trace callback failure: " + BarrelResearchPatchFailure);
            BarrelResearchCheck(BasePlayer.activePlayerList.Count == 0 ||
                (BasePlayer.activePlayerList.Count == 1 && ReferenceEquals(BasePlayer.activePlayerList[0], BarrelResearchActor)),
                "external player joined fixture");
        }

        private void BarrelResearchHold(Item Value, bool Acquired = false)
        {
            // Zero UID is the pooled terminal record, not a new acquisition.
            // Keep any prior ledger UID intact and never remove it again.
            if (Value == null || Value.uid.Value == 0) return;
            for (int Index = 0; Index < BarrelResearchLedgerCount; Index++)
                if (ReferenceEquals(BarrelResearchLedger[Index].Item, Value)) {
                    if (BarrelResearchLedger[Index].ItemUid != Value.uid.Value) {
                        // Only a genuine new acquisition (factory return or
                        // exact cold inventory) may replace a pooled record.
                        // Repeated observations never refresh stale UID authority.
                        BarrelResearchCheck(Acquired && Value.uid.Value != 0 && !BarrelResearchLedger[Index].OwnershipUnknown,
                            "held Item UID changed without explicit acquisition");
                        BarrelResearchLedger[Index].ItemUid = Value.uid.Value;
                    }
                    return;
                }
            BarrelResearchCheck(BarrelResearchLedgerCount < BarrelResearchLimit, "Item ledger cap");
            BarrelResearchLedger[BarrelResearchLedgerCount++] = new BarrelResearchResource { Item = Value, ItemUid = Value.uid.Value };
        }
        private void BarrelResearchHold(BaseEntity Value)
        {
            if (ReferenceEquals(Value, null)) return;
            for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                if (!ReferenceEquals(BarrelResearchLedger[Index].Entity, Value)) continue;
                var Witness = BarrelResearchLedger[Index].Witness;
                BarrelResearchCheck(Witness == null || EntityLifetimes.IsWitnessEpochUnchanged(Witness), "held entity must not acquire a new epoch");
                if (Value.net != null) {
                    BarrelResearchCheck(BarrelResearchLedger[Index].Id == 0 || BarrelResearchLedger[Index].Id == Value.net.ID.Value,
                        "held entity must not acquire a new network ID");
                    BarrelResearchLedger[Index].Id = Value.net.ID.Value;
                }
                if (Witness == null) BarrelResearchLedger[Index].Witness = EntityLifetimes.CaptureCompletedWitness(Value);
                return;
            }
            BarrelResearchCheck(BarrelResearchLedgerCount < BarrelResearchLimit, "world ledger cap");
            BarrelResearchLedger[BarrelResearchLedgerCount++] = new BarrelResearchResource {
                Entity = Value, Id = Value.net == null ? 0 : Value.net.ID.Value,
                Witness = EntityLifetimes.CaptureCompletedWitness(Value)
            };
        }
        private void BarrelResearchOwnership(Item Value, bool Unknown)
        {
            for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                if (!ReferenceEquals(BarrelResearchLedger[Index].Item, Value)) continue;
                BarrelResearchLedger[Index].OwnershipUnknown = Unknown; return;
            }
            throw new InvalidOperationException("Item missing from fixed ledger");
        }
        private void BarrelResearchInventory(ItemContainer Container, BarrelResearchCase Case, bool Remember)
        {
            BarrelResearchCheck(Container != null && Container.itemList.Count <= BarrelResearchItemLimit,
                "bounded direct fixture inventory");
            for (int Index = 0; Index < Container.itemList.Count; Index++) {
                Item Value = Container.itemList[Index];
                BarrelResearchCheck(Value != null && ReferenceEquals(Value.parent, Container) &&
                    ReferenceEquals(Value.GetWorldEntity(), null), "physical stored Item owner");
                BarrelResearchHold(Value, !Remember);
                if (Remember) {
                    int Stored = Case.StoredCount++;
                    Case.Stored[Stored] = Value; Case.StoredUids[Stored] = Value.uid.Value; Case.StoredAmounts[Stored] = Value.amount;
                }
            }
        }

        private BarrelResearchCase BarrelResearchCreate(string Label, BarrelResearchMode Mode, Vector3 Position, bool Seed, int Slot = 0, string Prefab = null, bool QualifiedProfile = true)
        {
            BarrelResearchGuard();
            BarrelResearchCheck(BarrelResearchCases[Slot] == null && BarrelResearchLedgerCount <= 32, "fixture case reservation");
            Prefab = Prefab ?? BarrelResearchSelectedPrefab ?? BarrelResearchPrefab;
            BaseEntity Entity = GameManager.server.CreateEntity(Prefab, Position, Quaternion.identity, true);
            BarrelResearchHold(Entity); // Also clean a returned wrong-type/unspawned entity.
            var Case = new BarrelResearchCase { Label = Label, Prefab = Prefab, Source = Entity as LootContainer, Mode = Mode, Position = Position };
            BarrelResearchCases[Slot] = Case;
            BarrelResearchCheck(Case.Source != null, "exact prefab must be LootContainer");
            BarrelResearchCurrent = Case;
            Entity.enableSaving = false;
            BarrelResearchColdPrefab(Case.Source);
            Entity.Spawn();
            BarrelResearchHold(Entity);
            BarrelResearchCheck(Entity.GetType() == typeof(LootContainer) && Entity.PrefabName == Case.Prefab,
                "exact concrete class / canonical prefab");
            // An actual unsupported plain prefab may be used only as a vanilla
            // negative source, never as a qualified replacement profile.
            BarrelResearchCheck(QualifiedProfile || Mode == BarrelResearchMode.Vanilla, "unsupported fixture source is vanilla-only");
            if (QualifiedProfile) ObserveBarrelFixtureProfile(Case.Source);
            Case.Container = Case.Source.inventory;
            Case.ContainerUid = Case.Container == null ? null : Case.Container.uid.ToString();
            BarrelResearchInventory(Case.Container, Case, false);
            if (Seed) {
                Case.Container.Clear(); ItemManager.DoRemoves();
                BarrelResearchCheck(Case.Container.itemList.Count == 0, "seed clear completed");
                BarrelResearchSeed(Case, "stones", 13, 0);
                BarrelResearchSeed(Case, "scrap", 2, 1);
            }
            BarrelResearchInventory(Case.Container, Case, true);
            BarrelResearchCheck(TryAdmitEntity(Case.Source, Gameplay.Active, out Case.Binding), "original D20 admission");
            Case.Id = Entity.net.ID.Value;
            EntityPositionComposition.Position Actual = default(EntityPositionComposition.Position);
            BarrelResearchCheck(EntityPositionReader != null && EntityPositionReader.TryObserve(Entity, out Actual),
                "bounded no-wait source position");
            Puts(BarrelResearchMarker + "ARM case=" + Label + " prefab=" + Entity.PrefabName + " prefab_id=" + Entity.prefabID +
                " type=" + Entity.GetType().FullName + " id=" + Case.Id + " token=" + Case.Binding.Lifetime.Record.Token +
                " epoch=" + Case.Binding.Lifetime.Record.Epoch + " position=" + Actual.X + "," + Actual.Y + "," + Actual.Z +
                " stored=" + Case.StoredCount + " mode=" + Mode);
            return Case;
        }
        partial void ObserveBarrelFixtureProfile(LootContainer Source);
        private void BarrelResearchSeed(BarrelResearchCase Case, string Name, int Amount, int Slot)
        {
            Item Value = BarrelResearchNewItem(Name, Amount);
            BarrelResearchOwnership(Value, true);
            bool Moved = Value.MoveToContainer(Case.Container, Slot, false, false, null, false);
            BarrelResearchOwnership(Value, false);
            BarrelResearchCheck(Moved &&
                ReferenceEquals(Value.parent, Case.Container) && ReferenceEquals(Case.Container.GetSlot(Slot), Value),
                "deterministic seed " + Name);
        }
        private Item BarrelResearchNewItem(string Name, int Amount)
        {
            BarrelResearchCheck(BarrelResearchLedgerCount <= BarrelResearchLimit - 2, "reserve Item and world");
            ItemDefinition Definition = ItemManager.FindItemDefinition(Name);
            BarrelResearchCheck(Definition != null, "definition " + Name);
            Item Value = ItemManager.Create(Definition, Amount, 0);
            BarrelResearchHold(Value, true);
            BaseEntity ObservedWorld = Value == null ? null : Value.GetWorldEntity();
            Puts(BarrelResearchMarker + "NEW_ITEM name=" + Name + " wanted=" + Amount +
                " returned=" + (Value != null) + " valid=" + (Value != null && Value.IsValid()) +
                " amount=" + (Value == null ? -1 : Value.amount) + " uid=" + (Value == null ? "nil" : Value.uid.ToString()) +
                " parent=" + (Value != null && Value.parent != null) + " world_ref=" + (!ReferenceEquals(ObservedWorld, null)) +
                " world_unity_live=" + (ObservedWorld != null));
            BarrelResearchCheck(Value != null && Value.IsValid() && Value.amount == Amount &&
                Value.parent == null && ReferenceEquals(Value.GetWorldEntity(), null), "new unowned Item " + Name);
            return Value;
        }

        private static bool BarrelResearchDropPrefix(IItemContainerEntity __0, BaseEntity __1, out bool __state)
        {
            __state = false;
            var Owner = BarrelResearchOwner;
            if (Owner == null || Owner.BarrelResearchCleaning) return true;
            // No proximity, prefab search, ID reacquisition or cross-barrel matching.
            var Case = Owner.BarrelResearchCurrent;
            if (Case == null || !ReferenceEquals(__0, Case.Source)) return true;
            if (!Owner.BarrelResearchOnOwner()) return true;
            __state = true; Owner.BarrelResearchDropDepth++; Case.DropCalls++;
            try {
                Owner.BarrelResearchCheckSource(Case);
                Owner.Puts(BarrelResearchMarker + "STATIC_DROP case=" + Case.Label + " initiator_exact=" +
                    ReferenceEquals(__1, Owner.BarrelResearchActor) + " depth=" + Owner.BarrelResearchDropDepth);
                if (Case.Mode == BarrelResearchMode.Vanilla) return true;
                BarrelResearchCheck(Case.DropCalls == 1, "duplicate replacement fence");
                Case.ReplacementCalls++;
                if (Case.Mode == BarrelResearchMode.Replace) Owner.BarrelResearchReplace(Case);
            }
            catch (Exception Error) { Owner.BarrelResearchPatchFailure = Error.GetType().Name + ": " + Error.Message; }
            return false; // An entered experimental replacement never falls back/replays vanilla.
        }
        private static void BarrelResearchDropPostfix(bool __state, bool __runOriginal)
        {
            var Owner = BarrelResearchOwner;
            if (Owner == null || !__state || !Owner.BarrelResearchOnOwner()) return;
            if (__runOriginal) Owner.BarrelResearchCurrent.OriginalCalls++;
        }
        private static void BarrelResearchDropFinalizer(bool __state, Exception __exception)
        {
            var Owner = BarrelResearchOwner;
            if (Owner == null || !__state || !Owner.BarrelResearchOnOwner()) return;
            Owner.BarrelResearchDropDepth--;
            if (__exception != null) {
                Owner.BarrelResearchPatchFailure = "actual static DropItems exception; returned Item ownership UNKNOWN";
                var Case = Owner.BarrelResearchCurrent;
                // Conservative even if some earlier drops were returned. No
                // null world getter certifies an original throwing Drop's owner.
                for (int Index = 0; Index < Case.StoredCount; Index++) Owner.BarrelResearchOwnership(Case.Stored[Index], true);
            }
        }
        private static void BarrelResearchBonusPrefix(LootContainer __instance, BaseEntity __0, ItemContainer __1, out bool __state)
        {
            __state = false;
            var Owner = BarrelResearchOwner;
            if (Owner == null || Owner.BarrelResearchCleaning) return;
            var Case = Owner.BarrelResearchCurrent;
            if (Case == null || !ReferenceEquals(__instance, Case.Source) || !Owner.BarrelResearchOnOwner()) return;
            __state = true; Owner.BarrelResearchBonusDepth++; Case.BonusCalls++;
            Case.ExactBonusActor = ReferenceEquals(__0, Owner.BarrelResearchActor);
            // Individual world drops may supply null instead of a receiving container.
            // A nonnull container must be the exact original fixture inventory.
            if (__1 != null && !ReferenceEquals(__1, Case.Container)) Owner.BarrelResearchPatchFailure = "bonus container mismatch";
            try {
                Owner.Puts(BarrelResearchMarker + "BONUS_PREFIX case=" + Case.Label + " initiator_exact=" + Case.ExactBonusActor +
                    " container_null=" + (__1 == null) + " container_original=" + ReferenceEquals(__1, Case.Container));
            }
            catch (Exception Error) { Owner.BarrelResearchPatchFailure = Error.GetType().Name + ": " + Error.Message; }
        }
        private static void BarrelResearchBonusFinalizer(bool __state)
        {
            var Owner = BarrelResearchOwner;
            if (Owner != null && __state && Owner.BarrelResearchOnOwner()) Owner.BarrelResearchBonusDepth--;
        }
        private static void BarrelResearchWorldPrefix(Item __instance)
        {
            var Owner = BarrelResearchOwner;
            if (Owner == null || Owner.BarrelResearchCleaning || Owner.BarrelResearchCurrent == null || !Owner.BarrelResearchOnOwner()) return;
            if (!Owner.BarrelResearchKnown(Owner.BarrelResearchCurrent, __instance) &&
                Owner.BarrelResearchDropDepth == 0 && Owner.BarrelResearchBonusDepth == 0) return;
            try {
                Owner.BarrelResearchHold(__instance, !Owner.BarrelResearchKnown(Owner.BarrelResearchCurrent, __instance));
                // Covers vanilla and bonus as well as our own returned Items:
                // InitializeItem -> throwing Spawn can precede SetWorldEntity.
                Owner.BarrelResearchOwnership(__instance, true);
            }
            catch (Exception Error) { Owner.BarrelResearchPatchFailure = Error.GetType().Name + ": " + Error.Message; }
        }
        private static void BarrelResearchWorldPostfix(Item __instance, object __result)
        {
            var Owner = BarrelResearchOwner;
            if (Owner == null || Owner.BarrelResearchCleaning || Owner.BarrelResearchCurrent == null) return;
            if (!Owner.BarrelResearchOnOwner()) return;
            var Case = Owner.BarrelResearchCurrent;
            bool Known = Owner.BarrelResearchKnown(Case, __instance);
            if (!Known && Owner.BarrelResearchDropDepth == 0 && Owner.BarrelResearchBonusDepth == 0) return;
            try {
                Owner.BarrelResearchHold(__instance);
                var World = __result as BaseEntity;
                // The returned world is held before any physical assertions.
                Owner.BarrelResearchHold(World);
                BarrelResearchCheck(Owner.BarrelResearchTraceCount < Owner.BarrelResearchTrace.Length, "world trace cap");
                Owner.BarrelResearchTrace[Owner.BarrelResearchTraceCount++] = new BarrelResearchWorld {
                    Source = Case, Item = __instance, Entity = World, ParentAtCreate = __instance.parent,
                    Bonus = Owner.BarrelResearchBonusDepth > 0, Amount = __instance.amount,
                    Name = __instance.info == null ? "<null>" : __instance.info.shortname
                };
                WorldItem Physical = World as WorldItem;
                if (Physical != null && ReferenceEquals(Physical.item, __instance) && ReferenceEquals(__instance.GetWorldEntity(), World))
                    Owner.BarrelResearchOwnership(__instance, false);
                Owner.Puts(BarrelResearchMarker + "WORLD_CREATE case=" + Case.Label + " known_item=" + Known +
                    " bonus_scope=" + (Owner.BarrelResearchBonusDepth > 0) + " uid=" + __instance.uid +
                    " amount=" + __instance.amount + " parent_original=" + ReferenceEquals(__instance.parent, Case.Container) +
                    " reciprocal=" + (Physical != null && ReferenceEquals(Physical.item, __instance) && ReferenceEquals(__instance.GetWorldEntity(), World)) +
                    " fully_spawned=" + (World != null && World.IsFullySpawned()) +
                    " registry_exact=" + (World != null && World.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World)));
            }
            catch (Exception Error) { Owner.BarrelResearchPatchFailure = Error.GetType().Name + ": " + Error.Message; }
        }
        private bool BarrelResearchKnown(BarrelResearchCase Case, Item Value)
        {
            for (int Index = 0; Index < Case.StoredCount; Index++) if (ReferenceEquals(Case.Stored[Index], Value)) return true;
            return ReferenceEquals(Case.Replacement[0], Value) || ReferenceEquals(Case.Replacement[1], Value);
        }
        private void BarrelResearchCheckSource(BarrelResearchCase Case)
        {
            BarrelResearchCheck(Case.Source != null && Case.Source.PrefabName == Case.Prefab &&
                ReferenceEquals(Case.Source.inventory, Case.Container) && Case.Source.net != null && Case.Source.net.ID.Value == Case.Id &&
                ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Case.Id)), Case.Source), "original payout source");
        }
        private void BarrelResearchReplace(BarrelResearchCase Case)
        {
            for (int Index = 0; Index < 2; Index++) {
                // Fail only at our own second-entry call seams. No global Create/Drop patch.
                if (Index == 1 && (Case.Fault == BarrelResearchFault.CreateNull || Case.Fault == BarrelResearchFault.CreateThrow)) {
                    Case.Injected++;
                    if (Case.Fault == BarrelResearchFault.CreateThrow) {
                        try { throw new InvalidOperationException("fixture Create seam"); }
                        catch (InvalidOperationException) { return; }
                    }
                    return; // Simulated null return, no second host Create entered.
                }
                Item Value = BarrelResearchNewItem(Index == 0 ? "scrap" : "metal.fragments", Index == 0 ? 7 : 11);
                Case.Replacement[Index] = Value;
                if (Index == 1 && (Case.Fault == BarrelResearchFault.DropNull || Case.Fault == BarrelResearchFault.DropThrow)) {
                    Case.Injected++;
                    if (Case.Fault == BarrelResearchFault.DropThrow) {
                        try { throw new InvalidOperationException("fixture Drop seam"); }
                        catch (InvalidOperationException) { return; }
                    }
                    return; // Returned Item remains in the ledger; host Drop was not entered.
                }
                // CreateWorldObject initializes a WorldItem before Spawn and only
                // sets Item.worldEnt after Spawn returns. An actual host exception
                // can therefore leave an inaccessible wrapper owning this Item.
                // UNKNOWN must never be converted into "unowned" by a null getter.
                BarrelResearchOwnership(Value, true);
                BaseEntity World = Value.Drop(Case.Position, Vector3.zero);
                BarrelResearchHold(World);
                BarrelResearchCheck(World != null && ReferenceEquals(Value.GetWorldEntity(), World) &&
                    World is WorldItem && ReferenceEquals(((WorldItem)World).item, Value) && Value.parent == null,
                    "replacement Drop return physical owner");
                BarrelResearchOwnership(Value, false);
            }
        }

        private int BarrelResearchVerifyWorlds(BarrelResearchCase Case, bool RequireStored, bool RequireReplacement, out int BonusScrap)
        {
            BarrelResearchGuard();
            int Count = 0; BonusScrap = 0;
            for (int Index = 0; Index < BarrelResearchTraceCount; Index++) {
                var Trace = BarrelResearchTrace[Index];
                if (!ReferenceEquals(Trace.Source, Case)) continue;
                Count++;
                WorldItem World = Trace.Entity as WorldItem;
                BarrelResearchCheck(World != null && !World.IsDestroyed && World.IsFullySpawned() && World.net != null &&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                    Trace.Item.IsValid() && Trace.Item.parent == null && ReferenceEquals(Trace.Item.GetWorldEntity(), World) &&
                    ReferenceEquals(World.item, Trace.Item) && Trace.Item.amount == Trace.Amount,
                    "physical Item/world reciprocal identity " + Case.Label);
                EntityFacadeBinding Binding;
                BarrelResearchCheck(TryAdmitEntity(World, Gameplay.Active, out Binding), "world D20 admission " + Case.Label);
                EntityPositionComposition.Position Actual;
                BarrelResearchCheck(EntityPositionReader.TryObserve(World, out Actual), "bounded world position");
                Puts(BarrelResearchMarker + "PHYSICAL case=" + Case.Label + " name=" + Trace.Name + " amount=" + Trace.Amount +
                    " id=" + World.net.ID.Value + " token=" + Binding.Lifetime.Record.Token + " epoch=" + Binding.Lifetime.Record.Epoch +
                    " parent_at_create_original=" + ReferenceEquals(Trace.ParentAtCreate, Case.Container) +
                    " position=" + Actual.X + "," + Actual.Y + "," + Actual.Z);
                if (Trace.Bonus && Trace.Name == "scrap") BonusScrap = checked(BonusScrap + Trace.Amount);
                if (RequireReplacement) BarrelResearchCheck(ReferenceEquals(Trace.Item, Case.Replacement[0]) ||
                    ReferenceEquals(Trace.Item, Case.Replacement[1]), "extra default payout in replacement");
                if (RequireReplacement) {
                    bool First = ReferenceEquals(Trace.Item, Case.Replacement[0]);
                    BarrelResearchCheck(Trace.Name == (First ? "scrap" : "metal.fragments") && Trace.Amount == (First ? 7 : 11) &&
                        Actual.X == Case.Position.x && Actual.Y == Case.Position.y && Actual.Z == Case.Position.z,
                        "replacement exact quantities and known nonzero origin");
                }
            }
            if (RequireStored) for (int Index = 0; Index < Case.StoredCount; Index++) {
                int Matches = 0;
                for (int WorldIndex = 0; WorldIndex < BarrelResearchTraceCount; WorldIndex++)
                    if (ReferenceEquals(BarrelResearchTrace[WorldIndex].Source, Case) &&
                        ReferenceEquals(BarrelResearchTrace[WorldIndex].Item, Case.Stored[Index])) Matches++;
                BarrelResearchCheck(Matches == 1, "each original stored Item drops once");
            }
            if (RequireReplacement) {
                int Expected = Case.Mode == BarrelResearchMode.Zero ? 0 : Case.Fault == BarrelResearchFault.None ? 2 : 1;
                BarrelResearchCheck(Count == Expected && Case.BonusCalls == 0 && Case.ReplacementCalls == 1 && Case.OriginalCalls == 0,
                    "replacement cardinality / original suppressed / no escaped bonus");
                for (int Index = 0; Index < Case.StoredCount; Index++)
                    BarrelResearchCheck(ReferenceEquals(Case.Stored[Index].GetWorldEntity(), null), "original contents never world-owned");
            }
            return Count;
        }
        private void BarrelResearchRetired(BarrelResearchCase Case)
        {
            BarrelResearchCheck(!ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Case.Id)), Case.Source) &&
                !ValidateEntity(Case.Binding), "original world removal and D20 retirement " + Case.Label);
        }
        private int BarrelResearchDeath(BarrelResearchCase Case, bool Vanilla)
        {
            BarrelResearchCurrent = Case;
            BarrelResearchCheck(ValidateEntity(Case.Binding), "live original before damage");
            Case.Source.Hurt(10000f, Rust.DamageType.Generic, BarrelResearchActor, false);
            BarrelResearchGuard();
            BarrelResearchCheck(BarrelResearchDropDepth == 0 && BarrelResearchBonusDepth == 0 && Case.DropCalls == 1,
                "one completed static payout frame");
            BarrelResearchRetired(Case);
            int Bonus;
            int Count = BarrelResearchVerifyWorlds(Case, Vanilla, !Vanilla, out Bonus);
            if (Vanilla) BarrelResearchCheck(Case.OriginalCalls == 1 && Case.ReplacementCalls == 0 && Count >= Case.StoredCount,
                "vanilla original ran and physical contents paid");
            if (Case.Label != "vanilla") {
                if (Vanilla) {
                    BarrelResearchCheck(Case.StoredCount == 2 && Case.Stored[0].info.shortname == "stones" && Case.Stored[0].amount == 13 &&
                        Case.Stored[1].info.shortname == "scrap" && Case.Stored[1].amount == 2, "deterministic contents unchanged");
                }
            }
            Puts(BarrelResearchMarker + "MEASURED case=" + Case.Label + " worlds=" + Count + " bonus_calls=" + Case.BonusCalls +
                " bonus_scrap=" + Bonus + " injected=" + Case.Injected);
            return Bonus;
        }

        private bool BarrelResearchBoost()
        {
            // Resolve only the actual modifiers member and List<ModifierDefintion>
            // Add overload; never fake an increased payout or patch the modifier getter.
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var Member = typeof(BasePlayer).GetField("modifiers", Flags);
            object Modifiers = Member == null ? null : Member.GetValue(BarrelResearchActor);
            if (Modifiers == null) { Puts(BarrelResearchMarker + "UNMEASURED boosted_bonus modifiers member unavailable"); return false; }
            MemberInfo[] Adds = Modifiers.GetType().GetMember("Add", MemberTypes.Method, Flags);
            BarrelResearchCheck(Adds.Length <= 8, "modifier Add overload cap");
            MethodInfo Add = null; Type Definition = null;
            foreach (MethodInfo Candidate in Adds) {
                ParameterInfo[] Parameters = Candidate.GetParameters();
                if (Parameters.Length != 3 || Parameters[1].ParameterType != typeof(float) || Parameters[2].ParameterType != typeof(float) ||
                    !Parameters[0].ParameterType.IsGenericType ||
                    Parameters[0].ParameterType.GetGenericTypeDefinition() != typeof(List<>)) continue;
                Type Element = Parameters[0].ParameterType.GetGenericArguments()[0];
                if (Element.Name != "ModifierDefintion") continue;
                BarrelResearchCheck(Add == null, "ambiguous modifier Add"); Add = Candidate; Definition = Element;
            }
            if (Add == null) { Puts(BarrelResearchMarker + "UNMEASURED boosted_bonus Add(List<ModifierDefintion>,float,float) unavailable"); return false; }
            FieldInfo Type = Definition.GetField("type", Flags), Value = Definition.GetField("value", Flags),
                Duration = Definition.GetField("duration", Flags), Source = Definition.GetField("source", Flags);
            if (Type == null || !Type.FieldType.IsEnum || Value == null || Value.FieldType != typeof(float) ||
                Duration == null || Duration.FieldType != typeof(float) || Source == null || !Enum.IsDefined(Type.FieldType, "Scrap_Yield")) {
                Puts(BarrelResearchMarker + "UNMEASURED boosted_bonus modifier field shape unsupported"); return false;
            }
            object Entry = Activator.CreateInstance(Definition);
            Type.SetValue(Entry, Enum.Parse(Type.FieldType, "Scrap_Yield"));
            Value.SetValue(Entry, 1f); Duration.SetValue(Entry, 60f);
            Source.SetValue(Entry, Source.FieldType.IsValueType ? Activator.CreateInstance(Source.FieldType) : null);
            IList Entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Definition), 1);
            Entries.Add(Entry);
            BarrelResearchLogMethod(Add);
            Add.Invoke(Modifiers, new object[] { Entries, 1f, 1f });
            Puts(BarrelResearchMarker + "MODIFIER actual_Add list_count=1 scale_args=1,1 type=Scrap_Yield value=1 duration=60 source=default synthetic_actor_no_connection");
            return true;
        }
        private void BarrelResearchCreateActor()
        {
            BaseEntity Entity = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", BarrelResearchPosition + new Vector3(0, 0, 4));
            BarrelResearchHold(Entity); BarrelResearchActor = Entity as BasePlayer;
            BarrelResearchCheck(BarrelResearchActor != null, "bonus fixture actor");
            BarrelResearchActor.enableSaving = false;
            BarrelResearchActor.userID = 76561198000000115;
            BarrelResearchActor.UserIDString = "76561198000000115";
            BarrelResearchActor.displayName = "Barrel research fixture";
            BarrelResearchActor.Spawn(); BarrelResearchHold(BarrelResearchActor);
            BarrelResearchActor.lifestate = BaseCombatEntity.LifeState.Alive;
            BarrelResearchCheck(BarrelResearchActor.net.connection == null, "fixture actor has no authenticated connection");
            BarrelResearchInventory(BarrelResearchActor.inventory.containerMain, null, false);
            BarrelResearchInventory(BarrelResearchActor.inventory.containerBelt, null, false);
            BarrelResearchInventory(BarrelResearchActor.inventory.containerWear, null, false);
        }

        private void BarrelResearchColdPrefab(LootContainer Source)
        {
            // Cold, exact fixture component only. This is evidence for the parent's
            // corpse/debris/reskin proof, not a claim those paths are excluded.
            Component[] Components = Source.GetComponents<Component>();
            BarrelResearchCheck(Components.Length <= 32, "prefab component evidence cap");
            for (int Index = 0; Index < Components.Length; Index++)
                Puts(BarrelResearchMarker + "COLD_COMPONENT index=" + Index + " type=" +
                    (Components[Index] == null ? "<missing>" : Components[Index].GetType().FullName));
            int Visited = 0, Depth = 0;
            Type Current = Source.GetType();
            while (Current != null && Current != typeof(MonoBehaviour) && Current != typeof(object)) {
                BarrelResearchCheck(++Depth <= 8, "cold class depth cap");
                FieldInfo[] Fields = Current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Visited = checked(Visited + Fields.Length);
                BarrelResearchCheck(Visited <= 512, "cold field evidence cap");
                foreach (FieldInfo Field in Fields) {
                    string Name = Field.Name;
                    bool Relevant = Name.IndexOf("corpse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("debris", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("reskin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name == "LootContainerType" || Name == "lootContainerType" || Name == "initialLootSpawn" ||
                        Name == "DropsLoot" || Name == "dropsLoot" || Name == "ShouldDropItemsIndividually" ||
                        Name == "dropItemsOnDestroy" || Name == "dropItemsIndividually" || Name == "scrapAmount" ||
                        Name == "deathStat" || Name == "lootDefinition";
                    if (!Relevant) continue;
                    object Value = Field.GetValue(Source);
                    string Detail = "<null>";
                    if (Value != null) {
                        Type Kind = Value.GetType();
                        Detail = Kind.FullName;
                        if (Kind.IsPrimitive || Kind.IsEnum || Kind == typeof(string)) Detail += ":" + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture);
                        else {
                            // Prefab handles are usually GameObjectRef. Inspect
                            // scalar backing fields only, never a resource-loading getter.
                            FieldInfo[] HandleFields = Kind.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            BarrelResearchCheck(HandleFields.Length <= 32, "cold prefab handle field cap");
                            foreach (FieldInfo HandleField in HandleFields) {
                                if (HandleField.Name != "guid" && HandleField.Name != "resourcePath" && HandleField.Name != "resourceID") continue;
                                if (!HandleField.FieldType.IsPrimitive && HandleField.FieldType != typeof(string)) continue;
                                string Text = Convert.ToString(HandleField.GetValue(Value), System.Globalization.CultureInfo.InvariantCulture);
                                BarrelResearchCheck(Text == null || Text.Length <= 512, "cold handle text cap");
                                Detail += " " + HandleField.Name + "=" + Text;
                            }
                        }
                    }
                    BarrelResearchCheck(Detail.Length <= 1024, "cold evidence text cap");
                    Puts(BarrelResearchMarker + "COLD_FIELD owner=" + Current.FullName + " name=" + Name + " value=" + Detail);
                }
                Current = Current.BaseType;
            }
            foreach (string Name in new[] { "DropsLoot", "ShouldDropItemsIndividually", "LootContainerType" }) {
                PropertyInfo Property = Source.GetType().GetProperty(Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (Property != null) Puts(BarrelResearchMarker + "COLD_PROPERTY name=" + Name + " type=" + Property.PropertyType.FullName +
                    " value=UNMEASURED_getter_not_invoked");
            }
            Puts(BarrelResearchMarker + "COLD_CONFIG inventory_capacity=" + (Source.inventory == null ? -1 : Source.inventory.capacity) +
                " fields_visited=" + Visited + " corpse_debris_item_path_exclusion=UNMEASURED_parent_exact_source_proof_required");
        }

        private static MethodInfo BarrelResearchMethod(Type Type, string Name)
        {
            MemberInfo[] Members = Type.GetMember(Name, MemberTypes.Method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            BarrelResearchCheck(Members.Length <= 8, "method overload cap " + Name);
            return Members.Length == 1 ? (MethodInfo)Members[0] : null;
        }
        private void BarrelResearchLogMethod(MethodInfo Method)
        {
            if (Method == null) { Puts(BarrelResearchMarker + "UNMEASURED loaded_method missing/overloaded"); return; }
            // Repeated exact-method evidence is rechecked below without retaining
            // duplicate cleanup identities in the unchanged sixteen-slot array.
            bool Retained = false;
            for (int Index = 0; Index < BarrelResearchMethodCount; Index++)
                if (Method.Equals(BarrelResearchMethods[Index])) { Retained = true; break; }
            if (!Retained) {
                BarrelResearchCheck(BarrelResearchMethodCount < BarrelResearchMethods.Length, "method evidence cap");
                BarrelResearchMethods[BarrelResearchMethodCount++] = Method;
            }
            MethodBody Body = Method.GetMethodBody();
            BarrelResearchCheck(Body != null && Body.ExceptionHandlingClauses.Count <= 16, "bounded managed body/EH");
            byte[] Bytes = Body.GetILAsByteArray();
            BarrelResearchCheck(Bytes.Length <= 16384, "raw IL evidence bound");
            using (var Hash = SHA256.Create())
                Puts(BarrelResearchMarker + "LIVE_BODY method=" + Method.DeclaringType.FullName + "." + Method +
                    " token=0x" + Method.MetadataToken.ToString("x8") + " mvid=" + Method.Module.ModuleVersionId +
                    " raw_il_bytes=" + Bytes.Length + " raw_il_sha256=" + BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant() +
                    " eh=" + Body.ExceptionHandlingClauses.Count + " raw_reflection_body_not_harmony_replacement=true" +
                    " exact_method_already_retained=" + Retained + " retained_unique_methods=" + BarrelResearchMethodCount + " method_cap=" + BarrelResearchMethods.Length);
            foreach (ExceptionHandlingClause Clause in Body.ExceptionHandlingClauses)
                Puts(BarrelResearchMarker + "EH flags=" + Clause.Flags + " try=" + Clause.TryOffset + ":" + Clause.TryLength +
                    " handler=" + Clause.HandlerOffset + ":" + Clause.HandlerLength +
                    (Clause.Flags == ExceptionHandlingClauseOptions.Filter ? " filter=" + Clause.FilterOffset : ""));
        }
        private void BarrelResearchPatch(MethodInfo Target, string Prefix, string Postfix, string Finalizer)
        {
            BarrelResearchCheck(Target != null, "required research target missing");
            Func<string, HarmonyMethod> Hook = Name => Name == null ? null : new HarmonyMethod(
                typeof(CarbonLuau).GetMethod(Name, BindingFlags.Static | BindingFlags.NonPublic)) { priority = Priority.Last };
            BarrelResearchHarmony.Patch(Target, prefix: Hook(Prefix), postfix: Hook(Postfix), finalizer: Hook(Finalizer));
        }
        private void BarrelResearchInstall()
        {
            MethodInfo Drop = typeof(StorageContainer).GetMethod("DropItems", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(IItemContainerEntity), typeof(BaseEntity) }, null);
            MethodInfo World = BarrelResearchMethod(typeof(Item), "CreateWorldObject");
            MethodInfo Bonus = typeof(LootContainer).GetMethod("DropBonusItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(BaseEntity), typeof(ItemContainer) }, null);
            BarrelResearchCheck(Drop != null && Drop.ReturnType == typeof(void) && World != null &&
                typeof(BaseEntity).IsAssignableFrom(World.ReturnType) && Bonus != null, "exact loaded target signatures");
            BarrelResearchLogMethod(Drop); BarrelResearchLogMethod(World); BarrelResearchLogMethod(Bonus);
            foreach (string Name in new[] { "SpawnLoot", "PopulateLoot", "GenerateScrap", "OnDied", "OnKilled" })
                BarrelResearchLogMethod(BarrelResearchMethod(typeof(LootContainer), Name));
            BarrelResearchLogMethod(BarrelResearchMethod(typeof(StorageContainer), "OnKilled"));
            BarrelResearchLogMethod(BarrelResearchMethod(typeof(Item), "Drop"));
            BarrelResearchLogMethod(BarrelResearchMethod(typeof(ItemContainer), "Clear"));
            BarrelResearchLogMethod(BarrelResearchMethod(typeof(BaseCombatEntity), "Die"));
            BarrelResearchLogMethod(BarrelResearchMethod(typeof(BaseNetworkable), "Kill"));
            BarrelResearchLogMethod(BarrelResearchMethod(typeof(BaseNetworkable), "TerminateOnServer"));
            BarrelResearchHarmony = new Harmony("CarbonLuau.Test.BarrelLootResearch." + Guid.NewGuid().ToString("N"));
            BarrelResearchOwner = this;
            BarrelResearchPatch(Drop, nameof(BarrelResearchDropPrefix), nameof(BarrelResearchDropPostfix), nameof(BarrelResearchDropFinalizer));
            BarrelResearchPatch(World, nameof(BarrelResearchWorldPrefix), nameof(BarrelResearchWorldPostfix), null);
            BarrelResearchPatch(Bonus, nameof(BarrelResearchBonusPrefix), null, nameof(BarrelResearchBonusFinalizer));
            BarrelResearchCheck(VerifyAllEntityPatches() && EntityDestroyedAvailable(), "D20 and B3 topology preserved after private patches");
        }

        private void BarrelResearchIsolated(Action Work)
        {
            try { Work(); }
            finally { BarrelResearchCleanup(); }
        }
        private void BarrelResearchRun()
        {
            BarrelResearchThread = Thread.CurrentThread.ManagedThreadId;
            bool Measured = false, Clean = false;
            try {
                BarrelResearchCheck(BarrelResearchOwner == null && Host != null && Host.Ready && !Host.Busy && Gameplay != null && Gameplay.Active != null &&
                    EntityStartupQualified && !EntityObserverBroken && BasePlayer.activePlayerList.Count == 0,
                    "fresh startup-qualified ready disposable zero-player server required");
                BarrelResearchInstall();
                BarrelResearchIsolated(() => BarrelResearchDeath(BarrelResearchCreate("vanilla", BarrelResearchMode.Vanilla, BarrelResearchPosition, false), true));
                BarrelResearchIsolated(() => BarrelResearchDeath(BarrelResearchCreate("stored", BarrelResearchMode.Vanilla, BarrelResearchPosition, true), true));
                BarrelResearchIsolated(() => {
                    BarrelResearchCreateActor();
                    var Baseline = BarrelResearchCreate("bonus_baseline", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    int Normal = BarrelResearchDeath(Baseline, true);
                    if (!BarrelResearchBoost()) return;
                    var Boosted = BarrelResearchCreate("bonus_boosted", BarrelResearchMode.Vanilla, BarrelResearchPosition + new Vector3(0.25f, 0, 0), true, 1);
                    int Increased = BarrelResearchDeath(Boosted, true);
                    BarrelResearchCheck(Normal == 0 && Increased == Boosted.Source.scrapAmount &&
                        Increased > Normal && Baseline.ExactBonusActor && Boosted.ExactBonusActor,
                        "actual Scrap_Yield boost must increase measured exact-actor bonus");
                    Puts(BarrelResearchMarker + "MEASURED boosted_bonus baseline=" + Normal + " boosted=" + Increased);
                });
                BarrelResearchIsolated(() => {
                    var Case = BarrelResearchCreate("explicit_kill", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    Case.Source.Kill(); BarrelResearchGuard(); BarrelResearchRetired(Case);
                    int Bonus;
                    BarrelResearchCheck(Case.DropCalls == 0 && Case.BonusCalls == 0 && BarrelResearchVerifyWorlds(Case, false, false, out Bonus) == 0,
                        "explicit Kill has no payout");
                });
                BarrelResearchIsolated(() => {
                    var Case = BarrelResearchCreate("vetoed_kill", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    BarrelResearchVeto = true;
                    try { Case.Source.Kill(); } finally { BarrelResearchVeto = false; }
                    BarrelResearchGuard();
                    BarrelResearchCheck(Case.KillHooks == 1 && !Case.Source.IsDestroyed && ValidateEntity(Case.Binding) &&
                        Case.DropCalls == 0 && Case.BonusCalls == 0 && BarrelResearchTraceCount == 0, "veto preserves exact live barrel/no payout");
                    for (int Index = 0; Index < Case.StoredCount; Index++)
                        BarrelResearchCheck(ReferenceEquals(Case.Stored[Index].parent, Case.Container), "veto preserves contents");
                    Case.Source.Kill(); BarrelResearchRetired(Case);
                    BarrelResearchCheck(Case.KillHooks == 2, "veto removed for successful normal Kill");
                });
                BarrelResearchIsolated(() => {
                    var Case = BarrelResearchCreate("death_kill_veto", BarrelResearchMode.Replace, BarrelResearchPosition, true);
                    BarrelResearchVeto = true;
                    try { Case.Source.Die(null); } finally { BarrelResearchVeto = false; }
                    BarrelResearchGuard();
                    int Bonus;
                    BarrelResearchCheck(Case.DropCalls == 1 && Case.KillHooks == 1 &&
                        BarrelResearchVerifyWorlds(Case, false, true, out Bonus) == 2,
                        "death before vetoed Kill paid exact custom Items");
                    BarrelResearchCheck(!Case.Source.IsDestroyed && Case.Source.net != null && Case.Source.net.ID.Value == Case.Id &&
                        ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Case.Id)), Case.Source) &&
                        ValidateEntity(Case.Binding), "vetoed death source stays keyed and D20 lifetime not retired");
                    Puts(BarrelResearchMarker + "PARTIAL case=death_kill_veto physical_custom_payout_verified=true source_keyed=true source_retired=false no_payout_retry");
                    Case.Source.Kill(); BarrelResearchRetired(Case);
                    BarrelResearchCheck(Case.KillHooks == 2 && Case.DropCalls == 1, "release death veto retires without repeated payout");
                });
                BarrelResearchIsolated(() => BarrelResearchDeath(BarrelResearchCreate("zero", BarrelResearchMode.Zero, BarrelResearchPosition, true), false));
                BarrelResearchIsolated(() => {
                    var First = BarrelResearchCreate("nearby_replace", BarrelResearchMode.Replace, BarrelResearchPosition, true);
                    var Second = BarrelResearchCreate("nearby_vanilla", BarrelResearchMode.Vanilla, BarrelResearchPosition + new Vector3(0.25f, 0, 0), true, 1);
                    BarrelResearchDeath(First, false);
                    BarrelResearchCheck(ValidateEntity(Second.Binding) && Second.DropCalls == 0, "neighbor remains original/live");
                    for (int Index = 0; Index < Second.StoredCount; Index++)
                        BarrelResearchCheck(ReferenceEquals(Second.Stored[Index].parent, Second.Container), "neighbor Item owner preserved");
                    BarrelResearchDeath(Second, true);
                    int Bonus; BarrelResearchVerifyWorlds(First, false, true, out Bonus);
                });
                foreach (BarrelResearchFault Fault in new[] { BarrelResearchFault.CreateNull, BarrelResearchFault.CreateThrow, BarrelResearchFault.DropNull, BarrelResearchFault.DropThrow }) {
                    BarrelResearchIsolated(() => {
                        var Case = BarrelResearchCreate("seam_" + Fault, BarrelResearchMode.Replace, BarrelResearchPosition, true);
                        Case.Fault = Fault; BarrelResearchDeath(Case, false);
                        BarrelResearchCheck(Case.Injected == 1 && Case.Replacement[0].amount == 7, "performed second-entry failure seam / first payout retained");
                        if (Fault == BarrelResearchFault.DropNull || Fault == BarrelResearchFault.DropThrow)
                            BarrelResearchCheck(Case.Replacement[1] != null && Case.Replacement[1].IsValid() && Case.Replacement[1].parent == null &&
                                ReferenceEquals(Case.Replacement[1].GetWorldEntity(), null), "failed Drop seam unowned Item accounted");
                        else BarrelResearchCheck(Case.Replacement[1] == null, "failed Create seam returned no second Item");
                        Puts(BarrelResearchMarker + "MEASURED fixture_seam=" + Fault + " host_internal_failure=UNMEASURED no_rollback_no_retry");
                    });
                }
                BarrelResearchGuard();
                BarrelResearchCheck(VerifyAllEntityPatches() && EntityDestroyedAvailable() && !EntityObserverBroken,
                    "D20/B3 remain available after all measured cases");
                Measured = true;
            }
            catch (Exception Error) { PrintError(BarrelResearchMarker + "FAIL " + Error); }
            finally {
                try {
                    BarrelResearchCleaning = true; BarrelResearchVeto = false;
                    bool Unpatched = true;
                    if (BarrelResearchHarmony != null) for (int Index = 0; Index < BarrelResearchMethodCount; Index++) {
                        try { BarrelResearchHarmony.Unpatch(BarrelResearchMethods[Index], HarmonyPatchType.All, BarrelResearchHarmony.Id); }
                        catch (Exception Error) { Unpatched = false; PrintError(BarrelResearchMarker + "UNPATCH_FAIL " + Error); }
                    }
                    BarrelResearchCleanup();
                    BarrelResearchCheck(Unpatched && BarrelResearchLedgerCount == 0 && BarrelResearchOffThread == 0 && BarrelResearchPatchFailure == null &&
                        VerifyAllEntityPatches() && EntityDestroyedAvailable(), "final cleanup / original observer continuity");
                    Clean = true;
                }
                catch (Exception Error) { PrintError(BarrelResearchMarker + "CLEANUP_FAIL " + Error); }
                finally {
                    BarrelResearchOwner = null; BarrelResearchCurrent = null; BarrelResearchActor = null; BarrelResearchHarmony = null;
                    Array.Clear(BarrelResearchMethods, 0, BarrelResearchMethods.Length);
                    Array.Clear(BarrelResearchCases, 0, BarrelResearchCases.Length);
                    Array.Clear(BarrelResearchLedger, 0, BarrelResearchLedger.Length);
                    Array.Clear(BarrelResearchTrace, 0, BarrelResearchTrace.Length);
                    if (Measured && Clean) Puts(BarrelResearchMarker + "PASS mechanism exact first-prefab measured physical cases; boost only when MEASURED; failures are fixture seams; corpse/debris exclusion and actual failing Spawn ownership unqualified; no full D2 qualification");
                    try { ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"); }
                    catch (Exception Error) { PrintError(BarrelResearchMarker + "QUIT_FAIL " + Error); }
                }
            }
        }
        private void BarrelResearchCleanup()
        {
            BarrelResearchCheck(Thread.CurrentThread.ManagedThreadId == BarrelResearchThread && Host != null && !Host.Busy, "owner cleanup / no VM entry");
            BarrelResearchCleaning = true; BarrelResearchVeto = false;
            bool Failed = false;
            // First release every exact world/entity through normal Kill, then
            // observe keyed removal. Never unregister/destroy a bookkeeping ghost.
            for (int Index = BarrelResearchLedgerCount - 1; Index >= 0; Index--) {
                var Resource = BarrelResearchLedger[Index];
                if (ReferenceEquals(Resource.Entity, null)) continue;
                try {
                    BarrelResearchCheck(Resource.Witness == null || EntityLifetimes.IsWitnessEpochUnchanged(Resource.Witness),
                        "cleanup original entity epoch / observer continuity");
                    BarrelResearchCheck(Resource.Entity == null || Resource.Entity.net == null || Resource.Id == 0 ||
                        Resource.Entity.net.ID.Value == Resource.Id, "cleanup original entity network ID");
                    if (Resource.Entity != null && !Resource.Entity.IsDestroyed) Resource.Entity.Kill();
                    BarrelResearchCheck(Resource.Witness == null || EntityLifetimes.IsWitnessEpochUnchanged(Resource.Witness),
                        "cleanup must not remove a reused entity epoch");
                    BarrelResearchCheck(Resource.Id == 0 || !ReferenceEquals(
                        BaseNetworkable.serverEntities.Find(new NetworkableId(Resource.Id)), Resource.Entity), "owned world removed " + Resource.Id);
                    BarrelResearchCheck(Resource.Entity == null || Resource.Entity.net == null, "owned world network authority released");
                }
                catch (Exception Error) { Failed = true; PrintError(BarrelResearchMarker + "CLEANUP_FAIL entity " + Resource.Id + " " + Error); }
            }
            for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                Item Value = BarrelResearchLedger[Index].Item;
                if (Value == null) continue;
                try {
                    BarrelResearchCheck(!BarrelResearchLedger[Index].OwnershipUnknown,
                        "UNKNOWN host-call Item ownership: possible initialized unreturned WorldItem; no Item.Remove attempted");
                    // IsValid only checks removeTime. EnterPool resets it to zero;
                    // removing a zero-UID pooled record again would double-free
                    // it and corrupt the fixture's later ItemManager.Create calls.
                    if (Value.uid.Value == 0) continue;
                    BarrelResearchCheck(Value.uid.Value == BarrelResearchLedger[Index].ItemUid && Value.info != null,
                        "cleanup exact current Item UID / definition");
                    if (Value.IsValid()) {
                        BarrelResearchCheck(Value.parent == null && ReferenceEquals(Value.GetWorldEntity(), null), "cleanup only unowned Item");
                        Value.Remove(0);
                        BarrelResearchCheck(Value.IsRemoved(), "unowned Item removal accepted");
                    }
                }
                catch (Exception Error) { Failed = true; PrintError(BarrelResearchMarker + "CLEANUP_FAIL Item " + Error); }
            }
            try { ItemManager.DoRemoves(); }
            catch (Exception Error) { Failed = true; PrintError(BarrelResearchMarker + "CLEANUP_FAIL DoRemoves " + Error); }
            BarrelResearchCurrent = null; BarrelResearchActor = null;
            Array.Clear(BarrelResearchCases, 0, BarrelResearchCases.Length);
            Array.Clear(BarrelResearchTrace, 0, BarrelResearchTrace.Length);
            if (!Failed) { Array.Clear(BarrelResearchLedger, 0, BarrelResearchLedger.Length); BarrelResearchLedgerCount = 0; }
            BarrelResearchTraceCount = 0; BarrelResearchDropDepth = BarrelResearchBonusDepth = 0;
            BarrelResearchCleaning = false;
            BarrelResearchCheck(!Failed, "owned cleanup incomplete; no success claim");
        }
    }
}
