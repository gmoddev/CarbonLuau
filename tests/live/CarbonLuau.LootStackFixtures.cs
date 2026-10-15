// TEST ONLY. Small old-source PREPARE/COMMIT gap diagnostic; no engine interception.
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private readonly string[] LootStackNames = { "sulfur", "gears", "antiradpills", "lowgradefuel", "hat.bunnyhat", "wood" };
        private readonly int[] LootStackIds = { -1581843485, 479143914, -1432674913, -946369541, 23391694, -151838493 };
        private readonly int[] LootStackVanilla = { 1000, 20, 10, 500, 1, 1000 };
        private int LootStackIndex, LootStackGapFailures, LootStackGapVanilla;
        private bool LootStackMutate, LootStackObserved, LootStackCommitted, LootStackSuccess;
        private int LootStackCreated, LootStackAccepted;
        private ItemDefinition LootStackDefinition;
        private int LootStackOriginal;
        private Harmony LootStackPatches;
        private MethodInfo LootStackEnter;
        private bool LootStackGapMode = true;
        private int LootStackPart;
        partial void SelectLootStackMode(ref bool GapOnly, ref int Part);
        partial void RunLootStackMatrix();
        partial void RunLootStackSweep();
        partial void CleanupLootStackMatrix();
        partial void ObserveLootStackMatrixFrame(LootPayoutFrame Frame, bool Success);
        partial void ObserveLootStackMatrixWorld(BaseEntity World, Item Value);
        partial void SelectLootStackItems(ref bool Items)
        { Items = true; SelectLootStackMode(ref LootStackGapMode, ref LootStackPart); }
        partial void GetLootStackBudget(ref int Needed)
        { if (LootFixtureStackItems && LootFixtureStep >= 62) Needed = 4; }
        private static void LootStackPrepared(CarbonLuau __instance, LootPayoutFrame __result)
        {
            if (__instance == null || !__instance.LootStackMutate || __result == null) return;
            __instance.LootStackMutate = false;
            BarrelResearchCheck(__result.Rule.Count == 1 && __result.Amounts[0] == 2 &&
                ReferenceEquals(__result.Definitions[0], __instance.LootStackDefinition) && !__result.Committed && __result.Created == 0,
                "D8 stack mutation after exact PREPARE and before COMMIT");
            __instance.LootStackDefinition.stackable = 1;
            __instance.Puts("[CarbonLuau:LootFixture] D8_STACK_MUTATION point=after_prepare item=" +
                __instance.LootStackDefinition.shortname + " old=2 new=1 planned=2 committed=false created=0");
        }
        partial void ObserveLootStackFrame(LootPayoutFrame Frame, bool Success)
        {
            ObserveLootStackMatrixFrame(Frame, Success);
            if (!LootFixtureStackItems || LootStackDefinition == null) return;
            LootStackObserved = true; LootStackCommitted = Frame.Committed; LootStackSuccess = Success;
            LootStackCreated = Frame.Created; LootStackAccepted = 0;
            for (int Index = 0; Index < Frame.Rule.Count; Index++) if (Frame.Accepted[Index]) LootStackAccepted++;
        }
        partial void ObserveLootStackWorld(BaseEntity World, Item Value) { ObserveLootStackMatrixWorld(World, Value); }
        partial void CleanupLootStack()
        {
            CleanupLootStackMatrix();
            LootStackMutate = false;
            if (LootStackDefinition != null) { LootStackDefinition.stackable = LootStackOriginal; LootStackDefinition = null; }
            if (LootStackPatches != null && LootStackEnter != null)
                LootStackPatches.Unpatch(LootStackEnter, HarmonyPatchType.All, LootStackPatches.Id);
        }
        partial void RunLootStackStep()
        {
            if (!LootStackGapMode && LootStackPart == 3) { RunLootStackSweep(); return; }
            if (!LootStackGapMode) { RunLootStackMatrix(); return; }
            if (LootStackEnter == null) {
                LootStackEnter = typeof(CarbonLuau).GetMethod("EnterLoot", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(LootContainer) }, null);
                BarrelResearchCheck(LootStackEnter != null && LootStackEnter.ReturnType == typeof(LootPayoutFrame),
                    "D8 exact test-only private admission postfix target");
                LootStackPatches = new Harmony("CarbonLuau.Test.StackGap." + Guid.NewGuid().ToString("N"));
                LootStackPatches.Patch(LootStackEnter, postfix: new HarmonyMethod(typeof(CarbonLuau).GetMethod(
                    "LootStackPrepared", BindingFlags.Static | BindingFlags.NonPublic)));
                Puts("[CarbonLuau:LootFixture] D8_STACK_BEGIN mode=gap_probe representatives=6 profiles=2 production_engine_hooks_added=0 world_cap=8 amount_cap=1000");
            }
            int Spec = LootStackIndex / 2;
            string Prefab = LootStackIndex % 2 == 0 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab;
            LootStackDefinition = ItemManager.FindItemDefinition(LootStackNames[Spec]);
            BarrelResearchCheck(LootStackDefinition != null && LootStackDefinition.itemid == LootStackIds[Spec] &&
                LootStackDefinition.stackable == LootStackVanilla[Spec], "D8 representative original name/ID/vanilla stack");
            LootStackOriginal = LootStackDefinition.stackable;
            try {
                LootStackDefinition.stackable = 2;
                LootFixturePrefab = Prefab; BarrelResearchSelectedPrefab = Prefab;
                LootFixtureEntries = new[] { new LootDropEntry(LootStackNames[Spec], 2, 2) };
                LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 stack exact public rule publication");
                var Rule = LootRegistryRule(Prefab, Gameplay.Active);
                var Case = BarrelResearchCreate("d8_stack_gap_" + LootStackNames[Spec], BarrelResearchMode.Vanilla,
                    BarrelResearchPosition, true, 0, Prefab);
                BarrelResearchCurrent = Case;
                int Receipts = LootFixtureReceipts, Successes = LootFixtureSuccesses;
                long Selected = LootSelected, Partial = LootPartial;
                LootStackObserved = false; LootStackMutate = true;
                Case.Source.Hurt(10000f);
                BarrelResearchRetired(Case);
                BarrelResearchCheck(!LootStackMutate && LootStackObserved && LootFixtureReceipts == Receipts + 1 &&
                    LootFixtureSuccesses == Successes && LootSelected == Selected + 1 && LootCurrent == null &&
                    !LootStackSuccess && LootStackCreated == 0 && LootStackAccepted == 0 &&
                    ReferenceEquals(LootRegistryRule(Prefab, Gameplay.Active), Rule),
                    "D8 admitted occurrence one completion, original publication, no allocation or accepted world");
                if (LootStackCommitted) {
                    BarrelResearchCheck(LootPartial == Partial + 1, "D8 old source committed partial counterexample");
                    for (int Index = 0; Index < Case.StoredCount; Index++)
                        BarrelResearchCheck(Case.Stored[Index].IsRemoved() && Case.Stored[Index].GetWorldEntity() == null,
                            "D8 old source suppressed exact stored vanilla payout");
                    LootStackGapFailures++;
                } else {
                    BarrelResearchCheck(LootPartial == Partial, "D8 pre-COMMIT abort does not become partial");
                    for (int Index = 0; Index < Case.StoredCount; Index++) {
                        Item Value = Case.Stored[Index]; WorldItem World = Value.GetWorldEntity() as WorldItem;
                        BarrelResearchHold(World);
                        BarrelResearchCheck(Value.uid.Value == Case.StoredUids[Index] && Value.amount == Case.StoredAmounts[Index] &&
                            Value.parent == null && World != null && ReferenceEquals(World.item, Value) && World.IsFullySpawned() &&
                            !World.IsDestroyed && World.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                            EntityLifetimes.CaptureCompletedWitness(World) != null, "D8 admitted abort exact stored ItemUID vanilla worlds");
                    }
                    LootStackGapVanilla++;
                }
                Puts("[CarbonLuau:LootFixture] D8_STACK_GAP item=" + LootStackNames[Spec] + " id=" + LootStackIds[Spec] +
                    " prefab=" + Prefab + " result=" + (LootStackCommitted ? "FAIL" : "PASS") +
                    " committed=" + LootStackCommitted + " created=0 accepted=0 selected_delta=1 partial_delta=" +
                    (LootPartial - Partial) + " original_stored=" + Case.StoredCount + " vanilla_worlds=" +
                    (LootStackCommitted ? 0 : Case.StoredCount) + " reason=" +
                    (LootStackCommitted ? "stack_drop_after_prepare_suppresses_vanilla" : "stack_drop_before_commit_preserves_vanilla"));
            } finally {
                LootStackMutate = false; LootStackDefinition.stackable = LootStackOriginal; LootStackDefinition = null;
                BarrelResearchCleanup();
                BarrelResearchCheck(BarrelResearchLedgerCount == 0, "D8 stack gap exact cleanup zero");
            }
            Puts("[CarbonLuau:LootFixture] D8_STACK_CLEANUP item=" + LootStackNames[Spec] + " prefab=" + Prefab + " ledger=0");
            if (++LootStackIndex == 12) {
                LootFixtureEntries = TwoLootEntries(); LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK,
                    "D8 gap restores original two-entry public descriptor before unchanged B2 assertions");
                LootRegistryPayout("d8_stack_gap_b2_handoff", LootFixturePrefab, 2, true);
                LootFixtureStackDone = true;
                Puts("[CarbonLuau:LootFixture] D8_STACK_BATCH mode=gap_probe completed=true cases=12 pass=" +
                    LootStackGapVanilla + " fail=" + LootStackGapFailures + " cleanup=0 all_pass=" + (LootStackGapFailures == 0));
            }
        }
    }
}
