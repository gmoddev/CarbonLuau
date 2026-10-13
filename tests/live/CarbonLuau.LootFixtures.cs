// Controlled D2A/D3/D4 qualification; all constructor/receipt/selector seams
// are erased when this fixture is not packaged.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using UnityEngine;

namespace Carbon.Plugins
{
    public sealed class CarbonLuauLootThrowingUpdate : MonoBehaviour, IOnSendNetworkUpdate
    {
        public void OnSendNetworkUpdate(BaseEntity Entity)
        { throw new InvalidOperationException("D2A fixture real late Spawn failure"); }
    }
    public partial class CarbonLuau
    {
        private LootDropEntry[] LootFixtureEntries;
        private bool LootFixtureInjectSpawn, LootFixtureMutateAmount;
        private int LootFixtureReceipts, LootFixtureSuccesses;
        private BaseEntity LootFixtureFailedWorld;
        private string LootFixtureSource = "local S=require('state'); local W=game:GetService('Workspace'); W.EntitySpawned:Connect(function(E) S.Spawned=(S.Spawned or 0)+1 end); W.EntityDestroyed:Connect(function(C) S.Destroyed=(S.Destroyed or 0)+1 end)";
        private bool LootFixtureDone;
        private bool LootFixturePublic;
        private bool LootFixtureBarrel2;
        private bool LootFixtureItems, LootFixtureItemProbeOnly, LootFixtureItemProbeDone;
        private bool LootFixtureRegistry, LootFixtureRegistryDone;
        private string LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab;
        private LootDropEntry[] LootFixtureSecondEntries;
        private string LootFixturePrefab = LootRulePolicy.Prefab;
        private int LootFixtureBudgetFrame = -1, LootFixtureBudgetWaits;
        private bool LootFixtureModuleRollback;
        private MethodInfo LootFixtureIndividualMethod, LootFixtureBagMethod;
        private BarrelResearchCase LootFixtureBagCase;
        private DroppedItemContainer LootFixtureBag;
        private int LootFixtureBagReturns, LootFixtureIndividualOverrides;
        private bool LootFixtureForceBag;
        partial void PrepareEntityDeathFixture();
        partial void ContinueEntityDeathFixture(ref bool Deferred);
        partial void SelectPublicLootQualification(ref bool Public);
        partial void SelectLootItemQualification(ref bool Items);
        partial void SelectLootItemProbe(ref bool ProbeOnly);
        partial void RunLootItemProbe();
        partial void RunLootItemCases();
        partial void SelectLootRegistryQualification(ref bool Registry);
        partial void PopulateLootRegistryModules(ScriptSnapshot Snapshot);
        partial void RunLootRegistryStep();
        partial void ObserveLootRegistryFrame(LootPayoutFrame Frame);
        private int LootFixtureStep;
        private bool LootFixtureInitialPayoutDone;
        private int LootFixtureEventWaits;
        private int LootFixtureEventWaitFrame = -1;
        private int LootFixtureCreateFault;
        private Harmony LootFixtureFaultPatches;
        private MethodInfo LootFixtureCreateMethod;
        private object LootFixtureProvider = new object();

        public sealed partial class FacadeSession
        {
            partial void StageLootQualification()
            {
                var Owner = ActiveEntityObserver;
                if (Owner != null && !Owner.LootFixturePublic && ReferenceEquals(World, Owner.Gameplay) && Owner.LootFixtureEntries != null)
                    StageLootRule(Owner.LootFixturePrefab, Owner.LootFixtureEntries);
            }
        }
        partial void SelectLootQualification(ref bool Qualification) { Qualification = true; }
        partial void RunLootQualification() { NextTick(StartLootQualification); }
        private static bool LootFixtureCreatePrefix(ref Item __result)
        {
            var Owner = ActiveEntityObserver;
            if (Owner == null || System.Threading.Thread.CurrentThread.ManagedThreadId != Owner.LootThread ||
                Owner.LootFixtureCreateFault == 0 || Owner.LootCurrent == null || Owner.LootCurrent.Index != 1) return true;
            if (Owner.LootFixtureCreateFault == 2) throw new InvalidOperationException("fixture real Create call dispatch failure");
            __result = null; return false;
        }
        partial void LootFixtureWorldCaptured(BaseEntity World, Item Value)
        {
            BarrelResearchCheck(LootCurrent != null && LootCurrent.Index >= 0 &&
                ReferenceEquals(LootCurrent.Items[LootCurrent.Index], Value) && Value.uid.Value == LootCurrent.Uids[LootCurrent.Index],
                "fixture acquisition uses exact returned production Item/UID");
            BarrelResearchHold(Value, true); BarrelResearchHold(World);
            if (LootFixtureMutateAmount && LootCurrent != null && LootCurrent.Index == 1) {
                Value.amount++;
                Puts("[CarbonLuau:LootFixture] INTERFERENCE actual returned Item.amount changed before world initialization");
            }
            if (LootFixtureInjectSpawn && LootCurrent != null && LootCurrent.Index == 1 && World != null) {
                LootFixtureFailedWorld = World;
                World.gameObject.AddComponent<CarbonLuauLootThrowingUpdate>();
            }
        }
        partial void LootFixtureCompleted(LootPayoutFrame Frame, bool Success)
        {
            LootFixtureReceipts++; if (Success) LootFixtureSuccesses++;
            ObserveLootRegistryFrame(Frame);
            BarrelResearchCheck(ReferenceEquals(Frame.Source, BarrelResearchCurrent.Source), "private original source receipt");
            int Worlds = 0;
            for (int Index = 0; Index < Frame.Rule.Count; Index++) {
                Item Value = Frame.Items[Index];
                if (Value == null) continue;
                BarrelResearchHold(Value); BarrelResearchHold(Frame.Worlds[Index]);
                if (!Frame.Accepted[Index]) continue;
                BarrelResearchCheck(LootWorldOwned(Frame, Index), "accepted original Item/UID/epoch/world reciprocal owner");
                BarrelResearchCheck(Value.info != null && ReferenceEquals(Value.info, Frame.Definitions[Index]) &&
                    Value.info.shortname == Frame.Rule.Entry(Index).Item, "actual accepted world exact intended definition including partial outcomes");
                if (Success) BarrelResearchCheck(VerifyLootWorld(Frame, Index), "verified planned amount/definition");
                Worlds++;
                Puts("[CarbonLuau:LootFixture] WORLD item=" + Value.info.shortname + " amount=" + Value.amount +
                    " uid=" + Value.uid + " id=" + Frame.Worlds[Index].net.ID.Value + " class=" + Frame.Worlds[Index].GetType().Name +
                    " entry=" + Index + " planned=" + Frame.Amounts[Index] + " prefab=" + Frame.Rule.Prefab);
            }
            Puts("[CarbonLuau:LootFixture] RECEIPT success=" + Success + " committed=" + Frame.Committed +
                " complete=" + Frame.PayoutComplete + " source_removal_certified=" + Frame.Removal.Certified +
                " accepted_worlds=" + Worlds + " cleanup_failed=" + LootCleanupFailed);
        }
        private void StartLootQualification()
        {
            BarrelResearchThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            try {
                Puts("[CarbonLuau:LootFixture] READY host=" + (Host != null && Host.Ready) + " startup=" + EntityStartupQualified +
                    " observer_broken=" + EntityObserverBroken + " armed=" + LootArmed + " factory_marker=" + LootWorldMarker +
                    " thread=" + LootThread + "/" + System.Threading.Thread.CurrentThread.ManagedThreadId +
                    " destroy_qualified=" + DestroySourceQualified + " destroy_stopped=" + DestroySourceStopped +
                    " removal_failure=" + DestroySourceFailure);
                BarrelResearchLogMethod(LootDeathTarget);
                LootFixtureCanonical(LootDeathTarget);
                BarrelResearchCheck(BasePlayer.activePlayerList.Count == 0 && Host != null && Host.Ready &&
                    EntityStartupQualified && LootAvailable(), "isolated qualified private source ready");
                SelectPublicLootQualification(ref LootFixturePublic);
                SelectLootItemQualification(ref LootFixtureItems);
                SelectLootRegistryQualification(ref LootFixtureRegistry);
                SelectLootItemProbe(ref LootFixtureItemProbeOnly);
                if (LootFixtureBarrel2) {
                    BarrelResearchCheck(LootFixturePublic, "D4 requires the actual public declaration fixture");
                    LootFixturePrefab = LootRulePolicy.SecondPrefab;
                    BarrelResearchSelectedPrefab = LootFixturePrefab;
                }
                Puts("[CarbonLuau:LootFixture] SELECT public=" + LootFixturePublic + " d4_barrel2=" + LootFixtureBarrel2 + " prefab=" + LootFixturePrefab);
                if (LootFixtureItems) {
                    BarrelResearchCheck(LootFixturePublic && LootFixtureBarrel2, "D5 requires existing public D4 substrate");
                    RunLootItemProbe();
                    if (LootFixtureItemProbeOnly) { EndLootQualification(null); return; }
                }
                PrepareEntityDeathFixture();
                if (Addons != null) { Addons.Dispose(); Addons = null; }
                Host.Dispose();
                if (LootFixtureRegistry) BarrelResearchCheck(Gameplay.LootRetainedSessionCount == 0,
                    "D6 old host disposal releases retained rule owners before new fixture host");
                Host = new ScriptHost(Native, new RuntimeConfig { MaxCallbackMilliseconds = 100 }, () => {
                    var Snapshot = new ScriptSnapshot { EntryName = "init.luau", EntrySource = (LootFixtureModuleRollback ? "" : LootFixtureDeclaration()) + LootFixtureSource };
                    Snapshot.Modules.Add("state", "return {}");
                    Snapshot.Modules.Add("failed", "error('failed publication fixture')");
                    Snapshot.Modules.Add("stagedfailed", LootFixtureDeclaration() + "error('failed staged loot module')");
                    PopulateLootRegistryModules(Snapshot);
                    return Snapshot;
                }, Gameplay);
                LootFixtureCreateMethod = typeof(ItemManager).GetMethod("Create", BindingFlags.Static | BindingFlags.Public,
                    null, new[] { typeof(ItemDefinition), typeof(int), typeof(ulong), typeof(bool), typeof(ulong) }, null);
                BarrelResearchCheck(LootFixtureCreateMethod != null, "exact creation fault target");
                LootFixtureFaultPatches = new Harmony("CarbonLuau.Test.LootCreation." + Guid.NewGuid().ToString("N"));
                LootFixtureFaultPatches.Patch(LootFixtureCreateMethod, prefix: new HarmonyMethod(
                    typeof(CarbonLuau).GetMethod("LootFixtureCreatePrefix", BindingFlags.Static | BindingFlags.NonPublic)));
                LootFixtureEntries = TwoLootEntries();
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && LootAvailable(), "private root publication");
                NextTick(RunLootQualificationStep);
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        private void LootFixtureCanonical(MethodInfo Method)
        {
            byte[] Bytes = Method.GetMethodBody().GetILAsByteArray();
            BarrelResearchCheck(Bytes.Length == 83, "bounded OnDied semantic diagnostic");
            var Codes = new Dictionary<ushort, OpCode>();
            foreach (FieldInfo Field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (Field.FieldType == typeof(OpCode)) { OpCode Code = (OpCode)Field.GetValue(null); Codes[unchecked((ushort)Code.Value)] = Code; }
            var Lines = new List<string>(); int Offset = 0;
            while (Offset < Bytes.Length) {
                BarrelResearchCheck(Lines.Count < 64, "semantic instruction bound");
                int Start = Offset; ushort Value = Bytes[Offset++];
                if (Value == 0xfe) Value = (ushort)(0xfe00 | Bytes[Offset++]);
                OpCode Code = Codes[Value];
                Lines.Add(Start.ToString("x4") + "|" + Code.Name + "|" + GameplayIlOperand(Method.Module, Bytes, ref Offset, Code.OperandType));
            }
            string Text = String.Join("\n", Lines);
            using (var Hash = SHA256.Create())
                Puts("[CarbonLuau:LootFixture] SEMANTIC_SHA256=" + BitConverter.ToString(Hash.ComputeHash(Encoding.UTF8.GetBytes(Text))).Replace("-", "").ToLowerInvariant());
            foreach (string Line in Lines) Puts("[CarbonLuau:LootFixture] SEMANTIC_IL " + Line);
        }
        private LootDropEntry[] TwoLootEntries()
        { return LootFixtureItems
            ? new[] { new LootDropEntry("scrap", 7, 7), new LootDropEntry("wood", 11, 11) }
            : new[] { new LootDropEntry("scrap", 7, 7), new LootDropEntry("metal.fragments", 11, 11) }; }
        private void RunLootQualificationStep()
        {
            try {
                int Needed = LootFixtureStep == 25 ? 6 : LootFixtureStep == 24 ? 5 : LootFixtureStep == 18 ? 2 : 1;
                if (LootFixtureRegistry && LootFixtureStep >= 26)
                    Needed = LootFixtureStep == 27 || LootFixtureStep == 33 ? 8 : LootFixtureStep >= 38 && LootFixtureStep <= 40 ? 4 :
                        LootFixtureStep == 43 ? 3 : LootFixtureStep == 28 || LootFixtureStep == 29 || LootFixtureStep == 31 ? 1 : 2;
                if (LootFixtureBarrel2 && (LootFixtureBudgetFrame == Time.frameCount ||
                    LootFrameNumber == Time.frameCount && LootFrameUsed > LootRulePolicy.PerFrame - Needed)) {
                    BarrelResearchCheck(++LootFixtureBudgetWaits <= 16, "D4 bounded wait for next actual frame after exhaustion");
                    NextTick(RunLootQualificationStep); return;
                }
                LootFixtureBudgetWaits = 0;
                // Separate actual owner turns for cleanup/native destruction and
                // producer-budget qualification; never sleep or pump Unity inline.
                if (LootFixtureStep == 0) {
                    if (!LootFixtureInitialPayoutDone) {
                        var Case = BarrelResearchCreate("root_two", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                        // EntitySpawned delivery revalidates the original
                        // live world. Retain both exact worlds throughout
                        // the existing bounded wait before owned cleanup.
                        LootFixturePayout(Case, 2, true, false);
                        LootFixtureInitialPayoutDone = true;
                        LootFixtureEventWaitFrame = Time.frameCount;
                    }
                    foreach (var Delivery in Host.Drain())
                        BarrelResearchCheck(Delivery.Status == RuntimeStatus.OK, "initial deferred event callback");
                    var Result = Host.Execute("loot.events", "local S=require('state'); assert((S.Spawned or 0)>=2, 'spawned='..tostring(S.Spawned)); assert((S.Destroyed or 0)>=1, 'destroyed='..tostring(S.Destroyed))");
                    if (Result.Status != RuntimeStatus.OK) {
                        // Event intake is deferred and budgeted by actual Unity
                        // frame, not by NextTick count. Never replay the payout.
                        if (LootFixtureEventWaitFrame != Time.frameCount) {
                            LootFixtureEventWaitFrame = Time.frameCount;
                            LootFixtureEventWaits++;
                        }
                        if (LootFixtureEventWaits < 16) { NextTick(RunLootQualificationStep); return; }
                    }
                    BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "B1/B3 callbacks remain usable: " + Result.Error);
                    BarrelResearchCleanup();
                } else if (LootFixtureStep == 1) {
                    var Old = Gameplay.Active;
                    LootFixtureEntries = new[] { new LootDropEntry(LootFixtureItems ? "wood" : "scrap", 1000, 1000) };
                    string Previous = LootFixtureSource; LootFixtureSource = "require('failed')";
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old), "failed root preserves exact rule owner");
                    LootFixtureSource = Previous;
                    LootFixturePayout("failed_candidate_old_rule", 2, true);
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Old.Disposed && !Old.Active, "successful root replaces old authority");
                } else if (LootFixtureStep == 2) {
                    LootFixturePayout("maximum_amount", 1, true);
                    LootFixtureEntries = new LootDropEntry[0];
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "empty replacement publication");
                } else if (LootFixtureStep == 3) {
                    LootFixturePayout("zero", 0, true);
                    LootFixtureEntries = TwoLootEntries();
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "restore normal descriptor");
                } else if (LootFixtureStep == 4) {
                    var Case = BarrelResearchCreate("vetoed_death", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    int Before = LootFixtureSuccesses, Receipts = LootFixtureReceipts;
                    BarrelResearchVeto = true;
                    try { Case.Source.Die(null); } finally { BarrelResearchVeto = false; }
                    BarrelResearchCheck(LootFixtureSuccesses == Before && LootFixtureReceipts == Receipts + 1 && !Case.Source.IsDestroyed && Case.Source.net != null &&
                        ValidateEntity(Case.Binding), "vetoed retirement remains partial, original not retargeted");
                    Puts("[CarbonLuau:LootFixture] PASS veto partial; release without payout replay");
                    Case.Source.Kill(); BarrelResearchRetired(Case);
                    BarrelResearchCheck(LootFixtureReceipts == Receipts + 1, "releasing Kill veto never replays payout");
                    BarrelResearchCleanup();
                } else if (LootFixtureStep == 5) {
                    long CleanupBefore = LootCleanupFailed;
                    LootFixtureInjectSpawn = true;
                    try { LootFixturePayout("real_late_spawn_failure", 1, false); }
                    finally { LootFixtureInjectSpawn = false; }
                    BarrelResearchCheck(LootFixtureFailedWorld != null && LootFixtureFailedWorld.net == null &&
                        !EntityLifetimes.HasCompletedObservation(LootFixtureFailedWorld) && LootCleanupFailed == CleanupBefore,
                        "real failed Spawn world captured, never admitted and cleaned through normal host path");
                    Puts("[CarbonLuau:LootFixture] PASS actual late Spawn failure, ownership capture, no false success/replay");
                    LootFixtureFailedWorld = null;
                } else if (LootFixtureStep == 6) {
                    var Old = Gameplay.Active;
                    BarrelResearchCheck(Host.Execute("loot.fatal", "while true do end").Status == RuntimeStatus.TIMEOUT && Host.Ready &&
                        Old.Disposed && !Old.Active && !ReferenceEquals(Gameplay.Active, Old), "fatal VM recovery retires old rule authority");
                    LootFixturePayout("recovery_fresh_rule", 2, true);
                } else if (LootFixtureStep == 7) {
                    // Eight maximum descriptor entries, eight distinct actual worlds.
                    LootFixtureEntries = new LootDropEntry[LootRulePolicy.Entries];
                    for (int Index = 0; Index < LootFixtureEntries.Length; Index++) LootFixtureEntries[Index] = new LootDropEntry(LootFixtureItems ? "wood" : "scrap", 1000, 1000);
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "max entry publication");
                } else if (LootFixtureStep == 8) {
                    LootFixturePayout("maximum_entries", 8, true);
                    LootFixtureEntries = null;
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "rule retirement");
                } else if (LootFixtureStep == 9) {
                    var Case = BarrelResearchCreate("no_rule_vanilla", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    int Before = LootFixtureReceipts;
                    Case.Source.Hurt(10000f);
                    BarrelResearchCheck(LootFixtureReceipts == Before, "retired owner cannot select private policy");
                    // No policy means no private completion receipt. The fixture
                    // must still track vanilla worlds through its original
                    // stored Items, not scan the realm or abandon their owners.
                    for (int Index = 0; Index < Case.StoredCount; Index++) {
                        Item Value = Case.Stored[Index]; WorldItem World = Value.GetWorldEntity() as WorldItem;
                        BarrelResearchCheck(World != null && ReferenceEquals(World.item, Value) &&
                            Value.parent == null && World.IsFullySpawned(), "original vanilla world owner after rule retirement");
                        BarrelResearchHold(World);
                    }
                    BarrelResearchRetired(Case); BarrelResearchCleanup();
                    Puts("[CarbonLuau:LootFixture] PASS future selection removed, normal vanilla destruction remains");
                    LootFixtureEntries = TwoLootEntries();
                    Addons = new AddonRegistry(Host, Native.HostLifetimeId);
                    string[] Registered = Addons.RegisterArchive(LootFixtureProvider, LootFixturePackage("lootowner", "return true"));
                    BarrelResearchCheck(Registered[0] == "OK" && Addons.ProcessOne() && Addons.Status(LootFixtureProvider, Registered[1])[2] == "Active",
                        "actual addon private rule publication");
                } else if (LootFixtureStep == 10) {
                    LootFixturePayout("addon_owner", 2, true);
                    BarrelResearchCheck(Addons.UnloadProvider(LootFixtureProvider) == 1, "provider unload retires addon rule");
                    LootFixtureEntries = TwoLootEntries();
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "root recovers ownership after provider unload");
                } else if (LootFixtureStep == 11) {
                    int Before = LootFixtureReceipts;
                    int Remaining = LootFrameNumber == Time.frameCount ? LootRulePolicy.PerFrame - LootFrameUsed : LootRulePolicy.PerFrame;
                    for (int Count = 0; Count < Remaining; Count++) LootFixturePayout("producer_" + Count, 2, true);
                    var Case = BarrelResearchCreate("producer_rejected", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    Case.Source.Hurt(10000f);
                    BarrelResearchCheck(LootFixtureReceipts == Before + Remaining && LootFrameUsed == LootRulePolicy.PerFrame,
                        "hard actual-frame producer admission boundary");
                    for (int Index = 0; Index < Case.StoredCount; Index++) {
                        WorldItem World = Case.Stored[Index].GetWorldEntity() as WorldItem;
                        BarrelResearchCheck(World != null && ReferenceEquals(World.item, Case.Stored[Index]), "precommit budget rejection preserves vanilla");
                        BarrelResearchHold(World);
                    }
                    BarrelResearchRetired(Case); BarrelResearchCleanup();
                    Puts("[CarbonLuau:LootFixture] PASS producer frame limit remaining=" + Remaining + " no hidden custom admission/queue");
                } else if (LootFixtureStep == 12) {
                    LootFixtureMutateAmount = true;
                    try { LootFixturePayout("trusted_quantity_interference", 2, false); }
                    finally { LootFixtureMutateAmount = false; }
                    Puts("[CarbonLuau:LootFixture] PASS actual trusted mutation classified partial, terminal world ownership preserved, no compensation");
                } else if (LootFixtureStep == 13) {
                    BarrelResearchCreateActor();
                    BarrelResearchCheck(BarrelResearchBoost(), "real modifier for private bonus suppression");
                    LootFixturePayout("boosted_bonus_suppressed", 2, true);
                    Puts("[CarbonLuau:LootFixture] PASS private common payout cut suppresses real Scrap_Yield bonus and stored contents");
                } else if (LootFixtureStep == 14 || LootFixtureStep == 15) {
                    LootFixtureCreateFault = LootFixtureStep == 14 ? 1 : 2;
                    try { LootFixturePayout(LootFixtureStep == 14 ? "actual_create_null_dispatch" : "actual_create_throw_dispatch", 1, false); }
                    finally { LootFixtureCreateFault = 0; }
                    Puts("[CarbonLuau:LootFixture] PASS actual ItemManager.Create fault dispatch; no unreturned allocation claim, partial world retained/no replay");
                    if (LootFixtureStep == 15 && !LootFixturePublic) { LootFixtureDone = true; EndLootQualification(null); return; }
                } else if (LootFixtureStep == 16) {
                    LootFixtureEntries = LootFixtureItems
                        ? new[] { new LootDropEntry("wood", 50, 100), new LootDropEntry("metal.fragments", 10, 25) }
                        : new[] { new LootDropEntry("scrap", 5, 12), new LootDropEntry("metal.fragments", 10, 25) };
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "public inclusive range publication");
                } else if (LootFixtureStep == 17) {
                    LootFixturePayout("public_range", 2, true);
                    BarrelResearchCheck(Host.Execute("loot.active", "assert(not pcall(function() game:GetService('Loot'):RegisterDropRule('" + LootFixturePrefab + "',Enum.DropMode.Replace,{}) end))").Status == RuntimeStatus.OK,
                        "committed public declaration rejected without effects");
                    if (!LootFixtureBarrel2) { FinishLootFixtureMatrix(); return; }
                    LootFixtureEntries = TwoLootEntries();
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D4 restore fixed public descriptor");
                } else if (LootFixtureStep == 18) {
                    LootFixtureBothProfiles();
                } else if (LootFixtureStep == 19) {
                    LootFixtureProfileBudget();
                } else if (LootFixtureStep == 20) {
                    LootFixtureForeignProfiles();
                } else if (LootFixtureStep == 21) {
                    LootFixtureRejectedSources();
                } else if (LootFixtureStep == 22) {
                    LootFixtureCaughtModule();
                } else if (LootFixtureStep == 23) {
                    LootFixtureControlledBag();
                    if (!LootFixtureItems) { FinishLootFixtureMatrix(); return; }
                } else if (LootFixtureStep == 24 || LootFixtureStep == 25) {
                    RunLootItemCases();
                    if (LootFixtureStep == 25) {
                        if (!LootFixtureRegistry) {
                            LootFixtureBudgetFrame = Time.frameCount; LootFixtureBudgetWaits = 0;
                            NextTick(FinishLootItemMatrix); return;
                        }
                    }
                } else if (LootFixtureRegistry && LootFixtureStep >= 26) {
                    RunLootRegistryStep();
                    if (LootFixtureRegistryDone) {
                        LootFixtureBudgetFrame = Time.frameCount; LootFixtureBudgetWaits = 0;
                        NextTick(FinishLootItemMatrix); return;
                    }
                    LootFixtureBudgetFrame = Time.frameCount;
                }
                LootFixtureStep++; NextTick(RunLootQualificationStep);
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        private string LootFixtureDeclaration()
        {
            if (!LootFixturePublic) return "";
            return LootFixtureSingleDeclaration(LootFixturePrefab, LootFixtureEntries) +
                LootFixtureSingleDeclaration(LootFixtureSecondPrefab, LootFixtureSecondEntries);
        }
        private string LootFixtureSingleDeclaration(string Prefab, LootDropEntry[] Entries)
        {
            if (Entries == null) return "";
            var Source = new StringBuilder("local L=game:GetService('Loot'); assert(table.isfrozen(Enum) and table.isfrozen(Enum.DropMode)); assert(type(Enum.DropMode.Replace)=='userdata' and tostring(Enum.DropMode.Replace)=='DropMode.Replace'); local D={");
            foreach (var Entry in Entries)
                Source.Append("{Item='").Append(Entry.Item).Append("',Min=").Append(Entry.Min).Append(",Max=").Append(Entry.Max).Append("},");
            Source.Append("}; L:RegisterDropRule('").Append(Prefab).Append("',Enum.DropMode.Replace,D); if D[1] then D[1].Item='invalid'; D[1].Min=999; D[1].Max=0 end; table.clear(D); ");
            return Source.ToString();
        }
        private byte[] LootFixturePackage(string Id, string Source, bool Declaration = true, string Version = "1.0.0")
        {
            using (var Bytes = new MemoryStream()) {
                using (var Zip = new ZipArchive(Bytes, ZipArchiveMode.Create, true)) {
                    using (var Writer = new StreamWriter(Zip.CreateEntry("addon.json").Open()))
                        Writer.Write("{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"" + Version + "\"}");
                    using (var Writer = new StreamWriter(Zip.CreateEntry("init.luau").Open())) Writer.Write((Declaration ? LootFixtureDeclaration() : "") + Source);
                }
                return Bytes.ToArray();
            }
        }
        private void LootFixturePayout(string Label, int ExpectedWorlds, bool Success)
        {
            var Case = BarrelResearchCreate(Label, BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
            LootFixturePayout(Case, ExpectedWorlds, Success, true);
        }
        private void LootFixturePayout(BarrelResearchCase Case, int ExpectedWorlds, bool Success, bool Cleanup)
        {
            string Label = Case.Label;
            BarrelResearchCurrent = Case;
            int Before = LootFixtureReceipts, BeforeSuccess = LootFixtureSuccesses;
            LootDropRule RangeRule = null;
            if (Label == "public_range") {
                FacadeSession Owner;
                BarrelResearchCheck(Gameplay.TryLootRule(Case.Prefab, out Owner, out RangeRule) && RangeRule != null,
                    "inclusive range checks retain exact selected immutable descriptor");
            }
            try {
                Case.Source.Hurt(10000f, Rust.DamageType.Generic, BarrelResearchActor, false);
                BarrelResearchRetired(Case);
                BarrelResearchCheck(LootFixtureReceipts == Before + 1 && LootFixtureSuccesses == BeforeSuccess + (Success ? 1 : 0),
                    "one private completion, correct success classification " + Label);
                int Actual = 0;
                for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                    Item Value = BarrelResearchLedger[Index].Item;
                    if (Value == null || Value.uid.Value == 0 || Value.IsRemoved() || Value.parent != null || Value.GetWorldEntity() == null) continue;
                    WorldItem World = Value.GetWorldEntity() as WorldItem;
                    BarrelResearchCheck(World != null && ReferenceEquals(World.item, Value) && World.IsFullySpawned() && !World.IsDestroyed &&
                        Value.uid.Value == BarrelResearchLedger[Index].ItemUid, "physical current original world owner");
                    Actual++;
                    if (RangeRule != null) {
                        bool Matches = false;
                        for (int EntryIndex = 0; EntryIndex < RangeRule.Count; EntryIndex++) {
                            var Entry = RangeRule.Entry(EntryIndex);
                            if (Value.info.shortname == Entry.Item && Value.amount >= Entry.Min && Value.amount <= Entry.Max) Matches = true;
                        }
                        BarrelResearchCheck(Matches, "public physical inclusive amount range from immutable descriptor");
                    }
                }
                BarrelResearchCheck(Actual == ExpectedWorlds, "no extra vanilla/bonus physical payout " + Label);
                foreach (var Result in Host.Drain()) BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "existing callback regression");
                Puts("[CarbonLuau:LootFixture] PASS case=" + Label + " worlds=" + Actual + " success=" + Success);
            } finally { if (Cleanup) BarrelResearchCleanup(); }
        }
        private void FinishLootFixtureMatrix()
        {
            LootFixtureDone = true;
            bool Deferred = false; ContinueEntityDeathFixture(ref Deferred);
            if (!Deferred) EndLootQualification(null);
        }
        private void FinishLootItemMatrix()
        {
            try {
                // D5's final six admitted payouts consume the shared actual
                // frame budget. B2's independent terminal cases start only in
                // a fresh actual frame; never reset production accounting.
                if (Time.frameCount == LootFixtureBudgetFrame) {
                    BarrelResearchCheck(++LootFixtureBudgetWaits <= 16, "D5 bounded actual-frame continuation before B2 regressions");
                    NextTick(FinishLootItemMatrix); return;
                }
                Puts("[CarbonLuau:LootFixture] " + (LootFixtureRegistry ? "D6_B2_CONTINUE" : "D5_B2_CONTINUE") + " previous_frame=" + LootFixtureBudgetFrame +
                    " current_frame=" + Time.frameCount + " loot_frame=" + LootFrameNumber + " prior_used=" + LootFrameUsed +
                    " production_budget_reset=false fresh_actual_frame=true");
                FinishLootFixtureMatrix();
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        partial void ObserveBarrelFixtureProfile(LootContainer Source)
        {
            if (!LootFixtureBarrel2) return;
            uint Id;
            var Components = Source.GetComponents<Component>();
            BarrelResearchCheck(Components.Length == 3, "D4 measured three root components");
            int Transforms = 0, Spawnables = 0, Loot = 0;
            foreach (var Component in Components) {
                BarrelResearchCheck(Component != null, "D4 no missing root component");
                if (Component.GetType() == typeof(Transform)) Transforms++;
                if (Component.GetType().Name == "Spawnable") Spawnables++;
                if (Component.GetType() == typeof(LootContainer)) Loot++;
            }
            var Cache = LootComponentsField.GetValue(Source) as List<EntityComponentBase>;
            BarrelResearchCheck(LootRulePolicy.TryPrefabId(Source.PrefabName, out Id) && Id == Source.prefabID &&
                Source.GetType() == typeof(LootContainer) && Transforms == 1 && Spawnables == 1 && Loot == 1 &&
                Cache != null && Cache.Count == 0 && Source.dropsLoot && Source.scrapAmount == 2 &&
                !Source.spawnDeployableCorpseOnDeath && Source.debrisPrefab != null && String.IsNullOrEmpty(Source.debrisPrefab.guid) &&
                Source.inventory != null && ReferenceEquals(Source.inventory.entityOwner, Source), "D4 exact measured source profile / InitShared cache");
            LootFixtureIndividualMethod = typeof(LootContainer).GetMethod("ShouldDropItemsIndividually", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            BarrelResearchCheck(LootFixtureIndividualMethod != null && LootFixtureIndividualMethod.ReturnType == typeof(bool) &&
                LootFixtureIndividualMethod.MetadataToken == 0x06005771, "D4 exact individual-drop selector");
            byte[] Body = LootFixtureIndividualMethod.GetMethodBody().GetILAsByteArray();
            BarrelResearchCheck(Body.Length == 2 && Body[0] == 0x17 && Body[1] == 0x2a &&
                (bool)LootFixtureIndividualMethod.Invoke(Source, null), "ordinary plain barrel selector is constant true");
            Puts("[CarbonLuau:LootFixture] PROFILE prefab=" + Source.PrefabName + " prefab_id=" + Id +
                " type=LootContainer root_components=Transform,Spawnable,LootContainer cache=0 dropsLoot=true scrapAmount=2 corpse=false debris_guid=empty" +
                " inventory_capacity=" + Source.inventory.capacity + " individual=true natural_bag_branch=false");
        }
        private void LootFixtureSelectProfile(string Prefab)
        {
            var Old = Gameplay.Active;
            LootFixturePrefab = Prefab; BarrelResearchSelectedPrefab = Prefab;
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Old.Disposed && !Old.Active,
                "D4 same outgoing root replaces its sole profile");
            FacadeSession Owner; LootDropRule Rule;
            BarrelResearchCheck(Gameplay.TryLootRule(Prefab, out Owner, out Rule) && ReferenceEquals(Owner, Gameplay.Active) && Rule.Prefab == Prefab,
                "D4 declaration selects its immutable exact profile");
            string Other = Prefab == LootRulePolicy.Prefab ? LootRulePolicy.SecondPrefab : LootRulePolicy.Prefab;
            BarrelResearchCheck(!Gameplay.TryLootRule(Other, out Owner, out Rule) && Owner == null && Rule == null,
                "D4 nonmatching profile returns no authority");
        }
        private void LootFixtureVanilla(BarrelResearchCase Case, bool ControlledOnDied = false)
        {
            BarrelResearchCurrent = Case;
            // Fixture authority is the original factory resource and captured
            // completed Spawn witness, never a facade made stale by a control.
            // No proxy is revived or retargeted by this raw test-only mutation.
            BarrelResearchCheck(LootFixtureOriginalSourceEpoch(Case) && Case.Source != null && !Case.Source.IsDestroyed &&
                Case.Source.net != null && Case.Source.net.ID.Value == Case.Id &&
                ReferenceEquals(BaseNetworkable.serverEntities.Find(Case.Source.net.ID), Case.Source),
                "vanilla control exact original raw source/ID/Spawn epoch");
            int Before = LootFixtureReceipts, BeforeSuccess = LootFixtureSuccesses;
            int Frame = LootFrameNumber, Used = LootFrameUsed;
            if (ControlledOnDied) {
                // Hurt's ordinary prelude can refill Components before the
                // OnDied admission prefix. Enter the exact full pinned payout
                // method directly so null is present at the intended gate.
                // This controlled negative does not qualify the outer Die path.
                BarrelResearchCheck(Case.Source.GetType() == typeof(LootContainer) && LootDeathTarget != null &&
                    LootDeathTarget.DeclaringType == typeof(LootContainer) && LootAvailable() &&
                    LootComponentsField.GetValue(Case.Source) == null, "controlled missing cache at full original OnDied entry");
                Puts("[CarbonLuau:LootFixture] CONTROL case=" + Case.Label +
                    " entry=full_LootContainer.OnDied cache=null controlled_payout_only=true outer_Die_not_entered=true");
                LootDeathTarget.Invoke(Case.Source, new object[] { null });
            } else Case.Source.Hurt(10000f); // Ordinary nil-initiator source, no modifier bonus.
            BarrelResearchRetired(Case);
            BarrelResearchCheck(LootFixtureReceipts == Before && LootFixtureSuccesses == BeforeSuccess &&
                LootFrameNumber == Frame && LootFrameUsed == Used && LootCurrent == null, "D4 pre-COMMIT source leaves counters/budget unchanged");
            for (int Index = 0; Index < Case.StoredCount; Index++) {
                Item Value = Case.Stored[Index];
                WorldItem World = Value.GetWorldEntity() as WorldItem;
                BarrelResearchHold(World);
                BarrelResearchCheck(Value.uid.Value == Case.StoredUids[Index] && Value.amount == Case.StoredAmounts[Index] &&
                    Value.parent == null && World != null && ReferenceEquals(World.item, Value) && World.IsFullySpawned() && !World.IsDestroyed &&
                    World.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                    EntityLifetimes.CaptureCompletedWitness(World) != null, "D4 exact original stored Item/UID vanilla world");
            }
            Puts("[CarbonLuau:LootFixture] PASS D4 vanilla case=" + Case.Label + " original_worlds=" + Case.StoredCount +
                " no_commit=true controlled_payout_only=" + ControlledOnDied);
        }
        private void LootFixtureBothProfiles()
        {
            foreach (string Selected in new[] { LootRulePolicy.SecondPrefab, LootRulePolicy.Prefab }) {
                LootFixtureSelectProfile(Selected);
                string Other = Selected == LootRulePolicy.Prefab ? LootRulePolicy.SecondPrefab : LootRulePolicy.Prefab;
                try {
                    var Match = BarrelResearchCreate("d4_selected_" + (Selected == LootRulePolicy.SecondPrefab ? "2" : "1"),
                        BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Selected);
                    var Nonmatch = BarrelResearchCreate("d4_unselected_" + (Selected == LootRulePolicy.SecondPrefab ? "1" : "2"),
                        BarrelResearchMode.Vanilla, BarrelResearchPosition + new Vector3(0.25f, 0, 0), true, 1, Other);
                    LootFixturePayout(Match, 2, true, false);
                    BarrelResearchCheck(ValidateEntity(Nonmatch.Binding), "D4 other original barrel stays live across selected payout");
                    for (int Index = 0; Index < Nonmatch.StoredCount; Index++)
                        BarrelResearchCheck(Nonmatch.Stored[Index].uid.Value == Nonmatch.StoredUids[Index] &&
                            ReferenceEquals(Nonmatch.Stored[Index].parent, Nonmatch.Container), "D4 other stored owners unchanged");
                    LootFixtureVanilla(Nonmatch);
                } finally { BarrelResearchCleanup(); }
            }
            LootFixtureSelectProfile(LootRulePolicy.SecondPrefab);
            Puts("[CarbonLuau:LootFixture] PASS D4 both profiles active; sole matching rule replaces, unselected original stays vanilla");
        }
        private void LootFixtureProfileBudget()
        {
            int Frame = Time.frameCount;
            int Remaining = LootFrameNumber == Frame ? LootRulePolicy.PerFrame - LootFrameUsed : LootRulePolicy.PerFrame;
            int Before = LootFixtureReceipts;
            for (int Index = 0; Index < Remaining; Index++) LootFixturePayout("d4_frame_fill_" + Index, 2, true);
            BarrelResearchCheck(LootFixtureReceipts == Before + Remaining && LootFrameNumber == Frame &&
                LootFrameUsed == LootRulePolicy.PerFrame, "D4 exhaust actual frame before profile switch");
            foreach (string Selected in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                LootFixtureSelectProfile(Selected);
                BarrelResearchCheck(Time.frameCount == Frame && LootFrameNumber == Frame && LootFrameUsed == LootRulePolicy.PerFrame,
                    "D4 same-owner profile publication never resets shared frame budget");
                try {
                    LootFixtureVanilla(BarrelResearchCreate("d4_switched_budget_" + (Selected == LootRulePolicy.SecondPrefab ? "2" : "1"),
                        BarrelResearchMode.Vanilla, BarrelResearchPosition, true));
                } finally { BarrelResearchCleanup(); }
            }
            LootFixtureBudgetFrame = Frame;
            Puts("[CarbonLuau:LootFixture] PASS D4 same-frame profile switch budget retained frame=" + Frame + " used=" + LootFrameUsed);
        }
        private void LootFixtureForeignProfiles()
        {
            var Old = Gameplay.Active;
            int RegisteredCount = 0;
            try {
                foreach (string Prefab in new[] { LootRulePolicy.SecondPrefab, LootRulePolicy.Prefab }) {
                    LootFixturePrefab = Prefab;
                    string[] Registered = Addons.RegisterArchive(LootFixtureProvider, LootFixturePackage("d4conflict" + RegisteredCount, "return true"));
                    BarrelResearchCheck(Registered[0] == "OK", "D4 conflicting provider snapshot registration");
                    RegisteredCount++;
                    bool Same = Prefab == LootRulePolicy.SecondPrefab;
                    BarrelResearchCheck(Addons.ProcessOne() && Addons.Status(LootFixtureProvider, Registered[1])[2] == (Same ? "Failed" : "Active") &&
                        ReferenceEquals(Gameplay.Active, Old) && Old.PublishedLootRule(LootRulePolicy.SecondPrefab).Prefab == LootRulePolicy.SecondPrefab,
                        "same-prefab foreign conflict and disjoint owner coexistence preserve incumbent");
                    if (!Same) {
                        FacadeSession Owner; LootDropRule Rule;
                        BarrelResearchCheck(Gameplay.TryLootRule(Prefab, out Owner, out Rule) && !ReferenceEquals(Owner, Old),
                            "disjoint addon owns only its exact qualified profile");
                    }
                }
            } finally {
                LootFixturePrefab = LootRulePolicy.SecondPrefab;
                BarrelResearchCheck(Addons.UnloadProvider(LootFixtureProvider) == RegisteredCount, "failed/active provider registrations retire independently");
            }
            LootFixturePayout("d4_foreign_conflicts_old_rule", 2, true);
            Puts("[CarbonLuau:LootFixture] PASS same-prefab foreign conflict, disjoint coexistence and provider retirement preserve incumbent");
        }
        private void LootFixtureRejectedSources()
        {
            FieldInfo PrefabField = typeof(BaseNetworkable).GetField("_prefabName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            BarrelResearchCheck(PrefabField != null && PrefabField.FieldType == typeof(string), "D4 exact cached prefab fixture field");
            for (int Index = 0; Index < 3; Index++) {
                BarrelResearchCase Case = null;
                object Cache = null;
                uint PrefabId = 0;
                string PrefabCache = null;
                Exception FirstCause = null;
                try {
                    Case = Index == 0
                        ? BarrelResearchCreate("d4_unsupported_actual_source", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0,
                            "assets/bundled/prefabs/radtown/loot_barrel_1.prefab", false)
                        : BarrelResearchCreate(Index == 1 ? "d4_wrong_prefab_id" : "d4_missing_component_cache",
                            BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    Cache = LootComponentsField.GetValue(Case.Source);
                    PrefabId = Case.Source.prefabID;
                    PrefabCache = (string)PrefabField.GetValue(Case.Source);
                    BarrelResearchCheck(Case.Prefab == Case.Binding.Lifetime.Record.Prefab && Case.Source.PrefabName == Case.Prefab,
                        "negative control starts from exact captured source prefab");
                    if (Index == 1) Case.Source.prefabID = LootRulePolicy.PrefabId;
                    if (Index == 2) LootComponentsField.SetValue(Case.Source, null);
                    uint MeasuredId;
                    if (Index == 0) BarrelResearchCheck(!LootRulePolicy.TryPrefabId(Case.Source.PrefabName, out MeasuredId),
                        "actual negative prefab remains outside production admission");
                    if (Index == 1) BarrelResearchCheck(Case.Source.PrefabName == Case.Binding.Lifetime.Record.Prefab &&
                        Case.Source.prefabID != PrefabId, "mismatched-ID control keeps original cached source name");
                    if (Index == 2) BarrelResearchCheck(LootComponentsField.GetValue(Case.Source) == null,
                        "missing-cache control must actually remove only the original component cache");
                    Puts("[CarbonLuau:LootFixture] CONTROL case=" + Case.Label + " original_prefab=" + Case.Binding.Lifetime.Record.Prefab +
                        " observed_prefab=" + Case.Source.PrefabName + " original_prefab_id=" + PrefabId + " observed_prefab_id=" + Case.Source.prefabID);
                    LootFixtureVanilla(Case, Index == 2);
                } catch (Exception Error) {
                    FirstCause = Error;
                    PrintError("[CarbonLuau:LootFixture] FIRST_CAUSE case=" + (Case == null ? "create_negative_source" : Case.Label) + " " + Error);
                } finally {
                    try {
                        if (Case != null && Case.Binding != null && LootFixtureOriginalSourceEpoch(Case)) {
                            // Cached presentation identity is a fixture control,
                            // not cleanup authority. Restore only the still-keyed
                            // original, using the actual saved scalar/cache values.
                            if (Case.Source != null && Case.Source.net != null && Case.Source.net.ID.Value == Case.Id &&
                                ReferenceEquals(BaseNetworkable.serverEntities.Find(Case.Source.net.ID), Case.Source)) {
                                PrefabField.SetValue(Case.Source, PrefabCache);
                                Case.Source.prefabID = PrefabId;
                                LootComponentsField.SetValue(Case.Source, Cache);
                            }
                            LootFixtureReleaseStored(Case);
                        }
                    } catch (Exception Error) {
                        PrintError("[CarbonLuau:LootFixture] CONTROL_CLEANUP_FAIL " + Error);
                        if (FirstCause == null) FirstCause = Error;
                    }
                    try { BarrelResearchCleanup(); }
                    catch (Exception Error) {
                        PrintError("[CarbonLuau:LootFixture] SECONDARY_CLEANUP_FAIL " + Error);
                        if (FirstCause == null) FirstCause = Error;
                    }
                }
                if (FirstCause != null) ExceptionDispatchInfo.Capture(FirstCause).Throw();
            }
            var Old = Gameplay.Active;
            LootFixturePrefab = "assets/bundled/prefabs/autospawn/resource/loot/d4-unknown.prefab";
            try {
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Old),
                    "D4 unknown public declaration fails before replacing original owner");
            } finally { LootFixturePrefab = LootRulePolicy.SecondPrefab; }
            LootFixturePayout("d4_unknown_declaration_old_rule", 2, true);
            Puts("[CarbonLuau:LootFixture] PASS D4 actual unsupported/invalid source vanilla; unknown declaration preserves owner");
        }
        private bool LootFixtureOriginalSourceEpoch(BarrelResearchCase Case)
        {
            for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                var Resource = BarrelResearchLedger[Index];
                if (!ReferenceEquals(Resource.Entity, Case.Source)) continue;
                return Resource.Witness != null && ReferenceEquals(Resource.Witness.Target, Case.Source) &&
                    Resource.Witness.Epoch == Case.Binding.Lifetime.Record.Epoch &&
                    Resource.Witness.ObservationGeneration == Case.Binding.Lifetime.Record.ObservationGeneration &&
                    EntityLifetimes.IsWitnessEpochUnchanged(Resource.Witness);
            }
            return false;
        }
        private void LootFixtureReleaseStored(BarrelResearchCase Case)
        {
            // An assertion before death can leave seeded Items in the original
            // owned inventory. Release that inventory through its supported
            // Clear, before normal source Kill; never Remove an owned Item.
            // Successful vanilla worlds are verified before this cleanup and
            // do not belong to this container, so no payout is compensated.
            if (Case.Container == null || Case.Container.itemList == null || Case.Container.itemList.Count == 0) return;
            BarrelResearchCheck(LootFixtureOriginalSourceEpoch(Case) && Case.Container.uid.ToString() == Case.ContainerUid &&
                ReferenceEquals(Case.Container.entityOwner, Case.Source) &&
                (Case.Source.inventory == null || ReferenceEquals(Case.Source.inventory, Case.Container)) &&
                Case.Container.itemList.Count <= BarrelResearchItemLimit, "cleanup original bounded source inventory/UID/epoch");
            foreach (var Value in Case.Container.itemList) {
                bool Original = false;
                for (int Index = 0; Index < Case.StoredCount; Index++)
                    if (ReferenceEquals(Value, Case.Stored[Index]) && Value.uid.Value != 0 && Value.uid.Value == Case.StoredUids[Index]) Original = true;
                BarrelResearchCheck(Original && ReferenceEquals(Value.parent, Case.Container) && ReferenceEquals(Value.GetWorldEntity(), null),
                    "cleanup only original seeded Items still owned by original inventory");
            }
            Case.Container.Clear();
            BarrelResearchCheck(Case.Container.itemList.Count == 0, "original fixture inventory Clear completed");
        }
        private void LootFixtureCaughtModule()
        {
            string Previous = LootFixtureSource;
            LootFixtureModuleRollback = true;
            LootFixtureSource = "assert(not pcall(function() require('stagedfailed') end))";
            try {
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Gameplay.Active.PublishedLootRule(LootFixturePrefab) == null,
                    "D4 caught failed nested module discards its staged public rule");
                LootFixtureVanilla(BarrelResearchCreate("d4_caught_module_no_rule", BarrelResearchMode.Vanilla, BarrelResearchPosition, true));
            } finally {
                BarrelResearchCleanup(); LootFixtureModuleRollback = false; LootFixtureSource = Previous;
            }
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D4 root restores public descriptor after nested rollback");
            Puts("[CarbonLuau:LootFixture] PASS D4 caught nested publication has no rule or replay");
        }
        private static bool LootFixtureIndividualPrefix(LootContainer __instance, ref bool __result)
        {
            var Owner = ActiveEntityObserver;
            if (Owner == null || !Owner.LootFixtureForceBag || Owner.LootFixtureBagCase == null ||
                !ReferenceEquals(__instance, Owner.LootFixtureBagCase.Source)) return true;
            BarrelResearchCheck(Owner.BarrelResearchOnOwner(), "controlled exact-source getter owner/no VM entry");
            Owner.LootFixtureIndividualOverrides++;
            Owner.Puts("[CarbonLuau:LootFixture] BAG_SELECTOR case=" + Owner.LootFixtureBagCase.Label + " calls=" + Owner.LootFixtureIndividualOverrides);
            BarrelResearchCheck(Owner.LootFixtureIndividualOverrides == 1, "one bounded controlled bag selector call");
            __result = false; return false;
        }
        private static void LootFixtureBagPostfix(ItemContainer __instance, float __3, DroppedItemContainer __result)
        {
            var Owner = ActiveEntityObserver;
            if (Owner == null || !Owner.LootFixtureForceBag || Owner.LootFixtureBagCase == null) return;
            var Case = Owner.LootFixtureBagCase;
            if (!ReferenceEquals(__instance, Case.Container)) return;
            BarrelResearchCheck(Owner.BarrelResearchOnOwner(), "controlled actual bag-return owner/no VM entry");
            Owner.BarrelResearchHold(__result);
            Owner.LootFixtureBag = __result;
            if (__result != null) {
                __result.enableSaving = false;
                var Inventory = __result.inventory;
                BarrelResearchCheck(Inventory != null && Inventory.itemList != null && Inventory.itemList.Count <= BarrelResearchItemLimit,
                    "bounded actual returned bag inventory capture");
                // Genuine returned bag and every accessible Item enter the ledger
                // before physical or cardinality assertions can throw.
                foreach (var Value in Inventory.itemList) Owner.BarrelResearchHold(Value);
            }
            Owner.LootFixtureBagReturns++;
            Owner.Puts("[CarbonLuau:LootFixture] BAG_RETURN case=" + Case.Label + " returns=" + Owner.LootFixtureBagReturns +
                " returned=" + (__result != null) + " destroy_percent=" + __3 + " target=" + Owner.LootFixtureBagMethod);
            BarrelResearchCheck(__result != null && Owner.LootFixtureBagReturns == 1, "one actual original-container Drop bag return");
        }
        private void LootFixtureControlledBag()
        {
            // The plain asset's real selector is constant true. This override is
            // limited to one original fixture source and proves a controlled IL
            // branch, never natural asset configuration or subclass admission.
            LootFixtureBagMethod = typeof(ItemContainer).GetMethod("Drop", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                new[] { typeof(string), typeof(Vector3), typeof(Quaternion), typeof(float) }, null);
            BarrelResearchCheck(LootFixtureBagMethod != null && LootFixtureBagMethod.ReturnType == typeof(DroppedItemContainer) &&
                LootFixtureBagMethod.MetadataToken == 0x06006a77 &&
                LootFixtureIndividualMethod != null, "D4 exact controlled bag-return and selector signatures");
            Puts("[CarbonLuau:LootFixture] BAG_TARGET selector=" + LootFixtureIndividualMethod +
                " selector_owner=" + LootFixtureIndividualMethod.DeclaringType.FullName +
                " selector_token=0x" + LootFixtureIndividualMethod.MetadataToken.ToString("x8") +
                " selector_virtual=" + LootFixtureIndividualMethod.IsVirtual + " selector_final=" + LootFixtureIndividualMethod.IsFinal +
                " drop=" + LootFixtureBagMethod + " drop_owner=" + LootFixtureBagMethod.DeclaringType.FullName +
                " drop_token=0x" + LootFixtureBagMethod.MetadataToken.ToString("x8") +
                " drop_static=" + LootFixtureBagMethod.IsStatic + " mvid=" + LootFixtureBagMethod.Module.ModuleVersionId);
            Exception FirstCause = null;
            try {
                LootFixtureFaultPatches.Patch(LootFixtureIndividualMethod, prefix: new HarmonyMethod(
                    typeof(CarbonLuau).GetMethod(nameof(LootFixtureIndividualPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                LootFixtureFaultPatches.Patch(LootFixtureBagMethod, postfix: new HarmonyMethod(
                    typeof(CarbonLuau).GetMethod(nameof(LootFixtureBagPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                BarrelResearchCheck(LootAvailable(), "controlled branch observer preserves common-cut/D20/B3 topology");
                LootFixtureEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "controlled bag baseline has no published rule");
                LootFixtureBagIsolated(() => {
                    var Case = BarrelResearchCreate("d4_controlled_bag_baseline", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    LootFixtureBagCase = Case; LootFixtureForceBag = true;
                    LootFixtureIndividualOverrides = LootFixtureBagReturns = 0;
                    int Before = LootFixtureReceipts;
                    LootFixtureBagDiagnostic(); // Captured scalars/owners before actual host dispatch.
                    Case.Source.Hurt(10000f);
                    BarrelResearchRetired(Case);
                    BarrelResearchCheck(LootFixtureReceipts == Before && LootFixtureIndividualOverrides == 1 && LootFixtureBagReturns == 1 &&
                        LootFixtureBag != null && LootFixtureBag.IsFullySpawned() && LootFixtureBag.net != null &&
                        ReferenceEquals(BaseNetworkable.serverEntities.Find(LootFixtureBag.net.ID), LootFixtureBag) &&
                        LootFixtureBag.inventory.itemList.Count == Case.StoredCount, "controlled false selector actually returns one physical bag");
                    for (int Index = 0; Index < Case.StoredCount; Index++) {
                        Item Value = Case.Stored[Index];
                        BarrelResearchCheck(Value.uid.Value == Case.StoredUids[Index] && Value.amount == Case.StoredAmounts[Index] &&
                            ReferenceEquals(Value.parent, LootFixtureBag.inventory) && LootFixtureBag.inventory.itemList.Contains(Value) &&
                            ReferenceEquals(Value.GetWorldEntity(), null), "controlled actual bag owns each original Item/UID");
                    }
                    Puts("[CarbonLuau:LootFixture] PASS D4 controlled bag baseline bag_returns=1 stored=" + Case.StoredCount +
                        " provenance=actual_ItemContainer.Drop_return natural_bag_branch=false");
                });
                LootFixtureEntries = TwoLootEntries();
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D4 public rule restored for controlled bag suppression");
                LootFixtureBagIsolated(() => {
                    var Case = BarrelResearchCreate("d4_controlled_bag_replaced", BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                    LootFixtureBagCase = Case; LootFixtureForceBag = true;
                    LootFixtureIndividualOverrides = LootFixtureBagReturns = 0;
                    LootFixturePayout(Case, 2, true, false);
                    BarrelResearchCheck(LootFixtureBagReturns == 0 && LootFixtureIndividualOverrides == 0 && LootFixtureBag == null,
                        "public common cut suppresses entire bag body before selector or container Drop");
                    Puts("[CarbonLuau:LootFixture] PASS D4 controlled bag suppression bag_returns=0 replacement_worlds=2 natural_bag_branch=false");
                });
            } catch (Exception Error) {
                FirstCause = Error;
                PrintError("[CarbonLuau:LootFixture] FIRST_CAUSE controlled_bag " + Error);
            } finally {
                LootFixtureForceBag = false; LootFixtureBagCase = null;
                try { RemoveLootFixtureBagPatches(); }
                catch (Exception Error) {
                    PrintError("[CarbonLuau:LootFixture] SECONDARY_CLEANUP_FAIL bag unpatch " + Error);
                    if (FirstCause == null) FirstCause = Error;
                }
            }
            if (FirstCause != null) ExceptionDispatchInfo.Capture(FirstCause).Throw();
            BarrelResearchCheck(LootAvailable() && VerifyAllEntityPatches() && EntityDestroyedAvailable(), "D4 controlled branch unpatched with original observer intact");
        }
        private void LootFixtureBagIsolated(Action Work)
        {
            Exception FirstCause = null;
            try { Work(); }
            catch (Exception Error) {
                FirstCause = Error;
                PrintError("[CarbonLuau:LootFixture] FIRST_CAUSE case=" + (LootFixtureBagCase == null ? "bag_setup" : LootFixtureBagCase.Label) + " " + Error);
                try { LootFixtureBagDiagnostic(); }
                catch (Exception Failure) { PrintError("[CarbonLuau:LootFixture] BAG_DIAGNOSTIC_FAIL " + Failure); }
            } finally {
                LootFixtureForceBag = false;
                try {
                    if (LootFixtureBagCase != null && LootFixtureOriginalSourceEpoch(LootFixtureBagCase))
                        LootFixtureReleaseStored(LootFixtureBagCase);
                } catch (Exception Error) {
                    PrintError("[CarbonLuau:LootFixture] SECONDARY_CLEANUP_FAIL bag source " + Error);
                    if (FirstCause == null) FirstCause = Error;
                }
                try { BarrelResearchCleanup(); }
                catch (Exception Error) {
                    PrintError("[CarbonLuau:LootFixture] SECONDARY_CLEANUP_FAIL bag ledger " + Error);
                    if (FirstCause == null) FirstCause = Error;
                }
                LootFixtureBagCase = null; LootFixtureBag = null;
            }
            if (FirstCause != null) ExceptionDispatchInfo.Capture(FirstCause).Throw();
        }
        private void LootFixtureBagDiagnostic()
        {
            var Case = LootFixtureBagCase;
            Puts("[CarbonLuau:LootFixture] BAG_STATE selector_calls=" + LootFixtureIndividualOverrides + " bag_returns=" + LootFixtureBagReturns +
                " receipts=" + LootFixtureReceipts + " successes=" + LootFixtureSuccesses + " selected=" + LootSelected +
                " verified=" + LootVerified + " partial=" + LootPartial + " rejected=" + LootRejected + " cleanup_failed=" + LootCleanupFailed +
                " actual_frame=" + Time.frameCount + " budget_frame=" + LootFrameNumber + " budget_used=" + LootFrameUsed +
                " in_flight=" + (LootCurrent != null) + " source_prefab=" + (Case == null ? "nil" : Case.Prefab) +
                " source_id=" + (Case == null ? 0 : Case.Id) +
                " ledger=" + BarrelResearchLedgerCount + " source_keyed=" + (Case != null && Case.Source != null && Case.Source.net != null &&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(Case.Source.net.ID), Case.Source)) +
                " bag_keyed=" + (LootFixtureBag != null && LootFixtureBag.net != null &&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(LootFixtureBag.net.ID), LootFixtureBag)));
            if (Case == null) return;
            BarrelResearchCheck(Case.StoredCount <= BarrelResearchItemLimit, "bounded bag diagnostic original items");
            for (int Index = 0; Index < Case.StoredCount; Index++) {
                Item Value = Case.Stored[Index];
                var World = Value == null ? null : Value.GetWorldEntity() as WorldItem;
                // An ineffective forced branch may have produced ordinary
                // individual worlds. Capture only exact original Item/UID
                // reciprocal owners for cleanup; no search or ID reacquisition.
                if (Value != null && Value.uid.Value == Case.StoredUids[Index] && Value.parent == null && World != null &&
                    ReferenceEquals(World.item, Value) && World.IsFullySpawned() && World.net != null &&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) &&
                    EntityLifetimes.CaptureCompletedWitness(World) != null) BarrelResearchHold(World);
                Puts("[CarbonLuau:LootFixture] BAG_ITEM index=" + Index + " captured_uid=" + Case.StoredUids[Index] +
                    " current_uid=" + (Value == null ? 0 : Value.uid.Value) + " parent_source=" + (Value != null && ReferenceEquals(Value.parent, Case.Container)) +
                    " parent_bag=" + (Value != null && LootFixtureBag != null && ReferenceEquals(Value.parent, LootFixtureBag.inventory)) +
                    " world_ref=" + (Value != null && !ReferenceEquals(Value.GetWorldEntity(), null)));
            }
        }
        private void RemoveLootFixtureBagPatches()
        {
            if (LootFixtureFaultPatches == null) return;
            if (LootFixtureIndividualMethod != null)
                LootFixtureFaultPatches.Unpatch(LootFixtureIndividualMethod, HarmonyPatchType.All, LootFixtureFaultPatches.Id);
            if (LootFixtureBagMethod != null)
                LootFixtureFaultPatches.Unpatch(LootFixtureBagMethod, HarmonyPatchType.All, LootFixtureFaultPatches.Id);
        }
        private void EndLootQualification(Exception Error)
        {
            bool Clean = false;
            if (Error != null) PrintError("[CarbonLuau:LootFixture] FAIL " + Error);
            try {
                LootFixtureEntries = LootFixtureSecondEntries = null; LootFixtureInjectSpawn = false; BarrelResearchVeto = false;
                LootFixtureCreateFault = 0;
                LootFixtureMutateAmount = LootFixtureForceBag = false;
                LootFixtureBagCase = null;
                RemoveLootFixtureBagPatches();
                if (LootFixtureFaultPatches != null && LootFixtureCreateMethod != null)
                    LootFixtureFaultPatches.Unpatch(LootFixtureCreateMethod, HarmonyPatchType.All, LootFixtureFaultPatches.Id);
                BarrelResearchCleanup();
                LootFixtureBag = null;
                var PreviousWorld = Gameplay;
                ReleaseNative();
                if (LootFixtureRegistry) BarrelResearchCheck(PreviousWorld != null && PreviousWorld.LootRetainedSessionCount == 0,
                    "D6 native/host release converges original world rule-owner retention to zero");
                BarrelResearchCheck(LootCurrent == null && BarrelResearchLedgerCount == 0, "private resources converge");
                Clean = true;
            } catch (Exception Failure) { PrintError("[CarbonLuau:LootFixture] CLEANUP_FAIL " + Failure); }
            if (Error == null && Clean && LootFixtureDone) Puts(LootFixturePublic
                ? "[CarbonLuau:LootFixture] PASS public qualification actual Luau declarations/snapshots, root/addon/recovery, physical replacement/partial/cleanup"
                : "[CarbonLuau:LootFixture] PASS private qualification root/publication/recovery, exact payout/retirement, real failed Spawn ownership and cleanup; no public Loot API");
            if (Error == null && Clean && LootFixtureDone && LootFixtureBarrel2)
                Puts("[CarbonLuau:LootFixture] PASS barrel-2 public qualification full matrix, exact profiles, vanilla rejection, sole owner/shared frame budget, controlled bag branch; natural_bag_branch=false");
            if (Error == null && Clean && LootFixtureItems && LootFixtureItemProbeDone)
                Puts(LootFixtureItemProbeOnly
                    ? "[CarbonLuau:LootFixture] PASS D5 item probe definition/mods, fresh actual Item/world reciprocal completed-Spawn and exact cleanup; public wood remains unqualified"
                    : LootFixtureDone ? "[CarbonLuau:LootFixture] PASS D5 item expansion full public wood matrix, both profiles, exact ownership/failure/budgets and cleanup"
                    : "[CarbonLuau:LootFixture] D5 incomplete matrix");
            if (Error == null && Clean && LootFixtureRegistryDone && LootFixtureDone)
                Puts("[CarbonLuau:LootFixture] PASS D6 bounded per-prefab registry physical/publication/independent ownership/shared budget/failure matrix and cleanup");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit");
        }
    }
}
