// Disposable exact-host phase research. Not a production binding or work proof.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.LowLevel;

namespace Oxide.Plugins
{
    [Info("CarbonLuau.WorldPositionPhaseResearch", "CarbonLuau", "0.1.0")]
    [Description("Task-owned late-phase dependency observations, not discovery qualification")]
    public class CarbonLuauWorldPositionPhaseResearch : RustPlugin
    {
        private BaseEntity OwnedEntity;
        private GameObject Owned;
        private TransformAccessArray Access;
        private JobHandle Job;
        private NativeArray<int> WorkerThread;
        private PlayerLoopSystem Original;
        private bool Installed, Scheduled, Finished, Quitting;
        private int OwnerThread, Samples, PendingLate, NodeCount, PhaseCount, StartedFrame;
        private float StartedAt;
        private FieldInfo DataField;

        private struct SlowJob : IJobParallelForTransform
        {
            public NativeArray<int> WorkerThread;
            public void Execute(int Index, TransformAccess Transform)
            {
                // Finite fixture-only work. One worker, one TASK-OWNED Transform.
                // Demonstrates a scheduling possibility, never a latency/work guarantee.
                WorkerThread[0] = Thread.CurrentThread.ManagedThreadId;
                Thread.Sleep(2000);
                Transform.localPosition += new Vector3(0.25f, 0, 0);
            }
        }

        private PlayerLoopSystem Instrument(PlayerLoopSystem Node, int Depth)
        {
            if (++NodeCount > 512 || Depth > 16) throw new Exception("fixture loop envelope exceeded");
            var Children = new List<PlayerLoopSystem>();
            string Name = Node.type == null ? "" : Node.type.FullName;
            if (Node.subSystemList != null)
                foreach (PlayerLoopSystem Child in Node.subSystemList) {
                    Children.Add(Instrument(Child, Depth + 1));
                    if (Name == "UnityEngine.PlayerLoop.PreLateUpdate") {
                        string Stage = Child.type == null ? "UnnamedPreLateChild" : Child.type.Name;
                        Children.Add(new PlayerLoopSystem { type = typeof(CarbonLuauWorldPositionPhaseResearch),
                            updateDelegate = () => StageSample(Stage) });
                    }
                }
            if (Name == "UnityEngine.PlayerLoop.Update" || Name == "UnityEngine.PlayerLoop.FixedUpdate" ||
                Name == "UnityEngine.PlayerLoop.PreLateUpdate" || Name == "UnityEngine.PlayerLoop.PostLateUpdate") {
                string Stage = Node.type.Name;
                ++PhaseCount;
                Children.Add(new PlayerLoopSystem { type = typeof(CarbonLuauWorldPositionPhaseResearch),
                    updateDelegate = () => StageSample(Stage) });
                Puts("[CarbonLuau:PositionPhaseResearch] ATTACH " + Stage);
            }
            // Copy every array. Do not mutate the original tree retained for restoration.
            Node.subSystemList = Children.ToArray();
            return Node;
        }

        private void OnServerInitialized()
        {
            try {
                OwnerThread = Thread.CurrentThread.ManagedThreadId;
                DataField = typeof(TransformHandle).GetField("pTransformData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (DataField == null) throw new Exception("pinned descriptor absent");
                OwnedEntity = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", new Vector3(41, 100, 0));
                if (OwnedEntity == null) throw new Exception("task-owned entity creation failed");
                OwnedEntity.enableSaving = false;
                OwnedEntity.Spawn();
                if (!OwnedEntity.IsFullySpawned() || OwnedEntity.net == null || OwnedEntity.net.ID.Value == 0 ||
                    !ReferenceEquals(BaseNetworkable.serverEntities.Find(OwnedEntity.net.ID), OwnedEntity))
                    throw new Exception("task-owned entity admission precondition failed");
                // Read-only private test witness: do not manufacture a baseline,
                // admit a public proxy, or create membership from a token table.
                var HostPlugin = plugins.Find("CarbonLuau");
                if (HostPlugin == null) throw new Exception("qualified host plugin absent");
                FieldInfo ModelField = HostPlugin.GetType().GetField("EntityLifetimes", BindingFlags.Instance | BindingFlags.NonPublic);
                object Model = ModelField == null ? null : ModelField.GetValue(HostPlugin);
                MethodInfo Observed = Model == null ? null : Model.GetType().GetMethod("HasCompletedObservation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (Observed == null || !(bool)Observed.Invoke(Model, new object[] { OwnedEntity }))
                    throw new Exception("qualified full-Spawn observation absent");
                // Ordinary host API, TASK-OWNED entity only. Challenge whether late
                // phase quiescence is universal or merely a scheduled position tick.
                OwnedEntity.ToggleNetworkPositionTick(false);
                Owned = OwnedEntity.gameObject;
                Access = new TransformAccessArray(new[] { Owned.transform });
                WorkerThread = new NativeArray<int>(1, Allocator.Persistent);
                Original = PlayerLoop.GetCurrentPlayerLoop();
                PlayerLoopSystem Modified = Instrument(Original, 0);
                if (PhaseCount != 4) throw new Exception("pinned phase coverage mismatch");
                PlayerLoop.SetPlayerLoop(Modified);
                Installed = true;
                StartedAt = Time.realtimeSinceStartup;
                Puts("[CarbonLuau:PositionPhaseResearch] READY nodes=" + NodeCount + " owner=" + OwnerThread +
                    " entity=" + OwnedEntity.GetType().Name + " id=" + OwnedEntity.net.ID.Value +
                    " fullySpawned=True keyed=True observedCompleted=True");
                Puts("[CarbonLuau:PositionPhaseResearch] CONTEXT task-owned ToggleNetworkPositionTick(false); no registry/topology/native mutation");
            } catch (Exception Error) { Fail(Error.GetType().Name); }
        }

        private void StageSample(string Stage)
        {
            if (Finished || Quitting) return;
            try {
                if (Thread.CurrentThread.ManagedThreadId != OwnerThread) throw new Exception("phase thread mismatch");
                if (!Scheduled) {
                    // Let ordinary post-Spawn invokes run before testing the steady phase.
                    if (Stage != "Update" || Time.realtimeSinceStartup - StartedAt < 3) return;
                    Job = new SlowJob { WorkerThread = WorkerThread }.Schedule(Access);
                    JobHandle.ScheduleBatchedJobs(); // Fixture only; never a proposed bounded read.
                    Scheduled = true;
                    StartedFrame = Time.frameCount;
                    Puts("[CarbonLuau:PositionPhaseResearch] SCHEDULE frame=" + StartedFrame + " postSpawnGrace=3s");
                }
                // Diagnostic on a stable task-owned hierarchy. No world or Position getter.
                IntPtr Descriptor = (IntPtr)DataField.GetValue(Owned.transform.transformHandle);
                IntPtr Hierarchy = Marshal.ReadIntPtr(Descriptor);
                ulong Fence = unchecked((ulong)Marshal.ReadInt64(Hierarchy));
                long PollStart = System.Diagnostics.Stopwatch.GetTimestamp();
                bool Completed = Job.IsCompleted; // Diagnostic flush/status, not a work-bound primitive.
                double PollMs = (System.Diagnostics.Stopwatch.GetTimestamp() - PollStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (!Completed && Stage != "Update") ++PendingLate;
                if (Samples++ < 20)
                    Puts("[CarbonLuau:PositionPhaseResearch] SAMPLE stage=" + Stage + " frame=" + Time.frameCount +
                        " completed=" + Completed + " fence=" + Fence.ToString("x") + " pollMs=" + PollMs);
                if (Completed) {
                    Job.Complete(); // Fixture teardown ownership; not a bounded query operation.
                    float X = Owned.transform.position.x;
                    if (X != 41.25f) throw new Exception("fixture result mismatch");
                    Finished = true;
                    Puts("[CarbonLuau:PositionPhaseResearch] OBSERVATION_PASS pendingLate=" + PendingLate +
                        " endFrame=" + Time.frameCount + " worldX=" + X + " workerThread=" + WorkerThread[0] +
                        " ownerThread=" + OwnerThread + " NOT_HARD_BOUND NOT_DISCOVERY_QUALIFICATION");
                    Cleanup(); Quit();
                } else if (Time.realtimeSinceStartup - StartedAt > 20) Fail("fixture deadline");
            } catch (Exception Error) { Fail(Error.GetType().Name); }
        }

        private void Fail(string Reason)
        {
            Puts("[CarbonLuau:PositionPhaseResearch] FAIL " + Reason);
            Finished = true;
            Cleanup(); Quit();
        }
        private void Cleanup()
        {
            if (Installed) { PlayerLoop.SetPlayerLoop(Original); Installed = false; }
            if (Scheduled) { Job.Complete(); Scheduled = false; }
            if (Access.isCreated) Access.Dispose();
            if (WorkerThread.IsCreated) WorkerThread.Dispose();
            if (OwnedEntity != null && !OwnedEntity.IsDestroyed) OwnedEntity.Kill();
            OwnedEntity = null;
            Owned = null;
        }
        private void Quit()
        {
            if (Quitting) return;
            Quitting = true;
            timer.Once(1, () => { Puts("[CarbonLuau:PositionPhaseResearch] CLEANUP loop restored; owned job/array/object released"); ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"); });
        }
        private void Unload() { Cleanup(); }
    }
}
