// Controlled private D2A qualification only; constructor and receipt seams are
// erased when this fixture is not packaged. No Luau Loot service is introduced.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Security.Cryptography;
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
        partial void SelectPublicLootQualification(ref bool Public);
        private int LootFixtureStep;
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
                    StageLootRule(LootRulePolicy.Prefab, Owner.LootFixtureEntries);
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
            BarrelResearchHold(Value); BarrelResearchHold(World);
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
            BarrelResearchCheck(ReferenceEquals(Frame.Source, BarrelResearchCurrent.Source), "private original source receipt");
            int Worlds = 0;
            for (int Index = 0; Index < Frame.Rule.Count; Index++) {
                Item Value = Frame.Items[Index];
                if (Value == null) continue;
                BarrelResearchHold(Value); BarrelResearchHold(Frame.Worlds[Index]);
                if (!Frame.Accepted[Index]) continue;
                BarrelResearchCheck(LootWorldOwned(Frame, Index), "accepted original Item/UID/epoch/world reciprocal owner");
                if (Success) BarrelResearchCheck(VerifyLootWorld(Frame, Index), "verified planned amount/definition");
                Worlds++;
                Puts("[CarbonLuau:LootFixture] WORLD item=" + Value.info.shortname + " amount=" + Value.amount +
                    " uid=" + Value.uid + " id=" + Frame.Worlds[Index].net.ID.Value + " class=" + Frame.Worlds[Index].GetType().Name);
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
                if (Addons != null) { Addons.Dispose(); Addons = null; }
                Host.Dispose();
                Host = new ScriptHost(Native, new RuntimeConfig { MaxCallbackMilliseconds = 100 }, () => {
                    var Snapshot = new ScriptSnapshot { EntryName = "init.luau", EntrySource = LootFixtureDeclaration() + LootFixtureSource };
                    Snapshot.Modules.Add("state", "return {}");
                    Snapshot.Modules.Add("failed", "error('failed publication fixture')");
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
        private static LootDropEntry[] TwoLootEntries()
        { return new[] { new LootDropEntry("scrap", 7, 7), new LootDropEntry("metal.fragments", 11, 11) }; }
        private void RunLootQualificationStep()
        {
            try {
                // Separate actual owner turns for cleanup/native destruction and
                // producer-budget qualification; never sleep or pump Unity inline.
                if (LootFixtureStep == 0) {
                    LootFixturePayout("root_two", 2, true);
                    var Result = Host.Execute("loot.events", "local S=require('state'); assert((S.Spawned or 0)>=2 and (S.Destroyed or 0)>=1)");
                    BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "B1/B3 callbacks remain usable: " + Result.Error);
                } else if (LootFixtureStep == 1) {
                    var Old = Gameplay.Active;
                    LootFixtureEntries = new[] { new LootDropEntry("scrap", 1000, 1000) };
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
                    int Before = LootFixtureSuccesses;
                    BarrelResearchVeto = true;
                    try { Case.Source.Die(null); } finally { BarrelResearchVeto = false; }
                    BarrelResearchCheck(LootFixtureSuccesses == Before && !Case.Source.IsDestroyed && Case.Source.net != null &&
                        ValidateEntity(Case.Binding), "vetoed retirement remains partial, original not retargeted");
                    Puts("[CarbonLuau:LootFixture] PASS veto partial; release without payout replay");
                    Case.Source.Kill(); BarrelResearchCleanup();
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
                    for (int Index = 0; Index < LootFixtureEntries.Length; Index++) LootFixtureEntries[Index] = new LootDropEntry("scrap", 1000, 1000);
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
                    LootFixtureEntries = new[] { new LootDropEntry("scrap", 5, 12), new LootDropEntry("metal.fragments", 10, 25) };
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "public inclusive range publication");
                } else if (LootFixtureStep == 17) {
                    LootFixturePayout("public_range", 2, true);
                    BarrelResearchCheck(Host.Execute("loot.active", "assert(not pcall(function() game:GetService('Loot'):RegisterDropRule('" + LootRulePolicy.Prefab + "',Enum.DropMode.Replace,{}) end))").Status == RuntimeStatus.OK,
                        "committed public declaration rejected without effects");
                    LootFixtureDone = true; EndLootQualification(null); return;
                }
                LootFixtureStep++; NextTick(RunLootQualificationStep);
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        private string LootFixtureDeclaration()
        {
            if (!LootFixturePublic || LootFixtureEntries == null) return "";
            var Source = new StringBuilder("local L=game:GetService('Loot'); assert(table.isfrozen(Enum) and table.isfrozen(Enum.DropMode)); assert(type(Enum.DropMode.Replace)=='userdata' and tostring(Enum.DropMode.Replace)=='DropMode.Replace'); local D={");
            foreach (var Entry in LootFixtureEntries)
                Source.Append("{Item='").Append(Entry.Item).Append("',Min=").Append(Entry.Min).Append(",Max=").Append(Entry.Max).Append("},");
            Source.Append("}; L:RegisterDropRule('").Append(LootRulePolicy.Prefab).Append("',Enum.DropMode.Replace,D); if D[1] then D[1].Item='invalid'; D[1].Min=999; D[1].Max=0 end; table.clear(D); ");
            return Source.ToString();
        }
        private byte[] LootFixturePackage(string Id, string Source)
        {
            using (var Bytes = new MemoryStream()) {
                using (var Zip = new ZipArchive(Bytes, ZipArchiveMode.Create, true)) {
                    using (var Writer = new StreamWriter(Zip.CreateEntry("addon.json").Open()))
                        Writer.Write("{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"}");
                    using (var Writer = new StreamWriter(Zip.CreateEntry("init.luau").Open())) Writer.Write(LootFixtureDeclaration() + Source);
                }
                return Bytes.ToArray();
            }
        }
        private void LootFixturePayout(string Label, int ExpectedWorlds, bool Success)
        {
            var Case = BarrelResearchCreate(Label, BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
            int Before = LootFixtureReceipts, BeforeSuccess = LootFixtureSuccesses;
            try {
                Case.Source.Hurt(10000f);
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
                    if (Label == "public_range")
                        BarrelResearchCheck((Value.info.shortname == "scrap" && Value.amount >= 5 && Value.amount <= 12) ||
                            (Value.info.shortname == "metal.fragments" && Value.amount >= 10 && Value.amount <= 25), "public inclusive amount range");
                }
                BarrelResearchCheck(Actual == ExpectedWorlds, "no extra vanilla/bonus physical payout " + Label);
                foreach (var Result in Host.Drain()) BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "existing callback regression");
                Puts("[CarbonLuau:LootFixture] PASS case=" + Label + " worlds=" + Actual + " success=" + Success);
            } finally { BarrelResearchCleanup(); }
        }
        private void EndLootQualification(Exception Error)
        {
            bool Clean = false;
            if (Error != null) PrintError("[CarbonLuau:LootFixture] FAIL " + Error);
            try {
                LootFixtureEntries = null; LootFixtureInjectSpawn = false; BarrelResearchVeto = false;
                LootFixtureCreateFault = 0;
                if (LootFixtureFaultPatches != null && LootFixtureCreateMethod != null)
                    LootFixtureFaultPatches.Unpatch(LootFixtureCreateMethod, HarmonyPatchType.All, LootFixtureFaultPatches.Id);
                BarrelResearchCleanup();
                ReleaseNative();
                BarrelResearchCheck(LootCurrent == null && BarrelResearchLedgerCount == 0, "private resources converge");
                Clean = true;
            } catch (Exception Failure) { PrintError("[CarbonLuau:LootFixture] CLEANUP_FAIL " + Failure); }
            if (Error == null && Clean && LootFixtureDone) Puts(LootFixturePublic
                ? "[CarbonLuau:LootFixture] PASS public qualification actual Luau declarations/snapshots, root/addon/recovery, physical replacement/partial/cleanup"
                : "[CarbonLuau:LootFixture] PASS private qualification root/publication/recovery, exact payout/retirement, real failed Spawn ownership and cleanup; no public Loot API");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit");
        }
    }
}
