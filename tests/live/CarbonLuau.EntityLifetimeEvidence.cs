// Research-only fixture. Install ONLY on a disposable, isolated Rust/Carbon server.
// This deliberately mutates fixture-owned host objects; it is NOT an Entity adapter.
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CarbonLuau.EntityLifetimeEvidence", "CarbonLuau", "0.1.0")]
    [Description("Isolated exact-build entity lifecycle research; no public Luau API")]
    public class CarbonLuauEntityLifetimeEvidence : RustPlugin
    {
        private const string Prefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private BaseEntity Owned;
        private bool Veto;
        private int KillHooks, SpawnHooks, SpawnedHooks, LoadedHooks, Checks;

        private void Assert(bool Condition, string Label)
        {
            if (!Condition) throw new InvalidOperationException(Label);
            Checks++;
            Puts("[CarbonLuau:EntityLive] CHECK " + Label);
        }
        private void OnServerInitialized() { NextTick(Run); }
        private void OnEntitySpawn(BaseNetworkable Entity)
        {
            if (!ReferenceEquals(Entity, Owned)) return;
            SpawnHooks++;
            Puts("[CarbonLuau:EntityLive] PRESPAWN fully=" + Entity.IsFullySpawned() + " destroyed=" + Entity.IsDestroyed + " net=" + (Entity.net != null));
        }
        private void OnEntitySpawned(BaseNetworkable Entity)
        {
            if (!ReferenceEquals(Entity, Owned)) return;
            SpawnedHooks++;
            Puts("[CarbonLuau:EntityLive] SPAWNED fully=" + Entity.IsFullySpawned() + " occupied=" + ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity));
        }
        private void OnEntityLoaded(BaseNetworkable Entity, BaseNetworkable.LoadInfo Info)
        { if (ReferenceEquals(Entity, Owned)) LoadedHooks++; }
        private object OnEntityKill(BaseNetworkable Entity)
        {
            if (!ReferenceEquals(Entity, Owned)) return null;
            KillHooks++;
            Puts("[CarbonLuau:EntityLive] KILL veto=" + Veto + " destroyed=" + Entity.IsDestroyed + " occupied=" + (Entity.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity)));
            return Veto ? (object)true : null;
        }
        private BaseEntity Create()
        {
            Owned = GameManager.server.CreateEntity(Prefab, new Vector3(0, 100, 0));
            Assert(Owned != null, "fixture prefab exists");
            Owned.enableSaving = false;
            return Owned;
        }
        private void Run()
        {
            bool Complete = false;
            try {
                Puts("[CarbonLuau:EntityLive] BEGIN pool.enabled=" + ConVar.Pool.enabled + " pool.mode=" + ConVar.Pool.mode);
                var First = Create();
                Puts("[CarbonLuau:EntityLive] POOLABLE present=" + (First.GetComponent<Poolable>() != null) + " supported=" + First.gameObject.SupportsPooling());
                First.Spawn();
                var Id = First.net.ID;
                var Instance = First.GetInstanceID();
                Assert(First.IsFullySpawned() && ReferenceEquals(BaseNetworkable.serverEntities.Find(Id), First), "spawn has current keyed occupancy");
                Assert(SpawnHooks == 1 && SpawnedHooks == 1, "prefix and spawned hooks observed");
                Veto = true;
                First.Kill();
                Assert(KillHooks == 1 && !First.IsDestroyed && ReferenceEquals(BaseNetworkable.serverEntities.Find(Id), First), "vetoed Kill did not retire entity");
                Veto = false;
                int Before = KillHooks + SpawnHooks + SpawnedHooks + LoadedHooks;
                BaseNetworkable.serverEntities.UnregisterID(First);
                Assert(BaseNetworkable.serverEntities.Find(Id) == null, "direct unregister removes keyed occupancy");
                BaseNetworkable.serverEntities.RegisterID(First);
                Assert(ReferenceEquals(BaseNetworkable.serverEntities.Find(Id), First), "direct register restores same object and ID");
                Assert(Before == KillHooks + SpawnHooks + SpawnedHooks + LoadedHooks, "registry remove/reinsert bypasses tested lifecycle hooks");
                First.Kill();
                Assert(First.IsDestroyed && First.net == null && BaseNetworkable.serverEntities.Find(Id) == null, "normal Kill removes net and registry before return");
                var Second = Create();
                bool Reused = ReferenceEquals(First, Second);
                Puts("[CarbonLuau:EntityLive] POOL sameManaged=" + Reused + " sameUnityId=" + (Second.GetInstanceID() == Instance));
                if (Reused) Assert(Second.GetInstanceID() == Instance, "actual reuse retains Unity InstanceID");
                Second.Spawn();
                Assert(Second.net.ID.Value != Id.Value, "ordinary fresh spawn allocates different ID");
                Second.Kill();
                var Third = Create();
                bool ThirdReused = ReferenceEquals(First, Third);
                Third.InitLoad(Id);
                Assert(ReferenceEquals(BaseNetworkable.serverEntities.Find(Id), Third), "InitLoad restores explicit ID before Spawn");
                Third.Spawn();
                Assert(Third.net.ID.Value == Id.Value && Third.PrefabName == Prefab && !Third.IsDestroyed && Third.IsFullySpawned(), "forced load path restores same ID and prefab");
                Puts("[CarbonLuau:EntityLive] RESTORED sameManaged=" + ThirdReused + " sameUnityId=" + (Third.GetInstanceID() == Instance) + "; same-object ABA " + (ThirdReused ? "OBSERVED" : "NOT OBSERVED"));
                Complete = true;
            }
            catch (Exception Error) { Puts("[CarbonLuau:EntityLive] FAIL " + Error.Message); }
            finally { Veto = false; if (Owned != null && !Owned.IsDestroyed) Owned.Kill(); Owned = null; }
            if (Complete) Puts("[CarbonLuau:EntityLive] PASS checks=" + Checks + "; controlled-host research only; NOT lifetime-proof PASS");
        }
    }
}
