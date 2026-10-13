// TEST ONLY: bounded D6 registry qualification over the unchanged D2A-D5 adapter.
using System;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private readonly object LootRegistryProvider = new object(), LootRegistryConflictProvider = new object();
        private string LootRegistryToken;
        partial void SelectLootRegistryQualification(ref bool Registry) { Registry = true; }
        partial void PopulateLootRegistryModules(ScriptSnapshot Snapshot)
        {
            Snapshot.Modules.Add("registrysecondfailed", LootFixtureSingleDeclaration(LootFixtureSecondPrefab,
                LootFixtureSecondEntries) + "error('D6 second staged module fails')");
        }
        partial void ObserveLootRegistryFrame(LootPayoutFrame Frame)
        {
            if (!LootFixtureRegistry || LootFixtureStep < 26) return;
            FacadeSession Owner; LootDropRule Rule;
            string Prefab = Frame.Binding.Lifetime.Record.Prefab;
            BarrelResearchCheck(Frame.Rule.Prefab == Prefab && Gameplay.TryLootRule(Prefab, out Owner, out Rule) &&
                ReferenceEquals(Owner, Frame.Domain) && ReferenceEquals(Rule, Frame.Rule),
                "D6 occurrence captures exact selected immutable rule and defining domain");
            Puts("[CarbonLuau:LootFixture] D6_FRAME prefab=" + Frame.Rule.Prefab + " domain=" + Frame.Domain.DomainLifetimeId +
                " vm=" + Frame.Domain.VmGenerationId + " actual_frame=" + Time.frameCount + " global_used=" + LootFrameUsed);
        }
        private LootDropRule LootRegistryRule(string Prefab, FacadeSession ExpectedOwner = null, LootDropRule ExpectedRule = null)
        {
            FacadeSession Owner; LootDropRule Rule;
            BarrelResearchCheck(Gameplay.TryLootRule(Prefab, out Owner, out Rule) && Owner != null && Rule != null &&
                Rule.Prefab == Prefab && ReferenceEquals(Owner.PublishedLootRule(Prefab), Rule) &&
                (ExpectedOwner == null || ReferenceEquals(Owner, ExpectedOwner)) &&
                (ExpectedRule == null || ReferenceEquals(Rule, ExpectedRule)), "D6 exact published selection " + Prefab);
            return Rule;
        }
        private FacadeSession LootRegistryOwner(string Prefab)
        {
            FacadeSession Owner; LootDropRule Rule;
            BarrelResearchCheck(Gameplay.TryLootRule(Prefab, out Owner, out Rule) && Owner != null && Rule != null,
                "D6 exact selected defining owner");
            return Owner;
        }
        private void LootRegistryNone(string Prefab)
        {
            FacadeSession Owner; LootDropRule Rule;
            BarrelResearchCheck(!Gameplay.TryLootRule(Prefab, out Owner, out Rule) && Owner == null && Rule == null,
                "D6 withdrawn slot exposes neither owner nor descriptor");
        }
        private void LootRegistryRetention()
        {
            FacadeSession First, Second; LootDropRule Rule;
            bool HasFirst = Gameplay.TryLootRule(LootRulePolicy.Prefab, out First, out Rule);
            bool HasSecond = Gameplay.TryLootRule(LootRulePolicy.SecondPrefab, out Second, out Rule);
            int Owners = (HasFirst ? 1 : 0) + (HasSecond && (!HasFirst || !ReferenceEquals(First, Second)) ? 1 : 0);
            BarrelResearchCheck(Gameplay.LootRetainedSessionCount == Owners && Owners <= 2 &&
                LootRulePolicy.RetainedRuleSessions == 3 && LootRulePolicy.RetainedRules == 6,
                "D6 completed publication/rollback/retirement retains only exact committed owner sessions");
            Puts("[CarbonLuau:LootFixture] D6_RETAINED step=" + LootFixtureStep + " owners=" + Owners +
                " retained_sessions=" + Gameplay.LootRetainedSessionCount + " session_cap=3 rule_cap=6");
        }
        private void LootRegistryRoot(bool Both)
        {
            LootFixturePrefab = LootRulePolicy.Prefab; BarrelResearchSelectedPrefab = LootFixturePrefab;
            LootFixtureEntries = new[] { new LootDropEntry("scrap", 7, 7), new LootDropEntry("metal.fragments", 11, 11) };
            LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab;
            LootFixtureSecondEntries = Both ? TwoLootEntries() : null;
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D6 complete root set publication");
            LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active);
            if (Both) LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
        }
        private void LootRegistryPayout(string Label, string Prefab, int Worlds = 2, bool Success = true)
        {
            BarrelResearchSelectedPrefab = Prefab;
            LootFixturePayout(Label, Worlds, Success);
        }
        private void LootRegistryPair(string Label)
        {
            try {
                var First = BarrelResearchCreate(Label + "_1", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, LootRulePolicy.Prefab);
                var Second = BarrelResearchCreate(Label + "_2", BarrelResearchMode.Vanilla,
                    BarrelResearchPosition + new Vector3(0.25f, 0, 0), true, 1, LootRulePolicy.SecondPrefab);
                LootFixturePayout(First, 2, true, false);
                BarrelResearchCheck(ValidateEntity(Second.Binding), "D6 simultaneous second source remains its original lifetime");
                for (int Index = 0; Index < Second.StoredCount; Index++)
                    BarrelResearchCheck(Second.Stored[Index].uid.Value == Second.StoredUids[Index] &&
                        ReferenceEquals(Second.Stored[Index].parent, Second.Container), "D6 other source stored ownership unchanged");
                // Four is the aggregate of the first frame's two retained exact
                // worlds and the second frame's two newly verified worlds. Each
                // completion independently proves its own Item/UID/definition.
                LootFixturePayout(Second, 4, true, false);
                Puts("[CarbonLuau:LootFixture] PASS D6 interleaved pair=" + Label + " active_sources=2 distinct_receipts=2 total_worlds=4");
            } finally { BarrelResearchCleanup(); }
        }
        private string LootRegistryAdd(object Provider, string Id, string Source, string Expected)
        {
            string[] Result = Addons.RegisterArchive(Provider, LootFixturePackage(Id, Source, false));
            BarrelResearchCheck(Result[0] == "OK" && Addons.ProcessOne() && Addons.Status(Provider, Result[1])[2] == Expected,
                "D6 actual provider candidate state " + Id + " / " + Expected);
            return Result[1];
        }
        private void LootRegistryReplace(string Source, string Version, string Expected)
        {
            string[] Result = Addons.ReplaceArchive(LootRegistryProvider, LootRegistryToken,
                LootFixturePackage("d6owner", Source, false, Version));
            BarrelResearchCheck(Result[0] == "OK" && Addons.ProcessOne() &&
                Addons.Status(LootRegistryProvider, LootRegistryToken)[2] == Expected, "D6 exact owned addon replacement " + Version);
        }
        private string LootRegistryAddonDeclaration(int Amount = 11)
        {
            return LootFixtureSingleDeclaration(LootRulePolicy.SecondPrefab,
                new[] { new LootDropEntry("scrap", 7, 7), new LootDropEntry("wood", Amount, Amount) });
        }
        private void LootRegistryVeto(string Prefab)
        {
            BarrelResearchSelectedPrefab = Prefab;
            var Case = BarrelResearchCreate("d6_veto_" + (Prefab == LootRulePolicy.Prefab ? "1" : "2"),
                BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
            int Receipts = LootFixtureReceipts, Successes = LootFixtureSuccesses;
            BarrelResearchVeto = true;
            try { Case.Source.Die(null); } finally { BarrelResearchVeto = false; }
            BarrelResearchCheck(LootFixtureReceipts == Receipts + 1 && LootFixtureSuccesses == Successes &&
                !Case.Source.IsDestroyed && ValidateEntity(Case.Binding), "D6 selected owner payout remains partial after original Kill veto");
            Case.Source.Kill(); BarrelResearchRetired(Case);
            BarrelResearchCheck(LootFixtureReceipts == Receipts + 1, "D6 later source removal never replays selected rule");
            BarrelResearchCleanup();
            LootRegistryPayout("d6_other_after_veto", Prefab == LootRulePolicy.Prefab ? LootRulePolicy.SecondPrefab : LootRulePolicy.Prefab);
        }
        partial void RunLootRegistryStep()
        {
            switch (LootFixtureStep) {
            case 26:
                BarrelResearchCheck(LootRulePolicy.GlobalRules == 2 && LootRulePolicy.DomainRules == 2 &&
                    LootRulePolicy.PerFrame == 8 && LootRulePolicy.MaximumCreatedWorlds == 8, "D6 only registry cardinalities expand");
                LootRegistryRoot(true); LootRegistryPair("d6_same_root");
                break;
            case 27: {
                int Frame = Time.frameCount;
                int Remaining = LootFrameNumber == Frame ? LootRulePolicy.PerFrame - LootFrameUsed : LootRulePolicy.PerFrame;
                int Before = LootFixtureReceipts;
                for (int Index = 0; Index < Remaining; Index++)
                    LootRegistryPayout("d6_shared_fill_" + Index, Index % 2 == 0 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(LootFixtureReceipts == Before + Remaining && LootFrameUsed == 8 && LootFrameNumber == Frame,
                    "D6 interleaved profiles share exactly eight actual-frame admissions");
                LootRegistryRoot(true);
                BarrelResearchCheck(Time.frameCount == Frame && LootFrameNumber == Frame && LootFrameUsed == 8,
                    "D6 complete-set replacement never resets shared actual-frame budget");
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    try {
                        LootFixtureVanilla(BarrelResearchCreate("d6_shared_rejected", BarrelResearchMode.Vanilla,
                            BarrelResearchPosition, true, 0, Prefab));
                    } finally { BarrelResearchCleanup(); }
                }
                Puts("[CarbonLuau:LootFixture] PASS D6 global actual-frame limit=8 both profiles preserve vanilla beyond admission");
                break;
            }
            case 28:
            case 29:
                LootFixturePrefab = LootFixtureStep == 28 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab;
                LootFixtureSecondPrefab = LootFixtureStep == 28 ? LootRulePolicy.SecondPrefab : LootRulePolicy.Prefab;
                LootFixtureEntries = LootFixtureSecondEntries = TwoLootEntries();
                BarrelResearchSelectedPrefab = LootFixturePrefab;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D6 two slots before controlled bag branch");
                LootFixtureControlledBag();
                LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
                Puts("[CarbonLuau:LootFixture] PASS D6 controlled bag selected=" + LootFixturePrefab + " other slot retained natural_bag_branch=false");
                break;
            case 30: {
                LootRegistryRoot(true);
                var Old = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Old);
                var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Old);
                string Previous = LootFixtureSource;
                LootFixtureEntries = new[] { new LootDropEntry("wood", 1000, 1000) };
                LootFixtureSecondEntries = new[] { new LootDropEntry("scrap", 23, 23) };
                LootFixtureSource = "error('D6 failed complete candidate')";
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old),
                        "D6 failed multi-rule initialization preserves exact outgoing owner");
                    LootRegistryRule(LootRulePolicy.Prefab, Old, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Old, Second);
                } finally { LootFixtureSource = Previous; }
                LootRegistryPair("d6_failed_multi_old_set");
                LootFixtureEntries = new[] { new LootDropEntry("scrap", 7, 7), new LootDropEntry("metal.fragments", 11, 11) };
                LootFixtureSecondEntries = TwoLootEntries(); LootFixtureSecondPrefab = LootRulePolicy.Prefab;
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old),
                        "D6 repeated same-prefab declaration rejects entire candidate set");
                    LootRegistryRule(LootRulePolicy.Prefab, Old, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Old, Second);
                } finally { LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab; }
                Puts("[CarbonLuau:LootFixture] PASS D6 failed/repeated declarations preserve both exact immutable incumbents");
                break;
            }
            case 31: {
                string Previous = LootFixtureSource;
                LootFixtureModuleRollback = true;
                LootFixtureSource = LootFixtureSingleDeclaration(LootRulePolicy.Prefab, LootFixtureEntries) +
                    "assert(not pcall(function() require('registrysecondfailed') end)); " + Previous;
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D6 caught nested failure permits outer first rule publication");
                    LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryNone(LootRulePolicy.SecondPrefab);
                    LootRegistryPayout("d6_nested_outer_rule", LootRulePolicy.Prefab);
                    try {
                        LootFixtureVanilla(BarrelResearchCreate("d6_nested_second_vanilla", BarrelResearchMode.Vanilla,
                            BarrelResearchPosition, true, 0, LootRulePolicy.SecondPrefab));
                    } finally { BarrelResearchCleanup(); }
                } finally { LootFixtureModuleRollback = false; LootFixtureSource = Previous; LootFixtureSecondEntries = null; }
                Puts("[CarbonLuau:LootFixture] PASS D6 module rollback removes only its staged second rule; outer rule publishes");
                break;
            }
            case 32:
                LootRegistryRoot(false);
                LootRegistryToken = LootRegistryAdd(LootRegistryProvider, "d6owner", LootRegistryAddonDeclaration(), "Active");
                BarrelResearchCheck(!ReferenceEquals(LootRegistryOwner(LootRulePolicy.Prefab), LootRegistryOwner(LootRulePolicy.SecondPrefab)),
                    "D6 root and addon are independent exact defining owners");
                LootRegistryPair("d6_root_addon");
                break;
            case 33: {
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                LootRegistryAdd(LootRegistryConflictProvider, "d6collision", LootFixtureSingleDeclaration(LootRulePolicy.Prefab, TwoLootEntries()), "Failed");
                LootRegistryAdd(LootRegistryConflictProvider, "d6multicollision", LootFixtureDeclaration() + LootRegistryAddonDeclaration(), "Failed");
                LootFixtureSecondEntries = TwoLootEntries();
                try {
                    BarrelResearchCheck(Host.Reload().Status != RuntimeStatus.OK && ReferenceEquals(Gameplay.Active, Root),
                        "D6 root candidate overlap with foreign second owner fails whole publication");
                    LootRegistryRule(LootRulePolicy.Prefab, Root, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSecondEntries = null; }
                LootRegistryPair("d6_conflict_incumbents");
                int Frame = Time.frameCount;
                int Remaining = LootRulePolicy.PerFrame - LootFrameUsed;
                for (int Index = 0; Index < Remaining; Index++)
                    LootRegistryPayout("d6_separate_owner_fill_" + Index,
                        Index % 2 == 0 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(LootFrameNumber == Frame && LootFrameUsed == 8,
                    "D6 separate root/addon owners share the same eight-occurrence frame cap");
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Root.Disposed &&
                    Time.frameCount == Frame && LootFrameNumber == Frame && LootFrameUsed == 8,
                    "D6 first-slot owner replacement preserves exhausted global frame budget");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    try {
                        LootFixtureVanilla(BarrelResearchCreate("d6_owner_replacement_budget_vanilla", BarrelResearchMode.Vanilla,
                            BarrelResearchPosition, true, 0, Prefab));
                    } finally { BarrelResearchCleanup(); }
                }
                Puts("[CarbonLuau:LootFixture] PASS D6 separate owners and exact owner replacement share eight-frame budget; both rejected sources vanilla");
                break;
            }
            case 34: {
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                LootFixtureEntries = new[] { new LootDropEntry("scrap", 8, 8), new LootDropEntry("metal.fragments", 11, 11) };
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Root.Disposed, "D6 root replaces only its first slot set");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                LootRegistryReplace(LootRegistryAddonDeclaration(19) + "error('failed D6 addon replacement')", "2.0.0", "Active");
                BarrelResearchCheck(Addons.Status(LootRegistryProvider, LootRegistryToken)[5].Contains("replacement failed"),
                    "D6 failed exact addon replacement keeps old active plan");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                var NextRoot = Gameplay.Active; var NextFirst = LootRegistryRule(LootRulePolicy.Prefab, NextRoot);
                LootRegistryReplace(LootRegistryAddonDeclaration(13), "3.0.0", "Active");
                BarrelResearchCheck(Addon.Disposed && !ReferenceEquals(LootRegistryOwner(LootRulePolicy.SecondPrefab), Addon),
                    "D6 addon replacement retires only its old second-slot authority");
                LootRegistryRule(LootRulePolicy.Prefab, NextRoot, NextFirst);
                BarrelResearchCheck(First.Entry(0).Min == 7 && Second.Entry(1).Min == 11,
                    "D6 captured outgoing descriptors never mutate or retarget");
                LootRegistryPair("d6_independent_replaced");
                break;
            }
            case 35: {
                var Root = Gameplay.Active; var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab);
                long Vm = Host.VmGenerationId; int Receipts = LootFixtureReceipts;
                BarrelResearchCheck(Host.Execute("d6.fatal", "while true do end").Status == RuntimeStatus.TIMEOUT && Host.Ready &&
                    Host.VmGenerationId != Vm && Root.Disposed && Addon.Disposed, "D6 fatal retirement invalidates both defining owners");
                LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryNone(LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(Addons.ProcessOne() && Addons.Status(LootRegistryProvider, LootRegistryToken)[2] == "Active" &&
                    !ReferenceEquals(LootRegistryOwner(LootRulePolicy.SecondPrefab), Addon) && LootFixtureReceipts == Receipts,
                    "D6 addon reconstructs committed source with fresh generation and no payout replay");
                try { Root.StageLootRule(LootRulePolicy.Prefab, TwoLootEntries()); throw new InvalidOperationException("D6 stale owner unexpectedly declared"); }
                catch (FacadeException) { }
                LootRegistryPair("d6_recovery_fresh_owners");
                break;
            }
            case 36: LootRegistryVeto(LootRulePolicy.Prefab); break;
            case 37: LootRegistryVeto(LootRulePolicy.SecondPrefab); break;
            case 38:
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    long Before = LootCleanupFailed; LootFixtureInjectSpawn = true;
                    try { LootRegistryPayout("d6_actual_incomplete_spawn", Prefab, 1, false); }
                    finally { LootFixtureInjectSpawn = false; }
                    BarrelResearchCheck(LootFixtureFailedWorld != null && LootFixtureFailedWorld.net == null &&
                        !EntityLifetimes.HasCompletedObservation(LootFixtureFailedWorld) && LootCleanupFailed == Before,
                        "D6 incomplete world never receives lifetime admission; cleanup retains original attempt authority");
                    LootFixtureFailedWorld = null;
                }
                LootRegistryPair("d6_after_spawn_partial");
                break;
            case 39:
            case 40:
                LootFixtureCreateFault = LootFixtureStep == 39 ? 1 : 2;
                try {
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab })
                        LootRegistryPayout("d6_actual_create_" + LootFixtureCreateFault, Prefab, 1, false);
                } finally { LootFixtureCreateFault = 0; }
                LootRegistryPair("d6_after_creation_partial");
                break;
            case 41:
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    BarrelResearchCreateActor(); BarrelResearchCheck(BarrelResearchBoost(), "D6 real Scrap_Yield modifier");
                    LootRegistryPayout("d6_boosted_bonus_suppressed", Prefab);
                }
                Puts("[CarbonLuau:LootFixture] PASS D6 stored and real bonus suppression for separate root/addon owners");
                break;
            case 42: {
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                LootFixtureEntries = LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D6 empty root replacement withdraws only its own slot");
                LootRegistryNone(LootRulePolicy.Prefab); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                try {
                    LootFixtureVanilla(BarrelResearchCreate("d6_root_withdrawn_vanilla", BarrelResearchMode.Vanilla,
                        BarrelResearchPosition, true, 0, LootRulePolicy.Prefab));
                } finally { BarrelResearchCleanup(); }
                LootRegistryPayout("d6_addon_survives_root_withdrawal", LootRulePolicy.SecondPrefab);
                LootRegistryRoot(false); var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                BarrelResearchCheck(Addons.UnloadProvider(LootRegistryProvider) == 1 && Addon.Disposed,
                    "D6 provider unload removes only its second slot");
                LootRegistryNone(LootRulePolicy.SecondPrefab); LootRegistryRule(LootRulePolicy.Prefab, Root, First);
                BarrelResearchCheck(Addons.UnloadProvider(LootRegistryConflictProvider) == 2, "D6 failed conflict registrations released");
                LootRegistryPayout("d6_root_survives_provider_unload", LootRulePolicy.Prefab);
                break;
            }
            case 43:
                LootRegistryRoot(true); LootRegistryPair("d6_same_root_reclaimed_both");
                LootFixturePrefab = LootRulePolicy.SecondPrefab; BarrelResearchSelectedPrefab = LootFixturePrefab;
                LootFixtureEntries = TwoLootEntries(); LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D6 final single-root fixture set for independent B2 regression");
                LootRegistryNone(LootRulePolicy.Prefab); LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
                LootRegistryPayout("d6_final_public_owner", LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(Addons.Count == 0 && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D6 registry fixture provider/physical resources converge before B2");
                LootFixtureRegistryDone = true;
                Puts("[CarbonLuau:LootFixture] PASS D6 same/separate owners, whole-set publication/rollback/recovery, both physical profiles and shared budgets");
                break;
            default: throw new InvalidOperationException("D6 unknown bounded fixture step");
            }
            LootRegistryRetention();
        }
    }
}
