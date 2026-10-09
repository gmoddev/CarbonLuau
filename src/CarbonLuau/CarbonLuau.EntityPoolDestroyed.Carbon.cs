using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Pool admission can finish an incarnation without invalidating its
        // Unity wrapper. This is separate from D20's unchanged Spawn topology.
        private static readonly MethodInfo PoolDestroyMethod = ResolveGameplayMethod(typeof(PrefabPool), "Push", 0x06008344, "Poolable");
        private static bool PoolDestroyMarkerValid;
        private ConditionalWeakTable<object, DestroyWatch> DestroyPoolObjects = new ConditionalWeakTable<object, DestroyWatch>();
        private ConditionalWeakTable<DestroyWatch, WeakReference> DestroyPoolWatchObjects = new ConditionalWeakTable<DestroyWatch, WeakReference>();
        private readonly List<PoolDestroyFrame> DestroyPoolFrames = new List<PoolDestroyFrame>(DestroyFrameLimit);

        private sealed class PoolDestroyFrame
        {
            internal CarbonLuau Owner;
            internal DestroyWatch Watch;
            internal PrefabPool Pool;
            internal Poolable Poolable;
            internal GameObject Object;
            internal BaseEntity Entity;
            internal bool Exited, Marker, Postfix, Original;
        }

        private static bool PoolDestroyPinned()
        {
            try {
                bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
                MethodInfo Method = PoolDestroyMethod;
                if (Method == null || Method.IsStatic || Method.IsAbstract || Method.ReturnType != typeof(void) ||
                    Method.DeclaringType != typeof(PrefabPool) || Method.MetadataToken != 0x06008344 ||
                    !HasHash(Method.Module.Assembly, Linux ? LinuxRustHash : WindowsRustHash) ||
                    Method.Module.ModuleVersionId.ToString("D") != (Linux ? "616082a0-36f4-4680-a1ab-efc5766796cc" : "c1c11bd1-baa1-4d85-b64e-1d4a4664d62e")) return false;
                ParameterInfo[] Parameters = Method.GetParameters();
                MethodBody Body = Method.GetMethodBody();
                if (Parameters.Length != 1 || Parameters[0].ParameterType != typeof(Poolable) || Body == null ||
                    Body.ExceptionHandlingClauses.Count != 0) return false;
                byte[] Bytes = Body.GetILAsByteArray();
                if (Bytes.Length != 41) return false;
                using (var Hash = SHA256.Create()) return BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant() ==
                    // Exact loaded Carbon-publicized body on both qualified
                    // hosts; backing PE operands are not the loaded IL pin.
                    "cd2bcf544bc4e38c1969ea954f7f00ee3e8d098b3980695a178fa8e40be851d9";
            }
            catch (Exception) { return false; }
        }

        private void ArmEntityPoolWatch(DestroyWatch Watch, GameObject Object)
        {
            if (Watch == null || Watch.Closed || ReferenceEquals(Object, null)) return;
            if (Thread.CurrentThread.ManagedThreadId != DestroyOwnerThread) {
                DestroyOffThread = true; DestroySourceQualified = false; return;
            }
            DestroyWatch Previous;
            if (DestroyPoolObjects.TryGetValue(Object, out Previous)) {
                if (ReferenceEquals(Previous, Watch)) return;
                // A shared GameObject is not authority to select one facade's
                // incarnation over another. Preserve the existing witness.
                RejectEntityDestroyedSource("ambiguous pool GameObject ownership");
                throw new InvalidOperationException("Ambiguous pool GameObject ownership.");
            }
            DestroyPoolObjects.Add(Object, Watch);
            try { DestroyPoolWatchObjects.Add(Watch, new WeakReference(Object)); }
            catch (Exception) { DestroyPoolObjects.Remove(Object); throw; }
        }

        private void ReleaseEntityPoolWatch(DestroyWatch Watch)
        {
            if (Watch == null) return;
            WeakReference Reference;
            if (!DestroyPoolWatchObjects.TryGetValue(Watch, out Reference)) return;
            object Object = Reference.Target;
            DestroyWatch Current;
            if (Object != null && DestroyPoolObjects.TryGetValue(Object, out Current) && ReferenceEquals(Current, Watch))
                DestroyPoolObjects.Remove(Object);
            DestroyPoolWatchObjects.Remove(Watch);
        }

        private void StopEntityPoolWatches()
        {
            DestroyPoolObjects = new ConditionalWeakTable<object, DestroyWatch>();
            DestroyPoolWatchObjects = new ConditionalWeakTable<DestroyWatch, WeakReference>();
            // In-flight frames clear their temporary host references in their
            // own finalizer; stopping must not unbalance shared removal depth.
        }

        private bool PoolDestroyOriginalRemoved(PoolDestroyFrame Frame)
        {
            var Watch = Frame.Watch;
            var Entity = Frame.Entity;
            return Watch != null && !Watch.Closed && !Watch.Certified && Entity != null &&
                Frame.Poolable != null && Frame.Object != null &&
                ReferenceEquals(Frame.Poolable.gameObject, Frame.Object) && ReferenceEquals(Entity.gameObject, Frame.Object) &&
                EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness) && Entity.net == null &&
                !ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(UInt64.Parse(Watch.Id, CultureInfo.InvariantCulture))), Entity);
        }

        private PoolDestroyFrame EnterEntityPoolDestroy(PrefabPool Pool, Poolable Poolable)
        {
            if (Thread.CurrentThread.ManagedThreadId != DestroyOwnerThread) {
                DestroyOffThread = true; DestroySourceQualified = false; return null;
            }
            if (!EntityDestroyedAvailable() || !PoolDestroyMarkerValid || Pool == null || Poolable == null) return null;
            GameObject Object = Poolable.gameObject;
            DestroyWatch Watch;
            if (Object == null || !DestroyPoolObjects.TryGetValue(Object, out Watch) || Watch.Closed || Watch.Certified) return null;
            if (DestroyDepth >= DestroyFrameLimit) {
                DestroyRejected++; RejectEntityDestroyedSource("nested pool removal frame quota; continuity lost"); return null;
            }
            var Frame = new PoolDestroyFrame { Owner = this, Watch = Watch, Pool = Pool, Poolable = Poolable,
                Object = Object, Entity = Watch.Entity.Target as BaseEntity };
            if (!PoolDestroyOriginalRemoved(Frame)) return null;
            DestroyPoolFrames.Add(Frame);
            DestroyDepth++;
            return Frame;
        }

        private static void EntityPoolAdmissionCompleted(PrefabPool Pool, Poolable Poolable)
        {
            var Owner = ActiveEntityObserver;
            if (Owner == null) return;
            if (Thread.CurrentThread.ManagedThreadId != Owner.DestroyOwnerThread) {
                Owner.DestroyOffThread = true; Owner.DestroySourceQualified = false; return;
            }
            for (int Index = Owner.DestroyPoolFrames.Count - 1; Index >= 0; Index--) {
                var Frame = Owner.DestroyPoolFrames[Index];
                if (ReferenceEquals(Frame.Pool, Pool) && ReferenceEquals(Frame.Poolable, Poolable) && !Frame.Exited) {
                    try { Owner.CompleteEntityPoolDestroy(Frame); }
                    catch (Exception) { Owner.DestroyRejected++; Owner.RejectEntityDestroyedSource("pool completion cleanup"); }
                    return;
                }
            }
        }

        private void CompleteEntityPoolDestroy(PoolDestroyFrame Frame)
        {
            if (Frame.Marker) return;
            Frame.Marker = true;
            DestroyWatch Watch = Frame.Watch;
            try {
                if (!EntityDestroyedAvailable() || !PoolDestroyMarkerValid || !PoolDestroyOriginalRemoved(Frame)) return;
                // The pinned Stack.Push has returned. Pool ownership is actual,
                // even if EnterPool next throws, keeps the GameObject active in
                // mode 2, or invokes callbacks which create a new incarnation.
                Watch.Certified = true; DestroyCertified++;
                if (Gameplay != null && Host != null && Host.Ready && Gameplay.GameplayEvents.Capture("entitydestroyed")) {
                    Watch.Published = true;
                    Gameplay.EntityDestroyed(new GameplayEntityDestroyedObservation(Watch.Id, Watch.Prefab, Watch.EpochIdentity, null,
                        () => EntityDestroyedAvailable() && Watch.Certified));
                    RequestDrain();
                }
            }
            catch (Exception) { DestroyRejected++; }
            finally { if (Watch != null && Watch.Certified) ReleaseDestroyWatch(Watch); }
        }

        private void ExitEntityPoolDestroy(PoolDestroyFrame Frame)
        {
            if (Frame == null || Frame.Exited) return;
            Frame.Exited = true;
            DestroyPoolFrames.Remove(Frame); DestroyDepth--;
            Frame.Owner = null; Frame.Watch = null; Frame.Pool = null;
            Frame.Poolable = null; Frame.Object = null; Frame.Entity = null;
        }

        private static bool IsPoolStackPush(CodeInstruction Instruction)
        {
            var Method = Instruction.operand as MethodInfo;
            if (Instruction.opcode != OpCodes.Callvirt || Method == null || Method.DeclaringType != typeof(Stack<Poolable>) ||
                Method.Name != "Push" || Method.ReturnType != typeof(void) || Method.IsStatic) return false;
            ParameterInfo[] Parameters = Method.GetParameters();
            return Parameters.Length == 1 && Parameters[0].ParameterType == typeof(Poolable);
        }

        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class EntityDestroyPoolPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            { return PoolDestroyPinned() ? new MethodBase[] { PoolDestroyMethod } : new MethodBase[0]; }
            [HarmonyPrepare]
            private static bool Prepare() { return PoolDestroyPinned(); }
            [HarmonyTranspiler, HarmonyPriority(Priority.Last)]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions)
            {
                var Source = new List<CodeInstruction>(Instructions);
                int Count = 0;
                foreach (CodeInstruction Instruction in Source) if (IsPoolStackPush(Instruction)) Count++;
                MethodInfo Marker = typeof(CarbonLuau).GetMethod("EntityPoolAdmissionCompleted", BindingFlags.Static | BindingFlags.NonPublic);
                PoolDestroyMarkerValid = PoolDestroyPinned() && Count == 1 && Source.Count <= 64 && Marker != null;
                if (!PoolDestroyMarkerValid) return Source;
                var Result = new List<CodeInstruction>(Source.Count + 3);
                foreach (CodeInstruction Instruction in Source) {
                    Result.Add(Instruction);
                    if (IsPoolStackPush(Instruction)) {
                        Result.Add(new CodeInstruction(OpCodes.Ldarg_0));
                        Result.Add(new CodeInstruction(OpCodes.Ldarg_1));
                        Result.Add(new CodeInstruction(OpCodes.Call, Marker));
                    }
                }
                return Result;
            }
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(PrefabPool __instance, Poolable __0, out PoolDestroyFrame __state)
            {
                __state = null; var Owner = ActiveEntityObserver; if (Owner == null) return;
                try { __state = Owner.EnterEntityPoolDestroy(__instance, __0); }
                catch (Exception) { Owner.DestroyRejected++; Owner.RejectEntityDestroyedSource("pool removal entry"); }
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, PoolDestroyFrame __state)
            { if (__state != null) { __state.Postfix = true; __state.Original = __runOriginal; } }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, PoolDestroyFrame __state)
            {
                if (__state == null || __state.Owner == null) return;
                var Owner = __state.Owner;
                try { Owner.ExitEntityPoolDestroy(__state); }
                catch (Exception) { Owner.DestroyRejected++; Owner.RejectEntityDestroyedSource("pool removal exit"); }
            }
        }
    }
}
