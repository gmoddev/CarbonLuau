// Isolated test package only. No public discovery binding and no native patch.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string DiscoveryFixturePrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private readonly List<BaseEntity> DiscoveryFixtureEntities = new List<BaseEntity>();
        private readonly List<GameObject> DiscoveryFixtureObjects = new List<GameObject>();
        private int DiscoveryFixtureCallbacks;
        private long DiscoveryFixtureStart;
        private int DiscoveryFixtureTokenCount;
        private int DiscoveryTokenCount()
        {
            return ((IDictionary)typeof(EntityLifetimeModel).GetField("TokenRecords", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(EntityLifetimes)).Count;
        }

        partial void RunEntityDiscoveryFixtures()
        {
            NextTick(() => {
                try { BeginDiscoveryFixtures(); }
                catch (Exception Error) { EndDiscoveryFixture(false, Error.Message); }
            });
        }

        private static void DiscoveryCheck(bool Condition, string Reason)
        { if (!Condition) throw new InvalidOperationException(Reason); }

        private BaseEntity DiscoveryBox(Vector3 Position)
        {
            BaseEntity Entity = GameManager.server.CreateEntity(DiscoveryFixturePrefab, Position);
            DiscoveryCheck(Entity != null, "box creation failed");
            DiscoveryFixtureEntities.Add(Entity);
            Entity.Spawn();
            DiscoveryCheck(EntityLifetimes.HasCatalogObservation(Entity), "completed Spawn missing direct catalog membership");
            EntityFacadeBinding Binding;
            DiscoveryCheck(TryAdmitEntity(Entity, Gameplay.Active, out Binding), "box admission failed");
            return Entity;
        }

        private void CheckDiscoveryPosition(BaseEntity Entity)
        {
            EntityPositionComposition.Position Actual;
            DiscoveryCheck(EntityPositionReader.TryObserve(Entity, out Actual), "bounded position observation failed");
            // Reference getter is fixture-only and outside the bounded borrow.
            Vector3 Expected = Entity.transform.position;
            DiscoveryCheck(Math.Abs(Actual.X - Expected.x) < 0.002 && Math.Abs(Actual.Y - Expected.y) < 0.002 &&
                Math.Abs(Actual.Z - Expected.z) < 0.002, "bounded composition differs from settled host position");
        }

        private struct DiscoveryPositionJob : IJobParallelForTransform
        {
            public void Execute(int Index, TransformAccess Transform)
            {
                long End = Stopwatch.GetTimestamp() + 2 * Stopwatch.Frequency;
                while (Stopwatch.GetTimestamp() < End)
                    Transform.localPosition = new Vector3(1.125f, 2, 3);
            }
        }

        private void BeginDiscoveryFixtures()
        {
            DiscoveryCheck(EntityDiscovery != null && EntityPositionReader != null && EntityPositionReader.Available &&
                EntityLifetimes.CatalogReady && Gameplay != null && Gameplay.Active != null,
                "qualified traversal/root unavailable");
            BaseEntity Box = DiscoveryBox(new Vector3(0, 5000, 0));
            CheckDiscoveryPosition(Box);
            GameObject Parent = new GameObject("CarbonLuauDiscoveryOwnedParent");
            DiscoveryFixtureObjects.Add(Parent);
            Parent.transform.position = new Vector3(12, 13, 14);
            Parent.transform.rotation = Quaternion.Euler(12, 31, 43);
            Parent.transform.localScale = new Vector3(2, 3, 4);
            Box.transform.SetParent(Parent.transform, false);
            Box.transform.localPosition = new Vector3(1, 2, 3);
            CheckDiscoveryPosition(Box);
            var Access = new TransformAccessArray(new[] { Box.transform });
            JobHandle Job = default(JobHandle);
            try {
                Job = new DiscoveryPositionJob().Schedule(Access);
                JobHandle.ScheduleBatchedJobs();
                DiscoveryCheck(!Job.IsCompleted, "pending job witness completed before borrow");
                EntityPositionComposition.Position Observation;
                // No Complete/IsCompleted/pending-job guard inside producer.
                DiscoveryCheck(EntityPositionReader.TryObserve(Box, out Observation), "outstanding job rejected/no-wait borrow failed");
                DiscoveryCheck(!Job.IsCompleted, "borrow waited for the outstanding job");
                Puts("[CarbonLuau:DiscoveryFixture] PENDING_JOB no-wait observation returned while job incomplete");
            }
            finally { Job.Complete(); Access.Dispose(); }
            CheckDiscoveryPosition(Box);
            Transform Previous = Parent.transform;
            // 64 actual ancestors plus leaf is supported; 65 ancestors fails.
            for (int Index = 1; Index < 64; ++Index) {
                var Value = new GameObject("CarbonLuauDiscoveryOwnedAncestor");
                DiscoveryFixtureObjects.Add(Value);
                Previous.SetParent(Value.transform, false);
                Previous = Value.transform;
            }
            EntityPositionComposition.Position Deep;
            DiscoveryCheck(EntityPositionReader.TryObserve(Box, out Deep), "65-record terminating chain rejected");
            var BorrowWatch = Stopwatch.StartNew();
            for (int Iteration = 0; Iteration < 1024; ++Iteration)
                DiscoveryCheck(EntityPositionReader.TryObserve(Box, out Deep), "bounded deep borrow stress failed");
            BorrowWatch.Stop();
            Puts("[CarbonLuau:DiscoveryFixture] BORROW samples=1024 records=65 wallMs=" +
                BorrowWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            var Excess = new GameObject("CarbonLuauDiscoveryOwnedExcess");
            DiscoveryFixtureObjects.Add(Excess);
            Previous.SetParent(Excess.transform, false);
            DiscoveryCheck(!EntityPositionReader.TryObserve(Box, out Deep), "excessive depth did not fail closed");
            Box.transform.SetParent(null, false);
            Box.transform.position = new Vector3(0, 5000, 0);
            Box.transform.rotation = Quaternion.identity;
            Box.transform.localScale = Vector3.one;
            CheckDiscoveryPosition(Box);
            BaseEntity Second = DiscoveryBox(new Vector3(1, 5000, 0));
            // Full catalog, not a proxy-created subset. Nonmatching world members
            // still get the qualified lifetime/position encounter path.
            ulong Request;
            DiscoveryFixtureStart = Stopwatch.GetTimestamp();
            var Query = new EntityDiscoveryTraversal.Query(0, 5000, 0, 2, DiscoveryFixturePrefab, 8);
            DiscoveryCheck(StartEntityDiscovery(Gameplay.Active, Query, Completion => {
                try {
                    DiscoveryFixtureCallbacks++;
                    DiscoveryCheck(Completion.Status == EntityDiscoveryTraversal.Outcome.Success && Completion.Count == 2,
                        "complete catalog query did not find both current boxes: " + Completion.Status + "/" + Completion.Count);
                    DiscoveryCheck(Completion.RawSlots > 2, "lazy-token subset traversed instead of completed Spawn catalog");
                    Puts("[CarbonLuau:DiscoveryFixture] QUERY success raw=" + Completion.RawSlots + " matches=" + Completion.Count +
                        " elapsedMs=" + ((Stopwatch.GetTimestamp() - DiscoveryFixtureStart) * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) +
                        " observations=" + EntityPositionReader.Observations + " maxRecords=" + EntityPositionReader.MaximumObservedRecords);
                    Puts("[CarbonLuau:DiscoveryFixture] TURN maxUnits=" + EntityDiscoveryMaximumTurnUnits + " maxRaw=" + EntityDiscoveryMaximumTurnRaw +
                        " maxWallMs=" + (EntityDiscoveryMaximumTurnTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture));
                    BeginDiscoveryEncounterFixture();
                }
                catch (Exception Error) { EndDiscoveryFixture(false, Error.Message); }
            }, out Request) == EntityDiscoveryTraversal.StartStatus.Accepted, "query admission rejected");
            DiscoveryBox(new Vector3(-1, 5000, 0)); // Later birth must not enter the first query.
            DiscoveryFixtureTokenCount = DiscoveryTokenCount();
        }

        private void BeginDiscoveryEncounterFixture()
        {
            ulong Request;
            var Query = new EntityDiscoveryTraversal.Query(0, 5000, 0, 2, DiscoveryFixturePrefab, 8);
            DiscoveryCheck(StartEntityDiscovery(Gameplay.Active, Query, Completion => {
                try {
                    DiscoveryFixtureCallbacks++;
                    DiscoveryCheck(Completion.Status == EntityDiscoveryTraversal.Outcome.Success && Completion.Count == 2,
                        "encounter/current-position or nested query failed: " + Completion.Status);
                    ulong MovedId = DiscoveryFixtureEntities[0].net.ID.Value;
                    for (int Index = 0; Index < Completion.Count; ++Index)
                        DiscoveryCheck(Completion.GetResult(Index).Id != MovedId, "submission-time cache used instead of encounter position");
                    DiscoveryCheck(DiscoveryTokenCount() == DiscoveryFixtureTokenCount, "discovery grew shared proxy token records");
                    Puts("[CarbonLuau:DiscoveryFixture] NO_TOKEN_GROWTH direct catalog validation; shared token count=" + DiscoveryFixtureTokenCount);
                    Puts("[CarbonLuau:DiscoveryFixture] WATERMARK_AND_ENCOUNTER_PASS later birth excluded; nested request includes current members; moved-out candidate excluded");
                    BeginDiscoveryRetirementFixture();
                }
                catch (Exception Error) { EndDiscoveryFixture(false, Error.Message); }
            }, out Request) == EntityDiscoveryTraversal.StartStatus.Accepted, "nested request rejected");
            DiscoveryFixtureEntities[0].transform.position = new Vector3(0, 5005, 0);
        }

        private void BeginDiscoveryRetirementFixture()
        {
            FacadeSession Old = Gameplay.Active;
            ulong Request;
            var Query = new EntityDiscoveryTraversal.Query(0, 5000, 0, 2, DiscoveryFixturePrefab, 8);
            DiscoveryCheck(StartEntityDiscovery(Old, Query, Completion => {
                DiscoveryFixtureCallbacks++;
                EndDiscoveryFixture(false, "old root callback entered after replacement");
            }, out Request) == EntityDiscoveryTraversal.StartStatus.Accepted, "retirement request rejected");
            ExecutionResult Reload = Host.Reload();
            DiscoveryCheck(Reload.Status == RuntimeStatus.OK && !ReferenceEquals(Gameplay.Active, Old), "root replacement failed");
            timer.Once(2, () => {
                try {
                    DiscoveryCheck(DiscoveryFixtureCallbacks == 2 && EntityDiscovery.ActiveCount == 0,
                        "retired callback or pending request survived replacement");
                    EntityFacadeBinding Binding;
                    BaseEntity Retired = DiscoveryFixtureEntities[0];
                    DiscoveryCheck(TryAdmitEntity(Retired, Gameplay.Active, out Binding), "replacement fresh authority failed");
                    Retired.Kill();
                    DiscoveryCheck(!ValidateEntity(Binding), "killed entity remained valid");
                    EntityLifetimes.SweepCatalog(EntityDiscoveryPolicy.CatalogSlots);
                    DiscoveryCheck(!EntityLifetimes.HasCatalogObservation(Retired), "retired catalog slot survived sweep");
                    EndDiscoveryFixture(true, "composition/root-parent/job/depth/catalog/root-replacement/retirement");
                }
                catch (Exception Error) { EndDiscoveryFixture(false, Error.Message); }
            });
        }

        private void EndDiscoveryFixture(bool Success, string Detail)
        {
            foreach (BaseEntity Entity in DiscoveryFixtureEntities) if (Entity != null && !Entity.IsDestroyed) Entity.Kill();
            foreach (GameObject Value in DiscoveryFixtureObjects) if (Value != null) UnityEngine.Object.DestroyImmediate(Value);
            DiscoveryFixtureEntities.Clear(); DiscoveryFixtureObjects.Clear();
            Puts("[CarbonLuau:DiscoveryFixture] " + (Success ? "PASS " : "FAIL ") + Detail);
            timer.Once(1, () => ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"));
        }
    }
}
