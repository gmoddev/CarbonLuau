using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private EntityDiscoveryTraversal EntityDiscovery;
        private EntityPositionBorrow EntityPositionReader;
        private Dictionary<Type, MethodBase[]> EntityDiscoveryPatchPaths;
        private long EntityDiscoveryMaximumTurnTicks;
        private int EntityDiscoveryMaximumTurnRaw, EntityDiscoveryMaximumTurnUnits;
        private EntityDiscoveryFrame EntityDiscoveryFramePump;
        private FieldInfo EntityDiscoveryCachedPrefab;
        private uint[] EntityDiscoveryPrefabIds;
        private string[] EntityDiscoveryPrefabNames;
        partial void RunEntityDiscoveryFixtures();

        // One fixed Update receiver. No per-query/per-turn Carbon queue enqueue,
        // list resize, shared monitor wait, engine job or PlayerLoop patch.
        private sealed class EntityDiscoveryFrame : MonoBehaviour
        {
            internal CarbonLuau Owner;
            private void Update()
            {
                CarbonLuau Current = Owner;
                if (Current != null) {
                    Current.RunEntityDestroyedFrame();
                    Current.RunEntityDiscoveryFrame();
                }
            }
            private void OnDestroy()
            {
                CarbonLuau Current = Owner;
                Owner = null;
                // Unexpected receiver loss cannot strand native captures. Normal
                // teardown clears Owner before engine destruction, so this does
                // not recursively tear down an already retiring runtime.
                if (Current != null) Current.ReleaseNative();
            }
        }

        private static long EntityDiscoveryClock { get { return Stopwatch.GetTimestamp(); } }

        private void InitializeEntityDiscovery()
        {
            try {
                EntityPositionReader = new EntityPositionBorrow(EntityOwnerThread);
                if (!EntityPositionReader.Available || !EntityLifetimes.CatalogReady) return;
                // Cold private copy of the already initialized manifest pool.
                // No StringPool.Init/warning/host cold getter in a scan quantum.
                if (!StringPool.initialized) { PrintWarning("[CarbonLuau:Discovery] Manifest pool not initialized."); return; }
                // Carbon can publicize exact host fields in its loaded image;
                // field identity/type remains pinned, visibility is not authority.
                FieldInfo PoolField = typeof(StringPool).GetField("toString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                EntityDiscoveryCachedPrefab = typeof(BaseNetworkable).GetField("_prefabName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var Pool = PoolField == null ? null : PoolField.GetValue(null) as Dictionary<uint, string>;
                if (Pool == null || EntityDiscoveryCachedPrefab == null || Pool.Count > EntityDiscoveryPolicy.CatalogSlots) {
                    PrintWarning("[CarbonLuau:Discovery] Manifest shape/capacity unavailable; entries=" + (Pool == null ? -1 : Pool.Count)); return;
                }
                EntityDiscoveryPrefabIds = new uint[Pool.Count];
                EntityDiscoveryPrefabNames = new string[Pool.Count];
                int PoolIndex = 0;
                foreach (KeyValuePair<uint, string> Pair in Pool) {
                    if (PoolIndex == EntityDiscoveryPrefabIds.Length) return;
                    EntityDiscoveryPrefabIds[PoolIndex] = Pair.Key;
                    EntityDiscoveryPrefabNames[PoolIndex++] = Pair.Value;
                }
                if (PoolIndex != EntityDiscoveryPrefabIds.Length) return;
                Array.Sort(EntityDiscoveryPrefabIds, EntityDiscoveryPrefabNames);
                // Cold startup inventory only; no reflection hierarchy walk per
                // candidate. The exact Rust hash bounds the type/path inventory.
                var Paths = new Dictionary<Type, MethodBase[]>();
                foreach (Type Type in typeof(BaseEntity).Assembly.GetTypes()) {
                    if (!typeof(BaseEntity).IsAssignableFrom(Type)) continue;
                    if (Paths.Count == EntityDiscoveryPolicy.MaximumEntityTypes) return;
                    MethodInfo Effective = Type.GetMethod(nameof(BaseNetworkable.Spawn),
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    bool Covered = false;
                    var Path = new List<MethodBase>();
                    foreach (MethodBase Target in EntitySpawnTargets) {
                        if (!Target.DeclaringType.IsAssignableFrom(Type)) continue;
                        Path.Add(Target);
                        if (SameEntityMethod(Target, Effective)) Covered = true;
                    }
                    if (!Covered || Path.Count < 2 || Path.Count > 24) return;
                    Paths.Add(Type, Path.ToArray());
                }
                EntityDiscoveryPatchPaths = Paths;
                EntityDiscovery = new EntityDiscoveryTraversal(EntityLifetimes,
                    new EntityDiscoveryTraversal.WorkPolicy(EntityDiscoveryPolicy.MaximumQueries,
                        EntityDiscoveryPolicy.MaximumPerDomain, EntityDiscoveryPolicy.WorkPerTurn,
                        EntityDiscoveryPolicy.RawSlotsPerTurn, EntityDiscoveryPolicy.MaximumResults,
                        EntityDiscoveryPolicy.CatalogSlots, EntityDiscoveryPolicy.MaximumDeliveriesPerTurn,
                        EntityDiscoveryPolicy.MaximumPrefabBytes,
                        checked(Stopwatch.Frequency * EntityDiscoveryPolicy.DeadlineSeconds)));
                var PumpObject = new GameObject("CarbonLuauPrivateDiscoveryFrame");
                try {
                    EntityDiscoveryFramePump = PumpObject.AddComponent<EntityDiscoveryFrame>();
                    EntityDiscoveryFramePump.Owner = this;
                }
                catch { UnityEngine.Object.Destroy(PumpObject); throw; }
                Puts("[CarbonLuau:Discovery] Private bounded traversal ready; catalog=" + EntityLifetimes.CatalogSlotCount +
                    " manifestEntries=" + EntityDiscoveryPrefabIds.Length);
            }
            catch (Exception) {
                StopEntityDiscovery();
                PrintWarning("[CarbonLuau:Discovery] Private traversal unavailable; exact keyed Entity reads remain independent.");
            }
        }

        // Never wait for Harmony's shared lock. Contention is whole-query
        // controlled failure, not permission to skip a lifetime/path check.
        private bool EntityDiscoveryPatchCurrent(BaseEntity Entity)
        {
            MethodBase[] Path;
            IDictionary State = EntityReadPatchState;
            if (ReferenceEquals(Entity, null) || EntityDiscoveryPatchPaths == null || State == null ||
                !EntityDiscoveryPatchPaths.TryGetValue(Entity.GetType(), out Path) ||
                !Monitor.TryEnter(State, 0)) return false;
            try {
                if (!ReferenceEquals(EntityReadPatchStateField.GetValue(null), State) ||
                    State.Count > EntityDiscoveryPolicy.MaximumPatchRecords) return false;
                foreach (MethodBase Target in Path) {
                    byte[] Expected;
                    if (!EntityReadPatchStamps.TryGetValue(Target, out Expected) ||
                        !ReferenceEquals(State[Target], Expected)) {
                        BreakEntityObserver("discovery Entity Spawn patch record changed");
                        return false;
                    }
                }
                return true;
            }
            finally { Monitor.Exit(State); }
        }

        private bool EntityDiscoveryAuthorityCurrent(FacadeSession Session,
            FacadeSession.PublicationWitness Witness, EntityLifetimeModel.Authority Authority,
            NativeRuntime Runtime)
        {
            return !Stopping && !EntityObserverBroken && EntityStartupQualified &&
                BaseNetworkable.serverEntities != null && BaseNetworkable.serverEntities.Count <= EntityDiscoveryPolicy.CatalogSlots &&
                ReferenceEquals(Native, Runtime) && Gameplay != null && Session != null &&
                Session.Active && Gameplay.IsActive(Session) &&
                EntityFacadeCurrent(Authority, Session, Witness);
        }

        // Private asynchronous admission only. No Luau method/callback binding.
        private EntityDiscoveryTraversal.StartStatus StartEntityDiscovery(FacadeSession Session,
            EntityDiscoveryTraversal.Query Query, Action<EntityDiscoveryTraversal.Completion> Callback,
            out ulong RequestId, Func<bool> CompletionAuthorized = null)
        {
            RequestId = 0;
            if (Thread.CurrentThread.ManagedThreadId != EntityOwnerThread || EntityDiscovery == null ||
                EntityObserverBroken || Session == null || Callback == null || Session.Disposed)
                return EntityDiscoveryTraversal.StartStatus.Unavailable;
            FacadeSession.PublicationWitness Witness = Session.CapturePublicationWitness();
            var Authority = new EntityLifetimeModel.Authority(checked((ulong)Session.VmGenerationId),
                checked((ulong)Session.DomainLifetimeId), Witness.Token);
            NativeRuntime Runtime = Native;
            Func<bool> Current = () => EntityDiscoveryAuthorityCurrent(Session, Witness, Authority, Runtime);
            Func<EntityLifetimeModel.Authority, bool> CurrentBinding = Value => Current() &&
                Value.VmGeneration == Authority.VmGeneration && Value.DomainLifetime == Authority.DomainLifetime &&
                Value.PublicationLifetime == Authority.PublicationLifetime;
            EntityDiscoveryTraversal.StartStatus Status = EntityDiscovery.TryStart(Query, Authority.DomainLifetime, Candidate => {
                if (!Current() || !EntityLifetimes.IsCatalogCandidateCurrent(Candidate))
                    return new EntityDiscoveryTraversal.CandidateObservation(EntityDiscoveryTraversal.CandidateStatus.Skip);
                BaseEntity Entity = Candidate.Target as BaseEntity;
                if (ReferenceEquals(Entity, null))
                    return new EntityDiscoveryTraversal.CandidateObservation(EntityDiscoveryTraversal.CandidateStatus.Skip);
                if (!EntityDiscoveryPatchCurrent(Entity))
                    return new EntityDiscoveryTraversal.CandidateObservation(EntityDiscoveryTraversal.CandidateStatus.Failure);
                EntityLifetimeModel.HostEvidence Evidence;
                if (!EntityLifetimes.TryObserveCatalog(Candidate, Authority, CurrentBinding, ReadDiscoveryEntityEvidence, out Evidence))
                    return new EntityDiscoveryTraversal.CandidateObservation(EntityDiscoveryTraversal.CandidateStatus.Skip);
                EntityPositionComposition.Position Position;
                if (!EntityPositionReader.TryObserve(Entity, out Position))
                    return new EntityDiscoveryTraversal.CandidateObservation(EntityDiscoveryTraversal.CandidateStatus.Failure);
                if (!EntityLifetimes.IsCatalogCandidateCurrent(Candidate) ||
                    !EntityLifetimes.TryObserveCatalog(Candidate, Authority, CurrentBinding, ReadDiscoveryEntityEvidence, out Evidence))
                    return new EntityDiscoveryTraversal.CandidateObservation(EntityDiscoveryTraversal.CandidateStatus.Skip);
                return new EntityDiscoveryTraversal.CandidateObservation(Candidate, Candidate.Birth,
                    Evidence.NetworkId, Evidence.Prefab, Position.X, Position.Y, Position.Z);
            }, Current, Observation => {
                if (!Current() || !EntityLifetimes.IsCatalogCandidateCurrent(Observation.Candidate)) return false;
                BaseEntity Entity = Observation.Candidate.Target as BaseEntity;
                if (ReferenceEquals(Entity, null) || !EntityDiscoveryPatchCurrent(Entity)) return false;
                EntityLifetimeModel.HostEvidence Evidence;
                return EntityLifetimes.TryObserveCatalog(Observation.Candidate, Authority, CurrentBinding, ReadDiscoveryEntityEvidence, out Evidence) &&
                    Evidence.NetworkId == Observation.Id && String.Equals(Evidence.Prefab, Observation.Prefab, StringComparison.Ordinal);
            }, Completion => {
                // Delivery and this last lifetime check are one uninterrupted
                // owner turn. Retired requests never enter replacement code.
                if ((CompletionAuthorized ?? Current)()) Callback(Completion);
            }, EntityDiscoveryClock, out RequestId);
            return Status;
        }

        private EntityLifetimeModel.HostEvidence ReadDiscoveryEntityEvidence(object Identity)
        {
            BaseEntity Entity = Identity as BaseEntity;
            if (Entity == null || Entity.net == null || BaseNetworkable.serverEntities == null ||
                BaseNetworkable.serverEntities.Count > EntityDiscoveryPolicy.CatalogSlots)
                return default(EntityLifetimeModel.HostEvidence);
            string Prefab = EntityDiscoveryCachedPrefab.GetValue(Entity) as string;
            if (Prefab == null) {
                int Index = Array.BinarySearch(EntityDiscoveryPrefabIds, Entity.prefabID);
                if (Index >= 0) Prefab = EntityDiscoveryPrefabNames[Index];
            }
            return new EntityLifetimeModel.HostEvidence(!Entity.IsDestroyed, Entity.IsFullySpawned(),
                true, Entity.net.ID.Value, Prefab, BaseNetworkable.serverEntities.Find(Entity.net.ID));
        }

        private void RunEntityDiscoveryFrame()
        {
            if (Stopping || Native == null || EntityDiscovery == null || EntityDiscovery.ActiveCount == 0 ||
                (Host != null && Host.Busy)) return;
            try {
                Native.CheckOwner();
                PumpEntityDiscovery();
            }
            catch (Exception) {
                ReleaseNative();
                PrintWarning("[CarbonLuau:Discovery] Private traversal stopped after an internal intake failure.");
            }
        }

        private void SweepIdleEntityDiscovery()
        {
            // OnTick is not the frame scheduler. Active queries receive exactly
            // one independently bounded Update turn, never a second Tick drain.
            if (EntityDiscovery != null && EntityDiscovery.ActiveCount == 0) {
                EntityLifetimes.SweepCatalog(EntityDiscoveryPolicy.CatalogSweepPerTurn);
            }
        }

        private void PumpEntityDiscovery()
        {
            if (EntityDiscovery == null) return;
            long Began = EntityDiscoveryClock;
            EntityLifetimes.SweepCatalog(EntityDiscoveryPolicy.CatalogSweepPerTurn);
            EntityDiscoveryTraversal.TurnWork Work = EntityDiscovery.RunTurn(EntityDiscoveryClock);
            EntityDiscoveryMaximumTurnTicks = Math.Max(EntityDiscoveryMaximumTurnTicks, EntityDiscoveryClock - Began);
            EntityDiscoveryMaximumTurnRaw = Math.Max(EntityDiscoveryMaximumTurnRaw, Work.RawSlots);
            EntityDiscoveryMaximumTurnUnits = Math.Max(EntityDiscoveryMaximumTurnUnits, Work.Units);
        }

        private void StopEntityDiscovery()
        {
            RetireDiscovery(null);
            if (EntityDiscovery != null) EntityDiscovery.Dispose();
            EntityDiscovery = null;
            EntityDiscoveryFrame Pump = EntityDiscoveryFramePump;
            EntityDiscoveryFramePump = null;
            if (Pump != null) {
                Pump.Owner = null;
                Pump.enabled = false;
                UnityEngine.Object.Destroy(Pump.gameObject);
            }
            EntityPositionReader = null;
            EntityDiscoveryPatchPaths = null;
            EntityDiscoveryCachedPrefab = null;
            EntityDiscoveryPrefabIds = null;
            EntityDiscoveryPrefabNames = null;
        }
    }
}
