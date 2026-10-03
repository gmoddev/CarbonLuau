// Test-package-only public Entity-1B exercise. Never ship this partial class.
using System;
using System.Globalization;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string EntityReadFixturePrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        partial void RunEntityReadFixtures()
        {
            NextTick(() => {
                try { ExecuteEntityReadFixtures(); }
                catch (Exception Error) {
                    PrintError("[CarbonLuau:EntityReadFixture] FAIL " + Error.GetType().Name + ": " + Error.Message);
                }
            });
        }

        private static void RequireEntityRead(bool Condition, string Reason)
        { if (!Condition) throw new InvalidOperationException(Reason); }

        private void ExecuteEntityReadFixtures()
        {
            RequireEntityRead(EntityStartupQualified && !EntityObserverBroken && Gameplay != null &&
                Gameplay.Active != null, "qualified world/root unavailable");
            BaseEntity Existing = BaseNetworkable.serverEntities.OfType<BaseEntity>()
                .FirstOrDefault(Value => Value != null && !Value.IsDestroyed && Value.net != null &&
                    Value.IsFullySpawned() && !String.IsNullOrEmpty(Value.PrefabName) &&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(Value.net.ID), Value));
            RequireEntityRead(Existing != null, "no pre-existing keyed BaseEntity");
            var Samples = new Dictionary<string, BaseEntity>(StringComparer.Ordinal);
            var ScaleIds = new List<string>();
            int Inspected = 0, MaximumPrefabBytes = 0;
            foreach (BaseEntity Candidate in BaseNetworkable.serverEntities.OfType<BaseEntity>()) {
                if (Candidate == null || Candidate.IsDestroyed || Candidate.net == null ||
                    !Candidate.IsFullySpawned() ||
                    !ReferenceEquals(BaseNetworkable.serverEntities.Find(Candidate.net.ID), Candidate)) continue;
                string Prefab = Candidate.PrefabName;
                if (String.IsNullOrEmpty(Prefab)) continue;
                int Bytes = Encoding.UTF8.GetByteCount(Prefab);
                MaximumPrefabBytes = Math.Max(MaximumPrefabBytes, Bytes);
                RequireEntityRead(Bytes <= 512 && Prefab.IndexOf('\0') < 0,
                    "qualified keyed population exceeds canonical Prefab bound");
                ++Inspected;
                if (ScaleIds.Count < 128)
                    ScaleIds.Add(Candidate.net.ID.Value.ToString(CultureInfo.InvariantCulture));
                string Kind = Candidate is BasePlayer ? "player" :
                    Prefab.Contains("/building core/") ? "building" :
                    Prefab.Contains("/deployable/") ? "deployable" :
                    Prefab.Contains("npc") ? "npc" : "world";
                if (!Samples.ContainsKey(Kind)) Samples.Add(Kind, Candidate);
            }
            RequireEntityRead(Inspected > 0, "no valid pre-existing Prefab population");
            string ExistingId = Existing.net.ID.Value.ToString(CultureInfo.InvariantCulture);
            var FirstLookupWatch = Stopwatch.StartNew();
            ExecutionResult Result = Host.Execute("entity1b.preexisting",
                "local W=game:GetService('Workspace'); assert(W:GetEntityById('" + ExistingId + "')~=nil)");
            FirstLookupWatch.Stop();
            RequireEntityRead(Result.Status == RuntimeStatus.OK,
                "pre-existing public lookup failed: " + Result.Error);
            Puts("[CarbonLuau:EntityReadFixture] first public lookup wall=" +
                FirstLookupWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms");
            foreach (var Sample in Samples) {
                string Id = Sample.Value.net.ID.Value.ToString(CultureInfo.InvariantCulture);
                ExecutionResult PrefabResult = Host.Execute("entity1b.prefab." + Sample.Key,
                    "local E=game:GetService('Workspace'):GetEntityById('" + Id + "'); print(E.Prefab)");
                RequireEntityRead(PrefabResult.Status == RuntimeStatus.OK &&
                    PrefabResult.Logs == Sample.Value.PrefabName + "\n",
                    "public full Prefab mismatch for " + Sample.Key + ": " + PrefabResult.Error);
            }
            Puts("[CarbonLuau:EntityReadFixture] prefab population=" + Inspected +
                " maxUtf8Bytes=" + MaximumPrefabBytes +
                " testedKinds=" + String.Join(",", Samples.Keys));
            Result = Host.Execute("entity1b.prefab",
                "local E=game:GetService('Workspace'):GetEntityById('" + ExistingId + "'); print(E.Prefab)");
            RequireEntityRead(Result.Status == RuntimeStatus.OK && Result.Logs == Existing.PrefabName + "\n",
                "pre-existing public prefab mismatch: " + Result.Error);
            Result = Host.Execute("entity1b.id",
                "local E=game:GetService('Workspace'):GetEntityById('" + ExistingId +
                "'); assert(E.Id=='" + ExistingId + "')");
            RequireEntityRead(Result.Status == RuntimeStatus.OK, "captured Entity.Id mismatch: " + Result.Error);
            Result = Host.Execute("entity1b.equality",
                "local W=game:GetService('Workspace'); assert(W:GetEntityById('" + ExistingId +
                "')==W:GetEntityById('" + ExistingId + "'))");
            RequireEntityRead(Result.Status == RuntimeStatus.OK,
                "repeated exact-lifetime equality failed: " + Result.Error);
            Result = Host.Execute("entity1b.invalid",
                "local W=game:GetService('Workspace'); assert(not pcall(function() W:GetEntityById('0') end)); " +
                "assert(not pcall(function() W:GetEntityById('01') end)); " +
                "assert(not pcall(function() W:GetEntityById('18446744073709551616') end)); " +
                "assert(not pcall(function() W:GetEntityById(1) end))");
            RequireEntityRead(Result.Status == RuntimeStatus.OK,
                "canonical ID rejection failed: " + Result.Error);
            Result = Host.Execute("entity1b.position", "local E=game:GetService('Workspace'):GetEntityById('" +
                ExistingId + "'); return E.Position.X");
            RequireEntityRead(Result.Status == RuntimeStatus.OK && Result.HasNumber &&
                Math.Abs(Result.Number - Existing.transform.position.x) <= 0.0001,
                "world X position mismatch");
            RequireEntityRead(ScaleIds.Count == 128, "fewer than 128 qualified keyed entities for scale fixture");
            var ScaleWatch = Stopwatch.StartNew();
            foreach (string Id in ScaleIds) {
                Result = Host.Execute("entity1c.keyed-scale", "local E=game:GetService('Workspace'):GetEntityById('" +
                    Id + "'); assert(E~=nil and E.Id=='" + Id + "')");
                RequireEntityRead(Result.Status == RuntimeStatus.OK,
                    "keyed-scale lookup failed for a qualified entity: " + Result.Error);
            }
            ScaleWatch.Stop();
            Puts("[CarbonLuau:EntityReadFixture] keyed-scale distinct=128 totalWall=" +
                ScaleWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms");

            BaseEntity Parent = GameManager.server.CreateEntity(EntityReadFixturePrefab, new Vector3(100, 100, 100));
            BaseEntity Child = GameManager.server.CreateEntity(EntityReadFixturePrefab, new Vector3(110, 105, 107));
            RequireEntityRead(Parent != null && Child != null, "fixture prefab unavailable");
            Parent.enableSaving = false; Child.enableSaving = false;
            try {
                Parent.Spawn(); Child.Spawn();
                Child.SetParent(Parent, true, true);
                RequireEntityRead(Child.net != null && !Child.IsDestroyed && Child.IsFullySpawned(),
                    "parented child not keyed/spawned");
                RequireEntityRead(Child.transform.position != Child.transform.localPosition,
                    "parented fixture does not distinguish world/local position");
                string ChildId = Child.net.ID.Value.ToString(CultureInfo.InvariantCulture);
                Result = Host.Execute("entity1b.parented", "local E=game:GetService('Workspace'):GetEntityById('" +
                    ChildId + "'); assert(E~=nil); return E.Position.X");
                RequireEntityRead(Result.Status == RuntimeStatus.OK && Result.HasNumber &&
                    Math.Abs(Result.Number - Child.transform.position.x) <= 0.0001 &&
                    Math.Abs(Result.Number - Child.transform.localPosition.x) > 0.0001,
                    "parented Entity.Position did not use root world transform");
                Result = Host.Execute("entity1b.stale.defer", "local E=game:GetService('Workspace'):GetEntityById('" +
                    ChildId + "'); task.defer(function() assert(E==E); " +
                    "assert(not pcall(function() return E.Id end)); " +
                    "assert(not pcall(function() return E.Prefab end)); " +
                    "assert(not pcall(function() return E.Position end)); " +
                    "print('[CarbonLuau:EntityReadFixture] stale PASS') end)");
                RequireEntityRead(Result.Status == RuntimeStatus.OK && Host.HasWork,
                    "stale callback was not queued: " + Result.Error);
                Child.Kill();
                string Logs = "";
                for (int Frame = 0; Frame < 32 && Host.HasWork; ++Frame)
                    foreach (ExecutionResult Callback in Host.Drain()) {
                        RequireEntityRead(Callback.Status == RuntimeStatus.OK,
                            "stale callback failed: " + Callback.Error);
                        Logs += Callback.Logs;
                    }
                RequireEntityRead(Logs.Contains("[CarbonLuau:EntityReadFixture] stale PASS"),
                    "stale callback did not complete");
            }
            finally {
                if (Child != null && !Child.IsDestroyed) Child.Kill();
                if (Parent != null && !Parent.IsDestroyed) Parent.Kill();
            }
            Puts("[CarbonLuau:EntityReadFixture] PASS pre-existing keyed lookup, full prefab, " +
                "world-space parented Position, stale read, immutable fields");
        }
    }
}
