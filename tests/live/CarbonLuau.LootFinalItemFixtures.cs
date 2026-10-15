// TEST ONLY. Final-source per-item public sweep supplements the full D8 matrices.
using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private sealed class LootStackSweepSpec
        {
            internal readonly string Name, Family;
            internal readonly int Id, Stack;
            internal LootStackSweepSpec(string Name, int Id, int Stack, string Family)
            { this.Name = Name; this.Id = Id; this.Stack = Stack; this.Family = Family; }
        }
        private readonly List<LootStackSweepSpec> LootStackSweepSpecs = new List<LootStackSweepSpec>();
        private int LootStackSweepIndex;
        private FacadeSession LootStackSweepOwner;
        partial void PopulateLootStackSweepSpecs(List<LootStackSweepSpec> Specs);

        partial void RunLootStackSweep()
        {
            if (LootStackSweepSpecs.Count == 0) {
                PopulateLootStackSweepSpecs(LootStackSweepSpecs);
                BarrelResearchCheck(LootStackSweepSpecs.Count == 221 && LootStackSweepSpecs.Count <= 512 &&
                    Addons.Count == 0 && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D8 final-source finite manifest and isolated owner");
                string Previous = null;
                var Ids = new HashSet<int>();
                foreach (var Spec in LootStackSweepSpecs) {
                    ItemDefinition Definition = ItemManager.FindItemDefinition(Spec.Name);
                    BarrelResearchCheck(Spec.Name != null && Spec.Name.Length <= 15 &&
                        (Previous == null || StringComparer.Ordinal.Compare(Previous, Spec.Name) < 0) &&
                        Ids.Add(Spec.Id) && Spec.Stack > 0 && Definition != null &&
                        Definition.itemid == Spec.Id && Definition.stackable == Spec.Stack,
                        "D8 final-source exact ordinal Name/ID/vanilla stack snapshot");
                    Previous = Spec.Name;
                }
                Puts("[CarbonLuau:LootFixture] D8_STACK_SWEEP_BEGIN manifest=221 profiles=2 cases=442 world_cap=8 amount_cap=1000");
            }
            int SpecIndex = LootStackSweepIndex / 2;
            var Current = LootStackSweepSpecs[SpecIndex];
            string Prefab = LootStackSweepIndex % 2 == 0 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab;
            int Maximum = Math.Min(Current.Stack, LootRulePolicy.MaximumAmount);
            ItemDefinition ExpectedDefinition = ItemManager.FindItemDefinition(Current.Name);
            BarrelResearchCheck(ExpectedDefinition != null && ExpectedDefinition.itemid == Current.Id &&
                ExpectedDefinition.stackable == Current.Stack, "D8 final-source definition premises remain exact");
            if (LootStackSweepIndex % 2 == 0) {
                LootFixturePrefab = LootRulePolicy.Prefab; LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab;
                LootFixtureEntries = new[] { new LootDropEntry(Current.Name, 1, 1),
                    new LootDropEntry(Current.Name, Maximum, Maximum) };
                LootFixtureSecondEntries = new[] { new LootDropEntry(Current.Name, 1, 1),
                    new LootDropEntry(Current.Name, Maximum, Maximum) };
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK,
                    "D8 final-source actual Luau atomic whole-owner pair publication");
                LootStackSweepOwner = Gameplay.Active;
                LootRegistryRule(LootRulePolicy.Prefab, LootStackSweepOwner);
                LootRegistryRule(LootRulePolicy.SecondPrefab, LootStackSweepOwner);
            }
            LootDropRule Rule = LootRegistryRule(Prefab, LootStackSweepOwner);
            BarrelResearchSelectedPrefab = Prefab;
            var Case = BarrelResearchCreate("d8_final_source_" + Current.Name,
                BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Prefab);
            long Selected = LootSelected, Verified = LootVerified, Partial = LootPartial, CleanupFailed = LootCleanupFailed;
            try {
                LootFixturePayout(Case, 2, true, false);
                BarrelResearchCheck(LootSelected == Selected + 1 && LootVerified == Verified + 1 &&
                    LootPartial == Partial && LootCleanupFailed == CleanupFailed && LootCurrent == null &&
                    ReferenceEquals(Gameplay.Active, LootStackSweepOwner),
                    "D8 final-source single selected/verified occurrence without partial or cleanup failure");
                LootRegistryRule(Prefab, LootStackSweepOwner, Rule);
                int Worlds = 0;
                var Uids = new HashSet<ulong>(); var WorldIds = new HashSet<ulong>();
                for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                    var Resource = BarrelResearchLedger[Index];
                    Item Value = Resource.Item;
                    if (Value == null || Value.IsRemoved() || Value.GetWorldEntity() == null) continue;
                    WorldItem World = Value.GetWorldEntity() as WorldItem;
                    int ExpectedAmount = Worlds == 0 ? 1 : Maximum;
                    BarrelResearchCheck(Worlds < 2 && Value.IsValid() && Value.uid.Value != 0 &&
                        Value.uid.Value == Resource.ItemUid && !Resource.OwnershipUnknown && Uids.Add(Value.uid.Value) &&
                        ReferenceEquals(Value.info, ExpectedDefinition) && Value.amount == ExpectedAmount && Value.parent == null &&
                        World != null && !World.IsDestroyed && World.IsFullySpawned() && World.net != null &&
                        World.net.ID.Value != 0 && WorldIds.Add(World.net.ID.Value) && ReferenceEquals(World.item, Value) &&
                        ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                        World.PrefabName == "assets/prefabs/misc/burlap sack/generic_world.prefab",
                        "D8 final-source exact original Item/UID/amount/world reciprocal owner");
                    var Witness = EntityLifetimes.CaptureCompletedWitness(World);
                    BarrelResearchCheck(Witness != null && EntityLifetimes.IsWitnessEpochUnchanged(Witness),
                        "D8 final-source original completed Spawn epoch");
                    Puts("[CarbonLuau:LootFixture] D8_STACK_SWEEP_WORLD name=" + Current.Name + " id=" + Current.Id +
                        " prefab=" + Prefab + " entry=" + Worlds + " amount=" + Value.amount + " uid=" + Value.uid.Value +
                        " world=" + World.net.ID.Value + " epoch=" + Witness.Epoch + " reciprocal=true completed_spawn=true");
                    Worlds++;
                }
                BarrelResearchCheck(Worlds == 2, "D8 final-source exactly two original worlds");
                for (int Index = 0; Index < Case.StoredCount; Index++)
                    BarrelResearchCheck(Case.Stored[Index].IsRemoved() && Case.Stored[Index].GetWorldEntity() == null,
                        "D8 final-source stored vanilla payouts remain suppressed");
            } finally { BarrelResearchCleanup(); }
            BarrelResearchCheck(BarrelResearchLedgerCount == 0 && LootCurrent == null &&
                LootCleanupFailed == CleanupFailed, "D8 final-source exact Item/world cleanup convergence");
            Puts("[CarbonLuau:LootFixture] D8_STACK_SWEEP name=" + Current.Name + " id=" + Current.Id +
                " stackable=" + Current.Stack + " family=" + Current.Family + " prefab=" + Prefab +
                " minimum=1 maximum=" + Maximum + " result=PASS worlds=2 cleanup=0 original_uid_world_verified=true completed_spawn=true");
            if (++LootStackSweepIndex == LootStackSweepSpecs.Count * 2) {
                LootFixturePrefab = LootRulePolicy.SecondPrefab; BarrelResearchSelectedPrefab = LootFixturePrefab;
                LootFixtureEntries = TwoLootEntries(); LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Addons.Count == 0 &&
                    LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D8 final-source sweep restores unchanged two-world B2 handoff");
                // Reload owns a fresh state module. Deliver an actual death in
                // that domain before B2 checks its unchanged callback assertion.
                LootRegistryPayout("d8_final_source_b2_handoff", LootRulePolicy.SecondPrefab, 2, true);
                LootStackSweepOwner = null;
                LootFixtureStackDone = true;
                Puts("[CarbonLuau:LootFixture] D8_STACK_SWEEP_BATCH completed=true definitions=221 cases=442 pass=442 fail=0 cleanup=0 all_pass=True");
            }
        }
    }
}
