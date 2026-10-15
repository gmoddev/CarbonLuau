// TEST ONLY. Two bounded three-representative matrices, separate genuine Carbon hook plugins.
using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        [PluginReference] private Plugin CarbonLuauStackProbeA, CarbonLuauStackProbeB;
        private int LootStackMatrixIndex, LootStackPhase, LootStackCases;
        private bool LootStackMatrixActive, LootStackAfterFirst;
        private string LootStackName;
        private int LootStackMaxMode, LootStackMaxValue, LootStackCanMode, LootStackSplitMode, LootStackDropMode;
        private int LootStackSecondMaxMode, LootStackSecondMaxValue;
        private ItemDefinition LootStackMatrixDefinition;
        private int LootStackMatrixOriginal;
        private bool LootStackTerminal, LootStackTerminalObserved;
        private Item LootStackTerminalItem;
        private BaseEntity LootStackTerminalWorld;
        private ulong LootStackTerminalUid;
        partial void SkipLootStackFrame(LootPayoutFrame Frame, bool Success, ref bool Skip)
        {
            if (!LootStackTerminal) return;
            Skip = true;
            BarrelResearchCheck(!Success && Frame.Committed && Frame.Created == 1 && !Frame.Accepted[0] &&
                Frame.Items[0] != null && Frame.Uids[0] != 0 && Frame.Items[0].uid.Value == 0 && !LootWorldOwned(Frame, 0),
                "D8 terminal callback changes actual captured UID and refuses original cleanup authority");
            LootStackTerminalObserved = true; LootStackTerminalItem = Frame.Items[0];
            LootStackTerminalWorld = Frame.Worlds[0]; LootStackTerminalUid = Frame.Uids[0];
        }
        private void LootStackConfigure(Plugin Probe, int MaximumMode = 0, int MaximumValue = 0,
            int CanMode = 0, int SplitMode = 0, int DropMode = 0)
        {
            BarrelResearchCheck(Probe != null && (string)Probe.Call("D8StackConfigure", LootStackName,
                MaximumMode, MaximumValue, CanMode, SplitMode, DropMode) == "OK", "D8 genuine hook plugin bounded private configuration");
        }
        private int[] LootStackCounts(Plugin Probe)
        {
            var Values = Probe.Call("D8StackState") as object[];
            BarrelResearchCheck(Values != null && Values.Length == 4, "D8 hook count receipt shape");
            var Result = new int[4];
            for (int Index = 0; Index < 4; Index++) {
                BarrelResearchCheck(Values[Index] is int && (int)Values[Index] >= 0 && (int)Values[Index] < 10000,
                    "D8 bounded genuine callback count"); Result[Index] = (int)Values[Index];
            }
            return Result;
        }
        private Item LootStackTake(Plugin Probe)
        {
            var Values = Probe.Call("D8StackTake") as object[];
            BarrelResearchCheck(Values != null && Values.Length == 4 && Values[2] == null && Values[3] == null,
                "D8 ordinary packet never changes UID");
            Item Child = Values[0] as Item; BaseEntity World = Values[1] as BaseEntity;
            if (Child == null) { BarrelResearchCheck(World == null, "D8 no unclaimed callback world"); return null; }
            BarrelResearchCheck(Child.uid.Value != 0 && ReferenceEquals(Child.info, LootStackMatrixDefinition) &&
                Child.amount == 1 && Child.parent == null, "D8 exact private returned child factory identity");
            if (World != null) BarrelResearchCheck(ReferenceEquals(Child.GetWorldEntity(), World) &&
                World is WorldItem && ReferenceEquals(((WorldItem)World).item, Child) && World.IsFullySpawned() &&
                !World.IsDestroyed && World.net != null && EntityLifetimes.CaptureCompletedWitness(World) != null,
                "D8 callback split exact distinct child completed world");
            BarrelResearchHold(Child, true); BarrelResearchHold(World);
            Puts("[CarbonLuau:LootFixture] D8_STACK_CHILD item=" + LootStackName + " uid=" + Child.uid.Value +
                " amount=1 world=" + (World == null || World.net == null ? 0 : World.net.ID.Value) + " reciprocal=" + (World != null));
            return Child;
        }
        partial void ObserveLootStackMatrixFrame(LootPayoutFrame Frame, bool Success)
        {
            if (!LootStackMatrixActive) return;
            LootStackTake(CarbonLuauStackProbeA); LootStackTake(CarbonLuauStackProbeB);
            if (LootStackPhase == 15) {
                BarrelResearchCheck(!Success && Frame.Committed && Frame.Created == 1 && Frame.Items[0] != null &&
                    Frame.Items[0].amount == 2 && Frame.Accepted[0] && !Frame.Accepted[1] && Frame.Items[1] == null &&
                    LootStackMatrixDefinition.stackable == 1 && LootWorldOwned(Frame, 0),
                    "D8 post-COMMIT original first world quantity two remains owned above new stack one; second never created");
                Puts("[CarbonLuau:LootFixture] D8_STACK_POSTCOMMIT_WORLD item=" + LootStackName + " prefab=" + Frame.Rule.Prefab +
                    " uid=" + Frame.Uids[0] + " world=" + Frame.Worlds[0].net.ID.Value +
                    " amount=2 live_definition_stack=1 accepted=true reciprocal=true completed=true second_created=false");
            }
        }
        partial void ObserveLootStackMatrixWorld(BaseEntity World, Item Value)
        {
            if (LootStackTerminal) { World.enableSaving = false; return; }
            if (LootStackMatrixActive && LootStackAfterFirst && LootCurrent != null && LootCurrent.Index == 0) {
                BarrelResearchCheck(Value.amount == 2 && ReferenceEquals(Value.info, LootStackMatrixDefinition),
                    "D8 first captured original Item before second create");
                LootStackMatrixDefinition.stackable = 1; LootStackAfterFirst = false;
                Puts("[CarbonLuau:LootFixture] D8_STACK_MUTATION point=after_first_capture item=" + LootStackName + " new=1 first_planned=2 second_planned=2");
            }
        }
        partial void CleanupLootStackMatrix()
        {
            LootStackAfterFirst = false; LootStackMatrixActive = false;
            if (LootStackMatrixDefinition != null) {
                LootStackMatrixDefinition.stackable = LootStackMatrixOriginal; LootStackMatrixDefinition = null;
            }
            if (CarbonLuauStackProbeA != null) {
                LootStackTake(CarbonLuauStackProbeA); LootStackConfigure(CarbonLuauStackProbeA);
            }
            if (CarbonLuauStackProbeB != null) {
                LootStackTake(CarbonLuauStackProbeB); LootStackConfigure(CarbonLuauStackProbeB);
            }
        }
        private void LootStackRule(string Prefab, params LootDropEntry[] Entries)
        {
            LootFixturePrefab = Prefab; BarrelResearchSelectedPrefab = Prefab;
            LootFixtureEntries = Entries; LootFixtureSecondEntries = null;
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 actual public stack declaration");
            LootRegistryRule(Prefab, Gameplay.Active);
        }
        private void LootStackPublic(string Prefab, string Label, int Amount, int Worlds = 1, bool Success = true)
        {
            LootStackConfigure(CarbonLuauStackProbeA, LootStackMaxMode, LootStackMaxValue,
                LootStackCanMode, LootStackSplitMode, LootStackDropMode);
            LootStackConfigure(CarbonLuauStackProbeB, LootStackSecondMaxMode, LootStackSecondMaxValue);
            LootStackRule(Prefab, new LootDropEntry(LootStackName, Amount, Amount));
            int[] BeforeA = LootStackCounts(CarbonLuauStackProbeA), BeforeB = LootStackCounts(CarbonLuauStackProbeB);
            LootRegistryPayout("d8_stack_" + Label + "_" + LootStackName, Prefab, Amount == 0 ? 0 : Worlds, Success);
            int[] AfterA = LootStackCounts(CarbonLuauStackProbeA), AfterB = LootStackCounts(CarbonLuauStackProbeB);
            BarrelResearchCheck(AfterA[0] == BeforeA[0] && AfterA[1] == BeforeA[1] &&
                AfterB[0] == BeforeB[0] && AfterB[1] == BeforeB[1] &&
                (LootStackDropMode == 3 ? AfterA[2] == BeforeA[2] + 1 : AfterA[2] == BeforeA[2]) &&
                AfterB[2] == BeforeB[2] + (LootStackDropMode == 3 ? 1 : 0) && AfterA[3] == BeforeA[3] + (LootStackDropMode == 3 ? 2 : 1) &&
                AfterB[3] == BeforeB[3] + (LootStackDropMode == 3 ? 2 : 1),
                "D8 causal Create/Drop hook counts exclude diagnostic methods");
            Puts("[CarbonLuau:LootFixture] D8_STACK_PUBLIC item=" + LootStackName + " case=" + Label + " prefab=" + Prefab +
                " planned=" + Amount + " worlds=" + Worlds + " success=" + Success + " definition_stack=" +
                LootStackMatrixDefinition.stackable + " max_calls=0 can_calls=0 split_calls=" +
                (AfterA[2] - BeforeA[2]) + " drop_calls=" + (AfterA[3] - BeforeA[3]) + " diagnostic_calls_excluded=true cleanup=0");
        }
        private Item LootStackFresh(int Amount)
        {
            Item Value = ItemManager.Create(LootStackMatrixDefinition, Amount, 0);
            BarrelResearchCheck(Value != null && Value.uid.Value != 0 && Value.amount == Amount &&
                ReferenceEquals(Value.info, LootStackMatrixDefinition) && Value.parent == null && Value.GetWorldEntity() == null,
                "D8 diagnostic exact new original Item");
            BarrelResearchHold(Value, true); return Value;
        }
        private int LootStackMaximum(Item Value, int Expected, string Label)
        {
            int Actual = Value.MaxStackable();
            BarrelResearchCheck(Actual == Expected, "D8 exact effective diagnostic maximum " + Label);
            Puts("[CarbonLuau:LootFixture] D8_STACK_EFFECTIVE item=" + LootStackName + " case=" + Label + " actual=" + Actual +
                " definition_stack=" + Value.info.stackable + " parent=" + (Value.parent != null));
            return Actual;
        }
        private void LootStackDiagnosticMaximum(int Mode, int Number, int Expected, string Label)
        {
            try {
                LootStackConfigure(CarbonLuauStackProbeA, Mode, Number); LootStackConfigure(CarbonLuauStackProbeB);
                Item Value = LootStackFresh(1); LootStackMaximum(Value, Expected, Label);
                BarrelResearchCheck(LootStackCounts(CarbonLuauStackProbeA)[0] == 1 && LootStackCounts(CarbonLuauStackProbeB)[0] == 1,
                    "D8 both genuine hook subscriptions observed for diagnostic maximum");
            } finally { BarrelResearchCleanup(); }
        }
        private void LootStackContainers()
        {
            try {
                LootStackConfigure(CarbonLuauStackProbeA); LootStackConfigure(CarbonLuauStackProbeB);
                var Case = BarrelResearchCreate("d8_stack_container_" + LootStackName, BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                Item Value = LootStackFresh(10);
                ulong OriginalUid = Value.uid.Value;
                BarrelResearchCheck(Case.Container.capacity > 2 && Case.Container.GetSlot(2) == null,
                    "D8 deterministic seeded source leaves exact diagnostic slot two empty");
                BarrelResearchOwnership(Value, true);
                bool Moved = Value.MoveToContainer(Case.Container, 2, false, false, null, false);
                BarrelResearchOwnership(Value, false);
                Puts("[CarbonLuau:LootFixture] D8_STACK_CONTAINER_MOVE item=" + LootStackName + " moved=" + Moved +
                    " uid=" + Value.uid.Value + " original_uid=" + OriginalUid + " slot=2 parent_exact=" +
                    ReferenceEquals(Value.parent, Case.Container) + " slot_exact=" + ReferenceEquals(Case.Container.GetSlot(2), Value));
                BarrelResearchCheck(Moved && Value.uid.Value == OriginalUid && ReferenceEquals(Value.parent, Case.Container) &&
                    ReferenceEquals(Case.Container.GetSlot(2), Value), "D8 genuine original container parent and slot");
                BarrelResearchCheck(Value.MaxStackable() >= 10, "D8 original container permits the diagnostic quantity ten");
                Case.Container.maxStackSize = 3; Case.Container.allowItemsToIncreaseToMaxStackSize = false;
                LootStackMaximum(Value, 3, "container_lower_no_increase");
                Case.Container.allowItemsToIncreaseToMaxStackSize = true;
                LootStackMaximum(Value, 10, "container_lower_allow_increase");
                Case.Container.maxStackSize = 20; Case.Container.allowItemsToIncreaseToMaxStackSize = false;
                LootStackMaximum(Value, 10, "container_higher_no_increase");
                Case.Container.allowItemsToIncreaseToMaxStackSize = true;
                LootStackMaximum(Value, 20, "container_higher_allow_increase");
                LootStackConfigure(CarbonLuauStackProbeA, 1, 7);
                LootStackMaximum(Value, 7, "hook_after_container");
                int[] BeforeA = LootStackCounts(CarbonLuauStackProbeA), BeforeB = LootStackCounts(CarbonLuauStackProbeB);
                BarrelResearchOwnership(Value, true);
                BaseEntity Entity = Value.Drop(BarrelResearchPosition, Vector3.zero);
                BarrelResearchHold(Entity);
                WorldItem World = Entity as WorldItem;
                BarrelResearchCheck(World != null && World.GetType() == typeof(DroppedItem) && World.IsFullySpawned() &&
                    !World.IsDestroyed && World.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                    Value.uid.Value == OriginalUid && Value.amount == 10 && ReferenceEquals(Value.info, LootStackMatrixDefinition) &&
                    ReferenceEquals(World.item, Value) && ReferenceEquals(Value.GetWorldEntity(), World) && Value.parent == null &&
                    Case.Container.GetSlot(2) == null, "D8 parented Drop exact original ten Item/UID world without split or clamp");
                var Witness = EntityLifetimes.CaptureCompletedWitness(World);
                BarrelResearchCheck(Witness != null && EntityLifetimes.IsWitnessEpochUnchanged(Witness),
                    "D8 parented Drop completed Spawn authority");
                BarrelResearchOwnership(Value, false); World.enableSaving = false;
                int[] AfterA = LootStackCounts(CarbonLuauStackProbeA), AfterB = LootStackCounts(CarbonLuauStackProbeB);
                for (int Index = 0; Index < 4; Index++)
                    BarrelResearchCheck(AfterA[Index] - BeforeA[Index] == (Index == 3 ? 1 : 0) &&
                        AfterB[Index] - BeforeB[Index] == (Index == 3 ? 1 : 0), "D8 parented physical Drop causal callback counts");
                Puts("[CarbonLuau:LootFixture] D8_STACK_CONTAINER_DROP item=" + LootStackName + " uid=" + Value.uid.Value +
                    " original_uid=" + OriginalUid + " amount=10 effective_before=7 parent_removed=true world=" + World.net.ID.Value +
                    " epoch=" + Witness.Epoch + " completed=true reciprocal=true max_calls=0 can_calls=0 split_calls=0 drop_calls=1 original_slot_empty=true");
            } catch (Exception Error) {
                string Message = Error.Message ?? ""; if (Message.Length > 512) Message = Message.Substring(0, 512);
                Puts("[CarbonLuau:LootFixture] D8_STACK_FIRST_CAUSE case=container_limits item=" + LootStackName +
                    " type=" + Error.GetType().Name + " message=" + Message.Replace('\r', ' ').Replace('\n', ' '));
                throw;
            } finally { BarrelResearchCleanup(); }
        }
        private void LootStackCanStack()
        {
            try {
                Item First = LootStackFresh(2), Second = LootStackFresh(2);
                for (int Mode = 0; Mode < 3; Mode++) {
                    LootStackConfigure(CarbonLuauStackProbeA, 0, 0, Mode); LootStackConfigure(CarbonLuauStackProbeB);
                    bool Actual = First.CanStack(Second);
                    BarrelResearchCheck(Actual == (Mode != 1) && LootStackCounts(CarbonLuauStackProbeA)[1] == 1 &&
                        LootStackCounts(CarbonLuauStackProbeB)[1] == 1, "D8 real CanStackItem neutral/false/true overrides");
                    Puts("[CarbonLuau:LootFixture] D8_STACK_CAN item=" + LootStackName + " mode=" + Mode + " actual=" + Actual +
                        " first_uid=" + First.uid.Value + " second_uid=" + Second.uid.Value);
                }
            } finally { BarrelResearchCleanup(); }
        }
        private void LootStackSplit(bool Override)
        {
            try {
                LootStackConfigure(CarbonLuauStackProbeA, 0, 0, 0, Override ? 1 : 0); LootStackConfigure(CarbonLuauStackProbeB);
                Item First = LootStackFresh(5); ulong OriginalUid = First.uid.Value;
                Item Child = First.SplitItem(1);
                BarrelResearchCheck(First.uid.Value == OriginalUid && First.amount == 4 && Child != null && Child.uid.Value != 0 &&
                    Child.uid.Value != OriginalUid && Child.amount == 1 && ReferenceEquals(Child.info, First.info),
                    "D8 split original versus clone UID and exact amounts");
                BarrelResearchCheck(LootStackCounts(CarbonLuauStackProbeA)[2] == 1 &&
                    LootStackCounts(CarbonLuauStackProbeB)[2] == 1, "D8 both genuine OnItemSplit subscriptions observed exactly once");
                if (Override) BarrelResearchCheck(ReferenceEquals(Child, LootStackTake(CarbonLuauStackProbeA)),
                    "D8 exact hook-returned split child is the actual SplitItem result");
                else BarrelResearchHold(Child, true);
                BaseEntity FirstWorld = First.Drop(BarrelResearchPosition, Vector3.zero);
                BaseEntity ChildWorld = Child.Drop(BarrelResearchPosition + new Vector3(0.1f, 0, 0), Vector3.zero);
                BarrelResearchHold(FirstWorld); BarrelResearchHold(ChildWorld);
                foreach (Item Value in new[] { First, Child }) {
                    var World = Value.GetWorldEntity() as WorldItem;
                    BarrelResearchCheck(World != null && ReferenceEquals(World.item, Value) && World.IsFullySpawned() &&
                        !World.IsDestroyed && World.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                        EntityLifetimes.CaptureCompletedWitness(World) != null, "D8 explicit split exact distinct completed reciprocal worlds");
                    World.enableSaving = false;
                }
                Puts("[CarbonLuau:LootFixture] D8_STACK_SPLIT item=" + LootStackName + " override=" + Override +
                    " original_uid=" + OriginalUid + " clone_uid=" + Child.uid.Value + " original_amount=4 clone_amount=1 original_world=" +
                    FirstWorld.net.ID.Value + " clone_world=" + ChildWorld.net.ID.Value +
                    " reciprocal=true completed=true a_split_calls=1 b_split_calls=1 hook_returned_exact=" + Override);
            } finally { BarrelResearchCleanup(); }
        }
        partial void RunLootStackMatrix()
        {
            if (LootStackPart == 2) { RunLootStackTerminal(); return; }
            BarrelResearchCheck(LootStackPart >= 0 && LootStackPart <= 1 && CarbonLuauStackProbeA != null && CarbonLuauStackProbeB != null,
                "D8 matrix explicit bounded part and both genuine separate hook plugins");
            int Spec = LootStackPart * 3 + LootStackMatrixIndex;
            LootStackName = LootStackNames[Spec]; LootStackMatrixDefinition = ItemManager.FindItemDefinition(LootStackName);
            BarrelResearchCheck(LootStackMatrixDefinition != null && LootStackMatrixDefinition.itemid == LootStackIds[Spec] &&
                LootStackMatrixDefinition.stackable == LootStackVanilla[Spec], "D8 exact six representative identity and original stack");
            LootStackMatrixOriginal = LootStackMatrixDefinition.stackable; LootStackMatrixActive = true;
            if (LootStackCases == 0) Puts("[CarbonLuau:LootFixture] D8_STACK_BEGIN mode=matrix part=" + LootStackPart +
                " representatives=3 phases=19 world_cap=8 amount_cap=1000 engine_hooks_added=0");
            LootStackMaxMode = LootStackMaxValue = LootStackCanMode = LootStackSplitMode = LootStackDropMode = 0;
            LootStackSecondMaxMode = LootStackSecondMaxValue = 0;
            string Label = "";
            try {
                if (LootStackPhase == 0) Label = "vanilla";
                else if (LootStackPhase == 1) { Label = "definition_increase"; LootStackMatrixDefinition.stackable = 2000; }
                else if (LootStackPhase == 2) { Label = "definition_decrease"; LootStackMatrixDefinition.stackable = 2; }
                else if (LootStackPhase == 3) {
                    Label = "definition_decrease_reject";
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                        LootStackMatrixDefinition.stackable = 3;
                        LootStackPublic(Prefab, "same_frame_prime", 1);
                        LootStackRule(Prefab, new LootDropEntry(LootStackName, 3, 3));
                        var Case = BarrelResearchCreate("d8_stack_decrease_reject", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Prefab);
                        LootStackMatrixDefinition.stackable = 2;
                        try { LootFixtureVanilla(Case); }
                        finally { BarrelResearchCleanup(); }
                        Puts("[CarbonLuau:LootFixture] D8_STACK_MUTATION point=before_prepare_decrease item=" + LootStackName +
                            " old=3 new=2 planned=3 vanilla_worlds=2 prefab=" + Prefab + " cleanup=0");
                    }
                } else if (LootStackPhase >= 4 && LootStackPhase <= 8) {
                    LootStackMatrixDefinition.stackable = 10; LootStackMaxMode = LootStackPhase == 8 ? 2 : 1;
                    LootStackMaxValue = LootStackPhase == 4 ? 20 : LootStackPhase == 5 ? 1 : LootStackPhase == 6 ? 0 : -1;
                    Label = LootStackPhase == 4 ? "hook_higher" : LootStackPhase == 5 ? "hook_lower" : LootStackPhase == 6 ? "hook_zero" : LootStackPhase == 7 ? "hook_negative" : "hook_invalid_type";
                    LootStackDiagnosticMaximum(LootStackMaxMode, LootStackMaxValue, LootStackPhase == 8 ? 10 : LootStackMaxValue, Label);
                    if (LootStackPhase == 4) foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                        LootStackPublic(Prefab, Label, 10);
                        LootStackRule(Prefab, new LootDropEntry(LootStackName, 20, 20));
                        try { LootFixtureVanilla(BarrelResearchCreate("d8_stack_hook_higher_reject", BarrelResearchMode.Vanilla,
                            BarrelResearchPosition, true, 0, Prefab)); }
                        finally { BarrelResearchCleanup(); }
                        Puts("[CarbonLuau:LootFixture] D8_STACK_HIGHER_REJECT item=" + LootStackName + " prefab=" + Prefab +
                            " definition_stack=10 effective_hook=20 declaration_max=20 vanilla_worlds=2 no_commit=true cleanup=0");
                    }
                } else if (LootStackPhase == 9) {
                    Label = "container_limits"; LootStackMatrixDefinition.stackable = 10; LootStackContainers();
                } else if (LootStackPhase == 10) {
                    Label = "can_stack_overrides"; LootStackMatrixDefinition.stackable = 10; LootStackCanStack();
                } else if (LootStackPhase == 11 || LootStackPhase == 12) {
                    Label = LootStackPhase == 11 ? "split_default" : "split_override";
                    LootStackMatrixDefinition.stackable = 10; LootStackSplit(LootStackPhase == 12);
                } else if (LootStackPhase == 13) {
                    Label = "competing_hooks"; LootStackMatrixDefinition.stackable = 10;
                    try {
                        LootStackConfigure(CarbonLuauStackProbeA, 1, 7); LootStackConfigure(CarbonLuauStackProbeB, 1, 9);
                        Item Value = LootStackFresh(1); int Actual = Value.MaxStackable();
                        BarrelResearchCheck((Actual == 7 || Actual == 9) && LootStackCounts(CarbonLuauStackProbeA)[0] == 1 &&
                            LootStackCounts(CarbonLuauStackProbeB)[0] == 1, "D8 real competing overrides observed without order assumption");
                        Puts("[CarbonLuau:LootFixture] D8_STACK_CONFLICT item=" + LootStackName + " a=7 b=9 actual=" + Actual + " a_calls=1 b_calls=1 order_assumed=false");
                    } finally { BarrelResearchCleanup(); }
                    LootStackMaxMode = LootStackSecondMaxMode = 1; LootStackMaxValue = 7; LootStackSecondMaxValue = 9;
                } else if (LootStackPhase == 14) {
                    Label = "dynamic_before_prepare";
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                        LootStackConfigure(CarbonLuauStackProbeA); LootStackConfigure(CarbonLuauStackProbeB);
                        LootStackMatrixDefinition.stackable = 2;
                        LootStackRule(Prefab, new LootDropEntry(LootStackName, 3, 3));
                        var Case = BarrelResearchCreate("d8_stack_before_prepare_" + LootStackName,
                            BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Prefab);
                        LootStackMatrixDefinition.stackable = 3;
                        LootFixturePayout(Case, 1, true, true);
                        Puts("[CarbonLuau:LootFixture] D8_STACK_MUTATION point=before_prepare item=" + LootStackName +
                            " old=2 new=3 planned=3 public_worlds=1 prefab=" + Prefab + " cleanup=0");
                    }
                }
                else if (LootStackPhase == 15) {
                    Label = "postcommit_definition_drop"; LootStackMatrixDefinition.stackable = 2;
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                        LootStackConfigure(CarbonLuauStackProbeA); LootStackConfigure(CarbonLuauStackProbeB);
                        LootStackMatrixDefinition.stackable = 2;
                        LootStackRule(Prefab, new LootDropEntry(LootStackName, 2, 2), new LootDropEntry(LootStackName, 2, 2));
                        LootStackAfterFirst = true; long Partial = LootPartial;
                        LootRegistryPayout("d8_stack_postcommit_" + LootStackName, Prefab, 1, false);
                        BarrelResearchCheck(!LootStackAfterFirst && LootPartial == Partial + 1, "D8 post-COMMIT second create abort preserves first world");
                        Puts("[CarbonLuau:LootFixture] D8_STACK_POSTCOMMIT item=" + LootStackName + " prefab=" + Prefab +
                            " preserved_worlds=1 preserved_amount=2 live_definition_stack=1 second_created=false partial_delta=1 fallback=false compensated=false cleanup=0");
                    }
                } else if (LootStackPhase == 16 || LootStackPhase == 17) {
                    Label = LootStackPhase == 16 ? "drop_amount_interference" : "drop_split_interference";
                    LootStackMatrixDefinition.stackable = 10; LootStackDropMode = LootStackPhase == 16 ? 1 : 3;
                } else if (LootStackPhase == 18) {
                    Label = "fixed_caps"; LootStackMatrixDefinition.stackable = 2000;
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                        LootStackConfigure(CarbonLuauStackProbeA); LootStackConfigure(CarbonLuauStackProbeB);
                        var Entries = new LootDropEntry[8]; for (int Index = 0; Index < 8; Index++) Entries[Index] = new LootDropEntry(LootStackName, 1000, 1000);
                        LootStackRule(Prefab, Entries); var Rule = LootRegistryRule(Prefab, Gameplay.Active);
                        BarrelResearchCheck(Host.Execute("d8.stack.1001", "assert(not pcall(function() game:GetService('Loot'):RegisterDropRule('" + Prefab + "',Enum.DropMode.Replace,{{Item='" + LootStackName + "',Min=1001,Max=1001}}) end))").Status == RuntimeStatus.OK &&
                            ReferenceEquals(LootRegistryRule(Prefab, Gameplay.Active), Rule), "D8 1001 rejected and original rule retained");
                        LootRegistryPayout("d8_stack_fixed_eight_" + LootStackName, Prefab, 8, true);
                    }
                }
                if (LootStackPhase != 3 && LootStackPhase != 4 && LootStackPhase != 14 && LootStackPhase != 15 && LootStackPhase != 18) {
                    int Amount = LootStackPhase == 0 ? Math.Min(LootStackMatrixOriginal, 1000) : LootStackPhase == 1 ? 1000 : LootStackPhase == 14 ? 3 : 2;
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab })
                        LootStackPublic(Prefab, Label, Amount, LootStackPhase == 17 ? 2 : 1, LootStackPhase != 16 && LootStackPhase != 17);
                }
            } finally {
                LootStackAfterFirst = false;
                LootStackTake(CarbonLuauStackProbeA); LootStackTake(CarbonLuauStackProbeB);
                LootStackConfigure(CarbonLuauStackProbeA); LootStackConfigure(CarbonLuauStackProbeB);
                LootStackMatrixDefinition.stackable = LootStackMatrixOriginal; LootStackMatrixDefinition = null; LootStackMatrixActive = false;
                BarrelResearchCleanup();
            }
            BarrelResearchCheck(BarrelResearchLedgerCount == 0, "D8 stack ordinary exact cleanup zero");
            LootStackCases++;
            Puts("[CarbonLuau:LootFixture] D8_STACK_CASE part=" + LootStackPart + " item=" + LootStackName + " phase=" + LootStackPhase +
                " case=" + Label + " result=PASS profiles=2 cleanup=0");
            if (++LootStackPhase == 19) { LootStackPhase = 0; LootStackMatrixIndex++; }
            if (LootStackMatrixIndex == 3) {
                LootFixtureEntries = TwoLootEntries(); LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK,
                    "D8 stack restores original two-entry public descriptor before unchanged B2 assertions");
                LootRegistryPayout("d8_stack_matrix_b2_handoff", LootFixturePrefab, 2, true);
                LootFixtureStackDone = true;
                Puts("[CarbonLuau:LootFixture] D8_STACK_BATCH mode=matrix part=" + LootStackPart + " completed=true cases=57 pass=57 fail=0 cleanup=0 all_pass=True");
            }
        }
        private void RunLootStackTerminal()
        {
            BarrelResearchCheck(LootFixtureAdditionalDone && LootFixtureRegistryDone && LootFixtureItemProbeDone &&
                LootFixtureAdditionalProbeDone && BarrelResearchLedgerCount == 0,
                "D8 terminal prior D3-D7 matrices completed with no retained physical resources");
            Puts("[CarbonLuau:LootFixture] D8_STACK_LEGACY before_terminal=true d3_d7_completed=true ledger=0 b2_terminal_claim=false");
            BarrelResearchCheck(CarbonLuauStackProbeA != null && CarbonLuauStackProbeB != null,
                "D8 terminal packet genuine hook plugins required");
            LootStackName = "sulfur"; LootStackMatrixDefinition = ItemManager.FindItemDefinition(LootStackName);
            BarrelResearchCheck(LootStackMatrixDefinition != null && LootStackMatrixDefinition.itemid == -1581843485 &&
                LootStackMatrixDefinition.stackable == 1000, "D8 terminal exact qualified representative");
            LootStackMatrixOriginal = 1000;
            LootStackConfigure(CarbonLuauStackProbeA, 0, 0, 0, 0, 2); LootStackConfigure(CarbonLuauStackProbeB);
            LootStackRule(LootRulePolicy.Prefab, new LootDropEntry("sulfur", 2, 2));
            var Case = BarrelResearchCreate("d8_stack_terminal_uid", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, LootRulePolicy.Prefab);
            BarrelResearchCurrent = Case;
            long BeforePartial = LootPartial, BeforeCleanup = LootCleanupFailed;
            int BeforeReceipts = LootFixtureReceipts;
            LootStackTerminal = true; LootFixtureStackTerminal = true;
            Puts("[CarbonLuau:LootFixture] D8_STACK_BEGIN mode=terminal_uid representatives=1 profiles=1 normal_cleanup_claim=false");
            Case.Source.Hurt(10000f);
            BarrelResearchCheck(LootStackTerminalObserved && LootCurrent == null && LootPartial == BeforePartial + 1 &&
                LootCleanupFailed == BeforeCleanup + 1 && LootFixtureReceipts == BeforeReceipts + 1 &&
                LootStackTerminalItem != null && LootStackTerminalItem.uid.Value == 0 && LootStackTerminalWorld != null &&
                LootStackTerminalWorld.IsFullySpawned() && !LootStackTerminalWorld.IsDestroyed &&
                ReferenceEquals(LootStackTerminalItem.GetWorldEntity(), LootStackTerminalWorld) &&
                LootStackTerminalWorld is WorldItem && ReferenceEquals(((WorldItem)LootStackTerminalWorld).item, LootStackTerminalItem),
                "D8 actual terminal UID partial preserves unknown completed world and cleanup refusal");
            int[] A = LootStackCounts(CarbonLuauStackProbeA), B = LootStackCounts(CarbonLuauStackProbeB);
            BarrelResearchCheck(A[0] == 0 && A[1] == 0 && A[2] == 0 && A[3] == 1 &&
                B[0] == 0 && B[1] == 0 && B[2] == 0 && B[3] == 1, "D8 terminal actual callback counts");
            Puts("[CarbonLuau:LootFixture] D8_STACK_TERMINAL item=sulfur id=-1581843485 result=UNSUPPORTED_I12 original_uid=" +
                LootStackTerminalUid + " actual_uid=0 world=" + LootStackTerminalWorld.net.ID.Value +
                " completed=true reciprocal_ref=true partial_delta=1 cleanup_failed_delta=1 max_calls=0 can_calls=0 split_calls=0 drop_calls=1 normal_cleanup_claim=false uid_restored=false authority_refreshed=false");
            Puts("[CarbonLuau:LootFixture] D8_STACK_BATCH mode=terminal_uid completed=true cases=1 unsupported_i12=1 normal_cleanup_claim=false process_restoration_required=true");
            // Keep the original ledger UID and unknown completed world untouched.
            // The runner must terminate the owned process and restore the full test profile.
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit");
            // Prevent the ordinary owner-step scheduler from entering cleanup/B2.
            LootFixtureBudgetFrame = Time.frameCount;
        }
    }
}
