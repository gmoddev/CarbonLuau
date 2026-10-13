// TEST ONLY: D7 stones probe and later public matrix over unchanged D2A-D6 paths.
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        partial void SelectLootAdditionalItems(ref bool Items, ref bool ProbeOnly) { Items = true; }
        private void LootAdditionalMethod(MethodInfo Method, string Label)
        {
            BarrelResearchCheck(Method != null && Method.GetMethodBody() != null &&
                Method.GetMethodBody().GetILAsByteArray().Length <= 16384 &&
                Method.GetMethodBody().ExceptionHandlingClauses.Count <= 16, "D7 bounded loaded " + Label);
            byte[] Body = Method.GetMethodBody().GetILAsByteArray();
            using (var Hash = SHA256.Create())
                Puts("[CarbonLuau:LootFixture] D7_LIVE_BODY method=" + Method.DeclaringType.FullName + "." + Method +
                    " token=0x" + Method.MetadataToken.ToString("x8") + " mvid=" + Method.Module.ModuleVersionId +
                    " raw_il_bytes=" + Body.Length + " raw_il_sha256=" +
                    BitConverter.ToString(Hash.ComputeHash(Body)).Replace("-", "").ToLowerInvariant() +
                    " eh=" + Method.GetMethodBody().ExceptionHandlingClauses.Count + " raw_reflection_body_not_harmony_replacement=true");
        }
        partial void RunLootAdditionalProbe()
        {
            BarrelResearchCheck(BarrelResearchOnOwner() && LootCurrent == null && BarrelResearchLedgerCount == 0,
                "D7 bounded isolated probe owner and empty ledger");
            ItemDefinition Definition = ItemManager.FindItemDefinition("stones");
            BarrelResearchCheck(Definition != null, "D7 candidate name lookup");
            Puts("[CarbonLuau:LootFixture] D7_DEFINITION name=" + Definition.shortname + " item_id=" + Definition.itemid +
                " stackable=" + Definition.stackable + " condition_enabled=" + Definition.condition.enabled +
                " mods=" + (Definition.itemMods == null ? -1 : Definition.itemMods.Length));
            BarrelResearchCheck(Definition.shortname == "stones" && Definition.itemid == -2099697608 &&
                ReferenceEquals(Definition, ItemManager.FindItemDefinition(Definition.itemid)) && Definition.stackable == 1000 &&
                !Definition.condition.enabled && Definition.itemMods != null && Definition.itemMods.Length <= 8,
                "D7 exact host-reported stones name/numeric lookup identity stack and no condition");
            foreach (string Name in new[] { "scrap", "metal.fragments", "wood", "stones" }) {
                ItemDefinition Supported = ItemManager.FindItemDefinition(Name);
                BarrelResearchCheck(Supported != null && Supported.shortname == Name && Supported.itemid != 0 &&
                    Supported.stackable >= 1000 && ReferenceEquals(Supported, ItemManager.FindItemDefinition(Supported.itemid)),
                    "D7 bounded supported definition name/ID identity and existing maximum premise");
                Puts("[CarbonLuau:LootFixture] D7_SUPPORTED_DEFINITION name=" + Supported.shortname + " item_id=" + Supported.itemid +
                    " stackable=" + Supported.stackable + " name_id_lookup_reference=true");
            }
            var Components = Definition.GetComponents<Component>();
            BarrelResearchCheck(Components != null && Components.Length <= 16, "D7 bounded direct definition root components");
            foreach (var Component in Components) {
                BarrelResearchCheck(Component != null, "D7 no missing direct definition component");
                Puts("[CarbonLuau:LootFixture] D7_DEFINITION_COMPONENT type=" + Component.GetType().FullName);
            }
            foreach (var Mod in Definition.itemMods) {
                BarrelResearchCheck(Mod != null, "D7 no null ItemMod");
                Puts("[CarbonLuau:LootFixture] D7_ITEM_MOD_TYPE type=" + Mod.GetType().FullName);
            }
            int Experience = 0, Tax = 0;
            foreach (var Mod in Definition.itemMods) {
                string Name = Mod.GetType().Name;
                BarrelResearchCheck(Name == "ItemModXPWhenUsed" || Name == "ItemModApartmentTax",
                    "D7 only exact candidate mods with measured empty creation/world/removal paths");
                if (Name == "ItemModXPWhenUsed") Experience++; else Tax++;
                int Callbacks = 0;
                var Methods = typeof(ItemMod).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                BarrelResearchCheck(Methods.Length <= 128, "D7 bounded base ItemMod method inspection");
                foreach (var Base in Methods) {
                    if (Base.Name != "OnItemCreated" && Base.Name != "OnRemove" && Base.Name != "OnParentChanged" &&
                        Base.Name != "OnMovedToWorld" && Base.Name != "OnRemovedFromWorld" && Base.Name != "OnChanged") continue;
                    var Parameters = Base.GetParameters();
                    BarrelResearchCheck(Parameters.Length <= 8, "D7 bounded callback signature");
                    var Types = new Type[Parameters.Length];
                    for (int Index = 0; Index < Types.Length; Index++) Types[Index] = Parameters[Index].ParameterType;
                    MethodInfo Actual = Mod.GetType().GetMethod(Base.Name, BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic, null, Types, null);
                    LootAdditionalMethod(Actual, Name + "." + Base.Name);
                    byte[] Body = Actual.GetMethodBody().GetILAsByteArray();
                    BarrelResearchCheck(Actual.DeclaringType == typeof(ItemMod) && Body.Length == 1 && Body[0] == 0x2a,
                        "D7 exact empty inherited creation/world/removal callback");
                    Puts("[CarbonLuau:LootFixture] D7_ITEM_MOD_EMPTY type=" + Name + " method=" + Base.Name + " owner=" + Actual.DeclaringType.FullName + " body=ret");
                    Callbacks++;
                }
                BarrelResearchCheck(Callbacks == 6, "D7 all six required mod callbacks measured exactly once");
            }
            BarrelResearchCheck(Experience == 1 && Tax == 1 && Definition.itemMods.Length == 2,
                "D7 exact unique XP/tax candidate mods; unsupported special initialization fails closed");
            foreach (var Component in Components)
                BarrelResearchCheck(Component.GetType() == typeof(Transform) || ReferenceEquals(Component, Definition) ||
                    Component is ItemMod && Array.IndexOf(Definition.itemMods, (ItemMod)Component) >= 0,
                    "D7 no unsupported direct definition initialization component");
            LootAdditionalMethod(typeof(ItemManager).GetMethod("Create", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(ItemDefinition), typeof(int), typeof(ulong), typeof(bool), typeof(ulong) }, null), "ItemManager.Create");
            foreach (string Name in new[] { "Drop", "CreateWorldObject", "RemoveFromWorld", "Remove" })
                LootAdditionalMethod(BarrelResearchMethod(typeof(Item), Name), "Item." + Name);
            string WorldPrefab = null;
            try {
                foreach (int Amount in new[] { 1, 50, 250, 1000 }) {
                    Item Value = BarrelResearchNewItem("stones", Amount);
                    ulong Uid = Value.uid.Value;
                    FieldInfo Fuel = typeof(Item).GetField("fuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    BarrelResearchCheck(Fuel != null && Fuel.FieldType == typeof(float), "D7 exact existing scalar fuel member");
                    float FuelAmount = (float)Fuel.GetValue(Value);
                    BarrelResearchCheck(Uid != 0 && ReferenceEquals(Value.info, Definition) && Value.skin == 0 &&
                        !Value.hasCondition && Value.condition == 0 && Value.maxCondition == 0 && FuelAmount == 0 &&
                        Value.contents == null && Value.instanceData == null && ReferenceEquals(Value.GetHeldEntity(), null),
                        "D7 fresh actual stones scalar defaults no child/held/special ownership");
                    Puts("[CarbonLuau:LootFixture] D7_FRESH amount=" + Amount + " uid=" + Uid + " item_id=" + Value.info.itemid +
                        " skin=" + Value.skin + " condition=" + Value.condition + " max_condition=" + Value.maxCondition +
                        " fuel=" + FuelAmount + " contents=false instance_data=false held=false");
                    BarrelResearchOwnership(Value, true);
                    BaseEntity Entity = Value.Drop(BarrelResearchPosition, Vector3.zero);
                    BarrelResearchHold(Entity);
                    WorldItem World = Entity as WorldItem;
                    BarrelResearchCheck(World != null && !World.IsDestroyed && World.IsFullySpawned() && World.net != null &&
                        ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) && Value.uid.Value == Uid &&
                        ReferenceEquals(World.item, Value) && ReferenceEquals(Value.GetWorldEntity(), World) && Value.parent == null &&
                        Value.amount == Amount && ReferenceEquals(Value.info, Definition), "D7 actual exact reciprocal fully spawned stones world");
                    BarrelResearchOwnership(Value, false);
                    BarrelResearchCheck(World.GetType() == typeof(DroppedItem) &&
                        (WorldPrefab == null || World.PrefabName == WorldPrefab), "D7 identical ordinary generic dropped world type/prefab");
                    WorldPrefab = World.PrefabName;
                    var WorldComponents = World.GetComponents<Component>();
                    BarrelResearchCheck(WorldComponents != null && WorldComponents.Length <= 16, "D7 bounded actual world root components");
                    foreach (var Component in WorldComponents) {
                        BarrelResearchCheck(Component != null, "D7 no missing world root component");
                        Puts("[CarbonLuau:LootFixture] D7_WORLD_COMPONENT amount=" + Amount + " type=" + Component.GetType().FullName);
                    }
                    var Witness = EntityLifetimes.CaptureCompletedWitness(World);
                    BarrelResearchCheck(Witness != null && ReferenceEquals(Witness.Target, World) &&
                        EntityLifetimes.IsWitnessEpochUnchanged(Witness), "D7 completed Spawn original epoch observer authority");
                    ulong Id = World.net.ID.Value;
                    Puts("[CarbonLuau:LootFixture] D7_PROBE_WORLD amount=" + Amount + " uid=" + Uid + " id=" + Id +
                        " prefab=" + World.PrefabName + " epoch=" + Witness.Epoch + " fully_spawned=true reciprocal=true");
                    BarrelResearchCleanup();
                    BarrelResearchCheck(BarrelResearchLedgerCount == 0 &&
                        !ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Id)), World) &&
                        (Value.uid.Value == 0 || Value.uid.Value == Uid && Value.IsRemoved()), "D7 exact original Item/world cleanup converged");
                    Puts("[CarbonLuau:LootFixture] D7_PROBE_CLEAN amount=" + Amount + " id=" + Id + " ledger=0 original_removed=true");
                }
            } finally { BarrelResearchCleanup(); }
            LootFixtureAdditionalProbeDone = true;
        }
        private LootDropEntry[] LootAdditionalEntries(int First = 7, int Second = 11)
        { return new[] { new LootDropEntry("stones", First, First), new LootDropEntry("stones", Second, Second) }; }
        private void LootAdditionalRoot(bool Both = true)
        {
            LootFixturePrefab = LootRulePolicy.Prefab; BarrelResearchSelectedPrefab = LootFixturePrefab;
            LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab;
            LootFixtureEntries = LootAdditionalEntries();
            LootFixtureSecondEntries = Both ? LootAdditionalEntries(13, 17) : null;
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 stones exact complete root set publication");
            LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active);
            if (Both) LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
        }
        private string LootAdditionalAddon(int First = 13, int Second = 17)
        { return LootFixtureSingleDeclaration(LootRulePolicy.SecondPrefab, LootAdditionalEntries(First, Second)); }
        private void LootAdditionalReplace(string Source, string Version)
        {
            string[] Result = Addons.ReplaceArchive(LootRegistryProvider, LootRegistryToken,
                LootFixturePackage("d7owner", Source, false, Version));
            BarrelResearchCheck(Result[0] == "OK" && Addons.ProcessOne() &&
                Addons.Status(LootRegistryProvider, LootRegistryToken)[2] == "Active", "D7 exact second-owner addon replacement");
        }
        private void LootAdditionalAmounts(string Prefab)
        {
            LootFixtureSecondEntries = null;
            LootFixtureEntries = new[] { new LootDropEntry("stones", 1, 1), new LootDropEntry("stones", 0, 0),
                new LootDropEntry("scrap", 2, 2), new LootDropEntry("metal.fragments", 3, 3), new LootDropEntry("wood", 4, 4) };
            LootFixtureSelectProfile(Prefab); LootFixturePayout("d7_minimum_zero_four_item_mixed", 4, true);
            LootFixtureEntries = new[] { new LootDropEntry("stones", 0, 0) };
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 all-zero stones descriptor");
            LootFixturePayout("d7_all_zero_stones", 0, true);
            LootFixtureEntries = new[] { new LootDropEntry("stones", 50, 250) };
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 inclusive stones range50..250 descriptor");
            LootFixturePayout("public_range", 1, true);
            LootFixtureEntries = new[] { new LootDropEntry("stones", 1000, 1000) };
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 maximum stones descriptor");
            LootFixturePayout("d7_maximum_stones", 1, true);
            LootFixtureEntries = new LootDropEntry[LootRulePolicy.Entries];
            for (int Index = 0; Index < LootFixtureEntries.Length; Index++) LootFixtureEntries[Index] = new LootDropEntry("stones", 1000, 1000);
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 eight repeated maximum stones entries");
            LootFixturePayout("d7_repeated_eight_maximum_stones", 8, true);
            Puts("[CarbonLuau:LootFixture] PASS D7 amounts prefab=" + Prefab + " min=1 zero=0 range=50..250 max=1000 repeated=8 mixed=scrap,metal.fragments,wood,stones");
        }
        partial void GetLootAdditionalBudget(ref int Needed)
        {
            if (!LootFixtureAdditionalItems || LootFixtureStep < 44) return;
            Needed = LootFixtureStep == 44 || LootFixtureStep == 45 ? 5 : LootFixtureStep >= 47 && LootFixtureStep <= 51 ? 4 :
                LootFixtureStep == 59 ? 8 : LootFixtureStep == 46 || LootFixtureStep == 61 ? 3 : 2;
        }
        partial void RunLootAdditionalStep()
        {
            switch (LootFixtureStep) {
            case 44: LootAdditionalAmounts(LootRulePolicy.Prefab); break;
            case 45: LootAdditionalAmounts(LootRulePolicy.SecondPrefab); break;
            case 46: {
                LootAdditionalRoot();
                var Old = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Old);
                var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Old);
                foreach (string Item in new[] { "stones", "sulfur" }) {
                    LootFixtureEntries = new[] { new LootDropEntry(Item, Item == "stones" ? 1001 : 1, Item == "stones" ? 1001 : 1) };
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old),
                        "D7 maximum1001 and unsupported sulfur reject before publication");
                    LootRegistryRule(LootRulePolicy.Prefab, Old, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Old, Second);
                }
                LootFixtureEntries = new[] { new LootDropEntry("stones", 1000, 1000) };
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 controlled maximum stones stack premise");
                // Establish the current actual frame through an eligible payout,
                // as the unchanged D5 amount controls do. PREPARE may normalize a
                // fresh frame before rejecting the changed stack premise; this
                // control still requires unchanged counters and frame afterward.
                LootRegistryPayout("d7_stack_control_actual_frame_prime", LootRulePolicy.Prefab, 1, true);
                BarrelResearchCheck(LootFrameNumber == Time.frameCount && LootFrameUsed > 0,
                    "D7 stack control starts in the already normalized actual frame");
                ItemDefinition Definition = ItemManager.FindItemDefinition("stones");
                BarrelResearchCheck(Definition != null && Definition.stackable == 1000, "D7 exact stack1000 premise");
                var Case = BarrelResearchCreate("d7_stack999_precommit", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                Exception FirstCause = null;
                try {
                    Definition.stackable = 999;
                    Puts("[CarbonLuau:LootFixture] D7_CONTROL stones_stackable=999 planned_max=1000 I12_premise_change=true");
                    LootFixtureVanilla(Case);
                } catch (Exception Error) {
                    FirstCause = Error;
                    PrintError("[CarbonLuau:LootFixture] FIRST_CAUSE case=d7_stack999_precommit " + Error);
                } finally {
                    Definition.stackable = 1000;
                    try {
                        if (LootFixtureOriginalSourceEpoch(Case)) LootFixtureReleaseStored(Case);
                    } catch (Exception Error) {
                        PrintError("[CarbonLuau:LootFixture] SECONDARY_CLEANUP_FAIL case=d7_stack999_precommit source " + Error);
                        if (FirstCause == null) FirstCause = Error;
                    }
                    try { BarrelResearchCleanup(); }
                    catch (Exception Error) {
                        PrintError("[CarbonLuau:LootFixture] SECONDARY_CLEANUP_FAIL case=d7_stack999_precommit ledger " + Error);
                        if (FirstCause == null) FirstCause = Error;
                    }
                }
                if (FirstCause != null) ExceptionDispatchInfo.Capture(FirstCause).Throw();
                LootAdditionalRoot(); LootRegistryPair("d7_after_precommit_rejection");
                break;
            }
            case 47:
                LootRegistryVeto(LootRulePolicy.Prefab); LootRegistryVeto(LootRulePolicy.SecondPrefab);
                Puts("[CarbonLuau:LootFixture] PASS D7 source Kill veto preserves stones worlds partial outcome and no replay both prefabs");
                break;
            case 48:
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    long Before = LootCleanupFailed; LootFixtureInjectSpawn = true;
                    try { LootRegistryPayout("d7_actual_incomplete_stones_spawn", Prefab, 1, false); }
                    finally { LootFixtureInjectSpawn = false; }
                    BarrelResearchCheck(LootFixtureFailedWorld != null && LootFixtureFailedWorld.net == null &&
                        !EntityLifetimes.HasCompletedObservation(LootFixtureFailedWorld) && LootCleanupFailed == Before,
                        "D7 incomplete stones world has no completed lifetime; exact original attempt cleanup");
                    LootFixtureFailedWorld = null;
                }
                LootRegistryPair("d7_after_incomplete_stones_spawn");
                break;
            case 49:
            case 50:
                LootFixtureCreateFault = LootFixtureStep == 49 ? 1 : 2;
                try {
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab })
                        LootRegistryPayout("d7_actual_stones_create_" + LootFixtureCreateFault, Prefab, 1, false);
                } finally { LootFixtureCreateFault = 0; }
                LootRegistryPair("d7_after_stones_creation_partial");
                Puts("[CarbonLuau:LootFixture] PASS D7 actual null/throw Create dispatch existing physical first stones world survives; no unreturned allocation claim");
                break;
            case 51:
                LootFixtureMutateAmount = true;
                try {
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab })
                        LootRegistryPayout("d7_stones_trusted_quantity_interference", Prefab, 2, false);
                } finally { LootFixtureMutateAmount = false; }
                LootRegistryPair("d7_after_stones_interference");
                break;
            case 52:
            case 53:
                LootFixturePrefab = LootFixtureStep == 52 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab;
                LootFixtureSecondPrefab = LootFixtureStep == 52 ? LootRulePolicy.SecondPrefab : LootRulePolicy.Prefab;
                LootFixtureEntries = LootAdditionalEntries(); LootFixtureSecondEntries = LootAdditionalEntries(13, 17);
                BarrelResearchSelectedPrefab = LootFixturePrefab;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 two stones slots before controlled bag branch");
                LootFixtureControlledBag(LootAdditionalEntries());
                LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
                Puts("[CarbonLuau:LootFixture] PASS D7 controlled bag stones suppression selected=" + LootFixturePrefab + " other slot retained natural_bag_branch=false");
                break;
            case 54:
                LootAdditionalRoot();
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    BarrelResearchCreateActor(); BarrelResearchCheck(BarrelResearchBoost(), "D7 real Scrap_Yield modifier");
                    LootRegistryPayout("d7_stones_stored_real_bonus_suppressed", Prefab);
                }
                Puts("[CarbonLuau:LootFixture] PASS D7 stored and real bonus suppression with stones both prefabs");
                break;
            case 55:
                LootAdditionalRoot(false);
                LootRegistryToken = LootRegistryAdd(LootRegistryProvider, "d7owner", LootAdditionalAddon(), "Active");
                BarrelResearchCheck(!ReferenceEquals(LootRegistryOwner(LootRulePolicy.Prefab), LootRegistryOwner(LootRulePolicy.SecondPrefab)),
                    "D7 stones distinct exact root/addon defining owners");
                LootRegistryPair("d7_stones_independent_root_addon");
                break;
            case 56: {
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                LootFixtureSecondPrefab = LootRulePolicy.Prefab; LootFixtureSecondEntries = LootAdditionalEntries(23, 29);
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Root),
                        "D7 repeated stones prefab declaration rejects complete candidate set");
                    LootRegistryRule(LootRulePolicy.Prefab, Root, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab; LootFixtureSecondEntries = null; }
                LootRegistryAdd(LootRegistryConflictProvider, "d7collision", LootFixtureSingleDeclaration(LootRulePolicy.Prefab, LootAdditionalEntries()), "Failed");
                LootRegistryAdd(LootRegistryConflictProvider, "d7multicollision", LootFixtureDeclaration() + LootAdditionalAddon(), "Failed");
                LootFixtureSecondEntries = LootAdditionalEntries(23, 29);
                try {
                    BarrelResearchCheck(Host.Reload().Status != RuntimeStatus.OK && ReferenceEquals(Gameplay.Active, Root),
                        "D7 overlapping candidate complete stones set rejects atomically");
                    LootRegistryRule(LootRulePolicy.Prefab, Root, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSecondEntries = null; }
                string Previous = LootFixtureSource;
                LootFixtureSecondEntries = LootAdditionalEntries(23, 29); LootFixtureModuleRollback = true;
                LootFixtureSource = LootFixtureSingleDeclaration(LootRulePolicy.Prefab, LootFixtureEntries) +
                    "assert(not pcall(function() require('registrysecondfailed') end)); " + Previous;
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Root.Disposed,
                        "D7 caught stones second-key module failure rolls back before candidate publication");
                    LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSource = Previous; LootFixtureSecondEntries = null; LootFixtureModuleRollback = false; }
                LootRegistryPair("d7_conflict_original_stones_owners");
                break;
            }
            case 57: {
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                string Previous = LootFixtureSource;
                LootFixtureEntries = LootAdditionalEntries(19, 23); LootFixtureSource = "error('D7 failed stones candidate')";
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Root),
                        "D7 failed stones root candidate preserves outgoing immutable set");
                    LootRegistryRule(LootRulePolicy.Prefab, Root, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSource = Previous; }
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Root.Disposed, "D7 healthy root stones replacement");
                var NextRoot = Gameplay.Active; var NextFirst = LootRegistryRule(LootRulePolicy.Prefab, NextRoot);
                LootAdditionalReplace(LootAdditionalAddon(31, 37) + "error('D7 failed addon replacement')", "2.0.0");
                BarrelResearchCheck(Addons.Status(LootRegistryProvider, LootRegistryToken)[5].Contains("replacement failed"),
                    "D7 failed stones addon replacement retains original active source");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                LootAdditionalReplace(LootAdditionalAddon(31, 37), "3.0.0");
                BarrelResearchCheck(Addon.Disposed && !ReferenceEquals(LootRegistryOwner(LootRulePolicy.SecondPrefab), Addon),
                    "D7 independent second-owner replacement retires exact outgoing session");
                LootRegistryRule(LootRulePolicy.Prefab, NextRoot, NextFirst);
                BarrelResearchCheck(First.Entry(0).Min == 7 && Second.Entry(0).Min == 13,
                    "D7 outgoing stones descriptors retain copied scalar values");
                LootRegistryPair("d7_independent_replaced_stones");
                break;
            }
            case 58: {
                var Root = Gameplay.Active; var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab);
                long Vm = Host.VmGenerationId; int Receipts = LootFixtureReceipts;
                BarrelResearchCheck(Host.Execute("d7.fatal", "while true do end").Status == RuntimeStatus.TIMEOUT && Host.Ready &&
                    Host.VmGenerationId != Vm && Root.Disposed && Addon.Disposed, "D7 fatal retirement invalidates both stones owners");
                LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryNone(LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(Addons.ProcessOne() && Addons.Status(LootRegistryProvider, LootRegistryToken)[2] == "Active" &&
                    !ReferenceEquals(LootRegistryOwner(LootRulePolicy.SecondPrefab), Addon) && LootFixtureReceipts == Receipts,
                    "D7 stones addon reconstruction fresh generation no payout replay");
                try { Root.StageLootRule(LootRulePolicy.Prefab, LootAdditionalEntries()); throw new InvalidOperationException("D7 stale owner unexpectedly declared"); }
                catch (FacadeException) { }
                LootRegistryPair("d7_stones_recovery_fresh_owners");
                break;
            }
            case 59: {
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                int Frame = Time.frameCount, Before = LootFixtureReceipts;
                int Remaining = LootFrameNumber == Frame ? LootRulePolicy.PerFrame - LootFrameUsed : LootRulePolicy.PerFrame;
                for (int Index = 0; Index < Remaining; Index++)
                    LootRegistryPayout("d7_stones_shared_fill_" + Index, Index % 2 == 0 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(LootFixtureReceipts == Before + Remaining && LootFrameNumber == Frame && LootFrameUsed == 8,
                    "D7 stones independent owners share exactly eight actual Unity frame admissions");
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Time.frameCount == Frame &&
                    LootFrameNumber == Frame && LootFrameUsed == 8, "D7 stones root replacement never resets global frame budget");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    try { LootFixtureVanilla(BarrelResearchCreate("d7_stones_shared_rejected", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Prefab)); }
                    finally { BarrelResearchCleanup(); }
                }
                Puts("[CarbonLuau:LootFixture] PASS D7 independent stones owners share actual-frame limit=8; replacement retains exhausted budget and both excess sources vanilla");
                break;
            }
            case 60: {
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                LootFixtureEntries = LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 empty root set withdraws only owned stones slot");
                LootRegistryNone(LootRulePolicy.Prefab); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                try { LootFixtureVanilla(BarrelResearchCreate("d7_stones_root_withdrawn_vanilla", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, LootRulePolicy.Prefab)); }
                finally { BarrelResearchCleanup(); }
                LootRegistryPayout("d7_stones_addon_survives_root_withdrawal", LootRulePolicy.SecondPrefab);
                LootAdditionalRoot(false); var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                BarrelResearchCheck(Addons.UnloadProvider(LootRegistryProvider) == 1 && Addon.Disposed,
                    "D7 provider unload removes only exact stones second owner");
                LootRegistryNone(LootRulePolicy.SecondPrefab); LootRegistryRule(LootRulePolicy.Prefab, Root, First);
                BarrelResearchCheck(Addons.UnloadProvider(LootRegistryConflictProvider) == 2, "D7 failed stones conflicts release registrations");
                LootRegistryPayout("d7_stones_root_survives_provider_unload", LootRulePolicy.Prefab);
                break;
            }
            case 61:
                LootAdditionalRoot(); LootRegistryPair("d7_stones_root_reclaimed_both");
                LootFixturePrefab = LootRulePolicy.SecondPrefab; BarrelResearchSelectedPrefab = LootFixturePrefab;
                LootFixtureEntries = LootAdditionalEntries(); LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D7 final stones owner for unchanged B2 regression");
                LootRegistryNone(LootRulePolicy.Prefab); LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
                LootRegistryPayout("d7_final_stones_public_owner", LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(Addons.Count == 0 && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D7 zero retained provider/physical Item/world resources before B2");
                LootFixtureAdditionalDone = true;
                Puts("[CarbonLuau:LootFixture] PASS D7 stones both profiles independent publication/failure/suppression/budget matrix before unchanged B2 continuation");
                break;
            default: throw new InvalidOperationException("D7 unknown bounded fixture step");
            }
            LootRegistryRetention();
            BarrelResearchCheck(LootCurrent == null && BarrelResearchLedgerCount == 0, "D7 every public matrix step releases exact physical resources");
        }
    }
}
