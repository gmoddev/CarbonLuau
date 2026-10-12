// Qualification package only. Main calls this after the melee fixture cleanup.
// Uses D3's exact-owned barrel/Item ledger; never searches the world.
using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private bool EntityDeathBoundaryRunning, EntityDeathBoundaryPassed;
        private string EntityDeathBoundaryFailure;
        private int EntityDeathBoundaryStep, EntityDeathBoundaryDamageHooks, EntityDeathBoundaryNestedHooks;
        private BaseCombatEntity EntityDeathBoundaryTarget;
        private bool EntityDeathBoundaryDamageVeto, EntityDeathBoundaryNested, EntityDeathBoundaryLateSubtype;
        private static CarbonLuau EntityDeathBoundaryFaultOwner;
        private Harmony EntityDeathBoundaryHarmony;
        private MethodInfo EntityDeathBoundaryFaultMethod;
        private int EntityDeathBoundaryFaultMode, EntityDeathBoundaryFaultCalls;
        private bool EntityDeathBoundaryRestoredInsidePrefix;
        private const string EntityDeathBoundaryNpcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";
        private Harmony EntityDeathBoundaryNpcHarmony;
        private MethodInfo EntityDeathBoundaryNpcCorpseMethod;
        private bool EntityDeathBoundaryNpcVetoEnabled;
        private BasePlayer EntityDeathBoundaryNpc;
        private int EntityDeathBoundaryNpcKillVetoes, EntityDeathBoundaryNpcKillRequests, EntityDeathBoundaryNpcCorpses;
        private bool EntityDeathBoundaryNpcExplicitKill;
        private MethodInfo EntityDeathBoundaryNpcMissionMethod;
        private bool EntityDeathBoundaryNpcMissionThrow;
        private int EntityDeathBoundaryNpcMissionCalls;
        private static CarbonLuau EntityDeathBoundaryHumanOwner;
        private BasePlayer EntityDeathBoundaryHuman;
        private Harmony EntityDeathBoundaryHumanHarmony;
        private MethodInfo EntityDeathBoundaryHumanCorpseMethod;
        private int EntityDeathBoundaryHumanCorpses;
        private static CarbonLuau EntityDeathBoundaryBagOwner;
        private Harmony EntityDeathBoundaryBagHarmony;
        private MethodInfo EntityDeathBoundaryBagMethod;
        private BarrelResearchCase EntityDeathBoundaryBagCase;
        private int EntityDeathBoundaryBagReturns;

        private object OnEntityTakeDamage(BaseCombatEntity Entity, HitInfo Hit)
        {
            if (!EntityDeathBoundaryRunning || !EntityDeathBoundaryDamageVeto ||
                !ReferenceEquals(Entity, EntityDeathBoundaryTarget)) return null;
            BarrelResearchCheck(BarrelResearchOnOwner(), "damage veto owner/no VM entry");
            EntityDeathBoundaryDamageHooks++;
            return true;
        }

        private void OnEntityDeath(BaseCombatEntity Entity, HitInfo Hit)
        {
            if (!EntityDeathBoundaryRunning ||
                !ReferenceEquals(Entity, EntityDeathBoundaryTarget)) return;
            BarrelResearchCheck(BarrelResearchOnOwner(), "nested death owner/no VM entry");
            if (EntityDeathBoundaryLateSubtype) {
                EntityDeathBoundaryLateSubtype = false;
                EntityDeathBoundaryInstallLateSubtype();
                return;
            }
            if (!EntityDeathBoundaryNested) return;
            EntityDeathBoundaryNested = false; // One actual nested call, never recursion without a cap.
            EntityDeathBoundaryNestedHooks++;
            Entity.Die(Hit);
        }

        private void RunEntityDeathBoundaryQualification()
        {
            if (EntityDeathBoundaryRunning) throw new InvalidOperationException("duplicate death boundary entry");
            EntityDeathBoundaryPassed = false;
            EntityDeathBoundaryFailure = null;
            EntityDeathBoundaryStep = 0;
            EntityDeathBoundaryRunning = true;
            try {
                BarrelResearchGuard();
                BarrelResearchCheck(BarrelResearchLedgerCount == 0 && BarrelResearchCurrent == null &&
                    BarrelResearchActor == null && Host.Ready && !Host.Busy && EntityDiedAvailable(),
                    "boundary entry follows owned melee cleanup with qualified source");
                EntityDeathBoundaryDrain();
                EntityDeathBoundaryExecute("setup",
                    "local S=require('state'); assert(not S.B2Boundary); local W=game:GetService('Workspace'); " +
                    "local B={Watched={},D={},X={},ExpectedType={},Typed={}}; S.B2Boundary=B; " +
                    "B.Died=W.EntityDied:Connect(function(C) if B.Watched[C.Id] then " +
                    "assert(table.isfrozen(C) and C.Prefab==B.Watched[C.Id]); " +
                    "local T=B.ExpectedType[C.Id]; if T then assert(C.DamageType==T and C.Killer==nil " +
                    "and C.KillerId==nil and C.Weapon==nil); B.Typed[C.Id]=(B.Typed[C.Id] or 0)+1 end; " +
                    "B.D[C.Id]=(B.D[C.Id] or 0)+1 end end); " +
                    "B.Destroyed=W.EntityDestroyed:Connect(function(C) if B.Watched[C.Id] then " +
                    "assert(table.isfrozen(C)); B.X[C.Id]=(B.X[C.Id] or 0)+1 end end)");
                NextTick(RunEntityDeathBoundaryStep);
            } catch (Exception Error) { FinishEntityDeathBoundaryQualification(Error); }
        }

        private void RunEntityDeathBoundaryStep()
        {
            try {
                BarrelResearchGuard();
                BarrelResearchCheck(EntityDiedAvailable(), "qualified source before each boundary case");
                if (EntityDeathBoundaryStep == 15) {
                    EntityDeathBoundaryDrain();
                    FinishEntityDeathBoundaryQualification(null);
                    return;
                }
                if (EntityDeathBoundaryStep == 9) {
                    RunEntityDeathBoundaryNpc();
                    return;
                }
                if (EntityDeathBoundaryStep == 13) {
                    RunEntityDeathBoundaryHuman();
                    EntityDeathBoundaryStep++;
                    NextTick(RunEntityDeathBoundaryStep);
                    return;
                }
                string[] Labels = { "nonlethal", "damage_veto", "ordinary_kill", "terminal_d3",
                    "kill_veto_repeat", "nested_die", "foreign_skip", "foreign_prefix_throw", "foreign_postfix_throw",
                    "npc_full_respawn", "actorless_decay_die_context", "actorless_explosion_die_context", "late_subtype_skip",
                    "human_shared_finalizer", "late_subtype_skip_self_restore" };
                string Label = Labels[EntityDeathBoundaryStep];
                var Case = BarrelResearchCreate("b2_" + Label, BarrelResearchMode.Vanilla, BarrelResearchPosition, true);
                EntityDeathBoundaryTarget = Case.Source;
                string Id = Case.Id.ToString(CultureInfo.InvariantCulture);
                string Prefab = Case.Source.PrefabName;
                BarrelResearchCheck(Prefab == Case.Prefab && Prefab == Case.Binding.Lifetime.Record.Prefab,
                    "boundary watch captures the exact acquired original source prefab before death");
                EntityDeathBoundaryExecute("watch", "local B=require('state').B2Boundary; B.Watched['" + Id + "']=" + EntityMeleeFixtureLuaText(Prefab));
                string ExpectedType = EntityDeathBoundaryStep == 10 ? "Decay" : EntityDeathBoundaryStep == 11 ? "Explosion" : null;
                if (ExpectedType != null)
                    EntityDeathBoundaryExecute("expect_type", "require('state').B2Boundary.ExpectedType['" + Id + "']='" + ExpectedType + "'");
                EntityDeathBoundaryDrain(); // Clear the real Spawn notification, not any death history.
                long Completed = EntityDeathCompleted;
                int Captured = EntityDiedFixtureObservations;
                ulong Attempted = Host.Attempted;
                ulong Accepted = Gameplay.GameplayEvents.Accepted;
                int Deaths = 0, Destroyed = 0;
                Exception CaseError = null;
                try {
                    EntityDeathBoundaryInstallBagCapture(Case);
                    if (EntityDeathBoundaryStep == 0) {
                        float Health = Case.Source.health;
                        Case.Source.Hurt(1f, Rust.DamageType.Generic, null, false);
                        BarrelResearchCheck(Case.Source.health < Health && Case.Source.health > 0 &&
                            Case.Source.lifestate != BaseCombatEntity.LifeState.Dead && ValidateEntity(Case.Binding),
                            "actual nonlethal Hurt changed health without terminal death");
                    } else if (EntityDeathBoundaryStep == 1) {
                        float Health = Case.Source.health;
                        EntityDeathBoundaryDamageHooks = 0;
                        EntityDeathBoundaryDamageVeto = true;
                        try { Case.Source.Hurt(10000f, Rust.DamageType.Generic, null, false); }
                        finally { EntityDeathBoundaryDamageVeto = false; }
                        BarrelResearchCheck(EntityDeathBoundaryDamageHooks == 1 && Case.Source.health == Health &&
                            Case.Source.lifestate != BaseCombatEntity.LifeState.Dead && ValidateEntity(Case.Binding),
                            "real OnEntityTakeDamage veto preserves health and original lifetime");
                    } else if (EntityDeathBoundaryStep == 2) {
                        Case.Source.Kill();
                        BarrelResearchRetired(Case);
                        Destroyed = 1;
                    } else if (EntityDeathBoundaryStep == 3) {
                        int Receipts = LootFixtureReceipts, Successes = LootFixtureSuccesses;
                        Case.Source.Hurt(10000f, Rust.DamageType.Generic, null, false);
                        BarrelResearchRetired(Case);
                        BarrelResearchCheck(LootFixtureReceipts == Receipts + 1 && LootFixtureSuccesses == Successes + 1,
                            "real terminal Hurt preserves D3 successful replacement");
                        EntityDeathBoundaryWorlds(2);
                        Deaths = Destroyed = 1;
                    } else if (EntityDeathBoundaryStep == 4) {
                        int Receipts = LootFixtureReceipts, Successes = LootFixtureSuccesses;
                        BarrelResearchVeto = true;
                        try {
                            Case.Source.Die(null);
                            BarrelResearchCheck(Case.Source.lifestate == BaseCombatEntity.LifeState.Dead &&
                                !Case.Source.IsDestroyed && ValidateEntity(Case.Binding),
                                "completed terminal Die with vetoed Kill remains keyed/live");
                            Case.Source.Die(null); // Actual early-return repeat on the still-live object.
                        } finally { BarrelResearchVeto = false; }
                        BarrelResearchCheck(LootFixtureReceipts == Receipts + 1 && LootFixtureSuccesses == Successes,
                            "vetoed removal is D3 partial, repeated Die never replays payout");
                        EntityDeathBoundaryWorlds(2);
                        Deaths = 1;
                    } else if (EntityDeathBoundaryStep == 5) {
                        EntityDeathBoundaryNestedHooks = 0;
                        EntityDeathBoundaryNested = true;
                        try { Case.Source.Die(null); }
                        finally { EntityDeathBoundaryNested = false; }
                        BarrelResearchCheck(EntityDeathBoundaryNestedHooks == 1,
                            "actual Carbon death hook entered one nested original Die");
                        BarrelResearchRetired(Case);
                        Deaths = Destroyed = 1;
                    } else if (ExpectedType != null) {
                        if (EntityDeathBoundaryStep == 10) {
                            DestroyWatch EntryWatch;
                            bool HasWatch = DestroyWatches.TryGetValue(Case.Source, out EntryWatch);
                            MethodInfo Effective;
                            bool KnownType = EntityDeathEffective.TryGetValue(Case.Source.GetType(), out Effective);
                            bool IdValid = Case.Source.net != null && Case.Source.net.ID.Value == Case.Id &&
                                ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Case.Id)), Case.Source);
                            Puts("[CarbonLuau:EntityDeathBoundary] PRE_DIE_DIAGNOSTIC step=10" +
                                " SourceAvailable=" + EntityDiedAvailable() + " SourceArmed=" + EntityDeathArmed +
                                " SourceStopped=" + EntityDeathStopped + " Startup=" + EntityStartupQualified +
                                " ObserverBroken=" + EntityObserverBroken + " HostReady=" + (Host != null && Host.Ready) +
                                " ActiveObserverSame=" + ReferenceEquals(ActiveEntityObserver, this) +
                                " ActiveObserverNull=" + ReferenceEquals(ActiveEntityObserver, null) +
                                " EffectiveType=" + (KnownType ? Effective.DeclaringType.FullName : "unknown") +
                                " ConcreteType=" + Case.Source.GetType().FullName + " HasWatch=" + HasWatch +
                                " WatchDeathSeen=" + (HasWatch && EntryWatch.DeathSeen) +
                                " WatchClosed=" + (HasWatch && EntryWatch.Closed) +
                                " WatchEpochCurrent=" + (HasWatch && EntityLifetimes.IsWitnessEpochUnchanged(EntryWatch.Witness)) +
                                " LifeState=" + Case.Source.lifestate + " Id=" + Id + " IdValid=" + IdValid +
                                " BindingCurrent=" + ValidateEntity(Case.Binding) + " Frames=" + EntityDeathFrames.Count);
                        }
                        // A barrel may resist Decay Hurt. Invoke the actual full
                        // terminal method with an explicit death-associated hit;
                        // prove context labels, not autonomous damage provenance.
                        var Hit = new HitInfo { Initiator = null };
                        try {
                            Hit.damageTypes.Set(EntityDeathBoundaryStep == 10 ? Rust.DamageType.Decay : Rust.DamageType.Explosion, 1f);
                            Case.Source.Die(Hit);
                        } finally { Hit.Clear(); }
                        BarrelResearchRetired(Case);
                        Deaths = Destroyed = 1;
                    } else if (EntityDeathBoundaryStep == 12 || EntityDeathBoundaryStep == 14) {
                        bool SelfRestore = EntityDeathBoundaryStep == 14;
                        EntityDeathBoundaryLateSubtype = true;
                        try { Case.Source.Die(null); }
                        finally { EntityDeathBoundaryLateSubtype = false; }
                        BarrelResearchCheck((SelfRestore ? EntityDeathBoundaryHarmony == null && EntityDeathBoundaryRestoredInsidePrefix :
                            EntityDeathBoundaryHarmony != null) && EntityDeathBoundaryFaultCalls == 1 &&
                            EntityDeathCompleted == Completed && EntityDiedFixtureObservations == Captured &&
                            EntityDeathFrames.Count == 0 && EntityDiedAvailable(),
                            "post-entry subtype skip rejected at completion, including self-restored current topology");
                        BarrelResearchCheck(Case.Source.lifestate == BaseCombatEntity.LifeState.Dead &&
                            !Case.Source.IsDestroyed && ValidateEntity(Case.Binding),
                            "real original base Die returned dead/keyed despite skipped subtype OnDied");
                        // Persistent variant survives the original return/assertions.
                        // Self-restoring variant already removed itself before false.
                        EntityDeathBoundaryRemoveFault();
                        BarrelResearchCheck(EntityDiedAvailable() && EntityDeathTopology(EntityDeathTypeMethods[typeof(LootContainer)]),
                            "complete subtype topology restored after late-skip rejection");
                    } else {
                        EntityDeathBoundaryInstallFault(EntityDeathBoundaryStep - 5);
                        bool Threw = false;
                        try { Case.Source.Die(null); }
                        catch (InvalidOperationException Error) {
                            if (Error.Message != "B2 owned death boundary fault") throw;
                            Threw = true;
                        }
                        BarrelResearchCheck(EntityDeathBoundaryFaultCalls == 1 &&
                            Threw == (EntityDeathBoundaryFaultMode != 1),
                            "actual owned original dispatch skipped/threw as requested");
                        if (EntityDeathBoundaryFaultMode != 3)
                            BarrelResearchCheck(Case.Source.lifestate != BaseCombatEntity.LifeState.Dead &&
                                ValidateEntity(Case.Binding), "skipped/throwing prefix never performed terminal work");
                        else {
                            BarrelResearchRetired(Case);
                            Destroyed = 1;
                        }
                        EntityDeathBoundaryRemoveFault();
                        BarrelResearchCheck(EntityDiedAvailable(), "source topology restored after exact-owned unpatch");
                    }
                    bool SourcePresent = !ReferenceEquals(Case.Source, null);
                    bool RegistryExact = SourcePresent && ReferenceEquals(
                        BaseNetworkable.serverEntities.Find(new NetworkableId(Case.Id)), Case.Source);
                    Puts("[CarbonLuau:EntityDeathBoundary] CASE_DIAGNOSTIC case=" + Label +
                        " CompletedDelta=" + (EntityDeathCompleted - Completed) + " CapturedDelta=" + (EntityDiedFixtureObservations - Captured) +
                        " ExpectedDeaths=" + Deaths + " DeathFrames=" + EntityDeathFrames.Count + " AFrames=" + GameplayFrames.Count +
                        " SourceArmed=" + EntityDeathArmed + " SourceAvailable=" + EntityDiedAvailable() +
                        " AttemptedDelta=" + (Host.Attempted - Attempted) + " Busy=" + Host.Busy +
                        " Dead=" + (SourcePresent && Case.Source.lifestate == BaseCombatEntity.LifeState.Dead) +
                        " Destroyed=" + (SourcePresent && Case.Source.IsDestroyed) +
                        " NetPresent=" + (SourcePresent && Case.Source.net != null) + " RegistryExact=" + RegistryExact +
                        " AcceptedDelta=" + (Gameplay.GameplayEvents.Accepted - Accepted) +
                        " Pending=" + Gameplay.GameplayEvents.PendingCount + " source_status=" + EntityDiedStatus);
                    BarrelResearchCheck(Host.Attempted == Attempted && !Host.Busy,
                        "host terminal observation never entered Luau inline");
                    BarrelResearchCheck(EntityDeathCompleted == Completed + Deaths &&
                        EntityDiedFixtureObservations == Captured + Deaths,
                        "exact private completed/captured count " + Label);
                    if (Deaths != 0)
                        BarrelResearchCheck(Gameplay.GameplayEvents.Accepted > Accepted &&
                            Gameplay.GameplayEvents.PendingCount > 0, "real death entered bounded deferred transport");
                    EntityDeathBoundaryAssertPublic(Id, 0, 0); // Still no callback before owner-thread drain.
                    EntityDeathBoundaryDrain();
                    EntityDeathBoundaryAssertPublic(Id, Deaths, Destroyed);
                    if (ExpectedType != null)
                        EntityDeathBoundaryExecute("typed_delivery", "assert(require('state').B2Boundary.Typed['" + Id + "']==1)");
                    if (EntityDeathBoundaryStep == 4) {
                        long BeforeRelease = EntityDeathCompleted;
                        int BeforeCapture = EntityDiedFixtureObservations;
                        ulong BeforeEntry = Host.Attempted;
                        Case.Source.Kill();
                        BarrelResearchRetired(Case);
                        BarrelResearchCheck(Host.Attempted == BeforeEntry && EntityDeathCompleted == BeforeRelease &&
                            EntityDiedFixtureObservations == BeforeCapture,
                            "later actual Kill produces destruction, never another death");
                        EntityDeathBoundaryDrain();
                        EntityDeathBoundaryAssertPublic(Id, 1, 1);
                    }
                    Puts("[CarbonLuau:EntityDeathBoundary] PASS case=" + Label + " completed=" + Deaths +
                        " captured=" + Deaths + " public_death=" + Deaths + " public_destroy=" +
                        (EntityDeathBoundaryStep == 4 ? 1 : Destroyed) +
                        (ExpectedType == null ? "" : " published_type=" + ExpectedType + " killer=nil weapon=nil"));
                } catch (Exception Error) {
                    CaseError = Error;
                    Puts("[CarbonLuau:EntityDeathBoundary] CASE_FAIL case=" + Label + " original=" +
                        Error.GetType().Name + ": " + Error.Message);
                    throw;
                } finally {
                    EntityDeathBoundaryDamageVeto = EntityDeathBoundaryNested = EntityDeathBoundaryLateSubtype = false;
                    try {
                        EntityDeathBoundaryRemoveFault();
                        EntityDeathBoundaryTarget = null;
                        // Let an owned live barrel complete ordinary Kill while
                        // its exact-container bag-return observer is still active.
                        // Acquire any bag before the ledger's cleanup iteration.
                        if (Case.Source != null && !Case.Source.IsDestroyed) Case.Source.Kill();
                        BarrelResearchCleanup();
                    } catch (Exception CleanupError) {
                        Puts("[CarbonLuau:EntityDeathBoundary] CASE_CLEANUP_FAIL case=" + Label + " cleanup=" +
                            CleanupError.GetType().Name + ": " + CleanupError.Message);
                        if (CaseError == null) throw; // Keep an already-in-flight original case failure.
                    } finally {
                        EntityDeathBoundaryRemoveBagCapture();
                    }
                }
                EntityDeathBoundaryDrain();
                BarrelResearchCheck(BarrelResearchLedgerCount == 0 && EntityDeathFrames.Count == 0,
                    "owned ledger/death borrowed frames converge after case");
                EntityDeathBoundaryStep++;
                NextTick(RunEntityDeathBoundaryStep);
            } catch (Exception Error) { FinishEntityDeathBoundaryQualification(Error); }
        }

        private void EntityDeathBoundaryExecute(string Label, string Source)
        {
            var Result = Host.Execute("b2.boundary." + Label, Source);
            BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "public boundary assertion " + Label + ": " + Result.Error);
        }
        private void EntityDeathBoundaryInstallBagCapture(BarrelResearchCase Case)
        {
            BarrelResearchCheck(EntityDeathBoundaryBagHarmony == null && EntityDeathBoundaryBagOwner == null,
                "no duplicate owned bag-return observer");
            EntityDeathBoundaryBagMethod = typeof(ItemContainer).GetMethod("Drop", BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly, null,
                new[] { typeof(string), typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion), typeof(ItemContainer[]) }, null);
            BarrelResearchCheck(EntityDeathBoundaryBagMethod != null &&
                EntityDeathBoundaryBagMethod.ReturnType == typeof(DroppedItemContainer) &&
                EntityDeathBoundaryBagMethod.GetMethodBody() != null, "exact implemented static container Drop return seam");
            EntityDeathBoundaryBagOwner = this;
            EntityDeathBoundaryBagCase = Case;
            EntityDeathBoundaryBagReturns = 0;
            EntityDeathBoundaryBagHarmony = new Harmony("CarbonLuau.Test.EntityDeathBag." + Guid.NewGuid().ToString("N"));
            EntityDeathBoundaryBagHarmony.Patch(EntityDeathBoundaryBagMethod, postfix: new HarmonyMethod(
                typeof(CarbonLuau).GetMethod(nameof(EntityDeathBoundaryBagPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static void EntityDeathBoundaryBagPostfix(ItemContainer[] __3, DroppedItemContainer __result)
        {
            var Owner = EntityDeathBoundaryBagOwner;
            if (Owner == null || ReferenceEquals(__result, null) || __3 == null) return;
            var Case = Owner.EntityDeathBoundaryBagCase;
            if (Case == null) return;
            if (__3.Length > BarrelResearchItemLimit) return; // Never affect an unrelated oversized call.
            bool Exact = false;
            foreach (var Container in __3) if (ReferenceEquals(Container, Case.Container)) Exact = true;
            if (!Exact) return; // No proximity/ID/current-parent inference or unrelated bags.
            BarrelResearchCheck(Owner.BarrelResearchOnOwner(), "owned returned bag owner/no VM entry");
            Owner.BarrelResearchHold(__result);
            __result.enableSaving = false;
            BarrelResearchCheck(++Owner.EntityDeathBoundaryBagReturns <= 1, "bounded exact-container returned bag");
            Owner.BarrelResearchInventory(__result.inventory, null, false);
            Owner.Puts("[CarbonLuau:EntityDeathBoundary] OWNED_BAG case=" + Case.Label +
                " id=" + (__result.net == null ? "nil" : __result.net.ID.Value.ToString(CultureInfo.InvariantCulture)) +
                " items=" + __result.inventory.itemList.Count + " provenance=original_container_Drop_return");
        }
        private void EntityDeathBoundaryRemoveBagCapture()
        {
            if (EntityDeathBoundaryBagHarmony != null && EntityDeathBoundaryBagMethod != null)
                EntityDeathBoundaryBagHarmony.Unpatch(EntityDeathBoundaryBagMethod, HarmonyPatchType.All, EntityDeathBoundaryBagHarmony.Id);
            EntityDeathBoundaryBagHarmony = null;
            EntityDeathBoundaryBagMethod = null;
            EntityDeathBoundaryBagCase = null;
            EntityDeathBoundaryBagOwner = null;
        }
        private void EntityDeathBoundaryAssertPublic(string Id, int Died, int Destroyed)
        {
            EntityDeathBoundaryExecute("counts", "local B=require('state').B2Boundary; local D=B.D['" + Id +
                "']; local X=B.X['" + Id + "']; assert((D or 0)==" + Died + " and (X or 0)==" + Destroyed +
                ", 'B2 public counts Id=" + Id + " Died='..tostring(D)..' Destroyed='..tostring(X).." +
                "' EffectiveDied='..tostring(D or 0)..' EffectiveDestroyed='..tostring(X or 0).." +
                "' ExpectedDied=" + Died + " ExpectedDestroyed=" + Destroyed +
                " SourcesType='..type(B)..'/'..type(B.D)..'/'..type(B.X).." +
                "' WatchedPrefab='..tostring(B.Watched['" + Id + "']))");
        }
        private void EntityDeathBoundaryDrain()
        {
            for (int Turn = 0; Turn < 16 && Host.HasReadyWork; Turn++)
                foreach (var Result in Host.Drain())
                    BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "deferred boundary callback: " + Result.Error);
            BarrelResearchCheck(!Host.HasReadyWork && Gameplay.GameplayEvents.PendingCount == 0 &&
                Gameplay.GameplayEvents.RetainedBytes == 0, "bounded callback/resource drain converged");
        }
        private void EntityDeathBoundaryWorlds(int Expected)
        {
            int Count = 0;
            for (int Index = 0; Index < BarrelResearchLedgerCount; Index++) {
                Item Value = BarrelResearchLedger[Index].Item;
                if (Value == null || Value.uid.Value == 0 || Value.IsRemoved() || Value.parent != null) continue;
                WorldItem World = Value.GetWorldEntity() as WorldItem;
                if (World == null) continue;
                BarrelResearchCheck(Value.uid.Value == BarrelResearchLedger[Index].ItemUid &&
                    ReferenceEquals(World.item, Value) && !World.IsDestroyed && World.IsFullySpawned(),
                    "physical payout still has exact reciprocal owned world");
                Count++;
            }
            BarrelResearchCheck(Count == Expected, "D3 exactly two physical payouts, no vanilla escape");
        }

        private void EntityDeathBoundaryInstallFault(int Mode)
        {
            BarrelResearchCheck(EntityDeathBoundaryHarmony == null && EntityDeathBoundaryFaultOwner == null,
                "no duplicate temporary fault patch");
            EntityDeathBoundaryFaultMode = Mode;
            EntityDeathBoundaryFaultCalls = 0;
            EntityDeathBoundaryFaultMethod = typeof(BaseCombatEntity).GetMethod("Die", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(HitInfo) }, null);
            BarrelResearchCheck(EntityDeathBoundaryFaultMethod != null && EntityDeathPinned(EntityDeathBoundaryFaultMethod),
                "fault target is exact pinned original BaseCombatEntity.Die");
            EntityDeathBoundaryFaultOwner = this;
            EntityDeathBoundaryHarmony = new Harmony("CarbonLuau.Test.EntityDeathBoundary." + Guid.NewGuid().ToString("N"));
            string Name = Mode == 3 ? nameof(EntityDeathBoundaryFaultPostfix) : nameof(EntityDeathBoundaryFaultPrefix);
            var Patch = new HarmonyMethod(typeof(CarbonLuau).GetMethod(Name, BindingFlags.Static | BindingFlags.NonPublic));
            Patch.priority = Mode == 3 ? Priority.Last : Priority.First;
            if (Mode == 3) EntityDeathBoundaryHarmony.Patch(EntityDeathBoundaryFaultMethod, postfix: Patch);
            else EntityDeathBoundaryHarmony.Patch(EntityDeathBoundaryFaultMethod, prefix: Patch);
            BarrelResearchCheck(!EntityDiedAvailable(), "foreign death patch fails source topology closed");
        }
        private void EntityDeathBoundaryInstallLateSubtype()
        {
            BarrelResearchCheck(EntityDeathBoundaryHarmony == null && EntityDeathFrames.Count == 1 &&
                ReferenceEquals(EntityDeathFrames[0].Entity, EntityDeathBoundaryTarget),
                "real death hook runs after original B2 entry with exact active frame");
            EntityDeathBoundaryFaultMethod = typeof(LootContainer).GetMethod("OnDied", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(HitInfo) }, null);
            BarrelResearchCheck(EntityDeathBoundaryFaultMethod != null && EntityDeathPinned(EntityDeathBoundaryFaultMethod) &&
                EntityDeathFrames[0].CriticalMethods != null && Array.Exists(EntityDeathFrames[0].CriticalMethods,
                    Method => SameEntityMethod(Method, EntityDeathBoundaryFaultMethod)),
                "entry captured the exact pinned subtype method being changed");
            EntityDeathBoundaryFaultMode = EntityDeathBoundaryStep == 14 ? 5 : 4;
            EntityDeathBoundaryFaultCalls = 0;
            EntityDeathBoundaryRestoredInsidePrefix = false;
            EntityDeathBoundaryFaultOwner = this;
            EntityDeathBoundaryHarmony = new Harmony("CarbonLuau.Test.EntityDeathLateSubtype." + Guid.NewGuid().ToString("N"));
            EntityDeathBoundaryHarmony.Patch(EntityDeathBoundaryFaultMethod, prefix: new HarmonyMethod(
                typeof(CarbonLuau).GetMethod(nameof(EntityDeathBoundaryFaultPrefix), BindingFlags.Static | BindingFlags.NonPublic))
                { priority = Priority.First });
            BarrelResearchCheck(EntityDiedAvailable() && !EntityDeathTopology(EntityDeathFrames[0].CriticalMethods),
                "global target check alone misses late subtype interference; captured critical check rejects it");
        }
        private static bool EntityDeathBoundaryFaultPrefix(BaseCombatEntity __instance)
        {
            var Owner = EntityDeathBoundaryFaultOwner;
            if (Owner == null || !ReferenceEquals(__instance, Owner.EntityDeathBoundaryTarget)) return true;
            Owner.EntityDeathBoundaryFaultCalls++;
            if (Owner.EntityDeathBoundaryFaultMode == 2) throw new InvalidOperationException("B2 owned death boundary fault");
            if (Owner.EntityDeathBoundaryFaultMode == 5) {
                BarrelResearchCheck(Owner.EntityDeathFrames.Count == 1 &&
                    ReferenceEquals(Owner.EntityDeathFrames[0].Entity, __instance), "exact pending late-skip attempt");
                var Critical = Owner.EntityDeathFrames[0].CriticalMethods;
                Owner.EntityDeathBoundaryRemoveFault();
                // Deliberately refresh the shared cache after restoration. A
                // completion check against only current/cache topology is wrong.
                BarrelResearchCheck(Critical != null && Owner.EntityDiedAvailable() && Owner.EntityDeathTopology(Critical),
                    "current subtype topology restored/refreshed inside skipping prefix");
                Owner.EntityDeathBoundaryRestoredInsidePrefix = true;
            }
            return false;
        }
        private static void EntityDeathBoundaryFaultPostfix(BaseCombatEntity __instance)
        {
            var Owner = EntityDeathBoundaryFaultOwner;
            if (Owner == null || !ReferenceEquals(__instance, Owner.EntityDeathBoundaryTarget)) return;
            Owner.EntityDeathBoundaryFaultCalls++;
            throw new InvalidOperationException("B2 owned death boundary fault");
        }
        private void EntityDeathBoundaryRemoveFault()
        {
            if (EntityDeathBoundaryHarmony != null && EntityDeathBoundaryFaultMethod != null)
                EntityDeathBoundaryHarmony.Unpatch(EntityDeathBoundaryFaultMethod, HarmonyPatchType.All, EntityDeathBoundaryHarmony.Id);
            EntityDeathBoundaryHarmony = null;
            EntityDeathBoundaryFaultMethod = null;
            EntityDeathBoundaryFaultOwner = null;
        }

        private void RunEntityDeathBoundaryNpc()
        {
            bool Scheduled=false;
            try {
                BaseEntity Entity = GameManager.server.CreateEntity(EntityDeathBoundaryNpcPrefab,
                    BarrelResearchPosition, UnityEngine.Quaternion.identity, true);
                BarrelResearchHold(Entity);
                EntityDeathBoundaryNpc = Entity as BasePlayer;
                BarrelResearchCheck(EntityDeathBoundaryNpc != null && EntityDeathBoundaryNpc.IsNpc,
                    "existing A scientist prefab produces exact owned NPC");
                var Npc = EntityDeathBoundaryNpc;
                BarrelResearchActor = Npc; // Reuse existing zero-external-player scene guard.
                Npc.enableSaving = false;
                Npc.userID = 76561198000000119;
                Npc.UserIDString = "76561198000000119";
                Npc.displayName = "B2 NPC reset fixture";
                Npc.Spawn();
                BarrelResearchHold(Npc);
                BarrelResearchCheck(Npc.net != null && Npc.net.connection == null && CurrentGameplayPlayer(Npc) == null,
                    "owned NPC has no human D11 authority or authenticated connection");
                EntityFacadeBinding Binding;
                BarrelResearchCheck(TryAdmitEntity(Npc, Gameplay.Active, out Binding), "completed original NPC D20 admission");
                DestroyWatch Watch;
                BarrelResearchCheck(DestroyWatches.TryGetValue(Npc, out Watch) && !Watch.DeathSeen,
                    "original NPC watch begins unlatched");
                string Id = Npc.net.ID.Value.ToString(CultureInfo.InvariantCulture);
                ulong Epoch = Binding.Lifetime.Record.Epoch;
                EntityDeathBoundaryExecute("npc_watch", "require('state').B2Boundary.Watched['" + Id +
                    "']='" + EntityDeathBoundaryNpcPrefab + "'");
                EntityDeathBoundaryDrain();

                // Hold this exact NPC through actual Kill rejection. Capture actual
                // returned corpses into the existing ledger; no global spawn search.
                EntityDeathBoundaryNpcCorpseMethod = Npc.GetType().GetMethod("CreateCorpse", BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(BasePlayer.PlayerFlags),
                        typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion),
                        typeof(System.Collections.Generic.List<TriggerBase>), typeof(bool) }, null);
                if (EntityDeathBoundaryNpcCorpseMethod != null)
                    EntityDeathBoundaryNpcCorpseMethod = EntityDeathBoundaryNpcCorpseMethod.Module.ResolveMethod(
                        EntityDeathBoundaryNpcCorpseMethod.MetadataToken) as MethodInfo;
                EntityDeathBoundaryNpcCorpseMethod = EntityDeathBoundaryDeclaredCorpse(EntityDeathBoundaryNpcCorpseMethod);
                BarrelResearchCheck(EntityDeathBoundaryNpcCorpseMethod != null &&
                    typeof(BaseCorpse).IsAssignableFrom(EntityDeathBoundaryNpcCorpseMethod.ReturnType),
                    "exact owned NPC CreateCorpse signature");
                EntityDeathBoundaryFaultOwner = this;
                EntityDeathBoundaryNpcKillVetoes = EntityDeathBoundaryNpcKillRequests = EntityDeathBoundaryNpcCorpses = 0;
                EntityDeathBoundaryNpcHarmony = new Harmony("CarbonLuau.Test.EntityDeathNpc." + Guid.NewGuid().ToString("N"));
                EntityDeathBoundaryNpcVetoEnabled = true;
                EntityDeathBoundaryNpcHarmony.Patch(EntityDeathBoundaryNpcCorpseMethod, postfix: new HarmonyMethod(
                    typeof(CarbonLuau).GetMethod(nameof(EntityDeathBoundaryNpcCorpsePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                BarrelResearchCheck(EntityDiedAvailable(), "NPC retention adapter does not invalidate death source");
                long Completed = EntityDeathCompleted;
                int Captured = EntityDiedFixtureObservations;
                ulong Attempted = Host.Attempted;
                long BudgetStart = System.Diagnostics.Stopwatch.GetTimestamp();
                EntityDeathBoundaryNpcBudget("before_first_death", BudgetStart);
                Npc.Die(null);
                BarrelResearchCheck(EntityDeathBoundaryNpcKillRequests == 0,
                    "first full NPC Die made no inline Kill attempt on this exact host");
                EntityDeathBoundaryNpcExplicitKill=true;
                try { Npc.Kill(); } finally { EntityDeathBoundaryNpcExplicitKill=false; }
                BarrelResearchCheck(EntityDeathCompleted == Completed + 1 && EntityDiedFixtureObservations == Captured + 1 &&
                    Watch.DeathSeen && Npc.lifestate == BaseCombatEntity.LifeState.Dead && !Npc.IsDestroyed &&
                    EntityDeathBoundaryNpcKillVetoes == 1 && ValidateEntity(Binding) && Host.Attempted == Attempted,
                    "actual NPC full Die then explicit scoped Kill veto: no removal, extra death or inline callback");
                EntityDeathBoundaryNpcBudget("after_first_death", BudgetStart);
                Npc.Die(null);
                BarrelResearchCheck(EntityDeathCompleted == Completed + 1 && EntityDiedFixtureObservations == Captured + 1,
                    "actual repeated dead NPC Die never duplicates");
                BarrelResearchCheck(Host.Attempted==Attempted, "first NPC phase has no inline Lua entry");
                EntityDeathBoundaryAssertPublic(Id,0,0);
                EntityDeathBoundaryDrain();
                EntityDeathBoundaryAssertPublic(Id,1,0);
                int FirstFrame=UnityEngine.Time.frameCount, Waits=0;
                Action Continue=null;
                Continue=()=> {
                    if (!EntityDeathBoundaryRunning) { Continue=null; return; }
                    if (UnityEngine.Time.frameCount==FirstFrame) {
                        if (++Waits<=8) { NextTick(Continue); return; }
                        try { CleanupEntityDeathBoundaryNpc(); }
                        catch (Exception Error) { FinishEntityDeathBoundaryQualification(Error); Continue=null; return; }
                        FinishEntityDeathBoundaryQualification(new InvalidOperationException("NPC actual-frame continuation exceeded eight waits"));
                        Continue=null; return;
                    }
                    Exception Failure=null;
                    try {
                        BarrelResearchGuard();
                        BarrelResearchCheck(EntityDiedAvailable() && ValidateEntity(Binding) &&
                            ReferenceEquals(Npc,EntityDeathBoundaryNpc) && EntityDeathFrames.Count==0,
                            "next actual frame retains only the original owned NPC lifetime");
                        Attempted=Host.Attempted;
                string RespawnId = Npc.respawnId;
                // A's already-proved mission tail is after InitializeHealth inside
                // the actual full RespawnAt. Only this exact owned NPC throws.
                EntityDeathBoundaryNpcMissionMethod = typeof(BasePlayer).GetMethod("ProcessMissionEvent",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { typeof(BaseMission.MissionEventType), typeof(int), typeof(float) }, null);
                BarrelResearchCheck(EntityDeathBoundaryNpcMissionMethod != null, "existing exact A mission-tail fault seam");
                EntityDeathBoundaryNpcMissionCalls = 0;
                EntityDeathBoundaryNpcMissionThrow = true;
                EntityDeathBoundaryNpcHarmony.Patch(EntityDeathBoundaryNpcMissionMethod, prefix: new HarmonyMethod(
                    typeof(CarbonLuau).GetMethod(nameof(EntityDeathBoundaryNpcMissionPrefix), BindingFlags.Static | BindingFlags.NonPublic))
                    { priority = Priority.First });
                bool RespawnThrew = false;
                EntityDeathBoundaryNpcBudget("before_failed_respawn", BudgetStart);
                try { Npc.RespawnAt(BarrelResearchPosition, UnityEngine.Quaternion.identity, null); }
                catch (InvalidOperationException Error) {
                    if (Error.Message != "B2 owned NPC respawn tail fault") throw;
                    RespawnThrew = true;
                } finally {
                    EntityDeathBoundaryNpcMissionThrow = false;
                    EntityDeathBoundaryNpcHarmony.Unpatch(EntityDeathBoundaryNpcMissionMethod,
                        HarmonyPatchType.All, EntityDeathBoundaryNpcHarmony.Id);
                    EntityDeathBoundaryNpcMissionMethod = null;
                }
                BarrelResearchCheck(RespawnThrew && EntityDeathBoundaryNpcMissionCalls == 1 && Watch.DeathSeen &&
                    Npc.lifestate == BaseCombatEntity.LifeState.Alive && Npc.health > 0 && Npc.respawnId != RespawnId &&
                    ValidateEntity(Binding) && EntityDeathFrames.Count == 0 && EntityDeathCompleted == Completed + 1 &&
                    EntityDiedFixtureObservations == Captured + 1,
                    "real failed outer RespawnAt leaves Alive/new ID but never rearms old death latch");
                EntityDeathBoundaryNpcBudget("after_failed_respawn", BudgetStart);
                RespawnId = Npc.respawnId;
                Npc.RespawnAt(BarrelResearchPosition, UnityEngine.Quaternion.identity, null);
                BarrelResearchCheck(!Watch.DeathSeen && Npc.lifestate == BaseCombatEntity.LifeState.Alive && Npc.health > 0 &&
                    Npc.respawnId != RespawnId && Binding.Lifetime.Record.Epoch == Epoch && ValidateEntity(Binding) &&
                    Npc.net.ID.Value.ToString(CultureInfo.InvariantCulture) == Id && CurrentGameplayPlayer(Npc) == null,
                    "full original NPC RespawnAt rearms same exact Spawn incarnation without human authority");
                EntityDeathBoundaryNpcBudget("after_successful_retry", BudgetStart);
                int BeforeSecondRequests=EntityDeathBoundaryNpcKillRequests;
                Npc.Die(null);
                BarrelResearchCheck(EntityDeathBoundaryNpcKillRequests == BeforeSecondRequests,
                    "second full NPC Die made no inline Kill attempt on this exact host");
                EntityDeathBoundaryNpcExplicitKill=true;
                try { Npc.Kill(); } finally { EntityDeathBoundaryNpcExplicitKill=false; }
                EntityDeathBoundaryNpcBudget("after_second_death", BudgetStart);
                long CompletedDelta = EntityDeathCompleted - Completed;
                int CapturedDelta = EntityDiedFixtureObservations - Captured;
                bool Seen = Watch.DeathSeen;
                bool Dead = Npc.lifestate == BaseCombatEntity.LifeState.Dead;
                bool Valid = ValidateEntity(Binding);
                int KillVetoes = EntityDeathBoundaryNpcKillVetoes, Corpses = EntityDeathBoundaryNpcCorpses;
                ulong AttemptedDelta = Host.Attempted - Attempted;
                Puts("[CarbonLuau:EntityDeathBoundary] NPC_SECOND_DIAGNOSTIC CompletedDelta=" + CompletedDelta +
                    " CapturedDelta=" + CapturedDelta + " DeathSeen=" + Seen + " Dead=" + Dead + " Validation=" + Valid +
                    " KillVetoes=" + KillVetoes + " CorpseCount=" + Corpses + " AttemptedDelta=" + AttemptedDelta);
                BarrelResearchCheck(CompletedDelta == 2, "second NPC death: exact completed delta must be two");
                BarrelResearchCheck(CapturedDelta == 2, "second NPC death: exact captured delta must be two");
                BarrelResearchCheck(Seen, "second NPC death: completed episode must latch DeathSeen");
                BarrelResearchCheck(Dead, "second NPC death: original object must be terminal Dead");
                BarrelResearchCheck(Valid, "second NPC death: original D20 lifetime must remain valid under owned Kill veto");
                BarrelResearchCheck(KillVetoes == 2 && !Npc.IsDestroyed,
                    "second NPC death: two actual explicit scoped Kill vetoes, no world removal");
                BarrelResearchCheck(Corpses <= 2, "second NPC death: bounded returned corpse count");
                BarrelResearchCheck(AttemptedDelta == 0, "second NPC death: no inline Luau entry");
                EntityDeathBoundaryNpcBudget("before_pre_drain_public_assert", BudgetStart);
                EntityDeathBoundaryAssertPublic(Id, 1, 0);
                EntityDeathBoundaryNpcBudget("before_drain", BudgetStart);
                EntityDeathBoundaryDrain();
                EntityDeathBoundaryNpcBudget("after_drain", BudgetStart);
                EntityDeathBoundaryAssertPublic(Id, 2, 0);
                Puts("[CarbonLuau:EntityDeathBoundary] PASS case=npc_full_respawn_explicit_kill_veto completed=2 captured=2 public_death=2" +
                    " same_spawn_epoch=true explicit_kill_vetoes=2 total_kill_requests="+EntityDeathBoundaryNpcKillRequests+
                    " natural_Die_inline_Kill=0 corpses=" + EntityDeathBoundaryNpcCorpses +
                    " failed_outer_respawn_alive_latched=true successful_alive_retry_rearmed=true first_frame="+FirstFrame+
                    " second_frame="+UnityEngine.Time.frameCount+" natural_reuse_not_claimed=true");
                    } catch (Exception Error) { Failure=Error; }
                    finally {
                        try { CleanupEntityDeathBoundaryNpc(); }
                        catch (Exception Error) { if(Failure==null)Failure=Error; }
                        Continue=null;
                    }
                    if(Failure!=null) { FinishEntityDeathBoundaryQualification(Failure); return; }
                    EntityDeathBoundaryStep++; NextTick(RunEntityDeathBoundaryStep);
                };
                NextTick(Continue); Scheduled=true;
            } finally {
                if(!Scheduled)CleanupEntityDeathBoundaryNpc();
            }
        }
        private void CleanupEntityDeathBoundaryNpc()
        {
            EntityDeathBoundaryRemoveNpcPatches();
            EntityDeathBoundaryNpc=null;
            BarrelResearchCleanup();
            EntityDeathBoundaryDrain();
        }
        partial void ObserveEntityDeathBoundaryKill(BaseNetworkable Entity, ref object Veto)
        {
            if (!EntityDeathBoundaryRunning || !EntityDeathBoundaryNpcVetoEnabled || BarrelResearchCleaning ||
                ReferenceEquals(EntityDeathBoundaryNpc, null) || !ReferenceEquals(Entity, EntityDeathBoundaryNpc)) return;
            BarrelResearchCheck(BarrelResearchOnOwner(), "ordinary Carbon NPC Kill veto owner/no VM entry");
            EntityDeathBoundaryNpcKillRequests++;
            if (EntityDeathBoundaryNpcExplicitKill) EntityDeathBoundaryNpcKillVetoes++;
            Veto = true;
        }
        private void EntityDeathBoundaryNpcBudget(string Stage, long Start)
        {
            long Timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            double ElapsedMs = (Timestamp - Start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Puts("[CarbonLuau:EntityDeathBoundary] NPC_BUDGET stage=" + Stage +
                " Timestamp=" + Timestamp + " ElapsedMs=" + ElapsedMs.ToString("F3", CultureInfo.InvariantCulture) +
                " UnityFrame=" + UnityEngine.Time.frameCount + " BudgetFrame=" + Gameplay.GameplayEvents.FrameClock() +
                " Attempted=" + Host.Attempted + " HasReadyWork=" + Host.HasReadyWork +
                " Completed=" + EntityDeathCompleted + " Captured=" + EntityDiedFixtureObservations +
                " BudgetStatus=" + Gameplay.GameplayEvents.Status);
        }
        private static void EntityDeathBoundaryNpcCorpsePostfix(BasePlayer __instance, BaseCorpse __result)
        {
            var Owner = EntityDeathBoundaryFaultOwner;
            if (Owner == null || !ReferenceEquals(__instance, Owner.EntityDeathBoundaryNpc) || ReferenceEquals(__result, null)) return;
            BarrelResearchCheck(Owner.BarrelResearchOnOwner(), "exact owned NPC corpse return owner/no VM entry");
            Owner.EntityDeathBoundaryHoldCorpse(__result);
            BarrelResearchCheck(++Owner.EntityDeathBoundaryNpcCorpses <= 2, "bounded exact owned NPC corpse return");
        }
        private static void EntityDeathBoundaryNpcMissionPrefix(BasePlayer __instance)
        {
            var Owner = EntityDeathBoundaryFaultOwner;
            if (Owner == null || !Owner.EntityDeathBoundaryNpcMissionThrow ||
                !ReferenceEquals(__instance, Owner.EntityDeathBoundaryNpc)) return;
            BarrelResearchCheck(Owner.BarrelResearchOnOwner(), "owned NPC mission-tail owner/no VM entry");
            Owner.EntityDeathBoundaryNpcMissionCalls++;
            throw new InvalidOperationException("B2 owned NPC respawn tail fault");
        }
        private void EntityDeathBoundaryRemoveNpcPatches()
        {
            EntityDeathBoundaryNpcVetoEnabled = false;
            EntityDeathBoundaryNpcMissionThrow = false;
            if (EntityDeathBoundaryNpcHarmony != null) {
                if (EntityDeathBoundaryNpcMissionMethod != null)
                    EntityDeathBoundaryNpcHarmony.Unpatch(EntityDeathBoundaryNpcMissionMethod, HarmonyPatchType.All, EntityDeathBoundaryNpcHarmony.Id);
                if (EntityDeathBoundaryNpcCorpseMethod != null)
                    EntityDeathBoundaryNpcHarmony.Unpatch(EntityDeathBoundaryNpcCorpseMethod, HarmonyPatchType.All, EntityDeathBoundaryNpcHarmony.Id);
            }
            EntityDeathBoundaryNpcHarmony = null;
            EntityDeathBoundaryNpcCorpseMethod = null;
            EntityDeathBoundaryNpcMissionMethod = null;
            EntityDeathBoundaryFaultOwner = null;
        }

        private void RunEntityDeathBoundaryHuman()
        {
            Network.Connection Connection = null;
            PlayerLifetime Lifetime = null;
            bool AddedList = false, AddedLookup = false;
            try {
                BarrelResearchCreateActor();
                var Victim = BarrelResearchActor;
                EntityDeathBoundaryHuman = Victim;
                EntityDeathBoundaryHumanOwner = this;
                EntityDeathBoundaryHumanCorpses = 0;
                Connection = new Network.Connection { userid = Victim.userID, username = Victim.displayName,
                    connected = true, active = true, state = (Network.Connection.State)4, canReserveSlot = false };
                Connection.player = Victim;
                EntityMeleeFixtureSetConnection(Victim, Connection);
                Victim.SetPlayerFlag(BasePlayer.PlayerFlags.Connected, true);
                Victim.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, false);
                Victim.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                BarrelResearchCheck(BasePlayer.activePlayerList.Count <= 1, "isolated human connection list admission");
                if (!BasePlayer.activePlayerList.Contains(Victim)) {
                    BasePlayer.activePlayerList.Add(Victim);
                    AddedList = true;
                }
                BasePlayer Existing;
                if (BasePlayer.activePlayerLookup.TryGetValue(Victim.userID, out Existing))
                    BarrelResearchCheck(ReferenceEquals(Existing, Victim), "never overwrite an unrelated account lookup");
                else {
                    BasePlayer.activePlayerLookup[Victim.userID] = Victim;
                    AddedLookup = true;
                }
                OnPlayerConnected(Victim);
                Lifetime = CurrentGameplayPlayer(Victim);
                BarrelResearchCheck(Lifetime != null && ReferenceEquals(Lifetime.Connection, Connection) && !Victim.IsNpc,
                    "original constructed human D11 connection, no authenticated client claim");
                EntityFacadeBinding Binding;
                BarrelResearchCheck(TryAdmitEntity(Victim, Gameplay.Active, out Binding), "original human D20 lifetime");
                string Id = Victim.net.ID.Value.ToString(CultureInfo.InvariantCulture);
                EntityDeathBoundaryExecute("human_subscribe", "local B=require('state').B2Boundary; " +
                    "B.Watched['" + Id + "']='assets/prefabs/player/player.prefab'; B.A=0; " +
                    "B.Human=game:GetService('Players').PlayerDied:Connect(function(P,C) " +
                    "if P.UserId=='" + Victim.UserIDString + "' then assert(P.IsConnected and table.isfrozen(C)); " +
                    "assert(C.Killer==nil and C.KillerId==nil); B.A+=1 end end)");
                EntityDeathBoundaryDrain();
                EntityDeathBoundaryHumanCorpseMethod = typeof(BasePlayer).GetMethod("CreateCorpse", BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                    new[] { typeof(BasePlayer.PlayerFlags), typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion),
                        typeof(System.Collections.Generic.List<TriggerBase>), typeof(bool) }, null);
                EntityDeathBoundaryHumanCorpseMethod = EntityDeathBoundaryDeclaredCorpse(EntityDeathBoundaryHumanCorpseMethod);
                BarrelResearchCheck(EntityDeathBoundaryHumanCorpseMethod != null &&
                    typeof(BaseCorpse).IsAssignableFrom(EntityDeathBoundaryHumanCorpseMethod.ReturnType),
                    "exact owned human corpse-return cleanup seam");
                EntityDeathBoundaryHumanHarmony = new Harmony("CarbonLuau.Test.EntityDeathHuman." + Guid.NewGuid().ToString("N"));
                EntityDeathBoundaryHumanHarmony.Patch(EntityDeathBoundaryHumanCorpseMethod, postfix: new HarmonyMethod(
                    typeof(CarbonLuau).GetMethod(nameof(EntityDeathBoundaryHumanCorpsePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                BarrelResearchCheck(GameplayDeathQualified && EntityDiedAvailable(), "both A and B2 death sources remain qualified");
                long ADeaths = GameplayLifecycleDeaths, BDeaths = EntityDeathCompleted;
                int Captured = EntityDiedFixtureObservations;
                ulong Attempted = Host.Attempted;
                Victim.Die(null);
                BarrelResearchCheck(GameplayLifecycleDeaths == ADeaths + 1 && EntityDeathCompleted == BDeaths + 1 &&
                    EntityDiedFixtureObservations == Captured + 1 && Host.Attempted == Attempted &&
                    GameplayFrames.Count == 0 && EntityDeathFrames.Count == 0,
                    "same real human full Die completes A and B2 after shared-frame finalizer cleanup, no inline VM entry");
                // TryDropCorpse may transfer items after CreateCorpse returns.
                // Revisit only this fixture's returned bodies in its fixed ledger.
                int OwnedCount = BarrelResearchLedgerCount;
                for (int Index = 0; Index < OwnedCount; Index++) {
                    var Corpse = BarrelResearchLedger[Index].Entity as BaseCorpse;
                    if (Corpse != null) EntityDeathBoundaryHoldCorpse(Corpse);
                }
                EntityDeathBoundaryAssertPublic(Id, 0, 0);
                EntityDeathBoundaryExecute("human_before_drain", "assert(require('state').B2Boundary.A==0)");
                EntityDeathBoundaryDrain();
                EntityDeathBoundaryAssertPublic(Id, 1, 0);
                EntityDeathBoundaryExecute("human_after_drain", "assert(require('state').B2Boundary.A==1)");
                Puts("[CarbonLuau:EntityDeathBoundary] PASS case=human_shared_finalizer private_A=1 private_B2=1" +
                    " captured_B2=1 public_A=1 public_B2=1 owned_corpses=" + EntityDeathBoundaryHumanCorpses +
                    " constructed_connection=true authenticated_client=false");
            } finally {
                if (EntityDeathBoundaryHumanHarmony != null && EntityDeathBoundaryHumanCorpseMethod != null)
                    EntityDeathBoundaryHumanHarmony.Unpatch(EntityDeathBoundaryHumanCorpseMethod,
                        HarmonyPatchType.All, EntityDeathBoundaryHumanHarmony.Id);
                EntityDeathBoundaryHumanHarmony = null;
                EntityDeathBoundaryHumanCorpseMethod = null;
                EntityDeathBoundaryHumanOwner = null;
                var Victim = EntityDeathBoundaryHuman;
                try {
                    if (!ReferenceEquals(Victim, null)) {
                        OnPlayerDisconnected(Victim, "owned human boundary cleanup");
                        BarrelResearchCheck(Lifetime == null ||
                            !ReferenceEquals(Gameplay.Players.Find(Lifetime.UserId), Lifetime),
                            "original human D11 source retired before explicit fixture indexes are removed");
                        Victim.SetPlayerFlag(BasePlayer.PlayerFlags.Connected, false);
                        if (Victim.net != null) EntityMeleeFixtureSetConnection(Victim, null);
                    }
                } finally {
                    if (!ReferenceEquals(Victim, null)) {
                        BarrelResearchCheck(BasePlayer.activePlayerList.Count <= 1, "bounded owned human list cleanup");
                        if (AddedList)
                            for (int Index = BasePlayer.activePlayerList.Count - 1; Index >= 0; Index--)
                                if (ReferenceEquals(BasePlayer.activePlayerList[Index], Victim)) BasePlayer.activePlayerList.RemoveAt(Index);
                        BasePlayer Current;
                        if (AddedLookup && BasePlayer.activePlayerLookup.TryGetValue(Victim.userID, out Current) && ReferenceEquals(Current, Victim))
                            BasePlayer.activePlayerLookup.Remove(Victim.userID);
                    }
                    if (Connection != null) { Connection.connected = Connection.active = false; Connection.player = null; }
                    EntityDeathBoundaryHuman = null;
                    BarrelResearchCleanup();
                }
                EntityDeathBoundaryDrain();
                EntityDeathBoundaryExecute("human_disconnect_listener", "local B=require('state').B2Boundary; " +
                    "if B.Human then B.Human:Disconnect(); B.Human=nil end");
                BarrelResearchCheck(BasePlayer.activePlayerList.Count == 0 && BarrelResearchLedgerCount == 0,
                    "owned human/corpse/connection cleanup converges");
            }
        }
        private static void EntityDeathBoundaryHumanCorpsePostfix(BasePlayer __instance, BaseCorpse __result)
        {
            var Owner = EntityDeathBoundaryHumanOwner;
            if (Owner == null || !ReferenceEquals(__instance, Owner.EntityDeathBoundaryHuman) || ReferenceEquals(__result, null)) return;
            BarrelResearchCheck(Owner.BarrelResearchOnOwner(), "original human corpse return owner/no VM entry");
            // Acquire returned ownership before a diagnostic count assertion.
            // An unexpected second returned body must still be cleaned safely.
            Owner.EntityDeathBoundaryHoldCorpse(__result);
            BarrelResearchCheck(++Owner.EntityDeathBoundaryHumanCorpses <= 1, "bounded original human corpse return");
        }
        private void EntityDeathBoundaryHoldCorpse(BaseCorpse Corpse)
        {
            BarrelResearchHold(Corpse);
            Corpse.enableSaving = false;
            var Lootable = Corpse as LootableCorpse;
            if (Lootable == null) return;
            Lootable.blockBagDrop = true;
            ItemContainer[] Containers = Lootable.containers;
            if (Containers == null) return;
            BarrelResearchCheck(Containers.Length <= BarrelResearchItemLimit, "returned corpse container bound");
            for (int Index = 0; Index < Containers.Length; Index++)
                if (Containers[Index] != null) BarrelResearchInventory(Containers[Index], null, false);
        }
        private static MethodInfo EntityDeathBoundaryDeclaredCorpse(MethodInfo Method)
        {
            BarrelResearchCheck(Method != null, "resolved corpse method exists");
            // Harmony rejects inherited ReflectedType wrappers. Resolve this same
            // exact token at its declared implementation; do not widen the target.
            var Declared = Method.Module.ResolveMethod(Method.MetadataToken) as MethodInfo;
            BarrelResearchCheck(Declared != null && Declared.DeclaringType == Method.DeclaringType &&
                Declared.ReflectedType == Declared.DeclaringType && SameEntityMethod(Method, Declared) &&
                !Declared.IsAbstract && Declared.GetMethodBody() != null &&
                Declared.GetMethodBody().GetILAsByteArray().Length <= 8192,
                "exact implemented declaring-scope corpse method");
            return Declared;
        }
        private void FinishEntityDeathBoundaryQualification(Exception Error)
        {
            try {
                EntityDeathBoundaryDamageVeto = EntityDeathBoundaryNested = EntityDeathBoundaryLateSubtype = false;
                EntityDeathBoundaryRemoveNpcPatches();
                EntityDeathBoundaryRemoveFault();
                EntityDeathBoundaryRemoveBagCapture();
                EntityDeathBoundaryTarget = null;
                BarrelResearchCleanup();
                EntityDeathBoundaryDrain();
                if (Host.Ready)
                    EntityDeathBoundaryExecute("disconnect", "local S=require('state'); local B=S.B2Boundary; " +
                        "if B then B.Died:Disconnect(); B.Destroyed:Disconnect(); " +
                        "if B.Human then B.Human:Disconnect() end; S.B2Boundary=nil end");
                BarrelResearchCheck(EntityDiedAvailable() && EntityDeathFrames.Count == 0 && BarrelResearchLedgerCount == 0,
                    "restored topology and all private ownership released");
            } catch (Exception CleanupError) {
                if (Error == null) Error = CleanupError;
                else Puts("[CarbonLuau:EntityDeathBoundary] CLEANUP_FAIL " + CleanupError.Message);
            }
            EntityDeathBoundaryFailure = Error == null ? null : Error.GetType().Name + ": " + Error.Message;
            EntityDeathBoundaryPassed = Error == null && EntityDeathBoundaryStep == 15;
            EntityDeathBoundaryRunning = false;
            Puts("[CarbonLuau:EntityDeathBoundary] " + (EntityDeathBoundaryPassed ? "PASS" : "FAIL") +
                " completed_cases=" + EntityDeathBoundaryStep + " failure=" + (EntityDeathBoundaryFailure ?? "none") +
                "; scope=owned_barrel_terminal_boundaries,late_subtype_persistent_and_self_restored_interference," +
                "owned_scientist_failed_respawn_alive_retry_and_explicit_kill_veto," +
                "actorless_Decay_Explosion_Die_context_labels,constructed_human_shared_A_B2_finalizer" +
                "; autonomous_decay/explosive_origins,other_NPC/resource_families,authenticated_clients,natural_pooling=not_measured");
        }
    }
}
