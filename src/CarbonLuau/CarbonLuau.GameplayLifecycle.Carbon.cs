using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Oxide.Core.Plugins;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Private A0 producer. Neither a scripting hook nor a public Signal.
        internal sealed class GameplayHostObservation
        {
            internal readonly string Kind, PlayerToken, UserId, Name, KillerToken, KillerId, KillerName;
            internal readonly PlayerPosition? Position;
            internal GameplayHostObservation(string Kind, PlayerLifetime Player, PlayerPosition? Position,
                PlayerLifetime Killer)
            {
                this.Kind = Kind; PlayerToken = Player.Token; UserId = Player.UserId; Name = Player.Name;
                this.Position = Position;
                KillerToken = Killer == null ? null : Killer.Token;
                KillerId = Killer == null ? null : Killer.UserId;
                KillerName = Killer == null ? null : Killer.Name;
            }
        }

        private Action<GameplayHostObservation> GameplayObservationReceiver;
        private Func<string, bool> GameplayCaptureAdmission;
        private const int GameplayMaximumFrames = 16;
        private static volatile CarbonLuau ActiveGameplayObserver;
        private static bool GameplayDeathMarkerValid;
        private static string GameplayDeathPreparation, GameplaySpawnPreparation;
        private int GameplayLifecycleOwner;
        private volatile bool GameplayDeathQualified, GameplaySpawnQualified;
        private volatile bool GameplayOffThreadDeathSeen, GameplayOffThreadSpawnSeen;
        private int GameplayOffThreadDeathPending, GameplayOffThreadSpawnPending;
        private bool GameplayOffThreadReported;
        private long GameplayLifecycleOffThreadRejected;
        private string GameplayLifecycleFailure;
        private long GameplayLifecycleDeaths, GameplayLifecycleSpawns, GameplayLifecycleDrops;
        private long GameplayLifecycleRejected, GameplayLifecycleNested, GameplayLifecycleFailures;
        private long GameplayLifecyclePositions;
        private readonly Dictionary<string, GameplayLife> GameplayLives =
            new Dictionary<string, GameplayLife>(StringComparer.Ordinal);
        private readonly List<GameplayFrame> GameplayFrames = new List<GameplayFrame>(GameplayMaximumFrames);
        private static readonly MethodInfo GameplayDieMethod = ResolveGameplayMethod(typeof(BasePlayer), "Die", 0x06001aea, "HitInfo");
        private static readonly MethodInfo GameplayBaseDieMethod = ResolveGameplayMethod(typeof(BaseCombatEntity), "Die", 0x0600149b, "HitInfo");
        private static readonly MethodInfo GameplayOnDiedMethod = ResolveGameplayMethod(typeof(BasePlayer), "OnDied", 0x06001abd, "HitInfo");
        private static readonly MethodInfo GameplayRespawnMethod = ResolveGameplayMethod(typeof(BasePlayer), "RespawnAt", 0x06001abe,
            "UnityEngine.Vector3", "UnityEngine.Quaternion", "BaseEntity");
        private const string GameplayDeathHook = "Carbon.Hooks.Category_Player+Player_BasePlayer+Player_BasePlayer_db9ac3eb926b4eff9bee0481b3b20c1a";
        private const string GameplayEntityDeathHook = "Carbon.Hooks.Category_Entity+Entity_BaseCombatEntity+Entity_BaseCombatEntity_165ca5e2aedc4e16bb94f45744faead3";
        private const string GameplayRespawnHook = "Carbon.Hooks.Category_Player+Player_BasePlayer+Player_BasePlayer_09385e6f153d459aae0f142f5eaf7f5d";
        private const string GameplayWindowsStartupHash = "b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28";
        private const string GameplayLinuxStartupHash = "7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0";
        private string GameplayHooksMvid;
        private const int GameplayMaximumPatchRecords = 8192, GameplayMaximumPatchBytes = 65536;
        private FieldInfo GameplayPatchStateField, GameplayPatchLockField;
        private IDictionary GameplayPatchState;
        private object GameplayPatchLock;
        private readonly Dictionary<MethodBase, byte[]> GameplayPatchStamps = new Dictionary<MethodBase, byte[]>();
        private static readonly MethodInfo[] GameplayDeathTargets = { GameplayDieMethod, GameplayBaseDieMethod, GameplayOnDiedMethod };
        private static readonly MethodInfo[] GameplaySpawnTargets = { GameplayRespawnMethod };

        private sealed class GameplayLife
        {
            internal string UserId;
            internal ulong Epoch = 1;
            internal bool Dead;
        }
        private sealed class GameplayFrame
        {
            internal CarbonLuau Observer;
            internal BasePlayer Player;
            internal PlayerLifetime Lifetime;
            internal GameplayLife Life;
            internal string Kind, EntryRespawnId;
            internal bool EntryAlive, Poisoned, BaseReturned, PostfixSeen, OriginalRan, Exited;
        }
        private static void CountGameplay(ref long Counter) { if (Counter < Int64.MaxValue) Counter++; }
        private void RejectGameplayOffThread(bool Death)
        {
            // Managed atomic data only: no host object, collection, VM or logger.
            if (Death) {
                GameplayOffThreadDeathSeen = true; GameplayDeathQualified = false;
                Interlocked.Exchange(ref GameplayOffThreadDeathPending, 1);
            }
            else {
                GameplayOffThreadSpawnSeen = true; GameplaySpawnQualified = false;
                Interlocked.Exchange(ref GameplayOffThreadSpawnPending, 1);
            }
            // One CAS, no retry loop. Saturating diagnostic is best effort under
            // concurrent rejection; invalidation/pending bits never depend on it.
            long Current = Interlocked.Read(ref GameplayLifecycleOffThreadRejected);
            if (Current < Int64.MaxValue) Interlocked.CompareExchange(ref GameplayLifecycleOffThreadRejected, Current + 1, Current);
        }
        private void DrainGameplayThreadDiagnostics()
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) return;
            bool Death = Interlocked.Exchange(ref GameplayOffThreadDeathPending, 0) != 0;
            bool Spawn = Interlocked.Exchange(ref GameplayOffThreadSpawnPending, 0) != 0;
            if (GameplayOffThreadDeathSeen) GameplayDeathQualified = false;
            if (GameplayOffThreadSpawnSeen) GameplaySpawnQualified = false;
            if ((!Death && !Spawn) || GameplayOffThreadReported) return;
            GameplayOffThreadReported = true;
            CountGameplay(ref GameplayLifecycleFailures);
            if (GameplayLifecycleFailure == null) GameplayLifecycleFailure = "off-thread lifecycle capture";
            try { PrintWarning("[CarbonLuau:Gameplay] Private lifecycle capture rejected off-thread; affected capability disabled."); }
            catch (Exception) { }
        }
        private static MethodInfo ResolveGameplayMethod(Type Owner, string Name, int Token, params string[] Parameters)
        {
            try {
                // Resolve the pinned MethodDef directly rather than letting a
                // runtime binder substitute visibility or Unity type aliases.
                MethodInfo Method = Owner.Module.ResolveMethod(Token) as MethodInfo;
                if (Method == null || Method.DeclaringType != Owner || Method.Name != Name || Method.IsStatic) return null;
                ParameterInfo[] Actual = Method.GetParameters();
                if (Actual.Length != Parameters.Length) return null;
                for (int Index = 0; Index < Actual.Length; Index++)
                    if (Actual[Index].ParameterType.FullName != Parameters[Index]) return null;
                return Method;
            }
            catch (Exception) { return null; }
        }

        private void InitializeGameplayLifecycle()
        {
            int CurrentThread = Thread.CurrentThread.ManagedThreadId;
            if (GameplayLifecycleOwner != 0 && CurrentThread != GameplayLifecycleOwner) {
                RejectGameplayOffThread(true); RejectGameplayOffThread(false); return;
            }
            try { if (Gameplay != null) Gameplay.Players.CheckOwner(); }
            catch (Exception) { RejectGameplayOffThread(true); RejectGameplayOffThread(false); return; }
            GameplayLifecycleOwner = Thread.CurrentThread.ManagedThreadId;
            if (Gameplay != null) {
                Gameplay.GameplayEvents.FrameClock = () => UnityEngine.Time.frameCount;
                Gameplay.GameplayAvailable = Kind => Kind == "died" ? GameplayDeathQualified : Kind == "spawned" && GameplaySpawnQualified;
                GameplayCaptureAdmission = Kind => !Stopping && Host != null && Host.Ready && Gameplay.GameplayEvents.Capture(Kind);
                GameplayObservationReceiver = Observation => {
                    if (Stopping || Host == null || !Host.Ready || Gameplay == null) return;
                    Gameplay.GameplayEvent(Observation.Kind, Observation.PlayerToken, Observation.UserId, Observation.Name,
                        Observation.Position, Observation.KillerToken, Observation.KillerId, Observation.KillerName);
                    RequestDrain();
                };
            }
            GameplayDeathQualified = GameplaySpawnQualified = false;
            GameplayOffThreadDeathSeen = GameplayOffThreadSpawnSeen = false;
            Interlocked.Exchange(ref GameplayOffThreadDeathPending, 0);
            Interlocked.Exchange(ref GameplayOffThreadSpawnPending, 0);
            GameplayOffThreadReported = false;
            GameplayLifecycleFailure = null;
            GameplayLives.Clear(); GameplayFrames.Clear();
            if (ActiveGameplayObserver != null && !ReferenceEquals(ActiveGameplayObserver, this)) {
                ActiveGameplayObserver.RejectGameplayCapability(true, "overlapping observer");
                ActiveGameplayObserver.RejectGameplayCapability(false, "overlapping observer");
                GameplayLifecycleFailure = "overlapping observer"; ReportGameplayInitialization(); return;
            }
            ActiveGameplayObserver = this;
            try {
                if (!VerifyGameplayHost()) { GameplayLifecycleFailure = "host binary pins"; return; }
                if (!InitializeGameplayPatchState()) { GameplayLifecycleFailure = "Harmony state shape"; return; }
                string DieFailure, BaseFailure, OnDiedFailure, SpawnFailure;
                bool DiePinned = GameplayMethodPinned(GameplayDieMethod, true, out DieFailure);
                bool BasePinned = GameplayMethodPinned(GameplayBaseDieMethod, true, out BaseFailure);
                bool OnDiedPinned = GameplayMethodPinned(GameplayOnDiedMethod, true, out OnDiedFailure);
                GameplayDeathQualified = GameplayDeathMarkerValid && DiePinned && BasePinned && OnDiedPinned;
                GameplaySpawnQualified = GameplayMethodPinned(GameplayRespawnMethod, false, out SpawnFailure);
                if (!GameplayDeathQualified) GameplayLifecycleFailure = !GameplayDeathMarkerValid ? "death marker" :
                    !DiePinned ? "death method " + DieFailure : !BasePinned ? "base death method " + BaseFailure : "OnDied method " + OnDiedFailure;
                if (!GameplaySpawnQualified && GameplayLifecycleFailure == null) GameplayLifecycleFailure = "spawn method " + SpawnFailure;
                bool Busy;
                if (GameplayDeathQualified && !VerifyGameplayTopology(true, out Busy)) RejectGameplayCapability(true, "death patch topology");
                if (GameplaySpawnQualified && !VerifyGameplayTopology(false, out Busy)) RejectGameplayCapability(false, "spawn patch topology");
            }
            catch (Exception) { RejectGameplayCapability(true, "initialization"); RejectGameplayCapability(false, "initialization"); }
            finally { ReportGameplayInitialization(); }
        }
        private void ReportGameplayInitialization()
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) return;
            DrainGameplayThreadDiagnostics();
            try {
                Puts("[CarbonLuau:Gameplay] Private initialization Death=" + GameplayDeathQualified +
                    " Spawn=" + GameplaySpawnQualified + " Marker=" + GameplayDeathMarkerValid +
                    " DieResolved=" + (GameplayDieMethod != null) + " SpawnResolved=" + (GameplayRespawnMethod != null) +
                    " DeathPrepare=" + (GameplayDeathPreparation ?? "not seen") +
                    " SpawnPrepare=" + (GameplaySpawnPreparation ?? "not seen") +
                    " Reason=" + (GameplayLifecycleFailure ?? "none"));
            }
            catch (Exception) { }
        }
        private string GameplayLifecycleStatus
        {
            get {
                DrainGameplayThreadDiagnostics();
                return "[CarbonLuau:Gameplay] died_source=" + GameplayDeathQualified + "; spawned_source=" + GameplaySpawnQualified +
                    "; reason=" + (GameplayLifecycleFailure ?? "none") + "; deaths=" + GameplayLifecycleDeaths +
                    "; spawns=" + GameplayLifecycleSpawns + "; drops=" + GameplayLifecycleDrops +
                    "; rejected=" + GameplayLifecycleRejected + "; nested=" + GameplayLifecycleNested +
                    "; off_thread_rejected=" + Interlocked.Read(ref GameplayLifecycleOffThreadRejected) +
                    "\n" + (Gameplay == null ? "[CarbonLuau:Gameplay] unavailable" : Gameplay.GameplayEvents.Status);
            }
        }
        // Explicit A0 fixture diagnostic only. Never used by capture/admission,
        // never a permission to accept a changed live method body.
        private void WriteGameplayLiveIlProof()
        {
            MethodInfo[] Methods = { GameplayDieMethod, GameplayBaseDieMethod, GameplayOnDiedMethod, GameplayRespawnMethod };
            var Opcodes = new Dictionary<ushort, OpCode>();
            foreach (FieldInfo Field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)) {
                if (Field.FieldType != typeof(OpCode)) continue;
                OpCode Code = (OpCode)Field.GetValue(null); Opcodes[unchecked((ushort)Code.Value)] = Code;
            }
            foreach (MethodInfo Method in Methods) {
                if (Method == null) { Puts("[CarbonLuau:GameplayIL] unresolved method"); continue; }
                try {
                    byte[] Bytes = Method.GetMethodBody().GetILAsByteArray();
                    string Identity = Method.DeclaringType.FullName + "." + Method.Name;
                    if (Bytes.Length > 4096) { Puts("[CarbonLuau:GameplayIL] " + Identity + " rejected diagnostic size=" + Bytes.Length); continue; }
                    string RawHash;
                    using (SHA256 Hash = SHA256.Create()) RawHash = BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
                    string Location = Method.Module.Assembly.Location;
                    if (Location == null || Location.Length > 512) Location = "unavailable";
                    Puts("[CarbonLuau:GameplayIL] Method=" + Identity + " Token=0x" + Method.MetadataToken.ToString("x8") +
                        " Size=" + Bytes.Length + " RawSHA256=" + RawHash + " MVID=" + Method.Module.ModuleVersionId.ToString("D") +
                        " Location=" + Location);
                    Puts("[CarbonLuau:GameplayIL] RawHex " + Identity + " " + BitConverter.ToString(Bytes).Replace("-", ""));
                    var Lines = new List<string>();
                    int Offset = 0, Characters = 0;
                    while (Offset < Bytes.Length) {
                        if (Lines.Count >= 1024) throw new InvalidOperationException("diagnostic instruction bound");
                        int Start = Offset;
                        ushort Value = Bytes[Offset++];
                        if (Value == 0xfe) Value = (ushort)(0xfe00 | Bytes[Offset++]);
                        OpCode Opcode;
                        if (!Opcodes.TryGetValue(Value, out Opcode)) throw new InvalidOperationException("diagnostic opcode");
                        string Operand = GameplayIlOperand(Method.Module, Bytes, ref Offset, Opcode.OperandType);
                        string Line = Start.ToString("x4") + "|" + Opcode.Name + "|" + Operand;
                        Characters += Line.Length;
                        if (Characters > 262144) throw new InvalidOperationException("diagnostic text bound");
                        Lines.Add(Line);
                    }
                    foreach (ExceptionHandlingClause Clause in Method.GetMethodBody().ExceptionHandlingClauses)
                        Puts("[CarbonLuau:GameplayIL] EH " + Identity + " Kind=" + Clause.Flags + " Try=" + Clause.TryOffset + ":" + Clause.TryLength +
                            " Handler=" + Clause.HandlerOffset + ":" + Clause.HandlerLength);
                    string Canonical = String.Join("\n", Lines);
                    string CanonicalHash;
                    using (SHA256 Hash = SHA256.Create()) CanonicalHash = BitConverter.ToString(Hash.ComputeHash(Encoding.UTF8.GetBytes(Canonical))).Replace("-", "").ToLowerInvariant();
                    Puts("[CarbonLuau:GameplayIL] Canonical " + Identity + " Count=" + Lines.Count + " SHA256=" + CanonicalHash);
                    foreach (string Line in Lines) Puts("[CarbonLuau:GameplayIL] IL " + Identity + " " + Line);
                }
                catch (Exception Error) { Puts("[CarbonLuau:GameplayIL] Diagnostic rejected " + Method.Name + " " + Error.GetType().Name); }
            }
        }
        private static string GameplayIlType(Type Type)
        {
            if (Type.IsByRef) return GameplayIlType(Type.GetElementType()) + "&";
            if (Type.IsPointer) return GameplayIlType(Type.GetElementType()) + "*";
            if (Type.IsArray) return GameplayIlType(Type.GetElementType()) + "[" + new string(',', Type.GetArrayRank() - 1) + "]";
            if (Type.IsGenericParameter) return (Type.DeclaringMethod == null ? "!" : "!!") + Type.GenericParameterPosition;
            if (Type.IsGenericType) {
                Type[] Arguments = Type.GetGenericArguments();
                string[] Names = new string[Arguments.Length];
                for (int Index = 0; Index < Arguments.Length; Index++) Names[Index] = GameplayIlType(Arguments[Index]);
                return Type.GetGenericTypeDefinition().FullName + "<" + String.Join(",", Names) + ">";
            }
            return Type.FullName;
        }
        private static string GameplayIlMember(Module Module, int Token)
        {
            MemberInfo Member = Module.ResolveMember(Token);
            Type Type = Member as Type;
            if (Type != null) return "type:" + GameplayIlType(Type);
            FieldInfo Field = Member as FieldInfo;
            if (Field != null) return "field:" + GameplayIlType(Field.DeclaringType) + "::" + Field.Name + ":" + GameplayIlType(Field.FieldType);
            MethodBase Method = Member as MethodBase;
            if (Method == null) throw new InvalidOperationException("diagnostic member kind");
            ParameterInfo[] Parameters = Method.GetParameters();
            string[] Names = new string[Parameters.Length];
            for (int Index = 0; Index < Names.Length; Index++) Names[Index] = GameplayIlType(Parameters[Index].ParameterType);
            MethodInfo Function = Method as MethodInfo;
            string Result = Function == null ? "System.Void" : GameplayIlType(Function.ReturnType);
            string Generic = "";
            if (Function != null && Function.IsGenericMethod) {
                Type[] Arguments = Function.GetGenericArguments(); string[] Types = new string[Arguments.Length];
                for (int Index = 0; Index < Types.Length; Index++) Types[Index] = GameplayIlType(Arguments[Index]);
                Generic = "<" + String.Join(",", Types) + ">";
            }
            return "method:" + GameplayIlType(Method.DeclaringType) + "::" + Method.Name + Generic + "(" + String.Join(",", Names) + "):" + Result;
        }
        private static string GameplayIlOperand(Module Module, byte[] Bytes, ref int Offset, OperandType Kind)
        {
            int Token;
            switch (Kind) {
                case OperandType.InlineNone: return "";
                case OperandType.ShortInlineI: return unchecked((sbyte)Bytes[Offset++]).ToString(CultureInfo.InvariantCulture);
                case OperandType.InlineI: Token = BitConverter.ToInt32(Bytes, Offset); Offset += 4; return Token.ToString(CultureInfo.InvariantCulture);
                case OperandType.InlineI8: long Long = BitConverter.ToInt64(Bytes, Offset); Offset += 8; return Long.ToString(CultureInfo.InvariantCulture);
                case OperandType.ShortInlineR: float SingleValue = BitConverter.ToSingle(Bytes, Offset); Offset += 4; return SingleValue.ToString("R", CultureInfo.InvariantCulture);
                case OperandType.InlineR: double DoubleValue = BitConverter.ToDouble(Bytes, Offset); Offset += 8; return DoubleValue.ToString("R", CultureInfo.InvariantCulture);
                case OperandType.ShortInlineVar: return Bytes[Offset++].ToString(CultureInfo.InvariantCulture);
                case OperandType.InlineVar: int Variable = BitConverter.ToUInt16(Bytes, Offset); Offset += 2; return Variable.ToString(CultureInfo.InvariantCulture);
                case OperandType.ShortInlineBrTarget: int ShortBranch = unchecked((sbyte)Bytes[Offset++]); return (Offset + ShortBranch).ToString("x4");
                case OperandType.InlineBrTarget: int Branch = BitConverter.ToInt32(Bytes, Offset); Offset += 4; return (Offset + Branch).ToString("x4");
                case OperandType.InlineSwitch:
                    int Count = BitConverter.ToInt32(Bytes, Offset); Offset += 4;
                    if (Count < 0 || Count > 1024) throw new InvalidOperationException("diagnostic switch bound");
                    int End = checked(Offset + Count * 4); string[] Targets = new string[Count];
                    for (int Index = 0; Index < Count; Index++) { Targets[Index] = (End + BitConverter.ToInt32(Bytes, Offset)).ToString("x4"); Offset += 4; }
                    return String.Join(",", Targets);
                case OperandType.InlineString:
                    Token = BitConverter.ToInt32(Bytes, Offset); Offset += 4;
                    string Text = Module.ResolveString(Token);
                    if (Text.Length > 4096) throw new InvalidOperationException("diagnostic string bound");
                    return "string:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Text));
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    Token = BitConverter.ToInt32(Bytes, Offset); Offset += 4; return GameplayIlMember(Module, Token);
                case OperandType.InlineSig:
                    Token = BitConverter.ToInt32(Bytes, Offset); Offset += 4;
                    return "signature:" + BitConverter.ToString(Module.ResolveSignature(Token)).Replace("-", "");
                default: throw new InvalidOperationException("diagnostic operand kind");
            }
        }
        private void StopGameplayLifecycle()
        {
            if (GameplayLifecycleOwner == 0) {
                GameplayDeathQualified = GameplaySpawnQualified = false;
                if (ReferenceEquals(ActiveGameplayObserver, this)) ActiveGameplayObserver = null;
                GameplayCaptureAdmission = null; GameplayObservationReceiver = null; return;
            }
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) {
                RejectGameplayOffThread(true); RejectGameplayOffThread(false); return;
            }
            DrainGameplayThreadDiagnostics();
            GameplayDeathQualified = GameplaySpawnQualified = false;
            if (ReferenceEquals(ActiveGameplayObserver, this)) ActiveGameplayObserver = null;
            GameplayLives.Clear(); GameplayFrames.Clear();
            GameplayPatchStamps.Clear(); GameplayPatchState = null; GameplayPatchLock = null;
            GameplayPatchStateField = GameplayPatchLockField = null;
            GameplayCaptureAdmission = null; GameplayObservationReceiver = null;
        }
        private void GameplayLifecycleDisconnected(PlayerLifetime Lifetime)
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner || Lifetime == null) return;
            GameplayLife Life;
            if (GameplayLives.TryGetValue(Lifetime.Token, out Life) && Life.UserId == Lifetime.UserId)
                GameplayLives.Remove(Lifetime.Token);
        }
        private void RejectGameplayCapability(bool Death, string Reason)
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) { RejectGameplayOffThread(Death); return; }
            if (Death) GameplayDeathQualified = false; else GameplaySpawnQualified = false;
            CountGameplay(ref GameplayLifecycleFailures);
            if (GameplayLifecycleFailure == null) {
                GameplayLifecycleFailure = Reason;
                try { PrintWarning("[CarbonLuau:Gameplay] Private lifecycle observer unavailable: " + Reason); }
                catch (Exception) { }
            }
        }

        // Cold file hashing is independent of Entity observer startup continuity.
        private bool VerifyGameplayHost()
        {
            bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
            if ((!Linux && Environment.OSVersion.Platform != PlatformID.Win32NT) || IntPtr.Size != 8) return false;
            Assembly Rust = typeof(BasePlayer).Assembly;
            if (!HasHash(Rust, Linux ? LinuxRustHash : WindowsRustHash) ||
                Rust.ManifestModule.ModuleVersionId.ToString("D") !=
                (Linux ? "616082a0-36f4-4680-a1ab-efc5766796cc" : "c1c11bd1-baa1-4d85-b64e-1d4a4664d62e")) return false;
            DirectoryInfo Managed = new DirectoryInfo(Path.GetDirectoryName(Rust.Location));
            if (Managed.Name != "Managed" || Managed.Parent == null || Managed.Parent.Name != "RustDedicated_Data" ||
                Managed.Parent.Parent == null) return false;
            string CarbonManaged = Path.Combine(Managed.Parent.Parent.FullName, "carbon", "managed");
            Assembly Carbon = LoadedEntityAssembly("Carbon"), Common = typeof(CarbonPlugin).Assembly;
            Assembly Startup = LoadedEntityAssembly("Carbon.Startup");
            Assembly HarmonyAssembly = typeof(Harmony).Assembly;
            if (Carbon == null || Carbon.GetName().Version.ToString() != (Linux ? "2.0.261.0" : "2.0.262.0") ||
                Carbon.ManifestModule.ModuleVersionId.ToString("D") != (Linux ? LinuxCarbonMvid : WindowsCarbonMvid) ||
                Common.ManifestModule.ModuleVersionId.ToString("D") != (Linux ? LinuxCarbonCommonMvid : WindowsCarbonCommonMvid) ||
                Common.GetName().Version.ToString() != (Linux ? "2.0.261.0" : "2.0.262.0") ||
                !HasFileHash(Path.Combine(CarbonManaged, "Carbon.dll"), Linux ? LinuxCarbonHash : WindowsCarbonHash) ||
                !HasFileHash(Path.Combine(CarbonManaged, "Carbon.Common.dll"), Linux ? LinuxCarbonCommonHash : WindowsCarbonCommonHash) ||
                (!String.IsNullOrEmpty(Carbon.Location) && !HasHash(Carbon, Linux ? LinuxCarbonHash : WindowsCarbonHash)) ||
                (!String.IsNullOrEmpty(Common.Location) && !HasHash(Common, Linux ? LinuxCarbonCommonHash : WindowsCarbonCommonHash)) ||
                Startup == null || Startup.GetName().Version.ToString() != (Linux ? "2.0.261.0" : "2.0.262.0") ||
                Startup.ManifestModule.ModuleVersionId.ToString("D") !=
                    (Linux ? "4e5e212d-8fa6-4aa7-9141-1a5c52297ddb" : "0b893c28-fec5-44c5-bac5-f7795614beea") ||
                !HasFileHash(Path.Combine(CarbonManaged, "Carbon.Startup.dll"), Linux ? GameplayLinuxStartupHash : GameplayWindowsStartupHash) ||
                (!String.IsNullOrEmpty(Startup.Location) && !HasHash(Startup, Linux ? GameplayLinuxStartupHash : GameplayWindowsStartupHash)) ||
                HarmonyAssembly.GetName().Version.ToString() != "2.4.2.0" ||
                HarmonyAssembly.ManifestModule.ModuleVersionId.ToString("D") != "b9e6cf65-9433-482b-8860-83cff28d0128" ||
                !HasFileHash(Path.Combine(CarbonManaged, "lib", "0Harmony.dll"), LinuxHarmonyHash) ||
                (!String.IsNullOrEmpty(HarmonyAssembly.Location) && !HasHash(HarmonyAssembly, LinuxHarmonyHash)) ||
                !HasFileHash(Path.Combine(CarbonManaged, "hooks", "Carbon.Hooks.Oxide.dll"), Linux ? LinuxHooksHash : WindowsHooksHash) ||
                !HasFileHash(Path.Combine(CarbonManaged, "hooks", "Carbon.Hooks.Community.dll"), Linux ? LinuxCommunityHash : WindowsOldCommunityHash)) return false;
            FieldInfo IPlayer = typeof(BasePlayer).GetField("IPlayer", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (IPlayer == null || IPlayer.IsStatic || !IPlayer.IsNotSerialized ||
                IPlayer.FieldType.FullName != "Oxide.Core.Libraries.Covalence.IPlayer" ||
                !ReferenceEquals(IPlayer.FieldType.Assembly, Common)) return false;
            GameplayHooksMvid = Linux ? "24c572a8-968b-4048-94a7-40ae119d0b8c" : "25387c43-282d-4de5-9dcf-2aea220206a2";
            return true;
        }
        private static bool GameplayMethodPinned(MethodInfo Method, bool Death)
        {
            string Failure;
            return GameplayMethodPinned(Method, Death, out Failure);
        }
        private static bool GameplayMethodPinned(MethodInfo Method, bool Death, out string Failure)
        {
            Failure = "unresolved";
            try {
                bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
                if (Method == null) return false;
                if (Method.IsAbstract || Method.ReturnType != typeof(void)) { Failure = "method shape"; return false; }
                // These are the processed LIVE module bodies, not original PE
                // bodies. Pinned Carbon.Startup.InjectIPlayer adds one field;
                // Cecil serialization reindexes operand tokens. The research
                // helper independently reproduces all four exact live hashes
                // and verifies unchanged opcodes/resolved operands/EH on W/L.
                // VerifyGameplayHost separately pins the original backing PE.
                string Expected; int Token, Size;
                if (!Death) { Token = 0x06001abe; Size = 618; Expected = Linux ?
                    "30f2bd0521a68902d2acce8687014f10f6f92938e4e2690576308311ab475bc5" :
                    "ae7730d132edacf55b32d62947aeba9149cdac022862bc7266456d629d8d7f17"; }
                else if (Method.DeclaringType == typeof(BaseCombatEntity)) { Token = 0x0600149b; Size = 337;
                    Expected = "ab193eb2cb6a1abe502927eb2ca174993e02674b8883b265a2931e12b661c363"; }
                else if (Method.Name == "OnDied") { Token = 0x06001abd; Size = 2861; Expected = Linux ?
                    "2462879e57b67cbac0ea8f0e079c353cca437458f422554cf1de51d12ec40b1b" :
                    "617412ad28c15aedd49143a2057e1b4c9c72ce835c97e12b1bc864a83576e9c7"; }
                else { Token = 0x06001aea; Size = 210; Expected = "3e6d22f6ace067b6b16a8cc209946f91d39c605bd6c43e5aa61b190e5662b731"; }
                MethodBody Body = Method.GetMethodBody();
                byte[] Bytes = Body.GetILAsByteArray();
                if (Method.MetadataToken != Token || Bytes.Length != Size) {
                    Failure = "token=0x" + Method.MetadataToken.ToString("x8") + "/size=" + Bytes.Length; return false;
                }
                if (!GameplayEhPinned(Body, Token)) { Failure = "exception regions"; return false; }
                using (SHA256 Hash = SHA256.Create()) {
                    string Actual = BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
                    bool Valid = String.Equals(Actual, Expected, StringComparison.Ordinal);
                    Failure = Valid ? "pinned" : "IL=" + Actual;
                    return Valid;
                }
            }
            catch (Exception Error) { Failure = Error.GetType().Name; return false; }
        }
        private static bool GameplayEhPinned(MethodBody Body, int Token)
        {
            // Independently matched original, reproduced and live receipts.
            // Every recorded handler is Finally; RespawnAt has zero clauses.
            int[] Expected;
            if (Token == 0x06001abe) return Body.ExceptionHandlingClauses.Count == 0;
            if (Token == 0x06001aea) Expected = new[] { 12, 187, 199, 10 };
            else if (Token == 0x0600149b) Expected = new[] { 308, 16, 324, 12 };
            else if (Token == 0x06001abd) Expected = new[] {
                62, 40, 102, 14, 522, 101, 623, 14,
                1062, 128, 1190, 14, 2625, 18, 2643, 12
            };
            else return false;
            if (Body.ExceptionHandlingClauses.Count != Expected.Length / 4) return false;
            for (int Index = 0; Index < Expected.Length / 4; Index++) {
                ExceptionHandlingClause Clause = Body.ExceptionHandlingClauses[Index];
                int At = Index * 4;
                if (Clause.Flags != ExceptionHandlingClauseOptions.Finally ||
                    Clause.TryOffset != Expected[At] || Clause.TryLength != Expected[At + 1] ||
                    Clause.HandlerOffset != Expected[At + 2] || Clause.HandlerLength != Expected[At + 3]) return false;
            }
            return true;
        }
        private bool OwnGameplayPatch(HarmonyLib.Patch Patch, Type Type, string Name)
        {
            MethodInfo Expected = Type.GetMethod(Name, BindingFlags.Static | BindingFlags.NonPublic);
            return Patch != null && Patch.PatchMethod != null && Expected != null &&
                ReferenceEquals(Patch.PatchMethod.Module, Expected.Module) && SameEntityMethod(Patch.PatchMethod, Expected);
        }
        private bool PinnedGameplayHook(HarmonyLib.Patch Patch, string Name)
        {
            MethodInfo Method = Patch == null ? null : Patch.PatchMethod;
            if (Method == null || Name == null || Method.DeclaringType == null ||
                Method.DeclaringType.FullName != Name || Method.Name != "Transpiler" || Patch.priority != 700) return false;
            Assembly Hooks = Method.Module.Assembly;
            return Hooks.GetName().Name == "Carbon.Hooks.Oxide" && Hooks.GetName().Version.ToString() == "2.0.0.0" &&
                Hooks.ManifestModule.ModuleVersionId.ToString("D") == GameplayHooksMvid;
        }
        private bool VerifyGameplayMethodTopology(MethodInfo Target, Type Own, string OptionalHook, bool Marker)
        {
            HarmonyLib.Patches Patches = Harmony.GetPatchInfo(Target);
            if (Patches == null) return Own == null;
            if (Patches.InnerPrefixes.Count != 0 || Patches.InnerPostfixes.Count != 0 ||
                Patches.Prefixes.Count != (Own == null ? 0 : 1) ||
                Patches.Postfixes.Count != (Own == null ? 0 : 1) ||
                Patches.Finalizers.Count != (Own == null ? 0 : 1) ||
                Patches.Transpilers.Count < (Marker ? 1 : 0) || Patches.Transpilers.Count > (Marker ? 2 : 1)) return false;
            if (Own != null && (!OwnGameplayPatch(Patches.Prefixes[0], Own, "Prefix") || Patches.Prefixes[0].priority != Priority.First ||
                !OwnGameplayPatch(Patches.Postfixes[0], Own, "Postfix") || Patches.Postfixes[0].priority != Priority.Last ||
                !OwnGameplayPatch(Patches.Finalizers[0], Own, "Finalizer") || Patches.Finalizers[0].priority != Priority.Last)) return false;
            int OwnMarkers = 0, Carbon = 0;
            foreach (HarmonyLib.Patch Patch in Patches.Transpilers) {
                if (Marker && OwnGameplayPatch(Patch, Own, "Transpiler") && Patch.priority == Priority.Last) OwnMarkers++;
                else if (PinnedGameplayHook(Patch, OptionalHook)) Carbon++;
                else return false;
            }
            return OwnMarkers == (Marker ? 1 : 0) && Carbon <= (OptionalHook == null ? 0 : 1);
        }
        private bool InitializeGameplayPatchState()
        {
            GameplayPatchStamps.Clear();
            Assembly Assembly = typeof(Harmony).Assembly;
            Type Shared = Assembly.GetType("HarmonyLib.HarmonySharedState", false);
            Type Processor = Assembly.GetType("HarmonyLib.PatchProcessor", false);
            GameplayPatchStateField = Shared == null ? null : Shared.GetField("state", BindingFlags.Static | BindingFlags.NonPublic);
            GameplayPatchLockField = Processor == null ? null : Processor.GetField("locker", BindingFlags.Static | BindingFlags.NonPublic);
            GameplayPatchState = GameplayPatchStateField == null ? null : GameplayPatchStateField.GetValue(null) as IDictionary;
            GameplayPatchLock = GameplayPatchLockField == null ? null : GameplayPatchLockField.GetValue(null);
            return GameplayPatchState != null && GameplayPatchLock != null && !ReferenceEquals(GameplayPatchState, GameplayPatchLock);
        }
        private bool VerifyGameplayTopology(bool Death, out bool Busy)
        {
            Busy = false;
            IDictionary State = GameplayPatchState;
            object PatchLock = GameplayPatchLock;
            if (State == null || PatchLock == null || GameplayPatchStateField == null || GameplayPatchLockField == null ||
                (Death && !GameplayDeathMarkerValid)) return false;
            // Pinned Harmony.GetPatchInfo takes PatchProcessor.locker and then
            // HarmonySharedState.state. Acquire BOTH with zero wait first; its
            // nested Monitor.Enter calls are then reentrant, never new waits.
            if (!Monitor.TryEnter(PatchLock, 0)) { Busy = true; return false; }
            try {
                if (!Monitor.TryEnter(State, 0)) { Busy = true; return false; }
                try {
                    if (!ReferenceEquals(GameplayPatchLockField.GetValue(null), PatchLock) ||
                        !ReferenceEquals(GameplayPatchStateField.GetValue(null), State) || State.Count > GameplayMaximumPatchRecords) return false;
                    MethodInfo[] Targets = Death ? GameplayDeathTargets : GameplaySpawnTargets;
                    foreach (MethodInfo Target in Targets) {
                        if (Target == null) return false;
                        object Record = State[Target];
                        byte[] Stamp = Record as byte[], Expected;
                        if ((Record != null && Stamp == null) || (Stamp != null && Stamp.Length > GameplayMaximumPatchBytes)) return false;
                        if (GameplayPatchStamps.TryGetValue(Target, out Expected) && ReferenceEquals(Expected, Stamp)) continue;
                        Type Own = Target == GameplayDieMethod ? typeof(GameplayDeathPatch) :
                            Target == GameplayRespawnMethod ? typeof(GameplaySpawnPatch) : null;
                        string Hook = Target == GameplayDieMethod ? GameplayDeathHook :
                            Target == GameplayBaseDieMethod ? GameplayEntityDeathHook :
                            Target == GameplayRespawnMethod ? GameplayRespawnHook : null;
                        if (!VerifyGameplayMethodTopology(Target, Own, Hook, Target == GameplayDieMethod)) return false;
                        GameplayPatchStamps[Target] = Stamp;
                    }
                    return true;
                }
                finally { Monitor.Exit(State); }
            }
            finally { Monitor.Exit(PatchLock); }
        }

        private PlayerLifetime CurrentGameplayPlayer(BasePlayer Player, PlayerLifetime Expected = null)
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) return null;
            if (Gameplay == null || Player == null || Player.IsDestroyed || Player.IsNpc || !Player.IsConnected ||
                Player.Connection == null || !Player.Connection.connected || !Player.Connection.active ||
                !ReferenceEquals(Player.Connection.player, Player)) return null;
            PlayerLifetime Lifetime = Gameplay.Players.Find(Player.UserIDString);
            if (Lifetime == null || !ReferenceEquals(Lifetime.Identity, Player) ||
                !ReferenceEquals(Lifetime.Connection, Player.Connection) ||
                (Expected != null && (!ReferenceEquals(Expected, Lifetime) || Expected.Token != Lifetime.Token))) return null;
            return Lifetime;
        }
        private GameplayFrame EnterGameplay(BasePlayer Player, string Kind)
        {
            bool Death = Kind == "died";
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) { RejectGameplayOffThread(Death); return null; }
            DrainGameplayThreadDiagnostics();
            if (!(Death ? GameplayDeathQualified : GameplaySpawnQualified)) return null;
            bool Busy;
            if (!VerifyGameplayTopology(Death, out Busy)) {
                if (Busy) CountGameplay(ref GameplayLifecycleRejected);
                else RejectGameplayCapability(Death, "patch topology drift");
                return null;
            }
            PlayerLifetime Lifetime = CurrentGameplayPlayer(Player);
            if (Lifetime == null) { CountGameplay(ref GameplayLifecycleRejected); return null; }
            if (GameplayFrames.Count >= GameplayMaximumFrames) {
                foreach (GameplayFrame Pending in GameplayFrames) Pending.Poisoned = true;
                CountGameplay(ref GameplayLifecycleRejected); return null;
            }
            GameplayLife Life;
            if (!GameplayLives.TryGetValue(Lifetime.Token, out Life)) {
                if (GameplayLives.Count >= FacadePolicy.Players) { CountGameplay(ref GameplayLifecycleRejected); return null; }
                Life = new GameplayLife { UserId = Lifetime.UserId }; GameplayLives.Add(Lifetime.Token, Life);
            }
            var Frame = new GameplayFrame { Observer = this, Player = Player, Lifetime = Lifetime, Life = Life,
                Kind = Kind, EntryAlive = Player.IsAlive(), EntryRespawnId = Player.respawnId };
            if (Frame.EntryRespawnId != null && Frame.EntryRespawnId.Length > 32) {
                Frame.EntryRespawnId = null; Frame.Poisoned = true;
            }
            foreach (GameplayFrame Pending in GameplayFrames) {
                if (!ReferenceEquals(Pending.Player, Player)) continue;
                Pending.Poisoned = Frame.Poisoned = true; CountGameplay(ref GameplayLifecycleNested);
            }
            GameplayFrames.Add(Frame); return Frame;
        }
        private static void GameplayBaseDeathReturned(BasePlayer Player)
        {
            CarbonLuau Observer = ActiveGameplayObserver;
            if (Observer == null) return;
            if (Thread.CurrentThread.ManagedThreadId != Observer.GameplayLifecycleOwner) { Observer.RejectGameplayOffThread(true); return; }
            if (Observer.GameplayFrames.Count == 0) return;
            GameplayFrame Frame = Observer.GameplayFrames[Observer.GameplayFrames.Count - 1];
            if (Frame.Kind == "died" && ReferenceEquals(Frame.Player, Player)) Frame.BaseReturned = true;
        }
        private void CompleteGameplayPostfix(bool OriginalRan, GameplayFrame Frame, bool Death)
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) { RejectGameplayOffThread(Death); return; }
            if (Frame != null) { Frame.PostfixSeen = true; Frame.OriginalRan = OriginalRan; }
        }
        private void ExitGameplay(Exception Error, GameplayFrame Frame, HitInfo Hit, bool Death)
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) { RejectGameplayOffThread(Death); return; }
            DrainGameplayThreadDiagnostics();
            if (Frame == null || Frame.Exited) return;
            Frame.Exited = true;
            try {
                if (GameplayFrames.Count == 0 || !ReferenceEquals(GameplayFrames[GameplayFrames.Count - 1], Frame)) {
                    CountGameplay(ref GameplayLifecycleRejected); return;
                }
                if (!ReferenceEquals(ActiveGameplayObserver, this) ||
                    !(Death ? GameplayDeathQualified : GameplaySpawnQualified) || Frame.Poisoned || Error != null ||
                    !Frame.PostfixSeen || !Frame.OriginalRan) { CountGameplay(ref GameplayLifecycleRejected); return; }
                bool Busy;
                if (!VerifyGameplayTopology(Death, out Busy)) {
                    if (Busy) CountGameplay(ref GameplayLifecycleRejected);
                    else RejectGameplayCapability(Death, "completion patch topology drift");
                    return;
                }
                PlayerLifetime Player = CurrentGameplayPlayer(Frame.Player, Frame.Lifetime);
                GameplayLife Life;
                if (Player == null || !GameplayLives.TryGetValue(Player.Token, out Life) || !ReferenceEquals(Life, Frame.Life)) {
                    CountGameplay(ref GameplayLifecycleRejected); return;
                }
                if (Death) {
                    if (!Frame.EntryAlive || !Frame.BaseReturned || !Frame.Player.IsDead() || Life.Dead) {
                        CountGameplay(ref GameplayLifecycleRejected); return;
                    }
                    Life.Dead = true; CountGameplay(ref GameplayLifecycleDeaths);
                }
                else {
                    float Health = Frame.Player.Health();
                    if (!Frame.Player.IsAlive() || Frame.Player.IsWounded() || Single.IsNaN(Health) || Single.IsInfinity(Health) || Health <= 0 ||
                        String.IsNullOrEmpty(Frame.Player.respawnId) || Frame.Player.respawnId.Length != 32 ||
                        String.Equals(Frame.EntryRespawnId, Frame.Player.respawnId, StringComparison.Ordinal) || Life.Epoch == UInt64.MaxValue) {
                        CountGameplay(ref GameplayLifecycleRejected); return;
                    }
                    Life.Epoch++; Life.Dead = false; CountGameplay(ref GameplayLifecycleSpawns);
                }
                Func<string, bool> Admission = GameplayCaptureAdmission;
                Action<GameplayHostObservation> Receiver = GameplayObservationReceiver;
                ulong CompletedEpoch = Life.Epoch;
                string CompletedRespawnId = Death ? null : Frame.Player.respawnId;
                if (Admission == null || Receiver == null || !Admission(Frame.Kind)) { CountGameplay(ref GameplayLifecycleDrops); return; }
                // Admission may call trusted managed code: revalidate before any
                // host snapshot and retain no host reference in the result.
                if (Frame.Poisoned || Life.Epoch != CompletedEpoch || CurrentGameplayPlayer(Frame.Player, Player) == null ||
                    (Death ? !Frame.Player.IsDead() : !Frame.Player.IsAlive() || Frame.Player.IsWounded() ||
                        !String.Equals(CompletedRespawnId, Frame.Player.respawnId, StringComparison.Ordinal))) {
                    CountGameplay(ref GameplayLifecycleDrops); return;
                }
                PlayerPosition? Position = ObserveGameplayPosition(Frame.Player, Player);
                PlayerLifetime Killer = null;
                if (Death && Hit != null) {
                    BasePlayer Initiator = Hit.Initiator as BasePlayer;
                    if (Initiator != null) Killer = CurrentGameplayPlayer(Initiator);
                }
                Receiver(new GameplayHostObservation(Frame.Kind, Player, Position, Killer));
            }
            catch (Exception) { CountGameplay(ref GameplayLifecycleDrops); }
            finally {
                int Index = GameplayFrames.IndexOf(Frame);
                if (Index >= 0) GameplayFrames.RemoveAt(Index);
                Frame.Player = null; Frame.Lifetime = null; Frame.Life = null; Frame.Observer = null;
            }
        }
        private PlayerPosition? ObserveGameplayPosition(BasePlayer Player, PlayerLifetime Lifetime)
        {
            if (Thread.CurrentThread.ManagedThreadId != GameplayLifecycleOwner) return null;
            if (EntityObserverBroken || !EntityStartupQualified || EntityLifetimes == null || EntityPositionReader == null ||
                !EntityPositionReader.Available || !EntityLifetimes.HasCatalogObservation(Player) || !EntityDiscoveryPatchCurrent(Player)) return null;
            EntityPositionComposition.Position Position;
            if (!EntityPositionReader.TryObserve(Player, out Position) || !EntityLifetimes.HasCatalogObservation(Player) ||
                CurrentGameplayPlayer(Player, Lifetime) == null) return null;
            CountGameplay(ref GameplayLifecyclePositions);
            return new PlayerPosition(Position.X, Position.Y, Position.Z);
        }

        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class GameplayDeathPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            { return GameplayMethodPinned(GameplayDieMethod, true, out GameplayDeathPreparation) ? new MethodBase[] { GameplayDieMethod } : new MethodBase[0]; }
            [HarmonyPrepare]
            private static bool Prepare() { return GameplayMethodPinned(GameplayDieMethod, true, out GameplayDeathPreparation); }
            [HarmonyTranspiler, HarmonyPriority(Priority.Last)]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions)
            {
                var Source = new List<CodeInstruction>(Instructions);
                int Count = 0;
                foreach (CodeInstruction Instruction in Source)
                    if (Instruction.opcode == OpCodes.Call && SameEntityMethod(Instruction.operand as MethodBase, GameplayBaseDieMethod)) Count++;
                GameplayDeathMarkerValid = Count == 1 && Source.Count <= 256;
                if (!GameplayDeathMarkerValid) return Source;
                var Result = new List<CodeInstruction>(Source.Count + 2);
                MethodInfo Marker = typeof(CarbonLuau).GetMethod("GameplayBaseDeathReturned", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (CodeInstruction Instruction in Source) {
                    Result.Add(Instruction);
                    if (Instruction.opcode == OpCodes.Call && SameEntityMethod(Instruction.operand as MethodBase, GameplayBaseDieMethod)) {
                        Result.Add(new CodeInstruction(OpCodes.Ldarg_0)); Result.Add(new CodeInstruction(OpCodes.Call, Marker));
                    }
                }
                return Result;
            }
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(BasePlayer __instance, out GameplayFrame __state)
            {
                __state = null; CarbonLuau Observer = ActiveGameplayObserver;
                if (Observer == null) return;
                try { __state = Observer.EnterGameplay(__instance, "died"); }
                catch (Exception) { Observer.RejectGameplayCapability(true, "death entry"); }
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, GameplayFrame __state)
            { if (__state != null && __state.Observer != null) __state.Observer.CompleteGameplayPostfix(__runOriginal, __state, true); }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, GameplayFrame __state, HitInfo __0)
            { if (__state != null && __state.Observer != null) __state.Observer.ExitGameplay(__exception, __state, __0, true); }
        }
        [AutoPatch(IsRequired = true), HarmonyPatch]
        private static class GameplaySpawnPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            { return GameplayMethodPinned(GameplayRespawnMethod, false, out GameplaySpawnPreparation) ? new MethodBase[] { GameplayRespawnMethod } : new MethodBase[0]; }
            [HarmonyPrepare]
            private static bool Prepare() { return GameplayMethodPinned(GameplayRespawnMethod, false, out GameplaySpawnPreparation); }
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(BasePlayer __instance, out GameplayFrame __state)
            {
                __state = null; CarbonLuau Observer = ActiveGameplayObserver;
                if (Observer == null) return;
                try { __state = Observer.EnterGameplay(__instance, "spawned"); }
                catch (Exception) { Observer.RejectGameplayCapability(false, "spawn entry"); }
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(bool __runOriginal, GameplayFrame __state)
            { if (__state != null && __state.Observer != null) __state.Observer.CompleteGameplayPostfix(__runOriginal, __state, false); }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
            private static void Finalizer(Exception __exception, GameplayFrame __state)
            { if (__state != null && __state.Observer != null) __state.Observer.ExitGameplay(__exception, __state, null, false); }
        }
    }
}
