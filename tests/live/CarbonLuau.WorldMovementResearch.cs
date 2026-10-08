// Research only: install on the task-owned disposable Rust/Carbon host.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CarbonLuau.WorldMovementResearch", "CarbonLuau", "0.1.0")]
    [Description("Private spatial movement evidence; no Luau discovery API")]
    public class CarbonLuauWorldMovementResearch : RustPlugin
    {
        // Alternative-seam research only. Never installed by production CarbonLuau.
        public sealed class MovementMarker : MonoBehaviour { }
        private const string Prefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private readonly List<BaseEntity> Owned = new List<BaseEntity>();
        private int OwnerThread;
        private readonly List<object> Dispatchers = new List<object>();
        private readonly List<Type> TrackedTypes = new List<Type>();
        private readonly List<MovementMarker> Markers = new List<MovementMarker>();
        private object MarkerDispatcher;
        private Type DispatcherType;
        private object TrackingKind;
        private MethodInfo DrainMethod;
        private MethodInfo EnableMethod;
        private object BorrowedDispatcher;
        private Type BorrowedTrackedType;
        private MethodInfo BorrowedDrain;
        private Delegate BorrowedCallback;
        private string BorrowedLabel;
        private int BorrowedCount;
        private int BorrowedCalls;
        private bool InsideBorrowedDrain;
        private int HistoryBorrowedCalls;
        private int HistoryStartFrame;
        private Vector3 HistoryExpectedPosition;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeImageInfo
        {
            public IntPtr FileName;
            public IntPtr Base;
            public IntPtr SymbolName;
            public IntPtr SymbolAddress;
        }

        // Read-only research: resolve existing icalls; never replace or invoke them.
        [DllImport("libmonobdwgc-2.0.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mono_lookup_internal_call(IntPtr Method);
        [DllImport("libmonobdwgc-2.0.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mono_method_get_name(IntPtr Method);
        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dladdr(IntPtr Address, out NativeImageInfo Info);

        private void InspectNativeIcalls()
        {
            if (Application.platform != RuntimePlatform.LinuxServer) return;
            foreach (Type Type in new[] { DispatcherType, typeof(Transform), typeof(Physics), typeof(Unity.Jobs.LowLevel.Unsafe.JobsUtility) }) {
                foreach (MethodInfo Method in Type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) {
                    if ((Method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0) continue;
                    if (Type != DispatcherType && Method.Name != "set_position_Injected" &&
                        Method.Name != "set_localPosition_Injected" && Method.Name != "SetParent" &&
                        Method.Name != "get_position_Injected" && Method.Name != "Simulate_Internal_Injected" &&
                        Method.Name != "SyncTransforms" && Method.Name != "ScheduleParallelForTransformReadOnly_Injected") continue;
                    IntPtr Handle = Method.MethodHandle.Value;
                    if (Marshal.PtrToStringAnsi(mono_method_get_name(Handle)) != Method.Name)
                        throw new InvalidOperationException("native method identity mismatch");
                    IntPtr Address = mono_lookup_internal_call(Handle);
                    NativeImageInfo Info;
                    if (Address == IntPtr.Zero || dladdr(Address, out Info) == 0)
                        throw new InvalidOperationException("native icall image unresolved");
                    Puts("[CarbonLuau:WorldMovementResearch] NATIVE_ICALL type=" + Type.FullName +
                        " method=" + Method.Name + " image=" + Marshal.PtrToStringAnsi(Info.FileName) +
                        " offset=0x" + (Address.ToInt64() - Info.Base.ToInt64()).ToString("x"));
                }
            }
        }

        private void ReceiveBorrowed<T>(T Data)
        {
            if (!InsideBorrowedDrain || Thread.CurrentThread.ManagedThreadId != OwnerThread)
                throw new InvalidOperationException("borrowed callback occurred outside owner-thread drain");
            BorrowedCalls++;
            // Research reflection only. Inspect length while the native view is borrowed;
            // never retain, copy, dispose or enumerate this internal NativeArray.
            object Ids = typeof(T).GetField("transformedID").GetValue(Data);
            BorrowedCount = (int)Ids.GetType().GetProperty("Length").GetValue(Ids, null);
            Puts("[CarbonLuau:WorldMovementResearch] BORROWED_DISPATCH " + BorrowedLabel + " count=" + BorrowedCount);
        }

        private void StartDispatchers(BaseEntity Entity)
        {
            DispatcherType = typeof(Transform).Assembly.GetType("UnityEngine.ObjectDispatcher", true);
            InspectNativeIcalls();
            Type TrackingType = DispatcherType.GetNestedType("TransformTrackingType", BindingFlags.Public | BindingFlags.NonPublic);
            TrackingKind = Enum.Parse(TrackingType, "GlobalTRS");
            EnableMethod = DispatcherType.GetMethod("EnableTransformTracking", new[] { TrackingType, typeof(Type[]) });
            DrainMethod = DispatcherType.GetMethod("GetTransformChangesAndClear", new[] { typeof(Type), TrackingType, typeof(bool) });
            foreach (Type Tracked in new[] { typeof(BaseEntity), Entity.GetType(), typeof(Transform) }) {
                object Dispatcher = Activator.CreateInstance(DispatcherType, true);
                try {
                    PropertyInfo History = DispatcherType.GetProperty("maxDispatchHistoryFramesCount");
                    Puts("[CarbonLuau:WorldMovementResearch] HISTORY_DEFAULT type=" + Tracked.FullName + " frames=" + History.GetValue(Dispatcher, null));
                    History.SetValue(Dispatcher, 1, null);
                    EnableMethod.Invoke(Dispatcher, new object[] { TrackingKind, new[] { Tracked } });
                    Dispatchers.Add(Dispatcher);
                    TrackedTypes.Add(Tracked);
                    Puts("[CarbonLuau:WorldMovementResearch] DISPATCHER_ENABLED type=" + Tracked.FullName);
                }
                catch (Exception Error) {
                    ((IDisposable)Dispatcher).Dispose();
                    Puts("[CarbonLuau:WorldMovementResearch] DISPATCHER_REJECTED type=" + Tracked.FullName + " error=" + Error.GetBaseException().Message);
                }
            }
            Drain("INITIAL", Entity);
            Type DataType = typeof(Transform).Assembly.GetType("UnityEngine.TransformDispatchData", true);
            Type CallbackType = typeof(Action<>).MakeGenericType(DataType);
            BorrowedTrackedType = Entity.GetType();
            BorrowedDispatcher = Activator.CreateInstance(DispatcherType, true);
            EnableMethod.Invoke(BorrowedDispatcher, new object[] { TrackingKind, new[] { BorrowedTrackedType } });
            DispatcherType.GetProperty("maxDispatchHistoryFramesCount").SetValue(BorrowedDispatcher, 1, null);
            BorrowedDrain = DispatcherType.GetMethod("DispatchTransformChangesAndClear", new[] { typeof(Type), TrackingType, CallbackType });
            MethodInfo Receiver = GetType().GetMethod("ReceiveBorrowed", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(DataType);
            BorrowedCallback = Delegate.CreateDelegate(CallbackType, this, Receiver);
        }

        private void Drain(string Label, BaseEntity Entity, bool? ExpectedExact = null)
        {
            for (int Index = 0; Index < Dispatchers.Count; Index++) {
                var Changes = (Component[])DrainMethod.Invoke(Dispatchers[Index], new object[] { TrackedTypes[Index], TrackingKind, false });
                bool FoundEntity = false;
                foreach (Component Change in Changes)
                    if (ReferenceEquals(Change, Entity) || ReferenceEquals(Change, Entity.transform)) FoundEntity = true;
                Puts("[CarbonLuau:WorldMovementResearch] DISPATCHER " + Label + " type=" + TrackedTypes[Index].FullName + " count=" + Changes.Length + " found=" + FoundEntity);
                if (ExpectedExact.HasValue && TrackedTypes[Index] == Entity.GetType() && FoundEntity != ExpectedExact.Value)
                    throw new InvalidOperationException("exact-type dispatcher assertion failed: " + Label);
            }
            if (BorrowedDispatcher != null) {
                BorrowedLabel = Label;
                BorrowedCount = -1;
                InsideBorrowedDrain = true;
                try { BorrowedDrain.Invoke(BorrowedDispatcher, new object[] { BorrowedTrackedType, TrackingKind, BorrowedCallback }); }
                finally { InsideBorrowedDrain = false; }
                if (ExpectedExact == true && Entity.GetType() == BorrowedTrackedType && BorrowedCount < 1)
                    throw new InvalidOperationException("borrowed dispatcher assertion failed: " + Label);
            }
            if (MarkerDispatcher != null) {
                var Changes = (Component[])DrainMethod.Invoke(MarkerDispatcher, new object[] { typeof(MovementMarker), TrackingKind, false });
                MovementMarker Expected = Entity.GetComponent<MovementMarker>();
                bool Present = false;
                foreach (Component Change in Changes) if (ReferenceEquals(Change, Expected)) Present = true;
                Puts("[CarbonLuau:WorldMovementResearch] MARKER_DISPATCH " + Label + " count=" + Changes.Length + " found=" + Present);
                if (ExpectedExact == true && !Present) throw new InvalidOperationException("marker dispatcher assertion failed: " + Label);
            }
        }

        private void OnServerInitialized()
        {
            OwnerThread = Thread.CurrentThread.ManagedThreadId;
            NextTick(Run);
        }

        private BaseEntity Create(Vector3 Position)
        {
            BaseEntity Entity = GameManager.server.CreateEntity(Prefab, Position);
            if (Entity == null) throw new InvalidOperationException("fixture prefab unavailable");
            Owned.Add(Entity);
            Entity.enableSaving = false;
            Entity.Spawn();
            Entity.NetworkPositionTick();
            return Entity;
        }

        private bool Found(BaseEntity Entity, Vector3 Position)
        {
            var Results = new List<BaseEntity>();
            BaseEntity.Query.Server.GetInSphere(Position, 0.5f, Results,
                BaseEntity.Query.DistanceCheckType.OnlyCenter);
            return Results.Exists(Value => ReferenceEquals(Value, Entity));
        }

        private void Observe(string Label, BaseEntity Entity, Vector3 Position)
        {
            bool Occupied = Entity.net != null &&
                ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity);
            Puts("[CarbonLuau:WorldMovementResearch] " + Label +
                " ownerThread=" + (Thread.CurrentThread.ManagedThreadId == OwnerThread) +
                " fully=" + Entity.IsFullySpawned() + " occupied=" + Occupied +
                " syncPosition=" + Entity.syncPosition +
                " hasChanged=" + Entity.transform.hasChanged +
                " livePosition=" + Entity.transform.position +
                " indexedAtLivePosition=" + Found(Entity, Position));
        }

        private void Run()
        {
            bool Passed = false;
            try {
                if (BaseEntity.Query.Server == null) throw new InvalidOperationException("host EntityTree unavailable");
                BaseEntity Entity = Create(new Vector3(-300, 100, -300));
                StartDispatchers(Entity);
                MarkerDispatcher = Activator.CreateInstance(DispatcherType, true);
                EnableMethod.Invoke(MarkerDispatcher, new object[] { TrackingKind, new[] { typeof(MovementMarker) } });
                Markers.Add(Entity.gameObject.AddComponent<MovementMarker>());
                DrainMethod.Invoke(MarkerDispatcher, new object[] { typeof(MovementMarker), TrackingKind, false });
                if (!Found(Entity, Entity.transform.position))
                    throw new InvalidOperationException("initial entity missing from host tree");
                Vector3 Destination = new Vector3(300, 100, 300);
                int BeforeMoveCalls = BorrowedCalls;
                Entity.ServerWorldPosition = Destination;
                Puts("[CarbonLuau:WorldMovementResearch] PRODUCER_CALLBACKS directMove=" + (BorrowedCalls - BeforeMoveCalls));
                if (BorrowedCalls != BeforeMoveCalls) throw new InvalidOperationException("unexpected producer callback");
                Drain("SERVER_WORLD_POSITION_IMMEDIATE", Entity, true);
                Observe("SERVER_WORLD_POSITION_BEFORE_TICK", Entity, Destination);
                if (Found(Entity, Destination))
                    throw new InvalidOperationException("expected exact-host delayed movement counterexample absent");
                Entity.NetworkPositionTick();
                Observe("SERVER_WORLD_POSITION_AFTER_TICK", Entity, Destination);
                if (!Found(Entity, Destination))
                    throw new InvalidOperationException("explicit position tick did not maintain host tree");

                BaseEntity Parent = Create(new Vector3(-300, 100, -300));
                BaseEntity Child = Create(new Vector3(-298, 100, -300));
                Markers.Add(Parent.gameObject.AddComponent<MovementMarker>());
                Markers.Add(Child.gameObject.AddComponent<MovementMarker>());
                DrainMethod.Invoke(MarkerDispatcher, new object[] { typeof(MovementMarker), TrackingKind, false });
                Child.SetParent(Parent, true, true);
                Drain("REPARENT_IMMEDIATE", Child, true);
                Child.NetworkPositionTick();
                Parent.ServerWorldPosition = new Vector3(0, 100, 0);
                Drain("PARENT_MOVED_CHILD_IMMEDIATE", Child, true);
                Parent.NetworkPositionTick();
                Observe("PARENT_MOVED_CHILD_BEFORE_CHILD_TICK", Child, Child.transform.position);
                Child.NetworkPositionTick();
                Observe("PARENT_MOVED_CHILD_AFTER_CHILD_TICK", Child, Child.transform.position);
                Parent.transform.rotation = Quaternion.Euler(0, 90, 0);
                Drain("PARENT_ROTATED_CHILD_IMMEDIATE", Child, true);
                Parent.transform.localScale = new Vector3(2, 2, 2);
                Drain("PARENT_SCALED_CHILD_IMMEDIATE", Child, true);
                Child.transform.localPosition = new Vector3(9, 1, 3);
                Drain("LOCAL_POSITION_IMMEDIATE", Child, true);
                Child.SetParent(null, true, true);
                Drain("UNPARENT_IMMEDIATE", Child, true);

                Entity.transform.position = new Vector3(-100, 100, 100);
                Drain("DIRECT_TRANSFORM_IMMEDIATE", Entity, true);
                Observe("DIRECT_TRANSFORM_BEFORE_TICK", Entity, Entity.transform.position);
                Entity.NetworkPositionTick();
                Observe("DIRECT_TRANSFORM_AFTER_TICK", Entity, Entity.transform.position);
                Entity.transform.SetPositionAndRotation(new Vector3(100, 100, 100), Quaternion.identity);
                Drain("SET_POSITION_AND_ROTATION_IMMEDIATE", Entity, true);
                for (int Move = 0; Move < 100; Move++) Entity.transform.position += Vector3.right;
                Drain("REPEATED_100_MOVES_IMMEDIATE", Entity, true);
                Entity.enabled = false;
                Entity.transform.position += new Vector3(25, 0, 0);
                Observe("DISABLED_COMPONENT", Entity, Entity.transform.position);
                Drain("DISABLED_COMPONENT_IMMEDIATE", Entity, true);
                Entity.enabled = true;
                Entity.gameObject.SetActive(false);
                Entity.transform.position += new Vector3(25, 0, 0);
                Observe("INACTIVE_GAMEOBJECT", Entity, Entity.transform.position);
                Drain("INACTIVE_GAMEOBJECT_IMMEDIATE", Entity, true);
                Entity.gameObject.SetActive(true);
                Drain("REACTIVATED", Entity);

                Child.SetParent(Parent, true, true);
                Parent.SetParent(Entity, true, true);
                Drain("GRANDPARENT_HIERARCHY", Child, true);
                Entity.transform.position += new Vector3(40, 0, 0);
                Drain("GRANDPARENT_TRANSLATION_IMMEDIATE", Child, true);
                Entity.transform.rotation = Quaternion.Euler(0, 45, 0);
                Drain("GRANDPARENT_ROTATION_IMMEDIATE", Child, true);
                Entity.transform.localScale = new Vector3(1.25f, 1.5f, 1.75f);
                Drain("GRANDPARENT_SCALE_IMMEDIATE", Child, true);
                Child.SetParent(null, true, true);
                Parent.SetParent(null, true, true);
                Drain("HISTORY_BASELINE_CLEAR", Entity);
                HistoryBorrowedCalls = BorrowedCalls;
                Entity.transform.position += new Vector3(10, 0, 0);
                HistoryExpectedPosition = Entity.transform.position;
                HistoryStartFrame = Time.frameCount;
                Item Item = ItemManager.CreateByName("wood", 1);
                if (Item == null) throw new InvalidOperationException("physics fixture item unavailable");
                BaseEntity PhysicsEntity = Item.Drop(new Vector3(0, 200, 0), new Vector3(5, 0, 0));
                if (PhysicsEntity == null) { Item.Remove(); throw new InvalidOperationException("natural dropped item unavailable"); }
                Owned.Add(PhysicsEntity);
                PhysicsEntity.enableSaving = false;
                Markers.Add(PhysicsEntity.gameObject.AddComponent<MovementMarker>());
                object PhysicsDispatcher = Activator.CreateInstance(DispatcherType, true);
                try { EnableMethod.Invoke(PhysicsDispatcher, new object[] { TrackingKind, new[] { PhysicsEntity.GetType() } }); }
                catch { ((IDisposable)PhysicsDispatcher).Dispose(); throw; }
                Dispatchers.Add(PhysicsDispatcher);
                TrackedTypes.Add(PhysicsEntity.GetType());
                // Clear only this stream, not the pending BoxStorage history record.
                DrainMethod.Invoke(PhysicsDispatcher, new object[] { PhysicsEntity.GetType(), TrackingKind, false });
                DrainMethod.Invoke(MarkerDispatcher, new object[] { typeof(MovementMarker), TrackingKind, false });
                Vector3 PhysicsBefore = PhysicsEntity.transform.position;
                Passed = true;
                // Linux may not have advanced physics when a timer first fires
                // after startup; require actual displacement, with a bounded wait.
                AwaitPhysics(Entity, PhysicsEntity, PhysicsDispatcher, PhysicsBefore, 0);
            }
            catch (Exception Error) {
                PrintError("[CarbonLuau:WorldMovementResearch] FAIL " + Error.GetType().Name + ": " + Error.Message);
            }
            finally {
                if (!Passed) {
                    Cleanup();
                    timer.Once(1f, () => ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"));
                }
            }
        }

        private void AwaitPhysics(BaseEntity Entity, BaseEntity PhysicsEntity, object PhysicsDispatcher, Vector3 PhysicsBefore, int Attempt)
        {
                timer.Once(0.25f, () => {
                    if (PhysicsEntity != null && PhysicsEntity.transform.position == PhysicsBefore && Attempt < 20) {
                        AwaitPhysics(Entity, PhysicsEntity, PhysicsDispatcher, PhysicsBefore, Attempt + 1);
                        return;
                    }
                    bool DelayedPassed = false;
                    try {
                        if (PhysicsEntity.transform.position == PhysicsBefore)
                            throw new InvalidOperationException("natural physics fixture did not move");
                        Puts("[CarbonLuau:WorldMovementResearch] PHYSICS_DISPLACEMENT value=" + (PhysicsEntity.transform.position - PhysicsBefore) + " rigidbody=" + (PhysicsEntity.GetComponent<Rigidbody>() != null));
                        var PhysicsChanges = (Component[])DrainMethod.Invoke(PhysicsDispatcher, new object[] { PhysicsEntity.GetType(), TrackingKind, false });
                        bool PhysicsFound = false;
                        foreach (Component Change in PhysicsChanges)
                            if (ReferenceEquals(Change, PhysicsEntity)) PhysicsFound = true;
                        Puts("[CarbonLuau:WorldMovementResearch] NATIVE_PHYSICS_LATER count=" + PhysicsChanges.Length + " found=" + PhysicsFound);
                        if (!PhysicsFound) throw new InvalidOperationException("natural physics dispatch missing");
                        var MarkerChanges = (Component[])DrainMethod.Invoke(MarkerDispatcher, new object[] { typeof(MovementMarker), TrackingKind, false });
                        bool MarkerPhysicsFound = false;
                        MovementMarker PhysicsMarker = PhysicsEntity.GetComponent<MovementMarker>();
                        foreach (Component Change in MarkerChanges) if (ReferenceEquals(Change, PhysicsMarker)) MarkerPhysicsFound = true;
                        Puts("[CarbonLuau:WorldMovementResearch] MARKER_PHYSICS count=" + MarkerChanges.Length + " found=" + MarkerPhysicsFound + " trackedRoots=" + Markers.Count);
                        if (!MarkerPhysicsFound) throw new InvalidOperationException("marker stream missed native physics movement");
                        Puts("[CarbonLuau:WorldMovementResearch] HISTORY_DELAY frames=" + (Time.frameCount - HistoryStartFrame) + " expectedPosition=" + HistoryExpectedPosition + " currentPosition=" + Entity.transform.position);
                        Puts("[CarbonLuau:WorldMovementResearch] PRODUCER_CALLBACKS betweenDrains=" + (BorrowedCalls - HistoryBorrowedCalls));
                        if (BorrowedCalls != HistoryBorrowedCalls) throw new InvalidOperationException("unexpected between-drain callback");
                        Drain("UNDRAINED_HISTORY_AFTER_FRAMES", Entity);
                        DelayedPassed = true;
                    }
                    catch (Exception Error) { PrintError("[CarbonLuau:WorldMovementResearch] FAIL delayed dispatch " + Error.GetBaseException().Message); }
                    finally {
                        Cleanup();
                        if (DelayedPassed) Puts("[CarbonLuau:WorldMovementResearch] PASS delayed-movement counterexample and dispatcher probe cleanup");
                        timer.Once(1f, () => ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"));
                    }
                });
        }

        private void Cleanup()
        {
            foreach (object Dispatcher in Dispatchers) ((IDisposable)Dispatcher).Dispose();
            if (BorrowedDispatcher != null) ((IDisposable)BorrowedDispatcher).Dispose();
            if (MarkerDispatcher != null) ((IDisposable)MarkerDispatcher).Dispose();
            MarkerDispatcher = null;
            foreach (MovementMarker Marker in Markers) if (Marker != null) UnityEngine.Object.Destroy(Marker);
            Markers.Clear();
            BorrowedDispatcher = null;
            BorrowedCallback = null;
            Dispatchers.Clear();
            TrackedTypes.Clear();
            foreach (BaseEntity Entity in Owned)
                if (Entity != null && !Entity.IsDestroyed) Entity.Kill();
            Owned.Clear();
        }

        private void Unload() { Cleanup(); }
    }
}
