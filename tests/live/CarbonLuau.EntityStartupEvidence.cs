// Research-only startup ordering fixture. Install only on a disposable Carbon server.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    public sealed class CarbonLuauStartupThrowingUpdate : MonoBehaviour, IOnSendNetworkUpdate
    {
        public void OnSendNetworkUpdate(BaseEntity Entity)
        { throw new InvalidOperationException("startup fixture network tail"); }
    }

    public sealed class CarbonLuauStartupThrowingParent : MonoBehaviour, IOnParentSpawning
    {
        public void OnParentSpawning()
        { throw new InvalidOperationException("startup fixture outer tail"); }
    }

    [Info("CarbonLuau.EntityStartupEvidence", "CarbonLuau", "0.1.0")]
    [Description("Records plugin/load/Spawn/world readiness ordering; no public Entity API")]
    public class CarbonLuauEntityStartupEvidence : RustPlugin
    {
        private int SpawnPrefixes;
        private int SpawnedHooks;
        private int FirstSpawnThread;
        private int InitRegistryCount;
        private bool Initialized;
        private static int PatchPrefixes;
        private static int FullCompletions;
        private static int PendingFailures;
        private static int PatchTargets;
        private static int InitializePrefixes;
        private static int BadOuter, BadBase, BadFrame, ExceptionFailures, MissingPostfix, SkippedOriginal;
        private static List<MethodBase> SpawnTargets;
        private const string FixturePrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        [AutoPatch(IsRequired = true), HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.Initialize),
            typeof(bool), typeof(string), typeof(bool), typeof(bool))]
        private static class StartupInitializePatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                InitializePrefixes++;
                UnityEngine.Debug.Log("[CarbonLuau:EntityStartup] INITIALIZE_PREFIX count=" +
                    InitializePrefixes + " patchTargets=" + PatchTargets +
                    " registryCount=" + BaseNetworkable.serverEntities.Count +
                    " thread=" + Thread.CurrentThread.ManagedThreadId);
            }
        }

        private sealed class AttemptRecord
        {
            internal ulong Epoch;
            internal ulong Attempt;
            internal readonly Stack<MethodBase> Methods = new Stack<MethodBase>();
            internal bool Completed;
            internal bool BaseSucceeded;
            internal bool Poisoned;
        }

        private sealed class AttemptFrame
        {
            internal AttemptRecord Record;
            internal ulong Attempt;
            internal bool SawPostfix;
            internal bool RanOriginal;
            internal bool Exited;
            internal MethodBase Method;
        }

        private static readonly ConditionalWeakTable<BaseNetworkable, AttemptRecord> Attempts =
            new ConditionalWeakTable<BaseNetworkable, AttemptRecord>();

        private static bool SameMethod(MethodBase Left, MethodBase Right)
        {
            return Left != null && Right != null && Left.MetadataToken == Right.MetadataToken &&
                Left.Module.ModuleVersionId == Right.Module.ModuleVersionId;
        }

        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class FullSpawnPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var BaseMethod = typeof(BaseNetworkable).GetMethod(nameof(BaseNetworkable.Spawn),
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (BaseMethod == null) throw new InvalidOperationException("Base Spawn unavailable");
                var Targets = new List<MethodBase> { BaseMethod };
                foreach (var Type in typeof(BaseEntity).Assembly.GetTypes())
                {
                    if (!typeof(BaseEntity).IsAssignableFrom(Type)) continue;
                    var Method = Type.GetMethod(nameof(BaseNetworkable.Spawn),
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                        null, Type.EmptyTypes, null);
                    if (Method == null) continue;
                    if (!Method.IsVirtual || Method.IsAbstract || Method.ReturnType != typeof(void) ||
                        Method.GetBaseDefinition().MetadataToken != BaseMethod.MetadataToken)
                        throw new InvalidOperationException("Unexpected Spawn slot: " + Type.FullName);
                    Targets.Add(Method);
                }
                PatchTargets = Targets.Count;
                if (PatchTargets != 24) throw new InvalidOperationException("Spawn override inventory changed");
                SpawnTargets = Targets;
                return Targets;
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(BaseNetworkable __instance, MethodBase __originalMethod,
                out AttemptFrame __state)
            {
                var Record = Attempts.GetValue(__instance, _ => new AttemptRecord());
                if (Record.Methods.Count == 0)
                {
                    Record.Epoch++;
                    Record.Attempt++;
                    Record.Completed = false;
                    Record.BaseSucceeded = false;
                    Record.Poisoned = false;
                    var Outermost = __instance.GetType().GetMethod(nameof(BaseNetworkable.Spawn),
                        BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (!SameMethod(Outermost, __originalMethod))
                    { Record.Poisoned = true; BadOuter++; }
                }
                else
                {
                    var ParentType = Record.Methods.Peek().DeclaringType.BaseType;
                    var ExpectedBase = ParentType == null ? null : ParentType.GetMethod(nameof(BaseNetworkable.Spawn),
                        BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (!SameMethod(ExpectedBase, __originalMethod))
                    { Record.Poisoned = true; BadBase++; }
                }
                Record.Methods.Push(__originalMethod);
                __state = new AttemptFrame { Record = Record, Attempt = Record.Attempt, Method = __originalMethod };
                if (__originalMethod.DeclaringType != typeof(BaseNetworkable)) return;
                if (Interlocked.Increment(ref PatchPrefixes) == 1)
                    UnityEngine.Debug.Log("[CarbonLuau:EntityStartup] FIRST_PATCH_PREFIX type=" +
                        __instance.GetType().FullName + " thread=" + Thread.CurrentThread.ManagedThreadId);
            }

            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, AttemptFrame __state)
            {
                if (__state == null) return;
                __state.SawPostfix = true;
                __state.RanOriginal = __runOriginal;
                if (!__runOriginal) { __state.Record.Poisoned = true; SkippedOriginal++; }
                if (__state.Method.DeclaringType == typeof(BaseNetworkable) && __runOriginal)
                    __state.Record.BaseSucceeded = true;
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, AttemptFrame __state)
            {
                if (__state == null || __state.Exited) return;
                __state.Exited = true;
                var Record = __state.Record;
                if (Record.Attempt != __state.Attempt || Record.Methods.Count == 0 ||
                    !SameMethod(Record.Methods.Peek(), __state.Method))
                {
                    Record.Poisoned = true;
                    BadFrame++;
                    return;
                }
                if (__exception != null || !__state.SawPostfix || !__state.RanOriginal)
                    Record.Poisoned = true;
                if (__exception != null) ExceptionFailures++;
                if (!__state.SawPostfix) MissingPostfix++;
                Record.Methods.Pop();
                if (Record.Methods.Count != 0) return;
                if (!Record.Poisoned && Record.BaseSucceeded)
                {
                    Record.Completed = true;
                    FullCompletions++;
                }
                else PendingFailures++;
            }
        }

        private void Init()
        {
            InitRegistryCount = BaseNetworkable.serverEntities.Count;
            Puts("[CarbonLuau:EntityStartup] INIT thread=" + Thread.CurrentThread.ManagedThreadId);
            Puts("[CarbonLuau:EntityStartup] INIT_REGISTRY count=" + InitRegistryCount);
        }

        private void Loaded()
        {
            Puts("[CarbonLuau:EntityStartup] LOADED thread=" + Thread.CurrentThread.ManagedThreadId +
                " prefixes=" + SpawnPrefixes + " spawned=" + SpawnedHooks + " patchPrefixes=" + PatchPrefixes +
                " registryCount=" + BaseNetworkable.serverEntities.Count);
        }

        private void OnEntitySpawn(BaseNetworkable Entity)
        {
            int Count = ++SpawnPrefixes;
            if (Count != 1) return;
            FirstSpawnThread = Thread.CurrentThread.ManagedThreadId;
            Puts("[CarbonLuau:EntityStartup] FIRST_PREFIX type=" + Entity.GetType().FullName +
                " fully=" + Entity.IsFullySpawned() + " initialized=" + Initialized +
                " thread=" + FirstSpawnThread);
        }

        private void OnEntitySpawned(BaseNetworkable Entity)
        {
            int Count = ++SpawnedHooks;
            if (Count != 1) return;
            Puts("[CarbonLuau:EntityStartup] FIRST_SPAWNED type=" + Entity.GetType().FullName +
                " fully=" + Entity.IsFullySpawned() + " initialized=" + Initialized +
                " thread=" + Thread.CurrentThread.ManagedThreadId);
        }

        private void OnServerInitialized()
        {
            Initialized = true;
            if (InitializePrefixes == 0) {
                Puts("[CarbonLuau:EntityStartup] HOTLOAD_UNQUALIFIED initializePrefix=missing");
                return;
            }
            Puts("[CarbonLuau:EntityStartup] SERVER_INITIALIZED prefixes=" + SpawnPrefixes +
                " spawned=" + SpawnedHooks + " patchPrefixes=" + PatchPrefixes +
                " fullCompletions=" + FullCompletions + " pendingFailures=" + PendingFailures +
                " patchTargets=" + PatchTargets + " initializePrefixes=" + InitializePrefixes +
                " badOuter=" + BadOuter + " badBase=" + BadBase + " badFrame=" + BadFrame +
                " exceptions=" + ExceptionFailures + " missingPostfix=" + MissingPostfix +
                " skippedOriginal=" + SkippedOriginal +
                " thread=" + Thread.CurrentThread.ManagedThreadId +
                " firstSpawnThread=" + FirstSpawnThread);
            if (SpawnTargets != null) foreach (MethodBase Target in SpawnTargets)
                Puts("[CarbonLuau:EntityStartup] PATCH_TOPOLOGY " + DescribePatches(Target));
            NextTick(() => {
                try {
                    if (PatchPrefixes != SpawnPrefixes || SpawnPrefixes == 0 || InitializePrefixes != 1 ||
                        InitRegistryCount != 0)
                        throw new InvalidOperationException("patch/hook count mismatch");
                    SaveRestore.Save(AndWait: true);
                    ProbeFullSpawn();
                    Puts("[CarbonLuau:EntityStartup] PASS postInitPrefixes=" + SpawnPrefixes +
                        " patchPrefixes=" + PatchPrefixes + " save=complete");
                } catch (Exception Error) {
                    PrintError("[CarbonLuau:EntityStartup] SAVE_FAILED " + Error.GetType().Name);
                }
            });
        }

        private static string DescribePatches(MethodBase Target)
        {
            var Info = Harmony.GetPatchInfo(Target);
            if (Info == null) return Target.DeclaringType.FullName + ".Spawn=missing";
            var Result = new StringBuilder(Target.DeclaringType.FullName + ".Spawn");
            foreach (var Patch in Info.Prefixes)
                Result.Append(" prefix:").Append(Patch.PatchMethod.DeclaringType.FullName).Append(".").Append(Patch.PatchMethod.Name);
            foreach (var Patch in Info.Postfixes)
                Result.Append(" postfix:").Append(Patch.PatchMethod.DeclaringType.FullName).Append(".").Append(Patch.PatchMethod.Name);
            foreach (var Patch in Info.Finalizers)
                Result.Append(" finalizer:").Append(Patch.PatchMethod.DeclaringType.FullName).Append(".").Append(Patch.PatchMethod.Name);
            foreach (var Patch in Info.Transpilers)
                Result.Append(" transpiler:").Append(Patch.PatchMethod.DeclaringType.FullName).Append(".").Append(Patch.PatchMethod.Name);
            return Result.ToString();
        }

        private void ProbeFullSpawn()
        {
            ProbeCase("normal", null, false);
            ProbeCase("network-tail", Entity => Entity.gameObject.AddComponent<CarbonLuauStartupThrowingUpdate>(), true);
            ProbeCase("outer-tail", Entity => Entity.gameObject.AddComponent<CarbonLuauStartupThrowingParent>(), true);
        }

        private void ProbeCase(string Label, Action<BaseEntity> Prepare, bool ExpectThrow)
        {
            BaseEntity Entity = GameManager.server.CreateEntity(FixturePrefab, new Vector3(0, 100, 0));
            if (Entity == null) throw new InvalidOperationException("fixture entity unavailable");
            Entity.enableSaving = false;
            try {
                Prepare?.Invoke(Entity);
                bool Threw = false;
                try { Entity.Spawn(); }
                catch (InvalidOperationException) { Threw = true; }
                AttemptRecord Record;
                if (!Attempts.TryGetValue(Entity, out Record))
                    throw new InvalidOperationException("fixture Spawn missed observer");
                if (Threw != ExpectThrow || Record.Completed == ExpectThrow || Record.Methods.Count != 0)
                    throw new InvalidOperationException("full Spawn completion mismatch: " + Label);
                Puts("[CarbonLuau:EntityStartup] CASE " + Label + " threw=" + Threw +
                    " completed=" + Record.Completed + " epoch=" + Record.Epoch +
                    " keyed=" + (Entity.net != null &&
                        ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity)));
                if (ExpectThrow)
                {
                    var FailingComponent = Label == "network-tail"
                        ? (Component)Entity.gameObject.GetComponent<CarbonLuauStartupThrowingUpdate>()
                        : Entity.gameObject.GetComponent<CarbonLuauStartupThrowingParent>();
                    if (FailingComponent == null) throw new InvalidOperationException("fixture failure component missing");
                    UnityEngine.Object.DestroyImmediate(FailingComponent);
                    bool RetryThrew = false;
                    string RetryError = "none";
                    try { Entity.Spawn(); }
                    catch (Exception Error) { RetryThrew = true; RetryError = Error.GetType().Name; }
                    if (Record.Epoch != 2 || Record.Completed == RetryThrew || Record.Methods.Count != 0)
                        throw new InvalidOperationException("same-object retry completion mismatch: " + Label);
                    Puts("[CarbonLuau:EntityStartup] RETRY " + Label + " threw=" + RetryThrew +
                        " error=" + RetryError + " completed=" + Record.Completed +
                        " epoch=" + Record.Epoch + " keyed=" + (Entity.net != null &&
                            ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity)));
                }
            } finally {
                if (Entity != null && !Entity.IsDestroyed) Entity.Kill();
            }
        }
    }
}
