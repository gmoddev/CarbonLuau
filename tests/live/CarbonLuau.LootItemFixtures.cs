// TEST ONLY: D5 item inspection and actual public matrix on the D4 substrate.
using System;
using System.Reflection;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        partial void SelectLootItemQualification(ref bool Items) { Items = true; }
        partial void RunLootItemProbe()
        {
            BarrelResearchCheck(BarrelResearchOnOwner() && LootCurrent == null && BarrelResearchLedgerCount == 0,
                "D5 bounded isolated probe owner and empty ledger");
            ItemDefinition Definition = ItemManager.FindItemDefinition("wood");
            BarrelResearchCheck(Definition != null && Definition.shortname == "wood" && Definition.itemid == -151838493 &&
                ReferenceEquals(Definition, ItemManager.FindItemDefinition(Definition.itemid)) && Definition.stackable == 1000 &&
                !Definition.condition.enabled && Definition.itemMods != null && Definition.itemMods.Length <= 8,
                "D5 exact wood name/ID lookup identity stack and no condition");
            var Components = Definition.GetComponents<Component>();
            BarrelResearchCheck(Components != null && Components.Length <= 16, "D5 bounded direct definition root components");
            foreach (var Component in Components) {
                BarrelResearchCheck(Component != null, "D5 no missing direct definition component");
                Puts("[CarbonLuau:LootFixture] D5_DEFINITION_COMPONENT type=" + Component.GetType().FullName);
            }
            int Burnable = 0, Experience = 0, Tax = 0; float ExpectedFuel = 0;
            foreach (var Mod in Definition.itemMods) {
                BarrelResearchCheck(Mod != null, "D5 no null ItemMod");
                string Name = Mod.GetType().Name;
                Puts("[CarbonLuau:LootFixture] D5_ITEM_MOD_TYPE type=" + Mod.GetType().FullName);
                BarrelResearchCheck(Name == "ItemModBurnable" || Name == "ItemModXPWhenUsed" || Name == "ItemModApartmentTax",
                    "D5 only exact measured wood mod types");
                MethodInfo Created = Mod.GetType().GetMethod("OnItemCreated", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic, null, new[] { typeof(Item) }, null);
                BarrelResearchCheck(Created != null && Created.GetMethodBody() != null &&
                    Created.GetMethodBody().GetILAsByteArray().Length <= 128, "D5 bounded exact mod creation method");
                BarrelResearchLogMethod(Created);
                if (Name != "ItemModBurnable") {
                    if (Name == "ItemModXPWhenUsed") Experience++; else Tax++;
                    byte[] Body = Created.GetMethodBody().GetILAsByteArray();
                    BarrelResearchCheck(Created.DeclaringType == typeof(ItemMod) && Body.Length == 1 && Body[0] == 0x2a,
                        "D5 XP/tax inherit exact empty creation callback");
                    Puts("[CarbonLuau:LootFixture] D5_ITEM_MOD_NO_CREATE type=" + Name + " owner=" + Created.DeclaringType.FullName + " body=ret");
                    continue;
                }
                Burnable++;
                FieldInfo Fuel = Mod.GetType().GetField("fuelAmount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                BarrelResearchCheck(Fuel != null && Fuel.FieldType == typeof(float), "D5 exact scalar burnable fuel member");
                float Amount = (float)Fuel.GetValue(Mod);
                BarrelResearchCheck(!Single.IsNaN(Amount) && !Single.IsInfinity(Amount) && Amount > 0, "D5 finite positive scalar burnable fuel");
                Puts("[CarbonLuau:LootFixture] D5_ITEM_MOD type=" + Mod.GetType().FullName + " fuel_amount=" + Amount);
                ExpectedFuel = Amount;
            }
            BarrelResearchCheck(Burnable == 1 && Experience == 1 && Tax == 1 && Definition.itemMods.Length == 3,
                "D5 exact unique wood mods with one scalar burnable creation callback");
            Puts("[CarbonLuau:LootFixture] D5_DEFINITION name=" + Definition.shortname + " item_id=" + Definition.itemid +
                " stackable=" + Definition.stackable + " condition_enabled=" + Definition.condition.enabled + " mods=" + Definition.itemMods.Length);
            string WorldPrefab = null;
            try {
                foreach (int Amount in new[] { 1, 50, 1000 }) {
                    Item Value = BarrelResearchNewItem("wood", Amount);
                    ulong Uid = Value.uid.Value;
                    FieldInfo Fuel = typeof(Item).GetField("fuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    BarrelResearchCheck(Fuel != null && Fuel.FieldType == typeof(float), "D5 exact Item scalar fuel member");
                    float FuelAmount = (float)Fuel.GetValue(Value);
                    BarrelResearchCheck(Uid != 0 && ReferenceEquals(Value.info, Definition) && Value.skin == 0 &&
                        !Value.hasCondition && !Single.IsNaN(Value.condition) && !Single.IsInfinity(Value.condition) &&
                        !Single.IsNaN(Value.maxCondition) && !Single.IsInfinity(Value.maxCondition) && Value.contents == null &&
                        Value.instanceData == null && ReferenceEquals(Value.GetHeldEntity(), null) &&
                        FuelAmount == ExpectedFuel,
                        "D5 fresh exact Item scalar creation state no child/held ownership");
                    Puts("[CarbonLuau:LootFixture] D5_FRESH amount=" + Amount + " uid=" + Uid + " skin=" + Value.skin +
                        " condition=" + Value.condition + " max_condition=" + Value.maxCondition + " fuel=" + FuelAmount +
                        " contents=false instance_data=false held=false");
                    // UNKNOWN protects a throwing host Drop. A normal return must
                    // prove both exact reciprocal references before cleanup authority.
                    BarrelResearchOwnership(Value, true);
                    BaseEntity Entity = Value.Drop(BarrelResearchPosition, Vector3.zero);
                    BarrelResearchHold(Entity);
                    WorldItem World = Entity as WorldItem;
                    BarrelResearchCheck(World != null && !World.IsDestroyed && World.IsFullySpawned() && World.net != null &&
                        ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) && Value.uid.Value == Uid &&
                        ReferenceEquals(World.item, Value) && ReferenceEquals(Value.GetWorldEntity(), World) && Value.parent == null &&
                        Value.amount == Amount && ReferenceEquals(Value.info, Definition), "D5 actual exact reciprocal fully spawned wood world");
                    BarrelResearchOwnership(Value, false);
                    BarrelResearchCheck(World.GetType() == typeof(DroppedItem) &&
                        (WorldPrefab == null || World.PrefabName == WorldPrefab), "D5 ordinary identical generic dropped world type/prefab for all amounts");
                    WorldPrefab = World.PrefabName;
                    var WorldComponents = World.GetComponents<Component>();
                    BarrelResearchCheck(WorldComponents != null && WorldComponents.Length <= 16, "D5 bounded actual world root components");
                    foreach (var Component in WorldComponents) {
                        BarrelResearchCheck(Component != null, "D5 no missing world root component");
                        Puts("[CarbonLuau:LootFixture] D5_WORLD_COMPONENT amount=" + Amount + " type=" + Component.GetType().FullName);
                    }
                    var Witness = EntityLifetimes.CaptureCompletedWitness(World);
                    BarrelResearchCheck(Witness != null && ReferenceEquals(Witness.Target, World) &&
                        EntityLifetimes.IsWitnessEpochUnchanged(Witness), "D5 completed Spawn observer authority");
                    ulong Id = World.net.ID.Value;
                    Puts("[CarbonLuau:LootFixture] D5_PROBE_WORLD amount=" + Amount + " uid=" + Uid + " id=" + Id +
                        " prefab=" + World.PrefabName + " epoch=" + Witness.Epoch + " fully_spawned=true reciprocal=true");
                    BarrelResearchCleanup();
                    BarrelResearchCheck(BarrelResearchLedgerCount == 0 &&
                        !ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Id)), World) &&
                        (Value.uid.Value == 0 || Value.uid.Value == Uid && Value.IsRemoved()), "D5 exact original world and Item cleanup converged");
                    Puts("[CarbonLuau:LootFixture] D5_PROBE_CLEAN amount=" + Amount + " id=" + Id + " ledger=0 original_removed=true");
                }
            } finally { BarrelResearchCleanup(); }
            LootFixtureItemProbeDone = true;
        }
        partial void RunLootItemCases()
        {
            string Prefab = LootFixtureStep == 24 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab;
            {
                LootFixtureEntries = new[] { new LootDropEntry("wood", 1, 1), new LootDropEntry("scrap", 2, 2),
                    new LootDropEntry("metal.fragments", 3, 3), new LootDropEntry("wood", 0, 0) };
                LootFixtureSelectProfile(Prefab);
                LootFixturePayout("d5_minimum_zero_mixed", 3, true);
                LootFixtureEntries = new[] { new LootDropEntry("wood", 0, 0) };
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D5 all-zero wood descriptor");
                LootFixturePayout("d5_all_zero_wood", 0, true);
                LootFixtureEntries = new[] { new LootDropEntry("wood", 50, 100), new LootDropEntry("metal.fragments", 10, 25) };
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D5 range on selected exact profile");
                LootFixturePayout("public_range", 2, true);
                LootFixtureEntries = new[] { new LootDropEntry("wood", 1000, 1000) };
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D5 maximum wood on selected profile");
                LootFixturePayout("d5_maximum_wood", 1, true);
                ItemDefinition Definition = ItemManager.FindItemDefinition("wood");
                BarrelResearchCheck(Definition != null && Definition.stackable == 1000, "D5 exact stack premise before controlled interference");
                var Case = BarrelResearchCreate("d5_stack999_precommit", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                try {
                    Definition.stackable = 999;
                    Puts("[CarbonLuau:LootFixture] D5_CONTROL exact wood stackable=999 planned_max=1000 I12_premise_change=true");
                    LootFixtureVanilla(Case);
                } finally { Definition.stackable = 1000; BarrelResearchCleanup(); }
                var Old = Gameplay.Active;
                LootFixtureEntries = new[] { new LootDropEntry("wood", 1001, 1001) };
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old) &&
                        Old.PublishedLootRule(Prefab).Prefab == Prefab && Old.PublishedLootRule(Prefab).Entry(0).Max == 1000,
                        "D5 invalid wood 1001 declaration preserves exact old owner and descriptor before COMMIT");
                } finally { LootFixtureEntries = new[] { new LootDropEntry("wood", 1000, 1000) }; }
                LootFixtureEntries = new[] { new LootDropEntry("sulfur", 1, 1) };
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old) &&
                        Old.PublishedLootRule(Prefab).Prefab == Prefab && Old.PublishedLootRule(Prefab).Entry(0).Item == "wood",
                        "D5 unsupported sulfur declaration preserves exact old wood owner and descriptor");
                } finally { LootFixtureEntries = new[] { new LootDropEntry("wood", 1000, 1000) }; }
                LootFixtureEntries = new LootDropEntry[LootRulePolicy.Entries];
                for (int Index = 0; Index < LootFixtureEntries.Length; Index++) LootFixtureEntries[Index] = new LootDropEntry("wood", 1000, 1000);
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D5 eight duplicate wood entries");
                LootFixturePayout("d5_duplicate_eight_maximum", 8, true);
            }
            LootFixtureEntries = TwoLootEntries(); LootFixtureSelectProfile(Prefab);
            if (LootFixtureStep == 25) {
                // Publication creates a fresh state module/listener owner. Prove
                // an actual death under that final owner before B2 checks its
                // public callback; retired owners' history is never copied/replayed.
                LootFixturePayout("d5_restored_public_owner", 2, true);
                Puts("[CarbonLuau:LootFixture] PASS D5 wood min1 zero range50..100 max1000 duplicate8 mixed scrap/metal both profiles; one global rule unchanged");
            }
        }
    }
}
