using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // These are separate tested binary tuples. Carbon's generated transpiler
        // has equivalent inspected IL, but its type identity differs by platform.
        private const string LinuxRustHash = "cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450";
        private const string LinuxCarbonHash = "44d0e88a7c8c9c45f8a94a67896cb7a8da4f0f4ac73e4bdc85fba1d40f5584f4";
        private const string LinuxCarbonMvid = "8fddac27-e7ed-48b4-929b-27c2c6809ce9";
        private const string LinuxCarbonCommonHash = "5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb";
        private const string LinuxCarbonCommonMvid = "a232bf18-5bfc-442d-8a2a-8fad03f12e20";
        private const string LinuxHooksHash = "b065731b4a06f47a2459d3a64995a4819edc720b40c665de794f17ffe39d2764";
        private const string LinuxCommunityHash = "4de8c464f4a5d18086ea24100b3e26680ac35ee8bfa23d22b27a47d62da5f343";
        private const string LinuxHarmonyHash = "77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d";
        private const string WindowsRustHash = "bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2";
        private const string WindowsCarbonHash = "b8873f74343de09c49ea285aa981b1b1e3a53d7f193a7d6c38b3de4bd467abe4";
        private const string WindowsCarbonMvid = "f2192753-db0e-4160-8929-f27aaf70cb33";
        private const string WindowsCarbonCommonHash = "0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a";
        private const string WindowsCarbonCommonMvid = "fdebe341-e8d3-4804-a523-7d88f1a05347";
        private const string WindowsHooksHash = "71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d";
        private const string WindowsUpdatedHooksHash = "0b668e1f4819ef3455c70781428c0afdd02601d2398eef71bd28428a8a073afa";
        private const string WindowsCommunityHash = "4364a8782fd012dd8cfa8acf1d778c1e6bee9bf6bbe0efb801deba685778f50e";
        private const string WindowsOldCommunityHash = "1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3";
        private const string WindowsHarmonyHash = "77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d";
        private const string CarbonPrefixType = "Carbon.Hooks.Category_Entity+Entity_BaseNetworkable+OnEntitySpawn";
        private const string LinuxTranspilerType = "Carbon.Hooks.Category_Entity+Entity_BaseNetworkable+Entity_BaseNetworkable_7cc7a8d1f2b14faf96c00d917542524f";
        private const string WindowsTranspilerType = "Carbon.Hooks.Category_Entity+Entity_BaseNetworkable+Entity_BaseNetworkable_3dfbe84a8fb1482ea238d43e7f12aece";
        private const string EntityProcessObserverKey = "CarbonLuau.EntityLifetime.ObserverInstalled.v1";

        private static readonly HashSet<string> SpawnTypeNames = new HashSet<string>(StringComparer.Ordinal) {
            "BaseNetworkable", "BaseEntity", "BaseVehicle", "CargoShip", "ClanManager", "Drone",
            "ExcavatorArm", "IOEntity", "JunkPile", "Lift", "PoweredRemoteControlEntity",
            "ProceduralLift", "SlotMachine", "TrainCar", "VehicleModuleCamper",
            "VehicleModuleStorage", "ElevatorStatic", "DiveSite", "JunkPileWater",
            "Marketplace", "HalloweenDungeon", "TrainCarUnloadableLoot", "ItemPickup",
            "SlidingProgressDoor"
        };

        private static CarbonLuau ActiveEntityObserver;
        private static Assembly PinnedEntityHooksAssembly;
        private static Assembly PinnedEntityCommunityAssembly;
        private static string PinnedEntityHooksHash, PinnedEntityHooksMvid, PinnedEntityHooksPath, PinnedEntityTranspilerType;
        private static string PinnedEntityCommunityHash, PinnedEntityCommunityMvid, PinnedEntityCommunityPath;
        private static MethodBase[] EntitySpawnTargets;
        private static bool EntityTargetInventoryFailed;
        private static string EntityTopologyFailure;
        private EntityLifetimeModel EntityLifetimes;
        // Harmony 2.4.2's pinned shared state replaces each serialized patch
        // record on update. Startup validates the full topology; an operation
        // compares these exact record objects under Harmony's own dictionary lock.
        private IDictionary EntityReadPatchState;
        private FieldInfo EntityReadPatchStateField;
        private Dictionary<MethodBase, byte[]> EntityReadPatchStamps;
        private readonly ConditionalWeakTable<BaseNetworkable, EntitySpawnChain> EntitySpawnChains =
            new ConditionalWeakTable<BaseNetworkable, EntitySpawnChain>();
        private int EntityOwnerThread;
        private volatile bool EntityObserverBroken;
        private bool EntityInitializeSeen, EntityStartupQualified, EntityHostPinned;
        private string EntityObserverFailure;

        private sealed class EntitySpawnChain
        {
            internal readonly Stack<MethodBase> Methods = new Stack<MethodBase>();
            internal EntityLifetimeModel.SpawnAttempt Attempt;
            internal bool BaseReturned, Poisoned;
        }

        private sealed class EntitySpawnFrame
        {
            internal EntitySpawnChain Chain;
            internal MethodBase Method;
            internal bool PostfixSeen, OriginalRan, Exited;
        }

        private void InitializeEntityObserver()
        {
            try {
                EntityOwnerThread = Thread.CurrentThread.ManagedThreadId;
                EntityLifetimes = new EntityLifetimeModel(EntityDiscoveryPolicy.CatalogSlots);
                if (AppDomain.CurrentDomain.GetData(EntityProcessObserverKey) != null) {
                    BreakEntityObserver("same-process observer reload or gap; server restart required");
                    return;
                }
                AppDomain.CurrentDomain.SetData(EntityProcessObserverKey, new object());
                if (ActiveEntityObserver != null) {
                    ActiveEntityObserver.BreakEntityObserver("overlapping plugin instance");
                    BreakEntityObserver("overlapping plugin instance");
                    return;
                }
                ActiveEntityObserver = this;
                string HostFailure;
                EntityHostPinned = VerifyEntityHostPins(out HostFailure);
                InitializeEntityDestroyedSource();
                if (!EntityHostPinned) BreakEntityObserver("host pin mismatch: " + HostFailure);
            }
            catch (Exception) { BreakEntityObserver("observer initialization failed"); }
        }

        private static bool SameEntityMethod(MethodBase Left, MethodBase Right)
        {
            return Left != null && Right != null && Left.MetadataToken == Right.MetadataToken &&
                Left.Module.ModuleVersionId == Right.Module.ModuleVersionId;
        }

        private static bool HasHash(Assembly Assembly, string Expected)
        {
            return Assembly != null && HasFileHash(Assembly.Location, Expected);
        }

        private static bool HasFileHash(string PathValue, string Expected)
        {
            if (String.IsNullOrEmpty(PathValue) || !File.Exists(PathValue)) return false;
            using (var Stream = File.OpenRead(PathValue))
            using (var Hash = SHA256.Create())
                return String.Equals(BitConverter.ToString(Hash.ComputeHash(Stream)).Replace("-", ""),
                    Expected, StringComparison.OrdinalIgnoreCase);
        }

        private static Assembly LoadedEntityAssembly(string Name)
        {
            foreach (Assembly Assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (String.Equals(Assembly.GetName().Name, Name, StringComparison.Ordinal)) return Assembly;
            return null;
        }

        private static bool VerifyEntityHostPins(out string Failure)
        {
            Failure = "inspection unavailable";
            try {
                Assembly Rust = typeof(BaseNetworkable).Assembly;
                Assembly Carbon = LoadedEntityAssembly("Carbon");
                Assembly CarbonCommon = typeof(CarbonPlugin).Assembly;
                Assembly HarmonyAssembly = typeof(Harmony).Assembly;
                bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
                bool Windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                if (!Linux && !Windows) { Failure = "platform"; return false; }
                if (!HasHash(Rust, Linux ? LinuxRustHash : WindowsRustHash) ||
                    Rust.ManifestModule.ModuleVersionId.ToString("D") !=
                    (Linux ? "616082a0-36f4-4680-a1ab-efc5766796cc" : "c1c11bd1-baa1-4d85-b64e-1d4a4664d62e")) {
                    Failure = "Rust"; return false;
                }
                DirectoryInfo Managed = new DirectoryInfo(Path.GetDirectoryName(Rust.Location));
                if (Managed.Name != "Managed" || Managed.Parent == null ||
                    Managed.Parent.Name != "RustDedicated_Data" || Managed.Parent.Parent == null) {
                    Failure = "Rust server root"; return false;
                }
                string CarbonManaged = Path.Combine(Managed.Parent.Parent.FullName, "carbon", "managed");
                PinnedEntityHooksPath = Path.Combine(CarbonManaged, "hooks", "Carbon.Hooks.Oxide.dll");
                PinnedEntityCommunityPath = Path.Combine(CarbonManaged, "hooks", "Carbon.Hooks.Community.dll");
                string CarbonCommonPath = Path.Combine(CarbonManaged, "Carbon.Common.dll");
                string ExpectedCommonHash = Linux ? LinuxCarbonCommonHash : WindowsCarbonCommonHash;
                if (!HasFileHash(CarbonCommonPath, ExpectedCommonHash) ||
                    CarbonCommon.GetName().Version.ToString() != (Linux ? "2.0.261.0" : "2.0.262.0") ||
                    CarbonCommon.ManifestModule.ModuleVersionId.ToString("D") !=
                        (Linux ? LinuxCarbonCommonMvid : WindowsCarbonCommonMvid) ||
                    (!String.IsNullOrEmpty(CarbonCommon.Location) && !HasHash(CarbonCommon, ExpectedCommonHash))) {
                    Failure = "Carbon.Common"; return false;
                }
                string CarbonPath = Path.Combine(CarbonManaged, "Carbon.dll");
                string ExpectedCarbonHash = Linux ? LinuxCarbonHash : WindowsCarbonHash;
                if (!HasFileHash(CarbonPath, ExpectedCarbonHash)) {
                    Failure = "Carbon.dll on disk"; return false;
                }
                if (Carbon == null ||
                    Carbon.GetName().Version.ToString() != (Linux ? "2.0.261.0" : "2.0.262.0") ||
                    Carbon.ManifestModule.ModuleVersionId.ToString("D") !=
                        (Linux ? LinuxCarbonMvid : WindowsCarbonMvid) ||
                    (!String.IsNullOrEmpty(Carbon.Location) && !HasHash(Carbon, ExpectedCarbonHash))) {
                    Failure = "loaded Carbon identity"; return false;
                }
                string ExpectedHarmonyHash = Linux ? LinuxHarmonyHash : WindowsHarmonyHash;
                if (!HasFileHash(Path.Combine(CarbonManaged, "lib", "0Harmony.dll"), ExpectedHarmonyHash) ||
                    (!String.IsNullOrEmpty(HarmonyAssembly.Location) && !HasHash(HarmonyAssembly, ExpectedHarmonyHash)) ||
                    HarmonyAssembly.GetName().Version.ToString() != "2.4.2.0" ||
                    HarmonyAssembly.ManifestModule.ModuleVersionId.ToString("D") !=
                    "b9e6cf65-9433-482b-8860-83cff28d0128") {
                    Failure = "Harmony"; return false;
                }
                if (Linux) {
                    PinnedEntityHooksHash = LinuxHooksHash;
                    PinnedEntityHooksMvid = "24c572a8-968b-4048-94a7-40ae119d0b8c";
                    PinnedEntityTranspilerType = LinuxTranspilerType;
                    PinnedEntityCommunityHash = LinuxCommunityHash;
                    PinnedEntityCommunityMvid = "43e30681-4802-43e3-a6a3-ab5c32de6a94";
                }
                else {
                    bool UpdatedHooks = HasFileHash(PinnedEntityHooksPath, WindowsUpdatedHooksHash);
                    PinnedEntityHooksHash = UpdatedHooks ? WindowsUpdatedHooksHash : WindowsHooksHash;
                    PinnedEntityHooksMvid = UpdatedHooks ? "eaf4dad6-6dd5-4f38-a5af-d61fab1780bc" :
                        "25387c43-282d-4de5-9dcf-2aea220206a2";
                    PinnedEntityTranspilerType = UpdatedHooks ? WindowsTranspilerType : LinuxTranspilerType;
                    bool UpdatedCommunity = HasFileHash(PinnedEntityCommunityPath, WindowsCommunityHash);
                    if (UpdatedHooks != UpdatedCommunity) {
                        Failure = "mixed Windows Carbon hook components"; return false;
                    }
                    PinnedEntityCommunityHash = UpdatedCommunity ? WindowsCommunityHash : WindowsOldCommunityHash;
                    PinnedEntityCommunityMvid = UpdatedCommunity ? "e7a46446-eb51-426d-908e-59aa9cdb4677" :
                        "1cdb2195-1e83-452b-8d85-05c73a5d50e2";
                }
                if (!HasFileHash(PinnedEntityHooksPath, PinnedEntityHooksHash)) {
                    Failure = "Carbon.Hooks.Oxide on disk"; return false;
                }
                if (!HasFileHash(PinnedEntityCommunityPath, PinnedEntityCommunityHash)) {
                    Failure = "Carbon.Hooks.Community on disk"; return false;
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        private static MethodBase[] ResolveEntitySpawnTargets()
        {
            var Targets = new List<MethodBase>();
            var Names = new HashSet<string>(StringComparer.Ordinal);
            MethodInfo BaseMethod = typeof(BaseNetworkable).GetMethod(nameof(BaseNetworkable.Spawn),
                BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (BaseMethod == null || BaseMethod.IsAbstract || BaseMethod.ReturnType != typeof(void)) return null;
            Targets.Add(BaseMethod);
            Names.Add(typeof(BaseNetworkable).FullName);
            foreach (Type Type in typeof(BaseEntity).Assembly.GetTypes()) {
                if (!typeof(BaseEntity).IsAssignableFrom(Type)) continue;
                MethodInfo Method = Type.GetMethod(nameof(BaseNetworkable.Spawn),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
                if (Method == null) continue;
                if (!Method.IsVirtual || Method.IsAbstract || Method.ReturnType != typeof(void) ||
                    !SameEntityMethod(Method.GetBaseDefinition(), BaseMethod) ||
                    !Names.Add(Type.FullName)) return null;
                Targets.Add(Method);
            }
            if (Targets.Count != 24 || !Names.SetEquals(SpawnTypeNames)) return null;
            return Targets.ToArray();
        }

        private static bool IsOwnEntityPatch(HarmonyLib.Patch Patch, string Name)
        {
            MethodInfo Expected = typeof(EntityFullSpawnPatch).GetMethod(Name, BindingFlags.Static | BindingFlags.NonPublic);
            return Patch != null && Patch.PatchMethod != null && Expected != null &&
                ReferenceEquals(Patch.PatchMethod.Module, Expected.Module) &&
                SameEntityMethod(Patch.PatchMethod, Expected);
        }

        private static bool IsPinnedCarbonPatch(HarmonyLib.Patch Patch, string TypeName, string MethodName)
        {
            MethodInfo Method = Patch == null ? null : Patch.PatchMethod;
            if (Method == null || Method.DeclaringType == null) return false;
            if (!String.Equals(Method.DeclaringType.FullName, TypeName, StringComparison.Ordinal) ||
                !String.Equals(Method.Name, MethodName, StringComparison.Ordinal)) return false;
            bool Prefix = TypeName == CarbonPrefixType && MethodName == "Prefix";
            bool Transpiler = TypeName == PinnedEntityTranspilerType && MethodName == "Transpiler";
            if (!Prefix && !Transpiler) return false;
            Assembly Hooks = Method.DeclaringType.Assembly;
            string Name = Prefix ? "Carbon.Hooks.Community" : "Carbon.Hooks.Oxide";
            string PathValue = Prefix ? PinnedEntityCommunityPath : PinnedEntityHooksPath;
            string Mvid = Prefix ? PinnedEntityCommunityMvid : PinnedEntityHooksMvid;
            string Hash = Prefix ? PinnedEntityCommunityHash : PinnedEntityHooksHash;
            Assembly Pinned = Prefix ? PinnedEntityCommunityAssembly : PinnedEntityHooksAssembly;
            if (Pinned == null) {
                if (PathValue == null ||
                    Hooks.GetName().Name != Name ||
                    Hooks.GetName().Version.ToString() != "2.0.0.0" ||
                    Hooks.ManifestModule.ModuleVersionId.ToString("D") != Mvid ||
                    (!String.IsNullOrEmpty(Hooks.Location) && !HasHash(Hooks, Hash))) return false;
                if (Prefix) PinnedEntityCommunityAssembly = Hooks;
                else PinnedEntityHooksAssembly = Hooks;
                Pinned = Hooks;
            }
            return ReferenceEquals(Hooks, Pinned);
        }

        private static bool RejectEntityTopology(MethodBase Target, string Detail)
        {
            EntityTopologyFailure = (Target == null ? "missing target" : Target.DeclaringType.FullName + "." + Target.Name) +
                " " + Detail;
            return false;
        }

        private static string EntityPatchName(HarmonyLib.Patch Patch)
        {
            MethodInfo Method = Patch == null ? null : Patch.PatchMethod;
            return Method == null || Method.DeclaringType == null ? "missing" :
                Method.DeclaringType.FullName + "." + Method.Name + " priority=" + Patch.priority +
                " assembly=" + Method.Module.Assembly.GetName().FullName +
                " mvid=" + Method.Module.ModuleVersionId.ToString("D");
        }

        private static bool VerifyEntityPatchTopology(MethodBase Target)
        {
            if (Target == null) return RejectEntityTopology(Target, "unresolved");
            HarmonyLib.Patches Info = Harmony.GetPatchInfo(Target);
            if (Info == null) return RejectEntityTopology(Target, "unpatched");
            bool Base = Target.DeclaringType == typeof(BaseNetworkable);
            int OwnPrefixes = 0, CarbonPrefixes = 0, OwnPostfixes = 0;
            int OwnFinalizers = 0, CarbonTranspilers = 0;
            foreach (HarmonyLib.Patch Patch in Info.Prefixes) {
                if (IsOwnEntityPatch(Patch, "Prefix")) { OwnPrefixes++; if (Patch.priority != Priority.First) return RejectEntityTopology(Target, "own prefix priority"); }
                else if (Base && IsPinnedCarbonPatch(Patch, CarbonPrefixType, "Prefix") &&
                    Patch.priority == 700) CarbonPrefixes++;
                else return RejectEntityTopology(Target, "prefix " + EntityPatchName(Patch));
            }
            foreach (HarmonyLib.Patch Patch in Info.Postfixes) {
                if (IsOwnEntityPatch(Patch, "Postfix")) { OwnPostfixes++; if (Patch.priority != Priority.Last) return RejectEntityTopology(Target, "own postfix priority"); }
                else return RejectEntityTopology(Target, "postfix " + EntityPatchName(Patch));
            }
            foreach (HarmonyLib.Patch Patch in Info.Finalizers) {
                if (IsOwnEntityPatch(Patch, "Finalizer")) { OwnFinalizers++; if (Patch.priority != Priority.Last) return RejectEntityTopology(Target, "own finalizer priority"); }
                else return RejectEntityTopology(Target, "finalizer " + EntityPatchName(Patch));
            }
            foreach (HarmonyLib.Patch Patch in Info.Transpilers) {
                if (Base && IsPinnedCarbonPatch(Patch, PinnedEntityTranspilerType, "Transpiler") &&
                    Patch.priority == 700) CarbonTranspilers++;
                else return RejectEntityTopology(Target, "transpiler " + EntityPatchName(Patch));
            }
            bool CarbonPair = Base ? (CarbonPrefixes == 0 && CarbonTranspilers == 0) ||
                (CarbonPrefixes == 1 && CarbonTranspilers == 1) :
                CarbonPrefixes == 0 && CarbonTranspilers == 0;
            if (OwnPrefixes != 1 || OwnPostfixes != 1 || OwnFinalizers != 1 || !CarbonPair)
                return RejectEntityTopology(Target, "patch count mismatch own=" + OwnPrefixes + "/" +
                    OwnPostfixes + "/" + OwnFinalizers + " carbon=" + CarbonPrefixes + "/" +
                    CarbonTranspilers);
            return true;
        }

        private bool VerifyAllEntityPatches()
        {
            EntityTopologyFailure = null;
            try {
                if (!EntityHostPinned || EntityTargetInventoryFailed || EntitySpawnTargets == null ||
                    EntitySpawnTargets.Length != 24) return RejectEntityTopology(null, "host or 24-target inventory");
                if (!HasFileHash(PinnedEntityHooksPath, PinnedEntityHooksHash) ||
                    !HasFileHash(PinnedEntityCommunityPath, PinnedEntityCommunityHash))
                    return RejectEntityTopology(null, "installed Carbon hooks changed");
                foreach (MethodBase Target in EntitySpawnTargets)
                    if (!VerifyEntityPatchTopology(Target)) return false;
                MethodInfo Initialize = typeof(ServerMgr).GetMethod(nameof(ServerMgr.Initialize),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(bool), typeof(string), typeof(bool), typeof(bool) }, null);
                HarmonyLib.Patches Info = Initialize == null ? null : Harmony.GetPatchInfo(Initialize);
                if (Info == null) return RejectEntityTopology(Initialize, "unpatched initialize");
                MethodInfo OwnInitialize = typeof(EntityInitializePatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (Info.Prefixes.Count != 1 || Info.Postfixes.Count != 0 ||
                    Info.Finalizers.Count != 0 || Info.Transpilers.Count != 0)
                    return RejectEntityTopology(Initialize, "initialize patch counts " +
                        Info.Prefixes.Count + "/" + Info.Postfixes.Count + "/" +
                        Info.Finalizers.Count + "/" + Info.Transpilers.Count);
                if (Info.Prefixes[0].PatchMethod == null || OwnInitialize == null ||
                    !ReferenceEquals(Info.Prefixes[0].PatchMethod.Module, OwnInitialize.Module) ||
                    !SameEntityMethod(Info.Prefixes[0].PatchMethod, OwnInitialize))
                    return RejectEntityTopology(Initialize, "prefix " + EntityPatchName(Info.Prefixes[0]));
                return true;
            }
            catch (Exception) { EntityTopologyFailure = "patch inspection exception"; return false; }
        }

        private bool CaptureEntityReadPatchStamps()
        {
            try {
                Type SharedState = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState", false);
                FieldInfo Field = SharedState == null ? null : SharedState.GetField("state",
                    BindingFlags.Static | BindingFlags.NonPublic);
                IDictionary State = Field == null ? null : Field.GetValue(null) as IDictionary;
                if (State == null || EntitySpawnTargets == null) return false;
                var Stamps = new Dictionary<MethodBase, byte[]>();
                lock (State) {
                    foreach (MethodBase Target in EntitySpawnTargets) {
                        byte[] Stamp = State[Target] as byte[];
                        if (Stamp == null || Stamp.Length == 0) return false;
                        Stamps.Add(Target, Stamp);
                    }
                }
                EntityReadPatchStateField = Field;
                EntityReadPatchState = State;
                EntityReadPatchStamps = Stamps;
                return true;
            }
            catch (Exception) { return false; }
        }

        private bool EntityReadPatchPathCurrent(BaseEntity Entity)
        {
            if (Entity == null || EntityReadPatchState == null || EntityReadPatchStateField == null ||
                EntityReadPatchStamps == null || EntitySpawnTargets == null) return false;
            Type RuntimeType = Entity.GetType();
            if (RuntimeType.Assembly != typeof(BaseEntity).Assembly) return false;
            MethodInfo Effective = RuntimeType.GetMethod(nameof(BaseNetworkable.Spawn),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            bool EffectiveCovered = false;
            int PathCount = 0;
            IDictionary State = EntityReadPatchState;
            lock (State) {
                if (!ReferenceEquals(EntityReadPatchStateField.GetValue(null), State)) return false;
                foreach (MethodBase Target in EntitySpawnTargets) {
                    if (!Target.DeclaringType.IsAssignableFrom(RuntimeType)) continue;
                    PathCount++;
                    if (SameEntityMethod(Target, Effective)) EffectiveCovered = true;
                    byte[] Expected;
                    if (!EntityReadPatchStamps.TryGetValue(Target, out Expected) ||
                        !ReferenceEquals(State[Target], Expected)) return false;
                }
            }
            return EffectiveCovered && PathCount >= 2;
        }

        private void BreakEntityObserver(string Reason)
        {
            EntityObserverBroken = true;
            EntityStartupQualified = false;
            if (EntityObserverFailure == null) {
                EntityObserverFailure = Reason;
                try { Puts("[CarbonLuau:EntityLifetime] Observer unavailable: " + Reason); }
                catch (Exception) { }
            }
            if (EntityLifetimes != null && Thread.CurrentThread.ManagedThreadId == EntityOwnerThread) {
                try { EntityLifetimes.BreakObserverContinuity(); }
                catch (Exception) { }
            }
        }

        private void OnEntityInitializePrefix()
        {
            try {
                if (EntityObserverBroken || EntityLifetimes == null || EntityInitializeSeen ||
                    Thread.CurrentThread.ManagedThreadId != EntityOwnerThread ||
                    BaseNetworkable.serverEntities.Count != 0 || !VerifyAllEntityPatches()) {
                    BreakEntityObserver("startup or patch topology not qualified: " + EntityTopologyFailure);
                    return;
                }
                EntityInitializeSeen = true;
                EntityLifetimes.BeginQualifiedObservation();
            }
            catch (Exception) { BreakEntityObserver("initialize-prefix inspection failed"); }
        }

        private void QualifyEntityStartup()
        {
            try {
                if (EntityObserverBroken) return;
                if (!EntityInitializeSeen) {
                    BreakEntityObserver("observer was not installed before world startup; server restart required");
                    return;
                }
                int Reconciled;
                BaseEntity ReadWarmupCandidate;
                if (!VerifyAllEntityPatches() ||
                    !ReconcileEntityStartupRegistry(out Reconciled, out ReadWarmupCandidate) ||
                    !CaptureEntityReadPatchStamps() ||
                    !EntityLifetimes.QualifyStartupCompletion()) {
                    BreakEntityObserver("startup completion, registry, or patch topology not qualified: " + EntityTopologyFailure);
                    return;
                }
                EntityStartupQualified = true;
                InitializeEntityDiscovery();
                QualifyEntityDestroyedSource();
                if (ReadWarmupCandidate != null && Native != null && Gameplay != null)
                    WarmEntityReadPath(ReadWarmupCandidate);
                if (EntityObserverBroken) return;
                Puts("[CarbonLuau:EntityLifetime] Private startup observer qualified for pinned host; keyed completions=" + Reconciled);
            }
            catch (Exception) { BreakEntityObserver("startup qualification failed"); }
        }

        // A bounded one-time check at the world-load boundary, never a per-Spawn
        // or World-enumeration scan. An unobserved current keyed entity poisons
        // the entire startup window rather than becoming an implicit baseline.
        private bool ReconcileEntityStartupRegistry(out int Reconciled, out BaseEntity ReadWarmupCandidate)
        {
            const int RegistryLimit = 262144;
            Reconciled = 0;
            ReadWarmupCandidate = null;
            if (Thread.CurrentThread.ManagedThreadId != EntityOwnerThread ||
                BaseNetworkable.serverEntities == null ||
                BaseNetworkable.serverEntities.Count > RegistryLimit)
                return RejectEntityTopology(null, "startup registry unavailable or over cap");
            int Enumerated = 0;
            foreach (object Candidate in BaseNetworkable.serverEntities) {
                if (++Enumerated > RegistryLimit)
                    return RejectEntityTopology(null, "startup registry iteration cap");
                BaseEntity Entity = Candidate as BaseEntity;
                if (Entity == null || Entity.net == null || Entity.net.ID.Value == 0 ||
                    !Entity.IsFullySpawned() ||
                    !ReferenceEquals(BaseNetworkable.serverEntities.Find(Entity.net.ID), Entity)) continue;
                Reconciled++;
                EntitySpawnChain Chain;
                if (!EntitySpawnChains.TryGetValue(Entity, out Chain) ||
                    Chain.Methods.Count != 0 || Chain.Attempt != null || Chain.Poisoned ||
                    !EntityLifetimes.HasCompletedObservation(Entity))
                    return RejectEntityTopology(null, "current keyed entity lacks full observed completion");
                // Membership comes from full Spawn completion, not proxy/token
                // acquisition. Missing enrollment closes discovery only.
                if (!EntityLifetimes.HasCatalogObservation(Entity)) EntityLifetimes.InvalidateCatalog();
                if (ReadWarmupCandidate == null && !Entity.IsDestroyed &&
                    !String.IsNullOrEmpty(Entity.PrefabName) &&
                    Entity.PrefabName.IndexOf('\0') < 0) {
                    try {
                        if (new System.Text.UTF8Encoding(false, true).GetByteCount(Entity.PrefabName) <= 512)
                            ReadWarmupCandidate = Entity;
                    }
                    catch (System.Text.EncoderFallbackException) { }
                }
            }
            if (Enumerated != BaseNetworkable.serverEntities.Count)
                return RejectEntityTopology(null, "startup registry changed during reconciliation");
            return true;
        }

        private void StopEntityObserver()
        {
            StopEntityDiscovery();
            BreakEntityObserver("plugin unload or observer gap");
            if (ReferenceEquals(ActiveEntityObserver, this)) ActiveEntityObserver = null;
            if (EntityLifetimes != null && Thread.CurrentThread.ManagedThreadId == EntityOwnerThread)
                try { EntityLifetimes.Dispose(); } catch (Exception) { }
        }

        // Private admission seam for Entity-1B. No Luau object or service is published here.
        private bool TryAdmitEntity(BaseEntity Entity, EntityLifetimeModel.Authority Authority,
            Func<EntityLifetimeModel.Authority, bool> IsCurrent, out EntityLifetimeModel.Binding Binding)
        { return TryAdmitEntity(Entity, Authority, IsCurrent, ReadEntityEvidence, false, out Binding); }

        private bool TryAdmitEntity(BaseEntity Entity, EntityLifetimeModel.Authority Authority,
            Func<EntityLifetimeModel.Authority, bool> IsCurrent,
            Func<object, EntityLifetimeModel.HostEvidence> ReadEvidence,
            bool PatchTopologyChecked,
            out EntityLifetimeModel.Binding Binding)
        {
            Binding = null;
            if (Thread.CurrentThread.ManagedThreadId != EntityOwnerThread) return false;
            try {
                if (EntityObserverBroken || !EntityStartupQualified) return false;
                if (!PatchTopologyChecked && !VerifyAllEntityPatches()) { BreakEntityObserver("admission patch topology changed"); return false; }
                return EntityLifetimes.TryAdmit(Entity, Authority, IsCurrent, ReadEvidence, out Binding);
            }
            catch (Exception) { BreakEntityObserver("entity admission check failed"); return false; }
        }

        private bool ValidateEntity(EntityLifetimeModel.Binding Binding,
            Func<EntityLifetimeModel.Authority, bool> IsCurrent)
        {
            if (Thread.CurrentThread.ManagedThreadId != EntityOwnerThread) return false;
            try {
                if (EntityObserverBroken || !EntityStartupQualified) return false;
                if (!VerifyAllEntityPatches()) { BreakEntityObserver("validation patch topology changed"); return false; }
                return EntityLifetimes.Validate(Binding, IsCurrent, ReadEntityEvidence);
            }
            catch (Exception) { BreakEntityObserver("entity validation check failed"); return false; }
        }

        // The private host model is bound to the same exact facade session and
        // publication checkpoint that own future script proxies. A committed
        // nested publication remains valid; rollback or domain retirement does
        // not transfer authority to another session with the same numeric IDs.
        private sealed class EntityFacadeBinding
        {
            internal readonly EntityLifetimeModel.Binding Lifetime;
            internal readonly FacadeSession Session;
            internal readonly FacadeSession.PublicationWitness Publication;
            internal EntityFacadeBinding(EntityLifetimeModel.Binding Lifetime, FacadeSession Session,
                FacadeSession.PublicationWitness Publication)
            { this.Lifetime = Lifetime; this.Session = Session; this.Publication = Publication; }
        }

        private static bool EntityFacadeCurrent(EntityLifetimeModel.Authority Authority,
            FacadeSession Session, FacadeSession.PublicationWitness Publication)
        {
            return Session != null && !Session.Disposed && Publication != null &&
                Session.VmGenerationId > 0 && Session.DomainLifetimeId > 0 &&
                Authority.VmGeneration == checked((ulong)Session.VmGenerationId) &&
                Authority.DomainLifetime == checked((ulong)Session.DomainLifetimeId) &&
                Authority.PublicationLifetime == Publication.Token &&
                Session.IsPublicationWitnessCurrent(Publication);
        }

        private bool TryAdmitEntity(BaseEntity Entity, FacadeSession Session, out EntityFacadeBinding Result)
        { return TryAdmitEntity(Entity, Session, ReadEntityEvidence, false, out Result); }

        private bool TryAdmitEntity(BaseEntity Entity, FacadeSession Session,
            Func<object, EntityLifetimeModel.HostEvidence> ReadEvidence,
            bool PatchTopologyChecked, out EntityFacadeBinding Result)
        {
            Result = null;
            if (Session == null || Session.Disposed || Session.VmGenerationId <= 0 ||
                Session.DomainLifetimeId <= 0 || Thread.CurrentThread.ManagedThreadId != EntityOwnerThread)
                return false;
            try {
                FacadeSession.PublicationWitness Publication = Session.CaptureEntityWitness();
                var Authority = new EntityLifetimeModel.Authority(checked((ulong)Session.VmGenerationId),
                    checked((ulong)Session.DomainLifetimeId), Publication.Token);
                Func<EntityLifetimeModel.Authority, bool> IsCurrent = Value =>
                    EntityFacadeCurrent(Value, Session, Publication);
                EntityLifetimeModel.Binding Lifetime;
                if (!TryAdmitEntity(Entity, Authority, IsCurrent, ReadEvidence, PatchTopologyChecked, out Lifetime)) return false;
                Result = new EntityFacadeBinding(Lifetime, Session, Publication);
                return true;
            }
            catch (Exception) { return false; }
        }

        private bool ValidateEntity(EntityFacadeBinding Value)
        {
            if (Value == null || Value.Lifetime == null || Value.Session == null ||
                Thread.CurrentThread.ManagedThreadId != EntityOwnerThread) return false;
            return ValidateEntity(Value.Lifetime, Authority =>
                EntityFacadeCurrent(Authority, Value.Session, Value.Publication));
        }

        private static EntityLifetimeModel.HostEvidence ReadEntityEvidence(object Identity)
        {
            BaseEntity Entity = Identity as BaseEntity;
            if (Entity == null || Entity.net == null)
                return new EntityLifetimeModel.HostEvidence(false, false, false, 0, null, null);
            ulong Id = Entity.net.ID.Value;
            return new EntityLifetimeModel.HostEvidence(!Entity.IsDestroyed, Entity.IsFullySpawned(),
                true, Id, Entity.PrefabName, BaseNetworkable.serverEntities.Find(Entity.net.ID));
        }

        [AutoPatch(IsRequired = true), HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.Initialize),
            typeof(bool), typeof(string), typeof(bool), typeof(bool))]
        private static class EntityInitializePatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                CarbonLuau Observer = ActiveEntityObserver;
                if (Observer != null) Observer.OnEntityInitializePrefix();
            }
        }

        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class EntityFullSpawnPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                try {
                    MethodBase[] Targets = ResolveEntitySpawnTargets();
                    if (Targets == null) { EntityTargetInventoryFailed = true; return new MethodBase[0]; }
                    EntitySpawnTargets = Targets;
                    return Targets;
                }
                catch (Exception) { EntityTargetInventoryFailed = true; return new MethodBase[0]; }
            }

            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(BaseNetworkable __instance, MethodBase __originalMethod,
                out EntitySpawnFrame __state)
            {
                __state = null;
                CarbonLuau Observer = ActiveEntityObserver;
                if (Observer == null || Observer.EntityObserverBroken) return;
                try { __state = Observer.EnterEntitySpawn(__instance, __originalMethod); }
                catch (Exception) { Observer.BreakEntityObserver("Spawn prefix failed"); }
            }

            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, EntitySpawnFrame __state)
            {
                CarbonLuau Observer = ActiveEntityObserver;
                if (Observer == null || __state == null) return;
                try { Observer.ExitEntityPostfix(__runOriginal, __state); }
                catch (Exception) { Observer.BreakEntityObserver("Spawn postfix failed"); }
            }

            [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, EntitySpawnFrame __state)
            {
                CarbonLuau Observer = ActiveEntityObserver;
                if (Observer == null || __state == null) return;
                try { Observer.ExitEntityFinalizer(__exception, __state); }
                catch (Exception) { Observer.BreakEntityObserver("Spawn finalizer failed"); }
            }
        }

        private EntitySpawnFrame EnterEntitySpawn(BaseNetworkable Entity, MethodBase Method)
        {
            if (Entity == null || Method == null || Thread.CurrentThread.ManagedThreadId != EntityOwnerThread ||
                !EntityInitializeSeen || !VerifyEntityPatchTopology(Method)) {
                BreakEntityObserver("Spawn entry or patch topology mismatch");
                return null;
            }
            EntitySpawnChain Chain = EntitySpawnChains.GetValue(Entity, Key => new EntitySpawnChain());
            if (Chain.Methods.Count == 0) {
                MethodInfo Outer = Entity.GetType().GetMethod(nameof(BaseNetworkable.Spawn),
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (!SameEntityMethod(Outer, Method)) { BreakEntityObserver("Spawn outer-chain mismatch"); return null; }
                Chain.Poisoned = false;
                Chain.BaseReturned = false;
                RetireEntityDestroyWatchAtSpawn(Entity);
                Chain.Attempt = EntityLifetimes.BeginSpawn(Entity);
            }
            else {
                Type Parent = Chain.Methods.Peek().DeclaringType.BaseType;
                MethodInfo Expected = Parent == null ? null : Parent.GetMethod(nameof(BaseNetworkable.Spawn),
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (!SameEntityMethod(Expected, Method)) {
                    EntityLifetimes.BeginSpawn(Entity); // poisons any pending same-object attempt
                    Chain.Poisoned = true;
                    BreakEntityObserver("Spawn nested or base-chain mismatch");
                    return null;
                }
            }
            Chain.Methods.Push(Method);
            return new EntitySpawnFrame { Chain = Chain, Method = Method };
        }

        private void ExitEntityPostfix(bool OriginalRan, EntitySpawnFrame Frame)
        {
            if (EntityObserverBroken) return;
            Frame.PostfixSeen = true;
            Frame.OriginalRan = OriginalRan;
            if (!OriginalRan) Frame.Chain.Poisoned = true;
            if (Frame.Method.DeclaringType == typeof(BaseNetworkable) && OriginalRan)
                Frame.Chain.BaseReturned = true;
            if (!VerifyEntityPatchTopology(Frame.Method)) BreakEntityObserver("Spawn postfix patch topology changed");
        }

        private void ExitEntityFinalizer(Exception Error, EntitySpawnFrame Frame)
        {
            if (Frame.Exited) return;
            Frame.Exited = true;
            EntitySpawnChain Chain = Frame.Chain;
            if (Thread.CurrentThread.ManagedThreadId != EntityOwnerThread ||
                Chain.Methods.Count == 0 || !SameEntityMethod(Chain.Methods.Peek(), Frame.Method)) {
                BreakEntityObserver("Spawn finalizer chain mismatch");
                return;
            }
            if (Error != null || !Frame.PostfixSeen || !Frame.OriginalRan) Chain.Poisoned = true;
            if (!VerifyEntityPatchTopology(Frame.Method)) BreakEntityObserver("Spawn finalizer patch topology changed");
            Chain.Methods.Pop();
            if (Chain.Methods.Count != 0) return;
            EntityLifetimeModel.SpawnAttempt Attempt = Chain.Attempt;
            Chain.Attempt = null;
            if (Attempt != null && EntityLifetimes.CompleteSpawn(Attempt,
                !EntityObserverBroken && !Chain.Poisoned && Chain.BaseReturned, !Chain.Poisoned)) {
                ArmEntityDestroyWatch(Attempt.Target as BaseEntity);
                ObserveEntitySpawned(Attempt.Target as BaseEntity);
            }
        }
    }
}
